//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class XilinxGPIOPS : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize, IHasMappedRegisters
    {
        public XilinxGPIOPS(IMachine machine, string platform = "Zynq") : base(machine, GetNumberOfPins(platform))
        {
            switch(platform.ToLowerInvariant())
            {
            case "zynq":
                numberOfGpioBanks = 4;
                portOffsets = new uint[] { 0, 32, 54, 86, 118 };
                bankType = new GPIOType[] { GPIOType.MIO, GPIOType.MIO, GPIOType.EMIO, GPIOType.EMIO };
                break;
            case "zynqmp":
                // 3 26-bit MIO banks and 3 32-bit EMIO banks
                numberOfGpioBanks = 6;
                portOffsets = new uint[] { 0, 26, 52, 78, 110, 142, 174 };
                bankType = new GPIOType[] { GPIOType.MIO, GPIOType.MIO, GPIOType.MIO, GPIOType.EMIO, GPIOType.EMIO, GPIOType.EMIO };
                break;
            case "versal-pmc":
                numberOfGpioBanks = 4;
                portOffsets = new uint[] { 0, 26, 52, 84, 116 };
                bankType = new GPIOType[] { GPIOType.MIO, GPIOType.MIO, GPIOType.EMIO, GPIOType.EMIO };
                break;
            case "versal-ps":
                numberOfGpioBanks = 2;
                portOffsets = new uint[] { 0, 26, 58 };
                bankType = new GPIOType[] { GPIOType.MIO, GPIOType.EMIO };
                break;
            default:
                throw new ConstructionException("This platform is not valid for GPIO peripheral XilinxGPIOPS.");
            }
            Data = new uint[numberOfGpioBanks];
            portControllers = new GPIOController[numberOfGpioBanks];
            IRQ = new GPIO(); // NEW - support of IRQs. Per the doc there is 1 interrupt line for all banks
            for(uint i = 0; i < numberOfGpioBanks; i++)
            {
                portControllers[i] = new GPIOController(this, i);
            }
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            for(int i = 0; i < (int)numberOfGpioBanks; i++)
            {
                uint level = GetCurrentBankLevel(i);
                UpdateInterrupts(i, level, level);
            }
        }

        public string OffsetToString(long offset) => registerMapper.ToString(offset);

        public uint ReadDoubleWord(long offset)
        {
            if(offset > 0x200)
            {
                var portNumber = (uint)((offset - 0x200) / 0x40);
                return portControllers[portNumber].ReadRegister(offset % 0x40);
            }
            switch((RegistersOffsets)offset)
            {
            // Reading the Maskable Output registers returns the value stored in the Output registers, regardless of
            // if OutputEnable is true or not, per the documentation
            case RegistersOffsets.MaskableOutputData0Low:
                return Data[0] & 0xFFFF;
            case RegistersOffsets.MaskableOutputData0Hi:
                return Data[0] >> 16;
            case RegistersOffsets.MaskableOutputData1Low:
                return Data[1] & 0xFFFF;
            case RegistersOffsets.MaskableOutputData1Hi:
                return Data[1] >> 16;
            case RegistersOffsets.MaskableOutputData2Low:
                if(numberOfGpioBanks > 2) return Data[2] & 0xFFFF;
                this.WarningLog("Tried to read MaskableOutputData2Low when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.MaskableOutputData2Hi:
                if(numberOfGpioBanks > 2) return Data[2] >> 16;
                this.WarningLog("Tried to read MaskableOutputData2High when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.MaskableOutputData3Low:
                if(numberOfGpioBanks > 3) return Data[3] & 0xFFFF;
                this.WarningLog("Tried to read MaskableOutputData3Low when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.MaskableOutputData3Hi:
                if(numberOfGpioBanks > 3) return Data[3] >> 16;
                this.WarningLog("Tried to read MaskableOutputData3High when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.MaskableOutputData4Low:
                if(numberOfGpioBanks > 4) return Data[4] & 0xFFFF;
                this.WarningLog("Tried to read MaskableOutputData4Low when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.MaskableOutputData4Hi:
                if(numberOfGpioBanks > 4) return Data[4] >> 16;
                this.WarningLog("Tried to read MaskableOutputData4High when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.MaskableOutputData5Low:
                if(numberOfGpioBanks > 5) return Data[5] & 0xFFFF;
                this.WarningLog("Tried to read MaskableOutputData5Low when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.MaskableOutputData5Hi:
                if(numberOfGpioBanks > 5) return Data[5] >> 16;
                this.WarningLog("Tried to read MaskableOutputData5High when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.Data_RO0:
                // If Output Enable is off, data is stored inside the Output register, but not passed to the pins
                return GetCurrentBankLevel(0);
            case RegistersOffsets.Data_RO1:
                return GetCurrentBankLevel(1);
            case RegistersOffsets.Data_RO2:
                if(numberOfGpioBanks > 2) return GetCurrentBankLevel(2);
                this.WarningLog("Tried to read Data_RO2 when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.Data_RO3:
                if(numberOfGpioBanks > 3) return GetCurrentBankLevel(3);
                this.WarningLog("Tried to read Data_RO3 when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.Data_RO4:
                if(numberOfGpioBanks > 4) return GetCurrentBankLevel(4);
                this.WarningLog("Tried to read Data_RO4 when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.Data_RO5:
                if(numberOfGpioBanks > 5) return GetCurrentBankLevel(5);
                this.WarningLog("Tried to read Data_RO5 when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.Data0:
                return Data[0];
            case RegistersOffsets.Data1:
                return Data[1];
            case RegistersOffsets.Data2:
                if(numberOfGpioBanks > 2) return Data[2];
                this.WarningLog("Tried to read Data2 when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.Data3:
                if(numberOfGpioBanks > 3) return Data[3];
                this.WarningLog("Tried to read Data3 when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.Data4:
                if(numberOfGpioBanks > 4) return Data[4];
                this.WarningLog("Tried to read Data4 when there are only {0} banks; returning 0.", numberOfGpioBanks);
                return 0;
            case RegistersOffsets.Data5:
                if(numberOfGpioBanks > 5) return Data[5];
                this.WarningLog("Tried to read Data5 when there are only {0} banks; returning 0.", numberOfGpioBanks);
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
            uint previous, after;
            switch((RegistersOffsets)offset)
            {
            case RegistersOffsets.MaskableOutputData0Low:
                previous = GetCurrentBankLevel(0);
                Data[0] = (Data[0] & 0xFFFF0000) | (value & 0xFFFF);
                this.DoPinOperation(0, value & 0xFFFF, 0xFFFF0000 | value >> 16);
                after = GetCurrentBankLevel(0);
                UpdateInterrupts(0, previous, after);
                break;
            case RegistersOffsets.MaskableOutputData0Hi:
                previous = GetCurrentBankLevel(0);
                Data[0] = (Data[0] & 0x0000FFFF) | (value << 16);
                this.DoPinOperation(0, value & 0xFFFF, 0x0000FFFF | value >> 16);
                after = GetCurrentBankLevel(0);
                UpdateInterrupts(0, previous, after);
                break;
            case RegistersOffsets.MaskableOutputData1Low:
                previous = GetCurrentBankLevel(1);
                Data[1] = (Data[1] & 0xFFFF0000) | (value & 0xFFFF);
                this.DoPinOperation(1, value & 0xFFFF, 0xFFFF0000 | value >> 16);
                after = GetCurrentBankLevel(1);
                UpdateInterrupts(1, previous, after);
                break;
            case RegistersOffsets.MaskableOutputData1Hi:
                previous = GetCurrentBankLevel(1);
                Data[1] = (Data[1] & 0x0000FFFF) | (value << 16);
                this.DoPinOperation(1, value & 0xFFFF, 0x0000FFFF | value >> 16);
                after = GetCurrentBankLevel(1);
                UpdateInterrupts(1, previous, after);
                break;
            case RegistersOffsets.MaskableOutputData2Low:
                if(numberOfGpioBanks > 2)
                {
                    previous = GetCurrentBankLevel(2);
                    Data[2] = (Data[2] & 0xFFFF0000) | (value & 0xFFFF);
                    this.DoPinOperation(2, value & 0xFFFF, 0xFFFF0000 | value >> 16);
                    after = GetCurrentBankLevel(2);
                    UpdateInterrupts(2, previous, after);
                }
                break;
            case RegistersOffsets.MaskableOutputData2Hi:
                if(numberOfGpioBanks > 2)
                {
                    previous = GetCurrentBankLevel(2);
                    Data[2] = (Data[2] & 0x0000FFFF) | (value << 16);
                    this.DoPinOperation(2, value & 0xFFFF, 0x0000FFFF | value >> 16);
                    after = GetCurrentBankLevel(2);
                    UpdateInterrupts(2, previous, after);
                }
                break;
            case RegistersOffsets.MaskableOutputData3Low:
                if(numberOfGpioBanks > 3)
                {
                    previous = GetCurrentBankLevel(3);
                    Data[3] = (Data[3] & 0xFFFF0000) | (value & 0xFFFF);
                    this.DoPinOperation(3, value & 0xFFFF, 0xFFFF0000 | value >> 16);
                    after = GetCurrentBankLevel(3);
                    UpdateInterrupts(3, previous, after);
                }
                break;
            case RegistersOffsets.MaskableOutputData3Hi:
                if(numberOfGpioBanks > 3)
                {
                    previous = GetCurrentBankLevel(3);
                    Data[3] = (Data[3] & 0x0000FFFF) | (value << 16);
                    this.DoPinOperation(3, value & 0xFFFF, 0x0000FFFF | value >> 16);
                    after = GetCurrentBankLevel(3);
                    UpdateInterrupts(3, previous, after);
                }
                break;
            case RegistersOffsets.MaskableOutputData4Low:
                if(numberOfGpioBanks > 4)
                {
                    previous = GetCurrentBankLevel(4);
                    Data[4] = (Data[4] & 0xFFFF0000) | (value & 0xFFFF);
                    this.DoPinOperation(4, value & 0xFFFF, 0xFFFF0000 | value >> 16);
                    after = GetCurrentBankLevel(4);
                    UpdateInterrupts(4, previous, after);
                }
                break;
            case RegistersOffsets.MaskableOutputData4Hi:
                if(numberOfGpioBanks > 4)
                {
                    previous = GetCurrentBankLevel(4);
                    Data[4] = (Data[4] & 0x0000FFFF) | (value << 16);
                    this.DoPinOperation(4, value & 0xFFFF, 0x0000FFFF | value >> 16);
                    after = GetCurrentBankLevel(4);
                    UpdateInterrupts(4, previous, after);
                }
                break;
            case RegistersOffsets.MaskableOutputData5Low:
                if(numberOfGpioBanks > 5)
                {
                    previous = GetCurrentBankLevel(5);
                    Data[5] = (Data[5] & 0xFFFF0000) | (value & 0xFFFF);
                    this.DoPinOperation(5, value & 0xFFFF, 0xFFFF0000 | value >> 16);
                    after = GetCurrentBankLevel(5);
                    UpdateInterrupts(5, previous, after);
                }
                break;
            case RegistersOffsets.MaskableOutputData5Hi:
                if(numberOfGpioBanks > 5)
                {
                    previous = GetCurrentBankLevel(5);
                    Data[5] = (Data[5] & 0x0000FFFF) | (value << 16);
                    this.DoPinOperation(5, value & 0xFFFF, 0x0000FFFF | value >> 16);
                    after = GetCurrentBankLevel(5);
                    UpdateInterrupts(5, previous, after);
                }
                break;
            case RegistersOffsets.Data_RO0:
                this.Log(LogLevel.Warning, "Writing read only register offset: {0:X} value: {1:X}", offset, value);
                break;
            case RegistersOffsets.Data_RO1:
                this.Log(LogLevel.Warning, "Writing read only register offset: {0:X} value: {1:X}", offset, value);
                break;
            case RegistersOffsets.Data_RO2:
                this.Log(LogLevel.Warning, "Writing read only register offset: {0:X} value: {1:X}", offset, value);
                break;
            case RegistersOffsets.Data_RO3:
                this.Log(LogLevel.Warning, "Writing read only register offset: {0:X} value: {1:X}", offset, value);
                break;
            case RegistersOffsets.Data_RO4:
                this.Log(LogLevel.Warning, "Writing read only register offset: {0:X} value: {1:X}", offset, value);
                break;
            case RegistersOffsets.Data_RO5:
                this.Log(LogLevel.Warning, "Writing read only register offset: {0:X} value: {1:X}", offset, value);
                break;
            case RegistersOffsets.Data0:
                previous = GetCurrentBankLevel(0);
                Data[0] = value;
                this.DoPinOperation(0, value, 0);
                after = GetCurrentBankLevel(0);
                UpdateInterrupts(0, previous, after);
                break;
            case RegistersOffsets.Data1:
                previous = GetCurrentBankLevel(1);
                Data[1] = value;
                this.DoPinOperation(1, value, 0);
                after = GetCurrentBankLevel(1);
                UpdateInterrupts(1, previous, after);
                break;
            case RegistersOffsets.Data2:
                if(numberOfGpioBanks > 2)
                {
                    previous = GetCurrentBankLevel(2);
                    Data[2] = value;
                    this.DoPinOperation(2, value, 0);
                    after = GetCurrentBankLevel(2);
                    UpdateInterrupts(2, previous, after);
                }
                break;
            case RegistersOffsets.Data3:
                if(numberOfGpioBanks > 3)
                {
                    previous = GetCurrentBankLevel(3);
                    Data[3] = value;
                    this.DoPinOperation(3, value, 0);
                    after = GetCurrentBankLevel(3);
                    UpdateInterrupts(3, previous, after);
                }
                break;
            case RegistersOffsets.Data4:
                if(numberOfGpioBanks > 4)
                {
                    previous = GetCurrentBankLevel(4);
                    Data[4] = value;
                    this.DoPinOperation(4, value, 0);
                    after = GetCurrentBankLevel(4);
                    UpdateInterrupts(4, previous, after);
                }
                break;
            case RegistersOffsets.Data5:
                if(numberOfGpioBanks > 5)
                {
                    previous = GetCurrentBankLevel(5);
                    Data[5] = value;
                    this.DoPinOperation(5, value, 0);
                    after = GetCurrentBankLevel(5);
                    UpdateInterrupts(5, previous, after);
                }
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            this.NoisyLog("OnGPIO: call with GPIO number {0} and value {1}", number, value);
            // Find the port this pin belongs to
            int bank, offset;
            this.PinToBank(number, out bank, out offset);
            uint previous = GetCurrentBankLevel(bank);

            // Assert this pin is in, not out
            if((portControllers[bank].OutputEnabled() & (1 << offset)) != 0)
            {
                this.Log(LogLevel.Warning, "OnGPIO: Trying to change an out port, skipping");
                return;
            }
            // If the pin is in, update value
            base.OnGPIO(number, value);
            uint after = GetCurrentBankLevel(bank);
            UpdateInterrupts(bank, previous, after);
        }

        public GPIO IRQ { get; }

        public long Size
        {
            get
            {
                return 0x2E8;
            }
        }

        uint GetCurrentBankLevel(int bank)
        {
            GPIOController portController = portControllers[bank];
            uint nbOfPins = portOffsets[bank + 1] - portOffsets[bank];
            uint direction = portController.ReadRegister((long)GPIOController.RegistersOffsets.DirectionMode);

            uint level = Data[bank] & portController.ReadRegister((long)GPIOController.RegistersOffsets.OutputEnable);
            for(int i = 0; i < nbOfPins; i++)
            {
                // We have the output pin level ; let's get the level for input pins as well
                if((direction & (1u << i)) == 0 && State[(int)portOffsets[bank] + i]) level |= 1u << i;
            }
            return level;
        }

        private static int GetNumberOfPins(string platform)
        {
            switch(platform)
            {
            case "Zynq": return 118; //54 MIO + 64 EMIO
            case "ZynqMP": return 174; //78 MIO + 96 EMIO
            case "Versal-PMC": return 116; // 52 MIO,
            case "Versal-PS": return 58; // 26 MIO, 
            default: throw new ConstructionException("This platform is not valid for GPIO peripheral XilinxGPIOPS.");
            }
        }

        private void DoPinOperation(int portNumber, uint value, uint mask)
        {
            /* Compute port length based on portOffset array (that has been extended with the end of the port range as well) */
            var portLength = portOffsets[portNumber + 1] - portOffsets[portNumber];
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

        private void PinToBank(int number, out int bank, out int offset)
        {
            bank = -1; offset = -1;
            for(int i = 0; i < 6; i++) // 6 is the max case, will never be reached ; we will use break before that
            {
                if(number < portOffsets[i + 1]) { bank = i; break; }
            }
            if(bank < 0)
            {
                this.Log(LogLevel.Error, "No bank found for GPIO {0}", number);
                return;
            }

            offset = number - (int)portOffsets[bank];
            this.Log(LogLevel.Info, "OnGPIO: GPIO {0} has been calculated to be in bank {1}, offset {2}", number, bank, offset);
        }

        private void UpdateInterrupts(int bank, uint before, uint after)
        {
            this.NoisyLog("UpdateInterrupts: bank {0}, old value {1}, new value {2}", bank, before, after);
            GPIOController portController = portControllers[bank];

            uint assert_low = ~after & portController.LevelLowInterrupt();
            uint assert_high = after & portController.LevelHighInterrupt();
            uint rising_edge = ~before & after & portController.RisingEdgeInterrupt();
            uint falling_edge = before & ~after & portController.FallingEdgeInterrupt();

            uint interrupt = assert_low | assert_high | rising_edge | falling_edge;
            if(interrupt != 0)
            {
                this.Log(LogLevel.Info, "Conditions for interrupt on bank {0} have been met. Rising interrupt....", bank);
                this.Log(LogLevel.Noisy, "Before value : {0} ; After value : {1}", before, after);
                this.Log(LogLevel.Noisy, "Level Low : {0} ; Level High : {1} ; Rising Edge {2}, Falling Edge {3}", assert_low, assert_high, rising_edge, falling_edge);

                portController.SetInterruptStatus(interrupt);
            }
            UpdateIRQLine();
        }

        private void UpdateIRQLine()
        {
            // For all banks, check if one of their interrupt lines is set ; that 
            bool allBankInterrupts = false;
            for(int i = 0; i < numberOfGpioBanks; i++)
            {
                GPIOController control = portControllers[i];
                if((control.GetInterruptStatus() & control.GetUnmaskedInterrupts()) != 0) allBankInterrupts = true;
            }
            IRQ.Set(allBankInterrupts);
        }

        /* Registers */
        private readonly uint[] Data;

        private readonly uint[] portOffsets;
        private readonly uint numberOfGpioBanks;
        private readonly GPIOType[] bankType;
        private readonly RegisterMapper registerMapper = new RegisterMapper(typeof(RegistersOffsets));

        private readonly GPIOController[] portControllers;

        /* Common register sets for the all the banks
         * This is where the interruption parameters are handled
         */
        protected class GPIOController
        {
            public GPIOController(XilinxGPIOPS parent, uint bankNumber)
            {
                this.parentClass = parent;
                this.bankNumber = bankNumber;
            }

            public void WriteRegister(long offset, uint value)
            {
                uint level;
                switch((RegistersOffsets)offset)
                {
                case RegistersOffsets.DirectionMode:
                    DirectionMode = value;
                    parentClass.DoPinOperation((int)this.bankNumber, parentClass.Data[this.bankNumber], 0); // Re-run connections on newly output pins
                    break;
                case RegistersOffsets.OutputEnable:
                    OutputEnable = value;
                    parentClass.DoPinOperation((int)this.bankNumber, parentClass.Data[this.bankNumber], 0); // Re-run connections on output-enabled pins
                    break;
                case RegistersOffsets.InterruptMaskStatus:
                    parentClass.Log(LogLevel.Warning, "Writing Read-only register, ignored");
                    break;
                case RegistersOffsets.InterruptEnable:
                    InterruptMask &= ~value;
                    level = parentClass.GetCurrentBankLevel((int)bankNumber);
                    parentClass.UpdateInterrupts((int)this.bankNumber, level, level);
                    break;
                case RegistersOffsets.InterruptDisable:
                    InterruptMask |= value;
                    break;
                case RegistersOffsets.InterruptStatus:
                    // Write 1 to clear
                    InterruptStatus &= ~value;
                    level = parentClass.GetCurrentBankLevel((int)bankNumber);
                    parentClass.UpdateInterrupts((int)this.bankNumber, level, level);
                    break;
                case RegistersOffsets.InterruptType:
                    parentClass.Log(LogLevel.Noisy, "InterruptType set to 0x{0}", value.ToString("X8"));
                    InterruptType = value;
                    level = parentClass.GetCurrentBankLevel((int)bankNumber);
                    parentClass.UpdateInterrupts((int)this.bankNumber, level, level);
                    break;
                case RegistersOffsets.InterruptPolarity:
                    InterruptPolarity = value;
                    level = parentClass.GetCurrentBankLevel((int)bankNumber);
                    parentClass.UpdateInterrupts((int)this.bankNumber, level, level);
                    break;
                case RegistersOffsets.InterruptAnyEdgeSensitive:
                    // Update bits only if InterruptType is set to Edge = 1
                    // So these should update only of Interrupt Type is 1
                    InterruptOnAny = value & InterruptType;
                    level = parentClass.GetCurrentBankLevel((int)bankNumber);
                    parentClass.UpdateInterrupts((int)this.bankNumber, level, level);
                    break;
                default:
                    this.parentClass.LogUnhandledWrite(0x204 + this.bankNumber * 0x40 + offset, value); //Should be 200 instead of 204?
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
                case RegistersOffsets.InterruptMaskStatus:
                    return InterruptMask;
                case RegistersOffsets.InterruptEnable:
                case RegistersOffsets.InterruptDisable:
                    parentClass.Log(LogLevel.Warning, "Reading Write-1-to-set register, returning 0"); return 0;
                case RegistersOffsets.InterruptStatus:
                    return InterruptStatus;
                case RegistersOffsets.InterruptType:
                    return InterruptType;
                case RegistersOffsets.InterruptPolarity:
                    return InterruptPolarity;
                case RegistersOffsets.InterruptAnyEdgeSensitive:
                    return InterruptOnAny;
                default:
                    this.parentClass.LogUnhandledRead(0x204 + this.bankNumber * 0x40 + offset);
                    return 0;
                }
            }

            public uint OutputEnabled()
            {
                return (uint)(DirectionMode & OutputEnable);
            }

            public uint LevelLowInterrupt()
            {
                parentClass.Log(LogLevel.Noisy, "Interrupt type is 0x{0:X}, Interrupt polarity is 0x{1:X}, mask is 0x{2:X}", InterruptType, InterruptPolarity, InterruptMask);
                return (uint)(~InterruptType & ~InterruptPolarity);
            }

            public uint LevelHighInterrupt()
            {
                return (uint)(~InterruptType & InterruptPolarity);
            }

            public uint RisingEdgeInterrupt()
            {
                return (uint)(InterruptType & (InterruptPolarity | InterruptOnAny));
            }

            public uint FallingEdgeInterrupt()
            {
                return (uint)(InterruptType & (~InterruptPolarity | InterruptOnAny));
            }

            public void SetInterruptStatus(uint status)
            {
                InterruptStatus |= status;
                parentClass.UpdateIRQLine();
            }

            public uint GetInterruptStatus()
            {
                return InterruptStatus;
            }

            public uint GetUnmaskedInterrupts()
            {
                return ~InterruptMask;
            }

            /* Registers */
            private uint DirectionMode = 0x00;
            private uint OutputEnable = 0x00;
            private uint InterruptMask = 0xFFFFFFFF; // After Linux tests, seems like masked is 0 and 1 is not masked ; all interrupts are masked on reset
            private uint InterruptStatus = 0x00; // No interrupt has happened on reset
            private uint InterruptType = 0xFFFFFFFF; // 0 is level-sensitive ; 1 is edge-sensitive
            private uint InterruptPolarity = 0x00; // O is low / falling-edge sensitive ; 1 is up / rising edge
            private uint InterruptOnAny = 0x00; // Sensitive on both edges if 1 (polarity is ignored); sensitive on only 1 edge if 0
            private readonly XilinxGPIOPS parentClass;
            private readonly uint bankNumber;

            public enum RegistersOffsets : uint
            {
                DirectionMode = 0x04,
                OutputEnable = 0x08,
                InterruptMaskStatus = 0x0C,
                InterruptEnable = 0x10,
                InterruptDisable = 0x14,
                InterruptStatus = 0x18,
                InterruptType = 0x1C,
                InterruptPolarity = 0x20,
                InterruptAnyEdgeSensitive = 0x24,
            }
        }

        private enum GPIOType
        {
            MIO,
            EMIO
        }

        /* Offsets */
        private enum RegistersOffsets : uint
        {
            // Used for partial GPIO within banks writes
            MaskableOutputData0Low = 0x000,
            MaskableOutputData0Hi  = 0x004,
            MaskableOutputData1Low = 0x008,
            MaskableOutputData1Hi  = 0x00C,
            MaskableOutputData2Low = 0x010,
            MaskableOutputData2Hi  = 0x014,
            MaskableOutputData3Low = 0x018,
            MaskableOutputData3Hi  = 0x01C,
            MaskableOutputData4Low = 0x020,
            MaskableOutputData4Hi  = 0x024,
            MaskableOutputData5Low = 0x028,
            MaskableOutputData5Hi  = 0x02C,

            // Used for full bank writes
            Data0 = 0x040,
            Data1 = 0x044,
            Data2 = 0x048,
            Data3 = 0x04C,
            Data4 = 0x050,
            Data5 = 0x054,

            Data_RO0 = 0x060,
            Data_RO1 = 0x064,
            Data_RO2 = 0x068,
            Data_RO3 = 0x06C,
            Data_RO4 = 0x070,
            Data_RO5 = 0x074,

            DirectionMode0 = 0x204,
            OutputEnable0  = 0x208,
            IntMask0       = 0x20C,
            IntEnable0     = 0x210,
            IntDisable0    = 0x214,
            IntStatus0     = 0x218,
            IntType0       = 0x21C,
            IntPolarity0   = 0x220,
            IntAny0        = 0x224,

            DirectionMode1 = 0x244,
            OutputEnable1  = 0x248,
            IntMask1       = 0x24C,
            IntEnable1     = 0x250,
            IntDisable1    = 0x254,
            IntStatus1     = 0x258,
            IntType1       = 0x25C,
            IntPolarity1   = 0x260,
            IntAny1        = 0x264,

            DirectionMode2 = 0x284,
            OutputEnable2  = 0x288,
            IntMask2       = 0x28C,
            IntEnable2     = 0x290,
            IntDisable2    = 0x294,
            IntStatus2     = 0x298,
            IntType2       = 0x29C,
            IntPolarity2   = 0x2A0,
            IntAny2        = 0x2A4,

            DirectionMode3 = 0x2C4,
            OutputEnable3  = 0x2C8,
            IntMask3       = 0x2CC,
            IntEnable3     = 0x2D0,
            IntDisable3    = 0x2D4,
            IntStatus3     = 0x2D8,
            IntType3       = 0x2DC,
            IntPolarity3   = 0x2E0,
            IntAny3        = 0x2E4,

            DirectionMode4 = 0x304,
            OutputEnable4  = 0x308,
            IntMask4       = 0x30C,
            IntEnable4     = 0x310,
            IntDisable4    = 0x314,
            IntStatus4     = 0x318,
            IntType4       = 0x31C,
            IntPolarity4   = 0x320,
            IntAny4        = 0x324,

            DirectionMode5 = 0x344,
            OutputEnable5  = 0x348,
            IntMask5       = 0x34C,
            IntEnable5     = 0x350,
            IntDisable5    = 0x354,
            IntStatus5     = 0x358,
            IntType5       = 0x35C,
            IntPolarity5   = 0x360,
            IntAny5        = 0x364,
        }
    }
}