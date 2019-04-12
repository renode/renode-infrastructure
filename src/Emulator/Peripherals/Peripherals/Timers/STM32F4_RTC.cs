//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class STM32F4_RTC : IDoubleWordPeripheral, IKnownSize
    {
        public STM32F4_RTC(Machine machine)
        {
            mainTimer = new TimerConfig(this);
            alarmA = new AlarmConfig(this, mainTimer);
            alarmB = new AlarmConfig(this, mainTimer);

            AlarmIRQ = new GPIO();
            ticker = new LimitTimer(machine.ClockSource, 1, this, nameof(ticker), 1, direction: Direction.Ascending, eventEnabled: true);
            ticker.LimitReached += UpdateState;
            ResetInnerTimers();

            IFlagRegisterField syncFlag = null;

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.TimeRegister, new DoubleWordRegister(this)
                    .WithValueField(0, 4, name: "SU",
                        writeCallback: (_, value) => UpdateMainTimer(Registers.TimeRegister, DateTimeSelect.Second, Rank.Units, value),
                        valueProviderCallback: _ => mainTimer.Read(DateTimeSelect.Second, Rank.Units))
                    .WithValueField(4, 3, name: "ST",
                        writeCallback: (_, value) => UpdateMainTimer(Registers.TimeRegister, DateTimeSelect.Second, Rank.Tens, value),
                        valueProviderCallback: _ => mainTimer.Read(DateTimeSelect.Second, Rank.Tens))
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 4, name: "MU",
                        writeCallback: (_, value) => UpdateMainTimer(Registers.TimeRegister, DateTimeSelect.Minute, Rank.Units, value),
                        valueProviderCallback: _ => mainTimer.Read(DateTimeSelect.Minute, Rank.Units))
                    .WithValueField(12, 3, name: "MT",
                        writeCallback: (_, value) => UpdateMainTimer(Registers.TimeRegister, DateTimeSelect.Minute, Rank.Tens, value),
                        valueProviderCallback: _ => mainTimer.Read(DateTimeSelect.Minute, Rank.Tens))
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 4, name: "HU",
                        writeCallback: (_, value) => UpdateMainTimer(Registers.TimeRegister, DateTimeSelect.Hour, Rank.Units, value),
                        valueProviderCallback: _ => mainTimer.Read(DateTimeSelect.Hour, Rank.Units))
                    .WithValueField(20, 2, name: "HT",
                        writeCallback: (_, value) => UpdateMainTimer(Registers.TimeRegister, DateTimeSelect.Hour, Rank.Tens, value),
                        valueProviderCallback: _ => mainTimer.Read(DateTimeSelect.Hour, Rank.Tens))
                    .WithFlag(22, name: "PM",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfInInitMode(Registers.TimeRegister) && CheckIfUnlocked(Registers.TimeRegister))
                            {
                                mainTimer.PM = value;
                            }
                        },
                        valueProviderCallback: _ => mainTimer.PM)
                    .WithReservedBits(23, 9)
                },
                {(long)Registers.DateRegister, new DoubleWordRegister(this, 0x2101)
                    .WithValueField(0, 4, name: "DU",
                        writeCallback: (_, value) => UpdateMainTimer(Registers.DateRegister, DateTimeSelect.Day, Rank.Units, value),
                        valueProviderCallback: _ => mainTimer.Read(DateTimeSelect.Day, Rank.Units))
                    .WithValueField(4, 2, name: "DT",
                        writeCallback: (_, value) => UpdateMainTimer(Registers.DateRegister, DateTimeSelect.Day, Rank.Tens, value),
                        valueProviderCallback: _ => mainTimer.Read(DateTimeSelect.Day, Rank.Tens))
                    .WithReservedBits(6, 2)
                    .WithValueField(8, 4, name: "MU",
                        writeCallback: (_, value) => UpdateMainTimer(Registers.DateRegister, DateTimeSelect.Month, Rank.Units, value),
                        valueProviderCallback: _ => mainTimer.Read(DateTimeSelect.Month, Rank.Units))
                    .WithValueField(12, 1, name: "MT",
                        writeCallback: (_, value) => UpdateMainTimer(Registers.DateRegister, DateTimeSelect.Month, Rank.Tens, value),
                        valueProviderCallback: _ => mainTimer.Read(DateTimeSelect.Month, Rank.Tens))
                    .WithValueField(13, 3, name: "WDU",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfInInitMode(Registers.DateRegister) && CheckIfUnlocked(Registers.DateRegister))
                            {
                                if(value == 0)
                                {
                                    this.Log(LogLevel.Warning, "Writting value 0 to WeekDay register is forbidden");
                                    return;
                                }
                                mainTimer.WeekDay = (DayOfTheWeek)value;
                            }
                        },
                        valueProviderCallback: _ => (uint)mainTimer.WeekDay)
                    .WithValueField(16, 4, name: "YU",
                        writeCallback: (_, value) => UpdateMainTimer(Registers.DateRegister, DateTimeSelect.Year, Rank.Units, value),
                        valueProviderCallback: _ => mainTimer.Read(DateTimeSelect.Year, Rank.Units))
                    .WithValueField(20, 4, name: "YT",
                        writeCallback: (_, value) => UpdateMainTimer(Registers.DateRegister, DateTimeSelect.Year, Rank.Tens, value),
                        valueProviderCallback: _ => mainTimer.Read(DateTimeSelect.Year, Rank.Tens))
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.ControlRegister, new DoubleWordRegister(this)
                    .WithTag("WUCKSEL", 0, 3)
                    .WithTag("TSEDGE", 3, 1)
                    .WithTag("REFCKON", 4, 1)
                    .WithTag("BYPSHAD", 5, 1)
                    .WithFlag(6, name: "FMT",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfUnlocked(Registers.ControlRegister))
                            {
                                AMPMFormat = value;

                                mainTimer.ConfigureAMPM();
                                alarmA.ConfigureAMPM();
                                alarmB.ConfigureAMPM();
                            }
                        },
                        valueProviderCallback: _ => AMPMFormat)
                    .WithTag("DCE", 7, 1)
                    .WithFlag(8, name: "ALRAE",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfUnlocked(Registers.ControlRegister))
                            {
                                alarmA.Enable = value;
                            }
                        },
                        valueProviderCallback: _ => alarmA.Enable)
                    .WithFlag(9, name: "ALRBE",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfUnlocked(Registers.ControlRegister))
                            {
                                alarmB.Enable = value;
                            }
                        },
                        valueProviderCallback: _ => alarmB.Enable)
                    .WithTag("WUTE", 10, 1) // Wakeup Timer not supported
                    .WithTag("TSE", 11, 1) // Timestamp not supported
                    .WithFlag(12, name: "ALRAIE",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfUnlocked(Registers.ControlRegister))
                            {
                                alarmA.InterruptEnable = value;
                            }
                        },
                        valueProviderCallback: _ => alarmA.InterruptEnable)
                    .WithFlag(13, name: "ALRBIE",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfUnlocked(Registers.ControlRegister) && value)
                            {
                                alarmB.InterruptEnable = value;
                            }
                        },
                        valueProviderCallback: _ => alarmB.InterruptEnable)
                    .WithTag("WUTIE", 14, 1) // Wakeup Timer not supported
                    .WithTag("TSIE", 15, 1) // Timestamp not supported
                    .WithTag("ADD1H", 16, 1)
                    .WithTag("SUB1H", 17, 1)
                    .WithTag("BKP", 18, 1)
                    .WithTag("COSEL", 19, 1)
                    .WithTag("POL", 20, 1)
                    .WithTag("OSEL", 21, 2)
                    .WithTag("COE", 23, 1)
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.ISR, new DoubleWordRegister(this, 0x7)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => !alarmA.Enable, name: "ALRAWF")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => !alarmB.Enable, name: "ALRBWF")
                    .WithTag("WUTWF", 2, 1)
                    .WithTag("SHPF", 3, 1)
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => mainTimer.TimeState.Year != 2000, name: "INITS")
                    .WithFlag(5, out syncFlag, FieldMode.Read | FieldMode.WriteZeroToClear, name: "RSF",
                              readCallback: (_, curr) => 
                              {
                                  // this strange logic is required by the Zephyr driver;
                                  // it wants to read 0 before reading 1, otherwise it times-out
                                  if(!curr)
                                  {
                                      syncFlag.Value = true;
                                  }
                              })
                    .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => initMode, name: "INITF")
                    .WithFlag(7, FieldMode.Write, name: "INIT",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfUnlocked(Registers.ISR))
                            {
                                ticker.Enabled = !value;
                                initMode = value;
                            }
                        })
                    .WithFlag(8, FieldMode.WriteZeroToClear | FieldMode.Read, name: "ALRAF",
                        changeCallback: (_, __) =>
                        {
                            alarmA.Flag = false;
                        },
                        valueProviderCallback: _ => alarmA.Flag)
                    .WithFlag(9, FieldMode.WriteZeroToClear | FieldMode.Read, name: "ALRBF",
                        changeCallback: (_, __) =>
                        {
                            alarmB.Flag = false;
                        },
                        valueProviderCallback: _ => alarmB.Flag)
                    .WithTag("WUTF", 10, 1)
                    .WithTag("TSF", 11, 1)
                    .WithTag("TSOVF", 12, 1)
                    .WithTag("TAMP1F", 13, 1)
                    .WithTag("TAMP2F", 14, 1)
                    .WithReservedBits(15, 1)
                    .WithTag("RECALPF", 16, 1)
                    .WithReservedBits(17, 15)
                },
                {(long)Registers.PrescalerRegister, new DoubleWordRegister(this)
                    .WithTag("PREDIV_S", 0, 15)
                    .WithReservedBits(15, 1)
                    .WithTag("PREDIV_A", 16, 7)
                    .WithReservedBits(23, 9)
                },
                {(long)Registers.WakeupTimerRegister, new DoubleWordRegister(this, 0x7F00FF)
                    .WithTag("WUT", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.CalibrationRegister, new DoubleWordRegister(this)
                    .WithTag("DC", 0, 5)
                    .WithReservedBits(5, 2)
                    .WithTag("DCS", 7, 1)
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.AlarmARegister, new DoubleWordRegister(this)
                    .WithValueField(0, 4, name: "SU",
                        writeCallback: (_, value) => UpdateTimerA(DateTimeSelect.Second, Rank.Units, value),
                        valueProviderCallback: _ => alarmA.Read(DateTimeSelect.Second, Rank.Units))
                    .WithValueField(4, 3, name: "ST",
                        writeCallback: (_, value) => UpdateTimerA(DateTimeSelect.Second, Rank.Tens, value),
                        valueProviderCallback: _ => alarmA.Read(DateTimeSelect.Second, Rank.Tens))
                    .WithFlag(7, name: "MSK1",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfDisabled(alarmA) && CheckIfUnlocked(Registers.AlarmARegister))
                            {
                                alarmA.SecondsMask = value;
                            }
                        },
                        valueProviderCallback: _ => alarmA.SecondsMask)
                    .WithValueField(8, 4, name: "MU",
                        writeCallback: (_, value) => UpdateTimerA(DateTimeSelect.Minute, Rank.Units, value),
                        valueProviderCallback: _ => alarmA.Read(DateTimeSelect.Minute, Rank.Units))
                    .WithValueField(12, 3, name: "MT",
                        writeCallback: (_, value) => UpdateTimerA(DateTimeSelect.Minute, Rank.Tens, value),
                        valueProviderCallback: _ => alarmA.Read(DateTimeSelect.Minute, Rank.Tens))
                    .WithFlag(15, name: "MSK2",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfDisabled(alarmA) && CheckIfUnlocked(Registers.AlarmARegister))
                            {
                                alarmA.MinutesMask = value;
                            }
                        }, valueProviderCallback: _ => alarmA.MinutesMask)
                    .WithValueField(16, 4, name: "HU",
                        writeCallback: (_, value) => UpdateTimerA(DateTimeSelect.Hour, Rank.Units, value),
                        valueProviderCallback: _ => alarmA.Read(DateTimeSelect.Hour, Rank.Units))
                    .WithValueField(20, 2, name: "HT",
                        writeCallback: (_, value) => UpdateTimerA(DateTimeSelect.Hour, Rank.Tens, value),
                        valueProviderCallback: _ => alarmA.Read(DateTimeSelect.Hour, Rank.Tens))
                    .WithFlag(22, name: "PM",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfDisabled(alarmA) && CheckIfUnlocked(Registers.AlarmARegister))
                            {
                                alarmA.PM = value;
                            }
                        },
                        valueProviderCallback: _ => alarmA.PM)
                    .WithFlag(23, name: "MSK3",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfDisabled(alarmA) && CheckIfUnlocked(Registers.AlarmARegister))
                            {
                                alarmA.HoursMask = value;
                            }
                        }, valueProviderCallback: _ => alarmA.HoursMask)
                    .WithValueField(24, 4, name: "DU",
                        writeCallback: (_, value) => UpdateTimerA(DateTimeSelect.Day, Rank.Units, value),
                        valueProviderCallback: _ => alarmA.Read(DateTimeSelect.Day, Rank.Units))
                    .WithValueField(28, 2, name: "DT",
                        writeCallback: (_, value) => UpdateTimerA(DateTimeSelect.Day, Rank.Tens, value),
                        valueProviderCallback: _ => alarmA.Read(DateTimeSelect.Day, Rank.Tens))
                    .WithTag("WDSEL", 30, 1) // Weekday instead of date units usupported
                    .WithFlag(31, name: "MSK4",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfDisabled(alarmA) && CheckIfUnlocked(Registers.AlarmARegister))
                            {
                                alarmA.DaysMask = value;
                            }
                        },
                        valueProviderCallback: _ => alarmA.DaysMask)
                },
                {(long)Registers.AlarmBRegister, new DoubleWordRegister(this)
                    .WithValueField(0, 4, name: "SU",
                        writeCallback: (_, value) => UpdateTimerB(DateTimeSelect.Second, Rank.Units, value),
                        valueProviderCallback: _ => alarmB.Read(DateTimeSelect.Second, Rank.Units))
                    .WithValueField(4, 3, name: "ST",
                        writeCallback: (_, value) => UpdateTimerB(DateTimeSelect.Second, Rank.Tens, value),
                        valueProviderCallback: _ => alarmB.Read(DateTimeSelect.Second, Rank.Tens))
                    .WithFlag(7, name: "MSK1",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfDisabled(alarmB) && CheckIfUnlocked(Registers.AlarmBRegister))
                            {
                                alarmB.SecondsMask = value;
                            }
                        },
                        valueProviderCallback: _ => alarmB.SecondsMask)
                    .WithValueField(8, 4, name: "MU",
                        writeCallback: (_, value) => UpdateTimerB(DateTimeSelect.Minute, Rank.Units, value),
                        valueProviderCallback: _ => alarmB.Read(DateTimeSelect.Minute, Rank.Units))
                    .WithValueField(12, 3, name: "MT",
                        writeCallback: (_, value) => UpdateTimerB(DateTimeSelect.Minute, Rank.Tens, value),
                        valueProviderCallback: _ => alarmB.Read(DateTimeSelect.Minute, Rank.Tens))
                    .WithFlag(15, name: "MSK2",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfDisabled(alarmB) && CheckIfUnlocked(Registers.AlarmBRegister))
                            {
                                alarmB.MinutesMask = value;
                            }
                        },
                        valueProviderCallback: _ => alarmB.MinutesMask)
                    .WithValueField(16, 4, name: "HU",
                        writeCallback: (_, value) => UpdateTimerB(DateTimeSelect.Hour, Rank.Units, value),
                        valueProviderCallback: _ => alarmB.Read(DateTimeSelect.Hour, Rank.Units))
                    .WithValueField(20, 2, name: "HT",
                        writeCallback: (_, value) => UpdateTimerB(DateTimeSelect.Hour, Rank.Tens, value),
                        valueProviderCallback: _ => alarmB.Read(DateTimeSelect.Hour, Rank.Tens))
                    .WithFlag(22, name: "PM",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfDisabled(alarmB) && CheckIfUnlocked(Registers.AlarmBRegister))
                            {
                                alarmB.PM = value;
                            }
                        },
                        valueProviderCallback: _ => alarmB.PM)
                    .WithFlag(23, name: "MSK3",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfDisabled(alarmB) && CheckIfUnlocked(Registers.AlarmBRegister))
                            {
                                alarmB.HoursMask = value;
                            }
                        },
                        valueProviderCallback: _ => alarmB.HoursMask)
                    .WithValueField(24, 4, name: "DU",
                        writeCallback: (_, value) => UpdateTimerB(DateTimeSelect.Day, Rank.Units, value),
                        valueProviderCallback: _ => alarmB.Read(DateTimeSelect.Day, Rank.Units))
                    .WithValueField(28, 2, name: "DT",
                        writeCallback: (_, value) => UpdateTimerB(DateTimeSelect.Day, Rank.Tens, value),
                        valueProviderCallback: _ => alarmB.Read(DateTimeSelect.Day, Rank.Tens))
                    .WithTag("WDSEL", 30, 1) // Weekday instead of date units usupported
                    .WithFlag(31, name: "MSK4",
                        writeCallback: (_, value) =>
                        {
                            if(CheckIfDisabled(alarmB) && CheckIfUnlocked(Registers.AlarmBRegister))
                            {
                                alarmB.DaysMask = value;
                            }
                        },
                        valueProviderCallback: _ => alarmB.DaysMask)
                },
                {(long)Registers.WriteProtectionRegister, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "KEY",
                        writeCallback: (_, value) =>
                        {
                            if(value == UnlockKey1 && !firstStageUnlocked)
                            {
                                firstStageUnlocked = true;
                            }
                            else if(value == UnlockKey2 && firstStageUnlocked)
                            {
                                registersUnlocked = true;
                            }
                            else
                            {
                                firstStageUnlocked = false;
                                registersUnlocked = false;
                            }
                        },
                        valueProviderCallback: _ => 0)
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.SubSecondRegister, new DoubleWordRegister(this)
                    .WithTag("SS", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.ShiftControlRegister, new DoubleWordRegister(this)
                    .WithTag("SUBFS", 0, 15)
                    .WithReservedBits(15, 16)
                    .WithTag("ADD1S", 31, 1)
                },
                {(long)Registers.TimestampTimeRegister, new DoubleWordRegister(this)
                    .WithTag("Second", 0, 7)
                    .WithReservedBits(7, 1)
                    .WithTag("Minute", 8, 7)
                    .WithReservedBits(15, 1)
                    .WithTag("Hour", 16, 6)
                    .WithTag("PM", 22, 1)
                    .WithReservedBits(23, 9)
                },
                {(long)Registers.TimestampDateRegister, new DoubleWordRegister(this)
                    .WithTag("Day", 0, 6)
                    .WithReservedBits(6, 2)
                    .WithTag("Month", 8, 5)
                    .WithTag("WDU", 13, 3)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.TimestampSubSecondRegister, new DoubleWordRegister(this)
                    .WithTag("SS", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.ClockCalibrationRegister, new DoubleWordRegister(this)
                    .WithTag("CALM", 0, 9)
                    .WithReservedBits(9, 4)
                    .WithTag("CALW16", 13, 1)
                    .WithTag("CALW8", 14, 1)
                    .WithTag("CALP", 15, 1)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.TamperAndAlternateFunctionConfigurationRegister, new DoubleWordRegister(this)
                    .WithTag("TAMP1E", 0, 1)
                    .WithTag("TAMP1TRG", 1, 1)
                    .WithTag("TAMPIE", 2, 1)
                    .WithTag("TAMP2E", 3, 1)
                    .WithTag("TAMP2TRG", 4, 1)
                    .WithReservedBits(5, 2)
                    .WithTag("TAMPTS", 7, 1)
                    .WithTag("TAMPFREQ", 8, 3)
                    .WithTag("TAMPFLT", 11, 2)
                    .WithTag("TAMPPRCH", 13, 2)
                    .WithTag("TAMPPUDIS", 15, 1)
                    .WithTag("TAMP1INSEL", 16, 1)
                    .WithTag("TSINSEL", 17, 1)
                    .WithTag("ALARMOUTTYPE", 18, 1)
                    .WithReservedBits(19, 13)
                },
                {(long)Registers.AlarmASubSecondRegister, new DoubleWordRegister(this)
                    .WithTag("SS", 0, 15)
                    .WithReservedBits(15, 9)
                    .WithTag("MASKSS", 24, 4)
                    .WithReservedBits(28, 4)
                },
                {(long)Registers.AlarmBSubSecondRegister, new DoubleWordRegister(this)
                    .WithTag("SS", 0, 15)
                    .WithReservedBits(15, 9)
                    .WithTag("MASKSS", 24, 4)
                    .WithReservedBits(28, 4)
                },
            };
            registers = new DoubleWordRegisterCollection(this, registerMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void Reset()
        {
            registers.Reset();
            ticker.Reset();
            AlarmIRQ.Unset();
            ResetInnerTimers();
            ResetInnerStatus();
        }

        public long Size => 0x1000;
        public GPIO AlarmIRQ { get; }

        private static DateTime UpdateTimeState(DateTime timeState, DateTimeSelect select, int value)
        {
            switch(select)
            {
                case DateTimeSelect.Second:
                    return timeState.With(second: value);
                case DateTimeSelect.Minute:
                    return timeState.With(minute: value);
                case DateTimeSelect.Hour:
                    return timeState.With(hour: value);
                case DateTimeSelect.Day:
                    return timeState.With(day: value);
                case DateTimeSelect.Month:
                    return timeState.With(month: value);
                case DateTimeSelect.Year:
                    return timeState.With(year: value);
                default:
                    throw new ArgumentException($"Unexpected select: {select}");
            }
        }
        
        private void ResetInnerTimers()
        {
            mainTimer.Reset();
            alarmA.Reset();
            alarmB.Reset();
        }

        private void ResetInnerStatus()
        {
            firstStageUnlocked = false;
            registersUnlocked = false;
            initMode = false;
            AMPMFormat = false;
        }

        private void UpdateState()
        {
            var previousDayOfWeek = mainTimer.TimeState.DayOfWeek;

            mainTimer.TimeState = mainTimer.TimeState.AddSeconds(1);

            if(previousDayOfWeek != mainTimer.TimeState.DayOfWeek)
            {
                // we allow for the WeekDay to be de-synchornized from
                // the actual day of week calculated from the TimeState
                // (as this is how the HW works)
                mainTimer.WeekDay = (DayOfTheWeek)(((int)mainTimer.WeekDay) % 7) + 1;
            }

            alarmA.UpdateInterruptFlag();
            alarmB.UpdateInterruptFlag();
        }

        private void UpdateInterrupts()
        {
            var state = false;

            state |= alarmA.Flag && alarmA.InterruptEnable;
            state |= alarmB.Flag && alarmB.InterruptEnable;

            AlarmIRQ.Set(state);
        }

        private bool CheckIfInInitMode(Registers reg)
        {
            if(initMode)
            {
                return true;
            }
            
            this.Log(LogLevel.Warning, "Writing to {0} allowed only in init mode", reg);
            return false;
        }

        private bool CheckIfUnlocked(Registers reg)
        {
            if(registersUnlocked)
            {
                return true;
            }
            
            this.Log(LogLevel.Warning, "Writing to {0} is allowed only when the register is unlocked", reg);
            return false;
        }

        private bool CheckIfDisabled(AlarmConfig timer)
        {
            var enabled = timer.Enable;
            if(!enabled)
            {
                return true;
            }

            this.Log(LogLevel.Warning, "Configuring {0} is allowed only when it is disabled", timer);
            return false;
        }

        private void UpdateMainTimer(Registers reg, DateTimeSelect what, Rank rank, uint value)
        {
            if(!CheckIfInInitMode(reg) && CheckIfUnlocked(reg))
            {
                return;
            }

            mainTimer.Update(what, rank, value);
        }

        private void UpdateTimerA(DateTimeSelect what, Rank rank, uint value)
        {
            if(!CheckIfDisabled(alarmA) && CheckIfUnlocked(Registers.AlarmARegister))
            {
                return;
            }

            alarmA.Update(what, rank, value);
        }

        private void UpdateTimerB(DateTimeSelect what, Rank rank, uint value)
        {
            if(!CheckIfDisabled(alarmB) && CheckIfUnlocked(Registers.AlarmBRegister))
            {
                return;
            }

            alarmB.Update(what, rank, value);
        }

        private readonly TimerConfig mainTimer;
        private readonly AlarmConfig alarmA;
        private readonly AlarmConfig alarmB;
        // timestamp timer is currenlty not implementedw

        private readonly DoubleWordRegisterCollection registers;
        private readonly LimitTimer ticker;
        private bool firstStageUnlocked;
        private bool registersUnlocked;
        private bool initMode;
        private bool AMPMFormat;

        private const uint UnlockKey1 = 0xCA;
        private const uint UnlockKey2 = 0x53;

        private class TimerConfig
        {
            public TimerConfig(STM32F4_RTC parent)
            {
                this.parent = parent;
            }
            
            public DateTime TimeState
            {
                get => timeState;

                set
                {
                    timeState = value;
                    ConfigureAMPM();
                }
            }

            public bool PM
            {
                get => timeState.Hour > 11 && parent.AMPMFormat;

                set
                {
                    pm = value;
                    ConfigureAMPM();
                }
            }

            // DateTime calculates the week day automatically based on the set date, 
            // but the device allows for setting an arbitrary value
            public DayOfTheWeek WeekDay { get; set; }

            public void Reset()
            {
                timeState = new DateTime(2020, 1, 1);

                WeekDay = DayOfTheWeek.Monday;
                pm = false;
            }

            public uint Read(DateTimeSelect select, Rank rank)
            {
                var currentValue = GetTimeSelect(select);
                return (uint)currentValue.ReadRank(rank);
            }

            public void ConfigureAMPM()
            {
                if(!parent.AMPMFormat)
                {
                    return;
                }

                if(pm)
                {
                    if(TimeState.Hour < 12)
                    {
                        var newHour = TimeState.Hour + 12;
                        timeState = UpdateTimeState(timeState, DateTimeSelect.Hour, newHour);
                    }
                }
                else
                {
                    if(TimeState.Hour >= 12)
                    {
                        var newHour = TimeState.Hour - 12;
                        timeState = UpdateTimeState(timeState, DateTimeSelect.Hour, newHour);
                    }
                }
            }

            public void Update(DateTimeSelect what, Rank rank, uint value)
            {
                var currentValue = GetTimeSelect(what);
                var val = currentValue.WithUpdatedRank((int)value, rank);
                TimeState = UpdateTimeState(TimeState, what, val);
            }

            private int GetTimeSelect(DateTimeSelect what)
            {
                switch(what)
                {
                    case DateTimeSelect.Second:
                        return timeState.Second;
                    case DateTimeSelect.Minute:
                        return timeState.Minute;
                    case DateTimeSelect.Hour:
                        return PM
                            ? timeState.Hour - 12 
                            : timeState.Hour;
                    case DateTimeSelect.Day:
                        return timeState.Day;
                    case DateTimeSelect.Month:
                        return timeState.Month;
                    case DateTimeSelect.Year:
                        return timeState.Year;
                    default:
                        throw new ArgumentException($"Unexpected date time select: {what}");
                }
            }

            private DateTime timeState;
            private bool pm;
            
            private readonly STM32F4_RTC parent;
        }

        private class AlarmConfig
        {
            public AlarmConfig(STM32F4_RTC parent, TimerConfig masterTimer)
            {
                this.parent = parent;
                this.masterTimer = masterTimer;

                Reset();
            }

            public int Day 
            { 
                get => day; 

                set
                {
                    day = value;
                    UpdateInterruptFlag();
                }
            }

            public int Hour 
            { 
                get => hour; 

                set
                {
                    hour = value;
                    UpdateInterruptFlag();
                }
            }

            public int Minute 
            { 
                get => minute; 

                set
                {
                    minute = value;
                    UpdateInterruptFlag();
                }
            }

            public int Second 
            { 
                get => second; 

                set
                {
                    second = value;
                    UpdateInterruptFlag();
                }
            }

            public bool PM
            {
                get => Hour > 11 && parent.AMPMFormat;

                set
                {
                    pm = value;
                    ConfigureAMPM();
                }
            }

            public bool Flag 
            { 
                get => flag;

                set
                {
                    if(value)
                    {
                        throw new ArgumentException("This field can only be explicitly cleared");
                    }
                    
                    flag = false;
                    parent.UpdateInterrupts();
                }
            }

            public bool Enable 
            { 
                get => enable;
                
                set
                {
                    enable = value;
                    UpdateInterruptFlag();
                }
            }

            public bool InterruptEnable 
            { 
                get => interruptEnable;
                
                set
                {
                    interruptEnable = value;
                    UpdateInterruptFlag();
                }
            }

            public bool SecondsMask 
            { 
                get => secondsMask;
                
                set
                {
                    secondsMask = value;
                    UpdateInterruptFlag();
                }
            }

            public bool MinutesMask 
            { 
                get => minutesMask;
                
                set
                {
                    minutesMask = value;
                    UpdateInterruptFlag();
                }
            }

            public bool HoursMask 
            { 
                get => hoursMask;
                
                set
                {
                    hoursMask = value;
                    UpdateInterruptFlag();
                }
            }

            public bool DaysMask 
            { 
                get => daysMask;
                
                set
                {
                    daysMask = value;
                    UpdateInterruptFlag();
                }
            }

            public void Reset()
            {
                day = 0;

                hour = 0;
                minute = 0;
                second = 0;

                pm = false;
                enable = false;
                flag = false;
                interruptEnable = false;
                secondsMask = false;
                minutesMask = false;
                hoursMask = false;
                daysMask = false;
            }

            public uint Read(DateTimeSelect select, Rank rank)
            {
                var currentValue = GetTimeSelect(select);
                return (uint)currentValue.ReadRank(rank);
            }

            public void ConfigureAMPM()
            {
                if(!parent.AMPMFormat)
                {
                    return;
                }

                if(pm)
                {
                    if(Hour < 12)
                    {
                        Hour += 12;
                    }
                }
                else
                {
                    if(Hour >= 12)
                    {
                        Hour -= 12;
                    }
                }
            }

            public void UpdateInterruptFlag()
            {
                // the initial value of `state` will be false
                // for a disabled timer
                var state = Enable;

                if(!SecondsMask)
                {
                    state &= (Second == masterTimer.TimeState.Second);
                }
                if(!MinutesMask)
                {
                    state &= (Minute == masterTimer.TimeState.Minute);
                }
                if(!HoursMask)
                {
                    state &= (Hour == masterTimer.TimeState.Hour);
                }
                if(!DaysMask)
                {
                    // day of week not supported ATM
                    state &= (Day == masterTimer.TimeState.Day);
                }

                flag = state;
                parent.UpdateInterrupts();
            }

            public void Update(DateTimeSelect what, Rank rank, uint value)
            {
                var currentValue = GetTimeSelect(what);
                var val = currentValue.WithUpdatedRank((int)value, rank);

                switch(what)
                {
                    case DateTimeSelect.Second:
                        Second = val;
                        break;
                    case DateTimeSelect.Minute:
                        Minute = val;
                        break;
                    case DateTimeSelect.Hour:
                        Hour = val;
                        break;
                    case DateTimeSelect.Day:
                        Day = val;
                        break;
                    default:
                        throw new ArgumentException($"Unexpected date time select: {what}");
                }
            }

            private int GetTimeSelect(DateTimeSelect what)
            {
                switch(what)
                {
                    case DateTimeSelect.Second:
                        return Second;
                    case DateTimeSelect.Minute:
                        return Minute;
                    case DateTimeSelect.Hour:
                        return PM
                            ? Hour - 12 
                            : Hour;
                    case DateTimeSelect.Day:
                        return Day;
                    default:
                        throw new ArgumentException($"Unexpected date time select: {what}");
                }
            }

            // private DateTime timeState;
            private bool pm;
            private bool flag;
            private bool enable;
            private bool interruptEnable;
            private bool secondsMask;
            private bool minutesMask;
            private bool hoursMask;
            private bool daysMask;

            private int day;
            private int second;
            private int minute;
            private int hour;
            
            private readonly STM32F4_RTC parent;
            private readonly TimerConfig masterTimer;
        }

        private enum DayOfTheWeek
        {
            // 0 value is forbidden
            Monday = 1,
            Tuesday = 2,
            Wednesday = 3,
            Thursday = 4,
            Friday = 5,
            Saturday = 6,
            Sunday = 7
        }

        private enum DateTimeSelect
        {
            Second,
            Minute,
            Hour,
            Day,
            Month,
            Year
        }

        private enum Registers
        {
            TimeRegister = 0x0,
            DateRegister = 0x4,
            ControlRegister = 0x8,
            ISR = 0xc,
            PrescalerRegister = 0x10,
            WakeupTimerRegister = 0x14,
            CalibrationRegister = 0x18,
            AlarmARegister = 0x1c,
            AlarmBRegister = 0x20,
            WriteProtectionRegister = 0x24,
            SubSecondRegister = 0x28,
            ShiftControlRegister = 0x2c,
            TimestampTimeRegister = 0x30,
            TimestampDateRegister = 0x34,
            TimestampSubSecondRegister = 0x38,
            ClockCalibrationRegister = 0x3c,
            TamperAndAlternateFunctionConfigurationRegister = 0x40,
            AlarmASubSecondRegister = 0x44,
            AlarmBSubSecondRegister = 0x48
        }
    }
}
