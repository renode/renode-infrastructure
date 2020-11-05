//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.I2C;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class ADXL372 : ISPIPeripheral, II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IGPIOReceiver, ISensor
    {
        public ADXL372()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number != 0)
            {
                this.Log(LogLevel.Warning, "This model supports only CS on pin 0, but got signal on pin {0}", number);
                return;
            }

            // value is the negated CS
            if(chipSelected && value)
            {
                FinishTransmission();
            }
            chipSelected = !value;
        }

        public void Write(byte[] bytes)
        {
            foreach(var b in bytes)
            {
                WriteByte(b);
            }
        }

        public void WriteByte(byte b)
        {
            switch(state)
            {
                case State.Idle:
                    address = b;
                    state = State.Processing;
                    break;

                case State.Processing:
                    RegistersCollection.Write(address, b);
                    address++;
                    break;

                default:
                    throw new ArgumentException($"Unexpected state: {state}");
            }
        }

        public byte[] Read(int count = 1)
        {
            byte[] result = null;

            switch(state)
            {
                case State.Idle:
                    this.Log(LogLevel.Noisy, "Unexpected reading in Idle state");
                    result = new byte[] { };
                    break;

                case State.Processing:
                    result = new byte[] { RegistersCollection.Read(address) };
                    address++;
                    break;

                default:
                    throw new ArgumentException($"Unexpected state: {state}");
            }

            return result;
        }

        public byte Transmit(byte b)
        {
            if(!chipSelected)
            {
                this.Log(LogLevel.Warning, "Received transmission, but CS pin is not selected");
                return 0;
            }

            byte result = 0;
            switch(state)
            {
                case State.Idle:
                    result = HandleIdle(b);
                    break;

                case State.Reading:
                    this.NoisyLog("Reading register {0} (0x{0:X})", (Registers)address);
                    result = RegistersCollection.Read(address);
                    address++;
                    break;

                case State.Writing:
                    this.NoisyLog("Writing 0x{0:X} to register {1} (0x{1:X})", b, (Registers)address);
                    RegistersCollection.Write(address, b);
                    address++;
                    break;

                default:
                    this.Log(LogLevel.Error, "Received byte in an unexpected state!");
                    break;
            }
            
            this.Log(LogLevel.Noisy, "Transmitting - received 0x{0:X}, sending 0x{1:X} back", b, result);
            return result;
        }

        public void FinishTransmission()
        {
            this.NoisyLog("Finishing transmission, going to the Idle state");
            state = State.Idle;
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            state = State.Idle;
            address = 0;
            chipSelected = false;

            AccelerationX = 0;
            AccelerationY = 0;
            AccelerationZ = 0;
        }

        public double AccelerationX { get; set; }
        public double AccelerationY { get; set; }
        public double AccelerationZ { get; set; }

        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.DeviceID.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "DEVID_AD", valueProviderCallback: _ => 0xAD);

            Registers.PartID.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "DEVID_PRODUCT", valueProviderCallback: _ => 0xFA);

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "DATA_RDY", valueProviderCallback: _ => true)
                .WithTag("FIFO_RDY", 1, 1)
                .WithTag("FIFO_FULL", 2, 1)
                .WithTag("FIFO_OVR", 3, 1)
                .WithReservedBits(4, 1)
                .WithTag("USER_NVM_BUSY", 5, 1)
                .WithTag("AWAKE", 6, 1)
                .WithTag("ERR_USER_REGS", 7, 1);

            Registers.MaxPeakXHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "MAXPEAK_X[11:4]", valueProviderCallback: _ => Convert(AccelerationX, upperByte: true));

            Registers.MaxPeakXLow.Define(this)
                .WithReservedBits(0, 4)
                .WithValueField(4, 4, FieldMode.Read, name: "MAXPEAK_X[3:0]", valueProviderCallback: _ => Convert(AccelerationX, upperByte: false));

            Registers.MaxPeakYHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "MAXPEAK_Y[11:4]", valueProviderCallback: _ => Convert(AccelerationY, upperByte: true));

            Registers.MaxPeakYLow.Define(this)
                .WithReservedBits(0, 4)
                .WithValueField(4, 4, FieldMode.Read, name: "MAXPEAK_Y[3:0]", valueProviderCallback: _ => Convert(AccelerationY, upperByte: false));

            Registers.MaxPeakZHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "MAXPEAK_Z[11:4]", valueProviderCallback: _ => Convert(AccelerationZ, upperByte: true));

            Registers.MaxPeakZLow.Define(this)
                .WithReservedBits(0, 4)
                .WithValueField(4, 4, FieldMode.Read, name: "MAXPEAK_Z[3:0]", valueProviderCallback: _ => Convert(AccelerationZ, upperByte: false));
        }

        private byte Convert(double value, bool upperByte)
        {
            var v = (uint)(value * 160);

            // lower byte contains only 4 bits that are left-shifted
            var result = upperByte
                ? (byte)(v >> 8)
                : (byte)(v >> 4);

            return result;
        }

        private byte HandleIdle(byte b)
        {
            address = (byte)(b >> 1);

            if(BitHelper.IsBitSet(b, 0))
            {
                state = State.Reading;
            }
            else
            {
                state = State.Writing;
            }

            return 0;
        }

        private byte address;

        private State state;

        private bool chipSelected;

        private enum State
        {
            Idle,
            // those two states are used in SPI mode
            Reading,
            Writing,
            // this state is used in I2C mode
            Processing
        }

        private enum Registers
        {
            DeviceID = 0x00,
            PartID =   0x02,
            Status =   0x04,

            MaxPeakXHigh = 0x15,
            MaxPeakXLow = 0x16,
            MaxPeakYHigh = 0x17,
            MaxPeakYLow = 0x18,
            MaxPeakZHigh = 0x19,
            MaxPeakZLow = 0x1A
        }
    }
}
