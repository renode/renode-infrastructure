//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Numerics;

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.UART
{
    public interface IUARTWithFrameInfo<T> : IUART<T>
        where T : IBinaryInteger<T>
    {
        void WriteChar(T value, UARTFrame frame);
    }

    public interface IUARTWithFrameInfo : IUARTWithFrameInfo<byte>, IUART { }

    public class UARTFrame
    {
        public static UARTFrame CreateFromSenderAndMessage<I, T>(I sender, T message, bool noise = false)
            where I : class, IUART<T>
            where T : IBinaryInteger<T>
        {
            return new UARTFrame(CalculateParityBit(message, sender.ParityBit), sender.StopBits, sender.BaudRate, noise);
        }

        public static ParityBitValue CalculateParityBit<T>(T msg, Parity parity)
            where T : IBinaryInteger<T>
        {
            switch(parity)
            {
            case Parity.None:
                return ParityBitValue.None;
            case Parity.Even:
                return BitHelper.CalculateParity(msg) ? ParityBitValue.One : ParityBitValue.Zero;
            case Parity.Odd:
                return BitHelper.CalculateParity(msg) ? ParityBitValue.Zero : ParityBitValue.One;
            case Parity.Forced1:
                return ParityBitValue.One;
            case Parity.Forced0:
                return ParityBitValue.Zero;
            case Parity.Unsupported:
                return ParityBitValue.Unsupported;
            default:
                throw new NotImplementedException($"Unsupported parity type {parity}");
            }
        }

        public UARTFrame(ParityBitValue parityBit, Bits stopBits, uint baudRate, bool noise)
        {
            ParityBit = parityBit;
            StopBits = stopBits;
            BaudRate = baudRate;
            Noise = noise;
        }

        public override string ToString()
        {
            return $"{nameof(UARTFrame)} {{{nameof(ParityBit)}: {ParityBit}, {nameof(StopBits)}: {StopBits}, {nameof(BaudRate)}: {BaudRate}, {nameof(Noise)}: {Noise}}}";
        }

        public ParityBitValue ParityBit { get; set; }

        public Bits StopBits { get; set; }

        public uint BaudRate { get; set; }

        public bool Noise { get; set; }

        public enum ParityBitValue
        {
            None,
            Zero,
            One,
            Unsupported
        }
    }
}
