using System;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class SiLabs_SEQACC_1 : SiLabs_SequencerAccelerator
    {
        public SiLabs_SEQACC_1(Machine machine, long frequency, SiLabs_IProtocolTimer protocolTimer = null)
            : base(machine, frequency, protocolTimer)
        {
        }

#region fields
        public bool LogRegisterAccess = false;
        public bool LogInterrupts = false;
#endregion

#region methods
        protected override uint ReadRegister(long offset, bool internal_read = false)
        {
            var result = 0U;
            long internal_offset = offset;

            // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
            if (offset >= SetRegisterOffset && offset < ClearRegisterOffset)
            {
                // Set register
                internal_offset = offset - SetRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Info, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            }
            else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset)
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Info, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            }
            else if (offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Info, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            }

            try
            {
                if (registersCollection.TryRead(internal_offset, out result))
                {
                    return result;
                }
            }
            finally
            {
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Info, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", internal_offset, (Registers)internal_offset, result);
                }
            }

            if (LogRegisterAccess && !internal_read)
            {
                this.Log(LogLevel.Warning, "Unhandled read at offset 0x{0:X} ({1}).", internal_offset, (Registers)internal_offset);

            }

            return 0;
        }

        protected override void WriteRegister(long offset, uint value, bool internal_write = false)
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                long internal_offset = offset;
                uint internal_value = value;

                if (offset >= SetRegisterOffset && offset < ClearRegisterOffset)
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value | value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Info, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                    }
                }
                else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset)
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value & ~value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Info, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                    }
                }
                else if (offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value ^ value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Info, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                    }
                }

                if (LogRegisterAccess)
                {
                    this.Log(LogLevel.Info, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);
                }
                if (!registersCollection.TryWrite(internal_offset, internal_value) && LogRegisterAccess)
                {
                    this.Log(LogLevel.Warning, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);
                    return;
                }
            });
        }

        protected override DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithFlag(0, out enable, name: "ENABLE")
                    .WithTaggedFlag("DISABLING", 1)
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Config, new DoubleWordRegister(this, 0x00000180)
                    .WithTag("NUMFIXED", 0, 4)
                    .WithValueField(4, 5, out baseAddressPosition, name: "BASEPOS")
                    .WithReservedBits(9, 7)
                    .WithEnumField<DoubleWordRegister, TimeBase>(16, 2, out timeBase, name: "TIMEBASESEL")
                    .WithReservedBits(18, 2)
                    .WithTag("AHBRDDEL", 20, 3)
                    .WithReservedBits(23, 7)
                    .WithTaggedFlag("DISABSEQERR", 30)
                    .WithTaggedFlag("DISABBUSERR", 31)
                },
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Set, writeCallback: (_, value) => SequenceStart((uint)value, false), name: "SEQSTART")
                    .WithReservedBits(8, 8)
                    .WithValueField(16, 8, FieldMode.Set, writeCallback: (_, value) => SequenceAbort((uint)value), name: "ABORT")
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.Control2, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if (value) { ResetAbsoluteDelayCounter(); } }, name: "ABSDELRST")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => GetPendingSequencesBitmask(), name: "SEQPEND")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.Busy, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => GetRunningSequencesBitmask(), name: "SEQBUSY")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.SequencerAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => GetRunningSequenceCurrentAddress(), name: "SEQADDR")
                },
                {(long)Registers.ErrorAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out lastBusErrorAddress, FieldMode.Read, name: "ERRADDR")
                },
                {(long)Registers.EqualConditionMask0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out equalMaskCondition0, name: "EQMASK0")
                },
                {(long)Registers.EqualConditionMask1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out equalMaskCondition1, name: "EQMASK1")
                },
                {(long)Registers.Spare0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out spare0, name: "SPARE0")
                },
                {(long)Registers.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out sequence[0].sequenceDoneInterrupt, name: "SEQDONE0IF")
                    .WithFlag(1, out sequence[1].sequenceDoneInterrupt, name: "SEQDONE1IF")
                    .WithFlag(2, out sequence[2].sequenceDoneInterrupt, name: "SEQDONE2IF")
                    .WithFlag(3, out sequence[3].sequenceDoneInterrupt, name: "SEQDONE3IF")
                    .WithFlag(4, out sequence[4].sequenceDoneInterrupt, name: "SEQDONE4IF")
                    .WithFlag(5, out sequence[5].sequenceDoneInterrupt, name: "SEQDONE5IF")
                    .WithFlag(6, out sequence[6].sequenceDoneInterrupt, name: "SEQDONE6IF")
                    .WithFlag(7, out sequence[7].sequenceDoneInterrupt, name: "SEQDONE7IF")
                    .WithReservedBits(8, 20)
                    .WithFlag(29, out sequenceAbortedInterrupt, name: "SEQABORTIF")
                    .WithFlag(30, out sequenceErrorInterrupt, name: "SEQERRORIF")
                    .WithFlag(31, out busErrorInterrupt, name: "BUSERRORIF")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out sequence[0].sequenceDoneInterruptEnable, name: "SEQDONE0IEN")
                    .WithFlag(1, out sequence[1].sequenceDoneInterruptEnable, name: "SEQDONE1IEN")
                    .WithFlag(2, out sequence[2].sequenceDoneInterruptEnable, name: "SEQDONE2IEN")
                    .WithFlag(3, out sequence[3].sequenceDoneInterruptEnable, name: "SEQDONE3IEN")
                    .WithFlag(4, out sequence[4].sequenceDoneInterruptEnable, name: "SEQDONE4IEN")
                    .WithFlag(5, out sequence[5].sequenceDoneInterruptEnable, name: "SEQDONE5IEN")
                    .WithFlag(6, out sequence[6].sequenceDoneInterruptEnable, name: "SEQDONE6IEN")
                    .WithFlag(7, out sequence[7].sequenceDoneInterruptEnable, name: "SEQDONE7IEN")
                    .WithReservedBits(8, 20)
                    .WithFlag(29, out sequenceAbortedInterruptEnable, name: "SEQABORTIEN")
                    .WithFlag(30, out sequenceErrorInterruptEnable, name: "SEQERRORIEN")
                    .WithFlag(31, out busErrorInterruptEnable, name: "BUSERRORIEN")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.SequencerInterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out sequence[0].seqSequenceDoneInterrupt, name: "SEQDONE0SEQIF")
                    .WithFlag(1, out sequence[1].seqSequenceDoneInterrupt, name: "SEQDONE1SEQIF")
                    .WithFlag(2, out sequence[2].seqSequenceDoneInterrupt, name: "SEQDONE2SEQIF")
                    .WithFlag(3, out sequence[3].seqSequenceDoneInterrupt, name: "SEQDONE3SEQIF")
                    .WithFlag(4, out sequence[4].seqSequenceDoneInterrupt, name: "SEQDONE4SEQIF")
                    .WithFlag(5, out sequence[5].seqSequenceDoneInterrupt, name: "SEQDONE5SEQIF")
                    .WithFlag(6, out sequence[6].seqSequenceDoneInterrupt, name: "SEQDONE6SEQIF")
                    .WithFlag(7, out sequence[7].seqSequenceDoneInterrupt, name: "SEQDONE7SEQIF")
                    .WithReservedBits(8, 20)
                    .WithFlag(29, out seqSequenceAbortedInterrupt, name: "SEQABORTSEQIF")
                    .WithFlag(30, out seqSequenceErrorInterrupt, name: "SEQERRORSEQIF")
                    .WithFlag(31, out seqBusErrorInterrupt, name: "BUSERRORSEQIF")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.SequencerInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out sequence[0].seqSequenceDoneInterruptEnable, name: "SEQDONE0SEQIEN")
                    .WithFlag(1, out sequence[1].seqSequenceDoneInterruptEnable, name: "SEQDONE1SEQIEN")
                    .WithFlag(2, out sequence[2].seqSequenceDoneInterruptEnable, name: "SEQDONE2SEQIEN")
                    .WithFlag(3, out sequence[3].seqSequenceDoneInterruptEnable, name: "SEQDONE3SEQIEN")
                    .WithFlag(4, out sequence[4].seqSequenceDoneInterruptEnable, name: "SEQDONE4SEQIEN")
                    .WithFlag(5, out sequence[5].seqSequenceDoneInterruptEnable, name: "SEQDONE5SEQIEN")
                    .WithFlag(6, out sequence[6].seqSequenceDoneInterruptEnable, name: "SEQDONE6SEQIEN")
                    .WithFlag(7, out sequence[7].seqSequenceDoneInterruptEnable, name: "SEQDONE7SEQIEN")
                    .WithReservedBits(8, 20)
                    .WithFlag(29, out seqSequenceAbortedInterruptEnable, name: "SEQABORTSEQIEN")
                    .WithFlag(30, out seqSequenceErrorInterruptEnable, name: "SEQERRORSEQIEN")
                    .WithFlag(31, out seqBusErrorInterruptEnable, name: "BUSERRORSEQIEN")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },

                // TODO: implement FSW IF/IEN
            };

            var startOffset = (long)Registers.StartAddress0;
            var startAddressOffset = (long)Registers.StartAddress0 - startOffset;
            var configOffset = (long)Registers.SequenceConfig0 - startOffset;
            var blockSize = (long)Registers.StartAddress1 - (long)Registers.StartAddress0;

            for (var index = 0; index < NumberOfSequenceConfigurations; index++)
            {
                var i = index;
                // StartAddress
                registerDictionary.Add(startOffset + blockSize * i + startAddressOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out sequence[i].startAddress, name: "STARTADDR")
                );
                // SequenceConfig
                registerDictionary.Add(startOffset + blockSize * i + configOffset,
                    new DoubleWordRegister(this, 0x10)
                        .WithValueField(0, 5, out sequence[i].continuousWritePosition, name: "CNTWRPOS")
                        .WithEnumField<DoubleWordRegister, HardwareStartSelect>(5, 5, out sequence[i].hardwareStartSelect, name: "HWSTSEL")
                        .WithEnumField<DoubleWordRegister, HardwareTriggerMode>(10, 3, out sequence[i].hardwareTriggerMode, name: "HWSTTRIG")
                        .WithFlag(13, out sequence[i].disableAbsoluteDelayReset, name: "DISABSRST")
                        .WithFlag(14, out sequence[i].movSwapAddresses, name: "MOVSWAP")
                        .WithReservedBits(15, 17)
                );
            }

            startOffset = (long)Registers.BaseAddress0;
            var baseAddressOffset = (long)Registers.BaseAddress0 - startOffset;
            blockSize = (long)Registers.BaseAddress1 - (long)Registers.BaseAddress0;

            for (var index = 0; index < NumberOfBaseAddresses; index++)
            {
                var i = index;
                // BaseAddress
                registerDictionary.Add(startOffset + blockSize * i + baseAddressOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out baseAddress[i], name: "BASEADDR")
                );
            }

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                // Host interrupt
                var irq = ((sequenceAbortedInterrupt.Value && sequenceAbortedInterruptEnable.Value)
                       || (sequenceErrorInterrupt.Value && sequenceErrorInterruptEnable.Value)
                       || (busErrorInterrupt.Value && busErrorInterruptEnable.Value));
                Array.ForEach(sequence, x => irq |= x.Interrupt);
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    registersCollection.TryRead((long)Registers.InterruptFlags, out IF);
                    registersCollection.TryRead((long)Registers.InterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: Host IRQ set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                HostIRQ.Set(irq);

                // Sequencer interrupt
                irq = ((seqSequenceAbortedInterrupt.Value && seqSequenceAbortedInterruptEnable.Value)
                       || (seqSequenceErrorInterrupt.Value && seqSequenceErrorInterruptEnable.Value)
                       || (seqBusErrorInterrupt.Value && seqBusErrorInterruptEnable.Value));
                Array.ForEach(sequence, x => irq |= x.SeqInterrupt);
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    registersCollection.TryRead((long)Registers.SequencerInterruptFlags, out IF);
                    registersCollection.TryRead((long)Registers.SequencerInterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: Sequencer IRQ set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                SequencerIRQ.Set(irq);
            });
        }
