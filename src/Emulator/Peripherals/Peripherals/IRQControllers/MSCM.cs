//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public sealed class MSCM : IIRQController, INumberedGPIOOutput, IWordPeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public MSCM(IMachine machine)
        {
            sysbus = machine.GetSystemBus(this);
            routingTable = new bool[NumberOfInterrupts * 2];
            destinations = new Destination[NumberOfInterrupts * 2];
            interProcessorInterrupts = new GPIO[8];
            for(var i = 0; i < interProcessorInterrupts.Length; i++)
            {
                interProcessorInterrupts[i] = new GPIO();
            }

            Connections = new IGPIORedirector(232, HandleIRQConnect, HandleIRQDisconnect);
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }

        public ushort ReadWord(long offset)
        {
            if(offset >= RoutingControlStart && offset < RoutingControlEnd)
            {
                var interruptNo = (offset - RoutingControlStart) / 2;
                lock(routingTable)
                {
                    return (ushort)((routingTable[interruptNo] ? 1u : 0u) + (routingTable[NumberOfInterrupts + interruptNo] ? 2u : 0u));
                }
            }
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteWord(long offset, ushort value)
        {
            if(offset >= RoutingControlStart && offset < RoutingControlEnd)
            {
                var interruptNo = (offset - RoutingControlStart) / 2;
                var cpu0 = (value & 1) != 0;
                var cpu1 = (value & 2) != 0;
                lock(routingTable)
                {
                    this.DebugLog("Interrupt no {0} set to be routed to CPU0: {1}, CPU1: {2}", interruptNo, cpu0, cpu1);
                    routingTable[interruptNo] = cpu0;
                    routingTable[NumberOfInterrupts + interruptNo] = cpu1;
                }
            }
            else
            {
                this.LogUnhandledWrite(offset, value);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Register)offset)
            {
            case Register.CP0:
                return HandleCPRead(0);
            case Register.CP1:
                return HandleCPRead(1);
            }
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Register)offset)
            {
            case Register.CP0:
                HandleCPWrite(0, value);
                break;
            case Register.CP1:
                HandleCPWrite(1, value);
                break;
            case Register.GenerateInterrupt:
                HandleGenerateInterrupt(value);
                break;
            }
            this.LogUnhandledWrite(offset, value);
        }

        public long Size
        {
            get
            {
                return 0x1000;
            }
        }

        private void HandleIRQConnect(int sourceNumber, IGPIOReceiver destination, int destinationNumber)
        {
            if(sourceNumber > 223)
            {
                lock(interProcessorInterrupts)
                {
                    if(sourceNumber - 224 >= interProcessorInterrupts.Length)
                    {
                        throw new ArgumentException("Invalid source number.");
                    }
                    interProcessorInterrupts[sourceNumber - 224].Connect(destination, destinationNumber);
                }
                return;
            }
            lock(routingTable)
            {
                destinations[sourceNumber] = new Destination(destination, destinationNumber);
            }
        }

        private void HandleIRQDisconnect(int sourceNumber)
        {
            lock(routingTable)
            {
                destinations[sourceNumber] = new Destination();
            }
        }

        public void OnGPIO(int number, bool value)
        {
            lock(routingTable)
            {
                if(routingTable[number])
                {
                    destinations[number].OnGPIO(value);
                }
            }
        }

        public void Reset()
        {
            Array.Clear(routingTable, 0, routingTable.Length);
        }

        private void HandleGenerateInterrupt(uint value)
        {
            var interruptId = (int)value & 3;
            var targetListField = (value >> 24) & 3;
            var cpuTargetList = (uint)((value >> 16) & 3);
            if(!sysbus.TryGetCurrentCPU(out var askingCpu))
            {
                this.Log(LogLevel.Warning, "Generate interrupt write by not a CPU - ignoring.");
                return;
            }
            switch(targetListField)
            {
            case 1:
                cpuTargetList = 2 - askingCpu.MultiprocessingId;
                break;
            case 2:
                cpuTargetList = askingCpu.MultiprocessingId + 1;
                break;
            }
            lock(interProcessorInterrupts)
            {
                if((cpuTargetList & 1) != 0)
                {
                    interProcessorInterrupts[interruptId].Set();
                }
                if((cpuTargetList & 2) != 0)
                {
                    interProcessorInterrupts[4 + interruptId].Set();
                }
            }
        }

        private uint HandleCPRead(int cpuNumber)
        {
            lock(interProcessorInterrupts)
            {
                var returnValue = 0u;
                for(var i = 0; i < 4; i++)
                {
                    returnValue |= interProcessorInterrupts[4 * cpuNumber + i].IsSet ? 1u : 0u;
                    returnValue <<= 1;
                }
                returnValue >>= 1;
                return returnValue;
            }
        }

        private void HandleCPWrite(int cpuNumber, uint value)
        {
            lock(interProcessorInterrupts)
            {
                for(var i = 0; i < 4; i++)
                {
                    if((value & 1) != 0)
                    {
                        interProcessorInterrupts[4 * cpuNumber + i].Unset();
                    }
                    value >>= 1;
                }
            }
        }

        private Destination[] destinations;
        private readonly bool[] routingTable;
        private readonly GPIO[] interProcessorInterrupts;
        private readonly IBusController sysbus;

        private const int NumberOfInterrupts = 112;
        private const int RoutingControlStart = 0x880;
        private const int RoutingControlEnd = RoutingControlStart + NumberOfInterrupts*2;

        private enum Register
        {
            CP0 = 0x800,
            CP1 = 0x804,
            GenerateInterrupt = 0x820
        }

        private struct Destination
        {
            public Destination(IGPIOReceiver receiver, int destinationNo)
            {
                this.receiver = receiver;
                this.destinationNo = destinationNo;
            }

            public void OnGPIO(bool state)
            {
                receiver.OnGPIO(destinationNo, state);
            }

            public readonly IGPIOReceiver receiver;
            public readonly int destinationNo;
        }
    }
}

