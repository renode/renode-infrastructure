using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.I2C;

using System;
using System.Text;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;


namespace Antmicro.Renode.Peripherals.Sensors
{
    public class LPS25HBTR : II2CPeripheral
    {
        private class Register 
        {
            public static readonly Register WhoAmI = new Register(0x0F, 1, new byte[] { 0xBD }, nameof(WhoAmI));
            public static readonly Register Temperature = new Register(0x2B, 2, new byte[2], nameof(Temperature));
            public static readonly Register Pressure = new Register(0x28, 3, new byte[3], nameof(Pressure));
            public static readonly Register PressureOffset = new Register(0x39, 2, new byte[2], nameof(PressureOffset));
            public static readonly Register Control1 = new Register(0x20, 1, new byte[1], nameof(Control1));
            
            public static readonly IEnumerable<Register> Values = typeof(Register)
                .GetFields(
                    BindingFlags.Public
                    | BindingFlags.Static
                    | BindingFlags.DeclaredOnly
                )
                .Where(f => f.IsInitOnly && f.FieldType == typeof(Register))
                .Select(f => (Register)f.GetValue(null)!);
            
            public readonly byte Address;
            
            private byte[] _Value;
            public byte[] Value 
            { 
                get => _Value; 
                set 
                {
                    if (_Value is not null && _Value.Length != Size)
                    {
                        Logger.Log(
                            LogLevel.Error,
                            $"i2c1.lps25hbtr: Register {Address:X} was attempted to be written to with incorrect length. " 
                                + $"Expected value.Length = {Size}, got value.Length = {value.Length}."
                        );
                        
                        return;
                    }
                    
                    _Value = value;
                }
            }
            
            public readonly int Size;
            
            public string Name { get; init; }
            
            public Register(byte address, int size, byte[] value, string name) 
            {
                Address = address;
                Size = size;
                Value = value;
                Name = name;
            }
            
            public override string ToString() => $"{Name} (0x{Address:X2})";
        }
        
        private const byte Address = 89;
        
        private const double TemperatureCelsiusToNative = 480.0;
        private const double TemperatureOffset = 42.5;
        
        private const double PressureHectopascalsToNative = 4096.0;
        
        private const double PressureOffsetHectopascalsToNative = 16.0;
        // Represents error in sensor. Must be corrected by writing to the PressureOffset register.
        private const double ConstPressureOffset = 23.5 + 80.0 / PressureOffsetHectopascalsToNative;
        
        /** Temperature measured by the device. */
        public double Temperature
        { 
            get => BytesToTemperature(Register.Temperature.Value);
            set => Register.Temperature.Value = TemperatureToBytes(value);
        }
        
        /** Represents the pressure measured by the device. */
        public double Pressure
        {
            get => BytesToPressure(Register.Pressure.Value);
            set => Register.Pressure.Value = PressureToBytes(value);
        }
        
        /** Represents the true pressure. */
        public double RealPressure 
        {
            get => Pressure + PressureOffset + ConstPressureOffset;
            set => Pressure = value - PressureOffset - ConstPressureOffset;
        }
        
        /** On the LPS25HBTR, this value is subtracted from the measured pressure. It is used for calibration purposes.  */
        public double PressureOffset
        {
            get => BytesToPressureOffset(Register.PressureOffset.Value);
            set
            {
                double real = RealPressure;
                Register.PressureOffset.Value = PressureOffsetToBytes(value);
                RealPressure = real;
            }
        }
        
        private readonly LinkedList<byte> _deque = new LinkedList<byte>();
        
        private Register _PendingReadWrite = null;
        
        public LPS25HBTR()
        {
            Temperature = 15.0;
            RealPressure = 1013.25;
            PressureOffset = -80.0 / PressureOffsetHectopascalsToNative;
        }

