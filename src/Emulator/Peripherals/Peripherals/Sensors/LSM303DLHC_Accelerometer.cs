//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class LSM303DLHC_Accelerometer : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ISensor
    {
        public LSM303DLHC_Accelerometer()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            IRQ0 = new GPIO();
            IRQ1 = new GPIO();
            irqs[0] = IRQ0;
            irqs[1] = IRQ1;
            DefineRegisters();
        }

        public void FinishTransmission()
        {
            registerAddress = 0;
            multipleOperation = false;
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            multipleOperation = false;
            registerAddress = 0;
            IRQ0.Unset();
            IRQ1.Unset();
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Unexpected write with no data");
                return;
            }

            this.Log(LogLevel.Noisy, "Write with {0} bytes of data", data.Length);
            // Bits 6-0 represent the first register address
            registerAddress = (Registers)(data[0] & 0x7F);
            // The most significant bit means multiple read/write operation
            multipleOperation = data[0] > 0x80;

            if(data.Length > 1)
            {
                // skip the first byte as it contains register address
                foreach(var b in data.Skip(1))
                {
                    this.Log(LogLevel.Noisy, "Writing 0x{0:X} to register {1} (0x{1:X})", b, registerAddress);
                    RegistersCollection.Write((byte)registerAddress, b);
                    RegistersAutoIncrement();
                }
            }
            else
            {
                this.Log(LogLevel.Noisy, "Preparing to read register {0} (0x{0:X})", registerAddress);
                if(dataRate.Value == 0)
                {
                    this.Log(LogLevel.Debug, "Power-down mode is set");
                    xyzDataAvailable.Value = false;
                    xDataAvailable.Value = false;
                    yDataAvailable.Value = false;
                    zDataAvailable.Value = false;
                }
                else
                {
                    if(xAxisEnable.Value && yAxisEnable.Value && zAxisEnable.Value)
                    {
                        xyzDataAvailable.Value = true;
                        xDataAvailable.Value = false;
                        yDataAvailable.Value = false;
                        zDataAvailable.Value = false;
                    }
                    else
                    {
                        xyzDataAvailable.Value = false;
                        xDataAvailable.Value = xAxisEnable.Value;
                        yDataAvailable.Value = yAxisEnable.Value;
                        zDataAvailable.Value = zAxisEnable.Value;
                    }
                }
                UpdateInterrupts();
            }
        }

        public byte[] Read(int count)
        {
            this.Log(LogLevel.Debug, "Reading {0} bytes from register {1} (0x{1:X})", count, registerAddress);
            var result = new byte[count];
            for(var i = 0; i < result.Length; i++)
            {
                result[i] = RegistersCollection.Read((byte)registerAddress);
                this.Log(LogLevel.Noisy, "Read value: {0}", result[i]);
                RegistersAutoIncrement();
            }
            return result;
        }

        public decimal AccelerationX
        {
            get => accelarationX;
            set
            {
                if(!IsAccelerationOutOfRange(value))
                {
                    accelarationX = value;
                    this.Log(LogLevel.Noisy, "AccelerationX set to {0}", accelarationX);
                }
            }
        }

        public decimal AccelerationY
        {
            get => accelarationY;
            set
            {
                if(!IsAccelerationOutOfRange(value))
                {
                    accelarationY = value;
                    this.Log(LogLevel.Noisy, "AccelerationY set to {0}", accelarationY);
                }
            }
        }

        public decimal AccelerationZ
        {
            get => accelarationZ;
            set
            {
                if(!IsAccelerationOutOfRange(value))
                {
                    accelarationZ = value;
                    this.Log(LogLevel.Noisy, "AccelerationZ set to {0}", accelarationZ);
                }
            }
        }

        public ByteRegisterCollection RegistersCollection { get; }
        public GPIO IRQ0 { get; }
        public GPIO IRQ1 { get; }

        private void DefineRegisters()
        {
            Registers.ChipID.Define(this, 0x33);

            Registers.Control1.Define(this, 0x43) //RW
                .WithFlag(0, out xAxisEnable, name: "X_AXIS_ENABLE")
                .WithFlag(1, out yAxisEnable, name: "Y_AXIS_ENABLE")
                .WithFlag(2, out zAxisEnable, name: "Z_AXIS_ENABLE")
                .WithTaggedFlag("LOW_POWER_ENABLE", 3)
                .WithValueField(4, 4, out dataRate, name: "DATA_RATE");

            Registers.Control4.Define(this) //RW
                .WithTaggedFlag("SIM", 0)
                .WithTag("PREREQ", 1, 2)
                .WithTaggedFlag("HR", 3)
                .WithValueField(4, 2, out fullScale, name: "FS")
                .WithTaggedFlag("BLE", 6)
                .WithTaggedFlag("BDU", 7);

            Registers.StatusReg.Define(this, 0x08) //RO
                .WithFlag(0, out xDataAvailable, FieldMode.Read, name: "X_DATA_AVAILABLE")
                .WithFlag(1, out yDataAvailable, FieldMode.Read, name: "Y_DATA_AVAILABLE")
                .WithFlag(2, out zDataAvailable, FieldMode.Read, name: "Z_DATA_AVAILABLE")
                .WithFlag(3, out xyzDataAvailable, FieldMode.Read, name: "XYZ_DATA_AVAILABLE")
                .WithTaggedFlag("X_DATA_OVERRUN", 4)
                .WithTaggedFlag("Y_DATA_OVERRUN", 5)
                .WithTaggedFlag("Z_DATA_OVERRUN", 6)
                .WithTaggedFlag("XYZ_DATA_OVERRUN", 7);

            Registers.DataOutXL.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "X_ACCEL_DATA[7:0]", valueProviderCallback: _ => Convert(AccelerationX, upperByte: false));

            Registers.DataOutXH.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "X_ACCEL_DATA[15:8]", valueProviderCallback: _ => Convert(AccelerationX, upperByte: true));

            Registers.DataOutYL.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Y_ACCEL_DATA[7:0]", valueProviderCallback: _ => Convert(AccelerationY, upperByte: false));

            Registers.DataOutYH.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Y_ACCEL_DATA[15:8]", valueProviderCallback: _ => Convert(AccelerationY, upperByte: true));

            Registers.DataOutZL.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Z_ACCEL_DATA[7:0]", valueProviderCallback: _ => Convert(AccelerationZ, upperByte: false));

            Registers.DataOutZH.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Z_ACCEL_DATA[15:8]", valueProviderCallback: _ => Convert(AccelerationZ, upperByte: true));

            Registers.Interrupt1Config.Define(this, 0x3F) //RW
                .WithFlag(0, out readyEnabledXIrq[0, 0], name: "XL_EVENT_IRQ_ENABLE")
                .WithFlag(1, out readyEnabledXIrq[0, 1], name: "XH_EVENT_IRQ_ENABLE")
                .WithFlag(2, out readyEnabledYIrq[0, 0], name: "YL_EVENT_IRQ_ENABLE")
                .WithFlag(3, out readyEnabledYIrq[0, 1], name: "YH_EVENT_IRQ_ENABLE")
                .WithFlag(4, out readyEnabledZIrq[0, 0], name: "ZL_EVENT_IRQ_ENABLE")
                .WithFlag(5, out readyEnabledZIrq[0, 1], name: "ZH_EVENT_IRQ_ENABLE")
                .WithTaggedFlag("6DIR_DETECT_ENABLE", 6)
                .WithTaggedFlag("AO_IRQ_EVENTS", 7)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.Interrupt1Source.Define(this, 0x40) //RO
                .WithFlag(0, out readyPendingXIrq[0, 0], FieldMode.Read, name: "XL_IRQ")
                .WithFlag(1, out readyPendingXIrq[0, 1], FieldMode.Read, name: "XH_IRQ")
                .WithFlag(2, out readyPendingYIrq[0, 0], FieldMode.Read, name: "YL_IRQ")
                .WithFlag(3, out readyPendingYIrq[0, 1], FieldMode.Read, name: "YH_IRQ")
                .WithFlag(4, out readyPendingZIrq[0, 0], FieldMode.Read, name: "ZL_IRQ")
                .WithFlag(5, out readyPendingZIrq[0, 1], FieldMode.Read, name: "ZH_IRQ")
                .WithFlag(6, out activeIrq[0] , FieldMode.Read, name: "IRQ_ACTIVE")
                .WithTaggedFlag("PREREQ", 7);

            Registers.Interrupt2Config.Define(this, 0x3F) //RW
                .WithFlag(0, out readyEnabledXIrq[1, 0], name: "XL_EVENT_IRQ_ENABLE")
                .WithFlag(1, out readyEnabledXIrq[1, 1], name: "XH_EVENT_IRQ_ENABLE")
                .WithFlag(2, out readyEnabledYIrq[1, 0], name: "YL_EVENT_IRQ_ENABLE")
                .WithFlag(3, out readyEnabledYIrq[1, 1], name: "YH_EVENT_IRQ_ENABLE")
                .WithFlag(4, out readyEnabledZIrq[1, 0], name: "ZL_EVENT_IRQ_ENABLE")
                .WithFlag(5, out readyEnabledZIrq[1, 1], name: "ZH_EVENT_IRQ_ENABLE")
                .WithTaggedFlag("6DIR_DETECT_ENABLE", 6)
                .WithTaggedFlag("AO_IRQ_EVENTS", 7)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.Interrupt2Source.Define(this, 0x40) //RO
                .WithFlag(0, out readyPendingXIrq[1, 0], FieldMode.Read, name: "XL_IRQ")
                .WithFlag(1, out readyPendingXIrq[1, 1], FieldMode.Read, name: "XH_IRQ")
                .WithFlag(2, out readyPendingYIrq[1, 0], FieldMode.Read, name: "YL_IRQ")
                .WithFlag(3, out readyPendingYIrq[1, 1], FieldMode.Read, name: "YH_IRQ")
                .WithFlag(4, out readyPendingZIrq[1, 0], FieldMode.Read, name: "ZL_IRQ")
                .WithFlag(5, out readyPendingZIrq[1, 1], FieldMode.Read, name: "ZH_IRQ")
                .WithFlag(6, out activeIrq[1], FieldMode.Read, name: "IRQ_ACTIVE")
                .WithTaggedFlag("PREREQ", 7);
        }

        private void RegistersAutoIncrement()
        {
            if(multipleOperation)
            {
                registerAddress = (Registers)((int)registerAddress + 1);
                this.Log(LogLevel.Noisy, "Auto-incrementing to the next register 0x{0:X} - {0}", registerAddress);
            }
        }

        private ushort GetSensitivity()
        {
            ushort sensitivity = 0; // [mg/LSB]
            switch(fullScale.Value)
            {
                case 0:
                    sensitivity = 1;
                    break;
                case 1:
                    sensitivity = 2;
                    break;
                case 2:
                    sensitivity = 4;
                    break;
                case 3:
                    sensitivity = 12;
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unsupported value of sensor sensitivity.");
                    break;
            }
            return sensitivity;
        }

        private bool IsAccelerationOutOfRange(decimal acceleration)
        {
            // This range protects from the overflow of the short variables in the 'Convert' function.
            if(acceleration < MinAcceleration || acceleration > MaxAcceleration)
            {
                this.Log(LogLevel.Warning, "Acceleration is out of range, use value from the range <{0};{1}>",
                                            MinAcceleration, MaxAcceleration);
                return true;
            }
            return false;
        }

        private byte Convert(decimal value, bool upperByte)
        {
            decimal convertedValue = (decimal)(((short)value << 14) / (GetSensitivity() * GravitationalConst));
            short convertedValueAsShort = (short)convertedValue;
            return upperByte ? (byte)(convertedValueAsShort >> 8) : (byte)convertedValueAsShort;
        }

        private void UpdateInterrupts()
        {
            var statusIrq = false;
            var statusX = xDataAvailable.Value || xyzDataAvailable.Value;
            var statusY = yDataAvailable.Value || xyzDataAvailable.Value;
            var statusZ = yDataAvailable.Value || xyzDataAvailable.Value;
            for(var irqNo = 0; irqNo < IrqAmount; ++irqNo)
            {
                for(var i = 0; i < AxisBytes; ++i)
                {
                    if(activeIrq[irqNo].Value)
                    {
                        readyPendingXIrq[irqNo, i].Value = statusX && readyEnabledXIrq[irqNo, i].Value;
                        this.Log(LogLevel.Noisy, "Setting readyPendingXIrq[{0}, {1}] to {2}",
                                irqNo, i, readyPendingXIrq[irqNo, i].Value);

                        readyPendingYIrq[irqNo, i].Value = statusY && readyEnabledYIrq[irqNo, i].Value;
                        this.Log(LogLevel.Noisy, "Setting readyPendingYIrq[{0}, {1}] to {2}",
                                irqNo, i, readyPendingYIrq[irqNo, i].Value);

                        readyPendingZIrq[irqNo, i].Value = statusZ && readyEnabledZIrq[irqNo, i].Value;
                        this.Log(LogLevel.Noisy, "Setting readyPendingZIrq[{0}, {1}] to {2}",
                                irqNo, i, readyPendingZIrq[irqNo, i].Value);

                        statusIrq = (readyPendingXIrq[irqNo, i].Value ||
                                      readyPendingYIrq[irqNo, i].Value ||
                                      readyPendingZIrq[irqNo, i].Value);
                    }
                }

                irqs[irqNo].Set(statusIrq);
                this.Log(LogLevel.Debug, "Setting IRQ{0} to {1}", irqNo, statusIrq);
            }
        }

        private Registers registerAddress;
        private bool multipleOperation;

        private IValueRegisterField dataRate;
        private IValueRegisterField fullScale;
        private IFlagRegisterField xyzDataAvailable;
        private IFlagRegisterField xAxisEnable, yAxisEnable, zAxisEnable;
        private IFlagRegisterField xDataAvailable, yDataAvailable, zDataAvailable;

        private GPIO[] irqs = new GPIO[IrqAmount];
        private IFlagRegisterField[] activeIrq = new IFlagRegisterField[IrqAmount];
        private IFlagRegisterField[,] readyEnabledXIrq = new IFlagRegisterField[IrqAmount, AxisBytes];
        private IFlagRegisterField[,] readyEnabledYIrq = new IFlagRegisterField[IrqAmount, AxisBytes];
        private IFlagRegisterField[,] readyEnabledZIrq = new IFlagRegisterField[IrqAmount, AxisBytes];
        private IFlagRegisterField[,] readyPendingXIrq = new IFlagRegisterField[IrqAmount, AxisBytes];
        private IFlagRegisterField[,] readyPendingYIrq = new IFlagRegisterField[IrqAmount, AxisBytes];
        private IFlagRegisterField[,] readyPendingZIrq = new IFlagRegisterField[IrqAmount, AxisBytes];

        private decimal accelarationX;
        private decimal accelarationY;
        private decimal accelarationZ;

        private const decimal MinAcceleration = -19.0m;
        private const decimal MaxAcceleration = 19.0m;
        private const decimal GravitationalConst = 9.806650m; // [m/s^2]
        private const ushort AxisBytes = 2;
        private const ushort IrqAmount = 2;

        private enum Registers : byte
        {
            ChipID = 0x0F,
            // Reserved: 0x00 - 0x1F
            Control1 = 0x20,
            Control4 = 0x23,
            StatusReg = 0x27,
            DataOutXL = 0x28,
            DataOutXH = 0x29,
            DataOutYL = 0x2A,
            DataOutYH = 0x2B,
            DataOutZL = 0x2C,
            DataOutZH = 0x2D,
            Interrupt1Config = 0x30,
            Interrupt1Source = 0x31,
            Interrupt2Config = 0x34,
            Interrupt2Source = 0x35,
            // Reserved: 0x3E - 0x3F
        }
    }
}
