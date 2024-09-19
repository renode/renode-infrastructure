//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.SCI
{
    // Due to unusual register offsets we cannot use address translations
    public class RenesasRA6M5_SCI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IUART, IWordPeripheral, IBytePeripheral, IProvidesRegisterCollection<WordRegisterCollection>, IKnownSize,
        IPeripheralContainer<IUART, NullRegistrationPoint>, IPeripheralContainer<II2CPeripheral, NumberRegistrationPoint<int>>
    {
        public RenesasRA6M5_SCI(IMachine machine, ulong frequency, bool enableManchesterMode, bool enableFIFO, bool fullModel = true) : base(machine)
        {
            this.machine = machine;
            this.frequency = frequency;
            i2cContainer = new SimpleContainerHelper<II2CPeripheral>(machine, this);
            ReceiveIRQ = new GPIO();
            TransmitIRQ = new GPIO();
            TransmitEndIRQ = new GPIO();
            receiveQueue = new Queue<ushort>();
            iicTransmitQueue = new Queue<byte>();
            RegistersCollection = new WordRegisterCollection(this);

            DefineRegisters(enableManchesterMode, enableFIFO, fullModel);
            Size = fullModel ? 0x100 : 0x20;
            Reset();
        }

        public ushort ReadWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            RegistersCollection.Write(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return (byte)RegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public void WriteChar(byte value)
        {
            receiveQueue.Enqueue(value);
            UpdateInterrupts();
        }

        public override void Reset()
        {
            fifoMode = false;
            manchesterMode = false;
            receiveQueue.Clear();
            iicTransmitQueue.Clear();

            ReceiveIRQ.Unset();
            TransmitIRQ.Unset();
            TransmitEndIRQ.Unset();
            RegistersCollection.Reset();
            peripheralMode = PeripheralMode.UART;
        }

        public void Register(IUART peripheral, NullRegistrationPoint registrationPoint)
        {
            if(registeredUartPeripheral != null)
            {
                throw new RegistrationException($"UART peripheral alredy registered");
            }

            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);

            CharReceived += peripheral.WriteChar;
            peripheral.CharReceived += WriteChar;

            registeredUartPeripheral = peripheral;
        }

        public void Unregister(IUART peripheral)
        {
            CharReceived -= peripheral.WriteChar;
            peripheral.CharReceived -= WriteChar;

            registeredUartPeripheral = null;
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(IUART peripheral)
        {
            return registeredUartPeripheral != null ?
                new[] { NullRegistrationPoint.Instance } :
                Enumerable.Empty<NullRegistrationPoint>();
        }

        public virtual void Register(II2CPeripheral peripheral, NumberRegistrationPoint<int> registrationPoint) => i2cContainer.Register(peripheral, registrationPoint);

        public virtual void Unregister(II2CPeripheral peripheral) => i2cContainer.Unregister(peripheral);

        public IEnumerable<NumberRegistrationPoint<int>> GetRegistrationPoints(II2CPeripheral peripheral) => i2cContainer.GetRegistrationPoints(peripheral);

        public bool IsDataReadyFIFO { get => (ulong)receiveQueue.Count < receiveFIFOTriggerCount.Value; }

        public bool IsReceiveFIFOFull { get => (ulong)receiveQueue.Count >= receiveFIFOTriggerCount.Value; }

        // This is used in the non-FIFO mode where there is room just for a single byte, hence "full" means non-empty queue
        public bool IsReceiveDataFull { get => receiveQueue.Count > 0; }

        public WordRegisterCollection RegistersCollection { get; }

        public GPIO ReceiveIRQ { get; }

        public GPIO TransmitIRQ { get; }

        public GPIO TransmitEndIRQ { get; }

        public uint BaudRate
        {
            get
            {
                var n = clockSource.Value == 0 ? 1UL : 2UL << (2 * (ushort)clockSource.Value - 1);
                return (uint)(frequency / (64UL * n * bitRate.Value)) - 1;
            }
        }

        public Bits StopBits => hasTwoStopBits.Value ? Bits.Two : Bits.One;

        public Parity ParityBit => parityEnabled.Value ? parityBit : Parity.None;

        public long Size { get; }

        IEnumerable<IRegistered<IUART, NullRegistrationPoint>> IPeripheralContainer<IUART, NullRegistrationPoint>.Children
        {
            get => registeredUartPeripheral != null ?
                new [] { Registered.Create(registeredUartPeripheral, NullRegistrationPoint.Instance) } :
                Enumerable.Empty<IRegistered<IUART, NullRegistrationPoint>>();
        }

        IEnumerable<IRegistered<II2CPeripheral, NumberRegistrationPoint<int>>> IPeripheralContainer<II2CPeripheral, NumberRegistrationPoint<int>>.Children =>
            i2cContainer.Children;

        [field: Transient]
        public event Action<byte> CharReceived;

        private void UpdateInterrupts()
        {
            if(peripheralMode == PeripheralMode.IIC)
            {
                UpdateInterruptsInIICMode();
                return;
            }
            var rxState = receiveInterruptEnabled.Value && (fifoMode ? IsReceiveFIFOFull : IsReceiveDataFull);
            var txState = transmitInterruptEnabled.Value && (fifoMode ? transmitFIFOEmpty.Value : true);
            var teState = transmitEndInterruptEnabled.Value;

            this.DebugLog("ReceiveIRQ: {0}, TransmitIRQ: {1}, TransmitEndIRQ: {2}.", rxState ? "set" : "unset", txState ? "set" : "unset", teState ? "set" : "unset");
            if(rxState)
            {
                ReceiveIRQ.Blink();
            }
            if(txState)
            {
                TransmitIRQ.Blink();
            }
            if(teState)
            {
                TransmitEndIRQ.Blink();
            }
        }

        private void UpdateInterruptsInIICMode()
        {
            // This does not update the Transmit/Receive interrupts, as there are not status flags in this mode and they are expected just to blink
            bool teState = transmitEndInterruptEnabled.Value && conditionCompletedFlag.Value;
            this.DebugLog("TransmitEndIRQ: {0}.", teState ? "set" : "unset");

            TransmitEndIRQ.Set(teState);
        }

        private void BlinkTxIRQ()
        {
            Debug.Assert(peripheralMode == PeripheralMode.IIC);

            if(transmitInterruptEnabled.Value)
            {
                TransmitIRQ.Blink();
            }
        }

        private void FlushIICTransmitQueue()
        {
            if(iicTransmitQueue.Count != 0)
            {
                selectedIICSlave.Write(iicTransmitQueue.ToArray());
                iicTransmitQueue.Clear();
            }
        }

        private void EmulateIICStartStopCondition(IICCondition condition)
        {
            conditionCompletedFlag.Value = true;
            transmitEnd.Value = true;
            if(condition == IICCondition.Stop)
            {
                if(selectedIICSlave == null)
                {
                    this.WarningLog("No slave selected. This condition will have no effect");
                    return;
                }

                switch(iicDirection)
                {
                    case IICTransactionDirection.Write:
                        FlushIICTransmitQueue();
                        break;
                    case IICTransactionDirection.Read:
                        receiveQueue.Clear();
                        break;
                    default:
                        throw new ArgumentException("Unknown IIC direction");
                }
                selectedIICSlave.FinishTransmission();
            }
            else if(condition == IICCondition.Restart)
            {
                //Flush the register address
                FlushIICTransmitQueue();
            }

            iicState = IICState.Idle;
            iicDirection = IICTransactionDirection.Unset;
            selectedIICSlave = null;
            UpdateInterruptsInIICMode();
        }

        private void SetPeripheralMode()
        {
            if(smartCardMode.Value)
            {
                peripheralMode = PeripheralMode.SmartCardInterface;
                if(i2cMode.Value)
                {
                    this.Log(LogLevel.Warning,
                        "The IICM flag in the {0} register (SIMR1) should be unset for Smart Card Interface mode; it's set",
                        nameof(Registers.IICMode1)
                    );
                }
                return;
            }

            if(i2cMode.Value)
            {
                peripheralMode = PeripheralMode.IIC;
                if(nonSmartCommunicationMode.Value != CommunicationMode.AsynchOrSimpleIIC)
                {
                    this.Log(LogLevel.Warning,
                        "The CM flag in the {0} register (SMR) should be unset ({1}) for Simple I2C mode; it's set ({2})",
                        nameof(Registers.SerialModeNonSmartCard), CommunicationMode.AsynchOrSimpleIIC, CommunicationMode.SynchOrSimpleSPI
                    );
                }

                if(dataTransferDirection.Value != DataTransferDirection.MSBFirst)
                {
                    this.Log(LogLevel.Warning,
                        "The SDIR flag in the {0} should be set ({1}) for Simple I2Cmode; it's unset ({2})",
                        nameof(Registers.SmartCardMode), DataTransferDirection.MSBFirst, DataTransferDirection.LSBFirst
                    );
                }
                return;
            }

            switch(nonSmartCommunicationMode.Value)
            {
                case CommunicationMode.AsynchOrSimpleIIC:
                    if(i2cContainer.ChildCollection.Count != 0)
                    {
                        peripheralMode = PeripheralMode.IIC;
                        return;
                    }
                    break;
                case CommunicationMode.SynchOrSimpleSPI:
                    if(RegisteredPeripheral != null)
                    {
                       peripheralMode = PeripheralMode.SPI;
                       return;
                    }
                    break;
                default:
                    break;
            }
            peripheralMode = PeripheralMode.UART;
        }

        private void DefineRegisters(bool enableManchesterMode, bool enableFIFO, bool fullModel)
        {
            // Non-Smart Card Mode
            Registers.SerialModeNonSmartCard.DefineConditional(this, () => !smartCardMode.Value)
                .WithValueField(0, 2, out clockSource, name: "CKS")
                .WithTaggedFlag("MP", 2)
                .WithFlag(3, out hasTwoStopBits, name: "STOP")
                .WithFlag(4, name: "PM",
                    valueProviderCallback: _ => parityBit == Parity.Odd,
                    writeCallback: (_, value) => parityBit = value ? Parity.Odd : Parity.Even)
                .WithFlag(5, out parityEnabled, name: "PE")
                .WithTaggedFlag("CHR", 6)
                .WithEnumField<WordRegister, CommunicationMode>(7, 1, out nonSmartCommunicationMode,
                    writeCallback: (_, __) => SetPeripheralMode(), name: "CM")
                .WithReservedBits(8, 8);

            // Smart Card Mode
            Registers.SerialModeSmartCard.DefineConditional(this, () => smartCardMode.Value)
                .WithTag("CKS", 0, 2)
                .WithTag("BCP", 2, 2)
                .WithTag("PM", 4, 1)
                .WithTag("PE", 5, 1)
                .WithTag("BLK", 6, 1)
                .WithTag("GM", 7, 1)
                .WithReservedBits(8, 8);

            Registers.BitRate.Define(this, 0xff)
                .WithValueField(0, 8, out bitRate,
                    writeCallback: (oldVal, newVal) => bitRate.Value = (!transmitEnabled.Value && !receiveEnabled.Value) ? newVal : oldVal,
                    name: "BRR")
                .WithReservedBits(8, 8);

            // Non-Smart Card Mode
            Registers.SerialControlNonSmartCard.DefineConditional(this, () => !smartCardMode.Value)
                .WithTag("CKE", 0, 2)
                .WithFlag(2, out transmitEndInterruptEnabled, name: "TEIE")
                .WithTaggedFlag("MPIE", 3)
                .WithFlag(4, out receiveEnabled, name: "RE")
                .WithFlag(5, out transmitEnabled, name: "TE")
                .WithFlag(6, out receiveInterruptEnabled, name: "RIE")
                .WithFlag(7, out transmitInterruptEnabled, name: "TIE")
                .WithReservedBits(8, 8)
                .WithChangeCallback((_, __) =>
                {
                    // The documentation states that the TXI interrupt should be fired when both TE and TIE are set
                    // with a single write operation. On the hardware however it takes some time to actually do that.
                    // The delay mechanism introduced below will prevent Renode from activating the interrupt too soon,
                    // because in some cases interrupts could be handled in the wrong order.
                    if(transmitEnabled.Value && transmitInterruptEnabled.Value)
                    {
                        machine.ScheduleAction(TimeInterval.FromMilliseconds(InterruptDelay), ___ => UpdateInterrupts());
                        return;
                    }
                    UpdateInterrupts();
                });

            // Smart Card Mode
            Registers.SerialControlSmartCard.DefineConditional(this, () => smartCardMode.Value)
                .WithTag("CKS", 0, 2)
                .WithTag("TEIE", 2, 1)
                .WithTag("MPIE", 3, 1)
                .WithTag("RE", 4, 1)
                .WithTag("TE", 5, 1)
                .WithTag("RIE", 6, 1)
                .WithTag("TIE", 7, 1)
                .WithReservedBits(8, 8);

            Registers.TransmitData.Define(this)
                .WithValueField(0, 8, FieldMode.Write, name: "TDR",
                    writeCallback: (_, value) =>
                    {
                        if(!transmitEnabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Transmission is not enabled, ignoring byte 0x{0:X}", value);
                            return;
                        }
                        switch(peripheralMode) {
                            case PeripheralMode.UART:
                                TransmitUARTData((byte)value);
                                break;
                            case PeripheralMode.IIC:
                                TransmitIICData((byte)value);
                                // No need to update the interrupts - in IIC mode we just blink the Tx interrupt
                                return;
                            case PeripheralMode.SPI:
                                TransmitSPIData((byte)value);
                                break;
                            default:
                                throw new Exception($"Unknown peripheral mode {peripheralMode}");
                        }
                        UpdateInterrupts();
                    })
                .WithReservedBits(8, 8);

            // Non-Smart Card, Non-FIFO, Non-Manchester
            Registers.SerialStatusNonSmartCardNonFIFO.DefineConditional(this, () => !smartCardMode.Value && !fifoMode && !manchesterMode, 0x84)
                .WithTaggedFlag("MPBT", 0)
                .WithTaggedFlag("MPB", 1)
                .WithFlag(2, out transmitEnd, FieldMode.Read, name: "TEND")
                .WithTaggedFlag("PER", 3)
                .WithTaggedFlag("FER", 4)
                .WithTaggedFlag("ORER", 5)
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => IsReceiveDataFull, name: "RDRF")
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => true, name: "TDRE")
                .WithReservedBits(8, 8)
                .WithChangeCallback((_, __) => UpdateInterrupts());

            if(enableFIFO)
            {
                // Non-Smart Card, FIFO, Non-manchester
                Registers.SerialStatusNonSmartCardFIFO.DefineConditional(this, () => !smartCardMode.Value && fifoMode && !manchesterMode, 0x84)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => IsDataReadyFIFO, name: "DR")
                    .WithReservedBits(1, 1)
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => true, name: "TEND")
                    .WithTaggedFlag("PER", 3)
                    .WithTaggedFlag("FER", 4)
                    .WithTaggedFlag("ORER", 5)
                    .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => IsReceiveFIFOFull, name: "RDF")
                    .WithFlag(7, out transmitFIFOEmpty, FieldMode.Read | FieldMode.WriteZeroToClear,  name: "TDFE")
                    .WithReservedBits(8, 8);
            }

            // Smart Card, Non-manchester
            Registers.SerialStatusSmartCard.DefineConditional(this, () => smartCardMode.Value && !manchesterMode, 0x84)
                .WithTaggedFlag("MPBT", 0)
                .WithTaggedFlag("MPB", 1)
                .WithTaggedFlag("TEND", 2)
                .WithTaggedFlag("PER", 3)
                .WithTaggedFlag("ERS", 4)
                .WithTaggedFlag("ORER", 5)
                .WithTaggedFlag("RDRF", 6)
                .WithTaggedFlag("TDRE", 7)
                .WithReservedBits(8, 8);

            // Non-Smart Card, Manchester
            Registers.SerialStatusManchesterMode.DefineConditional(this, () => !smartCardMode.Value && manchesterMode, 0x84)
                .WithTaggedFlag("MER", 0)
                .WithTaggedFlag("MPB", 1)
                .WithTaggedFlag("TEND", 2)
                .WithTaggedFlag("PER", 3)
                .WithTaggedFlag("FER", 4)
                .WithTaggedFlag("ORER", 5)
                .WithTaggedFlag("RDRF", 6)
                .WithTaggedFlag("TDRE", 7)
                .WithReservedBits(8, 8);

            Registers.ReceiveData.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "RDR",
                    valueProviderCallback: _ =>
                    {
                        if(peripheralMode == PeripheralMode.IIC)
                        {
                            if(iicState != IICState.InTransaction)
                            {
                                this.WarningLog("Trying to read the received data in the wrong state");
                            }
                            BlinkTxIRQ();
                            return TryReadFromIICSlave();
                        }

                        if(!receiveQueue.TryDequeue(out var result))
                        {
                            this.Log(LogLevel.Warning, "Queue is empty, returning 0.");
                            return 0;
                        }
                        UpdateInterrupts();
                        return result;
                    })
                .WithReservedBits(8, 8);

            Registers.SmartCardMode.Define(this, 0xf2)
                .WithFlag(0, out smartCardMode, name: "SMIF")
                .WithReservedBits(1, 1)
                .WithTaggedFlag("SINV", 2)
                .WithEnumField(3, 1, out dataTransferDirection, name: "SDIR")
                .WithTaggedFlag("CHR1", 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("BCP2", 7)
                .WithReservedBits(8, 8)
                .WithChangeCallback((_, __) => SetPeripheralMode());

            Registers.SerialExtendedMode.Define(this)
                .WithTaggedFlag("ACS0", 0)
                .WithTaggedFlag("PADIS", 1)
                .WithTaggedFlag("BRME", 2)
                .WithTaggedFlag("ABCSE", 3)
                .WithTaggedFlag("ABCS", 4)
                .WithTaggedFlag("NFEN", 5)
                .WithTaggedFlag("BGDM", 6)
                .WithTaggedFlag("RXDESEL", 7)
                .WithReservedBits(8, 8);

            Registers.NoiseFilterSetting.Define(this)
                .WithTag("NFCS", 0, 3)
                .WithReservedBits(3, 13);

            Registers.IICMode1.Define(this)
                .WithFlag(0, out i2cMode, changeCallback: (_, __) => SetPeripheralMode(), name: "IICM")
                .WithReservedBits(1, 2)
                .WithTag("IICDL", 3, 5)
                .WithReservedBits(8, 8);

            Registers.IICMode2.Define(this)
                .WithTag("IICINTM", 0, 1)
                .WithTaggedFlag("IICCSC", 1)
                .WithReservedBits(2, 3)
                .WithFlag(5, writeCallback: (_, val) => { if(val) BlinkTxIRQ(); }, name:"IICACKT")
                .WithReservedBits(6, 10);

            Registers.IICMode3.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            this.DebugLog("Start Condition Requested!");
                            EmulateIICStartStopCondition(IICCondition.Start);
                        }
                    }, name: "IICSTAREQ")
                .WithFlag(1, FieldMode.WriteOneToClear, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            this.DebugLog("Restart Condition Requested!");
                            EmulateIICStartStopCondition(IICCondition.Restart);
                        }
                    }, name: "IICRSTAREQ")
                .WithFlag(2, FieldMode.WriteOneToClear, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            this.DebugLog("Stop Condition Requested!");
                            EmulateIICStartStopCondition(IICCondition.Stop);
                        }
                    }, name: "IICSTPREQ")
                .WithFlag(3, out conditionCompletedFlag, FieldMode.WriteZeroToClear,name: "IICSTIF")
                .WithValueField(4, 2, name: "IICSDAS")
                .WithValueField(6, 2, name: "IICSCLS")
                .WithReservedBits(8, 8)
                .WithWriteCallback((_, val) =>
                    {
                        var conditionRequestBits = val & 0b111;
                        if(conditionRequestBits != 0 && ((conditionRequestBits & (conditionRequestBits - 1)) != 0))
                        {
                            this.WarningLog("More than one IIC condition requested at the same register acces");
                        }
                        else if(conditionRequestBits == 0)
                        {
                            // conditionCompletedFlag being zeroed
                            UpdateInterruptsInIICMode();
                        }
                    });

            Registers.IICStatus.Define(this)
                .WithTaggedFlag("IICACKR", 0)
                .WithReservedBits(1, 7)
                .WithReservedBits(8, 8);

            Registers.SPIMode.Define(this)
                .WithTaggedFlag("SSE", 0)
                .WithTaggedFlag("CTSE", 1)
                .WithTaggedFlag("MSS", 2)
                .WithTaggedFlag("CTSPEN", 3)
                .WithTaggedFlag("MFF", 4)
                .WithReservedBits(5, 1)
                .WithTaggedFlag("CKPOL", 6)
                .WithTaggedFlag("CKPH", 7)
                .WithReservedBits(8, 8);

            Registers.TransmitDataNonManchesterMode.DefineConditional(this, () => !manchesterMode && !fifoMode, 0xff)
                .WithTag("TDAT", 0, 9)
                .WithReservedBits(9, 7);

            Registers.TransmitDataManchesterMode.DefineConditional(this, () => manchesterMode && !fifoMode, 0xff)
                .WithTag("TDAT", 0, 9)
                .WithTaggedFlag("MPBT", 9)
                .WithReservedBits(10, 2)
                .WithTaggedFlag("TSYNC", 12)
                .WithReservedBits(13, 3);

            if(enableFIFO)
            {
                // TransmitFIFODataLowByte (below) is an 8-bits wide window of this register
                Registers.TransmitFIFOData.DefineConditional(this, () => fifoMode, 0xff)
                    .WithValueField(0, 9, FieldMode.Write, name: "TDAT",
                        writeCallback: (_, val) =>
                        {
                            TransmitUARTData((byte)(val >> 8));
                            TransmitUARTData((byte)val);
                            transmitFIFOEmpty.Value = true;
                            UpdateInterrupts();
                        })
                    .WithTaggedFlag("MPBT", 9)
                    .WithReservedBits(10, 6);

                // this is the upper 8-bits of transmitFIFOData register
                Registers.TransmitFIFODataLowByte.DefineConditional(this, () => fifoMode, 0xff)
                    .WithValueField(0, 8, FieldMode.Write, name: "TDATL",
                        writeCallback: (_, val) =>
                        {
                            TransmitUARTData((byte)val);
                            transmitFIFOEmpty.Value = true;
                            UpdateInterrupts();
                        })
                    .WithReservedBits(9, 7);
            }

            Registers.ReceiveDataNonManchesterMode.DefineConditional(this, () => !manchesterMode && !fifoMode)
                .WithTag("RDAT", 0, 9)
                .WithReservedBits(9, 7);

            Registers.ReceiveDataManchesterMode.DefineConditional(this, () => manchesterMode && !fifoMode)
                .WithReservedBits(10, 2)
                .WithTaggedFlag("RSYNC", 12)
                .WithReservedBits(13, 3);

            if(enableFIFO)
            {
                Registers.ReceiveFIFOData.DefineConditional(this, () => fifoMode)
                    .WithValueField(0, 9, FieldMode.Read, name: "RDAT",
                        valueProviderCallback: _ =>
                        {
                            if(!receiveQueue.TryDequeue(out var result))
                            {
                                this.Log(LogLevel.Warning, "Queue is empty, returning 0.");
                                return 0;
                            }
                            UpdateInterrupts();
                            return result;
                        })
                    .WithTaggedFlag("MPB", 9)
                    .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => IsDataReadyFIFO, name: "DR")
                    .WithTaggedFlag("PER", 11)
                    .WithTaggedFlag("FER", 12)
                    .WithTaggedFlag("ORER", 13)
                    .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => IsReceiveFIFOFull, name: "RDF")
                    .WithReservedBits(15, 1);
            }

            Registers.ModulationDuty.Define(this, 0xff)
                .WithTag("MDDR", 0, 8)
                .WithReservedBits(8, 8);

            Registers.DataCompareMatchControl.Define(this, 0x40)
                .WithTaggedFlag("DCMF", 0)
                .WithReservedBits(1, 2)
                .WithTaggedFlag("DPER", 3)
                .WithTaggedFlag("DFER", 4)
                .WithReservedBits(5, 1)
                .WithTaggedFlag("IDSEL", 6)
                .WithTaggedFlag("DCME", 7)
                .WithReservedBits(8, 8);

            if(enableFIFO)
            {
                Registers.FIFOControl.Define(this, 0xF800)
                    .WithFlag(0, name: "FM",
                        writeCallback: (_, val) => fifoMode = val,
                        valueProviderCallback: _ => fifoMode)
                    .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "RFRST",
                        writeCallback: (_, __) =>
                        {
                            if(fifoMode)
                            {
                                receiveQueue.Clear();
                            }
                        })
                    .WithTaggedFlag("TFRST", 2)
                    .WithTaggedFlag("DRES", 3)
                    .WithTaggedFlag("TTRG", 4)
                    .WithValueField(8, 4, out receiveFIFOTriggerCount, name: "RTRG")
                    .WithTag("RSTRG", 12, 4);

                Registers.FIFODataCount.Define(this)
                    .WithValueField(0, 5, FieldMode.Read, name: "R",
                        valueProviderCallback: _ => (ulong)receiveQueue.Count >= MaxFIFOSize ? MaxFIFOSize : (ulong)receiveQueue.Count)
                    .WithReservedBits(5, 3)
                    .WithTag("T", 8, 5)
                    .WithReservedBits(13, 3);
            }

            Registers.LineStatus.Define(this)
                .WithTaggedFlag("ORER", 0)
                .WithReservedBits(1, 1)
                .WithTag("FNUM", 2, 5)
                .WithReservedBits(7, 1)
                .WithTag("PNUM", 8, 5)
                .WithReservedBits(13, 3);

            Registers.CompareMatchData.Define(this)
                .WithTag("CMPD", 0, 9)
                .WithReservedBits(9, 7);

            Registers.SerialPort.Define(this, 0x03)
                .WithTaggedFlag("RXDMON", 0)
                .WithTaggedFlag("SPB2DT", 1)
                .WithTaggedFlag("SPB2IO", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("RINV", 4)
                .WithTaggedFlag("TINV", 5)
                .WithTaggedFlag("ASEN", 6)
                .WithTaggedFlag("ATEN", 7)
                .WithReservedBits(8, 8);

            Registers.AdjustmentCommunicationTiming.Define(this)
                .WithTag("AST", 0, 3)
                .WithTaggedFlag("AJD", 3)
                .WithTag("ATT", 4, 3)
                .WithTaggedFlag("AET", 7)
                .WithReservedBits(8, 8);

            if(!fullModel)
            {
                return;
            }

            if(enableManchesterMode)
            {
                Registers.ManchesterMode.Define(this)
                    .WithTag("RMPOL", 0, 1)
                    .WithTag("TMPOL", 1, 1)
                    .WithTag("ERTEN", 2, 1)
                    .WithReservedBits(3, 1)
                    .WithTag("SYNVAL", 4, 1)
                    .WithTag("SYNSEL", 5, 1)
                    .WithTag("SBSEL", 6, 1)
                    .WithFlag(7, name: "MANEN",
                        writeCallback: (_, val) => manchesterMode = val,
                        valueProviderCallback: _ => manchesterMode)
                    .WithReservedBits(8, 8);

                Registers.TransmitManchesterPrefaceSetting.Define(this)
                    .WithTag("TPLEN", 0, 3)
                    .WithTag("TPPAT", 4, 2)
                    .WithReservedBits(6, 10);

                Registers.ReceiveManchesterPrefaceSetting.Define(this)
                    .WithTag("RPLEN", 0, 3)
                    .WithTag("RPPAT", 4, 2)
                    .WithReservedBits(6, 10);

                Registers.ManchesterExtendedErrorStatus.Define(this)
                    .WithTag("PFER", 0, 1)
                    .WithTag("SYER", 1, 1)
                    .WithTag("SBER", 2, 1)
                    .WithReservedBits(3, 13);

                Registers.ManchesterExtendedErrorControl.Define(this)
                    .WithTag("PFEREN", 0, 1)
                    .WithTag("SYEREN", 1, 1)
                    .WithTag("SBEREN", 2, 1)
                    .WithReservedBits(3, 13);
            }
            else
            {
                Registers.ExtendedSerialModuleEnable.Define(this)
                    .WithReservedBits(1, 7)
                    .WithTag("ESME", 0, 1)
                    .WithReservedBits(8, 8);

                Registers.Control1.Define(this)
                    .WithTag("PIBS", 5, 3)
                    .WithTag("PIBE", 4, 1)
                    .WithTag("CF1DS", 2, 2)
                    .WithTag("CF0RE", 1, 1)
                    .WithTag("BFE", 0, 1)
                    .WithReservedBits(8, 8);

                Registers.Control2.Define(this)
                    .WithTag("RTS", 6, 2)
                    .WithTag("BSSS", 4, 2)
                    .WithReservedBits(3, 1)
                    .WithTag("DFCS", 0, 3)
                    .WithReservedBits(8, 8);

                Registers.Control3.Define(this)
                    .WithReservedBits(1, 7)
                    .WithTag("SDST", 0, 1)
                    .WithReservedBits(8, 8);

                Registers.PortControl.Define(this)
                    .WithReservedBits(5, 3)
                    .WithTag("SHARPS", 4, 1)
                    .WithReservedBits(2, 2)
                    .WithTag("RXDXPS", 1, 1)
                    .WithTag("TXDXPS", 0, 1)
                    .WithReservedBits(8, 8);
            }

            Registers.Control0.Define(this)
                .WithReservedBits(4, 4)
                .WithTag("BRME", 3, 1)
                .WithTag("RXDSF", 2, 1)
                .WithTag("SFSF", 1, 1)
                .WithReservedBits(0, 1)
                .WithReservedBits(8, 8);

            Registers.InterruptControl.Define(this)
                .WithReservedBits(6, 2)
                .WithTag("AEDIE", 5, 1)
                .WithTag("BCDIE", 4, 1)
                .WithTag("PBDIE", 3, 1)
                .WithTag("CF1MIE", 2, 1)
                .WithTag("CF0MIE", 1, 1)
                .WithTag("BFDIE", 0, 1)
                .WithReservedBits(8, 8);

            Registers.Status.Define(this)
                .WithReservedBits(6, 2)
                .WithTag("AEDF", 5, 1)
                .WithTag("BCDF", 4, 1)
                .WithTag("PIBDF", 3, 1)
                .WithTag("CF1MF", 2, 1)
                .WithTag("CF0MF", 1, 1)
                .WithTag("BFDF", 0, 1)
                .WithReservedBits(8, 8);

            Registers.StatusClear.Define(this)
                .WithReservedBits(6, 2)
                .WithTag("AEDCL", 5, 1)
                .WithTag("BCDCL", 4, 1)
                .WithTag("PIBDCL", 3, 1)
                .WithTag("CF1MCL", 2, 1)
                .WithTag("CF0MCL", 1, 1)
                .WithTag("BFDCL", 0, 1)
                .WithReservedBits(8, 8);

            Registers.ControlField0Data.Define(this)
                .WithReservedBits(0, 8)
                .WithReservedBits(8, 8);

            Registers.ControlField0CompareEnable.Define(this)
                .WithTag("CF0CE7", 7, 1)
                .WithTag("CF0CE6", 6, 1)
                .WithTag("CF0CE5", 5, 1)
                .WithTag("CF0CE4", 4, 1)
                .WithTag("CF0CE3", 3, 1)
                .WithTag("CF0CE2", 2, 1)
                .WithTag("CF0CE1", 1, 1)
                .WithTag("CF0CE0", 0, 1)
                .WithReservedBits(8, 8);

            Registers.ControlField0RecieveData.Define(this)
                .WithReservedBits(0, 8)
                .WithReservedBits(8, 8);

            Registers.PrimaryControlField1Data.Define(this)
                .WithReservedBits(0, 8)
                .WithReservedBits(8, 8);

            Registers.SecondaryControlField1Data.Define(this)
                .WithReservedBits(0, 8)
                .WithReservedBits(8, 8);

            Registers.ControlField1CompareEnable.Define(this)
                .WithTag("CF1CE7", 7, 1)
                .WithTag("CF1CE6", 6, 1)
                .WithTag("CF1CE5", 5, 1)
                .WithTag("CF1CE4", 4, 1)
                .WithTag("CF1CE3", 3, 1)
                .WithTag("CF1CE2", 2, 1)
                .WithTag("CF1CE1", 1, 1)
                .WithTag("CF1CE0", 0, 1)
                .WithReservedBits(8, 8);

            Registers.ControlField1ReceiveData.Define(this)
                .WithReservedBits(0, 8)
                .WithReservedBits(8, 8);

            Registers.TimerControl.Define(this)
                .WithReservedBits(1, 7)
                .WithTag("TCST", 0, 1)
                .WithReservedBits(8, 8);

            Registers.TimerMode.Define(this)
                .WithReservedBits(7, 1)
                .WithTag("TCSS", 4, 3)
                .WithTag("TWRC", 3, 1)
                .WithReservedBits(2, 1)
                .WithTag("TOMS", 0, 2)
                .WithReservedBits(8, 8);

            Registers.TimerPrescaler.Define(this)
                .WithReservedBits(0, 8)
                .WithReservedBits(8, 8);

            Registers.TimerCount.Define(this)
                .WithReservedBits(0, 8)
                .WithReservedBits(8, 8);
        }

        private ulong TryReadFromIICSlave()
        {
            ushort readByte;
            if(selectedIICSlave == null)
            {
                this.WarningLog("No peripheral selected. Will not perform read");
                return 0UL;
            }
            if(!receiveQueue.TryDequeue(out readByte))
            {
                // This will obviously try to read too much bytes, but this is necessary, as we have no way of guessing how many bytes the driver intends to read
                receiveQueue.EnqueueRange(selectedIICSlave.Read(IICReadBufferCount).Select(element => (ushort)element));

                if(!receiveQueue.TryDequeue(out readByte))
                {
                    this.ErrorLog("Unable to get bytes from the peripheral");
                    return 0ul;
                }
            }
            return readByte;
        }

        private void TransmitUARTData(byte value)
        {
            CharReceived?.Invoke(value);
        }

        private void TransmitIICData(byte value)
        {
            Debug.Assert((iicState == IICState.Idle) || (iicDirection != IICTransactionDirection.Unset), $"Incorrect communication direction {iicDirection} in state {iicState}");
            switch(iicState)
            {
                case IICState.Idle:
                    // Addressing frame
                    var rwBit = (value & 0x1);
                    var slaveAddress = value >> 1;
                    iicDirection = (rwBit == 1) ? IICTransactionDirection.Read : IICTransactionDirection.Write;
                    if(!i2cContainer.TryGetByAddress((int)slaveAddress, out selectedIICSlave))
                    {
                        this.WarningLog("Selecting unconnected IIC slave address: 0x{0:X}", slaveAddress);
                    }
                    this.DebugLog("Selected slave address 0x{0:X} for {1}", slaveAddress, iicDirection);
                    iicState = IICState.InTransaction;
                    conditionCompletedFlag.Value = false;
                    BlinkTxIRQ();
                    break;
                case IICState.InTransaction:
                    if(iicDirection == IICTransactionDirection.Write)
                    {
                        iicTransmitQueue.Enqueue(value);
                        BlinkTxIRQ();
                    }
                    else
                    {
                        if(value == DummyTransmitByte)
                        {
                            this.DebugLog("Ignoring the dummy transmission");
                            BlinkTxIRQ();
                        }
                    }
                    break;
                 default:
                    throw new Exception("Unreachable");
            }
        }

        private void TransmitSPIData(byte value)
        {
            if(RegisteredPeripheral == null)
            {
                this.WarningLog("No SPI peripheral connected");
                return;
            }
            receiveQueue.Enqueue(RegisteredPeripheral.Transmit((byte)value));
        }

        // This peripheral might work in a 9-bit mode,
        private readonly Queue<ushort> receiveQueue;
        private readonly Queue<byte> iicTransmitQueue;
        private readonly ulong frequency;
        private readonly IMachine machine;
        private readonly SimpleContainerHelper<II2CPeripheral> i2cContainer;
        private Parity parityBit = Parity.Even;

        private IUART registeredUartPeripheral;

        private bool manchesterMode;
        private bool fifoMode;
        private IICState iicState;
        private II2CPeripheral selectedIICSlave;
        private IICTransactionDirection iicDirection;
        private PeripheralMode peripheralMode;

        private IFlagRegisterField transmitEnabled;
        private IFlagRegisterField hasTwoStopBits;
        private IFlagRegisterField parityEnabled;
        private IFlagRegisterField transmitEndInterruptEnabled;
        private IFlagRegisterField transmitEnd;
        private IFlagRegisterField receiveEnabled;
        private IFlagRegisterField receiveInterruptEnabled;
        private IFlagRegisterField transmitInterruptEnabled;
        private IFlagRegisterField smartCardMode;
        private IFlagRegisterField transmitFIFOEmpty;
        private IFlagRegisterField conditionCompletedFlag;
        private IFlagRegisterField i2cMode;
        private IValueRegisterField receiveFIFOTriggerCount;
        private IValueRegisterField bitRate;
        private IValueRegisterField clockSource;
        private IEnumRegisterField<DataTransferDirection> dataTransferDirection;
        private IEnumRegisterField<CommunicationMode> nonSmartCommunicationMode;


        private const ulong MaxFIFOSize = 16;
        private const int InterruptDelay = 1;
        private const int IICReadBufferCount = 24;
        // This byte is used to trigger reception on IIC bus. It should not be transmitted
        private const byte DummyTransmitByte = 0xFF;

        private enum DataTransferDirection
        {
            LSBFirst = 0,
            MSBFirst = 1,
        }

        private enum IICCondition
        {
            Start,
            Stop,
            Restart,
        }

        private enum CommunicationMode
        {
            AsynchOrSimpleIIC = 0,
            SynchOrSimpleSPI = 1,
        }

        private enum IICState
        {
            Idle = 0,
            InTransaction = 1,
        }

        private enum IICTransactionDirection
        {
            Unset,
            Read,
            Write,
        }

        private enum PeripheralMode
        {
            UART,
            SPI,
            IIC,
            SmartCardInterface,
        }

        private enum Registers
        {
            SerialModeNonSmartCard = 0x00,  // SMR
            SerialModeSmartCard = 0x00,  // SMR_SMCI
            BitRate = 0x01,  // BRR
            SerialControlNonSmartCard = 0x02,  // SCR
            SerialControlSmartCard = 0x02,  // SCR_SMCI
            TransmitData = 0x03,  // TDR
            SerialStatusNonSmartCardNonFIFO = 0x04,  // SSR
            SerialStatusNonSmartCardFIFO = 0x04,  // SSR_FIFO
            SerialStatusSmartCard = 0x04,  // SSR_SMCI
            SerialStatusManchesterMode = 0x04,  // SSR_MANC
            ReceiveData = 0x05,  // RDR
            SmartCardMode = 0x06,  // SCMR
            SerialExtendedMode = 0x07,  // SEMR
            NoiseFilterSetting = 0x08,  // SNFR
            IICMode1 = 0x09,  // SIMR1
            IICMode2 = 0x0A,  // SIMR2
            IICMode3 = 0x0B,  // SIMR3
            IICStatus = 0x0C,  // SISR
            SPIMode = 0x0D,  // SPMR
            TransmitDataNonManchesterMode = 0xE,  // TDRHL
            TransmitDataManchesterMode = 0xE,  // TDRHL_MAN
            TransmitFIFOData = 0xE,  // FTDRHL
            TransmitFIFODataLowByte = 0xF,  // FTDRL
            ReceiveDataNonManchesterMode = 0x10,  // RDRHL
            ReceiveDataManchesterMode = 0x10,  // RDRHL_MAN
            ReceiveFIFOData = 0x10,  // FRDRHL
            ModulationDuty = 0x12,  // MDDR
            DataCompareMatchControl = 0x13,  // DCCR
            FIFOControl = 0x14,  // FCR
            FIFODataCount = 0x16,  // FDR
            LineStatus = 0x18,  // LSR
            CompareMatchData = 0x1A,  // CDR
            SerialPort = 0x1C,  // SPTR
            AdjustmentCommunicationTiming = 0x1D,  // ACTR
            ExtendedSerialModuleEnable = 0x20,  // ESMER
            ManchesterMode = 0x20,  // MMR
            Control0 = 0x21,  // CR0
            Control1 = 0x22,  // CR1
            TransmitManchesterPrefaceSetting = 0x22,  // TMPR
            Control2 = 0x23,  // CR2
            ReceiveManchesterPrefaceSetting = 0x23,  // RMPR
            Control3 = 0x24,  // CR3
            ManchesterExtendedErrorStatus = 0x24,  // MESR
            PortControl = 0x25,  // PCR
            ManchesterExtendedErrorControl = 0x25,  // MECR
            InterruptControl = 0x26,  // ICR
            Status = 0x27,  // STR
            StatusClear = 0x28,  // STCR
            ControlField0Data = 0x29,  // CF0DR
            ControlField0CompareEnable = 0x2A,  // CF0CR
            ControlField0RecieveData = 0x2B,  // CF0RR
            PrimaryControlField1Data = 0x2C,  // PCF1DR
            SecondaryControlField1Data = 0x2D,  // SCF1DR
            ControlField1CompareEnable = 0x2E,  // CF1CR
            ControlField1ReceiveData = 0x2F,  // CF1RR
            TimerControl = 0x30,  // TCR
            TimerMode = 0x31,  // TMR
            TimerPrescaler = 0x32,  // TPRE
            TimerCount = 0x33,  // TCNT
        }
    }
}
