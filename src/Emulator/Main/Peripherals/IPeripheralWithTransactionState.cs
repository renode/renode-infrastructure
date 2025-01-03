//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals
{
    public interface IPeripheralWithTransactionState : IPeripheral
    {
        IReadOnlyDictionary<string, int> StateBits { get; }
        bool TryConvertStateObjToUlong(IContextState stateObj, out ulong? state);
        bool TryConvertUlongToStateObj(ulong? state, out IContextState stateObj);
    }
}
