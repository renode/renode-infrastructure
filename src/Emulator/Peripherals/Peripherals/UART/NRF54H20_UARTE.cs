//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.UART
{
    public class NRF54H20_UARTE : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public NRF54H20_UARTE(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();

            sysbus = machine.GetSystemBus(this);
            interruptManager = new InterruptManager<Interrupts>(this);            
            registers = new DoubleWordRegisterCollection(this, DefineRegisters());
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
            interruptManager.Reset();
            registers.Reset();
            currentRxPointer = 0;
            rxStarted = false;
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
                        return parityType.Value ? Parity.Odd : Parity.Even;
                    default:
                        this.Log(LogLevel.Error, "Wrong parity bits register value");
                        return Parity.None;
                }
            }
        }

        public override uint BaudRate => GetBaudRate((uint)baudrate.Value);
        public long Size => 0x1000;

        [IrqProvider]
        public GPIO IRQ { get; private set; }

        protected override void CharWritten()
        {
            if(enabled.Value == EnableState.Disabled || !rxStarted)
            {
                this.Log(LogLevel.Warning, "Received a character, but the receiver is disabled.");
                // Received character should be dropped. This is safe because QueueEmptied is not used
                this.TryGetCharacter(out var _);
                return;
            }

            if(interruptManager.IsSet(Interrupts.DMARxEnd))
            {
                // The receiver stopped, but there might still be characters in the buffer.
                // This occurs when we paste text to terminal - UART is assumed to be slower
                // than ISR. That's why we silently wait for the StartRx event.
                return;
            }

            if(!TryGetCharacter(out var character))
            {
                this.Log(LogLevel.Warning, "Trying to do a DMA transfer from an empty Rx FIFO.");
            }
            this.Log(LogLevel.Noisy, "Transfering 0x{0:X} to 0x{1:X}", character, currentRxPointer);
            sysbus.WriteByte(currentRxPointer, character);
            rxAmount.Value++;
            currentRxPointer++;

            SetInterruptOnRxEnd();
            interruptManager.SetInterrupt(Interrupts.ReceiveReady);
        }

        protected override void QueueEmptied()
        {
            // Intentionally left blank. Implementing this callback might break
            // the logic of CharWritten when the receiver is disabled.
        }

        private void SetInterruptOnRxEnd()
        {
            if(rxAmount.Value >= rxMaximumCount.Value)
            {
                interruptManager.SetInterrupt(Interrupts.DMARxEnd);
            }
        }

        private Dictionary<long, DoubleWordRegister> DefineRegisters()
        {
            var dict = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.TaskRxStart, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) { StartRx(); }}, name: "TASK_STARTRX")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.TaskRxStop, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) { StopRx(); }}, name: "TASK_STOPRX")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.TaskTxStart, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) { StartTx(); }}, name: "TASK_STARTTX")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.TaskTxStop, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) { StopTx(); }}, name: "TASK_STOPTX")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.EventsClearToSend, GetEventRegister(Interrupts.ClearToSend, "EVENTS_CTS")
                    // we don't use this interrupt - just want to hush the register
                },
                {(long)Registers.EventsNotClearToSend, GetEventRegister(Interrupts.NotClearToSend, "EVENTS_NCTS")
                    // we don't use this interrupt - just want to hush the register
                },
                {(long)Registers.EventsTxDReady, GetEventRegister(Interrupts.TransmitReady, "EVENTS_TXDRDY")
                    // we don't use this interrupt - just want to hush the register
                },
                {(long)Registers.EventsRxDReady, GetEventRegister(Interrupts.ReceiveReady, "EVENTS_RXDRDY")
                },
                {(long)Registers.EventsErrorDetected, GetEventRegister(Interrupts.Error, "EVENTS_ERROR")
                    // we don't use this interrupt - just want to hush the register
                },
                {(long)Registers.EventsRxTimeout, GetEventRegister(Interrupts.ReceiveTimeout, "EVENTS_RXTO")},
                {(long)Registers.EventsTxStopped, GetEventRegister(Interrupts.TransmitStopped, "EVENTS_TXSTOPPED")},
                {(long)Registers.EventsDMARxEnd, GetEventRegister(Interrupts.DMARxEnd, "EVENTS_DMARXEND")},
                {(long)Registers.EventsDMATxEnd, GetEventRegister(Interrupts.DMATxEnd, "EVENTS_DMATXEND")},
                {(long)Registers.EventsFrameTimeout, GetEventRegister(Interrupts.FrameTimeout, "EVENTS_FRAMETIMEOUT")
                    // we don't use this interrupt - just want to hush the register
                },
                {(long)Registers.InterruptEnableSet, interruptManager.GetRegister<DoubleWordRegister>(
                    writeCallback: (interrupt, _, newValue) =>
                    {
                        if(newValue)
                        {
                            interruptManager.EnableInterrupt(interrupt);
                        }
                    },
                    valueProviderCallback: (interrupt, _) => interruptManager.IsEnabled(interrupt)
                    )
                },
                {(long)Registers.InterruptEnableClear, interruptManager.GetRegister<DoubleWordRegister>(
                    writeCallback: (interrupt, _, newValue) =>
                    {
                        if(newValue)
                        {
                            interruptManager.DisableInterrupt(interrupt);
                        }
                    },
                    valueProviderCallback: (interrupt, _) => interruptManager.IsEnabled(interrupt)
                    )
                },
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithEnumField(0, 4, out enabled, name: "ENABLE")
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.BaudRate, new DoubleWordRegister(this, 0x04000000)
                    .WithValueField(0, 32, out baudrate, name: "BAUDRATE")
                },
                {(long)Registers.Config, new DoubleWordRegister(this, 0x00001000)
                    .WithTaggedFlag("HWFC", 0)
                    .WithEnumField(1, 3, out parity, name: "PARITY")
                    .WithFlag(4, out stopBit, name: "STOP")
                    .WithReservedBits(5, 3)
                    .WithFlag(8, out parityType, name: "PARITYTYPE")
                    .WithTag("FRAMESIZE", 9, 4)
                    .WithTaggedFlag("ENDIAN", 13)
                    .WithTaggedFlag("FRAMETIMEOUT", 14)
                    .WithReservedBits(15, 17)
                },
                {(long)Registers.InterruptEnable, interruptManager.GetInterruptEnableRegister<DoubleWordRegister>()},
                {(long)Registers.RxRamBuffer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxPointer, name: "PTR")
                },
                {(long)Registers.RxMaxCount, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out rxMaximumCount, name: "MAXCNT")
                    .WithReservedBits(16, 16)
                    .WithChangeCallback((_, __) => SetInterruptOnRxEnd())
                },
                {(long)Registers.RxAmount, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out rxAmount, FieldMode.Read, name: "AMOUNT")
                    .WithReservedBits(16, 16)
                    .WithChangeCallback((_, __) => SetInterruptOnRxEnd())
                },
                {(long)Registers.TxRamBuffer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txPointer, name: "PTR")
                },
                {(long)Registers.TxMaxCount, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out txMaximumCount, name: "MAXCNT")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.TxCurrentAmount, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out txAmount, FieldMode.Read, name: "AMOUNT")
                    .WithReservedBits(16, 16)
                }
            };

            return dict;
        }

        private DoubleWordRegister GetEventRegister(Interrupts interrupt, string name)
        {
            return new DoubleWordRegister(this)
                .WithFlag(0,
                        valueProviderCallback: _ => interruptManager.IsSet(interrupt),
                        writeCallback: (_, value) => interruptManager.SetInterrupt(interrupt, value),
                        name: name)
                .WithReservedBits(1, 31);
        }

        private void StartRx()
        {
            interruptManager.SetInterrupt(Interrupts.ReceiveReady);
            interruptManager.ClearInterrupt(Interrupts.DMARxEnd);
            currentRxPointer = (uint)rxPointer.Value;
            rxAmount.Value = 0;
            rxStarted = true;
            if(Count > 0)
            {
                // With the new round of reception we might still have some characters in the
                // buffer.
                CharWritten();
            }
        }

        private void StopRx()
        {
            interruptManager.SetInterrupt(Interrupts.ReceiveTimeout);
            interruptManager.SetInterrupt(Interrupts.DMARxEnd);
            rxStarted = false;
        }

        private void StartTx()
        {
            // we set these interrupts regardless of the transfer length
            interruptManager.SetInterrupt(Interrupts.DMATxEnd);
            interruptManager.SetInterrupt(Interrupts.TransmitStopped);

            if(txMaximumCount.Value == 0)
            {
                return;
            }
            var bytesRead = sysbus.ReadBytes(txPointer.Value, (int)txMaximumCount.Value);
            foreach(var character in bytesRead)
            {
                TransmitCharacter(character);
            }
            txAmount.Value = txMaximumCount.Value;
        }

        private void StopTx()
        {
            // the remark from StopRx applies here as well, but we assume that StartTx is always finished at once
            interruptManager.SetInterrupt(Interrupts.TransmitStopped);
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

        private readonly IBusController sysbus;
        private readonly DoubleWordRegisterCollection registers;
        private readonly InterruptManager<Interrupts> interruptManager;

        private uint currentRxPointer;
        private bool rxStarted;

        private IFlagRegisterField parityType;
        private IFlagRegisterField stopBit;
        private IValueRegisterField rxMaximumCount;
        private IValueRegisterField txMaximumCount;
        private IValueRegisterField rxPointer;
        private IValueRegisterField txPointer;
        private IValueRegisterField rxAmount;
        private IValueRegisterField txAmount;
        private IValueRegisterField baudrate;
        private IEnumRegisterField<ParityConfig> parity;
        private IEnumRegisterField<EnableState> enabled;

        private enum Interrupts
        {
            ClearToSend = 0,
            NotClearToSend = 1,
            TransmitReady = 3,
            ReceiveReady = 4,
            Error = 5,
            ReceiveTimeout = 9,
            TransmitStopped = 12,
            DMARxEnd = 19,
            DMARxReady = 20,
            DMARxBusError = 21,
            DMARxMatch0 = 22,
            DMARxMatch1 = 23,
            DMARxMatch2 = 24,
            DMARxMatch3 = 25,
            DMATxEnd = 26,
            DMATxReady = 27,
            DMATxBusError = 28,
            FrameTimeout = 29
        }

        private enum Registers : long
        {
            FlushRx = 0x01C,

            TaskRxStart = 0x28,
            TaskRxStop = 0x2C,

            TaskTxStart = 0x50,
            TaskTxStop = 0x54,

            EventsClearToSend = 0x100,
            EventsNotClearToSend = 0x104,
            EventsTxDReady = 0x10C,
            EventsRxDReady = 0x110,
            EventsErrorDetected = 0x114,
            EventsRxTimeout = 0x124,
            EventsTxStopped = 0x130,

            EventsDMARxEnd = 0x14C,
            EventsDMATxEnd = 0x168,

            EventsFrameTimeout =  0x174,

            Shortcuts = 0x200,
            InterruptEnable = 0x300,
            InterruptEnableSet = 0x304,
            InterruptEnableClear = 0x308,
            ErrorSource = 0x480,

            Enable = 0x500,
            BaudRate = 0x524,
            Config = 0x56C,
            RxAddress9BitDataMode = 0x574,
            FrameTimeout = 0x578,

            PinSelectTXD = 0x604,
            PinSelectCTS = 0x608,
            PinSelectRXD = 0x60C,
            PinSelectRTS = 0x610,

            RxRamBuffer = 0x704,
            RxMaxCount = 0x708,
            RxAmount = 0x70C,
            RxCurrentAmount = 0x710,
            RxEasyDMAType = 0x714,

            TxRamBuffer = 0x73C,
            TxMaxCount = 0x740,
            TxAmount = 0x744,
            TxCurrentAmount = 0x748,
            TxEasyDMAType = 0x74C,
        }

        // The Baudrate values come from the driver
        private enum Baudrate : long
        {
            Baud1200 = 0x0004F000,
            Baud2400 = 0x0009D000,
            Baud4800 = 0x0013B000,
            Baud9600 = 0x00275000,
            Baud14400 = 0x003AF000,
            Baud19200 = 0x004EA000,
            Baud28800 = 0x0075C000,
            Baud31250 = 0x00800000,
            Baud38400 = 0x009D0000,
            Baud56000 = 0x00E50000,
            Baud57600 = 0x00EB0000,
            Baud76800 = 0x013A9000,
            Baud115200 = 0x01D60000,
            Baud230400 = 0x03B00000,
            Baud250000 = 0x04000000,
            Baud460800 = 0x07400000,
            Baud921600 = 0x0F000000,
            Baud1M = 0x10000000
        }

        private enum ParityConfig
        {
            Excluded = 0x0,
            Included = 0x7
        }

        private enum EnableState
        {
            Disabled = 0x0,
            Enabled = 0x8,
        }
    }
}
