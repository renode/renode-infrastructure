//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class GaislerMIC: IDoubleWordPeripheral, INumberedGPIOOutput, IIRQController, IGaislerAPB
    {
        public GaislerMIC(IMachine machine, uint totalNumberCPUs = 1)
        {
            this.numberOfProcessors = totalNumberCPUs;
            if(totalNumberCPUs > maxNumberOfProcessors)
            {
                this.Log(LogLevel.Warning, "Registration with unsupported  number of CPUs, defaulting to maximum {0:X]", maxNumberOfProcessors);
                this.numberOfProcessors = maxNumberOfProcessors;
            }
            registers = new deviceRegisters();
            registers.MultiprocessorStatus |= (((numberOfProcessors-1) << 28) & 0xF0000000);
            // Set Broadcast Available bit in MultiprocessorStatus register if ncpu > 1
            if(this.numberOfProcessors > 1)
            {
                registers.MultiprocessorStatus |= (1u << 27);
            }
            irqs = new GPIO[numberOfProcessors];
            resets = new GPIO[numberOfProcessors];
            runs = new GPIO[numberOfProcessors];
            set_nmi_interrupt = new bool[numberOfProcessors];
            for(var i = 0; i < numberOfProcessors; i++)
            {
                irqs[i] = new GPIO();
                resets[i] = new GPIO();
                runs[i] = new GPIO();
                interrupts[i] = new Dictionary<int, int>();
                set_nmi_interrupt[i] = false;
            }

            Connections = new IGPIORedirector((int)numberOfProcessors, HandleIRQConnect);
            Reset();
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }

        private void HandleIRQConnect(int src, IGPIOReceiver receiver, int dst)
        {
            switch(dst)
            {
            case 0:
                irqs[src].Connect(receiver, dst);
                break;
            case 1:
                resets[src].Connect(receiver, dst);
                break;
            case 2:
                runs[src].Connect(receiver, dst);
                break;
            default:
                this.Log(LogLevel.Warning, "Destination index value is undefined {0:X}", dst);
                break;
            }
        }

        #region IDoubleWordPeripheral implementation
        public uint ReadDoubleWord (long offset)
        {
            if(offset < (int)(registerOffset.ProcessorInterruptMaskBase))
            {
                switch((registerOffset)offset)
                {
                case registerOffset.InterruptLevel:
                    return registers.InterruptLevel;
                case registerOffset.InterruptPending:
                    return registers.InterruptPending;
                case registerOffset.InterruptForce:
                    return 0;
                case registerOffset.InterruptClear:
                    return 0;
                case registerOffset.MultiprocessorStatus:
                    return registers.MultiprocessorStatus;
                case registerOffset.Broadcast:
                    if(isBroadcastEnabled())
                    {
                        return registers.Broadcast;
                    }
                    else
                    {
                        this.LogUnhandledRead(offset);
                        return 0;
                    }
                default:
                    this.LogUnhandledRead(offset);
                    return 0;
                }
            }
            else if(offset < (int)(registerOffset.ProcessorInterruptForceBase))
            {
                for(var i = 0; i < numberOfProcessors; i++)
                {
                    if(offset == (int)(registerOffset.ProcessorInterruptMaskBase) + 4 * i)
                    {
                        return registers.ProcessorInterruptMask[i];
                    }
                }
                this.LogUnhandledRead(offset);
                return 0;
            }
            else if(offset < (int)(registerOffset.ProcessorExtendedInterruptAcknowledgeBase))
            {
                for(var i = 0; i < numberOfProcessors; i++)
                {
                    if(offset == (int)(registerOffset.ProcessorInterruptForceBase) + 4 * i)
                    {
                        return registers.ProcessorInterruptForce[i];
                    }
                }
                this.LogUnhandledRead(offset);
                return 0;
            }
            else if(offset < (int)(registerOffset.ProcessorExtendedInterruptAcknowledgeBase) + 4 * maxNumberOfProcessors)
            {
                for(var i = 0; i < numberOfProcessors; i++)
                {
                    if(offset == (int)(registerOffset.ProcessorExtendedInterruptAcknowledgeBase) + 4 * i)
                    {
                        return registers.ProcessorExtendedInterruptAcknowledge[i];
                    }
                }
                this.LogUnhandledRead(offset);
                return 0;
            }
            else
            {
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord (long offset, uint value)
        {
            if(offset < (int)(registerOffset.ProcessorInterruptMaskBase))
            {
                switch((registerOffset)offset)
                {
                case registerOffset.InterruptLevel:
                    // Each interrupt can be assigned to one of two levels (0 or 1) as programmed in
                    // the interrupt level register - bit 1-15. Level 1 has higher priority than level 0.
                    if(value < 0xFFFF)
                    {
                        registers.InterruptLevel = value;
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "Write of unsupported interrupt level value {0:X}", value);
                    }
                    break;
                case registerOffset.InterruptPending:
                    // read-only register
                    this.Log(LogLevel.Warning, "Write to read-only register (InterruptPending) value {0:X}", value);
                    break;
                case registerOffset.InterruptForce:
                    if(currentNumberCpus() == 1)
                    {
                        registers.InterruptPending |= (value & registers.ProcessorInterruptMask[0]);
                    }
                    else
                    {
                        this.LogUnhandledWrite(offset, value);
                    }
                    break;
                case registerOffset.InterruptClear:
                    // Don't clear interrupts whose input is set
                    registers.InterruptPending &= ~(value & ~pirqState);
                    break;
                case registerOffset.MultiprocessorStatus:
                    // A halted processor can be reset and restarted by writing a ‘1’ to its status field. Bit field = [15:0]
                    if((value & 0xF) != 0)
                    {
                        for(var i = 0; i < numberOfProcessors; i++)
                        {
                            // Check if CPU is halted and then if it is requested to reset
                            if((( ~(registers.MultiprocessorStatus >> i) & 0x1) == 0x1)
                                && (((value >> i) & 0x1) == 0x1))
                            {
                                    resets[i].Set();
                                    runs[i].Set();
                            }
                        }
                    }
                    // Make setting a bit sticky
                    registers.MultiprocessorStatus |= value;
                    break;
                case registerOffset.Broadcast:
                    if(isBroadcastEnabled())
                    {
                        registers.Broadcast = value;
                    }
                    break;
                default:
                    this.LogUnhandledWrite(offset, value);
                    break;
                }
            }
            else if(offset < (int)(registerOffset.ProcessorInterruptForceBase))
            {
                for(var i = 0; i < numberOfProcessors; i++)
                {
                    // TODO: Interrupt 15 cannot be masked, should be handled here
                    if(offset == (int)(registerOffset.ProcessorInterruptMaskBase) + 4 * i)
                    {
                        registers.ProcessorInterruptMask[i] = value;
                    }
                }
            }
            else if(offset < (int)(registerOffset.ProcessorExtendedInterruptAcknowledgeBase))
            {
                int cpuid = (int)(offset - (int)(registerOffset.ProcessorInterruptForceBase))/4;

                // Loop over the external interrupts in 'value' and if set insert them into the
                // cpu's interrupt force register and the cpu's pending interrupt list. Extended
                // interrupts (see VHDL generic eirq in the GRLIB IP Core Manual) are not dealt
                // with at the moment.
                for (int interrupt = 1; interrupt < maxNumberOfExternalInterrupts; interrupt++)
                {
                    uint interrupt_mask = (1u << interrupt);
                    if ((value & interrupt_mask) != 0x0) {
                        if((interrupt_mask & registers.ProcessorInterruptMask[cpuid]) != 0)
                        {
                            lock(interrupts[cpuid])
                            {
                                registers.ProcessorInterruptForce[cpuid] |= interrupt_mask;
                                addPendingInterrupt(cpuid, interrupt);
                                if (interrupt == NMI_IRQ)
                                {
                                    set_nmi_interrupt[cpuid] = true;
                                }
                            }
                        }
                    }
                }
            }
            else if(offset < (int)(registerOffset.ProcessorExtendedInterruptAcknowledgeBase) + 4 * maxNumberOfProcessors)
            {
                this.Log(LogLevel.Warning, "Write to read-only register (ProcessorExtendedInterruptAcknowledge) value {0:X}", value);
            }
            else
            {
                this.LogUnhandledWrite(offset, value);
            }
            this.forwardInterrupt();
        }
        #endregion

        #region IPeripheral implementation
        public void Reset ()
        {
            for(var i = 0; i < numberOfProcessors; i++)
            {
                registers.ProcessorInterruptMask[i] = 0;
                registers.ProcessorInterruptForce[i] = 0;
            }
            pirqState = 0;
        }
        #endregion

        #region IGPIOReceiver implementation
        public void OnGPIO(int number, bool value)
        {
            int i;
            uint pendingInterrupts = 0;
            uint processorPendingInterrupts = 0;

            if(value)
            {
                pirqState |= (1u << number);
                pendingInterrupts |= (1u << number);
                // If interrupt is enabled in Broadcast register use cpu Force registers instead of global Pending
                if(isBroadcastEnabled() && ((registers.Broadcast & pendingInterrupts) != 0))
                {
                    for(i = 0; i < numberOfProcessors; i++)
                    {
                        processorPendingInterrupts = pendingInterrupts & registers.ProcessorInterruptMask[i];
                        if(processorPendingInterrupts != 0)
                        {
                            lock(interrupts[i])
                            {
                                registers.ProcessorInterruptForce[i] |= processorPendingInterrupts;
                                addPendingInterrupt(i, number);
                                if (number == NMI_IRQ)
                                {
                                    set_nmi_interrupt[i] = true;
                                }
                            }
                        }
                    }
                }
                else
                {
                    for(i = 0; i < numberOfProcessors; i++)
                    {
                        processorPendingInterrupts = pendingInterrupts & registers.ProcessorInterruptMask[i];
                        if(processorPendingInterrupts != 0)
                        {
                            lock(interrupts[i])
                            {
                                registers.InterruptPending |= processorPendingInterrupts;
                                addPendingInterrupt(i, number);
                                if (number == NMI_IRQ)
                                {
                                    set_nmi_interrupt[i] = true;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                pirqState &= ~(1u << number);
            }

            this.forwardInterrupt();
        }
        #endregion

        #region IGaislerAPB implementation
        public uint GetVendorID ()
        {
            return vendorID;
        }

        public uint GetDeviceID ()
        {
            return deviceID;
        }

        public GaislerAPBPlugAndPlayRecord.SpaceType GetSpaceType ()
        {
            return spaceType;
        }

        public uint GetInterruptNumber()
        {
            return this.GetCpuInterruptNumber(irqs[0]);
        }
        #endregion

        public uint GetNumberOfProcessors()
        {
            return this.numberOfProcessors;
        }

        public GPIO GetCurrentCpuIrq (int index)
        {
            GPIO currentCpuIrq = null;
            if(index < numberOfProcessors)
            {
                currentCpuIrq = irqs[index];
            }
            else
            {
                this.NoisyLog("Current IRQ array index is out of range {0:X}.", index);
            }
            return currentCpuIrq;
        }

        private void forwardInterrupt()
        {
            for(var i = 0; i < numberOfProcessors; i++)
            {
                lock(interrupts[i])
                {
                    // If broadcast is set for an irq send this to each CPU
                    if(isBroadcastEnabled())
                    {
                        if((!irqs[i].IsSet) && (registers.ProcessorInterruptForce[i] != 0))
                        {
                            irqs[i].Set();
                        }
                    }
                    if((!irqs[i].IsSet) && (registers.InterruptPending & registers.ProcessorInterruptMask[i]) != 0)
                    {
                        irqs[i].Set();
                    }

                    // Always forward the NMI interrupt, even if the cpu is already servicing
                    // another interrupt. Not doing this when running an SMP Linux kernel will
                    // result in a deadlock in the kernels cpu cross call mechanism.
                    if(set_nmi_interrupt[i])
                    {
                        irqs[i].Unset();
                        irqs[i].Set();
                        set_nmi_interrupt[i] = false;
                    }
                }
            }
        }

        // Needs to be (interrupts[i]) locked from caller
        private void forwardInterruptSingleCPU(int cpuid)
        {
            // If broadcast is set for an irq send this to each CPU
            if(isBroadcastEnabled())
            {
                if((!irqs[cpuid].IsSet) && (registers.ProcessorInterruptForce[cpuid] != 0))
                {
                    irqs[cpuid].Set();
                }
            }
            if((!irqs[cpuid].IsSet) && (registers.InterruptPending & registers.ProcessorInterruptMask[cpuid]) != 0)
            {
                irqs[cpuid].Set();
            }
        }

        public int CPUGetInterrupt(int cpuid)
        {
            lock(interrupts[cpuid])
            {
                if(interrupts[cpuid].Any())
                {
                    // Find interrupt with highest priority for this CPU
                    var interrupt = interrupts[cpuid].OrderByDescending(x => x.Value).First().Key;
                    // Treat all extended interrupts as interrupt #1
                    if(IsExtendedInterruptNumber(interrupt))
                    {
                        interrupt = 1;
                    }
                    // As the irq no is external, we have to add 0x10
                    var intNo = interrupt + 0x10;
                    return intNo;
                }
            }
            return 0;
        }

        // When a processor acknowledges the interrupt, the corresponding pending bit will automatically be
        // cleared. Interrupt can also be forced by setting a bit in the interrupt force register.
        // In this case, the processor acknowledgement will clear the force bit rather than the pending bit.
        public void CPUAckInterrupt(int cpuid, int interruptNumber)
        {
            // Have to subtract 0x10 as the irq is external
            var realInterruptNumber = interruptNumber - 0x10;

            lock(interrupts[cpuid])
            {
                // Handle extended interrupts, which are all mapped to IRQ 1
                if(realInterruptNumber == 1)
                {
                    // Extended interrupts have no prioritization between individual interrupts
                    var extendedInt = interrupts[cpuid].Keys.FirstOrDefault(IsExtendedInterruptNumber);
                    if(extendedInt == 0)
                    {
                        // This is unusual, but supported by the documentation
                        this.DebugLog("Interrupt #1 acknowledged without a pending extended interrupt.");
                        registers.ProcessorExtendedInterruptAcknowledge[cpuid] = 0; // Clear PEXTACK register
                    }
                    else
                    {
                        realInterruptNumber = extendedInt;
                        registers.ProcessorExtendedInterruptAcknowledge[cpuid] = (uint)extendedInt;
                    }
                }

                // Check the irq is forced
                var interruptMask = 1u << realInterruptNumber;
                if((registers.ProcessorInterruptForce[cpuid] & interruptMask) != 0)
                {
                    registers.ProcessorInterruptForce[cpuid] &= ~interruptMask;
                    interrupts[cpuid].Remove(realInterruptNumber);
                    if(irqs[cpuid].IsSet)
                    {
                        irqs[cpuid].Unset();
                    }
                }
                else
                {
                    // Check if the interrupt is still pending, needs an ACK and the input is not set
                    if((registers.InterruptPending & interruptMask) != 0 && (pirqState & interruptMask) == 0)
                    {
                        // Remove the global pending interrupt
                        registers.InterruptPending &= ~interruptMask;
                        interrupts[cpuid].Remove(realInterruptNumber);
                        if(irqs[cpuid].IsSet)
                        {
                            irqs[cpuid].Unset();
                        }
                    }
                }
                this.forwardInterruptSingleCPU(cpuid);
            }
        }

        private void addPendingInterrupt(int cpuid, int number)
        {
            // Interrupts are added per CPU and have been checked against processor irq mask
            // in OnGPIO function before call - only handle priority here
            // Interrupt Level is either high (1) or low (0) - irq is prioritized per level, with 15 as highest
            // Extended interrupts have no prioritization between individual interrupts

            int interruptPriority;
            if(IsExtendedInterruptNumber(number))
            {
                interruptPriority = 1;
            }
            else
            {
                interruptPriority = number + (((registers.InterruptLevel & 1u<<number) != 0) ? 16 : 0);
            }

            // Safe as this is only called under the interrupts[cpuid] lock
            if(!interrupts[cpuid].ContainsKey(number))
            {
                interrupts[cpuid].Add(number, interruptPriority);
            }
        }

        private int currentNumberCpus ()
        {
            return (int)((registers.MultiprocessorStatus >> 28) & 0xF);
        }

        private bool isBroadcastEnabled ()
        {
            if(((registers.MultiprocessorStatus >> 27) & 0x1) == 0x1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static bool IsExtendedInterruptNumber(int irq)
        {
            return irq >= 16 && irq <= 32;
        }

        private readonly bool[] set_nmi_interrupt;
        private readonly uint numberOfProcessors;
        private readonly uint vendorID = 0x01;  // Aeroflex Gaisler
        private readonly uint deviceID = 0x00d; // GRLIB IRQMP
        private static uint maxNumberOfProcessors = 16;
        private readonly GaislerAPBPlugAndPlayRecord.SpaceType spaceType = GaislerAPBPlugAndPlayRecord.SpaceType.APBIOSpace;
        private deviceRegisters registers;
        private uint pirqState;
        private readonly GPIO[] irqs;
        private readonly GPIO[] resets;
        private readonly GPIO[] runs;
        private Dictionary<int, int>[] interrupts = new Dictionary<int, int>[maxNumberOfProcessors];

        private enum registerOffset : uint
        {
            InterruptLevel = 0x00,
            InterruptPending = 0x04,
            InterruptForce = 0x08,
            InterruptClear = 0x0C,
            MultiprocessorStatus = 0x10,
            Broadcast = 0x14,
            ProcessorInterruptMaskBase = 0x40,
            ProcessorInterruptForceBase = 0x80,
            ProcessorExtendedInterruptAcknowledgeBase = 0xC0
        }

        private class deviceRegisters
        {
            public uint InterruptLevel;
            public uint InterruptPending;
            public uint MultiprocessorStatus = 0x01 | (1 << 16); // 1 - interrupt number used for extended IRQs
            public uint Broadcast;
            public uint[] ProcessorInterruptMask;
            public uint[] ProcessorInterruptForce;
            public uint[] ProcessorExtendedInterruptAcknowledge;

            public deviceRegisters()
            {
                ProcessorInterruptMask = new uint[maxNumberOfProcessors];
                ProcessorInterruptForce = new uint[maxNumberOfProcessors];
                ProcessorExtendedInterruptAcknowledge = new uint[maxNumberOfProcessors];
            }
        }

        public const int maxNumberOfExternalInterrupts = 16;
        public const int NMI_IRQ = 15;
    }
}

