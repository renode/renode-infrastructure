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
            if(writeCommandWrapper != null)
            {
                // this means that the previous command was Write and the current packet contains data to be written
                dataBackend.Position = ((dynamic)writeCommandDescriptor).LogicalBlockAddress * BlockSize;
                // TODO: what about TransferLength?
                dataBackend.Write(packet, 0, packet.Length);

                SendResult(writeCommandWrapper.Value);
                writeCommandWrapper = null;
                return;
            }

            if(!BulkOnlyTransportCommandBlockWrapper.TryParse(packet, out var commandBlockWrapper))
            {
                this.Log(LogLevel.Warning, "Broken SCSI command block wrapper detected. Ignoring it.");
                return;
            }

            var command = SCSICommandDescriptorBlock.DecodeCommand(packet, BulkOnlyTransportCommandBlockWrapper.CommandOffset);
            switch(command)
            {
                case SCSICommand.TestUnitReady:
                    SendResult(commandBlockWrapper);
                    break;
                case SCSICommand.Inquiry:
                    // TODO: here we should include Standard INQUIRY Data Format
                    deviceToHostEndpoint.HandlePacket(Enumerable.Repeat((byte)0, 36));
                    SendResult(commandBlockWrapper);
                    break;
                case SCSICommand.ReadCapacity:
                    // the response must be splitted into two packets
                    var result = new ReadCapcity10Result { BlockLengthInBytes = 512 };
                    deviceToHostEndpoint.HandlePacket(Packet.Encode(result));
                    SendResult(commandBlockWrapper);
                    break;
                case SCSICommand.Read10:
                    var cmd = Packet.DecodeDynamic<IReadWrite10Command>(packet, BulkOnlyTransportCommandBlockWrapper.CommandOffset);
                    dataBackend.Position = cmd.LogicalBlockAddress * BlockSize;
                    var data = dataBackend.ReadBytes((int)cmd.TransferLength * BlockSize);
                    deviceToHostEndpoint.HandlePacket(data);
                    SendResult(commandBlockWrapper, CommandStatus.Success, (uint)(commandBlockWrapper.Length - data.Length));
                    break;
                case SCSICommand.Write10:
                    // the actual write will be triggered after receiving the next packet with data
                    // we should not send result now
                    writeCommandWrapper = commandBlockWrapper;
                    writeCommandDescriptor = Packet.DecodeDynamic<IReadWrite10Command>(packet, BulkOnlyTransportCommandBlockWrapper.CommandOffset);
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unsupported SCSI command: {0}", command);
                    break;
            }
        }

        public void Reset()
        {
            // it will clear any pending data
            deviceToHostEndpoint.Reset();
        }

        public USBDeviceCore USBCore { get; }

        private void SendResult(BulkOnlyTransportCommandBlockWrapper commandBlockWrapper, CommandStatus status = CommandStatus.Success, uint dataResidue = 0)
        {
            var response = new CommandStatusWrapper(commandBlockWrapper.Tag, dataResidue, status);
            deviceToHostEndpoint.HandlePacket(Packet.Encode(response));
        }

        private USBEndpoint hostToDeviceEndpoint;
        private USBEndpoint deviceToHostEndpoint;
        private BulkOnlyTransportCommandBlockWrapper? writeCommandWrapper;
        private object writeCommandDescriptor;
        private readonly Stream dataBackend;

        private const int BlockSize = 512;
    }
}
