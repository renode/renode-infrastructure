//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class EOSS3_SystemDMABridge : BasicDoubleWordPeripheral, IKnownSize
    {
        public EOSS3_SystemDMABridge(IMachine machine, UDMA systemDma) : base(machine)
        {
            this.systemDma = systemDma;
            DefineRegisters();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.Request.Define(this)
                .WithValueField(0, 10, FieldMode.Write, writeCallback: (_, value) => BitHelper.ForeachActiveBit(value, x => systemDma.InitTransfer(x, true)), name: "dma_req")
                .WithReservedBits(11, 5)
                .WithValueField(16, 10, FieldMode.Write, writeCallback: (_, value) => BitHelper.ForeachActiveBit(value, x => systemDma.InitTransfer(x, false)), name: "dma_req")
                .WithReservedBits(27, 5)
            ;

            Registers.Active.Define(this)
                .WithValueField(0, 10, FieldMode.Read, name: "dma_active") //it always returns 0, as the transfer is instant. Keeping this field for possible future latching etc
                .WithReservedBits(11, 21)
            ;

            // this register is effectively a scratchpad
            Registers.SramTimingAdjust.Define(this)
                .WithReservedBits(0, 1)
                .WithFlag(1, name: "sdma_sram_rme")
                .WithValueField(2, 4, name: "sdma_sram_rm")
                .WithReservedBits(6, 26)
            ;
        }

        private readonly UDMA systemDma;

        private enum Registers
        {
            Request = 0x0,
            WaitOnRequest = 0x4,
            Active = 0x8,
            PowerDownEventThreshold = 0xC,
            SramTimingAdjust = 0x10,
        }
    }
}
