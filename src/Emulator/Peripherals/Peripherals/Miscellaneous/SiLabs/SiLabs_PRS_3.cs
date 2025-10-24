//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2022-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.Wireless;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    // Allows for the viewing of register contents when debugging
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public partial class SiLabs_PRS_3
    {
        public SiLabs_PRS_3(Machine machine, SiLabs_HFXO_3 hfxo, SiLabs_SYSRTC_1 sysrtc, SiLabs_xG24_LPW lpw) : this(machine)
        {
            this.hfxo = hfxo;
            this.sysrtc = sysrtc;
            this.lpw = lpw;
        }

        partial void SiLabs_PRS_3_Constructor()
        {
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

        partial void PRS_Reset()
        {
            registers.Reset();
        }

        partial void Async_Ch_Ctrl_Write(ulong index, uint a, uint b)
        {
            // Parsing input
            uint sigsel = b & SignalSelectMask;
            var sourcesel = (AsyncProducerSourceSelect)((b >> SourceSelectOffset) & SourceSelectMask);

            this.Log(LogLevel.Debug, "ASYNC CH-{0} CTRL WRITE: sourcesel {1} sigsel 0x{2:X}", index, sourcesel, sigsel);

            // Subscribing to an event according to the sourcesel and sigsel
            if(sourcesel == AsyncProducerSourceSelect.Sysrtc0)
            {
                if(sigsel == (uint)SysrtcSignalSelect.Group0Compare0)
                {
                    this.Log(LogLevel.Debug, "Subscribing to SYSRTC CompareMatchGroup0Channel0 event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        sysrtc.CompareMatchGroup0Channel0 += asyncSignalHandlers[index];
                    });
                }
                else if(sigsel == (uint)SysrtcSignalSelect.Group0Compare1)
                {
                    this.Log(LogLevel.Debug, "Subscribing to SYSRTC CompareMatchGroup0Channel1 event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        sysrtc.CompareMatchGroup0Channel1 += asyncSignalHandlers[index];
                    });
                }
                else if(sigsel == (uint)SysrtcSignalSelect.Group1Compare0)
                {
                    this.Log(LogLevel.Debug, "Subscribing to SYSRTC CompareMatchGroup1Channel0 event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        sysrtc.CompareMatchGroup1Channel0 += asyncSignalHandlers[index];
                    });
                }
                else if(sigsel == (uint)SysrtcSignalSelect.Group1Compare1)
                {
                    this.Log(LogLevel.Debug, "Subscribing to SYSRTC CompareMatchGroup1Channel1 event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        sysrtc.CompareMatchGroup1Channel1 += asyncSignalHandlers[index];
                    });
                }
                else
                {
                    this.Log(LogLevel.Error, "Unhandled Sysrtc SIGSEL {0} on channel {1}", sigsel, index);
                }
            }
            else if(sourcesel == AsyncProducerSourceSelect.Hfxo0L)
            {
                if(sigsel == (uint)HfxoLSignalSelect.Status1)
                {
                    this.Log(LogLevel.Debug, "Subscribing to HFXO0L HfxoEnabled event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        hfxo.HfxoEnabled += asyncSignalHandlers[index];
                    });
                }
                else
                {
                    this.Log(LogLevel.Error, "Unhandled HFXO0L SIGSEL {0} on channel {1}", sigsel, index);
                }
            }
            else if(sourcesel == AsyncProducerSourceSelect.ProtimerL)
            {
                if(sigsel >= (uint)ProtimerLSignalSelect.Cc0
                   && sigsel <= (uint)ProtimerLSignalSelect.Cc4)
                {
                    uint cc = sigsel - (uint)ProtimerLSignalSelect.Cc0;
                    this.Log(LogLevel.Debug, "Subscribing to PROTIMERL Cc{0} event on channel {1}", cc, index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        lpw.PROTIMER_CaptureComparePrsEvent[cc] += asyncSignalHandlers[index];
                    });
                }
                else
                {
                    this.Log(LogLevel.Error, "Unhandled PROTIMERL SIGSEL {0} on channel {1}", sigsel, index);
                }
            }
            else
            {
                this.Log(LogLevel.Error, "Unhandled PRS ASYNC CH CTRL SOURCESEL {0} on channel {1}", sourcesel, index);
            }
        }

        partial void Async_swpulse_Write(uint a, uint b)
        {
            for(uint i = 0; i < NumberOfAsyncChannels; i++)
            {
                bool newValue = ((b >> (int)i) & 0x1) != 0;
                if(newValue)
                {
                    OnAsyncSignalReceived(i);
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

            for(int i = 0; i < NumberOfProtimerChannels; i++)
            {
                lpw.PROTIMER_CaptureComparePrsEvent[i] -= asyncSignalHandlers[channel];
            }
        }

        private void OnAsyncSignalReceived(uint channel)
        {
            this.Log(LogLevel.Debug, "Async signal on channel {0}", channel);

            // Triggers all consumers which are subscribed to the specified channel
            if(consumer_hfxo0_oscreq_prssel_field.Value == channel)
            {
                this.Log(LogLevel.Debug, "Triggering HFXO EM2 Wakeup");
                hfxo.OnEm2Wakeup();
            }

            if(consumer_sysrtc0_in0_prssel_field.Value == channel)
            {
                this.Log(LogLevel.Debug, "Triggering SYSRTC Capture Group 0");
                sysrtc.CaptureGroup0();
            }

            if(consumer_sysrtc0_in1_prssel_field.Value == channel)
            {
                this.Log(LogLevel.Debug, "Triggering SYSRTC Capture Group 1");
                sysrtc.CaptureGroup1();
            }

            if(consumer_protimer_rtcctrigger_prssel_field.Value == channel)
            {
                this.Log(LogLevel.Debug, "Triggering PROTIMER RTCC Trigger");
                lpw.PROTIMER_PrsRtcTrigger();
            }

            if(consumer_protimer_lbtstart_prssel_field.Value == channel)
            {
                this.Log(LogLevel.Debug, "Triggering PROTIMER LBT Start");
                lpw.PROTIMER_PrsLbtStart();
            }

            if(consumer_protimer_lbtstop_prssel_field.Value == channel)
            {
                this.Log(LogLevel.Debug, "Triggering PROTIMER LBT Stop");
                lpw.PROTIMER_PrsLbtStop();
            }

            if(consumer_protimer_lbtpause_prssel_field.Value == channel)
            {
                this.Log(LogLevel.Debug, "Triggering PROTIMER LBT Pause");
                lpw.PROTIMER_PrsLbtPause();
            }

            // TODO: Add more conditional blocks as we get more events
        }

        // An array of methods which handle an async signal on a given channel
        private Action[] asyncSignalHandlers;

        // A reference to sysrtc so that the prs can subscribe to events the sysrtc produces
        private readonly SiLabs_SYSRTC_1 sysrtc;

        // A reference to hfxo so that the prs can subscribe to events the hfxo produces
        private readonly SiLabs_HFXO_3 hfxo;

        // A reference to the LPW so that the prs can subscribe to events the LPW produces and 
        // call methods to dispatch events
        private readonly SiLabs_xG24_LPW lpw;

        private const int NumberOfAsyncChannels = 16;
        private const int NumberOfProtimerChannels = 5;
        private const uint SignalSelectMask = 0x7;
        private const int SourceSelectOffset = 8;
        private const uint SourceSelectMask = 0x7F;

        // From efr32mg24_prs_signals.h in gsdk
        private enum AsyncProducerSourceSelect
        {
            None = 0x0,
            Iadc0 = 0x1,
            Letimer0 = 0x2,
            Burtc = 0x3,
            Gpio = 0x4,
            CmuL = 0x5,
            Cmu = 0x6,
            CmuH = 0x7,
            PrsL = 0x8,
            Prs = 0x9,
            Acmp0 = 0xA,
            Acmp1 = 0xB,
            Pcnt0 = 0xC,
            Sysrtc0 = 0xD,
            Hfxo0L = 0xE,
            Hfxo0 = 0xF,
            Eusart0L = 0x10,
            Eusart0 = 0x11,
            Vdac0L = 0x12,
            Vdac0 = 0x13,
            Vdac1L = 0x14,
            Vdac1 = 0x15,
            Emul = 0x16,
            Emu = 0x17,
            Lfrco = 0x18,
            HfrcoEm23 = 0x19,
            Usart0 = 0x20,
            Timer0 = 0x21,
            Timer1 = 0x22,
            Timer2 = 0x23,
            Timer3 = 0x24,
            Core = 0x25,
            AgcL = 0x26,
            Agc = 0x27,
            Bufc = 0x28,
            ModemL = 0x29,
            Modem = 0x2A,
            ModemH = 0x2B,
            Frc = 0x2C,
            ProtimerL = 0x2D,
            Protimer = 0x2E,
            Synth = 0x2F,
            RacL = 0x30,
            Rac = 0x31,
            Timer4 = 0x32,
            Eusart1L = 0x33,
            Eusart1 = 0x34,
            Hfrco0 = 0x35,
        }

        private enum SysrtcSignalSelect
        {
            Group0Compare0 = 0,
            Group0Compare1 = 1,
            Group1Compare0 = 2,
            Group1Compare1 = 3,
        }

        private enum HfxoLSignalSelect
        {
            Status = 0,
            Status1 = 1,
        }

        private enum ProtimerLSignalSelect
        {
            Bof = 0,
            Cc0 = 1,
            Cc1 = 2,
            Cc2 = 3,
            Cc3 = 4,
            Cc4 = 5,
            Lbtf = 6,
            Lbtr = 7,
        }

        private enum ProtimerSignalSelect
        {
            Lbts = 0,
            Pof = 1,
            T0Match = 2,
            T0Uf = 3,
            T1Match = 4,
            T1Uf = 5,
            Wof = 6,
        }
    }
}
