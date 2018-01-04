//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;


using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Peripherals.I2C
{
    public sealed class XIIC : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral
    {
     
        public XIIC (Machine machine) : base(machine)
        {
            IRQ = new GPIO ();
            Reset ();
        }

        public uint ReadDoubleWord (long offset)
        {
            switch ((Offset)offset) {
            case Offset.ControlRegister:
                return control;
            case Offset.StatusRegister:
                return status;
            case Offset.I2CAddressRegister:
                return slaveAddress;
            case Offset.I2CDataRegister:
                return ReceiveData ();
            case Offset.InterruptStatusRegister:
                return interruptStatus;
            case Offset.TransferSizeRegister:
                return transferSize;
            case Offset.SlaveMonitorPauseRegister:
                return slaveMonitorPause;
            case Offset.TimeoutRegister:
                return timeout;
            case Offset.InterruptMaskRegister:
                return interruptMask;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord (long offset, uint value)
        {
            switch ((Offset)offset) {
            case Offset.ControlRegister:
                control = value & 0xFF7F;
                if ((control & (1 << (int)Control.ClearFifo)) > 0) {
                    fifo.Clear ();
                    transferSize = 0;
                    control &= ~((uint)1 << (int)Control.ClearFifo);
                }
                break;
            case Offset.I2CAddressRegister:
                slaveAddress = (byte)value;
                status |= (1 << (int)Status.BusActive);
                TransferData ();
                break;
            case Offset.StatusRegister:
                // seems that Linux does write to status register
                break;
            case Offset.I2CDataRegister:
                fifo.Enqueue ((byte)value);
                transferSize = (uint)(fifo.Count - 1);
                break;
            case Offset.InterruptStatusRegister:
                interruptStatus &= ~value;
                Update ();
                break;
            case Offset.TransferSizeRegister:
                transferSize = value & 0xFF;
                break;
            case Offset.SlaveMonitorPauseRegister:
                slaveMonitorPause = value & 0x0F; 
                break;
            case Offset.TimeoutRegister:
                timeout = value & 0xFF;
                break;
            case Offset.InterruptEnableRegister:
                interruptMask &= ~(value & 0x2FF);
                Update ();
                break;
            case Offset.InterruptDisableRegister:
                interruptMask |= (value & 0x2FF);
                Update ();
                break;
            default:
                throw new ArgumentOutOfRangeException ();
            }
        }
    
        public GPIO IRQ { get; private set; }

        public override void Reset ()
        {
            timeout = TimeoutRegisterReset;
            interruptMask = InterruptMaskRegisterReset; 
        }

        private uint ReceiveData ()
        {
            if (fifo.Count == 0) {
                SetInterrupt (Interrupts.ReceiveTransmitUnderflow);

                return 0;
            }
            var ret = fifo.Dequeue();
            if(fifo.Count == 0)
            {
                status &= ~((uint)(1<< (int)Status.ReceiverDataValid));
            }
            return ret;
        }

        private void TransferData ()
        {
            II2CPeripheral device;
            if (!TryGetByAddress(slaveAddress, out device)) {
                SetInterrupt (Interrupts.NoACK);
                return;
            }
            if ((control & (1 << (int)Control.Direction)) == 0) {
                //write
                var data = fifo.ToArray ();
                this.DebugLog (
                    "Write {0} to {1} (0x{2:X})",
                    data.Select (x => x.ToString ()).Aggregate ((x,y) => x + " " + y),
                    device.GetType ().Name,
                    slaveAddress
                );
                fifo.Clear ();
                device.Write (data);
                SetInterrupt (Interrupts.TransferComplete);
                status &= ~((uint)(1 << (int)Status.TransmitDataValid));
            } else {
                //read
                var data = device.Read ();
                foreach (var item in data) {
                    fifo.Enqueue (item);
                }
                this.DebugLog (
                    "Read from {0} (0x{1:X})",
                    device.GetType().Name,
                    slaveAddress,
                    data.Select (x => x.ToString ()).Aggregate ((x,y) => x + " " + y)
                );
                SetInterrupt (Interrupts.TransferComplete);
                status |= (uint)(1 << (int)Status.ReceiverDataValid);
                status &= ~(uint)(1 << (int)Status.BusActive);

            }
        }

        private void Update ()
        {
            if ((interruptStatus & (~interruptMask)) > 0)
            {
                this.NoisyLog ("Irq set");
                IRQ.Set ();
            } 
            else 
            {
                this.NoisyLog ("Irq unset");
                IRQ.Unset ();
            }
        }

        private void ClearInterrupt(params Interrupts[] interrupt)
        {
            foreach (var item in interrupt) 
            {
                interruptStatus &= (uint)~(1<<(int)item);
            }
            Update ();
        }


        private void SetInterrupt(params Interrupts[] interrupt)
        {
            foreach (var item in interrupt) 
            {
                interruptStatus |= (uint)(1 << (int)item);
            }
            Update ();
        }

        private uint timeout;
        private uint interruptMask;
        private uint interruptStatus;
        private uint slaveMonitorPause;
        private uint transferSize;
        private byte slaveAddress;
        private uint status;
        private uint control;
        
        private Queue<byte> fifo = new Queue<byte> ();

        private const int TimeoutRegisterReset = 0x1F; // R_TIME_OUT_REG_RESET
        private const int InterruptMaskRegisterReset = 0x2FF; // R_INTRPT_MASK_REG_RESET 0x2FF
        private const int RegisterCount = (int)Offset.InterruptDisableRegister + 1; // R_MAX 


        private enum Status
        {
            RXReadWrite = 3,
            ReceiverDataValid = 5,
            TransmitDataValid = 6,
            ReceiverOverflow = 7,
            BusActive = 8
        }

        private enum Control
        {
            Direction           = 0x0,
            MasterSlave         = 0x1,
            AddressMode         = 0x2,
            AcknowledgeEnabled  = 0x3,
            Hold                = 0x4,
            SlaveMonitorMode    = 0x5,
            ClearFifo           = 0x6
        }

        private enum Interrupts
        {
            TransferComplete         = 0x0,
            MoreData                 = 0x1,
            NoACK                    = 0x2,
            TransferTimeout          = 0x3,
            MonitoredSlaveReady      = 0x4,
            ReceiveOverflow          = 0x5,
            FifoTransmitOverflow     = 0x6,
            ReceiveTransmitUnderflow = 0x7,
            ArbitrationLost          = 0x9,
        }
                
        private enum Offset
        {
            ControlRegister            = 0x0 , // R_CONTROL_REG
            StatusRegister             = 0x4 , // R_STATUS_REG
            I2CAddressRegister         = 0x8 , // R_I2C_ADDRESS_REG
            I2CDataRegister            = 0xC , // R_I2C_DATA_REG
            InterruptStatusRegister    = 0x10, // R_INTERRUPT_STATUS_REG
            TransferSizeRegister       = 0x14, // R_TRANSFER_SIZE_REG
            SlaveMonitorPauseRegister  = 0x18, // R_SLAVE_MON_PAUSE_REG
            TimeoutRegister            = 0x1C, // R_TIME_OUT_REG
            InterruptMaskRegister      = 0x20, // R_INTRPT_MASK_REG
            InterruptEnableRegister    = 0x24, // R_INTRPT_ENABLE_REG
            InterruptDisableRegister   = 0x28  // R_INTRPT_DISABLE_REG
        }

    }
}

