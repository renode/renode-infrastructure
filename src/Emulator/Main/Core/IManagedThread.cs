//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Time;

namespace Antmicro.Renode.Core
{
    public interface IManagedThread : IDisposable
    {
        void Start();
        void StartDelayed(TimeInterval delay);
        void Stop();

        uint Frequency { get; set; }
    }
}

