//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities
{
    // You might ask, why there is CustomDateTime calculating elapsed time in such a strange manner?
    // This is because getting DateTime.Now on mono is veeeeeeeeery slow and this is much faster 
    public static class CustomDateTime
    {
        public static DateTime Now { get { return DateTime.UtcNow.Add(timeDifference); } }

        static CustomDateTime()
        {
            var ournow = DateTime.Now;
            var utcnow = TimeZoneInfo.ConvertTimeToUtc(ournow);

            timeDifference = ournow - utcnow;
        }

        private static readonly TimeSpan timeDifference;
    }
}

