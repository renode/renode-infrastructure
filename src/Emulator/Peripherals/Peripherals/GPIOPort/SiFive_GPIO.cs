﻿//
// Copyright (c) 2010-2023 Antmicro
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
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class SiFive_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public SiFive_GPIO(IMachine machine) : base(machine, NumberOfPins)
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
                            var bits = BitHelper.GetBits((uint)val);
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
                            var bits = BitHelper.GetBits((uint)val);
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
                                var bits = BitHelper.GetBits((uint)val);
                                for(var i = 0; i < bits.Length; i++)
                                {
                                    SetPinValue(i, bits[i]);
                                }
                            }
                        })
                },

                {(long)Registers.FallInterruptPending, new DoubleWordRegister(this)
                    .WithFlags(0, 32, out fallInterruptPending, FieldMode.Read | FieldMode.WriteOneToClear)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },

                {(long)Registers.FallInterruptEnable, new DoubleWordRegister(this)
                    .WithFlags(0, 32, out fallInterruptEnable)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },

                {(long)Registers.RiseInterruptPending, new DoubleWordRegister(this)
                    .WithFlags(0, 32, out riseInterruptPending, FieldMode.Read | FieldMode.WriteOneToClear)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },

                {(long)Registers.RiseInterruptEnable, new DoubleWordRegister(this)
                    .WithFlags(0, 32, out riseInterruptEnable)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },

                {(long)Registers.HighInterruptPending, new DoubleWordRegister(this)
                    .WithFlags(0, 32, out highInterruptPending, FieldMode.Read | FieldMode.WriteOneToClear)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },

                {(long)Registers.HighInterruptEnable, new DoubleWordRegister(this)
                    .WithFlags(0, 32, out highInterruptEnable)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },

                {(long)Registers.LowInterruptPending, new DoubleWordRegister(this)
                    .WithFlags(0, 32, out lowInterruptPending, FieldMode.Read | FieldMode.WriteOneToClear)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },

                {(long)Registers.LowInterruptEnable, new DoubleWordRegister(this)
                    .WithFlags(0, 32, out lowInterruptEnable)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateInterrupts();
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
            SetPinValue(number, value);
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

        /* UpdateIterrupts() sets the value of Connections[i]. */
        private void UpdateInterrupts()
        {
            lock(locker)
            {
                for(var i = 0; i < State.Length; i++)
                {
                    var value = State[i];
                    highInterruptPending[i].Value = value;
                    lowInterruptPending[i].Value = !value;
                }

                for(var i = 0; i < NumberOfPins; i++)
                {
                    var falling = fallInterruptEnable[i].Value && fallInterruptPending[i].Value;
                    var rising = riseInterruptEnable[i].Value && riseInterruptPending[i].Value;
                    var high = highInterruptEnable[i].Value && highInterruptPending[i].Value;
                    var low = lowInterruptEnable[i].Value && lowInterruptPending[i].Value;

                    Connections[i].Set(falling || rising || high || low);
                }
            }
        }

        private void SetPinValue(int i, bool value)
        {
            lock(locker)
            {
                if((pins[i].pinOperation & Operation.Write) == 0)
                {
                    this.Log(LogLevel.Warning, "Trying to write a pin that isn't configured for writing. Skipping.");
                    return;
                }

                var interruptsEnabled = fallInterruptEnable[i].Value || riseInterruptEnable[i].Value || highInterruptEnable[i].Value || lowInterruptEnable[i].Value;
                var prevState = State[i];
                base.OnGPIO(i, value);
                if(interruptsEnabled)
                {
                    /* We're handling these 2 interrupts here because we need to know previous pin's state.
                       The other two are handled in UpdateInterrupts() */
                    if(!prevState && value)
                    {
                        riseInterruptPending[i].Value = true;
                    }
                    else if(prevState && !value)
                    {
                        fallInterruptPending[i].Value = true;
                    }
                    UpdateInterrupts();
                }
                else
                {
                    Connections[i].Set(value);
                }
            }
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly object locker;
        private readonly Pin[] pins;

        private const int NumberOfPins = 32;

        private IFlagRegisterField[] fallInterruptEnable;
        private IFlagRegisterField[] fallInterruptPending;
        private IFlagRegisterField[] riseInterruptEnable;
        private IFlagRegisterField[] riseInterruptPending;
        private IFlagRegisterField[] highInterruptEnable;
        private IFlagRegisterField[] highInterruptPending;
        private IFlagRegisterField[] lowInterruptEnable;
        private IFlagRegisterField[] lowInterruptPending;

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
