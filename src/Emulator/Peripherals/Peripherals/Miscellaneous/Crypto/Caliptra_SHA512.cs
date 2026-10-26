//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Diagnostics;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

using Org.BouncyCastle.Crypto.Digests;

namespace Antmicro.Renode.Peripherals.Crypto;

// This is an implementation of: https://chipsalliance.github.io/caliptra-web/docs/2.1/hardware/hardware_spec.html#sha512sha384
public class Caliptra_SHA512 : BasicDoubleWordPeripheral, IKnownSize
{
    public Caliptra_SHA512(IMachine machine, Caliptra_KeyVault keyVault) : base(machine)
    {
        this.keyVault = keyVault;

        DefineRegisters();
        Reset();
    }

    public override void Reset()
    {
        base.Reset();

        shaReady.Value = true;

        Array.Clear(shaDigest);
        Array.Clear(shaPcrDigest);

        blockFromKeyVault = false;

        cryptoEngine?.Reset();
    }

    public long Size => 0xA14;

    private static byte[] ToBigEndianBytes(IValueRegisterField[] reg)
    {
        var values = reg.Select(e => (uint)e.Value).ToArray();
        return BitHelper.ToBigEndianBytes(values);
    }

    private static LongDigest CreateEngine(ShaMode mode)
    {
        LongDigest cryptoEngine;
        switch(mode)
        {
        case ShaMode.Sha512_224:
            cryptoEngine = new Sha512tDigest(224);
            break;
        case ShaMode.Sha512_256:
            cryptoEngine = new Sha512tDigest(256);
            break;
        case ShaMode.Sha384:
            cryptoEngine = new Sha384Digest();
            break;
        case ShaMode.Sha512:
            cryptoEngine = new Sha512Digest();
            break;
        default:
            throw new UnreachableException();
        };
        return cryptoEngine;
    }

