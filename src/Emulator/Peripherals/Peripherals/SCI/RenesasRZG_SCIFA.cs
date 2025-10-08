//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.SCI
{
    public class RenesasRZG_SCIFA : BasicWordPeripheral, IBytePeripheral, IUART, IHasFrequency, IKnownSize, INumberedGPIOOutput
    {
        public RenesasRZG_SCIFA(IMachine machine, long frequency) : base(machine)
        {
            Frequency = frequency;

            Connections = Enumerable
                .Range(0, NrOfInterrupts)
                .ToDictionary<int, int, IGPIO>(idx => idx, _ => new GPIO());

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            parityBit = Parity.Even;
            receiveQueue.Clear();
            txFifoResetCancellationTokenSrc?.Cancel();
            UpdateInterrupts();
        }

        // Most registers are 16 bit wide, but some are 8 bit.
        // The shorter ones are artificially extended to 16 bits (WithIgnoredBits).
        // We don't use translation to prevent valueProviderCallback from being triggered during writes.
        public byte ReadByte(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.BitRate: // ModulationDuty
            case Registers.FifoDataReceive:
            case Registers.FifoDataTransmit:
            case Registers.SerialExtendedMode:
                return (byte)RegistersCollection.Read(offset);
            default:
                this.ErrorLog(
                    "Trying to read byte from word register at offset 0x{0:X}. Returning 0x0",
                    offset
                );
                return 0;
            }
        }

        public void WriteByte(long offset, byte value)
        {
            switch((Registers)offset)
            {
            case Registers.BitRate: // ModulationDuty
            case Registers.FifoDataReceive:
            case Registers.FifoDataTransmit:
            case Registers.SerialExtendedMode:
                RegistersCollection.Write(offset, (ushort)value);
                break;
            default:
                this.ErrorLog(
                    "Trying to write byte 0x{0:X} to word register at offset 0x{1:X}. Register won't be updated",
                    value,
                    offset
                );
                break;
            }
        }

        public void WriteChar(byte value)
        {
            if(!receiveEnabled.Value)
            {
                this.ErrorLog("Receiver is not enabled, dropping byte: 0x{0:x}", value);
                return;
            }
            receiveQueue.Enqueue(value);
            if(receiveQueue.Count > 0 && receiveQueue.Count < ReceiveFIFOTriggerCount)
            {
                receiveDataReady.Value = true;
            }
            if(receiveQueue.Count >= ReceiveFIFOTriggerCount)
            {
                receiveFifoFull.Value = true;
            }
            UpdateInterrupts();
        }

        public long Size => 0x400;

        // IRQs are bundled into 5 signals per channel in the following order:
        // 0: ERI - Receive error
        // 1: BRI - Break detection or overrun
        // 2: RXI - Receive FIFO data full
        // 3: TXI - Transmit FIFO data empty
        // 4: TEI_DRI - Transmit end / Receive data ready
        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public Bits StopBits => hasTwoStopBits.Value ? Bits.Two : Bits.One;

        public Parity ParityBit => parityEnabled.Value ? parityBit : Parity.None;

        public uint BaudRate
        {
            get
            {
                // We can't perform a shift if the exponent is negative
                var n = clockSource.Value == 0 ?
                    0.5 : 1UL << (2 * (ushort)clockSource.Value - 1);

                var prefix = 64UL;
                if(doubleSpeedMode.Value)
                {
                    prefix /= 2;
                }
                if(asynchronousBaseClock8Times.Value)
                {
                    prefix /= 2;
                }

                return (uint)((Frequency * Math.Pow(10, 6)) / (prefix * n * bitRate.Value)) - 1;
            }
        }

        public long Frequency { get; set; }

        [field: Transient]
        public event Action<byte> CharReceived;

        private void UpdateInterrupts()
        {
            Connections[ReceiveFifoFullIrqIdx].Set(receiveInterruptEnabled.Value && receiveFifoFull.Value);
            // Transmit is always instant, so if the transmit interrupt is enabled
            // and we have written some char, the IRQ triggers.
            Connections[TransmitFifoEmptyIrqIdx].Set(transmitInterruptEnabled.Value && transmitFIFOEmpty.Value);
            Connections[TransmitEndReceiveReadyIrqIdx].Set((receiveInterruptEnabled.Value && receiveDataReady.Value) ||
                                                           (transmitEndInterruptEnabled.Value && transmitEnd.Value));
            // We don't implement these interrupts
            Connections[ReceiveErrorIrqIdx].Set(false);
            Connections[BreakOrOverrunIrqIdx].Set(false);
        }

        private void DefineRegisters()
        {
            Registers.SerialMode.Define(this)
                .WithValueField(0, 2, out clockSource, name: "CKS")
                .WithReservedBits(2, 1)
                .WithFlag(3, out hasTwoStopBits, name: "STOP")
                .WithFlag(4, name: "PM",
                    valueProviderCallback: _ => parityBit == Parity.Odd,
                    writeCallback: (_, value) => parityBit = value ? Parity.Odd : Parity.Even)
                .WithFlag(5, out parityEnabled, name: "PE")
                .WithTaggedFlag("CHR", 6)   // Character length, might be 8 or 7 bit
                .WithEnumField(7, 1, out communicationMode,
                    writeCallback: (_, val) =>
                    {
                        if(val == CommunicationMode.ClockSynchronous)
                        {
                            this.ErrorLog(
                                "{0} is not yet supported. Switching to {1}",
                                nameof(CommunicationMode.ClockSynchronous),
                                nameof(CommunicationMode.Asynchronous)
                            );
                            communicationMode.Value = CommunicationMode.Asynchronous;
                        }
                    },
                    name: "CM")
                .WithReservedBits(8, 8);

            Registers.BitRate.DefineConditional(this, () => registerSelect.Value == ModulationDutyRegisterSelect.BitRate, 0xff)
                .WithValueField(0, 8, out bitRate,
                    writeCallback: (oldVal, newVal) => bitRate.Value = (!transmitEnabled.Value && !receiveEnabled.Value) ? newVal : oldVal,
                    name: "BRR")
                .WithIgnoredBits(8, 8);

            // Conditional, exclusive with BitRate
            Registers.ModulationDuty.DefineConditional(this, () => registerSelect.Value == ModulationDutyRegisterSelect.ModulationDuty, 0xff)
                .WithTag("MDDR", 0, 8)
                .WithIgnoredBits(8, 8);

            Registers.SerialControl.Define(this)
                .WithTag("CKE", 0, 2)
                .WithFlag(2, out transmitEndInterruptEnabled, name: "TEIE")
                .WithTaggedFlag("REIE", 3)
                .WithFlag(4, out receiveEnabled, name: "RE")
                .WithFlag(5, out transmitEnabled, name: "TE",
                    changeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            transmitFIFOEmpty.Value = true;
                        }
                    })
                .WithFlag(6, out receiveInterruptEnabled, name: "RIE")
                .WithFlag(7, out transmitInterruptEnabled, name: "TIE",
                    // We always have empty transmit FIFO
                    changeCallback: (_, __) => transmitFIFOEmpty.Value = true)
                .WithReservedBits(8, 8)
                .WithChangeCallback((_, __) => UpdateInterrupts());

            Registers.FifoDataReceive.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        if(receiveQueue.Count == 0)
                        {
                            this.WarningLog("Trying to read from an empty receive FIFO, returning 0x0.");
                            return 0;
                        }
                        var ret = receiveQueue.Dequeue();
                        receiveFifoFull.Value = false;
                        if(receiveQueue.Count == 0)
                        {
                            receiveDataReady.Value = false;
                        }
                        UpdateInterrupts();
                        return ret;
                    },
                    name: "FRDR")
                .WithIgnoredBits(8, 8);

            Registers.FifoDataTransmit.Define(this)
                .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, val) =>
                    {
                        if(!transmitEnabled.Value)
                        {
                            this.ErrorLog("Transmitter is not enabled, dropping byte: 0x{0:x}", (byte)val);
                            return;
                        }
                        // RZ/G2L Group, RZ/G2LC Group User's Manual:
                        // 1. The hardware manual states that the TDFE and TEND flags are cleared when data is written to the FTDR register.
                        //    However, TDFE is set again when the quantity of data written to FTDR is less than the 'TranmitFIFOTriggerCount',
                        //    and TEND is set when FTDR becomes empty.
                        //    These updates take some time on real hardware but happen immediately in Renode.
                        //
                        // 2. If additional data is written when the transmit FIFO is full, the data is ignored.
                        //
                        // Additionally, software like Zephyr uses the 'FifoDataCount.T' register to determine how much data
                        // can be written to the FTDR at once. The use of an explicit queue is tricky,
                        // as there is no simple way to ensure constant transmission timing.
                        // Therefore, the current solution uses an implicit queue:
                        // After each write to FTDR, a write action is scheduled to run after 'TransmitInterruptDelay' micro seconds,
                        // and the number of scheduled data items is tracked using 'transmitFifoLevel',
                        // which is accessible via the 'FifoDataCount.T' register.

                        if(transmitFifoLevel < MaxFIFOSize)
                        {
                            CharReceived?.Invoke((byte)val);
                            transmitFIFOEmpty.Value = false;
                            transmitEnd.Value = false;
                            UpdateInterrupts();
                            transmitFifoLevel++;
                            var txFifoResetCancellationToken = txFifoResetCancellationTokenSrc.Token;
                            machine.ScheduleAction(TimeInterval.FromMicroseconds(TransmitInterruptDelay), ___ =>
                            {
                                if(!txFifoResetCancellationToken.IsCancellationRequested)
                                {
                                    transmitFifoLevel--;
                                    if(transmitFifoLevel < 0)
                                    {
                                        this.ErrorLog("Transmit FIFO contains a negative amount of characters, resetting it to 0");
                                        transmitFifoLevel = 0;
                                    }
                                    if(transmitFifoLevel <= TranmitFIFOTriggerCount)
                                    {
                                        transmitFIFOEmpty.Value = true;
                                    }
                                    if(transmitFifoLevel <= 0)
                                    {
                                        transmitEnd.Value = true;
                                    }
                                    UpdateInterrupts();
                                }
                            });
                        }
                        else
                        {
                            this.ErrorLog("Transmit FIFO is full, dropping byte: 0x{0:x}", (byte)val);
                        }
                    },
                    name: "FTDR")
                .WithIgnoredBits(8, 8);

            // According to the documentation this register should have a reset value of 0x20.
            // Some software expects the TEND flag to be set even before transmitting the first character.
            // Error status flags are modeled as fields to reduce the amount of logs generated during the simulation.
            //
            // This register has an uncommon property: "0 can be only written to clear the flag after 1 is read."
            // from: RZG/2L PDF Datasheet section 22.2.7 Serial Status Register (FSR)
            // It is solved via RenesasFlagState flag extension tracking whether flag was read after most recent write (also via code).
            Registers.SerialStatus.Define(this, 0x60)
                .WithWriteAllowedAfterReadingOneFlag(0, this, out receiveDataReady, "RD")
                .WithWriteAllowedAfterReadingOneFlag(1, this, out receiveFifoFull, "RDF")
                .WithFlag(2, FieldMode.Read, name: "PER")
                .WithFlag(3, FieldMode.Read, name: "FER")
                .WithWriteAllowedAfterReadingOneFlag(4, this, "BRK")
                .WithWriteAllowedAfterReadingOneFlag(5, this, out transmitFIFOEmpty, "TDFE")
                .WithWriteAllowedAfterReadingOneFlag(6, this, out transmitEnd, "TEND")
                .WithWriteAllowedAfterReadingOneFlag(7, this, "ER")
                .WithReservedBits(8, 8)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.FifoControl.Define(this)
                .WithTaggedFlag("LOOP", 0)
                .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "RFRST", writeCallback: (_, __) => receiveQueue.Clear())
                .WithFlag(2, FieldMode.Read | FieldMode.WriteOneToClear, name: "TFRST",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            txFifoResetCancellationTokenSrc?.Cancel();
                            txFifoResetCancellationTokenSrc = new CancellationTokenSource();
                            transmitFifoLevel = 0;
                            transmitFIFOEmpty.Value = true;
                            transmitEnd.Value = true;
                        }
                    })
                .WithTaggedFlag("MCE", 3)
                .WithValueField(4, 2, out transmitFifoDataTriggerNumberSelect, name: "TTRG")
                .WithValueField(6, 2, out receiveFifoDataTriggerNumberSelect, name: "RTRG")
                .WithTag("RSTRG", 8, 3)
                .WithReservedBits(11, 5)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.FifoDataCount.Define(this)
                .WithValueField(0, 5, FieldMode.Read, name: "R",
                    valueProviderCallback: _ => (ulong)receiveQueue.Count >= MaxFIFOSize ? MaxFIFOSize : (ulong)receiveQueue.Count)
                .WithReservedBits(5, 3)
                .WithValueField(8, 5, FieldMode.Read, name: "T",
                    valueProviderCallback: _ => (ulong)transmitFifoLevel >= MaxFIFOSize ? MaxFIFOSize : (ulong)transmitFifoLevel)
                .WithReservedBits(13, 3);

            Registers.SerialPort.Define(this)
                .WithTaggedFlag("SPB2DT", 0)
                .WithTaggedFlag("SPB2IO", 1)
                .WithTaggedFlag("SCKDT", 2)
                .WithTaggedFlag("SCKIO", 3)
                .WithTaggedFlag("CTS2DT", 4)
                .WithTaggedFlag("CTS2IO", 5)
                .WithTaggedFlag("RTS2DT", 6)
                .WithTaggedFlag("RTS2IO", 7)
                .WithReservedBits(8, 8);

            Registers.LineStatus.Define(this)
                .WithTaggedFlag("ORER", 0)
                .WithReservedBits(1, 1)
                // We don't model parity errors
                .WithTag("FER", 2, 5)
                .WithReservedBits(7, 1)
                .WithTag("PER", 8, 5)
                .WithReservedBits(13, 3);

            Registers.SerialExtendedMode.Define(this)
                .WithFlag(0, out asynchronousBaseClock8Times, name: "ABCS0")
                .WithReservedBits(1, 1)
                .WithTaggedFlag("NFEN", 2)
                .WithTaggedFlag("DIR", 3)
                .WithEnumField(4, 1, out registerSelect, name: "MDDRS")
                .WithTaggedFlag("BRME", 5)
                .WithReservedBits(6, 1)
                .WithFlag(7, out doubleSpeedMode, name: "BGDM")
                .WithIgnoredBits(8, 8);

            // We don't update interrupts here as docs don't specify that we should do it.
            // They seem to indicate that interrupt should only be triggered when FIFO count changes.
            // From RZ/G2L Group, RZ/G2LC Group User's Manual: Hardware, chapter 22:
            // "When the quantity of transmit data written in the FTDR register as a result of transmission
            // is equal to or less than the specified transmission trigger number ... interrupt request is generated"
            // or
            // "When the number of entries in the reception FIFO ... rises to or above the specified trigger number for reception,
            //  the RDF flag is set to 1 and a receive FIFO data full interrupt (RXI) request is generated"
            Registers.FifoTriggerControl.Define(this, 0x1f1f)
                .WithValueField(0, 5, out transmitFifoDataTriggerNumber, name: "TFTC")
                .WithReservedBits(5, 2)
                .WithFlag(7, out transmitTriggerSelect, name: "TTRGS")
                .WithValueField(8, 5, out receiveFifoDataTriggerNumber, name: "RFTC")
                .WithReservedBits(13, 2)
                .WithFlag(15, out receiveTriggerSelect, name: "RTRGS");
        }

        private int ReceiveFIFOTriggerCount
        {
            get
            {
                if(receiveTriggerSelect.Value)
                {
                    return (int)receiveFifoDataTriggerNumber.Value;
                }
                else
                {
                    switch(receiveFifoDataTriggerNumberSelect.Value)
                    {
                    case 0:
                        return 1;
                    case 1:
                        return 4;
                    case 2:
                        return 8;
                    case 3:
                        return 14;
                    default:
                        this.ErrorLog(
                            "{0} has invalid value {1}. Defaulting to 0x1.",
                            nameof(receiveFifoDataTriggerNumberSelect),
                            receiveFifoDataTriggerNumberSelect.Value
                        );
                        return 1;
                    }
                }
            }
        }

        private int TranmitFIFOTriggerCount
        {
            get
            {
                if(transmitTriggerSelect.Value)
                {
                    return (int)transmitFifoDataTriggerNumber.Value;
                }
                else
                {
                    switch(transmitFifoDataTriggerNumberSelect.Value)
                    {
                    case 0:
                        return 8;
                    case 1:
                        return 4;
                    case 2:
                        return 2;
                    case 3:
                        return 0;
                    default:
                        this.ErrorLog(
                            "{0} has invalid value {1}. Defaulting to 0x1.",
                            nameof(transmitFifoDataTriggerNumberSelect),
                            transmitFifoDataTriggerNumberSelect.Value
                        );
                        return 1;
                    }
                }
            }
        }

        private Parity parityBit;

        private IFlagRegisterField hasTwoStopBits;
        private IFlagRegisterField parityEnabled;
        private IFlagRegisterField doubleSpeedMode;
        private IFlagRegisterField transmitEnabled;
        private IFlagRegisterField transmitEndInterruptEnabled;
        private IFlagRegisterField receiveEnabled;
        private IFlagRegisterField receiveInterruptEnabled;
        private IFlagRegisterField transmitInterruptEnabled;
        private WriteAllowedAfterReadingOneFlag transmitFIFOEmpty;
        private WriteAllowedAfterReadingOneFlag transmitEnd;
        private WriteAllowedAfterReadingOneFlag receiveDataReady;
        private WriteAllowedAfterReadingOneFlag receiveFifoFull;
        private IValueRegisterField bitRate;
        private IValueRegisterField clockSource;
        private IValueRegisterField receiveFifoDataTriggerNumber;
        private IFlagRegisterField receiveTriggerSelect;
        private IValueRegisterField receiveFifoDataTriggerNumberSelect;
        private IValueRegisterField transmitFifoDataTriggerNumber;
        private IFlagRegisterField transmitTriggerSelect;
        private IValueRegisterField transmitFifoDataTriggerNumberSelect;
        private IFlagRegisterField asynchronousBaseClock8Times;
        private IEnumRegisterField<ModulationDutyRegisterSelect> registerSelect;
        private IEnumRegisterField<CommunicationMode> communicationMode;
        private int transmitFifoLevel = 0;
        private CancellationTokenSource txFifoResetCancellationTokenSrc;

        private readonly Queue<byte> receiveQueue = new Queue<byte>();

        private const int MaxFIFOSize = 16;
        private const int NrOfInterrupts = 5;
        private const int ReceiveErrorIrqIdx = 0;
        private const int BreakOrOverrunIrqIdx = 1;
        private const int ReceiveFifoFullIrqIdx = 2;
        private const int TransmitFifoEmptyIrqIdx = 3;
        private const int TransmitEndReceiveReadyIrqIdx = 4;
        private const int TransmitInterruptDelay = 10;

        internal class WriteAllowedAfterReadingOneFlag
        {
            public WriteAllowedAfterReadingOneFlag(int position, PeripheralRegister register, IPeripheral parent, string name)
            {
                flag = register.DefineFlagField(position, FieldMode.Read | FieldMode.WriteZeroToClear, readCallback: ReadCallback, changeCallback: ChangeCallback, name: name);
                this.name = name;
                this.parent = parent;
            }

            public bool Value
            {
                get => flag.Value;
                set
                {
                    flag.Value = value;
                    canWrite = false;
                }
            }

            private void ReadCallback(bool oldValue, bool value)
            {
                if(oldValue)
                {
                    canWrite = true;
                }
            }

            private void ChangeCallback(bool oldValue, bool value)
            {
                if(!canWrite)
                {
                    parent.WarningLog("Flag {0} was changed while this was not allowed, write ignored", name);
                    flag.Value = oldValue;
                }
                canWrite = false;
            }

            private bool canWrite;
            private readonly IFlagRegisterField flag;
            private readonly string name;
            private readonly IPeripheral parent;
        }

        private enum CommunicationMode
        {
            Asynchronous = 0,
            ClockSynchronous = 1,
        }

        private enum ModulationDutyRegisterSelect
        {
            BitRate,
            ModulationDuty
        }

        private enum Registers
        {
            SerialMode          = 0x0,  // SMR
            // BitRate and ModulationDuty exist at the same address
            // but are switchable at runtime via SEMR.MDDRS
            BitRate             = 0x2,  // BRR
            ModulationDuty      = 0x2,  // MDDR
            SerialControl       = 0x4,  // SCR
            FifoDataTransmit    = 0x6,  // FTDR
            SerialStatus        = 0x8,  // FSR
            FifoDataReceive     = 0xA,  // FRDR
            FifoControl         = 0xC,  // FCR
            FifoDataCount       = 0xE,  // FDR
            SerialPort          = 0x10, // SPTR
            LineStatus          = 0x12, // LSR
            SerialExtendedMode  = 0x14,  // SEMR
            FifoTriggerControl  = 0x16,  // FTCR
        }
    }

    internal static class RenesasRZG_SCIFA_Extensions
    {
        public static T WithWriteAllowedAfterReadingOneFlag<T>(this T register, int position, IPeripheral parent, string name = null)
            where T : PeripheralRegister
        {
            return WithWriteAllowedAfterReadingOneFlag(register, position, parent, out _, name);
        }

        public static T WithWriteAllowedAfterReadingOneFlag<T>(this T register, int position, IPeripheral parent, out RenesasRZG_SCIFA.WriteAllowedAfterReadingOneFlag flag, string name = null)
            where T : PeripheralRegister
        {
            flag = new RenesasRZG_SCIFA.WriteAllowedAfterReadingOneFlag(position, register, parent, name);
            return register;
        }
    }
}