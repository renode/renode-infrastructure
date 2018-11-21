//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.PCI
{
        [Flags]
        public enum HeaderType
        {
            Endpoint = 0,
            Bridge = 1,
            CardBus = 2,
            MultiFunctionDevice = 1 << 7,
        }

        public static class HeaderTypeExtensions
        {
            public static int MaxNumberOfBARs(this HeaderType type)
            {
                switch(type)
                {
                    case HeaderType.Bridge:
                        return 2;
                    case HeaderType.Endpoint:
                    default:
                        //6 is a safest default, but it's certainly ok for endpoints
                        return 6;
                }
            }
        }
}
