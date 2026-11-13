//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

#nullable enable

namespace Antmicro.Renode.Peripherals.MemoryControllers;

public class AndesLocalMemory : IPeripheral
{
    public AndesLocalMemory(IMachine machine, MappedMemory instructionMemory, MappedMemory dataMemory,
        BaseRiscV cpu, ulong instructionAddress = 0, ulong dataAddress = 0x200000,
        MappedMemory? systemMemory = null, bool eccEnabled = false, ulong systemAddress = 0)
    {
        if(cpu.AndesExtension == null)
        {
            throw new ConstructionException("Andes local memory requires a CPU with the Andes extension.");
        }

        var instructionConfiguration = GetConfiguration(instructionMemory, instructionAddress, eccEnabled);
        var dataConfiguration = GetConfiguration(dataMemory, dataAddress, eccEnabled);
        if(new Range(instructionAddress, (ulong)instructionMemory.Size).Intersects(new Range(dataAddress, (ulong)dataMemory.Size)))
        {
            throw new ConstructionException("Instruction and data local memory ranges must not overlap.");
        }

        if(systemMemory != null)
        {
            ValidateSystemMemoryRange(systemMemory, systemAddress, instructionAddress, instructionMemory.Size);
            ValidateSystemMemoryRange(systemMemory, systemAddress, dataAddress, dataMemory.Size);
        }

        this.sysbus = (SystemBus)machine.SystemBus;
        this.cpu = cpu;
        this.instructionMemory = instructionMemory;
        this.dataMemory = dataMemory;
        this.instructionAddress = instructionAddress;
        this.dataAddress = dataAddress;
        this.systemMemory = systemMemory;
        this.systemAddress = systemAddress;

        // Register twice to keep the storage alive when the last address mapping is removed.
        sysbus.Register((IPeripheral)instructionMemory, NullRegistrationPoint.Instance);
        sysbus.Register((IPeripheral)dataMemory, NullRegistrationPoint.Instance);
        if(systemMemory != null)
        {
            sysbus.Register((IPeripheral)systemMemory, NullRegistrationPoint.Instance);
        }

        instructionControl = DefineControl(instructionMemory, instructionAddress, eccEnabled, out instructionEnabled);
        dataControl = DefineControl(dataMemory, dataAddress, eccEnabled, out dataEnabled);
        cpu.RegisterCSR(0x7c0, instructionControl.Read, value => instructionControl.Write(0, value), "milmb");
        cpu.RegisterCSR(0x7c1, dataControl.Read, value => dataControl.Write(0, value), "mdlmb");
        cpu.AndesExtension.InstructionMemoryConfiguration = instructionConfiguration;
        cpu.AndesExtension.DataMemoryConfiguration = dataConfiguration;
    }

    public void Reset()
    {
        instructionControl.Reset();
        dataControl.Reset();
        UpdateMapping(instructionAddress, instructionMemory, InstructionMemoryEnabled);
        UpdateMapping(dataAddress, dataMemory, DataMemoryEnabled);
    }

    public bool InstructionMemoryEnabled => instructionEnabled.Value;

    public bool DataMemoryEnabled => dataEnabled.Value;

    private static ulong GetConfiguration(MappedMemory memory, ulong address, bool eccEnabled)
    {
        var size = (ulong)memory.Size;
        if(size < 1024 || size > 16 * 1024 * 1024 || (size & (size - 1)) != 0 || address % size != 0)
        {
            throw new ConstructionException("Local memory must be a power of two between 1 KiB and 16 MiB, aligned to its size.");
        }

        // One base register, size encoded as log2(bytes) - 9, and optional ECC.
        return (1UL << 12) | ((ulong)(BitHelper.GetMostSignificantSetBitIndex(size) - 9) << 15)
            | (eccEnabled ? 2UL << 21 : 0);
    }

    private static void ValidateSystemMemoryRange(MappedMemory memory, ulong baseAddress, ulong address, long size)
    {
        if(address < baseAddress || (ulong)size > (ulong)memory.Size || address - baseAddress > (ulong)(memory.Size - size))
        {
            throw new ConstructionException("System memory must cover both local memory ranges.");
        }

        if(size % memory.SegmentSize != 0 || (address - baseAddress) % (ulong)memory.SegmentSize != 0)
        {
            throw new ConstructionException("System memory segments must align with the local-memory windows. Use a smaller segmentSize.");
        }
    }

    private QuadWordRegister DefineControl(MappedMemory memory, ulong address, bool eccEnabled, out IFlagRegisterField enabled)
    {
        return new QuadWordRegister(this, address | 1UL | (eccEnabled ? 6UL : 0))
            .WithFlag(0, out enabled, changeCallback: (_, value) => UpdateMapping(address, memory, value), name: "EN")
            .WithValueField(1, 2, name: "ECCEN")
            .WithTaggedFlag("RWECC", 3)
            .WithReservedBits(4, 6)
            // AE350 uses fixed local-memory base addresses.
            .WithValueField(10, 54, FieldMode.Read, name: "BPA");
    }

    private void UpdateMapping(ulong address, MappedMemory localMemory, bool enabled)
    {
        var desiredMemory = enabled ? localMemory : systemMemory;
        var size = (ulong)localMemory.Size;
        var current = sysbus.WhatIsAt(address);
        if(current != null && ((current.Peripheral != localMemory && current.Peripheral != systemMemory)
            || current.RegistrationPoint.Range != new Range(address, size)
            || current.RegistrationPoint.Offset != (current.Peripheral == localMemory ? 0 : address - systemAddress)))
        {
            throw new RecoverableException("Local-memory mappings must match the exact window, backing memory and offset.");
        }

        if(current?.Peripheral == desiredMemory)
        {
            return;
        }

        using(sysbus.Machine.ObtainPausedState(true))
        {
            if(current != null)
            {
                sysbus.Unregister(current);
            }
            if(desiredMemory != null)
            {
                sysbus.Register(desiredMemory, new BusRangeRegistration(address, size, enabled ? 0 : address - systemAddress));
            }
            // The same guest address can now contain different instructions. Request a return
            // from the current block before discarding translations when called from a CSR.
            cpu.RequestTranslationCacheClearing();
        }
    }

    private readonly SystemBus sysbus;
    private readonly BaseRiscV cpu;
    private readonly MappedMemory instructionMemory;
    private readonly MappedMemory dataMemory;
    private readonly MappedMemory? systemMemory;
    private readonly ulong instructionAddress;
    private readonly ulong dataAddress;
    private readonly ulong systemAddress;
    private readonly QuadWordRegister instructionControl;
    private readonly QuadWordRegister dataControl;
    private readonly IFlagRegisterField instructionEnabled;
    private readonly IFlagRegisterField dataEnabled;
}
