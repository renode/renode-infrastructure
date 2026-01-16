//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class STM32F1_I2C : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public STM32F1_I2C(IMachine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            machine.ClockSource.AddClockEntry(new ClockEntry(
                period: 1,
                frequency: Frequency,
                handler: ExecuteScheduledAction,
                owner: this,
                localName: "transaction-delay",
                enabled: false,
                workMode: WorkMode.OneShot
            ));
        }

        public override void Reset()
        {
            child = null;
            address = null;
            dataState = DataState.Idle;
            data.Clear();
            RegistersCollection.Reset();
            UpdateSchedule(enable: false);
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)RegistersCollection.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            RegistersCollection.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x40;

        public int I2CReadCount { get; set; } = 1;

        public GPIO EventInterrupt { get; } = new GPIO();

        public GPIO ErrorInterrupt { get; } = new GPIO();

        public GPIO RxDMARequest { get; } = new GPIO();

        public GPIO TxDMARequest { get; } = new GPIO();

        private void DefineRegisters()
        {
            Registers.Control1.Define(this)
                .WithFlag(0, out peripheralEnable,
                    changeCallback: (_, __) => UpdateIdle(),
                    name: "PE"
                )
                .WithFlag(1,
                    valueProviderCallback: _ => false,
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            this.WarningLog("SMBus mode is not implemented, ignoring write to SMBUS");
                        }
                    },
                    name: "SMBUS"
                )
                .WithReservedBits(2, 1)
                .WithTaggedFlag("SMBTYPE", 3)
                .WithTaggedFlag("ENARP", 4)
                .WithTaggedFlag("ENPEC", 5)
                .WithTaggedFlag("ENGC", 6)
                .WithTaggedFlag("NOSTRETCH", 7)
                .WithFlag(8, out start, name: "START")
                .WithFlag(9, out stop, name: "STOP")
                .WithTaggedFlag("ACK", 10)
                .WithTaggedFlag("POS", 11)
                .WithTaggedFlag("PEC", 12)
                .WithTaggedFlag("ALERT", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("SWRST", 15)
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) =>
                {
                    lock(updateLock)
                    {
                        var updateInterrupts = start.Value || stop.Value;
                        var restart = start.Value && dataState != DataState.Idle && dataState != DataState.WriteAddress;
                        if(stop.Value || restart)
                        {
                            if(dataDirection.Value == Direction.Transmit)
                            {
                                if(data.Count > 0)
                                {
                                    this.DebugLog("Writing to 0x{0:X}: {1}", address, Misc.PrettyPrintCollectionHex(data));
                                    child?.Write(data.DequeueAll());
                                }
                                this.DebugLog("{0} transmission", stop.Value ? "Stopping" : "Restarting");
                                child?.FinishTransmission();
                                data.Clear();
                                byteTransferFinished.Value = false;
                                txDataEmpty.Value = false;
                                stop.Value = false;
                                dataState = DataState.Idle;
                                UpdateIdle();
                            }
                            else
                            {
                                dataState = DataState.LastRead;
                            }
                            mode.Value = Mode.Slave;
                        }
                        if(start.Value)
                        {
                            if(!restart)
                            {
                                this.DebugLog("Starting transmission");
                            }

                            mode.Value = Mode.Master;
                            startBit.Value = true;
                            byteTransferFinished.Value = false;
                            txDataEmpty.Value = false;
                            dataDirection.Value = Direction.Default;
                            dataState = DataState.WriteAddress;
                            start.Value = false;
                        }
                        if(updateInterrupts)
                        {
                            UpdateInterrupts();
                        }
                    }
                })
            ;

            Registers.Control2.Define(this, 0x4)
                .WithValueField(0, 6, out frequency,
                    changeCallback: (oldVal, newVal) =>
                    {
                        if(newVal < MinimalFrequency || newVal > MaximalFrequency)
                        {
                            this.WarningLog("Attempted write to FREQ field with a not allowed value (0x{0:X}), ignoring", newVal);
                            frequency.Value = oldVal;
                        }
                        else
                        {
                            lock(updateLock)
                            {
                                UpdateSchedule();
                            }
                        }
                    },
                    name: "FREQ"
                )
                .WithReservedBits(6, 2)
                .WithFlag(8, out errorInterruptEnabled, name: "ITERREN")
                .WithFlag(9, out eventInterruptEnable, name: "ITEVTEN")
                .WithFlag(10, out bufferInterruptEnable, name: "ITBUFFEN")
                .WithTaggedFlag("DMAEN", 11)
                .WithTaggedFlag("LAST", 12)
                .WithReservedBits(13, 19)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.OwnAddress1.Define(this)
                .WithTaggedFlag("ADD0", 0)
                .WithTag("ADD[7:1]", 1, 7)
                .WithTag("ADD[9:8]", 8, 2)
                .WithReservedBits(10, 4)
                .WithFlag(14,
                    valueProviderCallback: _ => false,
                    writeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            this.WarningLog("I2C_OAR1[14] should always be set to 1 by the software, but was set to 0");
                        }
                    },
                    name: "ONE"
                )
                .WithFlag(15,
                    valueProviderCallback: _ => false,
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            this.WarningLog("10-bit slave addressing is not implemented, addressing mode not changed");
                        }
                    },
                    name: "ADDMODE"
                )
                .WithReservedBits(16, 16)
            ;

            Registers.OwnAddress2.Define(this)
                .WithTaggedFlag("ENDUAL", 0)
                .WithTag("ADD2[7:1]", 1, 7)
                .WithReservedBits(8, 24)
            ;

            Registers.Data.Define(this)
                .WithValueField(0, 8,
                    valueProviderCallback: _ => HandleDataRead(),
                    writeCallback: (_, value) => HandleDataWrite((byte)value),
                    name: "DR"
                )
                .WithReservedBits(8, 24)
            ;

            Registers.Status1.Define(this)
                .WithFlag(0, out startBit, FieldMode.Read, name: "SB")
                .WithFlag(1, out addressSent, FieldMode.Read, name: "ADDR")
                .WithFlag(2, out byteTransferFinished, FieldMode.Read, name: "BTF")
                .WithTaggedFlag("ADD10", 3)
                .WithTaggedFlag("STOPF", 4)
                .WithReservedBits(5, 1)
                .WithFlag(6, out rxDataNotEmpty, FieldMode.Read, name: "RxNE")
                .WithFlag(7, out txDataEmpty, FieldMode.Read, name: "TxE")
                .WithTaggedFlag("BERR", 8)
                .WithTaggedFlag("ARLO", 9)
                .WithFlag(10, out acknowledgeFailure, FieldMode.ReadToClear | FieldMode.WriteZeroToClear, name: "AF")
                .WithTaggedFlag("OVR", 11)
                .WithTaggedFlag("PECERR", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("TIMEOUT", 14)
                .WithTaggedFlag("SMBALERT", 15)
                .WithReservedBits(16, 16)
            ;

            Registers.Status2.Define(this)
                .WithEnumField(0, 1, out mode, FieldMode.Read, name: "MSL")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => dataState != DataState.Idle && dataState != DataState.LastRead, name: "BUSY")
                .WithEnumField(2, 1, out dataDirection, FieldMode.Read, name: "TRA")
                .WithReservedBits(3, 1)
                .WithTaggedFlag("GENCALL", 4)
                .WithTaggedFlag("SMBDEFAULT", 5)
                .WithTaggedFlag("SMBHOST", 6)
                .WithTaggedFlag("DUALF", 7)
                .WithTag("PEC", 8, 8)
                .WithReservedBits(16, 16)
                .WithReadCallback((_, __) =>
                {
                    // Assume access after SR1 read
                    addressSent.Value = false;
                })
            ;

            Registers.ClockControl.Define(this)
                .WithTag("CCR", 0, 12)
                .WithReservedBits(12, 2)
                .WithTaggedFlag("DUTY", 14)
                .WithTaggedFlag("F/S", 15)
                .WithReservedBits(16, 16)
            ;

            Registers.RiseTime.Define(this)
                .WithTag("TRISE", 0, 6)
                .WithReservedBits(6, 26)
            ;
        }

        private void ExecuteScheduledAction()
        {
            lock(updateLock)
            {
                scheduledAction?.Invoke();
            }
        }

        private void UpdateSchedule(bool? enable = null)
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }
            machine.ClockSource.ExchangeClockEntryWith(ExecuteScheduledAction,
                entry => entry.With(enabled: enable ?? entry.Enabled, frequency: Frequency, value: 0));
        }

        private void QueueUpdate(Action action)
        {
            lock(updateLock)
            {
                scheduledAction = action;
                UpdateSchedule(enable: true);
            }
        }

        private void UpdateDataTransmission()
        {
            if(!txDataEmpty.Value)
            {
                this.NoisyLog("TxE updated in scheduled action");
                txDataEmpty.Value = true;
                byteTransferFinished.Value = false;
                QueueUpdate(UpdateDataTransmission);
            }
            else
            {
                this.NoisyLog("TxE's BTF updated in scheduled action");
                byteTransferFinished.Value = true;
            }
            UpdateInterrupts();
        }

        private void UpdateDataReceive()
        {
            if(!rxDataNotEmpty.Value)
            {
                this.NoisyLog("RxNE updated in scheduled action");
                rxDataNotEmpty.Value = true;
                byteTransferFinished.Value = false;
                QueueUpdate(UpdateDataReceive);
            }
            else if(dataState != DataState.LastRead)
            {
                this.NoisyLog("RxNE's BTF updated in scheduled action");
                byteTransferFinished.Value = true;
            }
            UpdateInterrupts();
        }

        private void HandleDataWrite(byte value)
        {
            lock(updateLock)
            {
                txDataEmpty.Value = false;
                rxDataNotEmpty.Value = false;
                // Assume access after SR1 read
                startBit.Value = false;
                byteTransferFinished.Value = false;

                switch(dataState)
                {
                case DataState.WriteAddress:
                    var newAddress = BitHelper.GetValue(value, offset: 1, size: 7);
                    if(address != newAddress)
                    {
                        if(!TryGetByAddress(newAddress, out child))
                        {
                            this.WarningLog("Child address changed to 0x{0:X}, but target is not registered", newAddress);
                            child = null;
                            address = null;
                            acknowledgeFailure.Value = true;
                            dataState = DataState.Idle;
                            UpdateIdle();
                            UpdateInterrupts();
                            return;
                        }
                        address = newAddress;
                        this.DebugLog("Child address changed to 0x{0:X}", address);
                    }
                    dataDirection.Value = BitHelper.IsBitSet(value, 0) ? Direction.Receive : Direction.Transmit;
                    addressSent.Value = true;

                    if(data.Count > 0)
                    {
                        this.DebugLog("Clearing non-empty buffer: {0}", Misc.PrettyPrintCollectionHex(data));
                        data.Clear();
                    }

                    if(dataDirection.Value == Direction.Transmit)
                    {
                        txDataEmpty.Value = true;
                        dataState = DataState.WriteData;
                        QueueUpdate(UpdateDataTransmission);
                    }
                    else
                    {
                        dataState = DataState.ReadData;
                        QueueUpdate(UpdateDataReceive);
                        if(data.Count == 0)
                        {
                            data.EnqueueRange(child.Read(I2CReadCount));
                            this.DebugLog("Performed read from 0x{0:X}: {1}", address, Misc.PrettyPrintCollectionHex(data));
                        }
                    }
                    this.NoisyLog("Selected 0x{0:X} for {1}", address, dataDirection.Value == Direction.Transmit ? "write" : "read");
                    break;
                case DataState.WriteData:
                    data.Enqueue(value);
                    txDataEmpty.Value = true;
                    break;
                default:
                    this.WarningLog("Writing data in improper state ({0}), ignoring", dataState);
                    return;
                }

                UpdateInterrupts();
            }
        }

        private byte HandleDataRead()
        {
            lock(updateLock)
            {
                var result = (byte)0x0;

                rxDataNotEmpty.Value = byteTransferFinished.Value;
                // Assume access after SR1 read
                byteTransferFinished.Value = false;

                switch(dataState)
                {
                case DataState.LastRead:
                    if(rxDataNotEmpty.Value)
                    {
                        // Not the last read yet, as the data register
                        // hasn't been read before setting stop
                        goto case DataState.ReadData;
                    }
                    UpdateSchedule(enable: false);
                    result = PerformRead();
                    child?.FinishTransmission();
                    if(mode.Value == Mode.Master)
                    {
                        this.DebugLog("Restarting transmission");
                        dataState = DataState.WriteAddress;
                    }
                    else
                    {
                        this.DebugLog("Stopping transmission");
                        dataState = DataState.Idle;
                        UpdateIdle();
                    }
                    stop.Value = false;
                    break;
                case DataState.ReadData:
                    result = PerformRead();
                    QueueUpdate(UpdateDataReceive);
                    break;
                default:
                    this.WarningLog("Reading data in improper state ({0}), returning 0x0", dataState);
                    break;
                }

                return result;
            }
        }

        private byte PerformRead()
        {
            if(data.TryDequeue(out var b))
            {
                return b;
            }
            if(child == null)
            {
                this.DebugLog("Child not registered, returning 0x0");
                return 0x0;
            }

            data.EnqueueRange(child.Read(I2CReadCount));
            this.DebugLog("Performed read from 0x{0:X}: {1}", address, Misc.PrettyPrintCollectionHex(data));
            return data.TryDequeue(out b) ? b : (byte)0x0;
        }

        private void UpdateIdle()
        {
            lock(updateLock)
            {
                if(dataState != DataState.Idle)
                {
                    return;
                }

                if(!peripheralEnable.Value)
                {
                    mode.Value = Mode.Slave;
                    startBit.Value = false;
                    addressSent.Value = false;
                    byteTransferFinished.Value = false;
                    dataDirection.Value = Direction.Default;
                    acknowledgeFailure.Value = false;
                    UpdateSchedule(enable: false);
                }
            }
        }

        private void UpdateInterrupts()
        {
            var bufferEventState = false;
            bufferEventState |= txDataEmpty.Value;
            bufferEventState |= rxDataNotEmpty.Value;

            var eventState = false;
            eventState |= startBit.Value;
            eventState |= addressSent.Value;
            eventState |= byteTransferFinished.Value;
            eventState |= bufferInterruptEnable.Value && bufferEventState;

            if(eventInterruptEnable.Value && eventState)
            {
                this.DebugLog("Event IRQ sent");
                EventInterrupt.Blink();
            }

            var errorState = false;
            errorState |= acknowledgeFailure.Value;

            if(errorInterruptEnabled.Value && errorState)
            {
                this.DebugLog("Error IRQ sent");
                ErrorInterrupt.Blink();
            }
        }

        // Frequency of sending byte of data
        // frequency field is in MHz
        private ulong Frequency => 1000000UL / 8 * frequency.Value;

        private Action scheduledAction;

        private DataState dataState;

        private IFlagRegisterField peripheralEnable;
        private IFlagRegisterField start;
        private IFlagRegisterField stop;
        private IValueRegisterField frequency;
        private IFlagRegisterField errorInterruptEnabled;
        private IFlagRegisterField eventInterruptEnable;
        private IFlagRegisterField bufferInterruptEnable;
        private IFlagRegisterField startBit;
        private IFlagRegisterField addressSent;
        private IFlagRegisterField byteTransferFinished;
        private IFlagRegisterField rxDataNotEmpty;
        private IFlagRegisterField txDataEmpty;
        private IFlagRegisterField acknowledgeFailure;
        private IEnumRegisterField<Mode> mode;
        private IEnumRegisterField<Direction> dataDirection;

        private int? address;
        private II2CPeripheral child;
        private readonly object updateLock = new object();
        private readonly Queue<byte> data = new Queue<byte>();

        private const ulong MinimalFrequency = 2;
        private const ulong MaximalFrequency = 42;

        public enum Registers
        {
            Control1        = 0x00,
            Control2        = 0x04,
            OwnAddress1     = 0x08,
            OwnAddress2     = 0x0C,
            Data            = 0x10,
            Status1         = 0x14,
            Status2         = 0x18,
            ClockControl    = 0x1C,
            RiseTime        = 0x20,
        }

        private enum Mode
        {
            Slave = 0,
            Master = 1,
        }

        private enum Direction
        {
            Default = 0,
            Receive = 0,
            Transmit = 1,
        }

        private enum DataState
        {
            Idle,
            WriteAddress,
            WriteData,
            ReadData,
            LastRead,
        }
    }
}
