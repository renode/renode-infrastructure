//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    public struct GBDRegisterDescriptor
    {
        public GBDRegisterDescriptor(uint number, uint size, string name, string type, string group) : this()
        {
            this.Number = number;
            this.Size = size;
            this.Name = name;
            this.Type = type;
            this.Group = group;
        }

        public uint Number { get; }
        public uint Size { get; }
        public string Name { get; }
        public string Type { get; }
        public string Group { get; }
    }

    public struct GBDFeatureDescriptor
    {
        public GBDFeatureDescriptor(string name) : this()
        {
            this.Name = name;
            this.Registers = new List<GBDRegisterDescriptor>();
        }

        public string Name { get; }
        public List<GBDRegisterDescriptor> Registers { get; }
    }

    public interface ICpuSupportingGdb : ICPUWithHooks, IControllableCPU
    {
        ulong Step(int count = 1);
        ExecutionMode ExecutionMode { get; set; }
        event Action<HaltArguments> Halted;
        void EnterSingleStepModeSafely(HaltArguments args);

        string GDBArchitecture { get; }
        List<GBDFeatureDescriptor> GDBFeatures { get; }
        bool DebuggerConnected { get; set; }
        uint Id { get; }
        string Name { get; }
    }
}

