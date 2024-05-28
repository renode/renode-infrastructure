//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    [AllowedTranslations(AllowedTranslation.DoubleWordToByte)]
    public class NPCX_FIU : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IKnownSize
    {
        public NPCX_FIU(IMachine machine) : base(machine)
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            userModeAccessLocked = false;
            userModeAccessAddress = 0x0;
            userModeAccessData = 0x0;
            use4ByteAddress = false;
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size => 0x1000;
        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.UMACodeByte.Define(this)
                .WithValueField(0, 8, out userModeAccessCode, name: "TR_CODE (Transaction Code)");
            
            Registers.UMAAddressByte0_0.DefineMany(this, 3, (register, i) =>
            {
                register
                    .WithValueField(0, 8, name: $"ADDR_B{i} (Address Byte {i})",
                        valueProviderCallback: _ => (byte)BitHelper.GetValue(userModeAccessAddress, i * 8, 8),
                        writeCallback: (_, value) => BitHelper.SetMaskedValue(ref userModeAccessAddress, (byte)value, i * 8, 8));
            });

            Registers.UMADataByte0_0.DefineMany(this, 4, (register, i) =>
            {
                register
                    .WithValueField(0, 8, name: $"DAT_B{i} (Data Byte {i})",
                        valueProviderCallback: _ => (byte)BitHelper.GetValue(userModeAccessData, i * 8, 8),
                        writeCallback: (_, value) => BitHelper.SetMaskedValue(ref userModeAccessData, (byte)value, i * 8, 8));
            });

            Registers.UMAControlAndStatus.Define(this, 0x40)
                .WithValueField(0, 3, out var dataWidth, name: "D_SIZE (Data Field Size Select)")
                .WithFlag(3, out var addressWidthSelect, name: "A_SIZE (Address Field Size Select)")
                .WithFlag(4, out var skipCodeField, name: "C_SIZE (Code Field Size Select)")
                .WithFlag(5, out var isWrite, name: "RD_WR (Read/Write Select)")
                .WithReservedBits(6, 1, allowedValue: 0x1)
                .WithFlag(7, out var umaTransactionStart, name: "EXEC_DONE (Operation Execute/Done)")
                .WithWriteCallback((_, __) =>
                    {
                        if(!umaTransactionStart.Value)
                        {
                            return;
                        }
                        umaTransactionStart.Value = false;

                        if(userModeAccessLocked)
                        {
                            this.ErrorLog("Attempted to start a UMA transaction while UMA_LOCK is active");
                            return;
                        }

                        var addressWidth = (int)userModeAccessAddressSize.Value;
                        if(addressWidthSelect.Value)
                        {
                            addressWidth = use4ByteAddress ? 4 : 3;
                        }

                        var skipCode = skipCodeField.Value;
                        if(isWrite.Value)
                        {
                            skipCode = false;
                        }

                        PerformUserModeAccessTransaction(addressWidth, (int)dataWidth.Value, isWrite.Value, skipCode);
                    });
            
            Registers.UMAExtendedControlAndStatus.Define(this, 0x3)
                .WithReservedBits(0, 1, allowedValue: 0x1)
                .WithFlag(1, out softwareChipSelect, name: "SW_CS1 (Software Controlled Chip-Select 1n)",
                    changeCallback: (_, value) =>
                        {
                            if(!value)
                            {
                                return;
                            }

                            RegisteredPeripheral?.FinishTransmission();
                        })
                .WithTaggedFlag("SEC_CS (Secondary Chip-Select)", 2)
                .WithFlag(3, name: "UMA_LOCK (UMA Operation Lock)",
                    valueProviderCallback: _ => userModeAccessLocked,
                    writeCallback: (_, value) =>
                        {
                            if(userModeAccessLocked)
                            {
                                return;
                            }

                            userModeAccessLocked = value;
                        })
                .WithValueField(4, 3, out userModeAccessAddressSize, name: "UMA_ADDR_SIZE (Address Field Size Select)")
                .WithReservedBits(7, 1);

            Registers.UMADataByte1_0.DefineMany(this, 4, (register, i) =>
            {
                register
                    .WithValueField(0, 8, FieldMode.Read, name: $"DAT_B{i} (Data Byte {i})",
                        valueProviderCallback: _ => (byte)BitHelper.GetValue(userModeAccessData, i * 8, 8));
            });
            
            Registers.UMAAddressByte1_0.DefineMany(this, 4, (register, i) =>
            {
                register
                    .WithValueField(0, 8, name: $"ADDR_B{i} (Address Byte {i})",
                        valueProviderCallback: _ => (byte)BitHelper.GetValue(userModeAccessAddress, i * 8, 8),
                        writeCallback: (_, value) => BitHelper.SetMaskedValue(ref userModeAccessAddress, (byte)value, i * 8, 8));
            });

            Registers.SPI1Device.Define(this)
                .WithTag("SPI1_LO_DEV_SIZE (SPI0 Low Device size)", 0, 4)
                .WithReservedBits(4, 2)
                .WithFlag(6, out var use4ByteCS10, name: "FOUR_BADDR_CS10 (Four Bytes Address for Chip-Select 10)")
                .WithFlag(7, out var use4ByteCS11, name: "FOUR_BADDR_CS11 (Four Bytes Address for Chip-Select 11)")
                .WithWriteCallback((_, __) =>
                    {
                        use4ByteAddress = use4ByteCS10.Value || use4ByteCS11.Value;
                    });
        }

        private void PerformUserModeAccessTransaction(int addressWidth, int dataWidth, bool isWrite, bool skipCode)
        {
            if(RegisteredPeripheral == null)
            {
                this.ErrorLog("Attempted to perform a UMA transaction with no peripheral attached");
                return;
            }

            if(!skipCode)
            {
                RegisteredPeripheral.Transmit((byte)userModeAccessCode.Value);
            }

            for(var i = 0; i < addressWidth; i++)
            {
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(userModeAccessAddress, i * 8, 8));
            }

            for(var i = 0; i < dataWidth; i++)
            {
                var value = RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(userModeAccessData, i * 8, 8));
                if(!isWrite)
                {
                    BitHelper.SetMaskedValue(ref userModeAccessData, value, i * 8, 8);
                }
            }

            if(softwareChipSelect.Value)
            {
                RegisteredPeripheral.FinishTransmission();
            }
        }

        private IValueRegisterField userModeAccessCode;
        private IValueRegisterField userModeAccessAddressSize;
        private IFlagRegisterField softwareChipSelect;

        private uint userModeAccessAddress;
        private uint userModeAccessData;
        private bool userModeAccessLocked;
        private bool use4ByteAddress;

        private enum Registers
        {
            BurstConfiguration          = 0x01, // BURST_CFG
            ResponseConfiguration       = 0x02, // RESP_CFG
            SPIFlashConfiguration       = 0x14, // SPI_FL_CFG
            UMACodeByte                 = 0x16, // UMA_CODE
            UMAAddressByte0_0           = 0x17, // UMA_AB0
            UMAAddressByte0_1           = 0x18, // UMA_AB1
            UMAAddressByte0_2           = 0x19, // UMA_AB2
            UMADataByte0_0              = 0x1A, // UMA_DB0
            UMADataByte0_1              = 0x1B, // UMA_DB1
            UMADataByte0_2              = 0x1C, // UMA_DB2
            UMADataByte0_3              = 0x1D, // UMA_DB3
            UMAControlAndStatus         = 0x1E, // UMA_CTS
            UMAExtendedControlAndStatus = 0x1F, // UMA_ECTS
            UMADataByte1_0              = 0x20, // UMA_DB0 NOTE: This is a read-only mirror of the UMA_DB0-3
            UMADataByte1_1              = 0x21, // UMA_DB1
            UMADataByte1_2              = 0x22, // UMA_DB2
            UMADataByte1_3              = 0x23, // UMA_DB3
            CRCControl                  = 0x26, // CRCCON
            CRCResult                   = 0x27, // CRCRSLT
            FIUReadCommand              = 0x30, // FIU_RD_CMD
            FIUDummyCycle               = 0x32, // FIU_DMM_CYC
            FIUExtendedConfiguration    = 0x33, // FIU_EXT_CFG
            UMAAddressByte1_0           = 0x34, // UMA_AB0 NOTE: Only to be used with external flash
            UMAAddressByte1_1           = 0x35, // UMA_AB1       and when UMA_ADDR_SIZE is 4
            UMAAddressByte1_2           = 0x36, // UMA_AB2
            UMAAddressByte1_3           = 0x37, // UMA_AB3
            SPI1Device                  = 0x3D, // SPI1_DEV
        }
    }
}

