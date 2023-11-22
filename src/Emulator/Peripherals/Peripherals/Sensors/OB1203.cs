//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class OB1203 : II2CPeripheral, ISensor, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public OB1203()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        public void Reset()
        {
            registerAddress = null;
            RegistersCollection.Reset();
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Error, "Unexpected write with no data.");
                return;
            }
            registerAddress = (Registers)data[0];

            if(data.Length <= 1)
            {
                return;
            }
            var count = data.Length - 1;
            if(count + (int)registerAddress > LastValidAddress)
            {
                count = LastValidAddress - (int)registerAddress;
            }
            for(var i = 0; i < count; ++i)
            {
                RegistersCollection.Write((byte)registerAddress, data[i + 1]);
            }
            if(count < data.Length - 1)
            {
                this.Log(LogLevel.Warning, "Block write reached outside valid range [0x{0:X2}:0x{1:X2}].", LastValidAddress + 1, LastValidAddress + data.Length - 1 - count);
            }
        }

        public byte[] Read(int count = 1)
        {
            if(!registerAddress.HasValue)
            {
                this.Log(LogLevel.Error, "Trying to read without setting address.");
                return new byte[0];
            }
            if((int)registerAddress > LastValidAddress)
            {
                this.Log(LogLevel.Warning, "Trying to read outside of valid address range [0x{0:X2}:0x{1:X2}].", (int)registerAddress, (int)registerAddress + count);
                return new byte[0]; // NACK
            }

            var result = new byte[count];
            if(registerAddress > Registers.FIFOData && count + (int)registerAddress > LastValidAddress)
            {
                count = LastValidAddress - (int)registerAddress;
            }
            for(var i = 0; i < count; ++i)
            {
                result[i] = RegistersCollection.Read((byte)((int)registerAddress));
                if(registerAddress != Registers.FIFOData)
                {
                    registerAddress = (Registers)((int)registerAddress + 1);
                }
            }
            if(result.Length != count)
            {
                this.Log(LogLevel.Warning, "Block read reached outside valid range [0x{0:X2}:0x{1:X2}].", LastValidAddress + 1, LastValidAddress + result.Length - count);
            }
            return result;
        }

        public void FinishTransmission()
        {
            registerAddress = null;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.Status0.Define(this, 0x80)
                .WithTaggedFlag("LS_data_status", 0)
                .WithTaggedFlag("LS_interrupt_status", 1)
                .WithReservedBits(2, 5)
                .WithTaggedFlag("Power-On_status", 7)
            ;

            Registers.Status1.Define(this)
                .WithTaggedFlag("PS_data_status", 0)
                .WithTaggedFlag("PS_interrupt_status", 1)
                .WithTaggedFlag("PS_logic_signal_status", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("PPG_data_status", 4)
                .WithTaggedFlag("FIFO_almost_full_interrupt", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("TS_data_status", 7)
            ;

            Registers.ProximitySensorData0.Define(this)
                .WithTag("PS_DATA_0", 0, 8)
            ;

            Registers.ProximitySensorData1.Define(this)
                .WithTag("PS_DATA_1", 0, 8)
            ;

            Registers.LightSensorClearData0.Define(this)
                .WithTag("LS_CLEAR_DATA_0", 0, 8)
            ;

            Registers.LightSensorClearData1.Define(this)
                .WithTag("LS_CLEAR_DATA_1", 0, 8)
            ;

            Registers.LightSensorClearData2.Define(this)
                .WithTag("LS_CLEAR_DATA_2", 0, 4)
                .WithReservedBits(4, 4)
            ;

            Registers.LightSensorGreenData0.Define(this)
                .WithTag("LS_GREEN_DATA_0", 0, 8)
            ;

            Registers.LightSensorGreenData1.Define(this)
                .WithTag("LS_GREEN_DATA_1", 0, 8)
            ;

            Registers.LightSensorGreenData2.Define(this)
                .WithTag("LS_GREEN_DATA_2", 0, 4)
                .WithReservedBits(4, 4)
            ;

            Registers.LightSensorBlueData0.Define(this)
                .WithTag("LS_BLUE_DATA_0", 0, 8)
            ;

            Registers.LightSensorBlueData1.Define(this)
                .WithTag("LS_BLUE_DATA_1", 0, 8)
            ;

            Registers.LightSensorBlueData2.Define(this)
                .WithTag("LS_BLUE_DATA_2", 0, 4)
                .WithReservedBits(4, 4)
            ;

            Registers.LightSensorRedData0.Define(this)
                .WithTag("LS_RED_DATA_0", 0, 8)
            ;

            Registers.LightSensorRedData1.Define(this)
                .WithTag("LS_RED_DATA_1", 0, 8)
            ;

            Registers.LightSensorRedData2.Define(this)
                .WithTag("LS_RED_DATA_2", 0, 4)
                .WithReservedBits(4, 4)
            ;

            Registers.LightSensorCompensationData0.Define(this)
                .WithTag("COMP_DATA_0", 0, 8)
            ;

            Registers.LightSensorCompensationData1.Define(this)
                .WithTag("COMP_DATA_1", 0, 8)
            ;

            Registers.LightSensorCompensationData2.Define(this)
                .WithTag("COMP_DATA_2", 0, 4)
                .WithReservedBits(4, 4)
            ;

            Registers.MainControl0.Define(this)
                .WithTaggedFlag("LS_EN", 0)
                .WithTaggedFlag("LS_MODE", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("SAI_LS", 3)
                .WithReservedBits(4, 3)
                .WithTaggedFlag("SW reset", 7)
            ;

            Registers.MainControl1.Define(this)
                .WithTaggedFlag("PPG_PS_EN", 0)
                .WithTag("PPG_PS_MODE", 1, 2)
                .WithTaggedFlag("SAI_PS", 3)
                .WithReservedBits(4, 4)
            ;

            Registers.ProximitySensorLEDCurrent0.Define(this, 0xFF)
                .WithTag("PS_LED_CURR_0", 0, 8)
            ;

            Registers.ProximitySensorLEDCurrent1.Define(this, 0x01)
                .WithTaggedFlag("PS_LED_CURR_1", 0)
                .WithReservedBits(1, 7)
            ;

            Registers.ProximitySensorCancellationAndPulses.Define(this, 0x1A)
                .WithReservedBits(0, 3)
                .WithTag("Number_of_LED_pulses", 3, 3)
                .WithTaggedFlag("PS_CAN_ANA", 6)
                .WithReservedBits(7, 1)
            ;

            Registers.ProximitySensorPulseWidthAndPeriod.Define(this, 0x15)
                .WithTag("PS_measurement_period", 0, 3)
                .WithReservedBits(3, 1)
                .WithTag("PS_pulse_width", 4, 2)
                .WithReservedBits(6, 2)
            ;

            Registers.ProximitySensorDigitalCancellation0.Define(this)
                .WithTag("PS_CAN_DIG_0", 0, 8)
            ;

            Registers.ProximitySensorDigitalCancellation1.Define(this)
                .WithTag("PS_CAN_DIG_1", 0, 8)
            ;

            Registers.ProximitySensorMovingAverageAndHysteresis.Define(this)
                .WithTag("PS_hysteresis_level", 0, 7)
                .WithTaggedFlag("PS_moving_average_enable", 7)
            ;

            Registers.ProximitySensorUpperThreshold0.Define(this, 0xFF)
                .WithTag("PS_THRES_UP_0", 0, 8)
            ;

            Registers.ProximitySensorUpperThreshold1.Define(this, 0xFF)
                .WithTag("PS_THRES_UP_1", 0, 8)
            ;

            Registers.ProximitySensorLowerThreshold0.Define(this)
                .WithTag("PS_THRES_LOW_0", 0, 8)
            ;

            Registers.ProximitySensorLowerThreshold1.Define(this)
                .WithTag("PS_THRES_LOW_1", 0, 8)
            ;

            Registers.LightSensorResolutionAndPeriod.Define(this, 0x22)
                .WithTag("LS_Measurement_Period", 0, 3)
                .WithReservedBits(3, 1)
                .WithTag("LS_Resolution", 4, 3)
                .WithReservedBits(7, 1)
            ;

            Registers.LightSensorGain.Define(this, 0x01)
                .WithTag("LS_gain_range", 0, 2)
                .WithReservedBits(2, 6)
            ;

            Registers.LightSensorUpperThreshold0.Define(this, 0xFF)
                .WithTag("LS_THRES_UP_0", 0, 8)
            ;

            Registers.LightSensorUpperThreshold1.Define(this, 0xFF)
                .WithTag("LS_THRES_UP_1", 0, 8)
            ;

            Registers.LightSensorUpperThreshold2.Define(this, 0x0F)
                .WithTag("LS_THRES_UP_2", 0, 3)
                .WithReservedBits(3, 5)
            ;

            Registers.LightSensorLowerThreshold0.Define(this)
                .WithTag("LS_THRES_LOW_0", 0, 8)
            ;

            Registers.LightSensorLowerThreshold1.Define(this)
                .WithTag("LS_THRES_LOW_1", 0, 8)
            ;

            Registers.LightSensorLowerThreshold2.Define(this)
                .WithTag("LS_THRES_LOW_2", 0, 3)
                .WithReservedBits(3, 5)
            ;

            Registers.LightSensorVarianceThreshold.Define(this)
                .WithTag("LS_THRES_VAR", 0, 3)
                .WithReservedBits(3, 5)
            ;

            Registers.InterruptConfiguration0.Define(this, 0x10)
                .WithTaggedFlag("LS_INT_EN", 0)
                .WithTaggedFlag("LS_VAR_MODE", 1)
                .WithReservedBits(2, 2)
                .WithTag("LS_INT_SEL", 4, 2)
                .WithReservedBits(6, 2)
            ;

            Registers.InterruptConfiguration1.Define(this)
                .WithTaggedFlag("PS_INT_EN", 0)
                .WithTaggedFlag("PS_LOGIC_MODE", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("PPG_INT_EN", 4)
                .WithTaggedFlag("A_FULL_INT_EN", 5)
                .WithReservedBits(6, 2)
            ;

            Registers.InterruptPersist.Define(this)
                .WithTag("PS_PERSIST", 0, 4)
                .WithTag("LS_PERSIST", 4, 4)
            ;

            Registers.PhotoplethysmographyOrProximitySensorGain.Define(this, 0x09)
                .WithReservedBits(0, 4)
                .WithTag("PPG/PS_gain_range", 4, 2)
                .WithReservedBits(6, 2)
            ;

            Registers.PhotoplethysmographyOrProximitySensorConfiguration.Define(this, 0x40)
                .WithReservedBits(0, 3)
                .WithTaggedFlag("LED_FLIP", 0)
                .WithReservedBits(4, 2)
                .WithTaggedFlag("PPG_POW_SAVE", 6)
                .WithReservedBits(7, 1)
            ;

            Registers.PhotoplethysmographyInfraredLEDCurrent0.Define(this)
                .WithTag("PPG_IRLED_CURR_0", 0, 8)
            ;

            Registers.PhotoplethysmographyInfraredLEDCurrent1.Define(this)
                .WithTaggedFlag("PPG_IRLED_CURR_1", 0)
                .WithReservedBits(1, 7)
            ;

            Registers.PhotoplethysmographyRedLEDCurrent0.Define(this)
                .WithTag("PPG_RLED_CURR_0", 0, 8)
            ;

            Registers.PhotoplethysmographyRedLEDCurrent1.Define(this)
                .WithTaggedFlag("PPG_RLED_CURR_1", 0)
                .WithReservedBits(1, 7)
            ;

            Registers.PhotoplethysmographyAnalogCancellation.Define(this)
                .WithTaggedFlag("PPG_CH2_CAN_ANA", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("PPG_CH1_CAN_ANA", 2)
                .WithReservedBits(3, 5)
            ;

            Registers.PhotoplethysmographyAverage.Define(this, 0x0A)
                .WithReservedBits(0, 4)
                .WithTag("PPG_AVG", 4, 3)
                .WithReservedBits(7, 1)
            ;

            Registers.PhotoplethysmographyPulseWidthAndPeriod.Define(this, 0x42)
                .WithTag("PPG_measurement_period", 0, 4)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("PPG_pulse_width", 4)
                .WithReservedBits(7, 1)
            ;

            Registers.FIFOConfiguration.Define(this)
                .WithTag("FIFO_A_FULL", 0, 4)
                .WithTaggedFlag("FIFO_ROLLOVER_EN", 4)
                .WithReservedBits(5, 3)
            ;

            Registers.FIFOWritePointer.Define(this)
                .WithTag("FIFO_WR_PTR", 0, 5)
                .WithReservedBits(5, 3)
            ;

            Registers.FIFOReadPointer.Define(this)
                .WithTag("FIFO_RD_PTR", 0, 5)
                .WithReservedBits(5, 3)
            ;

            Registers.FIFOOverflowCounter.Define(this)
                .WithTag("FIFO_OVF_CNT", 0, 4)
                .WithReservedBits(4, 4)
            ;

            Registers.FIFOData.Define(this)
                .WithTag("FIFO_DATA", 0, 8)
            ;

            Registers.PartNumberId.Define(this)
                .WithTag("Part_Number_ID", 0, 8)
            ;

            Registers.DigitalGainFactoryTrimLED1.Define(this)
                .WithTag("LED1Digital gain trim factory setting", 0, 8)
            ;

            Registers.DigitalGainFactoryTrimLED2.Define(this)
                .WithTag("LED2 Digital gain trim factory setting", 0, 8)
            ;
        }

        private Registers? registerAddress;

        private const int LastValidAddress = 0x51;

        public enum Registers
        {
            Status0 = 0x00,
            Status1 = 0x01,
            ProximitySensorData0 = 0x02,
            ProximitySensorData1 = 0x03,
            LightSensorClearData0 = 0x04,
            LightSensorClearData1 = 0x05,
            LightSensorClearData2 = 0x06,
            LightSensorGreenData0 = 0x07,
            LightSensorGreenData1 = 0x08,
            LightSensorGreenData2 = 0x09,
            LightSensorBlueData0 = 0x0A,
            LightSensorBlueData1 = 0x0B,
            LightSensorBlueData2 = 0x0C,
            LightSensorRedData0 = 0x0D,
            LightSensorRedData1 = 0x0E,
            LightSensorRedData2 = 0x0F,
            LightSensorCompensationData0 = 0x10,
            LightSensorCompensationData1 = 0x11,
            LightSensorCompensationData2 = 0x12,
            MainControl0 = 0x15,
            MainControl1 = 0x16,
            ProximitySensorLEDCurrent0 = 0x17,
            ProximitySensorLEDCurrent1 = 0x18,
            ProximitySensorCancellationAndPulses = 0x19,
            ProximitySensorPulseWidthAndPeriod = 0x1A,
            ProximitySensorDigitalCancellation0 = 0x1B,
            ProximitySensorDigitalCancellation1 = 0x1C,
            ProximitySensorMovingAverageAndHysteresis = 0x1D,
            ProximitySensorUpperThreshold0 = 0x1E,
            ProximitySensorUpperThreshold1 = 0x1F,
            ProximitySensorLowerThreshold0 = 0x20,
            ProximitySensorLowerThreshold1 = 0x21,
            LightSensorResolutionAndPeriod = 0x22,
            LightSensorGain = 0x23,
            LightSensorUpperThreshold0 = 0x24,
            LightSensorUpperThreshold1 = 0x25,
            LightSensorUpperThreshold2 = 0x26,
            LightSensorLowerThreshold0 = 0x27,
            LightSensorLowerThreshold1 = 0x28,
            LightSensorLowerThreshold2 = 0x29,
            LightSensorVarianceThreshold = 0x2A,
            InterruptConfiguration0 = 0x2B,
            InterruptConfiguration1 = 0x2C,
            InterruptPersist = 0x2D,
            PhotoplethysmographyOrProximitySensorGain = 0x2E,
            PhotoplethysmographyOrProximitySensorConfiguration = 0x2F,
            PhotoplethysmographyInfraredLEDCurrent0 = 0x30,
            PhotoplethysmographyInfraredLEDCurrent1 = 0x31,
            PhotoplethysmographyRedLEDCurrent0 = 0x32,
            PhotoplethysmographyRedLEDCurrent1 = 0x33,
            PhotoplethysmographyAnalogCancellation = 0x34,
            PhotoplethysmographyAverage = 0x35,
            PhotoplethysmographyPulseWidthAndPeriod = 0x36,
            FIFOConfiguration = 0x37,
            FIFOWritePointer = 0x38,
            FIFOReadPointer = 0x39,
            FIFOOverflowCounter = 0x3A,
            FIFOData = 0x3B,
            PartNumberId = 0x3D,
            DigitalGainFactoryTrimLED1 = 0x42,
            DigitalGainFactoryTrimLED2 = 0x43,
        }
    }
}
