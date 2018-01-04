//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Utilities
{
    public sealed class SerializableWeakReference<T> where T : class
    {
        public SerializableWeakReference(T target)
        {
            reference = new WeakReference(target);
        }

        public T Target
        {
            get
            {
                return (T)reference.Target;
            }
        }

        public bool IsAlive
        {
            get
            {
                return Target == null;
            }
        }

        [PreSerialization]
        private void BeforeSerialization()
        {
            objectToSave = Target;
        }

        [PostSerialization]
        private void AfterSerialization()
        {
            objectToSave = null;
        }

        [PostDeserialization]
        private void AfterDeserialization()
        {
            reference = new WeakReference(objectToSave);
        }

        [Transient]
        private WeakReference reference;

        private T objectToSave;
    }
}

