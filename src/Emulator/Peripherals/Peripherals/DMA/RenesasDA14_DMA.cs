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
    public class RenesasDA14_DMA : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize, IGPIOReceiver
    {
        public RenesasDA14_DMA(IMachine machine)
        {
            this.machine = machine;
            interruptsManager = new InterruptManager<Interrupt>(this, IRQ);
            channels = new Channel[ChannelCount];
            for(int i = 0; i < ChannelCount; i++)
            {
                channels[i] = new Channel(this, i);
            }
            peripheralSelect = new IValueRegisterField[ChannelCount / 2];
            RegistersCollection = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            Reset();
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
            if(number < 0 && number >= ChannelCount)
            {
                this.ErrorLog("Invalid channel number: {0}. Expected value between 0 and {1}", number, ChannelCount - 1);
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
                channels[(index * 2) + 1].DoTransfer();
            }
        }

        public long Size => 0x118;
        public DoubleWordRegisterCollection RegistersCollection { get; }
        public GPIO IRQ { get; } = new GPIO();

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.PeripheralsMapping, new DoubleWordRegister(this, 0x00000fff)
                    .WithValueField(0, 4, out peripheralSelect[0], name: "DMA01_SEL")
                    .WithValueField(4, 4, out peripheralSelect[1], name: "DMA23_SEL")
                    .WithValueField(8, 4, out peripheralSelect[2], name: "DMA45_SEL")
                    .WithReservedBits(ChannelCount * 2, 32 - ChannelCount * 2)
                },
                {(long)Registers.InterruptStatus, interruptsManager.GetMaskedInterruptFlagRegister<DoubleWordRegister>()},
                {(long)Registers.InterruptClear, interruptsManager.GetInterruptClearRegister<DoubleWordRegister>()},
                {(long)Registers.InterruptMask, interruptsManager.GetInterruptEnableRegister<DoubleWordRegister>()},
                {(long)Registers.SetInterruptMask, interruptsManager.GetInterruptEnableSetRegister<DoubleWordRegister>()},
                {(long)Registers.ResetInterruptMask, interruptsManager.GetInterruptEnableClearRegister<DoubleWordRegister>()},
            };

            for(long i = 0; i < ChannelCount; i++)
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

        private const int ChannelCount = 6;
        private readonly InterruptManager<Interrupt> interruptsManager;
        private readonly Channel[] channels;
        private readonly IMachine machine;
        private readonly IValueRegisterField[] peripheralSelect;

        private class Channel
        {
            public Channel(RenesasDA14_DMA parent, int channelNumber)
            {
                this.parent = parent;
                this.channelNumber = channelNumber;
                engine = new DmaEngine(parent.machine.GetSystemBus(parent));
            }

            public void DoTransfer()
            {
                parent.Log(LogLevel.Debug, "[Channel {0}] Starting transfer from 0x{1:X} to 0x{2:X}", channelNumber, sourceAddress.Value, destinationAddress.Value);
                Request getDescriptorData = new Request(sourceAddress.Value, destinationAddress.Value,
                    (int)transferLength.Value + 1, transferType, transferType, incrementSourceAddress.Value && !dmaInit.Value, 
                    incrementDestinationAddress.Value);
                engine.IssueCopy(getDescriptorData);
                itemsAlreadyTransferred.Value = transferLength.Value;
                if(interruptLength.Value <= transferLength.Value)
                {
                    parent.interruptsManager.SetInterrupt((Interrupt)channelNumber);
                }
                else
                {
                    parent.Log(LogLevel.Warning, "Interrupt length shouldn't be greater than transfer length ({0} > {1}). Not sending IRQ.", interruptLength.Value, transferLength.Value);
                }
                if(!(circularMode.Value && peripheralTriggered.Value))
                {
                    dmaEnabled.Value = false;
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
            private readonly RenesasDA14_DMA parent;
            private readonly DmaEngine engine;
        }

        private enum Registers
        {
            // Channel 0
            SourceAddress_0 = 0x0,
            DestinationAddress_0 = 0x4,
            InterruptLength_0 = 0x8,
            TransferLength_0 = 0xC,
            Control_0 = 0x10,
            IndexPointer_0 = 0x14,
            // Channel 1
            SourceAddress_1 = 0x20,
            DestinationAddress_1 = 0x24,
            InterruptLength_1 = 0x28,
            TransferLength_1 = 0x2C,
            Control_1 = 0x30,
            IndexPointer_1 = 0x34,
            // Channel 2
            SourceAddress_2 = 0x40,
            DestinationAddress_2 = 0x44,
            InterruptLength_2 = 0x48,
            TransferLength_2 = 0x4C,
            Control_2 = 0x50,
            IndexPointer_2 = 0x54,
            // Channel 3
            SourceAddress_3 = 0x60,
            DestinationAddress_3 = 0x64,
            InterruptLength_3 = 0x68,
            TransferLength_3 = 0x6C,
            Control_3 = 0x70,
            IndexPointer_3 = 0x74,
            // Channel 4
            SourceAddress_4 = 0x80,
            DestinationAddress_4 = 0x84,
            InterruptLength_4 = 0x88,
            TransferLength_4 = 0x8C,
            Control_4 = 0x90,
            IndexPointer_4 = 0x94,
            // Channel 5
            SourceAddress_5 = 0xA0,
            DestinationAddress_5 = 0xA4,
            InterruptLength_5 = 0xA8,
            TransferLength_5 = 0xAC,
            Control_5 = 0xB0,
            IndexPointer_5 = 0xB4,

            PeripheralsMapping = 0x100,
            InterruptStatus = 0x104,
            InterruptClear = 0x108,
            InterruptMask = 0x10C,
            SetInterruptMask = 0x110,
            ResetInterruptMask = 0x114,
        }

        private enum Interrupt
        {
            Channel0,
            Channel1,
            Channel2,
            Channel3,
            Channel4,
            Channel5
        }
    }
}
