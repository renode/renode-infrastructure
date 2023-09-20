//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Analog
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class AmbiqApollo4_ADC : BasicDoubleWordPeripheral, IKnownSize
    {
        public AmbiqApollo4_ADC(IMachine machine) : base(machine)
        {
            // This is just a mock. Getters simply read the properties.
            channelDataGettersArray = new Func<uint>[]
            {
                () => Channel0Data, () => Channel1Data, () => Channel2Data, () => Channel3Data, () => Channel4Data, () => Channel5Data,
                () => Channel6Data, () => Channel7Data, () => Channel8Data, () => Channel9Data, () => Channel10Data, () => Channel11Data,
            };

            fifo = new Queue<FifoEntry>();
            interruptStatuses = new bool[InterruptsCount];
            IRQ = new GPIO();

            slots = new Slot[SlotsCount];
            for(int slotNumber = 0; slotNumber < SlotsCount; slotNumber++)
            {
                slots[slotNumber] = new Slot(slotNumber);
            }

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();

            fifo.Clear();
            for(int interruptNumber = 0; interruptNumber < InterruptsCount; interruptNumber++)
            {
                interruptStatuses[interruptNumber] = false;
            }
        }

        public void ScanAllSlots()
        {
            this.Log(LogLevel.Noisy, "Scanning all enabled slots...");
            foreach(var slot in slots)
            {
                if(slot.IsEnabled)
                {
                    var channelNumber = (int)slot.channelSelect.Value;
                    if(TryGetDataFromChannel(channelNumber, out var data))
                    {
                        PushToFifo(data, (uint)slot.Number);
                    }
                }
            }
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
                case Registers.Configuration:
                case Registers.Slot0Configuration:
                case Registers.Slot1Configuration:
                case Registers.Slot2Configuration:
                case Registers.Slot3Configuration:
                case Registers.Slot4Configuration:
                case Registers.Slot5Configuration:
                case Registers.Slot6Configuration:
                case Registers.Slot7Configuration:
                    // Only the configuration changes which stop ADC are allowed if it's enabled.
                    var writeStopsTheModule = (Registers)offset == Registers.Configuration && (value & 1) == 0;
                    if(!moduleEnabled.Value || writeStopsTheModule)
                    {
                        break;
                    }
                    this.Log(LogLevel.Warning, "{0}: Ignoring the write with value: 0x{1:X}; the module has to be disabled first.", (Registers)offset, value);
                    return;
            }
            base.WriteDoubleWord(offset, value);
        }

        public uint Channel0Data { get; set; }
        public uint Channel1Data { get; set; }
        public uint Channel2Data { get; set; }
        public uint Channel3Data { get; set; }
        public uint Channel4Data { get; set; }
        public uint Channel5Data { get; set; }
        public uint Channel6Data { get; set; }
        public uint Channel7Data { get; set; }
        public uint Channel8Data { get; set; }
        public uint Channel9Data { get; set; }
        public uint Channel10Data { get; set; }
        public uint Channel11Data { get; set; }

        public GPIO IRQ { get; }

        public long Size => 0x294;

        private void DefineRegisters()
        {
            Registers.Configuration.Define(this)
                .WithFlag(0, out moduleEnabled, name: "ADCEN", writeCallback: (oldValue, newValue) => { if(oldValue && !newValue) fifo.Clear(); })
                .WithReservedBits(1, 1)
                .WithTaggedFlag("RPTEN", 2)
                .WithTaggedFlag("LPMODE", 3)
                .WithTaggedFlag("CKMODE", 4)
                .WithReservedBits(5, 7)
                .WithFlag(12, out fifoPushEnabled, name: "DFIFORDEN")
                .WithReservedBits(13, 3)
                .WithTag("TRIGSEL", 16, 3)
                .WithTaggedFlag("TRIGPOL", 19)
                .WithTaggedFlag("RPTTRIGSEL", 20)
                .WithReservedBits(21, 3)
                .WithTag("CLKSEL", 24, 2)
                .WithReservedBits(26, 6)
                ;

            Registers.PowerStatus.Define(this)
                .WithTaggedFlag("PWDSTAT", 0)
                .WithReservedBits(1, 31)
                ;

            Registers.SoftwareTrigger.Define(this)
                .WithValueField(0, 8, FieldMode.Write, name: "SWT", writeCallback: (_, newValue) =>
                {
                    // Writing the magic value initiates a scan regardless of the Trigger Select field in the Configuration register.
                    if(newValue == SoftwareTriggerMagicValue)
                    {
                        ScanAllSlots();
                    }
                })
                .WithReservedBits(8, 24)
                ;

            Registers.Slot0Configuration.Define32Many(this, SlotsCount, (register, index) =>
                {
                    register
                        .WithFlag(0, out slots[index].enableFlag, name: $"SLEN{index}")
                        .WithTaggedFlag($"WCEN{index}", 1)
                        .WithReservedBits(2, 6)
                        .WithEnumField(8, 4, out slots[index].channelSelect, name: $"CHSEL{index}", writeCallback: (oldValue, newValue) =>
                        {
                            if((int)newValue >= ChannelsCount)
                            {
                                this.Log(LogLevel.Error, "Slot{0}: Invalid channel select: {1}; the previous value will be kept: {2}",
                                        index, newValue, oldValue);
                                slots[index].channelSelect.Value = oldValue;
                            }
                        })
                        .WithReservedBits(12, 4)
                        .WithTag($"PRMODE{index}", 16, 2)
                        .WithTag($"TRKCYC{index}", 18, 6)
                        .WithTag($"ADSEL{index}", 24, 3)
                        .WithReservedBits(27, 5)
                        ;
                });

            Registers.WindowComparatorUpperLimits.Define(this)
                .WithTag("ULIM", 0, 20)
                .WithReservedBits(20, 12)
                ;

            Registers.WindowComparatorLowerLimits.Define(this)
                .WithTag("LLIM", 0, 20)
                .WithReservedBits(20, 12)
                ;

            Registers.ScaleWindowComparatorLimits.Define(this)
                .WithTaggedFlag("SCWLIMEN", 0)
                .WithReservedBits(1, 31)
                ;

            Registers.Fifo.Define(this)
                .WithValueField(0, 20, name: "DATA", valueProviderCallback: _ => fifo.Count > 0 ? fifo.Peek().Data : 0x0)
                .WithValueField(20, 8, name: "COUNT", valueProviderCallback: _ => (uint)fifo.Count)
                .WithValueField(28, 3, name: "SLOTNUM", valueProviderCallback: _ => fifo.Count > 0 ? fifo.Peek().SlotNumber : 0x0)
                .WithTaggedFlag("RSVD", 31)
                // Writing FIFO register with any value causes the Pop to occur.
                .WithWriteCallback((__, ___) => { if(fifo.Count > 0) _ = fifo.Dequeue(); })
                ;

            Registers.FifoPopRead.Define(this)
                .WithValueField(0, 20, FieldMode.Read, name: "DATA",
                        valueProviderCallback: _ => (fifoPushEnabled.Value && fifo.Count > 0) ? fifo.Peek().Data : 0x0)
                .WithValueField(20, 8, FieldMode.Read, name: "COUNT", valueProviderCallback: _ => (uint)fifo.Count)
                .WithValueField(28, 3, FieldMode.Read, name: "SLOTNUMPR",
                        valueProviderCallback: _ => (fifoPushEnabled.Value && fifo.Count > 0) ? fifo.Peek().SlotNumber : 0x0)
                .WithTaggedFlag("RSVDPR", 31)
                // Reading FIFOPR register causes the Pop to occur if it's enabled in the Configuration register.
                .WithReadCallback((__, ___) => { if(fifoPushEnabled.Value && fifo.Count > 0) _ = fifo.Dequeue(); })
                ;

            Registers.InternalTimerConfiguration.Define(this)
                .WithTag("TIMERMAX", 0, 10)
                .WithReservedBits(10, 6)
                .WithTag("CLKDIV", 16, 3)
                .WithReservedBits(19, 12)
                .WithTaggedFlag("TIMEREN", 31)
                ;

            Registers.ZeroCrossingComparatorConfiguration.Define(this)
                .WithTaggedFlag("ZXEN", 0)
                .WithReservedBits(1, 3)
                .WithTaggedFlag("ZXCHANSEL", 4)
                .WithReservedBits(5, 27)
                ;

            Registers.ZeroCrossingComparatorLimits.Define(this)
                .WithTag("LZXC", 0, 12)
                .WithReservedBits(12, 4)
                .WithTag("UZXC", 16, 12)
                .WithReservedBits(28, 4)
                ;

            Registers.PGAGainConfiguration.Define(this)
                .WithTaggedFlag("PGACTRLEN", 0)
                .WithReservedBits(1, 3)
                .WithTaggedFlag("UPDATEMODE", 4)
                .WithReservedBits(5, 27)
                ;

            Registers.PGAGainCodes.Define(this)
                .WithTag("LGA", 0, 7)
                .WithReservedBits(7, 1)
                .WithTag("HGADELTA", 8, 7)
                .WithReservedBits(15, 1)
                .WithTag("LGB", 16, 7)
                .WithReservedBits(23, 1)
                .WithTag("HGBDELTA", 24, 7)
                .WithReservedBits(31, 1)
                ;

            Registers.SaturationComparatorConfiguration.Define(this)
                .WithTaggedFlag("SATEN", 0)
                .WithReservedBits(1, 3)
                .WithTaggedFlag("SATCHANSEL", 4)
                .WithReservedBits(5, 27)
                ;

            Registers.SaturationComparatorLimits.Define(this)
                .WithTag("LSATC", 0, 12)
                .WithReservedBits(12, 4)
                .WithTag("USATC", 16, 12)
                .WithReservedBits(28, 4)
                ;

            Registers.SaturationComparatorEventCounterLimits.Define(this, 0x00010001)
                .WithTag("SATCAMAX", 0, 12)
                .WithReservedBits(12, 4)
                .WithTag("SATCBMAX", 16, 12)
                .WithReservedBits(28, 4)
                ;

            Registers.SaturationComparatorEventCounterClear.Define(this)
                .WithTaggedFlag("SATCACLR", 0)
                .WithTaggedFlag("SATCBCLR", 1)
                .WithReservedBits(2, 30)
                ;

            Registers.InterruptEnable.Define(this)
                .WithFlags(0, 12, out interruptEnableFlags, name: "INTENx")
                .WithReservedBits(12, 20)
                .WithChangeCallback((_, __) => UpdateIRQ())
                ;

            Registers.InterruptStatus.Define(this)
                .WithFlags(0, 12, FieldMode.Read, name: "INTSTATx", valueProviderCallback: (interrupt, _) => interruptStatuses[interrupt])
                .WithReservedBits(12, 20)
                ;

            Registers.InterruptClear.Define(this)
                .WithFlags(0, 12, FieldMode.Write, name: "INTCLRx",
                        writeCallback: (interrupt, _, newValue) => { if(newValue) SetInterruptStatus((Interrupts)interrupt, false); })
                .WithReservedBits(12, 20)
                ;

            Registers.InterruptSet.Define(this)
                .WithFlags(0, 12, FieldMode.Write, name: "INTSETx",
                        writeCallback: (interrupt, _, newValue) => { if(newValue) SetInterruptStatus((Interrupts)interrupt, true); })
                .WithReservedBits(12, 20)
                ;

            Registers.DMATriggerEnable.Define(this)
                .WithTaggedFlag("DFIFO75", 0)
                .WithTaggedFlag("DFIFOFULL", 1)
                .WithReservedBits(2, 30)
                ;

            Registers.DMATriggerStatus.Define(this)
                .WithTaggedFlag("D75STAT", 0)
                .WithTaggedFlag("DFULLSTAT", 1)
                .WithReservedBits(2, 30)
                ;

            Registers.DMAConfiguration.Define(this)
                .WithTaggedFlag("DMAEN", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("DMADIR", 2)
                .WithReservedBits(3, 5)
                .WithTaggedFlag("DMAPRI", 8)
                .WithTaggedFlag("DMADYNPRI", 9)
                .WithReservedBits(10, 7)
                .WithTaggedFlag("DMAMSK", 17)
                .WithTaggedFlag("DPWROFF", 18)
                .WithReservedBits(19, 13)
                ;

            Registers.DMATotalTransferCount.Define(this)
                .WithReservedBits(0, 2)
                .WithTag("TOTCOUNT", 2, 16)
                .WithReservedBits(18, 14)
                ;

            Registers.DMATargetAddress.Define(this, 0x10000000)
                .WithTag("LTARGADDR", 0, 28)
                .WithTag("UTARGADDR", 28, 4)
                ;

            Registers.DMAStatus.Define(this)
                .WithTaggedFlag("DMATIP", 0)
                .WithTaggedFlag("DMACPL", 1)
                .WithTaggedFlag("DMAERR", 2)
                .WithReservedBits(3, 29)
                ;
        }

        private void PushToFifo(uint data, uint slotNumber)
        {
            this.Log(LogLevel.Noisy, "Data pushed to Fifo for slot#{0}: 0x{1:X}", slotNumber, data);
            fifo.Enqueue(new FifoEntry(data, slotNumber));
            SetInterruptStatus(Interrupts.ConversionComplete, true);
        }

        private void SetInterruptStatus(Interrupts interrupt, bool value)
        {
            if(interruptStatuses[(int)interrupt] != value)
            {
                this.NoisyLog("{0} interrupt status {1}", interrupt, value ? "set" : "reset");
                interruptStatuses[(int)interrupt] = value;
                UpdateIRQ();
            }
        }

        private bool TryGetDataFromChannel(int channelNumber, out uint data)
        {
            data = 0x0;
            if(channelNumber >= ChannelsCount)
            {
                this.Log(LogLevel.Warning, "Invalid channel number: {0}", channelNumber);
                return false;
            }

            var channelDataGet = channelDataGettersArray[channelNumber];
            data = channelDataGet();
            return true;
        }

        private void UpdateIRQ()
        {
            var newStatus = false;
            for(int i = 0; i < InterruptsCount; i++)
            {
                if(interruptStatuses[i] && interruptEnableFlags[i].Value)
                {
                    newStatus = true;
                    break;
                }
            }

            if(newStatus != IRQ.IsSet)
            {
                this.NoisyLog("IRQ {0}", newStatus ? "set" : "reset");
                IRQ.Set(newStatus);
            }
        }

        private IFlagRegisterField fifoPushEnabled;
        private IFlagRegisterField[] interruptEnableFlags;
        private IFlagRegisterField moduleEnabled;

        private readonly Func<uint>[] channelDataGettersArray;
        private readonly Queue<FifoEntry> fifo;
        private readonly bool[] interruptStatuses;
        private readonly Slot[] slots;

        private const int ChannelsCount = 12;
        private const int InterruptsCount = 12;
        private const int SlotsCount = 8;
        private const int SoftwareTriggerMagicValue = 0x37;

        private struct FifoEntry
        {
            public FifoEntry(uint data, uint slotNumber)
            {
                Data = data;
                SlotNumber = slotNumber;
            }

            public uint Data;
            public uint SlotNumber;
        }

        private class Slot
        {
            public Slot(int number)
            {
                Number = number;
            }

            public bool IsEnabled => enableFlag.Value;
            public int Number { get; }

            public IEnumRegisterField<Channels> channelSelect;
            public IFlagRegisterField enableFlag;
        }

        private enum Channels
        {
            SingleEndedExternalGPIOPad16 = 0x0,
            SingleEndedExternalGPIOPad29 = 0x1,
            SingleEndedExternalGPIOPad11 = 0x2,
            SingleEndedExternalGPIOPad31 = 0x3,
            SingleEndedExternalGPIOPad32 = 0x4,
            SingleEndedExternalGPIOPad33 = 0x5,
            SingleEndedExternalGPIOPad34 = 0x6,
            SingleEndedExternalGPIOPad35 = 0x7,
            InternalTemperatureSensor = 0x8,
            InternalVoltageDivideByThreeConnection = 0x9,
            AnalogTestmux = 0xA,
            InputVSS = 0xB,
        }

        private enum Interrupts
        {
            // For values based on multiple scans, this is set only when the average value is pushed to FIFO.
            ConversionComplete = 0,
            ScanComplete = 1,
            Fifo75PercentFull = 2,
            Fifo100PercentFull = 3,
            WindowComparatorVoltageExcursion = 4,
            WindowComparatorVoltageIncursion = 5,
            DmaTransferComplete = 6,
            DmaErrorCondition = 7,
            ZeroCrossingChannelA = 8,
            ZeroCrossingChannelB = 9,
            SaturationChannelA = 10,
            SaturationChannelB = 11,
        }

        private enum Registers : long
        {
            Configuration = 0x0,
            PowerStatus = 0x4,
            SoftwareTrigger = 0x8,
            Slot0Configuration = 0xC,
            Slot1Configuration = 0x10,
            Slot2Configuration = 0x14,
            Slot3Configuration = 0x18,
            Slot4Configuration = 0x1C,
            Slot5Configuration = 0x20,
            Slot6Configuration = 0x24,
            Slot7Configuration = 0x28,
            WindowComparatorUpperLimits = 0x2C,
            WindowComparatorLowerLimits = 0x30,
            ScaleWindowComparatorLimits = 0x34,
            Fifo = 0x38,
            FifoPopRead = 0x3C,
            InternalTimerConfiguration = 0x40,
            ZeroCrossingComparatorConfiguration = 0x60,
            ZeroCrossingComparatorLimits = 0x64,
            PGAGainConfiguration = 0x68,
            PGAGainCodes = 0x6C,
            SaturationComparatorConfiguration = 0xA4,
            SaturationComparatorLimits = 0xA8,
            SaturationComparatorEventCounterLimits = 0xAC,
            SaturationComparatorEventCounterClear = 0xB0,
            InterruptEnable = 0x200,
            InterruptStatus = 0x204,
            InterruptClear = 0x208,
            InterruptSet = 0x20C,
            DMATriggerEnable = 0x240,
            DMATriggerStatus = 0x244,
            DMAConfiguration = 0x280,
            DMATotalTransferCount = 0x288,
            DMATargetAddress = 0x28C,
            DMAStatus = 0x290,
        }
    }
}
