//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ARM_GenericTimer : IPeripheral
    {
        // defaultCounterFrequencyRegister is the value that CNTFRQ will be set to at reset.
        // It can be used to configure the value configured by an earlier-stage bootloader not
        // run under Renode in the platform file.
        public ARM_GenericTimer(IMachine machine, ulong frequency, uint defaultCounterFrequencyRegister = 0)
        {
            if(frequency > long.MaxValue)
            {
                throw new ConstructionException($"Timer doesn't support frequency greater than {long.MaxValue}, given {frequency}.");
            }

            Frequency = (long)frequency;
            this.defaultCounterFrequencyRegister = defaultCounterFrequencyRegister;
            clockSource = machine.ClockSource;

            // The names used are based on Generic Timer documentation: https://developer.arm.com/documentation/102379/0101/The-processor-timers
            el1PhysicalTimer = new TimerUnit(clockSource, this, "EL1PhysicalTimer", EL1PhysicalTimerIRQ, Frequency);
            el1VirtualTimer = new TimerUnit(clockSource, this, "EL1VirtualTimer", EL1VirtualTimerIRQ, Frequency);
            el3PhysicalTimer = new TimerUnit(clockSource, this, "EL3PhysicalTimer", EL3PhysicalTimerIRQ, Frequency);
            nonSecureEL2PhysicalTimer = new TimerUnit(clockSource, this, "NonSecureEL2PhysicalTimer", NonSecureEL2PhysicalTimerIRQ, Frequency);
            nonSecureEL2VirtualTimer = new TimerUnit(clockSource, this, "NonSecureEL2VirtualTimer", NonSecureEL2VirtualTimerIRQ, Frequency);

            registersAArch64 = new QuadWordRegisterCollection(this, BuildRegisterAArch64Map());
            doubleWordRegistersAArch32 = new DoubleWordRegisterCollection(this, BuildDoubleWordRegisterAArch32Map());
            quadWordRegistersAArch32 = new QuadWordRegisterCollection(this, BuildQuadWordRegisterAArch32Map());

            Reset();
        }

        public void WriteRegisterAArch64(uint offset, ulong value)
        {
            this.Log(LogLevel.Debug, "Write to {0} (0x{1:X}) AArch64 register, value 0x{2:X}", (RegistersAArch64)offset, offset, value);
            registersAArch64.Write(offset, value);
        }

        public ulong ReadRegisterAArch64(uint offset)
        {
            var register = (RegistersAArch64)offset;
            var value = registersAArch64.Read(offset);

            // There can be lots and lots of '*Count' register reads.
            if((register != RegistersAArch64.PhysicalCount && register != RegistersAArch64.VirtualCount) || EnableCountReadLogs)
            {
                this.Log(LogLevel.Debug, "Read from {0} (0x{1:X}) AArch64 register, value 0x{2:X}", register, offset, value);
            }
            return value;
        }

        public void WriteDoubleWordRegisterAArch32(uint offset, uint value)
        {
            this.Log(LogLevel.Debug, "Write to {0} (0x{1:X}) AArch32 register, value 0x{2:X}", (DoubleWordRegistersAArch32)offset, offset, value);
            doubleWordRegistersAArch32.Write(offset, value);
        }

        public uint ReadDoubleWordRegisterAArch32(uint offset)
        {
            var value = doubleWordRegistersAArch32.Read(offset);
            this.Log(LogLevel.Debug, "Read from {0} (0x{1:X}) AArch32 register, value 0x{2:X}", (DoubleWordRegistersAArch32)offset, offset, value);
            return value;
        }

        public void WriteQuadWordRegisterAArch32(uint offset, ulong value)
        {
            this.Log(LogLevel.Debug, "Write to {0} (0x{1:X}) AArch32 64-bit register, value 0x{2:X}", (QuadWordRegistersAArch32)offset, offset, value);
            quadWordRegistersAArch32.Write(offset, value);
        }

        public ulong ReadQuadWordRegisterAArch32(uint offset)
        {
            var register = (QuadWordRegistersAArch32)offset;
            var value = quadWordRegistersAArch32.Read(offset);

            // There can be lots and lots of '*Count' register reads.
            if((register != QuadWordRegistersAArch32.PhysicalCount && register != QuadWordRegistersAArch32.VirtualCount) || EnableCountReadLogs)
            {
                this.Log(LogLevel.Debug, "Read from {0} (0x{1:X}) AArch32 64-bit register, value 0x{2:X}", register, offset, value);
            }
            return value;
        }

        public void Reset()
        {
            el1PhysicalTimer.Reset();
            el1VirtualTimer.Reset();
            el3PhysicalTimer.Reset();
            nonSecureEL2PhysicalTimer.Reset();
            nonSecureEL2VirtualTimer.Reset();

            CounterFrequencyRegister = defaultCounterFrequencyRegister;
            registersAArch64.Reset();
            doubleWordRegistersAArch32.Reset();
            quadWordRegistersAArch32.Reset();
        }

        public bool EnableCountReadLogs { get; set; }
        public long Frequency { get; }
        // There is the counter frequency register used by a software to discover a frequency of a timer
        // In the model we allow to preset it using the `CounterFrequencyRegister` property to support simulation scenarios without a bootloader.
        public uint CounterFrequencyRegister { set; get; }

        public GPIO EL1PhysicalTimerIRQ { get; } = new GPIO();
        public GPIO EL1VirtualTimerIRQ { get; } = new GPIO();
        public GPIO EL3PhysicalTimerIRQ { get; } = new GPIO();
        public GPIO NonSecureEL2PhysicalTimerIRQ { get; } = new GPIO();
        public GPIO NonSecureEL2VirtualTimerIRQ { get; } = new GPIO();

        // Some physical timers names in the ARMv7 specification are different than names in the ARMv8-A specification.
        public GPIO PL1PhysicalTimerIRQ => EL1PhysicalTimerIRQ;
        public GPIO PL2PhysicalTimerIRQ => NonSecureEL2PhysicalTimerIRQ;
        public GPIO VirtualTimerIRQ => EL1VirtualTimerIRQ;

        private Dictionary<long, QuadWordRegister> BuildRegisterAArch64Map()
        {
            var registersMap = new Dictionary<long, QuadWordRegister>
            {
                {(long)RegistersAArch64.Frequency,
                    BuildFrequencyRegister(new QuadWordRegister(this), "CounterFrequency", 64)
                },

                {(long)RegistersAArch64.PhysicalCount,
                    BuildTimerCountValueRegister(el1PhysicalTimer, "EL1Physical")
                },
                {(long)RegistersAArch64.VirtualCount,
                    BuildTimerCountValueRegister(el1VirtualTimer, "EL1Virtual")
                },
            
                // There is no implementation of the PhysicalTimerOffset register, because it require the Enhanced Counter Virtualization support.

                {(long)RegistersAArch64.VirtualOffset,
                    BuildTimerOffsetRegister(el1VirtualTimer, "EL1Virtual")
                },

                {(long)RegistersAArch64.EL1PhysicalTimerControl,
                    BuildTimerControlRegister(new QuadWordRegister(this), el1PhysicalTimer, "EL1PhysicalTimer")
                },
                {(long)RegistersAArch64.EL1VirtualTimerControl,
                    BuildTimerControlRegister(new QuadWordRegister(this), el1VirtualTimer, "EL1VirtualTimer")
                },
                {(long)RegistersAArch64.EL3PhysicalTimerControl,
                    BuildTimerControlRegister(new QuadWordRegister(this), el3PhysicalTimer, "EL3PhysicalTimer")
                },
                {(long)RegistersAArch64.NonSecureEL2PhysicalTimerControl,
                    BuildTimerControlRegister(new QuadWordRegister(this), nonSecureEL2PhysicalTimer, "NonSecureEL2PhysicalTimer")
                },
                {(long)RegistersAArch64.NonSecureEL2VirtualTimerControl,
                    BuildTimerControlRegister(new QuadWordRegister(this), nonSecureEL2VirtualTimer, "NonSecureEL2VirtualTimer")
                },

                {(long)RegistersAArch64.EL1PhysicalTimerCompareValue,
                    BuildTimerCompareValueRegister(el1PhysicalTimer, "EL1PhysicalTimer")
                },
                {(long)RegistersAArch64.EL1VirtualTimerCompareValue,
                    BuildTimerCompareValueRegister(el1VirtualTimer, "EL1VirtualTimer")
                },
                {(long)RegistersAArch64.EL3PhysicalTimerCompareValue,
                    BuildTimerCompareValueRegister(el3PhysicalTimer, "EL3PhysicalTimer")
                },
                {(long)RegistersAArch64.NonSecureEL2PhysicalTimerCompareValue,
                    BuildTimerCompareValueRegister(nonSecureEL2PhysicalTimer, "NonSecureEL2PhysicalTimer")
                },
                {(long)RegistersAArch64.NonSecureEL2VirtualTimerCompareValue,
                    BuildTimerCompareValueRegister(nonSecureEL2VirtualTimer, "NonSecureEL2VirtualTimer")
                },

                {(long)RegistersAArch64.EL1PhysicalTimerValue,
                    BuildTimerCountDownValueRegister(new QuadWordRegister(this), el1PhysicalTimer, "EL1PhysicalTimer", 64)
                },
                {(long)RegistersAArch64.EL1VirtualTimerValue,
                    BuildTimerCountDownValueRegister(new QuadWordRegister(this), el1VirtualTimer, "EL1VirtualTimer", 64)
                },
                {(long)RegistersAArch64.EL3PhysicalTimerValue,
                    BuildTimerCountDownValueRegister(new QuadWordRegister(this), el3PhysicalTimer, "EL3PhysicalTimer", 64)
                },
                {(long)RegistersAArch64.NonSecureEL2PhysicalTimerValue,
                    BuildTimerCountDownValueRegister(new QuadWordRegister(this), nonSecureEL2PhysicalTimer, "NonSecureEL2PhysicalTimer", 64)
                },
                {(long)RegistersAArch64.NonSecureEL2VirtualTimerValue,
                    BuildTimerCountDownValueRegister(new QuadWordRegister(this), nonSecureEL2VirtualTimer, "NonSecureEL2VirtualTimer", 64)
                },
            };

            // Specultative access doesn't occure in Renode.
            // Self synchronized registers can be just mapped to normal registers.
            registersMap[(long)RegistersAArch64.PhysicalSelfSynchronizedCount] =
                registersMap[(long)RegistersAArch64.PhysicalCount];
            registersMap[(long)RegistersAArch64.VirtualSelfSynchronizedCount] =
                registersMap[(long)RegistersAArch64.VirtualCount];

            return registersMap;
        }

        private Dictionary<long, DoubleWordRegister> BuildDoubleWordRegisterAArch32Map()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)DoubleWordRegistersAArch32.Frequency,
                    BuildFrequencyRegister(new DoubleWordRegister(this), "CounterFrequency", 32)
                },

                {(long)DoubleWordRegistersAArch32.PL1PhysicalTimerControl,
                    BuildTimerControlRegister(new DoubleWordRegister(this), el1PhysicalTimer, "PL1PhysicalTimer")
                },
                {(long)DoubleWordRegistersAArch32.PL2PhysicalTimerControl,
                    BuildTimerControlRegister(new DoubleWordRegister(this), nonSecureEL2PhysicalTimer, "PL2PhysicalTimer")
                },
                {(long)DoubleWordRegistersAArch32.VirtualTimerControl,
                    BuildTimerControlRegister(new DoubleWordRegister(this), el1VirtualTimer, "VirtualTimer")
                },

                {(long)DoubleWordRegistersAArch32.PL1PhysicalTimerValue,
                    BuildTimerCountDownValueRegister(new DoubleWordRegister(this), el1PhysicalTimer, "PL1PhysicalTimer", 32)
                },
                {(long)DoubleWordRegistersAArch32.PL2PhysicalTimerValue,
                    BuildTimerCountDownValueRegister(new DoubleWordRegister(this), nonSecureEL2PhysicalTimer, "PL2PhysicalTimer", 32)
                },
                {(long)DoubleWordRegistersAArch32.VirtualTimerValue,
                    BuildTimerCountDownValueRegister(new DoubleWordRegister(this), el1VirtualTimer, "VirtualTimer", 32)
                }
            };
            return registersMap;
        }

        private Dictionary<long, QuadWordRegister> BuildQuadWordRegisterAArch32Map()
        {
            var registersMap = new Dictionary<long, QuadWordRegister>
            {
                {(long)QuadWordRegistersAArch32.PhysicalCount,
                    BuildTimerCountValueRegister(el1PhysicalTimer, "Physical")
                },
                {(long)QuadWordRegistersAArch32.VirtualCount,
                    BuildTimerCountValueRegister(el1VirtualTimer, "Virtual")
                },

                {(long)QuadWordRegistersAArch32.VirtualOffset,
                    BuildTimerOffsetRegister(el1VirtualTimer, "Virtual")
                },

                {(long)QuadWordRegistersAArch32.PL1PhysicalTimerCompareValue,
                    BuildTimerCompareValueRegister(el1PhysicalTimer, "PL1PhysicalTimer")
                },
                {(long)QuadWordRegistersAArch32.PL2PhysicalTimerCompareValue,
                    BuildTimerCompareValueRegister(nonSecureEL2PhysicalTimer, "PL2PhysicalTimer")
                },
                {(long)QuadWordRegistersAArch32.VirtualTimerCompareValue,
                    BuildTimerCompareValueRegister(el1VirtualTimer, "VirtualTimer")
                }
            };
            return registersMap;
        }

        private T BuildFrequencyRegister<T>(T register, string name, int registerWidth) where T : PeripheralRegister
        {
            // According to the documentation "the value of the register is not interpreted by hardware",
            // it's there for software to discover a frequency of a timer set earlier by a bootloader.
            return register
                .WithReservedBits(32, registerWidth - 32)
                .WithValueField(0, 32, name: name,
                    valueProviderCallback: _ => CounterFrequencyRegister,
                    writeCallback: (_, val) =>
                        {
                            CounterFrequencyRegister = (uint)val;
                            if(Frequency != CounterFrequencyRegister)
                            {
                                this.Log(LogLevel.Warning, "Setting the counter frequency register to 0x{0:X} ({0}Hz), which is different than timer frequency ({1}Hz).", val, Frequency);
                            }
                        }
                );
        }

        private QuadWordRegister BuildTimerControlRegister(QuadWordRegister register, TimerUnit timer, string namePrefix)
        {
            return BuildTimerControlGenericRegister(register, timer, namePrefix, 64)
                .WithWriteCallback((_, __) => timer.UpdateInterrupt());
        }

        private DoubleWordRegister BuildTimerControlRegister(DoubleWordRegister register, TimerUnit timer, string namePrefix)
        {
            return BuildTimerControlGenericRegister(register, timer, namePrefix, 32)
                .WithWriteCallback((_, __) => timer.UpdateInterrupt());
        }

        private T BuildTimerControlGenericRegister<T>(T register, TimerUnit timer, string namePrefix, int registerWidth) where T : PeripheralRegister
        {
            return register
                .WithReservedBits(3, registerWidth - 3)
                .WithFlag(2, FieldMode.Read, name: $"{namePrefix}InterruptStatus",
                    valueProviderCallback: _ => timer.InterruptStatus
                )
                .WithFlag(1, name: $"{namePrefix}InterruptMask",
                    writeCallback: (_, val) => timer.InterruptMask = val,
                    valueProviderCallback: _ => timer.InterruptMask
                )
                .WithFlag(0, name: $"{namePrefix}InterruptEnable",
                    writeCallback: (_, val) => timer.InterruptEnable = val,
                    valueProviderCallback: _ => timer.InterruptEnable
                );
        }

        private T BuildTimerCountDownValueRegister<T>(T register, TimerUnit timer, string namePrefix, int registerWidth) where T : PeripheralRegister
        {
            return register
                .WithReservedBits(32, registerWidth - 32)
                .WithValueField(0, 32, name: $"{namePrefix}Value",
                    writeCallback: (_, val) =>
                        {
                            timer.CountDownValue = (int)val;
                            timer.UpdateInterrupt();
                        },
                    valueProviderCallback: _ => (ulong)timer.CountDownValue
                );
        }

        private QuadWordRegister BuildTimerCountValueRegister(TimerUnit timer, string namePrefix)
        {
            return new QuadWordRegister(this)
                .WithValueField(0, 64, FieldMode.Read, name: $"{namePrefix}Count",
                    valueProviderCallback: _ => timer.Value
                );
        }

        private QuadWordRegister BuildTimerOffsetRegister(TimerUnit timer, string namePrefix)
        {
            return new QuadWordRegister(this)
                .WithValueField(0, 64, name: $"{namePrefix}Offset",
                    writeCallback: (_, val) =>
                        {
                            timer.Offset = val;
                            timer.UpdateInterrupt();
                        },
                    valueProviderCallback: _ => timer.Offset
                );
        }

        private QuadWordRegister BuildTimerCompareValueRegister(TimerUnit timer, string namePrefix)
        {
            return new QuadWordRegister(this)
                .WithValueField(0, 64, name: $"{namePrefix}CompareValue",
                    writeCallback: (_, val) =>
                        {
                            timer.CompareValue = val;
                            timer.UpdateInterrupt();
                        },
                    valueProviderCallback: _ => timer.CompareValue
                );
        }

        private readonly TimerUnit el1PhysicalTimer;
        private readonly TimerUnit el1VirtualTimer;
        private readonly TimerUnit el3PhysicalTimer;
        private readonly TimerUnit nonSecureEL2PhysicalTimer;
        private readonly TimerUnit nonSecureEL2VirtualTimer;
        private readonly QuadWordRegisterCollection registersAArch64;
        private readonly DoubleWordRegisterCollection doubleWordRegistersAArch32;
        private readonly QuadWordRegisterCollection quadWordRegistersAArch32;
        private readonly IClockSource clockSource;
        private readonly uint defaultCounterFrequencyRegister;

        private enum RegistersAArch64
        {
            // Enum values are created from op0, op1, CRn, CRm and op2 fields of the MRS instruction.
            Frequency = 0xdf00, // CNTFRQ_EL0
            HypervisorControl = 0xe708, // CNTHCTL_EL2
            KernelControl = 0xc708, // CNTKCTL_EL1

            PhysicalCount = 0xdf01, // CNTPCT_EL0
            PhysicalOffset = 0xe706, // CNTPOFF_EL2
            PhysicalSelfSynchronizedCount = 0xdf05, // CNTPCTSS_EL0

            VirtualCount = 0xdf02, // CNTVCT_EL0
            VirtualOffset = 0xe703, // CNTVOFF_EL2
            VirtualSelfSynchronizedCount = 0xdf06, // CNTVCTSS_EL0

            EL1PhysicalTimerValue = 0xdf10, // CNTP_TVAL_EL0
            EL1PhysicalTimerControl = 0xdf11, // CNTP_CTL_EL0
            EL1PhysicalTimerCompareValue = 0xdf12, // CNTP_CVAL_EL0
            EL1VirtualTimerValue = 0xdf18, // CNTV_TVAL_EL0
            EL1VirtualTimerControl = 0xdf19, // CNTV_CTL_EL0
            EL1VirtualTimerCompareValue = 0xdf1a, // CNTV_CVAL_EL0

            EL3PhysicalTimerValue = 0xff10, // CNTPS_TVAL_EL1
            EL3PhysicalTimerControl = 0xff11, // CNTPS_CTL_EL1
            EL3PhysicalTimerCompareValue = 0xff12, // CNTPS_CVAL_EL1

            NonSecureEL2PhysicalTimerValue = 0xe710, // CNTHP_TVAL_EL2
            NonSecureEL2PhysicalTimerControl = 0xe711, // CNTHP_CTL_EL2
            NonSecureEL2PhysicalTimerCompareValue = 0xe712, // CNTHP_CVAL_EL2
            NonSecureEL2VirtualTimerValue = 0xe718, // CNTHV_TVAL_EL2
            NonSecureEL2VirtualTimerControl = 0xe719, // CNTHV_CTL_EL2
            NonSecureEL2VirtualTimerCompareValue = 0xe71a, // CNTHV_CVAL_EL2

            // Secure EL2 timers are added by ARMv8.4-SecEL2 extension.
            SecureEL2PhysicalTimerValue = 0xe728, // CNTHPS_TVAL_EL2
            SecureEL2PhysicalTimerControl = 0xe729, // CNTHPS_CTL_EL2
            SecureEL2PhysicalTimerCompareValue = 0xe72a, // CNTHPS_CVAL_EL2
            SecureEL2VirtualTimerValue = 0xe720, // CNTHVS_TVAL_EL2
            SecureEL2VirtualTimerControl = 0xe721, // CNTHVS_CTL_EL2
            SecureEL2VirtualTimerCompareValue = 0xe722, // CNTHVS_CVAL_EL2
        }

        private enum DoubleWordRegistersAArch32 : uint
        {
            // Enum values are created from opc1, CRn, opc2 and CRm fields of the MRC instruction.
            Frequency = 0x0e0000, // CNTFRQ
            PL1PhysicalControl = 0x0e0001, // CNTKCTL
            PL1PhysicalTimerValue = 0x0e0002, // CNTP_TVAL
            PL1PhysicalTimerControl = 0x0e0022, // CNTP_CTL
            VirtualTimerValue = 0x0e0003, // CNTV_TVAL
            VirtualTimerControl = 0x0e0023, // CNTV_CTL
            PL2PhysicalControl = 0x8e0001, // CNTHCTL
            PL2PhysicalTimerValue = 0x8e0002, // CNTHP_TVAL
            PL2PhysicalTimerControl = 0x8e0022// CNTHP_CTL PL2
        };

        private enum QuadWordRegistersAArch32 : uint
        {
            // Enum values are created from the opc1 and CRm fields of the MRRC instruction.
            PhysicalCount = 0x0e, // CNTPCT
            VirtualCount = 0x1e, // CNTVCT
            PL1PhysicalTimerCompareValue = 0x2e, // CNTP_CVAL
            VirtualTimerCompareValue = 0x3e, // CNTV_CVAL
            VirtualOffset = 0x4e, // CNTVOFF
            PL2PhysicalTimerCompareValue = 0x6e // CNTHP_CVAL
        };

        private class TimerUnit
        {
            public TimerUnit(IClockSource clockSource, IPeripheral parent, string name, GPIO irq, long frequency)
            {
                this.clockSource = clockSource;
                this.name = name;
                this.parent = parent;
                IRQ = irq;

                timer = new ComparingTimer(clockSource, frequency, parent, name, limit: ulong.MaxValue, compare: ulong.MaxValue, enabled: true, eventEnabled: true);
                timer.CompareReached += OnCompareReached;
            }

            public void Reset()
            {
                offset = 0;
                timer.Reset();

                InterruptMask = false;
                InterruptEnable = false;
                UpdateStatus();
                UpdateInterrupt();
            }

            public void UpdateInterrupt()
            {
                var value = InterruptStatus && !InterruptMask && InterruptEnable;
                if(value != IRQ.IsSet)
                {
                    parent.Log(LogLevel.Debug, "{0}: {1} IRQ", name, value ? "setting" : "unsetting");
                    IRQ.Set(value);
                }
            }

            public GPIO IRQ { get; }

            public ulong Value => timer.Value;

            // The offset property is defined as the difference between value properties of a some parent timer and the timer itself.
            // For example the Virtual timer has an offset over the Physical timer.
            public ulong Offset
            {
                get => offset;
                set
                {
                    clockSource.ExecuteInLock(() =>
                        {
                            var offsetDiff = value - offset;
                            timer.Value -= offsetDiff;
                            offset += offsetDiff;
                            UpdateStatus();
                        });
                }
            }

            public ulong CompareValue
            {
                get => timer.Compare;
                set
                {
                    clockSource.ExecuteInLock(() =>
                        {
                            timer.Compare = value;
                            UpdateStatus();
                        });
                }
            }

            public int CountDownValue
            {
                get
                {
                    int value = 0;
                    clockSource.ExecuteInLock(() =>
                        {
                            value = (int)(CompareValue - Value);
                        });
                    return value;
                }
                set
                {
                    clockSource.ExecuteInLock(() =>
                        {
                            CompareValue = Value + (ulong)value;
                        });
                }
            }

            public bool InterruptStatus { get; private set; }
            public bool InterruptMask { get; set; }
            public bool InterruptEnable { get; set; }

            private void UpdateStatus()
            {
                InterruptStatus = Value >= CompareValue;
                // This method is typically called inside the IClockSource.ExecuteInLock() helper.
                // To prevent deadlock it doesn't call the UpdateInterrupt() method.
            }

            private void OnCompareReached()
            {
                InterruptStatus = true;
                UpdateInterrupt();
            }

            private ulong offset;

            private readonly IClockSource clockSource;
            private readonly string name;
            private readonly IPeripheral parent;
            private readonly ComparingTimer timer;
        }
    }
}
