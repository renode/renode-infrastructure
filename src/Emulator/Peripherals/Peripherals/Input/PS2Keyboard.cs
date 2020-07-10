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

namespace Antmicro.Renode.Peripherals.Input
{
    public class PS2Keyboard : IPS2Peripheral, IKeyboard
    {
        public PS2Keyboard()
        {
            data = new Queue<byte>();
            Reset();
            data.Enqueue((byte)Command.SelfTestPassed);
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
            switch((Command)value)
            {
            case Command.Reset:
                Reset();
                lock(data)
                {
                    SendAck();
                    data.Enqueue((byte)Command.SelfTestPassed);
                }
                break;
            case Command.ReadId:
                lock(data)
                {
                    SendAck();
                    data.Enqueue((byte) (deviceId >> 8));
                    data.Enqueue((byte) (deviceId & 0xff));
                }
                break;
            default:
                this.Log(LogLevel.Warning, "Unhandled PS2 keyboard command: {0}", value);
                break;
            }
        }

        public void Press(KeyScanCode scanCode)
        {
            var key = PS2ScanCodeTranslator.Instance.GetCode(scanCode);
            data.Enqueue((byte)(key & 0x7f));
            NotifyParent();
        }

        public void Release(KeyScanCode scanCode)
        {
            var key = PS2ScanCodeTranslator.Instance.GetCode(scanCode);
            data.Enqueue((byte)Command.Release);
            data.Enqueue((byte)(key & 0x7f));
            NotifyParent();
        }

        public void Reset()
        {
            data.Clear();
        }

        public IPS2Controller Controller { get; set; }

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

        private readonly Queue<byte> data;
        private const ushort deviceId = 0xABBA;

        private enum Command
        {
            Reset = 0xFF,
            Acknowledge = 0xFA,
            ReadId = 0xF2,
            SetResetLeds = 0xED,
            Release = 0xF0,
            SelfTestPassed = 0xAA,
        }
    }
}
