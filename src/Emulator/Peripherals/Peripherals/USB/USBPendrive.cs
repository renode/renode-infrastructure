//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Core.USB.MSC;
using Antmicro.Renode.Core.USB.MSC.BOT;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Storage.SCSI;
using Antmicro.Renode.Storage.SCSI.Commands;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.USB
{
    public class USBPendrive : IUSBDevice, IDisposable
    {
        public USBPendrive(string imageFile, long? size = null, bool persistent = false)
        {
            dataBackend = DataStorage.Create(imageFile, size, persistent);

            USBCore = new USBDeviceCore(this)
                .WithConfiguration(configure: c => c.WithInterface<Core.USB.MSC.Interface>(
                    (byte)Core.USB.MSC.Subclass.ScsiTransparentCommandSet,
                    (byte)Core.USB.MSC.Protocol.BulkOnlyTransport,
                    configure: x =>
                        x.WithEndpoint(
                            Direction.HostToDevice,
                            EndpointTransferType.Bulk,
                            BlockSize,
                            0x10,
                            out hostToDeviceEndpoint)
                        .WithEndpoint(
                            Direction.DeviceToHost,
                            EndpointTransferType.Bulk,
                            BlockSize,
                            0x10,
                            out deviceToHostEndpoint))
                );

                hostToDeviceEndpoint.DataWritten += HandleData;
        }

        public void Dispose()
        {
            dataBackend.Dispose();
        }

        public void HandleData(byte[] packet)
        {
            this.Log(LogLevel.Debug, "Received {0} bytes of data.", packet.Length);
            if(writeCommandWrapper != null)
            {
                var cmd = (dynamic)writeCommandDescriptor;
                this.Log(LogLevel.Debug, "In write mode. Command args: LogicalBlockAddress: 0x{0:x}, TransferLength: {1}", (uint)cmd.LogicalBlockAddress, (ushort)cmd.TransferLength);
                // this means that the previous command was Write and the current packet contains data to be written
                var position = (long)cmd.LogicalBlockAddress * BlockSize;
                dataBackend.Position = position;
                if((cmd.TransferLength * BlockSize) != packet.Length)
                {
                    this.Log(LogLevel.Warning, "Lengths inconsistency: received {0} bytes of data, but declared TransferLength was {1} blocks (i.e., {2} bytes)", packet.Length, (uint)cmd.TransferLength, (uint)cmd.TransferLength * BlockSize);
                }
                dataBackend.Write(packet, 0, packet.Length);

                this.Log(LogLevel.Debug, "In write mode. Written {0} bytes at 0x{0}", packet.Length, position);
                SendResult(writeCommandWrapper.Value);
                writeCommandWrapper = null;
                return;
            }

            if(!BulkOnlyTransportCommandBlockWrapper.TryParse(packet, out var commandBlockWrapper))
            {
                this.Log(LogLevel.Warning, "Broken SCSI command block wrapper detected. Ignoring it.");
                return;
            }

            this.Log(LogLevel.Noisy, "Parsed command block wrapper: {0}", commandBlockWrapper);
            var command = SCSICommandDescriptorBlock.DecodeCommand(packet, BulkOnlyTransportCommandBlockWrapper.CommandOffset);
            this.Log(LogLevel.Noisy, "Decoded command: {0}", command);
            switch(command)
            {
                case SCSICommand.TestUnitReady:
                    SendResult(commandBlockWrapper);
                    break;
                case SCSICommand.Inquiry:
                    // TODO: here we should include Standard INQUIRY Data Format
                    SendData(Enumerable.Repeat((byte)0, 36).ToArray());
                    SendResult(commandBlockWrapper);
                    break;
                case SCSICommand.ReadCapacity:
                    var result = new ReadCapcity10Result
                    {
                        BlockLengthInBytes = BlockSize,
                    };
                    SendData(Packet.Encode(result));
                    SendResult(commandBlockWrapper);
                    break;
                case SCSICommand.Read10:
                    var cmd = Packet.DecodeDynamic<IReadWrite10Command>(packet, BulkOnlyTransportCommandBlockWrapper.CommandOffset);
                    this.Log(LogLevel.Noisy, "Command args: LogicalBlockAddress: 0x{0:x}, TransferLength: {1}", (uint)cmd.LogicalBlockAddress, (ushort)cmd.TransferLength);
                    var bytesCount = (int)cmd.TransferLength * BlockSize;
                    var readPosition = (long)cmd.LogicalBlockAddress * BlockSize;
                    dataBackend.Position = readPosition;
                    var data = dataBackend.ReadBytes(bytesCount);
                    this.Log(LogLevel.Noisy, "Reading {0} bytes from address 0x{1:x}", bytesCount, readPosition);
                    SendData(data);
                    SendResult(commandBlockWrapper, CommandStatus.Success, (uint)(commandBlockWrapper.DataTransferLength - data.Length));
                    break;
                case SCSICommand.Write10:
                    // the actual write will be triggered after receiving the next packet with data
                    // we should not send result now
                    writeCommandWrapper = commandBlockWrapper;
                    writeCommandDescriptor = Packet.DecodeDynamic<IReadWrite10Command>(packet, BulkOnlyTransportCommandBlockWrapper.CommandOffset);
                    this.Log(LogLevel.Debug, "Entering write mode and waiting for the actual data");
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unsupported SCSI command: {0}", command);
                    break;
            }
        }

        public void Reset()
        {
            USBCore.Reset();
        }

        public USBDeviceCore USBCore { get; }

        private void SendResult(BulkOnlyTransportCommandBlockWrapper commandBlockWrapper, CommandStatus status = CommandStatus.Success, uint dataResidue = 0)
        {
            var response = new CommandStatusWrapper(commandBlockWrapper.Tag, dataResidue, status);
            this.Log(LogLevel.Debug, "Sending result: {0}", response);
            deviceToHostEndpoint.HandlePacket(Packet.Encode(response));
        }

        private void SendData(byte[] data)
        {
            this.Log(LogLevel.Debug, "Sending data of length {0}: [{1}]", data.Length, data.Select(x => "0x{0:x}".FormatWith(x)).Stringify());
            deviceToHostEndpoint.HandlePacket(data);
        }

        private USBEndpoint hostToDeviceEndpoint;
        private USBEndpoint deviceToHostEndpoint;
        private BulkOnlyTransportCommandBlockWrapper? writeCommandWrapper;
        private object writeCommandDescriptor;
        private readonly Stream dataBackend;

        private const int BlockSize = 512;
    }
}
