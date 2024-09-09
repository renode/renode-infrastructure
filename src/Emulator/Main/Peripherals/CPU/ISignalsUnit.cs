//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ISignalsUnit
    {
        ulong GetAddress(string name);
        ulong GetSignal(string name);

        bool IsSignalEnabled(string name);
        bool IsSignalEnabledForCPU(string name, ICPU cpu);

        void SetSignal(string name, ulong value);
        void SetSignalFromAddress(string name, ulong address);

        void SetSignalState(string name, bool state, uint index);
        void SetSignalStateForCPU(string name, bool state, ICPU cpu);
    }
}

