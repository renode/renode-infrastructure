//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.UART
{
    // Due to unusual register offsets we cannot use address translations
    public class SAMD20_UART : IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IUART, IKnownSize
    {
        public SAMD20_UART(Machine machine) 
        {
            IRQ = new GPIO();

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ControlA, new DoubleWordRegister(this)
                    .WithFlag(0,
                        writeCallback: (_, val) =>
                        {
                            if(!val)
                            {
                                return;
                            }
                            // According to the docs writing '1' to this bit should reset all registers except Debug Control,
                            // but since that register is a stub, we don't bother.
                            Reset();
                            enabled.Value = false;
                        },
                        valueProviderCallback: _ => false,
                        name: "Software reset (SWRST)")
                    .WithFlag(1, out enabled, name: "Enable (ENABLE)")
                    .WithTag("Operating mode (MODE)", 2, 3)
                    .WithReservedBits(5, 2)
                    .WithTag("Run in standby (RUNSTDBY)", 7, 1)
                    .WithTag("Immediate buffer overflow notification (IBON)", 8, 1)
                    .WithReservedBits(9, 7)
                    .WithTag("Transmit data pinout (TXPO)", 16, 1)
                    .WithReservedBits(17, 3)
                    .WithTag("Receive data pinout (RXPO)", 20, 2)
                    .WithReservedBits(22, 2)
                    .WithEnumField(24, 4, out frameFormat, name: "Frame format (FORM)")
                    .WithTag("Communication mode (CMODE)", 28, 1)
                    .WithTag("Clock polarity (CPOL)", 29, 1)
                    .WithTag("Data order (DORD)", 30, 1)
                    .WithReservedBits(31, 1)
                },

                {(long)Registers.ControlB, new DoubleWordRegister(this)                    
                    .WithTag("Character Size (CHSIZE)", 0, 3)
                    .WithReservedBits(3, 3)
                    .WithFlag(6, out stopBitMode, name: "Stop bit mode (SBMODE)")
                    .WithReservedBits(7, 6)
                    .WithFlag(13, out parityMode, name: "Parity mode (PMODE)")
                    .WithReservedBits(14, 2)
                    .WithFlag(16, out transmitterEnabled, name: "Transmitter enable (TXEN)")
                    .WithFlag(17, out receiverEnabled, name: "Receiver enable (RXEN)")
                    .WithReservedBits(18, 14)
                },

                {(long)Registers.DebugControl, new DoubleWordRegister(this)
                    .WithTag("Debug stop mode (DGBSTOP)", 0, 1)
                    .WithReservedBits(1, 7)
                    .WithIgnoredBits(8, 24)
                },

                {(long)Registers.Baudrate, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out baudrate, name: "Baudrate (BAUD)")
                    .WithIgnoredBits(16, 16)
                },

                {(long)Registers.InterruptEnableClear, new DoubleWordRegister(this)
                    .WithFlag(0, out dataRegisterEmptyInterruptEnabled, FieldMode.Read | FieldMode.WriteOneToClear, name: "Data register empty interrupt disabled (DRE)")
                    .WithFlag(1, out transmitCompleteInterruptEnabled, FieldMode.Read | FieldMode.WriteOneToClear, name: "Transmit complete interrupt disabled (TXC)")
                    .WithFlag(2, out receiveCompleteInterruptEnabled, FieldMode.Read | FieldMode.WriteOneToClear, name: "Receive complete interrupt disabled (RXC)")
                    .WithFlag(3, out receiveStartedInterruptEnabled, FieldMode.Read | FieldMode.WriteOneToClear, name: "Receive start interrupt disabled (RXS)")
                    .WithReservedBits(4, 4)
                    .WithIgnoredBits(8, 24)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },

                {(long)Registers.InterruptEnableSet, new DoubleWordRegister(this)
                    .WithFlag(0,
                        writeCallback: (_, val) => dataRegisterEmptyInterruptEnabled.Value |= val,
                        valueProviderCallback: _ => dataRegisterEmptyInterruptEnabled.Value,
                        name: "Data register empty interrupt enabled (DRE)")
                    .WithFlag(1,
                        writeCallback: (_, val) => transmitCompleteInterruptEnabled.Value |= val,
                        valueProviderCallback: _ => transmitCompleteInterruptEnabled.Value,
                        name: "Transmit complete interrupt enabled (TXC)")
                    .WithFlag(2,
                        writeCallback: (_, val) => receiveCompleteInterruptEnabled.Value |= val,
                        valueProviderCallback: _ => receiveCompleteInterruptEnabled.Value,
                        name: "Receive complete interrupt enabled (RXC)")
                    .WithFlag(3,
                        writeCallback: (_, val) => receiveStartedInterruptEnabled.Value |= val,
                        valueProviderCallback: _ => receiveStartedInterruptEnabled.Value,
                        name: "Receive start interrupt enabled (RXS)")
                    .WithReservedBits(4, 4)
                    .WithIgnoredBits(8, 24)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },

                {(long)Registers.InterruptFlagStatusAndClear, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read,
                        valueProviderCallback: _ => receiveQueue.Count == 0, name: "Data register empty (DRE)")
                    .WithFlag(1, out transmitComplete, FieldMode.WriteOneToClear | FieldMode.Read, name: "Transmit complete (TXC)")
                    .WithFlag(2, FieldMode.Read,
                        valueProviderCallback: _ => receiveQueue.Count > 0, name: "Receive complete (RXC)")
                    .WithTag("Receive start (RXS)", 3, 1)
                    .WithReservedBits(4, 4)
                    .WithIgnoredBits(8, 24)
                },

                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithTag("Parity error (PERR)", 0, 1)
                    .WithTag("Frame error (FERR)", 1, 1)
                    .WithTag("Buffer overflow (BUFOVF)", 2, 1)
                    .WithReservedBits(3, 12)
                    .WithTag("Synchronization busy (SYNCBUSY))", 15, 1)
                    .WithIgnoredBits(16, 16)
                },

                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 9,
                        writeCallback: (_, val) => HandleTransmitData(val),
                        valueProviderCallback: _ => HandleReceiveData(),
                        name: "DATA")
                    .WithReservedBits(9, 7)
                    .WithIgnoredBits(16, 16)
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
            Reset();
        }

        public void Reset()
        {
            registers.Reset();
            receiveQueue.Clear();
            UpdateInterrupts();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)ReadDoubleWord(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteDoubleWord(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return (byte) ReadDoubleWord(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteDoubleWord(offset, value);
        }

        public void WriteChar(byte value)
        {
            if(!enabled.Value || !receiverEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Char was received, but the receiver (or the whole USART) is not enabled. Ignoring.");
                return;
            }
            receiveQueue.Enqueue(value);
            UpdateInterrupts();
        }

        public uint BaudRate => baudrate.Value;

        public Bits StopBits => stopBitMode.Value ? Bits.Two : Bits.One;

        public Parity ParityBit
        {
            get
            {
                if(frameFormat.Value == FrameFormat.WithParity)
                {
                    return parityMode.Value ? Parity.Odd : Parity.Even;
                }
                return Parity.None;
            }
        }

        public GPIO IRQ { get; }

        public event Action<byte> CharReceived;

        public long Size => 0x100;

        private void HandleTransmitData(uint value)
        {
            if(!enabled.Value || !transmitterEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Char was to be sent, but the transmitter (or the whole USART) is not enabled. Ignoring.");
                return;
            }
            CharReceived?.Invoke((byte)value);
            transmitComplete.Value = true;
            UpdateInterrupts();
        }

        private uint HandleReceiveData()
        {
            if(receiveQueue.TryDequeue(out var result))
            {
                UpdateInterrupts();
            }
            return result;
        }

        private void UpdateInterrupts()
        {
            var value = (transmitComplete.Value && transmitCompleteInterruptEnabled.Value)
                || (receiveQueue.Count == 0 && dataRegisterEmptyInterruptEnabled.Value)
                || (receiveQueue.Count > 0 && receiveCompleteInterruptEnabled.Value);
            IRQ.Set(value);
        }

        private readonly IFlagRegisterField dataRegisterEmptyInterruptEnabled;
        private readonly IFlagRegisterField transmitCompleteInterruptEnabled;
        private readonly IFlagRegisterField receiveCompleteInterruptEnabled;
        private readonly IFlagRegisterField receiveStartedInterruptEnabled;
        private readonly IFlagRegisterField enabled;
        private readonly IFlagRegisterField transmitterEnabled;
        private readonly IFlagRegisterField receiverEnabled;
        private readonly IFlagRegisterField transmitComplete;
        private readonly IFlagRegisterField parityMode;
        private readonly IEnumRegisterField<FrameFormat> frameFormat;
        private readonly IFlagRegisterField stopBitMode;

        private readonly IValueRegisterField baudrate;

        private readonly DoubleWordRegisterCollection registers;
        private readonly Queue<byte> receiveQueue = new Queue<byte>();

        private enum FrameFormat : int
        {
            NoParity = 0x0,
            WithParity = 0x1
            // 0x2-0xF Reserved
        }

        private enum Registers : long
        {
            ControlA = 0x00,
            ControlB = 0x04,
            DebugControl = 0x08,
            // 0x9 - reserved
            Baudrate = 0x0A,
            InterruptEnableClear = 0x0C,
            InterruptEnableSet = 0x0D,
            InterruptFlagStatusAndClear = 0x0E,
            // 0xF - reserved
            Status = 0x10,
            // 0x11:0x17 - reserved
            Data = 0x18
        }
    }
}
