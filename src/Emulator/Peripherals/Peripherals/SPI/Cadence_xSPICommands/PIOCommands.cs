//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

using static Antmicro.Renode.Peripherals.SPI.Cadence_xSPI;

namespace Antmicro.Renode.Peripherals.SPI.Cadence_xSPICommands
{
    internal abstract class PIOCommand : AutoCommand
    {
        public static PIOCommand CreatePIOCommand(Cadence_xSPI controller, CommandPayload payload)
        {
            var commandType = DecodeCommandType(payload);
            switch(commandType)
            {
            case CommandType.Reset:
            case CommandType.SectorErase:
            case CommandType.ChipErase:
                return new PIOOperationCommand(controller, payload);
            case CommandType.Program:
            case CommandType.Read:
                return new PIODataCommand(controller, payload);
            default:
                controller.Log(LogLevel.Warning, "Unable to create a PIO command, unknown command type 0x{0:x}", commandType);
                return null;
            }
        }

        public PIOCommand(Cadence_xSPI controller, CommandPayload payload) : base(controller, payload)
        {
            type = DecodeCommandType(payload);
            dmaRole = (DMARoleType)BitHelper.GetValue(payload[0], 19, 1);
            address = payload[1];
            length = (ulong)payload[4] + 1;
            if(length > uint.MaxValue)
            {
                Log(LogLevel.Warning, "The length of the PIOCommand doesn't fit in 32 bits. Value of the DMA transation size register is undefined.");
            }
        }

        public override string ToString()
        {
            return $"{base.ToString()}, commandType = {type}, address = 0x{address:x}, length = {length}";
        }

        protected void TransmitAddress()
        {
            foreach(var addressByte in BitHelper.GetBytesFromValue(address, 4))
            {
                Peripheral.Transmit(addressByte);
            }
        }

        protected readonly CommandType type;
        protected readonly DMARoleType dmaRole;
        protected readonly uint address;
        protected readonly ulong length;

        private static CommandType DecodeCommandType(CommandPayload payload)
        {
            return (CommandType)BitHelper.GetValue(payload[0], 0, 16);
        }

        protected enum CommandType
        {
            Reset = 0x1100,
            SectorErase = 0x1000,
            ChipErase = 0x1001,
            Read = 0x2200,
            Program = 0x2100
        }

        protected enum DMARoleType
        {
            Peripheral = 0,
            Controller = 1
        }
    }

    internal class PIOOperationCommand : PIOCommand
    {
        public PIOOperationCommand(Cadence_xSPI controller, CommandPayload payload) : base(controller, payload) { }

        public override void Transmit()
        {
            if(Peripheral != null)
            {
                switch(type)
                {
                case CommandType.Reset:
                    Peripheral.Transmit(ResetEnableOperation);
                    Peripheral.Transmit(ResetMemoryOperation);
                    break;
                case CommandType.ChipErase:
                    Peripheral.Transmit(WriteEnableOperation);
                    Peripheral.FinishTransmission();
                    Peripheral.Transmit(ChipEraseOperation);
                    break;
                case CommandType.SectorErase:
                    Peripheral.Transmit(WriteEnableOperation);
                    Peripheral.FinishTransmission();
                    Peripheral.Transmit(SectorErase4ByteOperation);
                    TransmitAddress();
                    break;
                default:
                    throw new ArgumentException($"Unknown PIO Command type: {type}.");
                }
            }
            Completed = true;
            FinishTransmission();
        }

        private const byte ResetEnableOperation = 0x66;
        private const byte ResetMemoryOperation = 0x99;
        private const byte ChipEraseOperation = 0xC7;
        private const byte SectorErase4ByteOperation = 0xDC;
        private const byte WriteEnableOperation = 0x06;
    }

