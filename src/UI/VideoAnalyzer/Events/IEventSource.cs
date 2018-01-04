//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Extensions.Analyzers.Video.Handlers;

namespace Antmicro.Renode.Extensions.Analyzers.Video.Events
{
    internal interface IEventSource
    {
        void AttachHandler(IOHandler h);
        void DetachHandler();

        int X { get; }
        int Y { get; }
    }
}

