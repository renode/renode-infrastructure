//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class NRF52840_I2C : SimpleContainer<II2CPeripheral>, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public NRF52840_I2C(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();

            slaveToMasterBuffer = new Queue<byte>();
            masterToSlaveBuffer = new Queue<byte>();

            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public override void Reset()
        {
            slaveToMasterBuffer.Clear();
            masterToSlaveBuffer.Clear();

            selectedSlave = null;
            enabled = false;
            transmissionInProgress = false;

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

        public long Size => 0x1000;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.StartReceiving.Define(this)
                .WithFlag(0, FieldMode.Write, name: "TASKS_STARTRX", writeCallback: (_, val) =>
                {
                    if(!val)
                    {
                        return;
                    }

                    transmissionInProgress = true;
                    // send what is buffered as this might be a repeated start condition
                    TrySendDataToSlave();
                    // prepare to receive data from slave
                    slaveToMasterBuffer.Clear();
                    // try read the response
                    TryFillReceivedBuffer(true);
                })
                .WithReservedBits(1, 31)
            ;

            Registers.StartTransmitting.Define(this)
                .WithFlag(0, FieldMode.Write, name: "TASKS_STARTTX", writeCallback: (_, val) =>
                {
                    if(!val)
                    {
                        return;
                    }

                    transmissionInProgress = true;
                    // send what is buffered as this might be a repeated start condition
                    TrySendDataToSlave();
                    // prepare to receive data from slave
                    slaveToMasterBuffer.Clear();
                    // wait for writing bytes to TransferBuffer...
                })
                .WithReservedBits(1, 31)
            ;

            Registers.StopTransmitting.Define(this)
                .WithFlag(0, FieldMode.Write, name: "TASKS_STOP", writeCallback: (_, val) =>
                {
                    if(!val)
                    {
                        return;
                    }

                    StopTransmission();
                })
                .WithReservedBits(1, 31)
            ;

            Registers.ResumeReceiving.Define(this)
                .WithFlag(0, FieldMode.Write, name: "TASKS_RESUME", writeCallback: (_, val) =>
                {
                    if(!val)
                    {
                        return;
                    }

                    if(!transmissionInProgress)
                    {
                        return;
                    }

                    TryFillReceivedBuffer(true);
                })
                .WithReservedBits(1, 31)
            ;

            Registers.StoppedInterruptPending.Define(this)
                .WithFlag(0, out stoppedInterruptPending, name: "EVENTS_STOPPED")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.RxInterruptPending.Define(this)
                .WithFlag(0, out rxInterruptPending, name: "EVENTS_RXREADY")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.TxInterruptPending.Define(this)
                .WithFlag(0, out txInterruptPending, name: "EVENTS_TXDSENT")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.ErrorInterruptPending.Define(this)
                .WithFlag(0, out errorInterruptPending, name: "EVENTS_ERROR")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.ErrorSource.Define(this)
                .WithTaggedFlag("OVERRUN", 0)
                .WithFlag(1, out addressNackError, name: "ANACK")
                .WithTaggedFlag("DNACK", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.Shortcuts.Define(this)
                .WithTag("BB_SUSPEND", 0, 1)
                .WithFlag(1, out byteBoundaryStopShortcut, name: "BB_STOP")
                .WithReservedBits(2, 30)
            ;

            Registers.SetEnableInterrupts.Define(this)
                .WithReservedBits(0, 1)
                .WithFlag(1, out stoppedInterruptEnabled, FieldMode.Read | FieldMode.Set, name: "STOPPED")
                .WithFlag(2, out rxInterruptEnabled, FieldMode.Read | FieldMode.Set, name: "RXREADY")
                .WithReservedBits(3, 4)
                .WithFlag(7, out txInterruptEnabled, FieldMode.Read | FieldMode.Set, name: "TXDSENT")
                .WithReservedBits(8, 1)
                .WithFlag(9, out errorInterruptEnabled, FieldMode.Read | FieldMode.Set, name: "ERROR")
                .WithReservedBits(10, 4)
                .WithFlag(14, name: "BB") // this is a flag to limit warnings, we don't support the byte-boundary interrupt
                .WithReservedBits(15, 3)
                .WithFlag(18, name: "SUSPENDED") // this is a flag to limit warnings, we don't support the suspended interrupt
                .WithReservedBits(19, 13)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.ClearEnableInterrupts.Define(this)
                .WithReservedBits(0, 1)
                .WithFlag(1, name: "STOPPED",
                    writeCallback: (_, val) => { if(val) stoppedInterruptEnabled.Value = false; },
                    valueProviderCallback: _ => stoppedInterruptEnabled.Value)
                .WithFlag(2, name: "RXREADY",
                    writeCallback: (_, val) => { if(val) rxInterruptEnabled.Value = false; },
                    valueProviderCallback: _ => rxInterruptEnabled.Value)
                .WithReservedBits(3, 4)
                .WithFlag(7, name: "TXDSENT",
                    writeCallback: (_, val) => { if(val) txInterruptEnabled.Value = false; },
                    valueProviderCallback: _ => txInterruptEnabled.Value)
                .WithReservedBits(8, 1)
                .WithFlag(9, name: "ERROR",
                    writeCallback: (_, val) => { if(val) errorInterruptEnabled.Value = false; },
                    valueProviderCallback: _ => errorInterruptEnabled.Value)
                .WithReservedBits(10, 4)
                .WithFlag(14, name: "BB") // this is a flag to limit warnings, we don't support the byte-boundary interrupt
                .WithReservedBits(15, 3)
                .WithFlag(18, name: "SUSPENDED") // this is a flag to limit warnings, we don't support the suspended interrupt
                .WithReservedBits(19, 13)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.Enable.Define(this)
                .WithValueField(0, 4, writeCallback: (_, val) =>
                {
                    switch(val)
                    {
                        case 0:
                            enabled = false;
                            break;

                        case 5:
                            enabled = true;
                            break;

                        default:
                            this.Log(LogLevel.Warning, "Wrong enabled value");
                            break;
                    }
                })
                .WithReservedBits(4, 28)
            ;

            Registers.ReceiveBuffer.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ =>
                {
                    if(!TryReadFromSlave(out var result))
                    {
                        this.Log(LogLevel.Warning, "Trying to read from an empty fifo");
                        result = 0;
                    }

                    if(byteBoundaryStopShortcut.Value)
                    {
                        StopTransmission();
                    }

                    return result;
                })
                .WithReservedBits(8, 24)
            ;

            Registers.TransferBuffer.Define(this)
                .WithValueField(0, 8, writeCallback: (_, val) =>
                {
                    if(selectedSlave == null)
                    {
                        this.Log(LogLevel.Warning, "No slave is currently attached at selected address 0x{0:X}", address.Value);
                        addressNackError.Value = true;
                        errorInterruptPending.Value = true;
                        UpdateInterrupts();
                        return;
                    }

                    this.Log(LogLevel.Noisy, "Enqueuing byte 0x{0:X}", val);
                    masterToSlaveBuffer.Enqueue((byte)val);

                    txInterruptPending.Value = true;
                    UpdateInterrupts();
                })
                .WithReservedBits(8, 24)
            ;

            Registers.Address.Define(this)
                .WithValueField(0, 7, out address, writeCallback: (_, val) =>
                {
                    if(!TryGetByAddress((int)val, out selectedSlave))
                    {
                        this.Log(LogLevel.Warning, "Tried to select a not-connected slave at address 0x{0:X}", val);
                    }
                })
                .WithReservedBits(8, 24)
            ;
        }

        private bool TryFillReceivedBuffer(bool generateInterrupt)
        {
            if(selectedSlave == null)
            {
                return false;
            }

            if(!slaveToMasterBuffer.Any())
            {
                var data = selectedSlave.Read();
                slaveToMasterBuffer.EnqueueRange(data);
            }

            if(slaveToMasterBuffer.Any())
            {
                if(generateInterrupt)
                {
                    rxInterruptPending.Value = true;
                    UpdateInterrupts();
                }
                return true;
            }

            return false;
        }

        private bool TryReadFromSlave(out byte b)
        {
            if(!enabled)
            {
                this.Log(LogLevel.Warning, "Tried to read data on a disabled controller");
                b = 0;
                return false;
            }

            if(!slaveToMasterBuffer.TryDequeue(out b))
            {
                TryFillReceivedBuffer(false);
                if(!slaveToMasterBuffer.TryDequeue(out b))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TrySendDataToSlave()
        {
            if(!enabled)
            {
                this.Log(LogLevel.Warning, "Tried to send data on a disabled controller");
                return false;
            }

            if(!masterToSlaveBuffer.Any())
            {
                return false;
            }

            if(selectedSlave == null)
            {
                this.Log(LogLevel.Warning, "No slave is currently attached at selected address 0x{0:X}", address.Value);
                return false;
            }

            var data = masterToSlaveBuffer.DequeueAll();
            this.Log(LogLevel.Noisy, "Sending {0} bytes to the device {1}", data.Length, address.Value);
            selectedSlave.Write(data);

            return true;
        }

        private void StopTransmission()
        {
            transmissionInProgress = false;

            // send out buffered data to slave;
            // in reality there is no fifo - each
            // byte is sent right away, but our
            // I2C interface in Renode works a bit
            // different
            TrySendDataToSlave();

            selectedSlave?.FinishTransmission();

            stoppedInterruptPending.Value = true;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var flag = false;

            flag |= txInterruptEnabled.Value && txInterruptPending.Value;
            flag |= rxInterruptEnabled.Value && rxInterruptPending.Value;
            flag |= stoppedInterruptEnabled.Value && stoppedInterruptPending.Value;
            flag |= errorInterruptEnabled.Value && errorInterruptPending.Value;

            this.Log(LogLevel.Noisy, "Setting IRQ to {0}", flag);
            IRQ.Set(flag);
        }

        private readonly Queue<byte> slaveToMasterBuffer;
        private readonly Queue<byte> masterToSlaveBuffer;

        private II2CPeripheral selectedSlave;
        private bool enabled;
        private bool transmissionInProgress;

        private IValueRegisterField address;
        private IFlagRegisterField txInterruptPending;
        private IFlagRegisterField txInterruptEnabled;

        private IFlagRegisterField rxInterruptPending;
        private IFlagRegisterField rxInterruptEnabled;

        private IFlagRegisterField errorInterruptPending;
        private IFlagRegisterField errorInterruptEnabled;

        private IFlagRegisterField stoppedInterruptPending;
        private IFlagRegisterField stoppedInterruptEnabled;

        private IFlagRegisterField byteBoundaryStopShortcut;

        private IFlagRegisterField addressNackError;

        private enum Registers
        {
            StartReceiving = 0x000,
            StartTransmitting = 0x008,
            StopTransmitting = 0x014,
            SuspendTransmitting = 0x01C,
            ResumeReceiving = 0x020,
            StoppedInterruptPending = 0x104,
            RxInterruptPending = 0x108,
            TxInterruptPending = 0x11C,
            ErrorInterruptPending = 0x124,
            ByteBoundaryEventPending = 0x138,
            SuspendedInterruptPending = 0x148,
            Shortcuts = 0x200,
            SetEnableInterrupts = 0x304,
            ClearEnableInterrupts = 0x308,
            ErrorSource = 0x4C4,
            Enable = 0x500,
            PinSelectSCL = 0x508,
            PinSelectSDA = 0x50C,
            ReceiveBuffer = 0x518,
            TransferBuffer = 0x51C,
            Frequency = 0x524,
            Address = 0x588
        }
    }
}

