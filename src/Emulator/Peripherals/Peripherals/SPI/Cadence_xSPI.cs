//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Helpers;
using Antmicro.Renode.Peripherals.SPI.Cadence_xSPICommands;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public partial class Cadence_xSPI : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
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
        public void WriteByteUsingDMA(long _, byte value)
        {
            WriteUsingDMA(new byte[] { value });
        }

        [ConnectionRegion("dma")]
        public void WriteWordUsingDMA(long _, ushort value)
        {
            WriteUsingDMA(BitHelper.GetBytesFromValue(value, 2));
        }

        [ConnectionRegion("dma")]
        public void WriteDoubleWordUsingDMA(long _, uint value)
        {
            WriteUsingDMA(BitHelper.GetBytesFromValue(value, 4));
        }

        [ConnectionRegion("dma")]
        public byte ReadByteUsingDMA(long _)
        {
            return ReadUsingDMA(1).First();
        }

        [ConnectionRegion("dma")]
        public ushort ReadWordUsingDMA(long _)
        {
            return BitHelper.ToUInt16(ReadUsingDMA(2).ToArray(), 0, false);
        }

        [ConnectionRegion("dma")]
        public uint ReadDoubleWordUsingDMA(long _)
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
            else if(previousCommand != null && !previousCommand.TransmissionFinished && previousCommand.ChipSelect != currentCommand.ChipSelect)
            {
                this.Log(LogLevel.Error, "Triggering command with chip select different than previous one, when the previous transaction isn't finished.");
                previousCommand.FinishTransmission();
            }
            else if(previousCommand != null && !previousCommand.TransmissionFinished && previousCommand.Mode != currentCommand.Mode)
            {
                this.Log(LogLevel.Error, "Finishing transmission due to mode change: {0} -> {1}", previousCommand.Mode, currentCommand.Mode);
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
                {(long)Registers.CommandStatusPointer, new DoubleWordRegister(this)
                    .WithReservedBits(3,29)
                    .WithTag("threadSelect", 0, 3)
                },
                {(long)Registers.CommandStatus, new DoubleWordRegister(this)
                    .WithValueField(16,16, name: "dataFromDev",
                        valueProviderCallback: _ => (currentCommand as DataSequenceCommand)?.ShortOutput ?? 0
                    )
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
                {(long)Registers.SequenceConfiguration0, new DoubleWordRegister(this)
                    .WithValueField(0, 4, out pageSizeRead,  name: "PSIZ_RD")
                    .WithValueField(4, 4, out pageSizeProgram, name: "PSIZ_PGM")
                    .WithTaggedFlag("CRC_EN", 8)
                    .WithTaggedFlag("CRC_VAR", 9)
                    .WithTaggedFlag("CRC_OE", 10)
                    .WithReservedBits(11, 1)
                    .WithTag("CHUNK_SIZ", 12, 3)
                    .WithReservedBits(15, 1)
                    .WithTaggedFlag("UAL_CHUNK_EN", 16)
                    .WithTaggedFlag("UAL_CHUNK_CHK", 17)
                    .WithTaggedFlag("TCMS_EN", 18)
                    .WithFlag(20, out dataSwap888, name: "DAT_SWAP")
                    .WithValueField(21, 1, FieldMode.Read, valueProviderCallback: (_) => 0,  name: "DAT_PER_ADDR") // 0 for xspi profile 1
                    .WithReservedBits(22, 1)
                    .WithValueField(23, 2, FieldMode.Read, valueProviderCallback: (_) => 0,  name: "TYP") // 0 for xspi profile 1
                    .WithReservedBits(25,7)
                },
                {(long)Registers.ResetSequenceConfiguration0, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out resetCmd0, name: "CMD0_VALUE")
                    .WithValueField(8, 8, out resetCmd1, name: "CMD1_VALUE")
                    .WithFlag(16, out resetCmd0Enabled, name: "CMD0_EN")
                    .WithReservedBits(17, 1)
                    .WithTag("DAT_IOS", 18,2)
                    .WithReservedBits(20, 1)
                    .WithTaggedFlag("DAT_EDGE", 21)
                    .WithFlag(22, out resetCmd1ConfirmationEnabled, name: "DAT_EN")
                    .WithReservedBits(23, 1)
                    .WithTag("CMD_IOS", 24,2)
                    .WithReservedBits(26, 2)
                    .WithTaggedFlag("CMD_EDGE", 28)
                    .WithReservedBits(29, 3)
                },
                {(long)Registers.ResetSequenceConfiguration1, new DoubleWordRegister(this)
                    .WithTaggedFlag("P1_CMD0_EXT_EN", 0)
                    .WithTaggedFlag("P1_CMD1_EXT_EN", 1)
                    .WithReservedBits(2, 6)
                    .WithTag("P1_CMD0_EXT_VALUE", 8, 8)
                    .WithTag("P1_CMD1_EXT_VALUE", 16, 8)
                    .WithValueField(24, 8, out resetCmd1Confirmation, name: "P1_DAT_VALUE")
                },
                {(long)Registers.EraseSequenceConfiguration0, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out eraseCmd, name: "CMD_VALUE")
                    .WithTag("CMD_IOS", 8, 2)
                    .WithReservedBits(10, 1)
                    .WithTaggedFlag("CMD_EDGE", 11)
                    .WithValueField(12,3, out eraseAddrCount, name:"ADDR_CNT")
                    .WithTaggedFlag("CMD_EXT_EN", 15)
                    .WithTag("CMD_EXT_VALUE", 16, 8)
                    .WithTag("ADDR_IOS", 24, 2)
                    .WithReservedBits(26, 2)
                    .WithTaggedFlag("ADDR_EDGE", 28)
                    .WithReservedBits(29, 3)
                    },
                {(long)Registers.EraseSequenceConfiguration1, new DoubleWordRegister(this)
                    .WithValueField(0, 5, out sectorSize, name: "P1_SECT_SIZ_VALUE")
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.EraseSequenceConfiguration2, new DoubleWordRegister(this)
                    .WithValueField(0,8, out eraseAllCmd, name:"ERSA_P1_CMD_VALUE")
                    .WithValueField(8, 2, out eraseAllCmdIOS, name: "ERSA_P1_CMD_IOS")
                    .WithReservedBits(10, 1)
                    .WithFlag(11, out eraseAllCmdEdge, name: "ERSA_P1_CMD_EDGE")
                    .WithReservedBits(12, 3)
                    .WithFlag(15, name: "ERSA_P1_CMD_EXT_EN")
                    .WithValueField( 16, 8, name: "ERSA_P1_CMD_EXT_VALUE")
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.ProgramSequenceConfiguration0, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out programCmd, name: "P1_CMD_VALUE")
                    .WithValueField(8, 2, out programCmdIOS, name: "P1_CMD_IOS")
                    .WithReservedBits(10, 1)
                    .WithFlag(11, out programCmdEdge,  name: "P1_CMD_EDGE")
                    .WithValueField(12, 3, out programAddressCount,  name: "P1_ADDR_CNT")
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 2, out programAddressIOS, name: "P1_ADDR_IOS")
                    .WithReservedBits(18, 1)
                    .WithFlag(19,  out programAddressEdge, name: "P1_ADDR_EDGE")
                    .WithValueField(20, 2, out programDataIOS, name: "P1_DAT_IOS")
                    .WithReservedBits(22, 1)
                    .WithFlag(23, out programDataEdge, name: "P1_DAT_EDGE")
                    .WithValueField(24, 6, out programDummyCount, name: "P1_DMY_CNT")
                    .WithReservedBits(30, 2)
                    },
                {(long)Registers.ProgramSequenceConfiguration1, new DoubleWordRegister(this)
                    .WithTaggedFlag("P1_CMD_EXT_EN", 0)
                    .WithTag("P1_CMD_EXT_VALUE", 8, 8)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.ReadSequenceConfiguration0, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out readCmd, name: "P1_CMD_VALUE")
                    .WithValueField(8,2, out readCmdIOS, name:"P1_CMD_IOS")
                    .WithReservedBits(10, 1)
                    .WithFlag(11, out readCmdEdge, name: "P1_CMD_EDGE")
                    .WithValueField(12, 3, out readAddressCount, name: "P1_ADDR_CNT")
                    .WithReservedBits(15, 1)
                    .WithValueField(16,2, out readAddressIOS, name: "P1_ADDR_IOS")
                    .WithReservedBits(18, 1)
                    .WithFlag(19, out readAddressEdge, name: "P1_ADDR_EDGE")
                    .WithValueField(20,2, out readDataIOS, name:"P1_DAT_IOS")
                    .WithReservedBits(22, 1)
                    .WithFlag(23, out readDataEdge, name: "P1_DAT_EDGE")
                    .WithValueField(24, 6, out readDummyCount, name: "P1_DMY_CNT")
                    .WithReservedBits(30, 2)
                },
                {(long)Registers.ReadSequenceConfiguration1, new DoubleWordRegister(this)
                    .WithTaggedFlag("P1_CMD_EXT_EN", 0)
                    .WithReservedBits(1, 3)
                    .WithTaggedFlag("P1_CACHE_RANDOM_READ_EN", 4)
                    .WithReservedBits(5,3)
                    .WithTag("P1_CMD_EXT_VALUE", 8, 8)
                    .WithReservedBits(16,8)
                    .WithTag("P1_MB_DMY_CNT", 24, 6)
                    .WithReservedBits(30,1)
                    .WithTaggedFlag("P1_MB_EN", 31)
                },
                {(long)Registers.WriteEnableSequenceConfiguration, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out weCmd,name: "P1_CMD_VALUE")
                    .WithTag("P1_CMD_IOS", 8, 2)
                    .WithReservedBits(10,1)
                    .WithTaggedFlag("P1_CMD_EDGE", 11)
                    .WithReservedBits(12,3)
                    .WithTaggedFlag("P1_CMD_EXT_EN", 15)
                    .WithTag("P1_CMD_EXT_VALUE", 16, 8)
                    .WithFlag(24, out weCmdEnabled ,name: "P1_EN")
                    .WithReservedBits(25,7)
                },
                {(long)Registers.StatusSequenceConfiguration0, new DoubleWordRegister(this)
                    .WithTag("P1_CMD_IOS", 0, 2)
                    .WithReservedBits(2,2)
                    .WithTaggedFlag("P1_CMD_EDGE", 4)
                    .WithTaggedFlag("P1_CMD_EXT_EN", 5)
                    .WithReservedBits(6,2)
                    .WithTag("P1_ADDR_CNT", 8, 2)
                    .WithTag("P1_ADDR_IOS", 10, 2)
                    .WithTaggedFlag("P1_ADDR_EDGE", 12)
                    .WithReservedBits(13,7)
                    .WithTag("P1_DAT_IOS", 20, 2)
                    .WithTaggedFlag("P1_DAT_EDGE", 22)
                    .WithReservedBits(23,9)
                },
                {(long)Registers.DMAStatus, new DoubleWordRegister(this)
                    .WithReservedBits(9, 23)
                    .WithEnumField<DoubleWordRegister, TransmissionDirection>(8, 1, FieldMode.Read, name: "DMADirection",
                        valueProviderCallback: _ => (currentCommand as IDMACommand)?.DMADirection ?? default(TransmissionDirection)
                    )
                    .WithReservedBits(0, 8)
                },
                {(long)Registers.DiscoveryControl, new DoubleWordRegister(this, resetValue: 0x00000000)
                    .WithFlag(0, out var discoveryRequest, name: "REQ")
                    .WithFlag(1, out fullDiscovery, name: "REQ_TYP",
                        valueProviderCallback: _ => !fullDiscovery.Value,  // Read: internal true → HW reads 0 (Full)
                        writeCallback: (_, val) =>
                        {
                            this.Log(LogLevel.Warning, "REQ_TYP write: HW wrote {0}, setting fullDiscovery to {1}", val, !val);
                            fullDiscovery.Value = !val;  // Write: HW writes 0 → store true (Full)
                        }
                    )
                    .WithFlag(2, out discoveryPassed, name:"PASS")
                    .WithValueField(3,2, out resultOfLastDiscovery, name: "FAIL")
                    .WithTaggedFlag("INHIBIT",5)
                    .WithTaggedFlag("OE_VAL",6)
                    .WithTaggedFlag("OE_EN",7)
                    .WithTag("CMD_TYP",8,2)
                    .WithFlag(10, out discoveryDummyCount, name: "DMY_CNT")
                    .WithFlag(11, out discoveryABNUM, name: "ABNUM")
                    .WithValueField(12,3, out discoveryNumberOfLines ,name: "NUM_LINES")
                    .WithReservedBits(15, 1)
                    .WithValueField(16,3, out discoveryBnk,name: "BNK")
                    .WithReservedBits(19,13)
                    .WithWriteCallback((_, __) =>
                    {
                        if(discoveryRequest.Value)
                        {
                            discoveryRequest.Value = false;  // Auto-clear REQ bit
                            ExecuteDiscovery();
                        }
                    })
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
            ControllerStatus = 0x0100, // GSTAT (General Controller Status)
            AutoCommandStatus = 0x0104, // Auto Command Engine Thread Status Register
            InterruptStatus = 0x0110,
            InterruptEnable = 0x0114,
            AutoCommandCompleteInterruptStatus = 0x0120,
            AutoCommandErrorInterruptStatus = 0x0130,
            AutoCommandErrorInterruptEnable = 0x0134,
            DMAErrorAddressLow = 0x0150,
            DMAErrorAddressHigh = 0x0154,
            BootStatus = 0x0158,
            LongPollingCount = 0x0208,
            ShortPollingCount = 0x0208,
            ControllerConfig = 0x0230, // Device Control Register
            DMAInterfaceControl = 0x023C,
            DMASize = 0x0240,
            DMAStatus = 0x0244,
            DMABufferAddress0 = 0x024C,
            DMABufferAddress1 = 0x0250,
            DiscoveryControl = 0x0260,
            XIPConfiguration = 0x0388,
            SequenceConfiguration0 = 0x0390,
            SequenceConfiguration1 = 0x0394,
            DACConfiguration = 0x0398,
            DACAddressRemapping0 = 0x039C,
            DACAddressRemapping1 = 0x03A0,
            ResetSequenceConfiguration0 = 0x0400,
            ResetSequenceConfiguration1 = 0x0404,
            EraseSequenceConfiguration0 = 0x0410,
            EraseSequenceConfiguration1 = 0x0414,
            EraseSequenceConfiguration2 = 0x0418,
            ProgramSequenceConfiguration0 = 0x0420,
            ProgramSequenceConfiguration1 = 0x0424,
            ProgramSequenceConfiguration2 = 0x0428,
            ReadSequenceConfiguration0 = 0x430,
            ReadSequenceConfiguration1 = 0x434,
            ReadSequenceConfiguration2 = 0x438,
            WriteEnableSequenceConfiguration = 0x440,
            StatusSequenceConfiguration0 = 0x450,
            StatusSequenceConfiguration1 = 0x454,
            StatusSequenceConfiguration2 = 0x458,
            StatusSequenceConfiguration3 = 0x45C,
            StatusSequenceConfiguration4 = 0x460,
            StatusSequenceConfiguration5 = 0x464,
            StatusSequenceConfiguration6 = 0x468,
            StatusSequenceConfiguration7 = 0x46C,
            StatusSequenceConfiguration8 = 0x470,
            StatusSequenceConfiguration9 = 0x474,
            StatusSequenceConfiguration10 = 0x478,
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
