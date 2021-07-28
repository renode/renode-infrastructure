//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Analog
{
    // This is a very incomplete stub and its only purpose is to make Linux boot proceed faster.
    // It may break other software - in that case, please remove this peripheral from your .repl.
    public class Xilinx_XADC : BasicDoubleWordPeripheral, IKnownSize
    {
        public Xilinx_XADC(Machine machine) : base(machine)
        {
            DefineRegisters();

            IRQ = new GPIO();

            Reset();
        }

        public override void Reset()
        {
            base.Reset();
        }

        public long Size => 0x1C;
        public GPIO IRQ { get; }

        private void DefineRegisters()
        {
            Register.Configuration.Define(this, resetValue: 0x00001114)
                .WithTag("IGAP", 0, 5)
                .WithReservedBits(5, 3)
                .WithTag("TCKRATE", 8, 2)
                .WithReservedBits(10, 2)
                .WithTag("REDGE", 12, 1)
                .WithTag("WEDGE", 13, 1)
                .WithReservedBits(14, 2)
                .WithTag("DFIFOTH", 16, 4)
                .WithTag("CFIFOTH", 20, 4)
                .WithReservedBits(24, 7)
                .WithTag("ENABLE", 31, 1);

            // We fix DFIFO_GTH to true because we want Linux to think data is available for reading
            // from the read data FIFO.
            Register.InterruptStatus.Define(this, resetValue: 0x00000200)
                .WithTag("ALM", 0, 7)
                .WithTag("OT", 7, 1)
                .WithFlag(8, FieldMode.Read, valueProviderCallback: (oldValue) => true, name: "DFIFO_GTH")
                .WithTag("CFIFO_LTH", 9, 1)
                .WithReservedBits(10, 22)
                .WithReadCallback((oldValue, newValue) => IRQ.Unset());

            Register.InterruptMask.Define(this, resetValue: 0xFFFFFFFF)
                .WithTag("M_ALM", 0, 7)
                .WithTag("M_OT", 7, 1)
                .WithTag("M_DFIFO_GTH", 8, 1)
                .WithTag("M_CFIFO_LTH", 9, 1)
                .WithReservedBits(10, 22);

            Register.MiscStatus.Define(this, resetValue: 0x00000500)
                .WithTag("ALM", 0, 7)
                .WithTag("OT", 7, 1)
                .WithTag("DFIFOE", 8, 1)
                .WithTag("DFIFOF", 9, 1)
                .WithTag("CFIFOE", 10, 1)
                .WithTag("CFIFOF", 11, 1)
                .WithTag("DFIFO_LVL", 12, 4)
                .WithTag("CFIFO_LVL", 16, 4)
                .WithReservedBits(20, 12);

            Register.CommmandFifo.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (oldValue, newValue) =>
                {
                    this.DebugLog($"Command FIFO write: {newValue:x}");
                    IRQ.Set();
                });

            Register.DataFifo.Define(this)
                .WithTag("RDDATA", 0, 32);

            Register.MiscControl.Define(this)
                .WithReservedBits(0, 1, 0)
                .WithReservedBits(1, 3)
                .WithTag("RESET", 4, 1)
                .WithReservedBits(5, 27);
        }

        private enum Register: long
        {
            Configuration   = 0x00, // XADCIF_CFG
            InterruptStatus = 0x04, // XADCIF_INT_STS
            InterruptMask   = 0x08, // XADCIF_INT_MASK
            MiscStatus      = 0x0C, // XADCIF_MSTS
            CommmandFifo    = 0x10, // XADCIF_CMDFIFO
            DataFifo        = 0x14, // XADCIF_RDFIFO
            MiscControl     = 0x18, // XADCIF_MCTL
        }
    }
}
