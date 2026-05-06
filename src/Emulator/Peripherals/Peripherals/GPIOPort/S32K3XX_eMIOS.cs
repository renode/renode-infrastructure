//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class S32K3XX_eMIOS : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IHasMappedRegisters
    {
        public S32K3XX_eMIOS(IMachine machine, int numberOfChannels = 24, ulong clockFrequency = 48_000_000) : base(machine, numberOfChannels * 3)
        {
            this.numberOfChannels = numberOfChannels;
            this.clockFrequency = clockFrequency;
            unifiedChannels = new UnifiedChannel[numberOfChannels];
            flagEnableFlags = new IFlagRegisterField[numberOfChannels];
            BusController = machine.GetSystemBus(this);
            RegistersCollection = new DoubleWordRegisterCollection(this, DefineRegisters());
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
            foreach(var channel in unifiedChannels)
            {
                channel.Reset();
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            if(number >= numberOfChannels || number < 0)
            {
                this.WarningLog("GPIO input number {0} is outside of supported range 0-{1}", number, numberOfChannels - 1);
                return;
            }
            base.OnGPIO(number, value);
            unifiedChannels[number].InputPin = value;
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public void UpdateInterrupts()
        {
            // To workaround the limitation of only having one Connections array, GPIO's are (0,numberOfChannels-1), IRQ's are (numberOfChannels, (2*numberOfChannels)-1), DMA's are (2*numberOfChannels, (3*numberOfChannels)-1)
            for(var i = 0; i < numberOfChannels; i++)
            {
                var channel = unifiedChannels[i];
                // Because this function ends up being called during the creation of channels, ignore a given channel if it is not initialized yet
                if(channel is null)
                {
                    continue;
                }
                if(channel.DMA)
                {
                    Connections[i + numberOfChannels].Set(false);
                    Connections[i + (2 * numberOfChannels)].Set(channel.Flag);
                }
                else
                {
                    Connections[i + (2 * numberOfChannels)].Set(false);
                    Connections[i + numberOfChannels].Set(channel.Flag);
                }
            }
        }

        public string OffsetToString(long offset) => registerMapper.ToString(offset);

        public long Size => 0x31C;

        public bool ModuleDisable => moduleDisable.Value;

        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        private Dictionary<long, DoubleWordRegister> DefineRegisters()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>();

            registerMap.Add((long)Registers.ModuleConfiguration, new DoubleWordRegister(this)
                .WithReservedBits(31, 1)
                .WithFlag(30, out moduleDisable, name: "MDIS")
                .WithTaggedFlag("FRZ", 29)
                .WithTaggedFlag("GTBE", 28)
                .WithReservedBits(27, 1)
                .WithFlag(26, out globalPrescaleEnable, name: "GPREN")
                .WithReservedBits(16, 10)
                .WithValueField(8, 8, out globalPrescaler, name: "GPRE")
                .WithReservedBits(0, 8)
                .WithChangeCallback((_, _) => UpdateGlobalPrescaler())
            );

            registerMap.Add((long)Registers.GlobalFlag, new DoubleWordRegister(this)
                .WithReservedBits(numberOfChannels, 32 - numberOfChannels)
                .WithFlags(0, numberOfChannels,
                    valueProviderCallback: (channel, _) => unifiedChannels[channel].Flag,
                    writeCallback: (channel, _, value) =>
                    {
                        if(value)
                        {
                            unifiedChannels[channel].Flag = false;
                        }
                    })
                .WithWriteCallback((_, __) => UpdateInterrupts())
            );

            registerMap.Add((long)Registers.OutputUpdateDisable, new DoubleWordRegister(this)
                .WithReservedBits(numberOfChannels, 32 - numberOfChannels)
                .WithFlags(0, numberOfChannels,
                    valueProviderCallback: (channel, _) => unifiedChannels[channel].OutputUpdateDisable,
                    writeCallback: (channel, _, value) => unifiedChannels[channel].OutputUpdateDisable = value)
            );

            registerMap.Add((long)Registers.DisableChannel, new DoubleWordRegister(this)
                .WithReservedBits(numberOfChannels, 32 - numberOfChannels)
                .WithFlags(0, numberOfChannels, changeCallback: (channel, _, value) => unifiedChannels[channel].DisableChannel = value)
            );

            for(var i = 0; i < numberOfChannels; i++)
            {
                // The channel types are only fully accurate for eMIOS_0, as the others have type H channels instead of type G.
                // But G is a superset of H so the only effect is that some unspported modes could be allowed
                var channelType = i switch
                {
                    >= 1 and <= 7 => ChannelType.G,
                    >= 9 and <= 15 => ChannelType.H,
                    >= 17 and <= 21 => ChannelType.Y,
                    _ => ChannelType.X
                };
                unifiedChannels[i] = new UnifiedChannel(this, channelType, (uint)i, Connections[i], clockFrequency, registerMap);
            }

            // Connect Counter bus events to channels that can drive them
            unifiedChannels[0].CounterTickEvent += (value) => OnCounterBusTick(CounterBus.B, value);
            unifiedChannels[8].CounterTickEvent += (value) => OnCounterBusTick(CounterBus.C, value);
            unifiedChannels[16].CounterTickEvent += (value) => OnCounterBusTick(CounterBus.D, value);
            unifiedChannels[22].CounterTickEvent += (value) => OnCounterBusTick(CounterBus.F, value);
            unifiedChannels[23].CounterTickEvent += (value) => OnCounterBusTick(CounterBus.A, value);

            return registerMap;
        }

        private void OnCounterBusTick(CounterBus bus, uint value)
        {
            foreach(var channel in unifiedChannels.Where((ch) => ch.SelectedCounterBus == bus))
            {
                channel.CounterTick(value);
            }
        }

        private void UpdateGlobalPrescaler()
        {
            foreach(var channel in unifiedChannels)
            {
                if(globalPrescaleEnable.Value)
                {
                    channel.Frequency = clockFrequency / (globalPrescaler.Value + 1);
                }
                else
                {
                    channel.Frequency = clockFrequency;
                }
            }
        }

        private IValueRegisterField globalPrescaler;
        private IFlagRegisterField moduleDisable;
        private IFlagRegisterField globalPrescaleEnable;
        private readonly RegisterMapper registerMapper = new RegisterMapper(typeof(Registers));
        private readonly int numberOfChannels;
        private readonly ulong clockFrequency;
        private readonly UnifiedChannel[] unifiedChannels;
        private readonly IFlagRegisterField[] flagEnableFlags;
        private readonly IBusController BusController;

        private class UnifiedChannel
        {
            public static bool IsModeSupported(ChannelModes mode, ChannelType type)
            {
                switch(mode)
                {
                case ChannelModes.GPIO: return true;
                case ChannelModes.SingleActionInputCapture: return true;
                case ChannelModes.SingleActionOutputCompare: return true;
                case ChannelModes.ModulusCounter: return type == ChannelType.X;
                case ChannelModes.ModulusCounterBuffered: return type == ChannelType.X || type == ChannelType.G;
                case ChannelModes.InputPulseWidthMeasurement: return type == ChannelType.G || type == ChannelType.H;
                case ChannelModes.InputPeriodMeasurement: return type == ChannelType.G || type == ChannelType.H;
                case ChannelModes.DoubleActionOutputCompare: return type == ChannelType.G || type == ChannelType.H;
                case ChannelModes.OutputPWMFrequencyBuffered: return type == ChannelType.X || type == ChannelType.G;
                case ChannelModes.OutputPWMBufferedCenterAligned: return type == ChannelType.G;
                case ChannelModes.OutputPWMBuffered: return true;
                case ChannelModes.OutputPWMTrigger: return true;
                case ChannelModes.PulseEdgeCounting: return type == ChannelType.G;
                default: throw new UnreachableException($"Unexpected ChannelMode variant {mode}");
                }
            }

            public UnifiedChannel(S32K3XX_eMIOS parent, ChannelType type, uint channelNumber, IGPIO outputPin, ulong clockFrequency, Dictionary<long, DoubleWordRegister> registerMap)
            {
                this.type = type;
                this.channelNumber = channelNumber;
                this.parent = parent;
                this.OutputPin = outputPin;
                this.baseFrequency = clockFrequency;
                this.internalTimer = new LimitTimer(parent.machine.ClockSource, clockFrequency, parent,
                    $"UC{this.channelNumber} Internal Timer",
                    limit: 1,
                    eventEnabled: true,
                    enabled: false
                );
                internalTimer.LimitReached += () => InternalCounterTick();
                DefineRegisters(registerMap);
                Reset();
            }

            public void Reset()
            {
                Flag = false;
                A = 0;
                B = 0;
                OutputPin.Set(false);
                Mode = ChannelModes.GPIO;
                GPIOOutputMode = false;
                internalTimer.Reset();
                InputPin = false;
                DisableChannel = false;
                OutputUpdateDisable = false;
            }

            public void SetChannelMode(byte value)
            {
                // For now only GPIO, Single Action Input Capture, and Output PWM buffered modes are supported
                // Some modes have configuration encoded in this field, which is why it is not simple a cast
                if((value & GPIOMask) == GPIOOpcode)
                {
                    // Entering GPIO mode resets and disables the internal counter
                    internalTimer.Value = 0;
                    internalTimer.Enabled = false;
                    Mode = ChannelModes.GPIO;
                    // GPIO submodes, 1 for output, 0 for input
                    GPIOOutputMode = BitHelper.IsBitSet(value, 0);
                    if(GPIOOutputMode)
                    {
                        // In GPIO Output mode the output pin is always equal to EDPOL
                        OutputPin.Set(edgePolarity.Value);
                    }
                    return;
                }
                if((value & MCBMask) == MCBOPCode)
                {
                    // MCB mode
                    Mode = ChannelModes.ModulusCounterBuffered;
                    // For the MVP we only support Up Count submode
                    if(BitHelper.GetValue(value, 1, 3) == 0)
                    {
                        // As we don't support external clocks, MCB Up will always use the internal timer
                        internalTimer.Enabled = true;
                        OutputPin.Set(false);
                    }
                    else
                    {
                        parent.ErrorLog("Only the MCB Up Count submode supported");
                    }
                    parent.DebugLog("Set channel {0} into MCB mode,", channelNumber);
                    return;
                }
                if((value & SAICMask) == SAICOpcode)
                {
                    Mode = ChannelModes.SingleActionInputCapture;
                    SAICEdgeCapture = BitHelper.IsBitSet(value, 7);
                    OutputPin.Set(false);
                    internalTimer.Enabled = busSelect.Value == 0b11;
                    parent.DebugLog("Set channel {0} into SAIC mode,", channelNumber);
                    return;
                }
                if((value & OPWMBMask) == OPWMBOpcode)
                {
                    Mode = ChannelModes.OutputPWMBuffered;
                    OPWMBMatchAS1 = BitHelper.IsBitSet(value, 1);
                    OutputPin.Set(edgePolarity.Value);
                    parent.DebugLog("Set channel {0} into OPWMB mode,", channelNumber);
                    return;
                }
                // No other modes supported yet
                parent.ErrorLog("Non-supported mode configuration 0b{0:b} for channel {1}. Only GPIO, SAIC, OPWMB, and MCB Up modes supported for now", value, channelNumber);
            }

            public void CounterTick(uint counterValue)
            {
                if(DisableChannel || parent.ModuleDisable)
                {
                    return;
                }
                switch(Mode)
                {
                case ChannelModes.ModulusCounterBuffered:
                    // MCB mode is only internally driven, so it ignores the counter bus value
                    if(counterField.Value == 0)
                    {
                        AS1 = AS2;
                        BS1 = BS2;
                    }
                    if(counterField.Value >= AS1)
                    {
                        if(flagEnable.Value)
                        {
                            Flag = true;
                        }
                        counterField.Value = 0;
                    }
                    break;
                case ChannelModes.SingleActionInputCapture:
                    counterField.Value = counterValue;
                    break;
                case ChannelModes.OutputPWMBuffered:
                    counterField.Value = counterValue;
                    if(counterField.Value == 0)
                    {
                        AS1 = AS2;
                        BS1 = BS2;
                    }
                    // BS1 matches take priority over AS1 if both happen in the same cycle
                    if(counterField.Value == BS1)
                    {
                        OutputPin.Set(!edgePolarity.Value);
                        if(flagEnable.Value)
                        {
                            Flag = true;
                        }
                    }
                    else if(counterField.Value == AS1)
                    {
                        OutputPin.Set(edgePolarity.Value);
                        if(flagEnable.Value && OPWMBMatchAS1)
                        {
                            Flag = true;
                        }
                    }
                    break;
                default:
                    // Other modes need no future action
                    // This log is gated behind debug for performance, as it is extremely noisy
#if DEBUG
                    parent.NoisyLog("UC channel {0} is in mode {1} which does not update anything in tick function", channelNumber, Mode);
#endif
                    break;
                }
            }

            public bool OutputUpdateDisable { get; set; }

            public bool DisableChannel { get; set; }

            public bool Flag
            {
                get => flagField.Value;
                set
                {
                    flagField.Value = value;
                    if(!value)
                    {
                        // Clearing the flag field also clears the overrun flag
                        overrunFlag.Value = false;
                    }
                    parent.UpdateInterrupts();
                }
            }

            public bool DMA
            {
                get => dmaToggleFlag.Value;
                set
                {
                    dmaToggleFlag.Value = value;
                    parent.UpdateInterrupts();
                }
            }

            public bool Overrun => overrunFlag.Value;

            public bool Overflow => overflowFlag.Value;

            public uint A
            {
                get
                {
                    switch(Mode)
                    {
                    case ChannelModes.GPIO:
                        return AS1;
                    case ChannelModes.SingleActionInputCapture:
                        return AS2;
                    case ChannelModes.ModulusCounterBuffered:
                    case ChannelModes.OutputPWMBuffered:
                        return AS1;
                    default:
                        parent.WarningLog("Tried to read A in unsupported mode {0}", Mode);
                        return 0;
                    }
                }

                set
                {
                    switch(Mode)
                    {
                    case ChannelModes.GPIO:
                        AS1 = value;
                        AS2 = value;
                        break;
                    case ChannelModes.SingleActionInputCapture:
                        // SAIC mode ignores writes to A
                        break;
                    case ChannelModes.ModulusCounterBuffered:
                    case ChannelModes.OutputPWMBuffered:
                        AS2 = value;
                        break;
                    default:
                        parent.WarningLog("Tried to write A in unsupported mode {0}", Mode);
                        break;
                    }
                }
            }

            public uint AltA
            {
                get
                {
                    switch(Mode)
                    {
                    case ChannelModes.GPIO:
                        return AS2;
                    default:
                        // Not enabled in any other supported modes, value is undefined
                        parent.WarningLog("Tried to read AltA in unsupported mode {0}", Mode);
                        return 0;
                    }
                }

                set
                {
                    switch(Mode)
                    {
                    case ChannelModes.GPIO:
                        AS2 = value;
                        break;
                    default:
                        // Not enabled in any other supported modes
                        parent.WarningLog("Tried to write AltA in unsupported mode {0}", Mode);
                        break;
                    }
                }
            }

            public uint B
            {
                get
                {
                    switch(Mode)
                    {
                    case ChannelModes.GPIO:
                        return BS1;
                    case ChannelModes.SingleActionInputCapture:
                        return BS2;
                    case ChannelModes.ModulusCounterBuffered:
                    case ChannelModes.OutputPWMBuffered:
                        return BS1;
                    default:
                        parent.WarningLog("Tried to read B in unsupported mode {0}", Mode);
                        return 0;
                    }
                }

                set
                {
                    switch(Mode)
                    {
                    case ChannelModes.GPIO:
                        BS1 = value;
                        BS2 = value;
                        break;
                    case ChannelModes.SingleActionInputCapture:
                        BS2 = value;
                        break;
                    case ChannelModes.ModulusCounterBuffered:
                    case ChannelModes.OutputPWMBuffered:
                        BS2 = value;
                        break;
                    default:
                        parent.WarningLog("Tried to write B in unsupported mode {0}", Mode);
                        break;
                    }
                }
            }

            public ChannelModes Mode { get; private set; }

            public CounterBus SelectedCounterBus => busSelect.Value switch
            {
                0b00 => channelNumber != 23 ? CounterBus.A : CounterBus.Reserved,
                0b01 => channelNumber switch
                {
                    >= 1 and <= 7 => CounterBus.B,
                    >= 9 and <= 15 => CounterBus.C,
                    >= 17 and <= 23 => CounterBus.D,
                    _ => CounterBus.Reserved
                },
                0b10 => channelNumber != 22 ? CounterBus.F : CounterBus.Reserved,
                0b11 => CounterBus.Internal,
                _ => throw new UnreachableException($"Impossible value for busSelect {busSelect.Value}"),
            };

            public ulong Frequency
            {
                get => internalTimer.Frequency;
                set
                {
                    baseFrequency = value;
                    UpdateFrequency();
                }
            }

            public bool InputPin
            {
                get => inputPin.Value;
                set
                {
                    // Shortcut if no state change
                    if(inputPin.Value == value)
                    {
                        return;
                    }
                    var isRisingEdge = value && !inputPin.Value;
                    switch(Mode)
                    {
                    case ChannelModes.GPIO:
                        if(!GPIOOutputMode)
                        {
                            // In GPIO Input mode, Edge Select set means to not set the flag
                            if(!edgeSelection.Value)
                            {
                                if((isRisingEdge && !edgePolarity.Value) || (!isRisingEdge && edgePolarity.Value))
                                {
                                    Flag = flagEnable.Value;
                                }
                            }
                        }
                        break;
                    case ChannelModes.SingleActionInputCapture:
                        // EDSEL == 1 indicates both edge trigger mode
                        if(((isRisingEdge && !edgePolarity.Value) || (!isRisingEdge && edgePolarity.Value) || (edgeSelection.Value)))
                        {
                            riseFall.Value = SAICEdgeCapture ? (edgePolarity.Value ? isRisingEdge : !isRisingEdge) : false;
                            // Capture the timestamp from the currently active bus
                            AS2 = (uint)counterField.Value;
                            if(Flag)
                            {
                                // Flag is already set, so set overrun flag
                                overrunFlag.Value = true;
                            }
                            Flag = flagEnable.Value;
                        }
                        break;
                    default:
                        parent.DebugLog("Input recived in mode {0} which does not use it", Mode);
                        break;
                    }
                    inputPin.Value = value;
                }
            }

            public event Action<uint> CounterTickEvent;

            public IGPIO OutputPin;

            private void InternalCounterTick()
            {
                if(DisableChannel || parent.ModuleDisable)
                {
                    return;
                }
                if(counterField.Value >= BitHelper.Bits(0, 24))
                {
                    // Internal Counter overflow
                    overflowFlag.Value = true;
                    counterField.Value = 0;
                }
                else
                {
                    counterField.Value += 1;
                }
                // In externally driven modes the internal counter is disabled so we can unconditionally call CounterTick here
                this.CounterTick((uint)counterField.Value);
                CounterTickEvent?.Invoke((uint)counterField.Value);
            }

            private void UpdateFrequency()
            {
                internalTimer.Frequency = baseFrequency / (extendedPrescaler.Value + 1);
            }

            private void DefineRegisters(Dictionary<long, DoubleWordRegister> registerMap)
            {
                registerMap.Add((long)Registers.UChannelA0 + (channelNumber * ChannelOffset), new DoubleWordRegister(parent)
                    .WithFlag(31, out riseFall, name: "RISE_FALL")
                    .WithReservedBits(24, 7)
                    .WithValueField(0, 24,
                        valueProviderCallback: _ => A,
                        writeCallback: (_, value) => A = (uint)value
                    )
                );
                registerMap.Add((long)Registers.UChannelB0 + (channelNumber * ChannelOffset), new DoubleWordRegister(parent)
                    .WithReservedBits(24, 8)
                    .WithValueField(0, 24,
                        valueProviderCallback: _ => B,
                        writeCallback: (_, value) => B = (uint)value
                    )
                );

                registerMap.Add((long)Registers.UChannelCounter0 + (channelNumber * ChannelOffset), new DoubleWordRegister(parent)
                    .WithReservedBits(24, 8)
                    .WithValueField(0, 23, out counterField, readCallback: (_, __) =>
                    {
                        if(parent.BusController.TryGetCurrentCPU(out var cpu))
                        {
                            cpu.SyncTime();
                        }
                    })
                );

                registerMap.Add((long)Registers.UChannelControl0 + (channelNumber * ChannelOffset), new DoubleWordRegister(parent)
                    .WithTaggedFlag("FREN", 31)
                    .WithFlag(30, out outputDisable, name: "ODIS")
                    .WithTag("ODISSL", 28, 2)
                    .WithValueField(26, 2, name: "UCPRE", // This register is a mirror of the lowest 2 bits of UCEXTPRE
                        valueProviderCallback: (_) => BitHelper.GetValue(extendedPrescaler.Value, 0, 2),
                        writeCallback: (_, value) => BitHelper.SetMaskedValue(extendedPrescaler.Value, value, 0, 2)
                    )
                    .WithTaggedFlag("UCPREN", 25)
                    .WithFlag(24, out dmaToggleFlag, name: "DMA")
                    .WithReservedBits(23, 1)
                    .WithTag("IF", 19, 4)
                    .WithTaggedFlag("FCK", 18)
                    .WithFlag(17, out flagEnable, name: "FEN")
                    .WithFlag(13, out forceMatchA, name: "FORCMA")
                    .WithFlag(12, out forceMatchB, name: "FORCMB")
                    .WithReservedBits(11, 1)
                    .WithValueField(9, 2, out busSelect, name: "BSL", changeCallback: (oldValue, newValue) =>
                    {
                        // When switching to or from the internal counter, the backing timer needs to be started or stopped
                        if(oldValue == 0b11)
                        {
                            internalTimer.Enabled = false;
                        }
                        else if(newValue == 0b11)
                        {
                            internalTimer.Enabled = true;
                        }
                    })
                    .WithFlag(8, out edgeSelection, name: "EDSEL")
                    .WithFlag(7, out edgePolarity, name: "EDPOL")
                    .WithValueField(0, 7, changeCallback: (_, value) => SetChannelMode((byte)value))
                    .WithWriteCallback((_, __) => parent.UpdateInterrupts())
                );
                registerMap.Add((long)Registers.UChannelStatus0 + (channelNumber * ChannelOffset), new DoubleWordRegister(parent)
                    .WithFlag(31, out overrunFlag, mode: FieldMode.Read | FieldMode.WriteOneToClear)
                    .WithReservedBits(16, 15)
                    .WithFlag(15, out overflowFlag, mode: FieldMode.Read | FieldMode.WriteOneToClear)
                    .WithReservedBits(3, 12)
                    .WithFlag(2, out inputPin, mode: FieldMode.Read)
                    .WithFlag(1, mode: FieldMode.Read, valueProviderCallback: _ => OutputPin.IsSet)
                    .WithFlag(0, out flagField, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "FLAG")
                    .WithChangeCallback((_, __) => parent.UpdateInterrupts())
                );
                registerMap.Add((long)Registers.AlternateAdress0 + (channelNumber * ChannelOffset), new DoubleWordRegister(parent)
                    .WithReservedBits(24, 8)
                    .WithValueField(0, 24,
                        valueProviderCallback: _ => AltA,
                        writeCallback: (_, value) => AltA = (uint)value
                    )
                );
                registerMap.Add((long)Registers.UChannelControl2_0 + (channelNumber * ChannelOffset), new DoubleWordRegister(parent)
                    .WithReservedBits(20, 12)
                    .WithValueField(16, 4, out extendedPrescaler, changeCallback: (_, _) => UpdateFrequency())
                    .WithReservedBits(15, 1)
                    .WithTaggedFlag("UCPRECLK", 14)
                    .WithReservedBits(5, 9)
                    .WithValueField(0, 5, out reloadSignalOutputDelay)
                );
            }

            private uint AS1 { get; set; }

            private uint AS2 { get; set; }

            private uint BS1 { get; set; }

            private uint BS2 { get; set; }

            private IFlagRegisterField riseFall;
            private IFlagRegisterField outputDisable;
            private IFlagRegisterField dmaToggleFlag;
            private IFlagRegisterField forceMatchA;
            private IFlagRegisterField forceMatchB;
            private IFlagRegisterField edgeSelection;
            private IFlagRegisterField edgePolarity;
            private IFlagRegisterField overrunFlag;
            private IFlagRegisterField overflowFlag;
            private IFlagRegisterField flagField;
            private IValueRegisterField reloadSignalOutputDelay;
            private IFlagRegisterField inputPin;
            private bool GPIOOutputMode;
            private bool SAICEdgeCapture;
            private ulong baseFrequency;
            private IValueRegisterField extendedPrescaler;
            private IFlagRegisterField flagEnable;
            private IValueRegisterField busSelect;
            private IValueRegisterField counterField;
            private bool OPWMBMatchAS1;
            private readonly ChannelType type;
            private readonly uint channelNumber;
            private readonly S32K3XX_eMIOS parent;
            private readonly LimitTimer internalTimer;
            private const uint MCBOPCode = 0b101_0000;
            private const uint GPIOOpcode = 0b000_0000;
            private const uint MCBMask = 0b111_1000;
            private const uint GPIOMask = 0b111_1110;
            private const int SAICMask = 0b011_1111;
            private const int SAICOpcode = 0b000_0010;
            private const byte OPWMBMask = 0b111_1101;
            private const byte OPWMBOpcode = 0b110_0000;
            private const uint ChannelOffset = 0x20;
        }

        private enum ChannelModes
        {
            GPIO,
            SingleActionInputCapture,
            SingleActionOutputCompare,
            ModulusCounter,
            ModulusCounterBuffered,
            InputPulseWidthMeasurement,
            InputPeriodMeasurement,
            DoubleActionOutputCompare,
            OutputPWMFrequencyBuffered,
            OutputPWMBufferedCenterAligned,
            OutputPWMBuffered,
            OutputPWMTrigger,
            PulseEdgeCounting
        }

        private enum ChannelType
        {
            X,
            Y,
            G,
            H
        }

        private enum CounterBus
        {
            A,
            B,
            C,
            D,
            F,
            Internal,
            Reserved
        }

        private enum Registers
        {
            ModuleConfiguration = 0x0,
            GlobalFlag = 0x4,
            OutputUpdateDisable = 0x8,
            DisableChannel = 0xC,
            UChannelA0 = 0x20,
            UChannelB0 = 0x24,
            UChannelCounter0 = 0x28,
            UChannelControl0 = 0x2C,
            UChannelStatus0 = 0x30,
            AlternateAdress0 = 0x34,
            UChannelControl2_0 = 0x38,
            // ...
            // Repeats for each channel
            // ...
            UChannelA23 = 0x300,
            UChannelB23 = 0x304,
            UChannelCounter23 = 0x308,
            UChannelControl23 = 0x30C,
            UChannelStatus23 = 0x310,
            AlternateAdress23 = 0x314,
            UChannelControl2_23 = 0x318,
        }
    }
}