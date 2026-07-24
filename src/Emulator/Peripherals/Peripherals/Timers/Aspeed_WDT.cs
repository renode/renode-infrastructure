//
// Copyright (c) 2026 Microsoft
// Licensed under the MIT License.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Timers
{
    // Aspeed AST2600 Watchdog Timer
    // Reference: QEMU hw/watchdog/wdt_aspeed.c (AST2600 variant)
    //
    // 4 instances on AST2600. Each has a 32-bit down-counter at 1 MHz.
    // Writing magic 0x4755 to RESTART copies RELOAD into STATUS.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_WDT : BasicDoubleWordPeripheral, IKnownSize
    {
        public Aspeed_WDT(IMachine machine) : base(machine)
        {
            DefineRegisters();
            // Set initial state (Reset() may not be called before first access)
            counterStatus = DefaultStatus;
            reloadValue = DefaultReloadValue;
            controlReg = 0;
            resetWidth = 0xFF;
        }

        public long Size => 0x40;

        public override void Reset()
        {
            base.Reset();
            counterStatus = DefaultStatus;
            reloadValue = DefaultReloadValue;
            controlReg = 0;
            resetWidth = 0xFF;
        }

        private void DefineRegisters()
        {
            Registers.Status.Define(this, DefaultStatus)
                .WithValueField(0, 32, name: "WDT_STATUS",
                    valueProviderCallback: _ => counterStatus,
                    writeCallback: (_, __) =>
                    {
                        this.Log(LogLevel.Warning, "Write to read-only WDT_STATUS");
                    });

            Registers.ReloadValue.Define(this, DefaultReloadValue)
                .WithValueField(0, 32, name: "WDT_RELOAD",
                    valueProviderCallback: _ => reloadValue,
                    writeCallback: (_, value) => { reloadValue = (uint)value; });

            Registers.Restart.Define(this, 0x0)
                .WithValueField(0, 32, name: "WDT_RESTART",
                    valueProviderCallback: _ => 0,
                    writeCallback: (_, value) =>
                    {
                        if((value & 0xFFFF) == RestartMagic)
                        {
                            counterStatus = reloadValue;
                            this.Log(LogLevel.Debug, "WDT restarted, counter = 0x{0:X8}", counterStatus);
                        }
                    });

            Registers.Control.Define(this, 0x0)
                .WithValueField(0, 32, name: "WDT_CTRL",
                    valueProviderCallback: _ => controlReg,
                    writeCallback: (_, value) =>
                    {
                        controlReg = (uint)(value & ~(0x7u << 7));
                    });

            Registers.TimeoutStatus.Define(this, 0x0)
                .WithValueField(0, 32, name: "WDT_TIMEOUT_STS");

            Registers.TimeoutClear.Define(this, 0x0)
                .WithValueField(0, 32, name: "WDT_TIMEOUT_CLR");

            Registers.ResetWidth.Define(this, 0xFF)
                .WithValueField(0, 32, name: "WDT_RESET_WIDTH",
                    valueProviderCallback: _ => resetWidth,
                    writeCallback: (_, value) =>
                    {
                        uint polarity = (uint)(value & 0xFF000000);
                        switch(polarity)
                        {
                            case ActiveHighMagic:
                                resetWidth |= (1u << 31);
                                break;
                            case ActiveLowMagic:
                                resetWidth &= ~(1u << 31);
                                break;
                            case PushPullMagic:
                                resetWidth |= (1u << 30);
                                break;
                            case OpenDrainMagic:
                                resetWidth &= ~(1u << 30);
                                break;
                        }
                        resetWidth = (resetWidth & 0xFFF00000) | ((uint)value & 0x000FFFFF);
                    });

            Registers.ResetMask1.Define(this, 0x0)
                .WithValueField(0, 32, name: "WDT_RESET_MASK1");

            Registers.ResetMask2.Define(this, 0x0)
                .WithValueField(0, 32, name: "WDT_RESET_MASK2");

            Registers.SWResetControl.Define(this, 0x0)
                .WithValueField(0, 32, name: "WDT_SW_RESET_CTRL",
                    writeCallback: (_, value) =>
                    {
                        if((uint)value == SWResetEnable)
                        {
                            this.Log(LogLevel.Warning, "WDT SW reset requested (ignored in emulation)");
                        }
                    });

            Registers.SWResetMask1.Define(this, 0x0)
                .WithValueField(0, 32, name: "WDT_SW_RESET_MASK1");

            Registers.SWResetMask2.Define(this, 0x0)
                .WithValueField(0, 32, name: "WDT_SW_RESET_MASK2");
        }

        private uint counterStatus;
        private uint reloadValue;
        private uint controlReg;
        private uint resetWidth;

        private const uint DefaultStatus = 0x014FB180;
        private const uint DefaultReloadValue = 0x014FB180;
        private const uint RestartMagic = 0x4755;
        private const uint SWResetEnable = 0xAEEDF123;

        private const uint ActiveHighMagic = 0xA5000000;
        private const uint ActiveLowMagic  = 0x5A000000;
        private const uint PushPullMagic   = 0xA8000000;
        private const uint OpenDrainMagic  = 0x8A000000;

        private enum Registers
        {
            Status          = 0x00,
            ReloadValue     = 0x04,
            Restart         = 0x08,
            Control         = 0x0C,
            TimeoutStatus   = 0x10,
            TimeoutClear    = 0x14,
            ResetWidth      = 0x18,
            ResetMask1      = 0x1C,
            ResetMask2      = 0x20,
            SWResetControl  = 0x24,
            SWResetMask1    = 0x28,
            SWResetMask2    = 0x2C,
        }
    }
}
