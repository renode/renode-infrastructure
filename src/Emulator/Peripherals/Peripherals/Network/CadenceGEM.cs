//
// Copyright (c) 2010-2025 Antmicro
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
using Antmicro.Renode.Peripherals.CPU;
using System.Collections.Generic;
using Antmicro.Renode.Network;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Network
{
    // The register and field names are taken from the UltraScale+ GEM docs,
    // because it is the most extensive description of this peripheral:
    // https://www.xilinx.com/html_docs/registers/ug1087/mod___gem.html
    // For further reference however, here are links to other sources:
    // https://www.xilinx.com/support/documentation/user_guides/ug585-Zynq-7000-TRM.pdf
    // https://www.mouser.com/datasheet/2/268/SAM-E70S70V70V71-Family_DataSheet-DS60001527B-1374834.pdf
    public class CadenceGEM : NetworkWithPHY, IDoubleWordPeripheral, IMACInterface, IKnownSize
    {
        // the default moduleRevision/moduleId are correct for Zynq with GEM p23
        public CadenceGEM(IMachine machine, ushort moduleRevision = 0x118, ushort moduleId = 0x2) : base(machine)
        {
            ModuleId = moduleId;
            ModuleRevision = moduleRevision;
            sysbus = machine.GetSystemBus(this);

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
                    .WithTag("loopback", 0, 1)
                    .WithTag("loopback_local", 1, 1)
                    .WithFlag(2, out receiveEnabled, name: "enable_receive")
                    .WithFlag(3, name: "enable_transmit",
                        writeCallback: (_, value) =>
                        {
                            if(!value)
                            {
                                isTransmissionStarted = false;
                            }
                            if(txDescriptorsQueue != null && !value)
                            {
                                txDescriptorsQueue.GoToBaseAddress();
                            }
                        })
                    .WithTag("man_port_en", 4, 1)
                    .WithTag("clear_all_stats_regs", 5, 1)
                    .WithTag("inc_all_stats_regs", 6, 1)
                    .WithTag("stats_write_en", 7, 1)
                    .WithTag("back_pressure", 8, 1)
                    .WithFlag(9, FieldMode.Read | FieldMode.WriteOneToClear, name: "tx_start_pclk",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                isTransmissionStarted = true;
                                SendFrames();
                            }
                        })
                    .WithFlag(10, FieldMode.Read | FieldMode.WriteOneToClear, name: "tx_halt_pclk",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                isTransmissionStarted = false;
                            }
                        })
                    .WithTag("tx_pause_frame_req", 11, 1)
                    .WithTag("tx_pause_frame_zero", 12, 1)
                    .WithReservedBits(13, 2)
                    .WithTag("store_rx_ts", 15, 1)
                    .WithTag("pfc_enable", 16, 1)
                    .WithTag("transmit_pfc_priority_based_pause_frame", 17, 1)
                    .WithTag("flush_rx_pkt_pclk", 18, 1)
                    .WithTag("tx_lpi_en", 19, 1)
                    .WithTag("ptp_unicast_ena", 20, 1)
                    .WithTag("alt_sgmii_mode", 21, 1)
                    .WithTag("store_udp_offset", 22, 1)
                    .WithTag("ext_tsu_port_enable", 23, 1)
                    .WithTag("one_step_sync_mode", 24, 1)
                    .WithReservedBits(25, 7)
                },

                {(long)Registers.NetworkConfiguration, new DoubleWordRegister(this, 0x80000)
                    .WithTag("speed", 0, 1)
                    .WithTag("full_duplex", 1, 1)
                    .WithTag("discard_non_vlan_frames", 2, 1)
                    .WithTag("jumbo_frames", 3, 1)
                    .WithTag("copy_all_frames", 4, 1)
                    .WithTag("no_broadcast", 5, 1)
                    .WithTag("multicast_hash_enable", 6, 1)
                    .WithTag("unicast_hash_enable", 7, 1)
                    .WithTag("receive_1536_byte_frames", 8, 1)
                    .WithTag("external_address_match_enable", 9, 1)
                    .WithTag("gigabit_mode_enable", 10, 1)
                    .WithTag("pcs_select", 11, 1)
                    .WithTag("retry_test", 12, 1)
                    .WithTag("pause_enable", 13, 1)
                    .WithValueField(14, 2, out receiveBufferOffset, name: "receive_buffer_offset")
                    .WithTag("length_field_error_frame_discard", 16, 1)
                    .WithFlag(17, out removeFrameChecksum, name: "fcs_remove")
                    .WithTag("mdc_clock_division", 18, 3)
                    .WithTag("data_bus_width", 21, 2)
                    .WithTag("disable_copy_of_pause_frames", 23, 1)
                    .WithTag("receive_checksum_offload_enable", 24, 1)
                    .WithTag("en_half_duplex_rx", 25, 1)
                    .WithFlag(26, out ignoreRxFCS, name: "ignore_rx_fcs")
                    .WithTag("sgmii_mode_enable", 27, 1)
                    .WithTag("ipg_stretch_enable", 28, 1)
                    .WithTag("nsp_change", 29, 1)
                    .WithTag("ignore_ipg_rx_er", 30, 1)
                    .WithTag("uni_direction_enable", 31, 1)
                },

                {(long)Registers.NetworkStatus, new DoubleWordRegister(this)
                    .WithTag("pcs_link_state", 0, 1)
                    .WithTag("mdio_in", 1, 1)
                    .WithFlag(2, FieldMode.Read, name: "man_done", valueProviderCallback: _ => true)
                    .WithTag("mac_full_duplex", 3, 1)
                    .WithTag("mac_pause_rx_en", 4, 1)
                    .WithTag("mac_pause_tx_en", 5, 1)
                    .WithTag("pfc_negotiate_pclk", 6, 1)
                    .WithTag("lpi_indicate_pclk", 7, 1)
                    .WithReservedBits(8, 24)
                },

                {(long)Registers.DmaConfiguration, new DoubleWordRegister(this, 0x00020784)
                    .WithTag("amba_burst_length", 0, 4)
                    .WithReservedBits(5, 1)
                    .WithTag("endian_swap_management", 6, 1)
                    .WithTag("endian_swap_packet", 7, 1)
                    .WithTag("rx_pbuf_size", 8, 2)
                    .WithTag("tx_pbuf_size", 10, 1)
                    .WithFlag(11, out checksumGeneratorEnabled, name: "tx_pbuf_tcp_en")
                    .WithReservedBits(12, 4)
                    .WithTag("rx_buf_size", 16, 8)
                    .WithTag("force_discard_on_err", 24, 1)
                    .WithTag("force_max_amba_burst_rx", 25, 1)
                    .WithTag("force_max_amba_burst_tx", 26, 1)
                    .WithReservedBits(27, 1)
                    .WithFlag(28, out extendedRxBufferDescriptorEnabled, name: "rx_bd_extended_mode_en")
                    .WithFlag(29, out extendedTxBufferDescriptorEnabled, name: "tx_bd_extended_mode_en")
                    .WithEnumField(30, 1, out dmaAddressBusWith, name: "dma_addr_bus_width_1")
                    .WithReservedBits(31, 1)
                },

                {(long)Registers.TransmitStatus, new DoubleWordRegister(this)
                    .WithFlag(0, out usedBitRead, FieldMode.Read | FieldMode.WriteOneToClear, name: "used_bit_read")
                    .WithTag("collision_occurred", 1, 1)
                    .WithTag("retry_limit_exceeded", 2, 1)
                    .WithTag("transmit_go", 3, 1)
                    .WithTag("amba_error", 4, 1)
                    .WithFlag(5, out transmitComplete, FieldMode.Read | FieldMode.WriteOneToClear, name: "transmit_complete")
                    .WithTag("transmit_under_run", 6, 1)
                    .WithTag("late_collision_occurred", 7, 1)
                    .WithTag("resp_not_ok", 8, 1)
                    .WithReservedBits(9, 23)
                },

                {(long)Registers.ReceiveBufferQueueBaseAddress, new DoubleWordRegister(this)
                    .WithReservedBits(0, 2)
                    .WithValueField(2, 30, name: "dma_rx_q_ptr",
                        valueProviderCallback: _ =>
                        {
                            return rxDescriptorsQueue.CurrentDescriptor.LowerDescriptorAddress;
                        },
                        writeCallback: (oldValue, value) =>
                        {
                            if(receiveEnabled.Value)
                            {
                                this.Log(LogLevel.Warning, "Changing value of receive buffer queue base address while reception is enabled is illegal");
                                return;
                            }
                            rxDescriptorsQueue = new DmaBufferDescriptorsQueue<DmaRxBufferDescriptor>(sysbus, GetCurrentContext(), (uint)value << 2, (sb, ctx, addr) => new DmaRxBufferDescriptor(sb, ctx, addr, dmaAddressBusWith.Value, extendedRxBufferDescriptorEnabled.Value));
                        })
                },

                {(long)Registers.ReceiveBufferQueueBaseAddressUpper, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "upper_rx_q_base_addr",
                        valueProviderCallback: _ =>
                        {
                            return rxDescriptorsQueue.CurrentDescriptor.UpperDescriptorAddress;
                        },
                        writeCallback: (oldValue, value) =>
                        {
                            rxDescriptorsQueue.CurrentDescriptor.UpperDescriptorAddress = (uint)value;
                        })
                },

                {(long)Registers.ReceiveBufferDescriptorControl, new DoubleWordRegister(this)
                    .WithReservedBits(0, 4)
                    .WithEnumField(4, 2, out rxBufferDescriptorTimeStampMode, name: "rx_bd_ts_mode")
                    .WithReservedBits(6, 26)
                },

                {(long)Registers.TransmitBufferQueueBaseAddress, new DoubleWordRegister(this)
                    .WithReservedBits(0, 2)
                    .WithValueField(2, 30, name: "dma_tx_q_ptr",
                        valueProviderCallback: _ =>
                        {
                            return txDescriptorsQueue.CurrentDescriptor.LowerDescriptorAddress;
                        },
                        writeCallback: (oldValue, value) =>
                        {
                            if(isTransmissionStarted)
                            {
                                this.Log(LogLevel.Warning, "Changing value of transmit buffer queue base address while transmission is started is illegal");
                                return;
                            }
                            txDescriptorsQueue = new DmaBufferDescriptorsQueue<DmaTxBufferDescriptor>(sysbus, GetCurrentContext(), (uint)value << 2, (sb, ctx, addr) => new DmaTxBufferDescriptor(sb, ctx, addr, dmaAddressBusWith.Value, extendedTxBufferDescriptorEnabled.Value));
                        })
                },

                {(long)Registers.TransmitBufferQueueBaseAddressUpper, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "upper_tx_q_base_addr",
                        valueProviderCallback: _ =>
                        {
                            return txDescriptorsQueue.CurrentDescriptor.UpperDescriptorAddress;
                        },
                        writeCallback: (oldValue, value) =>
                        {
                            txDescriptorsQueue.CurrentDescriptor.UpperDescriptorAddress = (uint)value;
                        })
                },

                {(long)Registers.TransmitBufferDescriptorControl, new DoubleWordRegister(this)
                    .WithReservedBits(0, 4)
                    .WithEnumField(4, 2, out txBufferDescriptorTimeStampMode, name: "tx_bd_ts_mode")
                    .WithReservedBits(6, 26)
                },

                {(long)Registers.ReceiveStatus, new DoubleWordRegister(this)
                    .WithFlag(0, out bufferNotAvailable, FieldMode.Read | FieldMode.WriteOneToClear, name: "buffer_not_available")
                    .WithFlag(1, out frameReceived, FieldMode.Read | FieldMode.WriteOneToClear, name: "frame_received")
                    .WithTag("receive_overrun", 2, 1)
                    .WithTag("resp_not_ok", 3, 1)
                    .WithReservedBits(4, 28)
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
                    .WithValueField(0, 31, name: "phy_management", writeCallback: (_, value) => HandlePhyWrite((uint)value), valueProviderCallback: _ => HandlePhyRead())
                },

                {(long)Registers.SpecificAddress1Bottom, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => BitConverter.ToUInt32(MAC.Bytes, 0))
                },

                {(long)Registers.SpecificAddress1Top, new DoubleWordRegister(this)
                    .WithValueField(0, 16, valueProviderCallback: _ => BitConverter.ToUInt16(MAC.Bytes, 4))
                    .WithTag("filter_type", 16, 1)
                    .WithReservedBits(17, 15)
                },

                {(long)Registers.ModuleId, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, name: "module_revision", valueProviderCallback: _ => ModuleRevision)
                    .WithValueField(16, 12, FieldMode.Read, name: "module_identification_number", valueProviderCallback: _ => ModuleId)
                    .WithTag("fix_number", 28, 4)
                },

                {(long)Registers.DesignConfiguration1, new DoubleWordRegister(this)
                    .WithTag("no_pcs", 0, 1)
                    .WithTag("serdes", 1, 1)
                    .WithTag("RDC_50", 2, 1)
                    .WithTag("TDC_50", 3, 1)
                    .WithTag("int_loopback", 4, 1)
                    .WithTag("no_int_loopback", 5, 1)
                    .WithTag("ext_fifo_interface", 6, 1)
                    .WithTag("apb_rev1", 7, 1)
                    .WithTag("apb_rev2", 8, 1)
                    .WithTag("user_io", 9, 1)
                    .WithTag("user_out_width", 10, 5)
                    .WithTag("user_in_width", 15, 5)
                    .WithTag("no_scan_pins", 20, 1)
                    .WithTag("no_stats", 21, 1)
                    .WithTag("no_snapshot", 22, 1)
                    .WithFlag(23, FieldMode.Read, name: "irq_read_clear", valueProviderCallback: _ => false)
                    .WithReservedBits(24, 1)
                    .WithValueField(25, 3, FieldMode.Read, name: "dma_bus_width", valueProviderCallback: _ => 1) // DMA data bus width - 32 bits
                    .WithTag("axi_cache_value", 28, 4)
                },

                {(long)Registers.DesignConfiguration2, new DoubleWordRegister(this)
                    .WithTag("jumbo_max_length", 0, 16)
                    .WithTag("hprot_value", 16, 4)
                    .WithFlag(20, FieldMode.Read, name: "rx_pkt_buffer", valueProviderCallback: _ => true) // includes the receiver packet buffer
                    .WithFlag(21, FieldMode.Read, name: "tx_pkt_buffer", valueProviderCallback: _ => true) // includes the transmitter packet buffer
                    .WithTag("rx_pbuf_addr", 22, 4)
                    .WithTag("tx_pbuf_addr", 26, 4)
                    .WithTag("axi", 30, 1)
                    .WithTag("spram", 31, 1)
                },

                {(long)Registers.DesignConfiguration6, new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithTaggedFlags("dma_priority_queue", 1, 15)
                    .WithTag("tx_pbuf_queue_segment_size", 16, 4)
                    .WithTag("ext_tsu_timer", 20, 1)
                    .WithTag("tx_add_fifo_if", 21, 1)
                    .WithTag("host_if_soft_select", 22, 1)
                    .WithFlag(23, FieldMode.Read, name: "dma_addr_width_is_64b", valueProviderCallback: _ => true)
                    .WithTag("pfc_multi_quantum", 24, 1)
                    .WithTag("pbuf_cutthru", 25, 1)
                    .WithReservedBits(26, 6)
                },

                {(long)Registers.Timer1588SecondsLow, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out secTimer, name: "tsu_timer_sec")
                },

                {(long)Registers.Timer1588SecondsHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 16, valueProviderCallback: _ => 0, writeCallback: (_, value) =>
                    {
                        if(value != 0)
                        {
                            this.Log(LogLevel.Warning, "Writing a non-zero value to the SecondsHigh register, timer values over 32 bits are not supported.");
                        }
                    }, name: "tsu_timer_msb_sec")
                    .WithReservedBits(16, 16)
                },

                {(long)Registers.Timer1588Nanoseconds, new DoubleWordRegister(this)
                    .WithValueField(0, 30, valueProviderCallback: _ =>
                    {
                        return (uint)nanoTimer.Value;
                    }, writeCallback: (_, value) =>
                    {
                        nanoTimer.Value = value;
                    }, name: "tsu_timer_nsec")
                    .WithReservedBits(30, 2)
                },

                {(long)Registers.Timer1588Adjust, new DoubleWordRegister(this)
                    .WithValueField(0, 30, out var timerIncrementDecrement, FieldMode.Write, name: "increment_value")
                    .WithReservedBits(30, 1)
                    .WithFlag(31, out var timerAdjust, FieldMode.Write, name: "add_subtract")
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
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => 0, name: "tsu_ptp_tx_msb_sec")
                    .WithReservedBits(16, 16)
                },

                {(long)Registers.PtpEventFrameTransmittedSeconds, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => txPacketTimestamp.seconds, name: "tsu_ptp_tx_sec")
                },

                {(long)Registers.PtpEventFrameTransmittedNanoseconds, new DoubleWordRegister(this)
                    .WithValueField(0, 30, FieldMode.Read, valueProviderCallback: _ => txPacketTimestamp.nanos, name: "tsu_ptp_tx_nsec")
                    .WithReservedBits(30, 2)
                },

                {(long)Registers.PtpEventFrameReceivedSecondsHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => 0, name: "tsu_ptp_rx_msb_sec")
                    .WithReservedBits(16, 16)
                },

                {(long)Registers.PtpEventFrameReceivedSeconds, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => rxPacketTimestamp.seconds, name: "tsu_ptp_rx_sec")
                },

                {(long)Registers.PtpEventFrameReceivedNanoseconds, new DoubleWordRegister(this)
                    .WithValueField(0, 30, FieldMode.Read, valueProviderCallback: _ => rxPacketTimestamp.nanos, name: "tsu_ptp_rx_nsec")
                    .WithReservedBits(30, 2)
                },

                {(long)Registers.PtpPeerEventFrameTransmittedSecondsHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => 0, name: "tsu_peer_tx_msb_sec")
                    .WithReservedBits(16, 16)
                },

                {(long)Registers.PtpPeerEventFrameTransmittedSeconds, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => txPacketTimestamp.seconds, name: "tsu_peer_tx_sec")
                },

                {(long)Registers.PtpPeerEventFrameTransmittedNanoseconds, new DoubleWordRegister(this)
                    .WithValueField(0, 30, FieldMode.Read, valueProviderCallback: _ => txPacketTimestamp.nanos, name: "tsu_peer_tx_nsec")
                    .WithReservedBits(30, 2)
                },

                {(long)Registers.PtpPeerEventFrameReceivedSecondsHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => 0, name: "tsu_peer_rx_msb_sec")
                    .WithReservedBits(16, 16)
                },

                {(long)Registers.PtpPeerEventFrameReceivedSeconds, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => rxPacketTimestamp.seconds, name: "tsu_peer_rx_sec")
                },

                {(long)Registers.PtpPeerEventFrameReceivedNanoseconds, new DoubleWordRegister(this)
                    .WithValueField(0, 30, FieldMode.Read, valueProviderCallback: _ => rxPacketTimestamp.nanos, name: "tsu_peer_rx_nsec")
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
            isTransmissionStarted = false;
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

                // the time obtained here is not single-instruction-precise (unless maximum block size is set to 1 and block chaining is disabled),
                // because timers are not updated instruction-by-instruction, but in batches when `TranslationCPU.ExecuteInstructions` finishes
                rxPacketTimestamp.seconds = (uint)secTimer.Value;
                rxPacketTimestamp.nanos = (uint)nanoTimer.Value;

                rxDescriptorsQueue.CurrentDescriptor.Invalidate();
                if(!rxDescriptorsQueue.CurrentDescriptor.Ownership)
                {
                    var actualLength = (uint)(removeFrameChecksum.Value ? frame.Bytes.Length - 4 : frame.Bytes.Length);
                    if(!rxDescriptorsQueue.CurrentDescriptor.WriteBuffer(frame.Bytes, actualLength, (uint)receiveBufferOffset.Value))
                    {
                        // The current implementation doesn't handle packets that do not fit into a single buffer.
                        // In case we encounter this error, we probably should implement partitioning/scattering procedure.
                        this.Log(LogLevel.Warning, "Could not write the incoming packet to the DMA buffer: maximum packet length exceeded.");
                        return;
                    }

                    rxDescriptorsQueue.CurrentDescriptor.StartOfFrame = true;
                    rxDescriptorsQueue.CurrentDescriptor.EndOfFrame = true;

                    if(rxBufferDescriptorTimeStampMode.Value != TimestampingMode.Disabled)
                    {
                        rxDescriptorsQueue.CurrentDescriptor.Timestamp = rxPacketTimestamp;
                    }
                    else
                    {
                        rxDescriptorsQueue.CurrentDescriptor.HasValidTimestamp = false;
                    }

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

            if(!TryGetPhy<ushort>(addr, out var phy))
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
            EnsureArrayLength(bytesArray, isCRCIncluded ? 64 : 60);

            EthernetFrame frame;
            var addCrc = !isCRCIncluded && checksumGeneratorEnabled.Value;
            if(!Misc.TryCreateFrameOrLogWarning(this, bytesArray, out frame, addCrc))
            {
                return;
            }
            if(addCrc)
            {
                frame.FillWithChecksums(new [] { EtherType.IpV4, EtherType.IpV6 },
                    new [] { IPProtocolType.TCP, IPProtocolType.UDP });
            }

            this.Log(LogLevel.Noisy, "Sending packet, length {0}", frame.Bytes.Length);
            FrameReady?.Invoke(frame);

            // the time obtained here is not single-instruction-precise (unless maximum block size is set to 1 and block chaining is disabled),
            // because timers are not updated instruction-by-instruction, but in batches when `TranslationCPU.ExecuteInstructions` finishes
            txPacketTimestamp.seconds = (uint)secTimer.Value;
            txPacketTimestamp.nanos = (uint)nanoTimer.Value;

            if(txBufferDescriptorTimeStampMode.Value != TimestampingMode.Disabled)
            {
                txDescriptorsQueue.CurrentDescriptor.Timestamp = txPacketTimestamp;
            }
            else
            {
                rxDescriptorsQueue.CurrentDescriptor.HasValidTimestamp = false;
            }
        }

        private void EnsureArrayLength(byte[] bytesArray, int length)
        {
            if(bytesArray.Length < length)
            {
                Array.Resize(ref bytesArray, length);
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

        private ICPU GetCurrentContext()
        {
            if(!sysbus.TryGetCurrentCPU(out var cpu))
            {
                this.Log(LogLevel.Debug, "Failed to retrieve context, only global peripherals will be accessible by DMA");
                return null;
            }
            return cpu;
        }

        private uint phyDataRead;
        private bool isTransmissionStarted;
        private DmaBufferDescriptorsQueue<DmaTxBufferDescriptor> txDescriptorsQueue;
        private DmaBufferDescriptorsQueue<DmaRxBufferDescriptor> rxDescriptorsQueue;

        private readonly IFlagRegisterField checksumGeneratorEnabled;
        private readonly IFlagRegisterField extendedRxBufferDescriptorEnabled;
        private readonly IFlagRegisterField extendedTxBufferDescriptorEnabled;
        private readonly IEnumRegisterField<DMAAddressWidth> dmaAddressBusWith;
        private readonly IFlagRegisterField transmitComplete;
        private readonly IFlagRegisterField usedBitRead;
        private readonly IFlagRegisterField receiveEnabled;
        private readonly IFlagRegisterField ignoreRxFCS;
        private readonly IFlagRegisterField bufferNotAvailable;
        private readonly IFlagRegisterField frameReceived;
        private readonly IFlagRegisterField removeFrameChecksum;
        private readonly IValueRegisterField receiveBufferOffset;
        private readonly IEnumRegisterField<TimestampingMode> rxBufferDescriptorTimeStampMode;
        private readonly IEnumRegisterField<TimestampingMode> txBufferDescriptorTimeStampMode;
        private readonly IValueRegisterField secTimer;

        private readonly IBusController sysbus;
        private readonly InterruptManager<Interrupts> interruptManager;
        private readonly DoubleWordRegisterCollection registers;
        private readonly object sync;

        private readonly LimitTimer nanoTimer;

        private PTPTimestamp txPacketTimestamp;
        private PTPTimestamp rxPacketTimestamp;

        private class DmaBufferDescriptorsQueue<T> where T : DmaBufferDescriptor
        {
            public DmaBufferDescriptorsQueue(IBusController bus, ICPU context, ulong baseAddress, Func<IBusController, ICPU, ulong, T> creator)
            {
                this.bus = bus;
                this.context = context;
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
                    descriptors.Add(creator(bus, context, baseAddress));
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
                            descriptors.Add(creator(bus, context, CurrentDescriptor.GetDescriptorAddress() + CurrentDescriptor.SizeInBytes));
                        }
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
            private readonly ulong baseAddress;
            private readonly IBusController bus;
            private readonly ICPU context;
            private readonly Func<IBusController, ICPU, ulong, T> creator;
        }

        private abstract class DmaBufferDescriptor
        {
            protected DmaBufferDescriptor(IBusController bus, ICPU context, ulong address, DMAAddressWidth dmaAddressWidth, bool isExtendedModeEnabled)
            {
                this.dmaAddressWidth = dmaAddressWidth;
                Bus = bus;
                Context = context;
                LowerDescriptorAddress = (uint)address;
                UpperDescriptorAddress = (uint)(address >> 32);
                IsExtendedModeEnabled = isExtendedModeEnabled;
                SizeInBytes = InitWords();
            }

            public void Invalidate()
            {
                var tempOffset = 0UL;
                for(var i = 0; i < words.Length; ++i)
                {
                    words[i] = Bus.ReadDoubleWord(GetDescriptorAddress() + tempOffset, Context);
                    tempOffset += 4;
                }
            }

            public void Update()
            {
                var tempOffset = 0UL;
                foreach(var word in words)
                {
                    Bus.WriteDoubleWord(GetDescriptorAddress() + tempOffset, word, Context);
                    tempOffset += 4;
                }
            }

            public ulong GetDescriptorAddress()
            {
                return dmaAddressWidth == DMAAddressWidth.Bit64 ? (((ulong)UpperDescriptorAddress << 32) | LowerDescriptorAddress) : LowerDescriptorAddress;
            }

            public ulong GetBufferAddress()
            {
                return dmaAddressWidth == DMAAddressWidth.Bit64 ? (((ulong)UpperBufferAddress << 32) | LowerBufferAddress) : LowerBufferAddress;
            }

            public IBusController Bus { get; }
            // The descriptor and its buffers are bound to the CPU that configured the descriptor into the
            // peripheral, this means they can access memory that is local to the CPU.
            // If the descriptor was not configured by a CPU, this field is null and only global peripherals
            // can be accessed.
            public ICPU Context { get; }
            public uint SizeInBytes { get; }
            public bool IsExtendedModeEnabled { get; }
            public uint LowerDescriptorAddress { get; set; }
            public uint UpperDescriptorAddress { get; set; }

            public uint UpperBufferAddress
            {
                get { return BitHelper.GetMaskedValue(words[2], 0, 32); }
                set { BitHelper.SetMaskedValue(ref words[2], value, 0, 32); }
            }

            public PTPTimestamp Timestamp
            {
                get
                {
                    var ptpTimestamp = new PTPTimestamp();
                    if(!IsExtendedModeEnabled)
                    {
                        return ptpTimestamp;
                    }
                    ptpTimestamp.nanos = BitHelper.GetMaskedValue(words[4], 0, 30);
                    ptpTimestamp.seconds = (BitHelper.GetMaskedValue(words[4], 30, 2) << 4) | BitHelper.GetMaskedValue(words[5], 0, 4);
                    return ptpTimestamp;
                }
                set
                {
                    if(IsExtendedModeEnabled)
                    {
                        BitHelper.SetMaskedValue(ref words[4], value.nanos, 0, 30);
                        BitHelper.SetMaskedValue(ref words[4], value.seconds >> 4, 30, 2);
                        BitHelper.SetMaskedValue(ref words[5], value.seconds & 0xF, 0, 4);
                        HasValidTimestamp = true;
                    }
                }
            }

            public abstract bool Wrap { get; }
            public abstract bool HasValidTimestamp { set; }
            public abstract uint LowerBufferAddress { get; set; }

            protected uint[] words;

            private uint InitWords()
            {
                if(dmaAddressWidth == DMAAddressWidth.Bit64 && IsExtendedModeEnabled)
                {
                    words = new uint[6];
                }
                else if((dmaAddressWidth == DMAAddressWidth.Bit32 && IsExtendedModeEnabled) || (dmaAddressWidth == DMAAddressWidth.Bit64 && !IsExtendedModeEnabled))
                {
                    words = new uint[4];
                }
                else if(dmaAddressWidth == DMAAddressWidth.Bit32 && !IsExtendedModeEnabled)
                {
                    words = new uint[2];
                }
                return (uint)words.Length * 4;
            }

            private readonly DMAAddressWidth dmaAddressWidth;
        }

        /// RX buffer descriptor format:
        /// * word 0:
        ///     * 0: Ownership flag
        ///     * 1: Wrap flag
        ///     * 2: Address of beginning of buffer, or in extended buffer descriptor
        ///          mode indicates a valid timestamp in the descriptor entry
        ///     * In basic buffer descriptor mode:
        ///     * 2-31: Address of the buffer [31:2]
        ///     * In extended buffer descriptor mode:
        ///     * 2: Valid timestamp flag
        ///     * 3-31: Address of the buffer [31:3]
        /// * word 1:
        ///     * 0-12: Length of the received frame
        ///     * 13: Bad FCS flag
        ///     * 14: Start of frame flag
        ///     * 15: End of frame flag
        ///     * 16: Cannonical form indicator flag
        ///     * 17-19: VLAN priority
        ///     * 20: Priority tag detected flag
        ///     * 21: VLAN tag detected flag
        ///     * 22-23: Type ID match
        ///     * 24: Type ID match meaning flag
        ///     * 25-26: Specific address register match
        ///     * 27: Specific address found flag
        ///     * 28: External address match flag
        ///     * 29: Unicast hash match flag
        ///     * 30: Multicast hash match flag
        ///     * 31: Broadcast address detected flag
        /// * word 2 (64-bit addressing):
        ///     * 0-31: Upper 32-bit address of the data buffer
        /// * word 3 (64-bit addressing):
        ///     * 0-31: Reserved
        /// * word 2 (32-bit addressing) or word 4 (64-bit addressing):
        ///     * 0-29: Timestamp nanosecods [29:0]
        ///     * 30-31: Timestamp seconds [1:0]
        /// * word 3 (32-bit addressing) or word 5 (64-bit addressing):
        ///     * 0-3: Timestamp seconds [5:2]
        ///     * 4-31: Reserved
        private class DmaRxBufferDescriptor : DmaBufferDescriptor
        {
            public DmaRxBufferDescriptor(IBusController bus, ICPU context, ulong address, DMAAddressWidth addressWidth, bool extendedModeEnabled) : base(bus, context, address, addressWidth, extendedModeEnabled)
            {
            }

            public bool WriteBuffer(byte[] bytes, uint length, uint offset = 0)
            {
                if(Ownership || bytes.Length > MaximumBufferLength)
                {
                    return false;
                }

                Length = length;
                Bus.WriteBytes(bytes, GetBufferAddress() + offset, true, Context);
                Ownership = true;

                return true;
            }

            public override uint LowerBufferAddress
            {
                get
                {
                    if(IsExtendedModeEnabled)
                    {
                        return BitHelper.GetMaskedValue(words[0], 3, 29);
                    }
                    return BitHelper.GetMaskedValue(words[0], 2, 30);
                }
                set
                {
                    if(IsExtendedModeEnabled)
                    {
                        BitHelper.SetMaskedValue(ref words[0], value, 3, 29);
                    }
                    else
                    {
                        BitHelper.SetMaskedValue(ref words[0], value, 2, 30);
                    }
                }
            }

            public override bool Wrap => BitHelper.IsBitSet(words[0], 1);

            public override bool HasValidTimestamp
            {
                set
                {
                    if(IsExtendedModeEnabled)
                    {
                        BitHelper.SetBit(ref words[0], 2, value);
                    }
                }
            }

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
        /// * word 0:
        ///     * 0-31: Address of the buffer [31:0]
        /// * word 1:
        ///     * 0-13: Lenght of the buffer
        ///     * 14: Reserved
        ///     * 15: Last buffer flag
        ///     * 16: CRC appended flag
        ///     * 17-19: Reserved
        ///     * 20-22: Transmit checksum errors
        ///   * In basic buffer descriptor mode:
        ///     * 23: Reserved
        ///   * In extended buffer descriptor mode:
        ///     * 23: Timestamp captured flag
        ///     * 24-25: Reserved
        ///     * 26: Late collision detected flag
        ///     * 27: Transmit frame corruption flag
        ///     * 28: Reserved
        ///     * 29: Retry limit exceeded
        ///     * 30: Wrap flag
        ///     * 31: Used flag
        /// * word 2 (64-bit addressing):
        ///     * 0-31: Address of the buffer [63:32]
        /// * word 3 (64-bit addressing):
        ///     * 0-31: Reserved
        /// * word 2 (32-bit addressing) or word 4 (64-bit addressing):
        ///     * 0-29: Timestamp nanosecods [29:0]
        ///     * 30-31: Timestamp seconds [1:0]
        /// * word 3 (32-bit addressing) or word 5 (64-bit addressing):
        ///     * 0-3: Timestamp seconds [5:2]
        ///     * 4-31: Reserved
        private class DmaTxBufferDescriptor : DmaBufferDescriptor
        {
            public DmaTxBufferDescriptor(IBusController bus, ICPU context, ulong address, DMAAddressWidth addressWidth, bool extendedModeEnabled) : base(bus, context, address, addressWidth, extendedModeEnabled)
            {
            }

            public byte[] ReadBuffer()
            {
                var result = Bus.ReadBytes(GetBufferAddress(), Length, true, Context);
                IsUsed = true;
                return result;
            }

            public ushort Length => (ushort)BitHelper.GetMaskedValue(words[1], 0, 14);

            public bool IsLast => BitHelper.IsBitSet(words[1], 15);

            public bool IsCRCIncluded => BitHelper.IsBitSet(words[1], 16);

            public bool IsUsed
            {
                get { return BitHelper.IsBitSet(words[1], 31); }
                set { BitHelper.SetBit(ref words[1], 31, value); }
            }

            public override bool HasValidTimestamp
            {
                set
                {
                    if(IsExtendedModeEnabled)
                    {
                        BitHelper.SetBit(ref words[1], 23, value);
                    }
                }
            }

            public override uint LowerBufferAddress
            {
                get
                {
                    return words[0];
                }
                set
                {
                    words[0] = value;
                }
            }

            public override bool Wrap => BitHelper.IsBitSet(words[1], 30);
        }

        private const uint NanosPerSecond = 1000000000;

        private struct PTPTimestamp
        {
            public uint seconds;
            public uint nanos;
        }

        private enum TimestampingMode
        {
            Disabled = 0,
            PTPEventFrames = 1,
            PTPAllFrames = 2,
            AllFrames = 3
        }

        private enum DMAAddressWidth
        {
            Bit32 = 0,
            Bit64 = 1,
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
            DesignConfiguration6 = 0x294,
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
            TransmitBufferQueueBaseAddressUpper = 0x4C8,
            TransmitBufferDescriptorControl = 0x4CC,
            ReceiveBufferDescriptorControl = 0x4D0,
            ReceiveBufferQueueBaseAddressUpper = 0x4D4,
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
