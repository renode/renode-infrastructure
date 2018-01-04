//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class CpuAbortException : Exception
    {
        public CpuAbortException()
        {
        }
        

        public CpuAbortException(string message) : base(message)
        {
        }
        

        public CpuAbortException(string message, Exception innerException) : base(message, innerException)
        {
        }
        
    }
}

