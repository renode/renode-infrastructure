//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class IOAPIC : IAPICPeripheral, IDoubleWordPeripheral, IIRQController, IKnownSize
    {
        public IOAPIC()
        {
            this.messageHelper = new APICMessageHelper(this);
            redirectionTable = new IValueRegisterField[MaxRedirectionTableEntries * 2];
            IRQStatus = new bool[MaxRedirectionTableEntries];
            internalLock = new object();
            DefineRegisters();
            Reset();
        }

        public void OnGPIO(int number, bool value)
        {
            lock(internalLock)
            {
                if(number < 0 || number >= MaxRedirectionTableEntries)
                {
                    this.Log(LogLevel.Warning, "IRQ number {0} is too big - max supported number of IRQ {1}", number, MaxRedirectionTableEntries);
                    return;
                }

                ulong tableEntry = redirectionTable[number * 2].Value | redirectionTable[number * 2 + 1].Value << 32;

                // We store IRQ status only for level triggered interrupts
                // edge trigger irq are stateless
                if((tableEntry & TriggerModeBit) != 0)
                {
                    this.Log(LogLevel.Debug, "Line {0} status changed to {1}", number, value);
                    IRQStatus[number] = value;
                }

                // Changing irq status to false does not affect IOAPIC/LAPIC
                if(!value)
                {
                    return;
                }

                this.Log(LogLevel.Noisy, "Received IRQ on line {0}, sending the message", number);
                TrySendShortMessage(number);
            }
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
                registers.Write(offset, value);
            }
        }

        public void Reset()
        {
            lock(internalLock)
            {
                registers.Reset();
            }
        }

        public void EndOfInterrupt(byte vector)
        {
            for(int i = 0; i < MaxRedirectionTableEntries; i++)
            {
                var lowerReg = redirectionTable[i * 2];

                if((lowerReg.Value & 0xFF) == vector)
                {
                    if((lowerReg.Value & RemoteIRRBit) == 0)
                    {
                        this.Log(LogLevel.Warning, "Requested EOI for IRQ {0}->{1} but, IRR bit is not set", i, vector);
                        return;
                    }

                    // Remove Remote IRR flag
                    lowerReg.Value &= ~RemoteIRRBit;
                    this.Log(LogLevel.Debug, "IRQ {0}->{1} EOI", i, vector);

                    if(IRQStatus[i])
                    {
                        this.Log(LogLevel.Debug, "Level on line {0} is still high - rising another interrupt {1}", i, vector);
                        TrySendShortMessage(i);
                    }

                    return;
                }
            }
        }

        public long Size => 1.MB();

        public APICPeripheralType APICPeripheralType => APICPeripheralType.IOAPIC;

        private void DefineRegisters()
        {
            // In IOREGSEL and innerRegisters Intel uses notation where offset 0x1 = 32 bit,
            // so we need to multiply everything by 4 to avoid register overlaping

            var addresses = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.IOREGSEL, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out ioregsel, name: "APIC Register Address")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.IOWIN, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "APIC Register Data",
                        writeCallback: (_, val) => {
                            var offset = ioregsel.Value * 4;
                            innerRegisters.Write((long)offset, (uint)val);
                        },
                        valueProviderCallback: (val) => {
                            var offset = ioregsel.Value * 4;
                            return innerRegisters.Read((long)offset);
                        }
                    )
                },
                {(long)Registers.IRQAssertion, new DoubleWordRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, val) => OnGPIO((int)val, true), name: "Vector")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.IOEOI, new DoubleWordRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, val) => EndOfInterrupt((byte)val), name: "Vector")
                    .WithReservedBits(8, 24)
                }
            };

            var innerAddresses = new Dictionary<long, DoubleWordRegister>
            {
                {(long)InnerRegisters.IOAPICID * 4, new DoubleWordRegister(this)
                    .WithReservedBits(0, 24)
                    .WithTag("IOAPIC Identification", 24, 4)
                    .WithReservedBits(28, 4)
                },
                {(long)InnerRegisters.IOAPICVER * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 8, valueProviderCallback: (_) => 0x11, name: "APIC Version")
                    .WithReservedBits(8, 8)
                    .WithValueField(16, 8, valueProviderCallback: (_) => MaxRedirectionTableEntries, name: "Maximum Redirection Entry")
                    .WithReservedBits(24, 8)
                },
                {(long)InnerRegisters.IOAPICARB * 4, new DoubleWordRegister(this)
                    .WithReservedBits(0, 24)
                    .WithTag("IOAPIC Identification", 24, 4)
                    .WithReservedBits(28, 4)
                }
            };

            // Create IOREDTBL registers 
            for(int reg = 0; reg < MaxRedirectionTableEntries; reg++)
            {
                var regOffset = ((long)InnerRegisters.IOREDTBL0 + (reg * 2));
                var lowerOffset = regOffset * 4;
                var upperOffset = (regOffset + 1) * 4;

                var lower = new DoubleWordRegister(this, 0x00010000)
                    .WithValueField(0, 32, out redirectionTable[reg * 2], name: $"Redirection entry{reg} lower");

                var upper = new DoubleWordRegister(this)
                    .WithValueField(0, 32, out redirectionTable[reg * 2 + 1], name: $"Redirection entry{reg} upper");

                innerAddresses.Add(lowerOffset, lower);
                innerAddresses.Add(upperOffset, upper);
            }

            registers = new DoubleWordRegisterCollection(this, addresses);
            innerRegisters = new DoubleWordRegisterCollection(this, innerAddresses);
        }

        private void TrySendShortMessage(int number)
        {
            ulong tableEntry = redirectionTable[number * 2].Value | redirectionTable[number * 2 + 1].Value << 32;

            // Check if mask or remote irr bits are set
            if((tableEntry & (RemoteIRRBit | InterruptMaskBit)) != 0)
            {
                this.Log(LogLevel.Debug, "Rejecting IRQ from line {0}", number);
                return;
            }

            // Check if trigger mode = level sensitive
            if((tableEntry & TriggerModeBit) != 0)
            {
                // Set Remote IRR field to 1
                redirectionTable[number * 2].Value |= RemoteIRRBit;
            }

            messageHelper.SendShortMessage(new APICMessageHelper.ShortMessage(tableEntry));
        }

        private IValueRegisterField ioregsel;

        private DoubleWordRegisterCollection registers;
        private DoubleWordRegisterCollection innerRegisters;

        private readonly IValueRegisterField[] redirectionTable;
        private readonly bool[] IRQStatus;
        private readonly APICMessageHelper messageHelper;
        private readonly object internalLock;

        private const int MaxRedirectionTableEntries = 239;
        private const int NumberOfOutgoingInterrupts = 256;
        private const ulong RemoteIRRBit = (1 << 14);
        private const ulong TriggerModeBit = (1 << 15);
        private const ulong InterruptMaskBit = (1 << 16);

        public enum Registers
        {
            IOREGSEL = 0x00,
            IOWIN = 0x10,
            IRQAssertion = 0x20,
            IOEOI = 0x40,
        }

        public enum InnerRegisters
        {
            IOAPICID = 0x00,
            IOAPICVER = 0x01,
            IOAPICARB = 0x02,
            IOREDTBL0 = 0x10,
            // ...
            IOREDTBL23 = 0x3F
        }
    }
}