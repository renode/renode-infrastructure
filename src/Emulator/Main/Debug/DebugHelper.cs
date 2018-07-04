//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Debugging
{
    public class DebugHelper
    {
        [Conditional("DEBUG")]
        public static void Assert(bool condition,
            string message = "",
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if(!condition)
            {
                var formattedMessage = $"Assertion in {memberName} ({sourceFilePath}:{sourceLineNumber}) failed. {(string.IsNullOrEmpty(message) ? string.Empty : message)}";
                Logger.Log(null, LogLevel.Error, formattedMessage);
                throw new AssertionException(formattedMessage);
            }
        }
    }

    // in fact this should be more `EmulationException` as we cannot guarantee that we can recover from it in any way
    public class AssertionException : RecoverableException
    {
        public AssertionException(string message) : base(message)
        {
        }
    }
}
