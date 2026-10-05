//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Logging.Profiling;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class NRF52840_NVMC : BasicDoubleWordPeripheral, IKnownSize
    {
        public NRF52840_NVMC(IMachine machine) : base(machine)
        {
            busyUntil = TimeInterval.Empty;
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            busyUntil = TimeInterval.Empty;
            configMode.Value = ConfigMode.ReadOnly;
            UpdateMemoryHooks(configMode.Value);
        }

        public long Size => 0x1000;

        private bool IsReady
        {
            get
            {
                if(busyUntil == TimeInterval.Empty)
                {
                    return true;
                }
                var now = machine.ClockSource.CurrentValue;
                if(now >= busyUntil)
                {
                    busyUntil = TimeInterval.Empty;
                    return true;
                }
                return false;
            }
        }

        private void ErasePage(uint address, bool isPartial)
        {
            var pageAddr = address & ~(PageSize - 1);
            var eraseBuffer = new byte[PageSize];
            Array.Fill(eraseBuffer, (byte)0xFF);

            try
            {
                machine.SystemBus.WriteBytes(eraseBuffer, pageAddr);
            }
            catch(Exception e)
            {
                this.Log(LogLevel.Warning, "Failed to erase page at 0x{0:X8}: {1}", pageAddr, e.Message);
            }

            var duration = isPartial ? PartialEraseDurationMs : PageEraseDurationMs;
            if(duration > 0)
            {
                var now = machine.ClockSource.CurrentValue;
                busyUntil = now + TimeInterval.FromMilliseconds(duration);
            }
            this.Log(LogLevel.Info, "Erase {0} page at 0x{1:X8} (duration {2} ms)", isPartial ? "partial" : "full", pageAddr, duration);
        }

        private void EraseAll()
        {
            var duration = PageEraseDurationMs * 256;
            if(duration > 0)
            {
                var now = machine.ClockSource.CurrentValue;
                busyUntil = now + TimeInterval.FromMilliseconds(duration);
            }
            this.Log(LogLevel.Info, "Erased all flash memory (duration {0} ms)", duration);
        }

        private void DefineRegisters()
        {
            Registers.Ready.Define(this, 0x1)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => IsReady, name: "READY")
                .WithReservedBits(1, 31);

            Registers.ReadyNext.Define(this, 0x1)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => IsReady, name: "READYNEXT")
                .WithReservedBits(1, 31);

            Registers.Config.Define(this)
                .WithEnumField<DoubleWordRegister, ConfigMode>(0, 2, out configMode, writeCallback: (_, val) => UpdateMemoryHooks(val), name: "WEN")
                .WithReservedBits(2, 30);

            Registers.ErasePage.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => ErasePage((uint)val, false), name: "ERASEPAGE");

            Registers.EraseAll.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) EraseAll(); }, name: "ERASEALL")
                .WithReservedBits(1, 31);

            Registers.ErasePcr0.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => ErasePage((uint)val, false), name: "ERASEPCR0");

            Registers.EraseUicr.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) ErasePage(0x10001000, false); }, name: "ERASEUICR")
                .WithReservedBits(1, 31);

            Registers.ErasePagePartial.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => ErasePage((uint)val, true), name: "ERASEPAGEPARTIAL");

            Registers.ErasePagePartialConfig.Define(this, 0x2)
                .WithValueField(0, 32, name: "DURATION");

            Registers.ICacheConfig.Define(this)
                .WithFlag(0, name: "CACHEEN")
                .WithReservedBits(1, 31);

            Registers.IHit.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "IHIT");

            Registers.IMiss.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "IMISS");
        }

        private void UpdateMemoryHooks(ConfigMode mode)
        {
            try
            {
                var sysbus = machine.SystemBus;
                if(sysbus == null)
                {
                    return;
                }
                var cpus = sysbus.GetCPUs().OfType<ICPUWithMemoryAccessHooks>().ToList();
                foreach(var cpu in cpus)
                {
                    cpu.SetHookAtMemoryAccess(OnFlashMemoryAccess);
                }
            }
            catch(Exception e)
            {
                this.Log(LogLevel.Warning, "Could not attach flash memory access hook: {0}", e.Message);
            }
        }

        private void OnFlashMemoryAccess(ulong pc, MemoryOperation operation, ulong virtualAddress, ulong physicalAddress, uint width, ulong value)
        {
            if(operation != MemoryOperation.MemoryWrite)
            {
                return;
            }

            bool isFlash = (physicalAddress >= FlashBase && physicalAddress < FlashBase + FlashSize);
            bool isUICR = (physicalAddress >= 0x10001000 && physicalAddress < 0x10001800);

            if(!isFlash && !isUICR)
            {
                return;
            }

            if(configMode.Value != ConfigMode.Write)
            {
                this.Log(LogLevel.Warning, "Write to Flash at 0x{0:X8} (val=0x{1:X}, width={2}) while NVMC is not in Write mode (WEN={3}) at PC 0x{4:X8}", physicalAddress, value, width, configMode.Value, pc);
                var cpu = machine.GetSystemBus(this).GetCPUs().OfType<CortexM>().FirstOrDefault();
                cpu?.RaisePreciseBusFault(physicalAddress);
                return;
            }

            if(width != 4 || (physicalAddress & 0x3) != 0)
            {
                this.Log(LogLevel.Error, "Unaligned or non-word Flash write to 0x{0:X8} (width {1} bytes, val=0x{2:X}) at PC 0x{3:X8} - nRF52 NVMC requires 32-bit aligned word writes!", physicalAddress, width, value, pc);
                var cpu = machine.GetSystemBus(this).GetCPUs().OfType<CortexM>().FirstOrDefault();
                cpu?.RaisePreciseBusFault(physicalAddress);
                return;
            }

            if(WordWriteDurationUs > 0)
            {
                var now = machine.ClockSource.CurrentValue;
                busyUntil = now + TimeInterval.FromMicroseconds(WordWriteDurationUs);
            }
        }

        private TimeInterval busyUntil;
        private IEnumRegisterField<ConfigMode> configMode;

        private const uint PageSize = 4096;
        private const uint PageEraseDurationMs = 85;
        private const uint PartialEraseDurationMs = 2;
        private const ulong WordWriteDurationUs = 41;
        private const ulong FlashBase = 0x0;
        private const ulong FlashSize = 0x100000;

        private enum ConfigMode
        {
            ReadOnly = 0,
            Write = 1,
            Erase = 2,
        }

        private enum Registers : long
        {
            Ready = 0x400,
            ReadyNext = 0x408,
            Config = 0x504,
            ErasePage = 0x508,
            EraseAll = 0x50C,
            ErasePcr0 = 0x510,
            EraseUicr = 0x514,
            ErasePagePartial = 0x518,
            ErasePagePartialConfig = 0x51C,
            ICacheConfig = 0x540,
            IHit = 0x548,
            IMiss = 0x54C
        }
    }
}
