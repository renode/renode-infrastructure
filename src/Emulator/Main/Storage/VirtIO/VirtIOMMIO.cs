//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Storage.VirtIO;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Storage
{
    public abstract class VirtIOMMIO : VirtIO
    {
        public VirtIOMMIO(IMachine machine) : base(machine)
        {
            BitHelper.SetBit(ref deviceFeatureBits, (byte)MMIOFeatureBits.Version1, true);
            BitHelper.SetBit(ref deviceFeatureBits, (byte)MMIOFeatureBits.AccessPlatform, true);
        }

        protected void VirtqueueHandle()
        {
            this.Log(LogLevel.Debug, "Handling virtqueue {0}", QueueSel);
            var vqueue = Virtqueues[QueueSel];
            vqueue.Handle();
        }

        protected void DefineMMIORegisters()
        {
            // General initialisation
            MMIORegisters.MagicValue.Define(this, MagicNumber)
                .WithValueField(0, 32, FieldMode.Read, name: "magic_value");

            MMIORegisters.DeviceVersion.Define(this, Version)
                .WithValueField(0, 32, FieldMode.Read, name: "dev_version");

            MMIORegisters.DeviceID.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "dev_id",
                    valueProviderCallback: _ => DeviceID);

            MMIORegisters.VendorID.Define(this, VendorID)
                .WithValueField(0, 16, FieldMode.Read, name: "vendor_id")
                .WithReservedBits(16, 16);

            MMIORegisters.Status.Define(this)
                .WithFlag(0, out deviceStatusAcknowledge, name: "status_acknowledge")
                .WithFlag(1, out deviceStatusDriver, name: "status_driver")
                .WithFlag(2, out deviceStatusDriverOk, name: "status_driver_ok")
                .WithFlag(3, out deviceStatusFeaturesOk, name: "status_features_ok")
                .WithReservedBits(4, 2)
                .WithFlag(6, out deviceStatusNeedsReset, name: "status_device_needs_reset")
                .WithFlag(7, out deviceStatusFailed, name: "status_failed")
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, val) => { if(val == 0) Reset(); });

            // Feature bits
            // Reading from this register returns 32 consecutive flag bits, the least signifi-
            // cant bit depending on the last value written to DeviceFeaturesSel. Access
            // to this register returns bits DeviceFeaturesSel ∗ 32 to (DeviceFeaturesSel ∗
            // 32)+31, e.g. feature bits from 0 to 31 if DeviceFeaturesSel is set to 0 and features
            // bits 32 to 63 if DeviceFeaturesSel is set to 1
            // Feature bits
            MMIORegisters.DeviceFeatures.Define(this)
               .WithValueField(0, 32, FieldMode.Read, name: "features", valueProviderCallback: _ =>
                   deviceFeatureBitsIndex.Value ? deviceFeatureBits >> 32 : deviceFeatureBits);

            MMIORegisters.DeviceFeaturesSelected.Define(this)
                .WithFlag(0, out deviceFeatureBitsIndex, FieldMode.Write, name: "features_sel")
                .WithReservedBits(1, 31);

            MMIORegisters.DriverFeatures.Define(this)
               .WithValueField(0, 32, FieldMode.Write, name: "guestbits", writeCallback: (_, val) =>
                    driverFeatureBits = (driverFeatureBitsIndex.Value ? (ulong)val << 32 : 0));

            MMIORegisters.DriverFeaturesSelected.Define(this)
                .WithFlag(0, out driverFeatureBitsIndex, FieldMode.Write, name: "guest_sel")
                .WithReservedBits(1, 31);

            // Virtual queue index
            // Writing to this register selects the virtual queue that the following op-
            // erations on QueueNumMax, QueueNum, QueueReady, QueueDescLow,
            // QueueDescHigh, QueueDriverlLow, QueueDriverHigh, QueueDeviceLow,
            // QueueDeviceHigh and QueueReset apply to. The index number of the first
            // queue is zero (0x0)
            MMIORegisters.QueueSel.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "queue_sel", writeCallback: (_, val) => QueueSel = (uint)val);

            MMIORegisters.QueueReady.Define(this)
                .WithFlag(0, name: "queue_ready",
                    writeCallback: (_, val) =>
                    {
                        if(QueueSel > lastQueueIdx)
                        {
                            return;
                        }
                        Virtqueues[QueueSel].IsReady = val;
                    },
                    valueProviderCallback: _ =>
                    {
                        if(QueueSel > lastQueueIdx)
                        {
                            return false;
                        }
                        return Virtqueues[QueueSel].IsReady;
                    }
                )
                .WithReservedBits(1, 31);

            MMIORegisters.QueueReset.Define(this)
                .WithFlag(0, name: "queue_reset",
                    writeCallback: (_, val) => { if(val) Virtqueues[QueueSel].Reset(); },
                    valueProviderCallback: _ => Virtqueues[QueueSel].IsReset
                )
                .WithReservedBits(1, 31);

            // Maximum virtual queue size
            // Reading from the register returns the maximum size (number of elements)
            // of the queue the device is ready to process or zero (0x0) if the queue is not
            // available
            // This applies to the queue selected by writing to QueueSel.
            MMIORegisters.QueueNumMax.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "queue_num_max",
                    valueProviderCallback: _ =>
                    {
                        if(QueueSel > lastQueueIdx)
                        {
                            deviceStatusFailed.Value = true;
                            return 0;
                        }
                        var vqueue = Virtqueues[QueueSel];
                        return (ulong)vqueue.maxSize;
                    });

            // Virtual queue size
            // Queue size is the number of elements in the queue. Writing to this register
            // notifies the device what size of the queue the driver will use. This applies
            // to the queue selected by writing to QueueSel
            MMIORegisters.QueueNum.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "queue_num",
                    writeCallback: (_, val) =>
                    {
                        Virtqueue vqueue = Virtqueues[QueueSel];
                        if(val > vqueue.maxSize)
                        {
                            this.Log(LogLevel.Error, "Virtqueue size exceeded max available value!");
                            deviceStatusFailed.Value = true;
                        }
                        Virtqueues[QueueSel].Size = val;
                    });

            // Interrupts registers
            MMIORegisters.InterruptStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "has_used_buffer", valueProviderCallback: _ => hasUsedBuffer.Value)
                .WithFlag(1, FieldMode.Read, name: "config_has_changed", valueProviderCallback: _ => configHasChanged.Value)
                .WithReservedBits(2, 30);

            MMIORegisters.InterruptACK.Define(this)
                .WithFlag(0, out hasUsedBuffer, FieldMode.WriteOneToClear, name: "has_used_buffer")
                .WithFlag(1, out configHasChanged, FieldMode.WriteOneToClear, name: "config_has_changed")
                .WithWriteCallback((_, __) => UpdateInterrupts())
                .WithReservedBits(2, 30);

            MMIORegisters.QueueDescLow.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "queue_desc_low", writeCallback: (_, val) =>
                    Virtqueues[QueueSel].DescTableAddress = BitHelper.SetBitsFrom((ulong) val, Virtqueues[QueueSel].DescTableAddress, 31, 32));

            MMIORegisters.QueueDescHigh.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "queue_desc_high", writeCallback: (_, val) =>
                    Virtqueues[QueueSel].DescTableAddress = BitHelper.SetBitsFrom((ulong)val << 32, Virtqueues[QueueSel].DescTableAddress, 0, 32));

            MMIORegisters.QueueDriverLow.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "queue_driver_low", writeCallback: (_, val) =>
                    Virtqueues[QueueSel].AvailableAddress = BitHelper.SetBitsFrom((ulong)val, Virtqueues[QueueSel].AvailableAddress, 31, 32));

            MMIORegisters.QueueDriverHigh.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "queue_driver_high", writeCallback: (_, val) =>
                    Virtqueues[QueueSel].AvailableAddress = BitHelper.SetBitsFrom((ulong)val << 32, Virtqueues[QueueSel].AvailableAddress, 0, 32));

            MMIORegisters.QueueDeviceLow.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "queue_device_low", writeCallback: (_, val) =>
                    Virtqueues[QueueSel].UsedAddress = BitHelper.SetBitsFrom((ulong)val, Virtqueues[QueueSel].UsedAddress, 31, 32));

            MMIORegisters.QueueDeviceHigh.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "queue_device_high", writeCallback: (_, val) =>
                    Virtqueues[QueueSel].UsedAddress = BitHelper.SetBitsFrom((ulong)val << 32, Virtqueues[QueueSel].UsedAddress, 0, 32));

            MMIORegisters.QueueNotify.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "queue_notifications",
                    writeCallback: (_, val) =>
                    {
                        var idx = val & 0xFFFF;
                        if(idx > lastQueueIdx)
                        {
                            this.Log(LogLevel.Error, "Tried to notify non-existent virtqueue");
                            return;
                        }
                        if(!Virtqueues[idx].IsReady)
                        {
                            this.Log(LogLevel.Error, "VirtIO driver started an operation, but current virtqueue isn't marked as ready.");
                        }
                        else if(!deviceStatusDriverOk.Value)
                        {
                            this.Log(LogLevel.Error, "VirtIO driver started an operation, but DriverOK flag not set in status register.");
                        }
                        else
                        {
                            VirtqueueHandle();
                        }
                    });

            MMIORegisters.ConfigGeneration.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "config_generation", valueProviderCallback: _ => 0x01)
                .WithReservedBits(8, 24);

            MMIORegisters.SHMSel.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "shm_sel", writeCallback: (_, val) => sharedMemoryId = val);

            MMIORegisters.SHMLenLow.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "shm_len_low", valueProviderCallback: _ =>
                {
                    if(sharedMemoryId != 0)
                    {
                        return 0xFFFFFFFF;
                    }
                    return sharedMemoryLength;
                });

            MMIORegisters.SHMLenHigh.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "shm_len_high", valueProviderCallback: _ =>
                {
                    if(sharedMemoryId != 0)
                    {
                        return 0xFFFFFFFF;
                    }
                    return sharedMemoryLength >> 32;
                });

            MMIORegisters.SHMBaseLow.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "shm_base_low", valueProviderCallback: _ =>
                {
                    if(sharedMemoryId != 0)
                    {
                        return 0xFFFFFFFF;
                    }
                    return sharedMemoryBase;
                });

            MMIORegisters.SHMBaseHigh.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "shm_base_high", valueProviderCallback: _ =>
                {
                    if(sharedMemoryId != 0)
                    {
                        return 0xFFFFFFFF;
                    }
                    return sharedMemoryBase >> 32;
                });
        }

        // Total number of request virtqueues exposed by the device
        protected ulong sharedMemoryId;
        protected ulong sharedMemoryLength;
        protected ulong sharedMemoryBase;
        protected int sharedMemoryFd;

        protected virtual uint DeviceID { get; }

        private const uint MagicNumber = 0x74726976;
        private const uint Version = 0x2;
        private const uint VendorID = 0x1AF4; // Constant value taken from https://wiki.osdev.org/Virtio

        [Flags]
        protected enum MMIOFeatureBits : byte
        {
            // VirtIO MMIO device specific flags
            // https://docs.oasis-open.org/virtio/virtio/v1.2/csd01/virtio-v1.2-csd01.pdf#chapter.6
            RingIndirectDescriptors = 28,
            RingEventIndex = 29,
            Version1 = 32,
            AccessPlatform = 33,
            RingPacket = 34,
            InOrder = 35,
            OrderPlatform = 36,
            SingleRootIOVirtualization = 37,
            NotificationData = 38,
            NotificationConfigData = 39,
        }

        private enum MMIORegisters : long
        {
            // Configuration space for MMIO device
            // https://docs.oasis-open.org/virtio/virtio/v1.2/csd01/virtio-v1.2-csd01.pdf#subsubsection.4.2.2.1
            MagicValue = 0x00,
            DeviceVersion = 0x04,
            DeviceID = 0x08,
            VendorID = 0x0c,
            DeviceFeatures = 0x10,
            DeviceFeaturesSelected = 0x14,
            DriverFeatures = 0x20,
            DriverFeaturesSelected = 0x24,
            QueueSel = 0x30,
            QueueNumMax = 0x34,
            QueueNum = 0x38,
            QueueReady = 0x44,
            QueueNotify = 0x50,
            InterruptStatus = 0x60,
            InterruptACK = 0x64,
            Status = 0x70,
            QueueDescLow = 0x80,
            QueueDescHigh = 0x84,
            QueueDriverLow = 0x90,
            QueueDriverHigh = 0x94,
            QueueDeviceLow = 0xa0,
            QueueDeviceHigh = 0xa4,
            SHMSel = 0xac,
            SHMLenLow = 0xb0,
            SHMLenHigh = 0xb4,
            SHMBaseLow = 0xb8,
            SHMBaseHigh = 0xbc,
            QueueReset = 0xc0,
            ConfigGeneration = 0xfc,
        }
    }
}
