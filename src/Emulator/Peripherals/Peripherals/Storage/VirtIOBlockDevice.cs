//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Runtime.InteropServices;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Storage.VirtIO;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.Storage
{
    // VirtIO class implementing VirtIO block devices.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class VirtIOBlockDevice : VirtIOMMIO, IDisposable
    {
        public VirtIOBlockDevice(IMachine machine) : base(machine)
        {
            storage = DataStorage.Create(size: 0);
            lastQueueIdx = 0;
            Virtqueues = new Virtqueue[lastQueueIdx + 1];
            for (int i = 0; i <= lastQueueIdx; i++)
            {
                Virtqueues[i] = new Virtqueue(this, Virtqueue.QueueMaxSize);
            }
            BitHelper.SetBit(ref deviceFeatureBits, (byte)FeatureBits.BlockFlagFlush, true);
            BitHelper.SetBit(ref deviceFeatureBits, (byte)FeatureBits.BlockFlagConfigWCE, true);
            DefineRegisters();
        }

        public void Dispose()
        {
            storage?.Dispose();
        }

        public void LoadImage(WriteFilePath file, bool persistent = false)
        {
            storage?.Dispose();
            storage = DataStorage.Create(file, persistent: persistent);
            capacity = (long)Math.Ceiling((decimal)storage.Length / SectorSize);
            configHasChanged.Value = true;
            UpdateInterrupts();
        }

        public void WriteStatus(Virtqueue vqueue)
        {
            vqueue.ReadDescriptorMetadata();
            SystemBus.WriteByte(vqueue.Descriptor.BufferAddress, status);
        }

        public void Flush()
        {
            storage.Flush();
        }

        public void MarkAsUnsupported()
        {
            status = (byte)VirtIOBlockRequestStatus.Unsupported;
            this.Log(LogLevel.Warning, "Block operation unsupported.");
        }

        public override bool ProcessChain(Virtqueue vqueue)
        {
            vqueue.ReadDescriptorMetadata();
            vqueue.TryReadFromBuffers(Marshal.SizeOf(typeof(Header)), out var hdrBuff);
            if(!Packet.TryDecode<Header>(hdrBuff, out var hdr))
            {
                this.Log(LogLevel.Error, "Error decoding block request header");
                return false;
            }
            if(!SeekToSector(hdr.sector))
            {
                this.Log(LogLevel.Error, "Driver tried to seek beyond the loaded image end.");
                return false;
            }
            
            vqueue.ReadDescriptorMetadata();
            var length = vqueue.Descriptor.Length;

            switch(hdr.type)
            {
                case BlockOperations.Out:
                    if(!vqueue.TryReadFromBuffers(length, out var res))
                    {
                        return false;
                    }
                    storage.Write(res, 0, length);
                    break;

                case BlockOperations.In:
                    byte[] driverBytes = new byte[length];
                    storage.Read(driverBytes, 0, length);
                    if(!vqueue.TryWriteToBuffers(driverBytes))
                    {
                        return false;
                    }
                    break;

                case BlockOperations.Flush:
                    if(IsFeatureEnabled((byte)FeatureBits.BlockFlagFlush))
                    {
                        Flush();
                    }
                    else
                    {
                        MarkAsUnsupported();
                    }
                    break;

                default:
                    this.Log(LogLevel.Error, "Unsupported block operation ({0})", hdr.type);
                    break;
            }

            WriteStatus(vqueue);
            return true;
        }

        protected override uint DeviceID => 0x2;

        private void DefineRegisters()
        {
            DefineMMIORegisters();
            Registers.CapacityHigh.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "capacity_high", valueProviderCallback: _ => (uint)(capacity >> 32));

            Registers.CapacityLow.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "capacity_low", valueProviderCallback: _ => (uint)capacity);

            // With this register driver can choose whether it will use write-back or write-through caching mode.
            // It should be 0 by default.
            Registers.Writeback.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "writeback", valueProviderCallback: _ => 0);
        }

        private bool SeekToSector(long sector)
        {
            var positionToSeek = SectorSize * sector;
            if(positionToSeek >= this.storage.Length)
            {
                return false;
            }
            storage.Seek(positionToSeek, SeekOrigin.Begin);
            return true;
        }

        private long capacity;
        private Stream storage;
        private byte status;

        private const int SectorSize = 0x200;

        private enum FeatureBits : byte
        {
            // Block device specific flags
            BlockFlagSizeMax = 1,
            BlockFlagSegmentsMaxNum = 2,
            BlockFlagGeometry = 4,
            BlockFlagReadOnly = 5,
            BlockFlagBlockSize = 6,
            BlockFlagFlush = 9,
            BlockFlagTopology = 10,
            BlockFlagConfigWCE = 11,
            BlockFlagDiscard = 13,
            BlockFlagWriteZeroes = 14,
        }

        private enum BlockRequestHeader
        {
            Type = 0x0,
            SectorLow = 0x8,
            SectorHigh = 0xc,
        }

        private enum BlockOperations : int
        {
            In = 0,
            Out = 1,
            Flush = 4,
            Discard = 11,
            WriteZeroes = 13,
        }

        private enum VirtIOBlockRequestStatus : byte
        {
            Success = 0,
            IoError = 1,
            Unsupported = 2,
        }

        private enum Registers : long
        {
            // Configuration space for block device
            // https://docs.oasis-open.org/virtio/virtio/v1.2/csd01/virtio-v1.2-csd01.pdf#subsection.5.2.4
            CapacityLow = 0x100,
            CapacityHigh = 0x104,
            SizeMax = 0x108,
            SegMax = 0x10c,
            Geometry = 0x110,
            BlockSize = 0x114,
            TopologyHigh = 0x118,
            TopologyLow = 0x11c,
            Writeback = 0x120,
            MaxDiscardSectors = 0x124,
            MaxDiscardSeg = 0x128,
            DiscardSectorAlignment = 0x12c,
            MaxWriteZeroesSectors = 0x130,
            MaxWriteZeroesSeg = 0x134,
            WriteZeroesMayUnmap = 0x138,
        }

        [LeastSignificantByteFirst]
        private struct Header
        {
            #pragma warning disable 0649
            [PacketField, Width(32)]
            public BlockOperations type;
            [PacketField, Offset(doubleWords: 2), Width(64)]
            public long sector;
            #pragma warning restore 0649
            // we don't use other fields from the documentation
        }
    }
}
