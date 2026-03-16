//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;

namespace Antmicro.Renode.Logging
{
    public class ConsoleBackend : FormattedTextBackend
    {
        static ConsoleBackend()
        {
            Instance = new ConsoleBackend();
        }

        public static ConsoleBackend Instance { get; private set; }

        public override void Dispose()
        {
        }

        public override void Flush()
        {
            output.Flush();
        }

        public string WindowTitle
        {
            // The setter is supported on all the platforms we target,
            // so it doesn't need the if clause.
            set
            {
                if(!PlainMode)
                {
                    Console.Title = value;
                }
            }
        }

        // do not generate color output
        // do not set window title
        // minimize the use of VT100 codes
        public override bool PlainMode
        {
            get => plainMode || output != Console.Out || isRedirected;
            set => plainMode = value;
        }

        protected override void SetColor(ConsoleColor color)
        {
            Console.ForegroundColor = color;
        }

        protected override void ResetColor()
        {
            Console.ResetColor();
        }

        protected override void WriteLine(string line)
        {
            output.WriteLine(line);
        }

        private ConsoleBackend()
        {
            isRedirected = Console.IsOutputRedirected;
        }

        private bool plainMode;

        private readonly TextWriter output = Console.Out;
        private readonly bool isRedirected;
    }
}