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
using Antmicro.Renode.Peripherals.SPI.Cadence_xSPICommands;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class Cadence_xSPI : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public Cadence_xSPI(IMachine machine) : base(machine)
        {
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            auxiliaryRegisters = new DoubleWordRegisterCollection(this);

            controllerIdle = new CadenceInterruptFlag(() => currentCommand == null || currentCommand.Completed || currentCommand.Failed);
            commandCompleted = new CadenceInterruptFlag(() => (currentCommand as STIGCommand)?.Completed ?? false);
            commandIgnored = new CadenceInterruptFlag();
            dmaTriggered = new CadenceInterruptFlag(() => (currentCommand as IDMACommand)?.DMATriggered ?? false);
            dmaError = new CadenceInterruptFlag(() => (currentCommand as IDMACommand)?.DMAError ?? false);
            autoCommandCompleted = new CadenceInterruptFlag(() => (currentCommand as PIOCommand)?.Completed ?? false, initialMask: true);
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
        public void WriteByteUsingDMA(long offset, byte value)
        {
            WriteUsingDMA(new byte[] { value });
        }

        [ConnectionRegion("dma")]
        public void WriteWordUsingDMA(long offset, ushort value)
        {
            WriteUsingDMA(BitHelper.GetBytesFromValue(value, 2));
        }

        [ConnectionRegion("dma")]
        public void WriteDoubleWordUsingDMA(long offset, uint value)
        {
            WriteUsingDMA(BitHelper.GetBytesFromValue(value, 4));
        }

        [ConnectionRegion("dma")]
        public byte ReadByteUsingDMA(long offset)
        {
            return ReadUsingDMA(1).First();
        }

        [ConnectionRegion("dma")]
        public ushort ReadWordUsingDMA(long offset)
        {
            return BitHelper.ToUInt16(ReadUsingDMA(2).ToArray(), 0, false);
        }

        [ConnectionRegion("dma")]
        public uint ReadDoubleWordUsingDMA(long offset)
        {
            return BitHelper.ToUInt32(ReadUsingDMA(4).ToArray(), 0, 4, false);
        }

        public override void Reset()
        {
            registers.Reset();
            Array.Clear(commandPayload, 0, commandPayload.Length);
            auxiliaryRegisters.Reset();
            currentCommand = null;
            foreach(var flag in GetAllInterruptFlags())
            {
                flag.Reset();
            }
            UpdateInterrupts();
        }

        public ControllerMode Mode => controllerMode.Value;

        public long Size => 0x1040;

        public GPIO IRQ { get; } = new GPIO();

        internal bool TryGetPeripheral(int address, out ISPIPeripheral peripheral)
        {
            return TryGetByAddress(address, out peripheral);
        }

        private void TriggerCommand()
        {
            var previousCommand = currentCommand;
            currentCommand = Command.CreateCommand(this, new CommandPayload(commandPayload));
            this.Log(LogLevel.Debug, "New command: {0}", currentCommand);

            if(currentCommand == null)
            {
                commandIgnored.SetSticky(true);
            }
            else if(previousCommand != null && previousCommand.ChipSelect != currentCommand.ChipSelect && !previousCommand.TransmissionFinished)
            {
                this.Log(LogLevel.Error, "Triggering command with chip select different than previous one, when the previous transaction isn't finished.");
                previousCommand.FinishTransmission();
            }

            currentCommand?.Transmit();
            UpdateSticky();
            UpdateInterrupts();
        }

        private void WriteUsingDMA(IReadOnlyList<byte> data)
        {
            if(TryGetDMACommand(TransmissionDirection.Write, out var command))
            {
                command.WriteData(data);
                UpdateSticky();
                UpdateInterrupts();
            }
        }

        private IEnumerable<byte> ReadUsingDMA(int length)
        {
            if(TryGetDMACommand(TransmissionDirection.Read, out var command))
            {
                var data = command.ReadData(length);
                UpdateSticky();
                UpdateInterrupts();
                return data;
            }
            return new byte[length];
        }

        private bool TryGetDMACommand(TransmissionDirection accessDirection, out IDMACommand command)
        {
            command = currentCommand as IDMACommand;
            if(command == null)
            {
                this.Log(LogLevel.Warning, "Trying to access data using DMA, when the latest command isn't a DMA transaction command.");
                return false;
            }
            if(command.DMADirection != accessDirection)
            {
                this.Log(LogLevel.Warning, "Trying to access data using DMA with the wrong direction ({0}), expected {1}.", accessDirection, command.DMADirection);
                return false;
            }
            return true;
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
                            if(!IsModeSupported(controllerMode.Value))
                            {
                                this.Log(LogLevel.Warning, "Command trigger ignored, the mode {0} isn't supported.", controllerMode.Value);
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
                {(long)Registers.AutoCommandStatus, new DoubleWordRegister(this)
                    .WithReservedBits(1, 31)
                    .WithFlag(0, FieldMode.Read, name: "autoCommandControllerBusy",
                        valueProviderCallback: _ => !controllerIdle.Status
                    )
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
                {(long)Registers.AutoCommandCompleteInterruptStatus, new DoubleWordRegister(this)
                    .WithReservedBits(1, 31)
                    .WithFlag(0, name: "autoCommandCompletedInterruptStatus",
                        valueProviderCallback: _ => autoCommandCompleted.StickyStatus,
                        writeCallback: (_, val) => autoCommandCompleted.ClearSticky(val)
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.ControllerConfig, new DoubleWordRegister(this)
                    .WithReservedBits(7, 25)
                    .WithEnumField(5, 2, out controllerMode, name: "controllerMode",
                        changeCallback: (_, val) =>
                        {
                            if(!IsModeSupported(val))
                            {
                                this.Log(LogLevel.Warning, "Setting the controller mode to one which isn't supported ({0}).", val);
                            }
                        }
                    )
                    .WithReservedBits(0, 5)
                },
                {(long)Registers.DMASize, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "DMASize",
                        valueProviderCallback: _ => (currentCommand as IDMACommand)?.DMADataCount ?? 0
                    )
                },
                {(long)Registers.DMAStatus, new DoubleWordRegister(this)
                    .WithReservedBits(9, 23)
                    .WithEnumField<DoubleWordRegister, TransmissionDirection>(8, 1, FieldMode.Read, name: "DMADirection",
                        valueProviderCallback: _ => (currentCommand as IDMACommand)?.DMADirection ?? default(TransmissionDirection)
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

        private bool IsModeSupported(ControllerMode mode)
        {
            return mode == ControllerMode.SoftwareTriggeredInstructionGenerator || mode == ControllerMode.AutoCommand;
        }

        private void UpdateSticky()
        {
            foreach(var flag in GetAllInterruptFlags())
            {
                flag.UpdateStickyStatus();
            }
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(interruptsEnabled.Value && GetAllInterruptFlags().Any(x => x.InterruptStatus));
        }

        private IEnumerable<CadenceInterruptFlag> GetControllerInterruptFlags()
        {
            yield return controllerIdle;
            yield return commandCompleted;
            yield return commandIgnored;
            yield return dmaTriggered;
            yield return dmaError;
        }

        private IEnumerable<CadenceInterruptFlag> GetAutoCommandInterruptFlags()
        {
            yield return autoCommandCompleted;
        }

        private IEnumerable<CadenceInterruptFlag> GetAllInterruptFlags()
        {
            return GetControllerInterruptFlags().Concat(GetAutoCommandInterruptFlags());
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
        private readonly CadenceInterruptFlag autoCommandCompleted;

        private readonly DoubleWordRegisterCollection registers;
        private readonly DoubleWordRegisterCollection auxiliaryRegisters;

        private const uint HardwareMagicNumber = 0x6522;
        private const uint HardwareRevision = 0x0;

        public enum ControllerMode
        {
            Direct = 0x0,
            SoftwareTriggeredInstructionGenerator = 0x1,
            // Based on the Linux driver there is no mode for the 0x2 value
            AutoCommand = 0x3
        }

        public enum TransmissionDirection
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
            CommandStatusPointer = 0x0040,
            CommandStatus = 0x0044,
            ControllerStatus = 0x0100,
            AutoCommandStatus = 0x0104,
            InterruptStatus = 0x0110,
            InterruptEnable = 0x0114,
            AutoCommandCompleteInterruptStatus = 0x0120,
            AutoCommandErrorInterruptStatus = 0x0130,
            AutoCommandErrorInterruptEnable = 0x0134,
            ControllerConfig = 0x0230,
            DMASize = 0x0240,
            DMAStatus = 0x0244,
            ControllerVersion = 0x0f00,
            ControllerFeatures = 0x0f04,
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
