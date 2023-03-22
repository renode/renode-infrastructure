//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Debugging;

namespace Antmicro.Renode.Utilities
{
    public class SealableValue<T> where T: struct
    {
        public DisposableWrapper Seal()
        {
            lock(locker)
            {
                lockCounter++;
                return DisposableWrapper.New(Unseal);
            }
        }

        public T Value
        {
            get => innerValue;
            set
            {
                lock(locker)
                {
                    deferredValue = value;
                    if(IsLocked)
                    {
                        return;
                    }
                    innerValue = value;
                }
            }
        }

        private void Unseal()
        {
            // This function should be only used by DisposableWrapper.Dispose()
            lock(locker)
            {
                DebugHelper.Assert(IsLocked);
                lockCounter--;

                if(!IsLocked)
                {
                    innerValue = deferredValue;
                }
            }
        }

        private bool IsLocked => lockCounter > 0;

        private T innerValue;
        private T deferredValue;

        private uint lockCounter;
        private readonly object locker = new object();
    }
}
