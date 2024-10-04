//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithPSCI : ICPU
    {
        void AddCustomPSCIStub(ulong functionIdentifier, Action stub);
    }
}
