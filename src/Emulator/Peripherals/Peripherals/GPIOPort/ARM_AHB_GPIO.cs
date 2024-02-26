//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class ARM_AHB_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public ARM_AHB_GPIO(IMachine machine, uint alternateFunctionResetValue = 0, bool hasDedicatedIRQs = false) : base(machine, NumberOfGPIOs)
        {
            this.alternateFunctionResetValue = alternateFunctionResetValue;
            this.hasDedicatedIRQs = hasDedicatedIRQs;
            RegistersCollection = new DoubleWordRegisterCollection(this);

            if(hasDedicatedIRQs)
            {
                DedicatedIRQs = new GPIO[NumberOfDedicatedIRQs];
                DedicatedIRQs[0] = DedicatedIRQ0;
                DedicatedIRQs[1] = DedicatedIRQ1;
                DedicatedIRQs[2] = DedicatedIRQ2;
                DedicatedIRQs[3] = DedicatedIRQ3;
                DedicatedIRQs[4] = DedicatedIRQ4;
                DedicatedIRQs[5] = DedicatedIRQ5;
                DedicatedIRQs[6] = DedicatedIRQ6;
                DedicatedIRQs[7] = DedicatedIRQ7;
            }

            DefineRegisters();
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void OnGPIO(int number, bool value)
        {
            var previousValue = State[number];
            base.OnGPIO(number, value);
            OnPinStateChanged(number, previousValue, value);
        }

        public override void Reset()
        {
            bool[] cachedState = new bool[NumberOfGPIOs];

            Array.Copy(State, cachedState, State.Length);
            base.Reset();
            RegistersCollection.Reset();
            UpdateInterrupts();

            for(byte i = 0; i < NumberOfGPIOs; i++)
            {
                if(!softReset.Value)
                {
                    pinAlternateFunctionEnabled[i] = BitHelper.IsBitSet(alternateFunctionResetValue, i);
                    pinOutputEnabled[i] = false;
                    pullEnable[i].Value = false;
                    pullUpDown[i].Value = false;
                    interruptEnabled[i] = false;
                    interruptStatus[i] = false;
                }
                else
                {
                    State[i] = cachedState[i];
                }
            }
        }

        public long Size => 0x1000;

        // Combined IRQ is a standard ARM AHB GPIO IRQ which is asserted when any of the pins for which IRQ is enabled is triggered
        [DefaultInterrupt]
        public GPIO CombinedIRQ { get; } = new GPIO();

        // In UT32 variant, a GPIO ports can optionally feature dedicated per-pin IRQs for first eight pins. For those pins the dedicated IRQ is asserted instead of the combined IRQ
        public GPIO DedicatedIRQ0 { get; } = new GPIO();
        public GPIO DedicatedIRQ1 { get; } = new GPIO();
        public GPIO DedicatedIRQ2 { get; } = new GPIO();
        public GPIO DedicatedIRQ3 { get; } = new GPIO();
        public GPIO DedicatedIRQ4 { get; } = new GPIO();
        public GPIO DedicatedIRQ5 { get; } = new GPIO();
        public GPIO DedicatedIRQ6 { get; } = new GPIO();
        public GPIO DedicatedIRQ7 { get; } = new GPIO();

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void UpdatePinOutput(int idx, bool value)
        {
            if(!pinOutputEnabled[idx])
            {
                this.Log(LogLevel.Noisy, "Attempted to set pin{0} to {1}, but it's not configured as output; ignoring", idx, value);
                return;
            }

            if(State[idx] == value)
            {
                return;
            }

            Connections[idx].Set(value);
            State[idx] = value;
        }

        private void OnPinStateChanged(int idx, bool previous, bool current)
        {
            var interruptPending = false;
            switch(interruptPolarity[idx])
            {
                case InterruptPolarity.LowFalling:
                    if(interruptType[idx] == InterruptType.EdgeTriggered)
                    {
                        interruptPending = !current && (previous != current);
                    }
                    if(interruptType[idx] == InterruptType.LevelTriggered)
                    {
                        interruptPending = !current;
                    }
                    break;
                case InterruptPolarity.HighRising:
                    if(interruptType[idx] == InterruptType.EdgeTriggered)
                    {
                        interruptPending = current && (previous != current);
                    }
                    if(interruptType[idx] == InterruptType.LevelTriggered)
                    {
                        interruptPending = current;
                    }
                    break;
                default:
                    throw new Exception("Should not reach here.");
            }

            interruptStatus[idx] |= interruptPending;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var pendingCombinedInterrupt = false;
            for(var i = 0; i < NumberOfConnections; ++i)
            {
                var pendingInterrupt = interruptEnabled[i] && interruptStatus[i];
                if(hasDedicatedIRQs && i < NumberOfDedicatedIRQs)
                {
                    DedicatedIRQs[i].Set(pendingInterrupt);
                }
                else
                {
                    pendingCombinedInterrupt |= pendingInterrupt;
                }
            }
            CombinedIRQ.Set(pendingCombinedInterrupt);
        }

        private void DefineRegisters()
        {
            Registers.Data.Define(this)
                .WithFlags(0, 16, name: "DATA",
                    valueProviderCallback: (i, _) =>
                    {
                        if(pinOutputEnabled[i])
                        {
                            this.Log(LogLevel.Noisy, "Trying to get value from pin {0} which is configured as output");
                            return false;
                        }
                        return State[i];
                    },
                    writeCallback: (i, _, val) => UpdatePinOutput(i, val))
                .WithReservedBits(16, 16);
            ;
            Registers.DataOutput.Define(this)
                .WithFlags(0, 16, name: "DATAOUT",
                    valueProviderCallback: (i, _) => State[i] && pinOutputEnabled[i],
                    writeCallback: (i, _, val) => UpdatePinOutput(i, val))
                .WithReservedBits(16, 16);
            ;
            Registers.OutputEnableSet.Define(this)
                .WithFlags(0, 16, name: "OUTENSET",
                    valueProviderCallback: (i, _) => pinOutputEnabled[i],
                    writeCallback: (i, _, val) => { if(val) pinOutputEnabled[i] = true; })
                .WithReservedBits(16, 16);
            ;
            Registers.OutputEnableClear.Define(this)
                .WithFlags(0, 16, name: "OUTENCLR",
                    valueProviderCallback: (i, _) => pinOutputEnabled[i],
                    writeCallback: (i, _, val) => { if(val) pinOutputEnabled[i] = false; })
                .WithReservedBits(16, 16);
            ;
            Registers.AlternateFunctionSet.Define(this)
                .WithFlags(0, 16, name: "ALTFUNCSET",
                    valueProviderCallback: (i, _) => pinAlternateFunctionEnabled[i],
                    writeCallback: (i, _, val) => { if(val) pinAlternateFunctionEnabled[i] = true; })
                .WithReservedBits(16, 16);
            ;
            Registers.AlternateFunctionClear.Define(this)
                .WithFlags(0, 16, name: "ALTFUNCCLR",
                    valueProviderCallback: (i, _) => pinAlternateFunctionEnabled[i],
                    writeCallback: (i, _, val) => { if(val) pinAlternateFunctionEnabled[i] = false; })
                .WithReservedBits(16, 16);
            ;
            Registers.InterruptEnableSet.Define(this)
                .WithFlags(0, 16, name: "INTENSET",
                    valueProviderCallback: (i, _) => interruptEnabled[i],
                    writeCallback: (i, _, val) => { if(val) interruptEnabled[i] = true; })
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            ;
            Registers.InterruptEnableClear.Define(this)
                .WithFlags(0, 16, name: "INTENCLR",
                    valueProviderCallback: (i, _) => interruptEnabled[i],
                    writeCallback: (i, _, val) => { if(val) interruptEnabled[i] = false; })
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            ;
            Registers.InterruptTypeSet.Define(this)
                .WithFlags(0, 16, name: "INTTYPESET",
                    valueProviderCallback: (i, _) => interruptType[i] == InterruptType.LevelTriggered ? false : true,
                    writeCallback: (i, _, val) => { if(val) interruptType[i] = InterruptType.EdgeTriggered; })
                .WithReservedBits(16, 16);
            ;
            Registers.InterruptTypeClear.Define(this)
                .WithFlags(0, 16, name: "INTTYPECLR",
                    valueProviderCallback: (i, _) => interruptType[i] == InterruptType.LevelTriggered ? false : true,
                    writeCallback: (i, _, val) => { if(val) interruptType[i] = InterruptType.LevelTriggered; })
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            ;
            Registers.InterruptPolaritySet.Define(this)
                .WithFlags(0, 16, name: "INTPOLSET",
                    valueProviderCallback: (i, _) => interruptPolarity[i] == InterruptPolarity.LowFalling ? false : true,
                    writeCallback: (i, _, val) => { if(val) interruptPolarity[i] = InterruptPolarity.HighRising; })
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            ;
            Registers.InterruptPolarityClear.Define(this)
                .WithFlags(0, 16, name: "INTPOLCLR",
                    valueProviderCallback: (i, _) => interruptPolarity[i] == InterruptPolarity.LowFalling ? false : true,
                    writeCallback: (i, _, val) => { if(val) interruptPolarity[i] = InterruptPolarity.LowFalling; })
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) => UpdateInterrupts());
            ;
            Registers.InterruptStatus_InterruptClear.Define(this)
                .WithFlags(0, 16, name: "INTSTATUS_INTCLEAR",
                    valueProviderCallback: (i, _) => interruptStatus[i],
                    writeCallback: (i, _, value) =>
                    {
                        if(value)
                        {
                            interruptStatus[i] = false;
                        }
                    })
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;
            Registers.SoftReset.Define(this)
                .WithFlag(0, out softReset, name: "SOFTRESET")
                .WithReservedBits(1, 31);
            ;
            Registers.PullEnable.Define(this)
                .WithFlags(0, 16, out pullEnable, name: "PULL_ENABLE")
                .WithReservedBits(16, 16);
            ;

            Registers.PullUpDown.Define(this)
                .WithFlags(0, 16, out pullUpDown, name: "PULL_UP_DOWN")
                .WithReservedBits(16, 16);
            ;

            Registers.MaskLowByte0.DefineMany(this, 256, (register, idx) =>
            {
                register
                    .WithTag("MASKLOWBYTE", 0, 8)
                    .WithReservedBits(8, 24);
            });

            Registers.MaskHighByte0.DefineMany(this, 256, (register, idx) =>
            {
                register
                    .WithTag("MASKHIGHBYTE", 0, 8)
                    .WithReservedBits(8, 24);
            });
        }

        private IFlagRegisterField softReset;
        private IFlagRegisterField[] pullEnable;
        private IFlagRegisterField[] pullUpDown;
        private GPIO[] DedicatedIRQs { get; }

        private readonly bool[] pinOutputEnabled = new bool[NumberOfGPIOs];
        private readonly bool[] pinAlternateFunctionEnabled = new bool[NumberOfGPIOs];
        private readonly bool[] interruptEnabled = new bool[NumberOfGPIOs];
        private readonly bool[] interruptStatus = new bool[NumberOfGPIOs];
        private readonly uint alternateFunctionResetValue;
        private readonly bool hasDedicatedIRQs;
        private readonly InterruptType[] interruptType = new InterruptType[NumberOfGPIOs];
        private readonly InterruptPolarity[] interruptPolarity = new InterruptPolarity[NumberOfGPIOs];

        private const int NumberOfGPIOs = 16;
        private const int NumberOfDedicatedIRQs = 8;

        private enum InterruptType
        {
            LevelTriggered = 0,
            EdgeTriggered,
        }

        private enum InterruptPolarity
        {
            LowFalling = 0,
            HighRising,
        }

        private enum Registers
        {
            Data = 0x000,
            DataOutput = 0x004,
            OutputEnableSet = 0x010,
            OutputEnableClear = 0x014,
            AlternateFunctionSet = 0x018,
            AlternateFunctionClear = 0x01c,
            InterruptEnableSet = 0x020,
            InterruptEnableClear = 0x024,
            InterruptTypeSet = 0x028,
            InterruptTypeClear = 0x02c,
            InterruptPolaritySet = 0x030,
            InterruptPolarityClear = 0x034,
            InterruptStatus_InterruptClear = 0x038,
            SoftReset = 0x03c,  // UT32 specific
            PullEnable = 0x040, // UT32 specific
            PullUpDown = 0x044, // UT32 specific
            MaskLowByte0 = 0x400,  // 256 MaskLowByte registers, 0x400-0x7fc
            MaskHighByte0 = 0x800, // 256 MaskHighByte registers, 0x800-0xbfc
        }
    }
}
