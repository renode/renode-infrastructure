//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class CoralNPU : IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral,
        IBytePeripheral, IWordPeripheral, IQuadWordPeripheral, IMultibyteWritePeripheral, IKnownSize, ICanLoadFiles
    {
        public CoralNPU(IMachine machine, string coreName = nameof(CoralNPU_RVV))
        {
            this.machine = machine;
            RegistersCollection = new DoubleWordRegisterCollection(this);

            memoryItcm = new MappedMemory(machine, InstructionMemorySize);
            memoryDtcm = new MappedMemory(machine, DataMemorySize);

            Core = new CoralNPU_RVV(machine, this, memoryItcm, memoryDtcm);

            DefineRegisters();
            Reset();

            machine.GetSystemBus(this).Register(Core, new CPURegistrationPoint());
            machine.SetLocalName(Core, coreName);
            machine.GetSystemBus(this).Register(memoryItcm, new BusRangeRegistration(new Range(InstructionMemoryStart, InstructionMemorySize), 0, Core));
            machine.SetLocalName(memoryItcm, coreName + "_ITCM");
            machine.GetSystemBus(this).Register(memoryDtcm, new BusRangeRegistration(new Range(DataMemoryStart, DataMemorySize), 0, Core));
            machine.SetLocalName(memoryDtcm, coreName + "_DTCM");
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            memoryDtcm.ZeroAll();
            memoryItcm.ZeroAll();
            Core.Reset();
            UpdateInterrupts();
        }

        public void LoadFileChunks(string path, IEnumerable<FileChunk> chunks, IPeripheral cpu)
        {
            this.LoadFileChunks(chunks, cpu);
        }

        public void WriteByte(long offset, byte value)
        {
            if(offset is >= InstructionMemoryStart and <= InstructionMemoryEnd)
            {
                memoryItcm.WriteByte(offset - InstructionMemoryStart, value);
            }
            else if(offset is >= DataMemoryStart and <= DataMemoryEnd)
            {
                memoryDtcm.WriteByte(offset - DataMemoryStart, value);
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled write access to offset 0x{0:X}, with value 0x{1:x}", offset, value);
            }
        }

        public byte ReadByte(long offset)
        {
            if(offset is >= InstructionMemoryStart and <= InstructionMemoryEnd)
            {
                return memoryItcm.ReadByte(offset - InstructionMemoryStart);
            }
            else if(offset is >= DataMemoryStart and <= DataMemoryEnd)
            {
                return memoryDtcm.ReadByte(offset - DataMemoryStart);
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled read access to offset 0x{0:X}, returning 0", offset);
                return 0x0;
            }
        }

        public void WriteWord(long offset, ushort value)
        {
            if(offset is >= InstructionMemoryStart and <= InstructionMemoryEnd)
            {
                memoryItcm.WriteWord(offset - InstructionMemoryStart, value);
            }
            else if(offset is >= DataMemoryStart and <= DataMemoryEnd)
            {
                memoryDtcm.WriteWord(offset - DataMemoryStart, value);
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled write access to offset 0x{0:X}, with value 0x{1:x}", offset, value);
            }
        }

        public ushort ReadWord(long offset)
        {
            if(offset is >= InstructionMemoryStart and <= InstructionMemoryEnd)
            {
                return memoryItcm.ReadWord(offset - InstructionMemoryStart);
            }
            else if(offset is >= DataMemoryStart and <= DataMemoryEnd)
            {
                return memoryDtcm.ReadWord(offset - DataMemoryStart);
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled read access to offset 0x{0:X}, returning 0", offset);
                return 0x0;
            }
        }

        // Registers are only accessible through DoubleWord (32-bit) wide access
        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset is >= InstructionMemoryStart and <= InstructionMemoryEnd)
            {
                memoryItcm.WriteDoubleWord(offset - InstructionMemoryStart, value);
            }
            else if(offset is >= DataMemoryStart and <= DataMemoryEnd)
            {
                memoryDtcm.WriteDoubleWord(offset - DataMemoryStart, value);
            }
            else if(offset is >= RegistersMemoryStart and <= RegistersMemoryEnd)
            {
                RegistersCollection.Write(offset - RegistersMemoryStart, value);
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled write access to offset 0x{0:X}, with value 0x{1:x}", offset, value);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset is >= InstructionMemoryStart and <= InstructionMemoryEnd)
            {
                return memoryItcm.ReadDoubleWord(offset - InstructionMemoryStart);
            }
            else if(offset is >= DataMemoryStart and <= DataMemoryEnd)
            {
                return memoryDtcm.ReadDoubleWord(offset - DataMemoryStart);
            }
            else if(offset is >= RegistersMemoryStart and <= RegistersMemoryEnd)
            {
                return RegistersCollection.Read(offset - RegistersMemoryStart);
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled read access to offset 0x{0:X}, returning 0", offset);
                return 0x0;
            }
        }

        public void WriteQuadWord(long offset, ulong value)
        {
            if(offset is >= InstructionMemoryStart and <= InstructionMemoryEnd)
            {
                memoryItcm.WriteQuadWord(offset - InstructionMemoryStart, value);
            }
            else if(offset is >= DataMemoryStart and <= DataMemoryEnd)
            {
                memoryDtcm.WriteQuadWord(offset - DataMemoryStart, value);
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled write access to offset 0x{0:X}, with value 0x{1:x}", offset, value);
            }
        }

        public ulong ReadQuadWord(long offset)
        {
            if(offset is >= InstructionMemoryStart and <= InstructionMemoryEnd)
            {
                return memoryItcm.ReadQuadWord(offset - InstructionMemoryStart);
            }
            else if(offset is >= DataMemoryStart and <= DataMemoryEnd)
            {
                return memoryDtcm.ReadQuadWord(offset - DataMemoryStart);
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled read access to offset 0x{0:X}, returning 0", offset);
                return 0x0;
            }
        }

        public void WriteBytes(long offset, byte[] array, int startingIndex, int count, IPeripheral context = null)
        {
            if(context != null)
            {
                throw new RecoverableException($"Context is not supported for this peripheral: {this.GetName()}");
            }

            long upperBound = count * sizeof(byte) + offset;

            // ITCM and DTCM are not contiguous; binaries may still span both regions.
            if(offset is >= InstructionMemoryStart and <= InstructionMemoryEnd)
            {
                long instructionOffset = offset - InstructionMemoryStart;
                int lowCount = count;
                if(upperBound > InstructionMemoryEnd)
                {
                    lowCount = (int)(InstructionMemorySize - instructionOffset);
                }
                memoryItcm.WriteBytes(instructionOffset, array, startingIndex, lowCount, context);
                if(lowCount != count)
                {
                    // The source bytes that map into the ITCM/DTCM hole are discarded
                    int remaining = count - lowCount;
                    int holeBytes = (int)(DataMemoryStart - InstructionMemorySize);
                    int dtcmCount = remaining - (holeBytes < remaining ? holeBytes : remaining);
                    this.Log(LogLevel.Debug, "Discarding up to {0} data bytes that fall in the ITCM/DTCM hole", holeBytes);
                    if(dtcmCount > 0)
                    {
                        memoryDtcm.WriteBytes(0, array, startingIndex + lowCount + holeBytes, dtcmCount, context);
                    }
                }
            }
            else if(offset is >= DataMemoryStart and <= DataMemoryEnd)
            {
                memoryDtcm.WriteBytes(offset - DataMemoryStart, array, startingIndex, count, context);
            }
            else if(offset is >= RegistersMemoryStart and <= RegistersMemoryEnd)
            {
                this.Log(LogLevel.Warning, "Use 32 bit regular accesses (WriteDoubleWord) to access CSR space. This operation is ignored!");
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled write access to offset 0x{0:X}", offset);
            }
        }

        public byte[] ReadBytes(long offset, int count, IPeripheral context = null)
        {
            if(context != null)
            {
                throw new RecoverableException($"Context is not supported for this peripheral: {this.GetName()}");
            }

            long upperBound = count * sizeof(byte) + offset;

            var rets = new List<byte>();

            // ITCM and DTCM are not contiguous; binaries may still span both regions.
            if(offset is >= InstructionMemoryStart and <= InstructionMemoryEnd)
            {
                long instructionOffset = offset - InstructionMemoryStart;
                int lowCount = count;
                if(upperBound > InstructionMemoryEnd)
                {
                    lowCount = (int)(InstructionMemorySize - instructionOffset);
                }
                rets.AddRange(memoryItcm.ReadBytes(instructionOffset, lowCount, context));
                if(lowCount != count)
                {
                    // The hole reads back as zeros, then the DTCM contents continue
                    int remaining = count - lowCount;
                    int holeBytes = (int)(DataMemoryStart - InstructionMemorySize);
                    int zeros = holeBytes < remaining ? holeBytes : remaining;
                    this.Log(LogLevel.Debug, "Returning {0} zero bytes for the ITCM/DTCM hole", zeros);
                    rets.AddRange(Enumerable.Repeat((byte)0, zeros));
                    int dtcmCount = remaining - zeros;
                    if(dtcmCount > 0)
                    {
                        rets.AddRange(memoryDtcm.ReadBytes(0, dtcmCount, context));
                    }
                }
            }
            else if(offset is >= DataMemoryStart and <= DataMemoryEnd)
            {
                rets.AddRange(memoryDtcm.ReadBytes(offset - DataMemoryStart, count, context));
            }
            else if(offset is >= RegistersMemoryStart and <= RegistersMemoryEnd)
            {
                this.Log(LogLevel.Warning, "Use 32 bit regular accesses (ReadDoubleWord) to access CSR space. This operation is ignored!");
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled read access to offset 0x{0:X}", offset);
            }
            return rets.ToArray();
        }

        public long Size => RegistersMemoryEnd;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public CoralNPU_RVV Core { get; }

        public GPIO HaltedIRQ { get; } = new GPIO();

        public GPIO FaultedIRQ { get; } = new GPIO();

        // There is no clock supplied to the peripheral (so for Renode it means - Halted state)
        public bool CoreHalted => Core.IsHalted || Core.IsInWfi || clockGated.Value;

        public bool CoreFaulted => Core.Faulted;

        private void LoadPcStart()
        {
            Core.PC = pcStart.Value;
        }

        private void UpdateInterrupts()
        {
            HaltedIRQ.Set(CoreHalted);
            FaultedIRQ.Set(CoreFaulted);
        }

        private void DefineRegisters()
        {
            // There is a bit of confusion between internal Renode and Coral terminology
            // in regards to "Reset", "Halted" etc. Read carefully
            Registers.ResetControl.Define(this, 0b11)
                .WithFlag(0, out peripheralReset,
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            Core.IsHalted = true;
                            // Reset only the core, not the peripheral registers
                            Core.Reset();
                            // Immediately resume the CPU, so it doesn't halt the emulation
                            Core.Resume();
                        }
                        else
                        {
                            LoadPcStart();
                            if(!clockGated.Value)
                            {
                                this.Log(LogLevel.Info, "Starting CPU, with PC: 0x{0:X}", Core.PC.RawValue);
                                Core.IsHalted = false;
                            }
                        }
                        UpdateInterrupts();
                    },
                    name: "RESET")
                .WithFlag(1, out clockGated,
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            Core.IsHalted = true;
                            UpdateInterrupts();
                        }
                        else
                        {
                            if(!peripheralReset.Value)
                            {
                                Core.IsHalted = false;
                                UpdateInterrupts();
                            }
                        }
                    },
                    name: "CLOCK_GATE")
                .WithReservedBits(2, 30);

            Registers.PcStart.Define(this, 0x0)
                .WithValueField(0, 32, out pcStart, name: "START_ADDRESS");

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => CoreHalted, name: "HALTED")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => CoreFaulted, name: "FAULT")
                .WithReservedBits(2, 30);
        }

        private IFlagRegisterField clockGated;
        private IFlagRegisterField peripheralReset;
        private IValueRegisterField pcStart;

        private readonly MappedMemory memoryItcm, memoryDtcm;

        private readonly IMachine machine;

        private const long InstructionMemoryStart = 0x0;
        private const long InstructionMemorySize = 0x2000;
        private const long InstructionMemoryEnd = InstructionMemoryStart + InstructionMemorySize - 1;

        private const long DataMemoryStart = 0x10000;
        private const long DataMemorySize = 0x8000;
        private const long DataMemoryEnd = DataMemoryStart + DataMemorySize - 1;

        private const long RegistersMemoryStart = 0x30000;
        private const long RegistersMemoryEnd = 0x30100;

        public class CoralNPU_RVV : RiscV32
        {
            public CoralNPU_RVV(IMachine machine, CoralNPU parent, MappedMemory memoryItcm, MappedMemory memoryDtcm, uint hartId = 0)
                : base(machine, "rv32imfv_zve32x_zicsr_zifencei_zbb", hartId: hartId, privilegeLevels: PrivilegeLevels.Machine, pmpEntryCount: 0)
            {
                this.parent = parent;

                this.AddHookAtWfiStateChange(HandleWfi);
                this.AddHookAtInterruptBegin(exceptionIndex =>
                    {
                        if(ShouldTriggerFault(exceptionIndex))
                        {
                            this.DebugLog("Fault encountered: {0}", exceptionIndex);
                            // FAULT is sticky until core reset (RESET_CONTROL.RESET), matching Coral NPU RTL.
                            Faulted = true;
                            parent.UpdateInterrupts();
                        }
                    }
                );

                this.InstallCustomInstruction(MpauseOpcodePattern,
                    opc =>
                    {
                        this.InfoLog("MPAUSE executed");
                        IsHalted = true;
                        parent.UpdateInterrupts();
                    },
                    "MPAUSE");

                this.memoryItcm = memoryItcm;
                this.memoryDtcm = memoryDtcm;

                foreach(var segment in memoryItcm.MappedSegments)
                {
                    this.MapMemory(segment);
                }

                this.EnableExternalWindowMmu(true);
                var insnFetchWindow = (uint)this.AcquireExternalMmuWindow(ExternalMmuBase.Privilege.Execute);
                this.SetMmuWindowStart(insnFetchWindow, InstructionMemoryStart);
                this.SetMmuWindowEnd(insnFetchWindow, InstructionMemoryEnd + 1);
                this.SetMmuWindowPrivileges(insnFetchWindow, ExternalMmuBase.Privilege.Execute);

                foreach(var segment in memoryDtcm.MappedSegments)
                {
                    var wrappedSegment = new SystemBus.MappedSegmentWrapper(segment, DataMemoryStart, segment.Size, this);
                    this.MapMemory(wrappedSegment);
                }

                var dataWindow = (uint)this.AcquireExternalMmuWindow(ExternalMmuBase.Privilege.ReadAndWrite);
                this.SetMmuWindowStart(dataWindow, DataMemoryStart);
                this.SetMmuWindowEnd(dataWindow, DataMemoryEnd + 1);
                this.SetMmuWindowPrivileges(dataWindow, ExternalMmuBase.Privilege.ReadAndWrite);

                AddHookOnMmuFault((faultAddress, accessType, faultyWindowId, firstTry) =>
                {
                    this.DebugLog("Fault encountered: External MMU fault @ 0x{0:x}, access type {1}", faultAddress, accessType);
                    // FAULT is sticky until core reset (RESET_CONTROL.RESET), matching Coral NPU RTL.
                    Faulted = true;
                    parent.UpdateInterrupts();
                    return ExternalMmuResult.Fault;
                });

                // The core starts halted, with clock gated behind the CLK_GATE
                IsHalted = true;
            }

            public override void Reset()
            {
                base.Reset();
                parent.LoadPcStart();
                Faulted = false;
                parent.UpdateInterrupts();
            }

            public override void InitFromElf(ELFSharp.ELF.IELF elf)
            {
                this.Log(LogLevel.Debug, "Ignoring sysbus-loaded ELFs; this core uses its own ITCM/DTCM.");
            }

            public override void InitFromUImage(ELFSharp.UImage.UImage uImage)
            {
                this.Log(LogLevel.Debug, "Ignoring sysbus-loaded UImages; this core uses its own ITCM/DTCM.");
            }

            public bool IsInWfi { get; private set; }

            public bool Faulted { get; private set; }

            private void HandleWfi(bool isInWfi)
            {
                this.IsInWfi = isInWfi;
                parent.UpdateInterrupts();
            }

            private bool ShouldTriggerFault(ulong exceptionIndex)
            {
                // This list is taken from RISC-V Translation Libs
                switch(exceptionIndex)
                {
                case 0x0: // RISCV_EXCP_INST_ADDR_MIS
                case 0x1: // RISCV_EXCP_INST_ACCESS_FAULT
                case 0xc: // RISCV_EXCP_INST_PAGE_FAULT          /* since: priv-1.10.0 */
                case 0x2: // RISCV_EXCP_ILLEGAL_INST
                case 0x4: // RISCV_EXCP_LOAD_ADDR_MIS
                case 0x5: // RISCV_EXCP_LOAD_ACCESS_FAULT
                case 0x6: // RISCV_EXCP_STORE_AMO_ADDR_MIS
                case 0x7: // RISCV_EXCP_STORE_AMO_ACCESS_FAULT
                case 0xd: // RISCV_EXCP_LOAD_PAGE_FAULT          /* since: priv-1.10.0 */
                case 0xf: // RISCV_EXCP_STORE_PAGE_FAULT         /* since: priv-1.10.0 */
                case 0x3: // RISCV_EXCP_BREAKPOINT
                    return true;
                default:
                    return false;
                }
            }

            private readonly MappedMemory memoryItcm, memoryDtcm;
            private readonly CoralNPU parent;

            private const string MpauseOpcodePattern = "00001000000000000000000001110011";
        }

        public enum Registers
        {
            ResetControl = 0x00,
            PcStart = 0x04,
            Status = 0x08,
        }
    }
}
