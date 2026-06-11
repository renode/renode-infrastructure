//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

using ICounter = Antmicro.Renode.Peripherals.Timers.ArmCorstone_SystemCounter.ICounter;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ArmCorstone_SystemWatchdog : BasicDoubleWordPeripheral, IKnownSize
    {
        public ArmCorstone_SystemWatchdog(IMachine machine, ArmCorstone_SystemCounter counter) : base(machine)
        {
            counter.Counter.RegisterComparePoint(HanldeLimitReached, this, "watchdog");
            this.counter = counter.Counter;
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
            UpdateInterrupts();
        }

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        public GPIO ResetRequest { get; } = new GPIO();

        public long Size => 0x2000;

        private void HanldeLimitReached()
        {
            if(interrupt0.Value)
            {
                interrupt1.Value = true;
            }
            else
            {
                interrupt0.Value = true;
            }
            CompareValue = counter.Value + offset.Value;
            UpdateInterrupts();
        }

        private void Refresh()
        {
            interrupt0.Value = false;
            interrupt1.Value = false;
            CompareValue = counter.Value + offset.Value;
            UpdateInterrupts();
        }

        private void UpdateComparePoint()
        {
            if(enabled.Value)
            {
                counter.SetComparePoint(HanldeLimitReached, CompareValue);
            }
            else
            {
                counter.DeactivateComparePoint(HanldeLimitReached);
            }
        }

        private void UpdateInterrupts()
        {
            var was = IRQ.IsSet;
            IRQ.Set(interrupt0.Value);
            if(was != interrupt0.Value)
            {
                this.NoisyLog("IRQ: {0}set", was ? "un" : "");
            }

            was = ResetRequest.IsSet;
            ResetRequest.Set(interrupt1.Value);
            if(was != interrupt1.Value)
            {
                this.NoisyLog("ResetRequest: {0}set", was ? "un" : "");
            }
        }

        private void DefineRegisters()
        {
            Registers.WatchdogRefresh.Define(this)
                .WithValueField(0, 32, name: "WRR",
                    valueProviderCallback: _ => 0x0,
                    writeCallback: (_, __) => Refresh()
                )
            ;

            Registers.WatchdogControlAndStatus.Define(this)
                .WithFlag(0, out enabled, name: "Watchdog Enable",
                    changeCallback: (_, __) => UpdateComparePoint()
                )
                .WithFlag(1, out interrupt0, FieldMode.Read, name: "Watchdog Signal Status (WS0 Interrupt)")
                .WithFlag(2, out interrupt1, FieldMode.Read, name: "Watchdog Signal Status (WS1 Interrupt)")
                .WithReservedBits(3, 29)
            ;

            Registers.WatchdogOffset.Define(this)
                .WithValueField(0, 32, out offset, name: "WOR",
                    writeCallback: (_, __) => Refresh()
                )
            ;

            Registers.WatchdogCompareValueLow.Define(this)
                .WithValueField(0, 32, name: "WCV",
                    valueProviderCallback: _ => CompareValueLow,
                    writeCallback: (_, value) => CompareValueLow = (uint)value
                )
            ;

            Registers.WatchdogCompareValueHigh.Define(this)
                .WithValueField(0, 32, name: "WCV",
                    valueProviderCallback: _ => CompareValueHigh,
                    writeCallback: (_, value) => CompareValueHigh = (uint)value
                )
            ;

            RegistersCollection.AddRegister((long)Registers.RefreshWatchdogInterfaceIdentification,
                Registers.WatchdogInterfaceIdentification.Define(this, 0x0000143B)
                    .WithValueField(0, 12, FieldMode.Read, name: "JEPCODE")
                    .WithValueField(12, 4, FieldMode.Read, name: "REV")
                    .WithValueField(16, 4, FieldMode.Read, name: "ARCH")
                    .WithReservedBits(20, 4)
                    .WithValueField(24, 8, FieldMode.Read, name: "ID")
            );

            RegistersCollection.AddRegister((long)Registers.RefreshPeripheralIdentification4,
                Registers.PeripheralIdentification4.Define(this, 0x00000004)
                    .WithReservedBits(0, 32)
            );

            Registers.PeripheralIdentification0.Define(this, 0x000000B1)
                .WithReservedBits(0, 32)
            ;

            Registers.RefreshPeripheralIdentification0.Define(this, 0x000000B0)
                .WithReservedBits(0, 32)
            ;

            RegistersCollection.AddRegister((long)Registers.RefreshPeripheralIdentification1,
                Registers.PeripheralIdentification1.Define(this, 0x000000B0)
                    .WithReservedBits(0, 32)
            );

            RegistersCollection.AddRegister((long)Registers.RefreshPeripheralIdentification2,
                Registers.PeripheralIdentification2.Define(this, 0x0000002B)
                    .WithReservedBits(0, 32)
            );

            RegistersCollection.AddRegister((long)Registers.RefreshPeripheralIdentification3,
                Registers.PeripheralIdentification3.Define(this, 0x00000000)
                    .WithReservedBits(0, 32)
            );

            RegistersCollection.AddRegister((long)Registers.RefreshComponentIdentification0,
                Registers.ComponentIdentification0.Define(this, 0x0000000D)
                    .WithReservedBits(0, 32)
            );

            RegistersCollection.AddRegister((long)Registers.RefreshComponentIdentification1,
                Registers.ComponentIdentification1.Define(this, 0x000000F0)
                    .WithReservedBits(0, 32)
            );

            RegistersCollection.AddRegister((long)Registers.RefreshComponentIdentification2,
                Registers.ComponentIdentification2.Define(this, 0x00000005)
                    .WithReservedBits(0, 32)
            );

            RegistersCollection.AddRegister((long)Registers.RefreshComponentIdentification3,
                Registers.ComponentIdentification3.Define(this, 0x000000B1)
                    .WithReservedBits(0, 32)
            );
        }

        private ulong CompareValue
        {
            get => compareValue;
            set
            {
                if(compareValue == value)
                {
                    return;
                }

                compareValue = value;
                UpdateComparePoint();
            }
        }

        private uint CompareValueLow
        {
            get => (uint)compareValue;
            set => CompareValue = BitHelper.SetBitsFrom(compareValue, value, position: 0, width: 32);
        }

        private uint CompareValueHigh
        {
            get => (uint)(compareValue >> 32);
            set => CompareValue = BitHelper.SetBitsFrom(compareValue, value, position: 32, width: 32);
        }

        private IFlagRegisterField enabled;
        private IFlagRegisterField interrupt0;
        private IFlagRegisterField interrupt1;

        private IValueRegisterField offset;

        private ulong compareValue;
        private readonly ICounter counter;

        public enum Registers
        {
            // Control frame
            WatchdogControlAndStatus        = 0x000, // WCS
            WatchdogOffset                  = 0x008, // WOR
            WatchdogCompareValueLow         = 0x010, // WCV[31:0]
            WatchdogCompareValueHigh        = 0x014, // WCV[63:32]
            WatchdogInterfaceIdentification = 0xFCC, // W_IIDR
            PeripheralIdentification4       = 0xFD0, // PIDR4
            PeripheralIdentification0       = 0xFE0, // PIDR0
            PeripheralIdentification1       = 0xFE4, // PDR1
            PeripheralIdentification2       = 0xFE8, // PIDR2
            PeripheralIdentification3       = 0xFEC, // PIDR3
            ComponentIdentification0        = 0xFF0, // CIDR0
            ComponentIdentification1        = 0xFF4, // CIDR1
            ComponentIdentification2        = 0xFF8, // CIDR2
            ComponentIdentification3        = 0xFFC, // CIDR3

            // Refresh frame
            WatchdogRefresh                        = 0x1000, // WRR
            RefreshWatchdogInterfaceIdentification = 0x1FCC, // W_IIDR
            RefreshPeripheralIdentification4       = 0x1FD0, // PIDR4
            RefreshPeripheralIdentification0       = 0x1FE0, // PIDR0
            RefreshPeripheralIdentification1       = 0x1FE4, // PIDR1
            RefreshPeripheralIdentification2       = 0x1FE8, // PIDR2
            RefreshPeripheralIdentification3       = 0x1FEC, // PIDR3
            RefreshComponentIdentification0        = 0x1FF0, // CIDR0
            RefreshComponentIdentification1        = 0x1FF4, // CIDR1
            RefreshComponentIdentification2        = 0x1FF8, // CIDR2
            RefreshComponentIdentification3        = 0x1FFC, // CIDR3
        }
    }
}
