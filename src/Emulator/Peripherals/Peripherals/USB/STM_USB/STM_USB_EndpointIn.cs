//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.USB;

public partial class STM_USB
{
    private class EndpointIn : Endpoint, IUSBPipeRead
    {
        public EndpointIn(STM_USB parent, uint idx, IFlagRegisterField fifoEmptyInterruptMaskFlag) : base(parent, (ulong)Registers.DeviceEpInRegisters + 0x20 * idx, true, (byte)idx)
        {
            this.fifoEmptyInterruptMaskFlag = fifoEmptyInterruptMaskFlag;
        }

        public bool MaskMatches(EndpointInterruptMask mask)
        {
            return (mask.TransferComplete && transferCompleteInterruptFlag.Value)
                || (mask.EndpointDisabled && endpointDisabledInterruptFlag.Value)
                || (fifoEmptyInterruptMaskFlag.Value && txFifoEmptyInterruptFlag.Value);
        }

        public void Flush()
        {
            fifo.Clear();
        }

        public void SubmitData(uint data)
        {
            var bytesToTake = Math.Min(FifoAvailableSpace, sizeof(uint));
            var bytes = BitHelper.GetBytesFromValue(data, sizeof(uint), reverse: true)[0..(int)bytesToTake];
            fifo.EnqueueRange(bytes);
            parent.DebugLog("In endpoint #{0} - got {1:X08} from device, taking {2}", endpointNumber, data, bytesToTake);

            var hasPacket = fifo.Count >= (int)transferSizeField.Value;
            if(hasPacket)
            {
                parent.DebugLog("In endpoint #{0} - got full packet from device, notifying host", endpointNumber);
                NewPacket?.Invoke();
            }
            txFifoEmptyInterruptFlag.Value = !hasPacket;
            parent.UpdateInterrupts();
        }

        public bool TryRead(out byte[] data)
        {
            data = null;
            if(!Active)
            {
                parent.WarningLog("In endpoint #{0} - host tried to read when endpoint is inactive", endpointNumber);
                return false;
            }
            if(!endpointEnabledFlag.Value || nakStatusFlag.Value)
            {
                parent.DebugLog("In endpoint #{0} - host tried to read an empty endpoint", endpointNumber);
                return false;
            }
            parent.DebugLog("In endpoint #{0} - host tried to read at {1}/{2} (max: {3})", endpointNumber, fifo.Count, transferSizeField.Value, maxPacketSize.Value);
            if(fifo.Count < (int)transferSizeField.Value)
            {
                return false;
            }
            data = fifo.DequeueRange((int)transferSizeField.Value);
            if(fifo.Count == 0)
            {
                txFifoEmptyInterruptFlag.Value = true;
                transferCompleteInterruptFlag.Value = true;
                endpointEnabledFlag.Value = false;
            }
            parent.UpdateInterrupts();
            return true;
        }

        public event Action NewPacket;

        protected override void OnEndpointEnable()
        {
            if(transferSizeField.Value == 0)
            {
                NewPacket?.Invoke();
            }
            txFifoEmptyInterruptFlag.Value = fifo.Count < (int)transferSizeField.Value;
            parent.UpdateInterrupts();
        }

        protected override ulong FifoAvailableSpace => (ulong)Math.Max(0, (int)transferSizeField.Value - fifo.Count);

        private readonly Queue<byte> fifo = new();

        private readonly IFlagRegisterField fifoEmptyInterruptMaskFlag;
    }
}
