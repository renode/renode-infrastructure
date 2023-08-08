//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class MAX32650_I2C : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public MAX32650_I2C(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();

            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            txQueue = new Queue<byte>();
            rxQueue = new Queue<byte>();
        }

        public override void Reset()
        {
            state = States.Idle;
            destinationAddress = 0;
            bytesToReceive = 0;
            enabled = false;

            registers.Reset();
            txQueue.Clear();
            rxQueue.Clear();
            IRQ.Unset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        private void HandleTransaction()
        {
            if(!enabled)
            {
                // We are ignoring calls to HandleTransaction when I2C peripheral is disabled
                return;
            }

            switch(state)
            {
                case States.Ready:
                    // We are being called as a result of START condition
                    while(state == States.Ready || state == States.WaitForAddress)
                    {
                        if(txQueue.TryDequeue(out var data))
                        {
                            HandleWriteByte(data);
                        }
                        else
                        {
                            state = States.Idle;
                            break;
                        }
                    }
                    break;

                case States.Writing:
                    // We are being called as a result of RESTART/STOP condition
                    CurrentSlave.Write(txQueue.ToArray());
                    txQueue.Clear();
                    interruptDonePending.Value = true;
                    break;

                default:
                    // We don't have any writes to do; ignore
                    break;
            }

            UpdateInterrupts();
        }

        private void HandleWriteByte(byte data)
        {
            if(!enabled)
            {
                this.Log(LogLevel.Warning, "Trying to write byte to FIFO, but peripheral is disabled");
                return;
            }

            switch(state)
            {
                case States.Idle:
                case States.Writing:
                    txQueue.Enqueue(data);
                    break;

                case States.Ready:
                    if(slaveExtendedAddress.Value)
                    {
                        destinationAddress = (uint)data & 0x3;
                        state = States.WaitForAddress;
                    }
                    else
                    {
                        state = ((data & 0x1) != 0) ? States.Reading : States.Writing;
                        destinationAddress = (uint)(data >> 1) & 0x7F;

                        if(CurrentSlave == null)
                        {
                            this.Log(LogLevel.Warning, "Trying to access a peripheral at an address 0x{0:X02}, but no such peripheral is connected", destinationAddress);
                            interruptTimeoutPending.Value = true;
                            state = States.Idle;
                            txQueue.Clear();
                        }
                    }

                    TryReadingToBuffer();
                    break;

                case States.WaitForAddress:
                    state = ((destinationAddress & 0x1) != 0) ? States.Reading : States.Writing;
                    destinationAddress = (destinationAddress & 0x6) << 7;
                    destinationAddress |= data;

                    if(CurrentSlave == null)
                    {
                        this.Log(LogLevel.Warning, "Trying to access a peripheral at an address 0x{0:X03}, but no such peripheral is connected", destinationAddress);
                        interruptTimeoutPending.Value = true;
                        state = States.Idle;
                        txQueue.Clear();
                    }

                    TryReadingToBuffer();
                    break;

                case States.ReadingFromBuffer:
                    this.Log(LogLevel.Warning, "Writing data to FIFO while in reading state, ignoring incoming data.");
                    break;

                default:
                    throw new Exception($"Unhandled state in HandleWriteByte: {state}");
            }

            UpdateInterrupts();
        }

        private byte HandleReadByte()
        {
            if(!enabled)
            {
                this.Log(LogLevel.Error, "Trying to read byte from FIFO, but peripheral is disabled");
                return DefaultReturnValue;
            }

            byte result = DefaultReturnValue;
            if(rxQueue.TryDequeue(out var data))
            {
                result = data;
            }
            else
            {
                interruptDonePending.Value = true;
            }

            UpdateInterrupts();
            return result;
        }

        private void TryReadingToBuffer()
        {
            if(state != States.Reading)
            {
                return;
            }

            if(rxQueue.Count > 0)
            {
                this.Log(LogLevel.Warning, "Receiving new data with non-empty RX queue");
            }

            var receivedBytes = CurrentSlave.Read(bytesToReceive);
            if(receivedBytes.Length != bytesToReceive)
            {
                this.Log(LogLevel.Warning, "Requested {0} bytes, but received {1}", bytesToReceive, rxQueue.Count);
            }

            foreach(var value in receivedBytes)
            {
                rxQueue.Enqueue(value);
            }

            state = States.ReadingFromBuffer;
        }

        private void UpdateInterrupts()
        {
            interruptRxThresholdPending.Value = rxQueue.Count >= (int)rxThreshold.Value;
            interruptTxThresholdPending.Value = txQueue.Count <= (int)txThreshold.Value;

            var pending = false;
            pending |= interruptTimeoutEnabled.Value && interruptTimeoutPending.Value;
            pending |= interruptRxThresholdEnabled.Value && interruptRxThresholdPending.Value;
            pending |= interruptTxThresholdEnabled.Value && interruptTxThresholdPending.Value;
            pending |= interruptStopEnabled.Value && interruptStopPending.Value;
            pending |= interruptDoneEnabled.Value && interruptDonePending.Value;
            IRQ.Set(pending);
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>()
            {
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, name: "STAT.busy",
                        valueProviderCallback: _ => state != States.Idle)
                    .WithFlag(1, FieldMode.Read, name: "STAT.rxe",
                        valueProviderCallback: _ => rxQueue.Count == 0)
                    .WithFlag(2, FieldMode.Read, name: "STAT.rxf",
                        valueProviderCallback: _ => false)
                    .WithFlag(3, FieldMode.Read, name: "STAT.txe",
                        valueProviderCallback: _ => true)
                    .WithFlag(4, FieldMode.Read, name: "STAT.txf",
                        valueProviderCallback: _ => false)
                    .WithFlag(5, FieldMode.Read, name: "STAT.chmd",
                        valueProviderCallback: _ => state != States.Idle)
                    .WithReservedBits(6, 26)
                },
                {(long)Registers.Control0, new DoubleWordRegister(this)
                    .WithFlag(0, name: "CTRL0.i2cen",
                        changeCallback: (_, value) =>
                        {
                            state = States.Idle;
                            enabled = value;
                        })
                    .WithFlag(1, name: "CTRL0.mst",
                        changeCallback: (_, value) =>
                        {
                            if(!value)
                            {
                                this.Log(LogLevel.Warning, "CTRL0.mst has been set to false, but only Master mode is supported; ignoring");
                            }
                            state = States.Idle;
                        })
                    .WithTaggedFlag("CTRL0.gcen", 2)
                    .WithTaggedFlag("CTRL0.irxm", 3)
                    .WithTaggedFlag("CTRL0.ack", 4)
                    .WithReservedBits(5, 1)
                    .WithFlag(6, out var sclOutput, name: "CTRL0.sclo")
                    .WithFlag(7, out var sdaOutput, name: "CTRL0.sdao")
                    .WithFlag(8, FieldMode.Read, name: "CTRL0.scl",
                        valueProviderCallback: _ => sclOutput.Value)
                    .WithFlag(9, FieldMode.Read, name: "CTRL0.sda",
                        valueProviderCallback: _ => sdaOutput.Value)
                    .WithFlag(10, name: "CTRL0.swoe")
                    .WithTaggedFlag("CTRL0.read", 11)
                    .WithTaggedFlag("CTRL0.scl_strd", 12)
                    .WithTaggedFlag("CTRL0.scl_ppm", 13)
                    .WithReservedBits(14, 1)
                    .WithTaggedFlag("CTRL0.hsmode", 15)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.InterruptFlags0, new DoubleWordRegister(this)
                    .WithFlag(0, out interruptDonePending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL0.donei")
                    .WithTaggedFlag("INT_FL0.irxmi", 1)
                    .WithTaggedFlag("INT_FL0.gci", 2)
                    .WithTaggedFlag("INT_FL0.ami", 3)
                    .WithFlag(4, out interruptRxThresholdPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL0.rxthi")
                    .WithFlag(5, out interruptTxThresholdPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL0.txthi")
                    .WithFlag(6, out interruptStopPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL0.stopi")
                    .WithTaggedFlag("INT_FL0.adracki", 7)
                    .WithTaggedFlag("INT_FL0.arberi", 8)
                    .WithFlag(9, out interruptTimeoutPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL0.toeri")
                    .WithTaggedFlag("INT_FL0.adreri", 10)
                    .WithTaggedFlag("INT_FL0.dateri", 11)
                    .WithTaggedFlag("INT_FL0.dnreri", 12)
                    .WithTaggedFlag("INT_FL0.strteri", 13)
                    .WithTaggedFlag("INT_FL0.stoperi", 14)
                    // We are using flag instead of tagged flag to hush down unnecessary logs
                    .WithFlag(15, FieldMode.WriteOneToClear, name: "INT_FL0.txloi")
                    .WithReservedBits(16, 16)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable0, new DoubleWordRegister(this)
                    .WithFlag(0, out interruptDoneEnabled, name: "INT_EN0.donei")
                    .WithTaggedFlag("INT_EN0.irxmi", 1)
                    .WithTaggedFlag("INT_EN0.gci", 2)
                    .WithTaggedFlag("INT_EN0.ami", 3)
                    .WithFlag(4, out interruptRxThresholdEnabled, name: "INT_EN0.rxthi")
                    .WithFlag(5, out interruptTxThresholdEnabled, name: "INT_EN0.txthi")
                    .WithFlag(6, out interruptStopEnabled, name: "INT_EN0.stopi")
                    .WithTaggedFlag("INT_EN0.adracki", 7)
                    .WithTaggedFlag("INT_EN0.arberi", 8)
                    .WithFlag(9, out interruptTimeoutEnabled, name: "INT_EN0.toeri")
                    .WithTaggedFlag("INT_EN0.adreri", 10)
                    .WithTaggedFlag("INT_EN0.dateri", 11)
                    .WithTaggedFlag("INT_EN0.dnreri", 12)
                    .WithTaggedFlag("INT_EN0.strteri", 13)
                    .WithTaggedFlag("INT_EN0.stoperi", 14)
                    .WithTaggedFlag("INT_EN0.txloi", 15)
                    .WithReservedBits(16, 16)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.ReceiveControl0, new DoubleWordRegister(this)
                    .WithTaggedFlag("RX_CTRL0.dnr", 0)
                    .WithReservedBits(1, 6)
                    .WithFlag(7, FieldMode.Read | FieldMode.WriteOneToClear, name: "RX_CTRL0.rxfsh",
                        writeCallback: (_, value) => { if(value) rxQueue.Clear(); })
                    .WithValueField(8, 4, out rxThreshold, name: "RX_CTRL0.rxth")
                    .WithReservedBits(12, 20)
                },
                {(long)Registers.ReceiveControl1, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "RX_CTRL1.rxcnt",
                        valueProviderCallback: _ => (uint)bytesToReceive,
                        writeCallback: (_, value) => bytesToReceive = (value == 0) ? 256 : (int)value)
                    .WithValueField(8, 4, FieldMode.Read, name: "RX_CTRL1.rxfifo",
                        valueProviderCallback: _ => (uint)rxQueue.Count)
                    .WithReservedBits(12, 20)
                },
                {(long)Registers.TransmitControl0, new DoubleWordRegister(this)
                    .WithFlag(0, name: "TX_CTRL0.txpreld")
                    .WithReservedBits(1, 6)
                    .WithFlag(7, FieldMode.Read | FieldMode.WriteOneToClear, name: "TX_CTRL0.txfsh",
                        writeCallback: (_, value) => { if(value) txQueue.Clear(); })
                    .WithValueField(8, 4, out txThreshold, name: "TX_CTRL0.txth")
                    .WithReservedBits(12, 20)
                },
                {(long)Registers.FIFO, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "FIFO.data",
                        valueProviderCallback: _ => HandleReadByte(),
                        writeCallback: (_, value) => HandleWriteByte((byte)value))
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.MasterMode, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read | FieldMode.Set, name: "MSTR_MODE.start",
                        valueProviderCallback: _ => state != States.Idle,
                        writeCallback: (_, value) =>
                        {
                            if(value && state == States.Idle)
                            {
                                state = States.Ready;
                                HandleTransaction();
                            }
                        })
                    .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "MSTR_MODE.restart",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                HandleTransaction();
                                state = States.Ready;
                            }
                        },
                        valueProviderCallback: _ => false)
                    .WithFlag(2, FieldMode.Read | FieldMode.WriteOneToClear, name: "MSTR_MODE.stop",
                        writeCallback: (_, value) =>
                        {
                            if(!value)
                            {
                                return;
                            }

                            HandleTransaction();
                            interruptStopPending.Value = true;
                            interruptDonePending.Value = true;
                            state = States.Idle;
                            CurrentSlave?.FinishTransmission();
                            UpdateInterrupts();
                        })
                    .WithReservedBits(3, 4)
                    .WithFlag(7, out slaveExtendedAddress, name: "MSTR_MODE.sea")
                    .WithReservedBits(8, 24)
                }
            };
            return registerMap;
        }

        private II2CPeripheral CurrentSlave =>
            TryGetByAddress((int)destinationAddress, out var peripheral) ? peripheral : null;

        private bool enabled;
        private States state;
        private uint destinationAddress;
        private int bytesToReceive;

        private IFlagRegisterField slaveExtendedAddress;

        private IValueRegisterField rxThreshold;
        private IValueRegisterField txThreshold;

        private IFlagRegisterField interruptTimeoutPending;
        private IFlagRegisterField interruptRxThresholdPending;
        private IFlagRegisterField interruptTxThresholdPending;
        private IFlagRegisterField interruptStopPending;
        private IFlagRegisterField interruptDonePending;

        private IFlagRegisterField interruptTimeoutEnabled;
        private IFlagRegisterField interruptRxThresholdEnabled;
        private IFlagRegisterField interruptTxThresholdEnabled;
        private IFlagRegisterField interruptStopEnabled;
        private IFlagRegisterField interruptDoneEnabled;

        private readonly Queue<byte> txQueue;
        private readonly Queue<byte> rxQueue;

        private readonly DoubleWordRegisterCollection registers;

        private const byte DefaultReturnValue = 0x00;

        private enum States
        {
            Idle,
            Ready,
            WaitForAddress,
            Reading,
            ReadingFromBuffer,
            Writing,
        }

        private enum Registers
        {
            Control0 = 0x00,
            Status = 0x04,
            InterruptFlags0 = 0x08,
            InterruptEnable0 = 0x0C,
            InterruptFlags1 = 0x10,
            InterruptEnable1 = 0x14,
            FIFOLength = 0x18,
            ReceiveControl0 = 0x1C,
            ReceiveControl1 = 0x20,
            TransmitControl0 = 0x24,
            TransmitControl1 = 0x28,
            FIFO = 0x2C,
            MasterMode = 0x30,
            ClockLowTime = 0x34,
            ClockHighTime = 0x38,
            Timeout = 0x40,
            SlaveAddress = 0x44,
            DMAEnable = 0x48,
        }
    }
}
