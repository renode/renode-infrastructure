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

namespace Antmicro.Renode.Backends.Terminals
{
    [Transient]
    public sealed class PromptTerminal : BackendTerminal
    {
        public PromptTerminal(Action<string, TimeSpan> onLine = null, Action<TimeSpan> onPrompt = null, string prompt = null)
        {
            if(!string.IsNullOrEmpty(prompt))
            {
                SetPrompt(prompt);
            }
            internalLock = new object();
            charBuffer = new List<char>();
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
                    charBuffer.Add((char)value);
                    if(promptBytes != null && index < promptBytes.Length)
                    {
                        if(promptBytes[index] != value)
                        {
                            index = promptBytes.Length + 1; // this way it will never match on this line
                        }
                        if(index == promptBytes.Length - 1 && onPrompt != null)
                        {
                            onPrompt(machine.ElapsedVirtualTime);
                        }
                        index++;
                    }
                    return;
                }
                // TODO: little optimization here
                if(onLine != null)
                {
                    onLine(new String(charBuffer.ToArray()), machine.ElapsedVirtualTime);
                }
                charBuffer.Clear();
                index = 0;
            }
        }

        public string GetWaitingLine()
        {
            return new string(charBuffer.ToArray());
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

        public void SetPrompt(string prompt)
        {
            promptBytes = prompt == null ? null : prompt.ToCharArray().Select(x => (byte)x).ToArray();
        }

        public TimeSpan WriteCharDelay { get; set; }

        private void WaitBeforeNextChar()
        {
            if(WriteCharDelay != TimeSpan.Zero)
            {
                Thread.Sleep(WriteCharDelay);
            }
        }

        private readonly List<char> charBuffer;
        private readonly Action<string, TimeSpan> onLine;
        private readonly Action<TimeSpan> onPrompt;
        private byte[] promptBytes;
        private int index;
        private Machine machine;
        private object internalLock;
    }
}
