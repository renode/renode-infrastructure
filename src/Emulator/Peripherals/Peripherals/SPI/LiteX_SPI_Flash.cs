//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
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
        public LiteX_SPI_Flash(Machine machine) : base(machine)
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
                },

                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "busy flag 0") // not busy
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "busy flag 1") // not busy
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "busy flag 2") // not busy
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "busy flag 3") // not busy
                    .WithReservedBits(4, 28)
                },

                {(long)Registers.InLength, new DoubleWordRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, val) =>
                    {
                        if(bitBangEnabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Write to non-bitbang register ignored");
                            return;
                        }

                        if(val > 0)
                        {
                            DoQuadTransfer((int)val);
                        }
                    })
                    .WithReservedBits(8, 24)
                },

                {(long)Registers.OutLength, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out outLength)
                    .WithReservedBits(8, 24)
                },
            };

            for(var i = 0; i < BufferSize; i++)
            {
                var j = i;

                registers[(long)Registers.SpiIn + 4*i] = new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, val) =>
                    {
                        hostToDeviceBuffer[j] = (byte)val;
                    })
                    .WithReservedBits(8, 24);

                registers[(long)Registers.SpiOut + 4*i] =  new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => deviceToHostBuffer[j])
                    .WithReservedBits(8, 24);
            }

            registersCollection = new DoubleWordRegisterCollection(this, registers);
            Reset();
        }

        public override void Reset()
        {
            bbHelper.Reset();
            registersCollection.Reset();
            previousChipSelectNegatedSignal = true;

            Array.Clear(hostToDeviceBuffer, 0, hostToDeviceBuffer.Length);
            Array.Clear(deviceToHostBuffer, 0, deviceToHostBuffer.Length);
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

        public long Size => 0x800;

        private void DoQuadTransfer(int length)
        {
            if(length > hostToDeviceBuffer.Length)
            {
                this.Log(LogLevel.Warning, "There was less bytes in the queue ({0}) than expected ({1})", hostToDeviceBuffer.Length, length);
            }

            foreach(var b in hostToDeviceBuffer.Take(length))
            {
                RegisteredPeripheral.Transmit(b);
            }

            var responseBytesLeft = (int)outLength.Value;
            if(responseBytesLeft > deviceToHostBuffer.Length)
            {
                this.Log(LogLevel.Warning, "Device reponse (of length {0} bytes) will not fit in the internal buffer (of size {1} bytes). Some data will be lost!", responseBytesLeft, deviceToHostBuffer.Length);
                responseBytesLeft = deviceToHostBuffer.Length;
            }

            for(var i = 0; i < responseBytesLeft; i++)
            {
                var response = RegisteredPeripheral.Transmit(0);
                deviceToHostBuffer[i] = response;
            }

            RegisteredPeripheral.FinishTransmission();
        }

        private bool previousChipSelectNegatedSignal;

        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly IFlagRegisterField bitBangEnabled;
        private readonly BitBangHelper bbHelper;
        private readonly IValueRegisterField outLength;

        private readonly byte[] hostToDeviceBuffer = new byte[BufferSize];
        private readonly byte[] deviceToHostBuffer = new byte[BufferSize];

        private const int BufferSize = 8;

        private enum Registers
        {
            BitBang = 0x0,
            Miso = 0x4,
            BitBangEnable = 0x8,

            Status = 0xc,
            InLength = 0x10,
            OutLength = 0x14,
            SpiIn = 0x18,
            SpiOut = 0x38
        }
    }
}