        public byte[] Read(int count = 1)
        {
            if (_PendingReadWrite is null)
            {
                this.ErrorLog("No register requested to read");
                
                return Array.Empty<byte>();
            }
            
            AddBytes(_PendingReadWrite.Value);
            _PendingReadWrite = null;
            
            // 1. Guard against invalid count requests
            if (count < 0)
            {
                this.ErrorLog("Read count cannot be negative.");
                return Array.Empty<byte>();
            }

            // 2. Check if the deque has enough items, warning the user if it doesn't
            if (count > _deque.Count)
            {
                this.WarningLog(
                    $"Requested {count} bytes, but only {_deque.Count} bytes are available."
                        + "Returning partial data."
                );
                // Cap the count to what is actually available to avoid crashes
                count = _deque.Count;
            }

            // 3. Extract the items from the front of the deque
            byte[] result = new byte[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = _deque.First.Value;
                _deque.RemoveFirst(); // Pop from the front
            }
            
            return result;
        }

        public void Write(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            
            if (_PendingReadWrite is not null)
            {
                this.InfoLog($"Written Bytes: {FormatArray(data)}");
                if (_PendingReadWrite.Address == Register.PressureOffset.Address)
                {
                    // Pressure bytes must be updated when offset is changed.
                    double real = RealPressure;
                    _PendingReadWrite.Value = data;
                    RealPressure = real;
                }
                else
                {
                    _PendingReadWrite.Value = data;
                }
                
                _PendingReadWrite = null;
            }
            else
            {
                byte addr = (byte)(data[0] & 0x7F);
                this.InfoLog($"Written Bytes: {FormatArray(data)}, Address: 0x{addr:X2}");
                
                Register? register = Register.Values.FirstOrDefault(reg => reg.Address == addr);
                if (register is null)
                {
                    this.ErrorLog($"Unrecognized address: 0x{addr:X2}");
                    
                    return;
                }
                
                this.InfoLog($"Requested Register: {register}. {register.Name}.Value = {FormatArray(register.Value)}");
                _PendingReadWrite = register;
            }
        }

        public void FinishTransmission() {}

        public void Reset() {}
        
        private byte[] TemperatureToBytes(double temperature)
        {
            Int16 native = (Int16)((temperature - TemperatureOffset) * TemperatureCelsiusToNative);
            
            return BitConverter.GetBytes(native);
        }
        
        private double BytesToTemperature(byte[] bytes)
        {
            if (bytes.Length != 2)
            {
                this.ErrorLog($"BytesToTemperature: byte array must be of length 2. bytes.Length = {bytes.Length}");
                
                return 0.0;
            }
            
            Int16 native = BitConverter.ToInt16(bytes);
            return native / TemperatureCelsiusToNative + TemperatureOffset;
        }
        
        private byte[] PressureToBytes(double pressure) 
        {
            Int32 native = (Int32)(pressure * PressureHectopascalsToNative);
            
            byte[] bytes = BitConverter.GetBytes(native)[0..3];
            
            return bytes;
        }
        
        private double BytesToPressure(byte[] bytes)
        {
            if (bytes.Length != 3) 
            {
                this.ErrorLog($"BytesToPressure: byte array must be of length 3. bytes.Length = {bytes.Length}");
                
                return 0.0;
            }
            
            byte[] fourBytes = new byte[] { bytes[0], bytes[1], bytes[2], 0x00 };
                
            UInt32 native = BitConverter.ToUInt32(fourBytes) & 0x00FFFFFF;
            
            return native / PressureHectopascalsToNative;
        }
        
        private byte[] PressureOffsetToBytes(double offset)
        {
            Int16 native = (Int16)(offset * PressureOffsetHectopascalsToNative);
            
            return BitConverter.GetBytes(native);
        }
        
        private double BytesToPressureOffset(byte[] bytes)
        {
            if (bytes.Length != 2) 
            {
                this.ErrorLog($"BytesToPressureOffsetNative: byte array must be of length 2. bytes.Length: {bytes.Length}");
                
                return 0;
            }
            
            Int16 native = BitConverter.ToInt16(bytes);
            
            return native / PressureOffsetHectopascalsToNative;
        }
        
        private void AddBytes(byte[] bytes) { foreach (byte b in bytes) _deque.AddLast(b); }
        
        private static String FormatArray(byte[] bytes) {
            StringBuilder sb = new StringBuilder();
            
            sb.Append("[");
            for (int i = 0; i < bytes.Length; i++) 
            {
                if (i < bytes.Length - 1) 
                    sb.Append($"0x{bytes[i]:X2}, ");
                else 
                    sb.Append($"0x{bytes[i]:X2}");
            }
            sb.Append(']');
            
            return sb.ToString();
        }
    }
}
