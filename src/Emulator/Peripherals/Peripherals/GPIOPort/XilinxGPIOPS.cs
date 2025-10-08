//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class XilinxGPIOPS : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public XilinxGPIOPS(IMachine machine, uint numberOfGpioBanks = 4) : base(machine, 54 + 64) //54 MIO + 64 EMIO
        {
            portControllers = new GPIOController[numberOfGpioBanks];
            for(uint i = 0; i < numberOfGpioBanks; i++)
            {
                portControllers[i] = new GPIOController(this, i);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset > 0x200)
            {
                var portNumber = (uint)((offset - 0x200) / 0x40);
                return portControllers[portNumber].ReadRegister(offset % 0x40);
            }
            switch((RegistersOffsets)offset)
            {
            case RegistersOffsets.MaskableOutputData0Low:
                return OutputData0 & 0xFFFF;
            case RegistersOffsets.MaskableOutputData0Hi:
                return OutputData0 >> 16;
            case RegistersOffsets.MaskableOutputData1Low:
                return OutputData1 & 0xFFFF;
            case RegistersOffsets.MaskableOutputData1Hi:
                return OutputData1 >> 16;
            case RegistersOffsets.MaskableOutputData2Low:
                return OutputData2 & 0xFFFF;
            case RegistersOffsets.MaskableOutputData2Hi:
                return OutputData2 >> 16;
            case RegistersOffsets.MaskableOutputData3Low:
                return OutputData3 & 0xFFFF;
            case RegistersOffsets.MaskableOutputData3Hi:
                return OutputData3 >> 16;
            case RegistersOffsets.OutputData0:
                return OutputData0;
            case RegistersOffsets.OutputData1:
                return OutputData1;
            case RegistersOffsets.OutputData2:
                return OutputData2;
            case RegistersOffsets.OutputData3:
                return OutputData3;
            case RegistersOffsets.InputData0:
                return 0;
            case RegistersOffsets.InputData1:
                return 0;
            case RegistersOffsets.InputData2:
                return 0;
            case RegistersOffsets.InputData3:
                return 0;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset > 0x200)
            {
                var portNumber = (uint)((offset - 0x200) / 0x40);
                portControllers[portNumber].WriteRegister(offset % 0x40, value);
                return;
            }
            switch((RegistersOffsets)offset)
            {
            case RegistersOffsets.MaskableOutputData0Low:
                OutputData0 = (OutputData0 & 0xFFFF0000) | (value & 0xFFFF);
                this.DoPinOperation(0, value & 0xFFFF, 0xFFFF0000 | value >> 16);
                break;
            case RegistersOffsets.MaskableOutputData0Hi:
                OutputData0 = (OutputData0 & 0x0000FFFF) | (value << 16);
                this.DoPinOperation(0, value & 0xFFFF, 0x0000FFFF | value >> 16);
                break;
            case RegistersOffsets.MaskableOutputData1Low:
                OutputData1 = (OutputData1 & 0xFFFF0000) | (value & 0xFFFF);
                this.DoPinOperation(1, value & 0xFFFF, 0xFFFF0000 | value >> 16);
                break;
            case RegistersOffsets.MaskableOutputData1Hi:
                OutputData1 = (OutputData1 & 0x0000FFFF) | (value << 16);
                this.DoPinOperation(1, value & 0xFFFF, 0x0000FFFF | value >> 16);
                break;
            case RegistersOffsets.MaskableOutputData2Low:
                OutputData2 = (OutputData2 & 0xFFFF0000) | (value & 0xFFFF);
                this.DoPinOperation(2, value & 0xFFFF, 0xFFFF0000 | value >> 16);
                break;
            case RegistersOffsets.MaskableOutputData2Hi:
                OutputData2 = (OutputData2 & 0x0000FFFF) | (value << 16);
                this.DoPinOperation(2, value & 0xFFFF, 0x0000FFFF | value >> 16);
                break;
            case RegistersOffsets.MaskableOutputData3Low:
                OutputData3 = (OutputData3 & 0xFFFF0000) | (value & 0xFFFF);
                this.DoPinOperation(3, value & 0xFFFF, 0xFFFF0000 | value >> 16);
                break;
            case RegistersOffsets.MaskableOutputData3Hi:
                OutputData3 = (OutputData3 & 0x0000FFFF) | (value << 16);
                this.DoPinOperation(3, value & 0xFFFF, 0x0000FFFF | value >> 16);
                break;
            case RegistersOffsets.OutputData0:
                OutputData0 = value;
                this.DoPinOperation(0, value, 0);
                break;
            case RegistersOffsets.OutputData1:
                OutputData1 = value;
                this.DoPinOperation(1, value, 0);
                break;
            case RegistersOffsets.OutputData2:
                OutputData2 = value;
                this.DoPinOperation(2, value, 0);
                break;
            case RegistersOffsets.OutputData3:
                OutputData2 = value;
                this.DoPinOperation(2, value, 0);
                break;
            case RegistersOffsets.InputData0:
                this.Log(LogLevel.Warning, "Writing read only register offset: {0:X} value: {1:X}", offset, value);
                break;
            case RegistersOffsets.InputData1:
                this.Log(LogLevel.Warning, "Writing read only register offset: {0:X} value: {1:X}", offset, value);
                break;
            case RegistersOffsets.InputData2:
                this.Log(LogLevel.Warning, "Writing read only register offset: {0:X} value: {1:X}", offset, value);
                break;
            case RegistersOffsets.InputData3:
                this.Log(LogLevel.Warning, "Writing read only register offset: {0:X} value: {1:X}", offset, value);
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public long Size
        {
            get
            {
                return 0x2E8;
            }
        }

        private void DoPinOperation(int portNumber, uint value, uint mask)
        {
            /* Port 1 is only 22 bit long, while all the others are 32 bit*/
            var portLength = portNumber == 1 ? 21 : 31;
            var outputEnabled = portControllers[portNumber].OutputEnabled();
            for(int i = 0; i < portLength; i++)
            {
                if((mask & (1u << i)) == 0)
                {
                    if((outputEnabled & (1u << i)) != 0)
                    {
                        if((value & (1u << i)) != 0)
                        {
                            Connections[(int)portOffsets[portNumber] + i].Set();
                        }
                        else
                        {
                            Connections[(int)portOffsets[portNumber] + i].Unset();
                        }
                    }
                }
            }
        }

        /* Registers */
        private uint OutputData0;
        private uint OutputData1;
        private uint OutputData2;
        private uint OutputData3;
        private readonly uint[] portOffsets = new uint[] { 0, 32, 54, 86 };

        private readonly GPIOController[] portControllers;

        /* Common register sets for the all the banks */
        private class GPIOController
        {
            public GPIOController(XilinxGPIOPS parent, uint bankNumber)
            {
                this.parentClass = parent;
                this.bankNumber = bankNumber;
            }

            public void WriteRegister(long offset, uint value)
            {
                switch((RegistersOffsets)offset)
                {
                case RegistersOffsets.DirectionMode:
                    DirectionMode = value;
                    break;
                case RegistersOffsets.OutputEnable:
                    OutputEnable = value;
                    break;
                default:
                    this.parentClass.LogUnhandledWrite(0x204 + this.bankNumber * 0x40 + offset, value);
                    break;
                }
            }

            public uint ReadRegister(long offset)
            {
                switch((RegistersOffsets)offset)
                {
                case RegistersOffsets.DirectionMode:
                    return DirectionMode;
                case RegistersOffsets.OutputEnable:
                    return OutputEnable;
                default:
                    this.parentClass.LogUnhandledRead(0x204 + this.bankNumber * 0x40 + offset);
                    return 0;
                }
            }

            public uint OutputEnabled()
            {
                return (uint)(DirectionMode & OutputEnable);
            }

            /* Registers */
            private uint DirectionMode = 0x00;
            private uint OutputEnable = 0x00;
            private readonly XilinxGPIOPS parentClass;
            private readonly uint bankNumber;

            private enum RegistersOffsets : uint
            {
                DirectionMode = 0x04,
                OutputEnable = 0x08,
                InterruptMaskStatus = 0x0C,
                InterruptEnable = 0x10,
                InterruptDisable = 0x14,
                InterruptPolarity = 0x18,
                InterruptAnyEdgeSensitive = 0x1C
            }
        }

        /* For the future use
        private uint InputData0;
        private uint InputData1;
        private uint InputData2;
        private uint InputData3;
        */
        /* Offsets */
        private enum RegistersOffsets : uint
        {
            MaskableOutputData0Low = 0x00,
            MaskableOutputData0Hi = 0x04,
            MaskableOutputData1Low = 0x08,
            MaskableOutputData1Hi = 0x0C,
            MaskableOutputData2Low = 0x10,
            MaskableOutputData2Hi = 0x14,
            MaskableOutputData3Low = 0x18,
            MaskableOutputData3Hi = 0x1C,
            OutputData0 = 0x40,
            OutputData1 = 0x44,
            OutputData2 = 0x48,
            OutputData3 = 0x4C,
            InputData0 = 0x60,
            InputData1 = 0x64,
            InputData2 = 0x68,
            InputData3 = 0x6C
        }
    }
}