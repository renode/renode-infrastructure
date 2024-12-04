//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    public class NRF52840_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public NRF52840_UART(IMachine machine, bool easyDMA = false) : base(machine)
        {
            sysbus = machine.GetSystemBus(this);
            this.easyDMA = easyDMA;
            IRQ = new GPIO();
            interruptManager = new InterruptManager<Interrupts>(this);
            registers = new DoubleWordRegisterCollection(this, DefineRegisters());
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(interruptManager)
            {
                return registers.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(interruptManager)
            {
                registers.Write(offset, value);
            }
        }

        public override void Reset()
        {
            base.Reset();
            lock(interruptManager)
            {
                interruptManager.Reset();
                registers.Reset();
            }

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
                        return Parity.Even;
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
                // The character should not be received. This is safe because QueueEmptied is not used
                this.TryGetCharacter(out var _);
                return;
            }

            lock(interruptManager)
            {
                if(interruptManager.IsSet(Interrupts.EndReceive))
                {
                    // The receiver stopped, but there might still be characters in the buffer.
                    // This occurs when we paste text to terminal - UART is assumed to be slower
                    // than ISR. That's why we silently wait for the StartRx event.
                    return;
                }

                if(easyDMA)
                {
                    // do DMA transfer
                    if(!TryGetCharacter(out var character))
                    {
                        this.Log(LogLevel.Warning, "Trying to do a DMA transfer from an empty Rx FIFO.");
                    }
                    this.Log(LogLevel.Noisy, "Transfering 0x{0:X} to 0x{1:X}", character, currentRxPointer);
                    sysbus.WriteByte(currentRxPointer, character);
                    rxAmount.Value++;
                    currentRxPointer++;
                    if(rxAmount.Value == rxMaximumCount.Value)
                    {
                        interruptManager.SetInterrupt(Interrupts.EndReceive);
                    }
                }
                interruptManager.SetInterrupt(Interrupts.ReceiveReady);
            }
        }

        protected override void QueueEmptied()
        {
            // Intentionally left blank. Implementing this callback might break
            // the logic of CharWritten when the receiver is disabled.
        }

        private Dictionary<long, DoubleWordRegister> DefineRegisters()
        {
            var dict = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.StartRx, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) { StartRx(); }}, name: "TASKS_STARTRX")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.StopRx, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) { StopRx(); }}, name: "TASKS_STOPRX")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.StartTx, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) { StartTx(); }}, name: "TASKS_STARTTX")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.StopTx, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) { StopTx(); }}, name: "TASKS_STOPTX")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.RxDReady, GetEventRegister(Interrupts.ReceiveReady, "EVENTS_RXDRDY")
                },
                {(long)Registers.TxDReady, GetEventRegister(Interrupts.TransmitReady, "EVENTS_TXDRDY")
                },
                {(long)Registers.ErrorDetected, GetEventRegister(Interrupts.Error, "EVENTS_ERROR")
                    // we don't use this interrupt - just want to hush the register
                },
                {(long)Registers.InterruptEnableSet, interruptManager.GetRegister<DoubleWordRegister>(
                    writeCallback: (interrupt, _, newValue) =>
                    {
                        if(newValue)
                        {
                            this.Machine.LocalTimeSource.ExecuteInNearestSyncedState(ts => interruptManager.EnableInterrupt(interrupt));
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
                    .WithEnumField(0, 8, out enabled, name: "ENABLE")
                    .WithReservedBits(9, 23)
                },
                {(long)Registers.PinSelectRTS, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithTag("PIN", 0, 5)
                    .WithTaggedFlag("PORT", 5)
                    .WithReservedBits(6, 25)
                    .WithTaggedFlag("CONNECT", 31)
                },
                {(long)Registers.PinSelectTXD, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithTag("PIN", 0, 5)
                    .WithTaggedFlag("PORT", 5)
                    .WithReservedBits(6, 25)
                    .WithTaggedFlag("CONNECT", 31)
                },
                {(long)Registers.PinSelectCTS, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithTag("PIN", 0, 5)
                    .WithTaggedFlag("PORT", 5)
                    .WithReservedBits(6, 25)
                    .WithTaggedFlag("CONNECT", 31)
                },
                {(long)Registers.PinSelectRXD, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithTag("PIN", 0, 5)
                    .WithTaggedFlag("PORT", 5)
                    .WithReservedBits(6, 25)
                    .WithTaggedFlag("CONNECT", 31)
                },
                {(long)Registers.BaudRate, new DoubleWordRegister(this, 0x04000000)
                    .WithValueField(0, 32, out baudrate, name: "BAUDRATE")
                },
                {(long)Registers.Config, new DoubleWordRegister(this)
                    .WithTaggedFlag("HWFC", 0)
                    .WithEnumField(1, 3, out parity, name: "CONFIG_PARITY")
                    .WithFlag(4, out stopBit, name: "CONFIG_STOP")
                    .WithReservedBits(5, 27)
                }
            };
            if(!easyDMA)
            {
                // these are registers only for non-eDMA version of the UART
                dict.Add((long)Registers.RxD, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, name: "RXD", valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var character))
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an empty Rx FIFO.");
                        }

                        if(Count > 0)
                        {
                            interruptManager.SetInterrupt(Interrupts.ReceiveReady);
                        }

                        return character;
                    })
                    .WithReservedBits(8, 24)
                );
                dict.Add((long)Registers.TxD, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, name: "TXD", writeCallback: (_, value) =>
                    {
                        if(enabled.Value == EnableState.Disabled)
                        {
                            this.Log(LogLevel.Warning, "Trying to transmit a character, but the peripheral is disabled.");
                            return;
                        }
                        TransmitCharacter((byte)value);
                        interruptManager.SetInterrupt(Interrupts.TransmitReady);
                    })
                    .WithReservedBits(8, 24)
                );
            }
            else
            {
                // these are registers only for eDMA version of the UART
                dict.Add((long)Registers.EndRx, GetEventRegister(Interrupts.EndReceive, "EVENTS_ENDRX"));

                dict.Add((long)Registers.EndTx, GetEventRegister(Interrupts.EndTransmit, "EVENTS_ENDRX"));

                dict.Add((long)Registers.RxTimeout, GetEventRegister(Interrupts.ReceiveTimeout, "EVENTS_RXTO"));

                dict.Add((long)Registers.RxStarted, GetEventRegister(Interrupts.ReceiveStarted, "EVENTS_RXSTARTED"));

                dict.Add((long)Registers.TxStarted, GetEventRegister(Interrupts.TransmitStarted, "EVENTS_TXSTARTED"));

                dict.Add((long)Registers.TxStopped, GetEventRegister(Interrupts.TransmitStopped, "EVENTS_TXSTOPPED"));

                dict.Add((long)Registers.InterruptEnable, interruptManager.GetInterruptEnableRegister<DoubleWordRegister>());

                dict.Add((long)Registers.RxDPointer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxPointer, name: "PTR")
                );

                dict.Add((long)Registers.RxDMaximumCount, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out rxMaximumCount, name: "MAXCNT")
                    .WithReservedBits(16, 16)
                );

                dict.Add((long)Registers.RxDAmount, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out rxAmount, FieldMode.Read, name: "AMOUNT")
                    .WithReservedBits(16, 16)
                );

                dict.Add((long)Registers.TxDPointer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txPointer, name: "PTR")
                );

                dict.Add((long)Registers.TxDMaximumCount, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out txMaximumCount, name: "MAXCNT")
                    .WithReservedBits(16, 16)
                );

                dict.Add((long)Registers.TxDAmount, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out txAmount, FieldMode.Read, name: "AMOUNT")
                    .WithReservedBits(16, 16)
                );
            }
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
            interruptManager.SetInterrupt(Interrupts.ReceiveStarted);
            if(easyDMA)
            {
                rxAmount.Value = 0;
                currentRxPointer = (uint)rxPointer.Value;
            }
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
            if(easyDMA && rxAmount.Value < rxMaximumCount.Value)
            {
                // we have not generater ENDRX yet, but it's guaranteed to appear before RXTO
                interruptManager.SetInterrupt(Interrupts.EndReceive);
            }
            interruptManager.SetInterrupt(Interrupts.ReceiveTimeout);
            rxStarted = false;
        }

        private void StartTx()
        {
            if(easyDMA)
            {
                // we set these interrupts regardless of the transfer length
                interruptManager.SetInterrupt(Interrupts.TransmitStarted);
                interruptManager.SetInterrupt(Interrupts.TransmitStopped);
                interruptManager.SetInterrupt(Interrupts.EndTransmit);

                if(txMaximumCount.Value == 0)
                {
                    // fake transfer to generate TXSTOPPED (according to the driver, but not the docs)
                    return;
                }
                // Should we preallocate? MAXCNT can reach 0xFFFF, which is quite a lot. We could split, but
                // it's complicated
                var bytesRead = sysbus.ReadBytes(txPointer.Value, (int)txMaximumCount.Value);
                foreach(var character in bytesRead)
                {
                    TransmitCharacter(character);
                }
                txAmount.Value = txMaximumCount.Value;
            }
            interruptManager.SetInterrupt(Interrupts.TransmitReady);
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
        private readonly bool easyDMA;

        private uint currentRxPointer;
        private bool rxStarted;

        private IValueRegisterField rxPointer;
        private IValueRegisterField rxMaximumCount;
        private IValueRegisterField rxAmount;
        private IValueRegisterField txPointer;
        private IValueRegisterField txMaximumCount;
        private IValueRegisterField txAmount;

        private IValueRegisterField baudrate;
        private IEnumRegisterField<ParityConfig> parity;
        private IEnumRegisterField<EnableState> enabled;
        private IFlagRegisterField stopBit;

        private enum Interrupts
        {
            ClearToSend = 0,
            NotClearToSend = 1,
            ReceiveReady = 2,
            EndReceive = 4,
            TransmitReady = 7,
            EndTransmit = 8,
            Error = 9,
            ReceiveTimeout = 17,
            ReceiveStarted = 19,
            TransmitStarted = 20,
            TransmitStopped = 22,
        }

        private enum Registers : long
        {
            StartRx = 0x000,
            StopRx = 0x004,
            StartTx = 0x008,
            StopTx = 0x00C,
            Suspend = 0x01C, // no easyDMA
            FlushRx = 0x02C, // easyDMA
            ClearToSend = 0x100,
            NotClearToSend = 0x104,
            RxDReady = 0x108,
            EndRx = 0x110, // easyDMA
            TxDReady = 0x11C,
            EndTx = 0x120, // easyDMA
            ErrorDetected = 0x124,
            RxTimeout = 0x144,
            RxStarted = 0x14C, // easyDMA
            TxStarted = 0x150, // easyDMA
            TxStopped = 0x158, // easyDMA
            Shortcuts = 0x200,
            InterruptEnable = 0x300, // easyDMA
            InterruptEnableSet = 0x304,
            InterruptEnableClear = 0x308,
            ErrorSource = 0x480,
            Enable = 0x500,
            PinSelectRTS = 0x508,
            PinSelectTXD = 0x50C,
            PinSelectCTS = 0x510,
            PinSelectRXD = 0x514,
            RxD = 0x518, // no easyDMA
            TxD = 0x51C, // no easyDMA
            BaudRate = 0x524,
            RxDPointer = 0x534, // easyDMA
            RxDMaximumCount = 0x538, // easyDMA
            RxDAmount = 0x53C, // easyDMA
            TxDPointer = 0x544, // easyDMA
            TxDMaximumCount = 0x548, // easyDMA
            TxDAmount = 0x54C, // easyDMA
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

        private enum EnableState
        {
            Disabled = 0x0,
            EnabledUART = 0x4,
            EnabledUARTE = 0x8,
        }
    }
}
