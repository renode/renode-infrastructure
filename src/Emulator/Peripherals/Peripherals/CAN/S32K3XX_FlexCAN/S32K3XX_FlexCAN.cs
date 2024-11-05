//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.CAN
{
    public partial class S32K3XX_FlexCAN : BasicDoubleWordPeripheral, IBytePeripheral, ICAN, IKnownSize
    {
        public S32K3XX_FlexCAN(IMachine machine, uint numberOfMessageBuffers = 64, uint enhancedRxFifoSize = 0) : base(machine)
        {
            if(numberOfMessageBuffers != 32 && numberOfMessageBuffers != 64 && numberOfMessageBuffers != 96)
            {
                throw new ConstructionException($"{nameof(numberOfMessageBuffers)} parameter should be set to one of the supported values: {{32, 64, 96}}");
            }
            if(enhancedRxFifoSize != 0 && enhancedRxFifoSize != 20)
            {
                throw new ConstructionException($"{nameof(enhancedRxFifoSize)} parameter should be set to one of the supported values: {{0, 20}}");
            }
            this.numberOfMessageBuffers = numberOfMessageBuffers;
            this.enhancedRxFifoSize = enhancedRxFifoSize;

            IRQ = new GPIO();

            messageBufferRange = new Range((ulong)Registers.MessageBuffer, numberOfMessageBuffers * 8);
            messageBuffers = new ArrayMemory(messageBufferRange.Size);
            messageBufferInterruptEnable = new IFlagRegisterField[numberOfMessageBuffers];
            messageBufferInterrupt = new IFlagRegisterField[numberOfMessageBuffers];
            messageBufferSize = new IEnumRegisterField<MessageBufferSize>[MessageBufferRegionsCount];
            individualMaskBits = new IValueRegisterField[numberOfMessageBuffers];

            legacyRxFifo = new Queue<KeyValuePair<ushort, CANMessageFrame>>();

            DefineRegisters();
        }

        public void OnFrameReceived(CANMessageFrame message)
        {
            this.Log(LogLevel.Noisy, "Received frame: {0}", message);
            if(loopback.Value)
            {
                this.Log(LogLevel.Debug, "Ignoring frame, in loopback mode");
                return;
            }
            OnFrameReceivedInner(message);
        }

        public override uint ReadDoubleWord(long offset)
        {
            if(messageBufferRange.Contains(offset))
            {
                var messageBufferOffset = offset - (long)messageBufferRange.StartAddress;
                var returnValue = messageBuffers.ReadDoubleWord(messageBufferOffset);

                // NOTE: Check if we read last double word of the FIFO entry pop next element
                if(legacyFifoEnable.Value && offset == LegacyRxFifoLastWordOffset)
                {
                    legacyRxFifo.TryDequeue(out var _);
                    UpdateLegacyRxFifoMemory();
                }
                return returnValue;
            }
            return RegistersCollection.Read(offset);
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(messageBufferRange.Contains(offset))
            {
                var mbOffset = (ulong)offset - messageBufferRange.StartAddress;
                messageBuffers.WriteDoubleWord((long)mbOffset, value);
                // NOTE: Align offset to size of the message buffer
                var mbRegion = GetMessageBufferRegionByOffset(mbOffset);
                mbOffset -= mbOffset % GetMessageBufferSizeByRegion(mbRegion);
                TryTransmitFromMessageBuffer(mbOffset);
                return;
            }
            RegistersCollection.Write(offset, value);
        }

        public byte ReadByte(long offset)
        {
            if(messageBufferRange.Contains(offset))
            {
                var messageBufferOffset = offset - (long)messageBufferRange.StartAddress;
                var returnValue = messageBuffers.ReadByte(messageBufferOffset);

                // NOTE: Check if we read last byte of the FIFO entry pop next element
                if(legacyFifoEnable.Value && offset == (LegacyRxFifoLastWordOffset + 3))
                {
                    legacyRxFifo.TryDequeue(out var _);
                    UpdateLegacyRxFifoMemory();
                }
                return returnValue;
            }
            return this.ReadByteNotTranslated(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            if(messageBufferRange.Contains(offset))
            {
                var mbOffset = (ulong)offset - messageBufferRange.StartAddress;
                messageBuffers.WriteByte((long)mbOffset, value);

                // NOTE: Align offset to size of the message buffer
                var mbRegion = GetMessageBufferRegionByOffset(mbOffset);
                mbOffset -= mbOffset % GetMessageBufferSizeByRegion(mbRegion);
                TryTransmitFromMessageBuffer(mbOffset);
                return;
            }
            this.WriteByteNotTranslated(offset, value);
        }

        public long Size => 0x3200;
        public event Action<CANMessageFrame> FrameSent;
        public GPIO IRQ { get; }

        private void SoftReset()
        {
            // NOTE: Currently, peripheral does not respect behaviour of soft/hard-reset,
            // and instead all registers are always cleared.
            var moduleDisable = this.moduleDisable.Value;
            Reset();
            this.moduleDisable.Value = moduleDisable;
        }

        private void DefineRegisters()
        {
            Registers.ModuleConfiguration.Define(this, 0xD890000F)
                .WithValueField(0, 7, out lastMessageBufferIndex, name: "Number of the Last Message Buffer (MCR.MAXMB)")
                .WithReservedBits(7, 1)
                .WithEnumField(8, 2, out legacyFilterFormat, name: "ID Acceptance Mode (MCR.IDAM)")
                .WithReservedBits(10, 1)
                .WithTaggedFlag("CAN FD Operation Enable (MCR.FDEN)", 11)
                .WithFlag(12, out abortEnable, name: "Abort Enable (MCR.AEN)",
                    changeCallback: GetFreezeModeOnlyWritableChangeCallback(abortEnable, "MCR.AEN"))
                .WithTaggedFlag("Local Priority Enable (MCR.LPRIOEN)", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("DMA Enable (MCR.DMA)", 15)
                .WithFlag(16, out individualMaskingAndQueue, name: "Individual RX Masking and Queue Enable (MCR.IRMQ)",
                    changeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            this.Log(LogLevel.Warning, "Global masking for message buffers is not currently implemented, frame reception will not work correctly");
                        }
                    })
                .WithFlag(17, out selfReceptionDisable, name: "Self-Reception Disable (MCR.SRXDIS)",
                    changeCallback: GetFreezeModeOnlyWritableChangeCallback(selfReceptionDisable, "MCR.SRXDIS"))
                .WithReservedBits(18, 2)
                .WithFlag(20, out lowPowerModeAcknowledge, FieldMode.WriteOneToClear | FieldMode.Read, name: "Low-Power Mode Acknowledge (MCR.LPMACK)")
                .WithTaggedFlag("Warning Interrupt Enable (MCR.WRNEN)", 21)
                .WithReservedBits(22, 1)
                .WithTaggedFlag("Supervisor Mode (MCR.SUPV)", 23)
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => Freeze, name: "Freeze Mode Acknowledge (MCR.FRZACK)")
                .WithFlag(25, FieldMode.WriteOneToClear, writeCallback: (_, value) => { if(value) SoftReset(); }, name: "Soft Reset (MCR.SOFTRST)")
                .WithReservedBits(26, 1)
                .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => moduleDisable.Value || Freeze, name: "FlexCAN Not Ready (MCR.NOTRDY)")
                .WithFlag(28, out halt, name: "Halt FlexCAN (MCR.HALT)")
                .WithFlag(29, out legacyFifoEnable, name: "Legacy RX FIFO Enable (MCR.RFEN)")
                .WithFlag(30, out freezeEnable, name: "Freeze Enable (MCR.FRZ)")
                .WithFlag(31, out moduleDisable, name: "Module Disable (MCR.MDIS)",
                    changeCallback: (_, value) => lowPowerModeAcknowledge.Value |= value)
                .WithChangeCallback((_, __) => RunArbitrationProcess())
            ;

            Registers.Control1.Define(this, softResettable: false)
                .WithTag("Propagation Segment (CTRL1.PROPSEG)", 0, 3)
                .WithFlag(3, out listenOnly, name: "Listen-Only Mode (CTRL1.LOM)")
                .WithFlag(4, out lowestBufferTransmittedFirst, name: "Lowest Buffer Transmitted First (CTRL1.LBUF)")
                .WithTaggedFlag("Timer Sync (CTRL1.TSYN)", 5)
                .WithTaggedFlag("Bus Off Recovery (CTRL1.BOFFREC)", 6)
                .WithTaggedFlag("CAN Bit Sampling (CTRL1.SMP)", 7)
                .WithReservedBits(8, 2)
                .WithTaggedFlag("RX Warning Interrupt Mask (CTRL1.RWRNMSK)", 10)
                .WithTaggedFlag("TX Warning Interrupt Mask (CTRL1.TWRNMSK)", 11)
                .WithFlag(12, out loopback, name: "Loopback Mode (CTRL1.LPB)")
                .WithReservedBits(13, 1)
                .WithTaggedFlag("Error Interrupt Mask (CTRL1.ERRMSK)", 14)
                .WithTaggedFlag("Bus Off Interrupt Mask (CTRL1.BOFFMSK)", 15)
                .WithTag("Phase Segment 2 (CTRL1.PSEG2)", 16, 3)
                .WithTag("Phase Segment 1 (CTRL1.PSEG1)", 19, 3)
                .WithTag("Resync Jump Width (CTRL1.RJW)", 22, 2)
                .WithTag("Prescaler Division Factor (CTRL1.PRESDIV)", 24, 8)
            ;

            Registers.FreeRunningTimer.Define(this)
                .WithTag("Timer Value (TIMER.TIMER)", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.RxMessageBuffersGlobalMask.Define(this)
                .WithTag("Global Mask for RX Message Buffers (RXMGMASK.MG)", 0, 32)
            ;

            Registers.Receive14Mask.Define(this)
                .WithTag("RX Buffer 14 Mask Bits (RX14MASK.RX14M)", 0, 32)
            ;

            Registers.Receive15Mask.Define(this)
                .WithTag("RX Buffer 15 Mask Bits (RX15MASK.RX15M)", 0, 32)
            ;

            Registers.ErrorCounter.Define(this)
                .WithTag("Transmit Error Counter (ECR.TXERRCNT)", 0, 8)
                .WithTag("Receive Error Counter (ECR.RXERRCNT)", 8, 8)
                .WithTag("Transmit Error Counter for Fast Bits (ECR.TXERRCNT_FAST)", 16, 8)
                .WithTag("Receive Error Counter for Fast Bits (ECR.RXERRCNT_FAST)", 24, 8)
            ;

            Registers.ErrorAndStatus1.Define(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("Error Interrupt Flag (ESR1.ERRINT)", 1)
                .WithTaggedFlag("Bus Off Interrupt Flag (ESR1.BOFFINT)", 2)
                .WithTaggedFlag("FlexCAN in Reception Flag (ESR1.RX)", 3)
                .WithTag("Fault Confinement State (ESR1.FLTCONF)", 4, 2)
                .WithTaggedFlag("FlexCAN In Transmission (ESR1.TX)", 6)
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => true, name: "Idle (ESR1.IDLE)")
                .WithTaggedFlag("RX Error Warning Flag (ESR1.RXWRN)", 8)
                .WithTaggedFlag("TX Error Warning Flag (ESR1.TXWRN)", 9)
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => false, name: "Stuffing Error Flag (ESR1.STFERR)")
                .WithTaggedFlag("Form Error Flag (ESR1.FRMERR)", 11)
                .WithTaggedFlag("Cyclic Redundancy Check Error Flag (ESR1.CRCERR)", 12)
                .WithTaggedFlag("Acknowledge Error Flag (ESR1.ACKERR)", 13)
                .WithTaggedFlag("Bit0 Error Flag (ESR1.BIT0ERR)", 14)
                .WithTaggedFlag("Bit1 Error Flag (ESR1.BIT1ERR)", 15)
                .WithTaggedFlag("RX Warning Interrupt Flag (ESR1.RWRNINT)", 16)
                .WithTaggedFlag("TX Warning Interrupt Flag (ESR1.TWRNINT)", 17)
                .WithFlag(18, FieldMode.Read, valueProviderCallback: _ => true, name: "CAN Synchronization Status Flag (ESR1.SYNCH)")
                .WithTaggedFlag("Bus Off Done Interrupt Flag (ESR1.BOFFDONEINT)", 19)
                .WithTaggedFlag("Fast Error Interrupt Flag (ESR1.ERRINT_FAST)", 20)
                .WithTaggedFlag("Error Overrun Flag (ESR1.ERROVR)", 21)
                .WithReservedBits(22, 4)
                .WithFlag(26, FieldMode.Read, valueProviderCallback: _ => false, name: "Fast Stuffing Error Flag (ESR1.STFERR_FAST)")
                .WithTaggedFlag("Fast Form Error Flag (ESR1.FRMERR_FAST)", 27)
                .WithTaggedFlag("Fast Cyclic Redundancy Check Error Flag (ESR1.CRCERR_FAST)", 28)
                .WithReservedBits(29, 1)
                .WithTaggedFlag("Fast Bit0 Error Flag (ESR1.BIT0ERR_FAST)", 30)
                .WithTaggedFlag("Fast Bit1 Error Flag (ESR1.BIT1ERR_FAST)", 31)
            ;

            var interruptMask1 = Registers.InterruptMasks1.Define(this)
                .WithFlag(0, out messageBufferInterruptEnable[0], name: "Buffer MB0 Mask (IMASK1.BUF0M)")
                .WithFlag(1, out messageBufferInterruptEnable[1], name: "Buffer MB1 Mask (IMASK1.BUF1M)")
                .WithFlag(2, out messageBufferInterruptEnable[2], name: "Buffer MB2 Mask (IMASK1.BUF2M)")
                .WithFlag(3, out messageBufferInterruptEnable[3], name: "Buffer MB3 Mask (IMASK1.BUF3M)")
                .WithFlag(4, out messageBufferInterruptEnable[4], name: "Buffer MB4 Mask (IMASK1.BUF4M)")
                .WithFlag(5, out messageBufferInterruptEnable[5], name: "Buffer MB5 Mask (IMASK1.BUF5M)")
                .WithFlag(6, out messageBufferInterruptEnable[6], name: "Buffer MB6 Mask (IMASK1.BUF6M)")
                .WithFlag(7, out messageBufferInterruptEnable[7], name: "Buffer MB7 Mask (IMASK1.BUF7M)")
            ;

            var interruptFlag1 = Registers.InterruptFlags1.Define(this)
                .WithFlag(0, out messageBufferInterrupt[0], FieldMode.WriteOneToClear | FieldMode.Read, name: "Buffer MB0 Interrupt or Clear Legacy FIFO bit (IFLAG1.BUF0I)")
                .WithFlag(1, out messageBufferInterrupt[1], FieldMode.WriteOneToClear | FieldMode.Read, name: "Buffer MB1 Interrupt (IFLAG1.BUF1M)")
                .WithFlag(2, out messageBufferInterrupt[2], FieldMode.WriteOneToClear | FieldMode.Read, name: "Buffer MB2 Interrupt (IFLAG1.BUF2M)")
                .WithFlag(3, out messageBufferInterrupt[3], FieldMode.WriteOneToClear | FieldMode.Read, name: "Buffer MB3 Interrupt (IFLAG1.BUF3M)")
                .WithFlag(4, out messageBufferInterrupt[4], FieldMode.WriteOneToClear | FieldMode.Read, name: "Buffer MB4 Interrupt (IFLAG1.BUF4M)")
                .WithFlag(5, out messageBufferInterrupt[5], FieldMode.WriteOneToClear | FieldMode.Read, name: "Buffer MB5 Interrupt or Frames available in Legacy RX FIFO (IFLAG1.BUF5I)")
                .WithFlag(6, out messageBufferInterrupt[6], FieldMode.WriteOneToClear | FieldMode.Read, name: "Buffer MB6 Interrupt or Legacy RX FIFO Warning (IFLAG1.BUF6I)")
                .WithFlag(7, out messageBufferInterrupt[7], FieldMode.WriteOneToClear | FieldMode.Read, name: "Buffer MB7 Interrupt or Legacy RX FIFO Overflow (IFLAG1.BUF7I)")
            ;

            var interruptMask2 = Registers.InterruptMasks2.Define(this);
            var interruptFlag2 = Registers.InterruptFlags2.Define(this);

            RegisterMessageBufferInterruptFlags(interruptFlag1, interruptMask1, 8, 8, 24);
            RegisterMessageBufferInterruptFlags(interruptFlag2, interruptMask2, 32);

            Registers.Control2.Define32(this, Control2ResetValue, false)
                .WithReservedBits(0, 6)
                .WithTag("Timestamp Capture Point (CTRL2.TSTAMPCAP)", 6, 2)
                .WithTag("Message Buffer Timestamp Base (CTRL2.MBTSBASE)", 8, 2)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("Edge Filter Disable (CTRL2.EDFLTDIS)", 11)
                .WithTaggedFlag("ISO CAN FD Enable (CTRL2.ISOCANFDEN)", 12)
                .WithTaggedFlag("Bit Timing Expansion Enable (CTRL2.BTE)", 13)
                .WithTaggedFlag("Protocol Exception Enable (CTRL2.PREXCEN)", 14)
                .WithTaggedFlag("Timer Source (CTRL2.TIMER_SRC)", 15)
                .WithTaggedFlag("Entire Frame Arbitration Field Comparison Enable for RX Message Buffers (CTRL2.EACEN)", 16)
                .WithTaggedFlag("Remote Request Storing (CTRL2.RRS)", 17)
                .WithFlag(18, out messageBuffersReceptionPriority, name: "Message Buffers Reception Priority (CTRL2.MRP)",
                    changeCallback: GetFreezeModeOnlyWritableChangeCallback(messageBuffersReceptionPriority, "CTRL2.MRP"))
                .WithValueField(19, 5, out transmissionArbitrationStartDelay, name: "Transmission Arbitration Start Delay (CTRL2.TASD)",
                    changeCallback: GetFreezeModeOnlyWritableChangeCallback(transmissionArbitrationStartDelay, "CTRL2.TASD"))
                .WithValueField(24, 4, out numberOfLegacyRxFifoFilters, name: "Number of Legacy Receive FIFO Filters (CTRL2.RFFN)",
                    changeCallback: GetFreezeModeOnlyWritableChangeCallback(numberOfLegacyRxFifoFilters, "CTRL2.RFFN"))
                .WithTaggedFlag("Write Access to Memory in Freeze Mode (CTRL2.WRMFRZ)", 28)
                .WithTaggedFlag("Error Correction Configuration Register Write Enable (CTRL2.ECRWRE)", 29)
                .WithTaggedFlag("Bus Off Done Interrupt Mask (CTRL2.BOFFDONEMSK)", 30)
                .WithTaggedFlag("Error Interrupt Mask for Errors Detected in the Data Phase of Fast CAN FD Frames (CTRL2.ERRMSK_FAST)", 31)
            ;

            Registers.ErrorAndStatus2.Define(this)
                .WithReservedBits(0, 13)
                .WithTaggedFlag("Inactive Message Buffer (ESR2.IMB)", 13)
                .WithTaggedFlag("Valid Priority Status (ESR2.VPS)", 14)
                .WithReservedBits(15, 1)
                .WithTag("Lowest Priority TX Message Buffer (ESR2.LPTM)", 16, 7)
                .WithReservedBits(23, 9)
            ;

            Registers.CyclicRedundancyCheck.Define(this)
                .WithTag("Transmitted CRC value (CRCR.TXCRC)", 0, 15)
                .WithReservedBits(15, 1)
                .WithTag("CRC Message Buffer (CRCR.MBCRC)", 16, 7)
                .WithReservedBits(23, 9)
            ;

            Registers.LegacyRxFifoGlobalMask.Define(this)
                .WithTag("Legacy RX FIFO Global Mask Bits (RXFGMASK.FGM)", 0, 32)
            ;

            Registers.LegacyRxFifoInformation.Define(this)
                .WithValueField(0, 9, name: "Identifier Acceptance Filter Hit Indicator (RXFIR.IDHIT)",
                    valueProviderCallback: _ => legacyRxFifo.Count > 0 ? (ulong)legacyRxFifo.Peek().Key : 0)
                .WithReservedBits(9, 23)
            ;

            Registers.CanBitTiming.Define(this, softResettable: false)
                // NOTE: Zephyr requires this for initialization
                .WithValueField(0, 5, name: "Extended Phase Segment 2 (CBT.EPSEG2)")
                .WithValueField(5, 5, name: "Extended Phase Segment 1 (CBT.EPSEG1)")
                .WithValueField(10, 6, name: "Extended Propagation Segment (CBT.EPROPSEG)")
                .WithValueField(16, 5, name: "Extended Resync Jump Width (CBT.ERJW)")
                .WithValueField(21, 10, name: "Extended Prescaler Division Factor (CBT.EPRESDIV)")
                .WithFlag(31, name: "Bit Timing Format Enable (CBT.BTF)")
            ;

            if(numberOfMessageBuffers > 64)
            {
                var interruptMask3 = Registers.InterruptMasks3.Define(this);
                var interruptFlag3 = Registers.InterruptFlags3.Define(this);
                RegisterMessageBufferInterruptFlags(interruptFlag3, interruptMask3, 64);
            }

            Registers.ReceiveIndividualMask.Define32Many(this, numberOfMessageBuffers, (register, index) =>
            {
                register
                    .WithValueField(0, 32, out individualMaskBits[index], name: $"Individual Mask Bits (RXIMR{index}.MI)")
                ;
            });

            Registers.MemoryErrorControl.Define(this, 0x800C0080)
                .WithReservedBits(0, 7)
                .WithTaggedFlag("Non-Correctable Errors in FlexCAN Access Put Device in Freeze Mode (MECR.NCEFAFRZ)", 7)
                .WithTaggedFlag("Error Correction Disable (MECR.ECCDIS)", 8)
                .WithTaggedFlag("Error Report Disable (MECR.RERRDIS)", 9)
                .WithReservedBits(10, 3)
                .WithTaggedFlag("Extended Error Injection Enable (MECR.EXTERRIE)", 13)
                .WithTaggedFlag("FlexCAN Access Error Injection Enable (MECR.FAERRIE)", 14)
                .WithTaggedFlag("Host Access Error Injection Enable (MECR.HAERRIE)", 15)
                .WithTaggedFlag("Correctable Errors Interrupt Mask (MECR.CEI_MSK)", 16)
                .WithReservedBits(17, 1)
                .WithTaggedFlag("FlexCAN Access with Non-Correctable Errors Interrupt Mask (MECR.FANCEI_MSK)", 18)
                .WithTaggedFlag("Host Access with Non-Correctable Errors Interrupt Mask (MECR.HANCEI_MSK)", 19)
                .WithReservedBits(20, 11)
                .WithTaggedFlag("Error Configuration Register Write Disable (MECR.ECRWRDIS)", 31)
            ;

            Registers.ErrorInjectionAddress.Define(this)
                .WithTag("Error Injection Address Low (ERRIAR.INJADDR_L)", 0, 2) // RO
                .WithTag("Error Injection Address High (ERRIAR.INJADDR_H)", 2, 12)
                .WithReservedBits(14, 18)
            ;

            Registers.ErrorInjectionDataPattern.Define(this)
                .WithTag("Data Flip Pattern (ERRIDPR.DFLIP)", 0, 32)
            ;

            Registers.ErrorInjectionParityPattern.Define(this)
                .WithTag("Parity Flip Pattern for Byte 0 (Least Significant) (ERRIPPR.PFLIP0)", 0, 5)
                .WithReservedBits(5, 3)
                .WithTag("Parity Flip Pattern for Byte 1 (ERRIPPR.PFLIP1)", 8, 5)
                .WithReservedBits(13, 3)
                .WithTag("Parity Flip Pattern for Byte 2 (ERRIPPR.PFLIP2)", 16, 5)
                .WithReservedBits(21, 3)
                .WithTag("Parity Flip Pattern for Byte 3 (Most Significant) (ERRIPPR.PFLIP3)", 24, 5)
                .WithReservedBits(29, 3)
            ;

            Registers.ErrorReportAddress.Define(this)
                .WithTag("Address Where Error Detected (RERRAR.ERRADDR)", 0, 14) // RO
                .WithReservedBits(14, 2)
                .WithTag("SAID (RERRAR.SAID)", 16, 3) // RO
                .WithReservedBits(19, 5)
                .WithTaggedFlag("Non-Correctable Error (RERRAR.NCE)", 24) // RO
                .WithReservedBits(25, 7)
            ;

            Registers.ErrorReportData.Define(this)
                .WithTag("Raw Data Word Read from Memory with Error (RERRDR.RDATA)", 0, 32) // RO
            ;

            Registers.ErrorReportSyndrome.Define(this)
                .WithTag("Error Syndrome for Byte 0 (Least Significant) (RERRSYNR.SYND0)", 0, 5)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("Byte Enabled for Byte 0 (Least Significant) (RERRSYNR.BE0)", 7)
                .WithTag("Error Syndrome for Byte 1 (RERRSYNR.SYND1)", 8, 5)
                .WithReservedBits(13, 2)
                .WithTaggedFlag("Byte Enabled for Byte 1 (RERRSYNR.BE1)", 15)
                .WithTag("Error Syndrome for Byte 2 (RERRSYNR.SYND2)", 16, 5)
                .WithReservedBits(21, 2)
                .WithTaggedFlag("Byte Enabled for Byte 2 (RERRSYNR.BE2)", 23)
                .WithTag("Error Syndrome for Byte 3 (Most Significant) (RERRSYNR.SYND3)", 24, 5)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("Byte Enabled for Byte 3 (Most Significant) (RERRSYNR.BE3)", 31)
            ;

            Registers.ErrorStatus.Define(this)
                .WithTaggedFlag("Correctable Error Interrupt Overrun Flag (ERRSR.CEIOF)", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("FlexCAN Access with Non-Correctable Error Interrupt Overrun Flag (ERRSR.FANCEIOF)", 2)
                .WithTaggedFlag("Host Access With Non-Correctable Error Interrupt Overrun Flag (ERRSR.HANCEIOF)", 3)
                .WithReservedBits(4, 12)
                .WithTaggedFlag("Correctable Error Interrupt Flag (ERRSR.CEIF)", 16)
                .WithReservedBits(17, 1)
                .WithTaggedFlag("FlexCAN Access with Non-Correctable Error Interrupt Flag (ERRSR.FANCEIF)", 18)
                .WithTaggedFlag("Host Access with Noncorrectable Error Interrupt Flag (ERRSR.HANCEIF)", 19)
                .WithReservedBits(20, 12)
            ;

            Registers.EnhancedCanBitTimingPrescalers.Define(this, softResettable: false)
                .WithTag("Extended Nominal Prescaler Division Factor (EPRS.ENPRESDIV)", 0, 10)
                .WithReservedBits(10, 6)
                .WithTag("Extended Data Phase Prescaler Division Factor (EPRS.EDPRESDIV)", 16, 10)
                .WithReservedBits(26, 6)
            ;

            Registers.EnhancedNominalCanBitTiming.Define(this, softResettable: false)
                .WithTag("Nominal Time Segment 1 (ENCBT.NTSEG1)", 0, 8)
                .WithReservedBits(8, 4)
                .WithTag("Nominal Time Segment 2 (ENCBT.NTSEG2)", 12, 7)
                .WithReservedBits(19, 3)
                .WithTag("Nominal Resynchronization Jump Width (ENCBT.NRJW)", 22, 7)
                .WithReservedBits(29, 3)
            ;

            Registers.EnhancedDataPhaseCanBitTiming.Define(this, softResettable: false)
                .WithTag("Data Phase Segment 1 (EDCBT.DTSEG1)", 0, 5)
                .WithReservedBits(5, 7)
                .WithTag("Data Phase Time Segment 2 (EDCBT.DTSEG2)", 12, 4)
                .WithReservedBits(16, 6)
                .WithTag("Data Phase Resynchronization Jump Width (EDCBT.DRJW)", 22, 4)
                .WithReservedBits(26, 6)
            ;

            Registers.EnhancedTransceiverDelayCompensation.Define(this)
                .WithTag("Enhanced Transceiver Delay Compensation Value (ETDC.ETDCVAL)", 0, 8) // RO
                .WithReservedBits(8, 7)
                .WithTaggedFlag("Transceiver Delay Compensation Fail (ETDC.ETDCFAIL)", 15) // W1C
                .WithTag("Enhanced Transceiver Delay Compensation Offset (ETDC.ETDCOFF)", 16, 7)
                .WithReservedBits(23, 7)
                .WithTaggedFlag("Transceiver Delay Measurement Disable (ETDC.TDMDIS)", 30)
                .WithTaggedFlag("Transceiver Delay Compensation Enable (ETDC.ETDCEN)", 31)
            ;

            Registers.CanFdControl.Define(this, 0x80000100, false)
                .WithTag("Transceiver Delay Compensation Value (FDCTRL.TDCVAL)", 0, 6)
                .WithReservedBits(6, 2)
                .WithTag("Transceiver Delay Compensation Offset (FDCTRL.TDCOFF)", 8, 5)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("Transceiver Delay Compensation Fail (FDCTRL.TDCFAIL)", 14)
                .WithTaggedFlag("Transceiver Delay Compensation Enable (FDCTRL.TDCEN)", 15)
                .WithEnumField(16, 2, out messageBufferSize[0], name: "Message Buffer Data Size for Region 0 (FDCTRL.MBDSR0)",
                    changeCallback: GetFreezeModeOnlyWritableChangeCallback(messageBufferSize[0], "FDCTRL.MBDSR0"))
                .WithReservedBits(18, 1)
                .WithEnumField(19, 2, out messageBufferSize[1], name: "Message Buffer Data Size for Region 1 (FDCTRL.MBDSR1)",
                    changeCallback: GetFreezeModeOnlyWritableChangeCallback(messageBufferSize[1], "FDCTRL.MBDSR1"))
                .WithReservedBits(21, 1)
                .WithEnumField(22, 2, out messageBufferSize[2], name: "Message Buffer Data Size for Region 2 (FDCTRL.MBDSR2)",
                    valueProviderCallback: _ => numberOfMessageBuffers <= 64 ? 0 : messageBufferSize[2].Value,
                    changeCallback: (previousValue, value) =>
                    {
                        if(numberOfMessageBuffers <= 64)
                        {
                            this.Log(LogLevel.Warning, "This peripheral configuration doesn't allow to modify FDCTRL.MBDSR2 field, ignoring");
                            return;
                        }
                        GetFreezeModeOnlyWritableChangeCallback(messageBufferSize[2], "FDCTRL.MBDSR2")(previousValue, value);
                    })
                .WithReservedBits(24, 7)
                .WithTaggedFlag("Bit Rate Switch Enable (FDCTRL.FDRATE)", 31)
            ;

            Registers.CanFdBitTiming.Define(this, softResettable: false)
                .WithTag("Fast Phase Segment 2 (FDCBT.FPSEG2)", 0, 3)
                .WithReservedBits(3, 2)
                .WithTag("Fast Phase Segment 1 (FDCBT.FPSEG1)", 5, 3)
                .WithReservedBits(8, 2)
                .WithTag("Fast Propagation Segment (FDCBT.FPROPSEG)", 10, 5)
                .WithReservedBits(15, 1)
                .WithTag("Fast Resync Jump Width (FDCBT.FRJW)", 16, 3)
                .WithReservedBits(19, 1)
                .WithTag("Fast Prescaler Division Factor (FDCBT.FPRESDIV)", 20, 10)
                .WithReservedBits(30, 2)
            ;

            Registers.CanFdCrc.Define(this)
                .WithTag("Extended Transmitted CRC value (FDCRC.FD_TXCRC)", 0, 21)
                .WithReservedBits(21, 3)
                .WithTag("CRC Message Buffer Number for FD_TXCRC (FDCRC.FD_MBCRC)", 24, 7)
                .WithReservedBits(31, 1)
            ;

            if(enhancedRxFifoSize != 0)
            {
                Registers.EnhancedRxFifoControl.Define(this)
                    .WithTag("Enhanced RX FIFO Watermark (ERFCR.ERFWM)", 0, 5)
                    .WithReservedBits(5, 3)
                    .WithTag("Number of Enhanced RX FIFO Filter Elements (ERFCR.NFE)", 8, 6)
                    .WithReservedBits(14, 2)
                    .WithTag("Number of Extended ID Filter Elements (ERFCR.NEXIF)", 16, 7)
                    .WithReservedBits(23, 3)
                    .WithTag("DMA Last Word (ERFCR.DMALW)", 26, 5)
                    .WithTaggedFlag("Enhanced RX FIFO enable (ERFCR.ERFEN)", 31)
                ;

                Registers.EnhancedRxFifoInterruptEnable.Define(this)
                    .WithReservedBits(0, 28)
                    .WithTaggedFlag("Enhanced RX FIFO Data Available Interrupt Enable (ERFIER.ERFDAIE)", 28)
                    .WithTaggedFlag("Enhanced RX FIFO Watermark Indication Interrupt Enable (ERFIER.ERFWMIIE)", 29)
                    .WithTaggedFlag("Enhanced RX FIFO Overflow Interrupt Enable (ERFIER.ERFOVFIE)", 30)
                    .WithTaggedFlag("Enhanced RX FIFO Underflow Interrupt Enable (ERFIER.ERFUFWIE)", 31)
                ;

                Registers.EnhancedRxFifoStatus.Define(this)
                    .WithTag("Enhanced RX FIFO Elements (ERFSR.ERFEL)", 0, 6)
                    .WithReservedBits(6, 8)
                    .WithTaggedFlag("Enhanced RX FIFO Full Flag (ERFSR.ERFF)", 16)
                    .WithTaggedFlag("Enhanced RX FIFO Empty Flag (ERFSR.ERFE)", 17)
                    .WithReservedBits(18, 9)
                    .WithTaggedFlag("Enhanced RX FIFO Clear (ERFSR.ERFCLR)", 27)
                    .WithTaggedFlag("Enhanced RX FIFO Data Available Flag (ERFSR.ERFDA)", 28)
                    .WithTaggedFlag("Enhanced RX FIFO Watermark Indication Flag (ERFSR.ERFWMI)", 29)
                    .WithTaggedFlag("Enhanced RX FIFO Overflow Flag (ERFSR.ERFOVF)", 30)
                    .WithTaggedFlag("Enhanced RX FIFO Underflow Flag (ERFSR.ERFUFW)", 31)
                ;
            }

            Registers.HighResolutionTimestamp.Define32Many(this, numberOfMessageBuffers, (register, index) =>
            {
                register
                    .WithTag($"High-Resolution Timestamp (HR_TIME_STAMP{index}.TS)", 0, 32)
                ;
            });

            Registers.EnhancedRxFifoFilterElement.Define32Many(this, enhancedRxFifoSize != 0 ? 128U : 0U, (register, index) =>
            {
                register
                    .WithTag($"Filter Element Bits (ERFFEL{index}.FEL)", 0, 32)
                ;
            });
        }

        private IEnumerable<ILegacyRxFifoMatcher> LegacyRxFifoFilterMacherIterator =>
            Enumerable.Range(0, (int)numberOfLegacyRxFifoFilters.Value).Select<int, ILegacyRxFifoMatcher>(index =>
            {
                switch(legacyFilterFormat.Value)
                {
                    case LegacyFilterFormat.A:
                        return LegacyRxFifoFilterAStructure.Fetch(messageBuffers, LegacyRxFifoFiltersOffset + index * LegacyRxFifoFilterSize);
                    case LegacyFilterFormat.B:
                        return LegacyRxFifoFilterBStructure.Fetch(messageBuffers, LegacyRxFifoFiltersOffset + index * LegacyRxFifoFilterSize);
                    case LegacyFilterFormat.C:
                        return LegacyRxFifoFilterCStructure.Fetch(messageBuffers, LegacyRxFifoFiltersOffset + index * LegacyRxFifoFilterSize);
                    default:
                        return null;
                }
            });

        private Action<T, T> GetFreezeModeOnlyWritableChangeCallback<T>(IRegisterField<T> field, string fieldName)
        {
            return (previousValue, _) =>
            {
                if(!freezeEnable.Value)
                {
                    this.Log(LogLevel.Debug, "Write to {0} blocked, not in Freeze mode", fieldName);
                    field.Value = previousValue;
                }
            };
        }

        private void RegisterMessageBufferInterruptFlags(DoubleWordRegister flagRegister, DoubleWordRegister maskRegister, int start, int fieldStart = 0, int fieldMax = 32)
        {
            var flags = (int)Math.Min(fieldMax, numberOfMessageBuffers - fieldStart);
            if(fieldMax - flags > 0)
            {
                flagRegister.WithReservedBits(flags, fieldMax - flags);
                maskRegister.WithReservedBits(flags, fieldMax - flags);
            }

            flagRegister.WithChangeCallback((_, __) => UpdateInterrupts());
            maskRegister.WithChangeCallback((_, __) => UpdateInterrupts());

            for(var i = 0; i < flags; ++i)
            {
                var index = i + start;

                maskRegister
                    .WithFlag(i + fieldStart, out messageBufferInterruptEnable[index], name: $"Buffer MB{index} Mask (IMASK{index / 32}.BUF{index}M)");

                flagRegister
                    .WithFlag(i + fieldStart, out messageBufferInterrupt[index], FieldMode.WriteOneToClear | FieldMode.Read, name: $"Buffer MB{index} Interrupt (IFLAG{index / 32}.BUF{index}I)");
            }
        }

        private void SendFrame(CANMessageFrame frame)
        {
            this.Log(LogLevel.Noisy, "Transmitted frame: {0}", frame);
            if(listenOnly.Value)
            {
                this.Log(LogLevel.Debug, "Transmission is disabled, listen-only enabled");
                return;
            }

            if(!loopback.Value)
            {
                FrameSent?.Invoke(frame);
                this.Log(LogLevel.Debug, "Transmission succeeded");
            }

            if(!selfReceptionDisable.Value)
            {
                this.Log(LogLevel.Debug, "Transmission loopbacked");
                OnFrameReceivedInner(frame);
            }
        }

        private IEnumerable<ulong> MessageBufferOffsetsIteratorForRegion(int regionIndex) =>
            Enumerable.Range(0, MessageBufferRegionSize / (int)GetMessageBufferSizeByRegion(regionIndex))
                .Select(index => (ulong)(MessageBufferRegionSize * regionIndex + index * GetMessageBufferSizeByRegion(regionIndex)));

        private IEnumerable<MessageBufferIteratorEntry> MessageBuffersIteratorForRegion(int regionIndex) =>
            MessageBufferOffsetsIteratorForRegion(regionIndex)
                .Where(offset => offset < messageBufferRange.Size)
                .Select(offset => new MessageBufferIteratorEntry(offset, regionIndex, MessageBufferStructure.FetchMetadata(messageBuffers, (ulong)offset)));

        private IEnumerable<MessageBufferIteratorEntry> MessageBuffersIterator =>
            MessageBuffersIteratorForRegion(0)
                .Concat(MessageBuffersIteratorForRegion(1))
                .Concat(MessageBuffersIteratorForRegion(2))
        ;

        private IEnumerable<ulong> MessageBufferOffsetsIterator =>
            MessageBufferOffsetsIteratorForRegion(0)
                .Concat(MessageBufferOffsetsIteratorForRegion(1))
                .Concat(MessageBufferOffsetsIteratorForRegion(2));

        private uint GetMessageBufferOffsetByIndex(int messageBufferIndex)
        {
            var currentRegion = 0U;
            var currentOffset = 0U;
            for(var i = 0; i < MessageBufferRegionsCount; ++i)
            {
                var currentRegionSize = GetMessageBufferSizeByRegion(i);
                if(messageBufferIndex * currentRegionSize < MessageBufferRegionSize)
                {
                    return currentOffset + (uint)messageBufferIndex * currentRegionSize;
                }

                currentRegion += 1;
                currentOffset += currentRegionSize;
                messageBufferIndex -= MessageBufferRegionSize / (int)currentRegionSize;
            }
            return 0;
        }

        private int GetMessageBufferIndexByOffset(ulong offset)
        {
            var messageBufferRegion = 0;
            var messageBufferIndex = 0;
            while(offset > MessageBufferRegionSize)
            {
                offset -= MessageBufferRegionSize;
                messageBufferIndex += MessageBufferRegionSize / (int)GetMessageBufferSizeByRegion(messageBufferRegion);
                messageBufferRegion += 1;
            }
            return messageBufferIndex + (int)offset / (int)GetMessageBufferSizeByRegion(messageBufferRegion);
        }

        private uint GetMessageBufferSizeByRegion(int regionIndex)
        {
            if(regionIndex > MessageBufferRegionsCount)
            {
                throw new ArgumentException($"regionIndex should be between 0 and {MessageBufferRegionsCount-1}", "regionIndex");
            }

            switch(messageBufferSize[regionIndex].Value)
            {
                case MessageBufferSize._8bytes:
                    return 8 + 8;
                case MessageBufferSize._16bytes:
                    return 8 + 16;
                case MessageBufferSize._32bytes:
                    return 8 + 32;
                case MessageBufferSize._64bytes:
                    return 8 + 64;
                default:
                    throw new Exception("unreachable");
            }
        }

        private int GetMessageBufferRegionByOffset(ulong offset)
        {
            return (int)(offset / MessageBufferRegionSize);
        }

        private MessageBufferMatcher GetMessageBufferMatcher(int index)
        {
            if(!individualMaskingAndQueue.Value)
            {
                // NOTE: Legacy masking is not currently supported
                return new MessageBufferMatcher(0);
            }

            return new MessageBufferMatcher(individualMaskBits[index].Value);
        }


        private void OnFrameReceivedInner(CANMessageFrame frame)
        {
            Func<CANMessageFrame, bool> firstFrameHandler = OnFrameReceivedToLegacyRxFifoInner;
            Func<CANMessageFrame, bool> secondFrameHandler = OnFrameReceivedToMessageBufferInner;

            if(messageBuffersReceptionPriority.Value)
            {
                // Swap order of handlers
                var temp = firstFrameHandler;
                firstFrameHandler = secondFrameHandler;
                secondFrameHandler = temp;
            }

            if(!firstFrameHandler(frame))
            {
                secondFrameHandler(frame);
            }
        }

        private bool OnFrameReceivedToMessageBufferInner(CANMessageFrame frame)
        {
            var matchedItem = MessageBuffersIterator
                .Select((Entry, Index) => new { Entry, Index })
                .Where(item => item.Entry.MessageBuffer.ReadyForReception && GetMessageBufferMatcher(item.Index).IsMatching(frame, item.Entry.MessageBuffer))
                .OrderBy(item => Tuple.Create(item.Entry.MessageBuffer.messageBufferCode, item.Index))
                .FirstOrDefault();

            if(matchedItem == null)
            {
                this.Log(LogLevel.Debug, "Did not found matching message buffer for rx frame: {0}", frame);
                return false;
            }

            var messageBuffer = matchedItem.Entry.MessageBuffer;
            var messageBufferOffset = matchedItem.Entry.Offset;
            var messageBufferIndex = matchedItem.Index;

            this.Log(LogLevel.Debug, "Found matching message buffer#{0}: {1}", messageBufferIndex, messageBuffer);

            messageBuffer.FillReceivedFrame(messageBuffers, messageBufferOffset, frame);
            messageBufferInterrupt[messageBufferIndex].Value = true;

            return true;
        }

        private bool OnFrameReceivedToLegacyRxFifoInner(CANMessageFrame frame)
        {
            if(!legacyFifoEnable.Value)
            {
                return false;
            }

            var matchedFilter = LegacyRxFifoFilterMacherIterator
                .Select((Filter, Index) => new { Filter, Index })
                .Where(item => item.Filter?.IsMatching(frame) ?? false)
                .FirstOrDefault()?.Index ?? -1;

            if(matchedFilter == -1)
            {
                return false;
            }

            if(legacyRxFifo.Count == LegacyRxFifoSize)
            {
                messageBufferInterrupt[LegacyRxFifoInterruptOverflow].Value = true;
                UpdateInterrupts();
                return false;
            }

            messageBufferInterrupt[LegacyRxFifoInterruptFramesAvailable].Value = true;

            legacyRxFifo.Enqueue(new KeyValuePair<ushort, CANMessageFrame>((ushort)matchedFilter, frame));
            if(legacyRxFifo.Count == 1)
            {
                UpdateLegacyRxFifoMemory();
            }

            messageBufferInterrupt[LegacyRxFifoInterruptWarning].Value = legacyRxFifo.Count == LegacyRxFifoSize - 1;
            UpdateInterrupts();

            return true;
        }

        private void UpdateLegacyRxFifoMemory()
        {
            if(legacyRxFifo.Count == 0)
            {
                return;
            }

            var currentEntry = legacyRxFifo.Peek();
            LegacyRxFifoStructure
                .FromCANFrame(currentEntry.Value, currentEntry.Key)
                .CommitToMemory(messageBuffers, 0);
        }

        private bool TryTransmitFromMessageBuffer(ulong offset)
        {
            if(Freeze || listenOnly.Value)
            {
                return false;
            }

            var messageBuffer = MessageBufferStructure.FetchMetadata(messageBuffers, offset);

            this.Log(LogLevel.Debug, "Loading {0} byte MB from 0x{1:X}", messageBuffer.Size, offset);
            this.Log(LogLevel.Noisy, "Loading MB: {0}", messageBuffer);

            if(!messageBuffer.ReadyForTransmission)
            {
                return false;
            }

            messageBuffer.FetchData(messageBuffers, offset);

            this.Log(LogLevel.Noisy, "Building frame from: {0}", messageBuffer);
            var frame = messageBuffer.ToCANMessageFrame();
            messageBuffer.Finalize(messageBuffers, offset);
            this.Log(LogLevel.Noisy, "Saved frame: {0}", messageBuffer);

            SendFrame(frame);

            var index = GetMessageBufferIndexByOffset(offset);
            messageBufferInterrupt[index].Value = true;

            return true;
        }

        private void RunArbitrationProcess()
        {
            if(Freeze || listenOnly.Value)
            {
                return;
            }

            // NOTE: Ignores prioritization
            var index = 0;
            if(legacyFifoEnable.Value)
            {
                // 80h–DCh legacy fifo (enabled)
                //  80h–8Ch oldest message received
                //  90h–DCh reserved for internal use of legacy fifo engine
                // E0h upto 2DCh depending on CTRL2.RFFN
                //  id filter tabled
                index = 6 + ((int)numberOfLegacyRxFifoFilters.Value + 3) / 4;
            }

            if(lowestBufferTransmittedFirst.Value)
            {
                // If Lowest Buffer Transmitted First is enabled, we can just iterate over offsets
                foreach(var messageBufferOffset in MessageBufferOffsetsIterator.Skip(index).Take((int)lastMessageBufferIndex.Value - index))
                {
                    TryTransmitFromMessageBuffer(messageBufferOffset);
                }
                return;
            }

            // Otherwise we have to actually read Message Buffer headers and sort them by priority
            foreach(var entry in MessageBuffersIterator.Skip(index)
                    .Take((int)lastMessageBufferIndex.Value - index)
                    .OrderBy(entry => -entry.MessageBuffer.Priority))
            {
                TryTransmitFromMessageBuffer(entry.Offset);
            }
        }

        private void UpdateInterrupts()
        {
            var interrupt = Enumerable.Range(0, (int)numberOfMessageBuffers).Any(i => messageBufferInterrupt[i].Value && messageBufferInterruptEnable[i].Value);
            if(interrupt != IRQ.IsSet)
            {
                this.Log(LogLevel.Debug, "IRQ: {0}", interrupt);
            }
            IRQ.Set(interrupt);
        }

        private uint Control2ResetValue => numberOfMessageBuffers > 64 ? 0x00600000U : 0x00800000U;

        private bool Freeze => freezeEnable.Value && halt.Value;

        private IValueRegisterField lastMessageBufferIndex;
        private IEnumRegisterField<LegacyFilterFormat> legacyFilterFormat;
        private IFlagRegisterField selfReceptionDisable;
        private IFlagRegisterField abortEnable;
        private IFlagRegisterField halt;
        private IFlagRegisterField legacyFifoEnable;
        private IFlagRegisterField freezeEnable;
        private IFlagRegisterField moduleDisable;
        private IFlagRegisterField listenOnly;
        private IFlagRegisterField loopback;
        private IValueRegisterField transmissionArbitrationStartDelay;
        private IValueRegisterField numberOfLegacyRxFifoFilters;
        private IFlagRegisterField individualMaskingAndQueue;
        private IFlagRegisterField lowPowerModeAcknowledge;
        private IFlagRegisterField messageBuffersReceptionPriority;
        private IFlagRegisterField lowestBufferTransmittedFirst;

        // classic CAN rx -> legacy fifo or message buffer
        // CAN FD rx -> message buffer or enhanced fifo
        private readonly IFlagRegisterField[] messageBufferInterruptEnable;
        private readonly IFlagRegisterField[] messageBufferInterrupt;
        private readonly IValueRegisterField[] individualMaskBits;
        private readonly IEnumRegisterField<MessageBufferSize>[] messageBufferSize;

        private readonly uint numberOfMessageBuffers;
        private readonly uint enhancedRxFifoSize;
        private readonly Range messageBufferRange;

        private readonly ArrayMemory messageBuffers;
        private readonly Queue<KeyValuePair<ushort, CANMessageFrame>> legacyRxFifo;
        private const int NumberOfLegacyMessageBuffers = 38;
        private const int MessageBufferRegionsCount = 3;
        private const int MessageBufferRegionSize = 0x200;
        private const int LegacyRxFifoSize = 6;
        private const int LegacyRxFifoBufferSize = 16;
        private const int LegacyRxFifoFiltersOffset = 6 * LegacyRxFifoBufferSize;
        private const int LegacyRxFifoFilterSize = 4;
        private const int LegacyRxFifoLastWordOffset = 0xC;

        private const int LegacyRxFifoInterruptFramesAvailable = 5;
        private const int LegacyRxFifoInterruptWarning = 6;
        private const int LegacyRxFifoInterruptOverflow = 7;

        public enum Registers
        {
            ModuleConfiguration = 0x0, // (MCR) D890_000Fh
            Control1 = 0x4, // (CTRL1) 0000_0000h
            FreeRunningTimer = 0x8, // (TIMER) 0000_0000h
            RxMessageBuffersGlobalMask = 0x10, // (RXMGMASK) RW
            Receive14Mask = 0x14, // (RX14MASK) RW
            Receive15Mask = 0x18, // (RX15MASK) RW
            ErrorCounter = 0x1C, // (ECR) 0000_0000h
            ErrorAndStatus1 = 0x20, // (ESR1) 0000_0000h
            InterruptMasks2 = 0x24, // (IMASK2) RW 0000_0000h
            InterruptMasks1 = 0x28, // (IMASK1) RW 0000_0000h
            InterruptFlags2 = 0x2C, // (IFLAG2) 0000_0000h
            InterruptFlags1 = 0x30, // (IFLAG1) 0000_0000h
            Control2 = 0x34, // (CTRL2) 32
            ErrorAndStatus2 = 0x38, // (ESR2) 0000_0000h
            CyclicRedundancyCheck = 0x44, // (CRCR) 0000_0000h
            LegacyRxFifoGlobalMask = 0x48, // (RXFGMASK) RW
            LegacyRxFifoInformation = 0x4C, // (RXFIR) 32
            CanBitTiming = 0x50, // (CBT) RW 0000_0000h
            InterruptMasks3 = 0x6C, // (IMASK3) RW 0000_0000h
            InterruptFlags3 = 0x74, // (IFLAG3) 0000_0000h
            MessageBuffer = 0x80, // - 0x67F 96 128-bit message buffers
            ReceiveIndividualMask = 0x880, // - 0x9FC (RXIMR0 - RXIMR95) RW
            MemoryErrorControl = 0xAE0, // (MECR) 800C_0080h
            ErrorInjectionAddress = 0xAE4, // (ERRIAR) 0000_0000h
            ErrorInjectionDataPattern = 0xAE8, // (ERRIDPR) RW 0000_0000h
            ErrorInjectionParityPattern = 0xAEC, // (ERRIPPR) 0000_0000h
            ErrorReportAddress = 0xAF0, // (RERRAR) 0000_0000h
            ErrorReportData = 0xAF4, // (RERRDR) RO 0000_0000h
            ErrorReportSyndrome = 0xAF8, // (RERRSYNR) 0000_0000h
            ErrorStatus = 0xAFC, // (ERRSR) 0000_0000h
            EnhancedCanBitTimingPrescalers = 0xBF0, // (EPRS) 0000_0000h
            EnhancedNominalCanBitTiming = 0xBF4, // (ENCBT) 0000_0000h
            EnhancedDataPhaseCanBitTiming = 0xBF8, // (EDCBT) 0000_0000h
            EnhancedTransceiverDelayCompensation = 0xBFC, // (ETDC) 0000_0000h
            CanFdControl = 0xC00, // (FDCTRL) 8000_0100h
            CanFdBitTiming = 0xC04, // (FDCBT) 0000_0000h
            CanFdCrc = 0xC08, // (FDCRC) 0000_0000h
            EnhancedRxFifoControl = 0xC0C, // (ERFCR) 0000_0000h
            EnhancedRxFifoInterruptEnable = 0xC10, // (ERFIER) 0000_0000h
            EnhancedRxFifoStatus = 0xC14, // (ERFSR) 0000_0000h
            HighResolutionTimestamp = 0xC30, // - 0xDAC (HR_TIME_STAMP0 - HR_TIME_STAMP95) RW
            EnhancedRxFifo = 0x2000,
            EnhancedRxFifoFilterElement = 0x3000 // - 0x31FC (ERFFEL0 - ERFFEL127) RW
        }
    }
}
