//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class VeeR_EL2 : RiscV32
    {
        public VeeR_EL2(IMachine machine, IRiscVTimeProvider timeProvider = null, long timerFrequency = 600000000, uint hartId = 0, PrivilegedArchitecture privilegedArchitecture = PrivilegedArchitecture.Priv1_12,
            Endianess endianness = Endianess.LittleEndian, string cpuType = "rv32imc_zicsr_zifencei_zba_zbb_zbc_zbs", PrivilegeLevels privilegeLevels = PrivilegeLevels.MachineUser, bool allowUnalignedAccesses = true)
            : base(machine, cpuType, timeProvider, hartId, privilegedArchitecture, endianness, allowUnalignedAccesses: allowUnalignedAccesses, privilegeLevels: privilegeLevels, pmpNumberOfAddrBits: 30)
        {
            internalTimers = new InternalTimerBlock(machine, this, timerFrequency, (uint)CustomInterrupt.InternalTimer0, (uint)CustomInterrupt.InternalTimer1,
                    (ushort)CustomCSR.InternalTimerCounter0, (ushort)CustomCSR.InternalTimerBound0, (ushort)CustomCSR.InternalTimerControl0,
                    (ushort)CustomCSR.InternalTimerCounter1, (ushort)CustomCSR.InternalTimerBound1, (ushort)CustomCSR.InternalTimerControl1);

            RegisterCustomCSRs();

            this.WfiAsNop = true;
            AddHookAtInterruptBegin(UpdateSecondaryCauseRegister);
        }

        public virtual void RegisterPIC(VeeR_EL2_PIC pic)
        {
            if(PIC != null)
            {
                throw new RecoverableException($"Another {pic.GetType().Name} has been registered to the given CPU already, only one is allowed per CPU");
            }
            PIC = pic;
        }

        public override void Reset()
        {
            base.Reset();
            internalTimers.Reset();
            mscauseValue = 0;
            pendingSecondaryCause = 0;
        }

        public void RaiseExceptionWithSecondaryCause(uint exception, uint secondaryCause)
        {
            pendingSecondaryCause = secondaryCause;
            RaiseException(exception);
        }

        public VeeR_EL2_PIC PIC { get; protected set; }

        /// <remarks>
        /// Called on interrupt start to make sure that normal RISC-V exceptions triggered without
        /// the extra info do not end up with an invalid mscause value
        /// </remarks>
        protected virtual void UpdateSecondaryCauseRegister(ulong exceptionType)
        {
            mscauseValue = pendingSecondaryCause;
            switch(exceptionType)
            {
            case (ulong)ExceptionCodes.InstructionAccessFault:
            case (ulong)ExceptionCodes.Breakpoint:
            case (ulong)ExceptionCodes.LoadAddressMisaligned:
            case (ulong)ExceptionCodes.LoadAccessFault:
            case (ulong)ExceptionCodes.StoreAddressMisaligned:
            case (ulong)ExceptionCodes.StoreAccessFault:
                break;
            default:
                // No other exceptions have secondary cause information
                mscauseValue = 0;
                break;
            }
            pendingSecondaryCause = 0;
        }

        protected InternalTimerBlock internalTimers;

        protected uint mscauseValue, pendingSecondaryCause;

        private void RegisterCustomCSRs()
        {
            RegisterCSRStub(CustomCSR.RegionAccessControl, "mrac");
            RegisterCSRStub(CustomCSR.CorePauseControl, "mcpc");
            RegisterCSRStub(CustomCSR.MemorySynchronizationTrigger, "dmst");
            RegisterCSRStub(CustomCSR.PowerManagementControl, "mpmc");
            RegisterCSRStub(CustomCSR.ICacheArrayWayIndexSelection, "dicawics");
            RegisterCSRStub(CustomCSR.ICacheArrayData0, "dicad0");
            RegisterCSRStub(CustomCSR.ICacheArrayData1, "dicad1");
            RegisterCSRStub(CustomCSR.ICacheArrayGo, "dicago");
            RegisterCSRStub(CustomCSR.ICacheDataArray0High, "dicac0h");
            RegisterCSRStub(CustomCSR.ForceDebugHaltThreshold, "mfdht");
            RegisterCSRStub(CustomCSR.ForceDebugHaltStatus, "mfdhs");
            RegisterCSRStub(CustomCSR.ICacheErrorCounterThreshold, "micect");
            RegisterCSRStub(CustomCSR.ICCMCorrectableErrorCounterThreshold, "miccmect");
            RegisterCSRStub(CustomCSR.DCCMCorrectableErrorCounterThreshold, "mdccmect");
            RegisterCSRStub(CustomCSR.ClockGatingControl, "mcgc");
            RegisterCSRStub(CustomCSR.FeatureDisableControl, "mfdc");

            RegisterCSR((ushort)CustomCSR.MachineSecondaryCause,
                    readOperation: () => (ulong)mscauseValue,
                    writeOperation: value => mscauseValue = (uint)(value & 0xF) // Only 4 lowest bits are writable
            );

            RegisterCSRStub(CustomCSR.DBUSErrorAddressUnlock, "mdeau");
            RegisterCSRStub(CustomCSR.DBUSFirstErrorAddressCapture, "mdseac");
        }

        protected class InternalTimerBlock
        {
            public InternalTimerBlock(IMachine machine, VeeR_EL2 owner, long timerFrequency, uint interrupt0, uint interrupt1, ushort counter0CSR, ushort bound0CSR, ushort control0CSR, ushort counter1CSR, ushort bound1CSR, ushort control1CSR)
            {
                // Hook to clear the timer `mip` bits on interrupt to simulate
                // them clearing on next clock cycle
                owner.AddHookAtInterruptBegin((ulong exceptionType) =>
                {
                    owner.TlibSetMipBit(interrupt0, 0);
                    owner.TlibSetMipBit(interrupt1, 0);
                });

                Timer0 = new InternalTimer(machine, owner, this, false, timerFrequency, interrupt0, counter0CSR, bound0CSR, control0CSR);
                Timer1 = new InternalTimer(machine, owner, this, true, timerFrequency, interrupt1, counter1CSR, bound1CSR, control1CSR);

                Reset();
            }

            public void Reset()
            {
                Timer0.Reset();
                Timer1.Reset();
            }

            public InternalTimer Timer0 { get; }

            public InternalTimer Timer1 { get; }

            public class InternalTimer : LimitTimer
            {
                public InternalTimer(IMachine machine, VeeR_EL2 owner, InternalTimerBlock internalTimerBlock, bool isTimer1, long timerFrequency, uint interrupt, ushort counterCSR, ushort boundCSR, ushort controlCSR)
                    : base(machine.ClockSource, timerFrequency, owner, localName: isTimer1 ? "InteralTimer1" : "InternalTimer0", limit: UInt32.MaxValue, enabled: true, direction: Direction.Ascending, eventEnabled: true)
                {
                    LimitReached += delegate
                    {
                        owner.Log(LogLevel.Noisy, "{0} Limit reached", LocalName);
                        owner.RaiseInterrupt(interrupt);
                    };

                    owner.RegisterCustomInternalInterrupt(interrupt);

                    // Register the CSRs on the CPU
                    owner.RegisterCSR(counterCSR,
                            readOperation: (Func<ulong>)(() =>
                            {
                                owner.SyncTime();
                                return this.InternalTimerCounter;
                            }),
                            writeOperation: (Action<ulong>)(value => this.InternalTimerCounter = (uint)value)
                    );
                    owner.RegisterCSR(boundCSR,
                            readOperation: () => InternalTimerBound,
                            writeOperation: value => InternalTimerBound = (uint)value
                    );
                    owner.RegisterCSR(controlCSR,
                            readOperation: () =>
                            {
                                ulong res = 0;
                                BitHelper.SetBit(ref res, 3, CascadeMode);
                                BitHelper.SetBit(ref res, 2, pauseEnable);
                                BitHelper.SetBit(ref res, 1, haltEnable);
                                BitHelper.SetBit(ref res, 0, Enabled);
                                return res;
                            },
                            writeOperation: value =>
                            {
                                CascadeMode = BitHelper.IsBitSet(value, 3);
                                pauseEnable = BitHelper.IsBitSet(value, 2);
                                haltEnable = BitHelper.IsBitSet(value, 1);
                                Enabled = BitHelper.IsBitSet(value, 0);
                            }
                    );

                    this.internalTimerBlock = internalTimerBlock;
                    this.isTimer1 = isTimer1;
                    this.owner = owner;
                }

                public override void Reset()
                {
                    base.Reset();

                    cascade = false;
                    enabledBit = true;
                    haltEnable = false;
                    pauseEnable = false;
                }

                public override bool Enabled
                {
                    get => enabledBit;
                    set
                    {
                        enabledBit = value;
                        if(isTimer1)
                        {
                            // Cascade mode has timer1 disabled and incremented manually
                            if(cascade)
                            {
                                return;
                            }
                        }
                        base.Enabled = value;
                    }
                }

                public uint InternalTimerBound
                {
                    get => (uint)Limit;
                    set => Limit = value;
                }

                public uint InternalTimerCounter
                {
                    get => (uint)Value;
                    set
                    {
                        //  LimitTimer does not allow setting a value higher than the limit, so clamp to limit
                        if(value >= Limit)
                        {
                            owner.Log(LogLevel.Debug, "Written value to {0} counter ({1}) is larger than the limit ({2}), clampig to the limit", LocalName, value, Limit);
                            Value = Limit;
                        }
                        else
                        {
                            Value = value;
                        }
                    }
                }

                private void CascadeModeTick()
                {
                    var timer1 = internalTimerBlock.Timer1;
                    if(timer1.Enabled)
                    {
                        // OnLimitReached event won't fire automatically when the timer is disabled
                        if(timer1.Increment(1) == 1)
                        {
                            timer1.OnLimitReached();
                        }
                    }
                }

                private bool CascadeMode
                {
                    get => cascade;
                    set
                    {
                        if(value == cascade || !isTimer1)
                        {
                            return;
                        }

                        var timer0 = internalTimerBlock.Timer0;
                        if(value)
                        {
                            owner.Log(LogLevel.Debug, "{0}: Enabled cascade mode", LocalName);
                            timer0.LimitReached += CascadeModeTick;
                            base.Enabled = false; // Automatic increment should always be off in cascade mode
                        }
                        else
                        {
                            owner.Log(LogLevel.Debug, "{0}: Disabled cascade mode", LocalName);
                            timer0.LimitReached -= CascadeModeTick;
                            base.Enabled = enabledBit; // If the timer was enabled in cascade mode, turn on automatic increment
                        }
                        cascade = value;
                    }
                }

                private bool haltEnable, pauseEnable;  // Don't do anything currently

                private bool cascade;  // Only present in control CSR of Timer 1
                private bool enabledBit;

                private readonly InternalTimerBlock internalTimerBlock;
                private readonly bool isTimer1;
                private readonly VeeR_EL2 owner;
            }
        }

        private enum CustomCSR : ushort
        {
            RegionAccessControl = 0x7C0,                                    // mrac
            CorePauseControl = 0x7C2,                                       // mcpc
            MemorySynchronizationTrigger = 0x7C4,                           // dmst
            PowerManagementControl = 0x7C6,                                 // mpmc
            ICacheArrayWayIndexSelection = 0x7C8,                           // dicawics
            ICacheArrayData0 = 0x7C9,                                       // dicad0
            ICacheArrayData1 = 0x7CA,                                       // dicad1
            ICacheArrayGo = 0x7CB,                                          // dicago
            ICacheDataArray0High = 0x7CC,                                   // dicac0h
            ForceDebugHaltThreshold = 0x7CE,                                // mfdht
            ForceDebugHaltStatus = 0x7CF,                                   // mfdhs
            InternalTimerCounter0 = 0x7D2,                                  // mitcnt0
            InternalTimerBound0 = 0x7D3,                                    // mitb0
            InternalTimerControl0 = 0x7D4,                                  // mitctl0
            InternalTimerCounter1 = 0x7D5,                                  // mitcnt1
            InternalTimerBound1 = 0x7D6,                                    // mitb1
            InternalTimerControl1 = 0x7D7,                                  // mitctl1
            ICacheErrorCounterThreshold = 0x7F0,                            // micect
            ICCMCorrectableErrorCounterThreshold = 0x7F1,                   // miccmect
            DCCMCorrectableErrorCounterThreshold = 0x7F2,                   // mdccmect
            ClockGatingControl = 0x7F8,                                     // mcgc
            FeatureDisableControl = 0x7F9,                                  // mfdc
            MachineSecondaryCause = 0x7FF,                                  // mscause
            DBUSErrorAddressUnlock = 0xBC0,                                 // mdeau
            // See VeeR_EL2_PIC for meivt and other PIC-related CSRs.
            DBUSFirstErrorAddressCapture = 0xFC0,                           // mdseac
        }

        private enum CustomInterrupt : ulong
        {
            InternalTimer0 = 29,
            InternalTimer1 = 28,
        }
    }
}