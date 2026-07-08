//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.CPU
{
    public enum EmulationCPUState
    {
        /// <summary>
        /// CPU aborted (needs to transition to reset state to recover).
        /// </summary>
        Aborted,

        /// <summary>
        /// CPU is kept in reset (wasn't started yet).
        /// </summary>
        InReset,

        /// <summary>
        /// CPU was started after transition from reset state. CPU stays in Running state until transition to reset or abort state. It stays in Running state even when Paused or Halted.
        /// </summary>
        Running,
    }
}