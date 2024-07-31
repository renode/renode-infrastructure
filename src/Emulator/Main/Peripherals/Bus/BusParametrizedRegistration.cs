//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Bus
{
    public abstract class BusParametrizedRegistration : BusRangeRegistration
    {
        public BusParametrizedRegistration(ulong address, ulong size, ICPU cpu, ICluster<ICPU> cluster = null)
            : base(address, size, 0, cpu, cluster)
        {
        }

        internal virtual void FillAccessMethods(IBusPeripheral peripheral, ref PeripheralAccessMethods methods)
        {
            methods.Peripheral = peripheral;
            var readQuadWord = GetReadQuadWordMethod(peripheral);
            if(readQuadWord != null)
            {
                methods.ReadQuadWord = new BusAccess.QuadWordReadMethod(readQuadWord);
            }
            var writeQuadWord = GetWriteQuadWordMethod(peripheral);
            if(writeQuadWord != null)
            {
                methods.WriteQuadWord = new BusAccess.QuadWordWriteMethod(writeQuadWord);
            }
            var readDoubleWord = GetReadDoubleWordMethod(peripheral);
            if(readDoubleWord != null)
            {
                methods.ReadDoubleWord = new BusAccess.DoubleWordReadMethod(readDoubleWord);
            }
            var writeDoubleWord = GetWriteDoubleWordMethod(peripheral);
            if(writeDoubleWord != null)
            {
                methods.WriteDoubleWord = new BusAccess.DoubleWordWriteMethod(writeDoubleWord);
            }
            var readWord = GetReadWordMethod(peripheral);
            if(readWord != null)
            {
                methods.ReadWord = new BusAccess.WordReadMethod(readWord);
            }
            var writeWord = GetWriteWordMethod(peripheral);
            if(writeWord != null)
            {
                methods.WriteWord = new BusAccess.WordWriteMethod(writeWord);
            }
            var readByte = GetReadByteMethod(peripheral);
            if(readByte != null)
            {
                methods.ReadByte = new BusAccess.ByteReadMethod(readByte);
            }
            var writeByte = GetWriteByteMethod(peripheral);
            if(writeByte != null)
            {
                methods.WriteByte = new BusAccess.ByteWriteMethod(writeByte);
            }
        }

        // Those functions return Func and Action instead of BusAccess.*Method, because the latter has `internal` visibility.
        // That's also the reason why `FillAccessMethods` is internal - its visibility can only be as high as
        // visibility of `PeripheralAccessMethods`, which is also marked `internal`.
        public virtual Func<long, ulong> GetReadQuadWordMethod(IBusPeripheral _) => null;
        public virtual Action<long, ulong> GetWriteQuadWordMethod(IBusPeripheral _) => null;
        public virtual Func<long, uint> GetReadDoubleWordMethod(IBusPeripheral _) => null;
        public virtual Action<long, uint> GetWriteDoubleWordMethod(IBusPeripheral _) => null;
        public virtual Func<long, ushort> GetReadWordMethod(IBusPeripheral _) => null;
        public virtual Action<long, ushort> GetWriteWordMethod(IBusPeripheral _) => null;
        public virtual Func<long, byte> GetReadByteMethod(IBusPeripheral _) => null;
        public virtual Action<long, byte> GetWriteByteMethod(IBusPeripheral _) => null;

        public virtual void RegisterForEachContext(Action<BusParametrizedRegistration> register) { }
    }
}
