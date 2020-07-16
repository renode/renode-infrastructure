//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    public class NRF52840_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public NRF52840_UART(Machine machine) : base(machine)
        {
            registers = new DoubleWordRegisterCollection(this, DefineRegisters());
            IRQ = new GPIO();
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
            base.Reset();
            registers.Reset();
            IRQ.Unset();
        }

        public override Bits StopBits => stopBit.Value ? Bits.Two : Bits.One;

        public override Parity ParityBit
        {
            get
            {
                switch(parity.Value)
                {
                    case ParityConfig.Excluded:
                        return Parity.None;
                    case ParityConfig.Included:
                        return Parity.Even;
                    default:
                        this.Log(LogLevel.Error, "Wrong parity bits register value");
                        return Parity.None;
                }
            }
        }

        public override uint BaudRate => GetBaudRate(baudrate.Value);
        public long Size => 0x1000;
        public GPIO IRQ { get; }

        private Dictionary<long, DoubleWordRegister> DefineRegisters()
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)Register.StartRx, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "TASKS_STARTRX", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            UpdateInterrupts();
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.StartTx, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "TASKS_STARTTX", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            txReady.Value = true;
                            UpdateInterrupts();
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.StopRx, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "TASKS_STOPRX", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            UpdateInterrupts();
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.StopTx, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "TASKS_STOPTX", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            txReady.Value = false;
                            UpdateInterrupts();
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.RxDReady, new DoubleWordRegister(this)
                    .WithFlag(0, out rxReady, name: "EVENTS_RXDRDY", writeCallback: (_, __) =>
                    {
                        UpdateInterrupts();
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.TxDReady, new DoubleWordRegister(this)
                    .WithFlag(0, out txReady, name: "EVENTS_RXDRDY", writeCallback: (_, __) =>
                    {
                        UpdateInterrupts();
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.ErrorDetected, new DoubleWordRegister(this)
                    .WithFlag(0, name: "EVENTS_ERROR") //empty implementation - just want to hush the register
                },
                {(long)Register.InterruptEnableSet, new DoubleWordRegister(this)
                    .WithTaggedFlag("CTS", 0)
                    .WithTaggedFlag("NCTS", 1)
                    .WithFlag(2, out rxReadyInterruptEnabled, FieldMode.Set | FieldMode.Read, name: "RXDRDY")
                    .WithReservedBits(3, 3)
                    .WithFlag(7, out txReadyInterruptEnabled, FieldMode.Set | FieldMode.Read, name: "TXDRDY")
                    .WithReservedBits(8, 1)
                    .WithTaggedFlag("ERROR", 9)
                    .WithReservedBits(10, 7)
                    .WithTaggedFlag("RXTO", 17)
                    .WithReservedBits(18, 14)
                    .WithChangeCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },
                {(long)Register.InterruptEnableClear, new DoubleWordRegister(this)
                    .WithTaggedFlag("CTS", 0)
                    .WithTaggedFlag("NCTS", 1)
                    .WithFlag(2, name: "INTENCLR_RXDRDY", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            rxReadyInterruptEnabled.Value = false;
                            UpdateInterrupts();
                        }
                    }, valueProviderCallback: _ => rxReadyInterruptEnabled.Value)
                    .WithReservedBits(3, 3)
                    .WithFlag(7, name: "INTENCLR_TXDRDY", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            txReadyInterruptEnabled.Value = false;
                            UpdateInterrupts();
                        }
                    }, valueProviderCallback: _ => txReadyInterruptEnabled.Value)
                    .WithReservedBits(8, 1)
                    .WithTaggedFlag("ERROR", 9)
                    .WithReservedBits(10, 7)
                    .WithTaggedFlag("RXTO", 17)
                    .WithReservedBits(18, 14)
                },
                {(long)Register.RxD, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, name: "RXD", valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var character))
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an empty Rx FIFO.");
                        }

                        rxReady.Value |= Count > 0;

                        return character;
                    })
                    .WithReservedBits(8, 24)
                },
                {(long)Register.TxD, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, name: "TXD", writeCallback: (_, value) =>
                    {
                        TransmitCharacter((byte)value);
                        txReady.Value = true;
                        UpdateInterrupts();
                    })
                    .WithReservedBits(8, 24)
                },
                {(long)Register.BaudRate, new DoubleWordRegister(this, 0x04000000)
                    .WithValueField(0, 32, out baudrate, name: "BAUDRATE")
                },
                {(long)Register.Config, new DoubleWordRegister(this)
                    .WithTaggedFlag("HWFC", 0)
                    .WithEnumField(1, 3, out parity, name: "CONFIG_PARITY")
                    .WithFlag(4, out stopBit, name: "CONFIG_STOP")
                    .WithReservedBits(5, 27)
                }
            };
        }

        private void UpdateInterrupts()
        {
            var txActive = txReadyInterruptEnabled.Value && txReady.Value;
            var rxActive = rxReadyInterruptEnabled.Value && rxReady.Value;
            IRQ.Set(txActive || rxActive);
        }

        private uint GetBaudRate(uint value)
        {
            switch((Baudrate)value)
            {
                case Baudrate.Baud1200: return 1200;
                case Baudrate.Baud2400: return 2400;
                case Baudrate.Baud4800: return 4800;
                case Baudrate.Baud9600: return 9600;
                case Baudrate.Baud14400: return 14400;
                case Baudrate.Baud19200: return 19200;
                case Baudrate.Baud28800: return 28800;
                case Baudrate.Baud31250: return 31250;
                case Baudrate.Baud56000: return 56000;
                case Baudrate.Baud57600: return 57600;
                case Baudrate.Baud115200: return 115200;
                case Baudrate.Baud230400: return 230400;
                case Baudrate.Baud250000: return 250000;
                case Baudrate.Baud460800: return 460800;
                case Baudrate.Baud921600: return 921600;
                case Baudrate.Baud1M: return 1000000;
                default: return 0;
            }
        }

        protected override void CharWritten()
        {
            rxReady.Value = true;
            UpdateInterrupts();
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private readonly DoubleWordRegisterCollection registers;

        private IFlagRegisterField txReady;
        private IFlagRegisterField rxReady;
        private IFlagRegisterField txReadyInterruptEnabled;
        private IFlagRegisterField rxReadyInterruptEnabled;
        private IValueRegisterField baudrate;
        private IEnumRegisterField<ParityConfig> parity;
        private IFlagRegisterField stopBit;

        private enum Register : long
        {
            StartRx = 0x000,
            StopRx = 0x004,
            StartTx = 0x008,
            StopTx = 0x00C,
            Suspend = 0x01C,
            ClearToSend = 0x100,
            NotClearToSend = 0x104,
            RxDReady = 0x108,
            TxDReady = 0x11C,
            ErrorDetected = 0x124,
            ReceiverTimeout = 0x144,
            Shortcuts = 0x200,
            InterruptEnableSet = 0x304,
            InterruptEnableClear = 0x308,
            ErrorSource = 0x480,
            Enable = 0x500,
            PinSelectRTS = 0x508,
            PinSelectTXD = 0x50C,
            PinSelectCTS = 0x510,
            PinSelectRXD = 0x514,
            RxD = 0x518,
            TxD = 0x51C,
            BaudRate = 0x524,
            Config = 0x56C
        }

        // The Baudrate values come from documentation
        private enum Baudrate : long
        {
            Baud1200 = 0x0004F000,
            Baud2400 = 0x0009D000,
            Baud4800 = 0x0013B000,
            Baud9600 = 0x00275000,
            Baud14400 = 0x003B0000,
            Baud19200 = 0x004EA000,
            Baud28800 = 0x0075F000,
            Baud31250 = 0x00800000,
            Baud38400 = 0x009D5000,
            Baud56000 = 0x00E50000,
            Baud57600 = 0x00EBF000,
            Baud76800 = 0x013A9000,
            Baud115200 = 0x01D7E000,
            Baud230400 = 0x03AFB000,
            Baud250000 = 0x04000000,
            Baud460800 = 0x075F7000,
            Baud921600 = 0x0EBED000,
            Baud1M = 0x10000000
        }

        private enum ParityConfig
        {
            Excluded = 0x0,
            Included = 0x7
        }
    }
}
