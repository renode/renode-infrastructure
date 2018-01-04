//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
	public class PL190 : IDoubleWordPeripheral, IIRQController
	{		
		public PL190 ()
		{			
			Masks = new uint[NumOfPrio + 1];
			Reset();
            IRQ = new GPIO();
            FIQ = new GPIO();
		}
		
		public uint ReadDoubleWord (long offset)
		{
            lock(sync)
            {
    			if (offset >= 0x100 && offset < 0x140)
    			{				
    				return VectorAddresses[(offset - 0x100) >> 2];
    			}
    			else if (offset >= 0x200 && offset < 0x240)
    			{				
    				return VectorControl[(offset - 0x200) >> 2];
    			}
    			else 
    			{
    				switch ((VIC)offset)
    				{
    				case VIC.PeriID0:
    					return 0x90;
    				case VIC.PeriID1:
    					return 0x11;
    				case VIC.PeriID2:
    					return 0x04;
    				case VIC.PeriID3:
    					return 0x00;
    				case VIC.CellID0:
    					return 0x0D;
    				case VIC.CellID1:
    					return 0xF0;
    				case VIC.CellID2:
    					return 0x05;
    				case VIC.CellID3:
    					return 0xB1;
    				case VIC.IRQStatus:
    					return IRQLevel;
    				case VIC.FIQStatus:
    					return (Level | SoftLevel) & FIQSelect;
    				case VIC.RawInterrput:
    					return Level | SoftLevel;
    				case VIC.InterruptSelect:
    					return FIQSelect;
    				case VIC.InterruptEnable:
    					return IRQEnable;
    				case VIC.SoftInterrupt:
    					return SoftLevel;
    				case VIC.Protection:
    					return Protection;
    				case VIC.VectorAddresses:
    					int i;
    					for (i = 0; i < Priority; i++)
    					{
    						if(((Level | SoftLevel) & Masks[i]) != 0)
    						{
    							break;
    						}
    					}
    					if (i == NumOfPrio)
    					{
    						return VectorAddresses[16];
    					}
    					if (i < Priority)
    					{
    						PreviousPriority[i] = Priority;
    						Priority = i;
    						Update();
    					}
    					return VectorAddresses[Priority];
    				case VIC.DefVectorAddresses:
    					return VectorAddresses[16];
                    default:
                        this.LogUnhandledRead(offset);
    					return 0x00;
    				}
    			}
            }
		}
		
		public void WriteDoubleWord (long offset, uint value)
		{
            lock(sync)
            {
    			if (offset >= 0x100 && offset < 0x140)
    			{
    				VectorAddresses[(offset - 0x100) >> 2] = value;
    				UpdateVectors();
    				return;
    			}
    			if (offset >= 0x200 && offset < 0x240)
    			{
    				VectorControl[(offset - 0x200) >> 2] = Convert.ToByte(value);
    				UpdateVectors();
    				return;
    			}
    			switch ((VIC)offset)
    			{
    			case VIC.IRQStatus:
    				break;
    			case VIC.InterruptSelect:
    				FIQSelect = value;
    				break;
    			case VIC.InterruptEnable:
    				IRQEnable |= value;
    				break;
    			case VIC.InterruptEnableClear:
    				IRQEnable &= ~value;
    				break;
    			case VIC.SoftInterrupt:
    				SoftLevel |= value;
    				break;
    			case VIC.SoftInterrputClear:
    				SoftLevel &= ~value;
    				break;
    			case VIC.Protection:
    				Protection = value & 1;
    				break;
    			case VIC.VectorAddresses:
    				if (Priority < NumOfPrio)
    				{
    					Priority = PreviousPriority[Priority];
    				}
    				break;
    			case VIC.DefVectorAddresses:
    				VectorAddresses[16] = value;
    				break;
    			case VIC.ITCR:
    				if (value != 0)
    				{
    					this.Log(LogLevel.Warning, "Cannot enable test mode.");
    				}	
    				break;
    			default:
    				this.LogUnhandledWrite(offset, value);
    				return;
    			}
    			Update();
            }
		}
		
		public void Reset ()
		{
			VectorControl = new byte[NumOfPrio - 1];
			VectorAddresses = new uint[NumOfPrio];
			Masks[NumOfPrio] = 0xFFFFFFFF;
			Priority = NumOfPrio;			
			PreviousPriority = new int[NumOfPrio];
			UpdateVectors();
		}

        public GPIO IRQ { get; private set; }
        public GPIO FIQ { get; private set; }
		
		public void OnGPIO(int number, bool value)
		{
            lock(sync)
            {
    			if(value)
    			{
    				Level |= 1u << number;
    			}
    			else
    			{
    				Level &= ~(1u << number);
    			}
    			Update();
            }
		}
		
		private void Update()
		{			
			var level = IRQLevel;
			if(IRQEnable != 0)
			{		
                IRQ.Set((level  & Masks[Priority]) != 0);
				FIQ.Set(((Level | SoftLevel) & FIQSelect) != 0);
			}
		}
		
		private void UpdateVectors()
		{
			var mask = 0u;
			for(var i = 0; i < 16; i++)
			{
				Masks[i] = mask;
				if ((VectorControl[i] & 0x20) != 0)
				{
					var n = VectorControl[i] & 0x1f;
					mask |= 1u << n;
				}
			}
			Masks[16] = mask;
			Update();
		}
		
		private const int NumOfPrio = 17;
		private uint Level;
		
		private uint IRQLevel
		{
			get
			{
				return (Level | SoftLevel) & IRQEnable & ~FIQSelect;
			}
		}
		
		private uint SoftLevel;		
		private uint IRQEnable;		
		private uint FIQSelect;
		private byte[] VectorControl;
		private uint[] VectorAddresses;
		private uint[] Masks;
		private uint Protection;
		private int Priority;
		private int[] PreviousPriority; // TODO: check that

        [Constructor]
        private object sync = new object();
		
		private enum VIC : uint
		{
			IRQStatus = 0x000,
			FIQStatus = 0x004,
			RawInterrput = 0x008,
			InterruptSelect = 0x00C,
			InterruptEnable = 0x010,
			InterruptEnableClear = 0x014,
			SoftInterrupt = 0x018,
			SoftInterrputClear = 0x01C,
			Protection = 0x020,
			VectorAddresses = 0x030,
			DefVectorAddresses = 0x034,
			ITCR = 0x300, // test control, not implemented
			PeriID0 = 0xFE0,
			PeriID1 = 0xFE4,
			PeriID2 = 0xFE8,
			PeriID3 = 0xFEC,
			CellID0 = 0xFF0,
			CellID1 = 0xFF4,
			CellID2 = 0xFF8,
			CellID3 = 0xFFC
		}
	}
	
}

