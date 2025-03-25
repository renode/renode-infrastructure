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
    public class EFR32xG2_GPIO_3 : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_GPIO_3(Machine machine) : base(machine, NumberOfPins * NumberOfPorts)
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
                    .WithReservedBits(14, 2)
                    .WithEnumField<DoubleWordRegister, Port>(16, 2, out externalInterruptPortSelect[12], name: "EXTIPSEL12")
                    .WithReservedBits(18, 2)
                    .WithEnumField<DoubleWordRegister, Port>(20, 2, out externalInterruptPortSelect[13], name: "EXTIPSEL13")
                    .WithReservedBits(22, 2)
                    .WithEnumField<DoubleWordRegister, Port>(24, 2, out externalInterruptPortSelect[14], name: "EXTIPSEL14")
                    .WithReservedBits(26, 2)
                    .WithEnumField<DoubleWordRegister, Port>(28, 2, out externalInterruptPortSelect[15], name: "EXTIPSEL15")
                    .WithReservedBits(30, 2)
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
                    .WithReservedBits(14, 2)
                    .WithValueField(16, 2, out externalInterruptPinSelect[12], name: "EXTIPINSEL12")
                    .WithReservedBits(18, 2)
                    .WithValueField(20, 2, out externalInterruptPinSelect[13], name: "EXTIPINSEL13")
                    .WithReservedBits(22, 2)
                    .WithValueField(24, 2, out externalInterruptPinSelect[14], name: "EXTIPINSEL14")
                    .WithReservedBits(26, 2)
                    .WithValueField(28, 2, out externalInterruptPinSelect[15], name: "EXTIPINSEL15")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateRouting())
                },
                {(long)Registers.ExternalInterruptRisingEdgeTrigger, new DoubleWordRegister(this)
                    .WithFlags(0, 16, 
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
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.ExternalInterruptFallingEdgeTrigger, new DoubleWordRegister(this)
                    .WithFlags(0, 16, 
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
                    .WithReservedBits(16, 16)
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
                    .WithFlag(12, out externalInterrupt[12], name: "EXTIF12")
                    .WithFlag(13, out externalInterrupt[13], name: "EXTIF13")
                    .WithFlag(14, out externalInterrupt[14], name: "EXTIF14")
                    .WithFlag(15, out externalInterrupt[15], name: "EXTIF15")
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
                    .WithTaggedFlag("EM4WUIF12", 28)
                    .WithTaggedFlag("EM4WUIF13", 29)
                    .WithTaggedFlag("EM4WUIF14", 30)
                    .WithTaggedFlag("EM4WUIF15", 31)
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
                    .WithFlag(12, out externalInterruptEnable[12], name: "EXTIEN12")
                    .WithFlag(13, out externalInterruptEnable[13], name: "EXTIEN13")
                    .WithFlag(14, out externalInterruptEnable[14], name: "EXTIEN14")
                    .WithFlag(15, out externalInterruptEnable[15], name: "EXTIEN15")
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
                    .WithTaggedFlag("EM4WUIEN12", 28)
                    .WithTaggedFlag("EM4WUIEN13", 29)
                    .WithTaggedFlag("EM4WUIEN14", 30)
                    .WithTaggedFlag("EM4WUIEN15", 31)
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
                {(long)Registers.EUSART0_RouteEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out EUSART0_RouteEnable_CsPin, name: "CSPEN")
                    .WithFlag(1, out EUSART0_RouteEnable_RtsPin, name: "RTSPEN")
                    .WithFlag(2, out EUSART0_RouteEnable_RxPin, name: "RXPEN")
                    .WithFlag(3, out EUSART0_RouteEnable_SclkPin, name: "SCLKPEN")
                    .WithFlag(4, out EUSART0_RouteEnable_TxPin, name: "TXPEN")
                },
                {(long)Registers.EUSART0_RX_Route, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out EUSART0_RxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out EUSART0_RxRoutePin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.EUSART0_TX_Route, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out EUSART0_TxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out EUSART0_TxRoutePin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.EUSART1_RouteEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out EUSART1_RouteEnable_CsPin, name: "CSPEN")
                    .WithFlag(1, out EUSART1_RouteEnable_RtsPin, name: "RTSPEN")
                    .WithFlag(2, out EUSART1_RouteEnable_RxPin, name: "RXPEN")
                    .WithFlag(3, out EUSART1_RouteEnable_SclkPin, name: "SCLKPEN")
                    .WithFlag(4, out EUSART1_RouteEnable_TxPin, name: "TXPEN")
                },
                {(long)Registers.EUSART1_RX_Route, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out EUSART1_RxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out EUSART1_RxRoutePin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.EUSART1_TX_Route, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out EUSART1_TxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out EUSART1_TxRoutePin, name: "PIN")
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
                     .WithFlags(0, 16, 
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
                     .WithReservedBits(16, 16)
                    );
            regs.Add((long)Registers.PortADataIn + regOffset, new DoubleWordRegister(this)
                     .WithFlags(0, 16, FieldMode.Read,
                                valueProviderCallback: (i, _) =>
                                {
                                    var pin = pinOffset + i;
                                    return State[pin];
                                },
                                name: "DIN")
                     .WithReservedBits(16, 16)
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
        private const int NumberOfExternalInterrupts = 16;
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
        // EUSART0
        private IFlagRegisterField EUSART0_RouteEnable_TxPin;
        private IFlagRegisterField EUSART0_RouteEnable_SclkPin;
        private IFlagRegisterField EUSART0_RouteEnable_RxPin;
        private IFlagRegisterField EUSART0_RouteEnable_RtsPin;
        private IFlagRegisterField EUSART0_RouteEnable_CsPin;
        private IEnumRegisterField<Port> EUSART0_RxRoutePort;
        private IValueRegisterField EUSART0_RxRoutePin;
        private IEnumRegisterField<Port> EUSART0_TxRoutePort;
        private IValueRegisterField EUSART0_TxRoutePin;
        // EUSART1
        private IFlagRegisterField EUSART1_RouteEnable_TxPin;
        private IFlagRegisterField EUSART1_RouteEnable_SclkPin;
        private IFlagRegisterField EUSART1_RouteEnable_RxPin;
        private IFlagRegisterField EUSART1_RouteEnable_RtsPin;
        private IFlagRegisterField EUSART1_RouteEnable_CsPin;
        private IEnumRegisterField<Port> EUSART1_RxRoutePort;
        private IValueRegisterField EUSART1_RxRoutePin;
        private IEnumRegisterField<Port> EUSART1_TxRoutePort;
        private IValueRegisterField EUSART1_TxRoutePin;
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
                case SignalSource.EUSART0:
                {
                    switch(signalType)
                    {
                        case SignalType.EUSART0_RX:
                        {
                            if (EUSART0_RouteEnable_RxPin.Value)
                            {
                                pinNumber = GetPinNumber(EUSART0_RxRoutePort.Value, (uint)EUSART0_RxRoutePin.Value);
                            }
                            break;
                        }
                        default:
                            this.Log(LogLevel.Error, string.Format("GPIO Signal type {0} for EUSART0 not supported", signalType));
                            return pinNumber;
                    }
                    break;
                }
                case SignalSource.EUSART1:
                {
                    switch(signalType)
                    {
                        case SignalType.EUSART1_RX:
                        {
                            if (EUSART1_RouteEnable_RxPin.Value)
                            {
                                pinNumber = GetPinNumber(EUSART1_RxRoutePort.Value, (uint)EUSART1_RxRoutePin.Value);
                            }
                            break;
                        }
                        default:
                            this.Log(LogLevel.Error, string.Format("GPIO Signal type {0} for EUSART1 not supported", signalType));
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
            EUSART0  = 2,
            EUSART1  = 3,
        }

        private enum SignalType
        {
            // If SignalSource is USART0
            USART0_CTS              = 0,
            USART0_RTS              = 1,
            USART0_RX               = 2,
            USART0_SCLK             = 3,
            USART0_TX               = 4,
            // If SignalSource is EUSART0
            EUSART0_CTS             = 0,
            EUSART0_RTS             = 1,
            EUSART0_RX              = 2,
            EUSART0_SCLK            = 3,
            EUSART0_TX              = 4,
            // If SignalSource is EUSART1
            EUSART1_CTS             = 0,
            EUSART1_RTS             = 1,
            EUSART1_RX              = 2,
            EUSART1_SCLK            = 3,
            EUSART1_TX              = 4,
        }

        private enum Registers
        {
            IpVersion                               = 0x0000,
            PortAControl                            = 0x0030,
            PortAModeLow                            = 0x0034,
            PortAModeHigh                           = 0x003C,
            PortADataOut                            = 0x0040,
            PortADataIn                             = 0x0044,
            PortBControl                            = 0x0060,
            PortBModeLow                            = 0x0064,
            PortBModeHigh                           = 0x006C,
            PortBDataOut                            = 0x0070,
            PortBDataIn                             = 0x0074,
            PortCControl                            = 0x0090,
            PortCModeLow                            = 0x0094,
            PortCModeHigh                           = 0x009C,
            PortCDataOut                            = 0x00A0,
            PortCDataIn                             = 0x00A4,
            PortDControl                            = 0x00C0,
            PortDModeLow                            = 0x00C4,
            PortDModeHigh                           = 0x00CC,
            PortDDataOut                            = 0x00D0,
            PortDDataIn                             = 0x00D4,
            Lock                                    = 0x0300,
            LockStatus                              = 0x0310,
            ABusAllocation                          = 0x0320,
            BBusAllocation                          = 0x0324,
            CDBusAllocation                         = 0x0328,
            AOdd0Switch                             = 0x0330,
            AOdd1Switch                             = 0x0334,
            AEven0Switch                            = 0x0338,
            AEven1Switch                            = 0x033C,
            BOdd0Switch                             = 0x0340,
            BOdd1Switch                             = 0x0344,
            BEven0Switch                            = 0x0348,
            BEven1Switch                            = 0x034C,
            CDOdd0Switch                            = 0x0350,
            CDOdd1Switch                            = 0x0354,
            CDEven0Switch                           = 0x0358,
            CDEven1Switch                           = 0x035C,
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
            ACMP0_RouteEnable                       = 0x0450,
            ACMP0_ACMPOUT_Route                     = 0x0454,
            ACMP1_RouteEnable                       = 0x045C,
            ACMP1_ACMPOUT_Route                     = 0x0460,
            CMU_RouteEnable                         = 0x0468,
            CMU_CLKIN0_Route                        = 0x046C,
            CMU_CLKOUT0_Route                       = 0x0470,
            CMU_CLKOUT1_Route                       = 0x0474,
            CMU_CLKOUT2_Route                       = 0x0478,
            CMU_CLKOUTHIDDEN_Route                  = 0x047C,
            DCDC_RouteEnable                        = 0x0484,
            DCDC_COREHIDDEN_Route                   = 0x0488,
            DCDC_VCMPHIDDEN_Route                   = 0x048C,
            EUSART0_RouteEnable                     = 0x0494,
            EUSART0_CS_Route                        = 0x0498,
            EUSART0_CTS_Route                       = 0x049C,
            EUSART0_RTS_Route                       = 0x04A0,
            EUSART0_RX_Route                        = 0x04A4,
            EUSART0_SCLK_Route                      = 0x04A8,
            EUSART0_TX_Route                        = 0x04AC,
            EUSART1_RouteEnable                     = 0x04B4,
            EUSART1_CS_Route                        = 0x04B8,
            EUSART1_CTS_Route                       = 0x04BC,
            EUSART1_RTS_Route                       = 0x04C0,
            EUSART1_RX_Route                        = 0x04C4,
            EUSART1_SCLK_Route                      = 0x04C8,
            EUSART1_TX_Route                        = 0x04CC,
            FRC_RouteEnable                         = 0x04D4,
            FRC_DCLK_Route                          = 0x04D8,
            FRC_DFRAME_Route                        = 0x04DC,
            FRC_DOUT_Route                          = 0x04E0,
            I2C0_RouteEnable                        = 0x04E8,
            I2C0_SCL_Route                          = 0x04EC,
            I2C0_SDA_Route                          = 0x04F0,
            I2C1_RouteEnable                        = 0x04F8,
            I2C1_SCL_Route                          = 0x04FC,
            I2C1_SDA_Route                          = 0x0500,
            KEYSCAN_RouteEnable                     = 0x0508,
            KEYSCAN_COLOUT0_Route                   = 0x050C,
            KEYSCAN_COLOUT1_Route                   = 0x0510,
            KEYSCAN_COLOUT2_Route                   = 0x0514,
            KEYSCAN_COLOUT3_Route                   = 0x0518,
            KEYSCAN_COLOUT4_Route                   = 0x051C,
            KEYSCAN_COLOUT5_Route                   = 0x0520,
            KEYSCAN_COLOUT6_Route                   = 0x0524,
            KEYSCAN_COLOUT7_Route                   = 0x0528,
            KEYSCAN_ROWSENSE0_Route                 = 0x052C,
            KEYSCAN_ROWSENSE1_Route                 = 0x0530,
            KEYSCAN_ROWSENSE2_Route                 = 0x0534,
            KEYSCAN_ROWSENSE3_Route                 = 0x0538,
            KEYSCAN_ROWSENSE4_Route                 = 0x053C,
            KEYSCAN_ROWSENSE5_Route                 = 0x0540,
            LETIMER_RouteEnable                     = 0x0548,
            LETIMER_OUT0_Route                      = 0x054C,
            LETIMER_OUT1_Route                      = 0x0550,
            MODEM_RouteEnable                       = 0x0558,
            MODEM_ANT0_Route                        = 0x055C,
            MODEM_ANT1_Route                        = 0x0560,
            MODEM_ANTROLLOVER_Route                 = 0x0564,
            MODEM_ANTRR0_Route                      = 0x0568,
            MODEM_ANTRR1_Route                      = 0x056C,
            MODEM_ANTRR2_Route                      = 0x0570,
            MODEM_ANTRR3_Route                      = 0x0574,
            MODEM_ANTRR4_Route                      = 0x0578,
            MODEM_ANTRR5_Route                      = 0x057C,
            MODEM_ANTSWEN_Route                     = 0x0580,
            MODEM_ANTSWUS_Route                     = 0x0584,
            MODEM_ANTTRIG_Route                     = 0x0588,
            MODEM_ANTTRIGSTOP_Route                 = 0x058C,
            MODEM_DCLK_Route                        = 0x0590,
            MODEM_DIN_Route                         = 0x0594,
            MODEM_DOUT_Route                        = 0x0598,
            MODEM_S0IN_Route                        = 0x05A4,
            MODEM_S1IN_Route                        = 0x05A8,
            PRS0_RouteEnable                        = 0x05B0,
            PRS0_ASYNCH0_Route                      = 0x05B4,
            PRS0_ASYNCH1_Route                      = 0x05B8,
            PRS0_ASYNCH2_Route                      = 0x05BC,
            PRS0_ASYNCH3_Route                      = 0x05C0,
            PRS0_ASYNCH4_Route                      = 0x05C4,
            PRS0_ASYNCH5_Route                      = 0x05C8,
            PRS0_ASYNCH6_Route                      = 0x05CC,
            PRS0_ASYNCH7_Route                      = 0x05D0,
            PRS0_ASYNCH8_Route                      = 0x05D4,
            PRS0_ASYNCH9_Route                      = 0x05D8,
            PRS0_ASYNCH10_Route                     = 0x05DC,
            PRS0_ASYNCH11_Route                     = 0x05E0,
            PRS0_ASYNCH12_Route                     = 0x05E4,
            PRS0_ASYNCH13_Route                     = 0x05E8,
            PRS0_ASYNCH14_Route                     = 0x05EC,
            PRS0_ASYNCH15_Route                     = 0x05F0,
            PRS0_SYNCH0_Route                       = 0x05F4,
            PRS0_SYNCH1_Route                       = 0x05F8,
            PRS0_SYNCH2_Route                       = 0x05FC,
            PRS0_SYNCH3_Route                       = 0x0600,
            RAC_RouteEnable                         = 0x0608,
            RAC_LNAEN_Route                         = 0x060C,
            RAC_PAEN_Route                          = 0x0610,
            RFECA0_RouteEnable                      = 0x0618,
            RFECA0_DATAOUT0_Route                   = 0x061C,
            RFECA0_DATAOUT1_Route                   = 0x0620,
            RFECA0_DATAOUT2_Route                   = 0x0624,
            RFECA0_DATAOUT3_Route                   = 0x0628,
            RFECA0_DATAOUT4_Route                   = 0x062C,
            RFECA0_DATAOUT5_Route                   = 0x0630,
            RFECA0_DATAOUT6_Route                   = 0x0634,
            RFECA0_DATAOUT7_Route                   = 0x0638,
            RFECA0_DATAOUT8_Route                   = 0x063C,
            RFECA0_DATAOUT9_Route                   = 0x0640,
            RFECA0_DATAOUT10_Route                  = 0x0644,
            RFECA0_DATAOUT11_Route                  = 0x0648,
            RFECA0_DATAOUT12_Route                  = 0x064C,
            RFECA0_DATAOUT13_Route                  = 0x0650,
            RFECA0_DATAOUT14_Route                  = 0x0654,
            RFECA0_DATAOUT15_Route                  = 0x0658,
            RFECA0_DATAOUT16_Route                  = 0x065C,
            RFECA0_DATAOUT17_Route                  = 0x0660,
            RFECA0_DATAOUT18_Route                  = 0x0664,
            RFECA0_DATAVALID_Route                  = 0x0668,
            RFECA0_TRIGGERIN_Route                  = 0x066C,
            SYXO0_BUFOUTREQINASYNC_Route            = 0x0678,
            TIMER0_RouteEnable                      = 0x0680,
            TIMER0_CC0_Route                        = 0x0684,
            TIMER0_CC1_Route                        = 0x0688,
            TIMER0_CC2_Route                        = 0x068C,
            TIMER0_CDTI0_Route                      = 0x0690,
            TIMER0_CDTI1_Route                      = 0x0694,
            TIMER0_CDTI2_Route                      = 0x0698,
            TIMER1_RouteEnable                      = 0x06A0,
            TIMER1_CC0_Route                        = 0x06A4,
            TIMER1_CC1_Route                        = 0x06A8,
            TIMER1_CC2_Route                        = 0x06AC,
            TIMER1_CDTI0_Route                      = 0x06B0,
            TIMER1_CDTI1_Route                      = 0x06B4,
            TIMER1_CDTI2_Route                      = 0x06B8,
            TIMER2_RouteEnable                      = 0x06C0,
            TIMER2_CC0_Route                        = 0x06C4,
            TIMER2_CC1_Route                        = 0x06C8,
            TIMER2_CC2_Route                        = 0x06CC,
            TIMER2_CDTI0_Route                      = 0x06D0,
            TIMER2_CDTI1_Route                      = 0x06D4,
            TIMER2_CDTI2_Route                      = 0x06D8,
            TIMER3_RouteEnable                      = 0x06E0,
            TIMER3_CC0_Route                        = 0x06E4,
            TIMER3_CC1_Route                        = 0x06E8,
            TIMER3_CC2_Route                        = 0x06EC,
            TIMER3_CDTI0_Route                      = 0x06F0,
            TIMER3_CDTI1_Route                      = 0x06F4,
            TIMER3_CDTI2_Route                      = 0x06F8,
            TIMER4_RouteEnable                      = 0x0700,
            TIMER4_CC0_Route                        = 0x0704,
            TIMER4_CC1_Route                        = 0x0708,
            TIMER4_CC2_Route                        = 0x070C,
            TIMER4_CDTI0_Route                      = 0x0710,
            TIMER4_CDTI1_Route                      = 0x0714,
            TIMER4_CDTI2_Route                      = 0x0718,
            USART0_RouteEnable                      = 0x0720,
            USART0_CS_Route                         = 0x0724,
            USART0_CTS_Route                        = 0x0728,
            USART0_RTS_Route                        = 0x072C,
            USART0_RX_Route                         = 0x0730,
            USART0_CLK_Route                        = 0x0734,
            USART0_TX_Route                         = 0x0738,
            RootAccessTypeDescriptor0               = 0x0740,
            RootAccessTypeDescriptor1               = 0x0744,
            RootAccessTypeDescriptor6               = 0x0758,
            RootAccessTypeDescriptor8               = 0x0760,
            RootAccessTypeDescriptor9               = 0x0764,
            RootAccessTypeDescriptor10              = 0x0768,
            RootAccessTypeDescriptor11              = 0x076C,
            RootAccessTypeDescriptor12              = 0x0770,
            RootAccessTypeDescriptor13              = 0x0774,
            RootAccessTypeDescriptor14              = 0x0778,
            // Set registers
            IpVersion_Set                           = 0x1000,
            PortAControl_Set                        = 0x1030,
            PortAModeLow_Set                        = 0x1034,
            PortAModeHigh_Set                       = 0x103C,
            PortADataOut_Set                        = 0x1040,
            PortADataIn_Set                         = 0x1044,
            PortBControl_Set                        = 0x1060,
            PortBModeLow_Set                        = 0x1064,
            PortBModeHigh_Set                       = 0x106C,
            PortBDataOut_Set                        = 0x1070,
            PortBDataIn_Set                         = 0x1074,
            PortCControl_Set                        = 0x1090,
            PortCModeLow_Set                        = 0x1094,
            PortCModeHigh_Set                       = 0x109C,
            PortCDataOut_Set                        = 0x10A0,
            PortCDataIn_Set                         = 0x10A4,
            PortDControl_Set                        = 0x10C0,
            PortDModeLow_Set                        = 0x10C4,
            PortDModeHigh_Set                       = 0x10CC,
            PortDDataOut_Set                        = 0x10D0,
            PortDDataIn_Set                         = 0x10D4,
            Lock_Set                                = 0x1300,
            LockStatus_Set                          = 0x1310,
            ABusAllocation_Set                      = 0x1320,
            BBusAllocation_Set                      = 0x1324,
            CDBusAllocation_Set                     = 0x1328,
            AOdd0Switch_Set                         = 0x1330,
            AOdd1Switch_Set                         = 0x1334,
            AEven0Switch_Set                        = 0x1338,
            AEven1Switch_Set                        = 0x133C,
            BOdd0Switch_Set                         = 0x1340,
            BOdd1Switch_Set                         = 0x1344,
            BEven0Switch_Set                        = 0x1348,
            BEven1Switch_Set                        = 0x134C,
            CDOdd0Switch_Set                        = 0x1350,
            CDOdd1Switch_Set                        = 0x1354,
            CDEven0Switch_Set                       = 0x1358,
            CDEven1Switch_Set                       = 0x135C,
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
            ACMP0_RouteEnable_Set                   = 0x1450,
            ACMP0_ACMPOUT_Route_Set                 = 0x1454,
            ACMP1_RouteEnable_Set                   = 0x145C,
            ACMP1_ACMPOUT_Route_Set                 = 0x1460,
            CMU_RouteEnable_Set                     = 0x1468,
            CMU_CLKIN0_Route_Set                    = 0x146C,
            CMU_CLKOUT0_Route_Set                   = 0x1470,
            CMU_CLKOUT1_Route_Set                   = 0x1474,
            CMU_CLKOUT2_Route_Set                   = 0x1478,
            CMU_CLKOUTHIDDEN_Route_Set              = 0x147C,
            DCDC_RouteEnable_Set                    = 0x1484,
            DCDC_COREHIDDEN_Route_Set               = 0x1488,
            DCDC_VCMPHIDDEN_Route_Set               = 0x148C,
            EUSART0_RouteEnable_Set                 = 0x1494,
            EUSART0_CS_Route_Set                    = 0x1498,
            EUSART0_CTS_Route_Set                   = 0x149C,
            EUSART0_RTS_Route_Set                   = 0x14A0,
            EUSART0_RX_Route_Set                    = 0x14A4,
            EUSART0_SCLK_Route_Set                  = 0x14A8,
            EUSART0_TX_Route_Set                    = 0x14AC,
            EUSART1_RouteEnable_Set                 = 0x14B4,
            EUSART1_CS_Route_Set                    = 0x14B8,
            EUSART1_CTS_Route_Set                   = 0x14BC,
            EUSART1_RTS_Route_Set                   = 0x14C0,
            EUSART1_RX_Route_Set                    = 0x14C4,
            EUSART1_SCLK_Route_Set                  = 0x14C8,
            EUSART1_TX_Route_Set                    = 0x14CC,
            FRC_RouteEnable_Set                     = 0x14D4,
            FRC_DCLK_Route_Set                      = 0x14D8,
            FRC_DFRAME_Route_Set                    = 0x14DC,
            FRC_DOUT_Route_Set                      = 0x14E0,
            I2C0_RouteEnable_Set                    = 0x14E8,
            I2C0_SCL_Route_Set                      = 0x14EC,
            I2C0_SDA_Route_Set                      = 0x14F0,
            I2C1_RouteEnable_Set                    = 0x14F8,
            I2C1_SCL_Route_Set                      = 0x14FC,
            I2C1_SDA_Route_Set                      = 0x1500,
            KEYSCAN_RouteEnable_Set                 = 0x1508,
            KEYSCAN_COLOUT0_Route_Set               = 0x150C,
            KEYSCAN_COLOUT1_Route_Set               = 0x1510,
            KEYSCAN_COLOUT2_Route_Set               = 0x1514,
            KEYSCAN_COLOUT3_Route_Set               = 0x1518,
            KEYSCAN_COLOUT4_Route_Set               = 0x151C,
            KEYSCAN_COLOUT5_Route_Set               = 0x1520,
            KEYSCAN_COLOUT6_Route_Set               = 0x1524,
            KEYSCAN_COLOUT7_Route_Set               = 0x1528,
            KEYSCAN_ROWSENSE0_Route_Set             = 0x152C,
            KEYSCAN_ROWSENSE1_Route_Set             = 0x1530,
            KEYSCAN_ROWSENSE2_Route_Set             = 0x1534,
            KEYSCAN_ROWSENSE3_Route_Set             = 0x1538,
            KEYSCAN_ROWSENSE4_Route_Set             = 0x153C,
            KEYSCAN_ROWSENSE5_Route_Set             = 0x1540,
            LETIMER_RouteEnable_Set                 = 0x1548,
            LETIMER_OUT0_Route_Set                  = 0x154C,
            LETIMER_OUT1_Route_Set                  = 0x1550,
            MODEM_RouteEnable_Set                   = 0x1558,
            MODEM_ANT0_Route_Set                    = 0x155C,
            MODEM_ANT1_Route_Set                    = 0x1560,
            MODEM_ANTROLLOVER_Route_Set             = 0x1564,
            MODEM_ANTRR0_Route_Set                  = 0x1568,
            MODEM_ANTRR1_Route_Set                  = 0x156C,
            MODEM_ANTRR2_Route_Set                  = 0x1570,
            MODEM_ANTRR3_Route_Set                  = 0x1574,
            MODEM_ANTRR4_Route_Set                  = 0x1578,
            MODEM_ANTRR5_Route_Set                  = 0x157C,
            MODEM_ANTSWEN_Route_Set                 = 0x1580,
            MODEM_ANTSWUS_Route_Set                 = 0x1584,
            MODEM_ANTTRIG_Route_Set                 = 0x1588,
            MODEM_ANTTRIGSTOP_Route_Set             = 0x158C,
            MODEM_DCLK_Route_Set                    = 0x1590,
            MODEM_DIN_Route_Set                     = 0x1594,
            MODEM_DOUT_Route_Set                    = 0x1598,
            MODEM_S0IN_Route_Set                    = 0x15A4,
            MODEM_S1IN_Route_Set                    = 0x15A8,
            PRS0_RouteEnable_Set                    = 0x15B0,
            PRS0_ASYNCH0_Route_Set                  = 0x15B4,
            PRS0_ASYNCH1_Route_Set                  = 0x15B8,
            PRS0_ASYNCH2_Route_Set                  = 0x15BC,
            PRS0_ASYNCH3_Route_Set                  = 0x15C0,
            PRS0_ASYNCH4_Route_Set                  = 0x15C4,
            PRS0_ASYNCH5_Route_Set                  = 0x15C8,
            PRS0_ASYNCH6_Route_Set                  = 0x15CC,
            PRS0_ASYNCH7_Route_Set                  = 0x15D0,
            PRS0_ASYNCH8_Route_Set                  = 0x15D4,
            PRS0_ASYNCH9_Route_Set                  = 0x15D8,
            PRS0_ASYNCH10_Route_Set                 = 0x15DC,
            PRS0_ASYNCH11_Route_Set                 = 0x15E0,
            PRS0_ASYNCH12_Route_Set                 = 0x15E4,
            PRS0_ASYNCH13_Route_Set                 = 0x15E8,
            PRS0_ASYNCH14_Route_Set                 = 0x15EC,
            PRS0_ASYNCH15_Route_Set                 = 0x15F0,
            PRS0_SYNCH0_Route_Set                   = 0x15F4,
            PRS0_SYNCH1_Route_Set                   = 0x15F8,
            PRS0_SYNCH2_Route_Set                   = 0x15FC,
            PRS0_SYNCH3_Route_Set                   = 0x1600,
            RAC_RouteEnable_Set                     = 0x1608,
            RAC_LNAEN_Route_Set                     = 0x160C,
            RAC_PAEN_Route_Set                      = 0x1610,
            RFECA0_RouteEnable_Set                  = 0x1618,
            RFECA0_DATAOUT0_Route_Set               = 0x161C,
            RFECA0_DATAOUT1_Route_Set               = 0x1620,
            RFECA0_DATAOUT2_Route_Set               = 0x1624,
            RFECA0_DATAOUT3_Route_Set               = 0x1628,
            RFECA0_DATAOUT4_Route_Set               = 0x162C,
            RFECA0_DATAOUT5_Route_Set               = 0x1630,
            RFECA0_DATAOUT6_Route_Set               = 0x1634,
            RFECA0_DATAOUT7_Route_Set               = 0x1638,
            RFECA0_DATAOUT8_Route_Set               = 0x163C,
            RFECA0_DATAOUT9_Route_Set               = 0x1640,
            RFECA0_DATAOUT10_Route_Set              = 0x1644,
            RFECA0_DATAOUT11_Route_Set              = 0x1648,
            RFECA0_DATAOUT12_Route_Set              = 0x164C,
            RFECA0_DATAOUT13_Route_Set              = 0x1650,
            RFECA0_DATAOUT14_Route_Set              = 0x1654,
            RFECA0_DATAOUT15_Route_Set              = 0x1658,
            RFECA0_DATAOUT16_Route_Set              = 0x165C,
            RFECA0_DATAOUT17_Route_Set              = 0x1660,
            RFECA0_DATAOUT18_Route_Set              = 0x1664,
            RFECA0_DATAVALID_Route_Set              = 0x1668,
            RFECA0_TRIGGERIN_Route_Set              = 0x166C,
            SYXO0_BUFOUTREQINASYNC_Route_Set        = 0x1678,
            TIMER0_RouteEnable_Set                  = 0x1680,
            TIMER0_CC0_Route_Set                    = 0x1684,
            TIMER0_CC1_Route_Set                    = 0x1688,
            TIMER0_CC2_Route_Set                    = 0x168C,
            TIMER0_CDTI0_Route_Set                  = 0x1690,
            TIMER0_CDTI1_Route_Set                  = 0x1694,
            TIMER0_CDTI2_Route_Set                  = 0x1698,
            TIMER1_RouteEnable_Set                  = 0x16A0,
            TIMER1_CC0_Route_Set                    = 0x16A4,
            TIMER1_CC1_Route_Set                    = 0x16A8,
            TIMER1_CC2_Route_Set                    = 0x16AC,
            TIMER1_CDTI0_Route_Set                  = 0x16B0,
            TIMER1_CDTI1_Route_Set                  = 0x16B4,
            TIMER1_CDTI2_Route_Set                  = 0x16B8,
            TIMER2_RouteEnable_Set                  = 0x16C0,
            TIMER2_CC0_Route_Set                    = 0x16C4,
            TIMER2_CC1_Route_Set                    = 0x16C8,
            TIMER2_CC2_Route_Set                    = 0x16CC,
            TIMER2_CDTI0_Route_Set                  = 0x16D0,
            TIMER2_CDTI1_Route_Set                  = 0x16D4,
            TIMER2_CDTI2_Route_Set                  = 0x16D8,
            TIMER3_RouteEnable_Set                  = 0x16E0,
            TIMER3_CC0_Route_Set                    = 0x16E4,
            TIMER3_CC1_Route_Set                    = 0x16E8,
            TIMER3_CC2_Route_Set                    = 0x16EC,
            TIMER3_CDTI0_Route_Set                  = 0x16F0,
            TIMER3_CDTI1_Route_Set                  = 0x16F4,
            TIMER3_CDTI2_Route_Set                  = 0x16F8,
            TIMER4_RouteEnable_Set                  = 0x1700,
            TIMER4_CC0_Route_Set                    = 0x1704,
            TIMER4_CC1_Route_Set                    = 0x1708,
            TIMER4_CC2_Route_Set                    = 0x170C,
            TIMER4_CDTI0_Route_Set                  = 0x1710,
            TIMER4_CDTI1_Route_Set                  = 0x1714,
            TIMER4_CDTI2_Route_Set                  = 0x1718,
            USART0_RouteEnable_Set                  = 0x1720,
            USART0_CS_Route_Set                     = 0x1724,
            USART0_CTS_Route_Set                    = 0x1728,
            USART0_RTS_Route_Set                    = 0x172C,
            USART0_RX_Route_Set                     = 0x1730,
            USART0_CLK_Route_Set                    = 0x1734,
            USART0_TX_Route_Set                     = 0x1738,
            RootAccessTypeDescriptor0_Set           = 0x1740,
            RootAccessTypeDescriptor1_Set           = 0x1744,
            RootAccessTypeDescriptor6_Set           = 0x1758,
            RootAccessTypeDescriptor8_Set           = 0x1760,
            RootAccessTypeDescriptor9_Set           = 0x1764,
            RootAccessTypeDescriptor10_Set          = 0x1768,
            RootAccessTypeDescriptor11_Set          = 0x176C,
            RootAccessTypeDescriptor12_Set          = 0x1770,
            RootAccessTypeDescriptor13_Set          = 0x1774,
            RootAccessTypeDescriptor14_Set          = 0x1778,
            // Clear registers
            IpVersion_Clr                           = 0x2000,
            PortAControl_Clr                        = 0x2030,
            PortAModeLow_Clr                        = 0x2034,
            PortAModeHigh_Clr                       = 0x203C,
            PortADataOut_Clr                        = 0x2040,
            PortADataIn_Clr                         = 0x2044,
            PortBControl_Clr                        = 0x2060,
            PortBModeLow_Clr                        = 0x2064,
            PortBModeHigh_Clr                       = 0x206C,
            PortBDataOut_Clr                        = 0x2070,
            PortBDataIn_Clr                         = 0x2074,
            PortCControl_Clr                        = 0x2090,
            PortCModeLow_Clr                        = 0x2094,
            PortCModeHigh_Clr                       = 0x209C,
            PortCDataOut_Clr                        = 0x20A0,
            PortCDataIn_Clr                         = 0x20A4,
            PortDControl_Clr                        = 0x20C0,
            PortDModeLow_Clr                        = 0x20C4,
            PortDModeHigh_Clr                       = 0x20CC,
            PortDDataOut_Clr                        = 0x20D0,
            PortDDataIn_Clr                         = 0x20D4,
            Lock_Clr                                = 0x2300,
            LockStatus_Clr                          = 0x2310,
            ABusAllocation_Clr                      = 0x2320,
            BBusAllocation_Clr                      = 0x2324,
            CDBusAllocation_Clr                     = 0x2328,
            AOdd0Switch_Clr                         = 0x2330,
            AOdd1Switch_Clr                         = 0x2334,
            AEven0Switch_Clr                        = 0x2338,
            AEven1Switch_Clr                        = 0x233C,
            BOdd0Switch_Clr                         = 0x2340,
            BOdd1Switch_Clr                         = 0x2344,
            BEven0Switch_Clr                        = 0x2348,
            BEven1Switch_Clr                        = 0x234C,
            CDOdd0Switch_Clr                        = 0x2350,
            CDOdd1Switch_Clr                        = 0x2354,
            CDEven0Switch_Clr                       = 0x2358,
            CDEven1Switch_Clr                       = 0x235C,
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
            ACMP0_RouteEnable_Clr                   = 0x2450,
            ACMP0_ACMPOUT_Route_Clr                 = 0x2454,
            ACMP1_RouteEnable_Clr                   = 0x245C,
            ACMP1_ACMPOUT_Route_Clr                 = 0x2460,
            CMU_RouteEnable_Clr                     = 0x2468,
            CMU_CLKIN0_Route_Clr                    = 0x246C,
            CMU_CLKOUT0_Route_Clr                   = 0x2470,
            CMU_CLKOUT1_Route_Clr                   = 0x2474,
            CMU_CLKOUT2_Route_Clr                   = 0x2478,
            CMU_CLKOUTHIDDEN_Route_Clr              = 0x247C,
            DCDC_RouteEnable_Clr                    = 0x2484,
            DCDC_COREHIDDEN_Route_Clr               = 0x2488,
            DCDC_VCMPHIDDEN_Route_Clr               = 0x248C,
            EUSART0_RouteEnable_Clr                 = 0x2494,
            EUSART0_CS_Route_Clr                    = 0x2498,
            EUSART0_CTS_Route_Clr                   = 0x249C,
            EUSART0_RTS_Route_Clr                   = 0x24A0,
            EUSART0_RX_Route_Clr                    = 0x24A4,
            EUSART0_SCLK_Route_Clr                  = 0x24A8,
            EUSART0_TX_Route_Clr                    = 0x24AC,
            EUSART1_RouteEnable_Clr                 = 0x24B4,
            EUSART1_CS_Route_Clr                    = 0x24B8,
            EUSART1_CTS_Route_Clr                   = 0x24BC,
            EUSART1_RTS_Route_Clr                   = 0x24C0,
            EUSART1_RX_Route_Clr                    = 0x24C4,
            EUSART1_SCLK_Route_Clr                  = 0x24C8,
            EUSART1_TX_Route_Clr                    = 0x24CC,
            FRC_RouteEnable_Clr                     = 0x24D4,
            FRC_DCLK_Route_Clr                      = 0x24D8,
            FRC_DFRAME_Route_Clr                    = 0x24DC,
            FRC_DOUT_Route_Clr                      = 0x24E0,
            I2C0_RouteEnable_Clr                    = 0x24E8,
            I2C0_SCL_Route_Clr                      = 0x24EC,
            I2C0_SDA_Route_Clr                      = 0x24F0,
            I2C1_RouteEnable_Clr                    = 0x24F8,
            I2C1_SCL_Route_Clr                      = 0x24FC,
            I2C1_SDA_Route_Clr                      = 0x2500,
            KEYSCAN_RouteEnable_Clr                 = 0x2508,
            KEYSCAN_COLOUT0_Route_Clr               = 0x250C,
            KEYSCAN_COLOUT1_Route_Clr               = 0x2510,
            KEYSCAN_COLOUT2_Route_Clr               = 0x2514,
            KEYSCAN_COLOUT3_Route_Clr               = 0x2518,
            KEYSCAN_COLOUT4_Route_Clr               = 0x251C,
            KEYSCAN_COLOUT5_Route_Clr               = 0x2520,
            KEYSCAN_COLOUT6_Route_Clr               = 0x2524,
            KEYSCAN_COLOUT7_Route_Clr               = 0x2528,
            KEYSCAN_ROWSENSE0_Route_Clr             = 0x252C,
            KEYSCAN_ROWSENSE1_Route_Clr             = 0x2530,
            KEYSCAN_ROWSENSE2_Route_Clr             = 0x2534,
            KEYSCAN_ROWSENSE3_Route_Clr             = 0x2538,
            KEYSCAN_ROWSENSE4_Route_Clr             = 0x253C,
            KEYSCAN_ROWSENSE5_Route_Clr             = 0x2540,
            LETIMER_RouteEnable_Clr                 = 0x2548,
            LETIMER_OUT0_Route_Clr                  = 0x254C,
            LETIMER_OUT1_Route_Clr                  = 0x2550,
            MODEM_RouteEnable_Clr                   = 0x2558,
            MODEM_ANT0_Route_Clr                    = 0x255C,
            MODEM_ANT1_Route_Clr                    = 0x2560,
            MODEM_ANTROLLOVER_Route_Clr             = 0x2564,
            MODEM_ANTRR0_Route_Clr                  = 0x2568,
            MODEM_ANTRR1_Route_Clr                  = 0x256C,
            MODEM_ANTRR2_Route_Clr                  = 0x2570,
            MODEM_ANTRR3_Route_Clr                  = 0x2574,
            MODEM_ANTRR4_Route_Clr                  = 0x2578,
            MODEM_ANTRR5_Route_Clr                  = 0x257C,
            MODEM_ANTSWEN_Route_Clr                 = 0x2580,
            MODEM_ANTSWUS_Route_Clr                 = 0x2584,
            MODEM_ANTTRIG_Route_Clr                 = 0x2588,
            MODEM_ANTTRIGSTOP_Route_Clr             = 0x258C,
            MODEM_DCLK_Route_Clr                    = 0x2590,
            MODEM_DIN_Route_Clr                     = 0x2594,
            MODEM_DOUT_Route_Clr                    = 0x2598,
            MODEM_S0IN_Route_Clr                    = 0x25A4,
            MODEM_S1IN_Route_Clr                    = 0x25A8,
            PRS0_RouteEnable_Clr                    = 0x25B0,
            PRS0_ASYNCH0_Route_Clr                  = 0x25B4,
            PRS0_ASYNCH1_Route_Clr                  = 0x25B8,
            PRS0_ASYNCH2_Route_Clr                  = 0x25BC,
            PRS0_ASYNCH3_Route_Clr                  = 0x25C0,
            PRS0_ASYNCH4_Route_Clr                  = 0x25C4,
            PRS0_ASYNCH5_Route_Clr                  = 0x25C8,
            PRS0_ASYNCH6_Route_Clr                  = 0x25CC,
            PRS0_ASYNCH7_Route_Clr                  = 0x25D0,
            PRS0_ASYNCH8_Route_Clr                  = 0x25D4,
            PRS0_ASYNCH9_Route_Clr                  = 0x25D8,
            PRS0_ASYNCH10_Route_Clr                 = 0x25DC,
            PRS0_ASYNCH11_Route_Clr                 = 0x25E0,
            PRS0_ASYNCH12_Route_Clr                 = 0x25E4,
            PRS0_ASYNCH13_Route_Clr                 = 0x25E8,
            PRS0_ASYNCH14_Route_Clr                 = 0x25EC,
            PRS0_ASYNCH15_Route_Clr                 = 0x25F0,
            PRS0_SYNCH0_Route_Clr                   = 0x25F4,
            PRS0_SYNCH1_Route_Clr                   = 0x25F8,
            PRS0_SYNCH2_Route_Clr                   = 0x25FC,
            PRS0_SYNCH3_Route_Clr                   = 0x2600,
            RAC_RouteEnable_Clr                     = 0x2608,
            RAC_LNAEN_Route_Clr                     = 0x260C,
            RAC_PAEN_Route_Clr                      = 0x2610,
            RFECA0_RouteEnable_Clr                  = 0x2618,
            RFECA0_DATAOUT0_Route_Clr               = 0x261C,
            RFECA0_DATAOUT1_Route_Clr               = 0x2620,
            RFECA0_DATAOUT2_Route_Clr               = 0x2624,
            RFECA0_DATAOUT3_Route_Clr               = 0x2628,
            RFECA0_DATAOUT4_Route_Clr               = 0x262C,
            RFECA0_DATAOUT5_Route_Clr               = 0x2630,
            RFECA0_DATAOUT6_Route_Clr               = 0x2634,
            RFECA0_DATAOUT7_Route_Clr               = 0x2638,
            RFECA0_DATAOUT8_Route_Clr               = 0x263C,
            RFECA0_DATAOUT9_Route_Clr               = 0x2640,
            RFECA0_DATAOUT10_Route_Clr              = 0x2644,
            RFECA0_DATAOUT11_Route_Clr              = 0x2648,
            RFECA0_DATAOUT12_Route_Clr              = 0x264C,
            RFECA0_DATAOUT13_Route_Clr              = 0x2650,
            RFECA0_DATAOUT14_Route_Clr              = 0x2654,
            RFECA0_DATAOUT15_Route_Clr              = 0x2658,
            RFECA0_DATAOUT16_Route_Clr              = 0x265C,
            RFECA0_DATAOUT17_Route_Clr              = 0x2660,
            RFECA0_DATAOUT18_Route_Clr              = 0x2664,
            RFECA0_DATAVALID_Route_Clr              = 0x2668,
            RFECA0_TRIGGERIN_Route_Clr              = 0x266C,
            SYXO0_BUFOUTREQINASYNC_Route_Clr        = 0x2678,
            TIMER0_RouteEnable_Clr                  = 0x2680,
            TIMER0_CC0_Route_Clr                    = 0x2684,
            TIMER0_CC1_Route_Clr                    = 0x2688,
            TIMER0_CC2_Route_Clr                    = 0x268C,
            TIMER0_CDTI0_Route_Clr                  = 0x2690,
            TIMER0_CDTI1_Route_Clr                  = 0x2694,
            TIMER0_CDTI2_Route_Clr                  = 0x2698,
            TIMER1_RouteEnable_Clr                  = 0x26A0,
            TIMER1_CC0_Route_Clr                    = 0x26A4,
            TIMER1_CC1_Route_Clr                    = 0x26A8,
            TIMER1_CC2_Route_Clr                    = 0x26AC,
            TIMER1_CDTI0_Route_Clr                  = 0x26B0,
            TIMER1_CDTI1_Route_Clr                  = 0x26B4,
            TIMER1_CDTI2_Route_Clr                  = 0x26B8,
            TIMER2_RouteEnable_Clr                  = 0x26C0,
            TIMER2_CC0_Route_Clr                    = 0x26C4,
            TIMER2_CC1_Route_Clr                    = 0x26C8,
            TIMER2_CC2_Route_Clr                    = 0x26CC,
            TIMER2_CDTI0_Route_Clr                  = 0x26D0,
            TIMER2_CDTI1_Route_Clr                  = 0x26D4,
            TIMER2_CDTI2_Route_Clr                  = 0x26D8,
            TIMER3_RouteEnable_Clr                  = 0x26E0,
            TIMER3_CC0_Route_Clr                    = 0x26E4,
            TIMER3_CC1_Route_Clr                    = 0x26E8,
            TIMER3_CC2_Route_Clr                    = 0x26EC,
            TIMER3_CDTI0_Route_Clr                  = 0x26F0,
            TIMER3_CDTI1_Route_Clr                  = 0x26F4,
            TIMER3_CDTI2_Route_Clr                  = 0x26F8,
            TIMER4_RouteEnable_Clr                  = 0x2700,
            TIMER4_CC0_Route_Clr                    = 0x2704,
            TIMER4_CC1_Route_Clr                    = 0x2708,
            TIMER4_CC2_Route_Clr                    = 0x270C,
            TIMER4_CDTI0_Route_Clr                  = 0x2710,
            TIMER4_CDTI1_Route_Clr                  = 0x2714,
            TIMER4_CDTI2_Route_Clr                  = 0x2718,
            USART0_RouteEnable_Clr                  = 0x2720,
            USART0_CS_Route_Clr                     = 0x2724,
            USART0_CTS_Route_Clr                    = 0x2728,
            USART0_RTS_Route_Clr                    = 0x272C,
            USART0_RX_Route_Clr                     = 0x2730,
            USART0_CLK_Route_Clr                    = 0x2734,
            USART0_TX_Route_Clr                     = 0x2738,
            RootAccessTypeDescriptor0_Clr           = 0x2740,
            RootAccessTypeDescriptor1_Clr           = 0x2744,
            RootAccessTypeDescriptor6_Clr           = 0x2758,
            RootAccessTypeDescriptor8_Clr           = 0x2760,
            RootAccessTypeDescriptor9_Clr           = 0x2764,
            RootAccessTypeDescriptor10_Clr          = 0x2768,
            RootAccessTypeDescriptor11_Clr          = 0x276C,
            RootAccessTypeDescriptor12_Clr          = 0x2770,
            RootAccessTypeDescriptor13_Clr          = 0x2774,
            RootAccessTypeDescriptor14_Clr          = 0x2778,
            // Toggle registers
            IpVersion_Tgl                           = 0x3000,
            PortAControl_Tgl                        = 0x3030,
            PortAModeLow_Tgl                        = 0x3034,
            PortAModeHigh_Tgl                       = 0x303C,
            PortADataOut_Tgl                        = 0x3040,
            PortADataIn_Tgl                         = 0x3044,
            PortBControl_Tgl                        = 0x3060,
            PortBModeLow_Tgl                        = 0x3064,
            PortBModeHigh_Tgl                       = 0x306C,
            PortBDataOut_Tgl                        = 0x3070,
            PortBDataIn_Tgl                         = 0x3074,
            PortCControl_Tgl                        = 0x3090,
            PortCModeLow_Tgl                        = 0x3094,
            PortCModeHigh_Tgl                       = 0x309C,
            PortCDataOut_Tgl                        = 0x30A0,
            PortCDataIn_Tgl                         = 0x30A4,
            PortDControl_Tgl                        = 0x30C0,
            PortDModeLow_Tgl                        = 0x30C4,
            PortDModeHigh_Tgl                       = 0x30CC,
            PortDDataOut_Tgl                        = 0x30D0,
            PortDDataIn_Tgl                         = 0x30D4,
            Lock_Tgl                                = 0x3300,
            LockStatus_Tgl                          = 0x3310,
            ABusAllocation_Tgl                      = 0x3320,
            BBusAllocation_Tgl                      = 0x3324,
            CDBusAllocation_Tgl                     = 0x3328,
            AOdd0Switch_Tgl                         = 0x3330,
            AOdd1Switch_Tgl                         = 0x3334,
            AEven0Switch_Tgl                        = 0x3338,
            AEven1Switch_Tgl                        = 0x333C,
            BOdd0Switch_Tgl                         = 0x3340,
            BOdd1Switch_Tgl                         = 0x3344,
            BEven0Switch_Tgl                        = 0x3348,
            BEven1Switch_Tgl                        = 0x334C,
            CDOdd0Switch_Tgl                        = 0x3350,
            CDOdd1Switch_Tgl                        = 0x3354,
            CDEven0Switch_Tgl                       = 0x3358,
            CDEven1Switch_Tgl                       = 0x335C,
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
            ACMP0_RouteEnable_Tgl                   = 0x3450,
            ACMP0_ACMPOUT_Route_Tgl                 = 0x3454,
            ACMP1_RouteEnable_Tgl                   = 0x345C,
            ACMP1_ACMPOUT_Route_Tgl                 = 0x3460,
            CMU_RouteEnable_Tgl                     = 0x3468,
            CMU_CLKIN0_Route_Tgl                    = 0x346C,
            CMU_CLKOUT0_Route_Tgl                   = 0x3470,
            CMU_CLKOUT1_Route_Tgl                   = 0x3474,
            CMU_CLKOUT2_Route_Tgl                   = 0x3478,
            CMU_CLKOUTHIDDEN_Route_Tgl              = 0x347C,
            DCDC_RouteEnable_Tgl                    = 0x3484,
            DCDC_COREHIDDEN_Route_Tgl               = 0x3488,
            DCDC_VCMPHIDDEN_Route_Tgl               = 0x348C,
            EUSART0_RouteEnable_Tgl                 = 0x3494,
            EUSART0_CS_Route_Tgl                    = 0x3498,
            EUSART0_CTS_Route_Tgl                   = 0x349C,
            EUSART0_RTS_Route_Tgl                   = 0x34A0,
            EUSART0_RX_Route_Tgl                    = 0x34A4,
            EUSART0_SCLK_Route_Tgl                  = 0x34A8,
            EUSART0_TX_Route_Tgl                    = 0x34AC,
            EUSART1_RouteEnable_Tgl                 = 0x34B4,
            EUSART1_CS_Route_Tgl                    = 0x34B8,
            EUSART1_CTS_Route_Tgl                   = 0x34BC,
            EUSART1_RTS_Route_Tgl                   = 0x34C0,
            EUSART1_RX_Route_Tgl                    = 0x34C4,
            EUSART1_SCLK_Route_Tgl                  = 0x34C8,
            EUSART1_TX_Route_Tgl                    = 0x34CC,
            FRC_RouteEnable_Tgl                     = 0x34D4,
            FRC_DCLK_Route_Tgl                      = 0x34D8,
            FRC_DFRAME_Route_Tgl                    = 0x34DC,
            FRC_DOUT_Route_Tgl                      = 0x34E0,
            I2C0_RouteEnable_Tgl                    = 0x34E8,
            I2C0_SCL_Route_Tgl                      = 0x34EC,
            I2C0_SDA_Route_Tgl                      = 0x34F0,
            I2C1_RouteEnable_Tgl                    = 0x34F8,
            I2C1_SCL_Route_Tgl                      = 0x34FC,
            I2C1_SDA_Route_Tgl                      = 0x3500,
            KEYSCAN_RouteEnable_Tgl                 = 0x3508,
            KEYSCAN_COLOUT0_Route_Tgl               = 0x350C,
            KEYSCAN_COLOUT1_Route_Tgl               = 0x3510,
            KEYSCAN_COLOUT2_Route_Tgl               = 0x3514,
            KEYSCAN_COLOUT3_Route_Tgl               = 0x3518,
            KEYSCAN_COLOUT4_Route_Tgl               = 0x351C,
            KEYSCAN_COLOUT5_Route_Tgl               = 0x3520,
            KEYSCAN_COLOUT6_Route_Tgl               = 0x3524,
            KEYSCAN_COLOUT7_Route_Tgl               = 0x3528,
            KEYSCAN_ROWSENSE0_Route_Tgl             = 0x352C,
            KEYSCAN_ROWSENSE1_Route_Tgl             = 0x3530,
            KEYSCAN_ROWSENSE2_Route_Tgl             = 0x3534,
            KEYSCAN_ROWSENSE3_Route_Tgl             = 0x3538,
            KEYSCAN_ROWSENSE4_Route_Tgl             = 0x353C,
            KEYSCAN_ROWSENSE5_Route_Tgl             = 0x3540,
            LETIMER_RouteEnable_Tgl                 = 0x3548,
            LETIMER_OUT0_Route_Tgl                  = 0x354C,
            LETIMER_OUT1_Route_Tgl                  = 0x3550,
            MODEM_RouteEnable_Tgl                   = 0x3558,
            MODEM_ANT0_Route_Tgl                    = 0x355C,
            MODEM_ANT1_Route_Tgl                    = 0x3560,
            MODEM_ANTROLLOVER_Route_Tgl             = 0x3564,
            MODEM_ANTRR0_Route_Tgl                  = 0x3568,
            MODEM_ANTRR1_Route_Tgl                  = 0x356C,
            MODEM_ANTRR2_Route_Tgl                  = 0x3570,
            MODEM_ANTRR3_Route_Tgl                  = 0x3574,
            MODEM_ANTRR4_Route_Tgl                  = 0x3578,
            MODEM_ANTRR5_Route_Tgl                  = 0x357C,
            MODEM_ANTSWEN_Route_Tgl                 = 0x3580,
            MODEM_ANTSWUS_Route_Tgl                 = 0x3584,
            MODEM_ANTTRIG_Route_Tgl                 = 0x3588,
            MODEM_ANTTRIGSTOP_Route_Tgl             = 0x358C,
            MODEM_DCLK_Route_Tgl                    = 0x3590,
            MODEM_DIN_Route_Tgl                     = 0x3594,
            MODEM_DOUT_Route_Tgl                    = 0x3598,
            MODEM_S0IN_Route_Tgl                    = 0x35A4,
            MODEM_S1IN_Route_Tgl                    = 0x35A8,
            PRS0_RouteEnable_Tgl                    = 0x35B0,
            PRS0_ASYNCH0_Route_Tgl                  = 0x35B4,
            PRS0_ASYNCH1_Route_Tgl                  = 0x35B8,
            PRS0_ASYNCH2_Route_Tgl                  = 0x35BC,
            PRS0_ASYNCH3_Route_Tgl                  = 0x35C0,
            PRS0_ASYNCH4_Route_Tgl                  = 0x35C4,
            PRS0_ASYNCH5_Route_Tgl                  = 0x35C8,
            PRS0_ASYNCH6_Route_Tgl                  = 0x35CC,
            PRS0_ASYNCH7_Route_Tgl                  = 0x35D0,
            PRS0_ASYNCH8_Route_Tgl                  = 0x35D4,
            PRS0_ASYNCH9_Route_Tgl                  = 0x35D8,
            PRS0_ASYNCH10_Route_Tgl                 = 0x35DC,
            PRS0_ASYNCH11_Route_Tgl                 = 0x35E0,
            PRS0_ASYNCH12_Route_Tgl                 = 0x35E4,
            PRS0_ASYNCH13_Route_Tgl                 = 0x35E8,
            PRS0_ASYNCH14_Route_Tgl                 = 0x35EC,
            PRS0_ASYNCH15_Route_Tgl                 = 0x35F0,
            PRS0_SYNCH0_Route_Tgl                   = 0x35F4,
            PRS0_SYNCH1_Route_Tgl                   = 0x35F8,
            PRS0_SYNCH2_Route_Tgl                   = 0x35FC,
            PRS0_SYNCH3_Route_Tgl                   = 0x3600,
            RAC_RouteEnable_Tgl                     = 0x3608,
            RAC_LNAEN_Route_Tgl                     = 0x360C,
            RAC_PAEN_Route_Tgl                      = 0x3610,
            RFECA0_RouteEnable_Tgl                  = 0x3618,
            RFECA0_DATAOUT0_Route_Tgl               = 0x361C,
            RFECA0_DATAOUT1_Route_Tgl               = 0x3620,
            RFECA0_DATAOUT2_Route_Tgl               = 0x3624,
            RFECA0_DATAOUT3_Route_Tgl               = 0x3628,
            RFECA0_DATAOUT4_Route_Tgl               = 0x362C,
            RFECA0_DATAOUT5_Route_Tgl               = 0x3630,
            RFECA0_DATAOUT6_Route_Tgl               = 0x3634,
            RFECA0_DATAOUT7_Route_Tgl               = 0x3638,
            RFECA0_DATAOUT8_Route_Tgl               = 0x363C,
            RFECA0_DATAOUT9_Route_Tgl               = 0x3640,
            RFECA0_DATAOUT10_Route_Tgl              = 0x3644,
            RFECA0_DATAOUT11_Route_Tgl              = 0x3648,
            RFECA0_DATAOUT12_Route_Tgl              = 0x364C,
            RFECA0_DATAOUT13_Route_Tgl              = 0x3650,
            RFECA0_DATAOUT14_Route_Tgl              = 0x3654,
            RFECA0_DATAOUT15_Route_Tgl              = 0x3658,
            RFECA0_DATAOUT16_Route_Tgl              = 0x365C,
            RFECA0_DATAOUT17_Route_Tgl              = 0x3660,
            RFECA0_DATAOUT18_Route_Tgl              = 0x3664,
            RFECA0_DATAVALID_Route_Tgl              = 0x3668,
            RFECA0_TRIGGERIN_Route_Tgl              = 0x366C,
            SYXO0_BUFOUTREQINASYNC_Route_Tgl        = 0x3678,
            TIMER0_RouteEnable_Tgl                  = 0x3680,
            TIMER0_CC0_Route_Tgl                    = 0x3684,
            TIMER0_CC1_Route_Tgl                    = 0x3688,
            TIMER0_CC2_Route_Tgl                    = 0x368C,
            TIMER0_CDTI0_Route_Tgl                  = 0x3690,
            TIMER0_CDTI1_Route_Tgl                  = 0x3694,
            TIMER0_CDTI2_Route_Tgl                  = 0x3698,
            TIMER1_RouteEnable_Tgl                  = 0x36A0,
            TIMER1_CC0_Route_Tgl                    = 0x36A4,
            TIMER1_CC1_Route_Tgl                    = 0x36A8,
            TIMER1_CC2_Route_Tgl                    = 0x36AC,
            TIMER1_CDTI0_Route_Tgl                  = 0x36B0,
            TIMER1_CDTI1_Route_Tgl                  = 0x36B4,
            TIMER1_CDTI2_Route_Tgl                  = 0x36B8,
            TIMER2_RouteEnable_Tgl                  = 0x36C0,
            TIMER2_CC0_Route_Tgl                    = 0x36C4,
            TIMER2_CC1_Route_Tgl                    = 0x36C8,
            TIMER2_CC2_Route_Tgl                    = 0x36CC,
            TIMER2_CDTI0_Route_Tgl                  = 0x36D0,
            TIMER2_CDTI1_Route_Tgl                  = 0x36D4,
            TIMER2_CDTI2_Route_Tgl                  = 0x36D8,
            TIMER3_RouteEnable_Tgl                  = 0x36E0,
            TIMER3_CC0_Route_Tgl                    = 0x36E4,
            TIMER3_CC1_Route_Tgl                    = 0x36E8,
            TIMER3_CC2_Route_Tgl                    = 0x36EC,
            TIMER3_CDTI0_Route_Tgl                  = 0x36F0,
            TIMER3_CDTI1_Route_Tgl                  = 0x36F4,
            TIMER3_CDTI2_Route_Tgl                  = 0x36F8,
            TIMER4_RouteEnable_Tgl                  = 0x3700,
            TIMER4_CC0_Route_Tgl                    = 0x3704,
            TIMER4_CC1_Route_Tgl                    = 0x3708,
            TIMER4_CC2_Route_Tgl                    = 0x370C,
            TIMER4_CDTI0_Route_Tgl                  = 0x3710,
            TIMER4_CDTI1_Route_Tgl                  = 0x3714,
            TIMER4_CDTI2_Route_Tgl                  = 0x3718,
            USART0_RouteEnable_Tgl                  = 0x3720,
            USART0_CS_Route_Tgl                     = 0x3724,
            USART0_CTS_Route_Tgl                    = 0x3728,
            USART0_RTS_Route_Tgl                    = 0x372C,
            USART0_RX_Route_Tgl                     = 0x3730,
            USART0_CLK_Route_Tgl                    = 0x3734,
            USART0_TX_Route_Tgl                     = 0x3738,
            RootAccessTypeDescriptor0_Tgl           = 0x3740,
            RootAccessTypeDescriptor1_Tgl           = 0x3744,
            RootAccessTypeDescriptor6_Tgl           = 0x3758,
            RootAccessTypeDescriptor8_Tgl           = 0x3760,
            RootAccessTypeDescriptor9_Tgl           = 0x3764,
            RootAccessTypeDescriptor10_Tgl          = 0x3768,
            RootAccessTypeDescriptor11_Tgl          = 0x376C,
            RootAccessTypeDescriptor12_Tgl          = 0x3770,
            RootAccessTypeDescriptor13_Tgl          = 0x3774,
            RootAccessTypeDescriptor14_Tgl          = 0x3778,            
        }
#endregion        
    }
}