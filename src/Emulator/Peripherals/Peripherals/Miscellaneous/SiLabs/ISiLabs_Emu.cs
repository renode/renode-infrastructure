//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public interface ISiLabs_Emu
    {
        /// <summary>
        /// Method to add a hook when entering Sleep.
        /// </summary>
        void AddEnterSleepHook(Action hook);

        /// <summary>
        /// Method to add a hook when exiting Sleep.
        /// </summary>
        void AddExitSleepHook(Action hook);

        /// <summary>
        /// Method to add a hook when entering Deep Sleep.
        /// </summary>
        void AddEnterDeepSleepHook(Action hook);

        /// <summary>
        /// Method to add a hook when exiting Deep Sleep.
        /// </summary>
        void AddExitDeepSleepHook(Action hook);

        /// <summary>
        /// Method to update the enabled state of a peripheral.
        /// </summary>
        void UpdatePeripheralEnabledStatus(IPeripheral peripheral, bool enabled);

        /// <summary>
        /// Method to get a list of all no retain peripherals.
        /// </summary>
        List<IPeripheral> GetNoRetainPeripherals();
    }
}
