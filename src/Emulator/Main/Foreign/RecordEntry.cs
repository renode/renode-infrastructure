//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.EventRecording
{
    internal class RecordEntry<T> : IRecordEntry
    {
        public RecordEntry(string name, T value, Action<T> handler, TimeInterval timestamp)
        {
            Value = value;
            @base = new RecordEntryBase(name, handler, timestamp);
        }

        public void Play(Func<string, Delegate, Delegate> handlerResolver)
        {
            ((Action<T>)handlerResolver(Name, @base.Handler))(Value);
        }

        public override string ToString()
        {
            return string.Format("[RecordEntry: Base={0}, Value={1}]", @base, Value);
        }

        public T Value { get; private set; }

        public string Name
        {
            get
            {
                return @base.Name;
            }
        }

        public TimeInterval Timestamp
        {
            get
            {
                return @base.Timestamp;
            }
        }

        private RecordEntryBase @base;
    }

    internal class RecordEntry<T1, T2> : IRecordEntry
    {
        public RecordEntry(string name, T1 value1, T2 value2, Action<T1, T2> handler, TimeInterval timestamp)
        {
            Value1 = value1;
            Value2 = value2;
            @base = new RecordEntryBase(name, handler, timestamp);
        }

        public void Play(Func<string, Delegate, Delegate> handlerResolver)
        {
            ((Action<T1, T2>)handlerResolver(Name, @base.Handler))(Value1, Value2);
        }

        public override string ToString()
        {
            return string.Format("[RecordEntry: Base={0}, Value1={1}, Value2={2}]", @base, Value1, Value2);
        }

        public T1 Value1 { get; private set; }
        public T2 Value2 { get; private set; }

        public string Name
        {
            get
            {
                return @base.Name;
            }
        }

        public TimeInterval Timestamp
        {
            get
            {
                return @base.Timestamp;
            }
        }

        private RecordEntryBase @base;
    }
}

