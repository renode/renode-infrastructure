//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SENT
{
    public class Transmitter
    {
        public Transmitter(IMachine machine, TimeInterval tickPeriod)
        {
            this.machine = machine;
            this.tickPeriod = tickPeriod;
        }

        public event Func<FastMessage> ProvideFastMessage;
        public event Func<SlowMessage> ProvideSlowMessage;
        public event Action<SENTEdge> Edge;

        public bool Transmitting
        {
            get => sendThread != null;
            set
            {
                if(value == Transmitting)
                {
                    return;
                }

                sendThread?.Dispose();
                sendThread = null;

                if(value)
                {
                    var coroutine = TransmitterThread().GetEnumerator();
                    sendThread = machine.ObtainManagedThread(delegate{}, tickPeriod, "SENT Transmitter", stopCondition: () => !coroutine.MoveNext());
                    sendThread.Start();
                }
            }
        }

        private IEnumerable TransmitterThread()
        {
            IEnumerator<bool> slowMessageBits = null;

            while(true)
            {
                // Generating pulses based on the SENT frame
                //
                // Description: | Sync pulse |       Each nibble      |   Pause pulse (optional)  | Sync pulse ...
                // Signal:      | Low | High | Low |        High      | Currently not implemented |
                // Ticks:       |  1  |  55  |  5  | 7 + nibble value |                           |
                //
                // Each `yield return null` is waiting until the time of the next tick

                var frame = ConstructFrame(ref slowMessageBits);

                // Sync pulse
                Edge?.Invoke(SENTEdge.Falling);
                yield return null;
                Edge?.Invoke(SENTEdge.Rising);

                for(var i = 0; i < SynchPulseWidth - 1; i++)
                {
                    yield return null;
                }

                // Pulse for each nibble to send
                foreach(var nibble in frame)
                {
                    Edge?.Invoke(SENTEdge.Falling);
                    for(var i = 0; i < NibbleLowPulseWidth; i++)
                    {
                        yield return null;
                    }
                    Edge?.Invoke(SENTEdge.Rising);

                    var pulseWidth = (nibble & NibbleByteMask) + NibblePulseOffset;
                    for(var i = 0; i < pulseWidth; i++)
                    {
                        yield return null;
                    }
                    // Next falling edge will be generated at the start of the inner or outer loop
                }
            }
        }

        private IEnumerable<byte> ConstructFrame(ref IEnumerator<bool> slowMessageBits)
        {
            // SENT frame structure (each segment is 4 bits long):
            // | Status | | Data0 | ... | DataN | CRC |
            //            <-    Max N = 6     ->
            // Status:
            //  Bit 3 - Serial sync. Should be 1 when starting a new slow message otherwise 0
            //  Bit 2 - Serial data. One bit of the slow message
            //  Bits 1, 0 - Currently reserved
            // Data:
            //  Data nibbles. Taken from the FastMessage class
            // CRC:
            //  CRC of data nibbles. Calculated using the x4 + x3 + x2 + 1 polynomial with a starting value of 5
            //  Note that CRC is added already added by the `FastMessage` class

            var newSlowMessage = false;
            if(slowMessageBits == null || !slowMessageBits.MoveNext())
            {
                slowMessageBits = ProvideSlowMessage().Bits.GetEnumerator();
                slowMessageBits.MoveNext();
                newSlowMessage = true;
            }

            byte statusNibble = 0x0;
            BitHelper.SetBit(ref statusNibble, StatusSerialSyncBit, newSlowMessage);
            BitHelper.SetBit(ref statusNibble, StatusSerialDataBit, slowMessageBits.Current);

            return ProvideFastMessage().Nibbles.Prepend(statusNibble);
        }

        private IManagedThread sendThread;

        private readonly IMachine machine;
        private readonly TimeInterval tickPeriod;

        private const int SynchPulseWidth = 56;
        private const int NibbleLowPulseWidth = 5;
        private const int NibblePulseOffset = 7;
        private const byte NibbleByteMask = 0xF;

        private const int StatusSerialSyncBit = 3;
        private const int StatusSerialDataBit = 2;
    }
}
