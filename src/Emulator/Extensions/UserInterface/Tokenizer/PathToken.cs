//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Migrant;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class PathToken : StringToken
    {
        public PathToken(string path) : base(path)
        {
            Path = path.TrimStart('@').Replace(@"\ ", " ");
        }

        public override object GetObjectValue()
        {
            return Path ?? OriginalValue;
        }

        public void Update(CachingFileFetcher fetcher)
        {
            var fname = OriginalValue.TrimStart('@').Replace(@"\ ", " ");
            try
            {
                var uri = new Uri(fname);
                if(uri.IsFile)
                {
                    throw new UriFormatException();
                }

                var success = fetcher.TryFetchFromUri(uri, out var path);
                if(!success)
                {
                    throw new UriFormatException();
                }

                Path = path;
            }
            catch(UriFormatException)
            {
                // Not a proper uri, so probably a nonexisting local path
                Path = fname;
            }
        }

        public override string ToString()
        {
            return $"[PathToken: Path={OriginalValue}]";
        }

        [field: Transient]
        public string Path { get; protected set; }
    }
}
