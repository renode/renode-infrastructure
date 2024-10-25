//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using Antmicro.Migrant;
using Antmicro.Migrant.Customization;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.UART;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.EventRecording
{
    [Transient]
    public class Player : IDisposable
    {
        public Player(FileStream stream, IMachine machine)
        {
            this.machine = machine;
            this.stream = stream;
            deserializer = new Serializer(new Settings(useBuffering: false, disableTypeStamping: true)).ObtainOpenStreamDeserializer(stream);
            handlersCache = new Dictionary<NameAndHandler, Delegate>();
            entries = deserializer.DeserializeMany<IRecordEntry>().GetEnumerator();
            if(!entries.MoveNext())
            {
                entries = null;
            }
        }

        public void Play(TimeInterval elapsedVirtualTime)
        {
            if(entries == null)
            {
                return;
            }
            while(entries.Current.Timestamp <= elapsedVirtualTime)
            {
                var entry = entries.Current;
                entry.Play(ResolveHandler);
                if(!entries.MoveNext())
                {
                    entries = null;
                    return;
                }
            }
        }

        public void Dispose()
        {
            stream.Close();
        }

        private Delegate ResolveHandler(string name, Delegate nullifiedHandler)
        {
            var nameAndHandler = new NameAndHandler(name, nullifiedHandler);
            if(handlersCache.ContainsKey(nameAndHandler))
            {
                return handlersCache[nameAndHandler];
            }
            var target = machine[name];
            var result = Delegate.CreateDelegate(nullifiedHandler.GetType(), target, nullifiedHandler.Method);
            handlersCache.Add(nameAndHandler, result);
            return result;
        }

        private IEnumerator<IRecordEntry> entries;
        private readonly Serializer.OpenStreamDeserializer deserializer;
        private readonly Dictionary<NameAndHandler, Delegate> handlersCache;
        private readonly FileStream stream;
        private readonly IMachine machine;

        private struct NameAndHandler
        {
            public NameAndHandler(string name, Delegate handler) : this()
            {
                this.Name = name;
                this.Handler = handler;
            }

            public string Name { get; private set; }
            public Delegate Handler { get; private set; }

            public override bool Equals(object obj)
            {
                if(obj == null)
                {
                    return false;
                }
                if(obj.GetType() != typeof(NameAndHandler))
                {
                    return false;
                }
                var other = (NameAndHandler)obj;
                return this == other;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Name != null ? Name.GetHashCode() : 0) ^ (Handler != null ? Handler.GetHashCode() : 0);
                }
            }

            public static bool operator==(NameAndHandler first, NameAndHandler second)
            {
                return first.Name == second.Name && first.Handler == second.Handler;
            }

            public static bool operator!=(NameAndHandler first, NameAndHandler second)
            {
                return !(first == second);
            }
        }
    }
}

