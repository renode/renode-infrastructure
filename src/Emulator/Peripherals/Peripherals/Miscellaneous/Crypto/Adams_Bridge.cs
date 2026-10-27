//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace Antmicro.Renode.Peripherals.Crypto;

// This is the implementation of: https://github.com/chipsalliance/adams-bridge/tree/76aa70ef4d27ce9c6bfc4a1b1f04d81b90c480d6
public class Adams_Bridge : BasicDoubleWordPeripheral, IKnownSize
{
    public Adams_Bridge(IMachine machine, Caliptra_KeyVault keyVault) : base(machine)
    {
        this.keyVault = keyVault;

        DefineRegisters();
        Reset();
    }

    public override void Reset()
    {
        base.Reset();

        Array.Clear(verifyResult);
        Array.Clear(pubKey);
        Array.Clear(signature);
        Array.Clear(privKeyOut);
        Array.Clear(privKeyIn);

        streamedMessage.Clear();
        pendingCommand = MldsaCommand.None;
        currentPrivateKey = null;

        UpdateInterrupts();
    }

    public long Size => 0xC018;

    public GPIO ErrorInterrupt { get; } = new GPIO();

    public GPIO NotificationInterrupt { get; } = new GPIO();

    private static IEnumerable<byte> ToBytes(IValueRegisterField[] reg) => ToBytes(reg.Select(e => (uint)e.Value).ToArray());

    private static IEnumerable<byte> ToBytes(uint[] reg) => reg.SelectMany(e => BitConverter.GetBytes(e));

