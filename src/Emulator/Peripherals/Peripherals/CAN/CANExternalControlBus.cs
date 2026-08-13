//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;

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
            untransferedFrames = new ConcurrentQueue<CANMessageFrame>();

            machine.StateChanged += (machine, state) => HandleStateChange(machine, state);
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
            if(this.machine.IsPaused)
            {
                this.DebugLog("Machine is paused, the frame will be enqueued and send when unpausing.");
                untransferedFrames.Enqueue(frame);
                return;
            }

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

        public void HandleStateChange(IMachine _, MachineStateChangedEventArgs state)
        {
            switch(state.CurrentState)
            {
            case MachineStateChangedEventArgs.State.Started:
                FlushQueue();
                break;
            case MachineStateChangedEventArgs.State.Disposed:
                if(untransferedFrames.Count > 0)
                {
                    this.WarningLog("Disposing peripheral with {0} messages queued. Machine was not started since they were sent.", untransferedFrames.Count);
                }
                break;
            }
        }

        public void FlushQueue()
        {
            while(untransferedFrames.TryDequeue(out var frameToTransfer))
            {
                this.DebugLog("Sending frame {0}", frameToTransfer);
                FrameSent(frameToTransfer);
            }
        }

        public event Action<CANMessageFrame> FrameSent;

        public event Action<CANMessageFrame> ReceivedMessage;

        private readonly IMachine machine;

        private readonly ConcurrentQueue<CANMessageFrame> untransferedFrames;
    }
}

