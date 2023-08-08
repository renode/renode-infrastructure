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

namespace Antmicro.Renode.Peripherals.I2C
{
    public class TegraDVC : TegraI2CController
    {
        public TegraDVC(IMachine machine) : base(machine)
        {
        }

        public override uint ReadDoubleWord (long offset)
        {
            if (offset < 0x40) {
                switch ((Registers)offset) {
                case Registers.Control1:
                    return control [0];
                case Registers.Control2:
                    return control [1];
                case Registers.Control3:
                    return control [2];
                case Registers.Status:
                    return status;
                default:
                    this.LogUnhandledRead(offset);
                    return 0;
                }
            }
            return base.ReadDoubleWord ((offset >= 0x60) ? offset - 0x10 : offset - 0x40);
        }

        public override void WriteDoubleWord (long offset, uint value)
        {
            if (offset < 0x40) {
                switch ((Registers)offset) {
                case Registers.Control1:
                    control [0] = value;
                    break;
                case Registers.Control2:
                    control [1] = value;
                    break;
                case Registers.Control3:
                    control [2] = value;
                    break;
                case Registers.Status:
                    status = value;
                    break;
                default:
                    this.LogUnhandledWrite(offset, value);
                    break;
                }
                return;
            }
            base.WriteDoubleWord ((offset >= 0x60) ? offset - 0x10 : offset - 0x40 , value);
        }

        private uint status;
        private uint[] control = new uint[3];
        private enum Registers
        {
            Control1 = 0x0,
            Control2 = 0x4,
            Control3 = 0x8,
            Status = 0xC,
        }
    }
}

