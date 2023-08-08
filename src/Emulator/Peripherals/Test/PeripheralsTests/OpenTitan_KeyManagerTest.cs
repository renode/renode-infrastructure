//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.MemoryControllers;
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
            var rom = new MappedMemory(machine, 0x100);
            var rom_ctrl = new OpenTitan_ROMController(rom, "0000000000000000", "0000000000000000");
            this.peripheral = new OpenTitan_KeyManager(machine, rom_ctrl,
                deviceId: "fa53b8058e157cb69f1f413e87242971b6b52a656a1cab7febf21e5bf1f45edd",
                lifeCycleDiversificationConstant: "6faf88f22bccd612d1c09f5c02b2c8d1",
                creatorKey: "9152e32c9380a4bcc3e0ab263581e6b0e8825186e1e445631646e8bef8c45d47",
                ownerKey: "fa365df52da48cd752fb3a026a8e608f0098cfe5fa9810494829d0cd9479eb78",
                rootKey: "efb7ea7ee90093cf4affd9aaa2d6c0ec446cfdf5f2d5a0bfd7e2d93edc63a10256d24a00181de99e0f690b447a8dde2a1ffb8bc306707107aa6e2410f15cfc37",
                softOutputSeed: "df273097a573a411332efd86009bd0a175f08814ecc17ab02cc1e3404e1cd8bf",
                hardOutputSeed: "69582e71443c8be0fc00de9d9734c3fe7f4266d10a752de74814f2a3079f69a3",
                destinationNoneSeed: "73e5bc251b143b74476e576754125d61930d203f199a87c123c074e020fd5028",
                destinationAesSeed: "ce44cbff5e09e6dd3ae54e9e45da6e662fb69c3aab936b415a0d6e7185eaa2e0",
                destinationOtbnSeed: "fcc581b66ae11d33f678e7d227881bcfe58a331208f189de6265edc8fde06db0",
                destinationKmacSeed: "b76a8aff9e4da0e3ff9f3036fd9c13ac08496db56fbc4894d38bd8674f4b542d",
                revisionSeed: "17a9838dd4cd7f1bdce673b937a6d75202fedbf893bf7d52c8a744ad83d2630b",
                creatorIdentitySeed: "c20c05a20251023541544776930be76bfbb22e1d8aaa4783f2b5e094e3e8d3f8",
                ownerIntermediateIdentitySeed: "93cdb1d9a6a60050ef0d8a166d91200dc6757907237df4401908799dfa1fe8f2",
                ownerIdentitySeed: "a88601ca1695a7c8c5d32486aac4e086628d6c8ca138f65d25dfa5f9c912f354"
            );
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

        private IMachine machine;
        private OpenTitan_KeyManager peripheral;
    }
}
