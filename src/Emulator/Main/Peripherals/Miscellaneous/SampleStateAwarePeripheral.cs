//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Linq;
using System.Text;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class SampleStateAwarePeripheral : IDoubleWordPeripheral, IKnownSize
    {
        public SampleStateAwarePeripheral(IMachine machine, long size)
        {
            this.size = size;
            sysbus = machine.GetSystemBus(this);
        }

        public void Reset()
        {
        }

        public uint ReadDoubleWord(long offset)
        {
            if(!sysbus.TryGetCurrentContextState(out var initiator, out var cpuState)) // not a final API
            {
                this.WarningLog("No context");
                return 0;
            }
            var peripheralName = initiator.GetName().Split('.')[1];
            this.WarningLog("Read from context: {0} state 0x{1:x8}", peripheralName, cpuState);
            var peripheralNameBytes = Encoding.UTF8.GetBytes(peripheralName).Take(3).Aggregate(0U, (v, b) => (v << 8) | b);
            return peripheralNameBytes << 8 | (uint)(cpuState & 0xff);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
        }

        public long Size => size;

        private readonly long size;
        private readonly IBusController sysbus;
    }
}
