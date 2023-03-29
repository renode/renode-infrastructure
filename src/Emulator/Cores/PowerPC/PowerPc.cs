//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
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
    [GPIO(NumberOfInputs = 1)]
    public partial class PowerPc : TranslationCPU, ICPUWithHooks
    {
        // Note that the reported endianness will be wrong if it is switched at runtime!
        public PowerPc(string cpuType, IMachine machine, Endianess endianness = Endianess.BigEndian) : base(cpuType, machine, endianness)
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
            this.Log(LogLevel.Warning, "PowerPC VLE mode not implemented for uImage loading.");
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

        public override string Architecture { get { return "ppc"; } }

        public override string GDBArchitecture { get { return "powerpc:common"; } }

        public override List<GDBFeatureDescriptor> GDBFeatures
        {
            get
            {
                var powerCore = new GDBFeatureDescriptor("org.gnu.gdb.power.core");
                for(var index = 0u; index < 32; index++)
                {
                    powerCore.Registers.Add(new GDBRegisterDescriptor(index, 32, $"r{index}", "uint32", "general"));
                }

                powerCore.Registers.Add(new GDBRegisterDescriptor(64, 32, "pc", "code_ptr", "general"));
                powerCore.Registers.Add(new GDBRegisterDescriptor(65, 32, "msr", "uint32", "general"));
                powerCore.Registers.Add(new GDBRegisterDescriptor(66, 32, "cr", "uint32", "general"));
                powerCore.Registers.Add(new GDBRegisterDescriptor(67, 32, "lr", "code_ptr", "general"));
                powerCore.Registers.Add(new GDBRegisterDescriptor(68, 32, "ctr", "uint32", "general"));
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

        protected override string GetExceptionDescription(ulong exceptionIndex)
        {
            return ExceptionDescriptionsMap.TryGetValue(exceptionIndex, out var result)
                ? result
                : base.GetExceptionDescription(exceptionIndex);
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
            return checked((uint)machine.ClockSource.GetClockEntry(DecrementerHandler).Value);
        }

        public bool StartInVle
        {
            get;
            set;
        }

        [Export]
        private void WriteDecrementer(ulong val)
        {
            // The API relies on 64-bit values because of PPC64, but the 32-bit PowerPC uses a 32-bit decrementer.
            var value = (uint)val;
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

        private readonly Dictionary<ulong, string> ExceptionDescriptionsMap = new Dictionary<ulong, string>
        {
            {0, "Critical input"},
            {1, "Machine check exception"},
            {2, "Data storage exception"},
            {3, "Instruction storage exception"},
            {4, "External input"},
            {5, "Alignment exception"},
            {6, "Program exception"},
            {7, "Floating-point unavailable exception"},
            {8, "System call exception"},
            {9, "Auxiliary processor unavailable"},
            {10, "Decrementer exception"},
            {11, "Fixed-interval timer interrupt"},
            {12, "Watchdog timer interrupt"},
            {13, "Data TLB miss"},
            {14, "Instruction TLB miss"},
            {15, "Debug interrupt"},
            {32, "SPE/embedded floating-point unavailable"},
            {33, "Embedded floating-point data interrupt"},
            {34, "Embedded floating-point round interrupt"},
            {35, "Embedded performance monitor interrupt"},
            {36, "Embedded doorbell interrupt"},
            {37, "Embedded doorbell critical interrupt"},
            {64, "System reset exception"},
            {65, "Data segment exception"},
            {66, "Instruction segment exception"},
            {67, "Hypervisor decrementer exception"},
            {68, "Trace exception"},
            {69, "Hypervisor data storage exception"},
            {70, "Hypervisor instruction storage exception"},
            {71, "Hypervisor data segment exception"},
            {72, "Hypervisor instruction segment exception"},
            {73, "Vector unavailable exception"},
            {74, "Programmable interval timer interrupt"},
            {75, "IO error exception"},
            {76, "Run mode exception"},
            {77, "Emulation trap exception"},
            {78, "Instruction fetch TLB miss"},
            {79, "Data load TLB miss"},
            {80, "Data store TLB miss"},
            {81, "Floating-point assist exception"},
            {82, "Data address breakpoint"},
            {83, "Instruction address breakpoint"},
            {84, "System management interrupt"},
            {85, "Embedded performance monitor interrupt"},
            {86, "Thermal interrupt"},
            {87, "Vector assist exception"},
            {88, "Soft patch exception"},
            {89, "Maintenance exception"},
            {90, "Maskable external breakpoint"},
            {91, "Non maskable external breakpoint"},
            {92, "Instruction TLB error"},
            {93, "Data TLB error"},
            {96, "EOL"},
            //tlib exceptions: used internally during code translation 
            {512, "Stop translation"},
            {513, "Branch instruction"},
            //tlib exceptions: special cases we want to stop translation
            {514, "Context synchronizing instruction"},
            {515, "System call in user mode only"},
            {516, "Conditional stores in user mode"}
        };

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
