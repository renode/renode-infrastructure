//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.SENT;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Mocks
{
    public class DummySENTTransmitter : SENTPeripheralBase
    {
        public DummySENTTransmitter(IMachine machine, uint tickPeriodMicroseconds)
            : base(machine, TimeInterval.FromMicroseconds(tickPeriodMicroseconds))
        {
            defaultFastMessage = new FastMessage(0, 0, 0, 0, 0, 0);
            defaultSlowMessage = new SlowMessage(0, 0, 0);

            fastQueue = new Queue<FastMessage>();
            slowQueue = new Queue<SlowMessage>();
        }

        public override FastMessage ProvideFastMessage()
        {
            if(!fastQueue.TryDequeue(out var message))
            {
                message = defaultFastMessage;
            }
            return message;
        }

        public override SlowMessage ProvideSlowMessage()
        {
            if(!slowQueue.TryDequeue(out var message))
            {
                message = defaultSlowMessage;
            }
            return message;
        }

        public void EnqueueFastMessage(FastMessage message)
        {
            fastQueue.Enqueue(message);
        }

        public void EnqueueFastMessage(params byte[] nibbles)
        {
            try
            {
                fastQueue.Enqueue(new FastMessage(nibbles));
            }
            catch(ArgumentException e)
            {
                // Rethrow as a RecoverableException as this method will most likely be called from the monitor
                throw new RecoverableException(e);
            }
        }

        public void EnqueueSlowMessage(SlowMessage message)
        {
            slowQueue.Enqueue(message);
        }

        public void EnqueueSlowMessage(params byte[] nibbles)
        {
            try
            {
                slowQueue.Enqueue(new SlowMessage(nibbles));
            }
            catch(ArgumentException e)
            {
                // Rethrow as a RecoverableException as this method will most likely be called from the monitor
                throw new RecoverableException(e);
            }
        }

        private readonly FastMessage defaultFastMessage;
        private readonly SlowMessage defaultSlowMessage;

        private readonly Queue<FastMessage> fastQueue;
        private readonly Queue<SlowMessage> slowQueue;
    }
}
