//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class STM32F4_RCCResetFlagsTests
    {
        [SetUp]
        public void CreatePeripherals()
        {
            machine = new Machine();
            // the watchdog requests the reset through the machine's time source
            EmulationManager.Instance.CurrentEmulation.AddMachine(machine);
            iwdg = new STM32_IndependentWatchdog(machine, WatchdogFrequency);
            rcc = new STM32F4_RCC(machine, null, iwdg);
        }

        [TearDown]
        public void RemoveMachine()
        {
            EmulationManager.Instance.Clear();
        }

        [Test]
        public void ShouldReportPowerOnFlagsAfterCreation()
        {
            // RM0090 7.3.21: reset value 0x0E000000 (BORRSTF, PINRSTF, PORRSTF)
            Assert.AreEqual(0x0E000000u, rcc.ReadDoubleWord(ClockControlAndStatus));
        }

        [Test]
        public void ShouldClearResetFlagsOnRemoveResetFlag()
        {
            rcc.WriteDoubleWord(ClockControlAndStatus, RemoveResetFlag);
            Assert.AreEqual(0u, rcc.ReadDoubleWord(ClockControlAndStatus) & ResetFlagsMask);
        }

        [Test]
        public void ShouldKeepResetFlagsAcrossSystemReset()
        {
            // "reset by system reset, except reset flags by power reset only"
            rcc.WriteDoubleWord(ClockControlAndStatus, RemoveResetFlag);
            rcc.Reset();
            Assert.AreEqual(0u, rcc.ReadDoubleWord(ClockControlAndStatus) & ResetFlagsMask);
        }

        [Test]
        public void ShouldSetIndependentWatchdogFlagWhenWatchdogResets()
        {
            rcc.WriteDoubleWord(ClockControlAndStatus, RemoveResetFlag);
            iwdg.WriteDoubleWord(WatchdogKey, WatchdogStart);
            // default PR = /4, RLR = 0xFFF: timeout = 4096 * 4 / 32 kHz = 0.512 s
            ((BaseClockSource)machine.ClockSource).Advance(TimeInterval.FromSeconds(1));
            // the machine reset requested by the watchdog runs in a synced state; do its part here
            rcc.Reset();
            iwdg.Reset();
            Assert.AreEqual(IndependentWatchdogResetFlag, rcc.ReadDoubleWord(ClockControlAndStatus) & ResetFlagsMask);
        }

        [Test]
        public void ShouldNotSetIndependentWatchdogFlagWhileWatchdogIsReloaded()
        {
            rcc.WriteDoubleWord(ClockControlAndStatus, RemoveResetFlag);
            iwdg.WriteDoubleWord(WatchdogKey, WatchdogStart);
            for(var i = 0; i < 10; i++)
            {
                ((BaseClockSource)machine.ClockSource).Advance(TimeInterval.FromSeconds(0.25));
                iwdg.WriteDoubleWord(WatchdogKey, WatchdogReload);
            }
            Assert.AreEqual(0u, rcc.ReadDoubleWord(ClockControlAndStatus) & ResetFlagsMask);
        }

        private IMachine machine;
        private STM32_IndependentWatchdog iwdg;
        private STM32F4_RCC rcc;

        private const ulong WatchdogFrequency = 32000;
        private const long ClockControlAndStatus = 0x74;
        private const long WatchdogKey = 0x0;
        private const uint WatchdogStart = 0xCCCC;
        private const uint WatchdogReload = 0xAAAA;
        private const uint RemoveResetFlag = 1u << 24;
        private const uint IndependentWatchdogResetFlag = 1u << 29;
        private const uint ResetFlagsMask = 0xFE000000;
    }
}
