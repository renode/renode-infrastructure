//
// Copyright (c) 2026 Microsoft
// Licensed under the MIT License.
//
using System;
using System.Security.Cryptography;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // Aspeed AST2600 HACE — Hash and Crypto Engine
    // Reference: QEMU hw/misc/aspeed_hace.c
    //
    // Implements SHA-256/SHA-384/SHA-512/SHA-1/MD5 hashing with
    // direct and scatter-gather DMA modes. Reads source data from
    // system bus (DRAM) and writes digest output back.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_HACE : IDoubleWordPeripheral, IKnownSize, IGPIOSender
    {
        public Aspeed_HACE(IMachine machine)
        {
            this.machine = machine;
            IRQ = new GPIO();
            storage = new uint[RegisterSpaceSize / 4];
            Reset();
        }

        public long Size => RegisterSpaceSize;
        public GPIO IRQ { get; }

        public void Reset()
        {
            Array.Clear(storage, 0, storage.Length);
            IRQ.Unset();
            totalReqLen = 0;
            accumulationCtx = null;
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset >= 0 && offset < RegisterSpaceSize)
            {
                return storage[(uint)offset / 4];
            }
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset < 0 || offset >= RegisterSpaceSize)
            {
                return;
            }

            var reg = (uint)offset / 4;

            switch(offset)
            {
                case StatusOffset:
                    // W1C for HASH_IRQ (bit 9)
                    if((value & HashIrqBit) != 0)
                    {
                        storage[reg] &= ~HashIrqBit;
                        IRQ.Unset();
                    }
                    break;

                case HashSrcOffset:
                    storage[reg] = value & SrcMask;
                    break;

                case HashDigestOffset:
                    storage[reg] = value & DestMask;
                    break;

                case HashKeyBuffOffset:
                    storage[reg] = value & KeyMask;
                    break;

                case HashSrcLenOffset:
                    storage[reg] = value & LenMask;
                    break;

                case HashCmdOffset:
                    storage[reg] = value & HashCmdMask;
                    ExecuteHash(value & HashCmdMask);
                    break;

                case CryptCmdOffset:
                    storage[reg] = value;
                    this.Log(LogLevel.Warning, "HACE: Crypt commands not implemented");
                    break;

                default:
                    storage[reg] = value;
                    break;
            }
        }

        private void ExecuteHash(uint cmd)
        {
            var algo = GetHashAlgorithm(cmd);
            if(!algo.HasValue)
            {
                this.Log(LogLevel.Error, "HACE: Invalid hash algorithm selection 0x{0:X}", cmd);
                return;
            }

            var hmacMode = (cmd >> 7) & 0x3;
            if(hmacMode == 1 || hmacMode == 3)
            {
                this.Log(LogLevel.Warning, "HACE: HMAC mode not implemented");
            }

            var cascadedMode = cmd & 0x3;
            if(cascadedMode != 0)
            {
                this.Log(LogLevel.Warning, "HACE: Cascaded mode not implemented");
            }

            bool sgMode = (cmd & SgEnBit) != 0;
            bool accumMode = hmacMode == 2;

            var srcAddr = storage[HashSrcOffset / 4];
            var digestAddr = storage[HashDigestOffset / 4];
            var srcLen = storage[HashSrcLenOffset / 4];

            byte[] sourceData;

            if(sgMode)
            {
                sourceData = ReadScatterGather(srcAddr, accumMode);
            }
            else
            {
                sourceData = ReadFromBus(srcAddr, (int)srcLen);
            }

            if(sourceData == null || sourceData.Length == 0)
            {
                this.Log(LogLevel.Warning, "HACE: No source data to hash");
                SetHashComplete(cmd);
                return;
            }

            byte[] digest;

            if(accumMode)
            {
                digest = ExecuteAccumulation(algo.Value, sourceData, sgMode);
            }
            else
            {
                digest = ComputeHash(algo.Value, sourceData);
            }

            if(digest != null && digest.Length > 0)
            {
                WriteToBus(digestAddr, digest);
            }

            SetHashComplete(cmd);
        }

        private byte[] ExecuteAccumulation(HashAlgorithmName algo, byte[] data, bool sgMode)
        {
            // In accumulation mode, detect the final request by checking for
            // SHA padding (0x80 byte followed by zeros and 8-byte big-endian
            // bit length). QEMU checks padding in both SG and direct modes.
            bool isFinal = false;
            int dataLen = data.Length;

            if(accumulationCtx == null)
            {
                accumulationCtx = IncrementalHash.CreateHash(algo);
                totalReqLen = 0;
            }

            // Update total_req_len BEFORE padding check (matches QEMU behavior)
            totalReqLen += dataLen;

            if(dataLen >= 9)
            {
                // Read 8-byte big-endian bit-length from end of data
                ulong bitLen = 0;
                for(int i = dataLen - 8; i < dataLen; i++)
                {
                    bitLen = (bitLen << 8) | data[i];
                }
                ulong totalMsgLen = bitLen / 8;
                if(totalMsgLen <= (ulong)totalReqLen)
                {
                    uint paddingSize = (uint)((ulong)totalReqLen - totalMsgLen);
                    if(paddingSize > 0 && paddingSize <= (uint)dataLen)
                    {
                        int padOffset = dataLen - (int)paddingSize;
                        if(data[padOffset] == 0x80)
                        {
                            isFinal = true;
                            dataLen = padOffset;
                        }
                    }
                }
            }

            accumulationCtx.AppendData(data, 0, dataLen);

            if(isFinal)
            {
                var digest = accumulationCtx.GetHashAndReset();
                accumulationCtx.Dispose();
                accumulationCtx = null;
                totalReqLen = 0;
                return digest;
            }

            return null;
        }

        private static byte[] ComputeHash(HashAlgorithmName algo, byte[] data)
        {
            using(var hasher = IncrementalHash.CreateHash(algo))
            {
                hasher.AppendData(data);
                return hasher.GetHashAndReset();
            }
        }

        private byte[] ReadScatterGather(uint sgListAddr, bool accumMode)
        {
            var result = new System.IO.MemoryStream();
            const int maxEntries = 256;

            for(int i = 0; i < maxEntries; i++)
            {
                uint lenWord = ReadUInt32FromBus(sgListAddr + (uint)(i * 8));
                uint addrWord = ReadUInt32FromBus(sgListAddr + (uint)(i * 8) + 4);

                uint dataLen = lenWord & SgListLenMask;
                uint dataAddr = addrWord & SgListAddrMask;
                bool isLast = (lenWord & SgListLastBit) != 0;

                if(dataLen > 0)
                {
                    var chunk = ReadFromBus(dataAddr, (int)dataLen);
                    if(chunk != null)
                    {
                        result.Write(chunk, 0, chunk.Length);
                    }
                }

                if(isLast)
                {
                    break;
                }
            }

            return result.ToArray();
        }

        private byte[] ReadFromBus(uint address, int length)
        {
            if(length <= 0 || length > MaxDmaLength)
            {
                return new byte[0];
            }

            var data = new byte[length];
            var sysbus = machine.SystemBus;
            for(int i = 0; i < length; i++)
            {
                data[i] = sysbus.ReadByte(address + (uint)i);
            }
            return data;
        }

        private uint ReadUInt32FromBus(uint address)
        {
            var sysbus = machine.SystemBus;
            return (uint)(sysbus.ReadByte(address)
                | (sysbus.ReadByte(address + 1) << 8)
                | (sysbus.ReadByte(address + 2) << 16)
                | (sysbus.ReadByte(address + 3) << 24));
        }

        private void WriteToBus(uint address, byte[] data)
        {
            var sysbus = machine.SystemBus;
            for(int i = 0; i < data.Length; i++)
            {
                sysbus.WriteByte(address + (uint)i, data[i]);
            }
        }

        private void SetHashComplete(uint cmd)
        {
            storage[StatusOffset / 4] |= HashIrqBit;

            if((cmd & HashIrqEnBit) != 0)
            {
                IRQ.Set(true);
            }
        }

        private static HashAlgorithmName? GetHashAlgorithm(uint cmd)
        {
            // Bits [6:4] encode the algorithm:
            //   0b000 (0) = MD5       → cmd & 0x070 = 0x000
            //   0b010 (2) = SHA1      → cmd & 0x070 = 0x020
            //   0b100 (4) = SHA224    → cmd & 0x070 = 0x040
            //   0b101 (5) = SHA256    → cmd & 0x070 = 0x050
            //   0b110 (6) = SHA512 series → cmd & 0x070 = 0x060
            uint algoBits = (cmd >> 4) & 0x7;

            switch(algoBits)
            {
                case 0: // MD5
                    return HashAlgorithmName.MD5;
                case 2: // SHA1
                    return HashAlgorithmName.SHA1;
                case 4: // SHA224 — .NET lacks native SHA224, use SHA256
                    return HashAlgorithmName.SHA256;
                case 5: // SHA256
                    return HashAlgorithmName.SHA256;
                case 6: // SHA512 series — bits [12:10] select variant
                    uint sha512Variant = (cmd >> 10) & 0x7;
                    switch(sha512Variant)
                    {
                        case 0: return HashAlgorithmName.SHA512;
                        case 1: return HashAlgorithmName.SHA384;
                        case 2: return HashAlgorithmName.SHA256;
                        case 3: return HashAlgorithmName.SHA256; // SHA224 truncated
                        default: return (HashAlgorithmName?)null;
                    }
                default:
                    return (HashAlgorithmName?)null;
            }
        }

        private readonly IMachine machine;
        private readonly uint[] storage;
        private IncrementalHash accumulationCtx;
        private int totalReqLen;

        private const int RegisterSpaceSize = 0x1000;
        private const int MaxDmaLength = 0x10000000; // 256 MB

        // Register offsets
        private const long CryptCmdOffset  = 0x10;
        private const long StatusOffset    = 0x1C;
        private const long HashSrcOffset   = 0x20;
        private const long HashDigestOffset = 0x24;
        private const long HashKeyBuffOffset = 0x28;
        private const long HashSrcLenOffset = 0x2C;
        private const long HashCmdOffset   = 0x30;

        // Bit definitions
        private const uint HashIrqBit   = 1u << 9;
        private const uint HashIrqEnBit = 1u << 9;
        private const uint SgEnBit      = 1u << 18;

        // AST2600 masks
        private const uint SrcMask     = 0xFFFFFFFF;
        private const uint DestMask    = 0xFFFFFFF8;
        private const uint KeyMask     = 0xFFFFFFF8;
        private const uint LenMask     = 0x0FFFFFFF;
        private const uint HashCmdMask = 0x00147FFF;

        // SG list parsing
        private const uint SgListLenMask  = 0x0FFFFFFF;
        private const uint SgListAddrMask = 0xFFFFFFFF;
        private const uint SgListLastBit  = 0x80000000;
    }
}
