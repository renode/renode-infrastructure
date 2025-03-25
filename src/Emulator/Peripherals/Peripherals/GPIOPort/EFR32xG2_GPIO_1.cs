//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.IO;
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.GPIOPort;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class EFR32xG2_GPIO_1 : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_GPIO_1(Machine machine) : base(machine, NumberOfPins * NumberOfPorts)
        {
            OddIRQ = new GPIO();
            EvenIRQ = new GPIO();
            
            registersCollection = BuildRegistersCollection();
            InnerReset();
        }

        public override void Reset()
        {
            lock(internalLock)
            {
                base.Reset();
                InnerReset();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadRegister(offset);
        }

        public byte ReadByte(long offset)
        {
            int byteOffset = (int)(offset & 0x3);
            uint registerValue = ReadRegister(offset, true);
            byte result = (byte)((registerValue >> byteOffset*8) & 0xFF);
            return result;
        }

        private uint ReadRegister(long offset, bool internal_read = false)
        {
            var result = 0U;
            long internal_offset = offset;

            lock(internalLock)
            {
                // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
                if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    if(!internal_read)
                    {  
                        this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                    }
                } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    if(!internal_read)
                    {
                        this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                    }
                } else if (offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    if(!internal_read)
                    {
                        this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                    }
                }

                if(!registersCollection.TryRead(internal_offset, out result))
                {
                    if(!internal_read)
                    {
                        this.Log(LogLevel.Noisy, "Unhandled read at offset 0x{0:X} ({1}).", internal_offset, (Registers)internal_offset);
                    }
                }
                else
                {
                    if(!internal_read)
                    {
                        this.Log(LogLevel.Noisy, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", internal_offset, (Registers)internal_offset, result);
                    }
                }

                return result;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            // TODO: A subset of registers is lockable: if the lock is on (see LockStatus register), these registers should not be accessible.
            WriteRegister(offset, value);
        }

        private void WriteRegister(long offset, uint value, bool internal_write = false)
        {
            lock(internalLock) 
            {
                long internal_offset = offset;
                uint internal_value = value;

                if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value | value;
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value & ~value;
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                } else if (offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value ^ value;
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                }

                this.Log(LogLevel.Noisy, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);

                if(!registersCollection.TryWrite(internal_offset, internal_value))
                {
                    this.Log(LogLevel.Noisy, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);
                    return;
                }
            }
        }

        private DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ExternalInterruptPortSelectLow, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out externalInterruptPortSelect[0], name: "EXTIPSEL0")
                    .WithReservedBits(2, 2)
                    .WithEnumField<DoubleWordRegister, Port>(4, 2, out externalInterruptPortSelect[1], name: "EXTIPSEL1")
                    .WithReservedBits(6, 2)
                    .WithEnumField<DoubleWordRegister, Port>(8, 2, out externalInterruptPortSelect[2], name: "EXTIPSEL2")
                    .WithReservedBits(10, 2)
                    .WithEnumField<DoubleWordRegister, Port>(12, 2, out externalInterruptPortSelect[3], name: "EXTIPSEL3")
                    .WithReservedBits(14, 2)
                    .WithEnumField<DoubleWordRegister, Port>(16, 2, out externalInterruptPortSelect[4], name: "EXTIPSEL4")
                    .WithReservedBits(18, 2)
                    .WithEnumField<DoubleWordRegister, Port>(20, 2, out externalInterruptPortSelect[5], name: "EXTIPSEL5")
                    .WithReservedBits(22, 2)
                    .WithEnumField<DoubleWordRegister, Port>(24, 2, out externalInterruptPortSelect[6], name: "EXTIPSEL6")
                    .WithReservedBits(26, 2)
                    .WithEnumField<DoubleWordRegister, Port>(28, 2, out externalInterruptPortSelect[7], name: "EXTIPSEL7")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateRouting())
                },
                {(long)Registers.ExternalInterruptPortSelectHigh, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out externalInterruptPortSelect[8], name: "EXTIPSEL8")
                    .WithReservedBits(2, 2)
                    .WithEnumField<DoubleWordRegister, Port>(4, 2, out externalInterruptPortSelect[9], name: "EXTIPSEL9")
                    .WithReservedBits(6, 2)
                    .WithEnumField<DoubleWordRegister, Port>(8, 2, out externalInterruptPortSelect[10], name: "EXTIPSEL10")
                    .WithReservedBits(10, 2)
                    .WithEnumField<DoubleWordRegister, Port>(12, 2, out externalInterruptPortSelect[11], name: "EXTIPSEL11")
                    .WithReservedBits(14, 18)
                    .WithChangeCallback((_, __) => UpdateRouting())
                },
                {(long)Registers.ExternalInterruptPinSelectLow, new DoubleWordRegister(this)
                    .WithValueField(0, 2, out externalInterruptPinSelect[0], name: "EXTIPINSEL0")
                    .WithReservedBits(2, 2)
                    .WithValueField(4, 2, out externalInterruptPinSelect[1], name: "EXTIPINSEL1")
                    .WithReservedBits(6, 2)
                    .WithValueField(8, 2, out externalInterruptPinSelect[2], name: "EXTIPINSEL2")
                    .WithReservedBits(10, 2)
                    .WithValueField(12, 2, out externalInterruptPinSelect[3], name: "EXTIPINSEL3")
                    .WithReservedBits(14, 2)
                    .WithValueField(16, 2, out externalInterruptPinSelect[4], name: "EXTIPINSEL4")
                    .WithReservedBits(18, 2)
                    .WithValueField(20, 2, out externalInterruptPinSelect[5], name: "EXTIPINSEL5")
                    .WithReservedBits(22, 2)
                    .WithValueField(24, 2, out externalInterruptPinSelect[6], name: "EXTIPINSEL6")
                    .WithReservedBits(26, 2)
                    .WithValueField(28, 2, out externalInterruptPinSelect[7], name: "EXTIPINSEL7")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateRouting())
                },
                {(long)Registers.ExternalInterruptPinSelectHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 2, out externalInterruptPinSelect[8], name: "EXTIPINSEL8")
                    .WithReservedBits(2, 2)
                    .WithValueField(4, 2, out externalInterruptPinSelect[9], name: "EXTIPINSEL9")
                    .WithReservedBits(6, 2)
                    .WithValueField(8, 2, out externalInterruptPinSelect[10], name: "EXTIPINSEL10")
                    .WithReservedBits(10, 2)
                    .WithValueField(12, 2, out externalInterruptPinSelect[11], name: "EXTIPINSEL11")
                    .WithReservedBits(14, 18)
                    .WithChangeCallback((_, __) => UpdateRouting())
                },
                {(long)Registers.ExternalInterruptRisingEdgeTrigger, new DoubleWordRegister(this)
                    .WithFlags(0, 12, 
                               writeCallback: (i, _, value) => 
                               {
                                   if (value)
                                   {
                                       interruptTrigger[i] |= (uint)InterruptTrigger.RisingEdge;
                                   }
                                   else
                                   {
                                       interruptTrigger[i] ^= (uint)InterruptTrigger.RisingEdge;
                                   }
                               }, 
                               valueProviderCallback: (i, _) => ((interruptTrigger[i] & (uint)InterruptTrigger.RisingEdge) > 0), 
                               name: "EXTIRISE")
                    .WithReservedBits(12, 20)
                },
                {(long)Registers.ExternalInterruptFallingEdgeTrigger, new DoubleWordRegister(this)
                    .WithFlags(0, 12, 
                               writeCallback: (i, _, value) => 
                               {
                                   if (value)
                                   {
                                       interruptTrigger[i] |= (uint)InterruptTrigger.FallingEdge;
                                   }
                                   else
                                   {
                                       interruptTrigger[i] ^= (uint)InterruptTrigger.FallingEdge;
                                   }
                               }, 
                               valueProviderCallback: (i, _) => ((interruptTrigger[i] & (uint)InterruptTrigger.FallingEdge) > 0), 
                               name: "EXTIFALL")
                    .WithReservedBits(12, 20)
                },
                {(long)Registers.InterruptFlag, new DoubleWordRegister(this)
                    .WithFlag(0, out externalInterrupt[0], name: "EXTIF0")
                    .WithFlag(1, out externalInterrupt[1], name: "EXTIF1")
                    .WithFlag(2, out externalInterrupt[2], name: "EXTIF2")
                    .WithFlag(3, out externalInterrupt[3], name: "EXTIF3")
                    .WithFlag(4, out externalInterrupt[4], name: "EXTIF4")
                    .WithFlag(5, out externalInterrupt[5], name: "EXTIF5")
                    .WithFlag(6, out externalInterrupt[6], name: "EXTIF6")
                    .WithFlag(7, out externalInterrupt[7], name: "EXTIF7")
                    .WithFlag(8, out externalInterrupt[8], name: "EXTIF8")
                    .WithFlag(9, out externalInterrupt[9], name: "EXTIF9")
                    .WithFlag(10, out externalInterrupt[10], name: "EXTIF10")
                    .WithFlag(11, out externalInterrupt[11], name: "EXTIF11")
                    .WithReservedBits(12, 4)
                    .WithTaggedFlag("EM4WUIF0", 16)
                    .WithTaggedFlag("EM4WUIF1", 17)
                    .WithTaggedFlag("EM4WUIF2", 18)
                    .WithTaggedFlag("EM4WUIF3", 19)
                    .WithTaggedFlag("EM4WUIF4", 20)
                    .WithTaggedFlag("EM4WUIF5", 21)
                    .WithTaggedFlag("EM4WUIF6", 22)
                    .WithTaggedFlag("EM4WUIF7", 23)
                    .WithTaggedFlag("EM4WUIF8", 24)
                    .WithTaggedFlag("EM4WUIF9", 25)
                    .WithTaggedFlag("EM4WUIF10", 26)
                    .WithTaggedFlag("EM4WUIF11", 27)
                    .WithReservedBits(28, 4)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out externalInterruptEnable[0], name: "EXTIEN0")
                    .WithFlag(1, out externalInterruptEnable[1], name: "EXTIEN1")
                    .WithFlag(2, out externalInterruptEnable[2], name: "EXTIEN2")
                    .WithFlag(3, out externalInterruptEnable[3], name: "EXTIEN3")
                    .WithFlag(4, out externalInterruptEnable[4], name: "EXTIEN4")
                    .WithFlag(5, out externalInterruptEnable[5], name: "EXTIEN5")
                    .WithFlag(6, out externalInterruptEnable[6], name: "EXTIEN6")
                    .WithFlag(7, out externalInterruptEnable[7], name: "EXTIEN7")
                    .WithFlag(8, out externalInterruptEnable[8], name: "EXTIEN8")
                    .WithFlag(9, out externalInterruptEnable[9], name: "EXTIEN9")
                    .WithFlag(10, out externalInterruptEnable[10], name: "EXTIEN10")
                    .WithFlag(11, out externalInterruptEnable[11], name: "EXTIEN11")
                    .WithReservedBits(12, 4)
                    .WithTaggedFlag("EM4WUIEN0", 16)
                    .WithTaggedFlag("EM4WUIEN1", 17)
                    .WithTaggedFlag("EM4WUIEN2", 18)
                    .WithTaggedFlag("EM4WUIEN3", 19)
                    .WithTaggedFlag("EM4WUIEN4", 20)
                    .WithTaggedFlag("EM4WUIEN5", 21)
                    .WithTaggedFlag("EM4WUIEN6", 22)
                    .WithTaggedFlag("EM4WUIEN7", 23)
                    .WithTaggedFlag("EM4WUIEN8", 24)
                    .WithTaggedFlag("EM4WUIEN9", 25)
                    .WithTaggedFlag("EM4WUIEN10", 26)
                    .WithTaggedFlag("EM4WUIEN11", 27)
                    .WithReservedBits(28, 4)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Lock, new DoubleWordRegister(this)
                    .WithValueField(0, 16, writeCallback: (_, value) => configurationLocked = (value != UnlockCode), name: "LOCKKEY")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.LockStatus, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => configurationLocked, name: "LOCK")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.USART0_RouteEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out USART0_RouteEnable_CsPin, name: "CSPEN")
                    .WithFlag(1, out USART0_RouteEnable_RtsPin, name: "RTSPEN")
                    .WithFlag(2, out USART0_RouteEnable_RxPin, name: "RXPEN")
                    .WithFlag(3, out USART0_RouteEnable_SclkPin, name: "SCLKPEN")
                    .WithFlag(4, out USART0_RouteEnable_TxPin, name: "TXPEN")
                },
                {(long)Registers.USART0_RX_Route, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out USART0_RxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out USART0_RxRoutePin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.USART0_TX_Route, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out USART0_TxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out USART0_TxRoutePin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.USART1_RouteEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out USART1_RouteEnable_CsPin, name: "CSPEN")
                    .WithFlag(1, out USART1_RouteEnable_RtsPin, name: "RTSPEN")
                    .WithFlag(2, out USART1_RouteEnable_RxPin, name: "RXPEN")
                    .WithFlag(3, out USART1_RouteEnable_SclkPin, name: "SCLKPEN")
                    .WithFlag(4, out USART1_RouteEnable_TxPin, name: "TXPEN")
                },
                {(long)Registers.USART1_RX_Route, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out USART1_RxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out USART1_RxRoutePin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.USART1_TX_Route, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out USART1_TxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out USART1_TxRoutePin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
            };

            for(var i = 0; i < NumberOfPorts; ++i)
            {
                BuildPortRegisters(registerDictionary, i);
            }

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private void BuildPortRegisters(Dictionary<long, DoubleWordRegister> regs, int portNumber)
        {
            var regOffset = PortOffset * portNumber;
            var pinOffset = portNumber * NumberOfPins;

            regs.Add((long)Registers.PortAControl + regOffset, new DoubleWordRegister(this)
                     .WithReservedBits(0, 4)
                     .WithTag("SLEWRATE", 4, 3)
                     .WithReservedBits(7, 5)
                     .WithTaggedFlag("DINDIS", 12)
                     .WithReservedBits(13, 7)
                     .WithTag("SLEWRATEALT", 20, 3)
                     .WithReservedBits(23, 5)
                     .WithTaggedFlag("DINDISALT", 28)
                     .WithReservedBits(29, 3)
                    );
            regs.Add((long)Registers.PortAModeLow + regOffset, new DoubleWordRegister(this)
                     .WithEnumField<DoubleWordRegister, PinMode>(0, 4, out pinMode[pinOffset], name: "MODE0")
                     .WithEnumField<DoubleWordRegister, PinMode>(4, 4, out pinMode[pinOffset + 1], name: "MODE1")
                     .WithEnumField<DoubleWordRegister, PinMode>(8, 4, out pinMode[pinOffset + 2], name: "MODE2")
                     .WithEnumField<DoubleWordRegister, PinMode>(12, 4, out pinMode[pinOffset + 3], name: "MODE3")
                     .WithEnumField<DoubleWordRegister, PinMode>(16, 4, out pinMode[pinOffset + 4], name: "MODE4")
                     .WithEnumField<DoubleWordRegister, PinMode>(20, 4, out pinMode[pinOffset + 5], name: "MODE5")
                     .WithEnumField<DoubleWordRegister, PinMode>(24, 4, out pinMode[pinOffset + 6], name: "MODE6")
                     .WithEnumField<DoubleWordRegister, PinMode>(28, 4, out pinMode[pinOffset + 7], name: "MODE7")
                    );
            regs.Add((long)Registers.PortAModeHigh + regOffset, new DoubleWordRegister(this)
                     .WithEnumField<DoubleWordRegister, PinMode>(0, 4, out pinMode[pinOffset + 8], name: "MODE8")
                     .WithEnumField<DoubleWordRegister, PinMode>(4, 4, out pinMode[pinOffset + 9], name: "MODE9")
                     .WithEnumField<DoubleWordRegister, PinMode>(8, 4, out pinMode[pinOffset + 10], name: "MODE10")
                     .WithEnumField<DoubleWordRegister, PinMode>(12, 4, out pinMode[pinOffset + 11], name: "MODE11")
                     .WithEnumField<DoubleWordRegister, PinMode>(16, 4, out pinMode[pinOffset + 12], name: "MODE12")
                     .WithEnumField<DoubleWordRegister, PinMode>(20, 4, out pinMode[pinOffset + 13], name: "MODE13")
                     .WithEnumField<DoubleWordRegister, PinMode>(24, 4, out pinMode[pinOffset + 14], name: "MODE14")
                     .WithEnumField<DoubleWordRegister, PinMode>(28, 4, out pinMode[pinOffset + 15], name: "MODE15")
                    );
            regs.Add((long)Registers.PortADataOut + regOffset, new DoubleWordRegister(this)
                     .WithFlags(0, 8, 
                                writeCallback: (i, _, value) => 
                                {
                                    var pin = pinOffset + i;
                                    if (IsOutput(pinMode[pin].Value))
                                    {
                                        Connections[pin].Set(value);
                                    }
                                },
                                valueProviderCallback: (i, _) => 
                                {
                                    var pin = pinOffset + i;
                                    return Connections[pin].IsSet;
                                },
                                name: "DOUT")
                     .WithReservedBits(8, 24)
                    );
            regs.Add((long)Registers.PortADataIn + regOffset, new DoubleWordRegister(this)
                     .WithFlags(0, 8, FieldMode.Read,
                                valueProviderCallback: (i, _) =>
                                {
                                    var pin = pinOffset + i;
                                    return State[pin];
                                },
                                name: "DIN")
                     .WithReservedBits(8, 24)
                    );
        }

        public long Size => 0x4000;
        public GPIO OddIRQ { get; }
        public GPIO EvenIRQ { get; }
        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly object internalLock = new object();
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private const int NumberOfPorts = 4;
        private const int NumberOfPins = 16;
        private const int NumberOfExternalInterrupts = 12;
        private const int UnlockCode = 0xA534;
        private const int PortOffset = 0x30;
