using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class MH1903_BPU : BasicDoubleWordPeripheral, IKnownSize
    {
        public MH1903_BPU(IMachine machine) : base(machine)
        {
            BuildRegisterNameMap();
            DefineRegisters();
        }

        public long Size => 0x600;

        public override uint ReadDoubleWord(long offset)
        {
            var value = base.ReadDoubleWord(offset);
            var regName = GetRegisterName(offset);
            this.Log(LogLevel.Info, "BPU read at {0}: 0x{1:X8}", regName, value);
            return value;
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            var regName = GetRegisterName(offset);
            this.Log(LogLevel.Info, "BPU write at {0}: 0x{1:X8}", regName, value);
            base.WriteDoubleWord(offset, value);
        }

        private string GetRegisterName(long offset)
        {
            if(registerNames.TryGetValue(offset, out var name))
            {
                return name;
            }
            return $"0x{offset:X3}";
        }

        private void BuildRegisterNameMap()
        {
            registerNames = new Dictionary<long, string>();

            // KEY registers
            for(int i = 0; i < 16; i++)
            {
                registerNames[(long)Registers.EncryptionKey0 + (i * 4)] = $"EncryptionKey{i}";
            }

            // BPK registers
            registerNames[(long)Registers.BpkReady] = "BpkReady";
            registerNames[(long)Registers.BpkClear] = "BpkClear";
            registerNames[(long)Registers.BpkLastReadAddress] = "BpkLastReadAddress";
            registerNames[(long)Registers.BpkLastWriteAddress] = "BpkLastWriteAddress";
            registerNames[(long)Registers.BpkLockRegister] = "BpkLockRegister";
            registerNames[(long)Registers.BpkStatusControlRegister] = "BpkStatusControlRegister";
            registerNames[(long)Registers.BpkPowerRegister] = "BpkPowerRegister";

            // RTC registers
            registerNames[(long)Registers.RealTimeClockControlStatus] = "RealTimeClockControlStatus";
            registerNames[(long)Registers.RealTimeClockReference] = "RealTimeClockReference";
            registerNames[(long)Registers.RealTimeClockAlarmRegister] = "RealTimeClockAlarmRegister";
            registerNames[(long)Registers.RealTimeClockTimer] = "RealTimeClockTimer";
            registerNames[(long)Registers.RealTimeClockInterruptClear] = "RealTimeClockInterruptClear";
            registerNames[(long)Registers.Oscillator32KHz] = "Oscillator32KHz";
            registerNames[(long)Registers.RealTimeClockAttachmentTimer] = "RealTimeClockAttachmentTimer";
            registerNames[(long)Registers.BpkResetRegister] = "BpkResetRegister";

            // SEN registers
            registerNames[(long)Registers.SensorExternalType] = "SensorExternalType";
            registerNames[(long)Registers.SensorExternalConfig] = "SensorExternalConfig";
            registerNames[(long)Registers.SensorSoftwareEnable] = "SensorSoftwareEnable";
            registerNames[(long)Registers.SensorState] = "SensorState";
            registerNames[(long)Registers.SensorBridge] = "SensorBridge";
            registerNames[(long)Registers.SensorSoftwareAttack] = "SensorSoftwareAttack";
            registerNames[(long)Registers.SensorSoftwareLock] = "SensorSoftwareLock";
            registerNames[(long)Registers.SensorAttackCounter] = "SensorAttackCounter";
            registerNames[(long)Registers.SensorAttackType] = "SensorAttackType";
            registerNames[(long)Registers.SensorVoltageGlitchDetect] = "SensorVoltageGlitchDetect";
            registerNames[(long)Registers.SensorRandomNumberGeneratorInitialization] = "SensorRandomNumberGeneratorInitialization";

            // SEN_EN registers
            for(int i = 0; i < 19; i++)
            {
                registerNames[(long)Registers.SensorEnable0 + (i * 4)] = $"SensorEnable{i}";
            }

            // Additional sensor registers
            registerNames[(long)Registers.SensorExtendedStart] = "SensorExtendedStart";
            registerNames[(long)Registers.SensorLock] = "SensorLock";
            registerNames[(long)Registers.SensorAnalog0] = "SensorAnalog0";
            registerNames[(long)Registers.SensorAnalog1] = "SensorAnalog1";
            registerNames[(long)Registers.SensorAttackClear] = "SensorAttackClear";
            registerNames[(long)Registers.SensorPullUpPullDownConfig] = "SensorPullUpPullDownConfig";

            // BPK_RAM
            for(int i = 0; i < 256; i++)
            {
                registerNames[(long)Registers.BreakpointRam0 + (i * 4)] = $"BreakpointRam{i}";
            }
        }

        private void DefineRegisters()
        {
            // KEY registers 0x00-0x3C (16 words)
            for(int i = 0; i < 16; i++)
            {
                ((Registers)((long)Registers.EncryptionKey0 + (i * 4))).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"EncryptionKey{i}");
            }

            // Reserved 0x40-0x7C
            for(int i = 0; i < 16; i++)
            {
                ((Registers)((long)Registers.BpuReserved0 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // BPK registers
            Registers.BpkReady.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0x00000001, name: "BpkReady");
            Registers.BpkClear.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "BpkClear");
            Registers.BpkLastReadAddress.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "BpkLastReadAddress");
            Registers.BpkLastWriteAddress.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "BpkLastWriteAddress");
            Registers.BpuReserved1.Define(this)
                .WithReservedBits(0, 32);
            Registers.BpkLockRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "BpkLockRegister");
            Registers.BpkStatusControlRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "BpkStatusControlRegister");
            Registers.BpkPowerRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "BpkPowerRegister");

            // RTC (Real-Time Clock) registers
            Registers.RealTimeClockControlStatus.Define(this, resetValue: 0x00000008) // Default: 1 << 3
                .WithValueField(0, 32, name: "RealTimeClockControlStatus");
            Registers.RealTimeClockReference.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RealTimeClockReference");
            Registers.RealTimeClockAlarmRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RealTimeClockAlarmRegister");
            Registers.RealTimeClockTimer.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RealTimeClockTimer");
            Registers.RealTimeClockInterruptClear.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "RealTimeClockInterruptClear");
            Registers.Oscillator32KHz.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "Oscillator32KHz");
            Registers.RealTimeClockAttachmentTimer.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RealTimeClockAttachmentTimer");
            Registers.BpkResetRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "BpkResetRegister");

            // SEN (Sensor) registers
            Registers.SensorExternalType.Define(this, resetValue: 0x000FF000)
                .WithValueField(0, 32, name: "SensorExternalType");
            Registers.SensorExternalConfig.Define(this, resetValue: 0x00A5A000)
                .WithValueField(0, 32, name: "SensorExternalConfig");
            Registers.SensorSoftwareEnable.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "SensorSoftwareEnable");
            Registers.SensorState.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Read, name: "SensorState");
            Registers.SensorBridge.Define(this, resetValue: 0x000000F0)
                .WithValueField(0, 32, name: "SensorBridge");
            Registers.SensorSoftwareAttack.Define(this, resetValue: 0x80000000)
                .WithValueField(0, 32, name: "SensorSoftwareAttack");
            Registers.SensorSoftwareLock.Define(this, resetValue: 0x0000000F)
                .WithValueField(0, 32, name: "SensorSoftwareLock");
            Registers.SensorAttackCounter.Define(this, resetValue: 0x0000000F)
                .WithValueField(0, 32, FieldMode.Read, name: "SensorAttackCounter");
            Registers.SensorAttackType.Define(this, resetValue: 0x00000001)
                .WithValueField(0, 32, FieldMode.Read, name: "SensorAttackType");
            Registers.SensorVoltageGlitchDetect.Define(this, resetValue: 0x00000003)
                .WithValueField(0, 32, name: "SensorVoltageGlitchDetect");
            Registers.SensorRandomNumberGeneratorInitialization.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "SensorRandomNumberGeneratorInitialization");

            // Reserved 0xEC-0x100
            for(int i = 0; i < 6; i++)
            {
                ((Registers)((long)Registers.Reserved3 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // SEN_EN registers 0x104-0x14C
            // SensorExternal0Enable through SensorExternal7Enable (0x104-0x120): 0x000000AA
            for(int i = 0; i < 8; i++)
            {
                ((Registers)((long)Registers.SensorEnable0 + (i * 4))).Define(this, resetValue: 0x000000AA)
                    .WithValueField(0, 32, name: $"SensorExternal{i}Enable");
            }

            // SensorReserved (0x124-0x130): 0x00000000
            for(int i = 8; i < 11; i++)
            {
                ((Registers)((long)Registers.SensorEnable0 + (i * 4))).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"SensorReserved{i}");
            }

            // SensorVoltageHighEnable (0x134): 0x000000AA
            ((Registers)((long)Registers.SensorEnable0 + (11 * 4))).Define(this, resetValue: 0x000000AA)
                .WithValueField(0, 32, name: "SensorVoltageHighEnable");

            // SensorVoltageLowEnable (0x138): 0x000000AA
            ((Registers)((long)Registers.SensorEnable0 + (12 * 4))).Define(this, resetValue: 0x000000AA)
                .WithValueField(0, 32, name: "SensorVoltageLowEnable");

            // SensorTemperatureHighEnable (0x13C): 0x000000AA
            ((Registers)((long)Registers.SensorEnable0 + (13 * 4))).Define(this, resetValue: 0x000000AA)
                .WithValueField(0, 32, name: "SensorTemperatureHighEnable");

            // SensorTemperatureLowEnable (0x140): 0x000000AA
            ((Registers)((long)Registers.SensorEnable0 + (14 * 4))).Define(this, resetValue: 0x000000AA)
                .WithValueField(0, 32, name: "SensorTemperatureLowEnable");

            // SensorCrystal32KhzEnable (0x144): 0x00000055
            ((Registers)((long)Registers.SensorEnable0 + (15 * 4))).Define(this, resetValue: 0x00000055)
                .WithValueField(0, 32, name: "SensorCrystal32KhzEnable");

            // SensorMessageEnable (0x148): 0x000000AA
            ((Registers)((long)Registers.SensorEnable0 + (16 * 4))).Define(this, resetValue: 0x000000AA)
                .WithValueField(0, 32, name: "SensorMessageEnable");

            // SensorVoltageGlitchLatchEnable (0x14C): 0x000000AA
            ((Registers)((long)Registers.SensorEnable0 + (17 * 4))).Define(this, resetValue: 0x000000AA)
                .WithValueField(0, 32, name: "SensorVoltageGlitchLatchEnable");

            // Additional sensor registers
            Registers.SensorExtendedStart.Define(this, resetValue: 0x800000AA)
                .WithValueField(0, 32, name: "SensorExtendedStart");
            Registers.SensorLock.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "SensorLock");
            Registers.SensorAnalog0.Define(this, resetValue: 0x02350220)
                .WithValueField(0, 32, name: "SensorAnalog0");
            Registers.SensorAnalog1.Define(this, resetValue: 0x00000024)
                .WithValueField(0, 32, name: "SensorAnalog1");

            // SensorAttackClear 0x160
            Registers.SensorAttackClear.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "SensorAttackClear");

            // Reserved 0x164-0x170
            for(int i = 0; i < 4; i++)
            {
                ((Registers)(0x164 + (i * 4))).Define(this, resetValue: 0x00000000)
                    .WithReservedBits(0, 32);
            }

            // SensorPullUpPullDownConfig 0x174
            Registers.SensorPullUpPullDownConfig.Define(this, resetValue: 0xFF0000FF)
                .WithValueField(0, 32, name: "SensorPullUpPullDownConfig");

            // Reserved 0x178-0x1FC
            for(int i = 0; i < 34; i++)
            {
                ((Registers)((long)Registers.BpuReserved4 + (i * 4))).Define(this)
                    .WithReservedBits(0, 32);
            }

            // BreakpointRam 0x200-0x5FC (256 words)
            for(int i = 0; i < 256; i++)
            {
                ((Registers)((long)Registers.BreakpointRam0 + (i * 4))).Define(this, resetValue: 0x00000000)
                    .WithValueField(0, 32, name: $"BreakpointRam{i}");
            }
        }

        private Dictionary<long, string> registerNames;

        private enum Registers : long
        {
            // KEY registers 0x00-0x3C
            EncryptionKey0 = 0x000,
            EncryptionKey1 = 0x004,
            EncryptionKey2 = 0x008,
            EncryptionKey3 = 0x00C,
            EncryptionKey4 = 0x010,
            EncryptionKey5 = 0x014,
            EncryptionKey6 = 0x018,
            EncryptionKey7 = 0x01C,
            EncryptionKey8 = 0x020,
            EncryptionKey9 = 0x024,
            EncryptionKey10 = 0x028,
            EncryptionKey11 = 0x02C,
            EncryptionKey12 = 0x030,
            EncryptionKey13 = 0x034,
            EncryptionKey14 = 0x038,
            EncryptionKey15 = 0x03C,

            // Reserved 0x40-0x7C
            BpuReserved0 = 0x040,

            // BPK registers 0x80-0x9C
            BpkReady = 0x080,
            BpkClear = 0x084,
            BpkLastReadAddress = 0x088,
            BpkLastWriteAddress = 0x08C,
            BpuReserved1 = 0x090,
            BpkLockRegister = 0x094,
            BpkStatusControlRegister = 0x098,
            BpkPowerRegister = 0x09C,

            // RTC registers 0xA0-0xBC
            RealTimeClockControlStatus = 0x0A0,
            RealTimeClockReference = 0x0A4,
            RealTimeClockAlarmRegister = 0x0A8,
            RealTimeClockTimer = 0x0AC,
            RealTimeClockInterruptClear = 0x0B0,
            Oscillator32KHz = 0x0B4,
            RealTimeClockAttachmentTimer = 0x0B8,
            BpkResetRegister = 0x0BC,

            // SEN registers 0xC0-0xE8
            SensorExternalType = 0x0C0,
            SensorExternalConfig = 0x0C4,
            SensorSoftwareEnable = 0x0C8,
            SensorState = 0x0CC,
            SensorBridge = 0x0D0,
            SensorSoftwareAttack = 0x0D4,
            SensorSoftwareLock = 0x0D8,
            SensorAttackCounter = 0x0DC,
            SensorAttackType = 0x0E0,
            SensorVoltageGlitchDetect = 0x0E4,
            SensorRandomNumberGeneratorInitialization = 0x0E8,

            // Reserved 0xEC-0x100
            Reserved3 = 0x0EC,

            // SEN_EN registers 0x104-0x14C
            SensorEnable0 = 0x104,

            // Additional sensor registers 0x150-0x160
            SensorExtendedStart = 0x150,
            SensorLock = 0x154,
            SensorAnalog0 = 0x158,
            SensorAnalog1 = 0x15C,
            SensorAttackClear = 0x160,

            // SEN_PUPU_CFG 0x174
            SensorPullUpPullDownConfig = 0x174,

            // Reserved 0x178-0x1FC
            BpuReserved4 = 0x178,

            // BPK_RAM 0x200-0x5FC
            BreakpointRam0 = 0x200,
        }
    }
}
