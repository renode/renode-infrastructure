//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class EFR32MG12_LDMA : BasicDoubleWordPeripheral, IGPIOReceiver, IKnownSize
    {
        public EFR32MG12_LDMA(IMachine machine) : base(machine)
        {
            engine = new DmaEngine(sysbus);
            signals = new HashSet<int>();
            IRQ = new GPIO();
            channels = new Channel[NumberOfChannels];
            for(var i = 0; i < NumberOfChannels; ++i)
            {
                channels[i] = new Channel(this, i);
            }
            BuildRegisters();
        }

        public override void Reset()
        {
            signals.Clear();
            foreach(var channel in channels)
            {
                channel.Reset();
            }
            base.Reset();
            UpdateInterrupts();
        }

        public void OnGPIO(int number, bool value)
        {
            var signal = (SignalSelect)(number & 0xf);
            var source = (SourceSelect)((number >> 4) & 0x3f);
            bool single = ((number >> 12) & 1) != 0;
            if(!value)
            {
                signals.Remove(number);
                return;
            }
            signals.Add(number);
            for(var i = 0; i < NumberOfChannels; ++i)
            {
                if(single && channels[i].IgnoreSingleRequests)
                {
                    continue;
                }
                if(channels[i].Signal == signal && channels[i].Source == source)
                {
                    channels[i].StartFromSignal();
                }
            }
        }

        public GPIO IRQ { get; }

        public long Size => 0x400;

        private void BuildRegisters()
        {
            Registers.Control.Define(this)
                .WithTag("SYNCPRSSETEN", 0, 8)
                .WithTag("SYNCPRSCLREN", 8, 8)
                .WithReservedBits(16, 8)
                .WithTag("NUMFIXED", 24, 3)
                .WithReservedBits(27, 4)
                .WithTaggedFlag("RESET", 31)
            ;
            Registers.Status.Define(this)
                .WithTaggedFlag("ANYBUSY", 0)
                .WithTaggedFlag("ANYREQ", 1)
                .WithReservedBits(2, 1)
                .WithTag("CHGRANT", 3, 3)
                .WithReservedBits(6, 2)
                .WithTag("CHERROR", 8, 3)
                .WithReservedBits(11, 5)
                .WithTag("FIFOLEVEL", 16, 5)
                .WithReservedBits(21, 3)
                .WithTag("CHNUM", 24, 5)
                .WithReservedBits(29, 3)
            ;
            Registers.SynchronizationTrigger.Define(this)
                .WithTag("SYNCTRIG", 0, 8)
                .WithReservedBits(8, 24)
            ;
            Registers.ChannelEnable.Define(this)
                .WithFlags(0, 8, writeCallback: (i, _, value) => channels[i].Enabled = value, valueProviderCallback: (i, _) => channels[i].Enabled, name: "CHEN")
                .WithReservedBits(8, 24)
            ;
            Registers.ChannelBusy.Define(this)
                .WithTag("BUSY", 0, 8)
                .WithReservedBits(8, 24)
            ;
            Registers.ChannelDone.Define(this)
                .WithFlags(0, 8, writeCallback: (i, _, value) => channels[i].Done = value, valueProviderCallback: (i, _) => channels[i].Done, name: "CHDONE")
                .WithReservedBits(8, 24)
            ;
            Registers.ChannelDebugHalt.Define(this)
                .WithTag("DBGHALT", 0, 8)
                .WithReservedBits(8, 24)
            ;
            Registers.ChannelSoftwareTransferRequest.Define(this)
                .WithFlags(0, 8, FieldMode.Set, writeCallback: (i, _, value) => { if(value) channels[i].StartTransfer(); }, name: "SWREQ")
                .WithReservedBits(8, 24)
            ;
            Registers.ChannelRequestDisable.Define(this)
                .WithFlags(0, 8, writeCallback: (i, _, value) => channels[i].RequestDisable = value, valueProviderCallback: (i, _) => channels[i].RequestDisable, name: "REQDIS")
                .WithReservedBits(8, 24)
            ;
            Registers.ChannelRequestsPending.Define(this)
                .WithTag("REQPEND", 0, 8)
                .WithReservedBits(8, 24)
            ;
            Registers.ChannelLinkLoad.Define(this)
                .WithFlags(0, 8, FieldMode.Set, writeCallback: (i, _, value) => { if(value) channels[i].LinkLoad(); }, name: "LINKLOAD")
                .WithReservedBits(8, 24)
            ;
            Registers.ChannelRequestClear.Define(this)
                .WithTag("REQCLEAR", 0, 8)
                .WithReservedBits(8, 24)
            ;
            Registers.InterruptFlag.Define(this)
                .WithFlags(0, 8, FieldMode.Read, valueProviderCallback: (i, _) => channels[i].DoneInterrupt, name: "DONE")
                .WithReservedBits(8, 23)
                .WithTaggedFlag("ERROR", 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;
            Registers.InterruptFlagSet.Define(this)
                .WithFlags(0, 8, FieldMode.Set, writeCallback: (i, _, value) => channels[i].DoneInterrupt |= value, name: "DONE")
                .WithReservedBits(8, 23)
                .WithTaggedFlag("ERROR", 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;
            Registers.InterruptFlagClear.Define(this)
                .WithFlags(0, 8, FieldMode.WriteOneToClear, writeCallback: (i, _, value) => channels[i].DoneInterrupt &= !value, name: "DONE")
                .WithReservedBits(8, 23)
                .WithTaggedFlag("ERROR", 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;
            Registers.InterruptEnable.Define(this)
                .WithFlags(0, 8, writeCallback: (i, _, value) => channels[i].DoneInterruptEnable = value, valueProviderCallback: (i, _) => channels[i].DoneInterruptEnable, name: "DONE")
                .WithReservedBits(8, 23)
                .WithTaggedFlag("ERROR", 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            var channelDelta = (uint)((long)Registers.Channel1PeripheralRequestSelect - (long)Registers.Channel0PeripheralRequestSelect);
            Registers.Channel0PeripheralRequestSelect.BindMany(this, NumberOfChannels, i => channels[i].PeripheralRequestSelectRegister, channelDelta);
            Registers.Channel0Configuration.BindMany(this, NumberOfChannels, i => channels[i].ConfigurationRegister, channelDelta);
            Registers.Channel0LoopCounter.BindMany(this, NumberOfChannels, i => channels[i].LoopCounterRegister, channelDelta);
            Registers.Channel0DescriptorControlWord.BindMany(this, NumberOfChannels, i => channels[i].DescriptorControlWordRegister, channelDelta);
            Registers.Channel0DescriptorSourceDataAddress.BindMany(this, NumberOfChannels, i => channels[i].DescriptorSourceDataAddressRegister, channelDelta);
            Registers.Channel0DescriptorDestinationDataAddress.BindMany(this, NumberOfChannels, i => channels[i].DescriptorDestinationDataAddressRegister, channelDelta);
            Registers.Channel0DescriptorLinkStructureAddress.BindMany(this, NumberOfChannels, i => channels[i].DescriptorLinkStructureAddressRegister, channelDelta);
        }

        private void UpdateInterrupts()
        {
            this.Log(LogLevel.Debug, "Interrupt set for channels: {0}", String.Join(", ",
                channels
                    .Where(channel => channel.IRQ)
                    .Select(channel => channel.Index)
                ));
            IRQ.Set(channels.Any(channel => channel.IRQ));
        }

        private readonly DmaEngine engine;
        private readonly HashSet<int> signals;
        private readonly Channel[] channels;

        private const int NumberOfChannels = 8;

        private enum SignalSelect
        {
            // if SOURCESEL is None
            // Off = 0bxxxx,
            // if SOURCESEL is PRS
            PRSRequest0                         = 0b0000,
            PRSRequest1                         = 0b0001,
            // if SOURCESEL is ADC0
            ADC0Single                          = 0b0000,
            ADC0Scan                            = 0b0001,
            // if SOURCESEL is VDAC0
            VDAC0CH0                            = 0b0000,
            VDAC0CH1                            = 0b0001,
            // if SOURCESEL is USART0
            USART0RxDataAvailable               = 0b0000,
            USART0TxBufferLow                   = 0b0001,
            USART0TxEmpty                       = 0b0010,
            // if SOURCESEL is USART1
            USART1RxDataAvailable               = 0b0000,
            USART1TxBufferLow                   = 0b0001,
            USART1TxEmpty                       = 0b0010,
            USART1RxDataAvailableRight          = 0b0011,
            USART1TxBufferLowRight              = 0b0100,
            // if SOURCESEL is USART2
            USART2RxDataAvailable               = 0b0000,
            USART2TxBufferLow                   = 0b0001,
            USART2TxEmpty                       = 0b0010,
            // if SOURCESEL is USART3
            USART3RxDataAvailable               = 0b0000,
            USART3TxBufferLow                   = 0b0001,
            USART3TxEmpty                       = 0b0010,
            USART3RxDataAvailableRight          = 0b0011,
            USART3TxBufferLowRight              = 0b0100,
            // if SOURCESEL is LEUART0
            LEUART0RxDataAvailable              = 0b0000,
            LEUART0TxBufferLow                  = 0b0001,
            LEUART0TxEmpty                      = 0b0010,
            // if SOURCESEL is I2C0
            I2C0RxDataAvailable                 = 0b0000,
            I2C0TxBufferLow                     = 0b0001,
            // if SOURCESEL is I2C1
            I2C1RxDataAvailable                 = 0b0000,
            I2C1TxBufferLow                     = 0b0001,
            // if SOURCESEL is TIMER0
            TIMER0UnderflowOverflow             = 0b0000,
            TIMER0CaptureCompare0               = 0b0001,
            TIMER0CaptureCompare1               = 0b0010,
            TIMER0CaptureCompare2               = 0b0011,
            // if SOURCESEL is TIMER1
            TIMER1UnderflowOverflow             = 0b0000,
            TIMER1CaptureCompare0               = 0b0001,
            TIMER1CaptureCompare1               = 0b0010,
            TIMER1CaptureCompare2               = 0b0011,
            TIMER1CaptureCompare3               = 0b0100,
            // if SOURCESEL is WTIMER0
            WTIMER0UnderflowOverflow            = 0b0000,
            WTIMER0CaptureCompare0              = 0b0001,
            WTIMER0CaptureCompare1              = 0b0010,
            WTIMER0CaptureCompare2              = 0b0011,
            // if SOURCESEL is WTIMER1
            WTIMER1UnderflowOverflow            = 0b0000,
            WTIMER1CaptureCompare0              = 0b0001,
            WTIMER1CaptureCompare1              = 0b0010,
            WTIMER1CaptureCompare2              = 0b0011,
            WTIMER1CaptureCompare3              = 0b0100,
            // if SOURCESEL is PROTIMER
            PROTIMERPreCounterOverflow          = 0b0000,
            PROTIMERBaseCounterOverflow         = 0b0001,
            PROTIMERWrapCounterOverflow         = 0b0010,
            PROTIMERCaptureCompare0             = 0b0011,
            PROTIMERCaptureCompare1             = 0b0100,
            PROTIMERCaptureCompare2             = 0b0101,
            PROTIMERCaptureCompare3             = 0b0110,
            PROTIMERCaptureCompare4             = 0b0111,
            // if SOURCESEL is MODEM
            MODEMDebug                          = 0b0000,
            // if SOURCESEL is AGC
            AGCReceivedSignalStrengthIndicator  = 0b0000,
            // if SOURCESEL is MSC
            MSCWriteDataReady                   = 0b0000,
            // if SOURCESEL is CRYPTO0
            CRYPTO0Data0Write                   = 0b0000,
            CRYPTO0Data0XorWrite                = 0b0001,
            CRYPTO0Data0Read                    = 0b0010,
            CRYPTO0Data1Write                   = 0b0011,
            CRYPTO0Data1Read                    = 0b0100,
            // if SOURCESEL is CSEN
            CSENData                            = 0b0000,
            CSENBaseline                        = 0b0001,
            // if SOURCESEL is LESENSE
            LESENSEBufferDataAvailable          = 0b0000,
            // if SOURCESEL is CRYPTO1
            CRYPTO1Data0Write                   = 0b0000,
            CRYPTO1Data0XorWrite                = 0b0001,
            CRYPTO1Data0Read                    = 0b0010,
            CRYPTO1Data1Write                   = 0b0011,
            CRYPTO1Data1Read                    = 0b0100,
        }

        private enum SourceSelect
        {
            None     = 0b000000,
            PRS      = 0b000001,
            ADC0     = 0b001000,
            VDAC0    = 0b001010,
            USART0   = 0b001100,
            USART1   = 0b001101,
            USART2   = 0b001110,
            USART3   = 0b001111,
            LEUART0  = 0b010000,
            I2C0     = 0b010100,
            I2C1     = 0b010101,
            TIMER0   = 0b011000,
            TIMER1   = 0b011001,
            WTIMER0  = 0b011010,
            WTIMER1  = 0b011011,
            PROTIMER = 0b100100,
            MODEM    = 0b100110,
            AGC      = 0b100111,
            MSC      = 0b110000,
            CRYPTO0  = 0b110001,
            CSEN     = 0b110010,
            LESENSE  = 0b110011,
            CRYPTO1  = 0b110100,
        }

        private enum Registers
        {
            Control                                     = 0x000,
            Status                                      = 0x004,
            SynchronizationTrigger                      = 0x008,
            ChannelEnable                               = 0x020,
            ChannelBusy                                 = 0x024,
            ChannelDone                                 = 0x028,
            ChannelDebugHalt                            = 0x02C,
            ChannelSoftwareTransferRequest              = 0x030,
            ChannelRequestDisable                       = 0x034,
            ChannelRequestsPending                      = 0x038,
            ChannelLinkLoad                             = 0x03C,
            ChannelRequestClear                         = 0x040,
            InterruptFlag                               = 0x060,
            InterruptFlagSet                            = 0x064,
            InterruptFlagClear                          = 0x068,
            InterruptEnable                             = 0x06C,
            Channel0PeripheralRequestSelect             = 0x080,
            Channel0Configuration                       = 0x084,
            Channel0LoopCounter                         = 0x088,
            Channel0DescriptorControlWord               = 0x08C,
            Channel0DescriptorSourceDataAddress         = 0x090,
            Channel0DescriptorDestinationDataAddress    = 0x094,
            Channel0DescriptorLinkStructureAddress      = 0x098,
            Channel1PeripheralRequestSelect             = 0x0B0,
            Channel1Configuration                       = 0x0B4,
            Channel1LoopCounter                         = 0x0B8,
            Channel1DescriptorControlWord               = 0x0BC,
            Channel1DescriptorSourceDataAddress         = 0x0C0,
            Channel1DescriptorDestinationDataAddress    = 0x0C4,
            Channel1DescriptorLinkStructureAddress      = 0x0C8,
            Channel2PeripheralRequestSelect             = 0x0E0,
            Channel2Configuration                       = 0x0E4,
            Channel2LoopCounter                         = 0x0E8,
            Channel2DescriptorControlWord               = 0x0EC,
            Channel2DescriptorSourceDataAddress         = 0x0F0,
            Channel2DescriptorDestinationDataAddress    = 0x0F4,
            Channel2DescriptorLinkStructureAddress      = 0x0F8,
            Channel3PeripheralRequestSelect             = 0x110,
            Channel3Configuration                       = 0x114,
            Channel3LoopCounter                         = 0x118,
            Channel3DescriptorControlWord               = 0x11C,
            Channel3DescriptorSourceDataAddress         = 0x120,
            Channel3DescriptorDestinationDataAddress    = 0x124,
            Channel3DescriptorLinkStructureAddress      = 0x128,
            Channel4PeripheralRequestSelect             = 0x140,
            Channel4Configuration                       = 0x144,
            Channel4LoopCounter                         = 0x148,
            Channel4DescriptorControlWord               = 0x14C,
            Channel4DescriptorSourceDataAddress         = 0x150,
            Channel4DescriptorDestinationDataAddress    = 0x154,
            Channel4DescriptorLinkStructureAddress      = 0x158,
            Channel5PeripheralRequestSelect             = 0x170,
            Channel5Configuration                       = 0x174,
            Channel5LoopCounter                         = 0x178,
            Channel5DescriptorControlWord               = 0x17C,
            Channel5DescriptorSourceDataAddress         = 0x180,
            Channel5DescriptorDestinationDataAddress    = 0x184,
            Channel5DescriptorLinkStructureAddress      = 0x188,
            Channel6PeripheralRequestSelect             = 0x1A0,
            Channel6Configuration                       = 0x1A4,
            Channel6LoopCounter                         = 0x1A8,
            Channel6DescriptorControlWord               = 0x1AC,
            Channel6DescriptorSourceDataAddress         = 0x1B0,
            Channel6DescriptorDestinationDataAddress    = 0x1B4,
            Channel6DescriptorLinkStructureAddress      = 0x1B8,
            Channel7PeripheralRequestSelect             = 0x1D0,
            Channel7Configuration                       = 0x1D4,
            Channel7LoopCounter                         = 0x1D8,
            Channel7DescriptorControlWord               = 0x1DC,
            Channel7DescriptorSourceDataAddress         = 0x1E0,
            Channel7DescriptorDestinationDataAddress    = 0x1E4,
            Channel7DescriptorLinkStructureAddress      = 0x1E8,
        }

        private class Channel
        {
            public Channel(EFR32MG12_LDMA parent, int index)
            {
                this.parent = parent;
                Index = index;
                descriptor = default(Descriptor);

                PeripheralRequestSelectRegister = new DoubleWordRegister(parent)
                    .WithEnumField<DoubleWordRegister, SignalSelect>(0, 4, out signalSelect, name: "SIGSEL")
                    .WithReservedBits(4, 12)
                    .WithEnumField<DoubleWordRegister, SourceSelect>(16, 6, out sourceSelect, name: "SOURCESEL")
                    .WithReservedBits(22, 10)
                    .WithWriteCallback((_, __) =>
                    {
                        if(ShouldPullSignal)
                        {
                            pullTimer.Enabled = true;
                        }
                    })
                ;
                ConfigurationRegister = new DoubleWordRegister(parent)
                    .WithReservedBits(0, 16)
                    .WithEnumField<DoubleWordRegister, ArbitrationSlotNumberMode>(16, 2, out arbitrationSlotNumberSelect, name: "ARBSLOTS")
                    .WithReservedBits(18, 2)
                    .WithEnumField<DoubleWordRegister, Sign>(20, 1, out sourceAddressIncrementSign, name: "SRCINCSIGN")
                    .WithEnumField<DoubleWordRegister, Sign>(21, 1, out destinationAddressIncrementSign, name: "DSTINCSIGN")
                    .WithReservedBits(22, 10)
                ;
                LoopCounterRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 8, out loopCounter, name: "LOOPCNT")
                    .WithReservedBits(8, 24)
                ;
                DescriptorControlWordRegister = new DoubleWordRegister(parent)
                    .WithEnumField<DoubleWordRegister, StructureType>(0, 2, FieldMode.Read,
                        valueProviderCallback: _ => descriptor.structureType,
                        name: "STRUCTTYPE")
                    .WithReservedBits(2, 1)
                    .WithFlag(3, FieldMode.Set,
                        writeCallback: (_, value) => descriptor.structureTransferRequest = value,
                        name: "STRUCTREQ")
                    .WithValueField(4, 11,
                        writeCallback: (_, value) => descriptor.transferCount = (ushort)value,
                        valueProviderCallback: _ => descriptor.transferCount,
                        name: "XFERCNT")
                    .WithFlag(15,
                        writeCallback: (_, value) => descriptor.byteSwap = value,
                        valueProviderCallback: _ => descriptor.byteSwap,
                        name: "BYTESWAP")
                    .WithEnumField<DoubleWordRegister, BlockSizeMode>(16, 4,
                        writeCallback: (_, value) => descriptor.blockSize = value,
                        valueProviderCallback: _ => descriptor.blockSize,
                        name: "BLOCKSIZE")
                    .WithFlag(20,
                        writeCallback: (_, value) => descriptor.operationDoneInterruptFlagSetEnable = value,
                        valueProviderCallback: _ => descriptor.operationDoneInterruptFlagSetEnable,
                        name: "DONEIFSEN")
                    .WithEnumField<DoubleWordRegister, RequestTransferMode>(21, 1,
                        writeCallback: (_, value) => descriptor.requestTransferModeSelect = value,
                        valueProviderCallback: _ => descriptor.requestTransferModeSelect,
                        name: "REQMODE")
                    .WithFlag(22,
                        writeCallback: (_, value) => descriptor.decrementLoopCount = value,
                        valueProviderCallback: _ => descriptor.decrementLoopCount,
                        name: "DECLOOPCNT")
                    .WithFlag(23,
                        writeCallback: (_, value) => descriptor.ignoreSingleRequests = value,
                        valueProviderCallback: _ => descriptor.ignoreSingleRequests,
                        name: "IGNORESREQ")
                    .WithEnumField<DoubleWordRegister, IncrementMode>(24, 2,
                        writeCallback: (_, value) => descriptor.sourceIncrement = value,
                        valueProviderCallback: _ => descriptor.sourceIncrement,
                        name: "SRCINC")
                    .WithEnumField<DoubleWordRegister, SizeMode>(26, 2,
                        writeCallback: (_, value) => descriptor.size = value,
                        valueProviderCallback: _ => descriptor.size,
                        name: "SIZE")
                    .WithEnumField<DoubleWordRegister, IncrementMode>(28, 2,
                        writeCallback: (_, value) => descriptor.destinationIncrement = value,
                        valueProviderCallback: _ => descriptor.destinationIncrement,
                        name: "DSTINC")
                    .WithEnumField<DoubleWordRegister, AddressingMode>(30, 1, FieldMode.Read,
                        valueProviderCallback: _ => descriptor.sourceAddressingMode,
                        name: "SRCMODE")
                    .WithEnumField<DoubleWordRegister, AddressingMode>(31, 1, FieldMode.Read,
                        valueProviderCallback: _ => descriptor.destinationAddressingMode,
                        name: "DSTMODE")
                    .WithChangeCallback((_, __) => { if(descriptor.structureTransferRequest) LinkLoad(); })
                ;
                DescriptorSourceDataAddressRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) => descriptor.sourceAddress = (uint)value,
                        valueProviderCallback: _ => descriptor.sourceAddress,
                        name: "SRCADDR")
                ;
                DescriptorDestinationDataAddressRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) => descriptor.destinationAddress = (uint)value,
                        valueProviderCallback: _ => descriptor.destinationAddress,
                        name: "DSTADDR")
                ;
                DescriptorLinkStructureAddressRegister = new DoubleWordRegister(parent)
                    .WithEnumField<DoubleWordRegister, AddressingMode>(0, 1, FieldMode.Read,
                        valueProviderCallback: _ => descriptor.linkMode,
                        name: "LINKMODE")
                    .WithFlag(1,
                        writeCallback: (_, value) => descriptor.link = value,
                        valueProviderCallback: _ => descriptor.link,
                        name: "LINK")
                    .WithValueField(2, 30,
                        writeCallback: (_, value) => descriptor.linkAddress = (uint)value,
                        valueProviderCallback: _ => descriptor.linkAddress,
                        name: "LINKADDR")
                ;

                pullTimer = new LimitTimer(parent.machine.ClockSource, 1000000, null, $"pullTimer-{Index}", 15, Direction.Ascending, false, WorkMode.Periodic, true, true);
                pullTimer.LimitReached += delegate
                {
                    if(!RequestDisable)
                    {
                        StartTransferInner();
                    }
                    if(!SignalIsOn || !ShouldPullSignal)
                    {
                        pullTimer.Enabled = false;
                    }
                };
            }

            public void StartFromSignal()
            {
                if(!RequestDisable)
                {
                    StartTransfer();
                }
            }

            public void LinkLoad()
            {
                LoadDescriptor();
                if(descriptor.structureTransferRequest || SignalIsOn)
                {
                    StartTransfer();
                }
            }

            public void StartTransfer()
            {
                if(ShouldPullSignal)
                {
                    pullTimer.Enabled = true;
                }
                else
                {
                    StartTransferInner();
                }
            }

            public void Reset()
            {
                descriptor = default(Descriptor);
                pullTimer.Reset();
                DoneInterrupt = false;
                DoneInterruptEnable = false;
                descriptorAddress = null;
                requestDisable = false;
                enabled = false;
                done = false;
            }

            public int Index { get; }

            public SignalSelect Signal => signalSelect.Value;
            public SourceSelect Source => sourceSelect.Value;
            public bool IgnoreSingleRequests => descriptor.ignoreSingleRequests;

            public bool DoneInterrupt { get; set; }
            public bool DoneInterruptEnable { get; set; }
            public bool IRQ => DoneInterrupt && DoneInterruptEnable;

            public DoubleWordRegister PeripheralRequestSelectRegister { get; }
            public DoubleWordRegister ConfigurationRegister { get; }
            public DoubleWordRegister LoopCounterRegister { get; }
            public DoubleWordRegister DescriptorControlWordRegister { get; }
            public DoubleWordRegister DescriptorSourceDataAddressRegister { get; }
            public DoubleWordRegister DescriptorDestinationDataAddressRegister { get; }
            public DoubleWordRegister DescriptorLinkStructureAddressRegister { get; }

            public bool Enabled
            {
                get
                {
                    return enabled;
                }
                set
                {
                    if(enabled == value)
                    {
                        return;
                    }
                    enabled = value;
                    if(enabled)
                    {
                        Done = false;
                        StartTransfer();
                    }
                }
            }

            public bool Done
            {
                get
                {
                    return done;
                }

                set
                {
                    done = value;
                    DoneInterrupt |= done && descriptor.operationDoneInterruptFlagSetEnable;
                }
            }

            public bool RequestDisable
            {
                get
                {
                    return requestDisable;
                }

                set
                {
                    if(requestDisable && !value)
                    {
                        requestDisable = value;
                        if(SignalIsOn)
                        {
                            StartTransfer();
                        }
                    }
                    requestDisable = value;
                }
            }

            private void StartTransferInner()
            {
                if(isInProgress || Done)
                {
                    return;
                }

                isInProgress = true;
                var loaded = false;
                do
                {
                    loaded = false;
                    Transfer();
                    if(Done && descriptor.link)
                    {
                        loaded = true;
                        LoadDescriptor();
                    }
                }
                while((descriptor.structureTransferRequest && loaded) || (!Done && SignalIsOn));
                isInProgress = false;
            }

            private void LoadDescriptor()
            {
                var address = LinkStructureAddress;
                if(descriptorAddress.HasValue && descriptor.linkMode == AddressingMode.Relative)
                {
                    address += descriptorAddress.Value;
                }
                var data = parent.sysbus.ReadBytes(address, DescriptorSize);
                descriptorAddress = address;
                descriptor = Packet.Decode<Descriptor>(data);
#if DEBUG
                parent.Log(LogLevel.Noisy, "Channel #{0} data {1}", Index, BitConverter.ToString(data));
                parent.Log(LogLevel.Debug, "Channel #{0} Loaded {1}", Index, descriptor.PrettyString);
#endif
            }

            private void Transfer()
            {
                switch(descriptor.structureType)
                {
                    case StructureType.Transfer:
                        var request = new Request(
                            source: new Place(descriptor.sourceAddress),
                            destination: new Place(descriptor.destinationAddress),
                            size: Bytes,
                            readTransferType: SizeAsTransferType,
                            writeTransferType: SizeAsTransferType,
                            sourceIncrementStep: SourceIncrement,
                            destinationIncrementStep: DestinationIncrement
                        );
                        parent.Log(LogLevel.Debug, "Channel #{0} Performing Transfer", Index);
                        parent.engine.IssueCopy(request);
                        if(descriptor.requestTransferModeSelect == RequestTransferMode.Block)
                        {
                            var blockSizeMultiplier = Math.Min(TransferCount, BlockSizeMultiplier);
                            if(blockSizeMultiplier == TransferCount)
                            {
                                Done = true;
                                descriptor.transferCount = 0;
                            }
                            else
                            {
                                descriptor.transferCount -= blockSizeMultiplier;
                            }
                            descriptor.sourceAddress += SourceIncrement * blockSizeMultiplier;
                            descriptor.destinationAddress += DestinationIncrement * blockSizeMultiplier;
                        }
                        else
                        {
                            Done = true;
                        }
                        break;
                    case StructureType.Synchronize:
                        parent.Log(LogLevel.Warning, "Channel #{0} Synchronize is not implemented.", Index);
                        break;
                    case StructureType.Write:
                        parent.Log(LogLevel.Warning, "Channel #{0} Write is not implemented.", Index);
                        break;
                    default:
                        parent.Log(LogLevel.Error, "Channel #{0} Invalid structure type value. No action was performed.", Index);
                        return;
                }
                parent.UpdateInterrupts();
            }

            private bool ShouldPullSignal
            {
                get
                {
                    // if this returns true for the selected source and signal
                    // then the signal will be periodically pulled instead of waiting
                    // for an rising edge
                    switch(Source)
                    {
                        case SourceSelect.None:
                            return false;
                        case SourceSelect.PRS:
                            switch(Signal)
                            {
                                case SignalSelect.PRSRequest0:
                                case SignalSelect.PRSRequest1:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.ADC0:
                            switch(Signal)
                            {
                                case SignalSelect.ADC0Single:
                                case SignalSelect.ADC0Scan:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.VDAC0:
                            switch(Signal)
                            {
                                case SignalSelect.VDAC0CH0:
                                case SignalSelect.VDAC0CH1:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.USART0:
                        case SourceSelect.USART2:
                        case SourceSelect.LEUART0:
                            switch(Signal)
                            {
                                case SignalSelect.USART0RxDataAvailable:
                                    return false;
                                case SignalSelect.USART0TxBufferLow:
                                case SignalSelect.USART0TxEmpty:
                                    return true;
                                default:
                                    goto default;
                            }
                        case SourceSelect.USART1:
                        case SourceSelect.USART3:
                            switch(Signal)
                            {
                                case SignalSelect.USART1RxDataAvailable:
                                case SignalSelect.USART1RxDataAvailableRight:
                                    return false;
                                case SignalSelect.USART1TxBufferLow:
                                case SignalSelect.USART1TxEmpty:
                                case SignalSelect.USART1TxBufferLowRight:
                                    return true;
                                default:
                                    goto default;
                            }
                        case SourceSelect.I2C0:
                        case SourceSelect.I2C1:
                            switch(Signal)
                            {
                                case SignalSelect.I2C0RxDataAvailable:
                                    return false;
                                case SignalSelect.I2C0TxBufferLow:
                                    return true;
                                default:
                                    goto default;
                            }
                        case SourceSelect.TIMER0:
                        case SourceSelect.WTIMER0:
                            switch(Signal)
                            {
                                case SignalSelect.TIMER0UnderflowOverflow:
                                case SignalSelect.TIMER0CaptureCompare0:
                                case SignalSelect.TIMER0CaptureCompare1:
                                case SignalSelect.TIMER0CaptureCompare2:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.TIMER1:
                        case SourceSelect.WTIMER1:
                            switch(Signal)
                            {
                                case SignalSelect.TIMER1UnderflowOverflow:
                                case SignalSelect.TIMER1CaptureCompare0:
                                case SignalSelect.TIMER1CaptureCompare1:
                                case SignalSelect.TIMER1CaptureCompare2:
                                case SignalSelect.TIMER1CaptureCompare3:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.PROTIMER:
                            switch(Signal)
                            {
                                case SignalSelect.PROTIMERPreCounterOverflow:
                                case SignalSelect.PROTIMERBaseCounterOverflow:
                                case SignalSelect.PROTIMERWrapCounterOverflow:
                                case SignalSelect.PROTIMERCaptureCompare0:
                                case SignalSelect.PROTIMERCaptureCompare1:
                                case SignalSelect.PROTIMERCaptureCompare2:
                                case SignalSelect.PROTIMERCaptureCompare3:
                                case SignalSelect.PROTIMERCaptureCompare4:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.MODEM:
                            switch(Signal)
                            {
                                case SignalSelect.MODEMDebug:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.AGC:
                            switch(Signal)
                            {
                                case SignalSelect.AGCReceivedSignalStrengthIndicator:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.MSC:
                            switch(Signal)
                            {
                                case SignalSelect.MSCWriteDataReady:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.CRYPTO0:
                        case SourceSelect.CRYPTO1:
                            switch(Signal)
                            {
                                case SignalSelect.CRYPTO0Data0Write:
                                case SignalSelect.CRYPTO0Data0XorWrite:
                                case SignalSelect.CRYPTO0Data0Read:
                                case SignalSelect.CRYPTO0Data1Write:
                                case SignalSelect.CRYPTO0Data1Read:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.CSEN:
                            switch(Signal)
                            {
                                case SignalSelect.CSENData:
                                case SignalSelect.CSENBaseline:
                                    return false;
                                default:
                                    goto default;
                            }
                        case SourceSelect.LESENSE:
                            switch(Signal)
                            {
                                case SignalSelect.LESENSEBufferDataAvailable:
                                    return false;
                                default:
                                    goto default;
                            }
                        default:
                            parent.Log(LogLevel.Error, "Channel #{0} Invalid Source (0x{1:X}) and Signal (0x{2:X}) pair.", Index, Source, Signal);
                            return false;
                    }
                }
            }

            private uint BlockSizeMultiplier
            {
                get
                {
                    switch(descriptor.blockSize)
                    {
                        case BlockSizeMode.Unit1:
                        case BlockSizeMode.Unit2:
                            return 1u << (byte)descriptor.blockSize;
                        case BlockSizeMode.Unit3:
                            return 3;
                        case BlockSizeMode.Unit4:
                            return 4;
                        case BlockSizeMode.Unit6:
                            return 6;
                        case BlockSizeMode.Unit8:
                            return 8;
                        case BlockSizeMode.Unit16:
                            return 16;
                        case BlockSizeMode.Unit32:
                        case BlockSizeMode.Unit64:
                        case BlockSizeMode.Unit128:
                        case BlockSizeMode.Unit256:
                        case BlockSizeMode.Unit512:
                        case BlockSizeMode.Unit1024:
                            return 1u << ((byte)descriptor.blockSize - 4);
                        case BlockSizeMode.All:
                            return TransferCount;
                        default:
                            parent.Log(LogLevel.Warning, "Channel #{0} Invalid Block Size Mode value.", Index);
                            return 0;
                    }
                }
            }

            private bool SignalIsOn
            {
                get
                {
                    var number = ((int)Source << 4) | (int)Signal;
                    return parent.signals.Contains(number) || (!IgnoreSingleRequests && parent.signals.Contains(number | 1 << 12));
                }
            }

            private uint TransferCount => (uint)descriptor.transferCount + 1;
            private ulong LinkStructureAddress => (ulong)descriptor.linkAddress << 2;

            private uint SourceIncrement => descriptor.sourceIncrement == IncrementMode.None ? 0u : ((1u << (byte)descriptor.size) << (byte)descriptor.sourceIncrement);
            private uint DestinationIncrement => descriptor.destinationIncrement == IncrementMode.None ? 0u : ((1u << (byte)descriptor.size) << (byte)descriptor.destinationIncrement);
            private TransferType SizeAsTransferType => (TransferType)(1 << (byte)descriptor.size);
            private int Bytes => (int)(descriptor.requestTransferModeSelect == RequestTransferMode.All ? TransferCount : Math.Min(TransferCount, BlockSizeMultiplier)) << (byte)descriptor.size;

            private Descriptor descriptor;
            private ulong? descriptorAddress;
            private bool requestDisable;
            private bool enabled;
            private bool done;

            // Accesses to sysubs may cause changes in signals, but we should ignore those during active transaction
            private bool isInProgress;

            private IEnumRegisterField<SignalSelect> signalSelect;
            private IEnumRegisterField<SourceSelect> sourceSelect;
            private IEnumRegisterField<ArbitrationSlotNumberMode> arbitrationSlotNumberSelect;
            private IEnumRegisterField<Sign> sourceAddressIncrementSign;
            private IEnumRegisterField<Sign> destinationAddressIncrementSign;
            private IValueRegisterField loopCounter;

            private readonly EFR32MG12_LDMA parent;
            private readonly LimitTimer pullTimer;

            protected readonly int DescriptorSize = Packet.CalculateLength<Descriptor>();

            private enum ArbitrationSlotNumberMode
            {
                One   = 0,
                Two   = 1,
                Four  = 2,
                Eight = 3,
            }

            private enum Sign
            {
                Positive = 0,
                Negative = 1,
            }

            protected enum StructureType : uint
            {
                Transfer    = 0,
                Synchronize = 1,
                Write       = 2,
            }

            protected enum BlockSizeMode : uint
            {
                Unit1    = 0,
                Unit2    = 1,
                Unit3    = 2,
                Unit4    = 3,
                Unit6    = 4,
                Unit8    = 5,
                Unit16   = 7,
                Unit32   = 9,
                Unit64   = 10,
                Unit128  = 11,
                Unit256  = 12,
                Unit512  = 13,
                Unit1024 = 14,
                All      = 15,
            }

            protected enum RequestTransferMode : uint
            {
                Block = 0,
                All   = 1,
            }

            protected enum IncrementMode : uint
            {
                One  = 0,
                Two  = 1,
                Four = 2,
                None = 3,
            }

            protected enum SizeMode : uint
            {
                Byte     = 0,
                HalfWord = 1,
                Word     = 2,
            }

            protected enum AddressingMode : uint
            {
                Absolute = 0,
                Relative = 1,
            }

            [LeastSignificantByteFirst]
            private struct Descriptor
            {
                public string PrettyString => $@"Descriptor {{
    structureType: {structureType},
    structureTransferRequest: {structureTransferRequest},
    transferCount: {transferCount + 1},
    byteSwap: {byteSwap},
    blockSize: {blockSize},
    operationDoneInterruptFlagSetEnable: {operationDoneInterruptFlagSetEnable},
    requestTransferModeSelect: {requestTransferModeSelect},
    decrementLoopCount: {decrementLoopCount},
    ignoreSingleRequests: {ignoreSingleRequests},
    sourceIncrement: {sourceIncrement},
    size: {size},
    destinationIncrement: {destinationIncrement},
    sourceAddressingMode: {sourceAddressingMode},
    destinationAddressingMode: {destinationAddressingMode},
    sourceAddress: 0x{sourceAddress:X},
    destinationAddress: 0x{destinationAddress:X},
    linkMode: {linkMode},
    link: {link},
    linkAddress: 0x{(linkAddress << 2):X}
}}";

// Some of this fields are read only via sysbus, but can be loaded from memory
#pragma warning disable 649
                [PacketField, Offset(doubleWords: 0, bits: 0), Width(2)]
                public StructureType structureType;
                [PacketField, Offset(doubleWords: 0, bits: 3), Width(1)]
                public bool structureTransferRequest;
                [PacketField, Offset(doubleWords: 0, bits: 4), Width(11)]
                public uint transferCount;
                [PacketField, Offset(doubleWords: 0, bits: 15), Width(1)]
                public bool byteSwap;
                [PacketField, Offset(doubleWords: 0, bits: 16), Width(4)]
                public BlockSizeMode blockSize;
                [PacketField, Offset(doubleWords: 0, bits: 20), Width(1)]
                public bool operationDoneInterruptFlagSetEnable;
                [PacketField, Offset(doubleWords: 0, bits: 21), Width(1)]
                public RequestTransferMode requestTransferModeSelect;
                [PacketField, Offset(doubleWords: 0, bits: 22), Width(1)]
                public bool decrementLoopCount;
                [PacketField, Offset(doubleWords: 0, bits: 23), Width(1)]
                public bool ignoreSingleRequests;
                [PacketField, Offset(doubleWords: 0, bits: 24), Width(2)]
                public IncrementMode sourceIncrement;
                [PacketField, Offset(doubleWords: 0, bits: 26), Width(2)]
                public SizeMode size;
                [PacketField, Offset(doubleWords: 0, bits: 28), Width(2)]
                public IncrementMode destinationIncrement;
                [PacketField, Offset(doubleWords: 0, bits: 30), Width(1)]
                public AddressingMode sourceAddressingMode;
                [PacketField, Offset(doubleWords: 0, bits: 31), Width(1)]
                public AddressingMode destinationAddressingMode;
                [PacketField, Offset(doubleWords: 1, bits: 0), Width(32)]
                public uint sourceAddress;
                [PacketField, Offset(doubleWords: 2, bits: 0), Width(32)]
                public uint destinationAddress;
                [PacketField, Offset(doubleWords: 3, bits: 0), Width(1)]
                public AddressingMode linkMode;
                [PacketField, Offset(doubleWords: 3, bits: 1), Width(1)]
                public bool link;
                [PacketField, Offset(doubleWords: 3, bits: 2), Width(30)]
                public uint linkAddress;
#pragma warning restore 649
            }
        }
    }
}
