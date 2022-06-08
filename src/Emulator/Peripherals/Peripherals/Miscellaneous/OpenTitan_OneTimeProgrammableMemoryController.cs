//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_OneTimeProgrammableMemoryController: BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_OneTimeProgrammableMemoryController(Machine machine) : base(machine)
        {
            memoryLock = new Object();
            transitionCountLock = new Object();
            DefineRegisters();
            Reset();

            aValues = new ushort[ABValuesWordsCount];
            bValues = new ushort[ABValuesWordsCount];
            cValues = new ushort[CDValuesWordsCount];
            dValues = new ushort[CDValuesWordsCount];

            InitPositionConsumedToLifeCycleMapping();
            underlyingMemory = new ArrayMemory(0x1000);
        }

        public override void Reset()
        {
            base.Reset();
            cachedLifeCycleState = null;
            cachedTransitionCount = null;
        }

        public void LoadVMem(ReadFilePath filename)
        {
            try
            {
                var reader = new VmemReader(filename);
                lock(memoryLock)
                {
                    foreach(var pair in reader.GetIndexDataPairs())
                    {
                        // VmemReader returns 4 byte values, but the two first contains only the ECC which we don't need
                        var offset = pair.Item1 * 2;
                        underlyingMemory.WriteWord(offset, (ushort)pair.Item2);
                    }
                }
            }
            catch(Exception e)
            {
                throw new RecoverableException($"Exception while loading file {filename}: {e.Message}");
            }
        }

        public byte[] GetOtpItem(OtpItem item)
        {
            var itemLength = GetItemSizeAttribute(item);
            lock(memoryLock)
            {
                return underlyingMemory.ReadBytes((uint)item, itemLength.ByteLength);
            }
        }

        public int IncrementTransitionCount()
        {
            lock(transitionCountLock)
            {
                var currentCount = LifeCycleTransitionCount;
                if(currentCount == MaximumTransitionsCount)
                {
                    this.Log(LogLevel.Warning, "Transitions count already reached its limit of {0} transitions. Trying to increment it now is illegal.", MaximumTransitionsCount);
                    return -1;

                }
                LifeCycleTransitionCount = (ushort)(currentCount + 1);
            }
            return LifeCycleTransitionCount;
        }

        public long Size => 0x1800;

        public string AValuesChain
        {
            set
            {
                aValues = SplitValueChainIntoWordsArray(value);
            }
        }

        public string BValuesChain
        {
            set
            {
                bValues = SplitValueChainIntoWordsArray(value);
            }
        }

        public string CValuesChain
        {
            set
            {
                cValues = SplitValueChainIntoWordsArray(value);
            }
        }

        public string DValuesChain
        {
            set
            {
                dValues = SplitValueChainIntoWordsArray(value);
            }
        }

        public OpenTitan_LifeCycleState LifeCycleState
        {
            set
            {
                EncodeLifeCycleState(value);
                cachedLifeCycleState = value;
            }
            get
            {
                if(!cachedLifeCycleState.HasValue)
                {
                    OpenTitan_LifeCycleState state;
                    cachedLifeCycleState = TryDecodeLifeCycleState(out state) ? state : OpenTitan_LifeCycleState.Invalid;
                }
                return cachedLifeCycleState.Value;
            }
        }

        public ushort LifeCycleTransitionCount
        {
            private set
            {
                cachedTransitionCount = value;
                EncodeLifeCycleTransitionCount(value);
            }
            get
            {
                if(!cachedTransitionCount.HasValue)
                {
                    cachedTransitionCount = DecodeLifeCycleTransitionCount();
                }
                return cachedTransitionCount.Value;
            }
        }

        private void DefineRegisters()
        {
            Registers.InterruptState.Define(this)
                .WithTaggedFlag("otp_operation_done", 0)
                .WithTaggedFlag("otp_error", 1)
                .WithIgnoredBits(2, 30);
            Registers.InterruptEnable.Define(this)
                .WithTaggedFlag("otp_operation_done", 0)
                .WithTaggedFlag("otp_error", 1)
                .WithIgnoredBits(2, 30);
            Registers.InterruptTest.Define(this)
                .WithTaggedFlag("otp_operation_done", 0)
                .WithTaggedFlag("otp_error", 1)
                .WithIgnoredBits(2, 30);
            Registers.AlertTest.Define(this)
                .WithTaggedFlag("fatal_macro_error", 0)
                .WithTaggedFlag("fatal_check_error", 1)
                .WithTaggedFlag("fatal_bus_integ_error", 2)
                .WithIgnoredBits(3, 29);
            Registers.Status.Define(this)
                .WithFlag(0, out vendorPartitionErrorFlag, FieldMode.Read, name: "VENDOR_TEST_ERROR")
                .WithFlag(1, out creatorPartitionErrorFlag, FieldMode.Read, name: "CREATOR_SW_CFG_ERROR")
                .WithFlag(2, out ownerPartitionErrorFlag, FieldMode.Read, name: "OWNER_SW_CFG_ERROR")
                .WithFlag(3, out hardwarePartitionErrorFlag, FieldMode.Read, name: "HW_CFG_ERROR")
                .WithFlag(4, out secret0PartitionErrorFlag, FieldMode.Read, name: "SECRET0_ERROR")
                .WithFlag(5, out secret1PartitionErrorFlag, FieldMode.Read, name: "SECRET1_ERROR")
                .WithFlag(6, out secret2PartitionErrorFlag, FieldMode.Read, name: "SECRET2_ERROR")
                .WithFlag(7, out lifeCyclePartitionErrorFlag, FieldMode.Read, name: "LIFE_CYCLE_ERROR")
                .WithFlag(8, out daiErrorFlag, FieldMode.Read, name: "DAI_ERROR")
                .WithTaggedFlag("LCI_ERROR", 9)
                .WithTaggedFlag("TIMEOUT_ERROR", 10)
                .WithTaggedFlag("LFSR_FSM_ERROR", 11)
                .WithTaggedFlag("SCRAMBLING_FSM_ERROR", 12)
                .WithTaggedFlag("KEY_DERIV_FSM_ERROR", 13)
                .WithTaggedFlag("KEY_DERIV_FSM_ERROR", 14)
                .WithFlag(15, out daiIdleFlag, FieldMode.Read, name: "DAI_IDLE")
                .WithTaggedFlag("CHECK_PENDING", 16)
                .WithReservedBits(17, 32 - 17);
            Registers.ErrorCode.Define(this)
                .WithEnumField<DoubleWordRegister, Error>(0, 3, out vendorPartitionError, FieldMode.Read, name: "ERR_CODE_0")
                .WithEnumField<DoubleWordRegister, Error>(3, 3, out creatorPartitionError, FieldMode.Read, name: "ERR_CODE_1")
                .WithEnumField<DoubleWordRegister, Error>(6, 3, out ownerPartitionError, FieldMode.Read, name: "ERR_CODE_2")
                .WithEnumField<DoubleWordRegister, Error>(9, 3, out hardwarePartitionError, FieldMode.Read, name: "ERR_CODE_3")
                .WithEnumField<DoubleWordRegister, Error>(12, 3, out secret0PartitionError, FieldMode.Read, name: "ERR_CODE_4")
                .WithEnumField<DoubleWordRegister, Error>(15, 3, out secret1PartitionError, FieldMode.Read, name: "ERR_CODE_5")
                .WithEnumField<DoubleWordRegister, Error>(18, 3, out secret2PartitionError, FieldMode.Read, name: "ERR_CODE_6")
                .WithEnumField<DoubleWordRegister, Error>(21, 3, out lifeCyclePartitionError, FieldMode.Read, name: "ERR_CODE_7")
                .WithEnumField<DoubleWordRegister, Error>(24, 3, out daiError, FieldMode.Read, name: "ERR_CODE_8")
                .WithTag("ERR_CODE_9", 27, 3)
                .WithReservedBits(30, 2);
            Registers.DirectAccesssRegisterEnable.Define(this, 0x1)
                .WithFlag(0, FieldMode.Read, name: "DIRECT_ACCESS_REGWEN")
                .WithReservedBits(1, 31);
            Registers.DirectAccessCommand.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, writeCallback: (_, val) => { if(val) ExecuteDirectRead(); }, name: "RD")
                .WithTaggedFlag("WR", 1)
                .WithTaggedFlag("DIGEST", 2)
                .WithReservedBits(3, 32 - 3);
            Registers.DirectAccessAddress.Define(this)
                .WithValueField(0, 11, out accessAddress, writeCallback: (_, val) =>
                    {
                        this.NoisyLog("Direct access address set to: 0x{0:X} [{1}]", val, (OtpItem)val);
                    }, name: "DIRECT_ACCESSS_ADDRESS")
                .WithReservedBits(11, 32 - 11);
            Registers.DirectAccessWriteData_0.Define(this)
                .WithTag("DIRECT_ACCESS_WDATA_0", 0, 32);
            Registers.DirectAccessWriteData_1.Define(this)
                .WithTag("DIRECT_ACCESS_WDATA_1", 0, 32);
            Registers.DirectAccessReadData_0.Define(this, 0)
                .WithValueField(0, 32, out readData0, FieldMode.Read, name: "DIRECT_ACCESS_RDATA_0");
            Registers.DirectAccessReadData_1.Define(this, 0)
                .WithValueField(0, 32, out readData1, FieldMode.Read, name: "DIRECT_ACCESS_RDATA_1");
            Registers.CheckTriggerRegisterWriteEnable.Define(this)
                .WithTaggedFlag("CHECK_TRIGGER_REGWEN", 0)
                .WithIgnoredBits(1, 31);
            Registers.CheckTrigger.Define(this)
                .WithTaggedFlag("INREGRITY", 0)
                .WithTaggedFlag("CONSISTENCY", 1)
                .WithIgnoredBits(2, 30);
            Registers.CheckRegistersWriteEnable.Define(this, 0x1)
                .WithFlag(0, FieldMode.Read | FieldMode.WriteZeroToClear, name: "CHECK_REGWEN")
                .WithReservedBits(1, 31);
            Registers.CheckTimeout.Define(this)
                .WithTag("CHECK_TIMEOUT", 0, 32);
            Registers.IntegrityCheckPeriod.Define(this)
                .WithTag("INTEGRITY_CHECK_PERIOD", 0, 32);
            Registers.ConsistencyCheckPeriod.Define(this)
                .WithTag("CONSISTENCY_CHECK_PERIOD", 0, 32);
            Registers.VendorTestReadLock.Define(this, 0x1)
                .WithFlag(0, out vendorPartitionUnlockedFlag, FieldMode.WriteZeroToClear, name: "VENDOR_TEST_READ_LOCK")
                .WithIgnoredBits(1, 31);
            Registers.CreatorSoftwareConfigReadLock.Define(this, 0x1)
                .WithFlag(0, out creatorPartitionUnlockedFlag, FieldMode.WriteZeroToClear, name: "VENDOR_TEST_READ_LOCK")
                .WithIgnoredBits(1, 31);
            Registers.OwnerSoftwareConfigReadLock.Define(this, 0x1)
                .WithFlag(0, out ownerPartitionUnlockedFlag, FieldMode.WriteZeroToClear, name: "VENDOR_TEST_READ_LOCK")
                .WithIgnoredBits(1, 31);
            Registers.VendorTestDigest0.Define(this)
                .WithTag("VENDOR_TEST_DIGEST_0", 0, 32);
            Registers.VendorTestDigest1.Define(this)
                .WithTag("VENDOR_TEST_DIGEST_1", 0, 32);
            Registers.CreatorSoftwareConfigDigest0.Define(this)
                .WithTag("CREATOR_SW_CFG_DIGEST_0", 0, 32);
            Registers.CreatorSoftwareConfigDigest1.Define(this)
                .WithTag("CREATOR_SW_CFG_DIGEST_1", 0, 32);
            Registers.OwnerSoftwareConfigDigest0.Define(this)
                .WithTag("OWNER_SW_CFG_DIGEST_0", 0, 32);
            Registers.OwnerSoftwareConfigDigest1.Define(this)
                .WithTag("OWNER_SW_CFG_DIGEST_1", 0, 32);
            Registers.HardwareConfigDigest0.Define(this)
                .WithTag("HW_CFG_DIGEST_0", 0, 32);
            Registers.HardwareConfigDigest1.Define(this)
                .WithTag("HW_CFG_DIGEST_1", 0, 32);
            Registers.Secret0Digest0.Define(this)
                .WithTag("SECRET0_DIGEST0", 0, 32);
            Registers.Secret0Digest1.Define(this)
                .WithTag("SECRET0_DIGEST1", 0, 32);
            Registers.Secret1Digest0.Define(this)
                .WithTag("SECRET1_DIGEST0", 0, 32);
            Registers.Secret1Digest1.Define(this)
                .WithTag("SECRET1_DIGEST1", 0, 32);
            Registers.Secret2Digest0.Define(this)
                .WithTag("SECRET2_DIGEST0", 0, 32);
            Registers.Secret2Digest1.Define(this)
                .WithTag("SECRET2_DIGEST1", 0, 32);
        }

        private void ExecuteDirectRead()
        {
            this.NoisyLog("Starting the DAI read");

            OtpItem item;
            OtpPartition partition;
            int itemOffset, partitionOffset;
            var readAddress = accessAddress.Value;

            if(!TryGetOtpPartitionAndItem(readAddress, out item, out partition, out itemOffset, out partitionOffset))
            {
                this.Log(LogLevel.Error, "Failed to find an OTP Partition or OTP Item at the address 0x{0:X}", readAddress);
                return;
            }

            if(!IsPartitionReadable(partition))
            {
                this.Log(LogLevel.Warning, "The DAI read failed due to the parition being locked");
                RaisePartitionError(Error.Access, partition);
                daiError.Value = Error.Access;
                daiErrorFlag.Value = true;
                return;
            }

            this.NoisyLog("Executing read from the {0}+0x{1:X} on the partition {2}+0x{3:X}", item, itemOffset, partition, partitionOffset);

            var itemAttribute = GetItemSizeAttribute(item);
            DirectReadInner(readAddress, itemAttribute.Is64Bit);
            daiIdleFlag.Value = true;
        }

        private void DirectReadInner(uint readAddress, bool is64BitItem)
        {
            lock(memoryLock)
            {
                readData0.Value = underlyingMemory.ReadDoubleWord(readAddress);
                if(is64BitItem)
                {
                    readData1.Value = underlyingMemory.ReadDoubleWord(readAddress + 0x4);
                }
            }
        }

        private bool TryGetOtpPartitionAndItem(uint readAddress, out OtpItem item, out OtpPartition partition, out int itemOffset, out int partitionOffset)
        {
            itemOffset = 0;
            partitionOffset = 0;
            item = default(OtpItem);
            partition = default(OtpPartition);

            return Misc.TryFindPreceedingEnumItem(readAddress, out item, out itemOffset) &&
                   Misc.TryFindPreceedingEnumItem(readAddress, out partition, out partitionOffset);
        }

        private bool IsPartitionReadable(OtpPartition partition)
        {
            switch(partition)
            {
                case OtpPartition.VendorTest:
                    return vendorPartitionUnlockedFlag.Value;
                case OtpPartition.CreatorSoftwareConfig:
                    return creatorPartitionUnlockedFlag.Value;
                case OtpPartition.OwnerSoftwareConfig:
                    return ownerPartitionUnlockedFlag.Value;
                default:
                    return true;
            }
        }

        private void RaisePartitionError(Error error, OtpPartition partition)
        {
            switch(partition)
            {
                case OtpPartition.VendorTest:
                    vendorPartitionError.Value = error;
                    vendorPartitionErrorFlag.Value = true;
                    break;
                case OtpPartition.OwnerSoftwareConfig:
                    ownerPartitionError.Value = error;
                    ownerPartitionErrorFlag.Value = true;
                    break;
                case OtpPartition.CreatorSoftwareConfig:
                    creatorPartitionError.Value = error;
                    creatorPartitionErrorFlag.Value = true;
                    break;
                case OtpPartition.HardwareConfig:
                    hardwarePartitionError.Value = error;
                    hardwarePartitionErrorFlag.Value = true;
                    break;
                case OtpPartition.Secret0:
                    secret0PartitionError.Value = error;
                    secret0PartitionErrorFlag.Value = true;
                    break;
                case OtpPartition.Secret1:
                    secret1PartitionError.Value = error;
                    secret1PartitionErrorFlag.Value = true;
                    break;
                case OtpPartition.Secret2:
                    secret2PartitionError.Value = error;
                    secret2PartitionErrorFlag.Value = true;
                    break;
                case OtpPartition.LifeCycle:
                    lifeCyclePartitionError.Value = error;
                    lifeCyclePartitionErrorFlag.Value = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown partition {partition}");
            }
        }

        private ItemSizeAttribute GetItemSizeAttribute(OtpItem item)
        {
            var type = item.GetType();
            var name = Enum.GetName(type, item);
            return type.GetField(name).GetCustomAttribute<ItemSizeAttribute>();
        }

        private ushort[] SplitValueChainIntoWordsArray(string valueChain)
        {
            var stringLength = valueChain.Length;
            if(stringLength % 4 != 0)
            {
                throw new ConstructionException($"Values chain string must consist of ordered 4 character hex values. Length {stringLength} uncorrect");
            }
            var chainElements = stringLength / 4;
            var output = new ushort[chainElements];
            int indexStart = 0;
            try
            {
                for(int elementIndex = 0; elementIndex < chainElements; elementIndex++)
                {
                    indexStart = elementIndex * 4;
                    output[elementIndex] = UInt16.Parse(valueChain.Substring(indexStart, 4), System.Globalization.NumberStyles.HexNumber);
                }
            }
            catch(FormatException)
            {
                throw new ConstructionException(String.Concat("Values chain string must consist of ordered 4 character hex values. ",
                                                $"Format incorrect between characters {indexStart} - {indexStart + 4} : \"{valueChain.Substring(indexStart, indexStart + 4)}\""));
            }
            return output;
        }

        /*
         * The LifeCycleStateTransitionCount is decoded/encoded using 24 words that are intially written with some combination of Cx/Dx values.
         * Each transition cause one Cn value to be overwritten with Dn value when doing the n'th transition.
         * Ex. when doing 6th transition the 6th word gets overwritten with D6 value. No more than 24 transitions are possible
         *  */
        private ushort DecodeLifeCycleTransitionCount()
        {
            var transitionCount = GetOtpItem(OtpItem.LifeCycleTransitionCount);
            if(transitionCount.All(x => x == 0))
            {
                return 0;
            }
            var positionsConsumed = GetConsumedPositionsMap(cValues, dValues, transitionCount);
            var stroke = MaximumTransitionsCount - Misc.CountTrailingZeroes(positionsConsumed);

            return (ushort)stroke;
        }

        private void EncodeLifeCycleTransitionCount(uint newCount)
        {
            // Need to consume only the last position,as the rest should already be set
            var strokeIndex = newCount - 1;
            var writeOffset = (uint)OtpItem.LifeCycleTransitionCount + strokeIndex * 2;
            lock(memoryLock)
            {
                underlyingMemory.WriteWord(writeOffset, (ushort)dValues[strokeIndex]);
            }
        }
        /* End of LifeCycleTransitionCount functions
         */

        /*
         * The LifeCycleState is decoded/encoded using 20 words that are intially written with some combination of Ax/Bx values.
         * Those values are organized in such a way that by advncing the state we consume the Ax val by overwriting it by Bx val,
         * future transition to an allowed state is not possible without knowing the Ax values.
         */
        private bool TryDecodeLifeCycleState(out OpenTitan_LifeCycleState state)
        {
            var lifeCycleState = GetOtpItem(OtpItem.LifeCycleState);
            if(lifeCycleState.All(x => x == 0))
            {
                state = OpenTitan_LifeCycleState.Raw;
                return true;
            }
            var positionsConsumed = GetConsumedPositionsMap(aValues, bValues, lifeCycleState);

            if(!positionsConsumedToLifeCycleState.ContainsKey(positionsConsumed))
            {
                this.Log(LogLevel.Error, "Unable to convert the consumed positions [{0}] to the LifeCycleState", Convert.ToString(positionsConsumed, 2).PadLeft(4, '0'));
                state = default(OpenTitan_LifeCycleState);
                return false;
            }
            state = positionsConsumedToLifeCycleState[positionsConsumed];
            return true;
        }

        private void EncodeLifeCycleState(OpenTitan_LifeCycleState state)
        {
            var positionsConsumed = positionsConsumedToLifeCycleState.FirstOrDefault(x => x.Value == state).Key;
            var mask = 1 << (aValues.Length - 1);
            lock(memoryLock)
            {
                for(int index = 0; index < aValues.Length; index++)
                {
                    var currentPositionConsumed = (positionsConsumed & mask) != 0;
                    var writeOffset = (uint)OtpItem.LifeCycleState + index * 2;
                    underlyingMemory.WriteWord(writeOffset, currentPositionConsumed ? bValues[index] : aValues[index]);
                    positionsConsumed <<= 1;
                }
            }
        }

        private void InitPositionConsumedToLifeCycleMapping()
        {
            positionsConsumedToLifeCycleState = new Dictionary<uint, OpenTitan_LifeCycleState>();
            positionsConsumedToLifeCycleState.Add(0x80000, OpenTitan_LifeCycleState.TestUnlocked0);
            positionsConsumedToLifeCycleState.Add(0xC0000, OpenTitan_LifeCycleState.TestLocked0);
            positionsConsumedToLifeCycleState.Add(0xE0000, OpenTitan_LifeCycleState.TestUnlocked1);
            positionsConsumedToLifeCycleState.Add(0xF0000, OpenTitan_LifeCycleState.TestLocked1);
            positionsConsumedToLifeCycleState.Add(0xF8000, OpenTitan_LifeCycleState.TestUnlocked2);
            positionsConsumedToLifeCycleState.Add(0xFC000, OpenTitan_LifeCycleState.TestLocked2);
            positionsConsumedToLifeCycleState.Add(0xFE000, OpenTitan_LifeCycleState.TestUnlocked3);
            positionsConsumedToLifeCycleState.Add(0xFF000, OpenTitan_LifeCycleState.TestLocked3);
            positionsConsumedToLifeCycleState.Add(0xFF800, OpenTitan_LifeCycleState.TestUnlocked4);
            positionsConsumedToLifeCycleState.Add(0xFFC00, OpenTitan_LifeCycleState.TestLocked4);
            positionsConsumedToLifeCycleState.Add(0xFFE00, OpenTitan_LifeCycleState.TestUnlocked5);
            positionsConsumedToLifeCycleState.Add(0xFFF00, OpenTitan_LifeCycleState.TestLocked5);
            positionsConsumedToLifeCycleState.Add(0xFFF80, OpenTitan_LifeCycleState.TestUnlocked6);
            positionsConsumedToLifeCycleState.Add(0xFFFC0, OpenTitan_LifeCycleState.TestLocked6);
            positionsConsumedToLifeCycleState.Add(0xFFFE0, OpenTitan_LifeCycleState.TestUnlocked7);
            positionsConsumedToLifeCycleState.Add(0xFFFF0, OpenTitan_LifeCycleState.Dev);
            positionsConsumedToLifeCycleState.Add(0xFFFE8, OpenTitan_LifeCycleState.Prod);
            positionsConsumedToLifeCycleState.Add(0xFFFE4, OpenTitan_LifeCycleState.Prod_end);
            positionsConsumedToLifeCycleState.Add(0xFFFFB, OpenTitan_LifeCycleState.Rma);
            positionsConsumedToLifeCycleState.Add(0xFFFFF, OpenTitan_LifeCycleState.Scrap);
        }

        /* End of LifeCycleState related functions
         */

        private uint GetConsumedPositionsMap(ushort[] unconsumedValues, ushort[] consumedValues, byte[] array)
        {
            var valuesCount = consumedValues.Length;
            if(valuesCount != unconsumedValues.Length)
            {
                throw new ArgumentException($"Both unconsumed and consumed values arrays have to be the same length! Unconsumed values length : {unconsumedValues.Length}, consumed values length: {valuesCount}");
            }
            uint output = 0;
            for(int index = 0; index < valuesCount; index++)
            {
                var current = (array[index * 2 + 1] << 8) | array[index * 2];
                if(current == unconsumedValues[index])
                {
                    output |= 0;
                }
                else if(current == consumedValues[index])
                {
                    output |= 1;
                }
                else
                {
                    throw new ArgumentException("Given value is now equal to any of the allowed states");
                }

                if(index < (valuesCount - 1))
                {
                    output <<= 1;
                }
            }
            return output;
        }

        private IFlagRegisterField creatorPartitionUnlockedFlag;
        private IFlagRegisterField daiIdleFlag;
        private IFlagRegisterField ownerPartitionUnlockedFlag;
        private IFlagRegisterField vendorPartitionUnlockedFlag;
        private IValueRegisterField accessAddress;
        private IValueRegisterField readData0;
        private IValueRegisterField readData1;

        private IEnumRegisterField<Error> vendorPartitionError;
        private IEnumRegisterField<Error> creatorPartitionError;
        private IEnumRegisterField<Error> ownerPartitionError;
        private IEnumRegisterField<Error> hardwarePartitionError;
        private IEnumRegisterField<Error> secret0PartitionError;
        private IEnumRegisterField<Error> secret1PartitionError;
        private IEnumRegisterField<Error> secret2PartitionError;
        private IEnumRegisterField<Error> lifeCyclePartitionError;
        private IEnumRegisterField<Error> daiError;

        private IFlagRegisterField vendorPartitionErrorFlag;
        private IFlagRegisterField creatorPartitionErrorFlag;
        private IFlagRegisterField ownerPartitionErrorFlag;
        private IFlagRegisterField hardwarePartitionErrorFlag;
        private IFlagRegisterField secret0PartitionErrorFlag;
        private IFlagRegisterField secret1PartitionErrorFlag;
        private IFlagRegisterField secret2PartitionErrorFlag;
        private IFlagRegisterField lifeCyclePartitionErrorFlag;
        private IFlagRegisterField daiErrorFlag;

        private Dictionary<uint, OpenTitan_LifeCycleState> positionsConsumedToLifeCycleState;
        private OpenTitan_LifeCycleState? cachedLifeCycleState;
        private ushort? cachedTransitionCount;
        private object memoryLock;
        private object transitionCountLock;
        private ushort[] aValues;
        private ushort[] bValues;
        private ushort[] cValues;
        private ushort[] dValues;

        private readonly ArrayMemory underlyingMemory;

        private const int MaximumTransitionsCount = 24;
        private const int ABValuesWordsCount = 20;
        private const int CDValuesWordsCount = 24;

        public enum Registers
        {
            InterruptState = 0x0,
            InterruptEnable = 0x4,
            InterruptTest = 0x8,
            AlertTest = 0xc,
            Status = 0x10,
            ErrorCode = 0x14,
            DirectAccesssRegisterEnable = 0x18,
            DirectAccessCommand = 0x1C,
            DirectAccessAddress = 0x20,
            DirectAccessWriteData_0 = 0x24,
            DirectAccessWriteData_1 = 0x28,
            DirectAccessReadData_0 = 0x2C,
            DirectAccessReadData_1 = 0x30,
            CheckTriggerRegisterWriteEnable = 0x34,
            CheckTrigger = 0x38,
            CheckRegistersWriteEnable = 0x3c,
            CheckTimeout = 0x40,
            IntegrityCheckPeriod = 0x44,
            ConsistencyCheckPeriod = 0x48,
            VendorTestReadLock = 0x4C,
            CreatorSoftwareConfigReadLock = 0x50,
            OwnerSoftwareConfigReadLock = 0x54,
            VendorTestDigest0 = 0x58,
            VendorTestDigest1 = 0x5C,
            CreatorSoftwareConfigDigest0 = 0x60,
            CreatorSoftwareConfigDigest1 = 0x64,
            OwnerSoftwareConfigDigest0 = 0x68,
            OwnerSoftwareConfigDigest1 = 0x6C,
            HardwareConfigDigest0 = 0x70,
            HardwareConfigDigest1 = 0x74,
            Secret0Digest0 = 0x78,
            Secret0Digest1 = 0x7C,
            Secret1Digest0 = 0x80,
            Secret1Digest1 = 0x84,
            Secret2Digest0 = 0x88,
            Secret2Digest1 = 0x8C,
            SoftwareConfiguredWindowStart = 0x1000,
        }

        public enum OtpItem
        {
            [ItemSizeAttribute(56)]
            Scratch = 0x000,

            [ItemSizeAttribute(8, is64Bit: true)]
            VendorTestDigest = 0x038,

            [ItemSizeAttribute(156)]
            CreatorSoftwareConfigAstConfig = 0x040,

            [ItemSizeAttribute(4)]
            CreatorSoftwareConfigAstInitEnable = 0x0DC,

            [ItemSizeAttribute(4)]
            CreatorSoftwareConfigRomExtSku = 0x0E0,

            [ItemSizeAttribute(4)]
            CreatorSoftwareConfigUseSwRsaVerify = 0x0E4,

            [ItemSizeAttribute(8)]
            CreatorSoftwareConfigKeyIsValid = 0x0E8,

            [ItemSizeAttribute(4)]
            CreatorSoftwareConfigFlashDataDefaultConfig = 0x0F0,

            [ItemSizeAttribute(4)]
            CreatorSoftwareConfigFlashInfoBootDataConfig = 0x0F4,

            [ItemSizeAttribute(4)]
            CreatorSoftwareConfigRngEnable = 0x0F8,

            [ItemSizeAttribute(4)]
            CreatorSoftwareConfigJitterEnable = 0x0FC,

            [ItemSizeAttribute(4)]
            CreatorSoftwareConfigRetRamResetMask = 0x100,

            [ItemSizeAttribute(8, is64Bit: true)]
            CreatorSoftwareConfigDigest = 0x358,

            [ItemSizeAttribute(4)]
            RomErrorReporting = 0x360,

            [ItemSizeAttribute(4)]
            RomBootstrapEnable = 0x364,

            [ItemSizeAttribute(4)]
            RomFaultResponse = 0x368,

            [ItemSizeAttribute(4)]
            RomAlertClassEnable = 0x36C,

            [ItemSizeAttribute(4)]
            RomAlertEscalation = 0x370,

            [ItemSizeAttribute(320)]
            RomAlertClassification = 0x374,

            [ItemSizeAttribute(64)]
            RomLocalAlertClassification = 0x4B4,

            [ItemSizeAttribute(16)]
            RomAlertAccumThresh = 0x4F4,

            [ItemSizeAttribute(16)]
            RomAlertTimeoutCycles = 0x504,

            [ItemSizeAttribute(64)]
            RomAlertPhaseCycles = 0x514,

            [ItemSizeAttribute(4)]
            RomWatchdogBiteThresholdCycles = 0x554,

            [ItemSizeAttribute(8, is64Bit: true)]
            OwnerSwCfgDigest = 0x678,

            [ItemSizeAttribute(32)]
            DeviceId = 0x680,

            [ItemSizeAttribute(32)]
            ManufacturingState = 0x6A0,

            [ItemSizeAttribute(1)]
            EnableSramIfetch = 0x6C0,

            [ItemSizeAttribute(1)]
            EnableCsrngSwAppRead = 0x6C1,

            [ItemSizeAttribute(1)]
            EnableEntropySrcFwRead = 0x6C2,

            [ItemSizeAttribute(1)]
            EnableEntropySrcFwOver = 0x6C3,

            [ItemSizeAttribute(8, is64Bit: true)]
            HwCfgDigest = 0x6C8,

            [ItemSizeAttribute(16, is64Bit: true)]
            TestUnlockToken = 0x6D0,

            [ItemSizeAttribute(16, is64Bit: true)]
            TestExitToken = 0x6E0,

            [ItemSizeAttribute(8, is64Bit: true)]
            Secret0Digest = 0x6F0,

            [ItemSizeAttribute(32, is64Bit: true)]
            FlashAddressKeySeed = 0x6F8,

            [ItemSizeAttribute(32, is64Bit: true)]
            FlashDataKeySeed = 0x718,

            [ItemSizeAttribute(16, is64Bit: true)]
            SramDataKeySeed = 0x738,

            [ItemSizeAttribute(8, is64Bit: true)]
            Secret1Digest = 0x748,

            [ItemSizeAttribute(16, is64Bit: true)]
            RmaToken = 0x750,

            [ItemSizeAttribute(32, is64Bit: true)]
            CreatorRootKeyShare0 = 0x760,

            [ItemSizeAttribute(32, is64Bit: true)]
            CreatorRootKeyShare1 = 0x780,

            [ItemSizeAttribute(8, is64Bit: true)]
            Secret2Digest = 0x7A0,

            [ItemSizeAttribute(48)]
            LifeCycleTransitionCount = 0x7A8,

            [ItemSizeAttribute(40)]
            LifeCycleState = 0x7D8,
        }

        private enum OtpPartition
        {
            VendorTest = OtpItem.Scratch,
            CreatorSoftwareConfig = OtpItem.CreatorSoftwareConfigAstConfig,
            OwnerSoftwareConfig = OtpItem.RomErrorReporting,
            HardwareConfig = OtpItem.DeviceId,
            Secret0 = OtpItem.TestUnlockToken,
            Secret1 = OtpItem.FlashAddressKeySeed,
            Secret2 = OtpItem.RmaToken,
            LifeCycle = OtpItem.LifeCycleTransitionCount,
        }

        private enum Error
        {
            No = 0x0,
            Macro = 0x1,
            MacroCorrectable = 0x2,
            MacroUncorrectable = 0x3,
            MacroWriteBlank = 0x4,
            Access = 0x5,
            CheckFail = 0x6,
            FsmState = 0x7,
        }

        private class ItemSizeAttribute: Attribute
        {
            public ItemSizeAttribute(int byteLength, bool is64Bit = false)
            {
                Is64Bit = is64Bit;
                ByteLength = byteLength;
            }
            public bool Is64Bit { get; }
            public int ByteLength { get; }
        }
    }
}
