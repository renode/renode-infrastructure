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
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class LiteX_SPI_Flash : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public LiteX_SPI_Flash(IMachine machine) : base(machine)
        {
            bbHelper = new BitBangHelper(8, loggingParent: this, outputMsbFirst: true);

            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.BitBang, new DoubleWordRegister(this)
                    .WithFlag(0, out var valueSignal, FieldMode.Write, name: "value")
                    .WithFlag(1, out var clockSignal, FieldMode.Write, name: "clk")
                    .WithFlag(2, out var chipSelectNegatedSignal, FieldMode.Write, name: "cs_n")
                    .WithFlag(3, out var dqInputSignal, FieldMode.Write, name: "dq_input")
                    .WithWriteCallback((_, val) =>
                    {
                        if(!bitBangEnabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Write to not-enabled BitBang register ignored");
                            return;
                        }

                        if(RegisteredPeripheral == null)
                        {
                            this.Log(LogLevel.Warning, "Trying to send bytes over SPI, but there is no device attached");
                            return;
                        }

                        if(chipSelectNegatedSignal.Value && !previousChipSelectNegatedSignal)
                        {
                            this.Log(LogLevel.Noisy, "ChipSelect signal down - finishing the transmission");
                            RegisteredPeripheral.FinishTransmission();
                        }
                        previousChipSelectNegatedSignal = chipSelectNegatedSignal.Value;

                        // do not latch bits when dqInputSignal is set or chipSelect is not set
                        if(bbHelper.Update(clockSignal.Value, valueSignal.Value, !dqInputSignal.Value && !chipSelectNegatedSignal.Value))
                        {
                            this.Log(LogLevel.Noisy, "Sending byte 0x{0:X}", bbHelper.DecodedOutput);
                            var input = RegisteredPeripheral.Transmit((byte)bbHelper.DecodedOutput);
                            this.Log(LogLevel.Noisy, "Received byte 0x{0:X}", input);

                            bbHelper.SetInputBuffer(input);
                        }
                    })
                },

                {(long)Registers.Miso, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => bbHelper.EncodedInput)
                },

                {(long)Registers.BitBangEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out bitBangEnabled)
                }
            };

            registersCollection = new DoubleWordRegisterCollection(this, registers);
            Reset();
        }

        public override void Reset()
        {
            bbHelper.Reset();
            registersCollection.Reset();
            previousChipSelectNegatedSignal = true;
        }

        public uint ReadDoubleWord(long offset)
        {
            return registersCollection.Read(offset);
        }

        [ConnectionRegionAttribute("xip")]
        public uint XipReadDoubleWord(long offset)
        {
            return (RegisteredPeripheral as IDoubleWordPeripheral)?.ReadDoubleWord(offset) ?? 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registersCollection.Write(offset, value);
        }

        [ConnectionRegionAttribute("xip")]
        public void XipWriteDoubleWord(long offset, uint value)
        {
            (RegisteredPeripheral as IDoubleWordPeripheral)?.WriteDoubleWord(offset, value);
        }

        public long Size => 0x10;

        private bool previousChipSelectNegatedSignal;

        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly IFlagRegisterField bitBangEnabled;
        private readonly BitBangHelper bbHelper;

        private enum Registers
        {
            BitBang = 0x0,
            Miso = 0x4,
            BitBangEnable = 0x8
        }
    }
}
