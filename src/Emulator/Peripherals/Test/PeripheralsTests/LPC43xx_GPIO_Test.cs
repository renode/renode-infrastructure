//
// Copyright (c) 2010-2020 LabMICRO FACET UNT
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.GPIOPort;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class LPC43xx_GPIO_Test
    {
        [Test]
        public void InitTest()
        {
            var machine = new Machine();
            var gpio = new LPC43xx_GPIO(machine);
            machine.SystemBus.Register(gpio, new BusRangeRegistration(0x4000A000, 0x400));

            // Given a just reseted gpio port
            gpio.Reset();

            for(var port = 0; port < 8; port ++)
            {
                // Then GPIO_DIR should have a value equal to cero
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_DIR + 4 * port), 0x00000000);
                // And GPIO_MASK should have a value equal to cero
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_MASK + 4 * port), 0x00000000);
                // And GPIO_SET should have a value equal to cero
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_SET + 4 * port), 0x00000000);
            }
        }

        [Test]
        public void ChangeDirectionToOutput()
        {
            var machine = new Machine();
            var gpio = new LPC43xx_GPIO(machine);
            machine.SystemBus.Register(gpio, new BusRangeRegistration(0x4000A000, 0x400));

            // Given a just reseted gpio port
            gpio.Reset();

            for(var port = 0; port < 8; port++)
            {
                // When GPIO_DIR it's written to set all pins as outputs
                gpio.WriteDoubleWord(GPIO_DIR + 4 * port, 0xFFFFFFFF);
                // Then GPIO_DIR should confirm that all pins are outpus
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_DIR + 4 * port), 0xFFFFFFFF);
                // And GPIO_MASK should have retained the reset value
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_MASK + 4 * port), 0x00000000);
                // And GPIO_PIN should have retained the reset value
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_PIN + 4 * port), 0x00000000);
                // And  GPIO_MPIN should have retained the reset value
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_MPIN + 4 * port), 0x00000000);
                // And  GPIO_SET should have retained the reset value
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_SET + 4 * port), 0x00000000);
            }
        }

        [Test]
        public void ChangeOutputsState()
        {
            var machine = new Machine();
            var gpio = new LPC43xx_GPIO(machine);
            machine.SystemBus.Register(gpio, new BusRangeRegistration(0x4000A000, 0x400));

            gpio.Reset();
            for(var port = 0; port < 8; port++)
            {
                // Given a gpio port with all pins configured as outputs
                gpio.WriteDoubleWord(GPIO_DIR + 4 * port, 0xFFFFFFFF);

                // When GPIO_PIN are writed to set all outpus pins to high state
                gpio.WriteDoubleWord(GPIO_PIN + 4 * port, 0xFFFFFFFF);
                // Then GPIO_PIN should have the current value of the outputs
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_PIN + 4 * port), 0xFFFFFFFF);
                // And GPIO_SET should have the current value of the outputs
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_SET + 4 * port), 0xFFFFFFFF);

                // When GPIO_PIN are writed to set all outpus pins to low state
                gpio.WriteDoubleWord(GPIO_PIN + 4 * port, 0x00000000);
                // Then GPIO_PIN should have the current value of the outputs
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_PIN + 4 * port), 0x00000000);
                // And GPIO_SET should have the current value of the outputs
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_SET + 4 * port), 0x00000000);

                // When GPIO_SET are writed to set all outpus pins to high state
                gpio.WriteDoubleWord(GPIO_SET + 4 * port, 0xFFFFFFFF);
                // Then GPIO_PIN should have the current value of the outputs
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_PIN + 4 * port), 0xFFFFFFFF);

                // When GPIO_SET are writed to not produce changes in the outputs
                gpio.WriteDoubleWord(GPIO_SET + 4 * port, 0x00000000);
                // Then GPIO_PIN should have retained the previous value of the outputs
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_PIN + 4 * port), 0xFFFFFFFF);

                // When GPIO_CLR are writed to set all outpus pins to low state
                gpio.WriteDoubleWord(GPIO_CLR + 4 * port, 0xFFFFFFFF);
                // Then GPIO_PIN should have the current value of the outputs
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_PIN + 4 * port), 0x00000000);

                // When GPIO_SET are writed to not produce changes in the outputs
                gpio.WriteDoubleWord(GPIO_CLR + 4 * port, 0x00000000);
                // Then GPIO_PIN should have retained the previous value of the outputs
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_PIN + 4 * port), 0x00000000);

                // When GPIO_NOT are writed to change all outpus pins
                gpio.WriteDoubleWord(GPIO_NOT + 4 * port, 0xFFFFFFFF);
                // Then GPIO_PIN should have the current value of the outputs
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_PIN + 4 * port), 0xFFFFFFFF);

                // When GPIO_NOT are writed to change all outpus pins
                gpio.WriteDoubleWord(GPIO_NOT + 4 * port, 0xFFFFFFFF);
                // Then GPIO_PIN should have the current value of the outputs
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_PIN + 4 * port), 0x00000000);

                // When GPIO_NOT are writed to to not produce changes in the outputs
                gpio.WriteDoubleWord(GPIO_NOT + 4 * port, 0x00000000);
                // Then GPIO_PIN should have retained the previous value of the outputs
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_PIN + 4 * port), 0x00000000);
            }
        }

        [Test]
        public void ChangeMask()
        {
            var machine = new Machine();
            var gpio = new LPC43xx_GPIO(machine);
            machine.SystemBus.Register(gpio, new BusRangeRegistration(0x4000A000, 0x400));

            gpio.Reset();
            for(var port = 0; port < 8; port++)
            {
                // When GPIO_MASK it's written to set filter out all pins
                gpio.WriteDoubleWord(GPIO_MASK + 4 * port, 0xFFFFFFFF);
                // Then GPIO_MASK should confirm that all pins are filtered out
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_MASK + 4 * port), 0xFFFFFFFF);
            }
        }

        [Test]
        public void ChangeMaskedOuputState()
        {
            var machine = new Machine();
            var gpio = new LPC43xx_GPIO(machine);
            machine.SystemBus.Register(gpio, new BusRangeRegistration(0x4000A000, 0x400));

            gpio.Reset();
            for(var port = 0; port < 8; port++)
            {
                // Given a gpio port with all pins configured as outputs
                gpio.WriteDoubleWord(GPIO_DIR + 4 * port, 0xFFFFFFFF);
                // And a gpio port with all pins setted to high state
                gpio.WriteDoubleWord(GPIO_PIN + 4 * port, 0xFFFFFFFF);

                // When GPIO_MASK it's written to set filter out lower half pins
                gpio.WriteDoubleWord(GPIO_MASK + 4 * port, 0x0000FFFF);
                // Then GPIO_MPORT should return filtered pins in low and not filtered pins in high
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_MPIN + 4 * port), 0xFFFF0000);

                // When GPIO_MPORT it's written to set filter pins to low
                gpio.WriteDoubleWord(GPIO_MPIN + 4 * port, 0x00000000);
                // Then GPIO_PORT should return not filtered pins in low and filtered pins in high
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_PIN + 4 * port), 0x0000FFFF);

                // When GPIO_MASK it's written to set filter out upper half pins
                gpio.WriteDoubleWord(GPIO_MASK + 4 * port, 0xFFFF0000);
                // Then GPIO_MPORT should return filtered pins in low and not filtered pins in high
                Assert.AreEqual(gpio.ReadDoubleWord(GPIO_MPIN + 4 * port), 0x0000FFFF);
            }
        }

        private const uint GPIO_DIR = 0x2000;
        private const uint GPIO_MASK = 0x2080;
        private const uint GPIO_PIN = 0x2100;
        private const uint GPIO_MPIN = 0x2180;
        private const uint GPIO_SET = 0x2200;
        private const uint GPIO_CLR = 0x2280;
        private const uint GPIO_NOT = 0x2300;

    }
}