#region register fields
        private readonly IEnumRegisterField<Port>[] externalInterruptPortSelect = new IEnumRegisterField<Port>[NumberOfExternalInterrupts];
        private readonly IValueRegisterField[] externalInterruptPinSelect = new IValueRegisterField[NumberOfExternalInterrupts];
        private readonly IEnumRegisterField<PinMode>[] pinMode = new IEnumRegisterField<PinMode>[NumberOfPins * NumberOfPorts];
        private readonly IFlagRegisterField[] externalInterrupt = new IFlagRegisterField[NumberOfExternalInterrupts];
        private readonly IFlagRegisterField[] externalInterruptEnable = new IFlagRegisterField[NumberOfExternalInterrupts];
        private readonly uint[] interruptTrigger = new uint[NumberOfExternalInterrupts];
        private readonly bool[] previousState = new bool[NumberOfExternalInterrupts];
        private readonly uint[] targetExternalPins = new uint[NumberOfPins * NumberOfPorts];
        private bool configurationLocked;
        // USART0
        private IFlagRegisterField USART0_RouteEnable_TxPin;
        private IFlagRegisterField USART0_RouteEnable_SclkPin;
        private IFlagRegisterField USART0_RouteEnable_RxPin;
        private IFlagRegisterField USART0_RouteEnable_RtsPin;
        private IFlagRegisterField USART0_RouteEnable_CsPin;
        private IEnumRegisterField<Port> USART0_RxRoutePort;
        private IValueRegisterField USART0_RxRoutePin;
        private IEnumRegisterField<Port> USART0_TxRoutePort;
        private IValueRegisterField USART0_TxRoutePin;
        // USART1
        private IFlagRegisterField USART1_RouteEnable_TxPin;
        private IFlagRegisterField USART1_RouteEnable_SclkPin;
        private IFlagRegisterField USART1_RouteEnable_RxPin;
        private IFlagRegisterField USART1_RouteEnable_RtsPin;
        private IFlagRegisterField USART1_RouteEnable_CsPin;
        private IEnumRegisterField<Port> USART1_RxRoutePort;
        private IValueRegisterField USART1_RxRoutePin;
        private IEnumRegisterField<Port> USART1_TxRoutePort;
        private IValueRegisterField USART1_TxRoutePin;
