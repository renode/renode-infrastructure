//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class RenesasRZG_IRQController: BasicDoubleWordPeripheral, IKnownSize, IIRQController, INumberedGPIOOutput
    {
        public RenesasRZG_IRQController(IMachine machine) : base(machine)
        {
            Connections = Enumerable
                .Range(0, NrOfGpioOutputs)
                .ToDictionary<int, int, IGPIO>(idx => idx, _ => new GPIO());

            pinFunctionInterrupts = Enumerable
                .Range(0, NrOfPinFunctionOutputs)
                .Select(_ => new GPIO()).ToArray();

            DefineRegisters();
            Reset();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number < NrOfNonMaskableInputs)
            {
                this.WarningLog("Received non maskable interrupt on pin {0} which is not implemented.", number);
            }
            else if(number < NrOfNonMaskableInputs + NrOfPinFunctionInputs)
            {
                number -= NrOfNonMaskableInputs;
                previousPinFunctionState[number] = pinFunctionState[number];
                pinFunctionState[number] = value;
                UpdateInterrupts();
            }
            else if(number < NrOfAllInputs)
            {
                number -= NrOfNonMaskableInputs + NrOfPinFunctionInputs;
                previousGpioState[number] = gpioState[number];
                gpioState[number] = value;
                UpdateInterrupts();
            }
            else
            {
                this.ErrorLog("GPIO number {0} is out of range [0; {1})", number, NrOfGpioInputs);
            }
        }

        public override void Reset()
        {
            base.Reset();
            Array.Clear(gpioState, 0, gpioState.Length);
            Array.Clear(previousGpioState, 0, previousGpioState.Length);
            Array.Clear(pinFunctionState, 0, pinFunctionState.Length);
            Array.Clear(previousPinFunctionState, 0, previousPinFunctionState.Length);
            UpdateInterrupts();
        }

        public long Size => 0x100;

        public GPIO IRQ0 => pinFunctionInterrupts[0];
        public GPIO IRQ1 => pinFunctionInterrupts[1];
        public GPIO IRQ2 => pinFunctionInterrupts[2];
        public GPIO IRQ3 => pinFunctionInterrupts[3];
        public GPIO IRQ4 => pinFunctionInterrupts[4];
        public GPIO IRQ5 => pinFunctionInterrupts[5];
        public GPIO IRQ6 => pinFunctionInterrupts[6];
        public GPIO IRQ7 => pinFunctionInterrupts[7];
        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private void DefineRegisters()
        {
            Registers.NonMaskableInterruptStatusControl.Define(this)
                .WithTaggedFlag("NSTAT", 0)
                .WithReservedBits(1, 15)
                .WithTaggedFlag("NSMON", 16)
                .WithReservedBits(17, 15);

            Registers.NonMaskableInterruptTypeSelection.Define(this)
                .WithTaggedFlag("NSEL", 0)
                .WithReservedBits(1, 31);

            Registers.InterruptStatusControl.Define(this)
                .WithFlags(0, 8, out pinFunctionInterruptStatus, FieldMode.Read | FieldMode.WriteZeroToSet,
                    writeCallback: InterruptStatusControlWriteCallback,
                    name: "ISTAT")
                .WithReservedBits(8, 24);

            Registers.InterruptTypeSelection.Define(this)
                .WithEnumFields(0, 2, 8, out pinFunctionInterruptType, name: "IISEL")
                .WithReservedBits(16, 16);

            Registers.GpioInterruptStatusControl.Define(this)
                .WithFlags(0, 32, out gpioInterruptStatus, FieldMode.Read | FieldMode.WriteZeroToSet,
                    writeCallback: GpioInterruptStatusControlWriteCallback,
                    name: "TSTAT");

            Registers.GpioInterruptTypeSelection0.Define(this)
                .WithEnumFields<DoubleWordRegister, GpioInterruptType>(0, 2, 16, out var gpioInterruptType1, name: "TITSEL")
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.GpioInterruptTypeSelection1.Define(this)
                .WithEnumFields<DoubleWordRegister, GpioInterruptType>(0, 2, 16, out var gpioInterruptType2, name: "TITSEL")
                .WithWriteCallback((_, __) => UpdateInterrupts());

            gpioInterruptType = gpioInterruptType1.Concat(gpioInterruptType2).ToArray();

            Registers.GpioInterruptSourceSelection0.DefineMany(this,
                NrOfGpioSourceSelectionRegisters, BuildGpioInterruptSourceSelectionRegister);

            Registers.BusErrorInterruptStatusControl0.Define(this)
                .WithTaggedFlags("BESTA", 0, 32);

            Registers.BusErrorInterruptStatusControl1.Define(this)
                .WithTaggedFlags("BESTA", 0, 13)
                .WithReservedBits(13, 19);

            Registers.EccRamErrorInterruptStatusControl0.Define(this)
                .WithTaggedFlags("E1STAT", 0, 8)
                .WithTaggedFlags("E2STAT", 8, 8)
                .WithTaggedFlags("OFSTAT", 16, 8)
                .WithReservedBits(24, 8);

            Registers.EccRamErrorInterruptStatusControl1.Define(this)
                .WithTaggedFlags("E1STAT", 0, 8)
                .WithTaggedFlags("E2STAT", 8, 8)
                .WithTaggedFlags("OFSTAT", 16, 8)
                .WithReservedBits(24, 8);
        }

        private void BuildGpioInterruptSourceSelectionRegister(DoubleWordRegister register, int registerIdx)
        {
            var interruptOffset = registerIdx * 4;
            register
                .WithValueField(0, 7, out gpioInterruptSelect[interruptOffset], name: $"TSSEL{interruptOffset}")
                .WithFlag(7, out gpioInterruptEnabled[interruptOffset], name: $"TIEN{interruptOffset}")
                .WithValueField(8, 7, out gpioInterruptSelect[interruptOffset + 1], name: $"TSSEL{interruptOffset + 1}")
                .WithFlag(15, out gpioInterruptEnabled[interruptOffset + 1], name: $"TIEN{interruptOffset + 1}")
                .WithValueField(16, 7, out gpioInterruptSelect[interruptOffset + 2], name: $"TSSEL{interruptOffset + 2}")
                .WithFlag(23, out gpioInterruptEnabled[interruptOffset + 2], name: $"TIEN{interruptOffset + 2}")
                .WithValueField(24, 7, out gpioInterruptSelect[interruptOffset + 3], name: $"TSSEL{interruptOffset + 3}")
                .WithFlag(31, out gpioInterruptEnabled[interruptOffset + 3], name: $"TIEN{interruptOffset + 3}")
                .WithWriteCallback((_, __) => UpdateInterrupts());
        }

        private void UpdateInterrupts()
        {
            for(var interruptIdx = 0; interruptIdx < NrOfGpioOutputs; interruptIdx++)
            {
                var gpioIdx = gpioInterruptSelect[interruptIdx].Value;
                var state = gpioState[gpioIdx];
                var gpioStateChanged = state ^ previousGpioState[gpioIdx];
                var isInterruptEnabled = gpioInterruptEnabled[interruptIdx].Value;
                var trigger = gpioInterruptType[interruptIdx].Value;

                if(isInterruptEnabled)
                {
                    switch(trigger)
                    {
                        case GpioInterruptType.RisingEdge:
                            if(gpioStateChanged && state)
                            {
                                gpioInterruptStatus[interruptIdx].Value = true;
                                Connections[interruptIdx].Blink();
                                this.DebugLog("TINT{0}: blinked", interruptIdx);
                            }
                            break;
                        case GpioInterruptType.FallingEdge:
                            if(gpioStateChanged && !state)
                            {
                                gpioInterruptStatus[interruptIdx].Value = true;
                                Connections[interruptIdx].Blink();
                                this.DebugLog("TINT{0}: blinked", interruptIdx);
                            }
                            break;
                        case GpioInterruptType.HighLevel:
                            gpioInterruptStatus[interruptIdx].Value = state;
                            Connections[interruptIdx].Set(state);
                            this.DebugLog("TINT{0}: {1}", interruptIdx, state ? "set" : "unset");
                            break;
                        case GpioInterruptType.LowLevel:
                            gpioInterruptStatus[interruptIdx].Value = !state;
                            Connections[interruptIdx].Set(!state);
                            this.DebugLog("TINT{0}: {1}", interruptIdx, !state ? "set" : "unset");
                            break;
                    }
                }
                else
                {
                    gpioInterruptStatus[interruptIdx].Value = false;
                    Connections[interruptIdx].Unset();
                    this.DebugLog("TINT{0}: unset", interruptIdx);
                }
            }

            for(var interruptIdx = 0; interruptIdx < NrOfPinFunctionOutputs; interruptIdx++)
            {
                var state = pinFunctionState[interruptIdx];
                var trigger = pinFunctionInterruptType[interruptIdx].Value;
                var irqStateChanged = state ^ previousPinFunctionState[interruptIdx];

                switch(trigger)
                {
                    case PinFunctionInterruptType.LowLevel:
                        pinFunctionInterruptStatus[interruptIdx].Value = !state;
                        pinFunctionInterrupts[interruptIdx].Set(!state);
                        this.DebugLog("IRQ{0}: {1}", interruptIdx, !state ? "set" : "unset");
                        break;
                    case PinFunctionInterruptType.RisingEdge:
                        if(irqStateChanged && state)
                        {
                            pinFunctionInterruptStatus[interruptIdx].Value = true;
                            pinFunctionInterrupts[interruptIdx].Blink();
                            this.DebugLog("IRQ{0}: blinked", interruptIdx);
                        }
                        break;
                    case PinFunctionInterruptType.FallingEdge:
                        if(irqStateChanged && !state)
                        {
                            pinFunctionInterruptStatus[interruptIdx].Value = true;
                            pinFunctionInterrupts[interruptIdx].Blink();
                            this.DebugLog("IRQ{0}: blinked", interruptIdx);
                        }
                        break;
                    case PinFunctionInterruptType.BothEdges:
                        if(irqStateChanged)
                        {
                            pinFunctionInterruptStatus[interruptIdx].Value = true;
                            pinFunctionInterrupts[interruptIdx].Blink();
                            this.DebugLog("IRQ{0}: blinked", interruptIdx);
                        }
                        break;
                }
            }
        }

        private void InterruptStatusControlWriteCallback(int interruptIdx, bool oldVal, bool newVal)
        {
            var trigger = pinFunctionInterruptType[interruptIdx].Value;
            if(trigger == PinFunctionInterruptType.LowLevel || newVal)
            {
                pinFunctionInterruptStatus[interruptIdx].Value = oldVal;
            }
        }

        private void GpioInterruptStatusControlWriteCallback(int interruptIdx, bool oldVal, bool newVal)
        {
            var trigger = gpioInterruptType[interruptIdx].Value;
            switch(trigger)
            {
                case GpioInterruptType.RisingEdge:
                case GpioInterruptType.FallingEdge:
                    if(newVal)
                    {
                        gpioInterruptStatus[interruptIdx].Value = oldVal;
                    }
                    break;
                default:
                    gpioInterruptStatus[interruptIdx].Value = oldVal;
                    break;
            }
        }

        private IValueRegisterField[] gpioInterruptSelect = new IValueRegisterField[NrOfGpioOutputs];
        private IFlagRegisterField[] gpioInterruptEnabled = new IFlagRegisterField[NrOfGpioOutputs];
        private IFlagRegisterField[] pinFunctionInterruptStatus;
        private IFlagRegisterField[] gpioInterruptStatus;
        private IEnumRegisterField<PinFunctionInterruptType>[] pinFunctionInterruptType;
        private IEnumRegisterField<GpioInterruptType>[] gpioInterruptType;

        private readonly GPIO[] pinFunctionInterrupts;
        private readonly bool[] pinFunctionState = new bool[NrOfPinFunctionInputs];
        private readonly bool[] previousPinFunctionState = new bool[NrOfPinFunctionInputs];
        private readonly bool[] gpioState = new bool[NrOfGpioInputs];
        private readonly bool[] previousGpioState = new bool[NrOfGpioInputs];

        private const int NrOfNonMaskableInputs = 1;
        private const int NrOfPinFunctionInputs = 8;
        private const int NrOfGpioInputs = 123;
        private const int NrOfAllInputs = NrOfNonMaskableInputs + NrOfPinFunctionInputs + NrOfGpioInputs;
        private const int NrOfGpioOutputs = 32;
        private const int NrOfPinFunctionOutputs = NrOfPinFunctionInputs;
        private const int NrOfGpioSourceSelectionRegisters = 8;

        private enum PinFunctionInterruptType
        {
            LowLevel    = 0x0,
            FallingEdge = 0x1,
            RisingEdge  = 0x2,
            BothEdges   = 0x3,
        }

        private enum GpioInterruptType
        {
            RisingEdge  = 0x0,
            FallingEdge = 0x1,
            HighLevel   = 0x2,
            LowLevel    = 0x3,
        }

        private enum Registers
        {
            NonMaskableInterruptStatusControl   = 0x00, // NSCR
            NonMaskableInterruptTypeSelection   = 0x04, // NITSR
            InterruptStatusControl              = 0x10, // ISCR
            InterruptTypeSelection              = 0x14, // IITSR
            GpioInterruptStatusControl          = 0x20, // TSCR
            GpioInterruptTypeSelection0         = 0x24, // TITSR0
            GpioInterruptTypeSelection1         = 0x28, // TITSR1
            GpioInterruptSourceSelection0       = 0x30, // TSSR0
            GpioInterruptSourceSelection1       = 0x34, // TSSR1
            GpioInterruptSourceSelection2       = 0x38, // TSSR2
            GpioInterruptSourceSelection3       = 0x3C, // TSSR3
            GpioInterruptSourceSelection4       = 0x40, // TSSR4
            GpioInterruptSourceSelection5       = 0x44, // TSSR5
            GpioInterruptSourceSelection6       = 0x48, // TSSR6
            GpioInterruptSourceSelection7       = 0x4C, // TSSR7
            BusErrorInterruptStatusControl0     = 0x50, // BEISR0
            BusErrorInterruptStatusControl1     = 0x54, // BEISR1
            EccRamErrorInterruptStatusControl0  = 0x60, // EREISR0
            EccRamErrorInterruptStatusControl1  = 0x64, // EREISR1
        }
    }
}
