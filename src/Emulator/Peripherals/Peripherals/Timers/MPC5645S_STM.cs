//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Alexey Zagorodnikov <xglooom@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    // MPC56xx STM control/counter subset used by the MPC5645S startup path.
    // The counter is scheduler-backed and advances only while CR[TEN] is set.
    public class MPC5645S_STM : IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize, IHasFrequency
    {
        public MPC5645S_STM(IMachine machine, ulong frequency = DefaultFrequency)
        {
            this.machine = machine;
            timer = new LimitTimer(machine.ClockSource, frequency, this, nameof(timer),
                limit: MaximumPeriod, direction: Direction.Ascending, enabled: false,
                workMode: WorkMode.Periodic, eventEnabled: true, autoUpdate: false);
            Reset();
        }

        public void Reset()
        {
            lock(sync)
            {
                control = 0;
                timer.Reset();
                timer.AutoUpdate = false;
                timer.EventEnabled = true;
                timer.Limit = MaximumPeriod;
                timer.Value = 0;
                timer.Divider = 1;
                timer.Enabled = false;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadRegister(offset & ~0x3L);
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)((ReadByte(offset) << 8) | ReadByte(offset + 1));
        }

        public byte ReadByte(long offset)
        {
            var alignedOffset = offset & ~0x3L;
            var shift = (int)((3 - (offset & 0x3L)) * 8);
            return (byte)(ReadRegister(alignedOffset) >> shift);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteSized(offset, 4, value);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteSized(offset, 2, value);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteSized(offset, 1, value);
        }

        public ulong Frequency
        {
            get => timer.Frequency;
            set => timer.Frequency = value;
        }

        public long Size => 0x4000;

        private static uint Merge(uint previous, uint value, uint mask)
        {
            return (previous & ~mask) | (value & mask);
        }

        private uint ReadRegister(long offset)
        {
            lock(sync)
            {
                switch(offset)
                {
                case ControlOffset:
                    return control;
                case CountOffset:
                    SynchronizeCurrentCpuTime();
                    return (uint)timer.Value;
                default:
                    return 0;
                }
            }
        }

        private void WriteSized(long offset, int size, uint value)
        {
            var lane = (int)(offset & 0x3L);
            if(size == 4 && lane == 0)
            {
                WriteRegister(offset, value, uint.MaxValue);
                return;
            }
            if(size != 1 && size != 2)
            {
                return;
            }
            if(lane + size > 4)
            {
                for(var i = 0; i < size; i++)
                {
                    var shift = (size - 1 - i) * 8;
                    WriteSized(offset + i, 1, (value >> shift) & 0xFFu);
                }
                return;
            }

            var alignedOffset = offset & ~0x3L;
            var bitShift = (4 - lane - size) * 8;
            var valueMask = size == 1 ? 0xFFu : 0xFFFFu;
            var mask = valueMask << bitShift;
            WriteRegister(alignedOffset, (value & valueMask) << bitShift, mask);
        }

        private void WriteRegister(long offset, uint value, uint mask)
        {
            lock(sync)
            {
                switch(offset)
                {
                case ControlOffset:
                    control = Merge(control, value, mask) & ControlWritableMask;
                    timer.Divider = ((control & CounterPrescalerMask) >> CounterPrescalerShift) + 1u;
                    timer.Enabled = (control & TimerEnableMask) != 0;
                    break;
                case CountOffset:
                    SynchronizeCurrentCpuTime();
                    var current = (uint)timer.Value;
                    timer.Value = Merge(current, value, mask);
                    break;
                }
            }
        }

        private void SynchronizeCurrentCpuTime()
        {
            if(machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }
        }

        private uint control;

        private readonly object sync = new object();
        private readonly IMachine machine;
        private readonly LimitTimer timer;

        private const ulong DefaultFrequency = 40000000;
        private const ulong MaximumPeriod = 1UL << 32;
        private const long ControlOffset = 0x0;
        private const long CountOffset = 0x4;
        private const uint TimerEnableMask = 1u << 0;
        private const uint FreezeMask = 1u << 1;
        private const int CounterPrescalerShift = 8;
        private const uint CounterPrescalerMask = 0xFFu << CounterPrescalerShift;
        private const uint ControlWritableMask = TimerEnableMask | FreezeMask | CounterPrescalerMask;
    }
}
