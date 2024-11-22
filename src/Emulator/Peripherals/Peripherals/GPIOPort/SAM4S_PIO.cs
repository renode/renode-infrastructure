//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class SAM4S_PIO : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        // Status register reset value is defined at product level, thus there's an appropriate variable in the constructor
        public SAM4S_PIO(IMachine machine, uint statusRegisterResetVal = 0xFFFFFFFF) : base(machine, NumberOfPins)
        {
            innerLock = new object();
            IRQ = new GPIO();
            irqManager = new GPIOInterruptManager(IRQ, State);
            enabled = new IFlagRegisterField[NumberOfPins];
            useAdditionalIrqMode = new IFlagRegisterField[NumberOfPins];
            selectEdgeLevel= new IFlagRegisterField[NumberOfPins];
            selectFallRiseLowHigh = new IFlagRegisterField[NumberOfPins];
            outputData = new IFlagRegisterField[NumberOfPins];
            outputDataWriteEnabled = new IFlagRegisterField[NumberOfPins];

            irqManager = new GPIOInterruptManager(IRQ, State);
            ResetDirection();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters(statusRegisterResetVal);
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(innerLock)
            {
                return RegistersCollection.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(innerLock)
            {
                RegistersCollection.Write(offset, value);
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            lock(innerLock)
            {
                if((irqManager.PinDirection[number] & GPIOInterruptManager.Direction.Input) == 0)
                {
                    this.Log(LogLevel.Warning, "Writing to an output GPIO pin #{0}", number);
                    return;
                }

                base.OnGPIO(number, value);
                irqManager.RefreshInterrupts();
            }
        }

        public override void Reset()
        {
            lock(innerLock)
            {
                base.Reset();
                irqManager.Reset();
                ResetDirection();
                RegistersCollection.Reset();
            }
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; }
        public long Size => 0x168;

        private void ResetDirection()
        {
            for(int i = 0; i < NumberOfPins; i++)
            {
                irqManager.PinDirection[i] = GPIOInterruptManager.Direction.Input;
            }
        }

        private void DefineRegisters(uint statusRegisterResetVal)
        {
            Registers.PioEnable.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                enabled[i].Value = true;
                                UpdateIOLine(i);
                            }
                        },
                        name: "PER")
            ;

            Registers.PioDisable.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                enabled[i].Value = false;
                                UpdateIOLine(i);
                            }
                        },
                        name: "PDR")
            ;

            Registers.PioStatus.Define(this, statusRegisterResetVal)
                .WithFlags(0, 32, out enabled, FieldMode.Read, name: "PSR")
            ;

            Registers.OutputEnable.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                irqManager.PinDirection[i] |= GPIOInterruptManager.Direction.Output;
                                UpdateIOLine(i);
                            }
                        },
                        name: "OER")
            ;

            Registers.OutputDisable.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                irqManager.PinDirection[i] &= ~GPIOInterruptManager.Direction.Output;
                                UpdateIOLine(i);
                            }
                        },
                        name: "ODR")
            ;

            Registers.OutputStatus.Define(this)
                .WithFlags(0, 32, FieldMode.Read,
                        valueProviderCallback: (i, _) =>
                        {
                            return (irqManager.PinDirection[i] & GPIOInterruptManager.Direction.Output) != 0;
                        },
                        name: "OSR")
            ;

            Registers.GlitchInputFilterEnable.Define(this)
                .WithTaggedFlags("IFER", 0, 32)
            ;

            Registers.GlitchInputFilterDisable.Define(this)
                .WithTaggedFlags("IFDR", 0, 32)
            ;

            Registers.GlitchInputFilterStatus.Define(this)
                .WithTaggedFlags("IFSR", 0, 32)
            ;

            Registers.SetOutputData.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                outputData[i].Value = true;
                                UpdateIOLine(i);
                            }
                        },
                        name: "SODR")
            ;

            Registers.ClearOutputData.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                outputData[i].Value = false;
                                UpdateIOLine(i);
                            }
                        },
                        name: "CODR")
            ;

            Registers.OutputDataStatus.Define(this)
                .WithFlags(0, 32, out outputData,
                        writeCallback: (i, _, value) =>
                        {
                            // Bits in this register can be Read/Write if 1 is set in OutputWriteEnable
                            if(outputDataWriteEnabled[i].Value)
                            {
                                outputData[i].Value = value;
                                UpdateIOLine(i);
                            }
                        },
                        name: "ODSR")
            ;

            Registers.PinDataStatus.Define(this)
                .WithFlags(0, 32, FieldMode.Read, valueProviderCallback: (i, _) => State[i], name: "PDSR")
            ;

            Registers.InterruptEnable.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                irqManager.InterruptEnable[i] = true;
                            }
                        },
                        name: "IER")
            ;

            Registers.InterruptDisable.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                irqManager.InterruptEnable[i] = false;
                            }
                        },
                        name: "IDR")
            ;

            Registers.InterruptMask.Define(this)
                .WithFlags(0, 32, FieldMode.Read, valueProviderCallback: (i, _) => irqManager.InterruptEnable[i], name: "IMR")
            ;

            Registers.InterruptStatus.Define(this)
                .WithFlags(0, 32, FieldMode.Read,
                        valueProviderCallback: (i, _) =>
                        {
                            var result = irqManager.ActiveInterrupts.ElementAt(i);
                            irqManager.ClearInterrupt(i);
                            return result;
                        },
                        name: "ISR")
            ;

            Registers.MultiDriverEnable.Define(this)
                .WithTaggedFlags("MDER", 0, 32)
            ;

            Registers.MultiDriverDisable.Define(this)
                .WithTaggedFlags("MDDR", 0, 32)
            ;

            Registers.MultiDriverStatus.Define(this)
                .WithTaggedFlags("MDSR", 0, 32)
            ;

            Registers.PullUpDisable.Define(this)
                .WithTaggedFlags("PUDR", 0, 32)
            ;

            Registers.PullUpEnable.Define(this)
                .WithTaggedFlags("PUER", 0, 32)
            ;

            Registers.PadPullUpStatus.Define(this)
                .WithTaggedFlags("PUSR", 0, 32)
            ;

            Registers.PeripheralSelect1.Define(this)
                .WithTaggedFlags("ABCDSR1", 0, 32)
            ;

            Registers.PeripheralSelect2.Define(this)
                .WithTaggedFlags("ABCDSR2", 0, 32)
            ;

            Registers.InputFilterSlowClockDisable.Define(this)
                .WithTaggedFlags("IFSCDR", 0, 32)
            ;

            Registers.InputFilterSlowClockEnable.Define(this)
                .WithTaggedFlags("IFSCER", 0, 32)
            ;

            Registers.InputFilterSlowClockStatus.Define(this)
                .WithTaggedFlags("IFSCSR", 0, 32)
            ;

            Registers.SlowClockDividerDebouncing.Define(this)
                .WithTag("DIV", 0, 14)
                .WithReservedBits(14, 18)
            ;

            Registers.PadPullDownDisable.Define(this)
                .WithTaggedFlags("PPDDR", 0, 32)
            ;

            Registers.PadPullDownEnable.Define(this)
                .WithTaggedFlags("PPDER", 0, 32)
            ;

            Registers.PadPullDownStatus.Define(this)
                .WithTaggedFlags("PPDSR", 0, 32)
            ;

            Registers.OutputWriteEnable.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                outputDataWriteEnabled[i].Value = true;
                            }
                        },
                        name: "OWER")
            ;

            Registers.OutputWriteDisable.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                outputDataWriteEnabled[i].Value = false;
                            }
                        },
                        name: "OWDR")
            ;

            Registers.OutputWriteStatus.Define(this)
                .WithFlags(0, 32, out outputDataWriteEnabled, FieldMode.Read, name: "OWSR")
            ;

            Registers.AdditionalInterruptModesEnable.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                useAdditionalIrqMode[i].Value = true;
                                UpdateInterruptType(i);
                            }
                        },
                        name: "AIMER")
            ;

            Registers.AdditionalInterruptModesDisable.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                useAdditionalIrqMode[i].Value = false;
                                UpdateInterruptType(i);
                            }
                        },
                        name: "AIMDR")
            ;

            Registers.AdditionalInterruptModesMask.Define(this)
                .WithFlags(0, 32, out useAdditionalIrqMode, FieldMode.Read, name: "AIMMR")
            ;

            Registers.EdgeSelect.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                selectEdgeLevel[i].Value = false;
                                UpdateInterruptType(i);
                            }
                        },
                        name: "ESR")
            ;

            Registers.LevelSelect.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                selectEdgeLevel[i].Value = true;
                                UpdateInterruptType(i);
                            }
                        },
                        name: "LSR")
            ;

            Registers.EdgeLevelStatus.Define(this)
                .WithFlags(0, 32, out selectEdgeLevel, FieldMode.Read, name: "ELSR")
            ;

            Registers.FallingEdgeLowLevelSelect.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                selectFallRiseLowHigh[i].Value = false;
                                UpdateInterruptType(i);
                            }
                        },
                        name: "FELLSR")
            ;

            Registers.RisingEdgeHighLevelSelect.Define(this)
                .WithFlags(0, 32, FieldMode.WriteOneToClear,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                selectFallRiseLowHigh[i].Value = true;
                                UpdateInterruptType(i);
                            }
                        },
                        name: "REHLSR")
            ;

            Registers.FallRiseLowHighStatus.Define(this)
                .WithFlags(0, 32, out selectFallRiseLowHigh, FieldMode.Read, name: "FRLHSR")
            ;

            Registers.LockStatus.Define(this)
                .WithTaggedFlags("LOCKSR", 0, 32)
            ;

            Registers.WriteProtectionMode.Define(this)
                .WithTaggedFlag("WPEN", 0)
                .WithReservedBits(1, 7)
                .WithTag("WPKEY", 8, 24)
            ;

            Registers.WriteProtectionStatus.Define(this)
                .WithTaggedFlag("WPVS", 0)
                .WithReservedBits(1, 7)
                .WithTag("WPVSRC", 8, 16)
                .WithReservedBits(24, 8)
            ;

            Registers.SchmittTrigger.Define(this)
                .WithTaggedFlags("SCHMITT", 0, 32)
            ;

            Registers.ParallelCaptureMode.Define(this)
                .WithTaggedFlag("PCEN", 0)
                .WithReservedBits(1, 3)
                .WithTag("DSIZE", 4, 2)
                .WithReservedBits(6, 3)
                .WithTaggedFlag("ALWYS", 9)
                .WithTaggedFlag("HALFS", 10)
                .WithTaggedFlag("FRSTS", 11)
                .WithReservedBits(12, 20)
            ;

            Registers.ParallelCaptureInterruptEnable.Define(this)
                .WithTaggedFlag("DRDY", 0)
                .WithTaggedFlag("OVRE", 1)
                .WithTaggedFlag("ENDRX", 2)
                .WithTaggedFlag("RXBUFF", 3)
                .WithReservedBits(4, 28)
            ;

            Registers.ParallelCaptureInterruptDisable.Define(this)
                .WithTaggedFlag("DRDY", 0)
                .WithTaggedFlag("OVRE", 1)
                .WithTaggedFlag("ENDRX", 2)
                .WithTaggedFlag("RXBUFF", 3)
                .WithReservedBits(4, 28)
            ;

            Registers.ParallelCaptureInterruptMask.Define(this)
                .WithTaggedFlag("DRDY", 0)
                .WithTaggedFlag("OVRE", 1)
                .WithTaggedFlag("ENDRX", 2)
                .WithTaggedFlag("RXBUFF", 3)
                .WithReservedBits(4, 28)
            ;

            Registers.ParallelCaptureInterruptStatus.Define(this)
                .WithTaggedFlag("DRDY", 0)
                .WithTaggedFlag("OVRE", 1)
                .WithTaggedFlag("ENDRX", 2)
                .WithTaggedFlag("RXBUFF", 3)
                .WithReservedBits(4, 28)
            ;

            Registers.ParallelCaptureReceptionHolding.Define(this)
                .WithTag("RDATA", 0, 32)
            ;
        }

        private void UpdateIOLine(int number)
        {
            if((irqManager.PinDirection[number] & GPIOInterruptManager.Direction.Output) != 0 && enabled[number].Value)
            {
                this.Log(LogLevel.Debug, "Setting connection on pin {0} to {1}", number, outputData[number].Value);
                Connections[number].Set(outputData[number].Value);
            }
        }

        private void UpdateInterruptType(int number)
        {
            if(!useAdditionalIrqMode[number].Value)
            {
                irqManager.InterruptType[number] = GPIOInterruptManager.InterruptTrigger.BothEdges;
            }
            else if(selectEdgeLevel[number].Value && selectFallRiseLowHigh[number].Value)
            {
                irqManager.InterruptType[number] = GPIOInterruptManager.InterruptTrigger.ActiveHigh;
            }
            else if(!selectEdgeLevel[number].Value && selectFallRiseLowHigh[number].Value)
            {
                irqManager.InterruptType[number] = GPIOInterruptManager.InterruptTrigger.RisingEdge;
            }
            else if(selectEdgeLevel[number].Value && !selectFallRiseLowHigh[number].Value)
            {
                irqManager.InterruptType[number] = GPIOInterruptManager.InterruptTrigger.ActiveLow;
            }
            else
            {
                irqManager.InterruptType[number] = GPIOInterruptManager.InterruptTrigger.FallingEdge;
            }
            this.Log(LogLevel.Debug, "Setting interrupt trigger type to {0}", irqManager.InterruptType[number]);
        }

        private IFlagRegisterField[] enabled;
        private IFlagRegisterField[] outputData;
        private IFlagRegisterField[] outputDataWriteEnabled;
        private IFlagRegisterField[] useAdditionalIrqMode;
        private IFlagRegisterField[] selectEdgeLevel;
        private IFlagRegisterField[] selectFallRiseLowHigh;
        private readonly GPIOInterruptManager irqManager;
        private readonly object innerLock;

        private const int NumberOfPins = 32;

        public enum Registers
        {
            PioEnable = 0x0000, // WO
            PioDisable = 0x0004, // WO
            PioStatus = 0x0008, // RO
            // Reserved = 0x000C
            OutputEnable = 0x0010, // WO
            OutputDisable = 0x0014, // WO
            OutputStatus = 0x0018, // RO
            // Reserved = 0x001C
            GlitchInputFilterEnable = 0x0020, // WO
            GlitchInputFilterDisable = 0x0024, // WO
            GlitchInputFilterStatus = 0x0028, // RO
            // Reserved = 0x002C
            SetOutputData = 0x0030, // WO
            ClearOutputData = 0x0034, // WO
            OutputDataStatus = 0x0038, // (RO or RW)
            PinDataStatus = 0x003C, // RO
            InterruptEnable = 0x0040, // WO
            InterruptDisable = 0x0044, // WO
            InterruptMask = 0x0048, // RO
            InterruptStatus = 0x004C, //RO
            MultiDriverEnable = 0x0050, // WO
            MultiDriverDisable = 0x0054, // WO
            MultiDriverStatus = 0x0058, // RO
            // Reserved = 0x005C
            PullUpDisable = 0x0060, // WO
            PullUpEnable = 0x0064, // WO
            PadPullUpStatus = 0x0068, // RO
            // Reserved = 0x006C
            PeripheralSelect1 = 0x0070, // RW
            PeripheralSelect2 = 0x0074, // RW
            // Reserved = 0x0078–0x007C
            InputFilterSlowClockDisable = 0x0080, // WO
            InputFilterSlowClockEnable = 0x0084, // WO
            InputFilterSlowClockStatus = 0x0088, // RO
            SlowClockDividerDebouncing = 0x008C, // RW
            PadPullDownDisable = 0x0090, // WO
            PadPullDownEnable = 0x0094, // WO
            PadPullDownStatus = 0x0098, // RO
            // Reserved = 0x009C
            OutputWriteEnable = 0x00A0, // WO
            OutputWriteDisable = 0x00A4, // WO
            OutputWriteStatus = 0x00A8, // RO
            // Reserved = 0x00AC
            AdditionalInterruptModesEnable = 0x00B0, // WO
            AdditionalInterruptModesDisable = 0x00B4, // WO
            AdditionalInterruptModesMask = 0x00B8, // RO
            // Reserved = 0x00BC
            EdgeSelect = 0x00C0, // WO
            LevelSelect = 0x00C4, // WO
            EdgeLevelStatus = 0x00C8, // RO
            // Reserved = 0x00CC
            FallingEdgeLowLevelSelect = 0x00D0, // WO
            RisingEdgeHighLevelSelect = 0x00D4, // WO
            FallRiseLowHighStatus = 0x00D8, // RO
            // Reserved = 0x00DC
            LockStatus = 0x00E0, // RO
            WriteProtectionMode = 0x00E4, // RW
            WriteProtectionStatus = 0x00E8, // RO
            // Reserved = 0x00EC–0x00FC
            SchmittTrigger = 0x0100, // RW
            // Reserved = 0x0104–0x010C
            // Reserved = 0x0110
            // Reserved = 0x0114–0x011C
            // Reserved = 0x0120–0x014C
            ParallelCaptureMode = 0x0150, // RW
            ParallelCaptureInterruptEnable = 0x0154, // WO
            ParallelCaptureInterruptDisable = 0x0158, // WO
            ParallelCaptureInterruptMask = 0x015C, // RO
            ParallelCaptureInterruptStatus = 0x0160, // RO
            ParallelCaptureReceptionHolding = 0x0164, // RO
        }
    }
}
