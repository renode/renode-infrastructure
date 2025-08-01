//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class Renesas_SCI : UARTBase, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public Renesas_SCI(IMachine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);

            DefineRegisters();
        }

        public override void WriteChar(byte value)
        {
            receiveFifo.Enqueue(value);
            UpdateInterrupts();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
            receiveFifo.Clear();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x400;

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        public GPIO RxIRQ { get; } = new GPIO();
        public GPIO TxIRQ { get; } = new GPIO();
        public GPIO TxEndIRQ { get; } = new GPIO();

        protected override void CharWritten()
        {
            // intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private void UpdateInterrupts()
        {
            // On real hardware FCR.RTRG value doesn't affect interrupt requests,
            // they are triggered for every character in RX fifo.
            RxIRQ.Set(receiveInterruptEnable.Value && receiveFifo.Count > 0);

            TxEndIRQ.Set(transmitEndInterruptEnable.Value);
            TxIRQ.Set(transmitInterruptEnable.Value);
        }

        private void DefineRegisters()
        {
            Registers.ReceiveData.Define(this, resetValue: 0x0)
                .WithValueField(0, 9, FieldMode.Read, name: "RDAT",
                    valueProviderCallback: _ =>
                    {
                        if(!receiveFifo.TryDequeue(out byte value))
                        {
                            this.Log(LogLevel.Warning, "Trying to read data from empty receive fifo");
                        }
                        UpdateInterrupts();
                        return value;
                    })
                .WithTaggedFlag("MPB", 9)
                .WithFlag(10, mode: FieldMode.Read, name: "DR",
                    valueProviderCallback: _ =>
                    {
                        return receiveFifo.Count > 0;
                    })
                .WithTaggedFlag("FPER", 11)
                .WithTaggedFlag("FFER", 12)
                .WithReservedBits(13, 11)
                .WithTaggedFlag("ORER", 24)
                .WithReservedBits(25, 2)
                .WithTaggedFlag("PER", 27)
                .WithTaggedFlag("FER", 28)
                .WithReservedBits(29, 3);

            Registers.TransmitData.Define(this, resetValue: 0xffffffff)
                .WithValueField(0, 9, FieldMode.Write, name: "TDAT",
                    writeCallback: (_, value) =>
                    {
                        if(BitHelper.IsBitSet(value, 8))
                        {
                            this.Log(LogLevel.Warning, "Trying to transmit data with 9-th bit set: {0:X}, sending: {1:X}", value, (byte)value);
                        }
                        this.TransmitCharacter((byte)value);
                    })
                .WithTaggedFlag("MPBT", 9)
                .WithReservedBits(10, 22);

            // no special effects, software requires RE and TE flags to be settable
            Registers.CommonControl0.Define(this, resetValue: 0x0)
                .WithFlag(0, name: "RE")
                .WithReservedBits(1, 3)
                .WithFlag(4, name: "TE")
                .WithReservedBits(5, 3)
                .WithTaggedFlag("MPIE", 8)
                .WithTaggedFlag("DCME", 9)
                .WithTaggedFlag("IDSEL", 10)
                .WithReservedBits(11, 5)
                .WithFlag(16, out receiveInterruptEnable, name: "RIE")
                .WithReservedBits(17, 3)
                .WithFlag(20, out transmitInterruptEnable, name: "TIE")
                .WithFlag(21, out transmitEndInterruptEnable, name: "TEIE")
                .WithReservedBits(22, 2)
                .WithTaggedFlag("SSE", 24)
                .WithReservedBits(25, 7)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.CommonControl1.Define(this, resetValue: 0x10)
                .WithTaggedFlag("CTSE", 0)
                .WithTaggedFlag("CTSPEN", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("SPB2DT", 4)
                .WithTaggedFlag("SPB2IO", 5)
                .WithReservedBits(6, 2)
                .WithTaggedFlag("PE", 8)
                .WithTaggedFlag("PM", 9)
                .WithReservedBits(10, 2)
                .WithTaggedFlag("TINV", 12)
                .WithTaggedFlag("RINV", 13)
                .WithReservedBits(14, 2)
                .WithTaggedFlag("SPLP", 16)
                .WithReservedBits(17, 3)
                .WithTaggedFlag("SHARPS", 20)
                .WithReservedBits(21, 3)
                .WithTag("NFCS", 24, 3)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("NFEN", 28)
                .WithReservedBits(29, 3);

            Registers.CommonControl2.Define(this, 0xff00ff04)
                .WithTag("BCP", 0, 3)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("BGDM", 4)
                .WithTaggedFlag("ABCS", 5)
                .WithTaggedFlag("ABCSE", 6)
                .WithReservedBits(7, 1)
                .WithTag("BRR", 8, 8)
                .WithTaggedFlag("BRME", 16)
                .WithReservedBits(17, 3)
                .WithTag("CKS", 20, 2)
                .WithReservedBits(22, 2)
                .WithTag("MDDR", 24, 8);

            Registers.CommonControl3.Define(this, 0x00001203)
                .WithTaggedFlag("CPHA", 0)
                .WithTaggedFlag("CPOL", 1)
                .WithReservedBits(2, 5)
                .WithTaggedFlag("BPEN", 7)
                .WithTag("CHR", 8, 2)
                .WithReservedBits(10, 2)
                .WithTaggedFlag("LSBF", 12)
                .WithTaggedFlag("SINV", 13)
                .WithTaggedFlag("STP", 14)
                .WithTaggedFlag("RXDESEL", 15)
                .WithTag("MOD", 16, 3)
                .WithTaggedFlag("MP", 19)
                .WithTaggedFlag("FM", 20)
                .WithTaggedFlag("DEN", 21)
                .WithReservedBits(22, 2)
                .WithTag("CKE", 24, 2)
                .WithReservedBits(26, 2)
                .WithTaggedFlag("GM", 28)
                .WithTaggedFlag("BLK", 29)
                .WithReservedBits(30, 2);

            Registers.CommonControl4.Define(this)
                .WithTag("CMPD", 0, 9)
                .WithReservedBits(9, 7)
                .WithTaggedFlag("ASEN", 16)
                .WithTaggedFlag("ATEN", 17)
                .WithReservedBits(18, 6)
                .WithTag("AST", 24, 3)
                .WithTaggedFlag("AJD", 27)
                .WithTag("ATT", 28, 3)
                .WithTaggedFlag("AET", 31);

            Registers.SimpleI2CControl.Define(this)
                .WithTag("IICDL", 0, 5)
                .WithReservedBits(5, 3)
                .WithTaggedFlag("IICINTM", 8)
                .WithTaggedFlag("IICCSC", 9)
                .WithReservedBits(10, 3)
                .WithTaggedFlag("IICACKT", 13)
                .WithReservedBits(14, 2)
                .WithTaggedFlag("IICSTAREQ", 16)
                .WithTaggedFlag("IICRSTAREQ", 17)
                .WithTaggedFlag("IICSTPREQ", 18)
                .WithReservedBits(19, 1)
                .WithTag("IICSDAS", 20, 2)
                .WithTag("IICSLS", 22, 2)
                .WithReservedBits(24, 8);

            Registers.FIFOControlRegister.Define(this, resetValue: 0x1f1f0000)
                .WithTaggedFlag("DRES", 0)
                .WithReservedBits(1, 7)
                .WithTag("TTRG", 8, 5)
                .WithReservedBits(13, 2)
                .WithTaggedFlag("TFRST", 15)
                // On real hardware FCR.RTRG value doesn't affect interrupt requests
                // they are triggered for every character in RX fifo.
                .WithValueField(16, 5, out receiveFifoDataTriggerNumber, name: "RTRG",
                    writeCallback: (oldValue, newValue) =>
                    {
                        if(newValue > 0xf)
                        {
                            this.Log(LogLevel.Warning, "{0:X} - value prohibited for FCR.RTRG field, keeping the previous value: {1:X}", newValue, oldValue);
                            receiveFifoDataTriggerNumber.Value = oldValue;
                        }
                        UpdateInterrupts();
                    })
                .WithReservedBits(21, 2)
                .WithTaggedFlag("RFRST", 23)
                .WithTag("RSTRG", 24, 5)
                .WithReservedBits(29, 3);

            Registers.DriverControl.Define(this)
                .WithTaggedFlag("DEPOL", 0)
                .WithReservedBits(1, 7)
                .WithTag("DEAST", 8, 5)
                .WithReservedBits(13, 3)
                .WithTag("DENGT", 16, 5)
                .WithReservedBits(21, 11);

            Registers.CommonStatus.Define(this, 0x60008000)
                .WithReservedBits(0, 4).WithTaggedFlag("ERS", 4).WithReservedBits(5, 10)
                .WithTaggedFlag("RXDM ON", 15)
                .WithTaggedFlag("DCMF", 16)
                .WithTaggedFlag("DPER", 17)
                .WithTaggedFlag("DFER", 18)
                .WithReservedBits(19, 5)
                .WithTaggedFlag("ORER", 24)
                .WithReservedBits(25, 1)
                .WithTaggedFlag("MFF", 26)
                .WithTaggedFlag("PER", 27)
                .WithTaggedFlag("FER", 28)
                .WithFlag(29, FieldMode.Read, name: "TDRE", valueProviderCallback: _ => true)
                .WithFlag(30, FieldMode.Read, name: "TEND", valueProviderCallback: _ => true)
                .WithFlag(31, FieldMode.Read, name: "RDRF", valueProviderCallback: _ =>
                {
                    return true;
                });

            Registers.SimpleI2CStatus.Define(this)
                .WithTaggedFlag("IICACKR", 0)
                .WithReservedBits(1, 2)
                .WithTaggedFlag("IICSTIF", 3)
                .WithReservedBits(4, 28);

            Registers.FIFOReceiveStatus.Define(this, resetValue: 0x0)
                .WithFlag(0, mode: FieldMode.Read, name: "DR",
                    valueProviderCallback: _ =>
                    {
                        return receiveFifo.Count > 0;
                    })
                .WithReservedBits(1, 7)
                .WithValueField(8, 6, FieldMode.Read, name: "R",
                    valueProviderCallback: _ => (ulong)receiveFifo.Count)
                .WithReservedBits(14, 2)
                .WithTag("PNUM", 16, 6)
                .WithReservedBits(22, 2)
                .WithTag("FNUM", 24, 6)
                .WithReservedBits(30, 2);

            Registers.FIFOTransmitStatus.Define(this)
                .WithValueField(0, 6, FieldMode.Read, name: "T", valueProviderCallback: _ =>
                {
                    return 0;
                })
                .WithReservedBits(6, 26);

            Registers.CommonFlagClear.Define(this)
                .WithReservedBits(0, 4)
                .WithTaggedFlag("ERSC", 4)
                .WithReservedBits(5, 11)
                .WithTaggedFlag("DCMFC", 16)
                .WithTaggedFlag("DPERC", 17)
                .WithTaggedFlag("DFERC", 18)
                .WithReservedBits(19, 5)
                .WithTaggedFlag("ORERC", 24)
                .WithReservedBits(25, 1)
                .WithTaggedFlag("MFFS", 26)
                .WithTaggedFlag("PERC", 27)
                .WithTaggedFlag("FERC", 28)
                .WithTaggedFlag("TDREC", 29)
                .WithReservedBits(30, 1)
                .WithTaggedFlag("RDRFC", 31);

            Registers.SimpleI2CFlagClear.Define(this)
                .WithReservedBits(0, 3)
                .WithTaggedFlag("IICSTIFC", 3)
                .WithReservedBits(4, 27);

            Registers.FIFOFlagClear.Define(this)
                .WithTaggedFlag("DRC", 0)
                .WithReservedBits(1, 31);
        }

        private IValueRegisterField receiveFifoDataTriggerNumber;
        private IFlagRegisterField receiveInterruptEnable;
        private IFlagRegisterField transmitInterruptEnable;
        private IFlagRegisterField transmitEndInterruptEnable;

        private readonly Queue<byte> receiveFifo = new Queue<byte>();

        private enum Registers : long
        {
            ReceiveData = 0x0, // RDR
            TransmitData = 0x4, // TDR
            CommonControl0 = 0x8, // CCR0
            CommonControl1 = 0xC, // CCR1
            CommonControl2 = 0x10, // CCR2
            CommonControl3 = 0x14, // CCR3
            CommonControl4 = 0x18, // CCR4
            SimpleI2CControl = 0x20, // ICR
            FIFOControlRegister = 0x24, // FCR
            DriverControl = 0x30, // DCR
            CommonStatus = 0x48, // CSR
            SimpleI2CStatus = 0x4C, // ISR
            FIFOReceiveStatus = 0x50, // FRSR
            FIFOTransmitStatus = 0x54, // FTSR
            CommonFlagClear = 0x68, // CFCLR
            SimpleI2CFlagClear = 0x6C, // ICFCLR
            FIFOFlagClear = 0x70 // FFCLR
        }
    }
}
