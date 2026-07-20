//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

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

public class IIS2MDC : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ISensor, IMagneticSensor, IUnderstandRESD
{
    public IIS2MDC()
    {
        RegistersCollection = new ByteRegisterCollection(this);
        DefineRegisters();
    }

    public void FeedMagneticSamplesFromRESD(ReadFilePath filePath, uint channelId = 0, RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
    {
        magResdStream?.Dispose();
        magResdStream = this.CreateRESDStream<MagneticSample>(filePath, channelId, sampleOffsetType, sampleOffsetTime);
        magResdFeederThread = magResdStream.StartSampleFeedThread<MagneticSample>(this, frequency: GetOutputDataRateHz(), shouldStop: false);
        this.Log(LogLevel.Noisy, $"RESD stream set to {filePath}");
    }

    public void Reset()
    {
        SoftwareReset();
        magResdStream?.Dispose();
        magResdStream = null;
    }

    #region I2C
    public void Write(byte[] data)
    {
        foreach(var b in data)
        {
            switch(state)
            {
            default:
                this.Log(LogLevel.Warning, $"Unexpected state {state} while writing data to the sensor. Treating as {I2CState.WaitingForRegister}.");
                goto case I2CState.WaitingForRegister;
            case I2CState.WaitingForRegister:
                // MSb of the sub-address controls auto-increment behaviour, 7 lower bits are the register address
                i2cAutoIncrement = (b & 0x80) != 0;
                selectedRegister = (Registers)(b & 0x7F);
                state = I2CState.WritingData;
                break;
            case I2CState.WritingData:
                RegistersCollection.Write((byte)selectedRegister, b);
                state = I2CState.WritingData;
                I2CAutoIncrement();
                break;
            }
        }
    }

    public byte[] Read(int count = 1)
    {
        state = I2CState.Reading;
        var result = new byte[count];

        /*
        The IIS2MDC datasheet (p. 23, section 6.1.1 "I²C operation") defines the sub-address MSb as the auto-increment bit (which we handle in `Write`).
        However, the actual behaviour of the device on a burst read is to read from multiple registers (effectively auto-incrementing the register address), regardless of the sub-address MSb value.
        We therefore ignore `i2cAutoIncrement` here and read the registers sequentially, starting from the last selected register.
        */
        var currentRegister = selectedRegister;
        for(int i = 0; i < count; i++)
        {
            result[i] = RegistersCollection.Read((byte)currentRegister);
            currentRegister = GetNextRegister(currentRegister);
        }

        return result;
    }

    void II2CPeripheral.FinishTransmission()
    {
        state = I2CState.WaitingForRegister;
    }
    #endregion

    public GPIO InterruptAndDataReady { get; } = new GPIO();

    public int MagneticFluxDensityX
    {
        get => magneticFluxDensityX;
        set
        {
            if(magResdFeederThread != null)
            {
                throw new RecoverableException("Magnetic flux density X should not be set manually when RESD stream is active.");
            }
            else
            {
                this.NoisyLog("Magnetic flux density X set to {0}.", value);
                magneticFluxDensityX = value;
                FetchDataAndUpdateState();
            }
        }
    }

    public int MagneticFluxDensityY
    {
        get => magneticFluxDensityY;
        set
        {
            if(magResdFeederThread != null)
            {
                throw new RecoverableException("Magnetic flux density Y should not be set manually when RESD stream is active.");
            }
            else
            {
                this.NoisyLog("Magnetic flux density Y set to {0}.", value);
                magneticFluxDensityY = value;
                FetchDataAndUpdateState();
            }
        }
    }

    public int MagneticFluxDensityZ
    {
        get => magneticFluxDensityZ;
        set
        {
            if(magResdFeederThread != null)
            {
                throw new RecoverableException("Magnetic flux density Z should not be set manually when RESD stream is active.");
            }
            else
            {
                this.NoisyLog("Magnetic flux density Z set to {0}.", value);
                magneticFluxDensityZ = value;
                FetchDataAndUpdateState();
            }
        }
    }

    public ByteRegisterCollection RegistersCollection { get; }

    [OnRESDSample(SampleType.MagneticFluxDensity), AfterRESDSample(SampleType.MagneticFluxDensity)]
    private void OnMagneticSample(MagneticSample sample, TimeInterval _)
    {
        magneticFluxDensityX = sample.MagneticFluxDensityX;
        magneticFluxDensityY = sample.MagneticFluxDensityY;
        magneticFluxDensityZ = sample.MagneticFluxDensityZ;
        FetchDataAndUpdateState();
    }

    private void DefineRegisters()
    {
        Registers.HardIronOffsetXLow.Define(this)
            .WithValueField(0, 8, name: "OFFSET_X_REG_L",
                valueProviderCallback: _ => (byte)BitHelper.GetValue(hardIronOffsetX, 0, 8),
                writeCallback: (_, value) => BitHelper.ReplaceBits(ref hardIronOffsetX, source: (uint)value, width: 8, destinationPosition: 0)
            )
        ;
        Registers.HardIronOffsetXHigh.Define(this)
            .WithValueField(0, 8, name: "OFFSET_X_REG_H",
                valueProviderCallback: _ => (byte)BitHelper.GetValue(hardIronOffsetX, 8, 8),
                writeCallback: (_, value) => BitHelper.ReplaceBits(ref hardIronOffsetX, source: (uint)value, width: 8, destinationPosition: 8)
            )
        ;
        Registers.HardIronOffsetYLow.Define(this)
            .WithValueField(0, 8, name: "OFFSET_Y_REG_L",
                valueProviderCallback: _ => (byte)BitHelper.GetValue(hardIronOffsetY, 0, 8),
                writeCallback: (_, value) => BitHelper.ReplaceBits(ref hardIronOffsetY, source: (uint)value, width: 8, destinationPosition: 0)
            )
        ;
        Registers.HardIronOffsetYHigh.Define(this)
            .WithValueField(0, 8, name: "OFFSET_Y_REG_H",
                valueProviderCallback: _ => (byte)BitHelper.GetValue(hardIronOffsetY, 8, 8),
                writeCallback: (_, value) => BitHelper.ReplaceBits(ref hardIronOffsetY, source: (uint)value, width: 8, destinationPosition: 8)
            )
        ;
        Registers.HardIronOffsetZLow.Define(this)
            .WithValueField(0, 8, name: "OFFSET_Z_REG_L",
                valueProviderCallback: _ => (byte)BitHelper.GetValue(hardIronOffsetZ, 0, 8),
                writeCallback: (_, value) => BitHelper.ReplaceBits(ref hardIronOffsetZ, source: (uint)value, width: 8, destinationPosition: 0)
            )
        ;
        Registers.HardIronOffsetZHigh.Define(this)
            .WithValueField(0, 8, name: "OFFSET_Z_REG_H",
                valueProviderCallback: _ => (byte)BitHelper.GetValue(hardIronOffsetZ, 8, 8),
                writeCallback: (_, value) => BitHelper.ReplaceBits(ref hardIronOffsetZ, source: (uint)value, width: 8, destinationPosition: 8)
            )
        ;
        Registers.Identification.Define(this, 0b01000000)
            .WithValueField(0, 8, FieldMode.Read, name: "WHO_AM_I")
        ;
        Registers.ConfigurationA.Define(this, 0b00000011)
            .WithEnumField(position: 0, width: 2, enumField: out modeOfOperation, name: "Mode of operation MD[1:0]",
                changeCallback: (oldValue, value) =>
                {
                    modeOfOperation.Value = value;
                })
            .WithEnumField(position: 2, width: 2, enumField: out outputDataRate, name: "Output data rate ODR[1:0]",
                changeCallback: (oldValue, value) =>
                {
                    outputDataRate.Value = value;

                    // Check oldValue != value due to reset below
                    if(oldValue != value && magResdFeederThread != null)
                    {
                        magResdFeederThread.Frequency = GetOutputDataRateHz();
                    }
                })
            .WithTaggedFlag(position: 4, name: "Low-power mode (LP)")
            .WithFlag(position: 5, name: "Software reset (SOFT_RST)",
                valueProviderCallback: _ => false,
                writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        SoftwareReset();
                    }
                }
            )
            .WithTaggedFlag(position: 6, name: "Reboot memory content (REBOOT)")
            .WithTaggedFlag(position: 7, name: "Temperature compensation (COMP_TEMP_EN)")
        ;
        Registers.ConfigurationB.Define(this)
            .WithTaggedFlag(position: 0, name: "Low-pass filter (LPF)")
            .WithTaggedFlag(position: 1, name: "Offset cancellation (OFF_CANC)")
            .WithTaggedFlag(position: 2, name: "Set pulse frequency (Set_FREQ)")
            .WithTaggedFlag(position: 3, name: "INT_on_DataOFF")
            .WithTaggedFlag(position: 4, name: "OFF_CANC_ONE_SHOT")
            .WithReservedBits(position: 5, width: 3)
        ;
        Registers.ConfigurationC.Define(this)
            .WithFlag(position: 0, name: "DRDY_on_PIN", flagField: out drdyOnPin)
            .WithTaggedFlag(position: 1, name: "Self_test")
            .WithReservedBits(position: 2, width: 1)
            .WithTaggedFlag(position: 3, name: "BLE")
            .WithTaggedFlag(position: 4, name: "BDU")
            .WithTaggedFlag(position: 5, name: "I2C_DIS")
            .WithFlag(position: 6, name: "INT_on_PIN", flagField: out interruptOnPin)
            .WithReservedBits(position: 7, width: 1)
        ;
        Registers.InterruptControl.Define(this, 0b11100000)
            .WithFlag(position: 0, name: "IEN", flagField: out interruptEnable)
            .WithFlag(position: 1, name: "IEL", flagField: out interruptLatched)
            .WithFlag(position: 2, name: "IEA", flagField: out interruptPolarity, writeCallback: (_, value) =>
            {
                interruptPolarity.Value = value;
                UpdateInterrupt();
            })
            .WithReservedBits(position: 3, width: 2)
            .WithFlag(position: 5, name: "ZIEN", flagField: out interruptEnableZ)
            .WithFlag(position: 6, name: "YIEN", flagField: out interruptEnableY)
            .WithFlag(position: 7, name: "XIEN", flagField: out interruptEnableX)
        ;

