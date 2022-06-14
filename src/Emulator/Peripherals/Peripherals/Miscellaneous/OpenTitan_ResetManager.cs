//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // OpenTitan ResetManager as per https://docs.opentitan.org/hw/ip/rstmgr/doc/ (sha: f4e3845)
    public class OpenTitan_ResetManager : BasicDoubleWordPeripheral, IGPIOReceiver, IKnownSize
    {
        public OpenTitan_ResetManager(Machine machine, ulong resetPC) : base(machine)
        {
            DefineRegisters();
            this.resetPC = resetPC;
            skippedOnLifeCycleReset = new HashSet<IPeripheral>();
            skippedOnSystemReset = new HashSet<IPeripheral>();
            modules = new IPeripheral[numberOfModules];

            // Outputs
            LifeCycleState = new GPIO();    // Current state of rst_lc_n tree.
            SystemState = new GPIO();       // Current state of rst_sys_n tree.
            Resets = new GPIO();            // Resets used by the rest of the core domain.
            // AstResets                    // Resets used by ast.
        }

        public void MarkAsSkippedOnLifeCycleReset(IPeripheral peripheral)
        {
            if(!skippedOnLifeCycleReset.Contains(peripheral))
            {
                skippedOnLifeCycleReset.Add(peripheral);
            }
        }

        public void MarkAsSkippedOnSystemReset(IPeripheral peripheral)
        {
            if(!skippedOnSystemReset.Contains(peripheral))
            {
                skippedOnSystemReset.Add(peripheral);
            }
        }

        public void LifeCycleReset()
        {
            ExecuteResetWithSkipped(skippedOnLifeCycleReset);
        }

        public void RegisterModuleSpecificReset(IPeripheral peripheral, uint id)
        {
            if(id >= modules.Length)
            {
                throw new RecoverableException($"Id has to be less than {modules.Length}.");
            }
            if(modules[id] != null)
            {
                throw new RecoverableException($"Module with id {id} already registered.");
            }
            modules[id] = peripheral;
        }

        public void OnGPIO(int number, bool value)
        {
            var signal = (GPIOInput)number;
            var ignored = false;
            switch(signal)
            {
                case GPIOInput.PowerOnReset:
                    if(value)
                    {
                        ExecuteResetWithSkipped(null);
                    }
                    break;
                case GPIOInput.CPUReset:
                case GPIOInput.NonDebugModeReset:
                    if(value)
                    {
                        nonDebugModuleResetFlag.Value = true;
                        SystemReset();
                    }
                    break;
                case GPIOInput.CPUDump:
                    ignored = true;
                    break;
                case GPIOInput.LifeCycleReset:
                    if(value)
                    {
                        LifeCycleReset();
                    }
                    break;
                case GPIOInput.SystemReset:
                    if(value)
                    {
                        SystemReset();
                    }
                    break;
                case GPIOInput.ResetCause:
                case GPIOInput.PeripheralReset:
                    ignored = true;
                    break;
                default:
                    this.Log(LogLevel.Error, "Received GPIO signal on an unsupported port #{0}.", number);
                    break;
            }
            if(ignored)
            {
                this.Log(LogLevel.Warning, "{0} signal ignored. Function not supported.", signal);
            }
        }

        public long Size => 0x1000;

        public GPIO LifeCycleState { get; }
        public GPIO SystemState { get; }
        public GPIO Resets { get; }

        private void ExecuteResetWithSkipped(ICollection<IPeripheral> toSkip)
        {
            // This method is intended to run only as a result of memory access from translated code.
            if(!machine.SystemBus.TryGetCurrentCPU(out var icpu))
            {
                this.Log(LogLevel.Error, "Couldn't find the cpu requesting reset.");
                return;
            }
            var cpu = icpu as TranslationCPU;
            if(cpu == null)
            {
                this.Log(LogLevel.Error, "Resetting for non TranslationCPU based CPUs is not implemented.");
                return;
            }
            this.Log(LogLevel.Info, "Software reset started.");

            if(!cpu.RequestTranslationBlockRestart())
            {
                this.Log(LogLevel.Error, "Software reset failed.");
                return;
            }

            machine.RequestResetInSafeState(() =>
            {
                cpu.PC = resetPC;
                this.Log(LogLevel.Info, "Software reset complete.");
            }, unresetable: toSkip);
        }

        private void SystemReset()
        {
            ExecuteResetWithSkipped(skippedOnSystemReset);
        }

        private void SoftwareRequestedReset()
        {
            LifeCycleReset();
            softwareResetFlag.Value = true;
        }

        private void SetResetHolds(uint value)
        {
            for(byte i = 0; i < softwareControllableResetsWriteEnableMask.Width; ++i)
            {
                if(!BitHelper.IsBitSet(value, i))
                {
                    continue;
                }
                if(BitHelper.IsBitSet(softwareControllableResetsWriteEnableMask.Value, i))
                {
                    modules[i].Reset();
                }
                else
                {
                    this.Log(LogLevel.Warning, "Trying to use disabled software controllable reset VAL_{0}.", i);
                }
            }
        }

        private void DefineRegisters()
        {
            Registers.AlertTest.Define(this, 0x0)
                .WithTaggedFlag("fatal_fault", 0)
                .WithTaggedFlag("fatal_cnsty_fault", 1)
                .WithReservedBits(2, 30);
            Registers.ResetRequested.Define(this, 0x5)
                .WithValueField(0, 4, out resetRequest, name: "VAL")
                .WithReservedBits(4, 28)
                .WithChangeCallback((_, __) =>
                    {
                        if(resetRequest.Value == KMULTIBITBOOL4TRUE)
                        {
                            resetRequest.Value = 0;
                            SoftwareRequestedReset();
                        }
                    });
            Registers.DeviceResetReason.Define(this, 0x1)
                .WithFlag(0, out powerOnResetFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "POR")
                .WithTaggedFlag("LOW_POWER_EXIT", 1)
                .WithFlag(2, out nonDebugModuleResetFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "NDM_RESET")
                .WithFlag(3, out softwareResetFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "SW_RESET")
                .WithTag("HW_REQ", 4, 4)
                .WithReservedBits(8, 24);
            Registers.AlertWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("EN", 0);
            Registers.AlertInfoControls.Define(this, 0x0)
                .WithTaggedFlag("EN", 0)
                .WithReservedBits(1, 3)
                .WithTag("INDEX", 4, 4)
                .WithReservedBits(8, 24);
            Registers.AlertInfoAttributes.Define(this, 0x0)
                .WithTag("CNT_AVAIL", 0, 4)
                .WithReservedBits(4, 24);
            Registers.AlertDumpInfo.Define(this, 0x0)
                .WithTag("VALUE", 0, 32);
            Registers.CpuWriteEnable.Define(this, 0x0)
                .WithTaggedFlag("EN", 0)
                .WithReservedBits(1, 31);
            Registers.CpuInfoControls.Define(this, 0x0)
                .WithTaggedFlag("EN", 0)
                .WithReservedBits(1, 3)
                .WithTag("INDEX", 4, 4)
                .WithReservedBits(8, 24);
            Registers.CpuInfoAttributes.Define(this, 0x0)
                .WithTag("CNT_AVAIL", 0, 4)
                .WithReservedBits(4, 28);
            Registers.CpuDumpInformation.Define(this, 0x0)
                .WithTag("VALUE", 0, 32);
            Registers.SoftwareControllableResetsWriteEnable.Define(this, 0xff)
                .WithValueField(0, 8, out softwareControllableResetsWriteEnableMask, FieldMode.Read | FieldMode.WriteZeroToClear, name: "EN")
                .WithReservedBits(8, 24);
            Registers.SoftwareControllableResets.Define(this, 0xff)
                .WithValueField(0, 8, writeCallback: (_, val) => { SetResetHolds(val); }, name: "VAL")
                .WithReservedBits(8, 24);
            Registers.ErrorCode.Define(this)
                .WithTaggedFlag("REG_INTG_ERR", 0)
                .WithTaggedFlag("RESET_CONSISTENCY_ERR", 1)
                .WithReservedBits(2, 30);
        }

        private IValueRegisterField resetRequest;
        private IFlagRegisterField powerOnResetFlag;
        private IFlagRegisterField nonDebugModuleResetFlag;
        private IFlagRegisterField softwareResetFlag;
        private IValueRegisterField softwareControllableResetsWriteEnableMask;
        private readonly ulong resetPC;
        private readonly HashSet<IPeripheral> skippedOnLifeCycleReset;
        private readonly HashSet<IPeripheral> skippedOnSystemReset;
        private readonly IPeripheral[] modules;

        private const int numberOfModules = 8;
        private const byte KMULTIBITBOOL4TRUE = 0xA;

        public enum GPIOInput
        {
            PowerOnReset,       // Input from ast. This signal is the root reset of the design and is used to generate rst_por_n.
            CPUReset,           // CPU reset indication. This informs the reset manager that the processor has reset.
            NonDebugModeReset,  // Non-debug-module reset request from rv_dm.
            CPUDump,            // CPU crash dump state from rv_core_ibex.
            LifeCycleReset,     // Power manager request to assert the rst_lc_n tree.
            SystemReset,        // Power manager request to assert the rst_sys_n tree.
            ResetCause,         // Power manager indication for why it requested reset, the cause can be low power entry or peripheral issued request.
            PeripheralReset,    // Peripheral reset requests.
        }

        private enum Registers
        {
            AlertTest = 0x0,                              // Alert Test Register
            ResetRequested = 0x4,                         // Software requested system reset.
            DeviceResetReason = 0x8,                      // Device reset reason.
            AlertWriteEnable = 0xc,                       // Alert write enable
            AlertInfoControls = 0x10,                     // Alert info dump controls.
            AlertInfoAttributes = 0x14,                   // Alert info dump attributes.
            AlertDumpInfo = 0x18,                         // Alert dump information prior to last reset. Which value read is controlled by the ALERT_INFO_CTRL register.
            CpuWriteEnable = 0x1c,                        // Cpu write enable
            CpuInfoControls = 0x20,                       // Cpu info dump controls.
            CpuInfoAttributes = 0x24,                     // Cpu info dump attributes.
            CpuDumpInformation = 0x28,                    // Cpu dump information prior to last reset. Which value read is controlled by the CPU_INFO_CTRL register.
            SoftwareControllableResetsWriteEnable = 0x2c, // Register write enable for software controllable resets. When a particular bit value is 0, the corresponding value in SW_RST_CTRL_N can no longer be changed. When a particular bit value is 1, the corresponding value in SW_RST_CTRL_N can be changed.
            SoftwareControllableResets = 0x30,            // Software controllable resets. When a particular bit value is 0, the corresponding module is held in reset. When a particular bit value is 1, the corresponding module is not held in reset.
            ErrorCode = 0x34,                             // A bit vector of all the errors that have occurred in reset manager
        }
    }
}
