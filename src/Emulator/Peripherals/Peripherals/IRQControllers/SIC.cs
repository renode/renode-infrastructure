//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Linq;
using Antmicro.Migrant;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
	public class SIC : IDoubleWordPeripheral, INumberedGPIOOutput, IIRQController
	{
		public SIC()
		{
			parent = Enumerable.Range(0, 32).ToArray();
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < 32; i++)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

			irq = 31;
		}
		
		public uint ReadDoubleWord(long offset)
		{
            lock(sync)
            {
    			switch ((SR)(offset >> 2))
    			{
    			case SR.Status:
    				return level & mask;
    			case SR.RawStatus:
    				return level;
    			case SR.Enable:
    				return mask;
    			case SR.SoftInterrupt:
    				return level & 1;
    			case SR.PICEnable:
    				return picEnable;
    			default:
    				this.LogUnhandledRead(offset);
    				return 0u;
    			}
            }
		}
		
		public void WriteDoubleWord(long offset, uint value)
		{
            lock(sync)
            {
    			switch ((SR)(offset >> 2))
    			{
    			case SR.Enable:
    				mask |= value;
    				break;
    			case SR.EnableClear:
    				mask &= ~value;
    				break;
    			case SR.SoftInterrupt:
    				if (value > 0)
    				{
    					mask |= 1u;
    				}
    				break;
    			case SR.SoftInterruptClear:
    				if (value > 0)
    				{
    					mask &= ~1u;
    				}
    				break;
    			case SR.PICEnable:
    				picEnable |= (value & 0x7FE00000);
    				UpdatePIC();
    				break;
    			case SR.PICEnableClear:
    				picEnable &= ~value;
    				UpdatePIC();
    				break;
    			default:
    				this.LogUnhandledWrite(offset, value);
    				return;
    			}
    			Update();
            }
		}
		
		public void Reset()
		{
			// TODO: some kind of reset?
		}

        public void OnGPIO(int irq, bool value)
        {
            lock(sync)
            {
                if (value)
                {
                    level |= 1u << irq;
                }
                else
                {
                    level &= ~(1u << irq);
                }
                if ((picEnable & (1u << irq)) > 0)
                {
                    Connections[parent[irq]].Set(value);
                }
                Update();
            }
        }   
		
		private void Update()
		{			
			this.DebugLog("Update.");
			var flags = level & mask;
			Connections[irq].Set(flags != 0);
		}
		
		private void UpdatePIC()
		{
			for (var i = 21; i <= 30; i++)
			{
				var mask = 1u << i;
				if ((picEnable & mask) == 0)
				{
					continue;
				}
                Connections[parent[i]].Set((level & mask) != 0);
			}
		}

		private uint level;
		private uint mask;
		private uint picEnable;
		private int[] parent;
        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }
		private readonly int irq;

        [Constructor]
        private object sync = new object();
		
		private enum SR : uint
		{
			Status = 0x0,
			RawStatus = 0x1,
			Enable = 0x2,
			EnableClear = 0x3,
			SoftInterrupt = 0x4,
			SoftInterruptClear = 0x5,
			PICEnable = 0x8,
			PICEnableClear = 0x9,
		}
	}
}

