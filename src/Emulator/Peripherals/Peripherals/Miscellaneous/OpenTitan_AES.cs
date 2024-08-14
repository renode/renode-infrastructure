//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // OpenTitan HMAC AES as per https://docs.opentitan.org/hw/ip/aes/doc/ (16.09.2021)
    public class OpenTitan_AES : BasicDoubleWordPeripheral, IKnownSize, ISideloadableKey
    {
        public OpenTitan_AES(IMachine machine) : base(machine)
        {
            DefineRegisters();
            initializationVector = new ByteArrayWithAccessTracking(this, InitializationVectorLengthInBytes / sizeof(uint), sizeof(uint), "IV");
            initialKeyShare_0 = new ByteArrayWithAccessTracking(this, InitialKeyShareLengthInBytes / sizeof(uint), sizeof(uint), "initialKeyShare_0");
            initialKeyShare_1 = new ByteArrayWithAccessTracking(this, InitialKeyShareLengthInBytes / sizeof(uint), sizeof(uint), "initialKeyShare_1");
            inputData = new ByteArrayWithAccessTracking(this, DataLengthInBytes / sizeof(uint), sizeof(uint), "DATA_IN");
            outputData = new ByteArrayWithAccessTracking(this, DataLengthInBytes / sizeof(uint), sizeof(uint), "DATA_OUT");

            FatalFaultAlert = new GPIO();
            UpdateErrorAlert = new GPIO();

            key = new byte[InitialKeyShareLengthInBytes];
            random = new PseudorandomNumberGenerator();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            FatalFaultAlert.Unset();
            UpdateErrorAlert.Unset();
            readyForInputWrite = true;
            initializationVector.Reset();
            initialKeyShare_0.Reset();
            initialKeyShare_1.Reset();
            inputData.Reset();
            outputData.Reset();
            Array.Clear(key, 0, InitialKeyShareLengthInBytes);
        }

        public GPIO FatalFaultAlert { get; }
        public GPIO UpdateErrorAlert { get; }

        public long Size => 0x1000;

        public bool InputDataReady => inputData.AllDataWritten;

        public IEnumerable<byte> SideloadKey
        {
            set
            {
                sideloadKey = value.ToArray();
            }
        }

        private void DefineRegisters()
        {
            // As the opentitan software often writes 1's to unused register fields they are defined as `IgnoredBits` to avoid flooding the logs
            Registers.AlertTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) UpdateErrorAlert.Blink(); }, name: "recov_ctrl_update_err")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalFaultAlert.Blink(); }, name: "fatal_fault")
                .WithIgnoredBits(2, 30);

            Registers.InitialKeyShare0_0.DefineMany(this, InitialKeyShareLengthInBytes / sizeof(uint), (register, idx) =>
            {
                var part = (uint)idx;
                register.WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) =>
                {
                    if(useSideloadedKey.Value)
                    {
                        this.Log(LogLevel.Noisy, "Ignored write to key_share_0_{0}, sideload is set", part);
                        return;
                    }
                    initialKeyShare_0.SetPart(part, (uint)value);
                    TryPrepareKey();
                }, name: $"key_share_0_{part}");
            });

            Registers.InitialKeyShare1_0.DefineMany(this, InitialKeyShareLengthInBytes / sizeof(uint), (register, idx) =>
            {
                var part = (uint)idx;
                register.WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) =>
                {
                    if(useSideloadedKey.Value)
                    {
                        this.Log(LogLevel.Noisy, "Ignored write to key_share_1_{0}, sideload is set", part);
                        return;
                    }
                    initialKeyShare_1.SetPart(part, (uint)value);
                    TryPrepareKey();
                }, name: $"key_share_1_{part}");
            });

            Registers.InitializationVector_0.DefineMany(this, InitializationVectorLengthInBytes / sizeof(uint), (register, idx) =>
            {
                var part = (uint)idx;
                register.WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) => initializationVector.SetPart(part, (uint)value), name: $"iv_{part}");
            });

            Registers.InputData_0.DefineMany(this, DataLengthInBytes / sizeof(uint), (register, idx) =>
            {
                var part = (uint)idx;
                register.WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) =>
                {
                    inputData.SetPart(part, (uint)value);
                    if(inputData.AllDataWritten && !manualOperation.Value)
                    {
                        ProcessInput();
                    }
                }, name: $"data_in_{part}");
            });

            Registers.OutputData_0.DefineMany(this, DataLengthInBytes / sizeof(uint), (register, idx) =>
            {
                var part = (uint)idx;
                register.WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                {
                    var val = outputData.GetPartAsDoubleWord(part);
                    if(outputData.AllDataRead)
                    {
                        readyForInputWrite = true;
                        outputValid.Value = false;
                    }
                    return val;
                }, name: $"data_out_{part}");
            });

            Registers.Control.Define(this, 0xc0)
                .WithEnumField<DoubleWordRegister, DecryptionMode>(0, 2, out decryptionMode, name: "OPERATION")
                .WithEnumField<DoubleWordRegister, OperationMode>(2, 6, out operationMode, name: "MODE")
                .WithEnumField<DoubleWordRegister, KeyLength>(8, 3, out keyLength, name: "KEY_LEN")
                .WithFlag(11, out useSideloadedKey, name: "SIDELOAD")
                .WithTag("PRNG_RESEED_RATE", 12, 3)
                .WithFlag(15, out manualOperation, name: "MANUAL_OPERATION")
                .WithTaggedFlag("FORCE_ZERO_MASKS", 16)
                .WithIgnoredBits(17, 15)
                .WithWriteCallback((_, val) =>
                {
                    this.Log(LogLevel.Debug, "New configuration:\n\tOPERATION = {0}\n\tMODE = {1}\n\tKEY_LEN = {2}\n\tSIDELOAD = {3}\n\tMANUAL_OPERATION = {4}",
                             decryptionMode.Value, operationMode.Value, keyLength.Value, useSideloadedKey.Value, manualOperation.Value);
                });

            Registers.AuxiliaryControl.Define(this, 0x1)
                .WithTaggedFlag("KEY_TOUCH_FORCES_RESEED", 0)
                .WithReservedBits(1, 31);

            Registers.AuxiliaryControlWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("CTRL_AUX_REGWEN", 0)
                .WithReservedBits(1, 31);

            Registers.Trigger.Define(this, 0xe)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) =>
                          {
                              if(val && manualOperation.Value)
                              {
                                  ProcessInput();
                              }
                              else
                              {
                                  this.Log(LogLevel.Warning, "Received the 'START' trigger while not in manual mode - ignoring");
                              }
                          }, name: "START")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            this.Log(LogLevel.Debug, "Randomizing input registers ('IV', 'INPUT_DATA', 'KEY_SHARE_')");
                            initializationVector.SetArrayTo(GetRandomArrayOfLength(InitializationVectorLengthInBytes), trackAccess: false);
                            initialKeyShare_0.SetArrayTo(GetRandomArrayOfLength(InitialKeyShareLengthInBytes), trackAccess: false);
                            initialKeyShare_1.SetArrayTo(GetRandomArrayOfLength(InitialKeyShareLengthInBytes), trackAccess: false);
                            inputData.SetArrayTo(GetRandomArrayOfLength(DataLengthInBytes), trackAccess: false);
                        }
                    }, name: "KEY_IV_DATA_IN_CLEAR")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            this.Log(LogLevel.Debug, "Randomizing output registers");
                            outputData.SetArrayTo(GetRandomArrayOfLength(DataLengthInBytes), trackAccess: false);
                        }
                    }, name: "DATA_OUT_CLEAR")
                .WithTaggedFlag("PRNG_RESEED", 3)
                .WithIgnoredBits(4, 28);

            Registers.Status.Define(this, 0x1)
                .WithFlag(0, out statusIdle, FieldMode.Read, name: "IDLE")
                .WithTaggedFlag("STALL", 1)
                .WithTaggedFlag("OUTPUT_LOST", 2)
                .WithFlag(3, out outputValid, FieldMode.Read, name: "OUTPUT_VALID")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => readyForInputWrite && !manualOperation.Value, name: "INPUT_READY")
                .WithTaggedFlag("ALERT_RECOV_CTRL_UPDATE_ERR", 5)
                .WithTaggedFlag("ALERT_FATAL_FAULT", 6)
                .WithIgnoredBits(7, 25);
        }

        private byte[] GetRandomArrayOfLength(int lengthInBytes)
        {
            var randomized = new byte[lengthInBytes];
            random.NextBytes(randomized);
            return randomized;
        }

        private void TryPrepareKey()
        {
            if(initialKeyShare_0.AllDataWritten && initialKeyShare_1.AllDataWritten)
            {
                var key0 = initialKeyShare_0.RetriveData(trackAccess: false);
                var key1 = initialKeyShare_1.RetriveData(trackAccess: false);

                for(var i = 0; i < InitialKeyShareLengthInBytes; i++)
                {
                    key[i] = (byte)(((int)key0[i]) ^ ((int)key1[i]));
                }
            }
        }

        private bool ConfigureAES()
        {
            switch(keyLength.Value)
            {
                case KeyLength.Bits128:
                    aes.KeySize = 128;
                    break;
                case KeyLength.Bits192:
                    aes.KeySize = 192;
                    break;
                case KeyLength.Bits256:
                    aes.KeySize = 256;
                    break;
                default:
                    // This kind of misconfiguration is possible, behaviour is undefined
                    this.Log(LogLevel.Error, "Invalid KEY_LEN size: '0x{0:x}'. Undefined behavior ahead!", keyLength.Value);
                    return false;
            }
            aes.Key = useSideloadedKey.Value ? sideloadKey : key;
#if DEBUG
            this.Log(LogLevel.Debug, "Generated key: {0}", Misc.Stringify(key, " "));
#endif
            switch(operationMode.Value)
            {
                case OperationMode.ElectronicCodebook:
                    aes.Mode = CipherMode.ECB;
                    break;
                default:
                    this.Log(LogLevel.Error, "Encryption Mode {0} is not supported yet", operationMode.Value);
                    return false;
            }
            return true;
        }

        private bool TransformData()
        {
            var output = new byte[DataLengthInBytes];
            var transformMethod = decryptionMode.Value == DecryptionMode.Decryption ? aes.CreateDecryptor() : aes.CreateEncryptor();
            var data = inputData.RetriveData(trackAccess: false);
            int bytesProcessed = 0;

            if(data.Length == 0)
            {
                this.Log(LogLevel.Error, "Started transforming data before full input was available. No output will be generated");
                return false;
            }
            // This is a workaround for the problem with zero bytes transformation on first call while decrypting;
            // doesn't happen while encrypting and found no plausible explanation
            while(bytesProcessed == 0)
            {
                bytesProcessed = transformMethod.TransformBlock(data, 0, DataLengthInBytes, output, 0);
            }
            outputData.SetArrayTo(output, trackAccess: false);
            this.Log(LogLevel.Debug, "Generated 'DATA_OUT' : {0}", Misc.Stringify(output, " "));
            return true;
        }

        private void ProcessInput()
        {
            var configurationOk = ConfigureAES();
            if(!configurationOk)
            {
                return;
            }

            var transformationSucessfull = TransformData();
            if(transformationSucessfull)
            {
                outputValid.Value = true;
            }
        }

        private readonly byte[] key;
        private byte[] sideloadKey;
        private bool readyForInputWrite;
        private readonly ByteArrayWithAccessTracking initialKeyShare_0;
        private readonly ByteArrayWithAccessTracking initialKeyShare_1;
        private readonly ByteArrayWithAccessTracking initializationVector;
        private readonly ByteArrayWithAccessTracking inputData;
        private readonly ByteArrayWithAccessTracking outputData;

        private IFlagRegisterField manualOperation;
        private IFlagRegisterField statusIdle;
        private IFlagRegisterField outputValid;
        private IFlagRegisterField useSideloadedKey;
        private IEnumRegisterField<DecryptionMode> decryptionMode;
        private IEnumRegisterField<OperationMode> operationMode;
        private IEnumRegisterField<KeyLength> keyLength;

