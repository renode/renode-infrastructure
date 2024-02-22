//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Time
{
    public enum TimeSourceState
    {
        /// <summary>
        /// Time source is idle -  it is currently not involved in handling virtual time flow.
        /// </summary>
        Idle,
        /// <summary>
        /// Synchronization hook is currently executed.
        /// </summary>
        /// <remark>
        /// All handles managed by this time source are in a safe state.
        /// </remark>
        ExecutingSyncHook,
        /// <summary>
        /// Delayed actions scheduled before the current virtual time are executed.
        /// </summary>
        /// <remark>
        /// All handles managed by this time source are in a safe state.
        /// </remark>
        ExecutingDelayedActions,
        /// <summary>
        /// Virtual time quantum is being granted to handles managed by this time source.
        /// </summary>
        ReportingElapsedTime,
        /// <summary>
        /// Time source is waiting until the slaves finish the execution of previously granted time quantum.
        /// </summary>
        WaitingForReportBack
    }
}
