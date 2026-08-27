//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Utilities.GDB
{
    [Transient]
    public class GdbStub : IDisposable, IExternal, IDisconnectableState
    {
        public GdbStub(IMachine machine, IEnumerable<ICpuSupportingGdb> cpus, int port, bool autostartEmulation)
            : this(machine, cpus)
        {
            terminal = new SocketServerProvider(false, serverName: "GDB");
            SetupTerminal(connected: false, autostartEmulation: autostartEmulation);
            terminal.Start(port);
            LogsEnabled = false;
        }

        public GdbStub(IMachine machine, IEnumerable<ICpuSupportingGdb> cpus, SocketServerProvider terminal)
            : this(machine, cpus)
        {
            this.terminal = terminal;
            SetupTerminal(connected: true, autostartEmulation: false);
            LogsEnabled = false;
        }

        public void AttachCPU(ICpuSupportingGdb cpu)
        {
            commandsManager.AttachCPU(cpu);
        }

        public bool IsCPUAttached(ICpuSupportingGdb cpu)
        {
            return commandsManager.IsCPUAttached(cpu);
        }

        public void DisconnectState()
        {
            terminal.ClearConnectionEvents();
            disconnectedState = true;
        }

        public void Dispose()
        {
            foreach(var cpu in cpus)
            {
                cpu.Halted -= OnHalted;
            }

            if(!disconnectedState)
            {
                terminal.Dispose();
            }
        }

        public event Action<Stream> ConnectionAccepted
        {
            add => terminal.ConnectionAccepted += value;
            remove => terminal.ConnectionAccepted -= value;
        }

        public IEnumerable<string> AttachedCPUNames => cpus.Select(cpu => commandsManager.Machine.GetLocalName(cpu));

        public int Port => Terminal.Port.Value;

        public bool LogsEnabled { get; set; }

        public bool GdbClientConnected => !commandsManager.CanAttachCPU;

        public SocketServerProvider Terminal => terminal;

        public CommandsManager CommandsManager => commandsManager;

        private GdbStub(IMachine machine, IEnumerable<ICpuSupportingGdb> cpus)
        {
            this.cpus = cpus;
            LogsEnabled = true;

            pcktBuilder = new PacketBuilder();
            commandsManager = new CommandsManager(machine, cpus);
            commHandler = new CommunicationHandler(this, commandsManager);
            TypeManager.Instance.AutoLoadedType += commandsManager.Register;
            EmulationManager.PreservableManager.RegisterPreservable(this, livesThroughEmulationChange: false);
        }

        private void SetupTerminal(bool connected, bool autostartEmulation)
        {
            terminal.DataReceived += OnByteWritten;
            terminal.ConnectionClosed += OnConnectionClosed;

            terminal.ConnectionAccepted += delegate
            {
                OnConnectionAccepted();
                if(autostartEmulation && !EmulationManager.Instance.CurrentEmulation.IsStarted)
                {
                    EmulationManager.Instance.CurrentEmulation.StartAll();
                }
            };

            if(connected)
            {
                OnConnectionAccepted();
            }
        }

        private void OnByteWritten(int b)
        {
            if(b == -1)
            {
                return;
            }
            var result = pcktBuilder.AppendByte((byte)b);
            if(result == null)
            {
                return;
            }

            if(result.Interrupt)
            {
                if(LogsEnabled)
                {
                    commandsManager.Cpu.Log(LogLevel.Noisy, "GDB CTRL-C occured - pausing CPU");
                }

                // This weird syntax ensures we have unpaused cores to report first, and only if there are none, we will fall-back to halted ones
                stopReplyingCpu = commandsManager.ManagedCpus.OrderByDescending(cpu => !cpu.IsHalted).FirstOrDefault();
                foreach(var cpu in commandsManager.ManagedCpus)
                {
                    // This call is synchronous, so it's safe to assume that `stopReplyingCpu` will still be valid
                    cpu.Pause();
                }
                stopReplyingCpu = null;
                return;
            }

            using(var ctx = commHandler.OpenContext())
            {
                if(result.CorruptedPacket)
                {
                    if(LogsEnabled)
                    {
                        commandsManager.Cpu.Log(LogLevel.Warning, "Corrupted GDB packet received: {0}", result.Packet.Data.GetDataAsStringLimited());
                    }
                    // send NACK
                    ctx.Send((byte)'-');
                    return;
                }

                if(LogsEnabled)
                {
                    commandsManager.Cpu.Log(LogLevel.Debug, "GDB packet received: {0}", result.Packet.Data.GetDataAsStringLimited());
                }
                // send ACK
                ctx.Send((byte)'+');

                Command command;
                if(!commandsManager.TryGetCommand(result.Packet, out command))
                {
                    if(LogsEnabled)
                    {
                        commandsManager.Cpu.Log(LogLevel.Warning, "Unsupported GDB command: {0}", result.Packet.Data.GetDataAsStringLimited());
                    }
                    ctx.Send(new Packet(PacketData.Empty));
                }
                else
                {
                    IEnumerable<PacketData> packetDatas;
                    // The CPU can halt before `Execute` returns, so the reply has to be expected
                    // before dispatching and unexpected again if the command answers on its own.
                    ExpectStopReply();
                    try
                    {
                        packetDatas = Command.Execute(command, result.Packet);
                    }
                    catch(Exception e)
                    {
                        ConsumeStopReply();
                        if(e.InnerException is InvalidRegisterAccessException)
                        {
                            ctx.Send(new Packet(PacketData.ErrorReply(Error.OperationNotPermitted)));
                            return;
                        }

                        // Get to the innermost exception, as it will have the reason for the error.
                        Exception innermostException = e;
                        while(innermostException.InnerException != null)
                        {
                            innermostException = innermostException.InnerException;
                        }
                        var commandString = result.Packet.Data.GetDataAsStringLimited();
                        commandsManager.Cpu.Log(LogLevel.Error, "GDB '{0}' command failed: {1}", commandString, innermostException.Message);

                        if(Emulator.InCIMode && !(innermostException is RecoverableException))
                        {
                            throw;
                        }

                        ctx.Send(new Packet(PacketData.ErrorReply(innermostException.Message)));
                        return;
                    }

                    // If there is no data here, we will respond later with Stop Reply Response
                    if(packetDatas.Any())
                    {
                        ConsumeStopReply();
                    }
                    foreach(var packetData in packetDatas)
                    {
                        ctx.Send(new Packet(packetData));
                    }
                }
            }
        }

        private void OnHalted(HaltArguments args)
        {
            using(var ctx = commHandler.OpenContext())
            {
                // If we got here, and the CPU doesn't support Gdb (ICpuSupportingGdb) something went seriously wrong - this is GdbStub after all
                var cpuSupportingGdb = (ICpuSupportingGdb)args.Cpu;

                // We only should send one stop response to Gdb in all-stop mode
                bool sendStopResponse = cpuSupportingGdb == stopReplyingCpu || stopReplyingCpu == null;

                switch(args.Reason)
                {
                case HaltReason.Breakpoint:
                    switch(args.BreakpointType)
                    {
                    case BreakpointType.AccessWatchpoint:
                    case BreakpointType.WriteWatchpoint:
                    case BreakpointType.ReadWatchpoint:
                    case BreakpointType.HardwareBreakpoint:
                    case BreakpointType.MemoryBreakpoint:
                        if(commandsManager.Machine.SystemBus.IsMultiCore)
                        {
                            commandsManager.SelectCpuForDebugging(cpuSupportingGdb);
                            SendStopReply(ctx, PacketData.StopReply(args.BreakpointType.Value, commandsManager.ManagedCpus[cpuSupportingGdb], args.Address));
                        }
                        else
                        {
                            SendStopReply(ctx, PacketData.StopReply(args.BreakpointType.Value, args.Address));
                        }
                        break;
                    }
                    return;
                case HaltReason.Pause:
                    if(commandsManager.Machine.InternalPause)
                    {
                        // Don't set Trap signal when the pause is internal as execution will
                        // be resumed after the reset is completed. This will cause GDB to stop and the emulation to continue
                        // resulting in a desync (eg. breakpoints will not be triggered)
                        return;
                    }
                    if(commandsManager.Machine.SystemBus.IsMultiCore)
                    {
                        if(sendStopResponse)
                        {
                            commandsManager.SelectCpuForDebugging(cpuSupportingGdb);
                            SendStopReply(ctx, PacketData.StopReply(InterruptSignal, commandsManager.ManagedCpus[cpuSupportingGdb]));
                        }
                    }
                    else
                    {
                        SendStopReply(ctx, PacketData.StopReply(InterruptSignal));
                    }
                    return;
                case HaltReason.Step:
                    // A single-stepping CPU reports itself as halted every time it enters the wait
                    // for the next step command, not once per completed step, so only the report
                    // that answers a resume is a stop. Sending the rest would desync the session:
                    // the surplus packet is read as the answer to the next resume.
                    if(!ConsumeStopReply())
                    {
                        return;
                    }
                    if(commandsManager.Machine.SystemBus.IsMultiCore)
                    {
                        commandsManager.SelectCpuForDebugging(cpuSupportingGdb);
                        SendStopReply(ctx, PacketData.StopReply(TrapSignal, commandsManager.ManagedCpus[cpuSupportingGdb]));
                    }
                    else
                    {
                        SendStopReply(ctx, PacketData.StopReply(TrapSignal));
                    }
                    return;
                case HaltReason.Abort:
                    ctx.Send(new Packet(PacketData.AbortReply(AbortSignal)));
                    return;
                default:
                    throw new ArgumentException("Unexpected halt reason");
                }
            }
        }

        // Commands that resume the CPU are answered by an asynchronous Stop Reply sent from
        // `OnHalted`, and exactly one is owed per resume. The CPU may however report being halted
        // several times for a single stop, so the surplus reports have to be dropped.
        private void ExpectStopReply()
        {
            lock(stopReplyLock)
            {
                stopReplyOwed = true;
            }
        }

        // Whether a reply was owed, and no longer is.
        private bool ConsumeStopReply()
        {
            lock(stopReplyLock)
            {
                var owed = stopReplyOwed;
                stopReplyOwed = false;
                return owed;
            }
        }

        // Sending is what settles the debt, rather than reaching `OnHalted`: the branches that
        // report nothing leave the reply owed, so a later halt can still answer the resume.
        private void SendStopReply(CommunicationHandler.Context ctx, PacketData data)
        {
            ConsumeStopReply();
            ctx.Send(new Packet(data));
        }

        private void OnConnectionAccepted()
        {
            commandsManager.CanAttachCPU = false;
            foreach(var cpu in commandsManager.ManagedCpus)
            {
                cpu.Halted += OnHalted;
                cpu.ExecutionMode = ExecutionMode.SingleStep;
                cpu.DebuggerConnected = true;
            }
        }

        private void OnConnectionClosed()
        {
            ConsumeStopReply();
            foreach(var cpu in commandsManager.ManagedCpus)
            {
                cpu.Halted -= OnHalted;
                cpu.ExecutionMode = ExecutionMode.Continuous;
                cpu.DebuggerConnected = false;
            }
            commandsManager.CanAttachCPU = true;
        }

        private ICpuSupportingGdb stopReplyingCpu;
        private bool stopReplyOwed;
        private bool disconnectedState;

        private readonly object stopReplyLock = new object();
        private readonly PacketBuilder pcktBuilder;
        private readonly IEnumerable<ICpuSupportingGdb> cpus;
        private readonly SocketServerProvider terminal;
        private readonly CommandsManager commandsManager;
        private readonly CommunicationHandler commHandler;

        private const int InterruptSignal = 2;
        private const int TrapSignal = 5;
        private const int AbortSignal = 6;

        private class CommunicationHandler
        {
            public CommunicationHandler(GdbStub stub, CommandsManager manager)
            {
                this.stub = stub;
                this.manager = manager;
                queue = new Queue<byte>();
                internalLock = new object();
            }

            public Context OpenContext()
            {
                lock(internalLock)
                {
                    counter++;
                    if(counter > 1)
                    {
                        if(stub.LogsEnabled)
                        {
                            manager.Cpu.Log(LogLevel.Debug, "Gdb stub: entering nested communication context. All bytes will be queued.");
                        }
                    }
                    return new Context(this, counter > 1);
                }
            }

            public void SendByteDirect(byte b)
            {
                stub.terminal.SendByte(b);
            }

            private void SendAllBufferedData()
            {
                foreach(var b in queue)
                {
                    stub.terminal.SendByte(b);
                }
                queue.Clear();
            }

            private void ContextClosed(IEnumerable<byte> buffer)
            {
                lock(internalLock)
                {
                    if(buffer != null)
                    {
                        foreach(var b in buffer)
                        {
                            queue.Enqueue(b);
                        }
                    }

                    counter--;
                    if(counter == 0 && queue.Count > 0)
                    {
                        if(stub.LogsEnabled)
                        {
                            manager.Cpu.Log(LogLevel.Debug, "Gdb stub: leaving nested communication context. Sending {0} queued bytes.", queue.Count);
                        }
                        SendAllBufferedData();
                    }
                }
            }

            private int counter;

            private readonly GdbStub stub;
            private readonly CommandsManager manager;
            private readonly Queue<byte> queue;
            private readonly object internalLock;

            public class Context : IDisposable
            {
                public Context(CommunicationHandler commHandler, bool useBuffering)
                {
                    this.commHandler = commHandler;
                    if(useBuffering)
                    {
                        buffer = new Queue<byte>();
                    }
                }

                public void Dispose()
                {
                    commHandler.ContextClosed(buffer);
                }

                public void Send(Packet packet)
                {
                    if(commHandler.stub.LogsEnabled)
                    {
                        commHandler.manager.Cpu.Log(LogLevel.Debug, "Sending response to GDB: {0}", packet.Data.GetDataAsStringLimited());
                    }
                    foreach(var b in packet.GetCompletePacket())
                    {
                        Send(b);
                    }
                }

                public void Send(byte b)
                {
                    if(buffer == null)
                    {
                        commHandler.SendByteDirect(b);
                    }
                    else
                    {
                        buffer.Enqueue(b);
                    }
                }

                private readonly CommunicationHandler commHandler;
                private readonly Queue<byte> buffer;
            }
        }
    }
}
