//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors;

public class LPS25HB : II2CPeripheral, IProvidesRegisterCollection<WordRegisterCollection>, ITemperatureSensor, IPressureSensor, ISensor, IUnderstandRESD
{
    public LPS25HB(IMachine _)
    {
        RegistersCollection = new WordRegisterCollection(this);
        DefineRegisters();
        Reset();
    }

    public byte[] Read(int count = 1)
    {
        if(disableI2C.Value)
        {
            this.WarningLog("Tried to read data from sensor via disabled I2C");
            return new byte[] { };
        }

        if(currentAddress == -1)
        {
            this.WarningLog("Tried to read without addressing");
            return new byte[] { };
        }

        var result = new byte[count];
        for(int i = 0; i < count; i++)
        {
            result[i] = (byte)(RegistersCollection.Read(currentAddress) & 0xFF);
            if(autoIncrement)
            {
                currentAddress++;
            }
        }

        return result;
    }

    public void Reset()
    {
        currentAddress = -1;
        pressureAverageConfiguration.Value = AveragePressureConfiguration.Average512;
        temperatureAverageConfiguration.Value = AverageTemperatureConfiguration.Average64;

        temperature = DefaultTemperatureOffset;
        pressure = 0;
    }

    public void Write(byte[] data)
    {
        if(disableI2C.Value)
        {
            this.WarningLog("Tried to write data to sensor via disabled I2C");
            return;
        }

        if(currentAddress != -1)
        {
            WriteToRegister(data[0]);
        }
        else
        {
            autoIncrement = (data[0] >> 7) == 1;
            currentAddress = data[0] & 0b01111111;
        }

        for(var i = 1; i < data.Length; i++)
        {
            WriteToRegister(data[i]);
        }
    }

    public void FinishTransmission()
    {
        autoIncrement = false;
        currentAddress = -1;
    }

