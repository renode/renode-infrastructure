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

namespace Antmicro.Renode.Peripherals.SPI
{
    public class RTS5817_SPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IKnownSize
    {
        public RTS5817_SPI(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            ctrlRegisters = new DoubleWordRegisterCollection(this, BuildCtrlRegisterMap());
            dwRegisters = new DoubleWordRegisterCollection(this, BuildDWRegisterMap());
        }

        public override void Reset()
        {
            ctrlRegisters.Reset();
            dwRegisters.Reset();
            transferActive = false;
            rdAddress = 0;
            wrAddress = 0;
            dataLength = 0;
        }

        [ConnectionRegion("dw")]
        public uint ReadDoubleWordFromDW(long offset)
        {
            return dwRegisters.Read(offset);
        }

        [ConnectionRegion("dw")]
        public void WriteDoubleWordToDW(long offset, uint value)
        {
            dwRegisters.Write(offset, value);
        }

        [ConnectionRegion("dw")]
        public ushort ReadWordFromDW(long offset)
        {
            return (ushort)dwRegisters.Read(offset);
        }

        [ConnectionRegion("dw")]
        public void WriteWordToDW(long offset, ushort value)
        {
            dwRegisters.Write(offset, value);
        }

        [ConnectionRegion("dw")]
        public byte ReadByteFromDW(long offset)
        {
            return (byte)dwRegisters.Read(offset);
        }

        [ConnectionRegion("dw")]
        public void WriteByteToDW(long offset, byte value)
        {
            dwRegisters.Write(offset, value);
        }

        [ConnectionRegion("ctrl")]
        public uint ReadDoubleWordFromControl(long offset)
        {
            return ctrlRegisters.Read(offset);
        }

        [ConnectionRegion("ctrl")]
        public void WriteDoubleWordToControl(long offset, uint value)
        {
            ctrlRegisters.Write(offset, value);
        }

        [ConnectionRegion("ctrl")]
        public ushort ReadWordFromControl(long offset)
        {
            return (ushort)ctrlRegisters.Read(offset);
        }

        [ConnectionRegion("ctrl")]
        public void WriteWordToControl(long offset, ushort value)
        {
            ctrlRegisters.Write(offset, value);
        }

        [ConnectionRegion("ctrl")]
        public byte ReadByteFromControl(long offset)
        {
            return (byte)ctrlRegisters.Read(offset);
        }

        [ConnectionRegion("ctrl")]
        public void WriteByteToControl(long offset, byte value)
        {
            ctrlRegisters.Write(offset, value);
        }

        public long Size => 0x100;

        public GPIO IRQ { get; }