    private void DefineRegisters()
    {
        Registers.MldsaName.DefineMany(this, 2, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ => (uint)(MldsaCoreName >> (idx * 32)),
                name: $"NAME_{idx}");
        });

        Registers.MldsaVersion.DefineMany(this, 2, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ => (uint)(MldsaCoreVersion >> (idx * 32)),
                name: $"VERSION_{idx}");
        });

        Registers.MldsaControl.Define(this)
            .WithEnumField<DoubleWordRegister, MldsaCommand>(0, 3, out var command, FieldMode.Write, name: "CTRL")
            .WithFlag(3, out var zeroize, FieldMode.Write, name: "ZEROIZE")
            .WithFlag(4, out platformConfigurationRegistersSign, FieldMode.Write, name: "PCR_SIGN")
            .WithFlag(5, out externalMu, FieldMode.Write, name: "EXTERNAL_MU")
            .WithFlag(6, out streamMessage, FieldMode.Write, name: "STREAM_MSG")
            .WithReservedBits(7, 25)
            .WithWriteCallback((_, __) =>
            {
                HandleCommand(command.Value);
                if(zeroize.Value)
                {
                    MldsaZeroize();
                }
            });

        Registers.MldsaStatus.Define(this, 0x1)
            .WithFlag(0, out mldsaReady, FieldMode.Read, name: "READY")
            .WithFlag(1, out mldsaValid, FieldMode.Read, name: "VALID")
            .WithFlag(2, out mldsaStreamReady, FieldMode.Read, name: "MSG_STREAM_READY")
            .WithFlag(3, out mldsaError, FieldMode.Read, name: "ERROR")
            .WithFlag(4, out mldsaVerifyPass, FieldMode.Read, name: "VERIFY_PASS")
            .WithReservedBits(5, 27);

        // Entropy is used by hardware for SCA countermeasures (no action in functional simulation)
        Registers.AbrEntropy.DefineMany(this, 16, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Write, name: $"ENTROPY_{idx}");
        });

        Registers.MldsaSeed.DefineMany(this, (uint)mldsaSeed.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => mldsaSeed[idx] = (uint)val, name: $"SEED_{idx}");
        });

        Registers.MldsaSignRandom.DefineMany(this, (uint)mldsaSignRnd.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, out mldsaSignRnd[idx], FieldMode.Write, name: $"SIGN_RND_{idx}");
        });

        Registers.MldsaMessage.DefineMany(this, (uint)mldsaMessage.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, out mldsaMessage[idx], FieldMode.Write, name: $"MSG_{idx}");
            if(idx == 0)
            {
                // Relevant in streaming mode only - write to MSG_0 to pass the message
                register.WithWriteCallback((_, val) => HandleMsgWrite((uint)val));
            }
        });

        Registers.MldsaVerifyResult.DefineMany(this, (uint)verifyResult.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => verifyResult[idx], name: $"VERIFY_RES_{idx}");
        });

        Registers.MldsaExternalMu.DefineMany(this, (uint)mldsaExternalMu.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, out mldsaExternalMu[idx], FieldMode.Write, name: $"EXTERNAL_MU_{idx}");
        });

        Registers.MldsaMessageStrobe.Define(this, 0xF)
            .WithValueField(0, 4, out messageStrobe, FieldMode.Write, name: "STROBE")
            .WithReservedBits(4, 28);

        Registers.MldsaCtxConfig.Define(this)
            .WithValueField(0, 8, out contextSize, FieldMode.Write, name: "CTX_SIZE")
            .WithReservedBits(8, 24);

        Registers.MldsaCtx.DefineMany(this, (uint)mldsaContext.Length, (register, idx) =>
        {
            register.WithValueField(0, 32, out mldsaContext[idx], FieldMode.Write, name: $"CTX_{idx}");
        });

        Registers.MldsaPubkey.DefineMany(this, PubKeyWords, (register, idx) =>
        {
            register.WithValueField(0, 32, valueProviderCallback: _ => pubKey[idx], writeCallback: (_, val) => pubKey[idx] = (uint)val, name: $"PUBKEY_{idx}");
        });

        Registers.MldsaSignature.DefineMany(this, SigWords, (register, idx) =>
        {
            register.WithValueField(0, 32, valueProviderCallback: _ => signature[idx], writeCallback: (_, val) => signature[idx] = (uint)val, name: $"SIGNATURE_{idx}");
        });

        Registers.MldsaPrivkeyOut.DefineMany(this, PrivKeyWords, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => privKeyOut[idx], name: $"PRIVKEY_OUT_{idx}");
        });

        Registers.MldsaPrivkeyIn.DefineMany(this, PrivKeyWords, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => privKeyIn[idx] = (uint)val, name: $"PRIVKEY_IN_{idx}");
        });

        Registers.KeyvaultMldsaSeedReadControl.Define(this)
            .WithFlag(0, FieldMode.Write,
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        HandleKvSeedRead();
                    }
                },
                name: "read_en")
            .WithValueField(1, 5, out keyvaultReadEntry, name: "read_entry")
            // Reused RTL, no-op in ABR
            .WithTaggedFlag("pcr_hash_extend", 6, silent: true)
            .WithReservedBits(7, 25);

        Registers.KeyvaultMldsaSeedReadStatus.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "READY")
            .WithFlag(1, out keyvaultStatusValid, FieldMode.Read, name: "VALID")
            .WithEnumField(2, 8, out keyvaultStatusError, FieldMode.Read, name: "ERROR")
            .WithReservedBits(10, 22);

        Registers.GlobalInterruptEnable.Define(this)
            .WithFlag(0, out errorGlobalEnable, name: "error_en")
            .WithFlag(1, out notificationGlobalEnable, name: "notif_en")
            .WithReservedBits(2, 30)
            .WithChangeCallback((_, __) => UpdateInterrupts());

        Registers.ErrorInterruptEnable.Define(this)
            .WithFlag(0, out errorInternalEnable, name: "error_internal_en")
            .WithReservedBits(1, 31)
            .WithChangeCallback((_, __) => UpdateInterrupts());

        Registers.NotificationInterruptEnable.Define(this)
            .WithFlag(0, out notificationCmdDoneEnable, name: "notif_cmd_done_en")
            .WithReservedBits(1, 31)
            .WithChangeCallback((_, __) => UpdateInterrupts());

        Registers.ErrorGlobalInterrupt.Define(this)
            .WithFlag(0, out errorGlobalStatus, FieldMode.Read, name: "agg_sts")
            .WithReservedBits(1, 31);

        Registers.NotificationGlobalInterrupt.Define(this)
            .WithFlag(0, out notificationGlobalStatus, FieldMode.Read, name: "agg_sts")
            .WithReservedBits(1, 31);

        Registers.ErrorInternalInterrupt.Define(this)
            .WithFlag(0, out errorInternalStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "error_internal_sts")
            .WithReservedBits(1, 31)
            .WithChangeCallback((_, __) => UpdateInterrupts());

        Registers.NotificationInternalInterrupt.Define(this)
            .WithFlag(0, out notificationCmdDoneStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "notif_cmd_done_sts")
            .WithReservedBits(1, 31)
            .WithChangeCallback((_, __) => UpdateInterrupts());

        Registers.ErrorInterruptTrigger.Define(this)
            .WithFlag(0, FieldMode.Write,
                writeCallback: (_, val) =>
                {
                    if(!val)
                    {
                        return;
                    }
                    errorInternalStatus.Value = true;
                    if(errorCount.Value < uint.MaxValue)
                    {
                        errorCount.Value++;
                    }
                    UpdateInterrupts();
                },
                name: "error_internal_trig")
            .WithReservedBits(1, 31);

        Registers.NotifInterruptTrigger.Define(this)
            .WithFlag(0, FieldMode.Write,
                writeCallback: (_, val) =>
                {
                    if(!val)
                    {
                        return;
                    }
                    notificationCmdDoneStatus.Value = true;
                    if(notificationCount.Value < uint.MaxValue)
                    {
                        notificationCount.Value++;
                    }
                    UpdateInterrupts();
                },
                name: "notif_cmd_done_trig")
            .WithReservedBits(1, 31);

        Registers.ErrorInternalInterruptCount.Define(this)
            .WithValueField(0, 32, out errorCount, name: "cnt");

        Registers.NotifCmdDoneInterruptCount.Define(this)
            .WithValueField(0, 32, out notificationCount, name: "cnt");

        Registers.ErrorInternalInterruptCountIncrement.Define(this)
            .WithTag("pulse", 0, 32);

        Registers.NotificationCmdDoneIntrruptCountIncrement.Define(this)
            .WithTag("pulse", 0, 32);

        // MLKEM component stubs
        Registers.MlkemName.DefineMany(this, 2, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ => (uint)(MlkemCoreName >> (idx * 32)),
                name: $"NAME_{idx}");
        });

        Registers.MlkemVersion.DefineMany(this, 2, (register, idx) =>
        {
            register.WithValueField(0, 32, FieldMode.Read,
                valueProviderCallback: _ => (uint)(MlkemCoreVersion >> (idx * 32)),
                name: $"VERSION_{idx}");
        });

        // MLKEM is not implemented
        Registers.MlkemStatus.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "READY")
            .WithReservedBits(1, 31);
    }

    private void MldsaZeroize()
    {
        this.Log(LogLevel.Info, "MLDSA zeroize requested");

        this.Reset();
    }

    private void HandleCommand(MldsaCommand command)
    {
        if(command == MldsaCommand.None)
        {
            return;
        }

        if(platformConfigurationRegistersSign.Value && command != MldsaCommand.KeygenAndSign)
        {
            this.Log(LogLevel.Warning, "PCR_SIGN asserted for command other than KeygenAndSign: {0}", command);
            mldsaReady.Value = false;
            mldsaValid.Value = true;
            mldsaError.Value = true;
            TriggerErrorInterrupt();
            return;
        }

        if(streamMessage.Value && (command == MldsaCommand.Sign || command == MldsaCommand.Verify || command == MldsaCommand.KeygenAndSign))
        {
            this.Log(LogLevel.Debug, "Entering message streaming mode for command {0}", command);
            pendingCommand = command;
            streamedMessage.Clear();
            mldsaReady.Value = false;
            mldsaValid.Value = false;
            mldsaStreamReady.Value = true;
            return;
        }

        ExecuteOperation(command, ToBytes(mldsaMessage).ToArray());
    }

    private void HandleMsgWrite(uint value)
    {
        if(!mldsaStreamReady.Value)
        {
            return;
        }

        var strobe = messageStrobe.Value;
        var validBytes = 0;
        while((strobe & 0x1) != 0)
        {
            ++validBytes;
            strobe >>= 1;
        }

        for(int i = 0; i < validBytes; i++)
        {
            streamedMessage.Add((byte)(value >> (i * 8)));
        }

        // Last DWORD is either 0x0 (if aligned) - and dummy message is discarded
        // or has any strobe set (different than 0xF) if it's not aligned
        // So strobe that is not all ones is a terminal condition for streaming mode
        // and we will issue the command
        if(messageStrobe.Value != 0xF)
        {
            mldsaStreamReady.Value = false;
            ExecuteOperation(pendingCommand, streamedMessage.ToArray());
            pendingCommand = MldsaCommand.None;
            streamedMessage.Clear();
        }
    }

    private void ExecuteOperation(MldsaCommand command, byte[] messageBytes)
    {
        mldsaReady.Value = false;
        mldsaValid.Value = false;
        mldsaError.Value = false;

        try
        {
            switch(command)
            {
            case MldsaCommand.Keygen:
                ExecuteKeygen();
                break;
            case MldsaCommand.Sign:
                ExecuteSign(messageBytes, useGeneratedKey: false);
                break;
            case MldsaCommand.Verify:
                ExecuteVerify(messageBytes);
                break;
            case MldsaCommand.KeygenAndSign:
                ExecuteKeygen();
                if(!mldsaError.Value)
                {
                    ExecuteSign(messageBytes, useGeneratedKey: true);
                }
                break;
            default:
                this.Log(LogLevel.Warning, "Unknown MLDSA command: {0}", command);
                break;
            }
        }
        catch(Exception e)
        {
            this.Log(LogLevel.Error, "Error during MLDSA {0} execution: {1}", command, e.Message);
            mldsaError.Value = true;
        }

        mldsaValid.Value = true;
        if(mldsaError.Value)
        {
            TriggerErrorInterrupt();
        }
        else
        {
            TriggerNotifInterrupt();
        }
        mldsaReady.Value = true;
    }

    private void ExecuteKeygen()
    {
        var seed = ToBytes(mldsaSeed).ToArray();
        this.Log(LogLevel.Debug, "Executing MLDSA Keygen (seed from KV: {0}), value {1}", keyvaultStatusValid.Value, seed.ToLazyHexString().ToString());

        if(platformConfigurationRegistersSign.Value)
        {
            this.ErrorLog("PCR signing is not yet implemented");
            TriggerErrorInterrupt();
            return;
        }

        var privKey = MLDsaPrivateKeyParameters.FromSeed(MLDsaParameters.ml_dsa_87, seed);
        BitHelper.ToUintAndCopy(privKey.GetPublicKeyEncoded(), pubKey, littleEndian: true);

        if(keyvaultStatusValid.Value)
        {
            Array.Clear(privKeyOut);
        }
        else
        {
            // "Clear the [read] lock only after completing standalone keygen where the seed did not come from the keyvault"
            BitHelper.ToUintAndCopy(privKey.GetEncoded(), privKeyOut, littleEndian: true);
        }

        currentPrivateKey = privKey;
    }

    private void ExecuteSign(byte[] messageBytes, bool useGeneratedKey)
    {
        this.Log(LogLevel.Debug, "Executing MLDSA Sign (message len: {0}, external_mu: {1})",
            messageBytes.Length, externalMu.Value);

        MLDsaPrivateKeyParameters privKey;
        if(currentPrivateKey != null && (useGeneratedKey || platformConfigurationRegistersSign.Value))
        {
            privKey = currentPrivateKey;
        }
        else
        {
            try
            {
                privKey = MLDsaPrivateKeyParameters.FromEncoding(MLDsaParameters.ml_dsa_87, ToBytes(privKeyIn).ToArray());
            }
            catch(Exception ex)
            {
                this.Log(LogLevel.Error, "Failed to decode MLDSA private key: {0}", ex.Message);
                mldsaError.Value = true;
                return;
            }
        }

        // sign_rnd should be all zero for deterministic variant
        var signRnd = ToBytes(mldsaSignRnd).ToArray();
        var isDeterministic = signRnd.All(b => b == 0);

        var signer = new MLDsaSigner(MLDsaParameters.ml_dsa_87, isDeterministic);
        var context = ToBytes(mldsaContext).Take((int)contextSize.Value).ToArray();

        ICipherParameters cipherParams = contextSize.Value > 0
            ? new ParametersWithContext(privKey, context)
            : privKey;

        if(!isDeterministic)
        {
            cipherParams = new ParametersWithRandom(cipherParams, new FixedSecureRandom(signRnd));
        }

        signer.Init(true, cipherParams);

        if(externalMu.Value)
        {
            InjectExternalMu(signer, ToBytes(mldsaExternalMu).ToArray());
        }
        else
        {
            signer.BlockUpdate(messageBytes);
        }

        BitHelper.ToUintAndCopy(signer.GenerateSignature(), signature, littleEndian: true);
    }

    private void ExecuteVerify(byte[] messageBytes)
    {
        this.Log(LogLevel.Debug, "Executing MLDSA Verify (message len: {0}, external_mu: {1})",
            messageBytes.Length, externalMu.Value);

        mldsaVerifyPass.Value = false;

        MLDsaPublicKeyParameters publicKey;
        try
        {
            publicKey = MLDsaPublicKeyParameters.FromEncoding(MLDsaParameters.ml_dsa_87, ToBytes(pubKey).ToArray());
        }
        catch(Exception ex)
        {
            this.Log(LogLevel.Error, "Failed to decode MLDSA public key: {0}", ex.Message);
            mldsaError.Value = true;
            return;
        }

        var verifier = new MLDsaSigner(MLDsaParameters.ml_dsa_87, deterministic: true);

        if(contextSize.Value > 0)
        {
            var context = ToBytes(mldsaContext).Take((int)contextSize.Value).ToArray();
            verifier.Init(false, new ParametersWithContext(publicKey, context));
        }
        else
        {
            verifier.Init(false, publicKey);
        }

        if(externalMu.Value)
        {
            InjectExternalMu(verifier, ToBytes(mldsaExternalMu).ToArray());
        }
        verifier.BlockUpdate(messageBytes);

        var sigBytes = ToBytes(signature).ToArray();

        var passed = false;
        try
        {
            passed = verifier.VerifySignature(sigBytes[..FipsSignatureBytes]);
        }
        catch(Exception ex)
        {
            this.Log(LogLevel.Error, "Verification failed due to an error: {0}", ex.Message);
            passed = false;
        }

        this.Log(LogLevel.Debug, "MLDSA Verification status: {0}", passed ? "passed" : "failed");

        mldsaVerifyPass.Value = passed;

        // In HW, MLDSA_VERIFY_RES receives recomputed c~ (first 64 bytes of signature) if passed,
        // or inverted signature c~ if failed.
        for(var i = 0; i < 16; i++)
        {
            verifyResult[i] = passed ? signature[i] : ~signature[i];
        }
    }

    private void InjectExternalMu(MLDsaSigner _, byte[] __)
    {
        // Currently not implemented
        this.Log(LogLevel.Error, "External MU mode is not currently supported");
        TriggerErrorInterrupt();
    }

    private void HandleKvSeedRead()
    {
        this.Log(LogLevel.Debug, "Key Vault seed read triggered from entry {0}", keyvaultReadEntry.Value);

        if(keyVault.TryGetKey((uint)keyvaultReadEntry.Value, out var key, Caliptra_KeyVault.DestinationCode.MldsaSeed))
        {
            for(int i = 0; i < mldsaSeed.Length; ++i)
            {
                mldsaSeed[mldsaSeed.Length - i - 1] = key[i];
            }
            keyvaultStatusError.Value = KeyVaultOperationStatus.Success;
        }
        else
        {
            keyvaultStatusError.Value = KeyVaultOperationStatus.ReadFail;
            TriggerErrorInterrupt();
        }
        keyvaultStatusValid.Value = true;
    }

    private void TriggerNotifInterrupt()
    {
        notificationCmdDoneStatus.Value = true;
        ++notificationCount.Value;
        UpdateInterrupts();
    }

    private void TriggerErrorInterrupt()
    {
        errorInternalStatus.Value = true;
        ++errorCount.Value;
        UpdateInterrupts();
    }

    private void UpdateInterrupts()
    {
        var errorAgg = errorInternalStatus.Value && errorInternalEnable.Value && errorGlobalEnable.Value;
        var notifAgg = notificationCmdDoneStatus.Value && notificationCmdDoneEnable.Value && notificationGlobalEnable.Value;

        errorGlobalStatus.Value = errorAgg;
        notificationGlobalStatus.Value = notifAgg;

        ErrorInterrupt.Set(errorAgg);
        NotificationInterrupt.Set(notifAgg);
    }

    private IFlagRegisterField platformConfigurationRegistersSign;
    private IFlagRegisterField externalMu;
    private IFlagRegisterField streamMessage;

    private IFlagRegisterField mldsaReady;
    private IFlagRegisterField mldsaValid;
    private IFlagRegisterField mldsaStreamReady;
    private IFlagRegisterField mldsaError;
    private IFlagRegisterField mldsaVerifyPass;

    private IValueRegisterField messageStrobe;
    private IValueRegisterField contextSize;

    private IValueRegisterField keyvaultReadEntry;
    private IFlagRegisterField keyvaultStatusValid;
    private IEnumRegisterField<KeyVaultOperationStatus> keyvaultStatusError;

    private IFlagRegisterField errorGlobalEnable;
    private IFlagRegisterField notificationGlobalEnable;
    private IFlagRegisterField errorInternalEnable;
    private IFlagRegisterField notificationCmdDoneEnable;
    private IFlagRegisterField errorGlobalStatus;
    private IFlagRegisterField notificationGlobalStatus;
    private IFlagRegisterField errorInternalStatus;
    private IFlagRegisterField notificationCmdDoneStatus;
    private IValueRegisterField errorCount;
    private IValueRegisterField notificationCount;

    private MldsaCommand pendingCommand;
    private MLDsaPrivateKeyParameters currentPrivateKey;

    private readonly IValueRegisterField[] mldsaSignRnd = new IValueRegisterField[8];
    private readonly IValueRegisterField[] mldsaMessage = new IValueRegisterField[16];
    private readonly IValueRegisterField[] mldsaExternalMu = new IValueRegisterField[16];
    private readonly IValueRegisterField[] mldsaContext = new IValueRegisterField[64];

    private readonly uint[] pubKey = new uint[PubKeyWords];
    private readonly uint[] signature = new uint[SigWords];
    private readonly uint[] privKeyOut = new uint[PrivKeyWords];
    private readonly uint[] privKeyIn = new uint[PrivKeyWords];
    private readonly uint[] verifyResult = new uint[16];
    private readonly uint[] mldsaSeed = new uint[MLDSASeedSizeWords];

    private readonly Caliptra_KeyVault keyVault;
    private readonly List<byte> streamedMessage = new List<byte>();

    private const int PubKeyWords = 648;

    private const int SigWords = 1157;
    private const int FipsSignatureBytes = 4627;

    private const int PrivKeyWords = 1224;
    private const int MLDSASeedSizeWords = 8;

    private const ulong MldsaCoreName = 0x3837412D_44534D4C; // "MLDSA-87"
    private const ulong MldsaCoreVersion = 0x00003100_302e322e; // "2.0.1"
    private const ulong MlkemCoreName = 0x32343130_4D2D4B45; // "KEM-1024"
    private const ulong MlkemCoreVersion = 0x00003100_302e322e; // "2.0.1"

    private class FixedSecureRandom : SecureRandom
    {
        public FixedSecureRandom(byte[] data)
        {
            this.data = data;
            this.index = 0;
        }

        public override void NextBytes(byte[] bytes)
        {
            NextBytes(bytes, 0, bytes.Length);
        }

        public override void NextBytes(byte[] buf, int off, int len)
        {
            if((len + index) > data.Length)
            {
                throw new IndexOutOfRangeException("Not enough random data for the engine");
            }
            Array.Copy(data, index, buf, off, len);
            index += len;
        }

        public override void NextBytes(Span<byte> buffer)
        {
            if((buffer.Length + index) > data.Length)
            {
                throw new IndexOutOfRangeException("Not enough random data for the engine");
            }
            data[index..(index + buffer.Length)].CopyTo(buffer);
            index += buffer.Length;
        }

        private int index;
        private readonly byte[] data;
    }

    private enum KeyVaultOperationStatus
    {
        Success,
        ReadFail,
        WriteFail,
    }

    private enum MldsaCommand
    {
        None = 0b000,
        Keygen = 0b001,
        Sign = 0b010,
        Verify = 0b011,
        KeygenAndSign = 0b100,
    }

    private enum Registers
    {
        MldsaName = 0x0000,
        MldsaVersion = 0x0008,
        MldsaControl = 0x0010,
        MldsaStatus = 0x0014,
        AbrEntropy = 0x0018,
        MldsaSeed = 0x0058,
        MldsaSignRandom = 0x0078,
        MldsaMessage = 0x0098,
        MldsaVerifyResult = 0x00D8,
        MldsaExternalMu = 0x0118,
        MldsaMessageStrobe = 0x0158,
        MldsaCtxConfig = 0x015C,
        MldsaCtx = 0x0160,
        MldsaPubkey = 0x1000,
        MldsaSignature = 0x2000,
        MldsaPrivkeyOut = 0x4000,
        MldsaPrivkeyIn = 0x6000,

        KeyvaultMldsaSeedReadControl = 0x8000,
        KeyvaultMldsaSeedReadStatus = 0x8004,

        // Interrupt block
        GlobalInterruptEnable = 0x8100,
        ErrorInterruptEnable = 0x8104,
        NotificationInterruptEnable = 0x8108,
        ErrorGlobalInterrupt = 0x810c,
        NotificationGlobalInterrupt = 0x8110,
        ErrorInternalInterrupt = 0x8114,
        NotificationInternalInterrupt = 0x8118,
        ErrorInterruptTrigger = 0x811c,
        NotifInterruptTrigger = 0x8120,
        ErrorInternalInterruptCount = 0x8200,
        NotifCmdDoneInterruptCount = 0x8280,
        ErrorInternalInterruptCountIncrement = 0x8300,
        NotificationCmdDoneIntrruptCountIncrement = 0x8304,

        // MLKEM component stubs
        MlkemName = 0x9000,
        MlkemVersion = 0x9008,
        MlkemCtrl = 0x9010,
        MlkemStatus = 0x9014,
        MlkemSeedD = 0x9018,
        MlkemSeedZ = 0x9038,
        MlkemSharedKey = 0x9058,
        MlkemMsg = 0x9080,
        MlkemDecapsKey = 0xA000,
        MlkemEncapsKey = 0xB000,
        MlkemCiphertext = 0xB800,
        KeyvaultMlkemSeedReadControl = 0xC000,
        KeyvaultMlkemSeedReadStatus = 0xC004,
        KeyvaultMlkemMessageReadControl = 0xC008,
        KeyvaultMlkemMessageReadStatus = 0xC00C,
        KeyvaultMlkemSharedkeyWriteControl = 0xC010,
        KeyvaultMlkemSharedkeyWriteStatus = 0xC014,
    }
}
