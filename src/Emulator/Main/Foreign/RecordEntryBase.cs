//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.EventRecording
{
    // this is struct to prevent open stream serializer from caching all classes
    internal struct RecordEntryBase
    {
        public RecordEntryBase(string name, Delegate handler, TimeInterval timestamp) : this()
        {
            DebugHelper.Assert(handler.Target == null, "The handler is supposed to have null target");

            this.Name = name;
            this.Timestamp = timestamp;
            this.Handler = handler;
        }

        public override string ToString()
        {
            return string.Format("[RecordEntry: Handler={0}, Name={1}, Timestamp={2}]", Handler.Method.Name, Name, Timestamp);
        }

        public string Name { get; private set; }
        public TimeInterval Timestamp { get; private set; }

        public readonly Delegate Handler;
    }
}

