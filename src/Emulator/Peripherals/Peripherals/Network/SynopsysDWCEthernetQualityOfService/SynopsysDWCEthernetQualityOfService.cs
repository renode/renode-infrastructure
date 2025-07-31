//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using PacketDotNet;

namespace Antmicro.Renode.Peripherals.Network
{
    public partial class SynopsysDWCEthernetQualityOfService : NetworkWithPHY, IMACInterface, IKnownSize
    {
        public SynopsysDWCEthernetQualityOfService(IMachine machine, long systemClockFrequency, ICPU cpuContext = null, BusWidth? dmaBusWidth = null) : base(machine)
        {
            if(dmaBusWidth.HasValue)
            {
                DMABusWidth = dmaBusWidth.Value;
            }
            else
            {
                this.WarningLog("DMA bus width not provided to an instance of {0}, defaulting to {1}", nameof(SynopsysDWCEthernetQualityOfService), nameof(BusWidth.Bits32));
                DMABusWidth = BusWidth.Bits32;
            }

            if(DMAChannelOffsets.Length < 1 || DMAChannelOffsets.Length > MaxDMAChannels)
            {
                throw new ConstructionException($"Invalid DMA channel count {DMAChannelOffsets.Length}. Expected value between 1 and {MaxDMAChannels}");
            }

            IRQ = new GPIO();
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            MAC1 = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            Bus = machine.GetSystemBus(this);
            this.CpuContext = cpuContext;

            dmaChannels = new DMAChannel[DMAChannelOffsets.Length];
            for(var i = 0; i < dmaChannels.Length; i++)
            {
                dmaChannels[i] = new DMAChannel(this, i, systemClockFrequency, SeparateDMAInterrupts);
            }

            rxIpcPacketCounterInterruptEnable = new IFlagRegisterField[NumberOfIpcCounters];
            rxIpcByteCounterInterruptEnable = new IFlagRegisterField[NumberOfIpcCounters];
            rxIpcPacketCounterInterrupt = new IFlagRegisterField[NumberOfIpcCounters];
            rxIpcByteCounterInterrupt = new IFlagRegisterField[NumberOfIpcCounters];
            rxIpcPacketCounter = new IValueRegisterField[NumberOfIpcCounters];
            rxIpcByteCounter = new IValueRegisterField[NumberOfIpcCounters];
            macAndMmcRegisters = new DoubleWordRegisterCollection(this, CreateRegisterMap());
            mtlRegisters = new DoubleWordRegisterCollection(this, CreateMTLRegisterMap());
            dmaRegisters = new DoubleWordRegisterCollection(this, CreateDMARegisterMap());
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            if(!rxEnable.Value)
            {
                this.Log(LogLevel.Debug, "Receive: Dropping frame {0}", frame);
                return;
            }

            var channelCount = dmaChannels.Length;

            // If only one channel is specified packetDuplicationControl and dmaChannelSelect fields will be null, so
            // route the packet to the only available channel and return early
            if(channelCount == 1)
            {
                dmaChannels[0].ReceiveFrame(frame);
                return;
            }

            if(packetDuplicationControl.Value)
            {
                // With packet duplication enabled each set bit in the DCS field encodes a DMA channel that should receive the frame
                BitHelper.ForeachActiveBit(dmaChannelSelect.Value, bit => dmaChannels[bit].ReceiveFrame(frame));
            }
            else
            {
                // DCS encodes a DMA channel number when packet duplication control is disabled
                var channel = (int)dmaChannelSelect.Value;
                if(channel >= channelCount)
                {
                    this.WarningLog("DMA channel number {0} is not in range of [0, {1}). Dropping packet", channel, channelCount);
                    return;
                }

                dmaChannels[channel].ReceiveFrame(frame);
            }
        }

        public override void Reset()
        {
            ResetRegisters();
            foreach(var channel in dmaChannels)
            {
                channel.Reset();
            }
            UpdateInterrupts();
        }

        public virtual long Size => 0xC00;
        [DefaultInterrupt]
        public GPIO IRQ { get; }
        public MACAddress MAC { get; set; }
        public MACAddress MAC0 => MAC;
        public MACAddress MAC1 { get; set; }

        public event Action<EthernetFrame> FrameReady;

        // Configuration options for derived classes

        // Offset at which each channel should start. This also determinates the amount of DMA channels
        protected virtual long[] DMAChannelOffsets => new long[]{ 0x100 };
        protected BusWidth DMABusWidth { get; private set; }
        protected virtual int RxQueueSize => 8192;
        protected virtual bool SeparateDMAInterrupts => false;

