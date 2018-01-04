//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

namespace Antmicro.Renode.Utilities
{
    public class WeakWrapper<T> where T : class
    {
        // this method can be used to create *fake* WeakWrapper that holds no real WeakReference
        // it is intended for use with methods like *Compare*, *ContainsKey* etc. yet without the overhead of creating the WeakReference
        public static WeakWrapper<T> CreateForComparison(T obj)
        {
            return new WeakWrapper<T>(obj, true);
        }

        public WeakWrapper(T obj) : this(obj, false)
        {
        }

        public void ConvertToRealWeakWrapper()
        {
            var objReferenceCopy = objReference;
            if(objReferenceCopy == null)
            {
                throw new InvalidOperationException("Cannot convert real weak wrapper.");
            }

            objWeakReference = new WeakReference<T>(objReferenceCopy);
            objReference = null;
        }

        public override bool Equals(object obj)
        {
            var objAsWeakWrapper = obj as WeakWrapper<T>;
            if(objAsWeakWrapper != null)
            {
                T myTarget, objTarget;
                bool hasTarget;
                return ((hasTarget = TryGetTarget(out myTarget)) == objAsWeakWrapper.TryGetTarget(out objTarget)) && hasTarget && myTarget.Equals(objTarget);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return objHashCode;
        }

        public bool TryGetTarget(out T target)
        {
            if(objReference != null)
            {
                target = objReference;
                return true;
            }
            return objWeakReference.TryGetTarget(out target);
        }

        private WeakWrapper(T obj, bool doNotCreateWeakReference)
        {
            // we can calculate the hashcode here, because according
            // to "The Path of C#" `GetHashCode` method result should not
            // be calculated from mutable fields, so it should not change
            // during object's lifetime
            objHashCode = obj.GetHashCode();
            if(doNotCreateWeakReference)
            {
                objReference = obj;
            }
            else
            {
                objWeakReference = new WeakReference<T>(obj);
            }
        }

        private T objReference;
        private WeakReference<T> objWeakReference;
        private readonly int objHashCode;
    }
}
