//
// Copyright (c) 2010-2023 Antmicro
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
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    [GPIO(NumberOfInputs = 2)]
    public abstract partial class Arm : TranslationCPU, ICPUWithHooks, IPeripheralRegister<SemihostingUart, NullRegistrationPoint>, IPeripheralRegister<ArmPerformanceMonitoringUnit, NullRegistrationPoint>
    {
        public Arm(string cpuType, IMachine machine, uint cpuId = 0, Endianess endianness = Endianess.LittleEndian, uint? numberOfMPURegions = null, ArmSignalsUnit signalsUnit = null)
            : base(cpuId, cpuType, machine, endianness)
        {
            if(numberOfMPURegions.HasValue)
            {
                this.NumberOfMPURegions = numberOfMPURegions.Value;
            }

            if(signalsUnit != null)
            {
                // There's no such unit in hardware but we need to share certain signals between cores.
                this.signalsUnit = signalsUnit;
                signalsUnit.RegisterCPU(this);
            }
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

        public void Register(ArmPerformanceMonitoringUnit peripheral, NullRegistrationPoint registrationPoint)
        {
            if(performanceMonitoringUnit != null)
            {
                throw new RegistrationException("A PMU is already registered.");
            }
            performanceMonitoringUnit = peripheral;
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);

            performanceMonitoringUnit.RegisterCPU(this);
        }

        public void Unregister(ArmPerformanceMonitoringUnit peripheral)
        {
            performanceMonitoringUnit = null;
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        public override string Architecture { get { return "arm"; } }

        //gdb does not contain arm-m and armv7 as independent architecteures so we need to pass "arm" in every case.
        public override string GDBArchitecture { get { return "arm"; } }

        public override List<GDBFeatureDescriptor> GDBFeatures { get { return new List<GDBFeatureDescriptor>(); } }

        public uint ModelID
        {
            get
            {
                return TlibGetCpuModelId();
            }
            set
            {
                TlibSetCpuModelId(value);
            }
        }

        public bool WfiAsNop
        {
            get => wfiAsNop;
            set
            {
                wfiAsNop = value;
                neverWaitForInterrupt = wfiAsNop && wfeAndSevAsNop;
            }
        }

        public bool WfeAndSevAsNop
        {
            get => wfeAndSevAsNop;
            set
            {
                wfeAndSevAsNop = value;
                neverWaitForInterrupt = wfiAsNop && wfeAndSevAsNop;
            }
        }

        public uint NumberOfMPURegions
        {
            get
            {
                return TlibGetNumberOfMpuRegions();
            }
            set
            {
                TlibSetNumberOfMpuRegions(value);
            }
        }

        protected bool wfiAsNop;
        protected bool wfeAndSevAsNop;

        [Export]
        protected uint Read32CP15(uint instruction)
        {
            return Read32CP15Inner(new Coprocessor32BitMoveInstruction(instruction));
        }

        [Export]
        protected void Write32CP15(uint instruction, uint value)
        {
            Write32CP15Inner(new Coprocessor32BitMoveInstruction(instruction), value);
        }

        [Export]
        protected ulong Read64CP15(uint instruction)
        {
            return Read64CP15Inner(new Coprocessor64BitMoveInstruction(instruction));
        }

        [Export]
        protected void Write64CP15(uint instruction, ulong value)
        {
            Write64CP15Inner(new Coprocessor64BitMoveInstruction(instruction), value);
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

        protected virtual uint Read32CP15Inner(Coprocessor32BitMoveInstruction instruction)
        {
            if(instruction.Opc1 == 4 && instruction.Opc2 == 0 && instruction.CRm == 0 && instruction.CRn == 15) // CBAR
            {
                // scu
                var scus = machine.GetPeripheralsOfType<ArmSnoopControlUnit>().ToArray();
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
            this.Log(LogLevel.Warning, "Unknown CP15 32-bit read - {0} - returning 0x0", instruction);
            return 0;
        }

        protected virtual void Write32CP15Inner(Coprocessor32BitMoveInstruction instruction, uint value)
        {
            this.Log(LogLevel.Warning, "Unknown CP15 32-bit write - {0}", instruction);
        }

        protected virtual ulong Read64CP15Inner(Coprocessor64BitMoveInstruction instruction)
        {
            this.Log(LogLevel.Warning, "Unknown CP15 64-bit read - {0} - returning 0x0", instruction);
            return 0;
        }

        protected virtual void Write64CP15Inner(Coprocessor64BitMoveInstruction instruction, ulong value)
        {
            this.Log(LogLevel.Warning, "Unknown CP15 64-bit write - {0}", instruction);
        }

        protected virtual UInt32 BeforePCWrite(UInt32 value)
        {
            TlibSetThumb((int)(value & 0x1));
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
            var condition = (itState >> 4) & 0x0E;
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
            if(exceptionIndex >= (ulong)ExceptionDescriptions.Length)
            {
                return base.GetExceptionDescription(exceptionIndex);
            }

            return ExceptionDescriptions[exceptionIndex];
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var config in defaultTCMConfiguration)
            {
                RegisterTCMRegion(config);
            }
        }

        public void SetEventFlag(bool value)
        {
            TlibSetEventFlag(value ? 1 : 0);
        }

        public void SetSevOnPending(bool value)
        {
            TlibSetSevOnPending(value ? 1 : 0);
        }

        public void RegisterTCMRegion(MappedMemory memory, uint interfaceIndex, uint regionIndex)
        {
            Action<IMachine, MachineStateChangedEventArgs> hook = null;
            hook = (_, args) =>
            {
                if(args.CurrentState != MachineStateChangedEventArgs.State.Started)
                {
                    return;
                }
                if(!TryRegisterTCMRegion(memory, interfaceIndex, regionIndex))
                {
                    this.Log(LogLevel.Error, "Attempted to register a TCM #{0} region #{1}, but {2} is not registered for this cpu.", interfaceIndex, regionIndex, machine.GetLocalName(memory));
                }
                machine.StateChanged -= hook;
            };
            machine.StateChanged += hook;
        }

        private void RegisterTCMRegion(TCMConfiguration config)
        {
            try
            {
                TlibRegisterTcmRegion(config.Address, config.Size, ((ulong)config.InterfaceIndex << 32) | config.RegionIndex);
            }
            catch(Exception e)
            {
                throw new RecoverableException(e);
            }
        }

        public ulong GetSystemRegisterValue(string name)
        {
            ValidateSystemRegisterAccess(name, isWrite: false);

            return TlibGetSystemRegister(name, 1u /* log_unhandled_access: true */);
        }

        public void SetSystemRegisterValue(string name, ulong value)
        {
            ValidateSystemRegisterAccess(name, isWrite: true);

            TlibSetSystemRegister(name, value, 1u /* log_unhandled_access: true */);
        }

        private void ValidateSystemRegisterAccess(string name, bool isWrite)
        {
            switch((SystemRegisterCheckReturnValue)TlibCheckSystemRegisterAccess(name, isWrite ? 1u : 0u))
            {
            case SystemRegisterCheckReturnValue.AccessValid:
                return;
            case SystemRegisterCheckReturnValue.AccessorNotFound:
                var accessName = isWrite ? "Writing" : "Reading";
                throw new RecoverableException($"{accessName} the {name} register isn't supported.");
            case SystemRegisterCheckReturnValue.RegisterNotFound:
                throw new RecoverableException("No such register.");
            default:
                throw new ArgumentException("Invalid TlibCheckSystemRegisterAccess return value!");
            }
        }

        private bool TryRegisterTCMRegion(MappedMemory memory, uint interfaceIndex, uint regionIndex)
        {
            ulong address;
            if(!TCMConfiguration.TryFindRegistrationAddress((SystemBus)machine.SystemBus, this, memory, out address))
            {
                return false;
            }

            var config = new TCMConfiguration(checked((uint)address), checked((ulong)memory.Size), regionIndex, interfaceIndex);
            RegisterTCMRegion(config);
            defaultTCMConfiguration.Add(config);

            return true;
        }

        [Export]
        private void ReportPMUOverflow(int counter)
        {
            performanceMonitoringUnit?.OnOverflowAction(counter);
        }

        private ArmPerformanceMonitoringUnit performanceMonitoringUnit;

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
                    var addr = this.TranslateAddress(r1, MpuAccess.InstructionFetch);
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
        private void FillConfigurationSignalsState(IntPtr allocatedStatePointer)
        {
            // It's OK not to set the fields if there's no ArmConfigurationSignals.
            // Default values of structure's fields are neutral to the simulation.
            signalsUnit?.FillConfigurationStateStruct(allocatedStatePointer, this);
        }

        [Export]
        private uint IsWfiAsNop()
        {
            return WfiAsNop ? 1u : 0u;
        }

        [Export]
        private uint IsWfeAndSevAsNop()
        {
            return WfeAndSevAsNop ? 1u : 0u;
        }

        private SemihostingUart semihostingUart = null;

        [Export]
        private void SetSystemEvent(int value)
        {
            var flag = value != 0;

            foreach(var cpu in machine.SystemBus.GetCPUs().OfType<Arm>())
            {
                cpu.SetEventFlag(flag);
            }
        }

        private enum SystemRegisterCheckReturnValue
        {
            RegisterNotFound = 1,
            AccessorNotFound = 2,
            AccessValid = 3,
        }

        private readonly List<TCMConfiguration> defaultTCMConfiguration = new List<TCMConfiguration>();
        private readonly ArmSignalsUnit signalsUnit;

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649

        [Import]
        private ActionUInt32 TlibSetCpuModelId;

        [Import]
        private FuncUInt32 TlibGetItState;

        [Import]
        private FuncUInt32UInt32 TlibEvaluateConditionCode;

        [Import]
        private FuncUInt32 TlibGetCpuModelId;

        [Import]
        private ActionInt32 TlibSetThumb;

        [Import]
        private ActionInt32 TlibSetEventFlag;

        [Import]
        private ActionInt32 TlibSetSevOnPending;

        [Import]
        private ActionUInt32 TlibSetNumberOfMpuRegions;

        [Import]
        private FuncUInt32 TlibGetNumberOfMpuRegions;

        [Import]
        private ActionUInt32UInt64UInt64 TlibRegisterTcmRegion;

        [Import]
        private FuncUInt32StringUInt32 TlibCheckSystemRegisterAccess;

        [Import]
        // The arguments are: char *name, bool log_unhandled_access.
        private FuncUInt64StringUInt32 TlibGetSystemRegister;

        [Import]
        // The arguments are: char *name, uint64_t value, bool log_unhandled_access.
        private ActionStringUInt64UInt32 TlibSetSystemRegister;

        [Import]
        public ActionInt32UInt32 TlibUpdatePmuCounters;

        [Import]
        public ActionUInt32 TlibPmuSetDebug;

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

        protected struct Coprocessor32BitMoveInstruction
        {
            public static bool operator ==(Coprocessor32BitMoveInstruction a, Coprocessor32BitMoveInstruction b)
            {
                return a.FieldsOnly == b.FieldsOnly;
            }

            public static bool operator !=(Coprocessor32BitMoveInstruction a, Coprocessor32BitMoveInstruction b)
            {
                return !(a == b);
            }

            public Coprocessor32BitMoveInstruction(uint instruction)
            {
                Opc1 = BitHelper.GetValue(instruction, Opc1Offset, Opc1Size);
                CRn = BitHelper.GetValue(instruction, CRnOffset, CRnSize);
                Opc2 = BitHelper.GetValue(instruction, Opc2Offset, Opc2Size);
                CRm = BitHelper.GetValue(instruction, CRmOffset, CRmSize);
                FieldsOnly = instruction & FieldsMask;
            }

            public Coprocessor32BitMoveInstruction(uint opc1, uint crn, uint crm, uint opc2)
            {
                Opc1 = opc1;
                CRn = crn;
                CRm = crm;
                Opc2 = opc2;
                FieldsOnly = (Opc1 << Opc1Offset) | (CRn << CRnOffset) | (CRm << CRmOffset) | (Opc2 << Opc2Offset);
            }

            public override bool Equals(object o)
            {
                return o is Coprocessor32BitMoveInstruction b && this == b;
            }

            public override int GetHashCode()
            {
                return (int)FieldsOnly;
            }

            public override string ToString()
            {
                return $"op1={Opc1}, crn={CRn}, crm={CRm}, op2={Opc2}";
            }

            public uint Opc1 { get; }
            public uint CRn { get; }
            public uint Opc2 { get; }
            public uint CRm { get; }
            public uint FieldsOnly { get; }

            public static readonly uint FieldsMask = BitHelper.CalculateMask(Opc1Size, Opc1Offset) | BitHelper.CalculateMask(CRnSize, CRnOffset)
                | BitHelper.CalculateMask(Opc2Size, Opc2Offset) | BitHelper.CalculateMask(CRmSize, CRmOffset);

            private const int Opc1Size = 3;
            private const int CRnSize = 4;
            private const int Opc2Size = 3;
            private const int CRmSize = 4;

            private const int Opc1Offset = 21;
            private const int CRnOffset = 16;
            private const int Opc2Offset = 5;
            private const int CRmOffset = 0;
        }

        protected struct Coprocessor64BitMoveInstruction
        {
            public Coprocessor64BitMoveInstruction(uint instruction)
            {
                Opc1 = BitHelper.GetValue(instruction, Opc1Offset, Opc1Size);
                CRm = BitHelper.GetValue(instruction, CRmOffset, CRmSize);
                FieldsOnly = instruction & FieldsMask;
            }

            public override string ToString()
            {
                return $"op1={Opc1}, crm={CRm}";
            }

            public uint Opc1 { get; }
            public uint CRm { get; }
            public uint FieldsOnly { get; }

            public static readonly uint FieldsMask = BitHelper.CalculateMask(Opc1Size, Opc1Offset) | BitHelper.CalculateMask(CRmSize, CRmOffset);

            private const int Opc1Size = 4;
            private const int CRmSize = 4;

            private const int Opc1Offset = 4;
            private const int CRmOffset = 0;
        }
    }
}
