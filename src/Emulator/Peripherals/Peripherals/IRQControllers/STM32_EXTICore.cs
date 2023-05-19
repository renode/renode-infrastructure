//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class STM32_EXTICore
    {
        public STM32_EXTICore(IPeripheral parent, ulong lineConfigurableMask, bool treatOutOfRangeLinesAsDirect = false,
                              bool separateConfigs = false, bool allowMaskingDirectLines = true)
        {
            this.parent = parent;
            this.treatOutOfRangeLinesAsDirect = treatOutOfRangeLinesAsDirect;
            this.allowMaskingDirectLines = allowMaskingDirectLines;
            this.separateConfigs = separateConfigs;
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

            // When using separeteConfigs the 'InterruptMask' is not used
            if(!separateConfigs && !BitHelper.IsBitSet(InterruptMask.Value, lineNumber))
            {
                if(isLineConfigurable || allowMaskingDirectLines)
                {
                    return false;
                }
            }

            if(isLineConfigurable)
            {
                var raisingEvent = (BitHelper.IsBitSet(RisingEdgeMask.Value, lineNumber) && value);
                var fallingEvent = (BitHelper.IsBitSet(FallingEdgeMask.Value, lineNumber) && !value);

                return raisingEvent || fallingEvent;
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
            IValueRegisterField registerField;
            if(separateConfigs)
            {
                var isRaising = (value != true);
                registerField = isRaising ? PendingRaisingInterrupts : PendingFallingInterrupts;
            }
            else
            {
                registerField = PendingInterrupts;
            }
            var reg = registerField.Value;
            BitHelper.SetBit(ref reg, (byte)bit, value);
            registerField.Value = reg;
        }

        // Those fields have to be public, as they should be used as out parameters
        // You could use ref properties instead, but mono may generate bad IL for them

        public IValueRegisterField InterruptMask;
        public IValueRegisterField PendingInterrupts;
        public IValueRegisterField RisingEdgeMask;
        public IValueRegisterField FallingEdgeMask;
        // Below fields are used only when the 'separateConfigs' is set
        public IValueRegisterField PendingRaisingInterrupts;
        public IValueRegisterField PendingFallingInterrupts;

        public ulong LineConfigurableMask { get; }

        private readonly IPeripheral parent;

        private readonly bool treatOutOfRangeLinesAsDirect;
        private readonly bool allowMaskingDirectLines;
        private readonly bool separateConfigs;

        // Max number is 64 due to using ulongs as backing values
        private const byte MaxLineNumber = 64;
    }
}
