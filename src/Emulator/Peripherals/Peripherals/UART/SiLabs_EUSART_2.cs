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
    public class SiLabs_EUSART_2 : UARTBase, IUARTWithBufferState, IDoubleWordPeripheral, IPeripheralContainer<ISPIPeripheral, NullRegistrationPoint>, IKnownSize
    {
        public SiLabs_EUSART_2(Machine machine, uint clockFrequency = 19000000) : base(machine)
        {
            this.machine = machine;
            uartClockFrequency = clockFrequency;

            TransmitIRQ = new GPIO();
            ReceiveIRQ = new GPIO();
            RxFifoLevel = new GPIO();
            TxFifoLevel = new GPIO();
            RxFifoLevelGpioSignal = new GPIO();

            deferTimer = new LimitTimer(machine.ClockSource, 1000000, this, "eusart-defer-timer", 1, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            deferTimer.LimitReached += DeferTimerLimitReached;

            registersCollection = BuildRegistersCollection();
            TxFifoLevel.Set(true);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteRegister(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadRegister(offset);
        }

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
            RxFifoLevelGpioSignal.Set(false);
            rxInProgress = false;

            registersCollection.Reset();
            UpdateInterrupts();
            ScheduleDeferredAction(DeferredAction.ResetTxSignals);
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
                    break;
                case BufferState.Ready:
                case BufferState.Full:
                    if(Count >= (int)rxWatermark.Value + 1
                        && !rxInProgress)
                    {
                        rxInProgress = true;
                        RxFifoLevelGpioSignal.Set(true);
                        RxFifoLevel.Set(true);
                        RxFifoLevelGpioSignal.Set(false);
                        rxInProgress = false;
                    }
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

        public GPIO TxFifoLevel { get; }

        public long Size => 0x4000;

        public event Action<BufferState> BufferStateChanged;

        protected override void CharWritten()
        {
            BufferState nextBufferState = Count == BufferSize ? BufferState.Full : BufferState.Ready;
            if(Count >= (int)rxWatermark.Value + 1)
            {
                rxDataValidInterrupt.Value = true;
                receiveDataValidFlag.Value = true;
                UpdateInterrupts();
            }
            if(nextBufferState == BufferState.Full)
            {
                rxBufferFullInterrupt.Value = true;
                UpdateInterrupts();
            }
            BufferState = nextBufferState;
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

        private void ScheduleDeferredAction(DeferredAction action)
        {
            deferredActionsMask |= (uint)action;
            if(!deferTimer.Enabled)
            {
                deferTimer.Enabled = true;
            }
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
        }

        private byte ReadBuffer()
        {
            byte character;
            return TryGetCharacter(out character) ? character : (byte)0;
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
                    .WithFlag(6, name: "TXOFIF")
                    .WithFlag(7, name: "TXUFIF")
                    .WithFlag(8, name: "PERRIF")
                    .WithFlag(9, name: "FERRIF")
                    .WithFlag(10, name: "MPAFIF")
                    .WithFlag(11, name: "LOADERRIF")
                    .WithFlag(12, name: "CCFIF")
                    .WithFlag(13, name: "TXIDLEIF")
                    .WithReservedBits(14, 2)
                    .WithFlag(16, name: "CSWUIF")
                    .WithReservedBits(17, 1)
                    .WithFlag(18, name: "STARTFIF")
                    .WithFlag(19, name: "SIGFIF")
                    .WithReservedBits(20, 4)
                    .WithFlag(24, name: "AUTOBAUDDONEIF")
                    .WithFlag(25, name: "RXTOIF")
                    .WithReservedBits(26, 6)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out txCompleteInterruptEnable, name: "TXCIEN")
                    .WithFlag(1, out txBufferLevelInterruptEnable, name: "TXFLIEN")
                    .WithFlag(2, out rxDataValidInterruptEnable, name: "RXFLIEN")
                    .WithFlag(3, out rxBufferFullInterruptEnable, name: "RXFULLIEN")
                    .WithFlag(4, out rxOverflowInterruptEnable, name: "RXOFIEN")
                    .WithFlag(5, out rxUnderflowInterruptEnable, name: "RXUFIEN")
                    .WithFlag(6, name: "TXOFIEN")
                    .WithFlag(7, name: "TXUFIEN")
                    .WithFlag(8, name: "PERRIEN")
                    .WithFlag(9, name: "FERRIEN")
                    .WithFlag(10, name: "MPAFIEN")
                    .WithFlag(11, name: "LOADERRIEN")
                    .WithFlag(12, name: "CCFIEN")
                    .WithFlag(13, name: "TXIDLEIEN")
                    .WithReservedBits(14, 2)
                    .WithFlag(16, name: "CSWUIEN")
                    .WithReservedBits(17, 1)
                    .WithFlag(18, name: "STARTFIEN")
                    .WithFlag(19, name: "SIGFIEN")
                    .WithReservedBits(20, 4)
                    .WithFlag(24, name: "AUTOBAUDDONEIEN")
                    .WithFlag(25, name: "RXTOIEN")
                    .WithReservedBits(26, 6)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithFlag(0, changeCallback: (_, value) => { isEnabled = value; }, name: "EN")
                    .WithFlag(1, name: "DISABLING")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Cfg_0, new DoubleWordRegister(this)
                    .WithEnumField(0, 1, out operationModeField, name: "SYNC")
                    .WithFlag(1, name: "LOOPBK")
                    .WithFlag(2, name: "CCEN")
                    .WithFlag(3, name: "MPM")
                    .WithFlag(4, name: "MPAB")
                    .WithEnumField(5, 3, out oversamplingField, name: "OVS")
                    .WithReservedBits(8, 2)
                    .WithFlag(10, name: "MSBF")
                    .WithReservedBits(11, 2)
                    .WithFlag(13, name: "RXINV")
                    .WithFlag(14, name: "TXINV")
                    .WithReservedBits(15, 2)
                    .WithFlag(17, name: "AUTOTRI")
                    .WithReservedBits(18, 2)
                    .WithFlag(20, name: "SKIPPERRF")
                    .WithReservedBits(21, 1)
                    .WithFlag(22, name: "ERRSDMA")
                    .WithFlag(23, name: "ERRSRX")
                    .WithFlag(24, name: "ERRSTX")
                    .WithReservedBits(25, 5)
                    .WithFlag(30, name: "MVDIS")
                    .WithFlag(31, name: "AUTOBAUDEN")
                },
                {(long)Registers.Cfg_1, new DoubleWordRegister(this)
                    .WithFlag(0, name: "DBGHALT")
                    .WithFlag(1, name: "CTSINV")
                    .WithFlag(2, name: "CTSEN")
                    .WithFlag(3, name: "RTSINV")
                    .WithValueField(4, 3, name: "RXTIMEOUT")
                    .WithReservedBits(7,2)
                    .WithFlag(9, name: "TXDMAWU")
                    .WithFlag(10, name: "RXDMAWU")
                    .WithFlag(11, name: "SFUBRX")
                    .WithReservedBits(12,3)
                    .WithFlag(15, name: "RXPRSEN")
                    .WithValueField(16, 4, name: "TXFIW")
                    .WithReservedBits(20,2)
                    .WithValueField(22, 4, name: "RTSRXFW")
                    .WithReservedBits(26,1)
                    .WithValueField(27, 4, out rxWatermark, name: "RXFIW")
                    .WithReservedBits(31,1)
                },
                {(long)Registers.Cfg_2, new DoubleWordRegister(this)
                    .WithFlag(0, name: "MASTER")
                    .WithFlag(1, name: "CLKPOL")
                    .WithFlag(2, name: "CLKPHA")
                    .WithFlag(3, name: "CSINV")
                    .WithFlag(4, name: "AUTOTX")
                    .WithFlag(5, name: "AUTOCS")
                    .WithFlag(6, name: "CLKPRSEN")
                    .WithFlag(7, name: "FORCELOAD")
                    .WithReservedBits(8,16)
                    .WithValueField(24, 8, name: "SDIV")
                },
                {(long)Registers.FrameCfg, new DoubleWordRegister(this)
                    .WithValueField(0, 4, name: "DATABITS")
                    .WithReservedBits(4, 4)
                    .WithEnumField(8, 2, out parityBitModeField, name: "PARITY")
                    .WithReservedBits(10, 2)
                    .WithEnumField(12, 2, out stopBitsModeField, name: "STOPBITS")
                    .WithReservedBits(14, 18)
                },
                {(long)Registers.DtxDataCfg, new DoubleWordRegister(this)
                    .WithValueField(0, 16, name: "DTXDAT")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.IrHfCfg, new DoubleWordRegister(this)
                    .WithFlag(0, name: "IRHFEN")
                    .WithValueField(1, 2, name: "IRHFPW")
                    .WithFlag(3, name: "IRHFFILT")
                    .WithReservedBits(4, 28)
                },
                 {(long)Registers.IrLfCfg, new DoubleWordRegister(this)
                    .WithFlag(0, name: "IRLFEN")
                    .WithReservedBits(1, 31)
                },
                 {(long)Registers.Timing, new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "TXDELAY")
                    .WithReservedBits(2, 2)
                    .WithValueField(4, 3, name: "CSSETUP")
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 3, name: "CSHOLD")
                    .WithReservedBits(11, 1)
                    .WithValueField(12, 3, name: "ICS")
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 4, name: "SETUPWINDOW")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.StartFrameCfg, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "STARTFRAME")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.SigFrameCfg, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "SIGFRAME")
                },
                {(long)Registers.ClkDiv, new DoubleWordRegister(this)
                    .WithReservedBits(0, 3)
                    .WithValueField(3, 20, out fractionalClockDividerField, name: "DIV")
                    .WithReservedBits(23, 9)
                },
                {(long)Registers.TriggerControl, new DoubleWordRegister(this)
                    .WithFlag(0, name: "RXTEN")
                    .WithFlag(1, name: "TXTEN")
                    .WithFlag(2, name: "AUTOTXTEN")
                    .WithReservedBits(3, 29)
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
                    .WithFlag(4, name: "RXBLOCKEN")
                    .WithFlag(5, name: "RXBLOCKDIS")
                    .WithFlag(6, name: "TXTRIEN")
                    .WithFlag(7, name: "TXTRIDIS")
                    .WithFlag(8, name: "CLEARTX")
                    .WithReservedBits(9, 23)
                },
                {(long)Registers.RxData, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => ReadBuffer(), name: "RXDATA")
                    .WithReservedBits(8, 24)
                },
                 {(long)Registers.RxDataPeek, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: (_) => PeekBuffer(), name: "RXDATAP")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.TxData, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write, writeCallback: (_, v) => HandleTxBufferData((byte)v), name: "TXDATA")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Status, new DoubleWordRegister(this, 0x00003040)
                    .WithFlag(0, out receiverEnableFlag, FieldMode.Read, name: "RXENS")
                    .WithFlag(1, out transmitterEnableFlag, FieldMode.Read, name: "TXENS")
                    .WithReservedBits(2,1)
                    .WithFlag(3, name: "RXBLOCK")
                    .WithFlag(4, name: "TXTRI")
                    .WithFlag(5, out transferCompleteFlag, FieldMode.Read, name: "TXC")
                    .WithFlag(6, name: "TXFL")
                    .WithFlag(7, out receiveDataValidFlag, FieldMode.Read, name: "RXFL")
                    .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => Count == BufferSize, name: "RXFULL")
                    .WithReservedBits(9,3)
                    .WithFlag(12, FieldMode.Read, valueProviderCallback: _ => true, name: "RXIDLE")
                    .WithFlag(13, FieldMode.Read, valueProviderCallback: _ => true, name: "TXIDLE")
                    .WithReservedBits(14,2)
                    .WithValueField(16, 5, FieldMode.Read, valueProviderCallback: _ => 0, name: "TXFCNT")
                    .WithReservedBits(21,3)
                    .WithFlag(24, name: "AUTOBAUDDONE")
                    .WithFlag(25, name: "CLEARTXBUSY")
                    .WithReservedBits(26, 6)
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
                    .WithFlag(11, FieldMode.Read, valueProviderCallback: _ => syncBusy, name: "AUTOTXTEN")
                    .WithReservedBits(12, 20)
                    .WithReadCallback((_, __) => {syncBusy = !syncBusy;})
                },
                {(long)Registers.DaliCfg, new DoubleWordRegister(this)
                    .WithFlag(0, name: "DALIEN")
                    .WithValueField(1, 5, name: "DALITXDATABITS")
                    .WithReservedBits(6, 2)
                    .WithValueField(8, 5, name: "DALIRXDATABITS")
                    .WithReservedBits(13, 2)
                    .WithFlag(15, name: "DALIRXENDT")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Test, new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "DBGPRSSEL")
                    .WithReservedBits(2, 30)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }


        private BufferState bufferState;
        private bool syncBusy = false;
        private bool isEnabled = false;
        private bool rxInProgress = false;
        private uint deferredActionsMask = 0;
        private ISPIPeripheral spiSlaveDevice;
        private IFlagRegisterField rxBufferFullInterruptEnable;
        private IFlagRegisterField rxDataValidInterruptEnable;
        private IFlagRegisterField txBufferLevelInterruptEnable;
        private IFlagRegisterField txCompleteInterruptEnable;
        private IFlagRegisterField rxUnderflowInterrupt;
        private IFlagRegisterField transmitterEnableFlag;
        private IFlagRegisterField rxOverflowInterrupt;
        private IFlagRegisterField rxBufferFullInterrupt;
        private IFlagRegisterField rxDataValidInterrupt;
        private IFlagRegisterField txBufferLevelInterrupt;
        private IFlagRegisterField txCompleteInterrupt;
        private IValueRegisterField rxWatermark;
        private IFlagRegisterField receiveDataValidFlag;
        private IFlagRegisterField transferCompleteFlag;
        private IValueRegisterField fractionalClockDividerField;
        private IEnumRegisterField<Bits> stopBitsModeField;
        private IEnumRegisterField<Parity> parityBitModeField;
        private IEnumRegisterField<OversamplingMode> oversamplingField;
        private IEnumRegisterField<OperationMode> operationModeField;
        private IFlagRegisterField rxOverflowInterruptEnable;
        private IFlagRegisterField receiverEnableFlag;
        private IFlagRegisterField rxUnderflowInterruptEnable;
        private readonly Machine machine;
        private readonly uint uartClockFrequency;
        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly LimitTimer deferTimer;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private const int BufferSize = 17; // with shift register

        IEnumerable<IRegistered<ISPIPeripheral, NullRegistrationPoint>> IPeripheralContainer<ISPIPeripheral, NullRegistrationPoint>.Children
        {
            get
            {
                return new[] { Registered.Create(spiSlaveDevice, NullRegistrationPoint.Instance) };
            }
        }

        private enum DeferredAction
        {
            ResetTxSignals = 0x00000001,
        }

        private enum OperationMode
        {
            Asynchronous,
            Synchronous
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
            Cfg_2                                           = 0x0010,
            FrameCfg                                        = 0x001F,
            DtxDataCfg                                      = 0x0018,
            IrHfCfg                                         = 0x001C,
            IrLfCfg                                         = 0x0020,
            Timing                                          = 0x0024,
            StartFrameCfg                                   = 0x0028,
            SigFrameCfg                                     = 0x002C,
            ClkDiv                                          = 0x0030,
            TriggerControl                                  = 0x0034,
            Command                                         = 0x0038,
            RxData                                          = 0x003C,
            RxDataPeek                                      = 0x0040,
            TxData                                          = 0x0044,
            Status                                          = 0x0048,
            InterruptFlag                                   = 0x004C,
            InterruptEnable                                 = 0x0050,
            SyncBusy                                        = 0x0054,
            DaliCfg                                         = 0x0058,
            Test                                            = 0x0100,
            //set
            IpVersion_Set                                   = 0x1000,
            Enable_Set                                      = 0x1004,
            Cfg_0_Set                                       = 0x1008,
            Cfg_1_Set                                       = 0x100C,
            Cfg_2_Set                                       = 0x1010,
            FrameCfg_Set                                    = 0x101F,
            DtxDataCfg_Set                                  = 0x1018,
            IrHfCfg_Set                                     = 0x101C,
            IrLfCfg_Set                                     = 0x1020,
            Timing_Set                                      = 0x1024,
            StartFrameCfg_Set                               = 0x1028,
            SigFrameCfg_Set                                 = 0x102C,
            ClkDiv_Set                                      = 0x1030,
            TriggerControl_Set                              = 0x1034,
            Command_Set                                     = 0x1038,
            RxData_Set                                      = 0x103C,
            RxDataPeek_Set                                  = 0x1040,
            TxData_Set                                      = 0x1044,
            Status_Set                                      = 0x1048,
            InterruptFlag_Set                               = 0x104C,
            InterruptEnable_Set                             = 0x1050,
            SyncBusy_Set                                    = 0x1054,
            DaliCfg_Set                                     = 0x1058,
            Test_Set                                        = 0x1100,
            //clr
            IpVersion_Clr                                   = 0x2000,
            Enable_Clr                                      = 0x2004,
            Cfg_0_Clr                                       = 0x2008,
            Cfg_1_Clr                                       = 0x200C,
            Cfg_2_Clr                                       = 0x2010,
            FrameCfg_Clr                                    = 0x201F,
            DtxDataCfg_Clr                                  = 0x2018,
            IrHfCfg_Clr                                     = 0x201C,
            IrLfCfg_Clr                                     = 0x2020,
            Timing_Clr                                      = 0x2024,
            StartFrameCfg_Clr                               = 0x2028,
            SigFrameCfg_Clr                                 = 0x202C,
            ClkDiv_Clr                                      = 0x2030,
            TriggerControl_Clr                              = 0x2034,
            Command_Clr                                     = 0x2038,
            RxData_Clr                                      = 0x203C,
            RxDataPeek_Clr                                  = 0x2040,
            TxData_Clr                                      = 0x2044,
            Status_Clr                                      = 0x2048,
            InterruptFlag_Clr                               = 0x204C,
            InterruptEnable_Clr                             = 0x2050,
            SyncBusy_Clr                                    = 0x2054,
            DaliCfg_Clr                                     = 0x2058,
            Test_Clr                                        = 0x2100,
            //Toggle
            IpVersion_Tgl                                   = 0x3000,
            Enable_Tgl                                      = 0x3004,
            Cfg_0_Tgl                                       = 0x3008,
            Cfg_1_Tgl                                       = 0x300C,
            Cfg_2_Tgl                                       = 0x3010,
            FrameCfg_Tgl                                    = 0x301F,
            DtxDataCfg_Tgl                                  = 0x3018,
            IrHfCfg_Tgl                                     = 0x301C,
            IrLfCfg_Tgl                                     = 0x3020,
            Timing_Tgl                                      = 0x3024,
            StartFrameCfg_Tgl                               = 0x3028,
            SigFrameCfg_Tgl                                 = 0x302C,
            ClkDiv_Tgl                                      = 0x3030,
            TriggerControl_Tgl                              = 0x3034,
            Command_Tgl                                     = 0x3038,
            RxData_Tgl                                      = 0x303C,
            RxDataPeek_Tgl                                  = 0x3040,
            TxData_Tgl                                      = 0x3044,
            Status_Tgl                                      = 0x3048,
            InterruptFlag_Tgl                               = 0x304C,
            InterruptEnable_Tgl                             = 0x3050,
            SyncBusy_Tgl                                    = 0x3054,
            DaliCfg_Tgl                                     = 0x3058,
            Test_Tgl                                        = 0x3100,
        }
    }
}