//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class LiteX_I2C : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public LiteX_I2C(IMachine machine) : base(machine)
        {
            // 0 - clock, 1 - data, 2 - direction
            i2cDecoder = new BitPatternDetector(new[] { true, true, true }, this);
            i2cDecoder.RegisterPatternHandler((prev, curr) => !prev[ClockSignal] && curr[ClockSignal], name: "clockRising", action: HandleClockRising);
            i2cDecoder.RegisterPatternHandler((prev, curr) => prev[ClockSignal] && !curr[ClockSignal], name: "clockFalling", action: HandleClockFalling);
            i2cDecoder.RegisterPatternHandler((prev, curr) => prev[ClockSignal] && curr[ClockSignal] && prev[DataSignal] && !curr[DataSignal], name: "start", action: HandleStartCondition);
            i2cDecoder.RegisterPatternHandler((prev, curr) => prev[ClockSignal] && curr[ClockSignal] && !prev[DataSignal] && curr[DataSignal], name: "stop", action: HandleStopCondition);

            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.BitBang, new DoubleWordRegister(this, 0x5) // SCL && SDA are high by default
                    .WithFlag(0, out var clockSignal, name: "SCL")
                    .WithFlag(1, out var directionSignal, name: "OE")
                    .WithFlag(2, out var dataSignal, name: "SDA")
                    .WithWriteCallback((_, val) => i2cDecoder.AcceptState(new [] { clockSignal.Value, dataSignal.Value, directionSignal.Value }))
                },
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        if(directionSignal.Value)
                        {
                            this.Log(LogLevel.Warning, "Trying to read data, but the direction signal is set to OUTPUT");
                            return true;
                        }

                        if(bufferFromDevice.Count == 0)
                        {
                            if(state == State.Read)
                            {
                                // try - maybe there is sth more waiting for us
                                ReadNextByteFromDevice();
                            }

                            if(bufferFromDevice.Count == 0)
                            {
                                this.Log(LogLevel.Warning, "There are no more output bits to read");
                                // SDA is high when no data
                                return true;
                            }
                        }

                        return bufferFromDevice.Peek();
                    })
                },
            };

            bufferFromDevice = new Queue<bool>();
            bufferToDevice = new Stack<bool>();
            bytesToDevice = new Queue<byte>();

            registersCollection = new DoubleWordRegisterCollection(this, registers);
            Reset();
        }

        public override void Reset()
        {
            registersCollection.Reset();
            i2cDecoder.Reset();
            ResetBuffers();

            state = State.Idle;
            tickCounter = 0;
            slave = null;
            isRead = false;
        }

        public uint ReadDoubleWord(long offset)
        {
            return registersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registersCollection.Write(offset, value);
        }

        public long Size => 0x10;

        private void HandleStopCondition(bool[] signals)
        {
            if(slave != null && bytesToDevice.Count > 0)
            {
                this.Log(LogLevel.Noisy, "Writing data to the device");
                slave.Write(bytesToDevice.DequeueAll());
            }

            ResetBuffers();

            this.Log(LogLevel.Noisy, "Stop condition detected in state: {0}", state);
            state = State.Idle;
        }

        private void HandleStartCondition(bool[] signals)
        {
            this.Log(LogLevel.Noisy, "(repeated) Start condition detected in state: {0}", state);

            if(slave != null && bytesToDevice.Count > 0)
            {
                this.Log(LogLevel.Noisy, "Writing data to the device");
                slave.Write(bytesToDevice.DequeueAll());
            }

            ResetBuffers();
            state = State.Address;
        }

        private void HandleClockRising(bool[] signals)
        {
            switch(state)
            {
            case State.Address:
            {
                this.Log(LogLevel.Noisy, "Appending address bit #{0}: {1}", bufferToDevice.Count, signals[DataSignal]);
                bufferToDevice.Push(signals[DataSignal]);

                if(bufferToDevice.Count == 7)
                {
                    var address = (int)BitHelper.GetValueFromBitsArray(bufferToDevice.PopAll());

                    this.Log(LogLevel.Noisy, "Address decoded: 0x{0:X}", address);
                    state = State.Operation;

                    if(!TryGetByAddress(address, out slave))
                    {
                        this.Log(LogLevel.Warning, "Trying to access a non-existing I2C device at address 0x{0:X}", address);
                    }
                }
                break;
            }

            case State.Operation:
            {
                isRead = signals[DataSignal];
                this.Log(LogLevel.Noisy, "Operation decoded: {0}", isRead ? "read" : "write");

                state = State.AddressAck;
                break;
            }

            case State.AddressAck:
            {
                // write ACK(false) or NACK(true)
                bufferFromDevice.Enqueue(slave == null);

                if(slave == null)
                {
                    // ignore the rest of transmission until the next (repeated) start condition
                    state = State.Idle;
                }
                else if(isRead)
                {
                    tickCounter = 0;
                    state = State.Read;
                }
                else // it must be write
                {
                    state = State.Write;
                }
                break;
            }

            case State.Read:
            {
                tickCounter++;
                if(tickCounter == 8)
                {
                    state = State.ReadAck;
                }
                break;
            }

            case State.ReadAck:
            {
                tickCounter = 0;
                state = State.Read;
                break;
            }

            case State.Write:
            {
                this.Log(LogLevel.Noisy, "Latching data bit #{0}: {1}", bufferToDevice.Count, signals[DataSignal]);
                bufferToDevice.Push(signals[DataSignal]);

                if(bufferToDevice.Count == 8)
                {
                    state = State.WriteAck;
                }
                break;
            }

            case State.WriteAck:
            {
                DebugHelper.Assert(bufferToDevice.Count == 8);

                var dataByte = (byte)BitHelper.GetValueFromBitsArray(bufferToDevice.PopAll());
                this.Log(LogLevel.Noisy, "Decoded data byte #{0}: 0x{1:X}", bytesToDevice.Count, dataByte);

                bytesToDevice.Enqueue(dataByte);

                // ACK
                this.Log(LogLevel.Noisy, "Enqueuing ACK");
                bufferFromDevice.Enqueue(false);

                state = State.Write;
                break;
            }
            }
        }

        private void HandleClockFalling(bool[] signals)
        {
            if(!signals[DirectionSignal])
            {
                bufferFromDevice.TryDequeue(out var unused);
            }
        }

        private void ResetBuffers()
        {
            bufferToDevice.Clear();
            bufferFromDevice.Clear();
            bytesToDevice.Clear();

            state = State.Idle;
            slave = null;
            isRead = false;
        }

        private void ReadNextByteFromDevice()
        {
            if(slave == null)
            {
                this.Log(LogLevel.Warning, "Trying to read data from the device, but no slave is currently selected");
                return;
            }

            this.Log(LogLevel.Noisy, "Reading byte from device");
            foreach(var @byte in slave.Read())
            {
                // bits in I2C are transmitted most-significant-bit first
                var bits = BitHelper.GetBits(@byte).Take(8).Reverse();
                foreach(var bit in bits)
                {
                    bufferFromDevice.Enqueue(bit);
                }
            }
        }

        private State state;
        private int tickCounter;
        private II2CPeripheral slave;
        private bool isRead;

        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly BitPatternDetector i2cDecoder;
        private readonly Stack<bool> bufferToDevice;
        private readonly Queue<bool> bufferFromDevice;
        private readonly Queue<byte> bytesToDevice;

        private const int ClockSignal = 0;
        private const int DataSignal = 1;
        private const int DirectionSignal = 2;

        private enum Registers
        {
            BitBang = 0x0,
            Data = 0x4
        }

        private enum State
        {
            Idle,
            Address,
            AddressAck,
            Operation,
            Write,
            WriteAck,
            Read,
            ReadAck
        }
    }
}