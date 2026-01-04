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
    public sealed class AttachedRegistrationPoint : ITheOnlyPossibleRegistrationPoint, IJsonSerializable
    {
        static AttachedRegistrationPoint()
        {
            Instance = new AttachedRegistrationPoint();
        }

        public static AttachedRegistrationPoint Instance { get; private set; }

        public override string ToString()
        {
            return PrettyString;
        }

        public Object SerializeJson()
        {
            return new
            {
                Type = "Attached"
            };
        }

        public string PrettyString
        {
            get
            {
                return "attached";
            }
        }

        private AttachedRegistrationPoint()
        {
        }
    }
}