#endregion


#region enums
        protected enum Registers : long
        {
            IpVersion = 0x0000,
            Enable = 0x0004,
            SoftwareReset = 0x0008,
            Config = 0x0010,
            Control = 0x0014,
            Status = 0x0018,
            Busy = 0x001C,
            InterruptFlags = 0x0020,
            InterruptEnable = 0x0024,
            SequencerInterruptFlags = 0x0028,
            SequencerInterruptEnable = 0x002C,
            Control2 = 0x0030,
            SequencerAddress = 0x0034,
            ErrorAddress = 0x0038,
            EqualConditionMask0 = 0x003C,
            EqualConditionMask1 = 0x0040,
            Spare0 = 0x0044,
            FastSwitchInterruptFlags = 0x0048,
            FastSwitchInterruptEnable = 0x004C,
            StartAddress0 = 0x0050,
            SequenceConfig0 = 0x0054,
            StartAddress1 = 0x0058,
            SequenceConfig1 = 0x005C,
            StartAddress2 = 0x0060,
            SequenceConfig2 = 0x0064,
            StartAddress3 = 0x0068,
            SequenceConfig3 = 0x006C,
            StartAddress4 = 0x0070,
            SequenceConfig4 = 0x0074,
            StartAddress5 = 0x0078,
            SequenceConfig5 = 0x007C,
            StartAddress6 = 0x0080,
            SequenceConfig6 = 0x0084,
            StartAddress7 = 0x0088,
            SequenceConfig7 = 0x008C,
            BaseAddress0 = 0x00D0,
            BaseAddress1 = 0x00D4,
            BaseAddress2 = 0x00D8,
            BaseAddress3 = 0x00DC,
            BaseAddress4 = 0x00E0,
            BaseAddress5 = 0x00E4,
            BaseAddress6 = 0x00E8,
            BaseAddress7 = 0x00EC,
            BaseAddress8 = 0x00F0,
            BaseAddress9 = 0x00F4,
            BaseAddress10 = 0x00F8,
            BaseAddress11 = 0x00FC,
            BaseAddress12 = 0x0100,
            BaseAddress13 = 0x0104,
            BaseAddress14 = 0x0108,
            BaseAddress15 = 0x010C,
            // Set registers
            IpVersion_Set = 0x1000,
            Enable_Set = 0x1004,
            SoftwareReset_Set = 0x1008,
            Config_Set = 0x1010,
            Control_Set = 0x1014,
            Status_Set = 0x1018,
            Busy_Set = 0x101C,
            InterruptFlags_Set = 0x1020,
            InterruptEnable_Set = 0x1024,
            SequencerInterruptFlags_Set = 0x1028,
            SequencerInterruptEnable_Set = 0x102C,
            Control2_Set = 0x1030,
            SequencerAddress_Set = 0x1034,
            ErrorAddress_Set = 0x1038,
            EqualConditionMask0_Set = 0x103C,
            EqualConditionMask1_Set = 0x1040,
            Spare0_Set = 0x1044,
            FastSwitchInterruptFlags_Set = 0x1048,
            FastSwitchInterruptEnable_Set = 0x104C,
            StartAddress0_Set = 0x1050,
            SequenceConfig0_Set = 0x1054,
            StartAddress1_Set = 0x1058,
            SequenceConfig1_Set = 0x105C,
            StartAddress2_Set = 0x1060,
            SequenceConfig2_Set = 0x1064,
            StartAddress3_Set = 0x1068,
            SequenceConfig3_Set = 0x106C,
            StartAddress4_Set = 0x1070,
            SequenceConfig4_Set = 0x1074,
            StartAddress5_Set = 0x1078,
            SequenceConfig5_Set = 0x107C,
            StartAddress6_Set = 0x1080,
            SequenceConfig6_Set = 0x1084,
            StartAddress7_Set = 0x1088,
            SequenceConfig7_Set = 0x108C,
            BaseAddress0_Set = 0x10D0,
            BaseAddress1_Set = 0x10D4,
            BaseAddress2_Set = 0x10D8,
            BaseAddress3_Set = 0x10DC,
            BaseAddress4_Set = 0x10E0,
            BaseAddress5_Set = 0x10E4,
            BaseAddress6_Set = 0x10E8,
            BaseAddress7_Set = 0x10EC,
            BaseAddress8_Set = 0x10F0,
            BaseAddress9_Set = 0x10F4,
            BaseAddress10_Set = 0x10F8,
            BaseAddress11_Set = 0x10FC,
            BaseAddress12_Set = 0x1100,
            BaseAddress13_Set = 0x1104,
            BaseAddress14_Set = 0x1108,
            BaseAddress15_Set = 0x110C,
            // Clear registers
            IpVersion_Clr = 0x2000,
            Enable_Clr = 0x2004,
            SoftwareReset_Clr = 0x2008,
            Config_Clr = 0x2010,
            Control_Clr = 0x2014,
            Status_Clr = 0x2018,
            Busy_Clr = 0x201C,
            InterruptFlags_Clr = 0x2020,
            InterruptEnable_Clr = 0x2024,
            SequencerInterruptFlags_Clr = 0x2028,
            SequencerInterruptEnable_Clr = 0x202C,
            Control2_Clr = 0x2030,
            SequencerAddress_Clr = 0x2034,
            ErrorAddress_Clr = 0x2038,
            EqualConditionMask0_Clr = 0x203C,
            EqualConditionMask1_Clr = 0x2040,
            Spare0_Clr = 0x2044,
            FastSwitchInterruptFlags_Clr = 0x2048,
            FastSwitchInterruptEnable_Clr = 0x204C,
            StartAddress0_Clr = 0x2050,
            SequenceConfig0_Clr = 0x2054,
            StartAddress1_Clr = 0x2058,
            SequenceConfig1_Clr = 0x205C,
            StartAddress2_Clr = 0x2060,
            SequenceConfig2_Clr = 0x2064,
            StartAddress3_Clr = 0x2068,
            SequenceConfig3_Clr = 0x206C,
            StartAddress4_Clr = 0x2070,
            SequenceConfig4_Clr = 0x2074,
            StartAddress5_Clr = 0x2078,
            SequenceConfig5_Clr = 0x207C,
            StartAddress6_Clr = 0x2080,
            SequenceConfig6_Clr = 0x2084,
            StartAddress7_Clr = 0x2088,
            SequenceConfig7_Clr = 0x208C,
            BaseAddress0_Clr = 0x20D0,
            BaseAddress1_Clr = 0x20D4,
            BaseAddress2_Clr = 0x20D8,
            BaseAddress3_Clr = 0x20DC,
            BaseAddress4_Clr = 0x20E0,
            BaseAddress5_Clr = 0x20E4,
            BaseAddress6_Clr = 0x20E8,
            BaseAddress7_Clr = 0x20EC,
            BaseAddress8_Clr = 0x20F0,
            BaseAddress9_Clr = 0x20F4,
            BaseAddress10_Clr = 0x20F8,
            BaseAddress11_Clr = 0x20FC,
            BaseAddress12_Clr = 0x2100,
            BaseAddress13_Clr = 0x2104,
            BaseAddress14_Clr = 0x2108,
            BaseAddress15_Clr = 0x210C,
            // Toggle registers
            IpVersion_Tgl = 0x3000,
            Enable_Tgl = 0x3004,
            SoftwareReset_Tgl = 0x3008,
            Config_Tgl = 0x3010,
            Control_Tgl = 0x3014,
            Status_Tgl = 0x3018,
            Busy_Tgl = 0x301C,
            InterruptFlags_Tgl = 0x3020,
            InterruptEnable_Tgl = 0x3024,
            SequencerInterruptFlags_Tgl = 0x3028,
            SequencerInterruptEnable_Tgl = 0x302C,
            Control2_Tgl = 0x3030,
            SequencerAddress_Tgl = 0x3034,
            ErrorAddress_Tgl = 0x3038,
            EqualConditionMask0_Tgl = 0x303C,
            EqualConditionMask1_Tgl = 0x3040,
            Spare0_Tgl = 0x3044,
            FastSwitchInterruptFlags_Tgl = 0x3048,
            FastSwitchInterruptEnable_Tgl = 0x304C,
            StartAddress0_Tgl = 0x3050,
            SequenceConfig0_Tgl = 0x3054,
            StartAddress1_Tgl = 0x3058,
            SequenceConfig1_Tgl = 0x305C,
            StartAddress2_Tgl = 0x3060,
            SequenceConfig2_Tgl = 0x3064,
            StartAddress3_Tgl = 0x3068,
            SequenceConfig3_Tgl = 0x306C,
            StartAddress4_Tgl = 0x3070,
            SequenceConfig4_Tgl = 0x3074,
            StartAddress5_Tgl = 0x3078,
            SequenceConfig5_Tgl = 0x307C,
            StartAddress6_Tgl = 0x3080,
            SequenceConfig6_Tgl = 0x3084,
            StartAddress7_Tgl = 0x3088,
            SequenceConfig7_Tgl = 0x308C,
            BaseAddress0_Tgl = 0x30D0,
            BaseAddress1_Tgl = 0x30D4,
            BaseAddress2_Tgl = 0x30D8,
            BaseAddress3_Tgl = 0x30DC,
            BaseAddress4_Tgl = 0x30E0,
            BaseAddress5_Tgl = 0x30E4,
            BaseAddress6_Tgl = 0x30E8,
            BaseAddress7_Tgl = 0x30EC,
            BaseAddress8_Tgl = 0x30F0,
            BaseAddress9_Tgl = 0x30F4,
            BaseAddress10_Tgl = 0x30F8,
            BaseAddress11_Tgl = 0x30FC,
            BaseAddress12_Tgl = 0x3100,
            BaseAddress13_Tgl = 0x3104,
            BaseAddress14_Tgl = 0x3108,
            BaseAddress15_Tgl = 0x310C,
        }
    }
#endregion    
}