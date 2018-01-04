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
using Antmicro.Renode.Utilities;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class AIC : IDoubleWordPeripheral, IIRQController, IKnownSize
    {
        public AIC()
        {
            IRQ = new GPIO();
            FIQ = new GPIO();
        }

        public GPIO IRQ { get; set; }
        public GPIO FIQ { get; set; }

        #region IGPIOReceiver implementation

        public void OnGPIO(int number, bool value)
        {
            lock (localLock)
            {
            this.Log(LogLevel.Noisy, "GPIO IRQ{0} set to {1}", number, value);

            if ((IsInternalHighlevelSensitive(number) && value) || (IsInternalPositiveEdgeTriggered(number) && !level[number] && value))
            {
                BitHelper.SetBit(ref interruptPendingRegister, (byte)number, false);
                if (IsIRQEnabled(number))
                {
                    if (CurrentIRQ.HasValue && CurrentIRQ.Value != -1 && GetPriority(number) > GetPriority(CurrentIRQ.Value))
                    {
                        IRQ.Set(false);
                    }
                    
                    IRQ.Set();
                }
            }

               level[number] = value;
            }
        }

        #endregion

        #region IDoubleWordPeripheral implementation

        public uint ReadDoubleWord(long offset)
        {
            lock(localLock)
            {
                switch ((Register)offset)
                {
                case Register.InterruptVectorRegister:
                    if (CurrentIRQ.HasValue)
                    {
                        nestedInterruptStack.Push(Tuple.Create(CurrentIRQ.Value, (int)GetPriority(CurrentIRQ.Value)));
                        BitHelper.SetBit(ref interruptPendingRegister, (byte)CurrentIRQ.Value, true);
                    }

                    uint result;
                    var irq = CalculateCurrentInterrupt();
                    if (irq.HasValue)
                    {
                        BitHelper.SetBit(ref interruptPendingRegister, (byte)irq.Value, false); // clears the interrupt
                        CurrentIRQ = irq.Value;
                        result = sourceVectorRegisters[irq.Value];
                    }
                    else
                    {
                        CurrentIRQ = -1; // hack - there is no irq, but spourius irq handler is called
                        result = spouriousInterruptVectorRegister;
                    }

                    IRQ.Unset(); // de-asserts nIRQ to processor
                    return result;

                case Register.InterruptStatusRegister:
                    if (CurrentIRQ.HasValue && CurrentIRQ > -1)
                    {
                        return (uint)CurrentIRQ.Value;
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "Spourious !!! level is: {0}", level[1]);
                        // When there is no interrupt or we have a spourious one return 0
                        return 0u;
                    }

                default:
                    this.LogUnhandledRead(offset);
                    return 0u;
                }
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock (localLock)
        {
            var val = -1;
            if ((val = IsOffsetInRange((uint)offset, (uint)Register.SourceModeRegister0, 0x04, 32)) != -1)
            {
                this.Log(LogLevel.Noisy, "This was write to Source Mode Register {0}", val);
                sourceModeRegisters[val] = value;
                return;
            }

            if ((val = IsOffsetInRange((uint)offset, (uint)Register.SourceVectorRegister0, 0x04, 32)) != -1)
            {
                this.Log(LogLevel.Noisy, "This was write to Source Vector Register {0}", val);
                sourceVectorRegisters[val] = value;
                return;
            }

            switch ((Register)offset)
            {
            case Register.EndofInterruptCommandRegister:
                
                if (nestedInterruptStack.Count > 0)
                {
                    var irq = nestedInterruptStack.Pop();
                    //CurrentIRQ = irq.Item1;
                    BitHelper.SetBit(ref interruptPendingRegister, (byte)irq.Item1, true);
                }
                else
                {
                    if (CurrentIRQ.HasValue)
                    {
                        BitHelper.SetBit(ref interruptPendingRegister, (byte)CurrentIRQ, false);
                    }
                    this.Log(LogLevel.Noisy, "IRQ set to false");
                    IRQ.Set(false);
                }

                CurrentIRQ = null;

                // save to this register indicates the end of the interrupt handling
                break;

            case Register.InterruptEnableCommandRegister:
                interruptMaskRegister |= value;
                break;

            case Register.SpuriousInterruptVectorRegister:
                spouriousInterruptVectorRegister = value;
                break;
              
            //case Register.DebugControlRegister:
                //debugControlRegister = value;
            //    break;

            case Register.InterruptClearCommandRegister:

                foreach(var irq in BitHelper.GetSetBits(value))
                {
                    if (IsInternalPositiveEdgeTriggered(irq))
                    {
                        BitHelper.SetBit(ref interruptPendingRegister, (byte)irq, false);
                    }
                }

                break;
    
            case Register.InterruptDisableCommandRegister:
                interruptMaskRegister &= ~value;
                //interruptPendingRegister &= ~value;
                break;

            default:
                this.LogUnhandledWrite(offset, value);
                return;
            }
        }
        }

        #endregion

        #region IPeripheral implementation

        public void Reset()
        {

        }

        #endregion

        #region IKnownSize implementation

        public long Size {
            get {
                return 512;
            }
        }

        #endregion

        #region Helper methods

        private int? CalculateCurrentInterrupt()
        {
            var result = (int?)null;
            for (int i = 0; i < level.Length; i++)
            {
                if (level[i])
                {
                    if (result == null || GetPriority(i) > GetPriority(result.Value))
                    {
                        result = i;
                    }
                }
            }

            return result;
        }
        
        private int IsOffsetInRange(uint offset, uint start, uint step, byte count)
        {
            var position  = start;
            var counter = 0;
            do
            {
                if (position == offset)
                {
                    return counter;
                }
                position += step;
                counter++;
            } while (counter < count);
            
            return -1;
        }

        private uint GetPriority(int irq)
        {
            return sourceModeRegisters[irq] & 7u;
        }

        private bool IsIRQEnabled(int irq)
        {
            return BitHelper.IsBitSet(interruptMaskRegister, (byte)irq);
        }

        private bool IsInternalHighlevelSensitive(int irq)
        {
            var val = ((sourceModeRegisters[irq] >> 5) & 3u);
            return val == 0 || val == 2;
        }

        private bool IsInternalPositiveEdgeTriggered(int irq)
        {
            var val = ((sourceModeRegisters[irq] >> 5) & 3u);
            return val == 1 || val == 3;
        }

        private int? CurrentIRQ;

        #endregion

        private bool[] level = new bool[32];

        private uint[] sourceModeRegisters = new uint[32];
        private uint[] sourceVectorRegisters = new uint[32];
        private uint interruptMaskRegister;
        private uint interruptPendingRegister;
        private uint spouriousInterruptVectorRegister;
        //private uint debugControlRegister; // TODO: use only two bits

        private Stack<Tuple<int, int>> nestedInterruptStack = new Stack<Tuple<int, int>>();

        private object localLock = new object();

        private enum Register : uint
        {
            SourceModeRegister0             = 0x000,   // SMR0
            SourceVectorRegister0           = 0x080,   // SVR0
            InterruptVectorRegister         = 0x100,   // IVR
            InterruptStatusRegister         = 0x108,   // ISR
            InterruptPendingRegister        = 0x10C,   // IPR
            InterruptEnableCommandRegister  = 0x120,   // IECR
            InterruptDisableCommandRegister = 0x124,   // IDCR
            InterruptClearCommandRegister   = 0x128,   // ICCR
            EndofInterruptCommandRegister   = 0x130,   // EICR
            SpuriousInterruptVectorRegister = 0x134,   // SIVR
            DebugControlRegister            = 0x138    // DCR
        }
    }
}

