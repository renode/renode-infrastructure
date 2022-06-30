//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.SPI.NORFlash;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class GigaDevice_GD25LQ : ISPIPeripheral, IGPIOReceiver
    {
        public GigaDevice_GD25LQ(MappedMemory underlyingMemory)
        {
            var registerMap = new Dictionary<long, ByteRegister>
            {
                {(long)Register.StatusLow, new ByteRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => writeInProgress, name: "Write in progress")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => writeEnableLatch, name: "Write enable latch")
                    .WithTag("Block protect 0", 2, 1)
                    .WithTag("Block protect 1", 3, 1)
                    .WithTag("Block protect 2", 4, 1)
                    .WithTag("Block protect 3", 5, 1)
                    .WithTag("Block protect 4", 6, 1)
                    .WithTag("Status register protect 0", 7, 1)
                },
                {(long)Register.StatusHigh, new ByteRegister(this)
                    .WithTag("Status register protect 1", 0, 1)
                    .WithFlag(1, out quadEnable, name: "Quad enable")
                    .WithTag("SUS2", 2, 1)
                    .WithTag("LB1", 3, 1)
                    .WithTag("LB2", 4, 1)
                    .WithTag("LB3", 5, 1)
                    .WithTag("CMP", 6, 1)
                    .WithTag("SUS1", 7, 1)
                }
            };
            registers = new ByteRegisterCollection(this, registerMap);
            this.underlyingMemory = underlyingMemory;
        }

        public void OnGPIO(int number, bool value)
        {
            if(number == 0 && value)
            {
                this.Log(LogLevel.Noisy, "Chip Select is deasserted.");
                FinishTransmission();
            }
        }

        public void FinishTransmission()
        {
            switch(currentOperation.State)
            {
                case DecodedOperation.OperationState.RecognizeOperation:
                case DecodedOperation.OperationState.AccumulateCommandAddressBytes:
                case DecodedOperation.OperationState.AccumulateNoDataCommandAddressBytes:
                case DecodedOperation.OperationState.HandleImmediateCommand:
                    this.Log(LogLevel.Warning, "Transmission finished in the unexpected state: {0}", currentOperation.State);
                    break;
                default:
                    this.Log(LogLevel.Noisy, "Transmission finished in state: {0}", currentOperation.State);
                    break;
            }
            currentOperation.State = DecodedOperation.OperationState.RecognizeOperation;
            currentOperation = default(DecodedOperation);
            writeInProgress = false;
        }

        public void Reset()
        {
            writeEnableLatch = false;
            writeInProgress = false;
            currentOperation = default(DecodedOperation);
            registers.Reset();
            FinishTransmission();
        }

        public byte Transmit(byte data)
        {
            this.Log(LogLevel.Noisy, "Transmitting data 0x{0:X}, current state: {1}", data, currentOperation.State);
            switch(currentOperation.State)
            {
                case DecodedOperation.OperationState.RecognizeOperation:
                    // When the command is decoded, depending on the operation, we will either start accumulating address bytes
                    // or immediately handle the command bytes
                    RecognizeOperation(data);
                    break;
                case DecodedOperation.OperationState.HandleCommand:
                    // Process the remaining command bytes
                    return HandleCommand(data);
            }
            return 0;
        }

        private void RecognizeOperation(byte firstByte)
        {
            currentOperation.Operation = DecodedOperation.OperationType.None;
            currentOperation.AddressLength = 0;
            currentOperation.State = DecodedOperation.OperationState.HandleCommand;
            switch((Commands)firstByte)
            {
                case Commands.WriteStatusRegister:
                    currentOperation.Operation = DecodedOperation.OperationType.WriteRegister;
                    currentOperation.Register = (uint)Register.StatusLow;
                    currentOperation.State = DecodedOperation.OperationState.HandleCommand;
                    break;
                case Commands.ReadStatusRegisterLow:
                    currentOperation.Operation = DecodedOperation.OperationType.ReadRegister;
                    currentOperation.Register = (uint)Register.StatusLow;
                    currentOperation.State = DecodedOperation.OperationState.HandleCommand;
                    break;
                case Commands.WriteEnable:
                    currentOperation.Operation = DecodedOperation.OperationType.WriteEnable;
                    currentOperation.State = DecodedOperation.OperationState.HandleImmediateCommand;
                    break;
                case Commands.ReadStatusRegisterHigh:
                    currentOperation.Operation = DecodedOperation.OperationType.ReadRegister;
                    currentOperation.Register = (uint)Register.StatusHigh;
                    currentOperation.State = DecodedOperation.OperationState.HandleCommand;
                    break;
                case Commands.ReadID:
                    currentOperation.Operation = DecodedOperation.OperationType.ReadID;
                    currentOperation.State = DecodedOperation.OperationState.HandleImmediateCommand;
                    break;
                default:
                    this.Log(LogLevel.Error, "Command decoding failed on byte: 0x{0:X} ({1}).", firstByte, (Commands)firstByte);
                    return;
            }
            if(currentOperation.State == DecodedOperation.OperationState.HandleImmediateCommand)
            {
                switch(currentOperation.Operation)
                {
                    case DecodedOperation.OperationType.WriteEnable:
                        writeEnableLatch = true;
                        break;
                    case DecodedOperation.OperationType.ReadID:
                        currentOperation.State = DecodedOperation.OperationState.HandleCommand;
                        break;
                    default:
                        this.Log(LogLevel.Error, "Encountered invalid immediate command: {0}", currentOperation.Operation);
                        break;
                }
            }
            this.Log(LogLevel.Noisy, "Decoded operation: {0}", currentOperation);
        }

        private byte HandleCommand(byte data)
        {
            byte result = 0;
            switch(currentOperation.Operation)
            {
                case DecodedOperation.OperationType.ReadID:
                    if(currentOperation.CommandBytesHandled < deviceID.Length)
                    {
                        result = deviceID[currentOperation.CommandBytesHandled];
                    }
                    else
                    {
                        this.Log(LogLevel.Error, "Trying to read beyond the length of the device ID table.");
                        result = 0;
                    }
                    break;
                case DecodedOperation.OperationType.ReadRegister:
                    result = ReadRegister(currentOperation.Register);
                    break;
                case DecodedOperation.OperationType.WriteRegister:
                    if(currentOperation.Register == (uint)Register.StatusLow)
                    {
                        WriteRegister(currentOperation.Register, data);
                        currentOperation.Register = (uint)Register.StatusHigh;
                        writeInProgress = true;
                    }
                    else
                    {
                        WriteRegister(currentOperation.Register, data);
                        writeEnableLatch = false;
                    }
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unhandled operation encountered while processing command bytes: {0}", currentOperation.Operation);
                    break;
            }
            currentOperation.CommandBytesHandled++;
            this.Log(LogLevel.Noisy, "Handled command: {0}, returning 0x{1:X}", currentOperation, result);
            return result;
        }

        private byte ReadRegister(uint offset)
        {
            return registers.Read(offset);
        }

        private void WriteRegister(uint offset, byte data)
        {
            if(!writeEnableLatch)
            {
                this.Log(LogLevel.Warning, "Trying to write 0x{0:X} to {1} register while operation is not enabled.", data, (Register)offset);
                return;
            }
            this.Log(LogLevel.Noisy, "Writing value: 0x{0:X} to {1}", data, (Register)offset);
            registers.Write(offset, data);
        }

        private DecodedOperation currentOperation;
        private ByteRegisterCollection registers;

        private readonly MappedMemory underlyingMemory;
        private readonly IFlagRegisterField quadEnable;

        private bool writeInProgress;
        private bool writeEnableLatch;

        private static byte[] deviceID = { 0xC8, 0x60, 0x18 };

        private enum Commands : byte
        {
            // There are multiple gaps in command coding
            WriteStatusRegister = 0x1,
            PageProgram = 0x2,
            ReadData = 0x3,
            WriteDisable = 0x4,
            ReadStatusRegisterLow = 0x5,
            WriteEnable = 0x6,
            FastRead = 0xB,
            SectorErase = 0x20,
            QuadPageProgram = 0x32,
            ReadStatusRegisterHigh = 0x35,
            EnableQPI = 0x38,
            DualOutputFastRead = 0x3B,
            ProgramSecurityRegisters = 0x42,
            EraseSecurityRegisters = 0x44,
            ReadSecurityRegisters = 0x48,
            VolatileStatusRegisterWriteEnable = 0x50,
            BlockErase32K = 0x52,
            ReadSerialFlash = 0x5A,
            EnableReset = 0x66,
            QuadOutputFastRead = 0x6B,
            ProgramOrEraseSuspend = 0x75,
            SetBurstWithWrap = 0x77,
            ProgramOrEraseResume = 0x7A,
            DeviceID = 0x90,
            DeviceIDByDualIO = 0x92,
            DeviceIDByQuadIO = 0x94,
            Reset = 0x99,
            ReadID = 0x9F,
            ReleaseFromDeepPowerDown = 0xAB,
            DeepPowerDown = 0xB9,
            DualIOFastRead = 0xBB,
            ChipErase = 0xC7,
            BlockErase64K = 0xD8,
            QuadIOWordFastRead = 0xE7,
            QuadIOFastRead = 0xEB
        }

        private enum Register : uint
        {
            StatusLow = 0,
            StatusHigh
        }
    }
}
