//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.MemoryControllers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_KeyManager : BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_KeyManager(IMachine machine, OpenTitan_ROMController romController,
            string deviceId, string lifeCycleDiversificationConstant, string creatorKey, string ownerKey, string rootKey,
            string softOutputSeed, string hardOutputSeed, string destinationNoneSeed, string destinationAesSeed, string destinationOtbnSeed, string destinationKmacSeed,
            string revisionSeed, string creatorIdentitySeed, string ownerIntermediateIdentitySeed, string ownerIdentitySeed,
            bool kmacEnableMasking = true, int randomSeed = 0, ISideloadableKey kmac = null, ISideloadableKey aes = null, ISideloadableKey otbn = null) : base(machine)
        {
            this.romController = romController;
            destinations = new Dictionary<Destination, ISideloadableKey>();
            if(kmac != null)
            {
                destinations.Add(Destination.KMAC, kmac);
            }
            if(aes != null)
            {
                destinations.Add(Destination.AES, aes);
            }
            if(otbn != null)
            {
                destinations.Add(Destination.OTBN, otbn);
            }
            OperationDoneIRQ = new GPIO();
            FatalAlert = new GPIO();
            RecoverableAlert = new GPIO();

            random = new Random(randomSeed);
            sealingSoftwareBinding = new byte[MultiRegistersCount * 4];
            attestationSoftwareBinding = new byte[MultiRegistersCount * 4];
            salt = new byte[MultiRegistersCount * 4];
            softwareShareOutput = new byte[MultiRegistersCount * 4 * NumberOfSoftwareShareOutputs];

            this.deviceId = ConstructorParseHexstringArgument("deviceId", deviceId, DeviceIdExpectedLength); // OTP_HW_CFG_DATA_DEFAULT.device_id
            this.lifeCycleDiversificationConstant = ConstructorParseHexstringArgument("lifeCycleDiversificationConstant", lifeCycleDiversificationConstant, LifeCycleDiversificationConstantLength); // RndCnstLcKeymgrDiv
            this.creatorKey = ConstructorParseHexstringArgument("creatorKey", creatorKey, CreatorKeyExpectedLength); // KEYMGR_FLASH_DEFAULT.seeds[CreatorSeedIdx]
            this.ownerKey = ConstructorParseHexstringArgument("ownerKey", ownerKey, OwnerKeyExpectedLength); // KEYMGR_FLASH_DEFAULT.seeds[OwnerSeedIdx]
            var rootKeyTemp = ConstructorParseHexstringArgument("rootKey", rootKey, RootKeyExpectedLength); // OTP_KEYMGR_KEY_DEFAULT
            // If `KmacEnMasking` is set then key is composed of both shares,
            // otherwise the first key share is a xor of shares and the second key share is zero 
            if(kmacEnableMasking)
            {
                this.rootKey = rootKeyTemp;
            }
            else
            {
                this.rootKey = rootKeyTemp
                    .Take(rootKeyTemp.Length / 2)
                    .Zip(rootKeyTemp.Skip(rootKeyTemp.Length / 2), (b0, b1) => (byte)(b0 ^ b1))
                    .Concat(Enumerable.Repeat((byte)0, rootKeyTemp.Length / 2))
                    .ToArray();
            }
            this.softOutputSeed = ConstructorParseHexstringArgument("softOutputSeed", softOutputSeed, SeedExpectedLength); // RndCnstSoftOutputSeed
            this.hardOutputSeed = ConstructorParseHexstringArgument("hardOutputSeed", hardOutputSeed, SeedExpectedLength); // RndCnstHardOutputSeed
            this.destinationNoneSeed = ConstructorParseHexstringArgument("destinationNoneSeed", destinationNoneSeed, SeedExpectedLength); // RndCnstAesSeed
            this.destinationAesSeed = ConstructorParseHexstringArgument("destinationAesSeed", destinationAesSeed, SeedExpectedLength); // RndCnstKmacSeed
            this.destinationOtbnSeed = ConstructorParseHexstringArgument("destinationOtbnSeed", destinationOtbnSeed, SeedExpectedLength); // RndCnstOtbnSeed
            this.destinationKmacSeed = ConstructorParseHexstringArgument("destinationKmacSeed", destinationKmacSeed, SeedExpectedLength); // RndCnstNoneSeed
            this.revisionSeed = ConstructorParseHexstringArgument("revisionSeed", revisionSeed, SeedExpectedLength); // RndCnstRevisionSeed
            this.creatorIdentitySeed = ConstructorParseHexstringArgument("creatorIdentitySeed", creatorIdentitySeed, SeedExpectedLength); // RndCnstCreatorIdentitySeed
            this.ownerIntermediateIdentitySeed = ConstructorParseHexstringArgument("ownerIntermediateIdentitySeed", ownerIntermediateIdentitySeed, SeedExpectedLength); // RndCnstOwnerIntIdentitySeed
            this.ownerIdentitySeed = ConstructorParseHexstringArgument("ownerIdentitySeed", ownerIdentitySeed, SeedExpectedLength); // RndCnstOwnerIdentitySeed

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();

            FatalAlert.Unset();
            RecoverableAlert.Unset();
            Array.Clear(sealingSoftwareBinding, 0, sealingSoftwareBinding.Length);
            Array.Clear(attestationSoftwareBinding, 0, attestationSoftwareBinding.Length);
            Array.Clear(salt, 0, salt.Length);
            Array.Clear(softwareShareOutput, 0, softwareShareOutput.Length);
        }

        public long Size => 0x1000;

        public GPIO OperationDoneIRQ { get; }

        public GPIO FatalAlert { get; }
        public GPIO RecoverableAlert { get; }

        static private byte[] ConstructorParseHexstringArgument(string fieldName, string value, int expectedLength)
        {
            byte[] field;
            var lengthInBytes = value.Length / 2;
            if(lengthInBytes != expectedLength)
            {
                throw new ConstructionException($"Expected `{fieldName}`'s size is {expectedLength} bytes, got {lengthInBytes}");
            }
            try
            {
                field = Misc.HexStringToByteArray(value);
            }
            catch
            {
                throw new ConstructionException($"Could not parse `{fieldName}`: Expected hexstring, got: \"{value}\"");
            }
            return field;
        }

        private static WorkingState[] IllegalForAdvance = { WorkingState.Invalid, WorkingState.Disabled };
        private static WorkingState[] IllegalForDisable = { WorkingState.Invalid, WorkingState.Disabled };
        private static WorkingState[] IllegalForGenerate = { WorkingState.Invalid, WorkingState.Disabled, WorkingState.Init };

        private void RunCommand()
        {
            var command = operationMode.Value;
            this.Log(LogLevel.Debug, "Received command: {0}", command);
            if(state.Value == WorkingState.Reset && command != OperationMode.Advance)
            {
                // In the Reset state no other commands are allowed
                this.Log(LogLevel.Warning, "Ignoring command {0} in the reset state", command);
                return;
            }

            switch(command)
            {
                case OperationMode.Advance:
                    if(CheckLegality(IllegalForAdvance))
                    {
                        AdvanceState();
                    }
                    break;
                case OperationMode.GenerateID:
                    if(CheckLegality(IllegalForGenerate))
                    {
                        softwareShareOutput = CalculateKMAC(IdentitySeed, softwareShareOutput.Length);
                    }
                    break;
                case OperationMode.GenerateHWOutput:
                    if(CheckLegality(IllegalForGenerate) && CheckKeyVersion())
                    {
                        var data = BitConverter.GetBytes((uint)keyVersion.Value)
                            .Concat(salt)
                            .Concat(DestinationCipherSeed)
                            .Concat(hardOutputSeed);
                        var length = destination.Value == Destination.OTBN ? SideloadKeyLengthOTBN : SideloadKeyLength;
                        var output = CalculateKMAC(data, length);
                        if(destinations.TryGetValue(destination.Value, out var dest))
                        {
                            dest.SideloadKey = output;
                        }
                        else
                        {
                            this.Log(LogLevel.Warning, "Sideload key for {0} is not possible, peripheral is not specified", destination.Value);
                        }
                    }
                    break;
                case OperationMode.GenerateSWOutput:
                    if(CheckLegality(IllegalForGenerate) && CheckKeyVersion())
                    {
                        var data = BitConverter.GetBytes((uint)keyVersion.Value)
                            .Concat(salt)
                            .Concat(DestinationCipherSeed)
                            .Concat(softOutputSeed);
                        softwareShareOutput = CalculateKMAC(data, softwareShareOutput.Length);
                    }
                    break;
                case OperationMode.Disable:
                    if(CheckLegality(IllegalForDisable))
                    {
                        state.Value = WorkingState.Disabled;
                    }
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unsupported {0} operation", state.Value);
                    break;
            }
        }

        private bool CheckLegality(WorkingState[] illegalStates)
        {
            if(illegalStates.Contains(state.Value))
            {
                HandleIllegalOperation();
                return false;
            }
            return true;
        }

        private bool CheckKeyVersion()
        {
            if(keyVersion.Value > MaxKeyVersion)
            {
                invalidKmacInputFlag.Value = true;
                return false;
            }
            return true;
        }

        private void Invalidate()
        {
            var key = new byte[SideloadKeyLength];
            foreach(var dest in destinations.Values)
            {
                random.NextBytes(key);
                dest.SideloadKey = key;
            }
            random.NextBytes(softwareShareOutput);
        }

        private void HandleIllegalOperation()
        {
            status.Value = Status.DoneError;
            invalidOperationFlag.Value = true;
        }

        private void AdvanceState()
        {
            IEnumerable<byte> data;

            switch(state.Value)
            {
                case WorkingState.Reset:
                    state.Value++;
                    internalKey = rootKey;
                    break;
                case WorkingState.Init:
                    state.Value++;
                    data = SoftwareBinding
                        .Concat(revisionSeed)
                        .Concat(deviceId)
                        .Concat(lifeCycleDiversificationConstant)
                        .Concat(romController.Digest)
                        .Concat(creatorKey);
                    creatorRootStateKey = CalculateKMAC(data, RootKeyExpectedLength);
                    internalKey = creatorRootStateKey;
                    break;
                case WorkingState.CreatorRootKey:
                    state.Value++;
                    data = SoftwareBinding
                        .Concat(ownerKey);
                    ownerIntermediateStateKey = CalculateKMAC(data, RootKeyExpectedLength);
                    internalKey = ownerIntermediateStateKey;
                    break;
                case WorkingState.OwnerIntermediateKey:
                    state.Value++;
                    data = SoftwareBinding;
                    ownerStateKey = CalculateKMAC(data, RootKeyExpectedLength);
                    internalKey = ownerStateKey;
                    break;
                case WorkingState.OwnerKey:
                    state.Value++;
                    break;
                case WorkingState.Disabled:
                    Invalidate();
                    break;
                case WorkingState.Invalid:
                    // This is a proper state, no additonal logging is required
                    break;
                default:
                    this.Log(LogLevel.Warning, "Reached unexpected state: {0}", state.Value);
                    break;
            }

            this.Log(LogLevel.Debug, "WorkingState advanced to '{0}'", state.Value);
            status.Value = Status.Idle;
        }

        public byte[] CalculateKMAC(IEnumerable<byte> data, int outputLength)
        {
            var mac = new KMac(KmacBitLength, null);
            mac.Init(new KeyParameter(internalKey));
            var dataArray = data.ToArray();
            mac.BlockUpdate(dataArray, 0, dataArray.Length);
            var output = new byte[outputLength];
            mac.DoFinal(output, 0, outputLength);
            return output;
        }

        private void DefineRegisters()
        {
            Registers.InterruptState.Define(this)
                .WithFlag(0, out interruptStatusOperationDone, FieldMode.Read | FieldMode.WriteOneToClear, name: "op_done")
                .WithIgnoredBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out interruptEnableOperationDone, name: "op_done")
                .WithIgnoredBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { interruptStatusOperationDone.Value |= val; }, name: "op_done")
                .WithIgnoredBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.AlertTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) RecoverableAlert.Blink(); }, name: "recov_operation_err") // FieldMode.Write
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name: "fatal_fault_err") // FieldMode.Write
                .WithIgnoredBits(2, 30);

            Registers.ConfigurationWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("EN", 0)   // FieldMode.Read
                .WithIgnoredBits(1, 31);

            Registers.OperationControls.Define(this)
                .WithFlag(0, writeCallback: (_, val) => { if (val) RunCommand(); }, valueProviderCallback: _ => false, name: "START")
                .WithReservedBits(1, 3)
                .WithEnumField<DoubleWordRegister, OperationMode>(4, 3, out operationMode, name: "OPERATION")
                .WithEnumField<DoubleWordRegister, CDISetting>(7, 1, out cdiSetting, name: "CDI_SEL")
                .WithReservedBits(8, 4)
                .WithEnumField<DoubleWordRegister, Destination>(12, 3, out destination, name: "DEST_SEL")
                .WithIgnoredBits(15, 17)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.SideloadClear.Define(this)
                .WithTag("VAL", 0, 3)
                .WithIgnoredBits(3, 29);

            Registers.ReseedIntervalWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("EN", 0) // FieldMode.Read | FieldMode.WriteZeroToClear
                .WithReservedBits(1, 31);

            Registers.ReseedInterval.Define(this, 0x100)
                .WithValueField(0, 32, out entropyReseedInterval, name: "VAL");

            Registers.SoftwareBindingWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("EN", 0) // FieldMode.Read | FieldMode.WriteZeroToClear
                .WithIgnoredBits(1, 31);

            Registers.SealingSoftwareBinding0.DefineMany(this, MultiRegistersCount, (register, idx) =>
            {
                register.WithValueField(0, 32, writeCallback: (_, val) => { sealingSoftwareBinding.SetBytesFromValue((uint)val, idx * 4); }, valueProviderCallback: _ => (uint)BitConverter.ToInt32(sealingSoftwareBinding, idx * 4), name: $"VAL_{idx}");
            });

            Registers.AttestationSoftwareBinding0.DefineMany(this, MultiRegistersCount, (register, idx) =>
            {
                register.WithValueField(0, 32, writeCallback: (_, val) => { attestationSoftwareBinding.SetBytesFromValue((uint)val, idx * 4); }, valueProviderCallback: _ => (uint)BitConverter.ToInt32(attestationSoftwareBinding, idx * 4), name: $"VAL_{idx}");
            });

            Registers.Salt0.DefineMany(this, MultiRegistersCount, (register, idx) =>
            {
                register.WithValueField(0, 32, writeCallback: (_, val) => { salt.SetBytesFromValue((uint)val, idx * 4); }, valueProviderCallback: _ => (uint)BitConverter.ToInt32(salt, idx * 4), name: $"VAL_{idx}");
            });

            Registers.KeyVersion.Define(this)
                .WithValueField(0, 32, out keyVersion, name: "VAL");

            Registers.MaxCreatorKeyVersionWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("EN", 0) // FieldMode.Read | FieldMode.WriteZeroToClear
                .WithIgnoredBits(1, 31);

            Registers.MaxCreatorKeyVersion.Define(this)
                .WithValueField(0, 32, out maxCreatorKeyVersion, name: "VAL");

            Registers.MaxOwnerIntermediateKeyVersionWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("EN", 0) // FieldMode.Read | FieldMode.WriteZeroToClear
                .WithIgnoredBits(1, 31);

            Registers.MaxOwnerIntermediateKeyVersion.Define(this)
                .WithValueField(0, 32, out maxOwnerIntermediateKeyVersion, name: "VAL");

            Registers.MaxOwnerKeyVersionWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("EN", 0) // FieldMode.Read | FieldMode.WriteZeroToClear
                .WithIgnoredBits(1, 31);

            Registers.MaxOwnerKeyVersion.Define(this)
                .WithValueField(0, 32, out maxOwnerKeyVersion, name: "VAL");

            for(var i = 0; i < NumberOfSoftwareShareOutputs; ++i)
            {
                var offset = Registers.SoftwareShare1Output0 - Registers.SoftwareShare0Output0;
                (Registers.SoftwareShare0Output0 + offset * i).DefineMany(this, MultiRegistersCount, (register, idx) =>
                {
                    register.WithValueField(0, 32, FieldMode.ReadToClear, valueProviderCallback: _ =>
                    {
                        var startIndex = i * MultiRegistersCount * 4 + idx * 4;
                        var value = (uint)BitConverter.ToInt32(softwareShareOutput, startIndex);
                        softwareShareOutput.SetBytesFromValue(0, startIndex);
                        return value;
                    }, name: $"VAL_{idx}");
                });
            }

            Registers.WorkingState.Define(this)
                .WithEnumField<DoubleWordRegister, WorkingState>(0, 3, out state, FieldMode.Read, name: "STATE")
                .WithIgnoredBits(3, 29);

            Registers.Status.Define(this)
                .WithEnumField<DoubleWordRegister, Status>(0, 2, out status, FieldMode.Read | FieldMode.WriteOneToClear, name: "STATE")
                .WithIgnoredBits(2, 30);

            Registers.ErrorCode.Define(this)
                .WithFlag(0, out invalidOperationFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "INVALID_OP")
                .WithFlag(1, out invalidKmacInputFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "INVALID_KMAC_INPUT")
                .WithTaggedFlag("INVALID_SHADOW_UPDATE", 2) // FieldMode.Read | FieldMode.WriteOneToClear
                .WithIgnoredBits(3, 29);

            Registers.FaultStatus.Define(this)
                .WithTaggedFlag("CMDA", 0)          // FieldMode.Read
                .WithTaggedFlag("KMAC_FSM", 1)      // FieldMode.Read
                .WithTaggedFlag("KMAC_OPKMAC", 2)   // FieldMode.Read
                .WithTaggedFlag("KMAC_OUTKMAC", 3)  // FieldMode.Read
                .WithTaggedFlag("REGFILE_INTG", 4)  // FieldMode.Read
                .WithTaggedFlag("SHADOW", 5)        // FieldMode.Read
                .WithTaggedFlag("CTRL_FSM_INTG", 6) // FieldMode.Read
                .WithTaggedFlag("CTRL_FSM_CNT", 7)  // FieldMode.Read
                .WithTaggedFlag("RESEED_CNT", 8)    // FieldMode.Read
                .WithTaggedFlag("SIDE_CTRL_FSM", 9) // FieldMode.Read
                .WithReservedBits(10, 22);
        }

        private void UpdateInterrupts()
        {
            OperationDoneIRQ.Set(interruptStatusOperationDone.Value && interruptEnableOperationDone.Value);
        }

        private IEnumerable<byte> SoftwareBinding => cdiSetting.Value == CDISetting.Sealing ? sealingSoftwareBinding : attestationSoftwareBinding;

        private IEnumerable<byte> DestinationCipherSeed
        {
            get
            {
                switch(destination.Value)
                {
                    case Destination.None:
                        return destinationNoneSeed;
                    case Destination.AES:
                        return destinationAesSeed;
                    case Destination.KMAC:
                        return destinationKmacSeed;
                    case Destination.OTBN:
                        return destinationOtbnSeed;
                    default:
                        this.Log(LogLevel.Error, "Invalid state, destination's value is 0x{0:X}", destination.Value);
                        return Enumerable.Empty<byte>();
                }
            }
        }

        private IEnumerable<byte> IdentitySeed
        {
            get
            {
                switch(state.Value)
                {
                    case WorkingState.CreatorRootKey:
                        return creatorIdentitySeed;
                    case WorkingState.OwnerIntermediateKey:
                        return ownerIntermediateIdentitySeed;
                    case WorkingState.OwnerKey:
                    default:
                        this.Log(LogLevel.Error, "Invalid state for getting `IdentitySeed`, state's value is 0x{0:X}", state.Value);
                        return Enumerable.Empty<byte>();
                }
            }
        }

        private uint MaxKeyVersion
        {
            get
            {
                switch(state.Value)
                {
                    case WorkingState.CreatorRootKey:
                        return (uint)maxCreatorKeyVersion.Value;
                    case WorkingState.OwnerIntermediateKey:
                        return (uint)maxOwnerIntermediateKeyVersion.Value;
                    case WorkingState.OwnerKey:
                        return (uint)maxOwnerKeyVersion.Value;
                    default:
                        this.Log(LogLevel.Error, "Invalid state for getting `MaxKeyVersion`, state's value is 0x{0:X}", state.Value);
                        return 0;
                }
            }
        }

        private IFlagRegisterField interruptStatusOperationDone;
        private IFlagRegisterField interruptEnableOperationDone;
        private IEnumRegisterField<CDISetting> cdiSetting;
        private IEnumRegisterField<Destination> destination;
        private IEnumRegisterField<OperationMode> operationMode;
        private IEnumRegisterField<Status> status;
        private IEnumRegisterField<WorkingState> state;
        private IFlagRegisterField invalidOperationFlag;
        private IFlagRegisterField invalidKmacInputFlag;
        private IValueRegisterField entropyReseedInterval;
        private IValueRegisterField keyVersion;
        private IValueRegisterField maxCreatorKeyVersion;
        private IValueRegisterField maxOwnerIntermediateKeyVersion;
        private IValueRegisterField maxOwnerKeyVersion;
        private byte[] creatorRootStateKey;
        private byte[] ownerIntermediateStateKey;
        private byte[] ownerStateKey;
        private byte[] softwareShareOutput;
        private byte[] internalKey;

        private readonly byte[] sealingSoftwareBinding;
        private readonly byte[] attestationSoftwareBinding;
        private readonly byte[] salt;
        private readonly byte[] revisionSeed;
        private readonly byte[] deviceId;
        private readonly byte[] lifeCycleDiversificationConstant;
        private readonly byte[] creatorKey;
        private readonly byte[] ownerKey;
        private readonly byte[] rootKey;
        private readonly byte[] softOutputSeed;
        private readonly byte[] hardOutputSeed;
        private readonly byte[] destinationNoneSeed;
        private readonly byte[] destinationAesSeed;
        private readonly byte[] destinationOtbnSeed;
        private readonly byte[] destinationKmacSeed;
        private readonly byte[] creatorIdentitySeed;
        private readonly byte[] ownerIntermediateIdentitySeed;
        private readonly byte[] ownerIdentitySeed;
        private readonly Random random;
        private readonly OpenTitan_ROMController romController;
        private readonly Dictionary<Destination, ISideloadableKey> destinations;

        private const int MultiRegistersCount = 8;
        private const int KmacBitLength = 256;
        private const int SeedExpectedLength = 256 / 8;
        private const int DeviceIdExpectedLength = 256 / 8;
        private const int LifeCycleDiversificationConstantLength = 128 / 8;
        private const int CreatorKeyExpectedLength = 256 / 8;
        private const int OwnerKeyExpectedLength = 256 / 8;
        private const int RootKeyExpectedLength = 256 * 2 / 8;
        private const int NumberOfSoftwareShareOutputs = 2;
        private const int SideloadKeyLength = 256 / 8;
        private const int SideloadKeyLengthOTBN = 384 / 8;

        public enum OperationMode
        {
            Advance = 0,
            GenerateID = 1,
            GenerateSWOutput = 2,
            GenerateHWOutput = 3,
            Disable = 4,
        }

        public enum WorkingState
        {
            Reset = 0,
            Init = 1,
            CreatorRootKey = 2,
            OwnerIntermediateKey = 3,
            OwnerKey = 4,
            Disabled = 5,
            Invalid = 6,
        }

        public enum Registers
        {
            InterruptState = 0x0,
            InterruptEnable = 0x4,
            InterruptTest = 0x8,
            AlertTest = 0xc,
            ConfigurationWriteEnable = 0x10,
            OperationControls = 0x14,
            SideloadClear = 0x18,
            ReseedIntervalWriteEnable = 0x1c,
            ReseedInterval = 0x20,
            SoftwareBindingWriteEnable = 0x24,
            SealingSoftwareBinding0 = 0x28,
            SealingSoftwareBinding1 = 0x2c,
            SealingSoftwareBinding2 = 0x30,
            SealingSoftwareBinding3 = 0x34,
            SealingSoftwareBinding4 = 0x38,
            SealingSoftwareBinding5 = 0x3c,
            SealingSoftwareBinding6 = 0x40,
            SealingSoftwareBinding7 = 0x44,
            AttestationSoftwareBinding0 = 0x48,
            AttestationSoftwareBinding1 = 0x4c,
            AttestationSoftwareBinding2 = 0x50,
            AttestationSoftwareBinding3 = 0x54,
            AttestationSoftwareBinding4 = 0x58,
            AttestationSoftwareBinding5 = 0x5c,
            AttestationSoftwareBinding6 = 0x60,
            AttestationSoftwareBinding7 = 0x64,
            Salt0 = 0x68,
            Salt1 = 0x6c,
            Salt2 = 0x70,
            Salt3 = 0x74,
            Salt4 = 0x78,
            Salt5 = 0x7c,
            Salt6 = 0x80,
            Salt7 = 0x84,
            KeyVersion = 0x88,
            MaxCreatorKeyVersionWriteEnable = 0x8c,
            MaxCreatorKeyVersion = 0x90,
            MaxOwnerIntermediateKeyVersionWriteEnable = 0x94,
            MaxOwnerIntermediateKeyVersion = 0x98,
            MaxOwnerKeyVersionWriteEnable = 0x9c,
            MaxOwnerKeyVersion = 0xa0,
            SoftwareShare0Output0 = 0xa4,
            SoftwareShare0Output1 = 0xa8,
            SoftwareShare0Output2 = 0xac,
            SoftwareShare0Output3 = 0xb0,
            SoftwareShare0Output4 = 0xb4,
            SoftwareShare0Output5 = 0xb8,
            SoftwareShare0Output6 = 0xbc,
            SoftwareShare0Output7 = 0xc0,
            SoftwareShare1Output0 = 0xc4,
            SoftwareShare1Output1 = 0xc8,
            SoftwareShare1Output2 = 0xcc,
            SoftwareShare1Output3 = 0xd0,
            SoftwareShare1Output4 = 0xd4,
            SoftwareShare1Output5 = 0xd8,
            SoftwareShare1Output6 = 0xdc,
            SoftwareShare1Output7 = 0xe0,
            WorkingState = 0xe4,
            Status = 0xe8,
            ErrorCode = 0xec,
            FaultStatus = 0xf0,
        }

        private enum CDISetting
        {
            Sealing = 0,
            Attestation = 1,
        }

        private enum Destination
        {
            None = 0,
            AES = 1,
            KMAC = 2,
            OTBN = 3,
        }

        private enum SideloadClear
        {
            None = 0,
            AES = 1,
            KMAC = 2,
            OTBN = 3,
        }

        private enum Status
        {
            Idle = 0,
            WIP = 1,
            DoneSuccess = 2,
            DoneError = 3,
        }
    }
}
