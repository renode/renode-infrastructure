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
    public sealed class AttachedRegistrationPoint : ITheOnlyPossibleRegistrationPoint
    {
        public static AttachedRegistrationPoint Instance { get; private set; }

        static AttachedRegistrationPoint()
        {
            Instance = new AttachedRegistrationPoint();
        }

        public string PrettyString
        {
            get
            {
                return "attached";
            }
        }

        public override string ToString()
        {
            return PrettyString;
        }

        private AttachedRegistrationPoint()
        {
        }
    }
}