        private void SendFrame(EthernetFrame frame)
        {
            if(loopbackEnabled.Value)
            {
                ReceiveFrame(frame);
            }
            else
            {
                FrameReady?.Invoke(frame);
            }

            IncrementPacketCounter(txGoodPacketCounter, txGoodPacketCounterInterrupt);
            IncrementPacketCounter(txPacketCounter, txPacketCounterInterrupt);
            // one added to account for Start of Frame Delimiter (SFD)
            var byteCount = 1 + (uint)frame.Bytes.Length;
            IncrementByteCounter(txByteCounter, txByteCounterInterrupt, byteCount);
            IncrementByteCounter(txGoodByteCounter, txGoodByteCounterInterrupt, byteCount);
            if(frame.DestinationMAC.IsBroadcast)
            {
                IncrementPacketCounter(txBroadcastPacketCounter, txBroadcastPacketCounterInterrupt);
            }
            else if(frame.DestinationMAC.IsMulticast)
            {
                IncrementPacketCounter(txMulticastPacketCounter, txMulticastPacketCounterInterrupt);
            }
            else
            {
                IncrementPacketCounter(txUnicastPacketCounter, txUnicastPacketCounterInterrupt);
            }
#if DEBUG
            this.Log(LogLevel.Noisy, "Transmission: frame {0}", Misc.PrettyPrintCollectionHex(frame.Bytes));
            this.Log(LogLevel.Debug, "Transmission: frame {0}", frame);
#endif
        }

        private void UpdateRxCounters(EthernetFrame frame, RxDescriptor.NormalWriteBackDescriptor writeBackStructure)
        {
            if(writeBackStructure.crcError)
            {
                IncrementPacketCounter(rxCrcErrorPacketCounter, rxCrcErrorPacketCounterInterrupt);
            }
            IncrementPacketCounter(rxPacketCounter, rxPacketCounterInterrupt);
            // byte count excludes preamble, one added to account for Start of Frame Delimiter (SFD)
            var byteCount = 1 + writeBackStructure.packetLength;
            IncrementByteCounter(rxByteCounter, rxByteCounterInterrupt, byteCount);

            var isRuntPacket = writeBackStructure.packetLength <= EthernetFrame.RuntPacketMaximumSize;
            var lengthOutOfRange = frame.Length > EthernetFrame.MaximumFrameSize;
            var isNontypePacket = (writeBackStructure.lengthTypeField != PacketKind.TypePacket);
            var lengthMismatch = (frame.Length != writeBackStructure.packetLength);

            var lengthError = isNontypePacket && lengthMismatch;
            var outOfRange = isNontypePacket && lengthOutOfRange;
            if(writeBackStructure.crcError || isRuntPacket || lengthError || outOfRange)
            {
                return;
            }

            IncrementByteCounter(rxGoodByteCounter, rxGoodByteCounterInterrupt, byteCount);
            if(frame.DestinationMAC.IsBroadcast)
            {
                IncrementPacketCounter(rxBroadcastPacketCounter, rxBroadcastPacketCounterInterrupt);
            }
            else if(frame.DestinationMAC.IsMulticast)
            {
                IncrementPacketCounter(rxMulticastPacketCounter, rxMulticastPacketCounterInterrupt);
            }
            else
            {
                IncrementPacketCounter(rxUnicastPacketCounter, rxUnicastPacketCounterInterrupt);
            }

            if(writeBackStructure.ipv4HeaderPresent)
            {
                IncreaseIpcCounter(IpcCounter.IpV4Good, byteCount);
                if(writeBackStructure.ipHeaderError)
                {
                    IncreaseIpcCounter(IpcCounter.IpV4HeaderError, byteCount);
                }
            }
            if(writeBackStructure.ipv6HeaderPresent)
            {
                IncreaseIpcCounter(IpcCounter.IpV6Good, byteCount);
                if(writeBackStructure.ipHeaderError)
                {
                    IncreaseIpcCounter(IpcCounter.IpV6HeaderError, byteCount);
                }
            }
            if(writeBackStructure.payloadType ==  PayloadType.UDP)
            {
                IncreaseIpcCounter(IpcCounter.UdpGood, byteCount);
            }
            else if(writeBackStructure.payloadType == PayloadType.TCP)
            {
                IncreaseIpcCounter(IpcCounter.TcpGood, byteCount);
            }
            else if(writeBackStructure.payloadType == PayloadType.ICMP)
            {
                IncreaseIpcCounter(IpcCounter.IcmpGood, byteCount);
            }
        }

        private void IncrementPacketCounter(IValueRegisterField counter, IFlagRegisterField status)
        {
            counter.Value += 1;
            status.Value |= EqualsHalfOrMaximumCounterValue(counter);
        }

