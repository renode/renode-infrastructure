//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System.Linq;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Input
{
    public class AR1021 : II2CPeripheral, IAbsolutePositionPointerInput
    {                    
        public AR1021()
        {
            IRQ = new GPIO();
        }
               
        public void Write(byte[] data)
        {
            this.DebugLog("Writing {0}.", data.Select(x => x.ToString()).Stringify());
        }

        public byte[] Read(int count)
        {
            var returnValue = currentRetValue ?? new byte[5];
            this.DebugLog("Read returning {0}.", returnValue.Select(x => x.ToString()).Stringify());
            readItAlready = true;
            return returnValue;
        }

        public void FinishTransmission()
        {
        }

        public void Reset()
        {
            readQueue.Clear();
            readItAlready = false;
            pressed = false;
            currentRetValue = null;
        }

        public void MoveTo(int x, int y)
        {
            points[0].X = (ushort)(y);
            points[0].Y = (ushort)(x);
            if(points[0].Type == PointType.Down)
            {
                this.DebugLog("Moving the pointer to {0}x{1}", x, y);
                EnqueueNewPoint();
                IRQ.Set();
            }
        }

        public void Press(MouseButton button = MouseButton.Left)
        {
            pressed = true;
            points[0].Type = PointType.Down;
            this.DebugLog("Button pressed, sending press signal at {0}x{1}.", points[0].X, points[0].Y);
            EnqueueNewPoint();
            IRQ.Set();
        }

        public void Release(MouseButton button = MouseButton.Left)
        {
            this.Log(LogLevel.Noisy, "Sending release signal");
            points[0].Type = PointType.Up;
            pressed = false;
            EnqueueNewPoint();
            IRQ.Set();
            this.DebugLog("Button released at {0}x{1}.", points[0].X, points[0].Y);
        }

        public GPIO IRQ 
        { 
            get;
            private set;
        }

        public int MaxY
        { 
            get{ return 4095; }
        }

        public int MaxX
        { 
            get{ return 4095; }
        }

        public int MinX 
        { 
            get { return 0; } 
        }

        public int MinY
        { 
            get { return 0; }
        }

        private void PressAgainIfNeeded()
        {
            var newPacket = false;
            if(readQueue.Any())
            {
                this.Log(LogLevel.Noisy, "Another packet to send.");
                newPacket = true;
                currentRetValue = readQueue.Dequeue();
                readItAlready = false;
            }
            if(pressed || newPacket || !readItAlready)
            {
                this.Log(LogLevel.Noisy, "Sending signal again at {0}x{1}, state is {2}.", points[0].X, points[0].Y, points[0].Type);
                IRQ.Set();
            }
            else
            {
                this.Log(LogLevel.Noisy, "No more packets.");
                currentRetValue = null;
            }
        }

        private void EnqueueNewPoint()
        {
            var data = PrepareTouchData();
            if(currentRetValue == null)
            {
                this.Log(LogLevel.Noisy, "Setting currentRetValue");
                currentRetValue = data;
                readItAlready = false;
            }
            else
            {
                this.Log(LogLevel.Noisy, "Enqueueing packet");
                readQueue.Enqueue(data);
                if(IRQ.IsSet)
                {
                    this.Log(LogLevel.Noisy, "Forcing IRQ");
                    IRQ.Unset();
                    IRQ.Set();
                }
            }
        }

        private byte[] PrepareTouchData()
        {
            var data = new byte[5];
            data[0] = (byte)(0x80 | ((points[0].Type == PointType.Down) ? 0x1 : 0));
            data[2] = (byte)((points[0].X >> 7) & 0x1f);
            data[1] = (byte)(points[0].X & 0x7f);
            data[4] = (byte)((points[0].Y >> 7) & 0x1f);
            data[3] = (byte)(points[0].Y & 0x7f);
            return data;
        }

        private Queue<byte[]> readQueue = new Queue<byte[]>();
        private readonly TouchedPoint[] points = new TouchedPoint[1];
        private byte[] currentRetValue;
        private bool pressed;
        private bool readItAlready;

        private enum Command : byte
        {
            Reset = 0x10,
            ScanComplete = 0x11
        }

        private struct TouchedPoint
        {
            public UInt16 X;
            public UInt16 Y;
            public PointType Type;
        }

        private enum PointType
        {
            Up = 0,
            Down = 1
        }
    }
}
