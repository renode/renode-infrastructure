//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;
using System;

//TODO: Priorities are handled not as in the docs, higher vector wins.
namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public sealed class LAPIC : IDoubleWordPeripheral, IIRQController, IKnownSize
    {
        public LAPIC(IMachine machine, int id = 0)
        {
            // frequency guessed from driver and zephyr code
            localTimer = new LimitTimer(machine.ClockSource, 32000000, this, nameof(localTimer), direction: Direction.Descending, workMode: WorkMode.OneShot, eventEnabled: true, divider: 2);
            localTimer.LimitReached += () =>
            {
                if(localTimerMasked.Value || !lapicEnabled.Value)
                {
                    return;
                }
                lock(sync)
                {
                    interrupts[(int)localTimerVector.Value] |= IRQState.Pending;
                    FindPendingInterrupt();
                }
            };
            IRQ = new GPIO();
            ID = id;
            DefineRegisters();
            Reset();
            this.machine = machine;
        }

        public void OnGPIO(int number, bool value)
        {
            lock(sync)
            {
                if(value)
                {
                    if(lapicEnabled.Value) //according to 10.4.7.2
                    {
                        this.Log(LogLevel.Noisy, "Received an interrupt vector {0}.", number);
                        // It is possible to have the same vector active and pending. We latch it whenever there is a change in Running.
                        if((interrupts[number] & IRQState.Running) == 0)
                        {
                            interrupts[number] |= IRQState.Pending;
                        }
                    }
                    interrupts[number] |= IRQState.Running;
                }
                else
                {
                    interrupts[number] &= ~IRQState.Running;
                }
                FindPendingInterrupt();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(sync)
            {
                int regNumber;
                if(offset >= 0x100 && offset < 0x280)
                {
                    IRQState flagToCheck;
                    if(offset < 0x180) //InService
                    {
                        offset -= 0x100;
                        flagToCheck = IRQState.Active;
                    }
                    else if(offset < 0x200) //TriggerMode
                    {
                        offset -= 0x180;
                        flagToCheck = IRQState.TriggerModeIndicator;
                    }
                    else //InterruptRequest
                    {
                        offset -= 0x200;
                        flagToCheck = IRQState.Pending;
                    }
                    regNumber = (int)offset / 0x10;
                    return BitHelper.GetValueFromBitsArray(interrupts.Skip(regNumber * 32).Take(32).Select(x => (x & flagToCheck) != 0));
                }
                return registers.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(sync)
            {
                registers.Write(offset, value);
            }
        }

        public void Reset()
        {
            localTimer.Reset();
            registers.Reset();
            interrupts = new IRQState[availableVectors];
            activeIrqs.Clear();
        }

        public int GetPendingInterrupt()
        {
            lock(sync)
            {
                var result = FindPendingInterrupt();
                if(result != -1)
                {
                    interrupts[result] |= IRQState.Active;
                    interrupts[result] &= ~IRQState.Pending;
                    this.NoisyLog("Acknowledged IRQ {0}.", result);
                    activeIrqs.Push(result);
                    IRQ.Unset();
                    return result;
                }
                this.Log(LogLevel.Warning, "Trying to acknowledge an interrupt, but there is nothing to acknowledge!");
                // We should probably handle spurious vector here
                return 0;
            }
        }

        public long Size
        {
            get
            {
                return 1.KB();
            }
        }

        public GPIO IRQ { get; private set; }

        public int ID { get; }

        private void DefineRegisters()
        {
            var addresses = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.LocalAPICId, new DoubleWordRegister(this)
                                .WithReservedBits(0, 24)
                                .WithValueField(24, 8, FieldMode.Read, valueProviderCallback: _ => (ulong)this.ID)
                },
                {(long)Registers.LocalAPICVersion, new DoubleWordRegister(this, Version + (MaxLVTEntry << 16))
                                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => Version)
                                .WithValueField(16, 8, FieldMode.Read, valueProviderCallback: _ => MaxLVTEntry)
                },
                {(long)Registers.LocalVectorTableThermal, new DoubleWordRegister(this, 0x10000)
                                .WithTag("Vector", 0, 8)
                                .WithTag("Delivery Mode", 8, 3)
                                .WithTag("Delivery status", 12, 1)
                                .WithTag("Masked", 16, 1)
                },
                {(long)Registers.EndOfInterrupt, new DoubleWordRegister(this).WithWriteCallback((_,__) => EndOfInterrupt())
                },
                {(long)Registers.SpuriousInterrupt, new DoubleWordRegister(this, 0xFF)
                                .WithTag("Spurious Vector, older bits", 4, 4)
                                .WithFlag(8, out lapicEnabled, changeCallback: ApicEnabledChanged, name: "APIC S/W enable/disable")
                },
                {(long)Registers.InterruptCommandLo, new DoubleWordRegister(this)
                                .WithTag("Vector", 0, 8)
                                .WithValueField(8, 3, out var deliveryMode, name: "Delivery Mode")
                                .WithTaggedFlag("Destination Mode", 11)
                                .WithReservedBits(12, 2)
                                .WithTaggedFlag("Level", 14)
                                .WithTaggedFlag("Trigger Mode", 15)
                                .WithReservedBits(16, 2)
                                .WithTag("Destination Shorthand", 18, 2)
                                .WithReservedBits(20, 12)
                                .WithWriteCallback((_,__) =>
                                {
                                    switch((LocalVectorTableDeliveryMode)deliveryMode.Value)
                                    {
                                        case LocalVectorTableDeliveryMode.SIPI:
                                            // Find the CPU of LAPIC with specified ID
                                            var cpu = machine.SystemBus.GetCPUs().OfType<BaseX86>()
                                                .FirstOrDefault(x => (ulong)x.Lapic.ID == destination.Value);

                                            if(cpu == null)
                                            {
                                                this.WarningLog("There is no cpu having LAPIC with id {0}. Not sending IPI", destination.Value);
                                            }
                                            else
                                            {
                                                this.InfoLog("Unhalting cpu having LAPIC with id {0}", destination.Value);
                                                cpu.IsHalted = false;
                                            }
                                            break;

                                        default:
                                            this.WarningLog("Received unsupported delivery mode value: {0}", deliveryMode.Value);
                                            break;
                                    }
                                })
                },
                {(long)Registers.InterruptCommandHi, new DoubleWordRegister(this)
                                .WithValueField(0, 32, out destination, name: "Destination Field")
                },
                {(long)Registers.LocalVectorTablePerformanceMonitorCounters, new DoubleWordRegister(this, 0x10000)
                                .WithTag("Vector", 0, 8)
                                .WithTag("Delivery Mode", 8, 3)
                                .WithTag("Delivery status", 12, 1)
                                .WithTag("Masked", 16, 1)
                },
                {(long)Registers.LocalVectorTableTimer, new DoubleWordRegister(this, 0x10000)
                                .WithValueField(0, 8, out localTimerVector, name: "Vector")
                                .WithTag("Delivery status", 12, 1) // Read-only. This should not be needed, as it is set before writing to IRR. We do not support
                                                                   // "rejecting" of interrupts, so everything is automatically accepted.
                                .WithFlag(16, out localTimerMasked, name: "Masked")
                                .WithFlag(17, name: "Periodic", changeCallback: (_, v) =>
                                {
                                    localTimer.Mode = v ? WorkMode.Periodic : WorkMode.OneShot;
                                    if(v)
                                    {
                                        localTimer.Enabled = true;
                                    }
                                    this.Log(LogLevel.Info, "Local timer mode set to {0}", localTimer.Mode);
                                })
                },
                //These two registers are not supported despite being written to, I think they are not relevant in our setup.
                {(long)Registers.LocalVectorTableLINT0, new DoubleWordRegister(this, 0x10000)
                                .WithTag("Vector", 0, 8)
                                .WithTag("Delivery mode", 8, 3)
                                .WithTag("Delivery status", 12, 1) //Read-only
                                .WithTag("Interrupt Input Pin Polarity", 13, 1)
                                .WithTag("Remote IRR", 14, 1) //Read-only
                                .WithTag("Level triggered", 15, 1)
                                .WithTag("Masked", 16, 1)
                },
                {(long)Registers.LocalVectorTableLINT1, new DoubleWordRegister(this, 0x10000)
                                .WithTag("Vector", 0, 8)
                                .WithTag("Delivery mode", 8, 3)
                                .WithTag("Delivery status", 12, 1) //Read-only
                                .WithTag("Interrupt Input Pin Polarity", 13, 1)
                                .WithTag("Remote IRR", 14, 1) //Read-only
                                .WithTag("Level triggered", 15, 1)
                                .WithTag("Masked", 16, 1)
                },
                {(long)Registers.LocalVectorTableError, new DoubleWordRegister(this, 0x10000)
                                .WithTag("Vector", 0, 8)
                                .WithTag("Delivery status", 12, 1)
                                .WithTag("Masked", 16, 1)
                },
                {(long)Registers.LocalVectorTableTimerInitialCount, new DoubleWordRegister(this)
                                .WithValueField(0, 32, name: "Initial Count Value", writeCallback: (_, val) =>
                                {
                                    this.Log(LogLevel.Info, "Setting local timer initial value to {0}", val);
                                    localTimer.Limit = val;
                                    localTimer.ResetValue();
                                    localTimer.Enabled = true;
                                })
                },
                {(long)Registers.LocalVectorTableTimerCurrentCount, new DoubleWordRegister(this)
                                .WithValueField(0, 32, FieldMode.Read, name: "Current Count Value", valueProviderCallback: _ => (uint)localTimer.Value)
                },
                {(long)Registers.LocalVectorTableTimerDivideConfig, new DoubleWordRegister(this)
                                .WithValueField(0, 4, name: "Divide Value", writeCallback: (_, val) =>
                                {
                                    switch(val)
                                    {
                                    case 0x0:
                                        localTimer.Divider = 2;
                                        break;
                                    case 0x1:
                                        localTimer.Divider = 4;
                                        break;
                                    case 0x2:
                                        localTimer.Divider = 8;
                                        break;
                                    case 0x3:
                                        localTimer.Divider = 16;
                                        break;
                                    case 0x8:
                                        localTimer.Divider = 32;
                                        break;
                                    case 0x9:
                                        localTimer.Divider = 64;
                                        break;
                                    case 0xA:
                                        localTimer.Divider = 128;
                                        break;
                                    case 0xB:
                                        localTimer.Divider = 1;
                                        break;
                                    default:
                                        this.Log(LogLevel.Warning, "Setting unsupported divider value: 0x{0:x}", val);
                                        return;
                                    }

                                    this.Log(LogLevel.Info, "Divider set to {0}", localTimer.Divider);
                                })
                }
            };
            registers = new DoubleWordRegisterCollection(this, addresses);
        }

        public void EndOfInterrupt()
        {
            lock(sync)
            {
                if(activeIrqs.Count() == 0)
                {
                    this.DebugLog("Trying to end and interrupt, but no interrupt was acknowledged.");
                }
                else
                {
                    var activeIRQ = activeIrqs.Pop();
                    interrupts[activeIRQ] &= ~IRQState.Active;
                    if((interrupts[activeIRQ] & IRQState.Running) > 0)
                    {
                        this.NoisyLog("Completed IRQ {0} active -> pending.", activeIRQ);
                        interrupts[activeIRQ] |= IRQState.Pending;
                    }
                    else
                    {
                        this.NoisyLog("Completed IRQ {0} active -> inactive.", activeIRQ);
                    }
                }
                FindPendingInterrupt();
            }
        }

        public uint InternalTimerVector { get => (uint)localTimerVector.Value; }

        private int FindPendingInterrupt()
        {
            var result = -1;
            var preemptionNeeded = activeIrqs.Count != 0;

            for(var i = interrupts.Length - 1; i >= 0; i--)
            {
                if((interrupts[i] & IRQState.Pending) != 0)
                {
                    result = i;
                    break;
                }
            }
            if(result != -1 && (!preemptionNeeded || activeIrqs.Peek() < result))
            {
                IRQ.Set();
            }
            return result;
        }

        private void ApicEnabledChanged(bool oldValue, bool newValue)
        {
            if(!newValue)
            {
                localTimerMasked.Value = true;
            } // not enabling otherwise, as we don't reenable timer just because we started LAPIC
        }

        private LimitTimer localTimer;

        private DoubleWordRegisterCollection registers;
        private IFlagRegisterField localTimerMasked;
        private IValueRegisterField localTimerVector;
        private IValueRegisterField destination;
        private IFlagRegisterField lapicEnabled;

        private readonly object sync = new object();
        private readonly IMachine machine;

        private IRQState[] interrupts = new IRQState[availableVectors];
        private Stack<int> activeIrqs = new Stack<int>();

        private const int availableVectors = 256;
        private const uint Version = 0x10; //1x means local apic, x is model specific
        private const uint MaxLVTEntry = 3; //lowest possible? for pentium

        [Flags]
        private enum IRQState
        {
            Running = 1,
            Pending = 2,
            Active = 4,
            TriggerModeIndicator = 8, //currently unused
        }

        private enum LocalVectorTableDeliveryMode
        {
            Fixed = 0,
            SMI = 2,
            NMI = 4,
            INIT = 5,
            SIPI = 6,
            ExtINT = 7
            //other values are reserved
        }

        public enum Registers
        {
            LocalAPICId = 0x20,
            LocalAPICVersion = 0x30,
            TaskPriority = 0x80,
            ArbitrationPriority = 0x90,
            ProcessorPriority = 0xa0,
            EndOfInterrupt = 0xb0,
            RemoteRead = 0xc0,
            LogicalDestination = 0xd0,
            DestinationFormat = 0xe0,
            SpuriousInterrupt = 0xf0,
            InService = 0x100,
            TriggerMode = 0x180,
            InterruptRequest = 0x200,
            ErrorStatus = 0x280,
            LocalVectorTableCMCI = 0x2F0,
            InterruptCommandLo = 0x300,
            InterruptCommandHi = 0x310,
            LocalVectorTableTimer = 0x320,
            LocalVectorTableThermal = 0x330,
            LocalVectorTablePerformanceMonitorCounters = 0x340,
            LocalVectorTableLINT0 = 0x350,
            LocalVectorTableLINT1 = 0x360,
            LocalVectorTableError = 0x370,
            LocalVectorTableTimerInitialCount = 0x380,
            LocalVectorTableTimerCurrentCount = 0x390,
            LocalVectorTableTimerDivideConfig = 0x3e0
        }
    }
}
