//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CAN;

// We manually implement word- and double-word-to-byte conversions for writes so
// TSNEXT and TSONE/TSALL can be set together before the transmission is sent.
[AllowedTranslations(AllowedTranslation.QuadWordToDoubleWord)]
public class CAST_CANCTRL : BasicBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IKnownSize, ICAN
{
    public CAST_CANCTRL(IMachine machine, ulong timestampClockFrequency = 1000000) : base(machine)
    {
        IRQ = new GPIO();
        transmitBuffer = new byte[TransmitBufferSize];
        secondaryWorkingBuffer = new byte[TransmitBufferSize];
        receiveBuffer = new Queue<byte[]>(ReceiveSlotCount);
        ciA603Counter = new LimitTimer(machine.ClockSource, timestampClockFrequency, this, nameof(ciA603Counter), direction: Direction.Ascending);

        counterRegisters = new DoubleWordRegisterCollection(this);
        // Enable translations for narrower accesses to the counter registers.
        readByteFromCounter = ReadWriteExtensions.BuildByteReadUsing(ReadDoubleWordFromCounter);
        writeByteToCounter = ReadWriteExtensions.BuildByteWriteUsing(ReadDoubleWordFromCounter, WriteDoubleWordToCounter);
        readWordFromCounter = ReadWriteExtensions.BuildWordReadUsing(ReadDoubleWordFromCounter);
        writeWordToCounter = ReadWriteExtensions.BuildWordWriteUsing(ReadDoubleWordFromCounter, WriteDoubleWordToCounter);
        DefineRegisters();

        Reset();
    }

    public override void Reset()
    {
        SoftwareReset();
        Array.Clear(transmitBuffer, 0, transmitBuffer.Length);
        Array.Clear(secondaryWorkingBuffer, 0, secondaryWorkingBuffer.Length);
        Array.Clear(acceptanceFilterCode, 0, acceptanceFilterCode.Length);
        Array.Clear(acceptanceFilterMask, 0, acceptanceFilterMask.Length);
        for(var i = 0; i < sizeof(uint); ++i)
        {
            acceptanceFilterMask[0, i] = i == 3 ? (byte)0x1F : (byte)0xFF;
        }
        ciA603Counter.Reset();
        counterRegisters.Reset();
        transmissionTimeStamp = 0;
        base.Reset();
    }

    public void OnFrameReceived(CANMessageFrame message)
    {
        if(loopBackInternal.Value)
        {
            return;
        }
        if(transmitStandby.Value)
        {
            // The first bus activity wakes the transceiver, but its frame is lost (section 15.1.4.8.5).
            transmitStandby.Value = false;
            return;
        }
        if(softwareReset.Value)
        {
            return;
        }
        this.DebugLog("Received {0} bytes [{1}] on id 0x{2:X}", message.Data.Length, message.DataAsHex, message.Id);
        var accepted = FilterFrame(message);
        this.DebugLog("Frame on id 0x{0:X} {1} by the acceptance filters", message.Id, accepted ? "accepted" : "rejected");
        if(!accepted)
        {
            return;
        }
        StoreFrame(message, transmitted: false);
        UpdateInterrupts();
    }

    public override void WriteByte(long offset, byte value)
    {
        if(!softwareReset.Value)
        {
            // Bit timing and acceptance codes/masks can only be programmed in reset (Table 15-9).
            if((offset >= (long)Registers.SlowSpeedBitTiming1 && offset <= (long)Registers.FastSpeedPrescaler)
                || offset == (long)Registers.TransmitterDelayCompensation
                || (offset >= (long)Registers.AcceptanceFilterCodeMask && offset < (long)Registers.AcceptanceFilterCodeMask + sizeof(uint)))
            {
                this.WarningLog("Ignored write to reset-protected register {0} at 0x{1:X}", (Registers)offset, offset);
                return;
            }
        }
        base.WriteByte(offset, value);
        if(softwareReset.Value)
        {
            SoftwareReset();
        }
        if(!writingMultipleBytes)
        {
            ProcessTransmissions();
        }
    }

    public ushort ReadWord(long offset) => this.ReadWordUsingByte(offset);

    public uint ReadDoubleWord(long offset) => this.ReadDoubleWordUsingByte(offset);

    public void WriteWord(long offset, ushort value)
    {
        WriteMultipleBytes(offset, value, sizeof(ushort));
    }

    public void WriteDoubleWord(long offset, uint value)
    {
        WriteMultipleBytes(offset, value, sizeof(uint));
    }

    [ConnectionRegion("counter")]
    public byte ReadByteFromCounter(long offset) => readByteFromCounter(offset);

    [ConnectionRegion("counter")]
    public void WriteByteToCounter(long offset, byte value) => writeByteToCounter(offset, value);

    [ConnectionRegion("counter")]
    public ushort ReadWordFromCounter(long offset) => readWordFromCounter(offset);

    [ConnectionRegion("counter")]
    public void WriteWordToCounter(long offset, ushort value) => writeWordToCounter(offset, value);

    [ConnectionRegion("counter")]
    public uint ReadDoubleWordFromCounter(long offset) => counterRegisters.Read(offset);

    [ConnectionRegion("counter")]
    public void WriteDoubleWordToCounter(long offset, uint value)
    {
        if(sysbus.TryGetCurrentCPU(out var cpu))
        {
            cpu.SyncTime();
        }
        counterRegisters.Write(offset, value);
    }

    public GPIO IRQ { get; }

    public long Size => 0x1000;

    public event Action<CANMessageFrame> FrameSent;

    protected override void DefineRegisters()
    {
        Registers.ReceiveBuffer.DefineMany(this, ReceiveBufferSize, setup: (register, i) => register
            .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => ReadReceiveBufferByte(i), name: $"RBUF[{i}]")
        );

        Registers.TransmitBuffer.DefineMany(this, TransmitBufferSize, setup: (register, i) => register
            .WithValueField(0, 8, writeCallback: (_, value) =>
            {
                if(transmitBufferSelect.Value == TransmitBufferSelect.PrimaryTransmitBuffer ? primaryTransmissionRequested : secondaryNextPending)
                {
                    // Writing to TBUF is blocked while the TSNEXT bit is set and the STB is full (section 15.1.4.7.3.2).
                    this.DebugLog("Blocked a write to a locked transmit buffer");
                    return;
                }
                CurrentTransmitBuffer[i] = (byte)value;
            },
            valueProviderCallback: _ => CurrentTransmitBuffer[i], name: $"TBUF[{i}]")
        );

