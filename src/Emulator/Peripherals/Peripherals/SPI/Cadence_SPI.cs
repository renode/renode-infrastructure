//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Helpers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class Cadence_SPI : SimpleContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public Cadence_SPI(IMachine machine, int txFifoCapacity = DefaultTxFifoCapacity, int rxFifoCapacity = DefaultRxFifoCapacity) : base(machine)
        {
            this.txFifoCapacity = txFifoCapacity;
            this.rxFifoCapacity = rxFifoCapacity;

            txFifo = new Queue<byte>(this.txFifoCapacity);
            rxFifo = new Queue<byte>(this.rxFifoCapacity);
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());

            txFifoFull = new CadenceInterruptFlag(() => txFifo.Count >= this.txFifoCapacity);
            txFifoNotFull = new CadenceInterruptFlag(() => txFifo.Count < (int)txFifoThreshold.Value);
            txFifoUnderflow = new CadenceInterruptFlag(() => false);
            rxFifoOverflow = new CadenceInterruptFlag(() => false);
            rxFifoFull = new CadenceInterruptFlag(() => rxFifo.Count >= this.rxFifoCapacity);
            rxFifoNotEmpty = new CadenceInterruptFlag(() => rxFifo.Count >= (int)rxFifoThreshold.Value);
            modeFail = new CadenceInterruptFlag(() => false); // Not handled
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public override void Reset()
        {
            registers.Reset();
            txFifo.Clear();
            rxFifo.Clear();
            selectedPeripheral = null;

            foreach(var flag in GetInterruptFlags())
            {
                flag.Reset();
            }
            UpdateInterrupts();
        }

        public long Size => 0x100;

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        public GPIO TxFifoFullIRQ { get; } = new GPIO();
        public GPIO TxFifoFillLevelThresholdIRQ { get; } = new GPIO();

        public GPIO RxFifoFullIRQ { get; } = new GPIO();
        public GPIO RxFifoFillLevelThresholdIRQ { get; } = new GPIO();

        private void TransmitData()
        {
            if(spiMode.Value == SPIMode.PeripheralMode)
            {
                this.Log(LogLevel.Warning, "Trying to transfer data from a SPI in peripheral mode. Only controller mode is supported.");
                return;
            }
            if(!spiEnable.Value)
            {
                this.Log(LogLevel.Warning, "Trying to transfer data from a disabled SPI.");
                return;
            }
            if(txFifo.Count == 0)
            {
                txFifoUnderflow.SetSticky(true);
                this.Log(LogLevel.Debug, "Trying to transfer data from an empty Tx FIFO.");
                return;
            }

            selectedPeripheral = GetPeripheral();
            if(selectedPeripheral != null)
            {
                while(txFifo.Count > 0)
                {
                    EnqueueRx(selectedPeripheral.Transmit(txFifo.Dequeue()));
                }
                if(!manualChipSelect.Value)
                {
                    DeselectPeripheral();
                }
            }
            else
            {
                for(var i = 0; i < txFifo.Count; i++)
                {
                    EnqueueRx(default(byte));
                }
                txFifo.Clear();
                this.Log(LogLevel.Warning, "There is no peripheral with selected address, received data contains dummy bytes.");
            }
        }

        private void TryTransmitDataAutomatically()
        {
            if(!manualTransmission.Value && txFifo.Count > 0)
            {
                TransmitData();
            }
        }

        private void DeselectPeripheral()
        {
            if(selectedPeripheral != null)
            {
                selectedPeripheral.FinishTransmission();
                selectedPeripheral = null;
            }
        }

        private void EnqueueTx(byte data)
        {
            if(!spiEnable.Value)
            {
                this.Log(LogLevel.Debug, "Writing to a disabled SPI peripheral FIFO.");
            }
            if(txFifoFull.Status)
            {
                this.Log(LogLevel.Warning, "Trying to write to a full Tx FIFO, data not queued.");
                // According to the Zynq US+ Technical Reference Manual, the rxFifoOverflow interrupt should be asserted also for Tx FIFO overflow.
                rxFifoOverflow.SetSticky(true);
                return;
            }
            txFifo.Enqueue(data);
        }

        private void EnqueueRx(byte data)
        {
            if(rxFifoFull.Status)
            {
                rxFifoOverflow.SetSticky(true);
                this.Log(LogLevel.Warning, "Can't write to a full Rx FIFO, data not queued.");
                return;
            }
            rxFifo.Enqueue(data);
        }

        private byte DequeueRx()
        {
            if(!spiEnable.Value)
            {
                this.Log(LogLevel.Debug, "Reading from a disabled SPI peripheral FIFO.");
            }
            if(rxFifo.Count == 0)
            {
                this.Log(LogLevel.Warning, "Trying to read from an empty Rx FIFO, dummy data returned.");
                return default(byte);
            }
            return rxFifo.Dequeue();
        }

        private ISPIPeripheral GetPeripheral()
        {
            if(TryGetPeripheralAddress(out var address))
            {
                if(TryGetByAddress(address, out var peripheral))
                {
                    return peripheral;
                }
                this.Log(LogLevel.Warning, "Can't find SPI peripheral at address 0x{0:X}.", address);
            }
            return null;
        }

        private bool TryGetPeripheralAddress(out int address)
        {
            if(chipSelectAddr.Value != ChipSelectNoPeripheral)
            {
                var addressReg = (int)chipSelectAddr.Value & ChipSelectAddrMask;
                if(chipSelectMode.Value != ChipSelectMode.ThreeChipsNoDecoder)
                {
                    address = addressReg;
                    return true;
                }

                // Address is set to 0 for register = 0bxxx0, 1 for register = 0bxx01 and so on
                for(var index = 0; index < ChipSelectNoDecoderCount; index++)
                {
                    if((addressReg & 0b1) == 0)
                    {
                        address = index;
                        return true;
                    }
                    addressReg >>= 1;
                }
                // The 0b0111 value of register is reserved
                this.Log(LogLevel.Warning, "The 0x{0:X} value of the chipSelectAddr register is reserved.", chipSelectAddr.Value);
            }
            address = -1;
            return false;
        }

        private void UpdateSticky()
        {
            foreach(var flag in GetInterruptFlags())
            {
                flag.UpdateStickyStatus();
            }
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(GetInterruptFlags().Any(x => x.InterruptStatus));
            RxFifoFullIRQ.Set(rxFifoFull.InterruptStatus);
            TxFifoFullIRQ.Set(txFifoFull.InterruptStatus);
            RxFifoFillLevelThresholdIRQ.Set(rxFifoNotEmpty.InterruptStatus);
            TxFifoFillLevelThresholdIRQ.Set(txFifoNotFull.InterruptStatus);
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var txFifoThresholdBits = BitHelper.GetMostSignificantSetBitIndex((ulong)txFifoCapacity);
            var rxFifoThresholdBits = BitHelper.GetMostSignificantSetBitIndex((ulong)rxFifoCapacity);
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Config, new DoubleWordRegister(this, 0x00020000)
                    .WithReservedBits(18, 14)
                    .WithFlag(17, out modeFailEnable, name: "modeFailEnable")
                    .WithFlag(16, FieldMode.Write, name: "manualStartTransmission",
                        writeCallback: (_, val) => { if(val) TransmitData(); })
                    .WithFlag(15, out manualTransmission, name: "manualTransmission")
                    .WithFlag(14, out manualChipSelect, name: "manualChipSelect")
                    .WithValueField(10, 4, out chipSelectAddr, name: "chipSelectAddress")
                    .WithEnumField(9, 1, out chipSelectMode)
                    .WithFlag(8, out referenceClockSelect, name: "referenceClockSelect")
                    .WithReservedBits(6, 2)
                    .WithValueField(3, 3, out baudRateDivider, name: "baudRateDivider")
                    .WithFlag(2, out clockPhase, name: "clockPhase")
                    .WithFlag(1, out clockPolarity, name: "clockPolarity")
                    .WithEnumField(0, 1, out spiMode, name: "spiMode",
                        changeCallback: (_, val) => {
                            if(val == SPIMode.PeripheralMode)
                            {
                                this.Log(LogLevel.Warning, "SPI mode changed to peripheral, while only controller mode is supported.");
                            }
                        })
                    .WithWriteCallback((_, val) =>
                    {
                        if(chipSelectAddr.Value == ChipSelectNoPeripheral || selectedPeripheral != GetPeripheral())
                        {
                            DeselectPeripheral();
                        }
                    })
                },
                {(long)Registers.InterruptStatus, new DoubleWordRegister(this)
                    .WithReservedBits(7, 25)
                    .WithFlag(6,
                        valueProviderCallback: (_) => txFifoUnderflow.StickyStatus,
                        writeCallback: (_, val) => txFifoUnderflow.ClearSticky(val),
                        name: "txFifoUnderflowInterruptStatus"
                    )
                    // A few interrupts aren't sticky so they are cleared at every read
                    .WithFlag(5,
                        valueProviderCallback: (_) => rxFifoFull.StickyStatus,
                        writeCallback: (_, val) => rxFifoFull.ClearSticky(val),
                        readCallback:  (_, __) => rxFifoFull.ClearSticky(true),
                        name: "rxFifoFullInterruptStatus"
                    )
                    .WithFlag(4,
                        valueProviderCallback: (_) => rxFifoNotEmpty.StickyStatus,
                        writeCallback: (_, val) => rxFifoNotEmpty.ClearSticky(val),
                        readCallback:  (_, __) => rxFifoNotEmpty.ClearSticky(true),
                        name: "rxFifoNotEmptyInterruptStatus"
                    )
                    .WithFlag(3,
                        valueProviderCallback: (_) => txFifoFull.StickyStatus,
                        writeCallback: (_, val) => txFifoFull.ClearSticky(val),
                        readCallback:  (_, __) => txFifoFull.ClearSticky(true),
                        name: "txFifoFullInterruptStatus"
                    )
                    .WithFlag(2,
                        valueProviderCallback: (_) => txFifoNotFull.StickyStatus,
                        writeCallback: (_, val) => txFifoNotFull.ClearSticky(val),
                        readCallback:  (_, __) => txFifoNotFull.ClearSticky(true),
                        name: "txFifoNotFullInterruptStatus"
                    )
                    .WithFlag(1,
                        valueProviderCallback: (_) => modeFail.StickyStatus,
                        writeCallback: (_, val) => modeFail.ClearSticky(val),
                        name: "modeFailInterruptStatus"
                    )
                    .WithFlag(0,
                        valueProviderCallback: (_) => rxFifoOverflow.StickyStatus,
                        writeCallback: (_, val) => rxFifoOverflow.ClearSticky(val),
                        name: "rxFifoOverflowInterruptStatus"
                    )
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateSticky();
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithReservedBits(7, 25)
                    .WithFlag(6, FieldMode.Write,
                        writeCallback: (_, val) => txFifoUnderflow.InterruptEnable(val),
                        name: "txFifoUnderflowInterruptEnable"
                    )
                    .WithFlag(5, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoFull.InterruptEnable(val),
                        name: "rxFifoFullInterruptEnable"
                    )
                    .WithFlag(4, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoNotEmpty.InterruptEnable(val),
                        name: "rxFifoNotEmptyInterruptEnable"
                    )
                    .WithFlag(3, FieldMode.Write,
                        writeCallback: (_, val) => txFifoFull.InterruptEnable(val),
                        name: "txFifoFullInterruptEnable"
                    )
                    .WithFlag(2, FieldMode.Write,
                        writeCallback: (_, val) => txFifoNotFull.InterruptEnable(val),
                        name: "txFifoNotFullInterruptEnable"
                    )
                    .WithFlag(1, FieldMode.Write,
                        writeCallback: (_, val) => modeFail.InterruptEnable(val),
                        name: "modeFailInterruptEnable"
                    )
                    .WithFlag(0, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoOverflow.InterruptEnable(val),
                        name: "rxFifoOverflowInterruptEnable"
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptDisable, new DoubleWordRegister(this)
                    .WithReservedBits(7, 25)
                    .WithFlag(6, FieldMode.Write,
                        writeCallback: (_, val) => txFifoUnderflow.InterruptDisable(val),
                        name: "txFifoUnderflowInterruptDisable"
                    )
                    .WithFlag(5, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoFull.InterruptDisable(val),
                        name: "rxFifoFullInterruptDisable"
                    )
                    .WithFlag(4, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoNotEmpty.InterruptDisable(val),
                        name: "rxFifoNotEmptyInterruptDisable"
                    )
                    .WithFlag(3, FieldMode.Write,
                        writeCallback: (_, val) => txFifoFull.InterruptDisable(val),
                        name: "txFifoFullInterruptDisable"
                    )
                    .WithFlag(2, FieldMode.Write,
                        writeCallback: (_, val) => txFifoNotFull.InterruptDisable(val),
                        name: "txFifoNotFullInterruptDisable"
                    )
                    .WithFlag(1, FieldMode.Write,
                        writeCallback: (_, val) => modeFail.InterruptDisable(val),
                        name: "modeFailInterruptDisable"
                    )
                    .WithFlag(0, FieldMode.Write,
                        writeCallback: (_, val) => rxFifoOverflow.InterruptDisable(val),
                        name: "rxFifoOverflowInterruptDisable"
                    )
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptMask, new DoubleWordRegister(this)
                    .WithReservedBits(7, 25)
                    .WithFlag(6, FieldMode.Read,
                        valueProviderCallback: (_) => txFifoUnderflow.InterruptMask,
                        name: "txFifoUnderflowInterruptMask"
                    )
                    .WithFlag(5, FieldMode.Read,
                        valueProviderCallback: (_) => rxFifoFull.InterruptMask,
                        name: "rxFifoFullInterruptMask"
                    )
                    .WithFlag(4, FieldMode.Read,
                        valueProviderCallback: (_) => rxFifoNotEmpty.InterruptMask,
                        name: "rxFifoNotEmptyInterruptMask"
                    )
                    .WithFlag(3, FieldMode.Read,
                        valueProviderCallback: (_) => txFifoFull.InterruptMask,
                        name: "txFifoFullInterruptMask"
                    )
                    .WithFlag(2, FieldMode.Read,
                        valueProviderCallback: (_) => txFifoNotFull.InterruptMask,
                        name: "txFifoNotFullInterruptMask"
                    )
                    .WithFlag(1, FieldMode.Read,
                        valueProviderCallback: (_) => modeFail.InterruptMask,
                        name: "modeFailInterruptMask"
                    )
                    .WithFlag(0, FieldMode.Read,
                        valueProviderCallback: (_) => rxFifoOverflow.InterruptMask,
                        name: "rxFifoOverflowInterruptMask"
                    )
                },
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithReservedBits(1, 31)
                    .WithFlag(0, out spiEnable, name: "spiEnable",
                        writeCallback: (_, val) =>
                        {
                            if(val)
                            {
                                TryTransmitDataAutomatically();
                            }
                            else
                            {
                                DeselectPeripheral();
                            }
                        })
                },
                {(long)Registers.TxData, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithValueField(0, 8, FieldMode.Write, name: "txData",
                        writeCallback: (_, data) =>
                        {
                            EnqueueTx((byte)data);
                            TryTransmitDataAutomatically();
                        })
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateSticky();
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.RxData, new DoubleWordRegister(this)
                    .WithReservedBits(8, 24)
                    .WithValueField(0, 8, FieldMode.Read, name: "rxData",
                        valueProviderCallback: (_) => DequeueRx()
                    )
                    .WithReadCallback((_, __) =>
                    {
                        UpdateSticky();
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.TxFifoThreshold, new DoubleWordRegister(this, InitialFifoThreshold)
                    .WithReservedBits(txFifoThresholdBits, 32 - txFifoThresholdBits)
                    .WithValueField(0, txFifoThresholdBits, out txFifoThreshold)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateSticky();
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.RxFifoThreshold, new DoubleWordRegister(this, InitialFifoThreshold)
                    .WithReservedBits(rxFifoThresholdBits, 32 - rxFifoThresholdBits)
                    .WithValueField(0, rxFifoThresholdBits, out rxFifoThreshold)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateSticky();
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.ModuleID, new DoubleWordRegister(this)
                    .WithReservedBits(25, 7)
                    .WithValueField(0, 25, FieldMode.Read, name: "moduleID",
                        valueProviderCallback: (_) => ModuleID
                    )
                }
            };
        }

        private IEnumerable<CadenceInterruptFlag> GetInterruptFlags()
        {
            yield return txFifoFull;
            yield return txFifoNotFull;
            yield return txFifoUnderflow;
            yield return rxFifoOverflow;
            yield return rxFifoFull;
            yield return rxFifoNotEmpty;
        }

        private ISPIPeripheral selectedPeripheral;

        private IFlagRegisterField modeFailEnable;
        private IFlagRegisterField manualTransmission;
        private IFlagRegisterField manualChipSelect;
        private IValueRegisterField chipSelectAddr;
        private IEnumRegisterField<ChipSelectMode> chipSelectMode;
        private IEnumRegisterField<SPIMode> spiMode;
        private IFlagRegisterField spiEnable;
        private IValueRegisterField txFifoThreshold;
        private IValueRegisterField rxFifoThreshold;

        // These fields are related to a physical layer, which Renode doesn't simulate for the SPI bus
        private IFlagRegisterField referenceClockSelect;
        private IValueRegisterField baudRateDivider;
        private IFlagRegisterField clockPhase;
        private IFlagRegisterField clockPolarity;

        private readonly CadenceInterruptFlag txFifoFull;
        private readonly CadenceInterruptFlag txFifoNotFull;
        private readonly CadenceInterruptFlag txFifoUnderflow;
        private readonly CadenceInterruptFlag rxFifoOverflow;
        private readonly CadenceInterruptFlag rxFifoFull;
        private readonly CadenceInterruptFlag rxFifoNotEmpty;
        private readonly CadenceInterruptFlag modeFail;

        private readonly int txFifoCapacity;
        private readonly int rxFifoCapacity;
        private readonly Queue<byte> txFifo;
        private readonly Queue<byte> rxFifo;
        private readonly DoubleWordRegisterCollection registers;

        private const int DefaultTxFifoCapacity = 128;
        private const int DefaultRxFifoCapacity = 128;
        private const int InitialFifoThreshold = 0x1;
        private const int ChipSelectNoPeripheral = 0b1111;
        private const int ChipSelectAddrMask = 0b0111;
        private const int ChipSelectNoDecoderCount = 3;
        private const int ModuleID = 0x90108;

        private enum ChipSelectMode
        {
            ThreeChipsNoDecoder = 0x0,
            EightChipsWithDecoder = 0x1
        }

        private enum SPIMode
        {
            PeripheralMode = 0x0,
            ControllerMode = 0x1
        }

        private enum Registers : long
        {
            Config = 0x00,
            InterruptStatus = 0x04,
            InterruptEnable = 0x08,
            InterruptDisable = 0x0c,
            InterruptMask = 0x10,
            Enable = 0x14,
            Delay = 0x18,
            TxData = 0x1c,
            RxData = 0x20,
            SlaveIdleCount = 0x24,
            TxFifoThreshold = 0x28,
            RxFifoThreshold = 0x2c,
            ModuleID = 0xfc
        }
    }
}
