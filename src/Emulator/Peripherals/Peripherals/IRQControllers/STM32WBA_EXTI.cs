//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class STM32WBA_EXTI : BasicDoubleWordPeripheral, IKnownSize, ILocalGPIOReceiver, INumberedGPIOOutput
    {
        public STM32WBA_EXTI(IMachine machine, int numberOfOutputLines): base(machine)
        {
            this.numberOfLines = numberOfOutputLines;
            core = new STM32_EXTICore(this, lineConfigurableMask: 0x1FFFF, separateConfigs: true);
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfOutputLines; ++i)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
            internalReceiversCache = new Dictionary<int, InternalReceiver>();
            DefineRegisters();
        }

        public IGPIOReceiver GetLocalReceiver(int index)
        {
            if(!internalReceiversCache.TryGetValue(index, out var receiver))
            {
                receiver = new InternalReceiver(this, index);
                internalReceiversCache.Add(index, receiver);
            }
            return receiver;
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var connection in Connections.Values)
            {
                connection.Unset();
            }
            foreach(var receiver in internalReceiversCache.Values)
            {
                for(int pin = 0; pin < GpioPins; ++pin)
                {
                    receiver.UpdateGPIO(pin);
                }
            }
        }

        public long Size => 0x1000;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private void DefineRegisters()
        {
            RegistersCollection.DefineRegister((long)Registers.RaisingTriggerSelection)
                .WithValueField(0, 17, out core.RisingEdgeMask, name: "RT")
                .WithReservedBits(17, 15);

            RegistersCollection.DefineRegister((long)Registers.FallingTriggerSelection)
                .WithValueField(0, 17, out core.FallingEdgeMask, name: "FT")
                .WithReservedBits(17, 15);

            RegistersCollection.DefineRegister((long)Registers.SoftwareInterruptEvent)
                .WithValueField(0, 32, name: "SWIER", changeCallback: (_, value) =>
                {
                    BitHelper.ForeachActiveBit(value & core.InterruptMask.Value, bit =>
                    {
                        Connections[bit].Set();
                    });
                });

            RegistersCollection.DefineRegister((long)Registers.RaisingTriggerPending)
                .WithValueField(0, numberOfLines, out core.PendingRaisingInterrupts,
                    writeCallback: (_, val) => BitHelper.ForeachActiveBit(val, x => Connections[x].Unset()), name: "RPIF");

            RegistersCollection.DefineRegister((long)Registers.FallingTriggerPending)
                .WithValueField(0, numberOfLines, out core.PendingFallingInterrupts,
                    writeCallback: (_, val) => BitHelper.ForeachActiveBit(val, x => Connections[x].Unset()), name: "FPIF");

            RegistersCollection.DefineRegister((long)Registers.SecurityConfiguration);

            RegistersCollection.DefineRegister((long)Registers.PrivilegeConfiguration);

            for(var registerIndex = 0; registerIndex < InterruptSelectionRegistersCount; registerIndex++)
            {
                var reg = new DoubleWordRegister(this, 0);
                for(var fieldNumber = 0; fieldNumber < NumberOfPortsPerInterruptSelectionRegister; ++fieldNumber)
                {
                    var pinNumber = registerIndex * 4 + fieldNumber;
                    extiMappings[pinNumber] = reg.DefineValueField(8 * fieldNumber, 8, name: $"EXTI{pinNumber}",
                        changeCallback: (_, portNumber) =>
                        {
                            Connections[pinNumber].Unset();
                            ((InternalReceiver)GetLocalReceiver((int)portNumber)).UpdateGPIO(pinNumber);
                        }
                    );
                }
                RegistersCollection.AddRegister((long)Registers.ExternalInterruptSelection1 + 4 * registerIndex, reg);
            }

            RegistersCollection.DefineRegister((long)Registers.Lock);

            RegistersCollection.DefineRegister((long)Registers.WakeUpInterruptMask);

            RegistersCollection.DefineRegister((long)Registers.WakeUpEventMask);
        }

        private readonly STM32_EXTICore core;
        private readonly int numberOfLines;
        private readonly Dictionary<int, InternalReceiver> internalReceiversCache;
        private readonly IValueRegisterField[] extiMappings = new IValueRegisterField[GpioPins];

        private const uint InterruptSelectionRegistersCount = 4;
        private const int GpioPins = 16;
        private const int NumberOfPortsPerInterruptSelectionRegister = GpioPins / (int)InterruptSelectionRegistersCount;

        private class InternalReceiver : IGPIOReceiver
        {
            public InternalReceiver(STM32WBA_EXTI parent, int portNumber)
            {
                this.parent = parent;
                this.portNumber = portNumber;
                this.state = new bool[GpioPins];
            }

            public void OnGPIO(int pinNumber, bool value)
            {
                if(pinNumber >= GpioPins)
                {
                    parent.Log(LogLevel.Error, "GPIO port {0}, pin {1}, is not supported. Up to {2} pins are supported", portNumber, pinNumber, GpioPins);
                    return;
                }
                parent.Log(LogLevel.Noisy, "GPIO port {0}, pin {1}, raised IRQ: {2}", portNumber, pinNumber, value);
                state[pinNumber] = value;

                UpdateGPIO(pinNumber);
            }

            public void UpdateGPIO(int pinNumber)
            {
                if((int)parent.extiMappings[pinNumber].Value == portNumber)
                {
                    var value = state[pinNumber];
                    if(parent.core.CanSetInterruptValue((byte)pinNumber, value, out var _))
                    {
                        parent.core.UpdatePendingValue((byte)pinNumber, true);
                        parent.Connections[pinNumber].Set(true);
                    }
                }
            }

            public void Reset()
            {
                // IRQs are cleared on Parent reset
                // Don't clear `state` array here - as it represents the state of input signals, and is not a property of this peripheral
                // The state can only be cleared when the input signal is reset - but it's not controlled by us, but by the peripheral connected to OnGPIO (IRQ/GPIO line)
                // and since peripherals with connected GPIOs will naturally unset them in their Reset, the state array won't contain stale data
            }

            // The state is recorded, so it's possible to update the GPIO state when changing source peripheral
            // since this peripheral is effectively a mux - when changing input source, it's needed to get the other source's value
            private readonly bool[] state;
            private readonly STM32WBA_EXTI parent;
            private readonly int portNumber;
        }


        private enum Registers
        {
            RaisingTriggerSelection     = 0x0,  // EXTI_RTSR1
            FallingTriggerSelection     = 0x4,  // EXTI_FTSR1
            SoftwareInterruptEvent      = 0x8,  // EXTI_SWIER1
            RaisingTriggerPending       = 0xc,  // EXTI_RPR1
            FallingTriggerPending       = 0x10, // EXTI_FPR1
            SecurityConfiguration       = 0x14, // EXTI_SECCFGR1
            PrivilegeConfiguration      = 0x18, // EXTI_PRIVCFGR1
            ExternalInterruptSelection1 = 0x60, // EXTI_EXTICR1
            ExternalInterruptSelection2 = 0x64, // EXTI_EXTICR2
            ExternalInterruptSelection3 = 0x68, // EXTI_EXTICR3
            ExternalInterruptSelection4 = 0x6c, // EXTI_EXTICR4
            Lock                        = 0x70, // EXTI_LOCKR
            WakeUpInterruptMask         = 0x80, // EXTI_IMR1
            WakeUpEventMask             = 0x84, // EXTI_EMR1
        }
    }
}
