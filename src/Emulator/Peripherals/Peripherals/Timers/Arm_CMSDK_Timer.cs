//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class Arm_CMSDK_Timer : BasicDoubleWordPeripheral, IKnownSize
    {
        public Arm_CMSDK_Timer(IMachine machine, ulong frequency, uint partNumber = 0x822, uint jedecId = 0xBB, bool loadOnReloadWrite = false) : base(machine)
        {
            this.partNumber = partNumber;
            this.jedecId = jedecId;
            this.loadOnReloadWrite = loadOnReloadWrite;
            timer = new LimitTimer(machine.ClockSource, frequency, this, "timer", Limit, eventEnabled: true);
            timer.Value = 0;
            timer.LimitReached += HandleLimitReached;

            DefineRegisters();
        }

        public override void Reset()
        {
            stoppedAtZero = true;
            timer.Reset();
            timer.Value = 0;
            base.Reset();
            UpdateInterrupts();
        }

        public long Size => 0x1000;

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        private void HandleLimitReached()
        {
            interrupt.Value = true;
            UpdateInterrupts();
            UpdateReload();
        }

        private void UpdateInterrupts()
        {
            var was = IRQ.IsSet;
            IRQ.Set(interrupt.Value && interruptEnabled.Value);
            if(was != IRQ.IsSet)
            {
                this.NoisyLog("IRQ: {0}set", was ? "un" : "");
            }
        }

        private void UpdateReload()
        {
            // stoppedAtZero is equivalent to reloadValue == 0 and timer.Value == 0
            stoppedAtZero = reloadValue.Value == 0;
            if(stoppedAtZero)
            {
                timer.Enabled = false;
                return;
            }

            timer.Value = reloadValue.Value;
            timer.Enabled = enabled.Value;
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, out enabled, name: "Enable",
                    changeCallback: (_, __) => timer.Enabled = !stoppedAtZero && enabled.Value
                )
                .WithFlag(1, out externalInputIsEnable, name: "Select external input as enable",
                    changeCallback: (oldValue, _) =>
                    {
                        this.WarningLog("External input is not implemented");
                        externalInputIsEnable.Value = oldValue;
                    }
                )
                .WithFlag(2, out externalInputIsClock, name: "Select external input as clock",
                    changeCallback: (oldValue, _) =>
                    {
                        this.WarningLog("External input is not implemented");
                        externalInputIsClock.Value = oldValue;
                    }
                )
                .WithFlag(3, out interruptEnabled, name: "Timer interrupt enable")
                .WithReservedBits(4, 28)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.CurrentValue.Define(this)
                .WithValueField(0, 32, name: "Current value",
                    valueProviderCallback: _ => Value,
                    writeCallback: (_, value) => Value = (uint)value
                )
            ;

            Registers.ReloadValue.Define(this)
                .WithValueField(0, 32, out reloadValue, name: "Reload value",
                    changeCallback: (_, __) => { if(stoppedAtZero || loadOnReloadWrite) UpdateReload(); }
                )
            ;

            Registers.TimerInterrupt.Define(this)
                .WithFlag(0, out interrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "Timer interrupt",
                    changeCallback: (_, __) =>
                    {
                        interrupt.Value |= Value == 0;
                        UpdateInterrupts();
                    }
                )
                .WithReservedBits(1, 31)
            ;

            Registers.PeripheralId4.Define(this, 0x04)
                .WithReservedBits(0, 32)
            ;

            Registers.PeripheralId5.Define(this, 0x0)
                .WithReservedBits(0, 32)
            ;

            Registers.PeripheralId6.Define(this, 0x0)
                .WithReservedBits(0, 32)
            ;

            Registers.PeripheralId7.Define(this, 0x0)
                .WithReservedBits(0, 32)
            ;

            Registers.PeripheralId0.Define(this, BitHelper.GetValue(partNumber, 0, 8))
                .WithTag("Part number[7:0]", 0, 8)
                .WithReservedBits(8, 24)
            ;

            Registers.PeripheralId1.Define(this, (BitHelper.GetValue(jedecId, 0, 4) << 4) | BitHelper.GetValue(partNumber, 8, 4))
                .WithTag("Part number[11:8]", 0, 4)
                .WithTag("jep106_id_3_0", 4, 4)
                .WithReservedBits(8, 24)
            ;

            Registers.PeripheralId2.Define(this, BitHelper.GetValue(jedecId, 4, 4))
                .WithTag("jep106_id_6_4", 0, 3)
                .WithTaggedFlag("jedec_used", 3)
                .WithTag("Revision", 4, 4)
                .WithReservedBits(8, 24)
            ;

            Registers.PeripheralId3.Define(this, 0x0)
                .WithTag("Customer modification number", 0, 4)
                .WithTag("ECO revision number", 4, 4)
                .WithReservedBits(8, 24)
            ;

            Registers.ComponentId0.Define(this, 0x0D)
                .WithReservedBits(0, 32)
            ;

            Registers.ComponentId1.Define(this, 0xF0)
                .WithReservedBits(0, 32)
            ;

            Registers.ComponentId2.Define(this, 0x05)
                .WithReservedBits(0, 32)
            ;

            Registers.ComponentId3.Define(this, 0xB1)
                .WithReservedBits(0, 32)
            ;
        }

        private uint Value
        {
            get
            {
                if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    cpu?.SyncTime();
                }

                return (uint)timer.Value;
            }
            set => timer.Value = value;
        }

        private bool stoppedAtZero = true;

        private IFlagRegisterField enabled;
        private IFlagRegisterField externalInputIsEnable;
        private IFlagRegisterField externalInputIsClock;
        private IFlagRegisterField interruptEnabled;
        private IFlagRegisterField interrupt;

        private IValueRegisterField reloadValue;

        private readonly uint partNumber;
        private readonly uint jedecId;
        private readonly bool loadOnReloadWrite;
        private readonly LimitTimer timer;

        private const ulong Limit = UInt32.MaxValue;

        public enum Registers
        {
            Control = 0x000, // CTRL
            CurrentValue = 0x004, // VALUE
            ReloadValue = 0x008, // RELOAD
            TimerInterrupt = 0x00C, // INTSTATUS INTCLEAR
            PeripheralId4 = 0xFD0, // PID4
            PeripheralId5 = 0xFD4, // PID5
            PeripheralId6 = 0xFD8, // PID6
            PeripheralId7 = 0xFDC, // PID7
            PeripheralId0 = 0xFE0, // PID0
            PeripheralId1 = 0xFE4, // PID1
            PeripheralId2 = 0xFE8, // PID2
            PeripheralId3 = 0xFEC, // PID3
            ComponentId0 = 0xFF0, // CID0
            ComponentId1 = 0xFF4, // CID1
            ComponentId2 = 0xFF8, // CID2
            ComponentId3 = 0xFFC // CID3
        }
    }
}
