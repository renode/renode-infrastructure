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
    public class EFR32xG22_LDMA : IBusPeripheral, IGPIOReceiver, IKnownSize
    {
        public EFR32xG22_LDMA(Machine machine)
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
                    .WithFlags(0, 8, writeCallback: (i, _, value) => { if (value) channels[i].Enabled = true; }, name: "CHEN")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.CHDIS, new DoubleWordRegister(this)
                    .WithFlags(0, 8, writeCallback: (i, _, value) => { if (value) channels[i].Enabled = false; }, name: "CHDIS")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.CHBUSY, new DoubleWordRegister(this)
                    .WithFlags(0, 8, FieldMode.Read, valueProviderCallback: (i, _) => channels[i].Busy, name: "CHBUSY")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.CHSTATUS, new DoubleWordRegister(this)
                    .WithFlags(0, 8, FieldMode.Read, valueProviderCallback: (i, _) => channels[i].Enabled, name: "CHSTATUS")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.CHDONE, new DoubleWordRegister(this)
                    .WithFlags(0, 8, writeCallback: (i, _, value) => channels[i].Done = value, valueProviderCallback: (i, _) => channels[i].Done, name: "CHDONE")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.DBGHALT, new DoubleWordRegister(this)
                    .WithTag("DBGHALT", 0, 8)
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.SWREQ, new DoubleWordRegister(this)
                    .WithFlags(0, 8, FieldMode.Set, writeCallback: (i, _, value) => { if(value) channels[i].StartTransfer(); }, name: "SWREQ")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.REQDIS, new DoubleWordRegister(this)
                    .WithFlags(0, 8, writeCallback: (i, _, value) => channels[i].RequestDisable = value, valueProviderCallback: (i, _) => channels[i].RequestDisable, name: "REQDIS")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.REQPEND, new DoubleWordRegister(this)
                    .WithTag("REQPEND", 0, 8)
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.LINKLOAD, new DoubleWordRegister(this)
                    .WithFlags(0, 8, FieldMode.Set, writeCallback: (i, _, value) => { if(value) channels[i].LinkLoad(); }, name: "LINKLOAD")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.REQCLEAR, new DoubleWordRegister(this)
                    .WithTag("REQCLEAR", 0, 8)
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.IF, new DoubleWordRegister(this)
                    .WithFlags(0, 8, writeCallback: (i, _, value) => channels[i].DoneInterrupt = value, valueProviderCallback: (i, _) => channels[i].DoneInterrupt, name: "IF")
                    .WithReservedBits(8, 23)
                    .WithTaggedFlag("ERROR", 31)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)LdmaRegisters.IEN, new DoubleWordRegister(this)
                    .WithFlags(0, 8, writeCallback: (i, _, value) => channels[i].DoneInterruptEnable = value, valueProviderCallback: (i, _) => channels[i].DoneInterruptEnable, name: "IEN")
                    .WithReservedBits(8, 23)
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
        private const int NumberOfChannels = 8;

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
            // if SOURCESEL is USART1
            USART1_DMA_RXDATAV                  = 0x0,
            USART1_DMA_RXDATAVRIGHT             = 0x1,
            USART1_DMA_TXBL                     = 0x2,
            USART1_DMA_TXBLRIGHT                = 0x3,
            USART1_DMA_TXEMPTY                  = 0x4,
            // if SOURCESEL is I2C0
            I2C0_DMA_RXDATAV                    = 0x0,
            I2C0_DMA_TXBL                       = 0x1,            
            // if SOURCESEL is I2C1
            I2C1_DMA_RXDATAV                    = 0x0,
            I2C1_DMA_TXBL                       = 0x1,
            // if SOURCESEL is IADC0
            IADC0_DMA_IADC_SCAN                 = 0x0,
            IADC0_DMA_IADC_SINGLE               = 0x1,
            // if SOURCESEL is MSC
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
            // if SOURCESEL is PDM
            PDM_DMA_RXDATAV                     = 0x0,
            // if SOURCESEL is EUART0
            EUART0_DMA_RXFL                     = 0x0,
            EUART0_DMA_TXFL                     = 0x1,
            // if SOURCESEL is TIMER4
            TIMER4_DMA_CC0                      = 0x0,
            TIMER4_DMA_CC1                      = 0x1,
            TIMER4_DMA_CC2                      = 0x2,
            TIMER4_DMA_UFOF                     = 0x3,
        }

        private enum SourceSelect
        {
            None     = 0x0,
            LDMAXBAR = 0x1,
            TIMER0   = 0x2,
            TIMER1   = 0x3,
            USART0   = 0x4,
            USART1   = 0x5,
            I2C0     = 0x6,
            I2C1     = 0x7,
            IADC0    = 0xB,
            MSC      = 0xC,
            TIMER2   = 0xD,
            TIMER3   = 0xE,
            PDM      = 0xF,
            EUART0   = 0x10,
            TIMER4   = 0x11,
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
        }

        private enum LdmaXbarRegisters : long
        {
            XBAR_CH0_REQSEL        = 0x0000,
            XBAR_CH1_REQSEL        = 0x0004,
            XBAR_CH2_REQSEL        = 0x0008,
            XBAR_CH3_REQSEL        = 0x000C,
            XBAR_CH4_REQSEL        = 0x0010,
            XBAR_CH5_REQSEL        = 0x0014,
            XBAR_CH6_REQSEL        = 0x0018,
            XBAR_CH7_REQSEL        = 0x001C,
            // Set registers
            XBAR_CH0_REQSEL_Set    = 0x1000,
            XBAR_CH1_REQSEL_Set    = 0x1004,
            XBAR_CH2_REQSEL_Set    = 0x1008,
            XBAR_CH3_REQSEL_Set    = 0x100C,
            XBAR_CH4_REQSEL_Set    = 0x1010,
            XBAR_CH5_REQSEL_Set    = 0x1014,
            XBAR_CH6_REQSEL_Set    = 0x1018,
            XBAR_CH7_REQSEL_Set    = 0x101C,
            // Clear registers
            XBAR_CH0_REQSEL_Clr    = 0x2000,
            XBAR_CH1_REQSEL_Clr    = 0x2004,
            XBAR_CH2_REQSEL_Clr    = 0x2008,
            XBAR_CH3_REQSEL_Clr    = 0x200C,
            XBAR_CH4_REQSEL_Clr    = 0x2010,
            XBAR_CH5_REQSEL_Clr    = 0x2014,
            XBAR_CH6_REQSEL_Clr    = 0x2018,
            XBAR_CH7_REQSEL_Clr    = 0x201C,
            // Toggle registers
            XBAR_CH0_REQSEL_Tgl    = 0x3000,
            XBAR_CH1_REQSEL_Tgl    = 0x3004,
            XBAR_CH2_REQSEL_Tgl    = 0x3008,
            XBAR_CH3_REQSEL_Tgl    = 0x300C,
            XBAR_CH4_REQSEL_Tgl    = 0x3010,
            XBAR_CH5_REQSEL_Tgl    = 0x3014,
            XBAR_CH6_REQSEL_Tgl    = 0x3018,
            XBAR_CH7_REQSEL_Tgl    = 0x301C,
        }


        private class Channel
        {
            public Channel(EFR32xG22_LDMA parent, int index)
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
                        case SourceSelect.USART0:
                        case SourceSelect.USART1:
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
                        case SourceSelect.EUART0:                            
                            switch(Signal)
                            {
                                case SignalSelect.EUART0_DMA_RXFL:
                                    return false;
                                case SignalSelect.EUART0_DMA_TXFL:
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

            private readonly EFR32xG22_LDMA parent;
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