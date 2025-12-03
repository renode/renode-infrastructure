//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Core.Structure
{
    public sealed class NullRegistrationPoint : ITheOnlyPossibleRegistrationPoint, IJsonSerializable
    {
        static NullRegistrationPoint()
        {
            Instance = new NullRegistrationPoint();
        }

        public static NullRegistrationPoint Instance { get; private set; }

        public override string ToString()
        {
            return "[-]";
        }

        public Object SerializeJson()
        {
            return new
            {
                Type = "Null"
            };
        }

        public string PrettyString
        {
            get
            {
                return ToString();
            }
        }

        private NullRegistrationPoint()
        {
        }
    }
}