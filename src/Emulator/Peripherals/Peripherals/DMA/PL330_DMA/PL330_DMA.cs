//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.DMA
{
    public partial class PL330_DMA : BasicDoubleWordPeripheral, IKnownSize, IDMA, INumberedGPIOOutput, IGPIOReceiver
    {
        // This model doesn't take into account differences in AXI bus width, 
        // which could have impact on unaligned transfers in real HW
        // this is a know limitation at this moment
        public PL330_DMA(IMachine machine, uint numberOfSupportedEventsAndInterrupts = MaximumSupportedEventsOrInterrupts, uint numberOfSupportedPeripheralRequestInterfaces = MaximumSupportedPeripheralRequestInterfaces, byte revision = 0x3) : base(machine)
        {
            this.Revision = revision;
            if(numberOfSupportedEventsAndInterrupts > MaximumSupportedEventsOrInterrupts)
            {
                throw new ConstructionException($"No more than {MaximumSupportedEventsOrInterrupts} Events or Interrupts are supported by this peripheral");
            }
            this.NumberOfSupportedEventsAndInterrupts = (int)numberOfSupportedEventsAndInterrupts;
            if(numberOfSupportedPeripheralRequestInterfaces > MaximumSupportedPeripheralRequestInterfaces)
            {
                throw new ConstructionException($"No more than {MaximumSupportedPeripheralRequestInterfaces} Peripheral Request Interfaces are supported by this peripheral");
            }
            this.NumberOfSupportedPeripheralRequestInterfaces = (int)numberOfSupportedPeripheralRequestInterfaces;

            channels = new Channel[NumberOfChannels];
            for(int i = 0; i < channels.Length; ++i)
            {
                channels[i] = new Channel(this, i);
            }

            RegisterInstructions();
            DefineRegisters();
 
            eventActive = new bool[NumberOfSupportedEventsAndInterrupts];

            var gpios = new Dictionary<int, IGPIO>();
            for(var i = 0; i < NumberOfSupportedEventsAndInterrupts; ++i)
            {
                gpios.Add(i, new GPIO());
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(gpios);

            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            debugStatus = false;

            for(int i = 0; i < channels.Length; ++i)
            {
                channels[i].Reset();
            }

            foreach(var connection in Connections.Values)
            {
                connection.Unset();
            }
            for(int i = 0; i < NumberOfSupportedEventsAndInterrupts; ++i)
            {
                eventActive[i] = false;
            }
            AbortIRQ.Unset();
        }

        public void OnGPIO(int number, bool value)
        {
            if(!IsPeripheralInterfaceValid((uint)number))
            {
                return;
            }
            lock(executeLock)
            {
                if(!value)
                {
                    // To simulate requestLast, hold IRQ line high during the entire transmission sequence,
                    // and bring it down, when all data is sent
                    // If this feature is not used at all in DMA microcode (program) this doesn't matter
                    foreach(var channel in channels.Where(c => c?.Peripheral == number))
                    {
                        channel.RequestLast = true;
                    }
                    return;
                }

                bool anyChangedState = false;
                foreach(var channel in channels.Where(c => c.Status == Channel.ChannelStatus.WaitingForPeripheral).Where(c => c.WaitingEventOrPeripheralNumber == number))
                {
                    // A peripheral has signaled that it's ready for DMA operations to start
                    channel.Status = Channel.ChannelStatus.Executing;
                    anyChangedState = true;
                }
                if(anyChangedState)
                {
                    // Rerun ExecuteLoop if any channel woke up from sleep
                    ExecuteLoop();
                }
            }
        }

        public void RequestTransfer(int channel)
        {
            throw new RecoverableException("This DMA requires an in-memory program to transfer data");
        }

        // This method should be called from Monitor to decode instruction at given address.
        // It is intended as a helper to investigate in-memory program.
        // Uses QuadWord accesses, so the target must support them.
        public string TryDecodeInstructionAtAddress(ulong address, bool fullDecode = false)
        {
            ulong bytes = machine.GetSystemBus(this).ReadQuadWord(address);
            if(!decoderRoot.TryParseOpcode((byte)bytes, out var instruction))
            {
                throw new RecoverableException("Unrecognized instruction");
            }
            if(fullDecode)
            {
                instruction.ParseAll(bytes);
            }
            return instruction.ToString();
        }

        public long Size => 0x1000;
        public int NumberOfChannels => 8;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }
        public GPIO AbortIRQ { get; } = new GPIO();

        public byte Revision { get; }
        public int NumberOfSupportedEventsAndInterrupts { get; }
        public int NumberOfSupportedPeripheralRequestInterfaces { get; }

        // The values below are only used in Configuration and DmaConfiguration registers - they have no impact on the model's operation
        public int InstructionCacheLineLength { get; set; } = 16;
        public int InstructionCacheLinesNumber { get; set; } = 32;

        public ulong WriteIssuingCapability { get; set; } = 8;
        public ulong ReadIssuingCapability { get; set; } = 8;

        public ulong ReadQueueDepth { get; set; } = 16;
        public ulong WriteQueueDepth { get; set; } = 16;

        public ulong DataBufferDepth { get; set; } = 1024;
        public ulong AXIBusWidth { get; set; } = 32;

        private void DefineRegisters()
        {
            Registers.DmaInterruptEnable.Define(this)
                .WithFlags(0, NumberOfSupportedEventsAndInterrupts, out interruptEnabled,
                    writeCallback: (idx, _, val) =>
                        {
                            if(!val)
                            {
                                Connections[idx].Unset();
                            }
                        },
                    name: "DMA Interrupt Enable")
                .WithReservedBits(NumberOfSupportedEventsAndInterrupts, 32 - NumberOfSupportedEventsAndInterrupts);

            Registers.DmaEventInterruptRawStatus.Define(this)
                .WithFlags(0, NumberOfSupportedEventsAndInterrupts, FieldMode.Read,
                    valueProviderCallback: (idx, _) => eventActive[idx] || Connections[idx].IsSet,
                    name: "DMA Event or Interrupt Status")
                .WithReservedBits(NumberOfSupportedEventsAndInterrupts, 32 - NumberOfSupportedEventsAndInterrupts);

            Registers.DmaInterruptStatus.Define(this)
                .WithFlags(0, NumberOfSupportedEventsAndInterrupts, FieldMode.Read,
                    valueProviderCallback: (idx, _) => Connections[idx].IsSet,
                    name: "DMA Interrupt Status")
                .WithReservedBits(NumberOfSupportedEventsAndInterrupts, 32 - NumberOfSupportedEventsAndInterrupts);

            Registers.DmaInterruptClear.Define(this)
                .WithFlags(0, NumberOfSupportedEventsAndInterrupts, FieldMode.Write,
                    writeCallback: (idx, _, val) => 
                        {
                            if(val)
                            {
                                Connections[idx].Unset();
                            }
                        },
                    name: "DMA Interrupt Clear")
                .WithReservedBits(NumberOfSupportedEventsAndInterrupts, 32 - NumberOfSupportedEventsAndInterrupts);

            // This register is RO. To bring channel out of fault, issue KILL to Debug Registers
            Registers.FaultStatusDmaChannel.Define(this)
                .WithFlags(0, NumberOfChannels, FieldMode.Read, 
                    valueProviderCallback: (idx, _) => channels[idx].Status == Channel.ChannelStatus.Faulting,
                    name: "Faulting channels");

            Registers.DebugStatus.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => debugStatus, name: "Debug status (dbgstatus)")
                .WithReservedBits(1, 31);

            Registers.DebugCommand.Define(this)
                .WithValueField(0, 2, FieldMode.Write,
                    writeCallback: (_, val) =>
                        {
                            if(val == 0b00)
                            {
                                ExecuteDebugStart();
                            }
                            else
                            {
                                this.Log(LogLevel.Error, "Undefined DMA Debug Command: {0}", val);
                            }
                        },
                    name: "Debug Command")
                .WithReservedBits(2, 30);

            Registers.DebugInstruction0.Define(this)
                .WithEnumField(0, 1, out debugThreadType, name: "Debug thread select")
                .WithReservedBits(1, 7)
                .WithValueField(8, 3, out debugChannelNumber, name: "Debug channel select")
                .WithReservedBits(11, 5)
                .WithValueField(16, 8, out debugInstructionByte0, name: "Instruction byte 0")
                .WithValueField(24, 8, out debugInstructionByte1, name: "Instruction byte 1");

            Registers.DebugInstruction1.Define(this)
                .WithValueField(0, 32, out debugInstructionByte2_3_4_5, name: "Instruction byte 2,3,4,5");

            Registers.Configuration0.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => NumberOfSupportedPeripheralRequestInterfaces > 0, name: "Supports Peripheral Request Interface")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "Boot from PC")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "Boot security state for Manager Thread")
                .WithReservedBits(3, 1)
                .WithValueField(4, 3, FieldMode.Read, valueProviderCallback: _ => (ulong)NumberOfChannels - 1, name: "Number of DMA channels")
                .WithReservedBits(7, 5)
                .WithValueField(12, 5, FieldMode.Read, valueProviderCallback: _ => (ulong)NumberOfSupportedPeripheralRequestInterfaces - 1, name: "Number of Peripheral Request Interfaces")
                .WithValueField(17, 5, FieldMode.Read, valueProviderCallback: _ => (ulong)NumberOfSupportedEventsAndInterrupts - 1, name: "Number of Events/Interrupts")
                .WithReservedBits(22, 10);

            Registers.Configuration1.Define(this)
                .WithValueField(0, 3, FieldMode.Read, valueProviderCallback: _ => (ulong)Math.Floor(Math.Log(InstructionCacheLineLength, 2)), name: "Length of icache line")
                .WithReservedBits(3, 1)
                .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => (ulong)InstructionCacheLinesNumber - 1, name: "Number of icache lines")
                .WithReservedBits(8, 24);

            // Following Configuration registers are tagged only, and currently have no functionality implemented
            // this is the model's limitation at this moment
            Registers.Configuration2.Define(this)
                .WithTag("Boot PC Address", 0, 32);

            Registers.Configuration3.Define(this)
                .WithTag("Event/Interrupt is Non-secure", 0, NumberOfSupportedEventsAndInterrupts)
                .WithReservedBits(NumberOfSupportedEventsAndInterrupts, 32 - NumberOfSupportedEventsAndInterrupts);

            Registers.Configuration4.Define(this)
                .WithTag("Peripheral Request Interface is Non-secure", 0, NumberOfSupportedEventsAndInterrupts)
                .WithReservedBits(NumberOfSupportedEventsAndInterrupts, 32 - NumberOfSupportedEventsAndInterrupts);

            // These are configurable, but have currently no effect on DMA operation
            Registers.DmaConfiguration.Define(this)
                .WithValueField(0, 3, valueProviderCallback: _ => (ulong)Math.Floor(Math.Log(AXIBusWidth, 2)) - 3, name: "AXI bus width") // formula is: width_bytes = log2 (width / 8) ==> log2 width - 3
                .WithReservedBits(3, 1)
                .WithValueField(4, 3, valueProviderCallback: _ => WriteIssuingCapability - 1, name: "Write issuing capability (number of outstanding write transactions)")
                .WithReservedBits(7, 1)
                .WithValueField(8, 4, valueProviderCallback: _ => WriteQueueDepth - 1, name: "Write queue depth")
                .WithValueField(12, 3, valueProviderCallback: _ => ReadIssuingCapability - 1, name: "Read issuing capability (number of outstanding read transactions)")
                .WithReservedBits(15, 1)
                .WithValueField(16, 4, valueProviderCallback: _ => ReadQueueDepth - 1, name: "Read queue depth")
                .WithValueField(20, 10, valueProviderCallback: _ => DataBufferDepth - 1, name: "Data buffer lines")
                .WithReservedBits(30, 2);

            Registers.PeripheralIdentification0.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0x30, name: "Part number 0")
                .WithReservedBits(8, 24);

            Registers.PeripheralIdentification1.Define(this)
                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => 0x3, name: "Part number 1")
                .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => 0x1, name: "Designer 0")
                .WithReservedBits(8, 24);

            Registers.PeripheralIdentification2.Define(this)
                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => 0x4, name: "Designer 1")
                .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => Revision, name: "Revision")
                .WithReservedBits(8, 24);

            Registers.PeripheralIdentification3.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "Integration test logic")
                .WithReservedBits(1, 31);

            Registers.ComponentIdentification0.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0x0D, name: "Component ID 0")
                .WithReservedBits(8, 24);

            Registers.ComponentIdentification1.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0xF0, name: "Component ID 1")
                .WithReservedBits(8, 24);

            Registers.ComponentIdentification2.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0x05, name: "Component ID 2")
                .WithReservedBits(8, 24);

            Registers.ComponentIdentification3.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0xB1, name: "Component ID 3")
                .WithReservedBits(8, 24);
        }

        private bool IsEventOrInterruptValid(uint eventNumber)
        {
            if(eventNumber >= NumberOfSupportedEventsAndInterrupts)
            {
                this.Log(LogLevel.Error, "Trying to signal event: {0}, greater than number of supported events {1}", eventNumber, NumberOfSupportedEventsAndInterrupts);
                return false;
            }
            return true;
        }

        private bool IsPeripheralInterfaceValid(uint peripheral)
        {
            if(peripheral >= NumberOfSupportedPeripheralRequestInterfaces)
            {
                this.Log(LogLevel.Error, "Peripheral Request Interface {0} is not supported. {1} Request Interfaces are enabled.",
                    peripheral, NumberOfSupportedPeripheralRequestInterfaces);
                return false;
            }
            return true;
        }

        private bool SignalEventOrInterrupt(uint eventNumber)
        {
            if(!IsEventOrInterruptValid(eventNumber))
            {
                return false;
            }

            // Depending on the value of DmaInterruptEnable register, we either signal an IRQ on a line
            // or raise an internal event to wake up a thread that might be waiting for this event
            // either now, or in the future.
            if(interruptEnabled[eventNumber].Value)
            {
                // Signal an IRQ - use DmaInterruptClear to clear it.
                Connections[(int)eventNumber].Set();
            }
            else
            {
                // Raise an event
                eventActive[eventNumber] = true;
                bool anyResumed = false;
                foreach(var channel in channels.Where(c => c.Status == Channel.ChannelStatus.WaitingForEvent).Where(c => c.WaitingEventOrPeripheralNumber == eventNumber))
                {
                    anyResumed = true;
                    // The channel will execute in next iteration of `ExecuteLoop`
                    channel.Status = Channel.ChannelStatus.Executing;
                }

                // Only clear the event if there already existed a channel waiting for this event - see section 2.7.1 of the Reference Manual
                if(anyResumed)
                {
                    eventActive[eventNumber] = false;
                }
            }
            return true;
        }

        private void UpdateAbortInterrupt()
        {
            if(channels.Any(c => c.Status == Channel.ChannelStatus.Faulting))
            {
                AbortIRQ.Set();
            }
            else
            {
                AbortIRQ.Unset();
            }
        }

        private void ExecuteDebugStart()
        {
            debugStatus = true;
            ulong debugInstructionBytes = debugInstructionByte2_3_4_5.Value << 16 | debugInstructionByte1.Value << 8 | debugInstructionByte0.Value;

            string binaryInstructionString = Convert.ToString((long)debugInstructionBytes, 2);
            this.Log(LogLevel.Debug, "Inserted debug instruction: {0}", binaryInstructionString);

            if(!decoderRoot.TryParseOpcode((byte)debugInstructionBytes, out var debugInstruction))
            {
                this.Log(LogLevel.Error, "Debug instruction \"{0}\" is not supported or invalid. DMA will not execute.", binaryInstructionString);
                return;
            }

            debugInstruction.ParseAll(debugInstructionBytes);
            ExecuteDebugInstruction(debugInstruction, (int)debugChannelNumber.Value, debugThreadType.Value);
            debugStatus = false;
        }

        private void ExecuteDebugInstruction(Instruction firstInstruction, int channelIndex, DMAThreadType threadType)
        {
            if(!(firstInstruction is DMAGO
                || firstInstruction is DMAKILL
                || firstInstruction is DMASEV))
            {
                this.Log(LogLevel.Error, "Debug instruction \"{0}\" is not DMAGO, DMAKILL, DMASEV. It cannot be the first instruction.", firstInstruction.ToString());
                return;
            }

            LogInstructionExecuted(firstInstruction, threadType, channelIndex);
            // This is an instruction provided by the debug registers - it can't advance PC
            firstInstruction.Execute(threadType, threadType != DMAThreadType.Manager ? (int?)channelIndex : null, suppressAdvance: true);
            debugStatus = false;
            ExecuteLoop();
        }

        private void ExecuteLoop()
        {
            // lock uses Monitor calls, and is re-entrant, so there will be no problems when executing on the same thread
            lock(executeLock)
            {
                context = GetCurrentCPUOrNull();
                // TODO: in case of infinite loop, this will hang the emulation.
                // It's not ideal - separate thread will be good, but what about time flow?
                // Still, it's enough in the beginning, for simple use cases
                var insnBuffer = new byte[7];
                do
                {
                    foreach(var channelThread in channels.Where(c => c.Status == Channel.ChannelStatus.Executing))
                    {
                        this.Log(LogLevel.Debug, "Executing channel thread: {0}", channelThread.Id);

                        while(channelThread.Status == Channel.ChannelStatus.Executing)
                        {
                            var address = channelThread.PC;
                            sysbus.ReadBytes(address, insnBuffer.Length, insnBuffer, 0, context: context);

                            if(!decoderRoot.TryParseOpcode(insnBuffer[0], out var instruction))
                            {
                                this.Log(LogLevel.Error, "Invalid instruction with opcode 0x{0:X} at address: 0x{1:X}. Aborting thread {2}.", insnBuffer[0], address, channelThread.Id);
                                channelThread.SignalChannelAbort(Channel.ChannelFaultReason.UndefinedInstruction);
                                continue;
                            }

                            var insnIdx = 0;
                            while(!instruction.IsFinished)
                            {
                                instruction.Parse(insnBuffer[insnIdx]);
                                insnIdx++;
                            }

                            LogInstructionExecuted(instruction, DMAThreadType.Channel, channelThread.Id, channelThread.PC);
                            instruction.Execute(DMAThreadType.Channel, channelThread.Id);
                        }
                    }

                // A channel might have become unpaused as a result of an event generated by another channel
                // As long as there are any channels in executing state, we have to retry
                } while(channels.Any(c => c.Status == Channel.ChannelStatus.Executing));
                context = null;
            }
        }

        private void LogInstructionExecuted(Instruction insn, DMAThreadType threadType, int threadId, ulong? address = null)
        {
            // We check log level here to avoid string interpolation
            if(Logger.MinimumLogLevel <= LogLevel.Noisy)
            {
                this.Log(LogLevel.Noisy, "[{0}] Executing: {1} {2}", threadType == DMAThreadType.Manager ? "M" : threadId.ToString(),
                    insn.ToString(), address != null ? $"@ 0x{address:X}" : "" );
            }
        }

        private ICPU GetCurrentCPUOrNull()
        {
            if(!machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                return null;
            }
            return cpu;
        }

        private IEnumRegisterField<DMAThreadType> debugThreadType;
        private IValueRegisterField debugChannelNumber;
        private IValueRegisterField debugInstructionByte0;
        private IValueRegisterField debugInstructionByte1;
        private IValueRegisterField debugInstructionByte2_3_4_5;
        private IFlagRegisterField[] interruptEnabled;
        // It's volatile, so this status is visible if several CPU threads will try to drive the DMA
        private volatile bool debugStatus;
        private readonly bool[] eventActive;
        // Driving DMA from several cores at once has little sense
        // but it's still possible that another core will drive a client peripheral requesting transfers
        private readonly object executeLock = new object();

        private readonly Channel[] channels;

        private const int MaximumSupportedEventsOrInterrupts = 32;
        private const int MaximumSupportedPeripheralRequestInterfaces = 32;

        private ICPU context = null;

        private class Channel
        {
            public Channel(PL330_DMA parent, int id)
            {
                this.Parent = parent;
                this.Id = id;

                Reset();
                DefineRegisters();
            }

            public void Reset()
            {
                ChannelControlRawValue = 0x00800200;
                PC = 0;
                SourceAddress = 0;
                DestinationAddress = 0;

                status = ChannelStatus.Stopped;
                faultReason = ChannelFaultReason.NoFault;
                RequestType = ChannelRequestType.Single;
                RequestLast = false;
                WaitingEventOrPeripheralNumber = 0;
                Peripheral = null;

                LoopCounter[0] = 0;
                LoopCounter[1] = 0;
                localMFIFO.Clear();
            }

            private void DefineRegisters()
            {
                (Registers.Channel0FaultType + Id * 4).Define(Parent)
                    .WithEnumField<DoubleWordRegister, ChannelFaultReason>(0, 32, FieldMode.Read, valueProviderCallback: _ => faultReason, name: $"Channel {Id} Fault Reason");

                (Registers.Channel0Status + Id * 8).Define(Parent)
                    .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => (ulong)Status, name: $"Channel {Id} Status")
                    .WithValueField(4, 5, FieldMode.Read, valueProviderCallback: _ => WaitingEventOrPeripheralNumber, name: $"Channel {Id} Wakeup Number")
                    .WithReservedBits(9, 5)
                    .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => RequestType == ChannelRequestType.Burst, name: "DMAWFP is burst set") // dmawfp_b_ns
                    .WithTaggedFlag("DMAWFP is periph bit set", 15) // It the transfer type is driven by the peripheral
                    .WithReservedBits(16, 5)
                    .WithTaggedFlag("Channel Non Secure", 21)
                    .WithReservedBits(22, 10);

                (Registers.Channel0ProgramCounter + Id * 8).Define(Parent)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => PC, name: $"Channel {Id} Program Counter");

                (Registers.Channel0SourceAddress + Id * 0x20).Define(Parent)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => SourceAddress, name: $"Channel {Id} Source Address");

                (Registers.Channel0DestinationAddress + Id * 0x20).Define(Parent)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => DestinationAddress, name: $"Channel {Id} Destination Address");

                (Registers.Channel0Control + Id * 0x20).Define(Parent)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => ChannelControlRawValue, name: $"Channel {Id} Control");

                (Registers.Channel0LoopCounter0 + Id * 0x20).Define(Parent)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => LoopCounter[0], name: $"Channel {Id} Loop Counter 0");

                (Registers.Channel0LoopCounter1 + Id * 0x20).Define(Parent)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => LoopCounter[1], name: $"Channel {Id} Loop Counter 1");
            }

            public ulong PC { get; set; }

            public uint SourceAddress { get; set; }
            public uint DestinationAddress { get; set; }
            public uint ChannelControlRawValue
            {
                get => channelControlRawValue;
                set
                {
                    channelControlRawValue = value;

                    SourceIncrementingAddress = BitHelper.GetValue(value, 0, 1) == 1;
                    SourceReadSize = 1 << (int)BitHelper.GetValue(value, 1, 3);
                    SourceBurstLength = (int)BitHelper.GetValue(value, 4, 4) + 1;

                    DestinationIncrementingAddress = BitHelper.GetValue(value, 14, 1) == 1;
                    DestinationWriteSize = 1 << (int)BitHelper.GetValue(value, 15, 3);
                    DestinationBurstLength = (int)BitHelper.GetValue(value, 18, 4) + 1;

                    EndianSwapSize = 1 << (int)BitHelper.GetValue(value, 28, 3);
                }
            }

            public void SignalChannelAbort(Channel.ChannelFaultReason reason)
            {
                Parent.Log(LogLevel.Error, "Channel {0} is aborting because of {1}", Id, reason.ToString());
                // Changing status will automatically set Abort IRQ
                this.Status = Channel.ChannelStatus.Faulting;
                this.faultReason = reason;
            }

            public ChannelStatus Status 
            {
                get => status;
                set
                {
                    status = value;
                    if(status != Channel.ChannelStatus.Faulting)
                    {
                        // If we are not Faulting then clear Fault Reason
                        // since it could be set previously, when the channel aborted
                        this.faultReason = ChannelFaultReason.NoFault;
                    }
                    Parent.UpdateAbortInterrupt();
                }
            }

            // RequestType is part of Peripheral Request Interface (is set by `DMAWFP`)
            public ChannelRequestType RequestType { get; set; }
            // Whether it's a last request - this is set in peripheral transfers only, in infinite loop transfers
            public bool RequestLast { get; set; }

            // Sizes are specified in bytes
            public int SourceReadSize { get; private set; }
            public int DestinationWriteSize { get; private set; }

            public int SourceBurstLength { get; private set; }
            public int DestinationBurstLength { get; private set; }

            public bool SourceIncrementingAddress { get; private set; }
            public bool DestinationIncrementingAddress { get; private set; }

            // In bytes
            public int EndianSwapSize { get; private set; }
            
            // What event is the channel waiting for (after calling DMAWFE)
            // We don't care about clearing this, since it only has meaning when the channel is in WaitingForEvent state
            public uint WaitingEventOrPeripheralNumber { get; set; }
            // Peripheral bound to the channel
            public int? Peripheral { get; set; }

            public readonly int Id;
            public readonly byte[] LoopCounter = new byte[2];
            public readonly Queue<byte> localMFIFO = new Queue<byte>();

            private ChannelStatus status;
            private ChannelFaultReason faultReason;
            private uint channelControlRawValue;
            private readonly PL330_DMA Parent;

            public enum ChannelStatus
            {
                // The documentation enumerates more states, but let's reduce this number for now
                // for the implementation to be more manageable.
                // Also, some states have no meaning for us, as they are related to the operation of the bus
                Stopped = 0b0000,
                Executing = 0b0001,
                WaitingForEvent = 0b0100,
                WaitingForPeripheral = 0b0111,
                Faulting = 0b1111,
            }

            [Flags]
            public enum ChannelFaultReason
            {
                NoFault = 0,
                UndefinedInstruction = 1 << 0,           // undef_instr
                InvalidOperand = 1 << 1,                 // operand_invalid
                EventInvalidSecurityState = 1 << 5,      // ch_evnt_err
                PeripheralInvalidSecurityState = 1 << 6, // ch_periph_err
                ChannelControlRegisterManipulationInvalidSecurityState = 1 << 7, // ch_rdwr_err
                OutOfSpaceInMFIFO = 1 << 12,          // mfifo_err
                NotEnoughStoredDataInMFIFO = 1 << 13, // st_data_unavailable
                InstructionFetchError = 1 << 16,      // instr_fetch_err
                DataWriteError = 1 << 17,             // data_write_err
                DataReadError = 1 << 18,              // data_read_err
                OriginatesFromDebugInstruction = 1 << 30, // dbg_instr
                LockupError = 1 << 31,                // lockup_err
            }

            public enum ChannelRequestType
            {
                Single = 0,
                Burst = 1
            }
        }

        private enum DMAThreadType
        {
            Manager = 0,
            Channel = 1
        }

        private enum Registers : long
        {
            DmaManagerStatus = 0x0,
            DmaProgramCounter = 0x4,
            DmaInterruptEnable = 0x20,
            DmaEventInterruptRawStatus = 0x24,
            DmaInterruptStatus = 0x28,
            DmaInterruptClear = 0x2C,
            FaultStatusDmaManager = 0x30,
            FaultStatusDmaChannel = 0x34,
            FaultTypeDmaManager = 0x38,

            Channel0FaultType = 0x40,
            Channel1FaultType = 0x44,
            Channel2FaultType = 0x48,
            Channel3FaultType = 0x4C,
            Channel4FaultType = 0x50,
            Channel5FaultType = 0x54,
            Channel6FaultType = 0x58,
            Channel7FaultType = 0x5C,

            Channel0Status = 0x100,
            Channel1Status = 0x108,
            Channel2Status = 0x110,
            Channel3Status = 0x118,
            Channel4Status = 0x120,
            Channel5Status = 0x128,
            Channel6Status = 0x130,
            Channel7Status = 0x138,

            Channel0ProgramCounter = 0x104,
            Channel1ProgramCounter = 0x10C,
            Channel2ProgramCounter = 0x114,
            Channel3ProgramCounter = 0x11C,
            Channel4ProgramCounter = 0x124,
            Channel5ProgramCounter = 0x12C,
            Channel6ProgramCounter = 0x134,
            Channel7ProgramCounter = 0x13C,

            Channel0SourceAddress = 0x400,
            Channel1SourceAddress = 0x420,
            Channel2SourceAddress = 0x440,
            Channel3SourceAddress = 0x460,
            Channel4SourceAddress = 0x480,
            Channel5SourceAddress = 0x4A0,
            Channel6SourceAddress = 0x4C0,
            Channel7SourceAddress = 0x4E0,

            Channel0DestinationAddress = 0x404,
            Channel1DestinationAddress = 0x424,
            Channel2DestinationAddress = 0x444,
            Channel3DestinationAddress = 0x464,
            Channel4DestinationAddress = 0x484,
            Channel5DestinationAddress = 0x4A4,
            Channel6DestinationAddress = 0x4C4,
            Channel7DestinationAddress = 0x4E4,

            Channel0Control = 0x408,
            Channel1Control = 0x428,
            Channel2Control = 0x448,
            Channel3Control = 0x468,
            Channel4Control = 0x488,
            Channel5Control = 0x4A8,
            Channel6Control = 0x4C8,
            Channel7Control = 0x4E8,

            Channel0LoopCounter0 = 0x40C,
            Channel1LoopCounter0 = 0x42C,
            Channel2LoopCounter0 = 0x44C,
            Channel3LoopCounter0 = 0x46C,
            Channel4LoopCounter0 = 0x48C,
            Channel5LoopCounter0 = 0x4AC,
            Channel6LoopCounter0 = 0x4CC,
            Channel7LoopCounter0 = 0x4EC,

            Channel0LoopCounter1 = 0x410,
            Channel1LoopCounter1 = 0x430,
            Channel2LoopCounter1 = 0x450,
            Channel3LoopCounter1 = 0x470,
            Channel4LoopCounter1 = 0x490,
            Channel5LoopCounter1 = 0x4B0,
            Channel6LoopCounter1 = 0x4D0,
            Channel7LoopCounter1 = 0x4F0,

            DebugStatus = 0xD00,
            DebugCommand = 0xD04,
            DebugInstruction0 = 0xD08,
            DebugInstruction1 = 0xD0C,

            Configuration0 = 0xE00,
            Configuration1 = 0xE04,
            Configuration2 = 0xE08,
            Configuration3 = 0xE0C,
            Configuration4 = 0xE10,
            DmaConfiguration = 0xE14,
            // The watchdog is used to detect lock-ups - when there are not enough resources (MFIFO space) to complete a transfer.
            // It can be a source of abort IRQ. For now, let's leave it unimplemented (2.8.3 Watchdog abort)
            Watchdog = 0xE80,

            PeripheralIdentification0 = 0xFE0,
            PeripheralIdentification1 = 0xFE4,
            PeripheralIdentification2 = 0xFE8,
            PeripheralIdentification3 = 0xFEC,
            ComponentIdentification0 = 0xFF0,
            ComponentIdentification1 = 0xFF4,
            ComponentIdentification2 = 0xFF8,
            ComponentIdentification3 = 0xFFC,
        }
    }
}
