//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Video
{
    public class ST7789 : AutoRepaintingVideo, ISPIPeripheral, IGPIOReceiver
    {
        public ST7789(IMachine machine, uint height = 320) : base(machine)
        {
            if(height > FrameMemoryHeight)
            {
                throw new ConstructionException($"Invalid display height: {height} > {FrameMemoryHeight}");
            }

            /* On some devices, the actual display height can be less than the one used in device
             * memory (FrameMemoryHeight). So both values are used in this model as parent.buffer's
             * size matches the actual display dimension (displayHeight) but internal computing for
             * cursor position and vertical scrolling depends on device memory (FrameMemoryHeight).
             */
            displayHeight = height;
            frameMemory = new byte[PixelFormat.RGB565.GetByteCount(FrameMemoryWidth * FrameMemoryHeight)];

            FramesPerVirtualSecond = 60; // Datasheet indicates 60Hz at power on

            currentArgs = new List<byte>();
            currentPixelData = new List<byte>();
            commands = new Dictionary<ST7789Commands, (Action, uint)>
            {
                { ST7789Commands.SWRESET, (Reset, 0) },
                { ST7789Commands.SLPOUT, (ProcessIgnoredCommand, 0) }, // Nothing to do as sleep mode is not implemented
                { ST7789Commands.NORON, (ProcessIgnoredCommand, 0) }, // Only normal mode is supported
                { ST7789Commands.INVON, (ProcessIgnoredCommand, 0) }, // Color inversion not supported, apply colors as they arrive
                { ST7789Commands.DISPON, (ProcessDisplayOn, 0) },
                { ST7789Commands.CASET, (ProcessCaset, 4) },
                { ST7789Commands.RASET, (ProcessRaset, 4) },
                { ST7789Commands.RAMWR, ( ProcessMemoryWrite, 0)},
                { ST7789Commands.VSCRDEF, (ProcessVerticalScrollingDefinition, 6) },
                { ST7789Commands.MADCTL, (ProcessMemoryDataAccessControl, 1) },
                { ST7789Commands.VSCSAD, (ProcessVerticalScrollStartAddr, 2) },
                { ST7789Commands.COLMOD, (ProcessColMod, 1) },
                { ST7789Commands.RAMCTRL, (ProcessRAMControl, 2) },
            };
            dataCommandSelection = DCXPinState.Command;
            Reset();
        }

        public void OnGPIO(int number, bool value)
        {
            dataCommandSelection = value ? DCXPinState.Data : DCXPinState.Command;
        }

        public override void Reset()
        {
            columnAddressSet = (0, FrameMemoryWidth - 1);
            rowAddressSet = (0, FrameMemoryHeight - 1);
            counter = (0, 0);
            scrollStartAddr = 0;
            topFixedArea = 0;
            scrollingArea = FrameMemoryHeight;
            bottomFixedArea = 0;

            ResetCmdContext();
        }

        public byte Transmit(byte data)
        {
            ApplyDataCommandSelection();

            this.NoisyLog("Received data 0x{0:X}, current state {1}", data, state);

            switch(state)
            {
            case DeviceState.WaitForCommand:
                SetNewCommand(data);
                ChangeState(DeviceState.WaitForArguments);
                break;
            case DeviceState.WaitForArguments:
                currentArgs.Add(data);
                ChangeState(DeviceState.WaitForArguments);
                break;
            case DeviceState.WaitForPixel:
                currentPixelData.Add(data);
                ChangeState(DeviceState.WaitForPixel);
                break;
            }

            return 0; // No read command implemented, always return 0
        }

        public void FinishTransmission()
        {
            // Nothing to do, everything is handled by the state machine
        }

        protected override void Repaint()
        {
            var endScrollingArea = topFixedArea + scrollingArea;

            /*                       Memory                          Display
             *                 ┌──────────────────┐          ┌──────────────────┐
             *                 │        TFA       ├─────────►│         TFA      │
             *     topFixedArea├──────────────────┤          ├──────────────────┤topFixedArea
             *                 │       VSA 2      │\        ►│                  │
             *  scrollStartAddr│------------------│ \     /  │                  │
             *                 │                  │  \   /   │                  │
             *                 │                  │   \ /    │                  │
             *                 │                  │    X     │        VSA 1     │
             *                 │                  │   / \    │                  │
             *                 │       VSA 1      │  /   \   │                  │
             *                 │                  │ /     \  │                  │
             *                 │                  │/       \ │------------------│endScrollingArea - (scrollStartAddr - topFixedArea)
             *                 │                  │         ►│       VSA 2      │
             * endScrollingArea├──────────────────┤          ├──────────────────┤endScrollingArea
             *                 │       BFA        ├─────────►│        BFA       │
             *                 └──────────────────┘          └──────────────────┘
             */

            FrameMemoryToDisplay(0, 0, topFixedArea, "TFA");

            FrameMemoryToDisplay(scrollStartAddr, topFixedArea, endScrollingArea - scrollStartAddr, "VSA 1");
            FrameMemoryToDisplay(topFixedArea, endScrollingArea - (scrollStartAddr - topFixedArea), scrollStartAddr - topFixedArea, "VSA 2");

            FrameMemoryToDisplay(endScrollingArea, endScrollingArea, bottomFixedArea, "BFA");
        }

        private void ApplyDataCommandSelection()
        {
            if(dataCommandSelection == DCXPinState.Command && state != DeviceState.WaitForCommand)
            {
                this.DebugLog("Current state is {0} but DCX pin in state {1}, waiting for new command", state, dataCommandSelection);
                ResetCmdContext();
            }
        }

        private void FrameMemoryToDisplay(uint memoryStartLine, uint displayStartLine, uint count, string debugPrefix)
        {
            var bytesPerPixel = (uint)Format.GetByteCount(1);

            if(displayStartLine > displayHeight)
            {
                /* Ignore pixels that are out of the panel */
                return;
            }

            if(displayStartLine + count > displayHeight)
            {
                count = displayHeight - displayStartLine;
            }

            this.DebugLog("Copy {0}: {1} -> {2} ({3} lines)", debugPrefix,
                          (memoryStartLine, memoryStartLine + count), (displayStartLine, displayStartLine + count), count);
            Array.Copy(frameMemory, memoryStartLine * Width * bytesPerPixel,
                       buffer, displayStartLine * Width * bytesPerPixel,
                       count * Width * bytesPerPixel);
        }

        /*
         * State machine:
         *                     Reset
         *                       │
         *                       ▼
         *       ┌──────────────────────────────┐
         *       │                              │
         *       │        WaitForCommand        │◄────────────────────┐
         *       │                              │                     │
         *       └───────────────┬──────────────┘                     │
         *                       │                                    │
         *               Command received                             │
         *                       │                                    │
         *                       ▼                                    │
         *       ┌──────────────────────────────┐                     │
         *       │                              ├─── D/C GPIO low ────┤
         *       │       WaitForArgument        │◄────────┐           │
         *       │                              │         │           │
         *       └───────────────┬──────────┬───┘  not enough args    │
         *                       │          │             │           │
         *          expected args received  └─────────────┘           │
         *                       │                                    │
         *                       ▼                                    │
         *       ┌──────────────────────────────┐                     │
         *       │                              ├─── D/C GPIO low ────┤
         *       │        ProcessCommand        │◄────────┐           │
         *       │                              │         │           │
         *       └───────────────┬──────────┬───┘  cmd != RAMWR       │
         *                       │          │             │           │
         *                 cmd == RAMWR     └─────────────┘           │
         *                       │                                    │
         *                       ▼                                    │
         *       ┌──────────────────────────────┐                     │
         *       │                              ├─── D/C GPIO low ────┤
         *    ┌─►│         WaitForPixel         │◄────────┐           │
         *    │  │                              │         │           │
         *    │  └───────────────┬──────────┬───┘  not enough data    │
         *    │                  │          │             │           │
         *    │             enough data     └─────────────┘           │
         * pixel drawn           │                                    │
         *    │                  ▼                                    │
         *    │  ┌──────────────────────────────┐                     │
         *    │  │                              │                     │
         *    │  │          DrawPixel           ├─── D/C GPIO low ────┘
         *    │  │                              │
         *    │  └──────────────┬───────────────┘
         *    └─────────────────┘
         */
        private void ChangeState(DeviceState newState)
        {
            state = newState;

            switch(newState)
            {
            case DeviceState.WaitForCommand:
                ResetCmdContext();
                break;
            case DeviceState.WaitForArguments:
                if(currentCmd is null)
                {
                    this.ErrorLog("No supported command in progress, trying to fallback to new command");
                    ChangeState(DeviceState.WaitForCommand);
                }
                else if(currentArgs.Count == commands[(ST7789Commands)currentCmd].argsCount)
                {
                    ChangeState(DeviceState.ProcessCommand);
                }
                else
                {
                    this.NoisyLog("Waiting for arguments: {0}/{1}", currentArgs.Count, commands[(ST7789Commands)currentCmd].argsCount);
                }
                break;
            case DeviceState.ProcessCommand:
                this.NoisyLog("Processing command {0} (0x{1:X}) with arguments: '{2}'", currentCmd, (byte)currentCmd,
                              string.Join(", ", currentArgs.Select(b => "0x" + b.ToString("X2"))));
                commands[(ST7789Commands)currentCmd].handler();
                // If handler changed state, do not override it
                if(state == newState)
                {
                    ChangeState(DeviceState.WaitForCommand);
                }
                break;
            case DeviceState.WaitForPixel:
                if((uint)currentPixelData.Count == Format.GetByteCount(1))
                {
                    ChangeState(DeviceState.DrawPixel);
                }
                break;
            case DeviceState.DrawPixel:
                this.NoisyLog("Pixel data: '{0}'", string.Join(", ", currentPixelData));
                var bytesPerPixel = (int)Format.GetByteCount(1);
                var offset = (counter.row * Width + counter.col) * bytesPerPixel;
                for(var i = 0; i < bytesPerPixel; i++)
                {
                    frameMemory[offset + i] = currentPixelData[i];
                }
                IncrementCounter();
                currentPixelData.Clear();
                ChangeState(DeviceState.WaitForPixel);
                break;
            }
        }

        private void ResetCmdContext()
        {
            state = DeviceState.WaitForCommand;
            currentCmd = null;
            currentArgs.Clear();
            currentPixelData.Clear();
        }

        private void SetNewCommand(byte data)
        {
            if(!Enum.IsDefined(typeof(ST7789Commands), data))
            {
                this.WarningLog("Command not supported 0x{0:X}", data);
            }
            else
            {
                currentCmd = (ST7789Commands)data;
            }
        }

        private void ProcessIgnoredCommand()
        {
            this.DebugLog("Ignoring command {0} (0x{1:X}) with arguments: '{2}'", currentCmd, (byte)currentCmd,
                          string.Join(", ", currentArgs.Select(b => "0x" + b.ToString("X2"))));
        }

        private void ProcessColMod()
        {
            // Ignored RGB interface color format

            var controlFormat = currentArgs[0] & 0x7;
            if(controlFormat != 0b101)
            {
                this.ErrorLog("Only 16-bit/pixel RGB565 format is supported");
            }
        }

        private void ProcessDisplayOn()
        {
            /* This call to base.Reconfigure will start the auto repaint thread. */
            Reconfigure(width: FrameMemoryWidth, height: (int)displayHeight, format: PixelFormat.RGB565, autoRepaint: true);
        }

        private void ProcessCaset()
        {
            var xs = BitHelper.ToUInt16([currentArgs[0], currentArgs[1]], 0, false);
            var xe = BitHelper.ToUInt16([currentArgs[2], currentArgs[3]], 0, false);
            if(xe < xs)
            {
                this.WarningLog("Invalid XE {0}, defaulting to {1}", xe, FrameMemoryWidth - 1);
                xe = FrameMemoryWidth - 1;
            }

            columnAddressSet = (xs, xe);
        }

        private void ProcessRaset()
        {
            var ys = BitHelper.ToUInt16([currentArgs[0], currentArgs[1]], 0, false);
            var ye = BitHelper.ToUInt16([currentArgs[2], currentArgs[3]], 0, false);
            if(ye < ys)
            {
                this.WarningLog("Invalid YE {0}, defaulting to {1}", ye, FrameMemoryHeight - 1);
                ye = FrameMemoryHeight - 1;
            }

            rowAddressSet = (ys, ye);
        }

        private void ProcessRAMControl()
        {
            if(BitHelper.GetValue(currentArgs[0], 0, 2) != 0 || BitHelper.GetValue(currentArgs[0], 4, 1) != 0)
            {
                this.ErrorLog("Only MCU interface is supported");
            }

            var mdt = BitHelper.GetValue(currentArgs[0], 0, 2);
            if(mdt != 0)
            {
                this.ErrorLog("Unsupported MDT {0}", mdt);
            }
            var endian = BitHelper.GetValue(currentArgs[1], 3, 1);
            if(endian != 1)
            {
                this.ErrorLog("Only little endian is supported");
            }
            var epf = BitHelper.GetValue(currentArgs[1], 4, 2); // It's fine to not store the value as it is the reset value
            if(epf != 3)
            {
                this.ErrorLog("Unsupported EPF {0}", epf);
            }
        }

        private void ProcessMemoryWrite()
        {
            counter.col = columnAddressSet.Start;
            counter.row = rowAddressSet.Start;
            ChangeState(DeviceState.WaitForPixel);
        }

        private void ProcessVerticalScrollingDefinition()
        {
            var tfa = BitHelper.ToUInt16([currentArgs[0], currentArgs[1]], 0, false);
            var vsa = BitHelper.ToUInt16([currentArgs[2], currentArgs[3]], 0, false);
            var bfa = BitHelper.ToUInt16([currentArgs[4], currentArgs[5]], 0, false);

            if(tfa + vsa + bfa != FrameMemoryHeight)
            {
                this.WarningLog("Invalid vertical scroll definition (TFA: {0}, VSA: {1}, BFA: {2})", tfa, vsa, bfa);
            }
            else
            {
                this.DebugLog("Vertical scrolling: {0}|{1}|{2}", tfa, vsa, bfa);
                topFixedArea = tfa;
                scrollingArea = vsa;
                bottomFixedArea = bfa;
            }
        }

        private void ProcessMemoryDataAccessControl()
        {
            if(currentArgs[0] != 0)
            {
                this.ErrorLog("Only supported memory data access control is MY = 0, MX = 0, MV = 0, ML = 0, RGB = 0, MH = 0");
            }
        }

        private void ProcessVerticalScrollStartAddr()
        {
            var vsp = BitHelper.ToUInt16([currentArgs[0], currentArgs[1]], 0, false);
            if(vsp < topFixedArea || vsp > (topFixedArea + scrollingArea))
            {
                this.WarningLog("Invalid vertical scrolling start address: {0} not in range [{1}; {2}]", vsp, topFixedArea,
                                topFixedArea + scrollingArea);
            }
            else
            {
                this.DebugLog("Vertical scrolling start at {0}", vsp);
                scrollStartAddr = vsp;
            }
        }

        private void IncrementCounter()
        {
            counter.col++;
            if(counter.col > columnAddressSet.End)
            {
                counter.col = columnAddressSet.Start;
                counter.row++;
                if(counter.row > rowAddressSet.End)
                {
                    counter.row = rowAddressSet.Start;
                }
            }
        }

        private (uint Start, uint End) columnAddressSet;
        private (uint Start, uint End) rowAddressSet;
        private (uint col, uint row) counter;
        private uint scrollStartAddr;
        private uint topFixedArea;
        private uint scrollingArea;
        private uint bottomFixedArea;

        private DeviceState state;
        private DCXPinState dataCommandSelection;
        private ST7789Commands? currentCmd;
        private readonly List<byte> currentArgs;
        private readonly List<byte> currentPixelData;
        private readonly Dictionary<ST7789Commands, (Action handler, uint argsCount)> commands;

        private readonly uint displayHeight;
        private readonly byte[] frameMemory;

        /* On ST7789, the buffer dimension is always 240x320 pixels, even if the display is actually
         * smaller.
         */
        private const int FrameMemoryWidth = 240;
        private const int FrameMemoryHeight = 320;

        // Only the used commands are listed here to know which commands are supported
        private enum ST7789Commands : byte
        {
            SWRESET = 0x01,
            SLPOUT = 0x11,
            NORON = 0x13,
            INVON =  0x21,
            DISPON = 0x29,
            CASET = 0x2A,
            RASET = 0x2B,
            RAMWR = 0x2C,
            VSCRDEF = 0x33,
            MADCTL = 0x36,
            VSCSAD = 0x37,
            COLMOD = 0x3A,
            RAMCTRL = 0xB0,
        }

        private enum DeviceState
        {
            WaitForCommand,
            WaitForArguments,
            ProcessCommand,
            WaitForPixel,
            DrawPixel
        }

        private enum DCXPinState
        {
            Command = 0,
            Data = 1,
        }
    }
}