    public void FeedPressureSamplesFromRESD(ReadFilePath filePath, uint channelId = 0, ulong startTime = 0,
        RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
    {
        pressureStream?.Dispose();
        pressureStream = this.CreateRESDStream<PressureSample>(filePath, channelId, sampleOffsetType, sampleOffsetTime);
        pressureFeederThread?.Stop();
        pressureFeederThread = pressureStream.StartSampleFeedThread(this,
            OutputFrequency,
            startTime: startTime
        );

        this.Log(LogLevel.Noisy, "RESD stream set to {0}", filePath);
    }

    public void FeedTemperatureSamplesFromRESD(ReadFilePath filePath, uint channelId = 0, ulong startTime = 0,
        RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
    {
        temperatureStream?.Dispose();
        temperatureStream = this.CreateRESDStream<TemperatureSample>(filePath, channelId, sampleOffsetType, sampleOffsetTime);
        temperatureFeederThread?.Stop();
        temperatureFeederThread = temperatureStream.StartSampleFeedThread(this,
            OutputFrequency,
            startTime: startTime
        );
        this.Log(LogLevel.Noisy, "RESD stream set to {0}", filePath);
    }

    public WordRegisterCollection RegistersCollection { get; }

    public decimal Temperature
    {
        set
        {
            if(TrySetTemperatureRegisters(value))
            {
                temperature = Temperature;
            }
        }

        get
        {
            var raw = (short)((temperatureOutputHigh.Value << 8) | temperatureOutputLow.Value);
            return DefaultTemperatureOffset + raw / 480M;
        }
    }

    public decimal Pressure
    {
        set
        {
            if(TrySetPressureRegisters(value))
            {
                pressure = Pressure;
            }
        }

        get
        {
            int pressureCount = From24BitBytes(pressureOutputHigh, pressureOutputMiddle, pressureOutputLow);
            if(autoZeroEnable.Value)
            {
                var referencePressure = From24BitBytes(referencePressureHigh, referencePressureMiddle, referencePressureLow);
                pressureCount -= referencePressure;
            }

            pressureCount -= ((int)referencePressureHigh.Value << 8 | (int)referencePressureLow.Value);

            return pressureCount / 4096M;
        }
    }

    private int From24BitBytes(IValueRegisterField high, IValueRegisterField mid, IValueRegisterField low)
    {
        int value = ((int)high.Value << 16) | ((int)mid.Value << 8) | (int)low.Value;
        return (value << 8) >> 8;
    }

    private void WriteToRegister(byte data)
    {
        this.DebugLog($"Writing 0x{data.ToHex()} to register 0x{currentAddress:X02}");
        if(currentAddress == -1)
        {
            this.WarningLog("Tried to write without specyfing register");
            return;
        }
        RegistersCollection.Write(currentAddress, data);
        if(autoIncrement)
        {
            currentAddress++;
        }
    }

    private bool TrySetPressureRegisters(decimal pressureValue)
    {
        if(pressureValue < MinimalPressureOperatingValue || pressureValue > MaximalPressureOperatingValue)
        {
            this.WarningLog($"Value {pressureValue} is not in valid pressure range, ignoring.");
            return false;
        }

        int intValue = (int) (pressureValue*4096M);
        pressureOutputHigh.Value = (ulong)(intValue >> 16) & 0xFF;
        pressureOutputMiddle.Value = (ulong)(intValue >> 8) & 0xFF;
        pressureOutputLow.Value = (ulong)(intValue) & 0xFF;
        return true;
    }

    private bool TrySetTemperatureRegisters(decimal temperatureValue)
    {
        if(temperatureValue < MinimalTemperatureOperatingValue || temperatureValue > MaximalTemperatureOperatingValue)
        {
            this.WarningLog($"Value {temperatureValue} is not in valid temperature range, ignoring.");
            return false;
        }

        int intValue = (int) ((temperatureValue - DefaultTemperatureOffset)*480);
        temperatureOutputHigh.Value = (ulong)(intValue >> 8) & 0xFF;
        temperatureOutputLow.Value = (ulong)(intValue) & 0xFF;
        return true;
    }

    [OnRESDSample(SampleType.Pressure)]
    private void OnPressureSample(PressureSample sample, TimeInterval _)
    {
        TrySetPressureRegisters(sample.Pressure / (decimal)(1e2));
    }

    [OnRESDSample(SampleType.Temperature)]
    private void OnTemperatureSample(TemperatureSample sample, TimeInterval _)
    {
        TrySetTemperatureRegisters(sample.Temperature / (decimal)(1e2));
    }

    [AfterRESDSample(SampleType.Temperature)]
    private void HandleTemperatureSampleEnded(TemperatureSample _, TimeInterval __)
    {
        Temperature = temperature;
        temperatureFeederThread.Stop();
        temperatureFeederThread = null;
    }

    [AfterRESDSample(SampleType.Pressure)]
    private void HandlePressureSampleEnded(PressureSample _, TimeInterval __)
    {
        Pressure = pressure;
        pressureFeederThread.Stop();
        pressureFeederThread = null;
    }

    private void DefineRegisters()
    {
        Registers.ReferencePressureLow.Define(this)
            .WithValueField(0, 8, out referencePressureLow, name: "REF_P_XL");
        Registers.ReferencePressureMiddle.Define(this)
            .WithValueField(0, 8, out referencePressureMiddle, name: "REF_P_L");
        Registers.ReferencePressureHigh.Define(this)
            .WithValueField(0, 8, out referencePressureHigh, name: "REF_P_HL");

        Registers.WhoAmIRegister.Define(this)
            .WithValueField(0, 8, mode: FieldMode.Read, valueProviderCallback: _ => 0b10111101, name: "WHO_AM_I");

        Registers.Resolution.Define(this)
            .WithEnumField(0, 2, out pressureAverageConfiguration, name: "AVGP")
            .WithEnumField(3, 2, out temperatureAverageConfiguration, name: "AVGT");

        Registers.Control1.Define(this)
            .WithFlag(0, name: "SIM")
            .WithFlag(1, FieldMode.WriteOneToClear, writeCallback: (_, _) => ResetAutoZero(), name: "RESET_AZ")
            .WithFlag(2, name: "BDU")
            .WithTaggedFlag("DIFF_EN", 3)
            .WithEnumField(4, 3, out outputDataRate, name: "ODR")
            .WithFlag(7, name: "PD");

        Registers.Control2.Define(this)
            .WithFlag(0, name: "ONE_SHOT")
            .WithFlag(1, out autoZeroEnable, name: "AUTOZERO")
            .WithFlag(2, FieldMode.WriteOneToClear, writeCallback: (_, _) => Reset(), name: "SWRESET")
            .WithFlag(3, out disableI2C, name: "I2C_DIS")
            .WithTaggedFlag("FIFO_MEAN_DEC", 4)
            .WithTaggedFlag("STOP_ON_FTH", 5)
            .WithTaggedFlag("FIFO_EN", 6)
            .WithFlag(7, FieldMode.WriteOneToClear, writeCallback: (_, _) => Reset(), name: "BOOT");

        Registers.Control3.Define(this)
            .WithEnumField<WordRegister, DataReadyInterruptConfiguration>(0, 2, name: "INT_S")
            .WithReservedBits(2, 4)
            .WithFlag(6, name: "PP_OD")
            .WithFlag(7, name: "INT_H_L");

        Registers.Control4.Define(this)
            .WithTaggedFlag("DRDY", 0)
            .WithTaggedFlag("F_OVR", 1)
            .WithTaggedFlag("F_FTH", 2)
            .WithTaggedFlag("F_EMPTY", 3)
            .WithReservedBits(4, 4);

        Registers.InterruptConfig.Define(this)
            .WithTaggedFlag("PH_E", 0)
            .WithTaggedFlag("PL_E", 1)
            .WithTaggedFlag("LIR", 2);

        Registers.InterruptSource.Define(this)
            .WithTaggedFlag("PH", 0)
            .WithTaggedFlag("PL", 1)
            .WithTaggedFlag("IA", 2);

        Registers.Status.Define(this)
            .WithFlag(0, mode: FieldMode.Read, valueProviderCallback: _ => true, name: "T_DA")
            .WithFlag(1, mode: FieldMode.Read, valueProviderCallback: _ => true, name: "P_DA")
            .WithReservedBits(2, 2)
            .WithFlag(4, mode: FieldMode.Read, valueProviderCallback: _ => false, name: "T_OR")
            .WithFlag(5, mode: FieldMode.Read, valueProviderCallback: _ => false, name: "P_OR")
            .WithReservedBits(6, 2);

        Registers.PressureOutputLow.Define(this)
            .WithValueField(0, 8, out pressureOutputLow, FieldMode.Read, name: "PRESS_OUT_XL");

        Registers.PressureOutputMiddle.Define(this)
            .WithValueField(0, 8, out pressureOutputMiddle, FieldMode.Read, name: "PRESS_OUT_L");

        Registers.PressureOutputHigh.Define(this)
            .WithValueField(0, 8, out pressureOutputHigh, FieldMode.Read, name: "PRESS_OUT_H");

        Registers.TemperatureOutputLow.Define(this)
            .WithValueField(0, 8, out temperatureOutputLow, FieldMode.Read, name: "TEMP_OUT_L");

        Registers.TemperatureOutputHigh.Define(this)
            .WithValueField(0, 8, out temperatureOutputHigh, FieldMode.Read, name: "TEMP_OUT_H");

        Registers.FifoControl.Define(this)
            .WithEnumField<WordRegister, RunningAverageSampleSize>(0, 5, name: "WTM_POINT")
            .WithEnumField<WordRegister, FifoModeSelection>(5, 3, name: "F_MODE");

        Registers.FifoStatus.Define(this)
            .WithValueField(0, 5, FieldMode.Read, valueProviderCallback: _ => 0, name: "FSS")
            .WithTaggedFlag("EMPTY_FIFO", 5)
            .WithTaggedFlag("OVR", 6)
            .WithTaggedFlag("FTH_FIFO", 7);

        Registers.PressureThresholdLow.Define(this)
            .WithValueField(0, 8, out pressureThresholdLow, name: "THS_P_L");

        Registers.PressureThresholdHigh.Define(this)
            .WithValueField(0, 8, out pressureThresholdHigh, name: "THS_P_H");

        Registers.PressureOffsetLow.Define(this)
            .WithValueField(0, 8, out pressureOffsetLow, name: "RPDS_L");

        Registers.PressureOffsetHigh.Define(this)
            .WithValueField(0, 8, out pressureOffsetHigh, name: "RPDS_H");
    }

    private void ResetAutoZero()
    {
        referencePressureHigh.Value = 0;
        referencePressureMiddle.Value = 0;
        referencePressureLow.Value = 0;
    }

    private uint OutputFrequency => outputDataRate.Value switch
    {
        OutputDataRate._1Hz => 1,
        OutputDataRate._7Hz => 7,
        OutputDataRate._12_5Hz => 12, // RESDFeeder expects integer frequency
        OutputDataRate._25Hz => 25,
        OutputDataRate.OneShotMode => throw new RecoverableException("Sensor is in one-shot mode"),
        _ => throw new RecoverableException("Illegal OutputDataRate")
    };

    private RESDStream<PressureSample> pressureStream;

    private IValueRegisterField pressureOffsetLow;
    private IValueRegisterField pressureOffsetHigh;

    private IValueRegisterField pressureThresholdLow;
    private IValueRegisterField pressureThresholdHigh;

    private IValueRegisterField temperatureOutputHigh;
    private IValueRegisterField temperatureOutputLow;

    private IValueRegisterField pressureOutputHigh;
    private IValueRegisterField pressureOutputMiddle;
    private IValueRegisterField pressureOutputLow;

    private IValueRegisterField referencePressureHigh;
    private IValueRegisterField referencePressureMiddle;
    private IValueRegisterField referencePressureLow;

    private IFlagRegisterField disableI2C;
    private IFlagRegisterField autoZeroEnable;

    private IEnumRegisterField<AverageTemperatureConfiguration> temperatureAverageConfiguration;
    private IEnumRegisterField<AveragePressureConfiguration> pressureAverageConfiguration;

    private RESDStream<TemperatureSample> temperatureStream;
    private IEnumRegisterField<OutputDataRate> outputDataRate;

    private IManagedThread pressureFeederThread;
    private IManagedThread temperatureFeederThread;

    private decimal temperature;
    private decimal pressure;

    private int currentAddress;
    private bool autoIncrement = false;

    private const decimal DefaultTemperatureOffset = 42.5M;

    private const int MinimalPressureOperatingValue = 260;
    private const int MaximalPressureOperatingValue = 1260;

    private const int MinimalTemperatureOperatingValue = -30;
    private const int MaximalTemperatureOperatingValue = 105;

    private enum DataReadyInterruptConfiguration
    {
        DataSignal = 0,
        PressureHigh = 1,
        PressureLow = 2,
        PressureLowOrHigh = 3
    }

    private enum OutputDataRate
    {
        OneShotMode = 0,
        _1Hz = 1,
        _7Hz = 2,
        _12_5Hz = 3,
        _25Hz = 4,
        Reserved = 5
    }

    private enum AverageTemperatureConfiguration
    {
        Average8 = 0,
        Average16 = 1,
        Average32 = 2,
        Average64 = 3
    }

    private enum AveragePressureConfiguration
    {
        Average8 = 0,
        Average32 = 1,
        Average128 = 2,
        Average512 = 3
    }

    private enum FifoModeSelection
    {
        Bypass = 0,
        Fifo = 1,
        Stream = 2,
        StreamToFifo = 3,
        BypassToStream = 4,
        NotAvailable = 5,
        FifoMean = 6,
        BypassToFifo = 7
    }

    private enum RunningAverageSampleSize
    {
        Average2 = 1,
        Average4 = 3,
        Average8 = 7,
        Average16 = 15,
        Average32 = 31
    }

    private enum Registers : byte
    {
        ReferencePressureLow    = 0x08, // REF_P_XL
        ReferencePressureMiddle = 0x09, // REF_P_L
        ReferencePressureHigh   = 0x0A, // REF_P_H
        WhoAmIRegister = 0x0F,          // WHO_AM_I
        Resolution = 0x10,              // RES_CONF
        Control1 = 0x20,                // CTRL_REG_1
        Control2 = 0x21,                // CTRL_REG_2
        Control3 = 0x22,                // CTRL_REG_3
        Control4 = 0x23,                // CTRL_REG_4
        InterruptConfig  = 0x24,        // INTERRUPT_CFG
        InterruptSource  = 0x25,        // INT_SOURCE
        Status = 0x27,                  // STATUS_REG
        PressureOutputLow = 0x28,       // PRESS_OUT_XL
        PressureOutputMiddle = 0x29,    // PRESS_OUT_L
        PressureOutputHigh = 0x2A,      // PRESS_OUT_H
        TemperatureOutputLow = 0x2B,    // TEMP_OUT_L
        TemperatureOutputHigh = 0x2C,   // TEMP_OUT_H
        FifoControl = 0x2E,             // FIFO_CTRL
        FifoStatus = 0x2F,              // FIFO_STATUS
        PressureThresholdLow = 0x30,    // THS_P_L
        PressureThresholdHigh = 0x31,   // THS_P_H
        PressureOffsetLow = 0x39,       // RPDS_L
        PressureOffsetHigh = 0x3A,      // RPDS_H
    }
}
