//
// Copyright (c) 2010-2017 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Wireless.IEEE802_15_4
{
    public class Frame
    {
        public static bool IsAcknowledgeRequested(byte[] frame)
        {
            return (frame[0] & 0x20) != 0;
        }

        public static Frame CreateAckForFrame(byte[] frame)
        {
            var sequenceNumber = frame[2];
            // TODO: here we set pending bit as false
            return CreateACK(sequenceNumber, false);
        }

        public static Frame CreateACK(byte sequenceNumber, bool pending)
        {
            var result = new Frame();
            result.Type = FrameType.ACK;
            result.FramePending = pending;
            result.DataSequenceNumber = sequenceNumber;
            result.Encode();

            return result;
        }

        public Frame(byte[] data)
        {
            Bytes = data;
            Decode(data);
        }

        public byte Length { get { return (byte)Bytes.Length; } }
        public FrameType Type { get; private set; }
        public bool SecurityEnabled { get; private set; }
        public bool FramePending { get; set; }
        public bool AcknowledgeRequest { get; private set; }
        public bool IntraPAN { get; private set; }
        public AddressingMode DestinationAddressingMode { get; private set; }
        public byte FrameVersion { get; private set; }
        public AddressingMode SourceAddressingMode { get; private set; }
        public byte DataSequenceNumber { get; private set; }
        public AddressInformation AddressInformation { get; private set; }
        public IList<byte> Payload { get; private set; }
        public byte[] Bytes { get; private set; }
        public uint FrameControlField { get; private set; }

        public string StringView
        {
            get
            {
                var result = new StringBuilder();
                var nonPrintableCharacterFound = false;
                for(int i = 0; i < Payload.Count; i++)
                {
                    if(Payload[i] < ' ' || Payload[i] > '~')
                    {
                        nonPrintableCharacterFound = true;
                    }
                    else {
                        if(nonPrintableCharacterFound)
                        {
                            result.Append('.');
                            nonPrintableCharacterFound = false;
                        }

                        result.Append((char)Payload[i]);
                    }
                }

                return result.ToString();
            }
        }

        private Frame()
        {
        }

        private int GetAddressingFieldsLength()
        {
            var result = 0;
            result += DestinationAddressingMode.GetBytesLength();
            result += SourceAddressingMode.GetBytesLength();
            if(DestinationAddressingMode != AddressingMode.None)
            {
                result += 2;
            }
            if(!IntraPAN && SourceAddressingMode != AddressingMode.None)
            {
                result += 2;
            }
            return result;
        }

        private void Decode(byte[] data)
        {
            if(data.Length > 127)
            {
                throw new Exception("Frame length should not exceed 127 bytes.");
            }

            Type = (FrameType)(data[0] & 0x7);
            SecurityEnabled = (data[0] & 0x8) != 0;
            FramePending = (data[0] & 0x10) != 0;
            AcknowledgeRequest = IsAcknowledgeRequested(data);
            IntraPAN = (data[0] & 0x40) != 0;
            DestinationAddressingMode = (AddressingMode)((data[1] >> 2) & 0x3);
            FrameVersion = (byte)((data[1] >> 4) & 0x3);
            SourceAddressingMode = (AddressingMode)(data[1] >> 6);

            FrameControlField = ((uint)data[0] << 8) + data[1];

            DataSequenceNumber = data[2];
            AddressInformation = new AddressInformation(DestinationAddressingMode, SourceAddressingMode, IntraPAN, new ArraySegment<byte>(data, 3, GetAddressingFieldsLength()));
            Payload = new ArraySegment<byte>(data, 3 + AddressInformation.Bytes.Count, Length - (5 + AddressInformation.Bytes.Count));
        }

        private void Encode()
        {
            var bytes = new List<byte>();
            var frameControlByte0 = (byte)Type;
            if(FramePending)
            {
                frameControlByte0 |= (0x1 << 4);
            }
            bytes.Add(frameControlByte0);
            bytes.Add(0); // frameControlByte1

            bytes.Add(DataSequenceNumber);
            if(AddressInformation != null && AddressInformation.Bytes.Count > 0)
            {
                bytes.AddRange(AddressInformation.Bytes);
            }
            if(Payload != null && Payload.Count > 0)
            {
                bytes.AddRange(Payload);
            }

            var crc = CalculateCRC(bytes);
            bytes.Add(crc[0]);
            bytes.Add(crc[1]);

            Bytes = bytes.ToArray();
        }

        public bool CheckCRC()
        {
            var crc = CalculateCRC(Bytes.Take(Bytes.Length - 2));
            return Bytes[Bytes.Length - 2] == crc[0] && Bytes[Bytes.Length - 1] == crc[1];
        }

        public static byte[] CalculateCRC(IEnumerable<byte> bytes)
        {
            var crc = 0x00;
            foreach(var b in bytes)
            {
                crc = AddByte(crc, BitHelper.ReverseBits(b));
            }

            return new[] { BitHelper.ReverseBits((byte)(crc >> 8)), BitHelper.ReverseBits((byte)crc) };
        }

        private static int AddByte(int currentCrc, byte b)
        {
            int newCrc = (((currentCrc >> 8) & 0xff) | (currentCrc << 8) & 0xffff);
            newCrc ^= b & 0xff;
            newCrc ^= (newCrc & 0xff) >> 4;
            newCrc ^= (newCrc << 12) & 0xffff;
            newCrc ^= (newCrc & 0xff) << 5;
            newCrc = newCrc & 0xffff;
            return newCrc;
        }
    }
}

