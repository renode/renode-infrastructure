//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2022-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class SiLabs_SecureElement
    {
        public SiLabs_SecureElement(Machine machine, IDoubleWordPeripheral parent, Queue<uint> txFifo, Queue<uint> rxFifo, bool series3)
            : this(machine, parent, txFifo, rxFifo, series3, 0, 0, 0, 0, 0, 0, null)
        {
        }

        public SiLabs_SecureElement(Machine machine, IDoubleWordPeripheral parent, Queue<uint> txFifo, Queue<uint> rxFifo, bool series3,
                                    uint flashSize, uint flashPageSize, uint flashRegionSize, uint flashCodeRegionStart, uint flashCodeRegionEnd = 0, uint flashDataRegionStart = 0, ISiLabs_KeyStorage ksuStorage = null)
        {
            this.machine = machine;
            this.parent = parent;
            this.ksuStorage = ksuStorage;
            this.txFifo = txFifo;
            this.rxFifo = rxFifo;
            this.series3 = series3;
            this.flashSize = flashSize;
            this.flashPageSize = flashPageSize;
            this.flashRegionSize = flashRegionSize;
            this.flashCodeRegionStart = flashCodeRegionStart;
            this.flashCodeRegionEnd = flashCodeRegionEnd;
            this.flashDataRegionStart = flashDataRegionStart;

            // TODO: the SE adds an internal key at slot 246 used for NVM3 encryption operations. 
            // We just add an arbitrary key here to faciliate all NVM3 operations.
            byte[] key = {0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF};
            volatileKeys[246] = key;

            Array.Fill(userDataToken, (byte)0xFF);
            userDataInitialized = false;
            userDataRemainingWrites = UserDataWriteLimit;
        }

#pragma warning disable IDE0060
        public bool TxFifoEnqueueCallback(uint word)
#pragma warning restore IDE0060
        {
            if(wordsLeftToBeReceived == 0)
            {
                parent.Log(LogLevel.Error, "TxFifoEnqueueCallback: 0 words left");
                return false;
            }

            wordsLeftToBeReceived--;

            if(wordsLeftToBeReceived == 0)
            {
                ProcessCommand();
                return true;
            }

            return false;
        }

        public void TxHeaderSetCallback(uint header)
        {
            // Setting the TX header starts a new "transaction".
            // The TX header as per HOST code, appears to be simply the number of 
            // bytes (multiple of 4) that constitute the message.
            // At minimum a message contains:
            // - The header itself
            // - The command ID
            // - Address in memory of command data input
            // - Address in memory of command data output
            // - Zero or more parameters
            wordsLeftToBeReceived = header / 4;
        }

        public uint GetDefaultErrorStatus()
        {
            return (uint)ResponseCode.InternalError;
        }

        public void Reset()
        {
            wordsLeftToBeReceived = 0;

            // Reset and re-initialize volatile keys with the NVM3 encryption key
            volatileKeys.Clear();
            byte[] key = {0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF};
            volatileKeys[246] = key;
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

        private static int ShaHWordCount(ShaMode hashMode)
        {
            switch(hashMode)
            {
            case ShaMode.Sha1:
                return 5;
            case ShaMode.Sha224:
            case ShaMode.Sha256:
            case ShaMode.Sha384:
            case ShaMode.Sha512:
                return 8;
            }
            return 0;
        }

        private static bool ShaIsLongDigest(ShaMode hashMode)
        {
            return hashMode == ShaMode.Sha384 || hashMode == ShaMode.Sha512;
        }

        private BigInteger ComputePasswordScalar(byte[] password, ECDomainParameters curve)
        {
            // Derive password scalar s = H(password) mod n
            var sha256 = new Sha256Digest();
            sha256.BlockUpdate(password, 0, password.Length);
            var pwHash = new byte[sha256.GetDigestSize()];
            sha256.DoFinal(pwHash, 0);

            var s = new BigInteger(1, pwHash).Mod(curve.N);
            if(s.Equals(BigInteger.Zero))
            {
                s = BigInteger.One; // ensure nonzero
            }

            return s;
        }

        // ---------------------------------------------------------------------------------------------
        // CCM helpers
        // ---------------------------------------------------------------------------------------------

        private KeyParameter CcmReadAuthAndKeyDescriptors(string label, string phase, KeyMode keyMode, uint keyIndex, uint inputDma, out uint nextDescriptorPtr)
        {
            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTransferOptions transferOptions;
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: {1}INPUT0: authPtr=0x{2:X} authLength={3} options={4} nextDescriptorPtr=0x{5:X}",
                     label, phase, authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: {1}INPUT1: keyPtr=0x{2:X} keyLength={3} options={4} nextDescriptorPtr=0x{5:X}",
                     label, phase, keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = ReadKey(keyMode, keyIndex, keyPtr, keyLength);
            parent.Log(LogLevel.Debug, "Key=[{0}]", BitConverter.ToString(key));
            return new KeyParameter(key);
        }

        private void CcmUnpackNonceAndAad(string label, string phase, uint descriptorPtr, out byte[] nonce, out byte[] aad, out uint nextDescriptorPtr)
        {
            uint noncePtr;
            uint nonceLength;
            DmaTransferOptions transferOptions;
            UnpackDmaDescriptor(descriptorPtr, out noncePtr, out nonceLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: {1}NONCE: noncePtr=0x{2:X} nonceLength={3} options={4} nextDescriptorPtr=0x{5:X}",
                     label, phase, noncePtr, nonceLength, transferOptions, nextDescriptorPtr);
            nonce = new byte[nonceLength];
            FetchFromRam(noncePtr, nonce, 0, nonceLength);
            parent.Log(LogLevel.Debug, "Nonce=[{0}]", BitConverter.ToString(nonce));

            uint aadPtr;
            uint aadLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out aadPtr, out aadLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: {1}AAD: aadPtr=0x{2:X} aadLength={3} options={4} nextDescriptorPtr=0x{5:X}",
                     label, phase, aadPtr, aadLength, transferOptions, nextDescriptorPtr);
            aad = new byte[aadLength];
            FetchFromRam(aadPtr, aad, 0, aadLength);
            parent.Log(LogLevel.Debug, "Aad=[{0}]", BitConverter.ToString(aad));
        }

        private ResponseCode CcmUnpackContextDescriptor(string label, string phase, uint descriptorPtr, out uint contextPtr, out uint nextDescriptorPtr)
        {
            uint contextLength;
            DmaTransferOptions transferOptions;
            UnpackDmaDescriptor(descriptorPtr, out contextPtr, out contextLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: {1}CONTEXT: contextPtr=0x{2:X} contextLength={3} options={4} nextDescriptorPtr=0x{5:X}",
                     label, phase, contextPtr, contextLength, transferOptions, nextDescriptorPtr);
            if(contextLength != CcmContextSize)
            {
                parent.Log(LogLevel.Error, "{0}: {1}invalid context length {2}", label, phase, contextLength);
                return ResponseCode.InvalidParameter;
            }
            return ResponseCode.Ok;
        }

        private byte CcmGetQ(int nonceLength)
        {
            return (byte)(CcmBlockSize - 1 - nonceLength);
        }

        private void CcmIncrementCounter(byte[] counterBlock)
        {
            for(int i = CcmBlockSize - 1; i >= 0; i--)
            {
                if(++counterBlock[i] != 0)
                {
                    break;
                }
            }
        }

        // Encrypts/decrypts `length` (1..16) bytes via CTR keystream derived from counterBlock, then
        // advances counterBlock by one. `length` may be less than CcmBlockSize for the trailing
        // partial block; input/output are always full 16-byte buffers, but only the first `length`
        // bytes are meaningful.
        private void CcmCtrBlock(KeyParameter keyParameter, byte[] counterBlock, byte[] input, byte[] output, int length)
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

        // Builds B0, runs the CBC-MAC over B0 and the length-prefixed, zero-padded AAD (producing the
        // initial macState), and seeds counterBlock to flags(q-1) || nonce || 0. The 2-byte AAD length
        // prefix mirrors the encoding the firmware itself implements (sufficient for aadLength < 0xFF00,
        // which covers every AAD size used by the SE manager).
        private void CcmStart(KeyParameter keyParameter, byte[] nonce, uint tagLength, uint aadLength, byte[] aad, uint totalMessageLength, byte[] macState, byte[] counterBlock)
        {
            byte q = CcmGetQ(nonce.Length);

            byte[] b0 = new byte[CcmBlockSize];
            byte flags = 0;
            if(aadLength > 0)
            {
                flags |= 0x40;
            }
            flags |= (byte)((((tagLength - 2) / 2) & 0x7) << 3);
            flags |= (byte)((q - 1) & 0x7);
            b0[0] = flags;
            Array.Copy(nonce, 0, b0, 1, nonce.Length);
            uint lengthField = totalMessageLength;
            for(int i = 0; i < q; i++)
            {
                b0[CcmBlockSize - 1 - i] = (byte)(lengthField & 0xFF);
                lengthField >>= 8;
            }

            Array.Clear(macState, 0, CcmBlockSize);
            CcmMacBlock(keyParameter, macState, b0);

            if(aadLength > 0)
            {
                CcmMacAad(keyParameter, macState, aad);
            }

            // Seed counterBlock to A0 (flags(q-1) || nonce || 0), then immediately advance it to A1:
            // A0 is reserved for the tag mask (S0 = Enc(A0), see CcmFinish) and must never be used as
            // a data keystream block, but CcmCtrBlock uses its counterBlock argument as-is for the
            // *current* call before incrementing it for next time -- so the very first data block
            // needs counterBlock to already hold A1 when CcmProcessBlocks/CcmFinish first calls it.
            counterBlock[0] = (byte)(q - 1);
            Array.Copy(nonce, 0, counterBlock, 1, nonce.Length);
            Array.Clear(counterBlock, 1 + nonce.Length, CcmBlockSize - 1 - nonce.Length);
            CcmIncrementCounter(counterBlock);
        }

        private void CcmMacAad(KeyParameter keyParameter, byte[] macState, byte[] aad)
        {
            byte[] block = new byte[CcmBlockSize];
            int use = Math.Min(aad.Length, CcmBlockSize - 2);
            block[0] = (byte)((aad.Length >> 8) & 0xFF);
            block[1] = (byte)(aad.Length & 0xFF);
            Array.Copy(aad, 0, block, 2, use);
            CcmMacBlock(keyParameter, macState, block);

            int offset = use;
            while(offset < aad.Length)
            {
                Array.Clear(block, 0, CcmBlockSize);
                int chunk = Math.Min(aad.Length - offset, CcmBlockSize);
                Array.Copy(aad, offset, block, 0, chunk);
                CcmMacBlock(keyParameter, macState, block);
                offset += chunk;
            }
        }

        // Processes `length` bytes (a multiple of CcmBlockSize) of full blocks, updating macState and
        // counterBlock in place. Order matches sl_se_ccm_multipart_update(): MAC-then-CTR when
        // encrypting (authenticate the plaintext being encrypted), CTR-then-MAC when decrypting
        // (authenticate the plaintext recovered by decryption).
        private void CcmProcessBlocks(bool encrypt, KeyParameter keyParameter, byte[] macState, byte[] counterBlock, byte[] input, uint length, byte[] output)
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

        // Processes the final (0..15 byte) partial block, if any, then computes the tag as
        // Enc(A0) XOR macState, truncated to tagLength, where A0 is counterBlock with its trailing q
        // counter bytes zeroed -- i.e. the very first counter value used by CcmStart, reconstructed
        // from the stationary flags/nonce bytes rather than requiring the caller to resend the nonce.
        private void CcmFinish(bool encrypt, KeyParameter keyParameter, byte[] macState, byte[] counterBlock, byte[] finalData, byte[] outputData, uint tagLength, byte[] tag)
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
                    // The un-produced tail of the partial block was never written by CcmCtrBlock (it
                    // only fills the first `finalLength` bytes of paddedPlain), so it is already zero --
                    // matching the firmware's use of a zero-padded plaintext block when authenticating
                    // the trailing partial block.
                    CcmMacBlock(keyParameter, macState, paddedPlain);
                    Array.Copy(paddedPlain, 0, outputData, 0, finalLength);
                }
            }

            byte[] a0 = new byte[CcmBlockSize];
            Array.Copy(counterBlock, 0, a0, 0, CcmBlockSize);
            byte q = (byte)(a0[0] + 1);
            Array.Clear(a0, CcmBlockSize - q, q);

            var aes = new AesEngine();
            aes.Init(true, keyParameter);
            byte[] s0 = new byte[CcmBlockSize];
            aes.ProcessBlock(a0, 0, s0, 0);

            for(int i = 0; i < tagLength; i++)
            {
                tag[i] = (byte)(macState[i] ^ s0[i]);
            }
        }

        private bool CcmTagsEqual(byte[] a, byte[] b, uint length)
        {
            bool equal = true;
            for(uint i = 0; i < length; i++)
            {
                if(a[i] != b[i])
                {
                    equal = false;
                }
            }
            return equal;
        }

        private bool VerifySchnorrProof(ECDomainParameters curve, ECPoint publicKey, SchnorrProof proof, byte[] userId, string tag, ECPoint basePoint = null)
        {
            try
            {
                // Default to generator point if no base point specified
                var actualBasePoint = basePoint ?? curve.G;

                // Schnorr proof verification: Check if V = r*basePoint + c*publicKey
                // where c = H(encode(basePoint) || encode(V) || encode(publicKey) || user_id || tag)

                // EC-JPAKE format: c = H(encode(basePoint) || encode(V) || encode(publicKey) || user_id || tag)
                var challenge = ComputeSchnorrChallenge(actualBasePoint, proof.V, publicKey, userId, tag);

                // Compute r*basePoint + c*publicKey
                var rBase = actualBasePoint.Multiply(proof.R);
                var cPublicKey = publicKey.Multiply(challenge);
                var verificationPoint = rBase.Add(cPublicKey).Normalize();

                // Check if V == r*basePoint + c*publicKey
                return proof.V.Equals(verificationPoint);
            }
            catch(Exception e)
            {
                parent.Log(LogLevel.Error, "VerifySchnorrProof: Exception during verification: {0}", e.Message);
                return false;
            }
        }

        private SchnorrProof DecodeSchnorrProofFromBytes(byte[] proofBytes, ECDomainParameters curve)
        {
            if(proofBytes.Length != 96) // 64 bytes for point V + 32 bytes for scalar r
            {
                throw new ArgumentException("Invalid proof byte length");
            }

            // Extract commitment point V (first 64 bytes)
            byte[] vBytes = new byte[64];
            Array.Copy(proofBytes, 0, vBytes, 0, 64);
            var vPoint = DecodeECPointFromBytes(vBytes, curve);

            // Extract response scalar r (last 32 bytes)
            byte[] rBytes = new byte[32];
            Array.Copy(proofBytes, 64, rBytes, 0, 32);
            var r = new BigInteger(1, rBytes);

            return new SchnorrProof { V = vPoint, R = r };
        }

        // Runs one CBC-MAC step over a 16-byte block, updating macState in place. CbcBlockCipher's
        // EncryptBlock XORs the block into its chaining value, encrypts it, and copies the ciphertext
        // back into that same chaining value -- so the block cipher's output *is* the new MAC state.
        private void CcmMacBlock(KeyParameter keyParameter, byte[] macState, byte[] block)
        {
            var mac = new CbcBlockCipher(new AesEngine());
            mac.Init(true, new ParametersWithIV(keyParameter, macState));
            byte[] macOut = new byte[CcmBlockSize];
            mac.ProcessBlock(block, 0, macOut, 0);
            Array.Copy(macOut, 0, macState, 0, CcmBlockSize);
        }

        private byte[] EncodeSchnorrProofToBytes(SchnorrProof proof)
        {
            // Encode as V || r where V is the EC point and r is the scalar
            var vBytes = EncodeECPointToBytes(proof.V);
            var rBytes = BigIntegerToFixedByteArray(proof.R, 32);

            var result = new byte[96]; // 64 + 32 bytes
            Array.Copy(vBytes, 0, result, 0, 64);
            Array.Copy(rBytes, 0, result, 64, 32);

            return result;
        }

        private byte[] EncodeECPointToBytes(ECPoint point)
        {
            // Encode as uncompressed format without the 0x04 prefix: x || y
            var xBytes = BigIntegerToFixedByteArray(point.AffineXCoord.ToBigInteger(), 32);
            var yBytes = BigIntegerToFixedByteArray(point.AffineYCoord.ToBigInteger(), 32);

            var result = new byte[64]; // 32 + 32 bytes for P-256
            Array.Copy(xBytes, 0, result, 0, 32);
            Array.Copy(yBytes, 0, result, 32, 32);

            return result;
        }

        private BigInteger ComputeSchnorrChallenge(ECPoint basePoint, ECPoint vPoint, ECPoint publicKey, byte[] userId, string tag)
        {
            // EC-JPAKE challenge: c = H(encode(basePoint) || encode(V) || encode(publicKey) || user_id || tag)
            // This unified method handles both Round 1 (with custom tags) and Round 2 (with "JPAKE-A")
            var sha256 = new Sha256Digest();

            // Add encode(basePoint) - the base point (G for Round 1, combined point B for Round 2)
            var basePointBytes = basePoint.GetEncoded(false);
            sha256.BlockUpdate(basePointBytes, 0, basePointBytes.Length);

            // Add encode(V) - the commitment point
            var vBytes = vPoint.GetEncoded(false);
            sha256.BlockUpdate(vBytes, 0, vBytes.Length);

            // Add encode(publicKey) - the public key we're proving knowledge of
            var publicKeyBytes = publicKey.GetEncoded(false);
            sha256.BlockUpdate(publicKeyBytes, 0, publicKeyBytes.Length);

            // Add user_id
            sha256.BlockUpdate(userId, 0, userId.Length);

            // Add tag (e.g., "JPAKE-X1", "JPAKE-X2", "JPAKE-A")
            var jpakeTag = System.Text.Encoding.UTF8.GetBytes(tag);
            sha256.BlockUpdate(jpakeTag, 0, jpakeTag.Length);

            var hash = new byte[sha256.GetDigestSize()];
            sha256.DoFinal(hash, 0);

            // Convert to BigInteger and reduce modulo curve order
            var challenge = new BigInteger(1, hash);
            var curve = GetP256CurveParameters();
            return challenge.Mod(curve.N);
        }

        private SchnorrProof GenerateSchnorrProof(ECDomainParameters curve, BigInteger privateKey, ECPoint publicKey, byte[] userId, SecureRandom random, string tag, ECPoint basePoint = null)
        {
            // EC-JPAKE Schnorr proof: ZKP{x}: publicKey = x*basePoint
            // Following EC-JPAKE protocol: prove knowledge of x such that X = x·basePoint
            // For Round 1: basePoint = G (generator), custom tag
            // For Round 2: basePoint = B (combined point), tag = "JPAKE-A"

            // Default to generator point if no base point specified
            var actualBasePoint = basePoint ?? curve.G;

            // 1. v = random_scalar(1, n-1)
            var v = GenerateRandomScalar(curve, random);

            // 2. V = point_mul(basePoint, v) - commitment using specified base point
            var vPoint = actualBasePoint.Multiply(v).Normalize();

            // 3. c = H(encode(basePoint) || encode(V) || encode(publicKey) || encode(user_id) || tag) mod n
            var challenge = ComputeSchnorrChallenge(actualBasePoint, vPoint, publicKey, userId, tag);

            // 4. r = (v - c * privateKey) mod n
            var r = v.Subtract(challenge.Multiply(privateKey)).Mod(curve.N);

            // ZKP = { V, r }
            return new SchnorrProof { V = vPoint, R = r };
        }

        private BigInteger GenerateRandomScalar(ECDomainParameters curve, SecureRandom random)
        {
            BigInteger scalar;
            do
            {
                scalar = new BigInteger(curve.N.BitLength, random);
            }
            while(scalar.CompareTo(BigInteger.One) <= 0 || scalar.CompareTo(curve.N) >= 0);

            return scalar;
        }

        private ECDomainParameters GetP256CurveParameters()
        {
            // NIST P-256 curve parameters
            var p = new BigInteger("FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF", 16);
            var a = new BigInteger("FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFC", 16);
            var b = new BigInteger("5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B", 16);
            var gx = new BigInteger("6B17D1F2E12C4247F8BCE6E563A440F277037D812DEB33A0F4A13945D898C296", 16);
            var gy = new BigInteger("4FE342E2FE1A7F9B8EE7EB4A7C0F9E162BCE33576B315ECECBB6406837BF51F5", 16);
            var n = new BigInteger("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551", 16);

            var curve = new FpCurve(p, a, b, n, BigInteger.One);
            var g = curve.CreatePoint(gx, gy);
            return new ECDomainParameters(curve, g, n);
        }

        private uint GetTagLength(uint tagLengthOption)
        {
            uint tagLength;
            switch(tagLengthOption)
            {
            case 0x4:
                tagLength = 4;
                break;
            case 0x8:
                tagLength = 8;
                break;
            case 0xC:
                tagLength = 12;
                break;
            case 0xD:
                tagLength = 13;
                break;
            case 0xE:
                tagLength = 14;
                break;
            case 0xF:
                tagLength = 15;
                break;
            default:
                tagLength = 16;
                break;
            }
            return tagLength;
        }

        private bool IsDataLengthValid(uint dataSize, CryptoMode cryptoMode)
        {
            bool lengthOk = false;
            switch(cryptoMode)
            {
            case CryptoMode.Ecb:
            case CryptoMode.Cfb:
            case CryptoMode.Ofb:
                // Must be > 0 and multiple of 16
                lengthOk = ((dataSize > 0) && ((dataSize & 0xF) == 0));
                break;
            case CryptoMode.Cbc:
                // Must be > 16
                lengthOk = (dataSize >= 16);
                break;
            case CryptoMode.Ctr:
                // Must be > 0
                lengthOk = (dataSize > 0);
                break;
            }
            return lengthOk;
        }

        private bool IsNonceSizeValid(uint nonceSize)
        {
            return (nonceSize >= 8 && nonceSize <= 13);
        }

        private bool IsTagSizeValid(uint tagSize)
        {
            // Tag size must be 0, 4, 6, 8, 10, 12, 14 or 16 bytes
            return (tagSize == 0 || tagSize == 4 || tagSize == 6 || tagSize == 8 || tagSize == 10 || tagSize == 12 || tagSize == 14 || tagSize == 16);
        }

        // The firmware-provided counter is the cumulative byte count over the whole hash stream.
        // For SHA-1/224/256 (GeneralDigest) the counter is a single long named "byteCount".
        // For SHA-384/512 (LongDigest) it is split into byteCount1 / byteCount2; we only support up
        // to 4 GiB so byteCount2 stays at zero.
        private bool SetDigestByteCount(IDigest digest, ShaMode hashMode, long count)
        {
            if(ShaIsLongDigest(hashMode))
            {
                var fi1 = LookupDigestField(digest, "byteCount1");
                var fi2 = LookupDigestField(digest, "byteCount2");
                if(fi1 == null || fi2 == null)
                {
                    parent.Log(LogLevel.Error, "SetDigestByteCount(): missing LongDigest counter fields");
                    return false;
                }
                fi1.SetValue(digest, count);
                fi2.SetValue(digest, 0L);
            }
            else
            {
                var fi = LookupDigestField(digest, "byteCount");
                if(fi == null)
                {
                    parent.Log(LogLevel.Error, "SetDigestByteCount(): missing GeneralDigest counter field");
                    return false;
                }
                fi.SetValue(digest, count);
            }
            return true;
        }

        private bool SetDigestRawState(IDigest digest, ShaMode hashMode, byte[] state)
        {
            int count = ShaHWordCount(hashMode);
            bool isLong = ShaIsLongDigest(hashMode);
            int expectedLength = isLong ? count * 8 : count * 4;
            if(state == null || state.Length != expectedLength)
            {
                parent.Log(LogLevel.Error, "SetDigestRawState(): expected {0} bytes, got {1}", expectedLength, state == null ? 0 : state.Length);
                return false;
            }

            for(int i = 0; i < count; i++)
            {
                var fi = LookupDigestField(digest, "H" + (i + 1));
                if(fi == null)
                {
                    parent.Log(LogLevel.Error, "SetDigestRawState(): missing H{0} on {1}", i + 1, digest.GetType().Name);
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

        private byte[] GetDigestRawState(IDigest digest, ShaMode hashMode)
        {
            int count = ShaHWordCount(hashMode);
            bool isLong = ShaIsLongDigest(hashMode);
            byte[] state = new byte[isLong ? count * 8 : count * 4];

            for(int i = 0; i < count; i++)
            {
                var fi = LookupDigestField(digest, "H" + (i + 1));
                if(fi == null)
                {
                    parent.Log(LogLevel.Error, "GetDigestRawState(): missing H{0} on {1}", i + 1, digest.GetType().Name);
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

        private ECPoint DecodeECPointFromBytes(byte[] pointBytes, ECDomainParameters curve)
        {
            if(pointBytes.Length != 64) // 32 bytes x + 32 bytes y for P-256
            {
                throw new ArgumentException("Invalid point byte length");
            }

            // Extract x and y coordinates
            byte[] xBytes = new byte[32];
            byte[] yBytes = new byte[32];
            Array.Copy(pointBytes, 0, xBytes, 0, 32);
            Array.Copy(pointBytes, 32, yBytes, 0, 32);

            var x = new BigInteger(1, xBytes);
            var y = new BigInteger(1, yBytes);

            // Create and validate the point
            var point = curve.Curve.CreatePoint(x, y);
            if(!point.IsValid())
            {
                throw new ArgumentException("Invalid EC point");
            }

            return point.Normalize();
        }

        private void ProcessCommand()
        {
            uint commandHandle = 0;

            if(txFifo.Count == 0)
            {
                parent.Log(LogLevel.Error, "ProcessCommand(): Queue is EMPTY!");
                WriteResponse(ResponseCode.InvalidParameter, commandHandle);
            }

            uint header = txFifo.Dequeue();
            // First 2 bytes of the header is the number of bytes in the message (header included)
            uint wordsCount = (header & 0xFFFF)/4;

            if(txFifo.Count < wordsCount - 1)
            {
                parent.Log(LogLevel.Error, "ProcessCommand(): Not enough words FifoSize={0}, expectedWords={1}", txFifo.Count, wordsCount - 1);
                WriteResponse(ResponseCode.InvalidParameter, commandHandle);
            }

            if(series3)
            {
                commandHandle = txFifo.Dequeue();
            }

            uint commandOptions = txFifo.Dequeue();
            var commandId = (CommandId)(commandOptions >> 16);
            commandOptions &= 0xFFFF;
            uint inputDmaDescriptorPtr = txFifo.Dequeue();
            uint outputDmaDescriptorPtr = txFifo.Dequeue();
            uint commandParamsCount = wordsCount - (series3 ? 5U : 4U);
            uint[] commandParams = new uint[13];
            for(var i = 0; i < commandParamsCount; i++)
            {
                commandParams[i] = txFifo.Dequeue();
            }

            parent.Log(LogLevel.Debug, "ProcessCommand(): command ID={0} command Options=0x{1:X} command params count={2}", commandId, commandOptions, commandParamsCount);

            ResponseCode responseCode;

            switch(commandId)
            {
            case CommandId.CreateKey:
                responseCode = HandleCreateKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                break;
            case CommandId.ReadPublicKey:
                responseCode = HandleReadPublicKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                break;
            case CommandId.ImportKey:
                responseCode = HandleImportKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                break;
            case CommandId.ExportKey:
                responseCode = HandleExportKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                break;
            case CommandId.TransferKey:
                responseCode = HandleTransferKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                break;
            case CommandId.DeleteKey:
                responseCode = HandleDeleteKeyCommand(commandParams, commandParamsCount);
                break;
            case CommandId.Hash:
                responseCode = HandleHashCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.HashUpdate:
            case CommandId.HashFinish:
                responseCode = HandleHashUpdateOrFinishCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions, (commandId == CommandId.HashFinish));
                break;
            case CommandId.HashHmac:
                responseCode = HandleHashHmacCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.AesEncrypt:
            case CommandId.AesDecrypt:
                responseCode = HandleAesEncryptOrDecryptCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions, (commandId == CommandId.AesEncrypt));
                break;
            case CommandId.AesCmac:
                responseCode = HandleAesCmacCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                break;
            case CommandId.AesCcmEncrypt:
            case CommandId.AesCcmDecrypt:
                responseCode = HandleAesCcmEncryptOrDecryptCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions, (commandId == CommandId.AesCcmEncrypt));
                break;
            case CommandId.AesGcmEncrypt:
                responseCode = HandleAesGcmEncryptCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.AesGcmDecrypt:
                responseCode = HandleAesGcmDecryptCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.SignatureSign:
                responseCode = HandleSignatureCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions, isVerify: false, isEddsa: false);
                break;
            case CommandId.SignatureVerify:
                responseCode = HandleSignatureCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions, isVerify: true, isEddsa: false);
                break;
            case CommandId.EddsaSign:
                responseCode = HandleSignatureCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions, isVerify: false, isEddsa: true);
                break;
            case CommandId.EddsaVerify:
                responseCode = HandleSignatureCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions, isVerify: true, isEddsa: true);
                break;
            case CommandId.Random:
                responseCode = HandleRandomCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                break;
            case CommandId.DiffieHellman:
                responseCode = HandleDiffieHellmanCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount);
                break;
            case CommandId.JPakeRound1:
                responseCode = HandleJPakeRound1Command(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.JPakeRound2:
                responseCode = HandleJPakeRound2Command(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.JPakeGenerateSessionKey:
                responseCode = HandleJPakeGenerateSessionKeyCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.ReadDeviceData:
                responseCode = HandleReadDeviceDataCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.UserDataStorage:
                responseCode = HandleUserDataStorageCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.FlashEraseDataRegion:
                responseCode = HandleFlashEraseDataRegionCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.FlashWriteDataRegion:
                responseCode = HandleFlashWriteDataRegionCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.FlashGetDataRegionLocation:
                responseCode = HandleFlashGetDataRegionLocationCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.FlashGetCodeRegionConfig:
                responseCode = HandleFlashGetCodeRegionConfigCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.ConfigureQspiRefClock:
                responseCode = HandleConfigureQspiRefClockCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.SetVectorTable:
                responseCode = HandleSetVectorTableCommand(inputDmaDescriptorPtr, outputDmaDescriptorPtr, commandParams, commandParamsCount, commandOptions);
                break;
            case CommandId.ConfigureSpiMemoryDeepPowerDownMode:
                // No-op for now
                responseCode = ResponseCode.Ok;
                break;
            default:
                responseCode = ResponseCode.InvalidCommand;
                parent.Log(LogLevel.Error, "ProcessCommand(): Command ID 0x{0:X} not handled!", (uint)commandId);
                break;
            }

            if(responseCode != ResponseCode.Ok)
            {
                parent.Log(LogLevel.Debug, "ProcessCommand(): Response code {0}", responseCode);
            }

            WriteResponse(responseCode, commandHandle);
        }

        private void WriteResponse(ResponseCode code, uint commandHandle)
        {
            rxFifo.Enqueue((uint)code);
            if(series3)
            {
                rxFifo.Enqueue(commandHandle);
            }
        }

        private ResponseCode HandleAesEncryptOrDecryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions, bool encrypt)
        {
            if(commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "AES_ENCRYPT/DECRYPT: invalid parameter count");
                return ResponseCode.Abort;
            }

            var cryptoMode = (CryptoMode)((commandOptions & 0xF00) >> 8);
            var contextMode = (ContextMode)(commandOptions & 0xF);

            parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: cryptoMode={0} contextMode={1}", cryptoMode, contextMode);

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);
            if(keyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "AES_ENCRYPT/DECRYPT: unsupported key type {0} (expected Raw)", keyType);
                return ResponseCode.InvalidParameter;
            }
            uint dataSize = commandParams[1];
            parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: dataSize={0}", dataSize);

            // Check that the length is compatible with the crypto mode
            if(!IsDataLengthValid(dataSize, cryptoMode))
            {
                return ResponseCode.InvalidParameter;
            }

            // Input DMA descriptor chain follows one of two valid SEMAILBOX layouts:
            // - Key-first: InputKey_t plaintext key (RawKeyData_t for KEY_MODE_UNPROTECT), then
            //              optional IV/context, then data. This matches the documented 0x0400/0x0401
            //              data payload when no leading auth buffer is present.
            // - Auth-then-key: auth buffer (0 or 8 bytes), then key material, then optional
            //                  IV/context, then data. Used by SE Manager-style callers.
            uint firstDescPtr;
            uint firstDescLength;
            uint secondDescPtr;
            uint secondDescLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out firstDescPtr, out firstDescLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: INPUT0: firstDescPtr=0x{0:X} firstDescLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     firstDescPtr, firstDescLength, transferOptions, nextDescriptorPtr);

            uint keyPtr;
            uint keyLength;
            uint expectedKeyLength;
            UnpackRawKeyMetadata(commandParams[0], out expectedKeyLength);
            bool inputStartsWithKey = (keyMode == KeyMode.Unprotected
                                       && firstDescLength == expectedKeyLength);
            if(inputStartsWithKey)
            {
                keyPtr = firstDescPtr;
                keyLength = firstDescLength;
                parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: INPUT0 is key (unprotected InputKey_t), keyLength={0}", keyLength);
            }
            else
            {
                // Second input DMA descriptor contains the key
                UnpackDmaDescriptor(nextDescriptorPtr, out secondDescPtr, out secondDescLength, out transferOptions, out nextDescriptorPtr, machine);
                keyPtr = secondDescPtr;
                keyLength = secondDescLength;
                parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                         keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            }
            byte[] key = ReadKey(keyMode, keyIndex, keyPtr, keyLength);
            parent.Log(LogLevel.Debug, "Key=[{0}]", BitConverter.ToString(key));
            var keyParameter = new KeyParameter(key);

            // Third input DMA descriptor contains the IV (for whole or start of message) or context (for middle/end)
            // The input IV/Context size is 0 for ECB mode and 16 for all other modes
            ParametersWithIV parametersWithIV = null;
            if(cryptoMode != CryptoMode.Ecb)
            {
                uint inputIvPtr = 0;
                uint inputIvLength = 0;
                UnpackDmaDescriptor(nextDescriptorPtr, out inputIvPtr, out inputIvLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: INPUT2: inputIvPtr=0x{0:X} inputIvLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        inputIvPtr, inputIvLength, transferOptions, nextDescriptorPtr);
                byte[] inputIv = new byte[inputIvLength];
                FetchFromRam(inputIvPtr, inputIv, 0, inputIvLength);
                parent.Log(LogLevel.Debug, "InputIv=[{0}]", BitConverter.ToString(inputIv));
                parametersWithIV = new ParametersWithIV(keyParameter, inputIv);
            }

            // Fourth input DMA descriptor(s) contains input data
            uint inputDataPtr;
            uint inputDataLength;
            byte[] inputData = new byte[dataSize];
            uint inputDataOffset = 0;

            while(inputDataOffset < dataSize)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: INPUT3: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);
                if(transferOptions != DmaTransferOptions.Discard)
                {
                    FetchFromRam(inputDataPtr, inputData, inputDataOffset, inputDataLength);
                    parent.Log(LogLevel.Debug, "InputData=[{0}]", BitConverter.ToString(inputData));
                    inputDataOffset += inputDataLength;
                }
            }

            // First output DMA descriptor(s) contains output data
            uint outputDataOffset = 0;
            nextDescriptorPtr = outputDma;
            while(outputDataOffset < dataSize)
            {
                uint tempOutputDataPtr;
                uint tempOutputDataLength;
                UnpackDmaDescriptor(nextDescriptorPtr, out tempOutputDataPtr, out tempOutputDataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        tempOutputDataPtr, tempOutputDataLength, transferOptions, nextDescriptorPtr);
                outputDataOffset += tempOutputDataLength;
            }

            // Second output DMA descriptor contains the context of block encryption for passing to next encryption/dceryption call.
            // The output context length is 0 if ECB mode is used or the mode is last/whole. It is 16 otherwise
            uint outputContextPtr = 0;
            uint outputContextLength = 0;
            if(cryptoMode != CryptoMode.Ecb && contextMode != ContextMode.WholeMessage && contextMode != ContextMode.EndOfMessage)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out outputContextPtr, out outputContextLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: OUTPUT1: outputContextPtr=0x{0:X} outputContextLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        outputContextPtr, outputContextLength, transferOptions, nextDescriptorPtr);
            }

            IBlockCipher cipher;
            switch(cryptoMode)
            {
            case CryptoMode.Cbc:
                cipher = new CbcBlockCipher(new AesEngine());
                cipher.Init(encrypt, parametersWithIV);
                break;
            case CryptoMode.Ecb:
                cipher = new AesEngine();
                // The input IV length is always 0 when using ECB
                cipher.Init(encrypt, keyParameter);
                break;
            case CryptoMode.Ctr: // TODOs
            case CryptoMode.Cfb:
            case CryptoMode.Ofb:
            default:
                parent.Log(LogLevel.Error, "AES_ENCRYPT/DECRYPT: invalid crypto mode");
                return ResponseCode.Abort;
            }

            byte[] outputData = new byte[dataSize];
            for(int i = 0; i < dataSize; i += 16)
            {
                cipher.ProcessBlock(inputData, i, outputData, i);
            }

            outputDataOffset = 0;
            nextDescriptorPtr = outputDma;
            while(outputDataOffset < dataSize)
            {
                uint tempOutputDataPtr;
                uint tempOutputDataLength;
                UnpackDmaDescriptor(nextDescriptorPtr, out tempOutputDataPtr, out tempOutputDataLength, out transferOptions, out nextDescriptorPtr, machine);
                if(transferOptions != DmaTransferOptions.Discard)
                {
                    WriteToRam(outputData, outputDataOffset, tempOutputDataPtr, tempOutputDataLength);
                    parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: Writing output data: length={0} offset={1} at location {2:X}",
                            tempOutputDataLength, outputDataOffset, tempOutputDataPtr);
                    outputDataOffset += tempOutputDataLength;
                }
            }

            parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: Output data=[{0}]", BitConverter.ToString(outputData));

            // The output iv_size is 0 if ECB mode is used or the mode is last/whole. It is 16 otherwise
            if(cryptoMode != CryptoMode.Ecb && contextMode != ContextMode.EndOfMessage && contextMode != ContextMode.WholeMessage)
            {
                // TODO: Write the output IV
                // - RENODE-46: For CBC mode, the output IV is the cbcV

                // For now we just do a +1 on the input IV
                byte[] iv = parametersWithIV.GetIV();
                for(uint i = 0; i < outputContextLength; i++)
                {
                    iv[i] = (byte)(iv[i] + 1);
                }
                WriteToRam(iv, 0, outputContextPtr, outputContextLength);
                parent.Log(LogLevel.Debug, "AES_ENCRYPT/DECRYPT: Output context=[{0}]", BitConverter.ToString(iv));
            }
            return ResponseCode.Ok;
        }

        private ResponseCode HandleTransferKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 2)
            {
                throw new NotSupportedException("TRANSFER_KEY: invalid parameter count");
            }

            KeyMode srcKeyMode;
            KeyType srcKeyType;
            KeyRestriction srcKeyRestriction;
            uint srcKeyIndex;

            UnpackKeyMetadata(commandParams[0], out srcKeyIndex, out srcKeyType, out srcKeyMode, out srcKeyRestriction);

            // Extract the update mode and index from keyUpdateMetadata
            uint keyUpdateMetadata = commandParams[1];
            var dstKeyMode = (KeyMode)((keyUpdateMetadata >> 8) & 0x3);
            uint dstKeyIndex = keyUpdateMetadata & 0xFF;

            parent.Log(LogLevel.Debug, "TRANSFER_KEY: SRC Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                       srcKeyIndex, srcKeyType, srcKeyMode, srcKeyRestriction);
            parent.Log(LogLevel.Debug, "TRANSFER_KEY: DST Key Metadata: keyIndex={0} keyMode={1}",
                       dstKeyIndex, dstKeyMode);

            // Input DMA descriptor chain follows the firmware layout in sl_se_transfer_key():
            //   INPUT0: source key auth buffer    (sli_add_key_metadata key_in)
            //                                     -> 0 bytes for plaintext/internal source,
            //                                        8 bytes (default_auth_data) for wrapped/volatile source
            //   INPUT1: source key data           (sli_add_key_input key_in)
            //                                     -> plaintext key bytes, or wrapped blob
            //                                        (12 IV + key + 16 tag), or 0 bytes for
            //                                        internal storage (volatile / immutable / KSU)
            //   INPUT2: destination key auth buf  (sli_se_get_auth_buffer key_out)
            //                                     -> 0 bytes for plaintext destination,
            //                                        8 bytes for wrapped/volatile destination
            uint srcAuthPtr;
            uint srcAuthLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out srcAuthPtr, out srcAuthLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "TRANSFER_KEY: INPUT0: srcAuthPtr=0x{0:X} srcAuthLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     srcAuthPtr, srcAuthLength, transferOptions, nextDescriptorPtr);

            uint srcKeyPtr;
            uint srcKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out srcKeyPtr, out srcKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "TRANSFER_KEY: INPUT1: srcKeyPtr=0x{0:X} srcKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     srcKeyPtr, srcKeyLength, transferOptions, nextDescriptorPtr);

            uint dstAuthPtr;
            uint dstAuthLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out dstAuthPtr, out dstAuthLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "TRANSFER_KEY: INPUT2: dstAuthPtr=0x{0:X} dstAuthLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     dstAuthPtr, dstAuthLength, transferOptions, nextDescriptorPtr);

            // Retrieve the source key
            byte[] sourceKey = ReadKey(srcKeyMode, srcKeyIndex, srcKeyPtr, srcKeyLength);
            if(sourceKey == null)
            {
                return ResponseCode.InvalidParameter;
            }

            parent.Log(LogLevel.Debug, "TRANSFER_KEY: SourceKey=[{0}]", BitConverter.ToString(sourceKey));

            // Write the key to destination and possibly output it to the output descriptor (only for unprotected/wrapped destination)
            byte[] outputKey = null;
            switch(dstKeyMode)
            {
            case KeyMode.Unprotected:
            {
                outputKey = sourceKey;
                break;
            }
            case KeyMode.Wrapped:
            {
                uint offset = 0;
                outputKey = new byte[sourceKey.Length + 12 + 16];

                // - 12 bytes: random IV assigned at time of wrapping
                // - var bytes: encrypted key data
                // - 16 bytes: AESGCM authentication tag
                // As of now, we leave the key stored as plain text, so we just grab that.
                FillArray(outputKey, 0, 12, WrappedKeyRandomIVMarker);
                offset += 12;
                Array.Copy(sourceKey, 0, outputKey, (int)offset, sourceKey.Length);
                offset += (uint)sourceKey.Length;
                FillArray(outputKey, offset, 16, WrappedKeyAuthenticationTagMarker);
                break;
            }
            case KeyMode.Volatile:
            {
                volatileKeys[dstKeyIndex] = sourceKey;
                break;
            }
            case KeyMode.Ksu:
            {
                ksuStorage.AddKey(dstKeyIndex, sourceKey);
                break;
            }
            default:
                parent.Log(LogLevel.Error, "TRANSFER_KEY: Invalid destination key mode={0}", dstKeyMode);
                return ResponseCode.InvalidParameter;
            }

            if(outputKey != null)
            {
                // Output DMA descriptor contains the new key data
                uint outputKeyDataPtr;
                uint outputKeyDataLength;
                UnpackDmaDescriptor(outputDma, out outputKeyDataPtr, out outputKeyDataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "TRANSFER_KEY: OUTPUT0: outputKeyDataPtr=0x{0:X} outputKeyDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                           outputKeyDataPtr, outputKeyDataLength, transferOptions, nextDescriptorPtr);

                // Write the new key data to the output DMA
                WriteToRam(outputKey, 0, outputKeyDataPtr, outputKeyDataLength);
                parent.Log(LogLevel.Debug, "TRANSFER_KEY: OutputDescLength={0} outputKeyLength={1} OutputKeyData=[{2}]", outputKeyDataLength, outputKey.Length, BitConverter.ToString(outputKey));
            }

            return ResponseCode.Ok;
        }

        private ResponseCode HandleHashCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if(commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "HASH: invalid parameter count");
                return ResponseCode.Abort;
            }

            var hashMode = (ShaMode)((commandOptions & 0xF00) >> 8);
            uint dataSize = commandParams[0];

            parent.Log(LogLevel.Debug, "HASH: mode={0} dataSize={1}", hashMode, dataSize);

            DmaTransferOptions transferOptions;
            uint inputPayloadDescriptorPtr;
            uint inputPayloadLength;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out inputPayloadDescriptorPtr, out inputPayloadLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "HASH: INPUT0: payloadPtr=0x{0:X} payloadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    inputPayloadDescriptorPtr, inputPayloadLength, transferOptions, nextDescriptorPtr);
            byte[] payload = new byte[inputPayloadLength];
            FetchFromRam(inputPayloadDescriptorPtr, payload, 0, inputPayloadLength);
            parent.Log(LogLevel.Debug, "Payload=[{0}]", BitConverter.ToString(payload));

            uint outputDigestDescriptorPtr;
            uint outputDigestLength;
            UnpackDmaDescriptor(outputDma, out outputDigestDescriptorPtr, out outputDigestLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "HASH: OUTPUT0: payloadPtr=0x{0:X} payloadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    outputDigestDescriptorPtr, outputDigestLength, transferOptions, nextDescriptorPtr);
            byte[] digest = new byte[outputDigestLength];

            var hashEngine = CreateHashEngine(hashMode);

            if(hashEngine == null)
            {
                parent.Log(LogLevel.Error, "HASH: unable to create hashing engine");
                return ResponseCode.Abort;
            }

            if(hashEngine.GetDigestSize() != outputDigestLength)
            {
                parent.Log(LogLevel.Error, "HASH: digest size mismatch");
                return ResponseCode.Abort;
            }

            hashEngine.BlockUpdate(payload, 0, (int)inputPayloadLength);
            hashEngine.DoFinal(digest, 0);

            parent.Log(LogLevel.Debug, "Digest=[{0}]", BitConverter.ToString(digest));
            WriteToRam(digest, 0, outputDigestDescriptorPtr, outputDigestLength);
            return ResponseCode.Ok;
        }

        private ResponseCode HandleHashUpdateOrFinishCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions, bool doFinish)
        {
            // HASHUPDATE (0x0301) sends a single parameter (data length) and HASHFINISH (0x0303)
            // sends two parameters (remaining bytes, total byte counter).
            uint expectedParamCount = doFinish ? 2U : 1U;
            if(commandParamsCount != expectedParamCount)
            {
                parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: invalid parameter count {0} (expected {1})",
                          commandParamsCount, expectedParamCount);
                return ResponseCode.Abort;
            }

            var hashMode = (ShaMode)((commandOptions & 0xF00) >> 8);
            uint dataSize = commandParams[0];
            uint totalCounter = doFinish ? commandParams[1] : 0;
            uint stateSize = HashEngineStateSize(hashMode);
            int blockSize = ShaIsLongDigest(hashMode) ? 128 : 64;

            parent.Log(LogLevel.Debug, "HASH_UPDATE/FINISH: mode={0} dataSize={1} totalCounter={2} doFinish={3}",
                      hashMode, dataSize, totalCounter, doFinish);

            if(stateSize == 0)
            {
                parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: invalid hash mode {0}", hashMode);
                return ResponseCode.InvalidParameter;
            }

            // HASHUPDATE always carries whole-block payloads (64 or 128 bytes depending on the
            // algorithm). HASHFINISH carries 0..blockSize-1 bytes of the residual buffer.
            if(!doFinish && (dataSize % blockSize) != 0)
            {
                parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: invalid dataSize={0} for hashMode={1}", dataSize, hashMode);
                return ResponseCode.InvalidParameter;
            }
            if(doFinish && dataSize >= blockSize)
            {
                parent.Log(LogLevel.Error, "HASH_FINISH: residual buffer size {0} >= block size {1}", dataSize, blockSize);
                return ResponseCode.InvalidParameter;
            }

            DmaTransferOptions transferOptions;
            uint inputStateDescriptorPtr;
            uint inputStateLength;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out inputStateDescriptorPtr, out inputStateLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "HASH_UPDATE/FINISH: INPUT0: statePtr=0x{0:X} inputStateLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    inputStateDescriptorPtr, inputStateLength, transferOptions, nextDescriptorPtr);
            if(inputStateLength != stateSize)
            {
                parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: input state length {0} != expected {1}", inputStateLength, stateSize);
                return ResponseCode.InvalidParameter;
            }
            byte[] inputState = new byte[inputStateLength];
            FetchFromRam(inputStateDescriptorPtr, inputState, 0, inputStateLength);
            parent.Log(LogLevel.Debug, "InputState=[{0}]", BitConverter.ToString(inputState));

            uint inputPayloadDescriptorPtr;
            uint inputPayloadLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputPayloadDescriptorPtr, out inputPayloadLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "HASH_UPDATE/FINISH: INPUT1: payloadPtr=0x{0:X} payloadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    inputPayloadDescriptorPtr, inputPayloadLength, transferOptions, nextDescriptorPtr);
            if(inputPayloadLength != dataSize)
            {
                parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: payload descriptor length {0} != param dataSize {1}", inputPayloadLength, dataSize);
                return ResponseCode.InvalidParameter;
            }
            byte[] payload = new byte[inputPayloadLength];
            FetchFromRam(inputPayloadDescriptorPtr, payload, 0, inputPayloadLength);
            parent.Log(LogLevel.Debug, "Payload=[{0}]", BitConverter.ToString(payload));

            uint outputDescriptorPtr;
            uint outputLength;
            UnpackDmaDescriptor(outputDma, out outputDescriptorPtr, out outputLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "HASH_UPDATE/FINISH: OUTPUT0: payloadPtr=0x{0:X} payloadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    outputDescriptorPtr, outputLength, transferOptions, nextDescriptorPtr);

            var hashEngine = CreateHashEngine(hashMode);
            if(hashEngine == null)
            {
                parent.Log(LogLevel.Error, "HASH_UPDATE/FINISH: unable to create hashing engine");
                return ResponseCode.Abort;
            }

            // Restore H1..Hn from the firmware-supplied state, then replay this update's payload.
            // The simulator is fully stateless across SE commands: every call rebuilds the engine
            // from the host-provided context, which matches what real hardware does.
            if(!SetDigestRawState(hashEngine, hashMode, inputState))
            {
                return ResponseCode.Abort;
            }
            hashEngine.BlockUpdate(payload, 0, (int)inputPayloadLength);

            if(doFinish)
            {
                if(hashEngine.GetDigestSize() != outputLength)
                {
                    parent.Log(LogLevel.Error, "HASH_FINISH: digest size mismatch (engine={0}, output={1})",
                              hashEngine.GetDigestSize(), outputLength);
                    return ResponseCode.Abort;
                }
                // The SHA padding embeds the total bit length; the firmware passes the cumulative
                // byte count separately so the digest matches what real hardware would produce
                // regardless of how the message was split across HASHUPDATE calls.
                if(!SetDigestByteCount(hashEngine, hashMode, totalCounter))
                {
                    return ResponseCode.Abort;
                }
                byte[] output = new byte[outputLength];
                hashEngine.DoFinal(output, 0);

                parent.Log(LogLevel.Debug, "Digest=[{0}]", BitConverter.ToString(output));
                WriteToRam(output, 0, outputDescriptorPtr, outputLength);
            }
            else
            {
                if(outputLength != stateSize)
                {
                    parent.Log(LogLevel.Error, "HASH_UPDATE: output state size {0} != expected {1}", outputLength, stateSize);
                    return ResponseCode.Abort;
                }
                byte[] outputState = GetDigestRawState(hashEngine, hashMode);
                if(outputState == null)
                {
                    return ResponseCode.Abort;
                }
                parent.Log(LogLevel.Debug, "OutputState=[{0}]", BitConverter.ToString(outputState));
                WriteToRam(outputState, 0, outputDescriptorPtr, outputLength);
            }
            return ResponseCode.Ok;
        }

        private ResponseCode HandleHashHmacCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if(commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "HASH_HMAC: invalid parameter count");
                return ResponseCode.Abort;
            }

            var hashMode = (ShaMode)((commandOptions & 0xF00) >> 8);
            uint messageLength = commandParams[1];

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "HASH_HMAC: hashMode={0} messageLength={1} keyIndex={2} keyType={3} keyMode={4} keyRestriction={5}",
                     hashMode, messageLength, keyIndex, keyType, keyMode, keyRestriction);
            if(keyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "HASH_HMAC: unsupported key type {0} (expected Raw)", keyType);
                return ResponseCode.InvalidParameter;
            }

            // First input DMA descriptor is the authorization data (0 bytes for unprotected key)
            uint authPtr;
            uint authLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "HASH_HMAC: INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "HASH_HMAC: INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = ReadKey(keyMode, keyIndex, keyPtr, keyLength);
            parent.Log(LogLevel.Debug, "Key=[{0}]", BitConverter.ToString(key));

            // Third input DMA descriptor contains the message
            uint messagePtr;
            uint messageDescLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out messagePtr, out messageDescLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "HASH_HMAC: INPUT2: messagePtr=0x{0:X} messageLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     messagePtr, messageDescLength, transferOptions, nextDescriptorPtr);
            byte[] message = new byte[messageLength];
            FetchFromRam(messagePtr, message, 0, messageLength);
            parent.Log(LogLevel.Debug, "Message=[{0}]", BitConverter.ToString(message));

            // Output DMA descriptor holds the HMAC digest
            uint outputPtr;
            uint outputLength;
            UnpackDmaDescriptor(outputDma, out outputPtr, out outputLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "HASH_HMAC: OUTPUT0: outputPtr=0x{0:X} outputLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputPtr, outputLength, transferOptions, nextDescriptorPtr);

            var digest = CreateHashEngine(hashMode);
            if(digest == null)
            {
                parent.Log(LogLevel.Error, "HASH_HMAC: unable to create hashing engine");
                return ResponseCode.Abort;
            }

            if(digest.GetDigestSize() != outputLength)
            {
                parent.Log(LogLevel.Error, "HASH_HMAC: output size mismatch (engine={0}, output={1})", digest.GetDigestSize(), outputLength);
                return ResponseCode.Abort;
            }

            var hmac = new HMac(digest);
            hmac.Init(new KeyParameter(key));
            hmac.BlockUpdate(message, 0, (int)messageLength);
            byte[] mac = new byte[hmac.GetMacSize()];
            hmac.DoFinal(mac, 0);
            parent.Log(LogLevel.Debug, "HASH_HMAC: MAC=[{0}]", BitConverter.ToString(mac));
            WriteToRam(mac, 0, outputPtr, outputLength);
            return ResponseCode.Ok;
        }

        private ResponseCode HandleAesCmacCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "AES_CMAC: invalid parameter count");
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "AES_CMAC: Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);
            if(keyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "AES_CMAC: unsupported key type {0} (expected Raw)", keyType);
                return ResponseCode.InvalidParameter;
            }
            uint inputDataSize = commandParams[1];
            parent.Log(LogLevel.Debug, "AES_CMAC: dataSize={0}", inputDataSize);

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_CMAC: INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key. 
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_CMAC: INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = ReadKey(keyMode, keyIndex, keyPtr, keyLength);
            parent.Log(LogLevel.Debug, "Key=[{0}]", BitConverter.ToString(key));
            var keyParameter = new KeyParameter(key);

            // Third input DMA descriptor contains the input data
            uint inputDataPtr;
            uint inputDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_CMAC: INPUT2: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);
            byte[] inputData = new byte[inputDataLength];
            FetchFromRam(inputDataPtr, inputData, 0, inputDataLength);
            parent.Log(LogLevel.Debug, "InputData=[{0}]", BitConverter.ToString(inputData));

            // First output DMA descriptor contains the output MAC
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_CMAC: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);
            byte[] outputData = new byte[outputDataLength];

            var cmac = new CMac(new AesEngine());
            cmac.Init(keyParameter);
            cmac.BlockUpdate(inputData, 0, (int)inputDataLength);
            cmac.DoFinal(outputData, 0);
            WriteToRam(outputData, 0, outputDataPtr, outputDataLength);
            parent.Log(LogLevel.Debug, "AES_CMAC: Output MAC=[{0}]", BitConverter.ToString(outputData));
            return ResponseCode.Ok;
        }

        private ResponseCode HandleExportKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "EXPORT_KEY: invalid parameter count");
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "EXPORT_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                       keyIndex, keyType, keyMode, keyRestriction);

            if(keyMode == KeyMode.Unprotected || keyMode == KeyMode.Ksu)
            {
                parent.Log(LogLevel.Error, "EXPORT_KEY: invalid key mode {0}", keyMode);
                return ResponseCode.InvalidParameter;
            }

            if(keyRestriction != KeyRestriction.Unlocked)
            {
                parent.Log(LogLevel.Error, "EXPORT_KEY: key is not Unlocked (restriction={0})", keyRestriction);
                return ResponseCode.AuthorizationError;
            }

            // For Wrapped, the source envelope lives in the input DMA (auth desc + wrapped key desc).
            // For Volatile/KSU, the key is resolved from internal storage and the input DMA is unused.
            uint sourcePtr = 0;
            uint sourceLength = 0;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            if(keyMode == KeyMode.Wrapped)
            {
                uint authDataPtr;
                uint authDataLength;
                UnpackDmaDescriptor(inputDma, out authDataPtr, out authDataLength, out transferOptions, out nextDescriptorPtr, machine);
                UnpackDmaDescriptor(nextDescriptorPtr, out sourcePtr, out sourceLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "EXPORT_KEY: wrappedKeyPtr=0x{0:X} keyLength={1}", sourcePtr, sourceLength);
            }

            byte[] key = ReadKey(keyMode, keyIndex, sourcePtr, sourceLength);
            if(key == null)
            {
                return ResponseCode.InvalidParameter;
            }
            parent.Log(LogLevel.Debug, "EXPORT_KEY: Key=[{0}]", BitConverter.ToString(key));

            // Output DMA receives the plaintext key.
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "EXPORT_KEY: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            if(outputDataLength != key.Length)
            {
                parent.Log(LogLevel.Error, "EXPORT_KEY: output buffer length mismatch got={0} expected={1}", outputDataLength, key.Length);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(key, 0, outputDataPtr, (uint)key.Length);
            return ResponseCode.Ok;
        }

        private ResponseCode HandleAesGcmEncryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if(commandParamsCount != 3)
            {
                parent.Log(LogLevel.Error, "AES_GCM_ENCRYPT: invalid parameter count");
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "AES_GCM_ENCRYPT: Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                    keyIndex, keyType, keyMode, keyRestriction);
            if(keyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "AES_GCM_ENCRYPT: unsupported key type {0} (expected Raw)", keyType);
                return ResponseCode.InvalidParameter;
            }
            uint associatedAuthenticatedDataSize = commandParams[1];
            uint inputDataSize = commandParams[2];

            // Extract Command Options
            uint tagLength = GetTagLength((commandOptions >> 8) & 0xFF);
            var contextMode = (ContextMode)(commandOptions & 0xFF);

            parent.Log(LogLevel.Debug, "AES_GCM_ENCRYPT: Command Params: aadSize={0} inputDataSize={1} Command Options: contextMode={2} tagLength={3}",
                    associatedAuthenticatedDataSize, inputDataSize, contextMode, tagLength);

            if(contextMode != ContextMode.WholeMessage)
            {
                parent.Log(LogLevel.Error, "AES_GCM_ENCRYPT: only support WholeMessage context mode");
                return ResponseCode.Abort;
            }

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_ENCRYPT: INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_ENCRYPT: INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = ReadKey(keyMode, keyIndex, keyPtr, keyLength);
            parent.Log(LogLevel.Debug, "Key=[{0}]", BitConverter.ToString(key));
            var keyParameter = new KeyParameter(key);

            // Third input DMA descriptor contains the IV (for Whole or Start) or context data (for Middle or End)
            uint ivPtr;
            uint ivLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out ivPtr, out ivLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_ENCRYPT: INPUT2: ivPtr=0x{0:X} ivLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    ivPtr, ivLength, transferOptions, nextDescriptorPtr);
            byte[] iv = new byte[ivLength];
            FetchFromRam(ivPtr, iv, 0, ivLength);
            parent.Log(LogLevel.Debug, "IV/Context=[{0}]", BitConverter.ToString(iv));

            // The iv_size is 12 for Whole and Start of message (= initial IV) and 32 for Middle/End of message (= context input).
            // For now we only support Whole, so iv_size should always be 12.
            if(ivLength != 12)
            {
                parent.Log(LogLevel.Error, "AES_GCM_ENCRYPT: invalid IV length");
                return ResponseCode.Abort;
            }

            // Fourth input DMA descriptor contains the associated authenticated data
            uint aadPtr;
            uint aadLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out aadPtr, out aadLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_ENCRYPT: INPUT3: aadPtr=0x{0:X} aadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    aadPtr, aadLength, transferOptions, nextDescriptorPtr);
            byte[] aad = new byte[aadLength];
            FetchFromRam(aadPtr, aad, 0, aadLength);
            parent.Log(LogLevel.Debug, "Aad=[{0}]", BitConverter.ToString(aad));

            // Fifth input DMA descriptor contains the plaintext input data
            uint inputDataPtr;
            uint inputDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_ENCRYPT: INPUT4: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);
            byte[] inputData = new byte[inputDataLength];
            FetchFromRam(inputDataPtr, inputData, 0, inputDataLength);
            parent.Log(LogLevel.Debug, "InputData=[{0}]", BitConverter.ToString(inputData));

            // Sixth input DMA descriptor contains the len(A)||len(C) (if applicable)
            uint lenAlenCPtr = 0;
            uint lenAlenCLength = 0;
            if(nextDescriptorPtr != NullDescriptor)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out lenAlenCPtr, out lenAlenCLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "AES_GCM_ENCRYPT: INPUT5: lenAlenCPtr=0x{0:X} lenAlenCLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        lenAlenCPtr, lenAlenCLength, transferOptions, nextDescriptorPtr);
            }
            byte[] lenAlenC = new byte[lenAlenCLength];
            if(lenAlenCLength > 0)
            {
                FetchFromRam(lenAlenCPtr, lenAlenC, 0, lenAlenCLength);
                parent.Log(LogLevel.Debug, "LenAlenC=[{0}]", BitConverter.ToString(lenAlenC));
            }

            // First output DMA descriptor contains the encrypted output data
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_ENCRYPT: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            // Second output DMA descriptor contains the context or tag.
            uint outputContextPtr;
            uint outputContextLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out outputContextPtr, out outputContextLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_ENCRYPT: OUTPUT1: outputContextPtr=0x{0:X} outputContextLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    outputContextPtr, outputContextLength, transferOptions, nextDescriptorPtr);

            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(keyParameter, (int)tagLength*8, iv, aad);
            cipher.Init(true, parameters);

            byte[] outputDataAndTag = new byte[outputDataLength + tagLength];
            int len = cipher.ProcessBytes(inputData, 0, (int)inputDataLength, outputDataAndTag, 0);
            len += cipher.DoFinal(outputDataAndTag, len);
            parent.Log(LogLevel.Debug, "EncryptedDataAndTag=[{0}]", BitConverter.ToString(outputDataAndTag));
            WriteToRam(outputDataAndTag, 0, outputDataPtr, outputDataLength);

            // For End/Whole mode, the output context is the MAC.
            parent.Log(LogLevel.Debug, "MAC=[{0}]", BitConverter.ToString(cipher.GetMac()));
            WriteToRam(cipher.GetMac(), 0, outputContextPtr, outputContextLength);

            return ResponseCode.Ok;
        }

        private ResponseCode HandleAesGcmDecryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if(commandParamsCount != 3)
            {
                parent.Log(LogLevel.Error, "AES_GCM_DECRYPT: invalid parameter count");
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "AES_GCM_DECRYPT: Key Metadata: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                    keyIndex, keyType, keyMode, keyRestriction);
            if(keyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "AES_GCM_DECRYPT: unsupported key type {0} (expected Raw)", keyType);
                return ResponseCode.InvalidParameter;
            }
            uint associatedAuthenticatedDataSize = commandParams[1];
            uint inputDataSize = commandParams[2];

            // Extract Command Options
            uint tagLength = GetTagLength((commandOptions >> 8) & 0xFF);
            var contextMode = (ContextMode)(commandOptions & 0xFF);

            parent.Log(LogLevel.Debug, "AES_GCM_DECRYPT: Command Params: aadSize={0} inputDataSize={1} Command Options: contextMode={2} tagLength={3}",
                    associatedAuthenticatedDataSize, inputDataSize, contextMode, tagLength);

            if(contextMode != ContextMode.WholeMessage)
            {
                parent.Log(LogLevel.Error, "AES_GCM_DECRYPT: only support WholeMessage context mode");
                return ResponseCode.Abort;
            }

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_DECRYPT: INPUT0: authPtr=0x{0:X} authLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_DECRYPT: INPUT1: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = ReadKey(keyMode, keyIndex, keyPtr, keyLength);
            parent.Log(LogLevel.Debug, "Key=[{0}]", BitConverter.ToString(key));
            var keyParameter = new KeyParameter(key);

            // Third input DMA descriptor contains the IV (for Whole or Start) or context data (for Middle or End)
            uint ivPtr;
            uint ivLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out ivPtr, out ivLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_DECRYPT: INPUT2: ivPtr=0x{0:X} ivLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    ivPtr, ivLength, transferOptions, nextDescriptorPtr);
            byte[] iv = new byte[ivLength];
            FetchFromRam(ivPtr, iv, 0, ivLength);
            parent.Log(LogLevel.Debug, "IV/Context=[{0}]", BitConverter.ToString(iv));

            // The iv_size is 12 for Whole and Start of message (= initial IV) and 32 for Middle/End of message (= context input).
            // For now we only support Whole, so iv_size should always be 12.
            if(ivLength != 12)
            {
                parent.Log(LogLevel.Error, "AES_GCM_DECRYPT: invalid IV length");
                return ResponseCode.Abort;
            }

            // Fourth input DMA descriptor contains the associated authenticated data
            uint aadPtr;
            uint aadLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out aadPtr, out aadLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_DECRYPT: INPUT3: aadPtr=0x{0:X} aadLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    aadPtr, aadLength, transferOptions, nextDescriptorPtr);
            byte[] aad = new byte[aadLength];
            FetchFromRam(aadPtr, aad, 0, aadLength);
            parent.Log(LogLevel.Debug, "Aad=[{0}]", BitConverter.ToString(aad));

            // Fifth input DMA descriptor contains the ciphertext input data
            uint inputDataPtr;
            uint inputDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_DECRYPT: INPUT4: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);

            // Sixth input DMA descriptor contains the MAC (End/Whole only)
            uint macPtr = 0;
            uint macLength = 0;
            if(nextDescriptorPtr != NullDescriptor)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out macPtr, out macLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "AES_GCM_DECRYPT: INPUT5: macPtr=0x{0:X} macLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        macPtr, macLength, transferOptions, nextDescriptorPtr);
            }

            // Seventh input DMA descriptor contains the len(A)||len(C) (End only)
            uint lenAlenCPtr = 0;
            uint lenAlenCLength = 0;
            if(nextDescriptorPtr != NullDescriptor)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out lenAlenCPtr, out lenAlenCLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "AES_GCM_DECRYPT: INPUT6: lenAlenCPtr=0x{0:X} lenAlenCLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        lenAlenCPtr, lenAlenCLength, transferOptions, nextDescriptorPtr);
            }
            byte[] lenAlenC = new byte[lenAlenCLength];
            if(lenAlenCLength > 0)
            {
                FetchFromRam(lenAlenCPtr, lenAlenC, 0, lenAlenCLength);
                parent.Log(LogLevel.Debug, "LenAlenC=[{0}]", BitConverter.ToString(lenAlenC));
            }

            byte[] inputDataAndTag = new byte[inputDataLength + tagLength];
            FetchFromRam(inputDataPtr, inputDataAndTag, 0, inputDataLength);
            FetchFromRam(macPtr, inputDataAndTag, inputDataLength, (uint)tagLength);
            parent.Log(LogLevel.Debug, "InputDataAndTag=[{0}]", BitConverter.ToString(inputDataAndTag));

            // First output DMA descriptor contains the decrypted output data
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "AES_GCM_DECRYPT: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                    outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);
            byte[] outputData = new byte[outputDataLength];

            // Second output DMA descriptor contains the context
            uint outputContextPtr = 0;
            uint outputContextLength = 0;
            if(nextDescriptorPtr != NullDescriptor)
            {
                UnpackDmaDescriptor(nextDescriptorPtr, out outputContextPtr, out outputContextLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "AES_GCM_DECRYPT: OUTPUT1: outputContextPtr=0x{0:X} outputContextLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        outputContextPtr, outputContextLength, transferOptions, nextDescriptorPtr);
            }

            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(keyParameter, (int)tagLength*8, iv, aad);
            cipher.Init(false, parameters);

            bool tagMatch = true;
            int len = cipher.ProcessBytes(inputDataAndTag, 0, (int)(inputDataLength+tagLength), outputData, 0);

            try
            {
                len += cipher.DoFinal(outputData, len);
            }
            catch(Exception e)
            {
                if(e is InvalidCipherTextException)
                {
                    tagMatch = false;
                }
                else
                {
                    throw e;
                }
            }

            WriteToRam(outputData, 0, outputDataPtr, outputDataLength);
            parent.Log(LogLevel.Debug, "DecryptedData=[{0}] tagMatch={1}", BitConverter.ToString(outputData), tagMatch);

            // TODO: write the output context here when we implement Start/Middle mode.

            return tagMatch ? ResponseCode.Ok : ResponseCode.CryptoError;
        }

