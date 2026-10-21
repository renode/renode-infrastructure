//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Utilities;

namespace Antmicro.Renode.Peripherals.Crypto;

// This is an implementation of: https://chipsalliance.github.io/caliptra-web/docs/2.1/hardware/hardware_spec.html#ecc
public class Caliptra_ECC : BasicDoubleWordPeripheral, IKnownSize
{
    public Caliptra_ECC(IMachine machine, Caliptra_KeyVault keyVault) : base(machine)
    {
        this.keyVault = keyVault;

        DefineRegisters();
        Reset();
    }

    public override void Reset()
    {
        base.Reset();

        eccReady.Value = true;

        Array.Clear(privKeyOut);
        Array.Clear(pubKeyX);
        Array.Clear(pubKeyY);
        Array.Clear(verifyR);
        Array.Clear(signR);
        Array.Clear(signS);
        Array.Clear(kvSeed);

        seedFromKeyVault = false;
    }

    public long Size => 0xA08;

    private static byte[] ToBigEndianBytes(IValueRegisterField[] reg)
    {
        var values = reg.Select(e => (uint)e.Value).ToArray();
        return ToBigEndianBytes(values);
    }

    private static byte[] ToBigEndianBytes(uint[] array)
    {
        var result = new byte[array.Length * 4];
        int index = 0;
        foreach(var val in array)
        {
            var bytes = BitConverter.GetBytes(val);
            if(BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Buffer.BlockCopy(bytes, 0, result, index, 4);
            index += 4;
        }
        return result;
    }

    private static void ToUintAndCopy(byte[] src, uint[] dst)
    {
        Array.Clear(dst, 0, dst.Length);
        uint[] tmp = BitHelper.ToUInt32Array(src, littleEndian: false);

        if(tmp.Length > dst.Length)
        {
            throw new ArgumentException("Source data is too large for the destination buffer", nameof(src));
        }

        Array.Copy(tmp, dst, tmp.Length);
    }

    private void DefineRegisters()
    {
        Registers.Name.DefineMany(this, 2, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ => (uint)(ECC_CORE_NAME >> (idx * 32)),
                name: $"NAME_{idx}");
        });

