//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class EFR32_GPIOPort : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public EFR32_GPIOPort(IMachine machine) : base(machine, NumberOfPins * NumberOfPorts)
        {
            EvenIRQ = new GPIO();
            OddIRQ = new GPIO();
            CreateRegisters();
            InnerReset();
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(internalLock)
            {
                return registers.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(internalLock)
            {
                if(configurationLocked)
                {
                    if(offset < (uint)Registers.ExternalInterruptPortSelectLow)
                    {
                        //port register, align it to the first port
                        offset %= PortOffset;
                    }
                    var register = (Registers)offset;
                    if(lockableRegisters.Contains(register))
                    {
                        this.Log(LogLevel.Debug, "Not writing to {0} because of configuration lock.", register);
                        return;
                    }
                }
                registers.Write(offset, value);
            }
        }

        public override void Reset()
        {
            lock(internalLock)
            {
                base.Reset();
                InnerReset();
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            if(number < 0 || number >= State.Length)
            {
                throw new ArgumentOutOfRangeException(string.Format("Gpio #{0} called, but only {1} lines are available", number, State.Length));
            }
            lock(internalLock)
            {
                if(IsOutput(pinModes[number].Value))
                {
                    this.Log(LogLevel.Warning, "Writing to an output GPIO pin #{0}", number);
                    return;
                }

                base.OnGPIO(number, value);
                UpdateInterrupts();
            }
        }

        public long Size
        {
            get
            {
                return 0x1000;
            }
        }

        public GPIO EvenIRQ { get; private set; }
        public GPIO OddIRQ { get; private set; }

        private void UpdateInterrupts()
        {
            for(var i = 0; i < State.Length; ++i)
            {
                var externalPin = targetExternalPins[i];
                if(!interruptEnable[externalPin])
                {
                    continue;
                }
                var isEdge = State[i] != previousState[externalPin];
                previousState[externalPin] = State[i];
                if(isEdge && State[i] == (interruptTriggers[externalPin] == InterruptTrigger.RisingEdge))
                {
                    externalInterrupt[externalPin] = true;
                }
                //no clear as it must be set manually with InterruptFlagClear
            }

            var even = false;
            var odd = false;
            for(var i = 0; i < interruptEnable.Length; i += 2)
            {
                even |= externalInterrupt[i];
            }
            for(var i = 1; i < interruptEnable.Length; i += 2)
            {
                odd |= externalInterrupt[i];
            }
            EvenIRQ.Set(even);
            OddIRQ.Set(odd);
        }

        private void InnerReset()
        {
            registers.Reset();
            configurationLocked = false;
            EvenIRQ.Unset();
            OddIRQ.Unset();
            for(var i = 0; i < NumberOfExternalInterrupts; ++i)
            {
                externalInterrupt[i] = false;
                interruptEnable[i] = false;
                interruptTriggers[i] = InterruptTrigger.None;
            }
            for(var i = 0; i < targetExternalPins.Length; ++i)
            {
                targetExternalPins[i] = 0;
            }
            for(var i = 0; i < externalInterruptToPinMapping.Length; ++i)
            {
                //both arrays have the same length
                externalInterruptToPinMapping[i] = i % 4;
                externalInterruptToPortMapping[i] = 0;
            }
        }

        private void CreateRegisters()
        {
            var regs = new Dictionary<long, DoubleWordRegister>()
            {
                {(long)Registers.ExternalInterruptPortSelectLow, new DoubleWordRegister(this)
                    .WithValueField(0, 32, changeCallback: (oldValue, newValue) => ReroutePort((uint)oldValue, (uint)newValue, false), name: "EXTIPSEL")
                },
                {(long)Registers.ExternalInterruptPortSelectHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 32, changeCallback: (oldValue, newValue) => ReroutePort((uint)oldValue, (uint)newValue, true), name: "EXTIPSEL")
                },
                {(long)Registers.ExternalInterruptPinSelectLow, new DoubleWordRegister(this, 0x32103210)
                    .WithValueField(0, 32, changeCallback: (oldValue, newValue) => ReroutePin((uint)oldValue, (uint)newValue, false), name: "EXTIPINSEL")
                },
                {(long)Registers.ExternalInterruptPinSelectHigh, new DoubleWordRegister(this, 0x32103210)
                    .WithValueField(0, 32, changeCallback: (oldValue, newValue) => ReroutePin((uint)oldValue, (uint)newValue, true), name: "EXTIPINSEL")
                },
                {(long)Registers.ExternalInterruptRisingEdgeTrigger, new DoubleWordRegister(this)
                    .WithValueField(0, 16, changeCallback: (_, value) => SetEdgeSensitivity((uint)value, InterruptTrigger.RisingEdge))
                },
                {(long)Registers.ExternalInterruptFallingEdgeTrigger, new DoubleWordRegister(this)
                    .WithValueField(0, 16, changeCallback: (_, value) => SetEdgeSensitivity((uint)value, InterruptTrigger.FallingEdge))
                },
                {(long)Registers.InterruptFlag, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: (_) => BitHelper.GetValueFromBitsArray(externalInterrupt), name: "EXT")
                    .WithTag("EM4WU", 16, 16)
                },
                {(long)Registers.InterruptFlagSet, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write, writeCallback: (_, value) => UpdateExternalInterruptBits((uint)value, true), name: "EXT")
                    .WithTag("EM4WU", 16, 16)
                },
                {(long)Registers.InterruptFlagClear, new DoubleWordRegister(this)
                    .WithValueField(0, 16, writeCallback: (_, value) => UpdateExternalInterruptBits((uint)value, false), valueProviderCallback: (_) =>
                    {
                        var result = BitHelper.GetValueFromBitsArray(externalInterrupt);
                        for(var i = 0; i < NumberOfExternalInterrupts; ++i)
                        {
                            externalInterrupt[i] = false;
                        }
                        UpdateInterrupts();
                        return result;
                    }, name: "EXT")
                    .WithTag("EM4WU", 16, 16)
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithValueField(0, 16, writeCallback: (_, value) =>
                    {
                        Array.Copy(BitHelper.GetBits((uint)value), interruptEnable, NumberOfExternalInterrupts);
                        UpdateInterrupts();
                    },
                                    valueProviderCallback: (_) => BitHelper.GetValueFromBitsArray(interruptEnable), name: "EXT")
                    .WithTag("EM4WU", 16, 16)
                },
                {(long)Registers.ConfigurationLock, new DoubleWordRegister(this)
                    .WithValueField(0, 16, writeCallback: (_, value) => configurationLocked = (value != UnlockCode),
                                    valueProviderCallback: (_)=> configurationLocked ? 1 : 0u, name: "LOCKKEY")
                },
            };
            for(var i = 0; i < NumberOfPorts; ++i)
            {
                CreatePortRegisters(regs, i);
            }
            registers = new DoubleWordRegisterCollection(this, regs);
        }

        private void CreatePortRegisters(Dictionary<long, DoubleWordRegister> regs, int portNumber)
        {
            var regOffset = PortOffset * portNumber;
            var pinOffset = portNumber * NumberOfPins;
            regs.Add((long)Registers.PortAControl + regOffset, new DoubleWordRegister(this, 0x700070)
                     .WithTag("DRIVESTRENGTH", 0, 1)
                     .WithTag("SLEWRATE", 4, 3)
                     .WithTag("DINDIS", 12, 1)
                     .WithTag("DRIVESTRENGTHALT", 16, 1)
                     .WithTag("SLEWRATEALT", 20, 3)
                     .WithTag("DINDISALT", 28, 1)
                    );

            var gpioModeLow = new DoubleWordRegister(this);
            var gpioModeHigh = new DoubleWordRegister(this);

            for(var pinNumber = 0; pinNumber < 8; ++pinNumber)
            {
                pinModes[pinOffset + pinNumber] = gpioModeLow.DefineEnumField<PinMode>(pinNumber * 4, 4, name: "MODEX"); //TODO: pin locking
            }

            for(var pinNumber = 8; pinNumber < 16; ++pinNumber)
            {
                pinModes[pinOffset + pinNumber] = gpioModeHigh.DefineEnumField<PinMode>((pinNumber - 8) * 4, 4, name: "MODEX"); //TODO: pin locking
            }

            regs.Add((long)Registers.PortAModeLow + regOffset, gpioModeLow);
            regs.Add((long)Registers.PortAModeHigh + regOffset, gpioModeHigh);

            regs.Add((long)Registers.PortADataOut + regOffset, new DoubleWordRegister(this)
                     .WithValueField(0, 16,
                                     writeCallback: (_, newValue) =>
                                     {
                                         var bits = BitHelper.GetBits((uint)newValue);
                                         for(var i = 0; i < 16; i++)
                                         {
                                             var pin = pinOffset + i;
                                             if(IsOutput(pinModes[pin].Value) && unlockedPins[pin].Value)
                                             {
                                                 Connections[pin].Set(bits[i]);
                                             }
                                         }
                                     },
                                     valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(Connections.Where(x => x.Key >= 0).OrderBy(x => x.Key).Select(x => x.Value.IsSet))));

            regs.Add((long)Registers.PortADataOutToggle + regOffset, new DoubleWordRegister(this)
                     .WithValueField(0, 16, FieldMode.Write,
                                     writeCallback: (_, newValue) =>
                                     {
                                         var bits = BitHelper.GetSetBits(newValue);
                                         foreach(var bit in bits)
                                         {
                                             var pin = pinOffset + bit;
                                             if(IsOutput(pinModes[pin].Value) && unlockedPins[pin].Value)
                                             {
                                                 Connections[pin].Toggle();
                                             }
                                         }
                                     }));

            regs.Add((long)Registers.PortADataIn + regOffset, new DoubleWordRegister(this)
                     .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: (oldValue) => BitHelper.GetValueFromBitsArray(State.Skip(pinOffset).Take(NumberOfPins))));

            var unlockedPinsRegister = new DoubleWordRegister(this, 0xFFFF);
            for(var pinNumber = 0; pinNumber < NumberOfPins; ++pinNumber)
            {
                unlockedPins[pinNumber + pinOffset] = unlockedPinsRegister.DefineFlagField(pinNumber, FieldMode.WriteZeroToClear);
            }
            regs.Add((long)Registers.PortAUnlockedPins + regOffset, unlockedPinsRegister);
        }

        private void SetEdgeSensitivity(uint value, InterruptTrigger trigger)
        {
            var bits = BitHelper.GetBits(value);
            for(var i = 0; i < interruptTriggers.Length; ++i)
            {
                if(bits[i])
                {
                    interruptTriggers[i] |= trigger;
                }
                else
                {
                    interruptTriggers[i] ^= trigger;
                }
            }
        }

        private void UpdateExternalInterruptBits(uint bits, bool value)
        {
            var setBits = BitHelper.GetSetBits(bits);
            foreach(var bit in setBits)
            {
                externalInterrupt[bit] = value;
            }
            UpdateInterrupts();
        }

        private void ReroutePort(uint oldValue, uint newValue, bool isHighRegister)
        {
            var setPins = new HashSet<int>();
            for(var i = 0; i < 8; ++i)
            {
                var externalIrq = i + (isHighRegister ? 8 : 0);
                var portNewValue = (int)(newValue & (0xF << (i * 4))) >> (i * 4);
                var portOldValue = (int)(oldValue & (0xF << (i * 4))) >> (i * 4);
                if(portOldValue == portNewValue)
                {
                    continue;
                }
                var pinGroup = externalIrq / 4;
                var oldPinNumber = externalInterruptToPinMapping[externalIrq] + pinGroup * 4 + portOldValue * NumberOfPins;
                if(!setPins.Contains(oldPinNumber))
                {
                    //if we did not set this pin in this run, let's unset it
                    targetExternalPins[oldPinNumber] = 0;
                }
                var newPinNumber = externalInterruptToPinMapping[externalIrq] + pinGroup * 4 + portNewValue * NumberOfPins;
                targetExternalPins[newPinNumber] = externalIrq;
                setPins.Add(newPinNumber);
                //we keep it for the sake of ReroutePin method
                externalInterruptToPortMapping[externalIrq] = portNewValue;
            }
            UpdateInterrupts();
        }

        private void ReroutePin(uint oldValue, uint newValue, bool isHighRegister)
        {
            var setPins = new HashSet<int>();
            for(var i = 0; i < 8; ++i)
            {
                var externalIrq = i + (isHighRegister ? 8 : 0);
                var pinNewValue = (int)(newValue & (0x3 << (i * 4))) >> (i * 4);
                var pinOldValue = (int)(oldValue & (0x3 << (i * 4))) >> (i * 4);
                if(pinOldValue == pinNewValue)
                {
                    continue;
                }
                var pinGroup = externalIrq / 4;
                var oldPinNumber = pinOldValue + pinGroup * 4 + externalInterruptToPortMapping[externalIrq] * NumberOfPins;
                if(!setPins.Contains(oldPinNumber))
                {
                    //if we did not set this pin in this run, let's unset it
                    targetExternalPins[oldPinNumber] = 0;
                }
                var newPinNumber = pinNewValue + pinGroup * 4 + externalInterruptToPortMapping[externalIrq] * NumberOfPins;
                targetExternalPins[newPinNumber] = externalIrq;
                setPins.Add(newPinNumber);
                //we keep it for the sake of ReroutePort method
                externalInterruptToPinMapping[externalIrq] = pinNewValue;
            }
            UpdateInterrupts();
        }

        private bool IsOutput(PinMode mode)
        {
            return mode >= PinMode.PushPull;
        }

        private readonly int[] externalInterruptToPortMapping = new int[NumberOfExternalInterrupts];
        private readonly int[] externalInterruptToPinMapping = new int[NumberOfExternalInterrupts];
        private readonly bool[] externalInterrupt = new bool[NumberOfExternalInterrupts];
        private readonly bool[] previousState = new bool[NumberOfExternalInterrupts];
        private readonly bool[] interruptEnable = new bool[NumberOfExternalInterrupts];
        private readonly int[] targetExternalPins = new int[NumberOfPins * NumberOfPorts];
        private readonly InterruptTrigger[] interruptTriggers = new InterruptTrigger[NumberOfExternalInterrupts];
        private readonly IEnumRegisterField<PinMode>[] pinModes = new IEnumRegisterField<PinMode>[NumberOfPins * NumberOfPorts];
        private readonly IFlagRegisterField[] unlockedPins = new IFlagRegisterField[NumberOfPins * NumberOfPorts];
        private readonly object internalLock = new object();

        private DoubleWordRegisterCollection registers;
        private bool configurationLocked;

        private readonly HashSet<Registers> lockableRegisters = new HashSet<Registers>
        {
            Registers.PortAControl,
            Registers.PortAModeLow,
            Registers.PortAModeHigh,
            Registers.PortAUnlockedPins,
            Registers.PortAOverVoltageDisable,
            Registers.ExternalInterruptPortSelectLow,
            Registers.ExternalInterruptPortSelectHigh,
            Registers.ExternalInterruptPinSelectLow,
            Registers.ExternalInterruptPinSelectHigh,
            Registers.IORoutingPinEnable,
            Registers.IORoutingLocation,
            Registers.InputSense,
        };

        private const int NumberOfPorts = 6;
        private const int NumberOfPins = 16;
        private const int NumberOfExternalInterrupts = 16;
        private const int UnlockCode = 0xA534;
        private const int PortOffset = 0x30;

        [Flags]
        private enum InterruptTrigger
        {
            None = 0,
            FallingEdge = 1,
            RisingEdge = 2
        }

        private enum PinMode
        {
            //not setting the values explicitly, the implicit values are used. Do not reorder.
            Disabled,
            Input,
            InputPull,
            InputPullFilter,
            PushPull,
            PushPullAlt,
            WiredOr,
            WiredOrPullDown,
            WiredAnd,
            WiredAndFilter,
            WiredAndPullUp,
            WiredAndPullUpFilter,
            WiredAndAlt,
            WiredAndAltFilter,
            WiredAndAltPullUp,
            WiredAndAltPullUpFilter,
        }

        private enum Registers
        {
            //port configuration
            PortAControl                        = 0x0,
            PortAModeLow                        = 0x4,
            PortAModeHigh                       = 0x8,
            PortADataOut                        = 0xC,
            //reserved x 2
            PortADataOutToggle                  = 0x18,
            PortADataIn                         = 0x1C,
            PortAUnlockedPins                   = 0x20,
            //reserved x 1
            PortAOverVoltageDisable             = 0x28,
            //reserved x 1
            PortBControl                        = 0x30,
            PortBModeLow                        = 0x34,
            PortBModeHigh                       = 0x38,
            PortBDataOut                        = 0x3C,
            //reserved x 2
            PortBDataOutToggle                  = 0x48,
            PortBDataIn                         = 0x4C,
            PortBUnlockedPins                   = 0x50,
            //reserved x 1
            PortBOverVoltageDisable             = 0x58,
            //reserved x 1
            PortCControl                        = 0x60,
            PortCModeLow                        = 0x64,
            PortCModeHigh                       = 0x68,
            PortCDataOut                        = 0x6C,
            //reserved x 2
            PortCDataOutToggle                  = 0x78,
            PortCDataIn                         = 0x7C,
            PortCUnlockedPins                   = 0x80,
            //reserved x 1
            PortCOverVoltageDisable             = 0x88,
            //reserved x 1
            PortDControl                        = 0x90,
            PortDModeLow                        = 0x94,
            PortDModeHigh                       = 0x98,
            PortDDataOut                        = 0x9C,
            //reserved x 2
            PortDDataOutToggle                  = 0xA8,
            PortDDataIn                         = 0xAC,
            PortDUnlockedPins                   = 0xB0,
            //reserved x 1
            PortDOverVoltageDisable             = 0xB8,
            //reserved x 1
            PortEControl                        = 0xC0,
            PortEModeLow                        = 0xC4,
            PortEModeHigh                       = 0xC8,
            PortEDataOut                        = 0xCC,
            //reserved x 2
            PortEDataOutToggle                  = 0xD8,
            PortEDataIn                         = 0xDC,
            PortEUnlockedPins                   = 0xE0,
            //reserved x 1
            PortEOverVoltageDisable             = 0xE8,
            //reserved x 1
            PortFControl                        = 0xF0,
            PortFModeLow                        = 0xF4,
            PortFModeHigh                       = 0xF8,
            PortFDataOut                        = 0xFC,
            //reserved x 2
            PortFDataOutToggle                  = 0x108,
            PortFDataIn                         = 0x10C,
            PortFUnlockedPins                   = 0x110,
            //reserved x 1
            PortFOverVoltageDisable             = 0x118,
            //reserved x 1
            //global registers
            ExternalInterruptPortSelectLow      = 0x400,
            ExternalInterruptPortSelectHigh     = 0x404,
            ExternalInterruptPinSelectLow       = 0x408,
            ExternalInterruptPinSelectHigh      = 0x40C,
            ExternalInterruptRisingEdgeTrigger  = 0x410,
            ExternalInterruptFallingEdgeTrigger = 0x414,
            ExternalInterruptLevel              = 0x418,
            InterruptFlag                       = 0x41C,
            InterruptFlagSet                    = 0x420,
            InterruptFlagClear                  = 0x424,
            InterruptEnable                     = 0x428,
            EM4WakeUpEnable                     = 0x42C,
            IORoutingPinEnable                  = 0x440,
            IORoutingLocation                   = 0x444,
            InputSense                          = 0x450,
            ConfigurationLock                   = 0x454,
        }
    }
}
