//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.EventRecording
{
    // this is struct to prevent open stream serializer from caching all classes
    internal struct RecordEntryBase
    {
        public RecordEntryBase(string name, Delegate handler, long syncNumber) : this()
        {
            this.Name = name;
            this.SyncNumber = syncNumber;
            if(handler.Target != null)
            {
                throw new ArgumentException("Assertion failed: the handler is supposed to have null target.");
            }
            this.Handler = handler;
        }

        public override string ToString()
        {
            return string.Format("[RecordEntry: Handler={0}, Name={1}, SyncNumber={2}]", Handler.Method.Name, Name, SyncNumber);
        }

        public string Name { get; private set; }
        public long SyncNumber { get; private set; }

        public readonly Delegate Handler;
    }
}