        Registers.Version.DefineMany(this, 2, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ => (uint)(ECC_CORE_VERSION >> (idx * 32)),
                name: $"VERSION_{idx}");
        });

        Registers.Control.Define(this)
            .WithEnumField<DoubleWordRegister, EccCommand>(0, 2, FieldMode.Write, writeCallback: (_, val) => HandleCommand(val), name: "CTRL")
            .WithFlag(2, FieldMode.Write,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        EccZeroize();
                    }
                },
                name: "ZEROIZE")
            .WithTaggedFlag("PCR_SIGN", 3)
            .WithFlag(4, FieldMode.Write,
                writeCallback: (_, val) =>
                {
                    if(val && eccReady.Value)
                    {
                        ExecuteECDH();
                    }
                },
                name: "DH_SHAREDKEY")
            .WithReservedBits(5, 27);

        Registers.Status.Define(this)
            .WithFlag(0, out eccReady, FieldMode.Read, name: "READY")
            .WithFlag(1, out eccValid, FieldMode.Read, name: "VALID")
            .WithReservedBits(2, 30);

        Registers.Seed.DefineMany(this, (uint)eccSeed.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, out eccSeed[idx], FieldMode.Write, name: $"SEED_{idx}");
        });

        Registers.Message.DefineMany(this, (uint)eccMsg.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, out eccMsg[idx], FieldMode.Write, name: $"MSG_{idx}");
        });

        Registers.PrivateKeyOut.DefineMany(this, (uint)privKeyOut.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => privKeyOut[idx], name: $"PRIVKEY_OUT_{idx}");
        });

        Registers.PublicKeyX.DefineMany(this, (uint)pubKeyX.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, valueProviderCallback: _ => pubKeyX[idx], writeCallback: (_, val) => pubKeyX[idx] = (uint)val, name: $"PUBKEY_X_{idx}");
        });

        Registers.PublicKeyY.DefineMany(this, (uint)pubKeyY.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, valueProviderCallback: _ => pubKeyY[idx], writeCallback: (_, val) => pubKeyY[idx] = (uint)val, name: $"PUBKEY_Y_{idx}");
        });

        Registers.SignR.DefineMany(this, (uint)signR.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, valueProviderCallback: _ => signR[idx], writeCallback: (_, val) => signR[idx] = (uint)val, name: $"SIGN_R_{idx}");
        });

        Registers.SignS.DefineMany(this, (uint)signS.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, valueProviderCallback: _ => signS[idx], writeCallback: (_, val) => signS[idx] = (uint)val, name: $"SIGN_S_{idx}");
        });

        Registers.VerifyR.DefineMany(this, (uint)verifyR.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => verifyR[idx], name: $"VERIFY_R_{idx}");
        });

        // IV is used by hardware for SCA countermeasures (It does not affect the cryptographic result)
        Registers.InitializationVector.DefineMany(this, 12, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Write, name: $"IV_{idx}");
        });

        Registers.Nonce.DefineMany(this, (uint)eccNonce.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, out eccNonce[idx], FieldMode.Write, name: $"NONCE_{idx}");
        });

        Registers.PrivateKeyIn.DefineMany(this, (uint)privKeyIn.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => privKeyIn[idx] = (uint)val, name: $"PRIVKEY_IN_{idx}");
        });

        Registers.DhSharedKey.DefineMany(this, (uint)dhSharedKey.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => dhSharedKey[idx], name: $"DH_SHARED_KEY_{idx}");
        });

        Registers.KeyVaultReadPrivKeyControl.Define(this)
            .WithFlag(0,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        HandleKvPkeyRead();
                    }
                },
                name: "read_en")
            .WithValueField(1, 5, out kvReadPkeyEntry, name: "read_entry")
            .WithTaggedFlag("pcr_hash_extend", 6)
            .WithReservedBits(7, 25);

        Registers.KeyVaultReadPrivKeyStatus.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "READY")
            .WithFlag(1, out kvRdPkeyStatusValid, FieldMode.Read, name: "VALID")
            .WithEnumField(2, 8, out kvRdPkeyStatusError, FieldMode.Read, name: "ERROR")
            .WithReservedBits(10, 22);

        Registers.KeyVaultReadSeedControl.Define(this)
            .WithFlag(0,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        HandleKvSeedRead();
                    }
                },
                name: "read_en")
            .WithValueField(1, 5, out kvReadSeedEntry, name: "read_entry")
            .WithTaggedFlag("pcr_hash_extend", 6)
            .WithReservedBits(7, 25);

        Registers.KeyVaultReadSeedStatus.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "READY")
            .WithFlag(1, out kvRdSeedStatusValid, FieldMode.Read, name: "VALID")
            .WithEnumField(2, 8, out kvRdSeedStatusError, FieldMode.Read, name: "ERROR")
            .WithReservedBits(10, 22);

        Registers.KeyVaultWritePrivKeyControl.Define(this)
            .WithFlag(0, out kvWriteEn, name: "write_en")
            .WithValueField(1, 5, out kvWriteEntry, name: "write_entry")
            .WithEnumField(6, 9, out kvTagWriteAllowed, name: "dest_valid")
            .WithReservedBits(15, 17);

        Registers.KeyVaultWritePrivKeyStatus.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "READY")
            .WithFlag(1, out kvWrPkeyStatusValid, FieldMode.Read, name: "VALID")
            .WithEnumField(2, 8, out kvWrPkeyStatusError, FieldMode.Read, name: "ERROR")
            .WithReservedBits(10, 22);

        Registers.GlobalIntrEn.Define(this)
            .WithTaggedFlag("error_en", 0)
            .WithTaggedFlag("notif_en", 1)
            .WithReservedBits(2, 30);

        Registers.ErrorIntrEn.Define(this)
            .WithTaggedFlag("error_internal_en", 0)
            .WithReservedBits(1, 31);

        Registers.NotifIntrEn.Define(this)
            .WithTaggedFlag("notif_cmd_done_en", 0)
            .WithReservedBits(1, 31);

        Registers.ErrorGlobalInterrupt.Define(this)
            .WithTaggedFlag("agg_sts", 0)
            .WithReservedBits(1, 31);

        Registers.NotifGlobalIntr.Define(this)
            .WithTaggedFlag("agg_sts", 0)
            .WithReservedBits(1, 31);

        Registers.ErrorInternalIntr.Define(this)
            .WithTaggedFlag("error_internal_sts", 0)
            .WithReservedBits(1, 31);

        Registers.NotifInternalIntr.Define(this)
            .WithTaggedFlag("notif_cmd_done_sts", 0)
            .WithReservedBits(1, 31);

        Registers.ErrorIntrTrig.Define(this)
            .WithTaggedFlag("error_internal_trig", 0)
            .WithReservedBits(1, 31);

        Registers.NotifIntrTrig.Define(this)
            .WithTaggedFlag("notif_cmd_done_trig", 0)
            .WithReservedBits(1, 31);

        Registers.ErrorInternalIntrCount.Define(this)
            .WithValueField(0, 32, name: "cnt");

        Registers.NotifCmdDoneIntrCount.Define(this)
            .WithValueField(0, 32, name: "cnt");

        Registers.ErrorInternalIntrCountIncr.Define(this)
            .WithTaggedFlag("pulse", 0);

        Registers.NotifCmdDoneIntrCountIncr.Define(this)
            .WithTaggedFlag("pulse", 0);
    }

    private void EccZeroize()
    {
        this.Log(LogLevel.Info, "ECC zeroize requested");

        this.Reset();
    }

    private void HandleCommand(EccCommand command)
    {
        if(command == EccCommand.None)
        {
            return;
        }

        ExecuteOperation(command, ToBigEndianBytes(eccMsg));
    }

    private void ExecuteOperation(EccCommand command, byte[] messageBytes)
    {
        eccReady.Value = false;
        eccValid.Value = false;

        try
        {
            switch(command)
            {
            case EccCommand.Keygen:
                ExecuteKeygen();
                break;
            case EccCommand.Sign:
                ExecuteSign(messageBytes);
                break;
            case EccCommand.Verify:
                ExecuteVerify(messageBytes);
                break;
            default:
                this.Log(LogLevel.Error, "Unknown ECC command: {0}", command);
                break;
            }
        }
        catch(Exception e)
        {
            this.Log(LogLevel.Error, "Error during ECC {0} execution: {1}", command, e.Message);
        }

        eccReady.Value = true;
        eccValid.Value = true;
    }

    private void ExecuteKeygen()
    {
        kvWrPkeyStatusValid.Value = false;

        var seed = seedFromKeyVault ? kvSeed : ToBigEndianBytes(eccSeed);
        this.Log(LogLevel.Debug, "Executing ECC Keygen (seed from KV: {0})", seedFromKeyVault);

        var nonce = ToBigEndianBytes(eccNonce);

        var privKey = HMAC_DRBG.Generate(seed, nonce);

        if(kvWriteEn.Value)
        {
            uint slotId = (uint)kvWriteEntry.Value;
            bool success = keyVault.TrySetKey(slotId, privKey, kvTagWriteAllowed.Value);

            if(success)
            {
                kvWrPkeyStatusError.Value = KeyVaultOperationError.Success;
            }
            else
            {
                kvWrPkeyStatusError.Value = KeyVaultOperationError.WriteFail;
            }

            kvWrPkeyStatusValid.Value = true;

            Array.Clear(privKeyOut);
            kvWriteEn.Value = false;
        }
        else if(seedFromKeyVault)
        {
            Array.Clear(privKeyOut);
        }
        else
        {
            ToUintAndCopy(privKey, privKeyOut);
        }

        var curveParams = NistNamedCurves.GetByName(CurveName);
        var d = new BigInteger(1, privKey);
        var q = curveParams.G.Multiply(d).Normalize();

        byte[] pubX = q.AffineXCoord.ToBigInteger().ToByteArrayUnsigned();
        byte[] pubY = q.AffineYCoord.ToBigInteger().ToByteArrayUnsigned();

        ToUintAndCopy(pubX, pubKeyX);
        ToUintAndCopy(pubY, pubKeyY);
    }

    private void ExecuteSign(byte[] messageBytes)
    {
        this.Log(LogLevel.Debug, "Executing ECC Sign");

        var privKey = ToBigEndianBytes(privKeyIn);
        byte[] h = messageBytes;

        byte[] k = HMAC_DRBG.Generate(privKey, h);

        var curveParams = NistNamedCurves.GetByName(CurveName);
        var n = curveParams.N;
        var g = curveParams.G;

        var kInt = new BigInteger(1, k);
        var privInt = new BigInteger(1, privKey);
        var hInt = new BigInteger(1, h);

        // r = x * (k * G) mod n
        var rPoint = g.Multiply(kInt).Normalize();
        var rInt = rPoint.AffineXCoord.ToBigInteger().Mod(n);

        // k^(-1) mod n
        var kInv = kInt.ModInverse(n);

        // s = k^(-1) * (h + r * priv) mod n
        var bracket = hInt.Add(rInt.Multiply(privInt)).Mod(n);
        var sInt = kInv.Multiply(bracket).Mod(n);

        byte[] rFinal = rInt.ToByteArrayUnsigned();
        byte[] sFinal = sInt.ToByteArrayUnsigned();

        ToUintAndCopy(rFinal, signR);
        ToUintAndCopy(sFinal, signS);
    }

    private void ExecuteVerify(byte[] messageBytes)
    {
        this.Log(LogLevel.Debug, "Executing ECC Verify");

        byte[] s = ToBigEndianBytes(signS);
        byte[] r = ToBigEndianBytes(signR);
        byte[] pubKeyXBytes = ToBigEndianBytes(pubKeyX);
        byte[] pubKeyYBytes = ToBigEndianBytes(pubKeyY);
        byte[] h = messageBytes;

        var curveParams = NistNamedCurves.GetByName(CurveName);
        var n = curveParams.N;

        var hInt = new BigInteger(1, h);
        var sInt = new BigInteger(1, s);
        var rInt = new BigInteger(1, r);
        var qxInt = new BigInteger(1, pubKeyXBytes);
        var qyInt = new BigInteger(1, pubKeyYBytes);

        // s1 = s^(-1) mod n
        var s1 = sInt.ModInverse(n);

        // u1 = (h * s1) mod n
        var u1 = hInt.Multiply(s1).Mod(n);
        // u2 = (r * s1) mod n
        var u2 = rInt.Multiply(s1).Mod(n);

        // u1 * G + u2 * pubKey
        var pubKeyPoint = curveParams.Curve.CreatePoint(qxInt, qyInt);
        var rPrimePoint = ECAlgorithms.SumOfTwoMultiplies(curveParams.G, u1, pubKeyPoint, u2).Normalize();

        // r' = R(x) mod n
        var rPrime = rPrimePoint.AffineXCoord.ToBigInteger().Mod(n);
        byte[] rPrimeBytes = rPrime.ToByteArrayUnsigned();

        ToUintAndCopy(rPrimeBytes, verifyR);
    }

    private void ExecuteECDH()
    {
        eccReady.Value = false;
        eccValid.Value = false;

        this.Log(LogLevel.Debug, "Executing ECC ECDH");

        byte[] privKeyBytes = ToBigEndianBytes(privKeyIn);
        byte[] pubKeyXBytes = ToBigEndianBytes(pubKeyX);
        byte[] pubKeyYBytes = ToBigEndianBytes(pubKeyY);

        var curveParams = NistNamedCurves.GetByName(CurveName);
        var priv = new BigInteger(1, privKeyBytes);
        var pubX = new BigInteger(1, pubKeyXBytes);
        var pubY = new BigInteger(1, pubKeyYBytes);

        var pubPoint = curveParams.Curve.CreatePoint(pubX, pubY);
        var sharedPoint = pubPoint.Multiply(priv).Normalize();
        var sharedX = sharedPoint.AffineXCoord.ToBigInteger();

        byte[] sharedKey = sharedX.ToByteArrayUnsigned();
        if(sharedKey.Length > BlockSizeInBytes)
        {
            throw new ArgumentException("Shared key too long");
        }

        var paddedSharedKey = new byte[BlockSizeInBytes];
        Buffer.BlockCopy(sharedKey, 0, paddedSharedKey, BlockSizeInBytes - sharedKey.Length, sharedKey.Length);

        if(kvWriteEn.Value)
        {
            uint slotId = (uint)kvWriteEntry.Value;
            bool success = keyVault.TrySetKey(slotId, paddedSharedKey, kvTagWriteAllowed.Value);

            if(success)
            {
                kvWrPkeyStatusError.Value = KeyVaultOperationError.Success;
            }
            else
            {
                kvWrPkeyStatusError.Value = KeyVaultOperationError.WriteFail;
            }

            kvWrPkeyStatusValid.Value = true;
            kvWriteEn.Value = false;
        }
        else
        {
            ToUintAndCopy(paddedSharedKey, dhSharedKey);
        }

        eccReady.Value = true;
        eccValid.Value = true;
    }

    private void HandleKvSeedRead()
    {
        this.Log(LogLevel.Debug, "Key Vault seed read triggered from entry {0}", kvReadSeedEntry.Value);

        if(keyVault.TryGetKey((uint)kvReadSeedEntry.Value, out var key, Caliptra_KeyVault.DestinationCode.EccSeed))
        {
            Array.Copy(key, kvSeed, BlockSizeInBytes);
            seedFromKeyVault = true;
            kvRdSeedStatusError.Value = KeyVaultOperationError.Success;
        }
        else
        {
            kvRdSeedStatusError.Value = KeyVaultOperationError.ReadFail;
        }
        kvRdSeedStatusValid.Value = true;
    }

    private void HandleKvPkeyRead()
    {
        this.Log(LogLevel.Debug, "Key Vault pkey read triggered from entry {0}", kvReadPkeyEntry.Value);

        if(keyVault.TryGetKey((uint)kvReadPkeyEntry.Value, out var key, Caliptra_KeyVault.DestinationCode.EccPKey))
        {
            ToUintAndCopy(key, privKeyIn);
            kvRdPkeyStatusError.Value = KeyVaultOperationError.Success;
        }
        else
        {
            kvRdPkeyStatusError.Value = KeyVaultOperationError.ReadFail;
        }
        kvRdPkeyStatusValid.Value = true;
    }

    private IFlagRegisterField eccReady;
    private IFlagRegisterField eccValid;

    private IValueRegisterField kvReadPkeyEntry;
    private IFlagRegisterField kvRdPkeyStatusValid;
    private IEnumRegisterField<KeyVaultOperationError> kvRdPkeyStatusError;
    private IValueRegisterField kvReadSeedEntry;
    private IFlagRegisterField kvRdSeedStatusValid;
    private IEnumRegisterField<KeyVaultOperationError> kvRdSeedStatusError;
    private IFlagRegisterField kvWriteEn;
    private IValueRegisterField kvWriteEntry;
    private IEnumRegisterField<Caliptra_KeyVault.DestinationCode> kvTagWriteAllowed;
    private IFlagRegisterField kvWrPkeyStatusValid;
    private IEnumRegisterField<KeyVaultOperationError> kvWrPkeyStatusError;

    private bool seedFromKeyVault;

    private readonly IValueRegisterField[] eccSeed = new IValueRegisterField[12];
    private readonly IValueRegisterField[] eccMsg = new IValueRegisterField[12];
    private readonly IValueRegisterField[] eccNonce = new IValueRegisterField[12];

    private readonly uint[] privKeyOut = new uint[12];
    private readonly uint[] pubKeyX = new uint[12];
    private readonly uint[] pubKeyY = new uint[12];
    private readonly uint[] verifyR = new uint[12];
    private readonly uint[] signR = new uint[12];
    private readonly uint[] signS = new uint[12];
    private readonly uint[] privKeyIn = new uint[12];
    private readonly uint[] dhSharedKey = new uint[12];
    private readonly byte[] kvSeed = new byte[BlockSizeInBytes];

    private readonly Caliptra_KeyVault keyVault;

    private const string CurveName = "P-384";
    private const int BlockSizeInBytes = 48;

    private const ulong ECC_CORE_NAME = 0x38342D33_63707365; // secp-384
    private const ulong ECC_CORE_VERSION = 0x00000000_3030312e; // 1.00

    // Implemented based on: https://chipsalliance.github.io/caliptra-web/docs/2.1/hardware/hardware_spec.html#operation-5
    private static class HMAC_DRBG
    {
        private static readonly BigInteger curveOrderN = NistNamedCurves.GetByName(CurveName).N;

        public static byte[] Generate(byte[] entropy, byte[] nonce)
        {
            // The entropy should be 48 bytes long
            if(entropy == null || entropy.Length != BlockSizeInBytes)
            {
                throw new ArgumentException(string.Format("Entropy must be exactly {0} bytes, BlockSizeInBytes"));
            }
            // The nonce should be 48 bytes long
            if(nonce == null || nonce.Length != BlockSizeInBytes)
            {
                throw new ArgumentException(string.Format("Nonce must be exactly {0} bytes", BlockSizeInBytes));
            }

            // step 1
            byte[] vInit = new byte[BlockSizeInBytes];
            Array.Fill(vInit, (byte)0x01);
            // step 2
            byte[] kInit = new byte[BlockSizeInBytes];
            // step 3
            byte[] buffer1 = Arrays.Concatenate(
                Arrays.Concatenate(vInit, new byte[] { 0x00 }),
                Arrays.Concatenate(entropy, nonce)
            );
            byte[] kTmp = ComputeHmac(kInit, buffer1);
            // step 4
            byte[] vTmp = ComputeHmac(kTmp, vInit);
            // step 5
            byte[] buffer2 = Arrays.Concatenate(
                Arrays.Concatenate(vTmp, new byte[] { 0x01 }),
                Arrays.Concatenate(entropy, nonce)
            );
            byte[] kNew = ComputeHmac(kTmp, buffer2);
            // step 6
            byte[] vNew = ComputeHmac(kNew, vTmp);
            // step 7
            byte[] t = Array.Empty<byte>();
            // steps 8-12
            while(true)
            {
                // step 8
                byte[] tHMAC = ComputeHmac(kNew, vNew);
                t = Arrays.Concatenate(t, tHMAC);
                // step 9
                var tInt = new BigInteger(1, t);
                if(tInt.SignValue > 0 && tInt.CompareTo(curveOrderN) < 0)
                {
                    return t;
                }
                // step 10
                byte[] vConcat = Arrays.Concatenate(vNew, new byte[] { 0x00 });
                kNew = ComputeHmac(kNew, vConcat);
                // step 11
                vNew = ComputeHmac(kNew, vNew);
            }
        }

        private static byte[] ComputeHmac(byte[] key, byte[] data)
        {
            return System.Security.Cryptography.HMACSHA384.HashData(key, data);
        }
    }

    private enum KeyVaultOperationError
    {
        Success,
        ReadFail,
        WriteFail,
    }

    private enum EccCommand
    {
        None = 0b00,
        Keygen = 0b01,
        Sign = 0b10,
        Verify = 0b11,
    }

    private enum Registers
    {
        Name = 0x000,
        Version = 0x008,
        Control = 0x010,
        Status = 0x018,
        Seed = 0x080,
        Message = 0x100,
        PrivateKeyOut = 0x180,
        PublicKeyX = 0x200,
        PublicKeyY = 0x280,
        SignR = 0x300,
        SignS = 0x380,
        VerifyR = 0x400,
        InitializationVector = 0x480,
        Nonce = 0x500,
        PrivateKeyIn = 0x580,
        DhSharedKey = 0x5C0,

        KeyVaultReadPrivKeyControl = 0x600,
        KeyVaultReadPrivKeyStatus = 0x604,
        KeyVaultReadSeedControl = 0x608,
        KeyVaultReadSeedStatus = 0x60C,
        KeyVaultWritePrivKeyControl = 0x610,
        KeyVaultWritePrivKeyStatus = 0x614,

        // Interrupt Register Block
        GlobalIntrEn = 0x800,
        ErrorIntrEn = 0x804,
        NotifIntrEn = 0x808,
        ErrorGlobalInterrupt = 0x80C,
        NotifGlobalIntr = 0x810,
        ErrorInternalIntr = 0x814,
        NotifInternalIntr = 0x818,
        ErrorIntrTrig = 0x81C,
        NotifIntrTrig = 0x820,
        ErrorInternalIntrCount = 0x900,
        NotifCmdDoneIntrCount = 0x980,
        ErrorInternalIntrCountIncr = 0x1000,
        NotifCmdDoneIntrCountIncr = 0x1004,
    }
}