    private void DefineRegisters()
    {
        Registers.Name.DefineMany(this, 2, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ => (uint)(ShaCoreName >> (idx * 32)),
                name: $"NAME_{idx}");
        });

        Registers.Version.DefineMany(this, 2, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ => (uint)(ShaCoreVersion >> (idx * 32)),
                name: $"VERSION_{idx}");
        });

        Registers.Control.Define(this)
            .WithFlag(0, FieldMode.Write,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        ProcessBlock(true);
                    }
                },
                name: "INIT")
            .WithFlag(1, FieldMode.Write,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        ProcessBlock(false);
                    }
                },
                name: "NEXT")
            .WithEnumField(2, 2, out shaMode, FieldMode.Write, name: "MODE")
            .WithFlag(4, FieldMode.Write,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        ShaZeroize();
                    }
                },
                name: "ZEROIZE")
            .WithFlag(5, out shaLast, name: "LAST")
            .WithFlag(6, out shaRestore, name: "RESTORE")
            .WithReservedBits(7, 25);

        Registers.Status.Define(this)
            .WithFlag(0, out shaReady, FieldMode.Read, name: "READY")
            .WithFlag(1, out shaValid, FieldMode.Read, name: "VALID")
            .WithReservedBits(2, 30);

        Registers.Block.DefineMany(this, (uint)shaBlock.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => shaBlock[idx] = (uint)val, name: $"Block_{idx}");
        });

        Registers.Digest.DefineMany(this, (uint)shaDigest.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, valueProviderCallback: _ => shaDigest[idx], writeCallback: (_, val) => shaDigest[idx] = (uint)val, name: $"Digest_{idx}");
        });

        Registers.KeyVaultReadControl.Define(this)
            .WithFlag(0,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        HandleKvRead();
                    }
                },
                name: "read_en")
            .WithValueField(1, 5, out kvReadEntry, name: "read_entry")
            .WithTaggedFlag("pcr_hash_extend", 6)
            .WithReservedBits(7, 25);

        Registers.KeyVaultReadStatus.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "READY")
            .WithFlag(1, out kvRdStatusValid, name: "VALID")
            .WithEnumField(2, 8, out kvRdStatusError, FieldMode.Read, name: "ERROR")
            .WithReservedBits(10, 22);

        Registers.KeyVaultWriteControl.Define(this)
            .WithFlag(0, out kvWriteEn, name: "write_en")
            .WithValueField(1, 5, out kvWriteEntry, name: "write_entry")
            .WithEnumField(6, 9, out kvTagWriteAllowed, name: "dest_valid")
            .WithReservedBits(15, 17);

        Registers.KeyVaultWriteStatus.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "READY")
            .WithFlag(1, out kvWrStatusValid, FieldMode.Read, name: "VALID")
            .WithEnumField(2, 8, out kvWrStatusError, FieldMode.Read, name: "ERROR")
            .WithReservedBits(10, 22);

        Registers.GenPcrHashNonce.DefineMany(this, (uint)shaPcrNonce.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, out shaPcrNonce[idx], FieldMode.Write, name: $"NONCE_{idx}");
        });

        Registers.GenPcrHashCtrl.Define(this)
            .WithFlag(0, FieldMode.Write,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        StartGenPcrHash();
                    }
                },
                name: "START")
            .WithReservedBits(1, 31);

        Registers.GenPcrHashStatus.Define(this)
            .WithFlag(0, out shaPcrReady, FieldMode.Read, name: "READY")
            .WithFlag(1, out shaPcrValid, FieldMode.Read, name: "VALID")
            .WithReservedBits(2, 30);

        Registers.GenPcrHashDigest.DefineMany(this, (uint)shaPcrDigest.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => shaPcrDigest[idx], name: $"DIGEST_{idx}");
        });

        Registers.GlobalIntrEn.Define(this)
            .WithTaggedFlag("error_en", 0)
            .WithTaggedFlag("notif_en", 1)
            .WithReservedBits(2, 30);

        Registers.ErrorIntrEn.Define(this)
            .WithTaggedFlag("error0_en", 0)
            .WithTaggedFlag("error1_en", 1)
            .WithTaggedFlag("error2_en", 2)
            .WithTaggedFlag("error3_en", 3)
            .WithReservedBits(4, 28);

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
            .WithTaggedFlag("error0_sts", 0)
            .WithTaggedFlag("error1_sts", 1)
            .WithTaggedFlag("error2_sts", 2)
            .WithTaggedFlag("error3_sts", 3)
            .WithReservedBits(4, 28);

        Registers.NotifInternalIntr.Define(this)
            .WithTaggedFlag("notif_cmd_done_sts", 0)
            .WithReservedBits(1, 31);

        Registers.ErrorIntrTrig.Define(this)
            .WithTaggedFlag("error0_trig", 0)
            .WithTaggedFlag("error1_trig", 1)
            .WithTaggedFlag("error2_trig", 2)
            .WithTaggedFlag("error3_trig", 3)
            .WithReservedBits(4, 28);

        Registers.NotifIntrTrig.Define(this)
            .WithTaggedFlag("notif_cmd_done_trig", 0)
            .WithReservedBits(1, 31);

        Registers.ErrorIntrCount0.Define(this)
            .WithValueField(0, 32, name: "cnt");

        Registers.ErrorIntrCount1.Define(this)
            .WithValueField(0, 32, name: "cnt");

        Registers.ErrorIntrCount2.Define(this)
            .WithValueField(0, 32, name: "cnt");

        Registers.ErrorIntrCount3.Define(this)
            .WithValueField(0, 32, name: "cnt");

        Registers.NotifCmdDoneIntrCount.Define(this)
            .WithValueField(0, 32, name: "cnt");

        Registers.ErrorIntrCountIncr0.Define(this)
            .WithTaggedFlag("pulse", 0)
            .WithReservedBits(1, 31);

        Registers.ErrorIntrCountIncr1.Define(this)
            .WithTaggedFlag("pulse", 0)
            .WithReservedBits(1, 31);

        Registers.ErrorIntrCountIncr2.Define(this)
            .WithTaggedFlag("pulse", 0)
            .WithReservedBits(1, 31);

        Registers.ErrorIntrCountIncr3.Define(this)
            .WithTaggedFlag("pulse", 0)
            .WithReservedBits(1, 31);

        Registers.NotifCmdDoneIntrCountIncr.Define(this)
            .WithTaggedFlag("pulse", 0)
            .WithReservedBits(1, 31);
    }

    private void ShaZeroize()
    {
        this.Log(LogLevel.Info, "SHA512 zeroize requested");

        this.Reset();
    }

    private void ProcessBlock(bool initEngine)
    {
        shaReady.Value = false;
        shaValid.Value = false;

        if(initEngine)
        {
            cryptoEngine = CreateEngine(shaMode.Value);
        }

        if(shaRestore.Value)
        {
            this.Log(LogLevel.Debug, "SHA512 restore triggered");
            byte[] currentDigestBytes = BitHelper.ToBigEndianBytes(shaDigest);
            cryptoEngine.SetCurrentDigest(currentDigestBytes, 0);
        }

        var blockBytes = blockFromKeyVault ? kvBlock : BitHelper.ToBigEndianBytes(shaBlock);
        this.Log(LogLevel.Debug, "Processing SHA block (data from KV: {0})", blockFromKeyVault);

        cryptoEngine.BlockUpdate(blockBytes, 0, blockBytes.Length);

        var fullDigest = new byte[DigestSizeInBytes];
        int digestSize = cryptoEngine.GetCurrentDigest(fullDigest, 0);

        if(kvWriteEn.Value && shaLast.Value)
        {
            var kvOutput = new byte[digestSize];
            Array.Copy(fullDigest, kvOutput, digestSize);

            uint slotId = (uint)kvWriteEntry.Value;
            bool success = keyVault.TrySetKey(slotId, kvOutput, kvTagWriteAllowed.Value);

            if(success)
            {
                kvWrStatusError.Value = KeyVaultOperationError.Success;
            }
            else
            {
                kvWrStatusError.Value = KeyVaultOperationError.WriteFail;
            }

            kvWrStatusValid.Value = true;

            Array.Clear(shaDigest);
            kvWriteEn.Value = false;
        }
        else
        {
            BitHelper.ToUintAndCopy(fullDigest, shaDigest, littleEndian: false);
        }

        shaReady.Value = true;
        shaValid.Value = true;
    }

    private void HandleKvRead()
    {
        this.Log(LogLevel.Debug, "Key Vault read triggered from entry {0}", kvReadEntry.Value);

        if(keyVault.TryGetKey((uint)kvReadEntry.Value, out var key, Caliptra_KeyVault.DestinationCode.None))
        {
            Array.Copy(key, kvBlock, 128);
            blockFromKeyVault = true;
            kvRdStatusError.Value = KeyVaultOperationError.Success;
        }
        else
        {
            kvRdStatusError.Value = KeyVaultOperationError.ReadFail;
        }

        kvRdStatusValid.Value = true;
    }

    private void StartGenPcrHash()
    {
        // PCR vault is not implemented yet
        this.Log(LogLevel.Error, "PCR hash generation requested, but the PCR Vault is not implemented yet.");
    }

    private IEnumRegisterField<ShaMode> shaMode;
    private IFlagRegisterField shaRestore;
    private IFlagRegisterField shaLast;
    private IFlagRegisterField shaReady;
    private IFlagRegisterField shaValid;
    private IValueRegisterField kvReadEntry;
    private IFlagRegisterField kvRdStatusValid;
    private IEnumRegisterField<KeyVaultOperationError> kvRdStatusError;
    private IFlagRegisterField kvWriteEn;
    private IValueRegisterField kvWriteEntry;
    private IEnumRegisterField<Caliptra_KeyVault.DestinationCode> kvTagWriteAllowed;
    private IFlagRegisterField kvWrStatusValid;
    private IEnumRegisterField<KeyVaultOperationError> kvWrStatusError;
    private IFlagRegisterField shaPcrReady;
    private IFlagRegisterField shaPcrValid;

    private LongDigest cryptoEngine;

    private bool blockFromKeyVault;

    private readonly IValueRegisterField[] shaPcrNonce = new IValueRegisterField[8];

    private readonly uint[] shaBlock = new uint[BlockSize];
    private readonly uint[] shaDigest = new uint[DigestSize];
    private readonly uint[] shaPcrDigest = new uint[DigestSize];
    private readonly byte[] kvBlock = new byte[BlockSizeInBytes];

    private readonly Caliptra_KeyVault keyVault;

    private const int BlockSize = 32;
    private const int DigestSize = 16;
    private const int BlockSizeInBytes = BlockSize * 4;
    private const int DigestSizeInBytes = DigestSize * 4;

    private const ulong ShaCoreName = 0x31322d35_61327368; // sha-512
    private const ulong ShaCoreVersion = 0x00000000_3830302e; // 0.80

    private enum KeyVaultOperationError
    {
        Success,
        ReadFail,
        WriteFail,
    }

    private enum ShaMode
    {
        Sha512_224,
        Sha512_256,
        Sha384,
        Sha512,
    }

    private enum Registers
    {
        Name = 0x000,
        Version = 0x008,
        Control = 0x010,
        Status = 0x018,
        Block = 0x080,
        Digest = 0x100,

        KeyVaultReadControl = 0x600,
        KeyVaultReadStatus = 0x604,
        KeyVaultWriteControl = 0x608,
        KeyVaultWriteStatus = 0x60C,
        GenPcrHashNonce = 0x610,
        GenPcrHashCtrl = 0x630,
        GenPcrHashStatus = 0x634,
        GenPcrHashDigest = 0x638,

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
        ErrorIntrCount0 = 0x900,
        ErrorIntrCount1 = 0x904,
        ErrorIntrCount2 = 0x908,
        ErrorIntrCount3 = 0x90C,
        NotifCmdDoneIntrCount = 0x980,
        ErrorIntrCountIncr0 = 0x1000,
        ErrorIntrCountIncr1 = 0x1004,
        ErrorIntrCountIncr2 = 0x1008,
        ErrorIntrCountIncr3 = 0x100C,
        NotifCmdDoneIntrCountIncr = 0x1010,
    }
}