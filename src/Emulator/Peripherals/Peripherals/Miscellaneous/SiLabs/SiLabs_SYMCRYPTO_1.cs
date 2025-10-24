//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Reflection;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.DMA;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class SiLabs_SYMCRYPTO_1 : SiLabsPeripheral
    {
        public SiLabs_SYMCRYPTO_1(Machine machine, ISiLabs_KeyStorage ksu = null, TransferType fetcherTransferWidth = TransferType.Byte, TransferType pusherTransferWidth = TransferType.Byte, ICPU cpu = null) : base(machine)
        {
            this.ksu = ksu;
            this.fetcherTransferWidth = fetcherTransferWidth;
            this.pusherTransferWidth = pusherTransferWidth;
            this.cpu = cpu;
            IRQ = new GPIO();
            IRQ2 = new GPIO();
        }

        public override void Reset()
        {
            base.Reset();
        }

        public GPIO IRQ { get; }

        public GPIO IRQ2 { get; }

        protected override void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var irq = ((fetcherEndOfBlockInterruptEnable.Value && fetcherEndOfBlockInterrupt.Value)
                           || (fetcherStoppedInterruptEnable.Value && fetcherStoppedInterrupt.Value)
                           || (fetcherErrorInterruptEnable.Value && fetcherErrorInterrupt.Value)
                           || (pusherEndOfBlockInterruptEnable.Value && pusherEndOfBlockInterrupt.Value)
                           || (pusherStoppedInterruptEnable.Value && pusherStoppedInterrupt.Value)
                           || (pusherErrorInterruptEnable.Value && pusherErrorInterrupt.Value));
                IRQ.Set(irq);
                IRQ2.Set(irq);
            });
        }

        protected override DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.FetcherAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out fetcherAddress, name: "ADDR")
                },
                {(long)Registers.FetcherLength, new DoubleWordRegister(this)
                    .WithValueField(0, 28, out fetcherLength, name: "LENGTH")
                    .WithFlag(28, out fetcherConstantAddress, name: "CONSTADDR")
                    .WithFlag(29, out fetcherRealignLength, name: "REALIGN")
                    .WithFlag(30, out fetcherZeroPadding, name: "ZPADDING")
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.FetcherTag, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out fetcherTag, name: "TAG")
                },
                {(long)Registers.PusherAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out pusherAddress, name: "ADDR")
                },
                {(long)Registers.PusherLength, new DoubleWordRegister(this)
                    .WithValueField(0, 28, out pusherLength, name: "LENGTH")
                    .WithFlag(28, out pusherConstantAddress, name: "CONSTADDR")
                    .WithFlag(29, out pusherRealignLength, name: "REALIGN")
                    .WithFlag(30, out pusherDiscardData, name: "DISCARD")
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out fetcherEndOfBlockInterruptEnable, name: "FETCHERENDOFBLOCKIEN")
                    .WithFlag(1, out fetcherStoppedInterruptEnable, name: "FETCHERSTOPPEDIEN")
                    .WithFlag(2, out fetcherErrorInterruptEnable, name: "FETCHERERRORIEN")
                    .WithFlag(3, out pusherEndOfBlockInterruptEnable, name: "PUSHERENDOFBLOCKIEN")
                    .WithFlag(4, out pusherStoppedInterruptEnable, name: "PUSHERSTOPPEDIEN")
                    .WithFlag(5, out pusherErrorInterruptEnable, name: "PUSHERERRORIEN")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnableSet, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherEndOfBlockInterruptEnable.Value = true; }, name: "FETCHERENDOFBLOCKIENSET")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherStoppedInterruptEnable.Value = true; }, name: "FETCHERSTOPPEDIENSET")
                    .WithFlag(2, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherErrorInterruptEnable.Value = true; }, name: "FETCHERERRORIENSET")
                    .WithFlag(3, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherEndOfBlockInterruptEnable.Value = true; }, name: "PUSHERENDOFBLOCKIENSET")
                    .WithFlag(4, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherStoppedInterruptEnable.Value = true; }, name: "PUSHERSTOPPEDIENSET")
                    .WithFlag(5, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherErrorInterruptEnable.Value = true; }, name: "PUSHERERRORIENSET")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnableClear, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherEndOfBlockInterruptEnable.Value = false; }, name: "FETCHERENDOFBLOCKIENCLR")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherStoppedInterruptEnable.Value = false; }, name: "FETCHERSTOPPEDIENCLR")
                    .WithFlag(2, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherErrorInterruptEnable.Value = false; }, name: "FETCHERERRORIENCLR")
                    .WithFlag(3, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherEndOfBlockInterruptEnable.Value = false; }, name: "PUSHERENDOFBLOCKIENCLR")
                    .WithFlag(4, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherStoppedInterruptEnable.Value = false; }, name: "PUSHERSTOPPEDIENCLR")
                    .WithFlag(5, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherErrorInterruptEnable.Value = false; }, name: "PUSHERERRORIENCLR")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptFlag, new DoubleWordRegister(this)
                    .WithFlag(0, out fetcherEndOfBlockInterrupt, FieldMode.Read, name: "FETCHERENDOFBLOCKIF")
                    .WithFlag(1, out fetcherStoppedInterrupt, FieldMode.Read, name: "FETCHERSTOPPEDIF")
                    .WithFlag(2, out fetcherErrorInterrupt, FieldMode.Read, name: "FETCHERERRORIF")
                    .WithFlag(3, out pusherEndOfBlockInterrupt, FieldMode.Read, name: "PUSHERENDOFBLOCKIF")
                    .WithFlag(4, out pusherStoppedInterrupt, FieldMode.Read, name: "PUSHERSTOPPEDIF")
                    .WithFlag(5, out pusherErrorInterrupt, FieldMode.Read, name: "PUSHERERRORIF")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptFlagMasked, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => (fetcherEndOfBlockInterrupt.Value && fetcherEndOfBlockInterruptEnable.Value), name: "FETCHERENDOFBLOCKIF")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => (fetcherStoppedInterrupt.Value && fetcherStoppedInterruptEnable.Value), name: "FETCHERSTOPPEDIF")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => (fetcherErrorInterrupt.Value && fetcherErrorInterruptEnable.Value), name: "FETCHERERRORIF")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => (pusherEndOfBlockInterrupt.Value && pusherEndOfBlockInterruptEnable.Value), name: "PUSHERENDOFBLOCKIF")
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => (pusherStoppedInterrupt.Value && pusherStoppedInterruptEnable.Value), name: "PUSHERSTOPPEDIF")
                    .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => (pusherErrorInterrupt.Value && pusherErrorInterruptEnable.Value), name: "PUSHERERRORIF")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptFlagClear, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherEndOfBlockInterrupt.Value = false; }, name: "FETCHERENDOFBLOCKIFCLR")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherStoppedInterrupt.Value = false; }, name: "FETCHERSTOPPEDIFCLR")
                    .WithFlag(2, FieldMode.Write, writeCallback: (_, value) => { if (value) fetcherErrorInterrupt.Value = false; }, name: "FETCHERERRORIFCLR")
                    .WithFlag(3, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherEndOfBlockInterrupt.Value = false; }, name: "PUSHERENDOFBLOCKIFCLR")
                    .WithFlag(4, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherStoppedInterrupt.Value = false; }, name: "PUSHERSTOPPEDIFCLR")
                    .WithFlag(5, FieldMode.Write, writeCallback: (_, value) => { if (value) pusherErrorInterrupt.Value = false; }, name: "PUSHERERRORIFCLR")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, out fetcherScatterGather, name: "FETCHERSCATTERGATHER")
                    .WithFlag(1, out pusherScatterGather, name: "PUSHERSCATTERGATHER")
                    .WithFlag(2, out fetcherStopAtEndOfBlock, name: "STOPFETCHER")
                    .WithFlag(3, out pusherStopAtEndOfBlock, name: "STOPPUSHER")
                    .WithFlag(4, name: "SWRESET")
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) { StartFetcher(); } }, name: "STARTFETCHER")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) { StartPusher(); } }, name: "STARTPUSHER")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, out fetcherBusy, FieldMode.Read, name: "FETCHERBSY")
                    .WithFlag(1, out pusherBusy, FieldMode.Read, name: "PUSHERBSY")
                    .WithReservedBits(2, 2)
                    .WithFlag(4, name: "NOTEMPTY")
                    .WithFlag(5, name: "WAITING")
                    .WithFlag(6, name: "SOFTRSTBSY")
                    .WithReservedBits(7, 9)
                    .WithValueField(16, 16, name: "FIFODATANUM")
                    //.WithValueField(16, 16, FieldMode.Read, valueProviderCallback: _ => /* TODO */ 0, name: "FIFODATANUM")
                },
                {(long)Registers.IncludeIpsHardwareConfig, new DoubleWordRegister(this, 0x00000011)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "AES")
                    .WithFlag(1, name: "AESGCM")
                    .WithFlag(2, name: "AESXTS")
                    .WithFlag(3, name: "DES")
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => true, name: "HASH")
                    .WithFlag(5, name: "ChaChaPoly")
                    .WithFlag(6, name: "SHA3")
                    .WithFlag(7, name: "ZUC")
                    .WithFlag(8, name: "SM4")
                    .WithFlag(9, name: "PKE")
                    .WithFlag(10, name: "NDRNG")
                    .WithFlag(11, name: "HPChaChaPoly")
                    .WithFlag(12, name: "Snow3G")
                    .WithFlag(13, name: "Kasumi")
                    .WithFlag(14, name: "Aria")
                    .WithReservedBits(15, 17)
                },
                {(long)Registers.Ba411eHardwareConfig1, new DoubleWordRegister(this, 0x0F03017F)
                    .WithValueField(0, 9, FieldMode.Read, valueProviderCallback: _ => 0x17F, name: "AesModesPoss")
                    .WithReservedBits(9, 7)
                    .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => true, name: "CS")
                    .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => true, name: "UseMasking")
                    .WithReservedBits(18, 6)
                    .WithValueField(24, 3, FieldMode.Read, valueProviderCallback: _ => 0x7, name: "KeySize")
                    .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => true, name: "CxSwitch")
                    .WithFlag(28, FieldMode.Read, valueProviderCallback: _ => false, name: "GlitchProtection")
                    .WithReservedBits(29, 3)
                },
                {(long)Registers.Ba413HardwareConfig, new DoubleWordRegister(this, 0x0013007F)
                    .WithValueField(0, 7, FieldMode.Read, valueProviderCallback: _ => 0x7F, name: "HashMaskFunc")
                    .WithReservedBits(7, 9)
                    .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => true, name: "HashPadding")
                    .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => true, name: "HMAC_enabled")
                    .WithFlag(18, FieldMode.Read, valueProviderCallback: _ => false, name: "HashVerifyDigest")
                    .WithReservedBits(19, 1)
                    .WithValueField(20, 4, FieldMode.Read, valueProviderCallback: _ => 0x1, name: "Ext_nb_Hash_keys")
                    .WithValueField(24, 4, FieldMode.Read, valueProviderCallback: _ => 0x0, name: "IKG_nv_Hash_keys")
                    .WithReservedBits(28, 4)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override Type RegistersType => typeof(Registers);

        private static byte CcmGetQ(int nonceLength)
        {
            return (byte)(CcmBlockSize - 1 - nonceLength);
        }

        private static void CcmIncrementCounter(byte[] counterBlock)
        {
            for(int i = CcmBlockSize - 1; i >= 0; i--)
            {
                if(++counterBlock[i] != 0)
                {
                    break;
                }
            }
        }

        private static void CcmMacBlock(KeyParameter keyParameter, byte[] macState, byte[] block)
        {
            var mac = new CbcBlockCipher(new AesEngine());
            mac.Init(true, new ParametersWithIV(keyParameter, macState));
            byte[] macOut = new byte[CcmBlockSize];
            mac.ProcessBlock(block, 0, macOut, 0);
            Array.Copy(macOut, 0, macState, 0, CcmBlockSize);
        }

        private static void CcmMacRaw(KeyParameter keyParameter, byte[] macState, byte[] data)
        {
            int offset = 0;
            while(offset < data.Length)
            {
                byte[] block = new byte[CcmBlockSize];
                int chunk = Math.Min(data.Length - offset, CcmBlockSize);
                Array.Copy(data, offset, block, 0, chunk);
                CcmMacBlock(keyParameter, macState, block);
                offset += chunk;
            }
        }

        private static void CcmCtrBlock(KeyParameter keyParameter, byte[] counterBlock, byte[] input, byte[] output, int length)
        {
            var ctr = new SicBlockCipher(new AesEngine());
            ctr.Init(true, new ParametersWithIV(keyParameter, counterBlock));

            byte[] paddedIn = new byte[CcmBlockSize];
            byte[] paddedOut = new byte[CcmBlockSize];
            Array.Copy(input, 0, paddedIn, 0, length);
            ctr.ProcessBlock(paddedIn, 0, paddedOut, 0);
            Array.Copy(paddedOut, 0, output, 0, length);

            CcmIncrementCounter(counterBlock);
        }

        // First 16 header bytes are B0 (already parsed). MAC B0, MAC any remaining header as AAD,
        // and seed counterBlock to A1.
        private static void CcmStartFromB0(KeyParameter keyParameter, byte[] headerData, byte[] nonce, byte[] macState, byte[] counterBlock)
        {
            byte[] b0 = new byte[CcmBlockSize];
            Array.Copy(headerData, 0, b0, 0, CcmBlockSize);
            Array.Clear(macState, 0, CcmBlockSize);
            CcmMacBlock(keyParameter, macState, b0);

            if(headerData.Length > CcmBlockSize)
            {
                byte[] remaining = new byte[headerData.Length - CcmBlockSize];
                Array.Copy(headerData, CcmBlockSize, remaining, 0, remaining.Length);
                CcmMacRaw(keyParameter, macState, remaining);
            }

            byte q = CcmGetQ(nonce.Length);
            counterBlock[0] = (byte)(q - 1);
            Array.Copy(nonce, 0, counterBlock, 1, nonce.Length);
            Array.Clear(counterBlock, 1 + nonce.Length, CcmBlockSize - 1 - nonce.Length);
            CcmIncrementCounter(counterBlock);
        }

        private static void CcmProcessBlocks(bool encrypt, KeyParameter keyParameter, byte[] macState, byte[] counterBlock, byte[] input, uint length, byte[] output)
        {
            for(int offset = 0; offset < length; offset += CcmBlockSize)
            {
                byte[] plainBlock = new byte[CcmBlockSize];
                byte[] cipherBlock = new byte[CcmBlockSize];
                if(encrypt)
                {
                    Array.Copy(input, offset, plainBlock, 0, CcmBlockSize);
                    CcmMacBlock(keyParameter, macState, plainBlock);
                    CcmCtrBlock(keyParameter, counterBlock, plainBlock, cipherBlock, CcmBlockSize);
                    Array.Copy(cipherBlock, 0, output, offset, CcmBlockSize);
                }
                else
                {
                    Array.Copy(input, offset, cipherBlock, 0, CcmBlockSize);
                    CcmCtrBlock(keyParameter, counterBlock, cipherBlock, plainBlock, CcmBlockSize);
                    CcmMacBlock(keyParameter, macState, plainBlock);
                    Array.Copy(plainBlock, 0, output, offset, CcmBlockSize);
                }
            }
        }

        private static void CcmFinish(bool encrypt, KeyParameter keyParameter, byte[] macState, byte[] counterBlock, byte[] finalData, byte[] outputData, uint tagLength, byte[] tag)
        {
            int finalLength = finalData.Length;
            if(finalLength > 0)
            {
                byte[] paddedPlain = new byte[CcmBlockSize];
                byte[] paddedCipher = new byte[CcmBlockSize];
                if(encrypt)
                {
                    Array.Copy(finalData, 0, paddedPlain, 0, finalLength);
                    CcmMacBlock(keyParameter, macState, paddedPlain);
                    CcmCtrBlock(keyParameter, counterBlock, paddedPlain, paddedCipher, finalLength);
                    Array.Copy(paddedCipher, 0, outputData, 0, finalLength);
                }
                else
                {
                    Array.Copy(finalData, 0, paddedCipher, 0, finalLength);
                    CcmCtrBlock(keyParameter, counterBlock, paddedCipher, paddedPlain, finalLength);
                    CcmMacBlock(keyParameter, macState, paddedPlain);
                    Array.Copy(paddedPlain, 0, outputData, 0, finalLength);
                }
            }

            byte[] a0 = new byte[CcmBlockSize];
            Array.Copy(counterBlock, 0, a0, 0, CcmBlockSize);
            byte q = (byte)((a0[0] & 0x7) + 1);
            Array.Clear(a0, CcmBlockSize - q, q);
            a0[0] &= 0x07;

            var aes = new AesEngine();
            aes.Init(true, keyParameter);
            byte[] s0 = new byte[CcmBlockSize];
            aes.ProcessBlock(a0, 0, s0, 0);

            for(int i = 0; i < tagLength; i++)
            {
                tag[i] = (byte)(macState[i] ^ s0[i]);
            }
        }

        private static void CcmPackContext(byte[] macState, byte[] counterBlock, int tagLength, byte[] context)
        {
            Array.Copy(macState, 0, context, 0, CcmBlockSize);
            Array.Copy(counterBlock, 0, context, CcmBlockSize, CcmBlockSize);
            // SYMCRYPTO resume does not re-send B0; stash tagLength in unused A[0] bits 7:3.
            context[CcmBlockSize] = (byte)((counterBlock[0] & 0x07) | ((tagLength & 0x1F) << 3));
        }

        private static void CcmUnpackContext(byte[] context, byte[] macState, byte[] counterBlock, out int tagLength)
        {
            Array.Copy(context, 0, macState, 0, CcmBlockSize);
            Array.Copy(context, CcmBlockSize, counterBlock, 0, CcmBlockSize);
            tagLength = (counterBlock[0] >> 3) & 0x1F;
            counterBlock[0] &= 0x07;
        }

        private static uint HashEngineStateSize(HashMode hashMode)
        {
            switch(hashMode)
            {
            case HashMode.Sha_1:
                return 20;
            case HashMode.Sha_224:
            case HashMode.Sha_256:
                return 32;
            case HashMode.Sha_384:
            case HashMode.Sha_512:
                return 64;
            }
            return 0;
        }

        private static int ShaHWordCount(HashMode hashMode)
        {
            switch(hashMode)
            {
            case HashMode.Sha_1:
                return 5;
            case HashMode.Sha_224:
            case HashMode.Sha_256:
            case HashMode.Sha_384:
            case HashMode.Sha_512:
                return 8;
            }
            return 0;
        }

        private static bool ShaIsLongDigest(HashMode hashMode)
        {
            return hashMode == HashMode.Sha_384 || hashMode == HashMode.Sha_512;
        }

        private static FieldInfo LookupDigestField(IDigest digest, string name)
        {
            for(var t = digest.GetType(); t != null; t = t.BaseType)
            {
                var fi = t.GetField(name, PrivateInstanceField);
                if(fi != null)
                {
                    return fi;
                }
            }
            return null;
        }

        private IDigest CreateHashEngine(HashMode hashMode)
        {
            IDigest ret = null;
            switch(hashMode)
            {
            case HashMode.Md5:
                ret = new MD5Digest();
                break;
            case HashMode.Sha_1:
                ret = new Sha1Digest();
                break;
            case HashMode.Sha_224:
                ret = new Sha224Digest();
                break;
            case HashMode.Sha_256:
                ret = new Sha256Digest();
                break;
            case HashMode.Sha_384:
                ret = new Sha384Digest();
                break;
            case HashMode.Sha_512:
                ret = new Sha512Digest();
                break;
            case HashMode.Sm3:
                ret = new SM3Digest();
                break;
            default:
                this.Log(LogLevel.Error, "CreateHashEngine(): invalid hash mode");
                break;
            }
            return ret;
        }

        private bool SetDigestRawState(IDigest digest, HashMode hashMode, byte[] state)
        {
            int count = ShaHWordCount(hashMode);
            if(count == 0)
            {
                this.Log(LogLevel.Error, "SetDigestRawState(): unsupported hash mode {0}", hashMode);
                return false;
            }
            bool isLong = ShaIsLongDigest(hashMode);
            int expectedLength = isLong ? count * 8 : count * 4;
            if(state == null || state.Length != expectedLength)
            {
                this.Log(LogLevel.Error, "SetDigestRawState(): expected {0} bytes, got {1}", expectedLength, state == null ? 0 : state.Length);
                return false;
            }

            for(int i = 0; i < count; i++)
            {
                var fi = LookupDigestField(digest, "H" + (i + 1));
                if(fi == null)
                {
                    this.Log(LogLevel.Error, "SetDigestRawState(): missing H{0} on {1}", i + 1, digest.GetType().Name);
                    return false;
                }
                if(isLong)
                {
                    int off = i * 8;
                    ulong v = ((ulong)state[off    ] << 56)
                            | ((ulong)state[off + 1] << 48)
                            | ((ulong)state[off + 2] << 40)
                            | ((ulong)state[off + 3] << 32)
                            | ((ulong)state[off + 4] << 24)
                            | ((ulong)state[off + 5] << 16)
                            | ((ulong)state[off + 6] << 8)
                            |  (ulong)state[off + 7];
                    fi.SetValue(digest, v);
                }
                else
                {
                    int off = i * 4;
                    uint v = ((uint)state[off    ] << 24)
                           | ((uint)state[off + 1] << 16)
                           | ((uint)state[off + 2] << 8)
                           |  (uint)state[off + 3];
                    fi.SetValue(digest, v);
                }
            }
            return true;
        }

        private byte[] GetDigestRawState(IDigest digest, HashMode hashMode)
        {
            int count = ShaHWordCount(hashMode);
            if(count == 0)
            {
                this.Log(LogLevel.Error, "GetDigestRawState(): unsupported hash mode {0}", hashMode);
                return null;
            }
            bool isLong = ShaIsLongDigest(hashMode);
            byte[] state = new byte[isLong ? count * 8 : count * 4];

            for(int i = 0; i < count; i++)
            {
                var fi = LookupDigestField(digest, "H" + (i + 1));
                if(fi == null)
                {
                    this.Log(LogLevel.Error, "GetDigestRawState(): missing H{0} on {1}", i + 1, digest.GetType().Name);
                    return null;
                }
                if(isLong)
                {
                    ulong v = (ulong)fi.GetValue(digest);
                    int off = i * 8;
                    state[off] = (byte)(v >> 56);
                    state[off + 1] = (byte)(v >> 48);
                    state[off + 2] = (byte)(v >> 40);
                    state[off + 3] = (byte)(v >> 32);
                    state[off + 4] = (byte)(v >> 24);
                    state[off + 5] = (byte)(v >> 16);
                    state[off + 6] = (byte)(v >> 8);
                    state[off + 7] = (byte)v;
                }
                else
                {
                    uint v = (uint)fi.GetValue(digest);
                    int off = i * 4;
                    state[off] = (byte)(v >> 24);
                    state[off + 1] = (byte)(v >> 16);
                    state[off + 2] = (byte)(v >> 8);
                    state[off + 3] = (byte)v;
                }
            }
            return state;
        }

        // Commmands
        private void StartFetcher()
        {
            fetcherBusy.Value = true;
            pusherBusy.Value = true;

            // Scatter/Gather mode
            if(fetcherScatterGather.Value)
            {
                this.Log(LogLevel.Noisy, "***** FETCHER Descriptor(s): *****");
                var fetcherDescriptorList = ParseDescriptors(true);
                this.Log(LogLevel.Noisy, "***** PUSHER Descriptor(s): *****");
                var pusherDescriptorList = ParseDescriptors(false);

                switch(fetcherDescriptorList[0].EngineSelect)
                {
                case CryptoEngine.Aes:
                    RunAesEngine(fetcherDescriptorList, pusherDescriptorList);
                    break;
                case CryptoEngine.Bypass:
                    RunBypassTransfer(fetcherDescriptorList, pusherDescriptorList);
                    break;
                case CryptoEngine.Hash:
                    RunHashEngine(fetcherDescriptorList, pusherDescriptorList);
                    break;
                default:
                    this.Log(LogLevel.Error, "EngineSelect={0}", fetcherDescriptorList[0].EngineSelect);
                    throw new NotSupportedException("START_FETCHER: crypto engine not supported");
                }
            }
            else // Direct mode
            {
                // TODO: handle REALIGN flag.
                this.Log(LogLevel.Noisy, "Direct Mode: SrcAddress:0x{0:X} DestAddress=0x{1:X} length={2} zeroPad={3} discard={4}",
                         fetcherAddress.Value, pusherAddress.Value, fetcherLength.Value, fetcherZeroPadding.Value, pusherDiscardData.Value);

                if(pusherDiscardData.Value)
                {
                    // Discard: perform fetcher reads but don't write anything
                }
                else if(fetcherZeroPadding.Value)
                {
                    var sysbus = machine.GetSystemBus(this);
                    for(ulong i = 0; i < fetcherLength.Value; i++)
                    {
                        ulong destAddr = pusherAddress.Value + (pusherConstantAddress.Value ? 0 : i);
                        sysbus.WriteByte(destAddr, 0x00, cpu);
                    }
                }
                else
                {
                    DmaTransfer(fetcherAddress.Value, pusherAddress.Value, fetcherLength.Value);
                }
            }

            fetcherBusy.Value = false;
            pusherBusy.Value = false;

            pusherStoppedInterrupt.Value = true;
            UpdateInterrupts();
        }

        private void StartPusher()
        {
            // Nothing to do here, the StartFetcher command already grabs the pusher descriptors and writes them.
        }

        private bool IsSecureEngineCpu()
        {
            if(!machine.SystemBus.TryGetCurrentCPU(out var contextCpu))
            {
                return false;
            }
            return machine.TryGetLocalName(contextCpu, out var name) && name == SeCpuName;
        }

        private void RunAesEngine(List<DmaDescriptor> fetcherDescriptorList, List<DmaDescriptor> pusherDescriptorList)
        {
            int configDescriptorIndex = -1;
            int keyDescriptorIndex = -1;
            int key2DescriptorIndex = -1;
            int ivDescriptorIndex = -1;
            int iv2DescriptorIndex = -1;
            int maskDescriptorIndex = -1;
            var inputHeaderDescriptorIndices = new List<int>();
            var inputPayloadDescriptorIndices = new List<int>();

            for(int i = 0; i < fetcherDescriptorList.Count; i++)
            {
                if(fetcherDescriptorList[i].IsData)
                {
                    switch(fetcherDescriptorList[i].DataType)
                    {
                    case CryptoDataType.Header:
                        inputHeaderDescriptorIndices.Add(i);
                        break;
                    case CryptoDataType.Payload:
                        inputPayloadDescriptorIndices.Add(i);
                        break;
                    }
                }
                else if(fetcherDescriptorList[i].IsConfig)
                {
                    switch((AesParameterOffset)fetcherDescriptorList[i].OffsetStartAddress)
                    {
                    case AesParameterOffset.Config:
                        configDescriptorIndex = i;
                        break;
                    case AesParameterOffset.Key:
                        keyDescriptorIndex = i;
                        break;
                    case AesParameterOffset.IV:
                        ivDescriptorIndex = i;
                        break;
                    case AesParameterOffset.IV2:
                        iv2DescriptorIndex = i;
                        break;
                    case AesParameterOffset.Key2:
                        key2DescriptorIndex = i;
                        break;
                    case AesParameterOffset.Mask:
                        maskDescriptorIndex = i;
                        break;
                    default:
                        throw new ArgumentException($"Unknown AesParameterOffset: {fetcherDescriptorList[i].OffsetStartAddress}");
                    }
                }
            }

            if(maskDescriptorIndex >= 0 && (configDescriptorIndex < 0 || inputPayloadDescriptorIndices.Count == 0))
            {
                // This command is reseeding the LFSR. We can just ignore the whole thing
                this.Log(LogLevel.Noisy, "RunAesEngine(): only maskDescriptor present: Setting the LFSR, gracefully ignore the command");
                return;
            }

            if(configDescriptorIndex < 0)
            {
                throw new ArgumentException("RunAesEngine(): config descriptor missing");
            }

            int firstInputPayloadDescriptorIndex = (inputPayloadDescriptorIndices.Count > 0 ) ? inputPayloadDescriptorIndices[0] : -1;

            this.Log(LogLevel.Noisy, "RunAesEngine(): config_index={0} key_index={1} key2_index={2} iv_index={3} iv2_index={4} mask_index={5} FirstPayloadIndex={6}",
                     configDescriptorIndex, keyDescriptorIndex, key2DescriptorIndex, ivDescriptorIndex, iv2DescriptorIndex, maskDescriptorIndex, firstInputPayloadDescriptorIndex);

            // Bit 0: encryption or decryption operation
            // 0: encryption operation
            // 1: decryption operation
            bool encrypt = ((fetcherDescriptorList[configDescriptorIndex].Data[0] & 0x1) == 0);
            // Bit 4: Cx_Load:
            // 0: AES operation is initial; no context is given as input
            // 1: AES operation is not initial; the context must be provided as input
            bool cxLoad = ((fetcherDescriptorList[configDescriptorIndex].Data[0] & (0x1 << 4)) > 0);
            // Bit 5: Cx_Save:
            // 0: AES operation is final; the engine will not return the context
            // 1: AES operation is not final; the engine will return the context
            bool cxSave = ((fetcherDescriptorList[configDescriptorIndex].Data[0] & (0x1 << 5)) > 0);
            // Bits 7:6 and 31:28: KeySel
            // bits 7:6->1:0
            // bits 31:28->5:2
            uint keySelect = (uint)(((fetcherDescriptorList[configDescriptorIndex].Data[0] & 0xC0) >> 6)
                                    | ((fetcherDescriptorList[configDescriptorIndex].Data[3] & 0xF0) >> 2));
            // 16:8 Mode of Operation
            var mode = (AesMode)(fetcherDescriptorList[configDescriptorIndex].Data[1] | ((fetcherDescriptorList[configDescriptorIndex].Data[2] & 0x1) << 8));

            this.Log(LogLevel.Noisy, "RunAesEngine(): Config:[{0}]", BitConverter.ToString(fetcherDescriptorList[configDescriptorIndex].Data));
            this.Log(LogLevel.Noisy, "RunAesEngine(): encrypt={0} cxLoad={1} cxSave={2} keySel={3:X} AES mode: {4}", encrypt, cxLoad, cxSave, keySelect, mode);

            byte[] key;
            if(keyDescriptorIndex < 0)
            {
                if(IsSecureEngineCpu())
                {
                    if((keySelect & 0x1) == 0x1)
                    {
                        // Hardware key 1: SE crypto key (lower 256 bits of the 512-bit PUF RT output)
                        key = (byte[])seCryptoKey.Clone();
                        this.Log(LogLevel.Noisy, "RunAesEngine(): Using the SE crypto HW key");
                    }
                    else
                    {
                        throw new ArgumentException($"RunAesEngine(): key selector bit 0 is not set for SE CPU");
                    }
                }
                else
                {
                    if(keySelect == 0)
                    {
                        // Slot 0 is reserved for a key programmed directly by software, which is passed in a key descriptor.
                        throw new ArgumentException("RunAesEngine(): key descriptor missing and keySel is 0, which is reserved for a software programmed key");
                    }
                    else
                    {
                        if(ksu == null)
                        {
                            throw new ArgumentException($"RunAesEngine(): no KSU configured to fetch key slot {keySelect - 1} from");
                        }
                        // Key slots are passed in as 1-based indexes.
                        // KeySel == 0: Key programmed by the SW
                        // KeySel > 0: KSU slot index (1-based)
                        this.Log(LogLevel.Noisy, "RunAesEngine(): fetching key from KSU at index {0}", keySelect - 1);
                        key = ksu.GetKey(keySelect - 1);
                    }
                }
            }
            else
            {
                var keyDescriptor = fetcherDescriptorList[keyDescriptorIndex];
                // Extract key, accounting for InvalidBytesOrBits if set
                // Keys are typically word-aligned, so InvalidBytesOrBits may be set for alignment
                int keyLength = (int)(keyDescriptor.Length - keyDescriptor.InvalidBytesOrBits);
                keyLength = Math.Max(0, Math.Min(keyLength, keyDescriptor.Data.Length));
                key = new byte[keyLength];
                Array.Copy(keyDescriptor.Data, 0, key, 0, keyLength);
                this.Log(LogLevel.Noisy, "RunAesEngine(): Key descriptor - FirstDataAddress:0x{0:X}, Length:{1}, InvalidBytesOrBits:{2}, Extracted key length: {3}, key:[{4}]",
                    keyDescriptor.FirstDataAddress, keyDescriptor.Length, keyDescriptor.InvalidBytesOrBits, keyLength, BitConverter.ToString(key));
            }

            var keyParameter = new KeyParameter(key);
            this.Log(LogLevel.Noisy, "RunAesEngine(): key:[{0}]", BitConverter.ToString(key));

            ParametersWithIV parametersWithIV = null;
            if(ivDescriptorIndex >= 0)
            {
                parametersWithIV = new ParametersWithIV(keyParameter, fetcherDescriptorList[ivDescriptorIndex].Data);
                this.Log(LogLevel.Noisy, "RunAesEngine(): IV:[{0}]", BitConverter.ToString(fetcherDescriptorList[ivDescriptorIndex].Data));
            }

            int outputTextDescriptorIndex = -1;
            int outputIvDescriptorIndex = -1;

            switch(mode)
            {
            case AesMode.Ecb:
            case AesMode.Ctr:
            {
                IBlockCipher cipher = null;

                if(mode == AesMode.Ecb)
                {
                    cipher = new AesEngine();
                    // The output IV length is always 0 when using ECB
                    cipher.Init(encrypt, keyParameter);
                    // In ECB mode we expect a single pusher descriptor which stores the output text
                    outputTextDescriptorIndex = 0;
                }
                else // AesMode.Ctr
                {
                    cipher = new SicBlockCipher(new AesEngine());
                    cipher.Init(encrypt, parametersWithIV);
                    outputTextDescriptorIndex = 0;
                    outputIvDescriptorIndex = 1;
                }

                // TODO: process all payload descriptors (also take into account "invalid bytes" field)
                for(int i = 0; i < fetcherDescriptorList[firstInputPayloadDescriptorIndex].Length; i += 16)
                {
                    cipher.ProcessBlock(fetcherDescriptorList[firstInputPayloadDescriptorIndex].Data, i, pusherDescriptorList[outputTextDescriptorIndex].Data, i);
                }

                for(uint i = 0; i < pusherDescriptorList[outputTextDescriptorIndex].Length; i++)
                {
                    machine.SystemBus.WriteByte(pusherDescriptorList[outputTextDescriptorIndex].FirstDataAddress + i, pusherDescriptorList[outputTextDescriptorIndex].Data[i], cpu);
                }

                this.Log(LogLevel.Noisy, "RunAesEngine(): Output Data:[{0}]", BitConverter.ToString(pusherDescriptorList[outputTextDescriptorIndex].Data));

                // TODO: RENODE-51: The crypto mode classes do not expose the IV (for example the SicBlockCipher class).
                // We should write the output IV here, for now we just do a +1 on each byte of the input IV.
                if(cxSave)
                {
                    for(uint i = 0; i < pusherDescriptorList[outputIvDescriptorIndex].Length; i++)
                    {
                        pusherDescriptorList[outputIvDescriptorIndex].Data[i] = (byte)(fetcherDescriptorList[ivDescriptorIndex].Data[i] + 1);
                        machine.SystemBus.WriteByte(pusherDescriptorList[outputIvDescriptorIndex].FirstDataAddress + i, pusherDescriptorList[outputIvDescriptorIndex].Data[i], cpu);
                    }
                    this.Log(LogLevel.Noisy, "RunAesEngine(): Output IV:[{0}]", BitConverter.ToString(pusherDescriptorList[outputIvDescriptorIndex].Data));
                }
                break;
            }
            case AesMode.Cmac:
            {
                this.Log(LogLevel.Noisy, "RunAesEngine(): CMAC - Key:[{0}] (length={1})", BitConverter.ToString(key), key.Length);
                if(keyDescriptorIndex >= 0)
                {
                    var keyDescriptor = fetcherDescriptorList[keyDescriptorIndex];
                }
                var mac = new CMac(new AesEngine());
                mac.Init(keyParameter);
                outputTextDescriptorIndex = 0;

                // Process all payload descriptors, accounting for InvalidBytesOrBits
                // InvalidBytesOrBits is only applied when length is a multiple of 16 (same rule as GCM/GMAC)
                uint totalPayloadLength = 0;
                for(int i = 0; i < inputPayloadDescriptorIndices.Count; i++)
                {
                    int descIndex = inputPayloadDescriptorIndices[i];
                    var descriptor = fetcherDescriptorList[descIndex];
                    // InvalidBytesOrBits is only applied when length is a multiple of 16
                    int payloadLength = (int)(descriptor.Length - (descriptor.Length % 16 == 0 ? descriptor.InvalidBytesOrBits : 0));
                    payloadLength = Math.Max(0, Math.Min(payloadLength, descriptor.Data.Length));
                    this.Log(LogLevel.Noisy, "RunAesEngine(): CMAC payload descriptor {0} - Length:{1}, InvalidBytesOrBits:{2}, payloadLength:{3}, Data:[{4}]",
                        i, descriptor.Length, descriptor.InvalidBytesOrBits, payloadLength,
                        payloadLength > 0 ? BitConverter.ToString(descriptor.Data, 0, payloadLength) : "empty");
                    if(payloadLength > 0)
                    {
                        mac.BlockUpdate(descriptor.Data, 0, payloadLength);
                        totalPayloadLength += (uint)payloadLength;
                    }
                }
                this.Log(LogLevel.Noisy, "RunAesEngine(): CMAC - Total payload length: {0} bytes", totalPayloadLength);

                byte[] macResult = new byte[mac.GetMacSize()];
                mac.DoFinal(macResult, 0);

                this.Log(LogLevel.Noisy, "RunAesEngine(): CMAC Output Data:[{0}]", BitConverter.ToString(macResult));

                // Write the result to multiple pusher descriptors if needed
                uint outDataOffset = 0;
                for(var descIndex = 0; descIndex < pusherDescriptorList.Count; descIndex++)
                {
                    if(pusherDescriptorList[descIndex].Discard)
                    {
                        continue;
                    }

                    uint bytesToWrite = Math.Min((uint)macResult.Length - outDataOffset, pusherDescriptorList[descIndex].Length);
                    for(uint i = 0; i < bytesToWrite; i++)
                    {
                        machine.SystemBus.WriteByte(pusherDescriptorList[descIndex].FirstDataAddress + i, macResult[outDataOffset + i], cpu);
                    }
                    outDataOffset += bytesToWrite;
                    if(outDataOffset >= macResult.Length)
                    {
                        break;
                    }
                }
                break;
            }
            case AesMode.GcmGmac:
            {
                // Last data payload descriptor is a 16 byte descriptor containing len(A) || len(C)
                int lastFetcherIndex = fetcherDescriptorList.Count - 1;
                if(!fetcherDescriptorList[lastFetcherIndex].IsData || fetcherDescriptorList[lastFetcherIndex].DataType != CryptoDataType.Payload || fetcherDescriptorList[lastFetcherIndex].Data.Length != 16)
                {
                    throw new ArgumentException("RunAesEngine(): GCM/GMAC requires last descriptor len(A)||len(C)");
                }
                byte[] reversedLastDescriptorData = new byte[fetcherDescriptorList[lastFetcherIndex].Data.Length];
                Array.Copy(fetcherDescriptorList[lastFetcherIndex].Data, reversedLastDescriptorData, fetcherDescriptorList[lastFetcherIndex].Data.Length);
                Array.Reverse(reversedLastDescriptorData);

                int tagLength = 16;
                ulong aadLengthBits = BitConverter.ToUInt64(reversedLastDescriptorData, 8);
                ulong payloadLengthBits = BitConverter.ToUInt64(reversedLastDescriptorData, 0);
                int aadLength = (int)(aadLengthBits / 8);
                int payloadLength = (int)(payloadLengthBits / 8);

                // Collect AAD from all Header-type descriptors
                var aadDescriptorIndices = new List<int>();
                for(int i = 0; i < fetcherDescriptorList.Count; i++)
                {
                    if(fetcherDescriptorList[i].IsData && fetcherDescriptorList[i].DataType == CryptoDataType.Header)
                    {
                        aadDescriptorIndices.Add(i);
                    }
                }

                // Collect payload from all Payload-type descriptors (excluding the last one which is len(A)||len(C))
                var payloadDescriptorIndices = new List<int>();
                for(int i = 0; i < fetcherDescriptorList.Count - 1; i++)
                {
                    if(fetcherDescriptorList[i].IsData && fetcherDescriptorList[i].DataType == CryptoDataType.Payload)
                    {
                        payloadDescriptorIndices.Add(i);
                    }
                }

                // Build AAD array from all Header descriptors
                byte[] aad = new byte[aadLength];
                int aadOffset = 0;
                uint totalAadLength = 0;
                for(int i = 0; i < aadDescriptorIndices.Count && aadOffset < aadLength; i++)
                {
                    int descIndex = aadDescriptorIndices[i];
                    var descriptor = fetcherDescriptorList[descIndex];
                    // InvalidBytesOrBits is only applied when length is a multiple of 16
                    int descriptorAadLength = (int)(descriptor.Length - (descriptor.Length % 16 == 0 ? descriptor.InvalidBytesOrBits : 0));
                    descriptorAadLength = Math.Max(0, Math.Min(descriptorAadLength, descriptor.Data.Length));
                    int bytesToCopy = Math.Min(descriptorAadLength, aadLength - aadOffset);
                    Array.Copy(descriptor.Data, 0, aad, aadOffset, bytesToCopy);
                    aadOffset += bytesToCopy;
                    totalAadLength += (uint)descriptorAadLength;
                }

                if(totalAadLength != aadLength)
                {
                    throw new ArgumentException($"RunAesEngine(): GCM/GMAC AAD length mismatch: expected {aadLength}, got {totalAadLength}");
                }

                this.Log(LogLevel.Noisy, "RunAesEngine(): GCM/GMAC: aadLength={0} payloadLength={1} tagLength={2} aad=[{3}]", aadLength, payloadLength, tagLength, BitConverter.ToString(aad));

                var cipher = new GcmBlockCipher(new AesEngine());
                var aeadParameters = new AeadParameters(keyParameter, tagLength*8, fetcherDescriptorList[ivDescriptorIndex].Data, aad);
                cipher.Init(encrypt, aeadParameters);

                // Calculate total payload length first
                uint totalPayloadLength = 0;
                for(int i = 0; i < payloadDescriptorIndices.Count; i++)
                {
                    int descIndex = payloadDescriptorIndices[i];
                    var descriptor = fetcherDescriptorList[descIndex];
                    // InvalidBytesOrBits is only applied when length is a multiple of 16
                    int descriptorPayloadLength = (int)(descriptor.Length - (descriptor.Length % 16 == 0 ? descriptor.InvalidBytesOrBits : 0));
                    descriptorPayloadLength = Math.Max(0, Math.Min(descriptorPayloadLength, descriptor.Data.Length));
                    totalPayloadLength += (uint)descriptorPayloadLength;
                }

                if(totalPayloadLength != payloadLength)
                {
                    throw new ArgumentException($"RunAesEngine(): GCM/GMAC payload length mismatch: expected {payloadLength}, got {totalPayloadLength}");
                }

                // Allocate buffer - start with payloadLength, we'll check GetOutputSize before DoFinal
                // Process all payload descriptors first
                int outputOffset = 0;
                byte[] outputDataAndTag = new byte[payloadLength + tagLength]; // Allocate extra space for TAG

                for(int i = 0; i < payloadDescriptorIndices.Count && outputOffset < payloadLength; i++)
                {
                    int descIndex = payloadDescriptorIndices[i];
                    var descriptor = fetcherDescriptorList[descIndex];
                    // InvalidBytesOrBits is only applied when length is a multiple of 16
                    int descriptorPayloadLength = (int)(descriptor.Length - (descriptor.Length % 16 == 0 ? descriptor.InvalidBytesOrBits : 0));
                    descriptorPayloadLength = Math.Max(0, Math.Min(descriptorPayloadLength, descriptor.Data.Length));
                    int bytesToProcess = Math.Min(descriptorPayloadLength, payloadLength - outputOffset);
                    if(bytesToProcess > 0)
                    {
                        int processed = cipher.ProcessBytes(descriptor.Data, 0, bytesToProcess, outputDataAndTag, outputOffset);
                        outputOffset += processed;
                    }
                }

                // If decrypting, process as many extra zero bytes as the tag length.
                if(!encrypt)
                {
                    for(uint i = 0; i < tagLength; i++)
                    {
                        int processed = cipher.ProcessByte(0, outputDataAndTag, outputOffset);
                        outputOffset += processed;
                    }
                }

                // BA411E hardware does not verify the tag for GCM/GMAC mode.
                try
                {
                    int doFinalBytes = cipher.DoFinal(outputDataAndTag, outputOffset);
                    outputOffset += doFinalBytes;
                }
                catch(Exception e)
                {
                    if(!(e is InvalidCipherTextException))
                    {
                        throw e;
                    }
                }

                byte[] tag = cipher.GetMac();
                this.Log(LogLevel.Noisy, "RunAesEngine(): GCM/GMAC outputData length={0} outputOffset={1} payloadLength={2} outputDataAndTag=[{3}] tag=[{4}]",
                    outputDataAndTag.Length, outputOffset, payloadLength,
                    BitConverter.ToString(outputDataAndTag, 0, Math.Min(payloadLength, outputDataAndTag.Length)),
                    BitConverter.ToString(tag));

                // Write output data to multiple pusher descriptors (only write payloadLength bytes, not the entire buffer)
                var usedOutputDescriptors = new HashSet<int>();
                uint bytesWritten = 0;
                for(int descIndex = 0; descIndex < pusherDescriptorList.Count && bytesWritten < payloadLength; descIndex++)
                {
                    if(pusherDescriptorList[descIndex].Discard)
                    {
                        continue;
                    }
                    uint bytesToWrite = Math.Min((uint)payloadLength - bytesWritten, pusherDescriptorList[descIndex].Length);
                    for(uint i = 0; i < bytesToWrite; i++)
                    {
                        machine.SystemBus.WriteByte(pusherDescriptorList[descIndex].FirstDataAddress + i, outputDataAndTag[bytesWritten + i], cpu);
                    }
                    usedOutputDescriptors.Add(descIndex);
                    bytesWritten += bytesToWrite;
                }

                // Tag is stored in a pusher descriptor that does not have the "discard" bit set and hasn't been used for output data.
                // Find the first non-discarded descriptor that's not in usedOutputDescriptors
                // Prefer descriptors with length >= tagLength, but accept smaller ones if needed (4~16 bytes tag tag generation is supported)
                int tagDescriptorIndex = -1;
                int bestTagDescriptorIndex = -1;  // Best match (length >= tagLength)
                int fallbackTagDescriptorIndex = -1;  // Fallback (smaller than tagLength)

                // First, search from index 1 onwards (preferred)
                for(int i = 1; i < pusherDescriptorList.Count; i++)
                {
                    if(!pusherDescriptorList[i].Discard && !usedOutputDescriptors.Contains(i))
                    {
                        if(pusherDescriptorList[i].Length >= tagLength)
                        {
                            bestTagDescriptorIndex = i;
                            break;
                        }
                        else if(fallbackTagDescriptorIndex == -1)
                        {
                            // Keep first available descriptor as fallback (even if smaller this is supported by the AES specification)
                            fallbackTagDescriptorIndex = i;
                        }
                    }
                }

                // Use best match if found, otherwise use fallback
                if(bestTagDescriptorIndex != -1)
                {
                    tagDescriptorIndex = bestTagDescriptorIndex;
                }
                else if(fallbackTagDescriptorIndex != -1)
                {
                    tagDescriptorIndex = fallbackTagDescriptorIndex;
                }

                if(tagDescriptorIndex == -1)
                {
                    var discardedIndices = new List<int>();
                    for(int i = 0; i < pusherDescriptorList.Count; i++)
                    {
                        if(pusherDescriptorList[i].Discard)
                        {
                            discardedIndices.Add(i);
                        }
                    }
                    this.Log(LogLevel.Error, "RunAesEngine(): GCM/GMAC no available descriptor for tag. Total pusher descriptors: {0}, Used for output: [{1}], Discarded: [{2}]",
                        pusherDescriptorList.Count,
                        string.Join(",", usedOutputDescriptors),
                        string.Join(",", discardedIndices));
                    throw new ArgumentException("RunAesEngine(): GCM/GMAC no available descriptor for tag");
                }

                this.Log(LogLevel.Noisy, "RunAesEngine(): GCM/GMAC tag=[{0}] tagDescriptor={1}", BitConverter.ToString(tag), tagDescriptorIndex);
                if(tag.Length != tagLength)
                {
                    throw new ArgumentException("RunAesEngine(): GCM/GMAC tag length mismatch");
                }

                uint tagBytesToWrite = Math.Min((uint)tagLength, pusherDescriptorList[tagDescriptorIndex].Length);
                for(uint i = 0; i < tagBytesToWrite; i++)
                {
                    machine.SystemBus.WriteByte(pusherDescriptorList[tagDescriptorIndex].FirstDataAddress + i, tag[i], cpu);
                }
                break;
            }
            case AesMode.Ccm:
            {
                RunAesCcm(encrypt, cxLoad, cxSave, keyParameter, fetcherDescriptorList, pusherDescriptorList,
                          inputHeaderDescriptorIndices, inputPayloadDescriptorIndices, ivDescriptorIndex);
                break;
            }
            default:
            {
                throw new ArgumentException("RunAesEngine(): AES mode not supported");
            }
            }
        }

        private void RunAesCcm(bool encrypt, bool cxLoad, bool cxSave, KeyParameter keyParameter,
                               List<DmaDescriptor> fetcherDescriptorList, List<DmaDescriptor> pusherDescriptorList,
                               List<int> inputHeaderDescriptorIndices, List<int> inputPayloadDescriptorIndices,
                               int ivDescriptorIndex)
        {
            byte[] macState = new byte[CcmBlockSize];
            byte[] counterBlock = new byte[CcmBlockSize];
            int tagLength;

            byte[] headerData = BuildDataArray(inputHeaderDescriptorIndices, fetcherDescriptorList);
            if(headerData == null)
            {
                headerData = Array.Empty<byte>();
            }

            if(cxLoad)
            {
                if(ivDescriptorIndex < 0 || fetcherDescriptorList[ivDescriptorIndex].Data.Length < CcmContextSize)
                {
                    throw new ArgumentException("RunAesCcm(): Cx_Load requires a 32-byte IV/context descriptor");
                }
                CcmUnpackContext(fetcherDescriptorList[ivDescriptorIndex].Data, macState, counterBlock, out tagLength);
                this.Log(LogLevel.Noisy, "RunAesCcm(): Cx_Load tagLength={0} mac=[{1}] ctr=[{2}]",
                         tagLength, BitConverter.ToString(macState), BitConverter.ToString(counterBlock));
                if(headerData.Length > 0)
                {
                    CcmMacRaw(keyParameter, macState, headerData);
                }
            }
            else
            {
                if(headerData.Length < CcmBlockSize)
                {
                    throw new ArgumentException($"RunAesCcm(): CCM header data too short: {headerData.Length} bytes, expected at least 16");
                }

                byte flags = headerData[0];
                int l = (flags & 0x7) + 1;
                int nonceSize = 15 - l;
                int m = (flags >> 3) & 0x7;
                // Firmware CCM* encodes tagsz=0 as M=0; RFC 3610 maps M=0 to t=2. Prefer CCM*.
                tagLength = (m == 0) ? 0 : (m * 2 + 2);

                byte[] nonce = new byte[nonceSize];
                Array.Copy(headerData, 1, nonce, 0, nonceSize);

                uint messageLength = 0;
                for(int i = 0; i < l; i++)
                {
                    messageLength = (messageLength << 8) | headerData[1 + nonceSize + i];
                }

                this.Log(LogLevel.Noisy, "RunAesCcm(): flags=0x{0:X2} l={1} nonceSize={2} tagLength={3} msgLen={4} nonce=[{5}]",
                         flags, l, nonceSize, tagLength, messageLength, BitConverter.ToString(nonce));

                CcmStartFromB0(keyParameter, headerData, nonce, macState, counterBlock);
            }

            byte[] payload = BuildDataArray(inputPayloadDescriptorIndices, fetcherDescriptorList);
            if(payload == null)
            {
                payload = Array.Empty<byte>();
            }

            byte[] tagIn = Array.Empty<byte>();
            if(!encrypt && !cxSave && tagLength > 0)
            {
                if(payload.Length < tagLength)
                {
                    throw new ArgumentException($"RunAesCcm(): decrypt payload {payload.Length} shorter than tag {tagLength}");
                }
                tagIn = new byte[tagLength];
                Array.Copy(payload, payload.Length - tagLength, tagIn, 0, tagLength);
                byte[] ciphertext = new byte[payload.Length - tagLength];
                Array.Copy(payload, 0, ciphertext, 0, ciphertext.Length);
                payload = ciphertext;
            }

            this.Log(LogLevel.Noisy, "RunAesCcm(): encrypt={0} cxLoad={1} cxSave={2} payload={3} bytes [{4}]",
                     encrypt, cxLoad, cxSave, payload.Length, payload.Length > 0 ? BitConverter.ToString(payload) : "empty");

            if(cxSave && (payload.Length % CcmBlockSize) != 0)
            {
                throw new ArgumentException($"RunAesCcm(): Cx_Save payload length {payload.Length} is not a multiple of 16");
            }

            byte[] processed = new byte[payload.Length];
            uint fullBlocksLength = (uint)(payload.Length - (payload.Length % CcmBlockSize));
            if(fullBlocksLength > 0)
            {
                CcmProcessBlocks(encrypt, keyParameter, macState, counterBlock, payload, fullBlocksLength, processed);
            }

            byte[] tag = new byte[tagLength];
            if(!cxSave)
            {
                byte[] tail = Array.Empty<byte>();
                if(payload.Length > fullBlocksLength)
                {
                    tail = new byte[payload.Length - fullBlocksLength];
                    Array.Copy(payload, (int)fullBlocksLength, tail, 0, tail.Length);
                }
                byte[] tailOut = new byte[tail.Length];
                CcmFinish(encrypt, keyParameter, macState, counterBlock, tail, tailOut, (uint)tagLength, tag);
                if(tail.Length > 0)
                {
                    Array.Copy(tailOut, 0, processed, (int)fullBlocksLength, tail.Length);
                }

                if(!encrypt && tagLength > 0)
                {
                    byte[] tagXor = new byte[tagLength];
                    for(int i = 0; i < tagLength; i++)
                    {
                        tagXor[i] = (byte)(tagIn[i] ^ tag[i]);
                    }
                    tag = tagXor;
                }
            }

            var output = new List<byte>(processed);
            if(cxSave)
            {
                byte[] context = new byte[CcmContextSize];
                CcmPackContext(macState, counterBlock, tagLength, context);
                output.AddRange(context);
                this.Log(LogLevel.Noisy, "RunAesCcm(): Cx_Save context=[{0}]", BitConverter.ToString(context));
            }
            else if(tagLength > 0)
            {
                output.AddRange(tag);
                this.Log(LogLevel.Noisy, "RunAesCcm(): tag=[{0}]", BitConverter.ToString(tag));
            }

            byte[] finalOutput = output.ToArray();
            this.Log(LogLevel.Noisy, "RunAesCcm(): output ({0} bytes)=[{1}]", finalOutput.Length, BitConverter.ToString(finalOutput));
            WriteCcmPusherOutput(pusherDescriptorList, finalOutput);
        }

        private void RunHashEngine(List<DmaDescriptor> fetcherDescriptorList, List<DmaDescriptor> pusherDescriptorList)
        {
            int configDescriptorIndex = -1;

            for(int i = 0; i < fetcherDescriptorList.Count; i++)
            {
                if(fetcherDescriptorList[i].IsConfig)
                {
                    switch((HashParameterOffset)fetcherDescriptorList[i].OffsetStartAddress)
                    {
                    case HashParameterOffset.Config:
                        configDescriptorIndex = i;
                        break;
                    default:
                        throw new ArgumentException($"Unknown HashParameterOffset: {fetcherDescriptorList[i].OffsetStartAddress}");
                    }
                }
            }

            if(configDescriptorIndex < 0)
            {
                this.Log(LogLevel.Error, "RunHashEngine(): Missing config descriptor {0}", configDescriptorIndex);
                return;
            }

            this.Log(LogLevel.Noisy, "RunHashEngine(): config_index={0}", configDescriptorIndex);

            // Bit 6:0 Hash_mode
            var mode = (HashMode)((fetcherDescriptorList[configDescriptorIndex].Data[0] & 0x7F));
            // Bit 8: hmac
            // 0: Perform hash operation
            // 1: Perform hmac operation (requires Padding=1 and Hash_final=1)
            bool hmac = ((fetcherDescriptorList[configDescriptorIndex].Data[1] & 0x1) > 0);
            // Bit 9: Padding
            // 0: padding is done in software
            // 1: padding is done in hardware (requires Hash_final=1, and cannot be used when using initialization data)
            bool padding = ((fetcherDescriptorList[configDescriptorIndex].Data[1] & (0x1 << 1)) > 0);
            // Bit 10: Hash_final
            // 0: hash operation is update. Engine wil return the state
            // 1: hash operation is final. Engine will return the digest
            bool hashFinal = ((fetcherDescriptorList[configDescriptorIndex].Data[1] & (0x1 << 2)) > 0);
            // Bit 11: Verify
            // 0: Generate HASH
            // 1: verify the input reference hash instead of generating a new one
            bool verify = ((fetcherDescriptorList[configDescriptorIndex].Data[1] & (0x1 << 3)) > 0);

            this.Log(LogLevel.Noisy, "RunHashEngine(): mode={0}, hmac={1}, padding={2}, hashFinal={3}, verify={4}",
                     mode, hmac, padding, hashFinal, verify);

            if(verify)
            {
                this.Log(LogLevel.Error, "RunHashEngine(): Verify not supported");
                return;
            }

            if(hmac && padding && hashFinal)
            {
                // HMAC operation: requires Padding=1 and Hash_final=1
                // Find HMAC_Key descriptor
                int hmacKeyDescriptorIndex = -1;
                for(int i = 0; i < fetcherDescriptorList.Count; i++)
                {
                    if(fetcherDescriptorList[i].IsData && fetcherDescriptorList[i].DataType == CryptoDataType.HMAC_Key)
                    {
                        hmacKeyDescriptorIndex = i;
                        break;
                    }
                }

                if(hmacKeyDescriptorIndex < 0)
                {
                    this.Log(LogLevel.Error, "RunHashEngine(): HMAC requires HMAC_Key descriptor");
                    return;
                }

                // Extract HMAC key, accounting for InvalidBytesOrBits (always subtracted for HMAC keys)
                var keyDescriptor = fetcherDescriptorList[hmacKeyDescriptorIndex];
                int keyLength = (int)(keyDescriptor.Length - keyDescriptor.InvalidBytesOrBits);
                keyLength = Math.Max(0, Math.Min(keyLength, keyDescriptor.Data.Length));
                byte[] hmacKey = new byte[keyLength];
                if(keyLength > 0)
                {
                    Array.Copy(keyDescriptor.Data, 0, hmacKey, 0, keyLength);
                }
                this.Log(LogLevel.Noisy, "RunHashEngine(): HMAC key length={0}, key=[{1}]", keyLength, keyLength > 0 ? BitConverter.ToString(hmacKey) : "empty");

                // Create HMAC engine with underlying digest
                var digest = CreateHashEngine(mode);
                var hmacEngine = new HMac(digest);
                var keyParameter = new KeyParameter(hmacKey);
                hmacEngine.Init(keyParameter);

                // Process all Message-type descriptors
                uint totalMessageLength = 0;
                for(int i = 0; i < fetcherDescriptorList.Count; i++)
                {
                    if(fetcherDescriptorList[i].IsData && fetcherDescriptorList[i].DataType == CryptoDataType.Message)
                    {
                        var descriptor = fetcherDescriptorList[i];
                        // InvalidBytesOrBits is always subtracted for HMAC messages
                        int messageLength = (int)(descriptor.Length - descriptor.InvalidBytesOrBits);
                        messageLength = Math.Max(0, Math.Min(messageLength, descriptor.Data.Length));
                        this.Log(LogLevel.Noisy, "RunHashEngine(): HMAC message descriptor - Length:{0}, InvalidBytesOrBits:{1}, messageLength:{2}",
                            descriptor.Length, descriptor.InvalidBytesOrBits, messageLength);
                        if(messageLength > 0)
                        {
                            hmacEngine.BlockUpdate(descriptor.Data, 0, messageLength);
                            totalMessageLength += (uint)messageLength;
                        }
                    }
                }

                this.Log(LogLevel.Noisy, "RunHashEngine(): HMAC total message length: {0} bytes", totalMessageLength);

                // Generate HMAC
                byte[] hmacResult = new byte[hmacEngine.GetMacSize()];
                hmacEngine.DoFinal(hmacResult, 0);

                this.Log(LogLevel.Noisy, "RunHashEngine(): HMAC Output Data:[{0}]", BitConverter.ToString(hmacResult));

                // Write HMAC result to multiple pusher descriptors
                uint outDataOffset = 0;
                for(var descIndex = 0; descIndex < pusherDescriptorList.Count && outDataOffset < hmacResult.Length; descIndex++)
                {
                    if(pusherDescriptorList[descIndex].Discard)
                    {
                        continue;
                    }

                    uint bytesToWrite = Math.Min((uint)hmacResult.Length - outDataOffset, pusherDescriptorList[descIndex].Length);
                    for(uint i = 0; i < bytesToWrite; i++)
                    {
                        machine.SystemBus.WriteByte(pusherDescriptorList[descIndex].FirstDataAddress + i, hmacResult[outDataOffset + i], cpu);
                    }
                    outDataOffset += bytesToWrite;
                }
                return;
            }

            // Each RunHashEngine call is stateless: the firmware always supplies an
            // InitializationData descriptor whenever it wants to continue from a previously
            // saved digest state, and we save the raw H1..Hn state to the pusher buffer when
            // hashFinal==false.
            var digestEngine = CreateHashEngine(mode);
            if(digestEngine == null)
            {
                return;
            }

            int initDataIndex = -1;
            for(int i = 0; i < fetcherDescriptorList.Count; i++)
            {
                if(fetcherDescriptorList[i].IsData && fetcherDescriptorList[i].DataType == CryptoDataType.InitializationData)
                {
                    initDataIndex = i;
                    break;
                }
            }

            if(initDataIndex >= 0)
            {
                uint stateSize = HashEngineStateSize(mode);
                var stateDescriptor = fetcherDescriptorList[initDataIndex];
                if(stateSize == 0 || stateDescriptor.Data.Length < stateSize)
                {
                    this.Log(LogLevel.Error,
                             "RunHashEngine(): InitializationData length {0} < required state size {1} for mode {2}",
                             stateDescriptor.Data.Length, stateSize, mode);
                    return;
                }
                byte[] inputState = new byte[stateSize];
                Array.Copy(stateDescriptor.Data, 0, inputState, 0, (int)stateSize);
                this.Log(LogLevel.Noisy, "RunHashEngine(): InputState=[{0}]", BitConverter.ToString(inputState));
                if(!SetDigestRawState(digestEngine, mode, inputState))
                {
                    return;
                }
            }

            for(int inputIndex = 0; inputIndex < fetcherDescriptorList.Count; inputIndex++)
            {
                if(!fetcherDescriptorList[inputIndex].IsData)
                {
                    continue;
                }
                if(fetcherDescriptorList[inputIndex].DataType == CryptoDataType.InitializationData)
                {
                    continue;
                }
                var messageDescriptor = fetcherDescriptorList[inputIndex];
                int messageLength = (int)(messageDescriptor.Length - messageDescriptor.InvalidBytesOrBits);
                if(messageLength < 0)
                {
                    messageLength = 0;
                }
                if(messageLength > messageDescriptor.Data.Length)
                {
                    messageLength = messageDescriptor.Data.Length;
                }
                digestEngine.BlockUpdate(messageDescriptor.Data, 0, messageLength);
            }

            if(hashFinal)
            {
                if(padding)
                {
                    // Hardware-padded final: BouncyCastle handles SHA padding for us.
                    byte[] outData = new byte[digestEngine.GetDigestSize()];
                    digestEngine.DoFinal(outData, 0);
                    this.Log(LogLevel.Noisy, "RunHashEngine(): Output Data:[{0}]", BitConverter.ToString(outData));
                    WriteHashOutput(pusherDescriptorList, outData);
                }
                else
                {
                    // Software-padded final: the firmware appended SHA padding to the message
                    // itself, so the data length is a multiple of the block size and all blocks
                    // have already been compressed by BlockUpdate. The "digest" is just the raw
                    // H1..Hn state at this point.
                    byte[] outData = GetDigestRawState(digestEngine, mode);
                    if(outData == null)
                    {
                        return;
                    }
                    // Truncate / size to engine's natural digest size for SHA-2 family.
                    int digestSize = digestEngine.GetDigestSize();
                    if(outData.Length != digestSize)
                    {
                        byte[] trimmed = new byte[digestSize];
                        Array.Copy(outData, 0, trimmed, 0, digestSize);
                        outData = trimmed;
                    }
                    this.Log(LogLevel.Noisy, "RunHashEngine(): Output Data (raw state):[{0}]", BitConverter.ToString(outData));
                    WriteHashOutput(pusherDescriptorList, outData);
                }
            }
            else
            {
                // Update: write the raw H1..Hn state.
                byte[] outputState = GetDigestRawState(digestEngine, mode);
                if(outputState == null)
                {
                    return;
                }
                this.Log(LogLevel.Noisy, "RunHashEngine(): OutputState=[{0}]", BitConverter.ToString(outputState));
                WriteHashOutput(pusherDescriptorList, outputState);
            }
        }

        private void WriteHashOutput(List<DmaDescriptor> pusherDescriptorList, byte[] outData)
        {
            uint outDataOffset = 0;
            for(var descIndex = 0; descIndex < pusherDescriptorList.Count && outDataOffset < outData.Length; descIndex++)
            {
                if(pusherDescriptorList[descIndex].Discard)
                {
                    continue;
                }

                uint bytesToWrite = Math.Min((uint)outData.Length - outDataOffset, pusherDescriptorList[descIndex].Length);
                for(uint i = 0; i < bytesToWrite; i++)
                {
                    machine.SystemBus.WriteByte(pusherDescriptorList[descIndex].FirstDataAddress + i, outData[outDataOffset + i], cpu);
                }
                outDataOffset += bytesToWrite;
            }
        }

        private void RunBypassTransfer(List<DmaDescriptor> fetcherDescriptorList, List<DmaDescriptor> pusherDescriptorList)
        {
            this.Log(LogLevel.Noisy, "RunBypassTransfer(): fetcherListLength={0} pusherListLength={1}", fetcherDescriptorList.Count, pusherDescriptorList.Count);

            if(fetcherDescriptorList.Count == 0 || pusherDescriptorList.Count == 0)
            {
                throw new ArgumentException("RunBypassTransfer(): no fetcher or pusher descriptor");
            }

            int fetcherDescriptorIndex = 0;
            int pusherDescriptorIndex = 0;
            uint fetcherAddressOffset = 0;
            uint pusherAddressOffset = 0;

            while(fetcherDescriptorIndex < fetcherDescriptorList.Count && pusherDescriptorIndex < pusherDescriptorList.Count)
            {
                uint copyLength = Math.Min(fetcherDescriptorList[fetcherDescriptorIndex].Length - fetcherAddressOffset,
                                           pusherDescriptorList[pusherDescriptorIndex].Length - pusherAddressOffset);

                DmaTransfer(fetcherDescriptorList[fetcherDescriptorIndex].FirstDataAddress + fetcherAddressOffset,
                            pusherDescriptorList[pusherDescriptorIndex].FirstDataAddress + pusherAddressOffset,
                            copyLength);

                if(copyLength + fetcherAddressOffset < fetcherDescriptorList[fetcherDescriptorIndex].Length)
                {
                    fetcherAddressOffset += copyLength;
                }
                else
                {
                    fetcherAddressOffset = 0;
                    fetcherDescriptorIndex++;
                }

                if(copyLength + pusherAddressOffset < pusherDescriptorList[pusherDescriptorIndex].Length)
                {
                    pusherAddressOffset += copyLength;
                }
                else
                {
                    pusherAddressOffset = 0;
                    pusherDescriptorIndex++;
                }
            }
        }

        private List<DmaDescriptor> ParseDescriptors(bool parseFetcher)
        {
            var list = new List<DmaDescriptor>();
            uint currentDescriptorAddress = (uint)((parseFetcher) ? fetcherAddress.Value : pusherAddress.Value);
            bool moreDescriptors = true;
            uint descriptorIndex = 0;

            while(moreDescriptors)
            {
                var descriptor = new DmaDescriptor();
                descriptor.FirstDataAddress = machine.SystemBus.ReadDoubleWord(currentDescriptorAddress, cpu);
                descriptor.NextDescriptorAddress = machine.SystemBus.ReadDoubleWord(currentDescriptorAddress + 0x4, cpu);
                descriptor.LastDescriptor = ((descriptor.NextDescriptorAddress & 0x1) > 0);
                descriptor.NextDescriptorAddress &= ~0x3U;
                descriptor.Length = machine.SystemBus.ReadDoubleWord(currentDescriptorAddress + 0x8, cpu);
                descriptor.ConstantAddress = ((descriptor.Length & 0x10000000) > 0);
                descriptor.Realign = ((descriptor.Length & 0x20000000) > 0);
                if(parseFetcher)
                {
                    descriptor.ZeroPadding = ((descriptor.Length & 0x40000000) > 0);
                }
                else
                {
                    descriptor.Discard = ((descriptor.Length & 0x40000000) > 0);
                }
                descriptor.InterruptEnable = ((descriptor.Length & 0x80000000) > 0);
                descriptor.Length &= 0x0FFFFFFF;
                descriptor.Tag = (parseFetcher) ? machine.SystemBus.ReadDoubleWord(currentDescriptorAddress + 0xC, cpu) : 0;
                descriptor.Data = ReadDescriptorData(descriptor, parseFetcher);
                list.Add(descriptor);

                this.Log(LogLevel.Noisy, "DESCRIPTOR {0}: Tag:{1:X} Length:{2} Last:{3} Const:{4} Realign:{5} ZeroPad:{6} Discard:{7} IE:{8} Data:[{9}]",
                         descriptorIndex, descriptor.Tag, descriptor.Length, descriptor.LastDescriptor, descriptor.ConstantAddress,
                         descriptor.Realign, descriptor.ZeroPadding, descriptor.Discard, descriptor.InterruptEnable, BitConverter.ToString(descriptor.Data));
                this.Log(LogLevel.Noisy, "COMMON: Engine:{0}, Last:{1}", descriptor.EngineSelect, descriptor.IsLastDataOrConfig);
                if(parseFetcher)
                {
                    if(descriptor.IsData)
                    {
                        this.Log(LogLevel.Noisy, "DATA: Type:{0}, Invalid Bytes/Bits:{1}", descriptor.DataType, descriptor.InvalidBytesOrBits);
                    }
                    else
                    {
                        this.Log(LogLevel.Noisy, "CONFIG: Offset:{0}", descriptor.OffsetStartAddress);
                    }
                }

                moreDescriptors = !descriptor.LastDescriptor;
                // TODO: handle the realign bit set here
                currentDescriptorAddress = descriptor.NextDescriptorAddress;
                descriptorIndex++;

                // In scatter/gather node, the hardware updates the fetchAddress/pushAddress after each processed
                // descriptor unless the constant address flag is set.
                if(((parseFetcher && fetcherScatterGather.Value) || (!parseFetcher && pusherScatterGather.Value))
                    && moreDescriptors
                    && !descriptor.ConstantAddress)
                {
                    if(parseFetcher)
                    {
                        fetcherAddress.Value = currentDescriptorAddress;
                    }
                    else
                    {
                        pusherAddress.Value = currentDescriptorAddress;
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Builds a concatenated byte array from multiple descriptors.
        /// </summary>
        /// <param name="descriptorIndices">List of descriptor indices to concatenate</param>
        /// <param name="descriptorList">The list of fetcher descriptors to extract data from</param>
        /// <returns>A byte array containing all the data from the specified descriptors</returns>
        private byte[] BuildDataArray(List<int> descriptorIndices, List<DmaDescriptor> descriptorList)
        {
            if(descriptorIndices == null || descriptorIndices.Count == 0)
            {
                return null;
            }

            // Calculate total size needed (length - InvalidBytesOrBits for each descriptor)
            int totalSize = 0;
            foreach(int idx in descriptorIndices)
            {
                if(idx >= 0 && idx < descriptorList.Count)
                {
                    int validBytes = (int)(descriptorList[idx].Length - (descriptorList[idx].Length % 16 == 0 ? descriptorList[idx].InvalidBytesOrBits : 0));
                    totalSize += Math.Max(0, validBytes); // Ensure non-negative
                }
            }

            // Build the concatenated array
            byte[] result = new byte[totalSize];
            int offset = 0;

            foreach(int idx in descriptorIndices)
            {
                if(idx >= 0 && idx < descriptorList.Count)
                {
                    byte[] data = descriptorList[idx].Data;
                    int validBytes = (int)(descriptorList[idx].Length - (descriptorList[idx].Length % 16 == 0 ? descriptorList[idx].InvalidBytesOrBits : 0));
                    validBytes = Math.Max(0, Math.Min(validBytes, data.Length)); // Ensure bounds checking

                    if(validBytes > 0)
                    {
                        Array.Copy(data, 0, result, offset, validBytes);
                        offset += validBytes;
                    }
                }
            }

            return result;
        }

        private byte[] ReadDescriptorData(DmaDescriptor descriptor, bool parseFetcher)
        {
            var data = new byte[descriptor.Length];
            if(!parseFetcher || descriptor.ZeroPadding)
            {
                return data;
            }

            var transferLengthInBytes = (int)fetcherTransferWidth;
            var currentAddress = (ulong)descriptor.FirstDataAddress;
            var transferred = 0;

            while(transferred < data.Length)
            {
                var remainingBytes = data.Length - transferred;
                var transferData = new byte[transferLengthInBytes];

                switch(fetcherTransferWidth)
                {
                case TransferType.Byte:
                    transferData[0] = machine.SystemBus.ReadByte(currentAddress, cpu);
                    break;
                case TransferType.Word:
                    BitConverter.GetBytes(machine.SystemBus.ReadWord(currentAddress, cpu)).CopyTo(transferData, 0);
                    break;
                case TransferType.DoubleWord:
                    BitConverter.GetBytes(machine.SystemBus.ReadDoubleWord(currentAddress, cpu)).CopyTo(transferData, 0);
                    break;
                case TransferType.QuadWord:
                    BitConverter.GetBytes(machine.SystemBus.ReadQuadWord(currentAddress, cpu)).CopyTo(transferData, 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Fetcher transfer width {fetcherTransferWidth} is not supported");
                }

                var bytesToCopy = Math.Min(remainingBytes, transferLengthInBytes);
                Array.Copy(transferData, 0, data, transferred, bytesToCopy);
                transferred += bytesToCopy;

                if(!descriptor.ConstantAddress)
                {
                    currentAddress += (ulong)transferLengthInBytes;
                }
            }

            return data;
        }

        private void DmaTransfer(ulong sourceAddress, ulong destinationAddress, ulong length)
        {
            // CONSTADDR flag: when set, address should NOT increment
            bool incrementReadAddress = !fetcherConstantAddress.Value;
            bool incrementWriteAddress = !pusherConstantAddress.Value;

            var sysbus = machine.GetSystemBus(this);
            var readLengthInBytes = (int)fetcherTransferWidth;
            var writeLengthInBytes = (int)pusherTransferWidth;

            // Source increment step is the read transfer width
            var sourceOffsetStep = (ulong)readLengthInBytes;
            // Destination increment step is the write transfer width
            var destinationOffsetStep = (ulong)writeLengthInBytes;

            var buffer = new byte[length];
            var currentSourceAddress = sourceAddress;
            var currentDestinationAddress = destinationAddress;

            // Read phase: read data from source address
            var transferred = 0;
            while(transferred < (int)length)
            {
                var remainingBytes = (int)length - transferred;
                var readAddress = incrementReadAddress ? currentSourceAddress : sourceAddress;

                if(remainingBytes >= readLengthInBytes)
                {
                    // Full transfer unit read
                    switch(fetcherTransferWidth)
                    {
                    case TransferType.Byte:
                        buffer[transferred] = sysbus.ReadByte(readAddress, cpu);
                        break;
                    case TransferType.Word:
                        BitConverter.GetBytes(sysbus.ReadWord(readAddress, cpu)).CopyTo(buffer, transferred);
                        break;
                    case TransferType.DoubleWord:
                        BitConverter.GetBytes(sysbus.ReadDoubleWord(readAddress, cpu)).CopyTo(buffer, transferred);
                        break;
                    case TransferType.QuadWord:
                        BitConverter.GetBytes(sysbus.ReadQuadWord(readAddress, cpu)).CopyTo(buffer, transferred);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Fetcher transfer width {fetcherTransferWidth} is not supported");
                    }
                    transferred += readLengthInBytes;
                    if(incrementReadAddress)
                    {
                        currentSourceAddress += sourceOffsetStep;
                    }
                }
                else
                {
                    // Partial transfer - read full transfer unit and discard remaining bytes
                    byte[] fullTransfer = new byte[readLengthInBytes];
                    switch(fetcherTransferWidth)
                    {
                    case TransferType.Byte:
                        fullTransfer[0] = sysbus.ReadByte(readAddress, cpu);
                        break;
                    case TransferType.Word:
                        BitConverter.GetBytes(sysbus.ReadWord(readAddress, cpu)).CopyTo(fullTransfer, 0);
                        break;
                    case TransferType.DoubleWord:
                        BitConverter.GetBytes(sysbus.ReadDoubleWord(readAddress, cpu)).CopyTo(fullTransfer, 0);
                        break;
                    case TransferType.QuadWord:
                        BitConverter.GetBytes(sysbus.ReadQuadWord(readAddress, cpu)).CopyTo(fullTransfer, 0);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Fetcher transfer width {fetcherTransferWidth} is not supported");
                    }
                    // Copy only the remaining bytes, discard the rest
                    Array.Copy(fullTransfer, 0, buffer, transferred, remainingBytes);
                    transferred = (int)length;
                    if(incrementReadAddress)
                    {
                        currentSourceAddress += sourceOffsetStep;
                    }
                }
            }

            // Write phase: write data to destination address
            transferred = 0;
            while(transferred < (int)length)
            {
                var remainingBytes = (int)length - transferred;
                var writeAddress = incrementWriteAddress ? currentDestinationAddress : destinationAddress;

                if(remainingBytes >= writeLengthInBytes)
                {
                    // Full transfer unit write
                    switch(pusherTransferWidth)
                    {
                    case TransferType.Byte:
                        sysbus.WriteByte(writeAddress, buffer[transferred], cpu);
                        break;
                    case TransferType.Word:
                        sysbus.WriteWord(writeAddress, BitConverter.ToUInt16(buffer, transferred), cpu);
                        break;
                    case TransferType.DoubleWord:
                        sysbus.WriteDoubleWord(writeAddress, BitConverter.ToUInt32(buffer, transferred), cpu);
                        break;
                    case TransferType.QuadWord:
                        sysbus.WriteQuadWord(writeAddress, BitConverter.ToUInt64(buffer, transferred), cpu);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Pusher transfer width {pusherTransferWidth} is not supported");
                    }
                    transferred += writeLengthInBytes;
                    if(incrementWriteAddress)
                    {
                        currentDestinationAddress += destinationOffsetStep;
                    }
                }
                else
                {
                    // Partial transfer - write remaining bytes byte-by-byte
                    for(var i = 0; i < remainingBytes; i++)
                    {
                        sysbus.WriteByte(writeAddress + (ulong)i, buffer[transferred + i], cpu);
                    }
                    transferred = (int)length;
                }
            }
        }

        private void WriteCcmPusherOutput(List<DmaDescriptor> pusherDescriptorList, byte[] finalOutput)
        {
            uint writtenBytesCount = 0;
            int currentPusherDescriptorIndex = 0;
            while(writtenBytesCount < finalOutput.Length)
            {
                if(currentPusherDescriptorIndex >= pusherDescriptorList.Count)
                {
                    throw new ArgumentException("RunAesCcm(): ran out of pusher descriptors while writing output");
                }
                if(pusherDescriptorList[currentPusherDescriptorIndex].Discard)
                {
                    currentPusherDescriptorIndex++;
                    continue;
                }

                uint writeLength = (uint)Math.Min(pusherDescriptorList[currentPusherDescriptorIndex].Length, (finalOutput.Length - writtenBytesCount));
                for(uint i = 0; i < writeLength; i++)
                {
                    machine.SystemBus.WriteByte(pusherDescriptorList[currentPusherDescriptorIndex].FirstDataAddress + i,
                                                finalOutput[writtenBytesCount + i], cpu);
                }

                writtenBytesCount += writeLength;
                currentPusherDescriptorIndex++;
            }
        }

        // This bit is high as long as the fetcher is busy
        private IFlagRegisterField fetcherBusy;
        // When this bit is zero, the pusher runs in direct mode.
        // When this bit is one, the pusher runs in scatter-gather mode.
        private IFlagRegisterField  pusherScatterGather;
        // When this bit is high, the pusher will stop at the end of the current block
        // (even if the STOP bit in the descriptor is low).
        private IFlagRegisterField  pusherStopAtEndOfBlock;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IFlagRegisterField  pusherDiscardData;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IFlagRegisterField  pusherRealignLength;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IFlagRegisterField  pusherConstantAddress;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IValueRegisterField pusherLength;
        // Direct mode: written by SW (address of the first data)
        // Scatter/gather mode: Written by SW (address of first descriptor). Afterwards, it is updated
        //                      by the hardware after each processed descriptor.
        private IValueRegisterField pusherAddress;
        // When this bit is zero, the fetcher runs in direct mode.
        // When this bit is one, the fetcher runs in scatter-gather mode.
        private IFlagRegisterField  fetcherScatterGather;

        // Interrupt flags
        private IFlagRegisterField fetcherEndOfBlockInterruptEnable;
        // When this bit is high, the fetcher will stop at the end of the current block
        // (even if the STOP bit in the descriptor is low).
        private IFlagRegisterField  fetcherStopAtEndOfBlock;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IValueRegisterField fetcherTag;
        // Direct mode: when set, fetcher generates zeroes for padding bytes beyond the raw length
        // Scatter/gather mode: per-descriptor flag
        private IFlagRegisterField  fetcherZeroPadding;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IFlagRegisterField  fetcherRealignLength;
        private IFlagRegisterField pusherStoppedInterruptEnable;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IValueRegisterField fetcherLength;

        private IFlagRegisterField fetcherErrorInterruptEnable;
        // Triggered when reaching a block with Stop=1 (or end of direct transfer)
        private IFlagRegisterField pusherStoppedInterrupt;
        // Triggered at the end of each block (if enabled in the descriptor - scatter-gather only)
        private IFlagRegisterField pusherEndOfBlockInterrupt;
        // Triggered when an error response is received from AXI
        private IFlagRegisterField fetcherErrorInterrupt;
        // Triggered when reaching a block with Stop=1 (or end of direct transfer)
        private IFlagRegisterField fetcherStoppedInterrupt;
        // Direct mode: written by SW
        // Scatter/gather mode: not used
        private IFlagRegisterField  fetcherConstantAddress;
        // Triggered at the end of each block (if enabled in the descriptor - scatter-gather only)
        private IFlagRegisterField fetcherEndOfBlockInterrupt;
        private IFlagRegisterField pusherEndOfBlockInterruptEnable;
        // Triggered when an error response is received from AXI
        private IFlagRegisterField pusherErrorInterrupt;
        private IFlagRegisterField fetcherStoppedInterruptEnable;
        // This bit is high as long as the pusher is busy
        private IFlagRegisterField pusherBusy;
        // Direct mode: written by SW (address of the first data)
        // Scatter/gather mode: Written by SW (address of first descriptor). Afterwards, it is updated
        //                      by the hardware after each processed descriptor.
        private IValueRegisterField fetcherAddress;
        private IFlagRegisterField pusherErrorInterruptEnable;

        private readonly ISiLabs_KeyStorage ksu;
        // Transfer width for fetcher DMA transfers
        private readonly TransferType fetcherTransferWidth;
        // Transfer width for pusher DMA transfers
        private readonly TransferType pusherTransferWidth;
        private readonly ICPU cpu;

        // BouncyCastle's Sha{1,224,256,384,512}Digest classes hold H1..Hn as private fields
        // with no public accessor. We read/write those fields by reflection.
        private const BindingFlags PrivateInstanceField = BindingFlags.NonPublic | BindingFlags.Instance;

        // Local name of the Secure Element CPU in the platform description.
        private const string SeCpuName = "secpu";
        private const int CcmBlockSize = 16;
        private const int CcmContextSize = 2 * CcmBlockSize;

        // Lower 256 bits (SE crypto key)
        private static readonly byte[] seCryptoKey = new byte[] {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        private class DmaDescriptor
        {
            public DmaDescriptor()
            {
            }

            public CryptoEngine EngineSelect
            {
                get => (CryptoEngine)(this.Tag & 0xF);
            }

            public bool IsData
            {
                get => ((this.Tag & 0x10) == 0);
            }

            public bool IsConfig
            {
                get => !this.IsData;
            }

            public bool IsLastDataOrConfig
            {
                get => ((this.Tag & 0x20) > 0);
            }

            public uint InvalidBytesOrBits
            {
                // Bits 13:8
                get => IsData ? ((this.Tag & 0xCF00) >> 8) : 0;
            }

            public uint OffsetStartAddress
            {
                // Bits 15:8
                get => IsData ? 0 : ((this.Tag & 0xFF00) >> 8);
            }

            public CryptoDataType DataType
            {
                get
                {
                    if(!IsData)
                    {
                        return CryptoDataType.Unused;
                    }

                    // Bits 7:6
                    uint dataType = ((this.Tag & 0xC0) >> 6);
                    switch(this.EngineSelect)
                    {
                    case CryptoEngine.Aes:
                    case CryptoEngine.Sm4:
                    case CryptoEngine.Aria:
                        switch(dataType)
                        {
                        case 0:
                            return CryptoDataType.Payload;
                        case 1:
                            return CryptoDataType.Header;
                        }
                        break;
                    case CryptoEngine.Hash:
                    case CryptoEngine.Sha3:
                        switch(dataType)
                        {
                        case 0:
                            return CryptoDataType.Message;
                        case 1:
                            return CryptoDataType.InitializationData;
                        case 2:
                            return CryptoDataType.HMAC_Key;
                        case 3:
                            return CryptoDataType.ReferenceHash;
                        }
                        break;
                    case CryptoEngine.AesGcm:
                        switch(dataType)
                        {
                        case 0:
                            return CryptoDataType.Payload;
                        case 1:
                            return CryptoDataType.Header;
                        case 3:
                            return CryptoDataType.ReferenceTag;
                        }
                        break;
                    case CryptoEngine.ChaChaPoly:
                    case CryptoEngine.HpChaChaPoly:
                        switch(dataType)
                        {
                        case 0:
                            return CryptoDataType.Payload;
                        case 1:
                            return CryptoDataType.Header;
                        case 3:
                            return CryptoDataType.ReferenceDigest;
                        }
                        break;
                    }
                    return CryptoDataType.Unused;
                }
            }

            public uint FirstDataAddress;
            public bool LastDescriptor;
            public uint NextDescriptorAddress;
            public uint Length;
            public bool ConstantAddress;
            public bool Realign;
            public bool Discard;
            public bool ZeroPadding;
            public bool InterruptEnable;
            public uint Tag;
            public byte[] Data;
        }

        private enum AesMode
        {
            Ecb     = 0x001,
            Ccb     = 0x002,
            Ctr     = 0x004,
            Cfb     = 0x008,
            Ofb     = 0x010,
            Ccm     = 0x020,
            GcmGmac = 0x040,
            Xts     = 0x080,
            Cmac    = 0x100,
        }

        private enum AesParameterOffset : uint
        {
            Config = 0x00,
            Key    = 0x08,
            IV     = 0x28,
            IV2    = 0x38,
            Key2   = 0x48,
            Mask   = 0x68,
        }

        private enum HashMode : uint
        {
            Md5     = 0x01,
            Sha_1   = 0x02,
            Sha_224 = 0x04,
            Sha_256 = 0x08,
            Sha_384 = 0x10,
            Sha_512 = 0x20,
            Sm3     = 0x40,
        }

        private enum HashParameterOffset : uint
        {
            Config = 0x00,
        }

        private enum Registers
        {
            FetcherAddress              = 0x000,
            FetcherAddressMsb           = 0x004,
            FetcherLength               = 0x008,
            FetcherTag                  = 0x00C,
            PusherAddress               = 0x010,
            PusherAddressMsb            = 0x014,
            PusherLength                = 0x018,
            InterruptEnable             = 0x01C,
            InterruptEnableSet          = 0x020,
            InterruptEnableClear        = 0x024,
            InterruptFlag               = 0x028,
            InterruptFlagMasked         = 0x02C,
            InterruptFlagClear          = 0x030,
            Control                     = 0x034,
            Command                     = 0x038,
            Status                      = 0x03C,
            IncludeIpsHardwareConfig    = 0x400,
            Ba411eHardwareConfig1       = 0x404,
            Ba411eHardwareConfig2       = 0x408,
            Ba413HardwareConfig         = 0x40C,
            Ba418HardwareConfig         = 0x410,
            Ba419HardwareConfig         = 0x414,
            Ba424HardwareConfig         = 0x418,
        }

        private enum CryptoEngine
        {
            Bypass       = 0x0,
            Aes          = 0x1,
            Des          = 0x2,
            Hash         = 0x3,
            ChaChaPoly   = 0x4,
            Sha3         = 0x5,
            AesGcm       = 0x6,
            AesXts       = 0x7,
            HashPlusAes  = 0x8,
            AesPlusHash  = 0x9,
            Zuc          = 0xA,
            Sm4          = 0xB,
            HpChaChaPoly = 0xC,
            Snow3g       = 0xD,
            Kasumi       = 0xE,
            Aria         = 0xF,
        }

        private enum CryptoDataType
        {
            Unused,
            Payload,
            Header,
            Message,
            InitializationData,
            HMAC_Key,
            ReferenceHash,
            ReferenceTag,
            ReferenceDigest,
        }
    }
}
