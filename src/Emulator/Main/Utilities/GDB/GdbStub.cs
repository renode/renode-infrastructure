//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities.GDB;
using Antmicro.Renode.Utilities.GDB.Commands;
using System.Collections.Generic;
using System.Threading;

namespace Antmicro.Renode.Utilities
{
    public class GdbStub : IDisposable, IExternal
    {
        public GdbStub(int port, ICpuSupportingGdb cpu, bool autostartEmulation)
        {
            this.cpu = cpu;
            Port = port;

            LogsEnabled = true;

            pcktBuilder = new PacketBuilder();
            commands = new CommandsManager(cpu);
            commands.ShouldAutoStart = autostartEmulation;
            TypeManager.Instance.AutoLoadedType += commands.Register;

            terminal = new SocketServerProvider();
            terminal.DataReceived += OnByteWritten;
            terminal.ConnectionAccepted += delegate
            {
                DebuggerConnected = true;
                cpu.Halted += OnHalted;
                cpu.ExecutionMode = ExecutionMode.SingleStep;
            };
            terminal.ConnectionClosed += delegate
            {
                DebuggerConnected = false;
                cpu.Halted -= OnHalted;
                cpu.ExecutionMode = ExecutionMode.Continuous;
            };
            terminal.Start(port);
            commHandler = new CommunicationHandler(this);

            LogsEnabled = false;
        }

        public void Dispose()
        {
            cpu.Halted -= OnHalted;
            terminal.Dispose();
        }

        public int Port { get; private set; }

        public bool DebuggerConnected { get; set; }

        public bool LogsEnabled { get; set; }

        private void OnHalted(HaltArguments args)
        {
            using(var ctx = commHandler.OpenContext())
            {
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
                        ctx.Send(new Packet(PacketData.StopReply(args.BreakpointType.Value, args.Address)));
                        break;
                    }
                    return;
                case HaltReason.Step:
                case HaltReason.Pause:
                    ctx.Send(new Packet(PacketData.StopReply(TrapSignal)));
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
                    cpu.Log(LogLevel.Noisy, "GDB CTRL-C occured - pausing CPU");
                }
                // we need to pause CPU in order to escape infinite loops
                cpu.Pause();
                cpu.ExecutionMode = ExecutionMode.SingleStep;
                cpu.Resume();
                return;
            }

            using(var ctx = commHandler.OpenContext())
            {
                if(result.CorruptedPacket)
                {
                    if(LogsEnabled)
                    {
                        cpu.Log(LogLevel.Warning, "Corrupted GDB packet received: {0}", result.Packet.Data.DataAsString);
                    }
                    // send NACK
                    ctx.Send((byte)'-');
                    return;
                }

                if(LogsEnabled)
                {
                    cpu.Log(LogLevel.Debug, "GDB packet received: {0}", result.Packet.Data.DataAsString);
                }
                // send ACK
                ctx.Send((byte)'+');

                Command command;
                if(!commands.TryGetCommand(result.Packet, out command))
                {
                    if(LogsEnabled)
                    {
                        cpu.Log(LogLevel.Warning, "Unsupported GDB command: {0}", result.Packet.Data.DataAsString);
                    }
                    ctx.Send(new Packet(PacketData.Empty));
                }
                else
                {
                    var packetData = Command.Execute(command, result.Packet);
                    // null means that we will respond later with Stop Reply Response
                    if(packetData != null)
                    {
                        ctx.Send(new Packet(packetData));
                    }
                }
            }
        }

        private readonly PacketBuilder pcktBuilder;
        private readonly ICpuSupportingGdb cpu;
        private readonly SocketServerProvider terminal;
        private readonly CommandsManager commands;
        private readonly CommunicationHandler commHandler;

        private const int TrapSignal = 5;
        private const int AbortSignal = 6;

        private class CommunicationHandler
        {
            public CommunicationHandler(GdbStub stub)
            {
                this.stub = stub;
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
                            stub.cpu.Log(LogLevel.Debug, "Gdb stub: entering nested communication context. All bytes will be queued.");
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
                            stub.cpu.Log(LogLevel.Debug, "Gdb stub: leaving nested communication context. Sending {0} queued bytes.", queue.Count);
                        }
                        SendAllBufferedData();
                    }
                }
            }

            private readonly GdbStub stub;
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
                        commHandler.stub.cpu.Log(LogLevel.Debug, "Sending response to GDB: {0}", packet.Data.DataAsString);
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

