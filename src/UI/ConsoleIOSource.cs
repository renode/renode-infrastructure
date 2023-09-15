//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using AntShell.Terminal;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UI
{
    public class ConsoleIOSource : IActiveIOSource
    {
        public ConsoleIOSource()
        {
            isInputRedirected = Console.IsInputRedirected;
            if(!isInputRedirected)
            {
                Console.TreatControlCAsInput = true;
            }
            
            checker = new UTF8Checker();
            
            var inputHandler = new System.Threading.Thread(HandleInput)
            {
                IsBackground = true,
                Name = "Console IO handler thread"
            };

            inputHandler.Start();
        } 

        public void Dispose()
        {
        }

        public void Flush()
        {
        }

        public void Pause()
        {
            // Required by IActiveIOSource interface
        }

        public void Resume()
        {
            // Required by IActiveIOSource interface
        }

        public void Write(byte b)
        {
            if(checker.TryDecode(b, out var c))
            {
                try
                {
                    Console.Write(c);
                }
                catch(ArgumentOutOfRangeException)
                {
                    // this sometimes happens in System.TermInfoDriver.SetCursorPosition
                    // no idea why, but there is not much we can do
                    ;
                }
            }
        }

        public bool IsAnythingAttached => (ByteRead != null);
        
        public event Action<int> ByteRead;

        private void HandleInput()
        {
            if(isInputRedirected)
            {
                RedirectedHandling();
            }
            else
            {
                StandardHandling();
            }
        }

        private void RedirectedHandling()
        {
            // For cases in which input has been redirected from a file
            while(true)
            {
                ByteRead?.Invoke(Console.Read());
            }
        }

        private void StandardHandling()
        {
            var mappings = new Dictionary<ConsoleKey, byte[]>()
            {
                { ConsoleKey.Enter,      new [] { (byte)'\n' } },
                { ConsoleKey.UpArrow,    new [] { ESCCode, CSICode, (byte)'A' } },
                { ConsoleKey.DownArrow,  new [] { ESCCode, CSICode, (byte)'B' } },
                { ConsoleKey.RightArrow, new [] { ESCCode, CSICode, (byte)'C' } },
                { ConsoleKey.LeftArrow,  new [] { ESCCode, CSICode, (byte)'D' } },
                { ConsoleKey.Delete,     new [] { ESCCode, CSICode, (byte)'3', (byte)'~' } },
                { ConsoleKey.Home,       new [] { ESCCode, CSICode, (byte)'H' } },
                { ConsoleKey.End,        new [] { ESCCode, CSICode, (byte)'F' } },
                { ConsoleKey.Tab,        new [] { (byte)'\t' } },
                { ConsoleKey.Backspace,  new [] { (byte)127 } }
            };

            while(true)
            {
                var key = Console.ReadKey(true);
                if(key.Key == (ConsoleKey)0 && (key.Modifiers & ConsoleModifiers.Alt) != 0)
                {
                    ByteRead?.Invoke(ESCCode);
                    ByteRead?.Invoke(CSICode);
                    continue;
                }

                // It seems that Mono inserts Vt100 escape sequences, but this is not a case in .NET
                // Add handling for CtrlLeftArrow and CtrlRightArrow
                if(key.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    if(key.Key == ConsoleKey.RightArrow || key.Key == ConsoleKey.LeftArrow)
                    {
                        // Invoke correct Vt100 sequence for AntShell
                        ByteRead?.Invoke(ESCCode);
                        ByteRead?.Invoke(CSICode);
                        ByteRead?.Invoke('1');
                        ByteRead?.Invoke(';');
                        ByteRead?.Invoke('5');

                        if(key.Key == ConsoleKey.LeftArrow)
                        {
                            ByteRead?.Invoke('D');
                        }
                        else if(key.Key == ConsoleKey.RightArrow)
                        {
                            ByteRead?.Invoke('C');
                        }
                    }
                }

                if(mappings.TryGetValue(key.Key, out var sequence))
                {
                    foreach(var b in sequence)
                    {
                        ByteRead?.Invoke(b);
                    }
                }
                else
                {
                    foreach(var b in checker.UTF8Encoder.GetBytes(new [] { key.KeyChar }))
                    {
                        ByteRead?.Invoke(b);
                    }
                }
            }
        }

        private readonly UTF8Checker checker;
        private readonly bool isInputRedirected;

        private const byte ESCCode = 0x1B;
        private const byte CSICode = 0x5B;

        private class UTF8Checker
        {
            public bool TryDecode(byte b, out char c)
            {
                if(state == State.Idle)
                {
                    if(b <= 0x7F)
                    {
                        c = (char)b;
                        return true;
                    }
                    else 
                    {
                        buffer.Enqueue(b);
                        if((b & 0xE0) == 0xC0)
                        {
                            state = State.Need1;
                        }
                        else if((b & 0xF0) == 0xE0)
                        {
                            state = State.Need2;
                        }
                        else if((b & 0xF8) == 0xF0)
                        {
                            state = State.Need3;
                        }
                        else if((b & 0xFC) == 0xF8)
                        {
                            state = State.Need4;
                        }
                        else
                        {
                            state = State.Need5;
                        }
                    }
                }
                else
                {
                    buffer.Enqueue(b);
                    state = state - 1;

                    if(state == State.Idle)
                    {
                        c = encoder.GetString(buffer.DequeueAll())[0];
                        return true;
                    }
                }

                c = default(char);
                return false;
            }

            public System.Text.UTF8Encoding UTF8Encoder => encoder;

            private State state;
            private readonly Queue<byte> buffer = new Queue<byte>();
            private readonly System.Text.UTF8Encoding encoder = new System.Text.UTF8Encoding();

            private enum State
            {
                Idle = 0,
                Need1 = 1,
                Need2 = 2,
                Need3 = 3,
                Need4 = 4,
                Need5 = 5,
            }
        }
    }
}
