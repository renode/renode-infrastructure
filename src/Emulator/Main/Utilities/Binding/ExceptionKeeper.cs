//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Antmicro.Renode.Utilities.Binding
{
    public class ExceptionKeeper
    {
        public void AddException(Exception e)
        {
            // ExceptionKeeper holds the raised exception and throws it at a later time
            // so we use ExceptionDispatchInfo to preserve the original stacktrace.
            // Otherwise exception's stacktrace changes when it's rethrown.
            var dispatchInfo = ExceptionDispatchInfo.Capture(e);
            exceptions.Value.Add(dispatchInfo);
        }

        public void ThrowExceptions()
        {
            if(!exceptions.Value.Any())
            {
                return;
            }

            try
            {
                if(exceptions.Value.Count == 1)
                {
                    exceptions.Value[0].Throw();
                }

                throw new Exception
                (
                    "Multiple errors occured within tlib managed->native->managed boundary since the last ThrowExceptions call.",
                    new AggregateException(exceptions.Value.Select(x => x.SourceException))
                );
            }
            finally
            {
                exceptions.Value.Clear();
            }
        }

        public void PrintExceptions()
        {
            foreach(var exception in exceptions.Value)
            {
                Console.WriteLine(exception.SourceException);
            }
        }

        private readonly ThreadLocal<List<ExceptionDispatchInfo>> exceptions = new ThreadLocal<List<ExceptionDispatchInfo>>(() => new List<ExceptionDispatchInfo>());
    }
}
