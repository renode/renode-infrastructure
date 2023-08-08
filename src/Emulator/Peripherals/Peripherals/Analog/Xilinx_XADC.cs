//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class Xilinx_XADC : BasicDoubleWordPeripheral, IKnownSize
    {
        public Xilinx_XADC(IMachine machine) : base(machine)
        {
            DefineRegisters();

            IRQ = new GPIO();
            dataFifo = new Queue<uint>();
            channelValues = new ushort[NUMBER_OF_CHANNELS];

            Reset();
        }

        public override void Reset()
        {
            base.Reset();

            dataFifo.Clear();
            Array.Clear(channelValues, 0, NUMBER_OF_CHANNELS);
            IRQ.Unset();
            readDataValue = 0;
        }

        public void SetChannelValue(ushort channel, ushort value)
        {
            var channelIndex = ValidateChannelNumber(channel);
            channelValues[channelIndex] = value;
        }

        public ushort GetChannelValue(ushort channel)
        {
            var channelIndex = ValidateChannelNumber(channel);
            return channelValues[channelIndex];
        }

        public long Size => 0x1C;
        public GPIO IRQ { get; }

        private void DefineRegisters()
        {
            Register.Configuration.Define(this, resetValue: 0x00001114)
                .WithTag("IGAP", 0, 5)
                .WithReservedBits(5, 3)
                .WithTag("TCKRATE", 8, 2)
                .WithReservedBits(10, 2)
                .WithTag("REDGE", 12, 1)
                .WithTag("WEDGE", 13, 1)
                .WithReservedBits(14, 2)
                .WithValueField(16, 4, out dataFifoThreshold, name: "DFIFOTH")
                .WithTag("CFIFOTH", 20, 4)
                .WithReservedBits(24, 7)
                .WithTag("ENABLE", 31, 1);

            interruptStatus = Register.InterruptStatus.Define(this, resetValue: 0x00000200)
                .WithTag("ALM", 0, 7)
                .WithTag("OT", 7, 1)
                .WithFlag(8, out intStatusDfifoGth, FieldMode.Read | FieldMode.WriteOneToClear, name: "DFIFO_GTH")
                .WithTag("CFIFO_LTH", 9, 1)
                .WithReservedBits(10, 22)
                .WithChangeCallback((oldValue, newValue) => UpdateInterrupts());

            interruptMask = Register.InterruptMask.Define(this, resetValue: 0xFFFFFFFF)
                .WithTag("M_ALM", 0, 7)
                .WithTag("M_OT", 7, 1)
                .WithFlag(8, name: "M_DFIFO_GTH")
                .WithTag("M_CFIFO_LTH", 9, 1)
                .WithReservedBits(10, 22)
                .WithWriteCallback((oldValue, newValue) => UpdateInterrupts());

            Register.MiscStatus.Define(this, resetValue: 0x00000500)
                .WithTag("ALM", 0, 7)
                .WithTag("OT", 7, 1)
                .WithFlag(8, FieldMode.Read, valueProviderCallback: (_) => dataFifo.Count == 0, name: "DFIFOE")
                .WithFlag(9, FieldMode.Read, valueProviderCallback: (_) => dataFifo.Count == DATA_FIFO_CAPACITY, name: "DFIFOF")
                // Command FIFO empty - always true because we execute commands immediately after they're written to the FIFO
                .WithFlag(10, FieldMode.Read, valueProviderCallback: (_) => true, name: "CFIFOE")
                // Command FIFO full - always false, see above
                .WithFlag(11, FieldMode.Read, valueProviderCallback: (_) => false, name: "CFIFOF")
                .WithValueField(12, 4, FieldMode.Read, valueProviderCallback: (_) => (uint)dataFifo.Count, name: "DFIFO_LVL")
                .WithTag("CFIFO_LVL", 16, 4)
                .WithReservedBits(20, 12);

            Register.CommmandFifo.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (oldValue, newValue) =>
                {
                    this.DebugLog($"Command FIFO write: 0x{newValue:x}");
                    HandleCommand((uint)newValue);
                });

            Register.DataFifo.Define(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (oldValue) =>
                {
                    if(!dataFifo.TryDequeue(out var value))
                    {
                        this.Log(LogLevel.Warning, "Read from empty data FIFO");
                    }

                    this.DebugLog($"Data FIFO read returning 0x{value:x}");
                    return value;
                });

            Register.MiscControl.Define(this)
                .WithReservedBits(0, 1, 0)
                .WithReservedBits(1, 3)
                .WithTag("RESET", 4, 1)
                .WithReservedBits(5, 27);
        }

        private void HandleCommand(uint command)
        {
            var cmd = (command >> 26) & 0xF;
            var drpAddress = (ushort)((command >> 16) & 0x3FF);
            var drpData = (ushort)((command >> 0) & 0x7FFF);

            switch((PsXadcCommand)cmd)
            {
                case PsXadcCommand.NoOperation:
                    DataFifoSend(readDataValue);
                    readDataValue = 0;
                    break;
                case PsXadcCommand.DrpRead:
                    DataFifoSend(0);
                    readDataValue = HandleDrpRead(drpAddress);
                    break;
                case PsXadcCommand.DrpWrite:
                    DataFifoSend(HandleDrpWrite(drpAddress, drpData));
                    break;
                default:
                    this.Log(LogLevel.Warning, $"Unknown DRP command 0x{cmd:x}");
                    break;
            }
        }

        private uint? AdcChannelNumberToIndex(ushort address)
        {
            if(address >= (ushort)DrpRegister.Temperature && address <= (ushort)DrpRegister.VccBram)
            {
                return (uint)(address - (ushort)DrpRegister.Temperature);
            }

            if(address >= (ushort)DrpRegister.VccPint && address <= (ushort)DrpRegister.VauxpVauxn15)
            {
                return (uint)(address - (ushort)DrpRegister.VccPint)
                    + ((ushort)DrpRegister.VccBram - (ushort)DrpRegister.Temperature + 1);
            }

            return null;
        }

        private uint ValidateChannelNumber(ushort channel)
        {
            if(!(AdcChannelNumberToIndex(channel) is uint channelIndex))
            {
                throw new RecoverableException($"Invalid channel number {channel}");
            }
            return channelIndex;
        }

        private ushort HandleDrpRead(ushort address)
        {
            // The DRP register addresses match the ADC channel numbers used in the datasheet.
            if(AdcChannelNumberToIndex(address) is uint channelIndex)
            {
                var rawValue = channelValues[channelIndex];
                this.DebugLog($"Read ADC channel {address} with value {rawValue}");
                return (ushort)(rawValue << 4); // MSB-justify 12-bit value in 16-bit register
            }
            else
            {
                this.Log(LogLevel.Warning, $"Unhandled DRP register read: 0x{address:x}");
                return 0;
            }
        }

        private ushort HandleDrpWrite(ushort address, ushort value)
        {
            this.Log(LogLevel.Warning, $"Unhandled DRP register write: 0x{address:x}, value: 0x{value:x}");
            return 0;
        }

        private void DataFifoSend(uint value)
        {
            if(dataFifo.Count == DATA_FIFO_CAPACITY)
            {
                this.Log(LogLevel.Warning, $"Tried to enqueue onto a full data FIFO, value: 0x{value:x}");
                return;
            }

            dataFifo.Enqueue(value);
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            intStatusDfifoGth.Value |= dataFifo.Count > (int)dataFifoThreshold.Value;

            var interruptFlag = (interruptStatus.Value & ~interruptMask.Value) != 0;
            IRQ.Set(interruptFlag);
        }

        private uint readDataValue;

        private IValueRegisterField dataFifoThreshold;

        private DoubleWordRegister interruptStatus;
        private IFlagRegisterField intStatusDfifoGth;

        private DoubleWordRegister interruptMask;

        private readonly Queue<uint> dataFifo;
        private readonly ushort[] channelValues;

        private const int NUMBER_OF_CHANNELS = 1 +
            (int)DrpRegister.VccBram - (int)DrpRegister.Temperature +
            (int)DrpRegister.VauxpVauxn15 - (int)DrpRegister.VccPint;
        private const int DATA_FIFO_CAPACITY = 15;

        private enum PsXadcCommand
        {
            NoOperation = 0x0,
            DrpRead     = 0x1,
            DrpWrite    = 0x2,
        }

        private enum Register: long
        {
            Configuration   = 0x00, // XADCIF_CFG
            InterruptStatus = 0x04, // XADCIF_INT_STS
            InterruptMask   = 0x08, // XADCIF_INT_MASK
            MiscStatus      = 0x0C, // XADCIF_MSTS
            CommmandFifo    = 0x10, // XADCIF_CMDFIFO
            DataFifo        = 0x14, // XADCIF_RDFIFO
            MiscControl     = 0x18, // XADCIF_MCTL
        }

        private enum DrpRegister: ushort
        {
            /* ADC conversion result registers. Each of these is a 12-bit value MSB-justified to 16 bits. */
            Temperature           = 0x00,
            VccInt                = 0x01,
            VccAux                = 0x02,
            VpVn                  = 0x03,
            Vrefp                 = 0x04,
            Vrefn                 = 0x05,
            VccBram               = 0x06,
            VccPint               = 0x0D,
            VccPaux               = 0x0E,
            VccoDdr               = 0x0F,
            VauxpVauxn0           = 0x10,
            VauxpVauxn15          = 0x1F,
            /* End of ADC conversion result registers. */
        }
    }
}
