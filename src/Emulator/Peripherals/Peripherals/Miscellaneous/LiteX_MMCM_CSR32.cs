//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class LiteX_MMCM_CSR32 : BasicDoubleWordPeripheral, IKnownSize
    {
        public LiteX_MMCM_CSR32(Machine machine) : base(machine)
        {
            mmcmRegisters = new uint[RegistersCount];

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();

            for(var i = 0; i < mmcmRegisters.Length; i++)
            {
                mmcmRegisters[i] = 0;
            }
        }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.Reset.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "reset", writeCallback: (_, val) => 
                {
                    if(val)
                    {
                        // this "reset" should not 
                        // clear the internal mmcm registers
                        RegistersCollection.Reset();
                    }
                })
                .WithIgnoredBits(1, 31)
            ;

            Registers.Locked.Define(this)
                .WithFlag(0, name: "locked", valueProviderCallback: _ => true) // we are always ready
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8, 24)
            ;

            Registers.Read.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "read", writeCallback: (_, val) => 
                {
                    if(val)
                    {
                        HandleRead();
                    }
                })
                .WithIgnoredBits(1, 31)
            ;

            Registers.Write.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "write", writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        HandleWrite();
                    }
                })
                .WithIgnoredBits(1, 31)
            ;

            Registers.DataReady.Define(this)
                .WithFlag(0, out dataReadyField, name: "drdy")
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8, 24)
            ;
            Registers.Address.Define(this)
                .WithValueField(0, 8, out addressField, name: "addr")
                .WithIgnoredBits(8, 24)
            ;
            Registers.DataWrite.Define(this)
                .WithValueField(0, 16, out dataWriteField, FieldMode.Write, name: "dat_w")
                .WithIgnoredBits(16, 16)
            ;
            Registers.DataRead.Define(this)
                .WithValueField(0, 16, out dataReadField, FieldMode.Read, name: "dat_r")
                .WithIgnoredBits(16, 16)
            ;
        }

        private void HandleRead()
        {
            if(addressField.Value >= mmcmRegisters.Length)
            {
                this.Log(LogLevel.Error, "Trying to read from a non-existing MMCM register #{0}. This model supports registers <0-{1}>", addressField.Value, mmcmRegisters.Length - 1);
                return;
            }

            dataReadField.Value = mmcmRegisters[addressField.Value];
            dataReadyField.Value = true;
        }

        private void HandleWrite()
        {
            if(addressField.Value >= mmcmRegisters.Length)
            {
                this.Log(LogLevel.Error, "Trying to write to a non-existing MMCM register #{0}. This model supports registers <0-{1}>", addressField.Value, mmcmRegisters.Length - 1);
                return;
            }

            mmcmRegisters[addressField.Value] = dataWriteField.Value;
            dataReadyField.Value = true;
        }

        private IFlagRegisterField dataReadyField;
        private IValueRegisterField addressField;
        private IValueRegisterField dataReadField;
        private IValueRegisterField dataWriteField;

        private uint[] mmcmRegisters;

        private const int RegistersCount = 0x50;    // 0x4F is the last register in MMCM

        private enum Registers
        {
            Reset = 0x0,
            Locked = 0x4,
            Read = 0x8,
            Write = 0xc,
            DataReady = 0x10,
            Address = 0x14,
            DataWrite = 0x18,
            DataRead = 0x1c,
        }
    }
}
