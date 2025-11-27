//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class AndesATCSPI200 : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public AndesATCSPI200(IMachine machine) : base(machine)
        {
            this.machine = machine;
            sysbus = machine.GetSystemBus(this);

            IRQ = new GPIO();
            rxBuffer = new Queue<byte>();
            txBuffer = new Queue<byte>();
            registers = new DoubleWordRegisterCollection(this, DefineRegisters());

            Reset();
        }

        public override void Reset()
        {
            txTransferCounter = 0;
            rxTransferCounter = 0;
            registers.Reset();
            IRQ.Unset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x100;

        public GPIO IRQ { get; private set; }

        private Dictionary<long, DoubleWordRegister> DefineRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>()
            {
                // RevMinor and RevMajor are revision dependent
                {(long)Registers.IDAndRevision, new DoubleWordRegister(this)
                    .WithTag("RevMinor (Minor Revision Number)", 0, 4)
                    .WithTag("RevMajor (Major Revision Number)", 4, 4)
                    .WithValueField(8, 24, FieldMode.Read, valueProviderCallback: _ => IDNumber, name: "ID (ID number)")
                },
                {(long)Registers.TransferFormat, new DoubleWordRegister(this)
                    .WithTaggedFlag("CPHA (Clock Phase)", 0)
                    .WithTaggedFlag("CPOL (Clock Polarity)", 1)
                    .WithFlag(2, out isSlave, name: "SlvMode (Master/Slave mode selection)")
                    .WithTaggedFlag("LSB (Transfer data with LSB first)", 3)
                    .WithTaggedFlag("MOSIBiDir (Bi-directional MOSI in regular mode)", 4)
                    .WithReservedBits(5, 2)
                    .WithTaggedFlag("DataMerge (Enable Data Merge mode)", 7)
                    .WithTag("DataLen (Length of each data unit in bits)", 8, 5)
                    .WithReservedBits(13, 3)
                    .WithTag("AddrLen (Address length in bytes)", 16, 2)
                    .WithReservedBits(18, 14)
                    .WithWriteCallback((_, __) =>
                    {
                        if(RegisteredPeripheral != null)
                        {
                            RegisteredPeripheral.FinishTransmission();
                        }
                    })
                },
                // Reset value of bits [5:0] depends on pin values
                {(long)Registers.DirectIO, new DoubleWordRegister(this, 0x3100)
                    .WithTaggedFlag("CS_I (Status of the SPI CS (chip select) signal)", 0)
                    .WithTaggedFlag("SCLK_I (Status of the SPI SCLK signal)", 1)
                    .WithTaggedFlag("MOSI_I (Status of the SPI MOSI signal)", 2)
                    .WithTaggedFlag("MISO_I (Status of the SPI MISO signal)", 3)
                    .WithTaggedFlag("WP_I (Status of the SPI Flash write protect signal)", 4)
                    .WithTaggedFlag("HOLD_I (Status of the SPI Flash hold signal)", 5)
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("CS_O (Output value for the SPI CS (chip select) signal)", 8)
                    .WithTaggedFlag("SCLK_O (Output value for the SPI SCLK signal)", 9)
                    .WithTaggedFlag("MOSI_O (Output value for the SPI MOSI signal)", 10)
                    .WithTaggedFlag("MISO_O (Output value for the SPI MISO signal)", 11)
                    .WithTaggedFlag("WP_O (Output value for the SPI Flash write protect signal)", 12)
                    .WithTaggedFlag("HOLD_O (Output value for the SPI Flash hold signal)", 13)
                    .WithReservedBits(14, 2)
                    .WithTaggedFlag("CS_OE (Output enable for SPI CS (chip select) signal)", 16)
                    .WithTaggedFlag("SCLK_OE (Output enable for the SPI SCLK signal)", 17)
                    .WithTaggedFlag("MOSI_OE (Output enable for the SPI MOSI signal)", 18)
                    .WithTaggedFlag("MISO_OE (Output enable fo the SPI MISO signal)", 19)
                    .WithTaggedFlag("WP_OE (Output enable for the SPI Flash write protect signal)", 20)
                    .WithTaggedFlag("HOLD_OE (Output enable for the SPI Flash hold signal)", 21)
                    .WithReservedBits(22, 2)
                    .WithTaggedFlag("DirectIOEn (Enable Direct IO)", 24)
                    .WithReservedBits(25, 7)
                },
                {(long)Registers.WriteTransferCount, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txTransferDataCount,
                        writeCallback: (_, val) => txTransferCounter = 0,
                        name: "Transfer count for write transaction (SPI data)")
                },
                {(long)Registers.ReadTransferCount, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxTransferDataCount,
                        writeCallback: (_, val) => rxTransferCounter = 0,
                        name: "Transfer count for read transaction (SPI data)")
                },
                {(long)Registers.TransferControl, new DoubleWordRegister(this)
                    .WithTag("RdTranCnt (Transfer count for read data)", 0, 8)
                    .WithTag("DummyCnt (Dummy data count)", 9, 2)
                    .WithTaggedFlag("TokenValue (Token value (M-Mode only))", 11)
                    .WithTag("WrTranCnt (Transfer count write data)", 12, 9)
                    .WithTaggedFlag("TokenEn (Token transfer enable)", 21)
                    .WithTag("DualQuad (SPI data phase format)", 22, 2)
                    .WithEnumField(24, 4, out transferMode, name: "TransMode (Transfer mode)")
                    .WithTaggedFlag("AddrFmt (SPI address phase format (M-Mode only)", 28)
                    .WithFlag(29, out isAddressPhaseEnabled, name: "AddrEn (SPI address phase enable (M-Mode only))")
                    .WithFlag(30, out isCommandPhaseEnabled, name: "CmdEn (SPI command phase enable (M-Mode only))")
                    .WithTaggedFlag("SlvDataOnly (Data-only slave)", 31)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out command, name: "CMD (SPI command)")
                    .WithReservedBits(8, 24)
                    .WithWriteCallback((_, data) => TryExecuteTransaction((byte)data))
                },
                {(long)Registers.Address, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out spiAddress, name: "ADDR (SPI Address)",
                        writeCallback: (_, val) =>
                        {
                            if(isSlave.Value)
                            {
                                this.Log(LogLevel.Warning, "Address mode is available only for Master mode.");
                                return;
                            }
                            if(!isAddressPhaseEnabled.Value)
                            {
                                this.Log(LogLevel.Warning, "Address mode is not enabled, ignoring write.");
                                return;
                            }
                            spiAddress.Value = val;
                        })
                },
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "DATA (SPI data)",
                        writeCallback: (_, val) => TransmitByte((byte)val),
                        valueProviderCallback: _ => ReceiveByte())
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) Reset(); }, name: "SPIRST (SPI reset)")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) rxBuffer.Clear(); }, name: "RXFIFORST (Receive FIFO reset)")
                    .WithFlag(2, FieldMode.Write, writeCallback: (_, val) => { if(val) txBuffer.Clear(); }, name: "TXFIFORST (Transmit FIFO reset)")
                    .WithFlag(3, out rxDmaEnable, name: "RXDMAEN (RX DMA enable)")
                    .WithFlag(4, out txDmaEnable, name: "TXDMAEN (Tx DMA enable)")
                    .WithReservedBits(5, 2)
                    .WithValueField(8, 8, out rxBufferThreshold, name: "RXTHRES (RX FIFO Threshold)")
                    .WithValueField(16, 8, out txBufferThreshold, name: "TXTHRES (TX FIFO Threshold)")
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithTaggedFlag("SPIActive (SPI register programming is in progress)", 0)
                    .WithReservedBits(1, 7)
                    .WithValueField(8, 5, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            if(rxBuffer.Count <= MaxTransactionBatchSize - 1)
                            {
                                return (ulong)(rxBuffer.Count);
                            }
                            return MaxTransactionBatchSize - 1;
                        },
                        name: "RXNUM[5:0] (Number of valid entries in the Receive FIFO)")
                    .WithReservedBits(13, 1)
                    .WithFlag(14, FieldMode.Read,
                        valueProviderCallback: _ => rxBuffer.Count == 0,
                        name: "RXEMPTY (Receive FIFO Empty flag)")
                    .WithFlag(15, FieldMode.Read,
                        valueProviderCallback: _ => rxBuffer.Count == MaxRxBufferSize,
                        name: "RXFULL (Receive FIFO Full flag)")
                    .WithValueField(16, 5, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            if(txBuffer.Count <= MaxTransactionBatchSize - 1)
                            {
                                return (ulong)(txBuffer.Count);
                            }
                            return MaxTransactionBatchSize - 1;
                        },
                        name: "TXNUM[5:0] (Number of valid entries in the Transmit FIFO)")
                    .WithReservedBits(21, 1)
                    .WithFlag(22, FieldMode.Read,
                        valueProviderCallback: _ => txBuffer.Count == 0,
                        name: "TXEMPTY (Transmit FIFO Empty flag)")
                    .WithFlag(23, FieldMode.Read,
                        valueProviderCallback: _ => txBuffer.Count == MaxTxBufferSize,
                        name: "TXFULL (Transmit FIFO Full flag)")
                    .WithTag("RXNUM[7:6] (Number of valid entries in the Receive FIFO)", 24, 2)
                    .WithReservedBits(26, 2)
                    .WithTag("TXNUM[7:6] (Number of valid entries in the Transmit FIFO)", 28, 2)
                    .WithReservedBits(30, 2)
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithTaggedFlag("RXFIFOORIntEn (Enable the SPI Receive FIFO Overrun interrupt)", 0)
                    .WithTaggedFlag("TXFIFOURIntEn (Enable the SPI Transmit FIFO Underrun interrupt)", 1)
                    .WithFlag(2, out rxThresholdInterruptEnabled, name: "RXFIFOIntEn (Enable the SPI Receive FIFO Threshold interrupt)")
                    .WithFlag(3, out txThresholdInterruptEnabled, name: "TXFIFOIntEn (Enable the SPI Transmit FIFO Threshold interrupt)")
                    .WithFlag(4, out transactionEndInterruptEnabled, name: "EndIntEn (Enable End of SPI Transfer interrupt)")
                    .WithTaggedFlag("SlvCmdEn (Enable the Slave Command Interrupt)", 5)
                    .WithReservedBits(6, 26)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("RXFIFOORInt (RX FIFO Overrun interrupt)", 0)
                    .WithTaggedFlag("TXFIFOURInt (TX FIFO Underrun interrupt)", 1)
                    .WithFlag(2, out rxThresholdInterrupt, FieldMode.Read | FieldMode.WriteOneToClear,
                        name: "RXFIFOInt (RX FIFO Threshold interrupt)")
                    .WithFlag(3, out txThresholdInterrupt, FieldMode.Read | FieldMode.WriteOneToClear,
                        name: "TXFIFOInt (TX FIFO Threshold interrupt)")
                    .WithFlag(4, out transactionEndInterrupt, FieldMode.Read | FieldMode.WriteOneToClear,
                        name: "EndInt (End of SPI Transfer interrupt)")
                    .WithTaggedFlag("SlvCmd (Slave Command Interrupt)", 5)
                    .WithReservedBits(6, 26)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.TimingRegister, new DoubleWordRegister(this)
                    .WithTag("SCLK_DIV (CLK frequency ratio between clock source and interface SCLK)", 0, 8)
                    .WithTag("CHST (Minimum time that SPI CS should stay high)", 8, 4)
                    .WithTag("CS2SCLK (Minimum time between edges of SPI CS and SCLK)", 12, 2)
                    .WithReservedBits(14, 18)
                },
                {(long)Registers.MemoryAccessControl, new DoubleWordRegister(this)
                    .WithTag("MemRdCmd (Selects the SPI command for serving the memory-mapped reads on the AHB/EILM bus)", 0, 4)
                    .WithReservedBits(4, 4)
                    .WithTaggedFlag("MemCtrlChg (Detect change on 0x40 ir 0x50 registers)", 8)
                    .WithReservedBits(9, 23)
                },
                // Configuration dependent register
                {(long)Registers.Configuration, new DoubleWordRegister(this)
                    .WithValueField(0, 4, FieldMode.Read,
                        valueProviderCallback: _ => (ulong)RxBufferDepth,
                        name: "RxFIFOSize (Depth of RX FIFO)")
                    .WithValueField(4, 4, FieldMode.Read,
                        valueProviderCallback: _ => (ulong)TxBufferDepth,
                        name: "TxFIFOSize (Depth of TX FIFO)")
                    .WithTaggedFlag("DualSPI (Support for Dual I/O SPI)", 8)
                    .WithTaggedFlag("QuadSPI (Support for Quad I/O SPI)", 9)
                    .WithReservedBits(10, 1)
                    .WithTaggedFlag("DirectIO (Support for Direct SPI IO)", 11)
                    .WithTaggedFlag("AHBMem (Support for memory-mapped access through AHB bus)", 12)
                    .WithTaggedFlag("EILMMem (Support for memory-mapped access through EILM bus)", 13)
                    .WithTaggedFlag("Slave (Support for SPI Slave mode)", 14)
                    .WithReservedBits(15, 17)
                },
            };
            return registersMap;
        }

        private void UpdateInterrupts()
        {
            var irq = false;
            irq |= rxThresholdInterruptEnabled.Value & rxThresholdInterrupt.Value;
            irq |= txThresholdInterruptEnabled.Value & txThresholdInterrupt.Value;
            irq |= transactionEndInterruptEnabled.Value & transactionEndInterrupt.Value;
            IRQ.Set(irq);
        }

        private bool TryVerifyTransferMode()
        {
            switch(transferMode.Value)
            {
            case TransferMode.WriteReadAtTheSameTime:
                if(txTransferDataCount.Value != rxTransferDataCount.Value)
                {
                    this.Log(LogLevel.Warning, "For mode 0 transfer counters for Read and Write should be equal. Aborting.");
                    return false;
                }
                return true;
            case TransferMode.WriteOnly:
            case TransferMode.ReadOnly:
            case TransferMode.NoneData:
                return true;
            default:
                this.Log(LogLevel.Warning, "Encountered unexpected mode ({0}), aborting.", transferMode.Value);
                return false;
            }
        }

        private void TryExecuteTransaction(byte data)
        {
            if(RegisteredPeripheral == null)
            {
                this.Log(LogLevel.Warning, "Trying to issue a transaction to a slave peripheral, but nothing is connected");
                return;
            }
            if(isSlave.Value)
            {
                this.Log(LogLevel.Warning, "Command mode is available only for Master mode.");
                return;
            }
            if(!TryVerifyTransferMode())
            {
                return;
            }
            this.Log(LogLevel.Noisy, "Transfer mode: {0}", transferMode.Value);

            if(txDmaEnable.Value || rxDmaEnable.Value)
            {
                HandleTransactionDMA(data);
            }
            else
            {
                HandleTransactionSPI(data);
            }
            UpdateInterrupts();
        }

        private void HandleTransactionDMA(byte data)
        {
            if(txDmaEnable.Value)
            {
                TransmitDataDMA(data);
            }
            if(rxDmaEnable.Value)
            {
                ReceiveDataDMA(data);
            }
        }

        private void HandleTransactionSPI(byte data)
        {
            // Transmit command byte to the slave
            if(isCommandPhaseEnabled.Value)
            {
                TransmitByte(data);
            }
            else
            {
                this.Log(LogLevel.Warning, "Command mode is not enabled, writing dummy byte.");
                RegisteredPeripheral.Transmit(data);
                txThresholdInterrupt.Value = (ulong)txBuffer.Count <= txBufferThreshold.Value;
            }
        }

        private void TransmitDataDMA(byte data)
        {
            // Transmit command byte to the slave
            RegisteredPeripheral.Transmit(data);
            foreach(var b in sysbus.ReadBytes(spiAddress.Value, (int)txTransferDataCount.Value))
            {
                RegisteredPeripheral.Transmit(b);
            }
            transactionEndInterrupt.Value = IsWriteOnlyTransaction && IsTxCountTransferred;
        }

        private void ReceiveDataDMA(byte data)
        {
            // Transmit command byte to the slave
            RegisteredPeripheral.Transmit(data);
            for(var i = 0; i < (int)rxTransferDataCount.Value; i++)
            {
                rxBuffer.Enqueue(RegisteredPeripheral.Transmit(0));
            }
            sysbus.WriteBytes(rxBuffer.ToArray(), spiAddress.Value);
            transactionEndInterrupt.Value = (IsReadOnlyTransaction && IsRxCountTransferred) || (rxBuffer.Count == 0);
        }

        private void TransmitByte(byte data)
        {
            if((uint)txBuffer.Count == MaxTxBufferSize)
            {
                this.Log(LogLevel.Warning, "Trying to write to a full Tx FIFO, data not queued.");
                return;
            }
            this.Log(LogLevel.Noisy, "Transmitting byte 0x{0:X}", data);
            var received = RegisteredPeripheral.Transmit(data);
            if(IsReadOnlyTransaction || IsReadWriteTransaction)
            {
                rxBuffer.Enqueue(received);
            }
            txThresholdInterrupt.Value = (ulong)txBuffer.Count <= txBufferThreshold.Value;
            rxThresholdInterrupt.Value = IsReadWriteTransaction && ((ulong)rxBuffer.Count >= rxBufferThreshold.Value);
            transactionEndInterrupt.Value = IsWriteOnlyTransaction && IsTxCountTransferred;
            txTransferCounter += 1;
        }

        private byte ReceiveByte()
        {
            if(rxBuffer.Count == 0)
            {
                this.Log(LogLevel.Error, "RX Buffer is empty!");
                return 0;
            }
            var result = rxBuffer.Dequeue();
            rxThresholdInterrupt.Value = (ulong)rxBuffer.Count >= rxBufferThreshold.Value;
            transactionEndInterrupt.Value = (IsReadOnlyTransaction && IsRxCountTransferred) || (rxBuffer.Count == 0);
            rxTransferCounter += 1;
            return result;
        }

        private bool IsReadOnlyTransaction => (transferMode.Value == TransferMode.ReadOnly);

        private bool IsWriteOnlyTransaction => (transferMode.Value == TransferMode.WriteOnly);

        private bool IsReadWriteTransaction => (transferMode.Value == TransferMode.WriteReadAtTheSameTime);

        private bool IsTxCountTransferred => (txTransferCounter == txTransferDataCount.Value + 1);

        private bool IsRxCountTransferred => (rxTransferCounter == rxTransferDataCount.Value + 1);

        private uint MaxRxBufferSize => (uint)1 << ((short)RxBufferDepth + 1);

        private uint MaxTxBufferSize => (uint)1 << ((short)TxBufferDepth + 1);

        private IEnumRegisterField<TransferMode> transferMode;
        private IValueRegisterField command;
        private IValueRegisterField spiAddress;
        private IValueRegisterField rxBufferThreshold;
        private IValueRegisterField txBufferThreshold;
        private IValueRegisterField rxTransferDataCount;
        private IValueRegisterField txTransferDataCount;
        private IFlagRegisterField isSlave;
        private IFlagRegisterField rxDmaEnable;
        private IFlagRegisterField txDmaEnable;
        private IFlagRegisterField isAddressPhaseEnabled;
        private IFlagRegisterField isCommandPhaseEnabled;
        private IFlagRegisterField txThresholdInterrupt;
        private IFlagRegisterField txThresholdInterruptEnabled;
        private IFlagRegisterField rxThresholdInterrupt;
        private IFlagRegisterField rxThresholdInterruptEnabled;
        private IFlagRegisterField transactionEndInterrupt;
        private IFlagRegisterField transactionEndInterruptEnabled;
        private ulong txTransferCounter;
        private ulong rxTransferCounter;

        private readonly DoubleWordRegisterCollection registers;
        private readonly IBusController sysbus;
        private readonly IMachine machine;
        private readonly Queue<byte> rxBuffer;
        private readonly Queue<byte> txBuffer;

        private const uint MaxTransactionBatchSize = 32;
        private const BufferDepth RxBufferDepth = BufferDepth.Words_32;
        private const BufferDepth TxBufferDepth = BufferDepth.Words_32;
        private const uint IDNumber = 0x20020;

        private enum TransferMode
        {
            WriteReadAtTheSameTime = 0x0,
            WriteOnly = 0x1,
            ReadOnly = 0x2,
            WriteThenRead = 0x3,
            ReadThenWrite = 0x4,
            WriteDummyRead = 0x5,
            ReadDummyWrite = 0x6,
            NoneData = 0x7,
            DummyWrite = 0x8,
            DummyRead = 0x9
        }

        private enum BufferDepth
        {
            Words_2 = 0,
            Words_4 = 1,
            Words_8 = 2,
            Words_16 = 3,
            Words_32 = 4,
            Words_64 = 5,
            Words_128 = 6
        }

        private enum Registers
        {
            IDAndRevision = 0,
            // 0x04-0x0c Reserved
            TransferFormat = 0x10,
            DirectIO = 0x14,
            WriteTransferCount = 0x18,
            ReadTransferCount = 0x1c,
            TransferControl = 0x20,
            Command = 0x24,
            Address = 0x28,
            Data = 0x2c,
            Control = 0x30,
            Status = 0x34,
            InterruptEnable = 0x38,
            InterruptStatus = 0x3c,
            TimingRegister = 0x40,
            // 0x44-0x4c Reserved
            MemoryAccessControl = 0x50,
            // 0x54-0x5c Reserved
            SlaveStatus = 0x60,
            SlaveDataCount = 0x64,
            // 0x68-0x78 Reserved
            Configuration = 0x7c
        }
    }
}
