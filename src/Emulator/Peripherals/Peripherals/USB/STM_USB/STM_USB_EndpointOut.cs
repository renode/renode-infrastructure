//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.USB;

public partial class STM_USB
{
    private class EndpointOut : Endpoint, IUSBPipeWrite
    {
        public EndpointOut(STM_USB parent, uint idx) : base(parent, (ulong)Registers.DeviceEpOutRegisters + 0x20 * idx, false, (byte)idx)
        {
            // Intentionally left empty
        }

        public bool MaskMatches(EndpointInterruptMask mask)
        {
            return (mask.TransferComplete && transferCompleteInterruptFlag.Value)
                || (mask.EndpointDisabled && endpointDisabledInterruptFlag.Value)
                || (mask.SetupPhaseDone && setupPhaseDoneInterruptFlag.Value);
        }

        public void SetInterruptsForPacket(RxPacket packet)
        {
            if(packet.Data.Length > 0)
            {
                transferCompleteInterruptFlag.Value = true;
            }
            if(packet.IsSetup)
            {
                setupPhaseDoneInterruptFlag.Value = true;
                setupPacketReceivedInterruptFlag.Value = true;
            }
        }

        public void Write(byte[] data)
        {
            if(!Active || nakStatusFlag.Value)
            {
                parent.WarningLog("Out endpoint #{0} - host tried to write out when endpoint is inactive", endpointNumber);
                return;
            }
            if(data.Length > (int)maxPacketSize.Value)
            {
                parent.WarningLog("Out endpoint #{0} - host didn't respect max packet size, ignoring packet", endpointNumber);
                return;
            }
            parent.DebugLog("Out endpoint #{0} - writing out packet from host - {1}", endpointNumber, data.ToLazyHexString());
            parent.rxFifoPackets.Enqueue(new RxPacket
            {
                IsSetup = false,
                Endpoint = endpointNumber,
                Data = data
            });
            parent.UpdateInterrupts();
        }

        public void SetupPacketWrite(SetupPacket packet)
        {
            if(!Active)
            {
                parent.WarningLog("Out endpoint #{0} - host tried to write setup when endpoint is inactive", endpointNumber);
                return;
            }
            parent.DebugLog("Out endpoint #{0} - writing setup packet from host - {1}", endpointNumber, packet);
            parent.rxFifoPackets.Enqueue(new RxPacket
            {
                IsSetup = true,
                Endpoint = endpointNumber,
                Data = Packet.Encode(packet)
            });
            parent.UpdateInterrupts();
        }
    }
}
