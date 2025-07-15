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
using Antmicro.Renode.Peripherals.Wireless;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    // Allows for the viewing of register contents when debugging
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public partial class SiLabs_PRS_6
    {
        public SiLabs_PRS_6(IMachine machine, SiLabs_HFXO_3 hfxo, SiLabs_SYSRTC_1 sysrtc, SiLabs_xG24_LPW lpw) : base(machine)
        {
            this.hfxo = hfxo;
            this.sysrtc = sysrtc;
            this.lpw = lpw;

            Define_Registers();

            // Handles the consumption of an async signal by calling the OnAsyncSignalReceived function
            // with the index corresponding to the channel.
            asyncSignalHandlers = new Action[]
            {
                () => OnAsyncSignalReceived(0),
                () => OnAsyncSignalReceived(1),
                () => OnAsyncSignalReceived(2),
                () => OnAsyncSignalReceived(3),
                () => OnAsyncSignalReceived(4),
                () => OnAsyncSignalReceived(5),
                () => OnAsyncSignalReceived(6),
                () => OnAsyncSignalReceived(7),
                () => OnAsyncSignalReceived(8),
                () => OnAsyncSignalReceived(9),
                () => OnAsyncSignalReceived(10),
                () => OnAsyncSignalReceived(11),
                () => OnAsyncSignalReceived(12),
                () => OnAsyncSignalReceived(13),
                () => OnAsyncSignalReceived(14),
                () => OnAsyncSignalReceived(15),
            };
        }

        
        // Implementing declaration
        partial void Async_Ch_Ctrl_Write(ulong index, uint a, uint b)
        {
            // Parsing input
            uint sigsel = b & SIGSEL_MASK;
            uint sourcesel = (b >> SOURCESEL_OFFSET) & SOURCESEL_MASK;

            // Subscribing to an event according to the sourcesel and sigsel
            if (sourcesel == (uint)PRS_ASYNC_CH_CTRL_SOURCESEL.SYSRTC0)
            {
                if (sigsel == (uint)PRS_SYSRTC_SIGSEL.Group0Compare0)
                {
                    // Unsubscribe from all events then subscribe to sysrtc group0 channel 0 compare event
                    this.Log(LogLevel.Info, "Subscribing to Sysrtc CompareMatchGroup0Channel0 event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        sysrtc.CompareMatchGroup0Channel0 += asyncSignalHandlers[index];
                    });
                }

                if (sigsel == (uint)PRS_SYSRTC_SIGSEL.Group0Compare1)
                {
                    // Unsubscribe from all events then subscribe to sysrtc group0 channel 1 compare event
                    this.Log(LogLevel.Debug, "Subscribing to Sysrtc CompareMatchGroup0Channel1 event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        sysrtc.CompareMatchGroup0Channel1 += asyncSignalHandlers[index];
                    });
                }

                if (sigsel == (uint)PRS_SYSRTC_SIGSEL.Group1Compare0)
                {
                    // Unsubscribe from all events then subscribe to sysrtc group0 channel 0 compare event
                    this.Log(LogLevel.Debug, "Subscribing to Sysrtc CompareMatchGroup1Channel0 event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        sysrtc.CompareMatchGroup1Channel0 += asyncSignalHandlers[index];
                    });
                }

                if (sigsel == (uint)PRS_SYSRTC_SIGSEL.Group1Compare1)   
                {
                    // Unsubscribe from all events then subscribe to sysrtc group0 channel 0 compare event
                    this.Log(LogLevel.Debug, "Subscribing to Sysrtc CompareMatchGroup1Channel1 event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate {
                        UnsubscribeFromAllEvents(index);
                        sysrtc.CompareMatchGroup1Channel1 += asyncSignalHandlers[index];
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
            sysrtc.CompareMatchGroup0Channel0 -= asyncSignalHandlers[channel];
            sysrtc.CompareMatchGroup0Channel1 -= asyncSignalHandlers[channel];
            sysrtc.CompareMatchGroup1Channel0 -= asyncSignalHandlers[channel];
            sysrtc.CompareMatchGroup1Channel1 -= asyncSignalHandlers[channel];
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

            if (consumer_sysrtc0_in1_prssel_field.Value == channel)
            {
                SysrtcCaptureGroup1();
            }

            if (consumer_protimer_rtcctrigger_prssel_field.Value == channel)
            {
                ProtimerRtccTrigger();
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

        public void SysrtcCaptureGroup1()
        {
            this.Log(LogLevel.Debug, "Triggering SYSRTC Capture");
            sysrtc.CaptureGroup1();
        }

        public void ProtimerRtccTrigger()
        {
            this.Log(LogLevel.Debug, "Triggering PROTIMER RTCC Trigger");
            lpw.PROTIMER_RtcTrigger();
        }
        
        // A reference to sysrtc so that the prs can subscribe to events the sysrtc produces
        private readonly SiLabs_SYSRTC_1 sysrtc;

        // A reference to hfxo so that the prs can subscribe to events the hfxo produces
        private readonly SiLabs_HFXO_3 hfxo;

        // A reference to the LPW so that the prs can subscribe to events the hfxo produces and 
        // call methods to dispatch events
        private readonly SiLabs_xG24_LPW lpw;

        private const uint SIGSEL_MASK = 0x7;
        private const int SOURCESEL_OFFSET = 8;
        private const uint SOURCESEL_MASK = 0x7F;

        // An array of methods which handle an async signal on a given channel
        private Action[] asyncSignalHandlers;  

        // From efr32mg26_prs_signals.h in gsdk
        private enum PRS_ASYNC_CH_CTRL_SOURCESEL
        {
            SYSRTC0 = 0xD,
            HFXO0L = 0xE,
            // Add other sources as required
        }

        // An enum to be used in place of ASYNC_CH_CTRL_SIGSEL (from PRS_6_gen.cs) for the Sigsel bitfields in
        // the async channel control registers when SYSRTC0 is selected as a source (PRS_ASYNC_CH_CTRL_SOURCESEL.SYSRTC0)
        private enum PRS_SYSRTC_SIGSEL
        {
            Group0Compare0 = 0,
            Group0Compare1 = 1,
            Group1Compare0 = 2,
            Group1Compare1 = 3,
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