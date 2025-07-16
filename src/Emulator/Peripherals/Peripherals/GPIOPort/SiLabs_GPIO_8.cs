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
    public class SiLabs_GPIO_8 : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public SiLabs_GPIO_8(Machine machine) : base(machine, NumberOfPins * NumberOfPorts)
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
                {(long)Registers.Extint_Extipsell, new DoubleWordRegister(this)
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
                {(long)Registers.Extint_Extipselh, new DoubleWordRegister(this)
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
                {(long)Registers.Extint_Extipinsell, new DoubleWordRegister(this)
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
                {(long)Registers.Extint_Extipinselh, new DoubleWordRegister(this)
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
                {(long)Registers.Extint_Extirise, new DoubleWordRegister(this)
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
                {(long)Registers.Extint_Extifall, new DoubleWordRegister(this)
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
                {(long)Registers.Extint_If, new DoubleWordRegister(this)
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
                {(long)Registers.Extint_Ien, new DoubleWordRegister(this)
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
                {(long)Registers.Status_Gpiolockstatus, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => configurationLocked, name: "LOCK")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.Eusart0_Routeen, new DoubleWordRegister(this)
                    .WithFlag(0, out EUSART0_RouteEnable_CsPin, name: "CSPEN")
                    .WithFlag(1, out EUSART0_RouteEnable_RtsPin, name: "RTSPEN")
                    .WithFlag(2, out EUSART0_RouteEnable_RxPin, name: "RXPEN")
                    .WithFlag(3, out EUSART0_RouteEnable_SclkPin, name: "SCLKPEN")
                    .WithFlag(4, out EUSART0_RouteEnable_TxPin, name: "TXPEN")
                },
                {(long)Registers.Eusart0_Rxroute, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out EUSART0_RxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out EUSART0_RxRoutePin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.Eusart0_Txroute, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out EUSART0_TxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out EUSART0_TxRoutePin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.Eusart1_Routeen, new DoubleWordRegister(this)
                    .WithFlag(0, out EUSART1_RouteEnable_CsPin, name: "CSPEN")
                    .WithFlag(1, out EUSART1_RouteEnable_RtsPin, name: "RTSPEN")
                    .WithFlag(2, out EUSART1_RouteEnable_RxPin, name: "RXPEN")
                    .WithFlag(3, out EUSART1_RouteEnable_SclkPin, name: "SCLKPEN")
                    .WithFlag(4, out EUSART1_RouteEnable_TxPin, name: "TXPEN")
                },
                {(long)Registers.Eusart1_Rxroute, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out EUSART1_RxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out EUSART1_RxRoutePin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.Eusart1_Txroute, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out EUSART1_TxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out EUSART1_TxRoutePin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.Eusart2_Routeen, new DoubleWordRegister(this)
                    .WithFlag(0, out EUSART2_RouteEnable_CsPin, name: "CSPEN")
                    .WithFlag(1, out EUSART2_RouteEnable_RtsPin, name: "RTSPEN")
                    .WithFlag(2, out EUSART2_RouteEnable_RxPin, name: "RXPEN")
                    .WithFlag(3, out EUSART2_RouteEnable_SclkPin, name: "SCLKPEN")
                    .WithFlag(4, out EUSART2_RouteEnable_TxPin, name: "TXPEN")
                },
                {(long)Registers.Eusart2_Rxroute, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out EUSART2_RxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out EUSART2_RxRoutePin, name: "PIN")
                    .WithReservedBits(20, 12)
                },
                {(long)Registers.Eusart2_Txroute, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Port>(0, 2, out EUSART2_TxRoutePort, name: "PORT")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 4, out EUSART2_TxRoutePin, name: "PIN")
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

            regs.Add((long)Registers.Porta_Ctrl + regOffset, new DoubleWordRegister(this)
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
            regs.Add((long)Registers.Porta_Model + regOffset, new DoubleWordRegister(this)
                     .WithEnumField<DoubleWordRegister, PinMode>(0, 4, out pinMode[pinOffset], name: "MODE0")
                     .WithEnumField<DoubleWordRegister, PinMode>(4, 4, out pinMode[pinOffset + 1], name: "MODE1")
                     .WithEnumField<DoubleWordRegister, PinMode>(8, 4, out pinMode[pinOffset + 2], name: "MODE2")
                     .WithEnumField<DoubleWordRegister, PinMode>(12, 4, out pinMode[pinOffset + 3], name: "MODE3")
                     .WithEnumField<DoubleWordRegister, PinMode>(16, 4, out pinMode[pinOffset + 4], name: "MODE4")
                     .WithEnumField<DoubleWordRegister, PinMode>(20, 4, out pinMode[pinOffset + 5], name: "MODE5")
                     .WithEnumField<DoubleWordRegister, PinMode>(24, 4, out pinMode[pinOffset + 6], name: "MODE6")
                     .WithEnumField<DoubleWordRegister, PinMode>(28, 4, out pinMode[pinOffset + 7], name: "MODE7")
                    );
            regs.Add((long)Registers.Porta_Modeh + regOffset, new DoubleWordRegister(this)
                     .WithEnumField<DoubleWordRegister, PinMode>(0, 4, out pinMode[pinOffset + 8], name: "MODE8")
                     .WithEnumField<DoubleWordRegister, PinMode>(4, 4, out pinMode[pinOffset + 9], name: "MODE9")
                     .WithEnumField<DoubleWordRegister, PinMode>(8, 4, out pinMode[pinOffset + 10], name: "MODE10")
                     .WithEnumField<DoubleWordRegister, PinMode>(12, 4, out pinMode[pinOffset + 11], name: "MODE11")
                     .WithEnumField<DoubleWordRegister, PinMode>(16, 4, out pinMode[pinOffset + 12], name: "MODE12")
                     .WithEnumField<DoubleWordRegister, PinMode>(20, 4, out pinMode[pinOffset + 13], name: "MODE13")
                     .WithEnumField<DoubleWordRegister, PinMode>(24, 4, out pinMode[pinOffset + 14], name: "MODE14")
                     .WithEnumField<DoubleWordRegister, PinMode>(28, 4, out pinMode[pinOffset + 15], name: "MODE15")
                    );
            regs.Add((long)Registers.Porta_Dout + regOffset, new DoubleWordRegister(this)
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
            regs.Add((long)Registers.Porta_Din + regOffset, new DoubleWordRegister(this)
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
        // EUSART2
        private IFlagRegisterField EUSART2_RouteEnable_TxPin;
        private IFlagRegisterField EUSART2_RouteEnable_SclkPin;
        private IFlagRegisterField EUSART2_RouteEnable_RxPin;
        private IFlagRegisterField EUSART2_RouteEnable_RtsPin;
        private IFlagRegisterField EUSART2_RouteEnable_CsPin;
        private IEnumRegisterField<Port> EUSART2_RxRoutePort;
        private IValueRegisterField EUSART2_RxRoutePin;
        private IEnumRegisterField<Port> EUSART2_TxRoutePort;
        private IValueRegisterField EUSART2_TxRoutePin;        
#endregion

        #region methods
        public void InnerReset()
        {
            registersCollection.Reset();
            configurationLocked = false;
            EvenIRQ.Unset();
            OddIRQ.Unset();
            for (var i = 0; i < NumberOfExternalInterrupts; i++)
            {
                interruptTrigger[i] = (uint)InterruptTrigger.None;
                previousState[i] = false;
            }
            for (var i = 0; i < NumberOfPins * NumberOfPorts; i++)
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
                    this.Log(LogLevel.Info, "Route not enabled for internal signal (source={0} signal={1})", signalSource, signalType);
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
                case SignalSource.EUSART2:
                {
                    switch(signalType)
                    {
                        case SignalType.EUSART2_RX:
                        {
                            if (EUSART2_RouteEnable_RxPin.Value)
                            {
                                pinNumber = GetPinNumber(EUSART2_RxRoutePort.Value, (uint)EUSART2_RxRoutePin.Value);
                            }
                            break;
                        }
                        default:
                            this.Log(LogLevel.Error, string.Format("GPIO Signal type {0} for EUSART2 not supported", signalType));
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
            None    = 0,
            EUSART0 = 1,
            EUSART1 = 2,
            EUSART2 = 3,
        }

        private enum SignalType
        {
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

            // If SignalSource is EUSART2
            EUSART2_CTS             = 0,
            EUSART2_RTS             = 1,
            EUSART2_RX              = 2,
            EUSART2_SCLK            = 3,
            EUSART2_TX              = 4,
        }

        protected enum Registers
        {
            Ipversion = 0x0,
            Porta_Ctrl = 0x30,
            Porta_Model = 0x34,
            Porta_Modeh = 0x3C,
            Porta_Dout = 0x40,
            Porta_Din = 0x44,
            Porta_Amuxmode = 0x48,
            Porta_Cascode = 0x4C,
            Portb_Ctrl = 0x60,
            Portb_Model = 0x64,
            Portb_Modeh = 0x6C,
            Portb_Dout = 0x70,
            Portb_Din = 0x74,
            Portb_Amuxmode = 0x78,
            Portb_Cascode = 0x7C,
            Portc_Ctrl = 0x90,
            Portc_Model = 0x94,
            Portc_Modeh = 0x9C,
            Portc_Dout = 0xA0,
            Portc_Din = 0xA4,
            Portc_Amuxmode = 0xA8,
            Portc_Cascode = 0xAC,
            Portd_Ctrl = 0xC0,
            Portd_Model = 0xC4,
            Portd_Modeh = 0xCC,
            Portd_Dout = 0xD0,
            Portd_Din = 0xD4,
            Portd_Amuxmode = 0xD8,
            Portd_Cascode = 0xDC,
            Lock = 0x300,
            Status_Gpiolockstatus = 0x310,
            Abus_Abusalloc = 0x320,
            Abus_Bbusalloc = 0x324,
            Abus_Cdbusalloc = 0x328,
            Abus_Aodd0switch = 0x330,
            Abus_Aodd1switch = 0x334,
            Abus_Aeven0switch = 0x338,
            Abus_Aeven1switch = 0x33C,
            Abus_Bodd0switch = 0x340,
            Abus_Bodd1switch = 0x344,
            Abus_Beven0switch = 0x348,
            Abus_Beven1switch = 0x34C,
            Abus_Cdodd0switch = 0x350,
            Abus_Cdodd1switch = 0x354,
            Abus_Cdeven0switch = 0x358,
            Abus_Cdeven1switch = 0x35C,
            Extint_Extipsell = 0x400,
            Extint_Extipselh = 0x404,
            Extint_Extipinsell = 0x408,
            Extint_Extipinselh = 0x40C,
            Extint_Extirise = 0x410,
            Extint_Extifall = 0x414,
            Extint_If = 0x420,
            Extint_Ien = 0x424,
            Wakeup_Em4wuen = 0x42C,
            Wakeup_Em4wupol = 0x430,
            Route_Dbgroutepen = 0x440,
            Route_Traceroutepen = 0x444,
            Acmp0_Routeen = 0x450,
            Acmp0_Acmpoutroute = 0x454,
            Acmp1_Routeen = 0x45C,
            Acmp1_Acmpoutroute = 0x460,
            Eusart0_Routeen = 0x468,
            Eusart0_Csroute = 0x46C,
            Eusart0_Ctsroute = 0x470,
            Eusart0_Rtsroute = 0x474,
            Eusart0_Rxroute = 0x478,
            Eusart0_Sclkroute = 0x47C,
            Eusart0_Txroute = 0x480,
            Eusart1_Routeen = 0x488,
            Eusart1_Csroute = 0x48C,
            Eusart1_Ctsroute = 0x490,
            Eusart1_Rtsroute = 0x494,
            Eusart1_Rxroute = 0x498,
            Eusart1_Sclkroute = 0x49C,
            Eusart1_Txroute = 0x4A0,
            Eusart2_Routeen = 0x4A8,
            Eusart2_Csroute = 0x4AC,
            Eusart2_Ctsroute = 0x4B0,
            Eusart2_Rtsroute = 0x4B4,
            Eusart2_Rxroute = 0x4B8,
            Eusart2_Sclkroute = 0x4BC,
            Eusart2_Txroute = 0x4C0,
            Frc_Routeen = 0x4C8,
            Frc_Dclkroute = 0x4CC,
            Frc_Dframeroute = 0x4D0,
            Frc_Doutroute = 0x4D4,
            Cmu_Routeen = 0x4DC,
            Cmu_Clkin0route = 0x4E0,
            Cmu_Clkout0route = 0x4E4,
            Cmu_Clkout1route = 0x4E8,
            Cmu_Clkout2route = 0x4EC,
            Cmu_Clkouthiddenroute = 0x4F0,
            I2c0_Routeen = 0x4F8,
            I2c0_Sclroute = 0x4FC,
            I2c0_Sdaroute = 0x500,
            I2c1_Routeen = 0x508,
            I2c1_Sclroute = 0x50C,
            I2c1_Sdaroute = 0x510,
            I2c2_Routeen = 0x518,
            I2c2_Sclroute = 0x51C,
            I2c2_Sdaroute = 0x520,
            Letimer0_Routeen = 0x528,
            Letimer0_Out0route = 0x52C,
            Letimer0_Out1route = 0x530,
            Lfxo_Routeen = 0x538,
            Lfxo_Lfxoclklvrawroute = 0x53C,
            Modem_Routeen = 0x544,
            Modem_Ant0route = 0x548,
            Modem_Ant1route = 0x54C,
            Modem_Antrolloverroute = 0x550,
            Modem_Antrr0route = 0x554,
            Modem_Antrr1route = 0x558,
            Modem_Antrr2route = 0x55C,
            Modem_Antrr3route = 0x560,
            Modem_Antrr4route = 0x564,
            Modem_Antrr5route = 0x568,
            Modem_Antswenroute = 0x56C,
            Modem_Antswusroute = 0x570,
            Modem_Anttrigroute = 0x574,
            Modem_Anttrigstoproute = 0x578,
            Modem_Dclkroute = 0x57C,
            Modem_Dinroute = 0x580,
            Modem_Doutroute = 0x584,
            Pcnt0_S0inroute = 0x590,
            Pcnt0_S1inroute = 0x594,
            Pixelrz0_Routeen = 0x59C,
            Pixelrz0_Rztxoutroute = 0x5A0,
            Pixelrz1_Routeen = 0x5A8,
            Pixelrz1_Rztxoutroute = 0x5AC,
            Prs0_Routeen = 0x5B4,
            Prs0_Asynch0route = 0x5B8,
            Prs0_Asynch1route = 0x5BC,
            Prs0_Asynch2route = 0x5C0,
            Prs0_Asynch3route = 0x5C4,
            Prs0_Asynch4route = 0x5C8,
            Prs0_Asynch5route = 0x5CC,
            Prs0_Asynch6route = 0x5D0,
            Prs0_Asynch7route = 0x5D4,
            Prs0_Asynch8route = 0x5D8,
            Prs0_Asynch9route = 0x5DC,
            Prs0_Asynch10route = 0x5E0,
            Prs0_Asynch11route = 0x5E4,
            Prs0_Synch0route = 0x5E8,
            Prs0_Synch1route = 0x5EC,
            Prs0_Synch2route = 0x5F0,
            Prs0_Synch3route = 0x5F4,
            Rac_Routeen = 0x5FC,
            Rac_Lnaenroute = 0x600,
            Rac_Paenroute = 0x604,
            Rfeca0_Routeen = 0x60C,
            Rfeca0_Dataout0route = 0x610,
            Rfeca0_Dataout1route = 0x614,
            Rfeca0_Dataout2route = 0x618,
            Rfeca0_Dataout3route = 0x61C,
            Rfeca0_Dataout4route = 0x620,
            Rfeca0_Dataout5route = 0x624,
            Rfeca0_Dataout6route = 0x628,
            Rfeca0_Dataout7route = 0x62C,
            Rfeca0_Dataout8route = 0x630,
            Rfeca0_Dataout9route = 0x634,
            Rfeca0_Dataout10route = 0x638,
            Rfeca0_Dataout11route = 0x63C,
            Rfeca0_Dataout12route = 0x640,
            Rfeca0_Dataout13route = 0x644,
            Rfeca0_Dataout14route = 0x648,
            Rfeca0_Dataout15route = 0x64C,
            Rfeca0_Dataout16route = 0x650,
            Rfeca0_Dataout17route = 0x654,
            Rfeca0_Dataout18route = 0x658,
            Rfeca0_Datavalidroute = 0x65C,
            Rfeca0_Triggerinroute = 0x660,
            Timer0_Routeen = 0x668,
            Timer0_Cc0route = 0x66C,
            Timer0_Cc1route = 0x670,
            Timer0_Cc2route = 0x674,
            Timer0_Cdti0route = 0x678,
            Timer0_Cdti1route = 0x67C,
            Timer0_Cdti2route = 0x680,
            Timer1_Routeen = 0x688,
            Timer1_Cc0route = 0x68C,
            Timer1_Cc1route = 0x690,
            Timer1_Cc2route = 0x694,
            Timer1_Cdti0route = 0x698,
            Timer1_Cdti1route = 0x69C,
            Timer1_Cdti2route = 0x6A0,
            Timer2_Routeen = 0x6A8,
            Timer2_Cc0route = 0x6AC,
            Timer2_Cc1route = 0x6B0,
            Timer2_Cc2route = 0x6B4,
            Timer2_Cc3route = 0x6B8,
            Timer2_Cc4route = 0x6BC,
            Timer2_Cc5route = 0x6C0,
            Timer2_Cc6route = 0x6C4,
            Timer2_Cdti0route = 0x6C8,
            Timer2_Cdti1route = 0x6CC,
            Timer2_Cdti2route = 0x6D0,
            Timer2_Ccc3route = 0x6D4,
            Timer2_Ccc4route = 0x6D8,
            Timer2_Ccc5route = 0x6DC,
            Timer2_Ccc6route = 0x6E0,
            Timer3_Routeen = 0x6E8,
            Timer3_Cc0route = 0x6EC,
            Timer3_Cc1route = 0x6F0,
            Timer3_Cc2route = 0x6F4,
            Timer3_Cc3route = 0x6F8,
            Timer3_Cc4route = 0x6FC,
            Timer3_Cc5route = 0x700,
            Timer3_Cc6route = 0x704,
            Timer3_Cdti0route = 0x708,
            Timer3_Cdti1route = 0x70C,
            Timer3_Cdti2route = 0x710,
            Timer3_Ccc3route = 0x714,
            Timer3_Ccc4route = 0x718,
            Timer3_Ccc5route = 0x71C,
            Timer3_Ccc6route = 0x720,
            Drpu_Rpuratd0 = 0x728,
            Drpu_Rpuratd1 = 0x72C,
            Drpu_Rpuratd6 = 0x740,
            Drpu_Rpuratd8 = 0x748,
            Drpu_Rpuratd9 = 0x74C,
            Drpu_Rpuratd10 = 0x750,
            Drpu_Rpuratd11 = 0x754,
            Drpu_Rpuratd12 = 0x758,
            Drpu_Rpuratd13 = 0x75C,
            Drpu_Rpuratd14 = 0x760,
            
            Ipversion_SET = 0x1000,
            Porta_Ctrl_SET = 0x1030,
            Porta_Model_SET = 0x1034,
            Porta_Modeh_SET = 0x103C,
            Porta_Dout_SET = 0x1040,
            Porta_Din_SET = 0x1044,
            Porta_Amuxmode_SET = 0x1048,
            Porta_Cascode_SET = 0x104C,
            Portb_Ctrl_SET = 0x1060,
            Portb_Model_SET = 0x1064,
            Portb_Modeh_SET = 0x106C,
            Portb_Dout_SET = 0x1070,
            Portb_Din_SET = 0x1074,
            Portb_Amuxmode_SET = 0x1078,
            Portb_Cascode_SET = 0x107C,
            Portc_Ctrl_SET = 0x1090,
            Portc_Model_SET = 0x1094,
            Portc_Modeh_SET = 0x109C,
            Portc_Dout_SET = 0x10A0,
            Portc_Din_SET = 0x10A4,
            Portc_Amuxmode_SET = 0x10A8,
            Portc_Cascode_SET = 0x10AC,
            Portd_Ctrl_SET = 0x10C0,
            Portd_Model_SET = 0x10C4,
            Portd_Modeh_SET = 0x10CC,
            Portd_Dout_SET = 0x10D0,
            Portd_Din_SET = 0x10D4,
            Portd_Amuxmode_SET = 0x10D8,
            Portd_Cascode_SET = 0x10DC,
            Lock_SET = 0x1300,
            Status_Gpiolockstatus_SET = 0x1310,
            Abus_Abusalloc_SET = 0x1320,
            Abus_Bbusalloc_SET = 0x1324,
            Abus_Cdbusalloc_SET = 0x1328,
            Abus_Aodd0switch_SET = 0x1330,
            Abus_Aodd1switch_SET = 0x1334,
            Abus_Aeven0switch_SET = 0x1338,
            Abus_Aeven1switch_SET = 0x133C,
            Abus_Bodd0switch_SET = 0x1340,
            Abus_Bodd1switch_SET = 0x1344,
            Abus_Beven0switch_SET = 0x1348,
            Abus_Beven1switch_SET = 0x134C,
            Abus_Cdodd0switch_SET = 0x1350,
            Abus_Cdodd1switch_SET = 0x1354,
            Abus_Cdeven0switch_SET = 0x1358,
            Abus_Cdeven1switch_SET = 0x135C,
            Extint_Extipsell_SET = 0x1400,
            Extint_Extipselh_SET = 0x1404,
            Extint_Extipinsell_SET = 0x1408,
            Extint_Extipinselh_SET = 0x140C,
            Extint_Extirise_SET = 0x1410,
            Extint_Extifall_SET = 0x1414,
            Extint_If_SET = 0x1420,
            Extint_Ien_SET = 0x1424,
            Wakeup_Em4wuen_SET = 0x142C,
            Wakeup_Em4wupol_SET = 0x1430,
            Route_Dbgroutepen_SET = 0x1440,
            Route_Traceroutepen_SET = 0x1444,
            Acmp0_Routeen_SET = 0x1450,
            Acmp0_Acmpoutroute_SET = 0x1454,
            Acmp1_Routeen_SET = 0x145C,
            Acmp1_Acmpoutroute_SET = 0x1460,
            Eusart0_Routeen_SET = 0x1468,
            Eusart0_Csroute_SET = 0x146C,
            Eusart0_Ctsroute_SET = 0x1470,
            Eusart0_Rtsroute_SET = 0x1474,
            Eusart0_Rxroute_SET = 0x1478,
            Eusart0_Sclkroute_SET = 0x147C,
            Eusart0_Txroute_SET = 0x1480,
            Eusart1_Routeen_SET = 0x1488,
            Eusart1_Csroute_SET = 0x148C,
            Eusart1_Ctsroute_SET = 0x1490,
            Eusart1_Rtsroute_SET = 0x1494,
            Eusart1_Rxroute_SET = 0x1498,
            Eusart1_Sclkroute_SET = 0x149C,
            Eusart1_Txroute_SET = 0x14A0,
            Eusart2_Routeen_SET = 0x14A8,
            Eusart2_Csroute_SET = 0x14AC,
            Eusart2_Ctsroute_SET = 0x14B0,
            Eusart2_Rtsroute_SET = 0x14B4,
            Eusart2_Rxroute_SET = 0x14B8,
            Eusart2_Sclkroute_SET = 0x14BC,
            Eusart2_Txroute_SET = 0x14C0,
            Frc_Routeen_SET = 0x14C8,
            Frc_Dclkroute_SET = 0x14CC,
            Frc_Dframeroute_SET = 0x14D0,
            Frc_Doutroute_SET = 0x14D4,
            Cmu_Routeen_SET = 0x14DC,
            Cmu_Clkin0route_SET = 0x14E0,
            Cmu_Clkout0route_SET = 0x14E4,
            Cmu_Clkout1route_SET = 0x14E8,
            Cmu_Clkout2route_SET = 0x14EC,
            Cmu_Clkouthiddenroute_SET = 0x14F0,
            I2c0_Routeen_SET = 0x14F8,
            I2c0_Sclroute_SET = 0x14FC,
            I2c0_Sdaroute_SET = 0x1500,
            I2c1_Routeen_SET = 0x1508,
            I2c1_Sclroute_SET = 0x150C,
            I2c1_Sdaroute_SET = 0x1510,
            I2c2_Routeen_SET = 0x1518,
            I2c2_Sclroute_SET = 0x151C,
            I2c2_Sdaroute_SET = 0x1520,
            Letimer0_Routeen_SET = 0x1528,
            Letimer0_Out0route_SET = 0x152C,
            Letimer0_Out1route_SET = 0x1530,
            Lfxo_Routeen_SET = 0x1538,
            Lfxo_Lfxoclklvrawroute_SET = 0x153C,
            Modem_Routeen_SET = 0x1544,
            Modem_Ant0route_SET = 0x1548,
            Modem_Ant1route_SET = 0x154C,
            Modem_Antrolloverroute_SET = 0x1550,
            Modem_Antrr0route_SET = 0x1554,
            Modem_Antrr1route_SET = 0x1558,
            Modem_Antrr2route_SET = 0x155C,
            Modem_Antrr3route_SET = 0x1560,
            Modem_Antrr4route_SET = 0x1564,
            Modem_Antrr5route_SET = 0x1568,
            Modem_Antswenroute_SET = 0x156C,
            Modem_Antswusroute_SET = 0x1570,
            Modem_Anttrigroute_SET = 0x1574,
            Modem_Anttrigstoproute_SET = 0x1578,
            Modem_Dclkroute_SET = 0x157C,
            Modem_Dinroute_SET = 0x1580,
            Modem_Doutroute_SET = 0x1584,
            Pcnt0_S0inroute_SET = 0x1590,
            Pcnt0_S1inroute_SET = 0x1594,
            Pixelrz0_Routeen_SET = 0x159C,
            Pixelrz0_Rztxoutroute_SET = 0x15A0,
            Pixelrz1_Routeen_SET = 0x15A8,
            Pixelrz1_Rztxoutroute_SET = 0x15AC,
            Prs0_Routeen_SET = 0x15B4,
            Prs0_Asynch0route_SET = 0x15B8,
            Prs0_Asynch1route_SET = 0x15BC,
            Prs0_Asynch2route_SET = 0x15C0,
            Prs0_Asynch3route_SET = 0x15C4,
            Prs0_Asynch4route_SET = 0x15C8,
            Prs0_Asynch5route_SET = 0x15CC,
            Prs0_Asynch6route_SET = 0x15D0,
            Prs0_Asynch7route_SET = 0x15D4,
            Prs0_Asynch8route_SET = 0x15D8,
            Prs0_Asynch9route_SET = 0x15DC,
            Prs0_Asynch10route_SET = 0x15E0,
            Prs0_Asynch11route_SET = 0x15E4,
            Prs0_Synch0route_SET = 0x15E8,
            Prs0_Synch1route_SET = 0x15EC,
            Prs0_Synch2route_SET = 0x15F0,
            Prs0_Synch3route_SET = 0x15F4,
            Rac_Routeen_SET = 0x15FC,
            Rac_Lnaenroute_SET = 0x1600,
            Rac_Paenroute_SET = 0x1604,
            Rfeca0_Routeen_SET = 0x160C,
            Rfeca0_Dataout0route_SET = 0x1610,
            Rfeca0_Dataout1route_SET = 0x1614,
            Rfeca0_Dataout2route_SET = 0x1618,
            Rfeca0_Dataout3route_SET = 0x161C,
            Rfeca0_Dataout4route_SET = 0x1620,
            Rfeca0_Dataout5route_SET = 0x1624,
            Rfeca0_Dataout6route_SET = 0x1628,
            Rfeca0_Dataout7route_SET = 0x162C,
            Rfeca0_Dataout8route_SET = 0x1630,
            Rfeca0_Dataout9route_SET = 0x1634,
            Rfeca0_Dataout10route_SET = 0x1638,
            Rfeca0_Dataout11route_SET = 0x163C,
            Rfeca0_Dataout12route_SET = 0x1640,
            Rfeca0_Dataout13route_SET = 0x1644,
            Rfeca0_Dataout14route_SET = 0x1648,
            Rfeca0_Dataout15route_SET = 0x164C,
            Rfeca0_Dataout16route_SET = 0x1650,
            Rfeca0_Dataout17route_SET = 0x1654,
            Rfeca0_Dataout18route_SET = 0x1658,
            Rfeca0_Datavalidroute_SET = 0x165C,
            Rfeca0_Triggerinroute_SET = 0x1660,
            Timer0_Routeen_SET = 0x1668,
            Timer0_Cc0route_SET = 0x166C,
            Timer0_Cc1route_SET = 0x1670,
            Timer0_Cc2route_SET = 0x1674,
            Timer0_Cdti0route_SET = 0x1678,
            Timer0_Cdti1route_SET = 0x167C,
            Timer0_Cdti2route_SET = 0x1680,
            Timer1_Routeen_SET = 0x1688,
            Timer1_Cc0route_SET = 0x168C,
            Timer1_Cc1route_SET = 0x1690,
            Timer1_Cc2route_SET = 0x1694,
            Timer1_Cdti0route_SET = 0x1698,
            Timer1_Cdti1route_SET = 0x169C,
            Timer1_Cdti2route_SET = 0x16A0,
            Timer2_Routeen_SET = 0x16A8,
            Timer2_Cc0route_SET = 0x16AC,
            Timer2_Cc1route_SET = 0x16B0,
            Timer2_Cc2route_SET = 0x16B4,
            Timer2_Cc3route_SET = 0x16B8,
            Timer2_Cc4route_SET = 0x16BC,
            Timer2_Cc5route_SET = 0x16C0,
            Timer2_Cc6route_SET = 0x16C4,
            Timer2_Cdti0route_SET = 0x16C8,
            Timer2_Cdti1route_SET = 0x16CC,
            Timer2_Cdti2route_SET = 0x16D0,
            Timer2_Ccc3route_SET = 0x16D4,
            Timer2_Ccc4route_SET = 0x16D8,
            Timer2_Ccc5route_SET = 0x16DC,
            Timer2_Ccc6route_SET = 0x16E0,
            Timer3_Routeen_SET = 0x16E8,
            Timer3_Cc0route_SET = 0x16EC,
            Timer3_Cc1route_SET = 0x16F0,
            Timer3_Cc2route_SET = 0x16F4,
            Timer3_Cc3route_SET = 0x16F8,
            Timer3_Cc4route_SET = 0x16FC,
            Timer3_Cc5route_SET = 0x1700,
            Timer3_Cc6route_SET = 0x1704,
            Timer3_Cdti0route_SET = 0x1708,
            Timer3_Cdti1route_SET = 0x170C,
            Timer3_Cdti2route_SET = 0x1710,
            Timer3_Ccc3route_SET = 0x1714,
            Timer3_Ccc4route_SET = 0x1718,
            Timer3_Ccc5route_SET = 0x171C,
            Timer3_Ccc6route_SET = 0x1720,
            Drpu_Rpuratd0_SET = 0x1728,
            Drpu_Rpuratd1_SET = 0x172C,
            Drpu_Rpuratd6_SET = 0x1740,
            Drpu_Rpuratd8_SET = 0x1748,
            Drpu_Rpuratd9_SET = 0x174C,
            Drpu_Rpuratd10_SET = 0x1750,
            Drpu_Rpuratd11_SET = 0x1754,
            Drpu_Rpuratd12_SET = 0x1758,
            Drpu_Rpuratd13_SET = 0x175C,
            Drpu_Rpuratd14_SET = 0x1760,
            
            Ipversion_CLR = 0x2000,
            Porta_Ctrl_CLR = 0x2030,
            Porta_Model_CLR = 0x2034,
            Porta_Modeh_CLR = 0x203C,
            Porta_Dout_CLR = 0x2040,
            Porta_Din_CLR = 0x2044,
            Porta_Amuxmode_CLR = 0x2048,
            Porta_Cascode_CLR = 0x204C,
            Portb_Ctrl_CLR = 0x2060,
            Portb_Model_CLR = 0x2064,
            Portb_Modeh_CLR = 0x206C,
            Portb_Dout_CLR = 0x2070,
            Portb_Din_CLR = 0x2074,
            Portb_Amuxmode_CLR = 0x2078,
            Portb_Cascode_CLR = 0x207C,
            Portc_Ctrl_CLR = 0x2090,
            Portc_Model_CLR = 0x2094,
            Portc_Modeh_CLR = 0x209C,
            Portc_Dout_CLR = 0x20A0,
            Portc_Din_CLR = 0x20A4,
            Portc_Amuxmode_CLR = 0x20A8,
            Portc_Cascode_CLR = 0x20AC,
            Portd_Ctrl_CLR = 0x20C0,
            Portd_Model_CLR = 0x20C4,
            Portd_Modeh_CLR = 0x20CC,
            Portd_Dout_CLR = 0x20D0,
            Portd_Din_CLR = 0x20D4,
            Portd_Amuxmode_CLR = 0x20D8,
            Portd_Cascode_CLR = 0x20DC,
            Lock_CLR = 0x2300,
            Status_Gpiolockstatus_CLR = 0x2310,
            Abus_Abusalloc_CLR = 0x2320,
            Abus_Bbusalloc_CLR = 0x2324,
            Abus_Cdbusalloc_CLR = 0x2328,
            Abus_Aodd0switch_CLR = 0x2330,
            Abus_Aodd1switch_CLR = 0x2334,
            Abus_Aeven0switch_CLR = 0x2338,
            Abus_Aeven1switch_CLR = 0x233C,
            Abus_Bodd0switch_CLR = 0x2340,
            Abus_Bodd1switch_CLR = 0x2344,
            Abus_Beven0switch_CLR = 0x2348,
            Abus_Beven1switch_CLR = 0x234C,
            Abus_Cdodd0switch_CLR = 0x2350,
            Abus_Cdodd1switch_CLR = 0x2354,
            Abus_Cdeven0switch_CLR = 0x2358,
            Abus_Cdeven1switch_CLR = 0x235C,
            Extint_Extipsell_CLR = 0x2400,
            Extint_Extipselh_CLR = 0x2404,
            Extint_Extipinsell_CLR = 0x2408,
            Extint_Extipinselh_CLR = 0x240C,
            Extint_Extirise_CLR = 0x2410,
            Extint_Extifall_CLR = 0x2414,
            Extint_If_CLR = 0x2420,
            Extint_Ien_CLR = 0x2424,
            Wakeup_Em4wuen_CLR = 0x242C,
            Wakeup_Em4wupol_CLR = 0x2430,
            Route_Dbgroutepen_CLR = 0x2440,
            Route_Traceroutepen_CLR = 0x2444,
            Acmp0_Routeen_CLR = 0x2450,
            Acmp0_Acmpoutroute_CLR = 0x2454,
            Acmp1_Routeen_CLR = 0x245C,
            Acmp1_Acmpoutroute_CLR = 0x2460,
            Eusart0_Routeen_CLR = 0x2468,
            Eusart0_Csroute_CLR = 0x246C,
            Eusart0_Ctsroute_CLR = 0x2470,
            Eusart0_Rtsroute_CLR = 0x2474,
            Eusart0_Rxroute_CLR = 0x2478,
            Eusart0_Sclkroute_CLR = 0x247C,
            Eusart0_Txroute_CLR = 0x2480,
            Eusart1_Routeen_CLR = 0x2488,
            Eusart1_Csroute_CLR = 0x248C,
            Eusart1_Ctsroute_CLR = 0x2490,
            Eusart1_Rtsroute_CLR = 0x2494,
            Eusart1_Rxroute_CLR = 0x2498,
            Eusart1_Sclkroute_CLR = 0x249C,
            Eusart1_Txroute_CLR = 0x24A0,
            Eusart2_Routeen_CLR = 0x24A8,
            Eusart2_Csroute_CLR = 0x24AC,
            Eusart2_Ctsroute_CLR = 0x24B0,
            Eusart2_Rtsroute_CLR = 0x24B4,
            Eusart2_Rxroute_CLR = 0x24B8,
            Eusart2_Sclkroute_CLR = 0x24BC,
            Eusart2_Txroute_CLR = 0x24C0,
            Frc_Routeen_CLR = 0x24C8,
            Frc_Dclkroute_CLR = 0x24CC,
            Frc_Dframeroute_CLR = 0x24D0,
            Frc_Doutroute_CLR = 0x24D4,
            Cmu_Routeen_CLR = 0x24DC,
            Cmu_Clkin0route_CLR = 0x24E0,
            Cmu_Clkout0route_CLR = 0x24E4,
            Cmu_Clkout1route_CLR = 0x24E8,
            Cmu_Clkout2route_CLR = 0x24EC,
            Cmu_Clkouthiddenroute_CLR = 0x24F0,
            I2c0_Routeen_CLR = 0x24F8,
            I2c0_Sclroute_CLR = 0x24FC,
            I2c0_Sdaroute_CLR = 0x2500,
            I2c1_Routeen_CLR = 0x2508,
            I2c1_Sclroute_CLR = 0x250C,
            I2c1_Sdaroute_CLR = 0x2510,
            I2c2_Routeen_CLR = 0x2518,
            I2c2_Sclroute_CLR = 0x251C,
            I2c2_Sdaroute_CLR = 0x2520,
            Letimer0_Routeen_CLR = 0x2528,
            Letimer0_Out0route_CLR = 0x252C,
            Letimer0_Out1route_CLR = 0x2530,
            Lfxo_Routeen_CLR = 0x2538,
            Lfxo_Lfxoclklvrawroute_CLR = 0x253C,
            Modem_Routeen_CLR = 0x2544,
            Modem_Ant0route_CLR = 0x2548,
            Modem_Ant1route_CLR = 0x254C,
            Modem_Antrolloverroute_CLR = 0x2550,
            Modem_Antrr0route_CLR = 0x2554,
            Modem_Antrr1route_CLR = 0x2558,
            Modem_Antrr2route_CLR = 0x255C,
            Modem_Antrr3route_CLR = 0x2560,
            Modem_Antrr4route_CLR = 0x2564,
            Modem_Antrr5route_CLR = 0x2568,
            Modem_Antswenroute_CLR = 0x256C,
            Modem_Antswusroute_CLR = 0x2570,
            Modem_Anttrigroute_CLR = 0x2574,
            Modem_Anttrigstoproute_CLR = 0x2578,
            Modem_Dclkroute_CLR = 0x257C,
            Modem_Dinroute_CLR = 0x2580,
            Modem_Doutroute_CLR = 0x2584,
            Pcnt0_S0inroute_CLR = 0x2590,
            Pcnt0_S1inroute_CLR = 0x2594,
            Pixelrz0_Routeen_CLR = 0x259C,
            Pixelrz0_Rztxoutroute_CLR = 0x25A0,
            Pixelrz1_Routeen_CLR = 0x25A8,
            Pixelrz1_Rztxoutroute_CLR = 0x25AC,
            Prs0_Routeen_CLR = 0x25B4,
            Prs0_Asynch0route_CLR = 0x25B8,
            Prs0_Asynch1route_CLR = 0x25BC,
            Prs0_Asynch2route_CLR = 0x25C0,
            Prs0_Asynch3route_CLR = 0x25C4,
            Prs0_Asynch4route_CLR = 0x25C8,
            Prs0_Asynch5route_CLR = 0x25CC,
            Prs0_Asynch6route_CLR = 0x25D0,
            Prs0_Asynch7route_CLR = 0x25D4,
            Prs0_Asynch8route_CLR = 0x25D8,
            Prs0_Asynch9route_CLR = 0x25DC,
            Prs0_Asynch10route_CLR = 0x25E0,
            Prs0_Asynch11route_CLR = 0x25E4,
            Prs0_Synch0route_CLR = 0x25E8,
            Prs0_Synch1route_CLR = 0x25EC,
            Prs0_Synch2route_CLR = 0x25F0,
            Prs0_Synch3route_CLR = 0x25F4,
            Rac_Routeen_CLR = 0x25FC,
            Rac_Lnaenroute_CLR = 0x2600,
            Rac_Paenroute_CLR = 0x2604,
            Rfeca0_Routeen_CLR = 0x260C,
            Rfeca0_Dataout0route_CLR = 0x2610,
            Rfeca0_Dataout1route_CLR = 0x2614,
            Rfeca0_Dataout2route_CLR = 0x2618,
            Rfeca0_Dataout3route_CLR = 0x261C,
            Rfeca0_Dataout4route_CLR = 0x2620,
            Rfeca0_Dataout5route_CLR = 0x2624,
            Rfeca0_Dataout6route_CLR = 0x2628,
            Rfeca0_Dataout7route_CLR = 0x262C,
            Rfeca0_Dataout8route_CLR = 0x2630,
            Rfeca0_Dataout9route_CLR = 0x2634,
            Rfeca0_Dataout10route_CLR = 0x2638,
            Rfeca0_Dataout11route_CLR = 0x263C,
            Rfeca0_Dataout12route_CLR = 0x2640,
            Rfeca0_Dataout13route_CLR = 0x2644,
            Rfeca0_Dataout14route_CLR = 0x2648,
            Rfeca0_Dataout15route_CLR = 0x264C,
            Rfeca0_Dataout16route_CLR = 0x2650,
            Rfeca0_Dataout17route_CLR = 0x2654,
            Rfeca0_Dataout18route_CLR = 0x2658,
            Rfeca0_Datavalidroute_CLR = 0x265C,
            Rfeca0_Triggerinroute_CLR = 0x2660,
            Timer0_Routeen_CLR = 0x2668,
            Timer0_Cc0route_CLR = 0x266C,
            Timer0_Cc1route_CLR = 0x2670,
            Timer0_Cc2route_CLR = 0x2674,
            Timer0_Cdti0route_CLR = 0x2678,
            Timer0_Cdti1route_CLR = 0x267C,
            Timer0_Cdti2route_CLR = 0x2680,
            Timer1_Routeen_CLR = 0x2688,
            Timer1_Cc0route_CLR = 0x268C,
            Timer1_Cc1route_CLR = 0x2690,
            Timer1_Cc2route_CLR = 0x2694,
            Timer1_Cdti0route_CLR = 0x2698,
            Timer1_Cdti1route_CLR = 0x269C,
            Timer1_Cdti2route_CLR = 0x26A0,
            Timer2_Routeen_CLR = 0x26A8,
            Timer2_Cc0route_CLR = 0x26AC,
            Timer2_Cc1route_CLR = 0x26B0,
            Timer2_Cc2route_CLR = 0x26B4,
            Timer2_Cc3route_CLR = 0x26B8,
            Timer2_Cc4route_CLR = 0x26BC,
            Timer2_Cc5route_CLR = 0x26C0,
            Timer2_Cc6route_CLR = 0x26C4,
            Timer2_Cdti0route_CLR = 0x26C8,
            Timer2_Cdti1route_CLR = 0x26CC,
            Timer2_Cdti2route_CLR = 0x26D0,
            Timer2_Ccc3route_CLR = 0x26D4,
            Timer2_Ccc4route_CLR = 0x26D8,
            Timer2_Ccc5route_CLR = 0x26DC,
            Timer2_Ccc6route_CLR = 0x26E0,
            Timer3_Routeen_CLR = 0x26E8,
            Timer3_Cc0route_CLR = 0x26EC,
            Timer3_Cc1route_CLR = 0x26F0,
            Timer3_Cc2route_CLR = 0x26F4,
            Timer3_Cc3route_CLR = 0x26F8,
            Timer3_Cc4route_CLR = 0x26FC,
            Timer3_Cc5route_CLR = 0x2700,
            Timer3_Cc6route_CLR = 0x2704,
            Timer3_Cdti0route_CLR = 0x2708,
            Timer3_Cdti1route_CLR = 0x270C,
            Timer3_Cdti2route_CLR = 0x2710,
            Timer3_Ccc3route_CLR = 0x2714,
            Timer3_Ccc4route_CLR = 0x2718,
            Timer3_Ccc5route_CLR = 0x271C,
            Timer3_Ccc6route_CLR = 0x2720,
            Drpu_Rpuratd0_CLR = 0x2728,
            Drpu_Rpuratd1_CLR = 0x272C,
            Drpu_Rpuratd6_CLR = 0x2740,
            Drpu_Rpuratd8_CLR = 0x2748,
            Drpu_Rpuratd9_CLR = 0x274C,
            Drpu_Rpuratd10_CLR = 0x2750,
            Drpu_Rpuratd11_CLR = 0x2754,
            Drpu_Rpuratd12_CLR = 0x2758,
            Drpu_Rpuratd13_CLR = 0x275C,
            Drpu_Rpuratd14_CLR = 0x2760,
            
            Ipversion_TGL = 0x3000,
            Porta_Ctrl_TGL = 0x3030,
            Porta_Model_TGL = 0x3034,
            Porta_Modeh_TGL = 0x303C,
            Porta_Dout_TGL = 0x3040,
            Porta_Din_TGL = 0x3044,
            Porta_Amuxmode_TGL = 0x3048,
            Porta_Cascode_TGL = 0x304C,
            Portb_Ctrl_TGL = 0x3060,
            Portb_Model_TGL = 0x3064,
            Portb_Modeh_TGL = 0x306C,
            Portb_Dout_TGL = 0x3070,
            Portb_Din_TGL = 0x3074,
            Portb_Amuxmode_TGL = 0x3078,
            Portb_Cascode_TGL = 0x307C,
            Portc_Ctrl_TGL = 0x3090,
            Portc_Model_TGL = 0x3094,
            Portc_Modeh_TGL = 0x309C,
            Portc_Dout_TGL = 0x30A0,
            Portc_Din_TGL = 0x30A4,
            Portc_Amuxmode_TGL = 0x30A8,
            Portc_Cascode_TGL = 0x30AC,
            Portd_Ctrl_TGL = 0x30C0,
            Portd_Model_TGL = 0x30C4,
            Portd_Modeh_TGL = 0x30CC,
            Portd_Dout_TGL = 0x30D0,
            Portd_Din_TGL = 0x30D4,
            Portd_Amuxmode_TGL = 0x30D8,
            Portd_Cascode_TGL = 0x30DC,
            Lock_TGL = 0x3300,
            Status_Gpiolockstatus_TGL = 0x3310,
            Abus_Abusalloc_TGL = 0x3320,
            Abus_Bbusalloc_TGL = 0x3324,
            Abus_Cdbusalloc_TGL = 0x3328,
            Abus_Aodd0switch_TGL = 0x3330,
            Abus_Aodd1switch_TGL = 0x3334,
            Abus_Aeven0switch_TGL = 0x3338,
            Abus_Aeven1switch_TGL = 0x333C,
            Abus_Bodd0switch_TGL = 0x3340,
            Abus_Bodd1switch_TGL = 0x3344,
            Abus_Beven0switch_TGL = 0x3348,
            Abus_Beven1switch_TGL = 0x334C,
            Abus_Cdodd0switch_TGL = 0x3350,
            Abus_Cdodd1switch_TGL = 0x3354,
            Abus_Cdeven0switch_TGL = 0x3358,
            Abus_Cdeven1switch_TGL = 0x335C,
            Extint_Extipsell_TGL = 0x3400,
            Extint_Extipselh_TGL = 0x3404,
            Extint_Extipinsell_TGL = 0x3408,
            Extint_Extipinselh_TGL = 0x340C,
            Extint_Extirise_TGL = 0x3410,
            Extint_Extifall_TGL = 0x3414,
            Extint_If_TGL = 0x3420,
            Extint_Ien_TGL = 0x3424,
            Wakeup_Em4wuen_TGL = 0x342C,
            Wakeup_Em4wupol_TGL = 0x3430,
            Route_Dbgroutepen_TGL = 0x3440,
            Route_Traceroutepen_TGL = 0x3444,
            Acmp0_Routeen_TGL = 0x3450,
            Acmp0_Acmpoutroute_TGL = 0x3454,
            Acmp1_Routeen_TGL = 0x345C,
            Acmp1_Acmpoutroute_TGL = 0x3460,
            Eusart0_Routeen_TGL = 0x3468,
            Eusart0_Csroute_TGL = 0x346C,
            Eusart0_Ctsroute_TGL = 0x3470,
            Eusart0_Rtsroute_TGL = 0x3474,
            Eusart0_Rxroute_TGL = 0x3478,
            Eusart0_Sclkroute_TGL = 0x347C,
            Eusart0_Txroute_TGL = 0x3480,
            Eusart1_Routeen_TGL = 0x3488,
            Eusart1_Csroute_TGL = 0x348C,
            Eusart1_Ctsroute_TGL = 0x3490,
            Eusart1_Rtsroute_TGL = 0x3494,
            Eusart1_Rxroute_TGL = 0x3498,
            Eusart1_Sclkroute_TGL = 0x349C,
            Eusart1_Txroute_TGL = 0x34A0,
            Eusart2_Routeen_TGL = 0x34A8,
            Eusart2_Csroute_TGL = 0x34AC,
            Eusart2_Ctsroute_TGL = 0x34B0,
            Eusart2_Rtsroute_TGL = 0x34B4,
            Eusart2_Rxroute_TGL = 0x34B8,
            Eusart2_Sclkroute_TGL = 0x34BC,
            Eusart2_Txroute_TGL = 0x34C0,
            Frc_Routeen_TGL = 0x34C8,
            Frc_Dclkroute_TGL = 0x34CC,
            Frc_Dframeroute_TGL = 0x34D0,
            Frc_Doutroute_TGL = 0x34D4,
            Cmu_Routeen_TGL = 0x34DC,
            Cmu_Clkin0route_TGL = 0x34E0,
            Cmu_Clkout0route_TGL = 0x34E4,
            Cmu_Clkout1route_TGL = 0x34E8,
            Cmu_Clkout2route_TGL = 0x34EC,
            Cmu_Clkouthiddenroute_TGL = 0x34F0,
            I2c0_Routeen_TGL = 0x34F8,
            I2c0_Sclroute_TGL = 0x34FC,
            I2c0_Sdaroute_TGL = 0x3500,
            I2c1_Routeen_TGL = 0x3508,
            I2c1_Sclroute_TGL = 0x350C,
            I2c1_Sdaroute_TGL = 0x3510,
            I2c2_Routeen_TGL = 0x3518,
            I2c2_Sclroute_TGL = 0x351C,
            I2c2_Sdaroute_TGL = 0x3520,
            Letimer0_Routeen_TGL = 0x3528,
            Letimer0_Out0route_TGL = 0x352C,
            Letimer0_Out1route_TGL = 0x3530,
            Lfxo_Routeen_TGL = 0x3538,
            Lfxo_Lfxoclklvrawroute_TGL = 0x353C,
            Modem_Routeen_TGL = 0x3544,
            Modem_Ant0route_TGL = 0x3548,
            Modem_Ant1route_TGL = 0x354C,
            Modem_Antrolloverroute_TGL = 0x3550,
            Modem_Antrr0route_TGL = 0x3554,
            Modem_Antrr1route_TGL = 0x3558,
            Modem_Antrr2route_TGL = 0x355C,
            Modem_Antrr3route_TGL = 0x3560,
            Modem_Antrr4route_TGL = 0x3564,
            Modem_Antrr5route_TGL = 0x3568,
            Modem_Antswenroute_TGL = 0x356C,
            Modem_Antswusroute_TGL = 0x3570,
            Modem_Anttrigroute_TGL = 0x3574,
            Modem_Anttrigstoproute_TGL = 0x3578,
            Modem_Dclkroute_TGL = 0x357C,
            Modem_Dinroute_TGL = 0x3580,
            Modem_Doutroute_TGL = 0x3584,
            Pcnt0_S0inroute_TGL = 0x3590,
            Pcnt0_S1inroute_TGL = 0x3594,
            Pixelrz0_Routeen_TGL = 0x359C,
            Pixelrz0_Rztxoutroute_TGL = 0x35A0,
            Pixelrz1_Routeen_TGL = 0x35A8,
            Pixelrz1_Rztxoutroute_TGL = 0x35AC,
            Prs0_Routeen_TGL = 0x35B4,
            Prs0_Asynch0route_TGL = 0x35B8,
            Prs0_Asynch1route_TGL = 0x35BC,
            Prs0_Asynch2route_TGL = 0x35C0,
            Prs0_Asynch3route_TGL = 0x35C4,
            Prs0_Asynch4route_TGL = 0x35C8,
            Prs0_Asynch5route_TGL = 0x35CC,
            Prs0_Asynch6route_TGL = 0x35D0,
            Prs0_Asynch7route_TGL = 0x35D4,
            Prs0_Asynch8route_TGL = 0x35D8,
            Prs0_Asynch9route_TGL = 0x35DC,
            Prs0_Asynch10route_TGL = 0x35E0,
            Prs0_Asynch11route_TGL = 0x35E4,
            Prs0_Synch0route_TGL = 0x35E8,
            Prs0_Synch1route_TGL = 0x35EC,
            Prs0_Synch2route_TGL = 0x35F0,
            Prs0_Synch3route_TGL = 0x35F4,
            Rac_Routeen_TGL = 0x35FC,
            Rac_Lnaenroute_TGL = 0x3600,
            Rac_Paenroute_TGL = 0x3604,
            Rfeca0_Routeen_TGL = 0x360C,
            Rfeca0_Dataout0route_TGL = 0x3610,
            Rfeca0_Dataout1route_TGL = 0x3614,
            Rfeca0_Dataout2route_TGL = 0x3618,
            Rfeca0_Dataout3route_TGL = 0x361C,
            Rfeca0_Dataout4route_TGL = 0x3620,
            Rfeca0_Dataout5route_TGL = 0x3624,
            Rfeca0_Dataout6route_TGL = 0x3628,
            Rfeca0_Dataout7route_TGL = 0x362C,
            Rfeca0_Dataout8route_TGL = 0x3630,
            Rfeca0_Dataout9route_TGL = 0x3634,
            Rfeca0_Dataout10route_TGL = 0x3638,
            Rfeca0_Dataout11route_TGL = 0x363C,
            Rfeca0_Dataout12route_TGL = 0x3640,
            Rfeca0_Dataout13route_TGL = 0x3644,
            Rfeca0_Dataout14route_TGL = 0x3648,
            Rfeca0_Dataout15route_TGL = 0x364C,
            Rfeca0_Dataout16route_TGL = 0x3650,
            Rfeca0_Dataout17route_TGL = 0x3654,
            Rfeca0_Dataout18route_TGL = 0x3658,
            Rfeca0_Datavalidroute_TGL = 0x365C,
            Rfeca0_Triggerinroute_TGL = 0x3660,
            Timer0_Routeen_TGL = 0x3668,
            Timer0_Cc0route_TGL = 0x366C,
            Timer0_Cc1route_TGL = 0x3670,
            Timer0_Cc2route_TGL = 0x3674,
            Timer0_Cdti0route_TGL = 0x3678,
            Timer0_Cdti1route_TGL = 0x367C,
            Timer0_Cdti2route_TGL = 0x3680,
            Timer1_Routeen_TGL = 0x3688,
            Timer1_Cc0route_TGL = 0x368C,
            Timer1_Cc1route_TGL = 0x3690,
            Timer1_Cc2route_TGL = 0x3694,
            Timer1_Cdti0route_TGL = 0x3698,
            Timer1_Cdti1route_TGL = 0x369C,
            Timer1_Cdti2route_TGL = 0x36A0,
            Timer2_Routeen_TGL = 0x36A8,
            Timer2_Cc0route_TGL = 0x36AC,
            Timer2_Cc1route_TGL = 0x36B0,
            Timer2_Cc2route_TGL = 0x36B4,
            Timer2_Cc3route_TGL = 0x36B8,
            Timer2_Cc4route_TGL = 0x36BC,
            Timer2_Cc5route_TGL = 0x36C0,
            Timer2_Cc6route_TGL = 0x36C4,
            Timer2_Cdti0route_TGL = 0x36C8,
            Timer2_Cdti1route_TGL = 0x36CC,
            Timer2_Cdti2route_TGL = 0x36D0,
            Timer2_Ccc3route_TGL = 0x36D4,
            Timer2_Ccc4route_TGL = 0x36D8,
            Timer2_Ccc5route_TGL = 0x36DC,
            Timer2_Ccc6route_TGL = 0x36E0,
            Timer3_Routeen_TGL = 0x36E8,
            Timer3_Cc0route_TGL = 0x36EC,
            Timer3_Cc1route_TGL = 0x36F0,
            Timer3_Cc2route_TGL = 0x36F4,
            Timer3_Cc3route_TGL = 0x36F8,
            Timer3_Cc4route_TGL = 0x36FC,
            Timer3_Cc5route_TGL = 0x3700,
            Timer3_Cc6route_TGL = 0x3704,
            Timer3_Cdti0route_TGL = 0x3708,
            Timer3_Cdti1route_TGL = 0x370C,
            Timer3_Cdti2route_TGL = 0x3710,
            Timer3_Ccc3route_TGL = 0x3714,
            Timer3_Ccc4route_TGL = 0x3718,
            Timer3_Ccc5route_TGL = 0x371C,
            Timer3_Ccc6route_TGL = 0x3720,
            Drpu_Rpuratd0_TGL = 0x3728,
            Drpu_Rpuratd1_TGL = 0x372C,
            Drpu_Rpuratd6_TGL = 0x3740,
            Drpu_Rpuratd8_TGL = 0x3748,
            Drpu_Rpuratd9_TGL = 0x374C,
            Drpu_Rpuratd10_TGL = 0x3750,
            Drpu_Rpuratd11_TGL = 0x3754,
            Drpu_Rpuratd12_TGL = 0x3758,
            Drpu_Rpuratd13_TGL = 0x375C,
            Drpu_Rpuratd14_TGL = 0x3760,
        }   
#endregion        
    }
}