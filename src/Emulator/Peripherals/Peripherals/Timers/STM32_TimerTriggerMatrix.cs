//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class STM32_TimerTriggerMatrix : IGPIOReceiver, IPeripheral
    {
        public STM32_TimerTriggerMatrix(IMachine machine)
        {
            this.machine = machine;
            itrLines = new GPIO[32];
            for(var i = 0; i < itrLines.Length; i++)
            {
                itrLines[i] = new GPIO();
            }
        }

        public void OnGPIO(int number, bool value)
        {
            if(number < 0 || number >= itrLines.Length)
            {
                this.Log(LogLevel.Error, "Invalid TRGO input number: {0}", number);
                return;
            }

            itrLines[number].Set(value);
        }

        public IGPIO GetITR(int index)
        {
            if(index < 0 || index >= itrLines.Length)
            {
                this.Log(LogLevel.Error, "Invalid ITR index requested: {0}", index);
                return null;
            }
            return itrLines[index];
        }

        public void Reset()
        {
            foreach(var line in itrLines)
            {
                line.Unset();
            }
        }

        private readonly GPIO[] itrLines;
        private readonly IMachine machine;
    }
}
