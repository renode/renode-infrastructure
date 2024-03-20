//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class RenesasDA14_XTAL32MRegisters : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasDA14_XTAL32MRegisters(IMachine machine)
        {
            this.machine = machine;
            RegistersCollection = new DoubleWordRegisterCollection(this);

            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size => 0x2C;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public bool Enable { get => xtal32mEnable.Value; set => xtal32mEnable.Value = value; }

        private void DefineRegisters()
        {
            Registers.Start.Define(this, 0x70550)
                .WithValueField(0, 8, name: "XTAL32M_TRIM")
                .WithValueField(8, 4, name: "XTAL32M_CUR_SET")
                .WithValueField(12, 3, name: "XTAL32M_AMPL_SET")
                .WithValueField(15, 2, name: "XTAL32M_CMP_LVL")
                .WithValueField(17, 3, name: "XTAL32M_CMP_BLANK")
                .WithValueField(20, 6, name: "XTAL32M_TIMEOUT")
                .WithReservedBits(26, 6);

            Registers.Settle.Define(this, 0x685A0)
                .WithValueField(0, 8, name: "XTAL32M_TRIM")
                .WithValueField(8, 4, name: "XTAL32M_CUR_SET")
                .WithValueField(12, 3, name: "XTAL32M_AMPL_SET")
                .WithValueField(15, 2, name: "XTAL32M_CMP_LVL")
                .WithValueField(17, 3, name: "XTAL32M_CMP_BLANK")
                .WithValueField(20, 6, name: "XTAL32M_TIMEOUT")
                .WithReservedBits(26, 6);

            Registers.Trim.Define(this, 0x84A0)
                .WithValueField(0, 8, name: "XTAL32M_TRIM")
                .WithValueField(8, 4, name: "XTAL32M_CUR_SET")
                .WithValueField(12, 3, name: "XTAL32M_AMPL_SET")
                .WithValueField(15, 2, name: "XTAL32M_CMP_LVL")
                .WithReservedBits(17, 15);

            Registers.CapacitanceMeasurement.Define(this, 0x98)
                .WithValueField(0, 3, name: "XTAL32M_CAP_SELECT")
                .WithValueField(3, 2, name: "XTAL32M_MEAS_CUR")
                .WithFlag(5, name: "XTAL32M_MEAS_START")
                .WithValueField(6, 2, name: "XTAL32M_MEAS_TIME")
                .WithReservedBits(8, 24);

            Registers.StateMachine.Define(this)
                .WithFlag(0, name: "XTAL32M_CUR_MODE")
                .WithFlag(1, name: "XTAL32M_TRIM_MODE")
                .WithFlag(2, name: "XTAL32M_CMP_MODE")
                .WithFlag(3, name: "XTAL32M_FSM_FORCE_IDLE")
                .WithFlag(4, name: "XTAL32M_FSM_APPLY_CONFIG")
                .WithReservedBits(5, 27);

            Registers.Control.Define(this, 0x15)
                .WithValueField(0, 2, name: "XTAL32M_BIAS_SAH")
                .WithValueField(2, 2, name: "XTAL32M_AMPREG_SAH")
                .WithValueField(4, 2, name: "XTAL32M_LDO_SAH")
                .WithValueField(6, 2, name: "XTAL32M_BIASPROT")
                .WithFlag(8, out xtal32mEnable, name: "XTAL32M_ENABLE",
                        writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                xtal32mReady.Value = true;
                                xtal32mState.Value = XTAL32MState.Run;
                            }
                        })
                .WithReservedBits(9, 23);

            Registers.InterruptControl.Define(this, 0x9FF)
                .WithValueField(0, 8, name: "XTAL32M_IRQ_CNT")
                .WithFlag(8, name: "XTAL32M_IRQ_CLK")
                .WithFlag(9, name: "XTAL32M_IRQ_ENABLE")
                .WithValueField(10, 2, name: "XTAL32M_IRQ_CAP_CTRL")
                .WithReservedBits(12, 20);

            Registers.Status.Define(this)
                .WithFlag(0, out xtal32mReady, name: "XTAL32M_READY")
                .WithEnumField<DoubleWordRegister, XTAL32MState>(1, 3, out xtal32mState, name: "XTAL32M_STATE", valueProviderCallback: (_) => xtal32mEnable.Value ? xtal32mState.Value : XTAL32MState.Idle)
                .WithFlag(4, name: "XTAL32M_CMP_OUT")
                .WithFlag(5, FieldMode.Toggle, name: "XTAL32M_LDO_OK")
                .WithValueField(6, 5, name: "XTAL32M_CUR_SET_STAT")
                .WithValueField(11, 8, name: "XTAL32M_TRIM_VAL")
                .WithReservedBits(19, 13);

            Registers.InterruptStatus.Define(this)
                .WithValueField(0, 8, name: "XTAL32M_IRQ_COUNT_STAT")
                .WithValueField(8, 8, name: "XTAL32M_IRQ_COUNT_CAP")
                .WithReservedBits(16, 16);
        }

        private IMachine machine;

        private IEnumRegisterField<XTAL32MState> xtal32mState;
        private IFlagRegisterField xtal32mEnable;
        private IFlagRegisterField xtal32mReady;

        private enum Registers
        {
            Start = 0x0,
            Settle = 0x4,
            Trim = 0x8,
            CapacitanceMeasurement = 0xC,
            StateMachine = 0x10,
            Control = 0x14,
            InterruptControl = 0x18,
            Status = 0x24,
            InterruptStatus = 0x28,
        }

        private enum XTAL32MState
        {
            Idle = 0,
            WaitLdo,
            WaitBias,
            StartBlank,
            Start,
            SettleBlank,
            Settle,
            Run,
            CapTestIdle,
            CapTestMeas,
            CapTestEnd,
        }
    }
}
