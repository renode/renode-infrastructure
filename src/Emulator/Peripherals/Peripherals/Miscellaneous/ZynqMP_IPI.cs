//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.IRQControllers;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ZynqMP_IPI : BasicDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput, IPeripheralRegister<ZynqMP_PlatformManagementUnit, NullRegistrationPoint>
    {
        public static long GetRegisterOffset(ChannelId channelId, RegisterOffset registerOffset)
        {
            var channelOffset = GetChannelOffsetFromId(channelId);
            return channelOffset + (long)registerOffset;
        }

        public static long GetMailboxOffset(ChannelId channelId)
        {
            switch(channelId)
            {
                case ChannelId.Channel0:
                    // Mailbox for channel 0 should start at offset 0x400 according
                    // to documetation, but DTS uses this address instead.
                    return 0x5c0;
                case ChannelId.Channel1:
                    return 0x0;
                case ChannelId.Channel2:
                    return 0x200;
                // All PMU channels have the same mailbox
                case ChannelId.Channel3:
                case ChannelId.Channel4:
                case ChannelId.Channel5:
                case ChannelId.Channel6:
                    return 0xe00;
                case ChannelId.Channel7:
                    return 0x600;
                case ChannelId.Channel8:
                    return 0x800;
                case ChannelId.Channel9:
                    return 0xa00;
                case ChannelId.Channel10:
                    return 0xc00;
                default:
                    throw new ArgumentOutOfRangeException("channelId");
            }
        }

        public ZynqMP_IPI(IMachine machine, MappedMemory mailbox) : base(machine)
        {
            this.mailbox = mailbox;
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var channelIdx = 0; channelIdx < NrOfChannels; ++channelIdx)
            {
                innerConnections[channelIdx] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            channels = new Channel[] {
                new Channel(this, ChannelId.Channel0, Connections[0]),
                new Channel(this, ChannelId.Channel1, Connections[1]),
                new Channel(this, ChannelId.Channel2, Connections[2]),
                new Channel(this, ChannelId.Channel3, Connections[3]),
                new Channel(this, ChannelId.Channel4, Connections[4]),
                new Channel(this, ChannelId.Channel5, Connections[5]),
                new Channel(this, ChannelId.Channel6, Connections[6]),
                new Channel(this, ChannelId.Channel7, Connections[7]),
                new Channel(this, ChannelId.Channel8, Connections[8]),
                new Channel(this, ChannelId.Channel9, Connections[9]),
                new Channel(this, ChannelId.Channel10, Connections[10])
            };
        }

        public override void Reset()
        {
            base.Reset();

            foreach(var gpio in Connections)
            {
                gpio.Value.Unset();
            }
        }

        public void Register(ZynqMP_PlatformManagementUnit peripheral, NullRegistrationPoint registrationPoint)
        {
            if(pmu != null)
            {
                throw new RegistrationException("A PMU is already registered.");
            }
            pmu = peripheral;
            pmu.RegisterIPI(this);
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(ZynqMP_PlatformManagementUnit peripheral)
        {
            pmu = null;
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        public long Size => 0x80000;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public readonly MappedMemory mailbox;

        private static Registers GetRegisterFromChannelIdAndRegisterOffset(ChannelId channelId, RegisterOffset registerOffset)
        {
            // This function is used only in channel construction and should receive hardcoded arguments.
            // Hence we don't want to catch exeception as it may indicate error in setup code.
            var channelOffset = GetChannelOffsetFromId(channelId);
            return (Registers)(channelOffset + (long)registerOffset);
        }

        private static long GetChannelOffsetFromId(ChannelId channelId)
        {
            switch(channelId)
            {
                case ChannelId.Channel0:
                    return 0x00000;
                case ChannelId.Channel1:
                    return 0x10000;
                case ChannelId.Channel2:
                    return 0x20000;
                case ChannelId.Channel3:
                    return 0x30000;
                case ChannelId.Channel4:
                    return 0x31000;
                case ChannelId.Channel5:
                    return 0x32000;
                case ChannelId.Channel6:
                    return 0x33000;
                case ChannelId.Channel7:
                    return 0x40000;
                case ChannelId.Channel8:
                    return 0x50000;
                case ChannelId.Channel9:
                    return 0x60000;
                case ChannelId.Channel10:
                    return 0x70000;
                default:
                    throw new ArgumentOutOfRangeException("channelId");
            }
        }

        private Channel GetChannelFromId(ChannelId channelId)
        {
            switch(channelId)
            {
                case ChannelId.Channel0:
                    return channels[0];
                case ChannelId.Channel1:
                    return channels[1];
                case ChannelId.Channel2:
                    return channels[2];
                case ChannelId.Channel3:
                    return channels[3];
                case ChannelId.Channel4:
                    return channels[4];
                case ChannelId.Channel5:
                    return channels[5];
                case ChannelId.Channel6:
                    return channels[6];
                case ChannelId.Channel7:
                    return channels[7];
                case ChannelId.Channel8:
                    return channels[8];
                case ChannelId.Channel9:
                    return channels[9];
                case ChannelId.Channel10:
                    return channels[10];
                default:
                    throw new ArgumentOutOfRangeException("channelId");
            }
        }

        private void TryTriggerInterrupt(ChannelId targetChannelId, ChannelId sourceChannelId)
        {
            try
            {
                var sourceChannel = GetChannelFromId(sourceChannelId);
                var targetChannel = GetChannelFromId(targetChannelId);

                // We set channels in Observation and StatusAndClear even if interrupt wasn't triggered
                sourceChannel.SetChannelInObservation(targetChannelId);
                targetChannel.SetChannelInStatusAndClear(sourceChannelId);

                if(targetChannel.CanBeTriggeredBy(sourceChannelId))
                {
                    targetChannel.TriggerInterrupt();
                }
            }
            catch(ArgumentOutOfRangeException)
            {
                // We assume that sourceChannelId is correct. It is set and checked in channel initialization.
                this.Log(LogLevel.Warning, "Trying to trigger interrupt on non existing channel with id 0x{0:X}. The interrupt won't be triggered.", (uint)targetChannelId);
            }
        }

        private void ClearInterrupt(ChannelId targetChannelId, ChannelId sourceChannelId)
        {
            try
            {
                var sourceChannel = GetChannelFromId(sourceChannelId);
                var targetChannel = GetChannelFromId(targetChannelId);

                sourceChannel.ClearChannelInObservation(targetChannelId);
                targetChannel.ClearChannelInStatusAndClear(sourceChannelId);
            }
            catch(ArgumentOutOfRangeException)
            {
                // We assume that sourceChannelId is correct. It is set and checked in channel initialization.
                this.Log(LogLevel.Warning, "Trying to clear interrupt from non existing channel with id 0x{0:X}. No interrupt will be cleared.", (uint)targetChannelId);
            }
        }

        private readonly Channel[] channels;

        private ZynqMP_PlatformManagementUnit pmu;

        private const int NrOfChannels = 11;

        // Each IPI register has it's unique offset within channel.
        // We use this offset to recognize registers.
        public enum RegisterOffset
        {
            Trigger         = Registers.Channel0Trigger,        // TRIG
            Observation     = Registers.Channel0Observation,    // OBS
            StatusAndClear  = Registers.Channel0StatusAndClear, // ISR
            Mask            = Registers.Channel0Mask,           // IMR
            EnableMask      = Registers.Channel0EnableMask,     // IER
            DisableMask     = Registers.Channel0DisableMask     // IDR
        }

        // Each channel has corresponding bit in interrupt value.
        // We use this bit as an id of a channel.
        // None channel is for compatibility with Flags.
        [Flags]
        public enum ChannelId : uint
        {
            None        = 0,
            Channel0    = 1,
            Channel1    = 1 << 8,
            Channel2    = 1 << 9,
            Channel3    = 1 << 16,
            Channel4    = 1 << 17,
            Channel5    = 1 << 18,
            Channel6    = 1 << 19,
            Channel7    = 1 << 24,
            Channel8    = 1 << 25,
            Channel9    = 1 << 26,
            Channel10   = 1 << 27
        }

        private class Channel
        {
            public Channel(ZynqMP_IPI ipi, ChannelId id, IGPIO IRQ)
            {
                this.ipi = ipi;
                this.id = id;
                this.IRQ = IRQ;

                GetRegisterFromChannelIdAndRegisterOffset(id, RegisterOffset.Trigger).Define(ipi)
                    .WithEnumField<DoubleWordRegister, ChannelId>(0, 32, FieldMode.Write,
                            writeCallback: (_, val) => HandleWriteToTrigger(val));

                GetRegisterFromChannelIdAndRegisterOffset(id, RegisterOffset.Observation).Define(ipi)
                    .WithEnumField<DoubleWordRegister, ChannelId>(0, 32, out observationField, FieldMode.Read);

                GetRegisterFromChannelIdAndRegisterOffset(id, RegisterOffset.StatusAndClear).Define(ipi)
                    .WithEnumField<DoubleWordRegister, ChannelId>(0, 32, out statusAndClearField,
                            writeCallback: (_, val) => HandleWriteToStatusAndClear(val));

                GetRegisterFromChannelIdAndRegisterOffset(id, RegisterOffset.Mask).Define(ipi)
                    .WithEnumField<DoubleWordRegister, ChannelId>(0, 32, out maskField, FieldMode.Read);

                GetRegisterFromChannelIdAndRegisterOffset(id, RegisterOffset.EnableMask).Define(ipi)
                    .WithEnumField<DoubleWordRegister, ChannelId>(0, 32, FieldMode.Write,
                            writeCallback: (_, val) => HandleWriteToEnableMask(val));

                GetRegisterFromChannelIdAndRegisterOffset(id, RegisterOffset.DisableMask).Define(ipi)
                    .WithEnumField<DoubleWordRegister, ChannelId>(0, 32, FieldMode.Write,
                            writeCallback: (_, val) => HandleWriteToDisableMask(val));
            }

            public bool CanBeTriggeredBy(ChannelId sourceChannelId)
            {
                return !maskField.Value.HasFlag(sourceChannelId);
            }

            public void TriggerInterrupt()
            {
                ipi.Log(LogLevel.Noisy, "Interrupt triggered on: {0}", id);
                IRQ.Set(true);
            }

            public void SetChannelInStatusAndClear(ChannelId channelId)
            {
                statusAndClearField.Value |= channelId;
            }

            public void ClearChannelInStatusAndClear(ChannelId channelId)
            {
                statusAndClearField.Value &= ~channelId;
                if (statusAndClearField.Value == ChannelId.None)
                {
                    IRQ.Unset();
                }
            }

            public void SetChannelInObservation(ChannelId channelId)
            {
                observationField.Value |= channelId;
            }

            public void ClearChannelInObservation(ChannelId channelId)
            {
                observationField.Value &= ~channelId;
            }

            private void HandleWriteToTrigger(ChannelId channelId)
            {
                ipi.TryTriggerInterrupt(channelId, this.id);
            }

            private void HandleWriteToStatusAndClear(ChannelId channelId)
            {
                ipi.ClearInterrupt(id, channelId);
            }

            private void HandleWriteToEnableMask(ChannelId channelId)
            {
                maskField.Value &= ~channelId;
            }

            private void HandleWriteToDisableMask(ChannelId channelId)
            {
                maskField.Value |= channelId;
                // It's not a documented behavior, but Linux running OpenAMP depends on it
                ipi.ClearInterrupt(id, channelId);
            }

            private readonly ZynqMP_IPI ipi;
            private readonly ChannelId id;
            private readonly IGPIO IRQ;
            private readonly IEnumRegisterField<ChannelId> observationField;
            private readonly IEnumRegisterField<ChannelId> statusAndClearField;
            private readonly IEnumRegisterField<ChannelId> maskField;
        }

        private enum Registers
        {
           Channel0Trigger          = 0x00000,
           Channel0Observation      = 0x00004,
           Channel0StatusAndClear   = 0x00010,
           Channel0Mask             = 0x00014,
           Channel0EnableMask       = 0x00018,
           Channel0DisableMask      = 0x0001c,

           Channel1Trigger          = 0x10000,
           Channel1Observation      = 0x10004,
           Channel1StatusAndClear   = 0x10010,
           Channel1Mask             = 0x10014,
           Channel1EnableMask       = 0x10018,
           Channel1DisableMask      = 0x1001c,

           Channel2Trigger          = 0x20000,
           Channel2Observation      = 0x20004,
           Channel2StatusAndClear   = 0x20010,
           Channel2Mask             = 0x20014,
           Channel2EnableMask       = 0x20018,
           Channel2DisableMask      = 0x2001c,

           Channel3Trigger          = 0x30000,
           Channel3Observation      = 0x30004,
           Channel3StatusAndClear   = 0x30010,
           Channel3Mask             = 0x30014,
           Channel3EnableMask       = 0x30018,
           Channel3DisableMask      = 0x3001c,

           Channel4Trigger          = 0x31000,
           Channel4Observation      = 0x31004,
           Channel4StatusAndClear   = 0x31010,
           Channel4Mask             = 0x31014,
           Channel4EnableMask       = 0x31018,
           Channel4DisableMask      = 0x3101c,

           Channel5Trigger          = 0x32000,
           Channel5Observation      = 0x32004,
           Channel5StatusAndClear   = 0x32010,
           Channel5Mask             = 0x32014,
           Channel5EnableMask       = 0x32018,
           Channel5DisableMask      = 0x3201c,

           Channel6Trigger          = 0x33000,
           Channel6Observation      = 0x33004,
           Channel6StatusAndClear   = 0x33010,
           Channel6Mask             = 0x33014,
           Channel6EnableMask       = 0x33018,
           Channel6DisableMask      = 0x3301c,

           Channel7Trigger          = 0x40000,
           Channel7Observation      = 0x40004,
           Channel7StatusAndClear   = 0x40010,
           Channel7Mask             = 0x40014,
           Channel7EnableMask       = 0x40018,
           Channel7DisableMask      = 0x4001c,

           Channel8Trigger          = 0x50000,
           Channel8Observation      = 0x50004,
           Channel8StatusAndClear   = 0x50010,
           Channel8Mask             = 0x50014,
           Channel8EnableMask       = 0x50018,
           Channel8DisableMask      = 0x5001c,

           Channel9Trigger          = 0x60000,
           Channel9Observation      = 0x60004,
           Channel9StatusAndClear   = 0x60010,
           Channel9Mask             = 0x60014,
           Channel9EnableMask       = 0x60018,
           Channel9DisableMask      = 0x6001c,

           Channel10Trigger         = 0x70000,
           Channel10Observation     = 0x70004,
           Channel10StatusAndClear  = 0x70010,
           Channel10Mask            = 0x70014,
           Channel10EnableMask      = 0x70018,
           Channel10DisableMask     = 0x7001c,
        }
    }
}
