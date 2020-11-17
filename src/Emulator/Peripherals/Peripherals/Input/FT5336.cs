//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using System.Linq;
using Antmicro.Renode.Utilities;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Input
{
    /// <summary>
    /// This class differs from FT5x06. Although it was based on FT5x06 datasheet, it is inconsistent
    /// with the Linux driver we used to create FT5x06.cs.
    /// This name is used because of STM32F7 Cube, providing such a driver.
    /// </summary>
    public sealed class FT5336 : II2CPeripheral, IAbsolutePositionPointerInput
    {
        public FT5336(bool isRotated = false)
        {
            this.isRotated = isRotated;
            IRQ = new GPIO();
            Reset();
        }

        public void Reset()
        {
            IRQ.Unset();
            currentReturnValue = null;
            lastWriteRegister = 0;
            for(ushort i = 0; i < touchedPoints.Length; ++i)
            {
                touchedPoints[i] = new TouchedPoint
                {
                    Type = PointType.Reserved,
                    X = 0,
                    Y = 0,
                    Id = i
                };
            }
        }

        public void Write(byte[] data)
        {
            lastWriteRegister = (Registers)data[0];
            if(lastWriteRegister < Registers.TouchEndRegister && lastWriteRegister >= Registers.TouchBeginRegister)
            {
                PrepareTouchData((byte)((lastWriteRegister - Registers.TouchBeginRegister) % TouchInfoSize), (lastWriteRegister - Registers.TouchBeginRegister) / TouchInfoSize);
                return;
            }
            switch(lastWriteRegister)
            {
            case Registers.TouchDataStatus:
                SetReturnValue((byte)touchedPoints.Count(x => x.Type == PointType.Contact || x.Type == PointType.Down));
                break;
            case Registers.InterruptStatus:
                break;
            case Registers.ChipVendorId:
                SetReturnValue(ChipVendorId);
                break;
            default:
                this.Log(LogLevel.Warning, "Unhandled write to offset 0x{0:X}{1:X}.", lastWriteRegister,
                    data.Length == 1 ? String.Empty : ", values {0}".FormatWith(data.Skip(1).Select(x => "0x" + x.ToString("X")).Stringify(", ")));
                break;
            }
        }

        public byte[] Read(int count)
        {
            return currentReturnValue.Take(count).ToArray();
        }

        public void FinishTransmission()
        {
        }

        public void MoveTo(int x, int y)
        {
            if(!isRotated)
            {
                touchedPoints[0].X = (ushort)x;
                touchedPoints[0].Y = (ushort)y;
            }
            else
            {
                touchedPoints[0].X = (ushort)y;
                touchedPoints[0].Y = (ushort)x;
            }
            if(touchedPoints[0].Type == PointType.Down || touchedPoints[0].Type == PointType.Contact)
            {
                this.NoisyLog("Moving the pointer at {0}x{1}", touchedPoints[0].X, touchedPoints[0].Y);
                touchedPoints[0].Type = PointType.Contact;
            }
            if(touchedPoints.Any(b => b.Type == PointType.Down || b.Type == PointType.Contact))
            {
                IRQ.Blink();
            }
        }

        public void Press(MouseButton button = MouseButton.Left)
        {
            this.NoisyLog("Pressing the pointer at {0}x{1}", touchedPoints[0].X, touchedPoints[0].Y);
            touchedPoints[0].Type = PointType.Contact;
            IRQ.Blink();
        }

        public void Release(MouseButton button = MouseButton.Left)
        {
            this.NoisyLog("Releasing the pointer at {0}x{1}", touchedPoints[0].X, touchedPoints[0].Y);
            touchedPoints[0].Type = PointType.Up;
            IRQ.Blink();
        }

        public int MaxX { get; set; }

        public int MaxY { get; set; }

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

        public GPIO IRQ { get; private set; }

        private void PrepareTouchData(byte offset, int pointNumber)
        {
            var queue = new Queue<byte>();

            switch((TouchDataRegisters)offset)
            {
            case TouchDataRegisters.TouchXHigh:
                queue.Enqueue((byte)(((int)touchedPoints[pointNumber].Type << 6) | (touchedPoints[pointNumber].X.HiByte() & 0xF)));
                goto case TouchDataRegisters.TouchXLow;
            case TouchDataRegisters.TouchXLow:
                queue.Enqueue(touchedPoints[pointNumber].X.LoByte());
                goto case TouchDataRegisters.TouchYHigh;
            case TouchDataRegisters.TouchYHigh:
                queue.Enqueue((byte)((touchedPoints[pointNumber].Id << 4) | (touchedPoints[pointNumber].Y.HiByte() & 0xF)));
                goto case TouchDataRegisters.TouchYLow;
            case TouchDataRegisters.TouchYLow:
                queue.Enqueue(touchedPoints[pointNumber].Y.LoByte());
                goto case TouchDataRegisters.TouchWeight;
            case TouchDataRegisters.TouchWeight:
                queue.Enqueue(0);
                goto case TouchDataRegisters.TouchMisc;
            case TouchDataRegisters.TouchMisc:
                queue.Enqueue(0);
                break;
            default:
                throw new Exception("Should not reach here.");
            }
            SetReturnValue(queue.ToArray());
        }

        private void SetReturnValue(params byte[] bytes)
        {
            currentReturnValue = bytes;
        }

        private byte[] currentReturnValue;
        private Registers lastWriteRegister;
        private readonly bool isRotated;

        private readonly TouchedPoint[] touchedPoints = new TouchedPoint[5];

        private const byte ChipVendorId = 0x51;

        private const int TouchInfoSize = TouchDataRegisters.TouchMisc - TouchDataRegisters.TouchXHigh + 1;

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
            Contact = 2,
            Reserved = 3
        }

        private enum TouchDataRegisters
        {
            TouchXHigh = 0x0,
            TouchXLow = 0x1,
            TouchYHigh = 0x2,
            TouchYLow = 0x3,
            TouchWeight = 0x4,
            TouchMisc = 0x5,
        }

        private enum Registers
        {
            GestureId = 0x1,
            TouchDataStatus = 0x2,
            TouchBeginRegister = 0x3,
            TouchEndRegister = 0x21,
            InterruptStatus = 0xA4,
            ChipVendorId = 0xA8
        }
    }
}

