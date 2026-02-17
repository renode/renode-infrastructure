using System;
using System.IO;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class MH1903_OTP : BasicDoubleWordPeripheral, IBytePeripheral, IKnownSize
    {
        public MH1903_OTP(IMachine machine) : base(machine)
        {
            // OTP Data area: 0x2000 bytes (8KB)
            otpData = new byte[OtpDataSize];
            // Initialize to 0xFF (unprogrammed OTP state)
            for(int i = 0; i < otpData.Length; i++)
            {
                otpData[i] = 0xFF;
            }

            DefineRegisters();
        }

        public void LoadFromFile(string path)
        {
            if(!File.Exists(path))
            {
                this.Log(LogLevel.Warning, "OTP file not found: {0}", path);
                return;
            }

            try
            {
                var fileData = File.ReadAllBytes(path);
                var bytesToCopy = Math.Min(fileData.Length, otpData.Length);
                Array.Copy(fileData, otpData, bytesToCopy);
                this.Log(LogLevel.Info, "Loaded {0} bytes of OTP data from {1}", bytesToCopy, path);
            }
            catch(Exception e)
            {
                this.Log(LogLevel.Error, "Failed to load OTP file {0}: {1}", path, e.Message);
            }
        }

        public void SaveToFile(string path)
        {
            try
            {
                File.WriteAllBytes(path, otpData);
                this.Log(LogLevel.Info, "Saved OTP data to {0}", path);
            }
            catch(Exception e)
            {
                this.Log(LogLevel.Error, "Failed to save OTP file {0}: {1}", path, e.Message);
            }
        }

        public void SetRSASignKey(string modulusHex, string exponentHex)
        {
            if(string.IsNullOrEmpty(modulusHex) || string.IsNullOrEmpty(exponentHex))
            {
                this.Log(LogLevel.Error, "SetRSASignKey: modulus or exponent is null or empty");
                return;
            }

            // Parse hex strings to byte arrays
            byte[] modulus;
            byte[] exponent;

            try
            {
                modulus = ParseHexString(modulusHex);
                exponent = ParseHexString(exponentHex);
            }
            catch(Exception e)
            {
                this.Log(LogLevel.Error, "SetRSASignKey: failed to parse hex strings: {0}", e.Message);
                return;
            }

            if(modulus.Length != 256)
            {
                this.Log(LogLevel.Error, "SetRSASignKey: modulus must be 256 bytes (512 hex chars), got {0} bytes", modulus.Length);
                return;
            }

            if(exponent.Length > 8)
            {
                this.Log(LogLevel.Error, "SetRSASignKey: exponent must be 8 bytes or less (16 hex chars max), got {0} bytes", exponent.Length);
                return;
            }

            // RSA Key Slot 1 (Secondary) - Offset 0x307 (exponent), 0x309 (modulus)
            this.Log(LogLevel.Info, "Writing RSA key to Slot 1 (Secondary) at offset 0x307");

            // Write exponent (8 bytes) - pad with zeros if needed
            for(int i = 0; i < 8; i++)
            {
                otpData[0x307 + i] = (i < exponent.Length) ? exponent[i] : (byte)0;
            }

            // Write modulus (256 bytes)
            Array.Copy(modulus, 0, otpData, 0x309, 256);

            // RSA Key Slot 2 (Primary) - Offset 0x407 (exponent), 0x409 (modulus)
            this.Log(LogLevel.Info, "Writing RSA key to Slot 2 (Primary) at offset 0x407");

            // Write exponent (8 bytes) - pad with zeros if needed
            for(int i = 0; i < 8; i++)
            {
                otpData[0x407 + i] = (i < exponent.Length) ? exponent[i] : (byte)0;
            }

            // Write modulus (256 bytes)
            Array.Copy(modulus, 0, otpData, 0x409, 256);

            this.Log(LogLevel.Info, "RSA keys written to both slots");
        }

        public override uint ReadDoubleWord(long offset)
        {
            // OTP data area (0x0000-0x1FFC)
            if(offset < OtpDataSize)
            {
                // Ensure 4-byte aligned access
                if(offset % 4 != 0)
                {
                    this.Log(LogLevel.Warning, "Unaligned OTP read at offset 0x{0:X}", offset);
                    return 0;
                }

                // Read 32-bit value from OTP data (little-endian)
                uint value = (uint)(otpData[offset] |
                                   (otpData[offset + 1] << 8) |
                                   (otpData[offset + 2] << 16) |
                                   (otpData[offset + 3] << 24));
                return value;
            }

            // Control registers (0x2000+)
            return RegistersCollection.Read(offset);
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            // OTP data area (0x0000-0x1FFC)
            if(offset < OtpDataSize)
            {
                // OTP is typically write-once (one-time programmable)
                // We'll allow writes but log them
                if(offset % 4 != 0)
                {
                    this.Log(LogLevel.Warning, "Unaligned OTP write at offset 0x{0:X}", offset);
                    return;
                }

                this.Log(LogLevel.Debug, "OTP write at offset 0x{0:X}: 0x{1:X}", offset, value);

                // Write 32-bit value to OTP data (little-endian)
                // In real OTP, you can only change bits from 1 to 0 (programming)
                // For emulation, we'll just allow direct writes
                otpData[offset] = (byte)(value & 0xFF);
                otpData[offset + 1] = (byte)((value >> 8) & 0xFF);
                otpData[offset + 2] = (byte)((value >> 16) & 0xFF);
                otpData[offset + 3] = (byte)((value >> 24) & 0xFF);
                return;
            }

            // Control registers (0x2000+)
            RegistersCollection.Write(offset, value);
        }

        public byte ReadByte(long offset)
        {
            // OTP data area (0x0000-0x1FFF)
            if(offset < OtpDataSize)
            {
                return otpData[offset];
            }

            // Control registers (0x2000+)
            // Read the doubleword and extract the appropriate byte
            var value = RegistersCollection.Read(offset & ~3);
            var byteOffset = (int)(offset & 3);
            return (byte)((value >> (byteOffset * 8)) & 0xFF);
        }

        public void WriteByte(long offset, byte value)
        {
            // OTP data area (0x0000-0x1FFF)
            if(offset < OtpDataSize)
            {
                this.Log(LogLevel.Debug, "OTP byte write at offset 0x{0:X}: 0x{1:X}", offset, value);
                otpData[offset] = value;
                return;
            }

            // Control registers (0x2000+)
            // Read-modify-write for register area
            var alignedOffset = offset & ~3;
            var byteOffset = (int)(offset & 3);
            var currentValue = RegistersCollection.Read(alignedOffset);
            var mask = (uint)(0xFF << (byteOffset * 8));
            var newValue = (currentValue & ~mask) | ((uint)value << (byteOffset * 8));
            RegistersCollection.Write(alignedOffset, newValue);
        }

        public long Size => 0x4000;

        private byte[] ParseHexString(string hex)
        {
            // Remove common prefixes and whitespace
            hex = hex.Replace("0x", "").Replace("0X", "").Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");

            if(hex.Length % 2 != 0)
            {
                throw new ArgumentException("Hex string must have an even number of characters");
            }

            byte[] bytes = new byte[hex.Length / 2];
            for(int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        private void DefineRegisters()
        {
            // Configuration Register at 0x2000
            Registers.ConfigurationRegister.Define(this, resetValue: 0x00000008)
                .WithValueField(0, 32, name: "ConfigurationData");

            // Control/Status Register at 0x2004
            Registers.ControlStatusRegister.Define(this, resetValue: 0x80000000)
                .WithFlag(31, name: "OtpReady", mode: FieldMode.Read, valueProviderCallback: _ => true)
                .WithReservedBits(0, 31);

            // Protection Register at 0x2008
            Registers.ProtectionRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ProtectionData");

            // Address Register at 0x200C
            Registers.AddressRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "OtpAddress");

            // Program Data Register at 0x2010
            Registers.ProgramDataRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ProgramData");

            // Read-Only Control Register at 0x2014
            Registers.ReadOnlyControlRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ReadOnlyControl");

            // Read-Only Lock Enable Register at 0x2018
            Registers.ReadOnlyLockEnableRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ReadOnlyLockLength");

            // Reserved Register at 0x201C
            Registers.ReservedRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "Reserved");

            // Timing Register at 0x2020
            Registers.TimingRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "Timing");

            // Timing Enable Register at 0x2024
            Registers.TimingEnableRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "TimingEnable");
        }

        private readonly byte[] otpData;
        private const int OtpDataSize = 0x2000; // 8KB (0x0000-0x1FFC)

        private enum Registers : long
        {
            ConfigurationRegister = 0x2000,
            ControlStatusRegister = 0x2004,
            ProtectionRegister = 0x2008,
            AddressRegister = 0x200c,
            ProgramDataRegister = 0x2010,
            ReadOnlyControlRegister = 0x2014,
            ReadOnlyLockEnableRegister = 0x2018,
            ReservedRegister = 0x201c,
            TimingRegister = 0x2020,
            TimingEnableRegister = 0x2024,
        }
    }
}
