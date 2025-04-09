//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.DoubleWordToByte)]
    public class EFR32xG2_USART_0 : UARTBase, IUARTWithBufferState, IDoubleWordPeripheral, IPeripheralContainer<ISPIPeripheral, NullRegistrationPoint>, IKnownSize
    {
        public EFR32xG2_USART_0(Machine machine, uint clockFrequency) : base(machine)
        {
            this.machine = machine;
            uartClockFrequency = clockFrequency;

            TransmitIRQ = new GPIO();
            ReceiveIRQ = new GPIO();
            RxDataAvailableRequest = new GPIO();
            RxDataAvailableSingleRequest = new GPIO();
            TxBufferLowRequest = new GPIO();
            TxBufferLowSingleRequest = new GPIO();
            TxEmptyRequest = new GPIO();
            RxDataAvailableRightRequest = new GPIO();
            RxDataAvailableRightSingleRequest = new GPIO();
            RxDataAvailableGpioSignal = new GPIO();
            TxBufferLowRightRequest = new GPIO();
            TxBufferLowRightSingleRequest = new GPIO();

            compareTimer = new LimitTimer(machine.ClockSource, 115214, this, "usart-compare-timer", 255, direction: Direction.Ascending,
                                          enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            compareTimer.LimitReached += CompareTimerHandleLimitReached;

            registersCollection = BuildRegistersCollection();

            TxEmptyRequest.Set(true);
            TxBufferLowRequest.Set(true);
        }

        public override void Reset()
        {
            base.Reset();
            isEnabled = false;
            spiSlaveDevice = null;
            TxEmptyRequest.Set(true);
            TxBufferLowRequest.Set(true);
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadRegister(offset);
        }

        private uint ReadRegister(long offset, bool internal_read = false)
        {
            var result = 0U;
            long internal_offset = offset;

            // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
            if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
            {
                // Set register
                internal_offset = offset - SetRegisterOffset;
                if(!internal_read)
                {  
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            }

            if(!registersCollection.TryRead(internal_offset, out result))
            {
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "Unhandled read at offset 0x{0:X} ({1}).", internal_offset, (Registers)internal_offset);
                }
            }
            else
            {
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", internal_offset, (Registers)internal_offset, result);
                }
            }

            return result;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteRegister(offset, value);
        }

        private void WriteRegister(long offset, uint value, bool internal_write = false)
        {
            machine.ClockSource.ExecuteInLock(delegate {
                long internal_offset = offset;
                uint internal_value = value;

                if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value | value;
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value & ~value;
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                } else if (offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value ^ value;
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                }

                this.Log(LogLevel.Noisy, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);

                if(!registersCollection.TryWrite(internal_offset, internal_value))
                {
                    this.Log(LogLevel.Noisy, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);
                    return;
                }
            });
        }

        private DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptFlag, new DoubleWordRegister(this)
                    // RENODE-80: without this workaround, the RX flow in sl_iostream_usart breaks.
                    // It is unclear if this is the result of a bug in this model or a race condition 
                    // in the embedded code.
                    .WithFlag(0, out txCompleteInterrupt, valueProviderCallback: _ => (txCompleteInterrupt.Value && txCompleteInterruptEnable.Value), name: "TXCIF")
                    .WithFlag(1, out txBufferLevelInterrupt, name: "TXBLIF")
                    .WithFlag(2, out rxDataValidInterrupt, name: "RXDATAVIF")
                    .WithFlag(3, out rxBufferFullInterrupt, name: "RXFULLIF")
                    .WithFlag(4, out rxOverflowInterrupt, name: "RXOFIF")
                    .WithFlag(5, out rxUnderflowInterrupt, name: "RXUFIF")
                    .WithTaggedFlag("TXOFIF", 6)
                    .WithTaggedFlag("TXUFIF", 7)
                    .WithTaggedFlag("PERRIF", 8)
                    .WithTaggedFlag("FERRIF", 9)
                    .WithTaggedFlag("MPAFIF", 10)
                    .WithTaggedFlag("SSMIF", 11)
                    .WithTaggedFlag("CCFIF", 12)
                    .WithTaggedFlag("TXIDLEIF", 13)
                    .WithFlag(14, out timeCompareInterrupt[0], name: "TCMP0IF")
                    .WithFlag(15, out timeCompareInterrupt[1], name: "TCMP1IF")
                    .WithFlag(16, out timeCompareInterrupt[2], name: "TCMP2IF")
                    .WithReservedBits(17, 15)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out txCompleteInterruptEnable, name: "TXCIEN")
                    .WithFlag(1, out txBufferLevelInterruptEnable, name: "TXBLIEN")
                    .WithFlag(2, out rxDataValidInterruptEnable, name: "RXDATAVIEN")
                    .WithFlag(3, out rxBufferFullInterruptEnable, name: "RXFULLIEN")
                    .WithFlag(4, out rxOverflowInterruptEnable, name: "RXOFIEN")
                    .WithFlag(5, out rxUnderflowInterruptEnable, name: "RXUFIEN")
                    .WithTaggedFlag("TXOFIEN", 6)
                    .WithTaggedFlag("TXUFIEN", 7)
                    .WithTaggedFlag("PERRIEN", 8)
                    .WithTaggedFlag("FERRIEN", 9)
                    .WithTaggedFlag("MPAFIEN", 10)
                    .WithTaggedFlag("SSMIEN", 11)
                    .WithTaggedFlag("CCFIEN", 12)
                    .WithTaggedFlag("TXIDLEIEN", 13)
                    .WithFlag(14, out timeCompareInterruptEnable[0], name: "TCMP0IEN")
                    .WithFlag(15, out timeCompareInterruptEnable[1], name: "TCMP1IEN")
                    .WithFlag(16, out timeCompareInterruptEnable[2], name: "TCMP2IEN")
                    .WithReservedBits(17, 15)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithFlag(0, changeCallback: (_, value) => { isEnabled = value; }, name: "EN")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithEnumField(0, 1, out operationModeField, name: "SYNC")
                    .WithTaggedFlag("LOOPBK", 1)
                    .WithTaggedFlag("CCEN", 2)
                    .WithTaggedFlag("MPM", 3)
                    .WithTaggedFlag("MPAB", 4)
                    .WithEnumField(5, 2, out oversamplingField, name: "OVS")
                    .WithReservedBits(7, 1)
                    .WithTaggedFlag("CLKPOL", 8)
                    .WithTaggedFlag("CLKPHA", 9)
                    .WithTaggedFlag("MSBF", 10)
                    .WithTaggedFlag("CSMA", 11)
                    .WithTaggedFlag("TXBIL", 12)
                    .WithTaggedFlag("RXINV", 13)
                    .WithTaggedFlag("TXINV", 14)
                    .WithTaggedFlag("CSINV", 15)
                    .WithTaggedFlag("AUTOCS", 16)
                    .WithTaggedFlag("AUTOTRI", 17)
                    .WithTaggedFlag("SCMODE", 18)
                    .WithTaggedFlag("SCRETRANS", 19)
                    .WithTaggedFlag("SKIPPERRF", 20)
                    .WithTaggedFlag("BIT8DV", 21)
                    .WithTaggedFlag("ERRSDMA", 22)
                    .WithTaggedFlag("ERRSRX", 23)
                    .WithTaggedFlag("ERRSTX", 24)
                    .WithTaggedFlag("SSSEARLY", 25)
                    .WithReservedBits(26, 2)
                    .WithTaggedFlag("BYTESWAP", 28)
                    .WithTaggedFlag("AUTOTX", 29)
                    .WithTaggedFlag("MVDIS", 30)
                    .WithTaggedFlag("SMSDELAY", 31)
                },
                {(long)Registers.FrameFormat, new DoubleWordRegister(this)
                    .WithTag("DATABITS", 0, 4)
                    .WithReservedBits(4, 4)
                    .WithEnumField(8, 2, out parityBitModeField, name: "PARITY")
                    .WithReservedBits(10, 2)
                    .WithEnumField(12, 2, out stopBitsModeField, name: "STOPBITS")
                    .WithReservedBits(14, 18)
                },
                {(long)Registers.TriggerControl, new DoubleWordRegister(this)
                    .WithReservedBits(0, 4)
                    .WithTaggedFlag("RXTEN", 4)
                    .WithTaggedFlag("TXTEN", 5)
                    .WithTaggedFlag("AUTOTXTEN", 6)
                    .WithTaggedFlag("TXARX0EN", 7)
                    .WithTaggedFlag("TXARX1EN", 8)
                    .WithTaggedFlag("TXARX2EN", 9)
                    .WithTaggedFlag("RXATX0EN", 10)
                    .WithTaggedFlag("RXATX1EN", 11)
                    .WithTaggedFlag("RXATX2EN", 12)
                    .WithReservedBits(13, 3)
                    .WithTag("TSEL", 16, 4)
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, newValue) => { if(newValue) receiverEnableFlag.Value = true; }, name: "RXEN")
                    .WithFlag(1, FieldMode.Set, writeCallback: (_, newValue) => { if(newValue) receiverEnableFlag.Value = false; }, name: "RXDIS")
                    .WithFlag(2, FieldMode.Set, writeCallback: (_, newValue) =>
                    {
                        if(newValue)
                        {
                            transmitterEnableFlag.Value = true;
                            txBufferLevelInterrupt.Value = true;
                            UpdateInterrupts();
                        }
                    }, name: "TXEN")
                    .WithFlag(3, FieldMode.Set, writeCallback: (_, newValue) => { if(newValue) transmitterEnableFlag.Value = false; }, name: "TXDIS")
                    .WithTaggedFlag("MASTEREN", 4)
                    .WithTaggedFlag("MASTERDIS", 5)
                    .WithTaggedFlag("RXBLOCKEN", 6)
                    .WithTaggedFlag("RXBLOCKDIS", 7)
                    .WithTaggedFlag("TXTRIEN", 8)
                    .WithTaggedFlag("TXTRIDIS", 9)
                    .WithTaggedFlag("CLEARTX", 10)
                    .WithFlag(11, FieldMode.Set, writeCallback: (_, newValue) => { if(newValue) ClearBuffer(); }, name: "CLEARRX")
                    .WithReservedBits(12, 20)
                },
                {(long)Registers.Status, new DoubleWordRegister(this, 0x00002040)
                    .WithFlag(0, out receiverEnableFlag, FieldMode.Read, name: "RXENS")
                    .WithFlag(1, out transmitterEnableFlag, FieldMode.Read, name: "TXENS")
                    .WithTaggedFlag("MASTER", 2)
                    .WithTaggedFlag("RXBLOCK", 3)
                    .WithTaggedFlag("TXTRI", 4)
                    .WithFlag(5, out transferCompleteFlag, FieldMode.Read, name: "TXC")
                    .WithTaggedFlag("TXBL", 6)
                    .WithFlag(7, out receiveDataValidFlag, FieldMode.Read, name: "RXDATAV")
                    .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => Count == BufferSize, name: "RXFULL")
                    .WithTaggedFlag("TXBDRIGHT", 9)
                    .WithTaggedFlag("TXBSRIGHT", 10)
                    .WithTaggedFlag("RXDATAVRIGHT", 11)
                    .WithTaggedFlag("RXFULLRIGHT", 12)
                    .WithFlag(13, FieldMode.Read, valueProviderCallback: _ => true, name: "TXIDLE")
                    .WithTaggedFlag("TIMERRESTARTED", 14)
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 2, FieldMode.Read, valueProviderCallback: _ => 0, name: "TXBUFCNT")
                    .WithReservedBits(18, 14)
                },
                {(long)Registers.ClockControl, new DoubleWordRegister(this)
                    .WithReservedBits(0, 3)
                    .WithValueField(3, 20, out fractionalClockDividerField, name: "DIV")
                    .WithReservedBits(23, 8)
                    .WithTaggedFlag("AUTOBAUDEN", 31)
                },
                {(long)Registers.RxBufferDataExtended, new DoubleWordRegister(this)
                    .WithTag("RXDATA", 0, 8)
                    .WithReservedBits(8, 5)
                    .WithTaggedFlag("PERR", 14)
                    .WithTaggedFlag("FERR", 15)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.RxBufferData, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => ReadBuffer(), name: "RXDATA")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.RxBufferDoubleDataExtended, new DoubleWordRegister(this)
                    .WithTag("RXDATA0", 0, 8)
                    .WithReservedBits(8, 5)
                    .WithTaggedFlag("PERR0", 14)
                    .WithTaggedFlag("FERR0", 15)
                    .WithTag("RXDATA1", 16, 8)
                    .WithReservedBits(24, 5)
                    .WithTaggedFlag("PERR1", 30)
                    .WithTaggedFlag("FERR1", 31)
                },
                {(long)Registers.RxBufferDoubleData, new DoubleWordRegister(this)
                    .WithTag("RXDATA0", 0, 8)
                    .WithTag("RXDATA1", 8, 8)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.RxBufferDoubleDataExtendedPeek, new DoubleWordRegister(this)
                    .WithTag("RXDATAP", 0, 8)
                    .WithReservedBits(8, 5)
                    .WithTaggedFlag("PERRP", 14)
                    .WithTaggedFlag("FERRP", 15)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.TxBufferDataExtended, new DoubleWordRegister(this)
                    .WithTag("TXDATAX", 0, 8)
                    .WithReservedBits(8, 2)
                    .WithTaggedFlag("UBRXAT", 11)
                    .WithTaggedFlag("TXTRIAT", 12)
                    .WithTaggedFlag("TXBREAK", 13)
                    .WithTaggedFlag("TXDISAT", 14)
                    .WithTaggedFlag("RXENAT", 15)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.TxBufferData, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, v) => HandleTxBufferData((byte)v), name: "TXDATA")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.TxBufferDoubleDataExtended, new DoubleWordRegister(this)
                    .WithTag("TXDATA0", 0, 8)
                    .WithReservedBits(8, 2)
                    .WithTaggedFlag("UBRXAT0", 11)
                    .WithTaggedFlag("TXTRIAT0", 12)
                    .WithTaggedFlag("TXBREAK0", 13)
                    .WithTaggedFlag("TXDISAT0", 14)
                    .WithTaggedFlag("RXENAT0", 15)
                    .WithTag("TXDATA1", 16, 8)
                    .WithReservedBits(24, 2)
                    .WithTaggedFlag("UBRXAT1", 27)
                    .WithTaggedFlag("TXTRIAT1", 28)
                    .WithTaggedFlag("TXBREAK1", 29)
                    .WithTaggedFlag("TXDISAT1", 30)
                    .WithTaggedFlag("RXENAT1", 31)
                },
                {(long)Registers.TxBufferDoubleData, new DoubleWordRegister(this)
                    .WithTag("TXDATA0", 0, 8)
                    .WithTag("TXDATA1", 8, 8)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.IrDAControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("IREN", 0)
                    .WithTag("IRPW", 1, 2)
                    .WithTaggedFlag("IRFILT", 3)
                    .WithReservedBits(4, 3)
                    .WithTaggedFlag("IRPRSEN", 7)
                    .WithTag("IRPRSSEL", 8, 4)
                    .WithReservedBits(12, 20)
                },
                {(long)Registers.I2SControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("EN", 0)
                    .WithTaggedFlag("MONO", 1)
                    .WithTaggedFlag("JUSTIFY", 2)
                    .WithTaggedFlag("DMASPLIT", 3)
                    .WithTaggedFlag("DELAY", 4)
                    .WithReservedBits(5, 3)
                    .WithTag("FORMAT", 8, 3)
                    .WithReservedBits(11, 21)
                },
                {(long)Registers.Timing, new DoubleWordRegister(this)
                    .WithReservedBits(0, 16)
                    .WithTag("TXDELAY", 16, 2)
                    .WithReservedBits(19, 1)
                    .WithTag("CSSETUP", 20, 3)
                    .WithReservedBits(23, 1)
                    .WithTag("ICS", 24, 3)
                    .WithReservedBits(27, 1)
                    .WithTag("CSHOLD", 28, 3)
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.ControlExtended, new DoubleWordRegister(this)
                    .WithTaggedFlag("DBHALT", 0)
                    .WithTaggedFlag("CTSINV", 1)
                    .WithTaggedFlag("CTSEN", 2)
                    .WithTaggedFlag("RTSINV", 3)
                    .WithReservedBits(4, 27)
                    .WithTaggedFlag("GPIODELAYXOREN", 31)
                },
                {(long)Registers.TimeCompare0, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out compareTimerCompare[0], name: "TCMPVAL")
                    .WithReservedBits(8, 8)
                    .WithEnumField<DoubleWordRegister, TimeCompareStartSource>(16, 3, out timeCompareStartSource[0], name: "TSTART")
                    .WithReservedBits(19, 1)
                    .WithEnumField<DoubleWordRegister, TimeCompareStopSource>(20, 3, out timeCompareStopSource[0], name: "TSTOP")
                    .WithReservedBits(23, 1)
                    .WithFlag(24, out timeCompareRestart[0], name: "RESTARTEN")
                    .WithReservedBits(25, 7)
                },
                {(long)Registers.TimeCompare1, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out compareTimerCompare[1], name: "TCMPVAL")
                    .WithReservedBits(8, 8)
                    .WithEnumField<DoubleWordRegister, TimeCompareStartSource>(16, 3, out timeCompareStartSource[1], name: "TSTART")
                    .WithReservedBits(19, 1)
                    .WithEnumField<DoubleWordRegister, TimeCompareStopSource>(20, 3, out timeCompareStopSource[1], name: "TSTOP")
                    .WithReservedBits(23, 1)
                    .WithFlag(24, out timeCompareRestart[1], name: "RESTARTEN")
                    .WithReservedBits(25, 7)
                },
                {(long)Registers.TimeCompare2, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out compareTimerCompare[2], name: "TCMPVAL")
                    .WithReservedBits(8, 8)
                    .WithEnumField<DoubleWordRegister, TimeCompareStartSource>(16, 3, out timeCompareStartSource[2], name: "TSTART")
                    .WithReservedBits(19, 1)
                    .WithEnumField<DoubleWordRegister, TimeCompareStopSource>(20, 3, out timeCompareStopSource[2], name: "TSTOP")
                    .WithReservedBits(23, 1)
                    .WithFlag(24, out timeCompareRestart[2], name: "RESTARTEN")
                    .WithReservedBits(25, 7)
                },
                {(long)Registers.Test, new DoubleWordRegister(this)
                    .WithTaggedFlag("GPIODELAYSTABLE", 0)
                    .WithTaggedFlag("GPIODELAYXOR", 1)
                    .WithReservedBits(2, 30)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x4000;
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registersCollection;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private const uint NumberOfTimeCompareTimers = 3;

#region methods
        public void Register(ISPIPeripheral peripheral, NullRegistrationPoint registrationPoint)
        {
            if(spiSlaveDevice != null)
            {
                throw new RegistrationException("Cannot register more than one peripheral.");
            }
            Machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            spiSlaveDevice = peripheral;
        }

        public void Unregister(ISPIPeripheral peripheral)
        {
            if(peripheral != spiSlaveDevice)
            {
                throw new RegistrationException("Trying to unregister not registered device.");
            }

            Machine.UnregisterAsAChildOf(this, peripheral);
            spiSlaveDevice = null;
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(ISPIPeripheral peripheral)
        {
            if(peripheral != spiSlaveDevice)
            {
                throw new RegistrationException("Trying to obtain a registration point for a not registered device.");
            }

            return new[] { NullRegistrationPoint.Instance };
        }

        public override void WriteChar(byte value)
        {
            if(BufferState == BufferState.Full)
            {
                rxOverflowInterrupt.Value = true;
                UpdateInterrupts();
                this.Log(LogLevel.Warning, "RX buffer is full. Dropping incoming byte (0x{0:X})", value);
                return;
            }
            TriggerCompareTimerStopEvent(TimeCompareStopSource.RxActive);
            base.WriteChar(value);
        }

        IEnumerable<IRegistered<ISPIPeripheral, NullRegistrationPoint>> IPeripheralContainer<ISPIPeripheral, NullRegistrationPoint>.Children
        {
            get
            {
                return new[] { Registered.Create(spiSlaveDevice, NullRegistrationPoint.Instance) };
            }
        }

        public GPIO TransmitIRQ { get; }
        public GPIO ReceiveIRQ { get; }
        public GPIO RxDataAvailableRequest { get; }
        public GPIO RxDataAvailableSingleRequest { get; }
        public GPIO TxBufferLowRequest { get; }
        public GPIO TxBufferLowSingleRequest { get; }
        public GPIO TxEmptyRequest { get; }
        public GPIO RxDataAvailableRightRequest { get; }
        public GPIO RxDataAvailableRightSingleRequest { get; }
        public GPIO RxDataAvailableGpioSignal  { get; }
        public GPIO TxBufferLowRightRequest { get; }
        public GPIO TxBufferLowRightSingleRequest { get; }

        public override Parity ParityBit { get { return parityBitModeField.Value; } }

        public override Bits StopBits { get { return stopBitsModeField.Value; } }

        public override uint BaudRate
        {
            get
            {
                var oversample = 1u;
                switch(oversamplingField.Value)
                {
                    case OversamplingMode.Times16:
                        oversample = 16;
                        break;
                    case OversamplingMode.Times8:
                        oversample = 8;
                        break;
                    case OversamplingMode.Times6:
                        oversample = 6;
                        break;
                    case OversamplingMode.Times4:
                        oversample = 4;
                        break;
                }
                return (uint)(uartClockFrequency / (oversample * (1 + ((double)(fractionalClockDividerField.Value << 3)) / 256)));
            }
        }

        public BufferState BufferState
        {
            get
            {
                return bufferState;
            }

            private set
            {
                if(bufferState == value)
                {
                    return;
                }
                bufferState = value;
                BufferStateChanged?.Invoke(value);
                switch(bufferState)
                {
                    case BufferState.Empty:
                        RxDataAvailableRequest.Set(false);
                        RxDataAvailableSingleRequest.Set(false);
                        RxDataAvailableGpioSignal.Set(false);
                        break;
                    case BufferState.Ready:
                        RxDataAvailableRequest.Set(false);
                        RxDataAvailableSingleRequest.Set(true);
                        RxDataAvailableGpioSignal.Set(true);
                        break;
                    case BufferState.Full:
                        RxDataAvailableRequest.Set(true);
                        RxDataAvailableGpioSignal.Set(true);
                        rxBufferFullInterrupt.Value = true;
                        UpdateInterrupts();
                        break;
                    default:
                        this.Log(LogLevel.Error, "Unreachable code. Invalid BufferState value.");
                        return;
                }
            }
        }

        protected override void CharWritten()
        {
            rxDataValidInterrupt.Value = true;
            receiveDataValidFlag.Value = true;
            UpdateInterrupts();
            BufferState = Count == BufferSize ? BufferState.Full : BufferState.Ready;
            TriggerCompareTimerStopEvent(TimeCompareStopSource.RxInactive);
            TriggerCompareTimerStartEvent(TimeCompareStartSource.RxEndOfFrame);
        }

        protected override void QueueEmptied()
        {
            rxDataValidInterrupt.Value = false;
            receiveDataValidFlag.Value = false;
            BufferState = BufferState.Empty;
            UpdateInterrupts();
        }

        private void HandleTxBufferData(byte data)
        {
            this.Log(LogLevel.Noisy, "Handle TX buffer data: {0}", data);
            
            if(!transmitterEnableFlag.Value)
            {
                this.Log(LogLevel.Warning, "Trying to send data, but the transmitter is disabled: 0x{0:X}", data);
                return;
            }

            TriggerCompareTimerStopEvent(TimeCompareStopSource.TxStart);

            transferCompleteFlag.Value = false;
            if(operationModeField.Value == OperationMode.Synchronous)
            {
                if(spiSlaveDevice != null)
                {
                    var result = spiSlaveDevice.Transmit(data);
                    WriteChar(result);
                }
                else
                {
                    this.Log(LogLevel.Warning, "Writing data in synchronous mode, but no device is currently connected.");
                    WriteChar(0x0);
                }
            }
            else
            {
                TransmitCharacter(data);
                txBufferLevelInterrupt.Value = true;
                txCompleteInterrupt.Value = true;
                UpdateInterrupts();
            }
            transferCompleteFlag.Value = true;
            TriggerCompareTimerStartEvent(TimeCompareStartSource.TxEndOfFrame);
            TriggerCompareTimerStartEvent(TimeCompareStartSource.TxComplete);
        }

        private byte ReadBuffer()
        {
            byte character;
            if (TryGetCharacter(out character))
            {
                return character;
            }
            else
            {
                rxUnderflowInterrupt.Value = true;
                UpdateInterrupts();
                return (byte)0;
            }
        }

        protected void TriggerCompareTimerStartEvent(TimeCompareStartSource source)
        {
            for(uint i=0; i<NumberOfTimeCompareTimers; i++)
            {
                if (timeCompareStartSource[i].Value == source)
                {
                    // From the design book: "The start source enables the comparator, resets the counter, 
                    // and starts the counter. If the counter is already running, the start source will reset 
                    // the counter and restart it."
                    RestartCompareTimer(i);
                    break;
                }
            }
        }

        protected void TriggerCompareTimerStopEvent(TimeCompareStopSource source)
        {
            if (compareTimer.Enabled
                && startIndex < NumberOfTimeCompareTimers
                && source == timeCompareStopSource[startIndex].Value)
            {
                compareTimer.Enabled = false;
                startIndex = 0xFF;
            }
        }

        protected void CompareTimerHandleLimitReached()
        {
            uint timerIndex = startIndex;
            compareTimer.Enabled = false;
            timeCompareInterrupt[startIndex].Value = true;
            startIndex = 0xFF;
            UpdateInterrupts();

            if (timeCompareRestart[timerIndex].Value)
            {
                RestartCompareTimer(timerIndex);
            }
        }

        protected void RestartCompareTimer(uint timerIndex)
        {
            startIndex = (byte)timerIndex;

            // Start source will reset the counter and restart it
            compareTimer.Frequency = BaudRate;
            compareTimer.Limit = compareTimerCompare[timerIndex].Value;
            compareTimer.Enabled = true;
        }

        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                var txIrq = ((txCompleteInterruptEnable.Value && txCompleteInterrupt.Value)
                             || (txBufferLevelInterruptEnable.Value && txBufferLevelInterrupt.Value));
                TransmitIRQ.Set(txIrq);

                var rxIrq = ((rxDataValidInterruptEnable.Value && rxDataValidInterrupt.Value)
                             || (rxBufferFullInterruptEnable.Value && rxBufferFullInterrupt.Value)
                             || (rxOverflowInterruptEnable.Value && rxOverflowInterrupt.Value)
                             || (rxUnderflowInterruptEnable.Value && rxUnderflowInterrupt.Value)
                             || (timeCompareInterruptEnable[0].Value && timeCompareInterrupt[0].Value)
                             || (timeCompareInterruptEnable[1].Value && timeCompareInterrupt[1].Value)
                             || (timeCompareInterruptEnable[2].Value && timeCompareInterrupt[2].Value));
                ReceiveIRQ.Set(rxIrq);
            });
        }

        private bool TrySyncTime()
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
                return true;
            }
            return false;
        }

        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;
        protected override bool IsReceiveEnabled => receiverEnableFlag.Value;
