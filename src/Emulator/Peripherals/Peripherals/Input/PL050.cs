//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.Input
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class PL050 : NullRegistrationPointPeripheralContainer<IPS2Peripheral>, IPS2Controller, IDoubleWordPeripheral, IKnownSize
    {
        public PL050(IMachine machine, int size = 0x1000) : base(machine)
        {
            this.size = size;
            idHelper = new PrimeCellIDHelper(size, new byte[] { 0x50, 0x10, 0x04, 0x00, 0x0D, 0xF0, 0x05, 0xB1 }, this);
            IRQ = new GPIO();
            Reset();
        }

        public long Size
        {
            get
            {
                return size;
            }
        }

        public void Notify()
        {
            IRQ.Set();
        }

        public override void Register(IPS2Peripheral peripheral, NullRegistrationPoint registrationPoint)
        {
            base.Register(peripheral, registrationPoint);
            peripheral.Controller = this;
        }

        public override void Unregister(IPS2Peripheral peripheral)
        {
            base.Unregister(peripheral);
            peripheral.Controller = null;
        }

        public GPIO IRQ { get; private set; }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.Control:
                return controlRegister;
            case Registers.Status:
                return HandleStatusRegister();
            case Registers.Data:
                IRQ.Unset();
                if(RegisteredPeripheral != null)
                {
                    dataRegister = RegisteredPeripheral.Read();
                }
                return dataRegister;
            case Registers.ClockDivisor:
                return clkDivRegister;
            case Registers.InterruptIdentification:
                return IRQ.IsSet ? 1u : 0u;
            default:
                return idHelper.Read(offset);
            }
        }

        public override void Reset()
        {
            dataRegister = 0;
            controlRegister = 0;
            clkDivRegister = 0;
            IRQ.Unset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.Control:
                controlRegister = value;
                break;
            case Registers.Data:
                if(RegisteredPeripheral != null)
                {
                    RegisteredPeripheral.Write((byte)value);
                }
                break;
            case Registers.ClockDivisor:
                clkDivRegister = value;
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        private uint HandleStatusRegister()
        {
            var value = (uint)States.TransmitEmpty;
            // calculates parity, according to http://www-graphics.stanford.edu/~seander/bithacks.html#ParityWith64Bits
            if(((((dataRegister * 0x0101010101010101UL) & 0x8040201008040201UL) % 0x1FF) & 1) == 1)
            {
                value |= (uint)States.Parity;
            }
            if(IRQ.IsSet)
            {
                value |= (uint)States.ReceiveFull;
            }
            return value;
        }

        private readonly PrimeCellIDHelper idHelper;
        private readonly int size;
        private uint controlRegister;
        private uint dataRegister;
        private uint clkDivRegister;

        private enum Registers
        {
            Control = 0,
            Status = 4,
            Data = 8,
            ClockDivisor = 12,
            InterruptIdentification = 16
        }

        private enum States : uint
        {
            Parity = 1 << 2,
            ReceiveFull = 1 << 4,
            TransmitEmpty = 1 << 6
        }
    }
}

