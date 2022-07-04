//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class EmptyCPU : ICPU
    {

        public EmptyCPU(Machine machine)
        {
            this.machine = machine;
        }

        public virtual void Start()
        {

        }

        public virtual void Pause()
        {
        }

        public virtual void Resume()
        {

        }

        public virtual void Reset()
        {

        }

        public uint Id => 0;

        public virtual void SyncTime()
        {
        }
        
        public TimeHandle TimeHandle => null;

        public virtual ulong ExecutedInstructions
        {
            get
            {
                return 0;
            }
        }

        public virtual RegisterValue PC
        {
            get
            {
                return 0;
            }
            set
            {
            }
        }

        public SystemBus Bus
        {
            get
            {
                return machine.SystemBus;
            }
        }

        public virtual string Model
        {
            get
            {
                return "empty";
            }
        }

        public virtual void Load(PrimitiveReader reader)
        {

        }

        public bool IsHalted
        {
            get
            {
                return false;
            }
            set
            {
                if(value)
                {
                    // just to make the compilation warning about unused event disappear
                    Halted?.Invoke(null);
                }
            }
        }

        public bool OnPossessedThread { get { return true; } }

        public event Action<HaltArguments> Halted;

        public virtual void Save(PrimitiveWriter writer)
        {

        }

        public ulong Step(int count = 1, bool? blocking = null)
        {
            return 0;
        }

        public ExecutionMode ExecutionMode { get; set; }


        protected readonly Machine machine;
    }
}

