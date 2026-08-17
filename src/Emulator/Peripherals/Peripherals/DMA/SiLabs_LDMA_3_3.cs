//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class SiLabs_LDMA_3_3 : IBusPeripheral, IGPIOReceiver, IKnownSize
    {
        public SiLabs_LDMA_3_3(Machine machine, IPeripheral context = null)
        {
            this.machine = machine;
            this.context = context;
            engine = new DmaEngine(machine.GetSystemBus(this));
            signals = new HashSet<int>();

            channels = new Channel[NumberOfChannels];
            channelIRQs = new GPIO[NumberOfChannels];
            for(var i = 0; i < NumberOfChannels; ++i)
            {
                channels[i] = new Channel(this, i);
                channelIRQs[i] = new GPIO();
            }
            ldmaRegistersCollection = BuildLdmaRegisters();
            ldmaXbarRegistersCollection = BuildLdmaXbarRegisters();
        }

        [ConnectionRegion("ldmaxbar_ns")]
        public byte ReadByteFromLdmaXbarNonSecure(long offset)
        {
            return ReadByte<LdmaXbarRegisters>(ldmaXbarRegistersCollection, "LdmaXbar (LDMAXBAR_NS)", offset);
        }

        [ConnectionRegion("ldmaxbar_ns")]
        public uint ReadDoubleWordFromLdmaXbarNonSecure(long offset)
        {
            return Read<LdmaXbarRegisters>(ldmaXbarRegistersCollection, "LdmaXbar (LDMAXBAR_NS)", offset);
        }

        [ConnectionRegion("ldmaxbar_ns")]
#pragma warning disable IDE0060
        public void WriteByteToLdmaXbarNonSecure(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("ldmaxbar_ns")]
        public void WriteDoubleWordToLdmaXbarNonSecure(long offset, uint value)
        {
            Write<LdmaXbarRegisters>(ldmaXbarRegistersCollection, "LdmaXbar (LDMAXBAR_NS)", offset, value);
        }

        [ConnectionRegion("ldmaxbar_s")]
        public byte ReadByteFromLdmaXbar(long offset)
        {
            return ReadByte<LdmaXbarRegisters>(ldmaXbarRegistersCollection, "LdmaXbar (LDMAXBAR_S)", offset);
        }

        [ConnectionRegion("ldmaxbar_s")]
        public uint ReadDoubleWordFromLdmaXbar(long offset)
        {
            return Read<LdmaXbarRegisters>(ldmaXbarRegistersCollection, "LdmaXbar (LDMAXBAR_S)", offset);
        }

        [ConnectionRegion("ldmaxbar_s")]
        public void WriteDoubleWordToLdmaXbar(long offset, uint value)
        {
            Write<LdmaXbarRegisters>(ldmaXbarRegistersCollection, "LdmaXbar (LDMAXBAR_S)", offset, value);
        }

        [ConnectionRegion("ldma_ns")]
        public byte ReadByteFromLdmaNonSecure(long offset)
        {
            return ReadByte<LdmaRegisters>(ldmaRegistersCollection, "Ldma (LDMA_NS)", offset);
        }

        [ConnectionRegion("ldma_ns")]
        public uint ReadDoubleWordFromLdmaNonSecure(long offset)
        {
            return Read<LdmaRegisters>(ldmaRegistersCollection, "Ldma (LDMA_NS)", offset);
        }

        [ConnectionRegion("ldmaxbar_s")]
#pragma warning disable IDE0060
        public void WriteByteToLdmaXbar(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("ldma_ns")]
        public void WriteDoubleWordToLdmaNonSecure(long offset, uint value)
        {
            Write<LdmaRegisters>(ldmaRegistersCollection, "Ldma (LDMA_NS)", offset, value);
        }

        [ConnectionRegion("ldma_s")]
        public byte ReadByteFromLdma(long offset)
        {
            return ReadByte<LdmaRegisters>(ldmaRegistersCollection, "Ldma (LDMA_S)", offset);
        }

        [ConnectionRegion("ldma_s")]
        public uint ReadDoubleWordFromLdma(long offset)
        {
            return Read<LdmaRegisters>(ldmaRegistersCollection, "Ldma (LDMA_S)", offset);
        }

        [ConnectionRegion("ldma_s")]
#pragma warning disable IDE0060
        public void WriteByteToLdma(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("ldma_s")]
        public void WriteDoubleWordToLdma(long offset, uint value)
        {
            Write<LdmaRegisters>(ldmaRegistersCollection, "Ldma (LDMA_S)", offset, value);
        }

        [ConnectionRegion("ldma_ns")]
#pragma warning disable IDE0060
        public void WriteByteToLdmaNonSecure(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        public void Reset()
        {
            signals.Clear();
            syncTrigger = 0;
            inSyncNotification = false;
            syncNotificationPending = false;
            foreach(var channel in channels)
            {
                channel.Reset();
            }

            ldmaRegistersCollection.Reset();
            ldmaXbarRegistersCollection.Reset();

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

        public GPIO Channel0IRQ => channelIRQs[0];

        public GPIO Channel1IRQ => channelIRQs[1];

        public GPIO Channel2IRQ => channelIRQs[2];

        public GPIO Channel3IRQ => channelIRQs[3];

        public GPIO Channel4IRQ => channelIRQs[4];

        public GPIO Channel5IRQ => channelIRQs[5];

        public GPIO Channel6IRQ => channelIRQs[6];

        public GPIO Channel7IRQ => channelIRQs[7];

        public long Size => 0x4000;

        private uint Read<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, bool internal_read = false)
        where T : struct, IComparable, IFormattable
        {
            var result = 0U;
            long internal_offset = offset;

            // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
            if(offset >= SetRegisterOffset && offset < ClearRegisterOffset)
            {
                // Set register
                internal_offset = offset - SetRegisterOffset;
                if(LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            }
            else if(offset >= ClearRegisterOffset && offset < ToggleRegisterOffset)
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if(LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            }
            else if(offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if(LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            }

            try
            {
                if(registersCollection.TryRead(internal_offset, out result))
                {
                    return result;
                }
            }
            finally
            {
                if(LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Info, "{0}: Read from {1} at offset 0x{2:X} ({3}), returned 0x{4:X}",
                             this.GetTime(), regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), result);
                }
            }

            if(LogRegisterAccess && !internal_read)
            {
                this.Log(LogLevel.Warning, "Unhandled read from {0} at offset 0x{1:X} ({2}).", regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"));
            }

            return 0;
        }

        private void UpdateSyncTrigger(byte set, byte clear)
        {
            // Setting a bit always takes precedence over clearing it.
            var newValue = (byte)((syncTrigger & ~clear) | set);
            if(newValue == syncTrigger)
            {
                return;
            }
            this.Log(LogLevel.Debug, "SYNCTRIG changed from 0x{0:X} to 0x{1:X}", syncTrigger, newValue);
            syncTrigger = newValue;
            NotifySyncTriggerChanged();
        }

        private void NotifySyncTriggerChanged()
        {
            // A resumed channel may itself load a synchronization structure and change SYNCTRIG again,
            // so nested updates are turned into another pass instead of a recursive call.
            if(inSyncNotification)
            {
                syncNotificationPending = true;
                return;
            }

            inSyncNotification = true;
            try
            {
                do
                {
                    syncNotificationPending = false;
                    foreach(var channel in channels)
                    {
                        channel.OnSyncTriggerChanged();
                    }
                }
                while(syncNotificationPending);
            }
            finally
            {
                inSyncNotification = false;
            }
        }

        private void UpdateInterrupts()
        {
            for(var i = 0; i < NumberOfChannels; ++i)
            {
                var irq = channels[i].IRQ;
                if(LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "CHANNEL-{0}: IRQ set", i);
                }
                channelIRQs[i].Set(irq);
            }
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

        private DoubleWordRegisterCollection BuildLdmaXbarRegisters()
        {
            var c =  new DoubleWordRegisterCollection(this, new Dictionary<long, DoubleWordRegister>());

            var channelDelta = (uint)((long)LdmaXbarRegisters.XBAR_CH1_REQSEL - (long)LdmaXbarRegisters.XBAR_CH0_REQSEL);
            BindRegisters(LdmaXbarRegisters.XBAR_CH0_REQSEL, c, NumberOfChannels, i => channels[i].PeripheralRequestSelectRegister, channelDelta);

            return c;
        }

        private DoubleWordRegisterCollection BuildLdmaRegisters()
        {
            var c =  new DoubleWordRegisterCollection(this, new Dictionary<long, DoubleWordRegister>
            {
                {(long)LdmaRegisters.CTRL, new DoubleWordRegister(this, 0x1E000000)
                    .WithReservedBits(0, 24)
                    .WithValueField(24, 6, writeCallback: (_, value) => FixedPriorityChannelsNumber = (uint)value, valueProviderCallback: _ => FixedPriorityChannelsNumber, name: "NUMFIXED")
                    .WithReservedBits(30, 2)
                },
                {(long)LdmaRegisters.STATUS, new DoubleWordRegister(this)
                    .WithFlag(0, name: "ANYBUSY")
                    .WithFlag(1, name: "ANYREQ")
                    .WithReservedBits(2, 1)
                    .WithValueField(3, 4, name: "CHGRANT")
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 4, name: "CHERROR")
                    .WithReservedBits(12, 12)
                    .WithValueField(24, 5, name: "CHNUM")
                    .WithReservedBits(29, 3)
                },
                {(long)LdmaRegisters.SYNCSWSET, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) => UpdateSyncTrigger((byte)value, 0), name: "SYNCSWSET")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.SYNCSWCLR, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) => UpdateSyncTrigger(0, (byte)value), name: "SYNCSWCLR")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.SYNCHWEN, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out syncSetEnable, name: "SYNCSETEN")
                    .WithReservedBits(8, 8)
                    .WithValueField(16, 8, out syncClearEnable, name: "SYNCCLREN")
                    .WithReservedBits(24, 8)
                    .WithWriteCallback((_, __) =>
                    {
                        if(syncSetEnable.Value != 0 || syncClearEnable.Value != 0)
                        {
                            // PRS is not routed to this peripheral, so hardware sync triggers never arrive.
                            this.Log(LogLevel.Warning, "Hardware sync triggers enabled (set=0x{0:X}, clear=0x{1:X}) but PRS driven SYNCTRIG updates are not supported.",
                                     syncSetEnable.Value, syncClearEnable.Value);
                        }
                    })
                },
                {(long)LdmaRegisters.SYNCHWSEL, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "SYNCSETEDGE")
                    .WithReservedBits(8, 8)
                    .WithValueField(16, 8, name: "SYNCCLREDGE")
                    .WithReservedBits(24, 8)
                },
                {(long)LdmaRegisters.SYNCSTATUS, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => syncTrigger, name: "SYNCTRIG")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.CHEN, new DoubleWordRegister(this)
                    .WithFlags(0, 8, writeCallback: (i, _, value) => { if (value) channels[i].Enabled = true; }, name: "CHEN")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.CHDIS, new DoubleWordRegister(this)
                    .WithFlags(0, 8, writeCallback: (i, _, value) => { if (value) channels[i].Enabled = false; }, name: "CHDIS")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.CHSTATUS, new DoubleWordRegister(this)
                    .WithFlags(0, 8, FieldMode.Read, valueProviderCallback: (i, _) => channels[i].Enabled, name: "CHSTATUS")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.CHBUSY, new DoubleWordRegister(this)
                    .WithFlags(0, 8, FieldMode.Read, valueProviderCallback: (i, _) => channels[i].Busy, name: "CHBUSY")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.CHDONE, new DoubleWordRegister(this)
                    .WithFlags(0, 8, writeCallback: (i, _, value) => channels[i].Done = value, valueProviderCallback: (i, _) => channels[i].Done, name: "CHDONE")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.DBGHALT, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "DBGHALT")
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
                    .WithValueField(0, 8, name: "REQPEND")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.LINKLOAD, new DoubleWordRegister(this)
                    .WithFlags(0, 8, FieldMode.Set, writeCallback: (i, _, value) => { if(value) channels[i].LinkLoad(); }, name: "LINKLOAD")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.REQCLEAR, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "REQCLEAR")
                    .WithReservedBits(8, 24)
                },
                {(long)LdmaRegisters.IF, new DoubleWordRegister(this)
                    .WithFlags(0, 8, writeCallback: (i, _, value) => channels[i].DoneInterrupt = value, valueProviderCallback: (i, _) => channels[i].DoneInterrupt, name: "IF")
                    .WithReservedBits(8, 23)
                    .WithFlag(31, name: "ERROR")
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)LdmaRegisters.IEN, new DoubleWordRegister(this)
                    .WithFlags(0, 8, writeCallback: (i, _, value) => channels[i].DoneInterruptEnable = value, valueProviderCallback: (i, _) => channels[i].DoneInterruptEnable, name: "IEN")
                    .WithReservedBits(8, 23)
                    .WithFlag(31, name: "ERROR")
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
            BindRegisters(LdmaRegisters.CH0_XCTRL, c, NumberOfChannels, i => channels[i].DescriptorExtendedControlWordRegister, channelDelta);
            BindRegisters(LdmaRegisters.CH0_DUALDST, c, NumberOfChannels, i => channels[i].DescriptorDualDestinationDataAddressRegister, channelDelta);
            BindRegisters(LdmaRegisters.CH0_ILSRC, c, NumberOfChannels, i => channels[i].DescriptorInterleavingSourceDataAddressRegister, channelDelta);

            return c;
        }

        private void Write<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, uint value)
        where T : struct, IComparable, IFormattable
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                long internal_offset = offset;
                uint internal_value = value;

                if(offset >= SetRegisterOffset && offset < ClearRegisterOffset)
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    uint old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value | value;
                    if(LogRegisterAccess)
                    {
                        this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                    }
                }
                else if(offset >= ClearRegisterOffset && offset < ToggleRegisterOffset)
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value & ~value;
                    if(LogRegisterAccess)
                    {
                        this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                    }
                }
                else if(offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value ^ value;
                    if(LogRegisterAccess)
                    {
                        this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                    }
                }

                if(LogRegisterAccess)
                {
                    this.Log(LogLevel.Info, "{0}: Write to {1} at offset 0x{2:X} ({3}), value 0x{4:X}",
                            this.GetTime(), regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), internal_value);
                }
                if(!registersCollection.TryWrite(internal_offset, internal_value) && LogRegisterAccess)
                {
                    this.Log(LogLevel.Warning, "Unhandled write to {0} at offset 0x{1:X} ({2}), value 0x{3:X}.", regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), internal_value);
                    return;
                }
            });
        }

        private byte ReadByte<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset)
        where T : struct, IComparable, IFormattable
        {
            int byteOffset = (int)(offset & 0x3);
            // TODO: single byte reads are treated as internal reads for now to avoid flooding the log during debugging.
            uint registerValue = Read<T>(registersCollection, regionName, offset - byteOffset, true);
            byte result = (byte)((registerValue >> byteOffset*8) & 0xFF);
            return result;
        }

        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;

        private uint FixedPriorityChannelsNumber
        {
            set
            {
                for(uint i = 0; i < NumberOfChannels; i++)
                {
                    channels[i].FixedPriority = (i < value);
                }
            }

            get
            {
                uint ret = NumberOfChannels - 1;
                for(uint i = 0; i < NumberOfChannels; i++)
                {
                    if(!channels[i].FixedPriority)
                    {
                        ret = i;
                        break;
                    }
                }
                return ret;
            }
        }

        private byte syncTrigger;
        private bool inSyncNotification;
        private bool syncNotificationPending;

        private IValueRegisterField syncSetEnable;
        private IValueRegisterField syncClearEnable;

        private readonly GPIO[] channelIRQs;

        private readonly DmaEngine engine;
        private readonly IPeripheral context;
        private readonly HashSet<int> signals;
        private readonly Channel[] channels;
        private readonly DoubleWordRegisterCollection ldmaRegistersCollection;
        private readonly DoubleWordRegisterCollection ldmaXbarRegistersCollection;
        private readonly Machine machine;
        private readonly bool LogRegisterAccess = false;
        private readonly bool LogInterrupts = false;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private const int NumberOfChannels = 8;

        private class Channel
        {
            public Channel(SiLabs_LDMA_3_3 parent, int index)
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
                    .WithFlag(22, name: "STRUCTBUSPORT")
                    .WithFlag(23, name: "SRCBUSPORT")
                    .WithFlag(24, name: "DSTBUSPORT")
                    .WithFlag(25, name: "DUALDSTBUSPORT")
                    .WithReservedBits(26, 6)
                ;
                LoopCounterRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 8, out loopCounter, name: "LOOPCNT")
                    .WithReservedBits(8, 24)
                ;
                DescriptorControlWordRegister = new DoubleWordRegister(parent)
                    .WithEnumField<DoubleWordRegister, StructureType>(0, 2,
                        writeCallback: (_, value) => descriptor.StructureType = value,
                        valueProviderCallback: _ => descriptor.StructureType,
                        name: "STRUCTTYPE")
                    .WithFlag(2, name: "EXTEND")
                    .WithFlag(3, FieldMode.Set,
                        writeCallback: (_, value) => descriptor.StructureTransferRequest = value,
                        name: "STRUCTREQ")
                    .WithValueField(4, 11,
                        writeCallback: (_, value) => descriptor.TransferCount = (ushort)value,
                        valueProviderCallback: _ => descriptor.TransferCount,
                        name: "XFERCNT")
                    .WithFlag(15,
                        writeCallback: (_, value) => descriptor.ByteSwap = value,
                        valueProviderCallback: _ => descriptor.ByteSwap,
                        name: "BYTESWAP")
                    .WithEnumField<DoubleWordRegister, BlockSizeMode>(16, 4,
                        writeCallback: (_, value) => descriptor.BlockSize = value,
                        valueProviderCallback: _ => descriptor.BlockSize,
                        name: "BLOCKSIZE")
                    .WithFlag(20,
                        writeCallback: (_, value) => descriptor.OperationDoneInterruptFlagSetEnable = value,
                        valueProviderCallback: _ => descriptor.OperationDoneInterruptFlagSetEnable,
                        name: "DONEIEN")
                    .WithEnumField<DoubleWordRegister, RequestTransferMode>(21, 1,
                        writeCallback: (_, value) => descriptor.RequestTransferModeSelect = value,
                        valueProviderCallback: _ => descriptor.RequestTransferModeSelect,
                        name: "REQMODE")
                    .WithFlag(22,
                        writeCallback: (_, value) => descriptor.DecrementLoopCount = value,
                        valueProviderCallback: _ => descriptor.DecrementLoopCount,
                        name: "DECLOOPCNT")
                    .WithFlag(23,
                        writeCallback: (_, value) => descriptor.IgnoreSingleRequests = value,
                        valueProviderCallback: _ => descriptor.IgnoreSingleRequests,
                        name: "IGNORESREQ")
                    .WithEnumField<DoubleWordRegister, IncrementMode>(24, 2,
                        writeCallback: (_, value) => descriptor.SourceIncrement = value,
                        valueProviderCallback: _ => descriptor.SourceIncrement,
                        name: "SRCINC")
                    .WithEnumField<DoubleWordRegister, SizeMode>(26, 2,
                        writeCallback: (_, value) => descriptor.Size = value,
                        valueProviderCallback: _ => descriptor.Size,
                        name: "SIZE")
                    .WithEnumField<DoubleWordRegister, IncrementMode>(28, 2,
                        writeCallback: (_, value) => descriptor.DestinationIncrement = value,
                        valueProviderCallback: _ => descriptor.DestinationIncrement,
                        name: "DSTINC")
                    .WithEnumField<DoubleWordRegister, AddressingMode>(30, 1, FieldMode.Read,
                        valueProviderCallback: _ => descriptor.SourceAddressingMode,
                        name: "SRCMODE")
                    .WithEnumField<DoubleWordRegister, AddressingMode>(31, 1, FieldMode.Read,
                        valueProviderCallback: _ => descriptor.DestinationAddressingMode,
                        name: "DSTMODE")
                    .WithChangeCallback((_, __) => { if(descriptor.StructureTransferRequest) LinkLoad(); })
                ;
                DescriptorSourceDataAddressRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) => descriptor.SourceAddress = (uint)value,
                        valueProviderCallback: _ => descriptor.SourceAddress,
                        name: "SRCADDR")
                ;
                DescriptorDestinationDataAddressRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) => descriptor.DestinationAddress = (uint)value,
                        valueProviderCallback: _ => descriptor.DestinationAddress,
                        name: "DSTADDR")
                ;
                DescriptorLinkStructureAddressRegister = new DoubleWordRegister(parent)
                    .WithEnumField<DoubleWordRegister, AddressingMode>(0, 1, FieldMode.Read,
                        valueProviderCallback: _ => descriptor.LinkMode,
                        name: "LINKMODE")
                    .WithFlag(1,
                        writeCallback: (_, value) => descriptor.Link = value,
                        valueProviderCallback: _ => descriptor.Link,
                        name: "LINK")
                    .WithValueField(2, 30,
                        writeCallback: (_, value) => descriptor.LinkAddress = (uint)value,
                        valueProviderCallback: _ =>
                        {
                            if(descriptorAddress.HasValue && descriptor.LinkMode == AddressingMode.Relative)
                            {
                                return (descriptorAddress.Value + (ulong)unchecked((int)LinkStructureAddress)) >> 2;
                            }
                            else
                            {
                                return descriptor.LinkAddress;
                            }
                        },
                        name: "LINKADDR")
                ;
                DescriptorExtendedControlWordRegister = new DoubleWordRegister(parent)
                    .WithFlag(0, name: "DUALDSTEN")
                    .WithValueField(1, 2, name: "DUALDSTINC")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, name: "DSTILEN")
                    .WithValueField(5, 2, name: "ILMODE")
                    .WithFlag(7, name: "BUFFERABLE")
                    .WithReservedBits(8, 24)
                ;
                DescriptorDualDestinationDataAddressRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 32, name: "ADDR")
                ;
                DescriptorInterleavingSourceDataAddressRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 32, name: "ADDR")
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
                if(!RequestDisable && (ExecutesOnLoad || SignalIsOn))
                {
                    StartTransfer();
                }
            }

            public void OnSyncTriggerChanged()
            {
                if(!waitingForSyncMatch || !SyncMatched)
                {
                    return;
                }
                StartTransferInner();
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
                pullTimer.Enabled = false;
                DoneInterrupt = false;
                DoneInterruptEnable = false;
                descriptorAddress = null;
                requestDisable = false;
                enabled = false;
                done = false;
                waitingForSyncMatch = false;
                syncPatternApplied = false;
                FixedPriority = true;
            }

            public int Index { get; }

            public SignalSelect Signal => signalSelect.Value;

            public SourceSelect Source => sourceSelect.Value;

            public bool IgnoreSingleRequests => descriptor.IgnoreSingleRequests;

            public bool DoneInterrupt { get; set; }

            public bool DoneInterruptEnable { get; set; }

            public bool IRQ => DoneInterrupt && DoneInterruptEnable;

            public bool FixedPriority { get; set; }

            public DoubleWordRegister PeripheralRequestSelectRegister { get; }

            public DoubleWordRegister ConfigurationRegister { get; }

            public DoubleWordRegister LoopCounterRegister { get; }

            public DoubleWordRegister DescriptorControlWordRegister { get; }

            public DoubleWordRegister DescriptorSourceDataAddressRegister { get; }

            public DoubleWordRegister DescriptorDestinationDataAddressRegister { get; }

            public DoubleWordRegister DescriptorLinkStructureAddressRegister { get; }

            public DoubleWordRegister DescriptorExtendedControlWordRegister { get; }

            public DoubleWordRegister DescriptorDualDestinationDataAddressRegister { get; }

            public DoubleWordRegister DescriptorInterleavingSourceDataAddressRegister { get; }

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
                        // Enabling a channel does not move data on its own. A transfer starts on
                        // an unconditional STRUCTREQ or once a peripheral request asserts,
                        // otherwise the channel stays idle until a SWREQ, LINKLOAD or signal.
                        if(descriptor.StructureTransferRequest || SignalIsOn)
                        {
                            StartTransfer();
                        }
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
                    if(!done)
                    {
                        DoneInterrupt |= value && descriptor.OperationDoneInterruptFlagSetEnable;
                    }
                    done = value;
                }
            }

            public bool Busy
            {
                get
                {
                    return isInProgress || waitingForSyncMatch;
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

            protected readonly int DescriptorSize = Packet.CalculateLength<Descriptor>();

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
                    if(waitingForSyncMatch)
                    {
                        // The channel is paused on a synchronization structure and will be
                        // resumed by OnSyncTriggerChanged once the match condition is fulfilled.
                        break;
                    }
                    if(Done && descriptor.Link)
                    {
                        loaded = true;
                        LoadDescriptor();
                        Done = false;
                    }
                }
                while((ExecutesOnLoad && loaded) || (!Done && SignalIsOn));

                isInProgress = false;
                if(Done)
                {
                    pullTimer.Enabled = false;
                }
            }

            private void LoadDescriptor()
            {
                var address = LinkStructureAddress;
                if(descriptorAddress.HasValue && descriptor.LinkMode == AddressingMode.Relative)
                {
                    address = (descriptorAddress.Value + (ulong)unchecked((int)LinkStructureAddress));
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
                switch(descriptor.StructureType)
                {
                case StructureType.Transfer:
                    var request = new Request(
                            source: new Place(descriptor.SourceAddress),
                            destination: new Place(descriptor.DestinationAddress),
                            size: Bytes,
                            readTransferType: SizeAsTransferType,
                            writeTransferType: SizeAsTransferType,
                            sourceIncrementStep: SourceIncrement,
                            destinationIncrementStep: DestinationIncrement
                        );
                    parent.engine.IssueCopy(request, parent.context);
                    if(descriptor.RequestTransferModeSelect == RequestTransferMode.Block)
                    {
                        var blockSizeMultiplier = Math.Min(TransferCount, BlockSizeMultiplier);
                        if(blockSizeMultiplier == TransferCount)
                        {
                            Done = true;
                            descriptor.TransferCount = 0;
                        }
                        else
                        {
                            descriptor.TransferCount -= blockSizeMultiplier;
                        }
                        descriptor.SourceAddress += SourceIncrement * blockSizeMultiplier;
                        descriptor.DestinationAddress += DestinationIncrement * blockSizeMultiplier;
                    }
                    else
                    {
                        Done = true;
                    }
                    break;
                case StructureType.Synchronize:
                    if(!syncPatternApplied)
                    {
                        syncPatternApplied = true;
                        parent.Log(LogLevel.Debug, "Channel #{0} Performing Synchronize, set=0x{1:X} clear=0x{2:X} matchValue=0x{3:X} matchEnable=0x{4:X}",
                                   Index, SyncSetPattern, SyncClearPattern, SyncMatchValue, SyncMatchEnable);
                        parent.UpdateSyncTrigger(SyncSetPattern, SyncClearPattern);
                    }
                    if(SyncMatched)
                    {
                        waitingForSyncMatch = false;
                        syncPatternApplied = false;
                        Done = true;
                    }
                    else
                    {
                        waitingForSyncMatch = true;
                        parent.Log(LogLevel.Debug, "Channel #{0} Waiting for SYNCTRIG (0x{1:X}) to match 0x{2:X} on 0x{3:X}",
                                   Index, parent.syncTrigger, SyncMatchValue, SyncMatchEnable);
                    }
                    break;
                case StructureType.Write:
                    parent.Log(LogLevel.Debug, "Channel #{0} Performing Write of 0x{1:X} to 0x{2:X}", Index, ImmediateValue, descriptor.DestinationAddress);
                    parent.machine.GetSystemBus(parent).WriteDoubleWord(descriptor.DestinationAddress, ImmediateValue, parent.context);
                    Done = true;
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
                    case SourceSelect.EUSART0:
                    case SourceSelect.EUSART1:
                    case SourceSelect.EUSART2:
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
                    case SourceSelect.I2C2:
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
                    // TODO
                    case SourceSelect.PROTIMER:
                    case SourceSelect.MODEM:
                    case SourceSelect.ADC:
                    case SourceSelect.PIXELRZ0:
                    case SourceSelect.PIXELRZ1:
                        goto default;
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
                    switch(descriptor.BlockSize)
                    {
                    case BlockSizeMode.Unit1:
                    case BlockSizeMode.Unit2:
                        return 1u << (byte)descriptor.BlockSize;
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
                        return 1u << ((byte)descriptor.BlockSize - 4);
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

            // True for a structure that runs as soon as it is loaded, needing no peripheral
            // request: STRUCTREQ is an unconditional software request carried in the descriptor,
            // while synchronization and write structures transfer no data so no request applies.
            private bool ExecutesOnLoad => descriptor.StructureTransferRequest || descriptor.StructureType != StructureType.Transfer;

            // For non-transfer structures the source and destination descriptor words hold
            // different fields: SYNCSET/SYNCCLR and MATCHEN/MATCHVAL for a synchronization
            // structure, the immediate value and its destination for a write structure. This IP
            // supports extended descriptors, where MATCHEN comes before MATCHVAL, the reverse of
            // the older controllers.
            private byte SyncSetPattern => (byte)descriptor.SourceAddress;

            private byte SyncClearPattern => (byte)(descriptor.SourceAddress >> 8);

            private byte SyncMatchEnable => (byte)descriptor.DestinationAddress;

            private byte SyncMatchValue => (byte)(descriptor.DestinationAddress >> 8);

            private uint ImmediateValue => descriptor.SourceAddress;

            private bool SyncMatched => (parent.syncTrigger & SyncMatchEnable) == (SyncMatchValue & SyncMatchEnable);

            private uint TransferCount => (uint)descriptor.TransferCount + 1;

            private ulong LinkStructureAddress => (ulong)descriptor.LinkAddress << 2;

            private uint SourceIncrement => descriptor.SourceIncrement == IncrementMode.None ? 0u : ((1u << (byte)descriptor.Size) << (byte)descriptor.SourceIncrement);

            private uint DestinationIncrement => descriptor.DestinationIncrement == IncrementMode.None ? 0u : ((1u << (byte)descriptor.Size) << (byte)descriptor.DestinationIncrement);

            private TransferType SizeAsTransferType => (TransferType)(1 << (byte)descriptor.Size);

            private int Bytes => (int)(descriptor.RequestTransferModeSelect == RequestTransferMode.All ? TransferCount : Math.Min(TransferCount, BlockSizeMultiplier)) << (byte)descriptor.Size;

            private Descriptor descriptor;
            private ulong? descriptorAddress;
            private bool requestDisable;
            private bool enabled;
            private bool done;

            // A synchronization structure applies its set/clear pattern only once, even if the
            // channel has to wait for the match condition before the structure completes.
            private bool waitingForSyncMatch;
            private bool syncPatternApplied;

            // Accesses to sysubs may cause changes in signals, but we should ignore those during active transaction
            private bool isInProgress;

            private readonly IEnumRegisterField<SignalSelect> signalSelect;
            private readonly IEnumRegisterField<SourceSelect> sourceSelect;
            private readonly IEnumRegisterField<ArbitrationSlotNumberMode> arbitrationSlotNumberSelect;
            private readonly IEnumRegisterField<Sign> sourceAddressIncrementSign;
            private readonly IEnumRegisterField<Sign> destinationAddressIncrementSign;
            private readonly IValueRegisterField loopCounter;

            private readonly SiLabs_LDMA_3_3 parent;
            private readonly LimitTimer pullTimer;

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
                public string PrettyString
                {
                    get
                    {
                        switch(StructureType)
                        {
                        case StructureType.Synchronize:
                            return SynchronizePrettyString;
                        case StructureType.Write:
                            return WritePrettyString;
                        default:
                            return TransferPrettyString;
                        }
                    }
                }

                private string SynchronizePrettyString => $@"Descriptor {{
    StructureType: {StructureType},
    OperationDoneInterruptFlagSetEnable: {OperationDoneInterruptFlagSetEnable},
    SyncSet: 0x{(byte)SourceAddress:X},
    SyncClear: 0x{(byte)(SourceAddress >> 8):X},
    MatchEnable: 0x{(byte)DestinationAddress:X},
    MatchValue: 0x{(byte)(DestinationAddress >> 8):X},
    LinkMode: {LinkMode},
    Link: {Link},
    LinkAddress: 0x{(LinkAddress << 2):X}
}}";

                private string WritePrettyString => $@"Descriptor {{
    StructureType: {StructureType},
    OperationDoneInterruptFlagSetEnable: {OperationDoneInterruptFlagSetEnable},
    ImmediateValue: 0x{SourceAddress:X},
    DestinationAddress: 0x{DestinationAddress:X},
    LinkMode: {LinkMode},
    Link: {Link},
    LinkAddress: 0x{(LinkAddress << 2):X}
}}";

                private string TransferPrettyString => $@"Descriptor {{
    StructureType: {StructureType},
    StructureTransferRequest: {StructureTransferRequest},
    TransferCount: {TransferCount + 1},
    ByteSwap: {ByteSwap},
    BlockSize: {BlockSize},
    OperationDoneInterruptFlagSetEnable: {OperationDoneInterruptFlagSetEnable},
    RequestTransferModeSelect: {RequestTransferModeSelect},
    DecrementLoopCount: {DecrementLoopCount},
    IgnoreSingleRequests: {IgnoreSingleRequests},
    SourceIncrement: {SourceIncrement},
    Size: {Size},
    DestinationIncrement: {DestinationIncrement},
    SourceAddressingMode: {SourceAddressingMode},
    DestinationAddressingMode: {DestinationAddressingMode},
    SourceAddress: 0x{SourceAddress:X},
    DestinationAddress: 0x{DestinationAddress:X},
    LinkMode: {LinkMode},
    Link: {Link},
    LinkAddress: 0x{(LinkAddress << 2):X}
}}";

                // Some of this fields are read only via sysbus, but can be loaded from memory
