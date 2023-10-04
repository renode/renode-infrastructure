//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SD
{
    public class PULP_uDMA_SDIO : NullRegistrationPointPeripheralContainer<SDCard>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public PULP_uDMA_SDIO(IMachine machine) : base(machine)
        {
            sysbus = machine.GetSystemBus(this);
            response = new IValueRegisterField[ResponseBytes / 4];
            DefineRegisters();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x80;

        private void DefineRegisters()
        {
            Registers.RxBufferAddress.Define(this)
                .WithValueField(0, 32, out rxBufferAddress, name: "SDIO_RX_SADDR")
            ;

            Registers.RxBufferSize.Define(this)
                .WithValueField(0, 20, out rxBufferSize, name: "SDIO_RX_SIZE")
                .WithReservedBits(20, 12)
            ;

            Registers.RxConfiguration.Define(this)
                .WithTag("CONTINOUS", 0, 1)
                .WithTag("DATASIZE", 1, 2)
                .WithFlag(4, out rxBufferEnabled, name: "EN")
                .WithTag("PENDING", 5, 1)
                .WithTag("CLR", 6, 1)
                .WithReservedBits(7, 25)
            ;

            Registers.TxBufferAddress.Define(this)
                .WithValueField(0, 32, out txBufferAddress, name: "SDIO_TX_SADDR")
            ;

            Registers.TxBufferSize.Define(this)
                .WithValueField(0, 20, out txBufferSize, name: "SDIO_TX_SIZE")
                .WithReservedBits(20, 12)
            ;

            Registers.TxConfiguration.Define(this)
                .WithTag("CONTINOUS", 0, 1)
                .WithTag("DATASIZE", 1, 2)
                .WithFlag(4, out txBufferEnabled, name: "EN")
                .WithTag("PENDING", 5, 1)
                .WithTag("CLR", 6, 1)
                .WithReservedBits(7, 25)
            ;

            Registers.Command.Define(this)
                .WithEnumField<DoubleWordRegister, ResponseType>(0, 3, out responseType, FieldMode.Write, name: "CMD_RSP_TYPE")
                .WithReservedBits(3, 5)
                .WithValueField(8, 6, out commandOperation, FieldMode.Write, name: "CMD_OP")
                .WithReservedBits(8, 24)
            ;

            Registers.CommandArgument.Define(this)
                .WithValueField(0, 32, out commandArgument, FieldMode.Write, name: "CMD_ARG")
            ;

            Registers.DataSetup.Define(this)
                .WithFlag(0, out dataEnabled, name: "DATA_EN")
                .WithFlag(1, out dataRead, name: "DATA_RWN")
                .WithTag("DATA_QUAD", 2, 1)
                .WithReservedBits(3, 5)
                .WithValueField(8, 8, out numberOfBlocks, name: "BLOCK_NUM")
                .WithValueField(16, 10, out blockSize, name: "BLOCK_SIZE")
                .WithReservedBits(26, 6)
            ;

            Registers.Start.Define(this)
                .WithFlag(0, FieldMode.Write, name: "START", writeCallback: (_, val) =>
                {
                    if(!val)
                    {
                        return;
                    }

                    var device = RegisteredPeripheral;
                    if(device == null)
                    {
                        this.Log(LogLevel.Warning, "Tried to start a communication, but no device is currently attached");
                        return;
                    }

                    SendCommand(device);

                    if(dataEnabled.Value)
                    {
                        if(dataRead.Value)
                        {
                            if(!rxBufferEnabled.Value)
                            {
                                this.Log(LogLevel.Warning, "Tried to read data from the device, but the DMA RX channel is disabled");
                                return;
                            }

                            ReadFromDevice(device);
                        }
                        else
                        {
                            if(!txBufferEnabled.Value)
                            {
                                this.Log(LogLevel.Warning, "Tried to write data to the device, but the DMA TX channel is disabled");
                                return;
                            }

                            WriteDataToDevice(device);
                        }
                    }

                    endOfTransfer.Value = true;
                })
                .WithReservedBits(1, 31)
            ;

            Registers.Response0.Define(this)
                .WithValueField(0, 32, out response[0], FieldMode.Read, name: "SDIO_RSP0")
            ;

            Registers.Response1.Define(this)
                .WithValueField(0, 32, out response[1], FieldMode.Read, name: "SDIO_RSP1")
            ;

            Registers.Response2.Define(this)
                .WithValueField(0, 32, out response[2], FieldMode.Read, name: "SDIO_RSP2")
            ;
            
            Registers.Response3.Define(this)
                .WithValueField(0, 32, out response[3], FieldMode.Read, name: "SDIO_RSP3")
            ;

            Registers.Status.Define(this)
                .WithFlag(0, out endOfTransfer, name: "EOT")
                .WithTag("ERROR", 1, 1)
                .WithReservedBits(2, 14)
                .WithTag("CMD_ERR_STATUS", 16, 6)
                .WithReservedBits(22, 2)
                .WithTag("DATA_ERR_STATUS", 24, 6)
                .WithReservedBits(30, 2)
            ;

            Registers.StopCommand.Define(this)
                .WithTag("STOPCMD_RSP_TYPE", 0, 3)
                .WithReservedBits(3, 5)
                .WithTag("STOPCMD_OP", 8, 6)
                .WithReservedBits(14, 18)
            ;

            Registers.StopCommand.Define(this)
                .WithTag("STOPCMD_ARG", 0, 32)
            ;

            Registers.ClockDivider.Define(this)
                .WithTag("CLK_DIV", 0, 9)
                .WithReservedBits(9, 23)
            ;
        }

        private void SendCommand(SDCard device)
        {
            response[0].Value = 0;
            response[1].Value = 0;
            response[2].Value = 0;
            response[3].Value = 0;

            var commandResult = device.HandleCommand((uint)commandOperation.Value, (uint)commandArgument.Value);
            switch(responseType.Value)
            {
                case ResponseType.None:
                    if(commandResult.Length != 0)
                    {
                        this.Log(LogLevel.Warning, "Expected no response, but {0} bits received", commandResult.Length);
                        return;
                    }
                    break;
                case ResponseType.Bits136:
                    // our response does not contain 8 bits:
                    // * start bit
                    // * transmission bit
                    // * command index / reserved bits (6 bits)
                    if(commandResult.Length != 128)
                    {
                        this.Log(LogLevel.Warning, "Unexpected response of length 128 bits (excluding control bits), but {0} received", commandResult.Length);
                        return;
                    }
                    // the following bits are considered a part of returned register, but are not included in the response buffer:
                    // * CRC7 (7 bits)
                    // * end bit
                    // that's why we are skipping the initial 8-bits
                    response[0].Value = commandResult.AsUInt32(8);
                    response[1].Value = commandResult.AsUInt32(40);
                    response[2].Value = commandResult.AsUInt32(72);
                    response[3].Value = commandResult.AsUInt32(104, 24);
                    break;
                case ResponseType.Bits48WithCRC:
                case ResponseType.Bits48NoCRC:
                case ResponseType.Bits48WithBusyCheck:
                    // our response does not contain 16 bits:
                    // * start bit
                    // * transmission bit
                    // * command index / reserved bits (6 bits)
                    // * CRC7 (7 bits)
                    // * end bit
                    if(commandResult.Length != 32)
                    {
                        this.Log(LogLevel.Warning, "Expected a response of length {0} bits (excluding control bits and CRC), but {1} received", 32, commandResult.Length);
                        return;
                    }
                    response[0].Value = commandResult.AsUInt32();
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unexpected response type selected: {0}. Ignoring the command response.", responseType.Value);
                    return;
            }                    
        }

        private void ReadFromDevice(SDCard device)
        {
            var bytesToReadFromDevice = numberOfBlocks.Value * blockSize.Value;
            if(bytesToReadFromDevice != rxBufferSize.Value)
            {
                this.Log(LogLevel.Warning, "There seems to be an inconsistency between the number of bytes to read from the device ({0}) and the number of bytes to copy to the memory ({1})", bytesToReadFromDevice, rxBufferSize.Value);
            }

            var data = device.ReadData((uint)bytesToReadFromDevice);
            if((int)rxBufferSize.Value < data.Length)
            {
                data = data.Take((int)rxBufferSize.Value).ToArray();
            }
            
            sysbus.WriteBytes(data, (ulong)rxBufferAddress.Value);
            this.Log(LogLevel.Noisy, "Copied {0} bytes from the device to 0x{1:X}", data.Length, rxBufferAddress.Value);
        }

        private void WriteDataToDevice(SDCard device)
        {
            var bytesToWriteToDevice = numberOfBlocks.Value * blockSize.Value;
            if(bytesToWriteToDevice != txBufferSize.Value)
            {
                this.Log(LogLevel.Warning, "There seems to be an inconsistency between the number of bytes to write to the device ({0}) and the number of bytes to copy from the memory ({1})", bytesToWriteToDevice, txBufferSize.Value);
            }

            var data = sysbus.ReadBytes((ulong)txBufferAddress.Value, (int)txBufferSize.Value);
            if((int)bytesToWriteToDevice < data.Length)
            {
                data = data.Take((int)txBufferSize.Value).ToArray();
            }
            device.WriteData(data);

            this.Log(LogLevel.Noisy, "Copied {0} bytes from 0x{1:X} to the device", data.Length, txBufferAddress.Value);
        }

        private IFlagRegisterField endOfTransfer;

        private IValueRegisterField numberOfBlocks;
        private IValueRegisterField blockSize;

        private IFlagRegisterField dataEnabled;
        private IFlagRegisterField dataRead;

        private IValueRegisterField rxBufferAddress;
        private IValueRegisterField rxBufferSize;
        private IFlagRegisterField rxBufferEnabled;

        private IValueRegisterField txBufferAddress;
        private IValueRegisterField txBufferSize;
        private IFlagRegisterField txBufferEnabled;

        private IEnumRegisterField<ResponseType> responseType;
        private IValueRegisterField commandOperation;
        private IValueRegisterField commandArgument;

        private readonly IBusController sysbus;
        private readonly IValueRegisterField[] response;

        private const int ResponseBytes = 16;

        private enum ResponseType
        {
            None = 0x0,
            Bits48WithCRC = 0x1,
            Bits48NoCRC = 0x2,
            Bits136 = 0x3,
            Bits48WithBusyCheck = 0x4
        }

        private enum Registers
        {
            RxBufferAddress = 0x0,
            RxBufferSize = 0x4,
            RxConfiguration = 0x8,
            TxBufferAddress = 0x10,
            TxBufferSize = 0x14,
            TxConfiguration = 0x18,
            Command = 0x20,
            CommandArgument = 0x24,
            DataSetup = 0x28,
            Start = 0x2c,
            Response0 = 0x30,
            Response1 = 0x34,
            Response2 = 0x38,
            Response3 = 0x3c,
            ClockDivider = 0x40,
            Status = 0x44,
            StopCommand = 0x48,
            StopCommandArgument = 0x52
        }
    }
}

