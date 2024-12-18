//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Peripherals.Bus
{
    public interface IBusRegistered<out T> : IRegistered<T, BusRangeRegistration> where T : IBusPeripheral
    {
    }

    public static class IRegisteredExtensions
    {
        public static IBusRegistered<TTo> Convert<TFrom, TTo>(this IBusRegistered<TFrom> conversionSource) where TTo : TFrom where TFrom : IBusPeripheral
        {
            return new BusRegistered<TTo>((TTo)conversionSource.Peripheral, new BusRangeRegistration(conversionSource.RegistrationPoint.Range,
                                          conversionSource.RegistrationPoint.Offset, conversionSource.RegistrationPoint.Initiator));
        }

        public static IEnumerable<IBusRegistered<TTo>> Convert<TFrom, TTo>(this IEnumerable<IBusRegistered<TFrom>> sourceCollection) where TTo : TFrom where TFrom : IBusPeripheral
        {
            return sourceCollection.Select(x => x.Convert<TFrom, TTo>());
        }
    }
}

