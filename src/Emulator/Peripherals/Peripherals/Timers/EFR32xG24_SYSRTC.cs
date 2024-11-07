//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class EFR32xG24_SYSRTC : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG24_SYSRTC(Machine machine, long frequency = 32768) : base(machine)
        {
            AppIRQ = new GPIO();
            interruptManager = new InterruptManager<Interrupt>(this, AppIRQ);

            limitTimer = new LimitTimer(machine.ClockSource, frequency, this, "limitTimer", uint.MaxValue, direction: Direction.Ascending, autoUpdate: true, eventEnabled: true);
            limitTimer.LimitReached += () => interruptManager.SetInterrupt(Interrupt.Overflow);

            compare0Timer = new ComparingTimer(machine.ClockSource, frequency, this, "compare0Timer",
                limit: uint.MaxValue, compare: uint.MaxValue, workMode: WorkMode.Periodic, eventEnabled: true);
            compare0Timer.CompareReached += HandleCompare0TimerCompareReached;

            compare1Timer = new ComparingTimer(machine.ClockSource, frequency, this, "compare1Timer",
                limit: uint.MaxValue, compare: uint.MaxValue, workMode: WorkMode.Periodic, eventEnabled: true);
            compare1Timer.CompareReached += HandleCompare1TimerCompareReached;

            capture0Timer = new ComparingTimer(machine.ClockSource, frequency, this, "capture0Timer",
                limit: uint.MaxValue, compare: uint.MaxValue, workMode: WorkMode.Periodic, eventEnabled: true);
            capture0Timer.CompareReached += HandleCapture0TimerCompareReached;

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            limitTimer.Reset();
            compare0Timer.Reset();
            compare1Timer.Reset();
            capture0Timer.Reset();
            interruptManager.Reset();
            AppIRQ.Unset();
        }

        public long Size => 0x4000;

        public GPIO AppIRQ { get; }

        private void DefineRegisters()
        {
            Register.IpVersion.Define(this, 0x1)
                .WithTag("IPVERSION", 0, 32);

            Register.Enable.Define(this)
                .WithTaggedFlag("EN", 0)
                .WithTaggedFlag("DISABLING", 1)
                .WithReservedBits(2, 30);

            Register.SoftwareReset.Define(this)
                .WithTaggedFlag("SWRST", 0)
                .WithTaggedFlag("RESETTING", 1)
                .WithReservedBits(2, 30);

            Register.Config.Define(this)
                .WithTaggedFlag("DEBUGRUN", 0)
                .WithReservedBits(1, 31);

            Register.Command.Define(this)
                .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            return;
                        }
                        limitTimer.Enabled = true;
                        compare0Timer.Enabled = true;
                        compare1Timer.Enabled = true;
                        capture0Timer.Enabled = true;
                    },
                    name: "START")
                .WithFlag(1, FieldMode.Write,
                    writeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            return;
                        }
                        limitTimer.Enabled = false;
                        compare0Timer.Enabled = false;
                        compare1Timer.Enabled = false;
                        capture0Timer.Enabled = false;
                    },
                    name: "STOP")
                .WithReservedBits(2, 30);

            Register.Status.Define(this)
                .WithFlag(0, FieldMode.Read,
                    valueProviderCallback: _ => compare0Timer.Enabled,
                    name: "RUNNING")
                .WithTaggedFlag("LOCKSTATUS", 1)
                .WithReservedBits(2, 30);

            Register.Counter.Define(this)
                .WithValueField(0, 32,
                    valueProviderCallback: _ =>  compare0Timer.Value,
                    writeCallback: (_, value) =>
                    {
                        limitTimer.Value = value;
                        compare0Timer.Value = value;
                        compare1Timer.Value = value;
                        capture0Timer.Value = value;
                    },
                    name: "CNT");

            Register.SyncBusy.Define(this)
                .WithTaggedFlag("START", 0)
                .WithTaggedFlag("STOP", 1)
                .WithTaggedFlag("CNT", 2)
                .WithReservedBits(3, 29);

            Register.Lock.Define(this)
                .WithTag("LOCKKEY", 0, 16)
                .WithReservedBits(16, 16);

            Register.Group0Control.Define(this)
                .WithFlag(0, out group0Compare0Enable, name: "CMP0EN")
                .WithFlag(1, out group0Compare1Enable, name: "CMP1EN")
                .WithFlag(2, out group0Capture0Enable, name: "CAP0EN")
                .WithTag("CMP0CMOA", 3, 3)
                .WithTag("CMP1CMOA", 6, 3)
                .WithTag("CAP0EDGE", 9, 2)
                .WithReservedBits(11, 21)
                .WithChangeCallback((_, __) =>
                {
                    compare0Timer.Enabled = group0Compare0Enable.Value;
                    compare1Timer.Enabled = group0Compare1Enable.Value;
                    capture0Timer.Enabled = group0Capture0Enable.Value;
                    limitTimer.Enabled = compare0Timer.Enabled | compare1Timer.Enabled | capture0Timer.Enabled;
                });

            Register.Group0Compare0.Define(this)
                .WithValueField(0, 32, out group0Compare0Value,
                    writeCallback: (_, val) => compare0Timer.Compare = group0Compare0Value.Value,
                    name: "CMP0VALUE");

            Register.Group0Compare1.Define(this)
                .WithValueField(0, 32, out group0Compare1Value,
                    writeCallback: (_, val) => compare1Timer.Compare = group0Compare1Value.Value,
                    name: "CMP1VALUE");

            Register.Group0Capture0.Define(this)
                .WithValueField(0, 32, out group0Capture0Value,
                    writeCallback: (_, val) => capture0Timer.Compare = group0Capture0Value.Value,
                    name: "CAP0VALUE");

            Register.Group0SyncBusy.Define(this)
                .WithTaggedFlag("CTRL", 0)
                .WithTaggedFlag("CMP0VALUE", 1)
                .WithTaggedFlag("CMP1VALUE", 2)
                .WithReservedBits(3, 29);

            RegistersCollection.AddRegister((long)Register.Group0InterruptFlags, interruptManager.GetRegister<DoubleWordRegister>(
                valueProviderCallback: (irq, _) => interruptManager.IsSet(irq),
                writeCallback: (irq, _, newValue) => interruptManager.SetInterrupt(irq, newValue)));

            RegistersCollection.AddRegister((long)Register.Group0InterruptEnable, interruptManager.GetInterruptEnableRegister<DoubleWordRegister>());

            RegistersCollection.AddRegister((long)Register.Group0InterruptEnable_Set, interruptManager.GetInterruptEnableSetRegister<DoubleWordRegister>());

            RegistersCollection.AddRegister((long)Register.Group0InterruptFlags_Clear, interruptManager.GetInterruptClearRegister<DoubleWordRegister>());

            RegistersCollection.AddRegister((long)Register.Group0InterruptEnable_Clear, interruptManager.GetInterruptEnableClearRegister<DoubleWordRegister>());
        }

        private void HandleCompare0TimerCompareReached()
        {
            if(group0Compare0Enable.Value)
            {
                interruptManager.SetInterrupt(Interrupt.Compare0Match);
            }
        }

        private void HandleCompare1TimerCompareReached()
        {
            if(group0Compare1Enable.Value)
            {
                interruptManager.SetInterrupt(Interrupt.Compare1Match);
            }
        }

        private void HandleCapture0TimerCompareReached()
        {
            if(group0Capture0Enable.Value)
            {
                interruptManager.SetInterrupt(Interrupt.Capture0);
            }
        }

        private readonly InterruptManager<Interrupt> interruptManager;
        private readonly LimitTimer limitTimer;
        private readonly ComparingTimer compare0Timer;
        private readonly ComparingTimer compare1Timer;
        private readonly ComparingTimer capture0Timer;

        private IFlagRegisterField group0Compare0Enable;
        private IFlagRegisterField group0Compare1Enable;
        private IFlagRegisterField group0Capture0Enable;
        private IValueRegisterField group0Compare0Value;
        private IValueRegisterField group0Compare1Value;
        private IValueRegisterField group0Capture0Value;

        private enum Interrupt
        {
            Overflow,
            Compare0Match,
            Compare1Match,
            Capture0
        }

        private enum Register
        {
            IpVersion                       = 0x0000,
            Enable                          = 0x0004,
            SoftwareReset                   = 0x0008,
            Config                          = 0x000C,
            Command                         = 0x0010,
            Status                          = 0x0014,
            Counter                         = 0x0018,
            SyncBusy                        = 0x001C,
            Lock                            = 0x0020,
            Group0InterruptFlags            = 0x0040,
            Group0InterruptEnable           = 0x0044,
            Group0Control                   = 0x0048,
            Group0Compare0                  = 0x004C,
            Group0Compare1                  = 0x0050,
            Group0Capture0                  = 0x0054,
            Group0SyncBusy                  = 0x0058,

            IpVersion_Set                   = 0x1000,
            Enable_Set                      = 0x1004,
            SoftwareReset_Set               = 0x1008,
            Config_Set                      = 0x100C,
            Command_Set                     = 0x1010,
            Status_Set                      = 0x1014,
            Counter_Set                     = 0x1018,
            SyncBusy_Set                    = 0x101C,
            Lock_Set                        = 0x1020,
            Group0InterruptFlags_Set        = 0x1040,
            Group0InterruptEnable_Set       = 0x1044,
            Group0Control_Set               = 0x1048,
            Group0Compare0_Set              = 0x104C,
            Group0Compare1_Set              = 0x1050,
            Group0Capture0_Set              = 0x1054,
            Group0SyncBusy_Set              = 0x1058,

            IpVersion_Clear                 = 0x2000,
            Enable_Clear                    = 0x2004,
            SoftwareReset_Clear             = 0x2008,
            Config_Clear                    = 0x200C,
            Command_Clear                   = 0x2010,
            Status_Clear                    = 0x2014,
            Counter_Clear                   = 0x2018,
            SyncBusy_Clear                  = 0x201C,
            Lock_Clear                      = 0x2020,
            Group0InterruptFlags_Clear      = 0x2040,
            Group0InterruptEnable_Clear     = 0x2044,
            Group0Control_Clear             = 0x2048,
            Group0Compare0_Clear            = 0x204C,
            Group0Compare1_Clear            = 0x2050,
            Group0Capture0_Clear            = 0x2054,
            Group0SyncBusy_Clear            = 0x2058,

            IpVersion_Toggle                = 0x3000,
            Enable_Toggle                   = 0x3004,
            SoftwareReset_Toggle            = 0x3008,
            Config_Toggle                   = 0x300C,
            Command_Toggle                  = 0x3010,
            Status_Toggle                   = 0x3014,
            Counter_Toggle                  = 0x3018,
            SyncBusy_Toggle                 = 0x301C,
            Lock_Toggle                     = 0x3020,
            Group0InterruptFlags_Toggle     = 0x3040,
            Group0InterruptEnable_Toggle    = 0x3044,
            Group0Control_Toggle            = 0x3048,
            Group0Compare0_Toggle           = 0x304C,
            Group0Compare1_Toggle           = 0x3050,
            Group0Capture0_Toggle           = 0x3054,
            Group0SyncBusy_Toggle           = 0x3058,
        }
    }
}
