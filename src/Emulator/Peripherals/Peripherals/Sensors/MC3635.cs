//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.

using System;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class MC3635 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ISensor
    {
        public MC3635(IMachine machine)
        {
            this.machine = machine;
            RegistersCollection = new ByteRegisterCollection(this);
            accelerationFifo = new SensorSamplesFifo<Vector3DSample>();
            DefineRegisters();
            IRQ = new GPIO();
        }

        public void Reset()
        {
            initialized = false;
            maximumValue = MaximumValue.bits6;
            range = GRange.G2;
            mode.Value = Modes.Sleep;
            continuousPowerMode.Value = PowerModes.Low;
            sniffPowerMode.Value = PowerModes.Low;

            registerAddress = 0;
            samplingRate = 100;
            samplingRateRegister.Value = 0;
            sampleRead = true;
            interruptClearOnlyOnWrite.Value = false;
            dataOverwritten.Value = false;
            wrapAfterStatus.Value = false;

            xAxisDisabled.Value = false;
            yAxisDisabled.Value = false;
            zAxisDisabled.Value = false;

            ClearInterrupts();
            StopCollection();
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Unexpected write with no data");
                return;
            }

            registerAddress = data[0];
            this.Log(LogLevel.Noisy, "Register set to {0}", (Registers)registerAddress);
            
            if(data.Length > 2)
            {
                this.Log(LogLevel.Error, "Received write transaction with multiple bytes to write. As burst writes are not specified for this peripheral, redundant bytes will be ignored.");
            }
            
            if(data.Length > 1)
            {
                RegistersCollection.Write(registerAddress, data[1]);
                this.Log(LogLevel.Noisy, "Writing 0x{0:X}", data[1]);
            }
        }

        // For the purpose of direct acces from the monitor
        public void RegisterWrite(byte offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        // For the purpose of direct acces from the monitor
        public byte RegisterRead(byte offset)
        {
            return RegistersCollection.Read(offset);
        }

        public byte[] Read(int count = 0)
        {
            var ret = new byte[count];

            for(int i = 0; i < count; i++)
            {
                ret[i] = RegistersCollection.Read(registerAddress);
                this.Log(LogLevel.Noisy, "Reading from {0}: 0x{1:X}", (Registers)registerAddress, ret[i]);

                if(registerAddress == (uint)(wrapAfterStatus.Value ? Registers.Status2 : Registers.ZoutMsb))
                {
                    registerAddress = (byte)Registers.XoutLsb;
                }
                else
                {
                    registerAddress++;
                }
            }
            return ret;
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
            // Intentionally left blank
        }

        public decimal CurrentAccelerationX
        {
            get => accelerationFifo.Sample.X;
        }
        
        public decimal CurrentAccelerationY
        {
            get => accelerationFifo.Sample.Y;
        }
        
        public decimal CurrentAccelerationZ
        {
            get => accelerationFifo.Sample.Z;
        }
        
        public decimal DefaultAccelerationX
        {
            get => accelerationFifo.DefaultSample.X;
            set
            {
                accelerationFifo.DefaultSample.X = value;
            }
        }

        public decimal DefaultAccelerationY
        {
            get => accelerationFifo.DefaultSample.Y;
            set
            {
                accelerationFifo.DefaultSample.Y = value;
            }
        }

        public decimal DefaultAccelerationZ
        {
            get => accelerationFifo.DefaultSample.Z;
            set
            {
                accelerationFifo.DefaultSample.Z = value;
            }
        }

        public GPIO IRQ { get; }

        public ByteRegisterCollection RegistersCollection { get; }

        private void UpdateInterrupts()
        {
            var wake          = wakeInterrupt.Value && wakeInterruptEnable.Value;
            var acquired      = acquiredInterrupt.Value && acquiredInterruptEnable.Value;
            var fifoEmpty     = fifoEmptyInterrupt.Value && fifoEmptyInterruptEnable.Value;
            var fifoFull      = fifoFullInterrupt.Value && fifoFullInterruptEnable.Value;
            var fifoThreshold = fifoThresholdInterrupt.Value && fifoThresholdInterruptEnable.Value;
            var sniff         = sniffInterrupt.Value && sniffInterruptEnable.Value;

            IRQ.Set(wake || acquired || fifoEmpty || fifoFull || fifoThreshold || sniff);
        }

        private bool IsInterruptPending()
        {
            return wakeInterrupt.Value || acquiredInterrupt.Value || fifoEmptyInterrupt.Value
                || fifoFullInterrupt.Value || fifoThresholdInterrupt.Value || sniffInterrupt.Value;
        }

        private void RaiseInterrupt(ref IFlagRegisterField interrupt)
        {
            interrupt.Value = true;
            UpdateInterrupts();
        }

        private void ClearInterrupts()
        {
            wakeInterrupt.Value = false;
            acquiredInterrupt.Value = false;
            fifoEmptyInterrupt.Value = false;
            fifoFullInterrupt.Value = false;
            fifoThresholdInterrupt.Value = false;
            sniffInterrupt.Value = false;
            UpdateInterrupts();
        }

        private void LoadNextSample()
        {
            this.Log(LogLevel.Debug, "Acquiring next sample");
            if(sampleRead)
            {
                sampleRead = false;
                dataOverwritten.Value = false;
            }
            else
            {
                dataOverwritten.Value = true;
                this.Log(LogLevel.Warning, "Data Overwritten");
            }
            accelerationFifo.TryDequeueNewSample();
            RaiseInterrupt(ref acquiredInterrupt);
        }

        private void StartSampleCollection()
        {
            StopCollection();
            LoadNextSample();

            samplingThread = machine.ObtainManagedThread(LoadNextSample, samplingRate);
            samplingThread.Start();
            this.Log(LogLevel.Debug, "Sample acquisition thread started");
        }

        private void StopCollection()
        {
            if(samplingThread != null)
            {
                samplingThread.Dispose();
                samplingThread = null;
                this.Log(LogLevel.Debug, "Sample acquisition thread stopped");
            }
        }

        private byte GetScaledValue(decimal value, MaximumValue maximumVal, GRange range, bool upperByte)
        {
            var scaled = (short)(value * (uint)maximumVal / (uint)range);
            sampleRead = true;
            return upperByte
                ? (byte)(scaled >> 8)
                : (byte)scaled;
        }

        private void SetSamplingFrequency()
        {
            if(samplingRateRegister.Value == 0xF)
            {
                this.Log(LogLevel.Warning, "This model does not confirm if all necessary steps for setting the highest sampling rate ware taken. Please make sure to follow the flow from '(0X11) RATE REGISTER 1` chapter");
            }

            var currentMode = (mode.Value == Modes.Sniff ? sniffPowerMode : continuousPowerMode);

            switch(currentMode.Value)
            {
                // As there is no pattern, all possibilities must be handled manually
                case PowerModes.UltraLow:
                    switch(samplingRateRegister.Value)
                    {
                        case 0x6:
                            samplingRate = 25; break;
                        case 0x7:
                            samplingRate = 50; break;
                        case 0x8:
                            samplingRate = 100; break;
                        case 0x9:
                            samplingRate = 190; break;
                        case 0xA:
                            samplingRate = 380; break;
                        case 0xB:
                            samplingRate = 750; break;
                        case 0xC:
                            samplingRate = 1100; break;
                        case 0xF:
                            samplingRate = 1300; break;
                        default:
                            this.Log(LogLevel.Error, "0x{0:X} is not a legal RATE setting in {1} power mode. Setting the lowest possible value", samplingRateRegister.Value, continuousPowerMode);
                            goto case 0x6;
                    }
                    break;
                case PowerModes.Low:
                    switch(samplingRateRegister.Value)
                    {
                        case 0x5:
                            samplingRate = 14; break;
                        case 0x6:
                            samplingRate = 28; break;
                        case 0x7:
                            samplingRate = 54; break;
                        case 0x8:
                            samplingRate = 105; break;
                        case 0x9:
                            samplingRate = 210; break;
                        case 0xA:
                            samplingRate = 400; break;
                        case 0xB:
                            samplingRate = 600; break;
                        case 0xF:
                            samplingRate = 750; break;
                        default:
                            this.Log(LogLevel.Error, "0x{0:X} is not a legal RATE setting in {1} power mode. Setting the lowest possible value", samplingRateRegister.Value, continuousPowerMode);
                            goto case 0x5;
                    }
                    break;
                case PowerModes.Precision:
                    switch(samplingRateRegister.Value)
                    {
                        case 0x5:
                            samplingRate = 14; break;
                        case 0x6:
                            samplingRate = 28; break;
                        case 0x7:
                            samplingRate = 55; break;
                        case 0x8:
                            samplingRate = 80; break;
                        case 0xF:
                            samplingRate = 100; break;
                        default:
                            this.Log(LogLevel.Error, "0x{0:X} is not a legal RATE setting in {1} power mode. Setting the lowest possible value", samplingRateRegister.Value, continuousPowerMode);
                            goto case 0x5;
                    }
                    break;
                default:
                    throw new ArgumentException($"Invalid power mode: {continuousPowerMode}");
            }
            this.Log(LogLevel.Debug, "Sampling rate set to {0} Hz", samplingRate);
        }

        private void DefineRegisters()
        {
            Registers.ExtendedStatus1.Define(this)
                .WithReservedBits(0, 3)
                .WithTag("I2C_AD0", 3, 1)
                .WithReservedBits(4, 4);
            Registers.ExtendedStatus2.Define(this, 0x4)
                .WithFlag(0, out dataOverwritten, name: "OVR_DATA")
                .WithTaggedFlag("PD_CLK_STAT", 1)   // Clocks are disabled
                .WithReservedBits(2, 3)
                .WithTaggedFlag("OPT_BUSY", 5)      // OTP_VDD is enabled, OTP is powered
                .WithTaggedFlag("SNIFF_EN", 6)      // SNIFF mode is active
                .WithTaggedFlag("SNIFF_DETECT", 7); // Sniff event detected. move to CWAKE mode
            Registers.XoutLsb.Define(this)
                .WithValueField(0, 8, FieldMode.Read,
                    valueProviderCallback: _ => GetScaledValue(xAxisDisabled.Value ? 0 : accelerationFifo.Sample.X, maximumValue, range, upperByte: false),
                    name: "XOUT_LSB");
            Registers.XoutMsb.Define(this)
                .WithValueField(0, 8, FieldMode.Read,
                    valueProviderCallback: _ => GetScaledValue(xAxisDisabled.Value ? 0 : accelerationFifo.Sample.X, maximumValue, range, upperByte: true),
                    name: "XOUT_MSB");
            Registers.YoutLsb.Define(this)
                .WithValueField(0, 8, FieldMode.Read,
                    valueProviderCallback: _ => GetScaledValue(yAxisDisabled.Value ? 0 : accelerationFifo.Sample.Y, maximumValue, range, upperByte: false),
                    name: "YOUT_LSB");
            Registers.YoutMsb.Define(this)
                .WithValueField(0, 8, FieldMode.Read,
                    valueProviderCallback: _ => GetScaledValue(yAxisDisabled.Value ? 0 : accelerationFifo.Sample.Y, maximumValue, range, upperByte: true),
                    name: "YOUT_MSB");
            Registers.ZoutLsb.Define(this)
                .WithValueField(0, 8, FieldMode.Read,
                    valueProviderCallback: _ => GetScaledValue(zAxisDisabled.Value ? 0 : accelerationFifo.Sample.Z, maximumValue, range, upperByte: false),
                    name: "ZOUT_LSB");
            Registers.ZoutMsb.Define(this)
                .WithValueField(0, 8, FieldMode.Read,
                    valueProviderCallback: _ => GetScaledValue(zAxisDisabled.Value ? 0 : accelerationFifo.Sample.Z, maximumValue, range, upperByte: true),
                    name: "ZOUT_MSB");
            Registers.Status1.Define(this)
                .WithValueField(0, 3, FieldMode.Read, name: "MODE")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => !sampleRead, name: "NEW_DATA")
                .WithTaggedFlag("FIFO_EMPTY", 4)
                .WithTaggedFlag("FIFO_FULL", 5)
                .WithTaggedFlag("FIFO_THRESH", 6)
                .WithFlag(7, FieldMode.Read, valueProviderCallback: (_) => IsInterruptPending(), name: "INT_PEND");
            Registers.Status2.Define(this)
                .WithReservedBits(0, 2)
                .WithFlag(2, out wakeInterrupt, FieldMode.Read, name: "INT_WAKE")
                .WithFlag(3, out acquiredInterrupt, FieldMode.Read, name: "INT_ACQ")
                .WithFlag(4, out fifoEmptyInterrupt, FieldMode.Read, name: "INT_FIFO_EMPTY")
                .WithFlag(5, out fifoFullInterrupt, FieldMode.Read, name: "INT_FIFO_FULL")
                .WithFlag(6, out fifoThresholdInterrupt, FieldMode.Read, name: "INT_FIFO_THRESH")
                .WithFlag(7, out sniffInterrupt, FieldMode.Read, name: "INT_SWAKE")
                .WithReadCallback((_, __) => { if(!interruptClearOnlyOnWrite.Value) ClearInterrupts(); })
                .WithWriteCallback((_, __) => ClearInterrupts());
            Registers.Feature1.Define(this)
                .WithReservedBits(0, 3, 0b000)
                .WithTaggedFlag("FREEZE", 3)
                .WithTaggedFlag("INTSC_EN", 4)
                .WithTaggedFlag("SPI3_EN", 5)
                .WithFlag(6, FieldMode.Read, name: "I2C_EN")
                .WithFlag(7, writeCallback: (_, val) => { if(val) this.Log(LogLevel.Error, "SPI interface is not supported"); }, name: "SPI_EN");
            Registers.Feature2.Define(this)
                .WithFlag(0, out wrapAfterStatus, name: "WRAPA")
                .WithTaggedFlag("FIFO_BURST", 1)
                .WithTaggedFlag("SPI_STAT_EN", 2)
                .WithTaggedFlag("FIFO_STAT_EN", 3)
                .WithFlag(4, out interruptClearOnlyOnWrite, name: "I2CINT_WRCLRE")
                .WithTaggedFlag("FIFO_STREAM", 5)
                .WithTaggedFlag("EXT_TRIG_POL", 6)
                .WithTaggedFlag("EXT_TRIG_EN", 7);
            Registers.Initialization1.Define(this, 0x40)
                .WithValueField(0, 8, writeCallback: (_, val) => 
                    {
                        if(val == 0x42)
                        {
                            initialized = true;
                        }
                        else
                        {
                            this.Log(LogLevel.Error, "INIT_1 should always be written with 0x42, got 0x{0:x}", val); 
                        }
                    }, valueProviderCallback: (_) => { return initialized ? 0x43u : 0x40u; }, name: "INIT_1");
            Registers.ModeControl.Define(this)
                .WithEnumField(0, 3, out mode, writeCallback: (_, __) =>
                    {
                        switch(mode.Value)
                        {
                            case Modes.Cwake:
                                StartSampleCollection();
                                break;
                            case Modes.Sniff:
                            case Modes.Swake:
                                this.Log(LogLevel.Error, "{0} mode unimplemented. Switching to Standby", mode.Value);
                                mode.Value = Modes.Standby;
                                break;
                            default:
                                // SLEEP and STANDBY
                                StopCollection();
                                break;
                        }
                        this.Log(LogLevel.Debug, "Changed mode to {0}", mode);
                        SetSamplingFrequency();
                    }, name: "MCTRL")
                .WithReservedBits(3, 1)
                .WithFlag(4, out xAxisDisabled, name: "X_AXIS_PD")
                .WithFlag(5, out yAxisDisabled, name: "Y_AXIS_PD")
                .WithFlag(6, out zAxisDisabled, name: "Z_AXIS_PD")
                .WithTaggedFlag("TRIG_CMD", 7);
            Registers.Rate1.Define(this)
                .WithValueField(0, 4, out samplingRateRegister, writeCallback: (_, __) => SetSamplingFrequency(), name: "RATE_1")
                .WithReservedBits(4, 4);
            Registers.SniffControl.Define(this)
                .WithTag("SNIFF_RR", 0, 4)
                .WithReservedBits(4, 1, 0b0)
                .WithTag("STB_RATE", 5, 3);
            Registers.SniffThresholdControl.Define(this)
                .WithTag("SNIFF_TH", 0, 6)
                .WithTaggedFlag("SNIFF_AND_OR", 6)
                .WithTaggedFlag("SNIFF_MODE", 7);
            Registers.SniffConfiguration.Define(this)
                .WithTag("SNIFF_THARD", 0, 2)
                .WithTaggedFlag("SNIFF_CNTEN", 3)
                .WithTag("SNIFF_MUX", 4, 2)
                .WithTaggedFlag("SNIFF_RESET", 7);
            Registers.RangeResolutionControl.Define(this)
                .WithValueField(0, 3, writeCallback: (_, val) =>
                    {
                        switch(val)
                        {
                            case 0b000:  //6 bits
                                maximumValue = MaximumValue.bits6;
                                break;
                            case 0b001:  //7 bits
                                maximumValue = MaximumValue.bits7;
                                break;
                            case 0b010:  //8 bits
                                maximumValue = MaximumValue.bits8;
                                break;
                            case 0b011:  //10 bits
                                maximumValue = MaximumValue.bits10;
                                break;
                            case 0b100:  //12 bits
                                maximumValue = MaximumValue.bits12;
                                break;
                            case 0b101:  //14 bits (only 12-bits if FIFO enabled)
                                maximumValue = MaximumValue.bits14;
                                break;
                            default:
                                this.Log(LogLevel.Error, "Invalid RANGE.RES value. Setting to default");
                                goto case 0b000;
                        }
                        this.Log(LogLevel.Debug, "Bit width set to {0}", maximumValue);
                    }, name: "RES")
                .WithReservedBits(3, 1)
                .WithValueField(4, 3, writeCallback: (_, val) =>
                    {
                        switch(val)
                        {
                            case 0b000: // ±2g
                                range = GRange.G2;
                                break;
                            case 0b001: // ±4g
                                range = GRange.G4;
                                break;
                            case 0b010: // ±8g
                                range = GRange.G8;
                                break;
                            case 0b011: // ±16g
                                range = GRange.G16;
                                break;
                            case 0b100: // ±12g
                                range = GRange.G12;
                                break;
                            default:
                                this.Log(LogLevel.Error, "Invalid RANGE.RANGE value. Setting to default");
                                goto case 0b000;
                        }
                        this.Log(LogLevel.Debug, "Range set to {0}", range);
                    }, name: "RANGE")
                .WithReservedBits(7, 1);
            Registers.FifoControl.Define(this)
                .WithTag("FIFO_TH", 0, 4)
                .WithTaggedFlag("FIFO_MODE", 5)
                .WithTaggedFlag("FIFO_EN", 6)
                .WithTaggedFlag("FIFO_RESET", 7);
            Registers.InterruptControl.Define(this)
                .WithTaggedFlag("IPP", 0)
                .WithTaggedFlag("IAH", 1)
                .WithFlag(2, out wakeInterruptEnable, name: "INT_WAKE")
                .WithFlag(3, out acquiredInterruptEnable, name: "INT_ACQ")
                .WithFlag(4, out fifoEmptyInterruptEnable, name: "INT_FIFO_EMPTY")
                .WithFlag(5, out fifoFullInterruptEnable, name: "INT_FIFO_FULL")
                .WithFlag(6, out fifoThresholdInterruptEnable, name: "INT_FIFO_THRESH")
                .WithFlag(7, out sniffInterruptEnable, name: "INT_SWAKE")
                .WithWriteCallback((_, __) => UpdateInterrupts());
            Registers.Initialization3.Define(this)
                .WithTag("INIT_3", 0, 8)
                .WithWriteCallback((_, val) => { if(val != 0x0) this.Log(LogLevel.Error, "INIT_3 should always be written with 0x0, got 0x{0:x}", val); });
            Registers.Scratchpad.Define(this)
                .WithValueField(0, 8, name: "SCRATCH"); // Any value can be written and read-back, this is a part of the initialization
            Registers.PowerModeControl.Define(this)
                .WithEnumField(0, 2, out continuousPowerMode, writeCallback: (_ , __) => SetSamplingFrequency(), name: "CSPM")
                .WithReservedBits(3, 1)
                .WithEnumField(4, 3, out sniffPowerMode, writeCallback: (_ , __) => SetSamplingFrequency(), name: "SPM")
                .WithTaggedFlag("SPI_HS_EN", 7);
            Registers.DriveMotionX.Define(this)
                .WithReservedBits(0, 2, 0b01)
                .WithTaggedFlag("DPX", 2)
                .WithTaggedFlag("DNX", 3)
                .WithReservedBits(4, 4, 0b0000);
            Registers.DriveMotionY.Define(this)
                .WithReservedBits(0, 2, 0b00)
                .WithTaggedFlag("DPY", 2)
                .WithTaggedFlag("DNY", 3)
                .WithReservedBits(4, 4, 0b1000);
            Registers.DriveMotionZ.Define(this)
                .WithReservedBits(0, 2, 0b00)
                .WithTaggedFlag("DPZ", 2)
                .WithTaggedFlag("DNZ", 3)
                .WithReservedBits(4, 4, 0b0000);
            Registers.Reset.Define(this)
                .WithReservedBits(0, 6)
                .WithFlag(6, writeCallback: (_, val) => { if(val) Reset(); }, name: "RESET")
                .WithTaggedFlag("RELOAD", 7);
            Registers.Initialization2.Define(this)
                .WithTag("INIT_2", 0, 8)
                .WithWriteCallback((_, val) => { if(val != 0x0) this.Log(LogLevel.Error, "INIT_2 should always be written with 0x0, got 0x{0:x}", val); });
            Registers.TrigggerCount.Define(this)
                .WithTag("TRIGC", 0, 8);
            Registers.XOffsetLSB.Define(this)
                .WithTag("XOFFL", 0, 8);
            Registers.XOffsetMSB.Define(this)
                .WithTag("XOFFH", 0, 7)
                .WithTag("XGAINH", 7, 1);
            Registers.YOffsetLSB.Define(this)
                .WithTag("YOFFL", 0, 8);
            Registers.YOffsetMSB.Define(this)
                .WithTag("YOFFH", 0, 7)
                .WithTag("YGAINH", 7, 1);
            Registers.ZOffsetLSB.Define(this)
                .WithTag("ZOFFL", 0, 8);
            Registers.ZOffsetMSB.Define(this)
                .WithTag("ZOFFH", 0, 7)
                .WithTag("ZGAINH", 7, 1);
            Registers.GainX.Define(this)
                .WithTag("XGAINL", 0, 8);
            Registers.GainY.Define(this)
                .WithTag("YGAINL", 0, 8);
            Registers.GainZ.Define(this)
                .WithTag("ZGAINL", 0, 8);
        }

        private bool sampleRead;
        private bool initialized;
        private uint registerAddress;
        private uint samplingRate;
        private IValueRegisterField samplingRateRegister;

        private GRange range;
        private MaximumValue maximumValue;

        private IManagedThread samplingThread;
        private readonly IMachine machine;

        private readonly SensorSamplesFifo<Vector3DSample> accelerationFifo;

        private IEnumRegisterField<PowerModes> continuousPowerMode;
        private IEnumRegisterField<PowerModes> sniffPowerMode;
        private IEnumRegisterField<Modes> mode;

        private IFlagRegisterField dataOverwritten;
        private IFlagRegisterField interruptClearOnlyOnWrite;
        private IFlagRegisterField wrapAfterStatus;
        private IFlagRegisterField xAxisDisabled;
        private IFlagRegisterField yAxisDisabled;
        private IFlagRegisterField zAxisDisabled;

        private IFlagRegisterField acquiredInterruptEnable;
        private IFlagRegisterField fifoEmptyInterruptEnable;
        private IFlagRegisterField fifoFullInterruptEnable;
        private IFlagRegisterField fifoThresholdInterruptEnable;
        private IFlagRegisterField sniffInterruptEnable;
        private IFlagRegisterField wakeInterruptEnable;

        private IFlagRegisterField acquiredInterrupt;
        private IFlagRegisterField fifoEmptyInterrupt;
        private IFlagRegisterField fifoFullInterrupt;
        private IFlagRegisterField fifoThresholdInterrupt;
        private IFlagRegisterField sniffInterrupt;
        private IFlagRegisterField wakeInterrupt;

        private enum GRange: ushort
        {
            G2  = 2,
            G4  = 4,
            G8  = 8,
            G12 = 12,
            G16 = 16,
        }

        private enum MaximumValue
        {
            //Maixmum value that can be written with current bit width
            bits6  = (1 << 6) - 1,
            bits7  = (1 << 7) - 1,
            bits8  = (1 << 8) - 1,
            bits10 = (1 << 10) - 1,
            bits12 = (1 << 12) - 1,
            bits14 = (1 << 14) - 1,
        }

        private enum Modes
        {
            Sleep   = 0b000,
            Standby = 0b001,
            Sniff   = 0b010,
            Cwake   = 0b101,
            Swake   = 0b110,
        }

        private enum PowerModes
        {
            Low       = 0b000,
            UltraLow  = 0b011,
            Precision = 0b100,
        }

        private enum Registers : byte
        {
            ExtendedStatus1        = 0x00,
            ExtendedStatus2        = 0x01,
            XoutLsb                = 0x02,
            XoutMsb                = 0x03,
            YoutLsb                = 0x04,
            YoutMsb                = 0x05,
            ZoutLsb                = 0x06,
            ZoutMsb                = 0x07,
            Status1                = 0x08,
            Status2                = 0x09,
            // 0x0A – 0x0C RESERVED
            Feature1               = 0x0D,
            Feature2               = 0x0E,
            Initialization1        = 0x0F,
            ModeControl            = 0x10,
            Rate1                  = 0x11,
            SniffControl           = 0x12,
            SniffThresholdControl  = 0x13,
            SniffConfiguration     = 0x14,
            RangeResolutionControl = 0x15,
            FifoControl            = 0x16,
            InterruptControl       = 0x17,
            // 0x18 – 0x19 RESERVED
            Initialization3        = 0x1A,
            Scratchpad             = 0x1B,
            PowerModeControl       = 0x1C,
            // 0x1D – 0x1F RESERVED
            DriveMotionX           = 0x20,
            DriveMotionY           = 0x21,
            DriveMotionZ           = 0x22,
            // 0x23 RESERVED
            Reset                  = 0x24,
            // 0x25 – 0x27 RESERVED
            Initialization2        = 0x28,
            TrigggerCount          = 0x29,
            XOffsetLSB             = 0x2A,
            XOffsetMSB             = 0x2B,
            YOffsetLSB             = 0x2C,
            YOffsetMSB             = 0x2D,
            ZOffsetLSB             = 0x2E,
            ZOffsetMSB             = 0x2F,
            GainX                  = 0x30,
            GainY                  = 0x31,
            GainZ                  = 0x32,
            // 0x33 – 0x3F RESERVED
       }
    }
}