        Registers.TransmissionTimeStamp.DefineMany(this, sizeof(uint), setup: (register, i) => register
            .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ =>
                BitHelper.GetValue(transmissionTimeStamp, 8 * i, 8), name: $"TTS[{i}]"));

        Registers.ConfigurationAndStatus.Define(this, 0x80)
            .WithFlag(7, out softwareReset, name: "RESET")
            .WithFlag(6, out loopBackExternal, name: "LBME")
            .WithFlag(5, out loopBackInternal, name: "LBMI")
            .WithFlag(4, out primarySingleShot, name: "TPSS")
            .WithFlag(3, out secondarySingleShot, name: "TSSS")
            .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "RACTIVE")
            .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "TACTIVE")
            .WithTaggedFlag("BUSOFF", 0)
            .WithWriteCallback((_, __) =>
            {
                if(softwareReset.Value)
                {
                    SoftwareReset();
                }
            });

        Registers.TransmitCommand.Define(this)
            .WithEnumField(7, 1, out transmitBufferSelect, name: "TBSEL", changeCallback: (_, value) =>
            {
                if(softwareReset.Value)
                {
                    transmitBufferSelect.Value = TransmitBufferSelect.PrimaryTransmitBuffer;
                }
                this.NoisyLog("Transmit buffer select set to {0}", transmitBufferSelect.Value);
            })
            // LOM and STBY can always be cleared, but cannot be set while a transmission is requested.
            .WithConditionallyWritableFlag(6, out listenOnlyMode, () => !listenOnlyMode.Value || CheckNoTransmissionRequested("LOM"), name: "LOM")
            .WithConditionallyWritableFlag(5, out transmitStandby, () => !transmitStandby.Value || CheckNoTransmissionRequested("STBY"), name: "STBY")
            .WithFlag(4, out var transmitPrimaryEnable, valueProviderCallback: _ => primaryTransmissionRequested, name: "TPE")
            .WithFlag(3, out var transmitPrimaryAbort, valueProviderCallback: _ => primaryAbortRequested, name: "TPA")
            .WithFlag(2, out var transmitSecondaryOne, valueProviderCallback: _ => secondaryRequest == SecondaryTransmissionRequest.SingleFrame, name: "TSONE")
            .WithFlag(1, out var transmitSecondaryAll, valueProviderCallback: _ => secondaryRequest == SecondaryTransmissionRequest.AllFrames, name: "TSALL")
            .WithFlag(0, out var transmitSecondaryAbort, valueProviderCallback: _ => secondaryAbortRequested, name: "TSA")
            .WithWriteCallback((_, __) =>
            {
                if(transmitPrimaryEnable.Value && CheckTransmissionAllowed("TPE"))
                {
                    primaryTransmissionRequested = true;
                }
                if((transmitSecondaryOne.Value || transmitSecondaryAll.Value) && secondaryRequest == SecondaryTransmissionRequest.None && CheckTransmissionAllowed("TSONE/TSALL"))
                {
                    // Neither request can replace a pending one and TSALL wins when both are written together.
                    secondaryRequest = transmitSecondaryAll.Value ? SecondaryTransmissionRequest.AllFrames : SecondaryTransmissionRequest.SingleFrame;
                    secondaryTransmissionHadFrame = false;
                }
                primaryAbortRequested |= transmitPrimaryAbort.Value;
                secondaryAbortRequested |= transmitSecondaryAbort.Value;
            });

        Registers.TransmitControl.Define(this, 0x84)
            .WithConditionallyWritableFlag(7, out _, () => softwareReset.Value, name: "FD_ISO")
            .WithFlag(6, valueProviderCallback: _ => secondaryNextPending, name: "TSNEXT", writeCallback: (_, value) =>
            {
                if(value)
                {
                    FillSecondarySlot();
                }
            })
            .WithEnumField(5, 1, out transmitSecondaryOperationMode, name: "TSMODE", changeCallback: (_, value) => this.NoisyLog("Secondary transmit buffer operation mode set to {0}", value))
            .WithReservedBits(2, 3)
            .WithValueField(0, 2, FieldMode.Read, valueProviderCallback: _ => (ulong)CurrentSecondaryBufferStatus, name: "TSSTAT");

        Registers.ReceiveControl.Define(this)
            .WithValueField(0, 2, FieldMode.Read, valueProviderCallback: _ => (ulong)CurrentReceiveBufferStatus, name: "RSTAT")
            .WithReservedBits(2, 1)
            .WithFlag(3, out receiveAll, name: "RBALL")
            .WithFlag(4, FieldMode.Write, name: "RREL", writeCallback: (_, value) =>
            {
                if(value)
                {
                    ReleaseReceiveSlot();
                }
            })
            .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => receiveOverflow, name: "ROV")
            .WithFlag(6, out receiveOverwriteMode, name: "ROM")
            .WithFlag(7, out selfAcknowledge, name: "SACK");

        Registers.ReceiveTransmitInterruptEnable.Define(this, 0xFE)
            .WithFlag(7, out receiveInterruptEnable, name: "RIE")
            .WithFlag(6, out receiveOverflowInterruptEnable, name: "ROIE")
            .WithFlag(5, out receiveFullInterruptEnable, name: "RFIE")
            .WithFlag(4, out receiveAlmostFullInterruptEnable, name: "RAFIE")
            .WithFlag(3, out transmitPrimaryInterruptEnable, name: "TPIE")
            .WithFlag(2, out transmitSecondaryInterruptEnable, name: "TSIE")
            .WithTaggedFlag("EIE", 1)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => secondaryBufferQueue.Count == SecondarySlotCount, name: "TSFF")
            .WithWriteCallback((_, __) => UpdateInterrupts());

        Registers.ReceiveTransmitInterruptFlag.Define(this)
            .WithFlag(7, out receiveInterruptFlag, FieldMode.WriteOneToClear | FieldMode.Read, name: "RIF")
            .WithFlag(6, out receiveOverflowInterruptFlag, FieldMode.WriteOneToClear | FieldMode.Read, name: "ROIF")
            .WithFlag(5, out receiveFullInterruptFlag, FieldMode.WriteOneToClear | FieldMode.Read, name: "RFIF")
            .WithFlag(4, out receiveAlmostFullInterruptFlag, FieldMode.WriteOneToClear | FieldMode.Read, name: "RAFIF")
            .WithFlag(3, out transmitPrimaryInterruptFlag, FieldMode.WriteOneToClear | FieldMode.Read, name: "TPIF")
            .WithFlag(2, out transmitSecondaryInterruptFlag, FieldMode.WriteOneToClear | FieldMode.Read, name: "TSIF")
            .WithTaggedFlag("EIF", 1)
            .WithFlag(0, out transmitAbortInterruptFlag, FieldMode.WriteOneToClear | FieldMode.Read, name: "AIF")
            .WithWriteCallback((_, __) => UpdateInterrupts());

        Registers.ErrorInterrupt.Define(this)
            .WithTaggedFlag("BEIF", 0)
            .WithTaggedFlag("BEIE", 1)
            .WithTaggedFlag("ALIF", 2)
            .WithTaggedFlag("ALIE", 3)
            .WithTaggedFlag("EPIF", 4)
            .WithTaggedFlag("EPIE", 5)
            .WithTaggedFlag("EPASS", 6)
            .WithTaggedFlag("EWARN", 7);

        Registers.WarningLimits.Define(this, 0x1B)
            .WithValueField(0, 4, out errorWarningLimit, name: "EWL")
            .WithValueField(4, 4, out almostFullWarningLimit, name: "AFWL");

        Registers.SlowSpeedBitTiming1.Define(this, 0x03)
            .WithTag("S_SEG_1", 0, 8);

        Registers.SlowSpeedBitTiming2.Define(this, 0x02)
            .WithTag("S_SEG_2", 0, 7)
            .WithReservedBits(7, 1);

        Registers.SlowSpeedBitTiming3.Define(this, 0x02)
            .WithTag("S_SJW", 0, 7)
            .WithReservedBits(7, 1);

        Registers.SlowSpeedPrescaler.Define(this, 0x01)
            .WithTag("S_PRESC", 0, 8);

        Registers.FastSpeedBitTiming1.Define(this, 0x03)
            .WithTag("F_SEG_1", 0, 5)
            .WithReservedBits(5, 3);

        Registers.FastSpeedBitTiming2.Define(this, 0x02)
            .WithTag("F_SEG_2", 0, 4)
            .WithReservedBits(4, 4);

        Registers.FastSpeedBitTiming3.Define(this, 0x02)
            .WithTag("F_SJW", 0, 4)
            .WithReservedBits(4, 4);

        Registers.FastSpeedPrescaler.Define(this, 0x01)
            .WithTag("F_PRESC", 0, 8);

        Registers.ErrorArbitrationLossCapture.Define(this)
            .WithTag("ALC", 0, 5)
            .WithTag("KOER", 5, 3);

        Registers.TransmitterDelayCompensation.Define(this)
            .WithTag("SSPOFF", 0, 7)
            .WithTaggedFlag("TDCEN", 7);

        Registers.ReceiveErrorCount.Define(this)
            .WithTag("RECNT", 0, 8);

        Registers.TransmitErrorCount.Define(this)
            .WithTag("TECNT", 0, 8);

        Registers.AcceptanceFilterControl.Define(this)
            .WithValueField(0, 4, out acceptanceFilterAddress, name: "ACFADR")
            .WithReservedBits(4, 1)
            .WithFlag(5, out acceptanceFilterMaskSelected, name: "SELMASK")
            .WithReservedBits(6, 2);

        var timestampingPreviouslyEnabled = false;
        Registers.CiA603TimeStampConfig.Define(this, 0x02)
            .WithFlag(0, out ciaTimeStampEnable, name: "TIMEEN", writeCallback: (previous, _) => timestampingPreviouslyEnabled = previous)
            // Use TIMEEN before this write to allow changing TIMEPOS in the same access that enables time-stamping.
            .WithConditionallyWritableFlag(1, out _, () => !timestampingPreviouslyEnabled, name: "TIMEPOS")
            .WithReservedBits(2, 6);

        Registers.AcceptanceFilterEnable0.Define(this, 0x01)
            .WithValueField(0, 8, out acceptanceFilterEnable0, name: "AE_X[7:0]");

        Registers.AcceptanceFilterEnable1.Define(this)
            .WithValueField(0, 8, out acceptanceFilterEnable1, name: "AE_X[15:8]");

        // ACFADR and SELMASK select one of three filters and its code or mask view.
        Registers.AcceptanceFilterCodeMask.DefineMany(this, sizeof(uint), setup: (register, i) =>
        {
            if(i == 3)
            {
                register.WithValueField(0, 7, writeCallback: (_, value) => WriteAcceptanceFilterByte(i, (byte)value),
                        valueProviderCallback: _ => ReadAcceptanceFilterByte(i), name: "ACODE_X/AMASK_X[30:24]")
                    .WithReservedBits(7, 1);
            }
            else
            {
                register.WithValueField(0, 8, writeCallback: (_, value) => WriteAcceptanceFilterByte(i, (byte)value),
                    valueProviderCallback: _ => ReadAcceptanceFilterByte(i), name: $"ACODE_X/AMASK_X[{i * 8 + 7}:{i * 8}]");
            }
        });

        Registers.Version0.Define(this, VersionMinor)
            .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => VersionMinor, name: "VER_0");

        Registers.Version1.Define(this, VersionMajor)
            .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => VersionMajor, name: "VER_1");

        Registers.MemoryProtection.Define(this)
            .WithConditionallyWritableFlag(0, out _, () => softwareReset.Value, name: "MPEN")
            .WithTaggedFlag("MDWIE", 1)
            .WithTaggedFlag("MDWIF", 2)
            .WithTaggedFlag("MDEIF", 3)
            .WithTaggedFlag("MAEIF", 4)
            .WithReservedBits(5, 3);

        Registers.MemoryStatus.Define(this)
            .WithTaggedFlag("ACFA", 0)
            .WithTaggedFlag("TXS", 1)
            .WithTaggedFlag("TXB", 2)
            .WithTag("HELOC", 3, 2)
            .WithReservedBits(5, 3);

        Registers.MemoryErrorStimulation0.Define(this)
            .WithTag("MEBP1", 0, 6)
            .WithTaggedFlag("ME1EE", 6)
            .WithTaggedFlag("MEAEE", 7);

        Registers.MemoryErrorStimulation1.Define(this)
            .WithTag("MEBP2", 0, 6)
            .WithTaggedFlag("ME2EE", 6)
            .WithReservedBits(7, 1);

        Registers.MemoryErrorStimulation2.Define(this)
            .WithTag("MEEEC", 0, 4)
            .WithTag("MENEC", 4, 4);

        Registers.MemoryErrorStimulation3.Define(this)
            .WithTag("MEL", 0, 2)
            .WithTaggedFlag("MES", 2)
            .WithReservedBits(3, 5);

        Registers.SpatialRedundancyConfiguration.Define(this, 0x01)
            .WithConditionallyWritableFlag(0, out _, () => softwareReset.Value, name: "SREN")
            .WithConditionallyWritableFlag(1, out _, () => softwareReset.Value, name: "SRISEL")
            .WithTaggedFlag("SREIF", 2)
            .WithTaggedFlag("SREEH", 3)
            .WithTaggedFlag("SREEC", 4)
            .WithReservedBits(5, 3);

        CounterRegisters.Control.Define(counterRegisters)
            .WithFlag(0, FieldMode.Write, name: "CNTR_START", writeCallback: (_, value) =>
            {
                if(value)
                {
                    ciA603Counter.Enabled = true;
                }
            })
            .WithFlag(1, FieldMode.Write, name: "CNTR_STOP", writeCallback: (_, value) =>
            {
                if(value)
                {
                    ciA603Counter.Enabled = false;
                }
            })
            .WithFlag(2, FieldMode.Write, name: "CNTR_CLEAR", writeCallback: (_, value) =>
            {
                if(value)
                {
                    ciA603Counter.Value = 0;
                }
            })
            .WithReservedBits(3, 29);

        CounterRegisters.Low.Define(counterRegisters)
            .WithValueField(0, 32, valueProviderCallback: _ => (uint)CiA603Counter,
                writeCallback: (_, value) => ciA603Counter.Value = BitHelper.ReplaceBits(CiA603Counter, value, width: 32), name: "CNTR_LO");

        CounterRegisters.High.Define(counterRegisters)
            .WithValueField(0, 32, valueProviderCallback: _ => CiA603Counter >> 32,
                writeCallback: (_, value) => ciA603Counter.Value = BitHelper.ReplaceBits(CiA603Counter, value, width: 32, destinationPosition: 32), name: "CNTR_HI");
    }

    private static int GetDataLengthCode(int payloadLength, bool fdFormat)
    {
        if(!fdFormat && payloadLength > MaxClassicalDataLengthCode)
        {
            // DLC values above 0x8 code 8 payload bytes in CAN 2.0 frames so the original DLC is not recoverable.
            return MaxClassicalDataLengthCode;
        }
        return CANDataLengthCode.TryFromPayloadLength(payloadLength, out var dataLengthCode)
            ? dataLengthCode : MaxClassicalDataLengthCode;
    }

    private static int GetPayloadLength(int dataLengthCode, bool fdFormat)
    {
        if(!fdFormat && dataLengthCode > MaxClassicalDataLengthCode)
        {
            // DLC values above 0x8 code 8 payload bytes in CAN 2.0 frames.
            return MaxClassicalPayloadLength;
        }
        return CANDataLengthCode.TryToPayloadLength(dataLengthCode, out var payloadLength)
            ? payloadLength : 0;
    }

    private static bool IsValidPayloadLength(int payloadLength, bool fdFormat)
    {
        // Classic CAN only allows 0-8 bytes; CAN FD additionally allows 12, 16, 20, 24, 32, 48 and 64.
        return fdFormat
            ? CANDataLengthCode.TryFromPayloadLength(payloadLength, out _)
            : payloadLength <= MaxClassicalPayloadLength;
    }

    private static uint GetFilterWord(byte[,] storage, int filter)
    {
        var bytes = Enumerable.Range(0, sizeof(uint)).Select(i => storage[filter, i]).ToArray();
        return BitHelper.ToUInt32(bytes, 0, sizeof(uint), reverse: true);
    }

    private static uint GetArbitrationPriority(byte[] slot)
    {
        var id = GetSlotIdentifier(slot);
        var control = slot[ControlByteOffset];
        var remote = (control & RemoteFrameMask) != 0 && (control & FdFormatMask) == 0;
        var priority = 0u;
        if((control & ExtendedFormatMask) != 0)
        {
            // ID[28:18], SRR=1, IDE=1, ID[17:0], RTR.
            BitHelper.ReplaceBits(ref priority, id, width: 11, destinationPosition: 21, sourcePosition: 18);
            priority |= (1u << 20) | (1u << 19);
            BitHelper.ReplaceBits(ref priority, id, width: 18, destinationPosition: 1);
            priority |= remote ? 1u : 0u;
        }
        else
        {
            // ID[10:0], RTR, IDE=0. Standard frames win over extended frames with the same base ID.
            BitHelper.ReplaceBits(ref priority, id, width: 11, destinationPosition: 21);
            priority |= remote ? 1u << 20 : 0u;
        }
        return priority;
    }

    private static uint GetSlotIdentifier(byte[] slot)
    {
        var extendedFormat = (slot[ControlByteOffset] & ExtendedFormatMask) != 0;
        return BitHelper.ToUInt32(slot, 0, sizeof(uint), reverse: true)
            & (extendedFormat ? ExtendedIdentifierMask : StandardIdentifierMask);
    }

    private void ReleaseReceiveSlot()
    {
        if(receiveBuffer.Count > 0)
        {
            receiveBuffer.Dequeue();
            this.NoisyLog("Released the oldest receive buffer slot, {0} slot(s) now filled", receiveBuffer.Count);
        }
        else
        {
            this.NoisyLog("Ignored the receive buffer release as there is no stored message");
        }
        // ROV is cleared by setting RREL.
        receiveOverflow = false;
    }

    private byte ReadReceiveBufferByte(int offset)
    {
        // The RBUF registers always map the slot containing the oldest received message.
        return receiveBuffer.Count > 0 ? receiveBuffer.Peek()[offset] : (byte)0;
    }

    private byte[] EncodeFrame(CANMessageFrame message, bool transmitted)
    {
        var slot = new byte[ReceiveBufferSize];
        var identifier = message.Id & (message.ExtendedFormat ? ExtendedIdentifierMask : StandardIdentifierMask);
        BitHelper.GetBytesFromValue(slot, 0, identifier, sizeof(uint), reverse: true);
        // RTR is forced to 0x0 in CAN FD frames and BRS is forced to 0x0 in CAN 2.0 frames.
        slot[ControlByteOffset] = (byte)((message.ExtendedFormat ? ExtendedFormatMask : 0)
            | (message.RemoteFrame && !message.FDFormat ? RemoteFrameMask : 0)
            | (message.FDFormat ? FdFormatMask : 0)
            | (message.BitRateSwitch && message.FDFormat ? BitRateSwitchMask : 0)
            | (GetDataLengthCode(message.Data.Length, message.FDFormat) & DataLengthCodeMask));
        // The status byte holds the kind of the last error of the frame (KOER, bits 7-5)
        // and the TX bit (bit 4) marking frames received through loopback (section
        // 15.1.4.11.1). Only valid frames are stored, so KOER is always 0 (no error).
        slot[StatusByteOffset] = transmitted ? TransmitStatusMask : (byte)0;
        if(ciaTimeStampEnable.Value)
        {
            slot.SetBytesFromValue(CiA603Counter, ReceiveTimeStampOffset);
        }
        if(!message.RemoteFrame || message.FDFormat)
        {
            Array.Copy(message.Data, 0, slot, DataByteOffset, Math.Min(message.Data.Length, MaxPayloadLength));
        }
        return slot;
    }

    private void StoreFrame(CANMessageFrame message, bool transmitted)
    {
        if(!IsValidPayloadLength(message.Data.Length, message.FDFormat))
        {
            // Frame with unrepresentable length, drop it.
            this.WarningLog("Dropping a frame on id 0x{0:X} with an invalid payload length of {1} bytes", message.Id, message.Data.Length);
            return;
        }

        if(receiveBuffer.Count == ReceiveSlotCount)
        {
            receiveOverflow = true;
            if(receiveOverflowInterruptEnable.Value)
            {
                receiveOverflowInterruptFlag.Value = true;
            }
            if(receiveFullInterruptEnable.Value)
            {
                receiveFullInterruptFlag.Value = true;
            }
            if(receiveOverwriteMode.Value)
            {
                // ROM = 0x1: the newest message is not stored.
                this.WarningLog("Receive buffer full, discarding the newest frame on id 0x{0:X}", message.Id);
                return;
            }
            // ROM = 0x0: the oldest message is overwritten by the newest one.
            this.WarningLog("Receive buffer full, overwriting the oldest frame with the newest frame on id 0x{0:X}", message.Id);
            receiveBuffer.Dequeue();
        }
        receiveBuffer.Enqueue(EncodeFrame(message, transmitted));
        this.DebugLog("Stored frame on id 0x{0:X} in the receive buffer, {1} slot(s) now filled", message.Id, receiveBuffer.Count);
        if(receiveInterruptEnable.Value)
        {
            receiveInterruptFlag.Value = true;
        }
        if(receiveAlmostFullInterruptEnable.Value && receiveBuffer.Count == AlmostFullLimit)
        {
            receiveAlmostFullInterruptFlag.Value = true;
        }
        if(receiveFullInterruptEnable.Value && receiveBuffer.Count == ReceiveSlotCount)
        {
            receiveFullInterruptFlag.Value = true;
        }
    }

    private bool IsFilterEnabled(int filter)
    {
        return filter < 8
            ? BitHelper.IsBitSet(acceptanceFilterEnable0.Value, (byte)filter)
            : BitHelper.IsBitSet(acceptanceFilterEnable1.Value, (byte)(filter - 8));
    }

    private bool FilterFrame(CANMessageFrame message)
    {
        for(var filter = 0; filter < AcceptanceFilterCount; ++filter)
        {
            if(!IsFilterEnabled(filter))
            {
                continue;
            }
            // The AMASK_X/ACODE_X words share the following layout:
            //
            //      31 |    30     |    29    | 28 ............................ 0
            //         |   AIDEE   |   AIDE   | ID(28-0)
            //
            var mask = GetFilterWord(acceptanceFilterMask, filter);
            var code = GetFilterWord(acceptanceFilterCode, filter);
            // When AIDEE is set, the filter accepts only the frame type selected by AIDE,
            // otherwise both standard and extended frames are accepted.
            var checkFrameType = (mask & (1u << 30)) != 0;
            var acceptsExtended = (mask & (1u << 29)) != 0;
            if(checkFrameType && message.ExtendedFormat != acceptsExtended)
            {
                continue;
            }
            // AMASK_X bits set to 0x1 mean the corresponding identifier bit is don't care.
            // Standard frames only have ID(10-0), so the remaining identifier bits are always accepted.
            var comparedBits = (message.ExtendedFormat ? ExtendedIdentifierMask : StandardIdentifierMask) & ~mask;
            // The message is accepted when all compared identifier bits match the code.
            if(((message.Id ^ code) & comparedBits) == 0)
            {
                return true;
            }
        }
        return false;
    }

    private void WriteAcceptanceFilterByte(int offset, byte value)
    {
        var filter = Math.Min((int)acceptanceFilterAddress.Value, AcceptanceFilterCount - 1);
        if(acceptanceFilterMaskSelected.Value)
        {
            acceptanceFilterMask[filter, offset] = value;
        }
        else
        {
            acceptanceFilterCode[filter, offset] = offset == 3 ? (byte)(value & 0x1F) : value;
        }
    }

    private byte ReadAcceptanceFilterByte(int offset)
    {
        var filter = Math.Min((int)acceptanceFilterAddress.Value, AcceptanceFilterCount - 1);
        var value = acceptanceFilterMaskSelected.Value ? acceptanceFilterMask[filter, offset] : acceptanceFilterCode[filter, offset];
        return offset == 3 && !acceptanceFilterMaskSelected.Value ? (byte)(value & 0x1F) : value;
    }

    private int SelectSecondarySlot()
    {
        if(transmitSecondaryOperationMode.Value == SecondaryOperationMode.FifoMode)
        {
            // FIFO mode: the oldest frame is transmitted first.
            return 0;
        }
        // Use bus arbitration order, including the standard/extended distinction and RTR (section 15.1.4.4.2.2).
        // Frames with equal arbitration fields keep their relative order.
        var selected = 0;
        var selectedPriority = GetArbitrationPriority(secondaryBufferQueue[0]);
        for(var i = 1; i < secondaryBufferQueue.Count; ++i)
        {
            var priority = GetArbitrationPriority(secondaryBufferQueue[i]);
            if(priority < selectedPriority)
            {
                selected = i;
                selectedPriority = priority;
            }
        }
        return selected;
    }

    private void AbortSecondaryTransmission()
    {
        if(secondaryRequest == SecondaryTransmissionRequest.None)
        {
            this.NoisyLog("Ignored the secondary abort as there is no requested transmission");
            return;
        }
        var all = secondaryRequest == SecondaryTransmissionRequest.AllFrames;
        secondaryRequest = SecondaryTransmissionRequest.None;
        var released = 0;
        if(all)
        {
            // A TSALL abort releases all message slots and additionally resets TSNEXT
            // (sections 15.1.4.7.3.1 and 15.1.4.7.3.2).
            released = secondaryBufferQueue.Count;
            secondaryBufferQueue.Clear();
            secondaryNextPending = false;
        }
        else if(secondaryBufferQueue.Count > 0)
        {
            secondaryBufferQueue.RemoveAt(SelectSecondarySlot());
            secondaryNextPending = false;
            released = 1;
        }
        if(released > 0)
        {
            // AIF is set after the requested message(s) have been aborted.
            transmitAbortInterruptFlag.Value = true;
            if(secondaryTransmissionHadFrame && transmitSecondaryInterruptEnable.Value)
            {
                transmitSecondaryInterruptFlag.Value = true;
            }
            UpdateInterrupts();
        }
        this.NoisyLog("Aborted the {0} transmission, {1} message slot(s) released", all ? "TSALL" : "TSONE", released);
    }

    private void CommitSecondarySlot()
    {
        var slot = new byte[TransmitBufferSize];
        Array.Copy(secondaryWorkingBuffer, slot, slot.Length);
        secondaryBufferQueue.Add(slot);
        this.NoisyLog("Marked the STB slot as filled, {0} slot(s) now filled", secondaryBufferQueue.Count);
    }

    private void FillSecondarySlot()
    {
        if(softwareReset.Value || transmitBufferSelect.Value != TransmitBufferSelect.SecondaryTransmitBuffer)
        {
            // TSNEXT is meaningless when the PTB is selected and it is ignored in that case.
            this.NoisyLog("Ignored TSNEXT as the PTB is selected");
            return;
        }
        if(secondaryBufferQueue.Count == SecondarySlotCount)
        {
            // TSNEXT already marked the last available slot filled, there is no extra pending frame.
            this.NoisyLog("Ignored TSNEXT while all STB slots are filled");
            return;
        }
        CommitSecondarySlot();
        secondaryNextPending = secondaryBufferQueue.Count == SecondarySlotCount;
    }

    private void WriteMultipleBytes(long offset, uint value, int count)
    {
        // Apply the whole bus access before executing commands, so TSNEXT and TSONE/TSALL can be set together.
        writingMultipleBytes = true;
        using var restore = DisposableWrapper.New(() => writingMultipleBytes = false);
        for(var i = 0; i < count; ++i)
        {
            WriteByte(offset + i, (byte)BitHelper.GetValue(value, 8 * i, 8));
        }
        ProcessTransmissions();
    }

    private void SoftwareReset()
    {
        // Preserve acceptance filters, timing, interrupt enables, TTS and CANFD_CNT (Table 15-9).
        transmitBufferSelect.Value = TransmitBufferSelect.PrimaryTransmitBuffer;
        primaryTransmissionRequested = false;
        primaryAbortRequested = false;
        secondaryAbortRequested = false;
        secondaryRequest = SecondaryTransmissionRequest.None;
        secondaryNextPending = false;
        secondaryTransmissionHadFrame = false;
        secondaryBufferQueue.Clear();
        receiveBuffer.Clear();
        receiveOverflow = false;
        receiveInterruptFlag.Value = false;
        receiveOverflowInterruptFlag.Value = false;
        receiveFullInterruptFlag.Value = false;
        receiveAlmostFullInterruptFlag.Value = false;
        transmitPrimaryInterruptFlag.Value = false;
        transmitSecondaryInterruptFlag.Value = false;
        transmitAbortInterruptFlag.Value = false;
        loopBackInternal.Value = false;
        loopBackExternal.Value = false;
        primarySingleShot.Value = false;
        secondarySingleShot.Value = false;
        receiveAll.Value = false;
        selfAcknowledge.Value = false;
        errorWarningLimit.Value = 0xB;
        UpdateInterrupts();
    }

    private void UpdateInterrupts()
    {
        var interrupt =
            (receiveInterruptEnable.Value && receiveInterruptFlag.Value)
            || (receiveOverflowInterruptEnable.Value && receiveOverflowInterruptFlag.Value)
            || (receiveFullInterruptEnable.Value && receiveFullInterruptFlag.Value)
            || (receiveAlmostFullInterruptEnable.Value && receiveAlmostFullInterruptFlag.Value)
            || (transmitPrimaryInterruptEnable.Value && transmitPrimaryInterruptFlag.Value)
            || (transmitSecondaryInterruptEnable.Value && transmitSecondaryInterruptFlag.Value)
            || transmitAbortInterruptFlag.Value;
        this.NoisyLog("Setting IRQ to {0}", interrupt);
        IRQ.Set(interrupt);
    }

    private CANMessageFrame DecodeFrame(byte[] slot)
    {
        var control = slot[ControlByteOffset];
        var fdFormat = (control & FdFormatMask) != 0;
        var extendedFormat = (control & ExtendedFormatMask) != 0;
        // RTR is forced to 0x0 in CAN FD frames and BRS is forced to 0x0 in CAN 2.0 frames.
        var remoteFrame = (control & RemoteFrameMask) != 0 && !fdFormat;
        var bitRateSwitch = (control & BitRateSwitchMask) != 0 && fdFormat;
        var identifier = GetSlotIdentifier(slot);
        // For remote frames, the DLC still contains the length of the expected response,
        // so size the array accordingly but leave it zero-filled.
        var payloadLength = GetPayloadLength(control & DataLengthCodeMask, fdFormat);
        var data = new byte[payloadLength];
        if(!remoteFrame)
        {
            Array.Copy(slot, DataByteOffset, data, 0, payloadLength);
        }
        return new CANMessageFrame(identifier, data, extendedFormat, remoteFrame, fdFormat, bitRateSwitch);
    }

    private void TransmitFrame(byte[] slot, string source)
    {
        var frame = DecodeFrame(slot);
        if(ciaTimeStampEnable.Value && (slot[3] & TransmitTimeStampEnableMask) != 0)
        {
            transmissionTimeStamp = (uint)CiA603Counter;
            this.DebugLog("Acquired TTS 0x{0:X8} for the frame transmitted from the {1}", transmissionTimeStamp, source);
        }

        if(loopBackInternal.Value)
        {
            // LBMI: the controller is disconnected from the bus, the transmitted frame is fed back internally with a self-ACK
            // (section 15.1.4.8.4).
            this.NoisyLog("Looping back {0} bytes [{1}] on id 0x{2:X} from the {3} internally", frame.Data.Length, frame.DataAsHex, frame.Id, source);
            ReceiveOwnFrame(frame);
            return;
        }

        TransmitOnBus(frame, source);

        if(loopBackExternal.Value)
        {
            // LBME: the transmitted frame is visible on the bus and is received back by
            // this node (section 15.1.4.8.4).
            ReceiveOwnFrame(frame);
        }
    }

    private void TransmitOnBus(CANMessageFrame frame, string source)
    {
        var fs = FrameSent;
        if(fs == null)
        {
            this.WarningLog("Tried to transmit {0} bytes [{1}] on id 0x{2:X} from the {3} while not connected to the medium",
                frame.Data.Length, frame.DataAsHex, frame.Id, source);
            return;
        }

        this.NoisyLog("Transmitting {0} bytes [{1}] on id 0x{2:X} from the {3}", frame.Data.Length, frame.DataAsHex, frame.Id, source);
        fs(frame);
    }

    private void ReceiveOwnFrame(CANMessageFrame frame)
    {
        // Own frames follow the normal reception path, including the acceptance filtering (section 15.1.4.8.4).
        if(!FilterFrame(frame))
        {
            this.DebugLog("Own frame on id 0x{0:X} rejected by the acceptance filters", frame.Id);
            return;
        }
        StoreFrame(frame, transmitted: true);
        UpdateInterrupts();
    }

    private void ProcessTransmissions()
    {
        while(primaryTransmissionRequested || secondaryRequest != SecondaryTransmissionRequest.None)
        {
            if(TransmissionBlocked)
            {
                primaryTransmissionRequested = false;
                secondaryRequest = SecondaryTransmissionRequest.None;
                break;
            }
            if(primaryAbortRequested)
            {
                if(primaryTransmissionRequested)
                {
                    primaryTransmissionRequested = false;
                    transmitAbortInterruptFlag.Value = true;
                }
                primaryAbortRequested = false;
            }
            if(secondaryAbortRequested)
            {
                AbortSecondaryTransmission();
                secondaryAbortRequested = false;
            }
            if(primaryTransmissionRequested)
            {
                TransmitFrame(transmitBuffer, "PTB");
                primaryTransmissionRequested = false;
                primaryAbortRequested = false;
                if(!softwareReset.Value && transmitPrimaryInterruptEnable.Value)
                {
                    transmitPrimaryInterruptFlag.Value = true;
                }
                continue;
            }
            if(secondaryRequest == SecondaryTransmissionRequest.None)
            {
                continue;
            }
            if(secondaryBufferQueue.Count == 0)
            {
                // Requests on an empty STB clear without transmitting or interrupting (section 15.1.4.7.3).
                secondaryRequest = SecondaryTransmissionRequest.None;
                continue;
            }
            var slot = secondaryBufferQueue[SelectSecondarySlot()];
            TransmitFrame(slot, "STB");
            secondaryBufferQueue.Remove(slot);
            secondaryNextPending = false;
            if(softwareReset.Value)
            {
                continue;
            }
            secondaryTransmissionHadFrame = true;
            // TSALL includes frames added during transmission and finishes when the STB becomes empty.
            if(secondaryRequest == SecondaryTransmissionRequest.SingleFrame || secondaryBufferQueue.Count == 0)
            {
                secondaryRequest = SecondaryTransmissionRequest.None;
                secondaryAbortRequested = false;
                if(transmitSecondaryInterruptEnable.Value)
                {
                    transmitSecondaryInterruptFlag.Value = true;
                }
            }
        }
        primaryAbortRequested = false;
        secondaryAbortRequested = false;
        UpdateInterrupts();
    }

    private bool CheckTransmissionAllowed(string command)
    {
        if(!TransmissionBlocked)
        {
            return true;
        }
        // No transmission can be started while the transceiver is in the standby mode or
        // when listen only mode is enabled without external loopback
        // (sections 15.1.4.8.2 and 15.1.4.8.5).
        this.WarningLog("Ignored {0} as transmissions are blocked by {1}", command,
            softwareReset.Value ? "software reset (RESET)" : transmitStandby.Value ? "transceiver standby (STBY)" : "listen only mode (LOM)");
        return false;
    }

    private bool CheckNoTransmissionRequested(string mode)
    {
        if(!primaryTransmissionRequested && secondaryRequest == SecondaryTransmissionRequest.None)
        {
            return true;
        }
        this.WarningLog("Ignored {0} as a transmission is requested", mode);
        return false;
    }

    private bool TransmissionBlocked => softwareReset.Value || transmitStandby.Value || (listenOnlyMode.Value && !loopBackExternal.Value);

    private int AlmostFullLimit => Math.Min(2 * Math.Max((int)almostFullWarningLimit.Value, 1), ReceiveSlotCount);

    private byte[] CurrentTransmitBuffer => transmitBufferSelect.Value == TransmitBufferSelect.PrimaryTransmitBuffer
        ? transmitBuffer
        : secondaryWorkingBuffer;

    private SecondaryBufferStatus CurrentSecondaryBufferStatus
    {
        get
        {
            if(secondaryBufferQueue.Count == 0)
            {
                return SecondaryBufferStatus.Empty;
            }
            if(secondaryBufferQueue.Count == SecondarySlotCount)
            {
                return SecondaryBufferStatus.Full;
            }
            return secondaryBufferQueue.Count > SecondarySlotCount / 2
                ? SecondaryBufferStatus.MoreThanHalfFull
                : SecondaryBufferStatus.LessThanOrEqualToHalfFull;
        }
    }

    private ulong CiA603Counter
    {
        get
        {
            if(sysbus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }
            // SOF and EOF have the same emulated timestamp as frames are delivered instantly.
            return ciA603Counter.Value;
        }
    }

    private ReceiveBufferStatus CurrentReceiveBufferStatus
    {
        get
        {
            if(receiveBuffer.Count == 0)
            {
                return ReceiveBufferStatus.Empty;
            }
            if(receiveBuffer.Count == ReceiveSlotCount)
            {
                return ReceiveBufferStatus.Full;
            }
            return receiveBuffer.Count >= AlmostFullLimit
                ? ReceiveBufferStatus.AlmostFull
                : ReceiveBufferStatus.Filled;
        }
    }

    private IFlagRegisterField receiveInterruptEnable;
    private IFlagRegisterField receiveInterruptFlag;
    private IFlagRegisterField receiveOverflowInterruptEnable;
    private IFlagRegisterField receiveOverflowInterruptFlag;
    private IFlagRegisterField receiveFullInterruptEnable;
    private IFlagRegisterField receiveFullInterruptFlag;
    private IFlagRegisterField receiveAlmostFullInterruptEnable;
    private IFlagRegisterField receiveAlmostFullInterruptFlag;
    private IFlagRegisterField receiveOverwriteMode;
    private IFlagRegisterField receiveAll;
    private IFlagRegisterField selfAcknowledge;
    private IFlagRegisterField softwareReset;
    private IFlagRegisterField primarySingleShot;
    private IFlagRegisterField secondarySingleShot;
    private IFlagRegisterField transmitPrimaryInterruptEnable;
    private IFlagRegisterField transmitSecondaryInterruptEnable;
    private IFlagRegisterField transmitPrimaryInterruptFlag;
    private IFlagRegisterField transmitSecondaryInterruptFlag;
    private IFlagRegisterField transmitAbortInterruptFlag;
    private IEnumRegisterField<TransmitBufferSelect> transmitBufferSelect;
    private IEnumRegisterField<SecondaryOperationMode> transmitSecondaryOperationMode;
    private IFlagRegisterField listenOnlyMode;
    private IFlagRegisterField transmitStandby;
    private IFlagRegisterField loopBackExternal;
    private IFlagRegisterField loopBackInternal;
    private IFlagRegisterField ciaTimeStampEnable;
    private IFlagRegisterField acceptanceFilterMaskSelected;
    private IValueRegisterField almostFullWarningLimit;
    private IValueRegisterField errorWarningLimit;
    private IValueRegisterField acceptanceFilterEnable0;
    private IValueRegisterField acceptanceFilterEnable1;
    private IValueRegisterField acceptanceFilterAddress;
    private bool receiveOverflow;
    private bool secondaryNextPending;
    private bool primaryTransmissionRequested;
    private bool primaryAbortRequested;
    private bool secondaryAbortRequested;
    private bool secondaryTransmissionHadFrame;
    private bool writingMultipleBytes;
    private SecondaryTransmissionRequest secondaryRequest;
    private uint transmissionTimeStamp;

    private readonly Queue<byte[]> receiveBuffer;
    private readonly byte[] transmitBuffer;
    private readonly byte[] secondaryWorkingBuffer;
    private readonly List<byte[]> secondaryBufferQueue = new List<byte[]>();
    private readonly LimitTimer ciA603Counter;
    private readonly DoubleWordRegisterCollection counterRegisters;
    private readonly BusAccess.ByteReadMethod readByteFromCounter;
    private readonly BusAccess.ByteWriteMethod writeByteToCounter;
    private readonly BusAccess.WordReadMethod readWordFromCounter;
    private readonly BusAccess.WordWriteMethod writeWordToCounter;
    private readonly byte[,] acceptanceFilterCode = new byte[AcceptanceFilterCount, sizeof(uint)];
    private readonly byte[,] acceptanceFilterMask = new byte[AcceptanceFilterCount, sizeof(uint)];

    private const int ReceiveBufferSize = 20 * sizeof(uint);
    private const int TransmitBufferSize = 18 * sizeof(uint);
    private const int ReceiveSlotCount = 16; // If this is changed then AlmostFullLimit must be adjusted.
    private const int SecondarySlotCount = 16;
    private const int AcceptanceFilterCount = 3;
    private const int ControlByteOffset = 4;
    private const int StatusByteOffset = 5;
    private const int DataByteOffset = 8;
    private const int ReceiveTimeStampOffset = 72;
    private const int MaxPayloadLength = 64;
    private const byte DataLengthCodeMask = 0xF;
    private const byte ExtendedFormatMask = 0x80;
    private const byte RemoteFrameMask = 0x40;
    private const byte FdFormatMask = 0x20;
    private const byte BitRateSwitchMask = 0x10;
    private const byte TransmitStatusMask = 0x10; // TX bit
    private const byte TransmitTimeStampEnableMask = 0x80;
    private const uint StandardIdentifierMask = 0x7FF;
    private const uint ExtendedIdentifierMask = 0x1FFFFFFF;
    private const int MaxClassicalDataLengthCode = 8;
    private const int MaxClassicalPayloadLength = 8;
    private const byte VersionMinor = 0x9;
    private const byte VersionMajor = 0x7;

    internal enum CounterRegisters
    {
        Control = 0x0,
        Low = 0x4,
        High = 0x8,
    }

    internal enum Registers
    {
        ReceiveBuffer = 0x000,                  // CANFD_RBUFn, n = 0 to 19
        TransmitBuffer = 0x050,                 // CANFD_TBUFn, n = 0 to 17
        TransmissionTimeStamp = 0x098,          // CANFD_TTS, 32-bit
        ConfigurationAndStatus = 0x0A0,         // CANFD_CFG_STAT
        TransmitCommand = 0x0A1,                // CANFD_TCMD
        TransmitControl = 0x0A2,                // CANFD_TCTRL
        ReceiveControl = 0x0A3,                 // CANFD_RCTRL
        ReceiveTransmitInterruptEnable = 0x0A4, // CANFD_RTIE
        ReceiveTransmitInterruptFlag = 0x0A5,   // CANFD_RTIF
        ErrorInterrupt = 0x0A6,                 // CANFD_ERRINT
        WarningLimits = 0x0A7,                  // CANFD_LIMIT
        SlowSpeedBitTiming1 = 0x0A8,            // CANFD_S_SEG_1
        SlowSpeedBitTiming2 = 0x0A9,            // CANFD_S_SEG_2
        SlowSpeedBitTiming3 = 0x0AA,            // CANFD_S_SJW, synchronisation jump width
        SlowSpeedPrescaler = 0x0AB,             // CANFD_S_PRESC
        FastSpeedBitTiming1 = 0x0AC,            // CANFD_F_SEG_1
        FastSpeedBitTiming2 = 0x0AD,            // CANFD_F_SEG_2
        FastSpeedBitTiming3 = 0x0AE,            // CANFD_F_SJW
        FastSpeedPrescaler = 0x0AF,             // CANFD_F_PRESC
        ErrorArbitrationLossCapture = 0x0B0,    // CANFD_EALCAP
        TransmitterDelayCompensation = 0x0B1,   // CANFD_TDC
        ReceiveErrorCount = 0x0B2,              // CANFD_RECNT
        TransmitErrorCount = 0x0B3,             // CANFD_TECNT
        AcceptanceFilterControl = 0x0B4,        // CANFD_ACFCTRL
        CiA603TimeStampConfig = 0x0B5,          // CANFD_TIMECFG
        AcceptanceFilterEnable0 = 0x0B6,        // CANFD_ACF_EN_0
        AcceptanceFilterEnable1 = 0x0B7,        // CANFD_ACF_EN_1
        AcceptanceFilterCodeMask = 0x0B8,       // CANFD_ACF_0_3_CODE / CANFD_ACF_0_3_MASK, 32-bit
        Version0 = 0x0BC,                       // CANFD_VER_0
        Version1 = 0x0BD,                       // CANFD_VER_1
        MemoryProtection = 0x0CA,               // CANFD_MEM_PROT
        MemoryStatus = 0x0CB,                   // CANFD_MEM_STAT
        MemoryErrorStimulation0 = 0x0CC,        // CANFD_MEM_ES_0
        MemoryErrorStimulation1 = 0x0CD,        // CANFD_MEM_ES_1
        MemoryErrorStimulation2 = 0x0CE,        // CANFD_MEM_ES_2
        MemoryErrorStimulation3 = 0x0CF,        // CANFD_MEM_ES_3
        SpatialRedundancyConfiguration = 0x0D0, // CANFD_SRCFG
    }

    private enum TransmitBufferSelect
    {
        PrimaryTransmitBuffer = 0x0,
        SecondaryTransmitBuffer = 0x1,
    }

    private enum SecondaryTransmissionRequest
    {
        None,
        SingleFrame,
        AllFrames,
    }

    private enum SecondaryOperationMode
    {
        FifoMode = 0x0,
        PriorityMode = 0x1,
    }

    private enum SecondaryBufferStatus
    {
        Empty = 0x0,
        LessThanOrEqualToHalfFull = 0x1,
        MoreThanHalfFull = 0x2,
        Full = 0x3,
    }

    private enum ReceiveBufferStatus
    {
        Empty = 0x0,
        Filled = 0x1, // More than empty and less than almost full
        AlmostFull = 0x2,
        Full = 0x3,
    }
}
