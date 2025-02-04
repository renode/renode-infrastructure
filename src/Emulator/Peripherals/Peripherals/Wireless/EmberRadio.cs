//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Security.Cryptography;
using System.Linq;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public class EmberRadio : IDoubleWordPeripheral, IRadio
    {
        public EmberRadio(IMachine machine)
        {
            this.machine = machine;
            sysbus = machine.GetSystemBus(this);

            currentKey = new byte[16];
            currentData = new byte[16];
            currentValue = new byte[16];
            Tx = new GPIO();
            Rx = new GPIO();
            Tim = new GPIO();
            irqStatus = new uint[256];
            Reset();
        }

        public int Channel { get; set; }

        uint macRxConfig;

        public void Reset()
        {
            Array.Clear(currentKey, 0, currentKey.Length);
            Array.Clear(currentData, 0, currentData.Length);
            dataPointer = 0;
            valuePointer = 0;
            // TODO:
        }

        [ConnectionRegion("encryptor")]
        public uint ReadDoubleWordEncryptor(long offset)
        {
            switch((EncryptorRegister)offset)
            {
            case EncryptorRegister.Value: 
                if(valueToEncryptHasChanged)
                {
                    Encrypt();
                    valueToEncryptHasChanged = false;
                }
                var result = 0u;
                for(var i = 0; i < 3; i++)
                {
                    result |= currentValue[valuePointer + i];
                    result <<= 8;
                }
                result |= currentValue[valuePointer + 3];
                valuePointer = (valuePointer + 4) % 16;
                return result;
            }
            this.LogUnhandledRead(offset);
            return 0;
        }

        [ConnectionRegion("encryptor")]
        public void WriteDoubleWordEncryptor(long offset, uint value)
        {
            if(offset >= EncryptorKeyRegisterBegin && offset < EncryptorKeyRegisterEnd)
            {
                valueToEncryptHasChanged = true;
                var localOffset = offset - EncryptorKeyRegisterBegin;
                for(var i = 0; i < 4; i++)
                {
                    currentKey[15 - localOffset - i] = (byte)value;
                    value >>= 8;
                }
            }
            else
            {
                switch((EncryptorRegister)offset)
                {
                case EncryptorRegister.Data:
                    valueToEncryptHasChanged = true;
                    for(var i = 0; i < 4; i++)
                    {
                        currentData[dataPointer + 3 - i] = (byte)value;
                        value >>= 8;
                    }
                    dataPointer = (dataPointer + 4) % 16;
                    break;
                case EncryptorRegister.Control:
                    // we currently ignore it
                    break;
                default:
                    this.LogUnhandledWrite(offset, value);
                    break;
                }
            }
        }

        [ConnectionRegion("irq")]
        public uint ReadDoubleWordIRQ(long offset)
        {
            this.Log(LogLevel.Warning, "ReadDoubleWordIRQ({0:X})", offset);
            switch(offset)
            {
            case 0x0:
                return Rx.IsSet ? 0x20u : 0;
            case 0x4:
                return Tx.IsSet ? 0x400u : 0;
            case 0x40:
                return 0xFF;
            /* case 0x018:
                lastFlipVal = 1 - lastFlipVal;
                return lastFlipVal * 0xFFFFFFFF;*/
            case 0x01C:
                return (uint)((int)((machine.ElapsedVirtualTime.TimeElapsed).TotalMilliseconds * 1000) & 0xFFFFFFFF);
            case 0x18:
                return INT_MGMTFLAG;
            }
            return 0xFF;//irqStatus[offset];
        }

        public uint ReadDoubleWord(long offset)
        {
            this.Log(LogLevel.Noisy, "ReadDoubleWord({0:X})", offset);
            switch(offset)
            {
            case 0x1010:
                return MAC_TX_ST_ADDR_A;
            case 0x1014:
                return MAC_TX_END_ADDR_A;
            case 0x1020:
                this.Log(LogLevel.Warning, "packlength = {0} (0x{0:X})", packLength);
                return 14; // > 4
            case 0x1024:
                return 0x0;
            case 0x1038:
                // MAC_TIMER
                mac_timer += 1;
                return mac_timer;
            case 0x1050:
                //       Tx.Set();
                return 0xF;
            case 0x1054:
                return 0xFFFF;
            case 0x1064: // MAC_ACK_STROBE
                return 1;
            case 0x1068: // MAC_STATUS
                return 0x423;
            case 0x107C: // MAC_TX_ACK_FRAME
                return 0x10;
            //return 0xCC20;
            case 0x1084: // MAC_RX_CONFIG
                this.Log(LogLevel.Warning, "MAC_RX_CONFIG {0:X}", macRxConfig & 0xFFFFFFFE);
                return macRxConfig & 0xFFFFFFFE;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        uint[] irqStatus;
        uint INT_MGMTFLAG;

     /*   public void ReceiveFakePacket()
        {
            //ReceivePacket("41C8003412FFFFBA1F000002E1800081000080E10200001FBA48656C6C6F001F5B");
            ReceivePacket("41C8003412FFFFBA1F000002E1800081000080E10200001FBA48656C6C6F001F5B");//208041C8
        }*/

        [ConnectionRegion("irq")]
        public void WriteDoubleWordIRQ(long offset, uint value)
        {
            switch(offset)
            {
            case 0x18:
                INT_MGMTFLAG = value;
                break;

            }

            irqStatus[offset] = value;

            Rx.Unset();
            Tim.Unset();
            Tx.Unset();

            this.Log(LogLevel.Warning, "WriteDoubleWordIRQ({0:X}, {1:X})", offset, value);
        }


        public void WriteDoubleWord(long offset, uint value)
        {
            this.Log(LogLevel.Warning, "WriteDoubleWord ({0:X}, 0x{1:X})", offset, value);
            switch(offset)
            {
            // //////////////
            // i think the decision on what addr to use to rx/tx (ADDR_A or ADDR_B) is based on register MAC_DMA_CONFIG_xX_LOAD_A/B somehow @ 0x40002030
            // //////////////
            //
            case 0x1000: // MAC_RX_ST_ADDR_A
                MAC_RX_ST_ADDR_A = value;
                break;
                /*case 0x1004: // MAC_RX_END_ADDR_A
                MAC_RX_END_ADDR_A = value;
                break;*/
            case 0x1008: // MAC_RX_ST_ADDR_B
                MAC_RX_ST_ADDR_B = value;
                break;
                /* case 0x100C: // MAC_RX_END_ADDR_B
                MAC_RX_END_ADDR_B = value;
                break;*/
            case 0x1010: // MAC_TX_ST_ADDR_A
                MAC_TX_ST_ADDR_A = value;
                this.Log(LogLevel.Warning, "Setting MAC_TX_ST_A to {0:X}", value);
                break;
            case 0x1014: // MAC_TX_END_ADDR_A
                MAC_TX_END_ADDR_A = value;
                this.Log(LogLevel.Noisy, "Setting MAC_TX_END_A to {0:X}", value);
                break;
                /* case 0x1018: // MAC_TX_ST_ADDR_B
                MAC_TX_ST_ADDR_B = value;
                break;
            case 0x101C: // MAC_TX_END_ADDR_B
                MAC_TX_END_ADDR_B = value;
                break;*/

            case 0x1060:
                // MAC_TX_STROBE
                if((value & 0x1) > 0)
                {
                    // TODO: we should deterimine which ADDR to use
                    int packet_len = sysbus.ReadByte(MAC_TX_ST_ADDR_A);
                    sysbus.WriteByte(MAC_TX_ST_ADDR_A, 0); // clear len
                    if(packet_len == 0)
                    {
                        break;
                    }
                    this.Log(LogLevel.Warning, "Sending packet of size {0}", packet_len);
                    //string s = "";
                    var dataToSend = new byte[packet_len];
                    for(uint j = 0; j < packet_len; j++)
                    {
                        dataToSend[j] = sysbus.ReadByte(MAC_TX_ST_ADDR_A + j + 1);
                    //    s = s + string.Format("{0:X2}", sysbus.ReadByte(MAC_TX_ST_ADDR_A + j + 1));
                    }
                    this.Log(LogLevel.Info, "data = {0}", dataToSend.Select(x => x.ToString("X")).Stringify());
                    var frameSent = FrameSent;
                    if(frameSent != null)
                    {
                        frameSent(this, dataToSend);
                    }
                }

                if((value & 0x8) > 0)
                {
                    int packet_len = sysbus.ReadByte(MAC_TX_ST_ADDR_A);
                    this.Log(LogLevel.Noisy, "Adding CRC.");

                    if(packet_len == 0)
                    {
                        break;
                    }
                    this.Log(LogLevel.Noisy, "Adding CRC to packet of size {0}", packet_len);

                    var data = new byte[packet_len];
                    for(uint j = 0; j < packet_len; j++)
                        data[j] = sysbus.ReadByte(MAC_TX_ST_ADDR_A + 1 + j);

                    ushort crc = count_crc(data);
                    this.Log(LogLevel.Noisy, "Counted CRC = {0:X}", crc);

                    sysbus.WriteByte((ulong)(MAC_TX_ST_ADDR_A + packet_len - 1), (byte)(crc & 0xFF));
                    sysbus.WriteByte((ulong)(MAC_TX_ST_ADDR_A + packet_len), (byte)((crc >> 8) & 0xFF));

                }
                break;
            case 0x1084:
                macRxConfig = value;
                break;
            default:
                this.Log(LogLevel.Warning, "WriteDoubleWord missed ({0:X}, {1:X})", offset, value);
                break;
            }
        }

        #region IRadio implementation

        public event Action<IRadio, byte[]> FrameSent;

        public void ReceiveFrame(byte[] frame, IRadio sender)
        {
            this.Log(LogLevel.Warning, "packet as bytes '{0}' of len {1}", frame.Select(x => String.Format("0x{0:X}", x)).Aggregate((x, y) => x + " " + y), frame.Length);
            packLength = (uint)frame.Length;
            sysbus.WriteByte(MAC_RX_ST_ADDR_A, (byte)(frame.Length));
            sysbus.WriteByte(MAC_RX_ST_ADDR_B, (byte)(frame.Length));

            for(int i = 0; i < frame.Length; ++i)
            {
                sysbus.WriteByte((ulong)(MAC_RX_ST_ADDR_A + i + 1), frame[i]);
                sysbus.WriteByte((ulong)(MAC_RX_ST_ADDR_B + i + 1), frame[i]);
            }

            /*   ushort crc = count_crc(data);
            this.Log(LogType.Noisy, "Counted CRC = {0:X}", crc);
            this.SystemBus.WriteByte(MAC_RX_ST_ADDR_A + packet_data.Length - 1, (byte)(crc & 0xFF));
            this.SystemBus.WriteByte(MAC_RX_ST_ADDR_A + packet_data.Length, (byte)((crc >> 8) & 0xFF));
            this.SystemBus.WriteByte(MAC_RX_ST_ADDR_B + packet_data.Length - 1, (byte)(crc & 0xFF));
            this.SystemBus.WriteByte(MAC_RX_ST_ADDR_B + packet_data.Length, (byte)((crc >> 8) & 0xFF));
*/
            Rx.Set();
            Tim.Set();
        }

        #endregion

        private static ushort count_crc(byte[] data)
        {        
            ushort crc = 0;
            uint j;
            for(j = 0; j < data.Length; j++)
            {

                crc = (ushort)(crc ^ data[j] << 8);
                var b = 8;
                do
                {
                    if((crc & 0x8000) > 0)
                    {
                        crc = (ushort)(crc << 1 ^ 0x1021);
                    }
                    else
                    {
                        crc = (ushort)(crc << 1);
                    }
                }
                while(--b > 0);
            }
            return crc;
        }

      /*  public void ReceivePacket(string packetData)
        {
            // TODO: have some mechanism to determine to what ADDR we should put the data (ADDR_A/ADDR_B)
            this.Log(LogLevel.Warning, "TODO: should put packet '{0}' of len {3} to bufs @ {1:X} & @ {2:X}", packetData, MAC_RX_ST_ADDR_A, MAC_RX_ST_ADDR_B, packetData.Length);

            byte[] data;
            var ind = 0;
            data = packetData.GroupBy(x => ind++ / 2).Select(x => Convert.ToByte(string.Format("{0}{1}", x.First(), x.Skip(1).First()), 16)).ToArray();
            ReceiveFrame(data);
        }*/


        private void Encrypt()
        {
            var aes = Aes.Create();
            aes.KeySize = 128;
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            var encryptor = aes.CreateEncryptor(currentKey, new byte[16]);
            encryptor.TransformFinalBlock(currentData, 0, 16).CopyTo(currentValue, 0);
        }

        public GPIO Tx { get; private set; }

        public GPIO Rx { get; private set; }

        public GPIO Tim { get; private set; }

        private uint mac_timer;

        private readonly byte[] currentKey;
        private readonly byte[] currentData;
        private readonly byte[] currentValue;
        private bool valueToEncryptHasChanged;
        private int dataPointer;
        private int valuePointer;

        private uint packLength;

        private uint MAC_TX_ST_ADDR_A = 0x20000000;
        private uint MAC_TX_END_ADDR_A = 0x20000000;
        private uint MAC_RX_ST_ADDR_A = 0x20000000;
        private uint MAC_RX_ST_ADDR_B = 0x20000000;

        private readonly IMachine machine;
        private readonly IBusController sysbus;

       // public event Action<string> SendPacket;

        private enum EncryptorRegister
        {
            Control = 0x0,
            Data = 0x28,
            Value = 0x30
        }

        private const int EncryptorKeyRegisterBegin = 0x38;
        private const int EncryptorKeyRegisterEnd = 0x48;

    }
}

