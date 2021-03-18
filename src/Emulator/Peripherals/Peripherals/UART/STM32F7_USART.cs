//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)] 
    public sealed class STM32F7_USART : BasicDoubleWordPeripheral, IKnownSize, IUART
    {
        public STM32F7_USART(Machine machine) : base(machine)
        {
            IRQ = new GPIO();
            receiveQueue = new Queue<byte>();
            DefineRegisters();
        }

        public override void Reset()
        {
            receiveQueue.Clear();
            IRQ.Unset();
        }

        public void WriteChar(byte value)
        {
            if(receiveEnabled.Value && enabled.Value)
            {
                receiveQueue.Enqueue(value);
                UpdateInterrupt();
            }
            else
            {
                this.Log(LogLevel.Warning, "Char was received, but the receiver (or the whole USART) is not enabled. Ignoring.");
            }
        }

        public uint BaudRate => ApbClock / baudRateDivisor.Value;
        
        public Bits StopBits
        {
            get
            {
                switch(stopBits.Value)
                {
                case 0:
                    return Bits.One;
                case 1:
                    return Bits.Half;
                case 2:
                    return Bits.Two;
                case 3:
                    return Bits.OneAndAHalf;
                default:
                    throw new InvalidOperationException("Should not reach here.");
                }
            }
        }

        public Parity ParityBit
        {
            get
            {
                if(!parityControlEnabled.Value)
                {
                    return Parity.None;
                }
                return paritySelection.Value ? Parity.Odd : Parity.Even;
            }
        }

        [field: Transient]
        public event Action<byte> CharReceived;

        public GPIO IRQ { get; }

        public long Size => 0x400;
        
        private void DefineRegisters()
        {
            Register.ControlRegister1.Define(this)
                .WithFlag(0, out enabled, name: "UE")
                .WithTaggedFlag("UESM", 1)
                .WithFlag(2, out receiveEnabled, name: "RE")
                .WithFlag(3, out transmitEnabled, name: "TE")
                .WithTaggedFlag("IDLEIE", 4)
                .WithFlag(5, out readRegisterNotEmptyInterruptEnabled, name: "RXNEIE")
                .WithFlag(6, out transferCompleteInterruptEnabled, name: "TCIE")
                .WithFlag(7, out transmitRegisterEmptyInterruptEnabled, name: "TXEIE")
                .WithFlag(8, name: "PEIE")
                .WithFlag(9, out paritySelection, name: "PS")
                .WithFlag(10, out parityControlEnabled, name: "PCE")
                .WithTaggedFlag("WAKE", 11)
                .WithTaggedFlag("MO", 12)
                .WithTaggedFlag("MME", 13)
                .WithTaggedFlag("CMIE", 14)
                .WithTaggedFlag("OVER8", 15)
                .WithTag("DEDT", 16, 5)
                .WithTag("DEAT", 21, 5)
                .WithTaggedFlag("RTOIE", 26)
                .WithTaggedFlag("EOBIE", 27)
                .WithTaggedFlag("M1", 28)
                .WithReservedBits(29, 3)
                .WithWriteCallback((_, __) => UpdateInterrupt());

            Register.ControlRegister2.Define(this)
                .WithReservedBits(0, 4)
                .WithTaggedFlag("ADDM7", 4)
                .WithTaggedFlag("LBDL", 5)
                .WithTaggedFlag("LBDIE", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("LBCL", 8)
                .WithTaggedFlag("CPHA", 9)
                .WithTaggedFlag("CPOL", 10)
                .WithTaggedFlag("CLKEN", 11)
                .WithValueField(12, 2, out stopBits)
                .WithTaggedFlag("LINEN", 14)
                .WithTaggedFlag("SWAP", 15)
                .WithTaggedFlag("RXINV", 16)
                .WithTaggedFlag("TXINV", 17)
                .WithTaggedFlag("DATAINV", 18)
                .WithTaggedFlag("MSBFIRST", 19)
                .WithTaggedFlag("ABREN", 20)
                .WithTag("ABRMOD", 21, 2)
                .WithTaggedFlag("RTOEN", 23)
                .WithTag("ADD", 24, 8);

            Register.ControlRegister3.Define(this)
                .WithTaggedFlag("EIE", 0)
                .WithTaggedFlag("IREN", 1)
                .WithTaggedFlag("IRLP", 2)
                .WithTaggedFlag("HDSEL", 3)
                .WithTaggedFlag("NACK", 4)
                .WithTaggedFlag("SCEN", 5)
                .WithTaggedFlag("DMAR", 6)
                .WithTaggedFlag("DMAT", 7)
                .WithTaggedFlag("RTSE", 8)
                .WithTaggedFlag("CTSE", 9)
                .WithTaggedFlag("CTSIE", 10)
                .WithTaggedFlag("ONEBIT", 11)
                .WithTaggedFlag("OVRDIS", 12)
                .WithTaggedFlag("DDRE", 13)
                .WithTaggedFlag("DEM", 14)
                .WithTaggedFlag("DEP", 15)
                .WithReservedBits(16, 1)
                .WithTag("SCARCNT", 17, 3)
                .WithTag("WUS", 20, 2)
                .WithTaggedFlag("WUFIE", 22)
                .WithReservedBits(23, 9);

            Register.BaudRate.Define(this)
                .WithValueField(0, 16, out baudRateDivisor, name: "BRR")
                .WithReservedBits(16, 16);

            Register.InterruptAndStatus.Define(this, 0x200000C0)
                .WithTaggedFlag("PE", 0)
                .WithTaggedFlag("FE", 1)
                .WithTaggedFlag("NF", 2)
                .WithTaggedFlag("ORE", 3)
                .WithTaggedFlag("IDLE", 4)
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => (receiveQueue.Count != 0), name: "RXNE")
                .WithFlag(6, out transferComplete, FieldMode.Read, name: "TC")
                .WithFlag(7, FieldMode.Read, name: "TXE", valueProviderCallback: _ => true)
                .WithTaggedFlag("LBDF", 8)
                .WithTaggedFlag("CTSIF", 9)
                .WithTaggedFlag("CTS", 10)
                .WithTaggedFlag("RTOF", 11)
                .WithTaggedFlag("EOBF", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("ABRE", 14)
                .WithTaggedFlag("ABRF", 15)
                .WithTaggedFlag("BUSY", 16)
                .WithTaggedFlag("CMF", 17)
                .WithTaggedFlag("SBKF", 18)
                .WithTaggedFlag("RWU", 19)
                .WithTaggedFlag("WUF", 20)
                .WithFlag(21, FieldMode.Read, name: "TEACK", valueProviderCallback: _ => transmitEnabled.Value)
                .WithFlag(22, FieldMode.Read, name: "REACK", valueProviderCallback: _ => receiveEnabled.Value)
                .WithReservedBits(23, 8);

            Register.InterruptFlagClear.Define(this)
                .WithTaggedFlag("PECF", 0)
                .WithTaggedFlag("FECF", 1)
                .WithTaggedFlag("NCF", 2)
                .WithTaggedFlag("ORECF", 3)
                .WithTaggedFlag("IDLECF", 4)
                .WithReservedBits(5, 1)
                // this flag is clear on read or write 1
                .WithFlag(6, name: "TCCF",
                    readCallback: (_, __) => { transferComplete.Value = false; },
                    writeCallback: (_, val) => { if(val) transferComplete.Value = false; })
                .WithReservedBits(7, 1)
                .WithTaggedFlag("LBDCF", 8)
                .WithTaggedFlag("CTSCF", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("RTOCF", 11)
                .WithTaggedFlag("EOBCF", 12)
                .WithReservedBits(13, 4)
                .WithTaggedFlag("CMCF", 17)
                .WithReservedBits(18, 2)
                .WithTaggedFlag("WUCF", 20)
                .WithReservedBits(21, 11)
                .WithWriteCallback((_, __) => UpdateInterrupt());

            Register.ReceiveData.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => HandleReceiveData(), name: "RDR")
                .WithReservedBits(8, 24);

            Register.TransmitData.Define(this)
                .WithValueField(0, 8, 
                    // reading this register will intentionally return the last written value
                    writeCallback: (_, val) => HandleTransmitData(val), name: "TDR")
                .WithReservedBits(8, 24);
        }

        private void HandleTransmitData(uint value)
        {
            if(transmitEnabled.Value && enabled.Value)
            {
                CharReceived?.Invoke((byte)value);
                transferComplete.Value = true;
                UpdateInterrupt();
            }
            else
            {
                this.Log(LogLevel.Warning, "Char was to be sent, but the transmitter (or the whole USART) is not enabled. Ignoring.");
            }
        }

        private uint HandleReceiveData()
        {
            var result = receiveQueue.Dequeue();
            UpdateInterrupt();
            return result;
        }

        private void UpdateInterrupt()
        {
            var transmitRegisterEmptyInterrupt = transmitRegisterEmptyInterruptEnabled.Value; // we assume that transmit register is always empty
            var transferCompleteInterrupt = transferComplete.Value && transferCompleteInterruptEnabled.Value;
            var readRegisterNotEmptyInterrupt = (receiveQueue.Count != 0) && readRegisterNotEmptyInterruptEnabled.Value;
            
            IRQ.Set(transmitRegisterEmptyInterrupt || transferCompleteInterrupt || readRegisterNotEmptyInterrupt);
        }

        private IFlagRegisterField parityControlEnabled;
        private IFlagRegisterField paritySelection;
        private IFlagRegisterField transmitRegisterEmptyInterruptEnabled;
        private IFlagRegisterField transferCompleteInterruptEnabled;
        private IFlagRegisterField transferComplete;
        private IFlagRegisterField readRegisterNotEmptyInterruptEnabled;
        private IFlagRegisterField transmitEnabled;
        private IFlagRegisterField receiveEnabled;
        private IFlagRegisterField enabled;
        private IValueRegisterField baudRateDivisor;
        private IValueRegisterField stopBits;

        private readonly Queue<byte> receiveQueue;

        private const uint ApbClock = 200000000;

        private enum Register
        {
            ControlRegister1   = 0x0,
            ControlRegister2   = 0x4,
            ControlRegister3   = 0x8,
            BaudRate           = 0xC,
            ReceiverTimeout    = 0x14,
            Request            = 0x18,
            InterruptAndStatus = 0x1C,
            InterruptFlagClear = 0x20,
            ReceiveData        = 0x24,
            TransmitData       = 0x28,
        }
    }
}

