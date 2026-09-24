//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

using SDCardAppCommand = Antmicro.Renode.Peripherals.SD.SDCard.SdCardApplicationSpecificCommand;
using SDCardCommand = Antmicro.Renode.Peripherals.SD.SDCard.SdCardCommand;

namespace Antmicro.Renode.Peripherals.SD
{
    /*
    * minimal SDMMC model working with zephyr driver and STM32 HAL for FAT and EXT2:
    *
    * references:
    *   - https://www.st.com/resource/en/reference_manual/dm00124865.pdf
    *   Zephyr
    *   - https://github.com/zephyrproject-rtos/zephyr/blob/c008cbab1a05316139de191b0553ab6ccc0073ad/drivers/disk/sdmmc_stm32.c
    *   STM HAL
    *   - https://github.com/STMicroelectronics/stm32f7xx_hal_driver/blob/master/Src/stm32f7xx_hal_sd.c
    *   - https://github.com/STMicroelectronics/stm32f7xx_hal_driver/blob/master/Src/stm32f7xx_ll_sdmmc.c
    */
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public abstract class STM32SDMMC : NullRegistrationPointPeripheralContainer<SDCard>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public STM32SDMMC(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            responseFields = new IValueRegisterField[4];
            ReadDataBuffer = new Queue<byte>();
            InitializeRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            GoIdle();
        }

