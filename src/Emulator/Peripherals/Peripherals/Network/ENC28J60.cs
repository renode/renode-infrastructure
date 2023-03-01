//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public class ENC28J60 : ISPIPeripheral, IMACInterface
    {
        public ENC28J60()
        {
            sync = new object();
            ResetPointers();

            var econ1 = new ByteRegister(this).WithValueField(0, 2, out currentBank, name: "BSEL")
                                              .WithFlag(2, out ethernetReceiveEnabled, name: "RXEN")
                                              .WithFlag(3, FieldMode.Read, writeCallback: (_, value) => { if(value) TransmitPacket(); }, name: "TXRTS")
                                              .WithFlag(7, name: "TXRST");

            var econ2 = new ByteRegister(this, 0x80).WithFlag(6, FieldMode.Read, writeCallback: delegate { 
                                                        waitingPacketCount = Math.Max(0, waitingPacketCount - 1);
                                                        RefreshInterruptStatus(); }, name: "PKTDEC")
                                                    .WithFlag(7, out autoIncrement, name: "AUTOINC");

            var estat = new ByteRegister(this, 1).WithReadCallback(delegate { transmitPacketInterrupt.Value = false; RefreshInterruptStatus(); }) // not sure
                                                 .WithFlag(0, FieldMode.Read, name: "CLKRDY"); // we're always ready, so the reset value is 1

            var eie = new ByteRegister(this).WithFlag(3, out transmitPacketInterruptEnabled, writeCallback: delegate { RefreshInterruptStatus(); }, name: "TXIE")
                                            .WithFlag(6, out receivePacketInterruptEnabled, writeCallback: delegate { RefreshInterruptStatus(); }, name: "PKTIE")
                                            .WithFlag(7, out interruptsEnabled, writeCallback: delegate { RefreshInterruptStatus(); }, name: "INTIE");

            var eir = new ByteRegister(this).WithFlag(0, name: "RXERIF")
                                            .WithFlag(3, out transmitPacketInterrupt, writeCallback: delegate { RefreshInterruptStatus(); }, name: "TXIF")
                                            .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => IsReceiveInterruptActive(), name: "PKTIF");

            var bank0Map = new Dictionary<long, ByteRegister>
            {
                // ERDPTL
                { 0x00, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => GetLowByteOf(bufferReadPointer),
                                                              writeCallback: (_, value) => SetLowByteOf(ref bufferReadPointer, (byte)value)) },

                // ERDPTH
                { 0x01, new ByteRegister(this).WithValueField(0, 5, valueProviderCallback: _ => GetHighByteOf(bufferReadPointer),
                                                              writeCallback: (_, value) => SetHighByteOf(ref bufferReadPointer, (byte)value)) },

                // EWRPTL
                { 0x02, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => GetLowByteOf(bufferWritePointer),
                                                              writeCallback: (_, value) => SetLowByteOf(ref bufferWritePointer, (byte)value)) },

                // EWRPTH
                { 0x03, new ByteRegister(this).WithValueField(0, 5, valueProviderCallback: _ => GetHighByteOf(bufferWritePointer),
                                                              writeCallback: (_, value) => SetHighByteOf(ref bufferWritePointer, (byte)value)) },

                // ETXSTL
                { 0x04, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => GetLowByteOf(transmitBufferStart),
                                                              writeCallback: (_, value) => SetLowByteOf(ref transmitBufferStart, (byte)value)) },

                // ETXSTH
                { 0x05, new ByteRegister(this).WithValueField(0, 5, valueProviderCallback: _ => GetHighByteOf(transmitBufferStart),
                                                              writeCallback: (_, value) => SetHighByteOf(ref transmitBufferStart, (byte)value)) },

                // ETXNDL
                { 0x06, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => GetLowByteOf(transmitBufferEnd),
                                                              writeCallback: (_, value) => SetLowByteOf(ref transmitBufferEnd, (byte)value)) },

                // ETXNDH
                { 0x07, new ByteRegister(this).WithValueField(0, 5, valueProviderCallback: _ => GetHighByteOf(transmitBufferEnd),
                                                              writeCallback: (_, value) => SetHighByteOf(ref transmitBufferEnd, (byte)value)) },

                // ERXSTL
                { 0x08, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => GetLowByteOf(receiveBufferStart),
                                                              writeCallback: (_, value) => { SetLowByteOf(ref receiveBufferStart, (byte)value); currentReceiveWritePointer = receiveBufferStart; } ) },

                // ERXSTH
                { 0x09, new ByteRegister(this).WithValueField(0, 5, valueProviderCallback: _ => GetHighByteOf(receiveBufferStart),
                                                              writeCallback: (_, value) => { SetHighByteOf(ref receiveBufferStart, (byte)value); currentReceiveWritePointer = receiveBufferStart; } ) },

                // ERXNDL
                { 0x0A, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => GetLowByteOf(receiveBufferEnd),
                                                              writeCallback: (_, value) => SetLowByteOf(ref receiveBufferEnd, (byte)value)) },

                // ERXNDH
                { 0x0B, new ByteRegister(this).WithValueField(0, 5, valueProviderCallback: _ => GetHighByteOf(receiveBufferEnd),
                                                              writeCallback: (_, value) => SetHighByteOf(ref receiveBufferEnd, (byte)value)) },

                // ERXRDPTL
                { 0x0C, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => GetLowByteOf(receiveReadPointer),
                                                              writeCallback: (_, value) =>  bufferedLowByteOfReceiveReadPointer = (byte)value) },

                // ERXRDPTH
                { 0x0D, new ByteRegister(this).WithValueField(0, 5, valueProviderCallback: _ => GetHighByteOf(receiveReadPointer),
                                                              writeCallback: (_, value) => receiveReadPointer = (int)(bufferedLowByteOfReceiveReadPointer | ((value << 8)))) }
            };

            var bank1Map = new Dictionary<long, ByteRegister>
            {
                // ERXFCON
                { 0x18, new ByteRegister(this, 0xA1).WithFlag(5, out crcEnabled, name: "CRCEN") },

                // EPKTCNT
                { 0x19, new ByteRegister(this).WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => (uint)waitingPacketCount) }
            };

            var bank2Map = new Dictionary<long, ByteRegister>
            {
                // MACON1
                // note that we currently ignore all the Pause Control Frame stuff
                { 0x00, new ByteRegister(this).WithFlag(0, out macReceiveEnabled, name: "MARXEN").WithFlag(2,  name: "RXPAUS").WithFlag(3, name: "TXPAUS")},

                // MACON3
                { 0x02, new ByteRegister(this).WithFlag(0, name: "FULDPX") },

                // MABBIPG (too low level parameter for emulation)
                { 0x04, new ByteRegister(this).WithValueField(0, 7) },

                // MAIPGL (same as above)
                { 0x06, new ByteRegister(this).WithValueField(0, 7) },

                // MAIPGH (same as above)
                { 0x07, new ByteRegister(this).WithValueField(0, 7) },

                // MICMD
                { 0x12, new ByteRegister(this).WithFlag(0, writeCallback: (_, value) => { if(value) ReadPhyRegister(); }, name: "MIIRD") },

                // MIREGADR
                { 0x14, new ByteRegister(this).WithValueField(0, 5, out miiRegisterAddress) },

                // MIWRL
                { 0x16, new ByteRegister(this).WithValueField(0, 8, out phyWriteLow) },

                // MIWRH
                { 0x17, new ByteRegister(this).WithValueField(0, 8, writeCallback: (_, value) => WritePhyRegister((ushort)(phyWriteLow.Value | (value << 8)))) },

                // MIRDL
                { 0x18, new ByteRegister(this).WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => (byte)lastReadPhyRegisterValue) },

                // MIRDH
                { 0x19, new ByteRegister(this).WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => (byte)(lastReadPhyRegisterValue >> 8)) }
            };

            var bank3Map = new Dictionary<long, ByteRegister>
            {
                // MAADR5
                { 0x00, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => MAC.E, writeCallback: (_, value) => MAC = MAC.WithNewOctets(e: (byte)value)) },

                // MADDR6
                { 0x01, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => MAC.F, writeCallback: (_, value) => MAC = MAC.WithNewOctets(f: (byte)value)) },

                // MADDR3
                { 0x02, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => MAC.C, writeCallback: (_, value) => MAC = MAC.WithNewOctets(c: (byte)value)) },

                // MADDR4
                { 0x03, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => MAC.D, writeCallback: (_, value) => MAC = MAC.WithNewOctets(d: (byte)value)) },

                // MADDR1
                { 0x04, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => MAC.A, writeCallback: (_, value) => MAC = MAC.WithNewOctets(a: (byte)value)) },

                // MADDR2
                { 0x05, new ByteRegister(this).WithValueField(0, 8, valueProviderCallback: _ => MAC.B, writeCallback: (_, value) => MAC = MAC.WithNewOctets(b: (byte)value)) },

                // MISTAT
                { 0x0A, new ByteRegister(this).WithFlag(0, FieldMode.Read, name: "BUSY") } // we're never busy
            };

            var maps = new[] { bank0Map, bank1Map, bank2Map, bank3Map };
            // registers below are available in all banks
            foreach(var map in maps)
            {
                map.Add(0x1B, eie); // EIE
                map.Add(0x1C, eir); // EIR
                map.Add(0x1D, estat); // ESTAT
                map.Add(0x1E, econ2); // ECON2
                map.Add(0x1F, econ1); // ECON1
            }
            registers = maps.Select(x => new ByteRegisterCollection(this, x)).ToArray();

            ethernetBuffer = new byte[8.KB()];

            phyRegisters = new WordRegisterCollection(this, new Dictionary<long, WordRegister>
            {
                // PHCON1
                { 0x00, new WordRegister(this).WithFlag(8, name: "PDPXMD") }, // full duplex stuff, ignored

                // PHCON2
                { 0x10, new WordRegister(this) }
            });
            IRQ = new GPIO();
            IRQ.Set(); // the interrupt output is negated
        }

        public event Action<EthernetFrame> FrameReady;

        public MACAddress MAC { get; set; }

        public void ReceiveFrame(EthernetFrame frame)
        {
            lock(sync)
            {
                if(!macReceiveEnabled.Value || !ethernetReceiveEnabled.Value)
                {
                    return;
                }
                if(!TryReceivePacket(frame.Bytes))
                {
                    this.Log(LogLevel.Info, "Packet ignored.");
                }
            }
        }

        public void Reset()
        {
            lock(sync)
            {
                ResetPointers();
                foreach(var registerCollection in registers)
                {
                    registerCollection.Reset();
                }
                phyRegisters.Reset();
                waitingPacketCount = 0;
                currentMode = Mode.Normal;
                RefreshInterruptStatus();
            }
        }

        public byte Transmit(byte data)
        {
            lock(sync)
            {
                switch(currentMode)
                {
                case Mode.Normal:
                    return HandleTransmissionInNormalMode(data);
                case Mode.ReadMacOrMiiRegister:
                    currentMode = Mode.ReadControlRegister;
                    return 0x00; // dummy byte
                case Mode.ReadControlRegister:
                    return HandleReadRegister();
                case Mode.WriteControlRegister:
                    HandleWriteRegister(data);
                    return 0x00;
                case Mode.RegisterBitSet:
                    HandleBitSetOrClear(data, true);
                    return 0x00;
                case Mode.RegisterBitClear:
                    HandleBitSetOrClear(data, false);
                    return 0x00;
                case Mode.ReadBufferMemory:
                    return HandleReadBufferMemory();
                case Mode.WriteBufferMemory:
                    HandleWriteBufferMemory(data);
                    return 0x00;
                default:
                    throw new InvalidOperationException("Internal error: unexpected mode.");
                }
            }
        }

        public void FinishTransmission()
        {
            lock(sync)
            {
                currentMode = Mode.Normal;
            }
        }

        public GPIO IRQ { get; private set; }

        private void ResetPointers()
        {
            transmitBufferStart = 0;
            transmitBufferEnd = 0;
            receiveBufferStart = 0x5FA;
            receiveBufferEnd = 0x1FFF;
            bufferReadPointer = 0x5FA;
            bufferWritePointer = 0;
            receiveReadPointer = 0x5FA;
        }

        private byte HandleTransmissionInNormalMode(byte data)
        {
            if(data == 0xFF)
            {
                // soft reset
                return 0x00;
            }
            var commandType = data >> 5;
            selectedRegister = data & 0x1F;
            switch(commandType)
            {
                case 0:
                    currentMode = MacOrMiiRegisters.Contains(Tuple.Create((int)currentBank.Value, selectedRegister)) 
                        ? Mode.ReadMacOrMiiRegister : Mode.ReadControlRegister;
                    break;
                case 1:
                    currentMode = Mode.ReadBufferMemory;
                    break;
                case 2:
                    currentMode = Mode.WriteControlRegister;
                    break;
                case 3:
                    currentMode = Mode.WriteBufferMemory;
                    break;
                case 4:
                    currentMode = Mode.RegisterBitSet;
                    break;
                case 5:
                    currentMode = Mode.RegisterBitClear;
                    break;
                default:
                    this.Log(LogLevel.Error, "Unhandled command type: {0}.", commandType);
                    break;
            }
            return 0x00;
        }

        private byte HandleReadRegister()
        {
            var registerDetails = string.Format("0x{0:X}, bank {1}", selectedRegister, currentBank.Value);
            this.Log(LogLevel.Debug, "Read from {0}.", registerDetails);
            var result = default(byte);
            if(!GetCurrentRegistersBank().TryRead(selectedRegister, out result))
            {
                this.Log(LogLevel.Warning, "Read from unhandled register {0}.", registerDetails);
            }
            currentMode = Mode.Normal;
            return result;
        }

        private void HandleWriteRegister(byte data)
        {
            var registerDetails = string.Format("0x{0:X}, bank {1}, value 0x{2:X}", selectedRegister, currentBank.Value, data);
            this.Log(LogLevel.Debug, "Write to {0}.", registerDetails);
            if(!GetCurrentRegistersBank().TryWrite(selectedRegister, data))
            {
                this.Log(LogLevel.Warning, "Write to unhandled register {0}.", registerDetails);
            }
            currentMode = Mode.Normal;
        }

        private byte HandleReadBufferMemory()
        {
            this.Log(LogLevel.Debug, "Reading buffer memory at 0x{0:X}.", bufferReadPointer);
            var result = ethernetBuffer[bufferReadPointer];
            if(autoIncrement.Value)
            {
                bufferReadPointer++;
                if(bufferReadPointer > receiveBufferEnd)
                {
                    bufferReadPointer = receiveBufferStart;
                }
            }
            return result;
        }

        private void HandleWriteBufferMemory(byte value)
        {
            this.Log(LogLevel.Debug, "Writing buffer memory at 0x{0:X}, value 0x{1:X}.", bufferWritePointer, value);
            ethernetBuffer[bufferWritePointer] = value;
            if(autoIncrement.Value)
            {
                bufferWritePointer++;
                if(bufferWritePointer == ethernetBuffer.Length)
                {
                    bufferWritePointer = 0;
                }
            }
        }

        private void HandleBitSetOrClear(byte data, bool set)
        {
            var registerDetails = string.Format("0x{0:X}, bank {1}, bits to {3}: {2}", selectedRegister, currentBank.Value,
                                                BitHelper.GetSetBitsPretty(data), set ? "set" : "clear");
            this.Log(LogLevel.Debug, "Bit{1} to {0}.", registerDetails, set ? "Set" : "Clear");
            currentMode = Mode.Normal;
            if(MacOrMiiRegisters.Contains(Tuple.Create((int)currentBank.Value, selectedRegister)))
            {
                this.Log(LogLevel.Warning, "Trying to set a bit on MAC or MII register {0}.", registerDetails);
                return;
            }
            byte value;
            if(!GetCurrentRegistersBank().TryRead(selectedRegister, out value))
            {
                this.Log(LogLevel.Warning, "BitSet to unimplemented register {0}.", registerDetails);
                return;
            }
            if(set)
            {
                value |= data;
            }
            else
            {
                value &= (byte)~data;
            }
            GetCurrentRegistersBank().Write(selectedRegister, value);
        }

        private ByteRegisterCollection GetCurrentRegistersBank()
        {
            return registers[(int)currentBank.Value];
        }

        private void ReadPhyRegister()
        {
            ushort result = 0;
            var address = miiRegisterAddress.Value;
            this.DebugLog("Read from PHY register 0x{0:X}.", address);
            if(!phyRegisters.TryRead((long)address, out result))
            {
                this.Log(LogLevel.Warning, "Read from unimplemented PHY register 0x{0:X}.", address);
            }
            lastReadPhyRegisterValue = result;
        }

        private void WritePhyRegister(ushort value)
        {
            var address = miiRegisterAddress.Value;
            var registerFriendlyName = string.Format("PHY register 0x{0:X}, value 0x{1:X}", address, value);
            this.DebugLog("Write to {0}.", registerFriendlyName);
            if(!phyRegisters.TryWrite((long)address, value))
            {
                this.Log(LogLevel.Warning, "Write to unimplemented {0}.", registerFriendlyName);
            }
        }

        private void SetInterrupt(bool value)
        {
            IRQ.Set(!value);
        }

        private void RefreshInterruptStatus()
        {
            SetInterrupt(interruptsEnabled.Value && 
                         (IsReceiveInterruptActive() || (transmitPacketInterrupt.Value && transmitPacketInterruptEnabled.Value)));
        }

        private bool IsReceiveInterruptActive()
        {
            return waitingPacketCount > 0 && receivePacketInterruptEnabled.Value;
        }

        private bool TryReceivePacket(byte[] data)
        {
            // first check whether the packet fits into buffer
            var receiveBufferSize = receiveBufferEnd - receiveBufferStart + 1;
            var freeSpace = receiveBufferSize - ((receiveBufferSize + currentReceiveWritePointer - receiveReadPointer) % receiveBufferSize);
            var packetPlusHeadersLength = data.Length + 2 + 4; // next packet pointer and receive status vector
            packetPlusHeadersLength += packetPlusHeadersLength % 2; // padding byte
            if(freeSpace < packetPlusHeadersLength)
            {
                this.Log(LogLevel.Warning, "No free space for packet. Packet length (+ headers): {0}B, free space: {1}B.",
                         Misc.NormalizeBinary(packetPlusHeadersLength), Misc.NormalizeBinary(freeSpace));
                return false;
            }

            if(!EthernetFrame.CheckCRC(data) && crcEnabled.Value)
            {
                this.Log(LogLevel.Info, "Invalid CRC, packet discarded");
                return false;
            }

            var nextReceiveWritePointer = currentReceiveWritePointer + packetPlusHeadersLength;
            if(nextReceiveWritePointer > receiveBufferEnd)
            {
                nextReceiveWritePointer -= receiveBufferSize;
            }
            var packetWithHeader = new byte[packetPlusHeadersLength];
            BitConverter.GetBytes((ushort)nextReceiveWritePointer).CopyTo(packetWithHeader, 0);
            BitConverter.GetBytes((ushort)data.Length).CopyTo(packetWithHeader, 2); 
            BitConverter.GetBytes((ushort)(1 << 7)).CopyTo(packetWithHeader, 4);
            data.CopyTo(packetWithHeader, 6);

            var firstPartLength = Math.Min(packetPlusHeadersLength, receiveBufferEnd - currentReceiveWritePointer + 1);
            Array.Copy(packetWithHeader, 0, ethernetBuffer, currentReceiveWritePointer, firstPartLength);
            if(firstPartLength < packetPlusHeadersLength)
            {
                // packet overlaps buffer
                Array.Copy(packetWithHeader, firstPartLength, ethernetBuffer, receiveBufferStart, packetWithHeader.Length - firstPartLength);
            }
            currentReceiveWritePointer = nextReceiveWritePointer;

            waitingPacketCount++;
            RefreshInterruptStatus();
            return true;
        }

        private void TransmitPacket()
        {
            var packetSize = transmitBufferEnd - transmitBufferStart; // -1 for the per packet control byte, but transmitBufferEnd points to the last byte of the packet
            var data = new byte[packetSize];
            Array.Copy(ethernetBuffer, transmitBufferStart + 1, data, 0, packetSize);
            if(!Misc.TryCreateFrameOrLogWarning(this, data, out var frame, addCrc: true))
            {
                return;
            }
            // status vector is not implemented yet
            this.Log(LogLevel.Debug, "Sending frame {0}.", frame);
            FrameReady?.Invoke(frame);
            transmitPacketInterrupt.Value = true;
            RefreshInterruptStatus();
        }

        private static void SetLowByteOf(ref int ofWhat, byte with)
        {
            ofWhat = (ofWhat & 0xFF00) | with;
        }

        private static void SetHighByteOf(ref int ofWhat, byte with)
        {
            ofWhat = (with << 8) | (ofWhat & 0xFF);
        }

        // methods below return uint because of the register infrastructure (it is more convenient)
        private static uint GetLowByteOf(int ofWhat)
        {
            return (uint)(ofWhat & 0xFF);
        }

        private static uint GetHighByteOf(int ofWhat)
        {
            return (uint)((ofWhat >> 8) & 0x1F);
        }

        private Mode currentMode;
        private IValueRegisterField currentBank;
        private int selectedRegister;
        private int receiveBufferStart;
        private int receiveBufferEnd;
        private int transmitBufferStart;
        private int transmitBufferEnd;
        private int bufferReadPointer;
        private int bufferWritePointer;
        private byte bufferedLowByteOfReceiveReadPointer;
        private int receiveReadPointer;
        private int currentReceiveWritePointer;
        private IFlagRegisterField macReceiveEnabled;
        private IValueRegisterField miiRegisterAddress;
        private ushort lastReadPhyRegisterValue;
        private IValueRegisterField phyWriteLow;
        private IFlagRegisterField interruptsEnabled;
        private IFlagRegisterField receivePacketInterruptEnabled;
        private IFlagRegisterField transmitPacketInterruptEnabled;
        private IFlagRegisterField transmitPacketInterrupt;
        private IFlagRegisterField ethernetReceiveEnabled;
        private IFlagRegisterField autoIncrement;
        private int waitingPacketCount;
        private IFlagRegisterField crcEnabled;

        private readonly WordRegisterCollection phyRegisters;
        private readonly ByteRegisterCollection[] registers;
        private readonly byte[] ethernetBuffer;
        private readonly object sync;

        // here is the list of MAC and MII registers in the format (bank, register_number)
        // they have to be read in a slightly different way than ethernet registers
        private static readonly HashSet<Tuple<int, int>> MacOrMiiRegisters = new HashSet<Tuple<int, int>>
        {
            Tuple.Create(2, 0x00),
            Tuple.Create(2, 0x02),
            Tuple.Create(2, 0x04),
            Tuple.Create(2, 0x06),
            Tuple.Create(2, 0x07),
            Tuple.Create(2, 0x12),
            Tuple.Create(2, 0x14),
            Tuple.Create(2, 0x16),
            Tuple.Create(2, 0x17),
            Tuple.Create(2, 0x18),
            Tuple.Create(2, 0x19),
            Tuple.Create(3, 0x00),
            Tuple.Create(3, 0x01),
            Tuple.Create(3, 0x02),
            Tuple.Create(3, 0x03),
            Tuple.Create(3, 0x04),
            Tuple.Create(3, 0x05),
            Tuple.Create(3, 0x0A)
        };

        private enum Mode
        {
            Normal,
            ReadBufferMemory,
            WriteBufferMemory,
            ReadControlRegister,
            ReadMacOrMiiRegister,
            WriteControlRegister,
            RegisterBitSet,
            RegisterBitClear
        }
    }
}
