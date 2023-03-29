//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities.Binding;
using System.Collections.Generic;
using System;
using Antmicro.Renode.Time;
using ELFSharp.ELF;
using Machine = Antmicro.Renode.Core.Machine;
using ELFSharp.ELF.Sections;
using System.Linq;
using Antmicro.Renode.Logging;
using ELFSharp.UImage;

namespace Antmicro.Renode.Peripherals.CPU
{
    // This model is a retro-fitted PowerPc. Both should be unified, as they are almost identical.
    [GPIO(NumberOfInputs = 1)]
    public partial class PowerPc64 : TranslationCPU, ICPUWithHooks
    {
        // Note that the reported endianness will be wrong if it is switched at runtime!
        public PowerPc64(IMachine machine, string cpuType = "620", Endianess endianness = Endianess.BigEndian) : base(cpuType, machine,
            endianness, CpuBitness.Bits64)
        {
            initialEndianess = endianness;
            irqSync = new object();
            machine.ClockSource.AddClockEntry(
                new ClockEntry(long.MaxValue / 2, 128000000, DecrementerHandler, this, String.Empty, false, Direction.Descending));
            TlibSetLittleEndianMode(initialEndianess == Endianess.LittleEndian ? 1u : 0u);
        }

        public override void Reset()
        {
            base.Reset();
            TlibSetLittleEndianMode(initialEndianess == Endianess.LittleEndian ? 1u : 0u);
        }

        public override void InitFromUImage(UImage uImage)
        {
            this.Log(LogLevel.Warning, "PowerPC64 VLE mode not implemented for uImage loading.");
            base.InitFromUImage(uImage);
        }

        public override void InitFromElf(IELF elf)
        {
            base.InitFromElf(elf);

            var bamSection = elf.GetSections<Section<uint>>().FirstOrDefault(x => x.Name == ".__bam_bootarea");
            if(bamSection != null)
            {
                var bamSectionContents = bamSection.GetContents();
                var isValidResetConfigHalfWord = bamSectionContents[1] == 0x5a;
                if(!isValidResetConfigHalfWord)
                {
                    this.Log(LogLevel.Warning, "Invalid BAM section, ignoring.");
                }
                else
                {
                    StartInVle = (bamSectionContents[0] & 0x1) == 1;
                    this.Log(LogLevel.Info, "Will {0}start in VLE mode.", StartInVle ? "" : "not ");
                }
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            InternalSetInterrupt(InterruptType.External, value);
        }

        public override string Architecture { get { return "ppc64"; } }

        public override string GDBArchitecture { get { return "powerpc:common64"; } }

        public override List<GDBFeatureDescriptor> GDBFeatures
        {
            get
            {
                var powerCore = new GDBFeatureDescriptor("org.gnu.gdb.power.core");
                for(var index = 0u; index < 32; index++)
                {
                    powerCore.Registers.Add(new GDBRegisterDescriptor(index, 64, $"r{index}", "uint64", "general"));
                }

                powerCore.Registers.Add(new GDBRegisterDescriptor(64, 64, "pc", "code_ptr", "general"));
                powerCore.Registers.Add(new GDBRegisterDescriptor(65, 64, "msr", "uint64", "general"));
                powerCore.Registers.Add(new GDBRegisterDescriptor(66, 32, "cr", "uint32", "general"));
                powerCore.Registers.Add(new GDBRegisterDescriptor(67, 64, "lr", "code_ptr", "general"));
                powerCore.Registers.Add(new GDBRegisterDescriptor(68, 64, "ctr", "uint64", "general"));
                powerCore.Registers.Add(new GDBRegisterDescriptor(69, 32, "xer", "uint32", "general"));

                return new List<GDBFeatureDescriptor>(new GDBFeatureDescriptor[] { powerCore });
            }
        }

        public bool WaitAsNop 
        { 
            get => neverWaitForInterrupt; 
            set
            {
                neverWaitForInterrupt = value;
            }
        }

        protected override Interrupt DecodeInterrupt(int number)
        {
            if(number == 0)
            {
                return Interrupt.Hard;
            }
            throw InvalidInterruptNumberException;
        }

        [Export]
        public uint ReadTbl()
        {
            tb += 0x100;
            return tb;
        }

        [Export]
        public uint ReadTbu()
        {
            return 0;
        }

        [Export]
        private ulong ReadDecrementer()
        {
            return checked((ulong)machine.ClockSource.GetClockEntry(DecrementerHandler).Value);
        }

        public bool StartInVle
        {
            get;
            set;
        }

        [Export]
        private void WriteDecrementer(ulong value)
        {
            machine.ClockSource.ExchangeClockEntryWith(DecrementerHandler,
                entry => entry.With(period: value, value: value, enabled: value != 0));
        }

        private void InternalSetInterrupt(InterruptType interrupt, bool value)
        {
            lock(irqSync)
            {
                if(value)
                {
                    TlibSetPendingInterrupt((int)interrupt, 1);
                    base.OnGPIO(0, true);
                    return;
                }
                if(TlibSetPendingInterrupt((int)interrupt, 0) == 1)
                {
                    base.OnGPIO(0, false);
                }
            }
        }

        private void DecrementerHandler()
        {
            InternalSetInterrupt(InterruptType.Decrementer, true);
        }

        [Export]
        private uint IsVleEnabled()
        {
            //this should present the current state. Now it's a stub only.
            return StartInVle ? 1u : 0u;
        }

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import]
        private Func<int, int, int> TlibSetPendingInterrupt;

        [Import]
        private Action<uint> TlibSetLittleEndianMode;

        #pragma warning restore 649

        private uint tb;
        private readonly object irqSync;
        private readonly Endianess initialEndianess;

        // have to be in sync with translation libs
        private enum InterruptType
        {
            Reset = 0,
            WakeUp,
            MachineCheck,
            External,
            SMI,
            CritictalExternal,
            Debug,
            Thermal,
            Decrementer,
            Hypervisor,
            PIT,
            FIT,
            WDT,
            CriticalDoorbell,
            Doorbell,
            PerformanceMonitor
        }
    }
}
