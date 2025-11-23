//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Storage;
using Antmicro.Renode.Storage.VirtIO;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class VirtIOMMIOEntropy : VirtIOMMIO
    {
        public VirtIOMMIOEntropy(IMachine machine) : base(machine)
        {
            entropySource = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
            //entropy device only uses one queue
            Virtqueues = new Virtqueue[1];

            // The Spec limits the size of the queue at 32768,
            // the actual value used appears to be implementation specific however
            // This value is reflected in the QueueNum register of the virtio device
            // The actual queue size used will be negotiated using the QueueReady register
            // Since Renode deals with peripheral accesses immediately, we just chose some lower-range value here
            Virtqueues[0] = new Virtqueue(this, 128);

            DefineMMIORegisters();
        }

        public override bool ProcessChain(Virtqueue virtq)
        {
            // Place random bytes into the buffers
            // Get Buffer Size
            virtq.ReadDescriptorMetadata();
            var descriptor = virtq.Descriptor;
            var length = descriptor.Length;

            this.Log(LogLevel.Noisy, "Creating new Entropy Buffer with size {0}", length);
            var buff = new byte[length];

            entropySource.NextBytes(buff);

            return virtq.TryWriteToBuffers(buff);
        }

        protected override uint DeviceID { get; } = (uint)DeviceType.EntropySource;

        private readonly PseudorandomNumberGenerator entropySource;
    }
}