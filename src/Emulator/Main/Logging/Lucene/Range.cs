//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging.Backends;

namespace Antmicro.Renode.Logging.Lucene
{
    public class Range
    {
        public ulong? MinimalId { get; set; }
        public ulong? MaximalId { get; set; }

        public string GenerateQueryString()
        {
            return string.Format("{0}:[{1} TO {2}]", LuceneLoggerBackend.IdFieldName, MinimalId ?? 0, MaximalId ?? ulong.MaxValue);
        }
    }
}

