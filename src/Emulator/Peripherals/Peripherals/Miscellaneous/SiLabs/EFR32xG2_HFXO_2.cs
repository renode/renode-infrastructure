//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    // Allows for the viewing of register contents when debugging
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class EFR32xG2_HFXO_2 : IHFXO_EFR32xG2, IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_HFXO_2(Machine machine, uint startupDelayTicks)
        {
            this.machine = machine;
            this.delayTicks = startupDelayTicks;

            timer = new LimitTimer(machine.ClockSource, 32768, this, "hfxodelay", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += OnStartUpTimerExpired;

            IRQ = new GPIO();

            registersCollection = BuildRegistersCollection();
        }

        public void Reset()
        {
            timer.Enabled = false;
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadRegister(offset);
        }

        private uint ReadRegister(long offset, bool internal_read = false)
        {
            var result = 0U;
            long internal_offset = offset;

            // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
            if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
            {
                // Set register
                internal_offset = offset - SetRegisterOffset;
                if(!internal_read)
                {  
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            } else if (offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", (Registers)internal_offset, offset, internal_offset);
                }
            }

            if(!registersCollection.TryRead(internal_offset, out result))
            {
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "Unhandled read at offset 0x{0:X} ({1}).", internal_offset, (Registers)internal_offset);
                }
            }
            else
            {
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", internal_offset, (Registers)internal_offset, result);
                }
            }

            return result;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteRegister(offset, value);
        }

        private void WriteRegister(long offset, uint value, bool internal_write = false)
        {
            machine.ClockSource.ExecuteInLock(delegate {
                long internal_offset = offset;
                uint internal_value = value;

                if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value | value;
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value & ~value;
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                } else if (offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value ^ value;
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", (Registers)internal_offset, offset, internal_offset, value, old_value, internal_value);
                }

                this.Log(LogLevel.Noisy, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);

                if(!registersCollection.TryWrite(internal_offset, internal_value))
                {
                    this.Log(LogLevel.Noisy, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, (Registers)internal_offset, internal_value);
                    return;
                }
            });
        }

        private DoubleWordRegisterCollection BuildRegistersCollection()
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

#region methods
        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;
        private void StartDelayTimer()
        {
            // Function which starts the start-up delay timer
            timer.Enabled = false;
            timer.Limit = delayTicks;
            timer.Enabled = true;
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

        private void OnStartUpTimerExpired()
        {
            this.Log(LogLevel.Debug, "Start-up delay timer expired at: {0}", machine.ElapsedVirtualTime);
            this.Log(LogLevel.Debug, "Wakeup Requester = {0}", wakeUpSource);

            if (wakeUpSource == WakeUpSource.Force)
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

        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                var irq = (readyInterruptEnable.Value && readyInterrupt.Value);
                IRQ.Set(irq);
            });
        }
#endregion

        public long Size => 0x4000;
        private readonly Machine machine;
        public GPIO IRQ { get; }
        private readonly DoubleWordRegisterCollection registersCollection;
        public event Action HfxoEnabled;
        private WakeUpSource wakeUpSource = WakeUpSource.None;
        private LimitTimer timer;
        private uint delayTicks;
        private const uint UnlockCode = 0x580E;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
#region register fields
        private IFlagRegisterField ready;
        private IFlagRegisterField coreBiasReady;
        private IFlagRegisterField enabled;
        private IFlagRegisterField locked;
        private IFlagRegisterField fsmLock;
        private IFlagRegisterField forceEnable;
        private IFlagRegisterField disableOnDemand;
        // Interrupts
        private IFlagRegisterField readyInterrupt;
        private IFlagRegisterField readyInterruptEnable;
#endregion
 
#region enums
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
#endregion
    }
}
