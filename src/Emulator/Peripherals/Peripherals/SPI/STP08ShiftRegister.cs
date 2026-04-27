//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class STP08ShiftRegister : ISPIPeripheral, INumberedGPIOOutput, IRegisterablePeripheral<IGPIOReceiver, NumberRegistrationPoint<byte>>
    {
        public STP08ShiftRegister(IMachine machine)
        {
            this.machine = machine;
            Connections = Enumerable
                .Range(0, 8)
                .ToDictionary(idx => idx, _ => (IGPIO)new GPIO());
            Reset();
        }

        public void Reset()
        {
            foreach(var connection in Connections.Values)
            {
                connection.Set(false);
            }
        }

        public void FinishTransmission()
        {
            // Intentionally empty
        }

        public byte Transmit(byte data)
        {
            this.DebugLog("Recived data 0x{0}", data);
            BitHelper.ForeachBit(data, (idx, val) =>
            {
                this.DebugLog("Setting #{0} to {1}", idx, val);
                Connections[idx].Set(val);
            }, 8);
            return 0;
        }

        public void Register(IGPIOReceiver peripheral, NumberRegistrationPoint<byte> registrationPoint)
        {
            if(registrationPoint.Address >= 8)
            {
                throw new RegistrationException($"Registration point out of range. Expected 0-7, got {registrationPoint.Address}");
            }
            Connections[registrationPoint.Address].Connect(peripheral, 0);
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(IGPIOReceiver peripheral)
        {
            foreach(var connection in Connections.Values)
            {
                foreach(var endpoint in connection.Endpoints)
                {
                    if(endpoint.Receiver == peripheral)
                    {
                        connection.Disconnect(endpoint);
                    }
                }
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private readonly IMachine machine;
    }
}