#pragma warning disable 649
                [PacketField, Offset(bytes: 0 << 2, bits: 0), Width(bits: 2)]
                public StructureType StructureType;
                [PacketField, Offset(bytes: 0 << 2, bits: 3), Width(bits: 1)]
                public bool StructureTransferRequest;
                [PacketField, Offset(bytes: 0 << 2, bits: 4), Width(bits: 11)]
                public uint TransferCount;
                [PacketField, Offset(bytes: 0 << 2, bits: 15), Width(bits: 1)]
                public bool ByteSwap;
                [PacketField, Offset(bytes: 0 << 2, bits: 16), Width(bits: 4)]
                public BlockSizeMode BlockSize;
                [PacketField, Offset(bytes: 0 << 2, bits: 20), Width(bits: 1)]
                public bool OperationDoneInterruptFlagSetEnable;
                [PacketField, Offset(bytes: 0 << 2, bits: 21), Width(bits: 1)]
                public RequestTransferMode RequestTransferModeSelect;
                [PacketField, Offset(bytes: 0 << 2, bits: 22), Width(bits: 1)]
                public bool DecrementLoopCount;
                [PacketField, Offset(bytes: 0 << 2, bits: 23), Width(bits: 1)]
                public bool IgnoreSingleRequests;
                [PacketField, Offset(bytes: 0 << 2, bits: 24), Width(bits: 2)]
                public IncrementMode SourceIncrement;
                [PacketField, Offset(bytes: 0 << 2, bits: 26), Width(bits: 2)]
                public SizeMode Size;
                [PacketField, Offset(bytes: 0 << 2, bits: 28), Width(bits: 2)]
                public IncrementMode DestinationIncrement;
                [PacketField, Offset(bytes: 0 << 2, bits: 30), Width(bits: 1)]
                public AddressingMode SourceAddressingMode;
                [PacketField, Offset(bytes: 0 << 2, bits: 31), Width(bits: 1)]
                public AddressingMode DestinationAddressingMode;
                [PacketField, Offset(bytes: 1 << 2, bits: 0), Width(bits: 32)]
                public uint SourceAddress;
                [PacketField, Offset(bytes: 2 << 2, bits: 0), Width(bits: 32)]
                public uint DestinationAddress;
                [PacketField, Offset(bytes: 3 << 2, bits: 0), Width(bits: 1)]
                public AddressingMode LinkMode;
                [PacketField, Offset(bytes: 3 << 2, bits: 1), Width(bits: 1)]
                public bool Link;
                [PacketField, Offset(bytes: 3 << 2, bits: 2), Width(bits: 30)]
                public uint LinkAddress;
