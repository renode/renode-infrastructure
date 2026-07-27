//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    // The H5 EXTI layout is neither the WBA layout (single bank, IMR1 at 0x80 with no fields)
    // nor the H7 layout (3 banks, pending at 0x88/0x98/0xA8). The H5 has:
    //   - Bank stride 0x20 for the trigger/pending group (0x00..0x18 / 0x20..0x38)
    //   - Bank stride 0x10 for the mask group, sitting at 0x80/0x90
    //   - Separate rising/falling pending registers (RPR/FPR) with write-1-to-clear
    //   - EXTICR mux registers at 0x60..0x6C (same as WBA)
    //
    // IMR1/IMR2 are defined WITH fields (out core.InterruptMask), unlike STM32WBA_EXTI which
    // defines IMR1 with no fields — that drops the mask and leaves core.InterruptMask null
    // while SWIER1's callback dereferences it.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32H5_EXTI : BasicDoubleWordPeripheral, IKnownSize, IIRQController, ILocalGPIOReceiver, INumberedGPIOOutput
    {
        public STM32H5_EXTI(IMachine machine, int numberOfOutputLines = DefaultNumberOfLines) : base(machine)
        {
            cores = new STM32_EXTICore[BankCount];
            for(var i = 0; i < BankCount; i++)
            {
                cores[i] = new STM32_EXTICore(this, LineConfigurations[i], separateConfigs: true);
            }

            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfOutputLines; i++)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
            internalReceiversCache = new Dictionary<int, InternalReceiver>();

            DefineRegisters();
            Reset();
        }

        public IGPIOReceiver GetLocalReceiver(int index)
        {
            if(!internalReceiversCache.TryGetValue(index, out var receiver))
            {
                receiver = new InternalReceiver(this, index);
                internalReceiversCache.Add(index, receiver);
            }
            return receiver;
        }

        public void OnGPIO(int number, bool value)
        {
            if(number < 0 || number >= Connections.Count)
            {
                this.Log(LogLevel.Error, "GPIO number {0} is out of range [0; {1})", number, Connections.Count);
                return;
            }

            var coreIndex = number / LinesPerCore;
            var localLine = (byte)(number % LinesPerCore);
            var core = cores[coreIndex];

            if(core.CanSetInterruptValue(localLine, value, out var _))
            {
                // H5 edge detection: determine edge direction from the transition value itself
                // (true = rising edge, false = falling edge) and set RPR/FPR directly.
                //
                // We bypass STM32_EXTICore.UpdatePendingValue because its separateConfigs path
                // uses `isRaising = (value != true)`, which inverts the caller's boolean — passing
                // `true` lands in PendingFallingInterrupts.  WBA works around this by always passing
                // `true`, which puts every event in the raising register.  H5's HAL_EXTI_IRQHandler
                // reads RPR1 and FPR1 separately to dispatch callbacks, so the distinction is
                // observable and must be correct.
                //
                // The pending bit is set regardless of the interrupt mask (RPR/FPR record the edge);
                // the output (Connections[line]) is only asserted when IMR gates delivery.
                SetPendingBit(core, localLine, isRising: value);
                if(BitHelper.IsBitSet(core.InterruptMask.Value, localLine))
                {
                    Connections[number].Set(true);
                }
            }
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var connection in Connections.Values)
            {
                connection.Unset();
            }
            foreach(var receiver in internalReceiversCache.Values)
            {
                for(var pin = 0; pin < GpioPins; pin++)
                {
                    receiver.UpdateGPIO(pin);
                }
            }
        }

        public long Size => 0x400;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private void DefineRegisters()
        {
            // Bank 1 (lines 0-31): trigger/pending group at 0x00..0x18
            RegistersCollection.DefineRegister((long)Registers.RisingTriggerSelection1)
                .WithValueField(0, 17, out cores[0].RisingEdgeMask, name: "RT")
                .WithReservedBits(17, 15);

            RegistersCollection.DefineRegister((long)Registers.FallingTriggerSelection1)
                .WithValueField(0, 17, out cores[0].FallingEdgeMask, name: "FT")
                .WithReservedBits(17, 15);

            RegistersCollection.DefineRegister((long)Registers.SoftwareInterruptEvent1)
                .WithValueField(0, 32, name: "SWIER1", writeCallback: (_, value) =>
                {
                    BitHelper.ForeachActiveBit(value & cores[0].InterruptMask.Value, bit =>
                    {
                        // SWIER always simulates a rising edge (sets RPR)
                        SetPendingBit(cores[0], (byte)bit, isRising: true);
                        Connections[bit].Set();
                    });
                });

            RegistersCollection.DefineRegister((long)Registers.RisingPending1)
                .WithValueField(0, 32, out cores[0].PendingRaisingInterrupts,
                    FieldMode.Read | FieldMode.WriteOneToClear,
                    writeCallback: (_, val) => BitHelper.ForeachActiveBit(val, x => Connections[x].Unset()),
                    name: "RPIF1");

            RegistersCollection.DefineRegister((long)Registers.FallingPending1)
                .WithValueField(0, 32, out cores[0].PendingFallingInterrupts,
                    FieldMode.Read | FieldMode.WriteOneToClear,
                    writeCallback: (_, val) => BitHelper.ForeachActiveBit(val, x => Connections[x].Unset()),
                    name: "FPIF1");

            RegistersCollection.DefineRegister((long)Registers.SecurityConfiguration1)
                .WithValueField(0, 32, name: "SEC1");

            RegistersCollection.DefineRegister((long)Registers.PrivilegeConfiguration1)
                .WithValueField(0, 32, name: "PRIV1");

            // Bank 2 (lines 32-58): trigger/pending group at 0x20..0x38
            RegistersCollection.DefineRegister((long)Registers.RisingTriggerSelection2)
                .WithValueField(0, 27, out cores[1].RisingEdgeMask, name: "RT")
                .WithReservedBits(27, 5);

            RegistersCollection.DefineRegister((long)Registers.FallingTriggerSelection2)
                .WithValueField(0, 27, out cores[1].FallingEdgeMask, name: "FT")
                .WithReservedBits(27, 5);

            RegistersCollection.DefineRegister((long)Registers.SoftwareInterruptEvent2)
                .WithValueField(0, 27, name: "SWIER2", writeCallback: (_, value) =>
                {
                    BitHelper.ForeachActiveBit(value & cores[1].InterruptMask.Value, bit =>
                    {
                        var globalLine = LinesPerCore + bit;
                        // SWIER always simulates a rising edge (sets RPR)
                        SetPendingBit(cores[1], (byte)bit, isRising: true);
                        if(Connections.TryGetValue(globalLine, out var irq))
                        {
                            irq.Set();
                        }
                    });
                })
                .WithReservedBits(27, 5);

            RegistersCollection.DefineRegister((long)Registers.RisingPending2)
                .WithValueField(0, 27, out cores[1].PendingRaisingInterrupts,
                    FieldMode.Read | FieldMode.WriteOneToClear,
                    writeCallback: (_, val) => BitHelper.ForeachActiveBit(val, x =>
                    {
                        var globalLine = LinesPerCore + x;
                        if(Connections.TryGetValue(globalLine, out var irq))
                        {
                            irq.Unset();
                        }
                    }),
                    name: "RPIF2")
                .WithReservedBits(27, 5);

            RegistersCollection.DefineRegister((long)Registers.FallingPending2)
                .WithValueField(0, 27, out cores[1].PendingFallingInterrupts,
                    FieldMode.Read | FieldMode.WriteOneToClear,
                    writeCallback: (_, val) => BitHelper.ForeachActiveBit(val, x =>
                    {
                        var globalLine = LinesPerCore + x;
                        if(Connections.TryGetValue(globalLine, out var irq))
                        {
                            irq.Unset();
                        }
                    }),
                    name: "FPIF2")
                .WithReservedBits(27, 5);

            RegistersCollection.DefineRegister((long)Registers.SecurityConfiguration2)
                .WithValueField(0, 27, name: "SEC2")
                .WithReservedBits(27, 5);

            RegistersCollection.DefineRegister((long)Registers.PrivilegeConfiguration2)
                .WithValueField(0, 27, name: "PRIV2")
                .WithReservedBits(27, 5);

            // EXTICR[4] at 0x60..0x6C — external interrupt configuration (port mux for lines 0-15)
            for(var registerIndex = 0; registerIndex < InterruptSelectionRegistersCount; registerIndex++)
            {
                var reg = new DoubleWordRegister(this, 0);
                for(var fieldNumber = 0; fieldNumber < NumberOfPortsPerInterruptSelectionRegister; fieldNumber++)
                {
                    var pinNumber = registerIndex * NumberOfPortsPerInterruptSelectionRegister + fieldNumber;
                    extiMappings[pinNumber] = reg.DefineValueField(8 * fieldNumber, 8, name: $"EXTI{pinNumber}",
                        changeCallback: (_, portNumber) =>
                        {
                            Connections[pinNumber].Unset();
                            ((InternalReceiver)GetLocalReceiver((int)portNumber)).UpdateGPIO(pinNumber);
                        }
                    );
                }
                RegistersCollection.AddRegister((long)Registers.ExternalInterruptSelection1 + 4 * registerIndex, reg);
            }

            // LOCKR at 0x70
            RegistersCollection.DefineRegister((long)Registers.Lock)
                .WithFlag(0, name: "LOCKR")
                .WithReservedBits(1, 31);

            // IMR1 at 0x80 — interrupt mask, bank 1 (32 bits: lines 0-31)
            // Defined WITH fields so that core.InterruptMask is non-null and SWIER1 can dereference it
            RegistersCollection.DefineRegister((long)Registers.InterruptMask1, resetValue: 0xFFF80000)
                .WithValueField(0, 32, out cores[0].InterruptMask, name: "IM1");

            // EMR1 at 0x84 — event mask, bank 1 (32 bits: lines 0-31)
            RegistersCollection.DefineRegister((long)Registers.EventMask1)
                .WithValueField(0, 32, name: "EM1");

            // IMR2 at 0x90 — interrupt mask, bank 2 (26 bits: lines 32-57)
            RegistersCollection.DefineRegister((long)Registers.InterruptMask2, resetValue: 0x03FFFFFF)
                .WithValueField(0, 26, out cores[1].InterruptMask, name: "IM2")
                .WithReservedBits(26, 6);

            // EMR2 at 0x94 — event mask, bank 2 (26 bits: lines 32-57)
            RegistersCollection.DefineRegister((long)Registers.EventMask2)
                .WithValueField(0, 26, name: "EM2")
                .WithReservedBits(26, 6);
        }

        private readonly STM32_EXTICore[] cores;
        private readonly Dictionary<int, InternalReceiver> internalReceiversCache;
        private readonly IValueRegisterField[] extiMappings = new IValueRegisterField[GpioPins];

        /// <summary>
        /// Sets the correct pending register (RPR for rising, FPR for falling) directly,
        /// bypassing STM32_EXTICore.UpdatePendingValue whose separateConfigs path inverts
        /// the boolean.
        /// </summary>
        private static void SetPendingBit(STM32_EXTICore core, byte bit, bool isRising)
        {
            var field = isRising ? core.PendingRaisingInterrupts : core.PendingFallingInterrupts;
            var reg = field.Value;
            BitHelper.SetBit(ref reg, bit, true);
            field.Value = reg;
        }

        // Bank 1: lines 0-15 are EXTI_GPIO (configurable), line 16 is EXTI_CONFIG (configurable),
        //   lines 17-31 are EXTI_DIRECT — from stm32h5xx_hal_exti.h EXTI_LINE property table
        // Bank 2: resolved from stm32h5xx_hal_exti.h for STM32H563xx (RM0481 Rev 3, Table 145
        //   "EXTI lines connections"). For H563 the defined peripherals give:
        //     Line 46 (bit 14) = EXTI_CONFIG — ETH wakeup (#if defined(ETH), true for H563)
        //     Line 49 (bit 17) = EXTI_DIRECT — not H5E5/H5E4/H5F5/H5F4
        //     Line 50 (bit 18) = EXTI_CONFIG — unconditional
        //     Line 53 (bit 21) = EXTI_CONFIG — unconditional
        //   All other bank-2 lines (32-45, 47-49, 51-52, 54-57) are EXTI_DIRECT.
        //   Line 58 (COMP1/I3C2) is not present on H563 (neither COMP1 nor I3C2 defined).
        //   Resolved-from-documentation: unobservable in the reference firmware (no bank-2
        //   configurable sources are modelled), verified against HAL defines only.
        private static readonly ulong[] LineConfigurations = new ulong[BankCount]
        {
            0x0001FFFF, // Bank 1: bits [0:16] configurable (GPIO + EXTI_CONFIG line 16)
            0x00244000, // Bank 2: bits 14, 18, 21 configurable (lines 46, 50, 53)
        };

        private const int BankCount = 2;
        private const int LinesPerCore = 32;
        private const int DefaultNumberOfLines = 59;
        private const int GpioPins = 16;
        private const uint InterruptSelectionRegistersCount = 4;
        private const int NumberOfPortsPerInterruptSelectionRegister = GpioPins / (int)InterruptSelectionRegistersCount;

        private class InternalReceiver : IGPIOReceiver
        {
            public InternalReceiver(STM32H5_EXTI parent, int portNumber)
            {
                this.parent = parent;
                this.portNumber = portNumber;
                this.state = new bool[GpioPins];
            }

            public void OnGPIO(int pinNumber, bool value)
            {
                if(pinNumber >= GpioPins)
                {
                    parent.Log(LogLevel.Error, "GPIO port {0}, pin {1}, is not supported. Up to {2} pins are supported", portNumber, pinNumber, GpioPins);
                    return;
                }
                parent.Log(LogLevel.Noisy, "GPIO port {0}, pin {1}, raised IRQ: {2}", portNumber, pinNumber, value);
                state[pinNumber] = value;

                UpdateGPIO(pinNumber);
            }

            public void UpdateGPIO(int pinNumber)
            {
                if((int)parent.extiMappings[pinNumber].Value == portNumber)
                {
                    var value = state[pinNumber];
                    if(parent.cores[0].CanSetInterruptValue((byte)pinNumber, value, out var _))
                    {
                        // See OnGPIO for the rationale: set RPR/FPR from the edge direction,
                        // gate the output on InterruptMask.
                        SetPendingBit(parent.cores[0], (byte)pinNumber, isRising: value);
                        if(BitHelper.IsBitSet(parent.cores[0].InterruptMask.Value, (byte)pinNumber))
                        {
                            parent.Connections[pinNumber].Set(true);
                        }
                    }
                }
            }

            public void Reset()
            {
                // IRQs are cleared on parent reset
                // Don't clear state array here - it represents the state of input signals, not a property of this peripheral
            }

            private readonly bool[] state;
            private readonly STM32H5_EXTI parent;
            private readonly int portNumber;
        }

        private enum Registers
        {
            // Bank 1: trigger/pending group (stride 0x20 between banks)
            RisingTriggerSelection1     = 0x00, // EXTI_RTSR1
            FallingTriggerSelection1    = 0x04, // EXTI_FTSR1
            SoftwareInterruptEvent1     = 0x08, // EXTI_SWIER1
            RisingPending1              = 0x0C, // EXTI_RPR1
            FallingPending1             = 0x10, // EXTI_FPR1
            SecurityConfiguration1      = 0x14, // EXTI_SECCFGR1
            PrivilegeConfiguration1     = 0x18, // EXTI_PRIVCFGR1
            // Bank 2: trigger/pending group
            RisingTriggerSelection2     = 0x20, // EXTI_RTSR2
            FallingTriggerSelection2    = 0x24, // EXTI_FTSR2
            SoftwareInterruptEvent2     = 0x28, // EXTI_SWIER2
            RisingPending2              = 0x2C, // EXTI_RPR2
            FallingPending2             = 0x30, // EXTI_FPR2
            SecurityConfiguration2      = 0x34, // EXTI_SECCFGR2
            PrivilegeConfiguration2     = 0x38, // EXTI_PRIVCFGR2
            // EXTICR mux (port selection for GPIO lines 0-15)
            ExternalInterruptSelection1 = 0x60, // EXTI_EXTICR1
            ExternalInterruptSelection2 = 0x64, // EXTI_EXTICR2
            ExternalInterruptSelection3 = 0x68, // EXTI_EXTICR3
            ExternalInterruptSelection4 = 0x6C, // EXTI_EXTICR4
            // Lock
            Lock                        = 0x70, // EXTI_LOCKR
            // Mask group (stride 0x10 between banks, starting at 0x80)
            InterruptMask1              = 0x80, // EXTI_IMR1
            EventMask1                  = 0x84, // EXTI_EMR1
            InterruptMask2              = 0x90, // EXTI_IMR2
            EventMask2                  = 0x94, // EXTI_EMR2
        }
    }
}