#endregion

#region methods
        public void InnerReset()
        {
            registersCollection.Reset();
            configurationLocked = false;
            EvenIRQ.Unset();
            OddIRQ.Unset();
            for(var i = 0; i < NumberOfExternalInterrupts; i++)
            {
                interruptTrigger[i] = (uint)InterruptTrigger.None;
                previousState[i] = false;
            }
            for(var i = 0; i < NumberOfPins * NumberOfPorts; i++)
            {
                targetExternalPins[i] = 0;
            }
        }
        
        public override void OnGPIO(int number, bool value)
        {
            bool internalSignal = ((number & 0x1000) > 0);

            // Override the GPIO number if this is an internal signal.
            if (internalSignal)
            {
                SignalSource signalSource = (SignalSource)(number & 0xFF);
                SignalType signalType = (SignalType)((number & 0xF00) >> 8);
                
                number = GetPinNumberFromInternalSignal(signalSource, signalType);

                if (number < 0)
                {
                    this.Log(LogLevel.Warning, "Pin number not found for internal signal (source={0} signal={1})", signalSource, signalType);
                    return;
                }
            }

            if(number < 0 || number >= State.Length)
            {
                this.Log(LogLevel.Error, string.Format("Gpio #{0} called, but only {1} lines are available", number, State.Length));
                return;
            }
            
            lock(internalLock)
            {
                if(IsOutput(pinMode[number].Value))
                {
                    this.Log(LogLevel.Warning, "Writing to an output GPIO pin #{0}", number);
                    return;
                }

                base.OnGPIO(number, value);
                UpdateInterrupts();
            }
        } 

        int GetPinNumberFromInternalSignal(SignalSource signalSource, SignalType signalType)
        {
            int pinNumber = -1;

            switch(signalSource)
            {
                case SignalSource.USART0:
                {
                    switch(signalType)
                    {
                        case SignalType.USART0_RX:
                        {
                            if (USART0_RouteEnable_RxPin.Value)
                            {
                                pinNumber = GetPinNumber(USART0_RxRoutePort.Value, (uint)USART0_RxRoutePin.Value);
                            }
                            break;
                        }
                        default:
                            this.Log(LogLevel.Error, string.Format("GPIO Signal type {0} for USART0 not supported", signalType));
                            return pinNumber;
                    }
                    break;
                }
                case SignalSource.USART1:
                {
                    switch(signalType)
                    {
                        case SignalType.USART1_RX:
                        {
                            if (USART1_RouteEnable_RxPin.Value)
                            {
                                pinNumber = GetPinNumber(USART1_RxRoutePort.Value, (uint)USART1_RxRoutePin.Value);
                            }
                            break;
                        }
                        default:
                            this.Log(LogLevel.Error, string.Format("GPIO Signal type {0} for USART0 not supported", signalType));
                            return pinNumber;
                    }
                    break;
                }
                default:
                    this.Log(LogLevel.Error, string.Format("GPIO Signal source {0} not supported", signalSource));
                    return pinNumber;
            }

            return pinNumber;
        }

        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                for(var i = 0; i < NumberOfPorts*NumberOfPins; ++i)
                {
                    var externalInterruptIndex = targetExternalPins[i];
                    
                    if (!externalInterruptEnable[externalInterruptIndex].Value)
                    {
                        continue;
                    }
                    
                    var isEdge = (State[i] != previousState[externalInterruptIndex]);
                    previousState[externalInterruptIndex] = State[i];
                    
                    if(isEdge
                       && ((State[i] && ((interruptTrigger[externalInterruptIndex] & (uint)InterruptTrigger.RisingEdge) > 0))
                           || (!State[i] && ((interruptTrigger[externalInterruptIndex] & (uint)InterruptTrigger.FallingEdge) > 0))))
                    {
                        externalInterrupt[externalInterruptIndex].Value = true;
                    }
                }

                // Set even and/or odd interrupt as needed
                var even = false;
                var odd = false;
                for(var i = 0; i < NumberOfExternalInterrupts; i += 2)
                {
                    even |= externalInterrupt[i].Value;
                }
                for(var i = 1; i < NumberOfExternalInterrupts; i += 2)
                {
                    odd |= externalInterrupt[i].Value;
                }
                OddIRQ.Set(odd);    
                EvenIRQ.Set(even);
            });
        }

        private void UpdateRouting()
        {
            for(uint i=0; i<NumberOfPins * NumberOfPorts; i++)
            {
                targetExternalPins[i] = 0;
            }

            for(uint i=0; i<NumberOfExternalInterrupts; i++)
            {
                Port port = externalInterruptPortSelect[i].Value; 
                uint pin = (uint)externalInterruptPinSelect[i].Value;

                uint pinGroup = i / 4;
                uint pinNumber = ((uint)port * NumberOfPins) + (pinGroup * 4) + pin;

                targetExternalPins[pinNumber] = i;
            }

            UpdateInterrupts();
        }

        private int GetPinNumber(Port port, uint pinSelect)
        {
            return (int)(((uint)port)*NumberOfPins + pinSelect);
        }

        private bool IsOutput(PinMode mode)
        {
            return mode >= PinMode.PushPull;
        }

        private bool TrySyncTime()
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
                return true;
            }
            return false;
        }

        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;
