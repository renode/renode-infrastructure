//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.DoubleWordToByte)]
    public class SiLabs_EUART_0 : UARTBase, IUARTWithBufferState, IDoubleWordPeripheral, IPeripheralContainer<ISPIPeripheral, NullRegistrationPoint>, IKnownSize
    {
        public SiLabs_EUART_0(Machine machine, uint clockFrequency = 19000000) : base(machine)
        {
            this.machine = machine;
            uartClockFrequency = clockFrequency;

            TransmitIRQ = new GPIO();
            ReceiveIRQ = new GPIO();
            RxFifoLevel = new GPIO();
            TxFifoLevel = new GPIO();
            RxFifoLevelGpioSignal = new GPIO();

            deferTimer = new LimitTimer(machine.ClockSource, 1000000, this, "euart_defer_timer", 1, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            deferTimer.LimitReached += DeferTimerLimitReached;

            registersCollection = BuildRegistersCollection();
            TxFifoLevel.Set(true);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteRegister(offset, value);
        }

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
            base.WriteChar(value);
            this.Log(LogLevel.Noisy, " Character (0x{0:X}) has been written!", value);
        }

        public override void Reset()
        {
            base.Reset();
            isEnabled = false;
            syncBusy = false;
            spiSlaveDevice = null;
            deferredActionsMask = 0;
            deferTimer.Enabled = false;
            RxFifoLevel.Set(false);
            TxFifoLevel.Set(false);

            registersCollection.Reset();
            UpdateInterrupts();
            ScheduleDeferredAction(DeferredAction.ResetTxSignals);
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadRegister(offset);
        }

        public BufferState BufferState
        {
            get
            {
                return bufferState;
            }

            private set
            {
                bufferState = value;
                BufferStateChanged?.Invoke(value);
                this.Log(LogLevel.Noisy, "Buffer state: {0}", bufferState);
                switch(bufferState)
                {
                case BufferState.Empty:
                    RxFifoLevel.Set(false);
                    RxFifoLevelGpioSignal.Set(false);
                    break;
                case BufferState.Ready:
                    if(Count >= (int)rxWatermark.Value + 1)
                    {
                        ScheduleDeferredAction(DeferredAction.RxFifoLevelSignal);
                        RxFifoLevelGpioSignal.Set(true);
                        rxDataValidInterrupt.Value = true;
                        UpdateInterrupts();
                    }
                    else
                    {
                        RxFifoLevel.Set(false);
                        RxFifoLevelGpioSignal.Set(false);
                    }
                    break;
                case BufferState.Full:
                    ScheduleDeferredAction(DeferredAction.RxFifoLevelSignal);
                    RxFifoLevelGpioSignal.Set(true);
                    rxBufferFullInterrupt.Value = true;
                    UpdateInterrupts();
                    break;
                default:
                    this.Log(LogLevel.Error, "Unreachable code. Invalid BufferState value.");
                    return;
                }
            }
        }

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
                case OversamplingMode.Disabled:
                    oversample = 1;
                    break;
                }
                return (uint)(uartClockFrequency / (oversample * (1 + ((double)(fractionalClockDividerField.Value << 3)) / 256)));
            }
        }

        public override Bits StopBits { get { return stopBitsModeField.Value; } }

        public override Parity ParityBit { get { return parityBitModeField.Value; } }

        public GPIO RxFifoLevelGpioSignal { get; }

        public GPIO RxFifoLevel { get; }

        public GPIO ReceiveIRQ { get; }

        public GPIO TransmitIRQ { get; }

        public long Size => 0x4000;

        public GPIO TxFifoLevel { get; }

        public event Action<BufferState> BufferStateChanged;

        protected override void CharWritten()
        {
            if(Count >= (int)rxWatermark.Value + 1)
            {
                rxDataValidInterrupt.Value = true;
                receiveDataValidFlag.Value = true;
                UpdateInterrupts();
            }
            BufferState = Count == BufferSize ? BufferState.Full : BufferState.Ready;
        }

        protected override void QueueEmptied()
        {
            rxDataValidInterrupt.Value = false;
            receiveDataValidFlag.Value = false;
            BufferState = BufferState.Empty;
            UpdateInterrupts();
        }

        protected override bool IsReceiveEnabled => receiverEnableFlag.Value;

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

        private void DeferTimerLimitReached()
        {
            deferTimer.Enabled = false;

            if((deferredActionsMask & (uint)DeferredAction.ResetTxSignals) > 0)
            {
                TxFifoLevel.Set(true);
            }
            if((deferredActionsMask & (uint)DeferredAction.RxFifoLevelSignal) > 0)
            {
                RxFifoLevel.Set(true);
            }

            deferredActionsMask = 0;
        }

        private byte PeekBuffer()
        {
            byte character;
            return TryGetCharacter(out character, true) ? character : (byte)0;
        }

        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var txIrq = ((txCompleteInterruptEnable.Value && txCompleteInterrupt.Value)
                             || (txBufferLevelInterruptEnable.Value && txBufferLevelInterrupt.Value));
                TransmitIRQ.Set(txIrq);

                var rxIrq = ((rxDataValidInterruptEnable.Value && rxDataValidInterrupt.Value)
                             || (rxBufferFullInterruptEnable.Value && rxBufferFullInterrupt.Value)
                             || (rxOverflowInterruptEnable.Value && rxOverflowInterrupt.Value)
                             || (rxUnderflowInterruptEnable.Value && rxUnderflowInterrupt.Value));
                ReceiveIRQ.Set(rxIrq);
            });
        }

        private byte ReadBuffer()
        {
            byte character;
            return TryGetCharacter(out character) ? character : (byte)0;
        }

        private void HandleTxBufferData(byte data)
        {
            this.Log(LogLevel.Noisy, "Handle TX buffer data: {0}", (char)data);

            if(!transmitterEnableFlag.Value)
            {
                this.Log(LogLevel.Warning, "Trying to send data, but the transmitter is disabled: 0x{0:X}", data);
                return;
            }

            transferCompleteFlag.Value = false;
            TransmitCharacter(data);
            txBufferLevelInterrupt.Value = true;
            txCompleteInterrupt.Value = true;
            UpdateInterrupts();
            transferCompleteFlag.Value = true;
        }

        private void ScheduleDeferredAction(DeferredAction action)
        {
            deferredActionsMask |= (uint)action;
            if(!deferTimer.Enabled)
            {
                deferTimer.Enabled = true;
            }
        }

        private uint ReadRegister(long offset, bool internal_read = false)
        {
            var result = 0U;
            long internal_offset = offset;

            // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
            if(offset >= SetRegisterOffset && offset < ClearRegisterOffset)
            {
                // Set register
                internal_offset = offset - SetRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            }
            else if(offset >= ClearRegisterOffset && offset < ToggleRegisterOffset)
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            }
            else if(offset >= ToggleRegisterOffset)
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

        private void WriteRegister(long offset, uint value, bool internal_write = false)
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                long internal_offset = offset;
                uint internal_value = value;

                if(offset >= SetRegisterOffset && offset < ClearRegisterOffset)
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value | value;
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                }
                else if(offset >= ClearRegisterOffset && offset < ToggleRegisterOffset)
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value & ~value;
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                }
                else if(offset >= ToggleRegisterOffset)
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
                    .WithFlag(0, out txCompleteInterrupt, name: "TXCIF")
                    .WithFlag(1, out txBufferLevelInterrupt, name: "TXFLIF")
                    .WithFlag(2, out rxDataValidInterrupt, name: "RXFLIF")
                    .WithFlag(3, out rxBufferFullInterrupt, name: "RXFULLIF")
                    .WithFlag(4, out rxOverflowInterrupt, name: "RXOFIF")
                    .WithFlag(5, out rxUnderflowInterrupt, name: "RXUFIF")
                    .WithTaggedFlag("TXOFIF", 6)
                    .WithReservedBits(7, 1)
                    .WithTaggedFlag("PERRIF", 8)
                    .WithTaggedFlag("FERRIF", 9)
                    .WithTaggedFlag("MPAFIF", 10)
                    .WithReservedBits(11, 1)
                    .WithTaggedFlag("CCFIF", 12)
                    .WithTaggedFlag("TXIDLEIF", 13)
                    .WithReservedBits(14, 4)
                    .WithTaggedFlag("STARTFIF", 18)
                    .WithTaggedFlag("SIGFIF", 19)
                    .WithReservedBits(20, 4)
                    .WithTaggedFlag("AUTOBAUDDONEIF", 24)
                    .WithReservedBits(25, 7)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out txCompleteInterruptEnable, name: "TXCIEN")
                    .WithFlag(1, out txBufferLevelInterruptEnable, name: "TXFLIEN")
                    .WithFlag(2, out rxDataValidInterruptEnable, name: "RXFLIEN")
                    .WithFlag(3, out rxBufferFullInterruptEnable, name: "RXFULLIEN")
                    .WithFlag(4, out rxOverflowInterruptEnable, name: "RXOFIEN")
                    .WithFlag(5, out rxUnderflowInterruptEnable, name: "RXUFIEN")
                    .WithTaggedFlag("TXOFIEN", 6)
                    .WithReservedBits(7, 1)
                    .WithTaggedFlag("PERRIEN", 8)
                    .WithTaggedFlag("FERRIEN", 9)
                    .WithTaggedFlag("MPAFIEN", 10)
                    .WithReservedBits(11, 1)
                    .WithTaggedFlag("CCFIEN", 12)
                    .WithTaggedFlag("TXIDLEIEN", 13)
                    .WithReservedBits(14, 4)
                    .WithTaggedFlag("STARTFIEN", 18)
                    .WithTaggedFlag("SIGFIEN", 19)
                    .WithReservedBits(20, 4)
                    .WithTaggedFlag("AUTOBAUDDONEIEN", 24)
                    .WithReservedBits(25, 7)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithFlag(0, changeCallback: (_, value) => { isEnabled = value; }, name: "EN")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.Cfg_0, new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithTaggedFlag("LOOPBK", 1)
                    .WithTaggedFlag("CCEN", 2)
                    .WithTaggedFlag("MPM", 3)
                    .WithTaggedFlag("MPAB", 4)
                    .WithEnumField(5, 3, out oversamplingField, name: "OVS")
                    .WithReservedBits(8, 2)
                    .WithTaggedFlag("MSBF", 10)
                    .WithReservedBits(11, 2)
                    .WithTaggedFlag("RXINV", 13)
                    .WithTaggedFlag("TXINV", 14)
                    .WithReservedBits(15, 2)
                    .WithTaggedFlag("AUTOTRI", 17)
                    .WithReservedBits(18, 2)
                    .WithTaggedFlag("SKIPPERRF", 20)
                    .WithReservedBits(21, 1)
                    .WithTaggedFlag("ERRSDMA", 22)
                    .WithTaggedFlag("ERRSRX", 23)
                    .WithTaggedFlag("ERRSTX", 24)
                    .WithReservedBits(25, 5)
                    .WithTaggedFlag("MVDIS", 30)
                    .WithTaggedFlag("AUTOBAUDEN", 31)
                },
                {(long)Registers.Cfg_1, new DoubleWordRegister(this)
                    .WithTaggedFlag("DBGHALT", 0)
                    .WithTaggedFlag("CTSINV", 1)
                    .WithTaggedFlag("CTSEN", 2)
                    .WithTaggedFlag("RTSINV", 3)
                    .WithReservedBits(4, 5)
                    .WithTaggedFlag("TXDMAWU", 9)
                    .WithTaggedFlag("RXDMAWU", 10)
                    .WithTaggedFlag("SFUBRX", 11)
                    .WithReservedBits(12, 3)
                    .WithTaggedFlag("RXPRSEN", 15)
                    .WithTag("TXFIW", 16, 2)
                    .WithReservedBits(18, 1)
                    .WithValueField(19, 2, out rxWatermark, name: "RXFIW")
                    .WithReservedBits(21, 1)
                    .WithTag("RTSRXFW", 22, 2)
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.FrameCfg, new DoubleWordRegister(this, 0x2)
                    .WithTag("DATABITS", 0, 2)
                    .WithReservedBits(2, 6)
                    .WithEnumField(8, 2, out parityBitModeField, name: "PARITY")
                    .WithReservedBits(10, 2)
                    .WithEnumField(12, 2, out stopBitsModeField, name: "STOPBITS")
                    .WithReservedBits(14, 18)
                },
                {(long)Registers.IrHfCfg, new DoubleWordRegister(this)
                    .WithTaggedFlag("IRHFEN", 0)
                    .WithTag("IRHFPW", 1, 2)
                    .WithTaggedFlag("IRHFFILT", 3)
                    .WithReservedBits(4, 28)
                },
                 {(long)Registers.IrLfCfg, new DoubleWordRegister(this)
                    .WithTaggedFlag("IRLFEN", 0)
                    .WithReservedBits(1, 31)
                },
                 {(long)Registers.Timing, new DoubleWordRegister(this)
                    .WithTag("TXDELAY", 0, 2)
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.StartFrameCfg, new DoubleWordRegister(this)
                    .WithTag("STARTFRAME", 0, 9)
                    .WithReservedBits(9, 23)
                },
                {(long)Registers.SigFrameCfg, new DoubleWordRegister(this)
                    .WithTag("SIGFRAME", 0, 9)
                    .WithReservedBits(9, 23)
                },
                {(long)Registers.ClkDiv, new DoubleWordRegister(this)
                    .WithReservedBits(0, 3)
                    .WithValueField(3, 20, out fractionalClockDividerField, name: "DIV")
                    .WithReservedBits(23, 9)
                },
                {(long)Registers.TriggerControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("RXTEN", 0)
                    .WithTaggedFlag("TXTEN", 1)
                    .WithReservedBits(2, 30)
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
                    .WithTaggedFlag("RXBLOCKEN", 4)
                    .WithTaggedFlag("RXBLOCKDIS", 5)
                    .WithTaggedFlag("TXTRIEN", 6)
                    .WithTaggedFlag("TXTRIDIS", 7)
                    .WithTaggedFlag("CLEARTX", 8)
                    .WithReservedBits(9, 23)
                },
                {(long)Registers.RxData, new DoubleWordRegister(this)
                    .WithValueField(0, 11, FieldMode.Read, valueProviderCallback: (_) => ReadBuffer(), name: "RXDATA")
                    .WithReservedBits(11, 21)
                },
                 {(long)Registers.RxDataPeek, new DoubleWordRegister(this)
                    .WithValueField(0, 11, FieldMode.Read, valueProviderCallback: (_) => PeekBuffer(), name: "RXDATAP")
                    .WithReservedBits(11, 21)
                },
                {(long)Registers.TxData, new DoubleWordRegister(this)
                    .WithValueField(0, 14, FieldMode.Write, writeCallback: (_, v) => HandleTxBufferData((byte)v), name: "TXDATA")
                    .WithReservedBits(14, 18)
                },
                {(long)Registers.Status, new DoubleWordRegister(this, 0x00003040)
                    .WithFlag(0, out receiverEnableFlag, FieldMode.Read, name: "RXENS")
                    .WithFlag(1, out transmitterEnableFlag, FieldMode.Read, name: "TXENS")
                    .WithReservedBits(2, 1)
                    .WithTaggedFlag("RXBLOCK", 3)
                    .WithTaggedFlag("TXTRI", 4)
                    .WithFlag(5, out transferCompleteFlag, FieldMode.Read, name: "TXC")
                    .WithTaggedFlag("TXFL", 6)
                    .WithFlag(7, out receiveDataValidFlag, FieldMode.Read, name: "RXFL")
                    .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => Count == BufferSize, name: "RXFULL")
                    .WithReservedBits(9, 3)
                    .WithFlag(12, FieldMode.Read, valueProviderCallback: _ => true, name: "RXIDLE")
                    .WithFlag(13, FieldMode.Read, valueProviderCallback: _ => true, name: "TXIDLE")
                    .WithReservedBits(14, 2)
                    .WithValueField(16, 3, FieldMode.Read, valueProviderCallback: _ => 0, name: "TXFCNT")
                    .WithTaggedFlag("CLEARTXBUSY", 19)
                    .WithReservedBits(20, 4)
                    .WithTaggedFlag("AUTOBAUDDONE", 24)
                    .WithReservedBits(25, 7)
                },
                {(long)Registers.SyncBusy, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => syncBusy, name: "DIV")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => syncBusy, name: "RXTEN")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => syncBusy, name: "TXTEN")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => syncBusy, name: "RXEN")
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => syncBusy, name: "RXDIS")
                    .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => syncBusy, name: "TXEN")
                    .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => syncBusy, name: "TXDIS")
                    .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => syncBusy, name: "RXBLOCKEN")
                    .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => syncBusy, name: "RXBLOCKDIS")
                    .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => syncBusy, name: "TXTRIEN")
                    .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => syncBusy, name: "TXTRIDIS")
                    .WithReservedBits(11, 21)
                    .WithReadCallback((_, __) => {syncBusy = !syncBusy;})
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private BufferState bufferState;
        private IFlagRegisterField rxBufferFullInterruptEnable;
        private IFlagRegisterField rxDataValidInterruptEnable;
        private IFlagRegisterField txBufferLevelInterruptEnable;
        private IFlagRegisterField txCompleteInterruptEnable;
        private IFlagRegisterField rxUnderflowInterrupt;
        private IFlagRegisterField rxOverflowInterrupt;
        private IFlagRegisterField rxBufferFullInterrupt;
        private IFlagRegisterField rxDataValidInterrupt;
        private IFlagRegisterField txBufferLevelInterrupt;
        // Interrupts
        private IFlagRegisterField txCompleteInterrupt;
        private IFlagRegisterField transmitterEnableFlag;
        private bool syncBusy = false;
        private IFlagRegisterField receiverEnableFlag;
        private IFlagRegisterField receiveDataValidFlag;
        private IFlagRegisterField transferCompleteFlag;
        private IValueRegisterField fractionalClockDividerField;
        private IEnumRegisterField<Bits> stopBitsModeField;
        private IEnumRegisterField<Parity> parityBitModeField;
        private IEnumRegisterField<OversamplingMode> oversamplingField;
        private uint deferredActionsMask = 0;
        private ISPIPeripheral spiSlaveDevice;
        private IFlagRegisterField rxOverflowInterruptEnable;
        #endregion

        #region fields
        private bool isEnabled = false;
        private IValueRegisterField rxWatermark;
        private IFlagRegisterField rxUnderflowInterruptEnable;
        private readonly uint uartClockFrequency;
        private readonly LimitTimer deferTimer;
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registersCollection;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;

        IEnumerable<IRegistered<ISPIPeripheral, NullRegistrationPoint>> IPeripheralContainer<ISPIPeripheral, NullRegistrationPoint>.Children
        {
            get
            {
                return new[] { Registered.Create(spiSlaveDevice, NullRegistrationPoint.Instance) };
            }
        }

        private const int BufferSize = 5; // with shift register
        #endregion

        #region enums
        private enum DeferredAction
        {
            ResetTxSignals    = 0x00000001,
            RxFifoLevelSignal = 0x00000002,
        }

        private enum OversamplingMode
        {
            Times16,
            Times8,
            Times6,
            Times4,
            Disabled
        }

        private enum Registers : long
        {
            IpVersion                                       = 0x0000,
            Enable                                          = 0x0004,
            Cfg_0                                           = 0x0008,
            Cfg_1                                           = 0x000C,
            FrameCfg                                        = 0x0010,
            IrHfCfg                                         = 0x0014,
            IrLfCfg                                         = 0x0018,
            Timing                                          = 0x001C,
            StartFrameCfg                                   = 0x0020,
            SigFrameCfg                                     = 0x0024,
            ClkDiv                                          = 0x0028,
            TriggerControl                                  = 0x002C,
            Command                                         = 0x0030,
            RxData                                          = 0x0034,
            RxDataPeek                                      = 0x0038,
            TxData                                          = 0x003C,
            Status                                          = 0x0040,
            InterruptFlag                                   = 0x0044,
            InterruptEnable                                 = 0x0048,
            SyncBusy                                        = 0x004C,

            // Set registers
            IpVersion_Set                                   = 0x1000,
            Enable_Set                                      = 0x1004,
            Cfg_0_Set                                       = 0x1008,
            Cfg_1_Set                                       = 0x100C,
            FrameCfg_Set                                    = 0x1010,
            IrHfCfg_Set                                     = 0x1014,
            IrLfCfg_Set                                     = 0x1018,
            Timing_Set                                      = 0x101C,
            StartFrameCfg_Set                               = 0x1020,
            SigFrameCfg_Set                                 = 0x1024,
            ClkDiv_Set                                      = 0x1028,
            TriggerControl_Set                              = 0x102C,
            Command_Set                                     = 0x1030,
            RxData_Set                                      = 0x1034,
            RxDataPeek_Set                                  = 0x1038,
            TxData_Set                                      = 0x103C,
            Status_Set                                      = 0x1040,
            InterruptFlag_Set                               = 0x1044,
            InterruptEnable_Set                             = 0x1048,
            SyncBusy_Set                                    = 0x104C,

            // Clear registers
            IpVersion_Clr                                   = 0x2000,
            Enable_Clr                                      = 0x2004,
            Cfg_0_Clr                                       = 0x2008,
            Cfg_1_Clr                                       = 0x200C,
            FrameCfg_Clr                                    = 0x2010,
            IrHfCfg_Clr                                     = 0x2014,
            IrLfCfg_Clr                                     = 0x2018,
            Timing_Clr                                      = 0x201C,
            StartFrameCfg_Clr                               = 0x2020,
            SigFrameCfg_Clr                                 = 0x2024,
            ClkDiv_Clr                                      = 0x2028,
            TriggerControl_Clr                              = 0x202C,
            Command_Clr                                     = 0x2030,
            RxData_Clr                                      = 0x2034,
            RxDataPeek_Clr                                  = 0x2038,
            TxData_Clr                                      = 0x203C,
            Status_Clr                                      = 0x2040,
            InterruptFlag_Clr                               = 0x2044,
            InterruptEnable_Clr                             = 0x2048,
            SyncBusy_Clr                                    = 0x204C,

            // Toggle registers
            IpVersion_Tgl                                   = 0x3000,
            Enable_Tgl                                      = 0x3004,
            Cfg_0_Tgl                                       = 0x3008,
            Cfg_1_Tgl                                       = 0x300C,
            FrameCfg_Tgl                                    = 0x3010,
            IrHfCfg_Tgl                                     = 0x3014,
            IrLfCfg_Tgl                                     = 0x3018,
            Timing_Tgl                                      = 0x301C,
            StartFrameCfg_Tgl                               = 0x3020,
            SigFrameCfg_Tgl                                 = 0x3024,
            ClkDiv_Tgl                                      = 0x3028,
            TriggerControl_Tgl                              = 0x302C,
            Command_Tgl                                     = 0x3030,
            RxData_Tgl                                      = 0x3034,
            RxDataPeek_Tgl                                  = 0x3038,
            TxData_Tgl                                      = 0x303C,
            Status_Tgl                                      = 0x3040,
            InterruptFlag_Tgl                               = 0x3044,
            InterruptEnable_Tgl                             = 0x3048,
            SyncBusy_Tgl                                    = 0x304C,
        }
        #endregion
    }
}