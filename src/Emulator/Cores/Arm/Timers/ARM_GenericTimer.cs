//
// Copyright (c) 2010-2023 Antmicro
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
        public ARM_GenericTimer(Machine machine, ulong frequency)
        {
            if(frequency > long.MaxValue)
            {
                throw new ConstructionException($"Timer doesn't support frequency greater than {long.MaxValue}, given {frequency}.");
            }

            Frequency = (long)frequency;
            clockSource = machine.ClockSource;

            physicalTimer = new TimerUnit(clockSource, this, "physicalTimer", PhysicalTimerIRQ, Frequency);
            nonSecurePhysicalTimer = new TimerUnit(clockSource, this, "nonSecurePhysicalTimer", NonSecurePhysicalTimerIRQ, Frequency);
            virtualTimer = new TimerUnit(clockSource, this, "virtualTimer", VirtualTimerIRQ, Frequency);
            hypervisorPhysicalTimer = new TimerUnit(clockSource, this, "hypervisorPhysicalTimer", HypervisorPhysicalTimerIRQ, Frequency);

            registersAArch64 = new QuadWordRegisterCollection(this, BuildRegisterAArch64Map());
            doubleWordRegistersAArch32 = new DoubleWordRegisterCollection(this, BuildDoubleWordRegisterAArch32Map());
            quadWordRegistersAArch32 = new QuadWordRegisterCollection(this, BuildQuadWordRegisterAArch32Map());
        }

        public void WriteRegisterAArch64(uint offset, ulong value)
        {
            registersAArch64.Write(offset, value);
        }

        public ulong ReadRegisterAArch64(uint offset)
        {
            var value = registersAArch64.Read(offset);
            return value;
        }

        public void WriteDoubleWordRegisterAArch32(uint offset, uint value)
        {
            doubleWordRegistersAArch32.Write(offset, value);
        }

        public uint ReadDoubleWordRegisterAArch32(uint offset)
        {
            var value = doubleWordRegistersAArch32.Read(offset);
            return value;
        }

        public void WriteQuadWordRegisterAArch32(uint offset, ulong value)
        {
            quadWordRegistersAArch32.Write(offset, value);
        }

        public ulong ReadQuadWordRegisterAArch32(uint offset)
        {
            var value = quadWordRegistersAArch32.Read(offset);
            return value;
        }

        public void Reset()
        {
            physicalTimer.Reset();
            nonSecurePhysicalTimer.Reset();
            virtualTimer.Reset();
            hypervisorPhysicalTimer.Reset();

            CounterFrequencyRegister = 0;
            registersAArch64.Reset();
            doubleWordRegistersAArch32.Reset();
            quadWordRegistersAArch32.Reset();
        }

        public long Frequency { get; }
        // There is the counter frequency register used by a software to discover a frequency of a timer
        // In the model we allow to preset it using the `CounterFrequencyRegister` property to support simulation scenarios without a bootloader.
        public uint CounterFrequencyRegister { set; get; }

        public GPIO PhysicalTimerIRQ { get; } = new GPIO();
        public GPIO NonSecurePhysicalTimerIRQ { get; } = new GPIO();
        public GPIO VirtualTimerIRQ { get; } = new GPIO();
        public GPIO HypervisorPhysicalTimerIRQ { get; } = new GPIO();
        // Some physical timers names in the ARMv7 specification are different than names in the ARMv8-A specification.
        // We added mapping for them in order to make the model generic.
        // The virtual timer has same name in both specifications, so in this case additional mapping isn't needed.
        public GPIO PL1PhysicalTimerIRQ => PhysicalTimerIRQ;
        public GPIO PL2PhysicalTimerIRQ => NonSecurePhysicalTimerIRQ;

        private Dictionary<long, QuadWordRegister> BuildRegisterAArch64Map()
        {
            var registersMap = new Dictionary<long, QuadWordRegister>
            {
                {(long)RegistersAArch64.Frequency,
                    BuildFrequencyRegister(new QuadWordRegister(this), "CounterFrequency", 64)
                },

                {(long)RegistersAArch64.PhysicalCount,
                    BuildTimerCountValueRegister(physicalTimer, "Physical")
                },
                {(long)RegistersAArch64.VirtualCount,
                    BuildTimerCountValueRegister(virtualTimer, "Virtual")
                },
            
                // There is no implementation of the PhysicalTimerOffset register, because it require the Enhanced Counter Virtualization support.

                {(long)RegistersAArch64.VirtualOffset,
                    BuildTimerOffsetRegister(virtualTimer, "Virtual")
                },

                {(long)RegistersAArch64.PhysicalTimerControl,
                    BuildTimerControlRegister(new QuadWordRegister(this), physicalTimer, "PhysicalTimer")
                },
                {(long)RegistersAArch64.NonSecurePhysicalTimerControl,
                    BuildTimerControlRegister(new QuadWordRegister(this), nonSecurePhysicalTimer, "NonSecurePhysicalTimer")
                },
                {(long)RegistersAArch64.VirtualTimerControl,
                    BuildTimerControlRegister(new QuadWordRegister(this), virtualTimer, "VirtualTimer")
                },
                {(long)RegistersAArch64.HypervisorPhysicalTimerControl,
                    BuildTimerControlRegister(new QuadWordRegister(this), hypervisorPhysicalTimer, "HypervisorPhysicalTimer")
                },

                {(long)RegistersAArch64.PhysicalTimerCompareValue,
                    BuildTimerCompareValueRegister(physicalTimer, "PhysicalTimer")
                },
                {(long)RegistersAArch64.NonSecurePhysicalTimerCompareValue,
                    BuildTimerCompareValueRegister(nonSecurePhysicalTimer, "NonSecurePhysicalTimer")
                },
                {(long)RegistersAArch64.VirtualTimerCompareValue,
                    BuildTimerCompareValueRegister(virtualTimer, "VirtualTimer")
                },
                {(long)RegistersAArch64.HypervisorPhysicalTimerCompareValue,
                    BuildTimerCompareValueRegister(hypervisorPhysicalTimer, "HypervisorPhysicalTimer")
                },

                {(long)RegistersAArch64.PhysicalTimerValue,
                    BuildTimerCountDownValueRegister(new QuadWordRegister(this), physicalTimer, "PhysicalTimer", 64)
                },
                {(long)RegistersAArch64.NonSecurePhysicalTimerValue,
                    BuildTimerCountDownValueRegister(new QuadWordRegister(this), nonSecurePhysicalTimer, "NonSecurePhysicalTimer", 64)
                },
                {(long)RegistersAArch64.VirtualTimerValue,
                    BuildTimerCountDownValueRegister(new QuadWordRegister(this), virtualTimer, "VirtualTimer", 64)
                },
                {(long)RegistersAArch64.HypervisorPhysicalTimerValue,
                    BuildTimerCountDownValueRegister(new QuadWordRegister(this), hypervisorPhysicalTimer, "HypervisorPhysicalTimer", 64)
                }
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
                    BuildTimerControlRegister(new DoubleWordRegister(this), physicalTimer, "PL1PhysicalTimer")
                },
                {(long)DoubleWordRegistersAArch32.PL2PhysicalTimerControl,
                    BuildTimerControlRegister(new DoubleWordRegister(this), nonSecurePhysicalTimer, "PL2PhysicalTimer")
                },
                {(long)DoubleWordRegistersAArch32.VirtualTimerControl,
                    BuildTimerControlRegister(new DoubleWordRegister(this), virtualTimer, "VirtualTimer")
                },

                {(long)DoubleWordRegistersAArch32.PL1PhysicalTimerValue,
                    BuildTimerCountDownValueRegister(new DoubleWordRegister(this), physicalTimer, "PL1PhysicalTimer", 32)
                },
                {(long)DoubleWordRegistersAArch32.PL2PhysicalTimerValue,
                    BuildTimerCountDownValueRegister(new DoubleWordRegister(this), nonSecurePhysicalTimer, "PL2PhysicalTimer", 32)
                },
                {(long)DoubleWordRegistersAArch32.VirtualTimerValue,
                    BuildTimerCountDownValueRegister(new DoubleWordRegister(this), virtualTimer, "VirtualTimer", 32)
                }
            };
            return registersMap;
        }

        private Dictionary<long, QuadWordRegister> BuildQuadWordRegisterAArch32Map()
        {
            var registersMap = new Dictionary<long, QuadWordRegister>
            {
                {(long)QuadWordRegistersAArch32.PhysicalCount,
                    BuildTimerCountValueRegister(physicalTimer, "Physical")
                },
                {(long)QuadWordRegistersAArch32.VirtualCount,
                    BuildTimerCountValueRegister(virtualTimer, "Virtual")
                },

                {(long)QuadWordRegistersAArch32.VirtualOffset,
                    BuildTimerOffsetRegister(virtualTimer, "Virtual")
                },

                {(long)QuadWordRegistersAArch32.PL1PhysicalTimerCompareValue,
                    BuildTimerCompareValueRegister(physicalTimer, "PL1PhysicalTimer")
                },
                {(long)QuadWordRegistersAArch32.PL2PhysicalTimerCompareValue,
                    BuildTimerCompareValueRegister(nonSecurePhysicalTimer, "PL2PhysicalTimer")
                },
                {(long)QuadWordRegistersAArch32.VirtualTimerCompareValue,
                    BuildTimerCompareValueRegister(virtualTimer, "VirtualTimer")
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

        private readonly TimerUnit physicalTimer;
        private readonly TimerUnit nonSecurePhysicalTimer;
        private readonly TimerUnit virtualTimer;
        private readonly TimerUnit hypervisorPhysicalTimer;
        private readonly QuadWordRegisterCollection registersAArch64;
        private readonly DoubleWordRegisterCollection doubleWordRegistersAArch32;
        private readonly QuadWordRegisterCollection quadWordRegistersAArch32;
        private readonly IClockSource clockSource;

        private enum RegistersAArch64
        {
            // Enum values are created from op0, op1, CRn, CRm and op2 fields of the MRS instruction.
            Frequency = 0xdf00, // CNTFRQ_EL0 
            VirtualOffset = 0xe703, // CNTVOFF_EL2 
            HypervisorControl = 0xe708, // CNTHCTL_EL2 
            NonSecurePhysicalTimerControl = 0xe711, // CNTHP_CTL_EL2 
            VirtualTimerCompareValue = 0xdf1a, // CNTV_CVAL_EL0 
            VirtualCount = 0xdf02, // CNTVCT_EL0 
            VirtualTimerControl = 0xdf19, // CNTV_CTL_EL0 
            SecurePhysicalTimerControl = 0xe729, // CNTHPS_CTL_EL2 
            SecurePhysicalTimerCompareValue = 0xe72a, // CNTHPS_CVAL_EL2 
            SecurePhysicalTimerValue = 0xe728, // CNTHPS_TVAL_EL2 
            NonSecurePhysicalTimerCompareValue = 0xe712, // CNTHP_CVAL_EL2 
            NonSecurePhysicalTimerValue = 0xe710, // CNTHP_TVAL_EL2 
            SecureVirtualTimerControl = 0xe721, // CNTHVS_CTL_EL2 
            SecureVirtualTimerCompareValue = 0xe722, // CNTHVS_CVAL_EL2 
            SecureVirtualTimerValue = 0xe720, // CNTHVS_TVAL_EL2 
            NonSecureVirtualTimerControl = 0xe719, // CNTHV_CTL_EL2 
            NonSecureVirtualTimerCompareValue = 0xe71a, // CNTHV_CVAL_EL2 
            NonSecureVirtualTimerValue = 0xe718, // CNTHV_TVAL_EL2 
            KernelControl = 0xc708, // CNTKCTL_EL1 
            PhysicalSelfSynchronizedCount = 0xdf05, // CNTPCTSS_EL0 
            PhysicalCount = 0xdf01, // CNTPCT_EL0 
            PhysicalOffset = 0xe706, // CNTPOFF_EL2 
            HypervisorPhysicalTimerControl = 0xff11, // CNTPS_CTL_EL1 
            HypervisorPhysicalTimerCompareValue = 0xff12, // CNTPS_CVAL_EL1 
            HypervisorPhysicalTimerValue = 0xff10, // CNTPS_TVAL_EL1 
            PhysicalTimerControl = 0xdf11, // CNTP_CTL_EL0 
            PhysicalTimerCompareValue = 0xdf12, // CNTP_CVAL_EL0 
            PhysicalTimerValue = 0xdf10, // CNTP_TVAL_EL0 
            VirtualSelfSynchronizedCount = 0xdf06, // CNTVCTSS_EL0 
            VirtualTimerValue = 0xdf18 // CNTV_TVAL_EL0 
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
            public TimerUnit(IClockSource clockSource, IPeripheral parent, string timerName, GPIO irq, long frequency)
            {
                this.clockSource = clockSource;
                IRQ = irq;

                timer = new ComparingTimer(clockSource, frequency, parent, timerName, limit: ulong.MaxValue, compare: ulong.MaxValue, enabled: true, eventEnabled: true);
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
                IRQ.Set(InterruptStatus && !InterruptMask && InterruptEnable);
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
            private readonly ComparingTimer timer;
        }
    }
}
