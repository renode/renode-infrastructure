//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.IRQControllers.ARM_GenericInterruptControllerModel;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class ArmGicRedistributorRegistration : BusParametrizedRegistration
    {
        public ArmGicRedistributorRegistration(IARMSingleSecurityStateCPU attachedCPU, ulong address, ICPU visibleTo = null, ICluster<ICPU> visibleToCluster = null) : base(address, 0x20000, visibleTo, visibleToCluster)
        {
            Cpu = attachedCPU;
        }

        public override Action<long, byte> GetWriteByteMethod(IBusPeripheral peripheral)
        {
            GetGICAndCPUEntry(peripheral, out var gic, out var entry);
            return (offset, value) =>
            {
                gic.LockExecuteAndUpdate(() =>
                    {
                        var registerExists = IsByteAccessible(offset) && Utils.TryWriteByteToDoubleWordCollection(entry.RedistributorDoubleWordRegisters, offset, value, gic);
                        gic.LogWriteAccess(registerExists, value, "Redistributor (byte access)", offset, (ARM_GenericInterruptController.RedistributorRegisters)offset);
                    }
                );
            };
        }

        public override Func<long, byte> GetReadByteMethod(IBusPeripheral peripheral)
        {
            GetGICAndCPUEntry(peripheral, out var gic, out var entry);
            return (offset) =>
            {
                byte value = 0;
                gic.LockExecuteAndUpdate(() =>
                    {
                        var registerExists = IsByteAccessible(offset) && Utils.TryReadByteFromDoubleWordCollection(entry.RedistributorDoubleWordRegisters, offset, out value, gic);
                        gic.LogReadAccess(registerExists, value, "Redistributor (byte access)", offset, (ARM_GenericInterruptController.RedistributorRegisters)offset);
                    }
                );
                return value;
            };
        }

        public override Action<long, uint> GetWriteDoubleWordMethod(IBusPeripheral peripheral)
        {
            GetGICAndCPUEntry(peripheral, out var gic, out var entry);
            return (offset, value) =>
            {
                gic.LockExecuteAndUpdate(() =>
                    {
                        // Only a few virtual quad word registers can't be double word accessed, assume all accesses are valid.
                        var registerExists = entry.RedistributorDoubleWordRegisters.TryWrite(offset, value)
                            || Utils.TryWriteDoubleWordToQuadWordCollection(entry.RedistributorQuadWordRegisters, offset, value, gic);
                        gic.LogWriteAccess(registerExists, value, "Redistributor", offset, (ARM_GenericInterruptController.RedistributorRegisters)offset);
                    }
                );
            };
        }

        public override Func<long, uint> GetReadDoubleWordMethod(IBusPeripheral peripheral)
        {
            GetGICAndCPUEntry(peripheral, out var gic, out var entry);
            return (offset) =>
            {
                uint value = 0;
                gic.LockExecuteAndUpdate(() =>
                    {
                        // Only a few virtual quad word registers can't be double word accessed, assume all accesses are valid.
                        var registerExists = entry.RedistributorDoubleWordRegisters.TryRead(offset, out value)
                            || Utils.TryReadDoubleWordFromQuadWordCollection(entry.RedistributorQuadWordRegisters, offset, out value, gic);
                        gic.LogReadAccess(registerExists, value, "Redistributor", offset, (ARM_GenericInterruptController.RedistributorRegisters)offset);
                    }
                );
                return value;
            };
        }

        public override Action<long, ulong> GetWriteQuadWordMethod(IBusPeripheral peripheral)
        {
            GetGICAndCPUEntry(peripheral, out var gic, out var entry);
            return (offset, value) =>
            {
                gic.LockExecuteAndUpdate(() =>
                    gic.LogWriteAccess(entry.RedistributorQuadWordRegisters.TryWrite(offset, value), value, "Redistributor", offset, (ARM_GenericInterruptController.RedistributorRegisters)offset)
                );
            };
        }

        public override Func<long, ulong> GetReadQuadWordMethod(IBusPeripheral peripheral)
        {
            GetGICAndCPUEntry(peripheral, out var gic, out var entry);
            return (offset) =>
            {
                ulong value = 0;
                gic.LockExecuteAndUpdate(() =>
                    gic.LogReadAccess(entry.RedistributorQuadWordRegisters.TryRead(offset, out value), value, "Redistributor", offset, (ARM_GenericInterruptController.RedistributorRegisters)offset)
                );
                return value;
            };
        }

        public override void RegisterForEachContext(Action<BusParametrizedRegistration> register)
        {
            RegisterForEachContextInner(register, visibleTo => new ArmGicRedistributorRegistration(Cpu, Range.StartAddress, visibleTo));
        }

        private void GetGICAndCPUEntry(IBusPeripheral peripheral, out ARM_GenericInterruptController gic, out ARM_GenericInterruptController.CPUEntry entry)
        {
            gic = peripheral as ARM_GenericInterruptController;
            if(gic == null)
            {
                throw new RegistrationException($"RedistributorRegistration can only be attached to {nameof(ARM_GenericInterruptController)}");
            }
            if(!gic.ArchitectureVersionAtLeast3)
            {
                throw new RegistrationException($"RedistributorRegistration can only be attached to {nameof(ARM_GenericInterruptController)} version 3 and above");
            }
            if(!gic.TryGetCPUEntryForCPU(Cpu, out entry))
            {
                throw new RegistrationException($"Couldn't register redistributor for CPU because the CPU isn't attached to this GIC: {Cpu.GetName()}");
            }
        }

        public override string ToString()
        {
            return $"{base.ToString()} GIC redistributor, attached CPU: {Cpu}";
        }

        private bool IsByteAccessible(long offset)
        {
            const long maxByteOffset = 3;
            return (long)ARM_GenericInterruptController.RedistributorRegisters.InterruptPriority_0 <= offset && offset <= (long)ARM_GenericInterruptController.RedistributorRegisters.InterruptPriority_7 + maxByteOffset;
        }

        public IARMSingleSecurityStateCPU Cpu { get; }
    }
}
