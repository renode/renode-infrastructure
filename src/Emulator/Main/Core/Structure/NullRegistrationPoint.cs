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
    public sealed class NullRegistrationPoint : ITheOnlyPossibleRegistrationPoint
    {
        public static NullRegistrationPoint Instance { get; private set; }

        public string PrettyString
        {
            get
            {
                return ToString();
            }
        }

        public override string ToString()
        {
            return "[-]";
        }

        static NullRegistrationPoint()
        {
            Instance = new NullRegistrationPoint();
        }

        private NullRegistrationPoint()
        {
        }
    }
}

