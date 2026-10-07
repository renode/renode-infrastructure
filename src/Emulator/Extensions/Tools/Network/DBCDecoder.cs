//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Text;

using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

using DbcParserLib;
using DbcParserLib.Observers;

namespace Antmicro.Renode.Tools.Network;

#nullable enable

public class DBCDecoder
{
    public DBCDecoder(string path)
    {
        var failureObserver = new SimpleFailureObserver();
        Parser.SetParsingFailuresObserver(failureObserver);
        dbc = Parser.ParseFromPath(path);
        if(failureObserver.GetErrorList().Any())
        {
            var errorTextBuilder = new StringBuilder();
            foreach(var error in failureObserver.GetErrorList())
            {
                errorTextBuilder.AppendLine(error);
            }
            throw new RecoverableException($"Failed to parse DBC file {path}: {errorTextBuilder}");
        }
        Logger.Log(LogLevel.Info, "Loaded DBC database: {0} message definitions added", dbc.Messages.Count());
    }

    public bool TryDecodeCANMessage(CANMessageFrame frame, out DecodedMessage? decodedMessage)
    {
        var message = dbc.Messages.Where(m => m.ID == frame.Id).FirstOrDefault();

        if(message is null)
        {
            decodedMessage = null;
            return false;
        }

        var signals = new DecodedSignal[message.Signals.Count()];

        for(var i = 0; i < signals.Length; i++)
        {
            var signal = message.Signals[i];
            signals[i] = new DecodedSignal(signal.Name, Packer.RxSignalUnpack(frame.Data, signal), signal.Unit);
        }

        decodedMessage = new DecodedMessage(message.Name, signals);

        return true;
    }

    private readonly Dbc dbc;

    public class DecodedMessage
    {
        public DecodedMessage(string name, DecodedSignal[] signals)
        {
            Name = name;
            Signals = signals;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Name: {Name}");
            foreach(var signal in Signals)
            {
                builder.AppendLine($"   {signal}");
            }
            return builder.ToString();
        }

        public string Name { get; init; }

        public DecodedSignal[] Signals { get; init; }
    }

    public class DecodedSignal
    {
        public DecodedSignal(string name, double value, string unit)
        {
            Name = name;
            Value = value;
            Unit = unit;
        }

        public override string ToString()
        {
            return $"{Name}: {Value} {Unit}";
        }

        public string Name { get; init; }

        public double Value { get; init; }

        public string Unit { get; init; }
    }
}
