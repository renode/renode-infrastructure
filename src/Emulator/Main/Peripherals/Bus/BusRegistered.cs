//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Bus
{
	public class BusRegistered<T> : IBusRegistered<T> where T : IBusPeripheral
	{
		public BusRegistered(T what, BusRangeRegistration where)
        {			
			Peripheral = what;
            RegistrationPoint = where;
		}

        public override string ToString()
        {
            return $"{Peripheral.GetName()} registered at {RegistrationPoint}";
        }

		public T Peripheral { get; private set; }
        public BusRangeRegistration RegistrationPoint { get; private set; }
		
	}
}