        private void IncrementByteCounter(IValueRegisterField counter, IFlagRegisterField status, uint increment)
        {
            status.Value |= WouldExceedHalfOrMaximumCounterValue(counter, increment);
            counter.Value += increment;
        }

        private void IncreaseIpcCounter(IpcCounter type, uint byteCount)
        {
            var index = (int)type;
            IncrementPacketCounter(rxIpcPacketCounter[index], rxIpcPacketCounterInterrupt[index]);
            IncrementByteCounter(rxIpcByteCounter[index], rxIpcByteCounterInterrupt[index], byteCount);
        }

        private bool CheckAnySetIpcCounterInterrupts()
        {
            for(var i = 0; i < NumberOfIpcCounters; i++)
            {
                if((rxIpcPacketCounterInterrupt[i].Value && rxIpcPacketCounterInterruptEnable[i].Value) || (rxIpcByteCounterInterrupt[i].Value && rxIpcByteCounterInterruptEnable[i].Value))
                {
                    return true;
                }
            }
            return false;
        }

        private bool EqualsHalfOrMaximumCounterValue(IValueRegisterField counter)
        {
            return (counter.Value == CounterMaxValue) || (counter.Value == (CounterMaxValue / 2));
        }

        private bool WouldExceedHalfOrMaximumCounterValue(IValueRegisterField counter, uint increment)
        {
            return (counter.Value < CounterMaxValue && counter.Value + increment >= CounterMaxValue)
                || (counter.Value < CounterMaxValue / 2 && counter.Value + increment >= CounterMaxValue / 2);
        }

        private void UpdateInterrupts()
        {
            var dmaInterrupt = false;
            foreach(var channel in dmaChannels)
            {
                dmaInterrupt |= channel.UpdateInterrupts();
            }

            var irq = (ptpMessageTypeInterrupt.Value && ptpMessageTypeInterruptEnable.Value)   ||
                      (lowPowerIdleInterrupt.Value && lowPowerIdleInterruptEnable.Value)       ||
                      (timestampInterrupt.Value && timestampInterruptEnable.Value)             ||
                      dmaInterrupt                                                             ||
                      MMCTxInterruptStatus                                                     ||
                      MMCRxInterruptStatus                                                     ||
                      CheckAnySetIpcCounterInterrupts();

            this.Log(LogLevel.Noisy, "Setting IRQ: {0}", irq);
            IRQ.Set(irq);
        }

        private IBusController Bus { get; }
        private ICPU CpuContext { get; }

        private bool MMCTxInterruptStatus =>
            (txGoodPacketCounterInterrupt.Value && txGoodPacketCounterInterruptEnable.Value)           ||
            (txGoodByteCounterInterrupt.Value && txGoodByteCounterInterruptEnable.Value)               ||
            (txBroadcastPacketCounterInterrupt.Value && txBroadcastPacketCounterInterruptEnable.Value) ||
            (txMulticastPacketCounterInterrupt.Value && txMulticastPacketCounterInterruptEnable.Value) ||
            (txUnicastPacketCounterInterrupt.Value && txUnicastPacketCounterInterruptEnable.Value)     ||
            (txPacketCounterInterrupt.Value && txPacketCounterInterruptEnable.Value)                   ||
            (txByteCounterInterrupt.Value && txByteCounterInterruptEnable.Value);

        private bool MMCRxInterruptStatus =>
            (rxFifoPacketCounterInterrupt.Value && rxFifoPacketCounterInterruptEnable.Value)           ||
            (rxUnicastPacketCounterInterrupt.Value && rxUnicastPacketCounterInterruptEnable.Value)     ||
            (rxCrcErrorPacketCounterInterrupt.Value && rxCrcErrorPacketCounterInterruptEnable.Value)   ||
            (rxMulticastPacketCounterInterrupt.Value && rxMulticastPacketCounterInterruptEnable.Value) ||
            (rxBroadcastPacketCounterInterrupt.Value && rxBroadcastPacketCounterInterruptEnable.Value) ||
            (rxGoodByteCounterInterrupt.Value && rxGoodByteCounterInterruptEnable.Value)               ||
            (rxByteCounterInterrupt.Value && rxByteCounterInterruptEnable.Value)                       ||
            (rxPacketCounterInterrupt.Value && rxPacketCounterInterruptEnable.Value);

        private const ulong CounterMaxValue = UInt32.MaxValue;
        private const int RxWatchdogDivider = 256;
        private const uint EtherTypeMinimalValue = 0x600;

        // This value may be increased if required, but some changes in register creation may be required
        private const int MaxDMAChannels = 3;
    }
}
