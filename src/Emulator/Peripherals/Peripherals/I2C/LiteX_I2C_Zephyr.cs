//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Debugging;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class LiteX_I2C_Zephyr : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public LiteX_I2C_Zephyr(IMachine machine) : base(machine)
        {
            // 0 - clock, 1 - data
            i2cDecoder = new BitPatternDetector(new [] { true, true }, this);
            i2cDecoder.RegisterPatternHandler((prev, curr) => !prev[ClockSignal] && curr[ClockSignal], name: "clockRising", action: HandleClockRising);
            i2cDecoder.RegisterPatternHandler((prev, curr) => prev[ClockSignal] && !curr[ClockSignal], name: "clockFalling", action: HandleClockFalling);
            i2cDecoder.RegisterPatternHandler((prev, curr) => prev[ClockSignal] && curr[ClockSignal] && prev[DataSignal] && !curr[DataSignal], name: "start", action: HandleStartCondition);
            i2cDecoder.RegisterPatternHandler((prev, curr) => prev[ClockSignal] && curr[ClockSignal] && !prev[DataSignal] && curr[DataSignal], name: "stop", action: HandleStopCondition);


            IFlagRegisterField clockSignal, directionSignal, dataSignal;
            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.BitBang, new DoubleWordRegister(this, 0x5) // SCL && SDA are high by default
                    .WithFlag(0, out clockSignal, name: "SCL")
                    .WithFlag(1, out directionSignal, name: "OE")
                    .WithFlag(2, out dataSignal, name: "SDA")
                    .WithWriteCallback((_, val) => i2cDecoder.AcceptState(new [] { clockSignal.Value, dataSignal.Value }))
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
                            this.Log(LogLevel.Noisy, "There are no more output bits to read");
                            // SDA is high when no data
                            return true;
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

        public long Size { get { return 0x10; } }

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
            if(slave != null && bytesToDevice.Count > 0)
            {
                this.Log(LogLevel.Noisy, "Writing data to the device");
                slave.Write(bytesToDevice.DequeueAll());
            }

            ResetBuffers();

            this.Log(LogLevel.Noisy, "(repeated) Start condition detected in state: {0}", state);
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

                    // write ACK(false) or NACK(true)
                    bufferFromDevice.Enqueue(slave == null);

                    state = State.AddressAck;
                    break;
                }

                case State.AddressAck:
                {
                    if(slave == null)
                    {
                        // ignore the rest of transmission until the next (repeated) start condition
                        state = State.Idle;
                    }
                    else if(isRead)
                    {
                        this.Log(LogLevel.Noisy, "Reading from device");
                        foreach(var @byte in slave.Read(6))
                        {
                            foreach(var bit in BitHelper.GetBits(@byte).Take(8).Reverse())
                            {
                                this.Log(LogLevel.Noisy, "Enqueuing bit: {0}", bit);
                                bufferFromDevice.Enqueue(bit);
                            }
                        }

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
            bool unused;
            if(state == State.Read) {
                var isEmpty = bufferFromDevice.TryDequeue(out unused);
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

        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly BitPatternDetector i2cDecoder;
        private readonly Stack<bool> bufferToDevice;
        private readonly Queue<bool> bufferFromDevice;
        private readonly Queue<byte> bytesToDevice;

        private State state;
        private int tickCounter;
        private II2CPeripheral slave;
        private bool isRead;

        private const int ClockSignal = 0;
        private const int DataSignal = 1;

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

