//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public sealed class STM32H7_QuadSPI : NullRegistrationPointPeripheralContainer<GenericSpiFlash>, IKnownSize,
        IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral
    {
        // NOTE: Current implementation does not support DMA interface
        public STM32H7_QuadSPI(IMachine machine) : base(machine)
        {
            registers = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public override void Reset()
        {
            lock(locker)
            {
                pollingTokenSource.Cancel();
                pollingTokenSource = new CancellationTokenSource();

                registers.Reset();
                ClearTransferFifo();

                skipInstruction = true;
                skipAddress = true;
                skipAlternateBytes = true;
                skipData = true;

                IRQ.Unset();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            if(!CheckDataRegisterOffset(offset))
            {
                return 0;
            }
            return (ushort)ReadFromDataRegister(16);
        }

        public void WriteWord(long offset, ushort value)
        {
            if(!CheckDataRegisterOffset(offset))
            {
                return;
            }
            WriteToDataRegister(value, 16);
        }

        public byte ReadByte(long offset)
        {
            if(!CheckDataRegisterOffset(offset))
            {
                return 0;
            }
            return (byte)ReadFromDataRegister(8);
        }

        public void WriteByte(long offset, byte value)
        {
            if(!CheckDataRegisterOffset(offset))
            {
                return;
            }
            WriteToDataRegister(value, 8);
        }

        public long Size => 0x1000;
        public GPIO IRQ { get; } = new GPIO();

        // This value can be used to affect the interval between next polling events
        // since on real HW it depends on the clock speed that is configured for QSPI kernel
        public ulong PollingMultiplier { get; set; } = 1;

        private bool CheckDataRegisterOffset(long offset)
        {
            if(offset != (long)Registers.Data)
            {
                this.Log(LogLevel.Error, "This peripheral can only handle non 32-bit access at {0}. This operation has no effect", Registers.Data);
                return false;
            }
            return true;
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(registers)
                .WithFlag(0, out enabled, name: "Enable")
                .WithFlag(1,
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            lock(locker)
                            {
                                pollingTokenSource.Cancel();
                                pollingTokenSource = new CancellationTokenSource();
                                remainingBytesToTransfer = null;
                            }
                        }
                    },
                    name: "Abort")
                .WithReservedBits(2, 1)
                .WithTaggedFlag("Timeout counter enable", 3)
                .WithTaggedFlag("Sample shift", 4)
                .WithReservedBits(5, 1)
                .WithTaggedFlag("Dual-flash mode", 6)
                .WithTaggedFlag("Flash memory selection", 7)
                .WithValueField(8, 5, out fifoThreshold, name: "FIFO threshold level")
                .WithReservedBits(13, 3)
                .WithTaggedFlag("Transfer error interrupt enable", 16)
                .WithFlag(17, out transferCompleteIrqEnable, name: "Transfer complete interrupt enable")
                .WithFlag(18, out fifoThresholdInterruptEnable, name: "FIFO threshold interrupt enable")
                .WithFlag(19, out statusMatchInterruptEnable, name: "Status match interrupt enable")
                .WithTaggedFlag("Timeout interrupt enable", 20)
                .WithReservedBits(21, 1)
                .WithFlag(22, out pollingModeStopOnMatch, name: "Automatic status-polling mode stop")
                .WithEnumField(23, 1, out pollingMatchMode, name: "Polling match mode")
                .WithTag("Prescaler", 24, 8)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.DeviceConfiguration.Define(registers)
                .WithTaggedFlag("Clock mode (mode 0/mode 3)", 0)
                .WithReservedBits(1, 7)
                .WithTag("Chip select high time", 8, 3)
                .WithReservedBits(11, 5)
                .WithValueField(16, 5, out flashSize, name: "Flash memory size")
                .WithReservedBits(21, 11);

            Registers.Status.Define(registers)
                .WithFlag(0, FieldMode.Read, name: "Transmit error flag")
                .WithFlag(1, out transferComplete, FieldMode.Read, name: "Transfer complete flag")
                .WithFlag(2, out fifoThresholdReached, FieldMode.Read, name: "FIFO threshold flag")
                .WithFlag(3, out statusMatch, FieldMode.Read, name: "Status match flag")
                .WithFlag(4, FieldMode.Read, name: "Timeout flag")
                .WithFlag(5, FieldMode.Read, name: "Busy")
                .WithReservedBits(6, 2)
                .WithValueField(8, 6, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        switch(functionalMode.Value)
                        {
                            case ModeOfOperation.AutomaticStatusPolling:
                            case ModeOfOperation.MemoryMapped:
                                return 0;
                            case ModeOfOperation.IndirectRead:
                            case ModeOfOperation.IndirectWrite:
                                var count = transferFifo.Count;
                                return (count > MaximumFifoDepth) ? MaximumFifoDepth : (ulong)count;
                            default:
                                throw new InvalidOperationException($"Invalid mode: {functionalMode.Value}");
                        }
                    },
                    name: "FIFO level")
                .WithReservedBits(14, 18);

            Registers.FlagClear.Define(registers)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "Transmit error flag clear")
                .WithFlag(1, FieldMode.WriteOneToClear, writeCallback: (_, value) => transferComplete.Value = false, name: "Transfer complete flag clear")
                .WithReservedBits(2, 1)
                .WithFlag(3, FieldMode.WriteOneToClear, writeCallback: (_, value) => statusMatch.Value = false, name: "Status match flag clear")
                .WithFlag(4, FieldMode.WriteOneToClear, name: "Timeout flag clear")
                .WithReservedBits(5, 27)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.DataLength.Define(registers)
                .WithValueField(0, 32, out dataSize, name: "Data length");

            Registers.CommunicationConfiguration.Define(registers)
                .WithValueField(0, 8, out instruction, name: "Instruction")
                .WithValueField(8, 2, writeCallback: (_, value) => skipInstruction = value == 0, name: "Instruction mode")
                .WithValueField(10, 2, writeCallback: (_, value) => skipAddress = value == 0, name: "Address mode")
                .WithValueField(12, 2, out addressSize, name: "Address size")
                .WithValueField(14, 2, writeCallback: (_, value) => skipAlternateBytes = value == 0, name: "Alternate byte mode")
                .WithValueField(16, 2, out alternateBytesSize, name: "Alternate byte size")
                .WithTag("Number of dummy cycles", 18, 5)
                .WithReservedBits(23, 1)
                .WithValueField(24, 2, writeCallback: (_, value) => skipData = value == 0, name: "Data mode")
                .WithEnumField(26, 2, out functionalMode,
                    writeCallback: (oldValue, value) =>
                    {
                        // Can't use `changeCallback` here, since it will trigger after the write callback, that is hooked to triggering the transfer
                        // this could cause the queue to loose all data immediately after the transfer happens, if there was a mode change directly before
                        if(oldValue != value)
                        {
                            ClearTransferFifo();
                            pollingTokenSource.Cancel();
                            pollingTokenSource = new CancellationTokenSource();
                            UpdateInterrupts();
                        }
                    },
                    name: "Functional mode")
                .WithTaggedFlag("Send instruction only once", 28)
                .WithTaggedFlag("Free-running clock mode", 29)
                .WithTaggedFlag("DDR hold", 30)
                .WithTaggedFlag("DDR mode", 31)
                // This callback needs to be triggered last, after all other fields are populated and callbacks triggered
                .WithWriteCallback((_, __) => TriggerTransfer(TriggerTransferSource.Instruction));

            Registers.Address.Define(registers)
                .WithValueField(0, 32, out address, name: "Address")
                .WithWriteCallback(writeCallback: (_, __) => TriggerTransfer(TriggerTransferSource.Address));

            Registers.AlternateByte.Define(registers)
                .WithValueField(0, 32, out alternateBytes, name: "Alternate bytes");

            Registers.Data.Define(registers)
                .WithValueField(0, 32,
                    writeCallback: (_, value) =>
                    {
                        WriteToDataRegister(value, 32);
                    },
                    valueProviderCallback: _ =>
                    {
                        return ReadFromDataRegister(32);
                    },
                    name: "Data bytes");

            Registers.PollingStatusMask.Define(registers)
                .WithValueField(0, 32, out pollingMask, name: "Polling status mask");

            Registers.PollingStatusMatch.Define(registers)
                .WithValueField(0, 32, out pollingReferenceMatch, name: "Polling status match");

            Registers.PollingInterval.Define(registers)
                .WithValueField(0, 16, out pollingInterval, name: "Polling interval")
                .WithReservedBits(16, 16);
        }

        private void ClearTransferFifo()
        {
            transferFifo.Clear();
            remainingBytesToTransfer = null;
        }

        // For clarity, sizes are given in number of bits
        private void CheckDataRegisterAccessSize(int size)
        {
            if(size % 8 != 0 || size / 8 > 4)
            {
                throw new ArgumentException($"Size {size} is not properly constrained, cannot access data register");
            }
        }

        private void WriteToDataRegister(ulong val, int size)
        {
            CheckDataRegisterAccessSize(size);
            if(functionalMode.Value != ModeOfOperation.IndirectWrite)
            {
                this.Log(LogLevel.Error, "Data register should only be written in {0} mode. This operation has no effect", ModeOfOperation.IndirectWrite);
                return;
            }
            lock(locker)
            {
                transferFifo.EnqueueRange(BitHelper.GetBytesFromValue(val, size / 8));
                UpdateInterrupts();
                TriggerTransfer(TriggerTransferSource.DataWrite);
            }
        }

        private uint ReadFromDataRegister(int size)
        {
            CheckDataRegisterAccessSize(size);
            if(!(functionalMode.Value == ModeOfOperation.IndirectRead || functionalMode.Value == ModeOfOperation.AutomaticStatusPolling))
            {
                this.Log(LogLevel.Error, "Data register should not be read in mode: {0}. Returning 0", functionalMode.Value);
                return 0;
            }
            byte[] response = new byte[4];
            lock(locker)
            {
                for(int i = 0; i < size / 8; ++i)
                {
                    if(transferFifo.TryDequeue(out var val))
                    {
                        // Push the value back into the queue immediately in AutomaticStatusPolling mode, so it can be read from Data register again
                        // It's guaranteed in `ReceiveData` that the queue won't grow beyond 4-byte limit
                        if(functionalMode.Value == ModeOfOperation.AutomaticStatusPolling)
                        {
                            transferFifo.Enqueue(val);
                        }
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "Read from empty transfer FIFO, containing less data than 32 bits. Filling with zeroes");
                    }
                    response[i] = val;
                }
                if(functionalMode.Value == ModeOfOperation.AutomaticStatusPolling)
                {
                    // In case of polling, if less than the full size of the queue is requested, pop and push the data back, so it's not corrupted
                    for(int i = size / 8; i < 4; ++i)
                    {
                        if(transferFifo.TryDequeue(out var val))
                        {
                            transferFifo.Enqueue(val);
                        }
                    }
                }
                UpdateInterrupts();
                TriggerTransfer(TriggerTransferSource.DataRead);
            }
            return BitHelper.ToUInt32(response, 0, 4, true);
        }

        private void UpdateInterrupts()
        {
            bool thresholdReached = functionalMode.Value == ModeOfOperation.IndirectRead && (transferFifo.Count >= (int)fifoThreshold.Value);
            thresholdReached |= functionalMode.Value == ModeOfOperation.IndirectWrite && ((MaximumFifoDepth - transferFifo.Count) >= (int)fifoThreshold.Value);
            fifoThresholdReached.Value = thresholdReached;

            bool status = (transferComplete.Value && transferCompleteIrqEnable.Value)
                          || (statusMatch.Value && statusMatchInterruptEnable.Value)
                          || (fifoThresholdReached.Value && fifoThresholdInterruptEnable.Value);

            this.Log(LogLevel.Noisy, "IRQ set to: {0}", status);
            IRQ.Set(status);
        }

        private void SplitToBytesAndSend(IValueRegisterField value, int size, string name)
        {
            var bytes = BitHelper.GetBytesFromValue(value.Value, size);
            this.Log(LogLevel.Debug, "Sending {0}: {1}", name, Misc.PrettyPrintCollectionHex(bytes));
            foreach(var part in bytes)
            {
                RegisteredPeripheral.Transmit(part);
            }   
        }

        private void TriggerTransfer(TriggerTransferSource source)
        {
            if(!enabled.Value)
            {
                this.Log(LogLevel.Warning, "Trying to trigger SPI transfer, but the peripheral is disabled");
                return;
            }
            if(functionalMode.Value == ModeOfOperation.MemoryMapped)
            {
                this.Log(LogLevel.Debug, "The peripheral is in MemoryMapped mode, all transfers are ignored");
                return;
            }
            this.Log(LogLevel.Debug, "Trying to trigger SPI transfer, source: {0}, mode: {1}", source, functionalMode.Value);

            lock(locker)
            {
                // If more data is expected in the command sequence, delay the transfer
                // until the data is written into the relevant register
                if(!VerifyIfTransferShouldTrigger(source))
                {
                    return;
                }

                // If a transfer requires more data to complete, then skip immediately to the data phase
                if(remainingBytesToTransfer == null)
                {
                    if(!skipInstruction)
                    {
                        this.Log(LogLevel.Debug, "Sending command: 0x{0:X}", instruction.Value);
                        RegisteredPeripheral.Transmit((byte)instruction.Value);
                    }

                    if(!skipAddress)
                    {
                        SplitToBytesAndSend(address, (int)addressSize.Value + 1, "address");
                    }

                    if(!skipAlternateBytes)
                    {
                        SplitToBytesAndSend(alternateBytes, (int)alternateBytesSize.Value + 1, "alternate bytes");
                    }
                }

                if(!skipData)
                {
                    TransferData();
                    HandlePollingData();
                }
                else
                {
                    // Transfer is only complete here, if there was no data to send
                    // since this function is re-entrant otherwise
                    RegisteredPeripheral.FinishTransmission();
                    transferComplete.Value = true;
                }
                UpdateInterrupts();
            }
        }

        private bool VerifyIfTransferShouldTrigger(TriggerTransferSource source)
        {
            // Check if a write to this register (identified by `source`), should result in data transfer
            // or if the peripheral is still waiting for more data (e.g. it got an instruction but it's missing data)
            switch (source)
            {
                case TriggerTransferSource.Instruction:
                    if(skipInstruction && source == TriggerTransferSource.Instruction)
                    {
                        // If the source is Instruction register, but instruction stage is disabled, the transfer shouldn't trigger
                        this.Log(LogLevel.Noisy, "Not transferring, {0} is disabled", nameof(TriggerTransferSource.Instruction));
                        return false;
                    }
                    if(skipAddress && !skipData)
                    {
                        // Data without address - let the next stage determine if it's valid
                        goto case TriggerTransferSource.Address;
                    }
                    if(!skipAddress)
                    {
                        this.Log(LogLevel.Noisy, "Address is missing, not transferring");
                        // Wait for address
                        return false;
                    }
                    goto case TriggerTransferSource.Address;
                case TriggerTransferSource.Address:
                    if(skipAddress && source == TriggerTransferSource.Address)
                    {
                        this.Log(LogLevel.Noisy, "Not transferring, {0} is disabled", nameof(TriggerTransferSource.Address));
                        return false;
                    }
                    if(!skipData && functionalMode.Value == ModeOfOperation.IndirectWrite)
                    {
                        this.Log(LogLevel.Noisy, "Data is missing, not transferring");
                        // Wait for data
                        return false;
                    }
                    // It's safe to break here, instead of jumping to data, since it can be a terminal stage too
                    break;
                case TriggerTransferSource.DataWrite:
                    if(skipData && source == TriggerTransferSource.DataWrite)
                    {
                        this.Log(LogLevel.Noisy, "Not transferring, {0} is disabled", nameof(TriggerTransferSource.DataWrite));
                        return false;
                    }
                    break;
                case TriggerTransferSource.DataRead:
                    // Polling mode schedules transfers by its own
                    if(functionalMode.Value == ModeOfOperation.AutomaticStatusPolling)
                    {
                        return false;
                    }
                    // Only allow Data read to trigger transfer, if there already is a transfer ongoing
                    // and there was not enough space in the transfer FIFO, so the transfer was suspended
                    if(remainingBytesToTransfer == null)
                    {
                        this.Log(LogLevel.Noisy, "Not transferring, {0} transfer is complete", nameof(TriggerTransferSource.DataRead));
                        return false;
                    }
                    break;
                default:
                    this.ErrorLog("Trigger source {0} is unrecognized, not transferring", source);
                    return false;
            }
            return true;
        }

        private bool DoesPolledDataMatch(uint polledUInt)
        {
            switch(pollingMatchMode.Value)
            {
                case PollingMatchMode.AllBits:
                    // Since XOR returns 1 only on differing bits, zero means no change at all
                    // AND with the mask, and if equals zero, both values match on all bits
                    return ((polledUInt ^ pollingReferenceMatch.Value) & pollingMask.Value) == 0;
                case PollingMatchMode.AnyBit:
                    // If the diff (AND mask) is equal to the mask bytes, that means there was no single bit matched
                    return ((polledUInt ^ pollingReferenceMatch.Value) & pollingMask.Value) != pollingMask.Value;
                default:
                    throw new InvalidOperationException($"Invalid match mode: {pollingMatchMode.Value}");
            }
        }

        private void HandlePollingData()
        {
            if(functionalMode.Value != ModeOfOperation.AutomaticStatusPolling)
            {
                return;
            }

            uint responseUInt = ReadFromDataRegister((int)(dataSize.Value + 1) * 8);
            bool matched = DoesPolledDataMatch(responseUInt);
            if(matched)
            {
                statusMatch.Value = true;
            }
            
            if(!matched || !pollingModeStopOnMatch.Value)
            {
                var nextEvent = pollingInterval.Value * PollingMultiplier;
                this.Log(LogLevel.Debug, "Polling not matched (got: 0x{0:X}, expected: 0x{1:X}, mask: 0x{2:X}), scheduling next polling in {3} microseconds",
                    responseUInt, pollingReferenceMatch.Value, pollingMask.Value, nextEvent);
                var token = pollingTokenSource.Token;
                Machine.ScheduleAction(TimeInterval.FromMicroseconds(nextEvent), _ =>
                {
                    lock(locker)
                    {
                        if(token.IsCancellationRequested)
                        {
                            return;
                        }
                        // Schedule a deferred polling action, if there was no match
                        transferFifo.Clear();
                        TransferData();
                        HandlePollingData();
                        UpdateInterrupts();
                    }
                }, $"{nameof(STM32H7_QuadSPI)} Polling task");
            }
        }

        private void TransferData()
        {
            if(skipData)
            {
                return;
            }
            if(functionalMode.Value == ModeOfOperation.AutomaticStatusPolling && dataSize.Value > 3)
            {
                // In Status polling only 4 bytes can be transferred at once, for the purpose of the comparison
                this.Log(LogLevel.Error, "DataSize cannot be more than 3 when in {0} mode. Automatically clamping the comparison length to 3",
                    nameof(ModeOfOperation.AutomaticStatusPolling));
                dataSize.Value = 3;
            }

            if(remainingBytesToTransfer == null)
            {
                if(dataSize.Value == uint.MaxValue)
                {
                    // Transfer all bytes, until the end of the flash memory
                    remainingBytesToTransfer = 2 << (int)(flashSize.Value + 1);
                }
                else
                {
                    remainingBytesToTransfer = (int)dataSize.Value + 1;
                }
            }

            byte d = 0;
            while((functionalMode.Value != ModeOfOperation.IndirectWrite || transferFifo.TryDequeue(out d))
                && remainingBytesToTransfer > 0 && transferFifo.Count <= MaximumFifoDepth)
            {
                --remainingBytesToTransfer;
                if(functionalMode.Value == ModeOfOperation.IndirectWrite)
                {
                    this.Log(LogLevel.Debug, "Sending byte: 0x{0:X}, bytes left: {1}", d, remainingBytesToTransfer);
                }
                var recv = RegisteredPeripheral.Transmit(d);
                if(functionalMode.Value != ModeOfOperation.IndirectWrite)
                {
                    this.Log(LogLevel.Debug, "Got byte: 0x{0:X}, bytes left: {1}", recv, remainingBytesToTransfer);
                    ReceiveData(recv);
                }
            }
            if(remainingBytesToTransfer == 0)
            {
                RegisteredPeripheral.FinishTransmission();
                transferComplete.Value = true;
                remainingBytesToTransfer = null;
            }
        }

        private void ReceiveData(byte data)
        {
            if(functionalMode.Value == ModeOfOperation.IndirectWrite)
            {
                return;
            }
            transferFifo.Enqueue(data);
            if(functionalMode.Value == ModeOfOperation.AutomaticStatusPolling)
            {
                // Status polling implies that only the last 4 values obtained (uint32) make their way into the FIFO
                // So don't allow the queue to grow beyond this limit
                if(transferFifo.Count > 4)
                {
                    transferFifo.Dequeue();
                }
            }
        }

        private const int MaximumFifoDepth = 32;

        private IFlagRegisterField transferCompleteIrqEnable;
        private IFlagRegisterField statusMatchInterruptEnable;
        private IFlagRegisterField fifoThresholdInterruptEnable;

        private IFlagRegisterField transferComplete;
        private IFlagRegisterField statusMatch;
        private IFlagRegisterField fifoThresholdReached;
        private IFlagRegisterField pollingModeStopOnMatch;

        // Sizes specified here are always less by one than the real transfer size (that's how the HW handles the registers)
        // e.g. size 0 means that 1 byte is to be transferred
        private IFlagRegisterField enabled;
        private IValueRegisterField fifoThreshold;
        private IValueRegisterField instruction;
        private IValueRegisterField dataSize;
        private IValueRegisterField address;
        private IValueRegisterField addressSize;
        private IValueRegisterField alternateBytes;
        private IValueRegisterField alternateBytesSize;
        // Number of bytes in flash = 2 ** (flashSize + 1)
        private IValueRegisterField flashSize;
        
        private IValueRegisterField pollingMask;
        private IValueRegisterField pollingReferenceMatch;
        private IValueRegisterField pollingInterval;

        private bool skipInstruction = true;
        private bool skipAddress = true;
        private bool skipAlternateBytes = true;
        private bool skipData = true;
        private int? remainingBytesToTransfer;
        private readonly object locker = new object();

        private IEnumRegisterField<ModeOfOperation> functionalMode;
        private IEnumRegisterField<PollingMatchMode> pollingMatchMode;

        private readonly DoubleWordRegisterCollection registers;

        private readonly Queue<byte> transferFifo = new Queue<byte>();

        private CancellationTokenSource pollingTokenSource = new CancellationTokenSource();

        private enum TriggerTransferSource
        {
            Instruction,
            Address,
            DataWrite,
            DataRead
        }

        private enum PollingMatchMode
        {
            AllBits = 0b0, // AND
            AnyBit = 0b1, // OR
        }

        private enum ModeOfOperation
        {
            IndirectWrite = 0b00,
            IndirectRead = 0b01,
            AutomaticStatusPolling = 0b10,
            // Note, that we can't map a flash memory to address range here
            // this has to be a property of the flash itself (e.g. GenericSpiFlash: underlyingMemory)
            MemoryMapped = 0b11,
        }

        private enum Registers
        {
            Control = 0x00,
            DeviceConfiguration = 0x04,
            Status = 0x08,
            FlagClear = 0x0C,
            DataLength = 0x10,
            CommunicationConfiguration = 0x14,
            Address = 0x18,
            AlternateByte = 0x1C,
            Data = 0x20,
            PollingStatusMask = 0x24,
            PollingStatusMatch = 0x28,
            PollingInterval = 0x2C,
            LowPowerTimeout = 0x30,
        }
    }
}
