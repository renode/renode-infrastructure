//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class AmbiqApollo4_GPIO : BaseGPIOPort, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public AmbiqApollo4_GPIO(IMachine machine) : base(machine, NumberOfPins)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();

            outputPinValues.Initialize();
            tristatePinOutputEnabled.Initialize();
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            var oldState = State[number];
            base.OnGPIO(number, value);

            if(inputEnable[number].Value && oldState != value)
            {
                HandlePinStateChangeInterrupt(number, risingEdge: value);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset >= (long)Registers.PinConfiguration0 && offset <= (long)Registers.PinConfiguration127)
            {
                if(padKey.Value != PadKeyUnlockValue)
                {
                    this.Log(LogLevel.Warning, "Tried to change pin configuration register which is locked. PADKEY value: {0:X}", padKey.Value);
                    return;
                }
            }
            RegistersCollection.Write(offset, value);
        }

        public long Size => 0x440;

        public GPIO McuN0IrqBank0 => irq[(int)IrqType.McuN0IrqBank0];
        public GPIO McuN0IrqBank1 => irq[(int)IrqType.McuN0IrqBank1];
        public GPIO McuN0IrqBank2 => irq[(int)IrqType.McuN0IrqBank2];
        public GPIO McuN0IrqBank3 => irq[(int)IrqType.McuN0IrqBank3];
        public GPIO McuN1IrqBank0 => irq[(int)IrqType.McuN1IrqBank0];
        public GPIO McuN1IrqBank1 => irq[(int)IrqType.McuN1IrqBank1];
        public GPIO McuN1IrqBank2 => irq[(int)IrqType.McuN1IrqBank2];
        public GPIO McuN1IrqBank3 => irq[(int)IrqType.McuN1IrqBank3];

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.PinConfiguration0.DefineMany(this, NumberOfPins, (register, regIdx) =>
            {
                register
                    .WithValueField(0, 4, name: $"FUNCSEL{regIdx} - Function select for GPIO pin {regIdx}")
                    .WithFlag(4, out inputEnable[regIdx], name: $"INPEN{regIdx} - Input enable for GPIO {regIdx}",
                        changeCallback: (_, newValue) => { if(State[regIdx]) HandlePinStateChangeInterrupt(regIdx, risingEdge: newValue); })
                    .WithFlag(5, out readZero[regIdx], name: $"RDZERO{regIdx} - Return 0 for read data on GPIO {regIdx}")
                    .WithEnumField(6, 2, out interruptMode[regIdx], name: $"IRPTEN{regIdx} - Interrupt enable for GPIO {regIdx}")
                    .WithEnumField(8, 2, out ioMode[regIdx], name: $"OUTCFG{regIdx} - Pin IO mode selection for GPIO pin {regIdx}",
                        changeCallback: (_, __) => UpdateOutputPinState(regIdx))
                    .WithWriteCallback((_, __) =>
                    {
                        this.Log(LogLevel.Noisy, "Pin #{0} configured to IO mode {1}, input enable: {2}", regIdx, ioMode[regIdx].Value, inputEnable[regIdx].Value);
                    });

                if(regIdx < FirstVirtualPinIndex)
                {
                    register
                        .WithEnumField<DoubleWordRegister, DriveStrength>(10, 2, name: $"DS{regIdx} - Drive strength selection for GPIO {regIdx}")
                        .WithFlag(12, name: $"SR{regIdx} - Configure the slew rate")
                        .WithEnumField<DoubleWordRegister, PullUpDownConfiguration>(13, 3, name: $"PULLCFG30 - Pullup/Pulldown configuration for GPIO {regIdx}")
                        .WithEnumField<DoubleWordRegister, ChipSelectConfiguration>(16, 6, name: $"NCESRC{regIdx} - IOMSTR/MSPI N Chip Select {regIdx}, DISP control signals DE, CSX, and CS")
                        .WithEnumField<DoubleWordRegister, PolarityConfiguration>(22, 1, name: $"NCEPOL{regIdx} - Polarity select for NCE for GPIO {regIdx}")
                        .WithReservedBits(23, 2)
                        .WithFlag(25, name: $"VDDPWRSWEN{regIdx} - VDD power switch enable")
                        .WithFlag(26, name: $"FIEN{regIdx} - Force input enable active regardless of function selected")
                        .WithFlag(27, name: $"FOEN{regIdx} - Force output enable active regardless of function selected")
                        .WithReservedBits(28, 4);
                }
                else
                {
                    register.WithReservedBits(10, 22);
                }
            }, resetValue: 0x3 /* FUNCSEL: GPIO */);

            Registers.OutputWrite0.DefineMany(this, NumberOfBanks, (register, regIdx) =>
            {
                register.WithFlags(0, PinsPerBank, name: $"WT{regIdx} - GPIO Output {regIdx}", writeCallback: (bitIdx, _, value) =>
                {
                    var pinIdx = regIdx * PinsPerBank + bitIdx;

                    // Let's only call the method if the pins value is really changed.
                    if(outputPinValues[pinIdx] != value)
                    {
                        SetOutputPinValue(pinIdx, value);
                    }
                }, valueProviderCallback: (bitIdx, _) =>
                {
                    var pinIdx = regIdx * PinsPerBank + bitIdx;
                    return outputPinValues[pinIdx];
                });
            });

            Registers.OutputSet0.DefineMany(this, NumberOfBanks, (register, regIdx) =>
            {
                register.WithFlags(0, PinsPerBank, name: $"WTS{regIdx} - GPIO Output Set {regIdx}", writeCallback: (bitIdx, _, value) =>
                {
                    if(value)
                    {
                        SetOutputPinValue(regIdx * PinsPerBank + bitIdx, true);
                    }
                });
            });

            Registers.OutputClear0.DefineMany(this, NumberOfBanks, (register, regIdx) =>
            {
                register.WithFlags(0, PinsPerBank, name: $"WTC{regIdx} - GPIO Output Clear {regIdx}", writeCallback: (bitIdx, _, value) =>
                {
                    if(value)
                    {
                        SetOutputPinValue(regIdx * PinsPerBank + bitIdx, false);
                    }
                });
            });

            Registers.GPIOOutputEnable0.DefineMany(this, NumberOfBanks, (register, regIdx) =>
            {
                register.WithFlags(0, PinsPerBank, name: $"EN{regIdx} - GPIO Enable {regIdx}",
                    valueProviderCallback: (bitIdx, _) => tristatePinOutputEnabled[regIdx * PinsPerBank + bitIdx],
                    writeCallback: (bitIdx, _, value) =>
                    {
                        EnableTristatePinOutputState(regIdx * PinsPerBank + bitIdx, value);
                    });
            });

            Registers.GPIOOutputEnableSet0.DefineMany(this, NumberOfBanks, (register, regIdx) =>
            {
                register.WithFlags(0, PinsPerBank, name: $"ENS{regIdx} - GPIO Enable Set {regIdx}",
                    valueProviderCallback: (bitIdx, _) => tristatePinOutputEnabled[regIdx * PinsPerBank + bitIdx],
                    writeCallback: (bitIdx, _, value) =>
                    {
                        if(!value)
                        {
                            return;
                        }

                        EnableTristatePinOutputState(regIdx * PinsPerBank + bitIdx, true);
                    });
            });

            Registers.GPIOOutputEnableClear0.DefineMany(this, NumberOfBanks, (register, regIdx) =>
            {
                register.WithFlags(0, PinsPerBank, name: $"ENC{regIdx} - GPIO Enable Clear {regIdx}",
                    valueProviderCallback: (bitIdx, _) => tristatePinOutputEnabled[regIdx * PinsPerBank + bitIdx],
                    writeCallback: (bitIdx, _, value) =>
                    {
                        if(!value)
                        {
                            return;
                        }

                        EnableTristatePinOutputState(regIdx * PinsPerBank + bitIdx, false);
                    });
            });

            Registers.InputRead0.DefineMany(this, NumberOfBanks, (register, regIdx) =>
            {
                register.WithFlags(0, PinsPerBank, FieldMode.Read, name: $"RD{regIdx} - Pin {regIdx} state", valueProviderCallback: (bitIdx, _) =>
                {
                    var pinIdx = regIdx * PinsPerBank + bitIdx;

                    return readZero[pinIdx].Value || !inputEnable[pinIdx].Value
                        ? false
                        : State[pinIdx];
                });
            });

            foreach(var descriptor in new[]
            {
                new { Register = Registers.MCUPriorityN0InterruptEnable0, Type = IrqType.McuN0IrqBank0 },
                new { Register = Registers.MCUPriorityN0InterruptEnable1, Type = IrqType.McuN0IrqBank1 },
                new { Register = Registers.MCUPriorityN0InterruptEnable2, Type = IrqType.McuN0IrqBank2 },
                new { Register = Registers.MCUPriorityN0InterruptEnable3, Type = IrqType.McuN0IrqBank3 },

                new { Register = Registers.MCUPriorityN1InterruptEnable0, Type = IrqType.McuN1IrqBank0 },
                new { Register = Registers.MCUPriorityN1InterruptEnable1, Type = IrqType.McuN1IrqBank1 },
                new { Register = Registers.MCUPriorityN1InterruptEnable2, Type = IrqType.McuN1IrqBank2 },
                new { Register = Registers.MCUPriorityN1InterruptEnable3, Type = IrqType.McuN1IrqBank3 },
            })
            {
                descriptor.Register.Define(this)
                    .WithFlags(0, PinsPerBank, out irqEnabled[(int)descriptor.Type])
                    .WithWriteCallback((_, __) => UpdateInterrupt(descriptor.Type));
            }

            foreach(var descriptor in new[]
            {
                new { Register = Registers.MCUPriorityN0InterruptStatus0, Type = IrqType.McuN0IrqBank0 },
                new { Register = Registers.MCUPriorityN0InterruptStatus1, Type = IrqType.McuN0IrqBank1 },
                new { Register = Registers.MCUPriorityN0InterruptStatus2, Type = IrqType.McuN0IrqBank2 },
                new { Register = Registers.MCUPriorityN0InterruptStatus3, Type = IrqType.McuN0IrqBank3 },

                new { Register = Registers.MCUPriorityN1InterruptStatus0, Type = IrqType.McuN1IrqBank0 },
                new { Register = Registers.MCUPriorityN1InterruptStatus1, Type = IrqType.McuN1IrqBank1 },
                new { Register = Registers.MCUPriorityN1InterruptStatus2, Type = IrqType.McuN1IrqBank2 },
                new { Register = Registers.MCUPriorityN1InterruptStatus3, Type = IrqType.McuN1IrqBank3 },
            })
            {
                descriptor.Register.Define(this)
                    .WithFlags(0, PinsPerBank, out irqStatus[(int)descriptor.Type], FieldMode.Read);
            }

            foreach(var descriptor in new[]
            {
                new { Register = Registers.MCUPriorityN0InterruptClear0, Type = IrqType.McuN0IrqBank0 },
                new { Register = Registers.MCUPriorityN0InterruptClear1, Type = IrqType.McuN0IrqBank1 },
                new { Register = Registers.MCUPriorityN0InterruptClear2, Type = IrqType.McuN0IrqBank2 },
                new { Register = Registers.MCUPriorityN0InterruptClear3, Type = IrqType.McuN0IrqBank3 },

                new { Register = Registers.MCUPriorityN1InterruptClear0, Type = IrqType.McuN1IrqBank0 },
                new { Register = Registers.MCUPriorityN1InterruptClear1, Type = IrqType.McuN1IrqBank1 },
                new { Register = Registers.MCUPriorityN1InterruptClear2, Type = IrqType.McuN1IrqBank2 },
                new { Register = Registers.MCUPriorityN1InterruptClear3, Type = IrqType.McuN1IrqBank3 },
            })
            {
                descriptor.Register.Define(this)
                    .WithFlags(0, PinsPerBank, writeCallback: (bitIdx, _, value) =>
                        {
                            if(!value)
                            {
                                return;
                            }

                            irqStatus[(int)descriptor.Type][bitIdx].Value = false;
                        })
                    .WithWriteCallback((_, __) => UpdateInterrupt(descriptor.Type));
            }

            foreach(var descriptor in new[]
            {
                new { Register = Registers.MCUPriorityN0InterruptSet0, Type = IrqType.McuN0IrqBank0 },
                new { Register = Registers.MCUPriorityN0InterruptSet1, Type = IrqType.McuN0IrqBank1 },
                new { Register = Registers.MCUPriorityN0InterruptSet2, Type = IrqType.McuN0IrqBank2 },
                new { Register = Registers.MCUPriorityN0InterruptSet3, Type = IrqType.McuN0IrqBank3 },

                new { Register = Registers.MCUPriorityN1InterruptSet0, Type = IrqType.McuN1IrqBank0 },
                new { Register = Registers.MCUPriorityN1InterruptSet1, Type = IrqType.McuN1IrqBank1 },
                new { Register = Registers.MCUPriorityN1InterruptSet2, Type = IrqType.McuN1IrqBank2 },
                new { Register = Registers.MCUPriorityN1InterruptSet3, Type = IrqType.McuN1IrqBank3 },
            })
            {
                descriptor.Register.Define(this)
                    .WithFlags(0, PinsPerBank, writeCallback: (bitIdx, _, value) =>
                        {
                            if(!value)
                            {
                                return;
                            }

                            irqStatus[(int)descriptor.Type][bitIdx].Value = true;
                        })
                    .WithWriteCallback((_, __) => UpdateInterrupt(descriptor.Type));
            }

            Registers.PadKey.Define(this)
                .WithValueField(0, 32, out padKey, name: "PADKEY")
                ;
        }

        private void EnableTristatePinOutputState(int pinIdx, bool enabled)
        {
            tristatePinOutputEnabled[pinIdx] = enabled;
            this.Log(LogLevel.Debug, "GPIO #{0} tri-state {1}", pinIdx, enabled ? "enabled" : "disabled");

            UpdateOutputPinState(pinIdx);
        }

        private void HandlePinStateChangeInterrupt(int pinIdx, bool risingEdge)
        {
            var mode = interruptMode[pinIdx].Value;
            if(mode == InterruptEnable.EnabledOnAnyTransition
                || (risingEdge && mode == InterruptEnable.EnabledOnRisingEdgeTransition)
                || (!risingEdge && mode == InterruptEnable.EnabledOnFallingEdgeTransition))
            {
                this.Log(LogLevel.Noisy, "Triggering IRQ #{0} on the {1} edge", pinIdx, risingEdge ? "rising" : "falling");
                TriggerInterrupt(pinIdx);
            }
        }

        private bool IsPinOutputEnabled(int pinIdx)
        {
            switch(ioMode[pinIdx].Value)
            {
                case IOMode.OutputDisabled:
                    return false;

                case IOMode.PushPullOutputMode:
                    return true;

                case IOMode.OpenDrainOutputMode:
                    return false;

                case IOMode.TristatePushPullOutputMode:
                    return tristatePinOutputEnabled[pinIdx];

                default:
                    throw new ArgumentException($"Unexpected IOMode: {ioMode[pinIdx].Value}");
            }
        }

        private void SetOutputPinValue(int pinIdx, bool state)
        {
            outputPinValues[pinIdx] = state;
            UpdateOutputPinState(pinIdx);
        }

        private void TriggerInterrupt(int pinIdx)
        {
            var irqBank = pinIdx / PinsPerBank;
            var banksPinOffset = pinIdx % PinsPerBank;

            TriggerInterruptInner(irqBank, banksPinOffset);
            TriggerInterruptInner(irqBank, banksPinOffset, n1Priority: true);
        }

        private void TriggerInterruptInner(int irqBank, int banksPinOffset, bool n1Priority = false)
        {
            var irqType = n1Priority ? irqBank + NumberOfBanks : irqBank;
            if(irqEnabled[irqType][banksPinOffset].Value)
            {
                irqStatus[irqType][banksPinOffset].Value = true;
                UpdateInterrupt((IrqType)irqType);
            }
        }

        private void UpdateInterrupt(IrqType irqType)
        {
            var flag = false;
            for(var banksPinOffset = 0; banksPinOffset < PinsPerBank; banksPinOffset++)
            {
                if(irqEnabled[(int)irqType][banksPinOffset].Value && irqStatus[(int)irqType][banksPinOffset].Value)
                {
                    flag = true;
                    break;
                }
            }

            this.Log(LogLevel.Debug, "{0} {1} interrupt", flag ? "Setting" : "Clearing", irqType);
            irq[(int)irqType].Set(flag);
        }

        private void UpdateOutputPinState(int pinIdx)
        {
            var newPinState = IsPinOutputEnabled(pinIdx) && outputPinValues[pinIdx];
            if(Connections[pinIdx].IsSet != newPinState)
            {
                Connections[pinIdx].Set(newPinState);
                this.Log(LogLevel.Debug, "{0} output pin #{1}", newPinState ? "Setting" : "Clearing", pinIdx);
            }
        }

        private IValueRegisterField padKey;

        private readonly IFlagRegisterField[] inputEnable = new IFlagRegisterField[NumberOfPins];
        private readonly IEnumRegisterField<InterruptEnable>[] interruptMode = new IEnumRegisterField<InterruptEnable>[NumberOfPins];
        private readonly IEnumRegisterField<IOMode>[] ioMode = new IEnumRegisterField<IOMode>[NumberOfPins];
        private readonly IFlagRegisterField[][] irqEnabled = new IFlagRegisterField[NumberOfExternalInterrupts][];
        private readonly IFlagRegisterField[][] irqStatus = new IFlagRegisterField[NumberOfExternalInterrupts][];
        private readonly IFlagRegisterField[] readZero = new IFlagRegisterField[NumberOfPins];

        private readonly GPIO[] irq = new [] { new GPIO(), new GPIO(), new GPIO(), new GPIO(), new GPIO(), new GPIO(), new GPIO(), new GPIO() };
        private readonly bool[] outputPinValues = new bool[NumberOfPins];
        private readonly bool[] tristatePinOutputEnabled = new bool[NumberOfPins];

        private const int FirstVirtualPinIndex = 105;
        private const int NumberOfBanks = NumberOfPins / PinsPerBank;
        private const int NumberOfExternalInterrupts = 8;
        private const int NumberOfPins = 128;
        private const uint PadKeyUnlockValue = 0x73;
        private const int PinsPerBank = 32;

        private enum ChipSelectConfiguration
        {
            IOM0CE0 = 0x0, // IOM 0 NCE 0 module
            IOM0CE1 = 0x1, // IOM 0 NCE 1 module
            IOM0CE2 = 0x2, // IOM 0 NCE 2 module
            IOM0CE3 = 0x3, // IOM 0 NCE 3 module
            IOM1CE0 = 0x4, // IOM 1 NCE 0 module
            IOM1CE1 = 0x5, // IOM 1 NCE 1 module
            IOM1CE2 = 0x6, // IOM 1 NCE 2 module
            IOM1CE3 = 0x7, // IOM 1 NCE 3 module
            IOM2CE0 = 0x8, // IOM 2 NCE 0 module
            IOM2CE1 = 0x9, // IOM 2 NCE 1 module
            IOM2CE2 = 0xA, // IOM 2 NCE 2 module
            IOM2CE3 = 0xB, // IOM 2 NCE 3 module
            IOM3CE0 = 0xC, // IOM 3 NCE 0 module
            IOM3CE1 = 0xD, // IOM 3 NCE 1 module
            IOM3CE2 = 0xE, // IOM 3 NCE 2 module
            IOM3CE3 = 0xF, // IOM 3 NCE 3 module
            IOM4CE0 = 0x10, // IOM 4 NCE 0 module
            IOM4CE1 = 0x11, // IOM 4 NCE 1 module
            IOM4CE2 = 0x12, // IOM 4 NCE 2 module
            IOM4CE3 = 0x13, // IOM 4 NCE 3 module
            IOM5CE0 = 0x14, // IOM 5 NCE 0 module
            IOM5CE1 = 0x15, // IOM 5 NCE 1 module
            IOM5CE2 = 0x16, // IOM 5 NCE 2 module
            IOM5CE3 = 0x17, // IOM 5 NCE 3 module
            IOM6CE0 = 0x18, // IOM 6 NCE 0 module
            IOM6CE1 = 0x19, // IOM 6 NCE 1 module
            IOM6CE2 = 0x1A, // IOM 6 NCE 2 module
            IOM6CE3 = 0x1B, // IOM 6 NCE 3 module
            IOM7CE0 = 0x1C, // IOM 7 NCE 0 module
            IOM7CE1 = 0x1D, // IOM 7 NCE 1 module
            IOM7CE2 = 0x1E, // IOM 7 NCE 2 module
            IOM7CE3 = 0x1F, // IOM 7 NCE 3 module
            MSPI0CEN0 = 0x20, // MSPI 0 NCE 0 module
            MSPI0CEN1 = 0x21, // MSPI 0 NCE 1 module
            MSPI1CEN0 = 0x22, // MSPI 1 NCE 0 module
            MSPI1CEN1 = 0x23, // MSPI 1 NCE 1 module
            MSPI2CEN0 = 0x24, // MSPI 2 NCE 0 module
            MSPI2CEN1 = 0x25, // MSPI 2 NCE 1 module
            DC_DPI_DE = 0x26, // DC DPI DE module
            DISP_CONT_CSX = 0x27, // DISP CONT CSX module
            DC_SPI_CS_N = 0x28, // DC SPI CS_N module
            DC_QSPI_CS_N = 0x29, // DC QSPI CS_N module
            DC_RESX = 0x2A, // DC module RESX
        }

        private enum DriveStrength
        {
            OutputDriver0_1x = 0x0, // 0.1x output driver selected
            OutputDriver0_5x = 0x1, // 0.5x output driver selected
        }

        private enum InterruptEnable
        {
            Disabled = 0x0, // Interrupts are disabled for this GPIO
            EnabledOnFallingEdgeTransition = 0x1, // Interrupts are enabled for falling edge transition on this GPIO
            EnabledOnRisingEdgeTransition = 0x2, // Interrupts are enabled for rising edge transitions on this GPIO
            EnabledOnAnyTransition = 0x3, // Interrupts are enabled for any edge transition on this GPIO
        }

        private enum IOMode
        {
            OutputDisabled = 0x0, // Output Disabled
            PushPullOutputMode = 0x1, // Output configured in push pull mode. Will drive 0 and 1 values on pin.
            OpenDrainOutputMode = 0x2, // Output configured in open drain mode. Will only drive pin low, tristate otherwise.
            TristatePushPullOutputMode = 0x3, // Output configured in Tristate-able push pull mode. Will drive 0, 1 of HiZ on pin.
        }

        private enum IrqType
        {
            McuN0IrqBank0,
            McuN0IrqBank1,
            McuN0IrqBank2,
            McuN0IrqBank3,
            McuN1IrqBank0,
            McuN1IrqBank1,
            McuN1IrqBank2,
            McuN1IrqBank3
        }

        private enum PolarityConfiguration
        {
            ActiveLow = 0x0, // Polarity is active low
            ActiveHigh = 0x1, // Polarity is active high
        }

        private enum PullUpDownConfiguration
        {
            None = 0x0, // No pullup or pulldown selected
            Pulldown50K = 0x1, // 50K Pulldown selected
            Pullup1_5K = 0x2, // 1.5K Pullup selected
            Pullup6K = 0x3, // 6K Pullup selected
            Pullup12K = 0x4, // 12K Pullup selected
            Pullup24K = 0x5, // 24K Pullup selected
            Pullup50K = 0x6, // 50K Pullup selected
            Pullup100K = 0x7, // 100K Pullup selected
        }

        private enum Registers : long
        {
            PinConfiguration0 = 0x000, // Configuration control for GPIO pin 0
            PinConfiguration1 = 0x004, // Configuration control for GPIO pin 1
            PinConfiguration2 = 0x008, // Configuration control for GPIO pin 2
            PinConfiguration3 = 0x00C, // Configuration control for GPIO pin 3
            PinConfiguration4 = 0x010, // Configuration control for GPIO pin 4
            PinConfiguration5 = 0x014, // Configuration control for GPIO pin 5
            PinConfiguration6 = 0x018, // Configuration control for GPIO pin 6
            PinConfiguration7 = 0x01C, // Configuration control for GPIO pin 7
            PinConfiguration8 = 0x020, // Configuration control for GPIO pin 8
            PinConfiguration9 = 0x024, // Configuration control for GPIO pin 9
            PinConfiguration10 = 0x028, // Configuration control for GPIO pin 10
            PinConfiguration11 = 0x02C, // Configuration control for GPIO pin 11
            PinConfiguration12 = 0x030, // Configuration control for GPIO pin 12
            PinConfiguration13 = 0x034, // Configuration control for GPIO pin 13
            PinConfiguration14 = 0x038, // Configuration control for GPIO pin 14
            PinConfiguration15 = 0x03C, // Configuration control for GPIO pin 15
            PinConfiguration16 = 0x040, // Configuration control for GPIO pin 16
            PinConfiguration17 = 0x044, // Configuration control for GPIO pin 17
            PinConfiguration18 = 0x048, // Configuration control for GPIO pin 18
            PinConfiguration19 = 0x04C, // Configuration control for GPIO pin 19
            PinConfiguration20 = 0x050, // Configuration control for GPIO pin 20
            PinConfiguration21 = 0x054, // Configuration control for GPIO pin 21
            PinConfiguration22 = 0x058, // Configuration control for GPIO pin 22
            PinConfiguration23 = 0x05C, // Configuration control for GPIO pin 23
            PinConfiguration24 = 0x060, // Configuration control for GPIO pin 24
            PinConfiguration25 = 0x064, // Configuration control for GPIO pin 25
            PinConfiguration26 = 0x068, // Configuration control for GPIO pin 26
            PinConfiguration27 = 0x06C, // Configuration control for GPIO pin 27
            PinConfiguration28 = 0x070, // Configuration control for GPIO pin 28
            PinConfiguration29 = 0x074, // Configuration control for GPIO pin 29
            PinConfiguration30 = 0x078, // Configuration control for GPIO pin 30
            PinConfiguration31 = 0x07C, // Configuration control for GPIO pin 31
            PinConfiguration32 = 0x080, // Configuration control for GPIO pin 32
            PinConfiguration33 = 0x084, // Configuration control for GPIO pin 33
            PinConfiguration34 = 0x088, // Configuration control for GPIO pin 34
            PinConfiguration35 = 0x08C, // Configuration control for GPIO pin 35
            PinConfiguration36 = 0x090, // Configuration control for GPIO pin 36
            PinConfiguration37 = 0x094, // Configuration control for GPIO pin 37
            PinConfiguration38 = 0x098, // Configuration control for GPIO pin 38
            PinConfiguration39 = 0x09C, // Configuration control for GPIO pin 39
            PinConfiguration40 = 0x0A0, // Configuration control for GPIO pin 40
            PinConfiguration41 = 0x0A4, // Configuration control for GPIO pin 41
            PinConfiguration42 = 0x0A8, // Configuration control for GPIO pin 42
            PinConfiguration43 = 0x0AC, // Configuration control for GPIO pin 43
            PinConfiguration44 = 0x0B0, // Configuration control for GPIO pin 44
            PinConfiguration45 = 0x0B4, // Configuration control for GPIO pin 45
            PinConfiguration46 = 0x0B8, // Configuration control for GPIO pin 46
            PinConfiguration47 = 0x0BC, // Configuration control for GPIO pin 47
            PinConfiguration48 = 0x0C0, // Configuration control for GPIO pin 48
            PinConfiguration49 = 0x0C4, // Configuration control for GPIO pin 49
            PinConfiguration50 = 0x0C8, // Configuration control for GPIO pin 50
            PinConfiguration51 = 0x0CC, // Configuration control for GPIO pin 51
            PinConfiguration52 = 0x0D0, // Configuration control for GPIO pin 52
            PinConfiguration53 = 0x0D4, // Configuration control for GPIO pin 53
            PinConfiguration54 = 0x0D8, // Configuration control for GPIO pin 54
            PinConfiguration55 = 0x0DC, // Configuration control for GPIO pin 55
            PinConfiguration56 = 0x0E0, // Configuration control for GPIO pin 56
            PinConfiguration57 = 0x0E4, // Configuration control for GPIO pin 57
            PinConfiguration58 = 0x0E8, // Configuration control for GPIO pin 58
            PinConfiguration59 = 0x0EC, // Configuration control for GPIO pin 59
            PinConfiguration60 = 0x0F0, // Configuration control for GPIO pin 60
            PinConfiguration61 = 0x0F4, // Configuration control for GPIO pin 61
            PinConfiguration62 = 0x0F8, // Configuration control for GPIO pin 62
            PinConfiguration63 = 0x0FC, // Configuration control for GPIO pin 63
            PinConfiguration64 = 0x100, // Configuration control for GPIO pin 64
            PinConfiguration65 = 0x104, // Configuration control for GPIO pin 65
            PinConfiguration66 = 0x108, // Configuration control for GPIO pin 66
            PinConfiguration67 = 0x10C, // Configuration control for GPIO pin 67
            PinConfiguration68 = 0x110, // Configuration control for GPIO pin 68
            PinConfiguration69 = 0x114, // Configuration control for GPIO pin 69
            PinConfiguration70 = 0x118, // Configuration control for GPIO pin 70
            PinConfiguration71 = 0x11C, // Configuration control for GPIO pin 71
            PinConfiguration72 = 0x120, // Configuration control for GPIO pin 72
            PinConfiguration73 = 0x124, // Configuration control for GPIO pin 73
            PinConfiguration74 = 0x128, // Configuration control for GPIO pin 74
            PinConfiguration75 = 0x12C, // Configuration control for GPIO pin 75
            PinConfiguration76 = 0x130, // Configuration control for GPIO pin 76
            PinConfiguration77 = 0x134, // Configuration control for GPIO pin 77
            PinConfiguration78 = 0x138, // Configuration control for GPIO pin 78
            PinConfiguration79 = 0x13C, // Configuration control for GPIO pin 79
            PinConfiguration80 = 0x140, // Configuration control for GPIO pin 80
            PinConfiguration81 = 0x144, // Configuration control for GPIO pin 81
            PinConfiguration82 = 0x148, // Configuration control for GPIO pin 82
            PinConfiguration83 = 0x14C, // Configuration control for GPIO pin 83
            PinConfiguration84 = 0x150, // Configuration control for GPIO pin 84
            PinConfiguration85 = 0x154, // Configuration control for GPIO pin 85
            PinConfiguration86 = 0x158, // Configuration control for GPIO pin 86
            PinConfiguration87 = 0x15C, // Configuration control for GPIO pin 87
            PinConfiguration88 = 0x160, // Configuration control for GPIO pin 88
            PinConfiguration89 = 0x164, // Configuration control for GPIO pin 89
            PinConfiguration90 = 0x168, // Configuration control for GPIO pin 90
            PinConfiguration91 = 0x16C, // Configuration control for GPIO pin 91
            PinConfiguration92 = 0x170, // Configuration control for GPIO pin 92
            PinConfiguration93 = 0x174, // Configuration control for GPIO pin 93
            PinConfiguration94 = 0x178, // Configuration control for GPIO pin 94
            PinConfiguration95 = 0x17C, // Configuration control for GPIO pin 95
            PinConfiguration96 = 0x180, // Configuration control for GPIO pin 96
            PinConfiguration97 = 0x184, // Configuration control for GPIO pin 97
            PinConfiguration98 = 0x188, // Configuration control for GPIO pin 98
            PinConfiguration99 = 0x18C, // Configuration control for GPIO pin 99
            PinConfiguration100 = 0x190, // Configuration control for GPIO pin 100
            PinConfiguration101 = 0x194, // Configuration control for GPIO pin 101
            PinConfiguration102 = 0x198, // Configuration control for GPIO pin 102
            PinConfiguration103 = 0x19C, // Configuration control for GPIO pin 103
            PinConfiguration104 = 0x1A0, // Configuration control for GPIO pin 104
            PinConfiguration105 = 0x1A4, // Configuration control for Virtual GPIO pin 105
            PinConfiguration106 = 0x1A8, // Configuration control for Virtual GPIO pin 106
            PinConfiguration107 = 0x1AC, // Configuration control for Virtual GPIO pin 107
            PinConfiguration108 = 0x1B0, // Configuration control for Virtual GPIO pin 108
            PinConfiguration109 = 0x1B4, // Configuration control for Virtual GPIO pin 109
            PinConfiguration110 = 0x1B8, // Configuration control for Virtual GPIO pin 110
            PinConfiguration111 = 0x1BC, // Configuration control for Virtual GPIO pin 111
            PinConfiguration112 = 0x1C0, // Configuration control for Virtual GPIO pin 112
            PinConfiguration113 = 0x1C4, // Configuration control for Virtual GPIO pin 113
            PinConfiguration114 = 0x1C8, // Configuration control for Virtual GPIO pin 114
            PinConfiguration115 = 0x1CC, // Configuration control for Virtual GPIO pin 115
            PinConfiguration116 = 0x1D0, // Configuration control for Virtual GPIO pin 116
            PinConfiguration117 = 0x1D4, // Configuration control for Virtual GPIO pin 117
            PinConfiguration118 = 0x1D8, // Configuration control for Virtual GPIO pin 118
            PinConfiguration119 = 0x1DC, // Configuration control for Virtual GPIO pin 119
            PinConfiguration120 = 0x1E0, // Configuration control for Virtual GPIO pin 120
            PinConfiguration121 = 0x1E4, // Configuration control for Virtual GPIO pin 121
            PinConfiguration122 = 0x1E8, // Configuration control for Virtual GPIO pin 122
            PinConfiguration123 = 0x1EC, // Configuration control for Virtual GPIO pin 123
            PinConfiguration124 = 0x1F0, // Configuration control for Virtual GPIO pin 124
            PinConfiguration125 = 0x1F4, // Configuration control for Virtual GPIO pin 125
            PinConfiguration126 = 0x1F8, // Configuration control for Virtual GPIO pin 126
            PinConfiguration127 = 0x1FC, // Configuration control for Virtual GPIO pin 127
            PadKey = 0x200, // Key Register for all pad configuration registers
            InputRead0 = 0x204, // GPIO Input 0 (31-0)
            InputRead1 = 0x208, // GPIO Input 1 (63-32)
            InputRead2 = 0x20C, // GPIO Input 2 (95-64)
            InputRead3 = 0x210, // GPIO Input 3 (127-96)
            OutputWrite0 = 0x214, // GPIO Output 0 (31-0)
            OutputWrite1 = 0x218, // GPIO Output 1 (63-32)
            OutputWrite2 = 0x21C, // GPIO Output 2 (95-64)
            OutputWrite3 = 0x220, // GPIO Output 3 (127-96)
            OutputSet0 = 0x224, // GPIO Output Set 0 (31-0)
            OutputSet1 = 0x228, // GPIO Output Set 1 (63-32)
            OutputSet2 = 0x22C, // GPIO Output Set 2 (95-64)
            OutputSet3 = 0x230, // GPIO Output Set 3 (127-96)
            OutputClear0 = 0x234, // GPIO Output Clear 0 (31-0)
            OutputClear1 = 0x238, // GPIO Output Clear 1 (63-32)
            OutputClear2 = 0x23C, // GPIO Output Clear 2 (95-64)
            OutputClear3 = 0x240, // GPIO Output Clear 3 (127-96)
            GPIOOutputEnable0 = 0x244, // GPIO Enable 0 (31-0)
            GPIOOutputEnable1 = 0x248, // GPIO Enable 1 (63-32)
            GPIOOutputEnable2 = 0x24C, // GPIO Enable 2 (95-64)
            GPIOOutputEnable3 = 0x250, // GPIO Enable 3 (127-96)
            GPIOOutputEnableSet0 = 0x254, // GPIO Enable Set 0 (31-0)
            GPIOOutputEnableSet1 = 0x258, // GPIO Enable Set 1 (63-32)
            GPIOOutputEnableSet2 = 0x25C, // GPIO Enable Set 2 (95-64)
            GPIOOutputEnableSet3 = 0x260, // GPIO Enable Set 3 (127-96)
            GPIOOutputEnableClear0 = 0x264, // GPIO Enable Clear 0 (31-0)
            GPIOOutputEnableClear1 = 0x268, // GPIO Enable Clear 1 (63-32)
            GPIOOutputEnableClear2 = 0x26C, // GPIO Enable Clear 2 (95-64)
            GPIOOutputEnableClear3 = 0x270, // GPIO Enable Clear 3 (127-96)
            IOM0FlowControlIRQSelect = 0x274, // IOM0 Flow Control IRQ Select
            IOM1FlowControlIRQSelect = 0x278, // IOM1 Flow Control IRQ Select
            IOM2FlowControlIRQSelect = 0x27C, // IOM2 Flow Control IRQ Select
            IOM3FlowControlIRQSelect = 0x280, // IOM3 Flow Control IRQ Select
            IOM4FlowControlIRQSelect = 0x284, // IOM4 Flow Control IRQ Select
            IOM5FlowControlIRQSelect = 0x288, // IOM5 Flow Control IRQ Select
            IOM6FlowControlIRQSelect = 0x28C, // IOM6 Flow Control IRQ Select
            IOM7FlowControlIRQSelect = 0x290, // IOM7 Flow Control IRQ Select
            SDIFCDWPPadSelect = 0x294, // SDIF CD and WP Select
            ObservationModeSample = 0x298, // GPIO Observation Mode Sample
            InputEnableSignals0 = 0x29C, // Read only. Reflects the value of the input enable signals for pads 31-0 sent to the pad.
            InputEnableSignals1 = 0x2A0, // Read only. Reflects the value of the input enable signals for pads 63-32 sent to the pad.
            InputEnableSignals2 = 0x2A4, // Read only. Reflects the value of the input enable signals for pads 95-64 sent to the pad.
            InputEnableSignals3 = 0x2A8, // Read only. Reflects the value of the input enable signals for pads 127-96 sent to the pad.
            OutputEnableSignals0 = 0x2AC, // Read only. Reflects the value of the output enable signals for pads 31-0 sent to the pad.
            OutputEnableSignals1 = 0x2B0, // Read only. Reflects the value of the output enable signals for pads 63-32 sent to the pad.
            OutputEnableSignals2 = 0x2B4, // Read only. Reflects the value of the output enable signals for pads 95-64 sent to the pad.
            OutputEnableSignals3 = 0x2B8, // Read only. Reflects the value of the output enable signals for pads 127-96 sent to the pad.
            MCUPriorityN0InterruptEnable0 = 0x2C0, // GPIO MCU Interrupts N0 31-0: Enable
            MCUPriorityN0InterruptStatus0 = 0x2C4, // GPIO MCU Interrupts N0 31-0: Status
            MCUPriorityN0InterruptClear0 = 0x2C8, // GPIO MCU Interrupts N0 31-0: Clear
            MCUPriorityN0InterruptSet0 = 0x2CC, // GPIO MCU Interrupts N0 31-0: Set
            MCUPriorityN0InterruptEnable1 = 0x2D0, // GPIO MCU Interrupts N0 63-32: Enable
            MCUPriorityN0InterruptStatus1 = 0x2D4, // GPIO MCU Interrupts N0 63-32: Status
            MCUPriorityN0InterruptClear1 = 0x2D8, // GPIO MCU Interrupts N0 63-32: Clear
            MCUPriorityN0InterruptSet1 = 0x2DC, // GPIO MCU Interrupts N0 63-32: Set
            MCUPriorityN0InterruptEnable2 = 0x2E0, // GPIO MCU Interrupts N0 95-64: Enable
            MCUPriorityN0InterruptStatus2 = 0x2E4, // GPIO MCU Interrupts N0 95-64: Status
            MCUPriorityN0InterruptClear2 = 0x2E8, // GPIO MCU Interrupts N0 95-64: Clear
            MCUPriorityN0InterruptSet2 = 0x2EC, // GPIO MCU Interrupts N0 95-64: Set
            MCUPriorityN0InterruptEnable3 = 0x2F0, // GPIO MCU Interrupts N0 127-96: Enable
            MCUPriorityN0InterruptStatus3 = 0x2F4, // GPIO MCU Interrupts N0 127-96: Status
            MCUPriorityN0InterruptClear3 = 0x2F8, // GPIO MCU Interrupts N0 127-96: Clear
            MCUPriorityN0InterruptSet3 = 0x2FC, // GPIO MCU Interrupts N0 127-96: Set
            MCUPriorityN1InterruptEnable0 = 0x300, // GPIO MCU Interrupts N1 31-0: Enable
            MCUPriorityN1InterruptStatus0 = 0x304, // GPIO MCU Interrupts N1 31-0: Status
            MCUPriorityN1InterruptClear0 = 0x308, // GPIO MCU Interrupts N1 31-0: Clear
            MCUPriorityN1InterruptSet0 = 0x30C, // GPIO MCU Interrupts N1 31-0: Set
            MCUPriorityN1InterruptEnable1 = 0x310, // GPIO MCU Interrupts N1 63-32: Enable
            MCUPriorityN1InterruptStatus1 = 0x314, // GPIO MCU Interrupts N1 63-32: Status
            MCUPriorityN1InterruptClear1 = 0x318, // GPIO MCU Interrupts N1 63-32: Clear
            MCUPriorityN1InterruptSet1 = 0x31C, // GPIO MCU Interrupts N1 63-32: Set
            MCUPriorityN1InterruptEnable2 = 0x320, // GPIO MCU Interrupts N1 95-64: Enable
            MCUPriorityN1InterruptStatus2 = 0x324, // GPIO MCU Interrupts N1 95-64: Status
            MCUPriorityN1InterruptClear2 = 0x328, // GPIO MCU Interrupts N1 95-64: Clear
            MCUPriorityN1InterruptSet2 = 0x32C, // GPIO MCU Interrupts N1 95-64: Set
            MCUPriorityN1InterruptEnable3 = 0x330, // GPIO MCU Interrupts N1 127-96: Enable
            MCUPriorityN1InterruptStatus3 = 0x334, // GPIO MCU Interrupts N1 127-96: Status
            MCUPriorityN1InterruptClear3 = 0x338, // GPIO MCU Interrupts N1 127-96: Clear
            MCUPriorityN1InterruptSet3 = 0x33C, // GPIO MCU Interrupts N1 127-96: Set
            DSP0PriorityN0InterruptEnable0 = 0x340, // GPIO DSP0 Interrupts N0 31-0: Enable
            DSP0PriorityN0InterruptStatus0 = 0x344, // GPIO DSP0 Interrupts N0 31-0: Status
            DSP0PriorityN0InterruptClear0 = 0x348, // GPIO DSP0 Interrupts N0 31-0: Clear
            DSP0PriorityN0InterruptSet0 = 0x34C, // GPIO DSP0 Interrupts N0 31-0: Set
            DSP0PriorityN0InterruptEnable1 = 0x350, // GPIO DSP0 Interrupts N0 63-32: Enable
            DSP0PriorityN0InterruptStatus1 = 0x354, // GPIO DSP0 Interrupts N0 63-32: Status
            DSP0PriorityN0InterruptClear1 = 0x358, // GPIO DSP0 Interrupts N0 63-32: Clear
            DSP0PriorityN0InterruptSet1 = 0x35C, // GPIO DSP0 Interrupts N0 63-32: Set
            DSP0PriorityN0InterruptEnable2 = 0x360, // GPIO DSP0 Interrupts N0 95-64: Enable
            DSP0PriorityN0InterruptStatus2 = 0x364, // GPIO DSP0 Interrupts N0 95-64: Status
            DSP0PriorityN0InterruptClear2 = 0x368, // GPIO DSP0 Interrupts N0 95-64: Clear
            DSP0PriorityN0InterruptSet2 = 0x36C, // GPIO DSP0 Interrupts N0 95-64: Set
            DSP0PriorityN0InterruptEnable3 = 0x370, // GPIO DSP0 Interrupts N0 127-96: Enable
            DSP0PriorityN0InterruptStatus3 = 0x374, // GPIO DSP0 Interrupts N0 127-96: Status
            DSP0PriorityN0InterruptClear3 = 0x378, // GPIO DSP0 Interrupts N0 127-96: Clear
            DSP0PriorityN0InterruptSet3 = 0x37C, // GPIO DSP0 Interrupts N0 127-96: Set
            DSP0PriorityN1InterruptEnable0 = 0x380, // GPIO DSP0 Interrupts N1 31-0: Enable
            DSP0PriorityN1InterruptStatus0 = 0x384, // GPIO DSP0 Interrupts N1 31-0: Status
            DSP0PriorityN1InterruptClear0 = 0x388, // GPIO DSP0 Interrupts N1 31-0: Clear
            DSP0PriorityN1InterruptSet0 = 0x38C, // GPIO DSP0 Interrupts N1 31-0: Set
            DSP0PriorityN1InterruptEnable1 = 0x390, // GPIO DSP0 Interrupts N1 63-32: Enable
            DSP0PriorityN1InterruptStatus1 = 0x394, // GPIO DSP0 Interrupts N1 63-32: Status
            DSP0PriorityN1InterruptClear1 = 0x398, // GPIO DSP0 Interrupts N1 63-32: Clear
            DSP0PriorityN1InterruptSet1 = 0x39C, // GPIO DSP0 Interrupts N1 63-32: Set
            DSP0PriorityN1InterruptEnable2 = 0x3A0, // GPIO DSP0 Interrupts N1 95-64: Enable
            DSP0PriorityN1InterruptStatus2 = 0x3A4, // GPIO DSP0 Interrupts N1 95-64: Status
            DSP0PriorityN1InterruptClear2 = 0x3A8, // GPIO DSP0 Interrupts N1 95-64: Clear
            DSP0PriorityN1InterruptSet2 = 0x3AC, // GPIO DSP0 Interrupts N1 95-64: Set
            DSP0PriorityN1InterruptEnable3 = 0x3B0, // GPIO DSP0 Interrupts N1 127-96: Enable
            DSP0PriorityN1InterruptStatus3 = 0x3B4, // GPIO DSP0 Interrupts N1 127-96: Status
            DSP0PriorityN1InterruptClear3 = 0x3B8, // GPIO DSP0 Interrupts N1 127-96: Clear
            DSP0PriorityN1InterruptSet3 = 0x3BC, // GPIO DSP0 Interrupts N1 127-96: Set
            DSP1PriorityN0InterruptEnable0 = 0x3C0, // GPIO DSP1 Interrupts N0 31-0: Enable
            DSP1PriorityN0InterruptStatus0 = 0x3C4, // GPIO DSP1 Interrupts N0 31-0: Status
            DSP1PriorityN0InterruptClear0 = 0x3C8, // GPIO DSP1 Interrupts N0 31-0: Clear
            DSP1PriorityN0InterruptSet0 = 0x3CC, // GPIO DSP1 Interrupts N0 31-0: Set
            DSP1PriorityN0InterruptEnable1 = 0x3D0, // GPIO DSP1 Interrupts N0 63-32: Enable
            DSP1PriorityN0InterruptStatus1 = 0x3D4, // GPIO DSP1 Interrupts N0 63-32: Status
            DSP1PriorityN0InterruptClear1 = 0x3D8, // GPIO DSP1 Interrupts N0 63-32: Clear
            DSP1PriorityN0InterruptSet1 = 0x3DC, // GPIO DSP1 Interrupts N0 63-32: Set
            DSP1PriorityN0InterruptEnable2 = 0x3E0, // GPIO DSP1 Interrupts N0 95-64: Enable
            DSP1PriorityN0InterruptStatus2 = 0x3E4, // GPIO DSP1 Interrupts N0 95-64: Status
            DSP1PriorityN0InterruptClear2 = 0x3E8, // GPIO DSP1 Interrupts N0 95-64: Clear
            DSP1PriorityN0InterruptSet2 = 0x3EC, // GPIO DSP1 Interrupts N0 95-64: Set
            DSP1PriorityN0InterruptEnable3 = 0x3F0, // GPIO DSP1 Interrupts N0 127-96: Enable
            DSP1PriorityN0InterruptStatus3 = 0x3F4, // GPIO DSP1 Interrupts N0 127-96: Status
            DSP1PriorityN0InterruptClear3 = 0x3F8, // GPIO DSP1 Interrupts N0 127-96: Clear
            DSP1PriorityN0InterruptSet3 = 0x3FC, // GPIO DSP1 Interrupts N0 127-96: Set
            DSP1PriorityN1InterruptEnable0 = 0x400, // GPIO DSP1 Interrupts N1 31-0: Enable
            DSP1PriorityN1InterruptStatus0 = 0x404, // GPIO DSP1 Interrupts N1 31-0: Status
            DSP1PriorityN1InterruptClear0 = 0x408, // GPIO DSP1 Interrupts N1 31-0: Clear
            DSP1PriorityN1InterruptSet0 = 0x40C, // GPIO DSP1 Interrupts N1 31-0: Set
            DSP1PriorityN1InterruptEnable1 = 0x410, // GPIO DSP1 Interrupts N1 63-32: Enable
            DSP1PriorityN1InterruptStatus1 = 0x414, // GPIO DSP1 Interrupts N1 63-32: Status
            DSP1PriorityN1InterruptClear1 = 0x418, // GPIO DSP1 Interrupts N1 63-32: Clear
            DSP1PriorityN1InterruptSet1 = 0x41C, // GPIO DSP1 Interrupts N1 63-32: Set
            DSP1PriorityN1InterruptEnable2 = 0x420, // GPIO DSP1 Interrupts N1 95-64: Enable
            DSP1PriorityN1InterruptStatus2 = 0x424, // GPIO DSP1 Interrupts N1 95-64: Status
            DSP1PriorityN1InterruptClear2 = 0x428, // GPIO DSP1 Interrupts N1 95-64: Clear
            DSP1PriorityN1InterruptSet2 = 0x42C, // GPIO DSP1 Interrupts N1 95-64: Set
            DSP1PriorityN1InterruptEnable3 = 0x430, // GPIO DSP1 Interrupts N1 127-96: Enable
            DSP1PriorityN1InterruptStatus3 = 0x434, // GPIO DSP1 Interrupts N1 127-96: Status
            DSP1PriorityN1InterruptClear3 = 0x438, // GPIO DSP1 Interrupts N1 127-96: Clear
            DSP1PriorityN1InterruptSet3 = 0x43C, // GPIO DSP1 Interrupts N1 127-96: Set
        }
    }
}
