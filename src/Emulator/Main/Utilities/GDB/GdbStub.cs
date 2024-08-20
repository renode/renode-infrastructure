//
// Copyright (c) 2010-2024 Antmicro
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
using System.Linq;

namespace Antmicro.Renode.Utilities.GDB
{
    [Transient]
    public class GdbStub : IDisposable, IExternal
    {
        public GdbStub(IMachine machine, IEnumerable<ICpuSupportingGdb> cpus, int port, bool autostartEmulation)
        {
            this.cpus = cpus;
            Port = port;

            LogsEnabled = true;

            pcktBuilder = new PacketBuilder();
            commandsManager = new CommandsManager(machine, cpus);
            TypeManager.Instance.AutoLoadedType += commandsManager.Register;

            terminal = new SocketServerProvider(false, serverName: "GDB");
            terminal.DataReceived += OnByteWritten;
            terminal.ConnectionAccepted += delegate
            {
                commandsManager.CanAttachCPU = false;
                foreach(var cpu in commandsManager.ManagedCpus)
                {
                    cpu.Halted += OnHalted;
                    cpu.ExecutionMode = ExecutionMode.SingleStep;
                    cpu.DebuggerConnected = true;
                }
                if(autostartEmulation && !EmulationManager.Instance.CurrentEmulation.IsStarted)
                {
                    EmulationManager.Instance.CurrentEmulation.StartAll();
                }
            };
            terminal.ConnectionClosed += delegate
            {
                foreach(var cpu in commandsManager.ManagedCpus)
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
                            ctx.Send(new Packet(PacketData.StopReply(args.BreakpointType.Value, commandsManager.ManagedCpus[cpuSupportingGdb], args.Address)));
                        }
                        else
                        {
                            ctx.Send(new Packet(PacketData.StopReply(args.BreakpointType.Value, args.Address)));
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
                            ctx.Send(new Packet(PacketData.StopReply(InterruptSignal, commandsManager.ManagedCpus[cpuSupportingGdb])));
                        }
                    }
                    else
                    {
                        ctx.Send(new Packet(PacketData.StopReply(InterruptSignal)));
                    }
                    return;
                case HaltReason.Step:
                    if(commandsManager.Machine.SystemBus.IsMultiCore)
                    {
                        commandsManager.SelectCpuForDebugging(cpuSupportingGdb);
                        ctx.Send(new Packet(PacketData.StopReply(TrapSignal, commandsManager.ManagedCpus[cpuSupportingGdb])));
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

        private ICpuSupportingGdb stopReplyingCpu;

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
                    try
                    {
                        packetDatas = Command.Execute(command, result.Packet);
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
                    // If there is no data here, we will respond later with Stop Reply Response
                    foreach(var packetData in packetDatas)
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

