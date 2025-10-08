//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.DMA
{
    // It manages internal connections between peripherals and eDMAs.
    // It doesn't occupy memory space, but it's represented as an independent block in the platform file.
    public class IMXRT700_DmaMux : IPeripheral, IPeripheralRegister<IMXRT700_eDMA, NumberRegistrationPoint<int>>, ILocalGPIOReceiver
    {
        public IMXRT700_DmaMux()
        {
        }

        public void Reset()
        {
            foreach(var receiver in receivers.Values)
            {
                receiver.Reset();
            }
        }

        public void Register(IMXRT700_eDMA dma, NumberRegistrationPoint<int> id)
        {
            var duplicate = receivers.Values.FirstOrDefault(x => x.DMA == dma);
            if(duplicate != null)
            {
                throw new RegistrationException($"eDMA is already registered at mux line {duplicate.Index}");
            }

            var index = id.Address;
            if(receivers.TryGetValue(index, out var receiver))
            {
                if(receiver.DMA != null)
                {
                    throw new RegistrationException($"Mux line {index} is already occupied");
                }
            }
            else
            {
                receiver = new DmaMux(index);
                receivers[index] = receiver;
            }
            receiver.RegisterDMA(dma);
        }

        public void Unregister(IMXRT700_eDMA dma)
        {
            var receiver = receivers.Values.FirstOrDefault(x => x.DMA == dma);
            if(receiver != null)
            {
                receiver.UnregisterDMA(dma);
            }
        }

        public IGPIOReceiver GetLocalReceiver(int index)
        {
            if(!receivers.TryGetValue(index, out var receiver))
            {
                receiver = new DmaMux(index);
                receivers[index] = receiver;
            }
            return receiver;
        }

        public void EnableRequest(int index, int slot, bool value)
        {
            if(receivers.TryGetValue(index, out var receiver))
            {
                receiver.EnableRequest(slot, value);
                this.DebugLog("Set slot {0} enabled state for eDMA instance behind mux line {1} to {2}", slot, index, value);
            }
            else
            {
                this.WarningLog("Trying to set slot {0} enabled state for eDMA instance behind mux line {1} to {2}, but nothing is registered at mux line {1}", slot, index, value);
            }
        }

        private readonly Dictionary<int, DmaMux> receivers = new Dictionary<int, DmaMux>();

        private class DmaMux : IGPIOReceiver
        {
            public DmaMux(int index)
            {
                Index = index;
            }

            public void Reset()
            {
                slotsEnabledState.Clear();
            }

            public void OnGPIO(int number, bool value)
            {
                // DMA signals are blinked - DMA request is triggered at a rising edge and a falling edge implies transfer done.
                if(!value)
                {
                    return;
                }
                // No need to check return value - if key is not present in dictionary, a default disabled state is assumed.
                slotsEnabledState.TryGetValue(number, out var enabled);
                if(enabled)
                {
                    DMA?.HandlePeripheralRequest(number);
                }
            }

            public void RegisterDMA(IMXRT700_eDMA dma)
            {
                // If we are invoked by the parent class, then we have already confirmed the condition below is true.
                DebugHelper.Assert(DMA == null);
                DMA = dma;
            }

            public void UnregisterDMA(IMXRT700_eDMA dma)
            {
                // If we are invoked by the parent class, then we have already confirmed the condition below is true.
                DebugHelper.Assert(dma == DMA);
                DMA = null;
            }

            public void EnableRequest(int slot, bool value)
            {
                slotsEnabledState[slot] = value;
            }

            public int Index { get; }

            public IMXRT700_eDMA DMA { get; private set; }

            private readonly Dictionary<int, bool> slotsEnabledState = new Dictionary<int, bool>();
        }
    }
}