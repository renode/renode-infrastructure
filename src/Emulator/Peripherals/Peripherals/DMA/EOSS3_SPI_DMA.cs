//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Peripherals.GPIOPort;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class EOSS3_SPI_DMA : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public EOSS3_SPI_DMA(IMachine machine, DesignWare_SPI spi) : base(machine, 1)
        {
            sysbus = machine.GetSystemBus(this);
            this.spi = spi;
            innerLock = new object();
            RegistersCollection = new DoubleWordRegisterCollection(this);

            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            enabled = false;

            base.Reset();
            RegistersCollection.Reset();
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!value)
            {
                return;
            }

            lock(innerLock)
            {
                if(!Enabled)
                {
                    // ignore interrupts when not enabled
                    this.Log(LogLevel.Warning, "DMA not enabled, ignoring an interrupt from SPI Master");
                    return;
                }

                // this means that the SPIMaster has some data waiting in the buffer
                while(spi.TryDequeueFromReceiveBuffer(out var data))
                {
                    // now we must check if there is one or two bytes
                    var bytesToHandle = 0u;
                    switch(spi.FrameSize)
                    {
                        case DesignWare_SPI.TransferSize.SingleByte:
                        {
                            bytesToHandle = 1;
                            break;
                        }

                        case DesignWare_SPI.TransferSize.DoubleByte:
                        {
                            bytesToHandle = 2;
                            break;
                        }

                        default:
                            throw new ArgumentException($"Unexpected transfer size {spi.FrameSize}");
                    }

                    if(bytesToHandle > transferCount.Value)
                    {
                        this.Log(LogLevel.Warning, "DMA configuration mismatch detected - transfer count left is {0} bytes, but there are more ({1}) bytes available in the SPI Master's buffer. Some data will be lost!", transferCount.Value, bytesToHandle);
                        bytesToHandle = (uint)transferCount.Value;
                    }

                    // now we must check if there is one or two bytes again
                    switch(bytesToHandle)
                    {
                        case 1:
                        {
                            this.Log(LogLevel.Noisy, "DMA transfer: writing byte 0x{0:X} at offset 0x{1:X}", data, destinationAddress.Value);
                            sysbus.WriteByte(destinationAddress.Value, (byte)data);
                            break;
                        }

                        case 2:
                        {
                            this.Log(LogLevel.Noisy, "DMA transfer: writing ushort 0x{0:X} at offset 0x{1:X}", data, destinationAddress.Value);
                            sysbus.WriteWord(destinationAddress.Value, data);
                            break;
                        }
                    }

                    // transferCount is in bytes
                    transferCount.Value -= bytesToHandle;
                    destinationAddress.Value += bytesToHandle;

                    if(transferCount.Value == 0)
                    {
                        this.Log(LogLevel.Noisy, "That's the end of the DMA transfer");

                        dmaDataAvailable.Value = true;
                        UpdateInterrupts();
                        break;
                    }
                }
            }
        }

        public long Size => 0x100;
        public GPIO IRQ { get; } = new GPIO();

        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        public bool Enabled
        {
            get => enabled;

            set
            {
                lock(innerLock)
                {
                    if(value)
                    {
                        transferCount.Value += 1;
                    }

                    enabled = value;
                }
            }
        }

        private void UpdateInterrupts()
        {
            var state = false;

            state |= (dmaDataAvailable.Value && dmaDataAvailableEnable.Value);

            this.Log(LogLevel.Noisy, "Setting interrupt to: {0}", state);
            IRQ.Set(state);
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this, 0x40) // DMA_HREADY is set on reset
                .WithFlag(0, FieldMode.Write, name: "dma_start", writeCallback: (_, val) => { if(val) Enabled = true; })
                .WithFlag(1, FieldMode.Write, name: "dma_stop", writeCallback: (_, val) => { if(val) Enabled = false; })
                .WithTag("dma_ahb_sel", 2, 1)
                .WithTag("dma_hsel", 3, 1)
                .WithTag("dma_htrans_0", 4, 1)
                .WithTag("dma_htrans_1", 5, 1)
                .WithFlag(6, FieldMode.Read, name: "dma_hready", valueProviderCallback: _ => true)
                .WithTag("dma_xfr_pending", 7, 1)
                .WithTag("bridge_xfr_pending", 8, 1)
                .WithReservedBits(9, 23)
            ;

            Registers.DestinationAddress.Define(this)
                .WithValueField(0, 32, out destinationAddress, name: "dma_dest_addr")
            ;

            Registers.TransferCount.Define(this)
                .WithValueField(0, 26, out transferCount, name: "dma_xfr_cnt")
                .WithReservedBits(27, 5)
            ;

            Registers.ConfigFlashHeader.Define(this)
                .WithTag("dma_boot_xfr_size", 0, 16)
                .WithTag("dma_spi_clk_divide", 16, 8)
                .WithTag("dma_device_id", 24, 8)
            ;

            Registers.Interrupts.Define(this)
                .WithTag("dma_herror", 0, 1) // FieldMode.Read | FieldMode.WriteOneToClear
                .WithFlag(1, out dmaDataAvailable, FieldMode.Read, name: "dmrx_data_available")
                .WithTag("dmahb_bridge_fifo_overflow", 2, 1) // FieldMode.Read | FieldMode.WriteOneToClear
                .WithTag("dmspim_ssi_txe_intr", 3, 1)
                .WithTag("dmspim_ssi_txo_intr", 4, 1)
                .WithTag("dmspim_ssi_rxf_intr", 5, 1)
                .WithTag("dmspim_ssi_rxo_intr", 6, 1)
                .WithTag("dmspim_ssi_rxu_intr", 7, 1)
                .WithTag("dmspim_ssi_mst_intr", 8, 1)
                .WithReservedBits(9, 23)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptMask.Define(this, 0x7)
                .WithTag("dma_herror_mask", 0, 1)
                .WithFlag(1, out dmaDataAvailableEnable, name: "dmrx_data_available_mask")
                .WithTag("dmahb_bridge_fifo_overflow_mask", 2, 1)
                .WithReservedBits(3, 29)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.ConfigStateMachineDelay.Define(this)
                .WithTag("delay_reg", 0, 16)
                .WithReservedBits(16, 16)
            ;
        }

        private IValueRegisterField destinationAddress;
        private IValueRegisterField transferCount;
        private IFlagRegisterField dmaDataAvailable;
        private IFlagRegisterField dmaDataAvailableEnable;
        private bool enabled;

        private readonly IBusController sysbus;
        private readonly DesignWare_SPI spi;
        private readonly object innerLock;

        private enum Registers
        {
            Control = 0x00,
            DestinationAddress = 0x04,
            TransferCount = 0x08,
            ConfigFlashHeader = 0x0C,
            Interrupts = 0x10,
            InterruptMask = 0x14,
            ConfigStateMachineDelay = 0x18
        }
    }
}
