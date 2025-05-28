//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class ZynqMP_GQSPI : NullRegistrationPointPeripheralContainer<GenericSpiFlash>, IKnownSize, IDoubleWordPeripheral
    {
        public ZynqMP_GQSPI(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            sysbus = machine.GetSystemBus(this);

            registers = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            registers.Reset();

            receiveFifo.Clear();
            transmitFifo.Clear();
            genericFifo.Clear();

            dmaWrittenWords = 0;
            bytesToReceive = 0;
            bytesToTransmit = 0;

            UpdateInterrupts();
        }

        public long Size => 0x1000;
        public GPIO IRQ { get; private set; }

        private void UpdateInterrupts()
        {
            var irq = false;

            irq |= (rxFifoEmptyInterruptStatus.Value = RxFifoEmpty) && rxFifoEmptyInterruptEnabled.Value;
            irq |= (genericFifoFullInterruptStatus.Value = GenericFifoFull) && genericFifoFullInterruptEnabled.Value;
            irq |= (genericFifoNotFullInterruptStatus.Value = GenericFifoNotFull) && genericFifoNotFullInterruptEnabled.Value;
            irq |= (txFifoEmptyInterruptStatus.Value = TxFifoEmpty) && txFifoEmptyInterruptEnabled.Value;
            irq |= (genericFifoEmptyInterruptStatus.Value = GenericFifoEmpty) && genericFifoEmptyInterruptEnabled.Value;
            irq |= (rxFifoFullInterruptStatus.Value = RxFifoFull) && rxFifoFullInterruptEnabled.Value;
            irq |= (rxFifoNotEmptyInterruptStatus.Value = RxFifoNotEmpty) && rxFifoNotEmptyInterruptEnabled.Value;
            irq |= (txFifoFullInterruptStatus.Value = TxFifoFull) && txFifoFullInterruptEnabled.Value;
            irq |= (txFifoNotFullInterruptStatus.Value = TxFifoNotFull) && txFifoNotFullInterruptEnabled.Value;
            irq |= (pollTimeExpireInterruptStatus.Value = PollTimeExpire) && pollTimeExpireInterruptEnabled.Value;

            IRQ.Set(irq);
        }

        private void DefineRegisters()
        {
            BaseRegisters.LinearConfig.Define(registers, name: "LQSPI_CFG")
                .WithValueField(0, 31)
                .WithFlag(31, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        this.Log(LogLevel.Error, "Legacy LQSPI mode not supported");
                    }
                });

            BaseRegisters.Enable.Define(registers, name: "LQSPI_En_REG")
                .WithFlag(0, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        this.Log(LogLevel.Error, "Legacy LQSPI mode not supported");
                    }
                })
                .WithReservedBits(1, 31);

            GenericQSPIRegisters.Config.Define(registers, name: "GQSPI_CFG")
                .WithReservedBits(0, 1)
                .WithFlag(1, name: "CLK_POL")
                .WithFlag(2, name: "CLK_PH")
                .WithValueField(3, 3, name: "BAUD_RATE_DIV")
                .WithReservedBits(6, 13)
                .WithFlag(19, name: "WP_HOLD")
                .WithFlag(20, name: "EN_POLL_TIMEOUT")
                .WithReservedBits(21, 5)
                .WithFlag(26, out gqspiBigEndian, name: "ENDIAN")
                .WithReservedBits(27, 1)
                .WithFlag(28, name: "START_GEN_FIFO", writeCallback: (_, value) =>
                {
                    if(value && genFifoManualStart.Value)
                    {
                        this.Log(LogLevel.Noisy, "Triggered manual start");
                        ProcessGenericFifo();
                    }
                })
                .WithFlag(29, out genFifoManualStart, name: "GEN_FIFO_START_MODE")
                .WithEnumField(30, 2, out gqspiFlashMemoryMode)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            GenericQSPIRegisters.InterruptStatus.Define(registers, name: "GQSPI_ISR")
                .WithReservedBits(0, 1)
                .WithFlag(1, out pollTimeExpireInterruptStatus, mode: FieldMode.ReadToClear, name: "Poll_Time_Expire")
                .WithFlag(2, out txFifoNotFullInterruptStatus, mode: FieldMode.ReadToClear, name: "TX_FIFO_not_full")
                .WithFlag(3, out txFifoFullInterruptStatus, mode: FieldMode.ReadToClear, name: "TX_FIFO_full")
                .WithFlag(4, out rxFifoNotEmptyInterruptStatus, mode: FieldMode.ReadToClear, name: "RX_FIFO_not_empty")
                .WithFlag(5, out rxFifoFullInterruptStatus, mode: FieldMode.ReadToClear, name: "RX_FIFO_full")
                .WithReservedBits(6, 0)
                .WithFlag(7, out genericFifoEmptyInterruptStatus, mode: FieldMode.ReadToClear, name: "Gen_FIFO_Empty")
                .WithFlag(8, out txFifoEmptyInterruptStatus, mode: FieldMode.ReadToClear, name: "TX_FIFO_EMPTY")
                .WithFlag(9, out genericFifoNotFullInterruptStatus, mode: FieldMode.ReadToClear, name: "Gen_FIFO_not_full")
                .WithFlag(10, out genericFifoFullInterruptStatus, mode: FieldMode.ReadToClear, name: "Gen_FIFO_full")
                .WithFlag(11, out rxFifoEmptyInterruptStatus, mode: FieldMode.ReadToClear, name: "RX_FIFO_EMPTY")
                .WithReservedBits(12, 20);

            GenericQSPIRegisters.InterruptEnable.Define(registers, name: "GQSPI_IER")
                .WithReservedBits(0, 1)
                .WithFlag(1, out pollTimeExpireInterruptEnabled, mode: FieldMode.Set, name: "Poll_Time_Expire")
                .WithFlag(2, out txFifoNotFullInterruptEnabled, mode: FieldMode.Set, name: "TX_FIFO_not_full")
                .WithFlag(3, out txFifoFullInterruptEnabled, mode: FieldMode.Set, name: "TX_FIFO_full")
                .WithFlag(4, out rxFifoNotEmptyInterruptEnabled, mode: FieldMode.Set, name: "RX_FIFO_not_empty")
                .WithFlag(5, out rxFifoFullInterruptEnabled, mode: FieldMode.Set, name: "RX_FIFO_full")
                .WithReservedBits(6, 0)
                .WithFlag(7, out genericFifoEmptyInterruptEnabled, mode: FieldMode.Set, name: "Gen_FIFO_Empty")
                .WithFlag(8, out txFifoEmptyInterruptEnabled, mode: FieldMode.Set, name: "TX_FIFO_EMPTY")
                .WithFlag(9, out genericFifoNotFullInterruptEnabled, mode: FieldMode.Set, name: "Gen_FIFO_not_full")
                .WithFlag(10, out genericFifoFullInterruptEnabled, mode: FieldMode.Set, name: "Gen_FIFO_full")
                .WithFlag(11, out rxFifoEmptyInterruptEnabled, mode: FieldMode.Set, name: "RX_FIFO_EMPTY")
                .WithReservedBits(12, 20)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            GenericQSPIRegisters.InterruptDisable.Define(registers, name: "GQSPI_IDR")
                .WithReservedBits(0, 1)
                .WithFlag(1, writeCallback: (_, value) => pollTimeExpireInterruptEnabled.Value &= !value, mode: FieldMode.Write, name: "Poll_Time_Expire")
                .WithFlag(2, writeCallback: (_, value) => txFifoNotFullInterruptEnabled.Value &= !value, mode: FieldMode.Write, name: "TX_FIFO_not_full")
                .WithFlag(3, writeCallback: (_, value) => txFifoFullInterruptEnabled.Value &= !value, mode: FieldMode.Write, name: "TX_FIFO_full")
                .WithFlag(4, writeCallback: (_, value) => rxFifoNotEmptyInterruptEnabled.Value &= !value, mode: FieldMode.Write, name: "RX_FIFO_not_empty")
                .WithFlag(5, writeCallback: (_, value) => rxFifoFullInterruptEnabled.Value &= !value, mode: FieldMode.Write, name: "RX_FIFO_full")
                .WithReservedBits(6, 0)
                .WithFlag(7, writeCallback: (_, value) => genericFifoEmptyInterruptEnabled.Value &= !value, mode: FieldMode.Write, name: "Gen_FIFO_Empty")
                .WithFlag(8, writeCallback: (_, value) => txFifoEmptyInterruptEnabled.Value &= !value, mode: FieldMode.Write, name: "TX_FIFO_EMPTY")
                .WithFlag(9, writeCallback: (_, value) => genericFifoNotFullInterruptEnabled.Value &= !value, mode: FieldMode.Write, name: "Gen_FIFO_not_full")
                .WithFlag(10, writeCallback: (_, value) => genericFifoFullInterruptEnabled.Value &= !value, mode: FieldMode.Write, name: "Gen_FIFO_full")
                .WithFlag(11, writeCallback: (_, value) => rxFifoEmptyInterruptEnabled.Value &= !value, mode: FieldMode.Write, name: "RX_FIFO_EMPTY")
                .WithReservedBits(12, 20)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            GenericQSPIRegisters.InterruptMask.Define(registers, name: "GQSPI_IMASK")
                .WithReservedBits(0, 1)
                .WithFlag(1, valueProviderCallback: _ => !pollTimeExpireInterruptEnabled.Value, mode: FieldMode.Read, name: "Poll_Time_Expire")
                .WithFlag(2, valueProviderCallback: _ => !txFifoNotFullInterruptEnabled.Value, mode: FieldMode.Read, name: "TX_FIFO_not_full")
                .WithFlag(3, valueProviderCallback: _ => !txFifoFullInterruptEnabled.Value, mode: FieldMode.Read, name: "TX_FIFO_full")
                .WithFlag(4, valueProviderCallback: _ => !rxFifoNotEmptyInterruptEnabled.Value, mode: FieldMode.Read, name: "RX_FIFO_not_empty")
                .WithFlag(5, valueProviderCallback: _ => !rxFifoFullInterruptEnabled.Value, mode: FieldMode.Read, name: "RX_FIFO_full")
                .WithReservedBits(6, 0)
                .WithFlag(7, valueProviderCallback: _ => !genericFifoEmptyInterruptEnabled.Value, mode: FieldMode.Read, name: "Gen_FIFO_Empty")
                .WithFlag(8, valueProviderCallback: _ => !txFifoEmptyInterruptEnabled.Value, mode: FieldMode.Read, name: "TX_FIFO_EMPTY")
                .WithFlag(9, valueProviderCallback: _ => !genericFifoNotFullInterruptEnabled.Value, mode: FieldMode.Read, name: "Gen_FIFO_not_full")
                .WithFlag(10, valueProviderCallback: _ => !genericFifoFullInterruptEnabled.Value, mode: FieldMode.Read, name: "Gen_FIFO_full")
                .WithFlag(11, valueProviderCallback: _ => !rxFifoEmptyInterruptEnabled.Value, mode: FieldMode.Read, name: "RX_FIFO_EMPTY")
                .WithReservedBits(12, 20);

            GenericQSPIRegisters.Enable.Define(registers, name: "GQSPI_En_REG")
                .WithFlag(0, out gqspiEnabled)
                .WithValueField(1, 31, mode: FieldMode.Read);

            GenericQSPIRegisters.TxData.Define(registers, name: "GQSPI_TXD")
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) =>
                {
                    this.Log(LogLevel.Noisy, "Writing to TX FIFO");
                    transmitFifo.Enqueue((uint)value);
                }, name: "TX_DATA")
                .WithWriteCallback((_, __) => UpdateTransferQueues())
                .WithWriteCallback((_, __) => UpdateInterrupts());

            GenericQSPIRegisters.RxData.Define(registers, name: "GQSPI_RXD")
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        this.Log(LogLevel.Noisy, "Reading from RX FIFO");
                        if(receiveFifo.Count == 0)
                        {
                            this.Log(LogLevel.Noisy, "Attempted to read from empty RX FIFO");
                            return 0;
                        }
                        return receiveFifo.Dequeue();
                    },
                    name: "RX_DATA")
                .WithReadCallback((_, __) => UpdateInterrupts());

            GenericQSPIRegisters.TxThreshold.Define(registers, name: "GQSPI_TX_THRESH")
                .WithValueField(0, 6, out txThreshold, name: "Level_RX_FIFO")
                .WithReservedBits(6, 26);

            GenericQSPIRegisters.RxThreshold.Define(registers, name: "GQSPI_RX_THRESH")
                .WithValueField(0, 6, out rxThreshold, name: "Level_TX_FIFO")
                .WithReservedBits(6, 26);

            GenericQSPIRegisters.GPIO.Define(registers, name: "GQSPI_GPIO")
                .WithFlag(0, name: "WP_N")
                .WithReservedBits(1, 31);

            GenericQSPIRegisters.LoopbackClockDelay.Define(registers, 0x33, name: "GQSPI_LPBK_DLY_ADJ")
                .WithValueField(0, 3, name: "DLY0")
                .WithValueField(3, 2, name: "DLY1")
                .WithFlag(5, name: "USE_LPBK")
                .WithReservedBits(6, 26);

            GenericQSPIRegisters.GenericFifo.Define(registers, name: "GQSPI_GEN_FIFO")
                .WithValueField(0, 8, out gqspiCommandImmediate, mode: FieldMode.Write)
                .WithFlag(8, out gqspiCommandDataXref, mode: FieldMode.Write)
                .WithFlag(9, out gqspiCommandExponent, mode: FieldMode.Write)
                .WithEnumField(10, 2, out gqspiCommandSpiMode, mode: FieldMode.Write)
                .WithFlag(12, out gqspiCommandCsLower, mode: FieldMode.Write)
                .WithFlag(13, out gqspiCommandCsUpper, mode: FieldMode.Write)
                .WithFlag(14, out gqspiCommandDataBusLower, mode: FieldMode.Write)
                .WithFlag(15, out gqspiCommandDataBusUpper, mode: FieldMode.Write)
                .WithFlag(16, out gqspiCommandTransmit, mode: FieldMode.Write)
                .WithFlag(17, out gqspiCommandReceive, mode: FieldMode.Write)
                .WithFlag(18, out gqspiCommandStripe, mode: FieldMode.Write)
                .WithFlag(19, out gqspiCommandPoll, mode: FieldMode.Write)
                .WithReservedBits(20, 12)
                .WithWriteCallback((_, __) => EnqueueGenericFifo())
                .WithWriteCallback((_, __) => UpdateInterrupts());

            GenericQSPIRegisters.Select.Define(registers, name: "GQSPI_SEL")
                .WithFlag(0, out gqspiSelected)
                .WithReservedBits(1, 31);

            GenericQSPIRegisters.FifoControl.Define(registers, name: "GQSPI_FIFO_CTRL")
                .WithFlag(0, writeCallback: (_, value) => { if(value) genericFifo.Clear(); }, mode: FieldMode.Write)
                .WithFlag(1, writeCallback: (_, value) => { if(value) transmitFifo.Clear(); }, mode: FieldMode.Write)
                .WithFlag(2, writeCallback: (_, value) => { if(value) receiveFifo.Clear(); }, mode: FieldMode.Write)
                .WithReservedBits(3, 29);

            GenericQSPIRegisters.GenericFifoThreshold.Define(registers, name: "GQSPI_GF_THRESH")
                .WithValueField(0, 5, out genericFifoThreshold, name: "Level_GF_FIFO")
                .WithReservedBits(5, 27);

            GenericQSPIRegisters.PollConfig.Define(registers, name: "GQSPI_POLL_CFG")
                .WithValueField(0, 8, name: "DATA_VALUE")
                .WithValueField(8, 8, name: "MASK_EN")
                .WithReservedBits(16, 14)
                .WithFlag(30, name: "EN_MASK_LOWER")
                .WithFlag(31, name: "EN_MASK_UPPER");

            GenericQSPIRegisters.PollTimeout.Define(registers, name: "GQSPI_P_TIMEOUT")
                .WithValueField(0, 32, name: "VALUE");

            GenericQSPIRegisters.DataDelay.Define(registers, name: "QSPI_DATA_DLY_ADJ")
                .WithReservedBits(0, 28)
                .WithValueField(28, 3, name: "DATA_DLY_ADJ")
                .WithFlag(31, name: "USE_DATA_DLY");

            GenericQSPIRegisters.ModuleId.Define(registers, ModuleId, name: "MOD_ID")
                .WithValueField(0, 32);

            DMARegisters.DmaDestinationAddress.Define(registers, name: "QSPIDMA_DST_ADDR")
                .WithValueField(0, 32, out dmaAddress, mode: FieldMode.Write);

            DMARegisters.DmaDestinationSize.Define(registers, name: "QSPIDMA_DST_SIZE")
                .WithReservedBits(0, 2)
                .WithValueField(2, 30, out dmaSize, mode: FieldMode.Write);

            DMARegisters.DmaDestinationStatus.Define(registers, name: "QSPIDMA_DST_STS")
                .WithFlag(0, mode: FieldMode.Read, name: "BUSY")
                .WithValueField(13, 3, out dmaWritesDone, mode: FieldMode.Read | FieldMode.WriteToClear)
                .WithReservedBits(16, 16);

            DMARegisters.DmaDestinationControl.Define(registers, 0x803FFA00, name: "QSPIDMA_DST_CTRL")
                .WithFlag(0, name: "PAUSE_MEM")
                .WithFlag(1, name: "PAUSE_STRM")
                .WithValueField(2, 8, out dmaFifoThreshold, name: "FIFO_THRESH")
                .WithValueField(10, 12, name: "TIMEOUT_VAL")
                .WithFlag(22, name: "AXI_BRST_TYPE")
                .WithFlag(23, out dmaReversedByteOrdering, name: "ENDIANNESS")
                .WithFlag(24, name: "APB_ERR_RESP")
                .WithValueField(25, 7, name: "FIFO_LVL_HIT_THRESH");

            DMARegisters.DmaDestinationInterruptStatus.Define(registers, name: "QSPIDMA_DST_I_STS")
                .WithFlag(1, out dmaDoneInterruptStatus, mode: FieldMode.Read | FieldMode.WriteToClear, name: "DONE")
                .WithFlag(2, mode: FieldMode.Read | FieldMode.WriteToClear, name: "AXI_BRESP_ERR")
                .WithFlag(3, mode: FieldMode.Read | FieldMode.WriteToClear, name: "TIMEOUT_STRM")
                .WithFlag(4, mode: FieldMode.Read | FieldMode.WriteToClear, name: "TIMEOUT_MEM")
                .WithFlag(5, mode: FieldMode.Read | FieldMode.WriteToClear, name: "THRESH_HIT")
                .WithFlag(6, mode: FieldMode.Read | FieldMode.WriteToClear, name: "INVALID_APB")
                .WithFlag(7, mode: FieldMode.Read | FieldMode.WriteToClear, name: "FIFO_OVERFLOW")
                .WithReservedBits(8, 24);

            DMARegisters.DmaDestinationInterruptEnable.Define(registers, name: "QSPIDMA_DST_I_EN")
                .WithFlag(1, out dmaDoneInterruptEnable, mode: FieldMode.Read | FieldMode.Set, name: "DONE")
                .WithFlag(2, mode: FieldMode.WriteToClear, name: "AXI_BRESP_ERR")
                .WithFlag(3, mode: FieldMode.WriteToClear, name: "TIMEOUT_STRM")
                .WithFlag(4, mode: FieldMode.WriteToClear, name: "TIMEOUT_MEM")
                .WithFlag(5, mode: FieldMode.WriteToClear, name: "THRESH_HIT")
                .WithFlag(6, mode: FieldMode.WriteToClear, name: "INVALID_APB")
                .WithFlag(7, mode: FieldMode.WriteToClear, name: "FIFO_OVERFLOW")
                .WithReservedBits(8, 24);

            DMARegisters.DmaDestinationInterruptDisable.Define(registers, name: "QSPIDMA_DST_I_DIS")
                .WithFlag(1, writeCallback: (_, value) => dmaDoneInterruptEnable.Value &= !value, mode: FieldMode.Write, name: "DONE")
                .WithFlag(2, mode: FieldMode.WriteToClear, name: "AXI_BRESP_ERR")
                .WithFlag(3, mode: FieldMode.WriteToClear, name: "TIMEOUT_STRM")
                .WithFlag(4, mode: FieldMode.WriteToClear, name: "TIMEOUT_MEM")
                .WithFlag(5, mode: FieldMode.WriteToClear, name: "THRESH_HIT")
                .WithFlag(6, mode: FieldMode.WriteToClear, name: "INVALID_APB")
                .WithFlag(7, mode: FieldMode.WriteToClear, name: "FIFO_OVERFLOW")
                .WithReservedBits(8, 24);

            DMARegisters.DmaDestinationInterruptMask.Define(registers, name: "QSPIDMA_DST_I_MASK")
                .WithFlag(1, valueProviderCallback: _ => !dmaDoneInterruptEnable.Value, mode: FieldMode.Read, name: "DONE")
                .WithFlag(2, mode: FieldMode.Read, name: "AXI_BRESP_ERR")
                .WithFlag(3, mode: FieldMode.Read, name: "TIMEOUT_STRM")
                .WithFlag(4, mode: FieldMode.Read, name: "TIMEOUT_MEM")
                .WithFlag(5, mode: FieldMode.Read, name: "THRESH_HIT")
                .WithFlag(6, mode: FieldMode.Read, name: "INVALID_APB")
                .WithFlag(7, mode: FieldMode.Read, name: "FIFO_OVERFLOW")
                .WithReservedBits(8, 24);

            DMARegisters.DmaDestinationControl2.Define(registers, 0xFFF8, name: "QSPIDMA_DST_CTRL2")
                .WithValueField(0, 4, name: "MAX_OUTS_CMDS")
                .WithValueField(4, 12, name: "TIMEOUT_PRE")
                .WithReservedBits(16, 6)
                .WithFlag(22, name: "TIMEOUT_EN")
                .WithReservedBits(23, 1)
                .WithValueField(24, 3, name: "AWCACHE")
                .WithReservedBits(27, 5);

            DMARegisters.DmaDestinationAddressMsb.Define(registers, name: "QSPIDMA_DST_ADDR_MSB")
                .WithValueField(0, 12, out dmaAddressMsb, mode: FieldMode.Write)
                .WithValueField(12, 20);
        }

        private void EnqueueGenericFifo()
        {
            GenericFifoCommand entry = new GenericFifoCommand
            {
                immediate = (byte)gqspiCommandImmediate.Value,
                dataXref = gqspiCommandDataXref.Value,
                exponent = gqspiCommandExponent.Value,
                spiMode = gqspiCommandSpiMode.Value,
                csLower = gqspiCommandCsLower.Value,
                csUpper = gqspiCommandCsUpper.Value,
                dataBusLower = gqspiCommandDataBusLower.Value,
                dataBusUpper = gqspiCommandDataBusUpper.Value,
                tx = gqspiCommandTransmit.Value,
                rx = gqspiCommandReceive.Value,
                stripe = gqspiCommandStripe.Value,
            };

            genericFifo.Enqueue(entry);

            if(!genFifoManualStart.Value)
            {
                ProcessGenericFifo();
            }
        }

        private void ProcessGenericFifo()
        {
            if(!gqspiEnabled.Value)
            {
                this.Log(LogLevel.Noisy, "GQSPI command not executed: Generic QSPI is not enabled");
                return;
            }
            if(!gqspiSelected.Value)
            {
                this.Log(LogLevel.Noisy, "GQSPI command not executed: Generic QSPI is not selected");
                return;
            }

            while(genericFifo.TryDequeue(out var cmd))
            {
                if(!ExecuteGenericFifoCommand(cmd))
                {
                    break;
                }
            }
        }

        // Returns false when execution should stop and true when it can continue
        private bool ExecuteGenericFifoCommand(GenericFifoCommand cmd)
        {
            if(!(cmd.tx || cmd.rx))
            {
                if(cmd.immediate == 0)
                {
                    return true;
                }

                if(cmd.dataXref)
                {
                    if(cmd.csLower)
                    {
                        this.Log(LogLevel.Noisy, "GQSPI command: Dummy cycles: {0}", cmd.immediate);
                    }
                }
                else
                {
                    if(!cmd.csLower)
                    {
                        this.Log(LogLevel.Noisy, "GQSPI command: CS Deassert cycles: {0}", cmd.immediate);
                        if(cmd.immediate > 0)
                        {
                            // Finish transmission on chip select line deassert
                            RegisteredPeripheral?.FinishTransmission();
                        }
                    }
                    else
                    {
                        this.Log(LogLevel.Noisy, "GQSPI command: CS Assert cycles: {0}", cmd.immediate);
                    }
                }
                return true;
            }
            if(cmd.stripe)
            {
                this.Log(LogLevel.Error, "GQSPI command not executed: Stripe mode not supported");
                return false;
            }
            this.Log(LogLevel.Noisy, "GEN FIFO immediate {0} spimode: {1}", cmd.immediate, cmd.spiMode);
            if(cmd.dataXref)
            {
                uint count = (uint)cmd.immediate;
                if(cmd.exponent)
                {
                    count = (uint)(1 << (int)count);
                }

                this.Log(LogLevel.Noisy, "GQSPI command: Data transfer TX: {0} RX: {1} CNT: {2}", cmd.tx, cmd.rx, count);
                if(cmd.rx)
                {
                    bytesToReceive += count;
                }
                if(cmd.tx)
                {
                    bytesToTransmit += count;
                }
                UpdateTransferQueues();
            }
            else
            {
                this.Log(LogLevel.Noisy, "GQSPI command: Immediate data transfer TX: {0} RX: {1} Data: {2}", cmd.tx, cmd.rx, cmd.immediate);
                if(cmd.tx)
                {
                    RegisteredPeripheral?.Transmit(cmd.immediate);
                }
                if(cmd.rx)
                {
                    receiveFifo.Enqueue(RegisteredPeripheral?.Transmit(0) ?? 0);
                }
            }

            return true;
        }

        private void UpdateTransferQueues()
        {
            TryTransmitData();
            TryReceiveData();
        }

        private void TryTransmitData()
        {
            if(bytesToTransmit == 0)
            {
                return;
            }

            this.Log(LogLevel.Noisy, "TX transfer size: 0x{0:X}", bytesToTransmit);

            var words = RoundUpDiv4(bytesToTransmit);
            if(transmitFifo.Count < words)
            {
                this.Log(LogLevel.Noisy, "Not enough data in TX FIFO, current word count: {0}", transmitFifo.Count);
                return;
            }

            for(var i = 0; i < words; i++)
            {
                var word = transmitFifo.Dequeue();
                var bytes = Math.Min(bytesToTransmit, 4);
                for(var j = 0; j < bytes; j++)
                {
                    if(gqspiBigEndian.Value)
                    {
                        RegisteredPeripheral?.Transmit((byte)(word >> (8 * (3 - j))));
                    }
                    else
                    {
                        RegisteredPeripheral?.Transmit((byte)(word >> (8 * j)));
                    }
                    bytesToTransmit -= 1;
                }
            }
        }

        private void TryReceiveData()
        {
            if(bytesToReceive == 0)
            {
                return;
            }

            this.Log(LogLevel.Noisy, "RX transfer size: 0x{0:X}", bytesToReceive);
            if(gqspiFlashMemoryMode.Value == FlashMemoryMode.DMA)
            {
                IssueDMATransfer();
                return;
            }

            var words = RoundUpDiv4(bytesToReceive);
            for(var i = 0; i < words; i++)
            {
                uint word = 0;
                var bytes = Math.Min(bytesToReceive, 4);
                for(var j = 0; j < bytes; j++)
                {
                    var receivedByte = RegisteredPeripheral?.Transmit(0) ?? 0;
                    if(gqspiBigEndian.Value)
                    {
                        word |= (uint)(receivedByte << (8 * (3 - j)));
                    }
                    else
                    {
                        word |= (uint)(receivedByte << (8 * j));
                    }
                    bytesToReceive -= 1;
                }
                receiveFifo.Enqueue(word);
            }
        }

        private void IssueDMATransfer()
        {
            var dmaAddressFull = dmaAddress.Value | (dmaAddressMsb.Value << 32);
            var words = RoundUpDiv4(bytesToReceive);

            this.Log(LogLevel.Noisy, "DMA transfer, size: 0x{0:X} addr: 0x{1:X} written: 0x{2:X}", bytesToReceive, dmaAddressFull, dmaWrittenWords);
            for(ulong i = 0; i < words; i++)
            {
                uint word = 0;
                for(var j = 0; j < 4; j++)
                {
                    var receivedByte = 0;
                    if(bytesToReceive > 0)
                    {
                        receivedByte = RegisteredPeripheral?.Transmit(0) ?? 0;
                        bytesToReceive -= 1;
                    }

                    if(dmaReversedByteOrdering.Value)
                    {
                        word |= (uint)(receivedByte << (8 * (3 - j)));
                    }
                    else
                    {
                        word |= (uint)(receivedByte << (8 * j));
                    }
                }
                dmaSize.Value -= 1;

                sysbus.WriteDoubleWord(dmaAddressFull + dmaWrittenWords * 4, word);
                dmaWrittenWords += 1;
            }

            if(dmaSize.Value == 0)
            {
                this.Log(LogLevel.Noisy, "DMA transfer complete");
                dmaWritesDone.Value = Math.Min(dmaWritesDone.Value + 1, 0b111);
                dmaDoneInterruptStatus.Value = true;
                dmaWrittenWords = 0;
            }
            else
            {
                this.Log(LogLevel.Noisy, "DMA transfer not complete, remaining words: 0x{0:X}", dmaSize.Value);
            }
        }

        private uint RoundUpDiv4(uint value)
        {
            return (value + 3) >> 2;
        }

        private bool RxFifoEmpty => receiveFifo.Count == 0;
        private bool GenericFifoFull => genericFifo.Count == FifoDepth;
        private bool GenericFifoNotFull => genericFifo.Count >= (int)genericFifoThreshold.Value;
        private bool TxFifoEmpty => transmitFifo.Count == 0;
        private bool GenericFifoEmpty => genericFifo.Count == 0;
        private bool RxFifoFull => receiveFifo.Count == FifoDepth;
        private bool RxFifoNotEmpty => receiveFifo.Count >= (int)rxThreshold.Value;
        private bool TxFifoFull => transmitFifo.Count == FifoDepth;
        private bool TxFifoNotFull => transmitFifo.Count < (int)txThreshold.Value;
        private bool PollTimeExpire => false;

        private IEnumRegisterField<FlashMemoryMode> gqspiFlashMemoryMode;

        private IValueRegisterField dmaAddress;
        private IValueRegisterField dmaAddressMsb;
        private IValueRegisterField dmaSize;
        private IValueRegisterField dmaWritesDone;
        private IValueRegisterField dmaFifoThreshold;
        private IFlagRegisterField dmaReversedByteOrdering;

        private IFlagRegisterField gqspiEnabled;
        private IFlagRegisterField gqspiSelected;
        private IFlagRegisterField genFifoManualStart;
        private IFlagRegisterField gqspiBigEndian;

        private IValueRegisterField gqspiCommandImmediate;
        private IFlagRegisterField gqspiCommandDataXref;
        private IFlagRegisterField gqspiCommandExponent;
        private IEnumRegisterField<SpiMode> gqspiCommandSpiMode;
        private IFlagRegisterField gqspiCommandCsLower;
        private IFlagRegisterField gqspiCommandCsUpper;
        private IFlagRegisterField gqspiCommandDataBusUpper;
        private IFlagRegisterField gqspiCommandDataBusLower;
        private IFlagRegisterField gqspiCommandTransmit;
        private IFlagRegisterField gqspiCommandReceive;
        private IFlagRegisterField gqspiCommandStripe;
        private IFlagRegisterField gqspiCommandPoll;

        private IFlagRegisterField rxFifoFullInterruptEnabled;
        private IFlagRegisterField rxFifoFullInterruptStatus;
        private IFlagRegisterField txFifoFullInterruptEnabled;
        private IFlagRegisterField txFifoFullInterruptStatus;
        private IFlagRegisterField genericFifoFullInterruptEnabled;
        private IFlagRegisterField genericFifoFullInterruptStatus;
        private IFlagRegisterField rxFifoNotEmptyInterruptEnabled;
        private IFlagRegisterField rxFifoNotEmptyInterruptStatus;
        private IFlagRegisterField txFifoNotFullInterruptEnabled;
        private IFlagRegisterField txFifoNotFullInterruptStatus;
        private IFlagRegisterField genericFifoNotFullInterruptEnabled;
        private IFlagRegisterField genericFifoNotFullInterruptStatus;
        private IFlagRegisterField rxFifoEmptyInterruptEnabled;
        private IFlagRegisterField rxFifoEmptyInterruptStatus;
        private IFlagRegisterField txFifoEmptyInterruptEnabled;
        private IFlagRegisterField txFifoEmptyInterruptStatus;
        private IFlagRegisterField genericFifoEmptyInterruptEnabled;
        private IFlagRegisterField genericFifoEmptyInterruptStatus;
        private IFlagRegisterField pollTimeExpireInterruptEnabled;
        private IFlagRegisterField pollTimeExpireInterruptStatus;

        private IFlagRegisterField dmaDoneInterruptStatus;
        private IFlagRegisterField dmaDoneInterruptEnable;

        private IValueRegisterField txThreshold;
        private IValueRegisterField rxThreshold;
        private IValueRegisterField genericFifoThreshold;

        private uint bytesToTransmit;
        private uint bytesToReceive;
        private ulong dmaWrittenWords;

        private readonly IBusController sysbus;

        private readonly Queue<uint> receiveFifo = new Queue<uint>();
        private readonly Queue<uint> transmitFifo = new Queue<uint>();
        private readonly Queue<GenericFifoCommand> genericFifo = new Queue<GenericFifoCommand>();

        private readonly DoubleWordRegisterCollection registers;

        private const uint FifoDepth = 63;
        private const uint ModuleId = 0x10A0000;

        private struct GenericFifoCommand
        {
            public byte immediate;
            public bool dataXref;
            public bool exponent;
            public SpiMode spiMode;
            public bool csLower;
            public bool csUpper;
            public bool dataBusLower;
            public bool dataBusUpper;
            public bool tx;
            public bool rx;
            public bool stripe;
            public bool poll;
        }

        private enum SpiMode
        {
            Reserved = 0b00,
            SPI = 0b01,
            DualSPI = 0b10,
            QuadSPI = 0b11,
        }

        private enum FlashMemoryMode
        {
            IO = 0b0,
            DMA = 0b10,
        }

        private enum BaseRegisters
        {
            Config = 0x0,
            InterruptStatus = 0x4,
            InterruptEnable = 0x8,
            InterruptDisable = 0xC,
            InterruptUnmask = 0x10,
            Enable = 0x14,
            Delay = 0x18,
            TxData0 = 0x1C,
            RxData = 0x20,
            SlaveIdleCount = 0x24,
            TxThreshold = 0x28,
            RxThreshold = 0x2C,
            GPIO = 0x30,
            LoopbackClockDelay = 0x38,
            TxData1 = 0x80,
            TxData2 = 0x84,
            TxData3 = 0x88,
            LinearConfig = 0xA0,
            LinearStatus = 0xA4,
            Command = 0xC0,
            TransferSize = 0xC4,
            DummyCycleEnable = 0xC8,
            ModuleId = 0xFC,
        }

        private enum GenericQSPIRegisters
        {
            Config = 0x100,
            InterruptStatus = 0x104,
            InterruptEnable = 0x108,
            InterruptDisable = 0x10C,
            InterruptMask = 0x110,
            Enable = 0x114,
            TxData = 0x11C,
            RxData = 0x120,
            TxThreshold = 0x128,
            RxThreshold = 0x12C,
            GPIO = 0x130,
            LoopbackClockDelay = 0x138,
            GenericFifo = 0x140,
            Select = 0x144,
            FifoControl = 0x14C,
            GenericFifoThreshold = 0x150,
            PollConfig = 0x154,
            PollTimeout = 0x158,
            TransferStatus = 0x15C,
            FifoSnapshot = 0x160,
            RxCopy = 0x164,
            DataDelay = 0x1F8,
            ModuleId = 0x1FC,
        }

        private enum DMARegisters
        {
            DmaDestinationAddress = 0x800,
            DmaDestinationSize = 0x804,
            DmaDestinationStatus = 0x808,
            DmaDestinationControl = 0x80C,
            DmaDestinationInterruptStatus = 0x814,
            DmaDestinationInterruptEnable = 0x818,
            DmaDestinationInterruptDisable = 0x81C,
            DmaDestinationInterruptMask = 0x820,
            DmaDestinationControl2 = 0x824,
            DmaDestinationAddressMsb = 0x828,
        }
    }
}
