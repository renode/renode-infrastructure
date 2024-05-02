//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class NPCX_SMBus : SimpleContainer<II2CPeripheral>, IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IKnownSize
    {
        public NPCX_SMBus(IMachine machine) : base(machine)
        {
            RegistersCollection = new ByteRegisterCollection(this);
            IRQ = new GPIO();

            txQueue = new Queue<byte>();
            rxQueue = new Queue<byte>();

            DefineRegisters();
            DefineFIFORegisters();
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            RegistersCollection.Reset();

            CurrentState = State.Idle;
            activePeripheral = null;

            IRQ.Unset();
            rxQueue.Clear();
            txQueue.Clear();
        }

        public long Size => 0x20;

        public ByteRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; }

        private void HandleWrite(byte value)
        {
            switch(CurrentState)
            {
                case State.Idle:
                    this.Log(LogLevel.Warning, "Tried writing to SMB_DAT while in idle state");
                    break;

                case State.Start:
                    // NOTE: On Repeated Start we have to first send all the data
                    HandleStop();

                    var address = value >> 1;
                    var read = (value & 1) > 0;

                    if(!TryGetByAddress(address, out activePeripheral))
                    {
                        this.Log(LogLevel.Warning, "No I2C device with address {0} is connected", address);
                        negativeAcknowledge.Value = true;
                        CurrentState = State.Idle;
                        UpdateInterrupts();
                        break;
                    }

                    CurrentState = read ? State.Reading : State.Writing;
                    if(CurrentState == State.Reading)
                    {
                        TryReadFromPeripheral();
                    }
                    else
                    {
                        readyForTransaction.Value = true;
                        UpdateInterrupts();
                    }
                    break;

                case State.Writing:
                    txQueue.Enqueue(value);

                    readyForTransaction.Value |= true;
                    rxFullTxEmptyStatus.Value |= true;
                    UpdateInterrupts();
                    break;

                default:
                    this.Log(LogLevel.Warning, "Trying to write data in wrong state: {0}, ignoring", CurrentState);
                    return;
            }
        }

        private void HandleStop()
        {
            if(CurrentState == State.Start || CurrentState == State.Writing)
            {
                if(txQueue.Count > 0)
                {
                    activePeripheral?.Write(txQueue.ToArray());
                    txQueue.Clear();
                }
                rxFullTxEmptyStatus.Value = true;
            }

            if(CurrentState != State.Start)
            {
                this.Log(LogLevel.Debug, "Finishing transmission");

                CurrentState = State.Idle;
                activePeripheral?.FinishTransmission();
            }

            activePeripheral = null;
            readyForTransaction.Value = false;
            UpdateInterrupts();
        }

        private void TryReadFromPeripheral()
        {
            if(!fifoMode.Value)
            {
                var data = activePeripheral.Read(1);
                rxQueue.Enqueue(data.Length > 0 ? data[0] : (byte)0);

                readyForTransaction.Value = true;
                UpdateInterrupts();
                return;
            }

            rxQueue.EnqueueRange(activePeripheral.Read((int)rxFIFOThreshold.Value));

            rxFullTxEmptyStatus.Value |= rxQueue.Count == FIFOLength;
            rxFIFOThresholdStatus.Value |= fifoMode.Value && rxQueue.Count > 0 && rxQueue.Count == (int)rxFIFOThreshold.Value;

            if((rxQueue.Count > 0) ||
               (rxFIFOThresholdStatus.Value && !rxFIFOThresholdInterrupt.Value))
            {
                // NOTE: This should be set to true if:
                //  * there is data to read from the FIFO
                //  * we hit RX FIFO threshold and respective interrupt is _not_ enabled
                readyForTransaction.Value = true;
            }

            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var interrupt = false;
            if(interruptsEnabled.Value)
            {
                interrupt |= negativeAcknowledge.Value;
                interrupt |= readyForTransaction.Value;
                interrupt |= rxFIFOThresholdInterrupt.Value && rxFIFOThresholdStatus.Value;
                interrupt |= rxFullTxEmptyInterrupt.Value && rxFullTxEmptyStatus.Value;
            }

            this.Log(LogLevel.Debug, "Setting IRQ to {0} (interruptsEnabled={1}, negativeAcknowledge={2}, readyForTransaction={3} rxFIFOThreshold={4}, rxFullTxEmpty={5})",
                interrupt,
                interruptsEnabled.Value,
                negativeAcknowledge.Value,
                readyForTransaction.Value,
                rxFIFOThresholdInterrupt.Value && rxFIFOThresholdStatus.Value,
                rxFullTxEmptyInterrupt.Value && rxFullTxEmptyStatus.Value);

            IRQ.Set(interrupt);
        }

        private void DefineRegisters()
        {
            Registers.SerialData.Define(this)
                .WithValueField(0, 8, name: "SMB_DAT (SMBus Data)",
                    valueProviderCallback: _ =>
                    {
                        if(rxQueue.TryDequeue(out var val))
                        {
                            UpdateInterrupts();
                            return val;
                        }

                        readyForTransaction.Value = false;
                        if(lastPacket.Value)
                        {
                            HandleStop();
                        }
                        return (byte)0;
                    },
                    writeCallback: (_, value) => HandleWrite((byte)value))
            ;

            Registers.Status.Define(this)
                .WithFlag(0, name: "XMIT (Transmit Mode)",
                    valueProviderCallback: _ => CurrentState == State.Writing)
                .WithFlag(1, FieldMode.Read, name: "MASTER (Master Mode)",
                    valueProviderCallback: _ => CurrentState != State.Idle)
                .WithTaggedFlag("NMATCH (New Match)", 2)
                .WithTaggedFlag("STASTR (Stall After Start)", 3)
                .WithFlag(4, out negativeAcknowledge, FieldMode.Read | FieldMode.WriteOneToClear, name: "NEGACK (Negative Acknowledge)")
                .WithTaggedFlag("BER (Bus Error)", 5)
                // NOTE: This field should be set to true when:
                // * there is data to read from the RX buffer in receive mode
                // * TX buffer is empty in transmit mode
                // * in FIFO mode, either RX or TX threshold happened, and their respective interrupts aren't enabled
                .WithFlag(6, out readyForTransaction, name: "SDAST (SDA Status)")
                .WithTaggedFlag("SLVSTP (Slave Stop)", 7)
            ;

            Registers.ControlStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "BUSY (Module Busy)",
                    valueProviderCallback: _ => CurrentState != State.Idle)
                .WithTaggedFlag("BB (Bus Ready)", 1)
                .WithTaggedFlag("MATCH (Address Match)", 2)
                .WithTaggedFlag("GCMATCH (Global Call Match)", 3)
                .WithTaggedFlag("TSDA (Test SDA Line)", 4)
                .WithTaggedFlag("TGSCL (Toggle SCL Line)", 5)
                .WithTaggedFlag("MATCHAF (Match Address Field)", 6)
                .WithTaggedFlag("ARPMATCH (ARP Address Match)", 7)
            ;

            Registers.Control1.Define(this)
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "START",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            CurrentState = State.Start;
                            readyForTransaction.Value = true;

                            UpdateInterrupts();
                        }
                    }
                )
                .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "STOP",
                    writeCallback: (_, value) => { if(value) HandleStop(); })
                .WithFlag(2, out interruptsEnabled, name: "INTEN (Interrupt Enable)")
                .WithTaggedFlag("EOBINTE (End of 'Busy' Interrupt Enable)", 3)
                .WithTaggedFlag("ACK (Acknowledge)", 4)
                .WithTaggedFlag("GCMEN (Global Call Match Enable)", 5)
                .WithTaggedFlag("NMINTE (New Match Interrupt Enable)", 6)
                .WithTaggedFlag("STASTRE (Stall After Start Enable)", 7)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            var ownAddressRegisters = new[]
            {
                Registers.OwnAddress1,
                Registers.OwnAddress2,
                Registers.OwnAddress3,
                Registers.OwnAddress4,
                Registers.OwnAddress5,
                Registers.OwnAddress6,
                Registers.OwnAddress7,
                Registers.OwnAddress8,
            };

            foreach(var registerAddress in ownAddressRegisters)
            {
                var register = (long)registerAddress < CommonRegisterSize ?
                    registerAddress.Define(this) :
                    registerAddress.DefineConditional(this, () => FirstBankSelected)
                ;

                register
                    .WithTag("ADDR (Address)", 0, 7)
                    .WithTaggedFlag("SAEN (Slave Address Enable)", 7)
                ;
            }

            Registers.Control2.Define(this)
                .WithFlag(0, out moduleEnabled, name: "ENABLE")
                .WithTag("SCLFRQ6-0 (SCL Frequency bits 6 through 0)", 1, 7)
            ;

            Registers.Control3.Define(this)
                .WithTag("SCLFRQ8-7 (SCL Frequency bits 8 and 7)", 0, 2)
                .WithTaggedFlag("ARPMEN (ARP Match Enable)", 2)
                .WithTaggedFlag("SLP_START (Start Detect in Sleep Enable)", 3)
                // XXX: Don't allow for Slave mode when this field is set
                .WithFlag(4, name: "400K_MODE (400 kHz Master Enable)")
                .WithFlag(5, out bankSelect, name: "BNK_SEL (Bank Select)")
                .WithFlag(6, name: "SDA_LVL (SDA Level)",
                    valueProviderCallback: _ => CurrentState == State.Idle)
                .WithFlag(7, name: "SCL_LVL (SCL Level)",
                    valueProviderCallback: _ => CurrentState == State.Idle)
            ;

            var busTimeoutRegister = new ByteRegister(this)
                .WithTag("TO_CKDIV (Timeout Clock Divisor)", 0, 6)
                .WithTaggedFlag("T_OUTIE (Timeout Interrupt Enable)", 6)
                .WithTaggedFlag("T_OUTST (Timeout Status)", 7)
            ;

            RegistersCollection.AddRegister((long)Registers.BusTimeout, busTimeoutRegister);
            RegistersCollection.AddConditionalRegister((long)Registers.BusTimeout2, busTimeoutRegister, () => !FirstBankSelected);

            Registers.ControlStatus2.DefineConditional(this, () => FirstBankSelected)
                .WithTaggedFlag("MATCHA1F (Match Address 1 Field)", 0)
                .WithTaggedFlag("MATCHA2F (Match Address 2 Field)", 1)
                .WithTaggedFlag("MATCHA3F (Match Address 3 Field)", 2)
                .WithTaggedFlag("MATCHA4F (Match Address 4 Field)", 3)
                .WithTaggedFlag("MATCHA5F (Match Address 5 Field)", 4)
                .WithTaggedFlag("MATCHA6F (Match Address 6 Field)", 5)
                .WithTaggedFlag("MATCHA7F (Match Address 7 Field)", 6)
                .WithTaggedFlag("INTSTS (Interrupt Status)", 7)
            ;

            Registers.ControlStatus3.DefineConditional(this, () => FirstBankSelected)
                .WithTaggedFlag("MATCHA8F (Match Address 8 Field)", 0)
                .WithReservedBits(1, 6)
                .WithTaggedFlag("EO_BUSY (End of 'Busy')", 7)
            ;

            Registers.Control4.DefineConditional(this, () => FirstBankSelected)
                .WithTag("GLDT (SDA Hold Time)", 0, 6)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("LVL_WE (Level Control Write Enable)", 7)
            ;

            Registers.SCLLowTime.DefineConditional(this, () => FirstBankSelected)
                .WithTag("SCLLT7-0 (SCL Low Time)", 0, 8)
            ;

            Registers.FIFOControl.DefineConditional(this, () => FirstBankSelected)
                .WithReservedBits(0, 4)
                .WithFlag(4, out fifoMode, name: "FIFO_EN (Enable FIFO Mode)")
                .WithReservedBits(5, 3)
            ;

            Registers.SCLHighTime.DefineConditional(this, () => FirstBankSelected)
                .WithTag("SCLHT7-0 (SCL High Time)", 0, 8)
            ;
        }

        private void DefineFIFORegisters()
        {
            Registers.FIFOControlAndStatus.DefineConditional(this, () => !FirstBankSelected)
                .WithReservedBits(0, 1)
                .WithFlag(1, out rxFullTxEmptyStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "RXF_TXE (Rx-FIFO Full, Tx-FIFO Empty Status)")
                .WithReservedBits(2, 1)
                .WithFlag(3, out rxFullTxEmptyInterrupt, name: "RFTE_IE (Rx-FIFO Full, Tx-FIFO Empty Interrupt Enable)")
                .WithReservedBits(4, 2)
                .WithFlag(6, FieldMode.WriteOneToClear, name: "CLR_FIFO (Clear FIFOs)",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            rxQueue.Clear();
                            txQueue.Clear();
                        }
                    })
                .WithTaggedFlag("SLVRSTR (Slave Start or Restart)", 7)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.TxFIFOControl.DefineConditional(this, () => !FirstBankSelected)
                .WithTag("TX_THR (Tx-FIFO Threshold)", 0, 6)
                .WithTaggedFlag("THR_TXIE (Threshold Tx-FIFO Interrupt Enable)", 6)
                .WithReservedBits(7, 1)
            ;

            Registers.FrameTimeout.DefineConditional(this, () => !FirstBankSelected)
                .WithTag("FR_LEN_TO (Frame Length Timeout)", 0, 6)
                .WithTaggedFlag("FRTOIE (Frame Timeout Interrupt Enable)", 6)
                .WithTaggedFlag("FRTOST (Frame Timeout Status)", 7)
            ;

            Registers.PECData.DefineConditional(this, () => !FirstBankSelected)
                .WithTag("PEC_DATA (PEC Data)", 0, 7)
            ;

            Registers.TxFIFOStatus.DefineConditional(this, () => !FirstBankSelected)
                .WithTag("TX_BYTES (Tx-FIFO Number of Bytes)", 0, 6)
                .WithTaggedFlag("TX_THST (Tx-FIFO Threshold Status)", 6)
                .WithReservedBits(7, 1)
            ;

            Registers.RxFIFOStatus.DefineConditional(this, () => !FirstBankSelected)
                .WithValueField(0, 6, name: "RX_BYTES (Rx-FIFO Number of Bytes)",
                    valueProviderCallback: _ => (uint)rxQueue.Count)
                .WithFlag(6, out rxFIFOThresholdStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "RX_THST (Rx-FIFO Threshold Status)")
                .WithReservedBits(7, 1)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.RxFIFOControl.DefineConditional(this, () => !FirstBankSelected)
                .WithValueField(0, 6, out rxFIFOThreshold, name: "RX_THR (Rx-FIFO Threshold)")
                .WithFlag(6, out rxFIFOThresholdInterrupt, name: "THR_RXIE (Threshold Rx-FIFO Interrupt Enable)")
                .WithFlag(7, out lastPacket, name: "LAST_PEC (Last Byte or PEC Byte)")
            ;
        }

        private bool FirstBankSelected => !fifoMode.Value || !bankSelect.Value;
        private State CurrentState
        {
            get => currentState;
            set
            {
                if(!moduleEnabled.Value)
                {
                    this.Log(LogLevel.Warning, "Tried to change state to {0}, but module is disabled", value);
                    return;
                }
                this.Log(LogLevel.Debug, "Changing state from {0} to {1}", currentState, value);
                currentState = value;
            }
        }

        private readonly Queue<byte> txQueue;
        private readonly Queue<byte> rxQueue;

        private State currentState;
        private II2CPeripheral activePeripheral;

        private IFlagRegisterField interruptsEnabled;
        private IFlagRegisterField rxFIFOThresholdInterrupt;
        private IFlagRegisterField negativeAcknowledge;
        private IFlagRegisterField rxFullTxEmptyStatus;
        private IFlagRegisterField rxFullTxEmptyInterrupt;
        private IFlagRegisterField rxFIFOThresholdStatus;

        private IFlagRegisterField fifoMode;
        private IFlagRegisterField bankSelect;
        private IFlagRegisterField moduleEnabled;
        private IFlagRegisterField lastPacket;

        private IValueRegisterField rxFIFOThreshold;
        private IFlagRegisterField readyForTransaction;
        private const long CommonRegisterSize = 0x10;
        private const long FIFOLength = 32;

        private enum State
        {
            Idle,
            Start,
            Reading,
            Writing,
        }

        private enum Registers
        {
            // Common registers
            SerialData = 0x00,
            Status = 0x02,
            ControlStatus = 0x04,
            Control1 = 0x06,
            OwnAddress1 = 0x08,
            Control2 = 0x0A,
            OwnAddress2 = 0x0C,
            Control3 = 0x0E,
            BusTimeout = 0x0F,

            // Bank0 registers
            OwnAddress3 = 0x10,
            OwnAddress7 = 0x11,
            OwnAddress4 = 0x12,
            OwnAddress8 = 0x13,
            OwnAddress5 = 0x14,
            OwnAddress6 = 0x16,
            ControlStatus2 = 0x18,
            ControlStatus3 = 0x19,
            Control4 = 0x1A,
            SCLLowTime = 0x1C,
            FIFOControl = 0x1D,
            SCLHighTime = 0x1E,

            // Bank1 registers
            FIFOControlAndStatus = 0x10,
            TxFIFOControl = 0x12,
            FrameTimeout = 0x13,
            BusTimeout2 = 0x14,
            PECData = 0x16,
            TxFIFOStatus = 0x1A,
            RxFIFOStatus = 0x1C,
            RxFIFOControl = 0x1E,
        }
    }
}