        public GPIO IRQ { get; private set; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        protected virtual void ReadCard(SDCard sdCard, uint size)
        {
            var data = sdCard.ReadData(size);
            ReadDataBuffer.EnqueueRange(data);
        }

        protected virtual void WriteCard(SDCard sdCard, uint data)
        {
            sdCard.WriteData(data.AsRawBytes());
        }

        protected virtual void InitializeRegisters()
        {
            Registers.Power.Define(this)
                .WithValueField(0, 2, name: "SDMMC state control bits (PWRCTRL)", valueProviderCallback: _ => 3)
                .WithTaggedFlag("Voltage switch sequence start (VSWITCH)", 2)
                .WithTaggedFlag("Voltage switch procedure enable (VSWITCHEN)", 3)
                .WithTaggedFlag("Data and command direction signals polarity selection (DIRPOL)", 4)
                .WithReservedBits(5, 27);

            Clock = Registers.Clock.Define(this)
                .WithValueField(0, ClkDivWidth, name: "Clock divide factor (CLKDIV)");

            Registers.Arg.Define(this)
                .WithValueField(0, 32, out var commandArgument, name: "Command argument (CMDARG)");

            Cmd = Registers.Cmd.Define(this)
                .WithEnumField<DoubleWordRegister, SDCardCommand>(0, 6, out var commandIndex, name: "Command index (CMDINDEX)")
                .WithEnumField<DoubleWordRegister, ResponseWidth>(CommandFieldsOffset, 2, out var responseWidth, name: "Wait for response bits (WAITRESP)")
                .WithTaggedFlag("CPSM waits for interrupt request (WAITINT)", CommandFieldsOffset + 2)
                .WithTaggedFlag("CPSM waits for end of data transfer from DPSM (WAITPEND)", CommandFieldsOffset + 3)
                .WithFlag(CommandFieldsOffset + 4, out var commandStarted, FieldMode.Read | FieldMode.WriteToClear, name: "Command path state machine (CPSM) enable bit (CPSMEN)", writeCallback: (_, value) =>
                {
                    if(!value) return;
                    var sdCard = RegisteredPeripheral;
                    if(sdCard == null)
                    {
                        this.WarningLog("Tried to send a command with index {0}, but no SD card is currently attached", commandIndex);
                        cTimeout.Value = true;
                        UpdateInterrupts();
                        return;
                    }
                    cmdSent.Value = true;
                    cmdREnd.Value = true;
                    UpdateInterrupts();

                    var wasAppCommand = sdCard.TreatNextCommandAsAppCommand;
                    var commandResult = sdCard.HandleCommand((uint) commandIndex.Value, (uint) commandArgument.Value);

                    switch(responseWidth.Value)
                    {
                    case ResponseWidth.LongResponse:
                        responseFields[3].Value = commandResult.AsUInt32(0);
                        responseFields[3].Value &= 0xFFFFFFFE; /* lsb is always = 0 */
                        responseFields[2].Value = commandResult.AsUInt32(32);
                        responseFields[1].Value = commandResult.AsUInt32(64);
                        responseFields[0].Value = commandResult.AsUInt32(96);
                        break;
                    case ResponseWidth.ShortResponse:
                    case ResponseWidth.ShortResponseNoCrc:
                        responseFields[0].Value = commandResult.AsUInt32(0);
                        break;
                    case ResponseWidth.NoResponse:
                        break;
                    }
                    ProcessCommand(sdCard, commandIndex.Value, wasAppCommand);
                });

            Registers.RespCmd.Define(this)
                .WithValueField(0, 6, name: "Response command index (RESPCMD)", valueProviderCallback: _ => (ulong)commandIndex.Value)
                .WithReservedBits(6, 26);

            Registers.Resp1.Define(this)
                .WithValueField(0, 32, out responseFields[0], FieldMode.Read, name: "Card status (CARDSTATUS1)");
            Registers.Resp2.Define(this)
                .WithValueField(0, 32, out responseFields[1], FieldMode.Read, name: "Card status (CARDSTATUS2)");
            Registers.Resp3.Define(this)
                .WithValueField(0, 32, out responseFields[2], FieldMode.Read, name: "Card status (CARDSTATUS3)");
            Registers.Resp4.Define(this)
                .WithValueField(0, 32, out responseFields[3], FieldMode.Read, name: "Card status (CARDSTATUS4)");

            Registers.DataTimer.Define(this)
                .WithValueField(0, 32, name: "Data and R1b busy timeout period (DATATIME)"); // hush

            Registers.DataLength.Define(this)
                .WithValueField(0, 25, out dataLength, name: "Data length value (DATALENGTH)")
                .WithReservedBits(25, 7);

            DataCtrl = Registers.DataCtrl.Define(this)
                .WithTaggedFlag("Data transfer enable bit (DTEN)", 0)
                .WithFlag(1, out dataDirectionFromCard, name: "Data transfer direction selection (DTDIR)")
                .WithValueField(4, 4, out blockSizeField, name: "Data block size (DBLOCKSIZE)", writeCallback: (_, val) =>
                {
                    if(val == 15)
                    {
                        this.WarningLog("Data block size 15 is reserved");
                    }
                })
                .WithTaggedFlag("Read Wait start (RWSTART)", 8)
                .WithTaggedFlag("Read Wait stop (RWSTOP)", 9)
                .WithTaggedFlag("Read Wait mode (RWMOD)", 10)
                .WithTaggedFlag("SD I/O interrupt enable functions (SDIOEN)", 11);

            Registers.DataCount.Define(this)
                .WithTag("Data count value (DATACOUNT)", 0, 25)
                .WithReservedBits(25, 7);

            Registers.IntStatus.Define(this, resetValue: 0x90000)
                .WithFlag(0, FieldMode.Read, name: "Command response received (CRC check failed) (CCRCFAIL)", valueProviderCallback: _ => false)
                .WithFlag(1, FieldMode.Read, name: "Data block sent/received (CRC check failed) (DCRCFAIL)", valueProviderCallback: _ => false)
                .WithFlag(2, out cTimeout, FieldMode.Read, name: "Command response timeout (CTIMEOUT)")
                .WithTaggedFlag("Data timeout (DTIMEOUT)", 3)
                .WithTaggedFlag("Transmit FIFO underrun error (TXUNDERR)", 4)
                .WithTaggedFlag("Received FIFO overrun error (RXOVERR)", 5)
                .WithFlag(6, out cmdREnd, FieldMode.Read, name: "Command response received (CRC check passed, or no CRC) (CMDREND)")
                .WithFlag(7, out cmdSent, FieldMode.Read, name: "Command sent (no response required) (CMDSENT)")
                .WithFlag(8, out dataEnd, FieldMode.Read, name: "Data transfer ended correctly (DATAEND)")
                .WithTaggedFlag("Data transfer Hold (DHOLD)", 9)
                .WithFlag(10, out dBckEnd, FieldMode.Read, name: "Data block sent/received (DBCKEND)")
                .WithFlag(11, FieldMode.Read, name: "Command transfer in progress (CMDACT)", valueProviderCallback: _ => false)
                .WithTaggedFlag("Data transmit in progress (TXACT)", 12)
                .WithTaggedFlag("Data receive in progress (RXACT)", 13)
                .WithFlag(14, out txFifoHE, FieldMode.Read, name: "Transmit FIFO half empty: at least 8 words can be written into the FIFO (TXFIFOHE)")
                .WithFlag(15, out rxFifoHF, FieldMode.Read, name: "Receive FIFO half full: there are at least 8 words in the FIFO (RXFIFOHF)")
                .WithFlag(16, out txFifoF, FieldMode.Read, name: "Transmit FIFO full (TXFIFOF)")
                .WithTaggedFlag("Receive FIFO full (RXFIFOF)", 17)
                .WithTaggedFlag("Transmit FIFO empty (TXFIFOE)", 18)
                .WithFlag(19, out rxFifoE, FieldMode.Read, name: "Receive FIFO empty (RXFIFOE)")
                .WithTaggedFlag("Data available in transmit FIFO (TXDAVL)", 20)
                .WithTaggedFlag("Data available in receive FIFO (RXDAVL)", 21)
                .WithTaggedFlag("SDIO interrupt received (SDIOT)", 22)
                .WithTaggedFlag("Boot acknowledgment received (boot acknowledgment check fail) (ACKFAIL)", 23)
                .WithTaggedFlag("Boot acknowledgment timeout (ACKTIMEOUT)", 24)
                .WithTaggedFlag("Voltage switch critical timing section completion (VSWEND)", 25)
                .WithTaggedFlag("SDMMC_CK stopped in Voltage switch procedure (CKSTOP)", 26)
                .WithTaggedFlag("IDMA transfer error (IDMATE)", 27)
                .WithTaggedFlag("IDMA buffer transfer complete (IDMABTC)", 28)
                .WithReservedBits(29, 3);

            Func<IFlagRegisterField, Action<bool, bool>> clearCb = (IFlagRegisterField field) => (_, value) =>
            {
                if(value)
                {
                    field.Value = false;
                }
            };

            IntClear = Registers.IntClear.Define(this)
                .WithFlag(0, name: "CCRFAIL flag clear bit (CCRFAILC)") // hush
                .WithFlag(1, name: "DCRCFAIL flag clear bit (DCRCFAILC)") // hush
                .WithFlag(2, FieldMode.WriteToClear, name: "CTIMEOUT flag clear bit (CTIMEOUTC)", writeCallback: clearCb(cTimeout))
                .WithFlag(3, name: "DTIMEOUT flag clear bit (DTIMEOUTC)") // hush
                .WithFlag(4, name: "TXUNDERR flag clear bit (TXUNDERRC)") // hush
                .WithFlag(5, name: "RXOVERR flag clear bit (RXOVERRC)") // hush
                .WithFlag(6, FieldMode.WriteToClear, name: "CMDREND flag clear bit (CMDRENDC)", writeCallback: clearCb(cmdREnd))
                .WithFlag(7, FieldMode.WriteToClear, name: "CMDSENT flag clear bit (CMDSENTC)", writeCallback: clearCb(cmdSent))
                .WithFlag(8, FieldMode.WriteToClear, name: "DATAEND flag clear bit (DATAENDC)", writeCallback: clearCb(dataEnd))
                .WithFlag(9, name: "DHOLD flag clear bit (DHOLDC)") // hush
                .WithFlag(10, FieldMode.WriteToClear, name: "DBCKEN flag clear bit (DBCKEND)", writeCallback: clearCb(dBckEnd))
                .WithFlag(11, name: "DABORT flag clear bit (DABORTC)") // hush
                .WithReservedBits(12, 9)
                .WithFlag(21, name: "BUSYD0END flag clear bit (BUSYD0ENDC)") // hush
                .WithTaggedFlag("SDIOIT flag clear bit (SDIOITC)", 22)
                .WithTaggedFlag("ACKFAIL flag clear bit (ACKFAILC)", 23)
                .WithTaggedFlag("ACKTIMEOUT flag clear bit (ACKTIMEOUTC)", 24)
                .WithTaggedFlag("VSWEND flag clear bit (VSWENDC)", 25)
                .WithTaggedFlag("CKSTOP flag clear bit (CKSTOPC)", 26)
                .WithFlag(27, name: "IDMATE flag clear bit (IDMATEC)") // hush
                .WithFlag(28, name: "IDMABTC flag clear bit (IDMABTCC)") // hush
                .WithWriteCallback((_, __) => UpdateInterrupts());

            IntMask = Registers.IntMask.Define(this)
                .WithTaggedFlag("Command response received (CRC check failed) interrupt enable (CCRCFAILIE)", 0)
                .WithFlag(1, name: "Data block sent/received (CRC check failed) interrupt enable (DCRCFAILIE)") // hush
                .WithFlag(2, out cTimeoutEn, name: "Command response timeout interrupt enable (CTIMEOUTIE)")
                .WithFlag(3, name: "Data timeout interrupt enable (DTIMEOUTIE)") // hush
                .WithFlag(4, name: "Transmit FIFO underrun error interrupt enable (TXUNDERRIE)") // hush
                .WithFlag(5, name: "Received FIFO overrun error interrupt enable (RXOVERRIE)") // hush
                .WithFlag(6, out cmdREndEn, name: "Command response received (CRC check passed, or no CRC) interrupt enable (CMDRENDIE)")
                .WithFlag(7, out cmdSentEn, name: "Command sent (no response required) interrupt enable (CMDSENTIE)")
                .WithFlag(8, out dataEndEn, name: "Data transfer ended correctly interrupt enable (DATAENDIE)")
                .WithTaggedFlag("Data transfer Hold interrupt enable (DHOLDIE)", 9)
                .WithFlag(10, out dBckEndEn, name: "Data block sent/received interrupt enable (DBCKENDIE)")
                .WithTaggedFlag("Command transfer in progress interrupt enable (CMDACTIE)", 11)
                .WithTaggedFlag("Data transmit in progress interrupt enable (TXACTIE)", 12)
                .WithTaggedFlag("Data receive in progress interrupt enable (RXACTIE)", 13)
                .WithFlag(14, out txFifoHEEn, name: "Transmit FIFO half empty: at least 8 words can be written into the FIFO interrupt enable (TXFIFOHEIE)")
                .WithFlag(15, out rxFifoHFEn, name: "Receive FIFO half full: there are at least 8 words in the FIFO interrupt enable (RXFIFOHFIE)")
                .WithFlag(16, out txFifoFEn, name: "Transmit FIFO full interrupt enable (TXFIFOFIE)")
                .WithTaggedFlag("Receive FIFO full interrupt enable (RXFIFOFIE)", 17)
                .WithTaggedFlag("Transmit FIFO empty interrupt enable (TXFIFOEIE)", 18)
                .WithFlag(19, out rxFifoEEn, name: "Receive FIFO empty interrupt enable (RXFIFOEIE)")
                .WithTaggedFlag("Data available in transmit FIFO interrupt enable (TXDAVLIE)", 20)
                .WithTaggedFlag("Data available in receive FIFO interrupt enable (RXDAVLIE)", 21)
                .WithTaggedFlag("SDIO interrupt received interrupt enable (SDIOITIE)", 22)
                .WithTaggedFlag("Boot acknowledgment received (boot acknowledgment check fail) interrupt enable (ACKFAILIE)", 23)
                .WithTaggedFlag("Boot acknowledgment timeout interrupt enable (ACKTIMEOUTIE)", 24)
                .WithTaggedFlag("Voltage switch critical timing section completion interrupt enable (VSWENDIE)", 25)
                .WithTaggedFlag("SDMMC_CK stopped in Voltage switch procedure interrupt enable (CKSTOPIE)", 26)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("IDMA transfer error interrupt enable (IDMABTCIE)", 28)
                .WithReservedBits(29, 3)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.Fifo.Define(this)
                .WithValueField(0, 32, name: "Receive and transmit FIFO data (FIFODATA)", valueProviderCallback: _ => ReadBuffer(), writeCallback: (_, value) => WriteBuffer((uint)value));
        }

        protected uint ReadBuffer()
        {
            var internalBytes = ReadDataBuffer.DequeueRange(4);
            var val = internalBytes.ToUInt32Smart();
            if(ReadDataBuffer.Count == 0)
            {
                rxFifoHF.Value = false;
                rxFifoE.Value = true;
                dataEnd.Value = true;
                dBckEnd.Value = true;
                UpdateInterrupts();
            }
            return val;
        }

        protected void WriteBuffer(uint data)
        {
            var sdCard = RegisteredPeripheral;
            if(sdCard == null)
            {
                this.WarningLog("Tried to write to SD card, but no SD card is currently attached");
                return;
            }
            WriteCard(sdCard, data);
            WriteDataLeft -= 4;
            this.DebugLog("Remaining data to write {0}", WriteDataLeft);

            if(WriteDataLeft == 0)
            {
                txFifoHE.Value = false;
                txFifoF.Value = true;
                dataEnd.Value = true;
                dBckEnd.Value = true;
                UpdateInterrupts();
            }
        }

        protected ulong WriteDataLeft { get; private set; }

        protected virtual bool TxFifoFEnableable { get => false; }

        protected virtual bool RxFifoEEnableable { get => false; }

        protected abstract int ClkDivWidth { get; }

        protected abstract int CommandFieldsOffset { get; }

        protected DoubleWordRegister Clock;
        protected DoubleWordRegister Cmd;
        protected DoubleWordRegister DataCtrl;

        protected readonly Queue<byte> ReadDataBuffer;

        private void ProcessRead(SDCard sdCard, uint length = uint.MaxValue, bool ignoreBlockSize = false)
        {
            if(!dataDirectionFromCard.Value)
            {
                this.WarningLog("Read transfer while DTDIR is set");
            }
            ReadCard(sdCard, ignoreBlockSize ? length : Math.Min(BlockSize, length));
            rxFifoHF.Value = true;
            rxFifoE.Value = false;
            UpdateInterrupts();
        }

        private void ProcessWrite(uint length = uint.MaxValue, bool ignoreBlockSize = false)
        {
            if(dataDirectionFromCard.Value)
            {
                this.WarningLog("Write transfer while DTDIR is clear");
            }
            WriteDataLeft = ignoreBlockSize ? length : Math.Min(BlockSize, length);
            txFifoHE.Value = true;
            txFifoF.Value = false;
            UpdateInterrupts();
        }

        private void ProcessDataCommand(SDCard sdCard, SDCardCommand command)
        {
            switch(command)
            {
            case SDCardCommand.ReadSingleBlock_CMD17:
                ProcessRead(sdCard);
                break;
            case SDCardCommand.ReadMultipleBlocks_CMD18:
                ProcessRead(sdCard, (uint)dataLength.Value, true);
                break;
            case SDCardCommand.WriteSingleBlock_CMD24:
                ProcessWrite();
                break;
            case SDCardCommand.WriteMultipleBlocks_CMD25:
                ProcessWrite((uint)dataLength.Value, true);
                break;
            case (SDCardCommand)SDCardAppCommand.SendSDCardStatus_ACMD13:
                ProcessRead(sdCard, 64);
                break;
            case (SDCardCommand)SDCardAppCommand.SendSDConfigurationRegister_ACMD51:
                ProcessRead(sdCard, 8);
                break;
            }
        }

        private void ProcessCommand(SDCard sdCard, SDCardCommand command, bool wasAppCommand)
        {
            switch(command)
            {
            case SDCardCommand.GoIdleState_CMD0:
                GoIdle();
                break;
            case SDCardCommand.SendStatus_CMD13:
                // CMD13 sends data only when it is ACMD13 (ie. was treated as an application command)
                if(wasAppCommand) goto case SDCardCommand.ReadSingleBlock_CMD17;
                break;
            case SDCardCommand.ReadSingleBlock_CMD17:
            case SDCardCommand.ReadMultipleBlocks_CMD18:
            case SDCardCommand.WriteSingleBlock_CMD24:
            case SDCardCommand.WriteMultipleBlocks_CMD25:
            case (SDCardCommand)SDCardAppCommand.SendSDConfigurationRegister_ACMD51:
                // Need to delay the data so that firmware never consumes all data before reading the response
                Machine.LocalTimeSource.ExecuteInNearestSyncedState(_ => ProcessDataCommand(sdCard, command));
                break;
            case SDCardCommand.SendCardIdentification_CMD2:
            case SDCardCommand.SendRelativeAddress_CMD3:
            case SDCardCommand.CheckSwitchableFunction_CMD6:
            case SDCardCommand.SendCardSpecificData_CMD9:
            case SDCardCommand.SelectDeselectCard_CMD7:
            case SDCardCommand.StopTransmission_CMD12:
            case SDCardCommand.SetBlockLength_CMD16:
            case SDCardCommand.SendInterfaceConditionCommand_CMD8:
            case SDCardCommand.AppCommand_CMD55:
            case (SDCardCommand)SDCardAppCommand.SendOperatingConditionRegister_ACMD41:
                break;
            default:
                this.WarningLog("Calling command without explicit handling with index: {0}", command);
                break;
            }
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(
                   (txFifoHE.Value && txFifoHEEn.Value)
                || (TxFifoFEnableable && txFifoF.Value && txFifoFEn.Value)
                || (rxFifoHF.Value && rxFifoHFEn.Value)
                || (RxFifoEEnableable && rxFifoE.Value && rxFifoEEn.Value)
                || (dataEnd.Value && dataEndEn.Value)
                || (cmdSent.Value && cmdSentEn.Value)
                || (cmdREnd.Value && cmdREndEn.Value)
                || (dBckEnd.Value && dBckEndEn.Value)
                || (cTimeout.Value && cTimeoutEn.Value)
            );
        }

        private void GoIdle()
        {
            RegisteredPeripheral?.Reset();
            ReadDataBuffer.Clear();
            WriteDataLeft = 0;
            rxFifoE.Value = true;
            rxFifoHF.Value = false;
            txFifoF.Value = true;
            txFifoHE.Value = false;
            UpdateInterrupts();
        }

        private uint BlockSize { get => 1u << (byte)blockSizeField.Value; }

        private IValueRegisterField dataLength;
        private IValueRegisterField blockSizeField;
        private IFlagRegisterField dataDirectionFromCard;

        private IFlagRegisterField txFifoHE;
        private IFlagRegisterField txFifoF;
        private IFlagRegisterField rxFifoHF;
        private IFlagRegisterField rxFifoE;
        private IFlagRegisterField dataEnd;
        private IFlagRegisterField cmdSent;
        private IFlagRegisterField cmdREnd;
        private IFlagRegisterField dBckEnd;
        private IFlagRegisterField cTimeout;

        private IFlagRegisterField txFifoHEEn;
        private IFlagRegisterField txFifoFEn;
        private IFlagRegisterField rxFifoHFEn;
        private IFlagRegisterField rxFifoEEn;
        private IFlagRegisterField dataEndEn;
        private IFlagRegisterField cmdSentEn;
        private IFlagRegisterField cmdREndEn;
        private IFlagRegisterField dBckEndEn;
        private IFlagRegisterField cTimeoutEn;

        private DoubleWordRegister IntClear;
        private DoubleWordRegister IntMask;

        private readonly IValueRegisterField[] responseFields;

        private enum Registers
        {
            Power = 0x00,
            Clock = 0x04,
            Arg = 0x08,
            Cmd = 0x0C,
            RespCmd = 0x10,
            Resp1 = 0x14,
            Resp2 = 0x18,
            Resp3 = 0x1c,
            Resp4 = 0x20,
            DataTimer = 0x24,
            DataLength = 0x28,
            DataCtrl = 0x2C,
            DataCount = 0x30,
            IntStatus = 0x34,
            IntClear = 0x38,
            IntMask = 0x3C,
            Fifo = 0x80
        }

        private enum ResponseWidth
        {
            NoResponse = 0,
            ShortResponse = 1,
            ShortResponseNoCrc = 2,
            LongResponse = 3
        }
    }
}
