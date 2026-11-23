
//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Analog
{

    public class STM32_DAC :
        NullRegistrationPointPeripheralContainer<IVoltageSink>,
        IDoubleWordPeripheral,
        IWordPeripheral,
        IBytePeripheral,
        IKnownSize,
        IGPIOReceiver
    {
        
        public GPIO DMAReceive { get; }
        public STM32_DAC(IMachine machine) : base(machine)
        {
            registers = new DoubleWordRegisterCollection(this);
            DMAReceive = new GPIO();
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            registers.Reset();
            UpdateOutput();
        }

        public void OnGPIO(int number, bool value)
        {
            if (!lastValue && value)
            {
                DMAReceive.Blink();
            }

            lastValue = value;   
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)registers.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            registers.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }
        
        public byte ReadByte(long offset) {
            return (byte)registers.Read(offset);
        }
        
        public void WriteByte(long offset, byte value) {
            registers.Write(offset, value);
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            // 12-bit right aligned
            registers.DefineRegister(0x08)
                .WithValueField(
                    0,
                    12,
                    out dacValue,
                    name: "DHR12R1"
                )
                .WithWriteCallback((_, __) =>
                {
                    this.Log(LogLevel.Info, "WriteCallback");
                    UpdateOutput();
                });
            
            // 8-bit right aligned
            registers.DefineRegister(0x10)
                .WithValueField(
                    0,
                    8,
                    out dacValue,
                    name: "DHR8R1"
                )
                .WithWriteCallback((_, __) =>
                {
                    UpdateOutput();
                });

            // CR
            registers.DefineRegister(0x00)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0);

            // SR
            registers.DefineRegister(0x34)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0);
        }

        private void UpdateOutput()
        {
            var voltage = 3.3 * dacValue.Value / 256.0;
            RegisteredPeripheral?.SetVoltage(voltage);
        }

        private readonly DoubleWordRegisterCollection registers;

        private IValueRegisterField dacValue;

        private bool lastValue = false;
    }
}
