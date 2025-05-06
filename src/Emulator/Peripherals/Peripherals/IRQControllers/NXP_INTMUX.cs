//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class NXP_INTMUX : BasicDoubleWordPeripheral, ILocalGPIOReceiver, INumberedGPIOOutput, IKnownSize
    {
        public NXP_INTMUX(IMachine machine) : base(machine)
        {
            var connections = new Dictionary<int, IGPIO>();
            channels = new Channel[numberOfChannels];

            for(int channelNumber = 0; channelNumber < numberOfChannels; channelNumber++)
            {
                channels[channelNumber] = new Channel(this, channelNumber);
                connections.Add(channelNumber, new GPIO());
            }

            Connections = new ReadOnlyDictionary<int, IGPIO>(connections);
        }

        public IGPIOReceiver GetLocalReceiver(int index)
        {
            return channels[index];
        }

        public void InterruptHandler(Channel sender, bool value)
        {
            if(Connections.TryGetValue(sender.channelNumber, out var gpio))
            {
                gpio.Set(value);
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }
        public long Size => Channel.size * numberOfChannels;

        private static readonly int numberOfChannels = 8;
        private readonly Channel[] channels;

        public class Channel : IGPIOReceiver
        {
            public static readonly long size = 0x40;
            public static readonly int maxInterrruptSources = 32;

            public Channel(NXP_INTMUX intmux, int channelNumber)
            {
                this.intmux = intmux;
                this.channelNumber = channelNumber;
                BuildRegisters();
                Reset();
            }

            public void OnGPIO(int number, bool value)
            {
                if(number > maxInterrruptSources || number < 0)
                {
                    intmux.Log(LogLevel.Warning, "Channel {0} received interrupt from source {1}, which is outside of range 0 - {2}", channelNumber, number, maxInterrruptSources - 1);
                    return;
                }

                if(!interruptEnable[number].Value)
                {
                    intmux.Log(LogLevel.Noisy, "Channel {0}, received interrupt from disabled source {1}", channelNumber, number);
                    return;
                }

                interruptPending[number].Value = value;
                UpdateVectorNumber();

                if(!value || (value && CanTriggerGPIO()))
                {
                    intmux.InterruptHandler(this, value);
                }
            }

            public void Reset()
            {
                softwareReset.Value = false;
                logicAND.Value = false;
                channelInputNumber.Value = 0b00; // 32 interrupt inputs
                channelInstanceNumber.Value = (ulong)channelNumber;
                channelInterruptRequestPending.Value = false;

                vectorNumber.Value = 0;

                for(int interruptSource = 0; interruptSource < maxInterrruptSources; interruptSource++)
                {
                    interruptEnable[interruptSource].Value = false;

                    interruptPending[interruptSource].Value = false;
                }
            }

            public readonly int channelNumber;

            private bool CanTriggerGPIO()
            {
                bool result = logicAND.Value;

                for(int interruptSource = 0; interruptSource < maxInterrruptSources; interruptSource++)
                {
                    if(interruptEnable[interruptSource].Value)
                    {
                        result = logicAND.Value ?
                            result && interruptPending[interruptSource].Value :
                            result || interruptPending[interruptSource].Value;
                    }
                }

                return result;
            }

            private void UpdateVectorNumber()
            {
                // Lower interupt source => higher priority
                var interruptSource = interruptPending.IndexOf(i => i.Value);
                if(interruptSource != -1)
                {
                    vectorNumber.Value = (ulong)interruptSource + 48;
                    return;
                }

                vectorNumber.Value = 0;
            }

            private void UpdateInterruptEnable(int index, bool oldValue, bool newValue)
            {
                if(!newValue && interruptPending[index].Value)
                {
                    interruptPending[index].Value = false;
                    UpdateVectorNumber();
                    intmux.InterruptHandler(this, false);
                }

                intmux.Log(LogLevel.Debug, "Channel {0}, changed interrupt {1} enable: {2} -> {3}", channelNumber, index, oldValue, newValue);
            }

            private void BuildRegisters()
            {
                intmux.RegistersCollection.AddRegister(GetRegisterAddress(ChannelRegisters.ControlStatusRegister), new DoubleWordRegister(this)
                    .WithFlag(0, out softwareReset, FieldMode.Toggle, name: "SoftwareReset",
                        changeCallback: (_, __) => Reset())
                    .WithFlag(1, out logicAND, name: "LogicAND")
                    .WithReservedBits(2, 2)
                    .WithValueField(4, 2, out channelInputNumber, name: "ChannelInputNumber")
                    .WithReservedBits(6, 2)
                    .WithValueField(8, 4, out channelInstanceNumber, name: "ChannelInstanceNumber")
                    .WithReservedBits(12, 19)
                    .WithFlag(31, out channelInterruptRequestPending, name: "ChannelInterruptRequestPending")
                );

                intmux.RegistersCollection.AddRegister(GetRegisterAddress(ChannelRegisters.VectorNumberRegister), new DoubleWordRegister(this)
                    .WithReservedBits(0, 2)
                    .WithValueField(2, 12, out vectorNumber, FieldMode.Read, name: "VectorNumber")
                    .WithReservedBits(14, 18)
                );

                intmux.RegistersCollection.AddRegister(GetRegisterAddress(ChannelRegisters.InterruptEnableRegister), new DoubleWordRegister(this)
                    .WithFlags(0, 32, out interruptEnable, name: "InterruptEnable",
                        changeCallback: UpdateInterruptEnable)
                );

                intmux.RegistersCollection.AddRegister(GetRegisterAddress(ChannelRegisters.InterruptPendingRegister), new DoubleWordRegister(this)
                    .WithFlags(0, 32, out interruptPending, FieldMode.Read, name: "InterruptPending")
                );
            }

            private long GetRegisterAddress(ChannelRegisters register)
            {
                return (long)register + channelNumber * Channel.size;
            }

            // Control Status Register fields
            private IFlagRegisterField softwareReset;
            private IFlagRegisterField logicAND;
            private IValueRegisterField channelInputNumber;
            private IValueRegisterField channelInstanceNumber;
            private IFlagRegisterField channelInterruptRequestPending;

            // VectorNumberRegister fields
            private IValueRegisterField vectorNumber;

            // InterruptEnableRegister fields
            private IFlagRegisterField[] interruptEnable;

            // InterruptPendingRegister fields
            private IFlagRegisterField[] interruptPending;

            private readonly NXP_INTMUX intmux;

            public enum ChannelRegisters : long
            {
                ControlStatusRegister = 0x00, // CHn_CSR
                VectorNumberRegister = 0x04, // CHn_VEC
                InterruptEnableRegister = 0x10, // CHn_IER
                InterruptPendingRegister = 0x20 // Chn_IPR
            }
        }
    }
}