#pragma warning restore 649
            }

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
        }

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
            // if SOURCESEL is I2C0
            I2C0_DMA_RXDATAV                    = 0x0,
            I2C0_DMA_TXBL                       = 0x1,
            // if SOURCESEL is I2C1
            I2C1_DMA_RXDATAV                    = 0x0,
            I2C1_DMA_TXBL                       = 0x1,
            // if SOURCESEL is TIMER2
            TIMER2_DMA_CC0                      = 0x0,
            TIMER2_DMA_CC1                      = 0x1,
            TIMER2_DMA_CC2                      = 0x2,
            TIMER2_DMA_UFOF                     = 0x3,
            TIMER2_DMA_CC3                      = 0x4,
            TIMER2_DMA_CC4                      = 0x5,
            TIMER2_DMA_CC5                      = 0x6,
            TIMER2_DMA_CC6                      = 0x7,
            // if SOURCESEL is TIMER3
            TIMER3_DMA_CC0                      = 0x0,
            TIMER3_DMA_CC1                      = 0x1,
            TIMER3_DMA_CC2                      = 0x2,
            TIMER3_DMA_UFOF                     = 0x3,
            TIMER3_DMA_CC3                      = 0x4,
            TIMER3_DMA_CC4                      = 0x5,
            TIMER3_DMA_CC5                      = 0x6,
            TIMER3_DMA_CC6                      = 0x7,
            // if SOURCESEL is EUSART1
            EUSART1_DMA_RXFL                    = 0x0,
            EUSART1_DMA_TXFL                    = 0x1,
            // if SOURCESEL is EUSART0
            EUSART0_DMA_RXFL                    = 0x0,
            EUSART0_DMA_TXFL                    = 0x1,
            // if SOURCESEL is AGC
            AGC_RSSI                            = 0x0,
            // if SOURCESEL is PROTIMER
            PROTIMER_BOF                        = 0x0,
            PROTIMER_CC0                        = 0x1,
            PROTIMER_CC1                        = 0x2,
            PROTIMER_CC2                        = 0x3,
            PROTIMER_CC3                        = 0x4,
            PROTIMER_CC4                        = 0x5,
            PROTIMER_POF                        = 0x6,
            PROTIMER_WOF                        = 0x7,
            // if SOURCESEL is MODEM
            MODEM_DEBUG                         = 0x0,
            // if SOURCESEL is ADC
            ADC_SCAN                            = 0x0,
            // if SOURCESEL is PIXELRZ0
            PIXELRZ0_REQ_TXFL                   = 0x0,
            // if SOURCESEL is EUSART2
            EUSART2_DMA_RXFL                    = 0x0,
            EUSART2_DMA_TXFL                    = 0x1,
            // if SOURCESEL is EUSART3
            EUSART3_DMA_RXFL                    = 0x0,
            EUSART3_DMA_TXFL                    = 0x1,
            // if SOURCESEL is PIXELRZ1
            PIXELRZ1_REQ_TXFL                   = 0x0,
            // if SOURCESEL is I2C2
            I2C2_DMA_RXDATAV                    = 0x0,
            I2C2_DMA_TXBL                       = 0x1,
        }

        private enum SourceSelect
        {
            None     = 0x0,
            LDMAXBAR = 0x1,
            TIMER0   = 0x2,
            TIMER1   = 0x3,
            I2C0     = 0x4,
            I2C1     = 0x5,
            TIMER2   = 0x6,
            TIMER3   = 0x7,
            EUSART1  = 0x8,
            EUSART0  = 0x9,
            AGC      = 0xA,
            PROTIMER = 0xB,
            MODEM    = 0xC,
            ADC      = 0xD,
            PIXELRZ0 = 0xE,
            EUSART2  = 0xF,
            PIXELRZ1 = 0x10,
            I2C2     = 0x11,
        }

        private enum LdmaRegisters : long
        {
            IPVERSION              = 0x0000,
            EN                     = 0x0004,
            SWRST                  = 0x0008,
            CTRL                   = 0x000C,
            STATUS                 = 0x0010,
            SYNCSWSET              = 0x0014,
            SYNCSWCLR              = 0x0018,
            SYNCHWEN               = 0x001C,
            SYNCHWSEL              = 0x0020,
            SYNCSTATUS             = 0x0024,
            CHEN                   = 0x0028,
            CHDIS                  = 0x002C,
            CHSTATUS               = 0x0030,
            CHBUSY                 = 0x0034,
            CHDONE                 = 0x0038,
            DBGHALT                = 0x003C,
            SWREQ                  = 0x0040,
            REQDIS                 = 0x0044,
            REQPEND                = 0x0048,
            LINKLOAD               = 0x004C,
            REQCLEAR               = 0x0050,
            IF                     = 0x0054,
            IEN                    = 0x0058,
            REQABORT               = 0x005C,
            ABORTSTATUS            = 0x0060,
            CH0_CFG                = 0x0070,
            CH0_LOOP               = 0x0074,
            CH0_CTRL               = 0x0078,
            CH0_SRC                = 0x007C,
            CH0_DST                = 0x0080,
            CH0_LINK               = 0x0084,
            CH0_XCTRL              = 0x0088,
            CH0_DUALDST            = 0x008C,
            CH0_ILSRC              = 0x0090,
            CH1_CFG                = 0x00A0,
            CH1_LOOP               = 0x00A4,
            CH1_CTRL               = 0x00A8,
            CH1_SRC                = 0x00AC,
            CH1_DST                = 0x00B0,
            CH1_LINK               = 0x00B4,
            CH1_XCTRL              = 0x00B8,
            CH1_DUALDST            = 0x00BC,
            CH1_ILSRC              = 0x00C0,
            CH2_CFG                = 0x00D0,
            CH2_LOOP               = 0x00D4,
            CH2_CTRL               = 0x00D8,
            CH2_SRC                = 0x00DC,
            CH2_DST                = 0x00E0,
            CH2_LINK               = 0x00E4,
            CH2_XCTRL              = 0x00E8,
            CH2_DUALDST            = 0x00EC,
            CH2_ILSRC              = 0x00F0,
            CH3_CFG                = 0x0100,
            CH3_LOOP               = 0x0104,
            CH3_CTRL               = 0x0108,
            CH3_SRC                = 0x010C,
            CH3_DST                = 0x0110,
            CH3_LINK               = 0x0114,
            CH3_XCTRL              = 0x0118,
            CH3_DUALDST            = 0x011C,
            CH3_ILSRC              = 0x0120,
            CH4_CFG                = 0x0130,
            CH4_LOOP               = 0x0134,
            CH4_CTRL               = 0x0138,
            CH4_SRC                = 0x013C,
            CH4_DST                = 0x0140,
            CH4_LINK               = 0x0144,
            CH4_XCTRL              = 0x0148,
            CH4_DUALDST            = 0x014C,
            CH4_ILSRC              = 0x0150,
            CH5_CFG                = 0x0160,
            CH5_LOOP               = 0x0164,
            CH5_CTRL               = 0x0168,
            CH5_SRC                = 0x016C,
            CH5_DST                = 0x0170,
            CH5_LINK               = 0x0174,
            CH5_XCTRL              = 0x0178,
            CH5_DUALDST            = 0x017C,
            CH5_ILSRC              = 0x0180,
            CH6_CFG                = 0x0190,
            CH6_LOOP               = 0x0194,
            CH6_CTRL               = 0x0198,
            CH6_SRC                = 0x019C,
            CH6_DST                = 0x01A0,
            CH6_LINK               = 0x01A4,
            CH6_XCTRL              = 0x01A8,
            CH6_DUALDST            = 0x01AC,
            CH6_ILSRC              = 0x01B0,
            CH7_CFG                = 0x01C0,
            CH7_LOOP               = 0x01C4,
            CH7_CTRL               = 0x01C8,
            CH7_SRC                = 0x01CC,
            CH7_DST                = 0x01D0,
            CH7_LINK               = 0x01D4,
            CH7_XCTRL              = 0x01D8,
            CH7_DUALDST            = 0x01DC,
            CH7_ILSRC              = 0x01E0,
            // Set registers
            IPVERSION_Set          = 0x1000,
            EN_Set                 = 0x1004,
            SWRST_Set              = 0x1008,
            CTRL_Set               = 0x100C,
            STATUS_Set             = 0x1010,
            SYNCSWSET_Set          = 0x1014,
            SYNCSWCLR_Set          = 0x1018,
            SYNCHWEN_Set           = 0x101C,
            SYNCHWSEL_Set          = 0x1020,
            SYNCSTATUS_Set         = 0x1024,
            CHEN_Set               = 0x1028,
            CHDIS_Set              = 0x102C,
            CHSTATUS_Set           = 0x1030,
            CHBUSY_Set             = 0x1034,
            CHDONE_Set             = 0x1038,
            DBGHALT_Set            = 0x103C,
            SWREQ_Set              = 0x1040,
            REQDIS_Set             = 0x1044,
            REQPEND_Set            = 0x1048,
            LINKLOAD_Set           = 0x104C,
            REQCLEAR_Set           = 0x1050,
            IF_Set                 = 0x1054,
            IEN_Set                = 0x1058,
            REQABORT_Set           = 0x105C,
            ABORTSTATUS_Set        = 0x1060,
            CH0_CFG_Set            = 0x1070,
            CH0_LOOP_Set           = 0x1074,
            CH0_CTRL_Set           = 0x1078,
            CH0_SRC_Set            = 0x107C,
            CH0_DST_Set            = 0x1080,
            CH0_LINK_Set           = 0x1084,
            CH0_XCTRL_Set          = 0x1088,
            CH0_DUALDST_Set        = 0x108C,
            CH0_ILSRC_Set          = 0x1090,
            CH1_CFG_Set            = 0x10A0,
            CH1_LOOP_Set           = 0x10A4,
            CH1_CTRL_Set           = 0x10A8,
            CH1_SRC_Set            = 0x10AC,
            CH1_DST_Set            = 0x10B0,
            CH1_LINK_Set           = 0x10B4,
            CH1_XCTRL_Set          = 0x10B8,
            CH1_DUALDST_Set        = 0x10BC,
            CH1_ILSRC_Set          = 0x10C0,
            CH2_CFG_Set            = 0x10D0,
            CH2_LOOP_Set           = 0x10D4,
            CH2_CTRL_Set           = 0x10D8,
            CH2_SRC_Set            = 0x10DC,
            CH2_DST_Set            = 0x10E0,
            CH2_LINK_Set           = 0x10E4,
            CH2_XCTRL_Set          = 0x10E8,
            CH2_DUALDST_Set        = 0x10EC,
            CH2_ILSRC_Set          = 0x10F0,
            CH3_CFG_Set            = 0x1100,
            CH3_LOOP_Set           = 0x1104,
            CH3_CTRL_Set           = 0x1108,
            CH3_SRC_Set            = 0x110C,
            CH3_DST_Set            = 0x1110,
            CH3_LINK_Set           = 0x1114,
            CH3_XCTRL_Set          = 0x1118,
            CH3_DUALDST_Set        = 0x111C,
            CH3_ILSRC_Set          = 0x1120,
            CH4_CFG_Set            = 0x1130,
            CH4_LOOP_Set           = 0x1134,
            CH4_CTRL_Set           = 0x1138,
            CH4_SRC_Set            = 0x113C,
            CH4_DST_Set            = 0x1140,
            CH4_LINK_Set           = 0x1144,
            CH4_XCTRL_Set          = 0x1148,
            CH4_DUALDST_Set        = 0x114C,
            CH4_ILSRC_Set          = 0x1150,
            CH5_CFG_Set            = 0x1160,
            CH5_LOOP_Set           = 0x1164,
            CH5_CTRL_Set           = 0x1168,
            CH5_SRC_Set            = 0x116C,
            CH5_DST_Set            = 0x1170,
            CH5_LINK_Set           = 0x1174,
            CH5_XCTRL_Set          = 0x1178,
            CH5_DUALDST_Set        = 0x117C,
            CH5_ILSRC_Set          = 0x1180,
            CH6_CFG_Set            = 0x1190,
            CH6_LOOP_Set           = 0x1194,
            CH6_CTRL_Set           = 0x1198,
            CH6_SRC_Set            = 0x119C,
            CH6_DST_Set            = 0x11A0,
            CH6_LINK_Set           = 0x11A4,
            CH6_XCTRL_Set          = 0x11A8,
            CH6_DUALDST_Set        = 0x11AC,
            CH6_ILSRC_Set          = 0x11B0,
            CH7_CFG_Set            = 0x11C0,
            CH7_LOOP_Set           = 0x11C4,
            CH7_CTRL_Set           = 0x11C8,
            CH7_SRC_Set            = 0x11CC,
            CH7_DST_Set            = 0x11D0,
            CH7_LINK_Set           = 0x11D4,
            CH7_XCTRL_Set          = 0x11D8,
            CH7_DUALDST_Set        = 0x11DC,
            CH7_ILSRC_Set          = 0x11E0,
            // Clear registers
            IPVERSION_Clr          = 0x2000,
            EN_Clr                 = 0x2004,
            SWRST_Clr              = 0x2008,
            CTRL_Clr               = 0x200C,
            STATUS_Clr             = 0x2010,
            SYNCSWSET_Clr          = 0x2014,
            SYNCSWCLR_Clr          = 0x2018,
            SYNCHWEN_Clr           = 0x201C,
            SYNCHWSEL_Clr          = 0x2020,
            SYNCSTATUS_Clr         = 0x2024,
            CHEN_Clr               = 0x2028,
            CHDIS_Clr              = 0x202C,
            CHSTATUS_Clr           = 0x2030,
            CHBUSY_Clr             = 0x2034,
            CHDONE_Clr             = 0x2038,
            DBGHALT_Clr            = 0x203C,
            SWREQ_Clr              = 0x2040,
            REQDIS_Clr             = 0x2044,
            REQPEND_Clr            = 0x2048,
            LINKLOAD_Clr           = 0x204C,
            REQCLEAR_Clr           = 0x2050,
            IF_Clr                 = 0x2054,
            IEN_Clr                = 0x2058,
            REQABORT_Clr           = 0x205C,
            ABORTSTATUS_Clr        = 0x2060,
            CH0_CFG_Clr            = 0x2070,
            CH0_LOOP_Clr           = 0x2074,
            CH0_CTRL_Clr           = 0x2078,
            CH0_SRC_Clr            = 0x207C,
            CH0_DST_Clr            = 0x2080,
            CH0_LINK_Clr           = 0x2084,
            CH0_XCTRL_Clr          = 0x2088,
            CH0_DUALDST_Clr        = 0x208C,
            CH0_ILSRC_Clr          = 0x2090,
            CH1_CFG_Clr            = 0x20A0,
            CH1_LOOP_Clr           = 0x20A4,
            CH1_CTRL_Clr           = 0x20A8,
            CH1_SRC_Clr            = 0x20AC,
            CH1_DST_Clr            = 0x20B0,
            CH1_LINK_Clr           = 0x20B4,
            CH1_XCTRL_Clr          = 0x20B8,
            CH1_DUALDST_Clr        = 0x20BC,
            CH1_ILSRC_Clr          = 0x20C0,
            CH2_CFG_Clr            = 0x20D0,
            CH2_LOOP_Clr           = 0x20D4,
            CH2_CTRL_Clr           = 0x20D8,
            CH2_SRC_Clr            = 0x20DC,
            CH2_DST_Clr            = 0x20E0,
            CH2_LINK_Clr           = 0x20E4,
            CH2_XCTRL_Clr          = 0x20E8,
            CH2_DUALDST_Clr        = 0x20EC,
            CH2_ILSRC_Clr          = 0x20F0,
            CH3_CFG_Clr            = 0x2100,
            CH3_LOOP_Clr           = 0x2104,
            CH3_CTRL_Clr           = 0x2108,
            CH3_SRC_Clr            = 0x210C,
            CH3_DST_Clr            = 0x2110,
            CH3_LINK_Clr           = 0x2114,
            CH3_XCTRL_Clr          = 0x2118,
            CH3_DUALDST_Clr        = 0x211C,
            CH3_ILSRC_Clr          = 0x2120,
            CH4_CFG_Clr            = 0x2130,
            CH4_LOOP_Clr           = 0x2134,
            CH4_CTRL_Clr           = 0x2138,
            CH4_SRC_Clr            = 0x213C,
            CH4_DST_Clr            = 0x2140,
            CH4_LINK_Clr           = 0x2144,
            CH4_XCTRL_Clr          = 0x2148,
            CH4_DUALDST_Clr        = 0x214C,
            CH4_ILSRC_Clr          = 0x2150,
            CH5_CFG_Clr            = 0x2160,
            CH5_LOOP_Clr           = 0x2164,
            CH5_CTRL_Clr           = 0x2168,
            CH5_SRC_Clr            = 0x216C,
            CH5_DST_Clr            = 0x2170,
            CH5_LINK_Clr           = 0x2174,
            CH5_XCTRL_Clr          = 0x2178,
            CH5_DUALDST_Clr        = 0x217C,
            CH5_ILSRC_Clr          = 0x2180,
            CH6_CFG_Clr            = 0x2190,
            CH6_LOOP_Clr           = 0x2194,
            CH6_CTRL_Clr           = 0x2198,
            CH6_SRC_Clr            = 0x219C,
            CH6_DST_Clr            = 0x21A0,
            CH6_LINK_Clr           = 0x21A4,
            CH6_XCTRL_Clr          = 0x21A8,
            CH6_DUALDST_Clr        = 0x21AC,
            CH6_ILSRC_Clr          = 0x21B0,
            CH7_CFG_Clr            = 0x21C0,
            CH7_LOOP_Clr           = 0x21C4,
            CH7_CTRL_Clr           = 0x21C8,
            CH7_SRC_Clr            = 0x21CC,
            CH7_DST_Clr            = 0x21D0,
            CH7_LINK_Clr           = 0x21D4,
            CH7_XCTRL_Clr          = 0x21D8,
            CH7_DUALDST_Clr        = 0x21DC,
            CH7_ILSRC_Clr          = 0x21E0,
            // Toggle registers
            IPVERSION_Tgl          = 0x3000,
            EN_Tgl                 = 0x3004,
            SWRST_Tgl              = 0x3008,
            CTRL_Tgl               = 0x300C,
            STATUS_Tgl             = 0x3010,
            SYNCSWSET_Tgl          = 0x3014,
            SYNCSWCLR_Tgl          = 0x3018,
            SYNCHWEN_Tgl           = 0x301C,
            SYNCHWSEL_Tgl          = 0x3020,
            SYNCSTATUS_Tgl         = 0x3024,
            CHEN_Tgl               = 0x3028,
            CHDIS_Tgl              = 0x302C,
            CHSTATUS_Tgl           = 0x3030,
            CHBUSY_Tgl             = 0x3034,
            CHDONE_Tgl             = 0x3038,
            DBGHALT_Tgl            = 0x303C,
            SWREQ_Tgl              = 0x3040,
            REQDIS_Tgl             = 0x3044,
            REQPEND_Tgl            = 0x3048,
            LINKLOAD_Tgl           = 0x304C,
            REQCLEAR_Tgl           = 0x3050,
            IF_Tgl                 = 0x3054,
            IEN_Tgl                = 0x3058,
            REQABORT_Tgl           = 0x305C,
            ABORTSTATUS_Tgl        = 0x3060,
            CH0_CFG_Tgl            = 0x3070,
            CH0_LOOP_Tgl           = 0x3074,
            CH0_CTRL_Tgl           = 0x3078,
            CH0_SRC_Tgl            = 0x307C,
            CH0_DST_Tgl            = 0x3080,
            CH0_LINK_Tgl           = 0x3084,
            CH0_XCTRL_Tgl          = 0x3088,
            CH0_DUALDST_Tgl        = 0x308C,
            CH0_ILSRC_Tgl          = 0x3090,
            CH1_CFG_Tgl            = 0x30A0,
            CH1_LOOP_Tgl           = 0x30A4,
            CH1_CTRL_Tgl           = 0x30A8,
            CH1_SRC_Tgl            = 0x30AC,
            CH1_DST_Tgl            = 0x30B0,
            CH1_LINK_Tgl           = 0x30B4,
            CH1_XCTRL_Tgl          = 0x30B8,
            CH1_DUALDST_Tgl        = 0x30BC,
            CH1_ILSRC_Tgl          = 0x30C0,
            CH2_CFG_Tgl            = 0x30D0,
            CH2_LOOP_Tgl           = 0x30D4,
            CH2_CTRL_Tgl           = 0x30D8,
            CH2_SRC_Tgl            = 0x30DC,
            CH2_DST_Tgl            = 0x30E0,
            CH2_LINK_Tgl           = 0x30E4,
            CH2_XCTRL_Tgl          = 0x30E8,
            CH2_DUALDST_Tgl        = 0x30EC,
            CH2_ILSRC_Tgl          = 0x30F0,
            CH3_CFG_Tgl            = 0x3100,
            CH3_LOOP_Tgl           = 0x3104,
            CH3_CTRL_Tgl           = 0x3108,
            CH3_SRC_Tgl            = 0x310C,
            CH3_DST_Tgl            = 0x3110,
            CH3_LINK_Tgl           = 0x3114,
            CH3_XCTRL_Tgl          = 0x3118,
            CH3_DUALDST_Tgl        = 0x311C,
            CH3_ILSRC_Tgl          = 0x3120,
            CH4_CFG_Tgl            = 0x3130,
            CH4_LOOP_Tgl           = 0x3134,
            CH4_CTRL_Tgl           = 0x3138,
            CH4_SRC_Tgl            = 0x313C,
            CH4_DST_Tgl            = 0x3140,
            CH4_LINK_Tgl           = 0x3144,
            CH4_XCTRL_Tgl          = 0x3148,
            CH4_DUALDST_Tgl        = 0x314C,
            CH4_ILSRC_Tgl          = 0x3150,
            CH5_CFG_Tgl            = 0x3160,
            CH5_LOOP_Tgl           = 0x3164,
            CH5_CTRL_Tgl           = 0x3168,
            CH5_SRC_Tgl            = 0x316C,
            CH5_DST_Tgl            = 0x3170,
            CH5_LINK_Tgl           = 0x3174,
            CH5_XCTRL_Tgl          = 0x3178,
            CH5_DUALDST_Tgl        = 0x317C,
            CH5_ILSRC_Tgl          = 0x3180,
            CH6_CFG_Tgl            = 0x3190,
            CH6_LOOP_Tgl           = 0x3194,
            CH6_CTRL_Tgl           = 0x3198,
            CH6_SRC_Tgl            = 0x319C,
            CH6_DST_Tgl            = 0x31A0,
            CH6_LINK_Tgl           = 0x31A4,
            CH6_XCTRL_Tgl          = 0x31A8,
            CH6_DUALDST_Tgl        = 0x31AC,
            CH6_ILSRC_Tgl          = 0x31B0,
            CH7_CFG_Tgl            = 0x31C0,
            CH7_LOOP_Tgl           = 0x31C4,
            CH7_CTRL_Tgl           = 0x31C8,
            CH7_SRC_Tgl            = 0x31CC,
            CH7_DST_Tgl            = 0x31D0,
            CH7_LINK_Tgl           = 0x31D4,
            CH7_XCTRL_Tgl          = 0x31D8,
            CH7_DUALDST_Tgl        = 0x31DC,
            CH7_ILSRC_Tgl          = 0x31E0,
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
            XBAR_CH12_REQSEL       = 0x0024,
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
            // Set Registers
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
            XBAR_CH12_REQSEL_Set   = 0x1024,
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
            // Clear Registers
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
            XBAR_CH12_REQSEL_Clr   = 0x2024,
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
            // Toggle Registers
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
            XBAR_CH12_REQSEL_Tgl   = 0x3024,
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
    }
}
