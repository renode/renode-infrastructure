//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.DMA
{
    public abstract class RenesasDA_DMABase : IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IGPIOReceiver
    {
        public RenesasDA_DMABase(IMachine machine, int channelCount, int peripheralSelectCount)
        {
            this.machine = machine;
            interruptsManager = new InterruptManager<Interrupt>(this, IRQ);
            this.channelCount = channelCount;
            channels = new Channel[channelCount];
            for(int i = 0; i < channelCount; i++)
            {
                channels[i] = new Channel(this, i);
            }
            RegistersCollection = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            peripheralSelect = new IValueRegisterField[peripheralSelectCount];
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            interruptsManager.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint mask)
        {
            RegistersCollection.Write(offset, mask);
        }

        public void OnGPIO(int number, bool value)
        {
            if(number < 0 && number >= channelCount)
            {
                this.ErrorLog("Invalid channel number: {0}. Expected value between 0 and {1}", number, channelCount - 1);
                return;
            }

            if(!value)
            {
                return;
            }
            // Documentation 35.21
            // DMA_REQ_MUX_REG - Select which combination of peripherals are mapped on the DMA channels.
            var index = Array.FindIndex(peripheralSelect, val => (int)val.Value == number);
            if(index != -1)
            {
                var channelID = MapPeripheralSelectToDMAChannel(index);
                if(!channels[channelID].dmaEnabled.Value)
                {
                    this.Log(LogLevel.Warning, "Channel {0} isn't enabled. Ignoring request", channelID);
                    return;
                }
                channels[channelID].DoTransfer();
            }
            else
            {
                this.Log(LogLevel.Warning, "No DMA channel is programmed to handle request 0x{0:X}. Ignoring request", number);
            }
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; } = new GPIO();

        protected abstract int MapPeripheralSelectToDMAChannel(int peripheralSelectIndex);

        protected readonly Channel[] channels;
        protected readonly InterruptManager<Interrupt> interruptsManager;
        protected readonly IValueRegisterField[] peripheralSelect;

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>();
            for(long i = 0; i < channelCount; i++)
            {
                long channelID = i;
                long offset = 0x20 * i;
                registerMap.Add(offset + (long)Registers.SourceAddress_0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out channels[i].sourceAddress, name: $"DMA{i}_A_START")
                );
                registerMap.Add(offset + (long)Registers.DestinationAddress_0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out channels[i].destinationAddress, name: $"DMA{i}_B_START")
                );
                registerMap.Add(offset + (long)Registers.InterruptLength_0, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out channels[i].interruptLength, name: $"DMA{i}_INT")
                    .WithReservedBits(16, 16)
                );
                registerMap.Add(offset + (long)Registers.TransferLength_0, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out channels[i].transferLength, name: $"DMA{i}_LEN")
                    .WithReservedBits(16, 16)
                );
                registerMap.Add(offset + (long)Registers.Control_0, new DoubleWordRegister(this, 0x00018000)
                    .WithFlag(0, out channels[i].dmaEnabled, name: $"DMA{i}_ON", writeCallback: (_, value) =>
                        {
                            if(value && !channels[channelID].peripheralTriggered.Value)
                            {
                                channels[channelID].itemsAlreadyTransferred.Value = 0;
                                channels[channelID].DoTransfer();
                            }
                        })
                    .WithValueField(1, 2, name: "BW", writeCallback: (_, value) =>
                      {
                          if(value == 3)
                          {
                              this.Log(LogLevel.Warning, "The bus width value cannot be set to {0}", value);
                              return;
                          }
                          channels[channelID].transferType = (TransferType)Math.Pow(2, value);
                      })
                    .WithFlag(3, out channels[i].peripheralTriggered, name: "DREQ_MODE")
                    .WithFlag(4, out channels[i].incrementDestinationAddress, name: "BINC")
                    .WithFlag(5, out channels[i].incrementSourceAddress, name: "AINC")
                    .WithFlag(6, out channels[i].circularMode, name: "CIRCULAR")
                    .WithTag("DMA_PRIO", 7, 3)
                    .WithTaggedFlag("DMA_IDLE", 10)
                    .WithFlag(11, out channels[i].dmaInit, name: "DMA_INIT")
                    .WithTaggedFlag("REQ_SENSE", 12)
                    .WithTag("BURST_MODE", 13, 2)
                    .WithTaggedFlag("BUS_ERROR_DETECT", 15)
                    .WithTaggedFlag("DMA_EXCLUSIVE_ACCESS", 16)
                    .WithReservedBits(17, 15)
                );
                registerMap.Add(offset + (long)Registers.IndexPointer_0, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out channels[i].itemsAlreadyTransferred, FieldMode.Read, name: $"DMA{i}_IDX")
                    .WithReservedBits(16, 16)
                );
            }

            return registerMap;
        }

        private readonly IMachine machine;
        private readonly int channelCount;

        protected class Channel
        {
            public Channel(RenesasDA_DMABase parent, int channelNumber)
            {
                this.parent = parent;
                this.channelNumber = channelNumber;
                engine = new DmaEngine(parent.machine.GetSystemBus(parent));
            }

            public void DoTransfer()
            {
                var bytesToTransfer = (int)(transferLength.Value + 1) * (int)transferType;
                if((ulong)bytesToTransfer == itemsAlreadyTransferred.Value)
                {
                    parent.Log(LogLevel.Noisy, "All requested data are already transfered. Skipping this transfer.");
                    return;
                }
                // In normal mode, we are instantly coping requested data size,
                // in peripheral trigger mode, peripheral should trigger DMA on each sample
                var transactionLength = peripheralTriggered.Value ? (int)transferType : bytesToTransfer;
                Request getDescriptorData = new Request(sourceAddress.Value, destinationAddress.Value,
                    transactionLength, transferType, transferType, incrementSourceAddress.Value && !dmaInit.Value,
                    incrementDestinationAddress.Value);

                parent.Log(LogLevel.Debug, "[Channel {0}] Starting transfer from 0x{1:X} to 0x{2:X}. Copy length: 0x{3:X}", channelNumber, sourceAddress.Value, destinationAddress.Value, transactionLength);

                var response = engine.IssueCopy(getDescriptorData);
                itemsAlreadyTransferred.Value += (ulong)transactionLength;
                sourceAddress.Value = (ulong)response.ReadAddress;
                destinationAddress.Value = (ulong)response.WriteAddress;
                if((interruptLength.Value * (ulong)transferType) < itemsAlreadyTransferred.Value)
                {
                    parent.interruptsManager.SetInterrupt((Interrupt)channelNumber);
                }

                if(peripheralTriggered.Value)
                {
                    if(((ulong)bytesToTransfer == itemsAlreadyTransferred.Value) && !circularMode.Value)
                    {
                        this.parent.Log(LogLevel.Noisy, "Disabling DMA channel because all items were transferred and circular mode is off");
                        dmaEnabled.Value = false;
                    }
                    if(((ulong)bytesToTransfer == itemsAlreadyTransferred.Value) && circularMode.Value)
                    {
                        itemsAlreadyTransferred.Value = 0;
                    }
                }
            }

            public IFlagRegisterField dmaEnabled;
            public IValueRegisterField sourceAddress;
            public IValueRegisterField destinationAddress;
            public IValueRegisterField transferLength;
            public IValueRegisterField interruptLength;
            public IFlagRegisterField incrementSourceAddress;
            public IFlagRegisterField incrementDestinationAddress;
            public IValueRegisterField itemsAlreadyTransferred;
            public IFlagRegisterField peripheralTriggered;
            public IFlagRegisterField circularMode;
            public IFlagRegisterField dmaInit;
            public TransferType transferType;

            private readonly int channelNumber;
            private readonly RenesasDA_DMABase parent;
            private readonly DmaEngine engine;
        }

        protected enum Interrupt
        {
            Channel0,
            Channel1,
            Channel2,
            Channel3,
            Channel4,
            Channel5,
            Channel6,
            Channel7,
            Channel8,
            Channel9,
            Channel10,
            Channel11,
            Channel12,
            Channel13,
            Channel14,
            Channel15
        }

        private enum Registers
        {
            SourceAddress_0 = 0x0,
            DestinationAddress_0 = 0x4,
            InterruptLength_0 = 0x8,
            TransferLength_0 = 0xC,
            Control_0 = 0x10,
            IndexPointer_0 = 0x14,
        }
    }
}

