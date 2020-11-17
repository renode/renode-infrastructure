//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;
using System.Threading.Tasks;
using System.Threading;


namespace Antmicro.Renode.Peripherals.Input
{
    public class FT5x06 : II2CPeripheral, IAbsolutePositionPointerInput
    {
        public int MaxX 
        {
            get
            {
                return NumX * 64 - 1;
            }
        }
        public int MaxY
        {
            get
            {
                return NumY * 64 - 1;
            }
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
        public FT5x06 (int maxPoints = 5)
        {
            maxSupportedPoints = maxPoints;
            points = new TouchedPoint[maxSupportedPoints];
            for (ushort i = 0; i< points.Length; ++i) {
                points [i] = new TouchedPoint ()
                {
                    Type=PointType.Reserved,
                    X = 0,
                    Y = 0,
                    Id = i
                };
            }
            IRQ = new GPIO ();
            Reset ();
        }

        public void FinishTransmission()
        {
        }

        public void Reset ()
        {
            currentRetValue = null;
        }
        public void Write (byte[] data)
        {
            this.NoisyLog ("Write {0}",
                        data.Select (x => x.ToString ("X")).Aggregate ((x,y) => x + " " + y));
            switch ((Mode)data [0]) {
            case Mode.Model:
                currentRetValue = new System.Text.ASCIIEncoding ().GetBytes (" EP0700M06*A0G_110610$");
                break;
            case Mode.Data:
                PrepareTouchData ();
                break;
            case Mode.Register:
                byte val;
                switch ((Registers)(data [1] & 0x3f)) {
                case Registers.WorkRegisterThreshold:
                    val = 40;
                    break;
                case Registers.WorkRegisterGain:
                    val = 8;
                    break;
                case Registers.WorkRegisterOffset:
                    val = 0;
                    break;
                case Registers.WorkRegisterReportRate:
                    val = 8;
                    break;
                case Registers.WorkRegisterNumX:
                    val = NumX;
                    break;
                case Registers.WorkRegisterNumY:
                    val = NumY;
                    break;
                default:
                    this.Log (
                        LogLevel.Warning,
                        "Unknown register write: {0}",
                        data.Select (x => x.ToString ("X")).Aggregate ((x,y) => x + " " + y)
                    );
                    val = 0;
                    break;
                }

                currentRetValue = new byte[]{val, GetCRC (data, val)};

                break;
            default:
                this.Log (
                    LogLevel.Warning,
                    "Unknown mode write: {0}",
                    data.Select (x => x.ToString ("X")).Aggregate ((x,y) => x + " " + y)
                    );
                break;
            }
        }

        public byte[] Read (int count)
        {
            this.NoisyLog ("Read {0}", currentRetValue.Select(x=>x.ToString("X")).Aggregate((x,y)=>x+" "+y));
            //       throw new System.NotImplementedException ();
            return currentRetValue;
        }

        public void Press (MouseButton button = MouseButton.Left)
        {
            this.NoisyLog("Pressing the pointer at {0}x{1}", points[0].X, points[0].Y);
            points[0].Type = PointType.On;
            Update();
        }

        public void Release (MouseButton button = MouseButton.Left)
        {
            this.NoisyLog("Releasing the pointer at {0}x{1}", points[0].X, points[0].Y);
            points[0].Type = PointType.Up;
            Update();
        }

        public void MoveTo (int x, int y)
        {
            points[0].X = (ushort)x;
            points[0].Y = (ushort)y;
            if(points[0].Type == PointType.Down || points[0].Type == PointType.On)
            {
                this.NoisyLog("Moving the pointer at {0}x{1}", x, y);
                points[0].Type = PointType.On;
                Update();
            }
        }

        public void Unset ()
        {
            IRQ.Unset ();
        }

        public GPIO IRQ { get; private set; }

        private void Update ()
        {
            IRQ.Blink ();
        }

        private void PrepareTouchData ()
        {
            var data = new byte[26];
            data [0] = 0xAA;
            data [1] = 0xAA;
            data [2] = 26;
            for (int i = 0; i < points.Length; i++) {
                data [i * 4 + 5] = (byte)(((int)points [i].Type << 6) | ((points [i].X.HiByte () & 0xF)));
                data [i * 4 + 6] = points [i].X.LoByte ();
                data [i * 4 + 7] = (byte)(((int)points [i].Id << 4) | ((points [i].Y.HiByte () & 0xF)));
                data [i * 4 + 8] = points [i].Y.LoByte ();

            }

            data [25] = GetFullCRC (data.Take (25));
            currentRetValue = data;

        }

        private byte GetCRC (byte[] input, byte output)
        {
            return (byte)(input [0] ^ input [1] ^ output);
        }

        private byte GetFullCRC (IEnumerable<byte> data)
        {
            byte result = 0;
            foreach (var item in data) {
                result ^= item;
            }
            return result;
        }

        private byte[] currentRetValue;
        private readonly int maxSupportedPoints;
        private TouchedPoint[] points;

        private const byte NumX = 28;
        private const byte NumY = 16;

        private struct TouchedPoint
        {
            public UInt16 X;
            public UInt16 Y;
            public UInt16 Id;
            public PointType Type;
        }

        private enum PointType
        {
            Down = 0,
            Up = 1,
            On = 2,
            Reserved = 3
        }
      
        private enum Registers
        {
            WorkRegisterThreshold = 0x0,
            WorkRegisterReportRate = 0x08,
            WorkRegisterGain = 0x30,
            WorkRegisterOffset = 0x31,
            WorkRegisterNumX = 0x33,
            WorkRegisterNumY = 0x34
        }

        private enum Mode
        {
            Model = 0xBB,
            Data = 0xF9,
            Register = 0xFC,
        }
    }
}

