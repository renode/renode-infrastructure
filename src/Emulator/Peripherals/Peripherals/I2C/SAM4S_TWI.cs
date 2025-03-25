//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.DMA;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class SAM4S_TWI : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IKnownSize,
        IProvidesRegisterCollection<DoubleWordRegisterCollection>, ISamPdcBytePeripheral, ISamPdcBlockBytePeripheral
    {
        public SAM4S_TWI(Machine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            pdc = new SAM_PDC(machine, this, (long)Registers.PdcReceivePointer, HandlePdcStatusChange);
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public byte? DmaByteRead() => ReadByteFromBuffer();
        public void DmaByteWrite(byte data) => WriteByteToBuffer(data);

        public byte[] DmaBlockByteRead(int count) => ReadBuffer(count);

        public void DmaBlockByteWrite(byte[] data)
        {
            if(state == State.WriteFinished)
            {
                state = State.Write;
            }
            WriteBuffer(data);
        }

        public override void Reset()
        {
            state = State.Idle;
            delayRxReady = false;
            rxBuffer.Clear();
            txBuffer.Clear();
            RegistersCollection.Reset();
            selectedPeripheral = FindPeripheral();
            pdc.Reset();
            UpdateInterrupts();
        }

        public GPIO IRQ { get; } = new GPIO();

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x128;

        public TransferType DmaReadAccessWidth => TransferType.Byte;
        public TransferType DmaWriteAccessWidth => TransferType.Byte;

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, out var start, FieldMode.Write, name: "START",
                    changeCallback: (_, value) => { if(value) SendInternalAddress(); })
                .WithFlag(1, out var stop, FieldMode.Write, name: "STOP")
                .WithFlag(2, out masterEnabled, FieldMode.Set, name: "MSEN - Master Mode Enabled")
                .WithFlag(3, FieldMode.WriteOneToClear, name: "MSDIS - Master Mode Disabled",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            this.Log(LogLevel.Error, "Tried to disable master mode! Only master mode is supported");
                        }
                    })
                .WithFlag(4, FieldMode.Set, name: "SVEN - Slave Mode Enabled",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            this.Log(LogLevel.Error, "Tried to enable slave mode! Only master mode is supported");
                        }
                    })
                .WithFlag(5, FieldMode.WriteOneToClear, name: "SVDIS - Slave Mode Disabled")
                .WithReservedBits(6, 1)
                .WithFlag(7, FieldMode.Set, writeCallback: (_, value) => { if(value) Reset(); }, name: "SWRST")
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, __) => TriggerAction(start.Value, stop.Value))
            ;

            Registers.MasterMode.Define(this)
                .WithReservedBits(0, 8)
                .WithEnumField(8, 2, out internalAddressSize, name: "IADRSZ - Internal Device Address Size")
                .WithReservedBits(10, 2)
                .WithEnumField(12, 1, out readDirection, name: "MREAD - Master Read Direction")
                .WithReservedBits(13, 3)
                .WithValueField(16, 7, out deviceAddress, name: "DADR - Device Address")
                .WithReservedBits(23, 9)
                .WithChangeCallback((_, __) =>
                {
                    selectedPeripheral = FindPeripheral();
                    if(selectedPeripheral == null)
                    {
                        this.WarningLog("Selected SPI peripheral is not registered");
                    }
                    switch(readDirection.Value)
                    {
                    case ReadDirection.MasterWrite:
                        state = State.Write;
                        break;
                    case ReadDirection.MasterRead:
                        state = State.Idle;
                        break;
                    default:
                        throw new Exception("Unreachable");
                    }
                })
            ;

            Registers.SlaveMode.Define(this)
                .WithReservedBits(0, 16)
                .WithTag("SADR - Slave Address", 16, 7)
                .WithReservedBits(23, 9)
            ;

            Registers.InternalAddress.Define(this)
                .WithValueField(0, 24, out internalAddress, name: "IADR - Internal Address")
                .WithReservedBits(24, 8)
            ;

            Registers.ClockWaveformGenerator.Define(this)
                .WithTag("CLDIV - Clock Low Divider", 0, 8)
                .WithTag("CHDIV - Clock High Divider", 8, 8)
                .WithTag("CKDIV - Clock Divide", 16, 3)
                .WithReservedBits(19, 13)
            ;

            Registers.Status.Define(this)
                .WithFlag(0, out txCompleted, FieldMode.Read, name: "TXCOMP")
                .WithFlag(1, FieldMode.Read, name: "RXDRY",
                    valueProviderCallback: _ =>
                    {
                        // NOTE: This is a simple delay mechanism for data receive with PDC
                        //       it assumes that Status register is read in interrupt handler
                        var value = RxReady && !delayRxReady;
                        delayRxReady = false;
                        return value;
                    })
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => masterEnabled.Value, name: "TXRDY")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "SVREAD")
                .WithTaggedFlag("SVACC", 4)
                .WithTaggedFlag("GACC", 5)
                .WithTaggedFlag("OVRE", 6)
                .WithReservedBits(7, 1)
                .WithFlag(8, out notAcknowledged, FieldMode.Read, name: "NACK")
                .WithTaggedFlag("ARBLST", 9)
                .WithTaggedFlag("SCLWS", 10)
                .WithTaggedFlag("EOSACC", 11)
                .WithFlag(12, FieldMode.Read, valueProviderCallback: _ => EndOfRxBuffer, name: "ENDRX")
                .WithFlag(13, FieldMode.Read, valueProviderCallback: _ => EndOfTxBuffer, name: "ENDTX")
                .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => RxBufferFull, name: "RXBUFF")
                .WithFlag(15, FieldMode.Read, valueProviderCallback: _ => TxBufferEmpty, name: "TXBUFE")
                .WithReservedBits(16, 16)
            ;

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if(value) txCompletedInterruptEnable.Value = true; }, name: "TXCOMP")
                .WithFlag(1, FieldMode.Set, writeCallback: (_, value) => { if(value) rxDataReadyInterruptEnable.Value = true; }, name: "RXRDY")
                .WithFlag(2, FieldMode.Set, writeCallback: (_, value) => { if(value) txDataReadyInterruptEnable.Value = true; }, name: "TXRDY")
                .WithReservedBits(3, 1)
                .WithFlag(4, FieldMode.Set, writeCallback: (_, value) => { if(value) slaveAccessInterruptEnable.Value = true; }, name: "SVACC")
                .WithFlag(5, FieldMode.Set, writeCallback: (_, value) => { if(value) generalCallAccessInterruptEnable.Value = true; }, name: "GACC")
                .WithFlag(6, FieldMode.Set, writeCallback: (_, value) => { if(value) overrunErrorInterruptEnable.Value = true; }, name: "OVRE")
                .WithReservedBits(7, 1)
                .WithFlag(8, FieldMode.Set, writeCallback: (_, value) => { if(value) notAcknowledgeInterruptEnable.Value = true; }, name: "NACK")
                .WithFlag(9, FieldMode.Set, writeCallback: (_, value) => { if(value) arbitrationLostInterruptEnable.Value = true; }, name: "ARBLST")
                .WithFlag(10, FieldMode.Set, writeCallback: (_, value) => { if(value) clockWaitStateInterruptEnable.Value = true; }, name: "SCL_WS")
                .WithFlag(11, FieldMode.Set, writeCallback: (_, value) => { if(value) endOfSlaveAccessInterruptEnable.Value = true; }, name: "EOSACC")
                .WithFlag(12, FieldMode.Set, writeCallback: (_, value) => { if(value) endOfRxBufferInterruptEnable.Value = true; }, name: "ENDRX")
                .WithFlag(13, FieldMode.Set, writeCallback: (_, value) => { if(value) endOfTxBufferInterruptEnable.Value = true; }, name: "ENDTX")
                .WithFlag(14, FieldMode.Set, writeCallback: (_, value) => { if(value) rxBufferFullInterruptEnable.Value = true; }, name: "RXBUFF")
                .WithFlag(15, FieldMode.Set, writeCallback: (_, value) => { if(value) txBufferEmptyInterruptEnable.Value = true; }, name: "TXBUFE")
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptDisable.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) txCompletedInterruptEnable.Value = false; }, name: "TXCOMP")
                .WithFlag(1, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) rxDataReadyInterruptEnable.Value = false; }, name: "RXRDY")
                .WithFlag(2, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) txDataReadyInterruptEnable.Value = false; }, name: "TXRDY")
                .WithReservedBits(3, 1)
                .WithFlag(4, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) slaveAccessInterruptEnable.Value = false; }, name: "SVACC")
                .WithFlag(5, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) generalCallAccessInterruptEnable.Value = false; }, name: "GACC")
                .WithFlag(6, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) overrunErrorInterruptEnable.Value = false; }, name: "OVRE")
                .WithReservedBits(7, 1)
                .WithFlag(8, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) notAcknowledgeInterruptEnable.Value = false; }, name: "NACK")
                .WithFlag(9, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) arbitrationLostInterruptEnable.Value = false; }, name: "ARBLST")
                .WithFlag(10, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) clockWaitStateInterruptEnable.Value = false; }, name: "SCL_WS")
                .WithFlag(11, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) endOfSlaveAccessInterruptEnable.Value = false; }, name: "EOSACC")
                .WithFlag(12, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) endOfRxBufferInterruptEnable.Value = false; }, name: "ENDRX")
                .WithFlag(13, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) endOfTxBufferInterruptEnable.Value = false; }, name: "ENDTX")
                .WithFlag(14, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) rxBufferFullInterruptEnable.Value = false; }, name: "RXBUFF")
                .WithFlag(15, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) txBufferEmptyInterruptEnable.Value = false; }, name: "TXBUFE")
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptMask.Define(this)
                .WithFlag(0, out txCompletedInterruptEnable, FieldMode.Read, name: "TXCOMP")
                .WithFlag(1, out rxDataReadyInterruptEnable, FieldMode.Read, name: "RXRDY")
                .WithFlag(2, out txDataReadyInterruptEnable, FieldMode.Read, name: "TXRDY")
                .WithReservedBits(3, 1)
                .WithFlag(4, out slaveAccessInterruptEnable, FieldMode.Read, name: "SVACC")
                .WithFlag(5, out generalCallAccessInterruptEnable, FieldMode.Read, name: "GACC")
                .WithFlag(6, out overrunErrorInterruptEnable, FieldMode.Read, name: "OVRE")
                .WithReservedBits(7, 1)
                .WithFlag(8, out notAcknowledgeInterruptEnable, FieldMode.Read, name: "NACK")
                .WithFlag(9, out arbitrationLostInterruptEnable, FieldMode.Read, name: "ARBLST")
                .WithFlag(10, out clockWaitStateInterruptEnable, FieldMode.Read, name: "SCL_WS")
                .WithFlag(11, out endOfSlaveAccessInterruptEnable, FieldMode.Read, name: "EOSACC")
                .WithFlag(12, out endOfRxBufferInterruptEnable, FieldMode.Read, name: "ENDRX")
                .WithFlag(13, out endOfTxBufferInterruptEnable, FieldMode.Read, name: "ENDTX")
                .WithFlag(14, out rxBufferFullInterruptEnable, FieldMode.Read, name: "RXBUFF")
                .WithFlag(15, out txBufferEmptyInterruptEnable, FieldMode.Read, name: "TXBUFE")
            ;

            Registers.ReceiveHolding.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "RXDATA",
                    valueProviderCallback: _ =>
                    {
                        var data = ReadByteFromBuffer(dmaAccess: false) ?? 0x0;

                        switch(state)
                        {
                        case State.ReadLastButOne:
                            state = State.ReadLast;
                            break;
                        case State.ReadLast:
                            state = State.ReadFinished;
                            rxBuffer.Clear();
                            selectedPeripheral?.FinishTransmission();
                            break;
                        }
                        return data;
                    })
                .WithReservedBits(8, 24)
                .WithReadCallback((_, __) => UpdateInterrupts())
            ;

            Registers.TransmitHolding.Define(this)
                .WithValueField(0, 8, FieldMode.Write, name: "TXDATA",
                    writeCallback: (_, value) =>
                    {
                        WriteByteToBuffer((byte)value, dmaAccess: false);
                        if(state == State.WriteLast)
                        {
                            state = State.WriteFinished;
                            FinalizeWrite();
                            txCompleted.Value = true;
                            selectedPeripheral?.FinishTransmission();
                        }
                        UpdateInterrupts();
                    })
                .WithReservedBits(8, 24)
            ;
        }

        /// <summary>
        /// Find peripheral from bus based on <see cref="deviceAddress"/> and <see cref="internalAddress"/>
        /// </summary>
        /// <returns><see cref="II2CPeripheral"/> when found or null when not found</returns>
        private II2CPeripheral FindPeripheral()
        {
            slaveAddress = 0;

            // 10-bit addressing
            if(BitHelper.GetValue(deviceAddress.Value, offset: 2, size: 5) == _10BitAddressingPrefix)
            {
                if(internalAddressSize.Value == InternalAddressSize.None)
                {
                    this.ErrorLog("Decoding 10-bit target address from data is not implemented");
                }
                slaveAddress.ReplaceBits(deviceAddress.Value, width: 2, destinationPosition: 7);
                slaveAddress.ReplaceBits(internalAddress.Value, width: 8, destinationPosition: 0);
            }
            else
            {
                slaveAddress = BitHelper.GetValue(deviceAddress.Value, offset: 0, size: 7);
            }

            this.NoisyLog("Selected slave 0x{0:X}", slaveAddress);
            return ChildCollection.GetOrDefault((int)slaveAddress);
        }

        /// <summary>
        /// Convert <see cref="internalAddress"/> to byte array
        /// </summary>
        /// <returns>
        /// Byte array which length equals <see cref="internalAddressSize"/> or null if it's either 0 or > 3
        /// </returns>
        private byte[] GetInternalAddressBytes()
        {
            if(internalAddressSize.Value == InternalAddressSize.None)
            {
                return null;
            }
            return BitHelper.GetBytesFromValue(internalAddress.Value, (int)internalAddressSize.Value);
        }

        private byte? ReadByteFromBuffer(bool dmaAccess = true)
        {
            return ReadBuffer(1, dmaAccess)?[0];
        }

        private byte[] ReadBuffer(int count, bool dmaAccess = true)
        {
            FinalizeWrite();

            if(dmaAccess && state != State.Read)
            {
                return null;
            }

            if(!ReadMode)
            {
                this.WarningLog("Attempted to perform read, but it's not enabled");
                return null;
            }

            EnsureRxDataReady(count);
            var data = rxBuffer.DequeueRange(count);
            this.NoisyLog("Reading from slave 0x{0:X}: {1}", slaveAddress, data.ToLazyHexString());
            return data;
        }

        private void WriteByteToBuffer(byte value, bool dmaAccess = true)
        {
            WriteBuffer(new byte[] { value }, dmaAccess);
        }

        private void WriteBuffer(byte[] data, bool dmaAccess = true)
        {
            if(!WriteMode)
            {
                this.WarningLog("Attempted to perform write{0}, but it's not enabled", dmaAccess ? " via dma" : "");
                return;
            }

            if(selectedPeripheral == null)
            {
                notAcknowledged.Value = true;
                return;
            }

            if(dmaAccess && txBuffer.Count == 0)
            {
                // This is a first write initiated by PDC,
                // so the internal address is sent as it's start of a new frame
                SendInternalAddress();
            }

            txBuffer.EnqueueRange(data);
        }

        private void FinalizeWrite()
        {
            if(txBuffer.Count == 0)
            {
                return;
            }
            var data = txBuffer.ToArray();
            this.NoisyLog("Writing to slave 0x{0:X}: {1}", slaveAddress, data.ToLazyHexString());
            selectedPeripheral.Write(data);
            txBuffer.Clear();
        }

        private void EnsureRxDataReady(int count)
        {
            if(rxBuffer.Count >= count)
            {
                return;
            }

            IEnumerable<byte> data;
            if(selectedPeripheral == null)
            {
                this.DebugLog("SPI peripheral not connected, reading zeros");
                data = Enumerable.Repeat<byte>(0x0, count);
            }
            else
            {
                data = selectedPeripheral.Read(count);
            }
            rxBuffer.EnqueueRange(data);
        }

        private void SendInternalAddress()
        {
            if(selectedPeripheral == null)
            {
                notAcknowledged.Value = true;
                return;
            }

            var addressBytes = GetInternalAddressBytes();
            if(addressBytes != null)
            {
                this.NoisyLog("Writing to slave 0x{0:X}: {1}", slaveAddress, addressBytes.ToLazyHexString());
                selectedPeripheral.Write(addressBytes);
            }
        }

        private void HandlePdcStatusChange()
        {
            delayRxReady = EndOfRxBuffer || RxBufferFull;
            UpdateInterrupts();
        }

        private void TriggerAction(bool start, bool stop)
        {
            this.NoisyLog("TWI_CR.START={0}, TWI_CR.STOP={1}, TWI_MMR.MREAD={2}", start, stop, readDirection.Value);
            if(!start && !stop)
            {
                return;
            }

            if(start)
            {
                notAcknowledged.Value = false;
                txCompleted.Value = false;

                if(selectedPeripheral == null)
                {
                    this.NoisyLog("No action performed, SPI peripheral not found");
                    notAcknowledged.Value = true;
                    UpdateInterrupts();
                    return;
                }
            }

            switch(readDirection.Value)
            {
            case ReadDirection.MasterWrite:
                state = stop ? State.WriteLast : State.Write;
                break;
            case ReadDirection.MasterRead:
                if(start && stop)
                {
                    state = State.ReadLast;
                }
                else if(start)
                {
                    state = State.Read;
                    pdc.TriggerReceiver();
                }
                else
                {
                    state = State.ReadLastButOne;
                }
                break;
            default:
                throw new Exception("Unreachable");
            }
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var state = (txCompletedInterruptEnable.Value && txCompleted.Value)
                || (rxDataReadyInterruptEnable.Value && RxReady && !delayRxReady)
                || (txDataReadyInterruptEnable.Value && masterEnabled.Value)
                || (notAcknowledgeInterruptEnable.Value && notAcknowledged.Value)
                || (endOfRxBufferInterruptEnable.Value && EndOfRxBuffer)
                || (endOfTxBufferInterruptEnable.Value && EndOfTxBuffer)
                || (rxBufferFullInterruptEnable.Value && RxBufferFull)
                || (txBufferEmptyInterruptEnable.Value && TxBufferEmpty)
            ;

            this.DebugLog("IRQ {0}", state ? "set" : "unset");
            IRQ.Set(state);
        }

        private bool EndOfRxBuffer => pdc?.EndOfRxBuffer ?? false;
        private bool EndOfTxBuffer => pdc?.EndOfTxBuffer ?? false;
        private bool RxBufferFull => pdc?.RxBufferFull ?? false;
        private bool TxBufferEmpty => pdc?.TxBufferEmpty ?? false;

        private bool RxReady
        {
            get
            {
                switch(state)
                {
                case State.Read:
                case State.ReadLastButOne:
                case State.ReadLast:
                    return true;
                default:
                    return false;
                }
            }
        }

        private bool ReadMode => RxReady;

        private bool WriteMode
        {
            get
            {
                switch(state)
                {
                case State.Write:
                case State.WriteLast:
                    return true;
                default:
                    return false;
                }
            }
        }

        private State state;
        private bool delayRxReady;
        private ulong slaveAddress;
        private IFlagRegisterField masterEnabled;
        private IEnumRegisterField<InternalAddressSize> internalAddressSize;
        private IEnumRegisterField<ReadDirection> readDirection;
        private IValueRegisterField deviceAddress;
        private IValueRegisterField internalAddress;
        private IFlagRegisterField txCompleted;
        private IFlagRegisterField notAcknowledged;
        private IFlagRegisterField txBufferEmptyInterruptEnable;
        private IFlagRegisterField rxBufferFullInterruptEnable;
        private IFlagRegisterField endOfTxBufferInterruptEnable;
        private IFlagRegisterField endOfRxBufferInterruptEnable;
        private IFlagRegisterField endOfSlaveAccessInterruptEnable;
        private IFlagRegisterField clockWaitStateInterruptEnable;
        private IFlagRegisterField arbitrationLostInterruptEnable;
        private IFlagRegisterField notAcknowledgeInterruptEnable;
        private IFlagRegisterField overrunErrorInterruptEnable;
        private IFlagRegisterField generalCallAccessInterruptEnable;
        private IFlagRegisterField slaveAccessInterruptEnable;
        private IFlagRegisterField txDataReadyInterruptEnable;
        private IFlagRegisterField rxDataReadyInterruptEnable;
        private IFlagRegisterField txCompletedInterruptEnable;

        private II2CPeripheral selectedPeripheral;

        private readonly Queue<byte> rxBuffer = new Queue<byte>();
        private readonly Queue<byte> txBuffer = new Queue<byte>();
        private readonly SAM_PDC pdc;

        private const ulong _10BitAddressingPrefix = 0x1e;

        private enum State
        {
            Idle,
            Read,
            ReadLastButOne,
            ReadLast,
            ReadFinished,
            Write,
            WriteLast,
            WriteFinished,
        }

        public enum InternalAddressSize
        {
            None = 0,
            OneByte,
            TwoByte,
            ThreeByte
        }

        public enum ReadDirection
        {
            MasterWrite = 0x00,
            MasterRead = 0x01
        }

        public enum Registers : uint
        {
            Control = 0x00,
            MasterMode = 0x04,
            SlaveMode = 0x08,
            InternalAddress = 0x0C,
            ClockWaveformGenerator = 0x10,
            Reserved1 = 0x14,
            Reserved2 = 0x1C,
            Status = 0x20,
            InterruptEnable = 0x24,
            InterruptDisable = 0x28,
            InterruptMask = 0x2C,
            ReceiveHolding = 0x30,
            TransmitHolding = 0x34,
            PdcReceivePointer = 0x100,
            PdcReceiveCounter = 0x104,
            PdcTransmitPointer = 0x108,
            PdcTransmitCounter = 0x10C,
            PdcReceiveNextPointer = 0x110,
            PdcReceiveNextCounter = 0x114,
            PdcTransmitNextPointer = 0x118,
            PdcTransmitNextCounter = 0x11C,
            PdcTransferControl = 0x120,
            PdcTransferStatus = 0x124,
        }
    }
}
