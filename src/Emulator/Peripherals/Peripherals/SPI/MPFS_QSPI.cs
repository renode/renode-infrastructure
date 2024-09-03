//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.SPI
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class MPFS_QSPI: NullRegistrationPointPeripheralContainer<Micron_MT25Q>, IDoubleWordPeripheral, IKnownSize
    {
        public MPFS_QSPI(IMachine machine, uint size) : base(machine)
        {
            locker = new object();
            IRQ = new GPIO();

            if(size < MinimumSize)
            {
                throw new ConstructionException($"The {nameof(size)} argument can't be lower than {MinimumSize}.");
            }
            Size = size;

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this, 0x402) //the docs report 0x502, but this lights up a reserved bit.
                    .WithFlag(0, out enabled, name: "ENABLE")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "MASTER")
                    .WithFlag(2, out xipMode, name: "XIP")
                    .WithEnumField<DoubleWordRegister, XIPAddressBytes>(3, 1, name: "XIPADDR")
                    .WithReservedBits(4, 6)
                    .WithTag("CLKIDLE", 10, 1)
                    .WithTag("SAMPLE", 11, 2)
                    .WithTag("QSPIMODE[0]", 13, 1)
                    .WithTag("QSPIMODE[1]", 14, 2)
                    .WithFlag(16, out x4Enabled, name: "FLAGS4X")
                    .WithTag("CLKRATE", 24, 4)
                },

                {(long)Registers.Frames, new DoubleWordRegister(this)
                    .WithValueField(0, 16, writeCallback: (_, value) =>
                        {
                            BitHelper.UpdateWith(ref totalBytes, (uint)value, 0, 16);
                            bytesSent = 0;
                        }, valueProviderCallback: _ => totalBytes, name: "TOTALBYTES")
                    .WithValueField(16, 9, out commandBytes, name: "CMDBYTES")
                    .WithTag("QSPI", 25, 1)
                    .WithValueField(26, 4, writeCallback: (_, value) =>
                        {
                            idleCycles = (uint)value;
                            bytesToSkip = value >= 8 ? 1u : 0;
                            if(value != 0 && value != 8)
                            {
                                this.Log(LogLevel.Warning, "Requested {0} idle cycles, but values other than 0 or 8 might not be properly handled.", value);
                            }
                            totalBytes += bytesToSkip;
                        }, valueProviderCallback: _ => idleCycles, name: "IDLE")
                    .WithFlag(30, valueProviderCallback: _ => false,
                        // If set then the FIFO flags are set to byte mode
                        changeCallback: (_, value) => x4Enabled.Value = false, name: "FLAGBYTE")
                    .WithFlag(31, valueProviderCallback: _ => false,
                        // If set then the FIFO flags are set to word mode
                        changeCallback: (_, value) => x4Enabled.Value = true, name: "FLAGWORD")
                    .WithWriteCallback((_, __) =>
                    {
                        txDone.Value = false;
                        rxDone.Value = false;
                        RefreshInterrupt();
                    })
                },

                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out txDoneInterruptEnabled, name: "TXDONE")
                    .WithFlag(1, out rxDoneInterruptEnabled, name: "RXDONE")
                    .WithFlag(2, out rxAvailableInterruptEnabled, name: "RXAVAILABLE")
                    .WithFlag(3, out txAvailableInterruptEnabled, name: "TXAVAILABLE")
                    .WithFlag(4, out rxFifoEmptyInterruptEnabled, name: "RXFIFOEMPTY")
                    .WithFlag(5, name: "TXFIFOFULL") //we keep the value, but not do anything with it, as this never happens
                    .WithChangeCallback((_, __) => RefreshInterrupt())
                },

                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, out txDone, FieldMode.WriteOneToClear | FieldMode.Read, name: "TXDONE")
                    .WithFlag(1, out rxDone, FieldMode.WriteOneToClear | FieldMode.Read, name: "RXDONE")
                    .WithFlag(2, valueProviderCallback: _ => IsRxAvailable(), name: "RXAVAILABLE")
                    .WithFlag(3, valueProviderCallback: _ => true, name: "TXAVAILABLE")
                    .WithFlag(4, valueProviderCallback: _ => !IsRxAvailable(), name: "RXFIFOEMPTY")
                    .WithFlag(5, valueProviderCallback: _ => false, name: "TXFIFOFULL")
                    .WithReservedBits(6, 1)
                    .WithFlag(7, valueProviderCallback: _ => true, name: "READY")
                    .WithFlag(8, valueProviderCallback: _ => x4Enabled.Value, name: "FLAGSX4")
                    .WithWriteCallback((_, __) => RefreshInterrupt())
                },

                {(long)Registers.UpperAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out upperAddress, name: "ADDRUP")
                },

                {(long)Registers.RxData1, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            lock(locker)
                            {
                                return (receiveFifo.Count > 0 ? receiveFifo.Dequeue() : 0u);
                            }
                        },
                        name: "RXDATA")
                    .WithWriteCallback((_, __) => RefreshInterrupt())
                },

                {(long)Registers.TxData1, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, val) => HandleByte((uint)val), name: "TXDATA")
                },

                {(long)Registers.RxData4, new DoubleWordRegister(this)
                // The documentation is ambiguous on this register.
                // It says 4 bytes must be read from the FIFO, but does not state precisely what happens
                // when there is not enough data. This model ignores the read until there are at least 4 bytes to be read.
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        lock(locker)
                        {
                            if(receiveFifo.Count >= 4)
                            {
                                var value = 0u;
                                for(var i = 0; i < 4; ++i)
                                {
                                    value |= (uint)receiveFifo.Dequeue() << (8 * i);
                                }
                                return value;
                            }
                        }
                        this.Log(LogLevel.Warning, "Trying to read 4 bytes from the receive FIFO, but there are only {0} bytes available.",  receiveFifo.Count);
                        return 0;
                    },
                    name: "RXDATA4")
                    .WithWriteCallback((_, __) => RefreshInterrupt())
                },

                {(long)Registers.TxData4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write,
                        writeCallback: (_, val) =>
                        {
                            for(var i = 0; i < 4; i++)
                            {
                                HandleByte(BitHelper.GetValue((uint)val, i * 8, 8));
                            }
                        },
                        name: "TXDATA4")
                },
                // this register intentionally exposes the whole register for reading and the upper bytes for writing
                {(long)Registers.FramesUpper, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => totalBytes, name: "BYTESLOWER")
                    .WithValueField(16, 16, writeCallback: (_, value) => BitHelper.UpdateWithShifted(ref totalBytes, (uint)value, 16, 16),
                        valueProviderCallback: _ => totalBytes >> 16, name: "BYTESUPPER")
                }
            };
            registers = new DoubleWordRegisterCollection(this, registerMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return xipMode.Value
                ? RegisteredPeripheral.UnderlyingMemory.ReadDoubleWord(BitHelper.SetMaskedValue((uint)offset, (uint)upperAddress.Value, 24, 8))
                : registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            offset %= RegisterAliasSize;
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            registers.Reset();
            bytesSent = 0;
            idleCycles = 0;
            bytesToSkip = 0;
            RefreshInterrupt();
            lock(locker)
            {
                receiveFifo.Clear();
            }
        }

        public GPIO IRQ { get; }
        public long Size { get; }

        private void TryReceive(byte data)
        {
            lock(locker)
            {
                receiveFifo.Enqueue(data);
            }
        }

        /*
         * Method for handling commands sent to flash
         *
         * Every operation will consist of at least 1 byte - the command code.
         *
         * However, we can distinguish 4 types of flash operations based on the relation between
         * number of command address bytes and data bytes (a generalization):
         * 1) 0 address bytes and 0 data bytes
         * 2) 0 address bytes and at least 1 data byte
         * 3) 3 or 4 address bytes and 0 data bytes
         * 4) 3 or 4 address bytes and at least 1 data byte
         *
         * Description of significant variables:
         * a) `commandBytes` = command byte + address bytes
         * b) `totalBytes` = `commandBytes` + data bytes
         * c) `bytesSent` = internal counter of bytes sent
         *
         * We have a data reception command when `commandBytes` and `totalBytes` are not equal,
         * and data transmission when they are equal.
         *
         * If we have a transmission command, we send all `totalBytes` to the flash and store
         * the received data in the receiveFifo.
         *
         * If we have a reception command, we first check how many address bytes we have to send,
         * and then send the command and address bytes; if there are any data bytes in the command,
         * we can send dummy data to the flash and just store the received data in the receiveFifo.
         *
         * If the internal counter `bytesSent` is equal to `totalBytes`, we call the
         * `FinishTransmission()` method on the registered peripheral.
         * We can assume that commands will not be interrupted.
         *
         */
        private void HandleByte(uint val)
        {
            if(enabled.Value)
            {
                // reception
                if(commandBytes.Value != totalBytes)
                {
                    // 1 command byte
                    if(commandBytes.Value == 1)
                    {
                        HandleByteTransmission(val);
                        for(var i = bytesSent; i < totalBytes; i++)
                        {
                            HandleByteReception();
                        }
                    }
                    // 1 command byte + 3 or 4 address bytes
                    else
                    {
                        if(bytesSent < (int)commandBytes.Value)
                        {
                            HandleByteTransmission(val);
                        }
                        if(bytesSent == (int)commandBytes.Value)
                        {
                            for(var i = bytesSent; i < totalBytes; i++)
                            {
                                HandleByteReception();
                            }
                        }
                    }
                    TryHandleInterrupt(rxDone);
                }
                // transmission
                else
                {
                    if(bytesSent < totalBytes)
                    {
                        HandleByteTransmission(val);
                    }
                    TryHandleInterrupt(txDone);
                }
            }
        }

        private void HandleByteTransmission(uint val)
        {
            RegisteredPeripheral.Transmit((byte)val);
            TryFinishTransmission();
        }

        private void HandleByteReception()
        {
            var receivedByte = RegisteredPeripheral.Transmit(0);
            if(bytesToSkip > 0)
            {
                bytesToSkip--;
            }
            else
            {
                TryReceive(receivedByte);
            }
            TryFinishTransmission();
        }

        private void TryFinishTransmission()
        {
            bytesSent++;
            if(bytesSent == totalBytes)
            {
                RegisteredPeripheral.FinishTransmission();
            }
        }

        private void TryHandleInterrupt(IFlagRegisterField field)
        {
            if(bytesSent == totalBytes)
            {
                field.Value = true;
                RefreshInterrupt();
            }
        }

        private void RefreshInterrupt()
        {
            var value = false;
            value |= txDone.Value && txDoneInterruptEnabled.Value;
            value |= rxDone.Value && rxDoneInterruptEnabled.Value;
            value |= IsRxAvailable() && rxAvailableInterruptEnabled.Value;
            value |= txAvailableInterruptEnabled.Value;
            value |= !IsRxAvailable() && rxFifoEmptyInterruptEnabled.Value;

            IRQ.Set(value);
        }

        private bool IsRxAvailable()
        {
            lock(locker)
            {
                return receiveFifo.Count >= (x4Enabled.Value ? 4 : 1);
            }
        }


        private readonly Queue<byte> receiveFifo = new Queue<byte>();
        private readonly DoubleWordRegisterCollection registers;
        private readonly IFlagRegisterField enabled;
        private readonly IFlagRegisterField xipMode;
        private readonly IValueRegisterField commandBytes;
        private readonly IFlagRegisterField x4Enabled;
        private readonly IValueRegisterField upperAddress;
        private readonly IFlagRegisterField txDone;
        private readonly IFlagRegisterField rxDone;
        private readonly IFlagRegisterField txDoneInterruptEnabled;
        private readonly IFlagRegisterField rxDoneInterruptEnabled;
        private readonly IFlagRegisterField rxAvailableInterruptEnabled;
        private readonly IFlagRegisterField txAvailableInterruptEnabled;
        private readonly IFlagRegisterField rxFifoEmptyInterruptEnabled;

        private uint totalBytes;
        private uint bytesToSkip;
        private uint idleCycles;
        private int bytesSent;

        //Registers are aliased every 256 bytes
        private const int RegisterAliasSize = 256;
        private const uint MinimumSize = (uint)Registers.FramesUpper + 4;
        private object locker;

        private enum XIPAddressBytes
        {
            Bytes3 = 0,
            Bytes4 = 1
        }

        private enum Registers
        {
            Control = 0x0,
            Frames = 0x4,
            //0x8 reserved
            InterruptEnable = 0xc,
            Status = 0x10,
            DirectAccess = 0x14,
            UpperAddress = 0x18,
            RxData1 = 0x40,
            TxData1 = 0x44,
            RxData4 = 0x48,
            TxData4 = 0x4c,
            FramesUpper = 0x50,
        }
    }
}
