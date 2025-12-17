/**
 * ST7789 LCD controller compatible with 8/16-bit GPIO/SPI
 * Comes with a keyboard and rotary encoder knob.
 * (C)-2025 Gissio
 * 
 * SPI assumes 16-bit data is sent as two 8-bit transmissions.
 *
 * GPIO input lines:
 *   0: RESX (Reset)
 *   1: DCX (Data/Command)
 *   2: WRX8 (Write strobe for 8-bit parallel data)
 *   3: WRX16 (Write strobe for 16-bit parallel data)
 *   16-23: 8-bit parallel data
 *   16-31: 16-bit parallel data
 *
 * GPIO output lines:
 *   keyEnter
 *   keySpace
 *   keyBackSpace
 *   keyRight
 *   keyLeft
 *   keyUp
 *   keyDown
 *   knobA (mapped to PageUp/PageDown keys)
 *   knobB (mapped to PageUp/PageDown keys)
 *
 * The rotary encoder knob is controlled with the PageUp/PageDown keys.
 */

using System;
using System.Collections.Generic;

using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Input;
using Antmicro.Renode.Peripherals.SPI;

namespace Antmicro.Renode.Peripherals.Video
{
    public class ST7789 : AutoRepaintingVideo, IKeyboard, ISPIPeripheral, IGPIOReceiver, IGPIOSender
    {
        private int width;
        private int height;
        private int rotation;
        private byte[] framebuffer;
        private int framebufferData;
        private int parallelData;

        private bool isData;
        private byte command;
        private int dataIndex;

        private bool isSleepOff;
        private bool isDisplayOn;
        private int xStart;
        private int yStart;
        private int xEnd;
        private int yEnd;
        private int x;
        private int y;
        private int madCtl;

        public GPIO keyEnter { get; }
        public GPIO keySpace { get; }
        public GPIO keyBackSpace { get; }
        public GPIO keyRight { get; }
        public GPIO keyLeft { get; }
        public GPIO keyUp { get; }
        public GPIO keyDown { get; }
        public GPIO knobA { get; }
        public GPIO knobB { get; }

        private bool keyEnterInvert;
        private bool keySpaceInvert;
        private bool keyBackSpaceInvert;
        private bool keyRightInvert;
        private bool keyLeftInvert;
        private bool keyUpInvert;
        private bool keyDownInvert;

        private int knobState;

        public ST7789(IMachine machine,
            int width, int height, int rotation,
            bool keyEnterInvert = true,
            bool keySpaceInvert = true,
            bool keyBackSpaceInvert = true,
            bool keyRightInvert = true,
            bool keyLeftInvert = true,
            bool keyUpInvert = true,
            bool keyDownInvert = true) : base(machine)
        {
            // Display
            this.rotation = rotation;
            switch (rotation)
            {
                case 0:
                case 180:
                    this.width = width;
                    this.height = height;
                    break;

                case 90:
                case 270:
                    this.height = width;
                    this.width = height;
                    break;
            }
            framebuffer = new byte[this.width * this.height * 2];

            Reconfigure(this.width, this.height, PixelFormat.RGB565);

            isData = false;
            command = 0;
            dataIndex = 0;

            // Keyboard
            this.keyEnterInvert = keyEnterInvert;
            this.keySpaceInvert = keySpaceInvert;
            this.keyBackSpaceInvert = keyBackSpaceInvert;
            this.keyRightInvert = keyRightInvert;
            this.keyLeftInvert = keyLeftInvert;
            this.keyUpInvert = keyUpInvert;
            this.keyDownInvert = keyDownInvert;

            keyEnter = new GPIO();
            keySpace = new GPIO();
            keyBackSpace = new GPIO();
            keyRight = new GPIO();
            keyLeft = new GPIO();
            keyUp = new GPIO();
            keyDown = new GPIO();
            knobA = new GPIO();
            knobB = new GPIO();
        }

