//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
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
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public partial class SiLabs_PRS_1 : BasicDoubleWordPeripheral, IKnownSize
    {
        public SiLabs_PRS_1(Machine machine, SiLabs_RTCC_1 prortc, SiLabs_xG22_LPW lpw) : base(machine)
        {
            this.prortc = prortc;
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

            if(sourcesel == AsyncProducerSourceSelect.ProRtc)
            {
                this.Log(LogLevel.Debug, "Subscribing to PRORTC Compare Match channel {0} event through PRS channel {1}", sigsel, index);

                if(sigsel == (uint)ProRtcSignalSelect.CompareMatchChannel0)
                {
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        prortc.CompareMatchChannel[0] += asyncSignalHandlers[index];
                    });
                }
                else if(sigsel == (uint)ProRtcSignalSelect.CompareMatchChannel1)
                {
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        prortc.CompareMatchChannel[1] += asyncSignalHandlers[index];
                    });
                }
                else
                {
                    this.Log(LogLevel.Error, "Unhandled PRORTC SIGSEL {0} on channel {1}", sigsel, index);
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
            prortc.CompareMatchChannel[0] -= asyncSignalHandlers[channel];
            prortc.CompareMatchChannel[1] -= asyncSignalHandlers[channel];

            for(int i = 0; i < NumberOfProtimerChannels; i++)
            {
                lpw.PROTIMER_CaptureComparePrsEvent[i] -= asyncSignalHandlers[channel];
            }
        }

        private void OnAsyncSignalReceived(uint channel)
        {
            this.Log(LogLevel.Debug, "Async signal on channel {0}", channel);

            // PRORTC consumer
            if(consumer_prortc_cc0_prssel_field.Value == channel)
            {
                this.Log(LogLevel.Debug, "Triggering PRORTC Capture Channel 0");
                prortc.CaptureChannel(0);
            }
            if(consumer_prortc_cc1_prssel_field.Value == channel)
            {
                this.Log(LogLevel.Debug, "Triggering PRORTC Capture Channel 1");
                prortc.CaptureChannel(1);
            }

            // PROTIMER RTCC Trigger consumer
            if(consumer_protimer_rtcctrigger_prssel_field.Value == channel)
            {
                this.Log(LogLevel.Debug, "Triggering PROTIMER RTC Trigger");
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

            // TODO: Add more consumers here
        }

        // A reference to prortc so that the prs can subscribe to events the prortc produces
        private readonly SiLabs_RTCC_1 prortc;
        // A reference to the LPW so that the prs can subscribe to events the LPW produces and 
        // call methods to dispatch events
        private readonly SiLabs_xG22_LPW lpw;

        // An array of methods which handle an async signal on a given channel
        private readonly Action[] asyncSignalHandlers;
        private const int NumberOfAsyncChannels = 12;
        private const int NumberOfProtimerChannels = 5;
        private const uint SignalSelectMask = 0x7;
        private const int SourceSelectOffset = 8;
        private const uint SourceSelectMask = 0x7F;

        // From efr32mg22_prs_signals.h
        private enum AsyncProducerSourceSelect
        {
            None = 0x00,
            Iadc0 = 0x01,
            LeTimer0 = 0x02,
            Rtcc = 0x03,
            Burtc = 0x04,
            Gpio = 0x05,
            CmuL = 0x06,
            Cmu = 0x07,
            CmuH = 0x08,
            ProRtc = 0x09,
            PrsL = 0x0A,
            Prs = 0x0B,
            Euart0 = 0x0C,
            EmuL = 0x0D,
            Emu = 0x0E,
            Lfrco = 0x0F,
            RfSense = 0x10,
            Usart0 = 0x20,
            Usart1 = 0x21,
            Timer0 = 0x22,
            Timer1 = 0x23,
            Timer2 = 0x24,
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
            PdmL = 0x31,
            Pdm = 0x32,
            RacL = 0x33,
            Rac = 0x34,
            Timer4 = 0x35,
            Hfxo0 = 0x36,
            Hfrco0 = 0x37,
        }

        private enum ProRtcSignalSelect
        {
            CompareMatchChannel0 = 0x0,
            CompareMatchChannel1 = 0x1,
            CompareMatchChannel2 = 0x2,
        }

        private enum ProtimerLSignalSelect
        {
            Bof = 0x0,
            Cc0 = 0x1,
            Cc1 = 0x2,
            Cc2 = 0x3,
            Cc3 = 0x4,
            Cc4 = 0x5,
            Lbtf = 0x6,
            Lbtr = 0x7,
        }

        private enum ProtimerSignalSelect
        {
            Lbts = 0x0,
            Pof = 0x1,
            T0Match = 0x2,
            T0Uf = 0x3,
            T1Match = 0x4,
            T1Uf = 0x5,
            Wof = 0x6,
        }
    }
}
