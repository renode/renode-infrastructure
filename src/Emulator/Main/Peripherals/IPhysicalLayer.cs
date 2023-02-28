//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals
{
    public interface IPhysicalLayer<T> : IPhysicalLayer<T, T>
    {
    }

    public interface IPhysicalLayer<T, V> : IPhysicalLayer
    {
        V Read(T addr);
        void Write(T addr, V val);
    }

    public interface IPhysicalLayer: IPeripheral
    {
    }
}

