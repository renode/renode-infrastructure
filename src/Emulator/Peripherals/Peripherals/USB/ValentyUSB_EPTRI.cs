//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.USB
{
    public class ValentyUSB_EPTRI : BasicDoubleWordPeripheral, IUSBDevice, IKnownSize, IDisposable
    {
        public ValentyUSB_EPTRI(Machine machine, int maximumPacketSize = 64) : base(machine)
        {
            maxPacketSize = maximumPacketSize;
            USBCore = new USBDeviceCore(this, customSetupPacketHandler: SetupPacketHandler);
        }

        public override void Reset()
        {
            base.Reset();

            slaveToMasterBufferVirtualBase = 0;

            outPacketReady.Reset();

            setupPacketBuffer.Clear();
            masterToSlaveBuffer.Clear();
            masterToSlaveWaitingBuffer.Clear();
            slaveToMasterBuffer.Clear();
        }

        public void Dispose()
        {
            // this is to unblock `SetupPacketHandler`
            outPacketReady.Set();
        }

        public long Size => 0x100;

        public USBDeviceCore USBCore { get; }

        public GPIO IRQ { get; } = new GPIO();

        protected override void DefineRegisters()
        {
            Registers.SetupData.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "data",
                    valueProviderCallback: _ =>
                    {
                        if(setupPacketBuffer.Count == 0)
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an empty setup data queue");
                            return 0u;
                        }

                        var result = setupPacketBuffer.Peek();
                        this.Log(LogLevel.Noisy, "Reading byte from setup data buffer: 0x{0:X}. Bytes left: {1}", result, setupPacketBuffer.Count);
                        return result;
                    })
            ;

            Registers.SetupStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "have", valueProviderCallback: _ => setupPacketBuffer.Any())
                .WithFlag(1, FieldMode.Read, name: "is_in")
                .WithValueField(2, 4, FieldMode.Read, name: "epno")
                .WithFlag(6, FieldMode.Read, name: "pend")
                .WithFlag(7, FieldMode.Read, name: "data")
            ;

            Registers.SetupControl.Define(this)
                .WithFlag(0, name: "advance", writeCallback: (_, val) => setupPacketBuffer.TryDequeue(out var _))
                .WithFlag(1, name: "handled") // Write a `1` here to indicate SETUP has been handled.
                .WithFlag(2, name: "reset")
            ;

            Registers.SetupEventPending.Define(this)
                .WithFlag(0, out setupEventPendingField, FieldMode.Read | FieldMode.WriteOneToClear, name: "setup_ev_pending")
                .WithWriteCallback((_, val) => UpdateInterrupts())
            ;

            Registers.SetupEventEnable.Define(this)
                .WithFlag(0, out setupEventEnabledField, name: "setup_ev_enable")
                .WithWriteCallback((_, val) => UpdateInterrupts())
            ;

            Registers.InEventPending.Define(this)
                .WithFlag(0, out inEventPendingField, FieldMode.Read | FieldMode.WriteOneToClear, name: "in_ev_pending")
                .WithWriteCallback((_, val) => UpdateInterrupts())
            ;

            Registers.InEventEnable.Define(this)
                .WithFlag(0, out inEventEnabledField, name: "in_ev_enable")
                .WithWriteCallback((_, val) => UpdateInterrupts())
            ;

            Registers.OutEventPending.Define(this)
                .WithFlag(0, out outEventPendingField, FieldMode.Read | FieldMode.WriteOneToClear, name: "out_ev_pending")
                .WithWriteCallback((_, val) => UpdateInterrupts())
            ;

            Registers.OutEventEnable.Define(this)
                .WithFlag(0, out outEventEnabledField, name: "out_ev_enable")
                .WithWriteCallback((_, val) => UpdateInterrupts())
            ;

            Registers.InStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "have")
                .WithFlag(1, FieldMode.Read, name: "idle", valueProviderCallback: _ => true) // TODO:?!
                .WithFlag(7, FieldMode.Read, name: "pend")
            ;

            Registers.OutStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "have", valueProviderCallback: _ => masterToSlaveBuffer.Any())
                .WithFlag(1, FieldMode.Read, name: "idle", valueProviderCallback: _ => true) // `1` if the packet has finished receiving; we receive the whole packet at once, so this one is always true
                .WithValueField(2, 4, FieldMode.Read, name: "epno") // TODO: this supports EP0 for now only
                .WithFlag(6, FieldMode.Read, name: "pend", valueProviderCallback: _ => outEventPendingField.Value)
            ;

            Registers.OutData.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "data",
                    valueProviderCallback: _ =>
                    {
                        if(masterToSlaveBuffer.Count == 0)
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an empty OUT data queue");
                            return 0u;
                        }

                        var result = masterToSlaveBuffer.Peek();
                        this.Log(LogLevel.Noisy, "Reading byte from OUT buffer: 0x{0:X}. Bytes left: {1}", result, masterToSlaveBuffer.Count);
                        return result;
                    })
            ;

            Registers.OutControl.Define(this)
                .WithFlag(0, name: "advance", writeCallback: (_, val) => masterToSlaveBuffer.TryDequeue(out var _))
                .WithFlag(1, out outEnableField, name: "enable", writeCallback: (_, val) =>
                {
                    lock(masterToSlaveBuffer)
                    {
                        if(val)
                        {
                            // setting enable to true means that the device
                            // is ready for the data from master
                            PrepareDataFromMaster();
                        }
                    }
                })
                .WithFlag(2, name: "reset")
            ;

            Registers.InData.Define(this)
                .WithValueField(0, 8, name: "data",
                    valueProviderCallback: _ =>
                    {
                        if(slaveToMasterBuffer.Count == 0)
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an empty IN data queue");
                            return 0u;
                        }

                        var result = slaveToMasterBuffer.Peek();
                        this.Log(LogLevel.Noisy, "Reading byte from IN buffer: 0x{0:X}. Bytes left: {1}", result, slaveToMasterBuffer.Count);
                        return result;
                    },
                    writeCallback: (_, val) => slaveToMasterBuffer.Enqueue((byte)val))
            ;

            Registers.InControl.Define(this)
                .WithValueField(0, 4, name: "ep")
                .WithFlag(4, out var stallField, name: "stall")
                .WithFlag(5, name: "reset")
                .WithWriteCallback((_, __) =>
                {
                    if(stallField.Value)
                    {
                        HandleStall();
                    }
                    else
                    {
                        // writing EP field indicates that the data in buffer is ready
                        // we need to use global write callback to detect writing
                        // a value of 0 (as it would not trigger field-level callbacks)
                        ProduceDataToMaster();
                    }
                })
            ;

        }

        private void HandleStall()
        {
            this.Log(LogLevel.Debug, "Endpoint 0 stalled");
            outPacketReady.Set();
        }

        private void ProduceDataToMaster()
        {
            var chunkSize = slaveToMasterBuffer.Count - slaveToMasterBufferVirtualBase;
            slaveToMasterBufferVirtualBase = slaveToMasterBuffer.Count;

            if(chunkSize < maxPacketSize)
            {
                this.Log(LogLevel.Noisy, "Data chunk was shorter than max packet size (0x{0:X} vs 0x{1:X}), so this is the end of data", chunkSize, maxPacketSize);
                outPacketReady.Set();
            }
            else
            {
                // IN packet pending means that the master is waiting for more data
                // and slave should generate it
                inEventPendingField.Value = true;
                UpdateInterrupts();
            }
        }

        private void PrepareDataFromMaster()
        {
            if(masterToSlaveWaitingBuffer.Count == 0)
            {
                return;
            }

            var chunk = masterToSlaveWaitingBuffer.DequeueRange(maxPacketSize);
            this.Log(LogLevel.Noisy, "Enqueuing chunk of additional data from master of size {0}", chunk.Length);
            EnqueueDataFromMaster(chunk);

            outEnableField.Value = false;
        }

        private void EnqueueSetupFromMaster(IEnumerable<byte> data)
        {
            setupPacketBuffer.EnqueueRange(data);

            // fake 16-bit CRC
            setupPacketBuffer.Enqueue(0);
            setupPacketBuffer.Enqueue(0);

            setupEventPendingField.Value = true;
            UpdateInterrupts();
        }

        private void EnqueueDataFromMaster(IEnumerable<byte> data)
        {
            lock(masterToSlaveBuffer)
            {
                masterToSlaveBuffer.EnqueueRange(data);

                // fake 16-bit CRC
                masterToSlaveBuffer.Enqueue(0);
                masterToSlaveBuffer.Enqueue(0);

                outEventPendingField.Value = true;
                UpdateInterrupts();
            }
        }

        private void UpdateInterrupts()
        {
            var irqState = (setupEventPendingField.Value && setupEventEnabledField.Value)
                || (inEventPendingField.Value && inEventEnabledField.Value)
                || (outEventPendingField.Value && outEventEnabledField.Value);

            IRQ.Set(irqState);
        }

        private byte[] SetupPacketHandler(SetupPacket packet, byte[] additionalData)
        {
            this.Log(LogLevel.Noisy, "Received setup packet: {0}", packet.ToString());

            outPacketReady.Reset();
            setupPacketBuffer.Clear();

            var packetBytes = Packet.Encode(packet);
#if DEBUG_PACKETS
            this.Log(LogLevel.Noisy, "Setup packet bytes: [{0}]", Misc.PrettyPrintCollection(packetBytes, b => b.ToString("X")));
#endif
            EnqueueSetupFromMaster(packetBytes);

            if(additionalData != null)
            {
                lock(masterToSlaveBuffer)
                {
                    masterToSlaveWaitingBuffer.EnqueueRange(additionalData);
                    if(outEnableField.Value)
                    {
                        PrepareDataFromMaster();
                    }
                }
            }

            outPacketReady.WaitOne();

            if(masterToSlaveBuffer.Count != 0)
            {
                this.Log(LogLevel.Warning, "Setup packet handling finished, but there is still some unhandled additional data left. Dropping it, but expect problems");
                masterToSlaveBuffer.Clear();
            }

            this.Log(LogLevel.Noisy, "Setup packet handled");
#if DEBUG_PACKETS
            this.Log(LogLevel.Noisy, "Response bytes: [{0}]", Misc.PrettyPrintCollection(slaveToMasterBuffer, b => b.ToString("X")));
#endif
            var result = slaveToMasterBuffer.DequeueAll();
            slaveToMasterBufferVirtualBase = 0;

            inEventPendingField.Value = true;
            UpdateInterrupts();

            return result;
        }

        private IFlagRegisterField setupEventPendingField;
        private IFlagRegisterField setupEventEnabledField;
        private IFlagRegisterField inEventPendingField;
        private IFlagRegisterField inEventEnabledField;
        private IFlagRegisterField outEventPendingField;
        private IFlagRegisterField outEventEnabledField;
        private IFlagRegisterField outEnableField;

        private int slaveToMasterBufferVirtualBase;
        private readonly int maxPacketSize;
        private readonly ManualResetEvent outPacketReady = new ManualResetEvent(false);

        private readonly Queue<byte> setupPacketBuffer = new Queue<byte>();
        private readonly Queue<byte> masterToSlaveBuffer = new Queue<byte>();
        private readonly Queue<byte> masterToSlaveWaitingBuffer = new Queue<byte>();
        private readonly Queue<byte> slaveToMasterBuffer = new Queue<byte>();

        private enum Registers
        {
            PullupOut = 0x0, // RW1
            SetupData = 0x4, // R1
            SetupControl = 0x8, // RW1
            SetupStatus = 0xC, // R1
            SetupEventStatus = 0x10, // RW1
            SetupEventPending = 0x14, // RW1
            SetupEventEnable = 0x18, // RW1

            InData = 0x1C, //RW1
            InStatus = 0x20, //R1
            InControl = 0x24, //RW1
            InEventStatus = 0x28, //RW1
            InEventPending = 0x2C, //RW1
            InEventEnable = 0x30, // RW1

            OutData = 0x34, //R1
            OutStatus = 0x38, //R1
            OutControl = 0x3C,// RW1
            OutStall = 0x40, //RW1
            OutEventStatus = 0x44, //RW1
            OutEventPending = 0x48, //RW1
            OutEventEnable = 0x4C, //RW1
            Address = 0x50, //RW1
        }
    }
}
