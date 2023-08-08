//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

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
    public class PicoRV_SPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public PicoRV_SPI(IMachine machine) : base(machine)
        {
            bbHelper = new BitBangHelper(8, loggingParent: this, outputMsbFirst: true);

            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Config1, new DoubleWordRegister(this)
                    .WithFlag(0, out var data0, FieldMode.Write, name: "data0")
                    .WithFlag(1, FieldMode.Write, name: "data1") // data 1-3 used in QSPI only
                    .WithFlag(2, FieldMode.Write, name: "data2")
                    .WithFlag(3, FieldMode.Write, name: "data3")
                    .WithFlag(4, out var clock, FieldMode.Write, name: "clk")
                    .WithFlag(5, out var chipSelectNegated, FieldMode.Write, name: "cs_n")
                    .WithReservedBits(6, 2)

                    .WithWriteCallback((_, val) =>
                    {
                        if(memioEnable.Value)
                        {
                            this.Log(LogLevel.Warning, "MEMIO mode not enabled - bit banging not supported in this mode");
                            return;
                        }

                        if(qspiEnable.Value)
                        {
                            this.Log(LogLevel.Warning, "QSPI mode not yet supported");
                            return;
                        }

                        if(RegisteredPeripheral == null)
                        {
                            this.Log(LogLevel.Warning, "Trying to send bytes over SPI, but there is no device attached");
                            return;
                        }

                        if(chipSelectNegated.Value && !previousChipSelectNegated)
                        {
                            this.Log(LogLevel.Noisy, "ChipSelect signal down - finishing the transmission");
                            RegisteredPeripheral.FinishTransmission();
                            bbHelper.ResetOutput();
                        }
                        previousChipSelectNegated = chipSelectNegated.Value;

                        // do not latch bits when MEMIO is enabled or chipSelect is not set
                        if(bbHelper.Update(clock.Value, data0.Value, !memioEnable.Value && !chipSelectNegated.Value))
                        {
                            this.Log(LogLevel.Noisy, "Sending byte 0x{0:X}", bbHelper.DecodedOutput);
                            var input = RegisteredPeripheral.Transmit((byte)bbHelper.DecodedOutput);
                            this.Log(LogLevel.Noisy, "Received byte 0x{0:X}", input);

                            bbHelper.SetInputBuffer(input);
                        }
                    })
                },

                {(long)Registers.Config2, new DoubleWordRegister(this)
                    // those are not handled, but implemented to avoid warnings in logs
                    .WithFlag(0, name: "data0_output_en")
                    .WithFlag(1, name: "data1_output_en")
                    .WithFlag(2, name: "data2_output_en")
                    .WithFlag(3, name: "data3_output_en")
                    .WithFlag(4, name: "clk_output_en")
                    .WithFlag(5, name: "cs_output_en")
                    .WithReservedBits(6, 2)
                },

                {(long)Registers.Config3, new DoubleWordRegister(this)
                    .WithTag("read latency (dummy) cycles", 0, 4)
                    .WithTag("CRM enable", 4, 1)
                    .WithFlag(5, out qspiEnable, FieldMode.Write, name: "qspi_en")
                    .WithTag("DDR enable", 6, 1)
                    .WithReservedBits(7, 1)
                },

                {(long)Registers.Config4, new DoubleWordRegister(this)
                    .WithReservedBits(0, 7)
                    .WithFlag(7, out memioEnable, name: "memio_en")
                },

                {(long)Registers.Stat1, new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithFlag(1, FieldMode.Read, name: "MISO", valueProviderCallback: _ => bbHelper.EncodedInput)
                    .WithReservedBits(2, 6)
                }
            };

            registersCollection = new DoubleWordRegisterCollection(this, registers);
            Reset();
        }

        public override void Reset()
        {
            bbHelper.Reset();
            registersCollection.Reset();
            previousChipSelectNegated = true;
        }

        public uint ReadDoubleWord(long offset)
        {
            return registersCollection.Read(offset);
        }

        [ConnectionRegionAttribute("xip")]
        public uint XipReadDoubleWord(long offset)
        {
            if(!memioEnable.Value)
            {
                this.Log(LogLevel.Warning, "Trying to read from XIP region at offset 0x{0:X}, but memio is disabled", offset);
                return 0;
            }

            return (RegisteredPeripheral as IDoubleWordPeripheral)?.ReadDoubleWord(offset) ?? 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registersCollection.Write(offset, value);
        }

        [ConnectionRegionAttribute("xip")]
        public void XipWriteDoubleWord(long offset, uint value)
        {
            this.Log(LogLevel.Warning, "Trying to write 0x{0:X} to XIP region at offset 0x{1:x}. Direct writing is not supported", value, offset);
        }

        public long Size => 0x20;

        private bool previousChipSelectNegated;

        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly BitBangHelper bbHelper;
        private readonly IFlagRegisterField memioEnable;
        private readonly IFlagRegisterField qspiEnable;

        private enum Registers
        {
            Config1 = 0x0,
            Config2 = 0x4,
            Config3 = 0x8,
            Config4 = 0xC,
            Stat1 = 0x10,
            Stat2 = 0x14,
            Stat3 = 0x18,
            Stat4 = 0x1c,
        }
    }
}
