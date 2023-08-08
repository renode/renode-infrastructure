//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class AmbiqApollo4_Security : BasicDoubleWordPeripheral, IKnownSize
    {
        public AmbiqApollo4_Security(IMachine machine) : base(machine)
        {
            systemBus = machine.GetSystemBus(this);
            DefineRegisters();
        }

        public override void Reset()
        {
            crcErrorOccurred = false;
        }

        public bool LogDataRead { get; set; }
        public long Size => 0x90;

        private void CalculateCrc32()
        {
            var byteLength = (int)wordLength.Value * 4;
            this.Log(LogLevel.Debug, "Calculating CRC32 for the <0x{0:X8},0x{1:X8}> address range; seed: 0x{2:X8}", address.Value, (long)address.Value + byteLength, crcSeedOrResult.Value);

            if(crcSeedOrResult.Value != ValidCrcSeed)
            {
                this.Log(LogLevel.Warning, "The seed is invalid for CRC32 calculation: 0x{0:X8} (should be 0x{1:X8})", crcSeedOrResult.Value, ValidCrcSeed);
                crcErrorOccurred = true;
                return;
            }

            byte[] data;
            try
            {
                data = systemBus.ReadBytes(address.Value, byteLength, onlyMemory: true);
                if(LogDataRead)
                {
                    this.Log(LogLevel.Noisy, "Data for CRC32 calculation:\n{0}", data.Select(b => "0x" + b.ToString("X2")).Stringify(limitPerLine: 8));
                }

                var result = new CRCEngine(0x04C11DB7, 32, init: (uint)crcSeedOrResult.Value).Calculate(data);

                // The most common CRC-32 algorithm (CRC-32/BZIP2) requires inverting the output (xoring with 0xffffffff).
                // See the 'xorout' parameter: https://reveng.sourceforge.io/crc-catalogue/all.htm#crc.cat.crc-32-bzip2
                result = ~result;

                this.Log(LogLevel.Debug, "CRC32 calculation result: 0x{0:X8}", result);
                crcSeedOrResult.Value = result;
            }
            catch(Exceptions.RecoverableException exception)
            {
                this.Log(LogLevel.Debug, "Error when reading memory to calculate CRC32: {0}", exception.Message);
                crcErrorOccurred = true;
            }

            calculationEnabled.Value = false;
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, out calculationEnabled, name: "ENABLE")
                .WithReservedBits(1, 3)
                .WithEnumField(4, 4, out functionSelect, name: "FUNCTION", changeCallback: (_, newValue) =>
                {
                    if(newValue != Functions.CRC32)
                    {
                        this.Log(LogLevel.Warning, "Unsupported function selected: {0}", newValue);
                    }
                })
                .WithReservedBits(8, 23)
                .WithFlag(31, FieldMode.Read, name: "CRCERROR", valueProviderCallback: _ => crcErrorOccurred)
                .WithChangeCallback((_, __) =>
                {
                    if(calculationEnabled.Value && functionSelect.Value == Functions.CRC32)
                    {
                        CalculateCrc32();
                    }
                })
                // Every write clears the error status.
                .WithWriteCallback((_, __) => crcErrorOccurred = false)
                ;

            Registers.SourceAddress.Define(this)
                .WithValueField(0, 32, out address, name: "ADDR")
                ;

            Registers.Length.Define(this)
                .WithReservedBits(0, 2)
                .WithValueField(2, 22, out wordLength, name: "LEN")
                .WithReservedBits(24, 8)
                ;

            Registers.CRCSeedOrResult.Define(this)
                .WithValueField(0, 32, out crcSeedOrResult, name: "CRC")
                ;

            Registers.LockControl.Define(this)
                .WithTag("SELECT", 0, 8)
                .WithReservedBits(8, 24)
                ;

            Registers.LockStatus.Define(this)
                .WithTag("STATUS", 0, 32)
                ;

            Registers.Key0.Define(this)
                .WithTag("KEY0", 0, 32)
                ;

            Registers.Key1.Define(this)
                .WithTag("KEY1", 0, 32)
                ;

            Registers.Key2.Define(this)
                .WithTag("KEY2", 0, 32)
                ;

            Registers.Key3.Define(this)
                .WithTag("KEY3", 0, 32)
                ;
        }

        private bool crcErrorOccurred;

        private IValueRegisterField address;
        private IValueRegisterField crcSeedOrResult;
        private IFlagRegisterField calculationEnabled;
        private IEnumRegisterField<Functions> functionSelect;
        private IValueRegisterField wordLength;

        private readonly IBusController systemBus;

        private const uint ValidCrcSeed = 0xFFFFFFFF;

        private enum Functions
        {
            CRC32 = 0x0,
            DMAPseudoRandomNumberStreamFromCRC = 0x1,
            GenerateDMAStreamFromAddress = 0x2,
        }

        private enum Registers : long
        {
            Control = 0x0,
            SourceAddress = 0x10,
            Length = 0x20,
            CRCSeedOrResult = 0x30,
            LockControl = 0x78,
            LockStatus = 0x7C,
            Key0 = 0x80,
            Key1 = 0x84,
            Key2 = 0x88,
            Key3 = 0x8C,
        }
    }
}
