//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public sealed class MPC5567_UART : UARTBase, IDoubleWordPeripheral, IWordPeripheral, IKnownSize
    {
        public MPC5567_UART(IMachine machine) : base(machine)
        {
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((LongRegister)offset)
            {
            case LongRegister.Control1:
                return controlRegister1;
            case LongRegister.Status:
                var status = (uint)Status.TransmitDataRegisterEmpty | (uint)Status.TransmitComplete;
                if(Count > 0)
                {
                    status |= (uint)Status.ReceiveDataRegisterFull;
                }
                return status;
            case LongRegister.LINControl:
                break;
            case LongRegister.LINTransmit:
                break;
            case LongRegister.LINReceive:
                break;
            case LongRegister.LINCRCPolynomial:
                break;
            default:
                this.LogUnhandledRead(offset);
                break;
            }
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((LongRegister)offset)
            {
            case LongRegister.Control1:
                controlRegister1 = value;
                break;
            case LongRegister.Status:
                break;
            case LongRegister.LINControl:
                break;
            case LongRegister.LINTransmit:
                break;
            case LongRegister.LINReceive:
                break;
            case LongRegister.LINCRCPolynomial:
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public ushort ReadWord(long offset)
        {
            switch((ShortRegister)offset)
            {
            case ShortRegister.Control2:
                return controlRegister2;
            case ShortRegister.Data:
                byte result;
                TryGetCharacter(out result);
                return result;
            default:
                this.LogUnhandledRead(offset);
                break;
            }
            return 0;
        }

        public void WriteWord(long offset, ushort value)
        {
            switch((ShortRegister)offset)
            {
            case ShortRegister.Control2:
                controlRegister2 = value;
                break;
            case ShortRegister.Data:
                TransmitCharacter((byte)value);
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public new void Reset()
        {
            base.Reset();
            controlRegister1 = 1 << 18; //default baud rate divider
            controlRegister2 = 1 << StopDMAOnError;
        }

        public override Bits StopBits
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override Parity ParityBit
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override uint BaudRate
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public long Size
        {
            get
            {
                return 0x4000;
            }
        }

        protected override void CharWritten()
        {
			
        }

        protected override void QueueEmptied()
        {
            
        }

        private uint controlRegister1;
        private ushort controlRegister2;

        private const int StopDMAOnError = 13;

        [RegisterMapper.RegistersDescription]
        private enum LongRegister
        {
            Control1 = 0x0,
            Status = 0x8,
            LINControl = 0xC,
            LINTransmit = 0x10,
            LINReceive = 0x14,
            LINCRCPolynomial = 0x18
        }

        [RegisterMapper.RegistersDescription]
        private enum ShortRegister
        {
            Control2 = 0x4,
            Data = 0x6
        }

        [Flags]
        private enum Status : uint
        {
            LINFrameComplete = 1u << 8,
            ChecksumError = 1u << 9,
            CRCError = 1u << 10,
            ReceiveDataRegisterFull = 1u << 29,
            TransmitComplete = 1u << 30,
            TransmitDataRegisterEmpty = 1u << 31
        }

    }
}

