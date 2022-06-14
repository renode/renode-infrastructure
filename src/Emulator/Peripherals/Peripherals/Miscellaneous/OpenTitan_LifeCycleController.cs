//
// Copyright (c) 2010-2022 Antmicro
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
using Org.BouncyCastle.Crypto.Digests;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_LifeCycleController: BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_LifeCycleController(Machine machine, OpenTitan_ResetManager resetManager, OpenTitan_OneTimeProgrammableMemoryController otpController) : base(machine)
        {
            this.resetManager = resetManager;
            this.otpController = otpController;

            rmaToken = new byte[TokenRegistersCount * 4] ;
            deviceId = new byte[DeviceIdRegistersCount * 4];
            testExitToken = new byte[TokenRegistersCount * 4] ;
            testUnlockToken = new byte[TokenRegistersCount * 4] ;
            token = new byte[TokenRegistersCount * 4];

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            Array.Clear(token, 0, token.Length);
            readyFlag.Value = true;
        }

        public long Size => 0x1000;

        public string TestExitToken
        {
            set
            {
                testExitToken = Misc.HexStringToByteArray(value);
            }
        }

        public string TestUnlockToken
        {
            set
            {
                testUnlockToken = Misc.HexStringToByteArray(value);
            }
        }

        public string RMAToken
        {
            set
            {
                rmaToken = Misc.HexStringToByteArray(value);
            }
        }

        public string DeviceId
        {
            get
            {
                return deviceIdString;
            }
            set
            {
                // This array was reversed to comply with the OpenTitan tests suite - the spec does not specify this
                deviceId = Misc.HexStringToByteArray(value).Reverse().ToArray();
                var otpDeviceId = otpController.GetOtpItem(OpenTitan_OneTimeProgrammableMemoryController.OtpItem.DeviceId);
                if(deviceId != otpDeviceId)
                {
                    this.Log(LogLevel.Warning, "The set DeviceId differs from the one stored in the OTP Controller");
                }
                deviceIdString = value;
            }
        }

        private void DefineRegisters()
        {
            Registers.AlertTest.Define(this)
                .WithTaggedFlag("fatal_prog_error", 0)
                .WithTaggedFlag("fatal_state_error", 1)
                .WithTaggedFlag("fatal_bus_integ_error", 2)
                .WithIgnoredBits(3, 32 - 3);

            Registers.Status.Define(this)
                .WithFlag(0, out readyFlag, FieldMode.Read, name: "READY")
                .WithFlag(1, out transitionSuccesfulFlag, FieldMode.Read, name: "TRANSITION_SUCCESSFUL")
                .WithFlag(2, out transitionCountErorFlag, FieldMode.Read, name: "TRANSITION_COUNT_ERROR")
                .WithFlag(3, out transitionErrorFlag, FieldMode.Read, name: "TRANSITION_ERROR")
                .WithFlag(4, out tokenErrorFlag, FieldMode.Read, name: "TOKEN_ERROR")
                .WithTaggedFlag("FLASH_RMA_ERROR", 5)
                .WithTaggedFlag("OTP_ERROR", 6)
                .WithTaggedFlag("STATE_ERROR", 7)
                .WithTaggedFlag("OTP_PARTITION_ERROR", 8)
                .WithIgnoredBits(9, 32 - 9);

            Registers.ClaimTransitionIf.Define(this, 0x69)
                .WithEnumField<DoubleWordRegister, MutexState>(0, 8, out mutexState,
                    writeCallback: (_, val) =>
                    {
                        if(val == MutexState.MultiBitTrue)
                        {
                            if(mutexClaimed)
                            {
                                mutexState.Value = MutexState.Taken;
                            }
                            else
                            {
                                mutexClaimed = true;
                                transitionRegisterWriteEnable.Value = true;
                            }
                        }
                    }, name: "MUTEX")
                .WithIgnoredBits(8, 32 - 8);

            Registers.TransitionRegisterWriteEnable.Define(this)
                .WithFlag(0, out transitionRegisterWriteEnable, name: "TRANSITION_REGWEN")
                .WithIgnoredBits(1, 32 - 1);

            Registers.TransitionCommand.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, writeCallback: (_, val) => { if(val) ExecuteTransition(); }, name: "START")
                .WithIgnoredBits(1, 32 - 1);

            Registers.TransitionControl.Define(this)
                .WithFlag(0, name: "EXT_CLOCK_EN")
                .WithIgnoredBits(1, 32 - 1);

            Registers.TransitionToken0.DefineMany(this, TokenRegistersCount, (register, idx) =>
            {
                register.WithValueField(0, 32,
                                        writeCallback: (_, val) => Misc.ByteArrayWrite(idx * 4, val, token),
                                        valueProviderCallback: (_) => Misc.ByteArrayRead(idx * 4, token), name: $"TRANSITION_TOKEN_{idx}");
            });

            Registers.TransitionTarget.Define(this)
                .WithEnumField<DoubleWordRegister, TransitionTargetState>(0, 30, out transitionTarget, name: "STATE")
                .WithIgnoredBits(30, 2);

            Registers.OtpVendorTestControl.Define(this)
                .WithValueField(0, 32, name: "OTP_VENDOR_TEST_CTRL");

            Registers.OtpVendorTestStatus.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "OTP_VENDOR_TEST_STATUS");

            Registers.OpenTitan_LifeCycleState.Define(this)
                .WithEnumField<DoubleWordRegister, OpenTitan_LifeCycleState>(0, 30, FieldMode.Read, valueProviderCallback: _ => otpController.LifeCycleState, name: "STATE")
                .WithIgnoredBits(30, 32 - 30);

            Registers.LifeCycleTransitionCounter.Define(this)
                .WithValueField(0, 5, FieldMode.Read, valueProviderCallback: _ => otpController.LifeCycleTransitionCount, name: "COUNT")
                .WithIgnoredBits(5, 32 - 5);

            Registers.LifeCycleIdState.Define(this)
                .WithEnumField<DoubleWordRegister, IdState>(0, 32, FieldMode.Read, name: "STATE");

            Registers.DeviceId0.DefineMany(this, DeviceIdRegistersCount, (register, idx) =>
            {
                register.WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => Misc.ByteArrayRead(idx * 4, deviceId), name: $"DEVICE_ID_{idx}");
            });

            Registers.ManufacturingState0.DefineMany(this, ManufacturingStateRegistersCount, (register, idx) =>
            {
                register.WithTag($"MANUF_STATE_{idx}", 0, 32);
            });
        }

        private void ExecuteTransition()
        {
            var currentState = otpController.LifeCycleState;
            var nextState = (OpenTitan_LifeCycleState)transitionTarget.Value;

            ClearStatusFlags();

            // Every transition attempt increments the count
            otpController.IncrementTransitionCount();

            if(!IsTransitionAllowed(currentState, nextState))
            {
                transitionErrorFlag.Value = true;
                this.Log(LogLevel.Warning, "Transitions between states {0} and {1} is not allowed.", currentState, nextState);
                return;
            }
            if(!IsTransitionTokenValid(currentState, nextState))
            {
                tokenErrorFlag.Value = true;
                this.Log(LogLevel.Warning, "Invalid token for transition between states {0} and {1}.", currentState, nextState);
                return;
            }

            otpController.LifeCycleState = nextState;
            resetManager.LifeCycleReset();
            transitionSuccesfulFlag.Value = true;
        }

        private void ClearStatusFlags()
        {
            transitionSuccesfulFlag.Value = false;
            transitionCountErorFlag.Value = false;
            transitionErrorFlag.Value = false;
            tokenErrorFlag.Value = false;
        }

        private bool IsTransitionAllowed(OpenTitan_LifeCycleState currentState, OpenTitan_LifeCycleState nextState)
        {
            if(nextState == OpenTitan_LifeCycleState.Scrap ||
               currentState == OpenTitan_LifeCycleState.Raw && UnlockedTestStates.Contains(nextState))
            {
                return true;
            }
            else if(UnlockedTestStates.Contains(currentState))
            {
                if(LockedTestStates.Contains(nextState) && (nextState > currentState) ||
                   nextState == OpenTitan_LifeCycleState.Rma ||
                   MissionStates.Contains(nextState))
                {
                    return true;
                }
            }
            else if((currentState == OpenTitan_LifeCycleState.Dev || currentState == OpenTitan_LifeCycleState.Prod) &&
                    nextState == OpenTitan_LifeCycleState.Rma)
            {
                return true;
            }
            return false;
        }

        private bool IsTransitionTokenValid(OpenTitan_LifeCycleState currentState, OpenTitan_LifeCycleState nextState)
        {
            if(LockedTestStates.Contains(currentState) && UnlockedTestStates.Contains(nextState))
            {
                return token.SequenceEqual(testUnlockToken);
            }
            else if(UnlockedTestStates.Contains(currentState) && MissionStates.Contains(nextState))
            {
                return token.SequenceEqual(testExitToken);
            }
            else if(MissionStates.Contains(currentState) && nextState == OpenTitan_LifeCycleState.Rma)
            {
                return token.SequenceEqual(rmaToken);
            }
            // No token required for this transition
            return true;
        }

        IEnumRegisterField<TransitionTargetState> transitionTarget;
        IEnumRegisterField<MutexState> mutexState;
        IFlagRegisterField readyFlag;
        IFlagRegisterField transitionSuccesfulFlag;
        IFlagRegisterField transitionRegisterWriteEnable;
        IFlagRegisterField transitionCountErorFlag;
        IFlagRegisterField transitionErrorFlag;
        IFlagRegisterField tokenErrorFlag;

        private byte[] token;
        private byte[] rmaToken;
        private byte[] deviceId;
        private byte[] testExitToken;
        private byte[] testUnlockToken;

        private bool mutexClaimed;
        private string deviceIdString;
        private readonly OpenTitan_ResetManager resetManager;
        private readonly OpenTitan_OneTimeProgrammableMemoryController otpController;

        private readonly OpenTitan_LifeCycleState[] MissionStates = { OpenTitan_LifeCycleState.Dev, OpenTitan_LifeCycleState.Prod, OpenTitan_LifeCycleState.Prod_end };
        private readonly OpenTitan_LifeCycleState[] LockedTestStates = { OpenTitan_LifeCycleState.TestLocked0, OpenTitan_LifeCycleState.TestLocked1, OpenTitan_LifeCycleState.TestLocked2, OpenTitan_LifeCycleState.TestLocked3, OpenTitan_LifeCycleState.TestLocked4, OpenTitan_LifeCycleState.TestLocked5, OpenTitan_LifeCycleState.TestLocked6 };
        private readonly OpenTitan_LifeCycleState[] UnlockedTestStates = { OpenTitan_LifeCycleState.TestUnlocked0, OpenTitan_LifeCycleState.TestUnlocked1, OpenTitan_LifeCycleState.TestUnlocked2, OpenTitan_LifeCycleState.TestUnlocked3, OpenTitan_LifeCycleState.TestUnlocked4, OpenTitan_LifeCycleState.TestUnlocked5, OpenTitan_LifeCycleState.TestUnlocked6, OpenTitan_LifeCycleState.TestUnlocked7 };

        private const uint TokenRegistersCount = 4;
        private const uint DeviceIdRegistersCount = 8;
        private const uint ManufacturingStateRegistersCount = 8;

        #pragma warning disable format
        private enum IdState
        {
            Blank        = 0x00000000,
            Personalized = 0x11111111,
            Invalid      = 0x22222222,
        }

        private enum MutexState
        {
            MultiBitTrue  = 0x5A,
            MultiBitFalse = 0xA5,
            Taken         = 0x00,
        }

        // This is only a subset of the OpenTitan_LifeCycleState
        private enum TransitionTargetState
        {
            Raw           = OpenTitan_LifeCycleState.Raw           ,  // Raw life cycle state after fabrication where all functions are disabled.
            TestUnlocked0 = OpenTitan_LifeCycleState.TestUnlocked0 ,  // Unlocked test state where debug functions are enabled.
            TestLocked0   = OpenTitan_LifeCycleState.TestLocked0   ,  // Locked test state where where all functions are disabled.
            TestUnlocked1 = OpenTitan_LifeCycleState.TestUnlocked1 ,  // Unlocked test state where debug functions are enabled.
            TestLocked1   = OpenTitan_LifeCycleState.TestLocked1   ,  // Locked test state where where all functions are disabled.
            TestUnlocked2 = OpenTitan_LifeCycleState.TestUnlocked2 ,  // Unlocked test state where debug functions are enabled.
            TestLocked2   = OpenTitan_LifeCycleState.TestLocked2   ,  // Locked test state where debug all functions are disabled.
            TestUnlocked3 = OpenTitan_LifeCycleState.TestUnlocked3 ,  // Unlocked test state where debug functions are enabled.
            TestLocked3   = OpenTitan_LifeCycleState.TestLocked3   ,  // Locked test state where debug all functions are disabled.
            TestUnlocked4 = OpenTitan_LifeCycleState.TestUnlocked4 ,  // Unlocked test state where debug functions are enabled.
            TestLocked4   = OpenTitan_LifeCycleState.TestLocked4   ,  // Locked test state where debug all functions are disabled.
            TestUnlocked5 = OpenTitan_LifeCycleState.TestUnlocked5 ,  // Unlocked test state where debug functions are enabled.
            TestLocked5   = OpenTitan_LifeCycleState.TestLocked5   ,  // Locked test state where debug all functions are disabled.
            TestUnlocked6 = OpenTitan_LifeCycleState.TestUnlocked6 ,  // Unlocked test state where debug functions are enabled.
            TestLocked6   = OpenTitan_LifeCycleState.TestLocked6   ,  // Locked test state where debug all functions are disabled.
            TestUnlocked7 = OpenTitan_LifeCycleState.TestUnlocked7 ,  // Unlocked test state where debug functions are enabled.
            Dev           = OpenTitan_LifeCycleState.Dev           ,  // Development life cycle state where limited debug functionality is available.
            Prod          = OpenTitan_LifeCycleState.Prod          ,  // Production life cycle state.
            Prod_end      = OpenTitan_LifeCycleState.Prod_end      ,  // Same as PROD, but transition into RMA is not possible from this state.
            Rma           = OpenTitan_LifeCycleState.Rma           ,  // RMA life cycle state.
            Scrap         = OpenTitan_LifeCycleState.Scrap         ,  // SCRAP life cycle state where all functions are disabled.
        }

        public enum Registers
        {
            AlertTest                     = 0x00,
            Status                        = 0x04,
            ClaimTransitionIf             = 0x08,
            TransitionRegisterWriteEnable = 0x0C,
            TransitionCommand             = 0x10,
            TransitionControl             = 0x14,
            TransitionToken0              = 0x18,
            TransitionToken1              = 0x1C,
            TransitionToken2              = 0x20,
            TransitionToken3              = 0x24,
            TransitionTarget              = 0x28,
            OtpVendorTestControl          = 0x2C,
            OtpVendorTestStatus           = 0x30,
            OpenTitan_LifeCycleState                = 0x34,
            LifeCycleTransitionCounter    = 0x38,
            LifeCycleIdState              = 0x3C,
            DeviceId0                     = 0x40,
            DeviceId1                     = 0x44,
            DeviceId2                     = 0x48,
            DeviceId3                     = 0x4C,
            DeviceId4                     = 0x50,
            DeviceId5                     = 0x54,
            DeviceId6                     = 0x58,
            DeviceId7                     = 0x5C,
            ManufacturingState0           = 0x60,
            ManufacturingState1           = 0x64,
            ManufacturingState2           = 0x68,
            ManufacturingState3           = 0x6C,
            ManufacturingState4           = 0x70,
            ManufacturingState5           = 0x74,
            ManufacturingState6           = 0x78,
            ManufacturingState7           = 0x7C,
        }
        #pragma warning restore format
    }
}
