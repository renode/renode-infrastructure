//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_AlertHandler: BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver
    {
        public OpenTitan_AlertHandler(IMachine machine) : base(machine)
        {
            alertCause = new IFlagRegisterField[AlertsCount];
            alertEnabled = new IFlagRegisterField[AlertsCount];
            alertClasses = new IEnumRegisterField<AlertClass>[AlertsCount];

            localAlertCause = new IFlagRegisterField[LocalAlertsCount];
            localAlertEnabled = new IFlagRegisterField[LocalAlertsCount];
            localAlertClasses = new IEnumRegisterField<AlertClass>[LocalAlertsCount];

            ClassAInterrupt = new GPIO();
            ClassBInterrupt = new GPIO();
            ClassCInterrupt = new GPIO();
            ClassDInterrupt = new GPIO();

            DefineRegisters();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number >= AlertsCount)
            {
                this.Log(LogLevel.Error, "This peripheral can only handle {0}, the alert {1} is not valid", AlertsCount, number);
            }
            if(value)
            {
                this.DebugLog("Alert {0} triggered", number);
                alertCause[number].Value = value;
                var alertClass = alertClasses[number].Value;
                var enabled = alertEnabled[number].Value;
                if(value & enabled)
                {
                    HandleAlertOfClass(alertClass);
                }
            }
        }

        public GPIO ClassAInterrupt { get; }
        public GPIO ClassBInterrupt { get; }
        public GPIO ClassCInterrupt { get; }
        public GPIO ClassDInterrupt { get; }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.InterruptState.Define(this)
                .WithFlag(0, out classAInterruptTriggered, FieldMode.Read | FieldMode.WriteOneToClear, name: "classa")
                .WithFlag(1, out classBInterruptTriggered, FieldMode.Read | FieldMode.WriteOneToClear, name: "classb")
                .WithFlag(2, out classCInterruptTriggered, FieldMode.Read | FieldMode.WriteOneToClear, name: "classc")
                .WithFlag(3, out classDInterruptTriggered, FieldMode.Read | FieldMode.WriteOneToClear, name: "classd")
                .WithWriteCallback((_, val) => { if(val != 0) UpdateInterrupts(); })
                .WithReservedBits(4, 32 - 4);

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out classAInterruptEnable, name: "classa")
                .WithFlag(1, out classBInterruptEnable, name: "classb")
                .WithFlag(2, out classCInterruptEnable, name: "classc")
                .WithFlag(3, out classDInterruptEnable, name: "classd")
                .WithWriteCallback((_, __) => UpdateInterrupts())
                .WithReservedBits(4, 32 - 4);

            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) classAInterruptTriggered.Value = true; }, name: "classa")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) classBInterruptTriggered.Value = true; }, name: "classb")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, val) => { if(val) classCInterruptTriggered.Value = true; }, name: "classc")
                .WithFlag(3, FieldMode.Write, writeCallback: (_, val) => { if(val) classDInterruptTriggered.Value = true; }, name: "classd")
                .WithWriteCallback((_, val) => { if(val != 0) UpdateInterrupts(); })
                .WithReservedBits(4, 32 - 4);

            Registers.PingTimerRegisterWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("PING_TIMER_REGWEN", 0)
                .WithReservedBits(1, 31)
                // Locking registers means that the configuration is complete and the feature may be enabled.
                // This very moment we should start `pinging` connected peripherals
                .WithWriteCallback((_, val) => { if(val == 0) PingTimeout(); });

            Registers.PingTimeoutCycle.Define(this, 0x100)
                .WithValueField(0, 16, out pingTimeout, name: "PING_TIMEOUT_CYC_SHADOWED")
                .WithReservedBits(16, 16);

            Registers.PingTimerEnable.Define(this)
                // The `Read | Set` mode means that it cannot be disabled - this is intentional
                .WithFlag(0, out pingTimeoutEnable, FieldMode.Read | FieldMode.Set, name: "PING_TIMER_EN_SHADOWED")
                .WithReservedBits(1, 31);

            Registers.AlertRegisterWriteEnable0.DefineMany(this, AlertsCount, resetValue: 0x1, setup: (register, idx) =>
                register
                    // The value of this register does not matter. We just need it to retain value
                    .WithFlag(0, FieldMode.Read | FieldMode.WriteZeroToClear, name: $"EN_{idx}")
                    .WithReservedBits(1, 31));

            Registers.AlertEnable0.DefineMany(this, AlertsCount, (register, idx) =>
                register
                    .WithFlag(0, out alertEnabled[idx], name: $"EN_A_{idx}")
                    .WithReservedBits(1, 31));

            Registers.AlertClass0.DefineMany(this, AlertsCount, (register, idx) =>
                register
                    .WithEnumField<DoubleWordRegister, AlertClass>(0, 2, out alertClasses[idx], name: $"CLASS_A_{idx}")
                    .WithReservedBits(2, 30));

            Registers.AlertCause0.DefineMany(this, AlertsCount, (register, idx) =>
                register
                    .WithFlag(0, out alertCause[idx], FieldMode.Read | FieldMode.WriteOneToClear, name: $"A_{idx}")
                    .WithReservedBits(1, 31));

            Registers.LocalAlertRegisterWriteEnable0.DefineMany(this, LocalAlertsCount, resetValue: 0x1, setup: (register, idx) =>
                register
                    // The value of this register does not matter. We just need it to retain value
                    .WithFlag(0, FieldMode.Read | FieldMode.WriteZeroToClear, name: $"EN_{idx}")
                    .WithReservedBits(1, 31));

            Registers.LocalAlertEnable0.DefineMany(this, LocalAlertsCount, (register, idx) =>
                register
                    .WithFlag(0, out localAlertEnabled[idx], name: $"EN_LA_{idx}")
                    .WithReservedBits(1, 31));

            Registers.LocalAlertClass0.DefineMany(this, LocalAlertsCount, (register, idx) =>
                register
                    .WithEnumField<DoubleWordRegister, AlertClass>(0, 2, out localAlertClasses[idx], name: $"CLASS_LA_{idx}")
                    .WithReservedBits(2, 30));

            Registers.LocalAlertCause0.DefineMany(this, LocalAlertsCount, (register, idx) =>
                register
                    .WithFlag(0, out localAlertCause[idx], FieldMode.Read | FieldMode.WriteOneToClear, name: $"LA_{idx}")
                    .WithReservedBits(1, 31));

            var controlRegisterOffset = Registers.ClassAControl - Registers.ClassARegisterWriteEnable;
            var clearWenRegisterOffset = Registers.ClassAClearRegisterWriteEnable - Registers.ClassARegisterWriteEnable;
            var clearRegisterOffset = Registers.ClassAClear - Registers.ClassARegisterWriteEnable;
            var accumulationRegisterOffset = Registers.ClassAAccumulationCount - Registers.ClassARegisterWriteEnable;
            var accumulationThresholdRegisterOffset = Registers.ClassAAccumulationThreshold - Registers.ClassARegisterWriteEnable;
            var timeoutCycleRegisterOffset = Registers.ClassATimeoutCycle - Registers.ClassARegisterWriteEnable;
            var crashdumpTriggerRegisterOffset = Registers.ClassACrashdumpTrigger - Registers.ClassARegisterWriteEnable;
            var phase0CycleRegisterOffset = Registers.ClassAPhase0Cycle - Registers.ClassARegisterWriteEnable;
            var phase1CycleRegisterOffset = Registers.ClassAPhase1Cycle - Registers.ClassARegisterWriteEnable;
            var phase2CycleRegisterOffset = Registers.ClassAPhase2Cycle - Registers.ClassARegisterWriteEnable;
            var phase3RegisterOffset = Registers.ClassAPhase3Cycle - Registers.ClassARegisterWriteEnable;
            var escalationCountRegisterOffset = Registers.ClassAEscalationCount - Registers.ClassARegisterWriteEnable;
            var stateRegisterOffset = Registers.ClassAState - Registers.ClassARegisterWriteEnable;

            foreach(var entry in new Dictionary<string, Registers> {{"A", Registers.ClassARegisterWriteEnable},
                                                                    {"B", Registers.ClassBRegisterWriteEnable},
                                                                    {"C", Registers.ClassCRegisterWriteEnable},
                                                                    {"D", Registers.ClassDRegisterWriteEnable}})
            {
                var className = entry.Key;
                var firstRegister = entry.Value;
                ((Registers)firstRegister).Define(this, 0x1)
                    .WithTaggedFlag($"CLASS${className}_REGWEN", 0)
                    .WithReservedBits(1, 31);

                ((Registers)(firstRegister + controlRegisterOffset)).Define(this, 0x393c)
                    .WithTaggedFlag("EN", 0)
                    .WithTaggedFlag("LOCK", 1)
                    .WithTaggedFlag("EN_E0", 2)
                    .WithTaggedFlag("EN_E1", 3)
                    .WithTaggedFlag("EN_E2", 4)
                    .WithTaggedFlag("EN_E3", 5)
                    .WithTag("MAP_E0", 6, 2)
                    .WithTag("MAP_E1", 8, 2)
                    .WithTag("MAP_E2", 10, 2)
                    .WithTag("MAP_E3", 12, 2)
                    .WithReservedBits(14, 32 - 14);

                ((Registers)(firstRegister + clearWenRegisterOffset)).Define(this, 0x1)
                    .WithTaggedFlag($"CLASS${className}_CLR_REGWEN", 0)
                    .WithReservedBits(1, 31);

                ((Registers)(firstRegister + clearRegisterOffset)).Define(this, 0x1)
                    .WithTaggedFlag($"CLASS${className}_CLR_SHADOWED", 0)
                    .WithIgnoredBits(1, 31);

                ((Registers)(firstRegister + accumulationRegisterOffset)).Define(this)
                    .WithTag($"CLASS${className}_ACCUM_CNT", 0, 16)
                    .WithReservedBits(16, 16);

                ((Registers)(firstRegister + accumulationThresholdRegisterOffset)).Define(this)
                    .WithTag($"CLASS${className}_ACCUM_THRESH_SHADOWED", 0, 16)
                    .WithReservedBits(16, 16);

                ((Registers)(firstRegister + timeoutCycleRegisterOffset)).Define(this)
                    .WithTag($"CLASS${className}_TIMEOUT_CYC_SHADOWED", 0, 32);

                ((Registers)(firstRegister + crashdumpTriggerRegisterOffset)).Define(this)
                    .WithTag($"CLASS${className}_CRASHDUMP_TRIGGER_SHADOWED", 0, 2)
                    .WithReservedBits(2, 30);

                ((Registers)(firstRegister + phase0CycleRegisterOffset)).Define(this)
                    .WithTag($"CLASS${className}_PHASE0_CYC_SHADOWED", 0, 32);

                ((Registers)(firstRegister + phase1CycleRegisterOffset)).Define(this)
                    .WithTag($"CLASS${className}_PHASE1_CYC_SHADOWED", 0, 32);

                ((Registers)(firstRegister + phase2CycleRegisterOffset)).Define(this, 0x1)
                    .WithTag($"CLASS${className}_PHASE2_CYC_SHADOWED", 0, 32);

                ((Registers)(firstRegister + phase3RegisterOffset)).Define(this, 0xa)
                    .WithTag($"CLASS${className}_PHASE3_CYC_SHADOWED", 0, 32);

                ((Registers)(firstRegister + escalationCountRegisterOffset)).Define(this, 0x64)
                    .WithTag($"CLASS${className}_ESC_CNT", 0, 32);

                ((Registers)(firstRegister + stateRegisterOffset)).Define(this, 0x0)
                    .WithTag($"CLASS${className}_STATE", 0, 2)
                    .WithIgnoredBits(2, 30);
            }
        }

        private void PingTimeout()
        {
            // We don't implement the ping timeout fully, the only case we handle is the known alert condition - when the timeout is lower or equal to the ping response `pause cycles` defined in the hardware.
            // In that case we prepare to throw a local alert during the next execution sync. The reason it is postponed is that the software might execute wfi in the few next instructions.
            if(!pingTimeoutEnable.Value || pingTimeout.Value > PingPauseCycles)
            {
                return;
            }
            this.machine.LocalTimeSource.ExecuteInNearestSyncedState(_ => OnLocalAlert(LocalCauses.AlertPingFail));
        }

        private void OnLocalAlert(LocalCauses alert)
        {
            var index = (int) alert;
            localAlertCause[index].Value = true;
            var enabled = localAlertEnabled[index].Value;

            if(enabled)
            {
                var alertClass = localAlertClasses[index].Value;
                HandleAlertOfClass(alertClass);
            }
        }

        private void HandleAlertOfClass(AlertClass alertClass)
        {
            switch(alertClass)
            {
                case AlertClass.A:
                    classAInterruptTriggered.Value = true;
                    break;
                case AlertClass.B:
                    classBInterruptTriggered.Value = true;
                    break;
                case AlertClass.C:
                    classCInterruptTriggered.Value = true;
                    break;
                case AlertClass.D:
                    classDInterruptTriggered.Value = true;
                    break;
                default:
                    throw new ArgumentException("Unknown alert class");
            }
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            ClassAInterrupt.Set(classAInterruptTriggered.Value && classAInterruptEnable.Value);
            ClassBInterrupt.Set(classBInterruptTriggered.Value && classBInterruptEnable.Value);
            ClassCInterrupt.Set(classCInterruptTriggered.Value && classCInterruptEnable.Value);
            ClassDInterrupt.Set(classDInterruptTriggered.Value && classDInterruptEnable.Value);
        }

        private IFlagRegisterField classAInterruptTriggered;
        private IFlagRegisterField classBInterruptTriggered;
        private IFlagRegisterField classCInterruptTriggered;
        private IFlagRegisterField classDInterruptTriggered;
        private IFlagRegisterField classAInterruptEnable;
        private IFlagRegisterField classBInterruptEnable;
        private IFlagRegisterField classCInterruptEnable;
        private IFlagRegisterField classDInterruptEnable;
        private IFlagRegisterField pingTimeoutEnable;

        private IValueRegisterField pingTimeout;

        private readonly IEnumRegisterField<AlertClass>[] alertClasses;
        private readonly IEnumRegisterField<AlertClass>[] localAlertClasses;
        private readonly IFlagRegisterField[] alertEnabled;
        private readonly IFlagRegisterField[] alertCause;
        private readonly IFlagRegisterField[] localAlertEnabled;
        private readonly IFlagRegisterField[] localAlertCause;

        private const uint AlertsCount = 65;
        private const uint LocalAlertsCount = 7;
        private const uint PingPauseCycles = 2;

        #pragma warning disable format
        private enum AlertClass
        {
            A = 0,
            B = 1,
            C = 2,
            D = 3,
        }

        private enum LocalCauses
        {
            AlertPingFail = 0,
            EscalationPingFail = 1,
            AlertIntegrityFail = 2,
            EscalationIntegrityFail = 3,
            BusIntegrityFail = 4,
            ShadowRegisterUpdateError = 5,
            ShadowRegisterStorageError = 6,
        }

        public enum Registers
        {
            InterruptState                 = 0x0,
            InterruptEnable                = 0x4,
            InterruptTest                  = 0x8,
            PingTimerRegisterWriteEnable   = 0xc,
            PingTimeoutCycle               = 0x10,
            PingTimerEnable                = 0x14,
            AlertRegisterWriteEnable0      = 0x18,
            AlertRegisterWriteEnable1      = 0x1c,
            AlertRegisterWriteEnable2      = 0x20,
            AlertRegisterWriteEnable3      = 0x24,
            AlertRegisterWriteEnable4      = 0x28,
            AlertRegisterWriteEnable5      = 0x2c,
            AlertRegisterWriteEnable6      = 0x30,
            AlertRegisterWriteEnable7      = 0x34,
            AlertRegisterWriteEnable8      = 0x38,
            AlertRegisterWriteEnable9      = 0x3c,
            AlertRegisterWriteEnable10     = 0x40,
            AlertRegisterWriteEnable11     = 0x44,
            AlertRegisterWriteEnable12     = 0x48,
            AlertRegisterWriteEnable13     = 0x4c,
            AlertRegisterWriteEnable14     = 0x50,
            AlertRegisterWriteEnable15     = 0x54,
            AlertRegisterWriteEnable16     = 0x58,
            AlertRegisterWriteEnable17     = 0x5c,
            AlertRegisterWriteEnable18     = 0x60,
            AlertRegisterWriteEnable19     = 0x64,
            AlertRegisterWriteEnable20     = 0x68,
            AlertRegisterWriteEnable21     = 0x6c,
            AlertRegisterWriteEnable22     = 0x70,
            AlertRegisterWriteEnable23     = 0x74,
            AlertRegisterWriteEnable24     = 0x78,
            AlertRegisterWriteEnable25     = 0x7c,
            AlertRegisterWriteEnable26     = 0x80,
            AlertRegisterWriteEnable27     = 0x84,
            AlertRegisterWriteEnable28     = 0x88,
            AlertRegisterWriteEnable29     = 0x8c,
            AlertRegisterWriteEnable30     = 0x90,
            AlertRegisterWriteEnable31     = 0x94,
            AlertRegisterWriteEnable32     = 0x98,
            AlertRegisterWriteEnable33     = 0x9c,
            AlertRegisterWriteEnable34     = 0xa0,
            AlertRegisterWriteEnable35     = 0xa4,
            AlertRegisterWriteEnable36     = 0xa8,
            AlertRegisterWriteEnable37     = 0xac,
            AlertRegisterWriteEnable38     = 0xb0,
            AlertRegisterWriteEnable39     = 0xb4,
            AlertRegisterWriteEnable40     = 0xb8,
            AlertRegisterWriteEnable41     = 0xbc,
            AlertRegisterWriteEnable42     = 0xc0,
            AlertRegisterWriteEnable43     = 0xc4,
            AlertRegisterWriteEnable44     = 0xc8,
            AlertRegisterWriteEnable45     = 0xcc,
            AlertRegisterWriteEnable46     = 0xd0,
            AlertRegisterWriteEnable47     = 0xd4,
            AlertRegisterWriteEnable48     = 0xd8,
            AlertRegisterWriteEnable49     = 0xdc,
            AlertRegisterWriteEnable50     = 0xe0,
            AlertRegisterWriteEnable51     = 0xe4,
            AlertRegisterWriteEnable52     = 0xe8,
            AlertRegisterWriteEnable53     = 0xec,
            AlertRegisterWriteEnable54     = 0xf0,
            AlertRegisterWriteEnable55     = 0xf4,
            AlertRegisterWriteEnable56     = 0xf8,
            AlertRegisterWriteEnable57     = 0xfc,
            AlertRegisterWriteEnable58     = 0x100,
            AlertRegisterWriteEnable59     = 0x104,
            AlertRegisterWriteEnable60     = 0x108,
            AlertRegisterWriteEnable61     = 0x10c,
            AlertRegisterWriteEnable62     = 0x110,
            AlertRegisterWriteEnable63     = 0x114,
            AlertRegisterWriteEnable64     = 0x118,
            AlertEnable0                   = 0x10c+0x10,
            AlertEnable1                   = 0x110+0x10,
            AlertEnable2                   = 0x114+0x10,
            AlertEnable3                   = 0x118+0x10,
            AlertEnable4                   = 0x11c+0x10,
            AlertEnable5                   = 0x120+0x10,
            AlertEnable6                   = 0x124+0x10,
            AlertEnable7                   = 0x128+0x10,
            AlertEnable8                   = 0x12c+0x10,
            AlertEnable9                   = 0x130+0x10,
            AlertEnable10                  = 0x134+0x10,
            AlertEnable11                  = 0x138+0x10,
            AlertEnable12                  = 0x13c+0x10,
            AlertEnable13                  = 0x140+0x10,
            AlertEnable14                  = 0x144+0x10,
            AlertEnable15                  = 0x148+0x10,
            AlertEnable16                  = 0x14c+0x10,
            AlertEnable17                  = 0x150+0x10,
            AlertEnable18                  = 0x154+0x10,
            AlertEnable19                  = 0x158+0x10,
            AlertEnable20                  = 0x15c+0x10,
            AlertEnable21                  = 0x160+0x10,
            AlertEnable22                  = 0x164+0x10,
            AlertEnable23                  = 0x168+0x10,
            AlertEnable24                  = 0x16c+0x10,
            AlertEnable25                  = 0x170+0x10,
            AlertEnable26                  = 0x174+0x10,
            AlertEnable27                  = 0x178+0x10,
            AlertEnable28                  = 0x17c+0x10,
            AlertEnable29                  = 0x180+0x10,
            AlertEnable30                  = 0x184+0x10,
            AlertEnable31                  = 0x188+0x10,
            AlertEnable32                  = 0x18c+0x10,
            AlertEnable33                  = 0x190+0x10,
            AlertEnable34                  = 0x194+0x10,
            AlertEnable35                  = 0x198+0x10,
            AlertEnable36                  = 0x19c+0x10,
            AlertEnable37                  = 0x1a0+0x10,
            AlertEnable38                  = 0x1a4+0x10,
            AlertEnable39                  = 0x1a8+0x10,
            AlertEnable40                  = 0x1ac+0x10,
            AlertEnable41                  = 0x1b0+0x10,
            AlertEnable42                  = 0x1b4+0x10,
            AlertEnable43                  = 0x1b8+0x10,
            AlertEnable44                  = 0x1bc+0x10,
            AlertEnable45                  = 0x1c0+0x10,
            AlertEnable46                  = 0x1c4+0x10,
            AlertEnable47                  = 0x1c8+0x10,
            AlertEnable48                  = 0x1cc+0x10,
            AlertEnable49                  = 0x1d0+0x10,
            AlertEnable50                  = 0x1d4+0x10,
            AlertEnable51                  = 0x1d8+0x10,
            AlertEnable52                  = 0x1dc+0x10,
            AlertEnable53                  = 0x1e0+0x10,
            AlertEnable54                  = 0x1e4+0x10,
            AlertEnable55                  = 0x1e8+0x10,
            AlertEnable56                  = 0x1ec+0x10,
            AlertEnable57                  = 0x1f0+0x10,
            AlertEnable58                  = 0x1f4+0x10,
            AlertEnable59                  = 0x1f8+0x10,
            AlertEnable60                  = 0x1fc+0x10,
            AlertEnable61                  = 0x200+0x10,
            AlertEnable62                  = 0x204+0x10,
            AlertEnable63                  = 0x208+0x10,
            AlertEnable64                  = 0x20c+0x10,
            AlertClass0                    = 0x200+0x20,
            AlertClass1                    = 0x204+0x20,
            AlertClass2                    = 0x208+0x20,
            AlertClass3                    = 0x20c+0x20,
            AlertClass4                    = 0x210+0x20,
            AlertClass5                    = 0x214+0x20,
            AlertClass6                    = 0x218+0x20,
            AlertClass7                    = 0x21c+0x20,
            AlertClass8                    = 0x220+0x20,
            AlertClass9                    = 0x224+0x20,
            AlertClass10                   = 0x228+0x20,
            AlertClass11                   = 0x22c+0x20,
            AlertClass12                   = 0x230+0x20,
            AlertClass13                   = 0x234+0x20,
            AlertClass14                   = 0x238+0x20,
            AlertClass15                   = 0x23c+0x20,
            AlertClass16                   = 0x240+0x20,
            AlertClass17                   = 0x244+0x20,
            AlertClass18                   = 0x248+0x20,
            AlertClass19                   = 0x24c+0x20,
            AlertClass20                   = 0x250+0x20,
            AlertClass21                   = 0x254+0x20,
            AlertClass22                   = 0x258+0x20,
            AlertClass23                   = 0x25c+0x20,
            AlertClass24                   = 0x260+0x20,
            AlertClass25                   = 0x264+0x20,
            AlertClass26                   = 0x268+0x20,
            AlertClass27                   = 0x26c+0x20,
            AlertClass28                   = 0x270+0x20,
            AlertClass29                   = 0x274+0x20,
            AlertClass30                   = 0x278+0x20,
            AlertClass31                   = 0x27c+0x20,
            AlertClass32                   = 0x280+0x20,
            AlertClass33                   = 0x284+0x20,
            AlertClass34                   = 0x288+0x20,
            AlertClass35                   = 0x28c+0x20,
            AlertClass36                   = 0x290+0x20,
            AlertClass37                   = 0x294+0x20,
            AlertClass38                   = 0x298+0x20,
            AlertClass39                   = 0x29c+0x20,
            AlertClass40                   = 0x2a0+0x20,
            AlertClass41                   = 0x2a4+0x20,
            AlertClass42                   = 0x2a8+0x20,
            AlertClass43                   = 0x2ac+0x20,
            AlertClass44                   = 0x2b0+0x20,
            AlertClass45                   = 0x2b4+0x20,
            AlertClass46                   = 0x2b8+0x20,
            AlertClass47                   = 0x2bc+0x20,
            AlertClass48                   = 0x2c0+0x20,
            AlertClass49                   = 0x2c4+0x20,
            AlertClass50                   = 0x2c8+0x20,
            AlertClass51                   = 0x2cc+0x20,
            AlertClass52                   = 0x2d0+0x20,
            AlertClass53                   = 0x2d4+0x20,
            AlertClass54                   = 0x2d8+0x20,
            AlertClass55                   = 0x2dc+0x20,
            AlertClass56                   = 0x2e0+0x20,
            AlertClass57                   = 0x2e4+0x20,
            AlertClass58                   = 0x2e8+0x20,
            AlertClass59                   = 0x2ec+0x20,
            AlertClass60                   = 0x2f0+0x20,
            AlertClass61                   = 0x2f4+0x20,
            AlertClass62                   = 0x2f8+0x20,
            AlertClass63                   = 0x2fc+0x20,
            AlertClass64                   = 0x300+0x20,
            AlertCause0                    = 0x2f4+0x30,
            AlertCause1                    = 0x2f8+0x30,
            AlertCause2                    = 0x2fc+0x30,
            AlertCause3                    = 0x300+0x30,
            AlertCause4                    = 0x304+0x30,
            AlertCause5                    = 0x308+0x30,
            AlertCause6                    = 0x30c+0x30,
            AlertCause7                    = 0x310+0x30,
            AlertCause8                    = 0x314+0x30,
            AlertCause9                    = 0x318+0x30,
            AlertCause10                   = 0x31c+0x30,
            AlertCause11                   = 0x320+0x30,
            AlertCause12                   = 0x324+0x30,
            AlertCause13                   = 0x328+0x30,
            AlertCause14                   = 0x32c+0x30,
            AlertCause15                   = 0x330+0x30,
            AlertCause16                   = 0x334+0x30,
            AlertCause17                   = 0x338+0x30,
            AlertCause18                   = 0x33c+0x30,
            AlertCause19                   = 0x340+0x30,
            AlertCause20                   = 0x344+0x30,
            AlertCause21                   = 0x348+0x30,
            AlertCause22                   = 0x34c+0x30,
            AlertCause23                   = 0x350+0x30,
            AlertCause24                   = 0x354+0x30,
            AlertCause25                   = 0x358+0x30,
            AlertCause26                   = 0x35c+0x30,
            AlertCause27                   = 0x360+0x30,
            AlertCause28                   = 0x364+0x30,
            AlertCause29                   = 0x368+0x30,
            AlertCause30                   = 0x36c+0x30,
            AlertCause31                   = 0x370+0x30,
            AlertCause32                   = 0x374+0x30,
            AlertCause33                   = 0x378+0x30,
            AlertCause34                   = 0x37c+0x30,
            AlertCause35                   = 0x380+0x30,
            AlertCause36                   = 0x384+0x30,
            AlertCause37                   = 0x388+0x30,
            AlertCause38                   = 0x38c+0x30,
            AlertCause39                   = 0x390+0x30,
            AlertCause40                   = 0x394+0x30,
            AlertCause41                   = 0x398+0x30,
            AlertCause42                   = 0x39c+0x30,
            AlertCause43                   = 0x3a0+0x30,
            AlertCause44                   = 0x3a4+0x30,
            AlertCause45                   = 0x3a8+0x30,
            AlertCause46                   = 0x3ac+0x30,
            AlertCause47                   = 0x3b0+0x30,
            AlertCause48                   = 0x3b4+0x30,
            AlertCause49                   = 0x3b8+0x30,
            AlertCause50                   = 0x3bc+0x30,
            AlertCause51                   = 0x3c0+0x30,
            AlertCause52                   = 0x3c4+0x30,
            AlertCause53                   = 0x3c8+0x30,
            AlertCause54                   = 0x3cc+0x30,
            AlertCause55                   = 0x3d0+0x30,
            AlertCause56                   = 0x3d4+0x30,
            AlertCause57                   = 0x3d8+0x30,
            AlertCause58                   = 0x3dc+0x30,
            AlertCause59                   = 0x3e0+0x30,
            AlertCause60                   = 0x3e4+0x30,
            AlertCause61                   = 0x3e8+0x30,
            AlertCause62                   = 0x3ec+0x30,
            AlertCause63                   = 0x3f0+0x30,
            AlertCause64                   = 0x3f4+0x30,
            LocalAlertRegisterWriteEnable0 = 0x3e8+0x40,
            LocalAlertRegisterWriteEnable1 = 0x3ec+0x40,
            LocalAlertRegisterWriteEnable2 = 0x3f0+0x40,
            LocalAlertRegisterWriteEnable3 = 0x3f4+0x40,
            LocalAlertRegisterWriteEnable4 = 0x3f8+0x40,
            LocalAlertRegisterWriteEnable5 = 0x3fc+0x40,
            LocalAlertRegisterWriteEnable6 = 0x400+0x40,
            LocalAlertEnable0              = 0x404+0x40,
            LocalAlertEnable1              = 0x408+0x40,
            LocalAlertEnable2              = 0x40c+0x40,
            LocalAlertEnable3              = 0x410+0x40,
            LocalAlertEnable4              = 0x414+0x40,
            LocalAlertEnable5              = 0x418+0x40,
            LocalAlertEnable6              = 0x41c+0x40,
            LocalAlertClass0               = 0x420+0x40,
            LocalAlertClass1               = 0x424+0x40,
            LocalAlertClass2               = 0x428+0x40,
            LocalAlertClass3               = 0x42c+0x40,
            LocalAlertClass4               = 0x430+0x40,
            LocalAlertClass5               = 0x434+0x40,
            LocalAlertClass6               = 0x438+0x40,
            LocalAlertCause0               = 0x43c+0x40,
            LocalAlertCause1               = 0x440+0x40,
            LocalAlertCause2               = 0x444+0x40,
            LocalAlertCause3               = 0x448+0x40,
            LocalAlertCause4               = 0x44c+0x40,
            LocalAlertCause5               = 0x450+0x40,
            LocalAlertCause6               = 0x454+0x40,
            ClassARegisterWriteEnable      = 0x458+0x40,
            ClassAControl                  = 0x45c+0x40,
            ClassAClearRegisterWriteEnable = 0x460+0x40,
            ClassAClear                    = 0x464+0x40,
            ClassAAccumulationCount        = 0x468+0x40,
            ClassAAccumulationThreshold    = 0x46C+0x40,
            ClassATimeoutCycle             = 0x470+0x40,
            ClassACrashdumpTrigger         = 0x474+0x40,
            ClassAPhase0Cycle              = 0x478+0x40,
            ClassAPhase1Cycle              = 0x47C+0x40,
            ClassAPhase2Cycle              = 0x480+0x40,
            ClassAPhase3Cycle              = 0x484+0x40,
            ClassAEscalationCount          = 0x488+0x40,
            ClassAState                    = 0x48C+0x40,
            ClassBRegisterWriteEnable      = 0x490+0x40,
            ClassBControl                  = 0x494+0x40,
            ClassBClearRegisterWriteEnable = 0x498+0x40,
            ClassBClear                    = 0x49C+0x40,
            ClassBAccumulationCount        = 0x4A0+0x40,
            ClassBAccumulationThreshold    = 0x4A4+0x40,
            ClassBTimeoutCycle             = 0x4A8+0x40,
            ClassBCrashdumpTrigger         = 0x4AC+0x40,
            ClassBPhase0Cycle              = 0x4B0+0x40,
            ClassBPhase1Cycle              = 0x4B4+0x40,
            ClassBPhase2Cycle              = 0x4B8+0x40,
            ClassBPhase3Cycle              = 0x4BC+0x40,
            ClassBEscalationCount          = 0x4C0+0x40,
            ClassBState                    = 0x4C4+0x40,
            ClassCRegisterWriteEnable      = 0x4C8+0x40,
            ClassCControl                  = 0x4CC+0x40,
            ClassCClearRegisterWriteEnable = 0x4D0+0x40,
            ClassCClear                    = 0x4D4+0x40,
            ClassCAccumulationCount        = 0x4D8+0x40,
            ClassCAccumulationThreshold    = 0x4DC+0x40,
            ClassCTimeoutCycle             = 0x4E0+0x40,
            ClassCCrashdumpTrigger         = 0x4E4+0x40,
            ClassCPhase0Cycle              = 0x4E8+0x40,
            ClassCPhase1Cycle              = 0x4EC+0x40,
            ClassCPhase2Cycle              = 0x4F0+0x40,
            ClassCPhase3Cycle              = 0x4F4+0x40,
            ClassCEscalationCount          = 0x4F8+0x40,
            ClassCState                    = 0x4FC+0x40,
            ClassDRegisterWriteEnable      = 0x500+0x40,
            ClassDControl                  = 0x504+0x40,
            ClassDClearRegisterWriteEnable = 0x508+0x40,
            ClassDClear                    = 0x50C+0x40,
            ClassDAccumulationCount        = 0x510+0x40,
            ClassDAccumulationThreshold    = 0x514+0x40,
            ClassDTimeoutCycle             = 0x518+0x40,
            ClassDCrashdumpTrigger         = 0x51C+0x40,
            ClassDPhase0Cycle              = 0x520+0x40,
            ClassDPhase1Cycle              = 0x524+0x40,
            ClassDPhase2Cycle              = 0x528+0x40,
            ClassDPhase3Cycle              = 0x52C+0x40,
            ClassDEscalationCount          = 0x530+0x40,
            ClassDState                    = 0x534+0x40,
        }
       #pragma warning restore format
    }
}
