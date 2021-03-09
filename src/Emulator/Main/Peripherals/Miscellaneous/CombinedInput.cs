//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class CombinedInput : IGPIOReceiver
    {
        public CombinedInput(int numberOfInputs)
        {
           inputStates = new bool[numberOfInputs];
           OutputLine = new GPIO();
           
           Reset();
        }

        public void Reset()
        {
           Array.Clear(inputStates, 0, inputStates.Length);
           OutputLine.Unset();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number < 0 || number >= inputStates.Length)
            {
                this.Log(LogLevel.Error, "Received GPIO signal on an unsupported port #{0} (supported ports are 0 - {1}). Please check the platform configuration", number, inputStates.Length - 1);
                return;
            }

            inputStates[number] = value;
            OutputLine.Set(inputStates.Any(x => x));
        }
        
        public GPIO OutputLine { get; } 

        private readonly bool[] inputStates;
    }
}

