//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class ZMOD4xxx : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ISensor
    {
        public ZMOD4xxx(Model model)
        {
            this.model = model;
            /* Below configurations are not a property of the sensor, but a fields that can vary between units.
               Those are just one of the possible configurations that are proved to work */
            this.configuration = new byte[]{0x80,0x80,0x80,0x80,0x80,0x80};
            switch(model)
            {
                case Model.ZMOD4410:
                    productId = zmod4410_productId;
                    this.productionData = new byte[]{0x2D, 0xCF, 0x46, 0x29, 0x04, 0xB4};
                    this.initConfigurationRField = new byte[] { 0x21, 0x48, 0x3B, 0xAE, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
                    this.rField = new byte[] { 0x15, 0x48, 0xBF, 0x92, 0x8D, 0x59, 0xF2, 0x73, 0x42, 0xB5, 0x98, 0x1E, 0x8C, 0x09,
                                               0x71, 0xDB, 0x51, 0x40, 0x64, 0x58, 0x4E, 0xBE, 0x14, 0xDF, 0xB7, 0xA2, 0x86, 0x9D,
                                               0x4B, 0xB4, 0x02, 0x8D };
                    break;
                case Model.ZMOD4510:
                    productId = zmod4510_productId;
                    this.productionData = new byte[ProductionDataLengthInBytes];
                    this.initConfigurationRField = new byte[] { 0x2A, 0xFC, 0xF3, 0xDF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                                                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                    this.rField = new byte[] { 0x15, 0x6A, 0xBF, 0xC0, 0x1B, 0x48, 0x63, 0xA0, 0x84, 0xE8, 0xBF, 0x5C, 0x88, 0x18,
                                               0x7E, 0xF4, 0x3F, 0x64, 0xCC, 0x47, 0xBC, 0x8A, 0x3C, 0x59, 0x33, 0xB8, 0x75, 0x88,
                                               0x2C, 0x67, 0xC7, 0x5E };
                    break;
                default:
                    throw new ConstructionException($"This model ({model}) is not supported");
            }

            RegistersCollection = new ByteRegisterCollection(this);

            IRQ = new GPIO();

            DefineRegisters();
            Reset();
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.DebugLog("Empty write. Ignoring");
                return;
            }
            currentRegister = data[0];
            this.DebugLog("Address set to 0x{0:X} [{1}]", currentRegister, (Registers)currentRegister);
            foreach(var b in data.Skip(1))
            {
                this.DebugLog("Writing 0x{0:x} to addr 0x{1:x}", b, currentRegister);
                // Using TryWrite to avoid logs on unhandled writes
                RegistersCollection.TryWrite(currentRegister, b);
                currentRegister += 1;
            }
        }

        public byte[] Read(int count)
        {
            var response = new byte[count];
            for(var index = 0; index < count; index++)
            {
                // Using TryRead to avoid logs on unhandled reads
                RegistersCollection.TryRead(currentRegister, out response[index]);
                this.DebugLog("Read 0x{0:x} from addr 0x{1:x}", response[index], currentRegister);
                currentRegister++;
            }
            return response;
        }

        public void FinishTransmission()
        {
            this.DebugLog("Finished transmission");
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            sensorInMeasureMode = false;
            currentRegister = 0;
        }

        public String RValue
        {
            get
            {
                return Misc.Stringify(rField);
            }
            set
            {
                if(!TryParseHexStringWithAssertedLength(value, ResultLengthInBytes, out rField, out var err))
                {
                    throw new RecoverableException(err);
                }
            }
        }

        public String InitConfigurationRValue
        {
            get
            {
                return Misc.Stringify(initConfigurationRField);
            }
            set
            {
                if(!TryParseHexStringWithAssertedLength(value, ResultLengthInBytes, out initConfigurationRField, out var err))
                {
                    throw new RecoverableException(err);
                }
            }
        }

        public String Configuration
        {
            get
            {
                return Misc.Stringify(configuration);
            }
            set
            {
                if(!TryParseHexStringWithAssertedLength(value, ConfigurationLengthInBytes, out configuration, out var err))
                {
                    throw new RecoverableException(err);
                }
            }
        }

        public String ProductionData
        {
            get
            {
                return Misc.Stringify(productionData);
            }
            set
            {
                if(!TryParseHexStringWithAssertedLength(value, ProductionDataLengthInBytes, out productionData, out var err))
                {
                    throw new RecoverableException(err);
                }
            }
        }

        public GPIO IRQ { get; }

        public ByteRegisterCollection RegistersCollection { get; }

        private bool TryParseHexStringWithAssertedLength(string hexstring, int expectedLenghtInBytes, out byte[] byteArray, out string err)
        {
            byteArray = new byte[expectedLenghtInBytes];
            err = "";

            if(hexstring.Length != (expectedLenghtInBytes * 2))
            {
                err = $"Wrong hexsting length. Expected {expectedLenghtInBytes} bytes";
                return false;
            }

            if(!Misc.TryParseHexString(hexstring, out byteArray, elementSize: 1))
            {
                err = "Unable to parse as a hexstring";
                return false;
            }
            return true;
        }

        private void EmulateMeasurementFinished()
        {
            // Normally the sensor sets this line high when starting the measurements, and then sets is low when finished.
            // We emulate the measurement as instantaneous
            IRQ.Blink();
        }

        private void DefineRegisters()
        {
            Registers.ProductID0.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => productId[0], name: "PID 0");

            Registers.ProductID1.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => productId[1], name: "PID 1");

            Registers.Configuration.DefineMany(this, ConfigurationLengthInBytes, (register, index) =>
            {
                register
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => configuration[index], name: $"Configuration{index}");
            });

            Registers.ProductionData.DefineMany(this, ProductionDataLengthInBytes, (register, index) =>
            {
                register
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => productionData[index], name: $"ProductionData{index}");
            });

            Registers.Tracking.DefineMany(this, TrackingLengthInBytes, (register, index) =>
            {
                register
                    .WithTag($"Tracking{index}", 0, 8);
            });

            Registers.H.DefineMany(this, HLengthInBytes, (register, index) =>
            {
                register
                    .WithTag($"H{index}", 0, 8);
            });

            Registers.D.DefineMany(this, DLengthInBytes, (register, index) =>
            {
                register
                    .WithTag($"D{index}", 0, 8);
            });

            Registers.M.DefineMany(this, MLengthInBytes, (register, index) =>
            {
                register
                    .WithTag($"M{index}", 0, 8)
                    .WithWriteCallback((_, val) =>
                    {
                        if(index == 0)
                        {
                            sensorInMeasureMode = (val != M0InitValue);
                            this.DebugLog("Sensor in measure state = {0}", sensorInMeasureMode);
                        }
                    });
            });

            Registers.S.DefineMany(this, SLengthInBytes, (register, index) =>
            {
                register
                    .WithTag($"S{index}", 0, 8);
            });

            Registers.Command.Define(this)
                .WithReservedBits(0, 7)
                .WithFlag(7, changeCallback: (_, val) => {
                    if(val)
                    {
                        this.DebugLog("Measurement trigerred");
                        EmulateMeasurementFinished();
                    }
                    else
                    {
                        this.DebugLog("Measurement stopped");
                    }
                }, name: "Start");

            Registers.Status0.Define(this)
                .WithTag("Last executed sequencer step", 0, 5)
                .WithTaggedFlag("Alarm", 5)
                .WithTaggedFlag("Sleep Timer Enabled", 6)
                .WithTaggedFlag("Sequencer Running", 7);

            Registers.Result.DefineMany(this, ResultLengthInBytes, (register, index) =>
            {
                register
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => sensorInMeasureMode ? rField[index] : initConfigurationRField[index], name: $"R{index}");
            });

            Registers.Error.Define(this)
                .WithReservedBits(0, 6)
                .WithTaggedFlag("Access Conflict", 6)
                .WithTaggedFlag("POR Event", 7);
        }

        private readonly Model model;

        private const int ResultLengthInBytes = 32;
        private const int ConfigurationLengthInBytes = 6;
        private const int ProductionDataLengthInBytes = 6;
        private const int TrackingLengthInBytes = 6;
        private const int HLengthInBytes = 10;
        private const int DLengthInBytes = 6;
        private const int MLengthInBytes = 1;
        private const int SLengthInBytes = 30;

        // There's no documentation for that, but this is what gets written in the init phase
        private const int M0InitValue = 0xC3;

        private readonly byte[] zmod4510_productId = new byte[] { 0x63, 0x20 };
        private readonly byte[] zmod4410_productId = new byte[] { 0x23, 0x10 };
        private readonly byte[] productId;
        private byte[] configuration;
        private byte[] productionData;
        private byte[] initConfigurationRField;
        private byte[] rField;

        private byte currentRegister;
        private bool sensorInMeasureMode;

        public enum Model
        {
            // This is also the address on the I2C bus
            ZMOD4510 = 0x33,
            ZMOD4410 = 0x32,
        }

        private enum Registers : byte
        {
            ProductID0 = 0x0,
            ProductID1 = 0x1,
            Configuration = 0x20,
            ProductionData = 0x26,
            Tracking = 0x3A,
            H = 0x40,
            D = 0x50,
            M = 0x60,
            S = 0x68,
            Command = 0x93,
            Status0 = 0x94,
            Result = 0x97,
            Error = 0xB7,
        }
    }
}
