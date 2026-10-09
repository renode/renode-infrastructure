//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous;

public class Caliptra_Mailbox : BasicDoubleWordPeripheral
{
    public Caliptra_Mailbox(IMachine machine, IMemory backingMemory) : base(machine)
    {
        this.backingMemory = backingMemory;
        DefineRegisters();
    }

    public override void Reset()
    {
        base.Reset();
        readPointer = 0;
        writePointer = 0;
    }

    private void DefineRegisters()
    {
        Registers.Lock.Define(this)
            .WithFlag(0, out mailboxLocked, FieldMode.ReadToSet, name: "Locked",
                readCallback: (_, val) =>
                {
                    if(mailboxLocked.Value)
                    {
                        mailboxState.Value = MailboxState.MBOX_RDY_FOR_CMD;
                    }
                }
            )
            .WithReservedBits(1, 31);

        Registers.User.Define(this)
            .WithTag("USER", 0, 32);

        Registers.Command.Define(this)
            .WithValueField(0, 32, name: "Mailbox Command",
                writeCallback: (_, val) =>
                {
                    mailboxState.Value = MailboxState.MBOX_RDY_FOR_DLEN;
                }
            );

        Registers.DataLength.Define(this)
            .WithValueField(0, 32, out mailboxDataLength, name: "Data Length",
                writeCallback: (_, val) =>
                {
                    mailboxState.Value = MailboxState.MBOX_RDY_FOR_DATA;
                }
            );

        Registers.DataIn.Define(this)
            .WithValueField(0, 32, name: "Data In",
                writeCallback: (_, data) =>
                {
                    backingMemory.WriteDoubleWord(writePointer, (uint)data);
                    writePointer += 4;
                },
                valueProviderCallback: _ =>
                {
                    return backingMemory.ReadDoubleWord(writePointer);
                }
            );

        Registers.DataOut.Define(this)
            .WithValueField(0, 32, FieldMode.Read, name: "Data Out",
                valueProviderCallback: _ =>
                {
                    var ret = backingMemory.ReadDoubleWord(readPointer);
                    readPointer += 4;
                    return ret;
                }
            );

        Registers.Execute.Define(this)
            .WithFlag(0, name: "Execute",
                writeCallback: (_, val) =>
                {
                    if(!val)
                    {
                        // Side effect of clearing "Execute" is releasing the lock
                        mailboxLocked.Value = false;
                        mailboxState.Value = MailboxState.MBOX_IDLE;
                    }
                    else
                    {
                        mailboxState.Value = MailboxState.MBOX_EXECUTE_UC;
                    }
                    // No matter what action, always clear the pointers
                    readPointer = 0;
                    writePointer = 0;
                }
            )
            .WithReservedBits(1, 31);

        Registers.Status.Define(this)
            .WithEnumField(0, 4, out mailboxStatus, name: "Status",
                writeCallback: (_, val) =>
                {
                    if(val == MailboxStatus.DATA_READY)
                    {
                        mailboxState.Value = MailboxState.MBOX_EXECUTE_SOC;
                        // No matter what action, always clear the pointers
                        readPointer = 0;
                        writePointer = 0;
                    }
                }
            )
            .WithTaggedFlag("ECC Single Error", 4)
            .WithTaggedFlag("ECC Double Error", 5)
            .WithEnumField(6, 3, out mailboxState, FieldMode.Read, name: "Mailbox FSM State")
            .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => mailboxLocked.Value, name: "SOC Has lock")
            .WithValueField(10, 16, valueProviderCallback: _ => (ulong)readPointer, name: "Mailbox Read Pointer")
            .WithTaggedFlag("TAP has lock", 26)
            .WithReservedBits(27, 5);

        Registers.Unlock.Define(this)
            .WithFlag(0, name: "Force Unlock",
                writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        this.InfoLog("Forcing the unlock of Mailbox");
                        mailboxLocked.Value = false;
                        mailboxState.Value = MailboxState.MBOX_IDLE;
                    }
                }
            )
            .WithReservedBits(1, 31);

        Registers.TapMode.Define(this)
            .WithTaggedFlag("TAP mode enabled", 0)
            .WithReservedBits(1, 31);
    }

    private IFlagRegisterField mailboxLocked;
    private IValueRegisterField mailboxDataLength;
    private IEnumRegisterField<MailboxState> mailboxState;
    private IEnumRegisterField<MailboxStatus> mailboxStatus;

    private uint readPointer;
    private uint writePointer;

    private readonly IMemory backingMemory;

    public enum Registers
    {
        Lock = 0x0,
        User = 0x4,
        Command = 0x8,
        DataLength = 0xC,
        DataIn = 0x10,
        DataOut = 0x14,
        Execute = 0x18,
        Status = 0x1C,
        Unlock = 0x20,
        TapMode = 0x24,
    }

    private enum MailboxStatus
    {
        CMD_BUSY = 0x0,
        DATA_READY = 0x1,
        CMD_COMPLETE = 0x2,
        CMD_FAILURE = 0x3,
    }

    private enum MailboxState
    {
        MBOX_IDLE = 0x0,
        MBOX_RDY_FOR_CMD = 0x1,
        MBOX_RDY_FOR_DATA = 0x2,
        MBOX_RDY_FOR_DLEN = 0x3,
        MBOX_EXECUTE_SOC = 0x4,
        MBOX_EXECUTE_UC = 0x6,
        MBOX_ERROR = 0x7,
    }
}
