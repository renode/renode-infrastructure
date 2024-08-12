//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Timers
{
    // This class does not implement advanced-control timers interrupts
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32_Timer : LimitTimer, IDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput, IPeripheralRegister<IGPIOReceiver, NumberRegistrationPoint<int>>, IPeripheralRegister<IGPIOReceiver, NullRegistrationPoint>
    {
        public STM32_Timer(IMachine machine, long frequency, uint initialLimit) : base(machine.ClockSource, frequency, limit: initialLimit, direction: Direction.Ascending, enabled: false, autoUpdate: false)
        {
            this.machine = machine;
            sysbus = machine.GetSystemBus(this);
            IRQ = new GPIO();
            connections = Enumerable.Range(0, NumberOfCCChannels).ToDictionary(i => i, _ => (IGPIO)new GPIO());
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

            LimitReached += delegate
            {
                if(updateDisable.Value)
                {
                    return;
                }
                Limit = autoReloadValue;

                for(var i = 0; i < NumberOfCCChannels; ++i)
                {
                    UpdateCaptureCompareTimer(i);
                    if(!ccTimers[i].Enabled)
                    {
                        continue;
                    }

                    switch(outputCompareModes[i].Value)
                    {
                        case OutputCompareMode.PwmMode1:
                            Connections[i].Set();
                            break;
                        case OutputCompareMode.PwmMode2:
                            Connections[i].Unset();
                            break;
                    }
                }
                
                if(updateInterruptEnable.Value && repetitionsLeft == 0)
                {
                    // 2 of central-aligned modes should raise IRQ only on overflow/underflow, hence it happens 2 times less often
                    var centerAlignedUnbalancedMode = (centerAlignedMode.Value == CenterAlignedMode.CenterAligned1) || (centerAlignedMode.Value == CenterAlignedMode.CenterAligned2);
                    this.Log(LogLevel.Noisy, "IRQ pending");
                    updateInterruptFlag = true;
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
                ccTimers[j] = new LimitTimer(machine.ClockSource, frequency, this, String.Format("cctimer{0}", j + 1), limit: initialLimit, eventEnabled: true, direction: Direction.Ascending, enabled: false, autoUpdate: false, workMode: WorkMode.OneShot);
                ccTimers[j].LimitReached += delegate
                {
                    switch(outputCompareModes[j].Value)
                    {
                        case OutputCompareMode.SetActiveOnMatch:
                            Connections[j].Blink(); // high pulse
                            break;
                        case OutputCompareMode.SetInactiveOnMatch:
                            Connections[j].Unset();
                            Connections[j].Set(); // low pulse
                            break;
                        case OutputCompareMode.ToggleOnMatch:
                            Connections[j].Toggle();
                            break;
                        case OutputCompareMode.PwmMode1:
                            Connections[j].Unset();
                            break;
                        case OutputCompareMode.PwmMode2:
                            Connections[j].Set();
                            break;
                    }

                    if(ccInterruptEnable[j])
                    {
                        ccInterruptFlag[j] = true;
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
                        Enabled = enableRequested && autoReloadValue > 0;
                    }, valueProviderCallback: _ => enableRequested, name: "Counter enable (CEN)")
                    .WithFlag(1, out updateDisable, name: "Update disable (UDIS)")
                    .WithFlag(2, out updateRequestSource, name: "Update request source (URS)")
                    .WithFlag(3, writeCallback: (_, val) => Mode = val ? WorkMode.OneShot : WorkMode.Periodic, valueProviderCallback: _ => Mode == WorkMode.OneShot, name: "One-pulse mode (OPM)")
                    .WithFlag(4, writeCallback: (_, val) => Direction = val ? Direction.Descending : Direction.Ascending, valueProviderCallback: _ => Direction == Direction.Descending, name: "Direction (DIR)")
                    .WithEnumField(5, 2, out centerAlignedMode, name: "Center-aligned mode selection (CMS)")
                    .WithFlag(7, out autoReloadPreloadEnable, name: "Auto-reload preload enable (APRE)")
                    .WithTag("Clock Division (CKD)", 8, 2)
                    .WithReservedBits(10, 22)
                    .WithWriteCallback((_, __) => { UpdateCaptureCompareTimers(); UpdateInterrupts(); })
                },
                
                {(long)Registers.Control2, new DoubleWordRegister(this)
                    .WithTaggedFlag("CCPC", 0)
                    .WithReservedBits(1, 1)
                    .WithTaggedFlag("CCUS", 2)
                    .WithTaggedFlag("CCDS", 3)
                    .WithTag("MMS", 4, 2)
                    .WithTaggedFlag("TI1S", 7)
                    .WithTaggedFlag("OIS1", 8)
                    .WithTaggedFlag("OIS1N", 9)
                    .WithTaggedFlag("OIS2", 10)
                    .WithTaggedFlag("OIS2N", 11)
                    .WithTaggedFlag("OIS3", 12)
                    .WithTaggedFlag("OIS3N", 13)
                    .WithTaggedFlag("OIS4", 14)
                    .WithReservedBits(15, 17)
                },
                
                {(long)Registers.SlaveModeControl, new DoubleWordRegister(this)
                    .WithTag("SMS", 0, 3)
                    .WithTaggedFlag("OCCS", 3)
                    .WithTag("TS", 4, 2)
                    .WithTaggedFlag("MSM", 7)
                    .WithTag("ETF", 8, 3)
                    .WithTag("ETPS", 12, 2)
                    .WithTaggedFlag("ECE", 14)
                    .WithTaggedFlag("ETP", 15)
                    .WithReservedBits(16, 16)
                },
                
                {(long)Registers.DmaOrInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out updateInterruptEnable, name: "Update interrupt enable (UIE)")
                    .WithFlag(1, valueProviderCallback: _ => ccInterruptEnable[0], writeCallback: (_, val) => WriteCaptureCompareInterruptEnable(0, val), name: "Capture/Compare 1 interrupt enable (CC1IE)")
                    .WithFlag(2, valueProviderCallback: _ => ccInterruptEnable[1], writeCallback: (_, val) => WriteCaptureCompareInterruptEnable(1, val), name: "Capture/Compare 2 interrupt enable (CC2IE)")
                    .WithFlag(3, valueProviderCallback: _ => ccInterruptEnable[2], writeCallback: (_, val) => WriteCaptureCompareInterruptEnable(2, val), name: "Capture/Compare 3 interrupt enable (CC3IE)")
                    .WithFlag(4, valueProviderCallback: _ => ccInterruptEnable[3], writeCallback: (_, val) => WriteCaptureCompareInterruptEnable(3, val), name: "Capture/Compare 4 interrupt enable (CC4IE)")
                    .WithReservedBits(5, 1)
                    .WithTag("Trigger interrupt enable (TIE)", 6, 1)
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
                    .WithFlag(1, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, val) => ClaimCaptureCompareInterrupt(0, val), valueProviderCallback: _ => ccInterruptFlag[0], name: "Capture/Compare 1 interrupt flag (CC1IF)")
                    .WithFlag(2, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, val) => ClaimCaptureCompareInterrupt(1, val), valueProviderCallback: _ => ccInterruptFlag[1], name: "Capture/Compare 2 interrupt flag (CC2IF)")
                    .WithFlag(3, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, val) => ClaimCaptureCompareInterrupt(2, val), valueProviderCallback: _ => ccInterruptFlag[2], name: "Capture/Compare 3 interrupt flag (CC3IF)")
                    .WithFlag(4, FieldMode.Read | FieldMode.WriteZeroToClear, writeCallback: (_, val) => ClaimCaptureCompareInterrupt(3, val), valueProviderCallback: _ => ccInterruptFlag[3], name: "Capture/Compare 4 interrupt flag (CC4IF)")
                    // Reserved fields were changed to flags to prevent from very frequent logging
                    .WithFlag(5, name: "Reserved1")
                    // These write callbacks are here only to prevent from very frequent logging.
                    .WithValueField(6, 1, FieldMode.WriteZeroToClear, writeCallback: (_, __) => {}, name: "Trigger interrupt flag (TIE)")
                    .WithFlag(7, name: "Reserved2")
                    .WithFlag(8, name: "Reserved3")
                    .WithValueField(9, 1, FieldMode.WriteZeroToClear, writeCallback: (_, __) => {}, name: "Capture/Compare 1 overcapture flag (CC1OF)")
                    .WithValueField(10, 1, FieldMode.WriteZeroToClear, writeCallback: (_, __) => {}, name: "Capture/Compare 2 overcapture flag (CC2OF)")
                    .WithValueField(11, 1, FieldMode.WriteZeroToClear, writeCallback: (_, __) => {}, name: "Capture/Compare 3 overcapture flag (CC3OF)")
                    .WithValueField(12, 1, FieldMode.WriteZeroToClear, writeCallback: (_, __) => {}, name: "Capture/Compare 4 overcapture flag (CC4OF)")
                    .WithValueField(13, 19, name: "Reserved4")
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
                            if(ccTimers[i].Enabled)
                            {
                                ccTimers[i].Value = Value;
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
                
                {(long)Registers.CaptureOrCompareMode1, new DoubleWordRegister(this)
                    // Fields of this register vary between 'Output compare'/'Input capture' mode
                    // Only fields for output compare mode are defined
                    .WithEnumField<DoubleWordRegister, CaptureCompareSelection>(0, 2, writeCallback: (_, val) => WriteCaptureCompareSelection(0, val), name: "CC1S")
                    // Input mode:
                        // "IC1PSC", 2, 2
                        // "IC1F", 4, 4
                    .WithTaggedFlag("OC1FE", 2)
                    .WithTaggedFlag("OC1PE", 3)
                    .WithEnumField(4, 3, out outputCompareModes[0], writeCallback: (_, val) => WriteOutputCompareMode(0, val), name: "OC1M")
                    .WithTaggedFlag("OC1CE", 7)
                    .WithEnumField<DoubleWordRegister, CaptureCompareSelection>(8, 2, writeCallback: (_, val) => WriteCaptureCompareSelection(1, val), name: "CC2S")
                    .WithTaggedFlag("OC2FE", 10)
                    .WithTaggedFlag("OC2PE", 11)
                    .WithEnumField(12, 3, out outputCompareModes[1], writeCallback: (_, val) => WriteOutputCompareMode(1, val), name: "OC2M")
                    .WithTaggedFlag("OC2CE", 15)
                    // Input mode:
                        // "IC2PSC", 10, 2
                        // "IC2F", 12, 4
                    .WithReservedBits(16, 16)
                },
                
                {(long)Registers.CaptureOrCompareMode2, new DoubleWordRegister(this)
                    // Fields of this register vary between 'Output compare'/'Input capture' mode
                    // Only fields for output compare mode are defined
                    .WithEnumField<DoubleWordRegister, CaptureCompareSelection>(0, 2, writeCallback: (_, val) => WriteCaptureCompareSelection(2, val), name: "CC3S")
                    .WithTaggedFlag("OC3FE", 2)
                    .WithTaggedFlag("OC3PE", 3)
                    .WithEnumField(4, 3, out outputCompareModes[2], writeCallback: (_, val) => WriteOutputCompareMode(2, val), name: "OC3M")
                    .WithTaggedFlag("OC3CE", 7)
                    // Input mode:
                        // "IC3PSC", 2, 2
                        // "IC3F", 4, 4
                    .WithEnumField<DoubleWordRegister, CaptureCompareSelection>(8, 2, writeCallback: (_, val) => WriteCaptureCompareSelection(3, val), name: "CC4S")
                    .WithTaggedFlag("OC4FE", 10)
                    .WithTaggedFlag("OC4PE", 11)
                    .WithEnumField(12, 3, out outputCompareModes[3], writeCallback: (_, val) => WriteOutputCompareMode(3, val), name: "OC4M")
                    .WithTaggedFlag("OC4CE", 15)
                    // Input mode:
                        // "IC4PSC", 10, 2
                        // "IC4F", 12, 4
                    .WithReservedBits(16, 16)
                },
                
                {(long)Registers.CaptureOrCompareEnable, new DoubleWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => ccOutputEnable[0], writeCallback: (_, val) => WriteCaptureCompareOutputEnable(0, val), name: "Capture/Compare 1 enable (CC1E)")
                    .WithTaggedFlag("CC1P", 1) 
                    .WithTaggedFlag("CC1NE", 2) 
                    .WithTaggedFlag("CC1NP", 3) 
                    .WithFlag(4, valueProviderCallback: _ => ccOutputEnable[1], writeCallback: (_, val) => WriteCaptureCompareOutputEnable(1, val), name: "Capture/Compare 2 enable (CC2E)")
                    .WithTaggedFlag("CC2P", 5) 
                    .WithTaggedFlag("CC2NE", 6) 
                    .WithTaggedFlag("CC2NP", 7) 
                    .WithFlag(8, valueProviderCallback: _ => ccOutputEnable[2], writeCallback: (_, val) => WriteCaptureCompareOutputEnable(2, val), name: "Capture/Compare 3 enable (CC3E)")
                    .WithTaggedFlag("CC3P", 9) 
                    .WithTaggedFlag("CC3NE", 10) 
                    .WithTaggedFlag("CC3NP", 11) 
                    .WithFlag(12, valueProviderCallback: _ => ccOutputEnable[3], writeCallback: (_, val) => WriteCaptureCompareOutputEnable(3, val), name: "Capture/Compare 4 enable (CC4E)")
                    .WithTaggedFlag("CC4P", 13) 
                    .WithReservedBits(14, 18)
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
                            if(val < ccTimers[i].Limit)
                            {
                                ccTimers[i].Value = val;
                            }
                        }
                        UpdateInterrupts();
                    })
                },

                {(long)Registers.Prescaler, new DoubleWordRegister(this)
                    .WithValueField(0, 16, writeCallback: (_, val) => Divider = (int)val + 1, valueProviderCallback: _ => (uint)Divider - 1, name: "Prescaler value (PSC)")
                    .WithReservedBits(16, 16)
                    .WithWriteCallback((_, __) =>
                    {
                        for(var i = 0; i < NumberOfCCChannels; ++i)
                        {
                            ccTimers[i].Divider = Divider;
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

            for(var i = 0; i < NumberOfCCChannels; ++i)
            {
                var j = i;
                registersMap.Add((long)Registers.CaptureOrCompare1 + (j * 0x4), new DoubleWordRegister(this)
                    .WithValueField(0, timerCounterLengthInBits, valueProviderCallback: _ => (uint)ccTimers[j].Limit, writeCallback: (_, val) =>
                    {
                        if(val == 0)
                        {
                            ccTimers[j].Enabled = false;
                        }
                        ccTimers[j].Limit = val;
                    }, name: String.Format("Capture/compare value {0} (CCR{0})", j + 1))
                    .WithReservedBits(timerCounterLengthInBits, 32 - timerCounterLengthInBits)
                    .WithWriteCallback((_, __) => { UpdateCaptureCompareTimer(j); UpdateInterrupts(); })
                );
            }

            registers = new DoubleWordRegisterCollection(this, registersMap);
            Reset();

            EventEnabled = true;
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
                ccTimers[i].Reset();
                ccInterruptFlag[i] = false;
                ccInterruptEnable[i] = false;
                ccOutputEnable[i] = false;
                Connections[i].Unset();
            }
            UpdateInterrupts();
        }

        public GPIO IRQ { get; private set; }
        public IReadOnlyDictionary<int, IGPIO> Connections => connections;

        public long Size => 0x400;

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

        private void UpdateCaptureCompareTimer(int i)
        {
            ccTimers[i].Enabled = Enabled && IsInterruptOrOutputEnabled(i) && Value < ccTimers[i].Limit;
            if(ccTimers[i].Enabled)
            {
                ccTimers[i].Value = Value;
            }
            ccTimers[i].Direction = Direction;
        }

        private void UpdateCaptureCompareTimers()
        {
            for(var i = 0; i < NumberOfCCChannels; ++i)
            {
                UpdateCaptureCompareTimer(i);
            }
        }

        private void WriteCaptureCompareOutputEnable(int i, bool value)
        {
            ccOutputEnable[i] = value;
            UpdateCaptureCompareTimer(i);
            if(!value)
            {
                Connections[i].Unset();
            }
            this.Log(LogLevel.Noisy, "cctimer{0}: Output Enable set to {1}", i + 1, value);
        }

        private void WriteCaptureCompareInterruptEnable(int i, bool value)
        {
            ccInterruptEnable[i] = value;
            UpdateCaptureCompareTimer(i);
            this.Log(LogLevel.Noisy, "cctimer{0}: Interrupt Enable set to {1}", i + 1, value);
        }

        private bool IsInterruptOrOutputEnabled(int i)
        {
            return ccInterruptEnable[i] || ccOutputEnable[i];
        }

        private void WriteCaptureCompareSelection(int i, CaptureCompareSelection value)
        {
            if(value != CaptureCompareSelection.Output)
            {
                this.Log(LogLevel.Warning, "Channel {0}: input capture mode is not supported", i + 1);
            }
        }

        private void WriteOutputCompareMode(int i, OutputCompareMode value)
        {
            this.Log(LogLevel.Noisy, "cctimer{0}: output compare mode set to {1}", i + 1, value);
            switch(value)
            {
                case OutputCompareMode.ForceInactive:
                case OutputCompareMode.SetActiveOnMatch:
                    Connections[i].Unset();
                    break;
                case OutputCompareMode.SetInactiveOnMatch:
                case OutputCompareMode.ForceActive:
                    Connections[i].Set();
                    break;
            }
        }

        private void ClaimCaptureCompareInterrupt(int i, bool value)
        {
            if(!value)
            {
                ccInterruptFlag[i] = false;
                this.Log(LogLevel.Noisy, "cctimer{0}: Compare IRQ claimed", i + 1);
            }
        }

        private void UpdateInterrupts()
        {
            var value = false;
            value |= updateInterruptFlag & updateInterruptEnable.Value;
            for(var i  = 0; i < NumberOfCCChannels; ++i)
            {
                value |= ccInterruptFlag[i] & ccInterruptEnable[i];
            }

            IRQ.Set(value);
        }

        private readonly uint initialLimit;
        private readonly int timerCounterLengthInBits;
        private uint autoReloadValue;
        private uint repetitionsLeft;
        private bool updateInterruptFlag;
        private bool enableRequested;
        private bool[] ccInterruptFlag = new bool[NumberOfCCChannels];
        private bool[] ccInterruptEnable = new bool[NumberOfCCChannels];
        private bool[] ccOutputEnable = new bool[NumberOfCCChannels];
        private readonly IFlagRegisterField updateDisable;
        private readonly IFlagRegisterField updateRequestSource;
        private readonly IFlagRegisterField updateInterruptEnable;
        private readonly IFlagRegisterField autoReloadPreloadEnable;
        private readonly IEnumRegisterField<CenterAlignedMode> centerAlignedMode;
        private readonly IValueRegisterField repetitionCounter;
        private readonly DoubleWordRegisterCollection registers;
        private readonly IEnumRegisterField<OutputCompareMode>[] outputCompareModes = new IEnumRegisterField<OutputCompareMode>[NumberOfCCChannels];
        private readonly LimitTimer[] ccTimers = new LimitTimer[NumberOfCCChannels];
        private readonly IMachine machine;
        private readonly IBusController sysbus;
        private readonly Dictionary<int, IGPIO> connections;

        private const int NumberOfCCChannels = 4;

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

        private enum CaptureCompareSelection
        {
            Output   = 0, // Channel is configured as an output
            InputTi1 = 1, // Channel is configured as an input, mapped on TI1
            InputTi2 = 2, // Channel is configured as an input, mapped on TI2
            InputTrc = 3, // Channel is configured as an input, mapped on TRC
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
    }
}

