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
    public class Arm_CMSDK_Watchdog : BasicDoubleWordPeripheral, IGPIOReceiver, IKnownSize
    {
        public Arm_CMSDK_Watchdog(IMachine machine, ulong frequency, uint partNumber = 0x824, uint jedecId = 0xBB, uint revision = 0x1) : base(machine)
        {
            this.partNumber = partNumber;
            this.jedecId = jedecId;
            this.revision = revision;
            timer = new LimitTimer(machine.ClockSource, frequency, this, "watchdog", Limit, enabled: true, eventEnabled: true);
            timer.LimitReached += HandleLimitReached;

            DefineRegisters();
        }

        public override void Reset()
        {
            reset = false;
            timer.Reset();
            base.Reset();
            UpdateInterrupts();
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(writeEnabled.Value || offset == (long)Registers.Lock)
            {
                base.WriteDoubleWord(offset, value);
                return;
            }
            this.WarningLog("Attempted a write access to a locked register 0x{0:X} ({1}), value 0x{2:X}", offset, OffsetToString(offset), value);
        }

        public void OnGPIO(int number, bool value)
        {
            // 0 is the WDOGCLKEN signal, which is assumed true after reset
            if(number != 0)
            {
                this.WarningLog("Attempted to change GPIO input #{0}, only 0 (WDOGCLKEN) is legal", number);
                return;
            }

            timer.Enabled = value;
        }

        public long Size => 0x1000;

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        public GPIO ResetRequest { get; } = new GPIO();

        private void HandleLimitReached()
        {
            reset = interrupt.Value;
            interrupt.Value = true;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var wasIrqSet = IRQ.IsSet;
            var wasResetRequestSet = ResetRequest.IsSet;

            if(integrationTestModeEnabled.Value)
            {
                IRQ.Set(interruptTest.Value);
                ResetRequest.Set(resetTest.Value);
            }
            else
            {
                IRQ.Set(interrupt.Value && interruptEnabled.Value);
                ResetRequest.Set(reset && resetEnabled.Value);
            }

            if(wasIrqSet != IRQ.IsSet)
            {
                this.NoisyLog("IRQ: {0}set", wasIrqSet ? "un" : "");
            }
            if(wasResetRequestSet != ResetRequest.IsSet)
            {
                this.NoisyLog("ResetRequest: {0}set", wasResetRequestSet ? "un" : "");
            }
        }

        private void DefineRegisters()
        {
            Registers.Load.Define(this, 0xFFFFFFFF)
                .WithValueField(0, 32,
                    writeCallback: (oldValue, value) =>
                    {
                        if(value == 0)
                        {
                            this.WarningLog("Load value of 0 is illegal, write ignored");
                            return;
                        }
                        timer.Limit = value;
                        timer.ResetValue();
                    },
                    valueProviderCallback: _ => timer.Limit
                )
            ;

            Registers.Value.Define(this, 0xFFFFFFFF)
                .WithValueField(0, 32,
                    valueProviderCallback: _ => Value
                )
            ;

            Registers.Control.Define(this)
                .WithFlag(0, out interruptEnabled, name: "INTEN")
                .WithFlag(1, out resetEnabled, name: "RESEN")
                .WithReservedBits(2, 30)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.ClearInterrupt.Define(this)
                .WithValueField(0, 32, FieldMode.Write)
                .WithWriteCallback((_, __) =>
                {
                    interrupt.Value = false;
                    timer.ResetValue();
                    UpdateInterrupts();
                })
            ;

            Registers.RawInterruptStatus.Define(this)
                .WithFlag(0, out interrupt, FieldMode.Read, name: "Raw Watchdog Interrupt")
                .WithReservedBits(1, 31)
            ;

            Registers.InterruptStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "Watchdog Interrupt",
                    valueProviderCallback: _ => interrupt.Value && interruptEnabled.Value
                )
                .WithReservedBits(1, 31)
            ;

            Registers.Lock.Define(this)
                .WithFlag(0, out writeEnabled, name: "Register write enable status")
                .WithValueField(1, 31, FieldMode.Write, name: "Enable register writes")
                .WithWriteCallback((_, value) =>
                {
                    writeEnabled.Value = value == UnlockValue;
                    this.DebugLog("Write lock {0}abled", writeEnabled.Value ? "en" : "dis");
                })
            ;

            Registers.IntegrationTestControl.Define(this)
                .WithFlag(0, out integrationTestModeEnabled, name: "Integration Test Mode Enable")
                .WithReservedBits(1, 31)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.IntegrationTestOutputSet.Define(this)
                .WithFlag(0, out resetTest, FieldMode.Write, name: "Integration Test WDOGRES value")
                .WithFlag(1, out interruptTest, FieldMode.Write, name: "Integration Test WDOGINT value")
                .WithReservedBits(2, 30)
                .WithChangeCallback((_, __) => UpdateInterrupts())
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

            Registers.PeripheralId0.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Part number[7:0]",
                    valueProviderCallback: _ => BitHelper.GetValue(partNumber, 0, 8)
                )
                .WithReservedBits(8, 24)
            ;

            Registers.PeripheralId1.Define(this)
                .WithValueField(0, 4, FieldMode.Read, name: "Part number[11:8]",
                    valueProviderCallback: _ => BitHelper.GetValue(partNumber, 8, 4)
                )
                .WithValueField(4, 4, FieldMode.Read, name: "jep106_id_3_0",
                    valueProviderCallback: _ => BitHelper.GetValue(jedecId, 0, 4)
                )
                .WithReservedBits(8, 24)
            ;

            Registers.PeripheralId2.Define(this)
                .WithValueField(0, 3, FieldMode.Read, name: "jep106_id_6_4",
                    valueProviderCallback: _ => BitHelper.GetValue(jedecId, 4, 3)
                )
                .WithFlag(3, FieldMode.Read, name: "jedec_used",
                    valueProviderCallback: _ => BitHelper.IsBitSet(jedecId, 8)
                )
                .WithValueField(4, 4, FieldMode.Read, name: "Revision",
                    valueProviderCallback: _ => BitHelper.GetValue(revision, 0, 4)
                )
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
        }

        private bool reset;

        private IFlagRegisterField interruptEnabled;
        private IFlagRegisterField resetEnabled;
        private IFlagRegisterField interrupt;
        private IFlagRegisterField writeEnabled;
        private IFlagRegisterField integrationTestModeEnabled;
        private IFlagRegisterField resetTest;
        private IFlagRegisterField interruptTest;

        private readonly uint partNumber;
        private readonly uint jedecId;
        private readonly uint revision;
        private readonly LimitTimer timer;

        private const uint UnlockValue = 0x1ACCE551;
        private const ulong Limit = UInt32.MaxValue;

        public enum Registers
        {
            Load = 0x00, // WDOGLOAD
            Value = 0x04, // WDOGVALUE
            Control = 0x08, // WDOGCONTROL
            ClearInterrupt = 0x0C, // WDOGINTCLR
            RawInterruptStatus = 0x10, // WDOGRIS
            InterruptStatus = 0x14, // WDOGMIS
            Lock = 0xC00, // WDOGLOCK
            IntegrationTestControl = 0xF00, // WDOGITCR
            IntegrationTestOutputSet = 0xF04, // WDOGITOP
            PeripheralId4 = 0xFD0, // WDOGPERIPHID4
            PeripheralId5 = 0xFD4, // WDOGPERIPHID5a
            PeripheralId6 = 0xFD8, // WDOGPERIPHID6a
            PeripheralId7 = 0xFDC, // WDOGPERIPHID7a
            PeripheralId0 = 0xFE0, // WDOGPERIPHID0
            PeripheralId1 = 0xFE4, // WDOGPERIPHID1
            PeripheralId2 = 0xFE8, // WDOGPERIPHID2
            PeripheralId3 = 0xFEC, // WDOGPERIPHID3
            ComponentId0 = 0xFF0, // WDOGPCELLID0
            ComponentId1 = 0xFF4, // WDOGPCELLID1
            ComponentId2 = 0xFF8, // WDOGPCELLID2
            ComponentId3 = 0xFFC, // WDOGPCELLID3
        }
    }
}
