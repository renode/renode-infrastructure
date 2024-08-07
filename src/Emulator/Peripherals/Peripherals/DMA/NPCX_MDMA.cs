//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class NPCX_MDMA : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver
    {
        public NPCX_MDMA(IMachine machine, uint sourceAddress, uint destinationAddress) : base(machine)
        {
            IRQ = new GPIO();
            channelContexts = new DMAChannelContext[ChannelCount];

            DefineRegisters(sourceAddress, destinationAddress);
            Reset();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number < 0 && number > ChannelCount)
            {
                this.ErrorLog("Invalid channel number: {0}. Expected value between 0 and {1}", number, ChannelCount - 1);
            }

            if(value)
            {
                channelContexts[number].TryPerformCopy();
            }
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();
            foreach(var context in channelContexts)
            {
                context.Reset();
            }
        }

        public long Size => 0x100;

        public GPIO IRQ { get; }

        private void DefineRegisters(uint sourceAddress, uint destinationAddress)
        {
            for(var i = 0; i < ChannelCount; i++)
            {
                var channelIdx = i;
                
                DMAChannelContext context;
                switch(channelIdx)
                {
                case 0:
                    context = DefineChannel0Registers(sourceAddress);
                    break;
                case 1:
                    context = DefineChannel1Registers(destinationAddress);
                    break;
                default:
                    throw new Exception($"Invalid channel count value of {channelIdx}. Expected value between 0 and {ChannelCount - 1}");
                }

                channelContexts[channelIdx] = context;
                var channelOffset = channelIdx * 0x20;

                (Registers.Channel0Control + channelOffset).Define(this)
                    .WithFlag(0, out context.Enabled, name: "MDMAEN (MDMA Enable)",
                        writeCallback: (previous, value) =>
                            {
                                if(!value)
                                {
                                    if(previous)
                                    {
                                        this.WarningLog("Trying to disable the DMA channel {0} while a transaction is in progress. Ignoring", channelIdx);
                                        context.Enabled.Value = true;
                                    }
                                    return;
                                }

                                this.DebugLog("DMA Channel {0} {1}", channelIdx, value ? "enabled" : "disabled");
                                if(context.Direction == DMAChannelContext.TransferDirection.ToPeripheral)
                                {
                                    // Only process the DMA transfer request for ToPeripheral channels
                                    // as FromPeripheral transfers have to be explicitly requested by the peripheral
                                    context.TryPerformCopy();
                                }
                            })
                    .WithTag("MPD (MDMA Power-Down)", 1, 1)
                    .WithReservedBits(2, 6)
                    .WithFlag(8, out context.InterruptEnable, name: "SIEN (Stop Interrupt Enable)",
                        writeCallback: (_, __) => UpdateInterrupt())
                    .WithReservedBits(9, 5)
                    .WithTag("MPS (MDMA Power Save)", 14, 1)
                    .WithReservedBits(15, 3)
                    .WithFlag(18, out context.IsFinished, FieldMode.Read | FieldMode.WriteZeroToClear,
                        name: "TC (Terminal Count)", writeCallback: (_, __) => UpdateInterrupt())
                    .WithReservedBits(19, 13);

                (Registers.Channel0TransferCount + channelOffset).Define(this)
                    .WithValueField(0, 13, name: "TFR_CNT (13-bit Transfer Count)",
                        valueProviderCallback: _ => context.TransferCount,
                        writeCallback: (_, value) =>
                            {
                                if(value > MaxTransferCount)
                                {
                                    this.WarningLog("Maximum transfer count is {0} bytes. Got {1}", MaxTransferCount, value);
                                    value = MaxTransferCount;
                                }
                                context.TransferCount = (ushort)value;
                                context.CurrentTransferCount = (ushort)value;
                            })
                    .WithReservedBits(13, 19);

                (Registers.Channel0CurrentTransferCount + channelOffset).Define(this)
                    .WithValueField(0, 13, FieldMode.Read, name: "CURENT_TFR_CNT (13-bit Current Transfer Count)",
                        valueProviderCallback: _ => context.CurrentTransferCount)
                    .WithReservedBits(13, 19);
            }
        }

        private DMAChannelContext DefineChannel0Registers(uint sourceAddress)
        {
            var context = new DMAChannelContext(this, sourceAddress, DefaultMemoryAddress, DMAChannelContext.TransferDirection.FromPeripheral);

            Registers.Channel0SourceBaseAddress.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "SRC_BASE_ADDR (32-bit Source Base Address)",
                    valueProviderCallback: _ => context.SourceAddress);

            Registers.Channel0DestinationBaseAddress.Define(this)
                .WithValueField(0, 20, name: "DST_BASE_ADDR19-0 (20-bit Destination Base Address)",
                    valueProviderCallback: _ => BitHelper.GetValue(context.DestinationAddress, 0, 20),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref context.DestinationAddress, (uint)value, 0, 20))
                .WithValueField(20, 12, FieldMode.Read, name: "DST_BASE_ADDR31-20 (32-bit Destination Base Address)",
                    valueProviderCallback: _ => BitHelper.GetValue(context.DestinationAddress, 20, 12));

            Registers.Channel0CurrentDestination.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "CURRENT_DST_ADDR (32-bit Current Destination Address)",
                    valueProviderCallback: _ => context.DestinationAddress + context.CurrentTransferCount);

            return context;
        }

        private DMAChannelContext DefineChannel1Registers(uint destinationAddress)
        {
            var context = new DMAChannelContext(this, DefaultMemoryAddress, destinationAddress, DMAChannelContext.TransferDirection.ToPeripheral);

            Registers.Channel1SourceBaseAddress.Define(this)
                .WithValueField(0, 20, name: "SRC_BASE_ADDR19-0 (20-bit Source Base Address)",
                    valueProviderCallback: _ => BitHelper.GetValue(context.SourceAddress, 0, 20),
                    writeCallback: (_, value) => BitHelper.SetMaskedValue(ref context.SourceAddress, (uint)value, 0, 20))
                .WithValueField(20, 12, FieldMode.Read, name: "SRC_BASE_ADDR31-20 (12-bit Source Base Address)",
                    valueProviderCallback: _ => BitHelper.GetValue(context.SourceAddress, 20, 12));

            Registers.Channel1DestinationBaseAddress.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "DST_BASE_ADDR (32-bit Destination Base Address)",
                    valueProviderCallback: _ => context.DestinationAddress);

            Registers.Channel1CurrentSource.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "CURRENT_SRC_ADDR (32-bit Current Source Address)",
                    // Since ToPeripheral transfers happen instantly current source address will always be equal to
                    // to base source address
                    valueProviderCallback: _ => context.SourceAddress);

            return context;
        }

        private void UpdateInterrupt()
        {
            var state = false;
            foreach(var context in channelContexts)
            {
                state |= context.IsFinished.Value && context.InterruptEnable.Value;
            }
            this.DebugLog("{0} interrupt", state ? "Setting" : "Unsetting");
            IRQ.Set(state);
        }

        private enum Registers
        {
            Channel0Control = 0x0,
            Channel0SourceBaseAddress = 0x4,
            Channel0DestinationBaseAddress = 0x8,
            Channel0TransferCount = 0xC,
            Channel0CurrentDestination = 0x14,
            Channel0CurrentTransferCount = 0x18,
            Channel1Control = 0x20,
            Channel1SourceBaseAddress = 0x24,
            Channel1DestinationBaseAddress = 0x28,
            Channel1TransferCount = 0x2C,
            Channel1CurrentSource = 0x30,
            Channel1CurrentTransferCount = 0x38
        }

        private readonly DMAChannelContext[] channelContexts;

        private const int ChannelCount = 2;
        private const int MaxTransferCount = 4096;
        private const int DefaultMemoryAddress = 0x10000000;

        private class DMAChannelContext
        {
            public DMAChannelContext(NPCX_MDMA owner, uint sourceAddress, uint destinationAddress, TransferDirection direction)
            {
                this.owner = owner;
                defaultSourceAddress = sourceAddress;
                defaultDestinationAddress = destinationAddress;
                this.Direction = direction;

                engine = new DmaEngine(owner.machine.GetSystemBus(owner));

                Reset();
            }

            public void TryPerformCopy()
            {
                if(!Enabled.Value)
                {
                    owner.ErrorLog("Attempted to perform a DMA transaction without enabling the channel");
                    return;
                }

                Request request;

                switch(Direction)
                {
                case TransferDirection.FromPeripheral:
                    // FromPeripheral transfers are performed 1 byte at a time, since from the perspective of the MDMA
                    // device there is no way to tell if the peripheral's receive buffer contains valid data.
                    // The peripheral should notify the MDMA that data has been placed into the receive buffer via Renode's
                    // GPIO mechanism
                    request = new Request(
                        new Place(SourceAddress),
                        new Place(DestinationAddress + (uint)(TransferCount - CurrentTransferCount)),
                        1, TransferType.Byte, TransferType.Byte,
                        incrementReadAddress: false,
                        incrementWriteAddress: false
                    );

                    CurrentTransferCount--;
                    break;
                case TransferDirection.ToPeripheral:
                    request = new Request(
                        new Place(SourceAddress),
                        new Place(DestinationAddress),
                        TransferCount, TransferType.Byte, TransferType.Byte,
                        incrementReadAddress: true,
                        incrementWriteAddress: false
                    );

                    CurrentTransferCount = 0;
                    break;
                default:
                    throw new Exception($"Invalid {nameof(TransferDirection)} value: {Direction}");
                }

                engine.IssueCopy(request);
                if(CurrentTransferCount == 0)
                {
                    Enabled.Value = false;
                    IsFinished.Value = true;
                    owner.UpdateInterrupt();
                }
            }

            public void Reset()
            {
                TransferCount = 0;
                CurrentTransferCount = 0;
                SourceAddress = defaultSourceAddress;
                DestinationAddress = defaultDestinationAddress;
            }

            public TransferDirection Direction { get; }
            public ushort TransferCount { get; set; }
            public ushort CurrentTransferCount { get; set; }

            public IFlagRegisterField Enabled;
            public IFlagRegisterField IsFinished;
            public IFlagRegisterField InterruptEnable;

            public uint SourceAddress;
            public uint DestinationAddress;

            private readonly uint defaultSourceAddress;
            private readonly uint defaultDestinationAddress;

            public enum TransferDirection
            {
                FromPeripheral,
                ToPeripheral,
            }

            private readonly NPCX_MDMA owner;
            private readonly DmaEngine engine;
        }
    }
}
