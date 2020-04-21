//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Core.USB
{
    public struct USBTransactionStage
    {
        // SETUP is a combination of USB SETUP token and USB DATA token
        public static USBTransactionStage Setup(byte[] payload)
        {   
            if(payload.Length != 8)
            {
                throw new ArgumentException("SETUP data must be 8 bytes long");
            }

            return new USBTransactionStage(USBPacketId.SetupToken, payload);
        }

        public static USBTransactionStage Stall()
        {
            return new USBTransactionStage(USBPacketId.StallHandshake);
        }

        public static USBTransactionStage Ack()
        {
            return new USBTransactionStage(USBPacketId.AckHandshake);
        }

        public static USBTransactionStage NotAck()
        {
            return new USBTransactionStage(USBPacketId.NakHandshake);
        }

        public static USBTransactionStage In()
        {
            return new USBTransactionStage(USBPacketId.InToken);
        }

        // OUT is a combination of USB OUT packet and USB DATA packet
        public static USBTransactionStage Out(byte[] payload, bool data0 = false)
        {   
            return new USBTransactionStage(USBPacketId.OutToken, payload);
        }

        // DATA is used only as a response to USB IN packet
        public static USBTransactionStage Data(byte[] payload, bool data0 = true)
        {   
            return new USBTransactionStage(data0 ? USBPacketId.Data0 : USBPacketId.Data1, payload);
        }
        
        public USBTransactionStage(USBPacketId id)
        {
            PacketID = id;
            Payload = new byte[0];
        }

        public USBTransactionStage(USBPacketId id, byte[] payload)
        {
            PacketID = id;
            Payload = payload;
        }

        public override string ToString()
        {
            return $"[USB transaction stage: {PacketID} (0x{PacketID:X}) with payload of length {Payload.Length} bytes]";
        }

        public USBPacketId PacketID { get; }
        public byte[] Payload { get; }
    }
}

