//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Peripherals.CPU;

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
        /// Method to add a hook when entering Deep Sleep (EM2, PD1 powered down).
        /// </summary>
        void AddEnterDeepSleepHook(Action hook);

        /// <summary>
        /// Method to add a hook when exiting Deep Sleep (EM2 to EM0 or EM1P, PD1 powered up).
        /// </summary>
        void AddExitDeepSleepHook(Action hook);

        /// <summary>
        /// Method to add a hook when entering EM1P (host in deep sleep, PD1 powered for an EM1P requester).
        /// </summary>
        void AddEnterEm1pHook(Action hook);

        /// <summary>
        /// Method to add a hook when exiting EM1P (to EM0 or EM2).
        /// </summary>
        void AddExitEm1pHook(Action hook);

        /// <summary>
        /// Method to register a radio sequencer core whose WFI state does not select the system energy mode.
        /// </summary>
        void RegisterRadioSequencerCpu(CortexM cpu);

        /// <summary>
        /// Method to assert or deassert an EM1P request on behalf of a requester.
        /// </summary>
        void SetEm1pRequest(object requester, bool request);

        /// <summary>
        /// Method to add a hook when the Host EM1P clock domain is gated (EM2, or EM1P with no
        /// host-clock request). The domain holds the bus, EUSART0, iADC, SYSRTC, LDMA, PRS, GPIO and VDAC.
        /// </summary>
        void AddEnterHostClockGateHook(Action hook);

        /// <summary>
        /// Method to add a hook when the Host EM1P clock domain starts running again.
        /// </summary>
        void AddExitHostClockGateHook(Action hook);

        /// <summary>
        /// Method to add a hook when the HostAll clock domain is gated (EM1P or EM2). The domain
        /// is the entire HCLK tree, which includes the general-purpose timers and USART.
        /// </summary>
        void AddEnterHostAllClockGateHook(Action hook);

        /// <summary>
        /// Method to add a hook when the HostAll clock domain starts running again (EM0 or EM1).
        /// </summary>
        void AddExitHostAllClockGateHook(Action hook);

        /// <summary>
        /// Method to assert or deassert an EM1P host-clock request. In EM1P the host clock
        /// domain runs only while at least one such request is set. EM0 and EM1 always run
        /// host clocks; EM2 keeps them gated because PD1 is powered down.
        /// </summary>
        void SetEm1pHostClockRequest(object requester, bool request);

        /// <summary>
        /// Method to mark a Q-channel peripheral as busy or ready. The EMU asks every Q-channel
        /// peripheral to quiesce before it powers PD1 down for EM2. While one is busy the EMU
        /// holds the host in sleep with PD1 powered, and it enters EM2 once all of them are ready.
        /// </summary>
        void SetQChannelBusy(object peripheral, bool busy);

        /// <summary>
        /// Current system energy mode.
        /// </summary>
        SiLabs_EnergyMode CurrentEnergyMode { get; }

        /// <summary>
        /// True while the Host EM1P clock domain is running.
        /// </summary>
        bool HostClocksEnabled { get; }

        /// <summary>
        /// True while the HostAll clock domain is running, which is only in EM0 and EM1.
        /// </summary>
        bool HostAllClocksEnabled { get; }

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
