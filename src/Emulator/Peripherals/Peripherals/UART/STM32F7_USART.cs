//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.UART
{
    public sealed class STM32F7_USART : IDoubleWordPeripheral, IKnownSize, IUART
    {
        public STM32F7_USART()
        {
            sync = new object();
            IRQ = new GPIO();
            receiveQueue = new Queue<byte>();

            var controlRegister1 = new DoubleWordRegister(this);
            enabled = controlRegister1.DefineFlagField(0, name: "UE");
            receiveEnabled = controlRegister1.DefineFlagField(2, name: "RE");
            transmitEnabled = controlRegister1.DefineFlagField(3, name: "TE");
            receiveInterruptEnabled = controlRegister1.DefineFlagField(5, name: "RXNEIE");
            transmitQueueEmptyInterruptEnabled = controlRegister1.DefineFlagField(7, name: "TXEIE", writeCallback: delegate { RefreshInterrupt(); } );
            controlRegister1.DefineFlagField(8, name: "PEIE");
            paritySelection = controlRegister1.DefineFlagField(9, name: "PS");
            parityControlEnabled = controlRegister1.DefineFlagField(10, name: "PCE");

            var controlRegister2 = new DoubleWordRegister(this);
            stopBits = controlRegister2.DefineValueField(12, 2);

            registers = new DoubleWordRegisterCollection(this, new Dictionary<long, DoubleWordRegister> {
                { (long)Register.ControlRegister1, controlRegister1 },
                { (long)Register.ControlRegister2, controlRegister2 },
                { (long)Register.ControlRegister3, new DoubleWordRegister(this).WithFlag(0, name: "EIE") },
                { (long)Register.InterruptAndStatus, new DoubleWordRegister(this, 0x200000C0)
                        .WithFlag(5, FieldMode.Read, name: "RXNE", valueProviderCallback: delegate { return receiveQueue.Count > 0; })
                        .WithFlag(6, FieldMode.Read, name: "TC").WithFlag(7, FieldMode.Read, name: "TXE")
                        .WithFlag(21, FieldMode.Read, name: "TEACK", valueProviderCallback: delegate { return transmitEnabled.Value; })
                        .WithFlag(22, FieldMode.Read, name: "REACK", valueProviderCallback: delegate { return receiveEnabled.Value; })
                        .WithValueField(23, 8, FieldMode.Read, name: "Reserved") }
            });
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(sync)
            {
                switch((Register)offset)
                {
                case Register.BaudRate:
                    return baudRateDivisor;
                case Register.ReceiveData:
                    return HandleReceiveData();
                default:
                    return registers.Read(offset);
                }
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(sync)
            {
                switch((Register)offset)
                {
                case Register.BaudRate:
                    baudRateDivisor = value;
                    break;
                case Register.TransmitData:
                    HandleTransmitData(value);
                    break;
                default:
                    registers.Write(offset, value);
                    break;
                }
            }
        }

        public void Reset()
        {
            baudRateDivisor = 0;
            registers.Reset();
        }

        public void WriteChar(byte value)
        {
            lock(sync)
            {
                if(receiveEnabled.Value && enabled.Value)
                {
                    receiveQueue.Enqueue(value);
                    RefreshInterrupt();
                }
                else
                {
                    this.Log(LogLevel.Warning, "Char was received, but the receiver (or the whole USART) is not enabled. Ignoring.");
                }
            }
        }

        public uint BaudRate
        {
            get
            {
                return ApbClock / baudRateDivisor;
            }
        }

        public Bits StopBits
        {
            get
            {
                switch(stopBits.Value)
                {
                case 0:
                    return Bits.One;
                case 1:
                    return Bits.Half;
                case 2:
                    return Bits.Two;
                case 3:
                    return Bits.OneAndAHalf;
                default:
                    throw new InvalidOperationException("Should not reach here.");
                }
            }
        }

        public Parity ParityBit
        {
            get
            {
                if(!parityControlEnabled.Value)
                {
                    return Parity.None;
                }
                return paritySelection.Value ? Parity.Odd : Parity.Even;
            }
        }

        public event Action<byte> CharReceived;

        public GPIO IRQ { get; private set; }

        public long Size
        {
            get
            {
                return 0x400;
            }
        }

        private void HandleTransmitData(uint value)
        {
            if(transmitEnabled.Value && enabled.Value)
            {
                var charReceived = CharReceived;
                if(charReceived != null)
                {
                    charReceived((byte)value);
                }
            }
            else
            {
                this.Log(LogLevel.Warning, "Char was to be sent, but the transmitter (or the whole USART) is not enabled. Ignoring.");
            }
        }

        private uint HandleReceiveData()
        {
            var result = receiveQueue.Dequeue();
            RefreshInterrupt();
            return result;
        }

        private void RefreshInterrupt()
        {
            if(transmitQueueEmptyInterruptEnabled.Value)
            {
                IRQ.Set();
            }
            else if(receiveInterruptEnabled.Value)
            {
                IRQ.Set(receiveQueue.Count > 0);
            }
            else
            {
                IRQ.Unset();
            }
        }

        private uint baudRateDivisor;
        private readonly IFlagRegisterField parityControlEnabled;
        private readonly IFlagRegisterField paritySelection;
        private readonly IFlagRegisterField transmitQueueEmptyInterruptEnabled;
        private readonly IFlagRegisterField receiveInterruptEnabled;
        private readonly IFlagRegisterField transmitEnabled;
        private readonly IFlagRegisterField receiveEnabled;
        private readonly IFlagRegisterField enabled;
        private readonly IValueRegisterField stopBits;
        private readonly Queue<byte> receiveQueue;
        private readonly DoubleWordRegisterCollection registers;
        private readonly object sync;

        private const uint ApbClock = 200000000;

        private enum Register
        {
            ControlRegister1 = 0x0,
            ControlRegister2 = 0x4,
            ControlRegister3 = 0x8,
            BaudRate = 0xC,
            InterruptAndStatus = 0x1C,
            ReceiveData = 0x24,
            TransmitData = 0x28
        }
    }
}

