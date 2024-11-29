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
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.DMA
{
    // Currently only memory to memory transfers are supported.
    public class RenesasRZG_DMAC : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize, INumberedGPIOOutput
    {
        public RenesasRZG_DMAC(IMachine machine)
        {
            sysbus = machine.GetSystemBus(this);
            ErrorIRQ = new GPIO();
            channels = new Channel[ChannelCount];
            RegistersCollection = new DoubleWordRegisterCollection(this, DefineRegisters());
            Connections = channels
                .Select((channel, i) => new { Key = i, Value = (IGPIO)channel.IRQ })
                .ToDictionary(x => x.Key, x => x.Value);
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            ErrorIRQ.Unset();
            foreach(var channel in channels)
            {
                channel.Reset();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }
        public DoubleWordRegisterCollection RegistersCollection { get; }
        public long Size => 0x1000;
        public GPIO ErrorIRQ { get; }

        private Dictionary<long, DoubleWordRegister> DefineRegisters()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>();
            for(var block = 0; block < BlockCount; block++)
            {
                var blockIdx = block;
                for(var channelIdx = 0; channelIdx < ChannelsPerBlock; channelIdx++)
                {
                    var absoluteIdx = ChannelsPerBlock * block + channelIdx;
                    var channel = new Channel(this, absoluteIdx);
                    channels[absoluteIdx] = channel;

                    var registersOffset = 0x400 * block + 0x40 * channelIdx;
                    foreach(var registerEntry in channel.DefineRegisters())
                    {
                        registerMap.Add(registersOffset + registerEntry.Key, registerEntry.Value);
                    }
                }

                var baseOffset = 0x400 * block + 0x300;
                registerMap.Add(baseOffset + (long)ControlRegisters.Control, new DoubleWordRegister(this)
                    .WithTaggedFlag("PR (Transfer Priority)", 0)
                    .WithTaggedFlag("LVINT (Interrupt Level)", 1)
                    .WithReservedBits(2, 14)
                    .WithTag("LDPR (Link Descriptor Protection)", 16, 3)
                    .WithReservedBits(19, 1)
                    .WithTag("LDCA (Link Descriptor Cache)", 20, 4)
                    .WithTag("LWPR (Link WriteBack Protection)", 24, 3)
                    .WithReservedBits(27, 1)
                    .WithTag("LWCA (Link WriteBack Cache)", 28, 4));

                registerMap.Add(baseOffset + (long)ControlRegisters.StatusEnabled, new DoubleWordRegister(this)
                    .WithFlags(0, ChannelsPerBlock, FieldMode.Read, name: "EN",
                        valueProviderCallback: (i, _) => channels[ChannelsPerBlock * blockIdx + i].Enabled)
                    .WithReservedBits(ChannelsPerBlock, 32 - ChannelsPerBlock));

                registerMap.Add(baseOffset + (long)ControlRegisters.StatusError, new DoubleWordRegister(this)
                    .WithFlags(0, ChannelsPerBlock, FieldMode.Read, name: "ER",
                        valueProviderCallback: (i, _) => channels[ChannelsPerBlock * blockIdx + i].Error)
                    .WithReservedBits(ChannelsPerBlock, 32 - ChannelsPerBlock));

                registerMap.Add(baseOffset + (long)ControlRegisters.StatusInterrupted, new DoubleWordRegister(this)
                    .WithFlags(0, ChannelsPerBlock, FieldMode.Read, name: "END",
                        valueProviderCallback: (i, _) => channels[ChannelsPerBlock * blockIdx + i].EndInterrupt)
                    .WithReservedBits(ChannelsPerBlock, 32 - ChannelsPerBlock));

                registerMap.Add(baseOffset + (long)ControlRegisters.StatusTerminalCount, new DoubleWordRegister(this)
                    .WithFlags(0, ChannelsPerBlock, FieldMode.Read, name: "TC",
                        valueProviderCallback: (i, _) => channels[ChannelsPerBlock * blockIdx + i].TerminalCount)
                    .WithReservedBits(ChannelsPerBlock, 32 - ChannelsPerBlock));

                registerMap.Add(baseOffset + (long)ControlRegisters.StatusSuspend, new DoubleWordRegister(this)
                    .WithTaggedFlags("SUS", 0, ChannelsPerBlock)
                    .WithReservedBits(ChannelsPerBlock, 32 - ChannelsPerBlock));
            }
            return registerMap;
        }

        private void UpdateErrorInterrupt()
        {
            var status = channels.Where(x => x.Error).Any();
            this.DebugLog("ErrorIRQ: {0}", status ? "Set" : "Unset");
            ErrorIRQ.Set(status);
        }

        private readonly IBusController sysbus;
        private readonly Channel[] channels;

        private const int BlockCount = 2;
        private const int ChannelsPerBlock = 8;
        private const int ChannelCount = BlockCount * ChannelsPerBlock;

        private enum ControlRegisters
        {
            Control             = 0x00, // DCTRL_n_m/n_mS
            StatusEnabled       = 0x10, // DCTRL_n_m/n_mS
            StatusError         = 0x14, // DSTAT_ER_n_m/n_mS
            StatusInterrupted   = 0x18, // DSTAT_END_n_m/n_mS
            StatusTerminalCount = 0x1C, // DSTAT_TC_n_m/n_mS
            StatusSuspend       = 0x20, // DSTAT_SUS_n_m/n_mS
        }

        private class Channel
        {
            public Channel(RenesasRZG_DMAC parent, int channelId)
            {
                this.parent = parent;
                IRQ = new GPIO();
                logPrefix = $"DMA Channel {channelId}";
                dma = new DmaEngine(parent.sysbus);
                nextSourceAddresses = new IValueRegisterField[AddressRegisterBanks];
                nextDestinationAddresses = new IValueRegisterField[AddressRegisterBanks];
                nextTransactionBytes = new IValueRegisterField[AddressRegisterBanks];

                Reset();
            }

            public void Reset()
            {
                IRQ.Unset();
                currentSourceAddress = 0;
                currentDestinationAddress = 0;
                currentTransactionByte = 0;
                sourceTransferType = TransferType.Byte;
                destinationTransferType = TransferType.Byte;
                requestingCpu = null;
            }

            public Dictionary<long, DoubleWordRegister> DefineRegisters()
            {
                var registers = new Dictionary<long, DoubleWordRegister>();

                registers.Add((long)Registers.Next0SourceAddress, new DoubleWordRegister(parent)
                    .WithValueField(0, 32, out nextSourceAddresses[0], name: "SA (Next Source Address 0)"));

                registers.Add((long)Registers.Next0DestinationAddress, new DoubleWordRegister(parent)
                    .WithValueField(0, 32, out nextDestinationAddresses[0], name: "DA (Next Destination Address 0)"));

                registers.Add((long)Registers.Next0TransactionByte, new DoubleWordRegister(parent)
                    .WithValueField(0, 32, out nextTransactionBytes[0], name: "TB (Next Transaction Byte 0)"));

                registers.Add((long)Registers.Next1SourceAddress, new DoubleWordRegister(parent)
                    .WithValueField(0, 32, out nextSourceAddresses[1], name: "SA (Next Source Address 1)"));

                registers.Add((long)Registers.Next1DestinationAddress, new DoubleWordRegister(parent)
                    .WithValueField(0, 32, out nextDestinationAddresses[1], name: "DA (Next Destination Address 1)"));

                registers.Add((long)Registers.Next1TransactionByte, new DoubleWordRegister(parent)
                    .WithValueField(0, 32, out nextTransactionBytes[1], name: "TB (Next Transaction Byte 1)"));

                registers.Add((long)Registers.CurrentSourceAddress, new DoubleWordRegister(parent)
                    .WithValueField(0, 32, FieldMode.Read, name: "CRSA (Current Source Address)",
                        valueProviderCallback: _ => currentSourceAddress));

                registers.Add((long)Registers.CurrentDestinationAddress, new DoubleWordRegister(parent)
                    .WithValueField(0, 32, FieldMode.Read, name: "CRDA (Current Destination Address)",
                        valueProviderCallback: _ => currentDestinationAddress));

                registers.Add((long)Registers.CurrentTransactionByte, new DoubleWordRegister(parent)
                    .WithValueField(0, 32, FieldMode.Read, name: "CRTB (Current Transaction Byte)",
                        valueProviderCallback: _ => currentTransactionByte));

                statusRegister = new DoubleWordRegister(parent)
                    .WithFlag(0, out dmaEnabled, FieldMode.Read, name: "EN (Enable)")
                    .WithTaggedFlag("RQST (Request)", 1)
                    .WithTaggedFlag("TACT (Transaction Active)", 2)
                    .WithTaggedFlag("SUS (Suspended)", 3)
                    .WithFlag(4, out dmaError, FieldMode.Read, name: "ER (Error Bit)")
                    .WithFlag(5, out endInterrupt, FieldMode.Read, name: "END (End Interrupted)")
                    .WithFlag(6, out terminalCount, FieldMode.Read, name: "TC (Terminal Count)")
                    .WithFlag(7, FieldMode.Read, name: "SR (Selected Register Set)",
                        valueProviderCallback: _ => registerSetSelect.Value)
                    .WithTaggedFlag("DL (Descriptor Load)", 8)
                    .WithTaggedFlag("DW (Descriptor WriteBack)", 9)
                    .WithTaggedFlag("DER (Descriptor Error)", 10)
                    .WithEnumField<DoubleWordRegister, DMAMode>(11, 1, FieldMode.Read, name: "MODE (DMA Mode)",
                        valueProviderCallback: _ => dmaMode.Value)
                    .WithReservedBits(12, 4)
                    .WithTaggedFlag("INTMSK (Temporary Interrupt Mask)", 16)
                    .WithReservedBits(17, 15);

                registers.Add((long)Registers.ChannelStatus, statusRegister);

                registers.Add((long)Registers.ChannelControl, new DoubleWordRegister(parent)
                    .WithFlag(0, FieldMode.Write, name: "SETEN (Set Enable)",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                LoadTransferParameters();
                            }
                        })
                    .WithFlag(1, FieldMode.Write, name: "CLREN (Clear Enable)",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                dmaEnabled.Value = false;
                            }
                        })
                    .WithFlag(2, FieldMode.Write, name: "STG (Software Trigger)",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                if(!PerformTransfer())
                                {
                                    return;
                                }

                                // Continue the register transfer
                                LoadTransferParameters(setRequestingCpu: false);
                                if(performFullTransfer.Value)
                                {
                                    // Perform the transfer again, with new parameters, if full transfer was requested
                                    PerformTransfer();
                                }
                            }
                        })
                    .WithFlag(3, FieldMode.Write, name: "SWRST (Software Reset)",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                statusRegister.Reset();
                            }
                        })
                    .WithTaggedFlag("CLRRQ (Clear Request bit)", 4)
                    .WithFlag(5, FieldMode.Write, name: "CLREND (Clear End bit)",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                endInterrupt.Value = false;
                            }
                        })
                    .WithFlag(6, FieldMode.Write, name: "CLRTC (Clear Terminal Count)",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                terminalCount.Value = false;
                            }
                        })
                    .WithReservedBits(7, 1)
                    .WithTaggedFlag("SETSUS (Set Suspend)", 8)
                    .WithTaggedFlag("CLRSUS (Clear Suspend)", 9)
                    .WithReservedBits(10, 6)
                    .WithTaggedFlag("SETINTMSK (Set Temporary Interrupt Mask)", 16)
                    .WithTaggedFlag("CLRINTMSK (Clear Temporary Interrupt Mask)", 17)
                    .WithReservedBits(18, 14)
                    .WithWriteCallback((_, __) => UpdateInterrupts()));

                registers.Add((long)Registers.ChannelConfiguration, new DoubleWordRegister(parent)
                    .WithTag("SEL (DMAC Channel Select)", 0, 3)
                    .WithTaggedFlag("REQD (Request Direction)", 3)
                    .WithTaggedFlag("LOEN (Low Enable)", 4)
                    .WithTaggedFlag("HIEN (High Enable)", 5)
                    .WithTaggedFlag("LVL (Level)", 6)
                    .WithReservedBits(7, 1)
                    .WithTag("AM (ACK Mode)", 8, 3)
                    .WithReservedBits(11, 1)
                    .WithValueField(12, 4, name: "SDS (Source Data Size)",
                        changeCallback: (_, value) => sourceTransferType = ConvertToTransferType(value))
                    .WithValueField(16, 4, name: "DDS (Destination Data Size)",
                        changeCallback: (_, value) => destinationTransferType = ConvertToTransferType(value))
                    .WithFlag(20, out sourceAddressCounting, name: "SAD (Source Address Counting Direction)")
                    .WithFlag(21, out destinationAddressCounting, name: "DAD (Destination Address Counting Direction)")
                    .WithFlag(22, out performFullTransfer, name: "TM (Transfer Mode)")
                    .WithReservedBits(23, 1)
                    .WithFlag(24, out endInterruptMask, name: "DEM (DMA Transfer End Interrupt Mask)")
                    .WithReservedBits(25, 2)
                    .WithTaggedFlag("SBE (Sweep Buffer Enable)", 27)
                    .WithFlag(28, out registerSetSelect, name: "RSEL (Register Set Select)")
                    .WithFlag(29, out registerSetSwitch, name: "RSW (Register Select Switch)")
                    .WithFlag(30, out registerSetEnable, name: "REN (Register Set Enable)")
                    .WithEnumField(31, 1, out dmaMode, name: "DMS (DMA Mode Select)"));

                registers.Add((long)Registers.ChannelInterval, new DoubleWordRegister(parent)
                    .WithTag("ITVL (Channel Transfer Interval)", 0, 16)
                    .WithReservedBits(16, 16));

                registers.Add((long)Registers.ChannelExtension, new DoubleWordRegister(parent)
                    .WithTag("SPR (Source Protection)", 0, 3)
                    .WithReservedBits(3, 1)
                    .WithTag("SCA (Source Cache)", 4, 4)
                    .WithTag("DPR (Destination Protection)", 8, 3)
                    .WithReservedBits(11, 1)
                    .WithTag("DCA (Destination Cache)", 12, 4)
                    .WithReservedBits(16, 16));

                registers.Add((long)Registers.NextLinkAddress, new DoubleWordRegister(parent)
                    .WithTag("NXLA (Next Link Address)", 0, 32));

                registers.Add((long)Registers.CurrentLinkAddress, new DoubleWordRegister(parent)
                    .WithTag("CRLA (Current Link Address)", 0, 32));

                return registers;
            }

            public bool PerformTransfer()
            {
                if(!dmaEnabled.Value)
                {
                    parent.WarningLog("{0}: Attempted to perform trigger a transaction on a disabled DMA channel, ignoring", logPrefix);
                    return false;
                }

                Request request;
                if(performFullTransfer.Value)
                {
                    request = new Request(
                        new Place(currentSourceAddress),
                        new Place(currentDestinationAddress),
                        (int)currentTransactionByte,
                        sourceTransferType, destinationTransferType,
                        !sourceAddressCounting.Value,
                        !destinationAddressCounting.Value
                    );
                    currentTransactionByte = 0;
                }
                else
                {
                    var transferSize = (int)sourceTransferType;
                    request = new Request(
                        new Place(currentSourceAddress),
                        new Place(currentDestinationAddress),
                        transferSize,
                        sourceTransferType, destinationTransferType,
                        !sourceAddressCounting.Value,
                        !destinationAddressCounting.Value
                    );
                    currentTransactionByte -= (ulong)transferSize;
                }

                var response = dma.IssueCopy(request, requestingCpu);
                currentSourceAddress = response.ReadAddress.Value;
                currentDestinationAddress = response.WriteAddress.Value;

                if(currentTransactionByte == 0)
                {
                    if(registerSetSwitch.Value)
                    {
                        registerSetSelect.Value = !registerSetSelect.Value;
                    }

                    if(registerSetEnable.Value && dmaMode.Value == DMAMode.Register)
                    {
                        registerSetEnable.Value = false;
                        // Transfer hasn't completed it will be continued from the next register set
                        return true;
                    }

                    dmaEnabled.Value = false;
                    terminalCount.Value = true;
                    endInterrupt.Value = terminalCount.Value && !endInterruptMask.Value;
                    endInterruptMask.Value = false;
                    UpdateInterrupts();
                }

                return false;
            }

            public GPIO IRQ { get; }
            public bool Enabled => dmaEnabled.Value;
            public bool Error => dmaError.Value;
            public bool EndInterrupt => endInterrupt.Value;
            public bool TerminalCount => terminalCount.Value;

            private TransferType ConvertToTransferType(ulong value)
            {
                const ulong MaxTransferSize = (ulong)TransferType.QuadWord;
                ulong transferWordSize = value + 1;
                if(transferWordSize > MaxTransferSize)
                {
                    parent.ErrorLog("{0}: Transfer size set to {1}, but the maximum supported transfer size is {2}. Clamping to {2}",
                        logPrefix, transferWordSize, MaxTransferSize);
                    transferWordSize = MaxTransferSize;
                }

                return (TransferType)transferWordSize;
            }

            private void LoadTransferParameters(bool setRequestingCpu = true)
            {
                if(setRequestingCpu)
                {
                    if(!parent.sysbus.TryGetCurrentCPU(out requestingCpu))
                    {
                        parent.WarningLog("{0}: Could not obtain a CPU context when starting a transaction", logPrefix);
                    }
                }

                switch(dmaMode.Value)
                {
                    case DMAMode.Register:
                    {
                        dmaEnabled.Value = true;
                        var bank = registerSetSelect.Value ? 1 : 0;
                        currentSourceAddress = nextSourceAddresses[bank].Value;
                        currentDestinationAddress = nextDestinationAddresses[bank].Value;
                        currentTransactionByte = nextTransactionBytes[bank].Value;
                        break;
                    }
                    case DMAMode.Link:
                        parent.ErrorLog("{0}: {1} DMA mode is currently not supported, triggering an error", logPrefix, nameof(DMAMode.Link));
                        dmaEnabled.Value = false;
                        dmaError.Value = true;
                        UpdateInterrupts();
                        return;
                    default:
                        throw new Exception("unreachable");
                }
            }

            private void UpdateInterrupts()
            {
                parent.DebugLog("{0}: IRQ: {1}", logPrefix, endInterrupt.Value ? "Set" : "Unset");
                IRQ.Set(endInterrupt.Value);
                parent.UpdateErrorInterrupt();
            }

            private readonly RenesasRZG_DMAC parent;
            private readonly DmaEngine dma;
            private readonly string logPrefix;

            private readonly IValueRegisterField[] nextSourceAddresses;
            private readonly IValueRegisterField[] nextDestinationAddresses;
            private readonly IValueRegisterField[] nextTransactionBytes;

            private DoubleWordRegister statusRegister;
            private ulong currentSourceAddress;
            private ulong currentDestinationAddress;
            private ulong currentTransactionByte;
            private ICPU requestingCpu;
            private TransferType sourceTransferType;
            private TransferType destinationTransferType;
            private IFlagRegisterField sourceAddressCounting;
            private IFlagRegisterField destinationAddressCounting;
            private IFlagRegisterField registerSetSelect;
            private IFlagRegisterField registerSetSwitch;
            private IFlagRegisterField registerSetEnable;
            private IFlagRegisterField dmaEnabled;
            private IFlagRegisterField performFullTransfer;
            private IEnumRegisterField<DMAMode> dmaMode;

            private IFlagRegisterField terminalCount;
            private IFlagRegisterField endInterrupt;
            private IFlagRegisterField endInterruptMask;
            private IFlagRegisterField dmaError;

            private const int AddressRegisterBanks = 2;

            private enum DMAMode
            {
                Register = 0,
                Link     = 1,
            }

            private enum Registers
            {
                Next0SourceAddress          = 0x00, // N0SA_n/nS
                Next0DestinationAddress     = 0x04, // N0DA_n/nS
                Next0TransactionByte        = 0x08, // N0TB_n/nS
                Next1SourceAddress          = 0x0C, // N1SA_n/nS
                Next1DestinationAddress     = 0x10, // N1DA_n/nS
                Next1TransactionByte        = 0x14, // N1TB_n/nS
                CurrentSourceAddress        = 0x18, // CRSA_n/nS
                CurrentDestinationAddress   = 0x1C, // CRDA_n/nS
                CurrentTransactionByte      = 0x20, // CRTB_n/nS
                ChannelStatus               = 0x24, // CHSTAT_n/nS
                ChannelControl              = 0x28, // CHCTRL_n/nS
                ChannelConfiguration        = 0x2C, // CHCFG_n/nS
                ChannelInterval             = 0x30, // CHITVL_n/nS
                ChannelExtension            = 0x34, // CHEXT_n/nS
                NextLinkAddress             = 0x38, // NXLA_n/nS
                CurrentLinkAddress          = 0x3C, // CRLA_n/nS
            }
        }
    }
}
