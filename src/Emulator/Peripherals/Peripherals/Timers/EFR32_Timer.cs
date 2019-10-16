//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using System;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class EFR32_Timer : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32_Timer(Machine machine, long frequency, TimerWidth width) : base(machine)
        {
            IRQ = new GPIO();
            this.width = width;
            interruptManager = new InterruptManager<Interrupt>(this);

            innerTimer = new LimitTimer(machine.ClockSource, frequency, this, "timer", limit: (1UL << (int)width) - 1, direction: Direction.Ascending, eventEnabled: true, autoUpdate: true);
            innerTimer.LimitReached += LimitReached;
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            interruptManager.Reset();
            innerTimer.Reset();
        }

        public long Size => 0x400;

        [IrqProvider]
        public GPIO IRQ { get; }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithEnumField(0, 2, changeCallback: (Mode _, Mode value) => SetMode(value), name: "MODE")
                .WithReservedBits(2, 1)
                .WithTaggedFlag("SYNC", 3)
                .WithFlag(4, changeCallback: (_, value) => innerTimer.Mode = value ? WorkMode.OneShot : WorkMode.Periodic, name: "OSMEN")
                .WithTaggedFlag("QDM", 5)
                .WithTaggedFlag("DEBUGRUN", 6)
                .WithTaggedFlag("DMACLRACT", 7)
                .WithTag("RISEA", 8, 2)
                .WithTag("FALLA", 10, 2)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("X2CNT", 13)
                .WithTaggedFlag("DISSYNCOUT", 14)
                .WithReservedBits(15, 1)
                .WithTag("CLKSEL", 16, 2)
                .WithReservedBits(18, 6)
                .WithValueField(24, 4, changeCallback: (_, value) => {
                    if(value <= 10)
                    {
                        innerTimer.Divider = 2 << (int)value;
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "Trying to set the prescaler to an invalid value: {0}, ignoring.", 1 << (int)value);
                    }
                }, name: "PRESC")
                .WithTaggedFlag("ATI", 28)
                .WithTaggedFlag("RSSCOIST", 29)
                .WithReservedBits(30, 2)
            ;

            Registers.Command.Define(this)
                .WithFlag(0, FieldMode.Set, changeCallback: (_, __) => innerTimer.Enabled = false)
                .WithFlag(1, FieldMode.Set, changeCallback: (_, __) => innerTimer.Enabled = true)
            ;

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => innerTimer.Enabled)
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => innerTimer.Direction == Direction.Descending)
                .WithTaggedFlag("TOPBV", 2)
                .WithReservedBits(3, 5)
                .WithTaggedFlag("CCVBV0", 8)
                .WithTaggedFlag("CCVBV1", 9)
                .WithTaggedFlag("CCVBV2", 10)
                .WithTaggedFlag("CCVBV2", 11)
                .WithReservedBits(12, 4)
                .WithTaggedFlag("ICV0", 16)
                .WithTaggedFlag("ICV1", 17)
                .WithTaggedFlag("ICV2", 18)
                .WithTaggedFlag("ICV3", 19)
                .WithReservedBits(20, 4)
                .WithTaggedFlag("CCPOL0", 24)
                .WithTaggedFlag("CCPOL1", 25)
                .WithTaggedFlag("CCPOL2", 26)
                .WithTaggedFlag("CCPOL3", 27)
                .WithReservedBits(28, 4)
            ;

            RegistersCollection.AddRegister((long)Registers.InterruptFlag, interruptManager.GetMaskedInterruptFlagRegister<DoubleWordRegister>());
            RegistersCollection.AddRegister((long)Registers.InterruptFlagSet, interruptManager.GetInterruptSetRegister<DoubleWordRegister>());
            RegistersCollection.AddRegister((long)Registers.InterruptFlagClear, interruptManager.GetInterruptClearRegister<DoubleWordRegister>());
            RegistersCollection.AddRegister((long)Registers.InterruptEnable, interruptManager.GetInterruptEnableRegister<DoubleWordRegister>());

            Registers.CounterTopValue.Define(this)
                .WithValueField(0, (int)width, writeCallback: (_, value) => innerTimer.Limit = value, valueProviderCallback: _ => (uint)innerTimer.Limit, name: "TOP")
            ;
            Registers.CounterTopValueBuffer.Define(this)
                .WithValueField(0, (int)width, out topBuffer, name: "TOPB")
            ;

            Registers.CounterValue.Define(this)
                .WithValueField(0, (int)width, writeCallback: (_, value) => innerTimer.Value = value, valueProviderCallback: _ => (uint)innerTimer.Value, name: "CNT")
            ;

        }

        private void LimitReached()
        {
            if(innerTimer.Direction == Direction.Descending)
            {
                interruptManager.SetInterrupt(Interrupt.Underflow);
            }
            else
            {
                interruptManager.SetInterrupt(Interrupt.Overflow);
            }
        }

        private void SetMode(Mode value)
        {
            if(mode == Mode.QuadratureDecoder || mode == Mode.UpDown)
            {
                this.Log(LogLevel.Warning, "Unsupported mode {0}", mode);
                return;
            }
            mode = value;
            innerTimer.Direction = mode == Mode.Up ? Direction.Ascending : Direction.Descending;
        }

        private LimitTimer innerTimer;
        private Mode mode;
        private TimerWidth width;
        private InterruptManager<Interrupt> interruptManager;

        private IValueRegisterField topBuffer;

        public enum TimerWidth
        {
            Bit16 = 16,
            Bit32 = 32,
        }

        private enum Mode
        {
            Up,
            Down,
            UpDown,
            QuadratureDecoder,
        }

        private enum Interrupt
        {
            Overflow,
            Underflow,
            DirectionChanged,
            [NotSettable]
            Reserved,
            CompareCaptureChannel0,
            CompareCaptureChannel1,
            CompareCaptureChannel2,
            CompareCaptureChannel3,
            InputCaptureBufferOverflow0,
            InputCaptureBufferOverflow1,
            InputCaptureBufferOverflow2,
            InputCaptureBufferOverflow3,
        }

        private enum Registers
        {
            Control = 0x0,
            Command = 0x4,
            Status = 0x8,
            InterruptFlag = 0xC,
            InterruptFlagSet = 0x10,
            InterruptFlagClear = 0x14,
            InterruptEnable = 0x18,
            CounterTopValue = 0x1C,
            CounterTopValueBuffer = 0x20,
            CounterValue = 0x24,
            ConfigurationLock = 0x2C,
            IORoutingPinEnable = 0x30,
            IORoutingLocation0 = 0x34,
            IORoutingLocation2 = 0x3C,
            CCChannelControl0 = 0x60,
            CCChannelValue0 = 0x64,
            CCChannelValuePeek0 = 0x68,
            CCChannelBuffer0 = 0x6C,
            CCChannelControl1 = 0x70,
            CCChannelValue1 = 0x74,
            CCChannelValuePeek1 = 0x78,
            CCChannelBuffer1 = 0x7C,
            CCChannelControl2 = 0x80,
            CCChannelValue2 = 0x84,
            CCChannelValuePeek2 = 0x88,
            CCChannelBuffer2 = 0x8C,
            CCChannelControl3 = 0x90,
            CCChannelValue3 = 0x94,
            CCChannelValuePeek3 = 0x98,
            CCChannelBuffer3 = 0x9C,
            DTIControl = 0xA0,
            DTITimeControl = 0xA4,
            DTIFaultConfiguration = 0xA8,
            DTIOutputGenerationEnable = 0xAC,
            DTIFault = 0xB0,
            DTIFaultClear = 0xB4,
            DTIConfigurationLock = 0xB8,
        }
    }
}
