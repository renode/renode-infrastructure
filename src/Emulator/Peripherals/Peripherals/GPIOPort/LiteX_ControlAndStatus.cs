//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class LiteX_ControlAndStatus : BaseGPIOPort, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public LiteX_ControlAndStatus(IMachine machine) : base(machine, LedsCount + SwitchesCount + ButtonsCount)
        {
            buttonsPending = new bool[ButtonsCount];
            IRQ = new GPIO();

            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
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
            Array.Clear(buttonsPending, 0, buttonsPending.Length);

            UpdateInterrupt();
        }

        public override void OnGPIO(int number, bool value)
        {
            if(number >= 0 && number < LedsCount)
            {
                this.Log(LogLevel.Warning, "Input not allowed on LED ports");
                return;
            }

            base.OnGPIO(number, value);

            // here we are interested only in buttons as their state is latached and generates interrupt
            if(number >= LedsCount + SwitchesCount)
            {
                var buttonId = number - (LedsCount + SwitchesCount);
                buttonsPending[buttonId] |= value;

                UpdateInterrupt();
            }
        }

        public GPIO IRQ { get; }

        public long Size => 0x14;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            // according to LiteX-generated `csr.h` this register is readable
            Registers.Leds.Define(this)
                .WithValueField(0, LedsCount,
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(Connections.Where(x => x.Key >= 0).OrderBy(x => x.Key).Select(x => x.Value.IsSet)),
                    writeCallback: (_, val) =>
                    {
                        var bits = BitHelper.GetBits((uint)val);
                        for(var i = 0; i < LedsCount; i++)
                        {
                            Connections[i].Set(bits[i]);
                        }
                    })
            ;

            Registers.Switches.Define(this)
                .WithValueField(0, SwitchesCount, FieldMode.Read,
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(this.State.Skip(LedsCount).Take(SwitchesCount)))
            ;

            Registers.ButtonsStatus.Define(this)
                .WithValueField(0, ButtonsCount, FieldMode.Read,
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(this.State.Skip(LedsCount + SwitchesCount).Take(ButtonsCount)))
            ;

            Registers.ButtonsPending.Define(this)
                .WithValueField(0, ButtonsCount,
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(buttonsPending),
                    writeCallback: (_, val) =>
                    {
                        foreach(var bit in BitHelper.GetSetBits(val))
                        {
                            buttonsPending[bit] = false;
                        }

                        UpdateInterrupt();
                    })
            ;

            Registers.ButtonsEnabled.Define(this)
                .WithValueField(0, ButtonsCount, out buttonsEnabled, writeCallback: (_, __) => { UpdateInterrupt(); })
            ;
        }

        private void UpdateInterrupt()
        {
            var enabled = BitHelper.GetBits((uint)buttonsEnabled.Value).Take(ButtonsCount);
            IRQ.Set(enabled.Zip(buttonsPending, (en, pe) => en && pe).Any());
        }

        private IValueRegisterField buttonsEnabled;

        private readonly bool[] buttonsPending;

        private const int LedsCount = 32;
        private const int SwitchesCount = 32;
        private const int ButtonsCount = 32;

        private enum Registers
        {
            Leds = 0x0,
            Switches = 0x4,
            ButtonsStatus = 0x8,
            ButtonsPending = 0xC,
            ButtonsEnabled = 0x10,
        }
    }
}
