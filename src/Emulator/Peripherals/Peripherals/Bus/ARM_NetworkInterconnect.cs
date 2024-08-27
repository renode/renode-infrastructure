//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Bus
{
    // The peripheral is a model of ARM CoreLink NIC-400.
    public class ARM_NetworkInterconnect : BasicDoubleWordPeripheral, IKnownSize
    {
        public ARM_NetworkInterconnect(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x100000;
        public uint UserDefinedPeripheralID { get; set; } = 0x0;

        private void DefineRegisters()
        {
            Registers.PeripheralID4.Define(this, 0x04)
                .WithReservedBits(8, 24)
                .WithValueField(0, 8, FieldMode.Read, name: "JEP106 Continuation code");
            Registers.PeripheralID0.Define(this, 0x0)
                .WithReservedBits(8, 24)
                .WithValueField(0, 8, FieldMode.Read, name: "Part Number [7:0]");
            Registers.PeripheralID1.Define(this, 0xB4)
                .WithReservedBits(8, 24)
                .WithValueField(4, 4, FieldMode.Read, name: "JEP106 Identity")
                .WithValueField(0, 4, FieldMode.Read, name: "Part Number [11:8]");
            Registers.PeripheralID2.Define(this, 0x4B)
                .WithReservedBits(8, 24)
                .WithValueField(4, 4, FieldMode.Read, name: "Part Revision")
                .WithFlag(3, FieldMode.Read, name: "JEP106 Code flag")
                .WithValueField(0, 3, FieldMode.Read, name: "JEP Identity [6:4]");
            Registers.PeripheralID3.Define(this, UserDefinedPeripheralID)
                .WithReservedBits(8, 24)
                .WithValueField(0, 8, FieldMode.Read, name: "User Peripheral ID");
            Registers.ComponentID0.Define(this, 0x0D)
                .WithReservedBits(8, 24)
                .WithValueField(0, 8, FieldMode.Read, name: "Preamble");
            Registers.ComponentID1.Define(this, 0xF0)
                .WithReservedBits(8, 24)
                .WithValueField(0, 8, FieldMode.Read, name: "Generic IP component class");
            Registers.ComponentID2.Define(this, 0x05)
                .WithReservedBits(8, 24)
                .WithValueField(0, 8, FieldMode.Read, name: "Preamble");
            Registers.ComponentID3.Define(this, 0xB1)
                .WithReservedBits(8, 24)
                .WithValueField(0, 8, FieldMode.Read, name: "Preamble");
        }

        private enum Registers
        {
            Remap = 0x0, // remap
            Security_0 = 0x08, // security<n>

            PeripheralID4 = 0x1FD0,
            PeripheralID5 = 0x1FD4,
            PeripheralID6 = 0x1FD8,
            PeripheralID7 = 0x1FDC,
            PeripheralID0 = 0x1FE0,
            PeripheralID1 = 0x1FE4,
            PeripheralID2 = 0x1FE8,
            PeripheralID3 = 0x1FEC,
            ComponentID0 = 0x1FF0,
            ComponentID1 = 0x1FF4,
            ComponentID2 = 0x1FF8,
            ComponentID3 = 0x1FFC,

            MasterBusMatrixFunctionalityModification_0 = 0x2008, // AMIB fn_mod_bm_iss<n>
            MasterSynchronizationMode_0 = 0x2020, // AMIB sync_mode<n>
            MasterBypassMerge_0 = 0x2024, // AMIB fn_mod2<n>
            MasterLongBurstModification_0 = 0x202C, // AMIB fn_mod_lb<n>
            MasterWFIFOTidemark_0 = 0x2040, // AMIB wr_tidemark<n>
            MasterAHBControl_0 = 0x2044, // AMIB ahb_cntl<n>
            MasterFunctionalityModification_0 = 0x2108, // AMIB fn_mod<n>

            SlaveSynchronizationMode_0 = 0x42020, // ASIB sync_mode<n>
            SlaveBypassMerge_0 = 0x42024, // ASIB fn_mod<n>
            SlaveFunctionalityModificationAHB_0 = 0x42028, // ASIB fn_mod_ahb<n>
            SlaveLongBurst_0 = 0x4202C, // ASIB fn_mod_lb<n>
            SlaveWFIFOTidemark_0 = 0x42040, // ASIB wr_tidemark<n>
            SlaveReadChannelQoS_0 = 0x42100, // ASIB read_qos<n>
            SlaveWriteChanelQoS_0 = 0x42104, // ASIB write_qos<n>
            SlaveFunctionalityModification_0 = 0x42108, // ASIB fn_mod<n>

            InternalBusMatrixFunctionalityModification_0 = 0xC2008, // IB fn_mod_bm_iss<n>
            InternalSynchronizationMode_0 = 0xC2020, // IB sync_mode<n>
            InternalBypassMerge_0 = 0xC2024, // IB fn_mod2<n>
            InternalLongBurst_0 = 0xC202C, // IB fn_mod_lb<n>
            InternalWFIFOTidemark_0 = 0xC2040, // IB wr_tidemark<n>
            InternalFunctionalityModification_0 = 0xC2108, // IB fn_mod<n>
        }
    }
}
