//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public abstract class UARTDirectionBase
    {
        public UARTDirectionBase(IEmulationElement owner, Shifter shifter)
        {
            this.owner = owner;
            this.shifter = shifter;
            shifter.ControlOrConfigurationChanged += () => warningsShown = false;
        }

        public virtual void Reset()
        {
            warningsShown = false;
        }

        // The following properties support also unexpected configurations.
        public uint BaudRateDivider => (((shifter.Timer?.Compare ?? 0) & 0xFF) + 1) * 2 * (shifter.Timer?.Divider ?? 1);
        public Bits StopBits => shifter.StopBit == ShifterStopBitConfiguration ? Bits.One : Bits.None;

        protected void LogWarnings()
        {
            if(warningsShown)
            {
                return;
            }
            warningsShown = true;

            LogCommonWarnings();
            LogSpecificWarnings();
        }

        protected void LogWarningNonEqual(uint given, uint expected, string configuration, string block)
        {
            if(given != expected)
            {
                owner.Log(LogLevel.Warning, "{0}{1} of {2} is unexpected, given 0x{3:X}, expected 0x{4:X}", WarningPrefix, configuration, block, given, expected);
            }
        }

        protected void LogWarning(string message, params object[] arg)
        {
            owner.Log(LogLevel.Warning, WarningPrefix + message, arg);
        }

        protected abstract void LogSpecificWarnings();

        protected abstract string WarningPrefix { get; }

        protected readonly Shifter shifter;
        protected const uint Compare8bitShift = 2 * 8 - 1;
        protected const uint ShifterStopBitConfiguration = 0b11;

        private void LogCommonWarnings()
        {
            LogWarningNonEqual(shifter.StartBit, 0b10, "start bit", shifter.Name);
            LogWarningNonEqual(shifter.StopBit, ShifterStopBitConfiguration, "stop bit", shifter.Name);

            if(shifter.Timer == null)
            {
                LogWarning("No timer set for {1}", shifter.Name);
                return;
            }

            LogWarningNonEqual(shifter.Timer.Compare >> 8, Compare8bitShift, "compare value (upper bits)", shifter.Timer.Name);
            LogWarningNonEqual((uint)shifter.Timer.Decrement, (uint)TimerDecrement.OnFLEXIOClockDividedBy16, "decrement configuration", shifter.Timer.Name);
            LogWarningNonEqual((uint)shifter.Timer.StartBit, (uint)TimerStartBit.Always, "start bit", shifter.Timer.Name);
            LogWarningNonEqual((uint)shifter.Timer.StopBit, (uint)TimerStopBit.OnTimerDisable, "stop bit", shifter.Timer.Name);
            LogWarningNonEqual((uint)shifter.Timer.Mode, (uint)TimerMode.DualBaud, "mode", shifter.Timer.Name);
            LogWarningNonEqual((uint)shifter.Timer.OneTimeOperation, (uint)TimerTriggerOneTimeOperation.Normal, "one time operation", shifter.Timer.Name);
        }

        private IEmulationElement owner;
        private bool warningsShown;
    }
}
