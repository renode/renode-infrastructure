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
    public class SiLabs_SEQACC_1 : SiLabs_SequencerAccelerator
    {
        public SiLabs_SEQACC_1(Machine machine, long frequency, SiLabs_IProtocolTimer protocolTimer = null)
            : base(machine, frequency, protocolTimer)
        {
        }

        protected override DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Reg_2, new DoubleWordRegister(this)
                    .WithFlag(0, out field_18, name: "REG_2_FIELD_1")
                    .WithTaggedFlag("REG_2_FIELD_2", 1)
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Reg_4, new DoubleWordRegister(this, 0x00000180)
                    .WithTag("REG_4_FIELD_1", 0, 4)
                    .WithValueField(4, 5, out Field_2, name: "REG_4_FIELD_2")
                    .WithReservedBits(9, 7)
                    .WithEnumField<DoubleWordRegister, TimeBase>(16, 2, out Field_15, name: "REG_4_FIELD_3")
                    .WithReservedBits(18, 2)
                    .WithTag("REG_4_FIELD_4", 20, 3)
                    .WithReservedBits(23, 7)
                    .WithTaggedFlag("REG_4_FIELD_5", 30)
                    .WithTaggedFlag("REG_4_FIELD_6", 31)
                },
                {(long)Registers.Reg_5, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Set, writeCallback: (_, value) => SequenceStart((uint)value, false), name: "REG_5_FIELD_1")
                    .WithReservedBits(8, 8)
                    .WithValueField(16, 8, FieldMode.Set, writeCallback: (_, value) => SequenceAbort((uint)value), name: "REG_5_FIELD_2")
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.Reg_12, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if (value) { ResetAbsoluteDelayCounter(); } }, name: "REG_12_FIELD_1")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.Reg_6, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => GetPendingSequencesBitmask(), name: "REG_6_FIELD_1")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.Reg_7, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => GetRunningSequencesBitmask(), name: "REG_7_FIELD_1")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.Reg_13, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => GetRunningSequenceCurrentAddress(), name: "REG_13_FIELD_1")
                },
                {(long)Registers.Reg_14, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_19, FieldMode.Read, name: "REG_14_FIELD_1")
                },
                {(long)Registers.Reg_15, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out Field_5, name: "REG_15_FIELD_1")
                },
                {(long)Registers.Reg_16, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out Field_6, name: "REG_16_FIELD_1")
                },
                {(long)Registers.Reg_17, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_30, name: "REG_17_FIELD_1")
                },
                {(long)Registers.Reg_8, new DoubleWordRegister(this)
                    .WithFlag(0, out sequence[0].Field_12, name: "REG_8_FIELD_1")
                    .WithFlag(1, out sequence[1].Field_12, name: "REG_8_FIELD_2")
                    .WithFlag(2, out sequence[2].Field_12, name: "REG_8_FIELD_3")
                    .WithFlag(3, out sequence[3].Field_12, name: "REG_8_FIELD_4")
                    .WithFlag(4, out sequence[4].Field_12, name: "REG_8_FIELD_5")
                    .WithFlag(5, out sequence[5].Field_12, name: "REG_8_FIELD_6")
                    .WithFlag(6, out sequence[6].Field_12, name: "REG_8_FIELD_7")
                    .WithFlag(7, out sequence[7].Field_12, name: "REG_8_FIELD_8")
                    .WithReservedBits(8, 20)
                    .WithFlag(29, out field_26, name: "REG_8_FIELD_9")
                    .WithFlag(30, out field_28, name: "REG_8_FIELD_10")
                    .WithFlag(31, out field_16, name: "REG_8_FIELD_11")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Reg_9, new DoubleWordRegister(this)
                    .WithFlag(0, out sequence[0].Field_13, name: "REG_9_FIELD_1")
                    .WithFlag(1, out sequence[1].Field_13, name: "REG_9_FIELD_2")
                    .WithFlag(2, out sequence[2].Field_13, name: "REG_9_FIELD_3")
                    .WithFlag(3, out sequence[3].Field_13, name: "REG_9_FIELD_4")
                    .WithFlag(4, out sequence[4].Field_13, name: "REG_9_FIELD_5")
                    .WithFlag(5, out sequence[5].Field_13, name: "REG_9_FIELD_6")
                    .WithFlag(6, out sequence[6].Field_13, name: "REG_9_FIELD_7")
                    .WithFlag(7, out sequence[7].Field_13, name: "REG_9_FIELD_8")
                    .WithReservedBits(8, 20)
                    .WithFlag(29, out field_27, name: "REG_9_FIELD_9")
                    .WithFlag(30, out field_29, name: "REG_9_FIELD_10")
                    .WithFlag(31, out field_17, name: "REG_9_FIELD_11")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Reg_10, new DoubleWordRegister(this)
                    .WithFlag(0, out sequence[0].Field_10, name: "REG_10_FIELD_1")
                    .WithFlag(1, out sequence[1].Field_10, name: "REG_10_FIELD_2")
                    .WithFlag(2, out sequence[2].Field_10, name: "REG_10_FIELD_3")
                    .WithFlag(3, out sequence[3].Field_10, name: "REG_10_FIELD_4")
                    .WithFlag(4, out sequence[4].Field_10, name: "REG_10_FIELD_5")
                    .WithFlag(5, out sequence[5].Field_10, name: "REG_10_FIELD_6")
                    .WithFlag(6, out sequence[6].Field_10, name: "REG_10_FIELD_7")
                    .WithFlag(7, out sequence[7].Field_10, name: "REG_10_FIELD_8")
                    .WithReservedBits(8, 20)
                    .WithFlag(29, out field_22, name: "REG_10_FIELD_9")
                    .WithFlag(30, out field_24, name: "REG_10_FIELD_10")
                    .WithFlag(31, out field_20, name: "REG_10_FIELD_11")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Reg_11, new DoubleWordRegister(this)
                    .WithFlag(0, out sequence[0].Field_11, name: "REG_11_FIELD_1")
                    .WithFlag(1, out sequence[1].Field_11, name: "REG_11_FIELD_2")
                    .WithFlag(2, out sequence[2].Field_11, name: "REG_11_FIELD_3")
                    .WithFlag(3, out sequence[3].Field_11, name: "REG_11_FIELD_4")
                    .WithFlag(4, out sequence[4].Field_11, name: "REG_11_FIELD_5")
                    .WithFlag(5, out sequence[5].Field_11, name: "REG_11_FIELD_6")
                    .WithFlag(6, out sequence[6].Field_11, name: "REG_11_FIELD_7")
                    .WithFlag(7, out sequence[7].Field_11, name: "REG_11_FIELD_8")
                    .WithReservedBits(8, 20)
                    .WithFlag(29, out field_23, name: "REG_11_FIELD_9")
                    .WithFlag(30, out field_25, name: "REG_11_FIELD_10")
                    .WithFlag(31, out field_21, name: "REG_11_FIELD_11")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },

            };

            var startOffset = (long)Registers.Reg_20;
            var startAddressOffset = (long)Registers.Reg_20 - startOffset;
            var configOffset = (long)Registers.Reg_21 - startOffset;
            var blockSize = (long)Registers.Reg_22 - (long)Registers.Reg_20;

            for(var index = 0; index < NumberOfSequenceConfigurations; index++)
            {
                var i = index;
                registerDictionary.Add(startOffset + blockSize * i + startAddressOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out sequence[i].Field_14, name: "REG_11_FIELD_12")
                );
                registerDictionary.Add(startOffset + blockSize * i + configOffset,
                    new DoubleWordRegister(this, 0x10)
                        .WithValueField(0, 5, out sequence[i].Field_3, name: "REG_11_FIELD_13")
                        .WithEnumField<DoubleWordRegister, Enumeration_B>(5, 5, out sequence[i].Field_7, name: "REG_11_FIELD_14")
                        .WithEnumField<DoubleWordRegister, Enumeration_C>(10, 3, out sequence[i].Field_8, name: "REG_11_FIELD_15")
                        .WithFlag(13, out sequence[i].Field_4, name: "REG_11_FIELD_16")
                        .WithFlag(14, out sequence[i].Field_9, name: "REG_11_FIELD_17")
                        .WithReservedBits(15, 17)
                );
            }

            startOffset = (long)Registers.Reg_36;
            var baseAddressOffset = (long)Registers.Reg_36 - startOffset;
            blockSize = (long)Registers.Reg_37 - (long)Registers.Reg_36;

            for(var index = 0; index < NumberOfBaseAddresses; index++)
            {
                var i = index;
                registerDictionary.Add(startOffset + blockSize * i + baseAddressOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out Field_1[i], name: "REG_11_FIELD_18")
                );
            }

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var irq = ((field_26.Value && field_27.Value)
                       || (field_28.Value && field_29.Value)
                       || (field_16.Value && field_17.Value));
                Array.ForEach(sequence, x => irq |= x.Interrupt);
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    registersCollection.TryRead((long)Registers.Reg_8, out interruptFlag);
                    registersCollection.TryRead((long)Registers.Reg_9, out interruptEnable);
                }
                HostIRQ.Set(irq);

                irq = ((field_22.Value && field_23.Value)
                       || (field_24.Value && field_25.Value)
                       || (field_20.Value && field_21.Value));
                Array.ForEach(sequence, x => irq |= x.SeqInterrupt);
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    registersCollection.TryRead((long)Registers.Reg_10, out interruptFlag);
                    registersCollection.TryRead((long)Registers.Reg_11, out interruptEnable);
                }
                SequencerIRQ.Set(irq);
            });
        }

        protected override Type RegistersType => typeof(Registers);

        protected enum Registers : long
        {
            Reg_1                 = 0x0000,
            Reg_2                 = 0x0004,
            Reg_3                 = 0x0008,
            Reg_4                 = 0x0010,
            Reg_5                 = 0x0014,
            Reg_6                 = 0x0018,
            Reg_7                 = 0x001C,
            Reg_8                 = 0x0020,
            Reg_9                 = 0x0024,
            Reg_10                = 0x0028,
            Reg_11                = 0x002C,
            Reg_12                = 0x0030,
            Reg_13                = 0x0034,
            Reg_14                = 0x0038,
            Reg_15                = 0x003C,
            Reg_16                = 0x0040,
            Reg_17                = 0x0044,
            Reg_18                = 0x0048,
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
            Reg_33                = 0x0084,
            Reg_34                = 0x0088,
            Reg_35                = 0x008C,
            Reg_36                = 0x00D0,
            Reg_37                = 0x00D4,
            Reg_38                = 0x00D8,
            Reg_39                = 0x00DC,
            Reg_40                = 0x00E0,
            Reg_41                = 0x00E4,
            Reg_42                = 0x00E8,
            Reg_43                = 0x00EC,
            Reg_44                = 0x00F0,
            Reg_45                = 0x00F4,
            Reg_46                = 0x00F8,
            Reg_47                = 0x00FC,
            Reg_48                = 0x0100,
            Reg_49                = 0x0104,
            Reg_50                = 0x0108,
            Reg_51                = 0x010C,
            Reg_52                = 0x1000,
            Reg_53                = 0x1004,
            Reg_54                = 0x1008,
            Reg_55                = 0x1010,
            Reg_56                = 0x1014,
            Reg_57                = 0x1018,
            Reg_58                = 0x101C,
            Reg_59                = 0x1020,
            Reg_60                = 0x1024,
            Reg_61                = 0x1028,
            Reg_62                = 0x102C,
            Reg_63                = 0x1030,
            Reg_64                = 0x1034,
            Reg_65                = 0x1038,
            Reg_66                = 0x103C,
            Reg_67                = 0x1040,
            Reg_68                = 0x1044,
            Reg_69                = 0x1048,
            Reg_70                = 0x104C,
            Reg_71                = 0x1050,
            Reg_72                = 0x1054,
            Reg_73                = 0x1058,
            Reg_74                = 0x105C,
            Reg_75                = 0x1060,
            Reg_76                = 0x1064,
            Reg_77                = 0x1068,
            Reg_78                = 0x106C,
            Reg_79                = 0x1070,
            Reg_80                = 0x1074,
            Reg_81                = 0x1078,
            Reg_82                = 0x107C,
            Reg_83                = 0x1080,
            Reg_84                = 0x1084,
            Reg_85                = 0x1088,
            Reg_86                = 0x108C,
            Reg_87                = 0x10D0,
            Reg_88                = 0x10D4,
            Reg_89                = 0x10D8,
            Reg_90                = 0x10DC,
            Reg_91                = 0x10E0,
            Reg_92                = 0x10E4,
            Reg_93                = 0x10E8,
            Reg_94                = 0x10EC,
            Reg_95                = 0x10F0,
            Reg_96                = 0x10F4,
            Reg_97                = 0x10F8,
            Reg_98                = 0x10FC,
            Reg_99                = 0x1100,
            Reg_100               = 0x1104,
            Reg_101               = 0x1108,
            Reg_102               = 0x110C,
            Reg_103               = 0x2000,
            Reg_104               = 0x2004,
            Reg_105               = 0x2008,
            Reg_106               = 0x2010,
            Reg_107               = 0x2014,
            Reg_108               = 0x2018,
            Reg_109               = 0x201C,
            Reg_110               = 0x2020,
            Reg_111               = 0x2024,
            Reg_112               = 0x2028,
            Reg_113               = 0x202C,
            Reg_114               = 0x2030,
            Reg_115               = 0x2034,
            Reg_116               = 0x2038,
            Reg_117               = 0x203C,
            Reg_118               = 0x2040,
            Reg_119               = 0x2044,
            Reg_120               = 0x2048,
            Reg_121               = 0x204C,
            Reg_122               = 0x2050,
            Reg_123               = 0x2054,
            Reg_124               = 0x2058,
            Reg_125               = 0x205C,
            Reg_126               = 0x2060,
            Reg_127               = 0x2064,
            Reg_128               = 0x2068,
            Reg_129               = 0x206C,
            Reg_130               = 0x2070,
            Reg_131               = 0x2074,
            Reg_132               = 0x2078,
            Reg_133               = 0x207C,
            Reg_134               = 0x2080,
            Reg_135               = 0x2084,
            Reg_136               = 0x2088,
            Reg_137               = 0x208C,
            Reg_138               = 0x20D0,
            Reg_139               = 0x20D4,
            Reg_140               = 0x20D8,
            Reg_141               = 0x20DC,
            Reg_142               = 0x20E0,
            Reg_143               = 0x20E4,
            Reg_144               = 0x20E8,
            Reg_145               = 0x20EC,
            Reg_146               = 0x20F0,
            Reg_147               = 0x20F4,
            Reg_148               = 0x20F8,
            Reg_149               = 0x20FC,
            Reg_150               = 0x2100,
            Reg_151               = 0x2104,
            Reg_152               = 0x2108,
            Reg_153               = 0x210C,
            Reg_154               = 0x3000,
            Reg_155               = 0x3004,
            Reg_156               = 0x3008,
            Reg_157               = 0x3010,
            Reg_158               = 0x3014,
            Reg_159               = 0x3018,
            Reg_160               = 0x301C,
            Reg_161               = 0x3020,
            Reg_162               = 0x3024,
            Reg_163               = 0x3028,
            Reg_164               = 0x302C,
            Reg_165               = 0x3030,
            Reg_166               = 0x3034,
            Reg_167               = 0x3038,
            Reg_168               = 0x303C,
            Reg_169               = 0x3040,
            Reg_170               = 0x3044,
            Reg_171               = 0x3048,
            Reg_172               = 0x304C,
            Reg_173               = 0x3050,
            Reg_174               = 0x3054,
            Reg_175               = 0x3058,
            Reg_176               = 0x305C,
            Reg_177               = 0x3060,
            Reg_178               = 0x3064,
            Reg_179               = 0x3068,
            Reg_180               = 0x306C,
            Reg_181               = 0x3070,
            Reg_182               = 0x3074,
            Reg_183               = 0x3078,
            Reg_184               = 0x307C,
            Reg_185               = 0x3080,
            Reg_186               = 0x3084,
            Reg_187               = 0x3088,
            Reg_188               = 0x308C,
            Reg_189               = 0x30D0,
            Reg_190               = 0x30D4,
            Reg_191               = 0x30D8,
            Reg_192               = 0x30DC,
            Reg_193               = 0x30E0,
            Reg_194               = 0x30E4,
            Reg_195               = 0x30E8,
            Reg_196               = 0x30EC,
            Reg_197               = 0x30F0,
            Reg_198               = 0x30F4,
            Reg_199               = 0x30F8,
            Reg_200               = 0x30FC,
            Reg_201               = 0x3100,
            Reg_202               = 0x3104,
            Reg_203               = 0x3108,
            Reg_204               = 0x310C,
        }
    }
}