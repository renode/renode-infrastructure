//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Renode.Logging;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Exceptions;
using ELFSharp.ELF;
using ELFSharp.UImage;
using Machine = Antmicro.Renode.Core.Machine;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class CortexM : Arm
    {
        public CortexM(string cpuType, Machine machine, NVIC nvic, uint id = 0, Endianess endianness = Endianess.LittleEndian, uint? fpuInterruptNumber = null) : base(cpuType, machine, id, endianness)
        {
            if(nvic == null)
            {
                throw new RecoverableException(new ArgumentNullException("nvic"));
            }

            tlibSetFpuInterruptNumber((int?)fpuInterruptNumber ?? -1);

            this.nvic = nvic;
            nvic.AttachCPU(this);
        }

        public override void Reset()
        {
            pcNotInitialized = true;
            vtorInitialized = false;
            base.Reset();
        }

        protected override void OnResume()
        {
            InitPCAndSP();
            base.OnResume();
        }

        public override string Architecture { get { return "arm-m"; } }

        public override List<GDBFeatureDescriptor> GDBFeatures
        {
            get
            {
                var features = new List<GDBFeatureDescriptor>();

                var mProfileFeature = new GDBFeatureDescriptor("org.gnu.gdb.arm.m-profile");
                for(var index = 0u; index <= 12; index++)
                {
                    mProfileFeature.Registers.Add(new GDBRegisterDescriptor(index, 32, $"r{index}", "uint32", "general"));
                }
                mProfileFeature.Registers.Add(new GDBRegisterDescriptor(13, 32, "sp", "data_ptr", "general"));
                mProfileFeature.Registers.Add(new GDBRegisterDescriptor(14, 32, "lr", "uint32", "general"));
                mProfileFeature.Registers.Add(new GDBRegisterDescriptor(15, 32, "pc", "code_ptr", "general"));
                mProfileFeature.Registers.Add(new GDBRegisterDescriptor(25, 32, "xpsr", "uint32", "general"));
                features.Add(mProfileFeature);

                var mSystemFeature = new GDBFeatureDescriptor("org.gnu.gdb.arm.m-system");
                mSystemFeature.Registers.Add(new GDBRegisterDescriptor(26, 32, "msp", "uint32", "general"));
                mSystemFeature.Registers.Add(new GDBRegisterDescriptor(27, 32, "psp", "uint32", "general"));
                mSystemFeature.Registers.Add(new GDBRegisterDescriptor(28, 32, "primask", "uint32", "general"));
                mSystemFeature.Registers.Add(new GDBRegisterDescriptor(29, 32, "basepri", "uint32", "general"));
                mSystemFeature.Registers.Add(new GDBRegisterDescriptor(30, 32, "faultmask", "uint32", "general"));
                mSystemFeature.Registers.Add(new GDBRegisterDescriptor(31, 32, "control", "uint32", "general"));
                features.Add(mSystemFeature);

                return features;
            }
        }

        public uint VectorTableOffset
        {
            get
            {
                return tlibGetInterruptVectorBase();
            }
            set
            {
                vtorInitialized = true;
                if(machine.SystemBus.FindMemory(value, this) == null)
                {
                    this.Log(LogLevel.Warning, "Tried to set VTOR address at 0x{0:X} which does not lay in memory. Aborted.", value);
                    return;
                }
                this.NoisyLog("VectorTableOffset set to 0x{0:X}.", value);
                tlibSetInterruptVectorBase(value);
            }
        }

        public bool FpuEnabled
        {
            set
            {
                tlibToggleFpu(value ? 1 : 0);
            }
        }

        public UInt32 FaultStatus
        {
            set
            {
                tlibSetFaultStatus(value);
            }
            get
            {
                return tlibGetFaultStatus();
            }
        }

        public UInt32 MemoryFaultAddress
        {
            get
            {
                return tlibGetMemoryFaultAddress();
            }
        }

        public bool MPUEnabled
        {
            get
            {
                return tlibIsMpuEnabled() != 0;
            }
            set
            {
                tlibEnableMpu(value ? 1 : 0);
            }
        }

        public UInt32 MPURegionBaseAddress
        {
            set
            {
                tlibSetMpuRegionBaseAddress(value);
            }
            get
            {
                return tlibGetMpuRegionBaseAddress();
            }
        }

        public UInt32 MPURegionAttributeAndSize
        {
            set
            {
                tlibSetMpuRegionSizeAndEnable(value);
            }
            get
            {
                return tlibGetMpuRegionSizeAndEnable();
            }
        }

        public UInt32 MPURegionNumber
        {
            set
            {
                tlibSetMpuRegionNumber(value);
            }
            get
            {
                return tlibGetMpuRegionNumber();
            }
        }

        public uint XProgramStatusRegister
        {
            get
            {
                return tlibGetXpsr();
            }
        }

        public override void InitFromElf(IELF elf)
        {
            // do nothing
        }

        public override void InitFromUImage(UImage uImage)
        {
            // do nothing
        }

        protected override UInt32 BeforePCWrite(UInt32 value)
        {
            if(value % 2 == 0)
            {
                this.Log(LogLevel.Warning, "Patching PC 0x{0:X} for Thumb mode.", value);
                value += 1;
            }
            pcNotInitialized = false;
            return base.BeforePCWrite(value);
        }

        private void InitPCAndSP()
        {
            var firstNotNullSection = machine.SystemBus.Lookup.FirstNotNullSectionAddress;
            if(!vtorInitialized && firstNotNullSection.HasValue)
            {
                if((firstNotNullSection.Value & (2 << 6 - 1)) > 0)
                {
                    this.Log(LogLevel.Warning, "Alignment of VectorTableOffset register is not correct.");
                }
                else
                {
                    var value = firstNotNullSection.Value;
                    this.Log(LogLevel.Info, "Guessing VectorTableOffset value to be 0x{0:X}.", value);
                    VectorTableOffset = checked((uint)value);
                }
            }
            if(pcNotInitialized)
            {
                pcNotInitialized = false;
                // stack pointer and program counter are being sent according
                // to VTOR (vector table offset register)
                var sysbus = machine.SystemBus;
                var pc = sysbus.ReadDoubleWord(VectorTableOffset + 4, this);
                var sp = sysbus.ReadDoubleWord(VectorTableOffset, this);
                if(sysbus.FindMemory(pc, this) == null || (pc == 0 && sp == 0))
                {
                    this.Log(LogLevel.Error, "PC does not lay in memory or PC and SP are equal to zero. CPU was halted.");
                    IsHalted = true;
                }
                this.Log(LogLevel.Info, "Setting initial values: PC = 0x{0:X}, SP = 0x{1:X}.", pc, sp);
                PC = pc;
                SP = sp;
            }
        }

        [Export]
        private void SetPendingIRQ(int number)
        {
            nvic.SetPendingIRQ(number);
        }

        [Export]
        private int AcknowledgeIRQ()
        {
            var result = nvic.AcknowledgeIRQ();
            return result;
        }

        [Export]
        private void CompleteIRQ(int number)
        {
            nvic.CompleteIRQ(number);
        }

        [Export]
        private void OnBASEPRIWrite(int value)
        {
            nvic.BASEPRI = (byte)value;
        }

        [Export]
        private int FindPendingIRQ()
        {
            return nvic != null ? nvic.FindPendingInterrupt() : -1;
        }

        [Export]
        private int PendingMaskedIRQ()
        {
            return nvic.MaskedInterruptPresent ? 1 : 0;
        }


        private NVIC nvic;
        private bool pcNotInitialized = true;
        private bool vtorInitialized;

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import]
        private ActionInt32 tlibToggleFpu;

        [Import]
        private FuncUInt32 tlibGetFaultStatus;

        [Import]
        private ActionUInt32 tlibSetFaultStatus;

        [Import]
        private FuncUInt32 tlibGetMemoryFaultAddress;

        [Import]
        private ActionInt32 tlibEnableMpu;

        [Import]
        private FuncInt32 tlibIsMpuEnabled;

        [Import]
        private ActionUInt32 tlibSetMpuRegionBaseAddress;

        [Import]
        private FuncUInt32 tlibGetMpuRegionBaseAddress;

        [Import]
        private ActionUInt32 tlibSetMpuRegionSizeAndEnable;

        [Import]
        private FuncUInt32 tlibGetMpuRegionSizeAndEnable;

        [Import]
        private ActionUInt32 tlibSetMpuRegionNumber;

        [Import]
        private FuncUInt32 tlibGetMpuRegionNumber;

        [Import]
        private ActionInt32 tlibSetFpuInterruptNumber;

        [Import]
        private FuncUInt32 tlibGetInterruptVectorBase;

        [Import]
        private ActionUInt32 tlibSetInterruptVectorBase;

        [Import]
        private FuncUInt32 tlibGetXpsr;

        #pragma warning restore 649
    }
}

