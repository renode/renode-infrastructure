//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public partial class IMXRT700_ClockControl : IDoubleWordPeripheral, IKnownSize
    {
        public IMXRT700_ClockControl(IMachine machine, int instanceIndex)
        {
            switch (instanceIndex)
            {
                case 0:
                    registers = DefineInstance0Registers();
                    AddFRORegisters(ref registers, instanceIndex); // FRO1
                    break;
                case 1:
                    registers = DefineInstance1Registers();
                    break;
                case 2:
                    registers = DefineInstance2Registers();
                    break;
                case 3:
                    registers = DefineInstance3Registers();
                    AddFRORegisters(ref registers, instanceIndex); // FRO2
                    break;
                case 4:
                    registers = DefineInstance4Registers();
                    break;
                default:
                    throw new ConstructionException($"No registers definition for instance with index {instanceIndex}");
            }
            Reset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public long Size => 0x1000;

        private void AddFRORegisters(ref DoubleWordRegisterCollection collection, int instanceIndex)
        {
            var instancesContainingFRO = new int[]{0, 3};
            if (!instancesContainingFRO.Any(x => instanceIndex == x))
            {
                throw new RecoverableException($"CLKCTL instance number {instanceIndex} does not have an FRO instance");
            }

            FRORegisters.FROControlStatus.Define(collection, 0x0)
                .WithFlag(0, out froEnable, name: "FROEN")
                .WithReservedBits(1, 3)
                .WithFlag(4, out trimEnable, name: "TREN")
                .WithFlag(5, out autotrimUpdateEnable, name: "TRUPEN")
                .WithFlag(6, out coarseTrim, name: "COARSEN")
                .WithFlag(7, out tuneOnce, name: "TUNEONCE")
                .WithValueField(8, 5, out froClockGate, name: "CLKGATE")
                .WithReservedBits(13, 3)
                .WithTaggedFlag("LOL_ERR", 16)
                .WithTaggedFlag("TUNE_ERR", 17)
                .WithTaggedFlag("TRUPREQ", 18)
                .WithReservedBits(19, 5)
                .WithFlag(24, valueProviderCallback: _ => (trimEnable.Value && autotrimUpdateEnable.Value), name: "TRIM_LOCK")
                .WithTaggedFlag("TUNEONCE_DONE", 25)
                .WithReservedBits(26, 6);

            FRORegisters.FROControlStatusSet.Define(collection, 0x1F00)
                .WithFlag(0, writeCallback: (_, val) => { if (val) { froEnable.Value = true; } }, name: "FROEN")
                .WithReservedBits(1, 3)
                .WithFlag(4, writeCallback: (_, val) => { if (val) { trimEnable.Value = true; } }, name: "TREN")
                .WithFlag(5, writeCallback: (_, val) => { if (val) { autotrimUpdateEnable.Value = true; } }, name: "TRUPEN")
                .WithFlag(6, writeCallback: (_, val) => { if (val) { coarseTrim.Value = true; } }, name: "COARSEN")
                .WithFlag(7, writeCallback: (_, val) => { if (val) { tuneOnce.Value = true; } }, name: "TUNEONCE")
                .WithValueField(8, 5, writeCallback: (_, val) => { froClockGate.Value = val; }, name: "CLKGATE")
                .WithReservedBits(13, 3)
                .WithTaggedFlag("LOL_ERR", 16)
                .WithTaggedFlag("TUNE_ERR", 17)
                .WithTaggedFlag("TRUPREQ", 18)
                .WithReservedBits(19, 5)
                .WithFlag(24, name: "TRIM_LOCK")
                .WithTaggedFlag("TUNEONCE_DONE", 25)
                .WithReservedBits(26, 6);

            FRORegisters.FROControlStatusClear.Define(collection, 0x1F00)
                .WithFlag(0, writeCallback: (_, val) => { if (val) { froEnable.Value = false; } }, name: "FROEN")
                .WithReservedBits(1, 3)
                .WithFlag(4, writeCallback: (_, val) => { if (val) { trimEnable.Value = false; } }, name: "TREN")
                .WithFlag(5, writeCallback: (_, val) => { if (val) { autotrimUpdateEnable.Value = false; } }, name: "TRUPEN")
                .WithFlag(6, writeCallback: (_, val) => { if (val) { coarseTrim.Value = false; } }, name: "COARSEN")
                .WithFlag(7, writeCallback: (_, val) => { if (val) { tuneOnce.Value = false; } }, name: "TUNEONCE")
                .WithTag("CLKGATE", 8, 5)
                .WithReservedBits(13, 3)
                .WithTaggedFlag("LOL_ERR", 16)
                .WithTaggedFlag("TUNE_ERR", 17)
                .WithTaggedFlag("TRUPREQ", 18)
                .WithReservedBits(19, 5)
                .WithFlag(24, name: "TRIM_LOCK")
                .WithTaggedFlag("TUNEONCE_DONE", 25)
                .WithReservedBits(26, 6);

            FRORegisters.FROControlStatusToggle.Define(collection, 0x1F00)
                .WithFlag(0, writeCallback: (_, val) => { if (val) { froEnable.Value = !froEnable.Value; } }, name: "FROEN")
                .WithReservedBits(1, 3)
                .WithFlag(4, writeCallback: (_, val) => { if (val) { trimEnable.Value = !trimEnable.Value; } }, name: "TREN")
                .WithFlag(5, writeCallback: (_, val) => { if (val) { autotrimUpdateEnable.Value = !autotrimUpdateEnable.Value; } }, name: "TRUPEN")
                .WithFlag(6, writeCallback: (_, val) => { if (val) { coarseTrim.Value = !coarseTrim.Value; } }, name: "COARSEN")
                .WithFlag(7, writeCallback: (_, val) => { if (val) { tuneOnce.Value = !tuneOnce.Value; } }, name: "TUNEONCE")
                .WithTag("CLKGATE", 8, 5)
                .WithReservedBits(13, 3)
                .WithTaggedFlag("LOL_ERR", 16)
                .WithTaggedFlag("TUNE_ERR", 17)
                .WithTaggedFlag("TRUPREQ", 18)
                .WithReservedBits(19, 5)
                .WithTaggedFlag("TRIM_LOCK", 24)
                .WithTaggedFlag("TUNEONCE_DONE", 25)
                .WithReservedBits(26, 6);

            FRORegisters.FROTrimConfiguration1.Define(collection)
                .WithValueField(0, 32, out trimConfiguration1);  // Same fields as the Set/Clear equivalent, single field for the sake of simplicity of code

            FRORegisters.FROTrimConfiguration1Set.Define(collection)
                .WithTag("REFDIV", 0, 11)
                .WithReservedBits(11, 1)
                .WithTaggedFlag("LOL_ERR_IE", 12)
                .WithTaggedFlag("TUNE_ERR_IE", 13)
                .WithTaggedFlag("TRUPREQ_IE", 14)
                .WithReservedBits(15, 1)
                .WithTag("RFCLKCNT", 16, 16)
                .WithWriteCallback((_, val) => { trimConfiguration1.Value |= val; });

            FRORegisters.FROTrimConfiguration1Clear.Define(collection)
                .WithTag("REFDIV", 0, 11)
                .WithReservedBits(11, 1)
                .WithTaggedFlag("LOL_ERR_IE", 12)
                .WithTaggedFlag("TUNE_ERR_IE", 13)
                .WithTaggedFlag("TRUPREQ_IE", 14)
                .WithReservedBits(15, 1)
                .WithTag("RFCLKCNT", 16, 16)
                .WithWriteCallback((_, val) => { trimConfiguration1.Value &= ~val; });

            FRORegisters.FROTrimConfiguration1Toggle.Define(collection)
                .WithTag("REFDIV", 0, 11)
                .WithReservedBits(11, 1)
                .WithTaggedFlag("LOL_ERR_IE", 12)
                .WithTaggedFlag("TUNE_ERR_IE", 13)
                .WithTaggedFlag("TRUPREQ_IE", 14)
                .WithReservedBits(15, 1)
                .WithTag("RFCLKCNT", 16, 16)
                .WithWriteCallback((_, val) => { trimConfiguration1.Value ^= val; });

            FRORegisters.FROTrimConfiguration2.Define(collection)
                .WithValueField(0, 32, out trimConfiguration2);  // Same fields as the Set/Clear equivalent, single field for the sake of simplicity of code

            FRORegisters.FROTrimConfiguration2Set.Define(collection)
                .WithTag("TRIM2_DELAY", 0, 12)
                .WithReservedBits(12, 4)
                .WithTag("TRIM1_DELAY", 16, 12)
                .WithReservedBits(28, 4)
                .WithWriteCallback((_, val) => { trimConfiguration2.Value |= val; });

            FRORegisters.FROTrimConfiguration2Clear.Define(collection)
                .WithTag("TRIM2_DELAY", 0, 12)
                .WithReservedBits(12, 4)
                .WithTag("TRIM1_DELAY", 16, 12)
                .WithReservedBits(28, 4)
                .WithWriteCallback((_, val) => { trimConfiguration2.Value &= ~val; });

            FRORegisters.FROTrimConfiguration2Toggle.Define(collection)
                .WithTag("TRIM2_DELAY", 0, 12)
                .WithReservedBits(12, 4)
                .WithTag("TRIM1_DELAY", 16, 12)
                .WithReservedBits(28, 4)
                .WithWriteCallback((_, val) => { trimConfiguration2.Value ^= val; });

            FRORegisters.FROTrim.Define(collection)
                .WithTag("FINE_TRIM", 0, 7)
                .WithTag("COARSE_TRIM", 7, 5)
                .WithReservedBits(12, 4)
                .WithTag("TRIMTEMP", 16, 6)
                .WithReservedBits(22, 10);

            FRORegisters.FROTrimSet.Define(collection)
                .WithTag("FINE_TRIM", 0, 7)
                .WithTag("COARSE_TRIM", 7, 5)
                .WithReservedBits(12, 4)
                .WithTag("TRIMTEMP", 16, 6)
                .WithReservedBits(22, 10);

            FRORegisters.FROTrimClear.Define(collection)
                .WithTag("FINE_TRIM", 0, 7)
                .WithTag("COARSE_TRIM", 7, 5)
                .WithReservedBits(12, 4)
                .WithTag("TRIMTEMP", 16, 6)
                .WithReservedBits(22, 10);

            FRORegisters.FROTrimToggle.Define(collection)
                .WithTag("FINE_TRIM", 0, 7)
                .WithTag("COARSE_TRIM", 7, 5)
                .WithReservedBits(12, 4)
                .WithTag("TRIMTEMP", 16, 6)
                .WithReservedBits(22, 10);

            FRORegisters.FROExpectedTrimCount.Define(collection)
                .WithTag("TEXPCNT", 0, 16)
                .WithTag("TEXPCNT_RANGE", 16, 8)
                .WithReservedBits(24, 8);

            FRORegisters.FROExpectedTrimCountSet.Define(collection)
                .WithTag("TEXPCNT", 0, 16)
                .WithTag("TEXPCNT_RANGE", 16, 8)
                .WithReservedBits(24, 8);

            FRORegisters.FROExpectedTrimCountClear.Define(collection)
                .WithTag("TEXPCNT", 0, 16)
                .WithTag("TEXPCNT_RANGE", 16, 8)
                .WithReservedBits(24, 8);

            FRORegisters.FROExpectedTrimCountToggle.Define(collection)
                .WithTag("TEXPCNT", 0, 16)
                .WithTag("TEXPCNT_RANGE", 16, 8)
                .WithReservedBits(24, 8);

            FRORegisters.FROAutoTuneTrim.Define(collection)
                .WithTag("AUTOTRIM", 0, 12)
                .WithReservedBits(12, 20);

            FRORegisters.FROAutoTuneTrimSet.Define(collection)
                .WithTag("AUTOTRIM", 0, 12)
                .WithReservedBits(12, 20);

            FRORegisters.FROAutoTuneTrimClear.Define(collection)
                .WithTag("AUTOTRIM", 0, 12)
                .WithReservedBits(12, 20);

            FRORegisters.FROAutoTuneTrimToggle.Define(collection)
                .WithTag("AUTOTRIM", 0, 12)
                .WithReservedBits(12, 20);

            FRORegisters.FROTrimCount.Define(collection)
                .WithTag("TRIMCNT", 0, 32);

            FRORegisters.FROTrimCountSet.Define(collection)
                .WithTag("TRIMCNT", 0, 32);

            FRORegisters.FROTrimCountClear.Define(collection)
                .WithTag("TRIMCNT", 0, 32);

            FRORegisters.FROTrimCountToggle.Define(collection)
                .WithTag("TRIMCNT", 0, 32);
        }

        private DoubleWordRegisterCollection registers;

        /* FRO implementation
        *  As the memory adresses of the FRO instances collide with CLKCTL, this was implemented inside CLKCTL to avoid
        *  address collisions. Not all instances use below definitions.
        */
        private IFlagRegisterField froEnable;
        private IFlagRegisterField trimEnable;
        private IFlagRegisterField autotrimUpdateEnable;
        private IFlagRegisterField coarseTrim;
        private IFlagRegisterField tuneOnce;
        private IValueRegisterField froClockGate;

        private IValueRegisterField control0;
        private IValueRegisterField control1;
        private IValueRegisterField control2;
        private IValueRegisterField control3;
        private IValueRegisterField control4;
        private IValueRegisterField control5;

        private IValueRegisterField trimConfiguration1;
        private IValueRegisterField trimConfiguration2;

        private enum FRORegisters
        {
            /* FRO offsets start from 0x200, but it is registered +100 from the clkctl base
             * We adjust for this difference here by having all the offset enlarged by 0x100
             */
            FROControlStatus = 0x300,
            FROControlStatusSet = 0x304,
            FROControlStatusClear = 0x308,
            FROControlStatusToggle = 0x30C,
            FROTrimConfiguration1 = 0x310,
            FROTrimConfiguration1Set = 0x314,
            FROTrimConfiguration1Clear = 0x318,
            FROTrimConfiguration1Toggle = 0x31C,
            FROTrimConfiguration2 = 0x320,
            FROTrimConfiguration2Set = 0x324,
            FROTrimConfiguration2Clear = 0x328,
            FROTrimConfiguration2Toggle = 0x32C,
            FROTrim = 0x340,
            FROTrimSet = 0x344,
            FROTrimClear = 0x348,
            FROTrimToggle = 0x34C,
            FROExpectedTrimCount = 0x350,
            FROExpectedTrimCountSet = 0x354,
            FROExpectedTrimCountClear = 0x358,
            FROExpectedTrimCountToggle = 0x35C,
            FROAutoTuneTrim = 0x360,
            FROAutoTuneTrimSet = 0x364,
            FROAutoTuneTrimClear = 0x368,
            FROAutoTuneTrimToggle = 0x36C,
            FROTrimCount = 0x370,
            FROTrimCountSet = 0x374,
            FROTrimCountClear = 0x378,
            FROTrimCountToggle = 0x37C,
        }
    }
}
