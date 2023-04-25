//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities.Binding;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling.ProtoBuf
{
    public class PerfettoTraceWriter
    {
        public PerfettoTraceWriter()
        {
            trace = new Trace();
        }

        public void CreateTrack(string name, ulong trackId, bool isCounterTrack = false)
        {
            var track = new TrackDescriptor
            {
                Uuid = trackId,
                Name = name
            };

            if(isCounterTrack)
            {
                track.Counter = new TrackDescriptor.CounterDescriptor();
            }

            trace.Packets.Add(new TracePacket
            {
                TrustedPacketSequenceId = SequenceId,
                TrackDescriptor = track
            });
        }

        public void CreateEventBegin(ulong timestamp, string name, ulong trackId)
        {
            trace.Packets.Add(new TracePacket
            {
                Timestamp = timestamp,
                TrustedPacketSequenceId = SequenceId,
                TrackEvent = new TrackEvent
                {
                    Name = name,
                    TrackUuid = trackId,
                    type = TrackEvent.Type.TypeSliceBegin
                }
            });
        }

        public void CreateEventEnd(ulong timestamp, ulong trackId)
        {
            trace.Packets.Add(new TracePacket
            {
                Timestamp = timestamp,
                TrustedPacketSequenceId = SequenceId,
                TrackEvent = new TrackEvent
                {
                    TrackUuid = trackId,
                    type = TrackEvent.Type.TypeSliceEnd
                }
            });
        }

        public void CreateEventInstant(ulong timestamp, string name, ulong trackId)
        {
            trace.Packets.Add(new TracePacket
            {
                Timestamp = timestamp,
                TrustedPacketSequenceId = SequenceId,
                TrackEvent = new TrackEvent
                {
                    Name = name,
                    TrackUuid = trackId,
                    type = TrackEvent.Type.TypeInstant
                }
            });
        }

        public void CreateEventCounter(ulong timestamp, long value, ulong trackId)
        {
            trace.Packets.Add(new TracePacket
            {
                Timestamp = timestamp,
                TrustedPacketSequenceId = SequenceId,
                TrackEvent = new TrackEvent
                {
                    CounterValue = value,
                    TrackUuid = trackId,
                    type = TrackEvent.Type.TypeCounter
                }
            });
        }

        public void CreateEventCounter(ulong timestamp, double value, ulong trackId)
        {
            trace.Packets.Add(new TracePacket
            {
                Timestamp = timestamp,
                TrustedPacketSequenceId = SequenceId,
                TrackEvent = new TrackEvent
                {
                    DoubleCounterValue = value,
                    TrackUuid = trackId,
                    type = TrackEvent.Type.TypeCounter
                }
            });
        }

        public void FlushBuffer(Stream fileStream)
        {
            if(lastSyncMarker >= SyncMarkerInterval)
            {
                // Add a sync packet
                trace.Packets.Add(new TracePacket
                {
                    TrustedPacketSequenceId = SequenceId,
                    SynchronizationMarker = SyncMarkerBytes
                });
                lastSyncMarker = 0;
            }

            global::ProtoBuf.Serializer.Serialize(fileStream, trace);
            lastSyncMarker += (uint)PacketCount;
            trace.Packets.Clear();
        }

        public void RemoveLastNPackets(int packetCount)
        {
            trace.Packets.RemoveRange(trace.Packets.Count - packetCount, packetCount);
        }

        public int PacketCount => trace.Packets.Count;

        // Perfetto will look for this exact sequence when it is parsing a longer trace
        // Emitting this sequence can be used to partition the trace, so Perfetto doesn't have to load everything at once
        private static readonly byte[] SyncMarkerBytes = Guid.ParseExact("{82477a76-b28d-42ba-81dc-33326d57a079}", "B").ToByteArray();
        
        private readonly Trace trace;
        private uint lastSyncMarker;

        private const uint SequenceId = 0;
        private const uint SyncMarkerInterval = 10000;
    }
}
