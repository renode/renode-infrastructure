//
// Copyright (c) 2010-2018 Antmicro
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
    // This class is marked as `IExternal` to allow access to `LogLevel`
    // property from monitor. In order to do this one must create analyzer
    // and add it as external using command below:
    //
    // showAnalyzer "ExternalName" sysbus.uart_name Antmicro.Renode.Analyzers.LoggingAnalyzer
    //
    [Transient]
    public class LoggingUartAnalyzer : BasicPeripheralBackendAnalyzer<UARTBackend>, IExternal
    {
        public LoggingUartAnalyzer()
        {
            line = new StringBuilder(InitialCapacity);
            LogLevel = LogLevel.Info;
        }

        public override void AttachTo(UARTBackend backend)
        {
            base.AttachTo(backend);
            uart = backend.UART;

            // let's find out to which machine this uart belongs
            if(!EmulationManager.Instance.CurrentEmulation.TryGetMachineForPeripheral(uart, out machine))
            {
                throw new RecoverableException("Given uart does not belong to any machine.");
            }
        }

        public override void Show()
        {
            lastLineStampHost = CustomDateTime.Now;
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

        private void WriteChar(byte value)
        {
            if(value == 10)
            {
                var now = CustomDateTime.Now;
                var virtualNow = machine.ElapsedVirtualTime.TimeElapsed;
                uart.Log(LogLevel, "[+{0}s host +{1}s virt {2}s virt from start] {3}",
                    Misc.NormalizeDecimal((now - lastLineStampHost).TotalSeconds),
                    Misc.NormalizeDecimal((virtualNow - lastLineStampVirtual).TotalSeconds),
                    Misc.NormalizeDecimal(machine.ElapsedVirtualTime.TimeElapsed.TotalSeconds),
                    line.ToString());
                lastLineStampHost = now;
                lastLineStampVirtual = virtualNow;
                line.Clear();
                line.Append("  ");
                return;
            }
            if(char.IsControl((char)value))
            {
                return;
            }

            var nextCharacter = (char)value;
            line.Append(nextCharacter);
        }

        private DateTime lastLineStampHost;
        private TimeInterval lastLineStampVirtual;
        private IUART uart;
        private Machine machine;

        private readonly StringBuilder line;

        private const int InitialCapacity = 120;
    }
}

