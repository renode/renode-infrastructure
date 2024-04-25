//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Migrant;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.SENT;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class S32K3XX_FlexIO_SENT : NullRegistrationPointPeripheralContainer<ISENTPeripheral>, IEndpoint
    {
        public S32K3XX_FlexIO_SENT(IMachine machine, uint timerId, long? frequency = null)
            : base(machine)
        {
            this.timerId = timerId;
            this.frequency = frequency;
        }

        public void RegisterInFlexIO(S32K3XX_FlexIO flexIO)
        {
            this.flexIO = flexIO;
            if(!flexIO.TimersManager.Reserve(this, timerId, out timer))
            {
                throw new ConstructionException($"Timer with id: {timerId} could not be reserved");
            }

            sysbus = flexIO.GetMachine().GetSystemBus(flexIO);
            innerTimer = new LimitTimer(Machine.ClockSource, frequency ?? flexIO.Frequency, this, $"SENT Timer{timerId}", TimerLimit, divider: (int)timer.Divider);
            timer.ConfigurationChanged += ConfigureTimer;
            timer.ControlChanged += ConfigureTimer;
        }

        public override void Reset()
        {
            if(RegisteredPeripheral != null)
            {
                RegisteredPeripheral.TransmissionEnabled = false;
            }
            innerTimer?.Reset();
        }

        public override void Register(ISENTPeripheral peripheral, NullRegistrationPoint registrationPoint)
        {
            base.Register(peripheral, registrationPoint);
            peripheral.SENTEdgeChanged += EdgeHandler;
        }

        public override void Unregister(ISENTPeripheral peripheral)
        {
            base.Unregister(peripheral);
            peripheral.SENTEdgeChanged -= EdgeHandler;
        }

        private void EdgeHandler(SENTEdge edge)
        {
            if(edge != SENTEdge.Falling)
            {
                return;
            }

            if(sysbus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }

            var timerValue = (uint)(TimerLimit - innerTimer.Value);
            timer.Compare = timerValue;
            timer.Status.SetFlag(true);
            innerTimer.ResetValue();
        }

        private void ConfigureTimer()
        {
            var enableSENT =
                timer.TriggerSource == TimerTriggerSource.Internal &&
                timer.Disable == TimerDisable.OnTriggerFallingEdge &&
                timer.Mode == TimerMode.SingleInputCapture;

            innerTimer.Divider = (int)timer.Divider;
            innerTimer.Frequency = frequency ?? flexIO.Frequency;
            innerTimer.Enabled = enableSENT;

            if(RegisteredPeripheral != null)
            {
                RegisteredPeripheral.TransmissionEnabled = enableSENT;
            }

            this.DebugLog("SENT transmission {0}", enableSENT ? "enabled" : "disabled");
        }

        private S32K3XX_FlexIO flexIO;
        private Timer timer;
        private LimitTimer innerTimer;
        private IBusController sysbus;

        private readonly uint timerId;
        private readonly long? frequency;

        private const ulong TimerLimit = (1 << 16) - 1;
    }
}
