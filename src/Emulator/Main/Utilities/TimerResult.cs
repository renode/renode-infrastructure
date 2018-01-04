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
    public class TimerResult
    {
        public override string ToString()
        {
            return string.Format("{2} timer check at {0}, elapsed {1}{3}.", Timestamp.ToLongTimeString(), FromBeginning.ToString(), SequenceNumber.ToOrdinal(), EventName == null ? "" : " " + EventName);
        }

        public int SequenceNumber { get; set; }

        public TimeSpan FromBeginning { get; set; }

        public DateTime Timestamp { get; set; }

        public String EventName { get;set; }
    }
}