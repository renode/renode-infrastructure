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
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    // Allows for the viewing of register contents when debugging
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public partial class EFR32xG2_PRS_3
    {
        public EFR32xG2_PRS_3(Machine machine, EFR32xG2_HFXO_3 hfxo, EFR32xG2_SYSRTC_1 sysrtc) : this(machine)
        {
            this.hfxo = hfxo;
            this.sysrtc = sysrtc;
        }

        partial void EFR32xG2_PRS_3_Constructor()
        {
            // Handles the consumption of an async signal by calling the OnAsyncSignalReceived function
            // with the index corresponding to the channel.
            asyncSignalHandlers = new Action[]
            {
                () => OnAsyncSignalReceived(0),
                () => OnAsyncSignalReceived(1),
                () => OnAsyncSignalReceived(2),
                // TODO: Could expand to encompass all 16 channels
            };
        }

        partial void Async_Ch_Ctrl_Write(ulong index, uint a, uint b)
        {
            // Parsing input
            uint sigsel = b & SIGSEL_MASK;
            uint sourcesel = (b >> SOURCESEL_OFFSET) & SOURCESEL_MASK;

            // Subscribing to an event according to the sourcesel and sigsel
            if (sourcesel == (uint)PRS_ASYNC_CH_CTRL_SOURCESEL.SYSRTC0)
            {
                if (sigsel == (uint)PRS_SYSRTC_SIGSEL.Group0Compare1)   
                {
                    // Unsubscribe from all events then subscribe to sysrtc group0 channel 1 compare event
                    this.Log(LogLevel.Info, "Subscribing to Sysrtc CompareMatchGroup0Channel1 event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate {
                        UnsubscribeFromAllEvents(index);
                        sysrtc.CompareMatchGroup0Channel1 += asyncSignalHandlers[index];
                    });
                }
            }

            if (sourcesel == (uint)PRS_ASYNC_CH_CTRL_SOURCESEL.HFXO0L) 
            {
                if (sigsel == (uint)PRS_HFXO_SIGSEL.ENS) 
                {
                    // Unsubscribe from all events then subscribe to hfxo enabled event
                    this.Log(LogLevel.Debug, "Subscribing to Hfxo HfxoEnabled event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate {
                        UnsubscribeFromAllEvents(index);
                        hfxo.HfxoEnabled += asyncSignalHandlers[index];
                    });
                }
            }
        }

        private void UnsubscribeFromAllEvents(ulong channel)
        {
            // Unsubscribing from all events prior to subscribing to the next one ensures that
            // a producer only writes to one channel. Additionally, it prevents the same channel from subscribing
            // to an event multiple times. 
            hfxo.HfxoEnabled -= asyncSignalHandlers[channel];
            sysrtc.CompareMatchGroup0Channel1 -= asyncSignalHandlers[channel];
        }

        private void OnAsyncSignalReceived(uint channel)
        {
            this.Log(LogLevel.Debug, "Async signal on channel {0}", channel);
            // Triggers all consumers which are subscribed to the specified channel
            if (consumer_hfxo0_oscreq_prssel_field.Value == channel)
            {
                HfxoTriggerEarlyWakeup();
            }

            if (consumer_sysrtc0_in0_prssel_field.Value == channel)
            {
                SysrtcCaptureGroup0();
            }

            // TODO: Add more conditional blocks as we get more events
        }

        partial void Async_swpulse_Ch3pulse_Write(bool a, bool b)
        {
            OnAsyncSignalReceived(3);
        }

        public void HfxoTriggerEarlyWakeup()
        {
            this.Log(LogLevel.Debug, "Triggering HFXO EM2 Wakeup");
            hfxo.OnEm2Wakeup();
        }

        public void SysrtcCaptureGroup0()
        {
            this.Log(LogLevel.Debug, "Triggering SYSRTC Capture");
            sysrtc.CaptureGroup0();
        }

        // A reference to sysrtc so that the prs can subscribe to events the sysrtc produces
        private readonly EFR32xG2_SYSRTC_1 sysrtc;

        // A reference to hfxo so that the prs can subscribe to events the hfxo produces
        private readonly EFR32xG2_HFXO_3 hfxo;

        private const uint SIGSEL_MASK = 0x7;
        private const int SOURCESEL_OFFSET = 8;
        private const uint SOURCESEL_MASK = 0x7F;

        // An array of methods which handle an async signal on a given channel
        private Action[] asyncSignalHandlers;  

        // From efr32mg24_prs_signals.h in gsdk
        private enum PRS_ASYNC_CH_CTRL_SOURCESEL
        {
            SYSRTC0 = 0xd,
            HFXO0L = 0xe,
            // Add other sources as required
        }

        // An enum to be used in place of ASYNC_CH_CTRL_SIGSEL (from EFR32xG2_PRS_3_gen.cs) for the Sigsel bitfields in
        // the async channel control registers when SYSRTC0 is selected as a source (PRS_ASYNC_CH_CTRL_SOURCESEL.SYSRTC0)
        private enum PRS_SYSRTC_SIGSEL
        {
            Group0Compare1 = 1,
            // Add other signals as required
        }

        // An enum to be used in place of ASYNC_CH_CTRL_SIGSEL for the Sigsel bitfields in the async channel control 
        // registers when HFXO0L is selected as a source (PRS_ASYNC_CH_CTRL_SOURCESEL.HFXO0L)
        private enum PRS_HFXO_SIGSEL
        {
            ENS = 1,
            // Add other signals as required
        }
    }
}