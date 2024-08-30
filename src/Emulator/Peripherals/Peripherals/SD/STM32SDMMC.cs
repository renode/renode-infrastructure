//
// Copyright (c) 2010-2024 Antmicro
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
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.DMA;
using Antmicro.Renode.Peripherals.Sensors;
using Antmicro.Renode.Storage.SCSI.Commands;
using Antmicro.Renode.Utilities;

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
    public class STM32SDMMC : NullRegistrationPointPeripheralContainer<SDCard>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public STM32SDMMC(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            DMAReceive = new GPIO();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            responseFields = new IValueRegisterField[4];
            dataEndFlag = false;
            internalBuffer = new Queue<byte>();
            InitializeRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            responseFields[0].Value = 0;
            responseFields[1].Value = 0;
            responseFields[2].Value = 0;
            responseFields[3].Value = 0;
            RegisteredPeripheral?.Reset();
            internalBuffer.Clear();
            dataEndFlag = false;
            rxFifoHalfFullFlag = false;
            txFifoHalfEmptyFlag = false;
            UpdateInterrupts();
        }

        [IrqProvider]
        public GPIO IRQ { get; private set; }
        public GPIO DMAReceive { get; }

        public long Size => 0x400;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void InitializeRegisters()
        {
            Registers.Power.Define(this)
            .WithValueField(0, 2, name: "Power supply control bits (PWRCTRL)", valueProviderCallback: _ => 3)
            .WithReservedBits(2, 30);

            Registers.Arg.Define(this)
            .WithValueField(0, 32, out commandArgument, name: "Command Argument (CMDARG)");

            Registers.Cmd.Define(this)
            .WithEnumField(0, 6, out commandIndex, name: "Command Index (CMDINDEX)")
            .WithEnumField<DoubleWordRegister, ResponseWidth>(6, 2, out var responseWidth, name: "Wait for response bits (WAITRESP)")
            .WithTaggedFlag("CPSM waits for interrupt request (WAITINIT)", 8)
            .WithTaggedFlag("CPSM Waits for ends of data transfer (WAITPEND)", 9)
            .WithTaggedFlag("Command path state machine enable (CPSM)", 10)
            .WithTaggedFlag("SD I/O suspend command (SDIOSuspend)", 11)
            .WithReservedBits(12, 20)
            .WithWriteCallback((_, value) =>
            {
                var sdCard = RegisteredPeripheral;
                if(sdCard == null)
                {
                    this.Log(LogLevel.Warning, "Tried to send a command with index {0}, but no SD card is currently attached", commandIndex);
                    return;
                }
                var commandResult = sdCard.HandleCommand((uint) commandIndex.Value, (uint) commandArgument.Value);

                /* in case of no response there's no need to do anything extra */
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
                        responseFields[0].Value = commandResult.AsUInt32(0);
                        break;
                }
                ProcessCommand(sdCard, commandIndex.Value);
            });

            Registers.Fifo.Define(this)
                .WithValueField(0, 32, valueProviderCallback: _=> ReadBuffer())
                .WithReadCallback((_, __) => UpdateInterrupts())
                .WithWriteCallback((_, value) =>
                {
                    var sdCard = RegisteredPeripheral;
                    if(sdCard == null)
                    {
                        this.Log(LogLevel.Warning, "Tried to write to SD card, but no SD card is currently attached");
                        return;
                    }
                    WriteCard(sdCard, value);
                    writeDataAmount -= 4;
                    this.Log(LogLevel.Debug, "Remaining data to write {0}", writeDataAmount);
                    if(writeDataAmount == 0)
                    {
                        txFifoHalfEmptyFlag = false;
                        dataEndFlag = true;
                    }
                    UpdateInterrupts();
                });

            Registers.Resp1.Define(this)
                .WithValueField(0, 32, out responseFields[0], FieldMode.Read, name: "(CARDSTATUS1)");
            Registers.Resp2.Define(this)
                .WithValueField(0, 32, out responseFields[1], FieldMode.Read, name: "(CARDSTATUS2)");
            Registers.Resp3.Define(this)
                .WithValueField(0, 32, out responseFields[2], FieldMode.Read, name: "(CARDSTATUS3)");
            Registers.Resp4.Define(this)
                .WithValueField(0, 32, out responseFields[3], FieldMode.Read, name: "(CARDSTATUS4)");

            Registers.RespCmd.Define(this)
                .WithValueField(0, 6, name: "Response command index (RESPCMD)", valueProviderCallback: _ => (ulong)commandIndex.Value)
                .WithReservedBits(6, 26)
                .WithReadCallback((_, __) => UpdateInterrupts());

            Registers.Sta.Define(this)
                .WithFlag(0, FieldMode.Read, name: "Command CRC check failed (CCRCFAIL)", valueProviderCallback: _ => RegisteredPeripheral == null)
                .WithFlag(1, FieldMode.Read, name: "Data CRC check failed (DCRCFAIL)", valueProviderCallback: _ => RegisteredPeripheral == null)
                .WithFlag(2, FieldMode.Read, name: "Command response timeout (CTIMEOUT)", valueProviderCallback: _ => RegisteredPeripheral == null)
                .WithTaggedFlag("Data timeout (DTIMEOUT)", 3)
                .WithTaggedFlag("Transmit FIFO underrun error (TXUNDERR)", 4)
                .WithTaggedFlag("Receiverd FIFO overrun error (RXOVERR)", 5)
                .WithFlag(6, FieldMode.Read, name: "Command response received (CMDREND)", valueProviderCallback: _ => RegisteredPeripheral != null)
                .WithFlag(7, FieldMode.Read, name: "Command sent (CMDSENT)", valueProviderCallback: _ => RegisteredPeripheral != null)
                .WithFlag(8, FieldMode.Read, name: "Data end (DATAEND)", valueProviderCallback: _ => dataEndFlag)
                .WithReservedBits(9, 1)
                .WithFlag(10, FieldMode.Read, name: "Data block sent/received (DBCKEND)", valueProviderCallback: _ => RegisteredPeripheral != null)
                .WithFlag(11, FieldMode.Read, name: "Command transfer in progress (CMDACT)", valueProviderCallback: _ => false)
                .WithTaggedFlag("Data transmit in progress (TXACT)", 12)
                .WithTaggedFlag("Data receive in progress (RXACT)", 13)
                .WithFlag(14, FieldMode.Read, name: "Transmit FIFO half empty (TXFIFOHE)", valueProviderCallback: _ => txFifoHalfEmptyFlag)
                .WithFlag(15, FieldMode.Read, name: "Receive FIFO half full (RXFIFOHF)", valueProviderCallback: _ => rxFifoHalfFullFlag)
                .WithTaggedFlag("Transmit FIFO full (TXFIFOF)", 16)
                .WithTaggedFlag("Receive FIFO full (RXFIFOF)", 17)
                .WithTaggedFlag("Transmit FIFO empty (TXFIFOE)", 18)
                .WithTaggedFlag("Receive FIFO empty (RXFIFOE)", 19)
                .WithTaggedFlag("Data available in transmit FIFO (TXDAVL)", 20)
                .WithTaggedFlag("Data available in receive FIFO (RXDAVL)", 21)
                .WithTaggedFlag("SDIO interrupt received (SDIOT)", 22)
                .WithReservedBits(23, 9);

            Registers.DLen.Define(this)
                .WithValueField(0, 25, out dataLength, name: "Data length value (DATALENGTH)")
                .WithReservedBits(25, 7);

            Registers.Icr.Define(this)
                .WithTaggedFlag("CCRCFAIL flag clear bit (CCRCFAILC)", 0)
                .WithTaggedFlag("DCRCFAIL flag clear bit (DCRCFAILC)", 1)
                .WithTaggedFlag("CTIMEOUT flag clear bit (CTIMEOUTC)", 2)
                .WithTaggedFlag("DTIMEOUT flag clear bit (DTIMEOUTC)", 3)
                .WithTaggedFlag("TXUNDERR flag clear bit (TXUNDERRC)", 4)
                .WithTaggedFlag("RXOVERR flag clear bit (RXOVERRC)", 5)
                .WithTaggedFlag("CMDREND flag clear bit (CMDRENDC)", 6)
                .WithTaggedFlag("DCRCFAIL flag clear bit (DCRCFAILC)", 7)
                .WithFlag(8, FieldMode.Write, name: "DATAEND flag clear bit (DATAENDC)", writeCallback: (_, value) =>
                {
                    if(!value)
                    {
                        return;
                    }

                    dataEndFlag = false;
                    UpdateInterrupts();
                })
                .WithReservedBits(9, 1)
                .WithTaggedFlag("DBCKEND flag clear bit (DBCKENDC)", 10)
                .WithReservedBits(11, 11)
                .WithTaggedFlag("SDIOIT flag clear bit (SDIOITC)", 22)
                .WithReservedBits(23, 9);

            Registers.DCtrl.Define(this)
                .WithTaggedFlag("Data transfer enabled bit (DTEN)", 0)
                .WithTaggedFlag("Data transfer direction selection (DTDIR)", 1)
                .WithTaggedFlag("Data transfer mode selection (DTMODE)", 2)
                .WithFlag(3, out isDmaEnabled, name: "DMA enable bit")
                .WithTag("Data block size (DBLOCKSIZE)", 4, 4)
                .WithTaggedFlag("Read wait start (RWSTART)", 8)
                .WithTaggedFlag("Read wait stop (RWSTOP)", 9)
                .WithTaggedFlag("Read wait mode (RWMOD)", 10)
                .WithTaggedFlag("SD I/O enable funtions (SDIOEN)", 11)
                .WithReservedBits(12, 20);

            Registers.Mask.Define(this)
                .WithTaggedFlag("Command CRC fail interrupt enable (CCRCFAILIE)", 0)
                .WithTaggedFlag("Data CRC fail interrupt enable (DCRCFAILIE)", 1)
                .WithTaggedFlag("Command timeout interrupt enable (CTIMEOUTIE)", 2)
                .WithTaggedFlag("Data transfer enabled bit (DTIMEOUTIE)", 3)
                .WithTaggedFlag("Tx FIFO underrun error interrupt enable (TXUNDERRIE)", 4)
                .WithTaggedFlag("Rx FIFO overrun error interrupt enable (RXOVERRIE)", 5)
                .WithTaggedFlag("Command response received interrupt enable (CMDRENDIE)", 6)
                .WithTaggedFlag("Command sent interrupt enable (CMDSENTIE)", 7)
                .WithFlag(8, out dataEndItEnabled, name: "Data end interrupt enable (DATAENDIE)")
                .WithReservedBits(9, 1)
                .WithTaggedFlag("Data block end interrupt enable (DBCKENDIE)", 10)
                .WithTaggedFlag("Command acting interrupt enable (CMDACTIE)", 11)
                .WithTaggedFlag("Data transmit acting interrupt enable (TXACTIE)", 12)
                .WithTaggedFlag("Data receive acting interrupt enable (RXACTIE)", 13)
                .WithFlag(14, out txFifoHalfEmptyItEnabled, name: "Tx FIFO half empty interrupt enable (TXFIFOHEIE)")
                .WithFlag(15, out rxFifoHalfFullItEnabled, name: "Rx FIFO half full interrupt enable (RXFIFOHFIE)")
                .WithTaggedFlag("Tx FIFO full interrupt enable (TXFIFOFIE)", 16)
                .WithTaggedFlag("Rx FIFO full interrupt enable (RXFIFOFIE)", 17)
                .WithTaggedFlag("Tx FIFO empty interrupt enable (TXFIFOEIE)", 18)
                .WithTaggedFlag("Rx FIFO empty interrupt enable (RXFIFOEIE)", 19)
                .WithTaggedFlag("Data available in Tx FIFO interrupt enable (TXDAVLIE)", 20)
                .WithTaggedFlag("Data available in Rx FIFO interrupt enable (RXDAVLIE)", 21)
                .WithTaggedFlag("SDIO mode interrupt receied interrupt enable (SDIOITIE)", 22)
                .WithReservedBits(23, 9)
                .WithWriteCallback((_, __) => UpdateInterrupts());
        }

        private void ProcessCommand(SDCard sdCard, SDCardCommand command)
        {
            switch(command)
            {
                case SDCardCommand.GoIdleState:
                    Reset();
                    break;
                case SDCardCommand.ReadMultBlock:
                    ReadCard(sdCard, (uint) dataLength.Value);
                    rxFifoHalfFullFlag = true;
                    break;
                case SDCardCommand.WriteSingleBlock:
                    writeDataAmount = 512U;
                    txFifoHalfEmptyFlag = true;
                    break;
                case SDCardCommand.ReadSingleBlock:
                    ReadCard(sdCard, 512U);
                    rxFifoHalfFullFlag = true;
                    break;
                case SDCardCommand.WriteMultBlock:
                    writeDataAmount = (uint) dataLength.Value;
                    txFifoHalfEmptyFlag = true;
                    break;
                case SDCardCommand.SendStatus:
                    responseFields[0].Value = CardState;
                    break;
                default:
                    this.Log(LogLevel.Warning, "Calling command without explicit handling with index: {0}", command);
                    break;
            }
        }

        private uint ReadBuffer()
        {
            var internalBytes = internalBuffer.DequeueRange(4);
            if(internalBuffer.Count == 0)
            {
                dataEndFlag = true;
                rxFifoHalfFullFlag = false;
            }
            return internalBytes.ToUInt32Smart();
        }

        private void WriteCard(SDCard sdCard, uint data)
        {
            sdCard.WriteData(data.AsRawBytes());
        }

        private void ReadCard(SDCard sdCard, uint size)
        {
            var data = sdCard.ReadData(size);
            internalBuffer.EnqueueRange(data);
            if(!isDmaEnabled.Value)
            {
                return;
            }

            /* DMA reads data from FIFO in bursts of 4 bytes when this pin blinks */
            for(ulong i = 0; i < dataLength.Value / DmaReadChunk; i++)
            {
                DMAReceive.Blink();
            }
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(
                (dataEndFlag && dataEndItEnabled.Value) ||
                (rxFifoHalfFullFlag && rxFifoHalfFullItEnabled.Value) ||
                (txFifoHalfEmptyFlag && txFifoHalfEmptyItEnabled.Value)
            );
        }

        private IValueRegisterField commandArgument;
        private IValueRegisterField dataLength;

        private IEnumRegisterField<SDCardCommand> commandIndex;
        private IValueRegisterField[] responseFields;
        private IFlagRegisterField isDmaEnabled;
        private bool dataEndFlag;
        private bool rxFifoHalfFullFlag;
        private bool txFifoHalfEmptyFlag;
        private IFlagRegisterField dataEndItEnabled;
        private IFlagRegisterField rxFifoHalfFullItEnabled;
        private IFlagRegisterField txFifoHalfEmptyItEnabled;
        private ulong writeDataAmount;

        private const ulong CardState = 4 << 9;  /* HACK:  card state: transfer (otherwise it stucks here: https://github.com/zephyrproject-rtos/zephyr/blob/c008cbab1a05316139de191b0553ab6ccc0073ad/drivers/disk/sdmmc_stm32.c#L386) */
        private const ulong DmaReadChunk = 4;

        private readonly Queue<byte> internalBuffer;
        private enum Registers
        {
            Power = 0x00,
            Arg = 0x08,
            Cmd = 0x0C,
            RespCmd = 0x10,
            Resp1 = 0x14,
            Resp2 = 0x18,
            Resp3 = 0x1c,
            Resp4 = 0x20,
            DLen = 0x28,
            DCtrl = 0x2C,
            Sta = 0x34,
            Icr = 0x38,
            Mask = 0x3C,
            Fifo = 0x80
        }

        /* command ids: https://github.com/STMicroelectronics/stm32f7xx_hal_driver/blob/52bfa97ba66afc08481f6fd7631322593bd89691/Inc/stm32f7xx_ll_sdmmc.h#L171 */
        private enum SDCardCommand
        {
            GoIdleState = 0,
            SendStatus = 13,
            SetBlockLen = 16,
            ReadSingleBlock = 17,
            ReadMultBlock = 18,
            WriteSingleBlock = 24,
            WriteMultBlock = 25
        }

        private enum ResponseWidth
        {
            ShortResponse = 1,
            LongResponse = 3
        }
    }
}