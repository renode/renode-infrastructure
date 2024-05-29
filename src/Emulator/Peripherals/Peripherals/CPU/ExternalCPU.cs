//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using ELFSharp.ELF;
using Machine = Antmicro.Renode.Core.Machine;

namespace Antmicro.Renode.Peripherals.CPU
{
    // parts of this class can be left unmodified;
    // to integrate an external simulator you need to
    // look for comments in the code below
    public class ExternalCPU : BaseCPU, IGPIOReceiver, ITimeSink, IDisposable
    {
        public ExternalCPU(string cpuType, IMachine machine, Endianess endianness, CpuBitness bitness = CpuBitness.Bits32)
            : base(0, cpuType, machine, endianness, bitness)
        {
            throw new Exception("This is only a stub class that should not be directly used in a platform");
        }

        public override void Start()
        {
            base.Start();

            // [Here goes an invocation enabling an external simulator (if needed)]
            // [This can be used to initialize the simulator, but no actual instructions should be executed yet]
        }

        public override void Reset()
        {
            base.Reset();

            instructionsExecutedThisRound = 0;
            totalExecutedInstructions = 0;

            // [Here goes an invocation resetting the external simulator (if needed)]
            // [This can be used to revert the internal state of the simulator to the initial form]
        }

        public override void Dispose()
        {
            base.Dispose();
            // [Here goes an invocation disposing the external simulator (if needed)]
            // [This can be used to clean all unmanaged resources used to communicate with the simulator]
        }

        public void OnGPIO(int number, bool value)
        {
            this.NoisyLog("IRQ {0}, value {1}", number, value);
            if(!IsStarted)
            {
                return;
            }

            // [Here goes an invocation triggering an IRQ in the external simulator]
        }

        public virtual void SetRegisterValue32(int register, uint value)
        {
            // [Here goes an invocation setting the register value in the external simulator]
        }

        public virtual uint GetRegisterValue32(int register)
        {
            // [Here goes an invocation reading the register value from the external simulator]
            return 0;
        }

        public override ExecutionResult ExecuteInstructions(ulong numberOfInstructionsToExecute, out ulong numberOfExecutedInstructions)
        {
            instructionsExecutedThisRound = 0UL;

            try
            {
                // [Here comes the invocation of the external simulator for the given amount of instructions]
                // [This is the place where simulation of acutal instructions is to be executed]
            }
            catch(Exception)
            {
                this.NoisyLog("CPU exception detected, halting.");
                InvokeHalted(new HaltArguments(HaltReason.Abort, this));
                return ExecutionResult.Aborted;
            }
            finally
            {
                numberOfExecutedInstructions = instructionsExecutedThisRound;
                totalExecutedInstructions += instructionsExecutedThisRound;
            }

            return ExecutionResult.Ok;
        }

        public override string Architecture => "Unknown";

        public override RegisterValue PC
        {
            get
            {
                return GetRegisterValue32(PCRegisterId);
            }

            set
            {
                SetRegisterValue32(PCRegisterId, value);
            }
        }

        public override ulong ExecutedInstructions => totalExecutedInstructions;

        private ulong instructionsExecutedThisRound;
        private ulong totalExecutedInstructions;

        // [This needs to be mapped to the id of the Program Counter register used by the simulator]
        private const int PCRegisterId = 0;
    }
}