#endregion

#region fields
        private bool isEnabled = false;
        private ISPIPeripheral spiSlaveDevice;
        public event Action<BufferState> BufferStateChanged;
        private IEnumRegisterField<OperationMode> operationModeField;
        private IEnumRegisterField<OversamplingMode> oversamplingField;
        private IEnumRegisterField<Parity> parityBitModeField;       
        private IEnumRegisterField<Bits> stopBitsModeField;
        private IValueRegisterField fractionalClockDividerField;
        private IFlagRegisterField transferCompleteFlag;
        private IFlagRegisterField receiveDataValidFlag;
        private IFlagRegisterField receiverEnableFlag;
        private IFlagRegisterField transmitterEnableFlag;
        private readonly uint uartClockFrequency;
        private BufferState bufferState;
        private const int BufferSize = 3; // with shift register
        private IValueRegisterField[] compareTimerCompare = new IValueRegisterField[NumberOfTimeCompareTimers];
        private IEnumRegisterField<TimeCompareStartSource>[] timeCompareStartSource = new IEnumRegisterField<TimeCompareStartSource>[NumberOfTimeCompareTimers];
        private IEnumRegisterField<TimeCompareStopSource>[] timeCompareStopSource = new IEnumRegisterField<TimeCompareStopSource>[NumberOfTimeCompareTimers];
        private IFlagRegisterField[] timeCompareRestart = new IFlagRegisterField[NumberOfTimeCompareTimers];
        private LimitTimer compareTimer;
        private byte startIndex = 0xFF;
        // Interrupts
        private IFlagRegisterField txCompleteInterrupt;
        private IFlagRegisterField txBufferLevelInterrupt;
        private IFlagRegisterField rxDataValidInterrupt;
        private IFlagRegisterField rxBufferFullInterrupt;
        private IFlagRegisterField rxOverflowInterrupt;
        private IFlagRegisterField rxUnderflowInterrupt;
        private IFlagRegisterField[] timeCompareInterrupt = new IFlagRegisterField[NumberOfTimeCompareTimers];
        private IFlagRegisterField txCompleteInterruptEnable;
        private IFlagRegisterField txBufferLevelInterruptEnable;
        private IFlagRegisterField rxDataValidInterruptEnable;
        private IFlagRegisterField rxBufferFullInterruptEnable;
        private IFlagRegisterField rxOverflowInterruptEnable;
        private IFlagRegisterField rxUnderflowInterruptEnable;
        private IFlagRegisterField[] timeCompareInterruptEnable = new IFlagRegisterField[NumberOfTimeCompareTimers];
