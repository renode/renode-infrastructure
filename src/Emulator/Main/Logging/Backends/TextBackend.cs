//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Logging
{
    public abstract class TextBackend : LoggerBackend
    {
        protected virtual string FormatLogEntry(LogEntry entry)
        {
            var messageBuilder = new StringBuilder();
            var messages = entry.Message.Split('\n').GetEnumerator();
            messages.MoveNext();

            if(entry.ObjectName != null)
            {
                var currentEmulation = EmulationManager.Instance.CurrentEmulation;
                var machineCount = currentEmulation.MachinesCount;
                if((entry.ForceMachineName || machineCount > 1) && entry.MachineName != null)
                {
                    messageBuilder.AppendFormat("{2}/{0}: {1}", entry.ObjectName, messages.Current, entry.MachineName);
                }
                else
                {
                    messageBuilder.AppendFormat("{0}: {1}", entry.ObjectName, messages.Current);
                }
            }
            else
            {
                messageBuilder.Append(messages.Current);
            }
            while(messages.MoveNext())
            {
                messageBuilder.Append(Environment.NewLine);
                messageBuilder.Append("    ");
                messageBuilder.Append(messages.Current);
            }

            if(entry.Count > 1)
            {
                messageBuilder.AppendFormat(" ({0})", entry.Count);
            }

            return messageBuilder.ToString();
        }
    }
}

