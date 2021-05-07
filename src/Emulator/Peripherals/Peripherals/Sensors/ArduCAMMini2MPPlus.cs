//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.HostInterfaces.Camera;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class ArduCAMMini2MPPlus: ISPIPeripheral, II2CPeripheral, IGPIOReceiver, IProvidesRegisterCollection<ByteRegisterCollection>, ISensor
    {
        public ArduCAMMini2MPPlus()
        {
            sensor = new OV2640(this);

            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        #region SPI_Interface

        public byte Transmit(byte data)
        {
            byte result = 0;

            switch(state)
            {
                case State.Idle:
                {
                    selectedRegister = (Register)BitHelper.GetValue(data, 0, 7);
                    var isWrite = BitHelper.IsBitSet(data, 7);

                    this.NoisyLog("Decoded register {0} (0x{0:X}) and isWrite bit as {1}", selectedRegister, isWrite);
                    state = isWrite
                        ? State.Writing
                        : State.Reading;

                    break;
                }

                case State.Reading:
                    this.NoisyLog("Reading register {0} (0x{0:X})", selectedRegister);
                    result = RegistersCollection.Read((long)selectedRegister);
                    break;

                case State.Writing:
                    this.NoisyLog("Writing 0x{0:X} to register {1} (0x{1:X})", data, selectedRegister);
                    RegistersCollection.Write((long)selectedRegister, data);
                    break;

                default:
                    this.Log(LogLevel.Error, "Received byte in an unexpected state: {0}", state);
                    break;
            }

            this.NoisyLog("Received byte 0x{0:X}, returning 0x{1:X}", data, result);
            return result;
        }

        void ISPIPeripheral.FinishTransmission()
        {
            this.NoisyLog("Finishing transmission, going idle");
            state = State.Idle;
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
                ((ISPIPeripheral)this).FinishTransmission();
            }
            chipSelected = !value;
        }

        #endregion

        #region I2C_Interface

        public void Write(byte[] data)
        {
            sensor.Write(data);
        }

        public byte[] Read(int count)
        {
            return sensor.Read(count);
        }

        void II2CPeripheral.FinishTransmission()
        {
            sensor.FinishTransmission();
        }

        #endregion

        public void Reset()
        {
            RegistersCollection.Reset();

            sensor.Reset();
            chipSelected = false;
            selectedRegister = Register.Test;
            state = State.Idle;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public string ImageSource 
        {
            get
            {
                return imageSource;
            }

            set
            {
                if(value == null)
                {
                    preloadedImageData = null;
                    imageSource = null;
                    return;
                }

                try
                {
                    preloadedImageData = File.ReadAllBytes(value);
                    imageSource = value;

                    this.NoisyLog("Loaded {0} bytes of image data to buffer", preloadedImageData.Length);
                }
                catch(Exception e)
                {
                    throw new RecoverableException($"Could not load image {value}: {(e.Message)}");
                }
            }
        }

        public void AttachToExternalCamera(HostCamera camera)
        {
#if PLATFORM_LINUX
            externalCamera = camera;
#else
            throw new RecoverableException("The external camera integration is currently available on Linux only!");
#endif
        }

        public void DetachFromExternalCamera()
        {
#if PLATFORM_LINUX
            externalCamera = null;
#else
            throw new RecoverableException("The external camera integration is currently available on Linux only!");
#endif
        }

        private void DefineRegisters()
        {
            Register.Test.Define(this)
                .WithValueField(0, 8, name: "Test field")
            ;

            Register.FifoControl.Define(this)
                .WithFlag(0, FieldMode.Write, name: "Clear FIFO write done flag", writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        fifoDone.Value = false;
                    }
                })
                .WithFlag(1, FieldMode.Write, name: "Start capture", writeCallback: (_, val) =>
                {
                    if(!val)
                    {
                        return;
                    }

                    this.NoisyLog("Capturing frame");
#if PLATFORM_LINUX
                    var ec = externalCamera;
                    if(ec != null)
                    {
                        ec.SetImageSize((int)sensor.OutputWidth, (int)sensor.OutputHeight);
                        imageData = ec.GrabFrame();
                    }
                    else if(preloadedImageData != null)
#else
                    if(preloadedImageData != null)
#endif
                    { 
                        imageData = preloadedImageData;
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "No image source set. Generating an empty frame");
                        imageData = new byte[0];
                    }

                    imageDataPointer = 0;
                    fifoDone.Value = true;
                })
                .WithReservedBits(2, 2)
                .WithTaggedFlag("Reset FIFO read pointer", 4)
                .WithTaggedFlag("Reset FIFO write pointer", 5)
                .WithReservedBits(6, 1)
            ;

            Register.BurstRead.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ =>
                {
                    if(imageDataPointer >= imageData.Length)
                    {
                        this.Log(LogLevel.Warning, "Tried to read outside the image buffer");
                        return 0;
                    }

                    return imageData[imageDataPointer++];
                })
            ;

            Register.TriggerSource.Define(this)
                .WithTaggedFlag("Camera vsync pin status", 0)
                .WithReservedBits(1, 2)
                .WithFlag(3, out fifoDone, name: "Camera write FIFO done")
                .WithReservedBits(4, 3)
            ;

            Register.CameraWriteFifoSize0.Define(this)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)(imageData?.Length ?? 0))
            ;

            Register.CameraWriteFifoSize1.Define(this)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)((imageData?.Length ?? 0) >> 8))
            ;

            Register.CameraWriteFifoSize2.Define(this)
                .WithValueField(0, 8, valueProviderCallback: _ => (byte)((imageData?.Length ?? 0) >> 16))
            ;
        }

        private bool chipSelected;
        private Register selectedRegister;
        private State state;

        private int imageDataPointer;
        private byte[] imageData;
        private byte[] preloadedImageData;
        private string imageSource;

        private IFlagRegisterField fifoDone;

        private readonly OV2640 sensor;

#if PLATFORM_LINUX
        private HostCamera externalCamera;
#endif

        private enum State
        {
            Idle,
            Reading,
            Writing
        }

        private enum Register
        {
            Test = 0x00,
            CaptureControl = 0x01,
            Reserved02 = 0x02,
            SensorInterfaceTiming = 0x03,
            FifoControl = 0x04,
            GPIODirection = 0x05,
            GPIOWrite = 0x06,
            // this gap is not covered by the documentation
            Reserved3B = 0x3B,
            BurstRead = 0x3C,
            SingleRead = 0x3D,
            Reserved3E = 0x3E,
            Reserved3F = 0x3F,
            ArduChipVersion = 0x40,
            TriggerSource = 0x41,
            CameraWriteFifoSize0 = 0x42,
            CameraWriteFifoSize1 = 0x43,
            CameraWriteFifoSize2 = 0x44,
            GPIOReadRegister = 0x45
        }
    }
}
