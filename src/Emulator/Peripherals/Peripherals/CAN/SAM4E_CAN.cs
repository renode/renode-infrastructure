//
// Copyright (c) 2025 Menlo Systems GmbH
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CAN;

namespace Antmicro.Renode.Peripherals.CAN
{
    // SAM4E CAN controller with 8 mailboxes.
    //
    // Mailbox object types (MOT):
    //   0 = disabled, 1 = RX, 2 = RX overwrite, 3 = TX,
    //   4 = consumer, 5 = producer
    //
    // Register map:
    //   0x000-0x028: Global registers
    //   0x0E4-0x0E8: Write protection
    //   0x200-0x2FF: 8 mailboxes × 0x20 bytes each
    public class SAM4E_CAN : BasicDoubleWordPeripheral, IKnownSize, ICAN
    {
        public SAM4E_CAN(Machine machine) : base(machine)
        {
            IRQ = new GPIO();

            for(int i = 0; i < NumberOfMailboxes; i++)
            {
                mailboxes[i] = new Mailbox(i);
            }

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var mb in mailboxes)
            {
                mb.Reset();
            }
            interruptMask = 0;
            statusRegister = StatusBitErrorActive;
            IRQ.Unset();
        }

        public void OnFrameReceived(CANMessageFrame message)
        {
            if(!controllerEnabled)
            {
                this.Log(LogLevel.Warning, "Received frame while CAN controller is disabled, dropping");
                return;
            }

            this.Log(LogLevel.Debug, "Received frame: ID=0x{0:X}, len={1}, ext={2}", message.Id, message.Data.Length, message.ExtendedFormat);

            for(int i = 0; i < NumberOfMailboxes; i++)
            {
                var mb = mailboxes[i];
                if(mb.ObjectType != MailboxObjectType.RX && mb.ObjectType != MailboxObjectType.RXOverwrite)
                {
                    continue;
                }

                if(!MatchesFilter(mb, message))
                {
                    continue;
                }

                if(mb.MessageReady && mb.ObjectType == MailboxObjectType.RX)
                {
                    // RX mailbox already full, skip (try next)
                    continue;
                }

                // Store the message — update MID with received frame's ID
                mb.DataLow = PackDataLow(message.Data);
                mb.DataHigh = PackDataHigh(message.Data);
                mb.DataLengthCode = (uint)Math.Min(message.Data.Length, 8);
                var receivedMid = message.ExtendedFormat
                    ? (message.Id | MidExtendedBit)
                    : ((message.StandardIdPart << MidStandardIdShift) & MidStandardIdMask);
                mb.ReceivedId = receivedMid;
                mb.MessageId = receivedMid;
                mb.RemoteTransmitRequest = message.RemoteFrame;
                mb.MessageReady = true;

                statusRegister |= (1u << i);
                this.Log(LogLevel.Debug, "Stored in mailbox {0}", i);
                UpdateInterrupts();
                return;
            }

            this.Log(LogLevel.Debug, "No matching RX mailbox found, frame dropped");
        }

        public event Action<CANMessageFrame> FrameSent;

        public GPIO IRQ { get; }

        public long Size => 0x300;

        private bool MatchesFilter(Mailbox mb, CANMessageFrame message)
        {
            uint receivedId;
            if(message.ExtendedFormat)
            {
                receivedId = message.Id | MidExtendedBit;
            }
            else
            {
                receivedId = (message.StandardIdPart << MidStandardIdShift) & MidStandardIdMask;
            }

            // The acceptance mask determines which bits of MID are compared
            // MIDE bit in MAM: if set, also compare the IDE bit
            var mask = mb.AcceptanceMask;
            var filterId = mb.MessageId;

            return (receivedId & mask) == (filterId & mask);
        }

        private void TransmitMailbox(int index)
        {
            var mb = mailboxes[index];
            if(mb.ObjectType != MailboxObjectType.TX)
            {
                this.Log(LogLevel.Warning, "Transfer command on non-TX mailbox {0} (type={1})", index, mb.ObjectType);
                return;
            }

            var data = UnpackData(mb.DataLow, mb.DataHigh, mb.DataLengthCode);
            var extended = (mb.MessageId & MidExtendedBit) != 0;
            uint id;
            if(extended)
            {
                id = mb.MessageId & MidFullIdMask;
            }
            else
            {
                id = (mb.MessageId >> MidStandardIdShift) & 0x7FF;
            }

            var frame = new CANMessageFrame(id, data, extended, mb.RemoteTransmitRequest);
            this.Log(LogLevel.Debug, "TX mailbox {0}: ID=0x{1:X}, len={2}, ext={3}", index, id, data.Length, extended);

            mb.MessageAbort = false;
            statusRegister |= (1u << index);

            FrameSent?.Invoke(frame);

            // TX mailbox is ready for the next message after transmission
            mb.MessageReady = true;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var pending = statusRegister & interruptMask;
            var irq = pending != 0;
            this.Log(LogLevel.Debug, "IRQ={0} (SR=0x{1:X8}, IMR=0x{2:X8})", irq, statusRegister, interruptMask);
            IRQ.Set(irq);
        }

