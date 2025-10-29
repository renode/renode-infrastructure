//
// Copyright (c) 2010-2025 Antmicro
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
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class VeeR_EL2_PIC : BasicDoubleWordPeripheral, IIRQController, IKnownSize, IBytePeripheral, IWordPeripheral
    {
        // interruptSourcesCount: RV_PIC_TOTAL_INT
        public VeeR_EL2_PIC(IMachine machine, VeeR_EL2 cpu, uint interruptSourcesCount = InterruptSourcesMax, bool notAllGatewaysAreConfigurable = false)
                : base(machine)
        {
            this.cpu = cpu;
            cpu.RegisterPIC(this);

            if(interruptSourcesCount < InterruptSourcesMin || interruptSourcesCount > InterruptSourcesMax)
            {
                throw new ConstructionException($"Invalid {nameof(interruptSourcesCount)} value; it should be between "
                    + $"{InterruptSourcesMin} and {InterruptSourcesMax}, was: {interruptSourcesCount}");
            }
            InterruptSourcesCount = interruptSourcesCount;

            interruptSources = new InterruptSource[interruptSourcesCount];
            DefineMemoryMappedRegisters(interruptSourcesCount, defineAllGatewayRegisters: !notAllGatewaysAreConfigurable);
            DefineCSRs();

            // According to the manual, "A register is only present for interrupt source S if a configurable gateway is instantiated."
            // so configurable gateways can be instantiated for an arbitrary subset of interrupt sources.
            if(notAllGatewaysAreConfigurable)
            {
                this.InfoLog("Gateway registers weren't defined due to the '{0}' constructor parameter, "
                        + "use '{1}' to add gateway registers if any interrupt source should have them",
                        nameof(notAllGatewaysAreConfigurable), nameof(AddGatewayRegistersForInterruptSources)
                );
            }
        }

        // The `notAllGatewaysAreConfigurable` constructor parameter combined with this method can be used to handle cases of configurable
        // gateways instantiated for some, but not all, interrupt sources.
        public void AddGatewayRegistersForInterruptSources(uint fromId, uint toId)
        {
            // Further code assumes `firstId` isn't greater than `lastId`.
            var firstId = fromId <= toId ? fromId : toId;
            var lastId = fromId <= toId ? toId : fromId;

            if(firstId == 0 || lastId > LastInterruptSourceId)
            {
                throw new RecoverableException($"Range contains invalid IDs, it must not contain IDs outside 1-{LastInterruptSourceId} range");
            }

            for(var id = firstId; id <= lastId; id++)
            {
                interruptSources[id - 1].DefineGatewayRegisters();
            }
        }

        public void OnGPIO(int number, bool value)
        {
            if(number <= 0 || number > LastInterruptSourceId)
            {
                // Note that `interruptSources` indexes are source IDs subtracted by 1 because of the non-existent source ID 0.
                this.WarningLog("Ignoring state change received from external source with an invalid ID: {0} (valid IDs are from 1 to {1})",
                        number, LastInterruptSourceId);
                return;
            }

            lock(interruptSources)
            {
                // UpdateIRQs is called if, from a perspective of the given interrupt source, IRQ or MaxPriorityIRQ should have a different state.
                interruptSources[number - 1].OnSignalChange(signalHigh: value);
            }
        }

        public string[,] PrintInterruptSourcesInformation(bool onlyEnabledOrPending = false)
        {
            var table = new Table().AddRow("Id", "Enabled", "Pending", "Priority Level");
            lock(interruptSources)
            {
                foreach(var source in interruptSources)
                {
                    if(!onlyEnabledOrPending || source.IsEnabled || source.IsPending)
                    {
                        var elements = new object[] { source.Id, source.IsEnabled, source.IsPending, source.PriorityLevel };
                        table.AddRow(elements.Select(e => e.ToString()).ToArray());
                    }
                }
            }
            return table.ToArray();
        }

        public virtual byte ReadByte(long offset)
        {
            RaiseLoadAccessFault(offset, SysbusAccessWidth.Byte);
            return 0x0;
        }

        public virtual ushort ReadWord(long offset)
        {
            RaiseLoadAccessFault(offset, SysbusAccessWidth.Word);
            return 0x0;
        }

        public override uint ReadDoubleWord(long offset)
        {
            if(!RegistersCollection.HasRegisterAtOffset(offset))
            {
                // Warnings, typically used in this case, aren't suitable because generally it isn't an issue for PIC:
                // > Accessing unused addresses within the 32KB PIC address range do not trigger an unmapped address exception.
                // > Reads to unmapped addresses return 0, writes to unmapped addresses are silently dropped.
                // Noisy level is used so that such accesses are still noted in the log.
                this.NoisyLog("Unhandled read from offset 0x{0:X}", offset);
                return 0x0;
            }
            return base.ReadDoubleWord(offset);
        }

        public override void Reset()
        {
            base.Reset();

            IRQ.Unset();
            MaxPriorityIRQ.Unset();

            foreach(var register in customCSRsBackingRegisters)
            {
                register.Reset();
            }

            foreach(var interruptSource in interruptSources)
            {
                interruptSource.Reset();
            }
        }

        public virtual void WriteByte(long offset, byte value)
        {
            RaiseStoreAccessFault(offset, value, SysbusAccessWidth.Byte);
        }

        public virtual void WriteWord(long offset, ushort value)
        {
            RaiseStoreAccessFault(offset, value, SysbusAccessWidth.Word);
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(!RegistersCollection.HasRegisterAtOffset(offset))
            {
                // Warnings, typically used in this case, aren't suitable because generally it isn't an issue for PIC:
                // > Accessing unused addresses within the 32KB PIC address range do not trigger an unmapped address exception.
                // > Reads to unmapped addresses return 0, writes to unmapped addresses are silently dropped.
                // Noisy level is used so that such accesses are still noted in the log.
                this.NoisyLog("Unhandled write to offset 0x{0:X}, value 0x{1:X}", offset, value);
                return;
            }

            lock(interruptSources)
            {
                base.WriteDoubleWord(offset, value);
            }
        }

        public uint CapturedHighestPriorityLevel
        {
            get => (uint)capturedHighestPriorityLevel.Value;
            set => capturedHighestPriorityLevel.Value = value;
        }

        public uint CurrentPriorityLevel
        {
            get => (uint)currentPriorityLevelField.Value;
            set => currentPriorityLevelField.Value = value;
        }

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        public GPIO MaxPriorityIRQ { get; } = new GPIO();

        public uint CapturedHighestPrioritySourceId { get; private set; }

        public uint InterruptSourcesCount { get; }

        public uint LastInterruptSourceId => InterruptSourcesCount;  // It's the same because source 0 is never used.

        public uint MaxPriorityLevel => PriorityOrderReversed ? 0u : 15u;

        public byte PriorityLevelThreshold => (byte)priorityThresholdField.Value;

        public bool PriorityOrderReversed => priorityOrderFlag.Value;  // In reversed order: 15 is the lowest and 0 is the highest level.

        public long Size => 32.KB();

        public uint VectorTableBaseAddress => (uint)vectorTableBaseAddressField.Value;

        public const uint PICAccessErrorSecondaryCause = 0x6;

        protected void DefineCSRs()
        {
            var claimIDPriorityLevel = new DoubleWordRegister(this)
                .WithReservedBits(4, 28)
                .WithValueField(0, 4, out capturedHighestPriorityLevel, name: "clidpri")
            ;

            // "The `meicpct` register has WAR0 (Write Any value, Read 0) behavior. Writing ‘0’ is recommended."
            var claimIDPriorityLevelCaptureTrigger = new DoubleWordRegister(this)
                .WithValueField(0, 32, valueProviderCallback: _ => 0)
                .WithWriteCallback((_, __) => CaptureHighestPrioritySourceAndItsLevel())
            ;

            var currentPriorityLevel = new DoubleWordRegister(this)
                .WithReservedBits(4, 28)
                // Just a storage for interrupt handling routines.
                .WithValueField(0, 4, out currentPriorityLevelField, name: "currpri")
            ;

            var handlerAddressPointerRegister = new DoubleWordRegister(this)
                .WithValueField(10, 22, FieldMode.Read, valueProviderCallback: _ => VectorTableBaseAddress)
                .WithValueField(2, 8, FieldMode.Read, name: "claimid", valueProviderCallback: _ => CapturedHighestPrioritySourceId)
                .WithReservedBits(0, 2)
            ;

            var priorityThresholdRegister = new DoubleWordRegister(this)
                .WithReservedBits(4, 28)
                .WithValueField(0, 4, out priorityThresholdField, name: "prithresh")
                .WithChangeCallback((_, __) => UpdateIRQs())
            ;

            var vectorTableRegister = new DoubleWordRegister(this)
                .WithValueField(10, 22, out vectorTableBaseAddressField)
                .WithReservedBits(0, 10)
            ;

            RegisterCSRs(claimIDPriorityLevel, claimIDPriorityLevelCaptureTrigger, currentPriorityLevel, handlerAddressPointerRegister,
                priorityThresholdRegister, vectorTableRegister);
        }

        // "non-word sized/aligned loads cause a load access fault exception"
        protected void RaiseLoadAccessFault(long offset, SysbusAccessWidth width)
        {
            this.WarningLog("Attempted {0} read from 0x{1:X} isn't supported, raising access fault", width, offset);
            cpu.RaiseExceptionWithSecondaryCause((uint)BaseRiscV.ExceptionCodes.LoadAccessFault, PICAccessErrorSecondaryCause);
        }

        // "non-word sized/aligned stores cause a store/AMO access fault exception"
        protected void RaiseStoreAccessFault(long offset, ushort value, SysbusAccessWidth width)
        {
            this.WarningLog("Attempted {0} write to 0x{1:X} (value: 0x{2:X}) isn't supported, raising access fault", width, offset, value);
            cpu.RaiseExceptionWithSecondaryCause((uint)BaseRiscV.ExceptionCodes.StoreAccessFault, PICAccessErrorSecondaryCause);
        }

        protected virtual void RegisterCSRs(DoubleWordRegister claimIDPriorityLevel, DoubleWordRegister claimIDPriorityLevelCaptureTrigger,
                DoubleWordRegister currentPriorityLevel, DoubleWordRegister handlerAddressPointerRegister,
                DoubleWordRegister priorityThresholdRegister, DoubleWordRegister vectorTableRegister)
        {
            RegisterCSR(CustomCSR.ExternalInterruptClaimIDPriorityLevel, claimIDPriorityLevel);
            RegisterCSR(CustomCSR.ExternalInterruptClaimIDPriorityLevelCaptureTrigger, claimIDPriorityLevelCaptureTrigger);
            RegisterCSR(CustomCSR.ExternalInterruptCurrentPriorityLevel, currentPriorityLevel);
            RegisterCSR(CustomCSR.ExternalInterruptHandlerAddressPointer, handlerAddressPointerRegister);
            RegisterCSR(CustomCSR.ExternalInterruptPriorityThreshold, priorityThresholdRegister);
            RegisterCSR(CustomCSR.ExternalInterruptVectorTable, vectorTableRegister);
        }

        protected void RegisterCSR<T>(T csr, DoubleWordRegister register)
            where T : IConvertible
        {
            customCSRsBackingRegisters.Add(register);

            // Offset is used in PeripheralRegister.Write for unhandled write logs etc.
            var offset = Convert.ToInt64(csr);

            // `register.Read` can't be passed directly because CSR expects `Func<ulong>` while `DoubleWordRegister.Read` returns `uint`.
            cpu.RegisterCSR(Convert.ToUInt16(csr), () => register.Read(), value => register.Write(offset, (uint)value));
        }

        protected readonly InterruptSource[] interruptSources;  // Subtract 1 when using ids to get elements (`interrupt[0].Id == 1`).

        protected const uint InterruptSourcesMax = 255u;
        protected const uint InterruptSourcesMin = 2u;
        protected const uint NoInterruptSourceId = 0u;

        private void CaptureHighestPrioritySourceAndItsLevel()
        {
            lock(interruptSources)
            {
                var enabledPendingSources = interruptSources.Where(source => source.IsEnabled && source.IsPending);
                if(!enabledPendingSources.Any())
                {
                    // Setting claimid to 0 is based on the "Support for Vectored External Interrupts" chapter in the docs:
                    // "It is possible in some corner cases that the captured claim ID read from the meihap register is 0 (i.e., no
                    // interrupt request is pending). To keep the interrupt latency at a minimum, the external interrupt handler above
                    // should not check for this condition. Instead, the pointer stored at the base address of the external interrupt
                    // vector table (i.e., pointer 0) must point to a ‘no-interrupt’ handler, as shown in Fig. 8.5 above. That handler
                    // can be as simple as executing a return from interrupt (i.e., mret) instruction."
                    CapturedHighestPrioritySourceId = NoInterruptSourceId;
                    return;
                }

                // Regardless of the order, out of sources with the same priority level the one with a smaller id wins.
                // Therefore levels are taken directly for reversed order, for which level 0 has priority over level 15,
                // but negated for standard priority order.
                var modifier = PriorityOrderReversed ? 1 : -1;

                // `First` can be safely used as this code is unreachable with empty `enabledPendingSources` and the collection is locked.
                var highestPrioritySource = enabledPendingSources.OrderBy(source => (source.PriorityLevel * 1000 * modifier) + source.Id).First();

                capturedHighestPriorityLevel.Value = highestPrioritySource.PriorityLevel;
                CapturedHighestPrioritySourceId = highestPrioritySource.Id;

                this.NoisyLog("Captured external interrupt source with the highest priority level ({0}), source ID: {1}",
                        highestPrioritySource.PriorityLevel, highestPrioritySource.Id);
            }
        }

        private void DefineMemoryMappedRegisters(uint sourcesCount, bool defineAllGatewayRegisters)
        {
            Registers.PICConfiguration.Define(this, name: "mpiccfg")
                .WithReservedBits(1, 31)
                .WithFlag(0, out priorityOrderFlag, name: "priord")
                .WithChangeCallback((_, __) => UpdateIRQs())
            ;

            // We can always define 8 registers regardless of the sources count.
            // The ones without any supported sources will return 0 and according to the manual:
            // "Reads to unmapped addresses return 0, writes (...) are silently dropped."
            Registers.InterruptsPending0.DefineMany(this, count: 8,
                setup: (register, registerIndex) =>
                {
                    register.WithFlags(0, 32, FieldMode.Read, valueProviderCallback: (flagIndex, _) =>
                    {
                        var sourceId = registerIndex*32 + flagIndex;
                        if(sourceId == 0 || sourceId > LastInterruptSourceId)
                        {
                            return false;
                        }
                        return interruptSources[sourceId - 1].IsPending;
                    });
                }, name: "meipX");

            for(var id = 1u; id <= sourcesCount; id++)
            {
                interruptSources[id - 1] = new InterruptSource(this, id, defineGatewayRegisters: defineAllGatewayRegisters);
            }
        }

        private void UpdateIRQs()
        {
            lock(interruptSources)
            {
                var enabledAndPendingSources = interruptSources.Where(source => source.IsEnabled && source.IsPending);

                var maxPriorityIRQOldState = MaxPriorityIRQ.IsSet;
                var maxPriorityIRQNewState = enabledAndPendingSources.Any(source => source.PriorityLevel == MaxPriorityLevel);
                if(maxPriorityIRQOldState != maxPriorityIRQNewState)
                {
                    MaxPriorityIRQ.Set(maxPriorityIRQNewState);
                }

                var oldState = IRQ.IsSet;
                var newState = enabledAndPendingSources.Any(source => source.HasLevelOverThreshold);
                if(oldState == newState)
                {
                    return;
                }
                this.DebugLog("{0} IRQ", newState ? "Setting" : "Unsetting");
                IRQ.Set(newState);
            }
        }

        private IValueRegisterField capturedHighestPriorityLevel;
        private IValueRegisterField currentPriorityLevelField;
        private IFlagRegisterField priorityOrderFlag;
        private IValueRegisterField priorityThresholdField;
        private IValueRegisterField vectorTableBaseAddressField;

        private readonly VeeR_EL2 cpu;
        private readonly List<DoubleWordRegister> customCSRsBackingRegisters = new List<DoubleWordRegister>();

        protected class InterruptSource
        {
            public InterruptSource(VeeR_EL2_PIC pic, uint id, bool defineGatewayRegisters)
            {
                if(id == 0)
                {
                    throw new ArgumentException("Interrupt Source 0 must not be used");
                }
                this.pic = pic;
                Id = id;

                var offset = 4 * id;
                ((Registers)Registers.InterruptSource0PriorityLevel + offset).Define(pic, name: $"meipl{id}")
                    .WithReservedBits(4, 28)
                    .WithValueField(0, 4, out priorityField, name: "priority", changeCallback: PriorityFieldChangeCallback)
                ;

                ((Registers)Registers.InterruptSource0Enable + offset).Define(pic, name: $"meie{id}")
                    .WithReservedBits(1, 31)
                    .WithFlag(0, out enableFlag, name: "inten", changeCallback: EnableFlagChangeCallback)
                ;

                if(defineGatewayRegisters)
                {
                    DefineGatewayRegisters();
                }
            }

            public void DefineGatewayRegisters()
            {
                var offset = 4 * Id;
                ((Registers)Registers.InterruptSource0GatewayConfiguration + offset).Define(pic, name: $"meigwctrl{Id}")
                    .WithReservedBits(2, 30)
                    .WithFlag(1, name: "type")
                    .WithFlag(0, name: "polarity")
                    .WithChangeCallback((_, newValue) =>
                    {
                        TriggerTypeAndPolarity = (Trigger)newValue;
                        OnSignalChange(signalWasHigh);
                    })
                ;

                // "The register has WAR0 (Write Any value, Read 0) behavior. Writing '0' is recommended."
                ((Registers)Registers.InterruptSource0GatewayClear + offset).Define(pic, name: $"meigwclr{Id}")
                    .WithValueField(0, 32, valueProviderCallback: _ => 0)
                    .WithWriteCallback((_, __) => ClearGatewayInterruptPendingBit())
                ;
            }

            public void OnSignalChange(bool signalHigh)
            {
                signalWasHigh = signalHigh;

                var oldPendingBit = pendingBit;
                switch(TriggerTypeAndPolarity)
                {
                case Trigger.HighLevel:
                    pendingBit = signalHigh;
                    break;
                case Trigger.LowLevel:
                    pendingBit = !signalHigh;
                    break;
                case Trigger.LowToHighEdge:
                    // High to low edge doesn't clear pending bit.
                    pendingBit = pendingBit || signalHigh;
                    break;
                case Trigger.HighToLowEdge:
                    // Low to high edge doesn't clear pending bit.
                    pendingBit = pendingBit || !signalHigh;
                    break;
                }

                if(oldPendingBit == pendingBit)
                {
                    return;
                }
                Log(LogLevel.Debug, "Pending bit {0} after signal changed", pendingBit ? "set" : "cleared");
                UpdateIRQIfNeeded();
            }

            public void Reset()
            {
                pendingBit = false;
                signalWasHigh = false;
                TriggerTypeAndPolarity = Trigger.HighLevel;
            }

            public bool HasLevelOverThreshold
            {
                get
                {
                    if(pic.PriorityOrderReversed)
                    {
                        return PriorityLevel <= pic.PriorityLevelThreshold;
                    }
                    else
                    {
                        return PriorityLevel >= pic.PriorityLevelThreshold;
                    }
                }
            }

            public uint Id { get; }

            public bool IsEnabled => enableFlag.Value;

            public bool IsPending => pendingBit;

            public string Name => $"ExternalInterruptSource{Id}";

            public byte PriorityLevel => (byte)priorityField.Value;

            public Trigger TriggerTypeAndPolarity { get; private set; }

            private void ClearGatewayInterruptPendingBit()
            {
                // The interrupt pending bit is controlled directly by signal in gateways configured as level-triggered.
                if(!pendingBit || TriggerTypeAndPolarity == Trigger.HighLevel || TriggerTypeAndPolarity == Trigger.LowLevel)
                {
                    return;
                }
                pendingBit = false;
                Log(LogLevel.Debug, "Pending bit cleared");
                UpdateIRQIfNeeded();
            }

            private void EnableFlagChangeCallback(bool oldValue, bool newValue)
            {
                Log(LogLevel.Debug, newValue ? "Enabled" : "Disabled");
                UpdateIRQIfNeeded();
            }

            private void Log(LogLevel level, string message, params object[] args)
            {
                pic.Log(level, $"{Name}: {message}", args);
            }

            private void PriorityFieldChangeCallback(ulong oldValue, ulong newValue)
            {
                Log(LogLevel.Debug, "Priority level changed from {0} to {1}", (byte)oldValue, (byte)newValue);
                UpdateIRQIfNeeded();
            }

            private void UpdateIRQIfNeeded()
            {
                // True if, from perspective of this one interrupt source, PIC IRQs should be set.
                var irqShouldBeSet = IsEnabled && IsPending && HasLevelOverThreshold;
                var maxPriorityIRQShouldBeSet = IsEnabled && IsPending && PriorityLevel == pic.MaxPriorityLevel;

                // If PIC IRQ states are in line with this source then there's no need to reevaluate
                // other interrupt sources because changes influencing IRQ states are serialized.
                if(pic.IRQ.IsSet != irqShouldBeSet || pic.MaxPriorityIRQ.IsSet != maxPriorityIRQShouldBeSet)
                {
                    pic.UpdateIRQs();
                }
            }

            private bool pendingBit;
            private bool signalWasHigh;

            private readonly IFlagRegisterField enableFlag;
            private readonly VeeR_EL2_PIC pic;
            private readonly IValueRegisterField priorityField;

            public enum Trigger
            {
                HighLevel,
                LowLevel,
                LowToHighEdge,
                HighToLowEdge,
            }
        }

        protected enum Registers : long
        {
            // All the `InterruptSource0*` registers are reserved because 0 is a dummy source.

            // meiplS
            InterruptSource0PriorityLevel = 0x0,
            InterruptSource1PriorityLevel = 0x4,
            // ...
            InterruptSource255PriorityLevel = 0x3FC,

            // meipX: each of these registers contains a bit per interrupt source.
            InterruptsPending0 = 0x1000,
            InterruptsPending1 = 0x1004,
            // ...
            InterruptsPending7 = 0x101C,

            // meieS
            InterruptSource0Enable = 0x2000,
            InterruptSource1Enable = 0x2004,
            // ...
            InterruptSource255Enable = 0x23FC,

            // mpiccfg
            PICConfiguration = 0x3000,

            // meigwctrlS
            InterruptSource0GatewayConfiguration = 0x4000,
            InterruptSource1GatewayConfiguration = 0x4004,
            // ...
            InterruptSource255GatewayConfiguration = 0x43FC,

            // meigwclrS
            InterruptSource0GatewayClear = 0x5000,
            InterruptSource1GatewayClear = 0x5004,
            // ...
            InterruptSource255GatewayClear = 0x53FC,
        }

        private enum CustomCSR : ushort
        {
            ExternalInterruptVectorTable = 0xBC8,                           // meivt
            ExternalInterruptPriorityThreshold = 0xBC9,                     // meipt
            ExternalInterruptClaimIDPriorityLevelCaptureTrigger = 0xBCA,    // meicpct
            ExternalInterruptClaimIDPriorityLevel = 0xBCB,                  // meicidpl
            ExternalInterruptCurrentPriorityLevel = 0xBCC,                  // meicurpl
            ExternalInterruptHandlerAddressPointer = 0xFC8,                 // meihap
        }
    }
}