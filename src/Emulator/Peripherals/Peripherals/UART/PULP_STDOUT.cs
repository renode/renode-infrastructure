//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    public class PULP_STDOUT : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public PULP_STDOUT(IMachine machine) : base(machine)
        {
            CreateRegisters();
        }
        
        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }
            
        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
        }

        public long Size => 0x4;

        // None of those properties are used, we need them only to keep the compiler happy
        public override Bits StopBits => Bits.One;
        public override Parity ParityBit => Parity.None;
        public override uint BaudRate => 0;
        
        protected override void CharWritten()
        {
            // intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private void CreateRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.StandardOutput, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Write, writeCallback: (_, val) => this.TransmitCharacter((byte)val), name: "STD_OUT")
                    .WithReservedBits(16, 16)
                },
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        private DoubleWordRegisterCollection registers;

        private enum Registers : long
        {
            StandardOutput = 0,
        }
    }
}
