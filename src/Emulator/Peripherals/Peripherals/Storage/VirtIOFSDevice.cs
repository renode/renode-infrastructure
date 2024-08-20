#if !PLATFORM_WINDOWS
//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Text;
using System;
using System.IO;
using Mono.Unix;
using System.Net.Sockets;
using Antmicro.Renode.Sockets;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Storage.VirtIO;

namespace Antmicro.Renode.Peripherals.Storage
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class VirtIOFSDevice : VirtIOMMIO, IDisposable
    {
        public VirtIOFSDevice(IMachine machine) : base(machine)
        {
            BitHelper.SetBit(ref deviceFeatureBits, (byte)FeatureBits.FSFlagNotification, true);
        }

        public void Dispose()
        {
            fsSocket?.Close();
        }

        public void Create(string fsSocketPath, string tag = "dummyfs",
                                uint numRequestQueues = 1, uint queueSize = 128)
        {
            if(String.IsNullOrEmpty(fsSocketPath))
            {
                throw new RecoverableException("Missing filesystem socket path");
            }
            if(String.IsNullOrEmpty(tag))
            {
                throw new RecoverableException("Missing tag property");
            }
            if(numRequestQueues == 0)
            {
                throw new RecoverableException("num-request-queues property must be larger than 0");
            }
            if(!Misc.IsPowerOfTwo(queueSize))
            {
                throw new RecoverableException("queue-size property must be a power of 2");
            }
            if(queueSize > Virtqueue.QueueMaxSize)
            {
                throw new RecoverableException(String.Format("queue-size property must be {0} or smaller", Virtqueue.QueueMaxSize));
            }

            this.Log(LogLevel.Debug, "Looking for UDS socket in path: {0}", Path.GetFullPath(fsSocketPath));
            fsSocket = SocketsManager.Instance.AcquireSocket(this, AddressFamily.Unix, SocketType.Stream, ProtocolType.IP, new UnixEndPoint(fsSocketPath), asClient: true);

            StoreTag(tag, this.tag);

            this.queueSize = queueSize;
            this.numRequestQueues = numRequestQueues;
            notifyBufSize = 2 * 255 + 2 * 48; //size of fuse_notify_fsnotify_out
            // https://docs.oasis-open.org/virtio/virtio/v1.2/csd01/virtio-v1.2-csd01.pdf#subsection.5.11.2
            lastQueueIdx = numRequestQueues + 1;
            Virtqueues = new Virtqueue[lastQueueIdx + 1];
            for (int i = 0; i <= lastQueueIdx; i++)
            {
                Virtqueues[i] = new Virtqueue(this, queueSize);
            }
            DefineRegisters();
            UpdateInterrupts();
        }

        public override bool ProcessChain(Virtqueue vqueue)
        {
            if(fsSocket == null)
            {
                return false;
            }
            // Read request from buffers //
            if(!vqueue.TryReadFromBuffers(FuseInHdrLen, out var fuseInHdr))
            {
                this.Log(LogLevel.Error, "Error reading FUSE request header");
                return false;
            }

            this.Log(LogLevel.Debug, "FUSE request header: {0}", Misc.PrettyPrintCollection(fuseInHdr));
            fsSocket.Send(fuseInHdr);

            var fuseInLen = BitConverter.ToInt32(fuseInHdr, 0);
            if(!vqueue.TryReadFromBuffers(fuseInLen - FuseInHdrLen, out var fuseInData))
            {
                this.Log(LogLevel.Error, "Error reading FUSE request data");
                return false;
            }
            this.Log(LogLevel.Debug, "FUSE request data: {0}", Misc.PrettyPrintCollection(fuseInData));
            fsSocket.Send(fuseInData);

            // Read response from UDS //
            var socketHdr = new byte[FuseOutHdrLen];
            fsSocket.Receive(socketHdr);
            this.Log(LogLevel.Debug, "FUSE response header: {0}", Misc.PrettyPrintCollection(socketHdr));
            if(!vqueue.TryWriteToBuffers(socketHdr))
            {
                this.Log(LogLevel.Error, "Error writing FUSE response header to buffer");
                return false;
            }

            var fuseOutLen = BitConverter.ToInt32(socketHdr, 0);
            if(fuseOutLen > FuseOutHdrLen)
            {
                byte[] socketData = new byte[fuseOutLen-FuseOutHdrLen];
                fsSocket.Receive(socketData);
                this.Log(LogLevel.Debug, "FUSE response data: {0}", Misc.PrettyPrintCollection(socketData));
                vqueue.TryWriteToBuffers(socketData);
            }
            return true;
        }

        protected override uint DeviceID => 26;

        private void StoreTag(string tag, byte[] container)
        {
            tag = tag.Substring(0, Math.Min((int)MaxTagLen, tag.Length));
            byte[] toCopy = Encoding.ASCII.GetBytes(tag);
            Array.Copy(toCopy, container, toCopy.Length);
        }

        private void DefineRegisters()
        {
            DefineMMIORegisters();
            Registers.Tag.DefineMany(this, 9, (reg, idx) =>
            {
                reg.WithValueFields(0, 8, 4, FieldMode.Read, name: "tag", valueProviderCallback: (i, _) => tag[idx * 4 + i]);
            });
            Registers.NumRequestQueues.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "num_virtqueues", valueProviderCallback: _ => numRequestQueues);
            Registers.NotifyBufSize.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "notify_buf_size", valueProviderCallback: _ => notifyBufSize);
        }

        private byte[] tag = new byte[MaxTagLen];
        private uint queueSize;
        private Socket fsSocket;
        private uint numRequestQueues;
        private uint notifyBufSize;

        private const int FuseInHdrLen = 40;
        private const int FuseOutHdrLen = 16;
        private const uint MaxTagLen = 36;

        private enum FeatureBits : byte
        {
            // File System device specific flags
            // https://docs.oasis-open.org/virtio/virtio/v1.2/csd01/virtio-v1.2-csd01.pdf#subsection.5.11.3
            FSFlagNotification = 0,
        }

        private enum Registers : long
        {
            // Configuration space for file system device
            // https://docs.oasis-open.org/virtio/virtio/v1.2/csd01/virtio-v1.2-csd01.pdf#subsubsection.5.11.4.1
            Tag = 0x100,
            NumRequestQueues = 0x124,
            NotifyBufSize = 0x128,
        }
    }
}
#endif
