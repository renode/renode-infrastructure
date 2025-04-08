//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public static class LogicGateExtensions
    {
        public static void CreateLogicGate(this Emulation emulation, string name, LogicGate.LogicMode mode, int numberOfInputs = 2, bool invertedOutput = false)
        {
            emulation.ExternalsManager.AddExternal(new LogicGate(mode, numberOfInputs, invertedOutput), name);
        }
    }

    public class LogicGate : IExternal, IGPIOReceiver
    {
        public LogicGate(LogicMode mode, int numberOfInputs, bool invertedOutput = false)
        {
            if(!Enum.IsDefined(typeof(LogicMode), mode))
            {
                throw new ConstructionException($"{mode} is invalid argument for '{nameof(mode)}'");
            }

            Mode = mode;
            InvertedOutput = invertedOutput;

            invertedInputs = new bool[numberOfInputs];
            inputs = new bool[numberOfInputs];
        }

        public LogicGate(LogicMode mode, bool invertedOutput = false, bool invertedA = false, bool invertedB = false) : this(mode, 2, invertedOutput)
        {
            SetInvertedInput(0, invertedA);
            SetInvertedInput(1, invertedB);
        }

        public void SetInvertedInput(int index, bool inverted)
        {
            if(index < 0 || index > invertedInputs.Length)
            {
                throw new RecoverableException($"'{nameof(index)}' should be between 0 and {invertedInputs.Length - 1}");
            }

            invertedInputs[index] = inverted;
        }

        public void AttachInput(INumberedGPIOOutput sender, int pinNumber, int inputPin)
        {
            if(!sender.Connections.TryGetValue(pinNumber, out var gpio))
            {
                throw new RecoverableException($"{sender.GetName()}@{pinNumber} is an invalid GPIO");
            }

            if(!TryAttachInput(gpio, inputPin))
            {
                throw new RecoverableException($"{sender.GetName()}@{pinNumber} is already attached as #{inputPin} input");
            }
        }

        public void AttachInput(IGPIO sender, int inputPin)
        {
            if(!TryAttachInput(sender, inputPin))
            {
                throw new RecoverableException($"This gpio is already connected as #{inputPin} input");
            }
        }

        public void DetachInput(INumberedGPIOOutput sender, int pinNumber, int inputPin)
        {
            if(!sender.Connections.TryGetValue(pinNumber, out var gpio))
            {
                throw new RecoverableException($"{sender.GetName()}@{pinNumber} is an invalid GPIO");
            }

            if(!TryDetachInput(gpio, inputPin))
            {
                throw new RecoverableException($"{sender.GetName()}@{pinNumber} wasn't connected as #{inputPin} input");
            }
        }

        public void DetachInput(IGPIO sender, int inputPin)
        {
            if(!TryDetachInput(sender, inputPin))
            {
                throw new RecoverableException($"This gpio wasn't connected as #{inputPin} input");
            }
        }

        public void AttachOutput(IGPIOReceiver receiver, int pinNumber = 0)
        {
            if(!receivers.TryGetValue(receiver, out var pinNumbers))
            {
                pinNumbers = new List<int> { pinNumber };
                receivers.Add(receiver, pinNumbers);
                return;
            }

            if(pinNumbers.Contains(pinNumber))
            {
                throw new RecoverableException($"{receiver.GetName()}@{pinNumber} is already connected as output");
            }

            pinNumbers.Add(pinNumber);
        }

        public void DetachOutput(IGPIOReceiver receiver, int? pinNumber = null)
        {
            if(!receivers.ContainsKey(receiver))
            {
                throw new RecoverableException($"{receiver.GetName()} wasn't connected as output");
            }

            if(!pinNumber.HasValue)
            {
                receivers.Remove(receiver);
                return;
            }

            var pinNumbers = receivers[receiver];
            if(!pinNumbers.Contains(pinNumber.Value))
            {
                throw new RecoverableException($"{receiver.GetName()}@{pinNumber} wasn't connected as output");
            }

            pinNumbers.Remove(pinNumber.Value);
        }

        public void OnGPIO(int pin, bool value)
        {
            if(pin < 0 || pin >= inputs.Length)
            {
                this.Log(LogLevel.Warning, "This peripherals supports gpio inputs between 0 and {0}, but {1} was called", inputs.Length - 1, pin);
                return;
            }

            inputs[pin] = value;
            var values = inputs.Zip(InvertedInputs, (val, inverted) => inverted ? !val : val);

            bool output;
            switch(Mode)
            {
            case LogicMode.And:
                output = values.All(v => v);
                break;

            case LogicMode.Or:
                output = values.Any(v => v);
                break;

            case LogicMode.Xor:
                output = values.Aggregate(false, (acc, val) => acc ^ val);
                break;

            default:
                throw new Exception("unreachable");
            }

            Output.Set(InvertedOutput ? !output : output);

            if(receivers.Count == 0)
            {
                // NOTE: Short-circuit if this isn't used as external
                return;
            }

            if(!TimeDomainsManager.Instance.TryGetVirtualTimeStamp(out var vts))
            {
                vts = new TimeStamp(default(TimeInterval), EmulationManager.ExternalWorld);
            }

            foreach(var keyValue in receivers)
            {
                var receiver = keyValue.Key;
                var pinNumbers = keyValue.Value;

                foreach(var pinNumber in pinNumbers)
                {
                    receiver.GetMachine().HandleTimeDomainEvent(receiver.OnGPIO, pinNumber, Output.IsSet, vts);
                }
            }
        }

        public void Reset()
        {
            // NOTE: Intentionally left empty
        }

        public GPIO Output { get; } = new GPIO();

        public LogicMode Mode { get; }

        public IEnumerable<bool> InvertedInputs => invertedInputs;

        public bool InvertedOutput { get; }

        private bool TryAttachInput(IGPIO sender, int inputPin)
        {
            var keyValue = new KeyValuePair<int, IGPIO>(inputPin, sender);
            if(senders.Contains(keyValue))
            {
                return false;
            }

            senders.Add(keyValue);
            sender.Connect(this, inputPin);
            return true;
        }

        private bool TryDetachInput(IGPIO sender, int inputPin)
        {
            var keyValue = new KeyValuePair<int, IGPIO>(inputPin, sender);
            if(!senders.Contains(keyValue))
            {
                return false;
            }

            senders.Remove(keyValue);
            var endpoint = sender.Endpoints.Where(entry => entry.Receiver == this && entry.Number == inputPin)
                .Single();
            sender.Disconnect(endpoint);
            return true;
        }

        private readonly bool[] inputs;
        private readonly bool[] invertedInputs;
        private readonly List<KeyValuePair<int, IGPIO>> senders = new List<KeyValuePair<int, IGPIO>>();
        private readonly Dictionary<IGPIOReceiver, List<int>> receivers = new Dictionary<IGPIOReceiver, List<int>>();

        public enum LogicMode
        {
            And,
            Or,
            Xor,
        }
    }
}
