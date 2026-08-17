//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class ExternalControlSPIPeripheral : ISPIPeripheral
    {
        public void Reset()
        {
        }

        public byte Transmit(byte data)
        {
            if(OnTransmit == null)
            {
                this.Log(LogLevel.Error, "External Control callbacks not registered, returning default response: 0x{0:X2}", DefaultResponse);
            }
            var response = OnTransmit?.Invoke(data) ?? DefaultResponse;
            this.Log(LogLevel.Noisy, "Received 0x{0:X2} from master, returning 0x{1:X2}", data, response);
            return response;
        }

        public void FinishTransmission()
        {
            if(OnFinishTransmission == null)
            {
                this.Log(LogLevel.Error, "External Control callbacks not registered, skipping finish_transmission event");
            }
            OnFinishTransmission?.Invoke();
            this.Log(LogLevel.Noisy, "Transmission finished");
        }

        public Func<byte, byte> OnTransmit;
        public Action OnFinishTransmission;

        public const byte DefaultResponse = 0x00;
    }
}
