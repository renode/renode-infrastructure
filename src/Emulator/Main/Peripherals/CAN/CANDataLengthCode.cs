//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

namespace Antmicro.Renode.Core.CAN;

public static class CANDataLengthCode
{
    public static bool TryFromPayloadLength(int payloadLength, out byte dataLengthCode)
    {
        if(payloadLength >= 0 && payloadLength <= MaxClassicalDataLengthCode)
        {
            dataLengthCode = (byte)payloadLength;
            return true;
        }

        var index = Array.IndexOf(fdPayloadLengths, payloadLength);
        dataLengthCode = index >= 0 ? (byte)(FirstFdDataLengthCode + index) : (byte)0;
        return index >= 0;
    }

    public static bool TryToPayloadLength(int dataLengthCode, out int payloadLength)
    {
        if(dataLengthCode >= 0 && dataLengthCode <= MaxClassicalDataLengthCode)
        {
            payloadLength = dataLengthCode;
            return true;
        }

        var index = dataLengthCode - FirstFdDataLengthCode;
        if(index >= 0 && index < fdPayloadLengths.Length)
        {
            payloadLength = fdPayloadLengths[index];
            return true;
        }

        payloadLength = 0;
        return false;
    }

    private static readonly int[] fdPayloadLengths = { 12, 16, 20, 24, 32, 48, 64 };

    private const int MaxClassicalDataLengthCode = 8;
    private const int FirstFdDataLengthCode = MaxClassicalDataLengthCode + 1;
}