        private Dictionary<long, DoubleWordRegister> BuildCtrlRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)RegistersCtrl.StartControl, new DoubleWordRegister(this)
                    .WithFlag(0, out startTransfer, FieldMode.Write | FieldMode.Read, name: "MST_SSI_START",
                        writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                ExecuteTransfer();
                            }
                        })
                    .WithReservedBits(1, 31)
                },
                {(long)RegistersCtrl.StopControl, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "MST_SSI_STOP")
                    .WithReservedBits(1, 31)
                },
                {(long)RegistersCtrl.RdAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, val) => rdAddress = (uint)val,
                        valueProviderCallback: _ => rdAddress, name: "MST_SSI_RD_ADDR")
                },
                {(long)RegistersCtrl.WrAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, val) => wrAddress = (uint)val,
                        valueProviderCallback: _ => wrAddress, name: "MST_SSI_WR_ADDR")
                },
                {(long)RegistersCtrl.DataLength, new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, val) => dataLength = (uint)val,
                        valueProviderCallback: _ => dataLength, name: "MST_SSI_DATA_LEN")
                },
                {(long)RegistersCtrl.Control, new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "MST_SCK_INTERVAL_EN")
                    .WithValueField(2, 8, name: "MST_SCK_COUNT_MAX")
                    .WithValueField(10, 8, name: "MST_CK_COOLDOWN")
                    .WithFlag(18, name: "MST_FORCE_CS_N")
                    .WithReservedBits(19, 13)
                },
                {(long)RegistersCtrl.IrqEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out doneIntEnable, name: "MST_DONE_INT_ENABLE")
                    .WithFlag(1, name: "MST_SSI_WR_SRAM_OVERFLOW_ENABLE")
                    .WithFlag(2, name: "MST_SSI_RD_SRAM_UNDERFLOW_ENABLE")
                    .WithReservedBits(3, 29)
                },
                {(long)RegistersCtrl.IrqStatus, new DoubleWordRegister(this)
                    .WithFlag(0, out interruptStatus, FieldMode.Read | FieldMode.WriteOneToClear,
                        name: "MST_DONE_INT",
                        writeCallback: (_, _) =>
                        {
                            UpdateInterrupt();
                        })
                    .WithFlag(1, FieldMode.Read, name: "MST_SSI_WR_SRAM_OVERFLOW")
                    .WithFlag(2, FieldMode.Read, name: "MST_SSI_RD_SRAM_UNDERFLOW")
                    .WithReservedBits(3, 29)
                },
                {(long)RegistersCtrl.MsSelect, new DoubleWordRegister(this)
                    .WithFlag(0, name: "MST_SSI_MS_SEL")
                    .WithReservedBits(1, 31)
                },
                {(long)RegistersCtrl.DmaState, new DoubleWordRegister(this)
                    .WithValueField(0, 3, FieldMode.Read, name: "MST_RD_SRAM_STATE", valueProviderCallback: _ => 0u)
                    .WithValueField(8, 3, FieldMode.Read, name: "MST_WR_SRAM_STATE", valueProviderCallback: _ => 0u)
                    .WithReservedBits(11, 21)
                },
                {(long)RegistersCtrl.RdFifoThreshold, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "MST_SSI_RD_FIFO_THRESHOLD")
                    .WithReservedBits(8, 24)
                },
            };
        }

        private Dictionary<long, DoubleWordRegister> BuildDWRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)RegistersDW.Ctrlr0, new DoubleWordRegister(this)
                    .WithValueField(0, 4, out dataFrameSize, name: "DFS")
                    .WithValueField(4, 2, name: "FRF")
                    .WithFlag(6, name: "SCPH")
                    .WithFlag(7, name: "SCPOL")
                    .WithValueField(8, 2, out transferMode, name: "TMOD")
                    .WithFlag(10, name: "SLV_OE")
                    .WithFlag(11, name: "SRL")
                    .WithValueField(12, 4, name: "CFS")
                    .WithValueField(16, 5, name: "DFS_32")
                    .WithValueField(21, 2, name: "SPI_FRF")
                    .WithFlag(24, name: "SSTE")
                    .WithFlag(25, name: "SECONV")
                    .WithReservedBits(26, 6)
                },
                {(long)RegistersDW.Ctrlr1, new DoubleWordRegister(this)
                    .WithValueField(0, 16, name: "NDF")
                    .WithReservedBits(16, 16)
                },
                {(long)RegistersDW.SsiEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out ssiEnabled, name: "SSI_EN")
                    .WithReservedBits(1, 31)
                },
                {(long)RegistersDW.MultiWordControl, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "MWCR")
                },
                {(long)RegistersDW.SlaveEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out slaveSelected, name: "SER")
                    .WithReservedBits(1, 31)
                },
                {(long)RegistersDW.BaudRate, new DoubleWordRegister(this)
                    .WithValueField(0, 16, name: "BAUDR")
                    .WithReservedBits(16, 16)
                },
                {(long)RegistersDW.TxFifoThreshold, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "TXFTLR")
                    .WithReservedBits(8, 24)
                },
                {(long)RegistersDW.RxFifoThreshold, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "RXFTLR")
                    .WithReservedBits(8, 24)
                },
                {(long)RegistersDW.TxFifoLevel, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, name: "TXFLR", valueProviderCallback: _ => 0u)
                    .WithReservedBits(8, 24)
                },
                {(long)RegistersDW.RxFifoLevel, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, name: "RXFLR", valueProviderCallback: _ => 0u)
                    .WithReservedBits(8, 24)
                },
                {(long)RegistersDW.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, name: "BUSY", valueProviderCallback: _ => transferActive)
                    .WithFlag(1, FieldMode.Read, name: "TFNF", valueProviderCallback: _ => true)
                    .WithFlag(2, FieldMode.Read, name: "TFE", valueProviderCallback: _ => true)
                    .WithFlag(3, FieldMode.Read, name: "RFNE", valueProviderCallback: _ => false)
                    .WithFlag(4, FieldMode.Read, name: "RFF", valueProviderCallback: _ => false)
                    .WithFlag(5, FieldMode.Read, name: "TX_ERR")
                    .WithReservedBits(6, 26)
                },
                {(long)RegistersDW.InterruptMask, new DoubleWordRegister(this)
                    .WithValueField(0, 6, name: "IMR")
                    .WithReservedBits(6, 26)
                },
                {(long)RegistersDW.InterruptStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 6, FieldMode.Read, name: "ISR", valueProviderCallback: _ => 0u)
                    .WithReservedBits(6, 26)
                },
                {(long)RegistersDW.RawInterruptStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 6, FieldMode.Read, name: "RISR", valueProviderCallback: _ => 0u)
                    .WithReservedBits(6, 26)
                },
                {(long)RegistersDW.TxFifoOverflowClear, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, name: "TXOICR")
                },
                {(long)RegistersDW.RxFifoOverflowClear, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, name: "RXOICR")
                },
                {(long)RegistersDW.RxFifoUnderflowClear, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, name: "RXUICR")
                },
                {(long)RegistersDW.MultiMasterInterruptClear, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, name: "MSTICR")
                },
                {(long)RegistersDW.InterruptClear, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, name: "ICR")
                },
                {(long)RegistersDW.DmaControl, new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "DMACR")
                    .WithReservedBits(2, 30)
                },
                {(long)RegistersDW.DmaTxDataLevel, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "DMATDLR")
                    .WithReservedBits(8, 24)
                },
                {(long)RegistersDW.DmaRxDataLevel, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "DMARDLR")
                    .WithReservedBits(8, 24)
                },
                {(long)RegistersDW.Identification, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "IDR", valueProviderCallback: _ => 0x58170000)
                },
                {(long)RegistersDW.ComponentVersion, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "COMP_VERSION", valueProviderCallback: _ => 0x34302A1)
                },
                {(long)RegistersDW.DataRegister, new DoubleWordRegister(this)
                    .WithValueField(0, 16, name: "DR")
                    .WithReservedBits(16, 16)
                },
                {(long)RegistersDW.RxSampleDelay, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "RX_SAMPLE_DLY")
                    .WithReservedBits(8, 24)
                },
            };
        }

        private void ExecuteTransfer()
        {
            var device = RegisteredPeripheral;
            if(device == null)
            {
                this.Log(LogLevel.Warning, "Cannot start transfer: no SPI device connected");
                SignalTransferComplete();
                return;
            }

            if(!ssiEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Cannot start transfer: SSI is disabled");
                SignalTransferComplete();
                return;
            }

            var len = (int)dataLength;
            if(len == 0)
            {
                this.Log(LogLevel.Debug, "Transfer with zero length");
                SignalTransferComplete();
                return;
            }

            this.Log(LogLevel.Debug, "Starting SPI transfer: len={0}, rd_addr=0x{1:X8}, wr_addr=0x{2:X8}",
                len, rdAddress, wrAddress);

            transferActive = true;

            // Read TX data from system memory at RD_ADDR (source for MOSI)
            var txData = new byte[len];
            for(var i = 0; i < len; i++)
            {
                try
                {
                    txData[i] = Machine.SystemBus.ReadByte((ulong)(rdAddress + (uint)i));
                }
                catch(Exception e)
                {
                    this.Log(LogLevel.Error, "Failed to read TX data at 0x{0:X8}: {1}",
                        rdAddress + (uint)i, e.Message);
                    SignalTransferComplete();
                    return;
                }
            }

            // Perform SPI transfer with connected device
            var rxData = new byte[len];
            if(slaveSelected.Value)
            {
                for(var i = 0; i < len; i++)
                {
                    rxData[i] = device.Transmit(txData[i]);
                }
                device.FinishTransmission();

                // Write RX data to system memory at WR_ADDR (destination for MISO)
                for(var i = 0; i < len; i++)
                {
                    try
                    {
                        Machine.SystemBus.WriteByte((ulong)(wrAddress + (uint)i), rxData[i]);
                    }
                    catch(Exception e)
                    {
                        this.Log(LogLevel.Error, "Failed to write RX data at 0x{0:X8}: {1}",
                            wrAddress + (uint)i, e.Message);
                        SignalTransferComplete();
                        return;
                    }
                }
            }
            else
            {
                this.Log(LogLevel.Warning, "Transfer skipped: no slave selected (SER=0)");
            }

            this.Log(LogLevel.Debug, "SPI transfer completed: {0} bytes", len);
            SignalTransferComplete();
        }

        private void SignalTransferComplete()
        {
            transferActive = false;
            startTransfer.Value = false;
            interruptStatus.Value = true;
            UpdateInterrupt();
        }

        private void UpdateInterrupt()
        {
            var active = interruptStatus.Value && doneIntEnable.Value;
            IRQ.Set(active);
        }

        private bool transferActive;

        private uint rdAddress;
        private uint wrAddress;
        private uint dataLength;

        private IFlagRegisterField startTransfer;
        private IFlagRegisterField ssiEnabled;
        private IFlagRegisterField slaveSelected;
        private IValueRegisterField dataFrameSize;
        private IValueRegisterField transferMode;
        private IFlagRegisterField doneIntEnable;
        private IFlagRegisterField interruptStatus;

        private readonly DoubleWordRegisterCollection ctrlRegisters;
        private readonly DoubleWordRegisterCollection dwRegisters;

        private enum RegistersCtrl : long
        {
            StartControl = 0x00,
            StopControl = 0x04,
            RdAddress = 0x08,
            WrAddress = 0x0C,
            DataLength = 0x10,
            Control = 0x14,
            IrqEnable = 0x18,
            IrqStatus = 0x1C,
            MsSelect = 0x20,
            DmaState = 0x24,
            RdFifoThreshold = 0x28,
        }

        private enum RegistersDW : long
        {
            Ctrlr0 = 0x00,
            Ctrlr1 = 0x04,
            SsiEnable = 0x08,
            MultiWordControl = 0x0C,
            SlaveEnable = 0x10,
            BaudRate = 0x14,
            TxFifoThreshold = 0x18,
            RxFifoThreshold = 0x1C,
            TxFifoLevel = 0x20,
            RxFifoLevel = 0x24,
            Status = 0x28,
            InterruptMask = 0x2C,
            InterruptStatus = 0x30,
            RawInterruptStatus = 0x34,
            TxFifoOverflowClear = 0x38,
            RxFifoOverflowClear = 0x3C,
            RxFifoUnderflowClear = 0x40,
            MultiMasterInterruptClear = 0x44,
            InterruptClear = 0x48,
            DmaControl = 0x4C,
            DmaTxDataLevel = 0x50,
            DmaRxDataLevel = 0x54,
            Identification = 0x58,
            ComponentVersion = 0x5C,
            DataRegister = 0x60,
            RxSampleDelay = 0xF0,
        }
    }
}
