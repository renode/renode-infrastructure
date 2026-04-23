//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.Network
{
    // 8-, 16- and 32-bit accesses are supported to the Ethernet MAC registers, but only 32-bit-aligned accesses to the DMA registers (RM 0090/0385/0410)
    // Only 32-bit access is currently handled
    public class SynopsysEthernetMAC : NetworkWithPHY, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IMACInterface, IKnownSize
    {
        public SynopsysEthernetMAC(IMachine machine, BusWidth? busWidth = null, SynopsysEthernetVersion version = SynopsysEthernetVersion.STM32F) : base(machine)
        {
            if(busWidth.HasValue)
            {
                DMABusWidth = busWidth.Value;
            }
            else
            {
                this.Log(LogLevel.Info, $"{nameof(SynopsysEthernetMAC)}: Bus width not specified, defaulting to {nameof(BusWidth.Bits32)}");
                DMABusWidth = BusWidth.Bits32;
            }
            this.version = version;
            RegistersCollection = new DoubleWordRegisterCollection(this, CreateRegisterMap());
            IRQ = new GPIO();
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            dmaLock = new object();
            rxFifo = new Queue<EthernetFrame>();
            rxFifoSize = 0;
            txFrameState = new TxFrameState(this);
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            txFrameState.Reset();
            rxFifo.Clear();
            rxFifoSize = 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            if(!receiverEnable.Value)
            {
                this.Log(LogLevel.Debug, "Receiver disabled, dropping packet");
                return;
            }
            TryEnqueueRxFrame(frame);
            ProcessReceivePath();
        }

        public MACAddress MAC { get; set; }

        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        public long Size => 0x1400;

        public GPIO IRQ { get; private set; }

        public object NormalDescriptorType => version switch
        {
            SynopsysEthernetVersion.STM32F => typeof(STM32FNormalTxDescriptor),
            SynopsysEthernetVersion.BeagleV => typeof(BeagleVNormalTxDescriptor),
            _ => throw new ArgumentOutOfRangeException($"Unknown SynopsysEthernetVersion: {version}"),
        };

        public event Action<EthernetFrame> FrameReady;

        public readonly BusWidth DMABusWidth;

        private void UpdateInterrupts()
        {
            // NIS
            {
                dmaNormalInterruptSummary.Value |= dmaTransmitStatus.Value && dmaTransmitInterruptEnable.Value;
                dmaNormalInterruptSummary.Value |= dmaTransmitBufferUnavailableStatus.Value && dmaTransmitBufferUnavailableInterruptEnable.Value;
                dmaNormalInterruptSummary.Value |= dmaReceiveStatus.Value && dmaReceiveInterruptEnable.Value;
                dmaNormalInterruptSummary.Value |= dmaEarlyReceiveStatus.Value && dmaEarlyReceiveInterruptEnable.Value;
            }
            // AIS
            {
                dmaAbnormalInterruptSummary.Value |= dmaTransmitProcessStoppedStatus.Value && dmaTransmitProcessStoppedInterruptEnable.Value;
                dmaAbnormalInterruptSummary.Value |= dmaTransmitUnderflowStatus.Value && dmaTransmitUnderflowInterruptEnable.Value;
                dmaAbnormalInterruptSummary.Value |= dmaReceiveWatchdogTimeoutStatus.Value && dmaReceiveWatchdogTimeoutInterruptEnable.Value;
                dmaAbnormalInterruptSummary.Value |= dmaFatailBusErrorStatus.Value && dmaFatalBusErrorInterruptEnable.Value;
                dmaAbnormalInterruptSummary.Value |= dmaReceiveOverflowStatus.Value && dmaReceiveOverflowInterruptEnable.Value;
                dmaAbnormalInterruptSummary.Value |= dmaReceiveBufferUnavailableStatus.Value && dmaReceiveBufferUnavailableInterruptEnable.Value;
                dmaAbnormalInterruptSummary.Value |= dmaEarlyTransmitStatus.Value && dmaEarlyTransmitInterruptEnable.Value;
                dmaAbnormalInterruptSummary.Value |= dmaTransmitJabberTimeoutStatus.Value && dmaTransmitJabberTimeoutInterruptEnable.Value;
                dmaAbnormalInterruptSummary.Value |= dmaReceiveProcessStoppedStatus.Value && dmaReceiveProcessStoppedInterruptEnable.Value;
            }
            var set_irq = false;
            set_irq |= dmaNormalInterruptSummary.Value && dmaNormalInterruptSummaryEnable.Value;
            set_irq |= dmaAbnormalInterruptSummary.Value && dmaAbnormalInterruptSummaryEnable.Value;
            this.Log(LogLevel.Noisy, $"Updating IRQ: {set_irq}");
            IRQ.Set(set_irq);
        }

        private bool TryMoveOneReceiveFrameToMemory()
        {
            if(!dmaStartStopReceive.Value)
            {
                this.Log(LogLevel.Noisy, "Receive path processing requested, but DMAOMR Start/stop receive is disabled");
                return false;
            }

            if(rxFifo.Count == 0)
            {
                this.Log(LogLevel.Noisy, "Receive path processing requested, but receive FIFO is empty");
                return false;
            }

            var frame = rxFifo.Peek();
            this.Log(LogLevel.Noisy, $"Receiving packet, length: {frame.Bytes.Length}");
            var descriptor = new DMADescriptor(this, (uint)dmaCurrentReceiveDescriptorAddress.Value);
            descriptor.Fetch();
            var decodedDescriptor = descriptor.GetNormalRxDescriptor(version);
            var frameTransferred = false;
            var rxFrameBytesWritten = 0;
            var dmaRxProcessFirstDescriptorSet = false;

            // Iterate through descriptors until the whole frame is copied over or we run out of descriptors
            while(!frameTransferred && decodedDescriptor.Owned && rxFrameBytesWritten < frame.Bytes.Length)
            {
                if(!dmaRxProcessFirstDescriptorSet)
                {
                    decodedDescriptor.FirstDescriptor = true;
                    dmaRxProcessFirstDescriptorSet = true;
                }
                var bytesToWriteToBuffer1 = Math.Min(decodedDescriptor.Buffer1Size, frame.Bytes.Length - rxFrameBytesWritten);
                this.Log(LogLevel.Noisy, $"Writing {bytesToWriteToBuffer1} bytes to receive buffer at 0x{decodedDescriptor.Buffer1Address:X}");
                machine.SystemBus.WriteBytes(frame.Bytes, decodedDescriptor.Buffer1Address, startingIndex: rxFrameBytesWritten, count: bytesToWriteToBuffer1, context: this);
                rxFrameBytesWritten += bytesToWriteToBuffer1;

                if(!decodedDescriptor.SecondAddressChained)
                {
                    var bytesToWriteToBuffer2 = Math.Min(decodedDescriptor.Buffer2Size, frame.Bytes.Length - rxFrameBytesWritten);
                    this.Log(LogLevel.Noisy, $"Writing {bytesToWriteToBuffer2} bytes to receive buffer 2 at 0x{decodedDescriptor.Buffer2Address:X}");
                    machine.SystemBus.WriteBytes(frame.Bytes, decodedDescriptor.Buffer2Address, startingIndex: rxFrameBytesWritten, count: bytesToWriteToBuffer2, context: this);
                    rxFrameBytesWritten += bytesToWriteToBuffer2;
                }

                decodedDescriptor.Owned = false;
                if(rxFrameBytesWritten >= frame.Bytes.Length)
                {
                    this.Log(LogLevel.Debug, $"Packet of length {frame.Bytes.Length} delivered.");
                    decodedDescriptor.LastDescriptor = true;
                    decodedDescriptor.FrameLength = (ushort)frame.Bytes.Length;
                    rxFifo.Dequeue();
                    rxFifoSize -= frame.Bytes.Length;
                    rxFrameBytesWritten = 0;
                    dmaRxProcessFirstDescriptorSet = false;
                    if(!decodedDescriptor.DisableInterruptOnCompletion)
                    {
                        dmaReceiveStatus.Value = true;
                        UpdateInterrupts();
                    }
                    frameTransferred = true;
                }

                ulong nextDescriptorAddress;
                if(decodedDescriptor.EndOfRing)
                {
                    nextDescriptorAddress = dmaReceiveDescriptorListAddress.Value;
                    this.Log(LogLevel.Noisy, $"Next Address: Receive descriptor at 0x{dmaCurrentReceiveDescriptorAddress.Value:X} is end of ring, wrapping to 0x{nextDescriptorAddress:X}");
                }
                else if(decodedDescriptor.SecondAddressChained)
                {
                    nextDescriptorAddress = decodedDescriptor.NextDescriptorAddress;
                    this.Log(LogLevel.Noisy, $"Next Address: Receive descriptor at 0x{dmaCurrentReceiveDescriptorAddress.Value:X} is chained, next address: 0x{nextDescriptorAddress:X}");
                }
                else
                {
                    nextDescriptorAddress = (ulong)(descriptor.Address + descriptor.Size) + dmaDescriptorSkipLengthWords.Value * 4;
                    this.Log(LogLevel.Noisy, $"Next Address: Receive descriptor at 0x{dmaCurrentReceiveDescriptorAddress.Value:X} is not chained and not end of ring, next address: 0x{nextDescriptorAddress:X}");
                }

                var nextDescriptor = new DMADescriptor(this, (uint)nextDescriptorAddress);
                nextDescriptor.Fetch();
                var nextDecodedDescriptor = nextDescriptor.GetNormalRxDescriptor(version);
                if(!frameTransferred && !nextDecodedDescriptor.Owned)
                {
                    this.Log(LogLevel.Debug, "Next receive descriptor not owned by DMA, dropping partial frame from FIFO");
                    decodedDescriptor.DescriptorError = true;
                    decodedDescriptor.ErrorSummary = true;
                    decodedDescriptor.LastDescriptor = true;
                    // Drop partial frame
                    rxFifo.Dequeue();
                    dmaReceiveBufferUnavailableStatus.Value = true;
                    UpdateInterrupts();
                }
                descriptor.SetDescriptor(decodedDescriptor);
                descriptor.Write();

                descriptor = nextDescriptor;
                decodedDescriptor = nextDecodedDescriptor;
                dmaCurrentReceiveDescriptorAddress.Value = nextDescriptorAddress;
            }

            return frameTransferred;
        }

        private void ProcessReceivePath()
        {
            lock(dmaLock)
            {
                while(TryMoveOneReceiveFrameToMemory())
                {
                }
            }
            UpdateInterrupts();
        }

        private bool SendFrameIfReady()
        {
            if(!transmitterEnable.Value || !txFrameState.Ready)
            {
                return false;
            }

            FrameReady?.Invoke(txFrameState.Frame);
            if(txFrameState.InterruptOnCompletion)
            {
                dmaTransmitStatus.Value = true;
                UpdateInterrupts();
            }
            txFrameState.Reset();
            return true;
        }

        private void ProcessTransmitPath()
        {
            do
            {
                SendFrameIfReady();
            } while(AdvanceTxDma());

            UpdateInterrupts();
        }

        private bool AdvanceTxDma()
        {
            if(!dmaStartStopTransmission.Value)
            {
                this.Log(LogLevel.Debug, "TX DMA processing requested, but DMAOMR Start/stop transmission is disabled");
                return false;
            }

            if(txFrameState.Ready)
            {
                this.Log(LogLevel.Debug, "TX DMA processing requested, but transmit frame is already ready");
                return false;
            }

            dmaTransmitProcessState.Value = TxStates.FetchingDescriptor;
            var descriptor = new DMADescriptor(this, (uint)dmaCurrentTransmitDescriptorAddress.Value);
            descriptor.Fetch();
            var decodedDescriptor = descriptor.GetNormalTxDescriptor(version);

            if(!decodedDescriptor.Owned)
            {
                dmaTransmitProcessState.Value = TxStates.Suspended;
                dmaTransmitBufferUnavailableStatus.Value = true;
                return false;
            }

            dmaTransmitProcessState.Value = TxStates.ReadingData;
            this.Log(LogLevel.Noisy, $"Adding {decodedDescriptor.Buffer1Size} bytes from 0x{decodedDescriptor.Buffer1Address:X} to transmit packet data");
            txFrameState.PartialBytes.AddRange(machine.SystemBus.ReadBytes(decodedDescriptor.Buffer1Address, decodedDescriptor.Buffer1Size, context: this));
            if(!decodedDescriptor.SecondAddressChained)
            {
                this.Log(LogLevel.Noisy, $"Adding {decodedDescriptor.Buffer2Size} bytes from 0x{decodedDescriptor.Buffer2Address:X} to transmit packet data");
                txFrameState.PartialBytes.AddRange(machine.SystemBus.ReadBytes(decodedDescriptor.Buffer2Address, decodedDescriptor.Buffer2Size, context: this));
            }

            if(decodedDescriptor.FirstSegment)
            {
                txFrameState.DisableCRC = decodedDescriptor.DisableCRC;
                txFrameState.DisablePad = decodedDescriptor.DisablePad;
            }

            if(decodedDescriptor.LastSegment)
            {
                txFrameState.ConvertToReadyFrame(interruptOnCompletion: decodedDescriptor.InterruptOnCompletion, checksumInsertionControl: decodedDescriptor.ChecksumInsertionControl);
            }

            ulong nextDescriptorAddress;
            if(decodedDescriptor.SecondAddressChained)
            {
                nextDescriptorAddress = decodedDescriptor.NextDescriptorAddress;
                this.Log(LogLevel.Noisy, $"Next Address: Transmit descriptor at 0x{dmaCurrentTransmitDescriptorAddress.Value:X}: chained, next address: 0x{nextDescriptorAddress:X}");
            }
            else if(decodedDescriptor.EndOfRing)
            {
                nextDescriptorAddress = (uint)dmaTransmitDescriptorListAddress.Value;
                this.Log(LogLevel.Noisy, $"Next Address: Transmit descriptor at 0x{dmaCurrentTransmitDescriptorAddress.Value:X}: end of ring, wrapping to 0x{nextDescriptorAddress:X}");
            }
            else
            {
                nextDescriptorAddress = (uint)(descriptor.Address + descriptor.Size) + dmaDescriptorSkipLengthWords.Value * 4;
                this.Log(LogLevel.Noisy, $"Next Address: Transmit descriptor at 0x{dmaCurrentTransmitDescriptorAddress.Value:X}: is not chained and not end of ring, next address: 0x{nextDescriptorAddress:X}");
            }

            dmaCurrentTransmitDescriptorAddress.Value = nextDescriptorAddress;
            dmaTransmitProcessState.Value = TxStates.ClosingDescriptor;
            decodedDescriptor.Owned = !decodedDescriptor.Owned;
            descriptor.SetDescriptor(decodedDescriptor);
            descriptor.Write();

            return true;
        }

        private bool TryEnqueueRxFrame(EthernetFrame frame)
        {
            if(rxFifoSize + frame.Bytes.Length > FifoDepth)
            {
                this.Log(LogLevel.Warning, $"Receive FIFO full, dropping packet of length {frame.Bytes.Length}");
                return false;
            }

            rxFifo.Enqueue(frame);
            rxFifoSize += frame.Bytes.Length;
            return true;
        }

        private IDictionary<long, DoubleWordRegister> CreateRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.MACConfiguration, new DoubleWordRegister(this, 0x8000)
                    .WithReservedBits(0, 2)
                    .WithFlag(2, name: "MACCR.RE (Receiver enable)", flagField: out receiverEnable)
                    .WithFlag(3, name: "MACCR.TE (Transmitter enable)", flagField: out transmitterEnable, writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            ProcessTransmitPath();
                        }
                    })
                    .WithTaggedFlag(position: 4, name: "MACCR.DC (Deferral check)")
                    .WithTag(position: 5, width: 2, name: "MACCR.BL (Back-off limit)")
                    .WithTaggedFlag(position: 7, name: "MACCR.APCS (Automatic pad/CRC stripping)")
                    .WithReservedBits(8, 1)
                    .WithTaggedFlag(position: 9, name: "MACCR.RD (Retry disable)")
                    .WithTaggedFlag(position: 10, name: "MACCR.IPCO (IPv4 checksum offload)")
                    .WithTaggedFlag(position: 11, name: "MACCR.DM (Duplex mode)")
                    .WithTaggedFlag(position: 12, name: "MACCR.LM (Loopback mode)")
                    .WithTaggedFlag(position: 13, name: "MACCR.ROD (Receive own disable)")
                    .WithTaggedFlag(position: 14, name: "MACCR.FES (Fast Ethernet speed)")
                    .WithReservedBits(15, 1)
                    .WithTaggedFlag(position: 16, name: "MACCR.CSD (Carrier sense disable)")
                    .WithTag(position: 17, width: 3, name: "MACCR.IFG (Interframe gap)")
                    .WithReservedBits(20, 2)
                    .WithTaggedFlag(position: 22, name: "MACCR.JD (Jabber disable)")
                    .WithTaggedFlag(position: 23, name: "MACCR.WD (Watchdog disable)")
                    .WithReservedBits(24, 1)
                    .WithTaggedFlag(position: 25, name: "MACCR.CSTF (CRC stripping for Type frames)")
                    .WithReservedBits(26, 6)
                },
                {(long)Registers.MACFrameFilter, new DoubleWordRegister(this, 0x0)
                    .WithTaggedFlag(position: 0, name: "MACFFR/PM (Promiscuous mode)")
                    .WithTaggedFlag(position: 1, name: "MACFFR.HU (Hash unicast)")
                    .WithTaggedFlag(position: 2, name: "MACFFR.HM (Hash multicast)")
                    .WithTaggedFlag(position: 3, name: "MACFFR.DAIF (Destination address inverse filtering)")
                    .WithTaggedFlag(position: 4, name: "MACFFR.PAM (Pass all multicast)")
                    .WithTaggedFlag(position: 5, name: "MACFFR.BFD (Broadcast frames disable)")
                    .WithValueField(6, 2, name: "MACFFR.PCF (Pass control frames)")
                    .WithTaggedFlag(position: 8, name: "MACFFR.SAIF (Source address inverse filtering)")
                    .WithTaggedFlag(position: 9, name: "MACFFR.SAF (Source address filter)")
                    .WithTaggedFlag(position: 10, name: "MACFFR.HPF (Hash or perfect filter)")
                    .WithReservedBits(11, 20)
                    .WithTaggedFlag(position: 31, name: "MACFFR.RA (Receive all)")
                },
                /*
                 * Skipped: MACHTHR .. MACHTLR
                 */
                {(long)Registers.MACMIIAddress, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0, name: "MACMIIAR.MB (MII busy)", flagField: out var miiBusy)
                    .WithFlag(1, name: "MACMIIAR.MW (MII write)", flagField: out var miiWrite)
                    .WithValueField(2, 3, name: "MACMIIAR.CR (Clock Range)", valueField: out var _)
                    .WithReservedBits(5, 1)
                    .WithValueField(6, 5, name: "MACMIIAR.MR (MII register)", valueField: out var miiRegister)
                    .WithValueField(11, 5, name: "MACMIIAR.PA (PHY address)", valueField: out var miiPhyAddress)
                    .WithReservedBits(16, 16)
                    .WithWriteCallback((_, value) =>
                    {
                        if (!TryGetPhy<ushort>((uint)miiPhyAddress.Value, out var phy))
                        {
                            this.Log(LogLevel.Warning, $"Tried to access non-existent PHY: {miiPhyAddress.Value}");
                            miiBusy.Value = false;
                            return;
                        }

                        if (miiWrite.Value)
                        {
                                phy.Write((ushort)miiRegister.Value, (ushort)miiData.Value);
                        }
                        else
                        {
                                miiData.Value = phy.Read((ushort)miiRegister.Value);
                        }
                        miiBusy.Value = false;
                    })
                },
                {(long)Registers.MACMIIData, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 16, out miiData, name: "MACMIIDR.MD (MII data)")
                    .WithReservedBits(16, 16)
                },
                /*
                 * Skipped: MACFCR .. MACIMR
                 */
                {(long)Registers.MACAddress0High, new DoubleWordRegister(this, 0x8000_FFFF)
                    .WithValueField(0, 16, name: "MACA0HR.MACA0H (MAC address0 high)", changeCallback: (_, value) =>
                    {
                        var (e, f) = ((byte)value, (byte)(value >> 8));
                        MAC = MAC.WithNewOctets(e: e, f: f);
                    })
                    .WithReservedBits(16, 15)
                    .WithFlag(31, name: "MACA0HR.MO", valueProviderCallback: _ => true)
                },
                {(long)Registers.MACAddress0Low, new DoubleWordRegister(this, 0xFFFF_FFFF)
                    .WithValueField(0, 32, name: "MACA0LR.MACA0L (MAC address0 low)", changeCallback: (_, value) =>
                    {
                        var (a, b, c, d) = ((byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24));
                        MAC = MAC.WithNewOctets(a: a, b: b, c: c, d: d);
                    })
                },
                /*
                 * Skipped: MACA1HR .. PTPPPSCR
                 */
                 // Technically reset value is 0x20101, but writing to SR resets+clears the SR bit
                {(long)Registers.DMABusMode, new DoubleWordRegister(this, 0x0002_0100)
                    .WithFlag(name: "DMABMR.SR (Software reset)", position: 0, writeCallback: (_, value) =>
                    {
                        if (value)
                        {
                            Reset();
                        }
                    })
                    .WithTaggedFlag(position: 1, name: "DMABMR.DA (DMA arbitration)")
                    .WithValueField(2, 5, name: "DMABMR.DSL (Descriptor skip length)", valueField: out dmaDescriptorSkipLengthWords)
                    .WithTaggedFlag(position: 7, name: "DMABMR.EDFE (Enhanced descriptor format enable)")
                    .WithTag(position: 8, width: 6, name: "DMABMR.PBL (Programmable burst length)")
                    .WithTag(position: 14, width: 2, name: "DMABMR.PM (Rx Tx priority ratio)")
                    .WithFlag(position: 16, name: "DMABMR.FB (Fixed burst)")
                    .WithTag(position: 17, width: 6, name: "DMABMR.RDP (Rx DMA PBL)")
                    .WithTaggedFlag(position: 23, name: "DMABMR.USP (Use separate PBL)")
                    .WithTaggedFlag(position: 24, name: "DMABMR.FPM (4xPBL mode)")
                    .WithTaggedFlag(position: 25, name: "DMABMR.AAB (Address-aligned beats)")
                    .WithTaggedFlag(position: 26, name: "DMABMR.MB (Mixed burst)")
                    .WithReservedBits(27, 5)
                },
                {(long)Registers.DMATransmitPollDemand, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 32, name: "DMATPDR.TPD (Transmit poll demand)", writeCallback: (_, value) => ProcessTransmitPath())
                },
                {(long)Registers.DMAReceivePollDemand, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 32, name: "DMARPDR.TPD (Receive poll demand)", writeCallback: (_, value) => ProcessReceivePath())
                },
                {(long)Registers.DMAReceiveDescriptorListAddress, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 32, name: "DMARDLAR.SRL (Start of receive list)", valueField: out dmaReceiveDescriptorListAddress, writeCallback: (_, __) =>
                    {
                        // Reference manuals:
                        // > The LSB bits [1/2/3:0] for 32/64/128-bit bus width) are internally ignored and taken as all-zero by the DMA. Hence these LSB bits are read only.
                        dmaCurrentReceiveDescriptorAddress.Value = (uint)dmaReceiveDescriptorListAddress.Value & ~((uint)DMABusWidth);
                    })
                },
                {(long)Registers.DMATransmitDescriptorListAddress, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 32, name: "DMATDLAR.STL (Start of transmit list)", valueField: out dmaTransmitDescriptorListAddress, writeCallback: (_, __) =>
                    {
                        dmaCurrentTransmitDescriptorAddress.Value = (uint)dmaTransmitDescriptorListAddress.Value & ~((uint)DMABusWidth);
                    })
                },
                {(long)Registers.DMAStatus, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.TS (Transmit status)", flagField: out dmaTransmitStatus)
                    .WithFlag(1, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.TPSS (Transmit process stopped status)", flagField: out dmaTransmitProcessStoppedStatus)
                    .WithFlag(2, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.TBUS (Transmit buffer unavailable status)", flagField: out dmaTransmitBufferUnavailableStatus)
                    .WithFlag(3, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.TJTS (Transmit jabber timeout status)", flagField: out dmaTransmitJabberTimeoutStatus)
                    .WithFlag(4, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.ROS (Receive overflow status)", flagField: out dmaReceiveOverflowStatus)
                    .WithFlag(5, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.TUS (Transmit underflow status)", flagField: out dmaTransmitUnderflowStatus)
                    .WithFlag(6, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.RS (Receive status)", flagField: out dmaReceiveStatus)
                    .WithFlag(7, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.RBUS (Receive buffer unavailable status)", flagField: out dmaReceiveBufferUnavailableStatus)
                    .WithFlag(8, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.RPSS (Receive process stopped status)", flagField: out dmaReceiveProcessStoppedStatus)
                    .WithFlag(9, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.RWTS (Receive watchdog timeout status)", flagField: out dmaReceiveWatchdogTimeoutStatus)
                    .WithFlag(10, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.ETS (Early transmit status)", flagField: out dmaEarlyTransmitStatus)
                    .WithReservedBits(11, 2)
                    .WithFlag(13, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.FBES (Fatal bus error status)", flagField: out dmaFatailBusErrorStatus)
                    .WithFlag(14, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.ERS (Early receive status)", flagField: out dmaEarlyReceiveStatus)
                    .WithFlag(15, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.AIS (Abnormal interrupt summary)", flagField: out dmaAbnormalInterruptSummary)
                    .WithFlag(16, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.NIS (Normal interrupt summary)", flagField: out dmaNormalInterruptSummary)
                    .WithEnumField(17, 3, mode: FieldMode.Read, name: "DMASR.RPS (Receive process state)", enumField: out dmaReceiveProcessState)
                    .WithEnumField(20, 3, mode: FieldMode.Read, name: "DMASR.TPS (Transmit process state)", enumField: out dmaTransmitProcessState)
                    .WithValueField(23, 3, mode: FieldMode.Read, name: "DMASR.EBS (Error bits status)", valueField: out dmaErrorBitsStatus)
                    .WithReservedBits(26, 1)
                    .WithFlag(27, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.MMCS (MMC status)", flagField: out dmaMmcStatus)
                    .WithFlag(28, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.PMTS (PMT status)", flagField: out dmaPmtStatus)
                    .WithFlag(29, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "DMASR.TSTS (Time stamp trigger status)", flagField: out dmaTimestampTriggerStatus)
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.DMAOperationMode, new DoubleWordRegister(this, 0x0)
                    .WithReservedBits(0, 1)
                    .WithFlag(1, name: "DMAOMR.SR (Start/stop receive)", flagField: out dmaStartStopReceive)
                    .WithFlag(2, name: "DMAOMR.OSF (Operate on second frame)", flagField: out dmaOperateOnSecondFrame)
                    .WithTag(position: 3, width: 2, name: "DMAOMR.RTC (Receive threshold control)")
                    .WithReservedBits(5, 1)
                    .WithTaggedFlag(position: 6, name: "DMAOMR.FUGF (Forward undersized good frames)")
                    .WithTaggedFlag(position: 7, name: "DMAOMR.FEF (Forward error frames)")
                    .WithReservedBits(8, 5)
                    .WithFlag(13, name: "DMAOMR.ST (Start/stop transmission)", flagField: out dmaStartStopTransmission, writeCallback: (_, value) =>
                    {
                        if (value)
                        {
                            ProcessTransmitPath();
                        }
                    })
                    .WithTag(position: 14, width: 3, name: "DMAOMR.TTC (Transmit threshold control)")
                    .WithReservedBits(17, 3)
                    .WithFlag(20, name: "DMAOMR.FTF (Flush transmit FIFO)", valueProviderCallback: _ => false)
                    .WithTaggedFlag(position: 21, name: "DMAOMR.TSF (Transmit store and forward)")
                    .WithReservedBits(22, 2)
                    .WithTaggedFlag(position: 24, name: "DMAOMR.DFRF (Disable flushing of received frames)")
                    .WithTaggedFlag(position: 25, name: "DMAOMR.RSF (Receive store and forward)")
                    .WithTaggedFlag(position: 26, name: "DMAOMR.DTCEFD (Dropping of TCP/IP checksum error frames disable)")
                    .WithReservedBits(27, 5)
                },
                {(long)Registers.DMAInterruptEnable, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0, name: "DMAIER.TIE (Transmit interrupt enable)", flagField: out dmaTransmitInterruptEnable)
                    .WithFlag(1, name: "DMAIER.TPSIE (Transmit process stopped interrupt enable)", flagField: out dmaTransmitProcessStoppedInterruptEnable)
                    .WithFlag(2, name: "DMAIER.TBUIE (Transmit buffer unavailable interrupt enable)", flagField: out dmaTransmitBufferUnavailableInterruptEnable)
                    .WithFlag(3, name: "DMAIER.TJTIE (Transmit jabber timeout interrupt enable)", flagField: out dmaTransmitJabberTimeoutInterruptEnable)
                    .WithFlag(4, name: "DMAIER.ROIE (Overflow interrupt enable)", flagField: out dmaReceiveOverflowInterruptEnable)
                    .WithFlag(5, name: "DMAIER.TUIE (Underflow interrupt enable)", flagField: out dmaTransmitUnderflowInterruptEnable)
                    .WithFlag(6, name: "DMAIER.RIE (Receive interrupt enable)", flagField: out dmaReceiveInterruptEnable)
                    .WithFlag(7, name: "DMAIER.RBUIE (Receive buffer unavailable interrupt enable)", flagField: out dmaReceiveBufferUnavailableInterruptEnable)
                    .WithFlag(8, name: "DMAIER.RPSIE (Receive process stopped interrupt enable)", flagField: out dmaReceiveProcessStoppedInterruptEnable)
                    .WithFlag(9, name: "DMAIER.RWTIE (Receive watchdog timeout interrupt enable)", flagField: out dmaReceiveWatchdogTimeoutInterruptEnable)
                    .WithFlag(10, name: "DMAIER.ETIE (Early transmit interrupt enable)", flagField: out dmaEarlyTransmitInterruptEnable)
                    .WithReservedBits(11, 2)
                    .WithFlag(13, name: "DMAIER.FBEIE (Fatal bus error interrupt enable)", flagField: out dmaFatalBusErrorInterruptEnable)
                    .WithFlag(14, name: "DMAIER.ERIE (Early receive interrupt enable)", flagField: out dmaEarlyReceiveInterruptEnable)
                    .WithFlag(15, name: "DMAIER.AISE (Abnormal interrupt summary enable)", flagField: out dmaAbnormalInterruptSummaryEnable)
                    .WithFlag(16, name: "DMAIER.NISE (Normal interrupt summary enable)", flagField: out dmaNormalInterruptSummaryEnable)
                    .WithReservedBits(17, 15)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                /*
                 * Skipped: DMAMFBOCR .. DMARSWTR
                 */
                 {(long)Registers.DMACurrentHostTransmitDescriptor, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 32, name: "DMACHR.HTDAP (Host transmit descriptor address pointer)", valueField: out dmaCurrentReceiveDescriptorAddress)
                 },
                 {(long)Registers.DMACurrentHostReceiveDescriptor, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 32, name: "DMACHR.HRDAP (Host receive descriptor address pointer)", valueField: out dmaCurrentTransmitDescriptorAddress)
                 },
                 {(long)Registers.DMACurrentHostTransmitBufferAddress, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 32, name: "DMACHR.HTBAP (Host transmit buffer address pointer)")
                 },
                 {(long)Registers.DMACurrentHostReceiveBufferAddress, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 32, name: "DMACHR.HRBAP (Host receive buffer address pointer)")
                 }
            };
        }

        private int rxFifoSize;

        private IFlagRegisterField receiverEnable;

        private IFlagRegisterField transmitterEnable;
        private IValueRegisterField miiData;
        private IFlagRegisterField dmaStartStopReceive;
        private IFlagRegisterField dmaOperateOnSecondFrame;
        private IFlagRegisterField dmaStartStopTransmission;
        private IValueRegisterField dmaReceiveDescriptorListAddress;
        private IValueRegisterField dmaTransmitDescriptorListAddress;
        private IValueRegisterField dmaCurrentReceiveDescriptorAddress;
        private IValueRegisterField dmaCurrentTransmitDescriptorAddress;
        private IFlagRegisterField dmaTransmitStatus;
        private IFlagRegisterField dmaTransmitProcessStoppedStatus;
        private IFlagRegisterField dmaTransmitBufferUnavailableStatus;
        private IFlagRegisterField dmaTransmitJabberTimeoutStatus;
        private IFlagRegisterField dmaReceiveOverflowStatus;
        private IFlagRegisterField dmaTransmitUnderflowStatus;
        private IFlagRegisterField dmaReceiveStatus;
        private IFlagRegisterField dmaReceiveBufferUnavailableStatus;
        private IFlagRegisterField dmaReceiveProcessStoppedStatus;
        private IFlagRegisterField dmaReceiveWatchdogTimeoutStatus;
        private IFlagRegisterField dmaEarlyTransmitStatus;
        private IFlagRegisterField dmaFatailBusErrorStatus;
        private IFlagRegisterField dmaEarlyReceiveStatus;
        private IFlagRegisterField dmaAbnormalInterruptSummary;
        private IFlagRegisterField dmaNormalInterruptSummary;
        private IEnumRegisterField<RxStates> dmaReceiveProcessState;
        private IEnumRegisterField<TxStates> dmaTransmitProcessState;
        private IValueRegisterField dmaErrorBitsStatus;
        private IFlagRegisterField dmaMmcStatus;
        private IFlagRegisterField dmaPmtStatus;
        private IFlagRegisterField dmaTimestampTriggerStatus;
        private IFlagRegisterField dmaTransmitInterruptEnable;
        private IFlagRegisterField dmaReceiveInterruptEnable;
        private IFlagRegisterField dmaAbnormalInterruptSummaryEnable;
        private IFlagRegisterField dmaNormalInterruptSummaryEnable;
        private IFlagRegisterField dmaTransmitBufferUnavailableInterruptEnable;
        private IFlagRegisterField dmaEarlyReceiveInterruptEnable;
        private IFlagRegisterField dmaTransmitProcessStoppedInterruptEnable;
        private IFlagRegisterField dmaTransmitJabberTimeoutInterruptEnable;
        private IFlagRegisterField dmaTransmitUnderflowInterruptEnable;
        private IFlagRegisterField dmaReceiveOverflowInterruptEnable;
        private IFlagRegisterField dmaReceiveBufferUnavailableInterruptEnable;
        private IFlagRegisterField dmaReceiveProcessStoppedInterruptEnable;
        private IFlagRegisterField dmaReceiveWatchdogTimeoutInterruptEnable;
        private IFlagRegisterField dmaEarlyTransmitInterruptEnable;
        private IFlagRegisterField dmaFatalBusErrorInterruptEnable;
        private IValueRegisterField dmaDescriptorSkipLengthWords;
        private readonly SynopsysEthernetVersion version;
        private readonly object dmaLock;
        private readonly Queue<EthernetFrame> rxFifo;
        private const uint FifoDepth = 2 * 1024;

        [LeastSignificantByteFirst]
        public struct STM32FNormalRxDescriptor : INormalRxDescriptor
        {
            [PacketField, Offset(doubleWords: 0, bits: 31), Width(bits: 1)]
            public bool Owned { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 16), Width(bits: 14)]
            public uint FrameLength { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 15), Width(bits: 1)]
            public bool ErrorSummary { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 14), Width(bits: 1)]
            public bool DescriptorError { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 9), Width(bits: 1)]
            public bool FirstDescriptor { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 8), Width(bits: 1)]
            public bool LastDescriptor { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 31), Width(bits: 1)]
            public bool DisableInterruptOnCompletion { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 16), Width(bits: 13)]
            public int Buffer2Size { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 15), Width(bits: 1)]
            public bool EndOfRing { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 14), Width(bits: 1)]
            public bool SecondAddressChained { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 0), Width(bits: 13)]
            public int Buffer1Size { get; set; }

            [PacketField, Offset(doubleWords: 2, bits: 0), Width(bits: 32)]
            public uint Buffer1Address { get; set; }

            [PacketField, Offset(doubleWords: 3, bits: 0), Width(bits: 32)]
            public uint Buffer2Address { get; set; }
        }

        [LeastSignificantByteFirst]
        public struct BeagleVNormalRxDescriptor : INormalRxDescriptor
        {
            [PacketField, Offset(doubleWords: 0, bits: 31), Width(bits: 1)]
            public bool Owned { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 16), Width(bits: 14)]
            public uint FrameLength { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 15), Width(bits: 1)]
            public bool ErrorSummary { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 14), Width(bits: 1)]
            public bool DescriptorError { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 9), Width(bits: 1)]
            public bool FirstDescriptor { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 8), Width(bits: 1)]
            public bool LastDescriptor { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 31), Width(bits: 1)]
            public bool DisableInterruptOnCompletion { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 25), Width(bits: 1)]
            public bool EndOfRing { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 24), Width(bits: 1)]
            public bool SecondAddressChained { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 11), Width(bits: 11)]
            public int Buffer2Size { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 0), Width(bits: 11)]
            public int Buffer1Size { get; set; }

            [PacketField, Offset(doubleWords: 2, bits: 0), Width(bits: 32)]
            public uint Buffer1Address { get; set; }

            [PacketField, Offset(doubleWords: 3, bits: 0), Width(bits: 32)]
            public uint Buffer2Address { get; set; }
        }

        [LeastSignificantByteFirst]
        public struct STM32FNormalTxDescriptor : INormalTxDescriptor
        {
            [PacketField, Offset(doubleWords: 0, bits: 31), Width(bits: 1)]
            public bool Owned { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 30), Width(bits: 1)]
            public bool InterruptOnCompletion { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 29), Width(bits: 1)]
            public bool LastSegment { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 28), Width(bits: 1)]
            public bool FirstSegment { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 27), Width(bits: 1)]
            public bool DisableCRC { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 26), Width(bits: 1)]
            public bool DisablePad { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 22), Width(bits: 2)]
            public ChecksumInsertionMode ChecksumInsertionControl { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 21), Width(bits: 1)]
            public bool EndOfRing { get; set; }

            [PacketField, Offset(doubleWords: 0, bits: 20), Width(bits: 1)]
            public bool SecondAddressChained { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 16), Width(bits: 13)]
            public int Buffer2Size { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 0), Width(bits: 13)]
            public int Buffer1Size { get; set; }

            [PacketField, Offset(doubleWords: 2, bits: 0), Width(bits: 32)]
            public uint Buffer1Address { get; set; }

            [PacketField, Offset(doubleWords: 3, bits: 0), Width(bits: 32)]
            public uint Buffer2Address { get; set; }
        }

        [LeastSignificantByteFirst]
        public struct BeagleVNormalTxDescriptor : INormalTxDescriptor
        {
            [PacketField, Offset(doubleWords: 0, bits: 31), Width(bits: 1)]
            public bool Owned { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 31), Width(bits: 1)]
            public bool InterruptOnCompletion { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 30), Width(bits: 1)]
            public bool LastSegment { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 29), Width(bits: 1)]
            public bool FirstSegment { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 27), Width(bits: 2)]
            public ChecksumInsertionMode ChecksumInsertionControl { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 26), Width(bits: 1)]
            public bool DisableCRC { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 25), Width(bits: 1)]
            public bool EndOfRing { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 24), Width(bits: 1)]
            public bool SecondAddressChained { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 23), Width(bits: 1)]
            public bool DisablePad { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 11), Width(bits: 10)]
            public int Buffer2Size { get; set; }

            [PacketField, Offset(doubleWords: 1, bits: 0), Width(bits: 10)]
            public int Buffer1Size { get; set; }

            [PacketField, Offset(doubleWords: 2, bits: 0), Width(bits: 32)]
            public uint Buffer1Address { get; set; }

            [PacketField, Offset(doubleWords: 3, bits: 0), Width(bits: 32)]
            public uint Buffer2Address { get; set; }
        }

        public enum ChecksumInsertionMode
        {
            Disabled = 0b00,
            IPHeaderOnly = 0b01,
            IPHeaderAndPayload = 0b10,
            IPHeaderAndPayloadWithPseudoHeader = 0b11,
        }

        public enum SynopsysEthernetVersion
        {
            STM32F,
            BeagleV,
        }

        public enum Registers
        {
            // Ethernet registers
            MACConfiguration = 0x00, // MACCR
            MACFrameFilter = 0x04, // MACFFR
            MACHashTableHigh = 0x08, // MACHTHR
            MACHashTableLow = 0x0C, // MACHTLR
            MACMIIAddress = 0x10, // MACMIIAR
            MACMIIData = 0x14, // MACMIIDR
            MACFlowControl = 0x18, // MACFCR
            MACVLANTag = 0x1C, // MACVLANTR
            MACRemoteWakeupFrameFilter = 0x28, // MACRWUFFR
            MACPMTControlAndStatus = 0x2C, // MACPMTCSR
            MACDebug = 0x34, // MACDBGR
            MACInterruptStatus = 0x38, // MACSR
            MACInterruptMask = 0x3C, // MACIMR
            MACAddress0High = 0x40, // MACA0HR
            MACAddress0Low = 0x44, // MACA0LR
            MACAddress1High = 0x48, // MACA1HR
            MACAddress1Low = 0x4C, // MACA1LR
            MACAddress2High = 0x50, // MACA2HR
            MACAddress2Low = 0x54, // MACA2LR
            MACAddress3High = 0x58, // MACA3HR
            MACAddress3Low = 0x5C, // MACA3LR

            // MMC registers
            MMCControl = 0x100, // MMCCR
            MMCReceiveInterrupt = 0x104, // MMCRIR
            MMCTransmitInterrupt = 0x108, // MMCTIR
            MMCReceiveInterruptMask = 0x10C, // MMCRIMR
            MMCTransmitInterruptMask = 0x110, // MMCTIMR
            MMCTransmittedGoodFramesAfterSingleCollisionCounter = 0x14C, // MMCTGFSCCR
            MMCTransmittedGoodFramesAfterMultipleCollisionCounter = 0x150, // MMCTGFMSCCR
            MMCTransmittedGoodFramesCounter = 0x168, // MMCTGFCR
            MMCReceivedFramesWithCRCErrorCounter = 0x194, // MMCRFCECR
            MMCReceivedFramesWithAlignmentErrorCounter = 0x198, // MMCRFAECR
            MMCReceivedGoodUnicastFramesCounter = 0x1C4, // MMCRGUFCR

            // PTP registers
            PTPTimeStampControl = 0x700, // PTPTSCR
            PTPSubsecondIncrement = 0x704, // PTPSSIR
            PTPTimestampHigh = 0x708, // PTPTSHR
            PTPTimestampLow = 0x70C, // PTPTSLR
            PTPTimestampHighUpdate = 0x710, // PTPTSHUR
            PTPTimestampLowUpdate = 0x714, // PTPTSLUR
            PTPTimestampAddend = 0x718, // PTPTSAR
            PTPTargetTimeHigh = 0x71C, // PTPTTHR
            PTPTargetTimeLow = 0x720, // PTPTTLR
            PTPTimestampStatus = 0x728, // PTPTSSR
            PTPPPSControl = 0x72C, // PTPPPSCR

            // DMA registers
            DMABusMode = 0x1000, // DMABMR
            DMATransmitPollDemand = 0x1004, // DMATPDR
            DMAReceivePollDemand = 0x1008, // DMARPDR
            DMAReceiveDescriptorListAddress = 0x100C, // DMARDLAR
            DMATransmitDescriptorListAddress = 0x1010, // DMATDLAR
            DMAStatus = 0x1014, // DMASR
            DMAOperationMode = 0x1018, // DMAOMR
            DMAInterruptEnable = 0x101C, // DMAIER
            DMAMissedFrameAndBufferOverflowCounter = 0x1020, // DMAMFBOCR
            DMAReceiveStatusWatchdogTimer = 0x1024, // DMARSWTR
            DMACurrentHostTransmitDescriptor = 0x1048, // DMACHTDR
            DMACurrentHostReceiveDescriptor = 0x104C, // DMACHRDR
            DMACurrentHostTransmitBufferAddress = 0x1050, // DMACHTBAR
            DMACurrentHostReceiveBufferAddress = 0x1054, // DMACHRBAR
        }

        public enum BusWidth
        {
            Bits32 = 0b11,
            Bits64 = 0b111,
            Bits128 = 0b1111
        }

        private class TxFrameState
        {
            public TxFrameState(SynopsysEthernetMAC parent)
            {
                this.parent = parent;
                partialBytes = new List<byte>();
            }

            public void Reset()
            {
                Ready = false;
                DisablePad = false;
                DisableCRC = false;
                Frame = null;
                InterruptOnCompletion = false;
                partialBytes.Clear();
            }

            internal void ConvertToReadyFrame(bool interruptOnCompletion, ChecksumInsertionMode checksumInsertionControl)
            {
                var framePadded = false;
                if(!DisablePad && PartialBytes.Count < 64)
                {
                    framePadded = true;
                    this.parent.Log(LogLevel.Noisy, $"Padding transmit packet data with {64 - PartialBytes.Count} bytes");
                    PartialBytes.AddRange(new byte[64 - PartialBytes.Count]);
                }

                if(!Misc.TryCreateFrameOrLogWarning(this.parent, PartialBytes.ToArray(), out var frame, addCrc: framePadded || !DisableCRC))
                {
                    Reset();
                    return;
                }

                switch(checksumInsertionControl)
                {
                case ChecksumInsertionMode.Disabled:
                    break;
                case ChecksumInsertionMode.IPHeaderOnly:
                    frame.FillWithChecksums(new[] { EtherType.IpV4, EtherType.IpV6 }, new IPProtocolType[] { });
                    break;
                case ChecksumInsertionMode.IPHeaderAndPayload:
                case ChecksumInsertionMode.IPHeaderAndPayloadWithPseudoHeader:
                    frame.FillWithChecksums(new[] { EtherType.IpV4, EtherType.IpV6 }, new IPProtocolType[] { IPProtocolType.TCP, IPProtocolType.UDP, IPProtocolType.ICMP, IPProtocolType.ICMPV6 });
                    break;
                }

                InterruptOnCompletion = interruptOnCompletion;
                Frame = frame;
                Ready = true;
            }

            public List<byte> PartialBytes
            {
                get
                {
                    if(Ready)
                    {
                        throw new InvalidOperationException("Cannot access partial bytes when frame is ready");
                    }
                    return partialBytes;
                }
            }

            public EthernetFrame Frame { get; private set; }

            public bool Ready { get; private set; }

            public bool InterruptOnCompletion { get; private set; }

            public bool DisablePad = false;
            public bool DisableCRC = false;
            private readonly SynopsysEthernetMAC parent;
            private readonly List<byte> partialBytes;
        };

        readonly TxFrameState txFrameState;

        private class DMADescriptor
        {
            public DMADescriptor(SynopsysEthernetMAC parent, uint address, uint wordsCount = 4)
            {
                bus = parent.machine.SystemBus;
                context = parent;
                Size = wordsCount * sizeof(uint);
                Address = address;
                bytes = new byte[Size];
            }

            public void Fetch()
            {
                bus.ReadBytes(Address, bytes.Length, bytes, 0, true, context);
            }

            public virtual void Write()
            {
                bus.WriteBytes(bytes, Address, context: context);
            }

            public void SetDescriptor(object structure)
            {
                bytes = Packet.Encode(structure);
            }

            public INormalTxDescriptor GetNormalTxDescriptor(SynopsysEthernetVersion version)
            {
                return version switch
                {
                    SynopsysEthernetVersion.STM32F => GetDescriptor<STM32FNormalTxDescriptor>(),
                    SynopsysEthernetVersion.BeagleV => GetDescriptor<BeagleVNormalTxDescriptor>(),
                    _ => throw new NotImplementedException($"Normal Tx descriptor for {version} is not implemented"),
                };
            }

            public INormalRxDescriptor GetNormalRxDescriptor(SynopsysEthernetVersion version)
            {
                return version switch
                {
                    SynopsysEthernetVersion.STM32F => GetDescriptor<STM32FNormalRxDescriptor>(),
                    SynopsysEthernetVersion.BeagleV => GetDescriptor<BeagleVNormalRxDescriptor>(),
                    _ => throw new NotImplementedException($"Normal Rx descriptor for {version} is not implemented"),
                };
            }

            public long Size { get; private set; }

            public uint Address { get; private set; }

            protected byte[] bytes;
            protected readonly IPeripheral context;
            protected readonly IBusController bus;

            private T GetDescriptor<T>()
            {
                return Packet.Decode<T>(bytes);
            }
        }

        private enum TxStates
        {
            Stopped = 0b000,
            FetchingDescriptor = 0b001,
            WaitingForStatus = 0b010,
            ReadingData = 0b011,
            // Reserved: 100, 101
            Suspended = 0b110,
            ClosingDescriptor = 0b111,
        }

        private enum RxStates
        {
            Stopped = 0b000,
            FetchingDescriptor = 0b001,
            // Reserved: 010
            WaitingForPacket = 0b011,
            Suspended = 0b100,
            ClosingDescriptor = 0b101,
            // Reserved: 110
            TransferringData = 0b111,
        }

        private interface INormalRxDescriptor : ICommonDescriptor
        {
            public bool FirstDescriptor { get; set; }

            public bool LastDescriptor { get; set; }

            public uint FrameLength { get; set; }

            public bool DisableInterruptOnCompletion { get; set; }

            public bool ErrorSummary { get; set; }

            public bool DescriptorError { get; set; }
        }

        private interface INormalTxDescriptor : ICommonDescriptor
        {
            public bool FirstSegment { get; set; }

            public bool LastSegment { get; set; }

            public bool InterruptOnCompletion { get; set; }

            public bool DisableCRC { get; set; }

            public bool DisablePad { get; set; }

            public ChecksumInsertionMode ChecksumInsertionControl { get; set; }
        }

        private interface ICommonDescriptor
        {
            public bool Owned { get; set; }

            public bool EndOfRing { get; set; }

            public bool SecondAddressChained { get; set; }

            public int Buffer1Size { get; set; }

            public int Buffer2Size { get; set; }

            public uint Buffer1Address { get; set; }

            public uint Buffer2Address { get; set; }

            public uint NextDescriptorAddress => Buffer2Address;
        }
    }
}
