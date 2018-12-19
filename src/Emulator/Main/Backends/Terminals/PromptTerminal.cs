//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Time;
using System.Text;

namespace Antmicro.Renode.Backends.Terminals
{
    [Transient]
    public sealed class PromptTerminal : BackendTerminal
    {
        public PromptTerminal(Action<string, TimeInterval> onLine = null, Action<TimeInterval> onPrompt = null, string prompt = null)
        {
            internalLock = new object();
            buffer = new StringBuilder();
            if(!string.IsNullOrEmpty(prompt))
            {
                Prompt = prompt;
            }
            this.onLine = onLine;
            this.onPrompt = onPrompt;
        }

        public override void AttachTo(IUART uart)
        {
            lock(internalLock)
            {
                if(!EmulationManager.Instance.CurrentEmulation.TryGetMachineForPeripheral(uart, out machine))
                {
                    throw new ArgumentException("Could not find machine for uart");
                }
                base.AttachTo(uart);
            }
        }

        public void AttachTo(IUART uart, Machine machine)
        {
            lock(internalLock)
            {
                this.machine = machine;
                base.AttachTo(uart);
            }
        }

        public override void DetachFrom(IUART uart)
        {
            lock(internalLock)
            {
                base.DetachFrom(uart);
                machine = null;
            }
        }

        public override void WriteChar(byte value)
        {
            lock(internalLock)
            {
                // TODO: support for different line-end marks
                if(value == 13)
                {
                    return;
                }
                if(value != 10)
                {
                    buffer.Append((char)value);
                    CheckForPrompt();
                    return;
                }
                onLine?.Invoke(buffer.ToString(), machine.ElapsedVirtualTime.TimeElapsed);
                buffer.Clear();
                index = 0;
            }
        }

        public string GetWaitingLine()
        {
            return buffer.ToString();
        }

        public void WriteStringToTerminal(string line)
        {
            foreach(var chr in line)
            {
                CallCharReceived((byte)chr);
                WaitBeforeNextChar();
            }
        }

        public void WriteLineToTerminal(string line)
        {
            WriteStringToTerminal(line);
            CallCharReceived(13);
            WaitBeforeNextChar(); // for consistency
        }

        public string Prompt
        {
            get
            {
                return prompt;
            }

            set
            {
                lock(internalLock)
                {
                    prompt = value;
                    promptBytes = prompt == null ? null : Encoding.UTF8.GetBytes(prompt);
                    index = 0;
                    while(CheckForPrompt());
                }
            }
        }

        public TimeSpan WriteCharDelay { get; set; }

        private void WaitBeforeNextChar()
        {
            if(WriteCharDelay != TimeSpan.Zero)
            {
                Thread.Sleep(WriteCharDelay);
            }
        }

        private bool CheckForPrompt()
        {
            if(promptBytes != null && index < promptBytes.Length && buffer.Length > 0)
            {
                if(promptBytes[index] != buffer[buffer.Length - 1])
                {
                    index = promptBytes.Length + 1; // this way it will never match on this line
                    return false;
                }
                if(index == promptBytes.Length - 1 && onPrompt != null)
                {
                    onPrompt(machine.ElapsedVirtualTime.TimeElapsed);
                }
                index++;
                return true;
            }
            return false;
        }

        private readonly StringBuilder buffer;
        private readonly Action<string, TimeInterval> onLine;
        private readonly Action<TimeInterval> onPrompt;
        private byte[] promptBytes;
        private string prompt;
        private int index;
        private Machine machine;
        private object internalLock;
    }
}
