//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;

using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.SPI;

namespace Antmicro.Renode.Peripherals.Video
{
    public class ST7735_SPI : AutoRepaintingVideo, ISPIPeripheral, IGPIOReceiver
    {
        public ST7735_SPI(IMachine machine, int width = 160, int height = 128)
            : base(machine)
        {
            this.width = width;
            this.height = height;

            Reconfigure(width, height, PixelFormat.RGB565);

            framebuffer = new uint[width * height];
            commandBuffer = new List<byte>();
            isCommandMode = false;
            xStart = 0;
            xEnd = (ushort)(width - 1);
            yStart = 0;
            yEnd = (ushort)(height - 1);
            displayOn = false;
            chipSelectAsserted = false;
            dataCommandPin = false;
        }

        public void OnGPIO(int number, bool value)
        {
            // GPIO number 0 is Chip Select (CS)
            // GPIO number 1 is Command/Data pin (CMD)
            // When CMD is high: command mode
            // When CMD is low: data mode

            if(number == 0)
            {
                // Chip Select pin
                if(value && chipSelectAsserted)
                {
                    // CS transition from low to high (deasserted) - finish transmission
                    this.Log(LogLevel.Noisy, "Chip Select deasserted");
                    FinishCommand();
                }

                if(chipSelectAsserted != !value)
                {
                    this.Log(LogLevel.Info, $"LCD CS changed from {chipSelectAsserted} to {!value}");
                }
                chipSelectAsserted = !value; // CS is active low
            }
            else if(number == 1)
            {
                // Data/Command pin
                dataCommandPin = value;
                this.Log(LogLevel.Noisy, "CMD pin: {0} ({1})", value ? "high" : "low", value ? "data" : "command");
            }
        }

        public override void Reset()
        {
            commandBuffer.Clear();
            isCommandMode = false;
            xStart = 0;
            xEnd = (ushort)(width - 1);
            yStart = 0;
            yEnd = (ushort)(height - 1);
            displayOn = false;
            chipSelectAsserted = false;
            dataCommandPin = false;
            for(int i = 0; i < framebuffer.Length; i++)
            {
                framebuffer[i] = 0;
            }
        }

        public void FinishTransmission()
        {
            this.Log(LogLevel.Noisy, "Finishing SPI transmission");
            // Called when chip select is deasserted - finish any pending command
            FinishCommand();
        }

        public byte Transmit(byte data)
        {
            this.Log(LogLevel.Noisy, "SPI Transmit: 0x{0:X2}, CMD pin={1} ({2})", data, dataCommandPin ? "high" : "low", dataCommandPin ? "data" : "command");

            if(!dataCommandPin)
            {
                // CMD pin is low - this is a command byte
                if(isCommandMode)
                {
                    // Already in command mode, finish previous command and start new one
                    FinishCommand();
                }
                commandBuffer.Add(data);
                isCommandMode = true;
            }
            else
            {
                // CMD pin is high - this is data bytes for current command
                if(isCommandMode)
                {
                    commandBuffer.Add(data);
                }
            }

            return 0; // ST7735 typically doesn't return meaningful data during writes
        }

        protected override void Repaint()
        {
            if(!displayOn)
            {
                return;
            }

            // Framebuffer is already in RGB565 format
            // The display backend will handle rendering
            int pixelIndex = 0;
            for(int y = 0; y < height; y++)
            {
                for(int x = 0; x < width; x++)
                {
                    uint pixel = framebuffer[pixelIndex++];
                    buffer[(y * width + x) * 2 + 1] = (byte)(pixel >> 8); // High byte
                    buffer[(y * width + x) * 2] = (byte)(pixel & 0xFF);  // Low byte
                }
            }
        }

