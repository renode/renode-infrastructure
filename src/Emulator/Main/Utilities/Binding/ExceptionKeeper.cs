//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities.Binding
{
    public class ExceptionKeeper
    {
        public void AddException(Exception e)
        {
            exceptions.Add(e);
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
                    throw exceptions[0];
                }

                throw new Exception
                (
                    "Multiple errors occured within tlib managed->native->managed boundary since the last ThrowExceptions call.",
                    new AggregateException(exceptions)
                );
            }
            finally
            {
                exceptions.Clear();
            }
        }

        private readonly List<Exception> exceptions = new List<Exception>();
    }
}
