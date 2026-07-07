//
// Copyright (c) 2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;

using NUnit.Framework;

using RCC = Antmicro.Renode.Peripherals.Miscellaneous.STM32H7_RCC;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class STM32H7_RCC_TEST
    {
        [Test]
        public void ShouldPreventDisablingHsi()
        {
            WriteToPeripheral(RCC.Registers.ClockControl, 0);
            var read = ReadFromPeripheral(RCC.Registers.ClockControl);

            Assert.AreEqual(read, ClockControlHsion | ClockControlHsirdy | ClockControlHsidivf);
        }

        [Test]
        public void ShouldPreventDisablingCsi()
        {
            var read = ReadFromPeripheral(RCC.Registers.ClockControl);

            WriteToPeripheral(RCC.Registers.ClockControl, read | ClockControlCsion);
            // Select CSI as sys_ck
            WriteToPeripheral(RCC.Registers.ClockConfiguration, 0b01);
            // Try to disable everyting
            WriteToPeripheral(RCC.Registers.ClockControl, 0);
            read = ReadFromPeripheral(RCC.Registers.ClockControl);

            Assert.AreEqual(read, ClockControlCsion | ClockControlCsirdy | ClockControlHsidivf);
        }

        [Test]
        public void ShouldPreventDisablingHse()
        {
            var read = ReadFromPeripheral(RCC.Registers.ClockControl);

            WriteToPeripheral(RCC.Registers.ClockControl, read | ClockControlHseon);
            // Select HSE as sys_ck
            WriteToPeripheral(RCC.Registers.ClockConfiguration, 0b10);
            // Try to disable everyting
            WriteToPeripheral(RCC.Registers.ClockControl, 0);

            read = ReadFromPeripheral(RCC.Registers.ClockControl);

            Assert.AreEqual(read, ClockControlHseon | ClockControlHserdy | ClockControlHsidivf);
        }

        [Test]
        public void ShouldPreventDisablingPll1()
        {
            var read = ReadFromPeripheral(RCC.Registers.ClockControl);

            WriteToPeripheral(RCC.Registers.ClockControl, read | ClockControlPll1on);
            // Select HSE as sys_ck
            WriteToPeripheral(RCC.Registers.ClockConfiguration, 0b11);
            // Try to disable everyting
            WriteToPeripheral(RCC.Registers.ClockControl, 0);

            read = ReadFromPeripheral(RCC.Registers.ClockControl);

            // PLL1 and HSI has to stay on since HSI is the default reference clock for PLL1
            Assert.AreEqual(read, ClockControlPll1on | ClockControlPll1rdy | ClockControlHsidivf | ClockControlHsion | ClockControlHsirdy);
        }

        [Test]
        public void ShouldPreventPllsModificationWhileRunning()
        {
            var read = ReadFromPeripheral(RCC.Registers.ClockControl);
            WriteToPeripheral(RCC.Registers.ClockControl, read | ClockControlPll1on);

            var before = ReadFromPeripheral(RCC.Registers.PLLClockSourceSelect);
            // Try to change the source while a PLL is running (selected source is arbitrary)
            WriteToPeripheral(RCC.Registers.PLLClockSourceSelect, before | 0b1);
            var after = ReadFromPeripheral(RCC.Registers.PLLClockSourceSelect);

            Assert.AreEqual(before, after);

            WriteToPeripheral(RCC.Registers.ClockControl, read | ClockControlPll2on);

            before = ReadFromPeripheral(RCC.Registers.PLLClockSourceSelect);
            WriteToPeripheral(RCC.Registers.PLLClockSourceSelect, before | 0b10);
            after = ReadFromPeripheral(RCC.Registers.PLLClockSourceSelect);

            Assert.AreEqual(before, after);

            WriteToPeripheral(RCC.Registers.ClockControl, read | ClockControlPll3on);

            before = ReadFromPeripheral(RCC.Registers.PLLClockSourceSelect);
            WriteToPeripheral(RCC.Registers.PLLClockSourceSelect, before | 0b11);
            after = ReadFromPeripheral(RCC.Registers.PLLClockSourceSelect);

            Assert.AreEqual(before, after);
        }

        [Test]
        public void ShouldKeepHsecssonEnabled()
        {
            var before = ReadFromPeripheral(RCC.Registers.ClockControl);
            WriteToPeripheral(RCC.Registers.ClockControl, before | ClockControlHsecsson);
            WriteToPeripheral(RCC.Registers.ClockControl, before & ~ClockControlHsecsson);
            var after = ReadFromPeripheral(RCC.Registers.ClockControl);

            Assert.AreEqual(after & ClockControlHsecsson, ClockControlHsecsson);
        }

        [Test]
        public void ShouldPreventInvalidWritesToDIVP1()
        {
            var before = ReadFromPeripheral(RCC.Registers.PLL1DividersConfiguration);
            var divp1Mask = PLL1DividersConfigurationDivp1;

            WriteToPeripheral(RCC.Registers.PLL1DividersConfiguration, (before & ~divp1Mask) | (3u << PLL1DividersConfigurationDivp1Offset));

            var after = ReadFromPeripheral(RCC.Registers.PLL1DividersConfiguration);
            Assert.AreEqual(((3u << PLL1DividersConfigurationDivp1Offset) & divp1Mask), (after & divp1Mask));

            // Illegal write of odd division
            WriteToPeripheral(RCC.Registers.PLL1DividersConfiguration, (before & ~divp1Mask) | (2u << PLL1DividersConfigurationDivp1Offset));

            after = ReadFromPeripheral(RCC.Registers.PLL1DividersConfiguration);
            Assert.AreEqual(((3u << PLL1DividersConfigurationDivp1Offset) & divp1Mask), (after & divp1Mask));

            // Should allow to set the "no division"
            WriteToPeripheral(RCC.Registers.PLL1DividersConfiguration, before & ~divp1Mask);

            after = ReadFromPeripheral(RCC.Registers.PLL1DividersConfiguration);
            Assert.AreEqual(0u, (after & divp1Mask));
        }

        [SetUp]
        public void SetUp()
        {
            machine = new Machine();
            peripheral = new RCC(machine);
            machine.SystemBus.Register(peripheral, new Peripherals.Bus.BusRangeRegistration(new Range(PeripheralRegistrationPoint, (ulong)peripheral.Size)));
        }

        private void WriteToPeripheral(RCC.Registers register, uint value)
        {
            machine.SystemBus.WriteDoubleWord(PeripheralRegistrationPoint + (ulong)register, value);
        }

        private uint ReadFromPeripheral(RCC.Registers register)
        {
            return machine.SystemBus.ReadDoubleWord(PeripheralRegistrationPoint + (ulong)register);
        }

        private IMachine machine;
        private RCC peripheral;
        private const uint PeripheralRegistrationPoint = 0x1000;

        private const uint ClockControlHsion    = 1u << 0;
        private const uint ClockControlHsirdy   = 1u << 2;
        private const uint ClockControlHsidivf  = 1u << 5;
        private const uint ClockControlCsion    = 1u << 7;
        private const uint ClockControlCsirdy   = 1u << 8;
        private const uint ClockControlHseon    = 1u << 16;
        private const uint ClockControlHserdy   = 1u << 17;
        private const uint ClockControlHsecsson = 1u << 19;
        private const uint ClockControlPll1on   = 1u << 24;
        private const uint ClockControlPll1rdy  = 1u << 25;
        private const uint ClockControlPll2on   = 1u << 26;
        private const uint ClockControlPll2rdy  = 1u << 27;
        private const uint ClockControlPll3on   = 1u << 28;
        private const uint ClockControlPll3rdy  = 1u << 29;
        private const int PLL1DividersConfigurationDivp1Offset = 9;
        private const uint PLL1DividersConfigurationDivp1 = 0b1111111u << PLL1DividersConfigurationDivp1Offset;
    }
}
