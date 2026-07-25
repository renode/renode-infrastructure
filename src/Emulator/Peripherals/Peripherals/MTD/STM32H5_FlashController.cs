//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.MTD
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32H5_FlashController : STM32_FlashController, IKnownSize
    {
        public STM32H5_FlashController(IMachine machine, MappedMemory flash) : base(machine)
        {
            this.flash = flash;
            nonSecureLock = new LockRegister(this, nameof(nonSecureLock), NonSecureKeys);

            DefineRegisters();
            Reset();
        }

        public GPIO IRQ { get; } = new GPIO();

        public override void Reset()
        {
            base.Reset();
            nonSecureLock.Reset();
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.AccessControl.Define(this, 0x00000000)
                .WithValueField(0, 4, name: "LATENCY")
                .WithValueField(4, 2, name: "WRHIGHFREQ")
                .WithReservedBits(6, 2)
                .WithFlag(8, name: "PRFTEN")
                .WithReservedBits(9, 23);

            Registers.NonSecureKey.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "NSKEY", writeCallback: (_, value) =>
                {
                    nonSecureLock.ConsumeValue((uint)value);
                    if(nonSecureLock.DisabledUntilReset)
                    {
                        this.Log(LogLevel.Warning,
                            "Bad unlock key 0x{0:X8} written to NSKEYR; flash stays locked until reset", value);
                    }
                });

            Registers.OperationStatus.Define(this, 0x00000000)
                .WithValueField(0, 20, FieldMode.Read, name: "ADDR_OP")
                .WithReservedBits(20, 1)
                .WithFlag(21, FieldMode.Read, name: "DATA_OP")
                .WithFlag(22, FieldMode.Read, name: "BK_OP")
                .WithFlag(23, FieldMode.Read, name: "SYSF_OP")
                .WithFlag(24, FieldMode.Read, name: "OTP_OP")
                .WithReservedBits(25, 4)
                .WithValueField(29, 3, FieldMode.Read, name: "CODE_OP");

            Registers.NonSecureStatus.Define(this, 0x00000000)
                .WithFlag(0, FieldMode.Read, name: "BSY")
                .WithFlag(1, FieldMode.Read, name: "WBNE")
                .WithReservedBits(2, 1)
                .WithFlag(3, FieldMode.Read, name: "DBNE")
                .WithReservedBits(4, 12)
                .WithFlag(16, out eop, name: "EOP")
                .WithFlag(17, out wrperr, name: "WRPERR")
                .WithFlag(18, out pgserr, name: "PGSERR")
                .WithFlag(19, out strberr, name: "STRBERR")
                .WithFlag(20, out incerr, name: "INCERR")
                .WithFlag(21, out obkerr, name: "OBKERR")
                .WithFlag(22, out obkwerr, name: "OBKWERR")
                .WithFlag(23, out optchangeerr, name: "OPTCHANGEERR")
                .WithReservedBits(24, 8);

            Registers.NonSecureControl.Define(this, 0x00000001)
                .WithFlag(0, FieldMode.Read | FieldMode.Set, name: "LOCK",
                    valueProviderCallback: _ => nonSecureLock.IsLocked,
                    changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            nonSecureLock.Lock();
                        }
                    })
                .WithFlag(1, name: "PG")
                .WithFlag(2, name: "SER")
                .WithFlag(3, name: "BER")
                .WithFlag(4, name: "FW")
                .WithFlag(5, name: "START")
                .WithValueField(6, 7, name: "SNB")
                .WithReservedBits(13, 2)
                .WithFlag(15, name: "MER")
                .WithFlag(16, name: "EOPIE")
                .WithFlag(17, name: "WRPERRIE")
                .WithFlag(18, name: "PGSERRIE")
                .WithFlag(19, name: "STRBERRIE")
                .WithFlag(20, name: "INCERRIE")
                .WithFlag(21, name: "OBKERRIE")
                .WithFlag(22, name: "OBKWERRIE")
                .WithFlag(23, name: "OPTCHANGEERRIE")
                .WithReservedBits(24, 5)
                .WithFlag(29, name: "INV")
                .WithReservedBits(30, 1)
                .WithFlag(31, name: "BKSEL");

            Registers.NonSecureClearControl.Define(this, 0x00000000)
                .WithReservedBits(0, 16)
                .WithFlag(16, FieldMode.Write, name: "CLR_EOP",
                    writeCallback: (_, val) => { if(val) eop.Value = false; })
                .WithFlag(17, FieldMode.Write, name: "CLR_WRPERR",
                    writeCallback: (_, val) => { if(val) wrperr.Value = false; })
                .WithFlag(18, FieldMode.Write, name: "CLR_PGSERR",
                    writeCallback: (_, val) => { if(val) pgserr.Value = false; })
                .WithFlag(19, FieldMode.Write, name: "CLR_STRBERR",
                    writeCallback: (_, val) => { if(val) strberr.Value = false; })
                .WithFlag(20, FieldMode.Write, name: "CLR_INCERR",
                    writeCallback: (_, val) => { if(val) incerr.Value = false; })
                .WithFlag(21, FieldMode.Write, name: "CLR_OBKERR",
                    writeCallback: (_, val) => { if(val) obkerr.Value = false; })
                .WithFlag(22, FieldMode.Write, name: "CLR_OBKWERR",
                    writeCallback: (_, val) => { if(val) obkwerr.Value = false; })
                .WithFlag(23, FieldMode.Write, name: "CLR_OPTCHANGEERR",
                    writeCallback: (_, val) => { if(val) optchangeerr.Value = false; })
                .WithReservedBits(24, 8);
        }

        private IFlagRegisterField eop;
        private IFlagRegisterField wrperr;
        private IFlagRegisterField pgserr;
        private IFlagRegisterField strberr;
        private IFlagRegisterField incerr;
        private IFlagRegisterField obkerr;
        private IFlagRegisterField obkwerr;
        private IFlagRegisterField optchangeerr;

        private readonly MappedMemory flash;
        private readonly LockRegister nonSecureLock;

        private static readonly uint[] NonSecureKeys = { 0x45670123, 0xCDEF89AB };

        private enum Registers : long
        {
            AccessControl = 0x00,
            NonSecureKey = 0x04,
            // OPTKEYR 0x0C deliberately not defined — option bytes are out of scope
            OperationStatus = 0x18,
            // OPTCR 0x1C deliberately not defined — option bytes are out of scope
            NonSecureStatus = 0x20,
            NonSecureControl = 0x28,
            NonSecureClearControl = 0x30,
        }
    }
}
