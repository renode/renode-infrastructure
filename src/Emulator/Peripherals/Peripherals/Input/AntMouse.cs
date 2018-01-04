//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Linq;

namespace Antmicro.Renode.Peripherals.Input
{
    public class AntMouse : IDoubleWordPeripheral
    {
        public AntMouse()
        {
            IRQ = new GPIO();
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.X:
                return (uint)x;
            case Registers.Y:
                return (uint)y;
            case Registers.LeftButton:
                return leftButton ? 1u : 0u;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public GPIO IRQ { get; private set; }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.InterruptHandled:
                IRQ.Unset();
                return;
            }
            this.LogUnhandledWrite(offset, value);
        }

        public void Reset()
        {
            x = 0;
            y = 0;
            leftButton = false;
            Refresh();
        }

        public void Move(int newx, int newy)
        {
            x = newx;
            y = newy;
            if(leftButton)
            {
                Refresh();
            }
        }

        public void MouseDown()
        {
            leftButton = true;
            Refresh();
        }

        public void MouseUp()
        {
            leftButton = false;
            Refresh();
        }

        private void Refresh()
        {
            IRQ.Set();
        }

        private enum Registers
        {
            X = 0,
            Y = 4,
            LeftButton = 8,
            InterruptHandled = 12
        }

        private int x;
        private int y;
        private bool leftButton;
    }
}

