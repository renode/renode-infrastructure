//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_KeyManager : BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_KeyManager(Machine machine, string revisionSeed, int randomSeed = 0) : base(machine)
        {
            OperationDoneIRQ = new GPIO();
            internalKey = new byte[KeySize];
            random = new Random(randomSeed);
            sealingSoftwareBinding = new byte[MultiRegistersCount * 4];
            attestationSoftwareBinding = new byte[MultiRegistersCount * 4];
            salt = new byte[MultiRegistersCount * 4];
            softwareShareOutput = new byte[NumberOfSoftwareShareOutputs][];
            for(var i = 0; i < NumberOfSoftwareShareOutputs; ++i)
            {
                softwareShareOutput[i] = new byte[MultiRegistersCount * 4];
            }

            try
            {
                this.revisionSeed = ParseHexstring(revisionSeed).ToArray();
            }
            catch(FormatException)
            {
                throw new ConstructionException($"Could not parse `revisionSeed`: Expected hexstring, got: \"{revisionSeed}\"");
            }
            if(this.revisionSeed.Length != RevisionSeedExpectedLength)
            {
                throw new ConstructionException($"Expected `revisionSeed`'s size is {RevisionSeedExpectedLength} bytes, got {this.revisionSeed.Length}");
            }

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();

            Array.Clear(internalKey, 0, internalKey.Length);
            Array.Clear(sealingSoftwareBinding, 0, sealingSoftwareBinding.Length);
            Array.Clear(attestationSoftwareBinding, 0, attestationSoftwareBinding.Length);
        }

        public long Size => 0x1000;

        public IEnumerable<byte> InternalKey => internalKey;

        public GPIO OperationDoneIRQ { get; }

        private static void SetBytesFromDoubleWord(byte[] array, uint value, int startIndex)
        {
            foreach(var b in BitConverter.GetBytes(value))
            {
                array[startIndex++] = b;
            }
        }

        private static IEnumerable<byte> ParseHexstring(string value)
        {
            var chars = value.AsEnumerable(); 
            if(value.StartsWith("0x"))
            {
                chars = chars.Skip(2);
            }
            var i = value.Length % 2;
            return chars.GroupBy(c => i++ / 2).Select(c => byte.Parse(string.Join("", c), NumberStyles.HexNumber));
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
                        // Requires KMAC
                        this.Log(LogLevel.Warning, "Unsupported GenerateID operation");
                    }
                    break;
                case OperationMode.GenerateHWOutput:
                    if(CheckLegality(IllegalForGenerate))
                    {
                        // Requires KMAC
                        this.Log(LogLevel.Warning, "Unsupported GenerateHWOutput operation");
                    }
                    break;
                case OperationMode.GenerateSWOutput:
                    if(CheckLegality(IllegalForGenerate))
                    {
                        // Requires KMAC
                        this.Log(LogLevel.Warning, "Unsupported GenerateSWOutput operation");
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

        private void DoTransitionActions()
        {
            switch(state.Value)
            {
                case WorkingState.Init:
                    PopulateInternalKey();
                    break;
                    // TODO: Add Calculating next working state/internalKey
                    // Requires KMAC
                default:
                    this.Log(LogLevel.Warning, "Reached unimplemented state: {0}", state.Value);
                    break;
            }
        }

        private void PopulateInternalKey()
        {
            random.NextBytes(internalKey);
        }

        private void HandleIllegalOperation()
        {
            status.Value = Status.DoneError;
            invalidOperationFlag.Value = true;
        }

        private void AdvanceState(WorkingState? nextState = null)
        {
            if(nextState.HasValue)
            {
                state.Value = nextState.Value;
            }
            else
            {
                if(state.Value != WorkingState.Disabled)
                {
                    state.Value++;
                    DoTransitionActions();
                }
                else
                {
                    //TODO: Update hardware outputs and keys with randomly computed values
                    for(var i = 0; i < NumberOfSoftwareShareOutputs; ++i)
                    {
                        random.NextBytes(softwareShareOutput[i]);
                    }
                    PopulateInternalKey();
                }
            }

            this.Log(LogLevel.Debug, "WorkingState advanced to '{0}'", state.Value);
            status.Value = Status.Idle;
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
                .WithTaggedFlag("fatal_fault_err", 0) // FieldMode.Write
                .WithTaggedFlag("recov_operation_err", 1) // FieldMode.Write
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
                register.WithValueField(0, 32, writeCallback: (_, val) => { SetBytesFromDoubleWord(sealingSoftwareBinding, val, idx * 4); }, valueProviderCallback: _ => (uint)BitConverter.ToInt32(sealingSoftwareBinding, idx * 4), name: $"VAL_{idx}");
            });

            Registers.AttestationSoftwareBinding0.DefineMany(this, MultiRegistersCount, (register, idx) =>
            {
                register.WithValueField(0, 32, writeCallback: (_, val) => { SetBytesFromDoubleWord(attestationSoftwareBinding, val, idx * 4); }, valueProviderCallback: _ => (uint)BitConverter.ToInt32(attestationSoftwareBinding, idx * 4), name: $"VAL_{idx}");
            });

            Registers.Salt0.DefineMany(this, MultiRegistersCount, (register, idx) =>
            {
                register.WithValueField(0, 32, writeCallback: (_, val) => { SetBytesFromDoubleWord(salt, val, idx * 4); }, valueProviderCallback: _ => (uint)BitConverter.ToInt32(salt, idx * 4), name: $"VAL_{idx}");
            });

            Registers.KeyVersion.Define(this)
                .WithTag("VAL", 0, 32);

            Registers.MaxCreatorKeyVersionWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("EN", 0) // FieldMode.Read | FieldMode.WriteZeroToClear
                .WithIgnoredBits(1, 31);

            Registers.MaxCreatorKeyVersion.Define(this)
                .WithTag("VAL", 0, 32);

            Registers.MaxOwnerIntermediateKeyVersionWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("EN", 0) // FieldMode.Read | FieldMode.WriteZeroToClear
                .WithIgnoredBits(1, 31);

            Registers.MaxOwnerIntermediateKeyVersion.Define(this)
                .WithTag("VAL", 0, 32);

            Registers.MaxOwnerKeyVersionWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("EN", 0) // FieldMode.Read | FieldMode.WriteZeroToClear
                .WithIgnoredBits(1, 31);

            Registers.MaxOwnerKeyVersion.Define(this)
                .WithTag("VAL", 0, 32);

            for(var i = 0; i < NumberOfSoftwareShareOutputs; ++i)
            {
                var offset = Registers.SoftwareShare1Output0 - Registers.SoftwareShare0Output0;
                (Registers.SoftwareShare0Output0 + offset * i).DefineMany(this, MultiRegistersCount, (register, idx) =>
                {
                    register.WithValueField(0, 32, FieldMode.ReadToClear, valueProviderCallback: _ =>
                    {
                        var value = (uint)BitConverter.ToInt32(softwareShareOutput[i], idx * 4);
                        SetBytesFromDoubleWord(softwareShareOutput[i], 0, idx * 4);
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
                .WithTaggedFlag("INVALID_KMAC_INPUT", 1)    // FieldMode.Read | FieldMode.WriteOneToClear
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

        private IFlagRegisterField interruptStatusOperationDone;
        private IFlagRegisterField interruptEnableOperationDone;
        private IEnumRegisterField<CDISetting> cdiSetting;
        private IEnumRegisterField<Destination> destination;
        private IEnumRegisterField<OperationMode> operationMode;
        private IEnumRegisterField<Status> status;
        private IEnumRegisterField<WorkingState> state;
        private IFlagRegisterField invalidOperationFlag;
        private IValueRegisterField entropyReseedInterval;

        private readonly byte[] sealingSoftwareBinding;
        private readonly byte[] attestationSoftwareBinding;
        private readonly byte[] salt;
        private readonly byte[][] softwareShareOutput;
        private readonly byte[] internalKey;
        private readonly byte[] revisionSeed;
        private readonly Random random;

        private const int MultiRegistersCount = 8;
        private const int KeySize = 32;
        private const int RevisionSeedExpectedLength = 256 / 8;
        private const int NumberOfSoftwareShareOutputs = 2;

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
