//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using NUnit.Framework;
using Antmicro.Renode.UnitTests.Mocks;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class GPIOTests
    {

        [Test]
        public void ShouldPropagateConnected()
        {
            var source = new GPIO();
            var destination = new MockReceiver();
            source.Connect(destination, 2);
            var endpoint = source.Endpoints;
            Assert.AreEqual(2, endpoint[0].Number);
            Assert.AreEqual(destination, endpoint[0].Receiver);
        }

        [Test]
        public void ShouldBeEmptyWhenNotConnected()
        {
            var source = new GPIO();
            var endpoint = source.Endpoints;
            Assert.IsEmpty(endpoint);
        }

        [Test]
        public void ShouldConnectBoundGPIOs()
        {
            var source = new GPIO();
            var boundIn = new MockReceiverConstrained();
            source.Connect(boundIn, 2);
        }

        [Test]
        public void ShouldThrowOnIllegalInputNo()
        {
            var source = new GPIO();
            var boundIn = new MockReceiverConstrained();

            var ex = Assert.Throws<ConstructionException>(() => source.Connect(boundIn, 10));
            Assert.That(ex.Message, Is.EqualTo("Cannot connect 10th input of Antmicro.Renode.UnitTests.Mocks.MockReceiverConstrained; it has only 5 GPIO inputs."));
        }
    }
}
