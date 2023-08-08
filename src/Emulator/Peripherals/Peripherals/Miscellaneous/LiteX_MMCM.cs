//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class LiteX_MMCM : BasicDoubleWordPeripheral, IKnownSize
    {
        public LiteX_MMCM(IMachine machine) : base(machine)
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
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8,24)
            ;

            Registers.Locked.Define(this)
                .WithFlag(0, name: "locked", valueProviderCallback: _ => true) // we are always ready
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8,24)
            ;

            Registers.Read.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "read", writeCallback: (_, val) => 
                {
                    if(val)
                    {
                        HandleRead();
                    }
                })
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8,24)
            ;

            Registers.Write.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "write", writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        HandleWrite();
                    }
                })
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8,24)
            ;

            Registers.DataReady.Define(this)
                .WithFlag(0, out dataReadyField, name: "drdy")
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8,24)
            ;
            Registers.Address.Define(this)
                .WithValueField(0, 8, out addressField, name: "addr")
                .WithIgnoredBits(8,24)
            ;
            Registers.DataWriteLow.Define(this)
                .WithValueField(0, 8, out dataWriteLowField, FieldMode.Write, name: "dat_wL")
                .WithIgnoredBits(8,24)
            ;
            Registers.DataWriteHigh.Define(this)
                .WithValueField(0, 8, out dataWriteHighField, FieldMode.Write, name: "dat_wH")
                .WithIgnoredBits(8,24)
            ;
            Registers.DataReadLow.Define(this)
                .WithValueField(0, 8, out dataReadLowField, FieldMode.Read, name: "dat_rL")
                .WithIgnoredBits(8,24)
            ;
            Registers.DataReadHigh.Define(this)
                .WithValueField(0, 8, out dataReadHighField, FieldMode.Read, name: "dat_rH")
                .WithIgnoredBits(8,24)
            ;
        }

        private void HandleRead()
        {
            if((int)addressField.Value >= mmcmRegisters.Length)
            {
                this.Log(LogLevel.Error, "Trying to read from a non-existing MMCM register #{0}. This model supports registers <0-{1}>", addressField.Value, mmcmRegisters.Length - 1);
                return;
            }

            dataReadLowField.Value = (byte)mmcmRegisters[addressField.Value];
            dataReadHighField.Value = mmcmRegisters[addressField.Value] >> 8;
            dataReadyField.Value = true;
        }

        private void HandleWrite()
        {
            if((int)addressField.Value >= mmcmRegisters.Length)
            {
                this.Log(LogLevel.Error, "Trying to write to a non-existing MMCM register #{0}. This model supports registers <0-{1}>", addressField.Value, mmcmRegisters.Length - 1);
                return;
            }

            mmcmRegisters[addressField.Value] = (uint)((dataWriteHighField.Value << 8) | dataWriteLowField.Value);
            dataReadyField.Value = true;
        }

        private IFlagRegisterField dataReadyField;
        private IValueRegisterField addressField;
        private IValueRegisterField dataReadLowField;
        private IValueRegisterField dataReadHighField;
        private IValueRegisterField dataWriteLowField;
        private IValueRegisterField dataWriteHighField;

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
            DataWriteLow = 0x18,
            DataWriteHigh = 0x1c,
            DataReadLow = 0x20,
            DataReadHigh = 0x24
        }
    }
}
