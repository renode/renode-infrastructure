//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Core
{
    public interface IGPIO
    {
        bool IsSet { get; }
        void Set(bool value);
        // TODO: this method could be simulated by calling <<Set(!IsSet)>>, but this requires locking ...
        void Toggle();

        bool IsConnected { get; }
        void Connect(IGPIOReceiver destination, int destinationNumber);
        void Disconnect();

        GPIOEndpoint Endpoint { get; }
    }

    public static class IGPIOExtensions
    {
        public static void Set(this IGPIO gpio)
        {
            gpio.Set(true);
        }

        public static void Unset(this IGPIO gpio)
        {
            gpio.Set(false);
        }

        public static void Blink(this IGPIO gpio)
        {
            gpio.Set();
            gpio.Unset();
        }
    }
}

