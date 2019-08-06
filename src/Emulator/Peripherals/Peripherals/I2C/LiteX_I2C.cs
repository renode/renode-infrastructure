//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class LiteX_I2C : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public LiteX_I2C(Machine machine) : base(machine)
        {
            // 0 - clock, 1 - data
            i2cDecoder = new BitPatternDetector(new [] { true, true });
            clockRising = i2cDecoder.RegisterPatternHandler((prev, curr) => !prev[0] && curr[0]);
            startCondition = i2cDecoder.RegisterPatternHandler((prev, curr) => prev[0] && curr[0] && prev[1] && !curr[1]);
            stopCondition = i2cDecoder.RegisterPatternHandler((prev, curr) => prev[0] && curr[0] && !prev[1] && curr[1]);

            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.BitBang, new DoubleWordRegister(this)
                    .WithFlag(0, out var clockSignal, FieldMode.Write, name: "SCL")
                    .WithFlag(1, out var directionSignal, FieldMode.Write, name: "OE")
                    .WithFlag(2, out var dataSignal, FieldMode.Write, name: "SDA")
                    .WithWriteCallback((_, val) => DoBitBanging(clockSignal.Value, directionSignal.Value, dataSignal.Value))
                },

                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        if(bufferFromDevicePosition == -1 || bufferFromDevicePosition >= bufferFromDevice.Length)
                        {
                            this.Log(LogLevel.Warning, "There are no more output bits to read");
                            return false;
                        }

                        // TODO: this is not optimal
                        return bufferFromDevice.AsByte((uint)bufferFromDevicePosition, 1) != 0;
                    })
                },
            };

            bufferFromDevice = new BitStream();
            bufferToDevice = new BitStream();

            registersCollection = new DoubleWordRegisterCollection(this, registers);
            Reset();
        }


        private void DoBitBanging(bool clockSignal, bool directionSignal, bool dataSignal)
        {
            var i2cState = i2cDecoder.AcceptState(new [] { clockSignal, dataSignal });

            switch(state)
            {
                case State.Idle:
                {
                    if(i2cState == startCondition)
                    {
                        state = State.Address;
                    }
                    break;
                }

                case State.Address:
                {
                    if(i2cState == clockRising)
                    {
                        bufferToDevice.AppendBit(dataSignal);
                    }

                    if(bufferToDevice.Length == 7)
                    {
                        address = bufferToDevice.AsByte();
                        bufferToDevice.Clear();
                        this.Log(LogLevel.Noisy, "Address: 0x{0:X}", address);

                        state = State.Operation;
                    }
                    break;
                }

                case State.Operation:
                {
                    if(i2cState == clockRising)
                    {
                        isRead = dataSignal;

                        state = State.AddressAck;
                    }
                    break;
                }

                case State.AddressAck:
                {
                    if(i2cState == clockRising)
                    {
                        state = isRead
                            ? State.PrepareRead
                            : State.PrepareWrite;
                    }
                    break;
                }

                case State.PrepareRead:
                {
                    if(!TryGetByAddress(address, out var slave))
                    {
                        this.Log(LogLevel.Warning, "Tried to read I2C data from a non-existing slave at address 0x{0:X}", address);
                        break;
                    }

                    bufferFromDevice.Clear();
                    bufferFromDevicePosition = -1;
                    foreach(var b in slave.Read())
                    {
                        bufferFromDevice.Append(b);
                    }

                    state = State.Read;
                    break;
                }

                case State.Read:
                {
                    if(i2cState == stopCondition)
                    {
                        state = State.Idle;
                        break;
                    }

                    if(i2cState == clockRising)
                    {
                        bufferFromDevicePosition++;
                    }

                    break;
                }

                case State.PrepareWrite:
                {
                    // TODO: handle repeated start condition
                    if(i2cState == stopCondition)
                    {
                        state = State.Write;
                        break;
                    }

                    if(i2cState == clockRising)
                    {
                        bufferToDevice.AppendBit(dataSignal);
                    }
                    break;
                }

                case State.Write:
                {
                    // take the device and do the actual write
                    if(!TryGetByAddress(address, out var slave))
                    {
                        this.Log(LogLevel.Warning, "Tried to write I2C data to a non-existing slave at address 0x{0:X}", address);
                        break;
                    }

                    slave.Write(bufferToDevice.AsByteArray());
                    bufferToDevice.Clear();
                    state = State.Idle; // TODO: here I asssume stop condition...
                    break;
                }
            }
        }

        public override void Reset()
        {
            registersCollection.Reset();
            bufferToDevice.Clear();
            bufferFromDevice.Clear();

            i2cDecoder.Reset();
            state = State.Idle;
            address = 0;
            isRead = false;
            bufferFromDevicePosition = -1;
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

        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly BitPatternDetector i2cDecoder;
        private readonly BitStream bufferToDevice;
        private readonly BitStream bufferFromDevice;
        private readonly int clockRising;
        private readonly int startCondition;
        private readonly int stopCondition;

        private int bufferFromDevicePosition;
        private State state;
        private byte address;
        private bool isRead;

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
            PrepareWrite,
            Write,
            PrepareRead,
            Read
        }
    }
}
