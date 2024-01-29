//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System;
using System.Collections.Generic;
using static Antmicro.Renode.Peripherals.Bus.GaislerAPBPlugAndPlayRecord;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public sealed class Gaisler_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize, IGaislerAPB
    {
        // Connections [0; numberOfConnections) are the actual connections.
        // Connections [numberOfConnections; numberOfConnections + numberOfInterrupts) are the interrupt outputs.
        public Gaisler_GPIO(IMachine machine, int numberOfConnections, int numberOfInterrupts, string inputOnlyPins = "") : base(machine, numberOfConnections + numberOfInterrupts)
        {
            if(numberOfConnections < 1 || numberOfConnections > 32)
            {
                throw new ConstructionException("Number of connections has to be in [1; 32]");
            }
            if(numberOfInterrupts < 1 || numberOfInterrupts > 15)
            {
                throw new ConstructionException("Number of interrupts has to be in [1; 15].");
            }

            RegistersCollection = new DoubleWordRegisterCollection(this);
            numberOfActualConnections = numberOfConnections; // NumberOfConnections from BaseGPIOPort also counts the IRQ outputs.
            this.numberOfInterrupts = numberOfInterrupts;
            this.inputOnlyPins = new HashSet<int>();
            try
            {
                foreach(var pin in inputOnlyPins.Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    this.inputOnlyPins.Add(int.Parse(pin));
                }
            }
            catch(Exception e)
            {
                throw new ConstructionException($"Failed to parse {nameof(inputOnlyPins)}: {e.Message}", e);
            }
            interrupts = new IGPIO[numberOfInterrupts];
            for(var i = 0; i < numberOfInterrupts; i++)
            {
                interrupts[i] = Connections[numberOfConnections + i];
            }

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var interrupt in interrupts)
            {
                interrupt.Unset();
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }
            if(isOutput[number].Value)
            {
                this.WarningLog("Writing to GPIO #{0} which is configured as an output", number);
                return;
            }

            base.OnGPIO(number, value);
            if(TryGetInterruptIndex(number, out var it))
            {
                UpdateInterrupt(it);
            }
        }

        public long Size => 0x100;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public uint GetVendorID() => VendorID;

        public uint GetDeviceID() => DeviceID;

        public uint GetInterruptNumber() => 0;

        public SpaceType GetSpaceType() => SpaceType.APBIOSpace;

        private void DefineRegisters()
        {
            Registers.Data.Define(this)
                .WithFlags(0, numberOfActualConnections, FieldMode.Read, name: "data",
                    valueProviderCallback: (i, _) => State[i] && !isOutput[i].Value);

            // The GRLIB BSP's GRGPIO driver writes all 1s and reads back the value to determine how many GPIO
            // lines exist, but afterwards it will write 0s there so we don't define tags with allowed value 1
            Registers.Output.Define(this)
                .WithFlags(0, numberOfActualConnections, out outputValue, name: "output",
                    changeCallback: (i, _, value) => UpdateOutput(i));

            Registers.Direction.Define(this)
                .WithFlags(0, numberOfActualConnections, out isOutput, name: "direction",
                    changeCallback: (i, _, value) => UpdateOutput(i));

            Registers.InterruptMask.Define(this)
                .WithFlags(FirstInterruptPinIndex, numberOfInterrupts, out interruptMask, name: "interruptMask",
                    changeCallback: (i, _, value) => UpdateInterrupt(i));

            Registers.InterruptPolarity.Define(this)
                .WithFlags(FirstInterruptPinIndex, numberOfInterrupts, out interruptPolarity, name: "interruptPolarity",
                    changeCallback: (i, _, value) => UpdateInterrupt(i));

            Registers.InterruptEdge.Define(this)
                .WithFlags(FirstInterruptPinIndex, numberOfInterrupts, out interruptEdge, name: "interruptEdge",
                    changeCallback: (i, _, value) => UpdateInterrupt(i));

            Registers.Bypass.Define(this)
                .WithValueField(0, 32, name: "bypass");
        }

        private bool TryGetInterruptIndex(int pin, out int it)
        {
            it = pin - FirstInterruptPinIndex;
            return it >= 0 && it < numberOfInterrupts;
        }

        private void UpdateInterrupt(int it)
        {
            var pin = FirstInterruptPinIndex + it;

            // Output pins cannot trigger interrupts
            if(isOutput[pin].Value)
            {
                return;
            }

            var interruptState = interruptMask[it].Value && State[pin] == interruptPolarity[it].Value;
            // For edge-triggered interrupts we blink the output on the appropriate edge
            if(interruptEdge[it].Value)
            {
                if(interruptState)
                {
                    interrupts[it].Blink();
                }
            }
            else
            {
                interrupts[it].Set(interruptState);
            }
        }

        private void UpdateOutput(int pin)
        {
            if(!isOutput[pin].Value)
            {
                return;
            }
            if(inputOnlyPins.Contains(pin))
            {
                this.WarningLog("Ignoring attempt to drive input-only pin #{0}", pin);
                return;
            }
            Connections[pin].Set(outputValue[pin].Value);
        }

        private const uint VendorID = 0x01; // Gaisler Research
        private const uint DeviceID = 0x01a; // GRGPIO
        private const int FirstInterruptPinIndex = 1;
        private readonly int numberOfInterrupts;
        private readonly int numberOfActualConnections;
        private readonly IGPIO[] interrupts;
        private readonly HashSet<int> inputOnlyPins;

        private IFlagRegisterField[] isOutput;
        private IFlagRegisterField[] outputValue;
        private IFlagRegisterField[] interruptMask;
        private IFlagRegisterField[] interruptPolarity;
        private IFlagRegisterField[] interruptEdge;

        private enum Registers : uint
        {
            Data = 0x00,
            Output = 0x04,
            Direction = 0x08,
            InterruptMask = 0x0c,
            InterruptPolarity = 0x10,
            InterruptEdge = 0x14,
            Bypass = 0x18,
        }
    }
}
