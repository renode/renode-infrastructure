//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.EventRecording
{
    public interface IRecordEntry
    {
        void Play(Func<string, Delegate, Delegate> handlerResolver);
        TimeInterval Timestamp { get; }
    }
}

