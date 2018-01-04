//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using System.Linq;
using Antmicro.Renode.Logging.Backends;

namespace Antmicro.Renode.Logging.Lucene
{
    public class ViewFilter
    {
        public IEnumerable<LogLevel> LogLevels { get; set; }
        public IEnumerable<string> Sources { get; set; }
        public string CustomFilter { get; set; }

        public string GenerateQueryString()
        {
            var filters = new List<string>();
            if(LogLevels != null && LogLevels.Any())
            {
                var levelsString = String.Join(" OR ", LogLevels.Select(x => string.Format("{0}:{1}", LuceneLoggerBackend.TypeFiledName, x)));
                filters.Add(levelsString);
            }

            if(Sources != null && Sources.Any())
            {
                var sourcesString = String.Join(" OR ", Sources.Select(x => string.Format("{0}:{1}", LuceneLoggerBackend.SourceFieldName, x)));
                filters.Add(sourcesString);
            }

            if(!string.IsNullOrEmpty(CustomFilter))
            {
                filters.Add(CustomFilter);
            }

            return string.Join(" AND ", filters.Select(x => string.Format("({0})", x)));
        }
    }
}

