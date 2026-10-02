//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public abstract class WirelessMedium : IHasChildren<IMediumFunction>, IExternal, IConnectable<IRadio>, INetworkLogWireless
    {
        public void AttachTo(IRadio radio)
        {
            lock(sync)
            {
                if(radios.ContainsKey(radio))
                {
                    throw new RecoverableException("Cannot attach to the provided radio as it is already registered in this wireless medium.");
                }
                radios.Add(radio, new Position());
                radio.FrameSent += FrameSentHandler;
            }
        }

        public void DetachFrom(IRadio radio)
        {
            lock(sync)
            {
                radios.Remove(radio);
                radioHooks.Remove(radio);
                radio.FrameSent -= FrameSentHandler;
            }
        }

        public void SetMediumFunction(IMediumFunction function)
        {
            lock(sync)
            {
                mediumFunction = function;
            }
        }

        public void SetPosition(IRadio radio, decimal x, decimal y, decimal z)
        {
            lock(sync)
            {
                if(radios.ContainsKey(radio))
                {
                    radios[radio] = new Position(x, y, z);
                }
                else
                {
                    EmulationManager.Instance.CurrentEmulation.TryGetEmulationElementName(radio, out string name);
                    this.Log(LogLevel.Error, $"Cannot set position for {name} as it is not registered in this wireless medium.");
                }
            }
        }

        public IEnumerable<string> GetNames()
        {
            lock(sync)
            {
                return new[] { mediumFunction.FunctionName };
            }
        }

        public IEnumerable<string> GetAttachedRadiosNames()
        {
            var result = new List<string>();
            var currentEmulation = EmulationManager.Instance.CurrentEmulation;
            KeyValuePair<IRadio, Position>[] radiosSnapshot;
            lock(sync)
            {
                radiosSnapshot = radios.ToArray();
            }
            foreach(var radio in radiosSnapshot)
            {
                currentEmulation.TryGetEmulationElementName(radio.Key, out var name);
                result.Add(name);
            }
            return result;
        }

        public void AttachHookToRadio(IRadio radio, Action<byte[]> hook)
        {
            lock(sync)
            {
                radioHooks[radio] = hook;
            }
        }

        public IMediumFunction TryGetByName(string name, out bool success)
        {
            lock(sync)
            {
                if(mediumFunction.FunctionName == name)
                {
                    success = true;
                    return mediumFunction;
                }
                success = false;
                return null;
            }
        }

        public event Action<IExternal, IRadio, IRadio, byte[]> FrameTransmitted;

        public event Action<IExternal, IRadio, byte[]> FrameProcessed;

        protected WirelessMedium()
        {
            radioHooks = new Dictionary<IRadio, Action<byte[]>>();
            mediumFunction = SimpleMediumFunction.Instance;
            radios = new Dictionary<IRadio, Position>();
            sync = new object();
        }

        private void FrameSentHandler(IRadio sender, byte[] packet)
        {
            Position senderPosition;
            IMediumFunction mediumFunctionSnapshot;
            ReceiverInfo[] receivers;
            lock(sync)
            {
                if(!radios.TryGetValue(sender, out senderPosition))
                {
                    return;
                }
                mediumFunctionSnapshot = mediumFunction;
                receivers = radios
                    .Where(x => x.Key != sender)
                    .Select(x => new ReceiverInfo(x.Key, x.Value,
                        radioHooks.TryGetValue(x.Key, out var hook) ? hook : null))
                    .ToArray();
            }

            var currentEmulation = EmulationManager.Instance.CurrentEmulation;
            currentEmulation.TryGetEmulationElementName(sender, out var senderName);

            FrameProcessed?.Invoke(this, sender, packet);

            if(!mediumFunctionSnapshot.CanTransmit(senderPosition))
            {
                this.NoisyLog("Packet from {0} can't be transmitted, size {1}.", senderName, packet.Length);
                return;
            }

            foreach(var receiverInfo in receivers)
            {
                var receiver = receiverInfo.Radio;

                currentEmulation.TryGetEmulationElementName(receiver, out var receiverName);
                if(!mediumFunctionSnapshot.CanReach(senderPosition, receiverInfo.Position) || receiver.Channel != sender.Channel)
                {
                    this.NoisyLog("Packet {0}:chan{1} -> {2}:chan{3} NOT delivered, size {4}.",
                          senderName, sender.Channel, receiverName, receiver.Channel, packet.Length);
                    continue;
                }

                var vts = TimeDomainsManager.Instance.GetEffectiveVirtualTimeStamp();

                var packetCopy = packet.ToArray();
                receiverInfo.Hook?.Invoke(packetCopy);

                if(receiver is ISlipRadio)
                {
                    // send immediately
                    receiver.ReceiveFrame(packetCopy, sender);
                    continue;
                }

                receiver.GetMachine().HandleTimeDomainEvent(receiver.ReceiveFrame, packetCopy, sender, vts, () =>
                {
                    this.NoisyLog("Packet {0}:chan{1} -> {2}:chan{3} delivered, size {4}.",
                          senderName, sender.Channel, receiverName, receiver.Channel, packet.Length);
                    FrameTransmitted?.Invoke(this, sender, receiver, packetCopy);
                });
            }
        }

        private readonly struct ReceiverInfo
        {
            public ReceiverInfo(IRadio radio, Position position, Action<byte[]> hook)
            {
                Radio = radio;
                Position = position;
                Hook = hook;
            }

            public IRadio Radio { get; }
            public Position Position { get; }
            public Action<byte[]> Hook { get; }
        }

        private IMediumFunction mediumFunction;
        private readonly object sync;
        private readonly Dictionary<IRadio, Action<byte[]>> radioHooks;
        private readonly Dictionary<IRadio, Position> radios;
    }
}