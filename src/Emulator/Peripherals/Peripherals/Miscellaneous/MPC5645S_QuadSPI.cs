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
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // Targeted MPC5645S QuadSPI model for the flash transactions issued by the tested firmware.
    //
    // The controller contract follows MPC5645S Reference Manual Rev. 7,
    // chapter 35: SFAR selects Flash A/B, ICR triggers an IP command, TBDR and
    // RBDR0 carry transmit/receive data, SFMSR exposes BUSY/IP_ACC/RXWE, and
    // SFMFR contains W1C command/RX/error flags.
    //
    // The attached device profile implements only the documented S25FL-S
    // commands used by the firmware: MBR (FFh), BRWR (17h), RDCR (35h), WREN
    // (06h), WRR (01h), RDSR (05h), and WRDI (04h). Other command codes are
    // rejected with ICEF rather than returning a configured generic response.
    public class MPC5645S_QuadSPI : IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize
    {
        public MPC5645S_QuadSPI(IMachine machine, ulong transferFrequency = DefaultTransferFrequency,
            ulong transferTicks = DefaultTransferTicks, ulong registerWriteTicks = DefaultRegisterWriteTicks)
        {
            if(transferFrequency == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(transferFrequency));
            }
            if(transferTicks == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(transferTicks));
            }
            if(registerWriteTicks == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(registerWriteTicks));
            }

            this.machine = machine;
            this.transferTicks = transferTicks;
            this.registerWriteTicks = registerWriteTicks;
            registers = new byte[SizeValue];
            txBuffer = new Queue<byte>();
            flashes = new[] { new S25FLSFlash(), new S25FLSFlash() };

            transferTimer = new LimitTimer(machine.ClockSource, transferFrequency, this,
                nameof(transferTimer), limit: transferTicks, direction: Direction.Descending,
                enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: false);
            transferTimer.LimitReached += CompleteOperation;

            flashWriteTimer = new LimitTimer(machine.ClockSource, transferFrequency, this,
                nameof(flashWriteTimer), limit: registerWriteTicks, direction: Direction.Descending,
                enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: false);
            flashWriteTimer.LimitReached += CompleteFlashRegisterWrite;

            Reset();
        }

        public void Reset()
        {
            lock(sync)
            {
                Array.Clear(registers, 0, registers.Length);
                WriteRegisterRaw(ModuleConfigurationOffset, ModuleConfigurationResetValue);
                ResetTimer(transferTimer, transferTicks);
                ResetTimer(flashWriteTimer, registerWriteTicks);
                txBuffer.Clear();
                receiveData = 0;
                receiveEntries = 0;
                flags = TxBufferFillFlag;
                busy = false;
                ipAccess = false;
                currentPort = InvalidPort;
                currentCommand = 0;
                currentDataCount = 0;
                currentTxData = Array.Empty<byte>();
                pendingFlashWritePort = InvalidPort;
                completionCount = 0;
                rejectedCommandCount = 0;
                lastCommand = 0;
                lastAddress = 0;
                lastTransmitData = 0;
                lastReceiveData = 0;
                lastErrorFlags = 0;
                foreach(var flash in flashes)
                {
                    flash.Reset();
                }
            }
        }

        public void AttachS25FL256S(uint port = 0)
        {
            lock(sync)
            {
                var flash = GetFlashByPort(port);
                flash.Present = true;
                flash.Reset();
            }
        }

        public void DetachFlash(uint port = 0)
        {
            lock(sync)
            {
                var flash = GetFlashByPort(port);
                flash.Present = false;
                flash.Reset();
            }
        }

        public bool IsFlashPresent(uint port = 0)
        {
            lock(sync)
            {
                return GetFlashByPort(port).Present;
            }
        }

        public uint GetFlashStatusRegister1(uint port = 0)
        {
            lock(sync)
            {
                return GetFlashByPort(port).BuildStatusRegister1();
            }
        }

        public uint GetFlashConfigurationRegister1(uint port = 0)
        {
            lock(sync)
            {
                return GetFlashByPort(port).ConfigurationRegister1;
            }
        }

        public uint GetFlashBankRegister(uint port = 0)
        {
            lock(sync)
            {
                return GetFlashByPort(port).BankRegister;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadSized(offset, 4);
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)ReadSized(offset, 2);
        }

        public byte ReadByte(long offset)
        {
            return (byte)ReadSized(offset, 1);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteSized(offset, 4, value);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteSized(offset, 2, value);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteSized(offset, 1, value);
        }

        public ulong CompletionCount
        {
            get
            {
                lock(sync)
                {
                    return completionCount;
                }
            }
        }

        public ulong RejectedCommandCount
        {
            get
            {
                lock(sync)
                {
                    return rejectedCommandCount;
                }
            }
        }

        public uint LastCommand
        {
            get
            {
                lock(sync)
                {
                    return lastCommand;
                }
            }
        }

        public uint LastAddress
        {
            get
            {
                lock(sync)
                {
                    return lastAddress;
                }
            }
        }

        public uint LastTransmitData
        {
            get
            {
                lock(sync)
                {
                    return lastTransmitData;
                }
            }
        }

        public uint LastReceiveData
        {
            get
            {
                lock(sync)
                {
                    return lastReceiveData;
                }
            }
        }

        public uint LastErrorFlags
        {
            get
            {
                lock(sync)
                {
                    return lastErrorFlags;
                }
            }
        }

        public long Size => SizeValue;

        private static void ResetTimer(LimitTimer timer, ulong ticks)
        {
            timer.Reset();
            timer.AutoUpdate = false;
            timer.EventEnabled = true;
            timer.Limit = ticks;
            timer.Value = ticks;
            timer.Enabled = false;
        }

        private static int DecodePort(uint address)
        {
            if(address >= FlashABase && address <= FlashAEnd)
            {
                return 0;
            }
            if(address >= FlashBBase && address <= FlashBEnd)
            {
                return 1;
            }
            if(address >= ParallelFlashBase && address <= ParallelFlashEnd)
            {
                return ParallelPort;
            }
            return InvalidPort;
        }

        private static bool IsSupportedCommandShape(byte command, int count)
        {
            switch(command)
            {
            case ModeBitResetCommand:
            case WriteEnableCommand:
            case WriteDisableCommand:
                return count == 0;
            case BankRegisterWriteCommand:
            case ReadConfigurationRegisterCommand:
            case ReadStatusRegisterCommand:
                return count == 1;
            case WriteRegistersCommand:
                return count == 1 || count == 2;
            default:
                return false;
            }
        }

        private static bool IsTransmitCommand(byte command)
        {
            return command == BankRegisterWriteCommand || command == WriteRegistersCommand;
        }

        private static bool IsReceiveCommand(byte command)
        {
            return command == ReadConfigurationRegisterCommand || command == ReadStatusRegisterCommand;
        }

        private static byte ExtractByte(uint value, long lane)
        {
            return (byte)(value >> ((3 - (int)lane) * 8));
        }

        private static uint PositionValue(long registerOffset, long offset, int size, uint value)
        {
            var lane = (int)(offset - registerOffset);
            var shift = (4 - lane - size) * 8;
            var mask = size == 4 ? uint.MaxValue : (1u << (size * 8)) - 1;
            return (value & mask) << shift;
        }

        private static bool IsValidRange(long offset, int size)
        {
            return (size == 1 || size == 2 || size == 4)
                && offset >= 0 && offset <= SizeValue - size;
        }

        private static bool WithinRegister(long offset, int size, long registerOffset)
        {
            return offset >= registerOffset && offset + size <= registerOffset + 4;
        }

        private static bool Overlaps(long offset, int size, long registerOffset)
        {
            return offset < registerOffset + 4 && registerOffset < offset + size;
        }

        private uint ReadSized(long offset, int size)
        {
            if(!IsValidRange(offset, size))
            {
                return 0;
            }
            if(Overlaps(offset, size, StatusOffset) || Overlaps(offset, size, FlagsOffset)
                || Overlaps(offset, size, ReceiveDataOffset))
            {
                SynchronizeCurrentCpuTime();
            }
            lock(sync)
            {
                uint result = 0;
                for(var i = 0; i < size; i++)
                {
                    result = (result << 8) | ReadByteInternal(offset + i);
                }
                return result;
            }
        }

        private byte ReadByteInternal(long offset)
        {
            if(offset >= StatusOffset && offset < StatusOffset + 4)
            {
                return ExtractByte(BuildStatus(), offset - StatusOffset);
            }
            if(offset >= FlagsOffset && offset < FlagsOffset + 4)
            {
                return ExtractByte(flags, offset - FlagsOffset);
            }
            if(offset >= ReceiveDataOffset && offset < ReceiveDataOffset + 4)
            {
                return ExtractByte(receiveData, offset - ReceiveDataOffset);
            }
            if(offset >= TxBufferStatusOffset && offset < TxBufferStatusOffset + 4)
            {
                return ExtractByte(BuildTxBufferStatus(), offset - TxBufferStatusOffset);
            }
            if(offset >= RxBufferStatusOffset && offset < RxBufferStatusOffset + 4)
            {
                return ExtractByte(BuildRxBufferStatus(), offset - RxBufferStatusOffset);
            }
            return registers[offset];
        }

        private void WriteSized(long offset, int size, uint value)
        {
            if(!IsValidRange(offset, size))
            {
                return;
            }
            lock(sync)
            {
                if(WithinRegister(offset, size, StatusOffset)
                    || WithinRegister(offset, size, TxBufferStatusOffset)
                    || WithinRegister(offset, size, RxBufferStatusOffset)
                    || WithinRegister(offset, size, ReceiveDataOffset))
                {
                    return;
                }

                if(WithinRegister(offset, size, FlagsOffset))
                {
                    ClearFlags(PositionValue(FlagsOffset, offset, size, value));
                    return;
                }

                if(Overlaps(offset, size, SerialFlashAddressOffset) && busy)
                {
                    RejectArbitratedWrite("SFAR");
                    return;
                }
                if(Overlaps(offset, size, RxBufferControlOffset) && busy)
                {
                    RejectArbitratedWrite("RBCT");
                    return;
                }
                if(Overlaps(offset, size, InstructionCodeOffset) && busy)
                {
                    RejectArbitratedWrite("ICR");
                    return;
                }

                WriteBytes(offset, size, value);

                if(Overlaps(offset, size, ModuleConfigurationOffset))
                {
                    ApplyModuleConfigurationActions();
                }
                if(offset == TxBufferDataOffset && size == 4)
                {
                    PushTransmitWord(value);
                }
                if(Overlaps(offset, size, InstructionCodeOffset + 3))
                {
                    StartOperation(ReadRegister(InstructionCodeOffset));
                }
            }
        }

        private void ApplyModuleConfigurationActions()
        {
            var value = ReadRegister(ModuleConfigurationOffset);
            if((value & ClearTxBufferMask) != 0)
            {
                txBuffer.Clear();
                flags |= TxBufferFillFlag;
            }
            if((value & ClearRxBufferMask) != 0)
            {
                receiveData = 0;
                receiveEntries = 0;
                flags &= ~RxBufferDrainFlag;
            }
            value &= ~(ClearTxBufferMask | ClearRxBufferMask);
            WriteRegisterRaw(ModuleConfigurationOffset, value);
        }

        private void PushTransmitWord(uint value)
        {
            lastTransmitData = value;
            txBuffer.Enqueue((byte)(value >> 24));
            txBuffer.Enqueue((byte)(value >> 16));
            txBuffer.Enqueue((byte)(value >> 8));
            txBuffer.Enqueue((byte)value);
            if(txBuffer.Count >= TxBufferCapacityBytes)
            {
                flags &= ~TxBufferFillFlag;
            }
        }

        private void StartOperation(uint commandWord)
        {
            lastCommand = commandWord;
            lastAddress = ReadRegister(SerialFlashAddressOffset);
            lastErrorFlags = 0;

            if((ReadRegister(ModuleConfigurationOffset) & ModuleDisableMask) != 0)
            {
                RejectCommand(InstructionCodeErrorFlag,
                    "Ignoring QuadSPI IP command 0x{0:X2} while the module is disabled.", commandWord & 0xFF);
                return;
            }

            var vendorModel = (ReadRegister(ModuleConfigurationOffset) & VendorModelMask) >> VendorModelShift;
            if(vendorModel != SpansionVendorModel)
            {
                RejectCommand(InstructionCodeErrorFlag,
                    "Ignoring QuadSPI IP command 0x{0:X2}: VMID {1} is not the modeled Spansion profile.",
                    commandWord & 0xFF, vendorModel);
                return;
            }

            currentPort = DecodePort(lastAddress);
            if(currentPort == ParallelPort)
            {
                RejectCommand(IpCommandUsageErrorFlag,
                    "Ignoring non-data-read QuadSPI IP command 0x{0:X2} in parallel-flash address space 0x{1:X8}.",
                    commandWord & 0xFF, lastAddress);
                return;
            }
            if(currentPort == InvalidPort)
            {
                RejectCommand(InstructionCodeErrorFlag,
                    "Ignoring QuadSPI IP command 0x{0:X2} for unsupported SFAR address 0x{1:X8}.",
                    commandWord & 0xFF, lastAddress);
                return;
            }

            currentCommand = (byte)commandWord;
            // The observed fixed-function ICR encoding places the byte count in
            // ICO[15:8]: 0x000100xx transfers one byte and 0x000200xx two.
            currentDataCount = (int)((commandWord >> 16) & 0xFF);
            if(!IsSupportedCommandShape(currentCommand, currentDataCount))
            {
                RejectCommand(InstructionCodeErrorFlag,
                    "Ignoring unsupported QuadSPI/S25FL-S command 0x{0:X2} with data count {1}.",
                    currentCommand, currentDataCount);
                return;
            }

            currentTxData = PullTransmitData(IsTransmitCommand(currentCommand) ? currentDataCount : 0);
            flags &= ~TransactionFinishedFlag;
            receiveData = 0;
            receiveEntries = 0;
            flags &= ~RxBufferDrainFlag;
            busy = true;
            ipAccess = true;
            ArmTimer(transferTimer, transferTicks);
        }

        private void CompleteOperation()
        {
            lock(sync)
            {
                var flash = flashes[currentPort];
                uint rx = 0;
                var hasReceiveData = false;

                if(!flash.Present)
                {
                    if(IsReceiveCommand(currentCommand))
                    {
                        rx = 0xFF000000;
                        hasReceiveData = true;
                    }
                }
                else
                {
                    switch(currentCommand)
                    {
                    case ModeBitResetCommand:
                        flash.ContinuousReadMode = false;
                        break;
                    case BankRegisterWriteCommand:
                        flash.BankRegister = currentTxData[0];
                        break;
                    case ReadConfigurationRegisterCommand:
                        rx = (uint)flash.ConfigurationRegister1 << 24;
                        hasReceiveData = true;
                        break;
                    case WriteEnableCommand:
                        if(!flash.WriteInProgress)
                        {
                            flash.WriteEnableLatch = true;
                        }
                        break;
                    case WriteRegistersCommand:
                        StartFlashRegisterWrite(flash, currentPort, currentTxData);
                        break;
                    case ReadStatusRegisterCommand:
                        rx = (uint)flash.BuildStatusRegister1() << 24;
                        hasReceiveData = true;
                        break;
                    case WriteDisableCommand:
                        flash.WriteEnableLatch = false;
                        break;
                    }
                }

                if(hasReceiveData)
                {
                    receiveData = rx;
                    receiveEntries = 1;
                    flags |= RxBufferDrainFlag;
                    lastReceiveData = rx;
                }
                else
                {
                    lastReceiveData = 0;
                }

                busy = false;
                ipAccess = false;
                flags |= TransactionFinishedFlag;
                completionCount++;
                currentPort = InvalidPort;
                currentTxData = Array.Empty<byte>();
            }
        }

        private void StartFlashRegisterWrite(S25FLSFlash flash, int port, byte[] data)
        {
            if(!flash.WriteEnableLatch || flash.WriteInProgress)
            {
                return;
            }

            flash.PendingStatusRegister1 = (byte)(data[0] & StatusRegisterWritableMask);
            flash.PendingConfigurationRegister1 = data.Length > 1
                ? (byte)(data[1] & ConfigurationRegisterWritableMask)
                : flash.ConfigurationRegister1;
            flash.WriteEnableLatch = false;
            flash.WriteInProgress = true;
            pendingFlashWritePort = port;
            ArmTimer(flashWriteTimer, registerWriteTicks);
        }

        private void CompleteFlashRegisterWrite()
        {
            lock(sync)
            {
                if(pendingFlashWritePort == InvalidPort)
                {
                    return;
                }
                var flash = flashes[pendingFlashWritePort];
                flash.StatusRegister1 = flash.PendingStatusRegister1;
                flash.ConfigurationRegister1 = flash.PendingConfigurationRegister1;
                flash.WriteInProgress = false;
                pendingFlashWritePort = InvalidPort;
            }
        }

        private byte[] PullTransmitData(int count)
        {
            var result = new byte[count];
            for(var i = 0; i < count; i++)
            {
                if(txBuffer.Count == 0)
                {
                    flags |= TxBufferUnderrunFlag;
                    result[i] = 0xFF;
                }
                else
                {
                    result[i] = txBuffer.Dequeue();
                }
            }
            if(txBuffer.Count < TxBufferCapacityBytes)
            {
                flags |= TxBufferFillFlag;
            }
            return result;
        }

        private void RejectArbitratedWrite(string registerName)
        {
            flags |= IpCommandTriggerErrorFlag;
            lastErrorFlags = IpCommandTriggerErrorFlag;
            rejectedCommandCount++;
            this.Log(LogLevel.Warning,
                "Ignoring write to QuadSPI {0} while an IP command is active; IPIEF asserted.", registerName);
        }

        private void RejectCommand(uint errorFlag, string format, params object[] args)
        {
            flags |= errorFlag;
            lastErrorFlags = errorFlag;
            rejectedCommandCount++;
            this.Log(LogLevel.Warning, format, args);
        }

        private void ClearFlags(uint writtenBits)
        {
            if((writtenBits & RxBufferDrainFlag) != 0)
            {
                var watermark = (int)(ReadRegister(RxBufferControlOffset) & RxBufferWatermarkMask);
                if(receiveEntries > watermark)
                {
                    receiveEntries = Math.Max(0, receiveEntries - (watermark + 1));
                    if(receiveEntries == 0)
                    {
                        receiveData = 0;
                    }
                }
            }
            flags &= ~writtenBits;
            if(receiveEntries > (ReadRegister(RxBufferControlOffset) & RxBufferWatermarkMask))
            {
                flags |= RxBufferDrainFlag;
            }
        }

        private uint BuildStatus()
        {
            uint result = 0;
            if(txBuffer.Count != 0)
            {
                result |= TxBufferNotEmptyMask;
            }
            if(receiveEntries > 0)
            {
                result |= RxBufferWatermarkExceededMask;
            }
            if(ipAccess)
            {
                result |= IpAccessMask;
            }
            if(busy)
            {
                result |= BusyMask;
            }
            return result;
        }

        private uint BuildTxBufferStatus()
        {
            var entries = (uint)((txBuffer.Count + 3) / 4);
            return entries << TxBufferFillLevelShift;
        }

        private uint BuildRxBufferStatus()
        {
            return (uint)receiveEntries << RxBufferFillLevelShift;
        }

        private void WriteBytes(long offset, int size, uint value)
        {
            for(var i = 0; i < size; i++)
            {
                var shift = (size - 1 - i) * 8;
                registers[offset + i] = (byte)(value >> shift);
            }
        }

        private uint ReadRegister(long offset)
        {
            return ((uint)registers[offset] << 24)
                | ((uint)registers[offset + 1] << 16)
                | ((uint)registers[offset + 2] << 8)
                | registers[offset + 3];
        }

        private void WriteRegisterRaw(long offset, uint value)
        {
            registers[offset] = (byte)(value >> 24);
            registers[offset + 1] = (byte)(value >> 16);
            registers[offset + 2] = (byte)(value >> 8);
            registers[offset + 3] = (byte)value;
        }

        private void SynchronizeCurrentCpuTime()
        {
            if(machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }
        }

        private void ArmTimer(LimitTimer timer, ulong ticks)
        {
            timer.Limit = ticks;
            timer.Value = ticks;
            timer.Enabled = true;
        }

        private S25FLSFlash GetFlashByPort(uint port)
        {
            if(port >= flashes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "QuadSPI flash port must be 0 (A) or 1 (B).");
            }
            return flashes[port];
        }

        private byte currentCommand;
        private int currentDataCount;
        private byte[] currentTxData;
        private int pendingFlashWritePort;
        private ulong completionCount;
        private uint lastErrorFlags;
        private uint lastAddress;
        private uint lastTransmitData;
        private uint lastReceiveData;
        private int currentPort;
        private uint lastCommand;
        private uint flags;
        private ulong rejectedCommandCount;
        private uint receiveData;
        private bool ipAccess;

        private bool busy;
        private int receiveEntries;
        private readonly IMachine machine;
        private readonly LimitTimer transferTimer;
        private readonly LimitTimer flashWriteTimer;
        private readonly byte[] registers;

        private readonly object sync = new object();
        private readonly Queue<byte> txBuffer;
        private readonly S25FLSFlash[] flashes;
        private readonly ulong transferTicks;
        private readonly ulong registerWriteTicks;
        private const byte WriteDisableCommand = 0x04;
        private const int TxBufferFillLevelShift = 8;
        private const int RxBufferFillLevelShift = 8;
        private const int TxBufferCapacityBytes = 15 * 4;

        private const byte ModeBitResetCommand = 0xFF;
        private const byte BankRegisterWriteCommand = 0x17;
        private const byte ReadConfigurationRegisterCommand = 0x35;
        private const byte WriteEnableCommand = 0x06;
        private const byte WriteRegistersCommand = 0x01;
        private const byte ReadStatusRegisterCommand = 0x05;

        private const uint RxBufferWatermarkMask = 0x1F;

        private const byte StatusWriteInProgressMask = 1 << 0;
        private const uint FlashBBase = 0x78000000;
        private const byte StatusRegisterWritableMask = 0xFC;
        private const byte ConfigurationRegisterWritableMask = 0xFF;

        private const uint FlashABase = 0x70000000;
        private const uint FlashAEnd = 0x77FFFFFF;
        private const uint FlashBEnd = 0x7FFFFFFF;
        private const uint ParallelFlashBase = 0x80000000;
        private const uint ParallelFlashEnd = 0x8FFFFFFF;
        private const int InvalidPort = -1;
        private const int ParallelPort = -2;

        private const ulong DefaultTransferFrequency = 1000000;
        private const uint TransactionFinishedFlag = 1u;
        private const byte StatusWriteEnableLatchMask = 1 << 1;
        private const uint IpCommandTriggerErrorFlag = 1u << 6;
        private const long FlagsOffset = 0x160;
        private const uint IpCommandUsageErrorFlag = 1u << 11;

        private const int SizeValue = 0x4000;
        private const long ModuleConfigurationOffset = 0x000;
        private const long SerialFlashAddressOffset = 0x100;
        private const long InstructionCodeOffset = 0x104;
        private const long RxBufferStatusOffset = 0x10C;
        private const long RxBufferControlOffset = 0x110;
        private const long TxBufferStatusOffset = 0x150;
        private const long TxBufferDataOffset = 0x154;
        private const long StatusOffset = 0x15C;
        private const ulong DefaultTransferTicks = 10;
        private const long ReceiveDataOffset = 0x200;

        private const uint ModuleConfigurationResetValue = 0x000F4000;
        private const uint ModuleDisableMask = 1u << 14;
        private const uint ClearTxBufferMask = 1u << 11;
        private const uint ClearRxBufferMask = 1u << 10;
        private const uint VendorModelMask = 0x78;
        private const int VendorModelShift = 3;
        private const uint SpansionVendorModel = 2;

        private const uint TxBufferNotEmptyMask = 1u << 24;
        private const uint RxBufferWatermarkExceededMask = 1u << 16;
        private const uint IpAccessMask = 1u << 1;
        private const uint BusyMask = 1u;

        private const uint TxBufferFillFlag = 1u << 27;
        private const uint TxBufferUnderrunFlag = 1u << 26;
        private const uint RxBufferDrainFlag = 1u << 16;
        private const uint InstructionCodeErrorFlag = 1u << 10;
        private const ulong DefaultRegisterWriteTicks = 100;

        private sealed class S25FLSFlash
        {
            public void Reset()
            {
                StatusRegister1 = 0;
                ConfigurationRegister1 = 0;
                BankRegister = 0;
                PendingStatusRegister1 = 0;
                PendingConfigurationRegister1 = 0;
                WriteEnableLatch = false;
                WriteInProgress = false;
                ContinuousReadMode = false;
            }

            public byte BuildStatusRegister1()
            {
                var result = StatusRegister1;
                if(WriteInProgress)
                {
                    result |= StatusWriteInProgressMask;
                }
                if(WriteEnableLatch)
                {
                    result |= StatusWriteEnableLatchMask;
                }
                return result;
            }

            public bool Present;
            public bool WriteEnableLatch;
            public bool WriteInProgress;
            public bool ContinuousReadMode;
            public byte StatusRegister1;
            public byte ConfigurationRegister1;
            public byte BankRegister;
            public byte PendingStatusRegister1;
            public byte PendingConfigurationRegister1;
        }
    }
}
