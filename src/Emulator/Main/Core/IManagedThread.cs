//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Core
{
    public interface IManagedThread : ISimpleManagedThread
    {
        uint Frequency { get; set; }
    }
}