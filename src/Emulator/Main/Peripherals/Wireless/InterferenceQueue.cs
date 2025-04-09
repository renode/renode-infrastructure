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
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public interface IInterferenceQueueListener
    {
        void InteferenceQueueChangedCallback();
    }

    public static class InterferenceQueue
    {
        static readonly List<PacketInfo> overTheAirPackets = new List<PacketInfo>();
        static readonly List<IInterferenceQueueListener> listeners = new List<IInterferenceQueueListener>();

        public static void Subscribe(IInterferenceQueueListener listener)
        {
            listeners.Add(listener);
        }

        public static void Add(IRadio sender, RadioPhyId phyId, int channel, int txPowerDbm, byte[] content)
        {
            // First we check if there is already a packet from this sender in the queue. If that is the case, 
            // it should be stale and we are going to remove it.
            PacketInfo entry = PacketLookup(sender);
            if (entry != null)
            {
                if (!entry.PacketIsStale)
                {
                    Logger.Log(LogLevel.Error, "InterferenceQueue.Add: sender non-stale entry present");
                    return;
                }
                Remove(sender);
            }

            TimeInterval addTime = IPeripheralExtensions.GetMachine(sender).LocalTimeSource.ElapsedVirtualTime;
            PacketInfo newEntry = new PacketInfo(sender, phyId, channel, txPowerDbm, content);
            newEntry.StartTxTimestamp = addTime;
            overTheAirPackets.Add(newEntry);
            Logger.Log(LogLevel.Noisy, "InterferenceQueue.Add at {0}: [{1}]", addTime, BitConverter.ToString(content));
            NotifyListeners();
        }

        public static void Remove(IRadio sender)
        {
            PacketInfo entry = PacketLookup(sender);

            if (entry == null)
            {
                Logger.Log(LogLevel.Error, "InterferenceQueue.Remove: entry not found");
                return;
            }

            if (entry.PacketIsStale)
            {
                Logger.Log(LogLevel.Noisy, "InterferenceQueue.Remove at {0}, OTA time={1}: [{2}] - removed", 
                        IPeripheralExtensions.GetMachine(entry.Sender).LocalTimeSource.ElapsedVirtualTime, 
                        IPeripheralExtensions.GetMachine(entry.Sender).LocalTimeSource.ElapsedVirtualTime - entry.StartTxTimestamp,
                        BitConverter.ToString(entry.PacketContent));
                        overTheAirPackets.Remove(entry);
            }
            else
            {
                Logger.Log(LogLevel.Noisy, "InterferenceQueue.Remove at {0}, OTA time={1}: [{2}] - marking it stale", 
                        IPeripheralExtensions.GetMachine(entry.Sender).LocalTimeSource.ElapsedVirtualTime, 
                        IPeripheralExtensions.GetMachine(entry.Sender).LocalTimeSource.ElapsedVirtualTime - entry.StartTxTimestamp,
                        BitConverter.ToString(entry.PacketContent));
                        entry.PacketIsStale = true;
            }

            overTheAirPackets.Remove(entry);
            NotifyListeners();
        }

        public static TimeInterval GetTxStartTime(IRadio sender)
        {
            // Here we want to look up also stale packets, since it is possible that the sender already "completed"
            // the transmission while the receiver hasn't been notified yet (possibly due to a combination of the packet
            // being short and the QuantumTime being long enough).
            PacketInfo entry = PacketLookup(sender);

            if (entry == null)
            {
                Logger.Log(LogLevel.Error, "InterferenceQueue.GetTxStartTime: entry not found");
                return TimeInterval.Empty;
            }

            return entry.StartTxTimestamp;
        }

        // Simple initial implementation: we return a "high" hardcoded RSSI value if there is 
        // at least a packet going over the air on the passed PHY/Channel.
        // TODO: Eventually we will want to:
        // - take into account the distance between sender and receiver
        // - the sender TX power
        // - interference between different PHYs
        // - Co-channel interference 
        public static int GetCurrentRssi(IRadio receiver, RadioPhyId phyId, int channel)
        {
            if (ForceBusyRssi)
            {
                return RssiBusyChannelHardCodedValueDbm;
            }

            foreach(PacketInfo entry in overTheAirPackets)
            {
                if (entry.PhyId == phyId && entry.Channel == channel)
                {
                    return RssiBusyChannelHardCodedValueDbm;
                }
            }

            return RssiClearChannelHardCodedValueDbm;
        }

        private static PacketInfo PacketLookup(IRadio sender)
        {
            foreach(PacketInfo entry in overTheAirPackets)
            {
                if (entry.Sender == sender)
                {
                    return entry;
                }
            }

            return null;
        }

        private static void NotifyListeners()
        {
            foreach(IInterferenceQueueListener listener in listeners)
            {
                listener.InteferenceQueueChangedCallback();
            }
        }

        private const int RssiBusyChannelHardCodedValueDbm = 20;
        private const int RssiClearChannelHardCodedValueDbm = -120;
        public static bool ForceBusyRssi = false;
    }

    public class PacketInfo
    {
        // Fields
        private byte[] packetContent;
        private IRadio sender;
        private RadioPhyId phyId;
        private int channel;
        private int txPowerDbm;
        private TimeInterval startTx;
        private bool packetIsStale;

        // Methods
        public IRadio Sender => sender;
        public RadioPhyId PhyId => phyId;
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
        public PacketInfo(IRadio sender, RadioPhyId phyId, int channel, int txPowerDbm, byte[] content)
        {
            this.sender = sender;
            this.phyId = phyId;
            this.channel = channel;
            this.txPowerDbm = txPowerDbm;

            packetIsStale = false;
            startTx = IPeripheralExtensions.GetMachine(sender).LocalTimeSource.ElapsedVirtualTime;
            packetContent = new byte[content.Length];
            Array.Copy(content, packetContent, content.Length);
        }
    }

    public enum RadioPhyId
    {
        Phy_802154_2_4GHz_OQPSK = 0,
        Phy_BLE_2_4GHz_GFSK     = 1,
    }
}