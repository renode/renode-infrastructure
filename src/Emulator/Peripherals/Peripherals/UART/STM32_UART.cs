//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class STM32_UART : BasicDoubleWordPeripheral, IUART
    {
        public STM32_UART(IMachine machine, uint frequency = 8000000) : base(machine)
        {
            this.frequency = frequency;
            DefineRegisters();
        }

        public void WriteChar(byte value)
        {
            if(!usartEnabled.Value && !receiverEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Received a character, but the receiver is not enabled, dropping.");
                return;
            }
            receiveFifo.Enqueue(value);
            readFifoNotEmpty.Value = true;

            if(BaudRate == 0)
            {
                this.Log(LogLevel.Warning, "Unknown baud rate, couldn't trigger the idle line interrupt");
            }
            else
            {
                // Setup a timeout of 1 UART frame (8 bits) for Idle line detection
                idleLineDetectedCancellationTokenSrc?.Cancel();

                var idleLineIn = (8 * 1000000) / BaudRate;
                idleLineDetectedCancellationTokenSrc = new CancellationTokenSource();
                machine.ScheduleAction(TimeInterval.FromMicroseconds(idleLineIn), _ => ReportIdleLineDetected(idleLineDetectedCancellationTokenSrc.Token), name: $"{nameof(STM32_UART)} Idle line detected");
            }

            Update();
        }

        public override void Reset()
        {
            base.Reset();
            idleLineDetectedCancellationTokenSrc?.Cancel();
            receiveFifo.Clear();
            IRQ.Set(false);
        }

        public uint BaudRate
        {
            get
            {
                //OversamplingMode.By8 means we ignore the oldest bit of dividerFraction.Value
                var fraction = oversamplingMode.Value == OversamplingMode.By16 ? dividerFraction.Value : dividerFraction.Value & 0b111;

                var divisor = 8 * (2 - (int)oversamplingMode.Value) * (dividerMantissa.Value + fraction / 16.0);
                return divisor == 0 ? 0 : (uint)(frequency / divisor);
            }
        }

        public Bits StopBits
        {
            get
            {
                switch(stopBits.Value)
                {
                case StopBitsValues.Half:
                    return Bits.Half;
                case StopBitsValues.One:
                    return Bits.One;
                case StopBitsValues.OneAndAHalf:
                    return Bits.OneAndAHalf;
                case StopBitsValues.Two:
                    return Bits.Two;
                default:
                    throw new ArgumentException("Invalid stop bits value");
                }
            }
        }

        public Parity ParityBit => parityControlEnabled.Value ?
                                    (paritySelection.Value == ParitySelection.Even ?
                                        Parity.Even :
                                        Parity.Odd) :
                                    Parity.None;

        public GPIO IRQ { get; } = new GPIO();

        [field: Transient]
        public event Action<byte> CharReceived;

        private void DefineRegisters()
        {
            Register.Status.Define(this, 0xC0, name: "USART_SR")
                .WithTaggedFlag("PE", 0)
                .WithTaggedFlag("FE", 1)
                .WithTaggedFlag("NF", 2)
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "ORE") // we assume no receive overruns
                .WithFlag(4, out idleLineDetected, FieldMode.Read, name: "IDLE")
                .WithFlag(5, out readFifoNotEmpty, FieldMode.Read | FieldMode.WriteZeroToClear, name: "RXNE") // as these two flags are WZTC, we cannot just calculate their results
                .WithFlag(6, out transmissionComplete, FieldMode.Read | FieldMode.WriteZeroToClear, name: "TC")
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => true, name: "TXE") // we always assume "transmit data register empty"
                .WithTaggedFlag("LBD", 8)
                .WithTaggedFlag("CTS", 9)
                .WithReservedBits(10, 22)
                .WithWriteCallback((_, __) => Update())
            ;
            Register.Data.Define(this, name: "USART_DR")
                .WithValueField(0, 9, valueProviderCallback: _ =>
                    {
                        uint value = 0;

                        // "Cleared by a USART_SR register followed by a read to the USART_DR register."
                        // We can assume that USART_SR has already been read on the ISR.
                        idleLineDetected.Value = false;

                        if(receiveFifo.Count > 0)
                        {
                            value = receiveFifo.Dequeue();
                        }
                        readFifoNotEmpty.Value = receiveFifo.Count > 0;
                        Update();
                        return value;
                    }, writeCallback: (_, value) =>
                    {
                        if(!usartEnabled.Value && !transmitterEnabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Trying to transmit a character, but the transmitter is not enabled. dropping.");
                            return;
                        }
                        CharReceived?.Invoke((byte)value);
                        transmissionComplete.Value = true;
                        Update();
                    }, name: "DR"
                )
            ;
            Register.BaudRate.Define(this, name: "USART_BRR")
                .WithValueField(0, 4, out dividerFraction, name: "DIV_Fraction")
                .WithValueField(4, 12, out dividerMantissa, name: "DIV_Mantissa")
            ;
            Register.Control1.Define(this, name: "USART_CR1")
                .WithTaggedFlag("SBK", 0)
                .WithTaggedFlag("RWU", 1)
                .WithFlag(2, out receiverEnabled, name: "RE")
                .WithFlag(3, out transmitterEnabled, name: "TE")
                .WithFlag(4, out idleLineDetectedInterruptEnabled, name: "IDLEIE")
                .WithFlag(5, out receiverNotEmptyInterruptEnabled, name: "RXNEIE")
                .WithFlag(6, out transmissionCompleteInterruptEnabled, name: "TCIE")
                .WithFlag(7, out transmitDataRegisterEmptyInterruptEnabled, name: "TXEIE")
                .WithTaggedFlag("PEIE", 8)
                .WithEnumField(9, 1, out paritySelection, name: "PS")
                .WithFlag(10, out parityControlEnabled, name: "PCE")
                .WithTaggedFlag("WAKE", 11)
                .WithTaggedFlag("M", 12)
                .WithFlag(13, out usartEnabled, name: "UE")
                .WithReservedBits(14, 1)
                .WithEnumField(15, 1, out oversamplingMode, name: "OVER8")
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) =>
                {
                    if(!receiverEnabled.Value || !usartEnabled.Value)
                    {
                        idleLineDetectedCancellationTokenSrc?.Cancel();
                    }
                    Update();
                })
            ;
            Register.Control2.Define(this, name: "USART_CR2")
                .WithTag("ADD", 0, 4)
                .WithReservedBits(5, 1)
                .WithTaggedFlag("LBDIE", 6)
                .WithReservedBits(7, 1)
                .WithTaggedFlag("LBCL", 8)
                .WithTaggedFlag("CPHA", 9)
                .WithTaggedFlag("CPOL", 10)
                .WithTaggedFlag("CLKEN", 11)
                .WithEnumField(12, 2, out stopBits, name: "STOP")
                .WithTaggedFlag("LINEN", 14)
                .WithReservedBits(15, 17)
            ;
        }

        private void ReportIdleLineDetected(CancellationToken ct)
        {
            if(!ct.IsCancellationRequested)
            {
                idleLineDetected.Value = true;
                Update();
            }
        }

        private void Update()
        {
            IRQ.Set(
                (idleLineDetectedInterruptEnabled.Value && idleLineDetected.Value) ||
                (receiverNotEmptyInterruptEnabled.Value && readFifoNotEmpty.Value) ||
                (transmitDataRegisterEmptyInterruptEnabled.Value) || // TXE is assumed to be true
                (transmissionCompleteInterruptEnabled.Value && transmissionComplete.Value)
            );
        }

        private readonly uint frequency;

        private CancellationTokenSource idleLineDetectedCancellationTokenSrc;

        private IEnumRegisterField<OversamplingMode> oversamplingMode;
        private IEnumRegisterField<StopBitsValues> stopBits;
        private IFlagRegisterField usartEnabled;
        private IFlagRegisterField parityControlEnabled;
        private IEnumRegisterField<ParitySelection> paritySelection;
        private IFlagRegisterField transmissionCompleteInterruptEnabled;
        private IFlagRegisterField transmitDataRegisterEmptyInterruptEnabled;
        private IFlagRegisterField idleLineDetectedInterruptEnabled;
        private IFlagRegisterField receiverNotEmptyInterruptEnabled;
        private IFlagRegisterField receiverEnabled;
        private IFlagRegisterField transmitterEnabled;
        private IFlagRegisterField idleLineDetected;
        private IFlagRegisterField readFifoNotEmpty;
        private IFlagRegisterField transmissionComplete;
        private IValueRegisterField dividerMantissa;
        private IValueRegisterField dividerFraction;

        private readonly Queue<byte> receiveFifo = new Queue<byte>();

        private enum OversamplingMode
        {
            By16 = 0,
            By8 = 1
        }

        private enum StopBitsValues
        {
            One = 0,
            Half = 1,
            Two = 2,
            OneAndAHalf = 3
        }

        private enum ParitySelection
        {
            Even = 0,
            Odd = 1
        }

        private enum Register : long
        {
            Status = 0x00,
            Data = 0x04,
            BaudRate = 0x08,
            Control1 = 0x0C,
            Control2 = 0x10,
            Control3 = 0x14,
            GuardTimeAndPrescaler = 0x18
        }
    }
}