    internal class PIODataCommand : PIOCommand, IDMACommand
    {
        public PIODataCommand(Cadence_xSPI controller, CommandPayload payload) : base(controller, payload)
        {
            switch(type)
            {
            case CommandType.Program:
                DMADirection = TransmissionDirection.Write;
                break;
            case CommandType.Read:
                DMADirection = TransmissionDirection.Read;
                break;
            default:
                throw new ArgumentException($"Unknown PIO Command type: {type}.");
            }

            if(dmaRole == DMARoleType.Controller)
            {
                // NOTE: Second and third word contains lower and upper part of the host address respectively
                hostAddress = ((ulong)payload[3] << 32) | payload[2];
            }
        }

        public override void Transmit()
        {
            if(Peripheral == null)
            {
                DMAError = true;
                FinishTransmission();
                return;
            }

            if(hostAddress == null)
            {
                DMATriggered = true;
            }
            else
            {
                DoDMATransfer();
            }
        }

        public void DoDMATransfer()
        {
            var bus = controller.GetMachine().GetSystemBus(controller);

            if(DMADirection == TransmissionDirection.Write)
            {
                var data = bus.ReadBytes((ulong)hostAddress, (int)length, context: controller);

                // NOTE: Set WriteEnable
                Peripheral.Transmit(WriteEnableOperation);
                Peripheral.FinishTransmission();

                // NOTE: Start transmitting data
                Peripheral.Transmit(PageProgram4ByteOperation);
                TransmitAddress();

                foreach(var b in data)
                {
                    Peripheral.Transmit(b);
                }
            }
            else
            {
                Peripheral.Transmit(Read4ByteOperation);
                TransmitAddress();

                var data = Misc.Iterate(() => Peripheral.Transmit(0x00))
                    .Take((int)length)
                    .ToArray()
                ;

                bus.WriteBytes(data, (ulong)hostAddress, context: controller);
            }

            dataTransmittedCount = length;
            FinishIfDone();
        }

        public void WriteData(IReadOnlyList<byte> data)
        {
            if(DMADirection != TransmissionDirection.Write)
            {
                throw new InvalidOperationException($"Trying to write data using the command with wrong direction ({DMADirection}).");
            }

            if(Peripheral != null)
            {
                if(dataTransmittedCount == 0)
                {
                    Peripheral.Transmit(PageProgram4ByteOperation);
                    TransmitAddress();
                }
                foreach(var dataByte in data)
                {
                    Peripheral.Transmit(dataByte);
                }
            }
            dataTransmittedCount += (ulong)data.Count;
            FinishIfDone();
        }

        public IList<byte> ReadData(int length)
        {
            if(DMADirection != TransmissionDirection.Read)
            {
                throw new InvalidOperationException($"Trying to read data using the command with wrong direction ({DMADirection}).");
            }

            var data = Enumerable.Repeat(default(byte), length);
            if(Peripheral != null)
            {
                if(dataTransmittedCount == 0)
                {
                    Peripheral.Transmit(Read4ByteOperation);
                    TransmitAddress();
                }
                data = data.Select(
                    dataByte => Peripheral.Transmit(dataByte)
                );
            }
            var dataList = data.ToList();
            dataTransmittedCount += (ulong)dataList.Count;
            FinishIfDone();

            return dataList;
        }

        public override string ToString()
        {
            return $"{base.ToString()}, DMADirection = {DMADirection}";
        }

        public TransmissionDirection DMADirection { get; }

        public uint DMADataCount => (uint)length;

        public bool DMATriggered { get; private set; }

        public bool DMAError { get; private set; }

        private void FinishIfDone()
        {
            if(dataTransmittedCount < DMADataCount)
            {
                return;
            }
            if(dataTransmittedCount > DMADataCount)
            {
                Log(LogLevel.Warning, "Accessed more data than command data count.");
            }
            Completed = true;
            FinishTransmission();
        }

        private ulong dataTransmittedCount;

        private readonly ulong? hostAddress = null;

        private const byte PageProgram4ByteOperation = 0x12;
        private const byte Read4ByteOperation = 0x13;
        private const byte WriteEnableOperation = 0x06;
    }
}
