//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class SiLabs_BUFC_4 : SiLabs_BUFC_Base
    {
        public SiLabs_BUFC_4(Machine machine) : base(machine, NumberOfBuffers)
        {
        }

        protected override DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Reg_64, new DoubleWordRegister(this)
                    .WithFlag(0, out buffer[0].Field_6, name: "REG_64_FIELD_1")
                    .WithFlag(1, out buffer[0].Field_23, name: "REG_64_FIELD_2")
                    .WithFlag(2, out buffer[0].Field_19, name: "REG_64_FIELD_3")
                    .WithFlag(3, out buffer[0].Field_2, name: "REG_64_FIELD_4")
                    .WithFlag(4, out buffer[0].Field_4, name: "REG_64_FIELD_5")
                    .WithReservedBits(5, 3)
                    .WithFlag(8, out buffer[1].Field_6, name: "REG_64_FIELD_6")
                    .WithFlag(9, out buffer[1].Field_23, name: "REG_64_FIELD_7")
                    .WithFlag(10, out buffer[1].Field_19, name: "REG_64_FIELD_8")
                    .WithFlag(11, out buffer[1].Field_2, name: "REG_64_FIELD_9")
                    .WithFlag(12, out buffer[1].Field_4, name: "REG_64_FIELD_10")
                    .WithReservedBits(13, 3)
                    .WithFlag(16, out buffer[2].Field_6, name: "REG_64_FIELD_11")
                    .WithFlag(17, out buffer[2].Field_23, name: "REG_64_FIELD_12")
                    .WithFlag(18, out buffer[2].Field_19, name: "REG_64_FIELD_13")
                    .WithFlag(19, out buffer[2].Field_2, name: "REG_64_FIELD_14")
                    .WithFlag(20, out buffer[2].Field_4, name: "REG_64_FIELD_15")
                    .WithReservedBits(21, 3)
                    .WithFlag(24, out buffer[3].Field_6, name: "REG_64_FIELD_16")
                    .WithFlag(25, out buffer[3].Field_23, name: "REG_64_FIELD_17")
                    .WithFlag(26, out buffer[3].Field_19, name: "REG_64_FIELD_18")
                    .WithFlag(27, out buffer[3].Field_2, name: "REG_64_FIELD_19")
                    .WithFlag(28, out buffer[3].Field_4, name: "REG_64_FIELD_20")
                    .WithReservedBits(29, 2)
                    .WithTaggedFlag("REG_64_FIELD_21", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Reg_65, new DoubleWordRegister(this)
                    .WithFlag(0, out buffer[0].Field_5, name: "REG_65_FIELD_1")
                    .WithFlag(1, out buffer[0].Field_22, name: "REG_65_FIELD_2")
                    .WithFlag(2, out buffer[0].Field_18, name: "REG_65_FIELD_3")
                    .WithFlag(3, out buffer[0].Field_1, name: "REG_65_FIELD_4")
                    .WithFlag(4, out buffer[0].Field_3, name: "REG_65_FIELD_5")
                    .WithReservedBits(5, 3)
                    .WithFlag(8, out buffer[1].Field_5, name: "REG_65_FIELD_6")
                    .WithFlag(9, out buffer[1].Field_22, name: "REG_65_FIELD_7")
                    .WithFlag(10, out buffer[1].Field_18, name: "REG_65_FIELD_8")
                    .WithFlag(11, out buffer[1].Field_1, name: "REG_65_FIELD_9")
                    .WithFlag(12, out buffer[1].Field_3, name: "REG_65_FIELD_10")
                    .WithReservedBits(13, 3)
                    .WithFlag(16, out buffer[2].Field_5, name: "REG_65_FIELD_11")
                    .WithFlag(17, out buffer[2].Field_22, name: "REG_65_FIELD_12")
                    .WithFlag(18, out buffer[2].Field_18, name: "REG_65_FIELD_13")
                    .WithFlag(19, out buffer[2].Field_1, name: "REG_65_FIELD_14")
                    .WithFlag(20, out buffer[2].Field_3, name: "REG_65_FIELD_15")
                    .WithReservedBits(21, 3)
                    .WithFlag(24, out buffer[3].Field_5, name: "REG_65_FIELD_16")
                    .WithFlag(25, out buffer[3].Field_22, name: "REG_65_FIELD_17")
                    .WithFlag(26, out buffer[3].Field_18, name: "REG_65_FIELD_18")
                    .WithFlag(27, out buffer[3].Field_1, name: "REG_65_FIELD_19")
                    .WithFlag(28, out buffer[3].Field_3, name: "REG_65_FIELD_20")
                    .WithReservedBits(29, 2)
                    .WithTaggedFlag("REG_65_FIELD_21", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Reg_66, new DoubleWordRegister(this)
                    .WithFlag(0, out buffer[0].Field_12, name: "REG_66_FIELD_1")
                    .WithFlag(1, out buffer[0].Field_16, name: "REG_66_FIELD_2")
                    .WithFlag(2, out buffer[0].Field_14, name: "REG_66_FIELD_3")
                    .WithFlag(3, out buffer[0].Field_8, name: "REG_66_FIELD_4")
                    .WithFlag(4, out buffer[0].Field_10, name: "REG_66_FIELD_5")
                    .WithReservedBits(5, 3)
                    .WithFlag(8, out buffer[1].Field_12, name: "REG_66_FIELD_6")
                    .WithFlag(9, out buffer[1].Field_16, name: "REG_66_FIELD_7")
                    .WithFlag(10, out buffer[1].Field_14, name: "REG_66_FIELD_8")
                    .WithFlag(11, out buffer[1].Field_8, name: "REG_66_FIELD_9")
                    .WithFlag(12, out buffer[1].Field_10, name: "REG_66_FIELD_10")
                    .WithReservedBits(13, 3)
                    .WithFlag(16, out buffer[2].Field_12, name: "REG_66_FIELD_11")
                    .WithFlag(17, out buffer[2].Field_16, name: "REG_66_FIELD_12")
                    .WithFlag(18, out buffer[2].Field_14, name: "REG_66_FIELD_13")
                    .WithFlag(19, out buffer[2].Field_8, name: "REG_66_FIELD_14")
                    .WithFlag(20, out buffer[2].Field_10, name: "REG_66_FIELD_15")
                    .WithReservedBits(21, 3)
                    .WithFlag(24, out buffer[3].Field_12, name: "REG_66_FIELD_16")
                    .WithFlag(25, out buffer[3].Field_16, name: "REG_66_FIELD_17")
                    .WithFlag(26, out buffer[3].Field_14, name: "REG_66_FIELD_18")
                    .WithFlag(27, out buffer[3].Field_8, name: "REG_66_FIELD_19")
                    .WithFlag(28, out buffer[3].Field_10, name: "REG_66_FIELD_20")
                    .WithReservedBits(29, 2)
                    .WithTaggedFlag("REG_66_FIELD_21", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Reg_67, new DoubleWordRegister(this)
                    .WithFlag(0, out buffer[0].Field_11, name: "REG_67_FIELD_1")
                    .WithFlag(1, out buffer[0].Field_15, name: "REG_67_FIELD_2")
                    .WithFlag(2, out buffer[0].Field_13, name: "REG_67_FIELD_3")
                    .WithFlag(3, out buffer[0].Field_7, name: "REG_67_FIELD_4")
                    .WithFlag(4, out buffer[0].Field_9, name: "REG_67_FIELD_5")
                    .WithReservedBits(5, 3)
                    .WithFlag(8, out buffer[1].Field_11, name: "REG_67_FIELD_6")
                    .WithFlag(9, out buffer[1].Field_15, name: "REG_67_FIELD_7")
                    .WithFlag(10, out buffer[1].Field_13, name: "REG_67_FIELD_8")
                    .WithFlag(11, out buffer[1].Field_7, name: "REG_67_FIELD_9")
                    .WithFlag(12, out buffer[1].Field_9, name: "REG_67_FIELD_10")
                    .WithReservedBits(13, 3)
                    .WithFlag(16, out buffer[2].Field_11, name: "REG_67_FIELD_11")
                    .WithFlag(17, out buffer[2].Field_15, name: "REG_67_FIELD_12")
                    .WithFlag(18, out buffer[2].Field_13, name: "REG_67_FIELD_13")
                    .WithFlag(19, out buffer[2].Field_7, name: "REG_67_FIELD_14")
                    .WithFlag(20, out buffer[2].Field_9, name: "REG_67_FIELD_15")
                    .WithReservedBits(21, 3)
                    .WithFlag(24, out buffer[3].Field_11, name: "REG_67_FIELD_16")
                    .WithFlag(25, out buffer[3].Field_15, name: "REG_67_FIELD_17")
                    .WithFlag(26, out buffer[3].Field_13, name: "REG_67_FIELD_18")
                    .WithFlag(27, out buffer[3].Field_7, name: "REG_67_FIELD_19")
                    .WithFlag(28, out buffer[3].Field_9, name: "REG_67_FIELD_20")
                    .WithReservedBits(29, 2)
                    .WithTaggedFlag("REG_67_FIELD_21", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };

            var startOffset = (long)Registers.Reg_4;
            var controlOffset = (long)Registers.Reg_4 - startOffset;
            var addrOffset = (long)Registers.Reg_5 - startOffset;
            var wOffOffset = (long)Registers.Reg_6 - startOffset;
            var rOffOffset = (long)Registers.Reg_7 - startOffset;
            var wStartOffset = (long)Registers.Reg_8 - startOffset;
            var rDataOffset = (long)Registers.Reg_9 - startOffset;
            var wDataOffset = (long)Registers.Reg_10 - startOffset;
            var xWriteOffset = (long)Registers.Reg_11 - startOffset;
            var statusOffset = (long)Registers.Reg_12 - startOffset;
            var thresOffset = (long)Registers.Reg_13 - startOffset;
            var cmdOffset = (long)Registers.Reg_14 - startOffset;
            var rData32Offset = (long)Registers.Reg_16 - startOffset;
            var wData32Offset = (long)Registers.Reg_17 - startOffset;
            var xWrite32Offset = (long)Registers.Reg_18 - startOffset;
            var blockSize = (long)Registers.Reg_19 - (long)Registers.Reg_4;
            for(var index = 0; index < NumberOfBuffers; index++)
            {
                var i = index;
                registerDictionary.Add(startOffset + blockSize * i + controlOffset,
                    new DoubleWordRegister(this)
                        .WithEnumField<DoubleWordRegister, BUFC_SizeMode>(0, 3, out buffer[i].Field_17, name: "REG_67_FIELD_22")
                        .WithReservedBits(3, 29));
                registerDictionary.Add(startOffset + blockSize * i + addrOffset,
                    new DoubleWordRegister(this, 0x8000000)
                        .WithValueField(0, 32, valueProviderCallback: _ => buffer[i].Address, writeCallback: (_, value) => buffer[i].Address = (uint)(value & 0xFFFFFFFC), name: "REG_67_FIELD_23"));
                registerDictionary.Add(startOffset + blockSize * i + wOffOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 13, valueProviderCallback: _ => buffer[i].WriteOffset, writeCallback: (_, value) => buffer[i].WriteOffset = (uint)value, name: "REG_67_FIELD_24")
                        .WithReservedBits(13, 19));
                registerDictionary.Add(startOffset + blockSize * i + rOffOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 13, valueProviderCallback: _ => buffer[i].ReadOffset, writeCallback: (_, value) => buffer[i].ReadOffset = (uint)value, name: "REG_67_FIELD_25")
                        .WithReservedBits(13, 19));
                registerDictionary.Add(startOffset + blockSize * i + wStartOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 13, out buffer[i].Field_24, FieldMode.Read, name: "REG_67_FIELD_26")
                        .WithReservedBits(13, 19));
                registerDictionary.Add(startOffset + blockSize * i + rDataOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => buffer[i].ReadData, name: "REG_67_FIELD_27")
                        .WithReservedBits(8, 24));
                registerDictionary.Add(startOffset + blockSize * i + wDataOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) => buffer[i].WriteData = (uint)value, name: "REG_67_FIELD_28")
                        .WithReservedBits(8, 24));
                registerDictionary.Add(startOffset + blockSize * i + xWriteOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) => buffer[i].XorWriteData = (uint)value, name: "REG_67_FIELD_29")
                        .WithReservedBits(8, 24));
                registerDictionary.Add(startOffset + blockSize * i + statusOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 13, FieldMode.Read, valueProviderCallback: _ => buffer[i].BytesNumber, name: "REG_67_FIELD_30")
                        .WithReservedBits(13, 3)
                        .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => buffer[i].ReadReady, name: "REG_67_FIELD_31")
                        .WithReservedBits(17, 3)
                        .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => buffer[i].ThresholdFlag, name: "REG_67_FIELD_32")
                        .WithReservedBits(21, 3)
                        .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => buffer[i].Read32Ready, name: "REG_67_FIELD_33")
                        .WithReservedBits(25, 7));
                registerDictionary.Add(startOffset + blockSize * i + thresOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 13, out buffer[i].Field_20, name: "REG_67_FIELD_34")
                        .WithEnumField<DoubleWordRegister, BUFC_ThresholdMode>(13, 1, out buffer[i].Field_21, name: "REG_67_FIELD_35")
                        .WithReservedBits(14, 18)
                        .WithChangeCallback((_, __) => buffer[i].UpdateThresholdFlag()));
                registerDictionary.Add(startOffset + blockSize * i + cmdOffset,
                    new DoubleWordRegister(this)
                        .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if(value) buffer[i].Clear(); }, name: "REG_67_FIELD_36")
                        .WithFlag(1, FieldMode.Set, writeCallback: (_, value) => { if(value) buffer[i].Prefetch(); }, name: "REG_67_FIELD_37")
                        .WithFlag(2, FieldMode.Set, writeCallback: (_, value) => { if(value) buffer[i].UpdateWriteStartOffset(); }, name: "REG_67_FIELD_38")
                        .WithFlag(3, FieldMode.Set, writeCallback: (_, value) => { if(value) buffer[i].RestoreWriteOffset(); }, name: "REG_67_FIELD_39")
                        .WithReservedBits(4, 28));
                registerDictionary.Add(startOffset + blockSize * i + rData32Offset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => buffer[i].ReadData32, name: "REG_67_FIELD_40"));
                registerDictionary.Add(startOffset + blockSize * i + wData32Offset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) => buffer[i].WriteData32 = (uint)value, name: "REG_67_FIELD_41"));
                registerDictionary.Add(startOffset + blockSize * i + xWrite32Offset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) => buffer[i].XorWriteData32 = (uint)value, name: "REG_67_FIELD_42"));
            }

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                bool irq = false;
                Array.ForEach(buffer, x => irq |= x.Interrupt);
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    registersCollection.TryRead((long)Registers.Reg_64, out interruptFlag);
                    registersCollection.TryRead((long)Registers.Reg_65, out interruptEnable);
                }
                IRQ.Set(irq);

                irq = false;
                Array.ForEach(buffer, x => irq |= x.SeqInterrupt);
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    registersCollection.TryRead((long)Registers.Reg_66, out interruptFlag);
                    registersCollection.TryRead((long)Registers.Reg_67, out interruptEnable);
                }
                SequencerIRQ.Set(irq);
            });
        }

        protected override Type RegistersType => typeof(Registers);

        private const uint NumberOfBuffers = 4;

        private enum Registers
        {
            Reg_1                 = 0x0000,
            Reg_2                 = 0x0004,
            Reg_3                 = 0x0008,
            Reg_4                 = 0x000C,
            Reg_5                 = 0x0010,
            Reg_6                 = 0x0014,
            Reg_7                 = 0x0018,
            Reg_8                 = 0x001C,
            Reg_9                 = 0x0020,
            Reg_10                = 0x0024,
            Reg_11                = 0x0028,
            Reg_12                = 0x002C,
            Reg_13                = 0x0030,
            Reg_14                = 0x0034,
            Reg_15                = 0x0038,
            Reg_16                = 0x003C,
            Reg_17                = 0x0040,
            Reg_18                = 0x0044,
            Reg_19                = 0x004C,
            Reg_20                = 0x0050,
            Reg_21                = 0x0054,
            Reg_22                = 0x0058,
            Reg_23                = 0x005C,
            Reg_24                = 0x0060,
            Reg_25                = 0x0064,
            Reg_26                = 0x0068,
            Reg_27                = 0x006C,
            Reg_28                = 0x0070,
            Reg_29                = 0x0074,
            Reg_30                = 0x0078,
            Reg_31                = 0x007C,
            Reg_32                = 0x0080,
            Reg_33                = 0X0084,
            Reg_34                = 0x008C,
            Reg_35                = 0x0090,
            Reg_36                = 0x0094,
            Reg_37                = 0x0098,
            Reg_38                = 0x009C,
            Reg_39                = 0x00A0,
            Reg_40                = 0x00A4,
            Reg_41                = 0x00A8,
            Reg_42                = 0x00AC,
            Reg_43                = 0x00B0,
            Reg_44                = 0x00B4,
            Reg_45                = 0x00B8,
            Reg_46                = 0x00BC,
            Reg_47                = 0x00C0,
            Reg_48                = 0x00C4,
            Reg_49                = 0x00CC,
            Reg_50                = 0x00D0,
            Reg_51                = 0x00D4,
            Reg_52                = 0x00D8,
            Reg_53                = 0x00DC,
            Reg_54                = 0x00E0,
            Reg_55                = 0x00E4,
            Reg_56                = 0x00E8,
            Reg_57                = 0x00EC,
            Reg_58                = 0x00F0,
            Reg_59                = 0x00F4,
            Reg_60                = 0x00F8,
            Reg_61                = 0x00FC,
            Reg_62                = 0x0100,
            Reg_63                = 0x0104,
            Reg_64                = 0x0114,
            Reg_65                = 0x0118,
            Reg_66                = 0x011C,
            Reg_67                = 0x0120,
            Reg_68                = 0x0124,

            Reg_69                = 0x1000,
            Reg_70                = 0x1004,
            Reg_71                = 0x1008,
            Reg_72                = 0x100C,
            Reg_73                = 0x1010,
            Reg_74                = 0x1014,
            Reg_75                = 0x1018,
            Reg_76                = 0x101C,
            Reg_77                = 0x1020,
            Reg_78                = 0x1024,
            Reg_79                = 0x1028,
            Reg_80                = 0x102C,
            Reg_81                = 0x1030,
            Reg_82                = 0x1034,
            Reg_83                = 0x1038,
            Reg_84                = 0x103C,
            Reg_85                = 0x1040,
            Reg_86                = 0x1044,
            Reg_87                = 0x104C,
            Reg_88                = 0x1050,
            Reg_89                = 0x1054,
            Reg_90                = 0x1058,
            Reg_91                = 0x105C,
            Reg_92                = 0x1060,
            Reg_93                = 0x1064,
            Reg_94                = 0x1068,
            Reg_95                = 0x106C,
            Reg_96                = 0x1070,
            Reg_97                = 0x1074,
            Reg_98                = 0x1078,
            Reg_99                = 0x107C,
            Reg_100               = 0x1080,
            Reg_101               = 0X1084,
            Reg_102               = 0x108C,
            Reg_103               = 0x1090,
            Reg_104               = 0x1094,
            Reg_105               = 0x1098,
            Reg_106               = 0x109C,
            Reg_107               = 0x10A0,
            Reg_108               = 0x10A4,
            Reg_109               = 0x10A8,
            Reg_110               = 0x10AC,
            Reg_111               = 0x10B0,
            Reg_112               = 0x10B4,
            Reg_113               = 0x10B8,
            Reg_114               = 0x10BC,
            Reg_115               = 0x10C0,
            Reg_116               = 0x10C4,
            Reg_117               = 0x10CC,
            Reg_118               = 0x10D0,
            Reg_119               = 0x10D4,
            Reg_120               = 0x10D8,
            Reg_121               = 0x10DC,
            Reg_122               = 0x10E0,
            Reg_123               = 0x10E4,
            Reg_124               = 0x10E8,
            Reg_125               = 0x10EC,
            Reg_126               = 0x10F0,
            Reg_127               = 0x10F4,
            Reg_128               = 0x10F8,
            Reg_129               = 0x10FC,
            Reg_130               = 0x1100,
            Reg_131               = 0x1104,
            Reg_132               = 0x1114,
            Reg_133               = 0x1118,
            Reg_134               = 0x111C,
            Reg_135               = 0x1120,
            Reg_136               = 0x1124,

            Reg_137               = 0x2000,
            Reg_138               = 0x2004,
            Reg_139               = 0x2008,
            Reg_140               = 0x200C,
            Reg_141               = 0x2010,
            Reg_142               = 0x2014,
            Reg_143               = 0x2018,
            Reg_144               = 0x201C,
            Reg_145               = 0x2020,
            Reg_146               = 0x2024,
            Reg_147               = 0x2028,
            Reg_148               = 0x202C,
            Reg_149               = 0x2030,
            Reg_150               = 0x2034,
            Reg_151               = 0x2038,
            Reg_152               = 0x203C,
            Reg_153               = 0x2040,
            Reg_154               = 0x2044,
            Reg_155               = 0x204C,
            Reg_156               = 0x2050,
            Reg_157               = 0x2054,
            Reg_158               = 0x2058,
            Reg_159               = 0x205C,
            Reg_160               = 0x2060,
            Reg_161               = 0x2064,
            Reg_162               = 0x2068,
            Reg_163               = 0x206C,
            Reg_164               = 0x2070,
            Reg_165               = 0x2074,
            Reg_166               = 0x2078,
            Reg_167               = 0x207C,
            Reg_168               = 0x2080,
            Reg_169               = 0X2084,
            Reg_170               = 0x208C,
            Reg_171               = 0x2090,
            Reg_172               = 0x2094,
            Reg_173               = 0x2098,
            Reg_174               = 0x209C,
            Reg_175               = 0x20A0,
            Reg_176               = 0x20A4,
            Reg_177               = 0x20A8,
            Reg_178               = 0x20AC,
            Reg_179               = 0x20B0,
            Reg_180               = 0x20B4,
            Reg_181               = 0x20B8,
            Reg_182               = 0x20BC,
            Reg_183               = 0x20C0,
            Reg_184               = 0x20C4,
            Reg_185               = 0x20CC,
            Reg_186               = 0x20D0,
            Reg_187               = 0x20D4,
            Reg_188               = 0x20D8,
            Reg_189               = 0x20DC,
            Reg_190               = 0x20E0,
            Reg_191               = 0x20E4,
            Reg_192               = 0x20E8,
            Reg_193               = 0x20EC,
            Reg_194               = 0x20F0,
            Reg_195               = 0x20F4,
            Reg_196               = 0x20F8,
            Reg_197               = 0x20FC,
            Reg_198               = 0x2100,
            Reg_199               = 0x2104,
            Reg_200               = 0x2114,
            Reg_201               = 0x2118,
            Reg_202               = 0x211C,
            Reg_203               = 0x2120,
            Reg_204               = 0x2124,

            Reg_205               = 0x3000,
            Reg_206               = 0x3004,
            Reg_207               = 0x3008,
            Reg_208               = 0x300C,
            Reg_209               = 0x3010,
            Reg_210               = 0x3014,
            Reg_211               = 0x3018,
            Reg_212               = 0x301C,
            Reg_213               = 0x3020,
            Reg_214               = 0x3024,
            Reg_215               = 0x3028,
            Reg_216               = 0x302C,
            Reg_217               = 0x3030,
            Reg_218               = 0x3034,
            Reg_219               = 0x3038,
            Reg_220               = 0x303C,
            Reg_221               = 0x3040,
            Reg_222               = 0x3044,
            Reg_223               = 0x304C,
            Reg_224               = 0x3050,
            Reg_225               = 0x3054,
            Reg_226               = 0x3058,
            Reg_227               = 0x305C,
            Reg_228               = 0x3060,
            Reg_229               = 0x3064,
            Reg_230               = 0x3068,
            Reg_231               = 0x306C,
            Reg_232               = 0x3070,
            Reg_233               = 0x3074,
            Reg_234               = 0x3078,
            Reg_235               = 0x307C,
            Reg_236               = 0x3080,
            Reg_237               = 0X3084,
            Reg_238               = 0x308C,
            Reg_239               = 0x3090,
            Reg_240               = 0x3094,
            Reg_241               = 0x3098,
            Reg_242               = 0x309C,
            Reg_243               = 0x30A0,
            Reg_244               = 0x30A4,
            Reg_245               = 0x30A8,
            Reg_246               = 0x30AC,
            Reg_247               = 0x30B0,
            Reg_248               = 0x30B4,
            Reg_249               = 0x30B8,
            Reg_250               = 0x30BC,
            Reg_251               = 0x30C0,
            Reg_252               = 0x30C4,
            Reg_253               = 0x30CC,
            Reg_254               = 0x30D0,
            Reg_255               = 0x30D4,
            Reg_256               = 0x30D8,
            Reg_257               = 0x30DC,
            Reg_258               = 0x30E0,
            Reg_259               = 0x30E4,
            Reg_260               = 0x30E8,
            Reg_261               = 0x30EC,
            Reg_262               = 0x30F0,
            Reg_263               = 0x30F4,
            Reg_264               = 0x30F8,
            Reg_265               = 0x30FC,
            Reg_266               = 0x3100,
            Reg_267               = 0x3104,
            Reg_268               = 0x3114,
            Reg_269               = 0x3118,
            Reg_270               = 0x311C,
            Reg_271               = 0x3120,
            Reg_272               = 0x3124,
        }
    }
}