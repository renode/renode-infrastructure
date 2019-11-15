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
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Core.USB.MSC;
using Antmicro.Renode.Core.USB.MSC.BOT;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Extensions.Utilities.USBIP;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Storage.SCSI;
using Antmicro.Renode.Storage.SCSI.Commands;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.USB
{
    public static class USBPendriveExtensions
    {
        public static void PendriveFromFile(this Machine machine, string file, string name, IPeripheralRegister<IUSBDevice, NumberRegistrationPoint<int>> attachTo, int port, bool persistent = true)
        {
            var pendrive = new USBPendrive(file, persistent: persistent);
            attachTo.Register(pendrive, new NumberRegistrationPoint<int>(port));
            machine.SetLocalName(pendrive, name);
        }

        public static void PendriveFromFile(this USBIPServer usbController, string file, bool persistent = true, int? port = null)
        {
            var pendrive = new USBPendrive(file, persistent: persistent);
            usbController.Register(pendrive, port);
        }
    }

    public class USBPendrive : IUSBDevice, IDisposable
    {
        public USBPendrive(string imageFile, long? size = null, bool persistent = false, uint blockSize = 512)
        {
            BlockSize = blockSize;
            dataBackend = DataStorage.Create(imageFile, size, persistent);

            if(dataBackend.Length % blockSize != 0)
            {
                this.Log(LogLevel.Warning, "Underlying data size ({0} bytes) is not aligned to the block size ({1} bytes)", dataBackend.Length, blockSize);
            }

            USBCore = new USBDeviceCore(this)
                .WithConfiguration(configure: c => c.WithInterface<Core.USB.MSC.Interface>(
                    (byte)Core.USB.MSC.Subclass.ScsiTransparentCommandSet,
                    (byte)Core.USB.MSC.Protocol.BulkOnlyTransport,
                    configure: x =>
                        x.WithEndpoint(
                            Direction.HostToDevice,
                            EndpointTransferType.Bulk,
                            MaximumPacketSize,
                            0x10,
                            out hostToDeviceEndpoint)
                        .WithEndpoint(
                            Direction.DeviceToHost,
                            EndpointTransferType.Bulk,
                            MaximumPacketSize,
                            0x10,
                            out deviceToHostEndpoint))
                );

                hostToDeviceEndpoint.DataWritten += HandleInput;
        }

        public void Dispose()
        {
            dataBackend.Dispose();
        }

        public void HandleInput(byte[] packet)
        {
            this.Log(LogLevel.Debug, "Received a packet of {0} bytes in {1} mode.", packet.Length, mode);
            switch(mode)
            {
                case Mode.Command:
                    HandleCommand(packet);
                    break;

                case Mode.Data:
                    HandleData(packet);
                    break;

                default:
                    throw new ArgumentException($"Unexpected mode: {mode}");
            }
        }

        public void Reset()
        {
            USBCore.Reset();
            mode = Mode.Command;
            dataBackend.Position = 0;
            bytesToWrite = 0;
            writeCommandDescriptor = null;
        }

        public USBDeviceCore USBCore { get; }
        public uint BlockSize { get; }

        private void SendResult(BulkOnlyTransportCommandBlockWrapper commandBlockWrapper, CommandStatus status = CommandStatus.Success, uint dataResidue = 0)
        {
            var response = new CommandStatusWrapper(commandBlockWrapper.Tag, dataResidue, status);
            this.Log(LogLevel.Debug, "Sending result: {0}", response);
            deviceToHostEndpoint.HandlePacket(Packet.Encode(response));
        }

        private void SendData(byte[] data)
        {
            this.Log(LogLevel.Debug, "Sending data of length {0}.", data.Length);
            deviceToHostEndpoint.HandlePacket(data);
        }

        private void HandleData(byte[] packet)
        {
            if(packet.Length > bytesToWrite)
            {
                this.Log(LogLevel.Warning, "Received more data ({0} bytes) than expected ({1} bytes). Aborting the operation", packet.Length, bytesToWrite);
                SendResult(writeCommandWrapper, CommandStatus.Failure);
                return;
            }

            this.Log(LogLevel.Noisy, "Writing {0} bytes of data at address 0x{1:x}", packet.Length, dataBackend.Position);
            dataBackend.Write(packet, 0, packet.Length);
            bytesToWrite -= (uint)packet.Length;
            if(bytesToWrite == 0)
            {
                SendResult(writeCommandWrapper);
                this.Log(LogLevel.Noisy, "All data written, switching to Command mode");
                mode = Mode.Command;
            }
        }

        private void HandleCommand(byte[] packet)
        {
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
                    // this is just an empty stub
                    SendData(new byte[36]);
                    SendResult(commandBlockWrapper);
                    break;
                case SCSICommand.ReadCapacity:
                    var result = new ReadCapcity10Result
                    {
                        BlockLengthInBytes = BlockSize,
                        ReturnedLogicalBlockAddress = (uint)(dataBackend.Length / BlockSize - 1)
                    };
                    SendData(Packet.Encode(result));
                    SendResult(commandBlockWrapper);
                    break;
                case SCSICommand.Read10:
                    var cmd = Packet.DecodeDynamic<IReadWrite10Command>(packet, BulkOnlyTransportCommandBlockWrapper.CommandOffset);
                    this.Log(LogLevel.Noisy, "Command args: LogicalBlockAddress: 0x{0:x}, TransferLength: {1}", (uint)cmd.LogicalBlockAddress, (ushort)cmd.TransferLength);
                    var bytesCount = (int)(cmd.TransferLength * BlockSize);
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
                    var position = (long)((dynamic)writeCommandDescriptor).LogicalBlockAddress * BlockSize;
                    dataBackend.Position = position;
                    bytesToWrite = (uint)((dynamic)writeCommandDescriptor).TransferLength * BlockSize;
                    this.Log(LogLevel.Noisy, "Preparing to write {1} bytes of data at address: 0x{0:x}", dataBackend.Position, bytesToWrite);
                    mode = Mode.Data;
                    break;
                case SCSICommand.ModeSense6:
                    // this is just an empty stub
                    SendData(new byte[192]);
                    SendResult(commandBlockWrapper);
                    break;
                case SCSICommand.RequestSense:
                    // this is just an empty stub
                    SendData(new byte[commandBlockWrapper.DataTransferLength]);
                    SendResult(commandBlockWrapper);
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unsupported SCSI command: {0}", command);
                    SendResult(commandBlockWrapper, CommandStatus.Failure, commandBlockWrapper.DataTransferLength);
                    break;
            }
        }

        private uint bytesToWrite;
        private Mode mode;
        private USBEndpoint hostToDeviceEndpoint;
        private USBEndpoint deviceToHostEndpoint;
        private BulkOnlyTransportCommandBlockWrapper writeCommandWrapper;
        private object writeCommandDescriptor;
        private readonly Stream dataBackend;

        // 64 is a maximum value for USB 2.0 low/full-speed devices;
        // 512 is allowed only for high-speed devices that
        // might not be supported by all USB host controllers (i.e., MAX3421E)
        private const int MaximumPacketSize = 64;

        private enum Mode
        {
            Command,
            Data
        }
    }
}