        public override void Reset()
        {
            // Display
            Array.Clear(framebuffer);

            isSleepOff = false;
            isDisplayOn = false;

            // Keyboard
            keyEnter.Set(keyEnterInvert);
            keySpace.Set(keySpaceInvert);
            keyBackSpace.Set(keyBackSpaceInvert);
            keyRight.Set(keyRightInvert);
            keyLeft.Set(keyLeftInvert);
            keyUp.Set(keyUpInvert);
            keyDown.Set(keyDownInvert);
            knobA.Set(false);
            knobB.Set(false);
        }

        // Display
        protected override void Repaint()
        {
            if (!isSleepOff || !isDisplayOn)
            {
                Array.Clear(buffer);
            }
            else
            {
                Buffer.BlockCopy(framebuffer, 0,
                                 buffer, 0,
                                 framebuffer.Length);
            }
        }

        private void writeFramebuffer(int value)
        {
            int x = this.x;
            int y = this.y;

            bool mv = (madCtl & 0b00100000) != 0;
            if (mv)
            {
                (x, y) = (y, x);
            }

            bool mx = (madCtl & 0b01000000) != 0;
            if (mx)
            {
                x = width - x;
            }

            bool my = (madCtl & 0b10000000) != 0;
            if (my)
            {
                y = height - y;
            }

            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                int index = 2 * (y * width + x);
                framebuffer[index + 0] = (byte)(value >> 0);
                framebuffer[index + 1] = (byte)(value >> 8);
            }

            this.x++;
            if (this.x > xEnd)
            {
                this.x = xStart;
                this.y++;
                if (this.y > yEnd)
                {
                    this.y = yStart;
                }
            }
        }

        private byte WriteDisplay(int value, bool is16Bit)
        {
            if (!isData)
            {
                command = (byte)value;
                dataIndex = 0;

                switch (command)
                {
                    case 0x10:
                        // SLPIN
                        isSleepOff = false;

                        break;

                    case 0x11:
                        // SLPOUT
                        isSleepOff = true;

                        break;

                    case 0x28:
                        // DISPOFF
                        isDisplayOn = false;

                        break;

                    case 0x29:
                        // DISPON
                        isDisplayOn = true;

                        break;

                    case 0x2C:
                        // RAMWR
                        x = xStart;
                        y = yStart;

                        break;
                }

                if (command < 0x2a || command > 0x2c)
                {
                    this.Log(LogLevel.Info, $"Command: 0x{value:X2}");
                }
            }
            else
            {
                switch (command)
                {
                    case 0x2A:
                        // CASET
                        switch (dataIndex)
                        {
                            case 0:
                                xStart = (xStart & 0x00ff) | (value << 8);

                                break;

                            case 1:
                                xStart = (xStart & 0xff00) | (value << 0);

                                break;

                            case 2:
                                xEnd = (xEnd & 0x00ff) | (value << 8);

                                break;

                            case 3:
                                xEnd = (xEnd & 0xff00) | (value << 0);

                                break;
                        }

                        break;

                    case 0x2B:
                        // RASET
                        switch (dataIndex)
                        {
                            case 0:
                                yStart = (yStart & 0x00ff) | (value << 8);

                                break;

                            case 1:
                                yStart = (yStart & 0xff00) | (value << 0);

                                break;

                            case 2:
                                yEnd = (yEnd & 0x00ff) | (value << 8);

                                break;

                            case 3:
                                yEnd = (yEnd & 0xff00) | (value << 0);

                                break;
                        }

                        break;

                    case 0x2C:
                        // RAMWR
                        if (is16Bit)
                        {
                            writeFramebuffer(value);
                        }
                        else
                        {
                            if ((dataIndex & 1) == 0)
                            {
                                framebufferData = value;
                            }
                            else
                            {
                                writeFramebuffer(
                                    (framebufferData << 8) | value
                                    );
                            }
                        }

                        break;

                    case 0x36:
                        // MADCTL
                        if (dataIndex == 0)
                        {
                            switch (rotation)
                            {
                                default:
                                    madCtl = value;

                                    break;

                                case 90:
                                    madCtl = value ^ 0b10100000;

                                    break;

                                case 180:
                                    madCtl = value ^ 0b11000000;

                                    break;

                                case 270:
                                    madCtl = value ^ 0b01100000;

                                    break;
                            }
                        }

                        break;
                }

                if (command < 0x2a || command > 0x2c)
                {
                    this.Log(LogLevel.Info, $"   Data: 0x{value:X2}");
                }

                dataIndex++;
            }

            return 0;
        }

