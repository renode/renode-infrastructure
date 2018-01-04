//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core.Structure
{
	
	/// <summary>
	/// Interface representing registered device. It is covariant because registered specialised device is
	/// registered device.
	/// </summary>
	public interface IRegistered<out TPeripheral, TRegistrationPoint>
        where TPeripheral : IPeripheral where TRegistrationPoint : IRegistrationPoint
	{
		TPeripheral Peripheral { get; }
        TRegistrationPoint RegistrationPoint { get; }
	}
}
