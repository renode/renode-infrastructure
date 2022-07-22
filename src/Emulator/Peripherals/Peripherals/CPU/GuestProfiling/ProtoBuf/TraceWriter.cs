//
// Copyright (c) 2010-2022 Antmicro
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
    public class TraceWriter
    {
        public TraceWriter()
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

            if (isCounterTrack)
                track.Counter = new TrackDescriptor.CounterDescriptor();

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

        public void FlushBuffer(string filename)
        {
            // Add a sync packet
            trace.Packets.Add(new TracePacket
            {
                TrustedPacketSequenceId = SequenceId,
                SynchronizationMarker = SyncMarkerBytes
            });

            using(var stream = File.Open(filename, FileMode.Append))
            {
                global::ProtoBuf.Serializer.Serialize(stream, trace);
                trace.Packets.Clear();
            }
        }

        public int GetPacketCount()
        {
            return trace.Packets.Count;
        }

        // Sync marker has to use these bytes
        private static readonly byte[] SyncMarkerBytes = Guid.ParseExact("{82477a76-b28d-42ba-81dc-33326d57a079}", "B").ToByteArray();
        private const uint SequenceId = 0;
        private readonly Trace trace;
    }
}
