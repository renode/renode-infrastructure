//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class UARTReceiver : UARTDirectionBase
    {
        public UARTReceiver(IEmulationElement owner, Shifter shifter) : base(owner, shifter) { }

        public void WriteChar(byte value)
        {
            LogWarnings();

            // The input data is shifted into the buffer from the left side (MSB).
            shifter.OnDataReceive((uint)value << 24);
        }

        protected override void LogSpecificWarnings()
        {
            LogWarningNonEqual((uint)shifter.TimerPolarity, (uint)ShifterPolarity.OnNegedge, "timer polarity", shifter.Name);
            if(shifter.Timer != null)
            {
                LogWarningNonEqual((uint)shifter.Timer.Enable, (uint)TimerEnable.OnPinRisingEdge, "enable configuration", shifter.Timer.Name);
                LogWarningNonEqual((uint)shifter.Timer.Disable, (uint)TimerDisable.OnTimerCompare, "disable configuration", shifter.Timer.Name);
                LogWarningNonEqual((uint)shifter.Timer.Output, (uint)TimerOutput.OneOnResetToo, "output configuration", shifter.Timer.Name);
                LogWarningNonEqual((uint)shifter.Timer.ResetMode, (uint)TimerReset.OnPinRisingEdge, "reset configuration", shifter.Timer.Name);

                LogWarningNonEqual((uint)shifter.Timer.TriggerSource, (uint)TimerTriggerSource.External, "trigger source", shifter.Timer.Name);
                LogWarningNonEqual((uint)shifter.Timer.TriggerPolarity, (uint)TimerTriggerPolarity.ActiveHigh, "trigger polarity", shifter.Timer.Name);
            }
        }

        protected override string WarningPrefix => "Invalid configuration of receiver: ";
    }
}
