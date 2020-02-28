//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    [Icon("gpio")]
    public abstract class BaseGPIOPort : INumberedGPIOOutput, IPeripheralRegister<IGPIOReceiver, NullRegistrationPoint>,
        IPeripheralRegister<IGPIOSender, NullRegistrationPoint>, IPeripheralRegister<IGPIOReceiver, NumberRegistrationPoint<int>>,
        IPeripheral, IGPIOReceiver, IPeripheralRegister<IGPIOSender, NumberRegistrationPoint<int>>
    {
        public void Register(IGPIOSender peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(IGPIOSender peripheral)
        {
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        public void Register(IGPIOSender peripheral, NullRegistrationPoint registrationPoint)
        {
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Register(IGPIOReceiver peripheral, NullRegistrationPoint registrationPoint)
        {
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(IGPIOReceiver peripheral)
        {
            machine.UnregisterAsAChildOf(this, peripheral);
            foreach(var gpio in Connections.Values)
            {
                var endpoints = gpio.Endpoints;
                for(var i = 0; i < endpoints.Count; ++i)
                {
                    if(endpoints[i].Number == 0 && endpoints[i].Receiver == peripheral)
                    {
                        gpio.Disconnect(endpoints[i]);
                    }
                }
            }
        }

        public void Register(IGPIOReceiver peripheral, NumberRegistrationPoint<int> registrationPoint)
        {          
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public virtual void Reset()
        {
            foreach(var connection in Connections.Values)
            {
                connection.Unset();
            }
            for(int i = 0; i < State.Length; ++i)
            {
                State[i] = false;
            }
        }

        protected BaseGPIOPort(Machine machine, int numberOfConnections)
        {
            var innerConnections = new Dictionary<int, IGPIO>();
            State = new bool[numberOfConnections];
            for(var i = 0; i < numberOfConnections; i++)
            {
                innerConnections[i] = new GPIO();
            }
            for(var i = 1; i < numberOfConnections; i++)
            {
                innerConnections[-i] = new GPIO();
            }
            this.machine = machine;
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
        }

        protected void SetConnectionsStateUsingBits(uint bits)
        {
            foreach(var cn in Connections)
            {
                if((bits & 1u << cn.Key) != 0)
                {
                    cn.Value.Set();
                }
                else
                {
                    cn.Value.Unset();
                }
            }
        }

        protected bool CheckPinNumber(int number)
        {
            if(number < 0 || number >= State.Length)
            {
                this.Log(LogLevel.Error, $"This peripheral supports gpio inputs from 0 to {State.Length - 1}, but {number} was called.");
                return false;
            }
            return true;
        }

        public virtual void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            //GPIOs from outer peripherals have to be attached by their negative value.
            //Please keep in mind that it's impossible to connect outgoing GPIO to pin 0.
            State[number] = value;
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }

        protected bool[] State;
        protected readonly Machine machine;
    }
}

