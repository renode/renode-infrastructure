//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals
{
    public class DelayedInvalidationContext : IDisposable
    {
        public DelayedInvalidationContext(IHasDelayedInvalidationContext obj)
        {
            this.obj = obj;
            this.obj.SetDelayedInvalidation(true);
        }

        public void Dispose()
        {
            this.obj.SetDelayedInvalidation(false);
        }

        private readonly IHasDelayedInvalidationContext obj;
    }

    public interface IHasDelayedInvalidationContext
    {
        DelayedInvalidationContext EnterDelayedInvalidationContext();

        void SetDelayedInvalidation(bool value);
    }
}