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
    public partial class SiLabs_PRS_9
    {
        public SiLabs_PRS_9(IMachine machine, SiLabs_HFXO_5 hfxo, SiLabs_SYSRTC_2 sysrtc, SiLabs_xG301_LPW lpw) : base(machine)
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

        partial void PRS_Reset()
        {
            registers.Reset();
        }

        // Implementing declaration
        partial void Async_Ch_Ctrl_Write(ulong index, uint a, uint b)
        {
            // Parsing input
            uint sigsel = b & SignalSelectMask;
            var sourcesel = (AsyncProducerSourceSelect)((b >> SourceSelectOffset) & SourceSelectMask);

            this.Log(LogLevel.Info, "ASYNC CH-{0} CTRL WRITE: sourcesel {1} sigsel 0x{2:X}", index, (AsyncProducerSourceSelect)sourcesel, sigsel);

            // Subscribing to an event according to the sourcesel and sigsel
            if(sourcesel == AsyncProducerSourceSelect.Sysrtc0)
            {
                if(sigsel == (uint)SysrtcSignalSelect.Group0Compare0)
                {
                    // Unsubscribe from all events then subscribe to sysrtc group0 channel 0 compare event
                    this.Log(LogLevel.Info, "Subscribing to Sysrtc CompareMatchGroup0Channel0 event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        sysrtc.CompareMatchGroup0Channel0 += asyncSignalHandlers[index];
                    });
                }
                else if(sigsel == (uint)SysrtcSignalSelect.Group0Compare1)
                {
                    // Unsubscribe from all events then subscribe to sysrtc group0 channel 1 compare event
                    this.Log(LogLevel.Debug, "Subscribing to Sysrtc CompareMatchGroup0Channel1 event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        sysrtc.CompareMatchGroup0Channel1 += asyncSignalHandlers[index];
                    });
                }
                else if(sigsel == (uint)SysrtcSignalSelect.Group1Compare0)
                {
                    // Unsubscribe from all events then subscribe to sysrtc group0 channel 0 compare event
                    this.Log(LogLevel.Debug, "Subscribing to Sysrtc CompareMatchGroup1Channel0 event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        sysrtc.CompareMatchGroup1Channel0 += asyncSignalHandlers[index];
                    });
                }
                else if(sigsel == (uint)SysrtcSignalSelect.Group1Compare1)
                {
                    // Unsubscribe from all events then subscribe to sysrtc group0 channel 0 compare event
                    this.Log(LogLevel.Debug, "Subscribing to Sysrtc CompareMatchGroup1Channel1 event on channel {0}", index);
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
                    // Unsubscribe from all events then subscribe to hfxo enabled event
                    this.Log(LogLevel.Debug, "Subscribing to Hfxo HfxoEnabled event on channel {0}", index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        hfxo.HfxoEnabled += asyncSignalHandlers[index];
                    });
                }
                else
                {
                    this.Log(LogLevel.Error, "Unhandled HFXO SIGSEL {0} on channel {1}", sigsel, index);
                }
            }
            else if(sourcesel == AsyncProducerSourceSelect.ProtimerL)
            {
                if(sigsel >= (uint)ProtimerLSignalSelect.Cc0
                   && sigsel <= (uint)ProtimerLSignalSelect.Cc4)
                {
                    uint cc = sigsel - (uint)ProtimerLSignalSelect.Cc0;
                    // Unsubscribe from all events then subscribe to PROTIMERL CC enabled event
                    this.Log(LogLevel.Debug, "Subscribing to PROTIMERL CC{0} event on channel {1}", cc, index);
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        lpw.PROTIMER_CaptureComparePrsEvent[cc] += asyncSignalHandlers[index];
                    });
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

            for(int i = 0; i < 5; i++)
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
                this.Log(LogLevel.Debug, "Triggering SYSRTC Capture");
                sysrtc.CaptureGroup0();
            }

            if(consumer_sysrtc0_in1_prssel_field.Value == channel)
            {
                this.Log(LogLevel.Debug, "Triggering SYSRTC Capture");
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
        private readonly Action[] asyncSignalHandlers;

        // A reference to sysrtc so that the prs can subscribe to events the sysrtc produces
        private readonly SiLabs_SYSRTC_2 sysrtc;

        // A reference to hfxo so that the prs can subscribe to events the hfxo produces
        private readonly SiLabs_HFXO_5 hfxo;

        // A reference to the LPW so that the prs can subscribe to events the LPW produces and 
        // call methods to dispatch events
        private readonly SiLabs_xG301_LPW lpw;

        private const int NumberOfAsyncChannels = 16;
        private const uint SignalSelectMask = 0x7;
        private const int SourceSelectOffset = 8;
        private const uint SourceSelectMask = 0x7F;

        // From sixg301_prs_signals.h in gsdk
        private enum AsyncProducerSourceSelect
        {
            None = 0x0,
            Letimer0 = 0x1,
            Burtc = 0x2,
            Gpio = 0x3,
            CmuL = 0x4,
            Cmu = 0x5,
            CmuH = 0x6,
            PrsL = 0x7,
            Prs = 0x8,
            Acmp0 = 0x9,
            Acmp1 = 0xA,
            Pcnt0 = 0xB,
            Sysrtc0 = 0xC,
            Hfxo0L = 0xD,
            Hfxo0 = 0xE,
            Eusart0L = 0xF,
            Eusart0 = 0x10,
            Adc0 = 0x11,
            Etampdet = 0x12,
            FsRco = 0x13,
            Emul = 0x14,
            EmU = 0x15,
            Lfrco = 0x16,
            HfrcoEm23 = 0x17,
            Timer0 = 0x20,
            Timer1 = 0x21,
            Timer2L = 0x22,
            Timer2 = 0x23,
            Timer3L = 0x24,
            Timer3 = 0x25,
            Core = 0x26,
            AgcL = 0x27,
            Agc = 0x28,
            Bufc = 0x29,
            ModemL = 0x2A,
            Modem = 0x2B,
            ModemH = 0x2C,
            Frc = 0x2D,
            ProtimerL = 0x2E,
            Protimer = 0x2F,
            Synth = 0x30,
            RacL = 0x31,
            Rac = 0x32,
            Eusart1L = 0x33,
            Eusart1 = 0x34,
            Eusart2L = 0x35,
            Eusart2 = 0x36,
            RfTimer = 0x37,
            Seqacc = 0x38,
            Hfrco0 = 0x39,
            Hfrcolpw = 0x3A,
            SeHfrco = 0x3B,
            SeAtampdet = 0x3C,
            Qspi0 = 0x3D,
            Socpll0 = 0x3E,
            Pixelrz0 = 0x3F,
            Pixelrz1 = 0x40,
            Leddrv0 = 0x41,
            Rpa = 0x42,
            Ksu = 0x43,
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
            Debug0 = 2,
            Debug1 = 3,
            State0 = 4,
            State1 = 5,
            State2 = 6,
            State3 = 7,
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
