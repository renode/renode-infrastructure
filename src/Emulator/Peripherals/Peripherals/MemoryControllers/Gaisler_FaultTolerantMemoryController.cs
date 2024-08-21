//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.MTD;
using static Antmicro.Renode.Peripherals.Bus.GaislerAPBPlugAndPlayRecord;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public class Gaisler_FaultTolerantMemoryController : BasicDoubleWordPeripheral, IKnownSize, IGaislerAPB,
        IPeripheralContainer<MappedMemory, NullRegistrationPoint>, IPeripheralContainer<AMDCFIFlash, NullRegistrationPoint>
    {
        public Gaisler_FaultTolerantMemoryController(IMachine machine) : base(machine)
        {
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            SetPromWriteEnable(false);
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(MappedMemory peripheral)
        {
            return prom != null ?
                new [] { NullRegistrationPoint.Instance } :
                Enumerable.Empty<NullRegistrationPoint>();
        }

        public void Register(MappedMemory peripheral, NullRegistrationPoint registrationPoint)
        {
            if(prom != null)
            {
                throw new RegistrationException("PROM MappedMemory already registered");
            }

            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            prom = peripheral;
            promAddress = sysbus.GetRegistrationPoints(prom).Single().Range.StartAddress;
        }

        public void Unregister(MappedMemory peripheral)
        {
            prom = null;
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(AMDCFIFlash peripheral)
        {
            return flash != null ?
                new [] { NullRegistrationPoint.Instance } :
                Enumerable.Empty<NullRegistrationPoint>();
        }

        public void Register(AMDCFIFlash peripheral, NullRegistrationPoint registrationPoint)
        {
            if(flash != null)
            {
                throw new RegistrationException("Flash already registered");
            }

            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            flash = peripheral;
        }

        public void Unregister(AMDCFIFlash peripheral)
        {
            flash = null;
        }

        public long Size => 0x100;

        IEnumerable<IRegistered<MappedMemory, NullRegistrationPoint>> IPeripheralContainer<MappedMemory, NullRegistrationPoint>.Children
        {
            get => prom != null ?
                new [] { Registered.Create(prom, NullRegistrationPoint.Instance) } :
                Enumerable.Empty<IRegistered<MappedMemory, NullRegistrationPoint>>();
        }

        IEnumerable<IRegistered<AMDCFIFlash, NullRegistrationPoint>> IPeripheralContainer<AMDCFIFlash, NullRegistrationPoint>.Children
        {
            get => flash != null ?
                new [] { Registered.Create(flash, NullRegistrationPoint.Instance) } :
                Enumerable.Empty<IRegistered<AMDCFIFlash, NullRegistrationPoint>>();
        }

        private void DefineRegisters()
        {
            Registers.MemoryConfiguration1.Define(this)
                .WithTag("promReadWaitStates", 0, 4)
                .WithTag("promWriteWaitStates", 4, 4)
                .WithTag("promWidth", 8, 2)
                .WithReservedBits(10, 1)
                .WithFlag(11, name: "promWriteEnable", changeCallback: (_, value) => SetPromWriteEnable(value))
                .WithReservedBits(12, 2)
                .WithTag("promBankSize", 14, 3)
                .WithReservedBits(18, 1)
                .WithTaggedFlag("ioEnable", 19)
                .WithTag("ioWaitStates", 20, 4)
                .WithReservedBits(24, 1)
                .WithTaggedFlag("busErrorCode", 25)
                .WithTaggedFlag("ioBusReadyEnable", 26)
                .WithTag("ioBusWidth", 27, 2)
                .WithTaggedFlag("asynchronousBusReady", 29)
                .WithTaggedFlag("promAreaBusReady", 30)
                .WithReservedBits(31, 1);

            Registers.MemoryConfiguration2.Define(this)
                .WithTag("ramReadWaitStates", 0, 2)
                .WithTag("ramWriteWaitStates", 2, 2)
                .WithTag("ramWidth", 4, 2)
                .WithTaggedFlag("readModifyWrite", 6)
                .WithReservedBits(7, 2)
                .WithTag("ramBankSize", 9, 4)
                .WithTaggedFlag("sramDisable", 13)
                .WithTaggedFlag("sdramEnable", 14)
                .WithReservedBits(15, 4)
                .WithTag("sdramCommand", 19, 2)
                .WithTag("sdramColumnSize", 21, 2)
                .WithTag("sdramBankSize", 23, 3)
                .WithTaggedFlag("sdramTcas", 26)
                .WithTag("sdramTrfc", 27, 3)
                .WithTaggedFlag("sdramTrp", 30)
                .WithTaggedFlag("sdramRefreshEnable", 31);

            Registers.MemoryConfiguration3.Define(this)
                .WithTag("testCheckbits", 0, 8)
                .WithTaggedFlag("promEdacEnable", 8)
                .WithTaggedFlag("ramEdacEnable", 9)
                .WithTaggedFlag("edacDiagnosticReadBypass", 10)
                .WithTaggedFlag("edacDiagnosticWriteBypass", 11)
                .WithTag("sdramRefreshCounterReload", 12, 15)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("reedSolomonEdacEnable", 28)
                .WithReservedBits(29, 3);

            Registers.MemoryConfiguration4.Define(this)
                .WithTag("testCheckbits", 0, 16)
                .WithTaggedFlag("edacDiagnosticWriteBypass", 16)
                .WithReservedBits(17, 15);
        }

        private void SetPromWriteEnable(bool enable)
        {
            if(isWriteEnabled == enable)
            {
                return;
            }
            if(prom == null || flash == null)
            {
                this.ErrorLog("Attempted to set PROM write enable to {0} without a {1} (PROM) or {2} (flash) registered",
                    enable, nameof(MappedMemory), nameof(AMDCFIFlash));
                return;
            }
            this.DebugLog("Set PROM Write enable to {0}", enable);
            if(enable)
            {
                sysbus.Unregister(prom);
                sysbus.Register(flash, new BusPointRegistration(promAddress.Value));
            }
            else
            {
                sysbus.Unregister(flash);
                sysbus.Register(prom, new BusPointRegistration(promAddress.Value));
            }
            isWriteEnabled = enable;
        }

        public uint GetVendorID() => VendorID;

        public uint GetDeviceID() => DeviceID;

        public uint GetInterruptNumber() => 0;

        public SpaceType GetSpaceType() => SpaceType.APBIOSpace;

        private ulong? promAddress;
        private bool isWriteEnabled;
        private MappedMemory prom;
        private AMDCFIFlash flash;

        private const uint VendorID = 0x01;  // Frontgrade Gaisler
        private const uint DeviceID = 0x054; // FTMCTRL

        private enum Registers : uint
        {
            MemoryConfiguration1 = 0x0,
            MemoryConfiguration2 = 0x4,
            MemoryConfiguration3 = 0x8,
            MemoryConfiguration4 = 0xc,
        }
    }
}