        // GPIO
        public void OnGPIO(int index, bool value)
        {
            switch (index)
            {
                case 0:
                    // Reset
                    if (!value)
                    {
                        Reset();
                    }

                    break;

                case 1:
                    // DC
                    isData = value;

                    break;

                case 2:
                    // Write strobe for 8-bit parallel data
                    if (value)
                    {
                        WriteDisplay(parallelData, false);
                    }

                    break;

                case 3:
                    // Write strobe for 16-bit parallel data
                    if (value)
                    {
                        WriteDisplay(parallelData, true);
                    }

                    break;

                default:
                    // Parallel data
                    int mask = 1 << (index & 0xf);
                    parallelData = (parallelData & ~mask) | (value ? mask : 0);

                    break;
            }
        }

        // SPI
        public byte Transmit(byte value)
        {
            return WriteDisplay(value, false);
        }

        public void FinishTransmission()
        {
        }

        // Keyboard interface
        private static readonly int[] knobTable = new int[]
        {
            0b00,
            0b01,
            0b11,
            0b10
        };

        public void rotateKnob(int direction)
        {
            knobState += direction;
            knobState &= 0b11;

            int knobAB = knobTable[knobState];

            // this.Log(LogLevel.Info, $"Knob: {knobAB}");

            knobA.Set((knobAB & 0b10) != 0);
            knobB.Set((knobAB & 0b01) != 0);
        }

        public void Press(KeyScanCode scanCode)
        {
            switch (scanCode)
            {
                case KeyScanCode.Enter:
                    keyEnter.Set(!keyEnterInvert);

                    break;

                case KeyScanCode.Space:
                    keySpace.Set(!keySpaceInvert);

                    break;

                case KeyScanCode.BackSpace:
                    keyBackSpace.Set(!keyBackSpaceInvert);

                    break;

                case KeyScanCode.Right:
                    keyRight.Set(!keyRightInvert);

                    break;

                case KeyScanCode.Left:
                    keyLeft.Set(!keyLeftInvert);

                    break;

                case KeyScanCode.Up:
                    keyUp.Set(!keyUpInvert);

                    break;

                case KeyScanCode.Down:
                    keyDown.Set(!keyDownInvert);

                    break;

                case KeyScanCode.PageUp:
                    rotateKnob(-1);

                    break;

                case KeyScanCode.PageDown:
                    rotateKnob(1);

                    break;
            }
        }

        public void Release(KeyScanCode scanCode)
        {
            switch (scanCode)
            {
                case KeyScanCode.Enter:
                    keyEnter.Set(keyEnterInvert);

                    break;

                case KeyScanCode.Space:
                    keySpace.Set(keySpaceInvert);

                    break;

                case KeyScanCode.BackSpace:
                    keyBackSpace.Set(keyBackSpaceInvert);

                    break;

                case KeyScanCode.Right:
                    keyRight.Set(keyRightInvert);

                    break;

                case KeyScanCode.Left:
                    keyLeft.Set(keyLeftInvert);

                    break;

                case KeyScanCode.Up:
                    keyUp.Set(keyUpInvert);

                    break;

                case KeyScanCode.Down:
                    keyDown.Set(keyDownInvert);

                    break;
            }
        }
    }
}
