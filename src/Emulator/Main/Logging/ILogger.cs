//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Logging
{
    public interface ILogger : IDisposable
    {
        string GetMachineName(int id);
        string GetObjectName(int id);

        void Flush();

        int GetOrCreateSourceId(object source);
        bool TryGetName(int id, out string objectName, out string machineName);
        bool TryGetSourceId(object source, out int id);

        bool SynchronousLogging { get; set; }
    }
}

