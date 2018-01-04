//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
namespace Antmicro.Renode.Exceptions
{
    public class ConfigurationException : System.Exception
    {
        public ConfigurationException(String message) : base(message)
        {
        }
    }
}
