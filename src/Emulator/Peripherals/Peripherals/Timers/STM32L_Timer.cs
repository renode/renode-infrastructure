//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class STM32L_Timer : LimitTimer, IDoubleWordPeripheral
    {
        public STM32L_Timer(Machine machine) : base(machine.ClockSource, 0xF42400, direction: Direction.Ascending) //freq guesed from driver
        {
            IRQ = new GPIO();
            SetupRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
        }

        public GPIO IRQ { get; private set; }

        protected override void OnLimitReached()
        {
            Limit = autoReload.Value;
            UpdateEvent();
        }

        private void SetupRegisters()
        {
            autoReload = DoubleWordRegister.CreateRWRegister(0xFFFF, "TIMx_ARR");
            var interruptEnableRegister = new DoubleWordRegister(this);
            updateInterruptEnable = interruptEnableRegister.DefineFlagField(0, name: "UIE");
            var prescalerRegister = new DoubleWordRegister(this);
            prescaler = prescalerRegister.DefineValueField(0, 15);

            var controlRegister = new DoubleWordRegister(this)
                .WithEnumField<DoubleWordRegister, Direction>(4, 1, valueProviderCallback: _ => Direction, changeCallback: (_, value) => Direction = value, name: "DIR")
                .WithFlag(0, changeCallback: (_, value) => Enabled = value, valueProviderCallback: _ => Enabled, name: "CEN");
            updateRequestSource = controlRegister.DefineFlagField(2, name: "URS");
            updateDisable = controlRegister.DefineFlagField(1, name: "UDIS");
            registers = new DoubleWordRegisterCollection(this, new Dictionary<long, DoubleWordRegister>
            {
                { (long)Registers.Control1, controlRegister },
                { (long)Registers.DmaInterruptEnable, interruptEnableRegister },
                { (long)Registers.Status, new DoubleWordRegister(this)
                        // This write callback is here only to prevent from very frequent logging.
                        .WithValueField(1, 31, FieldMode.WriteZeroToClear, writeCallback: (_, __) => {})
                        .WithFlag(0, FieldMode.ReadToClear | FieldMode.WriteZeroToClear, changeCallback: (_, __) => IRQ.Unset(), valueProviderCallback: _ => IRQ.IsSet) },
                { (long)Registers.EventGeneration, new DoubleWordRegister(this).WithFlag(0, FieldMode.Write, writeCallback: UpdateGeneration) },
                { (long)Registers.Prescaler, prescalerRegister },
                { (long)Registers.AutoReload, autoReload },
                { (long)Registers.Counter, new DoubleWordRegister(this).WithValueField(0, 16, name: "Counter", valueProviderCallback: _ => (uint)Value, writeCallback: (_, value) => { Value = value; }) }
            });
        }

        private void UpdateGeneration(bool oldValue, bool newValue)
        {
            Divider = (int)prescaler.Value + 1;
            if(newValue && !updateRequestSource.Value)
            {
                UpdateEvent();
            }
        }

        private void UpdateEvent()
        {
            if(updateDisable.Value)
            {
                return;
            }
            Divider = (int)prescaler.Value + 1;
            if(updateInterruptEnable.Value)
            {
                Update();
            }
        }

        private void Update()
        {
            IRQ.Set();
        }

        private DoubleWordRegisterCollection registers;
        private DoubleWordRegister autoReload;
        private IFlagRegisterField updateInterruptEnable, updateRequestSource, updateDisable;
        private IValueRegisterField prescaler;

        private enum Registers
        {
            Control1 = 0x00,
            Control2 = 0x04,
            SlaveModeControl = 0x08,
            DmaInterruptEnable = 0x0C,
            Status = 0x10,
            EventGeneration = 0x14,
            CaptureCompareMode1 = 0x18,
            CaptureCompareMode2 = 0x1C,
            CaptureCompareEnable = 0x20,
            Counter = 0x24,
            Prescaler = 0x28,
            AutoReload = 0x2C,
            CaptureCompare1 = 0x34,
            CaptureCompare2 = 0x38,
            CaptureCompare3 = 0x3C,
            CaptureCompare4 = 0x40,
            DmaControl = 0x48,
            DmaAddress = 0x4C,
            Option = 0x50
        }
    }
}