#pragma warning disable IDE0060
        private ResponseCode HandleRandomCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
#pragma warning restore IDE0060
        {
            if(commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "RANDOM: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            uint randomLength = commandParams[0];

            parent.Log(LogLevel.Debug, "RANDOM: random length = {0}", randomLength);

            // First output DMA descriptor contains the random data
            uint outputDataPtr;
            uint outputDataLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "RANDOM: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            for(uint i = 0; i < randomLength; i++)
            {
                byte b = (byte)random.Next();
                machine.SystemBus.WriteByte(outputDataPtr + i, b);
            }
            return ResponseCode.Ok;
        }

#pragma warning disable IDE0060
        private ResponseCode HandleReadDeviceDataCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
#pragma warning restore IDE0060
        {
            if(commandParamsCount < 1 || commandParamsCount > 2)
            {
                parent.Log(LogLevel.Error, "ReadDeviceData: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            uint readLocation = commandOptions >> 12;
            var readLocationType = (ReadDeviceDataLocationType)(readLocation >> 4);

            switch(readLocationType)
            {
            case ReadDeviceDataLocationType.CC:
                //update location offset to CC
                break;
            case ReadDeviceDataLocationType.DI:
                //update location offset to DI
                break;
            case ReadDeviceDataLocationType.WaferProbe:
                //update location offset to WaferProbe
                break;
            default:
                return ResponseCode.InvalidParameter;
            }

            var readSize = (DeviceDataReadSize)(commandOptions & 0xFF);
            uint output = 1;

            switch(readSize)
            {
            case DeviceDataReadSize.WholeElement:
                //Get whole element data, size will depend on the readLocationType
                break;
            case DeviceDataReadSize.ChunkOfElement:
                if(commandParamsCount != 2)
                {
                    parent.Log(LogLevel.Error, "ReadDeviceData: invalid param count for ChunkOfElement read size");
                    return ResponseCode.Abort;
                }
                //Get chunk of element, need to check if we remain in bounds
                break;
            case DeviceDataReadSize.OneWord:
                //Get one word from specified offset
                break;
            case DeviceDataReadSize.GetSize:
                //Get size of element
                break;
            case DeviceDataReadSize.GetValue:
                if(readLocation != 0)
                {
                    return ResponseCode.InvalidParameter;
                }
                //Get value corresponding to address (only for CC section)
                break;
            default:
                return ResponseCode.InvalidParameter;
            }

            // First output DMA descriptor contains the location to which we send the data
            uint outputDataPtr;
            uint outputDataLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "ReadDeviceData: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            // For now, return 1 regardless of the location/option selected
            machine.SystemBus.WriteDoubleWord(outputDataPtr, output);
            return ResponseCode.Ok;
        }

        private ResponseCode HandleUserDataStorageCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            // The 16-bit options word is split into two bytes: the high byte ("Option 0") is reserved
            // and must be 0 (None), and the low byte ("Option 1") selects the action.
            uint option0 = (commandOptions >> 8) & 0xFF;
            uint option1 = commandOptions & 0xFF;

            parent.Log(LogLevel.Debug, "USER_DATA_STORAGE: option0={0} option1={1}", option0, option1);

            if(option0 != 0)
            {
                parent.Log(LogLevel.Error, "USER_DATA_STORAGE: unsupported option0 {0}", option0);
                return ResponseCode.InvalidParameter;
            }

            uint dataPtr;
            uint dataLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;

            switch((UserDataAction)option1)
            {
            case UserDataAction.Get:
                if(commandParamsCount != 0)
                {
                    parent.Log(LogLevel.Error, "USER_DATA_STORAGE(Get): invalid param count {0}", commandParamsCount);
                    return ResponseCode.InvalidParameter;
                }

                // Reading an unwritten token mirrors the SE returning SL_STATUS_NOT_INITIALIZED.
                if(!userDataInitialized)
                {
                    parent.Log(LogLevel.Debug, "USER_DATA_STORAGE(Get): token not initialized");
                    return ResponseCode.NotInitialized;
                }

                UnpackDmaDescriptor(outputDma, out dataPtr, out dataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "USER_DATA_STORAGE(Get): OUTPUT0: dataPtr=0x{0:X} dataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                         dataPtr, dataLength, transferOptions, nextDescriptorPtr);

                WriteToRam(userDataToken, 0, dataPtr, UserDataTokenSize);
                return ResponseCode.Ok;

            case UserDataAction.Set:
            case UserDataAction.ForceSet:
                bool force = (UserDataAction)option1 == UserDataAction.ForceSet;

                // The normal Set passes num_bytes as a single parameter (see sl_se_write_user_data).
                // Force set may be issued without a parameter, so tolerate either form for it.
                if(!force)
                {
                    if(commandParamsCount != 1 || commandParams[0] != UserDataTokenSize)
                    {
                        parent.Log(LogLevel.Error, "USER_DATA_STORAGE(Set): invalid param count {0} or size {1}",
                                 commandParamsCount, commandParamsCount >= 1 ? commandParams[0] : 0);
                        return ResponseCode.InvalidParameter;
                    }

                    if(userDataRemainingWrites == 0)
                    {
                        parent.Log(LogLevel.Debug, "USER_DATA_STORAGE(Set): write limit reached");
                        return ResponseCode.InvalidParameter;
                    }
                }
                else if(commandParamsCount > 1 || (commandParamsCount == 1 && commandParams[0] != UserDataTokenSize))
                {
                    parent.Log(LogLevel.Error, "USER_DATA_STORAGE(ForceSet): invalid param count {0} or size {1}",
                             commandParamsCount, commandParamsCount >= 1 ? commandParams[0] : 0);
                    return ResponseCode.InvalidParameter;
                }

                UnpackDmaDescriptor(inputDma, out dataPtr, out dataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "USER_DATA_STORAGE(Set): force={0} INPUT0: dataPtr=0x{1:X} dataLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                         force, dataPtr, dataLength, transferOptions, nextDescriptorPtr);

                FetchFromRam(dataPtr, userDataToken, 0, UserDataTokenSize);
                userDataInitialized = true;
                if(!force && userDataRemainingWrites > 0)
                {
                    userDataRemainingWrites--;
                }
                return ResponseCode.Ok;

            case UserDataAction.RemainingWrites:
                if(commandParamsCount != 0)
                {
                    parent.Log(LogLevel.Error, "USER_DATA_STORAGE(RemainingWrites): invalid param count {0}", commandParamsCount);
                    return ResponseCode.InvalidParameter;
                }

                UnpackDmaDescriptor(outputDma, out dataPtr, out dataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "USER_DATA_STORAGE(RemainingWrites): OUTPUT0: dataPtr=0x{0:X} dataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                         dataPtr, dataLength, transferOptions, nextDescriptorPtr);

                machine.SystemBus.WriteDoubleWord(dataPtr, userDataRemainingWrites);
                return ResponseCode.Ok;

            default:
                parent.Log(LogLevel.Error, "USER_DATA_STORAGE: unsupported option1 {0}", option1);
                return ResponseCode.InvalidParameter;
            }
        }

#pragma warning disable IDE0060
        private ResponseCode HandleFlashEraseDataRegionCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
#pragma warning restore IDE0060
        {
            if(commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "FlashEraseDataRegion: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            // check that flash is properly initialized
            if(series3 && (flashSize == 0 || flashPageSize == 0))
            {
                parent.Log(LogLevel.Error, "flashSize = {0} and flashPageSize = {1}. These must be initialized with non-zero values.", flashSize, flashPageSize);
                return ResponseCode.Abort;
            }

            // For erase operations, the address may be any within the page to be erased.
            uint startAddress = commandParams[0] & ~(flashPageSize - 1);
            uint sectorsCount = commandParams[1];

            parent.Log(LogLevel.Debug, "FLASH_ERASE_DATA_REGION: startAddress=0x{0:X} sectorNumber={1}", startAddress, sectorsCount);

            machine.ClockSource.ExecuteInLock(delegate
            {
                for(uint i = 0; i < sectorsCount; i++)
                {
                    for(uint addr = startAddress + i * flashPageSize; addr < startAddress + (i + 1) * flashPageSize; addr += 4)
                    {
                        machine.SystemBus.WriteDoubleWord(addr, 0xFFFFFFFF);
                    }
                }
            });
            return ResponseCode.Ok;
        }

#pragma warning disable IDE0060
        private ResponseCode HandleFlashWriteDataRegionCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
#pragma warning restore IDE0060
        {
            if(commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "FlashWriteDataRegion: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }
            // check that flash is properly initialized
            if(series3 && (flashSize == 0 || flashPageSize == 0))
            {
                parent.Log(LogLevel.Error, "flashSize = {0} and flashPageSize = {1}. These must be initialized with non-zero values.", flashSize, flashPageSize);
                return ResponseCode.Abort;
            }

            uint startAddress = commandParams[0];
            uint writeLength = commandParams[1];

            parent.Log(LogLevel.Debug, "FLASH_WRITE_DATA: startAddress=0x{0:X} length={1}", startAddress, writeLength);

            // First input DMA descriptor contains the data to be written
            uint inputDataPtr;
            uint inputDataLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "FLASH_WRITE_DATA: INPUT0: inputDataPtr=0x{0:X} inputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);

            machine.ClockSource.ExecuteInLock(delegate
            {
                for(uint i = 0; i < writeLength; i += 4)
                {
                    uint word = machine.SystemBus.ReadDoubleWord(inputDataPtr + i);
                    machine.SystemBus.WriteDoubleWord(startAddress + i, word);
                }
            });
            return ResponseCode.Ok;
        }

#pragma warning disable IDE0060
        private ResponseCode HandleFlashGetDataRegionLocationCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
#pragma warning restore IDE0060
        {
            if(commandParamsCount != 0)
            {
                parent.Log(LogLevel.Error, "FlashGetDataRegionLocation: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            // First output DMA descriptor contains the data region address
            uint outputDataRegionAddressPtr;
            uint outputDataRegionAddressLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(outputDma, out outputDataRegionAddressPtr, out outputDataRegionAddressLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "FLASH_GET_DATA_REGION_LOC: OUTPUT0: outputDataRegionAddressPtr=0x{0:X} outputDataRegionAddressLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataRegionAddressPtr, outputDataRegionAddressLength, transferOptions, nextDescriptorPtr);

            // Second output DMA descriptor contains the data region length
            uint outputDataRegionLengthPtr;
            uint outputDataRegionLengthLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out outputDataRegionLengthPtr, out outputDataRegionLengthLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "FLASH_GET_DATA_REGION_LOC: OUTPUT1: outputDataRegionLengthPtr=0x{0:X} outputDataRegionLengthLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputDataRegionLengthPtr, outputDataRegionLengthLength, transferOptions, nextDescriptorPtr);

            // check that flash is properly initialized
            if(series3 && (flashSize == 0 || flashPageSize == 0))
            {
                parent.Log(LogLevel.Error, "flashSize = {0} and flashPageSize = {1}. These must be initialized with non-zero values.", flashSize, flashPageSize);
                return ResponseCode.Abort;
            }
            uint flashDataSpaceLength = GetInitialDataSpaceLength;
            machine.SystemBus.WriteDoubleWord(outputDataRegionAddressPtr, QspiFlashHostBase + (flashSize - flashDataSpaceLength));
            machine.SystemBus.WriteDoubleWord(outputDataRegionLengthPtr, flashDataSpaceLength);
            return ResponseCode.Ok;
        }

#pragma warning disable IDE0060
        private ResponseCode HandleFlashGetCodeRegionConfigCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
#pragma warning restore IDE0060
        {
            if(commandParamsCount != 0)
            {
                parent.Log(LogLevel.Error, "HandleFlashGetCodeRegionConfigCommand: invalid param count {0}", commandParamsCount);
                return ResponseCode.InvalidParameter;
            }

            // Output DMA descriptor contains the region config array
            uint outputCodeRegionConfigAddressPtr;
            uint outputCodeRegionConfigAddressLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(outputDma, out outputCodeRegionConfigAddressPtr, out outputCodeRegionConfigAddressLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "FLASH_GET_CODE_REGION_CONFIG: OUTPUT0: outputCodeRegionConfigAddressPtr=0x{0:X} outputCodeRegionConfigAddressLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     outputCodeRegionConfigAddressPtr, outputCodeRegionConfigAddressLength, transferOptions, nextDescriptorPtr);

            // check that flash is properly initialized
            if(series3 && (flashSize == 0 || flashPageSize == 0))
            {
                parent.Log(LogLevel.Error, "flashSize = {0} and flashPageSize = {1}. These must be initialized with non-zero values.", flashSize, flashPageSize);
                return ResponseCode.Abort;
            }

            uint regionSize = (outputCodeRegionConfigAddressPtr & 0xFFF) * 32 * 1024; // Bits 0:11
            uint protectionMode = (outputCodeRegionConfigAddressPtr >> 12) & 0x3; // Bits 12:13
            bool bankSwappingEnabled = ((outputCodeRegionConfigAddressPtr >> 14) & 0x1) == 1; // Bit 14
            bool regionClosed = ((outputCodeRegionConfigAddressPtr >> 15) & 0x1) == 1; // Bit 15

            uint data = (regionSize / (32 * 1024)) & 0xFFF; // Bits 0:11
            data |= (protectionMode & 0x3) << 12; // Bits 12:13
            data |= (bankSwappingEnabled ? 1u : 0u) << 14; // Bit 14
            data |= (regionClosed ? 1u : 0u) << 15; // Bit 15

            machine.SystemBus.WriteDoubleWord(outputCodeRegionConfigAddressPtr, data);

            return ResponseCode.Ok;
        }

#pragma warning disable IDE0060
        private ResponseCode HandleConfigureQspiRefClockCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
#pragma warning restore IDE0060
        {
            switch(commandOptions)
            {
            case 0x01:
            case 0x0100:
                // Set FSRCO as the QSPI controller clock source
                if(commandParamsCount != 0)
                {
                    parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid param count {0} for option 0x01", commandParamsCount);
                    return ResponseCode.InvalidParameter;
                }
                parent.Log(LogLevel.Debug, "QSPI Clock Source set to FSRCO");
                break;

            case 0x02:
            case 0x0200:
                // Set FLPLL as the QSPI controller clock source
                if(commandParamsCount != 4)
                {
                    parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid param count {0} for option 0x02", commandParamsCount);
                    return ResponseCode.InvalidParameter;
                }

                uint flpllRefClock = commandParams[0];
                uint intDiv = (commandParams[1] >> 16) & 0xFFFF;
                uint fracDiv = commandParams[1] & 0xFFFF;
                uint flpllFreqRange = commandParams[2] & 0xF;
                uint qspiClockPrescaler = (commandParams[2] >> 4) & 0xF;

                if(flpllRefClock != 0x0 && flpllRefClock != 0x2)
                {
                    parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid FLPLL reference clock {0}", flpllRefClock);
                    return ResponseCode.InvalidParameter;
                }
                if(fracDiv >= 2048 || intDiv <= 5 || intDiv >= 12)
                {
                    parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid INT_DIV {0} or FRAC_DIV {1}", intDiv, fracDiv);
                    return ResponseCode.InvalidParameter;
                }

                parent.Log(LogLevel.Debug, "QSPI Clock Source set to FLPLL");
                parent.Log(LogLevel.Debug, "FLPLL Ref Clock: {0}, INT_DIV: {1}, FRAC_DIV: {2}, Freq Range: {3}, Prescaler: {4}",
                    flpllRefClock, intDiv, fracDiv, flpllFreqRange, qspiClockPrescaler);
                break;

            default:
                parent.Log(LogLevel.Error, "ConfigureQspiRefClock: invalid command option {0}", commandOptions);
                return ResponseCode.InvalidParameter;
            }

            return ResponseCode.Ok;
        }

        // ---------------------------------------------------------------------------------------------
        // AES-CCM encrypt/decrypt command handler and related helpers.
        // ---------------------------------------------------------------------------------------------

        private ResponseCode HandleAesCcmEncryptOrDecryptCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions, bool encrypt)
        {
            string label = encrypt ? "AES_CCM_ENCRYPT" : "AES_CCM_DECRYPT";
            var contextMode = (ContextMode)(commandOptions & 0xFF);
            switch(contextMode)
            {
            case ContextMode.WholeMessage:
                return AesCcmOneShot(encrypt, inputDma, outputDma, commandParams, commandParamsCount);
            case ContextMode.StartOfMessage:
                return AesCcmMultipartStart(encrypt, inputDma, outputDma, commandParams, commandParamsCount);
            case ContextMode.MiddleOfMessage:
                return AesCcmMultipartAdd(encrypt, inputDma, outputDma, commandParams, commandParamsCount);
            case ContextMode.EndOfMessage:
                return AesCcmMultipartFinish(encrypt, inputDma, outputDma, commandParams, commandParamsCount);
            default:
                parent.Log(LogLevel.Error, "{0}: invalid context mode {1}", label, contextMode);
                return ResponseCode.Abort;
            }
        }

        private ResponseCode AesCcmOneShot(bool encrypt, uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            string label = encrypt ? "AES_CCM_ENCRYPT" : "AES_CCM_DECRYPT";
            if(commandParamsCount != 4)
            {
                parent.Log(LogLevel.Error, "{0}: invalid parameter count", label);
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "{0}: Key Metadata: keyIndex={1} keyType={2} keyMode={3} keyRestriction={4}",
                     label, keyIndex, keyType, keyMode, keyRestriction);
            if(keyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "{0}: unsupported key type {1} (expected Raw)", label, keyType);
                return ResponseCode.InvalidParameter;
            }
            uint tagSize = commandParams[1] & 0xFFFF;
            uint nonceSize = (commandParams[1] >> 16) & 0xFFFF;
            uint associatedAuthenticatedDataSize = commandParams[2];
            uint inputDataSize = commandParams[3];
            parent.Log(LogLevel.Debug, "{0}: Other Command Params: tagSize={1} nonceSize={2} aadSize={3} inputDataSize={4}",
                     label, tagSize, nonceSize, associatedAuthenticatedDataSize, inputDataSize);

            if(!IsTagSizeValid(tagSize) || !IsNonceSizeValid(nonceSize))
            {
                return ResponseCode.InvalidParameter;
            }

            uint nextDescriptorPtr;
            var keyParameter = CcmReadAuthAndKeyDescriptors(label, "", keyMode, keyIndex, inputDma, out nextDescriptorPtr);

            byte[] nonce;
            byte[] aad;
            CcmUnpackNonceAndAad(label, "", nextDescriptorPtr, out nonce, out aad, out nextDescriptorPtr);

            uint inputDataLength = 0;
            byte[] inputData = new byte[0];
            uint outputDataPtr = 0;
            uint outputDataLength = 0;
            uint tagLength = 0;
            uint outputTagPtr = 0;
            uint outputTagLength = 0;
            byte[] inputTag = null;
            DmaTransferOptions transferOptions;

            if(encrypt)
            {
                // Fifth input DMA descriptor contains the plaintext input data.
                bool hasInputDataDescriptor = (nextDescriptorPtr != NullDescriptor);
                if(hasInputDataDescriptor)
                {
                    uint inputDataPtr;
                    UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
                    parent.Log(LogLevel.Debug, "{0}: INPUT4: inputDataPtr=0x{1:X} inputDataLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                             label, inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);
                    inputData = new byte[inputDataLength];
                    FetchFromRam(inputDataPtr, inputData, 0, inputDataLength);
                    parent.Log(LogLevel.Debug, "InputData=[{0}]", BitConverter.ToString(inputData));
                }

                // First output DMA descriptor contains the encrypted output data, unless there is no
                // input data descriptor above, in which case this is directly the output tag.
                UnpackDmaDescriptor(outputDma, out outputTagPtr, out outputTagLength, out transferOptions, out nextDescriptorPtr, machine);
                if(hasInputDataDescriptor)
                {
                    outputDataPtr = outputTagPtr;
                    outputDataLength = outputTagLength;
                    parent.Log(LogLevel.Debug, "{0}: OUTPUT0: outputDataPtr=0x{1:X} outputDataLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                             label, outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

                    // Second output DMA descriptor contains the output tag
                    UnpackDmaDescriptor(nextDescriptorPtr, out outputTagPtr, out outputTagLength, out transferOptions, out nextDescriptorPtr, machine);
                }
                parent.Log(LogLevel.Debug, "{0}: OUTPUT{1}: outputTagPtr=0x{2:X} outputTagLength={3} options={4} nextDescriptorPtr=0x{5:X}",
                         label, hasInputDataDescriptor ? 1 : 0, outputTagPtr, outputTagLength, transferOptions, nextDescriptorPtr);

                tagLength = outputTagLength;
            }
            else
            {
                // Fifth input DMA descriptor contains the ciphertext input data.
                uint inputDataPtr;
                UnpackDmaDescriptor(nextDescriptorPtr, out inputDataPtr, out inputDataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "{0}: INPUT4: inputDataPtr=0x{1:X} inputDataLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                         label, inputDataPtr, inputDataLength, transferOptions, nextDescriptorPtr);
                inputData = new byte[inputDataLength];
                FetchFromRam(inputDataPtr, inputData, 0, inputDataLength);
                parent.Log(LogLevel.Debug, "InputData=[{0}]", BitConverter.ToString(inputData));

                // Sixth input DMA descriptor contains the tag to be verified
                uint tagPtr;
                UnpackDmaDescriptor(nextDescriptorPtr, out tagPtr, out tagLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "{0}: INPUT5: tagPtr=0x{1:X} tagLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                         label, tagPtr, tagLength, transferOptions, nextDescriptorPtr);
                inputTag = new byte[tagLength];
                FetchFromRam(tagPtr, inputTag, 0, tagLength);
                parent.Log(LogLevel.Debug, "Tag=[{0}]", BitConverter.ToString(inputTag));

                // First output DMA descriptor contains the decrypted output data
                UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "{0}: OUTPUT0: outputDataPtr=0x{1:X} outputDataLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                         label, outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);
            }

            byte[] macState = new byte[CcmBlockSize];
            byte[] counterBlock = new byte[CcmBlockSize];
            CcmStart(keyParameter, nonce, tagLength, (uint)aad.Length, aad, inputDataLength, macState, counterBlock);

            byte[] outputData = new byte[inputDataLength];
            uint fullBlocksLength = inputDataLength - (inputDataLength % CcmBlockSize);
            if(fullBlocksLength > 0)
            {
                CcmProcessBlocks(encrypt, keyParameter, macState, counterBlock, inputData, fullBlocksLength, outputData);
            }
            byte[] finalInput = new byte[inputDataLength - fullBlocksLength];
            Array.Copy(inputData, (int)fullBlocksLength, finalInput, 0, finalInput.Length);
            byte[] finalOutput = new byte[finalInput.Length];
            byte[] computedTag = new byte[tagLength];
            CcmFinish(encrypt, keyParameter, macState, counterBlock, finalInput, finalOutput, tagLength, computedTag);
            Array.Copy(finalOutput, 0, outputData, (int)fullBlocksLength, finalOutput.Length);

            WriteToRam(outputData, 0, outputDataPtr, outputDataLength);

            if(encrypt)
            {
                WriteToRam(computedTag, 0, outputTagPtr, outputTagLength);
                parent.Log(LogLevel.Debug, "EncryptedData=[{0}] Tag=[{1}]", BitConverter.ToString(outputData), BitConverter.ToString(computedTag));
                return ResponseCode.Ok;
            }

            bool tagMatch = CcmTagsEqual(computedTag, inputTag, tagLength);
            parent.Log(LogLevel.Debug, "Decrypted data=[{0}] tagMatch={1}", BitConverter.ToString(outputData), tagMatch);
            return tagMatch ? ResponseCode.Ok : ResponseCode.CryptoError;
        }

        private ResponseCode AesCcmMultipartStart(bool encrypt, uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            string label = encrypt ? "AES_CCM_ENCRYPT" : "AES_CCM_DECRYPT";
            if(commandParamsCount != 4)
            {
                parent.Log(LogLevel.Error, "{0}: CCM Start: invalid parameter count", label);
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "{0}: CCM Start: Key Metadata: keyIndex={1} keyType={2} keyMode={3} keyRestriction={4}",
                     label, keyIndex, keyType, keyMode, keyRestriction);
            if(keyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "{0}: CCM Start: unsupported key type {1} (expected Raw)", label, keyType);
                return ResponseCode.InvalidParameter;
            }
            uint tagSize = commandParams[1] & 0xFFFF;
            uint nonceSize = (commandParams[1] >> 16) & 0xFFFF;
            uint aadSize = commandParams[2];
            parent.Log(LogLevel.Debug, "{0}: CCM Start: tagSize={1} nonceSize={2} aadSize={3}", label, tagSize, nonceSize, aadSize);

            if(!IsTagSizeValid(tagSize) || !IsNonceSizeValid(nonceSize))
            {
                return ResponseCode.InvalidParameter;
            }

            uint nextDescriptorPtr;
            var keyParameter = CcmReadAuthAndKeyDescriptors(label, "CCM Start: ", keyMode, keyIndex, inputDma, out nextDescriptorPtr);

            // Third input DMA descriptor contains the total message length (a raw 4-byte uint32_t)
            uint messageLengthPtr;
            uint messageLengthDescLength;
            DmaTransferOptions transferOptions;
            UnpackDmaDescriptor(nextDescriptorPtr, out messageLengthPtr, out messageLengthDescLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: CCM Start: INPUT2: messageLengthPtr=0x{1:X} messageLengthDescLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                     label, messageLengthPtr, messageLengthDescLength, transferOptions, nextDescriptorPtr);
            if(messageLengthDescLength != 4)
            {
                parent.Log(LogLevel.Error, "{0}: CCM Start: invalid message length descriptor size {1}", label, messageLengthDescLength);
                return ResponseCode.InvalidParameter;
            }
            uint totalMessageLength = (uint)machine.SystemBus.ReadDoubleWord(messageLengthPtr);
            parent.Log(LogLevel.Debug, "{0}: CCM Start: totalMessageLength={1}", label, totalMessageLength);

            byte[] nonce;
            byte[] aad;
            CcmUnpackNonceAndAad(label, "CCM Start: ", nextDescriptorPtr, out nonce, out aad, out nextDescriptorPtr);

            // First output DMA descriptor receives the 32-byte streaming context
            uint outputContextPtr;
            var contextResponseCode = CcmUnpackContextDescriptor(label, "CCM Start: ", outputDma, out outputContextPtr, out nextDescriptorPtr);
            if(contextResponseCode != ResponseCode.Ok)
            {
                return contextResponseCode;
            }

            byte[] macState = new byte[CcmBlockSize];
            byte[] counterBlock = new byte[CcmBlockSize];
            CcmStart(keyParameter, nonce, tagSize, aadSize, aad, totalMessageLength, macState, counterBlock);

            WriteToRam(macState, 0, outputContextPtr, CcmBlockSize);
            WriteToRam(counterBlock, 0, outputContextPtr + CcmBlockSize, CcmBlockSize);
            return ResponseCode.Ok;
        }

        private ResponseCode AesCcmMultipartAdd(bool encrypt, uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            string label = encrypt ? "AES_CCM_ENCRYPT" : "AES_CCM_DECRYPT";
            if(commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "{0}: CCM Add: invalid parameter count", label);
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "{0}: CCM Add: Key Metadata: keyIndex={1} keyType={2} keyMode={3} keyRestriction={4}",
                     label, keyIndex, keyType, keyMode, keyRestriction);
            if(keyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "{0}: CCM Add: unsupported key type {1} (expected Raw)", label, keyType);
                return ResponseCode.InvalidParameter;
            }
            uint length = commandParams[1];
            parent.Log(LogLevel.Debug, "{0}: CCM Add: length={1}", label, length);
            if(length == 0 || (length % CcmBlockSize) != 0)
            {
                parent.Log(LogLevel.Error, "{0}: CCM Add: invalid length {1} (must be a nonzero multiple of {2})", label, length, CcmBlockSize);
                return ResponseCode.InvalidParameter;
            }

            uint nextDescriptorPtr;
            var keyParameter = CcmReadAuthAndKeyDescriptors(label, "CCM Add: ", keyMode, keyIndex, inputDma, out nextDescriptorPtr);

            // Third input DMA descriptor contains the 32-byte streaming context
            uint contextPtr;
            var contextResponseCode = CcmUnpackContextDescriptor(label, "CCM Add: ", nextDescriptorPtr, out contextPtr, out nextDescriptorPtr);
            if(contextResponseCode != ResponseCode.Ok)
            {
                return contextResponseCode;
            }
            byte[] macState = new byte[CcmBlockSize];
            byte[] counterBlock = new byte[CcmBlockSize];
            FetchFromRam(contextPtr, macState, 0, CcmBlockSize);
            FetchFromRam(contextPtr + CcmBlockSize, counterBlock, 0, CcmBlockSize);

            // Fourth input DMA descriptor contains the data to process (length bytes, a multiple of 16)
            uint dataPtr;
            uint dataLength;
            DmaTransferOptions transferOptions;
            UnpackDmaDescriptor(nextDescriptorPtr, out dataPtr, out dataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: CCM Add: INPUT3: dataPtr=0x{1:X} dataLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                     label, dataPtr, dataLength, transferOptions, nextDescriptorPtr);
            if(dataLength != length)
            {
                parent.Log(LogLevel.Error, "{0}: CCM Add: data descriptor length {1} != param length {2}", label, dataLength, length);
                return ResponseCode.InvalidParameter;
            }
            byte[] inputData = new byte[length];
            FetchFromRam(dataPtr, inputData, 0, length);
            parent.Log(LogLevel.Debug, "InputData=[{0}]", BitConverter.ToString(inputData));

            // First output DMA descriptor receives the processed data
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: CCM Add: OUTPUT0: outputDataPtr=0x{1:X} outputDataLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                     label, outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            // Second output DMA descriptor receives the updated 32-byte streaming context
            uint outputContextPtr;
            var outputContextResponseCode = CcmUnpackContextDescriptor(label, "CCM Add: ", nextDescriptorPtr, out outputContextPtr, out nextDescriptorPtr);
            if(outputContextResponseCode != ResponseCode.Ok)
            {
                return outputContextResponseCode;
            }

            byte[] outputData = new byte[length];
            CcmProcessBlocks(encrypt, keyParameter, macState, counterBlock, inputData, length, outputData);
            parent.Log(LogLevel.Debug, "OutputData=[{0}]", BitConverter.ToString(outputData));

            WriteToRam(outputData, 0, outputDataPtr, outputDataLength);
            WriteToRam(macState, 0, outputContextPtr, CcmBlockSize);
            WriteToRam(counterBlock, 0, outputContextPtr + CcmBlockSize, CcmBlockSize);
            return ResponseCode.Ok;
        }

        private ResponseCode AesCcmMultipartFinish(bool encrypt, uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            string label = encrypt ? "AES_CCM_ENCRYPT" : "AES_CCM_DECRYPT";
            if(commandParamsCount != 3)
            {
                parent.Log(LogLevel.Error, "{0}: CCM Finish: invalid parameter count", label);
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "{0}: CCM Finish: Key Metadata: keyIndex={1} keyType={2} keyMode={3} keyRestriction={4}",
                     label, keyIndex, keyType, keyMode, keyRestriction);
            if(keyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "{0}: CCM Finish: unsupported key type {1} (expected Raw)", label, keyType);
                return ResponseCode.InvalidParameter;
            }
            uint tagLength = commandParams[1] & 0xFFFF;
            uint finalDataLength = commandParams[2];
            parent.Log(LogLevel.Debug, "{0}: CCM Finish: tagLength={1} finalDataLength={2}", label, tagLength, finalDataLength);
            if(!IsTagSizeValid(tagLength))
            {
                return ResponseCode.InvalidParameter;
            }
            if(finalDataLength > CcmBlockSize)
            {
                parent.Log(LogLevel.Error, "{0}: CCM Finish: invalid final data length {1}", label, finalDataLength);
                return ResponseCode.InvalidParameter;
            }

            // First input DMA descriptor is the authorization data
            uint authPtr;
            uint authLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out authPtr, out authLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: CCM Finish: INPUT0: authPtr=0x{1:X} authLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                     label, authPtr, authLength, transferOptions, nextDescriptorPtr);

            // Second input DMA descriptor contains the key
            uint keyPtr;
            uint keyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: CCM Finish: INPUT1: keyPtr=0x{1:X} keyLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                     label, keyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = ReadKey(keyMode, keyIndex, keyPtr, keyLength);
            parent.Log(LogLevel.Debug, "Key=[{0}]", BitConverter.ToString(key));
            var keyParameter = new KeyParameter(key);

            // Third input DMA descriptor contains the 32-byte streaming context
            uint contextPtr;
            uint contextLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out contextPtr, out contextLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: CCM Finish: INPUT2: contextPtr=0x{1:X} contextLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                     label, contextPtr, contextLength, transferOptions, nextDescriptorPtr);
            if(contextLength != CcmContextSize)
            {
                parent.Log(LogLevel.Error, "{0}: CCM Finish: invalid context length {1}", label, contextLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] macState = new byte[CcmBlockSize];
            byte[] counterBlock = new byte[CcmBlockSize];
            FetchFromRam(contextPtr, macState, 0, CcmBlockSize);
            FetchFromRam(contextPtr + CcmBlockSize, counterBlock, 0, CcmBlockSize);

            // Fourth input DMA descriptor contains the final (0..15 byte) residual data
            uint finalDataPtr;
            uint finalDataDescLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out finalDataPtr, out finalDataDescLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: CCM Finish: INPUT3: finalDataPtr=0x{1:X} finalDataDescLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                     label, finalDataPtr, finalDataDescLength, transferOptions, nextDescriptorPtr);
            if(finalDataDescLength != finalDataLength)
            {
                parent.Log(LogLevel.Error, "{0}: CCM Finish: final data descriptor length {1} != param length {2}", label, finalDataDescLength, finalDataLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] finalData = new byte[finalDataLength];
            FetchFromRam(finalDataPtr, finalData, 0, finalDataLength);
            parent.Log(LogLevel.Debug, "FinalData=[{0}]", BitConverter.ToString(finalData));

            byte[] tag = new byte[tagLength];
            if(!encrypt)
            {
                // Fifth input DMA descriptor contains the tag to be verified (decrypt only)
                uint tagPtr;
                uint tagDescLength;
                UnpackDmaDescriptor(nextDescriptorPtr, out tagPtr, out tagDescLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "{0}: CCM Finish: INPUT4: tagPtr=0x{1:X} tagDescLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                         label, tagPtr, tagDescLength, transferOptions, nextDescriptorPtr);
                FetchFromRam(tagPtr, tag, 0, tagDescLength);
                parent.Log(LogLevel.Debug, "Tag=[{0}]", BitConverter.ToString(tag));
            }

            // First output DMA descriptor receives the final (0..15 byte) processed data
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: CCM Finish: OUTPUT0: outputDataPtr=0x{1:X} outputDataLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                     label, outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            byte[] outputData = new byte[finalDataLength];
            byte[] computedTag = new byte[tagLength];
            CcmFinish(encrypt, keyParameter, macState, counterBlock, finalData, outputData, tagLength, computedTag);
            parent.Log(LogLevel.Debug, "OutputData=[{0}] ComputedTag=[{1}]", BitConverter.ToString(outputData), BitConverter.ToString(computedTag));

            WriteToRam(outputData, 0, outputDataPtr, outputDataLength);

            if(encrypt)
            {
                // Second output DMA descriptor receives the computed tag (encrypt only)
                uint outputTagPtr;
                uint outputTagLength;
                UnpackDmaDescriptor(nextDescriptorPtr, out outputTagPtr, out outputTagLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "{0}: CCM Finish: OUTPUT1: outputTagPtr=0x{1:X} outputTagLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                         label, outputTagPtr, outputTagLength, transferOptions, nextDescriptorPtr);
                WriteToRam(computedTag, 0, outputTagPtr, outputTagLength);
                return ResponseCode.Ok;
            }

            bool finishTagMatch = CcmTagsEqual(computedTag, tag, tagLength);
            parent.Log(LogLevel.Debug, "{0}: CCM Finish: tagMatch={1}", label, finishTagMatch);
            return finishTagMatch ? ResponseCode.Ok : ResponseCode.CryptoError;
        }

        private ResponseCode HandleImportKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "IMPORT_KEY: invalid parameter count");
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);

            parent.Log(LogLevel.Debug, "IMPORT_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);

            if(keyMode == KeyMode.Unprotected)
            {
                parent.Log(LogLevel.Error, "IMPORT_KEY: invalid key mode {0}", keyMode);
                return ResponseCode.InvalidParameter;
            }

            // First input DMA descriptor contains the plaintext key
            uint plaintextKeyPtr;
            uint keyLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(inputDma, out plaintextKeyPtr, out keyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "IMPORT_KEY: keyPtr=0x{0:X} keyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                     plaintextKeyPtr, keyLength, transferOptions, nextDescriptorPtr);
            byte[] key = new byte[keyLength];
            FetchFromRam(plaintextKeyPtr, key, 0, keyLength);
            parent.Log(LogLevel.Debug, "Key=[{0}]", BitConverter.ToString(key));

            // Input DMA has actually a second descriptor containing 8 bytes all set to zeros. I assume that is the auth_data[8] field,
            // TODO: For now we don't use the auth_data to do anything so we just ignore it.
            if(keyMode == KeyMode.Volatile)
            {
                // if keyMode == KeyMode.Volatile, we store the key in a dictionary.
                parent.Log(LogLevel.Debug, "IMPORT_KEY: Assigning VOLATILE key to index {0}", keyIndex);
                volatileKeys[keyIndex] = key;

                return ResponseCode.Ok;
            }
            else if(keyMode == KeyMode.Wrapped)
            {
                uint outputDataPtr;
                uint outputDataLength;
                DmaTransferOptions outputTransferOptions;
                UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out outputTransferOptions, out nextDescriptorPtr, machine);

                return WriteWrappedKeyToDescriptor(outputDataPtr, outputDataLength, key);
            }
            else if(keyMode == KeyMode.Ksu)
            {
                parent.Log(LogLevel.Debug, "IMPORT_KEY: Storing key in KSU at index {0}", keyIndex);
                ksuStorage.AddKey(keyIndex, key);

                return ResponseCode.Ok;
            }

            parent.Log(LogLevel.Error, "IMPORT_KEY: unsupported key mode {0}", keyMode);
            return ResponseCode.InvalidParameter;
        }

        private ResponseCode HandleCreateKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "CREATE_KEY: invalid parameter count");
                return ResponseCode.Abort;
            }

            uint keyMetadata = commandParams[0];
            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);

            parent.Log(LogLevel.Debug, "CREATE_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);

            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;
            EccWeirstrassKeyMetadata wprKeyMetadata = null;
            if(keyType == KeyType.EccWeirstrass)
            {
                wprKeyMetadata = UnpackWeirstrassPrimeFieldKeyMetadata(keyMetadata);
                if(wprKeyMetadata.HasDomainParameters)
                {
                    parent.Log(LogLevel.Error, "CREATE_KEY: Custom Curve not supported");
                    return ResponseCode.Abort;
                }
            }

            // Authorization data
            uint authDataPtr;
            uint authDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out authDataPtr, out authDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "CREATE_KEY: authDataPtr=0x{0:X} authDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       authDataPtr, authDataLength, transferOptions, nextDescriptorPtr);

            uint keyLength = 0;
            byte[] key = null;

            // TODO: for now we only support Raw and EccWeirstrass key types
            switch(keyType)
            {
            case KeyType.Raw:
            {
                UnpackRawKeyMetadata(keyMetadata, out keyLength);
                key = new byte[keyLength];

                for(uint i = 0; i < keyLength; i++)
                {
                    key[i] = (byte)random.Next();
                }
                break;
            }
            case KeyType.EccWeirstrass:
            {
                var generator = new ECKeyPairGenerator("ECDSA");
                var genParams = new ECKeyGenerationParameters(GetECDomainParametersFromKeyLength(wprKeyMetadata.Size*8), new SecureRandom());
                generator.Init(genParams);
                var keyPair = generator.GenerateKeyPair();
                // We keep track of the key pair so we can later on retrive the public key in the ReadPublicKey commands
                ecKeyPairs.Add(keyPair);

                keyLength = (wprKeyMetadata.HasPrivateKey ? wprKeyMetadata.Size : 0) + (wprKeyMetadata.HasPublicKey ? wprKeyMetadata.Size * 2 : 0);
                key = new byte[keyLength];
                uint offset = 0;

                // Extract private and public keys
                var privateKey = (ECPrivateKeyParameters)keyPair.Private;
                var publicKey = (ECPublicKeyParameters)keyPair.Public;

                if(wprKeyMetadata.HasPrivateKey)
                {
                    // Extract private key as byte array with proper padding
                    byte[] privateKeyBytes = BigIntegerToFixedByteArray(privateKey.D, (int)wprKeyMetadata.Size);
                    Array.Copy(privateKeyBytes, 0, key, (int)offset, (int)wprKeyMetadata.Size);
                    offset += wprKeyMetadata.Size;
                    parent.Log(LogLevel.Debug, "CREATE_KEY: EccWeirstrass: PrivateKey=[{0}]", BitConverter.ToString(privateKeyBytes));
                }

                if(wprKeyMetadata.HasPublicKey)
                {
                    // Extract public key coordinates (X and Y) with proper padding
                    byte[] publicKeyX = BigIntegerToFixedByteArray(publicKey.Q.AffineXCoord.ToBigInteger(), (int)wprKeyMetadata.Size);
                    byte[] publicKeyY = BigIntegerToFixedByteArray(publicKey.Q.AffineYCoord.ToBigInteger(), (int)wprKeyMetadata.Size);

                    parent.Log(LogLevel.Debug, "CREATE_KEY: EccWeirstrass: PublicKeyX=[{0}]", BitConverter.ToString(publicKeyX));
                    parent.Log(LogLevel.Debug, "CREATE_KEY: EccWeirstrass: PublicKeyY=[{0}]", BitConverter.ToString(publicKeyY));
                    // Copy X and Y coordinates to the key array
                    Array.Copy(publicKeyX, 0, key, (int)offset, (int)wprKeyMetadata.Size);
                    offset += wprKeyMetadata.Size;
                    Array.Copy(publicKeyY, 0, key, (int)offset, (int)wprKeyMetadata.Size);
                    offset += wprKeyMetadata.Size;
                }
                break;
            }
            default:
                parent.Log(LogLevel.Debug, "CREATE_KEY: keyType={0} not supported");
                return ResponseCode.InvalidParameter;
            }

            uint outputDataPtr;
            uint outputDataLength;
            DmaTransferOptions outputTransferOptions;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out outputTransferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "CREATE_KEY: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       outputDataPtr, outputDataLength, outputTransferOptions, nextDescriptorPtr);

            switch(keyMode)
            {
            case KeyMode.Unprotected:
            {
                if(outputDataLength != keyLength)
                {
                    parent.Log(LogLevel.Error, "CREATE_KEY: output descriptor length mismatch {0} - {1}", outputDataLength, keyLength);
                    return ResponseCode.InvalidParameter;
                }
                WriteToRam(key, 0, outputDataPtr, keyLength);
                break;
            }
            case KeyMode.Wrapped:
            {
                var result = WriteWrappedKeyToDescriptor(outputDataPtr, outputDataLength, key);
                if(result != ResponseCode.Ok)
                {
                    return result;
                }
                break;
            }
            case KeyMode.Volatile:
            {
                parent.Log(LogLevel.Debug, "CREATE_KEY: Assigning VOLATILE key to index {0}", keyIndex);
                volatileKeys[keyIndex] = key;
                break;
            }
            case KeyMode.Ksu:
            {
                parent.Log(LogLevel.Debug, "CREATE_KEY: Storing key in KSU at index {0}", keyIndex);
                ksuStorage.AddKey(keyIndex, key);
                break;
            }
            default:
                parent.Log(LogLevel.Error, "CREATE_KEY: keyMode={0} invalid", keyMode);
                return ResponseCode.InvalidParameter;
            }

            return ResponseCode.Ok;
        }

        private ResponseCode HandleReadPublicKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "READ_PUB_KEY: invalid parameter count");
                return ResponseCode.Abort;
            }

            uint keyMetadata = commandParams[0];
            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);

            parent.Log(LogLevel.Debug, "READ_PUB_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);

            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;

            // Authorization data
            uint authDataPtr;
            uint authDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out authDataPtr, out authDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "READ_PUB_KEY: authDataPtr=0x{0:X} authDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       authDataPtr, authDataLength, transferOptions, nextDescriptorPtr);

            // Input key
            uint inputKeyPtr;
            uint inputKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputKeyPtr, out inputKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "READ_PUB_KEY: inputKeyPtr=0x{0:X} inputKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       inputKeyPtr, inputKeyLength, transferOptions, nextDescriptorPtr);

            uint privateInputKeyLength = 0;

            // TODO: for now we only support EccWeirstrass key type
            if(keyType == KeyType.EccWeirstrass)
            {
                var wprKeyMetadata = UnpackWeirstrassPrimeFieldKeyMetadata(keyMetadata);
                privateInputKeyLength = wprKeyMetadata.Size;
                if(!wprKeyMetadata.HasPrivateKey)
                {
                    parent.Log(LogLevel.Error, "READ_PUB_KEY: Input EccWeirstrass has no private key");
                    return ResponseCode.InvalidParameter;
                }
            }
            else
            {
                parent.Log(LogLevel.Error, "READ_PUB_KEY: KeyType={0} not supported", keyType);
                return ResponseCode.InvalidParameter;
            }

            byte[] privateInputKey = ReadKey(keyMode, keyIndex, inputKeyPtr, inputKeyLength);
            if(privateInputKey == null)
            {
                return ResponseCode.InvalidParameter;
            }
            if(privateInputKey.Length != privateInputKeyLength)
            {
                parent.Log(LogLevel.Error, "READ_PUB_KEY: private key length mismatch got={0} expected={1}", privateInputKey.Length, privateInputKeyLength);
                return ResponseCode.InvalidParameter;
            }

            parent.Log(LogLevel.Debug, "READ_PUB_KEY: PrivateKey=[{0}]", BitConverter.ToString(privateInputKey));

            // Find the corresponding key pair based on the private key
            var keyPair = LookUpKeyPair(privateInputKey);
            if(keyPair == null)
            {
                parent.Log(LogLevel.Error, "READ_PUB_KEY: corresponding key pair not found");
                return ResponseCode.InvalidParameter;
            }

            // Extract public key coordinates (X and Y) with proper padding
            byte[] publicKeyX = BigIntegerToFixedByteArray(((ECPublicKeyParameters)(keyPair.Public)).Q.AffineXCoord.ToBigInteger(), (int)privateInputKeyLength);
            byte[] publicKeyY = BigIntegerToFixedByteArray(((ECPublicKeyParameters)(keyPair.Public)).Q.AffineYCoord.ToBigInteger(), (int)privateInputKeyLength);

            // Combine X and Y coordinates
            byte[] outputPublicKey = new byte[privateInputKeyLength * 2];
            Array.Copy(publicKeyX, 0, outputPublicKey, 0, (int)privateInputKeyLength);
            Array.Copy(publicKeyY, 0, outputPublicKey, (int)privateInputKeyLength, (int)privateInputKeyLength);

            uint outputDataPtr;
            uint outputDataLength;
            DmaTransferOptions outputTransferOptions;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out outputTransferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "READ_PUB_KEY: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       outputDataPtr, outputDataLength, outputTransferOptions, nextDescriptorPtr);
            if(outputDataLength != outputPublicKey.Length)
            {
                parent.Log(LogLevel.Error, "READ_PUB_KEY: outputDataLength mismatch {0} - {1}", outputDataLength, outputPublicKey.Length);
                return ResponseCode.InvalidParameter;
            }

            WriteToRam(outputPublicKey, 0, outputDataPtr, outputDataLength);
            parent.Log(LogLevel.Debug, "READ_PUB_KEY: PublicKey=[{0}]", BitConverter.ToString(outputPublicKey));

            return ResponseCode.Ok;
        }

        private ResponseCode HandleDiffieHellmanCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount)
        {
            if(commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: invalid parameter count");
                return ResponseCode.Abort;
            }

            uint keyMetadata = commandParams[0];
            uint outputKeyMetadata = commandParams[1];
            KeyMode keyMode, outputKeyMode;
            KeyType keyType, outputKeyType;
            KeyRestriction keyRestriction, outputKeyRestriction;
            uint keyIndex, outputKeyIndex;
            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;

            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "DIFFIE_HELLMAN: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                     keyIndex, keyType, keyMode, keyRestriction);

            uint netPrivateInputKeyLength = 0;

            // TODO: for now we only support EccWeirstrass key type
            if(keyType != KeyType.EccWeirstrass)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: KeyType={0} not supported", keyType);
                return ResponseCode.InvalidParameter;
            }

            var wprKeyMetadata = UnpackWeirstrassPrimeFieldKeyMetadata(keyMetadata);
            netPrivateInputKeyLength = wprKeyMetadata.Size;
            if(!wprKeyMetadata.HasPrivateKey)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: Input EccWeirstrass has no private key");
                return ResponseCode.InvalidParameter;
            }

            // Key Metadata for derived key (shared secret). Must be a RAW key with the correct length.
            UnpackKeyMetadata(outputKeyMetadata, out outputKeyIndex, out outputKeyType, out outputKeyMode, out outputKeyRestriction);
            parent.Log(LogLevel.Debug, "DIFFIE_HELLMAN: outputKeyIndex={0} outputKeyType={1} outputKeyMode={2} outputKeyRestriction={3}",
                     outputKeyIndex, outputKeyType, outputKeyMode, outputKeyRestriction);

            if(outputKeyType != KeyType.Raw)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: Derived key must be of Raw type (KeyType={0})", outputKeyType);
                return ResponseCode.InvalidParameter;
            }

            // Input key authorization data
            uint authDataPtr;
            uint authDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out authDataPtr, out authDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "DIFFIE_HELLMAN: authDataPtr=0x{0:X} authDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       authDataPtr, authDataLength, transferOptions, nextDescriptorPtr);

            // Input key
            uint privateInputKeyPtr;
            uint privateInputKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out privateInputKeyPtr, out privateInputKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "DIFFIE_HELLMAN: privateInputKeyPtr=0x{0:X} privateInputKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       privateInputKeyPtr, privateInputKeyLength, transferOptions, nextDescriptorPtr);

            byte[] privateInputKey = ReadKey(keyMode, keyIndex, privateInputKeyPtr, privateInputKeyLength);
            if(privateInputKey == null)
            {
                return ResponseCode.InvalidParameter;
            }
            if(privateInputKey.Length != netPrivateInputKeyLength)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: private key length mismatch got={0} expected={1}", privateInputKey.Length, netPrivateInputKeyLength);
                return ResponseCode.InvalidParameter;
            }

            // Public input key (other party)
            uint publicInputKeyPtr;
            uint publicInputKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out publicInputKeyPtr, out publicInputKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "DIFFIE_HELLMAN: publicInputKeyPtr=0x{0:X} publicInputKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       publicInputKeyPtr, publicInputKeyLength, transferOptions, nextDescriptorPtr);

            if(publicInputKeyLength != netPrivateInputKeyLength * 2)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: public input key length mismatch {0} - {1}", publicInputKeyLength, netPrivateInputKeyLength * 2);
                return ResponseCode.InvalidParameter;
            }
            byte[] publicInputKey = new byte[publicInputKeyLength];
            FetchFromRam(publicInputKeyPtr, publicInputKey, 0, publicInputKeyLength);

            // TODO: the following code assumes keyType to be EccWeirstrass. This was already checked earlier in this method.

            var domainParams = GetECDomainParametersFromKeyLength(netPrivateInputKeyLength * 8);

            // Create ECPrivateKeyParameters from private key bytes
            var privateKeyValue = new BigInteger(1, privateInputKey); // 1 for positive sign
            var privateKeyParam = new ECPrivateKeyParameters(privateKeyValue, domainParams);

            // Create ECPublicKeyParameters from public key bytes
            int coordinateSize = publicInputKey.Length / 2;
            byte[] xBytes = new byte[coordinateSize];
            byte[] yBytes = new byte[coordinateSize];

            Array.Copy(publicInputKey, 0, xBytes, 0, coordinateSize);
            Array.Copy(publicInputKey, coordinateSize, yBytes, 0, coordinateSize);

            var x = new BigInteger(1, xBytes);
            var y = new BigInteger(1, yBytes);

            var point = domainParams.Curve.CreatePoint(x, y);
            var publicKeyParam = new ECPublicKeyParameters(point, domainParams);

            // Perform ECDH calculation manually to get both X and Y coordinates
            var sharedPoint = publicKeyParam.Q.Multiply(privateKeyValue).Normalize();

            // Extract X and Y coordinates with proper padding to coordinate size
            byte[] sharedSecretX = BigIntegerToFixedByteArray(sharedPoint.AffineXCoord.ToBigInteger(), (int)netPrivateInputKeyLength);
            byte[] sharedSecretY = BigIntegerToFixedByteArray(sharedPoint.AffineYCoord.ToBigInteger(), (int)netPrivateInputKeyLength);

            parent.Log(LogLevel.Debug, "DIFFIE_HELLMAN: SharedSecretX=[{0}]", BitConverter.ToString(sharedSecretX));
            parent.Log(LogLevel.Debug, "DIFFIE_HELLMAN: SharedSecretY=[{0}]", BitConverter.ToString(sharedSecretY));

            // Use X coordinate as the shared secret (standard ECDH behavior)
            byte[] sharedSecret = sharedSecretX;

            // Output DMA descriptor contains the shared secret
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "DIFFIE_HELLMAN: OUTPUT0: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                        outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);

            // Check if output buffer expects both X and Y coordinates (64 bytes total for P-256)
            if(outputDataLength != sharedSecretX.Length + sharedSecretY.Length)
            {
                parent.Log(LogLevel.Error, "DIFFIE_HELLMAN: Output descriptor length mismatch {0} vs expected {1}", outputDataLength, sharedSecretX.Length + sharedSecretY.Length);
                return ResponseCode.InvalidParameter;
            }

            WriteToRam(sharedSecretX, 0, outputDataPtr, (uint)sharedSecretX.Length);
            WriteToRam(sharedSecretY, 0, outputDataPtr + (uint)sharedSecretX.Length, (uint)sharedSecretY.Length);
            return ResponseCode.Ok;
        }

        // Handles ECDSA / EdDSA sign and verify commands (CommandId.SignatureSign / SignatureVerify / EddsaSign / EddsaVerify).
        //   params[0] = keyspec metadata, params[1] = message length
        //   INPUT0 = auth data, INPUT1 = key material
        //   ECDSA  sign:   INPUT2=message                          -> OUTPUT0=signature (r||s, 2*keySize bytes)
        //   EdDSA  sign:   INPUT2=message, INPUT3=message (again)  -> OUTPUT0=signature (64 bytes)
        //   ECDSA  verify: INPUT2=message, INPUT3=signature        (no outputs)
        //   EdDSA  verify: INPUT2=signature, INPUT3=message        (no outputs)
        // For ECDSA, command_options bits [11:8] select the hash algorithm when the host has not pre-hashed
        // the message. When those bits are zero the message bytes are treated as a precomputed digest.
        private ResponseCode HandleSignatureCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions, bool isVerify, bool isEddsa)
        {
            string logTag = (isEddsa ? "EDDSA_" : "ECDSA_") + (isVerify ? "VERIFY" : "SIGN");

            if(commandParamsCount != 2)
            {
                parent.Log(LogLevel.Error, "{0}: invalid parameter count {1}", logTag, commandParamsCount);
                return ResponseCode.Abort;
            }

            uint keyMetadata = commandParams[0];
            uint messageLength = commandParams[1];

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);

            parent.Log(LogLevel.Debug, "{0}: keyIndex={1} keyType={2} keyMode={3} keyRestriction={4} messageLength={5} options=0x{6:X}",
                       logTag, keyIndex, keyType, keyMode, keyRestriction, messageLength, commandOptions);

            // Validate algorithm vs key type and derive key size in bytes from the ASYM size mask (bits [6:0]).
            // The size field stores (size - 1) per sl_se_manager_key_handling.c:673-674, decoded with +1 at line 905.
            uint keySize;
            bool hasPrivateKey = (keyMetadata & 0x4000) != 0;
            bool hasPublicKey = (keyMetadata & 0x2000) != 0;
            if(isEddsa)
            {
                if(keyType != KeyType.EccEddsa)
                {
                    parent.Log(LogLevel.Error, "{0}: unsupported key type {1} (expected EccEddsa)", logTag, keyType);
                    return ResponseCode.InvalidParameter;
                }
                keySize = 32;
            }
            else
            {
                if(keyType != KeyType.EccWeirstrass)
                {
                    parent.Log(LogLevel.Error, "{0}: unsupported key type {1} (expected EccWeirstrass)", logTag, keyType);
                    return ResponseCode.InvalidParameter;
                }
                keySize = (keyMetadata & 0x7F) + 1;
            }

            // Verify requires public key material in the buffer; sign requires private key material.
            if(isVerify)
            {
                if(!hasPublicKey)
                {
                    parent.Log(LogLevel.Error, "{0}: verify requires a public key in the key buffer", logTag);
                    return ResponseCode.InvalidParameter;
                }
            }
            else
            {
                if(!hasPrivateKey)
                {
                    parent.Log(LogLevel.Error, "{0}: sign requires a private key in the key buffer", logTag);
                    return ResponseCode.InvalidParameter;
                }
            }

            // INPUT0: authorization data (unused by the model)
            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;
            uint authDataPtr;
            uint authDataLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out authDataPtr, out authDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: authDataPtr=0x{1:X} authDataLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                       logTag, authDataPtr, authDataLength, transferOptions, nextDescriptorPtr);

            // INPUT1: key material
            uint keyBufferPtr;
            uint keyBufferLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out keyBufferPtr, out keyBufferLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: keyBufferPtr=0x{1:X} keyBufferLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                       logTag, keyBufferPtr, keyBufferLength, transferOptions, nextDescriptorPtr);

            byte[] keyBuffer = ReadKey(keyMode, keyIndex, keyBufferPtr, keyBufferLength);
            if(keyBuffer == null)
            {
                return ResponseCode.InvalidParameter;
            }

            // Compute expected key buffer layout: optional private || optional public (X||Y for ECDSA, 32 bytes for EdDSA).
            uint expectedKeyLength = 0;
            if(hasPrivateKey)
            {
                expectedKeyLength += keySize;
            }
            if(hasPublicKey)
            {
                expectedKeyLength += (isEddsa ? keySize : (keySize * 2));
            }
            if(keyBuffer.Length != expectedKeyLength)
            {
                parent.Log(LogLevel.Error, "{0}: key buffer length mismatch got={1} expected={2}", logTag, keyBuffer.Length, expectedKeyLength);
                return ResponseCode.InvalidParameter;
            }

            // Extract private / public bytes from the key buffer.
            byte[] privateKeyBytes = null;
            byte[] publicKeyBytes = null;
            {
                uint offset = 0;
                if(hasPrivateKey)
                {
                    privateKeyBytes = new byte[keySize];
                    Array.Copy(keyBuffer, (int)offset, privateKeyBytes, 0, (int)keySize);
                    offset += keySize;
                }
                if(hasPublicKey)
                {
                    uint pubLen = isEddsa ? keySize : (keySize * 2);
                    publicKeyBytes = new byte[pubLen];
                    Array.Copy(keyBuffer, (int)offset, publicKeyBytes, 0, (int)pubLen);
                }
            }

            // EdDSA sign/verify path
            if(isEddsa)
            {
                if(isVerify)
                {
                    // INPUT2 = signature (64 bytes), INPUT3 = message
                    uint signaturePtr;
                    uint signatureLength;
                    UnpackDmaDescriptor(nextDescriptorPtr, out signaturePtr, out signatureLength, out transferOptions, out nextDescriptorPtr, machine);
                    parent.Log(LogLevel.Debug, "{0}: signaturePtr=0x{1:X} signatureLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                               logTag, signaturePtr, signatureLength, transferOptions, nextDescriptorPtr);
                    if(signatureLength != 64)
                    {
                        parent.Log(LogLevel.Error, "{0}: EdDSA signature length must be 64 (got {1})", logTag, signatureLength);
                        return ResponseCode.InvalidParameter;
                    }

                    uint messagePtr;
                    uint messageDescLength;
                    UnpackDmaDescriptor(nextDescriptorPtr, out messagePtr, out messageDescLength, out transferOptions, out nextDescriptorPtr, machine);
                    parent.Log(LogLevel.Debug, "{0}: messagePtr=0x{1:X} messageDescLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                               logTag, messagePtr, messageDescLength, transferOptions, nextDescriptorPtr);
                    if(messageDescLength != messageLength)
                    {
                        parent.Log(LogLevel.Error, "{0}: message descriptor length {1} != params message length {2}", logTag, messageDescLength, messageLength);
                        return ResponseCode.InvalidParameter;
                    }

                    byte[] signature = new byte[signatureLength];
                    FetchFromRam(signaturePtr, signature, 0, signatureLength);
                    byte[] message = new byte[messageLength];
                    if(messageLength > 0)
                    {
                        FetchFromRam(messagePtr, message, 0, messageLength);
                    }

                    try
                    {
                        var pubKeyParam = new Ed25519PublicKeyParameters(publicKeyBytes, 0);
                        var verifier = new Ed25519Signer();
                        verifier.Init(false, pubKeyParam);
                        verifier.BlockUpdate(message, 0, message.Length);
                        bool ok = verifier.VerifySignature(signature);
                        return ok ? ResponseCode.Ok : ResponseCode.InvalidSignature;
                    }
                    catch(Exception e)
                    {
                        parent.Log(LogLevel.Error, "{0}: EdDSA verify exception: {1}", logTag, e.Message);
                        return ResponseCode.InvalidParameter;
                    }
                }
                else
                {
                    // INPUT2 = message, INPUT3 = message (again)
                    uint messagePtr;
                    uint messageDescLength;
                    UnpackDmaDescriptor(nextDescriptorPtr, out messagePtr, out messageDescLength, out transferOptions, out nextDescriptorPtr, machine);
                    parent.Log(LogLevel.Debug, "{0}: messagePtr=0x{1:X} messageDescLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                               logTag, messagePtr, messageDescLength, transferOptions, nextDescriptorPtr);

                    uint message2Ptr;
                    uint message2Length;
                    UnpackDmaDescriptor(nextDescriptorPtr, out message2Ptr, out message2Length, out transferOptions, out nextDescriptorPtr, machine);
                    parent.Log(LogLevel.Debug, "{0}: message2Ptr=0x{1:X} message2Length={2} options={3} nextDescriptorPtr=0x{4:X}",
                               logTag, message2Ptr, message2Length, transferOptions, nextDescriptorPtr);

                    if(messageDescLength != messageLength || message2Length != messageLength)
                    {
                        parent.Log(LogLevel.Error, "{0}: message descriptor length mismatch ({1}, {2}) vs params {3}",
                                   logTag, messageDescLength, message2Length, messageLength);
                        return ResponseCode.InvalidParameter;
                    }

                    byte[] message = new byte[messageLength];
                    if(messageLength > 0)
                    {
                        FetchFromRam(messagePtr, message, 0, messageLength);
                    }

                    byte[] signature;
                    try
                    {
                        var privKeyParam = new Ed25519PrivateKeyParameters(privateKeyBytes, 0);
                        var signer = new Ed25519Signer();
                        signer.Init(true, privKeyParam);
                        signer.BlockUpdate(message, 0, message.Length);
                        signature = signer.GenerateSignature();
                    }
                    catch(Exception e)
                    {
                        parent.Log(LogLevel.Error, "{0}: EdDSA sign exception: {1}", logTag, e.Message);
                        return ResponseCode.InvalidParameter;
                    }

                    return WriteSignatureToOutput(outputDma, signature, logTag);
                }
            }

            // ECDSA sign/verify path
            ECDomainParameters domainParams;
            try
            {
                domainParams = GetECDomainParametersFromKeyLength(keySize * 8);
            }
            catch(Exception e)
            {
                parent.Log(LogLevel.Error, "{0}: unsupported ECC key size {1} bytes: {2}", logTag, keySize, e.Message);
                return ResponseCode.InvalidParameter;
            }

            // Optional hashing: if SHA bits are set in command_options, hash the message; otherwise treat the
            // message bytes as a precomputed digest.
            uint hashSelector = (commandOptions >> 8) & 0xF;
            ShaMode? hashMode = null;
            if(hashSelector != 0)
            {
                hashMode = (ShaMode)hashSelector;
            }

            // Read message and signature descriptors in the order dictated by the operation.
            byte[] messageOrDigest;
            byte[] signatureForVerify = null;
            uint signatureLengthExpected = keySize * 2;
            if(isVerify)
            {
                // INPUT2 = message, INPUT3 = signature
                uint messagePtr;
                uint messageDescLength;
                UnpackDmaDescriptor(nextDescriptorPtr, out messagePtr, out messageDescLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "{0}: messagePtr=0x{1:X} messageDescLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                           logTag, messagePtr, messageDescLength, transferOptions, nextDescriptorPtr);

                uint signaturePtr;
                uint signatureDescLength;
                UnpackDmaDescriptor(nextDescriptorPtr, out signaturePtr, out signatureDescLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "{0}: signaturePtr=0x{1:X} signatureDescLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                           logTag, signaturePtr, signatureDescLength, transferOptions, nextDescriptorPtr);

                if(messageDescLength != messageLength)
                {
                    parent.Log(LogLevel.Error, "{0}: message descriptor length {1} != params {2}", logTag, messageDescLength, messageLength);
                    return ResponseCode.InvalidParameter;
                }
                if(signatureDescLength != signatureLengthExpected)
                {
                    parent.Log(LogLevel.Error, "{0}: signature descriptor length {1} != expected {2}", logTag, signatureDescLength, signatureLengthExpected);
                    return ResponseCode.InvalidParameter;
                }

                messageOrDigest = new byte[messageLength];
                if(messageLength > 0)
                {
                    FetchFromRam(messagePtr, messageOrDigest, 0, messageLength);
                }
                signatureForVerify = new byte[signatureDescLength];
                FetchFromRam(signaturePtr, signatureForVerify, 0, signatureDescLength);
            }
            else
            {
                // INPUT2 = message
                uint messagePtr;
                uint messageDescLength;
                UnpackDmaDescriptor(nextDescriptorPtr, out messagePtr, out messageDescLength, out transferOptions, out nextDescriptorPtr, machine);
                parent.Log(LogLevel.Debug, "{0}: messagePtr=0x{1:X} messageDescLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                           logTag, messagePtr, messageDescLength, transferOptions, nextDescriptorPtr);
                if(messageDescLength != messageLength)
                {
                    parent.Log(LogLevel.Error, "{0}: message descriptor length {1} != params {2}", logTag, messageDescLength, messageLength);
                    return ResponseCode.InvalidParameter;
                }
                messageOrDigest = new byte[messageLength];
                if(messageLength > 0)
                {
                    FetchFromRam(messagePtr, messageOrDigest, 0, messageLength);
                }
            }

            // If a hash algorithm was requested, hash the message; otherwise treat it as the digest already.
            byte[] digest;
            if(hashMode.HasValue)
            {
                var digestEngine = CreateHashEngine(hashMode.Value);
                if(digestEngine == null)
                {
                    parent.Log(LogLevel.Error, "{0}: unsupported hash mode 0x{1:X}", logTag, (uint)hashMode.Value);
                    return ResponseCode.InvalidParameter;
                }
                digestEngine.BlockUpdate(messageOrDigest, 0, messageOrDigest.Length);
                digest = new byte[digestEngine.GetDigestSize()];
                digestEngine.DoFinal(digest, 0);
            }
            else
            {
                digest = messageOrDigest;
            }

            try
            {
                if(isVerify)
                {
                    // Decode the public key (X||Y) into an ECPoint.
                    byte[] xBytes = new byte[keySize];
                    byte[] yBytes = new byte[keySize];
                    Array.Copy(publicKeyBytes, 0, xBytes, 0, (int)keySize);
                    Array.Copy(publicKeyBytes, (int)keySize, yBytes, 0, (int)keySize);
                    var qx = new BigInteger(1, xBytes);
                    var qy = new BigInteger(1, yBytes);
                    var q = domainParams.Curve.CreatePoint(qx, qy);
                    if(!q.IsValid())
                    {
                        parent.Log(LogLevel.Error, "{0}: public key point is not on the curve", logTag);
                        return ResponseCode.InvalidSignature;
                    }
                    var pubKeyParam = new ECPublicKeyParameters(q, domainParams);

                    // Split signature into r || s.
                    byte[] rBytes = new byte[keySize];
                    byte[] sBytes = new byte[keySize];
                    Array.Copy(signatureForVerify, 0, rBytes, 0, (int)keySize);
                    Array.Copy(signatureForVerify, (int)keySize, sBytes, 0, (int)keySize);
                    var r = new BigInteger(1, rBytes);
                    var s = new BigInteger(1, sBytes);

                    var verifier = new ECDsaSigner();
                    verifier.Init(false, pubKeyParam);
                    bool ok = verifier.VerifySignature(digest, r, s);
                    return ok ? ResponseCode.Ok : ResponseCode.InvalidSignature;
                }
                else
                {
                    var d = new BigInteger(1, privateKeyBytes);
                    var privKeyParam = new ECPrivateKeyParameters(d, domainParams);
                    var signer = new ECDsaSigner();
                    signer.Init(true, privKeyParam);
                    var rs = signer.GenerateSignature(digest);
                    byte[] rEncoded = BigIntegerToFixedByteArray(rs[0], (int)keySize);
                    byte[] sEncoded = BigIntegerToFixedByteArray(rs[1], (int)keySize);

                    byte[] signature = new byte[signatureLengthExpected];
                    Array.Copy(rEncoded, 0, signature, 0, (int)keySize);
                    Array.Copy(sEncoded, 0, signature, (int)keySize, (int)keySize);
                    return WriteSignatureToOutput(outputDma, signature, logTag);
                }
            }
            catch(Exception e)
            {
                parent.Log(LogLevel.Error, "{0}: ECDSA crypto exception: {1}", logTag, e.Message);
                return ResponseCode.InvalidParameter;
            }
        }

        private ResponseCode HandleJPakeRound1Command(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            int expectedCommandParamsCount = -1;
            bool isGenerateCommand = (commandOptions & 0x100) == 0;
            if(isGenerateCommand)
            {
                expectedCommandParamsCount = 2;
            }
            else
            {
                expectedCommandParamsCount = 3;
            }

            if(commandParamsCount != expectedCommandParamsCount)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1: invalid parameter count {0} vs expected {1} (isGenerateCommand={2})", commandParamsCount, expectedCommandParamsCount, isGenerateCommand);
                return ResponseCode.Abort;
            }

            uint keyMetadata = commandParams[0];
            uint userIdSize = commandParams[1];
            uint peerUserIdSize = 0;
            if(!isGenerateCommand)
            {
                peerUserIdSize = commandParams[2];
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;

            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}, userIdSize={4} peerUserIdSize={5}",
                       keyIndex, keyType, keyMode, keyRestriction, userIdSize, peerUserIdSize);

            // The JPAKE implementation currently only supports 256-bit prime Weierstraß curves, typically NIST P-256, 
            // and is hardcoded to use SHA256 as the point function.
            if(keyType != KeyType.EccWeirstrass)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1: KeyType={0} not supported", keyType);
                return ResponseCode.InvalidParameter;
            }

            // Consume input key descriptor (should be 0-length)
            uint inputKeyPtr;
            uint inputKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputKeyPtr, out inputKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1: inputKeyPtr=0x{0:X} inputKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       inputKeyPtr, inputKeyLength, transferOptions, nextDescriptorPtr);

            return (isGenerateCommand) ? HandleJPakeRound1GenerateCommand(nextDescriptorPtr, outputDma, userIdSize)
                                       : HandleJPakeRound1VerifyCommand(nextDescriptorPtr, outputDma, userIdSize, peerUserIdSize);
        }

        private ResponseCode HandleJPakeRound1GenerateCommand(uint inputDma, uint outputDma, uint userIdLengthFromCommandOptions)
        {
            uint nextDescriptorPtr;
            DmaTransferOptions transferOptions;

            // User ID
            uint userIdPtr;
            uint userIdLength;
            UnpackDmaDescriptor(inputDma, out userIdPtr, out userIdLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_GEN: userIdPtr=0x{0:X} userIdLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       userIdPtr, userIdLength, transferOptions, nextDescriptorPtr);
            if(userIdLength != userIdLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_GEN: User ID length mismatch, expected {0} got {1}", userIdLengthFromCommandOptions, userIdLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] userId = new byte[userIdLength];
            FetchFromRam(userIdPtr, userId, 0, userIdLength);

            // EC-JPAKE Round 1 Generation using NIST P-256 curve and SHA256
            // Get NIST P-256 curve parameters
            var curve = GetP256CurveParameters();
            var random = new SecureRandom();

            // Generate ephemeral key pairs (x1, g1) and (x2, g2)
            // x1, x2 are private scalars, g1 = x1*G, g2 = x2*G where G is the base point
            // TODO: if X1=O or X2=O, we should reject X1 and X2 and redraw (practically, never happens)
            var x1 = GenerateRandomScalar(curve, random);
            var x2 = GenerateRandomScalar(curve, random);

            var pointX1 = curve.G.Multiply(x1).Normalize();
            var pointX2 = curve.G.Multiply(x2).Normalize();

            // Generate Schnorr proofs for knowledge of discrete logarithms
            // ZKP{x1}: Prove knowledge of x1 such that g1 = x1*G
            var zkp1 = GenerateSchnorrProof(curve, x1, pointX1, userId, random, "JPAKE-X1");

            // ZKP{x2}: Prove knowledge of x2 such that g2 = x2*G
            var zkp2 = GenerateSchnorrProof(curve, x2, pointX2, userId, random, "JPAKE-X2");

            byte[] x2Bytes = BigIntegerToFixedByteArray(x2, 32);
            byte[] pointX1Bytes = EncodeECPointToBytes(pointX1);
            byte[] pointX2Bytes = EncodeECPointToBytes(pointX2);
            byte[] zkp1Bytes = EncodeSchnorrProofToBytes(zkp1);
            byte[] zkp2Bytes = EncodeSchnorrProofToBytes(zkp2);

            // First output DMA descriptor - random 32-byte scalar "S2"
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            if(outputDataLength != x2Bytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_GEN: OUTPUT0 (random scalar S2): Data length mismatch, expected {0} got {1}", x2Bytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(x2Bytes, 0, outputDataPtr, (uint)x2Bytes.Length);

            // Second output DMA descriptor - point X1
            UnpackDmaDescriptor(nextDescriptorPtr, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            if(outputDataLength != pointX1Bytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_GEN: OUTPUT1 (point X1): Data length mismatch, expected {0} got {1}", pointX1Bytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(pointX1Bytes, 0, outputDataPtr, (uint)pointX1Bytes.Length);

            // Third output DMA descriptor - X1 ZKP
            UnpackDmaDescriptor(nextDescriptorPtr, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            if(outputDataLength != zkp1Bytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_GEN: OUTPUT2 (X1 ZKP): Data length mismatch, expected {0} got {1}", zkp1Bytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(zkp1Bytes, 0, outputDataPtr, (uint)zkp1Bytes.Length);

            // Fourth output DMA descriptor - point X2
            UnpackDmaDescriptor(nextDescriptorPtr, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            if(outputDataLength != pointX2Bytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_GEN: OUTPUT3 (point X2): Data length mismatch, expected {0} got {1}", pointX2Bytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(pointX2Bytes, 0, outputDataPtr, (uint)pointX2Bytes.Length);

            // Fifth output DMA descriptor - X2 ZKP
            UnpackDmaDescriptor(nextDescriptorPtr, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            if(outputDataLength != zkp2Bytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_GEN: OUTPUT4 (X2 ZKP): Data length mismatch, expected {0} got {1}", zkp2Bytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(zkp2Bytes, 0, outputDataPtr, (uint)zkp2Bytes.Length);

            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_GEN: Random Scalar S2=[{0}]",
                       BitConverter.ToString(x2Bytes));
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_GEN: Generated X1=[{0}], X2=[{1}]",
                       BitConverter.ToString(pointX1Bytes), BitConverter.ToString(pointX2Bytes));
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_GEN: Generated ZKP1=[{0}], ZKP2=[{1}]",
                       BitConverter.ToString(zkp1Bytes), BitConverter.ToString(zkp2Bytes));

            return ResponseCode.Ok;
        }

#pragma warning disable IDE0060
        private ResponseCode HandleJPakeRound1VerifyCommand(uint inputDma, uint outputDma, uint userIdLengthFromCommandOptions, uint peerUserIdLengthFromCommandOptions)
#pragma warning restore IDE0060
        {
            uint nextDescriptorPtr;
            DmaTransferOptions transferOptions;

            // User ID
            uint userIdPtr;
            uint userIdLength;
            UnpackDmaDescriptor(inputDma, out userIdPtr, out userIdLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_VER: userIdPtr=0x{0:X} userIdLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       userIdPtr, userIdLength, transferOptions, nextDescriptorPtr);
            if(userIdLength != userIdLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_VER: User ID length mismatch, expected {0} got {1}", userIdLengthFromCommandOptions, userIdLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] userId = new byte[userIdLength];
            FetchFromRam(userIdPtr, userId, 0, userIdLength);

            // Peer user ID
            uint peerUserIdPtr;
            uint peerUserIdLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out peerUserIdPtr, out peerUserIdLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_VER: peerUserIdPtr=0x{0:X} peerUserIdLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       peerUserIdPtr, peerUserIdLength, transferOptions, nextDescriptorPtr);
            if(peerUserIdLength != peerUserIdLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_VER: Peer User ID length mismatch, expected {0} got {1}", peerUserIdLengthFromCommandOptions, peerUserIdLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] peerUserId = new byte[peerUserIdLength];
            FetchFromRam(peerUserIdPtr, peerUserId, 0, peerUserIdLength);

            // Point X3 (from other party round 1)
            uint pointX3Ptr;
            uint pointX3Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX3Ptr, out pointX3Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_VER: pointX3Ptr=0x{0:X} pointX3Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX3Ptr, pointX3Length, transferOptions, nextDescriptorPtr);
            byte[] pointX3 = new byte[pointX3Length];
            FetchFromRam(pointX3Ptr, pointX3, 0, pointX3Length);

            // Zero knowledge proof for X3 (from other party round 1)
            uint zkp3Ptr;
            uint zkp3Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out zkp3Ptr, out zkp3Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_VER: zkp3Ptr=0x{0:X} zkp3Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       zkp3Ptr, zkp3Length, transferOptions, nextDescriptorPtr);
            byte[] zkp3 = new byte[zkp3Length];
            FetchFromRam(zkp3Ptr, zkp3, 0, zkp3Length);

            // Point X4 (from other party round 1)
            uint pointX4Ptr;
            uint pointX4Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX4Ptr, out pointX4Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_VER: pointX4Ptr=0x{0:X} pointX4Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX4Ptr, pointX4Length, transferOptions, nextDescriptorPtr);
            byte[] pointX4 = new byte[pointX4Length];
            FetchFromRam(pointX4Ptr, pointX4, 0, pointX4Length);

            // Zero knowledge proof for X4 (from other party round 1)
            uint zkp4Ptr;
            uint zkp4Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out zkp4Ptr, out zkp4Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_VER: zkp4Ptr=0x{0:X} zkp4Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       zkp4Ptr, zkp4Length, transferOptions, nextDescriptorPtr);
            byte[] zkp4 = new byte[zkp4Length];
            FetchFromRam(zkp4Ptr, zkp4, 0, zkp4Length);

            // Verify the Schnorr zero-knowledge proofs for X3 and X4
            var curve = GetP256CurveParameters();

            ECPoint x3Point, x4Point;
            SchnorrProof zkp3Proof, zkp4Proof;

            try
            {
                x3Point = DecodeECPointFromBytes(pointX3, curve);
                x4Point = DecodeECPointFromBytes(pointX4, curve);
                zkp3Proof = DecodeSchnorrProofFromBytes(zkp3, curve);
                zkp4Proof = DecodeSchnorrProofFromBytes(zkp4, curve);
            }
            catch(Exception e)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_VER: Failed to decode points or proofs: {0}", e.Message);
                return ResponseCode.InvalidParameter;
            }

            if(x3Point.IsInfinity || x4Point.IsInfinity || zkp3Proof.V.IsInfinity || zkp4Proof.V.IsInfinity)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_VER: One or more points are at infinity");
                return ResponseCode.InvalidParameter;
            }

            if(!x3Point.IsValid() || !x4Point.IsValid() || !zkp3Proof.V.IsValid() || !zkp4Proof.V.IsValid())
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_1_VER: One or more points are not valid");
                return ResponseCode.InvalidParameter;
            }

            // X3 and X4 must be different points
            if(x3Point.Equals(x4Point))
            {
                parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_VER: Received points X3 and X4 are identical");
                return ResponseCode.InvalidParameter;
            }

            // Verify ZKP for X3: Check that the other party knows x3 such that X3 = x3*G
            bool zkp3Valid = VerifySchnorrProof(curve, x3Point, zkp3Proof, peerUserId, "JPAKE-X1");

            // Verify ZKP for X4: Check that the other party knows x4 such that X4 = x4*G
            bool zkp4Valid = VerifySchnorrProof(curve, x4Point, zkp4Proof, peerUserId, "JPAKE-X2");

            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_VER: X3 verification: {0}, X4 verification: {1}", zkp3Valid, zkp4Valid);

            // Both proofs must be valid for the verification to succeed
            if(!zkp3Valid || !zkp4Valid)
            {
                parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_VER: Zero-knowledge proof verification failed");
                return ResponseCode.CryptoError;
            }

            parent.Log(LogLevel.Debug, "JPAKE_ROUND_1_VER: Verification successful");
            return ResponseCode.Ok;
        }

        private ResponseCode HandleJPakeRound2Command(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            int expectedCommandParamsCount = -1;
            bool isGenerateCommand = (commandOptions & 0x100) == 0;
            if(isGenerateCommand)
            {
                expectedCommandParamsCount = 3;
            }
            else
            {
                expectedCommandParamsCount = 2;
            }

            if(commandParamsCount != expectedCommandParamsCount)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2: invalid parameter count {0} vs expected {1} (isGenerateCommand={2})", commandParamsCount, expectedCommandParamsCount, isGenerateCommand);
                return ResponseCode.Abort;
            }

            uint keyMetadata = commandParams[0];
            uint passwordSize = 0;
            uint userIdSize = 0;
            uint peerUserIdSize = 0;
            if(isGenerateCommand)
            {
                passwordSize = commandParams[1];
                userIdSize = commandParams[2];
            }
            else
            {
                peerUserIdSize = commandParams[1];
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;

            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}, userIdSize={4} peerUserIdSize={5}",
                       keyIndex, keyType, keyMode, keyRestriction, userIdSize, peerUserIdSize);

            // The JPAKE implementation currently only supports 256-bit prime Weierstraß curves, typically NIST P-256, 
            // and is hardcoded to use SHA256 as the point function.
            if(keyType != KeyType.EccWeirstrass)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2: KeyType={0} not supported", keyType);
                return ResponseCode.InvalidParameter;
            }

            // Consume input key descriptor (should be 0-length)
            uint inputKeyPtr;
            uint inputKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputKeyPtr, out inputKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2: inputKeyPtr=0x{0:X} inputKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       inputKeyPtr, inputKeyLength, transferOptions, nextDescriptorPtr);

            return (isGenerateCommand) ? HandleJPakeRound2GenerateCommand(nextDescriptorPtr, outputDma, userIdSize, passwordSize)
                                       : HandleJPakeRound2VerifyCommand(nextDescriptorPtr, outputDma, peerUserIdSize);
        }

        private ResponseCode HandleJPakeRound2GenerateCommand(uint inputDma, uint outputDma, uint userIdLengthFromCommandOptions, uint passwordLengthFromCommandOptions)
        {
            uint nextDescriptorPtr;
            DmaTransferOptions transferOptions;

            // Password
            uint passwordPtr;
            uint passwordLength;
            UnpackDmaDescriptor(inputDma, out passwordPtr, out passwordLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_GEN: passwordPtr=0x{0:X} passwordLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       passwordPtr, passwordLength, transferOptions, nextDescriptorPtr);
            if(passwordLength != passwordLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: Password length mismatch, expected {0} got {1}", passwordLengthFromCommandOptions, passwordLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] password = new byte[passwordLength];
            FetchFromRam(passwordPtr, password, 0, passwordLength);

            // User ID
            uint userIdPtr;
            uint userIdLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out userIdPtr, out userIdLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_GEN: userIdPtr=0x{0:X} userIdLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       userIdPtr, userIdLength, transferOptions, nextDescriptorPtr);
            if(userIdLength != userIdLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: User ID length mismatch, expected {0} got {1}", userIdLengthFromCommandOptions, userIdLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] userId = new byte[userIdLength];
            FetchFromRam(userIdPtr, userId, 0, userIdLength);

            // S2 random scalar (from round1)
            uint s2RandomScalarPtr;
            uint s2RandomScalarLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out s2RandomScalarPtr, out s2RandomScalarLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_GEN: s2RandomScalarPtr=0x{0:X} s2RandomScalarLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       s2RandomScalarPtr, s2RandomScalarLength, transferOptions, nextDescriptorPtr);
            byte[] s2RandomScalar = new byte[s2RandomScalarLength];
            FetchFromRam(s2RandomScalarPtr, s2RandomScalar, 0, s2RandomScalarLength);

            // Point X1 (from round1)
            uint pointX1Ptr;
            uint pointX1Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX1Ptr, out pointX1Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_GEN: pointX1Ptr=0x{0:X} pointX1Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX1Ptr, pointX1Length, transferOptions, nextDescriptorPtr);
            byte[] pointX1 = new byte[pointX1Length];
            FetchFromRam(pointX1Ptr, pointX1, 0, pointX1Length);

            // Point X3 (from other party round1)
            uint pointX3Ptr;
            uint pointX3Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX3Ptr, out pointX3Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_GEN: pointX3Ptr=0x{0:X} pointX3Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX3Ptr, pointX3Length, transferOptions, nextDescriptorPtr);
            byte[] pointX3 = new byte[pointX3Length];
            FetchFromRam(pointX3Ptr, pointX3, 0, pointX3Length);

            // Point X4 (from other party round1)
            uint pointX4Ptr;
            uint pointX4Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX4Ptr, out pointX4Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_GEN: pointX4Ptr=0x{0:X} pointX4Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX4Ptr, pointX4Length, transferOptions, nextDescriptorPtr);
            byte[] pointX4 = new byte[pointX4Length];
            FetchFromRam(pointX4Ptr, pointX4, 0, pointX4Length);

            // EC-JPAKE Round 2 Generation using NIST P-256 curve and SHA256
            var curve = GetP256CurveParameters();
            var random = new SecureRandom();

            ECPoint x1Point, x3Point, x4Point;
            BigInteger x2Scalar;

            try
            {
                x1Point = DecodeECPointFromBytes(pointX1, curve);
                x3Point = DecodeECPointFromBytes(pointX3, curve);
                x4Point = DecodeECPointFromBytes(pointX4, curve);
                x2Scalar = new BigInteger(1, s2RandomScalar);
            }
            catch(Exception e)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: Failed to decode points or scalars: {0}", e.Message);
                return ResponseCode.InvalidParameter;
            }

            if(x1Point.IsInfinity || x3Point.IsInfinity || x4Point.IsInfinity)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: One or more points are at infinity");
                return ResponseCode.InvalidParameter;
            }

            if(!x1Point.IsValid() || !x3Point.IsValid() || !x4Point.IsValid())
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: One or more points are not valid");
                return ResponseCode.InvalidParameter;
            }

            var passwordScalar = ComputePasswordScalar(password, curve);

            // B = X1 + X3 + X4 (our X1 plus both of the other party's points from round 1)
            var pointB = x1Point.Add(x3Point).Add(x4Point).Normalize();

            // k = (x2 * passwordScalar) mod n 
            var k = x2Scalar.Multiply(passwordScalar).Mod(curve.N);

            // Compute A = k * B (this is the point we send to the other party)
            var pointA = pointB.Multiply(k).Normalize();

            // Generate Schnorr proof for knowledge of k such that A = k * B
            // This proves we know the discrete logarithm of A with respect to base B
            var zkpA = GenerateSchnorrProof(curve, k, pointA, userId, random, "JPAKE-Round2", pointB);

            // Encode the output data
            byte[] aBytes = EncodeECPointToBytes(pointA);
            byte[] zkpABytes = EncodeSchnorrProofToBytes(zkpA);

            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_GEN: Generated A=[{0}]", BitConverter.ToString(aBytes));
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_GEN: Generated ZKP_A=[{0}]", BitConverter.ToString(zkpABytes));

            uint outputDataPtr;
            uint outputDataLength;

            // First output DMA descriptor - point A
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_GEN: OUTPUT1: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);
            if(outputDataLength != aBytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: OUTPUT1 (point A): Data length mismatch, expected {0} got {1}", aBytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(aBytes, 0, outputDataPtr, (uint)aBytes.Length);

            // Second output DMA descriptor - ZKP for A
            UnpackDmaDescriptor(nextDescriptorPtr, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_GEN: OUTPUT2: outputDataPtr=0x{0:X} outputDataLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);
            if(outputDataLength != zkpABytes.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_GEN: OUTPUT2 (ZKP for A): Data length mismatch, expected {0} got {1}", zkpABytes.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(zkpABytes, 0, outputDataPtr, (uint)zkpABytes.Length);

            return ResponseCode.Ok;
        }

#pragma warning disable IDE0060
        private ResponseCode HandleJPakeRound2VerifyCommand(uint inputDma, uint outputDma, uint peerUserIdLengthFromCommandOptions)
#pragma warning restore IDE0060
        {
            uint nextDescriptorPtr;
            DmaTransferOptions transferOptions;

            // Peer User ID
            uint peerUserIdPtr;
            uint peerUserIdLength;
            UnpackDmaDescriptor(inputDma, out peerUserIdPtr, out peerUserIdLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_VER: peerUserIdPtr=0x{0:X} peerUserIdLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       peerUserIdPtr, peerUserIdLength, transferOptions, nextDescriptorPtr);
            if(peerUserIdLength != peerUserIdLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_VER: Peer User ID length mismatch, expected {0} got {1}", peerUserIdLengthFromCommandOptions, peerUserIdLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] peerUserId = new byte[peerUserIdLength];
            FetchFromRam(peerUserIdPtr, peerUserId, 0, peerUserIdLength);

            // Point X1 (from round1)
            uint pointX1Ptr;
            uint pointX1Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX1Ptr, out pointX1Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_VER: pointX1Ptr=0x{0:X} pointX1Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX1Ptr, pointX1Length, transferOptions, nextDescriptorPtr);
            byte[] pointX1 = new byte[pointX1Length];
            FetchFromRam(pointX1Ptr, pointX1, 0, pointX1Length);

            // Point X2 (from round1)
            uint pointX2Ptr;
            uint pointX2Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX2Ptr, out pointX2Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_VER: pointX2Ptr=0x{0:X} pointX2Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX2Ptr, pointX2Length, transferOptions, nextDescriptorPtr);
            byte[] pointX2 = new byte[pointX2Length];
            FetchFromRam(pointX2Ptr, pointX2, 0, pointX2Length);

            // Point X3 (from other party round1)
            uint pointX3Ptr;
            uint pointX3Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX3Ptr, out pointX3Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_VER: pointX3Ptr=0x{0:X} pointX3Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX3Ptr, pointX3Length, transferOptions, nextDescriptorPtr);
            byte[] pointX3 = new byte[pointX3Length];
            FetchFromRam(pointX3Ptr, pointX3, 0, pointX3Length);

            // Point B (from other party round2)
            uint pointBPtr;
            uint pointBLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointBPtr, out pointBLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_VER: pointBPtr=0x{0:X} pointBLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointBPtr, pointBLength, transferOptions, nextDescriptorPtr);
            byte[] pointB = new byte[pointBLength];
            FetchFromRam(pointBPtr, pointB, 0, pointBLength);

            // Zero knowledge proof for B (from other party round 2)
            uint zkpBPtr;
            uint zkpBLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out zkpBPtr, out zkpBLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_VER: zkpBPtr=0x{0:X} zkpBLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       zkpBPtr, zkpBLength, transferOptions, nextDescriptorPtr);
            byte[] zkpB = new byte[zkpBLength];
            FetchFromRam(zkpBPtr, zkpB, 0, zkpBLength);

            // EC-JPAKE Round 2 Verification using NIST P-256 curve and SHA256
            var curve = GetP256CurveParameters();

            ECPoint x1Point, x2Point, x3Point, bPoint;
            SchnorrProof zkpBProof;

            try
            {
                x1Point = DecodeECPointFromBytes(pointX1, curve);
                x2Point = DecodeECPointFromBytes(pointX2, curve);
                x3Point = DecodeECPointFromBytes(pointX3, curve);
                bPoint = DecodeECPointFromBytes(pointB, curve);
                zkpBProof = DecodeSchnorrProofFromBytes(zkpB, curve);
            }
            catch(Exception e)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_VER: Exception during verification: {0}", e.Message);
                return ResponseCode.InvalidParameter;
            }

            if(x1Point.IsInfinity || x2Point.IsInfinity || x3Point.IsInfinity || bPoint.IsInfinity || zkpBProof.V.IsInfinity)
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_VER: One or more points are at infinity");
                return ResponseCode.InvalidParameter;
            }

            if(!x1Point.IsValid() || !x2Point.IsValid() || !x3Point.IsValid() || !bPoint.IsValid() || !zkpBProof.V.IsValid())
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_VER: One or more points are not valid");
                return ResponseCode.InvalidParameter;
            }

            // Rebuild the prover's base: B_base = X1 + X2 + X3
            var bBase = x1Point.Add(x2Point).Add(x3Point).Normalize();

            // Reject if B_base is not on-curve or is infinity
            if(bBase.IsInfinity || !bBase.IsValid())
            {
                parent.Log(LogLevel.Error, "JPAKE_ROUND_2_VER: Computed B_base is invalid or at infinity");
                return ResponseCode.InvalidParameter;
            }

            // Verify the Schnorr proof: ZKP(B) with base point B_base
            // Using "JPAKE-Round2" as the standard label for Round 2 proofs
            bool zkpBValid = VerifySchnorrProof(curve, bPoint, zkpBProof, peerUserId, "JPAKE-Round2", bBase);

            if(!zkpBValid)
            {
                parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_VER: ZKP(B) verification failed");
                return ResponseCode.InvalidParameter;
            }

            parent.Log(LogLevel.Debug, "JPAKE_ROUND_2_VER: Verification successful");
            return ResponseCode.Ok;
        }

        private ResponseCode HandleJPakeGenerateSessionKeyCommand(uint inputDma, uint outputDma, uint[] commandParams, uint commandParamsCount, uint commandOptions)
        {
            if(commandParamsCount != 3)
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: invalid parameter count");
                return ResponseCode.Abort;
            }

            var hashMode = (ShaMode)((commandOptions & 0xF00) >> 8);
            uint keyMetadata = commandParams[0];
            uint passwordLengthFromCommandOptions = commandParams[1];
            uint outKeyMetadata = commandParams[2];

            uint nextDescriptorPtr = inputDma;
            DmaTransferOptions transferOptions;
            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(keyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "JPAKE_GEN_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}", keyIndex, keyType, keyMode, keyRestriction);

            // The JPAKE implementation currently only supports 256-bit prime Weierstraß curves, typically NIST P-256, 
            // and is hardcoded to use SHA256 as the point function.
            if(keyType != KeyType.EccWeirstrass)
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: KeyType={0} not supported", keyType);
                return ResponseCode.InvalidParameter;
            }

            UnpackKeyMetadata(outKeyMetadata, out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "JPAKE_GEN_KEY: OUT_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}", keyIndex, keyType, keyMode, keyRestriction);
            //EccWeirstrassKeyMetadata wprKeyMetadata = UnpackWeirstrassPrimeFieldKeyMetadata(outKeyMetadata);

            // Output keyMode expected to be unprotected
            if(keyMode != KeyMode.Unprotected)
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Output KeyMode={0} not supported", keyMode);
                return ResponseCode.InvalidParameter;
            }

            // Consume input key descriptor (should be 0-length)
            uint inputKeyPtr;
            uint inputKeyLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out inputKeyPtr, out inputKeyLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_GEN_KEY: inputKeyPtr=0x{0:X} inputKeyLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       inputKeyPtr, inputKeyLength, transferOptions, nextDescriptorPtr);

            // Password
            uint passwordPtr;
            uint passwordLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out passwordPtr, out passwordLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_GEN_KEY: passwordPtr=0x{0:X} passwordLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       passwordPtr, passwordLength, transferOptions, nextDescriptorPtr);
            if(passwordLength != passwordLengthFromCommandOptions)
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Password length mismatch, expected {0} got {1}", passwordLengthFromCommandOptions, passwordLength);
                return ResponseCode.InvalidParameter;
            }
            byte[] password = new byte[passwordLength];
            FetchFromRam(passwordPtr, password, 0, passwordLength);

            // x2 scalar (our private scalar from round 1)
            uint x2ScalarPtr;
            uint x2ScalarLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out x2ScalarPtr, out x2ScalarLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_GEN_KEY: x2ScalarPtr=0x{0:X} x2ScalarLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       x2ScalarPtr, x2ScalarLength, transferOptions, nextDescriptorPtr);
            byte[] x2ScalarBytes = new byte[x2ScalarLength];
            FetchFromRam(x2ScalarPtr, x2ScalarBytes, 0, x2ScalarLength);

            // Point X4 (from other party round1)
            uint pointX4Ptr;
            uint pointX4Length;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointX4Ptr, out pointX4Length, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_GEN_KEY: pointX4Ptr=0x{0:X} pointX4Length={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointX4Ptr, pointX4Length, transferOptions, nextDescriptorPtr);
            byte[] pointX4 = new byte[pointX4Length];
            FetchFromRam(pointX4Ptr, pointX4, 0, pointX4Length);

            // Point B (from other party round2)
            uint pointBPtr;
            uint pointBLength;
            UnpackDmaDescriptor(nextDescriptorPtr, out pointBPtr, out pointBLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_GEN_KEY: pointBPtr=0x{0:X} pointBLength={1} options={2} nextDescriptorPtr=0x{3:X}",
                       pointBPtr, pointBLength, transferOptions, nextDescriptorPtr);
            byte[] pointB = new byte[pointBLength];
            FetchFromRam(pointBPtr, pointB, 0, pointBLength);

            // EC-JPAKE Session Key Generation using NIST P-256 curve and SHA256
            // Implementation follows the provided pseudo-code
            var curve = GetP256CurveParameters();

            ECPoint x4Point, bPoint;
            BigInteger x2Scalar; // S = our Round-1 private scalar (32 bytes)

            try
            {
                x4Point = DecodeECPointFromBytes(pointX4, curve);
                bPoint = DecodeECPointFromBytes(pointB, curve);
                x2Scalar = new BigInteger(1, x2ScalarBytes); // S = our private scalar from Round 1

                parent.Log(LogLevel.Debug, "JPAKE_GEN_KEY: Input X4 point=[{0}]", BitConverter.ToString(pointX4));
                parent.Log(LogLevel.Debug, "JPAKE_GEN_KEY: Input B point=[{0}]", BitConverter.ToString(pointB));
                parent.Log(LogLevel.Debug, "JPAKE_GEN_KEY: Input S scalar=[{0}]", BitConverter.ToString(x2ScalarBytes));
            }
            catch(Exception e)
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Exception during session key generation: {0}", e.Message);
                return ResponseCode.InvalidParameter;
            }

            if(x4Point.IsInfinity || bPoint.IsInfinity)
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: One or more points are at infinity");
                return ResponseCode.InvalidParameter;
            }

            if(!x4Point.IsValid() || !bPoint.IsValid())
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: One or more points are not valid");
                return ResponseCode.InvalidParameter;
            }

            var passwordScalar = ComputePasswordScalar(password, curve);

            // RFC 8236 correct formula: K = (B - (X4 * (S * s))) * S
            // First compute the combined scalar: S * s
            var combinedScalar = x2Scalar.Multiply(passwordScalar).Mod(curve.N);

            if(combinedScalar.Equals(BigInteger.Zero))
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Combined scalar (S*s) is zero - aborting");
                return ResponseCode.InvalidParameter;
            }

            // Compute X4 * (S * s)
            var x4ScaledPoint = x4Point.Multiply(combinedScalar).Normalize();

            // Compute B - (X4 * (S * s))
            var intermediatePoint = bPoint.Subtract(x4ScaledPoint).Normalize();

            if(!intermediatePoint.IsValid() || intermediatePoint.IsInfinity)
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Intermediate point (B - (X4*(S*s))) is invalid or at infinity - aborting");
                return ResponseCode.InvalidParameter;
            }

            // Final step: K = (B - (X4 * (S * s))) * S
            var sharedSecretPointK = intermediatePoint.Multiply(x2Scalar).Normalize();

            // Check if shared secret point K is valid and not infinity
            if(sharedSecretPointK.IsInfinity || !sharedSecretPointK.IsValid())
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Shared secret point K is invalid or at infinity");
                return ResponseCode.InvalidParameter;
            }

            // Derive session key from K
            var kXCoordinate = BigIntegerToFixedByteArray(sharedSecretPointK.AffineXCoord.ToBigInteger(), 32);

            var sha256 = new Sha256Digest();
            sha256.BlockUpdate(kXCoordinate, 0, kXCoordinate.Length);
            var sessionKey = new byte[sha256.GetDigestSize()];
            sha256.DoFinal(sessionKey, 0);

            parent.Log(LogLevel.Debug, "JPAKE_GEN_KEY: Generated session key=[{0}]", BitConverter.ToString(sessionKey));

            // Output the session key based on the output key mode
            uint outputDataPtr;
            uint outputDataLength;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "JPAKE_GEN_KEY: outputDataPtr=0x{0:X} outputDataLength={1} options={2}",
                        outputDataPtr, outputDataLength, transferOptions);

            // Output key mode is always "unprotected"
            if(outputDataLength != sessionKey.Length)
            {
                parent.Log(LogLevel.Error, "JPAKE_GEN_KEY: Output data length mismatch, expected {0} got {1}", sessionKey.Length, outputDataLength);
                return ResponseCode.InvalidParameter;
            }

            WriteToRam(sessionKey, 0, outputDataPtr, (uint)sessionKey.Length);

            parent.Log(LogLevel.Info, "JPAKE_GEN_KEY: Session key generation successful");
            return ResponseCode.Ok;
        }

