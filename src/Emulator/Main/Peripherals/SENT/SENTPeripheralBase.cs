//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.SENT
{
    public abstract class SENTPeripheralBase : ISENTPeripheral
    {
        public SENTPeripheralBase(IMachine machine, TimeInterval tickPeriod)
        {
            transmitter = new Transmitter(machine, tickPeriod);
            transmitter.Edge += edge => SENTEdgeChanged?.Invoke(edge);
            transmitter.ProvideFastMessage += ProvideFastMessage;
            transmitter.ProvideSlowMessage += ProvideSlowMessage;
        }

        public abstract FastMessage ProvideFastMessage();
        public abstract SlowMessage ProvideSlowMessage();

        public virtual void Reset()
        {
            TransmissionEnabled = false;
        }

        public event Action<SENTEdge> SENTEdgeChanged;

        public bool TransmissionEnabled
        {
            get => transmitter.Transmitting;
            set => transmitter.Transmitting = value;
        }

        protected readonly Transmitter transmitter;
    }
}
