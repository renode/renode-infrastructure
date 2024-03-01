//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class S32K3XX_SystemTimerModule : LimitTimer, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public S32K3XX_SystemTimerModule(IMachine machine, uint clockFrequency) : base(machine.ClockSource, clockFrequency, uint.MaxValue)
        {
            IRQ = new GPIO();

            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();

            channelTimer = new LimitTimer[ChannelCount];
            for(var i = 0; i < ChannelCount; ++i)
            {
                var j = i;
                channelTimer[i] = new LimitTimer(machine.ClockSource, clockFrequency, this, $"channel#{j}", workMode: WorkMode.OneShot);
                channelTimer[i].LimitReached += UpdateInterrupts;
            }

            LimitReached += () =>
            {
                for(var i = 0; i < ChannelCount; ++i)
                {
                    channelTimer[i].Value = channelTimer[i].Limit;
                    channelTimer[i].Enabled = channelTimer[i].EventEnabled;
                }
            };
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();

            for(var i = 0; i < ChannelCount; ++i)
            {
                channelTimer[i].Reset();
            }
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; }
        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void UpdateInterrupts()
        {
            var interrupt = channelTimer.Any(timer => timer.RawInterrupt);
            IRQ.Set(interrupt);
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, name: "TimerEnable",
                    valueProviderCallback: _ => Enabled,
                    writeCallback: (_, value) => Enabled = value)
                .WithTaggedFlag("Freeze", 1)
                .WithReservedBits(2, 6)
                .WithValueField(8, 8, name: "CounterPrescaler",
                    valueProviderCallback: _ => (uint)(Divider - 1),
                    writeCallback: (_, value) =>
                    {
                        Divider = (int)value + 1;
                        for(var i = 0; i < ChannelCount; ++i)
                        {
                            channelTimer[i].Divider = Divider;
                        }
                    })
                .WithReservedBits(16, 16);
            ;

            Registers.Count.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "TimerCount",
                    valueProviderCallback: _ => Value)
            ;

            var channelSize = (uint)(Registers.ChannelControl1 - Registers.ChannelControl0);
            Registers.ChannelControl0.DefineMany(this, ChannelCount, (register, index) =>
            {
                register
                    .WithFlag(0, name: "ChannelEnable",
                        valueProviderCallback: _ => channelTimer[index].EventEnabled,
                        changeCallback: (_, value) =>
                        {
                            channelTimer[index].EventEnabled = value;
                            if(!value)
                            {
                                return;
                            }

                            if(channelTimer[index].Limit > Value)
                            {
                                channelTimer[index].Value = Value;
                                channelTimer[index].Enabled = true;
                            }
                            else
                            {
                                this.Log(LogLevel.Debug, "Channel#{0} has been enabled, but it will be started after counter underflows.");
                            }
                        })
                    .WithReservedBits(1, 31)
                ;
            }, stepInBytes: channelSize);

            Registers.ChannelInterrupt0.DefineMany(this, ChannelCount, (register, index) =>
            {
                register
                    .WithFlag(0, name: "ChannelInterruptFlag",
                        valueProviderCallback: _ => channelTimer[index].RawInterrupt,
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                channelTimer[index].ClearInterrupt();
                                UpdateInterrupts();
                            }
                        })
                    .WithReservedBits(1, 31)
                ;
            }, stepInBytes: channelSize);

            Registers.ChannelCompare0.DefineMany(this, ChannelCount, (register, index) =>
            {
                register
                    .WithValueField(0, 32, name: "ChannelCompare",
                        valueProviderCallback: _ => channelTimer[index].Limit,
                        writeCallback: (_, value) => channelTimer[index].Limit = value)
                ;
            }, stepInBytes: channelSize);
        }

        private const uint ChannelCount = 4;

        private readonly LimitTimer[] channelTimer;

        private enum Registers
        {
            Control = 0x0, // CR
            Count = 0x4, // CNT
            ChannelControl0 = 0x10, // CCR0
            ChannelInterrupt0 = 0x14, // CIR0
            ChannelCompare0 = 0x18, // CMP0
            ChannelControl1 = 0x20, // CCR1
            ChannelInterrupt1 = 0x24, // CIR1
            ChannelCompare1 = 0x28, // CMP1
            ChannelControl2 = 0x30, // CCR2
            ChannelInterrupt2 = 0x34, // CIR2
            ChannelCompare2 = 0x38, // CMP2
            ChannelControl3 = 0x40, // CCR3
            ChannelInterrupt3 = 0x44, // CIR3
            ChannelCompare3 = 0x48 // CMP3
        }
    }
}
