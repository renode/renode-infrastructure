//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace Antmicro.Renode.UnitTests.Utilities
{
    public class ThreadSyncTester : IDisposable
    {
        public ThreadSyncTester()
        {
            errors = new List<string>();
            threads = new List<TestThread>();
            LocalThread = new TestThread("local");
        }

        public void Dispose()
        {
            foreach(var t in threads)
            {
                t.Dispose();
            }
            LocalThread.Dispose();
        }

        public void ReportError(string errorString)
        {
            errors.Add(errorString);
        }

        public TestThread ObtainThread(string name)
        {
            var t = new TestThread(name);
            threads.Add(t);
            return t;
        }

        public ExecutionResult Execute(TestThread t, Func<object> fun, string name = "unnamed operation")
        {
            var result = new ExecutionResult(this, name);
            var action = Tuple.Create(t, new DelayedAction(fun, result, name));
            action.Item2.ExecuteOn(action.Item1);
            LocalThread.Wait();
            return result;
        }

        public void Finish()
        {
            ExecutionFinished = true;
            foreach(var t in threads)
            {
                t.CheckException();
            }
            LocalThread.Finish();

            if(errors.Count > 0)
            {
                Assert.Fail("Got errors:\n" + string.Join("\n", errors));
            }
        }

        public bool ExecutionFinished { get; private set; }

        public TestThread LocalThread { get; private set; }

        private readonly List<TestThread> threads;
        private readonly List<string> errors;

        public class TestThread : IDisposable
        {
            public TestThread(string name)
            {
                Name = name;
                pump = new AutoResetEvent(false);
                report = new AutoResetEvent(false);
                underlyingThread = new System.Threading.Thread(ThreadBody)
                {
                    Name = name
                };
                underlyingThread.Start();
            }

            public void Dispose()
            {
            #if NET
                underlyingThread.Interrupt();
            #else
                underlyingThread.Abort();
            #endif
                underlyingThread.Join();
            }

            public bool Execute(Action a)
            {
                if(CaughtException != null)
                {
                    return false;
                }
                actionToRun = a;
                pump.Set();
                report.WaitOne();
                return CaughtException == null;
            }

            public void CheckException()
            {
                if(CaughtException != null)
                {
                    throw CaughtException;
                }
            }

            public void Finish()
            {
                Execute(null);
                underlyingThread.Join();
                CheckException();
            }

            public void Wait()
            {
                var mre = new ManualResetEvent(false);
                if(Execute(() => mre.Set()))
                {
                    mre.WaitOne();
                }
            }

            public string Name { get; private set; }

            public Exception CaughtException { get; private set; }

            private void ThreadBody()
            {
                try
                {
                    while(true)
                    {
                        pump.WaitOne();
                        var atr = actionToRun;
                        report.Set();
                        if(atr == null)
                        {
                            break;
                        }
                        atr();
                    }
                }
                catch(Exception e)
                {
                    // stop the thread on abort
                    CaughtException = e;
                }
                report.Set();
            }

            private Action actionToRun;
            private readonly System.Threading.Thread underlyingThread;
            private readonly AutoResetEvent pump;
            private readonly AutoResetEvent report;
        }

        public class DelayedAction
        {
            public DelayedAction(Func<object> a, ExecutionResult r, string name)
            {
                fun = a;
                executionResult = r;
                Name = name;
            }

            public void ExecuteOn(TestThread t)
            {
                t.Execute(() => {
                    executionResult.Result = fun();
                    executionResult.MarkAsFinished();
                });
            }

            public string Name { get; private set; }

            private readonly Func<object> fun;
            private readonly ExecutionResult executionResult;
        }

        public class ExecutionResult
        {
            public ExecutionResult(ThreadSyncTester tester, string name)
            {
                this.tester = tester;
                this.name = name;
                actionFinished = new ManualResetEvent(false);
            }

            public void MarkAsFinished()
            {
                actionFinished.Set();
            }

            public ExecutionResult ShouldFinish(object result = null)
            {
                tester.Execute(tester.LocalThread, () => {
                    if(!actionFinished.WaitOne(BlockingThreshold))
                    {
                        tester.ReportError($"Expected operation '{name}' to finish, but it looks like being stuck.");
                    }
                    if(result != null)
                    {
                        if(!result.Equals(Result))
                        {
                            tester.ReportError($"Expected {result} result of operation '{name}', but got {Result}");
                        }
                    }
                    return null;
                }, $"{name}: should finish");
                return this;
            }

            public ExecutionResult ShouldBlock()
            {
                tester.Execute(tester.LocalThread, () => {
                    if(actionFinished.WaitOne(BlockingThreshold))
                    {
                        tester.ReportError($"Expected operation '{name}' to block, but it finished with result: {Result}.");
                    }
                    return null;
                }, $"{name}: should block");
                return this;
            }

            public object Result
            {
                get; set;
            }

            private string name;
            private readonly ManualResetEvent actionFinished;
            private readonly ThreadSyncTester tester;

            private const int BlockingThreshold = 5000;
        }
    }
}