//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class LiteX_SPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public LiteX_SPI(Machine machine, int dataWidth = 8) : base(machine)
        {
            if(dataWidth <= 0 || dataWidth > 8)
            {
                throw new ConstructionException("Data width out of range (0, 8>: {0}".FormatWith(dataWidth));
            }

            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, out var startBit, name: "start")
                    .WithValueField(8, 8, out var lengthField, name: "length")
                    .WithWriteCallback((_, val) =>
                    {
                        if(!startBit.Value || lengthField.Value == 0)
                        {
                            return;
                        }

                        if(loopbackMode.Value)
                        {
                            this.Log(LogLevel.Noisy, "Sending data in loopback mode");
                            masterInBuffer.Value = masterOutBuffer.Value;
                        }
                        else
                        {
                            if(RegisteredPeripheral == null)
                            {
                                this.Log(LogLevel.Warning, "Tried to send some data over SPI, but no device is currently connected");
                                return;
                            }

                            var result = RegisteredPeripheral.Transmit((byte)BitHelper.GetMaskedValue(masterOutBuffer.Value, 0, dataWidth));
                            masterInBuffer.Value = BitHelper.GetMaskedValue(result, 0, dataWidth);
                        }
                    })
                },

                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "done")
                },

                {(long)Registers.MasterOutSlaveIn, new DoubleWordRegister(this)
                    .WithValueField(0, dataWidth, out masterOutBuffer)
                },

                {(long)Registers.MasterInSlaveOut, new DoubleWordRegister(this)
                    .WithValueField(0, dataWidth, out masterInBuffer)
                },

                {(long)Registers.Loopback, new DoubleWordRegister(this)
                    .WithFlag(0, out loopbackMode)
                }
            };

            registersCollection = new DoubleWordRegisterCollection(this, registers);
            Reset();
        }

        public override void Reset()
        {
            registersCollection.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registersCollection.Write(offset, value);
        }

        public long Size => 0x50;

        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly IValueRegisterField masterOutBuffer;
        private readonly IValueRegisterField masterInBuffer;
        private readonly IFlagRegisterField loopbackMode;

        private enum Registers
        {
            Control = 0x0,
            Status = 0x8,
            MasterOutSlaveIn = 0xC,
            MasterInSlaveOut = 0x10,
            ChipSelect = 0x14,
            Loopback = 0x18
        }
    }
}
