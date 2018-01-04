//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using System.Linq;

namespace Antmicro.Renode.Peripherals.I2C
{
    public sealed class STM32F7_I2C : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public STM32F7_I2C(Machine machine) : base(machine)
        {
            EventInterrupt = new GPIO();
            ErrorInterrupt = new GPIO();
            registers = CreateRegisters();
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            registers.Reset();
            dataPacket = new Queue<byte>();
            currentSlaveAddress = 0;
        }

        public long Size { get { return 0x400; } }

        public GPIO EventInterrupt { get; private set; }

        public GPIO ErrorInterrupt { get; private set; }

        private DoubleWordRegisterCollection CreateRegisters()
        {
            var map = new Dictionary<long, DoubleWordRegister> { {(long)Registers.Control1, new DoubleWordRegister(this)
                        .WithFlag(0, changeCallback: PeripheralEnabledChange, name: "PE")
                        .WithFlag(1, out transferInterruptEnabled, name: "TXIE")
                        .WithFlag(2, out receiveInterruptEnabled, name: "RXIE")
                        .WithTag("ADDRIE", 3, 1)
                        .WithFlag(4, out nackReceivedInterruptEnabled, name: "NACKIE")
                        .WithFlag(5, out stopDetectionInterruptEnabled, name: "STOPIE")
                        .WithFlag(6, out transferCompleteInterruptEnabled, name: "TCIE")
                        .WithTag("ERRIE", 7, 1).WithTag("DNF", 8, 4)
                        .WithTag("ANFOFF", 12, 1).WithTag("TXDMAEN", 14, 1).WithTag("RXDMAEN", 15, 1).WithTag("SBC", 16, 1).WithTag("NOSTRETCH", 17, 1).WithTag("GCEN", 19, 1)
                        .WithTag("SMBHEN", 20, 1).WithTag("SMBDEN", 21, 1).WithTag("ALERTEN", 22, 1).WithTag("PECEN", 23, 1)
                        .WithChangeCallback((_,__)=>Update())
                }, {
                    (long)Registers.Control2,
                    new DoubleWordRegister(this)
                        .WithTag("HEAD10R", 12, 1).WithTag("STOP", 14, 1).WithTag("NACK", 15, 1).WithTag("PECBYTE", 26, 1)
                        .WithValueField(0, 10, out slaveAddress, name: "SADD") //Changing this from a normal field to a callback requires a change in StartWrite
                        .WithFlag(10, out isReadTransfer, name: "RD_WRN")
                        .WithFlag(11, out use10BitAddressing, name: "ADD10")
                        .WithFlag(13, FieldMode.WriteOneToClear | FieldMode.Read, writeCallback: StartWrite, name: "START")
                        .WithValueField(16, 8, out bytesToTransfer, name: "NBYTES")
                        .WithFlag(24, out reload, name: "RELOAD")
                        .WithFlag(25, out autoEnd, name: "AUTOEND")
                        .WithChangeCallback((_,__)=>Update())
                }, {
                    (long)Registers.OwnAddress1, new DoubleWordRegister(this).WithTag("OA1", 0, 10).WithTag("OA1MODE", 10, 1).WithTag("OA1EN", 15, 1)
                }, {
                    (long)Registers.OwnAddress2, new DoubleWordRegister(this).WithTag("OA2", 1, 7).WithTag("OA2MSK", 8, 3).WithTag("OA2EN", 15, 1)
                }, {
                    (long)Registers.Timing, new DoubleWordRegister(this).WithValueField(0, 32) //written, but ignored
                }, {
                    (long)Registers.InterruptAndStatus, new DoubleWordRegister(this, 1)
                        .WithFlag(0, FieldMode.Read, writeCallback: (_, value)=> {if(value) dataPacket.Clear();}, name: "TXE")
                        .WithFlag(1, out transmitInterruptStatus, FieldMode.Set | FieldMode.Read, name: "TXIS")
                        .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => dataPacket.Any() && isReadTransfer.Value, name: "RXNE")
                        .WithTag("ADDR", 3, 1).WithTag("NACKF", 4, 1)
                        .WithFlag(5, out stopDetection, FieldMode.Read, name: "STOPF")
                        .WithTag("BERR", 8, 1).WithTag("ARLO", 9, 1).WithTag("OVR", 10, 1).WithTag("PECERR", 11, 1).WithTag("TIMEOUT", 12, 1)
                        .WithTag("ALERT", 13, 1).WithTag("BUSY", 15, 1).WithTag("DIR", 16, 1).WithTag("ADDCODE", 17, 7)
                        .WithFlag(6, out transferComplete, FieldMode.Read, name: "TC")
                        .WithFlag(7, out transferCompleteReload, FieldMode.Read, name: "TCR")
                        .WithChangeCallback((_,__)=>Update())
                }, {
                    (long)Registers.InterruptClear, new DoubleWordRegister(this, 0).WithTag("ADDRCF", 3, 1).WithTag("NACKCF", 4, 1)
                        .WithFlag(5, FieldMode.WriteOneToClear, writeCallback: (_, value) => stopDetection.Value = !value, name: "STOPCF")
                        .WithTag("BERRCF", 8, 1).WithTag("ARLOCF", 9, 1).WithTag("OVRCF", 10, 1).WithTag("PECCF", 11, 1).WithTag("TIMOUTCF", 12, 1).WithTag("ALERTCF", 13, 1)
                        .WithChangeCallback((_,__)=>Update())
                }, {
                    (long)Registers.ReceiveData, new DoubleWordRegister(this, 0).WithValueField(0, 8, FieldMode.Read, valueProviderCallback: ReceiveDataRead, name: "RXDATA")
                }, {
                    (long)Registers.TransmitData, new DoubleWordRegister(this, 0).WithValueField(0, 8, writeCallback: TransmitDataWrite, name: "TXDATA")
                }
            };

