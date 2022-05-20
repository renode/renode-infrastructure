//
// Copyright (c) 2010 - 2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Linq;
using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Video
{
    public class LiteX_Framebuffer_CSR32 : AutoRepaintingVideo, IBusPeripheral
    {
        public LiteX_Framebuffer_CSR32(Machine machine, PixelFormat format, IBusPeripheral memory, uint offset = 0, ushort hres = 0, ushort vres = 0) : base(machine)
        {
            this.memory = memory;
            this.resetOffset = offset;
            this.resetHres = hres;
            this.resetVres = vres;
            this.machine = machine;
            this.format = format;

            DefineDMARegisters();
            DefineVTGRegisters();
        }

        [ConnectionRegion("dma")]
        public void WriteDoubleWordToDMA(long address, uint value)
        {
            dmaRegisters.Write(address, value);
        }

        [ConnectionRegion("dma")]
        public uint ReadDoubleWordFromDMA(long offset)
        {
            return dmaRegisters.Read(offset);
        }

        [ConnectionRegion("vtg")]
        public void WriteDoubleWordToVTG(long address, uint value)
        {
            vtgRegisters.Write(address, value);
        }

        [ConnectionRegion("vtg")]
        public uint ReadDoubleWordFromVTG(long offset)
        {
            return vtgRegisters.Read(offset);
        }

        public override void Reset()
        {
            dmaRegisters.Reset();
            vtgRegisters.Reset();
            bufferAddress = 0;
        }

        protected override void Repaint()
        {
            machine.SystemBus.ReadBytes(bufferAddress, buffer.Length, buffer, 0);
        }

        private void DefineDMARegisters()
        {
            dmaRegisters = new DoubleWordRegisterCollection(this);

            DMARegisters.Base.Define(dmaRegisters, resetOffset)
                .WithValueField(0, 32, out bufferRegister, name: "base");

            DMARegisters.Enable.Define(dmaRegisters)
                .WithFlag(0, writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        var height = (int)vres.Value;
                        var width = (int)hres.Value;
                        bufferAddress = bufferRegister.Value;

                        var memoryBase = (uint)machine.SystemBus.GetRegistrationPoints(memory).First().Range.StartAddress;
                        bufferAddress += memoryBase;

                        this.Log(LogLevel.Debug, "Reconfiguring screen to {0}x{1}", width, height);

                        Reconfigure(width, height, format);
                    }
                    else
                    {
                        // it stops the repainter by passing nulls
                        Reconfigure();
                    }
                });
        }

        private void DefineVTGRegisters()
        {
            vtgRegisters = new DoubleWordRegisterCollection(this);

            VTGRegisters.Hres.Define(vtgRegisters, resetHres)
                .WithValueField(0, 16, out hres, name: "h_res").WithReservedBits(16, 16);

            VTGRegisters.Vres.Define(vtgRegisters, resetVres)
                .WithValueField(0, 16, out vres, name: "v_res").WithReservedBits(16, 16);
        }

        private DoubleWordRegisterCollection dmaRegisters;
        private DoubleWordRegisterCollection vtgRegisters;

        private IValueRegisterField vres;
        private IValueRegisterField hres;
        private IValueRegisterField bufferRegister;

        private uint bufferAddress;

        private readonly IBusPeripheral memory;
        private readonly Machine machine;
        private readonly PixelFormat format;
        private readonly uint resetOffset;
        private readonly uint resetHres;
        private readonly uint resetVres;

        private enum DMARegisters
        {
            Base = 0x0,
            Length = 0x4,
            Enable = 0x8,
        }

        private enum VTGRegisters
        {
            Enable = 0x0,
            Hres = 0x4,
            HsyncStart = 0x8,
            HsyncEnd = 0xc,
            Hscan = 0x10,
            Vres = 0x14,
            VsyncStart = 0x18,
            VsyncEnd = 0x1c,
            Vscan = 0x20,
        }
    }
}
