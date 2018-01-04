//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.ObjectModel;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class MockGPIOByNumberConnectorPeripheral : INumberedGPIOOutput, IGPIOReceiver, IBytePeripheral
    {
        public MockGPIOByNumberConnectorPeripheral(int gpios)
        {
            var innerConnections = new Dictionary<int, IGPIO>();
            for(int i = 0; i < gpios; i++)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
            Irq = new GPIO();
            OtherIrq = new GPIO();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }

        public void OnGPIO(int number, bool value)
        {

        }

        public byte ReadByte(long offset)
        {
            throw new NotImplementedException();
        }

        public void WriteByte(long offset, byte value)
        {
            throw new NotImplementedException();
        }

        public GPIO Irq { get; private set; }

        public GPIO OtherIrq { get; private set; }
    }
}

