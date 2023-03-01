//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Helpers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class Cadence_xSPI : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public Cadence_xSPI(Machine machine) : base(machine)
        {
            controllerIdle = new CadenceInterruptFlag(() => currentCommand == null || currentCommand.Completed || currentCommand.Failed);
            commandCompleted = new CadenceInterruptFlag(() => currentCommand?.Completed ?? false);
            commandIgnored = new CadenceInterruptFlag();
            dmaTriggered = new CadenceInterruptFlag(() => (currentCommand as DMATransactionCommand)?.DMATriggered ?? false);
            dmaError = new CadenceInterruptFlag(() => (currentCommand as DMATransactionCommand)?.DMAError ?? false);

            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            auxiliaryRegisters = new DoubleWordRegisterCollection(this);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        [ConnectionRegion("auxiliary")]
        public void WriteDoubleWordToAuxiliary(long offset, uint value)
        {
            auxiliaryRegisters.Write(offset, value);
        }

        [ConnectionRegion("auxiliary")]
        public uint ReadDoubleWordFromAuxiliary(long offset)
        {
            return auxiliaryRegisters.Read(offset);
        }

        // There is no information in the Linux driver about handling offset for a DMA access
        // The comment above applies to all Write*ToDMA and Read*FromDMA methods
        [ConnectionRegion("dma")]
        public void WriteByteToDMA(long offset, byte value)
        {
            TransmitByDMA(TransmissionDirection.Write, new byte[] { value });
        }

        [ConnectionRegion("dma")]
        public void WriteWordToDMA(long offset, ushort value)
        {
            TransmitByDMA(TransmissionDirection.Write, BitHelper.GetBytesFromValue(value, 2));
        }

        [ConnectionRegion("dma")]
        public void WriteDoubleWordToDMA(long offset, uint value)
        {
            TransmitByDMA(TransmissionDirection.Write, BitHelper.GetBytesFromValue(value, 4));
        }

        [ConnectionRegion("dma")]
        public byte ReadByteFromDMA(long offset)
        {
            var data = new byte[1];
            TransmitByDMA(TransmissionDirection.Read, data);
            return data[0];
        }

        [ConnectionRegion("dma")]
        public ushort ReadWordFromDMA(long offset)
        {
            var data = new byte[2];
            TransmitByDMA(TransmissionDirection.Read, data);
            return BitHelper.ToUInt16(data, 0, false);
        }

        [ConnectionRegion("dma")]
        public uint ReadDoubleWordFromDMA(long offset)
        {
            var data = new byte[4];
            TransmitByDMA(TransmissionDirection.Read, data);
            return BitHelper.ToUInt32(data, 0, data.Length, false);
        }

        public override void Reset()
        {
            registers.Reset();
            Array.Clear(commandPayload, 0, commandPayload.Length);
            auxiliaryRegisters.Reset();
            currentCommand = null;
            ResetSticky();
            UpdateInterrupts();
        }

        public long Size => 0x1040;

        public GPIO IRQ { get; } = new GPIO();

        private void TriggerCommand()
        {
            var previousCommand = currentCommand;
            currentCommand = Command.CreateCommand(this, commandPayload);
            this.Log(LogLevel.Debug, "New command: {0}", currentCommand);

            if(currentCommand == null)
            {
                commandIgnored.SetSticky(true);
            }
            else if(previousCommand != null && previousCommand.ChipSelect != currentCommand.ChipSelect && !previousCommand.TransmissionCompleted)
            {
                this.Log(LogLevel.Error, "Triggering command with chip select different than previous one, when the previous transaction isn't finished.");
                previousCommand.ForceFinish();
            }

            currentCommand?.Transmit();
            UpdateDMASticky();
            UpdateInterrupts();
        }

        private void TransmitByDMA(TransmissionDirection direction, byte[] data)
        {
            var command = currentCommand as DMATransactionCommand;
            if(command == null)
            {
                this.Log(LogLevel.Warning, "Trying to access data using DMA, when the latest command isn't a DMA transaction command.");
                return;
            }
            command.TransmitData(direction, data);
            UpdateDMASticky();
            UpdateInterrupts();
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Command0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "command0Payload",
                        valueProviderCallback: _ => commandPayload[0],
                        writeCallback: (_, val) => commandPayload[0] = (uint)val
                    )
                    .WithWriteCallback((_, __) =>
                        {
                            if(controllerMode.Value != ControllerMode.DMA)
                            {
                                this.Log(LogLevel.Warning, "Can't trigger command in {0} mode, different than DMA.", controllerMode.Value);
                                return;
                            }
                            // Based on the Linux driver all command registers are written in sequence
                            // The command0 register are written as the last one, what triggers command execution
                            TriggerCommand();
                        }
                    )
                },
                {(long)Registers.Command1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "command1Payload",
                        valueProviderCallback: _ => commandPayload[1],
                        writeCallback: (_, val) => commandPayload[1] = (uint)val
                    )
                },
                {(long)Registers.Command2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "command2Payload",
                        valueProviderCallback: _ => commandPayload[2],
                        writeCallback: (_, val) => commandPayload[2] = (uint)val
                    )
                },
                {(long)Registers.Command3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "command3Payload",
                        valueProviderCallback: _ => commandPayload[3],
                        writeCallback: (_, val) => commandPayload[3] = (uint)val
                    )
                },
                {(long)Registers.Command4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "command4Payload",
                        valueProviderCallback: _ => commandPayload[4],
                        writeCallback: (_, val) => commandPayload[4] = (uint)val
                    )
                },
                {(long)Registers.Command5, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "command5Payload",
                        valueProviderCallback: _ => commandPayload[5],
                        writeCallback: (_, val) => commandPayload[5] = (uint)val
                    )
                },
                {(long)Registers.CommandStatus, new DoubleWordRegister(this)
                    .WithReservedBits(16, 16)
                    .WithFlag(15, FieldMode.Read, name: "commandCompleted",
                        valueProviderCallback: _ => currentCommand?.Completed ?? false
                    )
                    .WithFlag(14, FieldMode.Read, name: "commandFailed",
                        valueProviderCallback: _ => currentCommand?.Failed ?? false
                    )
                    .WithReservedBits(4, 10)
                    .WithTaggedFlag("commandDQSError", 3)
                    .WithFlag(2, FieldMode.Read, name: "commandCRCError",
                        valueProviderCallback: _ => currentCommand?.CRCError ?? false
                    )
                    .WithFlag(1, FieldMode.Read, name: "commandBusError",
                        valueProviderCallback: _ => currentCommand?.BusError ?? false
                    )
                    .WithFlag(0, FieldMode.Read, name: "commandInvalidCommandError",
                        valueProviderCallback: _ => currentCommand?.InvalidCommandError ?? false
                    )
                },
                {(long)Registers.ControllerStatus, new DoubleWordRegister(this)
                    .WithReservedBits(17, 15)
                    .WithFlag(16, FieldMode.Read, name: "initializationCompleted",
                        valueProviderCallback: _ => true
                    )
                    .WithReservedBits(10, 6)
                    .WithTaggedFlag("initializationLegacy", 9)
                    .WithFlag(8, FieldMode.Read, name: "initializationFail",
                        valueProviderCallback: _ => false
                    )
                    .WithFlag(7, FieldMode.Read, name: "controllerBusy",
                        valueProviderCallback: _ => !controllerIdle.Status
                    )
                    .WithReservedBits(0, 7)
                },
                {(long)Registers.InterruptStatus, new DoubleWordRegister(this)
                    .WithReservedBits(24, 8)
                    .WithFlag(23, name: "commandCompletedInterruptStatus",
                        valueProviderCallback: _ => commandCompleted.StickyStatus,
                        writeCallback: (_, val) => commandCompleted.ClearSticky(val)
                    )
                    .WithFlag(22, name: "dmaErrorInterruptStatus",
                        valueProviderCallback: _ => dmaError.StickyStatus,
                        writeCallback: (_, val) => dmaError.ClearSticky(val)
                    )
                    .WithFlag(21, name: "dmaTriggeredInterruptStatus",
                        valueProviderCallback: _ => dmaTriggered.StickyStatus,
                        writeCallback: (_, val) => dmaTriggered.ClearSticky(val)
                    )
                    .WithFlag(20, name: "commandIgnoredInterruptStatus",
                        valueProviderCallback: _ => commandIgnored.StickyStatus,
                        writeCallback: (_, val) => commandIgnored.ClearSticky(val)
                    )
                    .WithReservedBits(19, 1)
                    .WithTaggedFlag("DDMA_TERR_InterruptStatus", 18)
                    .WithTaggedFlag("DMA_TREE_InterruptStatus", 17)
                    .WithFlag(16, name: "controllerIdleInterruptStatus",
                        valueProviderCallback: _ => controllerIdle.StickyStatus,
                        writeCallback: (_, val) => controllerIdle.ClearSticky(val)
                    )
                    .WithReservedBits(0, 16)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(31, out interruptsEnabled, name: "enableInterrupts")
                    .WithReservedBits(24, 7)
                    .WithFlag(23, name: "commandCompletedInterruptEnable",
                        valueProviderCallback: _ => commandCompleted.InterruptMask,
                        writeCallback: (_, val) => commandCompleted.InterruptEnable(val)
                    )
                    .WithFlag(22, name: "dmaErrorInterruptEnable",
                        valueProviderCallback: _ => dmaError.InterruptMask,
                        writeCallback: (_, val) => dmaError.InterruptEnable(val)
                    )
                    .WithFlag(21, name: "dmaTriggeredInterruptEnable",
                        valueProviderCallback: _ => dmaTriggered.InterruptMask,
                        writeCallback: (_, val) => dmaTriggered.InterruptEnable(val)
                    )
                    .WithFlag(20, name: "commandIgnoredInterruptEnable",
                        valueProviderCallback: _ => commandIgnored.InterruptMask,
                        writeCallback: (_, val) => commandIgnored.InterruptEnable(val)
                    )
                    .WithReservedBits(19, 1)
                    .WithTaggedFlag("DDMA_TERR_InterruptEnable", 18)
                    .WithTaggedFlag("DMA_TREE_InterruptEnable", 17)
                    .WithFlag(16, name: "controllerIdleInterruptEnable",
                        valueProviderCallback: _ => controllerIdle.InterruptMask,
                        writeCallback: (_, val) => controllerIdle.InterruptEnable(val)
                    )
                    .WithReservedBits(0, 16)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.ControllerConfig, new DoubleWordRegister(this)
                    .WithReservedBits(7, 25)
                    .WithEnumField(5, 2, out controllerMode, name: "controllerMode",
                        changeCallback: (_, val) =>
                        {
                            if(val != ControllerMode.DMA)
                            {
                                this.Log(LogLevel.Error, "Only the DMA mode is currently supported.");
                            }
                        }
                    )
                    .WithReservedBits(0, 5)
                },
                {(long)Registers.DmaSize, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "DMASize",
                        valueProviderCallback: _ => (currentCommand as DMATransactionCommand)?.DataCount ?? 0
                    )
                },
                {(long)Registers.DmaStatus, new DoubleWordRegister(this)
                    .WithReservedBits(9, 23)
                    .WithEnumField<DoubleWordRegister, TransmissionDirection>(8, 1, FieldMode.Read, name: "DMADirection",
                        valueProviderCallback: _ => (currentCommand as DMATransactionCommand)?.Direction ?? default(TransmissionDirection)
                    )
                    .WithReservedBits(0, 8)
                },
                {(long)Registers.ControllerVersion, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, name: "hardwareRevision",
                        valueProviderCallback: _ => HardwareRevision
                    )
                    .WithReservedBits(8, 8)
                    .WithValueField(16, 16, FieldMode.Read, name: "hardwareMagicNumber",
                        valueProviderCallback: _ => HardwareMagicNumber
                    )
                },
                {(long)Registers.ControllerFeatures, new DoubleWordRegister(this)
                    .WithReservedBits(26, 6)
                    .WithTag("banksCount", 24, 2)
                    .WithReservedBits(22, 2)
                    .WithTaggedFlag("dmaDataWidth", 21)
                    .WithReservedBits(4, 17)
                    .WithTag("threadsCount", 0, 4)
                }
            };
        }

        private void UpdateDMASticky()
        {
            controllerIdle.UpdateStickyStatus();
            commandCompleted.UpdateStickyStatus();
            commandIgnored.UpdateStickyStatus();
            dmaTriggered.UpdateStickyStatus();
            dmaError.UpdateStickyStatus();
        }

        private void ResetSticky()
        {
            foreach(var flag in GetInterruptFlags())
            {
                flag.ClearSticky(true);
                flag.UpdateStickyStatus();
            }
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(interruptsEnabled.Value && GetInterruptFlags().Any(x => x.InterruptStatus));
        }

        private IEnumerable<CadenceInterruptFlag> GetInterruptFlags()
        {
            yield return controllerIdle;
            yield return commandCompleted;
            yield return commandIgnored;
            yield return dmaTriggered;
            yield return dmaError;
        }

        private Command currentCommand;

        private IFlagRegisterField interruptsEnabled;
        private IEnumRegisterField<ControllerMode> controllerMode;

        // Command registers have different fields at same offset depending on the command type 
        // The commandPayload array contains all command registers values
        // It's passed to the Command class constructor and decoded
        private readonly uint[] commandPayload = new uint[6];

        private readonly CadenceInterruptFlag controllerIdle;
        private readonly CadenceInterruptFlag commandCompleted;
        private readonly CadenceInterruptFlag commandIgnored;
        private readonly CadenceInterruptFlag dmaTriggered;
        private readonly CadenceInterruptFlag dmaError;

        private readonly DoubleWordRegisterCollection registers;
        private readonly DoubleWordRegisterCollection auxiliaryRegisters;

        private const uint HardwareMagicNumber = 0x6522;
        private const uint HardwareRevision = 0x0;

        private abstract class Command
        {
            static public Command CreateCommand(Cadence_xSPI controller, uint[] payload)
            {
                var commandType = DecodeCommandType(payload);
                switch(commandType)
                {
                    case CommandType.SendOperation:
                    case CommandType.SendOperationWithoutFinish:
                        return new SendOperationCommand(controller, payload);
                    case CommandType.DataSequence:
                        return new DMATransactionCommand(controller, payload);
                    default:
                        controller.Log(LogLevel.Warning, "Unable to create a command, unknown command type 0x{0:x}", commandType);
                        return null;
                }
            }

            public Command(Cadence_xSPI controller, uint[] payload)
            {
                this.controller = controller;
                Type = DecodeCommandType(payload);
                ChipSelect = BitHelper.GetValue(payload[4], 12, 3);
            }

            public void ForceFinish()
            {
                if(TryGetPeripheral(out var peripheral))
                {
                    peripheral.FinishTransmission();
                }
            }

            public override string ToString()
            {
                return $"{this.GetType().Name}: type = {Type}, chipSelect = {ChipSelect}, invalidCommand = {InvalidCommandError}";
            }

            public abstract void Transmit();

            public CommandType Type { get; }
            public uint ChipSelect { get; }
            public bool TransmissionCompleted { get; protected set; }
            public bool Completed { get; protected set; }
            public bool CRCError { get; protected set; }
            public bool BusError { get; protected set; }
            public bool InvalidCommandError { get; protected set; }
            public bool Failed => CRCError || BusError || InvalidCommandError;

            protected bool TryGetPeripheral(out ISPIPeripheral peripheral)
            {
                return controller.TryGetByAddress((int)ChipSelect, out peripheral);
            }

            protected ISPIPeripheral GetPeripheral()
            {
                if(!TryGetPeripheral(out var peripheral))
                {
                    controller.Log(LogLevel.Warning, "There is no peripheral with selected address 0x{0:x}.", ChipSelect);
                }
                return peripheral;
            }

            protected readonly Cadence_xSPI controller;

            static private CommandType DecodeCommandType(uint[] payload)
            {
                return (CommandType)BitHelper.GetValue(payload[1], 0, 7);
            }

            public enum CommandType
            {
                SendOperation = 0x0,
                SendOperationWithoutFinish = 0x1,
                DataSequence = 0x7f
            }
        }

        private class SendOperationCommand : Command
        {
            public SendOperationCommand(Cadence_xSPI controller, uint[] payload)
               : base(controller, payload)
            {
                OperationCode = BitHelper.GetValue(payload[3], 16, 8);
                AddressRaw = (((ulong)payload[3] & 0xff) << 40) | ((ulong)payload[2] << 8) | (payload[1] >> 24);
                AddressValidBytes = (int)BitHelper.GetValue(payload[3], 28, 3);
                AddressBytes = BitHelper.GetBytesFromValue(AddressRaw, AddressValidBytes);
            }

            public override void Transmit()
            {
                var peripheral = GetPeripheral();
                if(peripheral != null)
                {
                    peripheral.Transmit((byte)OperationCode);
                    foreach(var addressByte in AddressBytes)
                    {
                        peripheral.Transmit(addressByte);
                    }
                }

                Completed = true;
                if(Type != CommandType.SendOperationWithoutFinish)
                {
                    TransmissionCompleted = true;
                    peripheral.FinishTransmission();
                }
            }

            public override string ToString()
            {
                return $"{base.ToString()}, operationCode = 0x{OperationCode:x}, addressBytes = [{string.Join(", ", AddressBytes.Select(x => $"0x{x:x2}"))}]";
            }

            public uint OperationCode { get; }
            public ulong AddressRaw { get; }
            public int AddressValidBytes { get; }
            public byte[] AddressBytes { get; }
        }

        private class DMATransactionCommand : Command
        {
            public DMATransactionCommand(Cadence_xSPI controller, uint[] payload)
               : base(controller, payload)
            {
                Direction = BitHelper.GetValue(payload[4], 4, 1) == 0 ? TransmissionDirection.Read : TransmissionDirection.Write;
                DoneTransmission = (payload[0] & 1) == 1;
                DataCount = (payload[3] & 0xffff) | payload[2] >> 16;
                var dummyBitsCount = BitHelper.GetValue(payload[3], 20, 6);
                if(dummyBitsCount % 8 != 0)
                {
                    controller.Log(LogLevel.Warning, "The dummy bit count equals to {0} isn't multiplication of 8. DMA transaction command doesn't support that.");
                }
                DummyBytesCount = dummyBitsCount / 8;
            }

            public override void Transmit()
            {
                var peripheral = GetPeripheral();
                if(peripheral != null)
                {
                    DMATriggered = true;
                }
                else
                {
                    DMAError = true;
                    FinishTransmission(peripheral);
                }
            }

            public void TransmitData(TransmissionDirection accessDirection, byte[] data)
            {
                if(accessDirection != Direction)
                {
                    controller.Log(LogLevel.Warning, "DMA transaction command direction ({0}) is invalid for a given DMA access.", Direction);
                    return;
                }

                var peripheral = GetPeripheral();
                if(peripheral == null)
                {
                    dummyTransmitted = DummyBytesCount;
                    dataTransmitted += (uint)data.Length;
                    if(dataTransmitted >= DataCount)
                    {
                        dataTransmitted = DataCount;
                        FinishTransmission(peripheral);
                    }
                    return;
                }

                for(; dummyTransmitted < DummyBytesCount; dummyTransmitted++)
                {
                    peripheral.Transmit(default(Byte));
                }

                var transferCount = (uint)data.Length;
                if(dataTransmitted + transferCount > DataCount)
                {
                    controller.Log(LogLevel.Warning, "Trying to access more data than command data count.");
                    transferCount = DataCount - dataTransmitted;
                }

                for(int i = 0; i < transferCount; i++)
                {
                    switch(Direction)
                    {
                        case TransmissionDirection.Read:
                            data[i] = peripheral.Transmit(default(Byte));
                            break;
                        case TransmissionDirection.Write:
                            peripheral.Transmit(data[i]);
                            break;
                        default:
                            controller.Log(LogLevel.Warning, "DMA transaction command direction {0} is unknown.", Direction);
                            break;
                    }
                }
                dataTransmitted += transferCount;

                if(dataTransmitted == DataCount)
                {
                    FinishTransmission(peripheral);
                }
            }

            public override string ToString()
            {
                return $"{base.ToString()}, dataCount = {DataCount}, dummyBytesCount = {DummyBytesCount}, doneTransmission = {DoneTransmission}";
            }

            public TransmissionDirection Direction { get; }
            public bool DoneTransmission { get; }
            public uint DataCount { get; }
            public uint DummyBytesCount { get; }
            public bool DMATriggered { get; private set; }
            public bool DMAError { get; private set; }

            private void FinishTransmission(ISPIPeripheral peripheral)
            {
                Completed = true;
                if(DoneTransmission)
                {
                    peripheral?.FinishTransmission();
                    TransmissionCompleted = true;
                }
            }

            private uint dummyTransmitted;
            private uint dataTransmitted;
        }

        private enum ControllerMode
        {
            Direct = 0x0,
            DMA = 0x1,
            // Based on the Linux driver there is no mode for the 0x2 value
            AutoCommand = 0x3
        }

        private enum TransmissionDirection
        {
            Read = 0x0,
            Write = 0x1
        }

        private enum Registers : long
        {
            Command0 = 0x0000,
            Command1 = 0x0004,
            Command2 = 0x0008,
            Command3 = 0x000c,
            Command4 = 0x0010,
            Command5 = 0x0014,
            CommandStatus = 0x0044,
            ControllerStatus = 0x0100,
            InterruptStatus = 0x0110,
            InterruptEnable = 0x0114,
            AutoTransactionCompleteInterruptStatus = 0x0120,
            AutoTransactionErrorInterurptStatus = 0x0130,
            AutoTransactionErrorInterruptEnable = 0x0134,
            ControllerConfig = 0x0230,
            DmaSize = 0x0240,
            DmaStatus = 0x0244,
            ControllerVersion = 0x0f00,
            ControllerFeatures = 0x0f04,
            AutoTransactionStatus = 0x0104,
            DLLControl = 0x1034,
        }

        private enum AuxiliaryRegisters : long
        {
            DQTiming = 0x0000,
            DQSTiming = 0x0004,
            GateLoopbackControl = 0x0008,
            DLLSlaveControl = 0x0010,
        }
    }
}
