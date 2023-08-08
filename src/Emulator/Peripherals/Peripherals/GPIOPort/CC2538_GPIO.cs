//
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

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class CC2538_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public CC2538_GPIO(IMachine machine) : base(machine, NumberOfGPIOs)
        {
            locker = new object();
            IRQ = new GPIO();
            irqManager = new GPIOInterruptManager(IRQ, State);

            PrepareRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(locker)
            {
                if(offset < 0x400)
                {
                    var mask = BitHelper.GetBits((uint)(offset >> 2) & 0xFF);
                    var bits = BitHelper.GetBits(registers.Read(0));
                    var result = new bool[8];
                    for(var i = 0; i < 8; i++)
                    {
                        if(mask[i])
                        {
                            result[i] = bits[i];
                        }
                    }

                    return BitHelper.GetValueFromBitsArray(result);
                }

                return registers.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(locker)
            {
                if(offset < 0x400)
                {
                    var mask = BitHelper.GetBits((uint)(offset >> 2) & 0xFF);
                    var bits = BitHelper.GetBits(value);
                    for(var i = 0; i < 8; i++)
                    {
                        if(mask[i])
                        {
                            Connections[i].Set(bits[i]);
                            State[i] = bits[i];
                        }
                    }
                }
                else
                {
                    registers.Write(offset, value);
                }
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            if(number < 0 || number >= NumberOfGPIOs)
            {
                throw new ArgumentOutOfRangeException(string.Format("Gpio #{0} called, but only {1} lines are available", number, NumberOfGPIOs));
            }

            lock(locker)
            {
                base.OnGPIO(number, value);
                irqManager.RefreshInterrupts();
            }
        }

        public override void Reset()
        {
            lock(locker)
            {
                base.Reset();
                irqManager.Reset();
                registers.Reset();
                IRQ.Unset();
            }
        }

        public GPIO IRQ { get; private set; }
        public long Size => 0x1000;

        private void PrepareRegisters()
        {
            registers = new DoubleWordRegisterCollection(this, new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 8,
                        writeCallback: (_, val) =>
                        {
                            var bits = BitHelper.GetBits((uint)val);
                            for(int i = 0; i < 8; i++)
                            {
                                if(irqManager.PinDirection[i] == GPIOInterruptManager.Direction.Input)
                                {
                                    Connections[i].Set(bits[i]);
                                    State[i] = bits[i];
                                }
                            }
                        },
                    valueProviderCallback: _ => { return BitHelper.GetValueFromBitsArray(State); })
                },
                {(long)Registers.DataDirection, new DoubleWordRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, val) =>
                    {
                        var bits = BitHelper.GetBits((uint)val);
                        for(var i = 0; i < 8; i++)
                        {
                            irqManager.PinDirection[i] = bits[i] ? GPIOInterruptManager.Direction.Output : GPIOInterruptManager.Direction.Input;
                        }
                    },
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(irqManager.PinDirection.Select(x => x == GPIOInterruptManager.Direction.Output)))
                },
                {(long)Registers.InterruptSense, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out interruptSenseField, writeCallback: (_, val) => CalculateInterruptTypes())
                },
                {(long)Registers.InterruptBothEdges, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out interruptBothEdgeField, writeCallback: (_, val) => CalculateInterruptTypes())
                },
                {(long)Registers.InterruptEvent, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out interruptEventField, writeCallback: (_, val) => CalculateInterruptTypes())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, val) =>
                    {
                        var bits = BitHelper.GetBits((uint)val);
                        for(var i = 0; i < 8; i++)
                        {
                            irqManager.InterruptEnable[i] = bits[i];
                            irqManager.InterruptMask[i] = bits[i];
                        }
                        irqManager.RefreshInterrupts();
                    },
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(irqManager.InterruptEnable))
                },
                {(long)Registers.RawInterruptStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 8, valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(irqManager.ActiveInterrupts))},
                {(long)Registers.MaskedInterruptStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 8, valueProviderCallback: _ => CalculateMaskedInterruptValue())},
                {(long)Registers.InterruptClear, new DoubleWordRegister(this)
                                .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, val) =>
                                {
                                    var bits = BitHelper.GetBits((uint)val);
                                    for(var i = 0; i < 8; i++)
                                    {
                                        if(bits[i])
                                        {
                                            irqManager.ClearInterrupt(i);
                                        }
                                    }
                                })
                },
                {(long)Registers.ModeControlSelect, new DoubleWordRegister(this)
                    .WithTag("AFSEL", 0, 32)
                },
                {(long)Registers.PortEdgeControl, new DoubleWordRegister(this)
                    .WithTag("P_EDGE_CTRL", 0, 32)
                },
                {(long)Registers.PowerUpInterruptEnable, new DoubleWordRegister(this)
                    .WithTag("PI_IEN", 0, 32)
                },
                {(long)Registers.IOPortsIRQDetectACK, new DoubleWordRegister(this)
                    .WithTag("IRQ_DETECT_ACK", 0, 32)
                },
                {(long)Registers.MaskedIRQDetectACK, new DoubleWordRegister(this)
                    .WithTag("IRQ_DETECT_UNMASK", 0, 32)
                }
            });
        }

        private void CalculateInterruptTypes()
        {
            lock(locker)
            {
                var isBothEdgesSensitive = BitHelper.GetBits((uint)interruptBothEdgeField.Value);
                var isLevelSensitive = BitHelper.GetBits((uint)interruptSenseField.Value);
                var isActiveHighOrRisingEdge = BitHelper.GetBits((uint)interruptEventField.Value);

                for(int i = 0; i < 8; i++)
                {
                    if(isLevelSensitive[i])
                    {
                        irqManager.InterruptType[i] = isActiveHighOrRisingEdge[i]
                                ? GPIOInterruptManager.InterruptTrigger.ActiveHigh
                                : GPIOInterruptManager.InterruptTrigger.ActiveLow;
                    }
                    else
                    {
                        if(isBothEdgesSensitive[i])
                        {
                            irqManager.InterruptType[i] = GPIOInterruptManager.InterruptTrigger.BothEdges;
                        }
                        else
                        {
                            irqManager.InterruptType[i] = isActiveHighOrRisingEdge[i]
                                ? GPIOInterruptManager.InterruptTrigger.RisingEdge
                                : GPIOInterruptManager.InterruptTrigger.FallingEdge;
                        }
                    }
                }
                irqManager.RefreshInterrupts();
            }
        }

        private uint CalculateMaskedInterruptValue()
        {
            var result = new bool[8];
            for(var i = 0; i < 8; i++)
            {
                result[i] = irqManager.ActiveInterrupts.ElementAt(i) && irqManager.InterruptMask[i];
            }
            return BitHelper.GetValueFromBitsArray(result);
        }

        private DoubleWordRegisterCollection registers;
        private readonly GPIOInterruptManager irqManager;
        private readonly object locker;

        private IValueRegisterField interruptSenseField;
        private IValueRegisterField interruptBothEdgeField;
        private IValueRegisterField interruptEventField;

        private const int NumberOfGPIOs = 8;

        private enum Registers
        {
            Data = 0x0,
            DataDirection = 0x400,
            InterruptSense = 0x404,
            InterruptBothEdges = 0x408,
            InterruptEvent = 0x40C,
            InterruptEnable = 0x410,
            RawInterruptStatus = 0x414,
            MaskedInterruptStatus = 0x418,
            InterruptClear = 0x41C,
            ModeControlSelect = 0x420,
            GPIOCommitUnlock = 0x520,
            GPIOCommit = 0x524,
            PMUX = 0x700,
            PortEdgeControl = 0x704,
            USBInputPowerUpEdgeControl = 0x708,
            PowerUpInterruptEnable = 0x710,
            IOPortsIRQDetectACK = 0x718,
            USBIRQDetectACK = 0x71C,
            MaskedIRQDetectACK = 0x720
        }
    }
}