// > warning SYSLIB0021: 'AesManaged' is obsolete: 'Derived cryptographic types are obsolete. Use the Create method on the base type instead.'
// Replacing with `Aes.Create` isn't straightforward as it breaks serialization on Windows. Let's just hush it.
#pragma warning disable SYSLIB0021
        private readonly AesManaged aes = new AesManaged();
#pragma warning restore SYSLIB0021

        private readonly PseudorandomNumberGenerator random;

        private const int InitialKeyShareLengthInBytes = 32;
        private const int InitializationVectorLengthInBytes = 16;
        private const int DataLengthInBytes = 16;

        private enum OperationMode
        {
            ElectronicCodebook = 0b000001,  // ECB mode
            CipherBlockChaining = 0b000010, // CBC mode
            CipherFeedback = 0b000100,      // CFB mode
            OutputFeedback = 0b001000,      // OFB mode
            Counter = 0b010000,             // CTR mode
        }

        private enum DecryptionMode
        {
            Encryption = 0x1,
            Decryption = 0x2,
        }

        private enum KeyLength
        {
            Bits128 = 0b001,
            Bits192 = 0b010,
            Bits256 = 0b100,
        }

        public enum Registers
        {
            AlertTest = 0x0,
            InitialKeyShare0_0 = 0x4,
            InitialKeyShare0_1 = 0x8,
            InitialKeyShare0_2 = 0xc,
            InitialKeyShare0_3 = 0x10,
            InitialKeyShare0_4 = 0x14,
            InitialKeyShare0_5 = 0x18,
            InitialKeyShare0_6 = 0x1c,
            InitialKeyShare0_7 = 0x20,
            InitialKeyShare1_0 = 0x24,
            InitialKeyShare1_1 = 0x28,
            InitialKeyShare1_2 = 0x2c,
            InitialKeyShare1_3 = 0x30,
            InitialKeyShare1_4 = 0x34,
            InitialKeyShare1_5 = 0x38,
            InitialKeyShare1_6 = 0x3c,
            InitialKeyShare1_7 = 0x40,
            InitializationVector_0 = 0x44,
            InitializationVector_1 = 0x48,
            InitializationVector_2 = 0x4c,
            InitializationVector_3 = 0x50,
            InputData_0 = 0x54,
            InputData_1 = 0x58,
            InputData_2 = 0x5c,
            InputData_3 = 0x60,
            OutputData_0 = 0x64,
            OutputData_1 = 0x68,
            OutputData_2 = 0x6c,
            OutputData_3 = 0x70,
            Control = 0x74,
            AuxiliaryControl = 0x78,
            AuxiliaryControlWriteEnable = 0x7C,
            Trigger = 0x80,
            Status = 0x84,
        }
    }
}
