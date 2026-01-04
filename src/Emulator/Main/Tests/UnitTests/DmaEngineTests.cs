//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.DMA;
using Antmicro.Renode.Peripherals.Memory;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class DmaEngineTests
    {
        [SetUp]
        public void SetUp()
        {
            var machine = new Machine();
            EmulationManager.Instance.CurrentEmulation.AddMachine(machine);

            rawArraySource = new byte[MemorySize];
            rawArrayDestination = new byte[MemorySize];

            rawArrayMemorySource = new ArrayMemory(rawArraySource);
            rawArrayMemoryDestination = new ArrayMemory(rawArrayDestination);

            mappedMemorySource = new MappedMemory(machine, MemorySize);
            mappedMemoryDestination = new MappedMemory(machine, MemorySize);

            arrayMemorySource = new ArrayMemory(MemorySize);
            arrayMemoryDestination = new ArrayMemory(MemorySize);

            sysbus = machine.SystemBus;
            sysbus.Register(mappedMemorySource, new BusPointRegistration(MappedMemorySourceStartAddress));
            sysbus.Register(mappedMemoryDestination, new BusPointRegistration(MappedMemoryDestinationStartAddress));
            sysbus.Register(arrayMemorySource, new BusPointRegistration(ArrayMemorySourceStartAddress));
            sysbus.Register(arrayMemoryDestination, new BusPointRegistration(ArrayMemoryDestinationStartAddress));

            dmaEngine = new DmaEngine(sysbus);
        }

        [Test]
        public void ShouldTransferDataFromSourceToDestination(
            [Values(PlaceType.SourceRawArray, PlaceType.SourceArrayMemory, PlaceType.SourceMappedMemory)] PlaceType sourcePlaceType,
            [Values(PlaceType.DestinationRawArray, PlaceType.DestinationArrayMemory, PlaceType.DestinationMappedMemory)] PlaceType destinationPlaceType,
            [Values(1, 2, 4, 8)] int sourceAccessWidth,
            [Values(1, 2, 4, 8)] int destinationAccessWidth
        )
        {
            FillMemoryWithRepeatingData(sourcePlaceType, InitialPattern);

            var initialDataPatternCheck = ReadBytesFromMemory(sourcePlaceType, 0, 16);
            Assert.That(InitialPattern.SequenceEqual(initialDataPatternCheck));

            const int numberOfBytesToTransfer = 32;
            const int sourceOffset = 8;
            const int destinationOffset = 32;

            var request = new Request(
                source: GetPlaceInMemory(sourcePlaceType, sourceOffset),
                destination: GetPlaceInMemory(destinationPlaceType, destinationOffset),
                size: numberOfBytesToTransfer,
                readTransferType: (TransferType)sourceAccessWidth,
                writeTransferType: (TransferType)destinationAccessWidth
            );

            var dataToTransfer = ReadBytesFromMemory(sourcePlaceType, sourceOffset, numberOfBytesToTransfer);
            var response = dmaEngine.IssueCopy(request);
            var dataTransferred = ReadBytesFromMemory(destinationPlaceType, destinationOffset, numberOfBytesToTransfer);

            Assert.That(dataToTransfer.SequenceEqual(dataTransferred));
        }

        private IMultibyteWritePeripheral GetMemory(PlaceType placeType)
        {
            switch(placeType)
            {
            case PlaceType.SourceRawArray:
                return rawArrayMemorySource;
            case PlaceType.DestinationRawArray:
                return rawArrayMemoryDestination;
            case PlaceType.SourceArrayMemory:
                return arrayMemorySource;
            case PlaceType.DestinationArrayMemory:
                return arrayMemoryDestination;
            case PlaceType.SourceMappedMemory:
                return mappedMemorySource;
            case PlaceType.DestinationMappedMemory:
                return mappedMemoryDestination;
            default:
                throw new ArgumentException("Invalid place specified for DMA test");
            }
        }

        private byte[] ReadBytesFromMemory(PlaceType placeType, int offset, int count)
        {
            var memory = GetMemory(placeType);
            return memory.ReadBytes(offset, count);
        }

        private void FillMemoryWithRepeatingData(PlaceType placeType, byte[] pattern)
        {
            var memory = GetMemory(placeType);
            memory.FillWithRepeatingData(pattern);
        }

        private Place GetPlaceInMemory(PlaceType placeType, int offset)
        {
            switch(placeType)
            {
            case PlaceType.SourceRawArray:
                return new Place(rawArraySource, offset);
            case PlaceType.DestinationRawArray:
                return new Place(rawArrayDestination, offset);
            case PlaceType.SourceArrayMemory:
                return (ulong)(ArrayMemorySourceStartAddress + offset);
            case PlaceType.DestinationArrayMemory:
                return (ulong)(ArrayMemoryDestinationStartAddress + offset);
            case PlaceType.SourceMappedMemory:
                return (ulong)(MappedMemorySourceStartAddress + offset);
            case PlaceType.DestinationMappedMemory:
                return (ulong)(MappedMemoryDestinationStartAddress + offset);
            default:
                throw new ArgumentException("Invalid place specified for DMA test");
            }
        }

        private static readonly byte[] InitialPattern = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF };

        private IBusController sysbus;
        private DmaEngine dmaEngine;

        private byte[] rawArraySource;
        private byte[] rawArrayDestination;
        private ArrayMemory rawArrayMemorySource;
        private ArrayMemory rawArrayMemoryDestination;
        private MappedMemory mappedMemorySource;
        private MappedMemory mappedMemoryDestination;
        private ArrayMemory arrayMemorySource;
        private ArrayMemory arrayMemoryDestination;

        private const uint MemorySize = 5u * 1024;
        private const uint MappedMemorySourceStartAddress = 0x0;
        private const uint MappedMemoryDestinationStartAddress = 0x10000;
        private const uint ArrayMemorySourceStartAddress = 0x20000;
        private const uint ArrayMemoryDestinationStartAddress = 0x30000;

        public enum PlaceType
        {
            SourceRawArray,
            SourceArrayMemory,
            SourceMappedMemory,
            DestinationRawArray,
            DestinationArrayMemory,
            DestinationMappedMemory,
        }
    }
}