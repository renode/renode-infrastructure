//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Network
{
    public class S32K3XX_GMAC : SynopsysDWCEthernetQualityOfService
    {
        public S32K3XX_GMAC(IMachine machine, long systemClockFrequency, ICPU cpuContext = null)
            : base(machine, systemClockFrequency, cpuContext)
        {
            Reset();
        }

        public override uint ReadDoubleWord(long offset)
        {
            if(offset < (long)Registers.MTLOperationMode)
            {
                return base.ReadDoubleWord(offset);
            }
            else if(offset < (long)Registers.DMAMode)
            {
                return ReadDoubleWordFromMTL(offset - (long)Registers.MTLOperationMode);
            }
            return ReadDoubleWordFromDMA(offset - (long)Registers.DMAMode);
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(offset < (long)Registers.MTLOperationMode)
            {
                base.WriteDoubleWord(offset, value);
            }
            else if(offset < (long)Registers.DMAMode)
            {
                WriteDoubleWordToMTL(offset - (long)Registers.MTLOperationMode, value);
            }
            else
            {
                WriteDoubleWordToDMA(offset - (long)Registers.DMAMode, value);
            }
        }

        public override long Size => 0x1300;

        public GPIO Channel0TX => dmaChannels[0].TxIRQ;
        public GPIO Channel0RX => dmaChannels[0].RxIRQ;
        public GPIO Channel1TX => dmaChannels[1].TxIRQ;
        public GPIO Channel1RX => dmaChannels[1].RxIRQ;
        public GPIO Channel2TX => dmaChannels[2].TxIRQ;
        public GPIO Channel2RX => dmaChannels[2].RxIRQ;

        // Base model configuration:
        protected override long[] DMAChannelOffsets => new long[]
        {
            (long)Registers.DMAChannel0Control - (long)Registers.DMAMode,
            (long)Registers.DMAChannel1Control - (long)Registers.DMAMode,
            (long)Registers.DMAChannel2Control - (long)Registers.DMAMode,
        };
        protected override BusWidth DMABusWidth => BusWidth.Bits64;
        protected override int RxQueueSize => 16384;
        protected override bool SeparateDMAInterrupts => true;

        private enum Registers
        {
            Configuration = 0x0, // MAC_Configuration
            ExtendedConfiguration = 0x4, // MAC_Ext_Configuration
            PacketFilter = 0x8, // MAC_Packet_Filter
            WatchdogTimeout = 0xC, // MAC_Watchdog_Timeout
            HashTable0 = 0x10, // MAC_Hash_Table_Reg0
            HashTable1 = 0x14, // MAC_Hash_Table_Reg1
            HashTable2 = 0x18, // MAC_Hash_Table_Reg2
            HashTable3 = 0x1C, // MAC_Hash_Table_Reg3
            HashTable4 = 0x20, // MAC_Hash_Table_Reg4
            HashTable5 = 0x24, // MAC_Hash_Table_Reg5
            HashTable6 = 0x28, // MAC_Hash_Table_Reg6
            HashTable7 = 0x2C, // MAC_Hash_Table_Reg7

            VLANTagControl = 0x50, // MAC_VLAN_Tag_Ctrl
            VLANTagData = 0x54, // MAC_VLAN_Tag_Data
            VLANTagFilter0 = 0x54, // MAC_VLAN_Tag_Filter0
            VLANTagFilter1 = 0x54, // MAC_VLAN_Tag_Filter1
            VLANTagFilter2 = 0x54, // MAC_VLAN_Tag_Filter2
            VLANTagFilter3 = 0x54, // MAC_VLAN_Tag_Filter3
            VLANTagFilter4 = 0x54, // MAC_VLAN_Tag_Filter4
            VLANTagFilter5 = 0x54, // MAC_VLAN_Tag_Filter5
            VLANTagFilter6 = 0x54, // MAC_VLAN_Tag_Filter6
            VLANTagFilter7 = 0x54, // MAC_VLAN_Tag_Filter7
            VLANTagFilter8 = 0x54, // MAC_VLAN_Tag_Filter8
            VLANTagFilter9 = 0x54, // MAC_VLAN_Tag_Filter9
            VLANTagFilter10 = 0x54, // MAC_VLAN_Tag_Filter10
            VLANTagFilter11 = 0x54, // MAC_VLAN_Tag_Filter11
            VLANTagFilter12 = 0x54, // MAC_VLAN_Tag_Filter12
            VLANTagFilter13 = 0x54, // MAC_VLAN_Tag_Filter13
            VLANTagFilter14 = 0x54, // MAC_VLAN_Tag_Filter14
            VLANTagFilter15 = 0x54, // MAC_VLAN_Tag_Filter15
            VLANTagFilter16 = 0x54, // MAC_VLAN_Tag_Filter16
            VLANTagFilter17 = 0x54, // MAC_VLAN_Tag_Filter17
            VLANTagFilter18 = 0x54, // MAC_VLAN_Tag_Filter18
            VLANTagFilter19 = 0x54, // MAC_VLAN_Tag_Filter19
            VLANTagFilter20 = 0x54, // MAC_VLAN_Tag_Filter20
            VLANTagFilter21 = 0x54, // MAC_VLAN_Tag_Filter21
            VLANTagFilter22 = 0x54, // MAC_VLAN_Tag_Filter22
            VLANTagFilter23 = 0x54, // MAC_VLAN_Tag_Filter23
            VLANTagFilter24 = 0x54, // MAC_VLAN_Tag_Filter24
            VLANTagFilter25 = 0x54, // MAC_VLAN_Tag_Filter25
            VLANTagFilter26 = 0x54, // MAC_VLAN_Tag_Filter26
            VLANTagFilter27 = 0x54, // MAC_VLAN_Tag_Filter27
            VLANTagFilter28 = 0x54, // MAC_VLAN_Tag_Filter28
            VLANTagFilter29 = 0x54, // MAC_VLAN_Tag_Filter29
            VLANTagFilter30 = 0x54, // MAC_VLAN_Tag_Filter30
            VLANTagFilter31 = 0x54, // MAC_VLAN_Tag_Filter31

            VLANHashTable = 0x58, // MAC_VLAN_Hash_Table

            VLANInclusion = 0x60, // MAC_VLAN_Incl
            VLANInclusion0 = 0x60, // MAC_VLAN_Incl0
            VLANInclusion1 = 0x60, // MAC_VLAN_Incl1
            VLANInclusion2 = 0x60, // MAC_VLAN_Incl2
            VLANInclusion3 = 0x60, // MAC_VLAN_Incl3
            VLANInclusion4 = 0x60, // MAC_VLAN_Incl4
            VLANInclusion5 = 0x60, // MAC_VLAN_Incl5
            VLANInclusion6 = 0x60, // MAC_VLAN_Incl6
            VLANInclusion7 = 0x60, // MAC_VLAN_Incl7
            VLANInclusion8 = 0x60, // MAC_VLAN_Incl8
            InnerVLANInclusion = 0x64, // MAC_Inner_VLAN_Incl

            Queue0TransmitFlowControl = 0x70, // MAC_Q0_Tx_Flow_Ctrl
            Queue1TransmitFlowControl = 0x74, // MAC_Q1_Tx_Flow_Ctrl
            Queue2TransmitFlowControl = 0x78, // MAC_Q2_Tx_Flow_Ctrl

            ReceiveFlowControl = 0x90, // MAC_Rx_Flow_Ctrl
            ReceiveQueueControl4 = 0x94, // MAC_RxQ_Ctrl4
            TransmitQueuePriorityMapping0 = 0x98, // MAC_TxQ_Prty_Map0
            ReceiveQueueControl0 = 0xA0, // MAC_RxQ_Ctrl0
            ReceiveQueueControl1 = 0xA4, // MAC_RxQ_Ctrl1
            ReceiveQueueControl2 = 0xA8, // MAC_RxQ_Ctrl2
            InterruptStatus = 0xB0, // MAC_Interrupt_Status
            InterruptEnable = 0xB4, // MAC_Interrupt_Enable
            ReceiveTransmitStatus = 0xB8, // MAC_Rx_Tx_Status
            PHYInterfaceControlAndStatus = 0xF8, // MAC_PHYIF_Control_Status
            Version = 0x110, // MAC_Version
            Debug = 0x114, // MAC_Debug
            HardwareFeature0 = 0x11C, // MAC_HW_Feature0
            HardwareFeature1 = 0x120, // MAC_HW_Feature1
            HardwareFeature2 = 0x124, // MAC_HW_Feature2
            HardwareFeature3 = 0x128, // MAC_HW_Feature3

            FSMErrorsInterruptStatus = 0x140, // MAC_DPP_FSM_Interrupt_Status
            FSMControl = 0x148, // MAC_FSM_Control
            FSMTimeouts = 0x14C, // MAC_FSM_ACT_Timer

            MDIOAddress = 0x200, // MAC_MDIO_Address
            ARPAddress = 0x210, // MAC_ARP_Address
            CSRSoftwareControl = 0x230, // MAC_CSR_SW_Ctrl
            FramePreeptionControl = 0x234, // MAC_FPE_CTRL_STS
            ExtendedConfiguration1 = 0x238, // MAC_Ext_Cfg1

            PresentationTimeNanoseconds = 0x240, // MAC_Presn_Time_ns
            PresentationTimeUpdate = 0x244, // MAC_Presn_Time_Updt

            Address0High = 0x300, // MAC_Address0_High
            Address0Low = 0x304, // MAC_Address0_Low
            Address1High = 0x308, // MAC_Address1_High
            Address1Low = 0x30C, // MAC_Address1_Low
            Address2High = 0x310, // MAC_Address2_High
            Address2Low = 0x314, // MAC_Address2_Low

            MMCControl = 0x700, // MMC_Control
            MMCReceiveInterrupt = 0x704, // MMC_Rx_Interrupt
            MMCTransmitInterrupt = 0x708, // MMC_Tx_Interrupt
            MMCReceiveInterruptMask = 0x70C, // MMC_Rx_Interrupt_Mask
            MMCTransmitInterruptMask = 0x710, // MMC_Tx_Interrupt_Mask

            TransmitOctetCount = 0x714, // Tx_Octet_Count_Good_Bad
            TransmitPacketCount = 0x718, // Tx_Packet_Count_Good_Bad
            TransmitBroadcastPacketGoodCount = 0x71C, // Tx_Broadcast_Packets_Good
            TransmitMulticastPacketGoodCount = 0x720, // Tx_Multicast_Packets_Good
            Transmit64OctetPacketCount = 0x724, // Tx_64Octets_Packets_Good_Bad
            Transmit64To127OctetPacketCount = 0x728, // Tx_65To127Octets_Packets_Good_Bad
            Transmit128To255OctetPacketCount = 0x72C, // Tx_128To255Octets_Packets_Good_Bad
            Transmit256To511OctetPacketCount = 0x730, // Tx_256To511Octets_Packets_Good_Bad
            Transmit512To1023OctetPacketCount = 0x734, // Tx_512To1023Octets_Packets_Good_Bad
            Transmit1024ToMaxOctetPacketCount = 0x738, // Tx_1024ToMaxOctets_Packets_Good_Bad
            TransmitUnicastPacketCount = 0x73C, // Tx_Unicast_Packets_Good_Bad
            TransmitMulticastPacketCount = 0x740, // Tx_Multicast_Packets_Good_Bad
            TransmitBroadcastPacketCount = 0x744, // Tx_Broadcast_Packets_Good_Bad
            TransmitUnderflowErrorPacketCount = 0x748, // Tx_Underflow_Error_Packets
            TransmitSingleCollisionPacketGoodCount = 0x74C, // Tx_Single_Collision_Good_Packets
            TransmitMultipleCollisionPacketGoodCount = 0x750, // Tx_Multiple_Collision_Good_Packets
            TransmitDeferredPacketCount = 0x754, // Tx_Deferred_Packets
            TransmitLateCollisionPacketCount = 0x758, // Tx_Late_Collision_Packets
            TransmitExcessiveCollisionPacketCount = 0x75C, // Tx_Late_Collision_Packets
            TransmitCarrierErrorPacketCount = 0x760, // Tx_Carrier_Error_Packets
            TransmitOctetGoodCount = 0x764, // Tx_Octet_Count_Good
            TransmitPacketGoodCount = 0x768, // Tx_Packet_Count_Good
            TransmitExcessiveDeferralErrorPacketCount = 0x76C, // Tx_Excessive_Deferral_Error
            TransmitPausePacketCount = 0x770, // Tx_Pause_Packets
            TransmitVLANPacketGoodCount = 0x774, // Tx_VLAN_Packets_Good
            TransmitGreaterThanMaxSizePacketGoodCount = 0x778, // Tx_OSize_Packets_Good

            ReceivePacketCount = 0x780, // Rx_Packets_Count_Good_Bad
            ReceiveOctetCount = 0x784, // Rx_Octet_Count_Good_Bad
            ReceiveOctetGoodCount = 0x788, // Rx_Octet_Count_Good
            ReceiveBroadcastPacketGoodCount = 0x78C, // Rx_Broadcast_Packets_Good
            ReceiveMulticastPacketGoodCount = 0x790, // Rx_Multicast_Packets_Good
            ReceiveCRCErrorPacketCount = 0x794, // Rx_CRC_Error_Packets
            ReceiveAlignmentErrorPacketCount = 0x798, // Rx_Alignment_Error_Packets
            ReceiveRuntErrorPacketCount = 0x79C, // Rx_Runt_Error_Packets
            ReceiveJabberErrorPacketCount = 0x7A0, // Rx_Jabber_Error_Packets
            ReceiveUndersizeErrorPacketCount = 0x7A4, // Rx_Undersize_Packets_Good
            ReceiveOversizeErrorPacketCount = 0x7A8, // Rx_Oversize_Packets_Good
            Receive64To127OctetPacketCount = 0x7B0, // Rx_65To127Octets_Packets_Good_Bad
            Receive128To255OctetPacketCount = 0x7B4, // Rx_128To255Octets_Packets_Good_Bad
            Receive256To511OctetPacketCount = 0x7B8, // Rx_256To511Octets_Packets_Good_Bad
            Receive512To1023OctetPacketCount = 0x7BC, // Rx_512To1023Octets_Packets_Good_Bad
            Receive1024ToMaxOctetPacketCount = 0x7C0, // Rx_1024ToMaxOctets_Packets_Good_Bad
            ReceiveUnicastPacketGoodCount = 0x7C4, // Rx_Unicast_Packets_Good
            ReceiveLengthErrorPacketCount = 0x7C8, // Rx_Length_Error_Packets
            ReceiveOutOfRangeTypePacketCount = 0x7CC, // Rx_Out_Of_Range_Type_Packets
            ReceivePausePacketCount = 0x7D0, // Rx_Pause_Packets
            ReceiveFIFOOverflowPacketCount = 0x7D4, // Rx_FIFO_Overflow_Packets
            ReceiveVLANPacketCount = 0x7D8, // Rx_VLAN_Packets_Good_Bad
            ReceiveWatchdogErrorPacketCount = 0x7DC, // Rx_Watchdog_Error_Packets
            ReceiveReceiveErrorPacketCount = 0x7E0, // Rx_Receive_Error_Packets
            ReceiveControlPacketGoodCount = 0x7E4, // Rx_Control_Packets_Good

            MMCFramePreemptionTransmitInterrupt = 0x8A0, // MMC_FPE_Tx_Interrupt
            MMCFramePreemptionTransmitInterruptMask = 0x8A4, // MMC_FPE_Tx_Interrupt_Mask
            MMCFramePreemptionTransmitFragmentCount = 0x8A8, // MMC_Tx_FPE_Fragment_Cntr
            MMCTransmitHoldRequestCount = 0x8AC, // MMC_Tx_Hold_Req_Cntr
            MMCFramePreemptionReceiveInterrupt = 0x8C0, // MMC_FPE_Rx_Interrupt
            MMCFramePreemptionReceiveInterruptMask = 0x8C4, // MMC_FPE_Rx_Interrupt_Mask
            MMCReceivePacketAssemblyErrorCount = 0x8C8, // MMC_Rx_Packet_Assembly_Err_Cntr
            MMCReceivePacketSMDErrorCount = 0x8CC, // MMC_Rx_Packet_SMD_Err_Cntr
            MMCReceivePacketAssemblyOkCount = 0x8D0, // MMC_Rx_Packet_Assembly_OK_Cntr
            MMCFramePreemptionReceiveFragmentCount = 0x8D4, // MMC_Rx_FPE_Fragment_Cntr

            Layer3Layer4Control0 = 0x900, // MAC_L3_L4_Control0
            Layer4Address0 = 0x904, // MAC_Layer4_Address0
            Layer3Address0Register0 = 0x910, // MAC_Layer3_Addr0_Reg0
            Layer3Address1Register0 = 0x914, // MAC_Layer3_Addr1_Reg0
            Layer3Address2Register0 = 0x918, // MAC_Layer3_Addr2_Reg0
            Layer3Address3Register0 = 0x91C, // MAC_Layer3_Addr3_Reg0

            Layer3Layer4Control1 = 0x930, // MAC_L3_L4_Control1
            Layer4Address1 = 0x934, // MAC_Layer4_Address1
            Layer3Address0Register1 = 0x940, // MAC_Layer3_Addr0_Reg1
            Layer3Address1Register1 = 0x944, // MAC_Layer3_Addr1_Reg1
            Layer3Address2Register1 = 0x948, // MAC_Layer3_Addr2_Reg1
            Layer3Address3Register1 = 0x94C, // MAC_Layer3_Addr3_Reg1

            Layer3Layer4Control2 = 0x960, // MAC_L3_L4_Control2
            Layer4Address2 = 0x964, // MAC_Layer4_Address2
            Layer3Address0Register2 = 0x970, // MAC_Layer3_Addr0_Reg2
            Layer3Address1Register2 = 0x974, // MAC_Layer3_Addr1_Reg2
            Layer3Address2Register2 = 0x978, // MAC_Layer3_Addr2_Reg2
            Layer3Address3Register2 = 0x97C, // MAC_Layer3_Addr3_Reg2

            Layer3Layer4Control3 = 0x990, // MAC_L3_L4_Control3
            Layer4Address3 = 0x994, // MAC_Layer4_Address3
            Layer3Address0Register3 = 0x9A0, // MAC_Layer3_Addr0_Reg3
            Layer3Address1Register3 = 0x9A4, // MAC_Layer3_Addr1_Reg3
            Layer3Address2Register3 = 0x9A8, // MAC_Layer3_Addr2_Reg3
            Layer3Address3Register3 = 0x9AC, // MAC_Layer3_Addr3_Reg3

            Layer3Layer4Control4 = 0x9C0, // MAC_L3_L4_Control4
            Layer4Address4 = 0x9C4, // MAC_Layer4_Address4
            Layer3Address0Register4 = 0x9D0, // MAC_Layer3_Addr0_Reg4
            Layer3Address1Register4 = 0x9D4, // MAC_Layer3_Addr1_Reg4
            Layer3Address2Register4 = 0x9D8, // MAC_Layer3_Addr2_Reg4
            Layer3Address3Register4 = 0x9DC, // MAC_Layer3_Addr3_Reg4

            Layer3Layer4Control5 = 0x9F0, // MAC_L3_L4_Control5
            Layer4Address5 = 0x9F4, // MAC_Layer4_Address5
            Layer3Address0Register5 = 0xA00, // MAC_Layer3_Addr0_Reg5
            Layer3Address1Register5 = 0xA04, // MAC_Layer3_Addr1_Reg5
            Layer3Address2Register5 = 0xA08, // MAC_Layer3_Addr2_Reg5
            Layer3Address3Register5 = 0xA0C, // MAC_Layer3_Addr3_Reg5

            Layer3Layer4Control6 = 0xA20, // MAC_L3_L4_Control6
            Layer4Address6 = 0xA24, // MAC_Layer4_Address6
            Layer3Address0Register6 = 0xA30, // MAC_Layer3_Addr0_Reg6
            Layer3Address1Register6 = 0xA34, // MAC_Layer3_Addr1_Reg6
            Layer3Address2Register6 = 0xA38, // MAC_Layer3_Addr2_Reg6
            Layer3Address3Register6 = 0xA3C, // MAC_Layer3_Addr3_Reg6

            Layer3Layer4Control7 = 0xA50, // MAC_L3_L4_Control7
            Layer4Address7 = 0xA54, // MAC_Layer4_Address7
            Layer3Address0Register7 = 0xA60, // MAC_Layer3_Addr0_Reg7
            Layer3Address1Register7 = 0xA64, // MAC_Layer3_Addr1_Reg7
            Layer3Address2Register7 = 0xA68, // MAC_Layer3_Addr2_Reg7
            Layer3Address3Register7 = 0xA6C, // MAC_Layer3_Addr3_Reg7

            IndirectAccessControl = 0xA70, // MAC_Indir_Access_Ctrl
            IndirectAccessData = 0xA74, // MAC_Indir_Access_Data

            TypeMatchReceiveQueueRegister0 = 0xA74, // MAC_TMRQ_Regs0
            TypeMatchReceiveQueueRegister1 = 0xA74, // MAC_TMRQ_Regs1
            TypeMatchReceiveQueueRegister2 = 0xA74, // MAC_TMRQ_Regs2
            TypeMatchReceiveQueueRegister3 = 0xA74, // MAC_TMRQ_Regs3
            TypeMatchReceiveQueueRegister4 = 0xA74, // MAC_TMRQ_Regs4
            TypeMatchReceiveQueueRegister5 = 0xA74, // MAC_TMRQ_Regs5
            TypeMatchReceiveQueueRegister6 = 0xA74, // MAC_TMRQ_Regs6
            TypeMatchReceiveQueueRegister7 = 0xA74, // MAC_TMRQ_Regs7

            TimestampControl = 0xB00, // MAC_Timestamp_Control
            SubSecondIncrement = 0xB04, // MAC_Sub_Second_Increment
            SystemTimeSeconds = 0xB08, // MAC_System_Time_Seconds
            SystemTimeNanoseconds = 0xB0C, // MAC_System_Time_Nanoseconds
            SystemTimeSecondsUpdate = 0xB10, // MAC_System_Time_Seconds_Update
            SystemTimeNanosecondsUpdate = 0xB14, // MAC_System_Time_Nanoseconds_Update
            TimestampAddend = 0xB18, // MAC_Timestamp_Addend
            SystemTimeHigherWordSeconds = 0xB1C, // MAC_System_Time_Higher_Word_Seconds
            TimestampStatus = 0xB20, // MAC_Timestamp_Status
            TransmitTimestampStatusNanoseconds = 0xB30, // MAC_Tx_Timestamp_Status_Nanoseconds
            TransmitTimestampStatusSeconds = 0xB34, // MAC_Tx_Timestamp_Status_Seconds
            TimestampIngressAsymmetryCorrection = 0xB50, // MAC_Timestamp_Ingress_Asym_Corr
            TimestampEgressAsymmetryCorrection = 0xB54, // MAC_Timestamp_Egress_Asym_Corr
            TimestampIngressCorrectionNanosecond = 0xB58, // MAC_Timestamp_Ingress_Corr_Nanosecond
            TimestampEgressCorrectionNanosecond = 0xB5C, // MAC_Timestamp_Egress_Corr_Nanosecond
            TimestampIngressCorrectionSubnanosecond = 0xB60, // MAC_Timestamp_Ingress_Corr_Subnanosec
            TimestampEgressCorrectionSubnanosecond = 0xB64, // MAC_Timestamp_Egress_Corr_Subnanosec
            TimestampIngressLatency = 0xB68, // MAC_Timestamp_Ingress_Latency
            TimestampEgressLatency = 0xB6C, // MAC_Timestamp_Egress_Latency

            PPSControl = 0xB70, // MAC_PPS_Control
            PPS0TargetTimeSeconds = 0xB80, // MAC_PPS0_Target_Time_Seconds
            PPS0TargetTimeNanoseconds = 0xB84, // MAC_PPS0_Target_Time_Nanoseconds
            PPS0Interval = 0xB88, // MAC_PPS0_Interval
            PPS0Width = 0xB8C, // MAC_PPS0_Width
            PPS1TargetTimeSeconds = 0xB90, // MAC_PPS1_Target_Time_Seconds
            PPS1TargetTimeNanoseconds = 0xB94, // MAC_PPS1_Target_Time_Nanoseconds
            PPS1Interval = 0xB98, // MAC_PPS1_Interval
            PPS1Width = 0xB9C, // MAC_PPS1_Width
            PPS2TargetTimeSeconds = 0xBA0, // MAC_PPS2_Target_Time_Seconds
            PPS2TargetTimeNanoseconds = 0xBA4, // MAC_PPS2_Target_Time_Nanoseconds
            PPS2Interval = 0xBA8, // MAC_PPS2_Interval
            PPS2Width = 0xBAC, // MAC_PPS2_Width
            PPS3TargetTimeSeconds = 0xBB0, // MAC_PPS3_Target_Time_Seconds
            PPS3TargetTimeNanoseconds = 0xBB4, // MAC_PPS3_Target_Time_Nanoseconds
            PPS3Interval = 0xBB8, // MAC_PPS3_Interval
            PPS3Width = 0xBBC, // MAC_PPS3_Width

            MTLOperationMode = 0xC00, // MTL_Operation_Mode
            MTLDebugControl = 0xC08, // MTL_DBG_CTL
            MTLDebugStatus = 0xC0C, // MTL_DBG_STS
            MTLFIFODebugData = 0xC10, // MTL_FIFO_Debug_Data
            MTLInterruptStatus = 0xC20, // MTL_Interrupt_Status
            MTLReceiveQueueDMAMap0 = 0xC30, // MTL_RxQ_DMA_Map0
            MTLTimeBasedSchedulingControl = 0xC40, // MTL_TBS_CTRL
            MTLEnhancementsToScheduledTransmissionControl = 0xC50, // MTL_EST_Control
            MTLEnhancementsToScheduledTransmissionExtendedControl = 0xC54, // MTL_EST_Ext_Control
            MTLEnhancementsToScheduledTransmissionStatus = 0xC58, // MTL_EST_Status
            MTLEnhancementsToScheduledTransmissionSchedulingError = 0xC60, // MTL_EST_Sch_Error
            MTLEnhancementsToScheduledTransmissionFrameSizeError = 0xC64, // MTL_EST_Frm_Size_Error
            MTLEnhancementsToScheduledTransmissionInterruptEnable = 0xC70, // MTL_EST_Intr_Enable
            MTLEnhancementsToScheduledTransmissionGateControlList = 0xC80, // MTL_EST_GCL_Control
            MTLEnhancementsToScheduledTransmissionGateControlData = 0xC84, // MTL_EST_GCL_Data
            MTLFramePreemptionControlStatus = 0xC90, // MTL_FPE_CTRL_STS
            MTLFramePreemptionAdvanceTime = 0xC94, // MTL_FPE_Advance
            MTLReceiveParserControlStatus = 0xCA0, // MTL_RXP_Control_Status
            MTLReceiveParserInterruptControlStatus = 0xCA4, // MTL_RXP_Interrupt_Control_Status
            MTLReceiveParserDropCount = 0xCA8, // MTL_RXP_Drop_Cnt
            MTLReceiveParserErrorCount = 0xCAC, // MTL_RXP_Error_Cnt
            MTLReceiveParserIndirectAccessControlStatus = 0xCB0, // MTL_RXP_Indirect_Acc_Control_Status
            MTLReceiveParserIndirectAccessData = 0xCB4, // MTL_RXP_Indirect_Acc_Data
            MTLReceiveParserBypassCount = 0xCB8, // MTL_RXP_Bypass_Cnt
            MTLErrorCorrectionControl = 0xCC0, // MTL_ECC_Control
            MTLSafetyInterruptStatus = 0xCC4, // MTL_Safety_Interrupt_Status
            MTLErrorCorrectionInterruptEnable = 0xCC8, // MTL_ECC_Interrupt_Enable
            MTLErrorCorrectionInterruptStatus = 0xCCC, // MTL_ECC_Interrupt_Status
            MTLErrorCorrectionErrorStatusCapture = 0xCD0, // MTL_ECC_Err_Sts_Rctl
            MTLErrorCorrectionErrorAddressStatus = 0xCD4, // MTL_ECC_Err_Addr_Status
            MTLErrorCorrectionErrorCountStatus = 0xCD8, // MTL_ECC_Err_Cntr_Status
            MTLDataParityProtectionControl = 0xCE0, // MTL_DPP_Control
            MTLDataParityProtectionErrorCorrectionErrorInjection = 0xCE4, // MTL_DPP_ECC_EIC
            MTLTransmitQueue0OperationMode = 0xD00, // MTL_TxQ0_Operation_Mode
            MTLTransmitQueue0Underflow = 0xD04, // MTL_TxQ0_Underflow
            MTLTransmitQueue0Debug = 0xD08, // MTL_TxQ0_Debug
            MTLTransmitQueue0ETSStatus = 0xD14, // MTL_TxQ0_ETS_Status
            MTLTransmitQueue0QuantumWeight = 0xD18, // MTL_TxQ0_Quantum_Weight
            MTLQueue0InterruptControlStatus = 0xD2C, // MTL_Q0_Interrupt_Control_Status
            MTLReceiveQueue0OperationMode = 0xD30, // MTL_RxQ0_Operation_Mode
            MTLReceiveQueue0MissedPacketOverflowCount = 0xD34, // MTL_RxQ0_Missed_Packet_Overflow_Cnt
            MTLReceiveQueue0Debug = 0xD38, // MTL_RxQ0_Debug
            MTLReceiveQueue0Control = 0xD3C, // MTL_RxQ0_Control
            MTLTransmitQueue1OperationMode = 0xD40, // MTL_TxQ1_Operation_Mode
            MTLTransmitQueue1Underflow = 0xD44, // MTL_TxQ1_Underflow
            MTLTransmitQueue1Debug = 0xD48, // MTL_TxQ1_Debug
            MTLTransmitQueue1ETSControl = 0xD50, // MTL_TxQ1_ETS_Control
            MTLTransmitQueue1ETSStatus = 0xD54, // MTL_TxQ1_ETS_Status
            MTLTransmitQueue1QuantumWeight = 0xD58, // MTL_TxQ1_Quantum_Weight
            MTLTransmitQueue1SendSlopeCredit = 0xD5C, // MTL_TxQ1_SendSlopeCredit
            MTLTransmitQueue1HighCredit = 0xD60, // MTL_TxQ1_HiCredit
            MTLTransmitQueue1LowCredit = 0xD64, // MTL_TxQ1_LoCredit
            MTLQueue1InterruptControlStatus = 0xD6C, // MTL_Q1_Interrupt_Control_Status
            MTLReceiveQueue1OperationMode = 0xD70, // MTL_RxQ1_Operation_Mode
            MTLReceiveQueue1MissedPacketOverflowCount = 0xD74, // MTL_RxQ1_Missed_Packet_Overflow_Cnt
            MTLReceiveQueue1Debug = 0xD78, // MTL_RxQ1_Debug
            MTLReceiveQueue1Control = 0xD7C, // MTL_RxQ1_Control
            MTLTransmitQueue2OperationMode = 0xD80, // MTL_TxQ2_Operation_Mode
            MTLTransmitQueue2Underflow = 0xD84, // MTL_TxQ2_Underflow
            MTLTransmitQueue2Debug = 0xD88, // MTL_TxQ2_Debug
            MTLTransmitQueue2ETSControl = 0xD90, // MTL_TxQ2_ETS_Control
            MTLTransmitQueue2ETSStatus = 0xD94, // MTL_TxQ2_ETS_Status
            MTLTransmitQueue2QuantumWeight = 0xD98, // MTL_TxQ2_Quantum_Weight
            MTLTransmitQueue2SendSlopeCredit = 0xD9C, // MTL_TxQ2_SendSlopeCredit
            MTLTransmitQueue2HighCredit = 0xDA0, // MTL_TxQ2_HiCredit
            MTLTransmitQueue2LowCredit = 0xDA4, // MTL_TxQ2_LoCredit
            MTLQueue2InterruptControlStatus = 0xDAC, // MTL_Q2_Interrupt_Control_Status
            MTLReceiveQueue2OperationMode = 0xDB0, // MTL_RxQ2_Operation_Mode
            MTLReceiveQueue2MissedPacketOverflowCount = 0xDB4, // MTL_RxQ2_Missed_Packet_Overflow_Cnt
            MTLReceiveQueue2Debug = 0xDB8, // MTL_RxQ2_Debug
            MTLReceiveQueue2Control = 0xDBC, // MTL_RxQ2_Control

            DMAMode = 0x1000, // DMA_Mode
            DMASystemBusMode = 0x1004, // DMA_SysBus_Mode
            DMAInterruptStatus = 0x1008, // DMA_Interrupt_Status
            DMADebugStatus0 = 0x100C, // DMA_Debug_Status0
            DMATBSControl0 = 0x1050, // DMA_TBS_CTRL0
            DMATBSControl1 = 0x1054, // DMA_TBS_CTRL1
            DMATBSControl2 = 0x1058, // DMA_TBS_CTRL2
            DMATBSControl3 = 0x105C, // DMA_TBS_CTRL3
            DMASafetyInterruptStatus = 0x1080, // DMA_Safety_Interrupt_Status

            DMAChannel0Control = 0x1100, // DMA_CH0_Control
            DMAChannel0TransmitControl = 0x1104, // DMA_CH0_Tx_Control
            DMAChannel0ReceiveControl = 0x1108, // DMA_CH0_Rx_Control
            DMAChannel0TransmitDescriptorListAddress = 0x1114, // DMA_CH0_TxDesc_List_Address
            DMAChannel0ReceiveDescriptorListAddress = 0x111C, // DMA_CH0_RxDesc_List_Address
            DMAChannel0TransmitDescriptorTailPointer = 0x1120, // DMA_CH0_TxDesc_Tail_Pointer
            DMAChannel0ReceiveDescriptorTailPointer = 0x1128, // DMA_CH0_RxDesc_Tail_Pointer
            DMAChannel0TransmitDescriptorRingLength = 0x112C, // DMA_CH0_TxDesc_Ring_Length
            DMAChannel0ReceiveControl2 = 0x1130, // DMA_CH0_Rx_Control2
            DMAChannel0InterruptEnable = 0x1134, // DMA_CH0_Interrupt_Enable
            DMAChannel0ReceiveInterruptWatchdogTimer = 0x1138, // DMA_CH0_Rx_Interrupt_Watchdog_Timer
            DMAChannel0SlotFunctionControlStatus = 0x113C, // DMA_CH0_Slot_Function_Control_Status
            DMAChannel0CurrentApplicationTransmitDescriptor = 0x1144, // DMA_CH0_Current_App_TxDesc
            DMAChannel0CurrentApplicationReceiveDescriptor = 0x114C, // DMA_CH0_Current_App_RxDesc
            DMAChannel0CurrentApplicationTransmitBuffer = 0x1154, // DMA_CH0_Current_App_TxBuffer
            DMAChannel0CurrentApplicationReceiveBuffer = 0x115C, // DMA_CH0_Current_App_RxBuffer
            DMAChannel0Status = 0x1160, // DMA_CH0_Status
            DMAChannel0MissedFrameCount = 0x1164, // DMA_CH0_Miss_Frame_Cnt
            DMAChannel0ReceiveParserAcceptCount = 0x1168, // DMA_CH0_RXP_Accept_Cnt
            DMAChannel0ReceiveERICount = 0x116C, // DMA_CH0_RX_ERI_Cnt

            DMAChannel1Control = 0x1180, // DMA_CH1_Control
            DMAChannel1TransmitControl = 0x1184, // DMA_CH1_Tx_Control
            DMAChannel1ReceiveControl = 0x1188, // DMA_CH1_Rx_Control
            DMAChannel1TransmitDescriptorListAddress = 0x1194, // DMA_CH1_TxDesc_List_Address
            DMAChannel1ReceiveDescriptorListAddress = 0x119C, // DMA_CH1_RxDesc_List_Address
            DMAChannel1TransmitDescriptorTailPointer = 0x11A0, // DMA_CH1_TxDesc_Tail_Pointer
            DMAChannel1ReceiveDescriptorTailPointer = 0x11A8, // DMA_CH1_RxDesc_Tail_Pointer
            DMAChannel1TransmitDescriptorRingLength = 0x11AC, // DMA_CH1_TxDesc_Ring_Length
            DMAChannel1ReceiveControl2 = 0x11B0, // DMA_CH1_Rx_Control2
            DMAChannel1InterruptEnable = 0x11B4, // DMA_CH1_Interrupt_Enable
            DMAChannel1ReceiveInterruptWatchdogTimer = 0x11B8, // DMA_CH1_Rx_Interrupt_Watchdog_Timer
            DMAChannel1SlotFunctionControlStatus = 0x11BC, // DMA_CH1_Slot_Function_Control_Status
            DMAChannel1CurrentApplicationTransmitDescriptor = 0x11C4, // DMA_CH1_Current_App_TxDesc
            DMAChannel1CurrentApplicationReceiveDescriptor = 0x11CC, // DMA_CH1_Current_App_RxDesc
            DMAChannel1CurrentApplicationTransmitBuffer = 0x11D4, // DMA_CH1_Current_App_TxBuffer
            DMAChannel1CurrentApplicationReceiveBuffer = 0x11DC, // DMA_CH1_Current_App_RxBuffer
            DMAChannel1Status = 0x11E0, // DMA_CH1_Status
            DMAChannel1MissedFrameCount = 0x11E4, // DMA_CH1_Miss_Frame_Cnt
            DMAChannel1ReceiveParserAcceptCount = 0x11E8, // DMA_CH1_RXP_Accept_Cnt
            DMAChannel1ReceiveERICount = 0x11EC, // DMA_CH1_RX_ERI_Cnt

            DMAChannel2Control = 0x1200, // DMA_CH2_Control
            DMAChannel2TransmitControl = 0x1204, // DMA_CH2_Tx_Control
            DMAChannel2ReceiveControl = 0x1208, // DMA_CH2_Rx_Control
            DMAChannel2TransmitDescriptorListAddress = 0x1214, // DMA_CH2_TxDesc_List_Address
            DMAChannel2ReceiveDescriptorListAddress = 0x1218, // DMA_CH2_RxDesc_List_Address
            DMAChannel2TransmitDescriptorTailPointer = 0x1220, // DMA_CH2_TxDesc_Tail_Pointer
            DMAChannel2ReceiveDescriptorTailPointer = 0x1228, // DMA_CH2_RxDesc_Tail_Pointer
            DMAChannel2TransmitDescriptorRingLength = 0x122C, // DMA_CH2_TxDesc_Ring_Length
            DMAChannel2ReceiveControl2 = 0x1230, // DMA_CH2_Rx_Control2
            DMAChannel2InterruptEnable = 0x1234, // DMA_CH2_Interrupt_Enable
            DMAChannel2ReceiveInterruptWatchdogTimer = 0x1238, // DMA_CH2_Rx_Interrupt_Watchdog_Timer
            DMAChannel2SlotFunctionControlStatus = 0x123C, // DMA_CH2_Slot_Function_Control_Status
            DMAChannel2CurrentApplicationTransmitDescriptor = 0x1244, // DMA_CH2_Current_App_TxDesc
            DMAChannel2CurrentApplicationReceiveDescriptor = 0x124C, // DMA_CH2_Current_App_RxDesc
            DMAChannel2CurrentApplicationTransmitBuffer = 0x1254, // DMA_CH2_Current_App_TxBuffer
            DMAChannel2CurrentApplicationReceiveBuffer = 0x125C, // DMA_CH2_Current_App_RxBuffer
            DMAChannel2Status = 0x1260, // DMA_CH2_Status
            DMAChannel2MissedFrameCount = 0x1264, // DMA_CH2_Miss_Frame_Cnt
            DMAChannel2ReceiveParserAcceptCount = 0x1268, // DMA_CH2_RXP_Accept_Cnt
            DMAChannel2ReceiveERICount = 0x126C, // DMA_CH2_RX_ERI_Cnt
        }
    }
}
