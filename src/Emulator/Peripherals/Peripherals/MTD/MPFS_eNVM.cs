//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Memory;
using System.Linq;
using Antmicro.Renode.Logging;
using System;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.MTD
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class MPFS_eNVM : IDoubleWordPeripheral, IKnownSize
    {
        public MPFS_eNVM(IMachine machine, MappedMemory memory)
        {
            this.memory = memory;
            IRQ = new GPIO();
            hvTimer = new LimitTimer(machine.ClockSource, Frequency, this, nameof(hvTimer), direction: Direction.Descending,
                workMode: WorkMode.OneShot, eventEnabled: true);
            hvTimer.LimitReached += TimerTick;

            preProgramOrWriteActions = new Dictionary<bool, Action<uint>>()
            {
                { true, x => WriteFlashWithValue(x, 0xFF, RowLength) },
                { false, x => WriteFlashWithPageLatch(x) }
            };

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.PageAddress, new DoubleWordRegister(this)
                    .WithTag("Byte Select", 0, 2)
                    .WithValueField(8, 6, out pageLatchAddress, name: "PA")
                },
                {(long)Registers.WriteDataAtPageAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, writeCallback: (_, value) => pageLatch[pageLatchAddress.Value] = (uint)value, name: "PAGE_WRITE_ADDR")
                },
                {(long)Registers.WriteDataAtPageAddressThenIncrement, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, writeCallback: (_, value) =>
                    {
                        pageLatch[pageLatchAddress.Value] = (uint)value;
                        pageLatchAddress.Value = (pageLatchAddress.Value + 1) % PageLatchEntries;
                    }, name: "PAGE_WRITE_INC_ADDR")
                },
                {(long)Registers.FlashMacroAddress, new DoubleWordRegister(this)
                    .WithTag("Bya", 0 ,2)
                    .WithFlag(8, out wordAddress, name: "Wa")
                    .WithValueField(9, 5, out columnAddress, name: "Ca")
                    .WithValueField(16, 8, out pageAddress, name: "Ra")
                    .WithEnumField(24, 2, out selectedSector, name: "Ba")
                },
                {(long)Registers.ReadFlashData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => ReadFlashDoubleWord(FlashAddressToOffset()), name: "FM_READ")
                },
                {(long)Registers.ReadFlashDataThenIncrement, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        var value = ReadFlashDoubleWord(FlashAddressToOffset());
                        // The incrementation process treats the address elements as a number in the following scheme:
                        // Ba[1:0] | Ra[7:0] | Ca[4:0] | Wa
                        // Below we implement the manual addition process
                        if(!wordAddress.Value)
                        {
                            wordAddress.Value = true;
                            goto additionFinished;
                        }
                        //carry
                        wordAddress.Value = false;
                        columnAddress.Value = (columnAddress.Value + 1) % (1u << columnAddress.Width);
                        if(columnAddress.Value != 0)
                        {
                            goto additionFinished;
                        }
                        //carry
                        pageAddress.Value = (pageAddress.Value + 1) % (1u << pageAddress.Width);
                        if(pageAddress.Value != 0)
                        {
                            goto additionFinished;
                        }
                        selectedSector.Value = (Sector)(((uint)selectedSector.Value + 1) % (1u << selectedSector.Width));

                    additionFinished:
                        return value;
                    }, name: "FM_READ_INC")
                },
                {(long)Registers.FlashMacroConfiguration, new DoubleWordRegister(this)
                    .WithEnumField(0, 4, out flashMacroMode, name: "FM_Mode")
                    .WithEnumField(8, 2, out hvSequenceState, name: "FM_Seq")
                    .WithTag("DAA MUX Select", 16, 6)
                },
                {(long)Registers.TimerConfiguration, new DoubleWordRegister(this)
                    .WithValueField(0, 16, writeCallback: (_, value) => hvTimer.Value = value, valueProviderCallback: _ => (uint)hvTimer.Value, name: "Period")
                    .WithFlag(16, writeCallback: (_, value) => hvTimer.Enabled = value, valueProviderCallback: _ => false
                            /*hvTimer.Enabled - this causes the demo to run extremely slow, as the timers are bumped only
                             *after quantum. This is not that important, as these timers are for delay purposes only*/,
                            name: "Timer_En")
                    .WithFlag(17, writeCallback: (_, value) => hvTimer.Divider = value ? 100 : 1, valueProviderCallback: _ => hvTimer.Divider == 100, name: "Scale")
                    .WithTag("Pe_en", 18, 1)
                    .WithFlag(19, writeCallback: (_, value) => { if(value) DoAclk(); }, name: "Aclk_en")
                    .WithTag("cfg", 24, 6)
                },
                {(long)Registers.AnalogConfiguration, new DoubleWordRegister(this, 0x16140068)
                    .WithTag("BDAC", 0, 4)
                    .WithTag("itim", 4, 4)
                    .WithTag("MDAC", 8, 8)
                    .WithTag("PDAC", 16, 5)
                    .WithTag("NDAC", 24, 5)
                },
                {(long)Registers.TestModeConfiguration, new DoubleWordRegister(this, 0x00804000)
                    .WithTag("tm", 0, 4)
                    .WithTag("tm_disneg", 6, 1)
                    .WithTag("tm_dispos", 7, 1)
                    .WithTag("tm_itim", 8, 1)
                    .WithTag("tm_rdhvpl", 9, 1)
                    .WithTag("tm_rdstrb", 10, 1)
                    .WithTag("tm_vdac_force", 12, 1)
                    .WithTag("tm_xydec", 13, 1)
                    .WithTag("turbo_b", 14, 1)
                    .WithFlag(15, out compareTestMode, name: "tm_cmpr")
                    .WithTag("extrm_tim", 20, 1)
                    .WithTag("pg_en", 23, 1)
                    .WithTag("pe_tm", 24, 1)
                    .WithTag("pnb", 25, 1)
                    .WithTag("tm_daa", 26, 1)
                    .WithTag("tm_disrow", 27, 1)
                    .WithTag("tm_isa", 31, 1)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => !hvTimer.Enabled, name: "hv_timer")
                    .WithReadCallback((_, __) => IRQ.Unset()) //this is a guess based on a comment from the sources. The documentation
                                                              //does not describe the interrupt handling at all
                },
                {(long)Registers.Wait, new DoubleWordRegister(this, 0x16)
                    .WithTag("wait_wr_hvpl", 0, 2)
                    .WithTag("wait_rd_fm", 2, 3)
                    .WithTag("vref_vlevel", 8, 3)
                    .WithTag("vref_ilevel_p", 16, 4)
                    .WithTag("vref_ilevel_n", 20, 4)
                    .WithTag("ctat", 24, 3)
                    .WithTag("vbg_ilevel", 27, 2)
                    .WithTag("vbg_curve", 29, 3)
                },
                {(long)Registers.Monitor, new DoubleWordRegister(this)
                    .WithTag("xy_out", 0, 1)
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: (_) => switchState == SwitchState.OnCode, name: "sw2fm_r") //the docs is not clear, but this behavior is probably right
                    .WithFlag(2, out compareResult, FieldMode.Read, name: "cmprx")
                },
                {(long)Registers.SW2FMEnable, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, SwitchState>(0, 8, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        if((switchState == SwitchState.Off && value == SwitchState.S2OnCode)
                        || (switchState == SwitchState.S2OnCode && value == SwitchState.S1OnCode)
                        || (switchState == SwitchState.S1OnCode && value == SwitchState.OnCode))
                        {
                            switchState = value;
                        }
                        else
                        {
                            // improper sequence code
                            switchState = SwitchState.Off;
                        }
                    }, name: "switch_code")
                },
                {(long)Registers.ScratchPad, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "scratch_pad")
                },
                {(long)Registers.HVConfiguration, new DoubleWordRegister(this, 0x19)
                    .WithTag("FM Clock Frequency", 0, 8) //this field is ignored, as our timer has an absolute frequency, not depending on external clock
                },
                {(long)Registers.InterruptMask, new DoubleWordRegister(this)
                    .WithFlag(0, out timerInterruptEnabled, name: "Mask_reg0")
                },
                {(long)Registers.GenerateAClk, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, __) => DoAclk(), name: "ACLK_GEN_ADDR") //value of this field is ignored. The fact of being written to is important
                },
                {(long)Registers.FlashMacroDummyRead, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, readCallback: (_, __) => hvSequenceState.Value = HVSequence.Seq0, name: "Mask_regX")
                },
            };

            registers = new DoubleWordRegisterCollection(this, registerMap);
        }

        public uint ReadDoubleWord(long offset)
        {
           return registers.Read(offset);
        }

        public void Reset()
        {
            preProgramMode = false;
            registers.Reset();
            hvTimer.Reset();
            switchState = SwitchState.Off;
            Array.Clear(pageLatch, 0, pageLatch.Length);
            IRQ.Unset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x200;

        public GPIO IRQ { get;private set; }

        private void WriteSector(Sector sectorName, byte value)
        {
            var sector = sectorMappings[sectorName];
            WriteFlashWithValue(sector.Start, value, sector.Size);
        }

        private void WriteFlashWithValue(uint offset, byte value, int count)
        {
            var valueToWrite = Enumerable.Repeat(value, count).ToArray();
            memory.WriteBytes(offset, valueToWrite);
        }

        private void WriteFlashWithPageLatch(uint offset)
        {
            for(var i = 0; i < PageLatchEntries; ++i)
            {
                memory.WriteDoubleWord(offset + i * 4, pageLatch[i]);
            }
        }

        private uint ReadFlashDoubleWord(uint offset)
        {
            if(switchState == SwitchState.OnCode)
            {
                // read based on Ca/Wa/Ra/Ba
                return memory.ReadDoubleWord(offset);
            }
            else
            {
                this.Log(LogLevel.Warning, "Trying to read the flash in r_bus mode, aborting...");
                return 0;
            }
        }

        private void DoAclk()
        {
            uint offset;
            SectorDescription sector;
            if(switchState != SwitchState.OnCode)
            {
                this.Log(LogLevel.Warning, "Aclk pulse activated with c-bus disconnected.");
                return;
            }
            if(compareTestMode.Value)
            {
                //unclear from the docs, but we'll assume a row comparison
                offset = FlashAddressToOffset();
                var low = ReadFlashDoubleWord(offset);
                var high = ReadFlashDoubleWord(offset + 4);

                compareResult.Value = pageLatch[columnAddress.Value * 2] == low && pageLatch[(columnAddress.Value * 2) + 1] == high;

                return;
            }
            else if(hvSequenceState.Value == HVSequence.Seq0 && flashMacroMode.Value == Mode.PreProgram)
            {
                preProgramMode = true;
                return; //to ease the handling of preProgramMode flag, we return here.
            }
            else if(hvSequenceState.Value != HVSequence.Seq2)
            {
                return;
            }
            switch(flashMacroMode.Value)
            {
                case Mode.Read:
                    //no-op
                    break;
                case Mode.ResetAfterMargin:
                    preProgramMode = false;
                    hvSequenceState.Value = HVSequence.Seq0;
                    flashMacroMode.Value = Mode.Read;
                    break;
                case Mode.ClearHVPL:
                    Array.Clear(pageLatch, 0, pageLatch.Length);
                    break;
                case Mode.EraseRowOrPage:
                    offset = FlashAddressToOffset() & 0xFFFFFF00;
                    WriteFlashWithValue(offset, 0, RowLength);
                    break;
                case Mode.EraseSubSector:
                    offset = FlashAddressToOffset() & 0xFFFFF800;
                    WriteFlashWithValue(offset, 0, 8 * RowLength);
                    break;
                case Mode.EraseSector:
                    WriteSector(selectedSector.Value, 0);
                    break;
                case Mode.EraseBulk:
                    if(selectedSector.Value < Sector.SMSector0)
                    {
                        WriteSector(Sector.FlashSector0, 0);
                        WriteSector(Sector.FlashSector1, 0);
                    }
                    else
                    {
                        WriteSector(Sector.SMSector0, 0);
                        WriteSector(Sector.SMSector1, 0);
                    }
                    break;
                case Mode.ProgramRowOrPage:
                    offset = FlashAddressToOffset() & 0xFFFFFF00;
                    preProgramOrWriteActions[preProgramMode](offset);
                    break;
                case Mode.ProgramSectorEvenOddRows:
                    sector = sectorMappings[selectedSector.Value];
                    offset = sector.Start + (((pageAddress.Value & 1) != 0) ? RowLength : 0u); //LSB of pageAddress indicates even/odd rows
                    while(offset < sector.Start + sector.Size)
                    {
                        preProgramOrWriteActions[preProgramMode](offset);
                        offset += 2 * RowLength; //skip one row
                    }
                    break;
                case Mode.ProgramSectorAllRows:
                    sector = sectorMappings[selectedSector.Value];
                    for(var i = 0u; i < sector.Size; i += RowLength)
                    {
                        preProgramOrWriteActions[preProgramMode](sector.Start + i);
                    }
                    break;
                case Mode.PreProgram:
                    //intentionally set blank, as it should happen only in seq0
                    break;
                case Mode.PageWriteAll:
                    //not clear what it does, not implemented
                case Mode.ProgramBulkEvenOddRows:
                    //todo : should it select normal/special sectors as well?
                case Mode.ProgramBulkAllRows:
                    // Contrary to the name (and the documentation), this operation only programs either
                    // all the normal sectors or all the special sectors depending on the msb of the address.
                default:
                    this.Log(LogLevel.Warning, "Aclk in an unsupported mode: {0}.",  flashMacroMode.Value);
                    break;
            }
            preProgramMode = false;
        }

        private void TimerTick()
        {
            if(timerInterruptEnabled.Value)
            {
                IRQ.Set();
            }
        }

        private uint FlashAddressToOffset()
        {
            var offset = (wordAddress.Value ? 1 : 0
                        + (columnAddress.Value << 1)
                        + 64 * pageAddress.Value)
                        * 4;
            //we assume it won't fail, as the register infrastructure takes care of the possible values
            offset += sectorMappings[selectedSector.Value].Start;
            return (uint)offset;
        }

        private readonly IValueRegisterField pageLatchAddress;
        private readonly IValueRegisterField columnAddress;
        private readonly IValueRegisterField pageAddress;
        private readonly IFlagRegisterField wordAddress;
        private readonly IEnumRegisterField<Sector> selectedSector;
        private readonly IEnumRegisterField<Mode> flashMacroMode;
        private readonly IFlagRegisterField timerInterruptEnabled;
        private readonly IEnumRegisterField<HVSequence> hvSequenceState;
        private readonly IFlagRegisterField compareResult;
        private readonly IFlagRegisterField compareTestMode;

        private bool preProgramMode;
        private uint[] pageLatch = new uint[PageLatchEntries];
        private SwitchState switchState;
        private readonly DoubleWordRegisterCollection registers;
        private readonly LimitTimer hvTimer;
        private readonly MappedMemory memory;

        private const uint Frequency = 0x83000000;

        private readonly Dictionary<bool, Action<uint>> preProgramOrWriteActions = new Dictionary<bool, Action<uint>>();

        private static readonly Dictionary<Sector, SectorDescription> sectorMappings = new Dictionary<Sector, SectorDescription>
        {
            {Sector.SMSector0, new SectorDescription{Start = 0x00000, Size = 0x2000}},
            {Sector.FlashSector0, new SectorDescription{Start = 0x02000, Size = 0xE000}},
            {Sector.FlashSector1, new SectorDescription{Start = 0x10000, Size = 0xE000}},
            {Sector.SMSector1, new SectorDescription{Start = 0x1E000, Size = 0x2000}},
        };

        private const int PageLatchEntries = 64;
        private const int RowLength = 256;

        private enum HVSequence
        {
            Seq0,
            Seq1,
            Seq2,
            Seq3
        }

        private enum SwitchState
        {
            Off = 0,
            S2OnCode = 0b11000011,
            S1OnCode = 0b10110010,
            OnCode = 0b10011001
        }

        private enum Sector
        {
            FlashSector0,
            FlashSector1,
            SMSector0,
            SMSector1
        }

        private enum Mode
        {
            Read = 0,
            PreProgram = 1,
            PageWriteAll = 2,
            ResetAfterMargin = 3,
            ClearHVPL = 4,
            //two unused fields,
            EraseRowOrPage = 7,
            EraseSubSector = 8,
            EraseSector = 9,
            EraseBulk = 10,
            ProgramRowOrPage = 11,
            ProgramSectorEvenOddRows = 12,
            ProgramSectorAllRows = 13,
            ProgramBulkEvenOddRows = 14,
            ProgramBulkAllRows = 15
        }
        private enum Registers
        {
            PageAddress = 0x00,
            WriteDataAtPageAddress = 0x04,
            WriteDataAtPageAddressThenIncrement = 0x08,
            FlashMacroAddress = 0x0C,
            ReadFlashData = 0x10,
            ReadFlashDataThenIncrement = 0x14,
            FlashMacroConfiguration = 0x18,
            TimerConfiguration = 0x1C,
            AnalogConfiguration = 0x20,
            TestModeConfiguration = 0x24,
            Status = 0x28,
            Wait = 0x2C,
            Monitor = 0x30,
            SW2FMEnable = 0x34,
            ScratchPad = 0x38,
            HVConfiguration = 0x3C,
            InterruptMask = 0x40,
            GenerateAClk = 0x44,
            FlashMacroDummyRead = 0x48,
        }

        private struct SectorDescription
        {
            public uint Start;
            public int Size;
        }
    }
}
