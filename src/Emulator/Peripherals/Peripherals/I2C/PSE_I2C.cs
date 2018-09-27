//
// Copyright (c) 2010-2018 Antmicro
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
    public class PSE_I2C : SimpleContainer<II2CPeripheral>, IProvidesRegisterCollection<ByteRegisterCollection>, IBytePeripheral, IKnownSize
    {
        public PSE_I2C(Machine machine) : base(machine)
        {
            dataToSlave = new Queue<byte>();
            dataFromSlave = new Queue<byte>();
            IRQ = new GPIO();

            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();

            Reset();
        }

        public override void Reset()
        {
            Interlocked.Exchange(ref irqResubmitCounter, 0);
            dataToSlave.Clear();
            dataFromSlave.Clear();
            RegistersCollection.Reset();
            // setting current state will update interrupts
            CurrentState = State.Idle;
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
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

            if(dataToSlave.Count > 0)
            {
                selectedSlave.Write(dataToSlave.ToArray());
                dataToSlave.Clear();
                CurrentState = State.DataTransmittedAckReceived;
            }
            else
            {
                CurrentState = State.Idle;
            }
            selectedSlave = null;
        }

        private void UpdateInterrupt()
        {
            IRQ.Set(coreEnable.Value && serialInterruptFlag.Value);
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithTag("CR2", 7, 1)
                .WithFlag(6, out coreEnable, name: "ens1")
                .WithFlag(5, name: "sta", writeCallback: (_, val) =>
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
                                CurrentState = State.StartConditionTransmitted;
                                break;
                            case State.StartConditionTransmitted:
                                SendDataToSlave();
                                CurrentState = State.RepeatedStartConditionTransmitted;
                                break;
                            default:
                                this.Log(LogLevel.Warning, "Setting START bit in unhandled state: {0}", CurrentState);
                                break;
                        }
                    })
                .WithFlag(4, name: "sto", writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }

                        SendDataToSlave();
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
                        UpdateInterrupt();
                    })
                .WithFlag(2, name: "aa")
                .WithTag("CR0/CR1", 0, 2);


            Registers.Status.Define(this)
                .WithEnumField<ByteRegister, State>(0, 8, FieldMode.Read, valueProviderCallback: _ => CurrentState);


            Registers.Data.Define(this)
                .WithValueField(0, 8, name: "sd",
                    valueProviderCallback: _ =>
                    {
                        if(dataFromSlave.Count == 0)
                        {
                            this.Log(LogLevel.Warning, "Trying to read from empty data buffer");
                            return 0;
                        }

                        return dataFromSlave.Dequeue();
                    },
                    writeCallback: (_, val) =>
                    {
                        switch(CurrentState)
                        {
                            case State.StartConditionTransmitted:
                            case State.RepeatedStartConditionTransmitted:
                                var slaveAddress = val >> 1;
                                var isReadOperation = BitHelper.IsBitSet(val, 0);
                                if(!ChildCollection.TryGetValue((int)slaveAddress, out selectedSlave))
                                {
                                    this.Log(LogLevel.Warning, "Addressing unregistered slave: 0x{0:X}", slaveAddress);
                                    CurrentState = (isReadOperation) ? State.SlaveAddressReadBitTransmittedAckNotReceived : State.SlaveAddressWriteBitTransmittedAckNotReceived;
                                }
                                else
                                {
                                    if(isReadOperation)
                                    {
                                        foreach(var b in selectedSlave.Read())
                                        {
                                            dataFromSlave.Enqueue(b);
                                        }
                                        CurrentState = State.DataReceivedAckReturned;
                                    }
                                    else
                                    {
                                        CurrentState = State.SlaveAddressWriteBitTransmittedAckReceived;
                                    }
                                }
                                break;
                            case State.SlaveAddressWriteBitTransmittedAckReceived:
                            case State.DataTransmittedAckReceived:
                                dataToSlave.Enqueue((byte)val);
                                CurrentState = State.DataTransmittedAckReceived;
                                break;
                            default:
                                this.Log(LogLevel.Warning, "Writing to data register in unhandled state: {0}", CurrentState);
                                break;
                        }
                    });
        }

        private State CurrentState
        {
            get
            {
                return state;
            }

            set
            {
                if(state == value)
                {
                    return;
                }
                state = value;
                SubmitInterrupt();
            }
        }

        private State state;
        private II2CPeripheral selectedSlave;

        private readonly Queue<byte> dataFromSlave;
        private readonly Queue<byte> dataToSlave;
        private IFlagRegisterField serialInterruptFlag;
        private IFlagRegisterField coreEnable;
        private int irqResubmitCounter;

        private enum State
        {
            StartConditionTransmitted = 0x08,
            RepeatedStartConditionTransmitted = 0x10,
            SlaveAddressWriteBitTransmittedAckReceived = 0x18,
            SlaveAddressWriteBitTransmittedAckNotReceived = 0x20,
            DataTransmittedAckReceived = 0x28,
            SlaveAddressReadBitTransmittedAckReceived = 0x40,
            SlaveAddressReadBitTransmittedAckNotReceived = 0x48,
            DataReceivedAckReturned = 0x50,
            Idle = 0xF8
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
