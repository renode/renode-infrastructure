//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;


/* TODO:
- Burst read
- Status register
- Data registers
*/

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class BME280 : ISPIPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public BME280()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            dataFifo = new SensorSamplesFifo<Vector3DSample>();
            calibrationValues = new byte[42];
            DefineRegisters();
        }

        private enum ModeSelect : byte
        {
            sleep = 0x00,
            forced1 = 0x01,
            forced2 = 0x02,
            normal = 0x03,
        }
        private Register regAddress;
        private State state;
        private IValueRegisterField spi3w_en;
        private IValueRegisterField filter;
        private IValueRegisterField t_sb;
        private IValueRegisterField osrs_p;
        private IValueRegisterField osrs_t;
        private IValueRegisterField osrs_h;

        private IEnumRegisterField<ModeSelect> mode;

        private byte[] calibrationValues;
        /* Using a 3DSample of type x y z for the 3 sensors here:
         X: Pressure
         Y: Temperature
         Z: Humidity
         */
        private readonly SensorSamplesFifo<Vector3DSample> dataFifo;

        private enum State
        {
            Idle,
            Reading,
            Writing
        }
        public void Reset()
        {
            RegistersCollection.Reset();
            state = State.Idle;
            regAddress = 0;
        }

        public void FinishTransmission()
        {
            regAddress = 0;
            state = State.Idle;
        }

        public byte Transmit(byte data)
        {
            byte result = 0;

            switch(state)
            {
                case State.Idle:
                {
                    regAddress = (Register)(data | 0x80);
                    var isWrite = !BitHelper.IsBitSet(data, 7);

                    this.NoisyLog("Decoded register {0} (0x{0:X}) and isWrite bit as {1}", regAddress, isWrite);
                    state = isWrite
                        ? State.Writing
                        : State.Reading;
                    break;
                }

                case State.Reading:
                    this.NoisyLog("Reading register {0} (0x{0:X})", regAddress);
                    result = RegistersCollection.Read((long)regAddress);
                    RegistersAutoIncrement();
                    break;

                case State.Writing:
                    this.NoisyLog("Writing 0x{0:X} to register {1} (0x{1:X})", data, regAddress);
                    RegistersCollection.Write((long)regAddress, data);
                    RegistersAutoIncrement();
                    break;

                default:
                    this.Log(LogLevel.Error, "Received byte in an unexpected state: {0}", state);
                    break;
            }

            this.NoisyLog("Received byte 0x{0:X}, returning 0x{1:X}", data, result);
            return result;
        }

        private void RegistersAutoIncrement()
        {
            if(regAddress >= Register.Humidity_lsb && regAddress < Register.Id)
            {
                regAddress = (Register)((int)regAddress + 1);
                this.Log(LogLevel.Noisy, "Auto-incrementing to the next register 0x{0:X} - {0}", regAddress);
            }
        }

        private void LoadNextSample()
        {
            this.Log(LogLevel.Noisy, "Acquiring next sample");
            dataFifo.TryDequeueNewSample();
        }

        private void DefineRegisters()
        {
            Register.Id.Define(this, 0x60);
            Register.Status.Define(this, 0x00);

            /*TODO: Pull from FIFO instead of hardcoding */
            Register.Humidity_lsb.Define(this, 0x00)
            .WithValueField(0, 8, FieldMode.Read, name: "Humidity LSB output",
                    valueProviderCallback: _ => 
                    {
                        return (byte)(getHumidity() & 0xFF);
                    });

            Register.Humidity_msb.Define(this, 0x01)
            .WithValueField(0, 8, FieldMode.Read, name: "Humidity MSB output",
                    valueProviderCallback: _ => 
                    {
                        return (byte)((getHumidity() >> 8) & 0xFF);
                    });
            
            Register.Temperature_xlsb.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "Temperature XLSB output",
                    valueProviderCallback: _ => 
                    {
                        return (byte)((getTemperature() & 0x0F)<<4);
                    });
            
            Register.Temperature_lsb.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "Temperature LSB output",
                    valueProviderCallback: _ => 
                    {
                        return (byte)((getTemperature()>>4) & 0xFF);
                    });
            
            Register.Temperature_msb.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "Temperature MSB output",
                    valueProviderCallback: _ => 
                    {
                        return (byte)((getTemperature()>>12) & 0xFF);
                    });
            
            Register.Pressure_xlsb.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "Pressure XLSB output",
                    valueProviderCallback: _ => 
                    {
                        return (byte)((getPressure() & 0x0F)<<4);
                    });
            
            Register.Pressure_lsb.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "Pressure LSB output",
                    valueProviderCallback: _ => 
                    {
                        return (byte)((getPressure()>>4) & 0xFF);
                    });
            
            Register.Pressure_msb.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "Pressure MSB output",
                    valueProviderCallback: _ => 
                    {
                        LoadNextSample();
                        return (byte)((getPressure()>>12) & 0xFF);
                    });

            Register.Ctrl_meas.Define(this)
                .WithEnumField(0,2, out mode, writeCallback: (_, value) => 
                { 
                    this.Log(LogLevel.Noisy, "Mode = {0}", mode.Value); 
                })
                .WithValueField(2,3, out osrs_p, name: "osrs_p", writeCallback: (_, value) => 
                { 
                    this.Log(LogLevel.Noisy, "osrs_p = {0}", value); 
                })
                .WithValueField(5,3, out osrs_t, name: "osrs_t", writeCallback: (_, value) => 
                { 
                    this.Log(LogLevel.Noisy, "osrs_t = {0}", value); 
                });

            Register.Ctrl_hum.Define(this)
                .WithValueField(2,3, out osrs_h, name: "osrs_h", writeCallback: (_, value) => 
                { 
                    this.Log(LogLevel.Noisy, "osrs_h = {0}", value); 
                });
            Register.Reset.Define(this)
                .WithValueField(0,8, name: "reset", writeCallback: (_, value) => 
                { 
                    if(value != 0xB6) {
                        this.Log(LogLevel.Warning, "Reset value not valid", value); 
                    } else {
                        this.Log(LogLevel.Noisy, "reset = {0}", value);
                        this.Reset();
                    }
                }, valueProviderCallback: _ =>
                {
                    return 0x00;
                });

            Register.Config.Define(this)
                .WithValueField(0,1, out spi3w_en, name: "SPI3WEN", writeCallback: (_, value) => 
                { 
                    this.Log(LogLevel.Noisy, "SPI3WEN = {0}", value); 
                })
                .WithValueField(2,3, out filter, name: "Filter", writeCallback: (_, value) => 
                { 
                    this.Log(LogLevel.Noisy, "Filter = {0}", value); 
                })
                .WithValueField(5,3, out t_sb, name: "t_Standby", writeCallback: (_, value) => 
                { 
                    this.Log(LogLevel.Noisy, "t_Standby = {0}", value); 
                });
            
            Register.Calib00.Define(this, calibrationValues[0]);
            Register.Calib01.Define(this, calibrationValues[1]);
            Register.Calib02.Define(this, calibrationValues[2]);
            Register.Calib03.Define(this, calibrationValues[3]);
            Register.Calib04.Define(this, calibrationValues[4]);
            Register.Calib05.Define(this, calibrationValues[5]);
            Register.Calib06.Define(this, calibrationValues[6]);
            Register.Calib07.Define(this, calibrationValues[7]);
            Register.Calib08.Define(this, calibrationValues[8]);
            Register.Calib09.Define(this, calibrationValues[9]);
            Register.Calib10.Define(this, calibrationValues[10]);
            Register.Calib11.Define(this, calibrationValues[11]);
            Register.Calib12.Define(this, calibrationValues[12]);
            Register.Calib13.Define(this, calibrationValues[13]);
            Register.Calib14.Define(this, calibrationValues[14]);
            Register.Calib15.Define(this, calibrationValues[15]);
            Register.Calib16.Define(this, calibrationValues[16]);
            Register.Calib17.Define(this, calibrationValues[17]);
            Register.Calib18.Define(this, calibrationValues[18]);
            Register.Calib19.Define(this, calibrationValues[19]);
            Register.Calib20.Define(this, calibrationValues[20]);
            Register.Calib21.Define(this, calibrationValues[21]);
            Register.Calib22.Define(this, calibrationValues[22]);
            Register.Calib23.Define(this, calibrationValues[23]);
            Register.Calib24.Define(this, calibrationValues[24]);
            Register.Calib25.Define(this, calibrationValues[25]);
            Register.Calib26.Define(this, calibrationValues[26]);
            Register.Calib27.Define(this, calibrationValues[27]);
            Register.Calib28.Define(this, calibrationValues[28]);
            Register.Calib29.Define(this, calibrationValues[29]);
            Register.Calib30.Define(this, calibrationValues[30]);
            Register.Calib31.Define(this, calibrationValues[31]);
            Register.Calib32.Define(this, calibrationValues[32]);
            Register.Calib33.Define(this, calibrationValues[33]);
            Register.Calib34.Define(this, calibrationValues[34]);
            Register.Calib35.Define(this, calibrationValues[35]);
            Register.Calib36.Define(this, calibrationValues[36]);
            Register.Calib37.Define(this, calibrationValues[37]);
            Register.Calib38.Define(this, calibrationValues[38]);
            Register.Calib39.Define(this, calibrationValues[39]);
            Register.Calib40.Define(this, calibrationValues[40]);
            Register.Calib41.Define(this, calibrationValues[41]);
        }

        private void reg_write_callback()
        {
            this.Log(LogLevel.Noisy, "Write callback activated!");
        }
        public ByteRegisterCollection RegistersCollection { get; }
         private enum Register : byte
        {
            // Reserved: 0x0 - 0xC
            Humidity_lsb = 0xFE,
            Humidity_msb = 0xFD,
            Temperature_xlsb = 0xFC,
            Temperature_lsb = 0xFB,
            Temperature_msb = 0xFA,
            Pressure_xlsb = 0xF9,
            Pressure_lsb = 0xF8,
            Pressure_msb = 0xF7,
            Config = 0xF5,
            Ctrl_meas = 0xF4,
            Status = 0xF3,
            Ctrl_hum = 0xF2,
            Reset = 0xE0,
            Id = 0xD0,

            Calib00 = 0x88,
            Calib01 = 0x89,
            Calib02 = 0x8A,
            Calib03 = 0x8B,
            Calib04 = 0x8C,
            Calib05 = 0x8D,
            Calib06 = 0x8E,
            Calib07 = 0x8F,
            Calib08 = 0x90,
            Calib09 = 0x91,
            Calib10 = 0x92,
            Calib11 = 0x93,
            Calib12 = 0x94,
            Calib13 = 0x95,
            Calib14 = 0x96,
            Calib15 = 0x97,
            Calib16 = 0x98,
            Calib17 = 0x99,
            Calib18 = 0x9A,
            Calib19 = 0x9B,
            Calib20 = 0x9C,
            Calib21 = 0x9D,
            Calib22 = 0x9E,
            Calib23 = 0x9F,
            Calib24 = 0xA0,
            Calib25 = 0xA1,
            Calib26 = 0xE1,
            Calib27 = 0xE2,
            Calib28 = 0xE3,
            Calib29 = 0xE4,
            Calib30 = 0xE5,
            Calib31 = 0xE6,
            Calib32 = 0xE7,
            Calib33 = 0xE8,
            Calib34 = 0xE9,
            Calib35 = 0xEA,
            Calib36 = 0xEB,
            Calib37 = 0xEC,
            Calib38 = 0xED,
            Calib39 = 0xEE,
            Calib40 = 0xEF,
            Calib41 = 0xF0   
        }

        public void FeedDataSample(int pressure, int temperature, int humidity, int repeat = 1)
        {
            var sample = new Vector3DSample(pressure, temperature, humidity);

            for(var i = 0; i < repeat; i++)
            {
                dataFifo.FeedSample(sample);
            }
        }

        public void FeedDataSample(string path)
        {
            // File needs to be a string file with 3 data columns (pressure, temperature, humidity)
            dataFifo.FeedSamplesFromFile(path);
        }

        public void LoadCalibrationValues(byte[] calib)
        {
            calibrationValues = calib.ToArray();
        }

        private int getPressure()
        {
            return (int)dataFifo.Sample.X;
        } 
        private int getTemperature()
        {
            return (int)dataFifo.Sample.Y;
        } 
        private int getHumidity()
        {
            return (int)dataFifo.Sample.Z;
        } 
    }
}

