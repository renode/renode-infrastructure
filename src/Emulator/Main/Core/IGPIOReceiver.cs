//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;

using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core
{
    public interface IGPIOReceiver : IPeripheral
    {
        void OnGPIO(int number, bool value);
    }

    public static class IGPIOReceiverExtensions
    {
        public static int GetPeripheralInputCount(this IGPIOReceiver receiver)
        {
            var attribute = (GPIOAttribute)receiver.GetType().GetCustomAttributes(true).FirstOrDefault(x => x is GPIOAttribute);
            return attribute != null ? attribute.NumberOfInputs : 0;
        }
    }
}

