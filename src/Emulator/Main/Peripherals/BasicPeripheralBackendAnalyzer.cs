//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals
{
    public abstract class BasicPeripheralBackendAnalyzer<T> : IAnalyzableBackendAnalyzer<T> where T : IAnalyzableBackend
    {
        public virtual void AttachTo(T backend)
        {
            Backend = backend;
        }

        public abstract void Show();
        public abstract void Hide();

        public IAnalyzableBackend Backend { get; private set; }
    }
}

