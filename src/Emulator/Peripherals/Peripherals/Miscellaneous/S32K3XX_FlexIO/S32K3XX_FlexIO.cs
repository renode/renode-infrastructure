//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class S32K3XX_FlexIO : BasicDoubleWordPeripheral, IPeripheralContainer<IEndpoint, NullRegistrationPoint>, IKnownSize,
        IResourceBlockOwner
    {
        public S32K3XX_FlexIO(IMachine machine) : base(machine)
        {
            timers = Timer.BuildRegisters(this, TimerCount);
            TimersManager = new ResourceBlocksManager<Timer>(this, "timer", timers);
            foreach(var timer in timers)
            {
                timer.AnyInterruptChanged += UpdateInterrupt;
            }

            shifters = Shifter.BuildRegisters(this, ShifterCount, TimersManager);
            ShiftersManager = new ResourceBlocksManager<Shifter>(this, "shifter", shifters);
            foreach(var shifter in shifters)
            {
                shifter.AnyInterruptChanged += UpdateInterrupt;
            }

            DefineRegisters();
        }

        public override void Reset()
        {
            inReset.Value = false;
            enabled.Value = false;
            InternalReset();
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(IEndpoint peripheral)
        {
            return endpoints.Select(_ => NullRegistrationPoint.Instance);
        }

        public void Register(IEndpoint peripheral, NullRegistrationPoint registrationPoint)
        {
            if(endpoints.Contains(peripheral))
            {
                throw new RegistrationException("The specified endpoint is already registered.");
            }
            endpoints.Add(peripheral);
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            peripheral.RegisterInFlexIO(this);
        }

        public void Unregister(IEndpoint peripheral)
        {
            if(!endpoints.Contains(peripheral))
            {
                throw new RegistrationException("The specified endpoint was never registered.");
            }
            endpoints.Remove(peripheral);
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        public IEnumerable<IRegistered<IEndpoint, NullRegistrationPoint>> Children => endpoints.Select(x => Registered.Create(x, NullRegistrationPoint.Instance));
        public uint Frequency { get; set; }
        public GPIO IRQ { get; } = new GPIO();
        public long Size => 0x4000;

        public ResourceBlocksManager<Shifter> ShiftersManager { get; }
        public ResourceBlocksManager<Timer> TimersManager { get; }

        private void DefineRegisters()
        {
            Registers.VersionID.Define(this, 0x02010003)
                .WithValueField(24, 8, FieldMode.Read, name: "MAJOR (Major Version)")
                .WithValueField(16, 8, FieldMode.Read, name: "MINOR (Minor Version)")
                .WithValueField(0, 16, FieldMode.Read, name: "FEATURE (Feature Specification)");

            Registers.Parameter.Define(this, 0x04000000)
                .WithTag("TRIGGER (Trigger Number)", 24, 8)
                .WithValueField(16, 8, FieldMode.Read, name: "PIN (Pin Number)",
                    valueProviderCallback: _ => PinCount
                )
                .WithValueField(8, 8, FieldMode.Read, name: "TIMER (Timer Number)",
                    valueProviderCallback: _ => (ulong)timers.Count
                )
                .WithValueField(0, 8, FieldMode.Read, name: "SHIFTER (Shifter Number)",
                    valueProviderCallback: _ => (ulong)shifters.Count
                );

            Registers.Control.Define(this, softResettable: false)
                .WithReservedBits(31, 1)
                .WithTaggedFlag("DBGE (Debug Enable)", 30)
                .WithReservedBits(2, 28)
                .WithFlag(1, out inReset, name: "SWRSRT (Software Reset)",
                    changeCallback: (_, val) => { if(val) InternalReset(); }
                )
                .WithFlag(0, out enabled, name: "FLEXEN (Enable)");

            Registers.PinState.Define(this)
                .WithTag("PDI (Pin Data Input)", 0, 32);

            Registers.ShifterStatusDMAEnable.Define(this)
                .WithReservedBits(8, 24)
                .WithTag("SSDE (Shifter Status DMA Enable)", 0, 8);

            Registers.TimerStatusDMAEnable.Define(this)
                .WithReservedBits(8, 24)
                .WithTag("TSDE (Timer Status DMA Enable)", 0, 8);

            Registers.ShifterState.Define(this)
                .WithReservedBits(3, 29)
                .WithTag("STATE (Current State Pointer)", 0, 3);

            Registers.TriggerStatus.Define(this)
                .WithReservedBits(4, 28)
                .WithTag("ETSF (External Trigger Status Flag)", 0, 4);

            Registers.ExternalTriggerInterruptEnable.Define(this)
                .WithReservedBits(4, 28)
                .WithTag("TRIE (External Trigger Interrupt Enable)", 0, 4);

            Registers.PinStatus.Define(this)
                .WithTag("PSF (Pin Status Flag)", 0, PinCount);

            Registers.PinInterruptEnable.Define(this)
                .WithTag("PSIE (Pin Status Interrupt Enable)", 0, PinCount);

            Registers.PinRisingEdgeEnable.Define(this)
                .WithTag("PRE (Pin Rising Edge)", 0, PinCount);

            Registers.PinFallingEdgeEnable.Define(this)
                .WithTag("PFE (Pin Falling Edge)", 0, PinCount);

            Registers.PinOutputData.Define(this)
                .WithTag("OUTD (Output Data)", 0, PinCount);

            Registers.PinOutputEnable.Define(this)
                .WithTag("OUTE (Output Enable)", 0, PinCount);

            Registers.PinOutputDisable.Define(this)
                .WithTag("OUTDIS (Output Disable)", 0, PinCount);

            Registers.PinOutputClear.Define(this)
                .WithTag("OUTCLR (Output Clear)", 0, PinCount);

            Registers.PinOutputSet.Define(this)
                .WithTag("OUTSET (Output Set)", 0, PinCount);

            Registers.PinOutputToggle.Define(this)
                .WithTag("OUTTOG (Output Toggle)", 0, PinCount);
        }

        private void InternalReset()
        {
            base.Reset();
            foreach(var shifter in shifters)
            {
                shifter.Reset();
            }
            UpdateInterrupt();
        }

        private void UpdateInterrupt(bool forceInterrupt = false)
        {
            var newValue = forceInterrupt || ResourceBlocks.SelectMany(x => x.Interrupts).Any(x => x.MaskedFlag);
            if(newValue != IRQ.IsSet)
            {
                this.Log(LogLevel.Debug, "Setting the IRQ interrupt to {0}", newValue);
                IRQ.Set(newValue);
            }
        }

        private IEnumerable<ResourceBlock> ResourceBlocks => shifters.Concat<ResourceBlock>(timers);

        private IFlagRegisterField enabled;
        private IFlagRegisterField inReset;

        private readonly ISet<IEndpoint> endpoints = new HashSet<IEndpoint>();
        private readonly IReadOnlyList<Shifter> shifters;
        private readonly IReadOnlyList<Timer> timers;

        private const int PinCount = 32;
        private const int ShifterCount = 8;
        private const int TimerCount = 8;

        public enum Registers
        {
            VersionID = 0x0, // VERID
            Parameter = 0x4, // PARAM
            Control = 0x8, // CTRL
            PinState = 0xC, // PIN
            ShifterStatus = 0x10, // SHIFTSTAT
            ShifterError = 0x14, // SHIFTERR
            TimerStatus = 0x18, // TIMSTAT
            ShifterStatusInterruptEnable = 0x20, // SHIFTSIEN
            ShifterErrorInterruptEnable = 0x24, // SHIFTEIEN
            TimerInterruptEnable = 0x28, // TIMIEN
            ShifterStatusDMAEnable = 0x30, // SHIFTSDEN
            TimerStatusDMAEnable = 0x38, // TIMERSDEN
            ShifterState = 0x40, // SHIFTSTATE
            TriggerStatus = 0x48, // TRGSTAT
            ExternalTriggerInterruptEnable = 0x4C, // TRIGIEN
            PinStatus = 0x50, // PINSTAT
            PinInterruptEnable = 0x54, // PINIEN
            PinRisingEdgeEnable = 0x58, // PINREN
            PinFallingEdgeEnable = 0x5C, // PINFEN
            PinOutputData = 0x60, // PINOUTD
            PinOutputEnable = 0x64, // PINOUTE
            PinOutputDisable = 0x68, // PINOUTDIS
            PinOutputClear = 0x6C, // PINOUTCLR
            PinOutputSet = 0x70, // PINOUTSET
            PinOutputToggle = 0x74, // PINOUTTOG
            ShifterControl0 = 0x80, // SHIFTCTL0
            ShifterControl7 = 0x9C, // SHIFTCTL7
            ShifterConfiguration0 = 0x100, // SHIFTCFG0
            ShifterConfiguration7 = 0x11C, // SHIFTCFG7
            ShifterBuffer0 = 0x200, // SHIFTBUF0
            ShifterBuffer7 = 0x21C, // SHIFTBUF7
            ShifterBuffer0BitSwapped = 0x280, // SHIFTBUFBIS0
            ShifterBuffer7BitSwapped = 0x29C, // SHIFTBUFBIS7
            ShifterBuffer0ByteSwapped = 0x300, // SHIFTBUFBYS0
            ShifterBuffer7ByteSwapped = 0x31C, // SHIFTBUFBYS7
            ShifterBuffer0BitByteSwapped = 0x380, // SHIFTBUFBBS0
            ShifterBuffer7BitByteSwapped = 0x39C, // SHIFTBUFBBS7
            TimerControl0 = 0x400, // TIMCTL0
            TimerControl7 = 0x41C, // TIMCTL7
            TimerConfiguration0 = 0x480, // TIMCFG0
            TimerConfiguration7 = 0x49C, // TIMCFG7
            TimerCompare0 = 0x500, // TIMCMP0
            TimerCompare7 = 0x51C, // TIMCMP7
            ShifterBuffer0NibbleByteSwapped = 0x680, // SHIFTBUFNBS0
            ShifterBuffer7NibbleByteSwapped = 0x69C, // SHIFTBUFNBS7
            ShifterBuffer0HalfwordSwapped = 0x700, // SHIFTBUFHWS0
            ShifterBuffer7HalfwordSwapped = 0x71C, // SHIFTBUFHWS7
            ShifterBuffer0NibbleSwapped = 0x780, // SHIFTBUFNIS0
            ShifterBuffer7NibbleSwapped = 0x79C, // SHIFTBUFNIS7
            ShifterBuffer0OddEvenSwapped = 0x800, // SHIFTBUFOES0
            ShifterBuffer7OddEvenSwapped = 0x81C, // SHIFTBUFOES7
            ShifterBuffer0EvenOddSwapped = 0x880, // SHIFTBUFEOS0
            ShifterBuffer7EvenOddSwapped = 0x89C, // HIFTBUFEOS7
            ShifterBuffer0HalfWordByteSwapped = 0x900, // SHIFTBUFHBS0
            ShifterBuffer7HalfwordByteSwapped = 0x91C, // SHIFTBUFHBS7
        }
    }
}
