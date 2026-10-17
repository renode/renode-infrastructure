//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // Register layout per RM0433 (STM32H742/743/750, single-core line). Renode doesn't
    // model analog supply rails, so PVD/AVD/temperature/VBAT monitoring stay as
    // WithTaggedFlag (same simplification STM32_PWR already uses for PVDO) rather than
    // faking a threshold-voltage curve; sleep/standby transitions are storage-only for
    // the same reason CPU sleep state isn't otherwise observable from software.
    public sealed class STM32H7_PWR : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32H7_PWR(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        // WKUPFR isn't otherwise settable from software (RM0433: cleared only via WKUPCR
        // or a system reset) -- Renode has no wakeup-pin GPIO wiring for this peripheral
        // yet, so this is the way a test or a wired-up board file raises one.
        public void SetWakeupFlag(int pin, bool value)
        {
            wakeupFlags[pin].Value = value;
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.Control1.Define(this)
                .WithFlag(0, name: "LPDS")
                .WithReservedBits(1, 3)
                .WithTaggedFlag("PVDE", 4)
                .WithTag("PLS", 5, 3)
                .WithFlag(8, out dbp, name: "DBP")
                .WithFlag(9, name: "FLPS")
                .WithReservedBits(10, 4)
                .WithTag("SVOS", 14, 2)
                .WithTaggedFlag("AVDEN", 16)
                .WithTag("ALS", 17, 2)
                .WithReservedBits(19, 13);

            Registers.ControlStatus1.Define(this)
                .WithReservedBits(0, 4)
                .WithTaggedFlag("PVDO", 4)
                .WithReservedBits(5, 8)
                .WithFlag(13, out actvosrdy, FieldMode.Read, name: "ACTVOSRDY")
                .WithValueField(14, 2, out actvos, FieldMode.Read, name: "ACTVOS")
                .WithTaggedFlag("AVDO", 16)
                .WithReservedBits(17, 15);

            Registers.Control2.Define(this)
                .WithFlag(0, out bren, name: "BREN", writeCallback: (_, value) => WithBackupWriteProtection(() => bren.Value = value))
                .WithReservedBits(1, 3)
                .WithFlag(4, name: "MONEN", writeCallback: (_, value) => WithBackupWriteProtection(() => { }))
                .WithReservedBits(5, 11)
                .WithFlag(16, out brrdy, FieldMode.Read, name: "BRRDY")
                .WithReservedBits(17, 3)
                .WithTaggedFlag("VBATL", 20)
                .WithTaggedFlag("VBATH", 21)
                .WithTaggedFlag("TEMPL", 22)
                .WithTaggedFlag("TEMPH", 23)
                .WithReservedBits(24, 8);

            Registers.Control3.Define(this)
                .WithFlag(0, name: "BYPASS")
                .WithFlag(1, name: "LDOEN")
                .WithFlag(2, name: "SCUEN")
                .WithReservedBits(3, 5)
                .WithFlag(8, name: "VBE")
                .WithFlag(9, name: "VBRS")
                .WithReservedBits(10, 14)
                .WithFlag(24, out usb33den, name: "USB33DEN", writeCallback: (_, __) => UpdateUsbReady())
                .WithFlag(25, out usbregen, name: "USBREGEN", writeCallback: (_, __) => UpdateUsbReady())
                .WithFlag(26, out usb33rdy, FieldMode.Read, name: "USB33RDY")
                .WithReservedBits(27, 5);

            Registers.CpuControl.Define(this)
                .WithFlag(0, name: "PDDS_D1")
                .WithFlag(1, name: "PDDS_D2")
                .WithFlag(2, name: "PDDS_D3")
                .WithReservedBits(3, 2)
                .WithFlag(5, out stopf, FieldMode.Read, name: "STOPF")
                .WithFlag(6, out sbf, FieldMode.Read, name: "SBF")
                .WithFlag(7, out sbfD1, FieldMode.Read, name: "SBF_D1")
                .WithFlag(8, out sbfD2, FieldMode.Read, name: "SBF_D2")
                .WithFlag(9, FieldMode.Write, name: "CSSF", writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        stopf.Value = false;
                        sbf.Value = false;
                        sbfD1.Value = false;
                        sbfD2.Value = false;
                    }
                })
                .WithReservedBits(10, 1)
                .WithFlag(11, name: "RUN_D3")
                .WithReservedBits(12, 20);

            Registers.D3Control.Define(this, 0x2000)
                .WithReservedBits(0, 13)
                .WithFlag(13, out vosrdy, FieldMode.Read, name: "VOSRDY")
                .WithEnumField<DoubleWordRegister, VoltageScalingSelection>(14, 2, out vos, name: "VOS", writeCallback: (_, __) => vosrdy.Value = true)
                .WithReservedBits(16, 16);

            Registers.WakeupClear.Define(this)
                .WithValueField(0, 6, FieldMode.Write, name: "WKUPC", writeCallback: (_, value) =>
                {
                    for(var i = 0; i < WakeupPinCount; ++i)
                    {
                        if(BitHelper.IsBitSet(value, (byte)i))
                        {
                            wakeupFlags[i].Value = false;
                        }
                    }
                })
                .WithReservedBits(6, 26);

            Registers.WakeupFlag.Define(this)
                .WithFlags(0, WakeupPinCount, out wakeupFlags, FieldMode.Read, name: "WKUPF")
                .WithReservedBits(WakeupPinCount, 32 - WakeupPinCount);

            Registers.WakeupEnableAndPolarity.Define(this)
                .WithFlags(0, WakeupPinCount, name: "WKUPEN")
                .WithReservedBits(6, 2)
                .WithFlags(8, WakeupPinCount, name: "WKUPP")
                .WithReservedBits(14, 2)
                .WithTag("WKUPPUPD1", 16, 2)
                .WithTag("WKUPPUPD2", 18, 2)
                .WithTag("WKUPPUPD3", 20, 2)
                .WithTag("WKUPPUPD4", 22, 2)
                .WithTag("WKUPPUPD5", 24, 2)
                .WithTag("WKUPPUPD6", 26, 2)
                .WithReservedBits(28, 4);
        }

        private void UpdateUsbReady()
        {
            usb33rdy.Value = usb33den.Value && usbregen.Value;
        }

        // PWR_CR2 (BREN, MONEN, and by extension RCC_BDCR/RTC) is write-protected until
        // DBP is set in PWR_CR1 -- RM0433 5.8.3.
        private void WithBackupWriteProtection(System.Action write)
        {
            if(!dbp.Value)
            {
                this.Log(LogLevel.Warning, "Write to a backup-domain register ignored: DBP is not set");
                return;
            }
            write();
        }

        private IFlagRegisterField dbp;
        private IFlagRegisterField bren;
        private IFlagRegisterField brrdy;
        private IFlagRegisterField usb33den;
        private IFlagRegisterField usbregen;
        private IFlagRegisterField usb33rdy;
        private IFlagRegisterField stopf;
        private IFlagRegisterField sbf;
        private IFlagRegisterField sbfD1;
        private IFlagRegisterField sbfD2;
        private IFlagRegisterField vosrdy;
        private IFlagRegisterField actvosrdy;
        private IValueRegisterField actvos;
        private IEnumRegisterField<VoltageScalingSelection> vos;
        private IFlagRegisterField[] wakeupFlags;

        private const int WakeupPinCount = 6;

        private enum Registers
        {
            Control1 = 0x0,
            ControlStatus1 = 0x4,
            Control2 = 0x8,
            Control3 = 0xC,
            CpuControl = 0x10,
            D3Control = 0x18,
            WakeupClear = 0x20,
            WakeupFlag = 0x24,
            WakeupEnableAndPolarity = 0x28,
        }

        private enum VoltageScalingSelection
        {
            Scale3 = 1,
            Scale2 = 2,
            Scale1 = 3,
        }
    }
}