        Registers.InterruptSource.Define(this)
            .WithFlag(position: 0, mode: FieldMode.Read, name: "INT", flagField: out interrupt)
            .WithFlag(position: 1, mode: FieldMode.Read, name: "MROI")
            .WithFlag(position: 2, mode: FieldMode.Read, name: "N_TH_S_Z", valueProviderCallback: _ => NegativeThresholdExceededZ)
            .WithFlag(position: 3, mode: FieldMode.Read, name: "N_TH_S_Y", valueProviderCallback: _ => NegativeThresholdExceededY)
            .WithFlag(position: 4, mode: FieldMode.Read, name: "N_TH_S_X", valueProviderCallback: _ => NegativeThresholdExceededX)
            .WithFlag(position: 5, mode: FieldMode.Read, name: "P_TH_S_Z", valueProviderCallback: _ => PositiveThresholdExceededZ)
            .WithFlag(position: 6, mode: FieldMode.Read, name: "P_TH_S_Y", valueProviderCallback: _ => PositiveThresholdExceededY)
            .WithFlag(position: 7, mode: FieldMode.Read, name: "P_TH_S_X", valueProviderCallback: _ => PositiveThresholdExceededX)
            .WithReadCallback((_, __) =>
            {
                if(interruptLatched.Value)
                {
                    RegistersCollection.ResetRegister((long)Registers.InterruptSource);
                }
            })
        ;
        Registers.InterruptThresholdLow.Define(this)
            .WithValueField(0, 8, name: "INT_THS_L_REG",
                valueProviderCallback: _ => (byte)BitHelper.GetValue(interruptThreshold, 0, 8),
                writeCallback: (_, value) => BitHelper.ReplaceBits(ref interruptThreshold, source: (uint)value, width: 8, destinationPosition: 0)
            )
        ;
        Registers.InterruptThresholdHigh.Define(this)
            .WithValueField(0, 8, name: "INT_THS_H_REG",
                valueProviderCallback: _ => (byte)BitHelper.GetValue(interruptThreshold, 8, 8),
                writeCallback: (_, value) => BitHelper.ReplaceBits(ref interruptThreshold, source: (uint)value, width: 8, destinationPosition: 8)
            )
        ;
        Registers.Status.Define(this)
            .WithFlag(position: 0, mode: FieldMode.Read, name: "xda", flagField: out newDataAvailableX)
            .WithFlag(position: 1, mode: FieldMode.Read, name: "yda", flagField: out newDataAvailableY)
            .WithFlag(position: 2, mode: FieldMode.Read, name: "zda", flagField: out newDataAvailableZ)
            .WithFlag(position: 3, mode: FieldMode.Read, name: "Zyxda", valueProviderCallback: _ => NewDataAvailableAll)
            .WithTaggedFlag(position: 4, name: "xor")
            .WithTaggedFlag(position: 5, name: "yor")
            .WithTaggedFlag(position: 6, name: "zor")
            .WithTaggedFlag(position: 7, name: "Zyxor")
        ;
        Registers.XAxisDataLow.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "OUTX_L_REG",
                valueProviderCallback: _ => (byte)BitHelper.GetValue((uint)(MagneticFluxDensityX / SensorSensitivity), 0, 8)
            )
            .WithReadCallback((_, __) =>
            {
                newDataAvailableX.Value = false;
                UpdateGPIOPin();
            })
        ;
        Registers.XAxisDataHigh.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "OUTX_H_REG",
                valueProviderCallback: _ => (byte)BitHelper.GetValue((uint)(MagneticFluxDensityX / SensorSensitivity), 8, 8)
            )
            .WithReadCallback((_, __) =>
            {
                newDataAvailableX.Value = false;
                UpdateGPIOPin();
            })
        ;
        Registers.YAxisDataLow.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "OUTY_L_REG",
                valueProviderCallback: _ => (byte)BitHelper.GetValue((uint)(MagneticFluxDensityY / SensorSensitivity), 0, 8)
            )
            .WithReadCallback((_, __) =>
            {
                newDataAvailableY.Value = false;
                UpdateGPIOPin();
            })
        ;
        Registers.YAxisDataHigh.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "OUTY_H_REG",
                valueProviderCallback: _ => (byte)BitHelper.GetValue((uint)(MagneticFluxDensityY / SensorSensitivity), 8, 8)
            )
            .WithReadCallback((_, __) =>
            {
                newDataAvailableY.Value = false;
                UpdateGPIOPin();
            })
        ;
        Registers.ZAxisDataLow.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "OUTZ_L_REG",
                valueProviderCallback: _ => (byte)BitHelper.GetValue((uint)(MagneticFluxDensityZ / SensorSensitivity), 0, 8)
            )
            .WithReadCallback((_, __) =>
            {
                newDataAvailableZ.Value = false;
                UpdateGPIOPin();
            })
        ;
        Registers.ZAxisDataHigh.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "OUTZ_H_REG",
                valueProviderCallback: _ => (byte)BitHelper.GetValue((uint)(MagneticFluxDensityZ / SensorSensitivity), 8, 8)
            )
            .WithReadCallback((_, __) =>
            {
                newDataAvailableZ.Value = false;
                UpdateGPIOPin();
            })
        ;
        Registers.InternalTemperatureLow.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "TEMP_OUT_L_REG")
        ;
        Registers.InternalTemperatureHigh.Define(this)
            .WithValueField(0, 8, FieldMode.Read, name: "TEMP_OUT_H_REG")
        ;
    }

    private void SoftwareReset()
    {
        RegistersCollection.Reset();
        hardIronOffsetX = 0;
        hardIronOffsetY = 0;
        hardIronOffsetZ = 0;
        selectedRegister = Registers.HardIronOffsetXLow;
        state = I2CState.WaitingForRegister;
        i2cAutoIncrement = false;
        UpdateGPIOPin();
    }

    private void I2CAutoIncrement()
    {
        if(!i2cAutoIncrement)
        {
            return;
        }
        selectedRegister = GetNextRegister(selectedRegister);
    }

    private Registers GetNextRegister(Registers currentRegister)
    {
        switch(currentRegister)
        {
        case Registers.HardIronOffsetZHigh:
            return Registers.Identification;
        case Registers.Identification:
            return Registers.ConfigurationA;
        case Registers.InternalTemperatureHigh:
            return Registers.HardIronOffsetXLow;
        default:
            return currentRegister + 1;
        }
    }

    private void UpdateInterrupt()
    {
        var interruptActive = interruptPolarity.Value == interrupt.Value;
        if(interruptLatched.Value && interruptActive)
        {
            return;
        }

        if(!interruptEnable.Value)
        {
            return;
        }

        // "Logical" interrupt value, we flip it later if the polarity is active low
        interrupt.Value = false;
        interrupt.Value |= newDataAvailableX.Value && interruptEnableX.Value && (NegativeThresholdExceededX || PositiveThresholdExceededX);
        interrupt.Value |= newDataAvailableY.Value && interruptEnableY.Value && (NegativeThresholdExceededY || PositiveThresholdExceededY);
        interrupt.Value |= newDataAvailableZ.Value && interruptEnableZ.Value && (NegativeThresholdExceededZ || PositiveThresholdExceededZ);

        if(interruptPolarity.Value == false)
        {
            interrupt.Value = !interrupt.Value;
        }

        if(!interruptLatched.Value && interruptPolarity.Value == interrupt.Value)
        {
            // If the interrupt is not latched, toggle/pulse the pin
            UpdateGPIOInterrupt();
            interrupt.Value = !interrupt.Value;
            UpdateGPIOInterrupt();
        }
        else
        {
            UpdateGPIOInterrupt();
        }
    }

    private void UpdateGPIOInterrupt()
    {
        if(!interruptOnPin.Value)
        {
            return;
        }

        InterruptAndDataReady.Set(interrupt.Value);
    }

    private void UpdateGPIOPin()
    {
        switch(drdyOnPin.Value, interruptOnPin.Value)
        {
        case (false, false):
            InterruptAndDataReady.Set(false);
            break;
        case (true, false):
            InterruptAndDataReady.Set(NewDataAvailableAll);
            break;
        case (false, true) or (true, true):
            UpdateGPIOInterrupt();
            break;
        }
    }

    private void FetchDataAndUpdateState()
    {
        if(modeOfOperation.Value == ModeOfOperation.Idle0 || modeOfOperation.Value == ModeOfOperation.Idle1)
        {
            this.NoisyLog("Sensor is in idle mode, not fetching data.");
            return;
        }
        newDataAvailableX.Value = true;
        newDataAvailableY.Value = true;
        newDataAvailableZ.Value = true;
        if(modeOfOperation.Value == ModeOfOperation.Single)
        {
            modeOfOperation.Value = ModeOfOperation.Idle0;
        }
        UpdateInterrupt();
        UpdateGPIOPin();
    }

    private uint GetOutputDataRateHz()
    {
        return outputDataRate.Value switch
        {
            OutputDataRate._10Hz => 10,
            OutputDataRate._20Hz => 20,
            OutputDataRate._50Hz => 50,
            OutputDataRate._100Hz => 100,
            _ => throw new ArgumentOutOfRangeException($"Invalid output data rate value: {outputDataRate.Value}")
        };
    }

    private bool NegativeThresholdExceededZ => magneticFluxDensityZ < -interruptThreshold;

    private bool NegativeThresholdExceededY => magneticFluxDensityY < -interruptThreshold;

    private bool NegativeThresholdExceededX => magneticFluxDensityX < -interruptThreshold;

    private bool PositiveThresholdExceededZ => magneticFluxDensityZ > interruptThreshold;

    private bool PositiveThresholdExceededY => magneticFluxDensityY > interruptThreshold;

    private bool PositiveThresholdExceededX => magneticFluxDensityX > interruptThreshold;

    private bool NewDataAvailableAll => newDataAvailableX.Value && newDataAvailableY.Value && newDataAvailableZ.Value;

    private RESDStream<MagneticSample> magResdStream;
    private IManagedThread magResdFeederThread;

    private I2CState state = I2CState.WaitingForRegister;
    private Registers selectedRegister;
    private bool i2cAutoIncrement;

    private uint interruptThreshold;
    private int magneticFluxDensityX;
    private int magneticFluxDensityY;
    private int magneticFluxDensityZ;
    private uint hardIronOffsetX;
    private uint hardIronOffsetZ;
    private uint hardIronOffsetY;

    private IEnumRegisterField<ModeOfOperation> modeOfOperation;
    private IFlagRegisterField drdyOnPin;
    private IFlagRegisterField interruptOnPin;
    private IFlagRegisterField interrupt;
    private IFlagRegisterField interruptEnable;
    private IFlagRegisterField interruptPolarity;
    private IFlagRegisterField interruptEnableZ;
    private IFlagRegisterField interruptEnableY;
    private IFlagRegisterField interruptEnableX;
    private IFlagRegisterField newDataAvailableX;
    private IFlagRegisterField newDataAvailableY;
    private IFlagRegisterField newDataAvailableZ;
    private IEnumRegisterField<OutputDataRate> outputDataRate;
    private IFlagRegisterField interruptLatched;

    private const int SensorSensitivity = 150; // 1.5 mgauss/LSB = 150 nT/LSB

    public enum Registers
    {
        // Reserved: 0x00 - 0x44
        HardIronOffsetXLow = 0x45, // OFFSET_X_REG_L
        HardIronOffsetXHigh = 0x46, // OFFSET_X_REG_H
        HardIronOffsetYLow = 0x47, // OFFSET_Y_REG_L
        HardIronOffsetYHigh = 0x48, // OFFSET_Y_REG_H
        HardIronOffsetZLow = 0x49, // OFFSET_Z_REG_L
        HardIronOffsetZHigh = 0x4A, // OFFSET_Z_REG_H
        // Reserved: 0x4B - 0x4C
        Identification = 0x4F, // WHO_AM_I
        // Reserved: 0x50 - 0x5F
        ConfigurationA = 0x60, // CFG_REG_A
        ConfigurationB = 0x61, // CFG_REG_B
        ConfigurationC = 0x62, // CFG_REG_C
        InterruptControl = 0x63, // INT_CRTL_REG
        InterruptSource = 0x64, // INT_SOURCE_REG
        InterruptThresholdLow = 0x65, // INT_THS_L_REG
        InterruptThresholdHigh = 0x66, // INT_THS_H_REG
        Status = 0x67, // STATUS_REG
        XAxisDataLow = 0x68, // OUTX_L_REG
        XAxisDataHigh = 0x69, // OUTX_H_REG
        YAxisDataLow = 0x6A, // OUTY_L_REG
        YAxisDataHigh = 0x6B, // OUTY_H_REG
        ZAxisDataLow = 0x6C, // OUTZ_L_REG
        ZAxisDataHigh = 0x6D, // OUTZ_H_REG
        InternalTemperatureLow = 0x6E, // TEMP_OUT_L_REG
        InternalTemperatureHigh = 0x6F // TEMP_OUT_H_REG
    }

    private enum OutputDataRate
    {
        _10Hz = 0b00,
        _20Hz = 0b01,
        _50Hz = 0b10,
        _100Hz = 0b11
    }

    private enum ModeOfOperation
    {
        Continuous = 0b00,
        Single = 0b01,
        Idle0 = 0b10,
        Idle1 = 0b11,
    }

    private enum I2CState
    {
        WaitingForRegister,
        WritingData,
        Reading,
    }
}