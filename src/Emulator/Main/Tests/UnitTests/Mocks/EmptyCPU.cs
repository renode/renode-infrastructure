//
// Copyright (c) 2010-2018 Antmicro
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

        public virtual void MapMemory(IMappedSegment segment)
        {
        }

        public virtual void UnmapMemory(Range range)
        {
        }

        public void SetPageAccessViaIo(ulong address)
        {
        }

        public void ClearPageAccessViaIo(ulong address)
        {
        }

        public virtual void UpdateContext()
        {
        }

        public virtual void SyncTime()
        {
        }

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
            }
        }

        public bool OnPossessedThread { get { return true; } }

        public virtual void Save(PrimitiveWriter writer)
        {

        }

        public void EnableProfiling()
        {

        }

        protected readonly Machine machine;
    }
}

