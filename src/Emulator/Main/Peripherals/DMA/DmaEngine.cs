//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.DMA
{
    public sealed class DmaEngine
    {
        public DmaEngine(IBusController systemBus)
        {
            sysbus = systemBus;
        }

        public Response IssueCopy(Request request)
        {
            var response = new Response
            {
                ReadAddress = request.Source.Address,
                WriteAddress = request.Destination.Address,
            };

            // some sanity checks
            if((request.Size % (int)request.ReadTransferType) != 0 || (request.Size % (int)request.WriteTransferType) != 0)
            {
                throw new ArgumentException("Request size is not aligned properly to given read or write transfer type (or both).");
            }

            var buffer = new byte[request.Size];
            IBusRegistered<IBusPeripheral> whatIsAt;
            if(!request.Source.Address.HasValue)
            {
                Array.Copy(request.Source.Array, request.Source.StartIndex.Value, buffer, 0, request.Size);
            }
            else
            {
                var sourceAddress = request.Source.Address.Value;
                whatIsAt = sysbus.WhatIsAt(sourceAddress);
                //allow ReadBytes if the read memory is without gaps
                if((whatIsAt == null || whatIsAt.Peripheral is MappedMemory) && (int)request.ReadTransferType == request.SourceIncrementStep)
                {
                    if(request.IncrementReadAddress)
                    {
                        sysbus.ReadBytes(sourceAddress, request.Size, buffer, 0);
                        response.ReadAddress += (ulong)request.Size;
                    }
                    else
                    {
                        sysbus.ReadBytes(sourceAddress, (int)request.ReadTransferType, buffer, 0);
                    }
                }
                else if(whatIsAt != null)
                {
                    var transferred = 0;
                    var offset = 0UL;
                    while(transferred < request.Size)
                    {
                        var readAddress = sourceAddress + offset;
                        switch(request.ReadTransferType)
                        {
                        case TransferType.Byte:
                            buffer[transferred] = sysbus.ReadByte(readAddress);
                            break;
                        case TransferType.Word:
                            BitConverter.GetBytes(sysbus.ReadWord(readAddress)).CopyTo(buffer, transferred);
                            break;
                        case TransferType.DoubleWord:
                            BitConverter.GetBytes(sysbus.ReadDoubleWord(readAddress)).CopyTo(buffer, transferred);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                        }
                        transferred += (int)request.ReadTransferType;
                        if(request.IncrementReadAddress)
                        {
                            offset += request.SourceIncrementStep;
                            response.ReadAddress += request.SourceIncrementStep;
                        }
                    }
                }
            }

            if(!request.Destination.Address.HasValue)
            {
                Array.Copy(buffer, 0, request.Destination.Array, request.Destination.StartIndex.Value, request.Size);
            }
            else
            {
                var destinationAddress = request.Destination.Address.Value;
                whatIsAt = sysbus.WhatIsAt(destinationAddress);
                if((whatIsAt == null || whatIsAt.Peripheral is MappedMemory) && (int)request.WriteTransferType == request.DestinationIncrementStep)
                {
                    if(request.IncrementWriteAddress)
                    {
                        sysbus.WriteBytes(buffer, destinationAddress);
                        response.WriteAddress += (ulong)request.Size;
                    }
                    else
                    {
                        // if the place to write is memory and we're not incrementing address, effectively only the last byte is written
                        sysbus.WriteByte(destinationAddress, buffer[buffer.Length - 1]);
                    }
                }
                else
                {
                    var transferred = 0;
                    var offset = 0UL;
                    while(transferred < request.Size)
                    {
                        switch(request.WriteTransferType)
                        {
                        case TransferType.Byte:
                            sysbus.WriteByte(destinationAddress + offset, buffer[transferred]);
                            break;
                        case TransferType.Word:
                            sysbus.WriteWord(destinationAddress + offset, BitConverter.ToUInt16(buffer, transferred));
                            break;
                        case TransferType.DoubleWord:
                            sysbus.WriteDoubleWord(destinationAddress + offset, BitConverter.ToUInt32(buffer, transferred));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                        }
                        transferred += (int)request.WriteTransferType;
                        if(request.IncrementWriteAddress)
                        {
                            offset += request.DestinationIncrementStep;
                            response.WriteAddress += request.DestinationIncrementStep;
                        }
                    }
                }
            }

            return response;
        }

        private readonly IBusController sysbus;
    }
}

