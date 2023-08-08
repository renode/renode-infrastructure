//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class EFM32GGI2CController : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral
    {

        public EFM32GGI2CController(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
        }

        // TODO: Implement checks on if the controller is enabled where needed, ie if bit 0 in i2cn_trl is set
        // TODO: Implment all interrupt handling p 419 EFM32GG Ref Man
        // TODO: handle and send ack/nack

        public virtual uint ReadDoubleWord(long offset)
        {
            this.Log(LogLevel.Noisy, "read {0}", (Registers)offset);

            switch((Registers)offset)
            {
            case Registers.I2Cn_CTRL:
                this.Log(LogLevel.Noisy, "register value = {0}", i2cn_ctrl);
                return i2cn_ctrl;
            case Registers.I2Cn_CMD:
                return 0;
            case Registers.I2Cn_STATE:
                this.Log(LogLevel.Noisy, "register value = {0}", i2cn_state);
                return i2cn_state;
            case Registers.I2Cn_STATUS:
                this.Log(LogLevel.Noisy, "register value = {0}", i2cn_status);
                return i2cn_status;
            case Registers.I2Cn_CLKDIV:
                this.Log(LogLevel.Noisy, "register value = {0}", i2cn_clkdiv);
                return i2cn_clkdiv;
            case Registers.I2Cn_SADDR:
                this.Log(LogLevel.Noisy, "register value = {0}", i2cn_saddr);
                return i2cn_saddr;
            case Registers.I2Cn_RXDATA:
                // Set underflow flag if buffer is empty
                uint i2cn_rxdata = 0;
                if(!rxBufferFull)
                {
                    SetInterrupt(1 << (int)Interrupts.RXUF);
                }
                else
                {
                    i2cn_rxdata = rxBuffer;
                    rxBuffer = 0;
                    rxBufferFull = false;
                    ShiftRxData();
                }
                this.Log(LogLevel.Noisy, "register value = {0}", i2cn_rxdata);
                return i2cn_rxdata;
            case Registers.I2Cn_RXDATAP:
                this.Log(LogLevel.Noisy, "register value = {0}", rxBuffer);
                return rxBuffer;
            case Registers.I2Cn_TXDATA:
                return 0;
            case Registers.I2Cn_IF:
                this.Log(LogLevel.Noisy, "register value = {0}", i2cn_if);
                return i2cn_if;
            case Registers.I2Cn_IFS:
                return 0;
            case Registers.I2Cn_IFC:
                return 0;
            case Registers.I2Cn_IEN:
                this.Log(LogLevel.Noisy, "register value = {0}", i2cn_ien);
                return i2cn_ien;
            case Registers.I2Cn_ROUTE:
                this.Log(LogLevel.Noisy, "register value = {0}", i2cn_route);
                return i2cn_route;
            default:
                this.Log(LogLevel.Warning, "Unexpected read at 0x{0:X}.", offset);
                return 0;
            }
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            this.Log(LogLevel.Noisy, "write {0}, value 0x{1:X}", (Registers)offset, value);

            switch((Registers)offset)
            {
            case Registers.I2Cn_CTRL:
                HandleCtrl(value);
                break;
            case Registers.I2Cn_CMD:
                HandleCommand(value);
                break;
            case Registers.I2Cn_STATE:
                break;
            case Registers.I2Cn_STATUS:
                break;
            case Registers.I2Cn_CLKDIV:
                i2cn_clkdiv = value;
                break;
            case Registers.I2Cn_SADDR:
                i2cn_saddr = value;
                break;
            case Registers.I2Cn_RXDATA:
                break;
            case Registers.I2Cn_RXDATAP:
                break;
            case Registers.I2Cn_TXDATA:
                LoadTxData(value);
                break;
            case Registers.I2Cn_IF:
                break;
            case Registers.I2Cn_IFS:
                SetInterrupt(value);
                break;
            case Registers.I2Cn_IFC:
                ClearInterrupt(value);
                break;
            case Registers.I2Cn_IEN:
                EnableInterrupt(value);
                break;
            case Registers.I2Cn_ROUTE:
                i2cn_route = value;
                break;
            default:
                this.Log(LogLevel.Warning, "Unexpected write at 0x{0:X}, value 0x{1:X}.", offset, value);
                break;
            }
        }

        public override void Reset()
        {
            this.Log(LogLevel.Noisy, "Reset");
            i2cn_ctrl = 0;
            i2cn_cmd = 0;
            i2cn_state = 0;
            i2cn_status = 0;
            i2cn_clkdiv = 0;
            i2cn_saddr = 0;
            i2cn_if = 0;
            i2cn_ien = 0;
            i2cn_route = 0;
            rxBuffer = 0;
            rxBufferFull = false;
            txBuffer = 0;
            txBufferFull = false;
            rxShiftRegister = 0;
            rxShiftRegisterFull = false;
            txShiftRegister = 0;
            txShiftRegisterFull = false;
            transferState = TransferState.Idle;
            txpacket.Clear();
            rxpacket.Clear();
        }

        private void HandleCtrl(uint value)
        {
            if(i2cn_ctrl == value)
            {
                this.Log(LogLevel.Noisy, "Received Ctrl register update but value is same as current {0}", (Commands)i2cn_ctrl);
                return;
            }
            // If EN bit in I2Cn_CTRL is cleared then reset internal state and terminate any ongoing transfers 
            if(((i2cn_ctrl & 0x1) == 0x1) && ((~value & 0x1) == 1))
            {
                // TODO: Abort ongoing transfers.
                Reset();
            }
            // If EN bit is set, i2c controller is to be enabled. Set TXBL flag if tx buffer is empty
            if(((value & 0x1) == 0x1) && ((~i2cn_ctrl & 0x1) == 1))
            {
                if(!txBufferFull && txShiftRegisterFull)
                {
                    SetStatus(Status.TXBL);
                    SetInterrupt(1 << (int)Interrupts.TXBL);
                }
            }
            i2cn_ctrl = value;
            this.Log(LogLevel.Noisy, "Changed Ctrl register to {0}", (Commands)i2cn_ctrl);
        }

        private void HandleCommand(uint value)
        {
            // I2Cn_CMD register is W1, ie write-only and only write of 1 has any action
            i2cn_cmd = value & 0xFF; //Only bits 0-7 used in I2Cn_CMD register

            if((i2cn_cmd & (uint)(Commands.START)) == (uint)(Commands.START))
            {
                // SDA line pulled low while SCL line is high
                SetInterrupt(1 << (int)Interrupts.START);
                // Since we send a packet list a restart means we should send the present list and start on a new
                if(((int)transferState & (int)TransferState.StartTrans) == (int)TransferState.StartTrans)
                {
                    transferState = TransferState.RestartTrans;
                }
                else
                {
                    transferState = TransferState.StartTrans;
                }
                this.Log(LogLevel.Noisy, "Received START Command");
                HandleTransfer();
            }
            if((i2cn_cmd & (uint)(Commands.STOP)) == (uint)(Commands.STOP))
            {
                // SDA line pulled high while SCL line is high
                SetInterrupt(1 << (int)Interrupts.MSTOP);
                transferState = TransferState.StopTrans;
                this.Log(LogLevel.Noisy, "Received STOP Command");
                HandleTransfer();
            }
            if((i2cn_cmd & (uint)(Commands.ACK)) == (uint)(Commands.ACK))
            {
                // TODO: set an internal variable to track and possibly use this, now slave does not expect ACK/NACK
                this.Log(LogLevel.Noisy, "Received ACK Command");
            }
            if((i2cn_cmd & (uint)(Commands.NACK)) == (uint)(Commands.NACK))
            {
                // TODO: set an internal variable to track and possibly use this, now slave does not expect ACK/NACK
                this.Log(LogLevel.Noisy, "Received NACK Command");
            }
            if((i2cn_cmd & (uint)(Commands.CONT)) == (uint)(Commands.CONT))
            {
                // TODO: Not implemented yet, set internal variable
                this.Log(LogLevel.Noisy, "Received CONT Command");
            }
            if((i2cn_cmd & (uint)(Commands.ABORT)) == (uint)(Commands.ABORT))
            {
                // TODO: Not implemented yet
                this.Log(LogLevel.Noisy, "Received ABORT Command");
            }
            if((i2cn_cmd & (uint)(Commands.CLEARTX)) == (uint)(Commands.CLEARTX))
            {
                // Clear transmit buffer and shift register, and TXBL flags in status register and interrupt flag register
                txBuffer = 0;
                txBufferFull = false;
                txShiftRegister = 0;
                txShiftRegisterFull = false;
                txpacket.Clear();
                ClearStatus(Status.TXBL);
                ClearInterrupt((uint)Interrupts.TXBL);
                this.Log(LogLevel.Noisy, "Received CLEARTX Command");
            }
            if((i2cn_cmd & (uint)(Commands.CLEARPC)) == (uint)(Commands.CLEARPC))
            {
                // TODO: Not implemented yet
                this.Log(LogLevel.Noisy, "Received CLEARPC Command");
            }
        }

        private void LoadTxData(uint value)
        {
            // Check TXBL flags in status register and interrupt flag register to see if there is room for data in transmit buffer
            // If not then set TXOF flag in status register and return without loading data.
            this.Log(LogLevel.Noisy, "LoadTxData - status reg = {0}", i2cn_status);
            if(CheckStatus(Status.TXBL) || (!txBufferFull && !txShiftRegisterFull))
            {
                this.Log(LogLevel.Noisy, "Loaded transmit buffer with data {0}", value);
                txBuffer = value;
                txBufferFull = true;
                if(CheckStatus(Status.TXBL))
                {
                    ClearStatus(Status.TXBL);
                    ClearInterrupt((uint)Interrupts.TXBL);
                }
                ShiftTxData();
            }
            else
            {
                this.Log(LogLevel.Noisy, "Transmit buffer is full - overflow interrupt flag set");
                SetInterrupt(1 << (int)Interrupts.TXOF);
            }
        }

        private void ShiftTxData()
        {
            // Only set TXC after transmission, how do we check this? Internal state?
            if((txBufferFull == false) && (txShiftRegisterFull == false))
            {
                SetStatus(Status.TXC);
                SetInterrupt(1 << (int)Interrupts.TXC);
            }
            // Check if shift register is empty 
            // If so then move data to shift register and set TXBL flags in status register and interrupt flag register
            if((txShiftRegisterFull == false) && txBufferFull)
            {
                txShiftRegister = txBuffer;
                txShiftRegisterFull = true;
                txBuffer = 0;
                txBufferFull = false;
                ClearStatus(Status.TXC);
                // TXC interrupt can only be cleared by software
                // Signal that transmit buffer is ready for receiving data
                SetStatus(Status.TXBL);
                SetInterrupt(1 << (int)Interrupts.TXBL);
                HandleTransfer();
            }
            // Check if BUSHOLD, then start sending slave address
            if(txShiftRegisterFull && CheckInterrupt(Interrupts.BUSHOLD))
            {
                HandleTransfer();
            }
        }

        private void WriteToSlave(int slaveAddress, List<byte> data)
        {
            II2CPeripheral slave;
            if(TryGetByAddress(slaveAddress, out slave))
            {
                slave.Write(data.ToArray());
            }
            else
            {
                this.Log(LogLevel.Warning, "Trying to write to nonexisting slave with address \"{0}\"", slaveAddress);
            }
        }

        private void HandleTransfer()
        {
            // If transmit shift register contains data, package it and handle TXBL flags in status register and interrupt flag register
            // TODO: see section 16.3.7.4 in EFM32GG Ref manual for master transmitter
            // TODO: controller acting as slave
            this.Log(LogLevel.Noisy, "HandleTransfer - txShiftRegister = {0}", txShiftRegister);
            switch((TransferState)transferState)
            {
            case TransferState.Idle:
                break;
            case TransferState.StartTrans:
                // Check if data is available in txShiftRegister, if so then use it, else BUS_HOLD
                if(txShiftRegisterFull)
                {
                    if(txBufferFull == false)
                    {
                        // Signal that transmit buffer is ready for receiving data
                        if(CheckStatus(Status.TXBL) == false)
                        {
                            this.Log(LogLevel.Noisy, "Setting TXBL status and intr flag");
                            SetStatus(Status.TXBL);
                            SetInterrupt(1 << (int)Interrupts.TXBL);
                        }
                    }
                    // Create the packet list with bytes for the save device
                    // Check the current status of the packet list and add whats needed
                    // TODO: currently no verification on the validity of the data
                    switch(txpacket.Count)
                    {
                    case 0:
                        this.Log(LogLevel.Noisy, "HandleTransfer - packet count = 0");
                        // Add mode as first byte in packet list
                        // Calculate what the mode should be from the device slave address
                        // First byte in tx buffer should be the 7-bit slave device address + Read/Write bit
                        slaveAddressForPacket = (byte)((txShiftRegister >> 1) & 0x7F);
                        if((txShiftRegister & 0x1) == 1)
                        {
                            // Mode is Read data from slave
                            txpacket.Add((byte)(PacketStates.ReadData));
                            // Since Read data is a command and not followed by address, 
                            // add the stored address and change to ReadTrans
                            txpacket.Add((byte)(registerAddress));
                            transferState = TransferState.ReadTrans;
                        }
                        else
                        {
                            // Mode is Write data to slave
                            txpacket.Add((byte)(PacketStates.SendData));
                        }
                        // Clear to enable more data
                        txShiftRegister = 0;
                        txShiftRegisterFull = false;
                        // We need to emulate receiving an ACK for the data byte sent
                        SetInterrupt(1 << (int)Interrupts.ACK);
                        this.Log(LogLevel.Noisy, "HandleTransfer - packet count = 0 - have set ACK interrupt");
                        break;
                    case 1:
                        this.Log(LogLevel.Noisy, "HandleTransfer - packet count = 1");
                        // Add register address
                        // Second byte in tx buffer should be the register address 
                        txpacket.Add((byte)(txShiftRegister & 0xFF));
                        // Save the address in case of upcoming Read
                        registerAddress = (byte)txShiftRegister;
                        // Clear to enable more data
                        txShiftRegister = 0;
                        txShiftRegisterFull = false;
                        // We need to emulate receiving an ACK for the data byte sent
                        SetInterrupt(1 << (int)Interrupts.ACK);
                        break;
                    case 2:
                        this.Log(LogLevel.Noisy, "HandleTransfer - packet count = 2");
                        // If read mode of single register then we are done and we can change to EndTransfer state
                        // If multiple reads then we could continue, how to know if this is the case?
                        // If mode is write, add value for operation
                        txpacket.Add((byte)(txShiftRegister & 0xFF));
                        // Clear to enable more data
                        txShiftRegister = 0;
                        txShiftRegisterFull = false;
                        // We need to emulate receiving an ACK for the data byte sent
                        SetInterrupt(1 << (int)Interrupts.ACK);
                        break;
                    default:
                        this.Log(LogLevel.Noisy, "HandleTransfer - packet count = ", txpacket.Count);
                        break;
                    }
                    ShiftTxData();
                }
                else
                {
                    this.Log(LogLevel.Noisy, "Setting state and intr flag BUSHOLD");
                    SetInterrupt(1 << (int)Interrupts.BUSHOLD);
                    i2cn_state |= (uint)StateReg.BUSHOLD;
                }
                // Change state to send packet list if we are finished - how do we know?
                // TODO: determine if we are finished 
                if(((int)transferState & (int)TransferState.ReadTrans) == (int)TransferState.ReadTrans)
                {
                    HandleTransfer();
                }
                break;
            case TransferState.RestartTrans:
                // Restart was issued - send packet list and change transferState to StartTrans
                // Use the registration point - ie memory address - for slave communication
                if(txpacket.Count > 0)
                {
                    currentAddress = (int)slaveAddressForPacket << 1;
                    WriteToSlave(currentAddress, txpacket);
                    txpacket.Clear();
                }
                transferState = TransferState.StartTrans;
                HandleTransfer();
                break;
            case TransferState.ReadTrans:
                // Read was issued - send packet list and then read
                // Use the registration point - ie memory address - for slave communication
                if(txpacket.Count > 0)
                {
                    currentAddress = (int)slaveAddressForPacket << 1;
                    WriteToSlave(currentAddress, txpacket);
                    txpacket.Clear();
                }
                ReadData();
                HandleTransfer();
                break;
            case TransferState.StopTrans:
                // STOP was issued - send packet list 
                if(txpacket.Count > 0)
                {
                    currentAddress = (int)slaveAddressForPacket << 1;
                    WriteToSlave(currentAddress, txpacket);
                    txpacket.Clear();
                }
                // If automatic stop on empty is enabled signal STOP
                if((i2cn_ctrl & (uint)(Control.AUTOSE)) == (uint)(Control.AUTOSE))
                {
                    SetInterrupt(1 << (int)Interrupts.MSTOP);
                    transferState = TransferState.Idle;
                }
                break;
            default:
                break;
            }
        }

        private void ShiftRxData()
        {
            // Check if the receive buffer is empty, if not then BUSHOLD
            if(rxBufferFull && rxShiftRegisterFull)
            {
                SetInterrupt(1 << (int)Interrupts.BUSHOLD);
                i2cn_state |= (uint)StateReg.BUSHOLD;
                return;
            }
            if((rxBufferFull == false) && rxShiftRegisterFull)
            {
                rxBuffer = rxShiftRegister;
                rxBufferFull = true;
                rxShiftRegister = 0;
                rxShiftRegisterFull = false;
                SetStatus(Status.RXDATAV);
                SetInterrupt(1 << (int)Interrupts.RXDATAV);
                this.Log(LogLevel.Noisy, "Data byte available for reading ({0})", rxBuffer);
            }
            // Strip away the CRC and do not pass it through
            // TODO: add CRC verification as well
            if(rxpacket.Count == 1)
            {
                rxpacket.Clear();
            }
            if(rxpacket.Count > 1)
            {
                AddRxData();
            }
        }

        private void AddRxData()
        {
            if(rxShiftRegisterFull == false)
            {
                // Copy first item in list to receiver shift buffer and remove it
                rxShiftRegister = rxpacket.ElementAt(0);
                rxShiftRegisterFull = true;
                rxpacket.RemoveAt(0);
                this.Log(LogLevel.Noisy, "AddRxData - rxShiftRegister = {0}", rxShiftRegister);
            }
            ShiftRxData();
        }

        private void ReadData()
        {
            // Fetch packet list from slave device
            II2CPeripheral slave;
            transferState = TransferState.Idle;
            if(!TryGetByAddress(currentAddress, out slave))
            {
                this.Log(LogLevel.Warning, "Trying to read from nonexisting slave with address \"{0}\"", currentAddress);
                return;
            }
            byte[] rxArray = slave.Read();
            // Packet list should have a least one byte plus a CRC byte
            if(rxArray.Length > 1)
            {
                rxpacket = new List<byte>(rxArray);
                this.Log(LogLevel.Noisy, "Read data - packet length = {0}", rxpacket.Count);
                AddRxData();
            }
        }

        private bool CheckStatus(Status status)
        {
            bool result = false;
            if((i2cn_status & (uint)(1 << (int)status)) == (uint)(1 << (int)status))
            {
                result = true;
            }
            return result;
        }

        private void SetStatus(Status status)
        {
            i2cn_status |= (uint)(1 << (int)status);
        }

        private void ClearStatus(Status status)
        {
            i2cn_status &= ~((uint)status);
        }

        public GPIO IRQ { get; private set; }

        private bool CheckInterrupt(Interrupts interrupt)
        {
            bool result = false;
            if((i2cn_if & (uint)(1 << (int)interrupt)) == (uint)(1 << (int)interrupt))
            {
                result = true;
            }
            return result;
        }

        private void UpdateInterrupt()
        {
            if((i2cn_if & i2cn_ien) > 0)
            {
                this.Log(LogLevel.Noisy, "UpdateInterrupt - Irq set");
                IRQ.Set();
            }
            else
            {
                this.Log(LogLevel.Noisy, "UpdateInterrupt - Irq cleared");
                IRQ.Unset();
            }
        }

        private void SetInterrupt(uint interruptMask)
        {
            // Only act if controller is enabled and on enabled interrupts
            uint enableInterruptMask = i2cn_ien & interruptMask;
            this.Log(LogLevel.Noisy, "SetInterrupt - enableInterruptMask = {0}", Convert.ToString(enableInterruptMask));
            this.Log(LogLevel.Noisy, "SetInterrupt i2cn_ctrl = {0}", Convert.ToString(i2cn_ctrl));
            // TODO: enable this check again once the Bitbanding.cs issue have been resolved
            //            if ((i2cn_ctrl & 0x1) == 0x1)
            //            {
            i2cn_if |= enableInterruptMask;
            UpdateInterrupt();
            //            }
        }

        private void ClearInterrupt(uint interruptMask)
        {
            // Only act if controller is enabled and on enabled interrupts
            uint enableInterruptMask = i2cn_ien & interruptMask;
            // TODO: enable this check again once the Bitbanding.cs issue have been resolved
            //            if ((i2cn_ctrl & 0x1) == 0x1)
            //            {
            i2cn_if &= ~enableInterruptMask;
            this.Log(LogLevel.Noisy, "ClearInterrupt i2cn_if = {0}", Convert.ToString(i2cn_if));
            UpdateInterrupt();
            //            }
        }

        private void EnableInterrupt(uint interruptMask)
        {
            i2cn_ien |= interruptMask;
            // Clear disabled interrupts
            i2cn_if &= interruptMask;
            UpdateInterrupt();
        }

        private uint i2cn_ctrl;
        private uint i2cn_cmd;
        private uint i2cn_state;
        private uint i2cn_status;
        private uint i2cn_clkdiv;
        private uint i2cn_saddr;
        private uint i2cn_if;
        private uint i2cn_ien;
        private uint i2cn_route;

        private uint txShiftRegister;
        private uint rxShiftRegister;
        private bool txShiftRegisterFull;
        private bool rxShiftRegisterFull;
        private uint txBuffer;
        private uint rxBuffer;
        private bool txBufferFull;
        private bool rxBufferFull;
        private TransferState transferState;

        private byte slaveAddressForPacket;
        private byte registerAddress;
        private int currentAddress;

        private List<byte> txpacket = new List<byte>();
        private List<byte> rxpacket = new List<byte>();

        // Source: pages 434-445 in EFM32GG Reference Manual
        private enum Registers
        {
            I2Cn_CTRL = 0x000, // Control Register - Read-Write
            I2Cn_CMD = 0x004, // Command Register - Write-1-only
            I2Cn_STATE = 0x008, // State Register - Read-only
            I2Cn_STATUS = 0x00C, // Status Register - Read-only
            I2Cn_CLKDIV = 0x010, // Clock Division Register - Read-Write
            I2Cn_SADDR = 0x014, // Slave Address Register - Read-Write
            I2Cn_SADDRMASK = 0x018, // Slave Address Mask Register - Read-Write
            I2Cn_RXDATA = 0x01C, // Receive Buffer Data Register - Read-only
            I2Cn_RXDATAP = 0x020, // Receive Buffer Data Peek Register - Read-only 
            I2Cn_TXDATA = 0x024, // Transmit Buffer Data Register - Write-only
            I2Cn_IF = 0x028, // Interrupt Flag Register - Read-only
            I2Cn_IFS = 0x02C, // Interrupt Flag Set Register - Write-1-only
            I2Cn_IFC = 0x030, // Interrupt Flag Clear Register - Write-1-only
            I2Cn_IEN = 0x034, // Interrupt Enable Register - Read-Write
            I2Cn_ROUTE = 0x038  // I/O Routing Register - Read-Write
        }

        // Bits in the Control register (0-31)
        private enum Control
        {
            EN = 0x0,   // I2C Enable
            SLAVE = 0x1,   // Addressable as slave
            AUTOACK = 0x2,   // Automatic acknowledge
            AUTOSE = 0x3,   // Automatic STOP when empty
            AUTOSN = 0x4,   // Automatic STOP on NACK
            ARBDIS = 0x5,   // Arbitration disable
            GCAMEN = 0x6,   // General Call Address Match Enable
            CLHR_0 = 0x8,   // Clock Low High Ratio, bit 0
            CLHR_1 = 0x9,   // Clock Low High Ratio, bit 1
            BITO_0 = 0x12,  // Bus Idle Timeout, bit 0
            BITO_1 = 0x13,  // Bus Idle Timeout, bit 1
            GIBITO = 0x15,  // Go Idle on Bus Idle Timeout
            CLTO_0 = 0x16,  // Clock Low Timeout, bit 0
            CLTO_1 = 0x17,  // Clock Low Timeout, bit 1
            CLTO_2 = 0x18   // Clock Low Timeout, bit 2

        }

        // Values in the Command register (Only bits 0-7 used, 8-31 reserved)
        private enum Commands
        {
            START = 0x01,  // Send start condition
            STOP = 0x02,  // Send stop condition
            ACK = 0x04,  // Send ACK
            NACK = 0x08,  // Send NACK
            CONT = 0x10,  // Continue transmission
            ABORT = 0x20,  // Abort transmission
            CLEARTX = 0x40,  // Clear TX
            CLEARPC = 0x80   // Clear pending commands
        }

        // Values in the State register (0-31) - Only some states, see pages 423-424 in EFM32GG ref manual
        private enum StateVal
        {
            StartTrans = 0x57,
            AddrWAck = 0x97,
            AddrWNack = 0x9F,
            DataWAck = 0xD7,
            DataWNack = 0xDF
        }

        // Bits in the State register (0-31)
        private enum StateReg
        {
            BUSY = 0x0,  // Bus busy
            MASTER = 0x1,  // Master
            TRANSMIT = 0x2,  // Transmitter 
            NACKED = 0x3,  // Nack Received
            BUSHOLD = 0x4,  // Bus held
            STATE_0 = 0x5,  // Transmission state, bit 0
            STATE_1 = 0x6,  // Transmission state, bit 1
            STATE_2 = 0x7   // Transmission state, bit 2
        }

        // Bits in the Status register (0-31)
        private enum Status
        {
            PSTART = 0x0,  // Pending start
            PSTOP = 0x1,  // Pending stop
            PACK = 0x2,  // Pending ACK
            PNACK = 0x3,  // Pending NACK
            PCONT = 0x4,  // Pending continue transmission
            PABORT = 0x5,  // Pending abort transmission
            TXC = 0x6,  // TX complete
            TXBL = 0x7,  // TX buffer level
            RXDATAV = 0x8   // RX data valid
        }

        // Bits in the Interrupt Flag Register (0-31)
        private enum Interrupts
        {
            START = 0x00, // START condition Interrupt Flag
            RSTART = 0x01, // Repeated START condition Interrupt Flag
            ADDR = 0x02, // Address Interrupt Flag
            TXC = 0x03, // Transfer Completed Interrupt Flag
            TXBL = 0x04, // Transmit Buffer Level Interrupt Flag
            RXDATAV = 0x05, // Receive Data Valid Interrupt Flag
            ACK = 0x06, // Acknowledge Received Interrupt Flag
            NACK = 0x07, // Not Acknowledge Received Interrupt Flag
            MSTOP = 0x08, // Master STOP Condition Interrupt Flag
            ARBLOST = 0x09, // Arbitration Lost Interrupt Flag
            BUSERR = 0x0A, // Bus Error Interrupt Flag
            BUSHOLD = 0x0B, // Bus Held Interrupt Flag
            TXOF = 0x0C, // Transmit Buffer Overflow Interrupt Flag
            RXUF = 0x0D, // Receive Buffer Underflow Interrupt Flag
            BITO = 0x0E, // Bus Idle Timeout Interrupt Flag
            CLTO = 0x0F, // Clock Low Timeout Interrupt Flag
            SSTOP = 0x10  // Slave STOP condition Interrupt Flag
        }

        // Transfer state enumaration
        private enum TransferState
        {
            Idle = 0x0,
            StartTrans = 0x1,
            RestartTrans = 0x2,
            ReadTrans = 0x3,
            StopTrans = 0x4
        }

        private enum PacketStates
        {
            ReadData = 0xFC,
            SendData = 0xFD
        }
    }
}

