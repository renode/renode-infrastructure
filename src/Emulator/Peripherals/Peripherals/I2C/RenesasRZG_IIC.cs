//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class RenesasRZG_IIC : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasRZG_IIC(IMachine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);

            ReceiveIRQ = new GPIO();
            TransmitIRQ = new GPIO();
            TransmitEndIRQ = new GPIO();
            NackIRQ = new GPIO();
            StartIRQ = new GPIO();
            StopIRQ = new GPIO();
            
            writeQueue = new Queue<byte>();
            readQueue = new Queue<byte>();

            startCondition = new Condition(this, "Start", HandleStartCondition);
            stopCondition = new Condition(this, "Stop", HandleStopCondition);
            restartCondition = new Condition(this, "Restart", HandleRestartCondition);

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

        public override void Reset()
        {
            RegistersCollection.Reset();
            InternalReset();
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }
        public long Size => 0x400;

        public GPIO ReceiveIRQ { get; }
        public GPIO TransmitIRQ { get; }
        public GPIO TransmitEndIRQ { get; }
        public GPIO NackIRQ { get; }
        public GPIO StartIRQ { get; }
        public GPIO StopIRQ { get; }

        private void DefineRegisters()
        {
            Registers.Control1.Define(this, 0x1F)
                .WithTaggedFlag("SDAI", 0)
                .WithTaggedFlag("SCLI", 1)
                .WithTaggedFlag("SDAO", 2)
                .WithTaggedFlag("SCLO", 3)
                .WithTaggedFlag("SOWP", 4)
                .WithTaggedFlag("CLO", 5)
                .WithFlag(6, out var iicReset, name: "IICRST")
                .WithFlag(7, out peripheralEnable, name: "ICE")
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, __) =>
                {
                    if(!iicReset.Value)
                    {
                        return;
                    }

                    // Both IICRST and ICE fields are not affected by this reset operation.
                    // See: Table 26.6 in RZ/G Group User's Manual
                    // We only need to save the value of ICE as we know IICRST is 1 here
                    var peripheralEnableValue = peripheralEnable.Value;

                    if(peripheralEnable.Value)
                    {
                        InternalReset();
                        this.DebugLog("Internal reset performed");
                    }
                    else
                    {
                        Reset();
                        this.DebugLog("IIC reset performed");
                    }

                    iicReset.Value = true;
                    peripheralEnable.Value = peripheralEnableValue;
                });

            control2Register = Registers.Control2.Define(this)
                .WithReservedBits(0, 1)
                .WithFlag(1, out startCondition.RequestFlag, name: "ST",
                    changeCallback: (_, __) =>
                    {
                        if(!startCondition.Requested)
                        {
                            return;
                        }

                        if(!peripheralEnable.Value)
                        {
                            this.ErrorLog("Attempted to start transmission on a disabled peripheral, clearing the request");
                            stopCondition.Requested = false;
                            return;
                        }

                        if(currentTransmissionState != TransmissionState.Idle)
                        {
                            this.WarningLog("Attempted to start a transaction while one is already running, clearing the request");
                            startCondition.Requested = false;
                            return;
                        }

                        startCondition.TryPerform();
                    })
                .WithFlag(2, out restartCondition.RequestFlag, name: "RS",
                    changeCallback: (_, __) =>
                    {
                        if(!restartCondition.Requested)
                        {
                            return;
                        }

                        if(!peripheralEnable.Value)
                        {
                            this.ErrorLog("Attempted to restart transmission on a disabled peripheral, clearing the request");
                            restartCondition.Requested = false;
                            return;
                        }

                        // When reading the condition will be generated after the last read has been performed
                        if(currentTransmissionState != TransmissionState.Read)
                        {
                            restartCondition.TryPerform();
                        }
                    })
                .WithFlag(3, out stopCondition.RequestFlag, name: "SP",
                    changeCallback: (_, __) =>
                    {
                        // When reading the condition will be generated after the last read has been performed
                        if(currentTransmissionState != TransmissionState.Read)
                        {
                            stopCondition.TryPerform();
                        }
                    })
                .WithReservedBits(4, 1)
                .WithFlag(5, name: "TRS",
                    valueProviderCallback: _ => currentTransmissionState != TransmissionState.Read,
                    changeCallback: (_, __) => this.WarningLog("Writing to TRS (Transmit/Receive Mode) is not supported in this model and has no effect"))
                .WithFlag(6, name: "MST",
                    valueProviderCallback: _ => currentTransmissionState != TransmissionState.Idle,
                    changeCallback: (_, __) => this.WarningLog("Writing to MST (Master/Slave Mode) is not supported in this model and has no effect"))
                .WithFlag(7, FieldMode.Read, name: "BBSY",
                    valueProviderCallback: _ => currentTransmissionState != TransmissionState.Idle)
                .WithReservedBits(8, 24);

            Registers.Mode1.Define(this, 0x8)
                .WithTag("BC[2:0]", 0, 3)
                .WithTaggedFlag("BCWP", 3)
                .WithTag("CKS[2:0]", 4, 3)
                .WithReservedBits(7, 25);

            Registers.Mode2.Define(this, 0x6)
                .WithTaggedFlag("TMOS", 0)
                .WithTaggedFlag("TMOL", 1)
                .WithTaggedFlag("TMOH", 2)
                .WithReservedBits(3, 1)
                .WithTag("SDDL[2:0]", 4, 3)
                .WithTaggedFlag("DLCS", 7)
                .WithReservedBits(8, 24);

            Registers.Mode3.Define(this)
                .WithTag("NF[1:0]", 0, 2)
                .WithTaggedFlag("ACKBR", 2)
                .WithFlag(3, out transmitNegativeAcknowledge, name: "ACKBT",
                    changeCallback: (previous, _) =>
                    {
                        if(!transmitAcknowledgeWriteEnable.Value)
                        {
                            this.WarningLog("Attempted to set ACKBT (Transmit Acknowledge) without disabling write protection, ignoring");
                            transmitNegativeAcknowledge.Value = previous;
                        }
                    })
                .WithFlag(4, out transmitAcknowledgeWriteEnable, name: "ACKWP")
                .WithTaggedFlag("RDRFS", 5)
                .WithTaggedFlag("WAIT", 6)
                .WithTaggedFlag("SMBE", 7)
                .WithReservedBits(8, 24);

            Registers.FunctionEnable.Define(this, 0x72)
                .WithTaggedFlag("TMOE", 0)
                .WithTaggedFlag("MALE", 1)
                .WithTaggedFlag("NALE", 2)
                .WithTaggedFlag("SALE", 3)
                .WithTaggedFlag("NACKE", 4)
                .WithTaggedFlag("NFE", 5)
                .WithTaggedFlag("SCLE", 6)
                .WithTaggedFlag("FMPE", 7)
                .WithReservedBits(8, 24);

            Registers.StatusEnable.Define(this, 0x9)
                .WithTaggedFlag("SAR0", 0)
                .WithTaggedFlag("SAR1", 1)
                .WithTaggedFlag("SAR2", 2)
                .WithTaggedFlag("GCE", 3)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("DIDE", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("HOAE", 7)
                .WithReservedBits(8, 24);

            Registers.InterruptEnable.Define(this)
                .WithTaggedFlag("TMOIE", 0)
                .WithTaggedFlag("ALIE", 1)
                .WithFlag(2, out startInterrupt.Enable, name: "STIE")
                .WithFlag(3, out stopInterrupt.Enable, name: "SPIE")
                .WithFlag(4, out nackInterrupt.Enable, name: "NAKIE")
                .WithFlag(5, out receiveInterrupt.Enable, name: "RIE")
                .WithFlag(6, out transmitEndInterrupt.Enable, name: "TEIE")
                .WithFlag(7, out transmitInterrupt.Enable, name: "TIE")
                .WithReservedBits(8, 24)
                .WithChangeCallback((_, __) => UpdateInterrupts());

            status1Register = Registers.Status1.Define(this)
                .WithTaggedFlag("AAS0", 0)
                .WithTaggedFlag("AAS1", 1)
                .WithTaggedFlag("AAS2", 2)
                .WithTaggedFlag("GCA", 3)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("DID", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("HOA", 7)
                .WithReservedBits(8, 24);

            status2Register = Registers.Status2.Define(this)
                .WithTaggedFlag("TMOF", 0)
                .WithTaggedFlag("AL", 1)
                .WithFlag(2, out startInterrupt.FlagField, FieldMode.Read | FieldMode.WriteZeroToClear, name: "START")
                .WithFlag(3, out stopInterrupt.FlagField, FieldMode.Read | FieldMode.WriteZeroToClear, name: "STOP")
                .WithFlag(4, out nackInterrupt.FlagField, FieldMode.Read | FieldMode.WriteZeroToClear, name: "NACKF")
                .WithFlag(5, out receiveInterrupt.FlagField, FieldMode.Read | FieldMode.WriteZeroToClear, name: "RDRF")
                .WithFlag(6, out transmitEndInterrupt.FlagField, FieldMode.Read | FieldMode.WriteZeroToClear, name: "TEND")
                .WithFlag(7, out transmitInterrupt.FlagField, FieldMode.Read | FieldMode.WriteZeroToClear, name: "TDRE")
                .WithReservedBits(8, 24)
                .WithChangeCallback((_, __) => UpdateInterrupts());

            for(var i = 0; i < SlaveAddressCount; i++)
            {
                (Registers.SlaveAddress0 + 0x4 * i).Define(this)
                    .WithTaggedFlag("SVA0", 0)
                    .WithTag("SVA[9:1]", 1, 9)
                    .WithReservedBits(10, 5)
                    .WithTaggedFlag("FSy", 15)
                    .WithReservedBits(16, 16);
            }

            Registers.BitRateLowLevel.Define(this, 0xFF)
                .WithTag("BRL[4:0]", 0, 5)
                .WithReservedBits(5, 3, 0x7)
                .WithReservedBits(8, 24);

            Registers.BitRateHighLevel.Define(this, 0xFF)
                .WithTag("BRH[4:0]", 0, 5)
                .WithReservedBits(5, 3, 0x7)
                .WithReservedBits(8, 24);
            
            Registers.TransmitData.Define(this)
                .WithValueField(0, 8, FieldMode.Write, name: "DRT[7:0]",
                    writeCallback: (_, value) =>
                    {
                        // TXI and TEI flags should be automatically cleared by a write to the ICDRT register
                        transmitInterrupt.Flag = false;
                        transmitEndInterrupt.Flag = false;
                        if(currentTransmissionState == TransmissionState.WriteAddress)
                        {
                            // In Renode I2C transmissions generally happen instantly. Since software may not be prepared for that
                            // we artificially delay the start of the transaction by delaying the write of the target peripheral address
                            // This is the best place for delays as software will always perform the address write regardless of how it
                            // starts the transmission (either via the Start or Restart conditions)
                            machine.ScheduleAction(addressWriteDelay, ___ =>
                            {
                                WriteData((byte)value);
                                UpdateInterrupts(); 
                            });
                        }
                        else
                        {
                            WriteData((byte)value);
                        }
                        UpdateInterrupts();
                    })
                .WithReservedBits(8, 24);

            Registers.ReceiveData.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "DRR[7:0]",
                    valueProviderCallback: _ =>
                    {
                        // RXI flag should be automatically cleared by reading the ICDRR register
                        receiveInterrupt.Flag = false;
                        var result = ReadData();
                        UpdateInterrupts();
                        return result;
                    })
                .WithReservedBits(8, 24);
        }

        private void WriteData(byte value)
        {
            switch(currentTransmissionState)
            {
                case TransmissionState.WriteAddress:
                {
                    if((value >> 3) == ExtendedAddressPrefix)
                    {
                        this.ErrorLog("10-bit addressing is currently not supported");
                        nackInterrupt.Flag = true;
                        return;
                    }

                    var isRead = BitHelper.IsBitSet(value, 0);
                    var address = value >> 1;
                    if(!TryGetByAddress(address, out selectedPeripheral))
                    {
                        nackInterrupt.Flag = true;
                        this.ErrorLog("Invalid slave peripheral address 0x{0:X}", address);
                        return;
                    }
                    this.DebugLog("Selected peripheral 0x{0:X} ({1})", address, selectedPeripheral.GetName());
                    currentTransmissionState = isRead ? TransmissionState.Read : TransmissionState.Write;
                    receiveInterrupt.Flag = isRead;
                    transmitInterrupt.Flag = !isRead;
                    transmitEndInterrupt.Flag = transmitInterrupt.Flag;
                    if(isRead)
                    {
                        // Software should discard the first read, as reading it is used for starting the transmission
                        // on real hardware. Add a dummy value to make sure that a value read from a sensor is not 
                        // accidentally discarded.
                        readQueue.Enqueue(0x0);
                    }
                    break;
                }
                case TransmissionState.Write:
                    writeQueue.Enqueue(value);
                    transmitInterrupt.Flag = true;
                    transmitEndInterrupt.Flag = true;
                    break;
                default:
                    this.ErrorLog("Transmission state: {0} is not valid when writing data (value 0x{1:X})", currentTransmissionState, value);
                    return;
            }
        }

        private byte ReadData()
        {
            switch(currentTransmissionState)
            {
                case TransmissionState.Read:
                {
                    if(readQueue.Count == 0)
                    {
                        if(selectedPeripheral == null)
                        {
                            this.WarningLog("Attempted to perform a peripheral read without selecting one");
                        }
                        else
                        {
                            // From IIC controller's perspective there is no way to determine
                            // how many bytes is software intending to read. Since some peripherals
                            // assume that the entire read operation will be performed using a single `Read` call
                            // we request more bytes, buffer them and return those values during subsequent reads
                            // The amount of bytes to buffer was chosen randomly and may be adjusted if required. 
                            var data = selectedPeripheral.Read(ReadByteCount);
                            this.DebugLog("Read {0} from peripheral", data.ToLazyHexString());
                            readQueue.EnqueueRange(data);
                        }
                    }

                    if(!readQueue.TryDequeue(out var result))
                    {
                        this.ErrorLog("Empty read buffer, returning 0x0");
                        nackInterrupt.Flag = true;
                        return 0x0;
                    }

                    // Issue Stop or Restart if a NACK was requested during the last read
                    if(finishReading)
                    {
                        finishReading = false;
                        // Perform restart if it was requested. Otherwise always stop
                        if(!restartCondition.TryPerform())
                        {
                            stopCondition.Perform();
                        }
                        return result;
                    }

                    finishReading = transmitNegativeAcknowledge.Value;
                    receiveInterrupt.Flag = true;
                    return result;
                }
                default:
                    this.ErrorLog("Transmission state: {0} is not valid when reading data", currentTransmissionState);
                    return 0x0;
            }
        }

        private void UpdateInterrupts()
        {
            var rxi = receiveInterrupt.InterruptState;
            var txi = transmitInterrupt.InterruptState;
            var tei = transmitEndInterrupt.InterruptState;
            var naki = nackInterrupt.InterruptState;
            var spi = stopInterrupt.InterruptState;
            var sti = startInterrupt.InterruptState;

            ReceiveIRQ.Set(rxi);
            TransmitIRQ.Set(txi);
            TransmitEndIRQ.Set(tei);
            NackIRQ.Set(naki);
            StopIRQ.Set(spi);
            StartIRQ.Set(sti);

            this.DebugLog("{0}: {1}, {2}: {3}, {4}: {5}, {6}: {7}, {8}: {9}, {10}: {11}",
                nameof(ReceiveIRQ), rxi,
                nameof(TransmitIRQ), txi,
                nameof(TransmitEndIRQ), tei,
                nameof(NackIRQ), naki,
                nameof(StopIRQ), spi,
                nameof(StartIRQ), sti
            );
        }

        private void HandleStartCondition()
        {
            currentTransmissionState = TransmissionState.WriteAddress;
            transmitInterrupt.Flag = true;
            startInterrupt.Flag = true;
            stopInterrupt.Flag = false;
        }

        private void HandleStopCondition()
        {
            stopInterrupt.Flag = true;
            startInterrupt.Flag = false;
            transmitNegativeAcknowledge.Value = false;
            transmitInterrupt.Flag = false;
            transmitEndInterrupt.Flag = false;
            finishReading = false;

            // Flush the transmit buffer to the peripheral
            if(selectedPeripheral != null && currentTransmissionState == TransmissionState.Write)
            {
                if(writeQueue.Count == 0)
                {
                    this.WarningLog("No data in the write buffer, aborting transmission");
                }
                else
                {
                    var content = writeQueue.ToArray();
                    writeQueue.Clear();

                    this.DebugLog("Writing {0} to peripheral", content.ToLazyHexString());
                    selectedPeripheral.Write(content);
                }
            }

            // Reset transmission state
            currentTransmissionState = TransmissionState.Idle;
            selectedPeripheral?.FinishTransmission();
            selectedPeripheral = null;
            readQueue.Clear();
            writeQueue.Clear();
        }

        private void HandleRestartCondition()
        {
            stopCondition.Perform(silent: true);
            startCondition.Perform(silent: true);
        }

        private void InternalReset()
        {
            status1Register.Reset();
            status2Register.Reset();
            control2Register.Reset();

            ReceiveIRQ.Unset();
            TransmitIRQ.Unset();
            TransmitEndIRQ.Unset();
            NackIRQ.Unset();
            StartIRQ.Unset();
            StopIRQ.Unset();

            currentTransmissionState = TransmissionState.Idle;
            writeQueue.Clear();
            readQueue.Clear();
            selectedPeripheral = null;
            finishReading = false;
        }

        private InterruptConfig receiveInterrupt;
        private InterruptConfig transmitInterrupt;
        private InterruptConfig transmitEndInterrupt;
        private InterruptConfig startInterrupt;
        private InterruptConfig stopInterrupt;
        private InterruptConfig nackInterrupt;

        private IFlagRegisterField transmitNegativeAcknowledge;
        private IFlagRegisterField transmitAcknowledgeWriteEnable;
        private IFlagRegisterField peripheralEnable;

        private DoubleWordRegister status1Register;
        private DoubleWordRegister status2Register;
        private DoubleWordRegister control2Register;

        private TransmissionState currentTransmissionState;
        private II2CPeripheral selectedPeripheral;
        private bool finishReading;

        private readonly Queue<byte> writeQueue;
        private readonly Queue<byte> readQueue;

        private readonly Condition startCondition;
        private readonly Condition stopCondition;
        private readonly Condition restartCondition;

        private static readonly TimeInterval addressWriteDelay = TimeInterval.FromMicroseconds(100);

        private const int SlaveAddressCount = 3;
        private const int ExtendedAddressPrefix = 0x1E;
        private const int ReadByteCount = 24;

        private struct InterruptConfig
        {
            public bool InterruptState => Enable.Value && Flag;
            public bool Flag
            {
                get => FlagField.Value;
                set => FlagField.Value = value;
            }

            public IFlagRegisterField Enable;
            public IFlagRegisterField FlagField;
        }

        private class Condition
        {
            public Condition(RenesasRZG_IIC parent, string name, Action handler)
            {
                this.parent = parent;
                this.name = name;
                this.handler = handler;
            }

            public bool TryPerform()
            {
                if(Requested)
                {
                    Perform();
                    return true;
                }
                return false;
            }

            public void Perform(bool silent = false)
            {
                if(!silent)
                {
                    parent.DebugLog("Handling {0} condition", name);
                }
                handler();
                RequestFlag.Value = false;
                parent.UpdateInterrupts();
            }

            public bool Requested
            {
                get => RequestFlag.Value;
                set => RequestFlag.Value = value;
            }

            public IFlagRegisterField RequestFlag;

            private readonly RenesasRZG_IIC parent;
            private readonly string name;
            private readonly Action handler;
        }

        private enum TransmissionState
        {
            Idle,
            WriteAddress,
            Write,
            Read,
        }

        private enum Registers : long
        {
            Control1            = 0x00, // ICCR1
            Control2            = 0x04, // ICCR2
            Mode1               = 0x08, // ICMR1
            Mode2               = 0x0C, // ICMR2
            Mode3               = 0x10, // ICMR3
            FunctionEnable      = 0x14, // ICFER
            StatusEnable        = 0x18, // ICSER
            InterruptEnable     = 0x1C, // ICIER
            Status1             = 0x20, // ICSR1
            Status2             = 0x24, // ICSR2
            SlaveAddress0       = 0x28, // SARL0
            SlaveAddress1       = 0x2C, // SARL1
            SlaveAddress2       = 0x30, // SARL2
            BitRateLowLevel     = 0x34, // ICBRL
            BitRateHighLevel    = 0x38, // ICBRH
            TransmitData        = 0x3C, // ICDRT
            ReceiveData         = 0x40, // ICDRR
        }
    }
}
