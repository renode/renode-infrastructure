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
    public class FusionF0710A : II2CPeripheral, IAbsolutePositionPointerInput
    {
        public FusionF0710A()
        {
            IRQ = new GPIO();
        }
               
        public void Write(byte[] data)
        {
            this.DebugLog("Writing {0}.", data.Select(x => x.ToString()).Stringify());
            IRQ.Unset();
            if(data.Length > 0)
            {
                switch((Command)data[0])
                {
                case Command.VersionInfoLow:
                case Command.VersionInfo:
                    //just set command. Here to prevent default case.
                    break;
                case Command.Reset:
                    Reset();
                    break;
                case Command.ScanComplete:
                    IRQ.Unset();
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unknown write data: {0}.", data.Select(x => x.ToString("X")).Stringify());
                    break;
                }            
                lastCommand = (Command)data[0];
            }

            PressAgainIfNeeded();           
        }

        public byte[] Read(int count)
        {
            byte[] returnValue;
            switch(lastCommand)
            {
            case Command.VersionInfoLow:
                returnValue = new []{ versionInfoLo };
                break;
            case Command.VersionInfo:
                returnValue = new []{ versionInfo };
                break;
            case Command.Unset:
            case Command.Reset:
            case Command.ScanComplete:
            default:
                returnValue = currentRetValue ?? new byte[12];
                this.DebugLog("Read returning {0}.", returnValue.Select(x => x.ToString()).Stringify());
                readItAlready = true;
                break;
            }
            lastCommand = Command.Unset;
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
            points[0].X = (ushort)(MaxY - y);
            points[0].Y = (ushort)(MaxX - x);
            //WARNING! X and Y ARE reversed! Intentionaly!
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

        public int MinX
        {
            get
            {
                return 0; 
            }
        }

        public int MinY
        {
            get
            {
                return 0;
            }
        }

        public int MaxX
        {
            get
            {
                return 2275;
            }
        }

        public int MaxY
        {
            get
            {
                return 1275;
            }
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
            var data = new byte[14];
            data[0] = 1;
            for(var i = 0; i < points.Length; i++)
            {
                data[i * 6 + XCoordinateHi] = (byte)(points[i].X >> 8);
                data[i * 6 + XCoordinateLo] = (byte)points[i].X;
                data[i * 6 + YCoordinateHi] = (byte)(points[i].Y >> 8);
                data[i * 6 + YCoordinateLo] = (byte)points[i].Y;
                data[i * 6 + PointPressure] = (byte)(points[i].Type == PointType.Down ? 1 : 0);
                data[i * 6 + DigitIdentifier] = (byte)((points[i].Type == PointType.Down ? 0x1 : 0) | 0x10); //as seen on HW

            }
            return data;
        }
            
        private Queue<byte[]> readQueue = new Queue<byte[]>();
        private readonly TouchedPoint[] points = new TouchedPoint[2];
        private byte[] currentRetValue;
        private bool pressed;
        private bool readItAlready;

        private const int XCoordinateHi = 1;
        private const int XCoordinateLo = 2;
        private const int YCoordinateHi = 3;
        private const int YCoordinateLo = 4;
        private const int PointPressure = 5;
        private const int DigitIdentifier = 6;

        private Command lastCommand;

        //Fake data, update when needed.
        private byte versionInfo = 0x1;
        private byte versionInfoLo = 0x2;


        private enum Command : byte
        {
            Unset = 0,
            VersionInfoLow = 0xE,
            VersionInfo = 0xF,
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

