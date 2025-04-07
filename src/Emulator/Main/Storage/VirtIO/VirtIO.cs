//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Storage.VirtIO
{
    public abstract class VirtIO : BasicDoubleWordPeripheral, IKnownSize
    {
        public VirtIO(IMachine machine) : base(machine) { }

        public void InterruptUsedBuffer()
        {
            hasUsedBuffer.Value = true;
            UpdateInterrupts();
        }

        public override void Reset()
        {
            // Ready bits in the QueueReady register for all queues in the device
            base.Reset();
            foreach(var vq in Virtqueues)
            {
                vq.Reset();
            }

            // Clear all bits in InterruptStatus
            UpdateInterrupts();
        }

        public abstract bool ProcessChain(Virtqueue vqueue);

        public Virtqueue[] Virtqueues { set; get; }

        public uint QueueSel { set; get; }

        public long Size => 0x150;

        public GPIO IRQ { get; } = new GPIO();

        public IBusController SystemBus => sysbus;

        protected void UpdateInterrupts()
        {
            var newVal = hasUsedBuffer.Value || configHasChanged.Value;
            this.Log(LogLevel.Debug, "Updating IRQ to {0}", newVal);
            IRQ.Set(newVal);
        }

        protected bool IsFeatureEnabled(byte feature)
        {
            return BitHelper.IsBitSet(driverFeatureBits, feature);
        }

        protected uint lastQueueIdx;
        protected ulong deviceFeatureBits;
        protected ulong driverFeatureBits;
        protected IFlagRegisterField deviceStatusAcknowledge;
        protected IFlagRegisterField deviceStatusDriver;
        protected IFlagRegisterField deviceStatusDriverOk;
        protected IFlagRegisterField deviceStatusFeaturesOk;
        protected IFlagRegisterField deviceStatusNeedsReset;
        protected IFlagRegisterField deviceStatusFailed;
        protected IFlagRegisterField deviceFeatureBitsIndex;
        protected IFlagRegisterField driverFeatureBitsIndex;
        protected IFlagRegisterField hasUsedBuffer;
        protected IFlagRegisterField configHasChanged;

        protected enum DeviceType
        {
            Reserved = 0,
            NetworkCard = 1,
            BlockDevice = 2,
            Console = 3,
            EntropySource = 4,
            MemoryBallooningTraditional = 5,
            IoMemory = 6,
            RPMSG = 7,
            SCSIHost = 8,
            NinePTransport = 9,
            Mac80211Wlan = 10,
            RPROCSerial = 11,
            VirtIOCAIF = 12,
            MemoryBalloon = 13,
            GPUDevice = 16,
            TimerClockDevice = 17,
            InputDevice = 18,
            SocketDevice = 19,
            CryptoDevice = 20,
            SignalDistributionModule = 21,
            PStoreDevice = 22,
            IOMMUDevice = 23,
            MemoryDevice = 24
        }
    }
}