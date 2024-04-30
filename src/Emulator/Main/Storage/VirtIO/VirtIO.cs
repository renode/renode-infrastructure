//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Storage.VirtIO
{
    public abstract class VirtIO : BasicDoubleWordPeripheral, IKnownSize
    {
        public VirtIO(IMachine machine) : base(machine) {}

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

        protected void UpdateInterrupts()
        {
            var newVal = hasUsedBuffer.Value || configHasChanged.Value;
            this.Log(LogLevel.Debug, "Updating IRQ to {0}", newVal);
            IRQ.Set(newVal);
        }

        public Virtqueue[] Virtqueues { set; get; }
        public uint QueueSel { set; get; }
        public long Size => 0x150;
        public GPIO IRQ { get; } = new GPIO();

        public IBusController SystemBus => sysbus;

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
    }
}
