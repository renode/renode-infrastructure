//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.UART
{
    [Icon("monitor")]
    public interface IUART<T> : IPeripheral
    {
        // This field should be made [Transient] in all implementor classes!
        event Action<T> CharReceived;

        void WriteChar(T value);

        uint BaudRate { get; }

        Bits StopBits { get; }

        Parity ParityBit { get; }
    }

    public interface IUART : IUART<byte> { }

    public enum Parity
    {
        Odd,
        Even,
        None,
        Forced1,
        Forced0,
        Multidrop
    }

    public enum Bits
    {
        None,
        One,
        Half,
        OneAndAHalf,
        Two
    }
}
