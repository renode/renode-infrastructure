//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Peripherals.CAN
{
    // Implements PeliCAN Mode of Operation
    public class UT32_CAN : BasicBytePeripheral, IKnownSize, ICAN
    {
        public UT32_CAN(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            receiveFifo.Clear();
            fifoUsedBytes = 0;
            overrunPending = false;
            resetMode = true;
            UpdateInterrupt();
        }

        public void OnFrameReceived(CANMessageFrame message)
        {
            var accept = FilterFrame(message);
            this.DebugLog("Received frame: {0}, filter {1}ed", message, accept ? "accept" : "reject");
            if(!accept)
            {
                return;
            }

            var thisMessageBytes = MessageFifoByteCount(message);
            if(fifoUsedBytes + thisMessageBytes <= FifoCapacity)
            {
                receiveFifo.Enqueue(message);
                fifoUsedBytes += thisMessageBytes;
            }
            else
            {
                // Functional difference from SJA1000: overrun IRQ and status not set until FIFO is read out
                overrunPending = true;
            }
            UpdateInterrupt();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        public event Action<CANMessageFrame> FrameSent;

        protected override void DefineRegisters()
        {
            Registers.Mode.Define(this, 1)
                .WithConditionallyWritableFlag(1, out listenOnlyMode, () => resetMode, this, name: "LOM")
                .WithConditionallyWritableFlag(2, out selfTestMode, () => resetMode, this, name: "STM")
                .WithConditionallyWritableFlag(3, out singleAcceptanceFilter, () => resetMode, this, name: "AFM")
                .WithTaggedFlag("SM", 4) // Sleep mode, SJA1000 only
                // Keep last to delay its action by relying on callback ordering
                .WithFlag(0, changeCallback: (_, value) => resetMode = value, valueProviderCallback: _ => resetMode, name: "RM")
                .WithReservedBits(5, 3);

            Registers.Command.Define(this)
                .WithFlag(0, FieldMode.Write, name: "TR", writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        SendFrame(TxFrame);
                    }
                })
                .WithTaggedFlag("AT", 1)
                .WithFlag(2, FieldMode.Write, name: "RRB", writeCallback: (_, value) => // Release Receive Buffer
                {
                    if(value)
                    {
                        if(receiveFifo.TryDequeue(out var message))
                        {
                            fifoUsedBytes -= MessageFifoByteCount(message);
                        }
                        dataOverrunStatus.Value = overrunPending;
                        overrunPending = false;
                        dataOverrunFlag.Value |= dataOverrunStatus.Value && dataOverrunInterruptEnable.Value;
                        UpdateInterrupt();
                    }
                })
                .WithFlag(3, FieldMode.Write, name: "CDO", writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        dataOverrunStatus.Value = false;
                        UpdateInterrupt();
                    }
                })
                .WithFlag(4, FieldMode.Write, name: "SRR", writeCallback: (_, value) =>
                {
                    if(value && selfTestMode.Value)
                    {
                        var frame = TxFrame;
                        SendFrame(frame);
                        OnFrameReceived(frame); // Self-reception
                    }
                })
                .WithReservedBits(5, 2);

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => receiveFifo.Any(), name: "RBS") // Message available for reading
                .WithFlag(1, out dataOverrunStatus, FieldMode.Read, name: "DOS")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => true, name: "TBS") // Transmit buffer available for writing
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => true, name: "TCS") // Last transmission completed successfully
                .WithTaggedFlag("RS", 4)
                .WithTaggedFlag("TS", 5)
                .WithTaggedFlag("ES", 6)
                .WithTaggedFlag("BS", 7);

            Registers.Interrupt.Define(this)
                .WithFlag(0, out receiveCompleteFlag, FieldMode.Read, name: "RI") // The only non-ReadToClear bit
                .WithFlag(1, out transmitCompleteFlag, FieldMode.ReadToClear, name: "TI")
                .WithTaggedFlag("EI", 2)
                .WithFlag(3, out dataOverrunFlag, FieldMode.ReadToClear, name: "DOI")
                .WithTaggedFlag("WUI", 4) // Sleep mode wakeup, SJA1000 only
                .WithTaggedFlag("EPI", 5)
                .WithTaggedFlag("ALI", 6)
                .WithTaggedFlag("BEI", 7)
                .WithReadCallback((_, __) => UpdateInterrupt()); // Read, not change, because of the RI bit

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out receiveCompleteInterruptEnable, name: "RIE")
                .WithFlag(1, out transmitCompleteInterruptEnable, name: "TIE")
                .WithTaggedFlag("EIE", 2)
                .WithFlag(3, out dataOverrunInterruptEnable, name: "DOIE")
                .WithTaggedFlag("WUIE", 4) // Sleep mode wakeup, SJA1000 only
                .WithTaggedFlag("EPIE", 5)
                .WithTaggedFlag("ALIE", 6)
                .WithTaggedFlag("BEIE", 7)
                .WithChangeCallback((_, __) => UpdateInterrupt());

            Registers.BusTiming0.Define(this)
                .WithTag("BRP", 0, 6)
                .WithTag("SJW", 6, 2);

            Registers.BusTiming1.Define(this)
                .WithTag("TSEG1", 0, 4)
                .WithTag("TSEG2", 4, 3)
                .WithTaggedFlag("SAM", 7);

            Registers.ArbLostCapture.Define(this)
                .WithTag("BITNO", 0, 5)
                .WithReservedBits(5, 3);

            Registers.ErrorCodeCapture.Define(this)
                .WithTag("SEG", 0, 5)
                .WithTaggedFlag("DIR", 5)
                .WithTag("ERRC", 6, 2);

            Registers.ErrorWarningLimit.Define(this, 96)
                .WithTag("ERROR_WARNING_LIMIT", 0, 8); // Writable in reset mode only

            Registers.RxErrorCounter.Define(this)
                .WithTag("RX_ERROR_COUNTER", 0, 8); // Writable in reset mode only

            Registers.TxErrorCounter.Define(this)
                .WithTag("TX_ERROR_COUNTER", 0, 8); // Writable in reset mode only

            // Acceptance mask registers are only available in reset mode
            ResetModeRegisters.AcceptanceCode.DefineManyConditional(this, AcceptanceCodeLength, () => resetMode, (reg, index) => reg
                .WithValueField(0, 8, out acceptanceCode[index], name: $"ACR{index}"), resetValue: 0x51); // The reset value for ACR0 is 0x51, the rest are undefined

            ResetModeRegisters.AcceptanceMask.DefineManyConditional(this, AcceptanceCodeLength, () => resetMode, (reg, index) => reg
                .WithValueField(0, 8, out acceptanceMask[index], name: $"AMR{index}"), resetValue: 0xff); // Same as above, the reset value for AMR0 is 0xff

            // Frame info is only available in normal mode
            Registers.FrameInfo.DefineConditional(this, () => !resetMode)
                .WithValueField(0, 4, out dataLengthCode, name: "DLC",
                    valueProviderCallback: _ => (ulong)(RxFrame?.Data.Length ?? 0))
                .WithReservedBits(4, 2)
                .WithFlag(6, out remoteTransmissionRequest, name: "RTR",
                    valueProviderCallback: _ => RxFrame?.RemoteFrame ?? false)
                .WithFlag(7, out extendedFrameFormat, name: "FF", valueProviderCallback: _ =>
                    {
                        // If we're reading an EFF frame, adjust the register layout to match
                        var receivingEff = RxFrame?.ExtendedFormat ?? false;
                        extendedFrameFormat.Value = receivingEff;
                        return receivingEff;
                    });

            // Normal mode, ID1..2 are common for SFF and EFF but interpreted differently. The ID is left-aligned
            // and the top 8 bits are always in ID1
            Registers.Id1.DefineManyConditional(this, SffIdLength, () => !resetMode, (reg, index) => reg
                .WithValueField(0, 8, out id[index], name: $"ID{index + 1}",
                    valueProviderCallback: _ => GetRxIdByte(RxFrame, index, extendedFormat: extendedFrameFormat.Value)));

            // Normal mode, extended format extra ID bytes
            ExtendedFrameFormatRegisters.Id3.DefineManyConditional(this, EffIdExtraLength, () => !resetMode && extendedFrameFormat.Value, (reg, index) => reg
                .WithValueField(0, 8, out id[index + SffIdLength], name: $"ID{index + SffIdLength + 1}",
                    valueProviderCallback: _ => GetRxIdByte(RxFrame, index + SffIdLength, extendedFormat: true)));

            // Normal mode, standard format data bytes
            StandardFrameFormatRegisters.Data.DefineManyConditional(this, MaxDataLength, () => !resetMode && !extendedFrameFormat.Value, (reg, index) => reg
                .WithValueField(0, 8, out data[index], name: $"DATA{index}", valueProviderCallback: _ => RxFrame?.Data[index] ?? 0));

            // Normal mode, extended format data bytes
            // In the register object model, a field can be a part of only one register, but these are the same
            // data fields as in the standard frame format. We use the standard fields as a backing and mirror
            // values written here to them in the write callbacks.
            ExtendedFrameFormatRegisters.Data.DefineManyConditional(this, MaxDataLength, () => !resetMode && extendedFrameFormat.Value, (reg, index) => reg
                .WithValueField(0, 8, name: $"DATA{index}",
                    valueProviderCallback: _ => RxFrame?.Data[index] ?? 0,
                    writeCallback: (_, value) => data[index].Value = value));

            // Normal mode, standard format next frame info
            StandardFrameFormatRegisters.NextFrameInfo.DefineConditional(this, () => !resetMode && !extendedFrameFormat.Value)
                .WithValueField(0, 4, FieldMode.Read, name: "DLC", valueProviderCallback: _ => (ulong)(receiveFifo.ElementAtOrDefault(1)?.Data.Length ?? 0))
                .WithReservedBits(4, 2)
                .WithFlag(6, FieldMode.Read, name: "RTR", valueProviderCallback: _ => receiveFifo.ElementAtOrDefault(1)?.RemoteFrame ?? false)
                .WithFlag(7, FieldMode.Read, name: "FF", valueProviderCallback: _ => extendedFrameFormat.Value);

            // Normal mode, standard format next frame ID1
            StandardFrameFormatRegisters.NextId1.DefineConditional(this, () => !resetMode && !extendedFrameFormat.Value)
                .WithValueField(0, 8, FieldMode.Read, name: "ID1",
                    valueProviderCallback: _ => GetRxIdByte(receiveFifo.ElementAtOrDefault(1), 0, extendedFormat: false));

            Registers.RxMessageCounter.Define(this)
                .WithValueField(0, 5, FieldMode.Read, name: "NM", valueProviderCallback: _ => (ulong)receiveFifo.Count)
                .WithReservedBits(5, 3);

            Registers.ClockDivider.Define(this)
                .WithReservedBits(0, 7)
                .WithFlag(7, FieldMode.Read, name: "PeliCAN", valueProviderCallback: _ => true);
        }

        private void UpdateInterrupt()
        {
            var state = false;

            // The receive complete interrupt flag is not cleared by reading the interrupt register,
            // so make make the interrupt corresponding to this flag edge-triggered
            var newReceiveComplete = receiveCompleteInterruptEnable.Value && receiveFifo.Any();
            state |= newReceiveComplete && !receiveCompleteFlag.Value;
            receiveCompleteFlag.Value = newReceiveComplete;

            // These interrupt flags only get set if the interrupt is enabled when their trigger
            // condition happens, so or them in directly now
            state |= transmitCompleteFlag.Value || dataOverrunFlag.Value;
            this.NoisyLog("Setting IRQ to {0}", state);
            IRQ.Set(state);
        }

        private void SendFrame(CANMessageFrame frame)
        {
            if(listenOnlyMode.Value)
            {
                this.WarningLog("Attempt to transmit frame when in listen-only mode");
                return;
            }
            var fs = FrameSent;
            if(fs == null)
            {
                this.ErrorLog("Attempt to transmit frame when not connected to medium");
                return;
            }
            this.DebugLog("Sending frame: {0}", frame);
            fs(frame);

            transmitCompleteFlag.Value |= transmitCompleteInterruptEnable.Value;
            UpdateInterrupt();
        }

        // Convert the ID of a CAN message to values for use in the ID1..2 (or 1..4) registers according to the specified format
        private static byte GetRxIdByte(CANMessageFrame frame, int pos, bool extendedFormat)
        {
            if(frame == null)
            {
                return 0;
            }

            if(extendedFormat)
            {
                switch(pos)
                {
                    case 0:
                        return (byte)(frame.Id >> 21);
                    case 1:
                        return (byte)(frame.Id >> 13);
                    case 2:
                        return (byte)(frame.Id >> 5);
                    case 3:
                        return (byte)((frame.Id << 3) | (frame.RemoteFrame ? 1u << 2 : 0));
                }
            }

            switch(pos)
            {
                case 0:
                    return (byte)(frame.Id >> 3);
                case 1:
                    return (byte)((frame.Id << 5) | (frame.RemoteFrame ? 1u << 4 : 0));
            }

            return 0;
        }

        private static int MessageFifoByteCount(CANMessageFrame message)
        {
            // Frame information always takes 1 byte; ID takes 4 bytes in EFF, 2 bytes in SFF
            int overhead = 1 + (message.ExtendedFormat ? EffIdLength : SffIdLength);
            return message.Data.Length + overhead;
        }

        private bool FilterFrame(CANMessageFrame message)
        {
            // The ACR values are masked by AMR - 1 in AMR means the ACR bit is a don't care
            var code = acceptanceCode.Select(r => (byte)r.Value).ToArray();
            var mask = acceptanceMask.Select(r => (byte)r.Value).ToArray();
            // For acceptance filtering, use the EFF flag in the incoming message, not our frame info register
            var input = Enumerable.Range(0, AcceptanceCodeLength).Select(i => GetRxIdByte(message, i, message.ExtendedFormat)).ToArray();
            if(singleAcceptanceFilter.Value)
            {
                if(message.ExtendedFormat)
                {
                    // ACR0              ACR1              ACR2              ACR3
                    // 7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0

                    // I I I I I I I I   I I I I I I I I   I I I I I I I I   I I I I I R
                    // D D D D D D D D   D D D D D D D D   D D D D D D D D   D D D D D T
                    // 2 2 2 2 2 2 2 2   2 1 1 1 1 1 1 1   1 1 1 9 8 7 6 5   4 3 2 1 0 R
                    // 8 7 6 5 4 3 2 1   0 9 8 7 6 5 4 3   2 1 0
                    mask[3] |= 0b11; // Mask out unused bits
                }
                else
                {
                    // ACR0              ACR1              ACR2              ACR3
                    // 7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0

                    // I I I I I I I I   I I I R           B B B B B B B B   B B B B B B B B
                    // D D D D D D D D   D D D T           1 1 1 1 1 1 1 1   2 2 2 2 2 2 2 2
                    // 2 2 2 2 2 2 2 2   2 1 1 R           . . . . . . . .   . . . . . . . .
                    // 8 7 6 5 4 3 2 1   0 9 8             7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0
                    mask[1] |= 0b1111; // Mask out unused bits
                    input[2] = message.Data.ElementAtOrDefault(0); // Compare the first 2 bytes of the frame data
                    input[3] = message.Data.ElementAtOrDefault(1);
                }
                var matches = ~(BitConverter.ToUInt32(input, 0) ^ BitConverter.ToUInt32(code, 0));
                var ignores = BitConverter.ToUInt32(mask, 0);
                return (matches | ignores) == uint.MaxValue;
            }
            else
            {
                if(message.ExtendedFormat)
                {
                    // ACR0              ACR1              ACR2              ACR3
                    // 7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0
                    //
                    // I I I I I I I I   I I I I I I I I   I I I I I I I I   I I I I I I I I
                    // D D D D D D D D   D D D D D D D D   D D D D D D D D   D D D D D D D D
                    // 2 2 2 2 2 2 2 2   2 1 1 1 1 1 1 1   2 2 2 2 2 2 2 2   2 1 1 1 1 1 1 1
                    // 8 7 6 5 4 3 2 1   0 9 8 7 6 5 4 3   8 7 6 5 4 3 2 1   0 9 8 7 6 5 4 3
                    // [ (filter #1) ]   [ (filter #1) ]   [ (filter #2) ]   [ (filter #2) ]
                    input[2] = input[0]; // Copy the input for the second filter to use
                    input[3] = input[1];
                    var matches = ~(BitConverter.ToUInt32(input, 0) ^ BitConverter.ToUInt32(code, 0));
                    var ignores = BitConverter.ToUInt32(mask, 0);
                    var halves = matches | ignores; // Calculate both halves of the filter packed
                    // The frame is accepted when filter #1 or filter #2 fully matches
                    return (ushort)(halves >> 16) == ushort.MaxValue || (ushort)halves == ushort.MaxValue;
                }
                else
                {
                    // ACR0              ACR1              ACR2              ACR3
                    // 7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0   7 6 5 4 3 2 1 0
                    //
                    // I I I I I I I I   I I I R B B B B   I I I I I I I I   I I I R B B B B
                    // D D D D D D D D   D D D T 1 1 1 1   D D D D D D D D   D D D T 1 1 1 1
                    // 2 2 2 2 2 2 2 2   2 1 1 R . . . .   2 2 2 2 2 2 2 2   2 1 1 R . . . .
                    // 8 7 6 5 4 3 2 1   0 9 8   7 6 5 4   8 7 6 5 4 3 2 1   0 9 8   3 2 1 0
                    // [ (filter #1) ]   [ (filter #1) ]   [ (filter #2) ]   [(#2) ] [(#1) ]
                    var firstByte = message.Data.ElementAtOrDefault(0);
                    input[1] = input[1].ReplaceBits(firstByte, 4, sourcePosition: 4);
                    input[3] = input[3].ReplaceBits(firstByte, 4);
                    var matches = ~(BitConverter.ToUInt32(input, 0) ^ BitConverter.ToUInt32(code, 0));
                    var ignores = BitConverter.ToUInt32(mask, 0);
                    var result = BitConverter.GetBytes(matches | ignores);
                    var filter1 = result[0] == byte.MaxValue && result[1] == byte.MaxValue && (result[3] & 0xf) == 0xf;
                    var filter2 = result[2] == byte.MaxValue;
                    return filter1 || filter2;
                }
            }
        }

        // ID of the frame to be transmitted
        private uint TxId
        {
            get
            {
                if(extendedFrameFormat.Value)
                {
                    return (uint)(id[0].Value << 21) | (uint)(id[1].Value << 13) | (uint)(id[2].Value << 5) | (uint)(id[3].Value >> 3);
                }
                return (uint)(id[0].Value << 3) | (uint)(id[1].Value >> 5);
            }
        }

        // Data to be transmitted
        private byte[] TxData => data.Take((int)dataLengthCode.Value).Select(r => (byte)r.Value).ToArray();

        // The frame to be transmitted
        private CANMessageFrame TxFrame => new CANMessageFrame(TxId, TxData,
            extendedFormat: extendedFrameFormat.Value, remoteFrame: remoteTransmissionRequest.Value);

        private CANMessageFrame RxFrame => receiveFifo.FirstOrDefault();

        private IFlagRegisterField listenOnlyMode;
        private IFlagRegisterField selfTestMode;
        private IFlagRegisterField singleAcceptanceFilter;
        private IFlagRegisterField extendedFrameFormat;
        private IValueRegisterField dataLengthCode;
        private IValueRegisterField[] acceptanceCode = new IValueRegisterField[AcceptanceCodeLength];
        private IValueRegisterField[] acceptanceMask = new IValueRegisterField[AcceptanceCodeLength];
        private IValueRegisterField[] id = new IValueRegisterField[EffIdLength];
        private IValueRegisterField[] data = new IValueRegisterField[MaxDataLength];
        private IFlagRegisterField remoteTransmissionRequest;
        private IFlagRegisterField dataOverrunStatus;
        private IFlagRegisterField receiveCompleteFlag;
        private IFlagRegisterField transmitCompleteFlag;
        private IFlagRegisterField dataOverrunFlag;
        private IFlagRegisterField receiveCompleteInterruptEnable;
        private IFlagRegisterField transmitCompleteInterruptEnable;
        private IFlagRegisterField dataOverrunInterruptEnable;

        private bool overrunPending;
        private bool resetMode;
        private int fifoUsedBytes;

        private readonly Queue<CANMessageFrame> receiveFifo = new Queue<CANMessageFrame>();

        private const int SffIdLength = 2;
        private const int EffIdExtraLength = 2;
        private const int EffIdLength = SffIdLength + EffIdExtraLength;
        private const int AcceptanceCodeLength = 4;
        private const int MaxDataLength = 8;
        // The number of messages that can be stored in the FIFO depends on the length of the individual messages
        private const int FifoCapacity = 64; // bytes

        private enum Registers : long
        {
            Mode = 0x00,
            Command = 0x01,
            Status = 0x02,
            Interrupt = 0x03,
            InterruptEnable = 0x04,
            // Gap
            BusTiming0 = 0x06,
            BusTiming1 = 0x07,
            // Gap
            ArbLostCapture = 0x0b,
            ErrorCodeCapture = 0x0c,
            ErrorWarningLimit = 0x0d,
            RxErrorCounter = 0x0e,
            TxErrorCounter = 0x0f,
            FrameInfo = 0x10,
            Id1 = 0x11,
            Id2 = 0x12,
            // 0x10..0x1c: SFF frame/EFF frame/Acceptance Code
            RxMessageCounter = 0x1d,
            // Gap
            ClockDivider = 0x1f,
        }

        private enum ResetModeRegisters : long
        {
            AcceptanceCode = 0x10, // 0x10..0x13
            AcceptanceMask = 0x14, // 0x14..0x17
        }

        private enum StandardFrameFormatRegisters : long
        {
            Data = 0x13, // 0x13..0x1a
            NextFrameInfo = 0x1b,
            NextId1 = 0x1c,
        }

        private enum ExtendedFrameFormatRegisters : long
        {
            Id3 = 0x13,
            Id4 = 0x14,
            Data = 0x15, // 0x15..0x1c
        }
    }

    internal static class PeripheralRegisterExtensions
    {
        public static T WithConditionallyWritableFlag<T>(this T register, int position, out IFlagRegisterField flagField, Func<bool> writabilityCondition, IPeripheral parent, Action<bool, bool> readCallback = null,
            Action<bool, bool> writeCallback = null, Action<bool, bool> changeCallback = null, Func<bool, bool> valueProviderCallback = null, bool softResettable = true, string name = null)
            where T : PeripheralRegister
        {
            IFlagRegisterField ff = null;
            ff = register.DefineFlagField(position, FieldMode.Read | FieldMode.Write, readCallback, writeCallback, changeCallback: (oldValue, value) =>
            {
                if(!writabilityCondition())
                {
                    parent.WarningLog("Flag {0} was changed while this was not allowed", name);
                    ff.Value = oldValue;
                }
                else
                {
                    changeCallback?.Invoke(oldValue, value);
                }
            }, valueProviderCallback, softResettable, name);
            flagField = ff;
            return register;
        }
    }
}
