//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Utilities.Binding;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class Xtensa : TranslationCPU, IPeripheralRegister<SemihostingUart, NullRegistrationPoint>
    {
        public Xtensa(string cpuType, IMachine machine, uint cpuId = 0, long frequency = 10000000)
                : base(cpuId, cpuType, machine, Endianess.LittleEndian)
        {
            innerTimers = new ComparingTimer[InnerTimersCount];
            for(var i = 0; i < innerTimers.Length; i++)
            {
                var j = i;
                innerTimers[i] = new ComparingTimer(machine.ClockSource, frequency, this, "", enabled: true, eventEnabled: true);
                innerTimers[i].CompareReached += () => HandleCompareReached(j) ;
            }
            Reset();
        }

        public override void OnGPIO(int number, bool value)
        {
            TlibSetIrqPendingBit((uint)number, value ? 1u : 0u);
            base.OnGPIO(number, value);
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

        public override void Reset()
        {
            base.Reset();
            ShouldEnterDebugMode = true;
        }

        public void Unregister(SemihostingUart peripheral)
        {
            semihostingUart = null;
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        public override string Architecture { get { return "xtensa"; } }

        public override ExecutionMode ExecutionMode
        {
            get
            {
                return base.ExecutionMode;
            }

            set
            {
                base.ExecutionMode = value;
                TlibSetSingleStep(IsSingleStepMode ? 1u : 0u);
            }
        }

        public override string GDBArchitecture { get { return "xtensa"; } }

        public override List<GDBFeatureDescriptor> GDBFeatures => new List<GDBFeatureDescriptor>();

        protected override Interrupt DecodeInterrupt(int number)
        {
            return Interrupt.Hard;
        }

        [Export]
        /* AKA SIMCALL. After the simcall: "a return code will be stored to a2 and an error number to a3." */
        private void DoSemihosting()
        {
            uint op = A[2];

            switch((XtensaSimcallOperation)op){
            case XtensaSimcallOperation.Write:
                uint fd = A[3];
                uint vaddr = A[4];
                uint len = A[5];
                this.Log(LogLevel.Debug, "WRITE SIMCALL: fd={0}; vaddr=0x{1:X}; len={2}", fd, vaddr, len);

                if(semihostingUart == null)
                {
                    this.Log(LogLevel.Warning, "WRITE SIMCALL: Semihosting UART not available!");
                    break;
                }
                if(fd != 1 && fd != 2)
                {
                    this.Log(LogLevel.Warning, "WRITE SIMCALL: Only writing to fd=1 or fd=2 is supported!", fd);
                    break;
                }

                var buf = Bus.ReadBytes(vaddr, (int)len);
                var bufString = System.Text.Encoding.ASCII.GetString(buf);
                using(ObtainGenericPauseGuard())
                {
                    semihostingUart.SemihostingWriteString(bufString);
                }

                A[2] = bufString.Length;
                A[3] = 0; // errno
                return;
            default:
                var opType = typeof(XtensaSimcallOperation);
                this.Log(LogLevel.Warning, "Unimplemented simcall op={0} ({1})!", op,
                    Enum.IsDefined(opType, op) ? Enum.GetName(opType, op) : "UNKNOWN");
                break;
            }
            A[2] = uint.MaxValue;
            A[3] = 88; // ENOSYS
        }

        [Export]
        private ulong GetCPUTime()
        {
            SyncTime();
            return innerTimers[0].Value;
        }

        private void HandleCompareReached(int id)
        {
            // this is a mapping for sample_controller
            var intMap = new uint[] { 6, 10, 13 };

            // this is a mapping for baytrail
            // var intMap = new uint[] { 1, 5, 7 };
            TlibSetIrqPendingBit(intMap[id], 1u);
        }

        [Export]
        private void TimerMod(uint id, ulong value)
        {
            if(id >= InnerTimersCount)
            {
                throw new Exception($"Unsupported compare #{id}");
            }

            innerTimers[id].Compare = value;
        }

        private readonly ComparingTimer[] innerTimers;
        private SemihostingUart semihostingUart = null;

        private const int InnerTimersCount = 3;

        // 649:  Field '...' is never assigned to, and will always have its default value null
#pragma warning disable 649
        [Import]
        private Action<uint, uint> TlibSetIrqPendingBit;

        [Import]
        private Action<uint> TlibSetSingleStep;
#pragma warning restore 649

        private enum XtensaSimcallOperation : uint
        {
            Exit = 1,
            Read = 3,
            Write = 4,
            Open = 5,
            Close = 6,
            Lseek = 19,
            SelectOne = 29,
            ReadArgc = 1000,
            ReadArgvSize = 1001,
            ReadArgv = 1002,
            Memset = 1004,
        }
    }
}

