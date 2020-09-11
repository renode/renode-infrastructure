//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities.Binding;
using System.Collections.Generic;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 2)]
    public partial class Arm : TranslationCPU, ICPUWithHooks, IPeripheralRegister<SemihostingUart, NullRegistrationPoint>
    {
        public Arm(string cpuType, Machine machine, Endianess endianness = Endianess.LittleEndian) : base(cpuType, machine, endianness)
        {
        }

        public void Register(SemihostingUart peripheral, NullRegistrationPoint registrationPoint)
        {
            if(semihostingUart != null)
            {
                throw new RegistrationException("A semihosting uart is already registered.");
            }
            semihostingUart = peripheral;
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(SemihostingUart peripheral)
        {
            semihostingUart = null;
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        public override string Architecture { get { return "arm"; } }

        //gdb does not contain arm-m and armv7 as independent architecteures so we need to pass "arm" in every case.
        public override string GDBArchitecture { get { return "arm"; } }

        public override List<GBDFeatureDescriptor> GDBFeatures { get { return new List<GBDFeatureDescriptor>(); } }

        public uint ID
        {
            get
            {
                return TlibGetCpuId();
            }
            set
            {
                TlibSetCpuId(value);
            }
        }

        public bool WfiAsNop { get; set; }

        [Export]
        protected uint Read32CP15(uint instruction)
        {
            return Read32CP15Inner(instruction);
        }

        [Export]
        protected void Write32CP15(uint instruction, uint value)
        {
            Write32CP15Inner(instruction, value);
        }

        [Export]
        protected ulong Read64CP15(uint instruction)
        {
            return Read64CP15Inner(instruction);
        }

        [Export]
        protected void Write64CP15(uint instruction, ulong value)
        {
            Write64CP15Inner(instruction, value);
        }

        protected override Interrupt DecodeInterrupt(int number)
        {
            switch(number)
            {
            case 0:
                return Interrupt.Hard;
            case 1:
                return Interrupt.TargetExternal1;
            default:
                throw InvalidInterruptNumberException;
            }
        }

        protected virtual uint Read32CP15Inner(uint instruction)
        {
            uint op1, op2, crm, crn;
            crm = instruction & 0xf;
            crn = (instruction >> 16) & 0xf;
            op1 = (instruction >> 21) & 7;
            op2 = (instruction >> 5) & 7;

            if((op1 == 4) && (op2 == 0) && (crm == 0))
            {
                // scu
                var scus = machine.GetPeripheralsOfType<SnoopControlUnit>().ToArray();
                switch(scus.Length)
                {
                case 0:
                    this.Log(LogLevel.Warning, "Trying to read SCU address, but SCU was not found - returning 0x0.");
                    return 0;
                case 1:
                    return (uint)((BusRangeRegistration)(machine.GetPeripheralRegistrationPoints(machine.SystemBus, scus[0]).Single())).Range.StartAddress;
                default:
                    this.Log(LogLevel.Error, "Trying to read SCU address, but more than one instance was found. Aborting.");
                    throw new CpuAbortException();
                }
            }
            this.Log(LogLevel.Warning, "Unknown CP15 32-bit read - op1={0}, op2={1}, crm={2}, crn={3} - returning 0x0", op1, op2, crm, crn);
            return 0;
        }

        protected virtual void Write32CP15Inner(uint instruction, uint value)
        {
            uint op1, op2, crm, crn;
            crm = instruction & 0xf;
            crn = (instruction >> 16) & 0xf;
            op1 = (instruction >> 21) & 7;
            op2 = (instruction >> 5) & 7;

            this.Log(LogLevel.Warning, "Unknown CP15 32-bit write - op1={0}, op2={1}, crm={2}, crn={3}", op1, op2, crm, crn);
        }

        protected virtual ulong Read64CP15Inner(uint instruction)
        {
            uint op1, crm;
            crm = instruction & 0xf;
            op1 = (instruction >> 4) & 0xf;
            this.Log(LogLevel.Warning, "Unknown CP15 64-bit read - op1={0}, crm={1} - returning 0x0", op1, crm);
            return 0;
        }

        protected virtual void Write64CP15Inner(uint instruction, ulong value)
        {
            uint op1, crm;
            crm = instruction & 0xf;
            op1 = (instruction >> 4) & 0xf;
            this.Log(LogLevel.Warning, "Unknown CP15 64-bit write - op1={0}, crm={1}", op1, crm);
        }

        protected virtual UInt32 BeforePCWrite(UInt32 value)
        {
            SetThumb((int)(value & 0x1));
            return value & ~(uint)0x1;
        }

        public uint GetItState()
        {
            uint itState = TlibGetItState();
            if((itState & 0x1F) == 0)
            {
                this.Log(LogLevel.Warning, "Checking IT_STATE, while not in IT block");
            }
            return itState;
        }

        public bool WillNextItInstructionExecute(uint itState)
        {
            /* Returns true if the oldest bit of 'abcd' field is set to 0 and the condition is met.
             * If there is no trailing one in the lower part, we are not in an IT block*/
            var MaskBit = (itState & 0x10) == 0 && ((itState & 0xF) > 0);
            var condition = ( itState >> 4 ) & 0x0E;
            if(EvaluateConditionCode(condition))
            {
                return MaskBit;
            }
            else
            {
                return !MaskBit;
            }
        }

        public bool EvaluateConditionCode(uint condition)
        {
            return TlibEvaluateConditionCode(condition) > 0;
        }

        protected override string GetExceptionDescription(ulong exceptionIndex)
        {
            if((int)exceptionIndex >= ExceptionDescriptions.Length)
            {
                return base.GetExceptionDescription(exceptionIndex);
            }

            return ExceptionDescriptions[exceptionIndex];
        }

        [Export]
        private uint DoSemihosting()
        {
            var uart = semihostingUart;
            //this.Log(LogLevel.Error, "Semihosing, r0={0:X}, r1={1:X} ({2:X})", this.GetRegisterUnsafe(0), this.GetRegisterUnsafe(1), this.TranslateAddress(this.GetRegisterUnsafe(1)));

            uint operation = R[0];
            uint r1 = R[1];
            uint result = 0;
            switch(operation)
            {
            case 7: // SYS_READC
                if(uart == null) break;
                result = uart.SemihostingGetByte();
                break;
            case 3: // SYS_WRITEC
            case 4: // SYS_WRITE0
                if(uart == null) break;
                string s = "";
                var addr = this.TranslateAddressNoFault(r1);
                do
                {
                    var c = this.Bus.ReadByte(addr++);
                    if(c == 0) break;
                    s = s + Convert.ToChar(c);
                    if((operation) == 3) break; // SYS_WRITEC
                } while(true);
                uart.SemihostingWriteString(s);
                break;
            default:
                this.Log(LogLevel.Debug, "Unknown semihosting operation: 0x{0:X}", operation);
                break;
            }
            return result;
        }

        [Export]
        private uint IsWfiAsNop()
        {
            return WfiAsNop ? 1u : 0u;
        }

        private SemihostingUart semihostingUart = null;

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649

        [Import]
        private ActionUInt32 TlibSetCpuId;

        [Import]
        private FuncUInt32 TlibGetItState;

        [Import]
        private FuncUInt32UInt32 TlibEvaluateConditionCode;

        [Import]
        private FuncUInt32 TlibGetCpuId;

        [Import]
        private ActionInt32 SetThumb;

#pragma warning restore 649

        private readonly string[] ExceptionDescriptions = 
        {
            "Undefined instruction",
            "Software interrupt",
            "Instruction Fetch Memory Abort (Prefetch Abort)",
            "Data Access Memory Abort (Data Abort)",
            "Normal Interrupt (IRQ)",
            "Fast Interrupt (FIQ)",
            "Breakpoint",
            "Kernel Trap",
            "STREX instruction"
        };
    }
}
