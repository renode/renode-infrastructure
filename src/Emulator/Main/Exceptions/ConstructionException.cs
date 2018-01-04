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
    public class ConstructionException : RecoverableException
    {
        public ConstructionException (string message):base(message)
        {
        }

        public ConstructionException (string message, Exception innerException):base(message, innerException)
        {
        }
    }
}

