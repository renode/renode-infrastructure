//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Net;

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
        public SynopsysDWCEthernetQualityOfService(IMachine machine, long systemClockFrequency, ICPU cpuContext = null, BusWidth? dmaBusWidth = null, long? ptpClockFrequency = null,
            int rxQueueSize = 8192, int txQueueSize = 8192) : base(machine)
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

            ValidateQueueSize(rxQueueSize, nameof(rxQueueSize));
            RxQueueSize = rxQueueSize;
            ValidateQueueSize(txQueueSize, nameof(txQueueSize));
            TxQueueSize = txQueueSize;

            IRQ = new GPIO();
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            MAC1 = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            Bus = machine.GetSystemBus(this);
            this.CpuContext = cpuContext;
            this.ptpClockFrequency = ptpClockFrequency ?? systemClockFrequency;
            timestampSubsecondTimer = new LimitTimer(machine.ClockSource, this.ptpClockFrequency, this, "Timestamp timer", BinarySubsecondRollover, Direction.Ascending, eventEnabled: true);
            timestampSubsecondTimer.LimitReached += () => timestampSecondTimer += 1;

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
            SoftwareReset();
            timestampSecondTimer = 0;
            timestampSubsecondTimer.Reset();
            UpdateInterrupts();
        }

        public virtual long Size => 0xC00;

        [DefaultInterrupt]
        public GPIO IRQ { get; }

        public MACAddress MAC { get; set; }

        public MACAddress MAC0 => MAC;

        public MACAddress MAC1 { get; set; }

        public byte IPVersion { get; set; } = 0x42;

        public byte UserIPVersion { get; set; } = 0x31;

        public event Action<EthernetFrame> FrameReady;

        // Configuration options for derived classes

        // Offset at which each channel should start. This also determinates the amount of DMA channels
        protected virtual long[] DMAChannelOffsets => new long[] { 0x100 };

        protected BusWidth DMABusWidth { get; private set; }

        protected int RxQueueSize { get; private set; }

        protected int TxQueueSize { get; private set; }

        protected virtual bool SeparateDMAInterrupts => false;

        private void SoftwareReset()
        {
            foreach(var channel in dmaChannels)
            {
                channel.Reset();
            }
            UpdateInterrupts();
        }

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
            if(writeBackStructure.CrcError)
            {
                IncrementPacketCounter(rxCrcErrorPacketCounter, rxCrcErrorPacketCounterInterrupt);
            }
            IncrementPacketCounter(rxPacketCounter, rxPacketCounterInterrupt);
            // byte count excludes preamble, one added to account for Start of Frame Delimiter (SFD)
            var byteCount = 1 + writeBackStructure.PacketLength;
            IncrementByteCounter(rxByteCounter, rxByteCounterInterrupt, byteCount);

            var isRuntPacket = writeBackStructure.PacketLength <= EthernetFrame.RuntPacketMaximumSize;
            var lengthOutOfRange = frame.Length > EthernetFrame.MaximumFrameSize;
            var isNontypePacket = (writeBackStructure.LengthTypeField != PacketKind.TypePacket);
            var lengthMismatch = (frame.Length != writeBackStructure.PacketLength);

            var lengthError = isNontypePacket && lengthMismatch;
            var outOfRange = isNontypePacket && lengthOutOfRange;
            if(writeBackStructure.CrcError || isRuntPacket || lengthError || outOfRange)
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

            if(writeBackStructure.Ipv4HeaderPresent)
            {
                IncreaseIpcCounter(IpcCounter.IpV4Good, byteCount);
                if(writeBackStructure.IpHeaderError)
                {
                    IncreaseIpcCounter(IpcCounter.IpV4HeaderError, byteCount);
                }
            }
            if(writeBackStructure.Ipv6HeaderPresent)
            {
                IncreaseIpcCounter(IpcCounter.IpV6Good, byteCount);
                if(writeBackStructure.IpHeaderError)
                {
                    IncreaseIpcCounter(IpcCounter.IpV6HeaderError, byteCount);
                }
            }
            if(writeBackStructure.PayloadType == PayloadType.UDP)
            {
                IncreaseIpcCounter(IpcCounter.UdpGood, byteCount);
            }
            else if(writeBackStructure.PayloadType == PayloadType.TCP)
            {
                IncreaseIpcCounter(IpcCounter.TcpGood, byteCount);
            }
            else if(writeBackStructure.PayloadType == PayloadType.ICMP)
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

        private void GetTimestamp(out ulong seconds, out ulong nanoseconds)
        {
            if(enableTimestamp.Value && Bus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }

            seconds = timestampSecondTimer;
            // `SSINC` dictates the accuracy of the subsecond value
            nanoseconds = (timestampSubsecondTimer.Value / timestampSubsecondIncrement.Value) * timestampSubsecondIncrement.Value;
        }

        private void SetTimestamp(ulong seconds, ulong nanoseconds, bool init)
        {
            if(!init)
            {
                GetTimestamp(out var currentSeconds, out var currentNanoseconds);
                if(addOrSubtractTime.Value)
                {
                    currentSeconds -= (1ul << 32) - seconds;
                    if(timestampDigitalOrBinaryRollover.Value)
                    {
                        // When TSCTRLSSR field in the Timestamp control Register is 1 the programmed value must be 10^9 – <subsecond value>
                        currentNanoseconds -= (ulong)Math.Pow(10, 9) - nanoseconds;
                    }
                    else
                    {
                        // When TSCTRLSSR field in the Timestamp control Register is 1 the programmed value must be 2^31 - <subsecond_value>
                        currentNanoseconds -= (1ul << 31) - nanoseconds;
                    }
                }
                else
                {
                    currentSeconds += seconds;
                    currentNanoseconds += nanoseconds;
                }
                seconds = currentSeconds;
                nanoseconds = currentNanoseconds;
            }

            ConfigureTimestampTimer();
            timestampSecondTimer = (uint)seconds;
            timestampSubsecondTimer.Value = nanoseconds;
            if(init)
            {
                timestampSubsecondTimer.Enabled = true;
            }
        }

        private void ConfigureTimestampTimer()
        {
            long effectiveFrequency;
            // Check for values that would cause an invalid operation, either:
            // * Timer frequency equal to 0Hz
            // * Division by 0
            if((timestampAddend.Value != 0 || !fineOrCoarseTimestampUpdate.Value) && timestampSubsecondIncrement.Value != 0)
            {
                // Subsecond increment (SSINC) controls by what value the timestamp timer is incremented each clock cycle.
                // This is implemented by multiplying the frequency of Renode's timer by SSINC.
                var subsecondTicksPerSecond = ptpClockFrequency * (long)timestampSubsecondIncrement.Value;
                // In the fine update method subsecond timer is incremented when a 32-bit accumulator overflows while the timestamp addend (TSAR)
                // is added to the accumulator. This creates a frequency divider so it is implemented in this way.
                var timeBetweenTimeSyncs = fineOrCoarseTimestampUpdate.Value ? (1L << 32) / (double)timestampAddend.Value : 1;
                effectiveFrequency = (long)Math.Round(subsecondTicksPerSecond / timeBetweenTimeSyncs);
            }
            else
            {
                this.ErrorLog("Register configuration would cause an invalid timestamp timer frequency (SSINC=0x{0:X}, TSAR=0x{1:X}, TSCFUPDT={2}), using the default frequency of {3}Hz",
                    timestampSubsecondIncrement.Value, timestampAddend.Value, fineOrCoarseTimestampUpdate.Value, ptpClockFrequency);
                effectiveFrequency = ptpClockFrequency;
            }
            timestampSubsecondTimer.Frequency = effectiveFrequency;
            this.DebugLog("Effective timestamp timer frequency is {0}Hz", effectiveFrequency);
        }

        private void ValidateQueueSize(int size, string paramName)
        {
            if(!Misc.IsPowerOfTwo((ulong)size) || size < MinimumQueueSize)
            {
                throw new ConstructionException($"{paramName} value has to be a power of 2 and at least {MinimumQueueSize}, got {size}");
            }
        }

        private bool MMCTxInterruptStatus =>
            (txGoodPacketCounterInterrupt.Value && txGoodPacketCounterInterruptEnable.Value) ||
            (txGoodByteCounterInterrupt.Value && txGoodByteCounterInterruptEnable.Value) ||
            (txBroadcastPacketCounterInterrupt.Value && txBroadcastPacketCounterInterruptEnable.Value) ||
            (txMulticastPacketCounterInterrupt.Value && txMulticastPacketCounterInterruptEnable.Value) ||
            (txUnicastPacketCounterInterrupt.Value && txUnicastPacketCounterInterruptEnable.Value) ||
            (txPacketCounterInterrupt.Value && txPacketCounterInterruptEnable.Value) ||
            (txByteCounterInterrupt.Value && txByteCounterInterruptEnable.Value);

        private bool MMCRxInterruptStatus =>
            (rxFifoPacketCounterInterrupt.Value && rxFifoPacketCounterInterruptEnable.Value) ||
            (rxUnicastPacketCounterInterrupt.Value && rxUnicastPacketCounterInterruptEnable.Value) ||
            (rxCrcErrorPacketCounterInterrupt.Value && rxCrcErrorPacketCounterInterruptEnable.Value) ||
            (rxMulticastPacketCounterInterrupt.Value && rxMulticastPacketCounterInterruptEnable.Value) ||
            (rxBroadcastPacketCounterInterrupt.Value && rxBroadcastPacketCounterInterruptEnable.Value) ||
            (rxGoodByteCounterInterrupt.Value && rxGoodByteCounterInterruptEnable.Value) ||
            (rxByteCounterInterrupt.Value && rxByteCounterInterruptEnable.Value) ||
            (rxPacketCounterInterrupt.Value && rxPacketCounterInterruptEnable.Value);

        private IBusController Bus { get; }

        private ICPU CpuContext { get; }

        private uint timestampSecondTimer;
        private readonly LimitTimer timestampSubsecondTimer;
        private readonly long ptpClockFrequency;

        private const ulong CounterMaxValue = UInt32.MaxValue;
        private const int RxWatchdogDivider = 256;
        private const uint EtherTypeMinimalValue = 0x600;
        private const ulong DigitalSubsecondRollover = 0x3B9AC9FFUL;
        private const ulong BinarySubsecondRollover = 0x7FFFFFFFUL;

        // This value may be increased if required, but some changes in register creation may be required
        private const int MaxDMAChannels = 3;
        private const int MinimumQueueSize = 128;

        private struct PTPInfo
        {
            public static PTPInfo? FromFrame(EthernetFrame frame, PTPVersion supportedPtpVersion)
            {
                try
                {
                    var packet = frame.UnderlyingPacket;
                    if(packet.Type == EthernetPacketType.PrecisionTimeProtocol)
                    {
                        return ExtractInfo(packet.PayloadPacket.Bytes, TransportType.Ethernet, supportedPtpVersion);
                    }
                    else if(packet.Type == EthernetPacketType.IpV4 || packet.Type == EthernetPacketType.IpV6)
                    {
                        var ipPacket = (IpPacket)packet.PayloadPacket;
                        if(!ptpIpAddresses.Contains(ipPacket.DestinationAddress))
                        {
                            return null;
                        }

                        if(ipPacket.PayloadPacket is UdpPacket udpPacket)
                        {
                            if(!ptpPorts.Contains(udpPacket.DestinationPort))
                            {
                                return null;
                            }
                            var transportType = packet.Type == EthernetPacketType.IpV4 ? TransportType.IpV4 : TransportType.IpV6;
                            return ExtractInfo(udpPacket.PayloadData, transportType, supportedPtpVersion);
                        }
                    }
                }
                catch(Exception)
                {
                    // Something went wrong during packet decoding
                }
                return null;
            }

            private static PTPInfo? ExtractInfo(byte[] ptpData, TransportType transport, PTPVersion ptpVersion)
            {
                var info = new PTPInfo();
                info.Transport = transport;
                // Documentation states that you can either process PTPv1 packets or PTPv2 packets, but never both
                if((ptpData[1] & 0x0F) - 1 != (int)ptpVersion)
                {
                    return null; // PTP version of the packet doesn't match what controller supports
                }
                info.Version = ptpVersion;

                switch(ptpData[0] & 0x0F)
                {
                case 0x0:
                    info.MessageType = PTPMessageType.Sync;
                    break;
                case 0x1:
                    info.MessageType = PTPMessageType.DelayRequest;
                    break;
                case 0x2:
                    info.MessageType = PTPMessageType.PdelayRequest;
                    break;
                case 0x3:
                    info.MessageType = PTPMessageType.PdelayResponse;
                    break;
                case 0x8:
                    info.MessageType = PTPMessageType.FollowUp;
                    break;
                case 0x9:
                    info.MessageType = PTPMessageType.DelayResponse;
                    break;
                case 0xA:
                    info.MessageType = PTPMessageType.PdelayResponseFollowUp;
                    break;
                case 0xB:
                    info.MessageType = PTPMessageType.Announce;
                    break;
                case 0xC:
                    info.MessageType = PTPMessageType.Signaling;
                    break;
                case 0xD:
                    info.MessageType = PTPMessageType.Management;
                    break;
                default:
                    return null; // Invalid message type
                }

                return info;
            }

            public enum TransportType
            {
                Ethernet,
                IpV4,
                IpV6,
            }

            private static readonly HashSet<IPAddress> ptpIpAddresses = new HashSet<IPAddress>()
            {
                IPAddress.Parse("224.0.0.107"),
                IPAddress.Parse("224.0.1.129"),
                IPAddress.Parse("224.0.1.130"),
                IPAddress.Parse("224.0.1.131"),
                IPAddress.Parse("224.0.1.132"),
                // Expanded `FF0x::181`
                IPAddress.Parse("FF00::181"),
                IPAddress.Parse("FF01::181"),
                IPAddress.Parse("FF02::181"),
                IPAddress.Parse("FF03::181"),
                IPAddress.Parse("FF04::181"),
                IPAddress.Parse("FF05::181"),
                IPAddress.Parse("FF06::181"),
                IPAddress.Parse("FF07::181"),
                IPAddress.Parse("FF08::181"),
                IPAddress.Parse("FF09::181"),
                IPAddress.Parse("FF0A::181"),
                IPAddress.Parse("FF0B::181"),
                IPAddress.Parse("FF0C::181"),
                IPAddress.Parse("FF0D::181"),
                IPAddress.Parse("FF0E::181"),
                IPAddress.Parse("FF0F::181"),
                IPAddress.Parse("FF02::6B"),
            };

            private static readonly HashSet<ushort> ptpPorts = new HashSet<ushort>(){ 319, 320 };

            public PTPMessageType MessageType { get; private set; }

            public TransportType Transport { get; private set; }

            public PTPVersion Version { get; private set; }
        }
    }
}