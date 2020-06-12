//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Input
{
    public class PS2Mouse : IPS2Peripheral, IRelativePositionPointerInput
    {
        public PS2Mouse()
        {
            data = new Queue<byte>();
            Reset();
        }

        public byte Read()
        {
            if(data.Count > 0)
            {
                var result = data.Dequeue();
                NotifyParent();
                return result;
            }
            this.Log(LogLevel.Warning, "Attempted to read while no data in buffer. Returning 0.");
            return 0;
        }

        public void Write(byte value)
        {
            if(lastCommand == Command.None)
            {
                switch((Command)value)
                {
                case Command.Reset:
                    AckAndReset();
                    break;
                case Command.GetDeviceId:
                    lock(data)
                    {
                        SendAck();
                        data.Enqueue(0x00);
                    }
                    break;
                case Command.SetSampleRate:
                case Command.SetResolution:
                    lastCommand = (Command)value;
                    SendAck();
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unhandled PS2 command: {0}", (Command)value);
                    break;
                }
            }
            else
            {
                switch(lastCommand)
                {
                case Command.SetSampleRate:
                case Command.SetResolution:
                    SendAck();
                    break;
                }
                lastCommand = Command.None;
            }
        }

        public void MoveBy(int x, int y)
        {
            byte dataByte = buttonState;
            y = -y;
            if(x < 0)
            {
                dataByte |= 1 << 4;
            }
            if(y < 0)
            {
                dataByte |= 1 << 5;
            }

            x = x.Clamp(-255, 255);
            y = y.Clamp(-255, 255);

            lock(data)
            {
                data.Enqueue(dataByte);
                data.Enqueue((byte)x);
                data.Enqueue((byte)y);
            }
            NotifyParent();
        }

        public void Press(MouseButton button = MouseButton.Left)
        {
            buttonState |= (byte)button;
            SendButtonState();
        }

        public void Release(MouseButton button = MouseButton.Left)
        {
            buttonState &= (byte) ~button;
            SendButtonState();
        }

        public void Reset()
        {
            buttonState = 0x08;
            data.Clear();
        }

        public IPS2Controller Controller { get; set; }

        private void SendButtonState()
        {
            lock(data)
            {
                data.Enqueue(buttonState);
                data.Enqueue(0x00);
                data.Enqueue(0x00);
            }
            NotifyParent();
        }

        private void AckAndReset()
        {
            Reset();
            lock(data)
            {
                SendAck();
                data.Enqueue((byte) Command.SelfTestPassed);
                data.Enqueue(0x00);
            }
        }

        private void SendAck()
        {
            data.Enqueue((byte)Command.Acknowledge);
            NotifyParent();
        }

        private void NotifyParent()
        {
            if(Controller != null)
            {
                if(data.Count > 0)
                {
                    Controller.Notify();
                }
            }
            else
            {
                this.Log(LogLevel.Noisy, "PS2 device not connected to any controller issued an update.");
            }
        }

        private Command lastCommand;
        private byte buttonState;
        private readonly Queue<byte> data;

        enum Command : byte
        {
            Reset = 0xFF,
            SetSampleRate = 0xF3,
            GetDeviceId = 0xF2,
            SetResolution = 0xE8,
            Acknowledge = 0xFA,
            SelfTestPassed = 0xAA,
            None = 0x00,
        }
    }
}
