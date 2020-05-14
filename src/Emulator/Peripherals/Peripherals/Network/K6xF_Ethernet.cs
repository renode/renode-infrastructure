//
// Copyright (c) 2010-2020 Antmicro
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
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public class K6xF_Ethernet : NetworkWithPHY, IDoubleWordPeripheral, IMACInterface, IKnownSize
    {
        public K6xF_Ethernet(Machine machine) : base(machine)
        {
            rxIRQ = new GPIO();
            txIRQ = new GPIO();
            ptpIRQ = new GPIO();
            miscIRQ = new GPIO();

            innerLock = new object();

            interruptManager = new InterruptManager<Interrupts>(this);

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptEvent, interruptManager.GetRegister<DoubleWordRegister>(
                    valueProviderCallback: (interrupt, oldValue) =>
                    {
                        return interruptManager.IsSet(interrupt);
                    },
                    writeCallback: (interrupt, oldValue, newValue) =>
                    {
                        if(newValue)
                        {
                            interruptManager.ClearInterrupt(interrupt);
                        }
                    })
                },
                {(long)Registers.InterruptMask, interruptManager.GetRegister<DoubleWordRegister>(
                    valueProviderCallback: (interrupt, oldValue) =>
                    {
                        return interruptManager.IsEnabled(interrupt);
                    },
                    writeCallback: (interrupt, oldValue, newValue) =>
                    {

                        if(newValue)
                        {
                            interruptManager.EnableInterrupt(interrupt);
                        }
                        else
                        {
                            interruptManager.DisableInterrupt(interrupt);
                            interruptManager.ClearInterrupt(interrupt);
                        }
                    })
                },
                {(long)Registers.ReceiveDescriptorActive, new DoubleWordRegister(this)
                    .WithReservedBits(25, 7)
                    .WithFlag(24, out receiverEnabled, FieldMode.Write | FieldMode.Read, name: "RDAR")
                    .WithReservedBits(0, 24)
                },
                {(long)Registers.TransmitDescriptorActive, new DoubleWordRegister(this)
                    .WithReservedBits(25, 7)
                    .WithFlag(24, FieldMode.Read | FieldMode.WriteOneToClear, name: "TDAR",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                isTransmissionStarted = true;
                                this.Log(LogLevel.Debug, "Sending Frames");
                                SendFrames();
                            }
                        })
                    .WithReservedBits(0, 24)
                },
                {(long)Registers.EthernetControl, new DoubleWordRegister(this)
                    .WithReservedBits(17, 15)
                    .WithReservedBits(12, 5)
                    .WithReservedBits(11, 1)
                    .WithReservedBits(10, 1)
                    .WithReservedBits(9, 1)
                    .WithTaggedFlag("DBSWP", 8)
                    .WithTaggedFlag("STOPEN", 7)
                    .WithTaggedFlag("DBGEN", 6)
                    .WithReservedBits(5, 1)
                    .WithFlag(4, out extendedMode, FieldMode.Write | FieldMode.Read, name: "EN1588")
                    .WithTaggedFlag("SLEEP", 3)
                    .WithTaggedFlag("MAGICEN", 2)
                    .WithFlag(1, out etherEnabled, FieldMode.Write | FieldMode.Read, name: "ETHEREN")
                    .WithFlag(0, FieldMode.Write,
                        writeCallback: (_, b) =>
                        {
                            if (b) Reset();
                        },name: "RESET")
                },
                {(long)Registers.ReceiveControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("GRS", 31)
                    .WithTaggedFlag("NLC", 30)
                    .WithValueField(16, 14, name: "MAX_FL")
                    .WithTaggedFlag("CFEN", 15)
                    .WithFlag(14, out forwardCRC, FieldMode.Write | FieldMode.Read, name: "CRCFWD")
                    .WithTaggedFlag("PAUFWD", 13)
                    .WithTaggedFlag("PADEN", 12)
                    .WithReservedBits(10, 2)
                    .WithTaggedFlag("RMII_10T", 9)
                    .WithTaggedFlag("RMII_MODE", 8)
                    .WithReservedBits(7, 1)
                    .WithReservedBits(6, 1)
                    .WithTaggedFlag("FCE", 5)
                    .WithTaggedFlag("BC_REJ", 4)
                    .WithTaggedFlag("PROM", 3)
                    .WithTaggedFlag("MII_MODE", 2)
                    .WithTaggedFlag("DRT", 1)
                    .WithTaggedFlag("LOOP", 0)
                },
                {(long)Registers.TransmitControl, new DoubleWordRegister(this)
                    .WithReservedBits(11, 21)
                    .WithReservedBits(10, 1)
                    .WithFlag(9, out checksumGeneratorEnabled, name: "CRCFWD")
                    .WithTaggedFlag("ADDINS", 8)
                    .WithValueField(5, 3, name: "ADDSEL")
                    .WithTaggedFlag("RFC_PAUSE", 4)
                    .WithTaggedFlag("TFC_PAUSE", 3)
                    .WithFlag(2, out fullDuplex, FieldMode.Write | FieldMode.Read, name: "FDEN")
                    .WithReservedBits(1, 1)
                    .WithTaggedFlag("GTS", 0)
                },
                {(long)Registers.PhysicalAddressLower, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write | FieldMode.Read, writeCallback: (_, value) =>
                    {
                        lowerMAC = value;
                        UpdateMac();
                    }, name: "PADDR1")
                },
                {(long)Registers.PhysicalAddressUpper, new DoubleWordRegister(this)
                    .WithValueField(16, 16, FieldMode.Write | FieldMode.Read, writeCallback: (_, value) =>
                    {
                        upperMac = value;
                        UpdateMac();
                    },name: "PADDR2")
                    .WithValueField(0, 16, name: "TYPE")
                },
                {(long)Registers.DescriptorGroupUpperAddress, new DoubleWordRegister(this)
                    .WithValueField(0,32, out upperLimit, name:"GADDR1")
                },
                {(long)Registers.DescriptorGroupLowerAddress, new DoubleWordRegister(this)
                    .WithValueField(0,32, out lowerLimit, name:"GADDR2")
                },
                {(long)Registers.TransmitFIFOWatermark, new DoubleWordRegister(this)
                    .WithReservedBits(9,22)
                    .WithFlag(8, out storeAndForward, FieldMode.Write | FieldMode.Read, name: "STRFWD")
                    .WithReservedBits(6, 2)
                    .WithValueField(0,6, name:"TFWR")
                },
                {(long)Registers.ReceiveDescriptorRingStart, new DoubleWordRegister(this)
                    .WithValueField(2,30, name:"R_DES_START",
                        writeCallback: (oldValue, value) =>
                        {
                            if(receiverEnabled.Value)
                            {
                                this.Log(LogLevel.Warning, "Changing value of receive buffer queue base address while reception is enabled is illegal");
                                return;
                            }
                            rxDescriptorsQueue = new DmaBufferDescriptorsQueue<DmaRxBufferDescriptor>(machine.SystemBus, value << 2, (sb, addr) => new DmaRxBufferDescriptor(sb, addr, extendedMode.Value));
                        })
                    .WithReservedBits(1, 1)
                    .WithReservedBits(0, 1)
                },
                {(long)Registers.TransmitBufferDescriptorRingStart, new DoubleWordRegister(this)
                    .WithValueField(2,30, name:"X_DES_START",
                        writeCallback: (oldValue, value) =>
                        {
                            if(isTransmissionStarted)
                            {
                                this.Log(LogLevel.Warning, "Changing value of transmit buffer descriptor ring start address while transmission is started is illegal");
                                return;
                            }
                            txDescriptorsQueue = new DmaBufferDescriptorsQueue<DmaTxBufferDescriptor>(machine.SystemBus, value << 2, (sb, addr) => new DmaTxBufferDescriptor(sb, addr, extendedMode.Value));
                        })
                    .WithReservedBits(1, 1)
                    .WithReservedBits(0, 1)
                }

            };

            registers = new DoubleWordRegisterCollection(this, registerMap);
        }

        public MACAddress MAC { get; set; }

        public long Size => 0x1000;

        public event Action<EthernetFrame> FrameReady;

        public uint ReadDoubleWord(long offset)
        {
            lock(innerLock)
            {
                var value = registers.Read(offset);
                this.Log(LogLevel.Debug, "Read from offset 0x{0:X}, value 0x{1:X}.", offset, value);
                return value;
            }
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            //throw new NotImplementedException();
            this.Log(LogLevel.Debug, "GOT A FRAME");
            lock(innerLock)
            {
                this.Log(LogLevel.Debug, "Received packet, length {0}", frame.Bytes.Length);
                if(!receiverEnabled.Value)
                {
                    this.Log(LogLevel.Info, "Receiver not enabled, dropping frame");
                    return;
                }

                if(!EthernetFrame.CheckCRC(frame.Bytes))
                {
                    this.Log(LogLevel.Info, "Invalid CRC, packet discarded");
                    return;
                }

                // the time obtained here is not single-instruction-precise (unless maximum block size is set to 1 and block chaining is disabled),
                // because timers are not updated instruction-by-instruction, but in batches when `TranslationCPU.ExecuteInstructions` finishes
                //rxPacketTimestamp.seconds = secTimer.Value;
                //rxPacketTimestamp.nanos = (uint)nanoTimer.Value;

                rxDescriptorsQueue.CurrentDescriptor.Invalidate();
                if(rxDescriptorsQueue.CurrentDescriptor.IsEmpty)
                {
                    //var actualLength = (uint)(removeFrameChecksum.Value ? frame.Bytes.Length - 4 : frame.Bytes.Length);
                    if(!rxDescriptorsQueue.CurrentDescriptor.WriteBuffer(frame.Bytes, (uint)frame.Bytes.Length))
                    {
                        // The current implementation doesn't handle packets that do not fit into a single buffer.
                        // In case we encounter this error, we probably should implement partitioning/scattering procedure.
                        this.Log(LogLevel.Warning, "Could not write the incoming packet to the DMA buffer: maximum packet length exceeded.");
                        return;
                    }

                    //rxDescriptorsQueue.CurrentDescriptor.StartOfFrame = true;
                    rxDescriptorsQueue.CurrentDescriptor.Length = (ushort)frame.Bytes.Length;
                    rxDescriptorsQueue.CurrentDescriptor.IsLast = true;
                    rxDescriptorsQueue.CurrentDescriptor.IsEmpty = false;
                    // write this back to memory
                    rxDescriptorsQueue.CurrentDescriptor.Update();

                    /*if(rxBufferDescriptorTimeStampMode.Value != TimestampingMode.Disabled)
                    {
                        rxDescriptorsQueue.CurrentDescriptor.Timestamp = rxPacketTimestamp;
                    }
                    else
                    {
                        rxDescriptorsQueue.CurrentDescriptor.HasValidTimestamp = false;
                    }*/

                    rxDescriptorsQueue.GoToNextDescriptor();

                    //frameReceived.Value = true;
                    interruptManager.SetInterrupt(Interrupts.ReceiveBufferInterrupt);
                    interruptManager.SetInterrupt(Interrupts.ReceiveFrameInterrupt);

                }
                else
                {
                    this.Log(LogLevel.Warning, "Receive DMA buffer overflow");
                    //bufferNotAvailable.Value = true;
                    //interruptManager.SetInterrupt(Interrupts.ReceiveUsedBitRead);
                }
            }
        }

        public override void Reset()
        {
            isTransmissionStarted = false;
            receiverEnabled.Value = false;
            etherEnabled.Value = false;
            registers.Reset();
            txDescriptorsQueue = null;
            rxDescriptorsQueue = null;
            interruptManager.Reset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            this.Log(LogLevel.Debug, "Write to offset 0x{0:X}, value 0x{1:X}", offset, value);
            printRegister(value);
            lock(innerLock)
            {
                registers.Write(offset, value);
            }

        }

        private void printRegister(uint value)
        {
            this.Log(LogLevel.Debug, "|31|30|29|28|27|26|25|24|23|22|21|20|19|18|17|16|15|14|13|12|11|10|09|08|07|06|05|04|03|02|01|00|");
            var valueStr = "|";

            for(var i = 31; i >= 0; --i)
            {
                var bit = value & (1 << i);
                if(0 != bit)
                    valueStr += " 1|";
                else valueStr += " 0|";
            }
            this.Log(LogLevel.Debug, "{0}", valueStr);
        }

        private void UpdateMac()
        {
            if((null == lowerMAC) || (null == upperMac)) return;

            ulong finalMac = (ulong)((lowerMAC << 32) + upperMac);
            this.Log(LogLevel.Info, "Setting MAC to {0:X}", finalMac);
            MAC = new MACAddress(finalMac);
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
            /*txPacketTimestamp.seconds = secTimer.Value;
            txPacketTimestamp.nanos = (uint)nanoTimer.Value;

            if(txBufferDescriptorTimeStampMode.Value != TimestampingMode.Disabled)
            {
                txDescriptorsQueue.CurrentDescriptor.Timestamp = txPacketTimestamp;
            }
            else
            {
                rxDescriptorsQueue.CurrentDescriptor.HasValidTimestamp = false;
            }*/

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
            lock(innerLock)
            {
                var packetBytes = new List<byte>();
                bool? isCRCIncluded = null;
                this.Log(LogLevel.Debug, "Loading descriptor");
                txDescriptorsQueue.CurrentDescriptor.Invalidate();
                while(txDescriptorsQueue.CurrentDescriptor.IsReady)
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

                    // Need to update the ready flag after processing
                    txDescriptorsQueue.CurrentDescriptor.IsReady = false; // free it up
                    txDescriptorsQueue.CurrentDescriptor.Update(); // write bag to memory
                    this.Log(LogLevel.Debug, "Going to next desciptor");
                    txDescriptorsQueue.GoToNextDescriptor();
                }

                this.Log(LogLevel.Debug, "Transmission completed");
                isTransmissionStarted = false;

                interruptManager.SetInterrupt(Interrupts.TransmitFrameInterrupt);
                interruptManager.SetInterrupt(Interrupts.TransmitBufferInterrupt);
            }
        }


        private readonly InterruptManager<Interrupts> interruptManager;
        private DoubleWordRegisterCollection registers;
        private readonly object innerLock;

        // Fields needed for the internal logic
        private bool isTransmissionStarted = false;

        //EthernetControl
        private IFlagRegisterField etherEnabled;
        private IFlagRegisterField extendedMode;

        //TransmitControl
        private IFlagRegisterField fullDuplex;
        private readonly IFlagRegisterField checksumGeneratorEnabled;

        //ReceiveControl
        private readonly IFlagRegisterField receiverEnabled;
        private IFlagRegisterField forwardCRC;

        //Lower MAC address registe
        private ulong? lowerMAC = null;
        private ulong? upperMac = null;

        //DescriptorGroup
        private IValueRegisterField upperLimit;
        private IValueRegisterField lowerLimit;

        //TransmitFIFOWatermark
        private IFlagRegisterField storeAndForward;

        //ReceiveDescriptorRingStart
        private IValueRegisterField rxDescRingStart;

        //TransmitBufferDescriptorRingStart
        private IValueRegisterField txDescRingStart;

        private enum Registers
        {
            InterruptEvent = 0x0004,
            InterruptMask = 0x0008,
            ReceiveDescriptorActive = 0x0010,
            TransmitDescriptorActive = 0x0014,
            EthernetControl = 0x0024,
            MIIManagementFrame = 0x0040,
            MIISpeedControl = 0x0044,
            MIBControl = 0x0064,
            ReceiveControl = 0x0084,
            TransmitControl = 0x00C4,
            PhysicalAddressLower = 0x00E4,
            PhysicalAddressUpper = 0x00E8,
            OpcodePauseDuration = 0x00EC,
            DescriptorIndividualUpperAddress = 0x0118,
            DescriptorIndividualLowerAddress = 0x011C,
            DescriptorGroupUpperAddress = 0x0120,
            DescriptorGroupLowerAddress = 0x0124,
            TransmitFIFOWatermark = 0x0144,
            ReceiveDescriptorRingStart = 0x0180,
            TransmitBufferDescriptorRingStart = 0x0184,
            MaximumReceiveBufferSize = 0x0188,
            ReceiveFIFOSectionFullThreshold = 0x0190,
            ReceiveFIFOSectionEmptyThreshold = 0x0194,
            ReceiveFIFOAlmostEmptyThreshold = 0x0198,
            ReceiveFIFOAlmostFullThreshold = 0x019C,
            TransmitFIFOSectionEmptyThreshold = 0x01A0,
            TransmitFIFOAlmostEmptyThreshold = 0x01A4,
            TransmitFIFOAlmostFullThreshold = 0x01A8,
            TransmitInterPacketGap = 0x01AC,
            FrameTruncationLength = 0x01B0,
            TransmitAcceleratorFunctionConfiguration = 0x01C0,
            ReceiveAcceleratorFunctionConfiguration = 0x01C4,
            TxPacketCountStatistic = 0x0204,
            TxBroadcastPacketsStatistic = 0x0208,
            TxMulticastPacketsStatistic = 0x020C,
            TxPacketswithCRCAlignErrorStatistic = 0x0210,
            TxPacketsLessThanBytesandGoodCRCStatistic = 0x0214,
            TxPacketsGTMAX_FLbytesandGoodCRCStatistic = 0x0218,
            TxPacketsLessThan64BytesandBadCRCStatistic = 0x021C,
            TxPacketsGreaterThanMAX_FLbytesandBadCRC = 0x0220,
            TxCollisionCountStatistic = 0x0224,
            Tx64BytePacketsStatistic = 0x0228,
            Tx65to127bytePacketsStatistic = 0x022C,
            Tx128to255bytePacketsStatistic = 0x0230,
            Tx256to511bytePacketsStatistic = 0x0234,
            Tx512to1023bytePacketsStatistic = 0x0238,
            Tx1024to2047bytePacketsStatistic = 0x023C,
            TxPacketsGreaterThan2048BytesStatistic = 0x0240,
            TxOctetsStatistic = 0x0244,
            FramesTransmittedOKStatistic = 0x024C,
            FramesTransmittedwithSingleCollisionStatistic = 0x0250,
            FramesTransmittedwithMultipleCollisionsStatistic = 0x0254,
            FramesTransmittedafterDeferralDelayStatistic = 0x0258,
            FramesTransmittedwithLateCollisionStatistic = 0x025C,
            FramesTransmittedwithExcessiveCollisionsStatistic = 0x0260,
            FramesTransmittedwithTxFIFOUnderrunStatistic = 0x0264,
            FramesTransmittedwithCarrierSenseErrorStatistic = 0x0268,
            FlowControlPauseFramesTransmittedStatistic = 0x0270,
            OctetCountforFramesTransmittedwoErrorStatistic = 0x0274,
            RxPacketCountStatistic = 0x0284,
            RxBroadcastPacketsStatistic = 0x0288,
            RxMulticastPacketsStatistic = 0x028C,
            RxPacketswithCRCAlignErrorStatistic = 0x0290,
            RxPacketswithLessThan64BytesandGoodCRC = 0x0294,
            RxPacketsGreaterThanMAX_FLandGoodCRCStatistic = 0x0298,
            RxPacketsLessThan64BytesandBadCRCStatistic = 0x029C,
            RxPacketsGreaterThanMAX_FLBytesandBadCRC = 0x02A0,
            Rx64BytePacketsStatistic = 0x02A8,
            Rx65to127BytePacketsStatistic = 0x02AC,
            Rx128to255BytePacketsStatistic = 0x02B0,
            Rx256to511BytePacketsStatistic = 0x02B4,
            Rx512to1023BytePacketsStatistic = 0x02B8,
            Rx1024to2047BytePacketsStatistic = 0x02BC,
            RxPacketsGreaterthan2048BytesStatistic = 0x02C0,
            RxOctetsStatistic = 0x02C4,
            FramesnotCountedCorrectlyStatistic = 0x02C8,
            FramesReceivedOKStatistic = 0x02CC,
            FramesReceivedwithCRCErrorStatistic = 0x02D0,
            FramesReceivedwithAlignmentErrorStatistic = 0x02D4,
            ReceiveFIFOOverflowCountStatistic = 0x02D8,
            FlowControlPauseFramesReceivedStatistic = 0x02DC,
            OctetCountforFramesReceivedwithoutErrorStatistic = 0x02E0,
            AdjustableTimerControl = 0x0400,
            TimerValue = 0x0404,
            TimerOffset = 0x0408,
            TimerPeriod = 0x040C,
            TimerCorrection = 0x0410,
            TimeStampingClockPeriod = 0x0414,
            TimestampofLastTransmittedFrame = 0x0418,
            TimerGlobalStatus = 0x0604,
            TimerControlStatusR0 = 0x0608,
            TimerCompareCaptureR0 = 0x060C,
            TimerControlStatusR1 = 0x0610,
            TimerCompareCaptureR1 = 0x0614,
            TimerControlStatusR2 = 0x0618,
            TimerCompareCaptureR2 = 0x061C,
            TimerControlStatusR3 = 0x0620,
            TimerCompareCaptureR3 = 0x0624
        }

        [IrqProvider("ptp irq", 3)]
        public GPIO ptpIRQ { get; private set; }

        [IrqProvider("misc irq", 2)]
        public GPIO miscIRQ { get; private set; }

        [IrqProvider("receive irq", 1)]
        public GPIO rxIRQ { get; private set; }

        [IrqProvider("transmit irq", 0)]
        public GPIO txIRQ { get; private set; }

        private enum Interrupts
        {
            [Subvector(1)]
            BabblingReceiveError = 30,
            [Subvector(0)]
            BabblingTransmitError = 29,
            [Subvector(2)]
            GracefulStopComplete = 28,
            [Subvector(0)]
            TransmitFrameInterrupt = 27,
            [Subvector(0)]
            TransmitBufferInterrupt = 26,
            [Subvector(1)]
            ReceiveFrameInterrupt = 25,
            [Subvector(1)]
            ReceiveBufferInterrupt = 24,
            [Subvector(2)]
            MIIInterrupt = 23,
            [Subvector(2)]
            EthernetBusError = 22,
            [Subvector(2)]
            LateCollision = 21,
            [Subvector(2)]
            CollisionRetryLimit = 20,
            [Subvector(2)]
            TransmitFIFOUnderrun = 19,
            [Subvector(2)]
            PayloadReceiveError = 18,
            [Subvector(2)]
            NodeWakeupRequestIndication = 17,
            [Subvector(3)]
            TransmitTimestampAvailable = 16,
            [Subvector(3)]
            TimestampTimer = 15
        }

        private DmaBufferDescriptorsQueue<DmaTxBufferDescriptor> txDescriptorsQueue = null;
        private DmaBufferDescriptorsQueue<DmaRxBufferDescriptor> rxDescriptorsQueue = null;


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
                    // Buffer is not last and next buffer is in the consecutive field
                    if(!CurrentDescriptor.IsLast && !CurrentDescriptor.Wrap)
                    {
                        descriptors.Add(creator(bus, CurrentDescriptor.DescriptorAddress + CurrentDescriptor.SizeInBytes));
                        currentDescriptorIndex++;
                    }
                }
                CurrentDescriptor.Invalidate();
            }

            public void GoToBaseAddress()
            {
                currentDescriptorIndex = 0;
                CurrentDescriptor.Invalidate();
            }

            public T CurrentDescriptor => descriptors[currentDescriptorIndex];

            private int currentDescriptorIndex;

            private readonly List<T> descriptors;
            private readonly uint baseAddress;
            private readonly SystemBus bus;
            private readonly Func<SystemBus, uint, T> creator;
        }

        private class DmaBufferDescriptor
        {
            protected DmaBufferDescriptor(SystemBus bus, uint address, bool isExtendedModeEnabled)
            {
                Bus = bus;
                DescriptorAddress = address;
                IsExtendedModeEnabled = isExtendedModeEnabled;
                SizeInBytes = InitWords();
            }

            public SystemBus Bus { get; }
            public uint SizeInBytes { get; }
            public bool IsExtendedModeEnabled { get; }
            public uint GetDataBufferAddress()
            {
                return (words[3] << 16) | words[2];
            }
            public uint DescriptorAddress { get; }
            public bool Wrap => BitHelper.IsBitSet(words[1], 13);

            protected uint[] words;

            private uint InitWords()
            {
                if(IsExtendedModeEnabled)
                {
                    words = new uint[16];
                }
                else
                {
                    words = new uint[4];
                }
                return (uint)words.Length * 2;
            }

            public void Invalidate()
            {
                var tempOffset = 0UL;
                for(var i = 0; i < words.Length; ++i)
                {
                    words[i] = Bus.ReadDoubleWord(DescriptorAddress + tempOffset);
                    tempOffset += 2;
                }
            }

            public void Update()
            {
                var tempOffset = 0UL;
                foreach(var word in words)
                {
                    Bus.WriteDoubleWord(DescriptorAddress + tempOffset, word);
                    tempOffset += 2;
                }
            }

            public bool IsLast => BitHelper.IsBitSet(words[1], 11);
        }


        /// Legacy Transmit Buffer
        /// 
        ///          =================================================================================
        ///          |             Byte 1                    |           Byte 0                      |
        ///          | 15 | 14 | 13 | 12 | 11 | 10 | 09 | 08 | 07 | 06 | 05 | 04 | 03 | 02 | 01 | 00 |
        ///          =================================================================================
        /// Offset +0|                              Data Length                                      |
        /// Offset +2| R  | TO1| W  | TO2| L  | TC |ABC | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset +4|                       Tx Data Buffer Pointer -- low halfword                  |
        /// Offset +6|                       Tx Data Buffer Pointer -- high halfword                 |
        ///          =================================================================================
        /// 
        /// 
        /// Enhanced Transmit Buffer
        /// 
        ///          =================================================================================
        ///          |             Byte 1                    |           Byte 0                      |
        ///          | 15 | 14 | 13 | 12 | 11 | 10 | 09 | 08 | 07 | 06 | 05 | 04 | 03 | 02 | 01 | 00 |
        ///          =================================================================================
        /// Offset +0|                              Data Length                                      |
        /// Offset +2| R  | TO1| W  | TO2| L  | TC | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset +4|                       Tx Data Buffer Pointer -- low halfword                  |
        /// Offset +6|                       Tx Data Buffer Pointer -- high halfword                 |
        /// Offset +8| TXE| -- | UE | EE | FE | LCE| OE | TSE| -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset +A| -- | INT| TS |PINS|IINS| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset +C| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset +E| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+10| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+12| BDU| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+14|                       1588 timestamp - low halfword                           |
        /// Offset+16|                       1588 timestamp - high halfword                          |
        /// Offset+18| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+1A| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+1C| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+1E| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        ///          =================================================================================


        private class DmaTxBufferDescriptor : DmaBufferDescriptor
        {
            public DmaTxBufferDescriptor(SystemBus bus, uint address, bool isExtendedModeEnabled) :
                base(bus, address, isExtendedModeEnabled)
            {
            }

            public byte[] ReadBuffer()
            {
                var result = Bus.ReadBytes(GetDataBufferAddress(), Length, true);
                //IsUsed = true;
                return result;
            }

            public ushort Length => (ushort)words[0];

            public bool IsCRCIncluded => BitHelper.IsBitSet(words[1], 10);

            public bool IsReady
            {
                get
                {
                    return BitHelper.IsBitSet(words[1], 15);
                }
                set
                {
                    BitHelper.SetBit(ref words[1], 15, value);
                }
            }
        }

        /// Legacy Receive Buffer
        /// 
        ///          =================================================================================
        ///          |             Byte 1                    |           Byte 0                      |
        ///          | 15 | 14 | 13 | 12 | 11 | 10 | 09 | 08 | 07 | 06 | 05 | 04 | 03 | 02 | 01 | 00 |
        ///          =================================================================================
        /// Offset +0|                              Data Length                                      |
        /// Offset +2| E  | RO1| W  | RO2| L  | -- | -- | M  | BC | MC | LG | NO | -- | -- | -- | TR |
        /// Offset +4|                       Rx Data Buffer Pointer -- low halfword                  |
        /// Offset +6|                       Rx Data Buffer Pointer -- high halfword                 |
        ///          =================================================================================
        /// 
        /// 
        /// Enhanced Receive Buffer
        /// 
        ///          =================================================================================
        ///          |             Byte 1                    |           Byte 0                      |
        ///          | 15 | 14 | 13 | 12 | 11 | 10 | 09 | 08 | 07 | 06 | 05 | 04 | 03 | 02 | 01 | 00 |
        ///          =================================================================================
        /// Offset +0|                              Data Length                                      |
        /// Offset +2| E  | RO1| W  | RO2| L  | -- | -- | M  | BC | MC | LG | NO | -- | CR | OV | TR |
        /// Offset +4|                       Rx Data Buffer Pointer -- low halfword                  |
        /// Offset +6|                       Rx Data Buffer Pointer -- high halfword                 |
        /// Offset +8|    VPCP      | -- | -- | -- | -- | -- | -- | -- | ICE| PCR| -- |VLAN|IPV6|FRAG|
        /// Offset +A| ME | -- | -- | -- | -- | PE | CE | UC | INT| -- | -- | -- | -- | -- | -- | -- |
        /// Offset +C|                              Payload Checksum                                 |
        /// Offset +E|     Header length      | -- | -- | -- |              Protocol Type            |
        /// Offset+10| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+12| BDU| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+14|                       1588 timestamp - low halfword                           |
        /// Offset+16|                       1588 timestamp - high halfword                          |
        /// Offset+18| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+1A| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+1C| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+1E| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        ///          =================================================================================


        private class DmaRxBufferDescriptor : DmaBufferDescriptor
        {
            public DmaRxBufferDescriptor(SystemBus bus, uint address, bool isExtendedModeEnabled) :
                base(bus, address, isExtendedModeEnabled)
            {
            }

            public byte[] ReadBuffer()
            {
                var result = Bus.ReadBytes(GetDataBufferAddress(), Length, true);
                //IsUsed = true;
                return result;
            }

            public ushort Length 
            {
                get
                {
                    return (ushort)words[0];
                }
                set
                {
                    words[0] = value;
                }
            }

            public bool IsEmpty
            {
                get
                {
                    return BitHelper.IsBitSet(words[1], 15);
                }
                set
                {
                    BitHelper.SetBit(ref words[1], 15, value);
                }
            }

            public new bool IsLast
            {
                set 
                {
                    BitHelper.SetBit(ref words[1], 11, value);
                }
            }

            public bool WriteBuffer(byte[] bytes, uint length)
            {
                if(bytes.Length > MaximumBufferLength)
                {
                    return false;
                }

                Length = (ushort)Length;
                Bus.WriteBytes(bytes, GetDataBufferAddress(), true);

                return true;
            }
            private const int MaximumBufferLength = (1 << 13) - 1;
        }
    }
}