#endregion

#region enums
        private enum Port
        {
            PortA = 0,
            PortB = 1,
            PortC = 2,
            PortD = 3,
        }

        private enum PinMode
        {
            Disabled                  = 0,
            Input                     = 1,
            InputPull                 = 2,
            InputPullFilter           = 3,
            PushPull                  = 4,
            PushPullAlt               = 5,
            WiredOr                   = 6,
            WiredOrPullDown           = 7,
            WiredAnd                  = 8,
            WiredAndFilter            = 9,
            WiredAndPullUp            = 10,
            WiredAndPullUpFilter      = 11,
            WiredAndAlt               = 12,
            WiredAndAltFilter         = 13,
            WiredAndAltPullUp         = 14,
            WiredAndAltPullUpFilter   = 15,
        }

        private enum InterruptTrigger
        {
            None        = 0x0,
            FallingEdge = 0x1,
            RisingEdge  = 0x2,
        }

        private enum SignalSource
        {
            None     = 0,
            USART0   = 1,
            USART1   = 2,
        }

        private enum SignalType
        {
            // If SignalSource is USART0
            USART0_CTS              = 0,
            USART0_RTS              = 1,
            USART0_RX               = 2,
            USART0_SCLK             = 3,
            USART0_TX               = 4,
            // If SignalSource is USART1
            USART1_CTS              = 0,
            USART1_RTS              = 1,
            USART1_RX               = 2,
            USART1_SCLK             = 3,
            USART1_TX               = 4,
        }

        private enum Registers
        {
            PortAControl                            = 0x0000,
            PortAModeLow                            = 0x0004,
            PortAModeHigh                           = 0x000C,
            PortADataOut                            = 0x0010,
            PortADataIn                             = 0x0014,
            PortBControl                            = 0x0030,
            PortBModeLow                            = 0x0034,
            PortBModeHigh                           = 0x004C,
            PortBDataOut                            = 0x0040,
            PortBDataIn                             = 0x0044,
            PortCControl                            = 0x0060,
            PortCModeLow                            = 0x0064,
            PortCModeHigh                           = 0x006C,
            PortCDataOut                            = 0x0070,
            PortCDataIn                             = 0x0074,
            PortDControl                            = 0x0090,
            PortDModeLow                            = 0x0094,
            PortDModeHigh                           = 0x009C,
            PortDDataOut                            = 0x00A0,
            PortDDataIn                             = 0x00A4,
            Lock                                    = 0x0300,
            LockStatus                              = 0x0310,
            ABusAllocation                          = 0x0320,
            BBusAllocation                          = 0x0324,
            CDBusAllocation                         = 0x0328,
            ExternalInterruptPortSelectLow          = 0x0400,
            ExternalInterruptPortSelectHigh         = 0x0404,
            ExternalInterruptPinSelectLow           = 0x0408,
            ExternalInterruptPinSelectHigh          = 0x040C,
            ExternalInterruptRisingEdgeTrigger      = 0x0410,
            ExternalInterruptFallingEdgeTrigger     = 0x0414,
            InterruptFlag                           = 0x0420,
            InterruptEnable                         = 0x0424,
            EM4WakeUpEnable                         = 0x042C,
            EM4WakeUpPolarity                       = 0x0430,
            DebugRoutePinEn                         = 0x0440,
            TraceRoutePinEn                         = 0x0444,
            CMU_RouteEnable                         = 0x0450,
            CMU_CLKIN0_Route                        = 0x0454,
            CMU_CLKOUT0_Route                       = 0x0458,
            CMU_CLKOUT1_Route                       = 0x045C,
            CMU_CLKOUT2_Route                       = 0x0460,
            DCDC_RouteEnable                        = 0x046C,
            FRC_RouteEnable                         = 0x047C,
            FRC_DCLK_Route                          = 0x0480,
            FRC_DFRAME_Route                        = 0x0484,
            FRC_DOUT_Route                          = 0x0488,
            I2C0_RouteEnable                        = 0x0490,
            I2C0_SCL_Route                          = 0x0494,
            I2C0_SDA_Route                          = 0x0498,
            I2C1_RouteEnable                        = 0x04A0,
            I2C1_SCL_Route                          = 0x04A4,
            I2C1_SDA_Route                          = 0x04A8,
            LETIMER_RouteEnable                     = 0x04B0,
            LETIMER_OUT0_Route                      = 0x04B4,
            LETIMER_OUT1_Route                      = 0x04B8,
            ESART0_RouteEnable                      = 0x04C0,
            ESART0_CS_Route                         = 0x04C4,
            ESART0_CTS_Route                        = 0x04C8,
            ESART0_RTS_Route                        = 0x04CC,
            ESART0_RX_Route                         = 0x04D0,
            MODEM_RouteEnable                       = 0x04D8,
            MODEM_ANT0_Route                        = 0x04DC,
            MODEM_ANT1_Route                        = 0x04E0,
            MODEM_ANTROLLOVER_Route                 = 0x04E4,
            MODEM_ANTRR0_Route                      = 0x04E8,
            MODEM_ANTRR1_Route                      = 0x04EC,
            MODEM_ANTRR2_Route                      = 0x04F0,
            MODEM_ANTRR3_Route                      = 0x04F4,
            MODEM_ANTRR4_Route                      = 0x04F8,
            MODEM_ANTRR5_Route                      = 0x04FC,
            MODEM_ANTSWEN_Route                     = 0x0500,
            MODEM_ANTSWUS_Route                     = 0x0504,
            MODEM_ANTTRIG_Route                     = 0x0508,
            MODEM_ANTTRIGSTOP_Route                 = 0x050C,
            MODEM_DCLK_Route                        = 0x0510,
            MODEM_DIN_Route                         = 0x0514,
            MODEM_DOUT_Route                        = 0x0518,
            PDM_RouteEnable                         = 0x0520,
            PDM_CLK_Route                           = 0x0524,
            PDM_DAT0_Route                          = 0x0528,
            PDM_DAT1_Route                          = 0x052C,
            PRS0_RouteEnable                        = 0x0534,
            PRS0_ASYNCH0_Route                      = 0x0538,
            PRS0_ASYNCH1_Route                      = 0x053C,
            PRS0_ASYNCH2_Route                      = 0x0540,
            PRS0_ASYNCH3_Route                      = 0x0544,
            PRS0_ASYNCH4_Route                      = 0x0548,
            PRS0_ASYNCH5_Route                      = 0x054C,
            PRS0_ASYNCH6_Route                      = 0x0550,
            PRS0_ASYNCH7_Route                      = 0x0554,
            PRS0_ASYNCH8_Route                      = 0x0558,
            PRS0_ASYNCH9_Route                      = 0x055C,
            PRS0_ASYNCH10_Route                     = 0x0560,
            PRS0_ASYNCH11_Route                     = 0x0564,
            PRS0_SYNCH0_Route                       = 0x0568,
            PRS0_SYNCH1_Route                       = 0x056C,
            PRS0_SYNCH2_Route                       = 0x0570,
            PRS0_SYNCH3_Route                       = 0x0574,
            TIMER0_RouteEnable                      = 0x057C,
            TIMER0_CC0_Route                        = 0x0580,
            TIMER0_CC1_Route                        = 0x0584,
            TIMER0_CC2_Route                        = 0x0588,
            TIMER0_CDTI0_Route                      = 0x058C,
            TIMER0_CDTI1_Route                      = 0x0590,
            TIMER0_CDTI2_Route                      = 0x0594,
            TIMER1_RouteEnable                      = 0x059C,
            TIMER1_CC0_Route                        = 0x05A0,
            TIMER1_CC1_Route                        = 0x05A4,
            TIMER1_CC2_Route                        = 0x05A8,
            TIMER1_CDTI0_Route                      = 0x05AC,
            TIMER1_CDTI1_Route                      = 0x05B0,
            TIMER1_CDTI2_Route                      = 0x05B4,
            TIMER2_RouteEnable                      = 0x05BC,
            TIMER2_CC0_Route                        = 0x05C0,
            TIMER2_CC1_Route                        = 0x05C4,
            TIMER2_CC2_Route                        = 0x05C8,
            TIMER2_CDTI0_Route                      = 0x05CC,
            TIMER2_CDTI1_Route                      = 0x05D0,
            TIMER2_CDTI2_Route                      = 0x05D4,
            TIMER3_RouteEnable                      = 0x05DC,
            TIMER3_CC0_Route                        = 0x05E0,
            TIMER3_CC1_Route                        = 0x05E4,
            TIMER3_CC2_Route                        = 0x05E8,
            TIMER3_CDTI0_Route                      = 0x05EC,
            TIMER3_CDTI1_Route                      = 0x05F0,
            TIMER3_CDTI2_Route                      = 0x05F4,
            TIMER4_RouteEnable                      = 0x05FC,
            TIMER4_CC0_Route                        = 0x0600,
            TIMER4_CC1_Route                        = 0x0604,
            TIMER4_CC2_Route                        = 0x0608,
            TIMER4_CDTI0_Route                      = 0x060C,
            TIMER4_CDTI1_Route                      = 0x0610,
            TIMER4_CDTI2_Route                      = 0x0614,
            USART0_RouteEnable                      = 0x061C,
            USART0_CS_Route                         = 0x0620,
            USART0_CTS_Route                        = 0x0624,
            USART0_RTS_Route                        = 0x0628,
            USART0_RX_Route                         = 0x062C,
            USART0_CLK_Route                        = 0x0630,
            USART0_TX_Route                         = 0x0634,
            USART1_RouteEnable                      = 0x063C,
            USART1_CS_Route                         = 0x0640,
            USART1_CTS_Route                        = 0x0644,
            USART1_RTS_Route                        = 0x0648,
            USART1_RX_Route                         = 0x064C,
            USART1_CLK_Route                        = 0x0650,
            USART1_TX_Route                         = 0x0654,
            // Set registers
            PortAControl_Set                        = 0x1000,
            PortAModeLow_Set                        = 0x1004,
            PortAModeHigh_Set                       = 0x100C,
            PortADataOut_Set                        = 0x1010,
            PortADataIn_Set                         = 0x1014,
            PortBControl_Set                        = 0x1030,
            PortBModeLow_Set                        = 0x1034,
            PortBModeHigh_Set                       = 0x104C,
            PortBDataOut_Set                        = 0x1040,
            PortBDataIn_Set                         = 0x1044,
            PortCControl_Set                        = 0x1060,
            PortCModeLow_Set                        = 0x1064,
            PortCModeHigh_Set                       = 0x106C,
            PortCDataOut_Set                        = 0x1070,
            PortCDataIn_Set                         = 0x1074,
            PortDControl_Set                        = 0x1090,
            PortDModeLow_Set                        = 0x1094,
            PortDModeHigh_Set                       = 0x109C,
            PortDDataOut_Set                        = 0x10A0,
            PortDDataIn_Set                         = 0x10A4,
            Lock_Set                                = 0x1300,
            LockStatus_Set                          = 0x1310,
            ABusAllocation_Set                      = 0x1320,
            BBusAllocation_Set                      = 0x1324,
            CDBusAllocation_Set                     = 0x1328,
            ExternalInterruptPortSelectLow_Set      = 0x1400,
            ExternalInterruptPortSelectHigh_Set     = 0x1404,
            ExternalInterruptPinSelectLow_Set       = 0x1408,
            ExternalInterruptPinSelectHigh_Set      = 0x140C,
            ExternalInterruptRisingEdgeTrigger_Set  = 0x1410,
            ExternalInterruptFallingEdgeTrigger_Set = 0x1414,
            InterruptFlag_Set                       = 0x1420,
            InterruptEnable_Set                     = 0x1424,
            EM4WakeUpEnable_Set                     = 0x142C,
            EM4WakeUpPolarity_Set                   = 0x1430,
            DebugRoutePinEn_Set                     = 0x1440,
            TraceRoutePinEn_Set                     = 0x1444,
            CMU_RouteEnable_Set                     = 0x1450,
            CMU_CLKIN0_Route_Set                    = 0x1454,
            CMU_CLKOUT0_Route_Set                   = 0x1458,
            CMU_CLKOUT1_Route_Set                   = 0x145C,
            CMU_CLKOUT2_Route_Set                   = 0x1460,
            DCDC_RouteEnable_Set                    = 0x146C,
            FRC_RouteEnable_Set                     = 0x147C,
            FRC_DCLK_Route_Set                      = 0x1480,
            FRC_DFRAME_Route_Set                    = 0x1484,
            FRC_DOUT_Route_Set                      = 0x1488,
            I2C0_RouteEnable_Set                    = 0x1490,
            I2C0_SCL_Route_Set                      = 0x1494,
            I2C0_SDA_Route_Set                      = 0x1498,
            I2C1_RouteEnable_Set                    = 0x14A0,
            I2C1_SCL_Route_Set                      = 0x14A4,
            I2C1_SDA_Route_Set                      = 0x14A8,
            LETIMER_RouteEnable_Set                 = 0x14B0,
            LETIMER_OUT0_Route_Set                  = 0x14B4,
            LETIMER_OUT1_Route_Set                  = 0x14B8,
            ESART0_RouteEnable_Set                  = 0x14C0,
            ESART0_CS_Route_Set                     = 0x14C4,
            ESART0_CTS_Route_Set                    = 0x14C8,
            ESART0_RTS_Route_Set                    = 0x14CC,
            ESART0_RX_Route_Set                     = 0x14D0,
            MODEM_RouteEnable_Set                   = 0x14D8,
            MODEM_ANT0_Route_Set                    = 0x14DC,
            MODEM_ANT1_Route_Set                    = 0x14E0,
            MODEM_ANTROLLOVER_Route_Set             = 0x14E4,
            MODEM_ANTRR0_Route_Set                  = 0x14E8,
            MODEM_ANTRR1_Route_Set                  = 0x14EC,
            MODEM_ANTRR2_Route_Set                  = 0x14F0,
            MODEM_ANTRR3_Route_Set                  = 0x14F4,
            MODEM_ANTRR4_Route_Set                  = 0x14F8,
            MODEM_ANTRR5_Route_Set                  = 0x14FC,
            MODEM_ANTSWEN_Route_Set                 = 0x1500,
            MODEM_ANTSWUS_Route_Set                 = 0x1504,
            MODEM_ANTTRIG_Route_Set                 = 0x1508,
            MODEM_ANTTRIGSTOP_Route_Set             = 0x150C,
            MODEM_DCLK_Route_Set                    = 0x1510,
            MODEM_DIN_Route_Set                     = 0x1514,
            MODEM_DOUT_Route_Set                    = 0x1518,
            PDM_RouteEnable_Set                     = 0x1520,
            PDM_CLK_Route_Set                       = 0x1524,
            PDM_DAT0_Route_Set                      = 0x1528,
            PDM_DAT1_Route_Set                      = 0x152C,
            PRS0_RouteEnable_Set                    = 0x1534,
            PRS0_ASYNCH0_Route_Set                  = 0x1538,
            PRS0_ASYNCH1_Route_Set                  = 0x153C,
            PRS0_ASYNCH2_Route_Set                  = 0x1540,
            PRS0_ASYNCH3_Route_Set                  = 0x1544,
            PRS0_ASYNCH4_Route_Set                  = 0x1548,
            PRS0_ASYNCH5_Route_Set                  = 0x154C,
            PRS0_ASYNCH6_Route_Set                  = 0x1550,
            PRS0_ASYNCH7_Route_Set                  = 0x1554,
            PRS0_ASYNCH8_Route_Set                  = 0x1558,
            PRS0_ASYNCH9_Route_Set                  = 0x155C,
            PRS0_ASYNCH10_Route_Set                 = 0x1560,
            PRS0_ASYNCH11_Route_Set                 = 0x1564,
            PRS0_SYNCH0_Route_Set                   = 0x1568,
            PRS0_SYNCH1_Route_Set                   = 0x156C,
            PRS0_SYNCH2_Route_Set                   = 0x1570,
            PRS0_SYNCH3_Route_Set                   = 0x1574,
            TIMER0_RouteEnable_Set                  = 0x157C,
            TIMER0_CC0_Route_Set                    = 0x1580,
            TIMER0_CC1_Route_Set                    = 0x1584,
            TIMER0_CC2_Route_Set                    = 0x1588,
            TIMER0_CDTI0_Route_Set                  = 0x158C,
            TIMER0_CDTI1_Route_Set                  = 0x1590,
            TIMER0_CDTI2_Route_Set                  = 0x1594,
            TIMER1_RouteEnable_Set                  = 0x159C,
            TIMER1_CC0_Route_Set                    = 0x15A0,
            TIMER1_CC1_Route_Set                    = 0x15A4,
            TIMER1_CC2_Route_Set                    = 0x15A8,
            TIMER1_CDTI0_Route_Set                  = 0x15AC,
            TIMER1_CDTI1_Route_Set                  = 0x15B0,
            TIMER1_CDTI2_Route_Set                  = 0x15B4,
            TIMER2_RouteEnable_Set                  = 0x15BC,
            TIMER2_CC0_Route_Set                    = 0x15C0,
            TIMER2_CC1_Route_Set                    = 0x15C4,
            TIMER2_CC2_Route_Set                    = 0x15C8,
            TIMER2_CDTI0_Route_Set                  = 0x15CC,
            TIMER2_CDTI1_Route_Set                  = 0x15D0,
            TIMER2_CDTI2_Route_Set                  = 0x15D4,
            TIMER3_RouteEnable_Set                  = 0x15DC,
            TIMER3_CC0_Route_Set                    = 0x15E0,
            TIMER3_CC1_Route_Set                    = 0x15E4,
            TIMER3_CC2_Route_Set                    = 0x15E8,
            TIMER3_CDTI0_Route_Set                  = 0x15EC,
            TIMER3_CDTI1_Route_Set                  = 0x15F0,
            TIMER3_CDTI2_Route_Set                  = 0x15F4,
            TIMER4_RouteEnable_Set                  = 0x15FC,
            TIMER4_CC0_Route_Set                    = 0x1600,
            TIMER4_CC1_Route_Set                    = 0x1604,
            TIMER4_CC2_Route_Set                    = 0x1608,
            TIMER4_CDTI0_Route_Set                  = 0x160C,
            TIMER4_CDTI1_Route_Set                  = 0x1610,
            TIMER4_CDTI2_Route_Set                  = 0x1614,
            USART0_RouteEnable_Set                  = 0x161C,
            USART0_CS_Route_Set                     = 0x1620,
            USART0_CTS_Route_Set                    = 0x1624,
            USART0_RTS_Route_Set                    = 0x1628,
            USART0_RX_Route_Set                     = 0x162C,
            USART0_CLK_Route_Set                    = 0x1630,
            USART0_TX_Route_Set                     = 0x1634,
            USART1_RouteEnable_Set                  = 0x163C,
            USART1_CS_Route_Set                     = 0x1640,
            USART1_CTS_Route_Set                    = 0x1644,
            USART1_RTS_Route_Set                    = 0x1648,
            USART1_RX_Route_Set                     = 0x164C,
            USART1_CLK_Route_Set                    = 0x1650,
            USART1_TX_Route_Set                     = 0x1654,
            // Clear registers
            PortAControl_Clr                        = 0x2000,
            PortAModeLow_Clr                        = 0x2004,
            PortAModeHigh_Clr                       = 0x200C,
            PortADataOut_Clr                        = 0x2010,
            PortADataIn_Clr                         = 0x2014,
            PortBControl_Clr                        = 0x2030,
            PortBModeLow_Clr                        = 0x2034,
            PortBModeHigh_Clr                       = 0x204C,
            PortBDataOut_Clr                        = 0x2040,
            PortBDataIn_Clr                         = 0x2044,
            PortCControl_Clr                        = 0x2060,
            PortCModeLow_Clr                        = 0x2064,
            PortCModeHigh_Clr                       = 0x206C,
            PortCDataOut_Clr                        = 0x2070,
            PortCDataIn_Clr                         = 0x2074,
            PortDControl_Clr                        = 0x2090,
            PortDModeLow_Clr                        = 0x2094,
            PortDModeHigh_Clr                       = 0x209C,
            PortDDataOut_Clr                        = 0x20A0,
            PortDDataIn_Clr                         = 0x20A4,
            Lock_Clr                                = 0x2300,
            LockStatus_Clr                          = 0x2310,
            ABusAllocation_Clr                      = 0x2320,
            BBusAllocation_Clr                      = 0x2324,
            CDBusAllocation_Clr                     = 0x2328,
            ExternalInterruptPortSelectLow_Clr      = 0x2400,
            ExternalInterruptPortSelectHigh_Clr     = 0x2404,
            ExternalInterruptPinSelectLow_Clr       = 0x2408,
            ExternalInterruptPinSelectHigh_Clr      = 0x240C,
            ExternalInterruptRisingEdgeTrigger_Clr  = 0x2410,
            ExternalInterruptFallingEdgeTrigger_Clr = 0x2414,
            InterruptFlag_Clr                       = 0x2420,
            InterruptEnable_Clr                     = 0x2424,
            EM4WakeUpEnable_Clr                     = 0x242C,
            EM4WakeUpPolarity_Clr                   = 0x2430,
            DebugRoutePinEn_Clr                     = 0x2440,
            TraceRoutePinEn_Clr                     = 0x2444,
            CMU_RouteEnable_Clr                     = 0x2450,
            CMU_CLKIN0_Route_Clr                    = 0x2454,
            CMU_CLKOUT0_Route_Clr                   = 0x2458,
            CMU_CLKOUT1_Route_Clr                   = 0x245C,
            CMU_CLKOUT2_Route_Clr                   = 0x2460,
            DCDC_RouteEnable_Clr                    = 0x246C,
            FRC_RouteEnable_Clr                     = 0x247C,
            FRC_DCLK_Route_Clr                      = 0x2480,
            FRC_DFRAME_Route_Clr                    = 0x2484,
            FRC_DOUT_Route_Clr                      = 0x2488,
            I2C0_RouteEnable_Clr                    = 0x2490,
            I2C0_SCL_Route_Clr                      = 0x2494,
            I2C0_SDA_Route_Clr                      = 0x2498,
            I2C1_RouteEnable_Clr                    = 0x24A0,
            I2C1_SCL_Route_Clr                      = 0x24A4,
            I2C1_SDA_Route_Clr                      = 0x24A8,
            LETIMER_RouteEnable_Clr                 = 0x24B0,
            LETIMER_OUT0_Route_Clr                  = 0x24B4,
            LETIMER_OUT1_Route_Clr                  = 0x24B8,
            ESART0_RouteEnable_Clr                  = 0x24C0,
            ESART0_CS_Route_Clr                     = 0x24C4,
            ESART0_CTS_Route_Clr                    = 0x24C8,
            ESART0_RTS_Route_Clr                    = 0x24CC,
            ESART0_RX_Route_Clr                     = 0x24D0,
            MODEM_RouteEnable_Clr                   = 0x24D8,
            MODEM_ANT0_Route_Clr                    = 0x24DC,
            MODEM_ANT1_Route_Clr                    = 0x24E0,
            MODEM_ANTROLLOVER_Route_Clr             = 0x24E4,
            MODEM_ANTRR0_Route_Clr                  = 0x24E8,
            MODEM_ANTRR1_Route_Clr                  = 0x24EC,
            MODEM_ANTRR2_Route_Clr                  = 0x24F0,
            MODEM_ANTRR3_Route_Clr                  = 0x24F4,
            MODEM_ANTRR4_Route_Clr                  = 0x24F8,
            MODEM_ANTRR5_Route_Clr                  = 0x24FC,
            MODEM_ANTSWEN_Route_Clr                 = 0x2500,
            MODEM_ANTSWUS_Route_Clr                 = 0x2504,
            MODEM_ANTTRIG_Route_Clr                 = 0x2508,
            MODEM_ANTTRIGSTOP_Route_Clr             = 0x250C,
            MODEM_DCLK_Route_Clr                    = 0x2510,
            MODEM_DIN_Route_Clr                     = 0x2514,
            MODEM_DOUT_Route_Clr                    = 0x2518,
            PDM_RouteEnable_Clr                     = 0x2520,
            PDM_CLK_Route_Clr                       = 0x2524,
            PDM_DAT0_Route_Clr                      = 0x2528,
            PDM_DAT1_Route_Clr                      = 0x252C,
            PRS0_RouteEnable_Clr                    = 0x2534,
            PRS0_ASYNCH0_Route_Clr                  = 0x2538,
            PRS0_ASYNCH1_Route_Clr                  = 0x253C,
            PRS0_ASYNCH2_Route_Clr                  = 0x2540,
            PRS0_ASYNCH3_Route_Clr                  = 0x2544,
            PRS0_ASYNCH4_Route_Clr                  = 0x2548,
            PRS0_ASYNCH5_Route_Clr                  = 0x254C,
            PRS0_ASYNCH6_Route_Clr                  = 0x2550,
            PRS0_ASYNCH7_Route_Clr                  = 0x2554,
            PRS0_ASYNCH8_Route_Clr                  = 0x2558,
            PRS0_ASYNCH9_Route_Clr                  = 0x255C,
            PRS0_ASYNCH10_Route_Clr                 = 0x2560,
            PRS0_ASYNCH11_Route_Clr                 = 0x2564,
            PRS0_SYNCH0_Route_Clr                   = 0x2568,
            PRS0_SYNCH1_Route_Clr                   = 0x256C,
            PRS0_SYNCH2_Route_Clr                   = 0x2570,
            PRS0_SYNCH3_Route_Clr                   = 0x2574,
            TIMER0_RouteEnable_Clr                  = 0x257C,
            TIMER0_CC0_Route_Clr                    = 0x2580,
            TIMER0_CC1_Route_Clr                    = 0x2584,
            TIMER0_CC2_Route_Clr                    = 0x2588,
            TIMER0_CDTI0_Route_Clr                  = 0x258C,
            TIMER0_CDTI1_Route_Clr                  = 0x2590,
            TIMER0_CDTI2_Route_Clr                  = 0x2594,
            TIMER1_RouteEnable_Clr                  = 0x259C,
            TIMER1_CC0_Route_Clr                    = 0x25A0,
            TIMER1_CC1_Route_Clr                    = 0x25A4,
            TIMER1_CC2_Route_Clr                    = 0x25A8,
            TIMER1_CDTI0_Route_Clr                  = 0x25AC,
            TIMER1_CDTI1_Route_Clr                  = 0x25B0,
            TIMER1_CDTI2_Route_Clr                  = 0x25B4,
            TIMER2_RouteEnable_Clr                  = 0x25BC,
            TIMER2_CC0_Route_Clr                    = 0x25C0,
            TIMER2_CC1_Route_Clr                    = 0x25C4,
            TIMER2_CC2_Route_Clr                    = 0x25C8,
            TIMER2_CDTI0_Route_Clr                  = 0x25CC,
            TIMER2_CDTI1_Route_Clr                  = 0x25D0,
            TIMER2_CDTI2_Route_Clr                  = 0x25D4,
            TIMER3_RouteEnable_Clr                  = 0x25DC,
            TIMER3_CC0_Route_Clr                    = 0x25E0,
            TIMER3_CC1_Route_Clr                    = 0x25E4,
            TIMER3_CC2_Route_Clr                    = 0x25E8,
            TIMER3_CDTI0_Route_Clr                  = 0x25EC,
            TIMER3_CDTI1_Route_Clr                  = 0x25F0,
            TIMER3_CDTI2_Route_Clr                  = 0x25F4,
            TIMER4_RouteEnable_Clr                  = 0x25FC,
            TIMER4_CC0_Route_Clr                    = 0x2600,
            TIMER4_CC1_Route_Clr                    = 0x2604,
            TIMER4_CC2_Route_Clr                    = 0x2608,
            TIMER4_CDTI0_Route_Clr                  = 0x260C,
            TIMER4_CDTI1_Route_Clr                  = 0x2610,
            TIMER4_CDTI2_Route_Clr                  = 0x2614,
            USART0_RouteEnable_Clr                  = 0x261C,
            USART0_CS_Route_Clr                     = 0x2620,
            USART0_CTS_Route_Clr                    = 0x2624,
            USART0_RTS_Route_Clr                    = 0x2628,
            USART0_RX_Route_Clr                     = 0x262C,
            USART0_CLK_Route_Clr                    = 0x2630,
            USART0_TX_Route_Clr                     = 0x2634,
            USART1_RouteEnable_Clr                  = 0x263C,
            USART1_CS_Route_Clr                     = 0x2640,
            USART1_CTS_Route_Clr                    = 0x2644,
            USART1_RTS_Route_Clr                    = 0x2648,
            USART1_RX_Route_Clr                     = 0x264C,
            USART1_CLK_Route_Clr                    = 0x2650,
            USART1_TX_Route_Clr                     = 0x2654,
            // Toggle registers
            PortAControl_Tgl                        = 0x3000,
            PortAModeLow_Tgl                        = 0x3004,
            PortAModeHigh_Tgl                       = 0x300C,
            PortADataOut_Tgl                        = 0x3010,
            PortADataIn_Tgl                         = 0x3014,
            PortBControl_Tgl                        = 0x3030,
            PortBModeLow_Tgl                        = 0x3034,
            PortBModeHigh_Tgl                       = 0x304C,
            PortBDataOut_Tgl                        = 0x3040,
            PortBDataIn_Tgl                         = 0x3044,
            PortCControl_Tgl                        = 0x3060,
            PortCModeLow_Tgl                        = 0x3064,
            PortCModeHigh_Tgl                       = 0x306C,
            PortCDataOut_Tgl                        = 0x3070,
            PortCDataIn_Tgl                         = 0x3074,
            PortDControl_Tgl                        = 0x3090,
            PortDModeLow_Tgl                        = 0x3094,
            PortDModeHigh_Tgl                       = 0x309C,
            PortDDataOut_Tgl                        = 0x30A0,
            PortDDataIn_Tgl                         = 0x30A4,
            Lock_Tgl                                = 0x3300,
            LockStatus_Tgl                          = 0x3310,
            ABusAllocation_Tgl                      = 0x3320,
            BBusAllocation_Tgl                      = 0x3324,
            CDBusAllocation_Tgl                     = 0x3328,
            ExternalInterruptPortSelectLow_Tgl      = 0x3400,
            ExternalInterruptPortSelectHigh_Tgl     = 0x3404,
            ExternalInterruptPinSelectLow_Tgl       = 0x3408,
            ExternalInterruptPinSelectHigh_Tgl      = 0x340C,
            ExternalInterruptRisingEdgeTrigger_Tgl  = 0x3410,
            ExternalInterruptFallingEdgeTrigger_Tgl = 0x3414,
            InterruptFlag_Tgl                       = 0x3420,
            InterruptEnable_Tgl                     = 0x3424,
            EM4WakeUpEnable_Tgl                     = 0x342C,
            EM4WakeUpPolarity_Tgl                   = 0x3430,
            DebugRoutePinEn_Tgl                     = 0x3440,
            TraceRoutePinEn_Tgl                     = 0x3444,
            CMU_RouteEnable_Tgl                     = 0x3450,
            CMU_CLKIN0_Route_Tgl                    = 0x3454,
            CMU_CLKOUT0_Route_Tgl                   = 0x3458,
            CMU_CLKOUT1_Route_Tgl                   = 0x345C,
            CMU_CLKOUT2_Route_Tgl                   = 0x3460,
            DCDC_RouteEnable_Tgl                    = 0x346C,
            FRC_RouteEnable_Tgl                     = 0x347C,
            FRC_DCLK_Route_Tgl                      = 0x3480,
            FRC_DFRAME_Route_Tgl                    = 0x3484,
            FRC_DOUT_Route_Tgl                      = 0x3488,
            I2C0_RouteEnable_Tgl                    = 0x3490,
            I2C0_SCL_Route_Tgl                      = 0x3494,
            I2C0_SDA_Route_Tgl                      = 0x3498,
            I2C1_RouteEnable_Tgl                    = 0x34A0,
            I2C1_SCL_Route_Tgl                      = 0x34A4,
            I2C1_SDA_Route_Tgl                      = 0x34A8,
            LETIMER_RouteEnable_Tgl                 = 0x34B0,
            LETIMER_OUT0_Route_Tgl                  = 0x34B4,
            LETIMER_OUT1_Route_Tgl                  = 0x34B8,
            ESART0_RouteEnable_Tgl                  = 0x34C0,
            ESART0_CS_Route_Tgl                     = 0x34C4,
            ESART0_CTS_Route_Tgl                    = 0x34C8,
            ESART0_RTS_Route_Tgl                    = 0x34CC,
            ESART0_RX_Route_Tgl                     = 0x34D0,
            MODEM_RouteEnable_Tgl                   = 0x34D8,
            MODEM_ANT0_Route_Tgl                    = 0x34DC,
            MODEM_ANT1_Route_Tgl                    = 0x34E0,
            MODEM_ANTROLLOVER_Route_Tgl             = 0x34E4,
            MODEM_ANTRR0_Route_Tgl                  = 0x34E8,
            MODEM_ANTRR1_Route_Tgl                  = 0x34EC,
            MODEM_ANTRR2_Route_Tgl                  = 0x34F0,
            MODEM_ANTRR3_Route_Tgl                  = 0x34F4,
            MODEM_ANTRR4_Route_Tgl                  = 0x34F8,
            MODEM_ANTRR5_Route_Tgl                  = 0x34FC,
            MODEM_ANTSWEN_Route_Tgl                 = 0x3500,
            MODEM_ANTSWUS_Route_Tgl                 = 0x3504,
            MODEM_ANTTRIG_Route_Tgl                 = 0x3508,
            MODEM_ANTTRIGSTOP_Route_Tgl             = 0x350C,
            MODEM_DCLK_Route_Tgl                    = 0x3510,
            MODEM_DIN_Route_Tgl                     = 0x3514,
            MODEM_DOUT_Route_Tgl                    = 0x3518,
            PDM_RouteEnable_Tgl                     = 0x3520,
            PDM_CLK_Route_Tgl                       = 0x3524,
            PDM_DAT0_Route_Tgl                      = 0x3528,
            PDM_DAT1_Route_Tgl                      = 0x352C,
            PRS0_RouteEnable_Tgl                    = 0x3534,
            PRS0_ASYNCH0_Route_Tgl                  = 0x3538,
            PRS0_ASYNCH1_Route_Tgl                  = 0x353C,
            PRS0_ASYNCH2_Route_Tgl                  = 0x3540,
            PRS0_ASYNCH3_Route_Tgl                  = 0x3544,
            PRS0_ASYNCH4_Route_Tgl                  = 0x3548,
            PRS0_ASYNCH5_Route_Tgl                  = 0x354C,
            PRS0_ASYNCH6_Route_Tgl                  = 0x3550,
            PRS0_ASYNCH7_Route_Tgl                  = 0x3554,
            PRS0_ASYNCH8_Route_Tgl                  = 0x3558,
            PRS0_ASYNCH9_Route_Tgl                  = 0x355C,
            PRS0_ASYNCH10_Route_Tgl                 = 0x3560,
            PRS0_ASYNCH11_Route_Tgl                 = 0x3564,
            PRS0_SYNCH0_Route_Tgl                   = 0x3568,
            PRS0_SYNCH1_Route_Tgl                   = 0x356C,
            PRS0_SYNCH2_Route_Tgl                   = 0x3570,
            PRS0_SYNCH3_Route_Tgl                   = 0x3574,
            TIMER0_RouteEnable_Tgl                  = 0x357C,
            TIMER0_CC0_Route_Tgl                    = 0x3580,
            TIMER0_CC1_Route_Tgl                    = 0x3584,
            TIMER0_CC2_Route_Tgl                    = 0x3588,
            TIMER0_CDTI0_Route_Tgl                  = 0x358C,
            TIMER0_CDTI1_Route_Tgl                  = 0x3590,
            TIMER0_CDTI2_Route_Tgl                  = 0x3594,
            TIMER1_RouteEnable_Tgl                  = 0x359C,
            TIMER1_CC0_Route_Tgl                    = 0x35A0,
            TIMER1_CC1_Route_Tgl                    = 0x35A4,
            TIMER1_CC2_Route_Tgl                    = 0x35A8,
            TIMER1_CDTI0_Route_Tgl                  = 0x35AC,
            TIMER1_CDTI1_Route_Tgl                  = 0x35B0,
            TIMER1_CDTI2_Route_Tgl                  = 0x35B4,
            TIMER2_RouteEnable_Tgl                  = 0x35BC,
            TIMER2_CC0_Route_Tgl                    = 0x35C0,
            TIMER2_CC1_Route_Tgl                    = 0x35C4,
            TIMER2_CC2_Route_Tgl                    = 0x35C8,
            TIMER2_CDTI0_Route_Tgl                  = 0x35CC,
            TIMER2_CDTI1_Route_Tgl                  = 0x35D0,
            TIMER2_CDTI2_Route_Tgl                  = 0x35D4,
            TIMER3_RouteEnable_Tgl                  = 0x35DC,
            TIMER3_CC0_Route_Tgl                    = 0x35E0,
            TIMER3_CC1_Route_Tgl                    = 0x35E4,
            TIMER3_CC2_Route_Tgl                    = 0x35E8,
            TIMER3_CDTI0_Route_Tgl                  = 0x35EC,
            TIMER3_CDTI1_Route_Tgl                  = 0x35F0,
            TIMER3_CDTI2_Route_Tgl                  = 0x35F4,
            TIMER4_RouteEnable_Tgl                  = 0x35FC,
            TIMER4_CC0_Route_Tgl                    = 0x3600,
            TIMER4_CC1_Route_Tgl                    = 0x3604,
            TIMER4_CC2_Route_Tgl                    = 0x3608,
            TIMER4_CDTI0_Route_Tgl                  = 0x360C,
            TIMER4_CDTI1_Route_Tgl                  = 0x3610,
            TIMER4_CDTI2_Route_Tgl                  = 0x3614,
            USART0_RouteEnable_Tgl                  = 0x361C,
            USART0_CS_Route_Tgl                     = 0x3620,
            USART0_CTS_Route_Tgl                    = 0x3624,
            USART0_RTS_Route_Tgl                    = 0x3628,
            USART0_RX_Route_Tgl                     = 0x362C,
            USART0_CLK_Route_Tgl                    = 0x3630,
            USART0_TX_Route_Tgl                     = 0x3634,
            USART1_RouteEnable_Tgl                  = 0x363C,
            USART1_CS_Route_Tgl                     = 0x3640,
            USART1_CTS_Route_Tgl                    = 0x3644,
            USART1_RTS_Route_Tgl                    = 0x3648,
            USART1_RX_Route_Tgl                     = 0x364C,
            USART1_CLK_Route_Tgl                    = 0x3650,
            USART1_TX_Route_Tgl                     = 0x3654,            
       }
#endregion        
    }
}