//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.UserInterface.Exceptions
{
    public class ParametersMismatchException : RecoverableException
    {
        public ParametersMismatchException(Type type, string command, string name) : base("Parameters did not match the signature")
        {
            Name = name;
            Type = type;
            Command = command;
        }
        public ParametersMismatchException(Type type, string command, string name, string message) : base(message)
        {
            Name = name;
            Type = type;
            Command = command;
        }

        public string Name { get; }
        public Type Type { get; }
        public string Command { get; }
    }
}
