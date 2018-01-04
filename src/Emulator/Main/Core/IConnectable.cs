//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Core
{
    public interface IConnectable<T> : IConnectable
    {
        void AttachTo(T obj);
        void DetachFrom(T obj);
    }

    public interface IConnectable
    {
    }
}

