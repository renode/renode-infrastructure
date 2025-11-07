//
// Copyright (c) 2010-2025 Silicon Labs
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

        private void UnsubscribeFromAllEvents(ulong channel)
        {
            // Unsubscribing from all events prior to subscribing to the next one ensures that
            // a producer only writes to one channel. Additionally, it prevents the same channel from subscribing
            // to an event multiple times. 
            prortc.CompareMatchChannel[0] -= asyncSignalHandlers[channel];
            prortc.CompareMatchChannel[1] -= asyncSignalHandlers[channel];
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
        private const uint SIGSEL_MASK = 0x7;
        private const int SOURCESEL_OFFSET = 8;
        private const uint SOURCESEL_MASK = 0x7F;

        partial void Async_Ch_Ctrl_Write(ulong index, uint a, uint b)
        {
            // Parsing input
            uint sigsel = b & SIGSEL_MASK;
            uint sourcesel = (b >> SOURCESEL_OFFSET) & SOURCESEL_MASK;

            this.Log(LogLevel.Info, "ASYNC_CH_CTRL[{0}] Write: sigsel={1}, sourcesel={2}", index, sigsel, (AsyncProducerSourceSelect)sourcesel);

            if(sourcesel == (uint)AsyncProducerSourceSelect.ProRtc)
            {
                this.Log(LogLevel.Info, "Subscribing to PRORTC Compare Match channel {0} event through PRS channel {1}", sigsel, index);

                if(sigsel == (uint)RtccSignalSelect.CompareMatchChannel0)
                {
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        prortc.CompareMatchChannel[0] += asyncSignalHandlers[index];
                    });
                }
                if(sigsel == (uint)RtccSignalSelect.CompareMatchChannel1)
                {
                    machine.ClockSource.ExecuteInLock(delegate
                    {
                        UnsubscribeFromAllEvents(index);
                        prortc.CompareMatchChannel[1] += asyncSignalHandlers[index];
                    });
                }
            }
        }

        partial void Async_swpulse_Write(uint a, uint b)
        {
            for(uint i = 0; i < NumberOfAsyncChannels; i++)
            {
                bool oldValue = ((a >> (int)i) & 0x1) != 0;
                bool newValue = ((b >> (int)i) & 0x1) != 0;
                if(newValue && !oldValue)
                {
                    OnAsyncSignalReceived(i);
                }
            }
        }

        private enum AsyncProducerSourceSelect
        {
            Iadc0 = 0x01,
            LeTimer = 0x02,
            Rtcc = 0x03,
            Burtc = 0x04,
            Gpio = 0x05,
            CmuL = 0x06,
            Cmu = 0x07,
            CmuH = 0x08,
            ProRtc = 0x09,
            Prs0L = 0x0A,
            Prs0 = 0x0B,
            LeUart = 0x0C,
            EmuL = 0x0D,
            Emu = 0x0E,
            RfSense = 0x0F,
            Usart0 = 0x20,
            Usart1 = 0x21,
            Timer0 = 0x22,
            Timer1 = 0x23,
            Timer2 = 0x24,
            Timer3 = 0x25,
            Teal = 0x26,
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
            Syxo0 = 0x36,
            Hfrco0 = 0x37,
            Plfrco = 0x38,
        }

        private enum RtccSignalSelect
        {
            CompareMatchChannel0 = 0x0,
            CompareMatchChannel1 = 0x1,
            CompareMatchChannel2 = 0x2,
        }
    }
}