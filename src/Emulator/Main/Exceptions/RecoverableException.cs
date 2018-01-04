//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Exceptions
{
    public class RecoverableException : Exception
    {
        public RecoverableException(Exception innerException) : base(String.Empty, innerException)
        {
        }

        public RecoverableException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public RecoverableException(string message):base(message)
        {
        }

        public RecoverableException():base()
        {
        }
    }
}

