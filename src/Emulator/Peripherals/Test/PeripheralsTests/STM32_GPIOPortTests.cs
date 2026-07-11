//
// Copyright (c) 2026 John Elliott
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.GPIOPort;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class STM32_GPIOPortTests
    {
        [Test]
        public void ShouldKeepInputAndOutputDataIndependentInInputMode()
        {
            using(var machine = new Machine())
            {
                var gpio = new STM32_GPIOPort(machine);

                gpio.WriteDoubleWord(OutputData, 1u << Pin);

                Assert.That(gpio.ReadDoubleWord(OutputData), Is.EqualTo(1u << Pin));
                Assert.That(gpio.ReadDoubleWord(InputData), Is.Zero);

                gpio.OnGPIO(Pin, true);

                Assert.That(gpio.ReadDoubleWord(InputData), Is.EqualTo(1u << Pin));
                Assert.That(gpio.ReadDoubleWord(OutputData), Is.EqualTo(1u << Pin));
            }
        }

        [TestCase(InputMode)]
        [TestCase(AlternateFunctionMode)]
        [TestCase(AnalogMode)]
        public void ShouldOnlyDriveLatchedValueInOutputMode(uint initialMode)
        {
            using(var machine = new Machine())
            {
                var gpio = new STM32_GPIOPort(machine);

                gpio.WriteDoubleWord(Mode, initialMode << (Pin * 2));
                gpio.WriteDoubleWord(OutputData, 1u << Pin);

                Assert.That(gpio.ReadDoubleWord(OutputData), Is.EqualTo(1u << Pin));
                Assert.That(gpio.ReadDoubleWord(InputData), Is.Zero);
                Assert.That(gpio.Connections[Pin].IsSet, Is.False);

                gpio.WriteDoubleWord(Mode, OutputMode << (Pin * 2));

                Assert.That(gpio.ReadDoubleWord(InputData), Is.EqualTo(1u << Pin));
                Assert.That(gpio.Connections[Pin].IsSet, Is.True);
            }
        }

        [Test]
        public void ShouldUpdateOutputLatchThroughSetAndResetRegisters()
        {
            using(var machine = new Machine())
            {
                var gpio = new STM32_GPIOPort(machine);

                gpio.OnGPIO(Pin, true);
                gpio.WriteDoubleWord(BitSetReset, 1u << Pin);

                Assert.That(gpio.ReadDoubleWord(OutputData), Is.EqualTo(1u << Pin));
                Assert.That(gpio.ReadDoubleWord(InputData), Is.EqualTo(1u << Pin));

                gpio.WriteDoubleWord(BitSetReset, 1u << (Pin + 16));

                Assert.That(gpio.ReadDoubleWord(OutputData), Is.Zero);
                Assert.That(gpio.ReadDoubleWord(InputData), Is.EqualTo(1u << Pin));

                gpio.WriteDoubleWord(OutputData, 1u << Pin);
                gpio.WriteDoubleWord(BitReset, 1u << Pin);

                Assert.That(gpio.ReadDoubleWord(OutputData), Is.Zero);
                Assert.That(gpio.ReadDoubleWord(InputData), Is.EqualTo(1u << Pin));
            }
        }

        [Test]
        public void ShouldPrioritizeSetWhenBitSetAndResetAreWrittenTogether()
        {
            using(var machine = new Machine())
            {
                var gpio = new STM32_GPIOPort(machine);

                gpio.WriteDoubleWord(BitSetReset, (1u << Pin) | (1u << (Pin + 16)));

                Assert.That(gpio.ReadDoubleWord(OutputData), Is.EqualTo(1u << Pin));
            }
        }

        [Test]
        public void ShouldClearOutputLatchOnReset()
        {
            using(var machine = new Machine())
            {
                var gpio = new STM32_GPIOPort(machine);

                gpio.WriteDoubleWord(Mode, OutputMode << (Pin * 2));
                gpio.WriteDoubleWord(OutputData, 1u << Pin);
                gpio.Reset();

                Assert.That(gpio.ReadDoubleWord(OutputData), Is.Zero);
                Assert.That(gpio.ReadDoubleWord(InputData), Is.Zero);
                Assert.That(gpio.Connections[Pin].IsSet, Is.False);
            }
        }

        private const int Pin = 2;
        private const uint InputMode = 0;
        private const uint OutputMode = 1;
        private const uint AlternateFunctionMode = 2;
        private const uint AnalogMode = 3;
        private const long Mode = 0x00;
        private const long InputData = 0x10;
        private const long OutputData = 0x14;
        private const long BitSetReset = 0x18;
        private const long BitReset = 0x28;
    }
}
