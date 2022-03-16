//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Exceptions
{
    public class RegisterValueUnavailableException : RecoverableException
    {
        public RegisterValueUnavailableException(string message) : base(message)
        {
        }

        public RegisterValueUnavailableException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

