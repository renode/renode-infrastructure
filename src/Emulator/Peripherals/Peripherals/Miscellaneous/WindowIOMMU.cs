//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class WindowIOMMU : SimpleContainer<IBusPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public WindowIOMMU(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            busController = new WindowMMUBusController(this, machine.GetSystemBus(this));
            busController.OnFault += OnMMUFault;

            registers = DefineRegisters();
        }

        public override void Register(IBusPeripheral peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            base.Register(peripheral, registrationPoint);
            machine.RegisterBusController(peripheral, busController);
        }

        public override void Reset()
        {
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            // Writing to a Privileges register asserts are MMU windows valid.
            registers.Write(offset, value);
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        private void OnMMUFault(ulong address, BusAccessPrivileges accessType, int? mmuWindow)
        {
            this.Log(LogLevel.Debug, "IOMMU fault occured. Setting the IRQ");
            IRQ.Set();
        }

        private DoubleWordRegisterCollection DefineRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();

            for(int i = 0; i < MaxWindowsCount; i++)
            {
                var index = i;
                busController.Windows.Add(new WindowMMUBusController.MMUWindow(this));
                registersMap.Add((long)Registers.RangeStartBase + index * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"RANGE_START[{index}]", writeCallback: (_, value) =>
                    {
                        busController.Windows[index].Start = (ulong)value;
                    }, valueProviderCallback: _ =>
                    {
                        return (uint)busController.Windows[index].Start;
                    }));
                registersMap.Add((long)Registers.RangeEndBase + index * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"RANGE_END[{index}]", writeCallback: (_, value) =>
                    {
                        busController.Windows[index].End = (ulong)value;
                    }, valueProviderCallback: _ =>
                    {
                        return (uint)busController.Windows[index].End;
                    }));
                registersMap.Add((long)Registers.OffsetBase + index * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"OFFSET[{index}]", writeCallback: (_, value) =>
                    {
                        busController.Windows[index].Offset = (int)value;
                    }, valueProviderCallback: _ =>
                    {
                        return (uint)busController.Windows[index].Offset;
                    }));
                registersMap.Add((long)Registers.PrivilegesBase + index * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"PRIVILEGES[{index}]", writeCallback: (_, value) =>
                    {
                        busController.Windows[index].Privileges = (BusAccessPrivileges)value;
                        busController.AssertWindowsAreValid();
                    }, valueProviderCallback: _ =>
                    {
                        return (uint)busController.Windows[index].Privileges;
                    }));
            }
            return new DoubleWordRegisterCollection(this, registersMap);
        }

        private readonly WindowMMUBusController busController;
        private readonly DoubleWordRegisterCollection registers;

        private enum Registers
        {
            RangeStartBase = 0x0,
            RangeEndBase = 0x400,
            OffsetBase = 0x800,
            PrivilegesBase = 0xC00,
        }

        private const int MaxWindowsCount = ((int)Registers.RangeEndBase) / 8;
    }
}
