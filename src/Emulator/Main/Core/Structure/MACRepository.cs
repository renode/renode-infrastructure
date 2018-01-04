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
    public class MACRepository
    {
        public MACAddress GenerateUniqueMAC()
        {
            lock(currentMAClock)
            {
                var result = currentMAC;
                currentMAC = currentMAC.Next();
                return result;
            }
        }

        private MACAddress currentMAC = MACAddress.Parse("00:00:00:00:00:02");
        private readonly object currentMAClock = new object();
    }
}

