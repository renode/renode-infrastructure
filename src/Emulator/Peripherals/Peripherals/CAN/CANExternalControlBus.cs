//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.CAN
{
    public class CANExternalControlBus : ICAN
    {
        public CANExternalControlBus(IMachine machine)
        {
            this.machine = machine;
        }

        public void Reset()
        {
            // Intentionally left blank
        }

        public void OnFrameReceived(CANMessageFrame message)
        {
            this.DebugLog("Received frame {0}", message);
            if(ReceivedMessage != null)
            {
                ReceivedMessage.Invoke(message);
            }
            else
            {
                this.WarningLog("Trying to handle received frame when the callback is not connected");
            }
        }

        public void SendFrame(byte[] buffer, uint id)
        {
            var frame = new CANMessageFrame(id, buffer);
            this.DebugLog("Sending frame {0}", frame);

            if(FrameSent != null)
            {
                FrameSent(frame);
            }
            else
            {
                this.WarningLog("Attempted to send CAN frame while not connected to a CAN network");
            }
        }

        public event Action<CANMessageFrame> FrameSent;

        public event Action<CANMessageFrame> ReceivedMessage;

        private readonly IMachine machine;
    }
}

