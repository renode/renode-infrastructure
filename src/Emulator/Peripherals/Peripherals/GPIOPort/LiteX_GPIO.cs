//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class LiteX_GPIO : BaseGPIOPort, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public LiteX_GPIO(IMachine machine, Type type, bool enableIrq = false) : base(machine, NumberOfPins)
        {
            this.type = type;
            this.enableIrq = enableIrq;

            if(type == Type.Out && enableIrq)
            {
                throw new ConstructionException("Out GPIO does not support interrupts");
            }

            IRQ = new GPIO();

            previousState = new bool[NumberOfPins];

            RegistersCollection = new DoubleWordRegisterCollection(this);
            Size = DefineRegisters();
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

            for(var i = 0; i < previousState.Length; i++)
            {
                previousState[i] = false;
            }

            IRQ.Unset();
        }

        public GPIO IRQ { get; }

        public long Size { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            base.OnGPIO(number, value);

            if(!enableIrq)
            {
                // irq support is not enabled
                return;
            }

            if(State[number] == previousState[number])
            {
                // nothing to do
                return;
            }

            previousState[number] = State[number];
            
            if(irqMode[number].Value == IrqMode.Edge)
            {
                if(irqEdge[number].Value == IrqEdge.Rising)
                {
                    if(State[number])
                    {
                        irqPending[number].Value = true;
                        UpdateInterrupts();
                    }
                }
                else // it must be Falling
                {
                    if(!State[number])
                    {
                        irqPending[number].Value = true;
                        UpdateInterrupts();
                    }
                }
            }
            else // it must be Change
            {
                irqPending[number].Value = true;
                UpdateInterrupts();
            }
        }

        private int DefineRegisters()
        {
            var offset = 0;
            if(type != Type.Out)
            {
                offset += DefineInRegisters(offset);
            }

            if(type != Type.In)
            {
                offset += DefineOutRegisters(offset);
            }

            return offset;
        }

        private int DefineInRegisters(long offset)
        {
            var size = 0;

            ((Registers)offset).Define(this)
                .WithValueField(0, NumberOfPins, FieldMode.Read, name: "In",
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(State));
            size += 0x4;

            if(enableIrq)
            {
                ((Registers)offset + 0x4).Define(this)
                    .WithEnumFields<DoubleWordRegister, IrqMode>(0, 1, NumberOfPins, out irqMode, name: "IRQ mode");
                size += 0x4;

                ((Registers)offset + 0x8).Define(this)
                    .WithEnumFields<DoubleWordRegister, IrqEdge>(0, 1, NumberOfPins, out irqEdge, name: "IRQ edge");
                size += 0x4;

                // status - return 0s for now
                ((Registers)offset + 0xc).Define(this)
                    .WithFlags(0, NumberOfPins, FieldMode.Read, name: "IRQ status");
                size += 0x4;

                ((Registers)offset + 0x10).Define(this)
                    .WithFlags(0, NumberOfPins, out irqPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "IRQ pending")
                    .WithWriteCallback((_, __) => UpdateInterrupts());
                size += 0x4;

                ((Registers)offset + 0x14).Define(this)
                    .WithFlags(0, NumberOfPins, out irqEnable, name: "IRQ enable")
                    .WithWriteCallback((_, __) => UpdateInterrupts());
                size += 0x4;
            }

            return size;
        }

        private int DefineOutRegisters(long offset)
        {
            var size = 0;

            ((Registers)offset).Define(this)
                .WithValueField(0, NumberOfPins, name: "Out",
                    writeCallback: (_, val) =>
                    {
                        var bits = BitHelper.GetBits((uint)val);
                        for(var i = 0; i < bits.Length; i++)
                        {
                            Connections[i].Set(bits[i]);
                        }
                    },
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(Connections.Where(x => x.Key >= 0).OrderBy(x => x.Key).Select(x => x.Value.IsSet)));
            size += 0x4;

            return size;
        }

        private void UpdateInterrupts()
        {
            var flag = false;
            for(var i = 0; i < NumberOfPins; i++)
            {
                flag |= irqEnable[i].Value && irqPending[i].Value;
            }

            if(IRQ.IsSet != flag)
            {
                this.Log(LogLevel.Debug, "Setting IRQ to {0}", flag);
            }
            IRQ.Set(flag);
        }

        private readonly Type type;
        private readonly bool enableIrq;

        private IEnumRegisterField<IrqEdge>[] irqEdge;
        private IEnumRegisterField<IrqMode>[] irqMode;

        private IFlagRegisterField[] irqPending;
        private IFlagRegisterField[] irqEnable;

        private readonly bool[] previousState;

        private const int NumberOfPins = 32;

        public enum Type
        {
            In,
            Out,
            InOut
        }

        private enum IrqMode
        {
            Edge = 0,
            Change = 1
        }

        private enum IrqEdge
        {
            Rising = 0,
            Falling = 1
        }

        private enum Registers
        {
            Register1 = 0x0,
            Register2 = 0x4,
            Register3 = 0x8,
            Register4 = 0xc,
            Register5 = 0x10,
            Register6 = 0x14,
            Register7 = 0x18,
            // the actual layout of registers
            // depends on the model configuration
        }
    }
}

