//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Reflection;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Logging
{
    [Convertible]
    public sealed class LogLevel : IEquatable<LogLevel>, IComparable<LogLevel>
    {
        public static LogLevel Noisy = new LogLevel(Level.Noisy);
        public static LogLevel Debug = new LogLevel(Level.Debug);
        public static LogLevel Info = new LogLevel(Level.Info, ConsoleColor.Green);
        public static LogLevel Warning = new LogLevel(Level.Warning, ConsoleColor.DarkYellow);
        public static LogLevel Error = new LogLevel(Level.Error, ConsoleColor.Red);

        private readonly Level type;

        private enum Level
        {
            Noisy = -1,
            Debug = 0,
            Info = 1,
            Warning = 2,
            Error = 3
        }

        static LogLevel()
        {
            // static fields of LogLevel type define possible LogLevel values
            AvailableLevels = typeof(LogLevel).GetFields(BindingFlags.Public | BindingFlags.Static).Where(x => x.FieldType.Equals(typeof(LogLevel)))
                .Select(x => (LogLevel)x.GetValue(null)).ToArray();
        }

        public static LogLevel[] AvailableLevels { get; private set; }

        public int NumericLevel { get; private set; }

        public override string ToString()
        {
            return type.ToString().ToUpper();
        }

        public string ToStringCamelCase()
        {
            return type.ToString();
        }

        public LogLevel[] ThisAndHigher()
        {
            return AvailableLevels.Where(l => l.type >= type).ToArray();
        }

        public ConsoleColor? Color { get; private set; }

        public static bool operator <(LogLevel first, LogLevel second)
        {
            return first.type < second.type;
        }

        public static bool operator <=(LogLevel first, LogLevel second)
        {
            return first.type <= second.type;
        }

        public static bool operator >(LogLevel first, LogLevel second)
        {
            return first.type > second.type;
        }

        public static bool operator >=(LogLevel first, LogLevel second)
        {
            return first.type >= second.type;
        }

        public static bool operator ==(LogLevel first, LogLevel second)
        {
            if(Object.ReferenceEquals(null, first) ^ Object.ReferenceEquals(null, second))
                return false;
            if(Object.ReferenceEquals(null, first) && Object.ReferenceEquals(null, second))
            {
                return true;
            }
            return first.type == second.type;
        }

        public static bool operator !=(LogLevel first, LogLevel second)
        {
            return !(first == second);
        }

        // parse is not case sensitive
        public static bool TryParse(string type, out LogLevel logLevel)
        {
            logLevel = AvailableLevels.FirstOrDefault(x => string.Compare(type, x.ToString(), StringComparison.OrdinalIgnoreCase) == 0);
            return logLevel != null;
        }

        public static bool TryToCreateFromInteger(int level, out LogLevel logLevel)
        {
            logLevel = AvailableLevels.FirstOrDefault(x => x.NumericLevel == level);
            if(logLevel == null)
            {
                return false;
            }
            return true;
        }

        public static LogLevel Parse(string type)
        {
            LogLevel result;
            if(!TryParse(type, out result))
            {
                throw new FormatException(string.Format("Cannot parse value '{0}' to correct log level.", type));
            }
            return result;
        }

        public static explicit operator LogLevel(string type)
        {
            return Parse(type);
        }

        public static explicit operator LogLevel(int level)
        {
            LogLevel logLevel;
            if(!TryToCreateFromInteger(level, out logLevel))
            {
                throw new InvalidCastException(string.Format("Cannot convert from level {0} to correct log level.", level));
            }
            return logLevel;
        }

        public static explicit operator int(LogLevel type)
        {
            return type.NumericLevel;
        }

        private LogLevel(Level type, ConsoleColor? color = null)
        {
            this.type = type;
            this.Color = color;
            NumericLevel = (int)type;
        }

        public bool Equals(LogLevel other)
        {
            if(other != null)
                return this.type == other.type;
            return false;
        }

        public override bool Equals(object obj)
        {
            var log = obj as LogLevel;
            if(log != null)
                return Equals(log);
            return false;
        }

        public int CompareTo(LogLevel other)
        {
            return type.CompareTo(other.type);
        }

        public override int GetHashCode()
        {
            return type.GetHashCode();
        }

    }
}

