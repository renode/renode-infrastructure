//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System;
using System.IO;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Storage
{
    // VirtIO class implementing VirtIO block devices.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class VirtIOBlockDevice : BasicDoubleWordPeripheral, IKnownSize, IDisposable
    {
        public VirtIOBlockDevice(Machine machine) : base(machine)
        {
            DefineRegisters();
            storage = DataStorage.Create(size: 0);
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

        public override void Reset()
        {
            availableIndex = 0;
            availableIndexFromDriver = 0;
            usedIndex = 0;
            usedIndexForDriver = 0;
            driverFeatureBits = 0;
            virtqueueDescTableAddress = 0;
            virtqueueAvailableAddress = 0;
            virtqueueUsedAddress = 0;
            base.Reset();
            UpdateInterrupts();
        }

        public long Size => 0x150;
        public GPIO IRQ { get; } = new GPIO();

        private void UpdateInterrupts()
        {
            var toSet = hasUsedBuffer.Value || configHasChanged.Value;
            IRQ.Set(toSet);
        }

        private void DefineRegisters()
        {
            // General initialisation
            Registers.MagicValue.Define(this, MagicNumber)
                .WithValueField(0, 32, FieldMode.Read, name: "magic_value");

            Registers.DeviceVersion.Define(this, Version)
                .WithValueField(0, 2, FieldMode.Read, name: "dev_version")
                .WithReservedBits(2, 30);

            Registers.DeviceID.Define(this, DeviceID)
                .WithValueField(0, 2, FieldMode.Read, name: "dev_id")
                .WithReservedBits(2, 30);

            Registers.VendorID.Define(this, VendorID)
                .WithValueField(0, 16, FieldMode.Read, name: "vendor_id")
                .WithReservedBits(16, 16);

            Registers.Status.Define(this)
                .WithValueField(0, 32, out deviceStatus, writeCallback: (_, val) =>
                {
                    // Writing 0 to status register resets the device. Source: https://docs.oasis-open.org/virtio/virtio/v1.1/csprd01/virtio-v1.1-csprd01.html#x1-1460002
                    if(val == 0)
                    {
                        Reset();
                    }
                }, name: "status");

            // Feature bits
            Registers.DeviceFeatures.Define(this)
               .WithValueField(0, 32, FieldMode.Read, name: "features", valueProviderCallback: _ =>
                                (uint)(DeviceFeatureBits >> (32 * (int)deviceFeatureBitsIndex.Value)));

            Registers.DeviceFeaturesSelected.Define(this)
                .WithValueField(0, 1, out deviceFeatureBitsIndex, FieldMode.Write, name: "features_sel")
                .WithReservedBits(1, 31);

            Registers.DriverFeatures.Define(this)
               .WithValueField(0, 32, FieldMode.Write, name: "guestbits", writeCallback: (_, val) =>
                                driverFeatureBits |= ((ulong)val << 32 * (int)driverFeatureBitsIndex.Value));

            Registers.DriverFeaturesSelected.Define(this)
                .WithValueField(0, 1, out driverFeatureBitsIndex, FieldMode.Write, name: "guest_sel")
                .WithReservedBits(1, 31);

            // Because it is a block device there is only one virtqueue with index 0.
            Registers.VirtqueueSel.Define(this)
                .WithTag("queue_num_select", 0, 32);

            Registers.VirtqueueSizeMax.Define(this, VirtqueueMaxSize)
                .WithValueField(0, 32, FieldMode.Read, name: "queue_size_max");

            Registers.VirtqueueSize.Define(this)
                .WithValueField(0, 16, out virtqueueSize, FieldMode.Write, name: "queue_size", writeCallback: (_, val) =>
                {
                    if(virtqueueSize.Value > VirtqueueMaxSize)
                    {
                        this.Log(LogLevel.Error, "Virtqueue size exceeded max available value!");
                        deviceStatus.Value |= (int)Status.Failed;
                    }
                })
                .WithReservedBits(16, 16);

            Registers.VirtqueueReady.Define(this)
                .WithFlag(0, out isVirtqueueReady, FieldMode.Write, name: "queue_ready")
                .WithReservedBits(1, 31);

            Registers.VirtqueueNotify.Define(this)
                .WithValueField(0, 32, out virtqueueNotify, FieldMode.WriteOneToClear, writeCallback: (_, val) =>
                {
                    if(!isVirtqueueReady.Value)
                    {
                        this.Log(LogLevel.Error, "VirtIO driver started a block operation, but current virtqueue isn't marked as ready.");
                    }
                    else if((deviceStatus.Value & (int)Status.DriverOk) == 0)
                    {
                        this.Log(LogLevel.Error, "VirtIO driver started a block operation, but DriverOK flag not set in status register.");
                    }
                    else
                    {
                        VirtqueueHandle();
                    }
                }, name: "queue_notifications");

            // Virtqueue addresses
            Registers.VirtqueueDescLow.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => virtqueueDescTableAddress = virtqueueDescTableAddress & 0xFFFFFFFF00000000 | (ulong)val);

            Registers.VirtqueueDescHigh.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => virtqueueDescTableAddress = virtqueueDescTableAddress & 0x00000000FFFFFFFF | ((ulong)val << 32));

            Registers.VirtqueueDriverLow.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => virtqueueAvailableAddress = virtqueueAvailableAddress & 0xFFFFFFFF00000000 | (ulong)val);

            Registers.VirtqueueDriverHigh.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => virtqueueAvailableAddress = virtqueueAvailableAddress & 0x00000000FFFFFFFF | ((ulong)val << 32));

            Registers.VirtqueueDeviceLow.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => virtqueueUsedAddress = virtqueueUsedAddress & 0xFFFFFFFF00000000 | (ulong)val);

            Registers.VirtqueueDeviceHigh.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => virtqueueUsedAddress = virtqueueUsedAddress & 0x00000000FFFFFFFF | ((ulong)val << 32));

            // Interrupts registers
            Registers.InterruptStatus.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => hasUsedBuffer.Value)
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => configHasChanged.Value)
                .WithReservedBits(2, 30);

            Registers.InterruptACK.Define(this)
                .WithFlag(0, out hasUsedBuffer, FieldMode.WriteOneToClear)
                .WithFlag(1, out configHasChanged, FieldMode.WriteOneToClear)
                .WithWriteCallback((_,__) => UpdateInterrupts())
                .WithReservedBits(2, 30);

            // Config Register
            Registers.CapacityHigh.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)(capacity >> 32), name: "capacity_high");

            Registers.CapacityLow.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)capacity, name: "capacity_low");

            // With this register driver can choose whether it will use write-back or write-through caching mode.
            // It should be 0 by default.
            Registers.Writeback.Define(this, 0)
                .WithValueField(0, 8, FieldMode.Read, name: "writeback");
        }

        // Virtqueue handling methods
        private void VirtqueueHandle()
        {
            var idx = (ushort)base.machine.SystemBus.ReadWord(virtqueueAvailableAddress + (ulong)VirtqueueUsedAndAvailable.Index);
            // Processing all available requests
            // We're using 2 variables: availableIndex, availableIndexFromDriver because we have to compare this value to index field in driver's struct for available descriptors.
            // This field is meant to start at 0, then only increase and wrap around 65535. That's why we have one variable for comparing and the other one for accessing tables.
            // Source: https://docs.oasis-open.org/virtio/virtio/v1.1/csprd01/virtio-v1.1-csprd01.html#x1-5300013
            while(availableIndexFromDriver < idx)
            {
                this.Log(LogLevel.Debug, "Processing queue request with index {0}", availableIndex);
                var data = ReadVirtqueueAvailable();
                var bytesProcessed = ProcessBuffer(data.Item1);
                if(bytesProcessed == -1)
                {
                    return;
                }
                WriteVirtqueueUsed(data.Item1, data.Item2, bytesProcessed);
                availableIndex = (ushort)((availableIndex + 1) % virtqueueSize.Value);
                availableIndexFromDriver++;
            }
        }

        // Read next available desctriptor chain.
        private Tuple<int, bool> ReadVirtqueueAvailable()
        {
            var flag = base.machine.SystemBus.ReadWord(virtqueueAvailableAddress + (ulong)VirtqueueUsedAndAvailable.Flags);
            var noInterruptOnUsed = (flag == 1);
            var ret = (int)base.machine.SystemBus.ReadWord(virtqueueAvailableAddress + (ulong)VirtqueueUsedAndAvailable.Ring
                                                            + AvailableRingEntrySize * (ulong)availableIndex);
            return Tuple.Create(ret, noInterruptOnUsed);
        }

        private int ProcessBuffer(int chainFirstIndex)
        {
            var request = new Request(this, chainFirstIndex);
            if(!request.ReadHeader())
            {
                return -1;
            }
            if(request.Type == (int)BlockOperations.Out)
            {
                if(!request.Read())
                {
                    return -1;
                }
            }
            else if(request.Type == (int)BlockOperations.In)
            {
                if(!request.Write())
                {
                    return -1;
                }
            }
            else if(IsFeatureEnabled(FeatureBits.BlockFlagFlush) && request.Type == (int)BlockOperations.Flush)
            {
                request.Flush();
            }
            else
            {
                request.MarkAsUnsupported();
            }
            request.WriteStatus();
            return request.BytesProcessed;
        }

        // Write processed chain to used descriptors table. usedIndex and usedIndexForDriver work analogically to availableIndex and availableIndexFromDevice.
        private void WriteVirtqueueUsed(int descriptorIndex, bool noInterruptOnUsed, int bytesProcessed)
        {
            var ringAddress = virtqueueUsedAddress + (ulong)VirtqueueUsedAndAvailable.Ring
                                + UsedRingEntrySize * ((ulong)usedIndex);
            usedIndex = (ushort)((usedIndex + 1) % virtqueueSize.Value);
            usedIndexForDriver++;
            base.machine.SystemBus.WriteWord(virtqueueUsedAddress + (ulong)VirtqueueUsedAndAvailable.Flags, 0);
            base.machine.SystemBus.WriteDoubleWord(ringAddress + (ulong)UsedRing.Index, (uint)descriptorIndex);
            base.machine.SystemBus.WriteDoubleWord(ringAddress + (ulong)UsedRing.Length, (uint)bytesProcessed);
            base.machine.SystemBus.WriteWord(virtqueueUsedAddress + (ulong)VirtqueueUsedAndAvailable.Index, (ushort)usedIndexForDriver);
            if(!noInterruptOnUsed)
            {
                hasUsedBuffer.Value = true;
                UpdateInterrupts();
            }
        }

        private bool IsFeatureEnabled(FeatureBits feature)
        {
            return (driverFeatureBits & (ulong)feature) != 0;
        }

        private ushort availableIndex;
        private ushort availableIndexFromDriver;
        private ushort usedIndex;
        private ushort usedIndexForDriver;
        private long capacity;
        private ulong driverFeatureBits;
        private ulong virtqueueDescTableAddress;
        private ulong virtqueueAvailableAddress;
        private ulong virtqueueUsedAddress;
        private Stream storage;
        private IValueRegisterField deviceFeatureBitsIndex;
        private IValueRegisterField driverFeatureBitsIndex;
        private IFlagRegisterField hasUsedBuffer;
        private IFlagRegisterField configHasChanged;
        private IValueRegisterField virtqueueNotify;
        private IFlagRegisterField isVirtqueueReady;
        private IValueRegisterField virtqueueSize;
        private IValueRegisterField deviceStatus;

        private const uint MagicNumber = 0x74726976;
        private const uint Version = 0x2;
        private const uint DeviceID = 0x2;    // Block device ID
        private const uint VendorID = 0x1AF4; // Constant value taken from https://wiki.osdev.org/Virtio
        private const int SectorSize = 0x200;
        private const ulong DeviceFeatureBits = (ulong)FeatureBits.Version1 | (ulong)FeatureBits.BlockFlagFlush | (ulong)FeatureBits.BlockFlagConfigWCE;
        private const uint AvailableRingEntrySize = 0x2;
        private const uint UsedRingEntrySize = 0x8;
        private const uint DescriptorSize = 0x10;
        // Constant taken from https://docs.oasis-open.org/virtio/virtio/v1.1/csprd01/virtio-v1.1-csprd01.html#x1-5300013
        private const uint VirtqueueMaxSize = 1 << 15;

        private sealed class Request
        {
            public Request(VirtIOBlockDevice v, int startingIndex)
            {
                index = startingIndex;
                parent = v;
            }

            public bool ReadHeader()
            {
                ReadDescriptorMetadata();
                Type = (int)parent.machine.SystemBus.ReadDoubleWord(bufferAddress + (ulong)BlockRequestHeader.Type);
                sector = (long)(((ulong)parent.machine.SystemBus.ReadDoubleWord(bufferAddress + (ulong)BlockRequestHeader.SectorHigh)) << 32) |
                            (long)parent.machine.SystemBus.ReadDoubleWord(bufferAddress + (ulong)BlockRequestHeader.SectorLow);
                if(!SetNextIndex())
                {
                    parent.Log(LogLevel.Error, "NEXT flag isn't set in header descriptor. Descriptor info: index: {0}, Address: {1}, Flags: {2}", index, bufferAddress, flags);
                    return false;
                }
                return true;
            }

            public void WriteStatus()
            {
                ReadDescriptorMetadata();
                parent.machine.SystemBus.WriteByte(bufferAddress, status);
            }

            public bool Read()
            {
                ReadDescriptorMetadata();
                if((flags & (ushort)VirtqueueDescriptorFlags.Write) != 0)
                {
                    status = (byte)VirtioBlockRequestStatus.IoError;
                    parent.Log(LogLevel.Error, "IO Error: Trying to read from device-write buffer. Descriptor info: index: {0}, Address: {1}, Flags: {2}", index, bufferAddress, flags);
                }
                else
                {
                    SeekToSector();
                    var deviceBytes = parent.machine.SystemBus.ReadBytes(bufferAddress, length);
                    parent.storage.Write(deviceBytes, 0, length);
                    BytesProcessed += length;
                }

                if(!SetNextIndex())
                {
                    parent.Log(LogLevel.Error, "NEXT flag isn't set in device-write data buffer descriptor. Descriptor info: index: {0}, Address: {1}, Flags: {2}", index, bufferAddress, flags);
                    return false;
                }
                return true;
            }

            public bool Write()
            {
                ReadDescriptorMetadata();
                if((flags & (ushort)VirtqueueDescriptorFlags.Write) == 0)
                {
                    status = (byte)VirtioBlockRequestStatus.IoError;
                    parent.Log(LogLevel.Error, "IO Error: Trying to write to device-read buffer. Descriptor info: index: {0}, Address: {1}, Flags: {2}", index, bufferAddress, flags);
                }
                else
                {
                    SeekToSector();
                    var driverBytes = new byte[length];
                    parent.storage.Read(driverBytes, 0, length);
                    parent.machine.SystemBus.WriteBytes(driverBytes, bufferAddress, true);
                    BytesProcessed += length;
                }

                if(!SetNextIndex())
                {
                    parent.Log(LogLevel.Error, "NEXT flag isn't set in device-read data buffer descriptor. Descriptor info: index: {0}, Address: {1}, Flags: {2}", index, bufferAddress, flags);
                    return false;
                }
                return true;
            }

            //Flush command doesn't use any buffer, so there is no need to read metadata or set new index.
            public void Flush()
            {
                parent.storage.Flush();
            }

            public void MarkAsUnsupported()
            {
                status = (byte)VirtioBlockRequestStatus.Unsupported;
                parent.Log(LogLevel.Warning, "Block operation unsupported.");
            }

            public int BytesProcessed { get; private set; }
            public int Type { get; private set; }

            private void SeekToSector()
            {
                var positionToSeek = SectorSize * sector;
                if(positionToSeek >= parent.storage.Length)
                {
                    throw new RecoverableException("Driver tried to seek beyond the loaded image end.");
                }
                parent.storage.Seek(positionToSeek, SeekOrigin.Begin);
            }

            private void ReadDescriptorMetadata()
            {
                ulong descriptorAddress = parent.virtqueueDescTableAddress + DescriptorSize * (ulong)index;
                bufferAddress = (((ulong)parent.machine.SystemBus.ReadDoubleWord(descriptorAddress + (ulong)VirtqueueDescriptor.AddressHigh)) << 32) |
                    (ulong)parent.machine.SystemBus.ReadDoubleWord(descriptorAddress + (ulong)VirtqueueDescriptor.AddressLow);
                next = (int)parent.machine.SystemBus.ReadWord(descriptorAddress + (ulong)VirtqueueDescriptor.Next);
                length = (int)parent.machine.SystemBus.ReadDoubleWord(descriptorAddress + (ulong)VirtqueueDescriptor.Length);
                flags = (ushort)parent.machine.SystemBus.ReadWord(descriptorAddress + (ulong)VirtqueueDescriptor.Flags);
            }

            private bool SetNextIndex()
            {
                if((flags & (ushort)VirtqueueDescriptorFlags.Next) != 0)
                {
                    index = next;
                    return true;
                }
                return false;
            }

            private int index;
            private byte status;
            private long sector;
            private ulong bufferAddress;
            private int length;
            private ushort flags;
            private int next;
            private readonly VirtIOBlockDevice parent;
        }

        // Virtqueue structures offsets
        private enum VirtqueueDescriptor
        {
            AddressLow = 0x0,
            AddressHigh = 0x4,
            Length = 0x8,
            Flags = 0xc,
            Next = 0xe,
        }

        // Used and Available have the same structure. The main difference is type of elemnts used in ring arrays.
        // In Available it's only a 16bit number. In Used it's a structure described in UsedRing enum.
        private enum VirtqueueUsedAndAvailable
        {
            Flags = 0x0,
            Index = 0x2,
            Ring  = 0x4,
        }

        private enum UsedRing
        {
            Index = 0x0,
            Length = 0x4,
        }

        private enum BlockRequestHeader
        {
            Type = 0x0,
            SectorLow = 0x8,
            SectorHigh = 0xc,
        }

        private enum BlockOperations
        {
            In = 0,
            Out = 1,
            Flush = 4,
            Discard = 11,
            WriteZeroes = 13,
        }

        private enum VirtioBlockRequestStatus
        {
            Success = 0,
            IoError = 1,
            Unsupported = 2,
        }

        [Flags]
        private enum VirtqueueDescriptorFlags : uint
        {
            Next = 1 << 0,
            Write = 1 << 1,
            Indirect = 1 << 2,
        }

        [Flags]
        private enum Status : uint
        {
            Acknowledge = 1 << 0,
            Driver = 1 << 1,
            DriverOk = 1 << 2,
            FeaturesOk = 1 << 3,
            DeviceNeedsReset = 1 << 6,
            Failed = 1 << 7,
        }

        [Flags]
        private enum FeatureBits : ulong
        {
            // Block device specific flags
            BlockFlagSizeMax = 1UL << 1,
            BlockFlagSegmentsMaxNum = 1UL << 2,
            BlockFlagGeometry = 1UL << 4,
            BlockFlagReadOnly = 1UL << 5,
            BlockFlagBlockSize = 1UL << 6,
            BlockFlagFlush = 1UL << 9,
            BlockFlagTopology = 1UL << 10,
            BlockFlagConfigWCE = 1UL << 11,
            BlockFlagDiscard = 1UL << 13,
            BlockFlagWriteZeroes = 1UL << 14,

            // VirtIO MMIO device specific flags
            RingIndirectDescriptors = 1UL << 28,
            RingEventIndex = 1UL << 29,
            Version1 = 1UL << 32,
            AccessPlatform = 1UL << 33,
            RingPacket = 1UL << 34,
            InOrder = 1UL << 35,
            OrderPlatform = 1UL << 36,
            SingleRootIOVirtualization = 1UL << 37,
            NotificationData = 1UL << 38,
        }

        private enum Registers : long
        {
            MagicValue = 0x00,
            DeviceVersion = 0x04,
            DeviceID = 0x08,
            VendorID = 0x0c,
            DeviceFeatures = 0x10,
            DeviceFeaturesSelected = 0x14,
            DriverFeatures = 0x20,
            DriverFeaturesSelected = 0x24,
            VirtqueueSel = 0x30,
            VirtqueueSizeMax = 0x34,
            VirtqueueSize = 0x38,
            VirtqueueReady = 0x44,
            VirtqueueNotify = 0x50,
            InterruptStatus = 0x60,
            InterruptACK = 0x64,
            Status = 0x70,
            VirtqueueDescLow = 0x80,
            VirtqueueDescHigh = 0x84,
            VirtqueueDriverLow = 0x90,
            VirtqueueDriverHigh = 0x94,
            VirtqueueDeviceLow = 0xa0,
            VirtqueueDeviceHigh = 0xa4,
            ConfigGeneration = 0xfc,

            //Configuration space for block device
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
