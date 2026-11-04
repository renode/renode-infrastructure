//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Numerics;

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

// Helpers shared by the Ethos-U models.
public static class ARM_EthosCommon
{
    public static long ReadValue(byte[] bytes, int byteId, int byteLength, bool signed)
    {
        if(byteId < 0 || byteId + byteLength > bytes.Length)
        {
            throw new InvalidOperationException($"Out of bounds read at {byteId} ({byteLength} bytes, buffer length {bytes.Length})");
        }

        return byteLength switch
        {
            1 => signed ? unchecked((sbyte)bytes[byteId]) : bytes[byteId],
            2 => signed ? BitConverter.ToInt16(bytes, byteId) : BitConverter.ToUInt16(bytes, byteId),
            4 => signed ? BitConverter.ToInt32(bytes, byteId) : BitConverter.ToUInt32(bytes, byteId),
            _ => throw new InvalidOperationException($"Invalid element byte length: {byteLength}"),
        };
    }

    public static void WriteValue(byte[] bytes, long byteId, long value, int byteLength)
    {
        if(byteId < 0 || byteId + byteLength > bytes.Length)
        {
            throw new InvalidOperationException($"Out of bounds write at {byteId} ({byteLength} bytes, buffer length {bytes.Length})");
        }

        for(var i = 0; i < byteLength; i++)
        {
            bytes[byteId + i] = unchecked((byte)(value >> (8 * i)));
        }
    }

    // U85 TRM rounded_shift_right
    public static long RoundedShiftRight(long acc, int shift, RoundingMode mode, int doubleRoundShift = 0)
    {
        if(shift < 0 || shift > 63)
        {
            throw new ArgumentOutOfRangeException(nameof(shift));
        }
        if(doubleRoundShift < 0 || doubleRoundShift > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(doubleRoundShift));
        }

        if(shift == 0)
        {
            return acc;
        }

        var rounding = BigInteger.One << (shift - 1);
        var doubleRounding = shift > (31 - doubleRoundShift) ? BigInteger.One << (30 - doubleRoundShift) : BigInteger.Zero;
        switch(mode)
        {
        case RoundingMode.DoubleSymmetric:
            rounding += acc >= 0 ? doubleRounding : -doubleRounding;
            break;
        case RoundingMode.DoubleAsymmetric:
            rounding += doubleRounding;
            break;
        case RoundingMode.TruncateToZero:
            rounding = acc >= 0 ? BigInteger.Zero : (BigInteger.One << shift) - 1;
            break;
        case RoundingMode.TruncateToLower:
            rounding = BigInteger.Zero;
            break;
        case RoundingMode.Natural:
            break;
        case RoundingMode.Symmetric:
            if(acc < 0)
            {
                rounding -= 1;
            }
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        return (long)(((BigInteger)acc + rounding) >> shift);
    }

    // Decode the activation type of an xFM precision register.
    public static (bool Signed, int ByteLength) DecodeActivationType(ushort precision, bool isOfm, bool allow64 = true)
    {
        var signed = (precision & 1) != 0;
        var precisionBits = isOfm ? (precision >> 1) & 0b11 : (precision >> 2) & 0b11;
        var byteLength = precisionBits switch
        {
            0 => 1,
            1 => 2,
            2 => 4,
            3 when allow64 => 8,
            _  => 0,
        };
        return (signed, byteLength);
    }

    public enum FeatureMap
    {
        Ifm,
        Ifm2,
        Ofm,
    }

    public enum IfmUpscaleMode
    {
        None = 0,
        Nearest = 1,
        InsertZeros = 2,
    }

    public enum RoundingMode
    {
        DoubleSymmetric,
        DoubleAsymmetric,
        TruncateToZero,
        TruncateToLower,
        Natural,
        Symmetric,
    }
}
