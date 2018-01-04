//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Backends.Terminals;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class FakeEfmSPITransmitter : BackendTerminal
    {
        public FakeEfmSPITransmitter()
        {
            responses = new Dictionary<int, byte>();
        }

        public void AddResponse(int byteNumber, byte data)
        {
            responses.Add(byteNumber, data);
        }

        public override void WriteChar(byte data)
        {
            // write the response char
            responses.TryGetValue(currentByteNo, out data);
            CallCharReceived(data);
            this.Log(LogLevel.Info, "Sent 0x{0:X} in {1} turn.", data, currentByteNo);
            currentByteNo++;
        }

        private int currentByteNo;
        private readonly Dictionary<int, byte> responses;
    }
}

