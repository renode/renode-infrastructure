//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class MAX32650_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public MAX32650_GPIO(IMachine machine, int numberOfPins) : base(machine, numberOfPins)
        {
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            IRQ = new GPIO();
            WakeUpIRQ = new GPIO();
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            UpdateInterrupts();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void OnGPIO(int number, bool value)
        {
            var previousValue = State[number];
            base.OnGPIO(number, value);
            OnPinStateChanged(number, previousValue, value);
        }

        public long Size => 0x400;

        public GPIO IRQ { get; }
        public GPIO WakeUpIRQ { get; }

        private void UpdatePinOutput(int idx, bool value)
        {
            if(!pinOutputEnabled[idx].Value)
            {
                this.Log(LogLevel.Warning, "Attempted to set pin{0} to {1}, but it's not set to OUTPUT; ignoring", idx, value);
                return;
            }

            if(!pinEnabled[idx].Value)
            {
                this.Log(LogLevel.Warning, "Attempted to set pin{0} to {1}, but it's not enabled; ignoring", idx, value);
                return;
            }

            if(State[idx] == value)
            {
                return;
            }

            Connections[idx].Set(value);
            State[idx] = value;
        }

        private void OnPinStateChanged(int idx, bool previous, bool current)
        {
            var interruptPending = false;
            switch(interruptPolarity[idx].Value)
            {
                case InterruptPolarity.LowFalling:
                    if(interruptMode[idx].Value == InterruptMode.EdgeTriggered)
                    {
                        interruptPending = !current && (previous != current);
                    }
                    if(interruptMode[idx].Value == InterruptMode.LevelTriggered)
                    {
                        interruptPending = !current;
                    }
                    break;
                case InterruptPolarity.HighRising:
                    if(interruptMode[idx].Value == InterruptMode.EdgeTriggered)
                    {
                        interruptPending = current && (previous != current);
                    }
                    if(interruptMode[idx].Value == InterruptMode.LevelTriggered)
                    {
                        interruptPending = current;
                    }
                    break;
            }

            interruptStatus[idx].Value |= interruptPending;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var pendingInterrupt = false;
            var pendingWakeUp = false;
            for(var i = 0; i < NumberOfConnections; ++i)
            {
                pendingInterrupt |= interruptEnabled[i].Value && interruptStatus[i].Value;
                pendingWakeUp |= wakeupEnabled[i].Value && interruptStatus[i].Value;
            }
            IRQ.Set(pendingInterrupt);
            WakeUpIRQ.Set(pendingWakeUp);
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Enable, new DoubleWordRegister(this, 0xffffffff)
                    .WithFlags(0, 32, out pinEnabled, name: "EN.gpio_en")
                },
                {(long)Registers.EnableSet, new DoubleWordRegister(this, 0xffffffff)
                    .WithFlags(0, 32, name: "EN_SET.all",
                        valueProviderCallback: (i, _) => pinEnabled[i].Value,
                        writeCallback: (i, _, val) => { if(val) pinEnabled[i].Value = true; })
                },
                {(long)Registers.EnableClear, new DoubleWordRegister(this, 0xffffffff)
                    .WithFlags(0, 32, name: "EN_CLR.all",
                        valueProviderCallback: (i, _) => pinEnabled[i].Value,
                        writeCallback: (i, _, val) => { if(val) pinEnabled[i].Value = false; })
                },
                {(long)Registers.OutputEnable, new DoubleWordRegister(this, 0x8000000)
                    .WithFlags(0, 32, out pinOutputEnabled, name: "OUT_EN.gpio_out_en")
                },
                {(long)Registers.OutputEnableSet, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "OUT_EN_SET.all",
                        valueProviderCallback: (i, _) => pinOutputEnabled[i].Value,
                        writeCallback: (i, _, val) => { if(val) pinOutputEnabled[i].Value = true; })
                },
                {(long)Registers.OutputEnableClear, new DoubleWordRegister(this)
                    .WithFlags(0, 32, out pinOutputEnabled, name: "OUT_EN_CLR.all",
                        valueProviderCallback: (i, _) => pinOutputEnabled[i].Value,
                        writeCallback: (i, _, val) => { if(val) pinOutputEnabled[i].Value = false; })
                },
                {(long)Registers.Output, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "OUT.gpio_out",
                        valueProviderCallback: (i, _) => State[i] && pinOutputEnabled[i].Value,
                        writeCallback: (i, _, val) => UpdatePinOutput(i, val))
                },
                {(long)Registers.OutputSet, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "OUT_SET.gpio_out_set",
                        valueProviderCallback: (i, _) => State[i] && pinOutputEnabled[i].Value,
                        writeCallback: (i, _, val) => { if(val) UpdatePinOutput(i, true) ;})
                },
                {(long)Registers.OutputClear, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "OUT_CLR.gpio_out_clr",
                        valueProviderCallback: (i, _) => State[i] && pinOutputEnabled[i].Value,
                        writeCallback: (i, _, val) => { if(val) UpdatePinOutput(i, false); })
                },
                {(long)Registers.Input, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "IN.gpio_in",
                        valueProviderCallback: (i, _) =>
                        {
                            if(!pinInputEnabled[i].Value)
                            {
                                this.Log(LogLevel.Noisy, "Trying to get value from pin {0} which doesn't have input mode enabled");
                                return false;
                            }
                            return State[i];
                        })
                },
                {(long)Registers.InterruptMode, new DoubleWordRegister(this)
                    .WithEnumFields<DoubleWordRegister, InterruptMode>(0, 1, 32, out interruptMode, name: "INT_MOD.gpio_int_mod")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptPolarity, new DoubleWordRegister(this)
                    .WithEnumFields<DoubleWordRegister, InterruptPolarity>(0, 1, 32, out interruptPolarity, name: "INT_POL.gpio_int_pol")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InputEnabled, new DoubleWordRegister(this, 0xffffffff)
                    .WithFlags(0, 32, out pinInputEnabled, name: "IN_EN.gpio_in_en")
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlags(0, 32, out interruptEnabled, name: "INT_EN.gpio_int_en")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnableSet, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "INT_EN_SET.gpio_int_en_set",
                        valueProviderCallback: (i, _) => interruptEnabled[i].Value,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                interruptEnabled[i].Value = true;
                            }
                        })
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnableClear, new DoubleWordRegister(this)
                    .WithFlags(0, 32,  name: "INT_EN_CLR.gpio_int_en_clr",
                        valueProviderCallback: (i, _) => interruptEnabled[i].Value,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                interruptEnabled[i].Value = false;
                            }
                        })
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptStatus, new DoubleWordRegister(this)
                    .WithFlags(0, 32, out interruptStatus, FieldMode.Read, name: "INT_STAT.gpio_int_stat")
                },
                {(long)Registers.InterruptStatusClear, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "INT_CLR.all",
                        valueProviderCallback: (i, _) => interruptStatus[i].Value,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                interruptStatus[i].Value = false;
                            }
                        })
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.WakeEnable, new DoubleWordRegister(this)
                    .WithFlags(0, 32, out wakeupEnabled, name: "WAKE_EN.gpio_wake_en")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.WakeEnableSet, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "WAKE_EN_SET.all",
                        valueProviderCallback: (i, _) => wakeupEnabled[i].Value,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                wakeupEnabled[i].Value = true;
                            }
                        })
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.WakeEnableClear, new DoubleWordRegister(this)
                    .WithFlags(0, 32, name: "WAKE_EN_CLR.all",
                        valueProviderCallback: (i, _) => wakeupEnabled[i].Value,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                wakeupEnabled[i].Value = false;
                            }
                        })
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptDualEdge, new DoubleWordRegister(this)
                    .WithTag("INT_DUAL_EDGE.gpio_int_dual_edge", 0, 32)
                },
                {(long)Registers.InputMode1, new DoubleWordRegister(this)
                    .WithTag("PAD_CFG1.gpio_pad_cfg1", 0, 32)
                },
                {(long)Registers.InputMode2, new DoubleWordRegister(this)
                    .WithTag("PAD_CFG2.gpio_pad_cfg2", 0, 32)
                },
                {(long)Registers.AlternateFunction1, new DoubleWordRegister(this)
                    .WithTag("EN1.gpio_en1", 0, 32)
                },
                {(long)Registers.AlternateFunction1Set, new DoubleWordRegister(this)
                    .WithTag("EN1_SET.all", 0, 32)
                },
                {(long)Registers.AlternateFunction1Clear, new DoubleWordRegister(this)
                    .WithTag("EN1_CLR.all", 0, 32)
                },
                {(long)Registers.AlternateFunction2, new DoubleWordRegister(this)
                    .WithTag("EN2.gpio_en2", 0, 32)
                },
                {(long)Registers.AlternateFunction2Set, new DoubleWordRegister(this)
                    .WithTag("EN2_SET.all", 0, 32)
                },
                {(long)Registers.AlternateFunction2Clear, new DoubleWordRegister(this)
                    .WithTag("EN2_CLR.all", 0, 32)
                },
                {(long)Registers.DriveStrength1, new DoubleWordRegister(this)
                    .WithTag("DS.ds", 0, 32)
                },
                {(long)Registers.DriveStrength2, new DoubleWordRegister(this)
                    .WithTag("DS1.all", 0, 32)
                },
                {(long)Registers.PullUpPullDown, new DoubleWordRegister(this)
                    .WithTag("PS.all", 0, 32)
                },
                {(long)Registers.Voltage, new DoubleWordRegister(this)
                    .WithTag("VSSEL.all", 0, 32)
                }
            };
            return registersMap;
        }

        private IFlagRegisterField[] pinEnabled;
        private IFlagRegisterField[] pinOutputEnabled;
        private IFlagRegisterField[] pinInputEnabled;
        private IFlagRegisterField[] interruptEnabled;
        private IFlagRegisterField[] interruptStatus;
        private IFlagRegisterField[] wakeupEnabled;

        private IEnumRegisterField<InterruptMode>[] interruptMode;
        private IEnumRegisterField<InterruptPolarity>[] interruptPolarity;

        private readonly DoubleWordRegisterCollection registers;

        enum InterruptMode
        {
            LevelTriggered = 0,
            EdgeTriggered,
        }

        enum InterruptPolarity
        {
            LowFalling = 0,
            HighRising,
        }

        private enum Registers
        {
            Enable = 0x00,
            EnableSet = 0x04,
            EnableClear = 0x08,
            OutputEnable = 0x0C,
            OutputEnableSet = 0x10,
            OutputEnableClear = 0x14,
            Output = 0x18,
            OutputSet = 0x1C,
            OutputClear = 0x20,
            Input = 0x24,
            InterruptMode = 0x28,
            InterruptPolarity = 0x2C,
            InputEnabled = 0x30,
            InterruptEnable = 0x34,
            InterruptEnableSet = 0x38,
            InterruptEnableClear = 0x3C,
            InterruptStatus = 0x40,
            InterruptStatusClear = 0x48,
            WakeEnable = 0x4C,
            WakeEnableSet = 0x50,
            WakeEnableClear = 0x54,
            InterruptDualEdge = 0x5C,
            InputMode1 = 0x60,
            InputMode2 = 0x64,
            AlternateFunction1 = 0x68,
            AlternateFunction1Set = 0x6C,
            AlternateFunction1Clear = 0x70,
            AlternateFunction2 = 0x74,
            AlternateFunction2Set = 0x78,
            AlternateFunction2Clear = 0x7C,
            DriveStrength1 = 0xB0,
            DriveStrength2 = 0xB4,
            PullUpPullDown = 0xB8,
            Voltage = 0xC0,
        }
    }
}
