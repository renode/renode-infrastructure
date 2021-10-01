//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class OpenTitan_KeyManagerTest
    {
        [SetUp]
        public void Setup()
        {
            this.machine = new Machine();
            this.peripheral = new OpenTitan_KeyManager(machine, "0x17a9838dd4cd7f1bdce673b937a6d75202fedbf893bf7d52c8a744ad83d2630b" ,0);
        }

        [Test]
        public void ShouldInitToResetState()
        {
            Assert.AreEqual(OpenTitan_KeyManager.WorkingState.Reset, GetWorkingState());
        }

        [Test]
        public void ShouldAdvanceStateProperly()
        {
            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);
            Assert.AreEqual(OpenTitan_KeyManager.WorkingState.Init, GetWorkingState());
            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);
            Assert.AreEqual(OpenTitan_KeyManager.WorkingState.CreatorRootKey, GetWorkingState());
            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);
            Assert.AreEqual(OpenTitan_KeyManager.WorkingState.OwnerIntermediateKey, GetWorkingState());
            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);
            Assert.AreEqual(OpenTitan_KeyManager.WorkingState.OwnerKey, GetWorkingState());
            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);
            Assert.AreEqual(OpenTitan_KeyManager.WorkingState.Disabled, GetWorkingState());
            // Should not advance from `Disabled`
            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);
            Assert.AreEqual(OpenTitan_KeyManager.WorkingState.Disabled, GetWorkingState());
        }

        [Test]
        public void ShouldIgnoreOtherCommandsInResetState()
        {
            peripheral.Reset();
            Disable();
            AssertErrorIsRaised(false);
            SendCommand(OpenTitan_KeyManager.OperationMode.GenerateID);
            AssertErrorIsRaised(false);
        }

        [Test]
        public void ShouldClearErrorsAfterValidOperation()
        {
            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);
            SendCommand(OpenTitan_KeyManager.OperationMode.GenerateID);
            AssertErrorIsRaised(true, "GenerateID in Init state");
            // Clear Errors
            peripheral.WriteDoubleWord((long)OpenTitan_KeyManager.Registers.ErrorCode, 0x7);
            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);
            AssertErrorIsRaised(false, "Advance in Init state");
        }

        [Test]
        public void ShouldRaiseErrorOnIllegalCommand()
        {
            peripheral.Reset();
            //Init
            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);

            SendCommand(OpenTitan_KeyManager.OperationMode.GenerateID);
            AssertErrorIsRaised(true, $"{OpenTitan_KeyManager.OperationMode.GenerateID} in Init state");
            SendCommand(OpenTitan_KeyManager.OperationMode.GenerateSWOutput);
            AssertErrorIsRaised(true, $"{OpenTitan_KeyManager.OperationMode.GenerateSWOutput} in Init state");
            SendCommand(OpenTitan_KeyManager.OperationMode.GenerateHWOutput);
            AssertErrorIsRaised(true, $"{OpenTitan_KeyManager.OperationMode.GenerateHWOutput} in Init state");

            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);
            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);
            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);
            //Disabled
            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);

            SendCommand(OpenTitan_KeyManager.OperationMode.Advance);
            AssertErrorIsRaised(true, $"{OpenTitan_KeyManager.OperationMode.Advance} in Disabled state");
            SendCommand(OpenTitan_KeyManager.OperationMode.Disable);
            AssertErrorIsRaised(true, $"{OpenTitan_KeyManager.OperationMode.Disable} in Disabled state");
            SendCommand(OpenTitan_KeyManager.OperationMode.GenerateID);
            AssertErrorIsRaised(true, $"{OpenTitan_KeyManager.OperationMode.GenerateID} in Disabled state");
            SendCommand(OpenTitan_KeyManager.OperationMode.GenerateSWOutput);
            AssertErrorIsRaised(true, $"{OpenTitan_KeyManager.OperationMode.GenerateSWOutput} in Disabled state");
            SendCommand(OpenTitan_KeyManager.OperationMode.GenerateHWOutput);
            AssertErrorIsRaised(true, $"{OpenTitan_KeyManager.OperationMode.GenerateHWOutput} in Disabled state");
        }

        private void SendCommand(OpenTitan_KeyManager.OperationMode command)
        {
            peripheral.WriteDoubleWord((long)OpenTitan_KeyManager.Registers.OperationControls, (((uint)command) << 4 | 0x1));
        }

        private void Disable()
        {
            SendCommand(OpenTitan_KeyManager.OperationMode.Disable);
        }

        private OpenTitan_KeyManager.WorkingState GetWorkingState()
        {
            return (OpenTitan_KeyManager.WorkingState)peripheral.ReadDoubleWord((long)OpenTitan_KeyManager.Registers.WorkingState);
        }

        private void AssertErrorIsRaised(bool errorExpected, string message = "")
        {
            if(errorExpected)
            {
                Assert.AreNotEqual(0x0, ReadError(), message);
                Assert.AreEqual(0x3 , ReadStatus(), message);
            }
            else
            {
                Assert.AreEqual(0x0, ReadError(), message);
                Assert.AreNotEqual(0x3 , ReadStatus(), message);
            }
        }

        private uint ReadError()
        {
            return peripheral.ReadDoubleWord((long)OpenTitan_KeyManager.Registers.ErrorCode);
        }

        private uint ReadStatus()
        {
            return peripheral.ReadDoubleWord((long)OpenTitan_KeyManager.Registers.Status);
        }

        private Machine machine;
        private OpenTitan_KeyManager peripheral;
    }
}
