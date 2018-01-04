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
    public class RegistrationException : RecoverableException
    {

        public RegistrationException(string name, string parentName):base(string.Format("Could not register {0} in {1}.", name, parentName))
        {
        }

        public RegistrationException (string name, string parentName, string reason):base(string.Format("Could not register {0} in {1}. Reason: {2}.", name, parentName, reason))
        {
        }
        public RegistrationException(string message):base(message)
        {
        }
    }
}

