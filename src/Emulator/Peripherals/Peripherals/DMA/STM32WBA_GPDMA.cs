//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class STM32WBA55_GPDMA : IDoubleWordPeripheral, IKnownSize, IGPIOReceiver, INumberedGPIOOutput
    {
        public STM32WBA55_GPDMA(IMachine machine, int numberOfChannels)
        {
            this.machine = machine;
            engine = new DmaEngine(machine.GetSystemBus(this));
            channels = new Channel[numberOfChannels];
            var innerConnections = new Dictionary<int, IGPIO>();

            for(var i = 0; i < channels.Length; ++i)
            {
                var gpio = new GPIO();
                channels[i] = new Channel(this, gpio, i);
                innerConnections[i] = gpio;
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            var nonsecureMaskedInteruptStatus = new DoubleWordRegister(this)
                .WithFlags(0, channels.Length, FieldMode.Read,
                    valueProviderCallback: (i, _) => channels[i].GlobalInterrupt,
                    name: $"Masked interrupt status for secure channel (MISn)"
                )
                .WithReservedBits(channels.Length, 32 - channels.Length);

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.NonsecureMaskedInteruptStatus, nonsecureMaskedInteruptStatus},
            };
            registers = new DoubleWordRegisterCollection(this, registerMap);
        }

        public void Reset()
        {
            registers.Reset();
            foreach(var channel in channels)
            {
                channel.Reset();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            if(registers.TryRead(offset, out var result))
            {
                return result;
            }
            if(TryGetChannelBasedOnOffset(offset, out var channel))
            {
                return channel.ReadDoubleWord(offset);
            }
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(registers.TryWrite(offset, value))
            {
                return;
            }
            if(TryGetChannelBasedOnOffset(offset, out var channel))
            {
                channel.WriteDoubleWord(offset, value);
                return;
            }
            this.LogUnhandledWrite(offset, value);
        }

        public void OnGPIO(int number, bool value)
        {
            var channel = channels.ElementAtOrDefault(number);
            if(channel == null)
            {
                this.Log(LogLevel.Warning, "Channel number {0} is out of range, must be in [0; {1}]", number, channels.Length - 1);
                return;
            }

            if(!value)
            {
                return;
            }

            this.Log(LogLevel.Noisy, "DMA peripheral request on channel {0}", number);
            if(channel.TryTriggerTransfer())
            {
                this.Log(LogLevel.Debug, "DMA peripheral request on channel {0} ignored", number);
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x1000;

        private bool TryGetChannelBasedOnOffset(long offset, out Channel channel)
        {
            var shifted = offset - (long)Registers.Channel0LinkedListBaseAddress;
            var channelNumber = shifted / ShiftBetweenChannels;
            channel = channels.ElementAtOrDefault((int)channelNumber);
            return channel != null;
        }

        private readonly IMachine machine;
        private readonly DmaEngine engine;
        private readonly DoubleWordRegisterCollection registers;
        private readonly Channel[] channels;

        private const int ShiftBetweenChannels = 0x80;

        private class Channel
        {
            public Channel(STM32WBA55_GPDMA parent, IGPIO interrupt, int number)
            {
                this.parent = parent;
                this.interrupt = interrupt;
                channelNumber = number;

                var registersMap = new Dictionary<long, DoubleWordRegister>();

                registersMap.Add((long)ChannelRegisters.ChannelLinkedListBaseAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithReservedBits(0, 16)
                    .WithValueField(16, 16, out linkedListBaseAddress, name: "Linked list base address (LBA)"));

                registersMap.Add((long)ChannelRegisters.ChannelFlagClear + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithReservedBits(0, 8)
                    .WithFlag(8, FieldMode.Write, name: "Transfer complete flag clear (TCF)",
                            writeCallback: (_, val) =>
                            {
                                if(val)
                                {
                                    TransferComplete = false;
                                }
                            })
                    .WithFlag(9, FieldMode.Write, name: "Half transfer flag clear (TCF)",
                            writeCallback: (_, val) =>
                            {
                                if(val)
                                {
                                    HalfTransfer = false;
                                }
                            })
                    .WithFlag(10, FieldMode.Write, name: "Data transfer error flag clear (DTEF)",
                            writeCallback: (_, val) =>
                            {
                                if(val)
                                {
                                    DataTransferError = false;
                                }
                            })
                    .WithFlag(11, FieldMode.Write, name: "Update link transfer error flag clear (ULEF)",
                            writeCallback: (_, val) =>
                            {
                                if(val)
                                {
                                    UpdateLinkTransferError = false;
                                }
                            })
                    .WithFlag(12, FieldMode.Write, name: "User setting error flag clear (USEF)",
                            writeCallback: (_, val) =>
                            {
                                if(val)
                                {
                                    UserSettingError = false;
                                }
                            })
                    .WithFlag(13, FieldMode.Write, name: "Completed suspension flag clear (SUSPF)",
                            writeCallback: (_, val) =>
                            {
                                if(val)
                                {
                                    CompletedSuspension = false;
                                }
                            })
                    .WithFlag(14, FieldMode.Write, name: "Trigger overrun flag clear (TOF)",
                            writeCallback: (_, val) =>
                            {
                                if(val)
                                {
                                    TriggerOverrun = false;
                                }
                            })
                    .WithReservedBits(15, 17)
                    .WithWriteCallback((_, __) => Update())
                );

                registersMap.Add((long)ChannelRegisters.ChannelStatus + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithTag("Idle (IDLEF)", 0, 1)
                    .WithReservedBits(1, 7)
                    .WithFlag(8, FieldMode.Read, name: "Transfer complete flag (TCF)",
                        valueProviderCallback: _ => TransferComplete)
                    .WithFlag(9, FieldMode.Read, name: "Half transfer flag (HTF)",
                        valueProviderCallback: _ => HalfTransfer)
                    .WithFlag(10, FieldMode.Read, name: "Data transfer error flag (DTEF)",
                        valueProviderCallback: _ => DataTransferError)
                    .WithFlag(11, FieldMode.Read, name: "Update link transfer error flag (ULEF)",
                        valueProviderCallback: _ => UpdateLinkTransferError)
                    .WithFlag(12, FieldMode.Read, name: "User setting error flag (USEF)",
                        valueProviderCallback: _ => UserSettingError)
                    .WithFlag(13, FieldMode.Read, name: "Completed suspension flag (SUSPF)",
                        valueProviderCallback: _ => CompletedSuspension)
                    .WithFlag(14, FieldMode.Read, name: "Trigger overrun flag (TOF)",
                        valueProviderCallback: _ => TriggerOverrun)
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 8, out monitoredFIFOlevel, name: "Monitored FIFO level (FIFOL)")   //TODO
                    .WithReservedBits(24, 8)
                    .WithWriteCallback((_, __) => Update()));

                registersMap.Add((long)ChannelRegisters.ChannelControl + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithFlag(0, out channelEnable,
                        writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                DoTransfer();
                            }
                        },
                        valueProviderCallback: _ => false, name: "Enable (EN)")
                      .WithFlag(1, out channelReset,
                        writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                //TODO: check GPDMA_CxBR1,GPDMA_CxSAR, and GPDMA_CxDAR
                                channelEnable.Value = false;
                                channelSuspend.Value = false;
                            }
                        },
                        valueProviderCallback: _ => false, name: "RESET (RESET)")
                      .WithFlag(2, out channelSuspend,
                        writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                // TODO
                            }
                        }, name: "SUSPEND (SUSP)")
                    .WithReservedBits(3, 5)
                    .WithFlag(8, out transferCompleteInterruptEnable, name: "Transfer complete interrupt enable (TCIE)")
                    .WithFlag(9, out halfTransferInterruptEnable, name: "Half transfer interrupt enable (HTIE)")
                    .WithFlag(10, out dataTransferErrorInterruptEnable, name: "Data transfer error interrupt enable (DTEIE)")
                    .WithFlag(11, out updateLinkTransferErrorInterruptEnable, name: "Update link transfer error interrupt enable (ULEIE)")
                    .WithFlag(12, out userSettingErrorInterruptEnable, name: "User setting error interrupt enable (USEIE)")
                    .WithFlag(13, out completedSuspensionInterruptEnable, name: "Completed suspension interrupt enable (SUSPIE)")
                    .WithFlag(14, out triggerOverrunInterruptEnable, name: "Trigger overrun interrupt enable (TOIE)")
                    .WithReservedBits(15, 1)
                    .WithTag("Link step mode (LSM)", 16, 1)
                    .WithTag("Linked list allocated port (LAP)", 17, 1)
                    .WithReservedBits(18, 4)
                    .WithEnumField(22, 2, out priorityLevel, name: "Priority level (PRIO)")
                    .WithReservedBits(24, 8)
                    .WithWriteCallback((_, __) => Update()));

                registersMap.Add((long)ChannelRegisters.ChannelTransfer1 + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithEnumField(0, 2, out sourceDataWidth, name: "Binary logarithm source data width (SDW_LOG2)")
                    .WithReservedBits(2, 1)
                    .WithFlag(3, out sourceIncrementingBurst, name: "Source incrementing burst (SINC)")
                    .WithValueField(4, 5, out sourceBurstLength, name: "Source burst length (SBL_1)")  //TODO
                    .WithReservedBits(10, 1)
                    .WithTag("Padding alignment mode (PAM)", 11, 2)
                    .WithTag("Source byte exchange (SBX)", 13, 1)
                    .WithTag("Source allocated port (SAP)", 14, 1)
                    .WithTag("Security attribute source (SSEC)", 15, 1)
                    .WithEnumField(16, 2, out destinationDataWith, name: "Binary logarithm destination data with (DDW_LOG2)")
                    .WithReservedBits(18, 1)
                    .WithFlag(19, out destinationIncrementingBurst, name: "Destination incrementing burst (DINC)")
                    .WithValueField(20, 6, out destinationBurstLength, name: "Destination burst length (DBL_1)")   //TODO
                    .WithTag("Destination byte exchange (DBX)", 26, 1)
                    .WithTag("Destination half-word exchange (DHX)", 27, 1)
                    .WithReservedBits(28, 2)
                    .WithTag("Destination allocated port (DAP)", 30, 1)
                    .WithTag("Security attribute destination (DSEC)", 31, 1));

                registersMap.Add((long)ChannelRegisters.ChannelTransfer2 + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithEnumField(0, 5, out hardwareRequestSelection, name: "Hardware request selection (REQSEL)")
                    .WithReservedBits(6, 3)
                    .WithFlag(9, out softwareRequest, name: "Software request (SWREQ)")
                    .WithTag("Destination hardware request (DREQ)", 10, 1)
                    .WithFlag(11, out blockHardwareRequest, name: "Block hardware request (BREQ)") //TODO: implement block/burst transfer
                    .WithReservedBits(12, 2)
                    .WithTag("Trigger mode (TRIGM)", 14, 2)
                    .WithEnumField(16, 5, out triggerEventInputSelection, name: "Trigger event input selection (TRIGSEL)")
                    .WithReservedBits(21, 3)
                    .WithTag("Trigger event polarity (TRIGPOL)", 24, 2)
                    .WithReservedBits(26, 4)
                    .WithValueField(30, 2, out transferCompleteEventMode, name: "Transfer complete event mode (TCEM)")   //TODO
                    );

                //TODO: implement registers & callbacks
                registersMap.Add((long)ChannelRegisters.ChannelBlock1 + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    /* TODO: Block size transferred from the source. When the channel is enabled, this field becomes
                    read-only and is decremented, indicating the remaining number of data items in the current
                    source block to be transferred. BNDT[15:0] is programmed in number of bytes, maximum
                    source block size is 64 Kbytes -1. Once the last data transfer is completed (BNDT[15:0] = 0)*/
                    .WithValueField(0, 16, out blockNumberDataBytesFromSource, name: "Block number data bytes from source (BNDT)",
                        writeCallback: (_, val) => originalBlockNumberDataBytesFromSource = val));
                        registersMap.Add((long)ChannelRegisters.ChannelSourceAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithValueField(0, 32, out sourceAddress, name: "Source address (SA)",
                        writeCallback: (_, val) => currentSourceAddress = val));

                registersMap.Add((long)ChannelRegisters.ChannelDestinationAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithValueField(0, 32, out destinationAddress, name: "Destination address (DA)",
                        writeCallback: (_, val) => currentDestinationAddress = val));

                registersMap.Add((long)ChannelRegisters.ChannelLinkedListAddress + (number * ShiftBetweenChannels), new DoubleWordRegister(parent)
                    .WithReservedBits(0, 2)
                    /*TODO: If UT1 = UT2 = UB1 = USA = UDA = ULL = 0 and if LA[15:2] = 0, the current LLI is the last
                    one. The channel transfer is completed without any update of the linked-list GPDMA register
                    file. Else, this field is the pointer to the memory address offset from which the next linked-list data
                    structure is automatically fetched from, once the data transfer is completed, in order to
                    conditionally update the linked-list GPDMA internal register file (GPDMA_CxTR1,
                    GPDMA_CxTR2, GPDMA_CxBR1, GPDMA_CxSAR, GPDMA_CxDAR, and
                    GPDMA_CxLLR*/
                    .WithValueField(2, 14, out lowSignificantAddress, name: "Low-significant address (LA)")
                    .WithFlag(16, out updateLLRfromMemory, name: "Update CxLLR register from memory (ULL)")
                    .WithReservedBits(17, 10)
                    .WithFlag(27, out updateDARfromMemory, name: "Update CxDAR register from memory (UDA)")
                    .WithFlag(28, out updateSARfromMemory, name: "Update CxSAR register from memory (USA)")
                    .WithFlag(29, out updateBR1fromMemory, name: "Update CxBR1 register from memory (UB1)")
                    .WithFlag(30, out updateTR2fromMemory, name: "Update CxTR2 register from memory (UT2)")
                    .WithFlag(31, out updateTR1fromMemory, name: "Update CxTR1 register from memory (UT1)"));

                registers = new DoubleWordRegisterCollection(parent, registersMap);
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
                TransferComplete = false;
                HalfTransfer = false;
            }

            public bool TryTriggerTransfer()
            {
                if(!Enabled || monitoredFIFOlevel.Value == 0)
                {
                    return false;
                }
                DoTransfer();
                Update();
                return true;
            }

            private void Update()
            {
                var result = (TransferComplete && TransferCompleteInterruptEnable)
                        || (HalfTransfer && HalfTransferInterruptEnable)
                        || (DataTransferError && DataTransferErrorInterruptEnable)
                        || (UpdateLinkTransferError && UpdateLinkTransferErrorInterruptEnable)
                        || (UserSettingError && UserSettingErrorInterruptEnable)
                        || (CompletedSuspension && CompletedSuspensionInterruptEnable)
                        || (TriggerOverrun && TriggerOverrunInterruptEnable);
                interrupt.Set(result);

                parent.Log(LogLevel.Noisy, "Update of channel {0} triggered. Interrupt set: {1}", channelNumber, result);
            }

            public bool GlobalInterrupt => HalfTransfer || TransferComplete || DataTransferError || UpdateLinkTransferError || UserSettingError || CompletedSuspension || TriggerOverrun;

            public bool Enabled => channelEnable.Value;

            public bool HalfTransfer { get; set; }
            public bool TransferComplete { get; set; }
            public bool DataTransferError { get; set; }
            public bool UpdateLinkTransferError { get; set; }
            public bool UserSettingError { get; set; }
            public bool CompletedSuspension { get; set; }
            public bool TriggerOverrun { get; set; }

            public bool HalfTransferInterruptEnable => halfTransferInterruptEnable.Value;
            public bool TransferCompleteInterruptEnable => transferCompleteInterruptEnable.Value;
            public bool DataTransferErrorInterruptEnable => dataTransferErrorInterruptEnable.Value;
            public bool UpdateLinkTransferErrorInterruptEnable => updateLinkTransferErrorInterruptEnable.Value;
            public bool UserSettingErrorInterruptEnable => userSettingErrorInterruptEnable.Value;
            public bool CompletedSuspensionInterruptEnable => completedSuspensionInterruptEnable.Value;
            public bool TriggerOverrunInterruptEnable => triggerOverrunInterruptEnable.Value;

            private void DoTransfer()
            {
                //TODO: implement linked list mode
                //TODO: implement to copy whole block if source = destination = memory

                //get the size of a data unit (data beat) to copy
                var toCopy = Math.Max((uint)SizeToType(sourceDataWidth.Value), (uint)SizeToType(destinationDataWith.Value));

                while(blockNumberDataBytesFromSource.Value > 0)
                {
                    var response = IssueCopy(currentSourceAddress, currentDestinationAddress, toCopy,
                        sourceIncrementingBurst.Value, destinationIncrementingBurst.Value, sourceDataWidth.Value,
                        destinationDataWith.Value);

                    sourceAddress.Value = response.ReadAddress.Value;
                    currentSourceAddress = response.ReadAddress.Value;
                    destinationAddress.Value = response.WriteAddress.Value;
                    currentDestinationAddress = response.WriteAddress.Value;

                    blockNumberDataBytesFromSource.Value -= 1;
                    HalfTransfer = blockNumberDataBytesFromSource.Value <= originalBlockNumberDataBytesFromSource / 2;
                    TransferComplete = blockNumberDataBytesFromSource.Value == 0;
                    Update();
                }
            }

            private Response IssueCopy(ulong sourceAddress, ulong destinationAddress, uint size,
                bool incrementReadAddress, bool incrementWriteAddress, TransferSize sourceDataWidth,
                TransferSize destinationDataWith)
            {
                var request = new Request(
                    sourceAddress,
                    destinationAddress,
                    (int)size,
                    SizeToType(sourceDataWidth),
                    SizeToType(destinationDataWith),
                    incrementReadAddress,
                    incrementWriteAddress
                );
                return parent.engine.IssueCopy(request);
            }

            private TransferType SizeToType(TransferSize size)
            {
                switch(size)
                {
                    case TransferSize.Bits32:
                        return TransferType.DoubleWord;
                    case TransferSize.Bits16:
                        return TransferType.Word;
                    case TransferSize.Bits8:
                    default:
                        return TransferType.Byte;
                }
            }

            private IValueRegisterField monitoredFIFOlevel;

            //Channel Control CxCR
            private IFlagRegisterField channelEnable;
            private IFlagRegisterField channelReset;
            private IFlagRegisterField channelSuspend;
            private IFlagRegisterField transferCompleteInterruptEnable;
            private IFlagRegisterField halfTransferInterruptEnable;
            private IFlagRegisterField dataTransferErrorInterruptEnable;
            private IFlagRegisterField updateLinkTransferErrorInterruptEnable;
            private IFlagRegisterField userSettingErrorInterruptEnable;
            private IFlagRegisterField completedSuspensionInterruptEnable;
            private IFlagRegisterField triggerOverrunInterruptEnable;
            private IEnumRegisterField<Priority> priorityLevel;

            private IValueRegisterField linkedListBaseAddress;

            //Transfer Register CxTR 1&2
            private IFlagRegisterField sourceIncrementingBurst;
            private IFlagRegisterField destinationIncrementingBurst;
            private IValueRegisterField sourceBurstLength;
            private IValueRegisterField destinationBurstLength;
            private IEnumRegisterField<TransferSize> sourceDataWidth;
            private IEnumRegisterField<TransferSize> destinationDataWith;
            private IEnumRegisterField<HardwareRequestSelection> hardwareRequestSelection;
            private IFlagRegisterField softwareRequest;
            private IFlagRegisterField blockHardwareRequest;
            private IEnumRegisterField<TriggerEventInputSelection> triggerEventInputSelection;
            private IValueRegisterField transferCompleteEventMode;

            private IValueRegisterField blockNumberDataBytesFromSource;
            private IValueRegisterField sourceAddress;
            private IValueRegisterField destinationAddress;

            // Linked list address
            private IValueRegisterField lowSignificantAddress;
            private IFlagRegisterField updateLLRfromMemory;
            private IFlagRegisterField updateDARfromMemory;
            private IFlagRegisterField updateSARfromMemory;
            private IFlagRegisterField updateBR1fromMemory;
            private IFlagRegisterField updateTR2fromMemory;
            private IFlagRegisterField updateTR1fromMemory;

            private ulong currentSourceAddress;
            private ulong currentDestinationAddress;
            private ulong originalBlockNumberDataBytesFromSource;

            private readonly DoubleWordRegisterCollection registers;
            private readonly STM32WBA55_GPDMA parent;
            private readonly int channelNumber;
            private readonly IGPIO interrupt;

            private enum Priority
            {
                LowPrioLowWeight = 0,
                LowPrioMidWeight = 1,
                LowPrioHighWeigt = 2,
                HighPrio = 3
            }

            private enum TransferSize
            {
                Bits8 = 0, //byte
                Bits16 = 1, // half-word (2bytes)
                Bits32 = 2, // word (4bytes)
                UserSettingError = 3
            }

            private enum HardwareRequestSelection
            {
                Adc4_dma = 0,
                Spi1_rx_dma = 1,
                Spi1_tx_dma = 2,
                Spi3_rx_dma = 3,
                Spi3_tx_dma = 4,
                I2c1_rx_dma = 5,
                I2c1_tx_dma = 6,
                I2c1_evc_dma = 7,
                I2c3_rx_dma = 8,
                I2c3_tx_dma = 9,
                I2c3_evc_dma = 10,
                Usart1_rx_dma = 11,
                Usart1_tx_dma = 12,
                Usart2_rx_dma = 13,
                Usart2_tx_dma = 14,
                Lpuart1_rx_dma = 15,
                Lpuart1_tx_dma = 16,
                Sai_a_dma = 17,
                Sai_b_dma = 18,
                Tim1_cc1_dma = 19,
                Tim1_cc2_dma = 20,
                Tim1_cc3_dma = 21,
                Tim1_cc4_dma = 22,
                Tim1_upd_dma = 23,
                Tim1_trg_dma = 24,
                Tim1_com_dma = 25,
                Tim2_cc1_dma = 26,
                Tim2_cc2_dma = 27,
                Tim2_cc3_dma = 28,
                Tim2_cc4_dma = 29,
                Tim2_upd_dma = 30,
                Tim3_cc1_dma = 31,
                Tim3_cc2_dma = 32,
                Tim3_cc3_dma = 33,
                Tim3_cc4_dma = 34,
                Tim3_upd_dma = 35,
                Tim3_trg_dma = 36,
                Tim16_cc1_dma = 37,
                Tim16_upd_dma = 38,
                Tim17_cc1_dma = 39,
                Tim17_upd_dma = 40,
                Aes_in_dma = 41,
                Aes_out_dma = 42,
                Hash_in_dma = 43,
                Saes_in_dma = 44,
                Saes_out_dma = 45,
                Lptim1_ic1_dma = 46,
                Lptim1_ic2_dma = 47,
                Lptim1_ue_dma = 48,
                Lptim2_ic1_dma = 49,
                Lptim2_ic2_dma = 50,
                Lptim2_ue_dma = 51,
            }

            private enum TriggerEventInputSelection
            {
                Exti0 = 0,
                Exti1 = 1,
                Exti2 = 2,
                Exti3 = 3,
                Exti4 = 4,
                Exti5 = 5,
                Exti6 = 6,
                Exti7 = 7,
                Tamp_trg1 = 8,
                Tamp_trg2 = 9,
                Tamp_trg3 = 10,
                Lptim1_ch1 = 11,
                Lptim1_ch2 = 12,
                Lptim2_ch1 = 13,
                Lptim2_ch2 = 14,
                Comp1_out = 15,
                Comp2_out = 16,
                Rtc_alra_trg = 17,
                Rtc_alrb_trg = 18,
                Rtc_wut_trg = 19,
                Gpdma1_ch0_tc = 20,
                Gpdma1_ch1_tc = 21,
                Gpdma1_ch2_tc = 22,
                Gpdma1_ch3_tc = 23,
                Gpdma1_ch4_tc = 24,
                Gpdma1_ch5_tc = 25,
                Gpdma1_ch6_tc = 26,
                Gpdma1_ch7_tc = 27,
                Tim2_trgo = 28,
                Adc4_awd1 = 29,
            }

            private enum ChannelRegisters
            {
                ChannelLinkedListBaseAddress = 0x50,
                ChannelFlagClear = 0x5C,
                ChannelStatus = 0x60,
                ChannelControl = 0x64,
                ChannelTransfer1 = 0x90,
                ChannelTransfer2 = 0x94,
                ChannelBlock1 = 0x98,
                ChannelSourceAddress = 0x9C,
                ChannelDestinationAddress = 0xA0,
                ChannelLinkedListAddress = 0xCC,
            }
        }

        private enum Registers : long
        {
            SecureConfiguration = 0x00, //SECCFGR
            PrivilegedConfiguration = 0x04, //PRIVCFGR
            ConfigurationLock = 0x08, //RCFGLOCKR
            NonsecureMaskedInteruptStatus = 0x0C, //MISR
            SecureMaskedInteruptStatus = 0x10, //SMISR

            Channel0LinkedListBaseAddress = 0x50, //CxLBAR
            Channel0FlagClear = 0x5C, //CxFCR
            Channel0Status = 0x60, //CxSR
            Channel0Control = 0x64, //CxCR
            Channel0Transfer1 = 0x90, //CxTR1
            Channel0Transfer2 = 0x94, //CxTR2
            Channel0Block1 = 0x98, //CxBR1
            Channel0SourceAddress = 0x9C, //CxSAR
            Channel0DestinationAddress = 0xA0, //CxDAR
            Channel0LinkedListAddress = 0xCC, //CxLLR

            //needed for proper log output when accessed
            Channel1LinkedListBaseAddress = 0xD0,
            Channel1FlagClear = 0xDC,
            Channel1Status = 0xE0,
            Channel1Control = 0xE4,
            Channel1Transfer1 = 0x110,
            Channel1Transfer2 = 0x114,
            Channel1Block1 = 0x118,
            Channel1SourceAddress = 0x11C,
            Channel1DestinationAddress = 0x120,
            Channel1LinkedListAddress = 0x14C,

            Channel2LinkedListBaseAddress = 0x150,
            Channel2FlagClear = 0x15C,
            Channel2Status = 0x160,
            Channel2Control = 0x164,
            Channel2Transfer1 = 0x190,
            Channel2Transfer2 = 0x194,
            Channel2Block1 = 0x198,
            Channel2SourceAddress = 0x19C,
            Channel2DestinationAddress = 0x1A0,
            Channel2LinkedListAddress = 0x1CC,

            Channel3LinkedListBaseAddress = 0x1D0,
            Channel3FlagClear = 0x1DC,
            Channel3Status = 0x1E0,
            Channel3Control = 0x1E4,
            Channel3Transfer1 = 0x210,
            Channel3Transfer2 = 0x214,
            Channel3Block1 = 0x218,
            Channel3SourceAddress = 0x21C,
            Channel3DestinationAddress = 0x220,
            Channel3LinkedListAddress = 0x24C,

            Channel4LinkedListBaseAddress = 0x250,
            Channel4FlagClear = 0x25C,
            Channel4Status = 0x260,
            Channel4Control = 0x264,
            Channel4Transfer1 = 0x290,
            Channel4Transfer2 = 0x294,
            Channel4Block1 = 0x298,
            Channel4SourceAddress = 0x29C,
            Channel4DestinationAddress = 0x2A0,
            Channel4LinkedListAddress = 0x2CC,

            Channel5LinkedListBaseAddress = 0x2D0,
            Channel5FlagClear = 0x2DC,
            Channel5Status = 0x2E0,
            Channel5Control = 0x2E4,
            Channel5Transfer1 = 0x310,
            Channel5Transfer2 = 0x314,
            Channel5Block1 = 0x318,
            Channel5SourceAddress = 0x31C,
            Channel5DestinationAddress = 0x320,
            Channel5LinkedListAddress = 0x34C,

            Channel6LinkedListBaseAddress = 0x350,
            Channel6FlagClear = 0x35C,
            Channel6Status = 0x360,
            Channel6Control = 0x364,
            Channel6Transfer1 = 0x390,
            Channel6Transfer2 = 0x394,
            Channel6Block1 = 0x398,
            Channel6SourceAddress = 0x39C,
            Channel6DestinationAddress = 0x3A0,
            Channel6LinkedListAddress = 0x3CC,

            Channel7LinkedListBaseAddress = 0x3D0,
            Channel7FlagClear = 0x3DC,
            Channel7Status = 0x3E0,
            Channel7Control = 0x3E4,
            Channel7Transfer1 = 0x410,
            Channel7Transfer2 = 0x414,
            Channel7Block1 = 0x418,
            Channel7SourceAddress = 0x41C,
            Channel7DestinationAddress = 0x420,
            Channel7LinkedListAddress = 0x44C,
        }
    }
}
