//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Linq;

using Antmicro.Renode.Core.ACPI;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.Extensions
{
    public static class ACPIExtensions
    {
        public static void GenerateACPITable(this IBusController bus, ulong address)
        {
            var rootSystemDescriptionPointerLength = (uint)Packet.CalculateLength<RootSystemDescriptionPointer>();
            var rootSystemDescriptionTableLength = (uint)Packet.CalculateLength<RootSystemDescriptionTable>();
            var fixedACPIDescriptionTableLength = (uint)Packet.CalculateLength<FixedACPIDescriptionTable>();
            var differentiatedSystemDescriptionTableLength = (uint)Packet.CalculateLength<DifferentiatedSystemDescriptionTable>();
            var multipleAPICDescriptionTableLength = (uint)Packet.CalculateLength<MultipleAPICDescriptionTable>();

            var rootSystemDescriptionTableOffset = (uint)address + rootSystemDescriptionPointerLength;
            var fixedACPIDescriptionTableAddress = rootSystemDescriptionTableOffset + rootSystemDescriptionTableLength;
            var differentiatedSystemDescriptionTableAddress = fixedACPIDescriptionTableAddress + fixedACPIDescriptionTableLength;
            var multipleAPICDescriptionTableHeaderAddress = differentiatedSystemDescriptionTableAddress + differentiatedSystemDescriptionTableLength;

            var rootSystemDescriptionPointer = new RootSystemDescriptionPointer(rootSystemDescriptionTableOffset);
            bus.WriteBytes(Packet.Encode(rootSystemDescriptionPointer), address);

            var rootSystemDescriptionTable = new RootSystemDescriptionTable(fixedACPIDescriptionTableAddress, multipleAPICDescriptionTableHeaderAddress);
            bus.WriteBytes(Packet.Encode(rootSystemDescriptionTable), rootSystemDescriptionTableOffset);

            var fixedACPIDescriptionTable = new FixedACPIDescriptionTable(fixedACPIDescriptionTableLength, differentiatedSystemDescriptionTableAddress);
            bus.WriteBytes(Packet.Encode(fixedACPIDescriptionTable), fixedACPIDescriptionTableAddress);

            var fixedACPIDifferentiatedSystemDescriptionTable = new DifferentiatedSystemDescriptionTable(differentiatedSystemDescriptionTableLength);
            bus.WriteBytes(Packet.Encode(fixedACPIDifferentiatedSystemDescriptionTable), differentiatedSystemDescriptionTableAddress);

            var ids = bus.GetCPUs().OfType<BaseX86>().Select(x => (ulong)x.Lapic.PhysicalID).ToList();
            var recordLength = (uint)Packet.CalculateLength<ProcessorLocalAPICRecord>();

            // Define table without records. They will be defined based on CPUs.
            var multipleAPICDescriptionTable = new MultipleAPICDescriptionTable((uint)(multipleAPICDescriptionTableLength + ids.Count() * recordLength));
            bus.WriteBytes(Packet.Encode(multipleAPICDescriptionTable), multipleAPICDescriptionTableHeaderAddress);

            var recordAddress = multipleAPICDescriptionTableHeaderAddress + multipleAPICDescriptionTableLength;

            foreach(var id in ids)
            {
                var processorLocalAPICRecord = new ProcessorLocalAPICRecord
                {
                    EntryType = 0x0,    // 0x0 means local APIC entry type
                    RecordLength = 0x8,
                    APICID = (byte)id,
                    Flags = 0x01,       // bit 0 = Processor Enabled
                };
                var table = Packet.Encode(processorLocalAPICRecord).ToArray();
                bus.WriteBytes(table, recordAddress);

                recordAddress += recordLength;
            }
        }
    }
}