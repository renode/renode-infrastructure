//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Alexey Zagorodnikov <xglooom@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.I2C
{
    public interface IMPC56xxI2CPeripheral : II2CPeripheral
    {
        bool BeginTransmission(bool read);

        void EndTransmission(bool stop);

        void AbortTransmission();
    }

    // MPC56xx/PXD20 byte-register I2C subset used by the tested MPC5645S firmware.
    // The target writes IBFD=0x56. Transfer completion is intentionally delayed
    // by a fixed functional byte latency so an asserted interrupt cannot re-enter
    // the initiating ISR. This is not an electrical or cycle-accurate bus model.
    public sealed class MPC56xx_I2C : SimpleContainer<II2CPeripheral>, IBytePeripheral,
        IWordPeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public MPC56xx_I2C(IMachine machine, ulong inputClockFrequency = DefaultInputClockFrequency,
            ulong byteTransferTicks = DefaultByteTransferTicks) : base(machine)
        {
            if(inputClockFrequency == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputClockFrequency));
            }
            if(byteTransferTicks == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(byteTransferTicks));
            }

            this.inputClockFrequency = inputClockFrequency;
            this.byteTransferTicks = byteTransferTicks;
            transferTimer = new LimitTimer(machine.ClockSource, inputClockFrequency, this,
                nameof(transferTimer), limit: byteTransferTicks, direction: Direction.Descending,
                enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: false);
            transferTimer.LimitReached += CompleteTransfer;
            writeBuffer = new List<byte>();
            registers = new byte[RegisterCount];
            IRQ = new GPIO();
            Reset();
        }

        public override void Reset()
        {
            lock(sync)
            {
                selectedExtendedDevice?.AbortTransmission();
                selectedDevice = null;
                selectedExtendedDevice = null;
                state = TransferState.Idle;
                pendingOperation = PendingOperation.None;
                writeBuffer.Clear();
                Array.Clear(registers, 0, registers.Length);
                registers[(int)Registers.Control] = ControlModuleDisable;
                registers[(int)Registers.Status] = StatusTransferComplete;
                transferTimer.Reset();
                transferTimer.AutoUpdate = false;
                transferTimer.EventEnabled = true;
                transferTimer.Limit = byteTransferTicks;
                transferTimer.Value = byteTransferTicks;
                transferTimer.Enabled = false;
                IRQ.Unset();
                CompletedTransferCount = 0;
                AddressAttemptCount = 0;
                AcknowledgedAddressCount = 0;
                ReadByteCount = 0;
                WriteByteCount = 0;
                LastDeviceAddress = 0;
            }
        }

        public byte ReadByte(long offset)
        {
            lock(sync)
            {
                if(offset < 0 || offset >= RegisterCount)
                {
                    return 0;
                }
                if(offset == (long)Registers.Data && IsReceiving)
                {
                    var value = registers[(int)Registers.Data];
                    // The OEM driver generates STOP before draining the final
                    // shift-register byte. Once MS is clear, this read must not
                    // schedule another transfer or interrupt.
                    if(IsMaster && !IsDisabled && selectedDevice != null)
                    {
                        ClearCompletion();
                        Schedule(PendingOperation.ReceiveByte);
                    }
                    return value;
                }
                return registers[offset];
            }
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)ReadSized(offset, 2);
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadSized(offset, 4);
        }

        public void WriteByte(long offset, byte value)
        {
            lock(sync)
            {
                if(offset < 0 || offset >= RegisterCount)
                {
                    return;
                }
                switch((Registers)offset)
                {
                case Registers.Status:
                    if((value & StatusInterruptFlag) != 0)
                    {
                        registers[(int)Registers.Status] &= unchecked((byte)~StatusInterruptFlag);
                    }
                    if((value & StatusArbitrationLost) != 0)
                    {
                        registers[(int)Registers.Status] &= unchecked((byte)~StatusArbitrationLost);
                    }
                    UpdateInterrupt();
                    return;
                case Registers.Control:
                    WriteControl(value);
                    return;
                case Registers.Data:
                    WriteData(value);
                    return;
                default:
                    registers[offset] = value;
                    return;
                }
            }
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteSized(offset, 2, value);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteSized(offset, 4, value);
        }

        public GPIO IRQ { get; }

        public long Size => SizeValue;

        public ulong InputClockFrequency => inputClockFrequency;

        public ulong ByteTransferTicks => byteTransferTicks;

        public ulong CompletedTransferCount { get; private set; }

        public ulong AddressAttemptCount { get; private set; }

        public ulong AcknowledgedAddressCount { get; private set; }

        public ulong ReadByteCount { get; private set; }

        public ulong WriteByteCount { get; private set; }

        public uint LastDeviceAddress { get; private set; }

        public bool IsTransferPending => pendingOperation != PendingOperation.None;

        private uint ReadSized(long offset, int size)
        {
            uint result = 0;
            for(var index = 0; index < size; index++)
            {
                result = (result << 8) | ReadByte(offset + index);
            }
            return result;
        }

        private void WriteSized(long offset, int size, uint value)
        {
            for(var index = 0; index < size; index++)
            {
                WriteByte(offset + index, (byte)(value >> ((size - 1 - index) * 8)));
            }
        }

        private void WriteControl(byte value)
        {
            var previous = registers[(int)Registers.Control];
            registers[(int)Registers.Control] = value;

            if((value & ControlModuleDisable) != 0)
            {
                CancelControllerActivity(abortDevice: true);
                registers[(int)Registers.Status] = StatusTransferComplete;
                UpdateInterrupt();
                return;
            }

            var wasMaster = (previous & ControlMaster) != 0;
            var isMaster = (value & ControlMaster) != 0;
            if(!wasMaster && isMaster)
            {
                BeginAddressing();
                registers[(int)Registers.Status] |= StatusBusBusy;
            }
            else if(wasMaster && !isMaster)
            {
                FinishTransaction(stop: true);
                registers[(int)Registers.Status] &= unchecked((byte)~StatusBusBusy);
                CancelPendingTransfer();
            }
            else if(isMaster && (value & ControlRepeatedStart) != 0)
            {
                FinishTransaction(stop: false);
                BeginAddressing();
                registers[(int)Registers.Control] &= unchecked((byte)~ControlRepeatedStart);
            }
            UpdateInterrupt();
        }

        private void WriteData(byte value)
        {
            registers[(int)Registers.Data] = value;
            if(IsDisabled || !IsMaster || !IsTransmitting)
            {
                return;
            }

            if(state == TransferState.Addressing)
            {
                AddressAttemptCount++;
                var deviceAddress = value >> 1;
                var isRead = (value & 1) != 0;
                LastDeviceAddress = (uint)deviceAddress;
                selectedDevice = null;
                selectedExtendedDevice = null;
                if(TryGetByAddress(deviceAddress, out var candidate))
                {
                    var extended = candidate as IMPC56xxI2CPeripheral;
                    if(extended == null || extended.BeginTransmission(isRead))
                    {
                        selectedDevice = candidate;
                        selectedExtendedDevice = extended;
                    }
                }

                var acknowledged = selectedDevice != null;
                SetReceiveAcknowledge(!acknowledged);
                if(acknowledged)
                {
                    AcknowledgedAddressCount++;
                    state = isRead ? TransferState.Receiving : TransferState.Transmitting;
                }
                else
                {
                    state = TransferState.Unaddressed;
                }
                Schedule(PendingOperation.ByteComplete);
                return;
            }

            if(state == TransferState.Transmitting && selectedDevice != null)
            {
                writeBuffer.Add(value);
                WriteByteCount++;
            }
            Schedule(PendingOperation.ByteComplete);
        }

        private void BeginAddressing()
        {
            selectedDevice = null;
            selectedExtendedDevice = null;
            writeBuffer.Clear();
            state = TransferState.Addressing;
            SetReceiveAcknowledge(false);
        }

        private void FinishTransaction(bool stop)
        {
            FlushWriteBuffer();
            if(selectedExtendedDevice != null)
            {
                selectedExtendedDevice.EndTransmission(stop);
            }
            else
            {
                selectedDevice?.FinishTransmission();
            }
            selectedDevice = null;
            selectedExtendedDevice = null;
            writeBuffer.Clear();
            state = TransferState.Idle;
        }

        private void FlushWriteBuffer()
        {
            if(writeBuffer.Count == 0 || selectedDevice == null)
            {
                return;
            }
            selectedDevice.Write(writeBuffer.ToArray());
            writeBuffer.Clear();
        }

        private void CompleteTransfer()
        {
            lock(sync)
            {
                var operation = pendingOperation;
                pendingOperation = PendingOperation.None;
                transferTimer.Enabled = false;
                if(operation == PendingOperation.ReceiveByte && selectedDevice != null && state == TransferState.Receiving)
                {
                    var data = selectedDevice.Read(1);
                    registers[(int)Registers.Data] = data.Length > 0 ? data[0] : (byte)0xFF;
                    ReadByteCount++;
                }
                registers[(int)Registers.Status] |= StatusTransferComplete | StatusInterruptFlag;
                CompletedTransferCount++;
                UpdateInterrupt();
            }
        }

        private void Schedule(PendingOperation operation)
        {
            pendingOperation = operation;
            transferTimer.Enabled = false;
            transferTimer.Limit = byteTransferTicks;
            transferTimer.Value = byteTransferTicks;
            transferTimer.Enabled = true;
        }

        private void CancelPendingTransfer()
        {
            pendingOperation = PendingOperation.None;
            transferTimer.Enabled = false;
        }

        private void CancelControllerActivity(bool abortDevice)
        {
            CancelPendingTransfer();
            if(abortDevice)
            {
                selectedExtendedDevice?.AbortTransmission();
            }
            selectedDevice = null;
            selectedExtendedDevice = null;
            writeBuffer.Clear();
            state = TransferState.Idle;
            registers[(int)Registers.Status] &= unchecked((byte)~(StatusBusBusy | StatusInterruptFlag | StatusReceiveAcknowledge));
        }

        private void ClearCompletion()
        {
            registers[(int)Registers.Status] &= unchecked((byte)~(StatusTransferComplete | StatusInterruptFlag));
            UpdateInterrupt();
        }

        private void SetReceiveAcknowledge(bool nack)
        {
            if(nack)
            {
                registers[(int)Registers.Status] |= StatusReceiveAcknowledge;
            }
            else
            {
                registers[(int)Registers.Status] &= unchecked((byte)~StatusReceiveAcknowledge);
            }
        }

        private void UpdateInterrupt()
        {
            var pending = (registers[(int)Registers.Status] & StatusInterruptFlag) != 0
                && (registers[(int)Registers.Control] & ControlInterruptEnable) != 0
                && (registers[(int)Registers.Control] & ControlModuleDisable) == 0;
            IRQ.Set(pending);
        }

        private bool IsDisabled => (registers[(int)Registers.Control] & ControlModuleDisable) != 0;

        private bool IsMaster => (registers[(int)Registers.Control] & ControlMaster) != 0;

        private bool IsTransmitting => (registers[(int)Registers.Control] & ControlTransmit) != 0;

        private bool IsReceiving => state == TransferState.Receiving && !IsTransmitting;

        private II2CPeripheral selectedDevice;
        private IMPC56xxI2CPeripheral selectedExtendedDevice;
        private TransferState state;
        private PendingOperation pendingOperation;

        private readonly object sync = new object();
        private readonly LimitTimer transferTimer;
        private readonly List<byte> writeBuffer;
        private readonly byte[] registers;
        private readonly ulong inputClockFrequency;
        private readonly ulong byteTransferTicks;

        private const ulong DefaultInputClockFrequency = 40000000;
        private const ulong DefaultByteTransferTicks = 400;
        private const int RegisterCount = 8;
        private const long SizeValue = 0x8000;

        private const byte ControlModuleDisable = 0x80;
        private const byte ControlInterruptEnable = 0x40;
        private const byte ControlMaster = 0x20;
        private const byte ControlTransmit = 0x10;
        private const byte ControlRepeatedStart = 0x04;

        private const byte StatusTransferComplete = 0x80;
        private const byte StatusBusBusy = 0x20;
        private const byte StatusArbitrationLost = 0x10;
        private const byte StatusInterruptFlag = 0x02;
        private const byte StatusReceiveAcknowledge = 0x01;

        private enum Registers
        {
            Address = 0,
            FrequencyDivider = 1,
            Control = 2,
            Status = 3,
            Data = 4,
            InterruptConfig = 5,
            Debug = 6,
        }

        private enum TransferState
        {
            Idle,
            Addressing,
            Transmitting,
            Receiving,
            Unaddressed,
        }

        private enum PendingOperation
        {
            None,
            ByteComplete,
            ReceiveByte,
        }
    }
}
