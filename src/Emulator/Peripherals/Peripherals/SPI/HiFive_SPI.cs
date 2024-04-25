//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class HiFive_SPI : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public HiFive_SPI(IMachine machine, bool isFlashEnabled = false, int numberOfSupportedSlaves = 1) : base(machine)
        {
            if(numberOfSupportedSlaves < 1 || numberOfSupportedSlaves > 32)
            {
                throw new ConstructionException($"Wrong number of supported slaves: {numberOfSupportedSlaves}. Provide a value in range from 1 to 32.");
            }

            this.csWidth = numberOfSupportedSlaves;
            receiveQueue = new Queue<byte>();
            IRQ = new GPIO();

            var registersMap = new Dictionary<long, DoubleWordRegister>()
            {
                {(long)Registers.SerialClockDivisor, new DoubleWordRegister(this, 0x3)
                    .WithValueField(0, 12, name: "div")
                    .WithReservedBits(12, 20)
                },
                {(long)Registers.SerialClockMode, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0, name: "pha")
                    .WithFlag(1, name: "pol")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.ChipSelectId, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out selectedSlave, name: "csid", writeCallback: (previousValue, value) =>
                    {
                        if(!ChildCollection.ContainsKey(checked((int)value)))
                        {
                            this.Log(LogLevel.Warning, "Selected pin {0}, but there is no device connected to it.", value);
                        }

                        if(previousValue != value)
                        {
                            FinishTransmission();
                        }
                        else
                        {
                            ClearReceiveQueue();
                        }
                    })
                },

                {(long)Registers.ChipSelectDefault, new DoubleWordRegister(this, (1u << numberOfSupportedSlaves) - 1)
                    // this field's width and reset value depend on a constructor parameter `numberOfSupportedSlaves`
                    .WithValueField(0, numberOfSupportedSlaves, name: "csdef")
                },

                {(long)Registers.ChipSelectMode, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, ChipSelectMode>(0, 2, name: "csmode",
                        writeCallback: (_, val) =>
                        {
                            // means off
                            if(val == ChipSelectMode.Auto)
                            {
                                FinishTransmission();
                            }
                        })
                },

                {(long)Registers.FrameFormat, new DoubleWordRegister(this, 0x80000)
                    .WithValueField(0, 2, out spiProtocol, name: "proto", writeCallback: (oldValue, value) =>
                    {
                        if(value != 0x0)
                        {
                            this.WarningLog("Only single SPI protocol (value 0x0) is supported");
                            spiProtocol.Value = oldValue;
                        }
                    })
                    .WithFlag(2, out spiEndianess, name: "endian", writeCallback: (oldValue, value) =>
                    {
                        if(value)
                        {
                            this.WarningLog("Only transmitting MSB first (value 0x0) is supported");
                            spiEndianess.Value = oldValue;
                        }
                    })
                    .WithFlag(3, out spiIODirection, name: "dir", writeCallback: (oldValue, value) =>
                    {
                        if(value)
                        {
                            this.WarningLog("Only Rx direction (value 0x0) is supported");
                            spiIODirection.Value = oldValue;
                        }
                    })
                    .WithReservedBits(4, 12)
                    .WithValueField(16, 4, out numberOfBitsPerFrame, name: "len", writeCallback: (oldValue, value) =>
                    {
                        if(value != 0x8)
                        {
                            this.WarningLog("Only 8 bits per frame (value 0x8) are supported");
                            numberOfBitsPerFrame.Value = oldValue;
                        }
                    })
                    .WithReservedBits(20, 12)
                },

                {(long)Registers.TxFifoData, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) => HandleFifoWrite((byte)value), name: "data")
                    .WithReservedBits(8, 23)
                    .WithFlag(31, FieldMode.Read, valueProviderCallback: _ => false, name: "full")
                },

                {(long)Registers.RxFifoData, new DoubleWordRegister(this, 0x0)
                    // According to the documentation this registers is divided into two fields and a reserved block.
                    // I decided not to split it because the value of both fields must be evaluated IN PROPER ORDER
                    // (flag before dequeuing) and currently the API does not guarantee that.
                    .WithValueField(0, 32, FieldMode.Read, name: "data+empty", valueProviderCallback: _ =>
                    {
                        var result = (receiveQueue.Count == 0)
                            ? FifoEmptyMarker
                            : receiveQueue.Dequeue();

                        UpdateInterrupts();
                        return result;
                    })
                },

                {(long)Registers.TxFifoWatermark, new DoubleWordRegister(this, isFlashEnabled ? 0x1 : 0x0u)
                    .WithValueField(0, 3, out transmitWatermark, writeCallback: (_, __) => UpdateInterrupts(), name: "txmark")
                    .WithReservedBits(3, 29)
                },

                {(long)Registers.RxFifoWatermark, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 3, out receiveWatermark, writeCallback: (_, __) => UpdateInterrupts(), name: "rxmark")
                    .WithReservedBits(3, 29)
                },

                {(long)Registers.InterruptEnable, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0, out transmitWatermarkInterruptEnable, writeCallback: (_, __) => UpdateInterrupts(), name: "txwm")
                    .WithFlag(1, out receiveWatermarkInterruptEnable, writeCallback: (_, __) => UpdateInterrupts(), name: "rxwm")
                    .WithReservedBits(2, 30)
                },

                {(long)Registers.InterruptPending, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0, out transmitWatermarkPending, FieldMode.Read, name: "txwm")
                    .WithFlag(1, out receiveWatermarkPending, FieldMode.Read, name: "rxwm")
                    .WithReservedBits(2, 30)
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Register(ISPIPeripheral peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            if(registrationPoint.Address < 0 || registrationPoint.Address > csWidth - 1)
            {
                throw new RecoverableException($"Wrong registration point: {registrationPoint}. Provide address in range from 0 to {csWidth - 1}.");
            }

            base.Register(peripheral, registrationPoint);
        }

        public override void Reset()
        {
            registers.Reset();
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

        public long Size => 0x78;

        public GPIO IRQ { get; private set; }

        private void HandleFifoWrite(byte value)
        {
            receiveQueue.Enqueue(!TryGetByAddress((int)selectedSlave.Value, out var slavePeripheral)
                ? (byte)0
                : slavePeripheral.Transmit(value));

            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            transmitWatermarkPending.Value = (transmitWatermark.Value > 0);
            receiveWatermarkPending.Value = (receiveQueue.Count > (int)receiveWatermark.Value);

            IRQ.Set((transmitWatermarkInterruptEnable.Value && transmitWatermarkPending.Value)
                || (receiveWatermarkInterruptEnable.Value && receiveWatermarkPending.Value));
        }

        private void ClearReceiveQueue()
        {
            if(receiveQueue.Count > 0)
            {
                this.Log(LogLevel.Warning, "Clearing receive queue, but there are still {0} bytes there that will be lost...", receiveQueue.Count);
            }

            receiveQueue.Clear();
            UpdateInterrupts();
        }

        private void FinishTransmission()
        {
            if(TryGetByAddress((int)selectedSlave.Value, out var slavePeripheral))
            {
                slavePeripheral.FinishTransmission();
            }

            ClearReceiveQueue();
        }

        private readonly int csWidth;
        private readonly Queue<byte> receiveQueue;
        private readonly IFlagRegisterField transmitWatermarkInterruptEnable;
        private readonly IFlagRegisterField receiveWatermarkInterruptEnable;
        private readonly IFlagRegisterField transmitWatermarkPending;
        private readonly IFlagRegisterField receiveWatermarkPending;
        private readonly IValueRegisterField transmitWatermark;
        private readonly IValueRegisterField receiveWatermark;
        private readonly IValueRegisterField selectedSlave;
        private readonly IValueRegisterField spiProtocol;
        private readonly IFlagRegisterField spiEndianess;
        private readonly IFlagRegisterField spiIODirection;
        private readonly IValueRegisterField numberOfBitsPerFrame;
        private readonly DoubleWordRegisterCollection registers;

        private const uint FifoEmptyMarker = 0x80000000;

        private enum Registers
        {
            SerialClockDivisor = 0x0,
            SerialClockMode = 0x4,
            // 0x8, 0xC - reserved
            ChipSelectId = 0x10,
            ChipSelectDefault = 0x14,
            ChipSelectMode = 0x18,
            // 0x1C, 0x20, 0x24 - reserved
            DelayControl0 = 0x28,
            DelayControl1 = 0x2C,
            // 0x30, 0x34, 0x38, 0x3C - reserved
            FrameFormat = 0x40,
            // 0x44 - reserved
            TxFifoData = 0x48,
            RxFifoData = 0x4C,
            TxFifoWatermark = 0x50,
            RxFifoWatermark = 0x54,
            // 0x58, 0x5C - reserved
            FlashInterfaceControl = 0x60,
            FlashInstructionFormat = 0x64,
            // 0x68, 0x6C - reserved
            InterruptEnable = 0x70,
            InterruptPending = 0x74
        }

        private enum ChipSelectMode
        {
            Auto = 0x0, // Assert/deassert CS at the beginning/end of each frame
            // 0x1 is undefined in the documentation
            Hold = 0x2, // Keep CS continously asserted after the initial frame
            Off = 0x3 // Disable hardware control of the CS pin
        }
    }
}
