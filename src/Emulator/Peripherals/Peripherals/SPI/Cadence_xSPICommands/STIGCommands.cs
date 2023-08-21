//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using static Antmicro.Renode.Peripherals.SPI.Cadence_xSPI;

namespace Antmicro.Renode.Peripherals.SPI.Cadence_xSPICommands
{
    internal abstract class STIGCommand : Command
    {
        static public STIGCommand CreateSTIGCommand(Cadence_xSPI controller, CommandPayload payload)
        {
            var commandType = DecodeCommandType(payload);
            switch(commandType)
            {
                case CommandType.SendOperation:
                case CommandType.SendOperationWithoutFinish:
                    return new SendOperationCommand(controller, payload);
                case CommandType.DataSequence:
                    return new DataSequenceCommand(controller, payload);
                default:
                    controller.Log(LogLevel.Warning, "Unable to create a STIG command, unknown command type 0x{0:x}", commandType);
                    return null;
            }
        }

        public STIGCommand(Cadence_xSPI controller, CommandPayload payload) : base(controller)
        {
            Type = DecodeCommandType(payload);
            ChipSelect = BitHelper.GetValue(payload[4], 12, 3);
        }

        public override string ToString()
        {
            return $"{base.ToString()}, commandType = {Type}";
        }

        public override uint ChipSelect { get; }

        protected CommandType Type { get; }

        static private CommandType DecodeCommandType(CommandPayload payload)
        {
            return (CommandType)BitHelper.GetValue(payload[1], 0, 7);
        }

        protected enum CommandType
        {
            SendOperation = 0x0,
            SendOperationWithoutFinish = 0x1,
            DataSequence = 0x7f
        }
    }

    internal class SendOperationCommand : STIGCommand
    {
        public SendOperationCommand(Cadence_xSPI controller, CommandPayload payload)
           : base(controller, payload)
        {
            operationCode = BitHelper.GetValue(payload[3], 16, 8);
            addressRaw = (((ulong)payload[3] & 0xff) << 40) | ((ulong)payload[2] << 8) | (payload[1] >> 24);
            addressValidBytes = (int)BitHelper.GetValue(payload[3], 28, 3);
            addressBytes = BitHelper.GetBytesFromValue(addressRaw, addressValidBytes);
        }

        public override void Transmit()
        {
            if(Peripheral != null)
            {
                Peripheral.Transmit((byte)operationCode);
                foreach(var addressByte in addressBytes)
                {
                    Peripheral.Transmit(addressByte);
                }
            }

            Completed = true;
            if(Type != CommandType.SendOperationWithoutFinish)
            {
                FinishTransmission();
            }
        }

        public override string ToString()
        {
            return $"{base.ToString()}, operationCode = 0x{operationCode:x}, addressBytes = [{string.Join(", ", addressBytes.Select(x => $"0x{x:x2}"))}]";
        }

        private readonly uint operationCode;
        private readonly ulong addressRaw;
        private readonly int addressValidBytes;
        private readonly byte[] addressBytes;
    }

    internal class DataSequenceCommand : STIGCommand, IDMACommand
    {
        public DataSequenceCommand(Cadence_xSPI controller, CommandPayload payload)
           : base(controller, payload)
        {
            DMADirection = BitHelper.GetValue(payload[4], 4, 1) == 0 ? TransmissionDirection.Read : TransmissionDirection.Write;
            doneTransmission = (payload[0] & 1) == 1;
            DMADataCount = (payload[3] & 0xffff) | payload[2] >> 16;
            var dummyBitsCount = BitHelper.GetValue(payload[3], 20, 6);
            if(dummyBitsCount % 8 != 0)
            {
                Log(LogLevel.Warning, "The dummy bit count equals to {0} isn't multiplication of 8. Data sequence command doesn't support that.");
            }
            dummyBytesCount = dummyBitsCount / 8;
        }

        public override void Transmit()
        {
            if(Peripheral == null)
            {
                DMAError = true;
                Finish();
                return;
            }
            DMATriggered = true;
        }

        public void WriteData(IReadOnlyList<byte> data)
        {
            if(DMADirection != TransmissionDirection.Write)
            {
                throw new InvalidOperationException($"Trying to write data using the command with wrong direction ({DMADirection}).");
            }

            TransmitDummyIfFirst();
            if(Peripheral != null)
            {
                foreach(var dataByte in data)
                {
                    Peripheral.Transmit(dataByte);
                    dataTransmittedCount++;
                }
            }
            FinishIfDone();
        }

        public IList<byte> ReadData(int length)
        {
            if(DMADirection != TransmissionDirection.Read)
            {
                throw new InvalidOperationException($"Trying to read data using the command with wrong direction ({DMADirection}).");
            }

            TransmitDummyIfFirst();
            var data = Enumerable.Repeat(default(byte), length);
            if(Peripheral != null)
            {
                data = data.Select(
                    dataByte => Peripheral.Transmit(dataByte)
                );
            }
            var dataList = data.ToList();
            dataTransmittedCount += (uint)dataList.Count;
            FinishIfDone();

            return dataList;
        }

        public override string ToString()
        {
            return $"{base.ToString()}, dataCount = {DMADataCount}, dummyBytesCount = {dummyBytesCount}, doneTransmission = {doneTransmission}";
        }

        public TransmissionDirection DMADirection { get; }
        public uint DMADataCount { get; }
        public bool DMATriggered { get; private set; }
        public bool DMAError { get; private set; }

        private void TransmitDummyIfFirst()
        {
            if(dataTransmittedCount == 0 && Peripheral != null)
            {
                for(var i = 0; i < dummyBytesCount; i++)
                {
                    Peripheral.Transmit(default(Byte));
                }
            }
        }

        private void FinishIfDone()
        {
            if(dataTransmittedCount >= DMADataCount)
            {
                if(dataTransmittedCount > DMADataCount)
                {
                    Log(LogLevel.Warning, "Accessed more data than command data count.");
                }
                Finish();
            }
        }

        private void Finish()
        {
            Completed = true;
            if(doneTransmission)
            {
                FinishTransmission();
            }
        }

        private uint dataTransmittedCount;
        private readonly bool doneTransmission;
        private readonly uint dummyBytesCount;
    }
}
