//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.DMA
{
    [AllowedTranslations(AllowedTranslation.QuadWordToDoubleWord)]
    public class MPFS_PDMA : IKnownSize, IDoubleWordPeripheral, INumberedGPIOOutput
    {
        public MPFS_PDMA(IMachine machine)
        {
            this.machine = machine;
            dmaEngine = new DmaEngine(this.machine.GetSystemBus(this));
            channels = new Channel[ChannelCount];

            var irqCounter = 0;
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < ChannelCount; ++i)
            {
                channels[i] = new Channel(this, i);
                innerConnections[irqCounter++] = channels[i].DoneInterrupt;
                innerConnections[irqCounter++] = channels[i].ErrorInterrupt;
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
            Reset();
        }

        public void Reset()
        {
            for(var i = 0; i < ChannelCount; ++i)
            {
                channels[i].Reset();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            var channelNumber = offset / ShiftBetweenChannels;
            if(channelNumber >= ChannelCount)
            {
                this.Log(LogLevel.Error, "Trying to read from nonexistent channel");
                return 0;
            }
            return channels[channelNumber].ReadDoubleWord(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            var channelNumber = offset / ShiftBetweenChannels;
            if(channelNumber >= ChannelCount)
            {
                this.Log(LogLevel.Error, "Trying to write to nonexistent channel");
                return;
            }
            channels[channelNumber].WriteDoubleWord(offset, value);
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }
        public long Size => 0x4000;

        private readonly IMachine machine;
        private readonly DmaEngine dmaEngine;
        private readonly Channel[] channels;

        private const int ChannelCount = 4;
        private const int ShiftBetweenChannels = 0x1000;

        private class Channel : IDoubleWordPeripheral
        {
            public Channel(MPFS_PDMA parent, int number)
            {
                this.parent = parent;
                channelNumber = number;
                DoneInterrupt = new GPIO();
                ErrorInterrupt = new GPIO();

                var registersMap = new Dictionary<long, DoubleWordRegister>();
                registersMap.Add(
                    (long)ChannelRegisters.Control + ShiftBetweenChannels * channelNumber,
                    new DoubleWordRegister(this)
                        .WithFlag(0,
                            writeCallback: (_, val) =>
                            {
                                if(!val && !isRun)
                                {
                                    isClaimed = false;
                                }
                                if(val && !isRun)
                                {
                                    if(!isClaimed)
                                    {
                                        nextBytesLow.Value = 0;
                                        nextBytesHigh.Value = 0;
                                        nextSourceLow.Value = 0;
                                        nextSourceHigh.Value = 0;
                                        nextDestinationLow.Value = 0;
                                        nextDestinationHigh.Value = 0;
                                    }
                                    isClaimed = true;
                                }
                            },
                            valueProviderCallback: _ =>
                            {
                                return isClaimed;
                            }, name: "claim")
                        .WithFlag(1,
                            writeCallback: (_, val) =>
                            {
                                if(val)
                                {
                                    isRun = true;
                                    do
                                    {
                                        InitTransfer();
                                    } while(repeat.Value);
                                }
                            },
                            valueProviderCallback: _ =>
                            {
                                return isRun;
                            }, name: "run")
                        .WithReservedBits(2, 12)
                        .WithFlag(14, out doneInterruptEnabled,
                            writeCallback: (_, val) =>
                            {
                                if(!val)
                                {
                                    DoneInterrupt.Unset();
                                }
                            }, name: "doneIE")
                        .WithTag("errorIE", 15, 1)
                        .WithReservedBits(16, 14)
                        .WithFlag(30, out isDone, name: "done")
                        .WithTag("error", 31, 1)
                );

                // NEXT registers
                nextConfigRegister = new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithFlag(2, out repeat, name: "repeat")
                    .WithTag("order", 3, 1)
                    .WithReservedBits(4, 20)
                    .WithValueField(24, 4, out wsize, name: "wsize")
                    .WithValueField(28, 4, out rsize, name: "rsize");
                registersMap.Add(
                    (long)ChannelRegisters.NextConfig + ShiftBetweenChannels * channelNumber,
                    nextConfigRegister
                );
                registersMap.Add(
                    (long)ChannelRegisters.NextBytesLow + ShiftBetweenChannels * channelNumber,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out nextBytesLow)
                );
                registersMap.Add(
                    (long)ChannelRegisters.NextBytesHigh + ShiftBetweenChannels * channelNumber,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out nextBytesHigh)
                );
                registersMap.Add(
                    (long)ChannelRegisters.NextDestinationLow + ShiftBetweenChannels * channelNumber,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out nextDestinationLow)
                );
                registersMap.Add(
                    (long)ChannelRegisters.NextDestinationHigh + ShiftBetweenChannels * channelNumber,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out nextDestinationHigh)
                );
                registersMap.Add(
                    (long)ChannelRegisters.NextSourceLow + ShiftBetweenChannels * channelNumber,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out nextSourceLow)
                );
                registersMap.Add(
                    (long)ChannelRegisters.NextSourceHigh + ShiftBetweenChannels * channelNumber,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out nextSourceHigh)
                );

                // EXEC registers
                execConfig = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read);
                registersMap.Add(
                    (long)ChannelRegisters.ExecConfig + ShiftBetweenChannels * channelNumber,
                    execConfig
                );
                execBytesLow = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read);
                registersMap.Add(
                    (long)ChannelRegisters.ExecBytesLow + ShiftBetweenChannels * channelNumber,
                    execBytesLow
                );
                execBytesHigh = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read);
                registersMap.Add(
                    (long)ChannelRegisters.ExecBytesHigh + ShiftBetweenChannels * channelNumber,
                    execBytesHigh
                );
                execDestinationLow = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read);
                registersMap.Add(
                    (long)ChannelRegisters.ExecDestinationLow + ShiftBetweenChannels * channelNumber,
                    execDestinationLow
                );
                execDestinationHigh = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read);
                registersMap.Add(
                    (long)ChannelRegisters.ExecDestinationHigh + ShiftBetweenChannels * channelNumber,
                    execDestinationHigh
                );
                execSourceLow = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read);
                registersMap.Add(
                    (long)ChannelRegisters.ExecSourceLow + ShiftBetweenChannels * channelNumber,
                    execSourceLow
                );
                execSourceHigh = new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read);
                registersMap.Add(
                    (long)ChannelRegisters.ExecSourceHigh + ShiftBetweenChannels * channelNumber,
                    execSourceHigh
                );
                registers = new DoubleWordRegisterCollection(this, registersMap);
            }

            public uint ReadDoubleWord(long offset)
            {
                return registers.Read(offset);
            }
            
            public void WriteDoubleWord(long offset, uint value)
            {
                registers.Write(offset, value);
            }
            
            public void Reset()
            {
                registers.Reset();
                DoneInterrupt.Unset();
                ErrorInterrupt.Unset();
                isClaimed = false;
                isRun = false;
            }

            public GPIO DoneInterrupt { get; private set; }
            public GPIO ErrorInterrupt { get; private set; }

            private void InitTransfer()
            {
                execConfig.Write(0, nextConfigRegister.Value);
                execBytesLow.Write(0, (uint)nextBytesLow.Value);
                execBytesHigh.Write(0, (uint)nextBytesHigh.Value);
                execSourceLow.Write(0, (uint)nextSourceLow.Value);
                execSourceHigh.Write(0, (uint)nextSourceHigh.Value);
                execDestinationLow.Write(0, (uint)nextDestinationLow.Value);
                execDestinationHigh.Write(0, (uint)nextDestinationHigh.Value);

                ulong sourceAddress = nextSourceHigh.Value;
                sourceAddress = sourceAddress << 32;
                sourceAddress = sourceAddress + nextSourceLow.Value;

                ulong destinationAddress = nextDestinationHigh.Value;
                destinationAddress = destinationAddress << 32;
                destinationAddress = destinationAddress + nextDestinationLow.Value;

                ulong size = nextBytesHigh.Value;
                size = size << 32;
                size = size + nextBytesLow.Value;

                parent.machine.LocalTimeSource.ExecuteInNearestSyncedState(_ =>
                {
                    IssueCopy(sourceAddress, destinationAddress, size);
                    FinishTransfer(sourceAddress, destinationAddress, size);
                });
            }

            private void IssueCopy(ulong sourceAddress, ulong destinationAddress, ulong size)
            {
                var dataLeft = size;
                while(dataLeft > 0)
                {
                    var partSize = int.MaxValue;
                    if(dataLeft <= int.MaxValue)
                    {
                        partSize = (int)dataLeft;
                    }

                    var request = new Request(
                        sourceAddress,
                        destinationAddress,
                        partSize,
                        GetTransactionSize(TransactionDirection.Read, rsize.Value),
                        GetTransactionSize(TransactionDirection.Write, wsize.Value)
                    );
                    parent.dmaEngine.IssueCopy(request);
                    sourceAddress += (ulong)partSize;
                    destinationAddress += (ulong)partSize;
                    dataLeft -= (ulong)partSize;
                }
            }

            private void FinishTransfer(ulong sourceAddress, ulong destinationAddress, ulong size)
            {
                execBytesLow.Write(0, 0);
                execBytesHigh.Write(0, 0);
                var execSource = sourceAddress + size;
                execSourceLow.Write(0, (uint)execSource);
                execSourceHigh.Write(0, (uint)(execSource >> 32));
                var execDestination = destinationAddress + size;
                execDestinationLow.Write(0, (uint)execDestination);
                execDestinationHigh.Write(0, (uint)(execDestination >> 32));

                isClaimed = false;
                isDone.Value = true;
                isRun = false;
                if(doneInterruptEnabled.Value)
                {
                    DoneInterrupt.Set();
                }
            }

            private TransferType GetTransactionSize(TransactionDirection direction, ulong val)
            {
                TransferType type;
                switch(val)
                {
                    case 0:
                        type = TransferType.Byte;
                        break;
                    case 1:
                        type = TransferType.Word;
                        break;
                    case 2:
                        type = TransferType.DoubleWord;
                        break;
                    default:
                        type = TransferType.DoubleWord;
                        this.Log(LogLevel.Warning, "{0} transaction size has been truncated to 4 bytes.", direction);
                        break;
                }
                return type;
            }

            private DoubleWordRegister nextConfigRegister;
            private DoubleWordRegister execConfig;
            private DoubleWordRegister execBytesLow;
            private DoubleWordRegister execBytesHigh;
            private DoubleWordRegister execDestinationLow;
            private DoubleWordRegister execDestinationHigh;
            private DoubleWordRegister execSourceLow;
            private DoubleWordRegister execSourceHigh;
            private bool isClaimed;
            private bool isRun;
            private IFlagRegisterField isDone;
            private IValueRegisterField wsize;
            private IValueRegisterField rsize;
            private IFlagRegisterField repeat;
            private IValueRegisterField nextBytesLow;
            private IValueRegisterField nextBytesHigh;
            private IValueRegisterField nextDestinationLow;
            private IValueRegisterField nextDestinationHigh;
            private IValueRegisterField nextSourceLow;
            private IValueRegisterField nextSourceHigh;

            private readonly IFlagRegisterField doneInterruptEnabled;
            private readonly DoubleWordRegisterCollection registers;
            private readonly MPFS_PDMA parent;
            private readonly int channelNumber;

            private enum TransactionDirection
            {
                Read,
                Write
            }

            private enum ChannelRegisters
            {
                Control = 0x000,
                NextConfig = 0x004,
                NextBytesLow = 0x008,
                NextBytesHigh = 0x00C,
                NextDestinationLow = 0x010,
                NextDestinationHigh = 0x014,
                NextSourceLow = 0x018,
                NextSourceHigh = 0x01C,
                ExecConfig = 0x104,
                ExecBytesLow = 0x108,
                ExecBytesHigh = 0x10C,
                ExecDestinationLow = 0x110,
                ExecDestinationHigh = 0x114,
                ExecSourceLow = 0x118,
                ExecSourceHigh = 0x11C,
            }
        }
    }
}