//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System.Linq;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class UDMA : IDoubleWordPeripheral, IKnownSize
    {
        public UDMA(IMachine machine, int numberOfChannels = 32)
        {
            engine = new DmaEngine(machine.GetSystemBus(this));
            SystemBus = machine.GetSystemBus(this);
            channels = new Channel[numberOfChannels];
            IRQ = new GPIO();
            Reset();
        }

        public void Reset()
        {
            basePointer = 0;
            busErrorStatus = 0;
            for(var i = 0; i < channels.Length; ++i)
            {
                channels[i] = new Channel(this, i);
            }
        }

        public long Size
        {
            get
            {
                return 0x1000;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.ChannelControlBase:
                return basePointer;
            case Registers.AlternateChannelControlBase:
                return basePointer + 0x200; //According to Table 10-3 from cc2538 manual.
            case Registers.ErrorClear:
                return busErrorStatus;
            case Registers.WaitStatus:
                return ReadFromChannels(x => x.WaitingOnRequest);
            case Registers.SoftwareRequest:
                return 0; //although it's write only, contiki drivers read from this. It's safe to read 0.
            case Registers.UseBurstSet:
                return ReadFromChannels(x => x.UseBurst);
            case Registers.RequestMaskSet:
                return ReadFromChannels(x => x.RequestMask);
            case Registers.EnableSet:
                return ReadFromChannels(x => x.ChannelEnabled);
            case Registers.AlternateSet:
                return ReadFromChannels(x => x.UseAlternateControlData);
            case Registers.PrioritySet:
                return ReadFromChannels(x => x.HighPriority);
            case Registers.InterruptStatus:
                return ReadFromChannels(x => x.InterruptStatus);
            case Registers.PeripheralIdentification4:
                return peripheralIdentification[4];
            case Registers.PeripheralIdentification0:
            case Registers.PeripheralIdentification1:
            case Registers.PeripheralIdentification2:
            case Registers.PeripheralIdentification3:
                return peripheralIdentification[offset - (int)Registers.PeripheralIdentification0];
            case Registers.PrimeCellIdentification0:
            case Registers.PrimeCellIdentification1:
            case Registers.PrimeCellIdentification2:
            case Registers.PrimeCellIdentification3:
                return primeCellIdentification[offset - (int)Registers.PrimeCellIdentification0];
            default:
                this.LogUnhandledRead(offset);
                break;
            }
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.Config:
                //enable not implemented
                break;
            case Registers.ChannelControlBase:
                basePointer = value;
                break;
            case Registers.SoftwareRequest:
                ActionOnChannels(x => x.InitTransfer(), value);
                break;
            case Registers.UseBurstSet:
                ActionOnChannels(x => x.UseBurst = true, value);
                break;
            case Registers.UseBurstClear:
                ActionOnChannels(x => x.UseBurst = false, value);
                break;
            case Registers.RequestMaskSet:
                ActionOnChannels(x => x.RequestMask = true, value);
                break;
            case Registers.RequestMaskClear:
                ActionOnChannels(x => x.RequestMask = false, value);
                break;
            case Registers.EnableSet:
                ActionOnChannels(x => x.ChannelEnabled = true, value);
                break;
            case Registers.EnableClear:
                ActionOnChannels(x => x.ChannelEnabled = false, value);
                break;
            case Registers.AlternateSet:
                ActionOnChannels(x => x.UseAlternateControlData = true, value);
                break;
            case Registers.AlternateClear:
                ActionOnChannels(x => x.UseAlternateControlData = false, value);
                break;
            case Registers.PrioritySet:
                ActionOnChannels(x => x.HighPriority = true, value);
                break;
            case Registers.PriorityClear:
                ActionOnChannels(x => x.HighPriority = false, value);
                break;
            case Registers.ErrorClear:
                if(value > 0)
                {
                    busErrorStatus = 0;
                }
                break;
            case Registers.Assignment:
                ActionOnChannels((x,y) => x.SecondaryChannelAssignment = y, value);
                break;
            case Registers.InterruptStatus:
                ActionOnChannels(x => x.InterruptStatus = false, value);
                IRQ.Unset();
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void InitTransfer(int channel, bool isBurst)
        {
            if(channel > channels.Length)
            {
                this.Log(LogLevel.Warning, "Trying to issue a {0} transfer on non-existent channel {1}", isBurst ? "burst" : "single", channel);
                return;
            }
            channels[channel].UseBurst = isBurst;
            channels[channel].InitTransfer();
        }

        public GPIO IRQ { get; private set; }

        private void ActionOnChannels(Action<Channel, bool> action, uint selector)
        {
            var index = 0;
            foreach(var bit in BitHelper.GetBits(selector))
            {
                action(channels[index], bit);
                index++;
            }
        }

        private void ActionOnChannels(Action<Channel> action, uint selector)
        {
            foreach(var i in BitHelper.GetSetBits(selector))
            {
                action(channels[i]);
            }
        }

        private uint ReadFromChannels(Func<Channel, bool> reader)
        {
            var result = 0u;
            for(var i = 0; i < channels.Length; ++i)
            {
                if(reader(channels[i]))
                {
                    result |= 1u << i;
                }
            }
            return result;
        }

        private uint basePointer;
        private uint busErrorStatus;
        private readonly Channel[] channels;
        private readonly DmaEngine engine;
        private readonly IBusController SystemBus;

        private readonly uint[] peripheralIdentification = new uint[]{ 0x30, 0xB2, 0xB, 0x0, 0x4 };
        private readonly uint[] primeCellIdentification = new uint[]{ 0xD, 0xF0, 0x5, 0xB1 };

        private class Channel
        {
            public Channel(UDMA parent, int number)
            {
                this.parent = parent;
                channelNumber = number;
            }

            public void InitTransfer()
            {
                var dataSource = (ulong)((UseAlternateControlData ? parent.basePointer + 0x200 : parent.basePointer) + 0x10 * channelNumber);
                var controlStructure = new ControlStructure(parent.SystemBus.ReadBytes(dataSource, 0x10));
                var request = new Request(controlStructure.SourcePointer, controlStructure.DestinationPointer, (int)controlStructure.TransferSize,
                                  (TransferType)controlStructure.SourceSize, (TransferType)controlStructure.DestinationSize,
                                  controlStructure.SourceIncrement, controlStructure.DestinationIncrement,
                                  controlStructure.SourceIncrement != 0, controlStructure.DestinationIncrement != 0);
                parent.engine.IssueCopy(request);

                controlStructure.ControlWord &= ~0x3FF7u; //zero request type and length
                parent.SystemBus.WriteBytes(controlStructure.GetBytes(), dataSource, true);
                ChannelEnabled = false;
                InterruptStatus = true;
                parent.IRQ.Set();

            }

            public bool WaitingOnRequest{ get; private set; }
            public bool InterruptStatus{ get; set; }
            public bool SecondaryChannelAssignment{ get; set; }
            public bool HighPriority{ get; set; }
            public bool UseAlternateControlData{ get; set; }
            public bool ChannelEnabled{ get; set; }
            public bool UseBurst{ get; set; }
            public bool RequestMask{ get; set; }
            public uint ChannelMapping{ get; set; }
            private readonly UDMA parent;
            private readonly int channelNumber;

            private struct ControlStructure
            {
                public ControlStructure(byte[] data)
                {
                    ControlWord = BitConverter.ToUInt32(data, 8);
                    //Source/DestinationPointer point to the LAST byte to tranfser, oddly.
                    SourceEndPointer = BitConverter.ToUInt32(data, 0);
                    DestinationEndPointer = BitConverter.ToUInt32(data, 4);
                    Unused = BitConverter.ToUInt32(data, 0xC);
                }

                public byte[] GetBytes()
                {
                    return BitConverter.GetBytes(SourceEndPointer).Concat(BitConverter.GetBytes(DestinationEndPointer))
                        .Concat(BitConverter.GetBytes(ControlWord)).Concat(BitConverter.GetBytes(Unused)).ToArray();
                }

                public uint DestinationIncrement { get { return IncrementHelper(ControlWord >> 30); } } //bits 31-30
                public uint SourceIncrement { get { return IncrementHelper((ControlWord >> 26) & 0x3); } } //bits 27-26
                public uint DestinationSize { get { return (uint)Math.Pow(2, (ControlWord >> 28) & 0x3); } }
                public uint SourceSize { get { return (uint)Math.Pow(2, (ControlWord >> 24) & 0x3); } }
                public uint TransferSize { get { return ((ControlWord >> 4) & 0x3FF) + 1; } } //Bits 13-4 of control word
                public uint SourcePointer { get { return SourceIncrement != 0 ? SourceEndPointer - TransferSize + 1 : SourceEndPointer; } }
                public uint DestinationPointer{ get { return DestinationIncrement != 0 ? DestinationEndPointer - TransferSize + 1 : DestinationEndPointer; } }

                //burst mode and transfer mode not implemented
                public uint SourceEndPointer;
                public uint DestinationEndPointer;
                public uint ControlWord;
                public uint Unused;

                //Translate bits to actual increase value.
                private static uint IncrementHelper(uint value)
                {
                    switch(value)
                    {
                    case 3:
                        return 0;
                    case 2:
                        return 4;
                    case 1:
                        return 2;
                    case 0:
                        return 1;
                    default:
                        throw new ArgumentException("Unhandled increment value {0}.".FormatWith(value));
                    }
                }
            }
        }

        private enum Registers
        {
            Status = 0x0, //not used
            Config = 0x4, //1 - enable, not implemented
            ChannelControlBase = 0x8, //[31:10]
            AlternateChannelControlBase = 0xC, //not used
            WaitStatus = 0x10, //bit per channel, not used
            SoftwareRequest = 0x14, //bit per channel, cleared when request completed <- action
            UseBurstSet = 0x18, //bit per channel <- data
            UseBurstClear = 0x1C, //bit per channel <- data
            RequestMaskSet = 0x20, //bit per channel <- data
            RequestMaskClear = 0x24, //bit per channel <- data
            EnableSet = 0x28, //bit per channel
            EnableClear = 0x2C, //bit per channel
            AlternateSet = 0x30, //bit per channel
            AlternateClear = 0x34, //bit per channel
            PrioritySet = 0x38, //bit per channel
            PriorityClear = 0x3C, //bit per channel
            ErrorClear = 0x4C, //write 1 to clear error, read 1 on error
            Assignment = 0x500, //bit per channel, not used
            InterruptStatus = 0x504, //bit per channel, w1c
            PeripheralIdentification4 = 0xFD0, //sic! it's before PeripheralIdentification0
            PeripheralIdentification0 = 0xFE0,
            PeripheralIdentification1 = 0xFE4,
            PeripheralIdentification2 = 0xFE8,
            PeripheralIdentification3 = 0xFEC,
            PrimeCellIdentification0 = 0xFF0,
            PrimeCellIdentification1 = 0xFF4,
            PrimeCellIdentification2 = 0xFF8,
            PrimeCellIdentification3 = 0xFFC
        }
    }
}
