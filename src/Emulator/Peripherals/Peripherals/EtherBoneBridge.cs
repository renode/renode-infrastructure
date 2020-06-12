//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class EtherBoneBridge : IDoubleWordPeripheral, IDisposable, IAbsoluteAddressAware
    {
        public EtherBoneBridge(int port, string host = "127.0.0.1")
        {
            try
            {
                connection = new TcpClient(host, port);
                dataStream = connection.GetStream();
            }
            catch(SocketException)
            {
                throw new ConstructionException(string.Format("Could not connect to EtherBone server at {0}:{1}", host, port));
            }

            ebRecord.Magic = 0x4e6f; // this is a constant
            ebRecord.VersionAndFlags = 0x10; // version: 1, all other flags: 0
            ebRecord.AddressAndPortWidth = 0x44; // address width: 32 bits, port width: 32 bits

            ebRecord.WishboneFlags = 0x0; // no wishbone flags
            ebRecord.ByteEnable = 0xf; // enable all 4 bytes
        }

        public void Reset()
        {
            // intentionally do nothing
        }

        public void Dispose()
        {
            dataStream.Close();
            connection.Close();
        }

        public void SetAbsoluteAddress(ulong address)
        {
            absoluteAddress = address;
        }

        public uint ReadDoubleWord(long offset)
        {
            if(!TryRead((uint)absoluteAddress, out var result))
            {
                this.Log(LogLevel.Warning, "Could not read from offset 0x{0:X}. Check your connection with EtherBone server", offset);
            }

            return result;
        }

        public void WriteDoubleWord(long offset, uint val)
        {
            if(!TryWrite((uint)absoluteAddress, val))
            {
                this.Log(LogLevel.Warning, "Could not write value 0x{0:X} to offset 0x{1:X}. Check your connection with EtherBone server", val, offset);
            }
        }

        private bool TryRead(uint offset, out uint result)
        {
            ebRecord.WritesCount = 0;
            ebRecord.ReadsCount = 1;

            ebRecord.WriteValueReadAddress = offset;

            var bytes = EtherBoneRecordToBytes(ebRecord);

            try
            {
                dataStream.Write(bytes, 0, bytes.Length);
            }
            catch(IOException)
            {
                result = 0;
                return false;
            }

            var replyBytes = new byte[RecordSize];
            if(dataStream.Read(replyBytes, 0, replyBytes.Length) != RecordSize)
            {
                result = 0;
                return false;
            }

            var reply = EtherBoneRecordFromBytes(replyBytes);
            result = reply.ReadValueWriteAddress;
            return true;
        }

        private bool TryWrite(uint offset, uint val)
        {
            ebRecord.WritesCount = 1;
            ebRecord.ReadsCount = 0;

            ebRecord.ReadValueWriteAddress = offset;
            ebRecord.WriteValueReadAddress = val;

            var bytes = EtherBoneRecordToBytes(ebRecord);

            try
            {
                dataStream.Write(bytes, 0, bytes.Length);
                return true;
            }
            catch(IOException)
            {
                return false;
            }
        }

        private byte[] EtherBoneRecordToBytes(EtherBoneRecord ebRecord)
        {
            var size = Marshal.SizeOf(ebRecord);
            var result = new byte[size];

            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(ebRecord, ptr, true);
            Marshal.Copy(ptr, result, 0, size);
            Marshal.FreeHGlobal(ptr);

            FixFieldEndianess(result);

            return result;
        }

        private EtherBoneRecord EtherBoneRecordFromBytes(byte[] bytes)
        {
            FixFieldEndianess(bytes);

            var size = Marshal.SizeOf(typeof(EtherBoneRecord));
            var ptr = Marshal.AllocHGlobal(size);

            Marshal.Copy(bytes, 0, ptr, size);

            var result = (EtherBoneRecord)Marshal.PtrToStructure(ptr, typeof(EtherBoneRecord));
            Marshal.FreeHGlobal(ptr);

            return result;
        }

        // .NET built-in struct packaging mechanism
        // does not allow to define field's endianess,
        // so we have to swap bytes of some fields
        private void FixFieldEndianess(byte[] arr)
        {
            // swap Magic
            Misc.SwapElements(arr, 0, 1);

            // swap ReadValueWriteAddress
            Misc.SwapElements(arr, 12, 15);
            Misc.SwapElements(arr, 13, 14);

            // swap WriteValueReadAddress
            Misc.SwapElements(arr, 16, 19);
            Misc.SwapElements(arr, 17, 18);
        }

        private ulong absoluteAddress;
        private EtherBoneRecord ebRecord;

        private readonly NetworkStream dataStream;
        private readonly TcpClient connection;

        [StructLayout(LayoutKind.Explicit, Size=RecordSize)]
        private struct EtherBoneRecord
        {
            // here we have a problem with byte order - see FixFieldEndianess
            [FieldOffset(0)] public ushort Magic;
            [FieldOffset(2)] public byte VersionAndFlags;
            [FieldOffset(3)] public byte AddressAndPortWidth;

            [FieldOffset(8)] public byte WishboneFlags;
            [FieldOffset(9)] public byte ByteEnable;

            [FieldOffset(10)] public byte WritesCount;
            [FieldOffset(11)] public byte ReadsCount;

            [FieldOffset(12)] public uint ReadValueWriteAddress;
            [FieldOffset(16)] public uint WriteValueReadAddress;
        }

        private const int RecordSize = 20;
    }
}
