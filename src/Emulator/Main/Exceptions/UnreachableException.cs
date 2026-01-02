//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Exceptions
{
    public sealed class UnreachableException : Exception
    {
        public UnreachableException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public UnreachableException(string message) : base(message)
        {
        }

        public UnreachableException() : base()
        {
        }
    }
}
