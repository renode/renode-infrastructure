//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.DoubleWordToQuadWord)]
    public class NEORV32_MachineSystemTimer : IQuadWordPeripheral, IKnownSize
    {
        public NEORV32_MachineSystemTimer(IMachine machine, long frequency)
        {
            mTimer = new ComparingTimer(machine.ClockSource, frequency, this, nameof(mTimer), direction: Time.Direction.Ascending, workMode: Time.WorkMode.Periodic, eventEnabled: true, enabled: true);
            mTimer.CompareReached += () => 
            {
                UpdateInterrupts();
            };

            var registersMap = new Dictionary<long, QuadWordRegister>();

            registersMap.Add((long)Registers.Time, new QuadWordRegister(this)
                .WithValueField(0, 64, name: "TIME",
                    valueProviderCallback: _ => TimerValue,
                    writeCallback: (_, value) => mTimer.Value = value)
                .WithWriteCallback((_, __) => UpdateInterrupts()));

            registersMap.Add((long)Registers.Compare, new QuadWordRegister(this)
                .WithValueField(0, 64, name: "COMPARE",
                    valueProviderCallback: _ => mTimer.Compare,
                    writeCallback: (_, value) => mTimer.Compare = value)
                .WithWriteCallback((_, __) => UpdateInterrupts()));
            
            RegistersCollection = new QuadWordRegisterCollection(this, registersMap);
            
            this.machine = machine;
        }

        public void Reset()
        {
            mTimer.Reset();
            UpdateInterrupts();
        }

        public ulong ReadQuadWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteQuadWord(long offset, ulong val)
        {
            RegistersCollection.Write(offset, val);
        }

        public QuadWordRegisterCollection RegistersCollection { get; }
        public GPIO IRQ { get; } = new GPIO();
        public long Size => 0x10;

        private void UpdateInterrupts()
        {
            bool shouldInterrupt = mTimer.Value >= mTimer.Compare;
            this.Log(LogLevel.Noisy, "Setting IRQ: {0}", shouldInterrupt);
            IRQ.Set(shouldInterrupt);
        }

        private ulong TimerValue 
        {
            get
            {
                if(machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu))
                {
                    cpu.SyncTime();
                }
                return mTimer.Value;
            }
        }

        private readonly IMachine machine;

        private readonly ComparingTimer mTimer;

        enum Registers
        {
            Time = 0x00,
            Compare = 0x08,
        }
    }
}


