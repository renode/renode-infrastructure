//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Prng.Drbg;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_CSRNG: BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_CSRNG(IMachine machine, OpenTitan_EntropySource entropySource) : base(machine)
        {
            this.entropySource = entropySource;

            DefineRegisters();

            RequestCompletedIRQ = new GPIO();
            EntropyeRequestedIRQ = new GPIO();
            HardwareInstanceIRQ = new GPIO();
            FatalErrorIRQ = new GPIO();
            RecoverableAlert = new GPIO();
            FatalAlert = new GPIO();

            WorkingMode = RandomType.HardwareCompliant;
            generatedBitsFifo = new Queue<uint>();

            seed = new uint[12];
            fakeEntropy = new FakeEntropy(DefaultSeedSizeInBytes);
            internalStateReadFifo = new Queue<uint>();
            generatedBitsFifo = new Queue<uint>();
            appendedData = new List<uint>();
            Reset();
        }

        public bool RequestData(out uint result)
        {
            if(generatedBitsFifo.TryDequeue(out result))
            {
                return generatedBitsFifo.Count != 0;
            }
            else
            {
                this.Log(LogLevel.Warning, "Trying to read from empty FIFO");
                return false;
            }
        }

        public void EdnSoftwareCommandRequestWrite(uint writeValue)
        {
            HandleCommandRequestWrite(writeValue);
        }

        public override void Reset()
        {
            appendedDataCount = 0;
            appendedData.Clear();
            generatedBitsFifo.Clear();
            internalStateReadFifo.Clear();
            base.Reset();
            RecoverableAlert.Unset();
            FatalAlert.Unset();

            InstantiateRandom();

            readyFlag.Value = true;
        }

        private void ReinstantiateInternal()
        {
            drbgEngine = new CtrSP800Drbg(new AesEngine(), keySizeInBits: 256, securityStrength: 256, entropySource: fakeEntropy,
                                          personalizationString: null, nonce: null, withDerivationFuction: false);
        }

        private void DefineRegisters()
        {
            Registers.InterruptState.Define(this)
                .WithFlag(0, out requestCompletedInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "cs_cmd_req_done")
                .WithFlag(1, out entropyRequestInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "cs_entropy_req")
                .WithFlag(2, out hardwareInstanceInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "cs_hw_inst_exc")
                .WithFlag(3, out fatalErrorInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "cs_fatal_err")
                .WithReservedBits(4, 32 - 4)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out requestCompletedInterruptEnabled, FieldMode.Read | FieldMode.WriteOneToClear, name: "cs_cmd_req_done")
                .WithFlag(1, out entropyRequestInterruptEnabled, FieldMode.Read | FieldMode.WriteOneToClear, name: "cs_entropy_req")
                .WithFlag(2, out hardwareInstanceInterruptEnabled, FieldMode.Read | FieldMode.WriteOneToClear, name: "cs_hw_inst_exc")
                .WithFlag(3, out fatalErrorInterruptEnabled, FieldMode.Read | FieldMode.WriteOneToClear, name: "cs_fatal_err")
                .WithReservedBits(4, 32 - 4)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) requestCompletedInterrupt.Value = true; }, name: "cs_cmd_req_done")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) entropyRequestInterrupt.Value = true; }, name: "cs_entropy_req")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, val) => { if(val) hardwareInstanceInterrupt.Value = true; }, name: "cs_hw_inst_exc")
                .WithFlag(3, FieldMode.Write, writeCallback: (_, val) => { if(val) fatalErrorInterrupt.Value = true; }, name: "cs_fatal_err")
                .WithReservedBits(4, 32 - 4)
                .WithWriteCallback((_, val) => { if(val != 0) UpdateInterrupts(); });
            Registers.AlertTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) RecoverableAlert.Blink(); }, name: "recov_alert")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name: "fatal_alert")
                .WithReservedBits(2, 30);
            Registers.RegisterWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("REGWEN", 0)
                .WithReservedBits(1, 31);
            Registers.Control.Define(this, 0x999)
                .WithEnumField<DoubleWordRegister, MultiBitBool4>(0, 4, out enabled, name: "ENABLE")
                .WithEnumField<DoubleWordRegister, MultiBitBool4>(4, 4, out genbitsReadEnabled, name: "SW_APP_ENABLE")
                .WithTag("READ_INT_STATE", 8, 4)
                .WithReservedBits(12, 20);
            Registers.CommandRequest.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => HandleCommandRequestWrite((uint)val), name: "CMD_REQ");
            Registers.CommandStatus.Define(this, 0x1)
                .WithFlag(0, out readyFlag, FieldMode.Read, name: "CMD_RDY")
                .WithFlag(1, out requestFailedFlag, FieldMode.Read, name: "CMD_STS")
                .WithReservedBits(2, 30);
            Registers.GenerateBitsValid.Define(this)
                .WithFlag(0, out generatedValidFlag, FieldMode.Read, name: "GENBITS_VLD")
                .WithFlag(1, out fipsCompliant, FieldMode.Read, name: "GENBITS_FIPS")
                .WithReservedBits(2, 30);
            Registers.GenerateBits.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) =>
                {
                    if(genbitsReadEnabled.Value == MultiBitBool4.False)
                    {
                        this.Log(LogLevel.Error, "Trying to read the generatedBitsFifo when 'SW_APP_ENABLE' is not set to true");
                        return 0;
                    }

                    if(generatedBitsFifo.TryDequeue(out var value))
                    {
                        return value;
                    }
                    this.Log(LogLevel.Warning, "Trying to get an value when the fifo with generated bits is empty. Generate lenght mismatch?");
                    return 0;
                }, name: "GENBITS");
            Registers.InternalStateNumber.Define(this)
                .WithValueField(0, 4, out internalStateSelection, writeCallback: (_, val) =>
                {
                    if(val != InternalStateSoftwareStateSelection)
                    {
                        this.Log(LogLevel.Error, "This internal state is not being tracked. The only internal state implemented is the SoftwareIdState ({})", InternalStateSoftwareStateSelection);

                    }
                    internalStateReadFifo.Clear();
                    FillFifoWithInternalState();
                }, name: "INT_STATE_NUM")
                .WithReservedBits(5, 32 - 5);
            Registers.InternalStateReadAccess.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) =>
                {
                    return internalStateReadFifo.Count > 0 ? internalStateReadFifo.Dequeue() : 0u;
                }, name: "INT_STATE_VAL");
            Registers.HardwareExceptionStatus.Define(this)
                .WithTag("HW_EXC_STS", 0, 15)
                .WithReservedBits(16, 16);
            Registers.RecoverableAlertStatus.Define(this)
                .WithTaggedFlag("ENABLE_FIELD_ALERT", 0)
                .WithTaggedFlag("SW_APP_ENABLE_FIELD_ALERT", 1)
                .WithTaggedFlag("READ_INT_STATE_FIELD_ALERT", 2)
                .WithReservedBits(3, 9)
                .WithTaggedFlag("CS_BUS_CMP_ALERT", 12)
                .WithReservedBits(13, 32 - 13);
            Registers.ErrorCode.Define(this)
                .WithFlag(0, out commandError, FieldMode.Read, name: "SFIFO_CMD_ERR")
                .WithTaggedFlag("SFIFO_GENBITS_ERR", 1)
                .WithTaggedFlag("SFIFO_CMDREQ_ERR", 2)
                .WithTaggedFlag("SFIFO_RCSTAGE_ERR", 3)
                .WithTaggedFlag("SFIFO_KEYVRC_ERR", 4)
                .WithTaggedFlag("SFIFO_UPDREQ_ERR", 5)
                .WithTaggedFlag("SFIFO_BENCREQ_ERR", 6)
                .WithTaggedFlag("SFIFO_BENCACK_ERR", 7)
                .WithTaggedFlag("SFIFO_PDATA_ERR", 8)
                .WithTaggedFlag("SFIFO_FINAL_ERR", 9)
                .WithTaggedFlag("SFIFO_GBENCACK_ERR", 10)
                .WithTaggedFlag("SFIFO_GRCSTAGE_ERR", 11)
                .WithTaggedFlag("SFIFO_GGENREQ_ERR", 12)
                .WithTaggedFlag("SFIFO_GADSTAGE_ERR", 13)
                .WithTaggedFlag("SFIFO_GGENBITS_ERR", 14)
                .WithTaggedFlag("SFIFO_BLKENC_ERR", 15)
                .WithReservedBits(16, 4)
                .WithTaggedFlag("CMD_STAGE_SM_ERR", 20)
                .WithTaggedFlag("MAIN_SM_ERR", 21)
                .WithTaggedFlag("DRBG_GEN_SM_ERR", 22)
                .WithTaggedFlag("DRBG_UPDBE_SM_ERR", 23)
                .WithTaggedFlag("DRBG_UPDOB_SM_ERR", 24)
                .WithTaggedFlag("AES_CIPHER_SM_ERR", 25)
                .WithTaggedFlag("CMD_GEN_CNT_ERR", 26)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("FIFO_WRITE_ERR", 28)
                .WithTaggedFlag("FIFO_READ_ERR", 29)
                .WithTaggedFlag("FIFO_STATE_ERR", 30)
                .WithReservedBits(31, 1);
            Registers.ErrorCodeTest.Define(this)
                .WithTag("ERR_CODE_TEST", 0, 5)
                .WithReservedBits(5, 32 - 5);
            Registers.StateMachineState.Define(this, 0x4e)
               .WithTag("MAIN_SM_STATE", 0, 8)
               .WithReservedBits(8, 24);
        }

        public long Size => 0x1000;

        public GPIO RequestCompletedIRQ { get; }
        public GPIO EntropyeRequestedIRQ { get; }
        public GPIO HardwareInstanceIRQ { get; }
        public GPIO FatalErrorIRQ { get; }

        public GPIO RecoverableAlert { get; }
        public GPIO FatalAlert { get; }

        public uint ReseedCount => (uint)(drbgEngine?.InternalReseedCount ?? 0u);
        public uint[] InternalV
        {
            get
            {
                if(drbgEngine == null)
                {
                    return new uint[0];
                }
                return ByteArrayToRegisterOrderedUIntArray(drbgEngine.InternalV);
            }
        }

        public uint[] InternalKey
        {
            get
            {
                if(drbgEngine == null)
                {
                    return new uint[0];
                }
                return ByteArrayToRegisterOrderedUIntArray(drbgEngine.InternalKey);
            }
        }

        public RandomType WorkingMode
        {
            get
            {
                return workingMode;
            }
            set
            {
                workingMode = value;
                if(workingMode != RandomType.HardwareCompliant)
                {
                    fipsCompliant.Value = true;
                    InstantiateRandom();
                }
            }
        }

        public string FixedData
        {
            set
            {
                if(value.Length % 8 != 0)
                {
                    throw new RecoverableException("The data must be aligned to a 4 bytes (double word)");
                }
                fixedData = Misc.HexStringToByteArray(value);
                Misc.EndiannessSwapInPlace(fixedData, sizeof(uint));
                InstantiateRandom();
            }
        }

        private void InstantiateRandom()
        {
            if(WorkingMode == RandomType.PseudoRandomFixedSeed)
            {
                randomSource = new Random((int)Misc.ByteArrayRead(0, fixedData));
                return;
            }
            randomSource = new Random();
        }

        private void UpdateInterrupts()
        {
            EntropyeRequestedIRQ.Set(entropyRequestInterrupt.Value && entropyRequestInterruptEnabled.Value);
            FatalErrorIRQ.Set(fatalErrorInterrupt.Value && fatalErrorInterruptEnabled.Value);
            HardwareInstanceIRQ.Set(hardwareInstanceInterrupt.Value && hardwareInstanceInterruptEnabled.Value);
            RequestCompletedIRQ.Set(requestCompletedInterrupt.Value && requestCompletedInterruptEnabled.Value);
        }

        private void HandleCommandRequestWrite(uint writeValue)
        {
            if(enabled.Value == MultiBitBool4.False)
            {
                this.Log(LogLevel.Error, "Peripheral disabled - will not execute command");
                return;
            }
            if(!NoMoreDataToConsume && TryConsumeAppendedData(writeValue))
            {
                return;
            }

            if(TryExtractCommandParameters(writeValue, out var command, out var commandLength, out var flags, out var generateLength))
            {
                this.Log(LogLevel.Debug, "Got command {0}, with commandLength {1}, and generateLength {2}", command, commandLength, generateLength);
                ExecuteCommand(command, commandLength, flags, generateLength);
            }
            else
            {
                RaiseCommandError();
            }
        }

        private bool TryConsumeAppendedData(uint data)
        {
            if(NoMoreDataToConsume)
            {
                return false;
            }
            appendedDataCount--;
            if(WorkingMode == RandomType.HardwareCompliant)
            {
                appendedData.Add(Misc.EndiannessSwap(data));
                if(NoMoreDataToConsume)
                {
                    this.Log(LogLevel.Warning, "Finished completing additional command data");
                    var dataArray = appendedData.ToArray();
                    appendedDataAction(dataArray);
                    appendedData.Clear();
                }
            }
            return true;
        }

        private bool TryExtractCommandParameters(uint commandHeader, out CommandName command, out uint commandLength, out bool[] flags, out uint generateLength)
        {
            command = default(CommandName);
            commandLength = 0;
            generateLength = 0;
            flags = new bool[8];

            var rawCommand = (int)BitHelper.GetValue(commandHeader, 0, 4);
            if(!Enum.IsDefined(typeof(CommandName), rawCommand))
            {
                return false;
            }

            command = (CommandName)rawCommand;
            commandLength = BitHelper.GetValue(commandHeader, 4, 4);
            flags = BitHelper.GetBits(BitHelper.GetValue(commandHeader, 8, 4));
            generateLength = BitHelper.GetValue(commandHeader, 12, 13);

            return true;
        }

        private void RaiseCommandError()
        {
            commandError.Value = true;
            fatalErrorInterrupt.Value = true;
            UpdateInterrupts();
        }

        private void ExecuteCommand(CommandName command, uint commandLength, bool[] flags, uint generateLength)
        {
            var useEntropy = !flags[0];
            switch(command)
            {
                case CommandName.Instantiate:
                    ExecuteInstantiate(commandLength, useEntropy);
                    break;
                case CommandName.Generate:
                    ExecuteGenerate(generateLength);
                    break;
                case CommandName.Uninstantiate:
                    if(commandLength != 0)
                    {
                        this.Log(LogLevel.Warning, "The 'Uninstantiate' command can be used only with a zero 'clen' value.");
                    }
                    ExecuteUninstantiate();
                    break;
                case CommandName.Reseed:
                    ExecuteReseed(commandLength, useEntropy);
                    break;
                case CommandName.Update:
                    ExecuteUpdate(commandLength);
                    break;
                default:
                    this.Log(LogLevel.Error, "Got an illegal application command. Ignoring");
                    return;
            }
            requestCompletedInterrupt.Value = true;
            UpdateInterrupts();
        }

        private void ExecuteGenerate(uint generateLength)
        {
            var lengthInBytes = BytesPerEntropyUnit * (int)generateLength;
            FillFifoWithGeneratedBits(lengthInBytes);
        }

        private void FillFifoWithGeneratedBits(int bytesToGenerate)
        {
            var generatedBytes = new byte[bytesToGenerate];
            switch(WorkingMode)
            {
                case RandomType.PseudoRandom:
                case RandomType.PseudoRandomFixedSeed:
                    randomSource.NextBytes(generatedBytes);
                    break;
                case RandomType.FixedData:
                    Misc.FillByteArrayWithArray(generatedBytes, fixedData);
                    break;
                case RandomType.HardwareCompliant:
                    if(drbgEngine.Generate(generatedBytes, additionalInput: null, predictionResistant: false) == -1)
                    {
                        requestFailedFlag.Value = true;
                        generatedValidFlag.Value = false;
                        fatalErrorInterrupt.Value = true;
                        UpdateInterrupts();
                        return;
                    };
                    // The CtrSP800Drbg returns bytes in reversed order
                    Array.Reverse(generatedBytes);
                    // Peripheral expects the entropy units to be in a reversed order
                    ReorderEntropyUnits(ref generatedBytes);
                    break;
                default:
                    throw new ArgumentException("Unknown type of simulation mode");
            }
            var generatedDoubleWords = new uint[bytesToGenerate / sizeof(uint)];
            Buffer.BlockCopy(generatedBytes, 0, generatedDoubleWords, 0, bytesToGenerate);

            requestFailedFlag.Value = false;
            generatedValidFlag.Value = true;
            foreach(var doubleWord in generatedDoubleWords)
            {
                generatedBitsFifo.Enqueue(doubleWord);
            }
        }

        private void ReorderEntropyUnits(ref byte[] inputData)
        {
            if(inputData.Length % BytesPerEntropyUnit != 0)
            {
                throw new ArgumentException($"Input data must bu aligned to the size of entropy unit ({BytesPerEntropyUnit} bytes)");
            }
            var temp = new byte[BytesPerEntropyUnit];
            var unitsCount = inputData.Length / BytesPerEntropyUnit;
            int insertOffset = (unitsCount - 1) * BytesPerEntropyUnit;
            for(var unit = 0; unit <= (unitsCount -1)/2; unit++)
            {
                var sourceOffset = unit * BytesPerEntropyUnit;
                Buffer.BlockCopy(inputData, sourceOffset, temp, 0, BytesPerEntropyUnit);
                Buffer.BlockCopy(inputData, insertOffset, inputData, sourceOffset, BytesPerEntropyUnit);
                Buffer.BlockCopy(temp, 0, inputData, insertOffset, BytesPerEntropyUnit);
                insertOffset -= BytesPerEntropyUnit;
            }
        }

        private void ExecuteInstantiate(uint commandLength, bool useEntropy)
        {
            if(WorkingMode == RandomType.PseudoRandomFixedSeed)
            {
                InstantiateRandom();
            }
            else if(WorkingMode == RandomType.HardwareCompliant)
            {
                fipsCompliant.Value = useEntropy;
                if(commandLength != 0)
                {
                    appendedDataCount = commandLength;
                    appendedDataAction = (dataArray) =>
                    {
                        var data = ProcessAppendedData(dataArray, useEntropy);
                        EngineReinitWithSeed(data);
                    };
                }
                else
                {
                    ReseedZeroLength(useEntropy);
                }
            }
        }

        private uint[] ProcessAppendedData(uint[] dataArray, bool useEntropy)
        {
            if(useEntropy)
            {
                this.DebugLog("Reseed with seed XOR'ed with received data");
                for(var index = 0; index < dataArray.Length; index++)
                {
                    seed[index] |= dataArray[index];
                }
            }
            else
            {
                this.DebugLog("Reseed with received data");
                seed = dataArray;
            }
            return seed.Reverse().ToArray();
        }

        private void EngineReinitWithSeed(uint[] seed)
        {
            fakeEntropy.SetEntropySizeInBytes(seed.Length * sizeof(uint));
            fakeEntropy.SetEntropySource(() =>
            {
                return Misc.AsBytes(seed);
            });
            ReinstantiateInternal();
            appendedData.Clear();
        }

        private void ReseedZeroLength(bool useEntropy)
        {
            if(useEntropy)
            {
                fakeEntropy.SetEntropySizeInBytes(DefaultSeedSizeInBytes);
                fakeEntropy.SetEntropySource(entropySource.RequestEntropySourceData);
            }
            else
            {
                // Seed should be all zeroes
                Array.Clear(seed, 0, seed.Length);
                EngineReseed(seed);
            }
        }

        private void ExecuteReseed(uint commandLength, bool useEntropy)
        {
            if(WorkingMode == RandomType.HardwareCompliant)
            {
                if(commandLength != 0)
                {
                    appendedDataCount = commandLength;
                    appendedDataAction = (dataArray) =>
                    {
                        var data = ProcessAppendedData(dataArray, useEntropy);
                        EngineReseed(data);
                    };
                }
                else
                {
                    ReseedZeroLength(useEntropy);
                }
            }
        }

        private void EngineReseed(uint[] data)
        {
            if(drbgEngine == null)
            {
                ReinstantiateInternal();
            }
            var dataAsBytes = Misc.AsBytes(data);
            drbgEngine.Reseed(dataAsBytes);
        }

        private void ExecuteUpdate(uint commandLength)
        {
            appendedDataCount = commandLength;
            appendedDataAction = (dataArray) =>
            {
                var data = dataArray.Reverse().ToArray();
                EngineUpdate(data);
            };
        }

        private void EngineUpdate(uint[] data)
        {
            var dataAsBytes = Misc.AsBytes(data);
            drbgEngine.Update(dataAsBytes);
        }

        private void ExecuteUninstantiate()
        {
            drbgEngine = null;
            ClearState();
        }

        private void ClearState()
        {
            fatalErrorInterrupt.Value = false;
            entropyRequestInterrupt.Value = false;
            hardwareInstanceInterrupt.Value = false;
            requestCompletedInterrupt.Value = false;
            fipsCompliant.Value = false;
            generatedValidFlag.Value = false;
            commandError.Value = false;
            requestFailedFlag.Value = false;
        }

        private void FillFifoWithInternalState()
        {
            foreach(var doubleWord in GenerateInternalStateArray())
            {
                internalStateReadFifo.Enqueue(doubleWord);
            }
        }

        private uint[] GenerateInternalStateArray()
        {
            var list = new List<uint>();
            list.Add(ReseedCount);
            list.AddRange(InternalV);
            list.AddRange(InternalKey);
            var statusBit = drbgEngine != null ? 1u : 0u;
            var complianceBit = (fipsCompliant.Value ? 1u : 0u) << 1;
            list.Add(statusBit | complianceBit);
            return list.ToArray();
        }

        private uint[] ByteArrayToRegisterOrderedUIntArray(byte[] inputArray)
        {
            uint[] doubleWordRepresentation = new uint[inputArray.Length / sizeof(uint)];
            Buffer.BlockCopy(inputArray, 0, doubleWordRepresentation, 0, inputArray.Length);
            return doubleWordRepresentation.Select(x => Misc.EndiannessSwap(x)).Reverse().ToArray();
        }

        private bool NoMoreDataToConsume => appendedDataCount == 0;

        private IFlagRegisterField requestCompletedInterrupt;
        private IFlagRegisterField entropyRequestInterrupt;
        private IFlagRegisterField hardwareInstanceInterrupt;
        private IFlagRegisterField fatalErrorInterrupt;
        private IFlagRegisterField requestCompletedInterruptEnabled;
        private IFlagRegisterField entropyRequestInterruptEnabled;
        private IFlagRegisterField hardwareInstanceInterruptEnabled;
        private IFlagRegisterField fatalErrorInterruptEnabled;

        private IFlagRegisterField fipsCompliant;
        private IFlagRegisterField readyFlag;
        private IFlagRegisterField commandError;
        private IFlagRegisterField generatedValidFlag;
        private IFlagRegisterField requestFailedFlag;
        private IValueRegisterField internalStateSelection;

        private IEnumRegisterField<MultiBitBool4> enabled;
        private IEnumRegisterField<MultiBitBool4> genbitsReadEnabled;
        private uint appendedDataCount;
        private byte[] fixedData;
        private uint[] seed;
        private Action<uint[]> appendedDataAction;
        private Random randomSource;
        private RandomType workingMode;
        private readonly Queue<uint> generatedBitsFifo;
        private readonly FakeEntropy fakeEntropy;
        private readonly List<uint> appendedData;
        private readonly Queue<uint> internalStateReadFifo;

        private CtrSP800Drbg drbgEngine;

        private const int BytesPerEntropyUnit = 16;
        private const int DefaultSeedSizeInBytes = 48;
        private const int InternalStateSoftwareStateSelection = 2;

        private readonly OpenTitan_EntropySource entropySource;

        #pragma warning disable format
        public enum RandomType
        {
            PseudoRandom          = 0x0,  // Generated using the System.Random
            FixedData             = 0x1,  // Fixed data supplied using the property
            PseudoRandomFixedSeed = 0x2,  // Generated using the System.Random using fixed seed - produces the same sequence of bytes every time
            HardwareCompliant     = 0x3,  // Per OpenTitan_CSRNG specification
        }

        private enum CommandName
        {
            Instantiate   = 0x1,
            Reseed        = 0x2,
            Generate      = 0x3,
            Update        = 0x4,
            Uninstantiate = 0x5,
        }

        private enum Registers
        {
            InterruptState          = 0x0,
            InterruptEnable         = 0x4,
            InterruptTest           = 0x8,
            AlertTest               = 0xC,
            RegisterWriteEnable     = 0x10,
            Control                 = 0x14,
            CommandRequest          = 0x18,
            CommandStatus           = 0x1C,
            GenerateBitsValid       = 0x20,
            GenerateBits            = 0x24,
            InternalStateNumber     = 0x28,
            InternalStateReadAccess = 0x2C,
            HardwareExceptionStatus = 0x30,
            RecoverableAlertStatus  = 0x34,
            ErrorCode               = 0x38,
            ErrorCodeTest           = 0x3C,
            StateMachineState       = 0x40,
        }
        #pragma warning restore format

        class FakeEntropy: IEntropySource
        {
            public FakeEntropy(int defaultLengthInBytes, bool predictionResistant = false)
            {
                this.entropySizeInBytes = defaultLengthInBytes;
                this.predictionResistant = predictionResistant;
            }

            public bool IsPredictionResistant => predictionResistant;

            // Entropy size in bits
            public int EntropySize => entropySizeInBytes * 8;

            public void SetEntropySizeInBytes(int size)
            {
                entropySizeInBytes = size;
            }
            public void SetEntropySource(Func<byte[]> function)
            {
                this.function = function;
            }

            public byte[] GetEntropy()
            {
                if(function == null)
                {
                    return new byte[entropySizeInBytes];
                }
                return function();
            }

            private Func<byte[]> function;
            private int entropySizeInBytes;
            private bool predictionResistant;
        }
    }
}
