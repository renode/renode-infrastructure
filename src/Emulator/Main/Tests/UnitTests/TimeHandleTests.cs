//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Threading;
using Antmicro.Renode.Time;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class TimeHandleTests
    {
        [SetUp]
        public void SetUp()
        {
            tsource = new MockTimeSource();
            var mockSink = new MockTimeSink();
            handle = new TimeHandle(tsource, mockSink);
            mockSink.TimeHandle = handle;
            tester = new ThreadSyncTester();

            sourceThread = tester.ObtainThread("source");
            sinkThread = tester.ObtainThread("sink");
            externalThread = tester.ObtainThread("external");

            interval = TimeInterval.FromTicks(1000);

            SetSourceSideActiveOnExternal(true);
        }

        [TearDown]
        public void TearDown()
        {
            Assert.IsTrue(tester.ExecutionFinished, "Did you forget to call `Finish`?");
            tester.Dispose();
        }

        [Test]
        public void WaitShouldNotBlockIfThereWasNoRequest()
        {
            GrantOnSource();
            WaitOnSource().ShouldFinish();
            // we expect 'WaitUntilDone' to finish with false if sink has not interacted with the handle yet
            ShouldWaitReturn(false, false, TimeInterval.Empty);
            Finish();
        }

        [Test]
        public void RequestShouldBlockWaitingForGrant()
        {
            var r = RequestOnSink().ShouldBlock();
            GrantOnSource();
            r.ShouldFinish();
            ShouldRequestReturn(true, interval);
            Finish();
        }

        [Test]
        public void WaitShouldBlockWaitingForReport()
        {
            var r = RequestOnSink().ShouldBlock();
            GrantOnSource();
            r.ShouldFinish();
            ShouldRequestReturn(true, interval);
            WaitOnSource().ShouldBlock();
            Finish();
        }

        [Test]
        public void  WaitShouldBlockWaitingForReport2()
        {
            GrantOnSource();
            RequestOnSink().ShouldFinish();
            ShouldRequestReturn(true, interval);
            WaitOnSource().ShouldBlock();
            Finish();
        }

        [Test]
        public void ShouldCallUnblockHandleAsResultOfRequestingTimeIntervalOnBlockedHandle()
        {
            RequestOnSink();
            GrantOnSource();
            WaitOnSource();
            BreakOnSink();
            Assert.AreEqual(0, tsource.UnblockCounter);
            RequestOnSink().ShouldFinish();
            Assert.AreEqual(1, tsource.UnblockCounter);
            Finish();
        }

        [Test]
        public void ShouldNotCallUnblockHandleAsResultOfRequestingTimeIntervalOnUnblockedHandle()
        {
            GrantOnSource();
            RequestOnSink();
            ContinueOnSink();
            WaitOnSource();
            GrantOnSource();
            Assert.AreEqual(0, tsource.UnblockCounter);
            RequestOnSink().ShouldFinish();
            Assert.AreEqual(0, tsource.UnblockCounter);
            Finish();
        }

        [Test]
        public void HandleShouldNotBeReadyForNewTimeGrantAfterDispose()
        {
            DisposeOnExternal();
            Assert.IsFalse(handle.IsReadyForNewTimeGrant);
            Finish();
        }

#if DEBUG
// those tests assume DebugHelper.Assert to throw an exception; asserts are supported in debug mode only, so it would fail in non-debug mode

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToGrantTimeAfterDispose()
        {
            DisposeOnExternal();
            GrantOnSource();
            Finish();
        }

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToGrantTwiceInARow()
        {
            GrantOnSource();
            GrantOnSource();
            Finish();
        }

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToWaitBeforeGranting()
        {
            WaitOnSource().ShouldBlock(); // this should block here is a hack not to finish this test too soon
            Finish();
        }

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToGrantTimeToBlockedHandle()
        {
            GrantOnSource();
            RequestOnSink();
            BreakOnSink();
            WaitOnSource();
            GrantOnSource().ShouldBlock(); // this is illegal; sink must call UnblockHandle (as a result of RequestTimeInterval) first!
            Finish();
        }

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToCallWaitTwiceAfterReportingContinue()
        {
            GrantOnSource();
            RequestOnSink();
            ContinueOnSink();
            WaitOnSource();
            WaitOnSource().ShouldBlock(); // this should block here is a hack not to finish this test too soon
            Finish();
        }

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToCallGrantAfterReportingBreak()
        {
            GrantOnSource();
            RequestOnSink();
            BreakOnSink();
            WaitOnSource();
            GrantOnSource();
            Finish();
        }

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToRequestTwiceInARow()
        {
            GrantOnSource();
            RequestOnSink();
            RequestOnSink().ShouldBlock(); // this should block here is a hack not to finish this test too soon
            Finish();
        }

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToReportContinueBeforeRequesting()
        {
            ContinueOnSink();
            Finish();
        }

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToReportBreakBeforeRequesting()
        {
            BreakOnSink();
            Finish();
        }

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToReportBreakTwiceInARow()
        {
            GrantOnSource();
            RequestOnSink();
            BreakOnSink();
            BreakOnSink();
            Finish();
        }

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToReportContinueTwiceInARow()
        {
            GrantOnSource();
            RequestOnSink();
            ContinueOnSink();
            ContinueOnSink();
            Finish();
        }

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToReportContinueAfterBreak()
        {
            GrantOnSource();
            RequestOnSink();
            BreakOnSink();
            ContinueOnSink();
            Finish();
        }

        [Test, ExpectedException(typeof(Antmicro.Renode.Debugging.AssertionException))]
        public void ShouldNotAllowToReportBreakAfterContinue()
        {
            GrantOnSource();
            RequestOnSink();
            ContinueOnSink();
            BreakOnSink();
            Finish();
        }
#endif

        [Test]
        public void RequestShouldReturnDifferentValuesDependingOnSideEnabledState2()
        {
            var r = RequestOnSink().ShouldBlock();
            SetSourceSideActiveOnExternal(false);
            r.ShouldFinish();
            RequestOnSink().ShouldFinish(Tuple.Create(false, TimeInterval.Empty));
            SetSourceSideActiveOnExternal(true);
            r = RequestOnSink().ShouldBlock();
            GrantOnSource();
            r.ShouldFinish();
            WaitOnSource().ShouldBlock();
            Finish();

            ShouldRequestReturn(true, interval);
        }

        [Test]
        public void GratingShouldFinishRequestAfterReEnablingSourceSide()
        {
            SetSourceSideActiveOnExternal(false);
            SetSourceSideActiveOnExternal(true);
            var r = RequestOnSink().ShouldBlock();
            GrantOnSource();
            r.ShouldFinish();
            Finish();

            ShouldRequestReturn(true, interval);
        }

        [Test]
        public void RequestShouldReturnDifferentValuesDependingOnSideEnabledState()
        {
            var r = RequestOnSink().ShouldBlock();
            GrantOnSource();
            r.ShouldFinish();
            BreakOnSink();
            WaitOnSource().ShouldFinish();
            SetSourceSideActiveOnExternal(false);
            RequestOnSink().ShouldFinish(Tuple.Create(false, TimeInterval.Empty));
            SetSourceSideActiveOnExternal(true);
            RequestOnSink().ShouldFinish();
            Finish();

            ShouldRequestReturn(true, TimeInterval.Empty);
        }

        [Test]
        public void DisablingHanldeSinkSideShouldFinishRequest()
        {
            var r = RequestOnSink().ShouldBlock();
            SetEnabledOnExternal(false).ShouldFinish();
            r.ShouldFinish();
            Finish();

            ShouldRequestReturn(false, TimeInterval.Empty);
        }

        [Test]
        public void DisablingHanldeSourceSideShouldFinishRequest()
        {
            var r = RequestOnSink().ShouldBlock();
            SetSourceSideActiveOnExternal(false);
            r.ShouldFinish();
            Finish();

            ShouldRequestReturn(false, TimeInterval.Empty);
        }

        [Test]
        public void RequestShouldNotBlockOnDisabledHandle()
        {
            SetEnabledOnExternal(false).ShouldFinish();
            RequestOnSink().ShouldFinish();
            Finish();

            ShouldRequestReturn(false, TimeInterval.Empty);
        }

        [Test]
        public void BreakingShouldFinishWaiting3()
        {
            GrantOnSource();
            WaitOnSource().ShouldFinish(Tuple.Create(false, false, TimeInterval.Empty));
            RequestOnSink().ShouldFinish();
            var w = WaitOnSource().ShouldBlock();
            BreakOnSink();
            w.ShouldFinish();
            Finish();

            ShouldRequestReturn(true, interval);
            ShouldWaitReturn(false, false, interval);
        }

        [Test]
        public void BreakingShouldFinishWaiting2()
        {
            var r = RequestOnSink().ShouldBlock();
            GrantOnSource();
            r.ShouldFinish();
            var w = WaitOnSource().ShouldBlock();
            BreakOnSink();
            w.ShouldFinish();
            Finish();

            ShouldRequestReturn(true, interval);
            ShouldWaitReturn(false, false, interval);
        }

        [Test]
        public void BreakingShouldFinishWaiting()
        {
            GrantOnSource();
            RequestOnSink().ShouldFinish();
            var w = WaitOnSource().ShouldBlock();
            BreakOnSink();
            w.ShouldFinish();
            Finish();

            ShouldRequestReturn(true, interval);
            ShouldWaitReturn(false, false, interval);
        }

        [Test]
        public void ContinueShouldFinishWaiting()
        {
            GrantOnSource();
            WaitOnSource().ShouldFinish(Tuple.Create(false, false, TimeInterval.Empty));
            RequestOnSink().ShouldFinish();
            var w = WaitOnSource().ShouldBlock();
            ContinueOnSink();
            w.ShouldFinish();
            Finish();

            ShouldRequestReturn(true, interval);
            ShouldWaitReturn(true, false, interval);
        }

        [Test]
        public void ContinueShouldFinishWaiting2()
        {
            var r = RequestOnSink().ShouldBlock();
            GrantOnSource();
            r.ShouldFinish();
            var w = WaitOnSource().ShouldBlock();
            ContinueOnSink();
            w.ShouldFinish();
            Finish();

            ShouldRequestReturn(true, interval);
            ShouldWaitReturn(true, false, interval);
        }

        [Test]
        public void ContinueShouldFinishWaiting3()
        {
            GrantOnSource();
            RequestOnSink().ShouldFinish();
            var w = WaitOnSource().ShouldBlock();
            ContinueOnSink();
            w.ShouldFinish();
            Finish();

            ShouldRequestReturn(true, interval);
            ShouldWaitReturn(true, false, interval);
        }

        [Test]
        public void WaitShouldNotBlockAfterContinue()
        {
            GrantOnSource();
            RequestOnSink().ShouldFinish();
            ContinueOnSink();
            WaitOnSource().ShouldFinish();
            Finish();

            ShouldRequestReturn(true, interval);
            ShouldWaitReturn(true, false, interval);
        }

        [Test]
        public void WaitShouldNotBlockAfterContinue2()
        {
            var r = RequestOnSink().ShouldBlock();
            GrantOnSource();
            r.ShouldFinish();
            ContinueOnSink();
            WaitOnSource().ShouldFinish();
            Finish();

            ShouldRequestReturn(true, interval);
            ShouldWaitReturn(true, false, interval);
        }

        [Test]
        public void WaitShouldNotBlockAfterBreak()
        {
            GrantOnSource();
            RequestOnSink().ShouldFinish();
            BreakOnSink();
            WaitOnSource().ShouldFinish();
            Finish();

            ShouldRequestReturn(true, interval);
            ShouldWaitReturn(false, false, interval);
        }

        [Test]
        public void WaitShouldNotBlockAfterBreak2()
        {
            var r = RequestOnSink().ShouldBlock();
            GrantOnSource();
            r.ShouldFinish();
            BreakOnSink();
            WaitOnSource().ShouldFinish();
            Finish();

            ShouldRequestReturn(true, interval);
            ShouldWaitReturn(false, false, interval);
        }

        [Test]
        public void WaitShouldNotBlockOnDisabledHandle()
        {
            var r = RequestOnSink().ShouldBlock();
            SetEnabledOnExternal(false).ShouldFinish();
            r.ShouldFinish();
            GrantOnSource();
            WaitOnSource().ShouldFinish();
            Finish();

            ShouldRequestReturn(false, TimeInterval.Empty);
            ShouldWaitReturn(true, false, interval);
        }

        [Test]
        public void RequestSholdNotBlockAfterGrantOnReEnabledHandle()
        {
            SetSourceSideActiveOnExternal(false);
            SetSourceSideActiveOnExternal(true);
            GrantOnSource();
            RequestOnSink().ShouldFinish();
            Finish();

            ShouldRequestReturn(true, interval);
        }

        [Test]
        public void FirstRequestAfterBreakShouldNotBlock()
        {
            var r = RequestOnSink().ShouldBlock();
            GrantOnSource();
            r.ShouldFinish(Tuple.Create(true, interval));
            BreakOnSink();
            WaitOnSource().ShouldFinish(Tuple.Create(false, false, interval));
            RequestOnSink().ShouldFinish();
            Finish();

            ShouldRequestReturn(true, TimeInterval.Empty);
        }

        [Test]
        public void SecondWaitShouldReturnEmptyIntervalOnBlockingHandle()
        {
            GrantOnSource();
            RequestOnSink().ShouldFinish();
            BreakOnSink();
            WaitOnSource().ShouldFinish(Tuple.Create(false, false, interval));
            WaitOnSource().ShouldFinish(Tuple.Create(false, false, TimeInterval.Empty));
            Finish();
        }

        [Test]
        public void WaitShouldNotBlockAfterGrantingOnReEnabledHandleS()
        {
            SetEnabledOnExternal(false).ShouldFinish();
            SetEnabledOnExternal(true).ShouldFinish();
            GrantOnSource();
            WaitOnSource().ShouldFinish();
            Finish();

            ShouldWaitReturn(false, false, TimeInterval.Empty);
        }

        [Test]
        public void DisableSinkSideShouldBlockOnLatchedHandle()
        {
            var r = RequestOnSink().ShouldBlock();
            SetSourceSideActiveOnExternal(false);
            r.ShouldFinish();
            SetSourceSideActiveOnExternal(true);

            SetEnabledOnExternal(false).ShouldFinish();
            LatchOnSource();
            GrantOnSource();
            var s = SetEnabledOnExternal(true).ShouldBlock();
            WaitOnSource().ShouldFinish();
            UnlatchOnSource();
            s.ShouldFinish();
            Finish();

            ShouldWaitReturn(true, false, interval);
        }

        private ThreadSyncTester.ExecutionResult GrantOnSource()
        {
            // granting should never block
            return tester.Execute(sourceThread, () => { handle.GrantTimeInterval(interval); return null; }, "Grant").ShouldFinish();
        }

        private ThreadSyncTester.ExecutionResult WaitOnSource()
        {
            // it may block or not
            waitResult = tester.Execute(sourceThread, () => { var r = handle.WaitUntilDone(out var ti); return Tuple.Create(r.IsDone, r.IsUnblockedRecently, ti); }, "Wait");
            return waitResult;
        }

        private ThreadSyncTester.ExecutionResult RequestOnSink()
        {
            // it may block or not
            requestResult = tester.Execute(sinkThread, () => { var r = handle.RequestTimeInterval(out var i); return Tuple.Create(r, i);}, "Request");
            return requestResult;
        }

        private ThreadSyncTester.ExecutionResult DisposeOnExternal()
        {
            // dispose should never block
            return tester.Execute(externalThread, () => { handle.Dispose(); return null; }, "Dispose").ShouldFinish();
        }

        private ThreadSyncTester.ExecutionResult SetSourceSideActiveOnExternal(bool value)
        {
            // this should never block
            return tester.Execute(externalThread, () => { handle.SourceSideActive = value; return null; }, "SourceSideActive").ShouldFinish();
        }

        private ThreadSyncTester.ExecutionResult SetEnabledOnExternal(bool value)
        {
            // this one can block!
            return tester.Execute(externalThread, (Func<object>)(() => { handle.Enabled = value; return null; }), "Enabled");
        }

        private ThreadSyncTester.ExecutionResult BreakOnSink(TimeInterval? i = null)
        {
            // this should never block
            return tester.Execute(sinkThread, () => { handle.ReportBackAndBreak(i ?? TimeInterval.Empty); return null; }, "Break").ShouldFinish();
        }

        private ThreadSyncTester.ExecutionResult ContinueOnSink(TimeInterval? i = null)
        {
            // this should never block
            return tester.Execute(sinkThread, () => { handle.ReportBackAndContinue(i ?? TimeInterval.Empty); return null; }, "Continue").ShouldFinish();
        }

        private ThreadSyncTester.ExecutionResult LatchOnSource()
        {
            // this should never block
            return tester.Execute(sourceThread, () => { handle.Latch(); return null; }, "Latch").ShouldFinish();
        }

        private ThreadSyncTester.ExecutionResult UnlatchOnSource()
        {
            // this should never block
            return tester.Execute(sourceThread, () => { handle.Unlatch(); return null; }, "Unlatch").ShouldFinish();
        }

        private void ShouldWaitReturn(bool result, bool isActivatedRecently, TimeInterval interval)
        {
            Assert.AreEqual(Tuple.Create(result, isActivatedRecently, interval), waitResult.Result, "wait result");
        }

        private void ShouldRequestReturn(bool result, TimeInterval interval)
        {
            Assert.AreEqual(Tuple.Create(result, interval), requestResult.Result, "request result");
        }

        private void Finish()
        {
            tester.Finish();
        }

        private MockTimeSource tsource;
        private ThreadSyncTester tester;
        private TimeHandle handle;
        private ThreadSyncTester.TestThread sourceThread;
        private ThreadSyncTester.TestThread sinkThread;
        private ThreadSyncTester.TestThread externalThread;
        private TimeInterval interval;
        private ThreadSyncTester.ExecutionResult waitResult;
        private ThreadSyncTester.ExecutionResult requestResult;

        private class MockTimeSource : ITimeSource, ITimeDomain
        {
            public TimeInterval Quantum { get; set; }

            public ITimeDomain Domain => this;

            public TimeInterval NearestSyncPoint => ElapsedVirtualTime + Quantum;

            public TimeInterval ElapsedVirtualTime { get; set; }

            public void RegisterSink(ITimeSink sink)
            {
                throw new NotImplementedException();
            }

            public bool UnblockHandle(TimeHandle h)
            {
                UnblockCounter++;
                return true;
            }

            public void ReportHandleActive()
            {
            }

            public void ReportTimeProgress(TimeHandle h, TimeInterval diff)
            {
            }

            public int UnblockCounter { get; private set; }
        }

        private class ThreadSyncTester : IDisposable
        {
            public ThreadSyncTester()
            {
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

            public TestThread ObtainThread(string name)
            {
                var t = new TestThread(name);
                threads.Add(t);
                return t;
            }

            public ExecutionResult Execute(TestThread t, Func<object> fun, string name)
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
            }

            public bool ExecutionFinished { get; private set; }

            public TestThread LocalThread { get; private set; }

            private readonly List<TestThread> threads;

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
                    underlyingThread.Abort();
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
                            Assert.Fail($"Expected operation '{name}' to finish, but it looks like being stuck.");
                        }
                        if(result != null)
                        {
                            Assert.AreEqual(result, Result, $"Expected another result of operation '{name}'");
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
                            Assert.Fail($"Expected operation '{name}' to block, but it finished with result: {Result}.");
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

        private class MockTimeSink : ITimeSink
        {
            public TimeHandle TimeHandle { get; set; }
        }
    }
}