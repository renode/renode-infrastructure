//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class Arm_Dma350 : BasicDoubleWordPeripheral, IKnownSize
    {
        public Arm_Dma350(IMachine machine, int numberOfChannels = 8) : this(machine, numberOfChannels, Variant.Dma350)
        {
        }

        public override void Reset()
        {
            base.Reset();
            UpdateInterrupts();
        }

        public long Size => Misc.AlignUpToMultipleOf(ChannelFrameOffset + numberOfChannels * ChannelFrameStride, RegisterFrameSize);

        public GPIO IRQ { get; } = new GPIO();

        protected Arm_Dma350(IMachine machine, int numberOfChannels, Variant variant) : base(machine)
        {
            if(!Enum.IsDefined<Variant>(variant))
            {
                throw new ConstructionException($"Invalid '{nameof(variant)}' value");
            }
            if(!IsNumberOfChannelsValid(variant, numberOfChannels))
            {
                var message = variant == Variant.Dma250
                    ? "Arm Dma 250 supports 2, 4, 8, 16, or 32 virtual channels"
                    : $"Arm Dma 350 supports between {MinimumDMA350Channels} and {MaximumDMA350Channels} channels";
                throw new ConstructionException(message);
            }

            this.variant = variant;
            this.numberOfChannels = numberOfChannels;
            // Channels must be initialized exactly once because their fields hold references
            // to register framework values created during register definition.
            channels = Misc.Iterate(() => new Channel(this)).Take(numberOfChannels).ToArray();
            DefineRegisters();
            Reset();
        }

        private static IEnumerable<ulong> EnumerateTransferAddresses(
            ulong startAddress, ulong xSize, ulong ySize, long xIncrement, long yIncrement)
        {
            for(long yShift = 0, yi = 0; yi < (long)ySize; yi += 1, yShift += yIncrement)
            {
                for(long xShift = 0, xi = 0; xi < (long)xSize; xi += 1, xShift += xIncrement)
                {
                    ulong address;
                    try
                    {
                        address = AddSignedOffsetOrThrow(startAddress, checked(xShift + yShift));
                    }
                    catch(OverflowException)
                    {
                        address = unchecked(startAddress + (ulong)yShift + (ulong)xShift);
                        Logger.Warning("DMA address calculation overflowed the 64-bit address space");
                    }
                    yield return address;
                }
            }
        }

        private static ulong AddSignedOffsetOrThrow(ulong address, long offset)
        {
            if(offset >= 0)
            {
                return checked(address + (ulong)offset);
            }
            var abs = unchecked((ulong)-offset);
            return checked(address - abs);
        }

        private static bool IsNumberOfChannelsValid(Variant variant, int numberOfChannels)
        {
            if(variant == Variant.Dma350)
            {
                return numberOfChannels >= MinimumDMA350Channels && numberOfChannels <= MaximumDMA350Channels;
            }

            return Misc.IsPowerOfTwo((ulong)numberOfChannels) &&
                numberOfChannels >= MinimumDMA250Channels &&
                numberOfChannels <= MaximumDMA250Channels;
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(channels.Any(channel => channel.InterruptPending));
        }

        private void DefineRegisters()
        {
            DefineSecurityConfigurationFrame();
            DefineControlFrame(secure: true);
            DefineControlFrame(secure: false);
            DefineInformationFrame();
            DefineChannelRegisters();
        }

        private void DefineSecurityConfigurationFrame()
        {
            Registers.SecurityChannels.Define(this)
                .WithTag("SCFGCHSEC0", 0, numberOfChannels)
                .WithReservedBits(numberOfChannels, MaximumDMA250Channels - numberOfChannels)
            ;
            if(variant == Variant.Dma350)
            {
                Registers.SecurityTriggerInputs.Define(this)
                    .WithTag("SCFGTRIGINSEC0", 0, 32)
                ;
                Registers.SecurityTriggerOutputs.Define(this)
                    .WithTag("SCFGTRIGOUTSEC0", 0, 32)
                ;
            }
            Registers.SecurityControl.Define(this)
                .WithTaggedFlag("INTREN_SECACCVIO", 0)
                .WithTaggedFlag("RSPTYPE_SECACCVIO", 1)
                .WithReservedBits(2, 29)
                .WithTaggedFlag("SEC_CFG_LCK", 31)
            ;
            Registers.SecurityInterruptStatus.Define(this)
                .WithTaggedFlag("INTR_SECACCVIO", 0)
                .WithReservedBits(1, 15)
                .WithTaggedFlag("STAT_SECACCVIO", 16)
                .WithReservedBits(17, 15)
            ;
        }

        private void DefineControlFrame(bool secure)
        {
            Registers Map(Registers secureRegister)
            {
                return secure ? secureRegister : ControlFrameRegistersMap.GetValueOrDefault(secureRegister);
            }

            string Prefix(string rawMessage)
            {
                var prefix = secure ? "SEC" : "NSEC";
                return $"{prefix}_{rawMessage}";
            }

            Map(Registers.SecureChannelInterruptStatus).Define(this)
                .WithTag(Prefix("CHINTRSTATUS0"), 0, numberOfChannels)
                .WithReservedBits(numberOfChannels, MaximumDMA250Channels - numberOfChannels)
            ;
            Map(Registers.SecureStatus).Define(this)
                .WithTaggedFlag(Prefix("INTR_ANYCHINTR"), 0)
                .WithTaggedFlag(Prefix("INTR_ALLCHIDLE"), 1)
                .WithTaggedFlag(Prefix("INTR_ALLCHSTOPPED"), 2)
                .WithTaggedFlag(Prefix("INTR_ALLCHPAUSED"), 3)
                .WithReservedBits(4, 13)
                .WithTaggedFlag(Prefix("STAT_ALLCHIDLE"), 17)
                .WithTaggedFlag(Prefix("STAT_ALLCHSTOPPED"), 18)
                .WithTaggedFlag(Prefix("STAT_ALLCHPAUSED"), 19)
                .If(secure && variant == Variant.Dma250)
                    .Then(r => r.WithTaggedFlag(Prefix("STAT_CTXERR"), 20))
                    .Else(r => r.WithReservedBits(20, 1))
                .WithReservedBits(21, 11)
            ;
            Map(Registers.SecureControl).Define(this)
                .WithTaggedFlag(Prefix("INTREN_ANYCHINTR"), 0)
                .WithTaggedFlag(Prefix("INTREN_ALLCHIDLE"), 1)
                .WithTaggedFlag(Prefix("INTREN_ALLCHSTOPPED"), 2)
                .WithTaggedFlag(Prefix("INTREN_ALLCHPAUSED"), 3)
                .If(variant == Variant.Dma250)
                    .Then(r => r.WithTaggedFlag(Prefix("INTREN_CTXERR"), 4))
                    .Else(r => r.WithReservedBits(4, 1))
                .WithReservedBits(5, 3)
                .WithTaggedFlag(Prefix("ALLCHSTOP"), 8)
                .WithTaggedFlag(Prefix("ALLCHPAUSE"), 9)
                .WithReservedBits(10, 17)
                .WithTaggedFlag(Prefix("DBGHALTNSRO"), 27)
                .WithTaggedFlag(Prefix("DBGHALTEN"), 28)
                .WithTaggedFlag(Prefix("IDLERETEN"), 29)
                .WithTag(Prefix("DISMINPWR"), 30, 2)
            ;
            if(variant == Variant.Dma250)
            {
                Map(Registers.SecureContextBase).Define(this)
                    .WithTag(Prefix("CNTXBASE"), 0, 32)
                ;
            }
            Map(Registers.SecureChannelPointer).Define(this)
                .WithTag(Prefix("CHPTR"), 0, 6)
                .WithReservedBits(6, 26)
            ;
            Map(Registers.SecureChannelConfiguration).Define(this)
                .WithTag(Prefix("CHID"), 0, 16)
                .WithTaggedFlag(Prefix("CHIDVLD"), 16)
                .WithTaggedFlag(Prefix("CHPRIV"), 17)
                .WithReservedBits(18, 14)
            ;
            Map(Registers.SecureStatusPointer).Define(this)
                .WithTag(Prefix("STATUSPTR"), 0, 4)
                .WithReservedBits(4, 28)
            ;
            Map(Registers.SecureStatusValue).Define(this)
                .WithTag(Prefix("STATUSVAL"), 0, 32)
            ;
            Map(Registers.SecureSignalPointer).Define(this)
                .WithTag(Prefix("SIGNALPTR"), 0, 4)
                .WithReservedBits(4, 28)
            ;
            Map(Registers.SecureSignalValue).Define(this)
                .WithTag(Prefix("SIGNALVAL"), 0, 32)
            ;
        }

        private void DefineInformationFrame()
        {
            var buildConfiguration0 = (uint)((DataWidthEncoding << 16)
                | ((AddressWidth - 1) << 10)
                | ((numberOfChannels - 1) << 4));
            var buildConfiguration1 = 1u << 16;
            var peripheralSize = (uint)numberOfChannels.DivCeil(ChannelsPerPage);

            Registers.BuildConfiguration0.Define(this, buildConfiguration0)
                .WithTag("FRAMETYPE", 0, 3)
                .WithReservedBits(3, 1)
                .WithTag("NUM_CHANNELS", 4, 6)
                .WithTag("ADDR_WIDTH", 10, 6)
                .WithTag("DATA_WIDTH", 16, 3)
                .WithReservedBits(19, 1)
                .WithTag("CHID_WIDTH", 20, 5)
                .WithReservedBits(25, 7)
            ;
            Registers.BuildConfiguration1.Define(this, buildConfiguration1)
                .WithTag("NUM_TRIGGER_IN", 0, 9)
                .WithTag("NUM_TRIGGER_OUT", 9, 7)
                .WithTaggedFlag("HAS_TRIGSEL", 16)
                .WithReservedBits(17, 15)
            ;
            Registers.BuildConfiguration2.Define(this,
                variant == Variant.Dma250 ? 0x700u : 0x300u)
                .WithReservedBits(0, 7)
                .WithTaggedFlag("HAS_GPOSEL", 7)
                .WithTaggedFlag("HAS_TZ", 8)
                .WithTaggedFlag("HAS_RET", 9)
                .If(variant == Variant.Dma250)
                    .Then(r => r
                        .WithTaggedFlag("HAS_VCH", 10)
                        .WithReservedBits(11, 21))
                    .Else(r => r.WithReservedBits(10, 22))
            ;
            Registers.ImplementationIdentification.Define(this,
                variant == Variant.Dma250 ? 0x2500043Bu : 0x3A00043Bu)
                .WithTag("IMPLEMENTER", 0, 12)
                .WithTag("REVISION", 12, 4)
                .WithTag("VARIANT", 16, 4)
                .WithTag("PRODUCTID", 20, 12)
            ;
            Registers.ArchitectureIdentification.Define(this,
                variant == Variant.Dma250 ? 0x1u : 0x0u)
                .WithTag("ARCH_MINOR_REV", 0, 4)
                .WithTag("ARCH_MAJOR_REV", 4, 4)
                .WithReservedBits(8, 24)
            ;
            Registers.PeripheralIdentification4.Define(this, (peripheralSize << 4) | 0x4u)
                .WithTag("DES_2", 0, 4)
                .WithTag("SIZE", 4, 4)
                .WithReservedBits(8, 24)
            ;
            Registers.PeripheralIdentification0.Define(this, variant == Variant.Dma250 ? 0x50u : 0xA0u)
                .WithTag("PART_0", 0, 8)
                .WithReservedBits(8, 24)
            ;
            Registers.PeripheralIdentification1.Define(this, variant == Variant.Dma250 ? 0xB2u : 0xB3u)
                .WithTag("PART_1", 0, 4)
                .WithTag("DES_0", 4, 4)
                .WithReservedBits(8, 24)
            ;
            Registers.PeripheralIdentification2.Define(this, 0x0B)
                .WithTag("DES_1", 0, 3)
                .WithTaggedFlag("JEDEC", 3)
                .WithTag("REVISION", 4, 4)
                .WithReservedBits(8, 24)
            ;
            Registers.PeripheralIdentification3.Define(this)
                .WithTag("CMOD", 0, 4)
                .WithTag("REVAND", 4, 4)
                .WithReservedBits(8, 24)
            ;
            Registers.ComponentIdentification0.Define(this, 0x0D)
                .WithTag("PRMBL_0", 0, 8)
                .WithReservedBits(8, 24)
            ;
            Registers.ComponentIdentification1.Define(this, 0xF0)
                .WithTag("PRMBL_1", 0, 4)
                .WithTag("CLASS", 4, 4)
                .WithReservedBits(8, 24)
            ;
            Registers.ComponentIdentification2.Define(this, 0x05)
                .WithTag("PRMBL_2", 0, 8)
                .WithReservedBits(8, 24)
            ;
            Registers.ComponentIdentification3.Define(this, 0xB1)
                .WithTag("PRMBL_3", 0, 8)
                .WithReservedBits(8, 24)
            ;
        }

        private void DefineChannelRegisters()
        {
            Registers.ChannelCommand.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) => register
                .WithFlag(0, name: "ENABLECMD",
                    valueProviderCallback: _ => false,
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            channels[index].PerformSingleTransfer();
                        }
                    })
                .WithTaggedFlag("CLEARCMD", 1)
                .WithTaggedFlag("DISABLECMD", 2)
                .WithTaggedFlag("STOPCMD", 3)
                .WithTaggedFlag("PAUSECMD", 4)
                .WithTaggedFlag("RESUMECMD", 5)
                .WithReservedBits(6, 10)
                .WithTaggedFlag("SRCSWTRIGINREQ", 16)
                .WithTag("SRCSWTRIGINTYPE", 17, 2)
                .WithReservedBits(19, 1)
                .WithTaggedFlag("DESSWTRIGINREQ", 20)
                .WithTag("DESSWTRIGINTYPE", 21, 2)
                .WithReservedBits(23, 1)
                .WithTaggedFlag("SWTRIGOUTACK", 24)
                .WithReservedBits(25, 7))
            ;
            Registers.ChannelStatus.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) => register
                .WithFlag(0, mode: FieldMode.Read, name: "INTR_DONE",
                    valueProviderCallback: _ => channels[index].EnableDoneField?.Value == true && channels[index].StatusDoneField?.Value == true)
                .WithTaggedFlag("INTR_ERR", 1)
                .WithTaggedFlag("INTR_DISABLED", 2)
                .WithTaggedFlag("INTR_STOPPED", 3)
                .WithReservedBits(4, 4)
                .WithTaggedFlag("INTR_SRCTRIGINWAIT", 8)
                .WithTaggedFlag("INTR_DESTRIGINWAIT", 9)
                .WithTaggedFlag("INTR_TRIGOUTACKWAIT", 10)
                .WithReservedBits(11, 5)
                .WithFlag(16, out channels[index].StatusDoneField, FieldMode.Read | FieldMode.WriteOneToClear, name: "STAT_DONE",
                    changeCallback: (_, _) => UpdateInterrupts())
                .WithTaggedFlag("STAT_ERR", 17)
                .WithTaggedFlag("STAT_DISABLED", 18)
                .WithTaggedFlag("STAT_STOPPED", 19)
                .WithTaggedFlag("STAT_PAUSED", 20)
                .WithTaggedFlag("STAT_RESUMEWAIT", 21)
                .WithReservedBits(22, 2)
                .WithTaggedFlag("STAT_SRCTRIGINWAIT", 24)
                .WithTaggedFlag("STAT_DESTRIGINWAIT", 25)
                .WithTaggedFlag("STAT_TRIGOUTACKWAIT", 26)
                .WithReservedBits(27, 5))
            ;
            Registers.ChannelInterruptEnable.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register
                        .WithFlag(0, out channels[index].EnableDoneField, name: "INTREN_DONE",
                            changeCallback: (_, __) => UpdateInterrupts())
                        .WithTaggedFlag("INTREN_ERR", 1)
                        .WithTaggedFlag("INTREN_DISABLED", 2)
                        .WithTaggedFlag("INTREN_STOPPED", 3)
                        .WithReservedBits(4, 4)
                        .WithTaggedFlag("INTREN_SRCTRIGINWAIT", 8)
                        .WithTaggedFlag("INTREN_DESTRIGINWAIT", 9)
                        .WithTaggedFlag("INTREN_TRIGOUTACKWAIT", 10)
                        .WithReservedBits(11, 21);
                })
            ;
            Registers.ChannelControl.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register
                        .WithValueField(0, 3, out channels[index].TransferSizeField, name: "TRANSIZE")
                        .WithReservedBits(3, 1)
                        .WithTag("CHPRIO", 4, 4)
                        .If(variant == Variant.Dma250)
                            .Then(r => r.WithTaggedFlag("CHLOCKREQ", 8))
                            .Else(r => r.WithReservedBits(8, 1))
                        .WithValueField(9, 3, out channels[index].XTypeField, name: "XTYPE")
                        .If(variant == Variant.Dma250)
                            .Then(r => r.WithReservedBits(12, 3))
                            .Else(r => r.WithValueField(12, 3, out channels[index].YTypeField, name: "YTYPE"))
                        .WithReservedBits(15, 3)
                        .WithTag("REGRELOADTYPE", 18, 3)
                        .If(variant == Variant.Dma250)
                            .Then(r => r
                                .WithTag("DONETYPE", 21, 2)
                                .WithReservedBits(23, 1))
                            .Else(r => r.WithTag("DONETYPE", 21, 3))
                        .WithTaggedFlag("DONEPAUSEEN", 24)
                        .WithTaggedFlag("USESRCTRIGIN", 25)
                        .WithTaggedFlag("USEDESTRIGIN", 26)
                        .WithTaggedFlag("USETRIGOUT", 27)
                        .WithTaggedFlag("USEGPO", 28)
                        .If(variant == Variant.Dma250)
                            .Then(r => r.WithReservedBits(29, 1))
                            .Else(r => r.WithTaggedFlag("USESTREAM", 29))
                        .WithReservedBits(30, 2);
                },
                resetValue: 0x00200200)
            ;
            Registers.ChannelSourceAddress.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register.WithValueField(0, 32,
                        out channels[index].SourceAddressField, name: "SRCADDR");
                })
            ;
            Registers.ChannelDestinationAddress.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register.WithValueField(0, 32,
                        out channels[index].DestinationAddressField, name: "DESADDR");
                })
            ;
            if(variant == Variant.Dma350)
            {
                Registers.ChannelSourceAddressHigh.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, _) =>
                    {
                        register.WithTag("SRCADDRHI", 0, 32);
                    })
                ;
                Registers.ChannelDestinationAddressHigh.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, _) =>
                    {
                        register.WithTag("DESADDRHI", 0, 32);
                    })
                ;
            }
            Registers.ChannelXSize.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register
                        .WithValueField(0, 16, out channels[index].SourceXSizeField, name: "SRCXSIZE")
                        .WithValueField(16, 16, out channels[index].DestinationXSizeField, name: "DESXSIZE");
                })
            ;
            if(variant == Variant.Dma350)
            {
                Registers.ChannelXSizeHigh.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, _) =>
                    {
                        register
                            .WithTag("SRCXSIZEHI", 0, 16)
                            .WithTag("DESXSIZEHI", 16, 16);
                    })
                ;
            }
            Registers.ChannelSourceTransactionConfiguration.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register
                        .WithTag("SRCMEMATTRLO", 0, 4)
                        .WithTag("SRCMEMATTRHI", 4, 4)
                        .WithTag("SRCSHAREATTR", 8, 2)
                        .WithTaggedFlag("SRCNONSECATTR", 10)
                        .WithTaggedFlag("SRCPRIVATTR", 11)
                        .WithReservedBits(12, 4)
                        .WithTag("SRCMAXBURSTLEN", 16, 4)
                        .WithReservedBits(20, 12);
                },
                resetValue: 0x000F0400)
            ;
            Registers.ChannelDestinationTransactionConfiguration.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register
                        .WithTag("DESMEMATTRLO", 0, 4)
                        .WithTag("DESMEMATTRHI", 4, 4)
                        .WithTag("DESSHAREATTR", 8, 2)
                        .WithTaggedFlag("DESNONSECATTR", 10)
                        .WithTaggedFlag("DESPRIVATTR", 11)
                        .WithReservedBits(12, 4)
                        .WithTag("DESMAXBURSTLEN", 16, 4)
                        .WithReservedBits(20, 12);
                },
                resetValue: 0x000F0400)
            ;
            Registers.ChannelXAddressIncrement.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register
                        .WithValueField(0, 16, out channels[index].SourceXAddressIncrementField,
                            name: "SRCXADDRINC")
                        .WithValueField(16, 16, out channels[index].DestinationXAddressIncrementField,
                            name: "DESXADDRINC");
                })
            ;
            if(variant == Variant.Dma350)
            {
                Registers.ChannelYAddressStride.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, index) =>
                    {
                        register
                            .WithValueField(0, 16, out channels[index].SourceYAddressStrideField,
                                name: "SRCYADDRSTRIDE")
                            .WithValueField(16, 16, out channels[index].DestinationYAddressStrideField,
                                name: "DESYADDRSTRIDE");
                    })
                ;
                Registers.ChannelFillValue.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, index) =>
                    {
                        register.WithTag("FILLVAL", 0, 32);
                    })
                ;
                Registers.ChannelYSize.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, index) =>
                    {
                        register
                            .WithValueField(0, 16, out channels[index].SourceYSizeField, name: "SRCYSIZE")
                            .WithValueField(16, 16, out channels[index].DestinationYSizeField, name: "DESYSIZE");
                    })
                ;
                Registers.ChannelTemplateConfiguration.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, index) =>
                    {
                        register
                            .WithReservedBits(0, 8)
                            .WithTag("SRCTMPLTSIZE", 8, 5)
                            .WithReservedBits(13, 3)
                            .WithTag("DESTMPLTSIZE", 16, 5)
                            .WithReservedBits(21, 11);
                    })
                ;
                Registers.ChannelSourceTemplate.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, index) =>
                    {
                        register
                            .WithTaggedFlag("SRCTMPLTLSB", 0)
                            .WithTag("SRCTMPLT", 1, 31);
                    },
                    resetValue: 1)
                ;
                Registers.ChannelDestinationTemplate.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, index) =>
                    {
                        register
                            .WithTaggedFlag("DESTMPLTLSB", 0)
                            .WithTag("DESTMPLT", 1, 31);
                    },
                    resetValue: 1)
                ;
            }
            Registers.ChannelSourceTriggerInputConfiguration.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register
                        .If(variant == Variant.Dma350)
                            .Then(r => r.WithTag("SRCTRIGINSEL", 0, 8))
                            .Else(r => r.WithReservedBits(0, 8))
                        .WithTag("SRCTRIGINTYPE", 8, 2)
                        .WithTag("SRCTRIGINMODE", 10, 2)
                        .WithReservedBits(12, 4)
                        .WithTag("SRCTRIGINBLKSIZE", 16, 8)
                        .WithReservedBits(24, 8);
                })
            ;
            Registers.ChannelDestinationTriggerInputConfiguration.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register
                        .If(variant == Variant.Dma350)
                            .Then(r => r.WithTag("DESTRIGINSEL", 0, 8))
                            .Else(r => r.WithReservedBits(0, 8))
                        .WithTag("DESTRIGINTYPE", 8, 2)
                        .WithTag("DESTRIGINMODE", 10, 2)
                        .WithReservedBits(12, 4)
                        .WithTag("DESTRIGINBLKSIZE", 16, 8)
                        .WithReservedBits(24, 8);
                })
            ;
            Registers.ChannelTriggerOutputConfiguration.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register
                        .If(variant == Variant.Dma350)
                            .Then(r => r.WithTag("TRIGOUTSEL", 0, 6))
                            .Else(r => r.WithReservedBits(0, 6))
                        .WithReservedBits(6, 2)
                        .WithTag("TRIGOUTTYPE", 8, 2)
                        .WithReservedBits(10, 22);
                })
            ;
            Registers.ChannelGeneralPurposeOutputEnable0.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register.WithTag("GPOEN0", 0, 32);
                })
            ;
            Registers.ChannelGeneralPurposeOutputValue0.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register.WithTag("GPOVAL0", 0, 32);
                })
            ;
            if(variant == Variant.Dma350)
            {
                Registers.ChannelStreamInterfaceConfiguration.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, index) =>
                    {
                        register
                            .WithReservedBits(0, 9)
                            .WithTag("STREAMTYPE", 9, 2)
                            .WithReservedBits(11, 21);
                    })
                ;
            }
            Registers.ChannelLinkAttributes.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register
                        .WithTag("LINKMEMATTRLO", 0, 4)
                        .WithTag("LINKMEMATTRHI", 4, 4)
                        .WithTag("LINKSHAREATTR", 8, 2)
                        .WithReservedBits(10, 22);
                })
            ;
            Registers.ChannelAutomaticConfiguration.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, index) =>
                {
                    register
                        .WithTag("CMDRESTARTCNT", 0, 16)
                        .WithTaggedFlag("CMDRESTARTINFEN", 16)
                        .WithReservedBits(17, 15);
                })
            ;
            Registers.ChannelLinkAddress.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, _) => register
                    .WithTaggedFlag("LINKADDREN", 0)
                    .WithReservedBits(1, 1)
                    .WithTag("LINKADDR", 2, 30))
            ;
            if(variant == Variant.Dma350)
            {
                Registers.ChannelLinkAddressHigh.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, _) =>
                    {
                        register.WithTag("LINKADDRHI", 0, 32);
                    })
                ;
            }
            Registers.ChannelGeneralPurposeOutputRead0.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, _) => register.WithTag("GPOREAD0", 0, 32))
            ;
            if(variant == Variant.Dma350)
            {
                Registers.ChannelWorkingRegisterPointer.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, _) => register
                        .WithTag("WRKREGPTR", 0, 4)
                        .WithReservedBits(4, 28))
                ;
                Registers.ChannelWorkingRegisterValue.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, _) => register.WithTag("WRKREGVAL", 0, 32))
                ;
            }
            Registers.ChannelErrorInformation.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, _) =>
                {
                    register
                        .WithTaggedFlag("BUSERR", 0)
                        .WithTaggedFlag("CFGERR", 1)
                        .If(variant == Variant.Dma350)
                            .Then(r => r
                                .WithTaggedFlag("SRCTRIGINSELERR", 2)
                                .WithTaggedFlag("DESTRIGINSELERR", 3)
                                .WithTaggedFlag("TRIGOUTSELERR", 4))
                            .Else(r => r.WithReservedBits(2, 3))
                        .WithReservedBits(5, 2)
                        .If(variant == Variant.Dma350)
                            .Then(r => r.WithTaggedFlag("STREAMERR", 7))
                            .Else(r => r.WithReservedBits(7, 1))
                        .WithReservedBits(8, 8)
                        .WithTag("ERRINFO", 16, 16);
                })
            ;
            Registers.ChannelImplementationIdentification.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, _) => register
                    .WithTag("IMPLEMENTER", 0, 12)
                    .WithTag("REVISION", 12, 4)
                    .WithTag("VARIANT", 16, 4)
                    .WithTag("PRODUCTID", 20, 12),
                resetValue: ChannelIdentificationResetValue)
            ;
            Registers.ChannelArchitectureIdentification.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, _) => register
                    .WithTag("ARCH_MINOR_REV", 0, 4)
                    .WithTag("ARCH_MAJOR_REV", 4, 4)
                    .WithReservedBits(8, 24))
            ;
            if(variant == Variant.Dma350)
            {
                Registers.ChannelIssueCapability.DefineMany(this,
                    count: (uint)numberOfChannels,
                    stepInBytes: ChannelFrameStride,
                    setup: (register, _) => register
                        .WithTag("ISSUECAP", 0, 3)
                        .WithReservedBits(3, 29),
                    resetValue: 0x7)
                ;
            }
            Registers.ChannelBuildConfiguration0.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, _) => register
                    .WithTag("DATA_BUFF_SIZE", 0, 8)
                    .WithTag("CMD_BUFF_SIZE", 8, 8)
                    .WithTag("ADDR_WIDTH", 16, 6)
                    .WithTag("DATA_WIDTH", 22, 3)
                    .WithReservedBits(25, 1)
                    .WithTag("INC_WIDTH", 26, 4)
                    .WithReservedBits(30, 2),
                resetValue: variant == Variant.Dma250
                    ? DMA250ChannelBuildConfiguration0ResetValue
                    : DMA350ChannelBuildConfiguration0ResetValue)
            ;
            Registers.ChannelBuildConfiguration1.DefineMany(this,
                count: (uint)numberOfChannels,
                stepInBytes: ChannelFrameStride,
                setup: (register, _) => register
                    .WithTaggedFlag("HAS_XSIZEHI", 0)
                    .WithTaggedFlag("HAS_WRAP", 1)
                    .WithTaggedFlag("HAS_2D", 2)
                    .WithTaggedFlag("HAS_TMPLT", 3)
                    .WithTaggedFlag("HAS_TRIG", 4)
                    .WithTaggedFlag("HAS_TRIGIN", 5)
                    .WithTaggedFlag("HAS_TRIGOUT", 6)
                    .WithTaggedFlag("HAS_TRIGSEL", 7)
                    .WithTaggedFlag("HAS_CMDLINK", 8)
                    .WithTaggedFlag("HAS_AUTO", 9)
                    .WithTaggedFlag("HAS_WRKREG", 10)
                    .WithTaggedFlag("HAS_STREAM", 11)
                    .WithTaggedFlag("HAS_STREAMSEL", 12)
                    .WithReservedBits(13, 5)
                    .WithTaggedFlag("HAS_GPOSEL", 18)
                    .WithTag("GPO_WIDTH", 19, 7)
                    .WithReservedBits(26, 6),
                resetValue: variant == Variant.Dma250
                    ? DMA250ChannelBuildConfiguration1ResetValue
                    : DMA350ChannelBuildConfiguration1ResetValue)
            ;
        }

        private int AddressWidth => variant == Variant.Dma250 ? DMA250AddressWidth : DMA350AddressWidth;

        private readonly Variant variant;
        private readonly int numberOfChannels;
        private readonly Channel[] channels;
        private readonly Dictionary<Registers, Registers> ControlFrameRegistersMap = new()
        {
            [Registers.SecureChannelInterruptStatus] = Registers.NonSecureChannelInterruptStatus,
            [Registers.SecureStatus] = Registers.NonSecureStatus,
            [Registers.SecureControl] = Registers.NonSecureControl,
            [Registers.SecureContextBase] = Registers.NonSecureContextBase,
            [Registers.SecureChannelPointer] = Registers.NonSecureChannelPointer,
            [Registers.SecureChannelConfiguration] = Registers.NonSecureChannelConfiguration,
            [Registers.SecureStatusPointer] = Registers.NonSecureStatusPointer,
            [Registers.SecureStatusValue] = Registers.NonSecureStatusValue,
            [Registers.SecureSignalPointer] = Registers.NonSecureSignalPointer,
            [Registers.SecureSignalValue] = Registers.NonSecureSignalValue,
        };

        private const int DMA250AddressWidth = 32;
        private const int DMA350AddressWidth = 64;
        private const uint DMA350ChannelBuildConfiguration1ResetValue = 0x0000079F;
        private const int MaximumDMA350Channels = 8;
        private const int MaximumDMA250Channels = 32;
        private const int MinimumDMA250Channels = 2;
        private const int MinimumDMA350Channels = 1;
        private const int ChannelsPerPage = 16;
        private const uint ChannelFrameStride = 0x100;
        private const long RegisterFrameSize = 0x1000;
        private const uint ChannelIdentificationResetValue = 0x3A00043B;
        private const uint DMA250ChannelBuildConfiguration0ResetValue = 0x3C9F0003;
        private const uint DMA350ChannelBuildConfiguration0ResetValue = 0x3CBF0003;
        private const uint DMA250ChannelBuildConfiguration1ResetValue = 0x00000370;
        private const int DataWidthEncoding = 2; // 32-bit data bus
        private const long SecurityConfigurationFrameOffset = 0x0000;
        private const long SecureControlFrameOffset = 0x0100;
        private const long NonSecureControlFrameOffset = 0x0200;
        private const long InformationFrameOffset = 0x0F00;
        private const long ChannelFrameOffset = 0x1000;

        public enum Variant
        {
            Dma250,
            Dma350,
        }

        private sealed class Channel
        {
            public Channel(Arm_Dma350 parent)
            {
                Parent = parent;
            }

            public void PerformSingleTransfer()
            {
                if(XType != TypeEnum.Continue || YType is not (TypeEnum.Disable or TypeEnum.Continue))
                {
                    Parent.WarningLog(
                        "Unsupported transfer configuration: XTYPE={0}, YTYPE={1}. " +
                        "Only XTYPE=CONTINUE and YTYPE=DISABLE or CONTINUE transfers are supported",
                        XType, YType);
                    return;
                }

                var isTransfer2D = YType is not TypeEnum.Disable;

                var source = EnumerateTransferAddresses(
                    SourceAddress,
                    SourceXSize,
                    isTransfer2D ? SourceYSize : 1,
                    SourceXAddressIncrement * TransferSize,
                    SourceYAddressStride * TransferSize);

                var destination = EnumerateTransferAddresses(
                    DestinationAddress,
                    DestinationXSize,
                    isTransfer2D ? DestinationYSize : 1,
                    DestinationXAddressIncrement * TransferSize,
                    DestinationYAddressStride * TransferSize);

                using(var sourceEnumerator = source.GetEnumerator())
                using(var destinationEnumerator = destination.GetEnumerator())
                {
                    var sysbus = Parent.sysbus;
                    var context = GetCurrentCPUOrNull();
                    while(sourceEnumerator.MoveNext() && destinationEnumerator.MoveNext())
                    {
                        var currentSource = sourceEnumerator.Current;
                        var currentDestination = destinationEnumerator.Current;
                        switch(TransferType)
                        {
                        case TransferType.Byte:
                            var readByte  = sysbus.ReadByte(currentSource, context);
                            sysbus.WriteByte(currentDestination, readByte, context);
                            break;
                        case TransferType.Word:
                            var readWord = sysbus.ReadWord(currentSource, context);
                            sysbus.WriteWord(currentDestination, readWord, context);
                            break;
                        case TransferType.DoubleWord:
                            var readDoubleWord = sysbus.ReadDoubleWord(currentSource, context);
                            sysbus.WriteDoubleWord(currentDestination, readDoubleWord, context);
                            break;
                        case TransferType.QuadWord:
                            var readQuadWord = sysbus.ReadQuadWord(currentSource, context);
                            sysbus.WriteQuadWord(currentDestination, readQuadWord, context);
                            break;
                        default:
                            Parent.WarningLog("Requested transfer type {0} is not supported", TransferType);
                            break;
                        }
                    }
                }

                StatusDoneField.Value = true;
                Parent.UpdateInterrupts();
            }

            public bool InterruptPending =>
                (EnableDoneField?.Value == true && StatusDoneField?.Value == true);

            public IValueRegisterField SourceAddressField;
            public IValueRegisterField DestinationAddressField;
            public IValueRegisterField SourceXSizeField;
            public IValueRegisterField DestinationXSizeField;
            public IValueRegisterField SourceXAddressIncrementField;
            public IValueRegisterField DestinationXAddressIncrementField;
            public IValueRegisterField SourceYAddressStrideField;
            public IValueRegisterField DestinationYAddressStrideField;
            public IValueRegisterField SourceYSizeField;
            public IValueRegisterField DestinationYSizeField;
            public IValueRegisterField XTypeField;
            public IValueRegisterField YTypeField;
            public IValueRegisterField TransferSizeField;

            public IFlagRegisterField StatusDoneField;
            public IFlagRegisterField EnableDoneField;

            public readonly Arm_Dma350 Parent;

            private ICPU GetCurrentCPUOrNull()
            {
                if(!Parent.sysbus.TryGetCurrentCPU(out var cpu))
                {
                    return null;
                }
                return cpu;
            }

            private ulong SourceAddress => (ulong)(SourceAddressField?.Value ?? 0);

            private ulong DestinationAddress => (ulong)(DestinationAddressField?.Value ?? 0);

            private ulong SourceXSize => SourceXSizeField?.Value ?? 0U;

            private ulong DestinationXSize => DestinationXSizeField?.Value ?? 0U;

            private long SourceXAddressIncrement => unchecked((short)(SourceXAddressIncrementField?.Value ?? 0));

            private long DestinationXAddressIncrement => unchecked((short)(DestinationXAddressIncrementField?.Value ?? 0));

            private long SourceYAddressStride => unchecked((short)(SourceYAddressStrideField?.Value ?? 0));

            private long DestinationYAddressStride => unchecked((short)(DestinationYAddressStrideField?.Value ?? 0));

            private ulong SourceYSize => SourceYSizeField?.Value ?? 1U;

            private ulong DestinationYSize => DestinationYSizeField?.Value ?? 1U;

            private TypeEnum XType => (TypeEnum)(XTypeField?.Value ?? 0);

            private TypeEnum YType => (TypeEnum)(YTypeField?.Value ?? 0);

            private int TransferSize => 1 << (int)(TransferSizeField?.Value ?? 0);

            private TransferType TransferType => (TransferType)TransferSize;

            private enum TypeEnum
            {
                Disable,
                Continue,
                Wrap,
                Fill,
            }
        }

        private enum SecurityConfigurationRegisters : long
        {
            Channels = 0x00,
            TriggerInputs = 0x08,
            TriggerOutputs = 0x28,
            Control = 0x40,
            InterruptStatus = 0x44,
        }

        private enum ControlRegisters : long
        {
            ChannelInterruptStatus = 0x00,
            Status = 0x08,
            Control = 0x0C,
            ContextBase = 0x10,
            ChannelPointer = 0x14,
            ChannelConfiguration = 0x18,
            StatusPointer = 0xF0,
            StatusValue = 0xF4,
            SignalPointer = 0xF8,
            SignalValue = 0xFC,
        }

        private enum InformationRegisters : long
        {
            BuildConfiguration0 = 0xB0,
            BuildConfiguration1 = 0xB4,
            BuildConfiguration2 = 0xB8,
            ImplementationIdentification = 0xC8,
            ArchitectureIdentification = 0xCC,
            PeripheralIdentification4 = 0xD0,
            PeripheralIdentification0 = 0xE0,
            PeripheralIdentification1 = 0xE4,
            PeripheralIdentification2 = 0xE8,
            PeripheralIdentification3 = 0xEC,
            ComponentIdentification0 = 0xF0,
            ComponentIdentification1 = 0xF4,
            ComponentIdentification2 = 0xF8,
            ComponentIdentification3 = 0xFC,
        }

        private enum ChannelRegisters : long
        {
            Command = 0x00,
            Status = 0x04,
            InterruptEnable = 0x08,
            Control = 0x0C,
            SourceAddress = 0x10,
            SourceAddressHigh = 0x14,
            DestinationAddress = 0x18,
            DestinationAddressHigh = 0x1C,
            XSize = 0x20,
            XSizeHigh = 0x24,
            SourceTransactionConfiguration = 0x28,
            DestinationTransactionConfiguration = 0x2C,
            XAddressIncrement = 0x30,
            YAddressStride = 0x34,
            FillValue = 0x38,
            YSize = 0x3C,
            TemplateConfiguration = 0x40,
            SourceTemplate = 0x44,
            DestinationTemplate = 0x48,
            SourceTriggerInputConfiguration = 0x4C,
            DestinationTriggerInputConfiguration = 0x50,
            TriggerOutputConfiguration = 0x54,
            GeneralPurposeOutputEnable0 = 0x58,
            GeneralPurposeOutputValue0 = 0x60,
            StreamInterfaceConfiguration = 0x68,
            LinkAttributes = 0x70,
            AutomaticConfiguration = 0x74,
            LinkAddress = 0x78,
            LinkAddressHigh = 0x7C,
            GeneralPurposeOutputRead0 = 0x80,
            WorkingRegisterPointer = 0x88,
            WorkingRegisterValue = 0x8C,
            ErrorInformation = 0x90,
            ImplementationIdentification = 0xC8,
            ArchitectureIdentification = 0xCC,
            IssueCapability = 0xE8,
            BuildConfiguration0 = 0xF8,
            BuildConfiguration1 = 0xFC,
        }

        // The public register map is kept in one enum. Its values are composed
        // from the frame-specific enums above, so each frame owns relative
        // offsets while this enum exposes the final peripheral offsets.
        [RegistersDescription]
        private enum Registers : long
        {
            SecurityChannels = SecurityConfigurationFrameOffset + (long)SecurityConfigurationRegisters.Channels,
            SecurityTriggerInputs = SecurityConfigurationFrameOffset + (long)SecurityConfigurationRegisters.TriggerInputs,
            SecurityTriggerOutputs = SecurityConfigurationFrameOffset + (long)SecurityConfigurationRegisters.TriggerOutputs,
            SecurityControl = SecurityConfigurationFrameOffset + (long)SecurityConfigurationRegisters.Control,
            SecurityInterruptStatus = SecurityConfigurationFrameOffset + (long)SecurityConfigurationRegisters.InterruptStatus,

            SecureChannelInterruptStatus = SecureControlFrameOffset + (long)ControlRegisters.ChannelInterruptStatus,
            SecureStatus = SecureControlFrameOffset + (long)ControlRegisters.Status,
            SecureControl = SecureControlFrameOffset + (long)ControlRegisters.Control,
            SecureContextBase = SecureControlFrameOffset + (long)ControlRegisters.ContextBase,
            SecureChannelPointer = SecureControlFrameOffset + (long)ControlRegisters.ChannelPointer,
            SecureChannelConfiguration = SecureControlFrameOffset + (long)ControlRegisters.ChannelConfiguration,
            SecureStatusPointer = SecureControlFrameOffset + (long)ControlRegisters.StatusPointer,
            SecureStatusValue = SecureControlFrameOffset + (long)ControlRegisters.StatusValue,
            SecureSignalPointer = SecureControlFrameOffset + (long)ControlRegisters.SignalPointer,
            SecureSignalValue = SecureControlFrameOffset + (long)ControlRegisters.SignalValue,

            NonSecureChannelInterruptStatus = NonSecureControlFrameOffset + (long)ControlRegisters.ChannelInterruptStatus,
            NonSecureStatus = NonSecureControlFrameOffset + (long)ControlRegisters.Status,
            NonSecureControl = NonSecureControlFrameOffset + (long)ControlRegisters.Control,
            NonSecureContextBase = NonSecureControlFrameOffset + (long)ControlRegisters.ContextBase,
            NonSecureChannelPointer = NonSecureControlFrameOffset + (long)ControlRegisters.ChannelPointer,
            NonSecureChannelConfiguration = NonSecureControlFrameOffset + (long)ControlRegisters.ChannelConfiguration,
            NonSecureStatusPointer = NonSecureControlFrameOffset + (long)ControlRegisters.StatusPointer,
            NonSecureStatusValue = NonSecureControlFrameOffset + (long)ControlRegisters.StatusValue,
            NonSecureSignalPointer = NonSecureControlFrameOffset + (long)ControlRegisters.SignalPointer,
            NonSecureSignalValue = NonSecureControlFrameOffset + (long)ControlRegisters.SignalValue,

            BuildConfiguration0 = InformationFrameOffset + (long)InformationRegisters.BuildConfiguration0,
            BuildConfiguration1 = InformationFrameOffset + (long)InformationRegisters.BuildConfiguration1,
            BuildConfiguration2 = InformationFrameOffset + (long)InformationRegisters.BuildConfiguration2,
            ImplementationIdentification = InformationFrameOffset + (long)InformationRegisters.ImplementationIdentification,
            ArchitectureIdentification = InformationFrameOffset + (long)InformationRegisters.ArchitectureIdentification,
            PeripheralIdentification4 = InformationFrameOffset + (long)InformationRegisters.PeripheralIdentification4,
            PeripheralIdentification0 = InformationFrameOffset + (long)InformationRegisters.PeripheralIdentification0,
            PeripheralIdentification1 = InformationFrameOffset + (long)InformationRegisters.PeripheralIdentification1,
            PeripheralIdentification2 = InformationFrameOffset + (long)InformationRegisters.PeripheralIdentification2,
            PeripheralIdentification3 = InformationFrameOffset + (long)InformationRegisters.PeripheralIdentification3,
            ComponentIdentification0 = InformationFrameOffset + (long)InformationRegisters.ComponentIdentification0,
            ComponentIdentification1 = InformationFrameOffset + (long)InformationRegisters.ComponentIdentification1,
            ComponentIdentification2 = InformationFrameOffset + (long)InformationRegisters.ComponentIdentification2,
            ComponentIdentification3 = InformationFrameOffset + (long)InformationRegisters.ComponentIdentification3,

            ChannelCommand = ChannelFrameOffset + (long)ChannelRegisters.Command,
            ChannelStatus = ChannelFrameOffset + (long)ChannelRegisters.Status,
            ChannelInterruptEnable = ChannelFrameOffset + (long)ChannelRegisters.InterruptEnable,
            ChannelControl = ChannelFrameOffset + (long)ChannelRegisters.Control,
            ChannelSourceAddress = ChannelFrameOffset + (long)ChannelRegisters.SourceAddress,
            ChannelSourceAddressHigh = ChannelFrameOffset + (long)ChannelRegisters.SourceAddressHigh,
            ChannelDestinationAddress = ChannelFrameOffset + (long)ChannelRegisters.DestinationAddress,
            ChannelDestinationAddressHigh = ChannelFrameOffset + (long)ChannelRegisters.DestinationAddressHigh,
            ChannelXSize = ChannelFrameOffset + (long)ChannelRegisters.XSize,
            ChannelXSizeHigh = ChannelFrameOffset + (long)ChannelRegisters.XSizeHigh,
            ChannelSourceTransactionConfiguration = ChannelFrameOffset + (long)ChannelRegisters.SourceTransactionConfiguration,
            ChannelDestinationTransactionConfiguration = ChannelFrameOffset + (long)ChannelRegisters.DestinationTransactionConfiguration,
            ChannelXAddressIncrement = ChannelFrameOffset + (long)ChannelRegisters.XAddressIncrement,
            ChannelYAddressStride = ChannelFrameOffset + (long)ChannelRegisters.YAddressStride,
            ChannelFillValue = ChannelFrameOffset + (long)ChannelRegisters.FillValue,
            ChannelYSize = ChannelFrameOffset + (long)ChannelRegisters.YSize,
            ChannelTemplateConfiguration = ChannelFrameOffset + (long)ChannelRegisters.TemplateConfiguration,
            ChannelSourceTemplate = ChannelFrameOffset + (long)ChannelRegisters.SourceTemplate,
            ChannelDestinationTemplate = ChannelFrameOffset + (long)ChannelRegisters.DestinationTemplate,
            ChannelSourceTriggerInputConfiguration = ChannelFrameOffset + (long)ChannelRegisters.SourceTriggerInputConfiguration,
            ChannelDestinationTriggerInputConfiguration = ChannelFrameOffset + (long)ChannelRegisters.DestinationTriggerInputConfiguration,
            ChannelTriggerOutputConfiguration = ChannelFrameOffset + (long)ChannelRegisters.TriggerOutputConfiguration,
            ChannelGeneralPurposeOutputEnable0 = ChannelFrameOffset + (long)ChannelRegisters.GeneralPurposeOutputEnable0,
            ChannelGeneralPurposeOutputValue0 = ChannelFrameOffset + (long)ChannelRegisters.GeneralPurposeOutputValue0,
            ChannelStreamInterfaceConfiguration = ChannelFrameOffset + (long)ChannelRegisters.StreamInterfaceConfiguration,
            ChannelLinkAttributes = ChannelFrameOffset + (long)ChannelRegisters.LinkAttributes,
            ChannelAutomaticConfiguration = ChannelFrameOffset + (long)ChannelRegisters.AutomaticConfiguration,
            ChannelLinkAddress = ChannelFrameOffset + (long)ChannelRegisters.LinkAddress,
            ChannelLinkAddressHigh = ChannelFrameOffset + (long)ChannelRegisters.LinkAddressHigh,
            ChannelGeneralPurposeOutputRead0 = ChannelFrameOffset + (long)ChannelRegisters.GeneralPurposeOutputRead0,
            ChannelWorkingRegisterPointer = ChannelFrameOffset + (long)ChannelRegisters.WorkingRegisterPointer,
            ChannelWorkingRegisterValue = ChannelFrameOffset + (long)ChannelRegisters.WorkingRegisterValue,
            ChannelErrorInformation = ChannelFrameOffset + (long)ChannelRegisters.ErrorInformation,
            ChannelImplementationIdentification = ChannelFrameOffset + (long)ChannelRegisters.ImplementationIdentification,
            ChannelArchitectureIdentification = ChannelFrameOffset + (long)ChannelRegisters.ArchitectureIdentification,
            ChannelIssueCapability = ChannelFrameOffset + (long)ChannelRegisters.IssueCapability,
            ChannelBuildConfiguration0 = ChannelFrameOffset + (long)ChannelRegisters.BuildConfiguration0,
            ChannelBuildConfiguration1 = ChannelFrameOffset + (long)ChannelRegisters.BuildConfiguration1,
        }
    }
}
