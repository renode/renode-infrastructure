//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class LIS2DW12 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ITemperatureSensor
    {
        public LIS2DW12()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            IRQ = new GPIO();
            DefineRegisters();

            accelerationFifo = new SensorSamplesFifo<Vector3DSample>();
        }

        public void FeedAccelerationSample(decimal x, decimal y, decimal z, uint repeat = 1)
        {
            var sample = new Vector3DSample(x, y, z);

            for(var i = 0; i < repeat; i++)
            {
                accelerationFifo.FeedSample(sample);
            }
        }

        public void FeedAccelerationSample(string path)
        {
            accelerationFifo.FeedSamplesFromFile(path);
        }

        public void FinishTransmission()
        {
            regAddress = 0;
            state = State.WaitingForRegister;
            this.Log(LogLevel.Noisy, "Transmission finished");
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            SetScaleDivider();
            state = State.WaitingForRegister;
            regAddress = 0;
            IRQ.Set(false);
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Unexpected write with no data");
                return;
            }
            this.Log(LogLevel.Noisy, "Write with {0} bytes of data: {1}", data.Length, Misc.PrettyPrintCollectionHex(data));
            if(state == State.WaitingForRegister)
            {
                regAddress = (Registers)data[0];
                this.Log(LogLevel.Noisy, "Preparing to access register {0} (0x{0:X})", regAddress);
                state = State.WaitingForData;
                data = data.Skip(1).ToArray();
            }
            if(state == State.WaitingForData)
            {
                InternalWrite(data);
            }
        }

        public byte[] Read(int count)
        {
            if(state == State.WaitingForRegister)
            {
                this.Log(LogLevel.Warning, "Unexpected read in state {0}", state);
                return new byte[count];
            }
            this.Log(LogLevel.Noisy, "Reading {0} bytes from register {1} (0x{1:X})", count, regAddress);
            var result = new byte[count];
            for(var i = 0; i < result.Length; i++)
            {
                result[i] = RegistersCollection.Read((byte)regAddress);
                this.Log(LogLevel.Noisy, "Read value 0x{0:X}", result[i]);
                if(autoIncrement.Value)
                {
                    RegistersAutoIncrement();
                }
            }
            return result;
        }

        public decimal AccelerationX
        {
            get => accelerationFifo.Sample.X;
            set
            {
                if(IsAccelerationOutOfRange(value))
                {
                    return;
                }
                accelerationFifo.Sample.X = value;
                this.Log(LogLevel.Noisy, "AccelerationX set to {0}", value);
                UpdateInterrupts();
            }
        }

        public decimal AccelerationY
        {
            get => accelerationFifo.Sample.Y;
            set
            {
                if(IsAccelerationOutOfRange(value))
                {
                    return;
                }
                accelerationFifo.Sample.Y = value;
                this.Log(LogLevel.Noisy, "AccelerationY set to {0}", value);
                UpdateInterrupts();
            }
        }

        public decimal AccelerationZ
        {
            get => accelerationFifo.Sample.Z;
            set
            {
                if(IsAccelerationOutOfRange(value))
                {
                    return;
                }
                accelerationFifo.Sample.Z = value;
                this.Log(LogLevel.Noisy, "AccelerationZ set to {0}", value);
                UpdateInterrupts();
            }
        }

        public decimal Temperature
        {
            get => temperature;
            set
            {
                if(IsTemperatureOutOfRange(value))
                {
                    return;
                }
                temperature = value;
                this.Log(LogLevel.Noisy, "Temperature set to {0}", temperature);
                UpdateInterrupts();
            }
        }

        public GPIO IRQ { get; }
        public ByteRegisterCollection RegistersCollection { get; }

        private void LoadNextSample()
        {
            this.Log(LogLevel.Noisy, "Acquiring next sample");
            accelerationFifo.TryDequeueNewSample();
            UpdateInterrupts();
        }

        private void DefineRegisters()
        {
            Registers.TemperatureOutLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Temperature output register in 12-bit resolution (OUT_T_L)",
                    valueProviderCallback: _ => (byte)(TwoComplementSignConvert(Temperature) << 4));
            
            Registers.TemperatureOutHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Temperature output register in 12-bit resolution (OUT_T_H)",
                    valueProviderCallback: _ => (byte)(TwoComplementSignConvert(Temperature) >> 4));

            Registers.WhoAmI.Define(this, 0x44);

            Registers.Control1.Define(this)
                .WithEnumField(0, 2, out lowPowerModeSelection, name: "Low-power mode selection (LP_MODE)")
                .WithEnumField(2, 2, out modeSelection, name: "Mode selection (MODE)")
                .WithEnumField(4, 4, out outDataRate, writeCallback: (_, __) =>
                    {
                        var samplingRate = 0;
                        switch(outDataRate.Value)
                        {
                            case DataRateConfig.HighPerformanceLowPower1_6Hz:
                                samplingRate = 2;
                                break;
                            case DataRateConfig.HighPerformanceLowPower12_5Hz:
                                samplingRate = 13;
                                break;
                            case DataRateConfig.HighPerformanceLowPower25Hz:
                                samplingRate = 25;
                                break;
                            case DataRateConfig.HighPerformanceLowPower50Hz:
                                samplingRate = 50;
                                break;
                            case DataRateConfig.HighPerformanceLowPower100Hz:
                                samplingRate = 100;
                                break;
                            case DataRateConfig.HighPerformanceLowPower200Hz:
                                samplingRate = 200;
                                break;
                            case DataRateConfig.HighPerformanceLowPower400Hz:
                                samplingRate = 400;
                                break;
                            case DataRateConfig.HighPerformanceLowPower800Hz:
                                samplingRate = 800;
                                break;
                            case DataRateConfig.HighPerformanceLowPower1600Hz:
                                samplingRate = 1600;
                                break;
                            default:
                                samplingRate = 0;
                                break;
                        }
                        this.Log(LogLevel.Noisy, "Sampling rate set to {0}", samplingRate);
                    }, name: "Output data rate and mode selection (ODR)");

            Registers.Control2.Define(this)
                .WithTaggedFlag("SPI serial interface mode selection (SIM)", 0)
                .WithTaggedFlag("Disable I2C communication protocol (I2C_DISABLE)", 1)
                .WithFlag(2, out autoIncrement, name: "Register address automatically incremented during multiple byte access with a serial interface (FF_ADD_INC)")
                .WithTaggedFlag("Block data update (BDU)", 3)
                .WithTaggedFlag("Disconnect CS pull-up (CS_PU_DISC)", 4)
                .WithReservedBits(5, 1)
                .WithTaggedFlag("Acts as reset for all control register (SOFT_RESET)", 6)
                .WithTaggedFlag("Enables retrieving the correct trimming parameters from nonvolatile memory into registers (BOOT)", 7);

            Registers.Control3.Define(this)
                .WithTaggedFlag("Single data conversion on demand mode enable (SLP_MODE_1)", 0)
                .WithTaggedFlag("Single data conversion on demand mode selection (SLP_MODE_SEL)", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("Interrupt active high, low (HL_ACTIVE)", 3)
                .WithTaggedFlag("Latched Interrupt (LIR)", 4)
                .WithTaggedFlag("Push-pull/open-drain selection on interrupt pad (PP_OD)", 5)
                .WithTag("Self-test enable (ST)", 6, 2);

            Registers.Control4.Define(this, 0x01)
                .WithFlag(0, out readyEnabledAcceleration, name: "Data-Ready is routed to INT1 pad (INT1_DRDY)")
                .WithTaggedFlag("FIFO threshold interrupt is routed to INT1 pad (INT1_FTH)", 1)
                .WithTaggedFlag("FIFO full recognition is routed to INT1 pad (INT1_DIFF5)", 2)
                .WithTaggedFlag("Double-tap recognition is routed to INT1 pad (INT1_TAP)", 3)
                .WithTaggedFlag("Free-fall recognition is routed to INT1 pad (INT1_FF)", 4)
                .WithTaggedFlag("Wakeup recognition is routed to INT1 pad (INT1_WU)", 5)
                .WithTaggedFlag("Single-tap recognition is routed to INT1 pad (INT1_SINGLE_TAP)", 6)
                .WithTaggedFlag("6D recognition is routed to INT1 pad (INT1_6D)", 7)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.Control5.Define(this)
                .WithTaggedFlag("Data-ready is routed to INT2 pad (INT2_DRDY)", 0)
                .WithTaggedFlag("FIFO threshold interrupt is routed to INT2 pad (INT2_FTH)", 1)
                .WithTaggedFlag("FIFO full recognition is routed to INT2 pad (INT2_DIFF5)", 2)
                .WithTaggedFlag("FIFO overrun interrupt is routed to INT2 pad (INT2_OVR)", 3)
                .WithFlag(4, out readyEnabledTemperature, name: "Temperature data-ready is routed to INT2 (INT2_DRDY_T)")
                .WithTaggedFlag("Boot state routed to INT2 pad (INT2_BOOT)", 5)
                .WithTaggedFlag("Sleep change status routed to INT2 pad (INT2_SLEEP_CHG)", 6)
                .WithTaggedFlag("Enable routing of SLEEP_STATE on INT2 pad (INT2_SLEEP_STATE)", 7);

            Registers.Control6.Define(this)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("Low-noise configuration (LOW_NOISE)", 2)
                .WithTaggedFlag("Filtered data type selection (FDS)", 3)
                .WithEnumField(4, 2, out fullScale, writeCallback: (_, __) => SetScaleDivider(), name: "Full-scale selection (FS)")
                .WithTag("Bandwidth selection (BW_FILT1)", 6, 2);

            Registers.TemperatureOut.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Temperature output register in 8-bit resolution (OUT_T)",
                    valueProviderCallback: _ => (byte)TwoComplementSignConvert(Temperature));

            Registers.Status.Define(this)
                .WithFlag(0, valueProviderCallback: _ => outDataRate.Value != DataRateConfig.PowerDown, name: "Data-ready status (DRDY)")
                .WithTaggedFlag("Free-fall event detection status (FF_IA)", 1)
                .WithTaggedFlag("Source of change in position portrait/landscape/face-up/face-down (6D_IA)", 2)
                .WithTaggedFlag("Single-tap event status (SINGLE_TAP)", 3)
                .WithTaggedFlag("Double-tap event status (DOUBLE_TAP)", 4)
                .WithTaggedFlag("Sleep event status (SLEEP_STATE)", 5)
                .WithTaggedFlag("Wakeup event detection status (WU_IA)", 6)
                .WithTaggedFlag("FIFO threshold status flag (FIFO_THS)", 7);

            Registers.DataOutXLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "X-axis LSB output register (OUT_X_L)",
                    valueProviderCallback: _ => 
                    {
                        LoadNextSample();
                        return Convert(accelerationFifo.Sample.X, upperByte: false);
                    });

            Registers.DataOutXHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "X-axis MSB output register (OUT_X_H)",
                    valueProviderCallback: _ => Convert(accelerationFifo.Sample.X, upperByte: true));

            Registers.DataOutYLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Y-axis LSB output register (OUT_Y_L)",
                    valueProviderCallback: _ => Convert(accelerationFifo.Sample.Y, upperByte: false));

            Registers.DataOutYHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Y-axis MSB output register (OUT_Y_H)",
                    valueProviderCallback: _ => Convert(accelerationFifo.Sample.Y, upperByte: true));

            Registers.DataOutZLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Z-axis LSB output register (OUT_Z_L)",
                    valueProviderCallback: _ => Convert(accelerationFifo.Sample.Z, upperByte: false));

            Registers.DataOutZHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Z-axis MSB output register (OUT_Z_H)",
                    valueProviderCallback: _ => Convert(accelerationFifo.Sample.Z, upperByte: true));

            Registers.FifoControl.Define(this)
                .WithValueField(0, 5, out fifoTreshold, name: "FIFO threshold level setting (FTH)")
                .WithEnumField(5, 3, out fifoModeSelection, name: "FIFO mode selection bits (FMode)");

            Registers.FifoSamples.Define(this)
                .WithValueField(0, 6, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        if(fifoModeSelection.Value == FIFOModeSelection.Bypass)
                        {
                            return 0;
                        }
                        return Math.Min(accelerationFifo.SamplesCount, MaxFifoSize);
                    }, name: "Number of unread samples stored in FIFO (DIFF)")
                .WithTag("FIFO overrun status (FIFO_OVR)", 6, 1)
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => accelerationFifo.SamplesCount >= fifoTreshold.Value,
                    name: "FIFO threshold status flag (FIFO_FTH)");

            Registers.TapThreshholdX.Define(this)
                .WithTag("Threshold for tap recognition on X direction (TAP_THSX)", 0, 5)
                .WithTag("Thresholds for 4D/6D function (6D_THS)", 5, 2)
                .WithTag("4D detection portrait/landscape position enable (4D_EN)", 7, 1);

            Registers.TapThreshholdY.Define(this)
                .WithTag("Threshold for tap recognition on Y direction (TAP_THSY)", 0, 5)
                .WithTag("Selection of priority axis for tap detection (TAP_PRIOR)", 5, 3);

            Registers.TapThreshholdZ.Define(this)
                .WithTag("Threshold for tap recognition on Z direction (TAP_THSZ)", 0, 5)
                .WithTag("Enables Z direction in tap recognition (TAP_Z_EN)", 5, 1)
                .WithTag("Enables Y direction in tap recognition (TAP_Y_EN)", 6, 1)
                .WithTag("Enables X direction in tap recognition (TAP_X_EN)", 7, 1);

            Registers.InterruptDuration.Define(this)
                .WithTag("Maximum duration of over-threshold event (SHOCK)", 0, 2)
                .WithTag("Expected quiet time after a tap detection (QUIET)", 2, 2)
                .WithTag("Duration of maximum time gap for double-tap recognition (LATENCY)", 4, 4);

            Registers.WakeupThreshhold.Define(this)
                .WithTag("Wakeup threshold  (WK_THS)", 0, 6)
                .WithTag("Sleep (inactivity) enable (SLEEP_ON)", 6, 1)
                .WithTag("Enable single/double-tap event (SINGLE_DOUBLE_TAP)", 7, 1);

            Registers.WakeupAndSleepDuration.Define(this)
                .WithTag("Duration to go in sleep mode (SLEEP_DUR)", 0, 4)
                .WithTag("Enable stationary detection / motion detection (STATIONARY)", 4, 1)
                .WithTag("Wakeup duration (WAKE_DUR)", 5, 2)
                .WithTag("Free-fall duration (FF_DUR5)", 7, 1);

            Registers.FreeFall.Define(this)
                .WithTag("Free-fall threshold (FF_THS)", 0, 3)
                .WithTag("Free-fall duration (FF_DUR)", 3, 5);

            Registers.StatusEventDetection.Define(this)
                .WithFlag(0, valueProviderCallback: _ => outDataRate.Value != DataRateConfig.PowerDown, name: "Data-ready status (DRDY)")
                .WithTaggedFlag("Free-fall event detection status (FF_IA)", 1)
                .WithTaggedFlag("Source of change in position portrait/landscape/face-up/face-down (6D_IA)", 2)
                .WithTaggedFlag("Single-tap event status (SINGLE_TAP)", 3)
                .WithTaggedFlag("Double-tap event status (DOUBLE_TAP)", 4)
                .WithTaggedFlag("Sleep event status (SLEEP_STATE_IA)", 5)
                .WithFlag(6, valueProviderCallback: _ => outDataRate.Value != DataRateConfig.PowerDown, name: "Temperature status (DRDY_T)")
                .WithTaggedFlag("FIFO threshold status flag (OVR)", 7);

            Registers.WakeupSource.Define(this)
                .WithTaggedFlag("Wakeup event detection status on Z-axis (Z_WU)", 0)
                .WithTaggedFlag("Wakeup event detection status on Y-axis (Y_WU)", 1)
                .WithTaggedFlag("Wakeup event detection status on X-axis (X_WU)", 2)
                .WithTaggedFlag("Wakeup event detection status (WU_IA)", 3)
                .WithTaggedFlag("Sleep event status (SLEEP_STATE_IA)", 4)
                .WithTaggedFlag("Free-fall event detection status (FF_IA)", 5)
                .WithReservedBits(6, 2);

            Registers.TapSource.Define(this)
                .WithTaggedFlag("Tap event detection status on Z-axis (Z_TAP)", 0)
                .WithTaggedFlag("Tap event detection status on Y-axis (Y_TAP)", 1)
                .WithTaggedFlag("Tap event detection status on X-axis (X_TAP)", 2)
                .WithTaggedFlag("Sign of acceleration detected by tap event (TAP_SIGN)", 3)
                .WithTaggedFlag("Double-tap event status (DOUBLE_TAP)", 4)
                .WithTaggedFlag("Single-tap event status (SINGLE_TAP)", 5)
                .WithTaggedFlag("Tap event status (TAP_IA)", 6)
                .WithReservedBits(7, 1);

            Registers.SourceOf6D.Define(this)
                .WithTaggedFlag("XL over threshold (XL)", 0)
                .WithTaggedFlag("XH over threshold (XH)", 1)
                .WithTaggedFlag("YL over threshold (YL)", 2)
                .WithTaggedFlag("YH over threshold (YH)", 3)
                .WithTaggedFlag("ZL over threshold (ZL)", 4)
                .WithTaggedFlag("ZH over threshold (ZL)", 5)
                .WithTaggedFlag("Source of change in position portrait/landscape/face-up/face-down (6D_IA)", 6)
                .WithReservedBits(7, 1);

            Registers.InterruptReset.Define(this)
                .WithTaggedFlag("Free-fall event detection status (FF_IA)", 0)
                .WithTaggedFlag("Wakeup event detection status (WU_IA)", 1)
                .WithTaggedFlag("Single-tap event status (SINGLE_TAP)", 2)
                .WithTaggedFlag("Double-tap event status (DOUBLE_TAP)", 3)
                .WithTaggedFlag("Source of change in position portrait/landscape/face-up/face-down (6D_IA)", 4)
                .WithTaggedFlag("Sleep change status (SLEEP_CHANGE_IA)", 5)
                .WithReservedBits(6, 2);

            Registers.UserOffsetX.Define(this)
                .WithTag("Two's complement user offset value on X-axis data, used for wakeup function (X_OFS_USR)", 0, 8);

            Registers.UserOffsetY.Define(this)
                .WithTag("Two's complement user offset value on Y-axis data, used for wakeup function (Y_OFS_USR)", 0, 8);

            Registers.UserOffsetZ.Define(this)
                .WithTag("Two's complement user offset value on Z-axis data, used for wakeup function (Z_OFS_USR)", 0, 8);

            Registers.Control7.Define(this)
                .WithTaggedFlag("Low pass filtered data sent to 6D interrupt function (LPASS_ON6D)", 0)
                .WithTaggedFlag("High-pass filter reference mode enable (HP_REF_MODE)", 1)
                .WithTaggedFlag("Selects the weight of the user offset words (USR_OFF_W)", 2)
                .WithTaggedFlag("Enable application of user offset value on XL data for wakeup function only (USR_OFF_ON_WU)", 3)
                .WithTaggedFlag("Enable application of user offset value on XL output data registers (USR_OFF_ON_OUT)", 4)
                .WithFlag(5, out interruptEnable, name: "Enable interrupts (INTERRUPTS_ENABLE)")
                .WithTaggedFlag("Signal routing (INT2_ON_INT1)", 6)
                .WithTaggedFlag("Switches between latched and pulsed mode for data ready interrupt (DRDY_PULSED)", 7)
                .WithChangeCallback((_,__) => UpdateInterrupts());
        }

        private void RegistersAutoIncrement()
        {
            if(regAddress >= Registers.DataOutXLow && regAddress < Registers.DataOutZHigh)
            {
                regAddress = (Registers)((int)regAddress + 1);
                this.Log(LogLevel.Noisy, "Auto-incrementing to the next register 0x{0:X} - {0}", regAddress);
            }
            else if((fifoModeSelection.Value == FIFOModeSelection.FIFOMode) && (regAddress == Registers.DataOutZHigh))
            {
                regAddress = Registers.DataOutXLow;
            }
        }

        private void UpdateInterrupts()
        {
            var status = 
                interruptEnable.Value && 
                (outDataRate.Value != DataRateConfig.PowerDown) &&
                (readyEnabledAcceleration.Value || readyEnabledTemperature.Value);
            this.Log(LogLevel.Noisy, "Setting IRQ to {0}", status);
            IRQ.Set(status);
        }

        private bool IsTemperatureOutOfRange(decimal temperature)
        {
            // This range protects from the overflow of the short variables in the 'Convert' function.
            if (temperature < MinTemperature || temperature > MaxTemperature)
            {
                this.Log(LogLevel.Warning, "Temperature {0} is out of range, use value from the range <{1:F2};{2:F2}>", temperature, MinTemperature, MaxTemperature);
                return true;
            }
            return false;
        }

        private bool IsAccelerationOutOfRange(decimal acceleration)
        {
            // This range protects from the overflow of the short variables in the 'Convert' function.
            if (acceleration < MinAcceleration || acceleration > MaxAcceleration)
            {
                this.Log(LogLevel.Warning, "Acceleration {0} is out of range, use value from the range <{1:F2};{2:F2}>", acceleration, MinAcceleration, MaxAcceleration);
                return true;
            }
            return false;
        }

        private byte Convert(decimal value, bool upperByte)
        {
            byte result = 0;
            if(outDataRate.Value == DataRateConfig.PowerDown)
            {
                return result;
            }

            var maxValue = MaxValue14Bit;
            var shift = 2;

            if(modeSelection.Value == ModeSelection.HighPerformance)
            {
                this.Log(LogLevel.Noisy, "High performance (14-bit resolution) mode is selected.");
            }
            else if((modeSelection.Value == ModeSelection.LowPower) && (lowPowerModeSelection.Value != LowPowerModeSelection.LowPowerMode1_12bitResolution))
            {
                this.Log(LogLevel.Noisy, "Low power (14-bit resolution) mode is selected.");
            }
            else if((modeSelection.Value == ModeSelection.LowPower) && (lowPowerModeSelection.Value == LowPowerModeSelection.LowPowerMode1_12bitResolution))
            {
                this.Log(LogLevel.Noisy, "Low power (12-bit resolution) mode is selected.");
                maxValue = MaxValue12Bit;
                shift = 4;
            }
            else
            {
                this.Log(LogLevel.Noisy, "Other conversion mode selected.");
            }

            var sensitivity = ((decimal)scaleDivider / maxValue) * 1000m;   // [mg/digit]
            var gain = 1m / sensitivity;
            var valueAsUshort = (ushort)((ushort)((short)((value * 1000) * gain)) << shift);
            this.Log(LogLevel.Noisy, "Conversion done with sensitivity: {0:F4}, and gain: {1:F4}", sensitivity, gain);

            if(upperByte)
            {
                return (byte)(valueAsUshort >> 8);
            }            
            result = (byte)(valueAsUshort);
            UpdateInterrupts();
            
            return result;
        }

        private void InternalWrite(byte[] data)
        {
            for(var i = 0; i < data.Length; i++)
            {
                this.Log(LogLevel.Noisy, "Writing 0x{0:X} to register {1} (0x{1:X})", data[i], regAddress);
                RegistersCollection.Write((byte)regAddress, data[i]);
                if(autoIncrement.Value)
                {
                    RegistersAutoIncrement();
                }
            }
        }

        private ushort TwoComplementSignConvert(decimal temp)
        {
            var tempAsUshort = Decimal.ToUInt16(temp);
            if(temp < 0)
            {
                var twoComplementTemp = (ushort)(~tempAsUshort + 1);
                return twoComplementTemp;
            }
            UpdateInterrupts();
            return tempAsUshort;
        }

        private void SetScaleDivider()
        {
            switch(fullScale.Value)
            {
                case FullScaleSelect.FullScale4g:
                    scaleDivider = 8;
                    break;
                case FullScaleSelect.FullScale8g:
                    scaleDivider = 16;
                    break;
                case FullScaleSelect.FullScale16g:
                    scaleDivider = 32;
                    break;
                default:
                    scaleDivider = 4;
                    break;
            }
        }

        private IFlagRegisterField autoIncrement;
        private IFlagRegisterField readyEnabledAcceleration;
        private IFlagRegisterField readyEnabledTemperature;
        private IFlagRegisterField interruptEnable;
        private IValueRegisterField fifoTreshold;
        private IEnumRegisterField<LowPowerModeSelection> lowPowerModeSelection;
        private IEnumRegisterField<DataRateConfig> outDataRate;
        private IEnumRegisterField<ModeSelection> modeSelection;
        private IEnumRegisterField<FIFOModeSelection> fifoModeSelection;
        private IEnumRegisterField<FullScaleSelect> fullScale;

        private Registers regAddress;
        private State state;
        private int scaleDivider;

        private readonly SensorSamplesFifo<Vector3DSample> accelerationFifo;

        private decimal temperature;

        private const decimal MinTemperature = -40.0m;
        private const decimal MaxTemperature = 85.0m;
        private const decimal MinAcceleration = -16m;
        private const decimal MaxAcceleration = 16m;
        private const int MaxFifoSize = 32;
        private const int MaxValue14Bit = 0x3FFF;
        private const int MaxValue12Bit = 0x0FFF;

        private enum State
        {
            WaitingForRegister,
            WaitingForData
        }

        private enum FullScaleSelect : byte
        {
            FullScale2g = 0x00,
            FullScale4g = 0x01,
            FullScale8g = 0x02,
            FullScale16g = 0x03,
        }

        private enum DataRateConfig : byte
        {
            PowerDown = 0x00,
            HighPerformanceLowPower1_6Hz = 0x01,
            HighPerformanceLowPower12_5Hz = 0x02,
            HighPerformanceLowPower25Hz = 0x03,
            HighPerformanceLowPower50Hz = 0x04,
            HighPerformanceLowPower100Hz = 0x05,
            HighPerformanceLowPower200Hz = 0x06,
            HighPerformanceLowPower400Hz = 0x07,
            HighPerformanceLowPower800Hz = 0x08,
            HighPerformanceLowPower1600Hz = 0x09,
        }

        private enum ModeSelection : byte
        {
            LowPower = 0x00,
            HighPerformance = 0x01,
            SingleDataConversionOnDemand = 0x02,
            Reserved = 0x03
        }

        private enum FIFOModeSelection : byte
        {
            Bypass = 0x00,
            FIFOMode = 0x01,
            // Reserved: 0x02
            ContinuousToFIFO = 0x03,
            BypassToContinuous = 0x04,
            // Reserved: 0x05
            Continuous = 0x06,
            // Reserved: 0x07
        }

        private enum LowPowerModeSelection : byte
        {
            LowPowerMode1_12bitResolution = 0x00,
            LowPowerMode2_14bitResolution = 0x01,
            LowPowerMode3_14bitResolution = 0x02,
            LowPowerMode4_14bitResolution = 0x03
        }

        private enum Registers : byte
        {
            // Reserved: 0x0 - 0xC
            TemperatureOutLow = 0xD,
            TemperatureOutHigh = 0xE,
            WhoAmI = 0x0F,
            // Reserved: 0x10 - 0x1F
            Control1 = 0x20,
            Control2 = 0x21,
            Control3 = 0x22,
            Control4 = 0x23,
            Control5 = 0x24,
            Control6 = 0x25,
            TemperatureOut = 0x26,
            Status = 0x27,
            DataOutXLow = 0x28,
            DataOutXHigh = 0x29,
            DataOutYLow = 0x2A,
            DataOutYHigh = 0x2B,
            DataOutZLow = 0x2C,
            DataOutZHigh = 0x2D,
            FifoControl = 0x2E,
            FifoSamples = 0x2F,
            TapThreshholdX = 0x30,
            TapThreshholdY = 0x31,
            TapThreshholdZ = 0x32,
            InterruptDuration = 0x33,
            WakeupThreshhold = 0x34,
            WakeupAndSleepDuration = 0x35,
            FreeFall = 0x36,
            StatusEventDetection = 0x37,
            WakeupSource = 0x38,
            TapSource = 0x39,
            SourceOf6D = 0x3A,
            InterruptReset = 0x3B,
            UserOffsetX = 0x3C,
            UserOffsetY = 0x3D,
            UserOffsetZ = 0x3E,
            Control7 = 0x3F
        }
    }
}
