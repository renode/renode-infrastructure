//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Logging;

using Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class UARTTransmitter : UARTDirectionBase
    {
        public UARTTransmitter(IEmulationElement owner, Shifter shifter) : base(owner, shifter)
        {
            shifter.DataTransmitted += OnTransmission;
        }

        public event Action<byte> CharReceived;

        public event Action<ushort> WordReceived;

        public event Action<uint> DoubleWordReceived;

        protected override void LogSpecificWarnings()
        {
            LogWarningNonEqual((uint)shifter.TimerPolarity, (uint)ShifterPolarity.OnPosedge, "timer polarity", shifter.Name);
            if(shifter.Timer != null)
            {
                LogWarningNonEqual((uint)shifter.Timer.Enable, (uint)TimerEnable.OnTriggerHigh, "enable configuration", shifter.Timer.Name);
                LogWarningNonEqual((uint)shifter.Timer.Disable, (uint)TimerDisable.OnTimerCompare, "disable configuration", shifter.Timer.Name);
                LogWarningNonEqual((uint)shifter.Timer.Output, (uint)TimerOutput.One, "output configuration", shifter.Timer.Name);
                LogWarningNonEqual((uint)shifter.Timer.ResetMode, (uint)TimerReset.Never, "reset configuration", shifter.Timer.Name);

                LogWarningNonEqual((uint)shifter.Timer.TriggerPolarity, (uint)TimerTriggerPolarity.ActiveLow, "trigger polarity", shifter.Timer.Name);
                LogWarningNonEqual((uint)shifter.Timer.TriggerSource, (uint)TimerTriggerSource.Internal, "trigger source", shifter.Timer.Name);
                LogWarningNonEqual((uint)shifter.Timer.TriggerPolarity, (uint)TimerTriggerPolarity.ActiveLow, "trigger polarity", shifter.Timer.Name);
                LogWarningNonEqual(shifter.Timer.TriggerSelect, Timer.EncodeShifterAsTriggerSelect(shifter), "trigger select", shifter.Timer.Name);
            }
        }

        protected override string WarningPrefix => "Invalid configuration of transmitter: ";

        private void OnTransmission(uint value)
        {
            // Ignore driver sending a frame with only ones (which is an idle state on bus).
            if(value == uint.MaxValue && shifter.StartBit == ShifterStopBitConfiguration)
            {
                return;
            }

            LogWarnings();

            switch(shifter.Timer.NumberOfBitsInWord)
            {
            case 8:
                CharReceived?.Invoke((byte)value);
                break;
            case 16:
                WordReceived?.Invoke((ushort)value);
                break;
            case 32:
                DoubleWordReceived?.Invoke(value);
                break;
            default:
                owner.WarningLog("Timer configured for {0} bit transfer. The only supported transfer widths are 8, 16 and 32");
                break;
            }

            shifter.Status.SetFlag(true);
            // Trigger timer expired (whole data frame sent).
            shifter.Timer?.Status.SetFlag(true, true);
        }
    }
}