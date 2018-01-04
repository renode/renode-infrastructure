//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public static class WirelessExtensions
    {
        public static void CreateWirelessMedium(this Emulation emulation, string name)
        {
            emulation.ExternalsManager.AddExternal(new WirelessMedium(), name);
        }
    }

    public sealed class WirelessMedium : SynchronizedExternalBase, IHasChildren<IMediumFunction>, IExternal, IConnectable<IRadio>, INetworkLogWireless
    {
        public WirelessMedium()
        {
            packetsToSend = new Queue<PacketWithSender>();
            radios = new Dictionary<IRadio, Position>();
            mediumFunction = SimpleMediumFunction.Instance;
        }

        public void AttachTo(IRadio radio)
        {
            radios.Add(radio, new Position());
            radio.FrameSent += FrameSentHandler;
        }

        public void DetachFrom(IRadio radio)
        {
            radios.Remove(radio);
            radio.FrameSent -= FrameSentHandler;
        }

        public void SetMediumFunction(IMediumFunction function)
        {
            mediumFunction = function;
        }

        public void SetPosition(IRadio radio, decimal x, decimal y, decimal z)
        {
            radios[radio] = new Position(x, y, z);
        }

        public IEnumerable<string> GetNames()
        {
            return new[] {mediumFunction.FunctionName};
        }

        public IMediumFunction TryGetByName(string name, out bool success)
        {
            if(mediumFunction.FunctionName == name)
            {
                success = true;
                return mediumFunction;
            }
            success = false;
            return null;
        }

        public event Action<IExternal, IRadio, IRadio, byte[]> FrameTransmitted;
        public event Action<IExternal, IRadio, byte[]> FrameProcessed;

        private void FrameSentHandler(IRadio sender, byte[] packet)
        {
            lock(packetsToSend)
            {
                packetsToSend.Enqueue(new PacketWithSender(packet, sender.Channel, sender, radios[sender]));
                if(executeOnSyncAlreadyQueued)
                {
                    return;
                }
                executeOnSyncAlreadyQueued = true;
                ExecuteOnNearestSync(NearestSyncHandler);
            }
        }

        private void NearestSyncHandler()
        {
            var frameTransmitted = FrameTransmitted;
            var frameProcessed = FrameProcessed;

            lock(packetsToSend)
            {
                executeOnSyncAlreadyQueued = false;
                while(packetsToSend.Count > 0)
                {
                    var packet = packetsToSend.Dequeue();

                    var sender = packet.Sender;
                    var senderPosition = packet.SenderPosition;
                    var senderName = sender.ToString();
                    var currentEmulation = EmulationManager.Instance.CurrentEmulation;
                    currentEmulation.TryGetEmulationElementName(sender, out senderName);

                    if(frameProcessed != null)
                    {
                        frameProcessed(this, sender, packet.Frame);
                    }

                    if(!mediumFunction.CanTransmit(packet.SenderPosition))
                    {
                        this.NoisyLog("Packet from {0} can't be transmitted, size {1}.", senderName, packet.Frame.Length);
                        continue;
                    }

                    foreach(var radioAndPosition in radios.Where(x => x.Key != packet.Sender))
                    {
                        var receiver = radioAndPosition.Key;
                        var receiverPosition = radioAndPosition.Value;
                        var receiverName = receiver.ToString();

                        currentEmulation.TryGetEmulationElementName(receiver, out receiverName);

                        if(!mediumFunction.CanReach(senderPosition, receiverPosition) || receiver.Channel != packet.Channel)
                        {
                            this.NoisyLog("Packet {0} -> {1} NOT delivered, size {2}.", senderName, receiverName, packet.Frame.Length);
                            continue;
                        }

                        this.NoisyLog("Packet {0} -> {1} delivered, size {2}.", senderName, receiverName, packet.Frame.Length);
                        receiver.ReceiveFrame(packet.Frame.ToArray());

                        if(frameTransmitted != null)
                        {
                            frameTransmitted(this, sender, receiver, packet.Frame);
                        }
                    }
                }
            }
        }

        private bool executeOnSyncAlreadyQueued;
        private IMediumFunction mediumFunction;
        private readonly Dictionary<IRadio, Position> radios;
        private readonly Queue<PacketWithSender> packetsToSend;

        private sealed class PacketWithSender
        {
            public PacketWithSender(byte[] frame, int channel, IRadio sender, Position senderPosition)
            {
                Frame = frame;
                Channel = channel;
                Sender = sender;
                SenderPosition = senderPosition;
            }

            public int Channel { get; private set; }
            public byte[] Frame { get; private set; }
            public IRadio Sender { get; private set; }
            public Position SenderPosition { get; private set; }
        }
    }
}

