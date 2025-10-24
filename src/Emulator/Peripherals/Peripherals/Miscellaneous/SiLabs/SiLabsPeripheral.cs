//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public abstract class SiLabsPeripheral : IDoubleWordPeripheral, IKnownSize, IAbsoluteAddressAware
    {
        public SiLabsPeripheral(Machine machine, bool buildRegisters = true)
        {
            this.machine = machine;
            if(buildRegisters)
            {
                registersCollection = BuildRegistersCollection();
            }
        }

        public void SetAbsoluteAddress(ulong address)
        {
            currentAbsoluteAddress = address;
        }

        public byte ReadByte(long offset)
        {
            int byteOffset = (int)(offset & 0x3);
            uint registerValue = ReadDoubleWord(offset - byteOffset);
            byte result = (byte)((registerValue >> byteOffset*8) & 0xFF);
            return result;
        }

        public uint ReadDoubleWord(long offset)
        {
            if(!VerifyPeripheralAccess(isWrite: false, offset))
            {
                return 0;
            }

            long internalOffset = offset & 0x0FFF;
            switch(offset & OffsetMask)
            {
            case 0x0000:
                return registersCollection.Read(internalOffset);
            default:
                this.Log(LogLevel.Warning, "Reading from Set/Clr/Tgl is not supported");
                return registersCollection.Read(internalOffset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                if(!VerifyPeripheralAccess(isWrite: true, offset))
                {
                    return;
                }

                long internalOffset = offset & 0x0FFF;
                switch(offset & OffsetMask)
                {
                case 0x0000:
                    registersCollection.Write(internalOffset, value);
                    break;
                case SetRegisterOffset:
                    registersCollection.Write(internalOffset, registersCollection.Read(internalOffset) | value);
                    break;
                case ClearRegisterOffset:
                    registersCollection.Write(internalOffset, registersCollection.Read(internalOffset) & ~value);
                    break;
                case ToggleRegisterOffset:
                    registersCollection.Write(internalOffset, registersCollection.Read(internalOffset) ^ value);
                    break;
                default:
                    this.Log(LogLevel.Error, "writing doubleWord to non existing offset 0x{0:X}", offset);
                    break;
                }
            });
        }

        public virtual void Reset()
        {
            registersCollection.Reset();
            UpdateInterrupts();
        }

        /// <summary>
        /// Indicates whether the peripheral's enable register is set.
        /// Derived classes should define this when the peripheral has an EN register.
        /// </summary>
        public virtual bool Enabled { get; set; }

        public bool IsSecureAccess => (currentAbsoluteAddress & 0x10000000) != 0;

        public virtual long Size => 0x4000;

        protected void WriteWSTATIC()
        {
            if(Enabled)
            {
                this.Log(LogLevel.Error, "Write to a WSTATIC register ({0}) while peripheral is enabled; ignored on hardware", Enum.ToObject(RegistersType, currentAbsoluteAddress & 0x3FFF));
            }
        }

        protected void WriteWSYNC()
        {
            if(!Enabled)
            {
                this.Log(LogLevel.Error, "Trying to write to a WSYNC register ({0}) while peripheral is disabled", Enum.ToObject(RegistersType, currentAbsoluteAddress & 0x3FFF));
                TriggerBusFault(currentAbsoluteAddress);
            }
        }

        protected void WriteRWSYNC()
        {
            if(!Enabled)
            {
                this.Log(LogLevel.Error, "Trying to write to a RWSYNC register ({0}) while peripheral is disabled", Enum.ToObject(RegistersType, currentAbsoluteAddress & 0x3FFF));
                TriggerBusFault(currentAbsoluteAddress);
            }
        }

        protected uint ReadWFIFO()
        {
            this.Log(LogLevel.Warning, "Reading from a WFIFO Field, value returned will always be 0");
            return 0x0;
        }

        protected uint ReadLFWSYNC()
        {
            this.Log(LogLevel.Warning, "Reading from a LFWSYNC/HVLFWSYNC Field, value returned will always be 0");
            return 0x0;
        }

        protected uint ReadRFIFO()
        {
            this.Log(LogLevel.Warning, "Reading from a RFIFO Field, value returned will always be 0");
            return 0x0;
        }

        internal bool VerifyPeripheralAccess(bool isWrite, long offset)
        {
            if(Cmu != null && Cmu.GetPeripheralClockEnable(this) == false)
            {
                this.Log(LogLevel.Error, "Accessing a peripheral while peripheral clock is disabled");
                TriggerBusFault(currentAbsoluteAddress);
                return false;
            }
            if(CurrentCpu is CortexM cortexM && cortexM.TrustZoneEnabled && Smu != null && Smu.GetPeripheralSecurityLevel(this) != IsSecureAccess)
            {
                this.Log(LogLevel.Error, "Accessing a {0} peripheral while peripheral security level is {1}",
                    IsSecureAccess ? "secure" : "non-secure",
                    IsSecureAccess ? "non-secure" : "secure");
                TriggerBusFault(currentAbsoluteAddress);
                return false;
            }
            if(!VerifyPapAccess(isWrite, offset))
            {
                return false;
            }
            Smu?.UpdateDirtyState(this);
            return true;
        }

        /// <summary>
        /// Optional PAP filter. Return false to reject the access:
        /// reads RAZ to 0, writes are dropped. Subclasses that reject a write
        /// should report the IMU VIOCON consequence themselves.
        /// </summary>
        protected virtual bool VerifyPapAccess(bool isWrite, long offset)
        {
            return true;
        }

        protected TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;

        protected bool TrySyncTime()
        {
            if(CurrentCpu != null)
            {
                CurrentCpu.SyncTime();
                return true;
            }
            return false;
        }

        protected virtual void UpdateInterrupts()
        {
        }

        /// <summary>
        /// Triggers a precise bus fault on the currently-executing CortexM CPU.
        /// </summary>
        protected void TriggerBusFault(ulong address)
        {
            if(CurrentCpu is CortexM)
            {
                throw new BusAccessException(BusAccessError.GenericError);
            }
            this.Log(LogLevel.Error, "TriggerBusFault: no active CortexM CPU; precise bus fault at 0x{0:X} not delivered", address);
        }

        protected abstract DoubleWordRegisterCollection BuildRegistersCollection();

        protected ulong HfxoFrequency => Cmu.HfxoFrequency;

        protected ISiLabs_CMU Cmu
        {
            get
            {
                if(Object.ReferenceEquals(cmu, null))
                {
                    foreach(var peripheral in machine.GetPeripheralsOfType<ISiLabs_CMU>())
                    {
                        cmu = peripheral;
                        break;
                    }
                }
                return cmu;
            }

            set
            {
                cmu = value;
            }
        }

        protected ISiLabs_Emu Emu
        {
            get
            {
                if(Object.ReferenceEquals(emu, null))
                {
                    foreach(var peripheral in machine.GetPeripheralsOfType<ISiLabs_Emu>())
                    {
                        emu = peripheral;
                        break;
                    }
                }
                return emu;
            }

            set
            {
                emu = value;
            }
        }

        protected ISiLabs_SMU Smu
        {
            get
            {
                if(Object.ReferenceEquals(smu, null))
                {
                    foreach(var peripheral in machine.GetPeripheralsOfType<ISiLabs_SMU>())
                    {
                        smu = peripheral;
                        break;
                    }
                }
                return smu;
            }

            set
            {
                smu = value;
            }
        }

        protected ICPU CurrentCpu
        {
            get
            {
                // Monitor and test writes have no executing CPU; GetCurrentCPU() throws in that case.
                if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    return cpu;
                }
                return null;
            }
        }

        protected abstract Type RegistersType { get; }

        protected DoubleWordRegisterCollection registersCollection;

        protected readonly Machine machine;

        private ISiLabs_CMU cmu;

        private ISiLabs_Emu emu;

        private ISiLabs_SMU smu;

        private ulong currentAbsoluteAddress;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private const uint OffsetMask = 0x3000;
    }

    /// <summary>
    /// Optional: implement this to report a custom retention state. If not implemented, the entire peripheral is treated as not retained.
    /// </summary>
    public interface ISiLabs_Retention
    {
        bool Enabled { get; }

        void PowerDown();
    }

    /// <summary>
    /// Optional: implement this to report registers to power down. If not implemented, the entire peripheral is treated as not retained.
    /// </summary>
    public interface ISiLabs_PartialRetention
    {
        List<DoubleWordRegister> GetPowerDownRegisters();
    }

    /// <summary>
    /// Optional, opt-in hook for partial-retention peripherals that carry C# state outside their
    /// DoubleWordRegister collection (e.g. per-channel objects). EMU invokes OnPartialPowerDown()
    /// once per non-retained peripheral, just before resetting the registers returned by
    /// GetPowerDownRegisters(). Peripherals whose entire non-retained state already lives in
    /// DoubleWordRegister instances should NOT implement this interface.
    /// </summary>
    public interface ISiLabs_PartialPowerDownHook
    {
        void OnPartialPowerDown();
    }
}
