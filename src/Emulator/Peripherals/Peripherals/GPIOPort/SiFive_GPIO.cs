﻿//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class SiFive_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public SiFive_GPIO(Machine machine) : base(machine, NumberOfPins)
        {
            locker = new object();
            pins = new Pin[NumberOfPins];

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.PinValue, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        valueProviderCallback: _ =>
                        {
                            var readOperations = pins.Select(x => (x.pinOperation & Operation.Read) != 0);
                            var result = readOperations.Zip(State, (operation, state) => operation && state);
                            return BitHelper.GetValueFromBitsArray(result);
                        })
                },

                {(long)Registers.PinInputEnable, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        writeCallback: (_, val) =>
                        {
                            var bits = BitHelper.GetBits(val);
                            for(var i = 0; i < bits.Length; i++)
                            {
                                Misc.FlipFlag(ref pins[i].pinOperation, Operation.Read, bits[i]);
                            }
                        })
                },

                {(long)Registers.PinOutputEnable, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
	                    writeCallback: (_, val) =>
                        {
                            var bits = BitHelper.GetBits(val);
                            for (var i = 0; i < bits.Length; i++)
                            {
                                Misc.FlipFlag(ref pins[i].pinOperation, Operation.Write, bits[i]);
                            }
                        })
                },

                {(long)Registers.OutputPortValue, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        writeCallback: (_, val) =>
                        {
                            lock(locker)
                            {
                                var bits = BitHelper.GetBits(val);
                                for(var i = 0; i < bits.Length; i++)
                                {
                                    if((pins[i].pinOperation & Operation.Write) != 0)
                                    {
                                        State[i] = bits[i];
                                        Connections[i].Set(bits[i]);
                                    }
                                }
                            }
                        })
                },

                {(long)Registers.RiseInterruptPending, new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, val) =>
                    {
                        lock(locker)
                        {
                            var bits = BitHelper.GetBits(val);
                            for(var i = 0; i < bits.Length; i++)
                            {
                                if(bits[i])
                                {
                                    Connections[i].Set(State[i]);
                                }
                            }
                        }
                    })
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void OnGPIO(int number, bool value)
        {
            lock(locker)
            {
                base.OnGPIO(number, value);
                Connections[number].Set(value);
            }
        }

        public override void Reset()
        {
            lock(locker)
            {
                base.Reset();
                registers.Reset();
            }
        }

        public long Size => 0x1000;

        private readonly DoubleWordRegisterCollection registers;
        private readonly object locker;
        private readonly Pin[] pins;

        private const int NumberOfPins = 32;

        private struct Pin
        {
            public Operation pinOperation;
        }

        [Flags]
        private enum Operation : long
        {
            Disabled = 0x0,
            Read = 0x1,
            Write = 0x2
        }

        private enum Registers : long
        {
            PinValue = 0x00,
            PinInputEnable = 0x04,
            PinOutputEnable = 0x08,
            OutputPortValue = 0x0C,
            InternalPullUpEnable = 0x10,
            PinDriveStrength = 0x14,
            RiseInterruptEnable = 0x18,
            RiseInterruptPending = 0x1C,
            FallInterruptEnable = 0x20,
            FallInterruptPending = 0x24,
            HighInterruptEnable = 0x28,
            HighInterruptPending = 0x2C,
            LowInterruptEnable = 0x30,
            LowInterruptPending = 0x34,
            HwIOFunctionEnable = 0x38,
            HwIOFunctionSelect = 0x3C,
            OutputXor = 0x40
        }
    }
}
