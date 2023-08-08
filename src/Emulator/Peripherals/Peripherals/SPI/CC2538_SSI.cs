//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class CC2538_SSI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public CC2538_SSI(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();

            rxFifo = new CircularBuffer<byte>(FifoCapacity);
            txFifo = new CircularBuffer<byte>(FifoCapacity);

            var regs = new Dictionary<long, DoubleWordRegister>
            {
                { (long)Registers.Control0, new DoubleWordRegister(this)
                    .WithValueField(0, 4, writeCallback: (_, value) => {
                        if(value <= 2)
                        {
                            this.Log(LogLevel.Warning, "Trying to set as reserved value of DSS: {0}", value);
                        }
                        else if(value != 7) //8-bit data
                        {
                            this.Log(LogLevel.Warning, "An unsupported data size {0} set, only 7 (8 bits) is legal.", value);
                        }
                    }, name: "DSS")
                    .WithTag("FRF", 4, 2)
                    .WithTaggedFlag("SPO", 6)
                    .WithTaggedFlag("SPH", 7)
                    .WithTag("SCR", 8, 8)
                    .WithReservedBits(16, 16)
                },
                { (long)Registers.Control1, new DoubleWordRegister(this)
                    .WithTaggedFlag("LBM", 0)
                    .WithFlag(1, out enabled, writeCallback: EnableTransmitter, name: "SSE")
                    .WithTaggedFlag("MS", 2)
                    .WithTaggedFlag("SOD", 3)
                    .WithReservedBits(4, 28)
                },
                { (long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 16, valueProviderCallback: _ => rxFifo.TryDequeue(out var val) ? val : 0u, writeCallback: (_, value) => SendData((uint)value), name: "DATA")
                    .WithReservedBits(16, 16)
                    .WithReadCallback((_, __) => Update())
                },
                { (long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => txFifo.Count == 0, name: "TFE")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => txFifo.Count != txFifo.Capacity, name: "TNF")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => rxFifo.Count != 0, name: "RNE")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => rxFifo.Count == rxFifo.Capacity, name: "RFF")
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => txFifo.Count != 0, name: "BSY") //SSI is currently transmitting and/or
                        //receiving a frame or the transmit fifo is not empty. Only the last part is applicable
                    .WithReservedBits(5, 27)
                },
                { (long)Registers.InterruptMask, new DoubleWordRegister(this)
                    .WithFlag(0, out rxFifoOverrunInterruptMask, name: "RORIM")
                    .WithTaggedFlag("RTIM", 1)
                    .WithFlag(2, out rxFifoInterruptMask, name: "RXIM")
                    .WithFlag(3, out txFifoInterruptMask, name: "TXIM")
                    .WithReservedBits(4, 28)
                    .WithWriteCallback((_, __) => Update())
                },
                { (long)Registers.RawInterruptStatus, new DoubleWordRegister(this)
                    .WithFlag(0, out rxFifoOverrunInterrupt, FieldMode.Read, name: "RORRIS")
                    .WithTaggedFlag("RTRIS", 1)
                    .WithFlag(2, out rxFifoInterrupt, FieldMode.Read, name: "RXRIS")
                    .WithFlag(3, out txFifoInterrupt, FieldMode.Read, name: "TXRIS")
                    .WithReservedBits(4, 28)
                },
                { (long)Registers.MaskedInterruptStatus, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => rxFifoOverrunInterrupt.Value && rxFifoOverrunInterruptMask.Value, name: "RORMIS")
                    .WithTaggedFlag("RTMIS", 1)
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => rxFifoInterrupt.Value && rxFifoInterruptMask.Value, name: "RXMIS")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => txFifoInterrupt.Value && txFifoInterruptMask.Value, name: "TXMIS")
                    .WithReservedBits(4, 28)
                },
                { (long)Registers.InterruptClear, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, value) => {
                                if(value)
                                {
                                    rxFifoOverrunInterrupt.Value = false;
                                }
                            })
                    .WithTaggedFlag("RTIC", 1)
                    .WithReservedBits(2, 30)
                    .WithWriteCallback((_, __) => Update())
                },
            };
            registers = new DoubleWordRegisterCollection(this, regs);
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
            registers.Reset();
            rxFifo.Clear();
            txFifo.Clear();
            IRQ.Unset();
        }

        public GPIO IRQ { get; private set; }

        public long Size => 0x1000;

        private void EnableTransmitter(bool _, bool enabled)
        {
            if(enabled && txFifo.Count > 0)
            {
                foreach(var value in txFifo.DequeueAll())
                {
                    SendByte(value);
                }
                Update();
            }
        }

        private void SendData(uint value)
        {
            var byteValue = (byte)value;
            if(value != byteValue)
            {
                this.Log(LogLevel.Warning, "Trying to send 0x{0:X}, but it doesn't fit in a byte. Will send 0x{1:X} instead", value, byteValue);
            }
            if(enabled.Value)
            {
                SendByte(byteValue);
            }
            else
            {
                this.Log(LogLevel.Noisy, "Deferring the transfer of 0x{0:X} as the transmitter is not enabled", byteValue);
                txFifo.Enqueue(byteValue);
            }
            Update();
        }

        private void SendByte(byte value)
        {
            if(RegisteredPeripheral == null)
            {
                this.Log(LogLevel.Warning, "Trying to write 0x{0:X} to a slave peripheral, but nothing is connected");
                return;
            }
            var response = RegisteredPeripheral.Transmit(value);
            this.Log(LogLevel.Noisy, "Transmitted deferred data 0x{0:X}, received 0x{1:X}", value, response);
            if(!rxFifo.Enqueue(response))
            {
                this.Log(LogLevel.Warning, "Receive FIFO overrun");
                rxFifoOverrunInterrupt.Value = true;
            }
        }

        private void Update()
        {
            txFifoInterrupt.Value = txFifo.Count <= txFifo.Capacity / 2;
            rxFifoInterrupt.Value = rxFifo.Count >= rxFifo.Capacity / 2;
            //rxFifoOverrunInterrupt is set in `SendData`
            IRQ.Set((txFifoInterrupt.Value && txFifoInterruptMask.Value)
                        || (rxFifoInterrupt.Value && rxFifoInterruptMask.Value)
                        || (rxFifoOverrunInterrupt.Value && rxFifoOverrunInterruptMask.Value));
        }

        private DoubleWordRegisterCollection registers;
        private CircularBuffer<byte> rxFifo;
        private CircularBuffer<byte> txFifo;

        private IFlagRegisterField enabled;

        private IFlagRegisterField rxFifoOverrunInterruptMask;
        private IFlagRegisterField rxFifoInterruptMask;
        private IFlagRegisterField txFifoInterruptMask;

        private IFlagRegisterField rxFifoOverrunInterrupt;
        private IFlagRegisterField rxFifoInterrupt;
        private IFlagRegisterField txFifoInterrupt;

        private const int FifoCapacity = 16;

        public enum Registers
        {
            Control0 = 0x0,
            Control1 = 0x4,
            Data = 0x8,
            Status = 0xC,
            ClockPrescaler = 0x10,
            InterruptMask = 0x14,
            RawInterruptStatus = 0x18,
            MaskedInterruptStatus = 0x1C,
            InterruptClear = 0x20,
            DMAControl = 0x24,
            ClockConfiguration = 0xFC8,
        }
    }
}
