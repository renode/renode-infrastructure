//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public sealed class STM32F7_I2C : SimpleContainer<II2CPeripheral>, II2CPeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public STM32F7_I2C(IMachine machine) : base(machine)
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
            txData = new Queue<byte>();
            rxData = new Queue<byte>();
            currentSlaveAddress = 0;
            transferOutgoing = false;
            EventInterrupt.Unset();
            ErrorInterrupt.Unset();
            masterMode = false;
        }

        public void Write(byte[] data)
        {
            // RM0444 Rev 5, p.991/1390
            // "0: Write transfer, slave enters receiver mode."
            transferOutgoing = false;

            rxData.EnqueueRange(data);
        }

        public byte[] Read(int count = 1)
        {
            if(!addressMatched.Value)
            {
                // Note 1:
                // RM0444 Rev 5, p.991/1390
                // "1: Read transfer, slave enters transmitter mode."
                // Note 2:
                // this is a workaround for the protocol not supporting start/stop bits
                transferOutgoing = (count > 0);
                bytesToTransfer.Value = (uint)count;
                addressMatched.Value = true;
                Update();
            }

            if(txData.Count >= (int)bytesToTransfer.Value)
            {
                // STOP condition
                stopDetection.Value = true;
                transmitInterruptStatus = false;
                addressMatched.Value = false;
                Update();
            }
            else
            {
                // TODO: return partial results
                return new byte[0];
            }

            var result = new byte[count];
            for(var i = 0; i < count; i++)
            {
                if(!txData.TryDequeue(out result[i]))
                {
                    return new byte[0];
                }
            }
            return result;
        }

        public void FinishTransmission()
        {
        }

        public long Size { get { return 0x400; } }

        public GPIO EventInterrupt { get; private set; }

        public GPIO ErrorInterrupt { get; private set; }

        public bool RxNotEmpty => rxData.Count > 0;
        
        public bool OwnAddress1Enabled => ownAddress1Enabled.Value;

        private DoubleWordRegisterCollection CreateRegisters()
        {
            var map = new Dictionary<long, DoubleWordRegister> { {
                    (long)Registers.Control1, new DoubleWordRegister(this)
                        .WithFlag(0, writeCallback: PeripheralEnabledWrite, name: "PE")
                        .WithFlag(1, out transferInterruptEnabled, name: "TXIE")
                        .WithFlag(2, out receiveInterruptEnabled, name: "RXIE")
                        .WithFlag(3, out addressMatchedInterruptEnabled, name: "ADDRIE")
                        .WithFlag(4, out nackReceivedInterruptEnabled, name: "NACKIE")
                        .WithFlag(5, out stopDetectionInterruptEnabled, name: "STOPIE")
                        .WithFlag(6, out transferCompleteInterruptEnabled, name: "TCIE")
                        .WithTag("ERRIE", 7, 1)
                        .WithTag("DNF", 8, 4)
                        .WithTag("ANFOFF", 12, 1)
                        .WithReservedBits(13, 1)
                        .WithTag("TXDMAEN", 14, 1)
                        .WithTag("RXDMAEN", 15, 1)
                        .WithTag("SBC", 16, 1)
                        .WithFlag(17, out noStretch, name: "NOSTRETCH")
                        .WithTag("WUPEN", 18, 1)
                        .WithTag("GCEN", 19, 1)
                        .WithTag("SMBHEN", 20, 1)
                        .WithTag("SMBDEN", 21, 1)
                        .WithTag("ALERTEN", 22, 1)
                        .WithTag("PECEN", 23, 1)
                        .WithReservedBits(24, 8)
                        .WithChangeCallback((_,__) => Update())
                }, {
                    (long)Registers.Control2,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 10, out slaveAddress, name: "SADD") //Changing this from a normal field to a callback requires a change in StartWrite
                        .WithFlag(10, out isReadTransfer, name: "RD_WRN")
                        .WithFlag(11, out use10BitAddressing, name: "ADD10")
                        .WithTag("HEAD10R", 12, 1)
                        .WithFlag(13, out start, name: "START")
                        .WithFlag(14, out stop, name: "STOP")
                        .WithTag("NACK", 15, 1)
                        .WithValueField(16, 8, out bytesToTransfer, name: "NBYTES")
                        .WithFlag(24, out reload, name: "RELOAD")
                        .WithFlag(25, out autoEnd, name: "AUTOEND")
                        .WithTag("PECBYTE", 26, 1)
                        .WithReservedBits(27, 5)
                        .WithWriteCallback((oldVal, newVal) =>
                        {
                            uint oldStart = (oldVal >> 13) & 1;
                            uint oldBytesToTransfer = (oldVal >> 16) & 0xFF;

                            if(start.Value && stop.Value)
                            {
                                this.Log(LogLevel.Warning, "Setting START and STOP at the same time, ignoring the transfer");
                            }
                            else if(start.Value)
                            {
                                StartTransfer();
                            }
                            else if(stop.Value)
                            {
                                StopTransfer();
                            }

                            if(!start.Value)
                            {
                                if(bytesToTransfer.Value > 0 && masterMode && transferCompleteReload.Value && currentSlave != null)
                                {
                                    ExtendTransfer();
                                }
                            }

                            if(oldStart == 1)
                            {
                                if(oldBytesToTransfer != bytesToTransfer.Value)
                                {
                                    this.Log(LogLevel.Error, "Changing NBYTES when START is set is not permitted");
                                }
                            }

                            start.Value = false;
                            stop.Value = false;
                        })
                        .WithChangeCallback((_,__) => Update())
                }, {
                    (long)Registers.OwnAddress1, new DoubleWordRegister(this)
                        .WithValueField(0, 10, out ownAddress1, name: "OA1")
                        .WithFlag(10, out ownAddress1Mode, name: "OA1MODE")
                        .WithReservedBits(11, 4)
                        .WithFlag(15, out ownAddress1Enabled, name: "OA1EN")
                        .WithReservedBits(16, 16)
                        .WithWriteCallback((_, val) => 
                            this.Log(LogLevel.Info, "Slave address 1: 0x{0:X}, mode: {1}, status: {2}", ownAddress1.Value, ownAddress1Mode.Value ? "10-bit" : "7-bit", ownAddress1Enabled.Value ? "enabled" : "disabled")
                        )
                }, {
                    (long)Registers.OwnAddress2, new DoubleWordRegister(this)
                        .WithReservedBits(0, 1)
                        .WithValueField(1, 7, out ownAddress2, name: "OA2")
                        .WithValueField(8, 3, out ownAddress2Mask, name: "OA2MSK")
                        .WithReservedBits(11, 4)
                        .WithFlag(15, out ownAddress2Enabled, name: "OA2EN")
                        .WithReservedBits(16, 16)
                        .WithWriteCallback((_, val) =>
                            this.Log(LogLevel.Info, "Slave address 2: 0x{0:X}, mask: 0x{1:X}, status: {2}", ownAddress2.Value, ownAddress2Mask.Value, ownAddress2Enabled.Value ? "enabled" : "disabled")
                        )
                }, {
                    (long)Registers.Timing, new DoubleWordRegister(this)
                        .WithTag("SCLL", 0, 8)
                        .WithTag("SCLH", 8, 8)
                        .WithTag("SDADEL", 16, 4)
                        .WithTag("SCLDEL", 20, 4)
                        .WithReservedBits(24, 4)
                        .WithTag("PRESC", 28, 4)
                }, {
                    (long)Registers.InterruptAndStatus, new DoubleWordRegister(this, 1)
                        .WithFlag(0,
                            valueProviderCallback: _ => txData.Count == 0,
                            writeCallback: (_, value)=> 
                            {
                                if(value)
                                {
                                    txData.Clear();
                                }
                            }, name: "TXE")
                        .WithFlag(1, 
                            valueProviderCallback: _ => transmitInterruptStatus,
                            writeCallback: (_, val) =>
                            {
                                if(!noStretch.Value)
                                {
                                    return;
                                }
                                transmitInterruptStatus = val && transferInterruptEnabled.Value;
                            } , name: "TXIS")
                        .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => RxNotEmpty, name: "RXNE")
                        .WithFlag(3, out addressMatched, FieldMode.Read, name: "ADDR")
                        .WithTag("NACKF", 4, 1)
                        .WithFlag(5, out stopDetection, FieldMode.Read, name: "STOPF")
                        .WithFlag(6, out transferComplete, FieldMode.Read, name: "TC")
                        .WithFlag(7, out transferCompleteReload, FieldMode.Read, name: "TCR")
                        .WithTag("BERR", 8, 1)
                        .WithTag("ARLO", 9, 1)
                        .WithTag("OVR", 10, 1)
                        .WithTag("PECERR", 11, 1)
                        .WithTag("TIMEOUT", 12, 1)
                        .WithTag("ALERT", 13, 1)
                        .WithReservedBits(14, 1)
                        .WithTag("BUSY", 15, 1)
                        .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => transferOutgoing, name: "DIR")
                        .WithTag("ADDCODE", 17, 7)
                        .WithReservedBits(24, 8)
                        .WithChangeCallback((_,__) => Update())
                }, {
                    (long)Registers.InterruptClear, new DoubleWordRegister(this, 0)
                        .WithReservedBits(0, 3)
                        .WithFlag(3, FieldMode.WriteOneToClear, 
                            writeCallback: (_, value) =>
                            {
                                if(value)
                                {
                                    transmitInterruptStatus = transferOutgoing & (txData.Count == 0);
                                    addressMatched.Value = false;
                                }
                            }, name: "ADDRCF")
                        .WithTag("NACKCF", 4, 1)
                        .WithFlag(5, FieldMode.WriteOneToClear, 
                            writeCallback: (_, value) =>
                            {
                                if(value)
                                {
                                    stopDetection.Value = false;
                                }
                            }, name: "STOPCF")
                        .WithReservedBits(6, 2)
                        .WithTag("BERRCF", 8, 1)
                        .WithTag("ARLOCF", 9, 1)
                        .WithTag("OVRCF", 10, 1)
                        .WithTag("PECCF", 11, 1)
                        .WithTag("TIMOUTCF", 12, 1)
                        .WithTag("ALERTCF", 13, 1)
                        .WithReservedBits(14, 18)
                        .WithChangeCallback((_,__) => Update())
                }, {
                    (long)Registers.ReceiveData, new DoubleWordRegister(this, 0)
                        .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: preVal => ReceiveDataRead((uint)preVal), name: "RXDATA")
                        .WithReservedBits(9, 23)
                }, {
                    (long)Registers.TransmitData, new DoubleWordRegister(this, 0)
                        .WithValueField(0, 8, writeCallback: (prevVal, val) => HandleTransmitDataWrite((uint)prevVal, (uint)val), name: "TXDATA")
                        .WithReservedBits(9, 23)
                }
            };

            return new DoubleWordRegisterCollection(this, map);
        }

        private void PeripheralEnabledWrite(bool oldValue, bool newValue)
        {
            if(newValue)
            {
                return;
            }
            stopDetection.Value = false;
            transferComplete.Value = false;
            transferCompleteReload.Value = false;
            transmitInterruptStatus = false;
        }

        private void ExtendTransfer()
        {
            //in case of reads we can fetch data from peripheral immediately, but in case of writes we have to wait until something is written to TXDATA
            if(isReadTransfer.Value)
            {
                var data = currentSlave.Read((int)bytesToTransfer.Value);
                foreach(var item in data)
                {
                    rxData.Enqueue(item);
                }
            }
            transferCompleteReload.Value = false;
            Update();
        }

        private void StartTransfer()
        {
            masterMode = true;
            transferComplete.Value = false;

            currentSlave = null;

            rxData.Clear();
            //This is kinda volatile. If we change slaveAddress setting to a callback action, it might not be set at this moment.
            currentSlaveAddress = (int)(use10BitAddressing.Value ? slaveAddress.Value : ((slaveAddress.Value >> 1) & 0x7F));
            if(!TryGetByAddress(currentSlaveAddress, out currentSlave))
            {
                this.Log(LogLevel.Warning, "Unknown slave at address {0}.", currentSlaveAddress);
                return;
            }

            if(isReadTransfer.Value)
            {
                transmitInterruptStatus = false;
                var data = currentSlave.Read((int)bytesToTransfer.Value);
                foreach(var item in data)
                {
                    rxData.Enqueue(item);
                }
            }
            else
            {
                transmitInterruptStatus = true;
            }
            Update();
        }

        private void StopTransfer()
        {
            masterMode = false;
            stopDetection.Value = true;
            currentSlave?.FinishTransmission();
            Update();
        }

        private uint ReceiveDataRead(uint oldValue)
        {
            if(rxData.Count > 0)
            {
                var value = rxData.Dequeue();
                if(rxData.Count == 0)
                {
                    SetTransferCompleteFlags(); //TC/TCR is set when NBYTES data have been transfered
                }
                return value;
            }
            this.Log(LogLevel.Warning, "Receive buffer underflow!");
            return 0;
        }

        private void HandleTransmitDataWrite(uint oldValue, uint newValue)
        {
            if(masterMode)
            {
                MasterTransmitDataWrite(oldValue, newValue);
            }
            else
            {
                SlaveTransmitDataWrite(oldValue, newValue);
            }
        }

        private void MasterTransmitDataWrite(uint oldValue, uint newValue)
        {
            if(currentSlave == null)
            {
                this.Log(LogLevel.Warning, "Trying to send byte {0} to an unknown slave with address {1}.", newValue, currentSlaveAddress);
                return;
            }
            txData.Enqueue((byte)newValue);
            if(txData.Count == (int)bytesToTransfer.Value)
            {
                currentSlave.Write(txData.ToArray());
                txData.Clear();
                SetTransferCompleteFlags();
            }
        }

        private void SlaveTransmitDataWrite(uint oldValue, uint newValue)
        {
            txData.Enqueue((byte)newValue);
        }

        private void SetTransferCompleteFlags()
        {
            if(!autoEnd.Value && !reload.Value)
            {
                transferComplete.Value = true;
            }
            if(autoEnd.Value)
            {
                currentSlave.FinishTransmission();
                stopDetection.Value = true;
                masterMode = false;
            }
            if(reload.Value)
            {
                transferCompleteReload.Value = true;
            }
            else
            {
                transmitInterruptStatus = false; //this is a guess based on a driver
            }
            Update();
        }

        private void Update()
        {
            var value = (transferCompleteInterruptEnabled.Value && (transferCompleteReload.Value || transferComplete.Value))
                || (transferInterruptEnabled.Value && transmitInterruptStatus)
                || (receiveInterruptEnabled.Value && isReadTransfer.Value && rxData.Count > 0) //RXNE is calculated dynamically
                || (stopDetectionInterruptEnabled.Value && stopDetection.Value)
                || (nackReceivedInterruptEnabled.Value && false) //TODO: implement NACKF
                || (addressMatchedInterruptEnabled.Value && addressMatched.Value);
            EventInterrupt.Set(value);
        }

        private IValueRegisterField bytesToTransfer;
        private IValueRegisterField slaveAddress;
        private IValueRegisterField ownAddress1;
        private IValueRegisterField ownAddress2;
        private IValueRegisterField ownAddress2Mask;
        private IFlagRegisterField transferInterruptEnabled;
        private IFlagRegisterField receiveInterruptEnabled;
        private IFlagRegisterField addressMatchedInterruptEnabled;
        private IFlagRegisterField nackReceivedInterruptEnabled;
        private IFlagRegisterField stopDetectionInterruptEnabled;
        private IFlagRegisterField transferCompleteInterruptEnabled;
        private IFlagRegisterField isReadTransfer;
        private IFlagRegisterField use10BitAddressing;
        private IFlagRegisterField reload;
        private IFlagRegisterField autoEnd;
        private IFlagRegisterField noStretch;
        private IFlagRegisterField ownAddress1Mode;
        private IFlagRegisterField ownAddress1Enabled;
        private IFlagRegisterField ownAddress2Enabled;
        private IFlagRegisterField transferComplete;
        private IFlagRegisterField transferCompleteReload;
        private IFlagRegisterField stopDetection;
        private IFlagRegisterField addressMatched;
        private IFlagRegisterField start;
        private IFlagRegisterField stop;

        private DoubleWordRegisterCollection registers;

        private II2CPeripheral currentSlave;
        private Queue<byte> rxData;
        private Queue<byte> txData;
        private int currentSlaveAddress;
        private bool transferOutgoing;
        private bool transmitInterruptStatus;
        private bool masterMode;

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
