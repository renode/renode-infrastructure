//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class OMAP_Timer : LimitTimer, IDoubleWordPeripheral, IKnownSize
    {
        public OMAP_Timer(IMachine machine, long frequency) : base(machine.ClockSource, frequency, direction: Direction.Ascending, limit: uint.MaxValue)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Tidr, new DoubleWordRegister(this, 0x40000100)
                    .WithTag("Y_MINOR", 0, 6)
                    .WithTag("CUSTOM", 6, 2)
                    .WithTag("X_MAJOR", 8, 3)
                    .WithTag("R_RTL", 11, 5)
                    .WithTag("FUNC", 16, 12)
                    .WithReservedBits(28, 2)
                    .WithTag("SCHEME", 30, 2)
                },
                {(long)Registers.TiocpCfg, new DoubleWordRegister(this)
                    .WithTaggedFlag("SOFTRESET", 0)
                    .WithTaggedFlag("EMUFREE", 1)
                    .WithTag("IDLEMODE", 2, 2)
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.Tier, new DoubleWordRegister(this)
                    .WithTaggedFlag("DMAEvent_Ack", 0)
                },
                {(long)Registers.Tistatr, new DoubleWordRegister(this)
                    .WithTaggedFlag("MAT_IT_FLAG", 0)
                    .WithTaggedFlag("OVF_IT_FLAG", 1)
                    .WithTaggedFlag("TCAR_IT_FLAG", 2)
                    .WithReservedBits(3, 29)
                },
                {(long)Registers.Tistat, new DoubleWordRegister(this)
                    .WithTaggedFlag("MAT_IT_FLAG", 0)
                    .WithTaggedFlag("OVF_IT_FLAG", 1)
                    .WithTaggedFlag("TCAR_IT_FLAG", 2)
                    .WithReservedBits(3, 29)
                },
                {(long)Registers.Tisr, new DoubleWordRegister(this)
                    .WithTaggedFlag("MAT_EN_FLAG", 0)
                    .WithTaggedFlag("OVF_EN_FLAG", 1)
                    .WithTaggedFlag("TCAR_EN_FLAG", 2)
                    .WithReservedBits(3, 29)
                },
                {(long)Registers.Tcicr, new DoubleWordRegister(this)
                    .WithTaggedFlag("MAT_EN_FLAG", 0)
                    .WithTaggedFlag("OVF_EN_FLAG", 1)
                    .WithTaggedFlag("TCAR_EN_FLAG", 2)
                    .WithReservedBits(3, 29)
                },
                {(long)Registers.Twer, new DoubleWordRegister(this)
                    .WithTaggedFlag("MAT_WUP_ENA", 0)
                    .WithTaggedFlag("OVF_WUP_ENA", 1)
                    .WithTaggedFlag("TCAR_WUP_ENA", 2)
                    .WithReservedBits(3, 29)
                },
                {(long)Registers.Tclr, new DoubleWordRegister(this, 0x38)
                    .WithFlag(0, valueProviderCallback: _ => Enabled,
                        writeCallback: (_, value) => Enabled = value, name: "ST")
                    .WithFlag(1, valueProviderCallback: _ => AutoUpdate,
                        writeCallback: (_, value) => AutoUpdate = value, name: "AR")
                    .WithTag("PTV", 2, 3)
                    .WithTaggedFlag("PRE", 5)
                    .WithTaggedFlag("CE", 6)
                    .WithTaggedFlag("SCPWM", 7)
                    .WithTag("TCM", 8, 2)
                    .WithTag("TRG", 10, 2)
                    .WithTaggedFlag("PT", 12)
                    .WithTaggedFlag("CAPT_MODE", 13)
                    .WithTaggedFlag("GPO_CFG", 14)
                    .WithReservedBits(15, 17)
                },
                {(long)Registers.Tcrr, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ =>
                    {
                        if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                        {
                            cpu.SyncTime();
                        }
                        return (uint)Value;
                    }, writeCallback: (_, value) => Value = value, name: "TCRR")
                },
                {(long)Registers.Tldr, new DoubleWordRegister(this)
                    .WithTag("LOAD_VALUE", 0, 32)
                },
                {(long)Registers.Ttgr, new DoubleWordRegister(this, 0xffffffff)
                    .WithTag("TTGR_VALUE", 0, 32)
                },
                {(long)Registers.Twps, new DoubleWordRegister(this)
                    .WithTaggedFlag("W_PEND_TCLR", 0)
                    .WithTaggedFlag("W_PEND_TCRR", 1)
                    .WithTaggedFlag("W_PEND_TLDR", 2)
                    .WithTaggedFlag("W_PEND_TTGR", 3)
                    .WithTaggedFlag("W_PEND_TMAR", 4)
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.Tmar, new DoubleWordRegister(this)
                    .WithTag("COMPARE_VALUE", 0, 32)
                },
                {(long)Registers.Tcar1, new DoubleWordRegister(this)
                    .WithTag("CAPTURED", 0, 32)
                },
                {(long)Registers.Tsicr, new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithTaggedFlag("SFT", 1)
                    .WithTaggedFlag("POSTED", 2)
                    .WithReservedBits(3, 29)
                },
                {(long)Registers.Tcar2, new DoubleWordRegister(this)
                    .WithTag("CAPTURED", 0, 32)
                },
             };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
        }

        public long Size => 0x100;

        private readonly DoubleWordRegisterCollection registers;

        private enum Registers : long
        {
            Tidr = 0x00,
            // gap
            TiocpCfg = 0x10,
            // gap
            Tier = 0x20,
            Tistatr = 0x24,
            Tistat = 0x28,
            Tisr = 0x2c,
            Tcicr = 0x30,
            Twer = 0x34,
            Tclr = 0x38,
            Tcrr = 0x3c,
            Tldr = 0x40,
            Ttgr = 0x44,
            Twps = 0x48,
            Tmar = 0x4c,
            Tcar1 = 0x50,
            Tsicr = 0x54,
            Tcar2 = 0x58,
        }
    }
}
