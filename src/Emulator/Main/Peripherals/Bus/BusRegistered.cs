//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Peripherals.Bus
{
	public class BusRegistered<T> : IBusRegistered<T> where T : IBusPeripheral
	{
		public BusRegistered(T what, BusRangeRegistration where)
        {			
			Peripheral = what;
            RegistrationPoint = where;
		}

		public T Peripheral { get; private set; }
        public BusRangeRegistration RegistrationPoint { get; private set; }
		
	}
}