        private static uint PackDataLow(byte[] data)
        {
            uint val = 0;
            for(int i = 0; i < 4 && i < data.Length; i++)
            {
                val |= (uint)data[i] << (i * 8);
            }
            return val;
        }

        private static uint PackDataHigh(byte[] data)
        {
            uint val = 0;
            for(int i = 0; i < 4 && i + 4 < data.Length; i++)
            {
                val |= (uint)data[i + 4] << (i * 8);
            }
            return val;
        }

        private static byte[] UnpackData(uint low, uint high, uint dlc)
        {
            var len = (int)Math.Min(dlc, 8u);
            var data = new byte[len];
            for(int i = 0; i < len && i < 4; i++)
            {
                data[i] = (byte)(low >> (i * 8));
            }
            for(int i = 0; i < len - 4 && i < 4; i++)
            {
                data[i + 4] = (byte)(high >> (i * 8));
            }
            return data;
        }

        private void DefineRegisters()
        {
            Registers.Mode.Define(this)
                .WithFlag(0, name: "CANEN",
                    writeCallback: (_, value) =>
                    {
                        controllerEnabled = value;
                        if(value)
                        {
                            // Controller enabled: set WAKEUP and ERRA in status
                            statusRegister |= StatusBitWakeup | StatusBitErrorActive;
                            statusRegister &= ~StatusBitSleep;
                        }
                        else
                        {
                            statusRegister &= ~StatusBitWakeup;
                        }
                    },
                    valueProviderCallback: _ => controllerEnabled)
                .WithTaggedFlag("LPM", 1)
                .WithTaggedFlag("ABM", 2)
                .WithTaggedFlag("OVL", 3)
                .WithTaggedFlag("TEOF", 4)
                .WithTaggedFlag("TTM", 5)
                .WithTaggedFlag("TIMFRZ", 6)
                .WithTaggedFlag("DRPT", 7)
                .WithReservedBits(8, 24)
            ;

            Registers.InterruptEnable.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "IER",
                    writeCallback: (_, value) =>
                    {
                        interruptMask |= (uint)value;
                        UpdateInterrupts();
                    })
            ;

