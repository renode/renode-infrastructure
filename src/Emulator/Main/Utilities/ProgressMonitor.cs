//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace Antmicro.Renode.Utilities
{
    public class ProgressMonitor
    {
        public MonitoredAction Start(string description, bool cancelable = false, bool progressable = false)
        {
            lock(locker)
            {
                var threadId = Thread.CurrentThread.ManagedThreadId;
                if(!runningActions.ContainsKey(threadId))
                {
                    runningActions.Add(threadId, new Stack<MonitoredAction>());
                }

                var actions = runningActions[threadId];
                var action = new MonitoredAction(this, description, cancelable, progressable); 
                actions.Push(action);

                UpdateDialog();

                return action;
            }
        }

        public void Stop()
        {
            lock(locker)
            {
                while (runningActions.Count > 0)
                {
                    var kvp = runningActions.First();
                    while(kvp.Value.Count > 0)
                    {
                        var a = kvp.Value.Pop();
                        a.Finish();
                    }
                }
            }
        }

        private void UpdateDialog()
        {
            lock(locker)
            {
                var h = Handler;
                var actionsToRemove = new List<int>();
                foreach(var action in runningActions)
                {
                    MonitoredAction current = null;
                    while(true)
                    {
                        current = action.Value.Count > 0 ? action.Value.Peek() : null;
                        if(current != null && current.IsFinished)
                        {
                            action.Value.Pop();
                            continue;
                        }
                        break;
                    }

                    if(current == null)
                    {
                        actionsToRemove.Add(action.Key);
                        if(h != null)
                        {
                            h.Finish(action.Key);
                        }
                        continue;
                    }

                    if(h != null)
                    {
                        h.Update(action.Key, current.Description, current.Progress);
                    }
                }

                foreach(var actionToRemove in actionsToRemove)
                {
                    runningActions.Remove(actionToRemove);
                }
            }
        }

        private Dictionary<int, Stack<MonitoredAction>> runningActions = new Dictionary<int, Stack<MonitoredAction>>();

        private object locker = new object();

        public IProgressMonitorHandler Handler { get; set; }

        public class MonitoredAction : IDisposable
        {
            public MonitoredAction(ProgressMonitor monitor, string description, bool cancelable, bool progressable)
            {
                this.monitor = monitor;
                Description = description;
                IsCancelable = cancelable;

                Progress = progressable ? (int?)0 : null;
            }

            public void UpdateProgress(int progress, string description = null)
            {
                Progress = progress;
                if(description != null)
                {
                    Description = description;
                }

                if(progress == 100)
                {
                    IsFinished = true;
                }

                monitor.UpdateDialog();
            }

            public void Finish()
            {
                IsFinished = true;
                monitor.UpdateDialog();
            }

            public void Dispose()
            {
                Finish();
            }

            public bool IsCancelable { get; private set; }
            public bool IsFinished { get; private set; }
            public int? Progress { get; private set; }
            public string Description { get; private set; }

            private ProgressMonitor monitor;
        }
    }
}

