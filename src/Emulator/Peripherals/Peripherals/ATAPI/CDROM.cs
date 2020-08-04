﻿//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Storage.SCSI;
using Antmicro.Renode.Storage.SCSI.Commands;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.ATAPI
{
    //This device implements ATAPI CDROM
    public class CDROM : IAtapiPeripheral, IDisposable
    {
        public CDROM(string imageFile, bool persistent = false, uint? size = null, uint blockSize = 2048)
        {
            BlockSize = blockSize;
            dataBackend = DataStorage.Create(imageFile, size, persistent);

            var sizeMisalignment = dataBackend.Length % blockSize;
            if(sizeMisalignment != 0)
            {
                dataBackend.SetLength(dataBackend.Length + (blockSize - sizeMisalignment));
                this.Log(LogLevel.Warning, "Underlying data size extended by {0} bytes to align it to the block size ({1} bytes)", blockSize - sizeMisalignment, blockSize);
            }
        }

        public void Dispose()
        {
            dataBackend.Dispose();
        }

        public void Reset()
        {
            dataQueue.Clear();
            dataBackend.Position = 0;
        }

        public void SendIdentifyResponse()
        {
            //First byte of Identify device responser - sets device type
            var response = new byte[] {0x00, 0x05};
            QueueData(response);
        }

        public ushort DequeueData()
        {
            return dataQueue.TryDequeue(out var ret) ? ret : (ushort)0;
        }

        public void HandleCommand(byte[] packet)
        {
            var command = SCSICommandDescriptorBlock.DecodeCommand(packet, 0);
            this.Log(LogLevel.Debug, "Decoded command: {0}", command);
            switch(command)
            {
                case SCSICommand.TestUnitReady:
                    break;
                case SCSICommand.Inquiry:
                    // this is just an empty stub
                    QueueData(new byte[36]);
                    break;
                case SCSICommand.Read10:
                    var cmd = Packet.DecodeDynamic<IReadWrite10Command>(packet, 0);
                    this.Log(LogLevel.Debug, "Command args: LogicalBlockAddress: 0x{0:x}, TransferLength: {1}", (uint)cmd.LogicalBlockAddress, (ushort)cmd.TransferLength);
                    var bytesCount = (int)(cmd.TransferLength * BlockSize);
                    var readPosition = (long)cmd.LogicalBlockAddress * BlockSize;
                    dataBackend.Position = readPosition;
                    var data = dataBackend.ReadBytes(bytesCount);
                    this.Log(LogLevel.Debug, "Reading {0} bytes from address 0x{1:x}", bytesCount, readPosition);
                    QueueData(data);
                    break;
                case SCSICommand.ModeSense6:
                    // this is just an empty stub
                    QueueData(new byte[192]);
                    break;
                case SCSICommand.RequestSense:
                    // this is just an empty stub
                    QueueData(new byte[512]);
                    break;
                default:
                    this.Log(LogLevel.Error, "Unsupported SCSI command: {0}", command);
                    break;
            }
        }

        public uint BlockSize { get; }
        public bool DataReady { get { return dataQueue.Count != 0;}}

        private void QueueData(byte[] data)
        {
            var dataLength = data.Length;
            if(dataLength % 2 != 0)
            {
                this.Log(LogLevel.Error, "Trying to send odd number of bytes. Padding with zeros");
                data = data.Concat(new byte[] { 0 }).ToArray();
                dataLength += 1;
            }

            for(uint i = 0; i < dataLength - 1; i += 2)
            {
                var val = data[i] | (data[i + 1] << 8);
                dataQueue.Enqueue((ushort)val);
            }
        }

        private readonly Queue<ushort> dataQueue = new Queue<ushort>();

        private readonly Stream dataBackend;
    }
}

