//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.SD
{
    public abstract class MMCController : NullRegistrationPointPeripheralContainer<DeprecatedSDCard>, IDisposable
    {
        public override void Reset()
        {
            RegisteredPeripheral.Reset();
        }

        public void Dispose()
        {
            if(RegisteredPeripheral != null)
            {
                RegisteredPeripheral.Dispose();
            }
        }

        protected MMCController(IMachine machine) : base(machine)
        {
            this.machine = machine;
        }

        protected byte[] ReadFromCard(uint cardOffset, int bytes)
        {
            return RegisteredPeripheral.ReadData((long)BlockSize * cardOffset, bytes);
        }

        protected void WriteToCard(uint cardOffset, byte[] data)
        {
            RegisteredPeripheral.WriteData((long)BlockSize * cardOffset, data.Length, data);
        }

        protected uint[] ExecuteCommand(Commands command, uint args, bool sendInitSequence, bool dataTransfer)
        {
            var response = new uint[4];
            if(RegisteredPeripheral == null)
            {
                return response;
            }
            
            switch(command)
            {
            case Commands.GoIdleState:
                if(sendInitSequence)
                {
                    response[0] = RegisteredPeripheral.GoIdleState();
                }
                break;
            case Commands.SendOpCond:
            case Commands.IoSendOpCond:
                response[0] = RegisteredPeripheral.SendOpCond();
                break;
            case Commands.SendStatus:
                response[0] = RegisteredPeripheral.SendStatus(dataTransfer);
                break;
            case Commands.SendSdConfiguration:
                SendSdConfigurationValue();
                break;
            case Commands.SetRelativeAddress:
                response[0] = RegisteredPeripheral.SetRelativeAddress();
                break;
            case Commands.SelectDeselectCard:
                response[0] = RegisteredPeripheral.SelectDeselectCard();
                break;
            case Commands.SendExtendedCardSpecificData:
                response[0] = RegisteredPeripheral.SendExtendedCardSpecificData();
                break;
            case Commands.SendCardSpecificData:
                response = RegisteredPeripheral.SendCardSpecificData();
                break;
            case Commands.AllSendCid:
                return RegisteredPeripheral.AllSendCardIdentification();
            case Commands.AppCommand:
                response[0] = RegisteredPeripheral.AppCommand(args);
                break;
            case Commands.Switch:
                response[0] = RegisteredPeripheral.Switch();
                break;
            case Commands.MmcSendAppOpCode:
                response[0] = RegisteredPeripheral.SendAppOpCode(args);
                break;
            case Commands.ReadMultipleBlocks:
                TransferDataFromCard(args, ByteCount);
                break;
            case Commands.ReadSingleBlock:
                TransferDataFromCard(args, BlockSize);
                break;
            case Commands.WriteMultipleBlocks:
                TransferDataToCard(args, ByteCount);
                break;
            default:
                this.Log(LogLevel.Warning, "Unhandled MMC command: 0x{0:X} with args 0x{1:X}", command, args);
                break;
            }
            return response;
        }

        protected abstract void SendSdConfigurationValue();
        protected abstract void TransferDataToCard(uint cardOffset, int bytes);
        protected abstract void TransferDataFromCard(uint cardOffset, int bytes);

        protected int BlockSize, ByteCount;

        protected readonly IMachine machine;

        protected enum Commands
        {
            GoIdleState = 0x0,
            SendOpCond = 0x1,
            AllSendCid = 0x2,
            SetRelativeAddress = 0x3,
            SetDsr = 0x4,
            IoSendOpCond = 0x05,
            Switch = 0x6,
            SelectDeselectCard = 0x7,
            SendExtendedCardSpecificData = 0x8,
            SendCardSpecificData = 0x9,
            SendCardIdentification = 0xa,
            ReadDataUntilStop = 0xb,
            StopTransmission = 0xc,
            SendStatus = 0xd,
            BusTestRead = 0xe,
            GoInactiveState = 0xf,
            SetBlockLength = 0x10,
            ReadSingleBlock = 0x11,
            ReadMultipleBlocks = 0x12,
            BusTestWrite = 0x13,
            WriteDataUntilStop = 0x14,
            SetBlockCount = 0x17,
            WriteBlock = 0x18,
            WriteMultipleBlocks = 0x19,
            ProgramCardIdentification = 0x1a,
            ProgramCardSpecificData = 0x1b,
            SetWriteProtection = 0x1c,
            ClearWriteProtection = 0x1d,
            SendWriteProtection = 0x1e,
            EraseGroupStart = 0x23,
            EraseGroupEnd = 0x24,
            Erase = 0x26,
            FastIo = 0x27,
            GoIrqState = 0x28,
            MmcSendAppOpCode = 0x29,
            LockUnlock = 0x2a,
            SendSdConfiguration = 0x33,
            IoRwDirect = 0x34,
            IoRwExtended = 0x35,
            AppCommand = 0x37,
            GenCommand = 0x38,
        }
    }
}
