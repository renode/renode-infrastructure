//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SCI
{
    // Due to unusual register offsets we cannot use address translations
    public class RenesasRA6M5_SCI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IUART, IWordPeripheral, IBytePeripheral, IProvidesRegisterCollection<WordRegisterCollection>, IKnownSize,
        IPeripheralContainer<IUART, NullRegistrationPoint>
    {
        public RenesasRA6M5_SCI(IMachine machine, ulong frequency, bool enableManchesterMode, bool enableFIFO) : base(machine)
        {
            this.machine = machine;
            this.frequency = frequency;
            ReceiveIRQ = new GPIO();
            TransmitIRQ = new GPIO();
            TransmitEndIRQ = new GPIO();
            receiveQueue = new Queue<ushort>();
            RegistersCollection = new WordRegisterCollection(this);

            DefineRegisters(enableManchesterMode, enableFIFO);
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

            ReceiveIRQ.Unset();
            TransmitIRQ.Unset();
            TransmitEndIRQ.Unset();
            RegistersCollection.Reset();
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

        public long Size => 0x20;

        IEnumerable<IRegistered<IUART, NullRegistrationPoint>> IPeripheralContainer<IUART, NullRegistrationPoint>.Children
        {
            get => registeredUartPeripheral != null ?
                new [] { Registered.Create(registeredUartPeripheral, NullRegistrationPoint.Instance) } :
                Enumerable.Empty<IRegistered<IUART, NullRegistrationPoint>>();
        }

        public event Action<byte> CharReceived;

        private void UpdateInterrupts()
        {
            var rxState = receiveInterruptEnabled.Value && (fifoMode ? IsReceiveFIFOFull : IsReceiveDataFull);
            var txState = transmitInterruptEnabled.Value && (fifoMode ? transmitFIFOEmpty.Value : true);
            var teState = transmitEndInterruptEnabled.Value;
            this.DebugLog("ReceiveIRQ: {0}, TransmitIRQ: {1}, TransmitEndIRQ: {2}.", rxState ? "set" : "unset", txState ? "set" : "unset", teState ? "set" : "unset");
            ReceiveIRQ.Set(rxState);
            TransmitIRQ.Set(txState);
            TransmitEndIRQ.Set(teState);
        }

        private void DefineRegisters(bool enableManchesterMode, bool enableFIFO)
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
                .WithTaggedFlag("CM", 7)
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
                        if(RegisteredPeripheral == null)
                        {
                            // Act as an UART
                            TransmitData((byte)value);
                        }
                        else
                        {
                            // Act as a SPI
                            receiveQueue.Enqueue(RegisteredPeripheral.Transmit((byte)value));
                        }
                        UpdateInterrupts();
                    })
                .WithReservedBits(8, 8);

            // Non-Smart Card, Non-FIFO, Non-Manchester
            Registers.SerialStatusNonSmartCardNonFIFO.DefineConditional(this, () => !smartCardMode.Value && !fifoMode && !manchesterMode, 0x84)
                .WithTaggedFlag("MPBT", 0)
                .WithTaggedFlag("MPB", 1)
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => true, name: "TEND")
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
                .WithTaggedFlag("SDIR", 3)
                .WithTaggedFlag("CHR1", 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("BCP2", 7)
                .WithReservedBits(8, 8);

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
                .WithTaggedFlag("IICM", 0)
                .WithReservedBits(1, 2)
                .WithTag("IICDL", 3, 5)
                .WithReservedBits(8, 8);

            Registers.IICMode2.Define(this)
                .WithTaggedFlag("IICINTM", 0)
                .WithTaggedFlag("IICCSC", 1)
                .WithReservedBits(2, 3)
                .WithTaggedFlag("IICACKT", 5)
                .WithReservedBits(6, 10);

            Registers.IICMode3.Define(this)
                .WithTaggedFlag("IICSTAREQ", 0)
                .WithTaggedFlag("IICRSTAREQ", 1)
                .WithTaggedFlag("IICSTPREQ", 2)
                .WithTaggedFlag("IICSTIF", 3)
                .WithTag("IICSDAS", 4, 2)
                .WithTag("IICSCLS", 6, 2)
                .WithReservedBits(8, 8);

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
                Registers.TransmitFIFOData.DefineConditional(this, () => fifoMode, 0xff)
                    .WithValueField(0, 9, FieldMode.Write, name: "TDAT",
                        writeCallback: (_, val) =>
                        {
                            TransmitData((byte)(val >> 8));
                            TransmitData((byte)val);
                            transmitFIFOEmpty.Value = true;
                            UpdateInterrupts();
                        })
                    .WithTaggedFlag("MPBT", 9)
                    .WithReservedBits(10, 6);
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
                    .WithFlag(1, name: "RFRST",
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

        private void TransmitData(byte value)
        {
            CharReceived?.Invoke(value);
        }

        private readonly IMachine machine;
        private readonly Queue<ushort> receiveQueue;
        private readonly ulong frequency;
        private Parity parityBit = Parity.Even;

        private IUART registeredUartPeripheral;

        private bool manchesterMode;
        private bool fifoMode;
        private IFlagRegisterField transmitEnabled;
        private IFlagRegisterField hasTwoStopBits;
        private IFlagRegisterField parityEnabled;
        private IFlagRegisterField transmitEndInterruptEnabled;
        private IFlagRegisterField receiveEnabled;
        private IFlagRegisterField receiveInterruptEnabled;
        private IFlagRegisterField transmitInterruptEnabled;
        private IFlagRegisterField smartCardMode;
        private IFlagRegisterField transmitFIFOEmpty;
        private IValueRegisterField receiveFIFOTriggerCount;
        private IValueRegisterField bitRate;
        private IValueRegisterField clockSource;

        private const ulong MaxFIFOSize = 16;
        private const int InterruptDelay = 1;

        private enum Registers
        {
            SerialModeNonSmartCard = 0x00,
            SerialModeSmartCard = 0x00,
            BitRate = 0x01,
            SerialControlNonSmartCard = 0x02,
            SerialControlSmartCard = 0x02,
            TransmitData = 0x03,
            SerialStatusNonSmartCardNonFIFO = 0x04,
            SerialStatusNonSmartCardFIFO = 0x04,
            SerialStatusSmartCard = 0x04,
            SerialStatusManchesterMode = 0x04,
            ReceiveData = 0x05,
            SmartCardMode = 0x06,
            SerialExtendedMode = 0x07,
            NoiseFilterSetting = 0x08,
            IICMode1 = 0x09,
            IICMode2 = 0x0A,
            IICMode3 = 0x0B,
            IICStatus = 0x0C,
            SPIMode = 0x0D,
            TransmitDataNonManchesterMode = 0xE,
            TransmitDataManchesterMode = 0xE,
            TransmitFIFOData = 0xE,
            ReceiveDataNonManchesterMode = 0x10,
            ReceiveDataManchesterMode = 0x10,
            ReceiveFIFOData = 0x10,
            ModulationDuty = 0x12,
            DataCompareMatchControl = 0x13,
            FIFOControl = 0x14,
            FIFODataCount = 0x16,
            LineStatus = 0x18,
            CompareMatchData = 0x1A,
            SerialPort = 0x1C,
            AdjustmentCommunicationTiming = 0x1D,
            ExtendedSerialModuleEnable = 0x20,
            ManchesterMode = 0x20,
            Control0 = 0x21,
            Control1 = 0x22,
            TransmitManchesterPrefaceSetting = 0x22,
            Control2 = 0x23,
            ReceiveManchesterPrefaceSetting = 0x23,
            Control3 = 0x24,
            ManchesterExtendedErrorStatus = 0x24,
            PortControl = 0x25,
            ManchesterExtendedErrorControl = 0x25,
            InterruptControl = 0x26,
            Status = 0x27,
            StatusClear = 0x28,
            ControlField0Data = 0x29,
            ControlField0CompareEnable = 0x2A,
            ControlField0RecieveData = 0x2B,
            PrimaryControlField1Data = 0x2C,
            SecondaryControlField1Data = 0x2D,
            ControlField1CompareEnable = 0x2E,
            ControlField1ReceiveData = 0x2F,
            TimerControl = 0x30,
            TimerMode = 0x31,
            TimerPrescaler = 0x32,
            TimerCount = 0x33,
        }
    }
}
