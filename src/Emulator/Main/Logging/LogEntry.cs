//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Logging
{
    public sealed class LogEntry : ISpeciallySerializable
    {
        public LogEntry(DateTime time, LogLevel level, string message, int sourceId = NoSource, bool forceMachineName = false, int? threadId = null)
        {
            Message = message;
            numericLogLevel = level.NumericLevel;
            SourceId = sourceId;
            Time = time;
            ThreadId = threadId;
            ForceMachineName = forceMachineName;
            GetNames();
        }

        public bool EqualsWithoutIdAndTime(LogEntry entry)
        {
            return entry != null &&
                numericLogLevel == entry.numericLogLevel &&
                ThreadId == entry.ThreadId &&
                SourceId == entry.SourceId &&
                Message == entry.Message;
        }

        public override bool Equals(object obj)
        {
            var leobj = obj as LogEntry;
            if(leobj != null)
            {
                return Id == leobj.Id;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (int)Id;
        }

        public int GetHashCodeWithoutIdAndTime()
        {    
            int hash = 17;
            hash = hash * 23 + numericLogLevel;
            hash = hash * 23 + Message.GetHashCode();
            hash = hash * 23 + SourceId;
            hash = hash * 23 + (ThreadId ?? -1);

            return hash;
        }

        public void Load(PrimitiveReader reader)
        {
            Id = reader.ReadUInt64();
            Message = reader.ReadString();
            SourceId = reader.ReadInt32();
            ThreadId = reader.ReadInt32();
            Time = new DateTime(reader.ReadInt64());
            numericLogLevel = reader.ReadInt32();
            GetNames();

            if(ThreadId == -1)
            {
                ThreadId = null;
            }
        }

        public void Save(PrimitiveWriter writer)
        {
            writer.Write(Id);
            writer.Write(Message);
            writer.Write(SourceId);
            writer.Write(ThreadId ?? -1);
            writer.Write(Time.Ticks);
            writer.Write(numericLogLevel);
        }

        public ulong Id { get; set; }
        public int SourceId { get; private set; }
        public string Message { get; private set; }
        public int? ThreadId { get; private set; }
        public DateTime Time { get; private set; }
        public LogLevel Type
        {
            get
            {
                return (LogLevel)numericLogLevel;
            }
        }

        public string ObjectName
        {
            get
            {
                if(objectName != null && objectName.StartsWith(string.Format("{0}.", Machine.SystemBusName)))
                {
                    objectName = objectName.Substring(Machine.SystemBusName.Length + 1);
                }
                return objectName;
            }
        }

        public string MachineName
        {
            get
            {
                return machineName;
            }
        }

        public bool ForceMachineName { get; }

        private void GetNames()
        {
            if(SourceId != NoSource)
            {
                EmulationManager.Instance.CurrentEmulation.CurrentLogger.TryGetName(SourceId, out objectName, out machineName);
            }
        }

        private int numericLogLevel;
        private string objectName;
        private string machineName;

        private const int NoSource = -1;
    }
}

