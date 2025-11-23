//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.IO;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Utilities
{
    public class AutoSnapshotCreator
    {
        public AutoSnapshotCreator()
        {
            actionIdLock = new object();
        }

        public void StartTakingSnapshots(string outputDirectory, TimeInterval period)
        {
            this.outputDirectory = outputDirectory;
            this.period = period;

            TakeSnapshot(new TimeStamp(EmulationManager.Instance.CurrentEmulation.MasterTimeSource.ElapsedVirtualTime, EmulationManager.Instance.CurrentEmulation.MasterTimeSource.Domain));
        }

        public void DisableSnapshotCreator()
        {
            if(actionId == null)
            {
                return;
            }

            lock(actionIdLock)
            {
                UnregisterSnapshotAction();
            }
        }

        public TimeInterval Period => period;

        private void UnregisterSnapshotAction()
        {
            if(!EmulationManager.Instance.CurrentEmulation.MasterTimeSource.CancelActionToExecuteInSyncedState(actionId.Value))
            {
                Logger.Log(LogLevel.Warning, "Cancellation of registered snapshot didn't work!");
            }

            actionId = null;
        }

        private void TakeSnapshot(TimeStamp timeStamp)
        {
            lock(actionIdLock)
            {
                if(outputDirectory == null)
                {
                    Logger.Log(LogLevel.Error, "Disabling auto-save due to OutputDirectory being null");
                    UnregisterSnapshotAction();
                    return;
                }

                var masterTimeSource = EmulationManager.Instance.CurrentEmulation.MasterTimeSource;

                // next snapshot is registered to be taken in time equal to Period from current timestamp
                actionId = masterTimeSource.ExecuteInSyncedState(TakeSnapshot, new TimeStamp(timeStamp.TimeElapsed + period, masterTimeSource.Domain));

                var filePath = Path.Combine(outputDirectory, EmulationManager.EmulationEpoch + "snap" + timeStamp.TimeElapsed.Ticks);
                try
                {
                    EmulationManager.Instance.Save(filePath);
                }
                catch(RecoverableException e)
                {
                    UnregisterSnapshotAction();
                    Logger.Log(LogLevel.Error, "Disabling auto-save due to exception thrown during snapshot creation: {0}", e.Message);
                }
            }
        }

        private TimeInterval period;
        private string outputDirectory;
        private ulong? actionId;
        private readonly object actionIdLock;
    }
}