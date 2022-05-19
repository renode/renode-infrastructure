//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
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
            exceptions.Add(dispatchInfo);
        }

        public void ThrowExceptions()
        {
            if(!exceptions.Any())
            {
                return;
            }

            try
            {
                if(exceptions.Count == 1)
                {
                    exceptions[0].Throw();
                }

                throw new Exception
                (
                    "Multiple errors occured within tlib managed->native->managed boundary since the last ThrowExceptions call.",
                    new AggregateException(exceptions.Select(x => x.SourceException))
                );
            }
            finally
            {
                exceptions.Clear();
            }
        }

        private readonly List<ExceptionDispatchInfo> exceptions = new List<ExceptionDispatchInfo>();
    }
}
