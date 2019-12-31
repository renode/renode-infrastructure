//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.USB
{
    public class Messible : BasicDoubleWordPeripheral, IKnownSize
    {
        public Messible(Machine machine) : base(machine)
        {
            buffer = new StringBuilder();

            EndOfMessagePattern = "\n";
            TrimNewline = true;

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            buffer.Clear();
        }

        public string EndOfMessagePattern { get; set; }
        public bool TrimNewline { get; set; }
        public long Size => 0x12;

        protected void DefineRegisters()
        {
            Registers.In.Define(this)
                .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, v) =>
                {
                    buffer.Append((char)v);

                    if(!buffer.EndsWith(EndOfMessagePattern, StringComparison.InvariantCulture))
                    {
                        return;
                    }

                    buffer.Remove(buffer.Length - EndOfMessagePattern.Length, EndOfMessagePattern.Length);

                    if(TrimNewline)
                    {
                        while(buffer.Length > 0
                                && (buffer[buffer.Length - 1] == '\n' || buffer[buffer.Length - 1] == '\r'))
                        {
                            buffer.Remove(buffer.Length - 1, 1);
                        }
                    }

                    this.Log(LogLevel.Info, "Message received: >>{0}<<", buffer.ToString());
                    buffer.Clear();
                })
            ;

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "FULL", valueProviderCallback: _ => false)
                .WithFlag(1, FieldMode.Read, name: "HAVE", valueProviderCallback: _ => false)
            ;
        }

        private readonly StringBuilder buffer;

        private enum Registers
        {
            In = 0x0,
            Out = 0x4,
            Status = 0x8
        }
    }
}
