//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Utilities;
using System.Threading;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class OmapDma : IDoubleWordPeripheral
    {
        public OmapDma ()
        {
            channels = new Channel[32];
            for(int i=0;i<32;i++)
            {
                channels[i] = new Channel();
                channels[i].InterruptControl = (1u<<13) | (1u<<10) | (1u<<9); 
            }
            IRQ = new GPIO();
        }

        public GPIO IRQ {get; private set;}

        #region IDoubleWordPeripheral implementation
        public uint ReadDoubleWord (long offset)
        {
            uint index;
            
            if( (offset >= (long)InternalRegister.IRQStatusLinej) && (offset < (long)InternalRegister.IRQEnableLinej) )
            {
                index = getIndex(offset,(uint)InternalRegister.IRQStatusLinej);
                return IRQStatus[index];
            }
            if( (offset >= (long)InternalRegister.IRQEnableLinej) && (offset < (long)InternalRegister.SystemStatus) )
            {
                index = getIndex(offset,(uint)InternalRegister.IRQEnableLinej);
                return IRQEnable[index];
            }
            
            if(offset >= (long)InternalRegister.ChannelsRegisters)
            {
                index = getChannelIndex(offset);
                uint channelOffset = getChannelOffset(offset, index);
                
                switch ((Channel.Offset)channelOffset)
                {
                case Channel.Offset.Control:
                    return channels[index].Control;
                case Channel.Offset.CurrentActiveDescriptor:
                    return channels[index].CurrentActiveDescriptor;
                case Channel.Offset.CurrentTransferedElementNumber:
                    return channels[index].CurrentTransferedElementNumber;
                case Channel.Offset.CurrentTransferedFrameNumber:
                    return channels[index].CurrentTransferedFrameNumber;
                case Channel.Offset.DestinationAddress:
                    return channels[index].DestinationAddress;
                case Channel.Offset.DestinationAddressValue:
                    return channels[index].DestinationAddressValue;
                case Channel.Offset.DestinationElementIndex:
                    return channels[index].DestinationElementIndex;
                case Channel.Offset.DestinationFrameIndex:
                    return channels[index].DestinationFrameIndex;
                case Channel.Offset.ElementNumber:
                    return channels[index].ElementNumber;
                case Channel.Offset.FrameNumber:
                    return channels[index].FrameNumber;
                case Channel.Offset.InterruptControl:
                    return channels[index].InterruptControl;
                case Channel.Offset.LinkControl:
                    return channels[index].LinkControl;
                case Channel.Offset.LinkListParameters:
                    return channels[index].LinkListParameters;
                case Channel.Offset.NextDescriptorPointer:
                    return channels[index].NextDescriptorPointer;
                case Channel.Offset.SourceAddress:
                    return channels[index].SourceAddress;
                case Channel.Offset.SourceDestinationParameters:
                    return channels[index].SourceDestinationParameters;
                case Channel.Offset.SourceElementIndex:
                    return channels[index].SourceElementIndex;
                case Channel.Offset.SourceFrameIndex:
                    return channels[index].SourceFrameIndex;
                case Channel.Offset.SourceStartAddress:
                    return channels[index].SourceStartAddress;
                case Channel.Offset.StatusRegister:
                    return channels[index].StatusRegister;
                }
            }
            
            switch ((InternalRegister)offset) 
            {
            case InternalRegister.Revision:
                return revision;
            case InternalRegister.SystemStatus:
                return systemStatus;  
            case InternalRegister.SystemConfiguration:
                return systemConfiguration;
            case InternalRegister.Capabilities0:
                return capabilities0;
            case InternalRegister.Capabilities2:
                return capabilities2;    
            case InternalRegister.Capabilities3:
                return capabilities3;
            case InternalRegister.Capabilities4:
                return capabilities4;    
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord (long offset, uint value)
        {
            uint index;
            if(value != 0)
            {
                this.Log(LogLevel.Info,"dummy");
            }
            
            
            if( (offset >= (long)InternalRegister.IRQStatusLinej) && (offset < (long)InternalRegister.IRQEnableLinej) )
            {
                index = getIndex(offset,(uint)InternalRegister.IRQStatusLinej);
                IRQStatus[index] = value;
            }
            if( (offset >= (long)InternalRegister.IRQEnableLinej) && (offset < (long)InternalRegister.SystemConfiguration) )
            {
                index = getIndex(offset,(uint)InternalRegister.IRQEnableLinej);
                IRQEnable[index] = value;
            }
            
            if(offset >= (long)InternalRegister.ChannelsRegisters)
            {
                index = getChannelIndex(offset);
                uint channelOffset = getChannelOffset(offset, index);
                
                switch ((Channel.Offset)channelOffset)
                {
                case Channel.Offset.Control:
                    channels[index].Control = value;
                    break;
                case Channel.Offset.CurrentActiveDescriptor:
                    channels[index].CurrentActiveDescriptor = value;
                    break;
                case Channel.Offset.CurrentTransferedElementNumber:
                    channels[index].CurrentTransferedElementNumber = value;
                    break;
                case Channel.Offset.CurrentTransferedFrameNumber:
                    channels[index].CurrentTransferedFrameNumber = value;
                    break;
                case Channel.Offset.DestinationAddress:
                    channels[index].DestinationAddress = value;
                    break;
                case Channel.Offset.DestinationAddressValue:
                    channels[index].DestinationAddressValue = value;
                    break;
                case Channel.Offset.DestinationElementIndex:
                    channels[index].DestinationElementIndex = value;
                    break;
                case Channel.Offset.DestinationFrameIndex:
                    channels[index].DestinationFrameIndex = value;
                    break;
                case Channel.Offset.ElementNumber:
                    channels[index].ElementNumber = value;
                    break;
                case Channel.Offset.FrameNumber:
                    channels[index].FrameNumber = value;
                    break;
                case Channel.Offset.InterruptControl:
                    channels[index].InterruptControl = value;
                    break;
                case Channel.Offset.LinkControl:
                    channels[index].LinkControl = value;
                    break;
                case Channel.Offset.LinkListParameters:
                    channels[index].LinkListParameters = value;
                    break;
                case Channel.Offset.NextDescriptorPointer:
                    channels[index].NextDescriptorPointer = value;
                    break;
                case Channel.Offset.SourceAddress:
                    channels[index].SourceAddress = value;
                    break;
                case Channel.Offset.SourceDestinationParameters:
                    channels[index].SourceDestinationParameters = value;
                    break;
                case Channel.Offset.SourceElementIndex:
                    channels[index].SourceElementIndex = value;
                    break;
                case Channel.Offset.SourceFrameIndex:
                    channels[index].SourceFrameIndex = value;
                    break;
                case Channel.Offset.SourceStartAddress:
                    channels[index].SourceStartAddress = value;
                    break;
                case Channel.Offset.StatusRegister:
                    channels[index].StatusRegister = value;
                    break;
                }
            }
            
            switch ((InternalRegister) offset)
            {
            case InternalRegister.SystemConfiguration:
                systemConfiguration=value;
                break;
            case InternalRegister.Capabilities0:
                capabilities0 = value;
                break;
            case InternalRegister.Capabilities4:
                capabilities4 = value;
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }
        #endregion

        public void Reset ()
        {
            throw new NotImplementedException ();
        }
  
        private uint getIndex(long offset, uint regBaseAddress)
        {
            return (uint)((offset - regBaseAddress)/0x04u);
        }
        
        
        private uint getChannelIndex(long offset)
        {
            return (uint)((offset - (uint)InternalRegister.ChannelsRegisters)/(uint)InternalRegister.ChannelRegisterLength);
        }
        
        private uint getChannelOffset(long offset, uint index)
        {
            return (uint)(offset - (uint)InternalRegister.ChannelsRegisters - 0x60u * index);
        }
        
        private uint[] IRQStatus = new uint[4];
        private uint[] IRQEnable = new uint[4];
        private struct Channel
        {
            public uint Control;
            public uint LinkControl;
            public uint InterruptControl; //TODO: init (1u<<13) | (1u<<10) | (1u<<9)
            public uint StatusRegister;
            public uint SourceDestinationParameters;
            public uint ElementNumber;
            public uint FrameNumber;
            public uint SourceStartAddress;
            public uint DestinationAddress;
            public uint SourceElementIndex;
            public uint SourceFrameIndex;
            public uint DestinationElementIndex;
            public uint DestinationFrameIndex;
            public uint SourceAddress;
            public uint DestinationAddressValue;
            public uint CurrentTransferedElementNumber;
            public uint CurrentTransferedFrameNumber;
            public uint LinkListParameters;
            public uint NextDescriptorPointer;
            public uint CurrentActiveDescriptor;
            
            public enum Offset:uint//register offsets in single channel register set
            {
                Control = 0x00,
                LinkControl = 0x04,
                InterruptControl = 0x08, //TODO: init (1u<<13) | (1u<<10) | (1u<<9)
                StatusRegister = 0x0C,
                SourceDestinationParameters = 0x10,
                ElementNumber = 0x14,
                FrameNumber = 0x18,
                SourceStartAddress = 0x1C,
                DestinationAddress = 0x20,
                SourceElementIndex = 0x24,
                SourceFrameIndex = 0x28,
                DestinationElementIndex = 0x2C,
                DestinationFrameIndex = 0x30,
                SourceAddress = 0x34,
                DestinationAddressValue = 0x38,
                CurrentTransferedElementNumber = 0x3C,
                CurrentTransferedFrameNumber = 0x40,
                LinkListParameters = 0x50,
                NextDescriptorPointer = 0x54,
                CurrentActiveDescriptor = 0x5C
            }
        }
        
        
        private Channel[] channels; 
        private uint capabilities0 = (1u<<20)|(1u<<19)|(1u<<18);
        private uint capabilities4 = 0x5dfe;
        private uint systemConfiguration = 0;
                
        // RO
        private const uint revision = 0;//Revision 0.0 (Highter revision numbers reserved for future use)
        private const uint systemStatus = 1;//Reset done 
        private const uint capabilities2 = 0x1f;
        private const uint capabilities3 = 0xf3;
        
        private enum InternalRegister:uint
        {
            Revision = 0x00,
            IRQStatusLinej = 0x08,//j = 0 .. 3 (each line register is 0x04 long)
            IRQEnableLinej = 0x18,//j = 0 .. 3 (each line register is 0x04 long)
            SystemStatus = 0x28,
            SystemConfiguration = 0x2c,
            Capabilities0 = 0x64,
            Capabilities2 = 0x6c,
            Capabilities3 = 0x70,
            Capabilities4 = 0x78,
            ChannelsRegisters = 0x80,
            ChannelRegisterLength = 0x60
        }        
    }
}

