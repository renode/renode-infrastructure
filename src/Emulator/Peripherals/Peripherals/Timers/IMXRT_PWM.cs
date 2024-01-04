//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Time;
using Antmicro.Renode.Debugging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class IMXRT_PWM : BasicWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public IMXRT_PWM(IMachine machine, long frequency = 10000000) : base(machine)
        {
            halfCycleTimer = new ComparingTimer(machine.ClockSource, frequency, this, "halfCycleTimer", compare: ushort.MaxValue, limit: ushort.MaxValue, workMode: WorkMode.Periodic, enabled: false, eventEnabled: true);
            fullCycleTimer = new ComparingTimer(machine.ClockSource, frequency, this, "fullCycleTimer", compare: ushort.MaxValue, limit: ushort.MaxValue, workMode: WorkMode.Periodic, enabled: false, eventEnabled: true);

            halfCycleTimer.CompareReached += () => OnCompare(false);
            fullCycleTimer.CompareReached += () => OnCompare(true);

            Connections = new ReadOnlyDictionary<int, IGPIO>(Enumerable.Range(0, NumberOfSubmodules * 3 + 2).ToDictionary<int, int, IGPIO>(x => x, x => new GPIO()));
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            UpdateInterrupts();
        }

        // The Connections is composed of Compare, Capture and Reload IRQ triplets
        // in that order for every submodule in ascending index order. The second
        // to last and last connection are Reload Error and Fault in that order.
        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x200;

        private void UpdateInterrupts()
        {
            // At the moment only Reload IRQ for submodule 0 is implemented
            GetReloadIRQ(0).Set(reloadFlag.Value && reloadInterruptEnable.Value);
            for(var i = 0; i < NumberOfSubmodules; ++i)
            {
                this.Log(LogLevel.Debug, "Setting submodule#{0} events: compare {1}, capture {2}, reload {3}.", i, GetCompareIRQ(i).IsSet, GetCaptureIRQ(i).IsSet, GetReloadIRQ(i).IsSet);
            }
            this.Log(LogLevel.Debug, "Setting reload error {0} and fault {1}", ReloadError.IsSet, Fault.IsSet);
        }

        private void DefineRegisters()
        {
            Registers.MasterControl.Define(this)
                .WithTag("LDOK - Load Okay", 0, 4)
                .WithTag("CLDOK - Clear Load Okay", 4, 4)
                .WithFlags(8, 4, name: "RUN - Run", writeCallback: (idx, _, val) =>
                {
                    ConfigureSubmodule(idx, val);
                })
                .WithTag("IPOL - Current Polarity", 12, 4)
            ;

            Registers.Submodule0InitialCount.Define(this)
                .WithValueField(0, 16, out initValue, name: "INIT - Initial Count Register Bits");

            Registers.Submodule0Value0.Define(this)
                .WithValueField(0, 16, out value0, name: "VAL0 - Value Register 0");

            Registers.Submodule0Value1.Define(this)
                .WithValueField(0, 16, out value1, name: "VAL1 - Value Register 1");

            Registers.Submodule0Control.Define(this)
                .WithTaggedFlag("DBLEN - Double Switching Enable", 0)
                .WithTaggedFlag("DBLX - PWMX Double Switching Enable", 1)
                .WithTaggedFlag("LDMOD - Load Mode Select", 2)
                .WithTaggedFlag("SPLIT - Split the DBLPWM signal to PWMA and PWMB", 3)
                .WithTag("PRSC - Prescaler", 4, 3)
                .WithTaggedFlag("COMPMODE - Compare Mode", 7)
                .WithTag("DT - Deadtime", 8, 2)
                .WithFlag(10, out fullCycleReload, name: "FULL - Full Cycle Reload")
                .WithFlag(11, out halfCycleReload, name: "HALF - Half Cycle Reload")
                .WithTag("LDFQ - Load Frequency", 12, 4);

            Registers.Submodule0Status.Define(this)
                .WithTag("CMPF - Compare Flags", 0, 6)
                .WithTaggedFlag("CFX0 - Capture Flag X0", 6)
                .WithTaggedFlag("CFX1 - Capture Flag X1", 7)
                .WithTaggedFlag("CFB0 - Capture Flag B0", 8)
                .WithTaggedFlag("CFB1 - Capture Flag B1", 9)
                .WithTaggedFlag("CFA0 - Capture Flag A0", 10)
                .WithTaggedFlag("CFA1 - Capture Flag A1", 11)
                .WithFlag(12, out reloadFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "RF - Reload Flag")
                .WithTaggedFlag("REF - Reload Error Flag", 13)
                .WithTaggedFlag("RUF - Registers Updated Flag", 14)
                .WithReservedBits(15, 1)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.Submodule0InterruptEnable.Define(this)
                .WithTag("CMPIE - Compare Interrupt Enables", 0, 6)
                .WithTaggedFlag("CX0IE - Capture X 0 Interrupt Enable", 6)
                .WithTaggedFlag("CX1IE - Capture X 1 Interrupt Enable", 7)
                .WithTaggedFlag("CB0IE - Capture B 0 Interrupt Enable", 8)
                .WithTaggedFlag("CB1IE - Capture B 1 Interrupt Enable", 9)
                .WithTaggedFlag("CA0IE - Capture A 0 Interrupt Enable", 10)
                .WithTaggedFlag("CA1IE - Capture A 1 Interrupt Enable", 11)
                .WithFlag(12, out reloadInterruptEnable, name: "RIE - Reload Interrupt Enable")
                .WithTaggedFlag("REIE - Reload Error Interrupt Enable", 13)
                .WithReservedBits(14, 2)
                .WithWriteCallback((_, __) => UpdateInterrupts());
        }

        private void ConfigureSubmodule(int idx, bool enabled)
        {
            DebugHelper.Assert(idx >= 0 && idx < NumberOfSubmodules);

            if(idx != 0)
            {
                this.Log(LogLevel.Warning, "Currently only submodule 0 is supported");
                return;
            }

            if(enabled)
            {
                ReloadTimer(false);
                ReloadTimer(true);
            }

            halfCycleTimer.Enabled = enabled;
            fullCycleTimer.Enabled = enabled;
        }

        private void OnCompare(bool fullCycle)
        {
            if(fullCycle && fullCycleReload.Value
                || !fullCycle && halfCycleReload.Value)
            {
                ReloadTimer(fullCycle);
            }

            reloadFlag.Value = true;
            UpdateInterrupts();
        }

        private void ReloadTimer(bool fullCycle)
        {
            var timer = fullCycle
                ? fullCycleTimer
                : halfCycleTimer;

            var valueRegister = fullCycle
                ? value1
                : value0;

            // this model supports signed values
            // in registers in order to make symmetric patterns easier
            // to configure (less calculations needed);
            // our timer infrastructure operates on unsiged values
            // that's why we need to calculate and add offset
            var initSignedValue = (short)initValue.Value;
            if(initSignedValue < 0)
            {
                timer.Value = 0;
                timer.Compare = valueRegister.Value + (ushort)(-initSignedValue);
            }
            else
            {
                timer.Value = initValue.Value;
                timer.Compare = valueRegister.Value;
            }
        }

        private IGPIO GetCompareIRQ(int index)
        {
            return Connections[index * 3];
        }

        private IGPIO GetCaptureIRQ(int index)
        {
            return Connections[index * 3 + 1];
        }

        private IGPIO GetReloadIRQ(int index)
        {
            return Connections[index * 3 + 2];
        }

        private IGPIO ReloadError => Connections[NumberOfSubmodules * 3];
        private IGPIO Fault => Connections[NumberOfSubmodules * 3 + 1];

        private IFlagRegisterField reloadInterruptEnable;
        private IFlagRegisterField reloadFlag;
        private IFlagRegisterField halfCycleReload;
        private IFlagRegisterField fullCycleReload;
        private IValueRegisterField value0;
        private IValueRegisterField value1;
        private IValueRegisterField initValue;

        private ComparingTimer halfCycleTimer;
        private ComparingTimer fullCycleTimer;

        private const int NumberOfSubmodules = 4;

        private enum Registers
        {
            Submodule0Counter = 0x000,
            Submodule0InitialCount = 0x002,
            Submodule0Control2 = 0x004,
            Submodule0Control = 0x006,
            Submodule0Value0 = 0x00A,
            Submodule0FractionalValue1 = 0x00C,
            Submodule0Value1 = 0x00E,
            Submodule0FractionalValue2 = 0x010,
            Submodule0Value2 = 0x012,
            Submodule0FractionalValue3 = 0x014,
            Submodule0Value3 = 0x016,
            Submodule0FractionalValue4 = 0x018,
            Submodule0Value4 = 0x01A,
            Submodule0FractionalValue5 = 0x01C,
            Submodule0Value5 = 0x01E,
            Submodule0FractionalControl = 0x020,
            Submodule0OutputControl = 0x022,
            Submodule0Status = 0x024,
            Submodule0InterruptEnable = 0x026,
            Submodule0DMAEnable = 0x028,
            Submodule0OutputTriggerControl = 0x02A,
            Submodule0FaultDisableMapping0 = 0x02C,
            Submodule0FaultDisableMapping1 = 0x02E,
            Submodule0DeadtimeCount0 = 0x030,
            Submodule0DeadtimeCount1 = 0x032,
            Submodule0CaptureControlA = 0x034,
            Submodule0CaptureCompareA = 0x036,
            Submodule0CaptureControlB = 0x038,
            Submodule0CaptureCompareB = 0x03A,
            Submodule0CaptureControlX = 0x03C,
            Submodule0CaptureCompareX = 0x03E,
            Submodule0CaptureValue0 = 0x040,
            Submodule0CaptureValue0Cycle = 0x042,
            Submodule0CaptureValue1 = 0x044,
            Submodule0CaptureValue1Cycle = 0x046,
            Submodule0CaptureValue2 = 0x048,
            Submodule0CaptureValue2Cycle = 0x04A,
            Submodule0CaptureValue3 = 0x04C,
            Submodule0CaptureValue3Cycle = 0x04E,
            Submodule0CaptureValue4 = 0x050,
            Submodule0CaptureValue4Cycle = 0x052,
            Submodule0CaptureValue5 = 0x054,
            Submodule0CaptureValue5Cycle = 0x056,

            Submodule1Counter = 0x060,
            Submodule1InitialCount = 0x062,
            Submodule1Control2 = 0x064,
            Submodule1Control = 0x066,
            Submodule1Value0 = 0x06A,
            Submodule1FractionalValue1 = 0x06C,
            Submodule1Value1 = 0x06E,
            Submodule1FractionalValue2 = 0x070,
            Submodule1Value2 = 0x072,
            Submodule1FractionalValue3 = 0x074,
            Submodule1Value3 = 0x076,
            Submodule1FractionalValue4 = 0x078,
            Submodule1Value4 = 0x07A,
            Submodule1FractionalValue5 = 0x07C,
            Submodule1Value5 = 0x07E,
            Submodule1FractionalControl = 0x080,
            Submodule1OutputControl = 0x082,
            Submodule1Status = 0x084,
            Submodule1InterruptEnable = 0x086,
            Submodule1DMAEnable = 0x088,
            Submodule1OutputTriggerControl = 0x08A,
            Submodule1FaultDisableMapping0 = 0x08C,
            Submodule1FaultDisableMapping1 = 0x08E,
            Submodule1DeadtimeCount0 = 0x090,
            Submodule1DeadtimeCount1 = 0x092,
            Submodule1CaptureControlA = 0x094,
            Submodule1CaptureCompareA = 0x096,
            Submodule1CaptureControlB = 0x098,
            Submodule1CaptureCompareB = 0x09A,
            Submodule1CaptureControlX = 0x09C,
            Submodule1CaptureCompareX = 0x09E,
            Submodule1CaptureValue0 = 0x0A0,
            Submodule1CaptureValue0Cycle = 0x0A2,
            Submodule1CaptureValue1 = 0x0A4,
            Submodule1CaptureValue1Cycle = 0x0A6,
            Submodule1CaptureValue2 = 0x0A8,
            Submodule1CaptureValue2Cycle = 0x0AA,
            Submodule1CaptureValue3 = 0x0AC,
            Submodule1CaptureValue3Cycle = 0x0AE,
            Submodule1CaptureValue4 = 0x0B0,
            Submodule1CaptureValue4Cycle = 0x0B2,
            Submodule1CaptureValue5 = 0x0B4,
            Submodule1CaptureValue5Cycle = 0x0B6,

            Submodule2Counter = 0x0C0,
            Submodule2InitialCount = 0x0C2,
            Submodule2Control2 = 0x0C4,
            Submodule2Control = 0x0C6,
            Submodule2Value0 = 0x0CA,
            Submodule2FractionalValue1 = 0x0CC,
            Submodule2Value1 = 0x0CE,
            Submodule2FractionalValue2 = 0x0D0,
            Submodule2Value2 = 0x0D2,
            Submodule2FractionalValue3 = 0x0D4,
            Submodule2Value3 = 0x0D6,
            Submodule2FractionalValue4 = 0x0D8,
            Submodule2Value4 = 0x0DA,
            Submodule2FractionalValue5 = 0x0DC,
            Submodule2Value5 = 0x0DE,
            Submodule2FractionalControl = 0x0E0,
            Submodule2OutputControl = 0x0E2,
            Submodule2Status = 0x0E4,
            Submodule2InterruptEnable = 0x0E6,
            Submodule2DMAEnable = 0x0E8,
            Submodule2OutputTriggerControl = 0x0EA,
            Submodule2FaultDisableMapping0 = 0x0EC,
            Submodule2FaultDisableMapping1 = 0x0EE,
            Submodule2DeadtimeCount0 = 0x0F0,
            Submodule2DeadtimeCount1 = 0x0F2,
            Submodule2CaptureControlA = 0x0F4,
            Submodule2CaptureCompareA = 0x0F6,
            Submodule2CaptureControlB = 0x0F8,
            Submodule2CaptureCompareB = 0x0FA,
            Submodule2CaptureControlX = 0x0FC,
            Submodule2CaptureCompareX = 0x0FE,
            Submodule2CaptureValue0 = 0x100,
            Submodule2CaptureValue0Cycle = 0x102,
            Submodule2CaptureValue1 = 0x104,
            Submodule2CaptureValue1Cycle = 0x106,
            Submodule2CaptureValue2 = 0x108,
            Submodule2CaptureValue2Cycle = 0x10A,
            Submodule2CaptureValue3 = 0x10C,
            Submodule2CaptureValue3Cycle = 0x10E,
            Submodule2CaptureValue4 = 0x110,
            Submodule2CaptureValue4Cycle = 0x112,
            Submodule2CaptureValue5 = 0x114,
            Submodule2CaptureValue5Cycle = 0x116,

            Submodule3Counter = 0x120,
            Submodule3InitialCount = 0x122,
            Submodule3Control2 = 0x124,
            Submodule3Control = 0x126,
            Submodule3Value0 = 0x12A,
            Submodule3FractionalValue1 = 0x12C,
            Submodule3Value1 = 0x12E,
            Submodule3FractionalValue2 = 0x130,
            Submodule3Value2 = 0x132,
            Submodule3FractionalValue3 = 0x134,
            Submodule3Value3 = 0x136,
            Submodule3FractionalValue4 = 0x138,
            Submodule3Value4 = 0x13A,
            Submodule3FractionalValue5 = 0x13C,
            Submodule3Value5 = 0x13E,
            Submodule3FractionalControl = 0x140,
            Submodule3OutputControl = 0x142,
            Submodule3Status = 0x144,
            Submodule3InterruptEnable = 0x146,
            Submodule3DMAEnable = 0x148,
            Submodule3OutputTriggerControl = 0x14A,
            Submodule3FaultDisableMapping0 = 0x14C,
            Submodule3FaultDisableMapping1 = 0x14E,
            Submodule3DeadtimeCount0 = 0x150,
            Submodule3DeadtimeCount1 = 0x152,
            Submodule3CaptureControlA = 0x154,
            Submodule3CaptureCompareA = 0x156,
            Submodule3CaptureControlB = 0x158,
            Submodule3CaptureCompareB = 0x15A,
            Submodule3CaptureControlX = 0x15C,
            Submodule3CaptureCompareX = 0x15E,
            Submodule3CaptureValue0 = 0x160,
            Submodule3CaptureValue0Cycle = 0x162,
            Submodule3CaptureValue1 = 0x164,
            Submodule3CaptureValue1Cycle = 0x166,
            Submodule3CaptureValue2 = 0x168,
            Submodule3CaptureValue2Cycle = 0x16A,
            Submodule3CaptureValue3 = 0x16C,
            Submodule3CaptureValue3Cycle = 0x16E,
            Submodule3CaptureValue4 = 0x170,
            Submodule3CaptureValue4Cycle = 0x172,
            Submodule3CaptureValue5 = 0x174,
            Submodule3CaptureValue5Cycle = 0x176,

            OutputEnable = 0x180,
            Mask = 0x182,
            SoftwareControlledOutput = 0x184,
            PWMSourceSelect = 0x186,
            MasterControl = 0x188,
            MasterControl2 = 0x18A,
            FaultControl = 0x18C,
            FaultStatus = 0x18E,
            FaultFilter = 0x190,
            FaultTest = 0x192,
            FaultControl2 = 0x194,
        }
    }
}
