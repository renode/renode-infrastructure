//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Utilities
{
    /// <summary>
    /// A helper class that contains time domain related extension methods.
    /// </summary>
    public static class TimeDomainExtensions
    {
        /// <summary>
        /// Tries to obtain virtual time stamp for the current thread,
        //  if the current thread does not have registered domain an
        //  external world time stamp is returned.
        /// </summary>
        /// <returns>Virtual time stamp</returns>
        /// <param name="instance">Instance of <see cref="TimeDomainsManager"/></param>
        public static TimeStamp GetEffectiveVirtualTimeStamp(this TimeDomainsManager instance)
        {
            if(instance.TryGetVirtualTimeStamp(out var threadTimeStamp))
            {
                return threadTimeStamp;
            }
            return new TimeStamp(default(TimeInterval), EmulationManager.ExternalWorld);
        }
    }
}
