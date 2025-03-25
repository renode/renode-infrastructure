//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class EFR32MG24_LDMA : IBusPeripheral, IGPIOReceiver, IKnownSize
    {
        public EFR32MG24_LDMA(Machine machine)
        {
            this.machine = machine;
            engine = new DmaEngine(machine.GetSystemBus(this));
            signals = new HashSet<int>();
            IRQ = new GPIO();
            channels = new Channel[NumberOfChannels];
            for(var i = 0; i < NumberOfChannels; ++i)
            {
                channels[i] = new Channel(this, i);
            }
            ldmaRegistersCollection = BuildLdmaRegisters();
            ldmaXbarRegistersCollection = BuildLdmaXbarRegisters();
        }

        public void Reset()
        {
            signals.Clear();
            foreach(var channel in channels)
            {
                channel.Reset();
            }
            UpdateInterrupts();
        }

        public void OnGPIO(int number, bool value)
        {
            var signal = (SignalSelect)(number & 0xf);
            var source = (SourceSelect)((number >> 4) & 0x3f);
            bool single = ((number >> 12) & 1) != 0;

            if(!value)
            {
                signals.Remove(number);
                return;
            }
            signals.Add(number);
            for(var i = 0; i < NumberOfChannels; ++i)
            {
                if(single && channels[i].IgnoreSingleRequests)
                {
                    continue;
                }
                if(channels[i].Signal == signal && channels[i].Source == source)
                {
                    channels[i].StartFromSignal();
                }
            }
        }

        public GPIO IRQ { get; }

        public long Size => 0x400;
        private readonly DoubleWordRegisterCollection ldmaRegistersCollection;
        private readonly DoubleWordRegisterCollection ldmaXbarRegistersCollection;
        private readonly Machine machine; 

        private uint Read<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, bool internal_read = false)
        where T : struct, IComparable, IFormattable
        {
            var result = 0U;
            long internal_offset = offset;

            // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
            if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
            {
                // Set register
                internal_offset = offset - SetRegisterOffset;
                if(!internal_read)
                {  
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            } else if (offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            }

            if(!registersCollection.TryRead(internal_offset, out result))
            {
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "Unhandled read from {0} at offset 0x{1:X} ({2}).", regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"));
                }
            }
            else
            {
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "{0}: Read from {1} at offset 0x{2:X} ({3}), returned 0x{4:X}", 
                             this.GetTime(), regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), result);
                }
            }

            return result;
        }

        private void Write<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, uint value)
        where T : struct, IComparable, IFormattable
        {
            machine.ClockSource.ExecuteInLock(delegate {
                long internal_offset = offset;
                uint internal_value = value;

                if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    uint old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value | value;
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value & ~value;
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                } else if (offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value ^ value;
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                }

                this.Log(LogLevel.Noisy, "{0}: Write to {1} at offset 0x{2:X} ({3}), value 0x{4:X}", 
                        this.GetTime(), regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), internal_value);

                if(!registersCollection.TryWrite(internal_offset, internal_value))
                {
                    this.Log(LogLevel.Debug, "Unhandled write to {0} at offset 0x{1:X} ({2}), value 0x{3:X}.", regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), internal_value);
                    return;
                }
            });
        }

        [ConnectionRegionAttribute("ldma")]
        public void WriteDoubleWordToLdma(long offset, uint value)
        {
            Write<LdmaRegisters>(ldmaRegistersCollection, "Ldma", offset, value);
        }

        [ConnectionRegionAttribute("ldma")]
        public uint ReadDoubleWordFromLdma(long offset)
        {
            return Read<LdmaRegisters>(ldmaRegistersCollection, "Ldma", offset);
        }

        [ConnectionRegionAttribute("ldmaxbar")]
        public void WriteDoubleWordToLdmaXbar(long offset, uint value)
        {
            Write<LdmaXbarRegisters>(ldmaXbarRegistersCollection, "LdmaXbar", offset, value);
        }

        [ConnectionRegionAttribute("ldmaxbar")]
        public uint ReadDoubleWordFromLdmaXbar(long offset)
        {
            return Read<LdmaXbarRegisters>(ldmaXbarRegistersCollection, "LdmaXbar", offset);
        }

        private DoubleWordRegisterCollection BuildLdmaRegisters()
        {
            DoubleWordRegisterCollection c =  new DoubleWordRegisterCollection(this, new Dictionary<long, DoubleWordRegister>
            {
                {(long)LdmaRegisters.CTRL, new DoubleWordRegister(this)
                    .WithReservedBits(0, 24)
                    .WithTag("NUMFIXED", 24, 5)
                    .WithReservedBits(29, 2)
                    .WithTaggedFlag("CORERST", 31)
                },
                {(long)LdmaRegisters.STATUS, new DoubleWordRegister(this)
                    .WithTaggedFlag("ANYBUSY", 0)
                    .WithTaggedFlag("ANYREQ", 1)
                    .WithReservedBits(2, 1)
                    .WithTag("CHGRANT", 3, 5)
                    .WithTag("CHERROR", 8, 5)
                    .WithReservedBits(13, 3)
                    .WithTag("FIFOLEVEL", 16, 5)
                    .WithReservedBits(21, 3)
                    .WithTag("CHNUM", 24, 5)
                    .WithReservedBits(29, 3)
                },
                {(long)LdmaRegisters.CHEN, new DoubleWordRegister(this)
                    .WithFlags(0, 31, writeCallback: (i, _, value) => { if (value) channels[i].Enabled = true; }, name: "CHEN")
                    .WithReservedBits(31, 1)
                },
                {(long)LdmaRegisters.CHDIS, new DoubleWordRegister(this)
                    .WithFlags(0, 31, writeCallback: (i, _, value) => { if (value) channels[i].Enabled = false; }, name: "CHDIS")
                    .WithReservedBits(31, 1)
                },
                {(long)LdmaRegisters.CHBUSY, new DoubleWordRegister(this)
                    .WithFlags(0, 31, FieldMode.Read, valueProviderCallback: (i, _) => channels[i].Busy, name: "CHBUSY")
                    .WithReservedBits(31, 1)
                },
                {(long)LdmaRegisters.CHSTATUS, new DoubleWordRegister(this)
                    .WithFlags(0, 31, FieldMode.Read, valueProviderCallback: (i, _) => channels[i].Enabled, name: "CHSTATUS")
                    .WithReservedBits(31, 1)
                },
                {(long)LdmaRegisters.CHDONE, new DoubleWordRegister(this)
                    .WithFlags(0, 31, writeCallback: (i, _, value) => channels[i].Done = value, valueProviderCallback: (i, _) => channels[i].Done, name: "CHDONE")
                    .WithReservedBits(31, 1)
                },
                {(long)LdmaRegisters.DBGHALT, new DoubleWordRegister(this)
                    .WithTag("DBGHALT", 0, 31)
                    .WithReservedBits(31, 1)
                },
                {(long)LdmaRegisters.SWREQ, new DoubleWordRegister(this)
                    .WithFlags(0, 31, FieldMode.Set, writeCallback: (i, _, value) => { if(value) channels[i].StartTransfer(); }, name: "SWREQ")
                    .WithReservedBits(31, 1)
                },
                {(long)LdmaRegisters.REQDIS, new DoubleWordRegister(this)
                    .WithFlags(0, 31, writeCallback: (i, _, value) => channels[i].RequestDisable = value, valueProviderCallback: (i, _) => channels[i].RequestDisable, name: "REQDIS")
                    .WithReservedBits(31, 1)
                },
                {(long)LdmaRegisters.REQPEND, new DoubleWordRegister(this)
                    .WithTag("REQPEND", 0, 31)
                    .WithReservedBits(31, 1)
                },
                {(long)LdmaRegisters.LINKLOAD, new DoubleWordRegister(this)
                    .WithFlags(0, 31, FieldMode.Set, writeCallback: (i, _, value) => { if(value) channels[i].LinkLoad(); }, name: "LINKLOAD")
                    .WithReservedBits(31, 1)
                },
                {(long)LdmaRegisters.REQCLEAR, new DoubleWordRegister(this)
                    .WithTag("REQCLEAR", 0, 31)
                    .WithReservedBits(31, 1)
                },
                {(long)LdmaRegisters.IF, new DoubleWordRegister(this)
                    .WithFlags(0, 31, writeCallback: (i, _, value) => channels[i].DoneInterrupt = value, valueProviderCallback: (i, _) => channels[i].DoneInterrupt, name: "IF")
                    .WithTaggedFlag("ERROR", 31)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)LdmaRegisters.IEN, new DoubleWordRegister(this)
                    .WithFlags(0, 31, writeCallback: (i, _, value) => channels[i].DoneInterruptEnable = value, valueProviderCallback: (i, _) => channels[i].DoneInterruptEnable, name: "IEN")
                    .WithTaggedFlag("ERROR", 31)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
            });

            var channelDelta = (uint)((long)LdmaRegisters.CH1_CFG - (long)LdmaRegisters.CH0_CFG);
            BindRegisters(LdmaRegisters.CH0_CFG, c, NumberOfChannels, i => channels[i].ConfigurationRegister, channelDelta);
            BindRegisters(LdmaRegisters.CH0_LOOP, c, NumberOfChannels, i => channels[i].LoopCounterRegister, channelDelta);
            BindRegisters(LdmaRegisters.CH0_CTRL, c, NumberOfChannels, i => channels[i].DescriptorControlWordRegister, channelDelta);
            BindRegisters(LdmaRegisters.CH0_SRC, c, NumberOfChannels, i => channels[i].DescriptorSourceDataAddressRegister, channelDelta);
            BindRegisters(LdmaRegisters.CH0_DST, c, NumberOfChannels, i => channels[i].DescriptorDestinationDataAddressRegister, channelDelta);
            BindRegisters(LdmaRegisters.CH0_LINK, c, NumberOfChannels, i => channels[i].DescriptorLinkStructureAddressRegister, channelDelta);

            return c;
        }

        private DoubleWordRegisterCollection BuildLdmaXbarRegisters()
        {
            DoubleWordRegisterCollection c =  new DoubleWordRegisterCollection(this, new Dictionary<long, DoubleWordRegister>());

            var channelDelta = (uint)((long)LdmaXbarRegisters.XBAR_CH1_REQSEL - (long)LdmaXbarRegisters.XBAR_CH0_REQSEL);
            BindRegisters(LdmaXbarRegisters.XBAR_CH0_REQSEL, c, NumberOfChannels, i => channels[i].PeripheralRequestSelectRegister, channelDelta);

            return c;
        }

        private void BindRegisters(IConvertible o, DoubleWordRegisterCollection c, uint count, Func<int, DoubleWordRegister> setup, uint stepInBytes = 4)
        {
            if(!o.GetType().IsEnum)
            {
                throw new ArgumentException("This method should be called on enumerated type");
            }

            var baseAddress = Convert.ToInt64(o);
            for(var i = 0; i < count; i++)
            {
                var register = setup(i);
                c.AddRegister(baseAddress + i * stepInBytes, register);
            }
        }

        private void UpdateInterrupts()
        {
            this.Log(LogLevel.Debug, "Interrupt set for channels: {0}", String.Join(", ",            
                channels
                    .Where(channel => channel.IRQ)
                    .Select(channel => channel.Index)
                ));
            IRQ.Set(channels.Any(channel => channel.IRQ));
        }

        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;

        private readonly DmaEngine engine;
        private readonly HashSet<int> signals;
        private readonly Channel[] channels;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private const int NumberOfChannels = 31;

        private enum SignalSelect
        {
            // if SOURCESEL is LDMAXBAR
            LDMAXBAR_DMA_PRSREQ0                = 0x0,
            LDMAXBAR_DMA_PRSREQ1                = 0x1,
            // if SOURCESEL is TIMER0
            TIMER0_DMA_CC0                      = 0x0,
            TIMER0_DMA_CC1                      = 0x1,
            TIMER0_DMA_CC2                      = 0x2,
            TIMER0_DMA_UFOF                     = 0x3,
            // if SOURCESEL is TIMER1
            TIMER1_DMA_CC0                      = 0x0,
            TIMER1_DMA_CC1                      = 0x1,
            TIMER1_DMA_CC2                      = 0x2,
            TIMER1_DMA_UFOF                     = 0x3,
            // if SOURCESEL is USART0
            USART0_DMA_RXDATAV                  = 0x0,
            USART0_DMA_RXDATAVRIGHT             = 0x1,
            USART0_DMA_TXBL                     = 0x2,
            USART0_DMA_TXBLRIGHT                = 0x3,
            USART0_DMA_TXEMPTY                  = 0x4,
            // if SOURCESEL is I2C0
            I2C0_DMA_RXDATAV                    = 0x0,
            I2C0_DMA_TXBL                       = 0x1,            
            // if SOURCESEL is I2C1
            I2C1_DMA_RXDATAV                    = 0x0,
            I2C1_DMA_TXBL                       = 0x1,
            // if SOURCESEL is IADC0
            IADC0_DMA_IADC_SCAN                 = 0x0,
            IADC0_DMA_IADC_SINGLE               = 0x1,
            // if SOURCESEL is MSB
            MSC_DMA_WDATA                       = 0x0,
            // if SOURCESEL is TIMER2
            TIMER2_DMA_CC0                      = 0x0,
            TIMER2_DMA_CC1                      = 0x1,
            TIMER2_DMA_CC2                      = 0x2,
            TIMER2_DMA_UFOF                     = 0x3,
            // if SOURCESEL is TIMER3
            TIMER3_DMA_CC0                      = 0x0,
            TIMER3_DMA_CC1                      = 0x1,
            TIMER3_DMA_CC2                      = 0x2,
            TIMER3_DMA_UFOF                     = 0x3,
            // if SOURCESEL is TIMER4
            TIMER4_DMA_CC0                      = 0x0,
            TIMER4_DMA_CC1                      = 0x1,
            TIMER4_DMA_CC2                      = 0x2,
            TIMER4_DMA_UFOF                     = 0x3,
            // if SOURCESEL is EUSART0
            EUSART0_DMA_RXFL                    = 0x0,
            EUSART0_DMA_TXFL                    = 0x1,
            // if SOURCESEL is EUSART1
            EUSART1_DMA_RXFL                    = 0x0,
            EUSART1_DMA_TXFL                    = 0x1,
            // if VDAC0
            VDAC0_DMA_CH0_REQ                   = 0x0,
            VDAC0_DMA_CH1_REQ                   = 0x1,
            // if VDAC1
            VDAC1_DMA_CH0_REQ                   = 0x0,
            VDAC1_DMA_CH1_REQ                   = 0x1,
        }

        private enum SourceSelect
        {
            None     = 0x0,
            LDMAXBAR = 0x1,
            TIMER0   = 0x2,
            TIMER1   = 0x3,
            USART0   = 0x4,
            I2C0     = 0x5,
            I2C1     = 0x6,
            IADC0    = 0xA,
            MSC      = 0xB,
            TIMER2   = 0xC,
            TIMER3   = 0xD,
            TIMER4   = 0xE,
            EUSART0  = 0xF,
            EUSART1  = 0x10,
            VDAC0    = 0x11,
            VDAC1    = 0x12,
        }

        private enum LdmaRegisters : long
        {
            IPVERSION              = 0x0000,
            EN                     = 0x0004,
            CTRL                   = 0x0008,
            STATUS                 = 0x000C,
            SYNCSWSET              = 0x0010,
            SYNCSWCLR              = 0x0014,
            SYNCHWEN               = 0x0018,
            SYNCHWSEL              = 0x001C,
            SYNCSTATUS             = 0x0020,
            CHEN                   = 0x0024,
            CHDIS                  = 0x0028,
            CHSTATUS               = 0x002C,
            CHBUSY                 = 0x0030,
            CHDONE                 = 0x0034,
            DBGHALT                = 0x0038,
            SWREQ                  = 0x003C,
            REQDIS                 = 0x0040,
            REQPEND                = 0x0044,
            LINKLOAD               = 0x0048,
            REQCLEAR               = 0x004C,
            IF                     = 0x0050,
            IEN                    = 0x0054,
            CH0_CFG                = 0x005C,
            CH0_LOOP               = 0x0060,
            CH0_CTRL               = 0x0064,
            CH0_SRC                = 0x0068,
            CH0_DST                = 0x006C,
            CH0_LINK               = 0x0070,
            CH1_CFG                = 0x008C,
            CH1_LOOP               = 0x0090,
            CH1_CTRL               = 0x0094,
            CH1_SRC                = 0x0098,
            CH1_DST                = 0x009C,
            CH1_LINK               = 0x00A0,
            CH2_CFG                = 0x00BC,
            CH2_LOOP               = 0x00C0,
            CH2_CTRL               = 0x00C4,
            CH2_SRC                = 0x00C8,
            CH2_DST                = 0x00CC,
            CH2_LINK               = 0x00D0,
            CH3_CFG                = 0x00EC,
            CH3_LOOP               = 0x00F0,
            CH3_CTRL               = 0x00F4,
            CH3_SRC                = 0x00F8,
            CH3_DST                = 0x00FC,
            CH3_LINK               = 0x0100,
            CH4_CFG                = 0x011C,
            CH4_LOOP               = 0x0120,
            CH4_CTRL               = 0x0124,
            CH4_SRC                = 0x0128,
            CH4_DST                = 0x012C,
            CH4_LINK               = 0x0130,
            CH5_CFG                = 0x014C,
            CH5_LOOP               = 0x0150,
            CH5_CTRL               = 0x0154,
            CH5_SRC                = 0x0158,
            CH5_DST                = 0x015C,
            CH5_LINK               = 0x0160,
            CH6_CFG                = 0x017C,
            CH6_LOOP               = 0x0180,
            CH6_CTRL               = 0x0184,
            CH6_SRC                = 0x0188,
            CH6_DST                = 0x018C,
            CH6_LINK               = 0x0190,
            CH7_CFG                = 0x01AC,
            CH7_LOOP               = 0x01B0,
            CH7_CTRL               = 0x01B4,
            CH7_SRC                = 0x01B8,
            CH7_DST                = 0x01BC,
            CH7_LINK               = 0x01C0,
            CH8_CFG                = 0x01DC,
            CH8_LOOP               = 0x01E0,
            CH8_CTRL               = 0x01E4,
            CH8_SRC                = 0x01E8,
            CH8_DST                = 0x01EC,
            CH8_LINK               = 0x01F0,
            CH9_CFG                = 0x020C,
            CH9_LOOP               = 0x0210,
            CH9_CTRL               = 0x0214,
            CH9_SRC                = 0x0218,
            CH9_DST                = 0x021C,
            CH9_LINK               = 0x0220,
            CH10_CFG               = 0x023C,
            CH10_LOOP              = 0x0240,
            CH10_CTRL              = 0x0244,
            CH10_SRC               = 0x0248,
            CH10_DST               = 0x024C,
            CH10_LINK              = 0x0250,
            CH11_CFG               = 0x026C,
            CH11_LOOP              = 0x0270,
            CH11_CTRL              = 0x0274,
            CH11_SRC               = 0x0278,
            CH11_DST               = 0x027C,
            CH11_LINK              = 0x0280,
            CH12_CFG               = 0x029C,
            CH12_LOOP              = 0x02A0,
            CH12_CTRL              = 0x02A4,
            CH12_SRC               = 0x02A8,
            CH12_DST               = 0x02AC,
            CH12_LINK              = 0x02B0,
            CH13_CFG               = 0x02CC,
            CH13_LOOP              = 0x02D0,
            CH13_CTRL              = 0x02D4,
            CH13_SRC               = 0x02D8,
            CH13_DST               = 0x02DC,
            CH13_LINK              = 0x02E0,
            CH14_CFG               = 0x02FC,
            CH14_LOOP              = 0x0300,
            CH14_CTRL              = 0x0304,
            CH14_SRC               = 0x0308,
            CH14_DST               = 0x030C,
            CH14_LINK              = 0x0310,
            CH15_CFG               = 0x032C,
            CH15_LOOP              = 0x0330,
            CH15_CTRL              = 0x0334,
            CH15_SRC               = 0x0338,
            CH15_DST               = 0x033C,
            CH15_LINK              = 0x0340,
            CH16_CFG               = 0x035C,
            CH16_LOOP              = 0x0360,
            CH16_CTRL              = 0x0364,
            CH16_SRC               = 0x0368,
            CH16_DST               = 0x036C,
            CH16_LINK              = 0x0370,
            CH17_CFG               = 0x038C,
            CH17_LOOP              = 0x0390,
            CH17_CTRL              = 0x0394,
            CH17_SRC               = 0x0398,
            CH17_DST               = 0x039C,
            CH17_LINK              = 0x03A0,
            CH18_CFG               = 0x03BC,
            CH18_LOOP              = 0x03C0,
            CH18_CTRL              = 0x03C4,
            CH18_SRC               = 0x03C8,
            CH18_DST               = 0x03CC,
            CH18_LINK              = 0x03D0,
            CH19_CFG               = 0x03EC,
            CH19_LOOP              = 0x03F0,
            CH19_CTRL              = 0x03F4,
            CH19_SRC               = 0x03F8,
            CH19_DST               = 0x03FC,
            CH19_LINK              = 0x0400,
            CH20_CFG               = 0x041C,
            CH20_LOOP              = 0x0420,
            CH20_CTRL              = 0x0424,
            CH20_SRC               = 0x0428,
            CH20_DST               = 0x042C,
            CH20_LINK              = 0x0430,
            CH21_CFG               = 0x044C,
            CH21_LOOP              = 0x0450,
            CH21_CTRL              = 0x0454,
            CH21_SRC               = 0x0458,
            CH21_DST               = 0x045C,
            CH21_LINK              = 0x0460,
            CH22_CFG               = 0x047C,
            CH22_LOOP              = 0x0480,
            CH22_CTRL              = 0x0484,
            CH22_SRC               = 0x0488,
            CH22_DST               = 0x048C,
            CH22_LINK              = 0x0490,
            CH23_CFG               = 0x04AC,
            CH23_LOOP              = 0x04B0,
            CH23_CTRL              = 0x04B4,
            CH23_SRC               = 0x04B8,
            CH23_DST               = 0x04BC,
            CH23_LINK              = 0x04C0,
            CH24_CFG               = 0x04DC,
            CH24_LOOP              = 0x04E0,
            CH24_CTRL              = 0x04E4,
            CH24_SRC               = 0x04E8,
            CH24_DST               = 0x04EC,
            CH24_LINK              = 0x04F0,
            CH25_CFG               = 0x050C,
            CH25_LOOP              = 0x0510,
            CH25_CTRL              = 0x0514,
            CH25_SRC               = 0x0518,
            CH25_DST               = 0x051C,
            CH25_LINK              = 0x0520,
            CH26_CFG               = 0x053C,
            CH26_LOOP              = 0x0540,
            CH26_CTRL              = 0x0544,
            CH26_SRC               = 0x0548,
            CH26_DST               = 0x054C,
            CH26_LINK              = 0x0550,
            CH27_CFG               = 0x056C,
            CH27_LOOP              = 0x0570,
            CH27_CTRL              = 0x0574,
            CH27_SRC               = 0x0578,
            CH27_DST               = 0x057C,
            CH27_LINK              = 0x0580,
            CH28_CFG               = 0x059C,
            CH28_LOOP              = 0x05A0,
            CH28_CTRL              = 0x05A4,
            CH28_SRC               = 0x05A8,
            CH28_DST               = 0x05AC,
            CH28_LINK              = 0x05B0,
            CH29_CFG               = 0x05CC,
            CH29_LOOP              = 0x05D0,
            CH29_CTRL              = 0x05D4,
            CH29_SRC               = 0x05D8,
            CH29_DST               = 0x05DC,
            CH29_LINK              = 0x05E0,
            CH30_CFG               = 0x05FC,
            CH30_LOOP              = 0x0600,
            CH30_CTRL              = 0x0604,
            CH30_SRC               = 0x0608,
            CH30_DST               = 0x060C,
            CH30_LINK              = 0x0610,
            // Set registers
            IPVERSION_Set          = 0x1000,
            EN_Set                 = 0x1004,
            CTRL_Set               = 0x1008,
            STATUS_Set             = 0x100C,
            SYNCSWSET_Set          = 0x1010,
            SYNCSWCLR_Set          = 0x1014,
            SYNCHWEN_Set           = 0x1018,
            SYNCHWSEL_Set          = 0x101C,
            SYNCSTATUS_Set         = 0x1020,
            CHEN_Set               = 0x1024,
            CHDIS_Set              = 0x1028,
            CHSTATUS_Set           = 0x102C,
            CHBUSY_Set             = 0x1030,
            CHDONE_Set             = 0x1034,
            DBGHALT_Set            = 0x1038,
            SWREQ_Set              = 0x103C,
            REQDIS_Set             = 0x1040,
            REQPEND_Set            = 0x1044,
            LINKLOAD_Set           = 0x1048,
            REQCLEAR_Set           = 0x104C,
            IF_Set                 = 0x1050,
            IEN_Set                = 0x1054,
            CH0_CFG_Set            = 0x105C,
            CH0_LOOP_Set           = 0x1060,
            CH0_CTRL_Set           = 0x1064,
            CH0_SRC_Set            = 0x1068,
            CH0_DST_Set            = 0x106C,
            CH0_LINK_Set           = 0x1070,
            CH1_CFG_Set            = 0x108C,
            CH1_LOOP_Set           = 0x1090,
            CH1_CTRL_Set           = 0x1094,
            CH1_SRC_Set            = 0x1098,
            CH1_DST_Set            = 0x109C,
            CH1_LINK_Set           = 0x10A0,
            CH2_CFG_Set            = 0x10BC,
            CH2_LOOP_Set           = 0x10C0,
            CH2_CTRL_Set           = 0x10C4,
            CH2_SRC_Set            = 0x10C8,
            CH2_DST_Set            = 0x10CC,
            CH2_LINK_Set           = 0x10D0,
            CH3_CFG_Set            = 0x10EC,
            CH3_LOOP_Set           = 0x10F0,
            CH3_CTRL_Set           = 0x10F4,
            CH3_SRC_Set            = 0x10F8,
            CH3_DST_Set            = 0x10FC,
            CH3_LINK_Set           = 0x1100,
            CH4_CFG_Set            = 0x111C,
            CH4_LOOP_Set           = 0x1120,
            CH4_CTRL_Set           = 0x1124,
            CH4_SRC_Set            = 0x1128,
            CH4_DST_Set            = 0x112C,
            CH4_LINK_Set           = 0x1130,
            CH5_CFG_Set            = 0x114C,
            CH5_LOOP_Set           = 0x1150,
            CH5_CTRL_Set           = 0x1154,
            CH5_SRC_Set            = 0x1158,
            CH5_DST_Set            = 0x115C,
            CH5_LINK_Set           = 0x1160,
            CH6_CFG_Set            = 0x117C,
            CH6_LOOP_Set           = 0x1180,
            CH6_CTRL_Set           = 0x1184,
            CH6_SRC_Set            = 0x1188,
            CH6_DST_Set            = 0x118C,
            CH6_LINK_Set           = 0x1190,
            CH7_CFG_Set            = 0x11AC,
            CH7_LOOP_Set           = 0x11B0,
            CH7_CTRL_Set           = 0x11B4,
            CH7_SRC_Set            = 0x11B8,
            CH7_DST_Set            = 0x11BC,
            CH7_LINK_Set           = 0x11C0,
            CH8_CFG_Set            = 0x11DC,
            CH8_LOOP_Set           = 0x11E0,
            CH8_CTRL_Set           = 0x11E4,
            CH8_SRC_Set            = 0x11E8,
            CH8_DST_Set            = 0x11EC,
            CH8_LINK_Set           = 0x11F0,
            CH9_CFG_Set            = 0x120C,
            CH9_LOOP_Set           = 0x1210,
            CH9_CTRL_Set           = 0x1214,
            CH9_SRC_Set            = 0x1218,
            CH9_DST_Set            = 0x121C,
            CH9_LINK_Set           = 0x1220,
            CH10_CFG_Set           = 0x123C,
            CH10_LOOP_Set          = 0x1240,
            CH10_CTRL_Set          = 0x1244,
            CH10_SRC_Set           = 0x1248,
            CH10_DST_Set           = 0x124C,
            CH10_LINK_Set          = 0x1250,
            CH11_CFG_Set           = 0x126C,
            CH11_LOOP_Set          = 0x1270,
            CH11_CTRL_Set          = 0x1274,
            CH11_SRC_Set           = 0x1278,
            CH11_DST_Set           = 0x127C,
            CH11_LINK_Set          = 0x1280,
            CH12_CFG_Set           = 0x129C,
            CH12_LOOP_Set          = 0x12A0,
            CH12_CTRL_Set          = 0x12A4,
            CH12_SRC_Set           = 0x12A8,
            CH12_DST_Set           = 0x12AC,
            CH12_LINK_Set          = 0x12B0,
            CH13_CFG_Set           = 0x12CC,
            CH13_LOOP_Set          = 0x12D0,
            CH13_CTRL_Set          = 0x12D4,
            CH13_SRC_Set           = 0x12D8,
            CH13_DST_Set           = 0x12DC,
            CH13_LINK_Set          = 0x12E0,
            CH14_CFG_Set           = 0x12FC,
            CH14_LOOP_Set          = 0x1300,
            CH14_CTRL_Set          = 0x1304,
            CH14_SRC_Set           = 0x1308,
            CH14_DST_Set           = 0x130C,
            CH14_LINK_Set          = 0x1310,
            CH15_CFG_Set           = 0x132C,
            CH15_LOOP_Set          = 0x1330,
            CH15_CTRL_Set          = 0x1334,
            CH15_SRC_Set           = 0x1338,
            CH15_DST_Set           = 0x133C,
            CH15_LINK_Set          = 0x1340,
            CH16_CFG_Set           = 0x135C,
            CH16_LOOP_Set          = 0x1360,
            CH16_CTRL_Set          = 0x1364,
            CH16_SRC_Set           = 0x1368,
            CH16_DST_Set           = 0x136C,
            CH16_LINK_Set          = 0x1370,
            CH17_CFG_Set           = 0x138C,
            CH17_LOOP_Set          = 0x1390,
            CH17_CTRL_Set          = 0x1394,
            CH17_SRC_Set           = 0x1398,
            CH17_DST_Set           = 0x139C,
            CH17_LINK_Set          = 0x13A0,
            CH18_CFG_Set           = 0x13BC,
            CH18_LOOP_Set          = 0x13C0,
            CH18_CTRL_Set          = 0x13C4,
            CH18_SRC_Set           = 0x13C8,
            CH18_DST_Set           = 0x13CC,
            CH18_LINK_Set          = 0x13D0,
            CH19_CFG_Set           = 0x13EC,
            CH19_LOOP_Set          = 0x13F0,
            CH19_CTRL_Set          = 0x13F4,
            CH19_SRC_Set           = 0x13F8,
            CH19_DST_Set           = 0x13FC,
            CH19_LINK_Set          = 0x1400,
            CH20_CFG_Set           = 0x141C,
            CH20_LOOP_Set          = 0x1420,
            CH20_CTRL_Set          = 0x1424,
            CH20_SRC_Set           = 0x1428,
            CH20_DST_Set           = 0x142C,
            CH20_LINK_Set          = 0x1430,
            CH21_CFG_Set           = 0x144C,
            CH21_LOOP_Set          = 0x1450,
            CH21_CTRL_Set          = 0x1454,
            CH21_SRC_Set           = 0x1458,
            CH21_DST_Set           = 0x145C,
            CH21_LINK_Set          = 0x1460,
            CH22_CFG_Set           = 0x147C,
            CH22_LOOP_Set          = 0x1480,
            CH22_CTRL_Set          = 0x1484,
            CH22_SRC_Set           = 0x1488,
            CH22_DST_Set           = 0x148C,
            CH22_LINK_Set          = 0x1490,
            CH23_CFG_Set           = 0x14AC,
            CH23_LOOP_Set          = 0x14B0,
            CH23_CTRL_Set          = 0x14B4,
            CH23_SRC_Set           = 0x14B8,
            CH23_DST_Set           = 0x14BC,
            CH23_LINK_Set          = 0x14C0,
            CH24_CFG_Set           = 0x14DC,
            CH24_LOOP_Set          = 0x14E0,
            CH24_CTRL_Set          = 0x14E4,
            CH24_SRC_Set           = 0x14E8,
            CH24_DST_Set           = 0x14EC,
            CH24_LINK_Set          = 0x14F0,
            CH25_CFG_Set           = 0x150C,
            CH25_LOOP_Set          = 0x1510,
            CH25_CTRL_Set          = 0x1514,
            CH25_SRC_Set           = 0x1518,
            CH25_DST_Set           = 0x151C,
            CH25_LINK_Set          = 0x1520,
            CH26_CFG_Set           = 0x153C,
            CH26_LOOP_Set          = 0x1540,
            CH26_CTRL_Set          = 0x1544,
            CH26_SRC_Set           = 0x1548,
            CH26_DST_Set           = 0x154C,
            CH26_LINK_Set          = 0x1550,
            CH27_CFG_Set           = 0x156C,
            CH27_LOOP_Set          = 0x1570,
            CH27_CTRL_Set          = 0x1574,
            CH27_SRC_Set           = 0x1578,
            CH27_DST_Set           = 0x157C,
            CH27_LINK_Set          = 0x1580,
            CH28_CFG_Set           = 0x159C,
            CH28_LOOP_Set          = 0x15A0,
            CH28_CTRL_Set          = 0x15A4,
            CH28_SRC_Set           = 0x15A8,
            CH28_DST_Set           = 0x15AC,
            CH28_LINK_Set          = 0x15B0,
            CH29_CFG_Set           = 0x15CC,
            CH29_LOOP_Set          = 0x15D0,
            CH29_CTRL_Set          = 0x15D4,
            CH29_SRC_Set           = 0x15D8,
            CH29_DST_Set           = 0x15DC,
            CH29_LINK_Set          = 0x15E0,
            CH30_CFG_Set           = 0x15FC,
            CH30_LOOP_Set          = 0x1600,
            CH30_CTRL_Set          = 0x1604,
            CH30_SRC_Set           = 0x1608,
            CH30_DST_Set           = 0x160C,
            CH30_LINK_Set          = 0x1610,
            // Clear registers
            IPVERSION_Clr          = 0x2000,
            EN_Clr                 = 0x2004,
            CTRL_Clr               = 0x2008,
            STATUS_Clr             = 0x200C,
            SYNCSWSET_Clr          = 0x2010,
            SYNCSWCLR_Clr          = 0x2014,
            SYNCHWEN_Clr           = 0x2018,
            SYNCHWSEL_Clr          = 0x201C,
            SYNCSTATUS_Clr         = 0x2020,
            CHEN_Clr               = 0x2024,
            CHDIS_Clr              = 0x2028,
            CHSTATUS_Clr           = 0x202C,
            CHBUSY_Clr             = 0x2030,
            CHDONE_Clr             = 0x2034,
            DBGHALT_Clr            = 0x2038,
            SWREQ_Clr              = 0x203C,
            REQDIS_Clr             = 0x2040,
            REQPEND_Clr            = 0x2044,
            LINKLOAD_Clr           = 0x2048,
            REQCLEAR_Clr           = 0x204C,
            IF_Clr                 = 0x2050,
            IEN_Clr                = 0x2054,
            CH0_CFG_Clr            = 0x205C,
            CH0_LOOP_Clr           = 0x2060,
            CH0_CTRL_Clr           = 0x2064,
            CH0_SRC_Clr            = 0x2068,
            CH0_DST_Clr            = 0x206C,
            CH0_LINK_Clr           = 0x2070,
            CH1_CFG_Clr            = 0x208C,
            CH1_LOOP_Clr           = 0x2090,
            CH1_CTRL_Clr           = 0x2094,
            CH1_SRC_Clr            = 0x2098,
            CH1_DST_Clr            = 0x209C,
            CH1_LINK_Clr           = 0x20A0,
            CH2_CFG_Clr            = 0x20BC,
            CH2_LOOP_Clr           = 0x20C0,
            CH2_CTRL_Clr           = 0x20C4,
            CH2_SRC_Clr            = 0x20C8,
            CH2_DST_Clr            = 0x20CC,
            CH2_LINK_Clr           = 0x20D0,
            CH3_CFG_Clr            = 0x20EC,
            CH3_LOOP_Clr           = 0x20F0,
            CH3_CTRL_Clr           = 0x20F4,
            CH3_SRC_Clr            = 0x20F8,
            CH3_DST_Clr            = 0x20FC,
            CH3_LINK_Clr           = 0x2100,
            CH4_CFG_Clr            = 0x211C,
            CH4_LOOP_Clr           = 0x2120,
            CH4_CTRL_Clr           = 0x2124,
            CH4_SRC_Clr            = 0x2128,
            CH4_DST_Clr            = 0x212C,
            CH4_LINK_Clr           = 0x2130,
            CH5_CFG_Clr            = 0x214C,
            CH5_LOOP_Clr           = 0x2150,
            CH5_CTRL_Clr           = 0x2154,
            CH5_SRC_Clr            = 0x2158,
            CH5_DST_Clr            = 0x215C,
            CH5_LINK_Clr           = 0x2160,
            CH6_CFG_Clr            = 0x217C,
            CH6_LOOP_Clr           = 0x2180,
            CH6_CTRL_Clr           = 0x2184,
            CH6_SRC_Clr            = 0x2188,
            CH6_DST_Clr            = 0x218C,
            CH6_LINK_Clr           = 0x2190,
            CH7_CFG_Clr            = 0x21AC,
            CH7_LOOP_Clr           = 0x21B0,
            CH7_CTRL_Clr           = 0x21B4,
            CH7_SRC_Clr            = 0x21B8,
            CH7_DST_Clr            = 0x21BC,
            CH7_LINK_Clr           = 0x21C0,
            CH8_CFG_Clr            = 0x21DC,
            CH8_LOOP_Clr           = 0x21E0,
            CH8_CTRL_Clr           = 0x21E4,
            CH8_SRC_Clr            = 0x21E8,
            CH8_DST_Clr            = 0x21EC,
            CH8_LINK_Clr           = 0x21F0,
            CH9_CFG_Clr            = 0x220C,
            CH9_LOOP_Clr           = 0x2210,
            CH9_CTRL_Clr           = 0x2214,
            CH9_SRC_Clr            = 0x2218,
            CH9_DST_Clr            = 0x221C,
            CH9_LINK_Clr           = 0x2220,
            CH10_CFG_Clr           = 0x223C,
            CH10_LOOP_Clr          = 0x2240,
            CH10_CTRL_Clr          = 0x2244,
            CH10_SRC_Clr           = 0x2248,
            CH10_DST_Clr           = 0x224C,
            CH10_LINK_Clr          = 0x2250,
            CH11_CFG_Clr           = 0x226C,
            CH11_LOOP_Clr          = 0x2270,
            CH11_CTRL_Clr          = 0x2274,
            CH11_SRC_Clr           = 0x2278,
            CH11_DST_Clr           = 0x227C,
            CH11_LINK_Clr          = 0x2280,
            CH12_CFG_Clr           = 0x229C,
            CH12_LOOP_Clr          = 0x22A0,
            CH12_CTRL_Clr          = 0x22A4,
            CH12_SRC_Clr           = 0x22A8,
            CH12_DST_Clr           = 0x22AC,
            CH12_LINK_Clr          = 0x22B0,
            CH13_CFG_Clr           = 0x22CC,
            CH13_LOOP_Clr          = 0x22D0,
            CH13_CTRL_Clr          = 0x22D4,
            CH13_SRC_Clr           = 0x22D8,
            CH13_DST_Clr           = 0x22DC,
            CH13_LINK_Clr          = 0x22E0,
            CH14_CFG_Clr           = 0x22FC,
            CH14_LOOP_Clr          = 0x2300,
            CH14_CTRL_Clr          = 0x2304,
            CH14_SRC_Clr           = 0x2308,
            CH14_DST_Clr           = 0x230C,
            CH14_LINK_Clr          = 0x2310,
            CH15_CFG_Clr           = 0x232C,
            CH15_LOOP_Clr          = 0x2330,
            CH15_CTRL_Clr          = 0x2334,
            CH15_SRC_Clr           = 0x2338,
            CH15_DST_Clr           = 0x233C,
            CH15_LINK_Clr          = 0x2340,
            CH16_CFG_Clr           = 0x235C,
            CH16_LOOP_Clr          = 0x2360,
            CH16_CTRL_Clr          = 0x2364,
            CH16_SRC_Clr           = 0x2368,
            CH16_DST_Clr           = 0x236C,
            CH16_LINK_Clr          = 0x2370,
            CH17_CFG_Clr           = 0x238C,
            CH17_LOOP_Clr          = 0x2390,
            CH17_CTRL_Clr          = 0x2394,
            CH17_SRC_Clr           = 0x2398,
            CH17_DST_Clr           = 0x239C,
            CH17_LINK_Clr          = 0x23A0,
            CH18_CFG_Clr           = 0x23BC,
            CH18_LOOP_Clr          = 0x23C0,
            CH18_CTRL_Clr          = 0x23C4,
            CH18_SRC_Clr           = 0x23C8,
            CH18_DST_Clr           = 0x23CC,
            CH18_LINK_Clr          = 0x23D0,
            CH19_CFG_Clr           = 0x23EC,
            CH19_LOOP_Clr          = 0x23F0,
            CH19_CTRL_Clr          = 0x23F4,
            CH19_SRC_Clr           = 0x23F8,
            CH19_DST_Clr           = 0x23FC,
            CH19_LINK_Clr          = 0x2400,
            CH20_CFG_Clr           = 0x241C,
            CH20_LOOP_Clr          = 0x2420,
            CH20_CTRL_Clr          = 0x2424,
            CH20_SRC_Clr           = 0x2428,
            CH20_DST_Clr           = 0x242C,
            CH20_LINK_Clr          = 0x2430,
            CH21_CFG_Clr           = 0x244C,
            CH21_LOOP_Clr          = 0x2450,
            CH21_CTRL_Clr          = 0x2454,
            CH21_SRC_Clr           = 0x2458,
            CH21_DST_Clr           = 0x245C,
            CH21_LINK_Clr          = 0x2460,
            CH22_CFG_Clr           = 0x247C,
            CH22_LOOP_Clr          = 0x2480,
            CH22_CTRL_Clr          = 0x2484,
            CH22_SRC_Clr           = 0x2488,
            CH22_DST_Clr           = 0x248C,
            CH22_LINK_Clr          = 0x2490,
            CH23_CFG_Clr           = 0x24AC,
            CH23_LOOP_Clr          = 0x24B0,
            CH23_CTRL_Clr          = 0x24B4,
            CH23_SRC_Clr           = 0x24B8,
            CH23_DST_Clr           = 0x24BC,
            CH23_LINK_Clr          = 0x24C0,
            CH24_CFG_Clr           = 0x24DC,
            CH24_LOOP_Clr          = 0x24E0,
            CH24_CTRL_Clr          = 0x24E4,
            CH24_SRC_Clr           = 0x24E8,
            CH24_DST_Clr           = 0x24EC,
            CH24_LINK_Clr          = 0x24F0,
            CH25_CFG_Clr           = 0x250C,
            CH25_LOOP_Clr          = 0x2510,
            CH25_CTRL_Clr          = 0x2514,
            CH25_SRC_Clr           = 0x2518,
            CH25_DST_Clr           = 0x251C,
            CH25_LINK_Clr          = 0x2520,
            CH26_CFG_Clr           = 0x253C,
            CH26_LOOP_Clr          = 0x2540,
            CH26_CTRL_Clr          = 0x2544,
            CH26_SRC_Clr           = 0x2548,
            CH26_DST_Clr           = 0x254C,
            CH26_LINK_Clr          = 0x2550,
            CH27_CFG_Clr           = 0x256C,
            CH27_LOOP_Clr          = 0x2570,
            CH27_CTRL_Clr          = 0x2574,
            CH27_SRC_Clr           = 0x2578,
            CH27_DST_Clr           = 0x257C,
            CH27_LINK_Clr          = 0x2580,
            CH28_CFG_Clr           = 0x259C,
            CH28_LOOP_Clr          = 0x25A0,
            CH28_CTRL_Clr          = 0x25A4,
            CH28_SRC_Clr           = 0x25A8,
            CH28_DST_Clr           = 0x25AC,
            CH28_LINK_Clr          = 0x25B0,
            CH29_CFG_Clr           = 0x25CC,
            CH29_LOOP_Clr          = 0x25D0,
            CH29_CTRL_Clr          = 0x25D4,
            CH29_SRC_Clr           = 0x25D8,
            CH29_DST_Clr           = 0x25DC,
            CH29_LINK_Clr          = 0x25E0,
            CH30_CFG_Clr           = 0x25FC,
            CH30_LOOP_Clr          = 0x2600,
            CH30_CTRL_Clr          = 0x2604,
            CH30_SRC_Clr           = 0x2608,
            CH30_DST_Clr           = 0x260C,
            CH30_LINK_Clr          = 0x2610,
            // Toggle registers
            IPVERSION_Tgl          = 0x3000,
            EN_Tgl                 = 0x3004,
            CTRL_Tgl               = 0x3008,
            STATUS_Tgl             = 0x300C,
            SYNCSWSET_Tgl          = 0x3010,
            SYNCSWCLR_Tgl          = 0x3014,
            SYNCHWEN_Tgl           = 0x3018,
            SYNCHWSEL_Tgl          = 0x301C,
            SYNCSTATUS_Tgl         = 0x3020,
            CHEN_Tgl               = 0x3024,
            CHDIS_Tgl              = 0x3028,
            CHSTATUS_Tgl           = 0x302C,
            CHBUSY_Tgl             = 0x3030,
            CHDONE_Tgl             = 0x3034,
            DBGHALT_Tgl            = 0x3038,
            SWREQ_Tgl              = 0x303C,
            REQDIS_Tgl             = 0x3040,
            REQPEND_Tgl            = 0x3044,
            LINKLOAD_Tgl           = 0x3048,
            REQCLEAR_Tgl           = 0x304C,
            IF_Tgl                 = 0x3050,
            IEN_Tgl                = 0x3054,
            CH0_CFG_Tgl            = 0x305C,
            CH0_LOOP_Tgl           = 0x3060,
            CH0_CTRL_Tgl           = 0x3064,
            CH0_SRC_Tgl            = 0x3068,
            CH0_DST_Tgl            = 0x306C,
            CH0_LINK_Tgl           = 0x3070,
            CH1_CFG_Tgl            = 0x308C,
            CH1_LOOP_Tgl           = 0x3090,
            CH1_CTRL_Tgl           = 0x3094,
            CH1_SRC_Tgl            = 0x3098,
            CH1_DST_Tgl            = 0x309C,
            CH1_LINK_Tgl           = 0x30A0,
            CH2_CFG_Tgl            = 0x30BC,
            CH2_LOOP_Tgl           = 0x30C0,
            CH2_CTRL_Tgl           = 0x30C4,
            CH2_SRC_Tgl            = 0x30C8,
            CH2_DST_Tgl            = 0x30CC,
            CH2_LINK_Tgl           = 0x30D0,
            CH3_CFG_Tgl            = 0x30EC,
            CH3_LOOP_Tgl           = 0x30F0,
            CH3_CTRL_Tgl           = 0x30F4,
            CH3_SRC_Tgl            = 0x30F8,
            CH3_DST_Tgl            = 0x30FC,
            CH3_LINK_Tgl           = 0x3100,
            CH4_CFG_Tgl            = 0x311C,
            CH4_LOOP_Tgl           = 0x3120,
            CH4_CTRL_Tgl           = 0x3124,
            CH4_SRC_Tgl            = 0x3128,
            CH4_DST_Tgl            = 0x312C,
            CH4_LINK_Tgl           = 0x3130,
            CH5_CFG_Tgl            = 0x314C,
            CH5_LOOP_Tgl           = 0x3150,
            CH5_CTRL_Tgl           = 0x3154,
            CH5_SRC_Tgl            = 0x3158,
            CH5_DST_Tgl            = 0x315C,
            CH5_LINK_Tgl           = 0x3160,
            CH6_CFG_Tgl            = 0x317C,
            CH6_LOOP_Tgl           = 0x3180,
            CH6_CTRL_Tgl           = 0x3184,
            CH6_SRC_Tgl            = 0x3188,
            CH6_DST_Tgl            = 0x318C,
            CH6_LINK_Tgl           = 0x3190,
            CH7_CFG_Tgl            = 0x31AC,
            CH7_LOOP_Tgl           = 0x31B0,
            CH7_CTRL_Tgl           = 0x31B4,
            CH7_SRC_Tgl            = 0x31B8,
            CH7_DST_Tgl            = 0x31BC,
            CH7_LINK_Tgl           = 0x31C0,
            CH8_CFG_Tgl            = 0x31DC,
            CH8_LOOP_Tgl           = 0x31E0,
            CH8_CTRL_Tgl           = 0x31E4,
            CH8_SRC_Tgl            = 0x31E8,
            CH8_DST_Tgl            = 0x31EC,
            CH8_LINK_Tgl           = 0x31F0,
            CH9_CFG_Tgl            = 0x320C,
            CH9_LOOP_Tgl           = 0x3210,
            CH9_CTRL_Tgl           = 0x3214,
            CH9_SRC_Tgl            = 0x3218,
            CH9_DST_Tgl            = 0x321C,
            CH9_LINK_Tgl           = 0x3220,
            CH10_CFG_Tgl           = 0x323C,
            CH10_LOOP_Tgl          = 0x3240,
            CH10_CTRL_Tgl          = 0x3244,
            CH10_SRC_Tgl           = 0x3248,
            CH10_DST_Tgl           = 0x324C,
            CH10_LINK_Tgl          = 0x3250,
            CH11_CFG_Tgl           = 0x326C,
            CH11_LOOP_Tgl          = 0x3270,
            CH11_CTRL_Tgl          = 0x3274,
            CH11_SRC_Tgl           = 0x3278,
            CH11_DST_Tgl           = 0x327C,
            CH11_LINK_Tgl          = 0x3280,
            CH12_CFG_Tgl           = 0x329C,
            CH12_LOOP_Tgl          = 0x32A0,
            CH12_CTRL_Tgl          = 0x32A4,
            CH12_SRC_Tgl           = 0x32A8,
            CH12_DST_Tgl           = 0x32AC,
            CH12_LINK_Tgl          = 0x32B0,
            CH13_CFG_Tgl           = 0x32CC,
            CH13_LOOP_Tgl          = 0x32D0,
            CH13_CTRL_Tgl          = 0x32D4,
            CH13_SRC_Tgl           = 0x32D8,
            CH13_DST_Tgl           = 0x32DC,
            CH13_LINK_Tgl          = 0x32E0,
            CH14_CFG_Tgl           = 0x32FC,
            CH14_LOOP_Tgl          = 0x3300,
            CH14_CTRL_Tgl          = 0x3304,
            CH14_SRC_Tgl           = 0x3308,
            CH14_DST_Tgl           = 0x330C,
            CH14_LINK_Tgl          = 0x3310,
            CH15_CFG_Tgl           = 0x332C,
            CH15_LOOP_Tgl          = 0x3330,
            CH15_CTRL_Tgl          = 0x3334,
            CH15_SRC_Tgl           = 0x3338,
            CH15_DST_Tgl           = 0x333C,
            CH15_LINK_Tgl          = 0x3340,
            CH16_CFG_Tgl           = 0x335C,
            CH16_LOOP_Tgl          = 0x3360,
            CH16_CTRL_Tgl          = 0x3364,
            CH16_SRC_Tgl           = 0x3368,
            CH16_DST_Tgl           = 0x336C,
            CH16_LINK_Tgl          = 0x3370,
            CH17_CFG_Tgl           = 0x338C,
            CH17_LOOP_Tgl          = 0x3390,
            CH17_CTRL_Tgl          = 0x3394,
            CH17_SRC_Tgl           = 0x3398,
            CH17_DST_Tgl           = 0x339C,
            CH17_LINK_Tgl          = 0x33A0,
            CH18_CFG_Tgl           = 0x33BC,
            CH18_LOOP_Tgl          = 0x33C0,
            CH18_CTRL_Tgl          = 0x33C4,
            CH18_SRC_Tgl           = 0x33C8,
            CH18_DST_Tgl           = 0x33CC,
            CH18_LINK_Tgl          = 0x33D0,
            CH19_CFG_Tgl           = 0x33EC,
            CH19_LOOP_Tgl          = 0x33F0,
            CH19_CTRL_Tgl          = 0x33F4,
            CH19_SRC_Tgl           = 0x33F8,
            CH19_DST_Tgl           = 0x33FC,
            CH19_LINK_Tgl          = 0x3400,
            CH20_CFG_Tgl           = 0x341C,
            CH20_LOOP_Tgl          = 0x3420,
            CH20_CTRL_Tgl          = 0x3424,
            CH20_SRC_Tgl           = 0x3428,
            CH20_DST_Tgl           = 0x342C,
            CH20_LINK_Tgl          = 0x3430,
            CH21_CFG_Tgl           = 0x344C,
            CH21_LOOP_Tgl          = 0x3450,
            CH21_CTRL_Tgl          = 0x3454,
            CH21_SRC_Tgl           = 0x3458,
            CH21_DST_Tgl           = 0x345C,
            CH21_LINK_Tgl          = 0x3460,
            CH22_CFG_Tgl           = 0x347C,
            CH22_LOOP_Tgl          = 0x3480,
            CH22_CTRL_Tgl          = 0x3484,
            CH22_SRC_Tgl           = 0x3488,
            CH22_DST_Tgl           = 0x348C,
            CH22_LINK_Tgl          = 0x3490,
            CH23_CFG_Tgl           = 0x34AC,
            CH23_LOOP_Tgl          = 0x34B0,
            CH23_CTRL_Tgl          = 0x34B4,
            CH23_SRC_Tgl           = 0x34B8,
            CH23_DST_Tgl           = 0x34BC,
            CH23_LINK_Tgl          = 0x34C0,
            CH24_CFG_Tgl           = 0x34DC,
            CH24_LOOP_Tgl          = 0x34E0,
            CH24_CTRL_Tgl          = 0x34E4,
            CH24_SRC_Tgl           = 0x34E8,
            CH24_DST_Tgl           = 0x34EC,
            CH24_LINK_Tgl          = 0x34F0,
            CH25_CFG_Tgl           = 0x350C,
            CH25_LOOP_Tgl          = 0x3510,
            CH25_CTRL_Tgl          = 0x3514,
            CH25_SRC_Tgl           = 0x3518,
            CH25_DST_Tgl           = 0x351C,
            CH25_LINK_Tgl          = 0x3520,
            CH26_CFG_Tgl           = 0x353C,
            CH26_LOOP_Tgl          = 0x3540,
            CH26_CTRL_Tgl          = 0x3544,
            CH26_SRC_Tgl           = 0x3548,
            CH26_DST_Tgl           = 0x354C,
            CH26_LINK_Tgl          = 0x3550,
            CH27_CFG_Tgl           = 0x356C,
            CH27_LOOP_Tgl          = 0x3570,
            CH27_CTRL_Tgl          = 0x3574,
            CH27_SRC_Tgl           = 0x3578,
            CH27_DST_Tgl           = 0x357C,
            CH27_LINK_Tgl          = 0x3580,
            CH28_CFG_Tgl           = 0x359C,
            CH28_LOOP_Tgl          = 0x35A0,
            CH28_CTRL_Tgl          = 0x35A4,
            CH28_SRC_Tgl           = 0x35A8,
            CH28_DST_Tgl           = 0x35AC,
            CH28_LINK_Tgl          = 0x35B0,
            CH29_CFG_Tgl           = 0x35CC,
            CH29_LOOP_Tgl          = 0x35D0,
            CH29_CTRL_Tgl          = 0x35D4,
            CH29_SRC_Tgl           = 0x35D8,
            CH29_DST_Tgl           = 0x35DC,
            CH29_LINK_Tgl          = 0x35E0,
            CH30_CFG_Tgl           = 0x35FC,
            CH30_LOOP_Tgl          = 0x3600,
            CH30_CTRL_Tgl          = 0x3604,
            CH30_SRC_Tgl           = 0x3608,
            CH30_DST_Tgl           = 0x360C,
            CH30_LINK_Tgl          = 0x3610,            
        }

        private enum LdmaXbarRegisters : long
        {
            XBAR_IPVERSION         = 0x0000,
            XBAR_CH0_REQSEL        = 0x0004,
            XBAR_CH1_REQSEL        = 0x0008,
            XBAR_CH2_REQSEL        = 0x000C,
            XBAR_CH3_REQSEL        = 0x0010,
            XBAR_CH4_REQSEL        = 0x0014,
            XBAR_CH5_REQSEL        = 0x0018,
            XBAR_CH6_REQSEL        = 0x001C,
            XBAR_CH7_REQSEL        = 0x0020,
            XBAR_CH8_REQSEL        = 0x0024,
            XBAR_CH9_REQSEL        = 0x0028,
            XBAR_CH10_REQSEL       = 0x002C,
            XBAR_CH11_REQSEL       = 0x0030,
            XBAR_CH12_REQSEL       = 0x0034,
            XBAR_CH13_REQSEL       = 0x0038,
            XBAR_CH14_REQSEL       = 0x003C,
            XBAR_CH15_REQSEL       = 0x0040,
            XBAR_CH16_REQSEL       = 0x0044,
            XBAR_CH17_REQSEL       = 0x0048,
            XBAR_CH18_REQSEL       = 0x004C,
            XBAR_CH19_REQSEL       = 0x0050,
            XBAR_CH20_REQSEL       = 0x0054,
            XBAR_CH21_REQSEL       = 0x0058,
            XBAR_CH22_REQSEL       = 0x005C,
            XBAR_CH23_REQSEL       = 0x0060,
            XBAR_CH24_REQSEL       = 0x0064,
            XBAR_CH25_REQSEL       = 0x0068,
            XBAR_CH26_REQSEL       = 0x006C,
            XBAR_CH27_REQSEL       = 0x0070,
            XBAR_CH28_REQSEL       = 0x0074,
            XBAR_CH29_REQSEL       = 0x0078,
            XBAR_CH30_REQSEL       = 0x007C,
            // Set registers
            XBAR_IPVERSION_Set     = 0x1000,
            XBAR_CH0_REQSEL_Set    = 0x1004,
            XBAR_CH1_REQSEL_Set    = 0x1008,
            XBAR_CH2_REQSEL_Set    = 0x100C,
            XBAR_CH3_REQSEL_Set    = 0x1010,
            XBAR_CH4_REQSEL_Set    = 0x1014,
            XBAR_CH5_REQSEL_Set    = 0x1018,
            XBAR_CH6_REQSEL_Set    = 0x101C,
            XBAR_CH7_REQSEL_Set    = 0x1020,
            XBAR_CH8_REQSEL_Set    = 0x1024,
            XBAR_CH9_REQSEL_Set    = 0x1028,
            XBAR_CH10_REQSEL_Set   = 0x102C,
            XBAR_CH11_REQSEL_Set   = 0x1030,
            XBAR_CH12_REQSEL_Set   = 0x1034,
            XBAR_CH13_REQSEL_Set   = 0x1038,
            XBAR_CH14_REQSEL_Set   = 0x103C,
            XBAR_CH15_REQSEL_Set   = 0x1040,
            XBAR_CH16_REQSEL_Set   = 0x1044,
            XBAR_CH17_REQSEL_Set   = 0x1048,
            XBAR_CH18_REQSEL_Set   = 0x104C,
            XBAR_CH19_REQSEL_Set   = 0x1050,
            XBAR_CH20_REQSEL_Set   = 0x1054,
            XBAR_CH21_REQSEL_Set   = 0x1058,
            XBAR_CH22_REQSEL_Set   = 0x105C,
            XBAR_CH23_REQSEL_Set   = 0x1060,
            XBAR_CH24_REQSEL_Set   = 0x1064,
            XBAR_CH25_REQSEL_Set   = 0x1068,
            XBAR_CH26_REQSEL_Set   = 0x106C,
            XBAR_CH27_REQSEL_Set   = 0x1070,
            XBAR_CH28_REQSEL_Set   = 0x1074,
            XBAR_CH29_REQSEL_Set   = 0x1078,
            XBAR_CH30_REQSEL_Set   = 0x107C,
            // Clear registers
            XBAR_IPVERSION_Clr     = 0x2000,
            XBAR_CH0_REQSEL_Clr    = 0x2004,
            XBAR_CH1_REQSEL_Clr    = 0x2008,
            XBAR_CH2_REQSEL_Clr    = 0x200C,
            XBAR_CH3_REQSEL_Clr    = 0x2010,
            XBAR_CH4_REQSEL_Clr    = 0x2014,
            XBAR_CH5_REQSEL_Clr    = 0x2018,
            XBAR_CH6_REQSEL_Clr    = 0x201C,
            XBAR_CH7_REQSEL_Clr    = 0x2020,
            XBAR_CH8_REQSEL_Clr    = 0x2024,
            XBAR_CH9_REQSEL_Clr    = 0x2028,
            XBAR_CH10_REQSEL_Clr   = 0x202C,
            XBAR_CH11_REQSEL_Clr   = 0x2030,
            XBAR_CH12_REQSEL_Clr   = 0x2034,
            XBAR_CH13_REQSEL_Clr   = 0x2038,
            XBAR_CH14_REQSEL_Clr   = 0x203C,
            XBAR_CH15_REQSEL_Clr   = 0x2040,
            XBAR_CH16_REQSEL_Clr   = 0x2044,
            XBAR_CH17_REQSEL_Clr   = 0x2048,
            XBAR_CH18_REQSEL_Clr   = 0x204C,
            XBAR_CH19_REQSEL_Clr   = 0x2050,
            XBAR_CH20_REQSEL_Clr   = 0x2054,
            XBAR_CH21_REQSEL_Clr   = 0x2058,
            XBAR_CH22_REQSEL_Clr   = 0x205C,
            XBAR_CH23_REQSEL_Clr   = 0x2060,
            XBAR_CH24_REQSEL_Clr   = 0x2064,
            XBAR_CH25_REQSEL_Clr   = 0x2068,
            XBAR_CH26_REQSEL_Clr   = 0x206C,
            XBAR_CH27_REQSEL_Clr   = 0x2070,
            XBAR_CH28_REQSEL_Clr   = 0x2074,
            XBAR_CH29_REQSEL_Clr   = 0x2078,
            XBAR_CH30_REQSEL_Clr   = 0x207C,
            // Toggle registers
            XBAR_IPVERSION_Tgl     = 0x3000,
            XBAR_CH0_REQSEL_Tgl    = 0x3004,
            XBAR_CH1_REQSEL_Tgl    = 0x3008,
            XBAR_CH2_REQSEL_Tgl    = 0x300C,
            XBAR_CH3_REQSEL_Tgl    = 0x3010,
            XBAR_CH4_REQSEL_Tgl    = 0x3014,
            XBAR_CH5_REQSEL_Tgl    = 0x3018,
            XBAR_CH6_REQSEL_Tgl    = 0x301C,
            XBAR_CH7_REQSEL_Tgl    = 0x3020,
            XBAR_CH8_REQSEL_Tgl    = 0x3024,
            XBAR_CH9_REQSEL_Tgl    = 0x3028,
            XBAR_CH10_REQSEL_Tgl   = 0x302C,
            XBAR_CH11_REQSEL_Tgl   = 0x3030,
            XBAR_CH12_REQSEL_Tgl   = 0x3034,
            XBAR_CH13_REQSEL_Tgl   = 0x3038,
            XBAR_CH14_REQSEL_Tgl   = 0x303C,
            XBAR_CH15_REQSEL_Tgl   = 0x3040,
            XBAR_CH16_REQSEL_Tgl   = 0x3044,
            XBAR_CH17_REQSEL_Tgl   = 0x3048,
            XBAR_CH18_REQSEL_Tgl   = 0x304C,
            XBAR_CH19_REQSEL_Tgl   = 0x3050,
            XBAR_CH20_REQSEL_Tgl   = 0x3054,
            XBAR_CH21_REQSEL_Tgl   = 0x3058,
            XBAR_CH22_REQSEL_Tgl   = 0x305C,
            XBAR_CH23_REQSEL_Tgl   = 0x3060,
            XBAR_CH24_REQSEL_Tgl   = 0x3064,
            XBAR_CH25_REQSEL_Tgl   = 0x3068,
            XBAR_CH26_REQSEL_Tgl   = 0x306C,
            XBAR_CH27_REQSEL_Tgl   = 0x3070,
            XBAR_CH28_REQSEL_Tgl   = 0x3074,
            XBAR_CH29_REQSEL_Tgl   = 0x3078,
            XBAR_CH30_REQSEL_Tgl   = 0x307C,            
        }


        private class Channel
        {
            public Channel(EFR32MG24_LDMA parent, int index)
            {
                this.parent = parent;
                Index = index;
                descriptor = default(Descriptor);

                PeripheralRequestSelectRegister = new DoubleWordRegister(parent)
                    .WithEnumField<DoubleWordRegister, SignalSelect>(0, 4, out signalSelect, name: "SIGSEL")
                    .WithReservedBits(4, 12)
                    .WithEnumField<DoubleWordRegister, SourceSelect>(16, 6, out sourceSelect, name: "SOURCESEL")
                    .WithReservedBits(22, 10)
                ;
                ConfigurationRegister = new DoubleWordRegister(parent)
                    .WithReservedBits(0, 16)
                    .WithEnumField<DoubleWordRegister, ArbitrationSlotNumberMode>(16, 2, out arbitrationSlotNumberSelect, name: "ARBSLOTS")
                    .WithReservedBits(18, 2)
                    .WithEnumField<DoubleWordRegister, Sign>(20, 1, out sourceAddressIncrementSign, name: "SRCINCSIGN")
                    .WithEnumField<DoubleWordRegister, Sign>(21, 1, out destinationAddressIncrementSign, name: "DSTINCSIGN")
                    .WithReservedBits(22, 10)
                ;
                LoopCounterRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 8, out loopCounter, name: "LOOPCNT")
                    .WithReservedBits(8, 24)
                ;
                DescriptorControlWordRegister = new DoubleWordRegister(parent)
                    .WithEnumField<DoubleWordRegister, StructureType>(0, 2, FieldMode.Read,
                        valueProviderCallback: _ => descriptor.structureType,
                        name: "STRUCTTYPE")
                    .WithReservedBits(2, 1)
                    .WithFlag(3, FieldMode.Set,
                        writeCallback: (_, value) => descriptor.structureTransferRequest = value,
                        name: "STRUCTREQ")
                    .WithValueField(4, 11,
                        writeCallback: (_, value) => descriptor.transferCount = (ushort)value,
                        valueProviderCallback: _ => descriptor.transferCount,
                        name: "XFERCNT")
                    .WithFlag(15,
                        writeCallback: (_, value) => descriptor.byteSwap = value,
                        valueProviderCallback: _ => descriptor.byteSwap,
                        name: "BYTESWAP")
                    .WithEnumField<DoubleWordRegister, BlockSizeMode>(16, 4,
                        writeCallback: (_, value) => descriptor.blockSize = value,
                        valueProviderCallback: _ => descriptor.blockSize,
                        name: "BLOCKSIZE")
                    .WithFlag(20,
                        writeCallback: (_, value) => descriptor.operationDoneInterruptFlagSetEnable = value,
                        valueProviderCallback: _ => descriptor.operationDoneInterruptFlagSetEnable,
                        name: "DONEIEN")
                    .WithEnumField<DoubleWordRegister, RequestTransferMode>(21, 1,
                        writeCallback: (_, value) => descriptor.requestTransferModeSelect = value,
                        valueProviderCallback: _ => descriptor.requestTransferModeSelect,
                        name: "REQMODE")
                    .WithFlag(22,
                        writeCallback: (_, value) => descriptor.decrementLoopCount = value,
                        valueProviderCallback: _ => descriptor.decrementLoopCount,
                        name: "DECLOOPCNT")
                    .WithFlag(23,
                        writeCallback: (_, value) => descriptor.ignoreSingleRequests = value,
                        valueProviderCallback: _ => descriptor.ignoreSingleRequests,
                        name: "IGNORESREQ")
                    .WithEnumField<DoubleWordRegister, IncrementMode>(24, 2,
                        writeCallback: (_, value) => descriptor.sourceIncrement = value,
                        valueProviderCallback: _ => descriptor.sourceIncrement,
                        name: "SRCINC")
                    .WithEnumField<DoubleWordRegister, SizeMode>(26, 2,
                        writeCallback: (_, value) => descriptor.size = value,
                        valueProviderCallback: _ => descriptor.size,
                        name: "SIZE")
                    .WithEnumField<DoubleWordRegister, IncrementMode>(28, 2,
                        writeCallback: (_, value) => descriptor.destinationIncrement = value,
                        valueProviderCallback: _ => descriptor.destinationIncrement,
                        name: "DSTINC")
                    .WithEnumField<DoubleWordRegister, AddressingMode>(30, 1, FieldMode.Read,
                        valueProviderCallback: _ => descriptor.sourceAddressingMode,
                        name: "SRCMODE")
                    .WithEnumField<DoubleWordRegister, AddressingMode>(31, 1, FieldMode.Read,
                        valueProviderCallback: _ => descriptor.destinationAddressingMode,
                        name: "DSTMODE")
                    .WithChangeCallback((_, __) => { if(descriptor.structureTransferRequest) LinkLoad(); })
                ;
                DescriptorSourceDataAddressRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) => descriptor.sourceAddress = (uint)value,
                        valueProviderCallback: _ => descriptor.sourceAddress,
                        name: "SRCADDR")
                ;
                DescriptorDestinationDataAddressRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) => descriptor.destinationAddress = (uint)value,
                        valueProviderCallback: _ => descriptor.destinationAddress,
                        name: "DSTADDR")
                ;
                DescriptorLinkStructureAddressRegister = new DoubleWordRegister(parent)
                    .WithEnumField<DoubleWordRegister, AddressingMode>(0, 1, FieldMode.Read,
                        valueProviderCallback: _ => descriptor.linkMode,
                        name: "LINKMODE")
                    .WithFlag(1,
                        writeCallback: (_, value) => descriptor.link = value,
                        valueProviderCallback: _ => descriptor.link,
                        name: "LINK")
                    .WithValueField(2, 30,
                        writeCallback: (_, value) => descriptor.linkAddress = (uint)value,
                        valueProviderCallback: _ => descriptor.linkAddress,
                        name: "LINKADDR")
                ;

                pullTimer = new LimitTimer(parent.machine.ClockSource, 1000000, null, $"pullTimer-{Index}", 15, Direction.Ascending, false, WorkMode.Periodic, true, true);
                pullTimer.LimitReached += delegate
                {
                    if(!RequestDisable)
                    {
                        StartTransferInner();
                    }
                    if(!SignalIsOn || !ShouldPullSignal)
                    {
                        pullTimer.Enabled = false;
                    }
                };
            }

            public void StartFromSignal()
            {
                if(!RequestDisable)
                {
                    StartTransfer();
                }
            }

            public void LinkLoad()
            {
                LoadDescriptor();
                if(!RequestDisable && (descriptor.structureTransferRequest || SignalIsOn))
                {
                    StartTransfer();
                }
            }

            public void StartTransfer()
            {
                if(ShouldPullSignal)
                {
                    pullTimer.Enabled = true;
                }
                else
                {
                    StartTransferInner();
                }
            }

            public void Reset()
            {
                descriptor = default(Descriptor);
                pullTimer.Reset();
                DoneInterrupt = false;
                DoneInterruptEnable = false;
                descriptorAddress = null;
                requestDisable = false;
                enabled = false;
                done = false;
            }

            public int Index { get; }

            public SignalSelect Signal => signalSelect.Value;
            public SourceSelect Source => sourceSelect.Value;
            public bool IgnoreSingleRequests => descriptor.ignoreSingleRequests;
            public bool DoneInterrupt { get; set; }
            public bool DoneInterruptEnable { get; set; }
            public bool IRQ => DoneInterrupt && DoneInterruptEnable;

            public DoubleWordRegister PeripheralRequestSelectRegister { get; }
            public DoubleWordRegister ConfigurationRegister { get; }
            public DoubleWordRegister LoopCounterRegister { get; }
            public DoubleWordRegister DescriptorControlWordRegister { get; }
            public DoubleWordRegister DescriptorSourceDataAddressRegister { get; }
            public DoubleWordRegister DescriptorDestinationDataAddressRegister { get; }
            public DoubleWordRegister DescriptorLinkStructureAddressRegister { get; }

            public bool Enabled
            {
                get
                {
                    return enabled;
                }
                set
                {
                    if(enabled == value)
                    {
                        return;
                    }
                    enabled = value;
                    if(enabled)
                    {
                        Done = false;
                        StartTransfer();
                    }
                }
            }

            public bool Done
            {
                get
                {
                    return done;
                }

                set
                {
                    if (!done)
                    {
                        DoneInterrupt |= value && descriptor.operationDoneInterruptFlagSetEnable;
                    }
                    done = value;
                }
            }

            public bool Busy
            {
                get
                {
                    return isInProgress;
                }
            }

            public bool RequestDisable
            {
                get
                {
                    return requestDisable;
                }

                set
                {
                    bool oldValue = requestDisable;
                    requestDisable = value;

                    if(oldValue && !value)
                    {
                        if(SignalIsOn)
                        {
                            StartTransfer();
                        }
                    }
                }
            }

            private void StartTransferInner()
            {
                if(isInProgress || Done)
                {
                    return;
                }
                
                isInProgress = true;
                var loaded = false;
                do
                {
                    loaded = false;
                    Transfer();
                    if(Done && descriptor.link)
                    {
                        loaded = true;
                        LoadDescriptor();
                        Done = false;
                    }
                }
                while((descriptor.structureTransferRequest && loaded) || (!Done && SignalIsOn));
                
                isInProgress = false;
                if (Done) 
                {
                    pullTimer.Enabled = false;
                }
            }

            private void LoadDescriptor()
            {
                var address = LinkStructureAddress;
                if(descriptorAddress.HasValue && descriptor.linkMode == AddressingMode.Relative)
                {
                    address += descriptorAddress.Value;
                }
                var data = parent.machine.SystemBus.ReadBytes(address, DescriptorSize);
                descriptorAddress = address;
                descriptor = Packet.Decode<Descriptor>(data);
#if DEBUG
                parent.Log(LogLevel.Noisy, "Channel #{0} data {1}", Index, BitConverter.ToString(data));
                parent.Log(LogLevel.Debug, "Channel #{0} Loaded {1}", Index, descriptor.PrettyString);
#endif
            }

            private void Transfer()
            {
                switch(descriptor.structureType)
                {
                    case StructureType.Transfer:
                        var request = new Request(
                            source: new Place(descriptor.sourceAddress),
                            destination: new Place(descriptor.destinationAddress),
                            size: Bytes,
                            readTransferType: SizeAsTransferType,
                            writeTransferType: SizeAsTransferType,
                            sourceIncrementStep: SourceIncrement,
                            destinationIncrementStep: DestinationIncrement
                        );
                        parent.Log(LogLevel.Debug, "Channel #{0} Performing Transfer", Index);
                        parent.engine.IssueCopy(request);
                        if(descriptor.requestTransferModeSelect == RequestTransferMode.Block)
                        {
                            var blockSizeMultiplier = Math.Min(TransferCount, BlockSizeMultiplier);
                            parent.Log(LogLevel.Debug, "Channel #{0} TransferCount={1} BlockSizeMultiplier={2}", Index, TransferCount, BlockSizeMultiplier);
                            if(blockSizeMultiplier == TransferCount)
                            {
                                Done = true;
                                descriptor.transferCount = 0;
                            }
                            else
                            {
                                descriptor.transferCount -= blockSizeMultiplier;
                            }
                            descriptor.sourceAddress += SourceIncrement * blockSizeMultiplier;
                            descriptor.destinationAddress += DestinationIncrement * blockSizeMultiplier;
                        }
                        else
                        {
                            Done = true;
                        }
                        break;
                    case StructureType.Synchronize:
                        parent.Log(LogLevel.Warning, "Channel #{0} Synchronize is not implemented.", Index);
                        break;
                    case StructureType.Write:
                        parent.Log(LogLevel.Warning, "Channel #{0} Write is not implemented.", Index);
                        break;
                    default:
                        parent.Log(LogLevel.Error, "Channel #{0} Invalid structure type value. No action was performed.", Index);
                        return;
                }
                parent.UpdateInterrupts();
            }

            private bool ShouldPullSignal
            {
                get
                {
                    // if this returns true for the selected source and signal
                    // then the signal will be periodically pulled instead of waiting
                    // for an rising edge
                    switch(Source)
                    {
                        case SourceSelect.None:
                            return false;
                        case SourceSelect.LDMAXBAR:
                            switch(Signal)
                            {
                                case SignalSelect.LDMAXBAR_DMA_PRSREQ0:
                                case SignalSelect.LDMAXBAR_DMA_PRSREQ1:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.IADC0:
                            switch(Signal)
                            {
                                case SignalSelect.IADC0_DMA_IADC_SCAN:
                                case SignalSelect.IADC0_DMA_IADC_SINGLE:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.VDAC0:
                        case SourceSelect.VDAC1:
                            switch(Signal)
                            {
                                case SignalSelect.VDAC0_DMA_CH0_REQ:
                                case SignalSelect.VDAC0_DMA_CH1_REQ:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.USART0:
                            switch(Signal)
                            {
                                case SignalSelect.USART0_DMA_RXDATAV:
                                    return false;
                                case SignalSelect.USART0_DMA_TXBL:
                                case SignalSelect.USART0_DMA_TXEMPTY:
                                    return true;
                                default:
                                    goto default;
                            }
                        case SourceSelect.EUSART0:                            
                        case SourceSelect.EUSART1:
                            switch(Signal)
                            {
                                case SignalSelect.EUSART0_DMA_RXFL:
                                    return false;
                                case SignalSelect.EUSART0_DMA_TXFL:
                                    return true;
                                default:
                                    goto default;
                            }
                        case SourceSelect.I2C0:
                        case SourceSelect.I2C1:
                            switch(Signal)
                            {
                                case SignalSelect.I2C0_DMA_RXDATAV:
                                    return false;
                                case SignalSelect.I2C0_DMA_TXBL:
                                    return true;
                                default:
                                    goto default;
                            }
                        case SourceSelect.TIMER0:
                        case SourceSelect.TIMER1:
                        case SourceSelect.TIMER2:
                        case SourceSelect.TIMER3:
                        case SourceSelect.TIMER4:
                            switch(Signal)
                            {
                                case SignalSelect.TIMER0_DMA_CC0:
                                case SignalSelect.TIMER0_DMA_CC1:
                                case SignalSelect.TIMER0_DMA_CC2:
                                case SignalSelect.TIMER0_DMA_UFOF:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.MSC:
                            switch(Signal)
                            {
                                case SignalSelect.MSC_DMA_WDATA:
                                    return false;
                                default:
                                    goto default;
                            }
                        default:
                            parent.Log(LogLevel.Error, "Channel #{0} Invalid Source (0x{1:X}) and Signal (0x{2:X}) pair.", Index, Source, Signal);
                            return false;
                    }
                }
            }

            private uint BlockSizeMultiplier
            {
                get
                {
                    switch(descriptor.blockSize)
                    {
                        case BlockSizeMode.Unit1:
                        case BlockSizeMode.Unit2:
                            return 1u << (byte)descriptor.blockSize;
                        case BlockSizeMode.Unit3:
                            return 3;
                        case BlockSizeMode.Unit4:
                            return 4;
                        case BlockSizeMode.Unit6:
                            return 6;
                        case BlockSizeMode.Unit8:
                            return 8;
                        case BlockSizeMode.Unit16:
                            return 16;
                        case BlockSizeMode.Unit32:
                        case BlockSizeMode.Unit64:
                        case BlockSizeMode.Unit128:
                        case BlockSizeMode.Unit256:
                        case BlockSizeMode.Unit512:
                        case BlockSizeMode.Unit1024:
                            return 1u << ((byte)descriptor.blockSize - 4);
                        case BlockSizeMode.All:
                            return TransferCount;
                        default:
                            parent.Log(LogLevel.Warning, "Channel #{0} Invalid Block Size Mode value.", Index);
                            return 0;
                    }
                }
            }

            private bool SignalIsOn
            {
                get
                {
                    var number = ((int)Source << 4) | (int)Signal;
                    return parent.signals.Contains(number) || (!IgnoreSingleRequests && parent.signals.Contains(number | 1 << 12));
                }
            }

            private uint TransferCount => (uint)descriptor.transferCount + 1;
            private ulong LinkStructureAddress => (ulong)descriptor.linkAddress << 2;

            private uint SourceIncrement => descriptor.sourceIncrement == IncrementMode.None ? 0u : ((1u << (byte)descriptor.size) << (byte)descriptor.sourceIncrement);
            private uint DestinationIncrement => descriptor.destinationIncrement == IncrementMode.None ? 0u : ((1u << (byte)descriptor.size) << (byte)descriptor.destinationIncrement);
            private TransferType SizeAsTransferType => (TransferType)(1 << (byte)descriptor.size);
            private int Bytes => (int)(descriptor.requestTransferModeSelect == RequestTransferMode.All ? TransferCount : Math.Min(TransferCount, BlockSizeMultiplier)) << (byte)descriptor.size;

            private Descriptor descriptor;
            private ulong? descriptorAddress;
            private bool requestDisable;
            private bool enabled;
            private bool done;

            // Accesses to sysubs may cause changes in signals, but we should ignore those during active transaction
            private bool isInProgress;

            private IEnumRegisterField<SignalSelect> signalSelect;
            private IEnumRegisterField<SourceSelect> sourceSelect;
            private IEnumRegisterField<ArbitrationSlotNumberMode> arbitrationSlotNumberSelect;
            private IEnumRegisterField<Sign> sourceAddressIncrementSign;
            private IEnumRegisterField<Sign> destinationAddressIncrementSign;
            private IValueRegisterField loopCounter;

            private readonly EFR32MG24_LDMA parent;
            private readonly LimitTimer pullTimer;

            protected readonly int DescriptorSize = Packet.CalculateLength<Descriptor>();

            private enum ArbitrationSlotNumberMode
            {
                One   = 0,
                Two   = 1,
                Four  = 2,
                Eight = 3,
            }

            private enum Sign
            {
                Positive = 0,
                Negative = 1,
            }

            protected enum StructureType : uint
            {
                Transfer    = 0,
                Synchronize = 1,
                Write       = 2,
            }

            protected enum BlockSizeMode : uint
            {
                Unit1    = 0,
                Unit2    = 1,
                Unit3    = 2,
                Unit4    = 3,
                Unit6    = 4,
                Unit8    = 5,
                Unit16   = 7,
                Unit32   = 9,
                Unit64   = 10,
                Unit128  = 11,
                Unit256  = 12,
                Unit512  = 13,
                Unit1024 = 14,
                All      = 15,
            }

            protected enum RequestTransferMode : uint
            {
                Block = 0,
                All   = 1,
            }

            protected enum IncrementMode : uint
            {
                One  = 0,
                Two  = 1,
                Four = 2,
                None = 3,
            }

            protected enum SizeMode : uint
            {
                Byte     = 0,
                HalfWord = 1,
                Word     = 2,
            }

            protected enum AddressingMode : uint
            {
                Absolute = 0,
                Relative = 1,
            }

            [LeastSignificantByteFirst]
            private struct Descriptor
            {
                public string PrettyString => $@"Descriptor {{
    structureType: {structureType},
    structureTransferRequest: {structureTransferRequest},
    transferCount: {transferCount + 1},
    byteSwap: {byteSwap},
    blockSize: {blockSize},
    operationDoneInterruptFlagSetEnable: {operationDoneInterruptFlagSetEnable},
    requestTransferModeSelect: {requestTransferModeSelect},
    decrementLoopCount: {decrementLoopCount},
    ignoreSingleRequests: {ignoreSingleRequests},
    sourceIncrement: {sourceIncrement},
    size: {size},
    destinationIncrement: {destinationIncrement},
    sourceAddressingMode: {sourceAddressingMode},
    destinationAddressingMode: {destinationAddressingMode},
    sourceAddress: 0x{sourceAddress:X},
    destinationAddress: 0x{destinationAddress:X},
    linkMode: {linkMode},
    link: {link},
    linkAddress: 0x{(linkAddress << 2):X}
}}";

// Some of this fields are read only via sysbus, but can be loaded from memory
#pragma warning disable 649
                [PacketField, Offset(bytes: 0 << 2, bits: 0), Width(2)]
                public StructureType structureType;
                [PacketField, Offset(bytes: 0 << 2, bits: 3), Width(1)]
                public bool structureTransferRequest;
                [PacketField, Offset(bytes: 0 << 2, bits: 4), Width(11)]
                public uint transferCount;
                [PacketField, Offset(bytes: 0 << 2, bits: 15), Width(1)]
                public bool byteSwap;
                [PacketField, Offset(bytes: 0 << 2, bits: 16), Width(4)]
                public BlockSizeMode blockSize;
                [PacketField, Offset(bytes: 0 << 2, bits: 20), Width(1)]
                public bool operationDoneInterruptFlagSetEnable;
                [PacketField, Offset(bytes: 0 << 2, bits: 21), Width(1)]
                public RequestTransferMode requestTransferModeSelect;
                [PacketField, Offset(bytes: 0 << 2, bits: 22), Width(1)]
                public bool decrementLoopCount;
                [PacketField, Offset(bytes: 0 << 2, bits: 23), Width(1)]
                public bool ignoreSingleRequests;
                [PacketField, Offset(bytes: 0 << 2, bits: 24), Width(2)]
                public IncrementMode sourceIncrement;
                [PacketField, Offset(bytes: 0 << 2, bits: 26), Width(2)]
                public SizeMode size;
                [PacketField, Offset(bytes: 0 << 2, bits: 28), Width(2)]
                public IncrementMode destinationIncrement;
                [PacketField, Offset(bytes: 0 << 2, bits: 30), Width(1)]
                public AddressingMode sourceAddressingMode;
                [PacketField, Offset(bytes: 0 << 2, bits: 31), Width(1)]
                public AddressingMode destinationAddressingMode;
                [PacketField, Offset(bytes: 1 << 2, bits: 0), Width(32)]
                public uint sourceAddress;
                [PacketField, Offset(bytes: 2 << 2, bits: 0), Width(32)]
                public uint destinationAddress;
                [PacketField, Offset(bytes: 3 << 2, bits: 0), Width(1)]
                public AddressingMode linkMode;
                [PacketField, Offset(bytes: 3 << 2, bits: 1), Width(1)]
                public bool link;
                [PacketField, Offset(bytes: 3 << 2, bits: 2), Width(30)]
                public uint linkAddress;
#pragma warning restore 649
            }
        }
    }
}