//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Time;

namespace Antmicro.Renode.Core
{
    public interface ISimpleManagedThread : IDisposable
    {
        void Start();
        void StartDelayed(TimeInterval delay);
        void Stop();
    }
}
