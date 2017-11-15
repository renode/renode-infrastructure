//
// Copyright (c) 2010-2017 Antmicro
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

            pcktBuilder = new PacketBuilder();
            commands = new CommandsManager(cpu);
            commands.ShouldAutoStart = autostartEmulation;
            TypeManager.Instance.AutoLoadedType += commands.Register;

            terminal = new SocketServerProvider();
            terminal.DataReceived += OnByteWritten;
            terminal.ConnectionAccepted += delegate
            {
                cpu.Halted += OnHalted;
                cpu.ExecutionMode = ExecutionMode.SingleStep;
            };
            terminal.ConnectionClosed += delegate
            {
                cpu.Halted -= OnHalted;
                cpu.ExecutionMode = ExecutionMode.Continuous;
            }; 
            terminal.Start(port);
            commHandler = new CommunicationHandler(this);
        }

        public void Dispose()
        {
            cpu.Halted -= OnHalted;
            terminal.Dispose();
        }

        public int Port { get; private set; }

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
                        beforeCommand += cmd =>
                        {
                            commandsCounter++;
                            if(commandsCounter > 15)
                            {
                                // this is a hack!
                                // I noticed that GDB will send `step` command after receiving
                                // information about watchpoint being hit.
                                // As a result cpu would execute next instruction and stop again.
                                // To prevent this situation we wait for `step` and ignore it, but
                                // only in small time window (15 - instructions, value choosen at random)
                                // and only after sending watchpoint-related stop reply.
                                this.Log(LogLevel.Error, "Expected step command after watchpoint. Further debugging might not work properly");
                                beforeCommand = null;
                                commandsCounter = 0;
                                return false;
                            }
                            if((cmd is SingleStepCommand))
                            {
                                using(var innerCtx = commHandler.OpenContext())
                                {
                                    innerCtx.Send(new Packet(PacketData.StopReply(TrapSignal)));
                                }   
                                beforeCommand = null;
                                commandsCounter = 0;
                                return true;
                            }
                            return false;
                        };
                        goto case BreakpointType.HardwareBreakpoint;
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
                cpu.Log(LogLevel.Noisy, "GDB CTRL-C occured - pausing CPU");
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
                    cpu.Log(LogLevel.Warning, "Corrupted GDB packet received: {0}", result.Packet.Data.DataAsString);
                    // send NACK
                    ctx.Send((byte)'-');
                    return;
                }

                cpu.Log(LogLevel.Noisy, "GDB packet received: {0}", result.Packet.Data.DataAsString);
                // send ACK
                ctx.Send((byte)'+');

                Command command;
                if(!commands.TryGetCommand(result.Packet, out command))
                {
                    cpu.Log(LogLevel.Warning, "Unsupported GDB command: {0}", result.Packet.Data.DataAsString);
                    ctx.Send(new Packet(PacketData.Empty));
                }
                else
                {
                    var before = beforeCommand;
                    if(before != null && before(command))
                    {
                        return;
                    }
                    var packetData = Command.Execute(command, result.Packet);
                    // null means that we will respond later with Stop Reply Response
                    if(packetData != null)
                    {
                        ctx.Send(new Packet(packetData));
                    }
                }
            }
        }

        private int commandsCounter;
        private Func<Command, bool> beforeCommand;

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
                return new Context(this);
            }

            private readonly GdbStub stub;
            private readonly Queue<byte> queue;
            private readonly object internalLock;
            private int counter;

            public class Context : IDisposable
            {
                public Context(CommunicationHandler commHandler)
                {
                    this.commHandler = commHandler;
                    Monitor.Enter(commHandler.internalLock);
                    commHandler.counter++;
                    if(commHandler.counter > 1)
                    {
                        commHandler.stub.cpu.Log(LogLevel.Debug, "Gdb stub: entering nested communication context. All bytes will be queued.");
                    }
                }

                public void Dispose()
                {
                    commHandler.counter--;
                    if(commHandler.counter == 0)
                    {
                        if(commHandler.queue.Count > 0)
                        {
                            commHandler.stub.cpu.Log(LogLevel.Debug, "Gdb stub: leaving nested communication context. Sending {0} queued bytes.", commHandler.queue.Count);
                        }
                        foreach(var b in commHandler.queue)
                        {
                            commHandler.stub.terminal.SendByte(b);
                        }
                        commHandler.queue.Clear();
                    }
                    Monitor.Exit(commHandler.internalLock);
                }

                public void Send(Packet packet)
                {
                    commHandler.stub.cpu.Log(LogLevel.Debug, "Sending response to GDB: {0}", packet.Data.DataAsString);
                    foreach(var b in packet.GetCompletePacket())
                    {
                        Send(b);
                    }
                }

                public void Send(byte b)
                {
                    if(commHandler.counter == 1)
                    {
                        commHandler.stub.terminal.SendByte(b);
                    }
                    else
                    {
                        commHandler.queue.Enqueue(b);
                    }
                }

                private readonly CommunicationHandler commHandler;
            }
        }
    }
}

