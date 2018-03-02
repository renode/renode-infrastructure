//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Core
{
    public class GPIOMultiplexer : IGPIO
    {
        public GPIOMultiplexer(params IGPIO[] gpio)
        {
            gpios = gpio;
        }

        public void Set(bool value)
        {
            foreach(var gpio in gpios)
            {
                gpio.Set(value);
            }
        }

        public void Toggle()
        {
            foreach(var gpio in gpios)
            {
                gpio.Toggle();
            }
        }

        public void Connect(IGPIOReceiver destination, int destinationNumber) => throw new InvalidOperationException($"Operation not supported in {this.GetType().Name}."); 

        public void Disconnect() => throw new InvalidOperationException($"Operation not supported in {this.GetType().Name}."); 

        public bool IsSet
        {
            get => throw new InvalidOperationException($"Operation not supported in {this.GetType().Name}.");
        }

        public bool IsConnected
        {
            get => throw new InvalidOperationException($"Operation not supported in {this.GetType().Name}.");
        }

        public GPIOEndpoint Endpoint
        {
            get => throw new InvalidOperationException($"Operation not supported in {this.GetType().Name}.");
        }

        private IGPIO[] gpios;
    }
}
