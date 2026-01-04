//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Text;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Storage;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Storage.VirtIO;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class VirtIOMMIOConsole : VirtIOMMIO, IUART
    {
        public VirtIOMMIOConsole(IMachine machine, bool virtioConsoleFeatureMultiport = true) : base(machine)
        {
            VirtioConsoleFeatureMultiport = virtioConsoleFeatureMultiport;

            // Initialize queues for the maximum number of ports plus two additional queues for the control port
            Virtqueues = new Virtqueue[MaxNumberPorts * 2 + 2];

            for(var i = 0; i < Virtqueues.Length; i++)
            {
                Virtqueues[i] = new Virtqueue(this, 128, (ulong)i);
            }

            // Create Ports from queues

            // Queue -> Port assignment according to the specification

            // Queue | Port
            //------------------------------------------------------
            //     0 |  0
            //     1 |  0
            //     2 |  Control       Only with Multiport enabled 
            //     3 |  Control                 ...
            //     4 |  1                       ...
            //     5 |  1                       ...
            //      ...                         ...

            ports = new ConsolePort<byte>[MaxNumberPorts];

            ports[0] = new ConsolePort<byte>(Virtqueues[0], Virtqueues[1], 0);
            ports[0].Parent = this;
            ports[0].ProcessNewReceiveBuffer = ProcessReceiveQueueBuffer;
            ports[0].ProcessNewTransmitBuffer = ProcessTransmitQueueBuffer;
            ports[0].SetAdded();

            for(var i = 1UL; i < MaxNumberPorts; i++)
            {
                ports[i] = new ConsolePort<byte>(Virtqueues[(i + 1) * 2], Virtqueues[((i + 1) * 2) + 1], (short)i);
                ports[i].Parent = this;
                ports[i].ProcessNewReceiveBuffer = ProcessReceiveQueueBuffer;
                ports[i].ProcessNewTransmitBuffer = ProcessTransmitQueueBuffer;
            }

            // Create controlPorts
            controlPort = new ConsolePort<ControlMessage>(Virtqueues[2], Virtqueues[3], -1);
            controlPort.Parent = this;
            controlPort.ProcessNewReceiveBuffer = ProcessControlPortReceiveBuffer;
            controlPort.ProcessNewTransmitBuffer = ProcessControlPortTransmitBuffer;

            lastQueueIdx = (uint)Virtqueues.Length - 1;

            DefineMMIORegisters();
            DefineConsoleRegisters();

            ports[0].Console = true;
            this.Log(LogLevel.Debug, "Set port 0 to console");
        }

        // ---- Utility functions to send messages to the guest from the Renode Monitor

        public void ChangeUsedPort(ushort portID)
        {
            if(portID >= MaxNumberPorts)
            {
                throw new RecoverableException("Selected Port is out of range");
            }
            selectedPort = portID;
        }

        public void OpenPort(ushort portID, bool open)
        {
            this.Log(LogLevel.Debug, "OpenPort {0} {1}", portID, open);
            if(!driverCanReceiveControlMessages)
            {
                throw new RecoverableException("The driver can not yet receive control messages, OpenPort message not sent!");
            }
            throw new RecoverableException("Sending OpenPort messages is currently not supported");
        }

        public void MakeConsole(ushort portID)
        {
            if(!driverCanReceiveControlMessages)
            {
                throw new RecoverableException("The driver can not yet receive control messages");
            }
            // No checks because the specification does disallow setting a unspecified port as a console port
            ports[portID].Console = true;
            lock(inputLock)
            {
                controlPort.HostInput.Enqueue(new ControlMessage { Event = (ushort)ControlMessageEvent.ConsolePort, ID = (uint)portID, Value = 0 });
            }
            controlPort.ReceiveQueue.Handle();
        }

        public void AddDevice(ushort portID)
        {
            if(!driverCanReceiveControlMessages)
            {
                throw new RecoverableException("The driver can not yet receive control messages");
            }
            if(!ports[portID].IsClosed())
            {
                throw new RecoverableException($"Cannot add port {portID}, port already specified");
            }
            ports[portID].SetAdded();
            lock(inputLock)
            {
                controlPort.HostInput.Enqueue(new ControlMessage { Event = (ushort)ControlMessageEvent.DeviceAdd, ID = (uint)portID, Value = 0 });
            }
            controlPort.ReceiveQueue.Handle();
        }

        public void RemoveDevice(ushort portID)
        {
            if(!driverCanReceiveControlMessages) // No ready signal received yet
            {
                throw new RecoverableException("The driver can not yet receive control messages");
            }
            if(ports[portID].IsClosed())
            {
                throw new RecoverableException($"Cannot remove port {portID}, port not specified");
            }
            ports[portID].SetClosed();

            lock(inputLock)
            {
                controlPort.HostInput.Enqueue(new ControlMessage { Event = (ushort)ControlMessageEvent.DeviceRemove, ID = (uint)portID, Value = 0 });
            }
            controlPort.ReceiveQueue.Handle();
        }

        public void ResizeConsole(ushort port, ushort cols, ushort rows)
        {
            this.Log(LogLevel.Debug, "ResizeConsole {0} {1} {2}", port, cols, rows);
            if(!driverCanReceiveControlMessages)
            {
                throw new RecoverableException("The driver can not yet receive control messages");
            }
            if(!VirtioConsoleFeatureSize)
            {
                throw new RecoverableException("Setting the Console Size is currently not supported");
            }
        }

        public void TagPort(ushort port, string name)
        {
            this.Log(LogLevel.Debug, "TagPort {0} {1}", port, name);
            if(!driverCanReceiveControlMessages)
            {
                throw new RecoverableException("The driver can not yet receive control messages");
            }
            throw new RecoverableException("Tagging ports is currently not supported");
        }

        public void WriteChar(byte value)
        {
            this.Log(LogLevel.Noisy, "Received host input to port {0}: {1}", selectedPort, (char)value);
            var port = ports[selectedPort];
            if(port.Console == false)
            {
                throw new RecoverableException($"Currently selected port {selectedPort} is not a console port, did not write");
            }
            if(port.IsClosed())
            {
                throw new RecoverableException("Currently selected port is not open, did not write");
            }
            lock(inputLock)
            {
                this.Log(LogLevel.Noisy, "Enqueuing on port {0}", selectedPort);
                port.HostInput.Enqueue(value);
            }
            port.ReceiveQueue.Handle();
        }

        // Called by Virtqueue when new Buffer is available (Virtqueue.Handle())
        public override bool ProcessChain(Virtqueue virtq)
        {
            // Two virtqueues per port + two virtqueues for the control port
            if(virtq.ArrayIndex > (MaxNumberPorts + 1) * 2)
            {
                this.Log(LogLevel.Error, "Accessing queue {0} - out of range for {1} ports", virtq.ArrayIndex, MaxNumberPorts);
            }
            if(virtq.ArrayIndex > 1 && !VirtioConsoleFeatureMultiport)
            {
                this.Log(LogLevel.Error, "Accessing queue {0}, but VirtioConsoleFeatureMultiport is not enabled!", virtq.ArrayIndex);
            }

            var select = GetQueueType(virtq.ArrayIndex);
            if(!TryGetPortId(virtq, out var portID))
            {
                this.Log(LogLevel.Error, "Could not map QueueID {0} to a portID!", virtq.ArrayIndex);
                return false;
            }
            this.Log(LogLevel.Debug, "Mapped queue index {0} to port number {1}", virtq.ArrayIndex, portID);

            if(portID == -1)
            {
                return controlPort.QueueNotified(select);
            }

            if(!IsQueuePortUsable(virtq))
            {
                this.Log(LogLevel.Warning, "Got new buffer for queue {0} but port {1} that the queue belongs to is neither added nor open", virtq.ArrayIndex, portID);
                return false;
            }
            var port = ports[portID];
            this.Log(LogLevel.Noisy, "{0} on Port {1}", select, portID);
            return port.QueueNotified(select);
        }

        public uint BaudRate => 0;

        public Parity ParityBit => Parity.None;

        public new long Size => 0x10c;

        public Bits StopBits => Bits.None;

        public int InitialOpenPortCount { get; set; } = 1;

        public bool VirtioConsoleFeatureMultiport
        {
            get => multiportFeature;

            set
            {
                multiportFeature = value;
                BitHelper.SetBit(ref deviceFeatureBits, (byte)MMIOConsoleFeatureBits.VirtioConsoleFeatureMultiport, VirtioConsoleFeatureMultiport);
            }
        }

        // VirtIO Console Features
        // Allows the driver to read the console dimensions from additional configuration registers        
        public bool VirtioConsoleFeatureSize
        {
            get => false;
            set => throw new RecoverableException("Setting console size is not supported");
        }

        // Enables the usage of multiple ports and enables the config queues that are used to notify of additional port creation          
        // Enables the emergency write configuration register for early debug writing
        public bool VirtioConsoleFeatureEmergencyWrite
        {
            get => false;
            set => throw new RecoverableException("Emergency Write feature not supported");
        }

        public ulong MaxNumberPorts { get; set; } = 31;

        [field: Transient]
        public event Action<byte> CharReceived;

        protected void DefineConsoleRegisters()
        {
            ConsoleRegisters.Cols.Define(this)
                .WithTag("console_cols", 0, 16);

            ConsoleRegisters.Rows.Define(this)
                .WithTag("console_rows", 0, 16);

            ConsoleRegisters.MaxNrPorts.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "console_max_ports",
                    valueProviderCallback: _ =>
                    {
                        return MaxNumberPorts;
                    });

            ConsoleRegisters.EmergencyWrite.Define(this)
                .WithTag("emergency_write", 0, 32);
        }

        protected override uint DeviceID => (uint)DeviceType.Console;

        // ------ Callbacks for Receiving and Transmitting messages
        private static bool ProcessTransmitQueueBuffer(Virtqueue virtq, ConsolePort<byte> port)
        {
            var parent = port.Parent;

            virtq.ReadDescriptorMetadata();
            var length = virtq.Descriptor.Length;

            virtq.TryReadFromBuffers(length, out byte[] buf);
            var handler = parent.CharReceived;

            parent.Log(LogLevel.Noisy, "Transmitted from Port {0}, {1}", port.PortID, buf.ToLazyHexString());

            if(handler != null)
            {
                for(var i = 0; i < length; i++)
                {
                    handler((buf[i]));
                }
            }
            return true;
        }

        private static bool ProcessReceiveQueueBuffer(Virtqueue virtq, ConsolePort<byte> port)
        {
            var parent = port.Parent;
            var receiveQueue = port.ReceiveQueue;
            var portID = port.PortID;
            var hostInput = port.HostInput;

            receiveQueue.ReadDescriptorMetadata();
            var length = receiveQueue.Descriptor.Length;

            var buf = new byte[length];

            // Queue already has some characters, request can immediately be served
            if(hostInput.Count > 0)
            {
                var countWritten = 0;
                lock(parent.inputLock)
                {
                    while(hostInput.Count != 0 && countWritten < length)
                    {
                        parent.Log(LogLevel.Noisy, "Dequeuing on port {0}", portID);
                        buf[countWritten] = hostInput.Dequeue();
                        countWritten++;
                    }
                }
                if(countWritten != length)
                {
                    // Fine for now because the driver only requests one byte each time, but the Spec does not say anything about requesting bigger chunks than 1 byte, so it should be possible
                    parent.Log(LogLevel.Warning, "Not Implemented: Less characters written ({0}) then requested ({1})", countWritten, length);
                }
                receiveQueue.TryWriteToBuffers(buf);
                parent.Log(LogLevel.Noisy, "Port {0}: RV", portID);

                return true;
            } // Nothing in queue to be immediately sent
            return false;
        }

        private static bool ProcessControlPortTransmitBuffer(Virtqueue virtq, ConsolePort<ControlMessage> port)
        {
            var parent = port.Parent;
            // According to the linux headers, the control message is always 8 bytes
            // However the driver in zephyr always sents a 24 Byte buffer to accomodate space for name tagging of a port
            var length = 8;

            virtq.TryReadFromBuffers(length, out byte[] buf);

            if(length < System.Runtime.InteropServices.Marshal.SizeOf(typeof(ControlMessage)) / 8)
            {
                parent.Log(LogLevel.Error, "Insufficient Control Message size: {0}", length);
                return false;
            }
            ControlMessage msg = Packet.Decode<ControlMessage>(buf);
            var eventId = (ControlMessageEvent)msg.Event;
            parent.Log(LogLevel.Debug, "Port {0}: Transmit Id {1} Event {2} Val {3}", port.PortID, msg.ID, eventId, msg.Value);
            parent.ProcessControlMessage(msg);

            return true;
        }

        private static bool ProcessControlPortReceiveBuffer(Virtqueue virtq, ConsolePort<ControlMessage> port)
        {
            var parent = port.Parent;
            var hostInput = port.HostInput;

            lock(parent.inputLock)
            {
                if(hostInput.Count > 0)
                {
                    var message = hostInput.Dequeue();
                    var buf = Packet.Encode<ControlMessage>(message);
                    port.ReceiveQueue.TryWriteToBuffers(buf);
                    var eventId = (ControlMessageEvent) message.Event;
                    parent.Log(LogLevel.Debug, "Port {0}: Receive Id {1} Event {2} Val {3}", port.PortID, message.ID, eventId, message.Value);
                    return true;
                }
            }
            return false;
        }

        // Convenience Debugging function to write strings to the Analyzer 
        private void WriteToAnalyzer(string message)
        {
            this.Log(LogLevel.Debug, "Writing back to analyzer");
            var handler = CharReceived;
            if(handler == null)
            {
                this.Log(LogLevel.Warning, "Analyzer Backend not set");
                return;
            }
            var bytes = Encoding.UTF8.GetBytes(message);

            foreach(var b in bytes)
            {
                handler(b);
            }
        }

        // Returns the port the given queue belongs to in the portID argument
        // Returning false indicates that the mapping was not possible because the queue number was out of range
        private bool TryGetPortId(Virtqueue queue, out short portID)
        {
            var queueIdx = queue.ArrayIndex;

            portID = 0;

            switch(queueIdx / 2)
            {
            case 0:
                return true;
            case 1:
                portID = -1;
                return true;
            default:
                if(queueIdx / 2 <= MaxNumberPorts)
                {
                    portID = (short)((queueIdx / 2) - 1);
                    return true;
                }
                return false;
            }
        }

        // Returns QueueSelect.Receive if the queue is a receivequeue (odd indexes), and QueueSelect.Transmit if the queue is a transmitqueue (even indexes)
        private QueueSelect GetQueueType(ulong queueindex)
        {
            return (queueindex % 2) == 0 ? QueueSelect.Receive : QueueSelect.Transmit;
        }

        private bool IsQueuePortUsable(Virtqueue virtq)
        {
            var success = TryGetPortId(virtq, out var portID);
            if(!success)
            {
                return false;
            }
            if(portID == -1)
            {
                return true;
            }
            var port = ports[portID];
            return !port.IsClosed();
        }

        private void PrintControlMessage(ControlMessage message)
        {
            this.Log(LogLevel.Debug, "Controllmessage \nPort {0}\nEvent {1}\nValue {2}\n", message.ID, Convert.ToString(message.Event, 10), Convert.ToString(message.Value, 16));
        }

        private void ProcessControlMessage(ControlMessage msg)
        {
            switch((ControlMessageEvent)msg.Event)
            {
            case ControlMessageEvent.DeviceReady:
                driverCanReceiveControlMessages = msg.Value == 1 ? true : false;
                // Send ADD PORT control messages for all ports // Exclude port 0
                for(int i = 0; i < InitialOpenPortCount; i++)
                {
                    lock(inputLock)
                    {
                        controlPort.HostInput.Enqueue(new ControlMessage { Event = (ushort)ControlMessageEvent.DeviceAdd, ID = (uint)i, Value = 1 });
                    }
                    ports[i].SetAdded();
                }
                controlPort.ReceiveQueue.Handle();
                break;
            case ControlMessageEvent.PortReady:
                if(msg.Value == 1 && ports[msg.ID].IsAdded())
                {
                    ports[msg.ID].SetReady();
                }
                else
                {
                    this.Log(LogLevel.Warning, "Driver transmitted a PortReady message for Port {0}, but port was not previously added", msg.ID);
                }

                if(!initialConsoleSet)
                {
                    this.Log(LogLevel.Debug, "Set port {0} to console", msg.ID);
                    initialConsoleSet = true;
                    ports[0].Console = true;
                    lock(inputLock)
                    {
                        controlPort.HostInput.Enqueue(new ControlMessage { Event = (ushort)ControlMessageEvent.ConsolePort, ID = (uint)0, Value = 0 });
                    }
                    controlPort.ReceiveQueue.Handle();
                }
                break;
            case ControlMessageEvent.PortOpen:
                ports[msg.ID].SetReady();
                // Reflect back portOpen message as ACK - Not specified but Qemu does it
                lock(inputLock)
                {
                    controlPort.HostInput.Enqueue(new ControlMessage { Event = (ushort)ControlMessageEvent.PortOpen, ID = (uint)msg.ID, Value = 1 });
                }
                controlPort.ReceiveQueue.Handle();
                break;
            default:
                this.Log(LogLevel.Error, "Unhandled Control Message: Port {0} Event {1} Value {2}", msg.ID, msg.Event, msg.Value);
                break;
            }
        }

        private bool driverCanReceiveControlMessages;

        private bool multiportFeature = true;
        private ushort selectedPort = 0;
        private bool initialConsoleSet = false;
        private readonly ConsolePort<byte>[] ports;
        private readonly ConsolePort<ControlMessage> controlPort;
        private readonly object inputLock = new object();

        private class ConsolePort<T>
        {
            public ConsolePort(Virtqueue rx, Virtqueue tx, short id)
            {
                this.ReceiveQueue = rx;
                this.TransmitQueue = tx;
                this.PortID = id;
            }

            public bool QueueNotified(QueueSelect sel)
            {
                if(sel == QueueSelect.Receive)
                {
                    Parent.Log(LogLevel.Debug, "New Receive Buffer on Port {0}", PortID);
                    if(ProcessNewReceiveBuffer == null)
                    {
                        Parent.Log(LogLevel.Error, "No ReceiveCallback Handler registered to port {0}", PortID);
                        return false;
                    }
                    return ProcessNewReceiveBuffer(ReceiveQueue, this);
                }
                else
                {
                    Parent.Log(LogLevel.Debug, "New Transmit Buffer on Port {0}", PortID);
                    if(ProcessNewTransmitBuffer == null)
                    {
                        Parent.Log(LogLevel.Error, "No TransmitCallback Handler registered to port {0}", PortID);
                        return false;
                    }
                    return ProcessNewTransmitBuffer(TransmitQueue, this);
                }
            }

            // Convenience functions because   ports[msg.ID].State = ConsolePort<byte>.PortState.PortReady; was too cumbersome
            public void SetClosed()
            {
                this.State = PortState.PortClosed;
            }

            public void SetAdded()
            {
                this.State = PortState.PortAdded;
            }

            public void SetReady()
            {
                this.State = PortState.PortReady;
            }

            public bool IsAdded()
            {
                return this.State == PortState.PortAdded;
            }

            public bool IsReady()
            {
                return this.State == PortState.PortReady;
            }

            public bool IsClosed()
            {
                return this.State == PortState.PortClosed;
            }

            public bool Console { get; set; }

            public Func<Virtqueue, ConsolePort<T>, bool> ProcessNewReceiveBuffer;
            public Func<Virtqueue, ConsolePort<T>, bool> ProcessNewTransmitBuffer;

            public VirtIOMMIOConsole Parent; // For Debugging
            public Virtqueue ReceiveQueue;
            public Virtqueue TransmitQueue;

            public Queue<T> HostInput = new Queue<T>();
            public PortState State = PortState.PortClosed;

            public readonly short PortID;

            public enum PortState
            {
                PortClosed,
                PortAdded,
                PortReady
            }
        }

        [LeastSignificantByteFirst]
        private struct ControlMessage
        {
            [PacketField, Width(bits: 32)]
            public uint ID;
            [PacketField, Offset(doubleWords: 1), Width(bits: 16)]
            public ushort Event;
            [PacketField, Offset(doubleWords: 1, bits: 16), Width(bits: 16)]
            public ushort Value;
        }

        private enum QueueSelect
        {
            Receive,
            Transmit
        }

        [Flags]
        private enum MMIOConsoleFeatureBits : byte
        {
            // Specific Flags for the Console Device
            VirtioConsoleFeatureSize = 0,         // Configuration cols and rows are valid
            VirtioConsoleFeatureMultiport = 1,    // Device has support for multiple port - config field max_nr_ports and control_virtqueues will be used
            VirtioConsoleFeatureEmergencyWrite = 2    // Support for emergency write - config field emerg_wr is valid
        }

        private enum ConsoleRegisters : long
        {
            Cols = 0x100,
            Rows = 0x102,
            MaxNrPorts = 0x104,
            EmergencyWrite = 0x108
        }

        private enum ControlMessageEvent : ushort
        {
            DeviceReady = 0,
            DeviceAdd = 1,
            DeviceRemove = 2,
            PortReady = 3,
            ConsolePort = 4,
            ConsoleResize = 5,
            PortOpen = 6,
            PortName = 7
        }
    }
}
