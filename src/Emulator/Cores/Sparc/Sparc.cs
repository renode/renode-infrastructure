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
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities.Binding;
using System.Collections.Generic;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.IRQControllers;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    // Changing CPU slot after start is not supported because of InitCPUId call and
    // because it is hardly ever needed.
    [GPIO(NumberOfInputs = 3)]
    public partial class Sparc : TranslationCPU
    {
        public Sparc(string cpuType, IMachine machine, Endianess endianness = Endianess.BigEndian): base(cpuType, machine, endianness)
        {
            Init();
        }

        public override string Architecture { get { return "sparc"; } }

        public override string GDBArchitecture { get { return Architecture; } }

        public override List<GDBFeatureDescriptor> GDBFeatures { get { return new List<GDBFeatureDescriptor>(); } }

        public bool ShutdownAsNop
        { 
            get => neverWaitForInterrupt; 
            set
            {
                neverWaitForInterrupt = value;
            }
        }

        private void Init()
        {

        }
            
        public override void Start()
        {
            InitCPUId();
            base.Start();
        }

        public override void OnGPIO(int number, bool value)
        {
            switch(number)
            {
            case 0:
                // Interrupt GPIO set from GaislerIRQMP controller
                if(isPowerDown)
                {
                    // Clear state when CPU has been issued a power-down (ASR19)
                    isPowerDown = false;
                }
                base.OnGPIO(number, value);
                break;
            case 1:
                // Reset GPIO set from GaislerIRQMP controller
                this.Log(LogLevel.Noisy, "Sparc Reset IRQ {0}, value {1}", number, value);
                Reset();
                this.Log(LogLevel.Info, "Setting Entry Point value to 0x{0:X}", this.EntryPoint);
                TlibSetEntryPoint(this.EntryPoint);
                break;
            case 2:
                // Run GPIO set from GaislerIRQMP controller
                this.Log(LogLevel.Noisy, "Sparc Run IRQ {0}, value {1}", number, value);
                // Undo halted CPU
                TlibClearWfi();
                if(IsHalted)
                {
                    IsHalted = false;
                }
                if(this.IsStarted)
                {
                    this.Start();
                }
                break;
            default:
                this.Log(LogLevel.Warning, "GPIO index out of range");
                break;
            }
        }

        private GaislerMIC connectedMIC;

        public GaislerMIC ConnectedMIC
        {
            get 
            {
                if (connectedMIC == null) 
                {
                    var gaislerMics = machine.GetPeripheralsOfType<GaislerMIC>();
                    foreach (var mic in gaislerMics) 
                    {
                        for(var micIndex=0; micIndex < mic.GetNumberOfProcessors(); micIndex++)
                        {
                            var endpoints = mic.GetCurrentCpuIrq(micIndex).Endpoints;
                            for(var i = 0; i < endpoints.Count; ++i)
                            {
                                if(endpoints[i].Receiver == this)
                                {
                                    connectedMIC = mic;
                                    return connectedMIC;
                                }
                            }
                        }
                    }
                }
                return connectedMIC;
            }
        }

        protected override Interrupt DecodeInterrupt(int number)
        {
            switch(number)
            {
            case 0:
                return Interrupt.Hard;
            case 1:
                return Interrupt.TargetExternal0;
            case 2:
                return Interrupt.TargetExternal1;
            default:
                throw InvalidInterruptNumberException;
            }
        }

        protected override string GetExceptionDescription(ulong exceptionIndex)
        {
            return ExceptionDescriptionsMap.TryGetValue(exceptionIndex, out var result)
                ? result
                : base.GetExceptionDescription(exceptionIndex);
        }

        public uint EntryPoint { get; private set; }

        [Export]
        private int FindBestInterrupt()
        {
            if( ConnectedMIC != null )
            {
                if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    return ConnectedMIC.CPUGetInterrupt((int)cpu.MultiprocessingId);
                }
                else
                {
                    this.Log(LogLevel.Warning, "Find best interrupt - Could not get CPUId.");
                }
            }
            return 0;
        }
        
        [Export]
        private void AcknowledgeInterrupt(int interruptNumber)
        {
            if( ConnectedMIC != null )
            {
                if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    ConnectedMIC.CPUAckInterrupt((int)cpu.MultiprocessingId, interruptNumber);
                }
                else
                {
                    this.Log(LogLevel.Warning, "Acknowledge interrupt - Could not get CPUId.");
                }
            }
        }

        [Export]
        private void OnCpuHalted()
        {
            IsHalted = true;
        }

        [Export]
        private void OnCpuPowerDown()
        {
            isPowerDown = true;
            this.NoisyLog("CPU has been powered down");
        }

        private void InitCPUId()
        {
            if(!cpuIdinitialized)
            {
                int cpuid = machine.SystemBus.GetCPUSlot(this);
                // Only update ASR17 for slave cores 1-15
                if(cpuid > 0 && cpuid < 16)
                {
                    TlibSetSlot(cpuid);
                    this.NoisyLog("Current CPUId is {0:X}.", cpuid);
                }
                else
                {
                    this.NoisyLog("Could not set CPUId - value {0:X} is outside of allowed range", cpuid);
                }
                // Halt the slave cores, only core 0 starts automatically
                if(cpuid > 0 && cpuid < 16)
                {
                    TlibSetWfi();
                    this.NoisyLog("Halting current CPU - core number {0:X}.", cpuid);
                }
                cpuIdinitialized = true;
            }
        }

        private void AfterPCSet(uint value)
        {
            SetRegisterValue32((int)SparcRegisters.NPC, value + 4);
            if(!entryPointInitialized)
            {
                EntryPoint = value;
                entryPointInitialized = true;
                this.Log(LogLevel.Info, "Using PC value as Entry Point value : 0x{0:X}", EntryPoint);
            }
        }

        private bool cpuIdinitialized = false;
        private bool entryPointInitialized;
        private bool isPowerDown;

        // 649:  Field '...' is never assigned to, and will always have its default value null
        #pragma warning disable 649

        [Import]
        private ActionInt32 TlibSetSlot;

        [Import]
        private ActionUInt32 TlibSetEntryPoint;

        [Import]
        private Action TlibClearWfi;

        [Import]
        private Action TlibSetWfi;

        #pragma warning restore 649

        private readonly Dictionary<ulong, string> ExceptionDescriptionsMap = new Dictionary<ulong, string>
        {
            {0x01, "Instruction access exception"},
            {0x02, "Illegal instruction"},
            {0x03, "Privileged instruction"},
            {0x04, "FP disabled"},
            {0x05, "Window overflow"},
            {0x06, "Window underflow"},
            {0x07, "Memory address not aligned"},
            {0x08, "FP exception"},
            {0x09, "Data access exception"},
            {0x0A, "Tag overflow"},
            {0x0B, "Watchpoint detected"},
            {0x20, "R register access error"},
            {0x21, "Instruction access error"},
            {0x24, "CP disabled"},
            {0x25, "Unimplemented FLUSH"},
            {0x28, "CP Exception"},
            {0x29, "Data access error"},
            {0x2A, "Division by zero"},
            {0x2B, "Data store error"},
            {0x2C, "Data access MMU miss"},
            {0x3C, "Instruction access MMU miss"}
        };
    }
}

