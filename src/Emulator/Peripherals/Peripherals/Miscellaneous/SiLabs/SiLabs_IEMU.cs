//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public interface SiLabs_IEmu
    {
        /// <summary>
        /// Method to add a hook when entering Deep Sleep.
        /// </summary>
        void AddEnterDeepSleepHook(Action hook);

        /// <summary>
        /// Method to add a hook when exiting Deep Sleep.
        /// </summary>
        void AddExitDeepSleepHook(Action hook);
    }
}