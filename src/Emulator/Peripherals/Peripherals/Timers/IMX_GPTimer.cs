//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class IMX_GPTimer : BasicDoubleWordPeripheral, IKnownSize
    {
        public IMX_GPTimer(IMachine machine, int frequency = DefaultFrequency) : base(machine)
        {
            timers = new ComparingTimer[NumberOfCaptures];
            capturesPending = new IFlagRegisterField[NumberOfCaptures];
            capturesEnabled = new IFlagRegisterField[NumberOfCaptures];
            for(int i = 0; i < NumberOfCaptures; i++)
            {
                var j = i;
                timers[j] = new ComparingTimer(machine.ClockSource, frequency, this, $"compare{j}", eventEnabled: true);
                timers[j].CompareReached += () =>
                {
                    this.Log(LogLevel.Noisy, "Compare reached on Compare {0}. Compare Value {1}", j, timers[j].Compare);
                    capturesPending[j].Value = true;
                    UpdateInterrupts();
                };
            }

            DefineRegisters();
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, name: "EN", writeCallback: (_, value) => 
                    { 
                        foreach(var timer in timers)
                        {
                           timer.Enabled = value; 
                        }
                    }
                )
                .WithTag("ENMOD", 1, 1)
                .WithTag("DBGEN", 2, 1)
                .WithTag("WAITEN", 3, 1)
                .WithTag("DOZEEN", 4, 1)
                .WithTag("STOPEN", 5, 1)
                .WithTag("CLKSRC", 6, 3)
                .WithFlag(9, out freeRunModeEnabled, name: "FRR")
                .WithReservedBits(10, 5)
                .WithFlag(15, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        Reset();
                    }
                }, name: "SWR")
                .WithTag("IM1", 16, 2)
                .WithTag("IM2", 18, 2)
                .WithTag("OM1", 20, 3)
                .WithTag("OM2", 23, 3)
                .WithTag("OM3", 26, 3)
                .WithTaggedFlag("FO1", 29)
                .WithTaggedFlag("FO2", 30)
                .WithTaggedFlag("FO3", 31)
            ;
            
            Registers.Prescaler.Define(this)
                .WithTag("PRESCALER", 0, 12)
                .WithReservedBits(12, 20)
            ;

            Registers.Status.Define(this)
                .WithFlags(0, NumberOfCaptures, out capturesPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "OFn",
                    writeCallback: (_,__,___) => UpdateInterrupts()) 
                .WithFlag(3, FieldMode.Read | FieldMode.WriteOneToClear, name: "IF1")
                .WithFlag(4, FieldMode.Read | FieldMode.WriteOneToClear, name: "IF2")
                .WithFlag(5, FieldMode.Read | FieldMode.WriteOneToClear, name: "ROV")
                .WithReservedBits(6, 26)
            ;

            Registers.Interrupt.Define(this)
                .WithFlags(0, NumberOfCaptures, out capturesEnabled, name: "OFnIE",
                    writeCallback: (_,__,___) => UpdateInterrupts()) 
                .WithTaggedFlag("IF1IE", 3)
                .WithTaggedFlag("IF2IE", 4)
                .WithTaggedFlag("ROVIE", 5)
                .WithReservedBits(6, 26)
            ;

            Registers.OutputCompare1.DefineMany(this, NumberOfCaptures, setup: (register, idx) =>
            {
                register
                    .WithValueField(0, 32, name: $"COMP{idx+1}", writeCallback: (_, value) =>
                        {
                            timers[idx].Compare = value;
                            // According to documentation, when timer is in Reset mode (FRR flag is set to 0),
                            // then counter value should be set to 0 after every write operation on output capture register 1 (i.e., idx == 0 in this model).
                            // We use separate timers for output channels, so it's necessary to reset all timers. 
                            if(idx == 0 && freeRunModeEnabled.Value == false)
                            {
                                foreach(var timer in timers)
                                {
                                    timer.Value = 0;
                                }
                            }
                        },
                        valueProviderCallback: _ => (uint)timers[idx].Compare);
            });

            Registers.InputCapture1.Define(this)
                .WithTag("CAPT", 0, 32)
            ;

            Registers.InputCapture2.Define(this)
                .WithTag("CAPT", 0, 32)
            ;

            Registers.Count.Define(this)
                .WithValueField(0, 32, FieldMode.Read,  name: "COUNT", valueProviderCallback: _ => (uint)timers[0].Value)
            ;
        }

        public override void Reset()
        {
            base.Reset();
            UpdateInterrupts();
        }

        public long Size => 0x1000;
        
        public void UpdateInterrupts()
        {
            var status = false;
            for(int i = 0; i < NumberOfCaptures; i++)
            {
                status |= capturesEnabled[i].Value && capturesPending[i].Value;
            }
            IRQ.Set(status);
        }

        public GPIO IRQ { get; } = new GPIO();

        private IFlagRegisterField freeRunModeEnabled;
        private IFlagRegisterField[] capturesEnabled;
        private IFlagRegisterField[] capturesPending;
        private readonly ComparingTimer[] timers;
        private const int NumberOfCaptures = 3;
        private const int DefaultFrequency = 32000;

        private enum Registers : long
        {
            Control = 0x00,
            Prescaler = 0x04,
            Status = 0x08,
            Interrupt = 0x0c,
            OutputCompare1 = 0x10,
            OutputCompare2 = 0x14,
            OutputCompare3 = 0x18,
            InputCapture1 = 0x1c,
            InputCapture2 = 0x20,
            Count = 0x24,
        }
    }
}