        private void FinishCommand()
        {
            if(commandBuffer.Count == 0)
            {
                return;
            }

            byte cmd = commandBuffer[0];
            this.Log(LogLevel.Noisy, "Processing command: 0x{0:X2}", cmd);

            switch((ST7735Commands)cmd)
            {
            case ST7735Commands.CASET:
                // Column Address Set - expects 4 bytes: XS[15:8], XS[7:0], XE[15:8], XE[7:0]
                if(commandBuffer.Count >= 5)
                {
                    xStart = (ushort)((commandBuffer[1] << 8) | commandBuffer[2]);
                    xEnd = (ushort)((commandBuffer[3] << 8) | commandBuffer[4]);
                    this.Log(LogLevel.Debug, "Column set: {0} to {1}", xStart, xEnd);
                }
                break;

            case ST7735Commands.RASET:
                // Row Address Set - expects 4 bytes: YS[15:8], YS[7:0], YE[15:8], YE[7:0]
                if(commandBuffer.Count >= 5)
                {
                    yStart = (ushort)((commandBuffer[1] << 8) | commandBuffer[2]);
                    yEnd = (ushort)((commandBuffer[3] << 8) | commandBuffer[4]);
                    this.Log(LogLevel.Debug, "Row set: {0} to {1}", yStart, yEnd);
                }
                break;

            case ST7735Commands.RAMWR:
                // RAM Write - remaining bytes are pixel data in RGB565 format
                if(commandBuffer.Count >= 3)
                {
                    WriteFrameBuffer();
                }
                break;

            case ST7735Commands.DISPON:
                displayOn = true;
                this.Log(LogLevel.Info, "Display ON");
                break;

            case ST7735Commands.DISPOFF:
                displayOn = false;
                this.Log(LogLevel.Info, "Display OFF");
                break;

            case ST7735Commands.MADCTL:
                // Memory Access Control - just log it
                if(commandBuffer.Count >= 2)
                {
                    this.Log(LogLevel.Debug, "MADCTL: 0x{0:X2}", commandBuffer[1]);
                }
                break;

            case ST7735Commands.COLMOD:
                // Interface Pixel Format - just log it
                if(commandBuffer.Count >= 2)
                {
                    this.Log(LogLevel.Debug, "COLMOD: 0x{0:X2}", commandBuffer[1]);
                }
                break;

            default:
                this.Log(LogLevel.Debug, "ST7735 Command: 0x{0:X2} with {1} bytes", cmd, commandBuffer.Count);
                break;
            }

            commandBuffer.Clear();
            isCommandMode = false;
        }

        private void WriteFrameBuffer()
        {
            // RAMWR: commandBuffer[0] is the command, commandBuffer[1:] are pixel bytes
            // Pixels are in RGB565 format (2 bytes per pixel)
            int pixelIndex = 0;
            int byteIndex = 1;

            // Calculate the number of pixels to write
            int pixelsToWrite = (commandBuffer.Count - 1) / 2;
            int pixelsPerLine = (int)(xEnd - xStart) + 1;

            for(int i = 0; i < pixelsToWrite && byteIndex + 1 < commandBuffer.Count; i++)
            {
                // Read two bytes in big-endian format
                ushort rgb565 = (ushort)((commandBuffer[byteIndex] << 8) | commandBuffer[byteIndex + 1]);
                byteIndex += 2;

                // Calculate position
                int x = (int)xStart + (pixelIndex % pixelsPerLine);
                int y = (int)yStart + (pixelIndex / pixelsPerLine);

                // Check bounds
                if(x < width && y < height)
                {
                    framebuffer[y * width + x] = rgb565;
                }

                pixelIndex++;
            }

            this.Log(LogLevel.Noisy, "Framebuffer write: {0} pixels at ({1},{2})", pixelsToWrite, xStart, yStart);
        }
        private bool isCommandMode;
        private ushort xStart;
        private ushort xEnd;
        private ushort yStart;
        private ushort yEnd;
        private bool displayOn;
        private bool chipSelectAsserted;
        private bool dataCommandPin;

        private readonly uint[] framebuffer;
        private readonly List<byte> commandBuffer;
        private readonly int width;
        private readonly int height;

        private enum ST7735Commands : byte
        {
            CASET = 0x2A,    // Column Address Set
            RASET = 0x2B,    // Row Address Set
            RAMWR = 0x2C,    // RAM Write
            MADCTL = 0x36,   // Memory Access Control
            COLMOD = 0x3A,   // Interface Pixel Format
            DISPON = 0x29,   // Display ON
            DISPOFF = 0x28,  // Display OFF
        }
    }
}
