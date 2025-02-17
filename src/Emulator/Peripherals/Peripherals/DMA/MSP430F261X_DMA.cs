//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Memory
{
    public class MSP430F261X_DMA : BasicWordPeripheral
    {
        public MSP430F261X_DMA(IMachine machine) : base(machine)
        {
            ChannelRegisterCollections = new WordRegisterCollection(this);

            DefineRegisters();
            DefineChannelRegisters();
        }

        [ConnectionRegionAttribute("channelRegisters")]
        public void WriteWordToChannelRegisters(long offset, ushort value)
        {
            ChannelRegisterCollections.Write(offset, value);
        }

        [ConnectionRegionAttribute("channelRegisters")]
        public ushort ReadWordFromChannelRegisters(long offset)
        {
            return ChannelRegisterCollections.Read(offset);
        }

        public WordRegisterCollection ChannelRegisterCollections { get; }

        public GPIO IRQ { get; } = new GPIO();

        private int GetIncrement(IncrementMode incrementMode, bool byteTransfer)
        {
            int incrementSign;
            switch(incrementMode)
            {
                case IncrementMode.Ignored0:
                case IncrementMode.Ignored1:
                    incrementSign = 0;
                    break;
                case IncrementMode.Increment:
                    incrementSign = 1;
                    break;
                case IncrementMode.Decrement:
                    incrementSign = -1;
                    break;
                default:
                    throw new Exception("unreachable");
            }

            return incrementSign * (byteTransfer ? 1 : 2);
        }

        private void PerformTransfer(int channelIndex)
        {
            var toTransfer = (transferMode[channelIndex].Value == TransferMode.Single || transferMode[channelIndex].Value == TransferMode.Repeated) ? 1 : transferSize[channelIndex].Value;

            var sourceIncrement = GetIncrement(sourceIncrementMode[channelIndex].Value, sourceByteTransfer[channelIndex].Value);
            var destinationIncrement = GetIncrement(destinationIncrementMode[channelIndex].Value, destinationByteTransfer[channelIndex].Value);

            while(toTransfer-- > 0)
            {
                var sourceValue = sourceByteTransfer[channelIndex].Value ? machine.SystemBus.ReadByte((ulong)sourceAddress[channelIndex]) : machine.SystemBus.ReadWord((ulong)sourceAddress[channelIndex]);
                if(destinationByteTransfer[channelIndex].Value)
                {
                    machine.SystemBus.WriteByte((ulong)destinationAddress[channelIndex], (byte)sourceValue);
                }
                else
                {
                    machine.SystemBus.WriteWord((ulong)destinationAddress[channelIndex], sourceValue);
                }

                sourceAddress[channelIndex] = sourceAddress[channelIndex] + sourceIncrement;
                destinationAddress[channelIndex] = destinationAddress[channelIndex] + destinationIncrement;
                transferSize[channelIndex].Value -= 1;
            }

            if(transferSize[channelIndex].Value == 0)
            {
                transferSize[channelIndex].Value = transferSizeCached[channelIndex];
                interruptPending[channelIndex].Value = true;
                UpdateInterrupts();
            }
        }

        private void UpdateInterrupts()
        {
            var interrupt = interruptEnabled.Zip(interruptPending, (enabled, pending) => enabled.Value && pending.Value).Any();
            this.Log(LogLevel.Debug, "IRQ set to {0}", interrupt);
            IRQ.Set(interrupt);
        }

        private void DefineRegisters()
        {
            Registers.Control0.Define(this)
                .WithEnumFields(0, 4, 3, out triggerSelect, name: "DMA_TSELx",
                    changeCallback: (index, _, value) =>
                    {
                        if(value != TriggerSelect.DMAREQ)
                        {
                            this.Log(LogLevel.Warning, "DMA_TSEL{0} changed to {1} but only DMAREQ trigger (software trigger) is supported", index, value);
                        }
                    })
                .WithReservedBits(12, 4)
            ;

            Registers.Control1.Define(this)
                .WithTaggedFlag("ENNMI", 0)
                .WithTaggedFlag("ROUNDROBIN", 1)
                .WithTaggedFlag("DMAONFETCH", 2)
                .WithReservedBits(3, 13)
            ;

            Registers.InterruptVector.Define(this)
                .WithValueField(0, 16, name: "DMAIVx",
                    valueProviderCallback: _ =>
                        interruptPending.Select((pending, index) => pending.Value ? (ulong)index + 1 : 0).FirstOrDefault(index => index > 0) << 1)
            ;
        }

        private void DefineChannelRegisters()
        {
            ChannelRegisters.ChannelControl0.DefineMany(ChannelRegisterCollections, ChannelsCount, (register, index) =>
                register
                    .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMAREQ",
                        writeCallback: (_, value) => { if(value) PerformTransfer(index); } )
                    .WithTaggedFlag("DMAABORT", 1)
                    .WithFlag(2, out interruptEnabled[index], name: "DMAIE")
                    .WithFlag(3, out interruptPending[index], name: "DMAIFG")
                    .WithFlag(4, out channelEnabled[index], name: "DMAEN")
                    .WithTaggedFlag("DMALEVEL", 5)
                    .WithFlag(6, out sourceByteTransfer[index], name: "DMASRCBYTE")
                    .WithFlag(7, out destinationByteTransfer[index], name: "DMADSTBYTE")
                    .WithEnumField(8, 2, out sourceIncrementMode[index], name: "DMASRCINCRx")
                    .WithEnumField(10, 2, out destinationIncrementMode[index], name: "DMADSTINCRx")
                    .WithEnumField(12, 3, out transferMode[index], name: "DMADTx")
                    .WithReservedBits(15, 1)
                    .WithChangeCallback((_, __) => UpdateInterrupts()),
                stepInBytes: ChannelStructureSize);

            ChannelRegisters.ChannelSource0.DefineMany(ChannelRegisterCollections, ChannelsCount, (register, index) =>
                register
                    .WithValueField(0, 16, name: "DMAxSA",
                        valueProviderCallback: _ => (ulong)sourceAddress[index],
                        writeCallback: (_, value) => sourceAddress[index] = (long)value),
                stepInBytes: ChannelStructureSize);

            ChannelRegisters.ChannelDestination0.DefineMany(ChannelRegisterCollections, ChannelsCount, (register, index) =>
                register
                    .WithValueField(0, 16, name: "DMAxDA",
                        valueProviderCallback: _ => (ulong)destinationAddress[index],
                        writeCallback: (_, value) => destinationAddress[index] = (long)value),
                stepInBytes: ChannelStructureSize);

            ChannelRegisters.ChannelTransferSize0.DefineMany(ChannelRegisterCollections, ChannelsCount, (register, index) =>
                register
                    .WithValueField(0, 16, out transferSize[index], name: "DMAxSZ",
                        writeCallback: (_, value) => transferSizeCached[index] = value),
                stepInBytes: ChannelStructureSize);
        }

        private long[] sourceAddress = new long[ChannelsCount];
        private long[] destinationAddress = new long[ChannelsCount];
        private ulong[] transferSizeCached = new ulong[ChannelsCount];

        private IEnumRegisterField<TriggerSelect>[] triggerSelect = new IEnumRegisterField<TriggerSelect>[ChannelsCount];
        private IEnumRegisterField<TransferMode>[] transferMode = new IEnumRegisterField<TransferMode>[ChannelsCount];
        private IEnumRegisterField<IncrementMode>[] sourceIncrementMode = new IEnumRegisterField<IncrementMode>[ChannelsCount];
        private IEnumRegisterField<IncrementMode>[] destinationIncrementMode = new IEnumRegisterField<IncrementMode>[ChannelsCount];

        private IValueRegisterField[] transferSize = new IValueRegisterField[ChannelsCount];

        private IFlagRegisterField[] interruptEnabled = new IFlagRegisterField[ChannelsCount];
        private IFlagRegisterField[] interruptPending = new IFlagRegisterField[ChannelsCount];
        private IFlagRegisterField[] channelEnabled = new IFlagRegisterField[ChannelsCount];
        private IFlagRegisterField[] sourceByteTransfer = new IFlagRegisterField[ChannelsCount];
        private IFlagRegisterField[] destinationByteTransfer = new IFlagRegisterField[ChannelsCount];

        private const int ChannelsCount = 3;
        private const int ChannelStructureSize = 0xC;

        private enum TransferMode
        {
            Single,
            Block,
            BurstBlock0,
            BurstBlock1,
            Repeated,
            RepeatedBlock,
            RepeatedBurstBlock0,
            RepeatedBurstBlock1,
        }

        private enum IncrementMode
        {
            Ignored0,
            Ignored1,
            Decrement,
            Increment,
        }

        private enum TriggerSelect
        {
            DMAREQ,
            TACCR2,
            TBCCR2,
            UCA0RXIFG,
            UCA0TXIFG,
            DAC12,
            ADC12,
            TACCR0,
            TBCCR0,
            UCA1RXIFG,
            UCA1TXIFG,
            Multiplier,
            UCB0RXIFG,
            UCB0TXIFG,
            Chained,
            External,
        }

        private enum Registers
        {
            Control0 = 0x00,
            Control1 = 0x02,
            InterruptVector = 0x04,
        }

        private enum ChannelRegisters
        {
            ChannelControl0 = 0x00,
            ChannelSource0 = 0x02,
            ChannelDestination0 = 0x06,
            ChannelTransferSize0 = 0x0A,

            ChannelControl1 = 0x0C,
            ChannelSource1 = 0x0E,
            ChannelDestination1 = 0x12,
            ChannelTransferSize1 = 0x16,

            ChannelControl2 = 0x18,
            ChannelSource2 = 0x1A,
            ChannelDestination2 = 0x1E,
            ChannelTransferSize2 = 0x22,
        }
    }
}
