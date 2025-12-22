//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public sealed class LAPIC : IAPICPeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public LAPIC(IMachine machine, int id = 0, ulong lapicTimerFrequency = 32000000)
        {
            this.messageHelper = new APICMessageHelper(this);

            Action limitReachedHandler = () =>
            {
                if(localTimerMasked.Value || !SoftwareEnabled)
                {
                    return;
                }
                lock(sync)
                {
                    AddNewInterrupt((int)localTimerVector.Value, false);
                }
            };

            lapicTimer = new LimitTimer(machine.ClockSource, lapicTimerFrequency, this, nameof(lapicTimer), direction: Direction.Descending, workMode: WorkMode.OneShot, eventEnabled: true, divider: 2);
            lapicTimer.LimitReached += limitReachedHandler;

            // timer used only in TSC Deadline mode, 1000000 is a placeholder and correct frequency (based on mips) is set every time the deadline value is set
            cpuTimer = new LimitTimer(machine.ClockSource, 1000000, this, nameof(cpuTimer), direction: Direction.Descending, workMode: WorkMode.OneShot, eventEnabled: true, divider: 1);
            cpuTimer.LimitReached += limitReachedHandler;

            hardwareEnabled = true;
            interrupts = new IRQState[AvailableVectors];
            activeIrqs = new Stack<int>();
            IRQ = new GPIO();
            sync = new object();
            physicalID = (ushort)id;

            DefineRegisters();
            Reset();
            this.machine = machine;
        }

        public void RaiseInterrupt(byte vector, bool levelTriggered)
        {
            lock(sync)
            {
                AddNewInterrupt(vector, levelTriggered);
            }
        }

        public bool CheckID(APICMessageHelper.DestinationMode mode, byte destination)
        {
            return mode == APICMessageHelper.DestinationMode.Physical ?
                CheckPhysicalID(destination) :
                CheckLogicalID(destination);
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(sync)
            {
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
            tscDeadlineMode = false;
            lapicTimer.Reset();
            cpuTimer.Reset();
            registers.Reset();
            interrupts = new IRQState[AvailableVectors];
            activeIrqs.Clear();
        }

        public void SetTscDeadlineValue(ulong value, ulong totalExecutedInstructions, uint currentMips)
        {
            if(value != 0)
            {
                cpuTimer.Enabled = false;
                cpuTimer.Frequency = currentMips * 1000000;
                cpuTimer.Limit = value - totalExecutedInstructions;
                cpuTimer.ResetValue();

                if(tscDeadlineMode)
                {
                    cpuTimer.Enabled = true;
                }
            }
        }

        public void SetApicBase(ulong value)
        {
            apicBase = value;
            HardwareEnabled = (apicBase & ApicBaseHardwareEnabledBit) != 0;
            X2Mode = (apicBase & (ApicBaseX2ModeBit | ApicBaseHardwareEnabledBit)) != 0;

            UpdateLogicalID(0);
        }

        public int GetPendingInterrupt()
        {
            lock(sync)
            {
                if(!IsIrqActive)
                {
                    // Spurious
                    return 0;
                }

                var activeIrq = activeIrqs.Peek();

                this.Log(LogLevel.Noisy, "IRQ {0} set to InService", activeIrq);
                interrupts[activeIrq] &= ~IRQState.Request;
                interrupts[activeIrq] |= IRQState.InService;

                this.Log(LogLevel.Noisy, "IRQ unset");
                IRQ.Unset();

                UpdateProcessorPriority();
                return activeIrq;
            }
        }

        public void EndOfInterrupt()
        {
            lock(sync)
            {
                if(!IsIrqActive)
                {
                    this.DebugLog("Trying to end and interrupt, but no interrupt was acknowledged.");
                    return;
                }

                var activeIrq = activeIrqs.Pop();
                interrupts[activeIrq] &= ~IRQState.InService;

                if(interrupts[activeIrq].HasFlag(IRQState.LevelTriggered))
                {
                    interrupts[activeIrq] &= ~IRQState.LevelTriggered;
                    this.Log(LogLevel.Noisy, "Broadcast EOI for vector {0} to all IOAPIC's", activeIrq);
                    messageHelper.SendEOIMessage(new APICMessageHelper.EOIMessage((byte)activeIrq));
                }

                this.Log(LogLevel.Noisy, "IRQ {0} EOI", activeIrq);
                UpdateProcessorPriority();

                // If we have some request with number higher than last element
                // on stack, we need to raise new interrupt
                var search = GetHighestInterruptInSpecificState(IRQState.Request);
                var nextIrqOnStack = activeIrqs.Count() != 0 ? activeIrqs.Peek() : -1;

                if(search == -1 || search == nextIrqOnStack)
                {
                    return;
                }

                if(nextIrqOnStack == -1)
                {
                    this.Log(LogLevel.Noisy, "IRQ {0} is the highest pending interrupt", search);
                }
                else
                {
                    this.Log(LogLevel.Noisy, "IRQ {0} is higher than {1} on stack", search, nextIrqOnStack);
                }

                activeIrqs.Push(search);
                this.Log(LogLevel.Noisy, "IRQ set");
                IRQ.Set();
            }
        }

        public APICPeripheralType APICPeripheralType => APICPeripheralType.LAPIC;

        public BaseX86 Cpu { get; set; }

        public GPIO IRQ { get; private set; }

        public long Size => 1.KB();

        public uint PhysicalID => physicalID;

        public ushort LogicalID => logicalID;

        public ushort ClusterID => clusterID;

        public ushort DestinationFormat => (ushort)destinationFormat.Value;

        public byte ProcessorPriority => processorPriority;

        public bool X2Mode { get; private set; }

        public bool SoftwareEnabled { get; private set; }

        public bool HardwareEnabled
        {
            get
            {
                return hardwareEnabled;
            }

            set
            {
                if(hardwareEnabled)
                {
                    return;
                }

                hardwareEnabled = value;
            }
        }

        private void DefineRegisters()
        {
            var addresses = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.LocalAPICId, new DoubleWordRegister(this)
                                .WithReservedBits(0, 24)
                                .WithValueField(24, 8, FieldMode.Read, valueProviderCallback: _ => (ulong)this.PhysicalID, name: "Local APIC ID")
                },
                {(long)Registers.LocalAPICVersion, new DoubleWordRegister(this, Version + (MaxLVTEntry << 16))
                                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => Version, name: "Local APIC Version")
                                .WithReservedBits(8, 8)
                                .WithValueField(16, 8, FieldMode.Read, valueProviderCallback: _ => MaxLVTEntry, name: "Max LVT Entry")
                                .WithTaggedFlag("EOI Broadcast Suppression Support", 24)
                                .WithReservedBits(25, 7)
                },
                {(long)Registers.TaskPriority, new DoubleWordRegister(this)
                                .WithValueField(0, 4, out taskPrioritySubClass, name: "Task Priority Subclass")
                                .WithValueField(4, 4, out taskPriorityClass, name: "Task Priority Class")
                                .WithReservedBits(8, 24)
                                .WithWriteCallback((_, __) => UpdateProcessorPriority())
                },
                // This value is used only in APIC Bus access order,
                // software have no influance on this process
                {(long)Registers.ArbitrationPriority, new DoubleWordRegister(this)
                                .WithTag("Arbitration Priority Subclass", 0, 4)
                                .WithTag("Arbitration Priority Class", 4, 4)
                                .WithReservedBits(8, 24)
                },
                {(long)Registers.ProcessorPriority, new DoubleWordRegister(this)
                                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => (ulong)(processorPriority & 0xF))
                                .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => (ulong)(processorPriority >> 4))
                                .WithReservedBits(8, 24)
                },
                {(long)Registers.EndOfInterrupt, new DoubleWordRegister(this)
                                .WithValueField(0, 32, name: "Send EOI Signal")
                                .WithWriteCallback((_,__) => EndOfInterrupt())
                },
                // This register was present only in xAPIC to read registers of other LAPIC
                // in x2APIC these bytes are reserved and unused
                { (long)Registers.RemoteRead, new DoubleWordRegister(this)
                                .WithReservedBits(0, 32)
                },
                {(long)Registers.LogicalDestination, new DoubleWordRegister(this)
                                .WithValueField(0, 32, name: "Logical APIC ID",
                                    writeCallback: (_, val) => UpdateLogicalID((uint)val),
                                    valueProviderCallback: _ => logicalIDRegValue)
                },
                {(long)Registers.DestinationFormat, new DoubleWordRegister(this, 0xFFFFFFFF)
                                .WithReservedBits(0, 28)
                                .WithValueField(28, 4, out destinationFormat, name: "Model")
                                .WithWriteCallback((_, __) => UpdateLogicalID(logicalIDRegValue))
                },
                {(long)Registers.SpuriousInterrupt, new DoubleWordRegister(this, 0xFF)
                                .WithTag("Spurious Interrupt Vector", 0, 8)
                                .WithFlag(8, writeCallback: (_, val) => SoftwareEnabled = val, name: "APIC Software Enable/Disable")
                                .WithTaggedFlag("Focus Processor Checking", 9)
                                .WithReservedBits(10, 2)
                                .WithTaggedFlag("EOI Broadcast Suppression", 12)
                                .WithReservedBits(13, 19)
                },
                {(long)Registers.ErrorStatus, new DoubleWordRegister(this)
                                .WithReservedBits(0, 5)
                                .WithTaggedFlag("Send Illegal Vector", 5)
                                .WithTaggedFlag("Receive Illegal Vector", 6)
                                .WithTaggedFlag("Illegal Register Access", 7)
                                .WithReservedBits(8, 24)
                },
                {(long)Registers.LocalVectorTableCMCI, new DoubleWordRegister(this)
                                .WithTag("Vector", 0, 8)
                                .WithTag("Delivery Mode", 8, 3)
                                .WithReservedBits(11, 1)
                                .WithTaggedFlag("Delivery status", 12)
                                .WithTaggedFlag("Masked", 16)
                },
                {(long)Registers.InterruptCommandLo, new DoubleWordRegister(this)
                                .WithValueField(0, 8, name: "Vector")
                                .WithValueField(8, 3, name: "Delivery Mode")
                                .WithFlag(11, name: "Destination Mode")
                                .WithTaggedFlag("Delivery Status", 12)
                                .WithReservedBits(13, 1)
                                .WithFlag(14, name: "Level")
                                .WithFlag(15, name: "Trigger Mode")
                                .WithReservedBits(16, 2)
                                .WithValueField(18, 2, out var icrDestinationShortland, name: "Destination Shorthand")
                                .WithReservedBits(20, 12)
                                .WithWriteCallback((_, val) =>
                                {
                                    messageHelper.SendShortMessage(new APICMessageHelper.ShortMessage((destination << 56) | val),
                                        (APICMessageHelper.DestinationShortland)Convert.ToInt32(icrDestinationShortland.Value));
                                })
                },
                {(long)Registers.InterruptCommandHi, new DoubleWordRegister(this)
                                .WithValueField(0, 32, name: "Destination Field", writeCallback: (_, val) => {
                                    // In x2ACPI size is extended from 8 to 32 bits
                                    destination = X2Mode ? val : val >> 24;
                                })
                },
                {(long)Registers.LocalVectorTableTimer, new DoubleWordRegister(this, 0x10000)
                                .WithValueField(0, 8, out localTimerVector, name: "Vector")
                                .WithReservedBits(8, 4)
                                .WithTaggedFlag("Delivery status", 12)  // Read-only. This should not be needed, as it is set before writing to IRR. We do not support
                                                                        // "rejecting" of interrupts, so everything is automatically accepted.
                                .WithReservedBits(13, 3)
                                .WithFlag(16, out localTimerMasked, name: "Masked")
                                .WithValueField(17, 2, name: "Timer Mode", changeCallback: (_, v) => {
                                    bool isPeriodic = (v & 0x1) == 0x1;
                                    tscDeadlineMode = (v & 0x2) == 0x2;

                                    if(tscDeadlineMode == true)
                                    {
                                        this.Log(LogLevel.Info, "Switched to TSC Deadline mode");
                                        lapicTimer.Enabled = false;

                                        if(isPeriodic)
                                        {
                                            this.Log(LogLevel.Warning, "Illegal operation - Set periodic mode when TSC Deadlne is enabled");
                                        }

                                        // TSC Deadline register value is already set
                                        if(cpuTimer.Limit != 0)
                                        {
                                            cpuTimer.Enabled = true;
                                        }
                                    }
                                    else
                                    {
                                        cpuTimer.Enabled = false;
                                        lapicTimer.Mode = isPeriodic ? WorkMode.Periodic : WorkMode.OneShot;
                                        this.Log(LogLevel.Info, "Switched to Regular mode ({0})", lapicTimer.Mode.ToString());

                                        if(isPeriodic)
                                        {
                                            lapicTimer.Enabled = true;
                                        }
                                    }
                                })
                                .WithReservedBits(19, 13)
                },
                {(long)Registers.LocalVectorTableThermal, new DoubleWordRegister(this, 0x10000)
                                .WithTag("Vector", 0, 8)
                                .WithTag("Delivery Mode", 8, 3)
                                .WithReservedBits(11, 1)
                                .WithTaggedFlag("Delivery status", 12)
                                .WithTaggedFlag("Masked", 16)
                                .WithReservedBits(17, 15)
                },
                {(long)Registers.LocalVectorTablePerformanceMonitorCounters, new DoubleWordRegister(this, 0x10000)
                                .WithTag("Vector", 0, 8)
                                .WithTag("Delivery Mode", 8, 3)
                                .WithReservedBits(11, 1)
                                .WithTaggedFlag("Delivery status", 12)
                                .WithTaggedFlag("Masked", 16)
                                .WithReservedBits(17, 15)
                },
                //These two registers are not supported despite being written to, I think they are not relevant in our setup.
                {(long)Registers.LocalVectorTableLINT0, new DoubleWordRegister(this, 0x10000)
                                .WithTag("Vector", 0, 8)
                                .WithTag("Delivery mode", 8, 3)
                                .WithReservedBits(11, 1)
                                .WithTaggedFlag("Delivery status", 12) //Read-only
                                .WithTaggedFlag("Interrupt Input Pin Polarity", 13)
                                .WithTaggedFlag("Remote IRR", 14) //Read-only
                                .WithTaggedFlag("Level triggered", 15)
                                .WithTaggedFlag("Masked", 16)
                                .WithReservedBits(17, 15)
                },
                {(long)Registers.LocalVectorTableLINT1, new DoubleWordRegister(this, 0x10000)
                                .WithTag("Interrupt Vector", 0, 8)
                                .WithTag("Delivery mode", 8, 3)
                                .WithReservedBits(11, 1)
                                .WithTaggedFlag("Delivery status", 12) //Read-only
                                .WithTaggedFlag("Interrupt Input Pin Polarity", 13)
                                .WithTaggedFlag("Remote IRR", 14) //Read-only
                                .WithTaggedFlag("Level triggered", 15)
                                .WithTaggedFlag("Masked", 16)
                                .WithReservedBits(17, 15)
                },
                {(long)Registers.LocalVectorTableError, new DoubleWordRegister(this, 0x10000)
                                .WithTag("Interrupt Vector", 0, 8)
                                .WithReservedBits(8, 4)
                                .WithTaggedFlag("Delivery status", 12)
                                .WithReservedBits(13, 3)
                                .WithTaggedFlag("Masked", 16)
                                .WithReservedBits(17, 15)
                },
                {(long)Registers.LocalVectorTableTimerInitialCount, new DoubleWordRegister(this)
                                .WithValueField(0, 32, name: "Initial Count Value", writeCallback: (_, val) =>
                                {
                                    this.Log(LogLevel.Info, "Setting local timer initial value to {0}", val);
                                    lapicTimer.Limit = val;
                                    lapicTimer.ResetValue();

                                    if(!tscDeadlineMode)
                                    {
                                        lapicTimer.Enabled = true;
                                    }
                                })
                },
                {(long)Registers.LocalVectorTableTimerCurrentCount, new DoubleWordRegister(this)
                                .WithValueField(0, 32, FieldMode.Read, name: "Current Count Value", valueProviderCallback: _ => (uint)lapicTimer.Value)
                },
                {(long)Registers.LocalVectorTableTimerDivideConfig, new DoubleWordRegister(this)
                                .WithValueField(0, 4, name: "Divide Value", writeCallback: (_, val) =>
                                {
                                    switch(val)
                                    {
                                    case 0x0:
                                        lapicTimer.Divider = 2;
                                        break;
                                    case 0x1:
                                        lapicTimer.Divider = 4;
                                        break;
                                    case 0x2:
                                        lapicTimer.Divider = 8;
                                        break;
                                    case 0x3:
                                        lapicTimer.Divider = 16;
                                        break;
                                    case 0x8:
                                        lapicTimer.Divider = 32;
                                        break;
                                    case 0x9:
                                        lapicTimer.Divider = 64;
                                        break;
                                    case 0xA:
                                        lapicTimer.Divider = 128;
                                        break;
                                    case 0xB:
                                        lapicTimer.Divider = 1;
                                        break;
                                    default:
                                        this.Log(LogLevel.Warning, "Setting unsupported divider value: 0x{0:x}", val);
                                        return;
                                    }

                                    this.Log(LogLevel.Info, "Divider set to {0}", lapicTimer.Divider);
                                })
                                .WithReservedBits(4, 28)
                }
            };

            // Setup 256 bit registers
            for(int i = 0; i < 8; i++)
            {
                int bitsFrom = i * 32;
                int bitsTo = bitsFrom + 32;

                var isrN = new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(interrupts.Skip(bitsFrom).Take(32).Select(x => x.HasFlag(IRQState.InService))),
                        name: $"ISR {bitsFrom}:{bitsTo}"
                    );

                var tmrN = new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(interrupts.Skip(bitsFrom).Take(32).Select(x => x.HasFlag(IRQState.LevelTriggered))),
                        name: $"TMR {bitsFrom}:{bitsTo}"
                    );

                var irrN = new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(interrupts.Skip(bitsFrom).Take(32).Select(x => x.HasFlag(IRQState.Request))),
                        name: $"IRR {bitsFrom}:{bitsTo}"
                    );

                // Each register is logically 256 bit, but in address 
                // space there are 8 registers (with gap = 0x10)
                int offset = i * 0x10;

                addresses.Add((long)Registers.InService0 + offset, isrN);
                addresses.Add((long)Registers.TriggerMode0 + offset, tmrN);
                addresses.Add((long)Registers.InterruptRequest0 + offset, irrN);
            }

            registers = new DoubleWordRegisterCollection(this, addresses);
        }

        private bool AddNewInterrupt(int number, bool isLevelTriggered)
        {
            if(interrupts[number].HasFlag(IRQState.Request))
            {
                return false;
            }

            this.Log(LogLevel.Noisy, "IRQ {0} set to Request", number);
            interrupts[number] |= isLevelTriggered ?
                IRQState.Request | IRQState.LevelTriggered :
                IRQState.Request;

            if(IsIrqActive && number <= activeIrqs.Peek())
            {
                return true;
            }

            this.Log(LogLevel.Noisy, "IRQ set");
            activeIrqs.Push(number);
            IRQ.Set();

            return true;
        }

        private bool CheckPhysicalID(byte destination)
        {
            return PhysicalID == destination;
        }

        private bool CheckLogicalID(byte destination)
        {
            // In x2APIC there is only cluster format
            if(X2Mode || DestinationFormat == 0x00)
            {
                byte msgClusterID = (byte)(destination >> 4);
                byte msgLogicalMask = (byte)(destination & 0xF);

                // 0xF is special value in cluster format - 
                // it's broadcast to all clusters

                return (msgClusterID == ClusterID || msgClusterID == 0xF) &&
                        (msgLogicalMask & LogicalID) != 0;
            }

            // For xAPIC mode there are only two legal values 0x00 or 0xFF
            if(DestinationFormat != 0xFF)
            {
                this.Log(LogLevel.Warning, "Unkown destination format value: {0}", DestinationFormat);
                return false;
            }

            // Handle Flat format - xAPIC only
            return (destination & LogicalID) != 0;
        }

        private void UpdateLogicalID(uint newValue)
        {
            if(X2Mode)
            {
                logicalID = (ushort)(physicalID & 0xF);
                clusterID = (ushort)(physicalID >> 4);
                logicalIDRegValue = (((uint)clusterID << 16) | logicalID);
                return;
            }

            logicalIDRegValue = newValue;

            if(destinationFormat.Value == 0xFF)
            {
                // Flat model
                logicalID = (ushort)(logicalIDRegValue >> 24);
                clusterID = 0;
            }
            else if(destinationFormat.Value == 0x00)
            {
                // Cluster mode
                logicalID = (ushort)((logicalIDRegValue >> 24) & 0xF);
                clusterID = (ushort)(logicalIDRegValue >> 28);
            }
            else
            {
                this.Log(LogLevel.Warning, "Unknown destination format value {0}", destinationFormat.Value);
            }
        }

        private int GetHighestInterruptInSpecificState(IRQState requestedState)
        {
            for(int irq = interrupts.Length - 1; irq >= 0; irq--)
            {
                if(interrupts[irq].HasFlag(requestedState))
                {
                    return irq;
                }
            }

            return -1;
        }

        private void UpdateProcessorPriority()
        {
            var search = GetHighestInterruptInSpecificState(IRQState.InService);
            var highestInterruptInService = search != -1 ? search : 0;

            var interruptClass = highestInterruptInService >> 4;

            processorPriority = (int)taskPriorityClass.Value > interruptClass ?
                (byte)(taskPriorityClass.Value << 4 | taskPrioritySubClass.Value) :
                (byte)(interruptClass << 4);
        }

        private bool IsIrqActive => activeIrqs.Count() != 0;

        private byte TaskPriority => (byte)(taskPriorityClass.Value << 4 | taskPrioritySubClass.Value);

        private byte processorPriority;
        private bool hardwareEnabled;
        private bool tscDeadlineMode;
        private ulong destination;
        private ulong apicBase;
        private uint logicalIDRegValue;
        private ushort clusterID;
        private ushort logicalID;

        private IFlagRegisterField localTimerMasked;
        private IValueRegisterField localTimerVector;
        private IValueRegisterField destinationFormat;
        private IValueRegisterField taskPriorityClass;
        private IValueRegisterField taskPrioritySubClass;

        private IRQState[] interrupts;
        private DoubleWordRegisterCollection registers;
        private readonly Stack<int> activeIrqs;

        private readonly object sync;
        private readonly IMachine machine;

        private readonly LimitTimer lapicTimer;
        private readonly LimitTimer cpuTimer;
        private readonly APICMessageHelper messageHelper;
        private readonly ushort physicalID;

        private const int AvailableVectors = 256;
        private const uint Version = 0x10; //1x means local apic, x is model specific
        private const uint MaxLVTEntry = 3; //lowest possible? for pentium
        private const ulong ApicBaseHardwareEnabledBit = (1 << 11);
        private const ulong ApicBaseX2ModeBit = (1 << 10);

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
            InService0 = 0x100,
            InService1 = 0x110,
            InService2 = 0x120,
            InService3 = 0x130,
            InService4 = 0x140,
            InService5 = 0x150,
            InService6 = 0x160,
            InService7 = 0x170,
            TriggerMode0 = 0x180,
            TriggerMode1 = 0x190,
            TriggerMode2 = 0x1A0,
            TriggerMode3 = 0x1B0,
            TriggerMode4 = 0x1C0,
            TriggerMode5 = 0x1D0,
            TriggerMode6 = 0x1E0,
            TriggerMode7 = 0x1F0,
            InterruptRequest0 = 0x200,
            InterruptRequest1 = 0x210,
            InterruptRequest2 = 0x220,
            InterruptRequest3 = 0x230,
            InterruptRequest4 = 0x240,
            InterruptRequest5 = 0x250,
            InterruptRequest6 = 0x260,
            InterruptRequest7 = 0x270,
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

        [Flags]
        private enum IRQState
        {
            None = 1,
            Request = 2,
            InService = 4,
            LevelTriggered = 8
        }
    }
}