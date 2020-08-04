//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

namespace Antmicro.Renode.Peripherals.ATAPI
{
    public interface IAtapiPeripheral : IPeripheral
    {
        void HandleCommand(byte[] packet);
        void SendIdentifyResponse();
        ushort DequeueData();
        bool DataReady { get; }
    }
}
