//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    // This peripheral implements the GPIO from NEORV32 v1.11.6.
    public class NEORV32_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public NEORV32_GPIO(IMachine machine, int numberOfPins = MaxPins) : base(machine, numberOfPins)
        {
            if(numberOfPins < 1 || numberOfPins > MaxPins)
            {
                throw new ConstructionException($"Number of pins has to be in [1; {MaxPins}], got {numberOfPins}");
            }

            IRQ = new GPIO();
            RegistersCollection = new DoubleWordRegisterCollection(this);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
            IRQ.Unset();
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            var previous = State[number];
            base.OnGPIO(number, value);

            if(irqEnable[number].Value && triggerType[number].Value == TriggerType.Edge)
            {
                var edge = triggerPolarity[number].Value == TriggerPolarity.HighRising ? !previous && value : previous && !value;
                irqPending[number].Value |= edge;
            }

            UpdateInterrupts();
        }

        public uint ReadDoubleWord(long offset) => RegistersCollection.Read(offset);

        public void WriteDoubleWord(long offset, uint value) => RegistersCollection.Write(offset, value);

        public GPIO IRQ { get; }

        public long Size => 0x20;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.PortIn.Define(this)
                .WithFlags(0, NumberOfConnections, FieldMode.Read, name: "PORT_IN",
                    valueProviderCallback: (i, _) => State[i])
                .WithReservedBits(NumberOfConnections, MaxPins - NumberOfConnections);

            Registers.PortOut.Define(this)
                .WithFlags(0, NumberOfConnections, name: "PORT_OUT",
                    valueProviderCallback: (i, _) => Connections[i].IsSet,
                    writeCallback: (i, _, value) => Connections[i].Set(value))
                .WithReservedBits(NumberOfConnections, MaxPins - NumberOfConnections);

            Registers.Reserved0.Define(this)
                .WithReservedBits(0, 32);

            Registers.Reserved1.Define(this)
                .WithReservedBits(0, 32);

            Registers.IrqType.Define(this)
                .WithEnumFields<DoubleWordRegister, TriggerType>(0, 1, NumberOfConnections, out triggerType, name: "IRQ_TYPE")
                .WithReservedBits(NumberOfConnections, MaxPins - NumberOfConnections)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.IrqPolarity.Define(this)
                .WithEnumFields<DoubleWordRegister, TriggerPolarity>(0, 1, NumberOfConnections, out triggerPolarity, name: "IRQ_POLARITY")
                .WithReservedBits(NumberOfConnections, MaxPins - NumberOfConnections)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.IrqEnable.Define(this)
                .WithFlags(0, NumberOfConnections, out irqEnable, name: "IRQ_ENABLE")
                .WithReservedBits(NumberOfConnections, MaxPins - NumberOfConnections)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.IrqPending.Define(this)
                .WithFlags(0, NumberOfConnections, out irqPending, FieldMode.Read | FieldMode.WriteZeroToClear, name: "IRQ_PENDING")
                .WithReservedBits(NumberOfConnections, MaxPins - NumberOfConnections)
                .WithWriteCallback((_, __) => UpdateInterrupts());
        }

        private void UpdateInterrupts()
        {
            var irq = false;
            for(var i = 0; i < NumberOfConnections; i++)
            {
                if(!irqEnable[i].Value)
                {
                    irqPending[i].Value = false;
                    continue;
                }

                if(triggerType[i].Value == TriggerType.Level)
                {
                    irqPending[i].Value |= triggerPolarity[i].Value == TriggerPolarity.HighRising ? State[i] : !State[i];
                }
                irq |= irqPending[i].Value;
            }
            IRQ.Set(irq);
        }

        private IEnumRegisterField<TriggerType>[] triggerType;
        private IEnumRegisterField<TriggerPolarity>[] triggerPolarity;
        private IFlagRegisterField[] irqEnable;
        private IFlagRegisterField[] irqPending;

        private const int MaxPins = 32;

        private enum TriggerType
        {
            Level = 0,
            Edge  = 1,
        }

        private enum TriggerPolarity
        {
            LowFalling = 0, // low-level or falling-edge
            HighRising = 1, // high-level or rising-edge
        }

        private enum Registers : long
        {
            PortIn      = 0x00,
            PortOut     = 0x04,
            Reserved0   = 0x08,
            Reserved1   = 0x0C,
            IrqType     = 0x10,
            IrqPolarity = 0x14,
            IrqEnable   = 0x18,
            IrqPending  = 0x1C,
        }
    }
}
