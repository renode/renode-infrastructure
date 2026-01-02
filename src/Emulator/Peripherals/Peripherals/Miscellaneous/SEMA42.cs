//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class SEMA42 : IBytePeripheral, IWordPeripheral, IKnownSize
    {
        public SEMA42(IMachine machine, NXP_XRDC xrdc)
        {
            if(xrdc == null)
            {
                throw new ConstructionException($"'{nameof(xrdc)}' cannot be null");
            }
            sysbus = machine.GetSystemBus(this);
            this.xrdc = xrdc;
            byteRegisters = BuildByteRegisters();
            wordRegisters = BuildWordRegisters();
            Reset();
        }

        public void Reset()
        {
            resetStep = 0;
            byteRegisters.Reset();
            wordRegisters.Reset();
        }

        public byte ReadByte(long offset)
        {
            return byteRegisters.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            byteRegisters.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            return wordRegisters.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            wordRegisters.Write(offset, value);
        }

        public long Size => 0x44;

        private ByteRegisterCollection BuildByteRegisters()
        {
            var registers = new ByteRegisterCollection(this);

            Registers.Gate.DefineMany(registers, NrOfGates, (register, offset) =>
            {
                // The offset for the gate n is calculated using the formula:
                // offset = 0 + (n + 3 - 2 * (n % 4))
                // So the following formula reverts it.
                var gateIdx = (offset / 4) * 4 - (offset % 4) + 3;
                register
                    .WithValueField(0, 4, out gateState[gateIdx],
                        writeCallback: (oldVal, newVal) =>
                        {
                            if(sysbus.TryGetTransactionInitiator(out var initiator) && xrdc.TryGetDomainId(initiator, out var domain))
                            {
                                var isLocked = oldVal != GateUnlockedState;

                                if(isLocked)
                                {
                                    // Try unlock
                                    if(oldVal - 1 == domain && newVal == GateUnlockedState)
                                    {
                                        this.NoisyLog("Semaphore #{0} unlocked by: {1}", gateIdx, domain);
                                        xrdc.SetSemaphore((uint)gateIdx, null);
                                        return;
                                    }
                                }
                                else
                                {
                                    // Try lock
                                    if(newVal - 1 == domain)
                                    {
                                        this.NoisyLog("Semaphore #{0} locked for: {1}", gateIdx, domain);
                                        xrdc.SetSemaphore((uint)gateIdx, domain);
                                        return;
                                    }
                                }
                            }

                            // Otherwise reset the gate to the previous value
                            gateState[gateIdx].Value = oldVal;
                        },
                        name: "GTFSM")
                    .WithReservedBits(4, 4);
            });

            return registers;
        }

        private WordRegisterCollection BuildWordRegisters()
        {
            var registers = new WordRegisterCollection(this);

            Registers.ResetGate.Define(registers)
                // Define Read register with fields and handle Write with ProgressReset in write callback
                .WithValueField(0, 8, out resetGateNumber, name: "RSTGTN")
                .WithValueField(8, 4, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        if(xrdc.TryGetCurrentDomainId(out var did))
                        {
                            return did;
                        }

                        this.WarningLog("Unable to provide DID, returning 0x0");
                        return 0x0;
                    },
                    name: "RSTGMS")
                .WithValueField(12, 2, FieldMode.Read,
                    valueProviderCallback: _ => resetStep,
                    name: "RSTGSM")
                .WithValueField(14, 2, FieldMode.Read, name: "Reserved")
                .WithWriteCallback((_, val) => ProgressReset(val));

            return registers;
        }

        private void ProgressReset(ushort writtenValue)
        {
            var resetKey = BitHelper.GetValue(writtenValue, offset: 8, size: 8);
            if(resetKey != resetKeys[resetStep])
            {
                resetStep = 0;
                return;
            }
            resetStep += 1;

            if(resetStep == resetKeys.Length)
            {
                PerformGateReset(resetGateNumber.Value);
                resetStep = 0;
            }
        }

        private void PerformGateReset(ulong target)
        {
            if(target >= ResetAllThreshold)
            {
                for(var i = 0; i < NrOfGates; ++i)
                {
                    gateState[i].Value = GateUnlockedState;
                    xrdc?.SetSemaphore((uint)i, null);
                }
                this.NoisyLog("All semphores unlocked");
            }
            else if(target < NrOfGates)
            {
                gateState[target].Value = GateUnlockedState;
                xrdc?.SetSemaphore((uint)target, null);
                this.NoisyLog("Semphore #{0} unlocked", target);
            }
            else
            {
                this.WarningLog(
                    "Trying to reset gate {0}, but only {1} are available. Ignoring action.",
                    target,
                    NrOfGates
                );
            }
        }

        private uint resetStep = 0;
        private IValueRegisterField resetGateNumber;

        private readonly IBusController sysbus;
        private readonly NXP_XRDC xrdc;
        private readonly ByteRegisterCollection byteRegisters;
        private readonly WordRegisterCollection wordRegisters;

        private readonly IValueRegisterField[] gateState = new IValueRegisterField[NrOfGates];

        private readonly ushort[] resetKeys = { 0xE2, 0x1D };

        private const uint NrOfGates = 16;
        private const uint GateUnlockedState = 0;
        private const uint ResetAllThreshold = 64;

        private enum Registers
        {
            Gate = 0x0,
            ResetGate = 0x42,
        }
    }
}
