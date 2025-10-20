//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ExternalMmuBase : IPeripheral
    {
        public ExternalMmuBase(ICPUWithExternalMmu cpu, uint windowsCount, ExternalMmuPosition position = ExternalMmuPosition.Replace)
        {
            this.cpu = cpu;
            this.windowsCount = windowsCount;
            windowMapping = new Dictionary<uint, ulong>();

            cpu.EnableExternalWindowMmu(position);
            for(uint index = 0; index < windowsCount; index++)
            {
                AddWindow(index);
            }
        }

        public virtual void Reset()
        {
            foreach(var realIndex in windowMapping.Values)
            {
                cpu.ResetMmuWindow(realIndex);
            }
            windowMapping.Clear();
        }

        public void SetWindowStart(uint index, ulong startAddress)
        {
            if(TryGetRealWindowIndex(index, out var realIndex))
            {
                cpu.SetMmuWindowStart(realIndex, startAddress);
            }
        }

        public void SetWindowEnd(uint index, ulong endAddress)
        {
            if(TryGetRealWindowIndex(index, out var realIndex))
            {
                cpu.SetMmuWindowEnd(realIndex, endAddress);
            }
        }

        public ulong GetWindowStart(uint index)
        {
            if(TryGetRealWindowIndex(index, out var realIndex))
            {
                return cpu.GetMmuWindowStart(realIndex);
            }
            return 0;
        }

        public ulong GetWindowEnd(uint index)
        {
            if(TryGetRealWindowIndex(index, out var realIndex))
            {
                return cpu.GetMmuWindowEnd(realIndex);
            }
            return 0;
        }

        public void SetWindowAddend(uint index, ulong addend)
        {
            if(TryGetRealWindowIndex(index, out var realIndex))
            {
                cpu.SetMmuWindowAddend(realIndex, addend);
            }
        }

        public void SetWindowPrivileges(uint index, Privilege privileges)
        {
            if(TryGetRealWindowIndex(index, out var realIndex))
            {
                cpu.SetMmuWindowPrivileges(realIndex, privileges);
            }
        }

        public ulong GetWindowAddend(uint index)
        {
            if(TryGetRealWindowIndex(index, out var realIndex))
            {
                return cpu.GetMmuWindowAddend(realIndex);
            }
            return 0;
        }

        public uint GetWindowPrivileges(uint index)
        {
            if(TryGetRealWindowIndex(index, out var realIndex))
            {
                return cpu.GetMmuWindowPrivileges(realIndex);
            }
            return 0;
        }

        public bool ContainsWindowWithIndex(uint index)
        {
            return windowMapping.ContainsValue(index);
        }

        protected void AddWindow(uint index, ulong? rangeStart = null, ulong? rangeEnd = null, ulong? addend = null, Privilege? privilege = null, Privilege type = Privilege.All)
        {
            var realIndex = cpu.AcquireExternalMmuWindow(type);
            windowMapping.Add(index, realIndex);

            if(rangeStart.HasValue)
            {
                cpu.SetMmuWindowStart(realIndex, rangeStart.Value);
            }
            if(rangeEnd.HasValue)
            {
                cpu.SetMmuWindowEnd(realIndex, rangeEnd.Value);
            }
            if(addend.HasValue)
            {
                cpu.SetMmuWindowAddend(realIndex, addend.Value);
            }
            if(privilege.HasValue)
            {
                cpu.SetMmuWindowPrivileges(realIndex, privilege.Value);
            }
        }

        private bool TryGetRealWindowIndex(uint index, out ulong realIndex)
        {
            realIndex = 0;
            if(index >= windowsCount)
            {
                this.Log(LogLevel.Error, "Window index {0} is higher than the peripheral windows count: {1}", index, windowsCount);
                return false;
            }
            realIndex = windowMapping[index];
            return true;
        }

        // There might be more than one ExternalMmu for a single CPU, hence the MMU window index is not the CPU MMU window index
        private readonly Dictionary<uint, ulong> windowMapping;
        private readonly ICPUWithExternalMmu cpu;
        private readonly uint windowsCount;

        public enum Privilege : uint
        {
            Read = 0b001,
            Write = 0b010,
            ReadAndWrite = 0b011,
            Execute = 0b100,
            All = 0b111,
        }
    }
}