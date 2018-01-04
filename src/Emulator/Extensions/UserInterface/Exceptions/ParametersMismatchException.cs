//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.UserInterface.Exceptions
{
    public class ParametersMismatchException : RecoverableException
    {
        public ParametersMismatchException() : base("Parameters did not match the signature")
        {
        }
        public ParametersMismatchException(string message) : base(message)
        {
        }
    }
}
