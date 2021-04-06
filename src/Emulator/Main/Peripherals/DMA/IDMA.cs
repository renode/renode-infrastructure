//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.DMA
{
    public interface IDMA : IPeripheral
    {
        void RequestTransfer(int channel);

        int NumberOfChannels { get; }
    }
}

