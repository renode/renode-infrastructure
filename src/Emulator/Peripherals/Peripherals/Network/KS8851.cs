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
using Antmicro.Renode.Peripherals.SPI;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public class KS8851 : ISPIPeripheral, IMACInterface
    {
        public KS8851()
        {
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            currentLength = 4;
            request = new byte[10240];
            response = new byte[10240];
            packetQueue = new Queue<EthernetFrame>();
            IRQ = new GPIO();
        }

        public GPIO IRQ { get; private set; }

        public void Reset()
        {
            interruptsEnabled = true;
        }

        public MACAddress MAC { get; set; }

        public void ReceiveFrame(EthernetFrame frame)
        {
            if(!frame.DestinationMAC.IsBroadcast && frame.DestinationMAC != MAC)
            {
                return;
            }
            lock(packetQueue)
            {
                packetQueue.Enqueue(frame);
                SignalInterrupt();
            }
        }

        public byte Transmit(byte data)
        {
            if(mode == Mode.SendingPacket)
            {
                goto RESULT;
            }
            if(counter == 1 && request[0] == 0x80)
            {
                mode = Mode.SendingPacket;
                currentLength = nearestPacketLength;
                counter = 0;
            }
            request[counter] = data;
            if(counter == 1)
            {
                Message();
            }
        RESULT:
            var result = response[counter];
            counter = (counter + 1)%currentLength;
            if(counter == 0)
            {
                Finished();
            }
            return result;
        }

        public void FinishTransmission()
        {
        }

        public void SignalInterrupt()
        {
            if(!interruptsEnabled)
            {
                return;
            }
            IRQ.Set(true);
            IRQ.Set(false);
        }

        public event Action<EthernetFrame> FrameReady;

        private int Align(int value)
        {
            return 4 * (int)Math.Ceiling(1.0 * value / 4);
        }

        private void Message()
        {
            var requestShort = BitConverter.ToUInt16(request, 0);
            this.NoisyLog("Request 0x{0:X} {1}.", requestShort, mode);
            var responseShort = (ushort)0;
            if((byte)requestShort == 0xC0 && mode != Mode.WaitingForPacket)
            {
                interruptAfterTransmision = (requestShort & 0x100) != 0;
                requestShort = 0xC0;
            }
            lastPacketType = requestShort;
            if(mode == Mode.WaitingForPacket)
            {
                return;
            }
            switch(requestShort)
            {
            case 0xF:
                responseShort = 0x8870;
                break;
            case 0xC0:
                currentLength = 5;
                break;
            case 0x4832:
                if(transmissionEnded)
                {
                    responseShort = Consts.TransmissionEnded;
                }
                lock(packetQueue)
                {
                    if(packetQueue.Count > 0)
                    {
                        responseShort |= Consts.PacketWaiting;
                    }
                }
                break;
            case 0xE00D:
                responseShort = 10000;
                break;
            case 0x740A:
                // number of packets?
                responseShort = 1;
                currentLength = 3;
                mode = Mode.Special;
                break;
            case 0x5448:
                currentLength = 3;
                mode = Mode.Special;
                break;
            case 0xF03D:
                currentLength = 6;
                mode = Mode.Special;
                lock(packetQueue)
                {
                    var frame = packetQueue.Dequeue();
                    var length = frame.Length + 4;
                    nearestPacketLength = Align(length) + 4;
                    frame.Bytes.CopyTo(response, 8);
                    response[4] = (byte)length;
                    response[5] = (byte)(length >> 8);
                }
                break;
            case 0x5408:
                PutMACInResponse(0, ref responseShort);
                break;
            case 0x5004:
                PutMACInResponse(1, ref responseShort);
                break;
            case 0x4C20:
                PutMACInResponse(2, ref responseShort);
                break;
            case 0x4810:
                PutMACInResponse(3, ref responseShort);
                break;
            case 0x4408:
                PutMACInResponse(4, ref responseShort);
                break;
            case 0x4004:
                PutMACInResponse(5, ref responseShort);
                break;
            case 0x200C:
                responseShort = 1 << 9; // CCR_EEPROM
                break;
            case 0x404E:
                interruptsEnabled = true;
                this.DebugLog("Interrupts enabled.");
                break;
            }
            response[2] = (byte)responseShort; 
            response[3] = (byte)(responseShort >> 8);
        }

        private void PutMACInResponse(int number, ref ushort responseShort)
        {
            currentLength = 3;
            responseShort = MAC.Bytes[number];
            mode = Mode.Special;
        }

        private void Finished()
        {
            if(mode == Mode.WaitingForPacket)
            {
                this.DebugLog("Packet received, LEN {7} {0:x} {1:x} {2:x} {3:x} (...) {4:x} {5:x} {6:x}", request[0], request[1], request[2], request[3], request[currentLength - 5], request[currentLength - 4], request[currentLength - 3], currentLength);
                var frame = new byte[currentLength];
                Array.Copy(request, 0, frame, 0, currentLength);
                //TODO: CRC handling
                if(!Misc.TryCreateFrameOrLogWarning(this, frame, out var ethernetFrame, addCrc: false))
                {
                    return;
                }
                FrameReady?.Invoke(ethernetFrame);
                mode = Mode.Standard;
                currentLength = 4;
                transmissionEnded = true;
                if(interruptAfterTransmision)
                {
                    SignalInterrupt();
                }
            }
            if(mode == Mode.SendingPacket)
            {
                mode = Mode.Standard;
                currentLength = 4;
                lock(packetQueue)
                {
                    if(packetQueue.Count > 0)
                    {
                        SignalInterrupt();
                    }
                }
            }
            if(mode == Mode.Special)
            {
                currentLength = 4;
                mode = Mode.Standard;
                return;
            }
            switch(lastPacketType)
            {
            case 0xC0:
                mode = Mode.WaitingForPacket;
                var encodedLength = request[3] + (request[4] << 8);
                this.DebugLog("Encoded length is 0x{0:X}.", encodedLength);
                currentLength = Align(encodedLength + 1 + 1);
                transmissionEnded = false;
                break;
            case 0xF:
                lastPacketType = 0;
                break;
            }
        }

        private int counter;
        private int currentLength = 4;
        private int nearestPacketLength;
        private Mode mode;
        private ushort lastPacketType;
        private bool interruptAfterTransmision;
        private bool transmissionEnded;
        private bool interruptsEnabled;
        private readonly Queue<EthernetFrame> packetQueue;
        private readonly byte[] request;
        private readonly byte[] response;

        private static class Consts
        {
            public static ushort TransmissionEnded = 1 << 14;
            public static ushort PacketWaiting = 1 << 13;
        }

        private enum Mode
        {
            Standard,
            Special,
            WaitingForPacket,
            SendingPacket
        }
    }
}

