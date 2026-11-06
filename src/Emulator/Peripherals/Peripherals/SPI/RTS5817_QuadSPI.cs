//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class RTS5817_QuadSPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RTS5817_QuadSPI(IMachine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();

            Array.Clear(subTransferMode, 0, subTransferMode.Length);
            Array.Clear(subTransferCommand, 0, subTransferCommand.Length);
            Array.Clear(subTransferAddress, 0, subTransferAddress.Length);
            Array.Clear(subTransferLength, 0, subTransferLength.Length);

            dmaAddress = 0;
            dmaLength = 0;
            dmaDirection = 0;

            isIdle = true;

            var device = RegisteredPeripheral;
            if(device != null)
            {
                device.FinishTransmission();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size => 0x200;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.MetaControl.Define(this)
                .WithValueField(0, 32, name: "META_CTRL");

            Registers.CANumber.Define(this)
                .WithValueField(0, 5, out caAddrBitNum, name: "U_ADDR_BIT_NUM")
                .WithValueField(5, 3, out caCmdBitNum, name: "U_CMD_BIT_NUM");

            Registers.Control.Define(this)
                .WithFlag(0, FieldMode.Write, name: "SOFT_RST", writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        Reset();
                    }
                })
                .WithReservedBits(1, 1)
                .WithValueField(2, 2, name: "SPI_MODE")
                .WithReservedBits(4, 1)
                .WithFlag(5, name: "LSB_FIRST")
                .WithFlag(6, name: "CS_POLARITY")
                .WithReservedBits(7, 25);

            Registers.TimingControl.Define(this)
                .WithValueField(0, 2, name: "T_EDO")
                .WithValueField(2, 2, name: "T_CS")
                .WithReservedBits(4, 4)
                .WithValueField(8, 5, out dummyBitNum, name: "U_DUM_BIT_NUM")
                .WithReservedBits(13, 19);

            Registers.ClockDivider.Define(this)
                .WithValueField(0, 8, name: "SCK_DIVIDER")
                .WithReservedBits(8, 24);

            Registers.PadPullControl.Define(this)
                .WithValueField(0, 2, name: "MISO_PULLCTL")
                .WithValueField(2, 2, name: "MOSI_PULLCTL")
                .WithValueField(4, 2, name: "SCK_PULLCTL")
                .WithValueField(6, 2, name: "CS_N_PULLCTL")
                .WithValueField(8, 2, name: "WP_N_PULLCTL")
                .WithValueField(10, 2, name: "HOLD_N_PULLCTL")
                .WithReservedBits(12, 20);

            Registers.CRCKey.Define(this)
                .WithValueField(0, 32, name: "CRC_KEY");

            Registers.CRCEnable.Define(this)
                .WithFlag(0, name: "CRC_EN")
                .WithReservedBits(1, 31);

            Registers.CROutput.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "CRC_OUT", valueProviderCallback: _ => 0u);

            // COM_TRANSFER: writing triggers transfer execution
            Registers.ComTransfer.Define(this)
                .WithValueField(0, 2, out transferNum, name: "WB_TRANS_NUM")
                .WithFlag(2, FieldMode.Write, name: "WB_RST", writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        isIdle = true;
                        transferTimeoutFlag.Value = false;

                        Array.Clear(subTransferMode, 0, subTransferMode.Length);
                        Array.Clear(subTransferCommand, 0, subTransferCommand.Length);
                        Array.Clear(subTransferAddress, 0, subTransferAddress.Length);
                        Array.Clear(subTransferLength, 0, subTransferLength.Length);

                        dmaAddress = 0;
                        dmaLength = 0;
                        dmaDirection = 0;

                        var device = RegisteredPeripheral;
                        if(device != null)
                        {
                            device.FinishTransmission();
                        }
                    }
                })
                .WithFlag(3, out transferTimeoutFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "WB_TIMEOUT")
                .WithReservedBits(4, 2)
                .WithFlag(6, FieldMode.Read, name: "WB_IDLE", valueProviderCallback: _ => isIdle)
                .WithFlag(7, FieldMode.Write, name: "WB_START", writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        ExecuteTransfer();
                    }
                })
                .WithReservedBits(8, 24);

            // Sub-transfer register banks
            DefineSubRegisters(0, Registers.Sub1Mode, Registers.Sub1Command, Registers.Sub1Address, Registers.Sub1Length);
            DefineSubRegisters(1, Registers.Sub2Mode, Registers.Sub2Command, Registers.Sub2Address, Registers.Sub2Length);
            DefineSubRegisters(2, Registers.Sub3Mode, Registers.Sub3Command, Registers.Sub3Address, Registers.Sub3Length);
            DefineSubRegisters(3, Registers.Sub4Mode, Registers.Sub4Command, Registers.Sub4Address, Registers.Sub4Length);

            Registers.WbDmaAddress.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => dmaAddress = (uint)val, valueProviderCallback: _ => dmaAddress, name: "WB_DMA_ADDR");

            Registers.WbDmaLength.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => dmaLength = (uint)val, valueProviderCallback: _ => dmaLength, name: "WB_DMA_LEN");

            Registers.WbDmaDirection.Define(this)
                .WithValueField(0, 1, writeCallback: (_, val) => dmaDirection = (uint)val, valueProviderCallback: _ => dmaDirection, name: "WB_DMA_DIR")
                .WithReservedBits(1, 31);

            Registers.ComTimingControl1.Define(this)
                .WithValueField(0, 32, name: "COM_TCTL_1");

            Registers.ComTimingControl2.Define(this)
                .WithValueField(0, 32, name: "COM_TCTL_2");

            Registers.TopControl.Define(this)
                .WithValueField(0, 8, name: "TOP_CTL")
                .WithReservedBits(8, 24);

            Registers.SckAutoStop.Define(this)
                .WithValueField(0, 32, name: "SCK_AUTO_STOP");

            Registers.AxiMasterConfig.Define(this)
                .WithValueField(0, 32, name: "AXI_M_CFG");
        }

        private void DefineSubRegisters(int index, Registers modeReg, Registers cmdReg, Registers addrReg, Registers lenReg)
        {
            modeReg.Define(this)
                .WithValueField(0, 4, writeCallback: (_, val) => subTransferMode[index] = (uint)val, valueProviderCallback: _ => subTransferMode[index], name: $"SUB{index + 1}_MODE");

            cmdReg.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => subTransferCommand[index] = (uint)val, valueProviderCallback: _ => subTransferCommand[index], name: $"SUB{index + 1}_COMMAND");

            addrReg.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => subTransferAddress[index] = (uint)val, valueProviderCallback: _ => subTransferAddress[index], name: $"SUB{index + 1}_ADDR");

            lenReg.Define(this)
                .WithValueField(0, 32, writeCallback: (_, val) => subTransferLength[index] = (uint)val, valueProviderCallback: _ => subTransferLength[index], name: $"SUB{index + 1}_LENGTH");
        }

        private void ExecuteTransfer()
        {
            var device = RegisteredPeripheral;
            if(device == null)
            {
                this.Log(LogLevel.Warning, "Cannot start transfer: no flash device connected");
                SetTransferComplete(error: true);
                return;
            }

            isIdle = false;
            transferTimeoutFlag.Value = false;

            var numSubTransfers = (int)transferNum.Value + 1;

            var dataOffset = 0;

            for(var i = 0; i < numSubTransfers; i++)
            {
                var mode = subTransferMode[i];
                var cmd = (byte)(subTransferCommand[i] & 0xFF);
                var addr = subTransferAddress[i];
                var subLen = subTransferLength[i];

                this.Log(LogLevel.Debug, "  SUB{0}: mode=0x{1:X}, cmd=0x{2:X}, addr=0x{3:X}, len={4}",
                    i + 1, mode, cmd, addr, subLen);

                if(!ExecuteSubTransfer(device, mode, cmd, addr, subLen, ref dataOffset))
                {
                    this.Log(LogLevel.Warning, "Sub-transfer {0} failed", i + 1);
                    device.FinishTransmission();
                    SetTransferComplete(error: true);
                    return;
                }
                device.FinishTransmission();
            }

            SetTransferComplete(error: false);
        }

        private bool ExecuteSubTransfer(ISPIPeripheral device, uint mode, byte cmd, uint addr, uint subDataLen, ref int dataOffset)
        {
            switch(mode)
            {
            case SubTransferMode.CommandOnly: // SPI_C_MODE0 - just command byte
                device.Transmit(cmd);
                return true;

            case SubTransferMode.CommandAddress: // SPI_CA_MODE0 - command + address, no data
                device.Transmit(cmd);
                SendAddressBytes(device, addr);
                return true;

            case SubTransferMode.CommandDataOut: // SPI_CDO_MODE0 - command + data (to flash)
                device.Transmit(cmd);
                return TransferDataToFlash(device, subDataLen, ref dataOffset);

            case SubTransferMode.CommandDataIn: // SPI_CDI_MODE0 - command + data (from flash)
                device.Transmit(cmd);
                return TransferDataFromFlash(device, subDataLen, ref dataOffset);

            case SubTransferMode.CommandAddressDataOut: // SPI_CADO_MODE0 - command + address + data (to flash)
                device.Transmit(cmd);
                SendAddressBytes(device, addr);
                return TransferDataToFlash(device, subDataLen, ref dataOffset);

            case SubTransferMode.CommandAddressDataIn: // SPI_CADI_MODE0 - command + address + data (from flash)
                device.Transmit(cmd);
                SendAddressBytes(device, addr);
                SendDummyBytes(device);
                return TransferDataFromFlash(device, subDataLen, ref dataOffset);

            case SubTransferMode.Polling: // SPI_POLLING_MODE0 - status register polling
                return HandlePolling(device, cmd);

            case SubTransferMode.FastReadSingle:
            case SubTransferMode.FastReadDualOut:
            case SubTransferMode.FastReadDualInOut:
            case SubTransferMode.FastReadQuadOut:
            case SubTransferMode.FastReadQuadInOut:
                // Fast read modes: command + address + dummy + data
                device.Transmit(cmd);
                SendAddressBytes(device, addr);
                SendDummyBytes(device);
                return TransferDataFromFlash(device, subDataLen, ref dataOffset);

            default:
                this.Log(LogLevel.Warning, "Unsupported sub-transfer mode: 0x{0:X}", mode);
                return false;
            }
        }

        private void SendAddressBytes(ISPIPeripheral device, uint addr)
        {
            var addrBytes = GetAddressByteCount();
            for(var i = addrBytes - 1; i >= 0; i--)
            {
                device.Transmit((byte)(addr >> (i * 8)));
            }
        }

        private int GetAddressByteCount()
        {
            var addrBitNum = caAddrBitNum.Value;
            var addrBytes = addrBitNum > 0 ? (int)((addrBitNum + 7) / 8) : 3;
            return addrBytes > 4 ? 4 : addrBytes;
        }

        private void SendDummyBytes(ISPIPeripheral device)
        {
            var dummyBits = (int)dummyBitNum.Value;
            var dummyBytes = (dummyBits + 7) / 8;
            for(var i = 0; i < dummyBytes; i++)
            {
                device.Transmit(0);
            }
        }

        private bool TransferDataToFlash(ISPIPeripheral device, uint dataLen, ref int dataOffset)
        {
            if(dataLen == 0)
            {
                return true;
            }

            for(var i = 0u; i < dataLen; i++)
            {
                byte data;
                try
                {
                    data = Machine.SystemBus.ReadByte((ulong)(dmaAddress + (uint)dataOffset));
                }
                catch(Exception e)
                {
                    this.Log(LogLevel.Error, "Failed to read from system bus at 0x{0:X}: {1}", dmaAddress + (uint)dataOffset, e.Message);
                    return false;
                }
                device.Transmit(data);
                dataOffset++;
            }

            return true;
        }

        private bool TransferDataFromFlash(ISPIPeripheral device, uint dataLen, ref int dataOffset)
        {
            if(dataLen == 0)
            {
                return true;
            }

            for(var i = 0u; i < dataLen; i++)
            {
                var data = device.Transmit(0);
                try
                {
                    Machine.SystemBus.WriteByte((ulong)(dmaAddress + (uint)dataOffset), data);
                }
                catch(Exception e)
                {
                    this.Log(LogLevel.Error, "Failed to write to system bus at 0x{0:X}: {1}", dmaAddress + (uint)dataOffset, e.Message);
                    return false;
                }
                dataOffset++;
            }

            return true;
        }

        private bool HandlePolling(ISPIPeripheral device, byte cmd)
        {
            // In the real hardware, the controller sends the RDSR command and
            // continuously monitors the SO (MISO) line for the WIP (bit 0)
            // to become 0. In simulation, write/erase operations are instant,
            // so polling always succeeds immediately.
            device.Transmit(cmd);
            var status = device.Transmit(0);
            if((status & 0x01) != 0)
            {
                this.Log(LogLevel.Warning, "Polling: flash reports WIP=1 (busy), but simulation proceeds as write/erase is instant");
            }

            this.Log(LogLevel.Noisy, "Polling completed (status=0x{0:X})", status);
            return true;
        }

        private void SetTransferComplete(bool error)
        {
            if(error)
            {
                transferTimeoutFlag.Value = true;
            }

            isIdle = true;
        }

        private bool isIdle = true;

        private uint dmaAddress;
        private uint dmaLength;
        private uint dmaDirection;
        private IValueRegisterField caAddrBitNum;
        private IValueRegisterField caCmdBitNum;
        private IValueRegisterField dummyBitNum;
        private IValueRegisterField transferNum;
        private IFlagRegisterField transferTimeoutFlag;
        private readonly uint[] subTransferMode = new uint[4];
        private readonly uint[] subTransferCommand = new uint[4];
        private readonly uint[] subTransferAddress = new uint[4];
        private readonly uint[] subTransferLength = new uint[4];

        private static class SubTransferMode
        {
            public const uint CommandOnly = 0x0;           // SPI_C_MODE0
            public const uint CommandAddress = 0x1;        // SPI_CA_MODE0
            public const uint CommandDataOut = 0x2;        // SPI_CDO_MODE0
            public const uint CommandDataIn = 0x3;         // SPI_CDI_MODE0
            public const uint CommandAddressDataOut = 0x4; // SPI_CADO_MODE0
            public const uint CommandAddressDataIn = 0x5;  // SPI_CADI_MODE0
            public const uint Polling = 0x6;               // SPI_POLLING_MODE0
            public const uint FastReadSingle = 0x7;        // SPI_FAST_READ_MODE
            public const uint FastReadDualOut = 0x8;       // SPI_FAST_READ_DUAL_OUT_MODE
            public const uint FastReadDualInOut = 0x9;     // SPI_FAST_READ_DUAL_INOUT_MODE
            public const uint FastReadQuadOut = 0xA;       // SPI_FAST_READ_QUAD_OUT_MODE
            public const uint FastReadQuadInOut = 0xB;     // SPI_FAST_READ_QUAD_INOUT_MODE
        }

        private enum Registers : long
        {
            MetaControl = 0x04,
            CANumber = 0x08,
            Control = 0x10,
            TimingControl = 0x14,
            ClockDivider = 0x18,
            PadPullControl = 0x1C,
            CRCKey = 0x20,
            CRCEnable = 0x24,
            CROutput = 0x28,
            ComTransfer = 0x2C,
            Sub1Mode = 0x30,
            Sub1Command = 0x34,
            Sub1Address = 0x38,
            Sub1Length = 0x3C,
            Sub2Mode = 0x40,
            Sub2Command = 0x44,
            Sub2Address = 0x48,
            Sub2Length = 0x4C,
            Sub3Mode = 0x50,
            Sub3Command = 0x54,
            Sub3Address = 0x58,
            Sub3Length = 0x5C,
            Sub4Mode = 0x60,
            Sub4Command = 0x64,
            Sub4Address = 0x68,
            Sub4Length = 0x6C,
            WbDmaAddress = 0x78,
            WbDmaLength = 0x7C,
            WbDmaDirection = 0x80,
            ComTimingControl1 = 0x90,
            ComTimingControl2 = 0x94,
            TopControl = 0x98,
            SckAutoStop = 0x9C,
            AxiMasterConfig = 0xA0,
        }
    }
}
