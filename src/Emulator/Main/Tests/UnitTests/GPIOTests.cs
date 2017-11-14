//
// Copyright (c) 2010-2017 Antmicro
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
            var endpoint = source.Endpoint;
            Assert.AreEqual(2, endpoint.Number);
            Assert.AreEqual(destination, endpoint.Receiver);
        }

        [Test]
        public void ShouldGiveNullOnNotConnected()
        {
            var source = new GPIO();
            var endpoint = source.Endpoint;
            Assert.AreEqual(null, endpoint);
        }

        [Test]
        public void ShouldConnectBoundGPIOs()
        {
            var source = new GPIO();
            var boundIn = new MockReceiverConstrained();
            source.Connect(boundIn, 2);
        }

        [Test]
        [ExpectedException(typeof(ConstructionException), UserMessage = NonExistingGPIO)]
        public void ShouldThrowOnIllegalInputNo()
        {
            var source = new GPIO();
            var boundIn = new MockReceiverConstrained();
            source.Connect(boundIn, 10);
        }

        private const string NonExistingGPIO = "Connector perimtted to connect to non existing GPIO.";
    }
}
