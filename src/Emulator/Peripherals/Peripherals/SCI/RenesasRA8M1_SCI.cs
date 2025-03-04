//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class RenesasRA8M1_SCI : IUART, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize,
        IPeripheralContainer<IUART, NullRegistrationPoint>,
        IPeripheralContainer<ISPIPeripheral, NullRegistrationPoint>,
        IPeripheralContainer<II2CPeripheral, NumberRegistrationPoint<int>>
    {
        public RenesasRA8M1_SCI(IMachine machine, ulong frequency)
        {
            this.machine = machine;
            this.frequency = frequency;

            ReceiveIRQ = new GPIO();
            TransmitIRQ = new GPIO();
            TransmitEndIRQ = new GPIO();

            receiveQueue = new Queue<ushort>();
            iicTransmitQueue = new Queue<byte>();

            uartContainer = new NullRegistrationPointContainerHelper<IUART>(machine, this);
            spiContainer = new NullRegistrationPointContainerHelper<ISPIPeripheral>(machine, this);
            i2cContainer = new SimpleContainerHelper<II2CPeripheral>(machine, this);

            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();

            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public void WriteChar(byte value)
        {
            if(!receiveEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Receiving is not enabled, ignoring byte 0x{0:X}", value);
                return;
            }

            receiveQueue.Enqueue(value);
            UpdateInterrupts();
        }

        public void Reset()
        {
            receiveQueue.Clear();
            iicTransmitQueue.Clear();

            ReceiveIRQ.Unset();
            TransmitIRQ.Unset();
            TransmitEndIRQ.Unset();
            RegistersCollection.Reset();

            currentPeripheralMode = PeripheralMode.UART;
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(ISPIPeripheral peripheral)
        {
            return spiContainer.GetRegistrationPoints(peripheral);
        }

        public void Register(ISPIPeripheral peripheral, NullRegistrationPoint registrationPoint)
        {
            spiContainer.Register(peripheral, registrationPoint);
        }

        public void Unregister(ISPIPeripheral peripheral)
        {
            spiContainer.Unregister(peripheral);
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(IUART peripheral)
        {
            return uartContainer.GetRegistrationPoints(peripheral);
        }

        public void Register(IUART peripheral, NullRegistrationPoint registrationPoint)
        {
            uartContainer.Register(peripheral, registrationPoint);
        }

        public void Unregister(IUART peripheral)
        {
            uartContainer.Unregister(peripheral);
        }

        public virtual void Register(II2CPeripheral peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            i2cContainer.Register(peripheral, registrationPoint);
        }

        public virtual void Unregister(II2CPeripheral peripheral)
        {
            i2cContainer.Unregister(peripheral);
        }

        public IEnumerable<NumberRegistrationPoint<int>> GetRegistrationPoints(II2CPeripheral peripheral)
        {
            return i2cContainer.GetRegistrationPoints(peripheral);
        }

        IEnumerable<IRegistered<IUART, NullRegistrationPoint>> IPeripheralContainer<IUART, NullRegistrationPoint>.Children => uartContainer.Children;
        IEnumerable<IRegistered<ISPIPeripheral, NullRegistrationPoint>> IPeripheralContainer<ISPIPeripheral, NullRegistrationPoint>.Children => spiContainer.Children;
        IEnumerable<IRegistered<II2CPeripheral, NumberRegistrationPoint<int>>> IPeripheralContainer<II2CPeripheral, NumberRegistrationPoint<int>>.Children => i2cContainer.Children;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public GPIO ReceiveIRQ { get; }
        public GPIO TransmitIRQ { get; }
        public GPIO TransmitEndIRQ { get; }

        public uint BaudRate
        {
            get
            {
                var n = clockSelect.Value == 0 ? 1UL : 2UL << (2 * (ushort)clockSelect.Value - 1);
                return (uint)(frequency / (64UL * n * bitRate.Value)) - 1;
            }
        }

        public Bits StopBits => useTwoStopBits.Value ? Bits.Two : Bits.One;

        public Parity ParityBit
        {
            get
            {
                if(!parityEnabled.Value)
                {
                    return Parity.None;
                }

                return useOddParity.Value ? Parity.Odd : Parity.Even;
            }
        }

        public long Size => 0x100;

        [field: Transient]
        public event Action<byte> CharReceived;

        private void UpdateInterrupts()
        {
            if(currentPeripheralMode == PeripheralMode.IIC)
            {
                UpdateInterruptsInIICMode();
                return;
            }
            var rxState = receiveInterruptEnabled.Value && IsReceiveDataFull;
            var txState = transmitInterruptEnabled.Value && IsDataTransmitted;
            var teState = transmitEndInterruptEnabled.Value;
            this.DebugLog("ReceiveIRQ: {0}blinking, TransmitIRQ: {1}blinking, TransmitEndIRQ: {2}blinking", rxState ? "" : "not ", txState ? "" : "not ", teState ? "" : "not ");

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

            if(teState)
            {
                TransmitEndIRQ.Blink();
            }
        }

        private void BlinkTxIRQ()
        {
            DebugHelper.Assert(currentPeripheralMode == PeripheralMode.IIC);

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
                selectedIICSlave = null;
            }
            else if(condition == IICCondition.Restart)
            {
                //Flush the register address
                FlushIICTransmitQueue();
            }

            iicState = IICState.Idle;
            iicDirection = IICTransactionDirection.Unset;
            UpdateInterruptsInIICMode();
        }

        private void SetPeripheralMode()
        {
            switch(nonSmartCommunicationMode.Value)
            {
                case CommunicationMode.SimpleI2C:
                    if(i2cContainer.ChildCollection.Count != 0)
                    {
                        currentPeripheralMode = PeripheralMode.IIC;
                        return;
                    }
                    break;
                case CommunicationMode.SimpleSPI:
                    if(spiContainer.RegisteredPeripheral != null)
                    {
                       currentPeripheralMode = PeripheralMode.SPI;
                       return;
                    }
                    break;
                default:
                    break;
            }
            currentPeripheralMode = PeripheralMode.UART;
        }

        private void DefineRegisters()
        {
            Registers.ReceiveData.Define(this)
                .WithReservedBits(29, 3)
                .WithTag("FER", 28, 1)
                .WithTag("PER", 27, 1)
                .WithReservedBits(25, 2)
                .WithTag("ORER", 24, 1)
                .WithReservedBits(13, 11)
                .WithTag("FFER", 12, 1)
                .WithTag("FPER", 11, 1)
                .WithTag("DR", 10, 1)
                .WithTag("MPB", 9, 1)
                .WithValueField(0, 9, FieldMode.Read, name: "RDAT",
                    valueProviderCallback: _ =>
                    {
                        if(currentPeripheralMode == PeripheralMode.IIC)
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
                    });

            Registers.TransmitData.Define(this)
                .WithReservedBits(13, 19)
                .WithTag("TSYNC", 12, 1)
                .WithReservedBits(10, 2)
                .WithTag("MPBT", 9, 1)
                .WithValueField(0, 9, FieldMode.Write, name: "TDAT",
                    writeCallback: (_, value) =>
                    {
                        if(!transmitEnabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Transmission is not enabled, ignoring byte 0x{0:X}", value);
                            return;
                        }

                        switch(currentPeripheralMode)
                        {
                            case PeripheralMode.UART:
                                TransmitUART((byte)value);
                                break;
                            case PeripheralMode.SPI:
                                TransmitSPI((byte)value);
                                break;
                            case PeripheralMode.IIC:
                                TransmitIICData((byte)value);
                                // No need to update the interrupts - in IIC mode we just blink the Tx interrupt
                                return;
                            default:
                                throw new Exception("unreachable");
                        }

                        UpdateInterrupts();
                    });

            Registers.CommonControl0.Define(this)
                .WithReservedBits(25, 7)
                .WithTag("SSE", 24, 1)
                .WithReservedBits(22, 2)
                .WithFlag(21, out transmitEndInterruptEnabled, name: "TEIE")
                .WithFlag(20, out transmitInterruptEnabled, name: "TIE")
                .WithReservedBits(17, 3)
                .WithFlag(16, out receiveInterruptEnabled, name: "RIE")
                .WithReservedBits(11, 5)
                .WithTag("IDSEL", 10, 1)
                .WithTag("DCME", 9, 1)
                .WithTag("MPIE", 8, 1)
                .WithReservedBits(5, 3)
                .WithFlag(4, out transmitEnabled, name: "TE", writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            conditionCompletedFlag.Value = false;
                        }
                    })
                .WithReservedBits(1, 3)
                .WithFlag(0, out receiveEnabled, name: "RE")
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

            Registers.CommonControl1.Define(this)
                .WithReservedBits(30, 2)
                .WithTag("NFM", 29, 1)
                .WithTag("NFEN", 28, 1)
                .WithReservedBits(27, 1)
                .WithTag("NFCS", 24, 3)
                .WithReservedBits(21, 3)
                .WithTag("SHARPS", 20, 1)
                .WithReservedBits(17, 3)
                .WithTag("SPLP", 16, 1)
                .WithReservedBits(14, 2)
                .WithTag("RINV", 13, 1)
                .WithTag("TINV", 12, 1)
                .WithReservedBits(10, 2)
                .WithFlag(9, out useOddParity, name: "PM")
                .WithFlag(8, out parityEnabled, name: "PE")
                .WithReservedBits(6, 2)
                .WithTag("SPB2IO", 5, 1)
                .WithTag("SPB2DT", 4, 1)
                .WithReservedBits(2, 2)
                .WithTag("CTSPEN", 1, 1)
                .WithTag("CTSE", 0, 1);

            Registers.CommonControl2.Define(this)
                .WithTag("MDDR", 24, 8)
                .WithReservedBits(22, 2)
                .WithValueField(20, 2, out clockSelect, name: "CKS")
                .WithReservedBits(17, 3)
                .WithTag("BRME", 16, 1)
                .WithValueField(8, 8, out bitRate, name: "BRR")
                .WithTag("ABCSE2", 7, 1)
                .WithTag("ABCSE", 6, 1)
                .WithTag("ABCS", 5, 1)
                .WithTag("BGDM", 4, 1)
                .WithReservedBits(3, 1)
                .WithTag("BCP", 0, 3);

            Registers.CommonControl3.Define(this)
                .WithReservedBits(30, 2)
                .WithTag("BLK", 29, 1)
                .WithTag("GM", 28, 1)
                .WithReservedBits(27, 1)
                .WithTag("ACS0", 26, 1)
                .WithTag("CKE", 24, 2)
                .WithReservedBits(22, 2)
                .WithTag("DEN", 21, 1)
                .WithTag("FM", 20, 1)
                .WithTag("MP", 19, 1)
                .WithEnumField<DoubleWordRegister, CommunicationMode>(16, 3, out nonSmartCommunicationMode,
                    writeCallback: (_, __) => SetPeripheralMode(), name: "MOD")
                .WithTag("RXDESEL", 15, 1)
                .WithFlag(14, out useTwoStopBits, name: "STP")
                .WithTag("SINV", 13, 1)
                .WithTag("LSBF", 12, 1)
                .WithReservedBits(10, 2)
                .WithTag("CHR", 8, 2)
                .WithTag("BPEN", 7, 1)
                .WithReservedBits(2, 5)
                .WithTag("CPOL", 1, 1)
                .WithTag("CPHA", 0, 1);

            Registers.CommonControl4.Define(this)
                .WithTag("AET", 31, 1)
                .WithTag("ATT", 28, 3)
                .WithTag("AJD", 27, 1)
                .WithTag("AST", 24, 3)
                .WithReservedBits(20, 4)
                .WithTag("SCKSEL", 19, 1)
                .WithReservedBits(18, 1)
                .WithTag("ATEN", 17, 1)
                .WithTag("ASEN", 16, 1)
                .WithReservedBits(9, 7)
                .WithTag("CMPD", 0, 9);

            Registers.CommunicationEnableStatus.Define(this)
                .WithReservedBits(5, 27)
                .WithFlag(4, FieldMode.Read, name: "TIST", valueProviderCallback: _ => transmitEnabled.Value)
                .WithReservedBits(1, 3)
                .WithFlag(0, FieldMode.Read, name: "RIST", valueProviderCallback: _ => receiveEnabled.Value);

            Registers.SimpleIICControl.Define(this)
                .WithReservedBits(24, 8)
                .WithTag("IICSCLS", 22, 2)
                .WithTag("IICSDAS", 20, 2)
                .WithReservedBits(19, 1)
                .WithFlag(18, FieldMode.WriteOneToClear, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            this.DebugLog("Stop Condition Requested!");
                            EmulateIICStartStopCondition(IICCondition.Stop);
                        }
                    }, name: "IICSTPREQ")
                .WithFlag(17, FieldMode.WriteOneToClear, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            this.DebugLog("Restart Condition Requested!");
                            EmulateIICStartStopCondition(IICCondition.Restart);
                        }
                    }, name: "IICRSTAREQ")
                .WithFlag(16, FieldMode.WriteOneToClear, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            this.DebugLog("Start Condition Requested!");
                            EmulateIICStartStopCondition(IICCondition.Start);
                        }
                    }, name: "IICSTAREQ")
                .WithReservedBits(14, 2)
                .WithFlag(13,
                    writeCallback: (_, val) =>
                    {
                        if(useRxTxInterrupts.Value)
                        {
                            return;
                        }
                        if(val)
                        {
                            BlinkTxIRQ();
                        }
                    }, name:"IICACKT")
                .WithReservedBits(10, 3)
                .WithTag("IICCSC", 9, 1)
                .WithFlag(8, out useRxTxInterrupts, name: "IICINTM")
                .WithReservedBits(5, 3)
                .WithTag("IICDL", 0, 5)
                .WithWriteCallback((_, val) =>
                    {
                        var conditionRequestBits = val & 0x70000;
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

            Registers.FIFOControl.Define(this)
                .WithReservedBits(29, 3)
                .WithTag("RSTRG", 24, 5)
                .WithTag("RFRST[]", 23, 1)
                .WithReservedBits(21, 2)
                .WithTag("RTRG", 16, 5)
                .WithTag("TFRST", 15, 1)
                .WithReservedBits(13, 2)
                .WithTag("TTRG", 8, 5)
                .WithReservedBits(1, 7)
                .WithTag("DRES", 0, 1);

            Registers.ManchesterControl.Define(this)
                .WithReservedBits(27, 5)
                .WithTag("SBEREN", 26, 1)
                .WithTag("SYEREN", 25, 1)
                .WithTag("PFEREN", 24, 1)
                .WithReservedBits(22, 2)
                .WithTag("RPPAT", 20, 2)
                .WithTag("RPLEN", 16, 4)
                .WithReservedBits(14, 2)
                .WithTag("TPPAT", 12, 2)
                .WithTag("TPLEN", 8, 4)
                .WithReservedBits(7, 1)
                .WithTag("SBSEL", 6, 1)
                .WithTag("SYNSEL", 5, 1)
                .WithTag("SYNVAL", 4, 1)
                .WithReservedBits(3, 1)
                .WithTag("ERTEN", 2, 1)
                .WithTag("TMPOL", 1, 1)
                .WithTag("RMPOL", 0, 1);

            Registers.DriverControl.Define(this)
                .WithReservedBits(21, 11)
                .WithTag("DENGT", 16, 5)
                .WithReservedBits(13, 3)
                .WithTag("DEAST", 8, 5)
                .WithReservedBits(1, 7)
                .WithTag("DEPOL", 0, 1);

            Registers.SimpleLINControl0.Define(this)
                .WithReservedBits(26, 6)
                .WithTag("BCCS", 24, 2)
                .WithReservedBits(23, 1)
                .WithTag("AEDIE", 22, 1)
                .WithTag("COFIE", 21, 1)
                .WithTag("BFDIE", 20, 1)
                .WithReservedBits(18, 2)
                .WithTag("BCDIE", 17, 1)
                .WithTag("BFOIE", 16, 1)
                .WithTag("PIBS", 13, 3)
                .WithTag("PIBE", 12, 1)
                .WithTag("CF1DS", 10, 2)
                .WithTag("CF0RE", 9, 1)
                .WithTag("BFE", 8, 1)
                .WithReservedBits(2, 6)
                .WithTag("TCCS", 0, 2);

            Registers.SimpleLINControl1.Define(this)
                .WithTag("CF1CE", 24, 8)
                .WithTag("SCF1D", 16, 8)
                .WithTag("PCF1D", 8, 8)
                .WithReservedBits(6, 2)
                .WithTag("BMEN", 5, 1)
                .WithTag("SDST", 4, 1)
                .WithReservedBits(1, 3)
                .WithTag("TCST", 0, 1);

            Registers.SimpleLINControl2.Define(this)
                .WithTag("BFLW", 16, 16)
                .WithTag("CF0CE", 8, 8)
                .WithTag("CF0D", 0, 8);

            Registers.CommonStatus.Define(this)
                .WithTag("RDRF", 31, 1)
                .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => true, name: "TEND")
                .WithTag("TDRE", 29, 1)
                .WithTag("FER", 28, 1)
                .WithTag("PER", 27, 1)
                .WithTag("MFF", 26, 1)
                .WithReservedBits(25, 1)
                .WithTag("ORER", 24, 1)
                .WithReservedBits(19, 5)
                .WithTag("DFER", 18, 1)
                .WithTag("DPER", 17, 1)
                .WithTag("DCMF", 16, 1)
                .WithTag("RXDMON", 15, 1)
                .WithReservedBits(5, 10)
                .WithTag("ERS", 4, 1)
                .WithReservedBits(0, 4);

            Registers.SimpleIICStatus.Define(this)
                .WithReservedBits(4, 28)
                .WithFlag(3, out conditionCompletedFlag, FieldMode.Read, name: "IICSTIF")
                .WithReservedBits(1, 2)
                .WithTag("IICACKR", 0, 1);

            Registers.FIFOReceiveStatus.Define(this)
                .WithReservedBits(30, 2)
                .WithTag("FNUM", 24, 6)
                .WithReservedBits(22, 2)
                .WithTag("PNUM", 16, 6)
                .WithReservedBits(14, 2)
                .WithTag("R", 8, 6)
                .WithReservedBits(1, 7)
                .WithTag("DR", 0, 1);

            Registers.FIFOTransmitStatus.Define(this)
                .WithReservedBits(6, 26)
                .WithTag("T", 0, 6);

            Registers.ManchesterStatus.Define(this)
                .WithReservedBits(7, 25)
                .WithTag("RSYNC", 6, 1)
                .WithReservedBits(5, 1)
                .WithTag("MER", 4, 1)
                .WithReservedBits(3, 1)
                .WithTag("SBER", 2, 1)
                .WithTag("SYER", 1, 1)
                .WithTag("PFER", 0, 1);

            Registers.SimpleLINStatus0.Define(this)
                .WithTag("CF1RD", 24, 8)
                .WithTag("CF0RD", 16, 8)
                .WithTag("AEDF", 15, 1)
                .WithTag("COF", 14, 1)
                .WithTag("PIBDF", 13, 1)
                .WithTag("CF1MF", 12, 1)
                .WithTag("CF0MF", 11, 1)
                .WithTag("BFDF", 10, 1)
                .WithTag("BCDF", 9, 1)
                .WithTag("BFOF", 8, 1)
                .WithReservedBits(2, 6)
                .WithTag("RXDSF", 1, 1)
                .WithTag("SFSF", 0, 1);

            Registers.SimpleIICStatus1.Define(this)
                .WithReservedBits(16, 16)
                .WithTag("TCNT", 0, 16);

            Registers.CommonFlagClear.Define(this)
                .WithTag("RDRFC", 31, 1)
                .WithReservedBits(30, 1)
                .WithTag("TDREC", 29, 1)
                .WithTag("FERC", 28, 1)
                .WithTag("PERC", 27, 1)
                .WithTag("MFFC", 26, 1)
                .WithReservedBits(25, 1)
                .WithTag("ORERC", 24, 1)
                .WithReservedBits(19, 5)
                .WithTag("DFERC", 18, 1)
                .WithTag("DPERC", 17, 1)
                .WithTag("DCMFC", 16, 1)
                .WithReservedBits(5, 11)
                .WithTag("ERSC", 4, 1)
                .WithReservedBits(0, 4);

            Registers.SimpleIICFlagCLear.Define(this)
                .WithReservedBits(4, 28)
                .WithFlag(3, name: "IICSTIFC", writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            conditionCompletedFlag.Value = false;
                        }
                    })
                .WithReservedBits(0, 3);

            Registers.FIFOFlagClear.Define(this)
                .WithReservedBits(1, 31)
                .WithTag("DRC", 0, 1);

            Registers.ManchesterFlagClear.Define(this)
                .WithReservedBits(5, 27)
                .WithTag("MERC", 4, 1)
                .WithReservedBits(3, 1)
                .WithTag("SBERC", 2, 1)
                .WithTag("SYERC", 1, 1)
                .WithTag("PFERC", 0, 1);

            Registers.SimpleLINFlagClear.Define(this)
                .WithReservedBits(16, 16)
                .WithTag("AEDC", 15, 1)
                .WithTag("COFC", 14, 1)
                .WithTag("PIBDC", 13, 1)
                .WithTag("CF1MC", 12, 1)
                .WithTag("CF0MC", 11, 1)
                .WithTag("BFDC", 10, 1)
                .WithTag("BCDC", 9, 1)
                .WithTag("BFOC", 8, 1)
                .WithReservedBits(0, 8);
        }

        private void TransmitUART(byte value)
        {
            CharReceived?.Invoke(value);
        }

        private void TransmitSPI(byte value)
        {
            if(spiContainer.RegisteredPeripheral == null)
            {
                this.WarningLog("No SPI peripheral connected");
                return;
            }

            receiveQueue.Enqueue(spiContainer.RegisteredPeripheral.Transmit(value));
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
            DebugHelper.Assert((iicState == IICState.Idle) || (iicDirection != IICTransactionDirection.Unset), $"Incorrect communication direction {iicDirection} in state {iicState}");
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
            if(spiContainer.RegisteredPeripheral == null)
            {
                this.WarningLog("No SPI peripheral connected");
                return;
            }
            receiveQueue.Enqueue(spiContainer.RegisteredPeripheral.Transmit((byte)value));
        }

        // This is used in the non-FIFO mode where there is room just for a single byte, hence "full" means non-empty queue
        private bool IsReceiveDataFull => receiveQueue.Count > 0;
        // This is used in the non-FIFO. Single character is transmitted instantly so this is always true
        private bool IsDataTransmitted => true;

        private readonly IMachine machine;
        private readonly Queue<ushort> receiveQueue;
        private readonly SimpleContainerHelper<II2CPeripheral> i2cContainer;
        private readonly Queue<byte> iicTransmitQueue;

        private IICState iicState;
        private II2CPeripheral selectedIICSlave;
        private IICTransactionDirection iicDirection;

        private readonly NullRegistrationPointContainerHelper<IUART> uartContainer;
        private readonly NullRegistrationPointContainerHelper<ISPIPeripheral> spiContainer;

        private readonly ulong frequency;

        private PeripheralMode currentPeripheralMode;

        private IFlagRegisterField transmitEndInterruptEnabled;
        private IFlagRegisterField transmitInterruptEnabled;
        private IFlagRegisterField receiveInterruptEnabled;
        private IFlagRegisterField transmitEnabled;
        private IFlagRegisterField receiveEnabled;

        private IFlagRegisterField parityEnabled;
        private IFlagRegisterField useOddParity;

        private IFlagRegisterField useTwoStopBits;

        private IValueRegisterField clockSelect;
        private IValueRegisterField bitRate;

        private IFlagRegisterField conditionCompletedFlag;

        private IFlagRegisterField useRxTxInterrupts;

        private IEnumRegisterField<CommunicationMode> nonSmartCommunicationMode;

        private const int InterruptDelay = 1;
        private const int IICReadBufferCount = 24;
        // This byte is used to trigger reception on IIC bus. It should not be transmitted
        private const byte DummyTransmitByte = 0xFF;

        private enum Registers
        {
            ReceiveData = 0x0,
            TransmitData = 0x4,
            CommonControl0 = 0x8,
            CommonControl1 = 0xC,
            CommonControl2 = 0x10,
            CommonControl3 = 0x14,
            CommonControl4 = 0x18,
            CommunicationEnableStatus = 0x1C,
            SimpleIICControl = 0x20,
            FIFOControl = 0x24,
            ManchesterControl = 0x2C,
            DriverControl = 0x30,
            SimpleLINControl0 = 0x34,
            SimpleLINControl1 = 0x38,
            SimpleLINControl2 = 0x3C,
            CommonStatus = 0x48,
            SimpleIICStatus = 0x4C,
            FIFOReceiveStatus = 0x50,
            FIFOTransmitStatus = 0x54,
            ManchesterStatus = 0x58,
            SimpleLINStatus0 = 0x5C,
            SimpleIICStatus1 = 0x60,
            CommonFlagClear = 0x68,
            SimpleIICFlagCLear = 0x6C,
            FIFOFlagClear = 0x70,
            ManchesterFlagClear = 0x74,
            SimpleLINFlagClear = 0x78,
        }

        private enum CommunicationMode
        {
            Asynchronous        = 0b000,
            SmartCardInterface  = 0b001,
            ClockSynchronous    = 0b010,
            SimpleSPI           = 0b011,
            SimpleI2C           = 0b100,
            Machnester          = 0b101,
            SimpleLIN           = 0b111,
        }

        private enum PeripheralMode
        {
            UART,
            SPI,
            IIC,
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

        private enum IICCondition
        {
            Start,
            Stop,
            Restart,
        }
    }
}


