//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class MPFS_I2C : SimpleContainer<II2CPeripheral>, IProvidesRegisterCollection<ByteRegisterCollection>, II2CPeripheral, IBytePeripheral, IKnownSize
    {
        public MPFS_I2C(IMachine machine) : base(machine)
        {
            transferBuffer = new Queue<byte>();
            receiveBuffer = new Queue<byte>();
            IRQ = new GPIO();

            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();

            Reset();
        }

        public override void Reset()
        {
            Interlocked.Exchange(ref irqResubmitCounter, 0);
            pendingMasterTransaction = false;
            isReadOperation = false;
            selectedSlave = null;
            transferBuffer.Clear();
            receiveBuffer.Clear();
            RegistersCollection.Reset();
            // setting current state will update interrupts
            CurrentState = State.Idle;
        }

        public void FinishTransmission()
        {
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public void Write(byte[] data)
        {
            foreach(var b in data)
            {
                receiveBuffer.Enqueue(b);
            }
            CurrentState = State.PreviouslyAddressedWithOwnAddressDataReceivedAckReturned;
        }

        public byte[] Read(int count = 1)
        {
            // at the moment we do not take `count` into account because of API changes in the near future
            return transferBuffer.ToArray();
        }

        public long Size => 0x1000;
        public GPIO IRQ { get; private set; }
        public ByteRegisterCollection RegistersCollection { get; private set; }

        // due to a troublesome implementation of the controller's isr
        // we need to queue the interrupt requests and resubmit them
        // on a clear of 'si'
        private void SubmitInterrupt()
        {
            Interlocked.Increment(ref irqResubmitCounter);
            serialInterruptFlag.Value = true;
            UpdateInterrupt();
        }

        private void SendDataToSlave()
        {
            if(selectedSlave == null)
            {
                CurrentState = State.Idle;
                return;
            }

            if(transferBuffer.Count > 0)
            {
                selectedSlave.Write(transferBuffer.ToArray());
                transferBuffer.Clear();
                CurrentState = State.DataTransmittedAckReceived;
            }
            else
            {
                CurrentState = State.Idle;
            }
        }

        private void UpdateInterrupt()
        {
            IRQ.Set(isCoreEnabled.Value && serialInterruptFlag.Value);
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithTag("CR2", 7, 1)
                .WithFlag(6, out isCoreEnabled, name: "ens1")
                .WithFlag(5, FieldMode.WriteOneToClear | FieldMode.Read, name: "sta",
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }

                        switch(CurrentState)
                        {
                            case State.Idle:
                            case State.SlaveAddressReadBitTransmittedAckNotReceived:
                            case State.SlaveAddressWriteBitTransmittedAckNotReceived:
                                pendingMasterTransaction = true;
                                CurrentState = State.StartConditionTransmitted;
                                break;
                            case State.StartConditionTransmitted:
                            case State.RepeatedStartConditionTransmitted:
                                SendDataToSlave();
                                CurrentState = State.RepeatedStartConditionTransmitted;
                                break;
                            case State.DataTransmittedAckReceived:
                                if(pendingMasterTransaction)
                                {
                                    isReadOperation = true;
                                    CurrentState = State.RepeatedStartConditionTransmitted;
                                }
                                break;
                            default:
                                this.Log(LogLevel.Warning, "Setting START bit in unhandled state: {0}", CurrentState);
                                break;
                        }
                    })
                .WithFlag(4, FieldMode.WriteOneToClear | FieldMode.Read, name: "sto",
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            selectedSlave = null;
                            isReadOperation = false;
                            pendingMasterTransaction = false;
                            CurrentState = State.Idle;
                        }
                    })
                .WithFlag(3, out serialInterruptFlag, name: "si", writeCallback: (previousValue, currentValue) =>
                    {
                        if(!(previousValue && !currentValue))
                        {
                            // writing 0 clears the interrupt
                            return;
                        }
                        if(Interlocked.Decrement(ref irqResubmitCounter) > 0)
                        {
                            // if there are any queued irqs set the flag again
                            serialInterruptFlag.Value = true;
                        }
                        else
                        {
                            Interlocked.Increment(ref irqResubmitCounter);
                        }
                        UpdateInterrupt();
                    })
                .WithFlag(2, name: "aa")
                .WithTag("CR0/CR1", 0, 2);


            Registers.Status.Define(this)
                .WithEnumField<ByteRegister, State>(0, 8, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        return CurrentState;
                    }
                );


            Registers.Data.Define(this)
                .WithValueField(0, 8, name: "sd",
                    valueProviderCallback: _ =>
                    {
                        if(receiveBuffer.Count == 0)
                        {
                            this.Log(LogLevel.Warning, "{0} trying to read from empty data buffer, transaction will halt", pendingMasterTransaction ? "Master" : "Slave");
                            if(pendingMasterTransaction)
                            {
                                pendingMasterTransaction = false;
                                CurrentState = State.DataReceivedAckNotReturned;
                            }
                            else
                            {
                                CurrentState = State.Stop;
                            }
                            return 0;
                        }
                        var result = receiveBuffer.Dequeue();
                        if(receiveBuffer.Count > 0)
                        {
                            // the state machine requires states in which master and slave are reading from the data register
                            CurrentState = pendingMasterTransaction ? State.DataReceivedAckReturned : State.PreviouslyAddressedWithOwnAddressDataReceivedAckReturned;
                        }
                        return result;
                    },
                    writeCallback: (_, val) =>
                    {
                        switch(CurrentState)
                        {
                            case State.StartConditionTransmitted:
                            case State.RepeatedStartConditionTransmitted:
                                var slaveAddress = val >> 1;
                                isReadOperation = BitHelper.IsBitSet(val, 0);
                                if(!ChildCollection.TryGetValue((int)slaveAddress, out selectedSlave))
                                {
                                    this.Log(LogLevel.Warning, "Addressing unregistered slave: 0x{0:X}", slaveAddress);
                                    CurrentState = (isReadOperation) ? State.SlaveAddressReadBitTransmittedAckNotReceived : State.SlaveAddressWriteBitTransmittedAckNotReceived;
                                }
                                else
                                {
                                    if(isReadOperation)
                                    {
                                        var bytes = selectedSlave.Read();
                                        if(bytes.Length > 0)
                                        {
                                            foreach(var b in bytes)
                                            {
                                                receiveBuffer.Enqueue(b);
                                            }
                                            CurrentState = State.DataReceivedAckReturned;
                                        }
                                    }
                                    else
                                    {
                                        CurrentState = State.SlaveAddressWriteBitTransmittedAckReceived;
                                    }
                                }
                                break;
                            case State.SlaveAddressWriteBitTransmittedAckReceived:
                            case State.DataTransmittedAckReceived:
                                transferBuffer.Enqueue((byte)val);
                                SendDataToSlave();
                                break;
                            default:
                                this.Log(LogLevel.Warning, "Writing to data register in unhandled state: {0}", CurrentState);
                                break;
                        }
                    });

            Registers.Slave0Address.Define(this)
                .WithValueField(1, 7, name: "adr")
                .WithFlag(0, name: "gc");

            Registers.Slave1Address.Define(this)
                .WithValueField(1, 7, name: "adr")
                .WithFlag(0, name: "gc");
        }

        private State CurrentState
        {
            get
            {
                return state;
            }

            set
            {
                state = value;
                SubmitInterrupt();
            }
        }

        private readonly Queue<byte> receiveBuffer;
        private readonly Queue<byte> transferBuffer;

        private State state;
        private II2CPeripheral selectedSlave;
        private IFlagRegisterField serialInterruptFlag;
        private IFlagRegisterField isCoreEnabled;
        private bool pendingMasterTransaction;
        private bool isReadOperation;
        private int irqResubmitCounter;

        private enum State
        {
            // MASTER states
            StartConditionTransmitted = 0x08,
            RepeatedStartConditionTransmitted = 0x10,
            SlaveAddressWriteBitTransmittedAckReceived = 0x18,
            SlaveAddressWriteBitTransmittedAckNotReceived = 0x20,
            DataTransmittedAckReceived = 0x28,
            SlaveAddressReadBitTransmittedAckReceived = 0x40,
            SlaveAddressReadBitTransmittedAckNotReceived = 0x48,
            DataReceivedAckReturned = 0x50,
            DataReceivedAckNotReturned = 0x58,
            Idle = 0xF8,

            // SLAVE states
            OwnSlaveAddressWriteBitReceivedAckReturned = 0x60,
            PreviouslyAddressedWithOwnAddressDataReceivedAckReturned = 0x80,
            Stop = 0xA0
        }

        private enum Registers
        {
            Control = 0x0,
            Status = 0x4,
            Data = 0x8,
            Slave0Address = 0xC,
            SMBus = 0x10,
            Frequency = 0x14,
            GlitchRegLength = 0x18,
            Slave1Address = 0x1C
        }
    }
}
