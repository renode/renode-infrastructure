//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class SiLabs_HFXO_2 : SiLabsPeripheral, SiLabs_IHFXO
    {
        public SiLabs_HFXO_2(Machine machine, uint startupDelayTicks) : base(machine)
        {
            this.delayTicks = startupDelayTicks;

            timer = new LimitTimer(machine.ClockSource, 32768, this, "hfxodelay", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += OnStartUpTimerExpired;

            IRQ = new GPIO();

            registersCollection = BuildRegistersCollection();
        }

        public override void Reset()
        {
            base.Reset();

            timer.Enabled = false;
            wakeUpSource = WakeUpSource.None;
        }

        public void OnRequest(HFXO_REQUESTER req)
        {
            this.Log(LogLevel.Error, "OnRequest not implemented");
        }

        public void OnEm2Wakeup()
        {
            HfxoEnabled?.Invoke();
            this.Log(LogLevel.Error, "OnEm2Wakeup not implemented");
        }

        public void OnClksel()
        {
            this.Log(LogLevel.Error, "OnClksel not implemented");
        }

        public GPIO IRQ { get; }

        public event Action HfxoEnabled;

        protected override void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var irq = (readyInterruptEnable.Value && readyInterrupt.Value);
                IRQ.Set(irq);
            });
        }

        protected override DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this, 0x00000002)
                    .WithFlag(0, out forceEnable, writeCallback: (oldValue, newValue) =>
                    {
                        if (!oldValue && newValue)
                        {
                            enabled.Value = true;
                            wakeUpSource = WakeUpSource.Force;
                            StartDelayTimer();
                        }
                        else if (!newValue && disableOnDemand.Value)
                        {
                            enabled.Value = false;
                        }
                    }, name: "FORCEEN")
                    .WithFlag(1, out disableOnDemand, writeCallback: (_, value) =>
                    {
                        if (value && !forceEnable.Value)
                        {
                            enabled.Value = false;
                        }
                        if (!value)
                        {
                            fsmLock.Value = true;
                        }
                    }, name: "DISONDEMAND")
                    .WithTaggedFlag("KEEPWARM", 2)
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("FORCEXI2GNDANA", 4)
                    .WithTaggedFlag("FORCEXO2GNDANA", 5)
                    .WithReservedBits(6, 26)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithFlag(0, out ready, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        coreBiasReady.Value = true;
                    }, name: "COREBIASOPT")
                    .WithFlag(1, out ready, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        if (coreBiasReady.Value)
                        {
                            if (forceEnable.Value && disableOnDemand.Value)
                            {
                                fsmLock.Value = false;
                            }
                        }
                    }, name: "MANUALOVERRIDE")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, out ready, FieldMode.Read, name: "RDY")
                    .WithFlag(1, out coreBiasReady, FieldMode.Read, name: "COREBIASOPTRDY")
                    .WithReservedBits(2, 14)
                    .WithFlag(16, out enabled, FieldMode.Read, name: "ENS")
                    .WithTaggedFlag("HWREQ", 17)
                    .WithReservedBits(18, 1)
                    .WithTaggedFlag("ISWARM", 19)
                    .WithReservedBits(20, 10)
                    .WithFlag(30, out fsmLock, FieldMode.Read, name: "FSMLOCK")
                    .WithFlag(31, out locked, FieldMode.Read, name: "LOCK")
                },
                {(long)Registers.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out readyInterrupt, name: "RDY")
                    .WithTaggedFlag("COREBIASOPTRDY", 1)
                    .WithReservedBits(2, 27)
                    .WithTaggedFlag("DNSERR", 29)
                    .WithReservedBits(30, 1)
                    .WithTaggedFlag("COREBIASOPTERR", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out readyInterruptEnable, name: "RDY")
                    .WithTaggedFlag("COREBIASOPTRDY", 1)
                    .WithReservedBits(2, 27)
                    .WithTaggedFlag("DNSERR", 29)
                    .WithReservedBits(30, 1)
                    .WithTaggedFlag("COREBIASOPTERR", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Lock, new DoubleWordRegister(this)
                    .WithValueField(0, 16, writeCallback: (_, value) =>
                    {
                        locked.Value = (value != UnlockCode);
                    }, name: "LOCKKEY")
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override Type RegistersType => typeof(Registers);

        private void StartDelayTimer()
        {
            // Function which starts the start-up delay timer
            timer.Enabled = false;
            timer.Limit = delayTicks;
            timer.Enabled = true;
        }

        private void OnStartUpTimerExpired()
        {
            this.Log(LogLevel.Debug, "Start-up delay timer expired at: {0}", machine.ElapsedVirtualTime);
            this.Log(LogLevel.Debug, "Wakeup Requester = {0}", wakeUpSource);

            if(wakeUpSource == WakeUpSource.Force)
            {
                ready.Value = true;
                coreBiasReady.Value = true;
                readyInterrupt.Value = true;
                wakeUpSource = WakeUpSource.None;
            }
            else
            {
                this.Log(LogLevel.Error, "Wake up source {0} not implemented", wakeUpSource);
            }

            timer.Enabled = false;
            UpdateInterrupts();
        }

        private IFlagRegisterField ready;
        // Interrupts
        private IFlagRegisterField readyInterrupt;
        private WakeUpSource wakeUpSource = WakeUpSource.None;
        private IFlagRegisterField coreBiasReady;
        private IFlagRegisterField readyInterruptEnable;
        private IFlagRegisterField disableOnDemand;
        private IFlagRegisterField forceEnable;
        private IFlagRegisterField fsmLock;
        private IFlagRegisterField locked;
        private IFlagRegisterField enabled;
        private readonly uint delayTicks;
        private readonly LimitTimer timer;
        private const uint UnlockCode = 0x580E;

        private enum Registers
        {
            IpVersion               = 0x0000,
            CrystalConfig           = 0x0010,
            CrystalControl          = 0x0018,
            Config                  = 0x0020,
            Control                 = 0x0028,
            Command                 = 0x0050,
            Status                  = 0x0058,
            InterruptFlags          = 0x0070,
            InterruptEnable         = 0x0074,
            Lock                    = 0x0080,
            // Set registers
            IpVersion_Set           = 0x1000,
            CrystalConfig_Set       = 0x1010,
            CrystalControl_Set      = 0x1018,
            Config_Set              = 0x1020,
            Control_Set             = 0x1028,
            Command_Set             = 0x1050,
            Status_Set              = 0x1058,
            InterruptFlags_Set      = 0x1070,
            InterruptEnable_Set     = 0x1074,
            Lock_Set                = 0x1080,
            // Clear registers
            IpVersion_Clr           = 0x2000,
            CrystalConfig_Clr       = 0x2010,
            CrystalControl_Clr      = 0x2018,
            Config_Clr              = 0x2020,
            Control_Clr             = 0x2028,
            Command_Clr             = 0x2050,
            Status_Clr              = 0x2058,
            InterruptFlags_Clr      = 0x2070,
            InterruptEnable_Clr     = 0x2074,
            Lock_Clr                = 0x2080,
            // Toggle registers
            IpVersion_Tgl           = 0x3000,
            CrystalConfig_Tgl       = 0x3010,
            CrystalControl_Tgl      = 0x3018,
            Config_Tgl              = 0x3020,
            Control_Tgl             = 0x3028,
            Command_Tgl             = 0x3050,
            Status_Tgl              = 0x3058,
            InterruptFlags_Tgl      = 0x3070,
            InterruptEnable_Tgl     = 0x3074,
            Lock_Tgl                = 0x3080,
        }

        private enum WakeUpSource
        {
            None  = 0,
            Prs   = 1,
            Force = 2,
        }
    }
}