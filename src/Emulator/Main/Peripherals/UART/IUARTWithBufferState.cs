//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.UART
{
    [Icon("monitor")]
    public interface IUARTWithBufferState : IUART
    {
        event Action<BufferState> BufferStateChanged;

        BufferState BufferState { get; }
    }

    public enum BufferState
    {
        Empty,
        Ready,
        Full,
    }
}