            return new DoubleWordRegisterCollection(this, map);
        }

        private void PeripheralEnabledChange(bool oldValue, bool newValue)
        {
            if(newValue)
            {
                return;
            }
            stopDetection.Value = false;
            transferComplete.Value = false;
            transferCompleteReload.Value = false;
            transmitInterruptStatus.Value = false;
        }

        private void StartWrite(bool oldValue, bool newValue)
        {
            if(!newValue)
            {
                return;
            }
            transmitInterruptStatus.Value = true;
            transferComplete.Value = false;

            currentSlave = null;

            dataPacket.Clear();
            //This is kinda volatile. If we change slaveAddress setting to a callback action, it might not be set at this moment.
            currentSlaveAddress = (int)(use10BitAddressing.Value ? slaveAddress.Value : ((slaveAddress.Value >> 1) & 0x7F));
            if(!TryGetByAddress(currentSlaveAddress, out currentSlave))
            {
                this.Log(LogLevel.Warning, "Unknown slave at address {0}.", currentSlaveAddress);
                return;
            }

            if(isReadTransfer.Value)
            {
                var data = currentSlave.Read((int)bytesToTransfer.Value);
                foreach(var item in data)
                {
                    dataPacket.Enqueue(item);
                }
                SetTransferCompleteFlags();
            }
        }

        private uint ReceiveDataRead(uint oldValue)
        {
            if(dataPacket.Any())
            {
                var value = dataPacket.Dequeue();
                Update();
                return value;
            }
            this.Log(LogLevel.Warning, "Receive buffer underflow!");
            return 0;
        }

        private void TransmitDataWrite(uint oldValue, uint newValue)
        {
            if(currentSlave == null)
            {
                this.Log(LogLevel.Warning, "Trying to send byte {0} to an unknown slave with address {1}.", newValue, currentSlaveAddress);
                return;
            }
            dataPacket.Enqueue((byte)newValue);
            if(dataPacket.Count == bytesToTransfer.Value)
            {
                currentSlave.Write(dataPacket.ToArray());
                dataPacket.Clear();
                SetTransferCompleteFlags();
                Update();
            }
        }

        private void SetTransferCompleteFlags()
        {
            if(!autoEnd.Value && !reload.Value)
            {
                transferComplete.Value = true;
            }
            if(autoEnd.Value)
            {
                stopDetection.Value = true;
            }
            if(reload.Value)
            {
                transferCompleteReload.Value = true;
            }
            else
            {
                transmitInterruptStatus.Value = false; //this is a guess based on a driver
            }
        }

        private void Update()
        {
            var value = (transferCompleteInterruptEnabled.Value && (transferCompleteReload.Value || transferComplete.Value))
                || (transferInterruptEnabled.Value && transmitInterruptStatus.Value)
                || (receiveInterruptEnabled.Value && isReadTransfer.Value && dataPacket.Count > 0) //RXNE is calculated dynamically
                || (stopDetectionInterruptEnabled.Value && stopDetection.Value)
                || (nackReceivedInterruptEnabled.Value && false); //TODO: implement NACKF
            EventInterrupt.Set(value);
        }

        private IFlagRegisterField transferInterruptEnabled;
        private IFlagRegisterField receiveInterruptEnabled;
        private IFlagRegisterField nackReceivedInterruptEnabled;
        private IFlagRegisterField stopDetectionInterruptEnabled;
        private IFlagRegisterField transferCompleteInterruptEnabled;

        private IFlagRegisterField transmitInterruptStatus;
        private IFlagRegisterField transferComplete;
        private IFlagRegisterField transferCompleteReload;
        private IFlagRegisterField stopDetection;

        private IValueRegisterField bytesToTransfer;
        private IValueRegisterField slaveAddress;
        private IFlagRegisterField isReadTransfer;
        private IFlagRegisterField use10BitAddressing;
        private IFlagRegisterField reload;
        private IFlagRegisterField autoEnd;

        private DoubleWordRegisterCollection registers;

        private II2CPeripheral currentSlave;
        private Queue<byte> dataPacket;
        private int currentSlaveAddress;

        private enum Registers
        {
            Control1 = 0x00,
            Control2 = 0x04,
            OwnAddress1 = 0x08,
            OwnAddress2 = 0x0C,
            Timing = 0x10,
            Timeout = 0x14,
            InterruptAndStatus = 0x18,
            InterruptClear = 0x1C,
            PacketErrorChecking = 0x20,
            ReceiveData = 0x24,
            TransmitData = 0x28
        }
    }
}