            Registers.InterruptDisable.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "IDR",
                    writeCallback: (_, value) =>
                    {
                        interruptMask &= ~(uint)value;
                        UpdateInterrupts();
                    })
            ;

            Registers.InterruptMask.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "IMR",
                    valueProviderCallback: _ => interruptMask)
            ;

            Registers.Status.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "SR",
                    valueProviderCallback: _ =>
                    {
                        var val = statusRegister;
                        // Mailbox bits (0-7) are cleared on read
                        statusRegister &= ~0xFFu;
                        UpdateInterrupts();
                        return val;
                    })
            ;

            Registers.Baudrate.Define(this)
                .WithTag("PHASE2", 0, 3)
                .WithReservedBits(3, 1)
                .WithTag("PHASE1", 4, 3)
                .WithReservedBits(7, 1)
                .WithTag("PROPAG", 8, 3)
                .WithReservedBits(11, 1)
                .WithTag("SJW", 12, 2)
                .WithReservedBits(14, 2)
                .WithTag("BRP", 16, 7)
                .WithReservedBits(23, 1)
                .WithTaggedFlag("SMP", 24)
                .WithReservedBits(25, 7)
            ;

            Registers.Timer.Define(this)
                .WithTag("TIMER", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.Timestamp.Define(this)
                .WithTag("MTIMESTAMP", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.ErrorCounter.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "REC", valueProviderCallback: _ => 0)
                .WithReservedBits(8, 8)
                .WithValueField(16, 9, FieldMode.Read, name: "TEC", valueProviderCallback: _ => 0)
                .WithReservedBits(25, 7)
            ;

            Registers.TransferCommand.Define(this)
                .WithFlags(0, 8, FieldMode.Write, name: "MB_TCR",
                    writeCallback: (index, _, value) =>
                    {
                        if(value)
                        {
                            TransmitMailbox(index);
                        }
                    })
                .WithReservedBits(8, 23)
                .WithFlag(31, FieldMode.Write, name: "TIMRST")
            ;

            Registers.AbortCommand.Define(this)
                .WithFlags(0, 8, FieldMode.Write, name: "MB_ACR",
                    writeCallback: (index, _, value) =>
                    {
                        if(value)
                        {
                            mailboxes[index].MessageAbort = true;
                        }
                    })
                .WithReservedBits(8, 24)
            ;

            Registers.WriteProtectMode.Define(this)
                .WithTaggedFlag("WPEN", 0)
                .WithReservedBits(1, 7)
                .WithTag("WPKEY", 8, 24)
            ;

            Registers.WriteProtectStatus.Define(this)
                .WithTaggedFlag("WPVS", 0)
                .WithReservedBits(1, 7)
                .WithTag("WPVSRC", 8, 16)
                .WithReservedBits(24, 8)
            ;

            // Define 8 mailboxes starting at offset 0x200, each 0x20 bytes
            for(int i = 0; i < NumberOfMailboxes; i++)
            {
                DefineMailboxRegisters(i);
            }
        }

        private void DefineMailboxRegisters(int index)
        {
            var mb = mailboxes[index];
            var baseOffset = (uint)(0x200 + index * 0x20);

            ((Registers)baseOffset).Define(this) // MMR
                .WithValueField(0, 16, name: $"MB{index}_MTIMEMARK")
                .WithValueField(16, 4, name: $"MB{index}_PRIOR",
                    writeCallback: (_, value) => mb.Priority = (uint)value,
                    valueProviderCallback: _ => mb.Priority)
                .WithReservedBits(20, 4)
                .WithValueField(24, 3, name: $"MB{index}_MOT",
                    writeCallback: (_, value) =>
                    {
                        mb.ObjectType = (MailboxObjectType)value;
                        // TX mailboxes start with MRDY=true (ready to accept data)
                        if(mb.ObjectType == MailboxObjectType.TX)
                        {
                            mb.MessageReady = true;
                        }
                        this.Log(LogLevel.Debug, "Mailbox {0} type set to {1}", index, mb.ObjectType);
                    },
                    valueProviderCallback: _ => (ulong)mb.ObjectType)
                .WithReservedBits(27, 5)
            ;

            ((Registers)(baseOffset + 0x04)).Define(this) // MAM
                .WithValueField(0, 30, name: $"MB{index}_MAM",
                    writeCallback: (_, value) => mb.AcceptanceMask = (uint)value,
                    valueProviderCallback: _ => mb.AcceptanceMask)
                .WithReservedBits(30, 2)
            ;

            ((Registers)(baseOffset + 0x08)).Define(this) // MID
                .WithValueField(0, 30, name: $"MB{index}_MID",
                    writeCallback: (_, value) => mb.MessageId = (uint)value,
                    valueProviderCallback: _ => mb.MessageId)
                .WithReservedBits(30, 2)
            ;

            ((Registers)(baseOffset + 0x0C)).Define(this) // MFID
                .WithValueField(0, 29, FieldMode.Read, name: $"MB{index}_MFID",
                    valueProviderCallback: _ => mb.ReceivedId & mb.AcceptanceMask & MidFullIdMask)
                .WithReservedBits(29, 3)
            ;

            ((Registers)(baseOffset + 0x10)).Define(this) // MSR
                .WithValueField(0, 16, FieldMode.Read, name: $"MB{index}_MTIMESTAMP",
                    valueProviderCallback: _ => 0)
                .WithValueField(16, 4, FieldMode.Read, name: $"MB{index}_MDLC",
                    valueProviderCallback: _ => mb.DataLengthCode)
                .WithFlag(20, FieldMode.Read, name: $"MB{index}_MRTR",
                    valueProviderCallback: _ => mb.RemoteTransmitRequest)
                .WithReservedBits(21, 1)
                .WithFlag(22, FieldMode.Read, name: $"MB{index}_MABT",
                    valueProviderCallback: _ => mb.MessageAbort)
                .WithFlag(23, FieldMode.Read, name: $"MB{index}_MRDY",
                    valueProviderCallback: _ => mb.MessageReady)
                .WithFlag(24, FieldMode.Read, name: $"MB{index}_MMI",
                    valueProviderCallback: _ => false)
                .WithReservedBits(25, 7)
            ;

            ((Registers)(baseOffset + 0x14)).Define(this) // MDL
                .WithValueField(0, 32, name: $"MB{index}_MDL",
                    writeCallback: (_, value) => mb.DataLow = (uint)value,
                    valueProviderCallback: _ => mb.DataLow)
            ;

            ((Registers)(baseOffset + 0x18)).Define(this) // MDH
                .WithValueField(0, 32, name: $"MB{index}_MDH",
                    writeCallback: (_, value) => mb.DataHigh = (uint)value,
                    valueProviderCallback: _ => mb.DataHigh)
            ;

            ((Registers)(baseOffset + 0x1C)).Define(this) // MCR
                .WithReservedBits(0, 16)
                .WithValueField(16, 4, FieldMode.Write, name: $"MB{index}_MDLC",
                    writeCallback: (_, value) => mb.DataLengthCode = (uint)value)
                .WithFlag(20, FieldMode.Write, name: $"MB{index}_MRTR",
                    writeCallback: (_, value) => mb.RemoteTransmitRequest = value)
                .WithReservedBits(21, 1)
                .WithFlag(22, FieldMode.Write, name: $"MB{index}_MACR",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            mb.MessageAbort = true;
                        }
                    })
                .WithFlag(23, FieldMode.Write, name: $"MB{index}_MTCR",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            // MTCR clears the mailbox event bit in SR
                            statusRegister &= ~(1u << index);
                            UpdateInterrupts();

                            if(mb.ObjectType == MailboxObjectType.TX)
                            {
                                TransmitMailbox(index);
                            }
                            else if(mb.ObjectType == MailboxObjectType.RX || mb.ObjectType == MailboxObjectType.RXOverwrite)
                            {
                                // Re-enable the mailbox for reception
                                mb.MessageReady = false;
                            }
                        }
                    })
                .WithReservedBits(24, 8)
            ;
        }

        private bool controllerEnabled;
        private uint interruptMask;
        private uint statusRegister;
        private readonly Mailbox[] mailboxes = new Mailbox[NumberOfMailboxes];

        private const int NumberOfMailboxes = 8;
        private const uint MidExtendedBit = 1u << 29;
        private const uint MidStandardIdMask = 0x1FFC0000u; // bits 28:18
        private const int MidStandardIdShift = 18;
        private const uint MidFullIdMask = 0x1FFFFFFFu; // bits 28:0
        private const uint StatusBitErrorActive = 1u << 16; // ERRA bit
        private const uint StatusBitWakeup = 1u << 21; // WAKEUP bit
        private const uint StatusBitSleep = 1u << 20; // SLEEP bit

        private class Mailbox
        {
            public Mailbox(int index)
            {
                Index = index;
                Reset();
            }

            public void Reset()
            {
                ObjectType = MailboxObjectType.Disabled;
                AcceptanceMask = 0;
                MessageId = 0;
                ReceivedId = 0;
                DataLow = 0;
                DataHigh = 0;
                DataLengthCode = 0;
                Priority = 0;
                MessageReady = false;
                MessageAbort = false;
                RemoteTransmitRequest = false;
            }

            public int Index { get; }
            public MailboxObjectType ObjectType { get; set; }
            public uint AcceptanceMask { get; set; }
            public uint MessageId { get; set; }
            public uint ReceivedId { get; set; }
            public uint DataLow { get; set; }
            public uint DataHigh { get; set; }
            public uint DataLengthCode { get; set; }
            public uint Priority { get; set; }
            public bool MessageReady { get; set; }
            public bool MessageAbort { get; set; }
            public bool RemoteTransmitRequest { get; set; }
        }

        private enum MailboxObjectType
        {
            Disabled = 0,
            RX = 1,
            RXOverwrite = 2,
            TX = 3,
            Consumer = 4,
            Producer = 5,
        }

        private enum Registers : uint
        {
            Mode = 0x00,
            InterruptEnable = 0x04,
            InterruptDisable = 0x08,
            InterruptMask = 0x0C,
            Status = 0x10,
            Baudrate = 0x14,
            Timer = 0x18,
            Timestamp = 0x1C,
            ErrorCounter = 0x20,
            TransferCommand = 0x24,
            AbortCommand = 0x28,
            WriteProtectMode = 0xE4,
            WriteProtectStatus = 0xE8,
            // Mailbox registers are at 0x200 + n*0x20
            // Defined dynamically in DefineMailboxRegisters
        }
    }
}
