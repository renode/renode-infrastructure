//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.QuadWordToDoubleWord | AllowedTranslation.DoubleWordToByte | AllowedTranslation.WordToByte)]
    public class CoreLocalInterruptController : BasicBytePeripheral, IDoubleWordPeripheral, IIndirectCSRPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, INumberedGPIOOutput, IGPIOReceiver
    {
        public CoreLocalInterruptController(IMachine machine, BaseRiscV cpu, uint numberOfInterrupts = 4096, uint numberOfTriggers = 32,
            int machineLevelBits = 8, // MNLBITS
            int supervisorLevelBits = 8 // SNLBITS
            )
            : base(machine)
        {
            this.cpu = cpu;
            this.numberOfInterrupts = numberOfInterrupts;
            this.numberOfTriggers = numberOfTriggers;
            this.machineLevelBits = machineLevelBits;
            this.supervisorLevelBits = supervisorLevelBits;
            DoubleWordRegisters = new DoubleWordRegisterCollection(this);

            interruptPending = new IFlagRegisterField[numberOfInterrupts];
            interruptEnable = new IFlagRegisterField[numberOfInterrupts];
            vectored = new IFlagRegisterField[numberOfInterrupts];
            edgeTriggered = new IFlagRegisterField[numberOfInterrupts];
            negative = new IFlagRegisterField[numberOfInterrupts];
            mode = new IEnumRegisterField<PrivilegeLevel>[numberOfInterrupts];
            priority = new IValueRegisterField[numberOfInterrupts];
            level = new IValueRegisterField[numberOfInterrupts];

            irqs[0] = new GPIO();
            Connections = new ReadOnlyDictionary<int, IGPIO>(irqs);

            cpu.RegisterLocalInterruptController(this);

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            bestInterrupt = NoInterrupt;
        }

        public uint ReadDoubleWord(long offset)
        {
            if(DoubleWordRegisters.TryRead(offset, out var result))
            {
                return result;
            }
            return this.ReadDoubleWordUsingByte(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(DoubleWordRegisters.TryWrite(offset, value))
            {
                return;
            }
            this.WriteDoubleWordUsingByte(offset, value);
        }

        public uint ReadIndirectCSR(uint iselect, uint ireg)
        {
            // iselect is the offset from the beginning of this peripheral's indirect CSR range
            // ireg is the 0-based index of the siregX CSR (sireg - 0, sireg2 - 1, ...)
            if(iselect < InterruptControlAttribute || iselect > ClicConfiguration || (ireg != 0 && ireg != 1))
            {
                LogUnhandledIndirectCSRRead(iselect, ireg);
                return 0x0;
            }

            if(iselect < InterruptPendingEnable)
            {
                var start = (iselect - InterruptControlAttribute) * 4 + (ireg == 0 ? 2 : 3); // ireg: control, ireg 2: attr
                return (uint)((uint)ReadByte(start)
                    | ((uint)ReadByte(start + 4) << 8)
                    | ((uint)ReadByte(start + 8) << 16)
                    | ((uint)ReadByte(start + 12) << 24)
                );
            }

            if(iselect < InterruptTrigger)
            {
                var start = (iselect - InterruptPendingEnable) * 32;
                return BitHelper.GetValueFromBitsArray(
                    ((ireg == 0) ? interruptPending : interruptEnable)
                        .Skip((int)start)
                        .Take(32)
                        .Select(r => r.Value)
                );
            }

            if(ireg == 1)
            {
                LogUnhandledIndirectCSRRead(iselect, ireg);
                return 0x0;
            }

            if(iselect < ClicConfiguration)
            {
                return ReadDoubleWord(iselect - InterruptTrigger + (long)Register.InterruptTrigger0);
            }

            this.Log(LogLevel.Warning, "Register reserved (iselect {0}, ireg{1})", iselect, FormatIregAlias(ireg));
            return 0x0;
        }

        public void WriteIndirectCSR(uint iselect, uint ireg, uint value)
        {
            // iselect is the offset from the beginning of this peripheral's indirect CSR range
            // ireg is the 0-based index of the siregX CSR (sireg - 0, sireg2 - 1, ...)
            if(iselect < InterruptControlAttribute || iselect > ClicConfiguration || (ireg != 0 && ireg != 1))
            {
                LogUnhandledIndirectCSRWrite(iselect, ireg);
                return;
            }

            if(iselect < InterruptPendingEnable)
            {
                var start = (iselect - InterruptControlAttribute) * 4 + (ireg == 0 ? 2 : 3);
                WriteByte(start, (byte)value);
                WriteByte(start + 4, (byte)(value >> 8));
                WriteByte(start + 8, (byte)(value >> 16));
                WriteByte(start + 16, (byte)(value >> 24));
                return;
            }

            if(iselect < InterruptTrigger)
            {
                var flags = (ireg == 0) ? interruptPending : interruptEnable;
                var i = (iselect - InterruptPendingEnable) * 32;
                foreach(var b in BitHelper.GetBits(value))
                {
                    flags[i++].Value = b;
                }
                return;
            }

            if(ireg == 1)
            {
                LogUnhandledIndirectCSRWrite(iselect, ireg);
                return;
            }

            if(iselect < ClicConfiguration)
            {
                WriteDoubleWord(iselect - InterruptTrigger + (long)Register.InterruptTrigger0, value);
                return;
            }

            this.WarningLog("Register reserved (iselect {0}, ireg{1}), value 0x{2:X}", iselect, FormatIregAlias(ireg), value);
            return;
        }

        public void OnGPIO(int number, bool value)
        {
            if(edgeTriggered[number].Value)
            {
                interruptPending[number].Value |= value ^ negative[number].Value;
            }
            else
            {
                interruptPending[number].Value = value ^ negative[number].Value;
            }
            UpdateInterrupt();
            this.WarningLog("clic incoming {0} {1} is et {2} neg {3} conn {4}", number, value, edgeTriggered[number].Value, negative[number].Value, Connections[0].IsSet);
        }

        public void ClearEdgeInterrupt()
        {
            if(bestInterrupt == NoInterrupt || !edgeTriggered[bestInterrupt].Value)
            {
                return;
            }
            interruptPending[bestInterrupt].Value = false;
            UpdateInterrupt();
        }

        private void UpdateInterrupt()
        {
            var bestLevel = bestInterrupt != NoInterrupt ? level[bestInterrupt].Value : MinLevel; // MinLevel - normal execution, not in ISR
            for(int i = 0; i < interruptEnable.Length; ++i)
            {
                if(interruptEnable[i].Value && interruptPending[i].Value && level[i].Value > bestLevel)
                {
                    cpu.ClicInterrupt(i, vectored[i].Value, (uint)(level[i]?.Value ?? MaxLevel), mode[i].Value); // if no bits are assigned to the level then it's defined to be the highest possible one
                    this.WarningLog("clic set intr {0} vec {1} lvl {2} mode {3}", i, vectored[i].Value, (uint)(level[i]?.Value ?? MaxLevel), mode[i].Value);
                    Connections[0].Set();
                    bestInterrupt = i;
                    return;
                }
            }
            Connections[0].Unset();
            bestInterrupt = NoInterrupt;
        }

        private void LogUnhandledIndirectCSRRead(uint iselect, uint ireg)
        {
            this.Log(LogLevel.Warning, "Unhandled read from register via indirect CSR access (iselect {0}, ireg{1})", iselect, FormatIregAlias(ireg));
        }

        private void LogUnhandledIndirectCSRWrite(uint iselect, uint ireg)
        {
            this.Log(LogLevel.Warning, "Unhandled write to register via indirect CSR access (iselect {0}, ireg{1})", iselect, FormatIregAlias(ireg));
        }

        private static string FormatIregAlias(uint n)
        {
            if(n == 0)
            {
                return "";
            }
            return $"{n + 1}";
        }

        protected override void DefineRegisters()
        {
            Register.Configuration.Define32(this)
                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => (uint)machineLevelBits, name: "mnlbits")
                .WithTag("nmbits", 4, 2)
                .WithReservedBits(6, 10)
                .WithTag("snlbits", 16, 4)
                .WithReservedBits(20, 4)
                .WithTag("unlbits", 24, 4)
                .WithReservedBits(28, 4);

            Register.InterruptTrigger0.Define32Many(this, numberOfTriggers, (register, index) =>
            {
                register
                    .WithTag("interrupt_number", 0, 13)
                    .WithReservedBits(13, 17)
                    .WithTaggedFlag("nxti_enable", 30)
                    .WithTaggedFlag("enable", 31)
                ;
            });

            Register.InterruptPending0.Define8Many(this, numberOfInterrupts, (register, index) =>
            {
                register
                    .WithFlag(0, out interruptPending[index], name: "pending")
                    .WithWriteCallback((_, __) => {UpdateInterrupt(); this.WarningLog("set {0} p{1}", index, interruptPending[index].Value);})
                ;
            }, 4);

            Register.InterruptEnable0.Define8Many(this, numberOfInterrupts, (register, index) =>
            {
                register
                    .WithFlag(0, out interruptEnable[index], name: "enable")
                    .WithReservedBits(1, 7)
                    .WithWriteCallback((_, __) => {UpdateInterrupt(); this.WarningLog("set {0} en{1}", index, interruptEnable[index].Value);})
                ;
            }, 4);

            Register.InterruptAttribute0.Define8Many(this, numberOfInterrupts, (register, index) =>
            {
                register
                    .WithFlag(0, out vectored[index], name: "shv")
                    .WithFlag(1, out edgeTriggered[index], name: "edge_triggered") // 0=level, 1=edge
                    .WithFlag(2, out negative[index], name: "negative") // 0=positive (rising), 1=negative (falling)
                    .WithReservedBits(3, 3)
                    .WithEnumField(6, 2, out mode[index], name: "mode")
                    .WithWriteCallback((_, __) => this.WarningLog("set {0} e{1} n{2}", index, edgeTriggered[index].Value, negative[index].Value))
                ;
            }, 4);

            Register.InterruptInputControl0.Define8Many(this, numberOfInterrupts, (register, index) =>
            {
                var machinePriorityBits = InterruptInputControlBits - machineLevelBits;
                var supervisorPriorityBits = InterruptInputControlBits - supervisorLevelBits;

                register
                    .If(machinePriorityBits > 0).Then(r => r.WithValueField(0, machinePriorityBits, out priority[index], name: "priority")).EndIf()
                    .If(machineLevelBits > 0).Then(r => r.WithValueField(machinePriorityBits, machineLevelBits, out level[index], name: "level")).EndIf()
                ;
            }, 4);
        }

        DoubleWordRegisterCollection IProvidesRegisterCollection<DoubleWordRegisterCollection>.RegistersCollection => DoubleWordRegisters;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private DoubleWordRegisterCollection DoubleWordRegisters { get; }

        private int bestInterrupt;

        private readonly IFlagRegisterField[] interruptPending;
        private readonly IFlagRegisterField[] interruptEnable;
        private readonly IFlagRegisterField[] vectored;
        private readonly IFlagRegisterField[] edgeTriggered;
        private readonly IFlagRegisterField[] negative;
        private readonly IEnumRegisterField<PrivilegeLevel>[] mode;
        private readonly IValueRegisterField[] priority;
        private readonly IValueRegisterField[] level;

        private readonly BaseRiscV cpu;
        private readonly uint numberOfInterrupts;
        private readonly uint numberOfTriggers;
        private readonly int machineLevelBits;
        private readonly int supervisorLevelBits;
        private readonly Dictionary<int, IGPIO> irqs = new Dictionary<int, IGPIO>();

        private const int InterruptInputControlBits = 8; // CLICINTCTLBITS
        private const int MinLevel = 0;
        private const int MaxLevel = 255;
        private const int NoInterrupt = -1;

        // This peripheral is mapped at 0x1000 in the indirect CSR space, here are the relative offsets for the various register groups:
        private const uint InterruptControlAttribute = 0x000; // - 0x3FF
        private const uint InterruptPendingEnable    = 0x400; // - 0x47F
        private const uint InterruptTrigger          = 0x480; // - 0x49F
        private const uint ClicConfiguration         = 0x4A0;

        private enum Register
        {
            Configuration           = 0x0000, // - 0x003F
            InterruptTrigger0       = 0x0040, // - 0x00BC
            Reserved1               = 0x00C0, // - 0x07FF
            Custom                  = 0x0800, // - 0x0FFF
            InterruptPending0       = 0x1000, // 0x1000 + 4 * i
            InterruptEnable0        = 0x1001, // 0x1001 + 4 * i
            InterruptAttribute0     = 0x1002, // 0x1002 + 4 * i
            InterruptInputControl0  = 0x1003, // 0x1003 + 4 * i
        }
    }
}
