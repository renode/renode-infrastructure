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
            foreach(var channel in channels)
            {
                channel.Reset();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint mask)
        {
            RegistersCollection.Write(offset, mask);
        }

        /*
         GPIO number is used to identify a peripheral that requested DMA transfer and its direction (rx/tx).
        */
        public void OnGPIO(int number, bool value)
        {
            var peripheralSource = number / 2;
            var oddChannel = number % 2;

            // See Table 528: DMA_REQ_MUX_REG.
            if(peripheralSource < 0 || peripheralSource >= 16)
            {
                this.ErrorLog("DMA request from unknown source: {0}. Allowed values are in the range 0-15", peripheralSource);
                return;
            }

            if(!value)
            {
                return;
            }
            // Documentation 35.21
            // DMA_REQ_MUX_REG - Select which combination of peripherals are mapped on the DMA channels.
            var index = Array.FindIndex(peripheralSelect, val => (int)val.Value == peripheralSource);
            if(index != -1)
            {
                var channelID = index * 2 + oddChannel;
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
                    .WithValueField(0, 32,
                        valueProviderCallback: (_) => channels[channelID].SourceAddress,
                        writeCallback: (_, val) => { channels[channelID].SourceAddress = val; },
                        name: $"DMA{i}_A_START")
                );
                registerMap.Add(offset + (long)Registers.DestinationAddress_0, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        valueProviderCallback: (_) => channels[channelID].DestinationAddress,
                        writeCallback: (_, val) => {channels[channelID].DestinationAddress = val; },
                        name: $"DMA{i}_B_START")
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
                    .WithEnumField(13, 2, out channels[i].burstMode, name: "BURST_MODE")
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

            public void Reset()
            {
                transferCompleted = false;
            }

            public void DoTransfer()
            {
                // Table 497: DMA0_IDX_REG
                // When the transfer is completed and circular mode is not set,
                // itemsAlreadyTransferred is automatically reset to 0 upon starting a new transfer.
                if(transferCompleted && !circularMode.Value)
                {
                    transferCompleted = false;
                    itemsAlreadyTransferred.Value = 0;
                }

                var bytesToTransfer = (int)(transferLength.Value + 1) * (int)transferType;
                if((ulong)bytesToTransfer == itemsAlreadyTransferred.Value)
                {
                    parent.Log(LogLevel.Noisy, "All requested data are already transfered. Skipping this transfer.");
                    return;
                }

                // In normal mode, we are instantly copying requested data size,
                // in peripheral trigger mode, peripheral should trigger DMA on each sample
                // unless burst mode is selected which requests a few samples at once.
                var transactionLength = peripheralTriggered.Value ? (int)transferType * GetBurstCount(burstMode.Value) : bytesToTransfer;
                Request getDescriptorData = new Request(sourceAddress, destinationAddress,
                    transactionLength, transferType, transferType, incrementSourceAddress.Value && !dmaInit.Value,
                    incrementDestinationAddress.Value);

                parent.Log(LogLevel.Debug, "[Channel {0}] Starting transfer from 0x{1:X} to 0x{2:X}. Copy length: 0x{3:X}; transferType = {4}, srcAddrIncr = {5} dstAddrIncr = {6}",
                           channelNumber,
                           sourceAddress,
                           destinationAddress,
                           transactionLength,
                           transferType,
                           (incrementSourceAddress.Value && !dmaInit.Value),
                           incrementDestinationAddress.Value);

                var response = engine.IssueCopy(getDescriptorData);
                itemsAlreadyTransferred.Value += (ulong)transactionLength;
                sourceAddress = (ulong)response.ReadAddress;
                destinationAddress = (ulong)response.WriteAddress;
                if((interruptLength.Value * (ulong)transferType) < itemsAlreadyTransferred.Value)
                {
                    parent.interruptsManager.SetInterrupt((Interrupt)channelNumber);
                }

                if((ulong)bytesToTransfer == itemsAlreadyTransferred.Value)
                {
                    // Keep internal information about transfer state
                    // to know if transfer continues or starts from begin.
                    // Used for tracking peripheral triggered requests.
                    transferCompleted = true;
                }

                if(peripheralTriggered.Value && transferCompleted)
                {
                    if(circularMode.Value)
                    {
                        CircularModeReset();
                        return;
                    }
                    this.parent.Log(LogLevel.Noisy, "Disabling DMA channel because all items were transferred and circular mode is off");
                    dmaEnabled.Value = false;
                }
            }

            public ulong SourceAddress
            {
                get
                {
                    return sourceAddress;
                }
                set
                {
                    setSourceAddress = value;
                    sourceAddress = value;
                }
            }

            public ulong DestinationAddress
            {
                get
                {
                    return destinationAddress;
                }
                set
                {
                    setDestinationAddress = value;
                    destinationAddress = value;
                }
            }

            public ulong sourceAddress;
            public ulong destinationAddress;

            public IFlagRegisterField dmaEnabled;
            public IValueRegisterField transferLength;
            public IValueRegisterField interruptLength;
            public IFlagRegisterField incrementSourceAddress;
            public IFlagRegisterField incrementDestinationAddress;
            public IValueRegisterField itemsAlreadyTransferred;
            public IFlagRegisterField peripheralTriggered;
            public IFlagRegisterField circularMode;
            public IFlagRegisterField dmaInit;
            public IEnumRegisterField<BurstMode> burstMode;
            public TransferType transferType;

            private void CircularModeReset()
            {
                sourceAddress = setSourceAddress;
                destinationAddress = setDestinationAddress;
                itemsAlreadyTransferred.Value = 0;
            }

            private int GetBurstCount(BurstMode mode)
            {
                switch(mode)
                {
                    case BurstMode.Disabled: return 1;
                    case BurstMode.Four: return 4;
                    case BurstMode.Eight: return 8;
                    default:
                        parent.WarningLog("Invalid selection of burst mode: {0}", mode);
                        return 0;
                }
            }

            // For use in circular mode
            private ulong setSourceAddress;
            private ulong setDestinationAddress;

            private bool transferCompleted;

            private readonly int channelNumber;
            private readonly RenesasDA_DMABase parent;
            private readonly DmaEngine engine;

            public enum BurstMode
            {
                Disabled = 0,
                Four = 1,
                Eight = 2,
                Reserved = 3
            }
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

