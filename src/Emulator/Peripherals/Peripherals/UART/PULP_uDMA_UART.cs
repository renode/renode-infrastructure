//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    public class PULP_uDMA_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public PULP_uDMA_UART(IMachine machine) : base(machine)
        {
            sysbus = machine.GetSystemBus(this);
            TxIRQ = new GPIO();
            RxIRQ = new GPIO();

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.RxBaseAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxBufferAddress, name: "RX_SADDR / Rx buffer base address")
                },
                {(long)Registers.RxSize, new DoubleWordRegister(this)
                    .WithValueField(0, 17, out rxBufferSize, name: "RX_SIZE / Rx buffer size")
                },
                {(long)Registers.RxConfig, new DoubleWordRegister(this)
                    .WithFlag(4, name: "EN / RX channel enable and start",
                              // Continuous mode is currently not supported
                              valueProviderCallback: _ => rxStarted,
                              writeCallback: (_, value) =>
                              {
                                  rxStarted = value;
                                  if(value)
                                  {
                                      rxIdx = 0;
                                      if(Count > 0)
                                      {
                                          // With the new round of reception we might still have some characters in the
                                          // buffer.
                                          CharWritten();
                                      }
                                  }
                              })
                },
                {(long)Registers.TxBaseAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txBufferAddress, name: "TX_SADDR / Tx buffer base address")
                },
                {(long)Registers.TxSize, new DoubleWordRegister(this)
                    .WithValueField(0, 17, out txBufferSize, name: "TX_SIZE / Tx buffer size")
                },
                {(long)Registers.TxConfig, new DoubleWordRegister(this)
                    .WithFlag(4, name: "EN / TX channel enable and start",
                              // Continuous mode is currently not supported
                              valueProviderCallback: _ => false,
                              writeCallback: (_, value) =>
                              {
                                  if(!value)
                                  {
                                      return;
                                  }

                                  if(txBufferSize.Value == 0)
                                  {
                                      this.Log(LogLevel.Warning, "TX is being enabled, but the buffer size is not configured. Ignoring the operation");
                                      return;
                                  }

                                  var data = sysbus.ReadBytes(txBufferAddress.Value, (int)txBufferSize.Value);
                                  foreach(var c in data)
                                  {
                                      TransmitCharacter(c);
                                  }
                                  TxIRQ.Blink();
                              })
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "TX busy") // tx is never busy
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "RX busy") // rx is never busy
                },
                {(long)Registers.Setup, new DoubleWordRegister(this)
                    .WithFlag(0, out parityEnable, name: "PARITY_ENA / Parity Enable")
                    .WithTag("BIT_LENGTH / Character length", 1, 2)
                    .WithFlag(3, out stopBits, name: "STOP_BITS / Stop bits length")
                    .WithTag("POLLING_EN / Polling Enabled", 4, 1)
                    .WithTag("CLEAN_FIFO / Clean RX FIFO", 5, 1)
                    .WithTag("TX_ENA / TX enabled", 8, 1)
                    .WithTag("RX_ENA / RX enabled", 9, 1)
                    .WithTag("CLKDIV / Clock divider", 16, 16)
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            rxIdx = 0;
            rxStarted = false;
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x80;

        public GPIO TxIRQ { get; }
        public GPIO RxIRQ { get; }

        public override Bits StopBits => stopBits.Value ? Bits.Two : Bits.One;
        public override Parity ParityBit => parityEnable.Value ? Parity.Even : Parity.None;

        public override uint BaudRate => 115200;

        protected override void CharWritten()
        {
            if(!rxStarted)
            {
                this.Log(LogLevel.Warning, "Received byte, but the RX is not started - ignoring it");
                return;
            }

            if(!TryGetCharacter(out var c))
            {
                this.Log(LogLevel.Error, "CharWritten called, but there is no data in the buffer. This might indicate a bug in the model");
                return;
            }

            if(rxIdx >= rxBufferSize.Value)
            {
                // this might happen when rxBufferSize is not initiated properly
                this.Log(LogLevel.Warning, "Received byte {0} (0x{0:X}), but there is no more space in the buffer - ignoring it", (char)c, c);
                return;
            }

            sysbus.WriteByte(rxBufferAddress.Value + rxIdx, c);
            rxIdx++;

            if(rxIdx == rxBufferSize.Value)
            {
                RxIRQ.Blink();
                rxStarted = false;
            }
        }

        protected override void QueueEmptied()
        {
            // Intentionally left blank
        }

        private uint rxIdx;
        private bool rxStarted;

        private readonly IBusController sysbus;
        private readonly DoubleWordRegisterCollection registers;

        private IFlagRegisterField parityEnable;
        private IFlagRegisterField stopBits;
        private IValueRegisterField txBufferAddress;
        private IValueRegisterField txBufferSize;
        private IValueRegisterField rxBufferAddress;
        private IValueRegisterField rxBufferSize;

        private enum Registers : long
        {
            RxBaseAddress = 0x00,
            RxSize        = 0x04,
            RxConfig      = 0x08,
            TxBaseAddress = 0x10,
            TxSize        = 0x14,
            TxConfig      = 0x18,
            Status        = 0x20,
            Setup         = 0x24,
            Error         = 0x28,
            IrqEnable     = 0x2c,
            Valid         = 0x30,
            Data          = 0x34,
        }
    }
}
