//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_CSRNG: BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_CSRNG(Machine machine) : base(machine)
        {
            DefineRegisters();

            RequestCompletedIRQ = new GPIO();
            EntropyeRequestedIRQ = new GPIO();
            HardwareInstanceIRQ = new GPIO();
            FatalErrorIRQ = new GPIO();

            WorkingMode = RandomType.PseudoRandom;
            generatedBitsFifo = new Queue<uint>();

            Reset();
        }

        public override void Reset()
        {
            appendedDataCount = 0;
            generatedBitsFifo.Clear();
            base.Reset();
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
                .WithWriteCallback((_, val) => { if(val != 0) UpdateInterrupts();});
            Registers.AlertTest.Define(this)
                .WithTaggedFlag("recov_alert", 0)
                .WithTaggedFlag("fatal_alert", 1)
                .WithReservedBits(2, 30);
            Registers.RegisterWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("REGWEN", 0)
                .WithReservedBits(1, 31);
            Registers.Control.Define(this, 0x999)
                .WithEnumField<DoubleWordRegister, MultiBitBool>(0, 4, out enabled, name: "ENABLE")
                .WithEnumField<DoubleWordRegister, MultiBitBool>(4, 4, out genbitsReadEnabled, name: "SW_APP_ENABLE")
                .WithTag("READ_INT_STATE", 8, 4)
                .WithReservedBits(12, 20);
            Registers.CommandRequest.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => HandleCommandRequestWrite(val), name: "CMD_REQ");
            Registers.CommandStatus.Define(this, 0x1)
                .WithFlag(0, out readyFlag, FieldMode.Read, name: "CMD_RDY")
                .WithFlag(1, out requestFailedFlag, FieldMode.Read, name: "CMD_STS")
                .WithReservedBits(2, 30);
            Registers.GenerateBitsValid.Define(this)
                .WithFlag(0, out generatedValidFlag, FieldMode.Read, name: "GENBITS_VLD")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "GENBITS_FIPS")
                .WithReservedBits(2, 30);
            Registers.GenerateBits.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) =>
                {
                    if(genbitsReadEnabled.Value == MultiBitBool.False)
                    {
                        this.Log(LogLevel.Error, "Trying to read the generatedBitsFifo when 'SW_APP_ENABLE' is not set to true");
                        return 0;
                    }

                    if(!generatedBitsFifo.TryDequeue(out var value))
                    {
                        this.Log(LogLevel.Error, "Trying to read generated bits when there are no more available. Generate lenght mismatch?");
                        return 0;
                    }
                    return value;
                }, name: "GENBITS");
            Registers.InternalStateNumber.Define(this)
                .WithTag("INT_STATE_NUM", 0, 4)
                .WithReservedBits(5, 32 - 5);
            Registers.InternalStateReadAccess.Define(this)
                .WithTag("INT_STATE_VAL", 0, 32);
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
            Registers.SelectTrackingStateRegister.Define(this)
               .WithTaggedFlag("SEL_TRACKING_SM", 0)
               .WithReservedBits(1, 31);
            Registers.TrackingStateObservationRegister.Define(this)
                .WithTag("TRACKING_SM_OBS0", 0, 8)
                .WithTag("TRACKING_SM_OBS0", 8, 8)
                .WithTag("TRACKING_SM_OBS0", 16, 8)
                .WithTag("TRACKING_SM_OBS0", 24, 8);
        }

        public long Size => 0x1000;

        public GPIO RequestCompletedIRQ { get; }
        public GPIO EntropyeRequestedIRQ { get; }
        public GPIO HardwareInstanceIRQ { get; }
        public GPIO FatalErrorIRQ { get; }

        public RandomType WorkingMode
        {
            get
            {
                return workingMode;
            }
            set
            {
                workingMode = value;
                InstantiateRandom();
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
            if(WorkingMode == RandomType.PseudoRandom)
            {
                randomSource = new Random();
            }
            else if(WorkingMode == RandomType.PseudoRandomFixedSeed)
            {
                randomSource = new Random((int)Misc.ByteArrayRead(0, fixedData));
            }
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
            if(enabled.Value == MultiBitBool.False)
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
            // As we don't follow the approach of the hardware, the appended data is discarded
            appendedDataCount--;
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
            switch(command)
            {
                case CommandName.Instantiate:
                    ExecuteInstantiate(commandLength);
                    break;
                case CommandName.Generate:
                    ExecuteGenerate(generateLength);
                    requestCompletedInterrupt.Value = true;
                    UpdateInterrupts();
                    break;
                case CommandName.Uninstantiate:
                    if(commandLength != 0)
                    {
                        this.Log(LogLevel.Warning, "The 'Uninstantiate' command can be used only with a zero 'clen' value.");
                    }
                    break;
                case CommandName.Reseed:
                case CommandName.Update:
                    this.NoisyLog($"The {command} is not implemented. This command will be ignored");
                    break;
                default:
                    this.Log(LogLevel.Error, "Got an illegal application command. Ignoring");
                    break;
            }
        }

        private void ExecuteGenerate(uint generateLength)
        {
            var lengthInBytes = BytesPerEntropyUnit * (int)generateLength;
            FillFifoWithGeneratedBits(lengthInBytes);
            generatedValidFlag.Value = true;
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
                default:
                    throw new ArgumentException("Unknown type of simulation mode");
            }
            var generatedDoubleWords = new uint[bytesToGenerate / sizeof(uint)];
            Buffer.BlockCopy(generatedBytes, 0, generatedDoubleWords, 0, bytesToGenerate);

            foreach(var doubleWord in generatedDoubleWords)
            {
                generatedBitsFifo.Enqueue(doubleWord);
            }
        }

        private void ExecuteInstantiate(uint commandLength)
        {
            if(WorkingMode == RandomType.PseudoRandomFixedSeed)
            {
                InstantiateRandom();
            }
            if(commandLength != 0)
            {
                appendedDataCount = commandLength;
            }
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

        private IFlagRegisterField readyFlag;
        private IFlagRegisterField commandError;
        private IFlagRegisterField generatedValidFlag;
        private IFlagRegisterField requestFailedFlag;

        private IEnumRegisterField<MultiBitBool> enabled;
        private IEnumRegisterField<MultiBitBool> genbitsReadEnabled;
        private uint appendedDataCount;
        private byte[] fixedData;
        private Random randomSource;
        private RandomType workingMode;
        private readonly Queue<uint> generatedBitsFifo;

        private const int BytesPerEntropyUnit = 16;

        #pragma warning disable format
        public enum RandomType
        {
            PseudoRandom          = 0x0,  // Generated using the System.Random
            FixedData             = 0x1,  // Fixed data supplied using the property
            PseudoRandomFixedSeed = 0x2,  // Generated using the System.Random using fixed seed - produces the same sequence of bytes every time
        }

        private enum CommandName
        {
            Instantiate   = 0x1,
            Reseed        = 0x2,
            Generate      = 0x3,
            Update        = 0x4,
            Uninstantiate = 0x5,
        }

        private enum MultiBitBool : byte
        {
            True  = 0xA,
            False = 0x5,
        }

        private enum Registers
        {
            InterruptState                   = 0x0,
            InterruptEnable                  = 0x4,
            InterruptTest                    = 0x8,
            AlertTest                        = 0xC,
            RegisterWriteEnable              = 0x10,
            Control                          = 0x14,
            CommandRequest                   = 0x18,
            CommandStatus                    = 0x1C,
            GenerateBitsValid                = 0x20,
            GenerateBits                     = 0x24,
            InternalStateNumber              = 0x28,
            InternalStateReadAccess          = 0x2C,
            HardwareExceptionStatus          = 0x30,
            RecoverableAlertStatus           = 0x34,
            ErrorCode                        = 0x38,
            ErrorCodeTest                    = 0x3C,
            SelectTrackingStateRegister      = 0x40,
            TrackingStateObservationRegister = 0x44,
        }
        #pragma warning restore format
    }
}