#endregion

#region enums
        protected enum OperationMode
        {
            Asynchronous,
            Synchronous
        }

        protected enum OversamplingMode
        {
            Times16,
            Times8,
            Times6,
            Times4
        }

        protected enum TimeCompareStartSource
        {
            Disabled            = 0,
            TxEndOfFrame        = 1,
            TxComplete          = 2,
            RxActive            = 3,
            RxEndOfFrame        = 4,
        }

        protected enum TimeCompareStopSource
        {
            CompareValueReached = 0,
            TxStart             = 1,
            RxActive            = 2,
            RxInactive          = 3,
        }

        private enum Registers
        {
            IpVersion                                       = 0x0000,
            Enable                                          = 0x0004,
            Control                                         = 0x0008,
            FrameFormat                                     = 0x000C,
            TriggerControl                                  = 0x0010,
            Command                                         = 0x0014,
            Status                                          = 0x0018,
            ClockControl                                    = 0x001C,
            RxBufferDataExtended                            = 0x0020,
            RxBufferData                                    = 0x0024,
            RxBufferDoubleDataExtended                      = 0x0028,
            RxBufferDoubleData                              = 0x002C,
            RxBufferDataExtendedPeek                        = 0x0030,
            RxBufferDoubleDataExtendedPeek                  = 0x0034,
            TxBufferDataExtended                            = 0x0038,
            TxBufferData                                    = 0x003C,
            TxBufferDoubleDataExtended                      = 0x0040,
            TxBufferDoubleData                              = 0x0044,
            InterruptFlag                                   = 0x0048,
            InterruptEnable                                 = 0x004C,
            IrDAControl                                     = 0x0050,
            I2SControl                                      = 0x0054,
            Timing                                          = 0x0058,
            ControlExtended                                 = 0x005C,
            TimeCompare0                                    = 0x0060,
            TimeCompare1                                    = 0x0064,
            TimeCompare2                                    = 0x0068,
            Test                                            = 0x006C,
            // Set
            IpVersion_Set                                   = 0x1000,
            Enable_Set                                      = 0x1004,
            Control_Set                                     = 0x1008,
            FrameFormat_Set                                 = 0x100C,
            TriggerControl_Set                              = 0x1010,
            Command_Set                                     = 0x1014,
            Status_Set                                      = 0x1018,
            ClockControl_Set                                = 0x101C,
            RxBufferDataExtended_Set                        = 0x1020,
            RxBufferData_Set                                = 0x1024,
            RxBufferDoubleDataExtended_Set                  = 0x1028,
            RxBufferDoubleData_Set                          = 0x102C,
            RxBufferDataExtendedPeek_Set                    = 0x1030,
            RxBufferDoubleDataExtendedPeek_Set              = 0x1034,
            TxBufferDataExtended_Set                        = 0x1038,
            TxBufferData_Set                                = 0x103C,
            TxBufferDoubleDataExtended_Set                  = 0x1040,
            TxBufferDoubleData_Set                          = 0x1044,
            InterruptFlag_Set                               = 0x1048,
            InterruptEnable_Set                             = 0x104C,
            IrDAControl_Set                                 = 0x1050,
            I2SControl_Set                                  = 0x1054,
            Timing_Set                                      = 0x1058,
            ControlExtended_Set                             = 0x105C,
            TimeCompare0_Set                                = 0x1060,
            TimeCompare1_Set                                = 0x1064,
            TimeCompare2_Set                                = 0x1068,
            Test_Set                                        = 0x106C,            
            // Clear
            IpVersion_Clr                                   = 0x2000,
            Enable_Clr                                      = 0x2004,
            Control_Clr                                     = 0x2008,
            FrameFormat_Clr                                 = 0x200C,
            TriggerControl_Clr                              = 0x2010,
            Command_Clr                                     = 0x2014,
            Status_Clr                                      = 0x2018,
            ClockControl_Clr                                = 0x201C,
            RxBufferDataExtended_Clr                        = 0x2020,
            RxBufferData_Clr                                = 0x2024,
            RxBufferDoubleDataExtended_Clr                  = 0x2028,
            RxBufferDoubleData_Clr                          = 0x202C,
            RxBufferDataExtendedPeek_Clr                    = 0x2030,
            RxBufferDoubleDataExtendedPeek_Clr              = 0x2034,
            TxBufferDataExtended_Clr                        = 0x2038,
            TxBufferData_Clr                                = 0x203C,
            TxBufferDoubleDataExtended_Clr                  = 0x2040,
            TxBufferDoubleData_Clr                          = 0x2044,
            InterruptFlag_Clr                               = 0x2048,
            InterruptEnable_Clr                             = 0x204C,
            IrDAControl_Clr                                 = 0x2050,
            I2SControl_Clr                                  = 0x2054,
            Timing_Clr                                      = 0x2058,
            ControlExtended_Clr                             = 0x205C,
            TimeCompare0_Clr                                = 0x2060,
            TimeCompare1_Clr                                = 0x2064,
            TimeCompare2_Clr                                = 0x2068,
            Test_Clr                                        = 0x206C,            
            // Toggle
            IpVersion_Tgl                                   = 0x3000,
            Enable_Tgl                                      = 0x3004,
            Control_Tgl                                     = 0x3008,
            FrameFormat_Tgl                                 = 0x300C,
            TriggerControl_Tgl                              = 0x3010,
            Command_Tgl                                     = 0x3014,
            Status_Tgl                                      = 0x3018,
            ClockControl_Tgl                                = 0x301C,
            RxBufferDataExtended_Tgl                        = 0x3020,
            RxBufferData_Tgl                                = 0x3024,
            RxBufferDoubleDataExtended_Tgl                  = 0x3028,
            RxBufferDoubleData_Tgl                          = 0x302C,
            RxBufferDataExtendedPeek_Tgl                    = 0x3030,
            RxBufferDoubleDataExtendedPeek_Tgl              = 0x3034,
            TxBufferDataExtended_Tgl                        = 0x3038,
            TxBufferData_Tgl                                = 0x303C,
            TxBufferDoubleDataExtended_Tgl                  = 0x3040,
            TxBufferDoubleData_Tgl                          = 0x3044,
            InterruptFlag_Tgl                               = 0x3048,
            InterruptEnable_Tgl                             = 0x304C,
            IrDAControl_Tgl                                 = 0x3050,
            I2SControl_Tgl                                  = 0x3054,
            Timing_Tgl                                      = 0x3058,
            ControlExtended_Tgl                             = 0x305C,
            TimeCompare0_Tgl                                = 0x3060,
            TimeCompare1_Tgl                                = 0x3064,
            TimeCompare2_Tgl                                = 0x3068,
            Test_Tgl                                        = 0x306C,
        }
#endregion        
    }
}