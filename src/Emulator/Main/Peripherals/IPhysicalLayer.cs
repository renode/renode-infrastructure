//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals
{
    public interface IPhysicalLayer<T> : IPeripheral
    {
        T Read(T addr);
        void Write(T addr, T val);
    }
}

