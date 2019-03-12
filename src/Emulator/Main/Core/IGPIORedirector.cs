//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Core
{
    public class IGPIORedirector : IReadOnlyDictionary<int, IGPIO>
    {
        public IGPIORedirector(int size, Action<int, IGPIOReceiver, int> connector, Action<int> disconnector = null)
        {
            Count = size;
            this.connector = connector;
            this.disconnector = disconnector;
        }

        public bool ContainsKey(int key)
        {
            return key >= 0 && key < Count;
        }

        public bool TryGetValue(int key, out IGPIO value)
        {
            if(key >= Count)
            {
                value = null;
                return false;
            }
            value = new GPIOWrapper(key, connector, disconnector);
            return true;
        }

        public IGPIO this[int index]
        {
            get
            {
                IGPIO value;
                if(!TryGetValue(index, out value))
                {
                    throw new ArgumentOutOfRangeException();
                }
                return value;
            }
        }

        public IEnumerable<int> Keys
        {
            get
            {
                for(int i = 0; i < Count; i++)
                {
                    yield return i;
                }
            }
        }

        public IEnumerable<IGPIO> Values
        {
            get
            {
                foreach(var key in Keys)
                {
                    yield return new GPIOWrapper(key, connector, disconnector);
                }
            }
        }

        public IEnumerator<KeyValuePair<int, IGPIO>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public int Count { get; private set; }

        private readonly Action<int, IGPIOReceiver, int> connector;
        private readonly Action<int> disconnector;

        private class GPIOWrapper : IGPIO
        {
            public GPIOWrapper(int id, Action<int, IGPIOReceiver, int> connector, Action<int> disconnector)
            {
                this.id = id;
                this.connector = connector;
                this.disconnector = disconnector;
            }

            public void Set(bool value)
            {
                throw new NotImplementedException();
            }

            public void Toggle()
            {
                throw new NotImplementedException();
            }

            public void Connect(IGPIOReceiver destination, int destinationNumber)
            {
                connector(id, destination, destinationNumber);
            }

            public void Disconnect()
            {
                if (disconnector != null)
                {
                    disconnector(id);
                    return;
                }

                throw new NotImplementedException();
            }

            public void Disconnect(GPIOEndpoint enpoint)
            {
                throw new NotImplementedException();
            }

            public bool IsSet
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public bool IsConnected
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public IList<GPIOEndpoint> Endpoints
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            private readonly int id;
            private readonly Action<int, IGPIOReceiver, int> connector;
            private readonly Action<int> disconnector;
        }
    }
}

