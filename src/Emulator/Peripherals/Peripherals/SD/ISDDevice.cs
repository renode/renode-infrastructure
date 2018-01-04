//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.SD
{
    [Icon("sd")]
    public interface ISDDevice : IPeripheral, IDisposable
    {
        uint GoIdleState();
        uint SendOpCond();
        uint SendStatus(bool dataTransfer);
        ulong SendSdConfigurationValue();
        uint SetRelativeAddress();
        uint SelectDeselectCard();
        uint SendExtendedCardSpecificData();
        uint[] AllSendCardIdentification();
        uint AppCommand(uint argument);
        uint Switch();
        uint[] SendCardSpecificData();
        uint SendAppOpCode(uint argument);
        byte[] ReadData(long offset, int size);
        void WriteData(long offset, int size, byte[] data);
    }
}

