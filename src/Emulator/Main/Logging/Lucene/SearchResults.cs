//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

namespace Antmicro.Renode.Logging.Lucene
{
    public struct SearchResults
    {
        public SearchResults(int totalHits, IEnumerable<LogEntry> entries) : this()
        {
            TotalHits = totalHits;
            Entries = entries;
        }

        public ulong FoundId { get; set; }
        public int TotalHits { get; private set; }
        public IEnumerable<LogEntry> Entries { get; private set; }
    }
}

