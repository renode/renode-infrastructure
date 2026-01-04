//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

using TimeDirection = Antmicro.Renode.Time.Direction;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class AndesATCWDT200_Watchdog : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize, IHasFrequency
    {
        public AndesATCWDT200_Watchdog(IMachine machine, long clockFrequency)
        {
            this.machine = machine;

            RegistersCollection = new DoubleWordRegisterCollection(this, BuildRegisterMap());

            IRQ = new GPIO();

            interruptTimer = new LimitTimer(machine.ClockSource, clockFrequency, this, "Watchdog Interrupt Timer", DefaultInterruptInterval, TimeDirection.Ascending, eventEnabled: true, workMode: WorkMode.OneShot);
            interruptTimer.LimitReached += OnInterruptTimerElapsed;

            resetTimer = new LimitTimer(machine.ClockSource, clockFrequency, this, "Watchdog Reset Timer", DefaultResetInterval, TimeDirection.Ascending, eventEnabled: true, workMode: WorkMode.OneShot);
            resetTimer.LimitReached += OnResetTimerElapsed;

            Reset();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            IRQ.Unset();
            interruptTimer.Reset();
            resetTimer.Reset();
            writeProtectionEnabled = true;
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(IsWriteProtected(offset))
            {
                this.WarningLog("Ignoring write of value {0} to write-protected register {1} at offset {2}", value, (Registers)offset, offset);
                return;
            }

            RegistersCollection.Write(offset, value);

            if(offset != (long)Registers.WriteEnable)
            {
                writeProtectionEnabled = true;
            }
        }

        public long Size => 0x100;

        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Frequency
        {
            get => interruptTimer.Frequency; // Both timers have the same frequency
            set
            {
                interruptTimer.Frequency = value;
                resetTimer.Frequency = value;
            }
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap() => new Dictionary<long, DoubleWordRegister>
        {
            {(long)Registers.IdAndRevision, new DoubleWordRegister(this)
                .WithValueField(0, 4, name: nameof(RevMinor), mode: FieldMode.Read,
                    valueProviderCallback: _ => RevMinor)
                .WithValueField(4, 8, name: nameof(RevMajor), mode: FieldMode.Read,
                    valueProviderCallback: _ => RevMajor)
                .WithValueField(12, 20, name: nameof(ID), mode: FieldMode.Read,
                    valueProviderCallback: _ => ID)
            },
            {(long)Registers.Control, new DoubleWordRegister(this)
                .WithFlag(0, out watchdogEnabled, name: "En")
                .WithTag("ClkSel", 1, 1)
                .WithFlag(2, out interruptEnabled, name: "IntEn")
                .WithFlag(3, out resetEnabled, name: "RstEn")
                .WithValueField(4, 4, name: "IntTime",
                    writeCallback: (_, value) =>
                    {
                        // Time interval starts at 2^6 when value is 0.
                        interruptTimer.Limit = (ulong)1 << ((int)value + 6);
                    })
                .WithValueField(8, 3, name: "RstTime",
                    writeCallback: (_, value) =>
                    {
                        // Time interval starts at 2^7 when value is 0.
                        resetTimer.Limit = (ulong)1 << ((int)value + 7);
                    })
                .WithReservedBits(11, 21)
                .WithChangeCallback((_, __) => Update())
            },
            {(long)Registers.Restart, new DoubleWordRegister(this)
                .WithValueField(0, 16, name: "Restart", mode: FieldMode.Write,
                    writeCallback: (_, value) =>
                    {
                        if (value != Restart)
                        {
                            return;
                        }
                        interruptTimer.ResetValue();
                        // Cancel the reset timer.
                        resetTimer.Enabled = false;
                        resetTimer.ResetValue();
                        this.DebugLog("Restarted watchdog");
                    })
                .WithReservedBits(16, 16)
            },
            {(long)Registers.WriteEnable, new DoubleWordRegister(this)
                .WithValueField(0, 16, name: "WEn", mode: FieldMode.Write,
                    writeCallback: (_ ,value ) => writeProtectionEnabled = (value != WriteProtectionDisable))
                .WithReservedBits(16, 16)
            },
            {(long)Registers.Status, new DoubleWordRegister(this)
                .WithFlag(0,out interruptExpired, FieldMode.WriteOneToClear | FieldMode.Read, name: "IntExpired",
                    writeCallback: (_, clear) =>
                    {
                        if (clear)
                        {
                            interruptExpired.Value = false;
                            Update();
                        }
                    })
                .WithReservedBits(1, 31)
            },
        };

        private void Update()
        {
            UpdateInterrupts();
            UpdateTimers();
        }

        private void UpdateInterrupts()
        {
            var isIRQSet = interruptExpired.Value && interruptEnabled.Value;
            IRQ.Set(isIRQSet);
            this.DebugLog("IRQ: {0}", isIRQSet);
        }

        private void UpdateTimers()
        {
            interruptTimer.Enabled = !interruptExpired.Value && watchdogEnabled.Value;
            resetTimer.Enabled = watchdogEnabled.Value && resetEnabled.Value && interruptExpired.Value;

            this.DebugLog("interruptTimer enabled: {0}", interruptTimer.Enabled);
            this.DebugLog("resetTimer enabled: {0}", resetTimer.Enabled);
        }

        private bool IsWriteProtected(long offset)
        {
            var isWriteProtectableRegister = offset == (long)Registers.Control || offset == (long)Registers.Restart;
            return writeProtectionEnabled && isWriteProtectableRegister;
        }

        private void OnInterruptTimerElapsed()
        {
            this.DebugLog("Interrupt timer elapsed");
            interruptExpired.Value = true;
            UpdateInterrupts();
            UpdateTimers();
        }

        private void OnResetTimerElapsed()
        {
            this.DebugLog("Reset timer elapsed");
            machine.RequestReset();
        }

        private IFlagRegisterField watchdogEnabled;
        private IFlagRegisterField interruptEnabled;
        private IFlagRegisterField resetEnabled;
        private IFlagRegisterField interruptExpired;
        private bool writeProtectionEnabled = true;

        private readonly IMachine machine;
        private readonly LimitTimer resetTimer;
        private readonly LimitTimer interruptTimer;

        private const ulong ID = 0x03002;
        private const ulong RevMajor = 1;
        private const ulong RevMinor = 2;
        private const ushort WriteProtectionDisable = 0x5AA5; // Magic number for disabling the write protection.
        private const ushort Restart = 0xCAFE; // Magic number for restarting the watchdog timer.
        private const ulong DefaultInterruptInterval = 1ul << 6;
        private const ulong DefaultResetInterval = 1ul << 7;

        private enum Registers : long
        {
            IdAndRevision = 0x00,
            Control = 0x10,
            Restart = 0x14,
            WriteEnable = 0x18,
            Status = 0x1C,
        }
    }
}
