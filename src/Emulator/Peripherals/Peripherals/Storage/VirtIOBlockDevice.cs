//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Storage.VirtIO;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.Storage
{
    // VirtIO class implementing VirtIO block devices.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class VirtIOBlockDevice : VirtIOMMIO, IDisposable
    {
        public VirtIOBlockDevice(IMachine machine, string diskId = "") : base(machine)
        {
            storage = DataStorage.CreateInTemporaryFile(size: 0);
            lastQueueIdx = 0;
            Virtqueues = new Virtqueue[lastQueueIdx + 1];
            for(int i = 0; i <= lastQueueIdx; i++)
            {
                Virtqueues[i] = new Virtqueue(this, Virtqueue.QueueMaxSize);
            }
            BitHelper.SetBit(ref deviceFeatureBits, (byte)FeatureBits.BlockFlagFlush, true);
            BitHelper.SetBit(ref deviceFeatureBits, (byte)FeatureBits.BlockFlagConfigWCE, true);
            DefineRegisters();

            if(string.IsNullOrEmpty(diskId))
            {
                machine.PeripheralsChanged += (machine, ev) =>
                {
                    /* Creation driver will set this device name after this creation. Therefore,
                     * retrieving the name of the device to assign diskId has to be delayed.
                     */
                    if(ev.Peripheral == this && ev.Operation == PeripheralsChangedEventArgs.PeripheralChangeType.NameChanged)
                    {
                        DiskId = this.GetName();
                    }
                };
            }
            else
            {
                DiskId = diskId;
            }
        }

        public void Dispose()
        {
            storage?.Dispose();
        }

        public void LoadImage(WriteFilePath file, bool persistent = false, CompressionType compression = CompressionType.None)
        {
            storage?.Dispose();
            storage = DataStorage.CreateFromFile(file, persistent: persistent, compression: compression);
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
            if(!SeekToSector(hdr.Sector))
            {
                this.Log(LogLevel.Error, "Driver tried to seek beyond the loaded image end.");
                return false;
            }

            vqueue.ReadDescriptorMetadata();
            var length = vqueue.Descriptor.Length;

            switch(hdr.Type)
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

            case BlockOperations.GetId:
                if(!vqueue.TryWriteToBuffers(diskIdBytes))
                {
                    return false;
                }
                break;

            default:
                this.Log(LogLevel.Error, "Unsupported block operation ({0})", hdr.Type);
                break;
            }

            WriteStatus(vqueue);
            return true;
        }

        public string DiskId
        {
            get => diskIdBytes is null ? "" : Encoding.ASCII.GetString(diskIdBytes).TrimEnd('\0');

            private set
            {
                diskIdBytes = new byte[IdSize]; // Always IdSize as VirtIO requirement

                if(!value.All(char.IsAscii))
                {
                    this.Log(LogLevel.Warning, "Disk Id ({0}) contains non-ASCII characters, they will be replaced with '?'", value);
                }

                var byteCount = Encoding.ASCII.GetByteCount(value);
                var size = byteCount < diskIdBytes.Length ? byteCount : diskIdBytes.Length;
                Encoding.ASCII.GetBytes(value, 0, size, diskIdBytes, 0);

                if(size < byteCount)
                {
                    this.Log(LogLevel.Warning, "Disk Id longer than {0}, truncating it to '{1}'", diskIdBytes.Length, DiskId);
                }
            }
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
        private byte[] diskIdBytes;

        private const int SectorSize = 0x200;
        private const int IdSize = 20; // VirtIO specification 1.4, section 5.2.6: 20 bytes NUL padded if diskId is less than 20 bytes.

        [LeastSignificantByteFirst]
        private struct Header
        {
#pragma warning disable 0649
            [PacketField, Width(bits: 32)]
            public BlockOperations Type;
            [PacketField, Offset(doubleWords: 2), Width(bits: 64)]
            public long Sector;
#pragma warning restore 0649
            // we don't use other fields from the documentation
        }

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
            GetId = 8,
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
    }
}
