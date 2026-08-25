//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

namespace Antmicro.Renode.Exceptions
{
    public static class ExceptionExtensions
    {
        public static bool IsExceptionRecoverable(this Exception e, bool allowAggregate = false)
        {
            if(allowAggregate && e is AggregateException aggregateException)
            {
                return aggregateException.InnerExceptions.All(e => IsExceptionRecoverable(e, true));
            }

            return e is RecoverableException;
        }
    }
}
