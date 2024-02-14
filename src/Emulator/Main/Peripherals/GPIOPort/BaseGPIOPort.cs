//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

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

        protected BaseGPIOPort(IMachine machine, int numberOfConnections)
        {
            var innerConnections = new Dictionary<int, IGPIO>();
            this.NumberOfConnections = numberOfConnections;
            State = new bool[numberOfConnections];
            for(var i = 0; i < numberOfConnections; i++)
            {
                innerConnections[i] = new GPIO();
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

        protected uint GetSetConnectionBits()
        {
            return BitHelper.GetValueFromBitsArray(Connections.Where(x => x.Key >= 0).OrderBy(x => x.Key).Select(x => x.Value.IsSet));
        }

        protected bool CheckPinNumber(int number)
        {
            if(number < 0 || number >= NumberOfConnections)
            {
                this.Log(LogLevel.Error, $"This peripheral supports gpio inputs from 0 to {NumberOfConnections}, but {number} was called.");
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

            State[number] = value;
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        protected readonly int NumberOfConnections;
        protected bool[] State;
        protected readonly IMachine machine;
    }
}

