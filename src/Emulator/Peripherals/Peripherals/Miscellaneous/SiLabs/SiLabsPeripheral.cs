//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public abstract class SiLabsPeripheral : IDoubleWordPeripheral
    {
        public SiLabsPeripheral(Machine machine, bool buildRegisters = true)
        {
            this.machine = machine;

            if(buildRegisters)
            {
                registersCollection = BuildRegistersCollection();
            }
        }

        public virtual byte ReadByte(long offset)
        {
            int byteOffset = (int)(offset & 0x3);
            uint registerValue = ReadRegister(offset, true);
            byte result = (byte)((registerValue >> byteOffset*8) & 0xFF);
            return result;
        }

        public virtual uint ReadDoubleWord(long offset)
        {
            return ReadRegister(offset);
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            WriteRegister(offset, value);
        }

        public virtual void Reset()
        {
            registersCollection.Reset();
            UpdateInterrupts();
        }

        protected TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;

        protected bool TrySyncTime()
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
                return true;
            }
            return false;
        }

        protected virtual void UpdateInterrupts()
        {
        }

        protected uint ReadRegister(long offset, bool internal_read = false)
        {
            var result = 0U;
            long internal_offset = offset;

            // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
            if(offset >= SetRegisterOffset && offset < ClearRegisterOffset)
            {
                // Set register
                internal_offset = offset - SetRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.ToObject(RegistersType, internal_offset), offset, internal_offset);
                }
            }
            else if(offset >= ClearRegisterOffset && offset < ToggleRegisterOffset)
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.ToObject(RegistersType, internal_offset), offset, internal_offset);
                }
            }
            else if(offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.ToObject(RegistersType, internal_offset), offset, internal_offset);
                }
            }

            try
            {
                if(registersCollection.TryRead(internal_offset, out result))
                {
                    return result;
                }
            }
            finally
            {
                if(!internal_read)
                {
                    this.Log(LogLevel.Debug, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", internal_offset, Enum.ToObject(RegistersType, internal_offset), result);
                }
            }

            if(!internal_read)
            {
                this.Log(LogLevel.Warning, "Unhandled read at offset 0x{0:X} ({1}).", internal_offset, Enum.ToObject(RegistersType, internal_offset));
            }

            return 0;
        }

        protected void WriteRegister(long offset, uint value, bool internal_write = false)
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                long internal_offset = offset;
                uint internal_value = value;

                if(offset >= SetRegisterOffset && offset < ClearRegisterOffset)
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value | value;
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.ToObject(RegistersType, internal_offset), offset, internal_offset, value, old_value, internal_value);
                }
                else if(offset >= ClearRegisterOffset && offset < ToggleRegisterOffset)
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value & ~value;
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.ToObject(RegistersType, internal_offset), offset, internal_offset, value, old_value, internal_value);
                }
                else if(offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = ReadRegister(internal_offset, true);
                    internal_value = old_value ^ value;
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.ToObject(RegistersType, internal_offset), offset, internal_offset, value, old_value, internal_value);
                }

                this.Log(LogLevel.Debug, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, Enum.ToObject(RegistersType, internal_offset), internal_value);

                if(!registersCollection.TryWrite(internal_offset, internal_value))
                {
                    this.Log(LogLevel.Warning, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", internal_offset, Enum.ToObject(RegistersType, internal_offset), internal_value);
                    return;
                }
            });
        }

        // Abstract methods to be implemented
        protected abstract DoubleWordRegisterCollection BuildRegistersCollection();

        protected abstract Type RegistersType { get; }

        protected DoubleWordRegisterCollection registersCollection;

        protected readonly Machine machine;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
    }
}