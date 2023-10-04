//
// Copyright (c) 2010 - 2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Video
{
    public class LiteX_Framebuffer : AutoRepaintingVideo, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public LiteX_Framebuffer(IMachine machine, PixelFormat format, IBusPeripheral memory) : base(machine)
        {
            this.memory = memory;
            sysbus = machine.GetSystemBus(this);
            this.format = format;

            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public void WriteDoubleWord(long address, uint value)
        {
            RegistersCollection.Write(address, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            bufferAddress = 0;
        }

        public long Size => 0x100;
        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        protected override void Repaint()
        {
            sysbus.ReadBytes(bufferAddress, buffer.Length, buffer, 0);
        }

        private void DefineRegisters()
        {
            Registers.DriverClockingMMCMDrdy.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true) // this should always be ready
            ;

            Registers.InitiatorHres.DefineMany(this, 2, stepInBytes: 4, setup: (reg, idx) =>
                reg.WithValueField(0, 8, out hres[idx], name: $"h_res_{idx}").WithIgnoredBits(8, 24)) // ignore upper bits to avoid unnecessary warnings in log
            ;

            Registers.InitiatorVres.DefineMany(this, 2, stepInBytes: 4, setup: (reg, idx) =>
                reg.WithValueField(0, 8, out vres[idx], name: $"v_res_{idx}").WithIgnoredBits(8, 24)) // ignore upper bits to avoid unnecessary warnings in log
            ;

            Registers.InitiatorBase.DefineMany(this, 4, stepInBytes: 4, setup: (reg, idx) =>
                reg.WithValueField(0, 8, out bufferRegisters[idx], name: $"base_{idx}").WithIgnoredBits(8, 24)) // ignore upper bits to avoid unnecessary warnings in log
            ;

            Registers.InitiatorEnable.Define(this)
                .WithFlag(0, writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        var height = (int)((vres[0].Value << 8) | vres[1].Value);
                        var width = (int)((hres[0].Value << 8) | hres[1].Value);
                        bufferAddress = (uint)((bufferRegisters[0].Value << 24) | (bufferRegisters[1].Value << 16) | (bufferRegisters[2].Value << 8) | bufferRegisters[3].Value);

                        var memoryBase = (uint)sysbus.GetRegistrationPoints(memory).First().Range.StartAddress;
                        bufferAddress += memoryBase;

                        this.Log(LogLevel.Debug, "Reconfiguring screen to {0}x{1}", width, height);

                        Reconfigure(width, height, format);
                    }
                    else
                    {
                        // it stops the repainter by passing nulls
                        Reconfigure();
                    }
                })
            ;
        }

        private IValueRegisterField[] vres = new IValueRegisterField[2];
        private IValueRegisterField[] hres = new IValueRegisterField[2];
        private IValueRegisterField[] bufferRegisters = new IValueRegisterField[4];

        private uint bufferAddress;

        private readonly IBusPeripheral memory;
        private readonly IBusController sysbus;
        private readonly PixelFormat format;

        private enum Registers
        {
            UnderflowEnable = 0x0,
            UnderflowUpdate = 0x4,
            UnderflowCounter = 0x8,

            InitiatorEnable = 0x18,
            InitiatorHres = 0x1c,
            InitiatorHsyncStart = 0x24,
            InitiatorHsyncEnd = 0x2c,
            InitiatorHscan = 0x34,
            InitiatorVres = 0x3c,
            InitiatorVsyncStart = 0x44,
            InitiatorVsyncEnd = 0x4c,
            InitatorVscan = 0x54,
            InitiatorBase = 0x5c,
            InitiatorLenght = 0x6c,

            DmaDelay = 0x7c,

            DriverClockingMMCMReset = 0x8c,
            DriverClockingMMCMRead = 0x90,
            DriverClockingMMCMWrite = 0x94,
            DriverClockingMMCMDrdy = 0x98,
            DriverClockingMMCMAdr = 0x9c,
            DriverClockingMMCMDatW = 0xa0,
            DriverClockingMMCMDatR = 0xa8
        }
    }
}
