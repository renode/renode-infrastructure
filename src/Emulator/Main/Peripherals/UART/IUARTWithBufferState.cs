//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

namespace Antmicro.Renode.Peripherals.UART
{
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
