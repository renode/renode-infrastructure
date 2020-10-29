//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class NRF52840_SPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public NRF52840_SPI(Machine machine) : base(machine)
        {
            IRQ = new GPIO();

            receiveFifo = new Queue<byte>();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            receiveFifo.Clear();
            enabled = false;
            RegistersCollection.Reset();
            UpdateInterrupts();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.PendingInterrupt.Define(this)
                .WithFlag(0, out readyPending, name: "EVENTS_READY")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.EnableInterrupt.Define(this)
                .WithReservedBits(0, 2)
                .WithFlag(2, out readyEnabled, FieldMode.Read | FieldMode.Set, name: "READY")
                .WithReservedBits(3, 29)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.DisableInterrupt.Define(this)
                .WithReservedBits(0, 2)
                .WithFlag(2, name: "READY",
                    valueProviderCallback: _ => readyEnabled.Value,
                    writeCallback: (_, val) => { if(val) readyEnabled.Value = false; })
                .WithReservedBits(3, 29)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.Enable.Define(this)
                .WithValueField(0, 4, 
                    valueProviderCallback: _ => enabled ? 1 : 0u,
                    writeCallback: (_, val) =>
                    {
                        switch(val)
                        {
                            case 0:
                                // disabled
                                enabled = false;
                                break;

                            case 1:
                                // enabled
                                enabled = true;
                                break;

                            default:
                                this.Log(LogLevel.Warning, "Unhandled enable value: 0x{0:X}. Expected 0x0 (disable SPI) or 0x1 (enable SPI)", val);
                                return;
                        }

                        UpdateInterrupts();
                    })
                .WithReservedBits(4, 28)
            ;

            Registers.TransmitBuffer.Define(this)
                // the documentation says this field is readable, so we use the automatic
                // underlying backing field to return the previously written value
                .WithValueField(0, 8, name: "TXD", writeCallback: (_, val) => SendData((byte)val))
                .WithReservedBits(8, 24)
            ;

            Registers.ReceiveBuffer.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "RXD",
                    valueProviderCallback: _ =>
                    {
                        if(receiveFifo.Count == 0)
                        {
                            this.Log(LogLevel.Warning, "Tried to read from an empty buffer");
                            return 0;
                        }

                        lock(receiveFifo)
                        {
                            var result = receiveFifo.Dequeue();

                            // some new byte moved to the head
                            // of the queue - let's generate the
                            // READY event
                            if(receiveFifo.Count > 0)
                            {
                                readyPending.Value = true;
                                UpdateInterrupts();
                            }
                            return result;
                        }
                    })
                .WithReservedBits(8, 24)
            ;
        }

        private void SendData(byte b)
        {
            if(!enabled)
            {
                this.Log(LogLevel.Warning, "Trying to send data, but the controller is disabled");
                return;
            }

            if(RegisteredPeripheral == null)
            {
                this.Log(LogLevel.Warning, "No device connected");
                return;
            }

            if(receiveFifo.Count == ReceiveBufferSize)
            {
                this.Log(LogLevel.Warning, "Buffers full, ignoring data");
                return;
            }

            // there is no need to queue transmitted bytes - let's send them right away
            var result = RegisteredPeripheral.Transmit(b);
            lock(receiveFifo)
            {
                receiveFifo.Enqueue(result);

                // the READY event is generated
                // only when the head
                // of the queue changes
                if(receiveFifo.Count == 1)
                {
                    readyPending.Value = true;
                    UpdateInterrupts();
                }
            }
        }

        // RXD is double buffered
        private const int ReceiveBufferSize = 2;

        private void UpdateInterrupts()
        {
            var status = readyEnabled.Value && readyPending.Value;
            status &= enabled;

            this.Log(LogLevel.Noisy, "Setting IRQ to {0}", status);
            IRQ.Set(status);
        }

        private IFlagRegisterField readyPending;
        private IFlagRegisterField readyEnabled;
        private bool enabled;

        private readonly Queue<byte> receiveFifo;

        private enum Registers
        {
            PendingInterrupt = 0x108,
            EnableInterrupt = 0x304,
            DisableInterrupt = 0x308,
            Enable = 0x500,
            PinSelectSCK = 0x508,
            PinSelectMOSI = 0x50C,
            PinSelectMISO = 0x510,
            ReceiveBuffer = 0x518,
            TransmitBuffer = 0x51C,
            Frequency = 0x524,
            Configuration = 0x554
        }
    }
}
