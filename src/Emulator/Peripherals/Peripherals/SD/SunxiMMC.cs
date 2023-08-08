//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.DMA;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.SD
{
    public class SunxiMMC : MMCController, IDoubleWordPeripheral, IKnownSize
    {
        public SunxiMMC(IMachine machine) : base(machine)
        {
            SetupRegisters();
            IRQ = new GPIO();
            dmaEngine = new DmaEngine(machine.GetSystemBus(this));
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.InterruptMaskRegister:
                return interruptMask;
            case Registers.RawInterruptStatusRegister:
                return rawInterruptStatus;
            case Registers.DescriptorListBaseAddress:
                return descriptorListBaseAddress;
            case Registers.CommandArgumentRegister:
                return commandArgument;
            case Registers.ClockControlRegister:
            case Registers.MaskedInterruptStatusRegister:
                return rawInterruptStatus & interruptMask;
            case Registers.ResponseRegister0:
                return responseRegisters[0];
            case Registers.ResponseRegister1:
                return responseRegisters[1];
            case Registers.ResponseRegister2:
                return responseRegisters[2];
            case Registers.ResponseRegister3:
                return responseRegisters[3];
            case Registers.ByteCountRegister:
                return (uint)ByteCount;
            case Registers.CommandRegister:
                return commandRegister.Read();
            case Registers.DmacStatus:
                return dmacStatusRegister.Read();
            default:
                return generalRegisters.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.InterruptMaskRegister:
                interruptMask = value;
                break;
            case Registers.RawInterruptStatusRegister:
                rawInterruptStatus &= ~value; // write one to clear
                Update();
                break;
            case Registers.DescriptorListBaseAddress:
                descriptorListBaseAddress = value;
                break;
            case Registers.CommandArgumentRegister:
                commandArgument = value;
                break;
            case Registers.ByteCountRegister:
                ByteCount = (int)value;
                break;
            case Registers.CommandRegister:
                commandRegister.Write(offset, value);
                break;
            case Registers.DmacStatus:
                dmacStatusRegister.Write(offset, value);
                break;
            default:
                generalRegisters.Write(offset, value);
                break;
            }
        }

        public override void Reset()
        {
            generalRegisters.Reset();
            interruptMask = 0;
            descriptorListBaseAddress = 0;
            commandArgument = 0;
            commandRegister.Reset();
            dmacStatusRegister.Reset();
        }

        public long Size
        {
            get
            {
                return 0x1000;
            }
        }

        public GPIO IRQ
        {
            get;
            private set;
        }

        protected override void TransferDataFromCard(uint cardOffset, int bytes)
        {
            byte[] data = ReadFromCard(cardOffset, bytes);
            DmaTransfer(data, bytes, DataDirection.ReadFromSD);
        }

        protected override void TransferDataToCard(uint cardOffset, int bytes)
        {
            var data = new byte[bytes];
            DmaTransfer(data, bytes, DataDirection.WriteToSD);
            WriteToCard(cardOffset, data);
        }

        protected override void SendSdConfigurationValue()
        {
            byte[] data = BitConverter.GetBytes(RegisteredPeripheral.SendSdConfigurationValue());
            DmaTransfer(data, 8, DataDirection.ReadFromSD);
        }

        private void SetupRegisters()
        {
            commandRegister = new DoubleWordRegister(this);
            dmacStatusRegister = new DoubleWordRegister(this);
            startCommandFlag = commandRegister.DefineFlagField(31, changeCallback: OnStartCommand);
            sendInitSequence = commandRegister.DefineFlagField(15);
            transferDirection = commandRegister.DefineFlagField(10);
            dataTransfer = commandRegister.DefineFlagField(9);
            receiveResponse = commandRegister.DefineFlagField(6);
            commandIndex = commandRegister.DefineValueField(0, 6);

            responseRegisters = new uint[4];

            receiveInterrupt = dmacStatusRegister.DefineFlagField(1, FieldMode.WriteOneToClear | FieldMode.Read);
            transmitInterrupt = dmacStatusRegister.DefineFlagField(0, FieldMode.WriteOneToClear | FieldMode.Read);
            generalRegisters = new DoubleWordRegisterCollection(this, new Dictionary<long, DoubleWordRegister>() {
                {(long)Registers.ControlRegister, new DoubleWordRegister(this).WithFlag(0, changeCallback: (oldValue, newValue) => {if(newValue) Reset();}).WithFlag(2).WithFlag(4).WithFlag(5)},
                {(long)Registers.BlockSizeRegister, new DoubleWordRegister(this, 0x200).WithValueField(0, 16, changeCallback: (oldValue, newValue) => BlockSize = (int)newValue)},
                {(long)Registers.DmacInterruptEnable, DoubleWordRegister.CreateRWRegister()},
            });
        }

        private void DmaTransfer(byte[] data,  int bytes, DataDirection direction)
        {
            Place source, destination;
            uint currentDescriptorAddress = descriptorListBaseAddress;
            int bytesTransferred = 0;

            while(bytesTransferred < bytes)
            {
                
                int bytesLeft = bytes - bytesTransferred;
                var currentDescriptor = new SunxiDMADescriptor(currentDescriptorAddress, dmaEngine);
                int bytesToTransfer = currentDescriptor.BufferSize > bytesLeft ? bytesLeft : (int) currentDescriptor.BufferSize;

                if(direction == DataDirection.ReadFromSD)
                {
                    destination = currentDescriptor.BufferAddress;
                    source = new Place(data, bytesTransferred);
                }
                else
                {
                    destination = new Place(data, bytesTransferred);
                    source = currentDescriptor.BufferAddress;
                }

                var request = new Request(source, destination, bytesToTransfer, TransferType.DoubleWord, TransferType.DoubleWord);
                dmaEngine.IssueCopy(request);
                currentDescriptor.Release();
                bytesTransferred += bytesToTransfer;
                currentDescriptorAddress = currentDescriptor.NextDescriptor;
            }
        }

        private void Update()
        {
            if((rawInterruptStatus & interruptMask) != 0)
            {
                IRQ.Set();
            }
            else
            {
                IRQ.Unset();
            }
        }

        private void OnStartCommand(bool oldValue, bool newValue)
        {
            if(newValue)
            {
                responseRegisters = ExecuteCommand((Commands)commandIndex.Value, commandArgument, sendInitSequence.Value, dataTransfer.Value);
                startCommandFlag.Value = false;

                if(dataTransfer.Value)
                {
                    if(transferDirection.Value)
                    {
                        transmitInterrupt.Value = true;
                    }
                    else
                    {
                        receiveInterrupt.Value = true;
                    }
                    if((interruptMask & (int)Interrupts.DataTransferComplete) != 0)
                    {
                        rawInterruptStatus |= (int)Interrupts.DataTransferComplete;
                    }
                    else if((interruptMask & (int)Interrupts.AutoCommandDone) != 0)
                    {
                        rawInterruptStatus |= (int)Interrupts.AutoCommandDone;
                    }
                }
                else
                {   
                    if(receiveResponse.Value || sendInitSequence.Value)
                    {
                        var cmd = (Commands)commandIndex.Value;
                        if(cmd == Commands.IoSendOpCond || cmd == Commands.SendOpCond || cmd == Commands.IoRwDirect)
                        {
                            rawInterruptStatus |= (int)Interrupts.BootAck;
                        }
                        rawInterruptStatus |= (int)Interrupts.CommandComplete;
                    }
                }
                Update();
            }
        }

        private DoubleWordRegisterCollection generalRegisters;
        private DoubleWordRegister commandRegister, dmacStatusRegister;
        private IFlagRegisterField startCommandFlag, receiveResponse, sendInitSequence, receiveInterrupt, transmitInterrupt, dataTransfer, transferDirection;
        private IValueRegisterField commandIndex;
        private uint commandArgument, descriptorListBaseAddress, rawInterruptStatus, interruptMask;
        private uint[] responseRegisters;

        private readonly DmaEngine dmaEngine;

        private enum Registers
        {
            ControlRegister = 0x00,
            ClockControlRegister = 0x04,
            TimeOutRegister = 0x08,
            BusWidthRegister = 0x0c,
            BlockSizeRegister = 0x10,
            ByteCountRegister = 0x14,
            CommandRegister = 0x18,
            CommandArgumentRegister = 0x1c,
            ResponseRegister0 = 0x20,
            ResponseRegister1 = 0x24,
            ResponseRegister2 = 0x28,
            ResponseRegister3 = 0x2c,
            InterruptMaskRegister = 0x30,
            MaskedInterruptStatusRegister = 0x34,
            RawInterruptStatusRegister = 0x38,
            StatusRegister = 0x3c,
            FifoWaterLevelRegister = 0x40,
            FifoFunctionSelectRegister = 0x44,
            DebugEnableRegister = 0x50,
            BusModeControl = 0x80,
            DescriptorListBaseAddress = 0x84,
            DmacStatus = 0x88,
            DmacInterruptEnable = 0x8c,
            ReadWriteFifo = 0x100
        }

        [Flags]
        private enum Interrupts
        {
            ResponseError = (1 << 1),
            CommandComplete = (1 << 2),
            DataTransferComplete = (1 << 3),
            DataransmitRequest = (1 << 4),
            DataReceiveRequest = (1 << 5),
            ResponseCrcError = (1 << 6),
            DataCrcError = (1 << 7),
            ResponseTimeout = (1 << 8),
            BootAck = (1 << 8),
            DataTimeout = (1 << 9),
            BootDataStart = (1 << 9),
            DataStarvationTimeout = (1 << 10),
            VoltageSwitchDone = (1 << 10),
            FifoUnderrun = (1 << 11),
            FifoOverflow = (1 << 11),
            CommandBusy = (1 << 12),
            IllegalWrite = (1 << 12),
            DataStartError = (1 << 13), 
            AutoCommandDone = (1 << 14),
            DataEndBitError = (1 << 15),
            SdioInterrupt = (1 << 16),
            CardInserted = (1 << 30),
            CardRemoved = (1 << 31)
        }

        private enum DataDirection
        {
            WriteToSD,
            ReadFromSD
        }

        private class SunxiDMADescriptor
        {
            public SunxiDMADescriptor(ulong address, DmaEngine dmaEngine)
            {
                Address = address;

                byte[] descriptorData = new byte[16];
                Request getDescriptorData = new Request(Address, new Place(descriptorData, 0), 16,
                    TransferType.DoubleWord, TransferType.DoubleWord);

                dmaEngine.IssueCopy(getDescriptorData);
                Status = BitConverter.ToUInt32(descriptorData, 0);
                BufferSize = BitConverter.ToUInt32(descriptorData, 4);
                BufferAddress = BitConverter.ToUInt32(descriptorData, 8);
                NextDescriptor = BitConverter.ToUInt32(descriptorData, 12);

                if(BufferSize == 0) // the driver assumes 0-sized blocks to be 64kB, which is inconsistent with the Allwinner user manual.
                {
                    BufferSize = 0x10000;
                }
            }

            public void Release()
            {
                Status &= ~(1 << 31);
            }

            public uint BufferAddress
            {
                get;
                private set;
            }
            public uint BufferSize
            {
                get;
                private set;
            }
            public uint NextDescriptor
            {
                get;
                private set;
            }
            public ulong Address
            {
                get;
                private set;
            }
            public uint Status
            {
                get;
                private set;
            }
        }
    }
}
