//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.UART
{
    public class EFR32_USART : UARTBase, IDoubleWordPeripheral, IPeripheralContainer<ISPIPeripheral, NullRegistrationPoint>
    {
        public EFR32_USART(Machine machine, uint clockFrequency = 19000000) : base(machine)
        {
            TransmitIRQ = new GPIO();
            ReceiveIRQ = new GPIO();

            interruptsManager = new InterruptManager<Interrupt>(this);

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithEnumField(0, 1, out operationModeField, name: "SYNC")
                    .WithEnumField(5, 2, out oversamplingField, name: "OVS")},
                {(long)Registers.FrameFormat, new DoubleWordRegister(this, 0x1005)
                    .WithEnumField(8, 2, out parityBitModeField, name: "PARITY")
                    .WithEnumField(12, 2, out stopBitsModeField, name: "STOPBITS")},
                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithFlag(11, FieldMode.Set, writeCallback: (_, newValue) => { if(newValue){ ClearBuffer(); }}, name: "CLEARRX")
                    .WithFlag(3, FieldMode.Set, writeCallback: (_, newValue) => { if(newValue) transmitterEnableFlag.Value = false; }, name: "TXDIS")
                    .WithFlag(2, FieldMode.Set, writeCallback: (_, newValue) =>
                    {
                        if(newValue)
                        {
                            transmitterEnableFlag.Value = true;
                            interruptsManager.SetInterrupt(Interrupt.TransmitBufferLevel);
                        }
                    }, name: "TXEN")
                    .WithFlag(1, FieldMode.Set, writeCallback: (_, newValue) => { if(newValue) receiverEnableFlag.Value = false; }, name: "RXDIS")
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, newValue) => { if(newValue) receiverEnableFlag.Value = true; }, name: "RXEN")},
                {(long)Registers.Status, new DoubleWordRegister(this, 0x2040)
                    .WithFlag(0, out receiverEnableFlag, FieldMode.Read, name: "RXENS")
                    .WithFlag(1, out transmitterEnableFlag, FieldMode.Read, name: "TXENS")
                    .WithFlag(5, out transferCompleteFlag, FieldMode.Read, name: "TXC")
                    .WithFlag(7, out receiveDataValidFlag, FieldMode.Read, name: "RXDATAV")},
                {(long)Registers.ClockControl, new DoubleWordRegister(this)
                    .WithValueField(3, 20, out fractionalClockDividerField, name: "DIV")},
                {(long)Registers.RxBufferData, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, name: "RXDATA", valueProviderCallback: (_) => ReadBuffer())},
                {(long)Registers.TxBufferData, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, v) => HandleTxBufferData((byte)v))},
            };
            registersMap.Add((long)Registers.InterruptFlag, interruptsManager.GetMaskedInterruptFlagRegister<DoubleWordRegister>());
            registersMap.Add((long)Registers.InterruptEnable, interruptsManager.GetInterruptEnableRegister<DoubleWordRegister>());
            registersMap.Add((long)Registers.InterruptFlagSet, interruptsManager.GetInterruptSetRegister<DoubleWordRegister>());
            registersMap.Add((long)Registers.InterruptFlagClear, interruptsManager.GetInterruptClearRegister<DoubleWordRegister>());

            registers = new DoubleWordRegisterCollection(this, registersMap);

            uartClockFrequency = clockFrequency;
        }

        public override void Reset()
        {
            base.Reset();
            interruptsManager.Reset();
            spiSlaveDevice = null;
        }

        public void Register(ISPIPeripheral peripheral, NullRegistrationPoint registrationPoint)
        {
            if(spiSlaveDevice != null)
            {
                throw new RegistrationException("Cannot register more than one peripheral.");
            }
            Machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            spiSlaveDevice = peripheral;
        }

        public void Unregister(ISPIPeripheral peripheral)
        {
            if(peripheral != spiSlaveDevice)
            {
                throw new RegistrationException("Trying to unregister not registered device.");
            }

            Machine.UnregisterAsAChildOf(this, peripheral);
            spiSlaveDevice = null;
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(ISPIPeripheral peripheral)
        {
            if(peripheral != spiSlaveDevice)
            {
                throw new RegistrationException("Trying to obtain a registration point for a not registered device.");
            }

            return new[] { NullRegistrationPoint.Instance };
        }

        public void WriteDoubleWord(long address, uint value)
        {
            registers.Write(address, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public IEnumerable<IRegistered<ISPIPeripheral, NullRegistrationPoint>> Children
        {
            get
            {
                return new[] { Registered.Create(spiSlaveDevice, NullRegistrationPoint.Instance) };
            }
        }

        [IrqProvider("transmit irq", 0)]
        public GPIO TransmitIRQ { get; private set; }

        [IrqProvider("receive irq", 1)]
        public GPIO ReceiveIRQ { get; private set; }

        public override Parity ParityBit { get { return parityBitModeField.Value; } }

        public override Bits StopBits { get { return stopBitsModeField.Value; } }

        public override uint BaudRate
        {
            get
            {
                //This calculation, according to the documentation, could be written as:
                //return uartClockFrequency / (multiplier * (1 + (fractionalClockDividerField.Value << 3) / 256));
                //But the result differs much from the one calculated by the driver, most probably due to
                //invalid integer division.
                //This code mimics the one in emlib driver.
                var oversample = 1u;
                var factor = 0u;
                switch(oversamplingField.Value)
                {
                case OversamplingMode.Times16:
                    oversample = 1;
                    factor = 256 / 16;
                    break;
                case OversamplingMode.Times8:
                    oversample = 1;
                    factor = 256 / 8;
                    break;
                case OversamplingMode.Times6:
                    oversample = 3;
                    factor = 256 / 2;
                    break;
                case OversamplingMode.Times4:
                    oversample = 1;
                    factor = 256 / 4;
                    break;
                }
                var divisor = oversample * (256 + (fractionalClockDividerField.Value << 3));
                var quotient = uartClockFrequency / divisor;
                var remainder = uartClockFrequency % divisor;
                return (factor * quotient) + (factor * remainder) / divisor;
            }
        }

        public override void WriteChar(byte value)
        {
            if(!receiverEnableFlag.Value)
            {
                this.Log(LogLevel.Info, "Data received when the receiver is disabled: 0x{0:X}", value);
                return;
            }
            base.WriteChar(value);
        }

        protected override void CharWritten()
        {
            interruptsManager.SetInterrupt(Interrupt.ReceiveDataValid);
            receiveDataValidFlag.Value = true;
        }

        protected override void QueueEmptied()
        {
            interruptsManager.ClearInterrupt(Interrupt.ReceiveDataValid);
            receiveDataValidFlag.Value = false;
        }

        private void HandleTxBufferData(byte data)
        {
            if(!transmitterEnableFlag.Value)
            {
                this.Log(LogLevel.Warning, "Trying to send data, but the transmitter is disabled: 0x{0:X}", data);
                return;
            }

            if(operationModeField.Value == OperationMode.Synchronous)
            {
                if(spiSlaveDevice == null)
                {
                    this.Log(LogLevel.Warning, "Writing data in synchronous mode, but no device is currently connected.");
                    return;
                }
                transferCompleteFlag.Value = false;
                var result = spiSlaveDevice.Transmit(data);
                transferCompleteFlag.Value = true;
                WriteChar(result);
            }
            else
            {
                interruptsManager.SetInterrupt(Interrupt.TransmitBufferLevel);
                interruptsManager.SetInterrupt(Interrupt.TransmitComplete);
                TransmitCharacter(data);
            }
        }

        private byte ReadBuffer()
        {
            byte character;
            return TryGetCharacter(out character) ? character : (byte)0;
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly InterruptManager<Interrupt> interruptsManager;
        private readonly IEnumRegisterField<OversamplingMode> oversamplingField;
        private readonly IEnumRegisterField<OperationMode> operationModeField;
        private readonly IEnumRegisterField<Parity> parityBitModeField;
        private readonly IEnumRegisterField<Bits> stopBitsModeField;
        private readonly IValueRegisterField fractionalClockDividerField;
        private readonly IFlagRegisterField transferCompleteFlag;
        private readonly IFlagRegisterField receiveDataValidFlag;
        private readonly IFlagRegisterField receiverEnableFlag;
        private readonly IFlagRegisterField transmitterEnableFlag;
        private readonly uint uartClockFrequency;
        private ISPIPeripheral spiSlaveDevice;

        private enum OperationMode
        {
            Asynchronous,
            Synchronous
        }

        private enum OversamplingMode
        {
            Times16,
            Times8,
            Times6,
            Times4
        }

        private enum Interrupt
        {
            [Subvector(0)]
            TransmitComplete,
            [Subvector(0), NotSettable]
            TransmitBufferLevel,
            [Subvector(1), NotSettable]
            ReceiveDataValid,
            [Subvector(1)]
            ReceiveBufferFull,
            [Subvector(1)]
            ReceiveOverflow,
            [Subvector(1)]
            ReceiveUnderflow,
            [Subvector(0)]
            TransmitOverflow,
            [Subvector(0)]
            TransmitUnderflow,
            [Subvector(1)]
            ParityError,
            [Subvector(1)]
            FramingError,
            [Subvector(1)]
            MultiProcessorAddressFrame,
            [Subvector(1)]
            SlaveSelectInMasterMode,
            [Subvector(0)]
            CollisionCheckFail,
            [Subvector(0)]
            TransmitIdle,
            [Subvector(1)]
            TimerComparator0,
            [Subvector(1)]
            TimerComparator1,
            [Subvector(1)]
            TimerComparator2
        }

        private enum Registers
        {
            Control = 0x0,
            FrameFormat = 0x4,
            TriggerControl = 0x8,
            Command = 0xC,
            Status = 0x10,
            ClockControl = 0x14,
            RxBufferDataExtended = 0x18,
            RxBufferData = 0x1C,
            RxBufferDoubleDataExtended = 0x20,
            RxBufferDoubleData = 0x24,
            RxBufferDataExtendedPeek = 0x28,
            RxBufferDoubleDataExtendedPeek = 0x2C,
            TxBufferDataExtended = 0x30,
            TxBufferData = 0x34,
            TxBufferDoubleDataExtended = 0x38,
            TxBufferDoubleData = 0x3C,
            InterruptFlag = 0x40,
            InterruptFlagSet = 0x44,
            InterruptFlagClear = 0x48,
            InterruptEnable = 0x4C,
            IrDAControl = 0x50,
            USARTInput = 0x58,
            I2SControl = 0x5C,
            Timing = 0x60,
            ControlExtended = 0x64,
            TimeCompare0 = 0x68,
            TimeCompare1 = 0x6C,
            TimeCompare2 = 0x70,
            IORoutingPinEnable = 0x74,
            IORoutingLocation0 = 0x78,
            IORoutingLocation1 = 0x7C,
        }
    }
}
