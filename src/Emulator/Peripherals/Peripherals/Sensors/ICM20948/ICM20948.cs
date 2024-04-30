//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public partial class ICM20948 : II2CPeripheral, ISPIPeripheral, IGPIOReceiver, IProvidesRegisterCollection<ByteRegisterCollection>, IPeripheralContainer<II2CPeripheral, NumberRegistrationPoint<int>>
    {
        public ICM20948(IMachine machine)
        {
            this.machine = machine;
            IRQ = new GPIO();

            gyroAccelUserBank0Registers = new ByteRegisterCollection(this);
            gyroAccelUserBank1Registers = new ByteRegisterCollection(this);
            gyroAccelUserBank2Registers = new ByteRegisterCollection(this);
            gyroAccelUserBank3Registers = new ByteRegisterCollection(this);

            i2cContainer = new SimpleContainerHelper<II2CPeripheral>(machine, this);

            userBankRegisters = new Dictionary<ulong, ByteRegisterCollection>
            {
                {0, gyroAccelUserBank0Registers},
                {1, gyroAccelUserBank1Registers},
                {2, gyroAccelUserBank2Registers},
                {3, gyroAccelUserBank3Registers},
            };

            DefineGyroAccelUserBank0Registers();
            DefineGyroAccelUserBank1Registers();
            DefineGyroAccelUserBank2Registers();
            DefineGyroAccelUserBank3Registers();
        }

        public void Write(byte[] data)
        {
            var offset = 0;
            this.NoisyLog("Write {0}", data.ToLazyHexString());

            switch(state)
            {
                case State.Idle:
                    selectedRegister = data[offset++];
                    this.DebugLog("Selected bank #{0} register 0x{1:X} ({2})", userBankSelected, selectedRegister, SelectedRegisterName);

                    state = State.ReceivedFirstByte;

                    if(data.Length == offset)
                    {
                        break;
                    }
                    goto case State.ReceivedFirstByte;

                case State.ReceivedFirstByte:
                case State.WritingWaitingForValue:
                    var startingRegister = selectedRegister;

                    foreach(var b in data.Skip(offset))
                    {
                        RegistersCollection.Write(selectedRegister++, b);
                    }

                    this.DebugLog("Write at bank #{0} from 0x{1:X} to 0x{2:X}: data {3}", userBankSelected, startingRegister, selectedRegister, SelectedRegisterName, data.Skip(offset).ToLazyHexString());
                    state = State.WritingWaitingForValue;
                    break;

                case State.Reading:
                    // Reads are able to use address set during write transfer, the opposite isn't true
                    this.WarningLog("Trying to write without specifying address, ignoring write");
                    break;

                default:
                    this.ErrorLog("Attempted write in an unexpected state: {0}", state);
                    break;
            }
        }

        public byte[] Read(int count)
        {
            state = State.Reading; // Read can be started regardless of state, last selectedRegister is used

            var startingRegister = selectedRegister;
            this.DebugLog("Staring read of {0} bytes at bank #{1} from 0x{2:X} ({3})", count, userBankSelected, selectedRegister, SelectedRegisterName);

            var result = Misc.Iterate(() => RegistersCollection.Read(selectedRegister++)).Take(count).ToArray();

            this.NoisyLog("Read at bank #{0} from 0x{1:X} to 0x{2:X}, returned {3}", userBankSelected, startingRegister, selectedRegister, Misc.PrettyPrintCollectionHex(result));
            return result;
        }

        void II2CPeripheral.FinishTransmission()
        {
            if(state != State.ReceivedFirstByte) // in case of reading we may (documentation permits this or repeated START) receive STOP before the read transfer
            {
                state = State.Idle;
            }
        }

        public byte Transmit(byte data)
        {
            byte result = 0;

            switch(state)
            {
                case State.Idle:
                    selectedRegister = BitHelper.GetValue(data, 0, 7);
                    var isRead = BitHelper.IsBitSet(data, 7);

                    if(isRead)
                    {
                        this.NoisyLog("Preparing for read at bank #{0} from 0x{1:X} ({2})", userBankSelected, selectedRegister, SelectedRegisterName);
                        state = State.Reading;
                        break;
                    }

                    this.NoisyLog("Preparing for write at bank #{0} to 0x{1:X} ({2})", userBankSelected, selectedRegister, SelectedRegisterName);
                    state = State.Writing;
                    break;

                case State.Reading:
                    result = RegistersCollection.Read(selectedRegister);
                    this.NoisyLog("Read at bank #{0} from 0x{1:X} ({2}), returned 0x{3:X}", userBankSelected, selectedRegister, SelectedRegisterName, result);
                    selectedRegister++;
                    break;

                case State.Writing:
                    this.DebugLog("Write at bank #{0} to 0x{1:X} ({2}): value 0x{3:X}", userBankSelected, selectedRegister, SelectedRegisterName, data);
                    RegistersCollection.Write(selectedRegister++, data);
                    break;

                default:
                    this.ErrorLog("Attempted transmission in an unexpected state: {0}", state);
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
                this.WarningLog("This model supports only CS on pin 0, but got signal on pin {0}", number);
                return;
            }

            var previousChipSelected = chipSelected;
            chipSelected = !value;

            if(previousChipSelected && !chipSelected)
            {
                ((ISPIPeripheral)this).FinishTransmission();
            }
        }

        public void Reset()
        {
            SoftwareReset();

            accelerometerFeederThread?.Stop();
            accelerometerResdStream?.Dispose();
            accelerometerResdStream = null;
            accelerometerFeederThread = null;

            gyroFeederThread?.Stop();
            gyroResdStream?.Dispose();
            gyroResdStream = null;
            gyroFeederThread = null;

            magFeederThread?.Stop();
            magFeederThread?.Dispose();
            magFeederThread = null;
        }

        public void SoftwareReset()
        {
            gyroAccelUserBank0Registers.Reset();
            gyroAccelUserBank1Registers.Reset();
            gyroAccelUserBank2Registers.Reset();
            gyroAccelUserBank3Registers.Reset();

            chipSelected = false;
            selectedRegister = 0x0;
            state = State.Idle;
        }

        public ByteRegisterCollection RegistersCollection => userBankRegisters.GetOrDefault(userBankSelected, userBankRegisters[0]);

        public GPIO IRQ { get; }

        public virtual void Register(II2CPeripheral peripheral, NumberRegistrationPoint<int> registrationPoint) => i2cContainer.Register(peripheral, registrationPoint);

        public virtual void Unregister(II2CPeripheral peripheral) => i2cContainer.Unregister(peripheral);

        public IEnumerable<NumberRegistrationPoint<int>> GetRegistrationPoints(II2CPeripheral peripheral) => i2cContainer.GetRegistrationPoints(peripheral);

        IEnumerable<IRegistered<II2CPeripheral, NumberRegistrationPoint<int>>> IPeripheralContainer<II2CPeripheral, NumberRegistrationPoint<int>>.Children =>
            i2cContainer.Children;

        private void UpdateInterrupts()
        {
            var dmpInterrupt = digitalMotionProcessorInterruptEnabled.Value && digitalMotionProcessorInterruptStatus.Value;
            var wakeOnMotionInterrupt = wakeOnMotionInterruptEnabled.Value && wakeOnMotionInterruptStatus.Value;
            var pllReadyInterrupt = pllReadyInterruptEnabled.Value && pllReadyInterruptStatus.Value;
            var i2cMasterInterrupt = i2cMasterInterruptEnabled.Value && i2cMasterInterruptStatus.Value;
            var rawDataReadyInterrupt = rawDataReadyInterruptEnabled.Value && rawDataReadyInterruptStatus.Value;
            var fifoOverflowInterrupt = fifoOverflowInterruptEnabled.Value && fifoOverflowInterruptStatus.Value;
            var fifoWatermarkInterrupt = fifoWatermarkInterruptEnabled.Value && fifoWatermarkInterruptStatus.Value;

            var irq = dmpInterrupt
                | wakeOnMotionInterrupt
                | pllReadyInterrupt
                | i2cMasterInterrupt
                | rawDataReadyInterrupt
                | fifoOverflowInterrupt
                | fifoWatermarkInterrupt
            ;

            this.Log(LogLevel.Debug, "IRQ {0}", irq ? "set" : "unset");
            this.Log(LogLevel.Noisy, "Interrupts: DMP {0}, Wake on Motion {1}, PLL RDY {2}, I2C Master {3}, Raw Data Ready {4}, FIFO Overflow {5}, FIFO Watermark {6}",
                dmpInterrupt, wakeOnMotionInterrupt, pllReadyInterrupt, i2cMasterInterrupt, rawDataReadyInterrupt, fifoOverflowInterrupt, fifoWatermarkInterrupt);
            IRQ.Set(irq);
        }

        private string RegisterIndexToString(int register, ulong bank)
        {
            switch(bank)
            {
                case 0:
                    return ((GyroAccelUserBank0Registers)register).ToString();
                case 1:
                    return ((GyroAccelUserBank1Registers)register).ToString();
                case 2:
                    return ((GyroAccelUserBank2Registers)register).ToString();
                case 3:
                    return ((GyroAccelUserBank3Registers)register).ToString();
                default:
                    throw new Exception("Unreachable");
            }
        }

        private void DefineBankSelectRegister(ByteRegisterCollection registers)
        {
            // this register is the same for all user banks
            // so there is no difference which enum we use
            GyroAccelUserBank0Registers.RegisterBankSelection.Define(registers)
                .WithReservedBits(6, 2)
                .WithValueField(4, 2, writeCallback: (_, value) => userBankSelected = value, name: "USER_BANK")
                .WithReservedBits(0, 4);
        }

        private ushort ConvertMeasurement(decimal value, Func<decimal, decimal> converter)
        {
            var converted = converter(value);
            var rounded = Math.Round(converted);
            var clamped = (short)rounded.Clamp(short.MinValue, short.MaxValue);
            return (ushort)clamped;
        }

        private string SelectedRegisterName => RegisterIndexToString(selectedRegister, userBankSelected);

        private readonly IMachine machine;
        private readonly SimpleContainerHelper<II2CPeripheral> i2cContainer;

        private bool chipSelected;
        private byte selectedRegister;
        private ulong userBankSelected;
        private State state;

        private readonly IReadOnlyDictionary<ulong, ByteRegisterCollection> userBankRegisters;
        private readonly ByteRegisterCollection gyroAccelUserBank0Registers;
        private readonly ByteRegisterCollection gyroAccelUserBank1Registers;
        private readonly ByteRegisterCollection gyroAccelUserBank2Registers;
        private readonly ByteRegisterCollection gyroAccelUserBank3Registers;

        private const int NumberOfExternalSlaveSensorDataRegisters = 24;
        private const decimal InternalSampleRateHz = 1125;
        private const int MaxFifoBytes = 4096;

        private enum State
        {
            Idle,
            Reading,
            Writing,
            ReceivedFirstByte,
            WaitingForAddress,
            WritingWaitingForValue,
        }

        private enum FifoMode
        {
            Stream = 0,
            Snapshot = 1
        }
    }
}
