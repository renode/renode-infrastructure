//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Sensor;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class BME280 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ITemperatureSensor
    {
        public BME280()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            selectedRegister = 0x0;
            EncodeTemperature();
            EncodeHumidity();
            EncodePressure();
            state = State.Idle;
        }

        public void Write(byte[] data)
        {
            this.Log(LogLevel.Noisy, "Write {0}", data.Select(x => x.ToString("X")).Aggregate((x, y) => x + " " + y));

            foreach(var b in data)
            {
                switch(state)
                {
                    case State.Idle:
                        selectedRegister = (Registers)b;
                        state = State.ReceivedFirstByte;
                        break;
                    case State.ReceivedFirstByte:
                    case State.WritingWaitingForValue:
                        RegistersCollection.Write((byte)selectedRegister, b); //bme280 have 256 addressable registers the same as byte max value
                        state = State.WaitingForAddress;
                        break;
                    case State.WaitingForAddress:
                        selectedRegister = (Registers)b;
                        state = State.WritingWaitingForValue;
                        break;
                    case State.Reading:
                        //this isn't documented, but reads are able to use address set during write transfer, opposite isn't true
                        this.Log(LogLevel.Warning, "Trying to write without specifying address, byte is omitted");
                        break;
                }
            }
        }

        public byte[] Read(int count = 0)
        {
            state = State.Reading; //reading can be started regardless of state, last selectedRegister is used
            byte[] buf = new byte[count];
            for(int i = 0; i < buf.Length; i++)
            {
                //bme280 have 256 addressable registers, byte covers them all and allows roll-over like in real hardware
                buf[i] = RegistersCollection.Read((byte)selectedRegister);
                selectedRegister++;
            }
            this.Log(LogLevel.Noisy, "Read {0}", buf.Select(x => x.ToString("X")).Aggregate((x, y) => x + " " + y));

            return buf;
        }

        public void FinishTransmission()
        {
            if(state != State.ReceivedFirstByte) //in case of reading we may (documentation permits this or repeated START) receive STOP before the read transfer
            {
                if(state == State.WritingWaitingForValue)
                {
                    this.Log(LogLevel.Warning, "Trying to write odd amount of bytes, last register is missing its value");
                }
                state = State.Idle;
            }
        }

        public decimal Temperature
        {
            get
            {
                return temperature;
            }
            set
            {
                temperature = value;
                EncodeTemperature();
            }
        }

        public double Pressure
        {
            get
            {
                return pressure;
            }
            set
            {
                pressure = value;
                EncodePressure();
            }
        }

        public double Humidity
        {
            get
            {
                return humidity;
            }
            set
            {
                humidity = value;
                EncodeHumidity();
            }
        }

        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.HumLsb.Define(this, 0x0)
                .WithValueField(0, 8, out humLsb, FieldMode.Read);
            Registers.HumMsb.Define(this, 0x80)
                .WithValueField(0, 8, out humMsb, FieldMode.Read);
            Registers.TempXlsb.Define(this, 0x0)
                .WithValueField(0, 8, out tempXlsb, FieldMode.Read);
            Registers.TempLsb.Define(this, 0x0)
                .WithValueField(0, 8, out tempLsb, FieldMode.Read);
            Registers.TempMsb.Define(this, 0x80)
                .WithValueField(0, 8, out tempMsb, FieldMode.Read);
            Registers.PressXlsb.Define(this, 0x0)
                .WithValueField(0, 8, out pressXlsb, FieldMode.Read);
            Registers.PressLsb.Define(this, 0x0)
                .WithValueField(0, 8, out pressLsb, FieldMode.Read);
            Registers.PressMsb.Define(this, 0x80)
                .WithValueField(0, 8, out pressMsb, FieldMode.Read);
            Registers.Config.Define(this, 0x0)
                .WithValueField(0, 8, name: "Config"); //read by the software, we need to implement it as a field, and not a tag
            Registers.CtrlMeas.Define(this, 0x0)
                .WithValueField(0, 8, name: "CtrlMeas"); //read by the software, we need to implement it as a field, and not a tag
            Registers.Status.Define(this, 0x0)
                .WithValueField(0, 8, name: "Status"); //read by the software, we need to implement it as a field, and not a tag
            Registers.CtrlHum.Define(this, 0x0)
                .WithValueField(0, 8, name: "CtrlHum"); //read by the software, we need to implement it as a field, and not a tag
            Registers.Reset.Define(this, 0x0)
                .WithValueField(0, 8)
                .WithWriteCallback((_, val) =>
                {
                    if(val == resetRequestVal)
                    {
                        Reset();
                    }
                });
            Registers.Id.Define(this, 0x60)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0x60);

            const ushort digT1T2 = 2 << 14;

            Registers.Calib0.Define(this, unchecked((byte)digT1T2));
            Registers.Calib1.Define(this, (byte)(digT1T2 >> 8));
            Registers.Calib2.Define(this, unchecked((byte)digT1T2));
            Registers.Calib3.Define(this, (byte)(digT1T2 >> 8));
            Registers.Calib4.Define(this, 0x0);
            Registers.Calib5.Define(this, 0x0);

            const ushort digP1P2 = 1;
            const ushort digP8 = 2 << 13;

            Registers.Calib6.Define(this, (byte)digP1P2);
            Registers.Calib7.Define(this, (byte)(digP1P2 >> 8));
            Registers.Calib8.Define(this, (byte)digP1P2);
            Registers.Calib9.Define(this, (byte)(digP1P2 >> 8));
            Registers.Calib10.Define(this, 0x0);
            Registers.Calib11.Define(this, 0x0);
            Registers.Calib12.Define(this, 0x0);
            Registers.Calib13.Define(this, 0x0);
            Registers.Calib14.Define(this, 0x0);
            Registers.Calib15.Define(this, 0x0);
            Registers.Calib16.Define(this, 0x0);
            Registers.Calib17.Define(this, 0x0);
            Registers.Calib18.Define(this, 0x0);
            Registers.Calib19.Define(this, 0x0);
            Registers.Calib20.Define(this, unchecked((byte)digP8));
            Registers.Calib21.Define(this, (byte)(digP8 >> 8));
            Registers.Calib22.Define(this, 0x0);
            Registers.Calib23.Define(this, 0x0);
            Registers.Calib24.Define(this, 0x0);

            const short digH2 = 361;
            const short digH4 = 321;
            const short digH5 = 50;
            const sbyte digH6 = 30;

            Registers.Calib25.Define(this, 0x0);
            Registers.Calib26.Define(this, unchecked((byte)digH2));
            Registers.Calib27.Define(this, (byte)(digH2 >> 8));
            Registers.Calib28.Define(this, 0x0);
            Registers.Calib29.Define(this, (byte)(digH4 >> 4));
            Registers.Calib30.Define(this, (byte)((digH4 & 0x0F) | (digH5 & 0x0F) << 4));
            Registers.Calib31.Define(this, (byte)(digH5 >> 4));
            Registers.Calib32.Define(this, (byte)digH6);
            Registers.Calib33.Define(this, 0x0);
            Registers.Calib34.Define(this, 0x0);
            Registers.Calib35.Define(this, 0x0);
            Registers.Calib36.Define(this, 0x0);
            Registers.Calib37.Define(this, 0x0);
            Registers.Calib38.Define(this, 0x0);
            Registers.Calib39.Define(this, 0x0);
            Registers.Calib40.Define(this, 0x0);
            Registers.Calib41.Define(this, 0x0);
        }

        private ushort RegistersToUShort(Registers lo, Registers hi)
        {
            ushort val = RegistersCollection.Read((byte)lo);
            val |= (ushort)(RegistersCollection.Read((byte)hi) << 8);
            return val;
        }

        private short RegistersToShort(Registers lo, Registers hi)
        {
            return (short)RegistersToUShort(lo, hi);
        }

        private int GetAdcTemperature()
        {
            var digT1 = RegistersToUShort(Registers.Calib0, Registers.Calib1);
            var digT2 = RegistersToShort(Registers.Calib2, Registers.Calib3);

            //formula and constants derived from the compensation formula in datasheet
            return (int)Math.Round(((Temperature * 100 * 256 - 128)/(5 * digT2) * 2048 + digT1 * 2) * 8);
        }

        private void EncodeTemperature()
        {
            int t = GetAdcTemperature();

            tempXlsb.Value = (byte)((t & 0x0F) << 4);
            tempLsb.Value = (byte)(t >> 4);
            tempMsb.Value = (byte)(t >> 12);
        }

        private void EncodePressure()
        {
            var digT1 = RegistersToUShort(Registers.Calib0, Registers.Calib1);
            var digT2 = RegistersToShort(Registers.Calib2, Registers.Calib3);
            var digP1 = RegistersToUShort(Registers.Calib6, Registers.Calib7);
            var digP2 = RegistersToShort(Registers.Calib8, Registers.Calib9);
            var digP8 = RegistersToShort(Registers.Calib20, Registers.Calib21);

            int adcTemp = GetAdcTemperature();
            //formula and constants derived from the compensation formula in datasheet
            long v1 = (((Int64)2 << 47) + (adcTemp / 8 - digT1 * 2) * digT2 / 2048 - 128000) * digP2 * 4096 * digP1 / ((Int64)2 << 33);
            int p = (int)Math.Round(-((Pressure - 52) * (2 << 27) / (digP8 + 1) * v1) / (3125 * ((Int64)2 << 31)) * 2 + 1048576);

            pressXlsb.Value = (byte)((p & 0x0F) << 4);
            pressLsb.Value = (byte)(p >> 4);
            pressMsb.Value = (byte)(p >> 12);
        }

        private void EncodeHumidity()
        {
            const ushort h0 = 20650;
            const ushort h100 = 38550;
            ushort h = (ushort)(h0 + (h100 - h0) * Humidity / 100);

            humLsb.Value = (byte)h;
            humMsb.Value = (byte)(h >> 8);
        }

        private State state;
        private Registers selectedRegister;

        private decimal temperature;
        private double pressure;
        private double humidity;

        private IValueRegisterField humLsb;
        private IValueRegisterField humMsb;
        private IValueRegisterField tempXlsb;
        private IValueRegisterField tempLsb;
        private IValueRegisterField tempMsb;
        private IValueRegisterField pressLsb;
        private IValueRegisterField pressMsb;
        private IValueRegisterField pressXlsb;

        private const byte resetRequestVal = 0xB6;

        private enum Registers
        {
            Calib0 = 0x88,
            Calib1 = 0x89,
            Calib2 = 0x8A,
            Calib3 = 0x8B,
            Calib4 = 0x8C,
            Calib5 = 0x8D,
            Calib6 = 0x8E,
            Calib7 = 0x8F,
            Calib8 = 0x90,
            Calib9 = 0x91,
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
            Id = 0xD0,
            Reset = 0xE0,
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
            Calib41 = 0xF0,
            CtrlHum = 0xF2,
            Status = 0xF3,
            CtrlMeas = 0xF4,
            Config = 0xF5,
            PressMsb = 0xF7,
            PressLsb = 0xF8,
            PressXlsb = 0xF9,
            TempMsb = 0xFA,
            TempLsb = 0xFB,
            TempXlsb = 0xFC,
            HumMsb = 0xFD,
            HumLsb = 0xFE
        }

        private enum State
        {
            Idle,
            ReceivedFirstByte,
            WaitingForAddress,
            WritingWaitingForValue,
            Reading
        }
    }
}
