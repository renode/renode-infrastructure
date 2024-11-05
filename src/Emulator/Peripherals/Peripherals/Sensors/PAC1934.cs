//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2020 Hugh Breslin <Hugh.Breslin@microchip.com>
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class PAC1934 : II2CPeripheral
    {
        public PAC1934()
        {
            channels = new Channel[ChannelCount];
            for(var i = 0; i < ChannelCount; ++i)
            {
                channels[i] = new Channel(this, i);
            }

            var registersMap = new Dictionary<long, ByteRegister>();
            registersMap.Add(
                (long)Registers.ChannelDisable,
                new ByteRegister(this)
                    .WithReservedBits(0, 1)
                    .WithFlag(1, out skipInactiveChannels, name: "NO_SKIP")
                    .WithTag("BYTE_COUNT", 2, 1)
                    .WithTag("TIMEOUT", 3, 1)
                    .WithFlag(4, out channels[3].IsChannelDisabled, name: "CH4")
                    .WithFlag(5, out channels[2].IsChannelDisabled, name: "CH3")
                    .WithFlag(6, out channels[1].IsChannelDisabled, name: "CH2")
                    .WithFlag(7, out channels[0].IsChannelDisabled, name: "CH1")
            );
            registersMap.Add(
                (long)Registers.ProductId,
                new ByteRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => ProductId)
                );
            registersMap.Add(
                (long)Registers.ManufacturerId,
                new ByteRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => ManufacturerId)
                );
            registersMap.Add(
                (long)Registers.RevisionId,
                new ByteRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => RevisionId)
                );

            registers = new ByteRegisterCollection(this, registersMap);
        }

        public void FinishTransmission()
        {
        }

        public byte[] Read(int count = 1)
        {
            // we do not use `count` due to block read operations
            if(state == State.NoRegisterContext)
            {
                this.Log(LogLevel.Warning, "Trying to read but no register is selected");
                return new byte[] { 0 };
            }
            return ReadRegister((uint)context);
        }

        public void Write(byte[] data)
        {
            // TODO: implement the auto-incrementing pointer described in the docs
            if(state == State.NoRegisterContext)
            {
                context = (Registers)data[0];
                state = GetStateBasedOnContext(context);
                if(state != State.RefreshContext)
                {
                    return;
                }
            }
            WriteRegister(context, data[0]);
        }

        public void Reset()
        {
            accumulatorCount = 0;
            state = default(State);
            context = default(Registers);
            for(var i = 0; i < ChannelCount; ++i)
            {
                channels[i].Reset();
            }
            registers.Reset();
        }

        private void WriteRegister(Registers register, byte data)
        {
            switch(register)
            {
                case Registers.Refresh:
                case Registers.RefreshG:
                    RefreshChannels(RefreshType.WithAccumulators);
                    break;
                case Registers.RefreshV:
                    RefreshChannels(RefreshType.NoAccumulators);
                    break;
                default:
                    registers.Write((long)register, data);
                    break;
            }
            state = State.NoRegisterContext;
        }

        private byte[] ReadRegister(uint offset)
        {
            state = State.NoRegisterContext;
            if(offset >= (uint)Registers.ProportionalPowerAccumulator1 && offset <= (uint)Registers.ProportionalPower4)
            {
                var channelNumber = (offset - 3) % 4;
                return channels[channelNumber].GetBytesFromChannelOffset(offset - channelNumber);
            }
            if(offset == (uint)Registers.AccumulatorCount)
            {
                return BitConverter.GetBytes(accumulatorCount);
            }
            return BitConverter.GetBytes((ushort)registers.Read(offset));
        }

        private void RefreshChannels(RefreshType refresh)
        {
            for(int i = 0; i < ChannelCount; ++i)
            {
                if(channels[i].IsChannelDisabled.Value && !skipInactiveChannels.Value)
                {
                    channels[i].RefreshInactiveChannel(refresh);
                }
                else if(!channels[i].IsChannelDisabled.Value)
                {
                    channels[i].RefreshActiveChannel(refresh);
                    accumulatorCount += (refresh == RefreshType.WithAccumulators ? 1u : 0);
                }
            }
        }

        private State GetStateBasedOnContext(Registers data)
        {
            if(data == Registers.Refresh || data == Registers.RefreshG || data == Registers.RefreshV)
            {
                return State.RefreshContext;
            }
            return State.RegisterContextSet;
        }

        private State state;
        private Registers context;
        private uint accumulatorCount;

        private readonly Channel[] channels;
        private readonly ByteRegisterCollection registers;

        private readonly IFlagRegisterField skipInactiveChannels;

        private const int ProductId = 0x5B;
        private const int ManufacturerId = 0x5D;
        private const int RevisionId = 0x3;

        private const int ChannelCount = 4;
        private const int ShiftBetweenChannelRegisters = 4;

        private class Channel
        {
            public Channel(PAC1934 parent, int number)
            {
                this.parent = parent;
                channelNumber = number;
                vBusQueue = new Queue<ushort>();
                vSenseQueue = new Queue<ushort>();
            }

            public byte[] GetBytesFromChannelOffset(long offset)
            {
                switch(offset)
                {
                    case (long)Registers.ProportionalPowerAccumulator1:
                        return BitConverter.GetBytes(proportionalPowerAccumulator);
                    case (long)Registers.BusVoltage1:
                        return BitConverter.GetBytes(busVoltage);
                    case (long)Registers.SenseResistorVoltage1:
                        return BitConverter.GetBytes(senseResistorVoltage);
                    case (long)Registers.AverageBusVoltage1:
                        return BitConverter.GetBytes(averageBusVoltage);
                    case (long)Registers.SenseResistorAverageVoltage1:
                        return BitConverter.GetBytes(senseResistorAverageVoltage);
                    case (long)Registers.ProportionalPower1:
                        return BitConverter.GetBytes(proportionalPower);
                    default:
                        parent.Log(LogLevel.Warning, "Trying to read bytes from unhandled channel {0} at offset 0x{1:X}", channelNumber, offset);
                        return new byte[] { 0 };
                }
            }

            public void Reset()
            {
                busVoltage = 0;
                proportionalPower = 0;
                averageBusVoltage = 0;
                senseResistorVoltage = 0;
                senseResistorAverageVoltage = 0;
                proportionalPowerAccumulator = 0;
            }

            public void RefreshActiveChannel(RefreshType refresh)
            {
                // populate the registers with dummy data
                var randomizer = EmulationManager.Instance.CurrentEmulation.RandomGenerator;

                proportionalPower = SampleBusVoltage * SampleSenseResistorVoltage;
                if(refresh == RefreshType.WithAccumulators)
                {
                    proportionalPowerAccumulator += proportionalPower;
                }
                busVoltage = (ushort)(SampleBusVoltage + randomizer.Next(-20, 20));
                senseResistorVoltage = (ushort)(SampleSenseResistorVoltage + randomizer.Next(-20, 20));
                averageBusVoltage = GetAverage(vBusQueue, busVoltage);
                senseResistorAverageVoltage = GetAverage(vSenseQueue, senseResistorVoltage);
            }

            public void RefreshInactiveChannel(RefreshType refresh)
            {
                if(refresh == RefreshType.WithAccumulators)
                {
                    proportionalPowerAccumulator = 0xFFFFFFFFFFFF;
                }
                busVoltage = 0xFFFF;
                senseResistorVoltage = 0xFFFF;
                averageBusVoltage = 0xFFFF;
                senseResistorAverageVoltage = 0xFFFF;
                proportionalPower = 0xFFFFFFF;
            }

            public IFlagRegisterField IsChannelDisabled;

            private ushort GetAverage(Queue<ushort> queue, ushort value)
            {
                var result = 0u;
                if(queue.Count == 8)
                {
                    queue.Dequeue();
                }
                queue.Enqueue(value);
                foreach(var val in queue)
                {
                    result += val;
                }
                return (ushort)(result / queue.Count);
            }

            private readonly Queue<ushort> vSenseQueue;
            private readonly Queue<ushort> vBusQueue;
            private readonly int channelNumber;
            private readonly PAC1934 parent;
            private ulong proportionalPowerAccumulator;
            private ushort busVoltage;
            private ushort senseResistorVoltage;
            private ushort averageBusVoltage;
            private ushort senseResistorAverageVoltage;
            private uint proportionalPower;

            private const ushort SampleBusVoltage = 3500;
            private const ushort SampleSenseResistorVoltage = 3500;
        }

        private enum Registers : byte
        {
            // General Registers
            Refresh = 0x00,
            Control = 0x1,
            AccumulatorCount = 0x2,

            // Channel Registers
            ProportionalPowerAccumulator1 = 0x3,
            ProportionalPowerAccumulator2 = 0x4,
            ProportionalPowerAccumulator3 = 0x5,
            ProportionalPowerAccumulator4 = 0x6,
            BusVoltage1 = 0x7,
            BusVoltage2 = 0x8,
            BusVoltage3 = 0x9,
            BusVoltage4 = 0xA,
            SenseResistorVoltage1 = 0xB,
            SenseResistorVoltage2 = 0xC,
            SenseResistorVoltage3 = 0xD,
            SenseResistorVoltage4 = 0xE,
            AverageBusVoltage1 = 0xF,
            AverageBusVoltage2 = 0x10,
            AverageBusVoltage3 = 0x11,
            AverageBusVoltage4 = 0x12,
            SenseResistorAverageVoltage1 = 0x13,
            SenseResistorAverageVoltage2 = 0x14,
            SenseResistorAverageVoltage3 = 0x15,
            SenseResistorAverageVoltage4 = 0x16,
            ProportionalPower1 = 0x17,
            ProportionalPower2 = 0x18,
            ProportionalPower3 = 0x19,
            ProportionalPower4 = 0x1A,

            // General Registers
            ChannelDisable = 0x1C,
            BidirectionalCurrentMeasurement = 0x1D,
            RefreshG = 0x1E,
            RefreshV = 0x1F,
            SlowMode = 0x20,
            ControlImage = 0x21,
            ChannelDisableImage = 0x22,
            BidirectionalCurrentMeasurementImage = 0x23,
            ControlPreviousImage = 0x24,
            ChannelDisablePreviousImage = 0x25,
            BidirectionalCurrentMeasurementPreviousImage = 0x26,
            ProductId = 0xFD,
            ManufacturerId = 0xFE,
            RevisionId = 0xFF
        }

        private enum State : byte
        {
            NoRegisterContext = 0,
            RegisterContextSet = 1,
            RefreshContext = 2
        }

        private enum RefreshType : byte
        {
            NoAccumulators = 0,
            WithAccumulators = 1
        }
    }
}
