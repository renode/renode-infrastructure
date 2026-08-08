//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.SPI;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class RTS_MAASensor : ISPIPeripheral
    {
        public RTS_MAASensor()
        {
            currentCommand = 0;
            currentCommandType = 0;
            currentOperation = OperationState.CommandTypeState;
            CommandBytesHandled = 0;
        }
        public void Reset()
        {
            currentCommand = 0;
            currentCommandType = 0;
            currentOperation = OperationState.CommandTypeState;
            CommandBytesHandled = 0;
        }

        public byte Transmit(byte data)
        {
            switch (currentOperation)
            {
            case OperationState.CommandTypeState:
                CommandTypeHandle(data);
                break;
            case OperationState.CommandState:
                CommandHandle(data);
                break;
            case OperationState.TransmitState:
                return TransmitHandle(data);
            }

            return 0;
        }

        public void FinishTransmission()
        {
            currentCommand = 0;
            currentCommandType = 0;
            currentOperation = OperationState.CommandTypeState;
            CommandBytesHandled = 0;
        }

        private void CommandTypeHandle(byte data)
        {
            if(((CommandType)data != CommandType.ReadCommand) && ((CommandType)data != CommandType.WriteCommand))
            {
                this.Log(LogLevel.Error, "Command Type 0x{0:X} is not supported", data);
            }
            else
            {
                CommandBytesHandled++;
                currentOperation = OperationState.CommandState;
                currentCommandType = (CommandType)data;
            }
        }

        private void CommandHandle(byte data)
        {
            if(currentCommandType == CommandType.ReadCommand)
            {
                switch ((Commands)data)
                {
                case Commands.ReadID:
                    currentCommand = (Commands)data;
                    CommandBytesHandled++;
                    currentOperation = OperationState.TransmitState;
                    break;
                default:
                    this.Log(LogLevel.Error, "Command 0x{0:X} is not supported when read", data);
                    break;
                }
            }
            else
            {
                switch ((Commands)data)
                {
                case Commands.ClodReset:
                    currentCommand = (Commands)data;
                    CommandBytesHandled++;
                    currentOperation = OperationState.TransmitState;
                    break;
                default:
                    this.Log(LogLevel.Error, "Command 0x{0:X} is not supported when write", data);
                    break;
                }
            }
        }

        private byte TransmitHandle(byte data)
        {
            byte result = 0;

            switch (currentCommand)
            {
            case Commands.ReadID:
                if(CommandBytesHandled == 2)
                {
                    result = 0x23;
                }
                break;
            case Commands.ClodReset:
                this.Log(LogLevel.Info, "Code reset sensor");
                break;
            default:
                this.Log(LogLevel.Error, "Unsupported Command 0x{0:X}", currentCommand);
                break;
            }
            CommandBytesHandled++;
            return result;
        }

        private Commands? currentCommand;
        private CommandType? currentCommandType;
        private OperationState? currentOperation;

        private uint CommandBytesHandled;


        private enum Commands : byte
        {
            ReadID = 0xB9,
            ClodReset = 0x02
        }

        private enum CommandType : byte
        {
            ReadCommand = 0xA8,
            WriteCommand = 0xA9
        }

        private enum OperationState
        {
            CommandTypeState,
            CommandState,
            TransmitState,
        }
    }
}
