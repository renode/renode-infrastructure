//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class OpenCoresI2C : SimpleContainer<II2CPeripheral>, IBytePeripheral, IKnownSize
    {
        public OpenCoresI2C(IMachine machine) : base(machine)
        {
            dataToSlave = new Queue<byte>();
            dataFromSlave = new Queue<byte>();

            var commonRegistersMap = new Dictionary<long, ByteRegister>()
            {
                {(long)Registers.Control, new ByteRegister(this)
                    .WithFlag(7, out enabled)
                    .WithTag("Interrupt enable", 6, 1)
                    .WithReservedBits(0, 6)
                }
            };

            var readRegistersMap = new Dictionary<long, ByteRegister>(commonRegistersMap)
            {
                {(long)Registers.Receive, new ByteRegister(this)
                    .WithValueField(0, 8, out receiveBuffer, FieldMode.Read)
                },

                {(long)Registers.Status, new ByteRegister(this)
                    .WithFlag(7, out receivedAckFromSlaveNegated, FieldMode.Read)
                    .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => transactionInProgress, name: "Busy")
                    // We're using receivedAckFromSlaveNegated as we do not implement other ways of detecting arbitration loss
                    .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => receivedAckFromSlaveNegated.Value, name: "Arbitration lost")
                    .WithReservedBits(2, 3)
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "Transfer in progress")
                    .WithFlag(0, out interruptFlag, FieldMode.Read)
                }
            };

            var writeRegistersMap = new Dictionary<long, ByteRegister>(commonRegistersMap)
            {
                {(long)Registers.Transmit, new ByteRegister(this)
                    .WithValueField(0, 8, out transmitBuffer, FieldMode.Write)
                },

                {(long)Registers.Command, new ByteRegister(this)
                    .WithFlag(7, out generateStartCondition, FieldMode.Write)
                    .WithFlag(6, out generateStopCondition, FieldMode.Write)
                    .WithFlag(5, out readFromSlave, FieldMode.Write)
                    .WithFlag(4, out writeToSlave, FieldMode.Write)
                    .WithTag("ACK", 3, 1)
                    .WithReservedBits(1, 2)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, __) => interruptFlag.Value = false)
                    .WithWriteCallback((_, __) =>
                    {
                        if(!enabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Writing to a disabled controller.");
                            return;
                        }

                        if(generateStartCondition.Value)
                        {
                            generateStartCondition.Value = false;
                            if(transactionInProgress)
                            {
                                // repeated start - finish previous transaction first
                                SendDataToSlave();
                            }
                            else
                            {
                                transactionInProgress = true;
                            }

                            dataFromSlave.Clear();

                            if(!TryResolveSelectedSlave(out selectedSlave))
                            {
                                interruptFlag.Value = true;
                                transactionInProgress = false;
                                return;
                            }
                        }
                        else if(writeToSlave.Value)
                        {
                            writeToSlave.Value = false;
                            HandleWriteToSlaveCommand();
                        }
                        else if(readFromSlave.Value)
                        {
                            readFromSlave.Value = false;
                            HandleReadFromSlaveCommand();
                        }

                        if(generateStopCondition.Value)
                        {
                            generateStopCondition.Value = false;
                            if(!transactionInProgress)
                            {
                                this.Log(LogLevel.Warning, "Asked to generate STOP signal, but no START signal has been recently generated");
                                return;
                            }

                            SendDataToSlave();
                            selectedSlave = null;
                            transactionInProgress = false;
                        }
                    })
                }
            };

            readRegisters = new ByteRegisterCollection(this, readRegistersMap);
            writeRegisters = new ByteRegisterCollection(this, writeRegistersMap);
        }

        public byte ReadByte(long offset)
        {
            return readRegisters.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            writeRegisters.Write(offset, value);
        }

        public override void Reset()
        {
            readRegisters.Reset();
            writeRegisters.Reset();
            dataToSlave.Clear();
            dataFromSlave.Clear();
            selectedSlave = null;
            transactionInProgress = false;
        }

        public long Size => 0x1000;

        private bool TryResolveSelectedSlave(out II2CPeripheral selectedSlave)
        {
            var slaveAddress = (byte)(transmitBuffer.Value >> 1);
            if(!ChildCollection.TryGetValue(slaveAddress, out selectedSlave))
            {
                 this.Log(LogLevel.Warning, "Addressing unregistered slave: 0x{0:X}", slaveAddress);
                 receivedAckFromSlaveNegated.Value = true;
                 return false;
            }

            receivedAckFromSlaveNegated.Value = false;
            return true;
        }

        private void HandleReadFromSlaveCommand()
        {
            if(dataFromSlave.Count == 0)
            {
                foreach(var b in selectedSlave.Read())
                {
                    dataFromSlave.Enqueue(b);
                }
                
                if(dataFromSlave.Count == 0)
                {
                    this.Log(LogLevel.Warning, "Trying to read from slave, but no data is available");
                    receiveBuffer.Value = 0;
                    return;
                }
            }

            receiveBuffer.Value = dataFromSlave.Dequeue();
        }

        private void SendDataToSlave()
        {
            if(dataToSlave.Count == 0 || selectedSlave == null)
            {
                this.Log(LogLevel.Warning, "Trying to send data to slave, but either no data is available or the slave is not selected");
                return;
            }

            selectedSlave.Write(dataToSlave.ToArray());
            dataToSlave.Clear();
        }

        private void HandleWriteToSlaveCommand()
        {
            if(selectedSlave == null)
            {
                this.Log(LogLevel.Warning, "Trying to write to not selected slave, ignoring");
                return;
            }

            dataToSlave.Enqueue((byte)transmitBuffer.Value);
            interruptFlag.Value = true;
        }

        private bool transactionInProgress;
        private II2CPeripheral selectedSlave;

        private readonly Queue<byte> dataToSlave;
        private readonly Queue<byte> dataFromSlave;
        private readonly IValueRegisterField receiveBuffer;
        private readonly IValueRegisterField transmitBuffer;
        private readonly IFlagRegisterField readFromSlave;
        private readonly IFlagRegisterField writeToSlave;
        private readonly IFlagRegisterField interruptFlag;
        private readonly IFlagRegisterField enabled;
        private readonly IFlagRegisterField receivedAckFromSlaveNegated;
        private readonly IFlagRegisterField generateStartCondition;
        private readonly IFlagRegisterField generateStopCondition;
        private readonly ByteRegisterCollection readRegisters;
        private readonly ByteRegisterCollection writeRegisters;

        private enum Registers
        {
            ClockPrescaleLow = 0x0,
            ClockPrescaleHigh = 0x4,
            Control = 0x8,
            // no, it's not a bug  - Transmit is write-only, Receive read-only
            Transmit = 0xC,
            Receive = 0xC,
            // no, it's not a bug  - Command is write-only, Status read-only
            Command = 0x10,
            Status = 0x10
        }
    }
}
