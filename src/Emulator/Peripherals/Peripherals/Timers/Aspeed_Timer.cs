//
// Copyright (c) 2026 Microsoft
// Licensed under the MIT License.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    // Aspeed AST2600 Timer Controller
    // Reference: QEMU hw/timer/aspeed_timer.c (AST2600 variant)
    //
    // 8 down-counting timers. Each has STATUS (current count), RELOAD,
    // MATCH1, MATCH2 registers. A shared CTRL register at 0x30 has 4 bits
    // per timer (enable, ext_clock, overflow_irq, pulse_enable).
    //
    // Register layout:
    //   Timer 1-4: 0x00-0x2C  (timer_idx = offset >> 4)
    //   CTRL:      0x30
    //   IRQ_STS:   0x34  (AST2600-specific)
    //   Timer 5-8: 0x40-0x8C  (timer_idx = (offset >> 4) - 1)
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_Timer : IDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public Aspeed_Timer(IMachine machine)
        {
            this.machine = machine;
            var dict = new Dictionary<int, IGPIO>();
            for(int i = 0; i < TimerCount; i++)
            {
                dict[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(dict);

            timerReload = new uint[TimerCount];
            timerMatch0 = new uint[TimerCount];
            timerMatch1 = new uint[TimerCount];
            timerStatus = new uint[TimerCount];
            timerEnabled = new bool[TimerCount];

            ctrl = 0;
            irqStatus = 0;
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x100;

        public void Reset()
        {
            ctrl = 0;
            irqStatus = 0;
            for(int i = 0; i < TimerCount; i++)
            {
                timerReload[i] = 0;
                timerMatch0[i] = 0;
                timerMatch1[i] = 0;
                timerStatus[i] = 0;
                timerEnabled[i] = false;
                Connections[i].Unset();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset == CtrlOffset)
            {
                return ctrl;
            }
            if(offset == IrqStsOffset)
            {
                return irqStatus;
            }

            int timerIdx = GetTimerIndex(offset);
            if(timerIdx < 0)
            {
                return 0;
            }

            int reg = ((int)offset & 0xF) / 4;
            switch(reg)
            {
                case RegStatus:
                    if(timerEnabled[timerIdx] && timerReload[timerIdx] > 0)
                    {
                        if(timerStatus[timerIdx] > 0)
                        {
                            timerStatus[timerIdx]--;
                        }
                        else
                        {
                            timerStatus[timerIdx] = timerReload[timerIdx];
                            if(IsOverflowIrqEnabled(timerIdx))
                            {
                                irqStatus |= (1u << timerIdx);
                                Connections[timerIdx].Set();
                            }
                        }
                        return timerStatus[timerIdx];
                    }
                    return timerReload[timerIdx];

                case RegReload:
                    return timerReload[timerIdx];

                case RegMatch1:
                    return timerMatch0[timerIdx];

                case RegMatch2:
                    return timerMatch1[timerIdx];

                default:
                    return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset == CtrlOffset)
            {
                SetControl(value);
                return;
            }
            if(offset == IrqStsOffset)
            {
                irqStatus &= ~value;
                for(int i = 0; i < TimerCount; i++)
                {
                    if((value & (1u << i)) != 0)
                    {
                        Connections[i].Unset();
                    }
                }
                return;
            }

            int timerIdx = GetTimerIndex(offset);
            if(timerIdx < 0)
            {
                return;
            }

            int reg = ((int)offset & 0xF) / 4;
            switch(reg)
            {
                case RegStatus:
                    timerStatus[timerIdx] = value;
                    break;
                case RegReload:
                    timerReload[timerIdx] = value;
                    if(timerEnabled[timerIdx] && value > 0)
                    {
                        timerStatus[timerIdx] = value;
                    }
                    break;
                case RegMatch1:
                    timerMatch0[timerIdx] = value;
                    break;
                case RegMatch2:
                    timerMatch1[timerIdx] = value;
                    break;
            }
        }

        private void SetControl(uint value)
        {
            uint oldCtrl = ctrl;
            ctrl = value;

            for(int i = 0; i < TimerCount; i++)
            {
                bool wasEnabled = (oldCtrl & (1u << (i * CtrlBitsPerTimer))) != 0;
                bool nowEnabled = (value & (1u << (i * CtrlBitsPerTimer))) != 0;

                timerEnabled[i] = nowEnabled;

                if(!wasEnabled && nowEnabled)
                {
                    timerStatus[i] = timerReload[i];
                }
            }
        }

        private bool IsOverflowIrqEnabled(int timerIdx)
        {
            return (ctrl & (1u << (timerIdx * CtrlBitsPerTimer + 2))) != 0;
        }

        private int GetTimerIndex(long offset)
        {
            if(offset >= 0x00 && offset <= 0x2F)
            {
                return (int)(offset >> 4);
            }
            if(offset >= 0x40 && offset <= 0x8F)
            {
                return (int)((offset >> 4) - 1);
            }
            return -1;
        }

        private readonly IMachine machine;

        private uint[] timerReload;
        private uint[] timerMatch0;
        private uint[] timerMatch1;
        private uint[] timerStatus;
        private bool[] timerEnabled;
        private uint ctrl;
        private uint irqStatus;

        private const int TimerCount = 8;
        private const int CtrlBitsPerTimer = 4;
        private const int CtrlOffset = 0x30;
        private const int IrqStsOffset = 0x34;

        private const int RegStatus = 0;
        private const int RegReload = 1;
        private const int RegMatch1 = 2;
        private const int RegMatch2 = 3;
    }
}
