//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class LC709205F : II2CPeripheral, IProvidesRegisterCollection<WordRegisterCollection>
    {
        public LC709205F(IMachine machine)
        {
            this.machine = machine;
            RegistersCollection = new WordRegisterCollection(this);
            DefineRegisters();
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Unexpected write with no data");
                return;
            }

            var offset = registerAddress == null ? 1 : 0;
            registerAddress = (Registers)data[0];
            if(data.Length > offset)
            {
                foreach(var item in data.Skip(offset).Select((value, index) => new { index, value }))
                {
                    WriteWithCRC(registerAddress.Value, item.index, item.value);
                }
            }
        }

        public byte[] Read(int count = 1)
        {
            if(!registerAddress.HasValue)
            {
                this.Log(LogLevel.Error, "Trying to read without setting address");
                return new byte[] {};
            }

            var result = new byte[count];
            for(var i = 0; i < count; ++i)
            {
                result[i] = ReadWithCRC(registerAddress.Value, i);
            }
            return result;
        }

        public void FinishTransmission()
        {
            registerAddress = null;
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            registerAddress = null;
        }

        public uint TimeToEmpty { get; set; }
        public uint TimeToFull { get; set; }
        public uint CellVoltage { get; set; }
        public uint CycleCount { get; set; }

        public decimal CellTemperature
        {
            get => (cellTemperature * TemperatureSensitivity) - CelciusToKelvin;
            set => cellTemperature = (uint)((value + CelciusToKelvin) / TemperatureSensitivity).Clamp(0m, uint.MaxValue);
        }

        public decimal AmbientTemperature
        {
            get => (ambientTemperature * TemperatureSensitivity) - CelciusToKelvin;
            set => ambientTemperature = (uint)((value + CelciusToKelvin) / TemperatureSensitivity).Clamp(0m, uint.MaxValue);
        }

        public decimal RelativeStateOfCharge { get; set; }

        public decimal FullChargeCapacity
        {
            get => fullChargeCapacity * CapacitySensitivity;
            set => fullChargeCapacity = (uint)(value / CapacitySensitivity).Clamp(0, uint.MaxValue);
        }

        public decimal DesignCapacity
        {
            get => designCapacity * CapacitySensitivity;
            set => designCapacity = (uint)(value / CapacitySensitivity).Clamp(0, uint.MaxValue);
        }

        public decimal RemainingCapacity
        {
            get => remainingCapacity * CapacitySensitivity;
            set => remainingCapacity = (uint)(value / CapacitySensitivity).Clamp(0, uint.MaxValue);
        }

        public WordRegisterCollection RegistersCollection { get; }

        private byte ReadWithCRC(Registers register, int offset)
        {
            var addressOffset = offset / 3;
            var result = RegistersCollection.Read((byte)((int)register + addressOffset));
            var step = (TransactionStep)(offset % 3);
            switch(step)
            {
                // First two bytes are the actual value
                case TransactionStep.LSB:
                case TransactionStep.MSB:
                    return step == TransactionStep.MSB ? (byte)(result >> 8) : (byte)result;
                // Third byte is CRC of the value
                case TransactionStep.CRC:
                    var crc = CalculateCrc8(new byte[] {(byte)(SlaveAddress << 1), (byte)register, (byte)((SlaveAddress << 1) + 1), (byte)result, (byte)(result >> 8)});
                    return (byte)crc;
                default:
                    throw new Exception("unreachable state");
            }
        }

        private void WriteWithCRC(Registers register, int offset, byte value)
        {
            var step = (TransactionStep)(offset % 3);
            if(step == TransactionStep.CRC)
            {
                // We are deliberately omitting checking CRC, as the only possibility for
                // it to be invalid, is when software generate wrong checksum for written data.
                return;
            }
            var addressOffset = offset / 3;
            var realAddress = (byte)((int)register + addressOffset);
            var underlyingValue = RegistersCollection.Read(realAddress);
            if(step == TransactionStep.MSB)
            {
                underlyingValue &= 0x00FF;
                underlyingValue |= (ushort)((short)value << 8);
            }
            else
            {
                underlyingValue &= 0xFF00;
                underlyingValue |= (ushort)value;
            }
            RegistersCollection.Write(realAddress, underlyingValue);
        }

        private byte CalculateCrc8(byte[] data)
        {
            var result = 0x00;
            foreach(var b in data)
            {
                result = (byte)(result ^ b);
                for(var i = 0; i < 8; ++i)
                {
                    if((result & 0x80) != 0x00)
                    {
                        // Polynomial x^8+x^2+x+1
                        result = (byte)((result << 1) ^ 0x07);
                    }
                    else
                    {
                        result = (byte)(result << 1);
                    }
                }
            }
            return (byte)result;
        }

        private void DefineRegisters()
        {
            Registers.TimeToEmpty.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "TimeToEmpty",
                    valueProviderCallback: _ => TimeToEmpty)
            ;

            Registers.TimeToFull.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "TimeToFull",
                    valueProviderCallback: _ => TimeToFull)
            ;

            Registers.CellTemperature.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "CellTemperature",
                    valueProviderCallback: _ => cellTemperature)
            ;

            Registers.CellVoltage.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "CellVoltage",
                    valueProviderCallback: _ => CellVoltage)
            ;

            Registers.RelativeStateOfCharge.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "RSOC",
                    valueProviderCallback: _ => (uint)RelativeStateOfCharge.Clamp(0, 100))
            ;

            Registers.IndicatorToEmpty.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "ITE",
                    valueProviderCallback: _ => (uint)(RelativeStateOfCharge * 10.0m).Clamp(0, 1000))
            ;

            Registers.FullChargeCapacity.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "FullChargeCapacity",
                    valueProviderCallback: _ => fullChargeCapacity)
            ;

            Registers.ICPowerMode.Define(this, 0x01)
                .WithValueField(0, 16, name: "ICPowerMode")
            ;

            Registers.CycleCount.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "CycleCount",
                    valueProviderCallback: _ => CycleCount)
            ;

            Registers.DesignCapacity.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "DesignCapacity",
                    valueProviderCallback: _ => designCapacity)
            ;

            Registers.NumberOfTheParameters.Define(this, 0x1001)
                .WithTag("NumberOfTheParameters", 0, 16)
            ;

            Registers.AmbientTemperature.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "AmbientTemperature",
                    valueProviderCallback: _ => ambientTemperature)
            ;

            Registers.RemainingCapacity.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "RemainingCapacity",
                    valueProviderCallback: _ => remainingCapacity)
            ;
        }

        private int SlaveAddress
        {
            get
            {
                if(!slaveAddress.HasValue)
                {
                    var parentPeripheral = machine.GetParentPeripherals(this).FirstOrDefault();
                    if(parentPeripheral == null)
                    {
                        this.Log(LogLevel.Warning, "Peripheral hasn't been connected to any I2C controller");
                        return DefaultSlaveAddress;
                    }
                    var registrationPoint = machine.GetPeripheralRegistrationPoints(parentPeripheral, this).FirstOrDefault();
                    if(registrationPoint == null)
                    {
                        throw new Exception("could not find registration point for this peripheral");
                    }

                    if(registrationPoint is NumberRegistrationPoint<int> numberRegistrationPoint)
                    {
                        slaveAddress = numberRegistrationPoint.Address;
                    }
                    else if(registrationPoint is TypedNumberRegistrationPoint<int> typedNumberRegistrationPoint)
                    {
                        slaveAddress = typedNumberRegistrationPoint.Address;
                    }
                    else
                    {
                        throw new Exception($"SlaveAddress is unimplemented for {registrationPoint.GetType().ToString()}");
                    }
                }
                return slaveAddress.Value;
            }
        }

        private const decimal CelciusToKelvin = 273.15m;
        private const decimal CapacitySensitivity = 0.1m;
        private const decimal TemperatureSensitivity = 0.1m;
        private const int DefaultSlaveAddress = 0x0B;

        private readonly IMachine machine;

        private Registers? registerAddress;

        private int? slaveAddress;

        private uint fullChargeCapacity;
        private uint designCapacity;
        private uint remainingCapacity;
        private uint cellTemperature;
        private uint ambientTemperature;

        private enum TransactionStep
        {
            LSB,
            MSB,
            CRC
        }

        private enum Registers
        {
            Prohibited1 = 0x00,
            Prohibited2 = 0x01,
            // Reserved (0x02)
            TimeToEmpty = 0x03,
            BeforeRelativeStateOfCharge = 0x04,
            TimeToFull = 0x05,
            TSense1TherimistorB = 0x06,
            InitialRelativeStateOfCharge = 0x07,
            CellTemperature = 0x08,
            CellVoltage = 0x09,
            CurrentDirection = 0x0A,
            AdjustmentPackApplication = 0x0B,
            AdjustmentPackThermistor = 0x0C,
            RelativeStateOfCharge = 0x0D,
            TSense2ThermistorB = 0x0E,
            IndicatorToEmpty = 0x0F,
            FullChargeCapacity = 0x10,
            ICVersion = 0x11,
            ChangeOfTheParameter = 0x12,
            AlarmLowRelativeStateOfCharge = 0x13,
            AlarmLowCellVoltage = 0x14,
            ICPowerMode = 0x15,
            StatusBit = 0x16,
            CycleCount = 0x17,
            DesignCapacity = 0x18,
            BatteryStatus = 0x19,
            NumberOfTheParameters = 0x1A,
            // Reserved (0x1B)
            TerminationCurrentRate = 0x1C,
            EmptyCellVoltage = 0x1D,
            ITEOffset = 0x1E,
            AlarmHighCellVoltage = 0x1F,
            AlarmLowTemperature = 0x20,
            AlarmHighTemperature = 0x21,
            AlarmOverChargingCurrent = 0x22,
            AlarmOverDischargingCurrent = 0x23,
            TotalRuntimeLow = 0x24,
            TotalRuntimeHigh = 0x25,
            AccumulatedTemperatureLow = 0x26,
            AccumulatedTemperatureHigh = 0x27,
            AccumulatedRelativeStateOfChargeLow = 0x28,
            AccumulatedRelativeStateOfChargeHigh = 0x29,
            MaximumCellVoltage = 0x2A,
            MinimumCellVoltage = 0x2B,
            MaximumCellTemperature = 0x2C,
            MinimumCellTemperature = 0x2D,
            MaximumCellCurrent = 0x2E,
            MinimumCellCurrent = 0x2F,
            AmbientTemperature = 0x30,
            SenseResistance = 0x31,
            StateOfHealth = 0x32,
            DynamicCellCurrent = 0x33,
            AverageCellCurrent = 0x34,
            RemainingCapacity = 0x35,
            UserIDLow = 0x36,
            UserIDHigh = 0x37
        }
    }
}
