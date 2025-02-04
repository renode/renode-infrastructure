//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.UnitTests.Mocks;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class MockIrqSenderWithTwoInterrupts : MockIrqSender
    {
        public MockIrqSenderWithTwoInterrupts() : base()
        {
            AnotherIrq = new GPIO();
        }

        public GPIO AnotherIrq { get; private set; }
    }
}
