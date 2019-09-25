//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities
{
    public enum CRCType
    {
        CRC32CCITTPolynomial = 0x04C11DB7,
        CRC16Polynomial = 0x8005,
        CRC16CCITTPolynomial = 0x1021,
    }

    public static class CRCTypeHelper
    {
        public static int GetLength(this CRCType type)
        {
            switch(type)
            {
                case CRCType.CRC16CCITTPolynomial:
                case CRCType.CRC16Polynomial:
                    return 2;
                case CRCType.CRC32CCITTPolynomial:
                    return 4;
                default:
                    var result = (uint)type > 0xFFFF ? 4 : 2; // we will assume that a long polynomial has a long CRC
                    Logger.Log(LogLevel.Warning, "Unknown CRC type length for polynomial 0x{0:X}. Guessed {1} bytes.", (int)type, result);
                    return result;
            }
        }
    }
}
