//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;
using Antmicro.Renode.Network;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Network
{
    public class CadenceGEM : NetworkWithPHY, IDoubleWordPeripheral, IMACInterface, IKnownSize
    {
        // the default moduleRevision/moduleId are correct for Zynq with GEM p23
        public CadenceGEM(Machine machine, ushort moduleRevision = 0x118, ushort moduleId = 0x2) : base(machine)
        {
            ModuleId = moduleId;
            ModuleRevision = moduleRevision;

            IRQ = new GPIO();
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            sync = new object();

            interruptManager = new InterruptManager<Interrupts>(this);

            nanoTimer = new LimitTimer(machine.ClockSource, NanosPerSecond, this, "PTP nanos",
                    limit: NanosPerSecond,
                    direction: Direction.Ascending,
                    workMode: WorkMode.Periodic,
                    eventEnabled: true);

            nanoTimer.LimitReached += () => secTimer.Value++;

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.NetworkControl, new DoubleWordRegister(this)
                    .WithFlag(2, out receiveEnabled, name: "RXEN")
                    .WithFlag(9, FieldMode.Write, name: "STARTTX",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                SendFrames();
                            }
                        })
                },

                {(long)Registers.NetworkConfiguration, new DoubleWordRegister(this, 0x80000)
                    .WithValueField(14, 2, out receiveBufferOffset, name: "RXOFFS")
                    .WithFlag(17, out removeFrameChecksum, name: "RFCS")
                    .WithFlag(26, out ignoreRxFCS, name: "FCSIGNORE")
                },

                {(long)Registers.NetworkStatus, new DoubleWordRegister(this)
                    .WithFlag(2, FieldMode.Read, name: "PHY_MGMT_IDLE", valueProviderCallback: _ => true)
                },

                {(long)Registers.DmaConfiguration, new DoubleWordRegister(this, 0x00020784)
                    .WithFlag(11, out checksumGeneratorEnabled, name: "TCPCKSUM")
                },

                {(long)Registers.TransmitStatus, new DoubleWordRegister(this)
                    .WithFlag(0, out usedBitRead, FieldMode.Read | FieldMode.WriteOneToClear, name: "USEDREAD")
                    .WithFlag(5, out transmitComplete, FieldMode.Read | FieldMode.WriteOneToClear, name: "TXCOMPL")
                },

                {(long)Registers.ReceiveBufferQueueBaseAddress, new DoubleWordRegister(this)
                    .WithValueField(2, 30, name: "RXQBASEADDR",
                        valueProviderCallback: _ =>
                        {
                            return rxDescriptorsQueue.CurrentDescriptor.BaseAddress;
                        },
                        writeCallback: (oldValue, value) =>
                        {
                            if(receiveEnabled.Value)
                            {
                                // TODO: this write should be ignored
                                this.Log(LogLevel.Warning, "Changing value of receive buffer queue base address when receiving is enabled is illegal");
                            }

                            rxDescriptorsQueue = new DmaBufferDescriptorsQueue<DmaRxBufferDescriptor>(machine.SystemBus, value << 2, (sb, addr) => new DmaRxBufferDescriptor(sb, addr));
                        })
                },

                {(long)Registers.TransmitBufferQueueBaseAddress, new DoubleWordRegister(this)
                    .WithValueField(2, 30, name: "TXQBASEADDR",
                        valueProviderCallback: _ =>
                        {
                            return txDescriptorsQueue.CurrentDescriptor.BaseAddress;
                        },
                        writeCallback: (oldValue, value) =>
                        {
                            txDescriptorsQueue = new DmaBufferDescriptorsQueue<DmaTxBufferDescriptor>(machine.SystemBus, value << 2, (sb, addr) => new DmaTxBufferDescriptor(sb, addr));
                        })
                },

                {(long)Registers.ReceiveStatus, new DoubleWordRegister(this)
                    .WithFlag(0, out bufferNotAvailable, FieldMode.Read | FieldMode.WriteOneToClear, name: "BUFFNA")
                    .WithFlag(1, out frameReceived, FieldMode.Read | FieldMode.WriteOneToClear, name: "FRAMERX")
                },

                {(long)Registers.InterruptStatus, interruptManager.GetRegister<DoubleWordRegister>(
                    valueProviderCallback: (interrupt, oldValue) =>
                    {
                        var status = interruptManager.IsSet(interrupt) && interruptManager.IsEnabled(interrupt);
                        interruptManager.ClearInterrupt(interrupt);

                        return status;
                    },
                    writeCallback: (interrupt, oldValue, newValue) =>
                    {
                        if(newValue)
                        {
                            interruptManager.ClearInterrupt(interrupt);
                        }
                    })
                },

                {(long)Registers.InterruptEnable, interruptManager.GetRegister<DoubleWordRegister>(
                    writeCallback: (interrupt, oldValue, newValue) =>
                    {
                        if(newValue)
                        {
                            interruptManager.EnableInterrupt(interrupt);
                        }
                    })
                },

                {(long)Registers.InterruptDisable, interruptManager.GetRegister<DoubleWordRegister>(
                    writeCallback: (interrupt, oldValue, newValue) =>
                    {
                        if(newValue)
                        {
                            interruptManager.DisableInterrupt(interrupt);
                        }
                    })
                },

                {(long)Registers.InterruptMaskStatus, interruptManager.GetRegister<DoubleWordRegister>(
                    valueProviderCallback: (interrupt, oldValue) => !interruptManager.IsEnabled(interrupt))
                },

                {(long)Registers.PhyMaintenance, new DoubleWordRegister(this)
                    .WithValueField(0, 31, name: "PHYMNTNC", writeCallback: (_, value) => HandlePhyWrite(value), valueProviderCallback: _ => HandlePhyRead())
                },

                {(long)Registers.SpecificAddress1Bottom, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => BitConverter.ToUInt32(MAC.Bytes, 0))
                },

                {(long)Registers.SpecificAddress1Top, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => BitConverter.ToUInt16(MAC.Bytes, 4))
                },

                {(long)Registers.ModuleId, new DoubleWordRegister(this)
                    .WithValueField(16, 16, FieldMode.Read, name: "MODULE_ID", valueProviderCallback: _ => ModuleId)
                    .WithValueField(0, 16, FieldMode.Read, name: "MODULE_REV", valueProviderCallback: _ => ModuleRevision)
                },

                {(long)Registers.DesignConfiguration1, new DoubleWordRegister(this)
                    .WithFlag(23, FieldMode.Read, name: "IRQCOR", valueProviderCallback: _ => false) // IRQ clear on read
                    .WithValueField(25, 3, FieldMode.Read, name: "DBWDEF", valueProviderCallback: _ => 1) // DMA data bus width - 32 bits
                },

                {(long)Registers.DesignConfiguration2, new DoubleWordRegister(this)
                    .WithFlag(21, FieldMode.Read, name: "GEM_TX_PKT_BUFFER", valueProviderCallback: _ => true) // includes the transmitter packet buffer
                    .WithFlag(20, FieldMode.Read, name: "GEM_RX_PKT_BUFFER", valueProviderCallback: _ => true) // includes the receiver packet buffer
                },

                {(long)Registers.Timer1588SecondsLow, new DoubleWordRegister(this)
                    .WithValueField(0, 31, out secTimer, name: "TCS")
                },

                {(long)Registers.Timer1588SecondsHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 31, valueProviderCallback: _ => 0, writeCallback: (_, value) =>
                    {
                        if(value != 0)
                        {
                            this.Log(LogLevel.Warning, "Writing a non-zero value to the SecondsHigh register, timer values over 32 bits are not supported.");
                        }
                    }, name: "TSH")
                },

                {(long)Registers.Timer1588Nanoseconds, new DoubleWordRegister(this)
                    .WithValueField(0, 29, valueProviderCallback: _ =>
                    {
                        return (uint)nanoTimer.Value;
                    }, writeCallback: (_, value) =>
                    {
                        nanoTimer.Value = value;
                    }, name: "TNS")
                    .WithReservedBits(30, 2)
                },

                {(long)Registers.Timer1588Adjust, new DoubleWordRegister(this)
                    .WithValueField(0, 29, out var timerIncrementDecrement, FieldMode.Write, name: "ITDT")
                    .WithReservedBits(30, 1)
                    .WithFlag(31, out var timerAdjust, FieldMode.Write, name: "ADJ")
                    .WithWriteCallback((_, __) =>
                    {
                        if(timerAdjust.Value)
                        {
                            secTimer.Value -= nanoTimer.Decrement(timerIncrementDecrement.Value);
                        }
                        else
                        {
                            secTimer.Value += nanoTimer.Increment(timerIncrementDecrement.Value);
                        }
                    })
                },

                {(long)Registers.PtpEventFrameTransmittedSecondsHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 31, FieldMode.Read, valueProviderCallback: _ => 0, name: "RUD")
                },

                {(long)Registers.PtpEventFrameTransmittedSeconds, new DoubleWordRegister(this)
                    .WithValueField(0, 31, FieldMode.Read, valueProviderCallback: _ => txPacketTimestamp.seconds, name: "RUD")
                },

                {(long)Registers.PtpEventFrameTransmittedNanoseconds, new DoubleWordRegister(this)
                    .WithValueField(0, 29, FieldMode.Read, valueProviderCallback: _ => txPacketTimestamp.nanos, name: "RUD")
                    .WithReservedBits(30, 2)
                },

                {(long)Registers.PtpEventFrameReceivedSecondsHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 31, FieldMode.Read, valueProviderCallback: _ => 0, name: "RUD")
                },

                {(long)Registers.PtpEventFrameReceivedSeconds, new DoubleWordRegister(this)
                    .WithValueField(0, 31, FieldMode.Read, valueProviderCallback: _ => rxPacketTimestamp.seconds, name: "RUD")
                },

                {(long)Registers.PtpEventFrameReceivedNanoseconds, new DoubleWordRegister(this)
                    .WithValueField(0, 29, FieldMode.Read, valueProviderCallback: _ => rxPacketTimestamp.nanos, name: "RUD")
                    .WithReservedBits(30, 2)
                },

                {(long)Registers.PtpPeerEventFrameTransmittedSecondsHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 31, FieldMode.Read, valueProviderCallback: _ => 0, name: "RUD")
                },

                {(long)Registers.PtpPeerEventFrameTransmittedSeconds, new DoubleWordRegister(this)
                    .WithValueField(0, 31, FieldMode.Read, valueProviderCallback: _ => txPacketTimestamp.seconds, name: "RUD")
                },

                {(long)Registers.PtpPeerEventFrameTransmittedNanoseconds, new DoubleWordRegister(this)
                    .WithValueField(0, 29, FieldMode.Read, valueProviderCallback: _ => txPacketTimestamp.nanos, name: "RUD")
                    .WithReservedBits(30, 2)
                },

                {(long)Registers.PtpPeerEventFrameReceivedSecondsHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 31, FieldMode.Read, valueProviderCallback: _ => 0, name: "RUD")
                },

                {(long)Registers.PtpPeerEventFrameReceivedSeconds, new DoubleWordRegister(this)
                    .WithValueField(0, 31, FieldMode.Read, valueProviderCallback: _ => rxPacketTimestamp.seconds, name: "RUD")
                },

                {(long)Registers.PtpPeerEventFrameReceivedNanoseconds, new DoubleWordRegister(this)
                    .WithValueField(0, 29, FieldMode.Read, valueProviderCallback: _ => rxPacketTimestamp.nanos, name: "RUD")
                    .WithReservedBits(30, 2)
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
            Reset();
        }

        public override void Reset()
        {
            registers.Reset();
            interruptManager.Reset();
            txDescriptorsQueue = null;
            rxDescriptorsQueue = null;
            phyDataRead = 0;
            nanoTimer.Reset();
            nanoTimer.Enabled = true;
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(sync)
            {
                return registers.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(sync)
            {
                registers.Write(offset, value);
            }
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            lock(sync)
            {
                this.Log(LogLevel.Debug, "Received packet, length {0}", frame.Bytes.Length);
                if(!receiveEnabled.Value)
                {
                    this.Log(LogLevel.Info, "Receiver not enabled, dropping frame");
                    return;
                }

                if(!ignoreRxFCS.Value && !EthernetFrame.CheckCRC(frame.Bytes))
                {
                    this.Log(LogLevel.Info, "Invalid CRC, packet discarded");
                    return;
                }

                rxDescriptorsQueue.CurrentDescriptor.Invalidate();
                if(!rxDescriptorsQueue.CurrentDescriptor.Ownership)
                {
                    var actualLength = (uint)(removeFrameChecksum.Value ? frame.Bytes.Length - 4 : frame.Bytes.Length);
                    if(!rxDescriptorsQueue.CurrentDescriptor.WriteBuffer(frame.Bytes, actualLength, receiveBufferOffset.Value))
                    {
                        // The current implementation doesn't handle packets that do not fit into a single buffer.
                        // In case we encounter this error, we probably should implement partitioning/scattering procedure.
                        this.Log(LogLevel.Warning, "Could not write the incoming packet to the DMA buffer: maximum packet length exceeded.");
                        return;
                    }

                    rxDescriptorsQueue.CurrentDescriptor.StartOfFrame = true;
                    rxDescriptorsQueue.CurrentDescriptor.EndOfFrame = true;

                    rxDescriptorsQueue.GoToNextDescriptor();

                    frameReceived.Value = true;
                    interruptManager.SetInterrupt(Interrupts.ReceiveComplete);
                }
                else
                {
                    this.Log(LogLevel.Warning, "Receive DMA buffer overflow");
                    bufferNotAvailable.Value = true;
                    interruptManager.SetInterrupt(Interrupts.ReceiveUsedBitRead);
                }
            }

            // the time obtained here is not single-instruction-precise (unless maximum block size is set to 1 and block chaining is disabled),
            // because timers are not updated instruction-by-instruction, but in batches when `TranslationCPU.ExecuteInstructions` finishes
            rxPacketTimestamp.seconds = secTimer.Value;
            rxPacketTimestamp.nanos = (uint)nanoTimer.Value;
        }

        public event Action<EthernetFrame> FrameReady;

        public long Size => 0x1000;

        public MACAddress MAC { get; set; }

        public ushort ModuleRevision { get; private set; }
        public ushort ModuleId { get; private set; }

        [IrqProvider]
        public GPIO IRQ { get; private set; }

        private uint HandlePhyRead()
        {
            return phyDataRead;
        }

        private void HandlePhyWrite(uint value)
        {
            var data = (ushort)(value & 0xFFFF);
            var reg = (ushort)((value >> 18) & 0x1F);
            var addr = (uint)((value >> 23) & 0x1F);
            var op = (PhyOperation)((value >> 28) & 0x3);

            if(!phys.TryGetValue(addr, out var phy))
            {
                this.Log(LogLevel.Warning, "Write to PHY with unknown address {0}", addr);
                phyDataRead = 0xFFFFU;
                return;
            }

            switch(op)
            {
                case PhyOperation.Read:
                    phyDataRead = phy.Read(reg);
                    break;
                case PhyOperation.Write:
                    phy.Write(reg, data);
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unknown PHY operation code 0x{0:X}", op);
                    break;
            }

            interruptManager.SetInterrupt(Interrupts.ManagementDone);
        }

        private void SendSingleFrame(IEnumerable<byte> bytes, bool isCRCIncluded)
        {
            var bytesArray = bytes.ToArray();
            EnsureArrayLength(isCRCIncluded ? 64 : 60);

            EthernetFrame frame;
            var addCrc = !isCRCIncluded && checksumGeneratorEnabled.Value;
            if(!Misc.TryCreateFrameOrLogWarning(this, bytesArray, out frame, addCrc))
            {
                return;
            }

            this.Log(LogLevel.Noisy, "Sending packet, length {0}", frame.Bytes.Length);
            FrameReady?.Invoke(frame);

            // the time obtained here is not single-instruction-precise (unless maximum block size is set to 1 and block chaining is disabled),
            // because timers are not updated instruction-by-instruction, but in batches when `TranslationCPU.ExecuteInstructions` finishes
            txPacketTimestamp.seconds = secTimer.Value;
            txPacketTimestamp.nanos = (uint)nanoTimer.Value;

            void EnsureArrayLength(int length)
            {
                if(bytesArray.Length < length)
                {
                    Array.Resize(ref bytesArray, length);
                }
            }
        }

        private void SendFrames()
        {
            lock(sync)
            {
                var packetBytes = new List<byte>();
                bool? isCRCIncluded = null;

                txDescriptorsQueue.CurrentDescriptor.Invalidate();
                while(!txDescriptorsQueue.CurrentDescriptor.IsUsed)
                {
                    if(!isCRCIncluded.HasValue)
                    {
                        // this information is interperted only for the first buffer in a frame
                        isCRCIncluded = txDescriptorsQueue.CurrentDescriptor.IsCRCIncluded;
                    }

                    // fill packet with data from memory
                    packetBytes.AddRange(txDescriptorsQueue.CurrentDescriptor.ReadBuffer());
                    if(txDescriptorsQueue.CurrentDescriptor.IsLast)
                    {
                        SendSingleFrame(packetBytes, isCRCIncluded.Value);
                        packetBytes.Clear();
                        isCRCIncluded = null;
                    }

                    txDescriptorsQueue.GoToNextDescriptor();
                }

                transmitComplete.Value = true;
                usedBitRead.Value = true;

                interruptManager.SetInterrupt(Interrupts.TransmitUsedBitRead);
                interruptManager.SetInterrupt(Interrupts.TransmitComplete);
            }
        }

        private uint phyDataRead;
        private DmaBufferDescriptorsQueue<DmaTxBufferDescriptor> txDescriptorsQueue;
        private DmaBufferDescriptorsQueue<DmaRxBufferDescriptor> rxDescriptorsQueue;

        private readonly IFlagRegisterField checksumGeneratorEnabled;
        private readonly IFlagRegisterField transmitComplete;
        private readonly IFlagRegisterField usedBitRead;
        private readonly IFlagRegisterField receiveEnabled;
        private readonly IFlagRegisterField ignoreRxFCS;
        private readonly IFlagRegisterField bufferNotAvailable;
        private readonly IFlagRegisterField frameReceived;
        private readonly IFlagRegisterField removeFrameChecksum;
        private readonly IValueRegisterField receiveBufferOffset;
        private readonly IValueRegisterField secTimer;

        private readonly InterruptManager<Interrupts> interruptManager;
        private readonly DoubleWordRegisterCollection registers;
        private readonly object sync;

        private readonly LimitTimer nanoTimer;

        private PTPTimestamp txPacketTimestamp;
        private PTPTimestamp rxPacketTimestamp;

        private class DmaBufferDescriptorsQueue<T> where T : DmaBufferDescriptor
        {
            public DmaBufferDescriptorsQueue(SystemBus bus, uint baseAddress, Func<SystemBus, uint, T> creator)
            {
                this.bus = bus;
                this.creator = creator;
                this.baseAddress = baseAddress;
                descriptors = new List<T>();

                GoToNextDescriptor();
            }

            public void GoToNextDescriptor()
            {
                if(descriptors.Count == 0)
                {
                    // this is the first descriptor - read it from baseAddress
                    descriptors.Add(creator(bus, baseAddress));
                    currentDescriptorIndex = 0;
                }
                else
                {
                    CurrentDescriptor.Update();

                    if(CurrentDescriptor.Wrap)
                    {
                        currentDescriptorIndex = 0;
                    }
                    else
                    {
                        if(currentDescriptorIndex == descriptors.Count - 1)
                        {
                            // we need to generate new descriptor
                            descriptors.Add(creator(bus, CurrentDescriptor.BaseAddress + DmaBufferDescriptor.LengthInBytes));
                        }
                        currentDescriptorIndex++;
                    }
                }

                CurrentDescriptor.Invalidate();
            }

            public T CurrentDescriptor => descriptors[currentDescriptorIndex];

            private int currentDescriptorIndex;

            private readonly List<T> descriptors;
            private readonly uint baseAddress;
            private readonly SystemBus bus;
            private readonly Func<SystemBus, uint, T> creator;
        }

        private abstract class DmaBufferDescriptor
        {
            public const uint LengthInBytes = 8;

            protected DmaBufferDescriptor(SystemBus bus, uint address)
            {
                this.bus = bus;
                BaseAddress = address;

                words = new uint[2];
            }

            public void Invalidate()
            {
                words[0] = bus.ReadDoubleWord(BaseAddress);
                words[1] = bus.ReadDoubleWord(BaseAddress + 4);
            }

            public void Update()
            {
                bus.WriteDoubleWord(BaseAddress, words[0]);
                bus.WriteDoubleWord(BaseAddress + 4, words[1]);
            }

            public uint BaseAddress { get; private set; }

            public abstract bool Wrap { get; }

            protected uint[] words;
            protected readonly SystemBus bus;
        }

        /// RX buffer descriptor format:
        /// * bits 0-31:
        ///     * 0: Ownership flag
        ///     * 1: Wrap flag
        ///     * 2-31: Address of beginning of buffer
        /// * bits 32-63:
        ///     * 32-44: Length of received frame
        ///     * 45: Bad FCS flag
        ///     * 46: Start of frame flag
        ///     * 47: End of frame flag
        ///     * 48: Cannonical form indicator flag
        ///     * 49-51: VLAN priority
        ///     * 52: Priority tag detected flag
        ///     * 53: VLAN tag detected flag
        ///     * 54-55: Type ID match
        ///     * 56: Type ID match meaning flag
        ///     * 57-58: Specific address register match
        ///     * 59: Reserved
        ///     * 60: External address match flag
        ///     * 61: Unicash hash match flag
        ///     * 62: Multicast hash match flag
        ///     * 63: Broadcast address detected flag
        private class DmaRxBufferDescriptor : DmaBufferDescriptor
        {
            public DmaRxBufferDescriptor(SystemBus bus, uint address) : base(bus, address)
            {
            }

            public bool WriteBuffer(byte[] bytes, uint length, uint offset = 0)
            {
                if(Ownership || bytes.Length > MaximumBufferLength)
                {
                    return false;
                }

                Length = length;
                bus.WriteBytes(bytes, BufferAddress + offset, true);
                Ownership = true;

                return true;
            }

            public ulong BufferAddress => BitHelper.GetMaskedValue(words[0], 2, 30);

            public override bool Wrap => BitHelper.IsBitSet(words[0], 1);

            public bool StartOfFrame { set { BitHelper.SetBit(ref words[1], 14, value); } }

            public bool EndOfFrame { set { BitHelper.SetBit(ref words[1], 15, value); } }

            public uint Length { set { BitHelper.SetMaskedValue(ref words[1], value, 0, 13); } }

            public bool Ownership
            {
                get { return BitHelper.IsBitSet(words[0], 0); }
                set { BitHelper.SetBit(ref words[0], 0, value); }
            }

            private const int MaximumBufferLength = (1 << 13) - 1;
        }

        /// TX buffer descriptor format:
        /// * bits 0-31:
        ///     * 0-31: Byte address of buffer
        /// * bits 32-63:
        ///     * 32-45: Lenght of buffer
        ///     * 46: Reserved
        ///     * 47: Last buffer flag
        ///     * 48: CRC appended flag
        ///     * 49-51: Reserved
        ///     * 52-54: Transmit checksum errors
        ///     * 55-57: Reserved
        ///     * 58: Late collision detected flag
        ///     * 59: Transmit frame corruption flag
        ///     * 60: Reserved (always set to 0)
        ///     * 61: Retry limit exceeded
        ///     * 62: Wrap flag
        ///     * 63: Used flag
        private class DmaTxBufferDescriptor : DmaBufferDescriptor
        {
            public DmaTxBufferDescriptor(SystemBus bus, uint address) : base(bus, address)
            {
            }

            public byte[] ReadBuffer()
            {
                var result = bus.ReadBytes(words[0], Length, true);
                IsUsed = true;
                return result;
            }

            public ushort Length => (ushort)BitHelper.GetValue(words[1], 0, 14);

            public bool IsLast => BitHelper.IsBitSet(words[1], 15);

            public bool IsCRCIncluded => BitHelper.IsBitSet(words[1], 16);

            public override bool Wrap => BitHelper.IsBitSet(words[1], 30);

            public bool IsUsed
            {
                get { return BitHelper.IsBitSet(words[1], 31); }
                set { BitHelper.SetBit(ref words[1], 31, value); }
            }
        }

        private const uint NanosPerSecond = 1000000000;

        private struct PTPTimestamp
        {
            public uint seconds;
            public uint nanos;
        }

        private enum Interrupts
        {
            ManagementDone = 0,
            ReceiveComplete = 1,
            ReceiveUsedBitRead = 2,
            TransmitUsedBitRead = 3,
            TransmitComplete = 7
        }

        private enum PhyOperation
        {
            Write = 0x1,
            Read = 0x2
        }

        private enum Registers : long
        {
            NetworkControl = 0x0,
            NetworkConfiguration = 0x4,
            NetworkStatus = 0x8,
            UserInputOutput = 0xC,
            DmaConfiguration = 0x10,
            TransmitStatus = 0x14,
            ReceiveBufferQueueBaseAddress = 0x18,
            TransmitBufferQueueBaseAddress = 0x1C,
            ReceiveStatus = 0x20,
            InterruptStatus = 0x24,
            InterruptEnable = 0x28,
            InterruptDisable = 0x2C,
            InterruptMaskStatus = 0x30,
            PhyMaintenance = 0x34,
            ReceivedPauseQuantum = 0x38,
            TransmitPauseQuantum = 0x3C,
            // gap intended
            HashRegisterBottom = 0x80,
            HashRegisterTop = 0x84,
            SpecificAddress1Bottom = 0x88,
            SpecificAddress1Top = 0x8C,
            SpecificAddress2Bottom = 0x90,
            SpecificAddress2Top = 0x94,
            SpecificAddress3Top = 0x98,
            SpecificAddress3Bottom = 0x9C,
            SpecificAddress4Top = 0xA0,
            SpecificAddress4Bottom = 0xA4,
            TypeIDMatch1 = 0xA8,
            TypeIDMatch2 = 0xAC,
            TypeIDMatch3 = 0xB0,
            TypeIDMatch4 = 0xB4,
            WakeOnLan = 0xB8,
            IpgStretch = 0xBC,
            StackedVlan = 0xC0,
            TransmitPfcPause = 0xC4,
            SpecificAddressMask1Bottom = 0xC8,
            SpecificAddressMask1Top = 0xCC,
            // gap intended
            PtpEventFrameTransmittedSecondsHigh = 0x0E8,
            PtpEventFrameReceivedSecondsHigh = 0x0EC,
            PtpPeerEventFrameTransmittedSecondsHigh = 0x0F0,
            PtpPeerEventFrameReceivedSecondsHigh = 0x0F4,
            ModuleId = 0xFC,
            OctetsTransmittedLow = 0x100,
            OctetsTransmittedHigh = 0x104,
            FramesTransmitted = 0x108,
            BroadcastFramesTx = 0x10C,
            MulticastFramesTx = 0x110,
            PauseFramesTx = 0x114,
            FramesTx64b = 0x118,
            FramesTx127b65b = 0x11C,
            FramesTx255b128b = 0x120,
            FramesTx511b256b = 0x124,
            FramesTx1023b512b = 0x128,
            FramesTx1518b1024b = 0x12C,
            // gap intended
            TransmitUnderRuns = 0x134,
            SingleCollisionFrames = 0x138,
            MultipleCollisionFrames = 0x13C,
            ExcessiveCollisions = 0x140,
            LateCollisions = 0x144,
            DeferredTransmissionFrames = 0x148,
            CarrierSenseErrors = 0x14C,
            OctetsReceivedLow = 0x150,
            OctetsReceivedHigh = 0x154,
            FramesReceived = 0x158,
            BroadcastFramesRx = 0x15C,
            MulticastFramesRx = 0x160,
            PauseFramesRx = 0x164,
            FramesRx64b = 0x168,
            FramesRx127b65b = 0x16C,
            FramesRx255b128b = 0x170,
            FramesRx511b256b = 0x174,
            FramesRx1023b512b = 0x178,
            FramesRx1518b1024b = 0x17C,
            UndersizeFramesReceived = 0x184,
            OversizeFramesReceived = 0x188,
            JabbersReceived = 0x18C,
            FrameCheckSequenceErrors = 0x190,
            LengthFieldFrameErrors = 0x194,
            ReceiveSymbolErrors = 0x198,
            AlignmentErrors = 0x19C,
            ReceiveResourceErrors = 0x1A0,
            ReceiveOverrunErrors = 0x1A4,
            IpHeaderChecksumErrors = 0x1A8,
            TcpCHecksumErrors = 0x1AC,
            UdpChecksumErrors = 0x1B0,
            // gap intended
            Timer1588IncrementSubnanoseconds = 0x1BC,
            Timer1588SecondsHigh = 0x1C0,
            // gap intended
            Timer1588SyncStrobeSeconds = 0x1C8,
            Timer1588SyncStrobeNanoseconds = 0x1CC,
            Timer1588SecondsLow = 0x1D0,
            Timer1588Nanoseconds = 0x1D4,
            Timer1588Adjust = 0x1D8,
            Timer1588Increment = 0x1DC,
            PtpEventFrameTransmittedSeconds = 0x1E0,
            PtpEventFrameTransmittedNanoseconds = 0x1E4,
            PtpEventFrameReceivedSeconds = 0x1E8,
            PtpEventFrameReceivedNanoseconds = 0x1EC,
            PtpPeerEventFrameTransmittedSeconds = 0x1F0,
            PtpPeerEventFrameTransmittedNanoseconds = 0x1F4,
            PtpPeerEventFrameReceivedSeconds = 0x1F8,
            PtpPeerEventFrameReceivedNanoseconds = 0x1FC,
            // gap intended
            DesignConfiguration1 = 0x280, // this register's implementation is based on linux driver as Zynq-7000 documentation does not mention it
            DesignConfiguration2 = 0x284,
            DesignConfiguration3 = 0x288,
            DesignConfiguration4 = 0x28C,
            DesignConfiguration5 = 0x290,
            // gap intended
            InterruptStatusPriorityQueue1 = 0x400,
            InterruptStatusPriorityQueue2 = 0x404,
            // gap intended
            TransmitBufferQueueBaseAddressPriorityQueue1 = 0x440,
            TransmitBufferQueueBaseAddressPriorityQueue2 = 0x444,
            // gap intended
            ReceiveBufferQueueBaseAddressPriorityQueue1 = 0x480,
            ReceiveBufferQueueBaseAddressPriorityQueue2 = 0x484,
            // gap intended
            ReceiveBufferSizePriorityQueue1 = 0x4A0,
            ReceiveBufferSizePriorityQueue2 = 0x4A4,
            // gap intended
            CreditBasedShapingControl = 0x4BC,
            CreditBasedShapingIdleSlopeQueueA = 0x4C0,
            CreditBasedShapingIdleSlopeQueueB = 0x4C4,
            // gap intended
            ScreeningType1PriorityQueues0 = 0x500,
            ScreeningType1PriorityQueues1 = 0x504,
            ScreeningType1PriorityQueues2 = 0x508,
            ScreeningType1PriorityQueues3 = 0x50C,
            // gap intended
            ScreeningType2PriorityQueues0 = 0x540,
            ScreeningType2PriorityQueues1 = 0x544,
            ScreeningType2PriorityQueues2 = 0x548,
            ScreeningType2PriorityQueues3 = 0x54C,
            ScreeningType2PriorityQueues4 = 0x550,
            ScreeningType2PriorityQueues5 = 0x554,
            ScreeningType2PriorityQueues6 = 0x55C,
            ScreeningType2PriorityQueues7 = 0x560,
            // gap intended
            InterruptEnablePriorityQueue1 = 0x600,
            InterruptEnablePriorityQueue2 = 0x604,
            // gap intended
            InterruptDisablePriorityQueue1 = 0x620,
            InterruptDisablePriorityQueue2 = 0x624,
            // gap intended
            InterruptMaskPriorityQueue1 = 0x640,
            InterruptMaskPriorityQueue2 = 0x644,
            // gap intended
            ScreeningType2Ethertype0 = 0x6E0,
            ScreeningType2Ethertype1 = 0x6E4,
            ScreeningType2Ethertype2 = 0x6E8,
            ScreeningType2Ethertype3 = 0x6EC,
            // gap intended
            ScreeningType2Compare0Word0 = 0x700,
            ScreeningType2Compare0Word1 = 0x704,
            ScreeningType2Compare1Word0 = 0x708,
            ScreeningType2Compare1Word1 = 0x70C,
            ScreeningType2Compare2Word0 = 0x710,
            ScreeningType2Compare2Word1 = 0x714,
            ScreeningType2Compare3Word0 = 0x718,
            ScreeningType2Compare3Word1 = 0x71C,
            ScreeningType2Compare4Word0 = 0x720,
            ScreeningType2Compare4Word1 = 0x724,
            ScreeningType2Compare5Word0 = 0x728,
            ScreeningType2Compare5Word1 = 0x72C,
            ScreeningType2Compare6Word0 = 0x730,
            ScreeningType2Compare6Word1 = 0x734,
            ScreeningType2Compare7Word0 = 0x738,
            ScreeningType2Compare7Word1 = 0x73C,
            ScreeningType2Compare8Word0 = 0x740,
            ScreeningType2Compare8Word1 = 0x744,
            ScreeningType2Compare9Word0 = 0x748,
            ScreeningType2Compare9Word1 = 0x74C,
            ScreeningType2Compare10Word0 = 0x750,
            ScreeningType2Compare10Word1 = 0x754,
            ScreeningType2Compare11Word0 = 0x758,
            ScreeningType2Compare11Word1 = 0x75C,
            ScreeningType2Compare12Word0 = 0x760,
            ScreeningType2Compare12Word1 = 0x764,
            ScreeningType2Compare13Word0 = 0x768,
            ScreeningType2Compare13Word1 = 0x76C,
            ScreeningType2Compare14Word0 = 0x770,
            ScreeningType2Compare14Word1 = 0x774,
            ScreeningType2Compare15Word0 = 0x778,
            ScreeningType2Compare15Word1 = 0x77C,
            ScreeningType2Compare16Word0 = 0x780,
            ScreeningType2Compare16Word1 = 0x784,
            ScreeningType2Compare17Word0 = 0x788,
            ScreeningType2Compare17Word1 = 0x78C,
            ScreeningType2Compare18Word0 = 0x790,
            ScreeningType2Compare18Word1 = 0x794,
            ScreeningType2Compare19Word0 = 0x798,
            ScreeningType2Compare19Word1 = 0x79C,
            ScreeningType2Compare20Word0 = 0x7A0,
            ScreeningType2Compare20Word1 = 0x7A4,
            ScreeningType2Compare21Word0 = 0x7A8,
            ScreeningType2Compare21Word1 = 0x7AC,
            ScreeningType2Compare22Word0 = 0x7B0,
            ScreeningType2Compare22Word1 = 0x7B4,
            ScreeningType2Compare23Word0 = 0x7B0,
            ScreeningType2Compare23Word1 = 0x7B4
        }
    }
}
