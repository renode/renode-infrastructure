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

namespace Antmicro.Renode.Peripherals.SPI
{
    public class LiteX_SPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public LiteX_SPI(IMachine machine) : base(machine)
        {
            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out bitsPerWord, name: "bits per word", changeCallback: (_, __) => VerifyBitsPerWord())
                    // the driver do not limit written value to a single byte, so without ignoring the following bits a warning would be generated
                    .WithIgnoredBits(8, 8)
                    .WithReservedBits(16, 16)
                },

                // see comment above
                {(long)Registers.Control2, new DoubleWordRegister(this)
                    .WithFlag(0, name: "start", writeCallback: (_, startBit) =>
                    {
                        if(!startBit)
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
                            if(!chipSelect.Value)
                            {
                                this.Log(LogLevel.Warning, "Tried to send some data over SPI, but CS is not set");
                                masterInBuffer.Value = NoResponse;
                                return;
                            }

                            if(RegisteredPeripheral == null)
                            {
                                this.Log(LogLevel.Warning, "Tried to send some data over SPI, but no device is currently connected");
                                masterInBuffer.Value = NoResponse;
                                return;
                            }

                            if(bitsPerWord.Value == 0)
                            {
                                this.Log(LogLevel.Warning, "Transfer length set to 0. Ignoring the transfer");
                                masterInBuffer.Value = NoResponse;
                                return;
                            }

                            // see method's comment for details
                            if(!VerifyBitsPerWord())
                            {
                                masterInBuffer.Value = NoResponse;
                                return;
                            }

                            var result = RegisteredPeripheral.Transmit((byte)masterOutBuffer.Value);
                            masterInBuffer.Value = (byte)result;
                        }
                    })
                    // the driver do not limit written value to a single byte, so without ignoring the following bits a warning would be generated
                    .WithIgnoredBits(1, 15)
                    .WithReservedBits(16, 16)
                },

                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "done")
                    .WithReservedBits(1, 31)
                },

                {(long)Registers.MasterOutSlaveIn, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out masterOutBuffer)
                    // the driver do not limit written value to a single byte, so without ignoring the following bits a warning would be generated
                    .WithIgnoredBits(8, 24)
                },

                {(long)Registers.MasterInSlaveOut, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out masterInBuffer)
                    // the driver do not limit written value to a single byte, so without ignoring the following bits a warning would be generated
                    .WithIgnoredBits(8, 24)
                },

                {(long)Registers.Loopback, new DoubleWordRegister(this)
                    .WithFlag(0, out loopbackMode)
                    .WithReservedBits(1, 31)
                },

                {(long)Registers.ChipSelect, new DoubleWordRegister(this)
                    // for now we support only one device
                    // this should be improved to support more devices
                    .WithFlag(0, out chipSelect, name: "cs", writeCallback: (_, val) =>
                    {
                        if(val && RegisteredPeripheral == null)
                        {
                            this.Log(LogLevel.Warning, "CS set, but no device is currently connected");
                        }
                    })
                    .WithReservedBits(1, 31)
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

        // This is due to limitations in Renode:
        // proper handling of other values of BPR would require support for bit-by-bit transmission in SPI.
        // Remove once the SPI interface is reworked.
        private bool VerifyBitsPerWord()
        {
            if(bitsPerWord.Value != 8)
            {
                this.Log(LogLevel.Warning, "Bits per word set to: {0}. This configuration is not supported by this model - ignoring the transfer", bitsPerWord.Value);
                return false;
            }

            return true;
        }

        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly IValueRegisterField masterOutBuffer;
        private readonly IValueRegisterField masterInBuffer;
        private readonly IFlagRegisterField loopbackMode;
        private readonly IFlagRegisterField chipSelect;
        private readonly IValueRegisterField bitsPerWord;

        private const byte NoResponse = 0xFF;

        private enum Registers
        {
            // in LiteX this is defined as one CSRStorage(16)
            Control = 0x0,
            Control2 = 0x4,

            Status = 0x8,
            MasterOutSlaveIn = 0xC,
            MasterInSlaveOut = 0x10,
            ChipSelect = 0x14,
            Loopback = 0x18
        }
    }
}
