//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Migrant;

namespace Antmicro.Renode.Utilities.GDB
{
    [Transient]
    public class GdbStub : IDisposable, IExternal
    {
        public GdbStub(Machine machine, IEnumerable<ICpuSupportingGdb> cpus, int port, bool autostartEmulation, bool blockOnStep)
        {
            this.cpus = cpus;
            Port = port;
            this.blockOnStep = blockOnStep;

            LogsEnabled = true;

            pcktBuilder = new PacketBuilder();
            commandsManager = new CommandsManager(machine, cpus, blockOnStep);
            commandsManager.ShouldAutoStart = autostartEmulation;
            TypeManager.Instance.AutoLoadedType += commandsManager.Register;

            terminal = new SocketServerProvider(false);
            terminal.DataReceived += OnByteWritten;
            terminal.ConnectionAccepted += delegate
            {
                commandsManager.CanAttachCPU = false;
                foreach(var cpu in commandsManager.ManagedCpus.Values)
                {
                    cpu.Halted += OnHalted;
                    cpu.ExecutionMode = blockOnStep ? ExecutionMode.SingleStepBlocking : ExecutionMode.SingleStepNonBlocking;
                    cpu.DebuggerConnected = true;
                }
            };
            terminal.ConnectionClosed += delegate
            {
                foreach(var cpu in commandsManager.ManagedCpus.Values)
                {
                    cpu.Halted -= OnHalted;
                    cpu.ExecutionMode = ExecutionMode.Continuous;
                    cpu.DebuggerConnected = false;
                }
                commandsManager.CanAttachCPU = true;
            };
            terminal.Start(port);
            commHandler = new CommunicationHandler(this, commandsManager);

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

        public void Dispose()
        {
            foreach(var cpu in cpus)
            {
                cpu.Halted -= OnHalted;
            }
            terminal.Dispose();
        }

        public int Port { get; private set; }

        public bool LogsEnabled { get; set; }

        private void OnHalted(HaltArguments args)
        {
            using(var ctx = commHandler.OpenContext())
            {
                // GDB counts threads starting from `1`, while Renode counts them from `0` - hence the incrementation
                var cpuId = args.CpuId + 1;
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
                            commandsManager.SelectCpuForDebugging(cpuId);
                            ctx.Send(new Packet(PacketData.StopReply(args.BreakpointType.Value, cpuId, args.Address)));
                        }
                        else
                        {
                            ctx.Send(new Packet(PacketData.StopReply(args.BreakpointType.Value, args.Address)));
                        }
                        break;
                    }
                    return;
                case HaltReason.Step:
                case HaltReason.Pause:
                    if(commandsManager.Machine.SystemBus.IsMultiCore)
                    {
                        commandsManager.SelectCpuForDebugging(cpuId);
                        ctx.Send(new Packet(PacketData.StopReply(TrapSignal, cpuId)));
                    }
                    else
                    {
                        ctx.Send(new Packet(PacketData.StopReply(TrapSignal)));
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
                foreach(var cpu in commandsManager.ManagedCpus.Values)
                {
                    cpu.ExecutionMode = blockOnStep ? ExecutionMode.SingleStepBlocking : ExecutionMode.SingleStepNonBlocking;
                }
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
                    PacketData packetData;
                    try
                    {
                        packetData = Command.Execute(command, result.Packet);
                    }
                    catch(Exception e)
                    {
                        if(LogsEnabled)
                        {
                            commandsManager.Cpu.Log(LogLevel.Debug, "{0}", e);
                            // Get to the inner-most exception. The outer-most exception here is often
                            // 'Reflection.TargetInvocationException' which doesn't have any useful message.
                            while(e.InnerException != null)
                            {
                                e = e.InnerException;
                            }
                            var commandString = result.Packet.Data.GetDataAsStringLimited();
                            commandsManager.Cpu.Log(LogLevel.Error, "GDB '{0}' command failed: {1}", commandString, e.Message);
                        }
                        ctx.Send(new Packet(PacketData.ErrorReply(Error.Unknown)));
                        return;
                    }
                    // null means that we will respond later with Stop Reply Response
                    if(packetData != null)
                    {
                        ctx.Send(new Packet(packetData));
                    }
                }
            }
        }

        private readonly PacketBuilder pcktBuilder;
        private readonly IEnumerable<ICpuSupportingGdb> cpus;
        private readonly SocketServerProvider terminal;
        private readonly CommandsManager commandsManager;
        private readonly CommunicationHandler commHandler;
        private readonly bool blockOnStep;

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

            private readonly GdbStub stub;
            private readonly CommandsManager manager;
            private readonly Queue<byte> queue;
            private readonly object internalLock;
            private int counter;

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

