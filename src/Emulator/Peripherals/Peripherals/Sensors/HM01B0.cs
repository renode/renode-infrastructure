//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.I2C;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class HiMaxHM01B0 : I2CPeripheralBase<HiMaxHM01B0.Registers>, ICPIPeripheral, ISensor
    {
        public HiMaxHM01B0() : base(16)
        {
            imageData = new byte[0];
        }

        public byte[] ReadFrame()
        {
            return imageData;
        }

        public string ImageSource 
        {
            get
            {
                return imageSource;
            }

            set
            {
                try
                {
                    imageData = File.ReadAllBytes(value);
                    imageSource = value;

                    this.NoisyLog("Loaded {0} bytes of image data to buffer", imageData.Length);
                }
                catch(Exception e)
                {
                    throw new RecoverableException($"Could not load image {value}: {(e.Message)}");
                }
            }
        }

        protected override void DefineRegisters()
        {
        }
        
        private byte[] imageData;
        private string imageSource;

        // based on https://github.com/sparkfun/SparkFun_Apollo3_AmbiqSuite_BSPs/blob/master/common/third_party/hm01b0/HM01B0.h 
        public enum Registers
        {
            ModelIdHigh = 0x0000,
            ModelIdLow = 0x0001,
            SiliconRevision = 0x0002,
            FrameCount = 0x0005,
            PixelOrder = 0x0006,

            ModeSelect = 0x0100,
            ImageOrientation = 0x0101,
            SoftwareReset = 0x0103,
            GRP_ParamHold = 0x0104,

            IntegrationHigh = 0x0202,
            IntegrationLow = 0x0203,
            AnalogGain = 0x0205,
            DigitalGainHigh = 0x020E,
            DigitalGainLow = 0x020F,

            AE_TargetMean = 0x2101,
            AE_MinMean = 0x2102,
            ConvergeInThreshold = 0x2103,
            ConvergeOutThreshold = 0x2104,

            I2C_IdSelection = 0x3400,
            I2C_Id = 0x3401,

            PMU_ProgrammableFrameCount = 0x3020,
        }
    }
}
