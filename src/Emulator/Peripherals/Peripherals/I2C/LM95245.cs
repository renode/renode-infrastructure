//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class LM95245 : II2CPeripheral
    {
        public LM95245 ()
        {
            Reset ();
        }

        public void Reset ()
        {
            //conversionRate = 2;
        }

        public void Write (byte[] data)
        {
            this.Log (LogLevel.Noisy, "Write! {0}", data.Select (x=>"0x"+x.ToString("X2")).Aggregate((x,y)=>x+" "+y));
        }

        public byte[] Read (int count)
        {
            this.Log (LogLevel.Noisy, "Read!");
            return new byte[]{0};
        }

        private byte ReadByte (byte offset)
        {
           switch ((Registers)offset) {
            case Registers.LocalTempMSB:
                break;
            case Registers.LocalTempLSB:
                break;
            case Registers.SignedRemoteTempMSB:
                break;
            case Registers.SignedRemoteTempLSB:
                break;
            case Registers.UnsignedRemoteTempMSB:
                break;
            case Registers.UnignedRemoteTempLSB:
                break;
            case Registers.ConfigurationRegister2:
                break;
            case Registers.RemoteOffsetHigh:
                break;
            case Registers.RemoteOffsetLow:
                break;
            case Registers.Configurationregister1A:
            case Registers.Configurationregister1B:
                break;
            case Registers.ConversionRateA:
            case Registers.ConversionRateB:
                break;
            case Registers.Status1:
                break;
            case Registers.Status2:
                return 0;
            case Registers.RemoteOSLimitA:
            case Registers.RemoteOSLimitB:
                break;
            case Registers.LocalTCritLimit:
                break;
            case Registers.RemoteTCritLimit:
                break;
            case Registers.CommonHysteresis:
                break;
            case Registers.ManufacturerID:
                return 1;   //according to spec
            case Registers.RevisionID:
                return 0xB3; //according to spec
            default:
                return 0;
            }
            return 0;
        }

        public void WriteByte (byte offset, byte value)
        {
            switch ((Registers)offset) {
            case Registers.ConfigurationRegister2:
                break;
            case Registers.RemoteOffsetHigh:
                break;
            case Registers.RemoteOffsetLow:
                break;
            case Registers.Configurationregister1A:
            case Registers.Configurationregister1B:
                break;
            case Registers.ConversionRateA:
            case Registers.ConversionRateB:
                break;
            case Registers.OneShot:
                break;
            case Registers.RemoteOSLimitA:
            case Registers.RemoteOSLimitB:
                break;
            case Registers.LocalTCritLimit:
                break;
            case Registers.RemoteTCritLimit:
                break;
            case Registers.CommonHysteresis:
                break;
            default:
                throw new ArgumentOutOfRangeException ();
            }
        }

        public void FinishTransmission()
        {
        }

     /*   private double localTemperature;
        private double remoteTemperature;
        private double remoteOffset;
        private byte configurationRegister1;
        private byte configurationRegister2;
        private byte conversionRate;*/

        private enum Registers
        {
            LocalTempMSB = 0x00,
            LocalTempLSB = 0x30,
            SignedRemoteTempMSB = 0x01,
            SignedRemoteTempLSB = 0x10,
            UnsignedRemoteTempMSB = 0x31,
            UnignedRemoteTempLSB = 0x32,
            ConfigurationRegister2 = 0xBF,
            RemoteOffsetHigh = 0x11,
            RemoteOffsetLow = 0x12,
            Configurationregister1A = 0x03,
            Configurationregister1B = 0x09,
            ConversionRateA = 0x04,
            ConversionRateB = 0x0A,
            OneShot = 0x0F,
            Status1 = 0x02,
            Status2 = 0x33,
            RemoteOSLimitA = 0x07,
            RemoteOSLimitB = 0x0D,
            LocalTCritLimit = 0x20,
            RemoteTCritLimit = 0x19,
            CommonHysteresis = 0x21,
            ManufacturerID = 0xFE,
            RevisionID = 0xFF
        }
    }
}

