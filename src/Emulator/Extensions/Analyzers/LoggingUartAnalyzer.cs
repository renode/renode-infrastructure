//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Utilities;
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Analyzers
{
    // This class is marked as `IExternal` to allow access to
    // properties from Monitor. In order to do this one must create analyzer
    // and add it as external using command below:
    //
    // showAnalyzer "ExternalName" sysbus.uart_name LoggingUartAnalyzer
    //
    [Transient]
    public class LoggingUartAnalyzer : BasicPeripheralBackendAnalyzer<UARTBackend>, IExternal
    {
        public LoggingUartAnalyzer()
        {
            line = new StringBuilder(InitialCapacity);
            LogLevel = LogLevel.Info;

            TimestampFormat = TimestampType.Full;
        }

        public override void AttachTo(UARTBackend backend)
        {
            base.AttachTo(backend);
            uart = backend.UART;

            // let's find out to which machine this uart belongs
            machine = uart.GetMachine();
        }

        public override void Show()
        {
            baseStampHost = CustomDateTime.Now;
            lastLineStampHost = baseStampHost;
            lastLineStampVirtual = machine.ElapsedVirtualTime.TimeElapsed;

            uart.CharReceived += WriteChar;
        }

        public override void Hide()
        {
            if(!String.IsNullOrWhiteSpace(line.ToString()))
            {
                WriteChar(10); // this allows us to flush the last, unfinished line (e.g. the prompt) to the log
            }
            uart.CharReceived -= WriteChar;
        }

        public LogLevel LogLevel { get; set; }

        public TimestampType TimestampFormat { get; set; }

        private void WriteChar(byte value)
        {
            if(value == 10)
            {
                var hostNow = CustomDateTime.Now;
                var virtualNow = machine.LocalTimeSource.ElapsedVirtualTime;

                var logLineBuilder = new StringBuilder();
                logLineBuilder.Append("[");

                var anythingAdded = false;
                if(TimestampFormat == TimestampType.Full || TimestampFormat == TimestampType.Host)
                {
                    anythingAdded = true;

                    var hostTimestamp = string.Format("{0}s (+{1}s)",
                        Misc.NormalizeDecimal((hostNow - baseStampHost).TotalSeconds),
                        Misc.NormalizeDecimal((hostNow - lastLineStampHost).TotalSeconds));

                    if(hostTimestamp.Length < maxHostTimestampLength)
                    {
                        hostTimestamp = hostTimestamp.PadLeft(maxHostTimestampLength, ' ');
                    }
                    else
                    {
                        maxHostTimestampLength = hostTimestamp.Length;
                    }

                    logLineBuilder.AppendFormat("host: {0}", hostTimestamp);
                }

                if(TimestampFormat == TimestampType.Full || TimestampFormat == TimestampType.Virtual)
                {
                    if(anythingAdded)
                    {
                        logLineBuilder.Append("|");
                    }
                    anythingAdded = true;

                    var virtTimestamp = string.Format("{0}s (+{1}s)",
                        Misc.NormalizeDecimal(virtualNow.TotalSeconds),
                        Misc.NormalizeDecimal((virtualNow - lastLineStampVirtual).TotalSeconds));

                    if(virtTimestamp.Length < maxVirtTimestampLength)
                    {
                        virtTimestamp = virtTimestamp.PadLeft(maxVirtTimestampLength, ' ');
                    }
                    else
                    {
                        maxVirtTimestampLength = virtTimestamp.Length;
                    }

                    logLineBuilder.AppendFormat("virt: {0}", virtTimestamp);
                }

                if(!anythingAdded)
                {
                    logLineBuilder.Append("output");
                }

                logLineBuilder.AppendFormat("] {0}", line.ToString());

                uart.Log(LogLevel, logLineBuilder.ToString());

                lastLineStampHost = hostNow;
                lastLineStampVirtual = virtualNow;
                line.Clear();

                return;
            }
            if(char.IsControl((char)value))
            {
                return;
            }

            var nextCharacter = (char)value;
            line.Append(nextCharacter);
        }

        private DateTime baseStampHost;
        private DateTime lastLineStampHost;
        private TimeInterval lastLineStampVirtual;
        private IUART uart;
        private IMachine machine;

        private int maxHostTimestampLength;
        private int maxVirtTimestampLength;

        private readonly StringBuilder line;

        private const int InitialCapacity = 120;

        public enum TimestampType
        {
            None,
            Virtual,
            Host,
            Full
        }
    }
}

