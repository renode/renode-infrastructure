//
// Copyright (c) 2026 John Elliott
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Reflection;

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.IRQControllers;

using NUnit.Framework;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class NVICTests
    {
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        public void ShouldReportReturnToBaseAccordingToActiveExceptionCount(int activeExceptionCount, bool expected)
        {
            using(var machine = new Machine())
            {
                var nvic = new NVIC(machine);
                var activeIRQsField = typeof(NVIC).GetField("activeIRQs", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(activeIRQsField, Is.Not.Null);
                var activeIRQs = (Stack<int>)activeIRQsField.GetValue(nvic);
                for(var i = 0; i < activeExceptionCount; i++)
                {
                    activeIRQs.Push(i + 1);
                }

                Assert.That(IsReturnToBaseSet(nvic), Is.EqualTo(expected));
            }
        }

        private static bool IsReturnToBaseSet(NVIC nvic)
        {
            return (nvic.ReadDoubleWord(InterruptControlState) & ReturnToBase) != 0;
        }

        private const long InterruptControlState = 0xD04;
        private const uint ReturnToBase = 1u << 11;
    }
}
