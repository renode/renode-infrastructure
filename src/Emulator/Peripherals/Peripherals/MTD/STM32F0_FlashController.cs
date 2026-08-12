//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.MTD
{
    // NOTE: We are using ByteToDoubleWord translation for option byte handling, and DoubleWordToByte
    //       for normal access handling. Because read/write handlers are only filled for non-existing
    //       methods, and region handlers are filled the same as default ones, this works as expected.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.DoubleWordToByte)]
    public class STM32F0_FlashController : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32F0_FlashController(IMachine machine, IMemory flash) : base(machine)
        {
            UnderlyingMemory = flash;

            mapperProvider = new MapperProvider(mapper);
            optionByteMapper = new RegisterMapper(typeof(OptionByteOffsets));

            DefineRegisters();
        }

        [ConnectionRegion("optionByte")]
        public byte ReadByteFromOptionByte(long offset)
        {
            using(mapperProvider.WithCurrent(optionByteMapper))
            {
                var field = (OptionByteOffsets)(offset & ~1);
                byte returningValue;
                switch(field)
                {
                case OptionByteOffsets.RDP:
                    returningValue = OptionReadProtection;
                    break;
                case OptionByteOffsets.USER:
                    returningValue = OptionUser;
                    break;
                case OptionByteOffsets.Data0:
                    returningValue = OptionData0;
                    break;
                case OptionByteOffsets.Data1:
                    returningValue = OptionData1;
                    break;
                case OptionByteOffsets.WPR0:
                    returningValue = (byte)BitHelper.GetValue(OptionWriteProtection, 0, 8);
                    break;
                case OptionByteOffsets.WPR1:
                    returningValue = (byte)BitHelper.GetValue(OptionWriteProtection, 8, 8);
                    break;
                case OptionByteOffsets.WPR2:
                    returningValue = (byte)BitHelper.GetValue(OptionWriteProtection, 16, 8);
                    break;
                case OptionByteOffsets.WPR3:
                    returningValue = (byte)BitHelper.GetValue(OptionWriteProtection, 24, 8);
                    break;
                default:
                    this.LogUnhandledRead(offset);
                    return 0;
                }

                var isComplement = (offset & 1) > 0;
                return isComplement ? (byte)~returningValue : returningValue;
            }
        }

        [ConnectionRegion("optionByte")]
        public void WriteByteToOptionByte(long offset, byte value)
        {
            using(mapperProvider.WithCurrent(optionByteMapper))
            {
                var field = (OptionByteOffsets)(offset & ~1);
                var isComplement = (offset & 1) > 0;
                if(isComplement)
                {
                    value = (byte)~value;
                }

                switch(field)
                {
                case OptionByteOffsets.RDP:
                    OptionReadProtection = value;
                    break;
                case OptionByteOffsets.USER:
                    OptionUser = value;
                    break;
                case OptionByteOffsets.Data0:
                    OptionData0 = value;
                    break;
                case OptionByteOffsets.Data1:
                    OptionData1 = value;
                    break;
                case OptionByteOffsets.WPR0:
                    OptionWriteProtection = BitHelper.ReplaceBits(OptionWriteProtection, value,
                        width: 8, destinationPosition: 0, sourcePosition: 0);
                    break;
                case OptionByteOffsets.WPR1:
                    OptionWriteProtection = BitHelper.ReplaceBits(OptionWriteProtection, value,
                        width: 8, destinationPosition: 8, sourcePosition: 0);
                    break;
                case OptionByteOffsets.WPR2:
                    OptionWriteProtection = BitHelper.ReplaceBits(OptionWriteProtection, value,
                        width: 8, destinationPosition: 16, sourcePosition: 0);
                    break;
                case OptionByteOffsets.WPR3:
                    OptionWriteProtection = BitHelper.ReplaceBits(OptionWriteProtection, value,
                        width: 8, destinationPosition: 24, sourcePosition: 0);
                    break;
                default:
                    this.LogUnhandledWrite(offset, value);
                    break;
                }
            }
        }

        public override string OffsetToString(long offset) => mapperProvider.CurrentMapper?.ToString(offset) ?? "<undefined>";

        public long Size => 0x100;

        public IMemory UnderlyingMemory { get; }

        public byte OptionReadProtection { get; set; }

        public byte OptionUser { get; set; }

        public byte OptionData0 { get; set; }

        public byte OptionData1 { get; set; }

        public uint OptionWriteProtection { get; set; }

        private void DefineRegisters()
        {
            Registers.AccessControl.Define(this)
                .WithTag("LATENCY", 0, 3)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("PRFTBE", 4)
                .WithTaggedFlag("PRFTBS", 5)
                .WithReservedBits(6, 26)
            ;

            Registers.Key.Define(this)
                .WithTag("FKEY", 0, 32)
            ;

            Registers.OptionKey.Define(this)
                .WithTag("OPTKEY", 0, 32)
            ;

            Registers.Status.Define(this)
                .WithTaggedFlag("BSY", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("PGERR", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("WRPRTERR", 4)
                .WithTaggedFlag("EOP", 5)
                .WithReservedBits(6, 26)
            ;

            Registers.Control.Define(this)
                .WithTaggedFlag("PG", 0)
                .WithTaggedFlag("PER", 1)
                .WithTaggedFlag("MER", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("OPTPG", 4)
                .WithTaggedFlag("OPTER", 5)
                .WithTaggedFlag("STRT", 6)
                .WithTaggedFlag("LOCK", 7)
                .WithReservedBits(8, 1)
                .WithTaggedFlag("OPTWRE", 9)
                .WithTaggedFlag("ERRIE", 10)
                .WithReservedBits(11, 1)
                .WithTaggedFlag("EOPIE", 12)
                .WithTaggedFlag("OBL_LAUNCH", 13)
                .WithReservedBits(14, 18)
            ;

            Registers.Address.Define(this)
                .WithTag("FAR", 0, 32)
            ;

            Registers.OptionByte.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "RDP",
                    valueProviderCallback: _ => OptionReadProtection)
                .WithValueField(8, 8, FieldMode.Read, name: "USER",
                    valueProviderCallback: _ => OptionUser)
                .WithValueField(16, 8, FieldMode.Read, name: "DATA0",
                    valueProviderCallback: _ => OptionData0)
                .WithValueField(24, 8, FieldMode.Read, name: "DATA1",
                    valueProviderCallback: _ => OptionData1)
            ;

            Registers.WriteProtection.Define(this)
                .WithValueField(0, 32, name: "WPR",
                    valueProviderCallback: _ => OptionWriteProtection,
                    writeCallback: (_, value) => OptionWriteProtection = (uint)value)
            ;
        }

        private readonly MapperProvider mapperProvider;
        private readonly RegisterMapper optionByteMapper;

        [RegisterMapper.RegistersDescription]
        public enum Registers
        {
            AccessControl = 0x00,
            Key = 0x04,
            OptionKey = 0x08,
            Status = 0x0C,
            Control = 0x10,
            Address = 0x14,
            OptionByte = 0x1C,
            WriteProtection = 0x20,
        }

        [RegisterMapper.RegistersDescription("optionByte")]
        public enum OptionByteOffsets
        {
            RDP, nRDP,
            USER, nUSER,
            Data0, nData0,
            Data1, nData1,
            WPR0, nWPR0,
            WPR1, nWPR1,
            WPR2, nWPR2,
            WPR3, nWPR3,
        }
    }
}
