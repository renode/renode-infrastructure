//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.ObjectModel;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class STM32_EXTICore
    {
        public STM32_EXTICore(IPeripheral parent, ulong lineConfigurableMask, bool treatOutOfRangeLinesAsDirect = false, bool allowMaskingDirectLines = true)
        {
            this.parent = parent;
            this.treatOutOfRangeLinesAsDirect = treatOutOfRangeLinesAsDirect;
            this.allowMaskingDirectLines = allowMaskingDirectLines;
            LineConfigurableMask = lineConfigurableMask;
        }

        public bool CanSetInterruptValue(byte lineNumber, bool value, out bool isLineConfigurable)
        {
            // Line out of range will never be reported as configurable
            isLineConfigurable = IsLineConfigurable(lineNumber, out var isOutOfRange);
            if(isOutOfRange)
            {
                return treatOutOfRangeLinesAsDirect;
            }

            if(!BitHelper.IsBitSet(InterruptMask.Value, lineNumber))
            {
                if(isLineConfigurable || allowMaskingDirectLines)
                {
                    return false;
                }
            }

            if(isLineConfigurable)
            {
                return (BitHelper.IsBitSet(RisingEdgeMask.Value, lineNumber) && value) ||  // rising edge
                       (BitHelper.IsBitSet(FallingEdgeMask.Value, lineNumber) && !value);  // falling edge
            }

            return true;
        }

        public bool IsLineConfigurable(byte lineNumber, out bool isOutOfRange)
        {
            if(lineNumber >= MaxLineNumber)
            {
                if(!treatOutOfRangeLinesAsDirect)
                {
                    parent.Log(LogLevel.Error, "Invalid line number: {0}. Accepted range is [0;{1})", lineNumber, MaxLineNumber);
                }
                isOutOfRange = true;
                return false;
            }
            isOutOfRange = false;
            return BitHelper.IsBitSet(LineConfigurableMask, lineNumber);
        }

        public void UpdatePendingValue(byte bit, bool value)
        {
            var reg = PendingInterrupts.Value;
            BitHelper.SetBit(ref reg, bit, value);
            PendingInterrupts.Value = reg;
        }

        // Those fields have to be public, as they should be used as out parameters
        // You could use ref properties instead, but mono may generate bad IL for them

        public IValueRegisterField InterruptMask;
        public IValueRegisterField RisingEdgeMask;
        public IValueRegisterField FallingEdgeMask;
        public IValueRegisterField PendingInterrupts;

        public ulong LineConfigurableMask { get; }

        private readonly IPeripheral parent;

        private readonly bool treatOutOfRangeLinesAsDirect;
        private readonly bool allowMaskingDirectLines;

        // Max number is 64 due to using ulongs as backing values
        private const byte MaxLineNumber = 64;
    }
}
