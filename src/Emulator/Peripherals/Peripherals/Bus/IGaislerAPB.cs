//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.Bus
{
    public interface IGaislerAPB : IBusPeripheral
    {
        uint GetVendorID();
        uint GetDeviceID();
        uint GetInterruptNumber();
        GaislerAPBPlugAndPlayRecord.SpaceType GetSpaceType();
    }

    public static class IGaislerAPBExtensions
    {
        public static uint GetCpuInterruptNumber(this IGaislerAPB @this, IGPIO gpio)
        {
            var endpoint = gpio.Endpoints.FirstOrDefault();
            if(endpoint == null)
            {
                return 0;
            }
            // For CombinedInputs, the true endpoint is the (*)-marked one in the
            // Peripheral->CombinedInput->[CombinedInput...]->(*)CPU chain
            while(endpoint?.Receiver is CombinedInput combiner)
            {
                endpoint = combiner.OutputLine.Endpoints.SingleOrDefault();
                if(endpoint == null)
                {
                    @this.WarningLog("IRQ output is connected to a CombinedInput, but this CombinedInput is not "
                        + "connected to a single destination. Check your platform description file.");
                    return 0;
                }
            }
            return (uint)endpoint.Number;
        }
    }
}

