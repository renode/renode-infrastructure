//
// Copyright (c) 2010-2018 Antmicro
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

namespace Antmicro.Renode.Peripherals.Wireless
{
    public static class WirelessExtensions
    {
        public static void CreateWirelessMedium(this Emulation emulation, string name)
        {
            emulation.ExternalsManager.AddExternal(new WirelessMedium(), name);
        }
    }

    public sealed class WirelessMedium : IHasChildren<IMediumFunction>, IExternal, IConnectable<IRadio>, INetworkLogWireless
    {
        public WirelessMedium()
        {
            radios = new Dictionary<IRadio, Position>();
            radioHooks = new Dictionary<IRadio, Action<byte[]>>();
            mediumFunction = SimpleMediumFunction.Instance;
        }

        public void AttachTo(IRadio radio)
        {
            if(radios.ContainsKey(radio))
            {
                throw new RecoverableException("Cannot attach to the provided radio as it is already registered in this wireless medium.");
            }
            radios.Add(radio, new Position());
            radio.FrameSent += FrameSentHandler;
        }

        public void DetachFrom(IRadio radio)
        {
            radios.Remove(radio);
            radioHooks.Remove(radio);
            radio.FrameSent -= FrameSentHandler;
        }

        public void SetMediumFunction(IMediumFunction function)
        {
            mediumFunction = function;
        }

        public void SetPosition(IRadio radio, decimal x, decimal y, decimal z)
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

        public IEnumerable<string> GetNames()
        {
            return new[] { mediumFunction.FunctionName };
        }

        public IEnumerable<string> GetAttachedRadiosNames()
        {
            var result = new List<string>();
            var currentEmulation = EmulationManager.Instance.CurrentEmulation;
            foreach(var radio in radios)
            {
                currentEmulation.TryGetEmulationElementName(radio.Key, out var name);
                result.Add(name);
            }
            return result;
        }

        public void AttachHookToRadio(IRadio radio, Action<byte[]> hook)
        {
            radioHooks[radio] = hook;
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
            var senderPosition = radios[sender];
            var currentEmulation = EmulationManager.Instance.CurrentEmulation;
            currentEmulation.TryGetEmulationElementName(sender, out var senderName);

            FrameProcessed?.Invoke(this, sender, packet);

            if(!mediumFunction.CanTransmit(senderPosition))
            {
                this.NoisyLog("Packet from {0} can't be transmitted, size {1}.", senderName, packet.Length);
                return;
            }

            foreach(var radioAndPosition in radios.Where(x => x.Key != sender))
            {
                var receiver = radioAndPosition.Key;

                currentEmulation.TryGetEmulationElementName(receiver, out var receiverName);
                if(!mediumFunction.CanReach(senderPosition, radioAndPosition.Value) || receiver.Channel != sender.Channel)
                {
                    this.NoisyLog("Packet {0} -> {1} NOT delivered, size {2}.", senderName, receiverName, packet.Length);
                    continue;
                }

                if(!TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts))
                {
                    // e.g. when the sender is a SLIP radio
                    vts = new TimeStamp(default(TimeInterval), EmulationManager.ExternalWorld);
                }

                var packetCopy = packet.ToArray();
                if(radioHooks.TryGetValue(receiver, out var hook))
                {
                    hook(packetCopy);
                }

                if(receiver is ISlipRadio)
                {
                    // send immediately
                    receiver.ReceiveFrame(packetCopy);
                    continue;
                }

                receiver.GetMachine().HandleTimeDomainEvent(receiver.ReceiveFrame, packetCopy, vts, () =>
                {
                    this.NoisyLog("Packet {0} -> {1} delivered, size {2}.", senderName, receiverName, packetCopy.Length);
                    FrameTransmitted?.Invoke(this, sender, receiver, packetCopy);
                });
            }
        }

        private IMediumFunction mediumFunction;
        private readonly Dictionary<IRadio, Action<byte[]>> radioHooks;
        private readonly Dictionary<IRadio, Position> radios;
    }
}