#pragma warning disable IDE0060
        private ResponseCode HandleSetVectorTableCommand(uint inputDmaDescriptorPtr, uint outputDmaDescriptorPtr, uint[] commandParams, uint commandParamsCount, uint commandOptions)
#pragma warning restore IDE0060
        {
            if(commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "SET_VTOR: invalid parameter count");
                return ResponseCode.Abort;
            }

            var cortexMCpus = machine.GetPeripheralsOfType<CortexM>();

            foreach(var cpu in cortexMCpus)
            {
                cpu.VectorTableOffset = commandParams[0];
            }

            return ResponseCode.Ok;
        }

        private ResponseCode HandleDeleteKeyCommand(uint[] commandParams, uint commandParamsCount)
        {
            // SDK (sl_se_delete_key) still emits sli_add_key_metadata as the input DMA chain;
            // the model only needs the keyspec in commandParams[0].
            if(commandParamsCount != 1)
            {
                parent.Log(LogLevel.Error, "DELETE_KEY: invalid parameter count");
                return ResponseCode.Abort;
            }

            KeyMode keyMode;
            KeyType keyType;
            KeyRestriction keyRestriction;
            uint keyIndex;
            UnpackKeyMetadata(commandParams[0], out keyIndex, out keyType, out keyMode, out keyRestriction);
            parent.Log(LogLevel.Debug, "DELETE_KEY: keyIndex={0} keyType={1} keyMode={2} keyRestriction={3}",
                       keyIndex, keyType, keyMode, keyRestriction);

            switch(keyMode)
            {
            case KeyMode.Volatile:
                if(!volatileKeys.Remove(keyIndex))
                {
                    parent.Log(LogLevel.Error, "DELETE_KEY: volatile key index {0} not found", keyIndex);
                    return ResponseCode.InvalidParameter;
                }
                return ResponseCode.Ok;
            case KeyMode.Ksu:
                if(ksuStorage == null || !ksuStorage.ContainsKey(keyIndex))
                {
                    parent.Log(LogLevel.Error, "DELETE_KEY: KSU key index {0} not found", keyIndex);
                    return ResponseCode.InvalidParameter;
                }
                ksuStorage.RemoveKey(keyIndex);
                return ResponseCode.Ok;
            default:
                parent.Log(LogLevel.Error, "DELETE_KEY: invalid key mode {0}", keyMode);
                return ResponseCode.InvalidParameter;
            }
        }

        private void FillArray(byte[] arr, uint offset, uint length, byte value)
        {
            for(uint i = offset; i < offset + length && i < arr.Length; i++)
            {
                arr[i] = value;
            }
        }

        private ResponseCode WriteSignatureToOutput(uint outputDma, byte[] signature, string logTag)
        {
            uint outputDataPtr;
            uint outputDataLength;
            DmaTransferOptions transferOptions;
            uint nextDescriptorPtr;
            UnpackDmaDescriptor(outputDma, out outputDataPtr, out outputDataLength, out transferOptions, out nextDescriptorPtr, machine);
            parent.Log(LogLevel.Debug, "{0}: outputDataPtr=0x{1:X} outputDataLength={2} options={3} nextDescriptorPtr=0x{4:X}",
                       logTag, outputDataPtr, outputDataLength, transferOptions, nextDescriptorPtr);
            if(outputDataLength != signature.Length)
            {
                parent.Log(LogLevel.Error, "{0}: signature output descriptor length {1} != expected {2}", logTag, outputDataLength, signature.Length);
                return ResponseCode.InvalidParameter;
            }
            WriteToRam(signature, 0, outputDataPtr, (uint)signature.Length);
            parent.Log(LogLevel.Debug, "{0}: Signature=[{1}]", logTag, BitConverter.ToString(signature));
            return ResponseCode.Ok;
        }

        // Unified helper for retrieving a key from any of the four supported storage modes.
        // Callers are responsible for validating keyType against the operation-specific
        // set of accepted families before invoking this helper. Returns null on any error.
        private byte[] ReadKey(KeyMode keyMode, uint keyIndex, uint keyPtr, uint keyLength)
        {
            switch(keyMode)
            {
            case KeyMode.Unprotected:
            {
                byte[] key = new byte[keyLength];
                if(keyLength > 0)
                {
                    FetchFromRam(keyPtr, key, 0, keyLength);
                }
                return key;
            }
            case KeyMode.Wrapped:
            {
                // Wrapped DMA is structured as follows:
                //   - KeyRandomIvLength bytes:        random IV assigned at time of wrapping
                //   - (keyLength - IV - tag) bytes:   encrypted key data
                //   - KeyAuthenticationTagLength:     AES-GCM authentication tag
                if(keyLength <= (KeyRandomIvLength + KeyAuthenticationTagLength))
                {
                    parent.Log(LogLevel.Error, "ReadKey: invalid wrapped key descriptor length {0}", keyLength);
                    return null;
                }
                byte[] key = new byte[keyLength - KeyRandomIvLength - KeyAuthenticationTagLength];
                FetchFromRam(keyPtr + KeyRandomIvLength, key, 0, (uint)key.Length);
                return key;
            }
            case KeyMode.Volatile:
            {
                if(!volatileKeys.ContainsKey(keyIndex))
                {
                    parent.Log(LogLevel.Error, "ReadKey: volatile key index {0} not found", keyIndex);
                    return null;
                }
                byte[] key = volatileKeys[keyIndex];
                parent.Log(LogLevel.Noisy, "ReadKey (volatile): keyIndex={0} key=[{1}]", keyIndex, BitConverter.ToString(key));
                return key;
            }
            case KeyMode.Ksu:
            {
                if(ksuStorage == null || !ksuStorage.ContainsKey(keyIndex))
                {
                    parent.Log(LogLevel.Error, "ReadKey: KSU key index {0} not found", keyIndex);
                    return null;
                }
                byte[] key = ksuStorage.GetKey(keyIndex);
                parent.Log(LogLevel.Noisy, "ReadKey (KSU): keyIndex={0} key=[{1}]", keyIndex, BitConverter.ToString(key));
                return key;
            }
            default:
                parent.Log(LogLevel.Error, "ReadKey: unsupported keyMode {0}", keyMode);
                return null;
            }
        }

        private ResponseCode WriteWrappedKeyToDescriptor(uint descriptorDataPtr, uint descriptorDataLength, byte[] key)
        {
            // Output DMA is expected to be structured as follows:
            // - 12 bytes: random IV assigned at time of wrapping
            // - var bytes: encrypted key data
            // - 16 bytes: AESGCM authentication tag
            if(descriptorDataLength != (KeyRandomIvLength + key.Length + KeyAuthenticationTagLength))
            {
                parent.Log(LogLevel.Error, "WriteWrappedKeyToDescriptor: descriptor length mismatch {0} - {1}", descriptorDataLength, (KeyRandomIvLength + key.Length + KeyAuthenticationTagLength));
                return ResponseCode.InvalidParameter;
            }

            // TODO: for now we simply copy the plaintext key to the output DMA. 
            // Random IV and AESGCM tags are assigned to special markers for debugging purposes.
            uint offset = 0;

            // Random IV (fake random IV, filled with specific marker)
            byte[] randomIV = new byte[KeyRandomIvLength];
            FillArray(randomIV, 0, KeyRandomIvLength, WrappedKeyRandomIVMarker);
            WriteToRam(randomIV, 0, descriptorDataPtr + offset, KeyRandomIvLength);
            offset += KeyRandomIvLength;
            // "encrypted" key data
            WriteToRam(key, 0, descriptorDataPtr + offset, (uint)key.Length);
            offset += (uint)key.Length;
            // AESGCM authentication tag (fake tag, filled with specific marker)
            byte[] authTag = new byte[KeyAuthenticationTagLength];
            FillArray(authTag, 0, KeyAuthenticationTagLength, WrappedKeyAuthenticationTagMarker);
            WriteToRam(authTag, 0, descriptorDataPtr + offset, KeyAuthenticationTagLength);

            return ResponseCode.Ok;
        }

        private ECDomainParameters GetECDomainParametersFromKeyLength(uint length)
        {
            DerObjectIdentifier oid;

            switch(length)
            {
            case 192:
                oid = X9ObjectIdentifiers.Prime192v1;
                break;
            case 224:
                oid = SecObjectIdentifiers.SecP224r1;
                break;
            case 239:
                oid = X9ObjectIdentifiers.Prime239v1;
                break;
            case 256:
                oid = X9ObjectIdentifiers.Prime256v1;
                break;
            case 384:
                oid = SecObjectIdentifiers.SecP384r1;
                break;
            case 521:
            case 528: // P-521 with 66-byte representation (66 * 8 = 528).
                oid = SecObjectIdentifiers.SecP521r1;
                break;
            default:
                throw new InvalidParameterException("unknown key size.");
            }

            var ecps = ECNamedCurveTable.GetByOid(oid);

            return new ECDomainParameters(ecps.Curve, ecps.G, ecps.N, ecps.H, ecps.GetSeed());
        }

        private AsymmetricCipherKeyPair LookUpKeyPair(byte[] privateKey)
        {
            // Find the corresponding key pair based on the private key
            foreach(var keyPair in ecKeyPairs)
            {
                var privKey = (ECPrivateKeyParameters)keyPair.Private;

                // Pad stored private key to match input length for comparison
                byte[] storedPrivateKeyBytes = BigIntegerToFixedByteArray(privKey.D, privateKey.Length);

                if(storedPrivateKeyBytes.SequenceEqual(privateKey))
                {
                    return keyPair;
                }
            }

            return null;
        }

        // from sl_se_manager_key_handling.c
        // Asymmetric key attributes:
        // KEYSPEC_ATTRIBUTES_ECC_PRIVATE_MASK (1U << 14)
        // KEYSPEC_ATTRIBUTES_ECC_PUBLIC_MASK  (1U << 13)
        // KEYSPEC_ATTRIBUTES_ECC_DOMAIN       (1U << 12)
        // KEYSPEC_ATTRIBUTES_ECC_SIGN         (1U << 10)
        // KEYSPEC_ATTRIBUTES_ECC_SIZE_MASK    0x0000007fU
        private EccWeirstrassKeyMetadata UnpackWeirstrassPrimeFieldKeyMetadata(uint keyMetadata)
        {
            var result = new EccWeirstrassKeyMetadata();

            // [6:0]: size of key in bytes minus 1 (0 = 8 bits, 127 = 1024 bits)
            result.Size = (keyMetadata & 0x7F) + 1;
            // [10]: true for signing false for key exchange
            result.Purpose = (keyMetadata & 0x0400) != 0 ? EccWeirstrassPurpose.Signature : EccWeirstrassPurpose.KeyExchange;
            // [12]: true if domain parameters are included
            result.HasDomainParameters = (keyMetadata & 0x1000) != 0;
            // [13]: true if public key included
            result.HasPublicKey = (keyMetadata & 0x2000) != 0;
            // [14]: true if private key included
            result.HasPrivateKey = (keyMetadata & 0x4000) != 0;

            // use params if domain else use selCurve
            if(result.HasDomainParameters)
            {
                // [5]: true if a = 0. Ignored if domain=0
                result.A0 = (keyMetadata & 0x0020) != 0;
                // [6]: true if a = -3. Ignored if domain=0
                result.An3 = (keyMetadata & 0x0040) != 0;
                // [7]: reserved
            }
            else
            {
                // [7:5]: Curve selected when domain = 0.
                // Only 0x0 is available (standard Weierstrass curve of size EccWPrMetadata.size).
                result.SelectCurve = (EccWeirstrassCurve)((keyMetadata >> 5) & 0x07);
                if(result.SelectCurve != EccWeirstrassCurve.CurveP192)
                {
                    throw new NotSupportedException("Only ECC Curve P-192 is supported ");
                }
            }

            parent.Log(LogLevel.Debug, "UnpackWeirstrassPrimeFieldKeyMetadata: priKey={0} pubKey={1} domain={2} purpose={3} size={4}",
                    result.HasPrivateKey, result.HasPublicKey, result.HasDomainParameters, result.Purpose, result.Size);

            return result;
        }

        private void UnpackKeyMetadata(uint keyMetadata, out uint index, out KeyType type, out KeyMode mode, out KeyRestriction restriction)
        {
            // [31:28]
            type = (KeyType)((keyMetadata >> 28) & 0xF);
            // [27:26]
            mode = (KeyMode)((keyMetadata >> 26) & 0x3);

            if(mode == KeyMode.Ksu)
            {
                // KSU metadata uses bits [25:24] for the KSU instance and
                // bits [23:18] for the key slot. It does not encode a key
                // restriction in this field.
                restriction = KeyRestriction.Unlocked;
                index = ((keyMetadata >> 18) & 0x3F);
            }
            else
            {
                // [25:24]
                restriction = (KeyRestriction)((keyMetadata >> 24) & 0x3);
                // [23:16]
                index = ((keyMetadata >> 16) & 0xFF);
            }

            // [15]
            // noProtection = (keyMetadata & 0x8000) != 0;

            parent.Log(LogLevel.Noisy, "UnpackKeyMetadata(): keyMetadata=0x{0:X} index={1} type={2} mode={3} restriction={4}",
                     keyMetadata, index, type, mode, restriction);
        }

        private void UnpackDmaDescriptor(uint dmaPointer, out uint dataPointer, out uint dataSize, out DmaTransferOptions options, out uint nextDescriptorPtr, Machine machine)
        {
            if(dmaPointer == NullDescriptor)
            {
                throw new NotSupportedException("UnpackDmaDescriptor trying to read a Null descriptor");
            }

            dataPointer = (uint)machine.SystemBus.ReadDoubleWord(dmaPointer);
            nextDescriptorPtr = (uint)machine.SystemBus.ReadDoubleWord(dmaPointer + 4);
            var word2 = (uint)machine.SystemBus.ReadDoubleWord(dmaPointer + 8);
            dataSize = word2 & 0xFFFFFFF;
            options = (DmaTransferOptions)(word2 >> 28);

            parent.Log(LogLevel.Noisy, "UnpackDmaDescriptor(): dataPointer=0x{0:X} dataSize={1} options={2} nextDescriptorPtr=0x{3:X}",
                     dataPointer, dataSize, options, nextDescriptorPtr);

            if(dataSize > 0)
            {
                byte[] data = new byte[dataSize];
                FetchFromRam(dataPointer, data, 0, dataSize);
                parent.Log(LogLevel.Noisy, "UnpackDmaDescriptor(): DATA=[{0}]", BitConverter.ToString(data));
            }
        }

        private void FetchFromRam(uint sourcePointer, byte[] destination, uint destinationOffset, uint length)
        {
            for(uint i = 0; i < length; i++)
            {
                destination[destinationOffset + i] = (byte)machine.SystemBus.ReadByte(sourcePointer + i);
            }
        }

        private void WriteToRam(byte[] source, uint sourceOffset, uint destinationPointer, uint length)
        {
            for(uint i = 0; i < length; i++)
            {
                machine.SystemBus.WriteByte(destinationPointer + i, source[sourceOffset + i]);
            }
        }

        private void UnpackRawKeyMetadata(uint keyMetadata, out uint keyLength)
        {
            // [14:0]
            keyLength = (keyMetadata & 0x7FFF);
        }

        /// Converts a BigInteger to a fixed-length byte array with proper zero-padding.
        /// This fixes the issue where ToByteArrayUnsigned() removes leading zeros.
        private byte[] BigIntegerToFixedByteArray(BigInteger bigInteger, int targetLength)
        {
            byte[] rawBytes = bigInteger.ToByteArrayUnsigned();
            byte[] paddedBytes = new byte[targetLength];

            // Copy raw bytes to the right side of the padded array (zero-padded on left)
            Array.Copy(rawBytes, 0, paddedBytes, targetLength - rawBytes.Length, rawBytes.Length);

            return paddedBytes;
        }

        private IDigest CreateHashEngine(ShaMode hashMode)
        {
            IDigest ret = null;

            switch(hashMode)
            {
            case ShaMode.Sha1:
                ret = new Sha1Digest();
                break;
            case ShaMode.Sha224:
                ret = new Sha224Digest();
                break;
            case ShaMode.Sha256:
                ret = new Sha256Digest();
                break;
            case ShaMode.Sha384:
                ret = new Sha384Digest();
                break;
            case ShaMode.Sha512:
                ret = new Sha512Digest();
                break;
            default:
                parent.Log(LogLevel.Error, "CreateHashEngine(): invalid hash mode");
                break;
            }
            return ret;
        }

        private uint HashEngineStateSize(ShaMode hashMode)
        {
            switch(hashMode)
            {
            case ShaMode.Sha1:
                return 20;
            case ShaMode.Sha224:
            case ShaMode.Sha256:
                return 32;
            case ShaMode.Sha384:
            case ShaMode.Sha512:
                return 64;
            }
            return 0;
        }

        private uint GetInitialDataSpaceLength
        {
            get
            {
                return flashSize - flashDataRegionStart;
            }
        }

        private uint userDataRemainingWrites;
        private bool userDataInitialized;

        private uint wordsLeftToBeReceived;
        private readonly Dictionary<uint, byte[]> volatileKeys = new Dictionary<uint, byte[]>();
        private readonly bool series3;
        private readonly uint flashDataRegionStart;
        private readonly uint flashCodeRegionEnd;
        private readonly uint flashCodeRegionStart;
        private readonly uint flashRegionSize;
        private readonly uint flashPageSize;
        private readonly uint flashSize;
        private readonly Queue<uint> rxFifo;
        private readonly Queue<uint> txFifo;
        private readonly ISiLabs_KeyStorage ksuStorage;
        private readonly Machine machine;
        private readonly IDoubleWordPeripheral parent;
        private readonly List<AsymmetricCipherKeyPair> ecKeyPairs = new List<AsymmetricCipherKeyPair>();
        private readonly byte[] userDataToken = new byte[UserDataTokenSize];

        // BouncyCastle's Sha{1,224,256,384,512}Digest classes hold H1..Hn as private (or internal-to-BC)
        // fields with no public accessor. The firmware exchanges raw H bytes with the host on every
        // HashUpdate/HashFinish, so we read/write those fields by reflection. Field names are stable
        // across BC's vendored history (see lib/bc-csharp/crypto/src/crypto/digests/).
        private const BindingFlags PrivateInstanceField = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        private const uint NullDescriptor = 1;
        // Series3 specific value.
        // Related to PSEC-5391, once that gets resolved, we might need to update this accordingly.
        private const uint QspiFlashHostBase = 0x1000000;
        private const byte WrappedKeyRandomIVMarker = 0xAE;
        private const byte WrappedKeyAuthenticationTagMarker = 0xBE;
        private const uint KeyRandomIvLength = 12;
        private const uint KeyAuthenticationTagLength = 16;
        private const uint UserDataTokenSize = 0xFC;
        private const uint UserDataWriteLimit = 100;
        private const int CcmBlockSize = 16;
        private const int CcmContextSize = 2 * CcmBlockSize;

        private class EccWeirstrassKeyMetadata
        {
            public bool HasPrivateKey { get; set; }

            public bool HasPublicKey { get; set; }

            public bool HasDomainParameters { get; set; }

            public EccWeirstrassPurpose Purpose { get; set; }

            public bool A0 { get; set; }

            public bool An3 { get; set; }

            public EccWeirstrassCurve SelectCurve { get; set; }

            public uint Size { get; set; }
        }

        // Data structure for Schnorr proof
        private class SchnorrProof
        {
            public ECPoint V { get; set; }  // Commitment point

            public BigInteger R { get; set; }  // Response scalar
        }

        private enum ResponseCode
        {
            Ok                 = 0x00000000,
            InvalidCommand     = 0x00010000,
            AuthorizationError = 0x00020000,
            InvalidSignature   = 0x00030000,
            BusError           = 0x00040000,
            InternalError      = 0x00050000,
            CryptoError        = 0x00060000,
            InvalidParameter   = 0x00070000,
            SecureBootError    = 0x00090000,
            SelfTestError      = 0x000A0000,
            NotInitialized     = 0x000B0000,
            MailboxInvalid     = 0x00FE0000,
            Abort              = 0x00FF0000,
        }

        private enum CommandId
        {
            ImportKey                           = 0x0100,
            ExportKey                           = 0x0102,
            DeleteKey                           = 0x0105,
            TransferKey                         = 0x0106,
            InstallKey                          = 0x0107,
            CreateKey                           = 0x0200,
            ReadPublicKey                       = 0x0201,
            DeriveKey                           = 0x0202,
            Hash                                = 0x0300,
            HashUpdate                          = 0x0301,
            HashHmac                            = 0x0302,
            HashFinish                          = 0x0303,
            AesEncrypt                          = 0x0400,
            AesDecrypt                          = 0x0401,
            AesGcmEncrypt                       = 0x0402,
            AesGcmDecrypt                       = 0x0403,
            AesCmac                             = 0x0404,
            AesCcmEncrypt                       = 0x0405,
            AesCcmDecrypt                       = 0x0406,
            SignatureSign                       = 0x0600,
            SignatureVerify                     = 0x0601,
            EddsaSign                           = 0x0602,
            EddsaVerify                         = 0x0603,
            Random                              = 0x0700,
            JPakeRound1                         = 0x0B00,
            JPakeRound2                         = 0x0B01,
            JPakeGenerateSessionKey             = 0x0B02,
            DiffieHellman                       = 0x0E00,
            ReadDeviceData                      = 0x4330,
            SetVectorTable                      = 0x4351,
            UserDataStorage                     = 0x43FD,
            ConfigureQspiRefClock               = 0xFF15,
            ConfigureSpiMemoryDeepPowerDownMode = 0xFF43,
            FlashGetCodeRegionConfig            = 0xFF53,
            FlashEraseDataRegion                = 0xFF62,
            FlashWriteDataRegion                = 0xFF63,
            FlashGetDataRegionLocation          = 0xFF64,
        }

        private enum ShaMode
        {
            Sha1    = 0x2,
            Sha224  = 0x3,
            Sha256  = 0x4,
            Sha384  = 0x5,
            Sha512  = 0x6,
        }

        private enum CryptoMode
        {
            Ecb = 0x1,
            Cbc = 0x2,
            Ctr = 0x3,
            Cfb = 0x4,
            Ofb = 0x5,
        }

        private enum ContextMode
        {
            WholeMessage    = 0x0,
            StartOfMessage  = 0x1,
            EndOfMessage    = 0x2,
            MiddleOfMessage = 0x3,
        }

        private enum DmaTransferOptions
        {
            Register       = 0x1,
            MemoryRealign  = 0x2,
            Discard        = 0x4,
        }

        private enum KeyRestriction
        {
            Unlocked   = 0x0,
            Locked     = 0x1,
            Internal   = 0x2,
            Restricted = 0x3,
        }

        private enum KeyMode
        {
            Unprotected        = 0x0,
            Volatile           = 0x1,
            Wrapped            = 0x2,
            Ksu                = 0x3,
        }

        private enum KeyType
        {
            Raw            = 0x0,
            EccWeirstrass  = 0x8,
            EccEdwards     = 0xA,
            EccMontgomery  = 0xB,
            EccEddsa       = 0xC,
        }

        private enum EccWeirstrassCurve
        {
            CurveP192 = 0x0,
            CurveP256 = 0x1,
            CurveP384 = 0x2,
            CurveP521 = 0x3,
        }

        private enum EccWeirstrassKeySize
        {
            KeySizeCurveP192 = 24,
            KeySizeCurveP256 = 32,
            KeySizeCurveP384 = 48,
            KeySizeCurveP521 = 66,
        }

        private enum EccWeirstrassPurpose
        {
            KeyExchange = 0,
            Signature = 1,
        }

        private enum ReadDeviceDataLocationType
        {
            CC         = 0x0,
            DI         = 0x1,
            WaferProbe = 0x2,
        }

        private enum DeviceDataReadSize
        {
            WholeElement   = 0x00,
            ChunkOfElement = 0x01,
            OneWord        = 0x02,
            GetSize        = 0x03,
            GetValue       = 0x04,
        }

        // Low byte ("Option 1") of the User Data Storage command options word.
        private enum UserDataAction
        {
            Get             = 0x00,
            Set             = 0x01,
            ForceSet        = 0x02,
            RemainingWrites = 0x03,
        }
    }
}
