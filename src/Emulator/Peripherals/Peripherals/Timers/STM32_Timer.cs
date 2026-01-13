//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    // This class does not implement advanced-control timers interrupts
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32_Timer : LimitTimer, IDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput, IRegisterablePeripheral<IGPIOReceiver, NumberRegistrationPoint<int>>, IRegisterablePeripheral<IGPIOReceiver, NullRegistrationPoint>, IGPIOReceiver
    {
        public STM32_Timer(IMachine machine, ulong frequency, uint initialLimit) : base(machine.ClockSource, frequency, limit: initialLimit, direction: Direction.Ascending, enabled: false, eventEnabled: true, autoUpdate: false)
        {
            this.machine = machine;
            sysbus = machine.GetSystemBus(this);
            this.initialLimit = initialLimit;
            // If initialLimit is 0, throw an error - this is an invalid state for us, since we would not be able to infer the counter's width
            if(initialLimit == 0)
            {
                throw new ConstructionException($"{nameof(initialLimit)} has to be greater than zero");
            }
            // We need to ensure that the counter is at least as wide as the position of MSB in initialLimit
            // but since we count from 0 (log_2 (1) = 0 ) - add 1
            this.timerCounterLengthInBits = (int)Math.Floor(Math.Log(initialLimit, 2)) + 1;
            if(this.timerCounterLengthInBits > 32)
            {
                throw new ConstructionException($"Timer's width cannot be more than 32 bits - requested {this.timerCounterLengthInBits} bits (inferred from {nameof(initialLimit)})");
            }

            for(var i = 0; i < NumberOfCCChannels; ++i)
            {
                channels[i] = new CaptureCompareChannel(this, i);
            }
            connections = Enumerable.Range(0, NumberOfCCChannels).ToDictionary(i => i, i => (IGPIO)channels[i].Connection);

            LimitReached += delegate
            {
                if(updateDisable.Value)
                {
                    return;
                }

                if(Mode == WorkMode.OneShot)
                {
                    enableRequested = false;
                }

                Limit = autoReloadValue;
                this.Log(LogLevel.Noisy, "IRQ pending");
                updateInterruptFlag = true;

                for(var i = 0; i < NumberOfCCChannels; ++i)
                {
                    var channel = channels[i];
                    channel.UpdateTimer();
                    if(!channel.Timer.Enabled || !channel.IsOutputMode)
                    {
                        continue;
                    }

                    switch(channel.CompareMode.Value)
                    {
                    case OutputCompareMode.PwmMode1:
                        channel.Connection.Set();
                        break;
                    case OutputCompareMode.PwmMode2:
                        channel.Connection.Unset();
                        break;
                    }
                }

                if(updateInterruptEnable.Value && repetitionsLeft == 0)
                {
                    // 2 of central-aligned modes should raise IRQ only on overflow/underflow, hence it happens 2 times less often
                    var centerAlignedUnbalancedMode = (centerAlignedMode.Value == CenterAlignedMode.CenterAligned1) || (centerAlignedMode.Value == CenterAlignedMode.CenterAligned2);
                    repetitionsLeft = 1u + (uint)repetitionCounter.Value * (centerAlignedUnbalancedMode ? 2u : 1u);
                    UpdateInterrupts();
                }

                if(repetitionsLeft > 0)
                {
                    repetitionsLeft--;
                }
            };

            for(var i = 0; i < NumberOfCCChannels; ++i)
            {
                var j = i;
                var channel = channels[j];
                channel.Timer = new LimitTimer(machine.ClockSource, frequency, this, String.Format("cctimer{0}", j + 1), limit: initialLimit, eventEnabled: true, direction: Direction.Ascending, enabled: false, autoUpdate: false, workMode: WorkMode.OneShot);
                channel.Timer.LimitReached += delegate
                {
                    if(!channel.IsOutputMode)
                    {
                        return;
                    }

                    switch(channel.CompareMode.Value)
                    {
                    case OutputCompareMode.SetActiveOnMatch:
                        channel.Connection.Blink(); // high pulse
                        break;
                    case OutputCompareMode.SetInactiveOnMatch:
                        channel.Connection.Unset();
                        channel.Connection.Set(); // low pulse
                        break;
                    case OutputCompareMode.ToggleOnMatch:
                        channel.Connection.Toggle();
                        break;
                    case OutputCompareMode.PwmMode1:
                        channel.Connection.Unset();
                        break;
                    case OutputCompareMode.PwmMode2:
                        channel.Connection.Set();
                        break;
                    }

                    if(channel.InterruptEnable)
                    {
                        channel.InterruptFlag = true;
                        this.Log(LogLevel.Noisy, "cctimer{0}: Compare IRQ pending", j + 1);
                        UpdateInterrupts();
                    }
                };
            }

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control1, new DoubleWordRegister(this)
                    .WithFlag(0, writeCallback: (_, val) =>
                    {
                        enableRequested = val;
                        if(IsTriggerMode || IsEncoderMode)
                        {
                            return;
                        }
                        Enabled = enableRequested && autoReloadValue > 0;
                    }, valueProviderCallback: _ => enableRequested, name: "Counter enable (CEN)")
                    .WithFlag(1, out updateDisable, name: "Update disable (UDIS)")
                    .WithFlag(2, out updateRequestSource, name: "Update request source (URS)")
                    .WithFlag(3, writeCallback: (_, val) => Mode = val ? WorkMode.OneShot : WorkMode.Periodic, valueProviderCallback: _ => Mode == WorkMode.OneShot, name: "One-pulse mode (OPM)")
                    .WithFlag(4, writeCallback: (_, val) => {
                        if(IsEncoderMode || centerAlignedMode.Value != CenterAlignedMode.EdgeAligned)
                        {
                            return;
                        }
                        Direction = val ? Direction.Descending : Direction.Ascending;
                    }, valueProviderCallback: _ => Direction == Direction.Descending, name: "Direction (DIR)")
                    .WithEnumField(5, 2, out centerAlignedMode, name: "Center-aligned mode selection (CMS)")
                    .WithFlag(7, out autoReloadPreloadEnable, name: "Auto-reload preload enable (APRE)")
                    .WithTag("Clock Division (CKD)", 8, 2)
                    .WithReservedBits(10, 22)
                    .WithWriteCallback((_, __) => { UpdateCaptureCompareTimers(); UpdateInterrupts(); })
                },
                {(long)Registers.Control2, new DoubleWordRegister(this)
                    .WithTaggedFlag("Capture/compare preloaded control (CCPC)", 0)
                    .WithReservedBits(1, 1)
                    .WithTaggedFlag("Capture/compare control update selection (CCUS)", 2)
                    .WithTaggedFlag("Capture/compare DMA selection (CCDS)", 3)
                    .WithTag("Master mode selection (MMS)", 4, 2)
                    .WithFlag(7, out timerInput1Selection, name: "TI1 selection (TI1S)")
                    .WithTaggedFlag("Output Idle state 1 (OC1 output) (OIS1)", 8)
                    .WithTaggedFlag("Output Idle state 1 (OC1N output) (OIS1N)", 9)
                    .WithTaggedFlag("Output Idle state 2 (OC2 output) (OIS2)", 10)
                    .WithTaggedFlag("Output Idle state 2 (OC2N output) (OIS2N)", 11)
                    .WithTaggedFlag("Output Idle state 3 (OC3 output) (OIS3)", 12)
                    .WithTaggedFlag("Output Idle state 3 (OC3N output) (OIS3N)", 13)
                    .WithTaggedFlag("Output Idle state 4 (OC4 output) (OIS4)", 14)
                    .WithReservedBits(15, 17)
                },
                {(long)Registers.SlaveModeControl, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, SlaveModeSelection>(0, 3, out slaveModeSelection, writeCallback: (_, val) => {
                        if(val == SlaveModeSelection.ExternalClockMode1)
                        {
                            this.Log(LogLevel.Warning, "External Clock mode 1 is not supported");
                            return;
                        }

                        if(IsTriggerMode || IsEncoderMode)
                        {
                            Enabled = false;
                            if(sysbus.TryGetCurrentCPU(out var cpu))
                            {
                                cpu.SyncTime();
                            }
                        }
                        else
                        {
                            Enabled = enableRequested && autoReloadValue > 0;
                        }
                    }, name: "Slave mode selection (SMS)")
                    .WithTaggedFlag("OCREF clear selection (OCCS)", 3)
                    .WithEnumField(4, 3, out triggerSelection, name: "Trigger Selection (TS)")
                    .WithTaggedFlag("Master/Slave mode (MSM)", 7)
                    .WithTag("External trigger filter (ETF)", 8, 3)
                    .WithTag("External trigger prescaler (ETPS)", 12, 2)
                    .WithTaggedFlag("External clock enable (ECE)", 14)
                    .WithTaggedFlag("External trigger polarity (ETP)", 15)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.DmaOrInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out updateInterruptEnable, name: "Update interrupt enable (UIE)")
                    .WithFlag(1, valueProviderCallback: _ => channels[0].InterruptEnable, writeCallback: (_, val) => WriteCaptureCompareInterruptEnable(0, val), name: "Capture/Compare 1 interrupt enable (CC1IE)")
                    .WithFlag(2, valueProviderCallback: _ => channels[1].InterruptEnable, writeCallback: (_, val) => WriteCaptureCompareInterruptEnable(1, val), name: "Capture/Compare 2 interrupt enable (CC2IE)")
                    .WithFlag(3, valueProviderCallback: _ => channels[2].InterruptEnable, writeCallback: (_, val) => WriteCaptureCompareInterruptEnable(2, val), name: "Capture/Compare 3 interrupt enable (CC3IE)")
                    .WithFlag(4, valueProviderCallback: _ => channels[3].InterruptEnable, writeCallback: (_, val) => WriteCaptureCompareInterruptEnable(3, val), name: "Capture/Compare 4 interrupt enable (CC4IE)")
                    .WithReservedBits(5, 1)
                    .WithFlag(6, out triggerInterruptEnable,  name: "Trigger interrupt enable (TIE)")
                    .WithReservedBits(7, 1)
                    .WithTag("Update DMA request enable (UDE)", 8, 1)
                    .WithTag("Capture/Compare 1 DMA request enable (CC1DE)", 9, 1)
                    .WithTag("Capture/Compare 2 DMA request enable (CC2DE)", 10, 1)
                    .WithTag("Capture/Compare 3 DMA request enable (CC3DE)", 11, 1)
                    .WithTag("Capture/Compare 4 DMA request enable (CC4DE)", 12, 1)
                    .WithReservedBits(13, 1)
                    .WithTag("Trigger DMA request enable (TDE)", 14, 1)
                    .WithReservedBits(15, 17)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read | FieldMode.WriteZeroToClear,
                        writeCallback: (_, val) =>
                        {
                            if(!val)
                            {
                                updateInterruptFlag = false;
                                this.Log(LogLevel.Noisy, "IRQ claimed");
                            }
                        },
                        valueProviderCallback: (_) =>
                        {
                            return updateInterruptFlag;
                        },
                        name: "Update interrupt flag (UIF)")
                    .WithFlag(1, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, val) => ClaimCaptureCompareInterrupt(0, val), valueProviderCallback: _ => channels[0].InterruptFlag, name: "Capture/Compare 1 interrupt flag (CC1IF)")
                    .WithFlag(2, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, val) => ClaimCaptureCompareInterrupt(1, val), valueProviderCallback: _ => channels[1].InterruptFlag, name: "Capture/Compare 2 interrupt flag (CC2IF)")
                    .WithFlag(3, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, val) => ClaimCaptureCompareInterrupt(2, val), valueProviderCallback: _ => channels[2].InterruptFlag, name: "Capture/Compare 3 interrupt flag (CC3IF)")
                    .WithFlag(4, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, val) => ClaimCaptureCompareInterrupt(3, val), valueProviderCallback: _ => channels[3].InterruptFlag, name: "Capture/Compare 4 interrupt flag (CC4IF)")
                    .WithTaggedFlag("COM interrupt flag (COMIF)", 5)
                    .WithFlag(6, out triggerInterruptFlag, FieldMode.Read | FieldMode.WriteZeroToClear, name: "Trigger interrupt flag (TIF)")
                    .WithTaggedFlag("Break interrupt flag (BIF)", 7)
                    .WithTaggedFlag("Break 2 interrupt flag (B2IF)", 8)
                    .WithFlag(9, out channels[0].OvercaptureFlag, FieldMode.Read | FieldMode.WriteZeroToClear, name: "Capture/Compare 1 overcapture flag (CC1OF)")
                    .WithFlag(10, out channels[1].OvercaptureFlag, FieldMode.Read | FieldMode.WriteZeroToClear, name: "Capture/Compare 2 overcapture flag (CC2OF)")
                    .WithFlag(11, out channels[2].OvercaptureFlag, FieldMode.Read | FieldMode.WriteZeroToClear, name: "Capture/Compare 3 overcapture flag (CC3OF)")
                    .WithFlag(12, out channels[3].OvercaptureFlag, FieldMode.Read | FieldMode.WriteZeroToClear, name: "Capture/Compare 4 overcapture flag (CC4OF)")
                    .WithTaggedFlag("System Break interrupt flag (SBIF)", 13)
                    .WithReservedBits(14, 18)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.EventGeneration, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.WriteOneToClear, writeCallback: (_, val) =>
                    {
                        if(updateDisable.Value)
                        {
                            return;
                        }
                        if(Direction == Direction.Ascending)
                        {
                            Value = 0;
                        }
                        else if(Direction == Direction.Descending)
                        {
                            Value = autoReloadValue;
                        }

                        repetitionsLeft = (uint)repetitionCounter.Value;

                        if(!updateRequestSource.Value && updateInterruptEnable.Value)
                        {
                            this.Log(LogLevel.Noisy, "IRQ pending");
                            updateInterruptFlag = true;
                        }
                        for(var i = 0; i < NumberOfCCChannels; ++i)
                        {
                            if(channels[i].Timer.Enabled)
                            {
                                channels[i].Timer.Value = Value;
                            }
                        }
                    }, name: "Update generation (UG)")
                    .WithTag("Capture/compare 1 generation (CC1G)", 1, 1)
                    .WithTag("Capture/compare 2 generation (CC2G)", 2, 1)
                    .WithTag("Capture/compare 3 generation (CC3G)", 3, 1)
                    .WithTag("Capture/compare 4 generation (CC4G)", 4, 1)
                    .WithTaggedFlag("Capture/compare update generation (COMG)", 5)
                    .WithTag("Trigger generation (TG)", 6, 1)
                    .WithReservedBits(7, 25)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.CaptureOrCompareEnable, new DoubleWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => channels[0].OutputEnable, writeCallback: (_, val) => WriteCaptureCompareOutputEnable(0, val), name: "Capture/Compare 1 enable (CC1E)")
                    .WithFlag(1, out channels[0].Polarity, name: "Capture/Compare 1 output polarity (CC1P)")
                    .WithTaggedFlag("Capture/Compare 1 complementary output enable (CC1NE)", 2)
                    .WithFlag(3, out channels[0].ComplementaryPolarity, name: "Capture/Compare 1 complementary output polarity (CC1NP)")
                    .WithFlag(4, valueProviderCallback: _ => channels[1].OutputEnable, writeCallback: (_, val) => WriteCaptureCompareOutputEnable(1, val), name: "Capture/Compare 2 enable (CC2E)")
                    .WithFlag(5, out channels[1].Polarity, name: "Capture/Compare 2 output polarity (CC2P)")
                    .WithTaggedFlag("Capture/Compare 2 complementary output enable (CC2NE)", 6)
                    .WithFlag(7, out channels[1].ComplementaryPolarity, name: "Capture/Compare 2 complementary output polarity (CC2NP)")
                    .WithFlag(8, valueProviderCallback: _ => channels[2].OutputEnable, writeCallback: (_, val) => WriteCaptureCompareOutputEnable(2, val), name: "Capture/Compare 3 enable (CC3E)")
                    .WithFlag(9, out channels[2].Polarity, name: "Capture/Compare 3 output polarity (CC3P)")
                    .WithTaggedFlag("Capture/Compare 3 complementary output enable (CC3NE)", 10)
                    .WithFlag(11, out channels[2].ComplementaryPolarity, name: "Capture/Compare 3 complementary output polarity (CC3NP)")
                    .WithFlag(12, valueProviderCallback: _ => channels[3].OutputEnable, writeCallback: (_, val) => WriteCaptureCompareOutputEnable(3, val), name: "Capture/Compare 4 enable (CC4E)")
                    .WithFlag(13, out channels[3].Polarity, name: "Capture/Compare 4 output polarity (CC4P)")
                    .WithReservedBits(14, 1)
                    .WithFlag(15, out channels[3].ComplementaryPolarity, name: "Capture/Compare 4 complementary output polarity (CC4NP)")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Counter, new DoubleWordRegister(this)
                    .WithValueField(0, timerCounterLengthInBits,
                        writeCallback: (_, val) => Value = val,
                        valueProviderCallback: _ =>
                        {
                            if(sysbus.TryGetCurrentCPU(out var cpu))
                            {
                                cpu.SyncTime();
                            }
                            return (uint)Value;
                        }, name: "Counter value (CNT)")
                    .WithReservedBits(timerCounterLengthInBits, 32 - timerCounterLengthInBits)
                    .WithWriteCallback((_, val) =>
                    {
                        for(var i = 0; i < NumberOfCCChannels; ++i)
                        {
                            if(val < channels[i].Timer.Limit)
                            {
                                channels[i].Timer.Value = val;
                            }
                        }
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.Prescaler, new DoubleWordRegister(this)
                    .WithValueField(0, 16, writeCallback: (_, val) => Divider = val + 1, valueProviderCallback: _ => (uint)Divider - 1, name: "Prescaler value (PSC)")
                    .WithReservedBits(16, 16)
                    .WithWriteCallback((_, __) =>
                    {
                        for(var i = 0; i < NumberOfCCChannels; ++i)
                        {
                            channels[i].Timer.Divider = Divider;
                        }
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.AutoReload, new DoubleWordRegister(this)
                    .WithValueField(0, timerCounterLengthInBits, writeCallback: (_, val) =>
                    {
                        autoReloadValue = (uint)val;
                        Enabled = enableRequested && autoReloadValue > 0;
                        if(!autoReloadPreloadEnable.Value)
                        {
                            Limit = autoReloadValue;
                        }
                    }, valueProviderCallback: _ => autoReloadValue, name: "Auto-reload value (ARR)")
                    .WithReservedBits(timerCounterLengthInBits, 32 - timerCounterLengthInBits)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.RepetitionCounter, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out repetitionCounter, name: "Repetition counter (TIM1_RCR)")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.BreakAndDeadTime, new DoubleWordRegister(this)
                    .WithTag("Dead Time Generator (DTG)", 0, 8)
                    .WithTag("LOCK", 8, 2)
                    .WithTaggedFlag("Off-state selection idle mode (OSSI)", 10)
                    .WithTaggedFlag("Off-state selection run mode (OSSR)", 11)
                    .WithTaggedFlag("Break enable (BKE)", 12)
                    .WithTaggedFlag("Break polarity (BKP)", 13)
                    .WithTaggedFlag("Automatic output enable (AOE)", 14)
                    .WithTaggedFlag("Main Output Enable (MOE)", 15)
                    .WithReservedBits(16, 16)
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);

            for(var i = 0; i < NumberOfCCChannels; ++i)
            {
                var j = i;
                registers.AddConditionalRegister((long)Registers.CaptureOrCompare1 + (j * 0x4), new DoubleWordRegister(this)
                    .WithValueField(0, timerCounterLengthInBits, valueProviderCallback: _ => channels[j].Timer.Limit, writeCallback: (_, val) =>
                    {
                        if(val == 0)
                        {
                            channels[j].Timer.Enabled = false;
                        }
                        channels[j].Timer.Limit = val;
                    }, name: String.Format("Capture/compare value {0} (CCR{0})", j + 1))
                    .WithReservedBits(timerCounterLengthInBits, 32 - timerCounterLengthInBits)
                    .WithWriteCallback((_, __) =>
                    {
                        channels[j].UpdateTimer();
                        UpdateInterrupts();
                    })
                    , () => channels[j].IsOutputMode
                );

                registers.AddConditionalRegister((long)Registers.CaptureOrCompare1 + (j * 0x4), new DoubleWordRegister(this)
                    .WithValueField(0, timerCounterLengthInBits, valueProviderCallback: _ => channels[j].CapturedValue, name: String.Format("Capture/compare value {0} (CCR{0})", j + 1))
                    .WithReservedBits(timerCounterLengthInBits, 32 - timerCounterLengthInBits)
                    , () => channels[j].IsInputMode
                );
            }

            Action<DoubleWordRegister, int, CaptureCompareChannel, ChannelDirection> withHalf = (register, bitOffset, channel, direction) =>
            {
                var idx = channel.Index;
                register.WithEnumField<DoubleWordRegister, CaptureCompareSelection>(bitOffset, 2,
                    writeCallback: (_, val) => WriteCaptureCompareSelection(idx, val),
                    valueProviderCallback: _ => channel.Mode,
                    name: $"Capture/Compare {idx + 1} selection (CC{idx + 1}S)");

                if(direction == ChannelDirection.Out)
                {
                    register
                        .WithTaggedFlag($"Output compare {idx + 1} fast enable (OC{idx + 1}FE)", bitOffset + 2)
                        .WithTaggedFlag($"Output compare {idx + 1} preload enable (OC{idx + 1}PE)", bitOffset + 3)
                        .WithEnumField(bitOffset + 4, 3, out channel.CompareMode,
                            writeCallback: (_, val) =>
                            {
                                channel.CompareMode.Value = val;
                                WriteOutputCompareMode(idx, val);
                            },
                            name: $"Output compare {idx + 1} mode (OC{idx + 1}M)")
                        .WithTaggedFlag($"Output compare {idx + 1} clear enable (OC{idx + 1}CE)", bitOffset + 7);
                }
                else
                {
                    register
                        .WithEnumField(bitOffset + 2, 2, out channel.Prescaler,
                            name: $"Input capture {idx + 1} prescaler (IC{idx + 1}PSC)")
                        .WithValueField(bitOffset + 4, 4,
                            name: $"Input capture {idx + 1} filter (IC{idx + 1}F)");
                }
            };

            Action<long, CaptureCompareChannel, CaptureCompareChannel> registerCCMR = (offset, low, high) =>
            {
                DoubleWordRegister Build(ChannelDirection lowDir, ChannelDirection highDir)
                {
                    var register = new DoubleWordRegister(this);
                    withHalf(register, 0, low, lowDir);
                    withHalf(register, 8, high, highDir);
                    return register.WithReservedBits(16, 16);
                }

                registers.AddConditionalRegister(offset, Build(ChannelDirection.Out, ChannelDirection.Out),
                    () => low.IsOutputMode && high.IsOutputMode);
                registers.AddConditionalRegister(offset, Build(ChannelDirection.Out, ChannelDirection.In),
                    () => low.IsOutputMode && high.IsInputMode);
                registers.AddConditionalRegister(offset, Build(ChannelDirection.In,  ChannelDirection.Out),
                    () => low.IsInputMode && high.IsOutputMode);
                registers.AddConditionalRegister(offset, Build(ChannelDirection.In,  ChannelDirection.In),
                    () => low.IsInputMode && high.IsInputMode);
            };

            registerCCMR((long)Registers.CaptureOrCompareMode1, channels[0], channels[1]);
            registerCCMR((long)Registers.CaptureOrCompareMode2, channels[2], channels[3]);

            Reset();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number == ResetPin)
            {
                if(value)
                {
                    Reset();
                }
                return;
            }

            var oldPinValue = channels[number].Connection.IsSet;
            channels[number].Connection.Set(value);

            var timerInput = new[]
            {
                timerInput1Selection.Value ? channels[0].Connection.IsSet ^ channels[1].Connection.IsSet ^ channels[2].Connection.IsSet : channels[0].Connection.IsSet,
                channels[1].Connection.IsSet,
                channels[2].Connection.IsSet,
                channels[3].Connection.IsSet,
            };

            for(var i = 0; i < NumberOfCCChannels; ++i)
            {
                var channel = channels[i];
                if(channel.IsDirectInput || channel.IsOutputMode)
                {
                    channel.SetInput(timerInput[i]);
                }
                else if(channel.IsCrossInput)
                {
                    channel.SetInput(timerInput[i ^ 1]);
                }
            }

            if(oldPinValue != value)
            {
                HandleModes(number, value);
            }
        }

        public void Register(IGPIOReceiver peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Register(IGPIOReceiver peripheral, NullRegistrationPoint registrationPoint)
        {
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(IGPIOReceiver peripheral)
        {
            machine.UnregisterAsAChildOf(this, peripheral);
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
            autoReloadValue = initialLimit;
            enableRequested = false;
            Limit = initialLimit;
            repetitionsLeft = 0;
            updateInterruptFlag = false;
            for(var i = 0; i < NumberOfCCChannels; ++i)
            {
                channels[i].Reset();
            }
            UpdateInterrupts();
        }

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        public GPIO BreakInterrupt { get; } = new GPIO();

        public GPIO UpdateInterrupt { get; } = new GPIO();

        public GPIO TriggerInterrupt { get; } = new GPIO();

        public GPIO CommutationInterrupt { get; } = new GPIO();

        public GPIO CaptureCompareInterrupt { get; } = new GPIO();

        public IReadOnlyDictionary<int, IGPIO> Connections => connections;

        public long Size => 0x400;

        private static int InputPrescalerDivider(InputPrescaler p)
        {
            switch(p)
            {
            case InputPrescaler.One:
                return 1;
            case InputPrescaler.Two:
                return 2;
            case InputPrescaler.Four:
                return 4;
            case InputPrescaler.Eight:
                return 8;
            default:
                throw new UnreachableException();
            }
        }

        private void HandleModes(int tiSource, bool value)
        {
            if(IsEncoderMode)
            {
                HandleEncoderMode(tiSource, value);
                return;
            }

            if(!CheckTriggerSelect(tiSource))
            {
                return;
            }
            switch(slaveModeSelection.Value)
            {
            case SlaveModeSelection.ResetMode:
                HandleResetMode(tiSource, value);
                break;
            case SlaveModeSelection.GatedMode:
                HandleGatedMode(tiSource, value);
                break;
            case SlaveModeSelection.TriggerMode:
                HandleTriggerMode(tiSource, value);
                break;
            }
        }

        private void HandleEncoderMode(int tiSource, bool value)
        {
            if(tiSource == 0 && (IsEncoderMode1 || IsEncoderMode3))
            {
                Value = (value ^ channels[1].Connection.IsSet) ? Value + 1 : Value - 1;
            }
            else if(tiSource == 1 && (IsEncoderMode2 || IsEncoderMode3))
            {
                Value = (value ^ channels[0].Connection.IsSet) ? Value - 1 : Value + 1;
            }
        }

        private void HandleResetMode(int tiSource, bool value)
        {
            if(channels[tiSource].CaptureEdge == InputCaptureEdge.Falling)
            {
                value = !value;
            }

            if(!value)
            {
                return;
            }

            Value = 0;
            if(!triggerInterruptFlag.Value && triggerInterruptEnable.Value)
            {
                triggerInterruptFlag.Value = true;
                TriggerInterrupt.Set(true);
            }
        }

        private void HandleGatedMode(int tiSource, bool value)
        {
            var gatedSignal = channels[tiSource].Polarity.Value ? !value : value;
            Enabled = gatedSignal && enableRequested && autoReloadValue > 0;
        }

        private void HandleTriggerMode(int tiSource, bool value)
        {
            if(channels[tiSource].CaptureEdge == InputCaptureEdge.Falling)
            {
                value = !value;
            }

            if(value && !Enabled)
            {
                Enabled = enableRequested && autoReloadValue > 0;
            }
        }

        private bool CheckTriggerSelect(int tiSource)
        {
            return (triggerSelection.Value == TriggerSelection.TimerInput1 && tiSource == 0) || (triggerSelection.Value == TriggerSelection.TimerInput2 && tiSource == 1);
        }

        private void UpdateCaptureCompareTimers()
        {
            for(var i = 0; i < NumberOfCCChannels; ++i)
            {
                channels[i].UpdateTimer();
            }
        }

        private void WriteCaptureCompareOutputEnable(int i, bool value)
        {
            channels[i].OutputEnable = value;
            channels[i].UpdateTimer();
            if(!value)
            {
                channels[i].Connection.Unset();
            }
            this.Log(LogLevel.Noisy, "cctimer{0}: Output Enable set to {1}", i + 1, value);
        }

        private void WriteCaptureCompareInterruptEnable(int i, bool value)
        {
            channels[i].InterruptEnable = value;
            channels[i].UpdateTimer();
            this.Log(LogLevel.Noisy, "cctimer{0}: Interrupt Enable set to {1}", i + 1, value);
        }

        private void WriteCaptureCompareSelection(int i, CaptureCompareSelection value)
        {
            if(value == CaptureCompareSelection.InputTrc)
            {
                this.Log(LogLevel.Warning, $"cctimer{i}: Trc mode is not supported");
                return;
            }
            channels[i].Mode = value;
        }

        private void WriteOutputCompareMode(int i, OutputCompareMode value)
        {
            this.Log(LogLevel.Noisy, "cctimer{0}: output compare mode set to {1}", i + 1, value);
            switch(value)
            {
            case OutputCompareMode.ForceInactive:
            case OutputCompareMode.SetActiveOnMatch:
                channels[i].Connection.Unset();
                break;
            case OutputCompareMode.SetInactiveOnMatch:
            case OutputCompareMode.ForceActive:
                channels[i].Connection.Set();
                break;
            }
        }

        private void ClaimCaptureCompareInterrupt(int i, bool value)
        {
            if(!value)
            {
                channels[i].InterruptFlag = false;
                this.Log(LogLevel.Noisy, "cctimer{0}: Compare IRQ claimed", i + 1);
            }
        }

        private void UpdateInterrupts()
        {
            var ccIrq = false;
            for(var i = 0; i < NumberOfCCChannels; ++i)
            {
                ccIrq |= channels[i].InterruptFlag & channels[i].InterruptEnable;
            }

            var updateIrq = updateInterruptFlag & updateInterruptEnable.Value;
            var triggerIrq = triggerInterruptFlag.Value & triggerInterruptEnable.Value;

            IRQ.Set(ccIrq || updateIrq || triggerIrq);
            BreakInterrupt.Set(false);
            UpdateInterrupt.Set(updateIrq);
            TriggerInterrupt.Set(triggerIrq);
            CommutationInterrupt.Set(false);
            CaptureCompareInterrupt.Set(ccIrq);
        }

        private bool IsEncoderMode1 => slaveModeSelection.Value == SlaveModeSelection.EncoderMode1;

        private bool IsEncoderMode2 => slaveModeSelection.Value == SlaveModeSelection.EncoderMode2;

        private bool IsEncoderMode3 => slaveModeSelection.Value == SlaveModeSelection.EncoderMode3;

        private bool IsEncoderMode => IsEncoderMode1 || IsEncoderMode2 || IsEncoderMode3;

        private bool IsTriggerMode => slaveModeSelection.Value == SlaveModeSelection.TriggerMode;

        private uint autoReloadValue;
        private uint repetitionsLeft;
        private bool updateInterruptFlag;
        private bool enableRequested;

        private readonly uint initialLimit;
        private readonly int timerCounterLengthInBits;
        private readonly IFlagRegisterField updateDisable;
        private readonly IFlagRegisterField updateRequestSource;
        private readonly IFlagRegisterField updateInterruptEnable;
        private readonly IFlagRegisterField autoReloadPreloadEnable;
        private readonly IFlagRegisterField timerInput1Selection;
        private readonly IFlagRegisterField triggerInterruptEnable;
        private readonly IFlagRegisterField triggerInterruptFlag;
        private readonly IEnumRegisterField<TriggerSelection> triggerSelection;
        private readonly IEnumRegisterField<SlaveModeSelection> slaveModeSelection;
        private readonly IEnumRegisterField<CenterAlignedMode> centerAlignedMode;
        private readonly IValueRegisterField repetitionCounter;
        private readonly DoubleWordRegisterCollection registers;
        private readonly CaptureCompareChannel[] channels = new CaptureCompareChannel[NumberOfCCChannels];
        private readonly IMachine machine;
        private readonly IBusController sysbus;
        private readonly Dictionary<int, IGPIO> connections;
        private const int NumberOfCCChannels = 4;

        // Does not resemble an actual pin, just serves the purpose
        // of passing a Reset() request
        private const int ResetPin = 0xFF;

        private class CaptureCompareChannel
        {
            public CaptureCompareChannel(STM32_Timer parent, int index)
            {
                this.parent = parent;
                Index = index;
                Connection = new GPIO();
            }

            public void Reset()
            {
                Timer.Reset();
                InterruptFlag = false;
                InterruptEnable = false;
                OutputEnable = false;
                EdgeCounter = 0;
                Mode = CaptureCompareSelection.Output;
                Signal = false;
                Connection.Unset();
            }

            public void SetInput(bool value)
            {
                if(Signal == value)
                {
                    return;
                }

                var captureFalling = !value && (CaptureEdge == InputCaptureEdge.Falling || CaptureEdge == InputCaptureEdge.Both);
                var captureRising = value && (CaptureEdge == InputCaptureEdge.Rising || CaptureEdge == InputCaptureEdge.Both);
                if(Mode != CaptureCompareSelection.Output && (captureFalling || captureRising))
                {
                    HandleCapture();
                }

                parent.UpdateInterrupts();
                Signal = value;
            }

            public void UpdateTimer()
            {
                Timer.Enabled = parent.Enabled && IsInterruptOrOutputEnabled && parent.Value < Timer.Limit;
                if(Timer.Enabled)
                {
                    Timer.Value = parent.Value;
                }
                Timer.Direction = parent.Direction;
            }

            public InputCaptureEdge CaptureEdge => (InputCaptureEdge)(((Polarity.Value ? 1 : 0) << 1) | (ComplementaryPolarity.Value ? 1 : 0));

            public bool IsInputMode => Mode == CaptureCompareSelection.InputTiSame || Mode == CaptureCompareSelection.InputTiCross || Mode == CaptureCompareSelection.InputTrc;

            public bool IsOutputMode => Mode == CaptureCompareSelection.Output;

            public bool IsInterruptOrOutputEnabled => InterruptEnable || OutputEnable;

            public bool IsDirectInput => Mode == CaptureCompareSelection.InputTiSame;

            public bool IsCrossInput => Mode == CaptureCompareSelection.InputTiCross;

            public IEnumRegisterField<OutputCompareMode> CompareMode;
            public IEnumRegisterField<InputPrescaler> Prescaler;
            public IFlagRegisterField OvercaptureFlag;
            public IFlagRegisterField ComplementaryPolarity;

            public IFlagRegisterField Polarity;
            public LimitTimer Timer;
            public uint CapturedValue;
            public bool InterruptEnable;
            public bool InterruptFlag;
            // The signal we get after the muxing, referenced in the RM as IC1..4
            public bool Signal;

            public CaptureCompareSelection Mode;
            public int EdgeCounter;
            public bool OutputEnable;

            public readonly int Index;
            // The signal we get on the pin, referenced in the RM as TI1..4
            public readonly GPIO Connection;

            private void HandleCapture()
            {
                var divider = InputPrescalerDivider(Prescaler.Value);
                EdgeCounter++;
                if(EdgeCounter < divider)
                {
                    return;
                }
                EdgeCounter = 0;

                if(parent.sysbus.TryGetCurrentCPU(out var cpu))
                {
                    cpu.SyncTime();
                }
                CapturedValue = (uint)parent.Value;

                if(InterruptFlag)
                {
                    OvercaptureFlag.Value = true;
                }
                InterruptFlag = true;
            }

            private readonly STM32_Timer parent;
        }

        private enum ChannelDirection
        {
            In,
            Out,
        }

        private enum CenterAlignedMode
        {
            EdgeAligned    = 0,   // Direction depending on direction bit (TIMx_CR1::BIT)
            CenterAligned1 = 1,   // Up and down alternatively, compare interrupt flag set only when counting down
            CenterAligned2 = 2,   // Up and down alternatively, compare interrupt flag set only when counting up
            CenterAligned3 = 3,   // Up and down alternatively, compare interrupt flag set on both up/down counting
        }

        private enum OutputCompareMode
        {
            Frozen             = 0, // Comparison between CNT and CCR has no effect on the outputs
            SetActiveOnMatch   = 1, // Output is high when CNT = CCR
            SetInactiveOnMatch = 2, // Output is low when CNT = CCR
            ToggleOnMatch      = 3, // Output is toggled when CNT = CCR
            ForceInactive      = 4, // Output is forced low
            ForceActive        = 5, // Output is forced high
            PwmMode1           = 6, // Ascending:  output is high when CNT < CCR
                                    // Descending: output is high when CNT ≤ CCR
            PwmMode2           = 7, // Ascending:  output is high when CNT ≥ CCR
                                    // Descending: output is high when CNT > CCR
        }

        private enum InputPrescaler
        {
            One   = 0,
            Two   = 1,
            Four  = 2,
            Eight = 3,
        }

        private enum InputCaptureEdge
        {
            Rising   = 0,
            Reserved = 1,
            Falling  = 2,
            Both     = 3,
        }

        private enum CaptureCompareSelection
        {
            Output       = 0, // Channel is configured as an output
            InputTiSame  = 1, // Channel is configured as an input, mapped to its own TI (TI1 for CH1, TI2 for CH2, etc.)
            InputTiCross = 2, // Channel is configured as an input, mapped to the opposite TI (TI2 for CH1, TI1 for CH2, etc.)
            InputTrc     = 3, // Channel is configured as an input, mapped on TRC
        }

        private enum Registers : long
        {
            Control1 = 0x0,
            Control2 = 0x04,
            SlaveModeControl = 0x08,
            DmaOrInterruptEnable = 0x0C,
            Status = 0x10,
            EventGeneration = 0x14,
            CaptureOrCompareMode1 = 0x18,
            CaptureOrCompareMode2 = 0x1C,
            CaptureOrCompareEnable = 0x20,
            Counter = 0x24,
            Prescaler = 0x28,
            AutoReload = 0x2C,
            // gap intended
            RepetitionCounter = 0x30,
            // gap intended
            CaptureOrCompare1 = 0x34,
            CaptureOrCompare2 = 0x38,
            CaptureOrCompare3 = 0x3C,
            CaptureOrCompare4 = 0x40,
            BreakAndDeadTime = 0x44,
            // gap intended
            DmaControl = 0x48,
            DmaAddressForFullTransfer = 0x4C,
            Option = 0x50
        }

        private enum TriggerSelection
        {
            InternalTrigger0 = 0,
            InternalTrigger1 = 1,
            InternalTrigger2 = 2,
            InternalTrigger3 = 3,
            TI1EdgeDetector = 4,
            TimerInput1 = 5,
            TimerInput2 = 6,
            ExternalTrigger = 7,
        }

        private enum SlaveModeSelection
        {
            Disabled = 0,
            EncoderMode1 = 1,
            EncoderMode2 = 2,
            EncoderMode3 = 3,
            ResetMode = 4,
            GatedMode = 5,
            TriggerMode = 6,
            ExternalClockMode1 = 7,
        }
    }
}