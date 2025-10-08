//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.DMA
{
    public struct Request
    {
        public Request(Place source, Place destination, int size, TransferType readTransferType, TransferType writeTransferType,
            bool incrementReadAddress = true, bool incrementWriteAddress = true) : this()
        {
            this.Source = source;
            this.Destination = destination;
            this.Size = size;
            this.ReadTransferType = readTransferType;
            this.WriteTransferType = writeTransferType;
            this.IncrementReadAddress = incrementReadAddress;
            this.IncrementWriteAddress = incrementWriteAddress;
            this.SourceIncrementStep = (ulong)readTransferType;
            this.DestinationIncrementStep = (ulong)writeTransferType;
        }

        public Request(Place source, Place destination, int size, TransferType readTransferType, TransferType writeTransferType,
            ulong sourceIncrementStep, ulong destinationIncrementStep, bool incrementReadAddress = true,
            bool incrementWriteAddress = true) : this()
        {
            this.Source = source;
            this.Destination = destination;
            this.Size = size;
            this.ReadTransferType = readTransferType;
            this.WriteTransferType = writeTransferType;
            this.IncrementReadAddress = incrementReadAddress;
            this.IncrementWriteAddress = incrementWriteAddress;
            this.SourceIncrementStep = sourceIncrementStep;
            this.DestinationIncrementStep = destinationIncrementStep;
        }

        public Place Source { get; private set; }

        public Place Destination { get; private set; }

        public ulong SourceIncrementStep { get; private set; }

        public ulong DestinationIncrementStep { get; private set; }

        public int Size { get; private set; }

        public TransferType ReadTransferType { get; private set; }

        public TransferType WriteTransferType { get; private set; }

        public bool IncrementReadAddress { get; private set; }

        public bool IncrementWriteAddress { get; private set; }
    }
}