//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Extensions;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public sealed class MPC5567_INTC : IIRQController, IKnownSize, IDoubleWordPeripheral, IBytePeripheral
    {
        public MPC5567_INTC()
        {
            sync = new object();
            IRQ = new GPIO();
            priorities = new byte[NumberOfInterrupts];
            pendingInterrupts = new bool[NumberOfInterrupts];
            acknowledgedInterrupts = new Stack<int>();
        }

        public byte ReadByte(long offset)
        {
            if(offset >= (long)Register.InterruptPriority0 && offset <= (long)Register.InterruptPriorityLast)
            {
                return HandlePriorityRead(offset - (long)Register.InterruptPriority0);
            }
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {
            if(offset >= (long)Register.InterruptPriority0 && offset <= (long)Register.InterruptPriorityLast)
            {
                HandlePriorityWrite(offset - (long)Register.InterruptPriority0, value);
                return;
            }
            if(offset >= (long)Register.SoftwareSetClear0 && offset <= (long)Register.SoftwareSetClearLast)
            {
                HandleSoftwareSetClearWrite(offset - (long)Register.SoftwareSetClear0, value);
                return;
            }
            this.LogUnhandledWrite(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Register)offset)
            {
            case Register.InterruptAcknowledge:
                return AcknowledgeInterrupts();
            case Register.CurrentPriority:
                return (uint)(acknowledgedInterrupts.Count > 0 ? acknowledgedInterrupts.Peek() : 0);				
            default:
                if(offset >= (long)Register.InterruptPriority0 && offset <= (long)Register.InterruptPriorityLast)
                {
                    return this.ReadDoubleWordUsingByte(offset);
                }
                this.LogUnhandledRead(offset);
                break;
            }
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Register)offset)
            {
            case Register.Configuration:
                if(value != 0)
                {
                    this.Log(LogLevel.Warning, "Unhandled configuration value written 0x{0:X}.", value);
                }
                break;
            case Register.CurrentPriority:
                if(value != 0)
                {
                    this.Log(LogLevel.Warning, "Unhandled priority value written 0x{0:X}.", value);
                }
                break;
            case Register.EndOfInterrupt:
                HandleEndOfInterrupt();
                break;
            default:
                if(offset >= (long)Register.InterruptPriority0 && offset <= (long)Register.InterruptPriorityLast)
                {
                    this.WriteDoubleWordUsingByte(offset, value);
                    break;
                }
                if(offset >= (long)Register.SoftwareSetClear0 && offset <= (long)Register.SoftwareSetClearLast)
                {
                    this.WriteDoubleWordUsingByte(offset, value);
                    break;
                }
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public GPIO IRQ { get; private set; }

        public void OnGPIO(int number, bool value)
        {
            lock(sync)
            {
                pendingInterrupts[number] = value;
                Update();
            }
        }

        public void Reset()
        {
            acknowledgedInterrupts.Clear();
            Array.Clear(pendingInterrupts, 0, pendingInterrupts.Length);
            Array.Clear(priorities, 0, priorities.Length);
        }

        public long Size { get { return 0x4000; } }

        private byte HandlePriorityRead(long interruptNo)
        {
            lock(sync)
            {
                return priorities[interruptNo];
            }
        }

        private void HandlePriorityWrite(long interruptNo, byte value)
        {
            lock(sync)
            {
                priorities[interruptNo] = value;
            }
        }

        private void HandleSoftwareSetClearWrite(long interruptNo, byte value)
        {
            var set = (value & 2) != 0;
            var clear = (value & 1) != 0;
            if(set && clear)
            {
                set = false;
            }
            if(set)
            {
                OnGPIO((int)interruptNo, true);
            }
            if(clear)
            {
                OnGPIO((int)interruptNo, false);
            }
        }

        private void HandleEndOfInterrupt()
        {
            lock(sync)
            {
                if(acknowledgedInterrupts.Count > 0)
                {
                    acknowledgedInterrupts.Pop();
                }
                Update();
            }
        }

        private uint AcknowledgeInterrupts()
        {
            lock(sync)
            {
                var best = FindBestInterrupt();
                acknowledgedInterrupts.Push(best);
                IRQ.Unset(); // since we've selected the best interrupt
                return (uint)best * 4;
            }
        }

        private void Update()
        {
            var result = FindBestInterrupt();
            IRQ.Set(result != -1);
        }

        private int FindBestInterrupt()
        {
            var result = -1;
            for(var i = 0; i < pendingInterrupts.Length; i++)
            {
                if(pendingInterrupts[i] && (result == -1 || priorities[i] > priorities[result]) && !acknowledgedInterrupts.Contains(i))
                {
                    result = i;
                }
            }
            return result;
        }

        private readonly Stack<int> acknowledgedInterrupts;
        private readonly bool[] pendingInterrupts;
        private readonly byte[] priorities;
        private readonly object sync;

        private const int NumberOfInterrupts = 360;

        [RegisterMapper.RegistersDescription]
        private enum Register
        {
            Configuration = 0x0,
            // INTC_MCR, INTC module configuration register
            CurrentPriority = 0x8,
            InterruptAcknowledge = 0x10,
            EndOfInterrupt = 0x18,
            SoftwareSetClear0 = 0x20,
            SoftwareSetClearLast = 0x27,
            InterruptPriority0 = 0x40,
            InterruptPriorityLast = InterruptPriority0 + NumberOfInterrupts
        }
    }
}

