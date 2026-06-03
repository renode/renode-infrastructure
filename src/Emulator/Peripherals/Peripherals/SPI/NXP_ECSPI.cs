//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class NXP_ECSPI : SimpleContainer<ISPIPeripheral>, IKnownSize, IDoubleWordPeripheral, IGPIOReceiver
    {
        public NXP_ECSPI(IMachine machine, bool externalChipSelect = false) : base(machine)
        {
            IRQ = new GPIO();
            registers = new DoubleWordRegisterCollection(this);
            ExternalChipSelect = externalChipSelect;
            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void OnGPIO(int number, bool value)
        {
            if(!ExternalChipSelect)
            {
                this.Log(LogLevel.Warning, "Got a chip select on GPIO {0}, but the controller is configured for internal chip select - ignoring", number);
                return;
            }

            // CS is active-low
            var asserted = !value;
            this.Log(LogLevel.Noisy, "Chip select line {0} {1}", number, asserted ? "asserted" : "deasserted");

            if(asserted)
            {
                activeChipSelects.Add(number);
            }
            else if(activeChipSelects.Remove(number) && TryGetByAddress(number, out var device))
            {
                device.FinishTransmission();
            }
            else
            {
                // Deassert of a line that either wasn't asserted (e.g. the idle-high state the
                // driver sets up before the first transfer) or that has no device registered at
                // its address. There is nothing to finish.
            }
        }

        public override void Reset()
        {
            registers.Reset();

            receiveFifo.Clear();
            transmitFifo.Clear();
            activeChipSelects.Clear();

            UpdateInterrupts();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; private set; }

        public bool ExternalChipSelect { get; }

        private void UpdateInterrupts()
        {
            var irq = false;

            irq |= (txFifoEmptyInterruptStatus.Value = TxFifoEmpty) && txFifoEmptyInterruptEnabled.Value;
            irq |= (txFifoDataRequestInterruptStatus.Value = TxFifoThreshold) && txFifoDataRequestInterruptEnabled.Value;
            irq |= (txFifoFullInterruptStatus.Value = TxFifoFull) && txFifoFullInterruptEnabled.Value;
            irq |= (rxFifoReadyInterruptStatus.Value = RxFifoNotEmpty) && rxFifoReadyInterruptEnabled.Value;
            irq |= (rxFifoDataRequestInterruptStatus.Value = RxFifoThreshold) && rxFifoDataRequestInterruptEnabled.Value;
            irq |= (rxFifoFullInterruptStatus.Value = RxFifoFull) && rxFifoFullInterruptEnabled.Value;
            irq |= (rxFifoOverflowInterruptStatus.Value = false) && rxFifoOverflowInterruptEnabled.Value;
            irq |= transferCompletedInterruptStatus.Value && transferCompletedInterruptEnabled.Value;

            IRQ.Set(irq);
        }

        private void DefineRegisters()
        {
            BaseRegisters.ReceiveData.Define(registers, name: "ECSPI_RXDATA")
                .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        if(!spiEnabled.Value)
                        {
                            this.Log(LogLevel.Noisy, "Data not read from FIFO: SPI is not enabled");
                            return 0;
                        }
                        this.Log(LogLevel.Noisy, "Reading from RX FIFO");
                        if(receiveFifo.Count == 0)
                        {
                            this.Log(LogLevel.Noisy, "Attempted to read from empty RX FIFO");
                            return 0;
                        }
                        return receiveFifo.Dequeue();
                    },
                    name: "ECSPI_RXDATA")
                .WithReadCallback((_, __) => UpdateInterrupts());

            BaseRegisters.TransmitData.Define(registers, name: "ECSPI_TXDATA")
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) =>
                {
                    if(!spiEnabled.Value)
                    {
                        this.Log(LogLevel.Noisy, "Data not written to FIFO: SPI is not enabled");
                        return;
                    }
                    this.Log(LogLevel.Noisy, "Writing to TX FIFO");
                    transmitFifo.Enqueue((uint)value);
                    if(smc.Value)
                    {
                        TriggerSpiBurst();
                    }
                }, name: "ECSPI_TXDATA")
                .WithWriteCallback((_, __) => UpdateInterrupts());

            BaseRegisters.Control.Define(registers, name: "ECSPI_CONREG")
                .WithFlag(0, out spiEnabled, name: "EN")
                .WithFlag(1, name: "HT")
                .WithFlag(2, name: "XCH", valueProviderCallback: (_) => false, writeCallback: (_, value) =>
                {
                    if(!smc.Value && value)
                    {
                        TriggerSpiBurst();
                    }
                })
                .WithFlag(3, out smc, name: "SMC")
                .WithValueField(4, 4, name: "CHANNEL_MODE", changeCallback: (_, value) =>
                {
                    this.Log(LogLevel.Noisy, "Channel mode: {0}", value);
                })
                .WithValueField(8, 4, name: "POST_DIVIDER")
                .WithValueField(12, 4, name: "PRE_DIVIDER")
                .WithEnumField<DoubleWordRegister, SpiReadyMode>(16, 2, name: "DRCTL", changeCallback: (_, value) =>
                {
                    this.Log(LogLevel.Noisy, "New Mode: {0}", value);
                })
                .WithValueField(18, 2, name: "CHANNEL_SELECT", valueProviderCallback: _ => 0) // this model only supports ch 0
                .WithValueField(20, 12, out burstLength, name: "BURST_LENGTH")
                .WithWriteCallback((_, __) => UpdateInterrupts());

            BaseRegisters.Config.Define(registers, name: "ECSPI_CONFIGREG")
                .WithValueField(0, 4, name: "SCLK_PHA")
                .WithValueField(4, 4, name: "SCLK_POL")
                .WithEnumField(8, 1, out ssCtl, name: "SS_CTL") // only ch 0 supports CS forming
                .WithValueField(9, 3, name: "SS_CTL") // ch 1..3 are unused
                .WithValueField(12, 4, name: "SS_POL")
                .WithValueField(16, 4, name: "DATA_CTL")
                .WithValueField(20, 4, name: "SCLK_CTL")
                .WithValueField(24, 5, name: "HT_LENGTH")
                .WithReservedBits(29, 3)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            BaseRegisters.InterruptControl.Define(registers, name: "ECSPI_INTREG")
                .WithFlag(0, out txFifoEmptyInterruptEnabled, name: "TEEN")
                .WithFlag(1, out txFifoDataRequestInterruptEnabled, name: "TDREN")
                .WithFlag(2, out txFifoFullInterruptEnabled, name: "TFEN")
                .WithFlag(3, out rxFifoReadyInterruptEnabled, name: "RREN")
                .WithFlag(4, out rxFifoDataRequestInterruptEnabled, name: "RDREN")
                .WithFlag(5, out rxFifoFullInterruptEnabled, name: "RFEN")
                .WithFlag(6, out rxFifoOverflowInterruptEnabled, name: "ROEN")
                .WithFlag(7, out transferCompletedInterruptEnabled, name: "TCEN")
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            BaseRegisters.DMAControl.Define(registers, name: "ECSPI_DMAREG")
                .WithValueField(0, 6, out txThreshold, name: "TX_THRESHOLD")
                .WithReservedBits(6, 1)
                .WithFlag(7, name: "TEDEN")
                .WithReservedBits(8, 8)
                .WithValueField(16, 6, out rxThreshold, name: "RX_THRESHOLD")
                .WithReservedBits(22, 1)
                .WithFlag(23, name: "RXDEN")
                .WithValueField(24, 6, name: "RX_DMA_LENGTH")
                .WithReservedBits(30, 1)
                .WithFlag(31, name: "RXTDEN")
                .WithWriteCallback((_, __) => UpdateInterrupts());

            BaseRegisters.Status.Define(registers, name: "ECSPI_STATREG")
                .WithFlag(0, out txFifoEmptyInterruptStatus, mode: FieldMode.Read, name: "TE")
                .WithFlag(1, out txFifoDataRequestInterruptStatus, mode: FieldMode.Read, name: "TDR")
                .WithFlag(2, out txFifoFullInterruptStatus, mode: FieldMode.Read, name: "TF")
                .WithFlag(3, out rxFifoReadyInterruptStatus, mode: FieldMode.Read, name: "RR")
                .WithFlag(4, out rxFifoDataRequestInterruptStatus, mode: FieldMode.Read, name: "RDR")
                .WithFlag(5, out rxFifoFullInterruptStatus, mode: FieldMode.Read, name: "RF")
                .WithFlag(6, out rxFifoOverflowInterruptStatus, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "RO")
                .WithFlag(7, out transferCompletedInterruptStatus, mode: FieldMode.Read | FieldMode.WriteOneToClear, name: "TC")
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            BaseRegisters.SamplePeriodControl.Define(registers, name: "ECSPI_PERIODREG")
                .WithValueField(0, 15, name: "SAMPLE_PERIOD")
                .WithFlag(15, name: "CSRC")
                .WithValueField(16, 6, name: "CSD_CTL")
                .WithReservedBits(22, 10)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            BaseRegisters.TestControl.Define(registers, name: "ECSPI_TESTREG")
                .WithValueField(0, 7, name: "TXCNT", valueProviderCallback: _ => (ulong)transmitFifo.Count)
                .WithReservedBits(7, 1)
                .WithValueField(8, 7, name: "RXCNT", valueProviderCallback: _ => (ulong)receiveFifo.Count)
                .WithReservedBits(15, 16)
                .WithFlag(31, name: "LBC")
                .WithWriteCallback((_, __) => UpdateInterrupts());
        }

        private void TransmitData(ISPIPeripheral peripheral)
        {
            var burstBits = (uint)burstLength.Value + 1;
            if(burstBits % 8 != 0)
            {
                this.Log(LogLevel.Warning, "Burst length of {0} bits is not byte-aligned", burstBits);
            }
            var remainingBytes = (int)RoundUpDiv8(burstBits);
            this.Log(LogLevel.Noisy, "Starting burst of {0} byte(s); TX FIFO has {1} word(s)", remainingBytes, transmitFifo.Count);

            // The eCSPI shifts each FIFO word MSB-first. Burst data is right-justified across the words, so
            // when the burst isn't a multiple of 4 bytes the FIRST word is the partial one (carrying the
            // leading bytes in its low bytes); every following word holds a full 4 bytes.
            while(remainingBytes > 0)
            {
                var wordBytes = (remainingBytes % 4 == 0) ? 4 : remainingBytes % 4;
                var txWord = transmitFifo.Count > 0 ? transmitFifo.Dequeue() : 0;
                uint rxWord = 0;
                for(var byteIndex = wordBytes - 1; byteIndex >= 0; byteIndex--)
                {
                    var txByte = (byte)(txWord >> (8 * byteIndex));
                    var rxByte = peripheral.Transmit(txByte);
                    this.Log(LogLevel.Noisy, "TX 0x{0:X2} -> RX 0x{1:X2}", txByte, rxByte);
                    rxWord |= (uint)rxByte << (8 * byteIndex);
                }
                receiveFifo.Enqueue(rxWord);
                remainingBytes -= wordBytes;
            }
        }

        private void TriggerSpiBurst()
        {
            if(spiEnabled.Value == false)
            {
                this.Log(LogLevel.Noisy, "Not transmitting, SPI disabled");
                return;
            }

            var peripheral = GetSelectedPeripheral();
            if(peripheral == null)
            {
                transmitFifo.Clear();
                transferCompletedInterruptStatus.Value = true;
                return;
            }

            // Chip-select boundaries depend on the CS source and SS_CTL waveform:
            // External CS: the controller only shifts data; FinishTransmission() runs when the GPIO line deasserts
            // Internal CS:
            // - SingleBurst - one SPI burst per XCH/SMC trigger, sized by BURST_LENGTH (may consume several FIFO words when the length allows)
            // - MultipleBurst - drain the TX FIFO in consecutive bursts; CS negates between bursts (each burst is still capped by BURST_LENGTH)
            if(ExternalChipSelect)
            {
                TransmitData(peripheral);
            }
            else if(ssCtl.Value == SlaveSelectWaveform.MultipleBurst)
            {
                while(transmitFifo.Count > 0)
                {
                    TransmitData(peripheral);
                    peripheral.FinishTransmission();
                }
            }
            else
            {
                TransmitData(peripheral);
                peripheral.FinishTransmission();
            }

            transferCompletedInterruptStatus.Value = true;
        }

        private ISPIPeripheral GetSelectedPeripheral()
        {
            var channel = 0;
            if(ExternalChipSelect)
            {
                if(activeChipSelects.Count == 0)
                {
                    this.Log(LogLevel.Warning, "Issued a transfer, but no chip select line is asserted - dropping the burst");
                    return null;
                }
                if(activeChipSelects.Count > 1)
                {
                    this.Log(LogLevel.Warning, "Issued a transfer with multiple chip selects asserted ({0}) - using the lowest", Misc.PrettyPrintCollectionHex(activeChipSelects));
                }
                channel = activeChipSelects.Min();
            }

            if(!TryGetByAddress(channel, out var peripheral))
            {
                this.Log(LogLevel.Warning, "Issued a transfer on channel {0}, but no SPI peripheral is connected - dropping the burst", channel);
                return null;
            }

            return peripheral;
        }

        private uint RoundUpDiv8(uint value)
        {
            return (value + 7) >> 3;
        }

        private bool TxFifoEmpty => transmitFifo.Count == 0;

        private bool RxFifoFull => receiveFifo.Count == FifoDepth;

        private bool RxFifoNotEmpty => receiveFifo.Count > 0;

        private bool RxFifoThreshold => receiveFifo.Count >= (int)rxThreshold.Value;

        private bool TxFifoFull => transmitFifo.Count == FifoDepth;

        private bool TxFifoThreshold => transmitFifo.Count <= (int)txThreshold.Value;

        private IValueRegisterField burstLength;

        private IFlagRegisterField spiEnabled;
        private IFlagRegisterField smc;

        private IFlagRegisterField txFifoEmptyInterruptEnabled;
        private IFlagRegisterField txFifoEmptyInterruptStatus;
        private IFlagRegisterField txFifoDataRequestInterruptEnabled;
        private IFlagRegisterField txFifoDataRequestInterruptStatus;
        private IFlagRegisterField txFifoFullInterruptEnabled;
        private IFlagRegisterField txFifoFullInterruptStatus;
        private IFlagRegisterField rxFifoReadyInterruptEnabled;
        private IFlagRegisterField rxFifoReadyInterruptStatus;
        private IFlagRegisterField rxFifoDataRequestInterruptEnabled;
        private IFlagRegisterField rxFifoDataRequestInterruptStatus;
        private IFlagRegisterField rxFifoFullInterruptEnabled;
        private IFlagRegisterField rxFifoFullInterruptStatus;
        private IFlagRegisterField rxFifoOverflowInterruptEnabled;
        private IFlagRegisterField rxFifoOverflowInterruptStatus;
        private IFlagRegisterField transferCompletedInterruptEnabled;
        private IFlagRegisterField transferCompletedInterruptStatus;

        private IValueRegisterField txThreshold;
        private IValueRegisterField rxThreshold;
        private IEnumRegisterField<SlaveSelectWaveform> ssCtl;

        private readonly Queue<uint> receiveFifo = new Queue<uint>();
        private readonly Queue<uint> transmitFifo = new Queue<uint>();
        private readonly HashSet<int> activeChipSelects = new HashSet<int>();

        private readonly DoubleWordRegisterCollection registers;

        private const uint FifoDepth = 64;

        private enum SlaveSelectWaveform
        {
            SingleBurst = 0b0,
            MultipleBurst = 0b1,
        }

        private enum SpiReadyMode
        {
            DontCare = 0b00,
            Falling = 0b01,
            Low = 0b10,
            Reserved = 0b11,
        }

        private enum BaseRegisters
        {
            ReceiveData = 0x0,
            TransmitData = 0x4,
            Control = 0x8,
            Config = 0xC,
            InterruptControl = 0x10,
            DMAControl = 0x14,
            Status = 0x18,
            SamplePeriodControl = 0x1C,
            TestControl = 0x20,
        }
    }
}
