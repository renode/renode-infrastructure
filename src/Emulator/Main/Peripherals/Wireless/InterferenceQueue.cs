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
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public static class InterferenceQueue
    {
        static readonly List<PacketInfo> overTheAirPackets = new List<PacketInfo>();

        static InterferenceQueue()
        {
            // This queue and its subscriber list are process-global, while the radios using them belong
            // to a single emulation. Without this, radios from a previous emulation would keep receiving
            // change notifications and would act on machine state that is gone.
            EmulationManager.Instance.EmulationChanged += Clear;
        }

        // Drops all over-the-air packets and subscribers. Called when the emulation is replaced.
        public static void Clear()
        {
            overTheAirPackets.Clear();
            InterferenceQueueChanged = null;
        }

        public static void Add(IRadio sender, PhySignature signature, int channel, int txPowerDbm, byte[] content)
        {
            // First we check if there is already a packet from this sender in the queue. If that is the case,
            // it should be stale and we are going to remove it.
            var entry = PacketLookup(sender);
            if(entry != null)
            {
                if(!entry.PacketIsStale)
                {
                    Logger.Log(LogLevel.Error, "InterferenceQueue.Add: sender non-stale entry present");
                    return;
                }
                // The previous transmission from this sender is retired (completed or aborted; its entry
                // lingered for late lookups). Drop it before adding the new one.
                overTheAirPackets.Remove(entry);
            }

            var addTime = IPeripheralExtensions.GetMachine(sender).LocalTimeSource.ElapsedVirtualTime;
            var newEntry = new PacketInfo(sender, signature, channel, txPowerDbm, content);
            newEntry.StartTxTimestamp = addTime;
            overTheAirPackets.Add(newEntry);
            Logger.Log(LogLevel.Noisy, "InterferenceQueue.Add at {0}: [{1}]", addTime, BitConverter.ToString(content));
            InterferenceQueueChanged?.Invoke();
        }

        // Retires the over-the-air entry for a sender, recording how the transmission ended.
        //
        // The entry is not dropped immediately in either case: sender and receiver do not advance in
        // lockstep, so a receiver may still be working on this transmission (or may not have been
        // notified of it yet) when the sender is already done with it. Keeping the retired entry lets
        // such a receiver still resolve the TX start time, the PHY signature, and — importantly — whether
        // this particular transmission completed or was aborted. A retired entry is excluded from
        // energy/overlap queries (GetCurrentRssi, AnyOtherOnAir) and is physically dropped when the
        // sender starts transmitting again (see Add).
        //
        // transmissionCompleted == false marks the entry as aborted, which is what lets a receiver
        // distinguish "the frame I am receiving was cut short" from "the sender has simply moved on".
        //
        // An entry that is already retired is physically dropped: it has already had its grace window.
        public static void Remove(IRadio sender, bool transmissionCompleted)
        {
            var entry = PacketLookup(sender);

            if(entry == null)
            {
                Logger.Log(LogLevel.Error, "InterferenceQueue.Remove: entry not found");
                return;
            }

            var now = IPeripheralExtensions.GetMachine(entry.Sender).LocalTimeSource.ElapsedVirtualTime;

            if(!entry.PacketIsStale)
            {
                Logger.Log(LogLevel.Noisy, "InterferenceQueue.Remove at {0}, OTA time={1}: [{2}] - marking it {3}",
                        now, now - entry.StartTxTimestamp, BitConverter.ToString(entry.PacketContent),
                        transmissionCompleted ? "stale" : "aborted");
                entry.PacketIsStale = true;
                entry.PacketWasAborted = !transmissionCompleted;
            }
            else
            {
                Logger.Log(LogLevel.Noisy, "InterferenceQueue.Remove at {0}, OTA time={1}: [{2}] - removed",
                        now, now - entry.StartTxTimestamp, BitConverter.ToString(entry.PacketContent));
                overTheAirPackets.Remove(entry);
            }

            InterferenceQueueChanged?.Invoke();
        }

        public static TimeInterval GetTxStartTime(IRadio sender)
        {
            // Here we want to look up also stale packets, since it is possible that the sender already "completed"
            // the transmission while the receiver hasn't been notified yet (possibly due to a combination of the packet
            // being short and the QuantumTime being long enough).
            var entry = PacketLookup(sender);

            // An aborted transmission never fully went on air, so there is no start time to hand out: a
            // receiver asking about it is trying to start a reception that must be dropped.
            if(entry == null || entry.PacketWasAborted)
            {
                return TimeInterval.Empty;
            }

            return entry.StartTxTimestamp;
        }

        // Returns the PHY signature of the (possibly stale) over-the-air packet from the given
        // sender. Used by receivers to decide whether an incoming frame is decode-compatible with
        // the local RX configuration.
        public static bool TryGetTxSignature(IRadio sender, out PhySignature signature)
        {
            var entry = PacketLookup(sender);
            if(entry == null)
            {
                signature = default(PhySignature);
                return false;
            }

            signature = entry.Signature;
            return true;
        }

        // Simple initial implementation: we return a "high" hardcoded RSSI value if there is at
        // least one packet going over the air on the same carrier frequency (channel).
        // Energy detection / RSSI is intentionally modulation-agnostic: a receiver senses RF power
        // in its band regardless of the transmitter's modulation, and could not decode it. Hence we
        // match on channel (carrier frequency) only here; modulation/coding/datarate/DSSS matching
        // belongs to the frame-decode path (see PhySignature.IsDecodeCompatibleWith).
        // TODO: Eventually we will want to:
        // - take into account the distance between sender and receiver
        // - the sender TX power
        // - occupied bandwidth / adjacent-channel overlap (currently only exact co-channel)
#pragma warning disable IDE0060
        public static int GetCurrentRssi(IRadio receiver, int channel)
#pragma warning restore IDE0060
        {
            if(ForceBusyRssi)
            {
                return RssiBusyChannelHardCodedValueDbm;
            }

            foreach(var entry in overTheAirPackets)
            {
                // Stale entries are completed transmissions kept only for late sender lookups; they no
                // longer contribute energy.
                if(!entry.PacketIsStale && entry.Channel == channel)
                {
                    return RssiBusyChannelHardCodedValueDbm;
                }
            }

            return RssiClearChannelHardCodedValueDbm;
        }

        // Returns true if there is at least one on-air packet on the given channel from a sender other
        // than the excluded one. Used by a receiver to detect co-channel interference that is already
        // present (overlapping in time) at the moment a reception begins. Energy-based, so it ignores
        // modulation: any co-channel transmission corrupts an in-progress reception.
        public static bool AnyOtherOnAir(IRadio excludedSender, int channel)
        {
            foreach(var entry in overTheAirPackets)
            {
                // Stale entries are completed transmissions kept only for late sender lookups; they are no
                // longer on air and cannot interfere.
                if(!entry.PacketIsStale && entry.Channel == channel && entry.Sender != excludedSender)
                {
                    return true;
                }
            }

            return false;
        }

        // True if the identified transmission (sender plus the TX start time the receiver locked onto)
        // was aborted rather than completed.
        //
        // The transmission is identified by its start time on purpose: the sender runs ahead of the
        // receiver in virtual time, so by the time a receiver asks, the sender may already have finished
        // this transmission and started — or aborted — a later one. Matching on the sender alone would
        // attribute that later outcome to the frame still being received here.
        public static bool WasTransmissionAborted(IRadio sender, TimeInterval txStartTime)
        {
            var entry = PacketLookup(sender);

            if(entry == null || entry.StartTxTimestamp != txStartTime)
            {
                return false;
            }

            return entry.PacketWasAborted;
        }

        // Snapshot of transmissions that are still on the air. Receivers use this to join a packet
        // that started before they were on-channel or in RxSearch. Stale entries are omitted.
        public static PacketInfo[] GetLivePackets()
        {
            var result = new List<PacketInfo>();
            foreach(var entry in overTheAirPackets)
            {
                if(!entry.PacketIsStale)
                {
                    result.Add(entry);
                }
            }
            return result.ToArray();
        }

        public static bool ForceBusyRssi = false;

        private static PacketInfo PacketLookup(IRadio sender)
        {
            foreach(var entry in overTheAirPackets)
            {
                if(entry.Sender == sender)
                {
                    return entry;
                }
            }

            return null;
        }

        private const int RssiBusyChannelHardCodedValueDbm = 20;
        private const int RssiClearChannelHardCodedValueDbm = -120;

        public static event Action InterferenceQueueChanged;
    }

    public class PacketInfo
    {
        public PacketInfo(IRadio sender, PhySignature signature, int channel, int txPowerDbm, byte[] content)
        {
            this.sender = sender;
            this.signature = signature;
            this.channel = channel;
            this.txPowerDbm = txPowerDbm;

            packetIsStale = false;
            startTx = IPeripheralExtensions.GetMachine(sender).LocalTimeSource.ElapsedVirtualTime;
            packetContent = new byte[content.Length];
            Array.Copy(content, packetContent, content.Length);
        }

        // Methods
        public IRadio Sender => sender;

        public PhySignature Signature => signature;

        public int Channel => channel;

        public int TxPowerDbm => txPowerDbm;

        public byte[] PacketContent => packetContent;

        public bool PacketIsStale
        {
            set
            {
                packetIsStale = value;
            }

            get
            {
                return packetIsStale;
            }
        }

        // Set when the transmission was cut short instead of completing. Only meaningful once the
        // packet is stale, i.e. once the sender has retired it.
        public bool PacketWasAborted { get; set; }

        public TimeInterval StartTxTimestamp
        {
            set
            {
                startTx = value;
            }

            get
            {
                return startTx;
            }
        }

        private TimeInterval startTx;
        private bool packetIsStale;
        // Fields
        private readonly byte[] packetContent;
        private readonly IRadio sender;
        private readonly PhySignature signature;
        private readonly int channel;
        private readonly int txPowerDbm;
    }

    // Coarse modulation grouping used to decide which transmissions can be sensed/decoded
    // by a given receiver.
    public enum RadioModulationFamily
    {
        Unknown = 0,
        Fsk,
        Msk,
        Oqpsk,
        Bpsk,
        Ook,
        Dqpsk,
        Dpsk8,
    }

    // Symbol coding scheme. The DSSS spreading parameters in PhySignature are only meaningful 
    // when this is Dsss.
    public enum RadioSymbolCoding
    {
        Nrz = 0,
        Manchester = 1,
        Dsss = 2,
        Linecode = 3,
    }

    // Register-derived description of the PHY a radio is currently configured for. The carrier
    // frequency is tracked separately via the channel; this captures the modulation properties
    // that determine whether two radios can sense (energy) or decode (frame) each other.
    public readonly struct PhySignature : IEquatable<PhySignature>
    {
        public PhySignature(RadioModulationFamily modulationFamily, RadioSymbolCoding coding, uint dataRateBps,
            uint dsssLength, uint dsssShifts, uint dsssDoublingMode, uint codedSymbolsPerInfoBit)
        {
            ModulationFamily = modulationFamily;
            Coding = coding;
            DataRateBps = dataRateBps;
            DsssLength = dsssLength;
            DsssShifts = dsssShifts;
            DsssDoublingMode = dsssDoublingMode;
            CodedSymbolsPerInfoBit = codedSymbolsPerInfoBit;
        }

        public RadioModulationFamily ModulationFamily { get; }

        public RadioSymbolCoding Coding { get; }

        public uint DataRateBps { get; }

        public uint DsssLength { get; }

        public uint DsssShifts { get; }

        public uint DsssDoublingMode { get; }

        // On-air symbols per information bit for FEC line-coded PHYs (BLE LE Coded: 8 for S=8/125 kbps,
        // 2 for S=2/500 kbps; 1 for every uncoded PHY). Carried so a receiver can clock an incoming coded
        // frame at the rate the transmitter actually used. Intentionally excluded from 
        // decode-compatibility/equality and hashing: a coded receiver can demodulate either sub-rate.
        public uint CodedSymbolsPerInfoBit { get; }

        // Relative tolerance applied when comparing data rates for decode compatibility (5%). The nearest
        // distinct standard rates are at least ~2x apart, so this safely merges register-rounding/crystal
        // differences without merging genuinely different PHYs.
        private const double DataRateRelativeTolerance = 0.05;

        // A receiver can attempt to demodulate a frame only when modulation family,
        // symbol coding, data rate and DSSS configuration all match. The sync word itself is
        // enforced later by each model's RX sync-detection pipeline.
        //
        // The data rate is compared with a relative tolerance: different radio models derive it
        // from registers with slightly different rounding, and real crystals add small offsets. 
        // The tolerance absorbs those while keeping genuinely different rates 
        // (which are at least ~2x apart) distinct.
        public bool IsDecodeCompatibleWith(PhySignature other)
        {
            return ModulationFamily == other.ModulationFamily
                && Coding == other.Coding
                && DataRatesMatch(DataRateBps, other.DataRateBps)
                && DsssLength == other.DsssLength
                && DsssShifts == other.DsssShifts
                && DsssDoublingMode == other.DsssDoublingMode;
        }

        private static bool DataRatesMatch(uint a, uint b)
        {
            if(a == b)
            {
                return true;
            }

            uint max = Math.Max(a, b);
            if(max == 0)
            {
                return true;
            }

            uint diff = (a > b) ? (a - b) : (b - a);
            return diff <= max * DataRateRelativeTolerance;
        }

        public bool Equals(PhySignature other)
        {
            return IsDecodeCompatibleWith(other);
        }

        public override bool Equals(object obj)
        {
            return obj is PhySignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            // Data rate is intentionally excluded: it is compared with a tolerance in equality, so two
            // equal signatures can have slightly different rates and must still hash the same.
            return ((int)ModulationFamily * 397) ^ (int)Coding
                ^ (int)DsssLength ^ (int)DsssShifts ^ (int)DsssDoublingMode;
        }

        public override string ToString()
        {
            var modulation = ModulationFamilyName(ModulationFamily);
            var coding = CodingName(Coding);
            var rate = DataRateString(DataRateBps);

            // The DSSS spreading parameters are only meaningful for DSSS coding. DsssLength is the
            // spreading sequence length minus one, so the chip count is DsssLength + 1.
            if(Coding == RadioSymbolCoding.Dsss)
            {
                uint chips = DsssLength + 1;
                coding = $"{coding} [chp={chips}, shft={DsssShifts}, dbl={DsssDoublingMode}]";
            }

            return $"{modulation} {coding} {rate}";
        }

        private static string ModulationFamilyName(RadioModulationFamily family)
        {
            switch(family)
            {
            case RadioModulationFamily.Fsk:
                return "FSK";
            case RadioModulationFamily.Msk:
                return "MSK";
            case RadioModulationFamily.Oqpsk:
                return "O-QPSK";
            case RadioModulationFamily.Bpsk:
                return "BPSK";
            case RadioModulationFamily.Ook:
                return "OOK";
            case RadioModulationFamily.Dqpsk:
                return "π/4-DQPSK";
            case RadioModulationFamily.Dpsk8:
                return "8-DPSK";
            default:
                return "Unknown";
            }
        }

        private static string CodingName(RadioSymbolCoding coding)
        {
            switch(coding)
            {
            case RadioSymbolCoding.Nrz:
                return "NRZ";
            case RadioSymbolCoding.Manchester:
                return "Manchester";
            case RadioSymbolCoding.Dsss:
                return "DSSS";
            case RadioSymbolCoding.Linecode:
                return "Linecode";
            default:
                return $"coding{(uint)coding}";
            }
        }

        private static string DataRateString(uint dataRateBps)
        {
            if(dataRateBps != 0 && (dataRateBps % 1000000) == 0)
            {
                return $"{dataRateBps / 1000000}Mbps";
            }
            if(dataRateBps != 0 && (dataRateBps % 1000) == 0)
            {
                return $"{dataRateBps / 1000}kbps";
            }
            return $"{dataRateBps}bps";
        }
    }
}
