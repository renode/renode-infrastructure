//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.Wireless
{
    public static class SimpleMediumExtension
    {
        public static void SetSimpleWirelessFunction(this WirelessMedium wirelessMedium)
        {
            wirelessMedium.SetMediumFunction(SimpleMediumFunction.Instance);
        }
    }

    public sealed class SimpleMediumFunction : IMediumFunction
    {
        static SimpleMediumFunction()
        {
            Instance = new SimpleMediumFunction();
        }

        public static SimpleMediumFunction Instance { get; private set; }

        public bool CanReach(Position from, Position to)
        {
            // always can reach destination - that's why we call it `simple`
            return true;
        }

        public bool CanTransmit(Position from)
        {
            // always can transmit - that's why we call it `simple`
            return true;
        }

        public string FunctionName { get { return Name; } }

        private SimpleMediumFunction()
        {
        }

        private const string Name = "simple_medium_function";
    }
}