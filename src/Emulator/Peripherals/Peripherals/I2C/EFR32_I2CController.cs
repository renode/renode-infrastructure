//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class EFR32_I2CController : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral
    {
        public EFR32_I2CController(Machine machine) : base(machine)
        {
            IRQ = new GPIO();
            txBuffer = new Queue<byte>();
            rxBuffer = new Queue<byte>();
            interruptsManager = new InterruptManager<Interrupt>(this);

            var map = new Dictionary<long, DoubleWordRegister> {
                {(long)Registers.ReceiveBufferData, new DoubleWordRegister(this)
                        .WithValueField(0, 8, FieldMode.Read, name: "RXDATA", valueProviderCallback: _ =>
                        {
                            var result = rxBuffer.Dequeue();
                            interruptsManager.SetInterrupt(Interrupt.ReceiveDataValid, rxBuffer.Any());
                            return result;
                        })
                },
                {(long)Registers.ReceiveBufferDataPeek, new DoubleWordRegister(this)
                        .WithValueField(0, 8, FieldMode.Read, name: "RXDATAP", valueProviderCallback: _ => rxBuffer.Peek())
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                        .WithValueField(0, 8, FieldMode.Write, name: "COMMAND", writeCallback: (_, v) => HandleCommand((Command)v))
                },
                {(long)Registers.TransmitBufferData, new DoubleWordRegister(this)
                        .WithValueField(0, 8, FieldMode.Write, name: "TXDATA", writeCallback: (_, v) => LoadTxData((byte)v))
                }
            };

            map.Add((long)Registers.InterruptFlag, interruptsManager.GetMaskedInterruptFlagRegister<DoubleWordRegister>());
            map.Add((long)Registers.InterruptEnable, interruptsManager.GetInterruptEnableRegister<DoubleWordRegister>());
            map.Add((long)Registers.InterruptFlagSet, interruptsManager.GetMaskedInterruptFlagRegister<DoubleWordRegister>());
            map.Add((long)Registers.InterruptFlagClear, interruptsManager.GetInterruptClearRegister<DoubleWordRegister>());

            registers = new DoubleWordRegisterCollection(this, map);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            currentAddress = 0;
            isWrite = false;
            waitingForAddressByte = false;
            txBuffer.Clear();
            rxBuffer.Clear();
            registers.Reset();
            interruptsManager.Reset();
        }

        [IrqProvider]
        public GPIO IRQ { get; private set; }

        private void HandleCommand(Command command)
        {
            foreach(var c in Enum.GetValues(typeof(Command)).Cast<Command>().Where(x => command.HasFlag(x)))
            {
                switch(c)
                {
                case Command.SendStartCondition:
                    interruptsManager.SetInterrupt(Interrupt.StartCondition);
                    switch(txBuffer.Count)
                    {
                    case 0:
                        // the first byte contains device address and R/W flag; we have to wait for it
                        waitingForAddressByte = true;
                        interruptsManager.SetInterrupt(Interrupt.BusHold);
                        // TODO: here we should also set I2Cn_STATE to 0x57 according to p.442
                        break;
                    case 1:
                        // there is a byte address waiting already in the buffer
                        HandleAddressByte();
                        break;
                    default:
                        this.Log(LogLevel.Error, $"Unexpected number of bytes in tx buffer when handling SendStartCondition command: {txBuffer.Count}");
                        this.Log(LogLevel.Warning, $"Dropping content of tx buffer");
                        txBuffer.Clear();
                        break;
                    }
                    break;

                case Command.SendStopCondition:
                    interruptsManager.SetInterrupt(Interrupt.MasterStopCondition);
                    if(isWrite)
                    {
                        WriteToSlave(currentAddress, txBuffer);
                        txBuffer.Clear();
                    }
                    break;

                default:
                    this.Log(LogLevel.Warning, "Received unsupported command: {0}", command);
                    break;
                }
            }
        }

        private void LoadTxData(byte value)
        {
            txBuffer.Enqueue((byte)value);
            if(waitingForAddressByte)
            {
                HandleAddressByte();
            }
            else
            {
                interruptsManager.SetInterrupt(Interrupt.AcknowledgeReceived);
            }
        }

        private void HandleAddressByte()
        {
            currentAddress = txBuffer.Dequeue();
            isWrite = (currentAddress & 0x1) == 0;
            currentAddress >>= 1;

            if(!isWrite)
            {
                ReadSlave();
            }
            interruptsManager.SetInterrupt(Interrupt.AcknowledgeReceived);
            interruptsManager.ClearInterrupt(Interrupt.BusHold);
            waitingForAddressByte = false;
        }

        private void ReadSlave()
        {
            // Fetch packet list from slave device
            if(!TryGetByAddress(currentAddress, out II2CPeripheral slave))
            {
                this.Log(LogLevel.Warning, "Trying to read from nonexisting slave with address \"{0}\"", currentAddress);
                return;
            }
            var rxArray = slave.Read();
            this.Log(LogLevel.Noisy, "Devices returned {0} bytes of data.", rxArray.Length);
            foreach(var b in rxArray)
            {
                rxBuffer.Enqueue(b);
            }
            interruptsManager.SetInterrupt(Interrupt.ReceiveDataValid, rxBuffer.Any());
        }

        private void WriteToSlave(int slaveAddress, IEnumerable<byte> data)
        {
            if(!TryGetByAddress(slaveAddress, out II2CPeripheral slave))
            {
                this.Log(LogLevel.Warning, "Trying to write to nonexisting slave with address \"{0}\"", slaveAddress);
                return;
            }

            slave.Write(data.ToArray());
        }

        private int currentAddress;
        private bool isWrite;
        private bool waitingForAddressByte;
        private readonly Queue<byte> txBuffer;
        private readonly Queue<byte> rxBuffer;
        private readonly DoubleWordRegisterCollection registers;
        private readonly InterruptManager<Interrupt> interruptsManager;

        private enum Registers
        {
            Control = 0x00,
            Command = 0x04,
            State = 0x08,
            Status = 0x0C,
            ClockDivision = 0x10,
            SlaveAddress = 0x14,
            SlaveAddressMask = 0x18,
            ReceiveBufferData = 0x1C,
            ReceiveBufferDoubleData = 0x20,
            ReceiveBufferDataPeek = 0x24,
            ReceiveBufferDoubleDataPeek = 0x28,
            TransmitBufferData = 0x2C,
            TransmitBufferDoubleData = 0x30,
            InterruptFlag = 0x34,
            InterruptFlagSet = 0x38,
            InterruptFlagClear = 0x3C,
            InterruptEnable = 0x40,
            IORoutingPinEnable = 0x44,
            IORoutingLocation = 0x48
        }

        [Flags]
        private enum Command
        {
            SendStartCondition = 0x01,
            SendStopCondition = 0x02,
            SendAck = 0x04,
            SendNotAck = 0x08,
            ContinueTransmission = 0x10,
            AbortTransmission = 0x20,
            ClearTransmitBufferAndShiftRegister = 0x40,
            ClearPendingCommands = 0x80
        }

        private enum Interrupt
        {
            StartCondition = 0x00,
            RepeatedStartCondition = 0x01,
            Address = 0x02,
            TransferCompleted = 0x03,
            [NotSettable]
            TransmitBufferLevel = 0x04,
            [NotSettable]
            ReceiveDataValid = 0x05,
            AcknowledgeReceived = 0x06,
            NotAcknowledgeReceived = 0x07,
            MasterStopCondition = 0x08,
            ArbitrationLost = 0x09,
            BusError = 0x0A,
            BusHold = 0x0B,
            TransmitBufferOverflow = 0x0C,
            ReceiveBufferUnderflow = 0x0D,
            BusIdleTimeout = 0x0E,
            ClockLowTimeout = 0x0F,
            SlaveStopCondition = 0x10,
            ReceiveBufferFull = 0x11,
            ClockLowError = 0x12
        }
    }
}
