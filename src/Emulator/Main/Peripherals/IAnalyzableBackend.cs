//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals
{
    public interface IAnalyzableBackend<T> : IAnalyzableBackend where T : IAnalyzable 
    {
        void Attach(T emulationElement);
    }

    public interface IAnalyzableBackend : IAutoLoadType
    {
        IAnalyzable AnalyzableElement { get; }
    }
}

