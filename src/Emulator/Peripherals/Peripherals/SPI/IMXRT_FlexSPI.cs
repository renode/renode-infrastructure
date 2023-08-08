//
// Copyright (c) 2010-2023 Antmicro
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
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class IMXRT_FlexSPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public IMXRT_FlexSPI(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            lutMemory = new byte[NumberOfLUTs * 4];

            rxQueue = new RandomAccessQueue(FifoDepth * 4);
            txQueue = new RandomAccessQueue(FifoDepth * 4);

            commandsEngine = new CommandsEngine(this);

            DefineRegisters();
            UpdateInterrupts();
        }

        public override void Reset()
        {
            Array.Clear(lutMemory, 0, lutMemory.Length);

            rxQueue.Reset();
            txQueue.Reset();

            isInTransmit = false;
            totalReadCounter = 0;

            RegistersCollection.Reset();
            commandsEngine.Reset();

            UpdateInterrupts();
        }

        [ConnectionRegion("ciphertext")]
        public uint ReadDoubleWordFromCiphertext(long offset)
        {
            return ReadFromCiphertext(offset, 4);
        }

        [ConnectionRegion("ciphertext")]
        public void WriteDoubleWordToCiphertext(long offset, uint value)
        {
            WriteToCiphertext(offset, value, 4);
        }

        [ConnectionRegion("ciphertext")]
        public ushort ReadWordFromCiphertext(long offset)
        {
            return (ushort)ReadFromCiphertext(offset, 2);
        }

        [ConnectionRegion("ciphertext")]
        public void WriteWordToCiphertext(long offset, ushort value)
        {
            WriteToCiphertext(offset, value, 2);
        }

        [ConnectionRegion("ciphertext")]
        public byte ReadByteFromCiphertext(long offset)
        {
            return (byte)ReadFromCiphertext(offset, 1);
        }

        [ConnectionRegion("ciphertext")]
        public void WriteByteToCiphertext(long offset, byte value)
        {
            WriteToCiphertext(offset, value, 1);
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size => 0x4000;

        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private uint ReadFromCiphertext(long offset, int width)
        {
            DebugHelper.Assert(width > 0 && width <= 4);

            // here we set parameters for the read command  
            // that will be executed by the commands engine
            // note: this overwrites values in registers; not sure if this is how the actual HW works
            dataSize.Value = (uint)width;
            rxQueue.Reset();
            serialFlashAddress.Value = (uint)offset;

            commandsEngine.LoadCommands((uint)ahbReadSequenceIndex.Value, (uint)ahbReadSequenceLength.Value + 1);
            commandsEngine.Execute();

            var result = rxQueue.Read(0, width);

            this.Log(LogLevel.Debug, "Read {0}-byte{1} from ciphertext at offset 0x{2:X}: 0x{3:X}", width, width == 1 ? string.Empty : "s", offset, result);

            return result;
        }

        private void WriteToCiphertext(long offset, uint value, int width)
        {
            DebugHelper.Assert(width > 0 && width <= 4);
            this.Log(LogLevel.Debug, "Writing {0}-byte{1} to ciphertext at offset 0x{2:X}: 0x{3:X}", width, width == 1 ? string.Empty : "s",  offset, value);

            // here we set parameters for the write command  
            // that will be executed by the commands engine
            // note: this overwrites values in registers; not sure if this is how the actual HW works
            dataSize.Value = (uint)width;
            var count = txQueue.Fill(BitConverter.GetBytes(value).Take(width), reset: true);
            if(count != width)
            {
                this.Log(LogLevel.Warning, "Could not fit all bytes into FIFO - {0} of them were dropped", width - count);
            }

            serialFlashAddress.Value = (uint)offset;

            commandsEngine.LoadCommands((uint)ahbWriteSequenceIndex.Value, (uint)ahbWriteSequenceLength.Value + 1);
            commandsEngine.Execute();
            TryPushData();
        }

        private void DefineRegisters()
        {
            Registers.StatusRegister0.Define(this)
                .WithFlag(0, FieldMode.Read, name: "SEQIDLE - Command sequence idle ", valueProviderCallback: _ => !commandsEngine.InProgress)
                .WithFlag(1, FieldMode.Read, name: "ARBIDLE - Arbitrator idle", valueProviderCallback: _ => true) // when false: there is command sequence granted by arbitrator and not finished yet on FlexSPI interface
                .WithTag("ARBCMDSRC", 2, 2)
                .WithReservedBits(4, 28);

            Registers.IPCommandRegister.Define(this)
                .WithFlag(0, name: "TRG", writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        TriggerCommand();
                    }
                })
                .WithReservedBits(1, 31);

            Registers.IPControlRegister0.Define(this)
                .WithValueField(0, 32, out serialFlashAddress, name: "SFAR - Serial Flash Address for IP command");

            Registers.IPControlRegister1.Define(this)
                .WithValueField(0, 16, out dataSize, name: "IDATSZ - Flash Read/Program Data Size (in bytes) for IP command")
                .WithValueField(16, 4, out sequenceIndex, name: "ISEQID - Sequence Index in LUT for IP command")
                .WithReservedBits(20, 4)
                .WithValueField(24, 3, out sequenceNumber, name: "ISEQNUM - Sequence Number for IP command: ISEQNUM+1")
                .WithReservedBits(27, 4)
                .WithTaggedFlag("IPAREN - Parallel mode Enabled for IP command", 31);

            Registers.LUT0.DefineMany(this, NumberOfLUTs, (register, idx) =>
            {
                var j = idx;
                register.
                    WithValueFields(0, 8, 4, 
                        valueProviderCallback: (i, _) => lutMemory[j * 0x4 + i],
                        writeCallback: (i, _, value) => lutMemory[j * 0x4 + i] = (byte)value);
            });

            Registers.IPRXFIFOControlRegister.Define(this)
                .WithFlag(0, name: "CLRIPRXF - Clear all valid data entries in IP RX FIFO",
                    valueProviderCallback: _ => false, // this is not explicitly stated in the documentation
                    writeCallback: (_, val) => { if(val) rxQueue.Reset(); })
                .WithTaggedFlag("RXDMAEN - IP RX FIFO reading by DMA enabled", 1)
                .WithValueField(2, 4, out rxWatermark, name: "RXWMRK - Watermark level")
                .WithReservedBits(6, 26)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.IPTXFIFOControlRegister.Define(this)
                .WithFlag(0, name: "CLRIPTXF - Clear all valid data entries in IP TX FIFO", 
                    valueProviderCallback: _ => false, // this is not explicitly stated in the documentation
                    writeCallback: (_, val) => { if(val) txQueue.Reset(); })
                .WithTaggedFlag("TXDMAEN - IP TX FIFO reading by DMA enabled", 1)
                .WithValueField(2, 4, out txWatermark, name: "TXWMRK - Watermark level")
                .WithReservedBits(6, 26)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptRegister.Define(this)
                .WithFlag(0, out ipCommandDone, FieldMode.Read | FieldMode.WriteOneToClear, name: "IPCMDDONE - IP triggered Command Sequences Execution finished interrupt")
                .WithTaggedFlag("AHBCMDERR - AHB triggered Command Sequences Error Detected interrupt", 1)
                .WithTaggedFlag("IPCMDGE - IP triggered Command Sequences Grant Timeout interrupt", 2)
                .WithTaggedFlag("AHBCMDGE - AHB triggered Command Sequences Grant Timeout interrupt", 3)
                .WithTaggedFlag("IPCMDERR - IP triggered Command Sequences Error Detected interrupt", 4)
                .WithFlag(5, out ipRxWatermarkAvailable, FieldMode.Read | FieldMode.WriteOneToClear, name: "IPRXWA - IP RX FIFO watermark available interrupt")
                .WithFlag(6, out ipTxWatermarkEmpty, FieldMode.Read | FieldMode.WriteOneToClear, name: "IPTXWE - IP TX FIFO watermark empty interrupt", writeCallback: (_, val) => { if(val) TryPushData(); })
                .WithReservedBits(7, 1)
                .WithTaggedFlag("SEQTIMEOUT - Sequence execution timeout interrupt", 8)
                .WithTaggedFlag("SCKSTOPBYRD - SCLK is stopped during command sequence because Async RX FIFO full interrupt", 9)
                .WithTaggedFlag("SCKSTOPBYWR - SCLK is stopped during command sequence because Async TX FIFO empty interrupt", 10)
                .WithTaggedFlag("AHBBUSTIMEOUT - AHB Bus timeout interrupt", 11)
                .WithReservedBits(12, 20)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptEnableRegister.Define(this)
                .WithFlag(0, out ipCommandDoneEnabled, name: "IPCMDDONEEN - IP triggered Command Sequences Execution finished interrupt enable")
                .WithTaggedFlag("AHBCMDERREN - AHB triggered Command Sequences Error Detected interrupt enable", 1)
                .WithTaggedFlag("IPCMDGEEN - IP triggered Command Sequences Grant Timeout interrupt enable", 2)
                .WithTaggedFlag("AHBCMDGEEN - AHB triggered Command Sequences Grant Timeout interrupt enable", 3)
                .WithTaggedFlag("IPCMDERREN - IP triggered Command Sequences Error Detected interrupt enable", 4)
                .WithFlag(5, out ipRxWatermarkAvailableEnabled, name: "IPRXWAEN - IP RX FIFO watermark available interrupt enable")
                .WithFlag(6, out ipTxWatermarkEmptyEnabled, name: "IPTXWEEN - IP TX FIFO watermark empty interrupt enable")
                .WithReservedBits(7, 1)
                .WithTaggedFlag("SEQTIMEOUTEN - Sequence execution timeout interrupt enable", 8)
                .WithTaggedFlag("SCKSTOPBYRDEN - SCLK is stopped during command sequence because Async RX FIFO full interrupt enable", 9)
                .WithTaggedFlag("SCKSTOPBYWREN - SCLK is stopped during command sequence because Async TX FIFO empty interrupt enable", 10)
                .WithTaggedFlag("AHBBUSTIMEOUTEN - AHB Bus timeout interrupt enable", 11)
                .WithReservedBits(12, 20)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.IPRXFIFOStatusRegister.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "FILL - Fill level of IP RX FIFO", valueProviderCallback: _ => (uint)rxQueue.FillLevel)
                .WithReservedBits(8, 8)
                .WithTag("RDCNTR - Total Read Data Counter", 16, 16);

            Registers.IPRXFIFODataRegister0.DefineMany(this, FifoDepth, (register, idx) =>
            {
                var j = idx;
                register
                    .WithValueField(0, 32, FieldMode.Read, name: "RXDATA", valueProviderCallback: _ => 
                    {
                        totalReadCounter += 4;
                        return rxQueue.Read(4 * j);
                    });
            });

            Registers.IPTXFIFODataRegister0.DefineMany(this, FifoDepth, (register, idx) =>
            {
                var j = idx;
                register
                    .WithValueField(0, 32, FieldMode.Write, name: "TXDATA", writeCallback: (_, val) =>
                    {
                        // since the position is based on the registed id
                        // there is no possiblity of running out of the buffer
                        txQueue.Fill(position: 4 * j, data: new []
                        {
                            (byte)(val >> 24),
                            (byte)(val >> 16),
                            (byte)(val >> 8),
                            (byte)(val)
                        });
                        UpdateInterrupts();
                    });
            });

            Registers.FlashA1ControlRegister2.Define(this)
                .WithValueField(0, 4, out ahbReadSequenceIndex, name: "ARDSEQID - Sequence Index for AHB Read triggered Command in LUT")
                .WithReservedBits(4, 1)
                .WithValueField(5, 3, out ahbReadSequenceLength, name: "ARDSEQNUM - Sequence Number for AHB Read triggered Command in LUT")
                .WithValueField(8, 4, out ahbWriteSequenceIndex, name: "AWRSEQID - Sequence Index for AHB Write triggered Command")
                .WithReservedBits(12, 1)
                .WithValueField(13, 3, out ahbWriteSequenceLength, name: "AWRSEQNUM - Sequence Number for AHB Write triggered Command")
                .WithTag("AWRWRITE", 16, 12)
                .WithTag("AWRWAITUNIT", 28, 3)
                .WithTag("CLRINSTRPTR", 31, 1);
        }

        private bool TryPushData()
        {
            lock(txQueue)
            {
                if(!isInTransmit)
                {
                    // it's fine to just return without issuing a warning;
                    // the transmit process is multi-step and there might
                    // be different ordering of operations 
                    return false;
                }

                this.Log(LogLevel.Debug, "Trying to push data to the device");
                if(!TryGetDevice(out var device))
                {
                    this.Log(LogLevel.Warning, "Tried to push data to the device, but nothing is connected");
                    return false;
                }

                var dequeuedBytes = txQueue.Dequeue((int)dataSize.Value);
                if(dequeuedBytes.Length == 0)
                {
                    this.Log(LogLevel.Warning, "Tried to push data to the device, but there is nothing in the queue");
                    return false;
                }

                this.Log(LogLevel.Debug, "Pushing {0} bytes of data to the device", dequeuedBytes.Length);

                foreach(var b in dequeuedBytes)
                {
                    device.Transmit(b);
                }

                dataSize.Value -= (uint)dequeuedBytes.Length;
                UpdateInterrupts();

                if(dataSize.Value == 0)
                {
                    this.Log(LogLevel.Debug, "Programming finished");
                    isInTransmit = false;
                    if(commandsEngine.InProgress)
                    {
                        // execute the rest of commands
                        commandsEngine.Execute();
                    }
                }
                else
                {
                    this.Log(LogLevel.Debug, "There is {0} more bytes to send left", dataSize.Value);
                }

                return true;
            }
        }

        private bool TryGetDevice(out ISPIPeripheral device)
        {
            device = RegisteredPeripheral;
            if(device == null)
            {
                this.Log(LogLevel.Warning, "No device connected");
                return false;
            }

            return true;
        }

        private void TriggerCommand()
        {
            if(commandsEngine.InProgress)
            {
                this.Log(LogLevel.Warning, "Tried to trigger a command, but the previous one is still in progress");
                return;
            }

            // sequenceNumber's value in register is 1 less than the actual value
            commandsEngine.LoadCommands((uint)sequenceIndex.Value, (uint)sequenceNumber.Value + 1);
            commandsEngine.Execute();
        }

        private void UpdateInterrupts()
        {
            var flag = false;

            ipTxWatermarkEmpty.Value = txQueue.EmptyLevel >= (int)txWatermark.Value;
            ipRxWatermarkAvailable.Value = rxQueue.FillLevel >= (int)rxWatermark.Value;

            flag |= ipCommandDone.Value && ipCommandDoneEnabled.Value;
            flag |= ipTxWatermarkEmpty.Value && ipTxWatermarkEmptyEnabled.Value;
            flag |= ipRxWatermarkAvailable.Value && ipRxWatermarkAvailableEnabled.Value;

            this.Log(LogLevel.Debug, "Setting IRQ flag to {0}", flag);
            IRQ.Set(flag);
        }

        private IValueRegisterField sequenceIndex;
        private IValueRegisterField sequenceNumber;
        private IValueRegisterField dataSize;

        private IValueRegisterField ahbWriteSequenceIndex;
        private IValueRegisterField ahbWriteSequenceLength;

        private IValueRegisterField ahbReadSequenceIndex;
        private IValueRegisterField ahbReadSequenceLength;

        private IValueRegisterField serialFlashAddress;

        private IFlagRegisterField ipCommandDone;
        private IFlagRegisterField ipCommandDoneEnabled;

        private IFlagRegisterField ipRxWatermarkAvailable;
        private IFlagRegisterField ipRxWatermarkAvailableEnabled;

        private IFlagRegisterField ipTxWatermarkEmpty;
        private IFlagRegisterField ipTxWatermarkEmptyEnabled;

        private IValueRegisterField rxWatermark;
        private IValueRegisterField txWatermark;

        private bool isInTransmit;
        private uint totalReadCounter;

        private readonly CommandsEngine commandsEngine;
        private readonly RandomAccessQueue txQueue;
        private readonly RandomAccessQueue rxQueue;
        private readonly byte[] lutMemory;

        private const int NumberOfLUTs = 64;
        private const int SequenceLength = 8;
        private const int FifoDepth = 32;

        private class CommandsEngine
        {
            public CommandsEngine(IMXRT_FlexSPI parent)
            {
                this.parent = parent;
                descriptors = new List<IPCommandDescriptor>();
            }
            
            public void Reset()
            {
                currentCommand = 0;
                descriptors.Clear();
            }
            
            public void LoadCommands(uint index, uint number)
            {
                if(currentCommand != descriptors.Count)
                {
                    parent.Log(LogLevel.Warning, "Loading new commands, but there are some left");
                    descriptors.Clear();
                    currentCommand = 0;
                }
                
                for(var i = 0u; i < number; i++)
                {
                    foreach(var cmd in DecodeCommands(index + i))
                    {
                        descriptors.Add(cmd);
                    }
                }
            }

            public void Execute()
            {
                if(currentCommand == descriptors.Count)
                {
                    // there is nothing more to do
                    InProgress = false;
                    return;
                }

                if(!parent.TryGetDevice(out var device))
                {
                    return;
                }

                InProgress = true; 
                while(currentCommand < descriptors.Count)
                {
                    if(!HandleCommand(descriptors[currentCommand++], device))
                    {
                        // come commands (e.g. write) are multi-step
                        // and do not finish right away;
                        // we need to exit the execution loop and wait
                        // for them to complete before handling the next
                        // command
                        return;
                    }
                }
                InProgress = false;
            }
            
            public bool InProgress { get; private set; }

            private IEnumerable<IPCommandDescriptor> DecodeCommands(uint sequenceIndex)
            {
                var result = new List<IPCommandDescriptor>();
                var startingIndex = sequenceIndex * 0x10;
                
                parent.Log(LogLevel.Debug, "Decoding command at starting index: {0}", startingIndex);

                for(var i = 0; i < SequenceLength; i++)
                {
                    var currentIndex = startingIndex + (2 * i);
                    var instr = (short)((parent.lutMemory[currentIndex + 1] << 8) + parent.lutMemory[currentIndex]);
                    var cmd = DecodeCommand(instr);
                    result.Add(cmd);

                    if(cmd.Command == IPCommand.Stop)
                    {
                        break;
                    }
                }
                return result;
            }

            private IPCommandDescriptor DecodeCommand(short instr)
            {
                return new IPCommandDescriptor
                {
                    Command = (IPCommand)(instr >> 10),
                    NumberOfPaddings = (instr >> 8) & 0x3,
                    Operand = (byte)instr
                };
            }

            private byte[] ReadFromDevice(ISPIPeripheral device, int count)
            {
                var result = new byte[count];
                for(var i = 0; i < result.Length; i++)
                {
                    // send a dummy byte
                    result[i] = device.Transmit(0);
                }

                return result;
            }

            private bool HandleCommand(IPCommandDescriptor cmd, ISPIPeripheral device)
            {
                var result = true;
                parent.Log(LogLevel.Debug, "About to execute command {0}", cmd);

                switch(cmd.Command)
                {
                    case IPCommand.TransmitCommand_SDR:
                        device.Transmit(cmd.Operand);
                        break;

                    case IPCommand.ReceiveReadData_SDR:
                        {   
                            var dataCount = (parent.dataSize.Value == 0)
                                ? cmd.Operand
                                : parent.dataSize.Value;

                            var data = ReadFromDevice(device, (int)dataCount);
                            var count = parent.rxQueue.Enqueue(data);
                            if(count != data.Length)
                            {
                                parent.Log(LogLevel.Warning, "There is no more space left in the RX queue. {0} bytes were dropped", data.Length - count);
                            }
                        }
                        break;

                    case IPCommand.Stop:
                        {
                            device.FinishTransmission();
                            parent.ipCommandDone.Value = true;
                            parent.UpdateInterrupts();
                        }
                        break;


                    case IPCommand.TransmitProgrammingData_SDR:
                        {
                            parent.isInTransmit = true;
                            // note: parent operation breaks execution of the command
                            // waiting for the data to be written to FIFO
                            result = false;
                        }
                        break;

                    case IPCommand.TransmitRowAddress_SDR:
                        {
                            var a0 = (byte)(parent.serialFlashAddress.Value);
                            var a1 = (byte)(parent.serialFlashAddress.Value >> 8);
                            var a2 = (byte)(parent.serialFlashAddress.Value >> 16);
                            device.Transmit(a2);
                            device.Transmit(a1);
                            device.Transmit(a0);
                        }
                        break;

                    default:
                        parent.Log(LogLevel.Info, "Unsupported IP command: {0}", cmd);
                        break;
                }

                return result;
            }

            private int currentCommand;

            private readonly IMXRT_FlexSPI parent;
            private readonly List<IPCommandDescriptor> descriptors; 
        }

        private class RandomAccessQueue
        {
            public RandomAccessQueue(int size)
            {
                internalBuffer = new byte[size];
            }

            public void Reset()
            {
                FillLevel = 0;
                Array.Clear(internalBuffer, 0, internalBuffer.Length);
            }

            public uint Read(int position, int width = 4)
            {
                DebugHelper.Assert(width > 0 && width <= 4);

                if(position >= FillLevel)
                {
                    return 0;
                }
                return BitHelper.ToUInt32(internalBuffer, position, Math.Min(width, FillLevel - position), true);
            }

            public byte[] Dequeue(int maxCount)
            {
                var bytesToDequeue = Math.Min(FillLevel, maxCount);
                var result = new byte[bytesToDequeue];

                Array.Copy(internalBuffer, 0, result, 0, result.Length);

                var bytesLeft = FillLevel - bytesToDequeue;
                for(int i = 0; i < bytesLeft; i++)
                {
                    internalBuffer[i] = internalBuffer[i + bytesToDequeue];
                }

                FillLevel = bytesLeft;
                return result;
            }

            public int Enqueue(byte[] bs)
            {
                var counter = 0;
                foreach(var b in bs)
                {
                    if(FillLevel == internalBuffer.Length)
                    {
                        break;
                    }

                    internalBuffer[FillLevel++] = b;
                    counter++;
                }

                return counter;
            }

            public int Fill(IEnumerable<byte> data, bool reset = false, int position = -1)
            {
                var counter = 0;
                if(reset)
                {
                    FillLevel = 0;
                }

                var idx = (position >= 0)
                    ? position
                    : FillLevel;

                foreach(var d in data)
                {
                    if(idx >= internalBuffer.Length)
                    {
                        break;
                    }
                    internalBuffer[idx++] = d;
                    counter++;
                }

                FillLevel = Math.Max(FillLevel, idx);

                return counter;
            }

            public int FillLevel { get; private set; }
            public int EmptyLevel => internalBuffer.Length - FillLevel;

            private readonly byte[] internalBuffer;
        }

        private struct IPCommandDescriptor
        {
            public IPCommand Command;
            public int NumberOfPaddings;
            public byte Operand;

            public override string ToString()
            {
                return $"[IPCommandDescriptor: {Command} (0x{Command:X}), NumberOfPaddings: {NumberOfPaddings}, Operand: 0x{Operand:X}]";
            }
        }

        private enum IPCommand
        {
            TransmitCommand_SDR = 0x1, // CMD_SDR
            TransmitCommand_DDR = 0x21, // CMD_DDR

            TransmitRowAddress_SDR = 0x2, // RADDR_SDR
            TransmitRowAddress_DDR = 0x22, // RADDR_DDR

            TransmitColumnAddress_SDR = 0x3, // CADDR_SDR
            TransmitColumnAddress_DDR = 0x23, // CADDR_SDR

            TransmitModeBits1_SDR = 0x4, // MODE1_SDR
            TransmitModeBits1_DDR = 0x24, // MODE1_DDR

            TransmitModeBits2_SDR = 0x5, // MODE2_SDR
            TransmitModeBits2_DDR = 0x25, // MODE2_DDR

            TransmitModeBits4_SDR = 0x6, // MODE4_SDR
            TransmitModeBits4_DDR = 0x26, // MODE4_DDR

            TransmitModeBits8_SDR = 0x7, // MODE8_SDR
            TransmitModeBits8_DDR = 0x27, // MODE8_DDR

            TransmitProgrammingData_SDR = 0x8, // WRITE_SDR
            TransmitProgrammingData_DDR = 0x28, // WRITE_DDR

            ReceiveReadData_SDR = 0x9, // READ_SDR
            ReceiveReadData_DDR = 0x29, // READ_DDR

            ReceiveReadDataOrPreamble_SDR = 0xa, // LEARN_SDR
            ReceiveReadDataOrPreamble_DDR = 0x2a, // LEARN_DDR

            DataSize_SDR = 0xb, // DATASZ_SDR
            DataSize_DDR = 0x2b, // DATASZ_DDR

            Dummy_SDR = 0xc, // DUMMY_SDR
            Dummy_DDR = 0x2c, // DUMMY_DDR

            DummyRWDS_SDR = 0xd, // DUMMY_RWDS_SDR
            DummyRWDS_DDR = 0x2d, // DUMMY_RWDS_DDR

            JumpOnCS = 0x1F, // JMP_ON_CS
            Stop = 0x00, // STOP
        }

        private enum Registers : long
        {
            ModuleControlRegister0 = 0x0, // (MCR0) 32 RW FFFF_80C2h
            ModuleControlRegister1 = 0x4, // (MCR1) 32 RW FFFF_FFFFh
            ModuleControlRegister2 = 0x8, // (MCR2) 32 RW 2000_81F7h
            AHBBusControlRegister = 0xC, // (AHBCR) 32 RW 0000_0018h
            InterruptEnableRegister = 0x10, // (INTEN) 32 RW 0000_0000h
            InterruptRegister = 0x14, // (INTR) 32 RW 0000_0000h
            LUTKeyRegister = 0x18, //(LUTKEY) 32 RW 5AF0_5AF0h
            LUTControlRegister = 0x1C, //(LUTCR) 32 RW 0000_0002h
            AHBRXBuffer0ControlRegister0 = 0x20, //(AHBRXBUF0CR0) 32 RW 8000_0020h
            AHBRXBuffer1ControlRegister0 = 0x24, //(AHBRXBUF1CR0) 32 RW 8001_0020h
            AHBRXBuffer2ControlRegister0 = 0x28, //(AHBRXBUF2CR0) 32 RW 8002_0020h
            AHBRXBuffer3ControlRegister0 = 0x2C, //(AHBRXBUF3CR0) 32 RW 8003_0020h
            FlashA1ControlRegister0 = 0x60, //(FLSHA1CR0) 32 RW 0001_0000h
            FlashA2ControlRegister0 = 0x64, //(FLSHA2CR0) 32 RW 0001_0000h
            FlashB1ControlRegister0 = 0x68, //(FLSHB1CR0) 32 RW 0001_0000h
            FlashB2ControlRegister0 = 0x6C, //(FLSHB2CR0) 32 RW 0001_0000h
            FlashA1ControlRegister1 = 0x70, //(FLSHA1CR1) 32 RW 0000_0063h
            FlashA2ControlRegister1 = 0x74, //(FLSHA2CR1) 32 RW 0000_0063h
            FlashB1ControlRegister1 = 0x78, //(FLSHB1CR1) 32 RW 0000_0063h
            FlashB2ControlRegister1 = 0x7C, //(FLSHB2CR1) 32 RW 0000_0063h
            FlashA1ControlRegister2 = 0x80, //(FLSHA1CR2) 32 RW 0000_0000h
            FlashA2ControlRegister2 = 0x84, //(FLSHA2CR2) 32 RW 0000_0000h
            FlashB1ControlRegister2 = 0x88, //(FLSHB1CR2) 32 RW 0000_0000h
            FlashB2ControlRegister2 = 0x8C, //(FLSHB2CR2) 32 RW 0000_0000h
            FlashControlRegister4 = 0x94, //(FLSHCR4) 32 RW 0000_0000h
            IPControlRegister0 = 0xA0, //(IPCR0) 32 RW 0000_0000h
            IPControlRegister1 = 0xA4, //(IPCR1) 32 RW 0000_0000h
            IPCommandRegister = 0xB0, //(IPCMD) 32 RW 0000_0000h
            IPRXFIFOControlRegister = 0xB8, //(IPRXFCR) 32 RW 0000_0000h
            IPTXFIFOControlRegister = 0xBC, //(IPTXFCR) 32 RW 0000_0000h
            DLLAControlRegister0 = 0xC0, //(DLLACR) 32 RW 0000_0100h
            DLLBControlRegister0 = 0xC4, //(DLLBCR) 32 RW 0000_0100h
            StatusRegister0 = 0xE0, //(STS0) 32 RO 0000_0002h
            StatusRegister1 = 0xE4, //(STS1) 32 RO 0000_0000h
            StatusRegister2 = 0xE8, //(STS2) 32 RO 0100_0100h
            AHBSuspendStatusRegister = 0xEC, //(AHBSPNDSTS) 32 RO 0000_0000h
            IPRXFIFOStatusRegister = 0xF0, //(IPRXFSTS) 32 RO 0000_0000h
            IPTXFIFOStatusRegister = 0xF4, //(IPTXFSTS) 32 RO 0000_0000h
            IPRXFIFODataRegister0 = 0x100, // (RFDR0 - RFDR31) 32 RO 0000_0000h
            IPTXFIFODataRegister0 = 0x180,
            LUT0 = 0x200,
        }
    }
}
