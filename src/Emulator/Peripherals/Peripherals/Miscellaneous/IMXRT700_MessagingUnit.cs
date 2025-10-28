//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class IMXRT700_MessagingUnit : IBusPeripheral
    {
        public IMXRT700_MessagingUnit()
        {
            sync = new object();
            aInstanceData = new InstanceData("aInstance");
            bInstanceData = new InstanceData("bInstance");
            AInstanceIRQ = new GPIO();
            BInstanceIRQ = new GPIO();
            aRegionRegisters = DefineRegionRegisters(mySide: aInstanceData, otherSide: bInstanceData, myIRQ: AInstanceIRQ, otherIRQ: BInstanceIRQ);
            bRegionRegisters = DefineRegionRegisters(mySide: bInstanceData, otherSide: aInstanceData, myIRQ: BInstanceIRQ, otherIRQ: AInstanceIRQ);

            Reset();
        }

        public void Reset()
        {
            aRegionRegisters.Reset();
            bRegionRegisters.Reset();

            UpdateInterrupts();
        }

        [ConnectionRegion("aInstance")]
        public uint ReadDoubleWordFromAInstance(long offset)
        {
            this.DebugLog("Reading value from aInstance region");
            if(RequiresLock(offset))
            {
                lock(sync)
                {
                    return aRegionRegisters.Read(offset);
                }
            }
            else
            {
                return aRegionRegisters.Read(offset);
            }
        }

        [ConnectionRegion("aInstance")]
        public void WriteDoubleWordFromAInstance(long offset, uint value)
        {
            this.DebugLog("Writing value to aInstance region");
            if(RequiresLock(offset))
            {
                lock(sync)
                {
                    aRegionRegisters.Write(offset, value);
                }
            }
            else
            {
                aRegionRegisters.Write(offset, value);
            }
        }

        [ConnectionRegion("bInstance")]
        public uint ReadDoubleWordFromBInstance(long offset)
        {
            this.DebugLog("Reading value from bInstance region");
            if(RequiresLock(offset))
            {
                lock(sync)
                {
                    return bRegionRegisters.Read(offset);
                }
            }
            else
            {
                return bRegionRegisters.Read(offset);
            }
        }

        [ConnectionRegion("bInstance")]
        public void WriteDoubleWordFromBInstance(long offset, uint value)
        {
            this.DebugLog("Writing value to bInstance region");
            if(RequiresLock(offset))
            {
                lock(sync)
                {
                    bRegionRegisters.Write(offset, value);
                }
            }
            else
            {
                bRegionRegisters.Write(offset, value);
            }
        }

        public GPIO AInstanceIRQ { get; }

        public GPIO BInstanceIRQ { get; }

        private static bool RequiresLock(long offset) =>
            offset == (long)Registers.GeneralPurposeControl ||
            offset == (long)Registers.GeneralPurposeStatus;

        private DoubleWordRegisterCollection DefineRegionRegisters(InstanceData mySide, InstanceData otherSide, GPIO myIRQ, GPIO otherIRQ)
        {
            var collection = new DoubleWordRegisterCollection(this);

            Registers.VersionID.Define(collection, 0x0309000F)
                .WithTag("MAJOR", 24, 8)
                .WithTag("MINOR", 16, 8)
                .WithTag("FEATURE", 0, 16);
            Registers.Parameter.Define(collection, 0x03040404)
               .WithTag("FLAG_WIDTH", 24, 8)
               .WithTag("GIR_NUM", 16, 8)
               .WithTag("RR_NUM", 8, 8)
               .WithTag("TR_NUM", 0, 8);
            Registers.Control.Define(collection)
                .WithReservedBits(1, 31)
                .WithTaggedFlag("MUR", 0);
            Registers.Status.Define(collection)
                .WithReservedBits(8, 24)
                .WithTaggedFlag("CEP", 7)
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => mySide.ReceiveFullPending, name: "RFP")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => mySide.TransmitEmptyPending, name: "TEP")
                .WithTaggedFlag("GIRP", 4)
                .WithTaggedFlag("FUP", 3)
                .WithTaggedFlag("EP", 2)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("MURS", 0);
            Registers.CoreControl0.Define(collection, 0x14)
                .WithReservedBits(1, 31)
                .WithTaggedFlag("NMI", 0);
            Registers.CoreInterruptEnable0.Define(collection)
                .WithReservedBits(6, 26)
                .WithTaggedFlag("WAITIE", 5)
                .WithReservedBits(0, 5);
            Registers.CoreStickyStatus0.Define(collection)
                .WithReservedBits(6, 26)
                .WithTaggedFlag("WAITIE", 5)
                .WithReservedBits(1, 4)
                .WithTaggedFlag("NMIC", 0);
            Registers.CoreStatus0.Define(collection)
                .WithReservedBits(6, 26)
                .WithTaggedFlag("WAIT", 5)
                .WithReservedBits(0, 5); ;
            Registers.FlagControl.Define(collection)
                .WithReservedBits(FLAGS_COUNT, 29)
                .WithFlags(0, FLAGS_COUNT, out mySide.Flags, name: "Fn");
            Registers.FlagStatus.Define(collection)
                .WithReservedBits(FLAGS_COUNT, 29)
                .WithFlags(0, FLAGS_COUNT, FieldMode.Read, valueProviderCallback: (idx, _) => otherSide.Flags[idx].Value, name: "Fn");
            Registers.GeneralPurposeInterruptEnable.Define(collection)
                .WithReservedBits(GP_FLAGS_COUNT, 28)
                .WithFlags(0, GP_FLAGS_COUNT, out mySide.GeneralPurposeInterruptEnable, name: "GIEn");
            Registers.GeneralPurposeControl.Define(collection)
                .WithReservedBits(4, 28)
                .WithFlags(0, GP_FLAGS_COUNT, out mySide.GeneralPurposeControl,
                    writeCallback: (index, _, value) =>
                    {
                        if(value)
                        {
                            mySide.GeneralPurposeControl[index].Value = true;
                            otherSide.GeneralPurposeStatus[index].Value = true;
                            // Trigger interrupt on other side iff other.GIER is set
                            if(otherSide.GeneralPurposeInterruptEnable[index].Value)
                            {
                                otherIRQ.Set();
                            }
                        }
                    },
                    name: "GIRn"
                );
            Registers.GeneralPurposeStatus.Define(collection)
                .WithReservedBits(4, width: 28)
                .WithFlags(0, GP_FLAGS_COUNT, out mySide.GeneralPurposeStatus,
                    writeCallback: (index, _, value) =>
                    {
                        if(value)
                        {
                            mySide.GeneralPurposeStatus[index].Value = false;
                            otherSide.GeneralPurposeControl[index].Value = false;
                            myIRQ.Unset();
                        }
                    },
                    name: "GIPn"
                );
            Registers.TransmitControl.Define(collection)
                .WithReservedBits(TXRX_WORDS_COUNT, 28)
                .WithFlags(0, TXRX_WORDS_COUNT, out mySide.TransmitInterruptEnable, name: "TIEn")
                .WithChangeCallback((_, __) => UpdateInterrupts());
            Registers.TransmitStatus.Define(collection, 0xF)
                .WithReservedBits(TXRX_WORDS_COUNT, 28)
                .WithFlags(0, TXRX_WORDS_COUNT, out mySide.TransmitEmptyStatus, FieldMode.Read, name: "TEn");
            Registers.ReceiveControl.Define(collection)
                .WithReservedBits(TXRX_WORDS_COUNT, 28)
                .WithFlags(0, TXRX_WORDS_COUNT, out mySide.ReceiveInterruptEnable, name: "RIEn")
                .WithChangeCallback((_, __) => UpdateInterrupts());
            Registers.ReceiveStatus.Define(collection)
                .WithReservedBits(TXRX_WORDS_COUNT, 28)
                .WithFlags(0, TXRX_WORDS_COUNT, out mySide.ReceiveFullStatus, FieldMode.Read, name: "REn");
            Registers.Transmit0.DefineMany(collection, 4, (register, index) =>
            {
                register
                    .WithValueField(0, 32, out mySide.TransmitWords[index], FieldMode.Write, name: "TR_DATA")
                    .WithWriteCallback((_, __) =>
                    {
                        mySide.TransmitEmptyStatus[index].Value = false;
                        otherSide.ReceiveFullStatus[index].Value = true;
                        UpdateInterrupts();
                    });
            });
            Registers.Receive0.DefineMany(collection, 4, (register, index) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => otherSide.TransmitWords[index].Value, name: "RR_DATA")
                    .WithReadCallback((_, __) =>
                    {
                        if(otherSide.TransmitEmptyStatus[index].Value)
                        {
                            this.WarningLog("Reading the word from {0}, but there is no transmit in progress", otherSide.InstanceName);
                        }
                        mySide.ReceiveFullStatus[index].Value = false;
                        otherSide.TransmitEmptyStatus[index].Value = true;
                        UpdateInterrupts();
                    });
            });

            return collection;
        }

        private void UpdateInterrupts()
        {
            var aInstanceIRQStatus = aInstanceData.ReceiveFullPending | aInstanceData.TransmitEmptyPending;
            var bInstanceIRQStatus = bInstanceData.ReceiveFullPending | bInstanceData.TransmitEmptyPending;
            this.DebugLog("Setting {0} to {1} and {2} to {3}", nameof(aInstanceIRQStatus), aInstanceIRQStatus, nameof(bInstanceIRQStatus), bInstanceIRQStatus);
            AInstanceIRQ.Set(aInstanceIRQStatus);
            BInstanceIRQ.Set(bInstanceIRQStatus);
        }

        private readonly InstanceData aInstanceData;
        private readonly InstanceData bInstanceData;
        private readonly DoubleWordRegisterCollection aRegionRegisters;
        private readonly DoubleWordRegisterCollection bRegionRegisters;
        private readonly object sync;
        private const int FLAGS_COUNT = 3;
        private const int TXRX_WORDS_COUNT = 4;
        private const int GP_FLAGS_COUNT = 4;

        private class InstanceData
        {
            public InstanceData(string instanceName)
            {
                InstanceName = instanceName;
                TransmitWords = new IValueRegisterField[TXRX_WORDS_COUNT];
            }

            public string InstanceName { get; }

            public bool ReceiveFullPending
            {
                get
                {
                    var receiveFull = false;
                    for(int i = 0; i < TXRX_WORDS_COUNT; i++)
                    {
                        receiveFull |= ReceiveInterruptEnable[i].Value && ReceiveFullStatus[i].Value;
                    }
                    return receiveFull;
                }
            }

            public bool TransmitEmptyPending
            {
                get
                {
                    var transmitEmpty = false;
                    for(int i = 0; i < TXRX_WORDS_COUNT; i++)
                    {
                        transmitEmpty |= TransmitInterruptEnable[i].Value && TransmitEmptyStatus[i].Value;
                    }
                    return transmitEmpty;
                }
            }

            public IFlagRegisterField[] TransmitInterruptEnable;
            public IFlagRegisterField[] ReceiveInterruptEnable;

            public IFlagRegisterField[] Flags;
            public IValueRegisterField[] TransmitWords;
            public IFlagRegisterField[] TransmitEmptyStatus;
            public IFlagRegisterField[] ReceiveFullStatus;

            public IFlagRegisterField[] GeneralPurposeControl;
            public IFlagRegisterField[] GeneralPurposeStatus;
            public IFlagRegisterField[] GeneralPurposeInterruptEnable;
        }

        private enum Registers
        {
            VersionID = 0x0,
            Parameter = 0x4,
            Control = 0x8,
            Status = 0xC,
            CoreControl0 = 0x10,
            CoreInterruptEnable0 = 0x14,
            CoreStickyStatus0 = 0x18,
            CoreStatus0 = 0x1C,
            FlagControl = 0x100,
            FlagStatus = 0x104,
            GeneralPurposeInterruptEnable = 0x110,
            GeneralPurposeControl = 0x114,
            GeneralPurposeStatus = 0x118,
            TransmitControl = 0x120,
            TransmitStatus = 0x124,
            ReceiveControl = 0x128,
            ReceiveStatus = 0x12C,
            Transmit0 = 0x200,
            Transmit1 = 0x204,
            Transmit2 = 0x208,
            Transmit3 = 0x20C,
            Receive0 = 0x280,
            Receive1 = 0x284,
            Receive2 = 0x288,
            Receive3 = 0x28C
        }
    }
}