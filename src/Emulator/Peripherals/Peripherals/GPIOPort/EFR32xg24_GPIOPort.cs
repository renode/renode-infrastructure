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
    public class EFR32xg24_GPIOPort : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xg24_GPIOPort(Machine machine) : base(machine, NumberOfPins * NumberOfPorts)
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
                //no clear as it must be set manually with InterruptFlag_Clr
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
                    .WithValueField(0, 12, changeCallback: (_, value) => SetEdgeSensitivity((uint)value, InterruptTrigger.RisingEdge))
                },
                {(long)Registers.ExternalInterruptFallingEdgeTrigger, new DoubleWordRegister(this)
                    .WithValueField(0, 12, changeCallback: (_, value) => SetEdgeSensitivity((uint)value, InterruptTrigger.FallingEdge))
                },
                {(long)Registers.InterruptFlag, new DoubleWordRegister(this)
                    .WithValueField(0, 12, FieldMode.Read, valueProviderCallback: (_) => BitHelper.GetValueFromBitsArray(externalInterrupt), name: "EXT")
                    .WithTag("EM4WU", 16, 12)
                },
                {(long)Registers.InterruptFlag_Set, new DoubleWordRegister(this)
                    .WithValueField(0, 12, FieldMode.Write, writeCallback: (_, value) => UpdateExternalInterruptBits((uint)value, true), name: "EXT")
                    .WithTag("EM4WU", 16, 12)
                },
                {(long)Registers.InterruptFlag_Clr, new DoubleWordRegister(this)
                    .WithValueField(0, 12, writeCallback: (_, value) => UpdateExternalInterruptBits((uint)value, false), valueProviderCallback: (_) =>
                    {
                        var result = BitHelper.GetValueFromBitsArray(externalInterrupt);
                        for(var i = 0; i < NumberOfExternalInterrupts; ++i)
                        {
                            externalInterrupt[i] = false;
                        }
                        UpdateInterrupts();
                        return result;
                    }, name: "EXT")
                    .WithTag("EM4WU", 16, 12)
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithValueField(0, 12, writeCallback: (_, value) =>
                    {
                        Array.Copy(BitHelper.GetBits((uint)value), interruptEnable, NumberOfExternalInterrupts);
                        UpdateInterrupts();
                    },
                                    valueProviderCallback: (_) => BitHelper.GetValueFromBitsArray(interruptEnable), name: "EXT")
                    .WithTag("EM4WU", 16, 16)
                },
                {(long)Registers.ConfigurationLock, new DoubleWordRegister(this)
                    .WithValueField(0, 16, writeCallback: (_, value) => configurationLocked = (value != UnlockCode), name: "LOCKKEY")
                },
                {(long)Registers.ConfigurationLockStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 1, FieldMode.Read, valueProviderCallback: (_)=> configurationLocked ? 1 : 0u, name: "LOCK")
                },
                {(long)Registers.GPIO_USART0_ROUTEEN, new DoubleWordRegister(this)
                    .WithFlag(0, out USART0_ROUTEEN.cspen, name: "CSPEN")
                    .WithFlag(1, out USART0_ROUTEEN.rtspen, name: "RTSPEN")
                    .WithFlag(2, out USART0_ROUTEEN.rxpen, name: "RXPEN")
                    .WithFlag(3, out USART0_ROUTEEN.sclkpen, name: "SCLKPEN")
                    .WithFlag(4, out USART0_ROUTEEN.txpen, name: "TXPEN")
                },
                {(long)Registers.GPIO_USART0_ROUTEEN_Set, new DoubleWordRegister(this)
                    .WithFlag(0, writeCallback: (_, val) => USART0_ROUTEEN.cspen.Value = val?val:_, name: "CSPEN")
                    .WithFlag(1, writeCallback: (_, val) => USART0_ROUTEEN.rtspen.Value = val?val:_, name: "RTSPEN")
                    .WithFlag(2, writeCallback: (_, val) => USART0_ROUTEEN.rxpen.Value = val?val:_, name: "RXPEN")
                    .WithFlag(3, writeCallback: (_, val) => USART0_ROUTEEN.sclkpen.Value = val?val:_, name: "SCLKPEN")
                    .WithFlag(4, writeCallback: (_, val) => USART0_ROUTEEN.txpen.Value = val?val:_, name: "TXPEN")
                },
                {(long)Registers.GPIO_USART0_TXROUTE, new DoubleWordRegister(this)
                    .WithTag("PORT", 0, 2)
                    .WithReservedBits(2, 14)
                    .WithTag("PIN", 16, 4)
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.GPIO_EUSART0_TXROUTE, new DoubleWordRegister(this)
                    .WithValueField(0, 2, out EUSART0_TXROUTE.port, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out EUSART0_TXROUTE.pin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.GPIO_EUSART0_RXROUTE, new DoubleWordRegister(this)
                    .WithValueField(0, 2, out EUSART0_RXROUTE.port, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out EUSART0_RXROUTE.pin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.GPIO_EUSART0_ROUTEEN, new DoubleWordRegister(this)
                    .WithFlag(0, out EUSART0_ROUTEEN.cspen, name: "CSPEN")
                    .WithFlag(1, out EUSART0_ROUTEEN.rtspen, name: "RTSPEN")
                    .WithFlag(2, out EUSART0_ROUTEEN.rxpen, name: "RXPEN")
                    .WithFlag(3, out EUSART0_ROUTEEN.sclkpen, name: "SCLKPEN")
                    .WithFlag(4, out EUSART0_ROUTEEN.txpen, name: "TXPEN")
                },
                {(long)Registers.GPIO_EUSART0_ROUTEEN_Set, new DoubleWordRegister(this)
                    .WithFlag(0, writeCallback: (_, val) => EUSART0_ROUTEEN.cspen.Value = val?val:_, name: "CSPEN")
                    .WithFlag(1, writeCallback: (_, val) => EUSART0_ROUTEEN.rtspen.Value = val?val:_, name: "RTSPEN")
                    .WithFlag(2, writeCallback: (_, val) => EUSART0_ROUTEEN.rxpen.Value = val?val:_, name: "RXPEN")
                    .WithFlag(3, writeCallback: (_, val) => EUSART0_ROUTEEN.sclkpen.Value = val?val:_, name: "SCLKPEN")
                    .WithFlag(4, writeCallback: (_, val) => EUSART0_ROUTEEN.txpen.Value = val?val:_, name: "TXPEN")
                },
                {(long)Registers.GPIO_EUSART0_ROUTEEN_Clr, new DoubleWordRegister(this)
                    .WithFlag(0, writeCallback: (_, val) => EUSART0_ROUTEEN.cspen.Value = val?!val:_, name: "CSPEN")
                    .WithFlag(1, writeCallback: (_, val) => EUSART0_ROUTEEN.rtspen.Value = val?!val:_, name: "RTSPEN")
                    .WithFlag(2, writeCallback: (_, val) => EUSART0_ROUTEEN.rxpen.Value = val?!val:_, name: "RXPEN")
                    .WithFlag(3, writeCallback: (_, val) => EUSART0_ROUTEEN.sclkpen.Value = val?!val:_, name: "SCLKPEN")
                    .WithFlag(4, writeCallback: (_, val) => EUSART0_ROUTEEN.txpen.Value = val?!val:_, name: "TXPEN")
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
                     .WithTag("SLEWRATE", 4, 3)
                     .WithTag("DINDIS", 12, 1)
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
                     .WithValueField(0, 10,
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

            regs.Add((long)Registers.PortADataIn + regOffset, new DoubleWordRegister(this)
                     .WithValueField(0, 10, FieldMode.Read, valueProviderCallback: (oldValue) => BitHelper.GetValueFromBitsArray(State.Skip(pinOffset).Take(NumberOfPins))));

            regs.Add((long)Registers.PortADataOut_Set + regOffset, new DoubleWordRegister(this)
                     .WithValueField(0, 10, FieldMode.Write,
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

            regs.Add((long)Registers.PortADataOut_Clr + regOffset, new DoubleWordRegister(this)
                     .WithValueField(0, 10, FieldMode.Write,
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

            regs.Add((long)Registers.PortADataOut_Tgl + regOffset, new DoubleWordRegister(this)
                     .WithValueField(0, 10, FieldMode.Write,
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

            var unlockedPinsRegister = new DoubleWordRegister(this, 0xFFFF);
            for(var pinNumber = 0; pinNumber < NumberOfPins; ++pinNumber)
            {
                unlockedPins[pinNumber + pinOffset] = unlockedPinsRegister.DefineFlagField(pinNumber, FieldMode.WriteZeroToClear);
            }
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
            Registers.ExternalInterruptPortSelectLow,
            Registers.ExternalInterruptPortSelectHigh,
            Registers.ExternalInterruptPinSelectLow,
            Registers.ExternalInterruptPinSelectHigh,
            Registers.DebugRoutePinEn,
            Registers.DebugRoutePinEn,
        };

        private const int NumberOfPorts = 4;
        private const int NumberOfPins = 16;
        private const int NumberOfExternalInterrupts = 12;
        private const int UnlockCode = 0xA534;
        private const int PortOffset = 0x30;

        [Flags]
        private enum InterruptTrigger
        {
            None = 0,
            FallingEdge = 1,
            RisingEdge = 2
        }

        private GPIO0_ROUTE_SEL EUSART0_TXROUTE;
        private GPIO0_ROUTE_SEL EUSART0_RXROUTE;

        struct GPIO0_ROUTE_SEL
        {
            public IValueRegisterField pin;
            public IValueRegisterField port;

        }
        
        private GPIO_ROUTEEN EUSART0_ROUTEEN;
        private GPIO_ROUTEEN USART0_ROUTEEN;

        struct GPIO_ROUTEEN
        {
            public IFlagRegisterField txpen;
            public IFlagRegisterField sclkpen;
            public IFlagRegisterField rxpen;
            public IFlagRegisterField rtspen;
            public IFlagRegisterField cspen;
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
            IpVersion                                       = 0x0,
            PortAControl                                    = 0x30,
            PortAModeLow                                    = 0x34,
            PortAModeHigh                                   = 0x3C,
            PortADataOut                                    = 0x40,
            PortADataIn                                     = 0x44,
            PortBControl                                    = 0x60,
            PortBModeLow                                    = 0x64,
            PortBModeHigh                                   = 0x6C,
            PortBDataOut                                    = 0x70,
            PortBDataIn                                     = 0x74,
            PortCControl                                    = 0x90,
            PortCModeLow                                    = 0x94,
            PortCModeHigh                                   = 0x9C,
            PortCDataOut                                    = 0xA0,
            PortCDataIn                                     = 0xA4,
            PortDControl                                    = 0xC0,
            PortDModeLow                                    = 0xC4,
            PortDModeHigh                                   = 0xCC,
            PortDDataOut                                    = 0xD0,
            PortDDataIn                                     = 0xD4,
            ConfigurationLock                               = 0x300,
            ConfigurationLockStatus                         = 0x310,
            ABusAllocation                                  = 0x320,
            BBusAllocation                                  = 0x324,
            CBusAllocation                                  = 0x328,
            ExternalInterruptPortSelectLow                  = 0x400,
            ExternalInterruptPortSelectHigh                 = 0x404,
            ExternalInterruptPinSelectLow                   = 0x408,
            ExternalInterruptPinSelectHigh                  = 0x40C,
            ExternalInterruptRisingEdgeTrigger              = 0x410,
            ExternalInterruptFallingEdgeTrigger             = 0x414,
            InterruptFlag                                   = 0x420,
            InterruptEnable                                 = 0x424,
            EM4WakeUpEnable                                 = 0x42C,
            EM4WakeUpPolarity                               = 0x430,
            DebugRoutePinEn                                 = 0x440,
            TraceRoutePinEn                                 = 0x444,

            GPIO_ACMP0_ROUTEEN                              = 0x450,    
            GPIO_ACMP0_ACMPOUTROUTE                         = 0x454,            
            GPIO_ACMP1_ROUTEEN                              = 0x45C,    
            GPIO_ACMP1_ACMPOUTROUTE                         = 0x460,            
            GPIO_CMU_ROUTEEN                                = 0x468,    
            GPIO_CMU_CLKIN0ROUTE                            = 0x46C,        
            GPIO_CMU_CLKOUT0ROUTE                           = 0x470,        
            GPIO_CMU_CLKOUT1ROUTE                           = 0x474,        
            GPIO_CMU_CLKOUT2ROUTE                           = 0x478,        
            GPIO_EUSART0_ROUTEEN                            = 0x494,        
            GPIO_EUSART0_CSROUTE                            = 0x498,        
            GPIO_EUSART0_CTSROUTE                           = 0x49C,        
            GPIO_EUSART0_RTSROUTE                           = 0x4A0,        
            GPIO_EUSART0_RXROUTE                            = 0x4A4,        
            GPIO_EUSART0_SCLKROUTE                          = 0x4A8,        
            GPIO_EUSART0_TXROUTE                            = 0x4AC,        
            GPIO_EUSART1_ROUTEEN                            = 0x4B4,        
            GPIO_EUSART1_CSROUTE                            = 0x4B8,        
            GPIO_EUSART1_CTSROUTE                           = 0x4BC,        
            GPIO_EUSART1_RTSROUTE                           = 0x4C0,        
            GPIO_EUSART1_RXROUTE                            = 0x4C4,        
            GPIO_EUSART1_SCLKROUTE                          = 0x4C8,        
            GPIO_EUSART1_TXROUTE                            = 0x4CC,        
            GPIO_FRC_ROUTEEN                                = 0x4D4,    
            GPIO_FRC_DCLKROUTE                              = 0x4D8,    
            GPIO_FRC_DFRAMEROUTE                            = 0x4DC,        
            GPIO_FRC_DOUTROUTE                              = 0x4E0,    
            GPIO_I2C0_ROUTEEN                               = 0x4E8,    
            GPIO_I2C0_SCLROUTE                              = 0x4EC,    
            GPIO_I2C0_SDAROUTE                              = 0x4F0,    
            GPIO_I2C1_ROUTEEN                               = 0x4F8,    
            GPIO_I2C1_SCLROUTE                              = 0x4FC,    
            GPIO_I2C1_SDAROUTE                              = 0x500,    
            GPIO_KEYSCAN_ROUTEEN                            = 0x508,        
            GPIO_KEYSCAN_COLOUT0ROUTE                       = 0x50C,            
            GPIO_KEYSCAN_COLOUT1ROUTE                       = 0x510,            
            GPIO_KEYSCAN_COLOUT2ROUTE                       = 0x514,            
            GPIO_KEYSCAN_COLOUT3ROUTE                       = 0x518,            
            GPIO_KEYSCAN_COLOUT4ROUTE                       = 0x51C,            
            GPIO_KEYSCAN_COLOUT5ROUTE                       = 0x520,            
            GPIO_KEYSCAN_COLOUT6ROUTE                       = 0x524,            
            GPIO_KEYSCAN_COLOUT7ROUTE                       = 0x528,            
            GPIO_KEYSCAN_ROWSENSE0ROUTE                     = 0x52C,                
            GPIO_KEYSCAN_ROWSENSE1ROUTE                     = 0x530,                
            GPIO_KEYSCAN_ROWSENSE2ROUTE                     = 0x534,                
            GPIO_KEYSCAN_ROWSENSE3ROUTE                     = 0x538,                
            GPIO_KEYSCAN_ROWSENSE4ROUTE                     = 0x53C,                
            GPIO_KEYSCAN_ROWSENSE5ROUTE                     = 0x540,                
            GPIO_LETIMER_ROUTEEN                            = 0x548,        
            GPIO_LETIMER_OUT0ROUTE                          = 0x54C,        
            GPIO_LETIMER_OUT1ROUTE                          = 0x550,        
            GPIO_MODEM_ROUTEEN                              = 0x558,    
            GPIO_MODEM_ANT0ROUTE                            = 0x55C,        
            GPIO_MODEM_ANT1ROUTE                            = 0x560,        
            GPIO_MODEM_ANTROLLOVERROUTE                     = 0x564,                
            GPIO_MODEM_ANTRR0ROUTE                          = 0x568,        
            GPIO_MODEM_ANTRR1ROUTE                          = 0x56C,        
            GPIO_MODEM_ANTRR2ROUTE                          = 0x570,        
            GPIO_MODEM_ANTRR3ROUTE                          = 0x574,        
            GPIO_MODEM_ANTRR4ROUTE                          = 0x578,        
            GPIO_MODEM_ANTRR5ROUTE                          = 0x57C,        
            GPIO_MODEM_ANTSWENROUTE                         = 0x580,            
            GPIO_MODEM_ANTSWUSROUTE                         = 0x584,            
            GPIO_MODEM_ANTTRIGROUTE                         = 0x588,            
            GPIO_MODEM_ANTTRIGSTOPROUTE                     = 0x58C,                
            GPIO_MODEM_DCLKROUTE                            = 0x590,        
            GPIO_MODEM_DINROUTE                             = 0x594,        
            GPIO_MODEM_DOUTROUTE                            = 0x598,        
            GPIO_PCNT0_S0INROUTE                            = 0x5A4,        
            GPIO_PCNT0_S1INROUTE                            = 0x5A8,        
            GPIO_PRS0_ROUTEEN                               = 0x5B0,    
            GPIO_PRS0_ASYNCH0ROUTE                          = 0x5B4,        
            GPIO_PRS0_ASYNCH1ROUTE                          = 0x5B8,        
            GPIO_PRS0_ASYNCH2ROUTE                          = 0x5BC,        
            GPIO_PRS0_ASYNCH3ROUTE                          = 0x5C0,        
            GPIO_PRS0_ASYNCH4ROUTE                          = 0x5C4,        
            GPIO_PRS0_ASYNCH5ROUTE                          = 0x5C8,        
            GPIO_PRS0_ASYNCH6ROUTE                          = 0x5CC,        
            GPIO_PRS0_ASYNCH7ROUTE                          = 0x5D0,        
            GPIO_PRS0_ASYNCH8ROUTE                          = 0x5D4,        
            GPIO_PRS0_ASYNCH9ROUTE                          = 0x5D8,        
            GPIO_PRS0_ASYNCH10ROUT                          = 0x5DC,        
            GPIO_PRS0_ASYNCH11ROUT                          = 0x5E0,        
            GPIO_PRS0_ASYNCH12ROUTE                         = 0x5E4,            
            GPIO_PRS0_ASYNCH13ROUT                          = 0x5E8,        
            GPIO_PRS0_ASYNCH14ROUTE                         = 0x5EC,            
            GPIO_PRS0_ASYNCH15ROUTE                         = 0x5F0,            
            GPIO_PRS0_SYNCH0ROUTE                           = 0x5F4,        
            GPIO_PRS0_SYNCH1ROUTE                           = 0x5F8,        
            GPIO_PRS0_SYNCH2ROUTE                           = 0x5FC,        
            GPIO_PRS0_SYNCH3ROUTE                           = 0x600,        
            GPIO_SYXO0_BUFOUTREQINASYNCROUTE                = 0x678,                    
            GPIO_TIMER0_ROUTEEN                             = 0x680,        
            GPIO_TIMER0_CC0ROUTE                            = 0x684,        
            GPIO_TIMER0_CC1ROUTE                            = 0x688,        
            GPIO_TIMER0_CC2ROUTE                            = 0x68C,        
            GPIO_TIMER0_CDTI0ROUTE                          = 0x690,        
            GPIO_TIMER0_CDTI1ROUTE                          = 0x694,        
            GPIO_TIMER0_CDTI2ROUTE                          = 0x698,        
            GPIO_TIMER1_ROUTEEN                             = 0x6A0,        
            GPIO_TIMER1_CC0ROUTE                            = 0x6A4,        
            GPIO_TIMER1_CC1ROUTE                            = 0x6A8,        
            GPIO_TIMER1_CC2ROUTE                            = 0x6AC,        
            GPIO_TIMER1_CDTI0ROUTE                          = 0x6B0,        
            GPIO_TIMER1_CDTI1ROUTE                          = 0x6B4,        
            GPIO_TIMER1_CDTI2ROUTE                          = 0x6B8,        
            GPIO_TIMER2_ROUTEEN                             = 0x6C0,        
            GPIO_TIMER2_CC0ROUTE                            = 0x6C4,        
            GPIO_TIMER2_CC1ROUTE                            = 0x6C8,        
            GPIO_TIMER2_CC2ROUTE                            = 0x6CC,        
            GPIO_TIMER2_CDTI0ROUTE                          = 0x6D0,        
            GPIO_TIMER2_CDTI1ROUTE                          = 0x6D4,        
            GPIO_TIMER2_CDTI2ROUTE                          = 0x6D8,        
            GPIO_TIMER3_ROUTEEN                             = 0x6E0,        
            GPIO_TIMER3_CC0ROUTE                            = 0x6E4,        
            GPIO_TIMER3_CC1ROUTE                            = 0x6E8,        
            GPIO_TIMER3_CC2ROUTE                            = 0x6EC,        
            GPIO_TIMER3_CDTI0ROUTE                          = 0x6F0,        
            GPIO_TIMER3_CDTI1ROUTE                          = 0x6F4,        
            GPIO_TIMER3_CDTI2ROUTE                          = 0x6F8,        
            GPIO_TIMER4_ROUTEEN                             = 0x700,        
            GPIO_TIMER4_CC0ROUTE                            = 0x704,        
            GPIO_TIMER4_CC1ROUTE                            = 0x708,        
            GPIO_TIMER4_CC2ROUTE                            = 0x70C,        
            GPIO_TIMER4_CDTI0ROUTE                          = 0x710,        
            GPIO_TIMER4_CDTI1ROUTE                          = 0x714,        
            GPIO_TIMER4_CDTI2ROUTE                          = 0x718,        
            GPIO_USART0_ROUTEEN                             = 0x720,        
            GPIO_USART0_CSROUTE                             = 0x724,        
            GPIO_USART0_CTSROUTE                            = 0x728,        
            GPIO_USART0_RTSROUTE                            = 0x72C,        
            GPIO_USART0_RXROUTE                             = 0x730,        
            GPIO_USART0_CLKROUTE                            = 0x734,        
            GPIO_USART0_TXROUTE                             = 0x738,        


            IpVersion_Set = 0x1000,
            PortAControl_Set = 0x1030,
            PortAModeLow_Set = 0x1034,
            PortAModeHigh_Set = 0x103c,
            PortADataOut_Set = 0x1040,
            PortADataIn_Set = 0x1044,
            PortBControl_Set = 0x1060,
            PortBModeLow_Set = 0x1064,
            PortBModeHigh_Set = 0x106c,
            PortBDataOut_Set = 0x1070,
            PortBDataIn_Set = 0x1074,
            PortCControl_Set = 0x1090,
            PortCModeLow_Set = 0x1094,
            PortCModeHigh_Set = 0x109c,
            PortCDataOut_Set = 0x10a0,
            PortCDataIn_Set = 0x10a4,
            PortDControl_Set = 0x10c0,
            PortDModeLow_Set = 0x10c4,
            PortDModeHigh_Set = 0x10cc,
            PortDDataOut_Set = 0x10d0,
            PortDDataIn_Set = 0x10d4,
            ConfigurationLock_Set = 0x1300,
            ConfigurationLockStatus_Set = 0x1310,
            ABusAllocation_Set = 0x1320,
            BBusAllocation_Set = 0x1324,
            CBusAllocation_Set = 0x1328,
            ExternalInterruptPortSelectLow_Set = 0x1400,
            ExternalInterruptPortSelectHigh_Set = 0x1404,
            ExternalInterruptPinSelectLow_Set = 0x1408,
            ExternalInterruptPinSelectHigh_Set = 0x140c,
            ExternalInterruptRisingEdgeTrigger_Set = 0x1410,
            ExternalInterruptFallingEdgeTrigger_Set = 0x1414,
            InterruptFlag_Set = 0x1420,
            InterruptEnable_Set = 0x1424,
            EM4WakeUpEnable_Set = 0x142c,
            EM4WakeUpPolarity_Set = 0x1430,
            DebugRoutePinEn_Set = 0x1440,
            TraceRoutePinEn_Set = 0x1444,
            GPIO_ACMP0_ROUTEEN_Set = 0x1450,
            GPIO_ACMP0_ACMPOUTROUTE_Set = 0x1454,
            GPIO_ACMP1_ROUTEEN_Set = 0x145c,
            GPIO_ACMP1_ACMPOUTROUTE_Set = 0x1460,
            GPIO_CMU_ROUTEEN_Set = 0x1468,
            GPIO_CMU_CLKIN0ROUTE_Set = 0x146c,
            GPIO_CMU_CLKOUT0ROUTE_Set = 0x1470,
            GPIO_CMU_CLKOUT1ROUTE_Set = 0x1474,
            GPIO_CMU_CLKOUT2ROUTE_Set = 0x1478,
            GPIO_EUSART0_ROUTEEN_Set = 0x1494,
            GPIO_EUSART0_CSROUTE_Set = 0x1498,
            GPIO_EUSART0_CTSROUTE_Set = 0x149c,
            GPIO_EUSART0_RTSROUTE_Set = 0x14a0,
            GPIO_EUSART0_RXROUTE_Set = 0x14a4,
            GPIO_EUSART0_SCLKROUTE_Set = 0x14a8,
            GPIO_EUSART0_TXROUTE_Set = 0x14ac,
            GPIO_EUSART1_ROUTEEN_Set = 0x14b4,
            GPIO_EUSART1_CSROUTE_Set = 0x14b8,
            GPIO_EUSART1_CTSROUTE_Set = 0x14bc,
            GPIO_EUSART1_RTSROUTE_Set = 0x14c0,
            GPIO_EUSART1_RXROUTE_Set = 0x14c4,
            GPIO_EUSART1_SCLKROUTE_Set = 0x14c8,
            GPIO_EUSART1_TXROUTE_Set = 0x14cc,
            GPIO_FRC_ROUTEEN_Set = 0x14d4,
            GPIO_FRC_DCLKROUTE_Set = 0x14d8,
            GPIO_FRC_DFRAMEROUTE_Set = 0x14dc,
            GPIO_FRC_DOUTROUTE_Set = 0x14e0,
            GPIO_I2C0_ROUTEEN_Set = 0x14e8,
            GPIO_I2C0_SCLROUTE_Set = 0x14ec,
            GPIO_I2C0_SDAROUTE_Set = 0x14f0,
            GPIO_I2C1_ROUTEEN_Set = 0x14f8,
            GPIO_I2C1_SCLROUTE_Set = 0x14fc,
            GPIO_I2C1_SDAROUTE_Set = 0x1500,
            GPIO_KEYSCAN_ROUTEEN_Set = 0x1508,
            GPIO_KEYSCAN_COLOUT0ROUTE_Set = 0x150c,
            GPIO_KEYSCAN_COLOUT1ROUTE_Set = 0x1510,
            GPIO_KEYSCAN_COLOUT2ROUTE_Set = 0x1514,
            GPIO_KEYSCAN_COLOUT3ROUTE_Set = 0x1518,
            GPIO_KEYSCAN_COLOUT4ROUTE_Set = 0x151c,
            GPIO_KEYSCAN_COLOUT5ROUTE_Set = 0x1520,
            GPIO_KEYSCAN_COLOUT6ROUTE_Set = 0x1524,
            GPIO_KEYSCAN_COLOUT7ROUTE_Set = 0x1528,
            GPIO_KEYSCAN_ROWSENSE0ROUTE_Set = 0x152c,
            GPIO_KEYSCAN_ROWSENSE1ROUTE_Set = 0x1530,
            GPIO_KEYSCAN_ROWSENSE2ROUTE_Set = 0x1534,
            GPIO_KEYSCAN_ROWSENSE3ROUTE_Set = 0x1538,
            GPIO_KEYSCAN_ROWSENSE4ROUTE_Set = 0x153c,
            GPIO_KEYSCAN_ROWSENSE5ROUTE_Set = 0x1540,
            GPIO_LETIMER_ROUTEEN_Set = 0x1548,
            GPIO_LETIMER_OUT0ROUTE_Set = 0x154c,
            GPIO_LETIMER_OUT1ROUTE_Set = 0x1550,
            GPIO_MODEM_ROUTEEN_Set = 0x1558,
            GPIO_MODEM_ANT0ROUTE_Set = 0x155c,
            GPIO_MODEM_ANT1ROUTE_Set = 0x1560,
            GPIO_MODEM_ANTROLLOVERROUTE_Set = 0x1564,
            GPIO_MODEM_ANTRR0ROUTE_Set = 0x1568,
            GPIO_MODEM_ANTRR1ROUTE_Set = 0x156c,
            GPIO_MODEM_ANTRR2ROUTE_Set = 0x1570,
            GPIO_MODEM_ANTRR3ROUTE_Set = 0x1574,
            GPIO_MODEM_ANTRR4ROUTE_Set = 0x1578,
            GPIO_MODEM_ANTRR5ROUTE_Set = 0x157c,
            GPIO_MODEM_ANTSWENROUTE_Set = 0x1580,
            GPIO_MODEM_ANTSWUSROUTE_Set = 0x1584,
            GPIO_MODEM_ANTTRIGROUTE_Set = 0x1588,
            GPIO_MODEM_ANTTRIGSTOPROUTE_Set = 0x158c,
            GPIO_MODEM_DCLKROUTE_Set = 0x1590,
            GPIO_MODEM_DINROUTE_Set = 0x1594,
            GPIO_MODEM_DOUTROUTE_Set = 0x1598,
            GPIO_PCNT0_S0INROUTE_Set = 0x15a4,
            GPIO_PCNT0_S1INROUTE_Set = 0x15a8,
            GPIO_PRS0_ROUTEEN_Set = 0x15b0,
            GPIO_PRS0_ASYNCH0ROUTE_Set = 0x15b4,
            GPIO_PRS0_ASYNCH1ROUTE_Set = 0x15b8,
            GPIO_PRS0_ASYNCH2ROUTE_Set = 0x15bc,
            GPIO_PRS0_ASYNCH3ROUTE_Set = 0x15c0,
            GPIO_PRS0_ASYNCH4ROUTE_Set = 0x15c4,
            GPIO_PRS0_ASYNCH5ROUTE_Set = 0x15c8,
            GPIO_PRS0_ASYNCH6ROUTE_Set = 0x15cc,
            GPIO_PRS0_ASYNCH7ROUTE_Set = 0x15d0,
            GPIO_PRS0_ASYNCH8ROUTE_Set = 0x15d4,
            GPIO_PRS0_ASYNCH9ROUTE_Set = 0x15d8,
            GPIO_PRS0_ASYNCH10ROUT_Set = 0x15dc,
            GPIO_PRS0_ASYNCH11ROUT_Set = 0x15e0,
            GPIO_PRS0_ASYNCH12ROUTE_Set = 0x15e4,
            GPIO_PRS0_ASYNCH13ROUT_Set = 0x15e8,
            GPIO_PRS0_ASYNCH14ROUTE_Set = 0x15ec,
            GPIO_PRS0_ASYNCH15ROUTE_Set = 0x15f0,
            GPIO_PRS0_SYNCH0ROUTE_Set = 0x15f4,
            GPIO_PRS0_SYNCH1ROUTE_Set = 0x15f8,
            GPIO_PRS0_SYNCH2ROUTE_Set = 0x15fc,
            GPIO_PRS0_SYNCH3ROUTE_Set = 0x1600,
            GPIO_SYXO0_BUFOUTREQINASYNCROUTE_Set = 0x1678,
            GPIO_TIMER0_ROUTEEN_Set = 0x1680,
            GPIO_TIMER0_CC0ROUTE_Set = 0x1684,
            GPIO_TIMER0_CC1ROUTE_Set = 0x1688,
            GPIO_TIMER0_CC2ROUTE_Set = 0x168c,
            GPIO_TIMER0_CDTI0ROUTE_Set = 0x1690,
            GPIO_TIMER0_CDTI1ROUTE_Set = 0x1694,
            GPIO_TIMER0_CDTI2ROUTE_Set = 0x1698,
            GPIO_TIMER1_ROUTEEN_Set = 0x16a0,
            GPIO_TIMER1_CC0ROUTE_Set = 0x16a4,
            GPIO_TIMER1_CC1ROUTE_Set = 0x16a8,
            GPIO_TIMER1_CC2ROUTE_Set = 0x16ac,
            GPIO_TIMER1_CDTI0ROUTE_Set = 0x16b0,
            GPIO_TIMER1_CDTI1ROUTE_Set = 0x16b4,
            GPIO_TIMER1_CDTI2ROUTE_Set = 0x16b8,
            GPIO_TIMER2_ROUTEEN_Set = 0x16c0,
            GPIO_TIMER2_CC0ROUTE_Set = 0x16c4,
            GPIO_TIMER2_CC1ROUTE_Set = 0x16c8,
            GPIO_TIMER2_CC2ROUTE_Set = 0x16cc,
            GPIO_TIMER2_CDTI0ROUTE_Set = 0x16d0,
            GPIO_TIMER2_CDTI1ROUTE_Set = 0x16d4,
            GPIO_TIMER2_CDTI2ROUTE_Set = 0x16d8,
            GPIO_TIMER3_ROUTEEN_Set = 0x16e0,
            GPIO_TIMER3_CC0ROUTE_Set = 0x16e4,
            GPIO_TIMER3_CC1ROUTE_Set = 0x16e8,
            GPIO_TIMER3_CC2ROUTE_Set = 0x16ec,
            GPIO_TIMER3_CDTI0ROUTE_Set = 0x16f0,
            GPIO_TIMER3_CDTI1ROUTE_Set = 0x16f4,
            GPIO_TIMER3_CDTI2ROUTE_Set = 0x16f8,
            GPIO_TIMER4_ROUTEEN_Set = 0x1700,
            GPIO_TIMER4_CC0ROUTE_Set = 0x1704,
            GPIO_TIMER4_CC1ROUTE_Set = 0x1708,
            GPIO_TIMER4_CC2ROUTE_Set = 0x170c,
            GPIO_TIMER4_CDTI0ROUTE_Set = 0x1710,
            GPIO_TIMER4_CDTI1ROUTE_Set = 0x1714,
            GPIO_TIMER4_CDTI2ROUTE_Set = 0x1718,
            GPIO_USART0_ROUTEEN_Set = 0x1720,
            GPIO_USART0_CSROUTE_Set = 0x1724,
            GPIO_USART0_CTSROUTE_Set = 0x1728,
            GPIO_USART0_RTSROUTE_Set = 0x172c,
            GPIO_USART0_RXROUTE_Set = 0x1730,
            GPIO_USART0_CLKROUTE_Set = 0x1734,
            GPIO_USART0_TXROUTE_Set = 0x1738,
            

            IpVersion_Clr = 0x2000,
            PortAControl_Clr = 0x2030,
            PortAModeLow_Clr = 0x2034,
            PortAModeHigh_Clr = 0x203c,
            PortADataOut_Clr = 0x2040,
            PortADataIn_Clr = 0x2044,
            PortBControl_Clr = 0x2060,
            PortBModeLow_Clr = 0x2064,
            PortBModeHigh_Clr = 0x206c,
            PortBDataOut_Clr = 0x2070,
            PortBDataIn_Clr = 0x2074,
            PortCControl_Clr = 0x2090,
            PortCModeLow_Clr = 0x2094,
            PortCModeHigh_Clr = 0x209c,
            PortCDataOut_Clr = 0x20a0,
            PortCDataIn_Clr = 0x20a4,
            PortDControl_Clr = 0x20c0,
            PortDModeLow_Clr = 0x20c4,
            PortDModeHigh_Clr = 0x20cc,
            PortDDataOut_Clr = 0x20d0,
            PortDDataIn_Clr = 0x20d4,
            ConfigurationLock_Clr = 0x2300,
            ConfigurationLockStatus_Clr = 0x2310,
            ABusAllocation_Clr = 0x2320,
            BBusAllocation_Clr = 0x2324,
            CBusAllocation_Clr = 0x2328,
            ExternalInterruptPortSelectLow_Clr = 0x2400,
            ExternalInterruptPortSelectHigh_Clr = 0x2404,
            ExternalInterruptPinSelectLow_Clr = 0x2408,
            ExternalInterruptPinSelectHigh_Clr = 0x240c,
            ExternalInterruptRisingEdgeTrigger_Clr = 0x2410,
            ExternalInterruptFallingEdgeTrigger_Clr = 0x2414,
            InterruptFlag_Clr = 0x2420,
            InterruptEnable_Clr = 0x2424,
            EM4WakeUpEnable_Clr = 0x242c,
            EM4WakeUpPolarity_Clr = 0x2430,
            DebugRoutePinEn_Clr = 0x2440,
            TraceRoutePinEn_Clr = 0x2444,
            GPIO_ACMP0_ROUTEEN_Clr = 0x2450,
            GPIO_ACMP0_ACMPOUTROUTE_Clr = 0x2454,
            GPIO_ACMP1_ROUTEEN_Clr = 0x245c,
            GPIO_ACMP1_ACMPOUTROUTE_Clr = 0x2460,
            GPIO_CMU_ROUTEEN_Clr = 0x2468,
            GPIO_CMU_CLKIN0ROUTE_Clr = 0x246c,
            GPIO_CMU_CLKOUT0ROUTE_Clr = 0x2470,
            GPIO_CMU_CLKOUT1ROUTE_Clr = 0x2474,
            GPIO_CMU_CLKOUT2ROUTE_Clr = 0x2478,
            GPIO_EUSART0_ROUTEEN_Clr = 0x2494,
            GPIO_EUSART0_CSROUTE_Clr = 0x2498,
            GPIO_EUSART0_CTSROUTE_Clr = 0x249c,
            GPIO_EUSART0_RTSROUTE_Clr = 0x24a0,
            GPIO_EUSART0_RXROUTE_Clr = 0x24a4,
            GPIO_EUSART0_SCLKROUTE_Clr = 0x24a8,
            GPIO_EUSART0_TXROUTE_Clr = 0x24ac,
            GPIO_EUSART1_ROUTEEN_Clr = 0x24b4,
            GPIO_EUSART1_CSROUTE_Clr = 0x24b8,
            GPIO_EUSART1_CTSROUTE_Clr = 0x24bc,
            GPIO_EUSART1_RTSROUTE_Clr = 0x24c0,
            GPIO_EUSART1_RXROUTE_Clr = 0x24c4,
            GPIO_EUSART1_SCLKROUTE_Clr = 0x24c8,
            GPIO_EUSART1_TXROUTE_Clr = 0x24cc,
            GPIO_FRC_ROUTEEN_Clr = 0x24d4,
            GPIO_FRC_DCLKROUTE_Clr = 0x24d8,
            GPIO_FRC_DFRAMEROUTE_Clr = 0x24dc,
            GPIO_FRC_DOUTROUTE_Clr = 0x24e0,
            GPIO_I2C0_ROUTEEN_Clr = 0x24e8,
            GPIO_I2C0_SCLROUTE_Clr = 0x24ec,
            GPIO_I2C0_SDAROUTE_Clr = 0x24f0,
            GPIO_I2C1_ROUTEEN_Clr = 0x24f8,
            GPIO_I2C1_SCLROUTE_Clr = 0x24fc,
            GPIO_I2C1_SDAROUTE_Clr = 0x2500,
            GPIO_KEYSCAN_ROUTEEN_Clr = 0x2508,
            GPIO_KEYSCAN_COLOUT0ROUTE_Clr = 0x250c,
            GPIO_KEYSCAN_COLOUT1ROUTE_Clr = 0x2510,
            GPIO_KEYSCAN_COLOUT2ROUTE_Clr = 0x2514,
            GPIO_KEYSCAN_COLOUT3ROUTE_Clr = 0x2518,
            GPIO_KEYSCAN_COLOUT4ROUTE_Clr = 0x251c,
            GPIO_KEYSCAN_COLOUT5ROUTE_Clr = 0x2520,
            GPIO_KEYSCAN_COLOUT6ROUTE_Clr = 0x2524,
            GPIO_KEYSCAN_COLOUT7ROUTE_Clr = 0x2528,
            GPIO_KEYSCAN_ROWSENSE0ROUTE_Clr = 0x252c,
            GPIO_KEYSCAN_ROWSENSE1ROUTE_Clr = 0x2530,
            GPIO_KEYSCAN_ROWSENSE2ROUTE_Clr = 0x2534,
            GPIO_KEYSCAN_ROWSENSE3ROUTE_Clr = 0x2538,
            GPIO_KEYSCAN_ROWSENSE4ROUTE_Clr = 0x253c,
            GPIO_KEYSCAN_ROWSENSE5ROUTE_Clr = 0x2540,
            GPIO_LETIMER_ROUTEEN_Clr = 0x2548,
            GPIO_LETIMER_OUT0ROUTE_Clr = 0x254c,
            GPIO_LETIMER_OUT1ROUTE_Clr = 0x2550,
            GPIO_MODEM_ROUTEEN_Clr = 0x2558,
            GPIO_MODEM_ANT0ROUTE_Clr = 0x255c,
            GPIO_MODEM_ANT1ROUTE_Clr = 0x2560,
            GPIO_MODEM_ANTROLLOVERROUTE_Clr = 0x2564,
            GPIO_MODEM_ANTRR0ROUTE_Clr = 0x2568,
            GPIO_MODEM_ANTRR1ROUTE_Clr = 0x256c,
            GPIO_MODEM_ANTRR2ROUTE_Clr = 0x2570,
            GPIO_MODEM_ANTRR3ROUTE_Clr = 0x2574,
            GPIO_MODEM_ANTRR4ROUTE_Clr = 0x2578,
            GPIO_MODEM_ANTRR5ROUTE_Clr = 0x257c,
            GPIO_MODEM_ANTSWENROUTE_Clr = 0x2580,
            GPIO_MODEM_ANTSWUSROUTE_Clr = 0x2584,
            GPIO_MODEM_ANTTRIGROUTE_Clr = 0x2588,
            GPIO_MODEM_ANTTRIGSTOPROUTE_Clr = 0x258c,
            GPIO_MODEM_DCLKROUTE_Clr = 0x2590,
            GPIO_MODEM_DINROUTE_Clr = 0x2594,
            GPIO_MODEM_DOUTROUTE_Clr = 0x2598,
            GPIO_PCNT0_S0INROUTE_Clr = 0x25a4,
            GPIO_PCNT0_S1INROUTE_Clr = 0x25a8,
            GPIO_PRS0_ROUTEEN_Clr = 0x25b0,
            GPIO_PRS0_ASYNCH0ROUTE_Clr = 0x25b4,
            GPIO_PRS0_ASYNCH1ROUTE_Clr = 0x25b8,
            GPIO_PRS0_ASYNCH2ROUTE_Clr = 0x25bc,
            GPIO_PRS0_ASYNCH3ROUTE_Clr = 0x25c0,
            GPIO_PRS0_ASYNCH4ROUTE_Clr = 0x25c4,
            GPIO_PRS0_ASYNCH5ROUTE_Clr = 0x25c8,
            GPIO_PRS0_ASYNCH6ROUTE_Clr = 0x25cc,
            GPIO_PRS0_ASYNCH7ROUTE_Clr = 0x25d0,
            GPIO_PRS0_ASYNCH8ROUTE_Clr = 0x25d4,
            GPIO_PRS0_ASYNCH9ROUTE_Clr = 0x25d8,
            GPIO_PRS0_ASYNCH10ROUT_Clr = 0x25dc,
            GPIO_PRS0_ASYNCH11ROUT_Clr = 0x25e0,
            GPIO_PRS0_ASYNCH12ROUTE_Clr = 0x25e4,
            GPIO_PRS0_ASYNCH13ROUT_Clr = 0x25e8,
            GPIO_PRS0_ASYNCH14ROUTE_Clr = 0x25ec,
            GPIO_PRS0_ASYNCH15ROUTE_Clr = 0x25f0,
            GPIO_PRS0_SYNCH0ROUTE_Clr = 0x25f4,
            GPIO_PRS0_SYNCH1ROUTE_Clr = 0x25f8,
            GPIO_PRS0_SYNCH2ROUTE_Clr = 0x25fc,
            GPIO_PRS0_SYNCH3ROUTE_Clr = 0x2600,
            GPIO_SYXO0_BUFOUTREQINASYNCROUTE_Clr = 0x2678,
            GPIO_TIMER0_ROUTEEN_Clr = 0x2680,
            GPIO_TIMER0_CC0ROUTE_Clr = 0x2684,
            GPIO_TIMER0_CC1ROUTE_Clr = 0x2688,
            GPIO_TIMER0_CC2ROUTE_Clr = 0x268c,
            GPIO_TIMER0_CDTI0ROUTE_Clr = 0x2690,
            GPIO_TIMER0_CDTI1ROUTE_Clr = 0x2694,
            GPIO_TIMER0_CDTI2ROUTE_Clr = 0x2698,
            GPIO_TIMER1_ROUTEEN_Clr = 0x26a0,
            GPIO_TIMER1_CC0ROUTE_Clr = 0x26a4,
            GPIO_TIMER1_CC1ROUTE_Clr = 0x26a8,
            GPIO_TIMER1_CC2ROUTE_Clr = 0x26ac,
            GPIO_TIMER1_CDTI0ROUTE_Clr = 0x26b0,
            GPIO_TIMER1_CDTI1ROUTE_Clr = 0x26b4,
            GPIO_TIMER1_CDTI2ROUTE_Clr = 0x26b8,
            GPIO_TIMER2_ROUTEEN_Clr = 0x26c0,
            GPIO_TIMER2_CC0ROUTE_Clr = 0x26c4,
            GPIO_TIMER2_CC1ROUTE_Clr = 0x26c8,
            GPIO_TIMER2_CC2ROUTE_Clr = 0x26cc,
            GPIO_TIMER2_CDTI0ROUTE_Clr = 0x26d0,
            GPIO_TIMER2_CDTI1ROUTE_Clr = 0x26d4,
            GPIO_TIMER2_CDTI2ROUTE_Clr = 0x26d8,
            GPIO_TIMER3_ROUTEEN_Clr = 0x26e0,
            GPIO_TIMER3_CC0ROUTE_Clr = 0x26e4,
            GPIO_TIMER3_CC1ROUTE_Clr = 0x26e8,
            GPIO_TIMER3_CC2ROUTE_Clr = 0x26ec,
            GPIO_TIMER3_CDTI0ROUTE_Clr = 0x26f0,
            GPIO_TIMER3_CDTI1ROUTE_Clr = 0x26f4,
            GPIO_TIMER3_CDTI2ROUTE_Clr = 0x26f8,
            GPIO_TIMER4_ROUTEEN_Clr = 0x2700,
            GPIO_TIMER4_CC0ROUTE_Clr = 0x2704,
            GPIO_TIMER4_CC1ROUTE_Clr = 0x2708,
            GPIO_TIMER4_CC2ROUTE_Clr = 0x270c,
            GPIO_TIMER4_CDTI0ROUTE_Clr = 0x2710,
            GPIO_TIMER4_CDTI1ROUTE_Clr = 0x2714,
            GPIO_TIMER4_CDTI2ROUTE_Clr = 0x2718,
            GPIO_USART0_ROUTEEN_Clr = 0x2720,
            GPIO_USART0_CSROUTE_Clr = 0x2724,
            GPIO_USART0_CTSROUTE_Clr = 0x2728,
            GPIO_USART0_RTSROUTE_Clr = 0x272c,
            GPIO_USART0_RXROUTE_Clr = 0x2730,
            GPIO_USART0_CLKROUTE_Clr = 0x2734,
            GPIO_USART0_TXROUTE_Clr = 0x2738,
            

            IpVersion_Tgl = 0x3000,
            PortAControl_Tgl = 0x3030,
            PortAModeLow_Tgl = 0x3034,
            PortAModeHigh_Tgl = 0x303c,
            PortADataOut_Tgl = 0x3040,
            PortADataIn_Tgl = 0x3044,
            PortBControl_Tgl = 0x3060,
            PortBModeLow_Tgl = 0x3064,
            PortBModeHigh_Tgl = 0x306c,
            PortBDataOut_Tgl = 0x3070,
            PortBDataIn_Tgl = 0x3074,
            PortCControl_Tgl = 0x3090,
            PortCModeLow_Tgl = 0x3094,
            PortCModeHigh_Tgl = 0x309c,
            PortCDataOut_Tgl = 0x30a0,
            PortCDataIn_Tgl = 0x30a4,
            PortDControl_Tgl = 0x30c0,
            PortDModeLow_Tgl = 0x30c4,
            PortDModeHigh_Tgl = 0x30cc,
            PortDDataOut_Tgl = 0x30d0,
            PortDDataIn_Tgl = 0x30d4,
            ConfigurationLock_Tgl = 0x3300,
            ConfigurationLockStatus_Tgl = 0x3310,
            ABusAllocation_Tgl = 0x3320,
            BBusAllocation_Tgl = 0x3324,
            CBusAllocation_Tgl = 0x3328,
            ExternalInterruptPortSelectLow_Tgl = 0x3400,
            ExternalInterruptPortSelectHigh_Tgl = 0x3404,
            ExternalInterruptPinSelectLow_Tgl = 0x3408,
            ExternalInterruptPinSelectHigh_Tgl = 0x340c,
            ExternalInterruptRisingEdgeTrigger_Tgl = 0x3410,
            ExternalInterruptFallingEdgeTrigger_Tgl = 0x3414,
            InterruptFlag_Tgl = 0x3420,
            InterruptEnable_Tgl = 0x3424,
            EM4WakeUpEnable_Tgl = 0x342c,
            EM4WakeUpPolarity_Tgl = 0x3430,
            DebugRoutePinEn_Tgl = 0x3440,
            TraceRoutePinEn_Tgl = 0x3444,
            GPIO_ACMP0_ROUTEEN_Tgl = 0x3450,
            GPIO_ACMP0_ACMPOUTROUTE_Tgl = 0x3454,
            GPIO_ACMP1_ROUTEEN_Tgl = 0x345c,
            GPIO_ACMP1_ACMPOUTROUTE_Tgl = 0x3460,
            GPIO_CMU_ROUTEEN_Tgl = 0x3468,
            GPIO_CMU_CLKIN0ROUTE_Tgl = 0x346c,
            GPIO_CMU_CLKOUT0ROUTE_Tgl = 0x3470,
            GPIO_CMU_CLKOUT1ROUTE_Tgl = 0x3474,
            GPIO_CMU_CLKOUT2ROUTE_Tgl = 0x3478,
            GPIO_EUSART0_ROUTEEN_Tgl = 0x3494,
            GPIO_EUSART0_CSROUTE_Tgl = 0x3498,
            GPIO_EUSART0_CTSROUTE_Tgl = 0x349c,
            GPIO_EUSART0_RTSROUTE_Tgl = 0x34a0,
            GPIO_EUSART0_RXROUTE_Tgl = 0x34a4,
            GPIO_EUSART0_SCLKROUTE_Tgl = 0x34a8,
            GPIO_EUSART0_TXROUTE_Tgl = 0x34ac,
            GPIO_EUSART1_ROUTEEN_Tgl = 0x34b4,
            GPIO_EUSART1_CSROUTE_Tgl = 0x34b8,
            GPIO_EUSART1_CTSROUTE_Tgl = 0x34bc,
            GPIO_EUSART1_RTSROUTE_Tgl = 0x34c0,
            GPIO_EUSART1_RXROUTE_Tgl = 0x34c4,
            GPIO_EUSART1_SCLKROUTE_Tgl = 0x34c8,
            GPIO_EUSART1_TXROUTE_Tgl = 0x34cc,
            GPIO_FRC_ROUTEEN_Tgl = 0x34d4,
            GPIO_FRC_DCLKROUTE_Tgl = 0x34d8,
            GPIO_FRC_DFRAMEROUTE_Tgl = 0x34dc,
            GPIO_FRC_DOUTROUTE_Tgl = 0x34e0,
            GPIO_I2C0_ROUTEEN_Tgl = 0x34e8,
            GPIO_I2C0_SCLROUTE_Tgl = 0x34ec,
            GPIO_I2C0_SDAROUTE_Tgl = 0x34f0,
            GPIO_I2C1_ROUTEEN_Tgl = 0x34f8,
            GPIO_I2C1_SCLROUTE_Tgl = 0x34fc,
            GPIO_I2C1_SDAROUTE_Tgl = 0x3500,
            GPIO_KEYSCAN_ROUTEEN_Tgl = 0x3508,
            GPIO_KEYSCAN_COLOUT0ROUTE_Tgl = 0x350c,
            GPIO_KEYSCAN_COLOUT1ROUTE_Tgl = 0x3510,
            GPIO_KEYSCAN_COLOUT2ROUTE_Tgl = 0x3514,
            GPIO_KEYSCAN_COLOUT3ROUTE_Tgl = 0x3518,
            GPIO_KEYSCAN_COLOUT4ROUTE_Tgl = 0x351c,
            GPIO_KEYSCAN_COLOUT5ROUTE_Tgl = 0x3520,
            GPIO_KEYSCAN_COLOUT6ROUTE_Tgl = 0x3524,
            GPIO_KEYSCAN_COLOUT7ROUTE_Tgl = 0x3528,
            GPIO_KEYSCAN_ROWSENSE0ROUTE_Tgl = 0x352c,
            GPIO_KEYSCAN_ROWSENSE1ROUTE_Tgl = 0x3530,
            GPIO_KEYSCAN_ROWSENSE2ROUTE_Tgl = 0x3534,
            GPIO_KEYSCAN_ROWSENSE3ROUTE_Tgl = 0x3538,
            GPIO_KEYSCAN_ROWSENSE4ROUTE_Tgl = 0x353c,
            GPIO_KEYSCAN_ROWSENSE5ROUTE_Tgl = 0x3540,
            GPIO_LETIMER_ROUTEEN_Tgl = 0x3548,
            GPIO_LETIMER_OUT0ROUTE_Tgl = 0x354c,
            GPIO_LETIMER_OUT1ROUTE_Tgl = 0x3550,
            GPIO_MODEM_ROUTEEN_Tgl = 0x3558,
            GPIO_MODEM_ANT0ROUTE_Tgl = 0x355c,
            GPIO_MODEM_ANT1ROUTE_Tgl = 0x3560,
            GPIO_MODEM_ANTROLLOVERROUTE_Tgl = 0x3564,
            GPIO_MODEM_ANTRR0ROUTE_Tgl = 0x3568,
            GPIO_MODEM_ANTRR1ROUTE_Tgl = 0x356c,
            GPIO_MODEM_ANTRR2ROUTE_Tgl = 0x3570,
            GPIO_MODEM_ANTRR3ROUTE_Tgl = 0x3574,
            GPIO_MODEM_ANTRR4ROUTE_Tgl = 0x3578,
            GPIO_MODEM_ANTRR5ROUTE_Tgl = 0x357c,
            GPIO_MODEM_ANTSWENROUTE_Tgl = 0x3580,
            GPIO_MODEM_ANTSWUSROUTE_Tgl = 0x3584,
            GPIO_MODEM_ANTTRIGROUTE_Tgl = 0x3588,
            GPIO_MODEM_ANTTRIGSTOPROUTE_Tgl = 0x358c,
            GPIO_MODEM_DCLKROUTE_Tgl = 0x3590,
            GPIO_MODEM_DINROUTE_Tgl = 0x3594,
            GPIO_MODEM_DOUTROUTE_Tgl = 0x3598,
            GPIO_PCNT0_S0INROUTE_Tgl = 0x35a4,
            GPIO_PCNT0_S1INROUTE_Tgl = 0x35a8,
            GPIO_PRS0_ROUTEEN_Tgl = 0x35b0,
            GPIO_PRS0_ASYNCH0ROUTE_Tgl = 0x35b4,
            GPIO_PRS0_ASYNCH1ROUTE_Tgl = 0x35b8,
            GPIO_PRS0_ASYNCH2ROUTE_Tgl = 0x35bc,
            GPIO_PRS0_ASYNCH3ROUTE_Tgl = 0x35c0,
            GPIO_PRS0_ASYNCH4ROUTE_Tgl = 0x35c4,
            GPIO_PRS0_ASYNCH5ROUTE_Tgl = 0x35c8,
            GPIO_PRS0_ASYNCH6ROUTE_Tgl = 0x35cc,
            GPIO_PRS0_ASYNCH7ROUTE_Tgl = 0x35d0,
            GPIO_PRS0_ASYNCH8ROUTE_Tgl = 0x35d4,
            GPIO_PRS0_ASYNCH9ROUTE_Tgl = 0x35d8,
            GPIO_PRS0_ASYNCH10ROUT_Tgl = 0x35dc,
            GPIO_PRS0_ASYNCH11ROUT_Tgl = 0x35e0,
            GPIO_PRS0_ASYNCH12ROUTE_Tgl = 0x35e4,
            GPIO_PRS0_ASYNCH13ROUT_Tgl = 0x35e8,
            GPIO_PRS0_ASYNCH14ROUTE_Tgl = 0x35ec,
            GPIO_PRS0_ASYNCH15ROUTE_Tgl = 0x35f0,
            GPIO_PRS0_SYNCH0ROUTE_Tgl = 0x35f4,
            GPIO_PRS0_SYNCH1ROUTE_Tgl = 0x35f8,
            GPIO_PRS0_SYNCH2ROUTE_Tgl = 0x35fc,
            GPIO_PRS0_SYNCH3ROUTE_Tgl = 0x3600,
            GPIO_SYXO0_BUFOUTREQINASYNCROUTE_Tgl = 0x3678,
            GPIO_TIMER0_ROUTEEN_Tgl = 0x3680,
            GPIO_TIMER0_CC0ROUTE_Tgl = 0x3684,
            GPIO_TIMER0_CC1ROUTE_Tgl = 0x3688,
            GPIO_TIMER0_CC2ROUTE_Tgl = 0x368c,
            GPIO_TIMER0_CDTI0ROUTE_Tgl = 0x3690,
            GPIO_TIMER0_CDTI1ROUTE_Tgl = 0x3694,
            GPIO_TIMER0_CDTI2ROUTE_Tgl = 0x3698,
            GPIO_TIMER1_ROUTEEN_Tgl = 0x36a0,
            GPIO_TIMER1_CC0ROUTE_Tgl = 0x36a4,
            GPIO_TIMER1_CC1ROUTE_Tgl = 0x36a8,
            GPIO_TIMER1_CC2ROUTE_Tgl = 0x36ac,
            GPIO_TIMER1_CDTI0ROUTE_Tgl = 0x36b0,
            GPIO_TIMER1_CDTI1ROUTE_Tgl = 0x36b4,
            GPIO_TIMER1_CDTI2ROUTE_Tgl = 0x36b8,
            GPIO_TIMER2_ROUTEEN_Tgl = 0x36c0,
            GPIO_TIMER2_CC0ROUTE_Tgl = 0x36c4,
            GPIO_TIMER2_CC1ROUTE_Tgl = 0x36c8,
            GPIO_TIMER2_CC2ROUTE_Tgl = 0x36cc,
            GPIO_TIMER2_CDTI0ROUTE_Tgl = 0x36d0,
            GPIO_TIMER2_CDTI1ROUTE_Tgl = 0x36d4,
            GPIO_TIMER2_CDTI2ROUTE_Tgl = 0x36d8,
            GPIO_TIMER3_ROUTEEN_Tgl = 0x36e0,
            GPIO_TIMER3_CC0ROUTE_Tgl = 0x36e4,
            GPIO_TIMER3_CC1ROUTE_Tgl = 0x36e8,
            GPIO_TIMER3_CC2ROUTE_Tgl = 0x36ec,
            GPIO_TIMER3_CDTI0ROUTE_Tgl = 0x36f0,
            GPIO_TIMER3_CDTI1ROUTE_Tgl = 0x36f4,
            GPIO_TIMER3_CDTI2ROUTE_Tgl = 0x36f8,
            GPIO_TIMER4_ROUTEEN_Tgl = 0x3700,
            GPIO_TIMER4_CC0ROUTE_Tgl = 0x3704,
            GPIO_TIMER4_CC1ROUTE_Tgl = 0x3708,
            GPIO_TIMER4_CC2ROUTE_Tgl = 0x370c,
            GPIO_TIMER4_CDTI0ROUTE_Tgl = 0x3710,
            GPIO_TIMER4_CDTI1ROUTE_Tgl = 0x3714,
            GPIO_TIMER4_CDTI2ROUTE_Tgl = 0x3718,
            GPIO_USART0_ROUTEEN_Tgl = 0x3720,
            GPIO_USART0_CSROUTE_Tgl = 0x3724,
            GPIO_USART0_CTSROUTE_Tgl = 0x3728,
            GPIO_USART0_RTSROUTE_Tgl = 0x372c,
            GPIO_USART0_RXROUTE_Tgl = 0x3730,
            GPIO_USART0_CLKROUTE_Tgl = 0x3734,
            GPIO_USART0_TXROUTE_Tgl = 0x3738,
        }
    }
}
