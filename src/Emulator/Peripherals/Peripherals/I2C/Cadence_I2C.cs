//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Helpers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class Cadence_I2C : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public Cadence_I2C(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            rxFifo = new Queue<byte>();
            txFifo = new Queue<byte>(FifoCapacity);
            txTransfer = new Queue<byte>();
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());

            txFifoOverflow = new CadenceInterruptFlag();
            rxFifoOverflow = new CadenceInterruptFlag();
            rxFifoUnderflow = new CadenceInterruptFlag();
            targetReady = new CadenceInterruptFlag();
            transferNotAcknowledged = new CadenceInterruptFlag();
            transferNewData = new CadenceInterruptFlag();
            transferCompleted = new CadenceInterruptFlag();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public long Size => 0x40;

        public GPIO IRQ { get; }

        public override void Reset()
        {
            registers.Reset();
            targetDevice = null;
            transferState = TransferState.Idle;

            ClearFifos();
            txTransfer.Clear();

            foreach(var flag in GetInterruptFlags())
            {
                flag.Reset();
            }
            UpdateInterrupts();
        }

        private void ClearFifos()
        {
            rxFifo.Clear();
            txFifo.Clear();
            // transferSize relates to the FIFOs counts, so it's also reseted
            transferSize.Value = 0;
        }

        private void UpdateInterrupts()
        {
            var newState = GetInterruptFlags().Any(x => x.InterruptStatus);
            if(IRQ.IsSet != newState)
            {
                this.Log(LogLevel.Debug, "Setting IRQ to {0}", newState);
                IRQ.Set(newState);
            }
        }

        private void EnqueueTx(byte data)
        {
            if(transferState != TransferState.Transmitting)
            {
                // Before transmission start data are collected in the txFifo
                if(txFifo.Count == FifoCapacity)
                {
                    this.Log(LogLevel.Warning, "Trying to write to a full Tx FIFO.");
                    txFifoOverflow.SetSticky(true);
                    return;
                }
                txFifo.Enqueue(data);
                transferSize.Value = (uint)txFifo.Count;
            }
            else
            {
                // When transmission is ongoing data are collected in the txTransfer for send to a peripheral at once
                txTransfer.Enqueue(data);
                transferSize.Value = 0;
                transferCompleted.SetSticky(true);
            }
        }

        private byte DequeueRx()
        {
            if(!rxFifo.TryDequeue(out var data))
            {
                this.Log(LogLevel.Warning, "Trying to read from an empty Rx FIFO");
                rxFifoUnderflow.SetSticky(true);
                return default(byte);
            }

            // RX interrupts are triggered at each FIFO reading to mimic a reception byte by byte
            SetRxSticky();
            transferSize.Value = (uint)rxFifo.Count;
            return data;
        }

        // TransferTriggered argument is set only by the transferAddress write callback which triggers transfer
        private bool TryChangeState(bool transferTriggered)
        {
            var oldState = transferState;
            ChangeState(transferTriggered);
            if(oldState != transferState)
            {
                OnStateChange(oldState, transferState);
                return true;
            }
            return false;
        }

        private void ChangeState(bool transferTriggered)
        {
            if(transferTriggered)
            {
                switch(transferDirection.Value)
                {
                    case TransferDirection.Transmit:
                        transferState = TransferState.Transmitting;
                        break;
                    case TransferDirection.Receive:
                        transferState = TransferState.Receiving;
                        break;
                    default:
                        throw new Exception($"Unsupported TransferSize enum member {transferDirection.Value}");
                }
            }
            else if(targetDevice == null || !transferHold.Value)
            {
                // There is second call of the TryChangeState in the transferAddress write callback to immediately return to the idle state in case of target non-appearence
                // Also transferHold flag clear causes return to the idle state
                transferState = TransferState.Idle;
            }
        }

        private void OnStateChange(TransferState oldState, TransferState state)
        {
            if(oldState == TransferState.Idle)
            {
                if(targetDevice == null)
                {
                    this.Log(LogLevel.Warning, "Can't find an I2C peripheral at address 0x{0:X}.", TransferAddress);
                    transferNotAcknowledged.SetSticky(true);
                }
                else if(targetMonitorEnabled.Value)
                {
                    targetReady.SetSticky(true);
                }
            }

            if(state == TransferState.Transmitting)
            {
                if(targetDevice != null)
                {
                    txTransfer.EnqueueRange(txFifo.DequeueAll());
                    transferCompleted.SetSticky(true);
                }
                else
                {
                    // Even in the case of a target non-appearance one byte is read from the Tx FIFO
                    // It's assumed that hardware sends at least one byte to receive NACK
                    txFifo.TryDequeue(out var result);
                }
                transferSize.Value = (uint)txFifo.Count;
            }

            if(oldState == TransferState.Transmitting)
            {
                if(targetDevice != null)
                {
                    // Flushing at once all data collected during transmission to the target device
                    targetDevice.Write(txTransfer.DequeueAll());
                }
            }

            if(state == TransferState.Receiving)
            {
                if(rxFifo.Count > 0)
                {
                    this.Log(LogLevel.Warning, "Starting new read operation when Rx FIFO isn't empty. TransferSize value may be corrupted.");
                    // Receiving data without a FIFO clear isn't recommended and isn't covered in detail by the datasheet. 
                    // This peripheral reads all data at once from an I2C peripheral, so we need to drop the data in that case. 
                    // Keeping some data mimics the FIFO full of data from the previous transaction.
                    var readData = rxFifo.DequeueRange(FifoCapacity);
                    rxFifo.Clear();
                    rxFifo.EnqueueRange(readData);
                }

                if(targetDevice != null)
                {
                    // All data are read from the target device at once
                    var size = (int)transferSize.Value;
                    var data = targetDevice.Read(size);
                    if(data.Length < size)
                    {
                        this.Log(LogLevel.Warning, "The I2C peripheral returns less bytes ({0}) than expected ({1}).", data.Length, size);
                    }
                    else if(data.Length > size)
                    {
                        this.Log(LogLevel.Warning, "The I2C peripheral returns more bytes ({0}) than expected ({1}).", data.Length, size);
                    }

                    if(rxFifo.Count + size <= MaxTransferSize)
                    {
                        // When data from the previous transaction and transfer data fit into FIFO, we may just enqueue it.
                        rxFifo.EnqueueRange(data, size);
                        // In case of returning too little data from the I2C peripheral, some dummy data are enqueued.
                        for(var i = data.Length; i < size; i++)
                        {
                            rxFifo.Enqueue(default(byte));
                        }
                    }
                    else
                    {
                        this.Log(LogLevel.Error, "Due to not empty Rx FIFO some data read from the I2C target can't be queued.");
                        rxFifo.EnqueueRange(data, size - rxFifo.Count);
                    }
                }

                SetRxSticky();
            }

            if(state == TransferState.Idle)
            {
                if(targetDevice != null)
                {
                    targetDevice.FinishTransmission();
                }
            }
        }

        private void SetRxSticky()
        {
            if(rxFifo.Count == 0)
            {
                return;
            }

            transferNewData.SetSticky(true);
            // The I2C interface implementation in Renode operates on byte collections - it sends all the data to the device in one go and receives one response.
            // As a result it can receive more data that could fit in the controller's FIFO (`rxFifo.Count > FifoCapacity`). We need to handle it correctly in the model.
            if(rxFifo.Count <= FifoCapacity)
            {
                transferCompleted.SetSticky(true);
            }
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithReservedBits(16, 16)
                    .WithTag("divisorA", 14, 2)
                    .WithTag("divisorB", 8, 6)
                    .WithReservedBits(7, 1)
                    .WithFlag(6, FieldMode.Read | FieldMode.WriteOneToClear, name: "clearFifo",
                        writeCallback: (_, val) => { if(val) ClearFifos(); }
                    )
                    .WithFlag(5, out targetMonitorEnabled, name: "targetMonitorEnabled")
                    .WithFlag(4, out transferHold, name: "transferHold",
                        writeCallback: (_, __) => TryChangeState(transferTriggered: false))
                    .WithTaggedFlag("transferAcknowledge", 3)
                    .WithEnumField(2, 1, out addressingMode, name: "addressingMode")
                    .WithTaggedFlag("interfaceMode", 1)
                    .WithEnumField(0, 1, out transferDirection, name: "transferDirection",
                        changeCallback: (prevVal, val) =>
                        {
                            if(transferState != TransferState.Idle && transferSize.Value > 0)
                            {
                                this.Log(LogLevel.Warning, "Changing transfer direction when transfer isn't completed.");
                            }
                        }
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithReservedBits(9, 2)
                    .WithFlag(8, FieldMode.Read, name: "busActive",
                        valueProviderCallback: (_) => transferState != TransferState.Idle
                    )
                    .WithFlag(7, FieldMode.Read, name: "rxFifoOverflow",
                        // It's always false, there is no way to overflow the Rx FIFO
                        valueProviderCallback: (_) => false
                    )
                    .WithFlag(6, FieldMode.Read, name: "txFifoNotEmpty",
                        valueProviderCallback: (_) => txFifo.Count > 0
                    )
                    .WithFlag(5, FieldMode.Read, name: "rxFifoNotEmpty",
                        valueProviderCallback: (_) => rxFifo.Count > 0
                    )
                    .WithReservedBits(4, 1)
                    .WithTaggedFlag("targetModeTransferDirection", 3)
                    .WithReservedBits(0, 3)
                },
                {(long)Registers.TransferAddress, new DoubleWordRegister(this)
                    .WithReservedBits(10, 22)
                    .WithValueField(0, 10, out transferAddressReg, name: "transferAddress",
                        writeCallback: (prevVal, val) =>
                        {
                            if(transferState != TransferState.Idle && prevVal != val)
                            {
                                this.Log(LogLevel.Error, "Changing the transfer address during transmission.");
                            }

                            TryGetByAddress(TransferAddress, out targetDevice);
                            if(TryChangeState(transferTriggered: true))
                            {
                                //Last byte reception/transmission or lack of target may cause immediate change to the Idle state 
                                TryChangeState(transferTriggered: false);
                            }
                        }
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.TransferData, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithValueField(0, 8, name: "transferData",
                        writeCallback: (_, val) => EnqueueTx((byte)val),
                        valueProviderCallback: (_) => DequeueRx()
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptStatus, new DoubleWordRegister(this)
                    .WithReservedBits(10, 22)
                    .WithTaggedFlag("arbitrationLostStatus", 9)
                    .WithReservedBits(8, 1)
                    .WithFlag(7,
                        valueProviderCallback: (_) => rxFifoUnderflow.StickyStatus,
                        writeCallback: (_, val) => rxFifoUnderflow.ClearSticky(val),
                        name: "rxFifoUnderflowStatus"
                    )
                    .WithFlag(6,
                        valueProviderCallback: (_) => txFifoOverflow.StickyStatus,
                        writeCallback: (_, val) => txFifoOverflow.ClearSticky(val),
                        name: "txFifoOverflowStatus"
                    )
                    .WithFlag(5,
                        valueProviderCallback: (_) => rxFifoOverflow.StickyStatus,
                        writeCallback: (_, val) => rxFifoOverflow.ClearSticky(val),
                        name: "rxFifoOverflowStatus"
                    )
                    .WithFlag(4,
                        valueProviderCallback: (_) => targetReady.StickyStatus,
                        writeCallback: (_, val) => targetReady.ClearSticky(val),
                        name: "targetReadyStatus"
                    )
                    .WithTaggedFlag("timeoutStatus", 3)
                    .WithFlag(2,
                        valueProviderCallback: (_) => transferNotAcknowledged.StickyStatus,
                        writeCallback: (_, val) => transferNotAcknowledged.ClearSticky(val),
                        name: "transferNotAcknowledgedStatus"
                    )
                    .WithFlag(1,
                        valueProviderCallback: (_) => transferNewData.StickyStatus,
                        writeCallback: (_, val) => transferNewData.ClearSticky(val),
                        name: "transferNewDataStatus"
                    )
                    .WithFlag(0,
                        valueProviderCallback: (_) => transferCompleted.StickyStatus,
                        writeCallback: (_, val) => transferCompleted.ClearSticky(val),
                        name: "transferCompletedStatus"
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.TransferSize, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithValueField(0, 8, out transferSize,
                        changeCallback: (_, __) =>
                        {
                            if(transferDirection.Value == TransferDirection.Transmit)
                            {
                                this.Log(LogLevel.Warning, "Changing transfer size while the transfer direction is set to transmit.");
                            }
                        })
                },
                {(long)Registers.InterruptMask, new DoubleWordRegister(this)
                    .WithReservedBits(10, 22)
                    .WithTaggedFlag("arbitrationLostMask", 9)
                    .WithReservedBits(8, 1)
                    .WithFlag(7, FieldMode.Read,
                        valueProviderCallback: (_) => !rxFifoUnderflow.InterruptMask,
                        name: "rxFifoUnderflowMask"
                    )
                    .WithFlag(6, FieldMode.Read,
                        valueProviderCallback: (_) => !txFifoOverflow.InterruptMask,
                        name: "txFifoOverflowMask"
                    )
                    .WithFlag(5, FieldMode.Read,
                        valueProviderCallback: (_) => !rxFifoOverflow.InterruptMask,
                        name: "rxFifoOverflowMask"
                    )
                    .WithFlag(4, FieldMode.Read,
                        valueProviderCallback: (_) => !targetReady.InterruptMask,
                        name: "targetReadyMask"
                    )
                    .WithTaggedFlag("timeoutMask", 3)
                    .WithFlag(2, FieldMode.Read,
                        valueProviderCallback: (_) => !transferNotAcknowledged.InterruptMask,
                        name: "transferNotAcknowledgedMask"
                    )
                    .WithFlag(1, FieldMode.Read,
                        valueProviderCallback: (_) => !transferNewData.InterruptMask,
                        name: "transferNewDataMask"
                    )
                    .WithFlag(0, FieldMode.Read,
                        valueProviderCallback: (_) => !transferCompleted.InterruptMask,
                        name: "transferCompletedMask"
                    )
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithReservedBits(10, 22)
                    .WithTaggedFlag("arbitrationLostInterruptEnable", 9)
                    .WithReservedBits(8, 1)
                    .WithFlag(7, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoUnderflow.InterruptEnable(val),
                        name: "rxFifoUnderflowInterruptEnable"
                    )
                    .WithFlag(6, FieldMode.Write,
                        writeCallback: (_, val) => txFifoOverflow.InterruptEnable(val),
                        name: "txFifoOverflowInterruptEnable"
                    )
                    .WithFlag(5, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoOverflow.InterruptEnable(val),
                        name: "rxFifoOverflowInterruptEnable"
                    )
                    .WithFlag(4, FieldMode.Write,
                        writeCallback: (_, val) => targetReady.InterruptEnable(val),
                        name: "targetReadyInterruptEnable"
                    )
                    .WithTaggedFlag("timeoutInterruptEnable", 3)
                    .WithFlag(2, FieldMode.Write,
                        writeCallback: (_, val) => transferNotAcknowledged.InterruptEnable(val),
                        name: "transferNotAcknowledgedInterruptEnable"
                    )
                    .WithFlag(1, FieldMode.Write,
                        writeCallback: (_, val) => transferNewData.InterruptEnable(val),
                        name: "transferNewDataInterruptEnable"
                    )
                    .WithFlag(0, FieldMode.Write,
                        writeCallback: (_, val) => transferCompleted.InterruptEnable(val),
                        name: "transferCompletedInterruptEnable"
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptDisable, new DoubleWordRegister(this)
                    .WithReservedBits(10, 22)
                    .WithTaggedFlag("arbitrationLostInterruptDisable", 9)
                    .WithReservedBits(8, 1)
                    .WithFlag(7, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoUnderflow.InterruptDisable(val),
                        name: "rxFifoUnderflowInterruptDisable"
                    )
                    .WithFlag(6, FieldMode.Write,
                        writeCallback: (_, val) => txFifoOverflow.InterruptDisable(val),
                        name: "txFifoOverflowInterruptDisable"
                    )
                    .WithFlag(5, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoOverflow.InterruptDisable(val),
                        name: "rxFifoOverflowInterruptDisable"
                    )
                    .WithFlag(4, FieldMode.Write,
                        writeCallback: (_, val) => targetReady.InterruptDisable(val),
                        name: "targetReadyInterruptDisable"
                    )
                    .WithTaggedFlag("timeoutInterruptDisable", 3)
                    .WithFlag(2, FieldMode.Write,
                        writeCallback: (_, val) => transferNotAcknowledged.InterruptDisable(val),
                        name: "transferNotAcknowledgedInterruptDisable"
                    )
                    .WithFlag(1, FieldMode.Write,
                        writeCallback: (_, val) => transferNewData.InterruptDisable(val),
                        name: "transferNewDataInterruptDisable"
                    )
                    .WithFlag(0, FieldMode.Write,
                        writeCallback: (_, val) => transferCompleted.InterruptDisable(val),
                        name: "transferCompletedInterruptDisable"
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
            };
        }

        private IEnumerable<CadenceInterruptFlag> GetInterruptFlags()
        {
            yield return txFifoOverflow;
            yield return rxFifoOverflow;
            yield return rxFifoUnderflow;
            yield return targetReady;
            yield return transferNotAcknowledged;
            yield return transferNewData;
            yield return transferCompleted;
        }

        private int TransferAddress
        {
            get
            {
                if(addressingMode.Value == AddressingMode.Extended)
                {
                    return (int)transferAddressReg.Value & ExtendedAddressMask;
                }
                return (int)transferAddressReg.Value & NormalAddressMask;
            }
        }

        private II2CPeripheral targetDevice;
        private TransferState transferState;

        private IFlagRegisterField targetMonitorEnabled;
        private IFlagRegisterField transferHold;
        private IEnumRegisterField<AddressingMode> addressingMode;
        private IEnumRegisterField<TransferDirection> transferDirection;
        private IValueRegisterField transferAddressReg;
        private IValueRegisterField transferSize;

        private readonly CadenceInterruptFlag txFifoOverflow;
        private readonly CadenceInterruptFlag rxFifoOverflow;
        private readonly CadenceInterruptFlag rxFifoUnderflow;
        private readonly CadenceInterruptFlag targetReady;
        private readonly CadenceInterruptFlag transferNotAcknowledged;
        private readonly CadenceInterruptFlag transferNewData;
        private readonly CadenceInterruptFlag transferCompleted;

        private readonly Queue<byte> txFifo;
        private readonly Queue<byte> txTransfer;
        private readonly Queue<byte> rxFifo;
        private readonly DoubleWordRegisterCollection registers;

        private const int FifoCapacity = 16;
        private const int MaxTransferSize = 255;
        private const int ExtendedAddressMask = 0x3FF;
        private const int NormalAddressMask = 0x7F;

        private enum TransferState
        {
            Idle,
            Transmitting,
            Receiving
        }

        private enum AddressingMode
        {
            Extended = 0x0,
            Normal = 0x1
        }

        private enum TransferDirection
        {
            Transmit = 0x0,
            Receive = 0x1
        }

        private enum Registers : long
        {
            Control = 0x00,
            Status = 0x04,
            TransferAddress = 0x08,
            TransferData = 0x0c,
            InterruptStatus = 0x10,
            TransferSize = 0x14,
            TargetMonitor = 0x18,
            Timeout = 0x1c,
            InterruptMask = 0x20,
            InterruptEnable = 0x24,
            InterruptDisable = 0x28,
            GlitchFilter = 0x2c
        }
    }
}
