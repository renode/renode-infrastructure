//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{ 
    public enum HFXO_REQUESTER
    {
        NONE = 0,
        PRS = 1,
        FORCEEN = 2,
        HWREQ = 3,
        SYSRTC = 4,
    }

    public interface IHFXO_EFR32xG2
    {
        /// <summary>
        /// Event which is invoked upon enabling the HFXO (Setting ENS in the status register).
        /// </summary>
        event Action HfxoEnabled;

        /// <summary>
        /// Method which is called by the CMU (or potentially other infrastructure peripherals) when the HFXO is selected
        /// by the CMU as a clock source so that the HFXO updates its internal states.
        /// </summary>
        void OnClksel();

        /// <summary>
        /// Method which is called by the PRS when HFXO is meant to receive a signal from the PRS to wake up.
        /// </summary>
        void OnEm2Wakeup();

        /// <summary>
        /// Method which is called when the HFXO is requested by an infrastructure peripheral. The argument is the requester.
        /// </summary>
        void OnRequest(HFXO_REQUESTER a);
    }
}