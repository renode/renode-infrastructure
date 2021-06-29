//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.IO;
using System.Text;
using WebAssembly;
using WebAssembly.Runtime;
using Antmicro.Migrant;
using System.Runtime.CompilerServices;

namespace Antmicro.Renode.Peripherals.UART
{
    public class RustUART : IUART, IDoubleWordPeripheral, IKnownSize
    {
        public RustUART()
        {
            IRQ = new GPIO();

        }
        public uint ReadDoubleWord(long offset)
        {
            if(exports == null)
            {
                this.Log(LogLevel.Error, "Trying to access unitialized RustUART. Set the Path first.");
                return 0;
            }
            return (uint) exports.ReadDoubleWord(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(exports == null)
            {
                this.Log(LogLevel.Error, "Trying to access unitialized RustUART. Set the Path first.");
                return;
            }
            exports.WriteDoubleWord(offset, (int) value);
        }

        public void WriteChar(byte value)
        {
            if(exports == null)
            {
                this.Log(LogLevel.Error, "Trying to access unitialized RustUART. Set the Path first.");
                return;
            }
            exports.WriteChar( (int) value);
        }

        public void Reset()
        {
            if(exports == null)
            {
                this.Log(LogLevel.Error, "Trying to access unitialized RustUART. Set the Path first.");
                return;
            }
            exports.Reset();
        }

        public string Path
        {
            get => path;
            set => Init(value);
        }

        [field: Transient]
        public event Action<byte> CharReceived;
        public long Size => 0x100;
        public GPIO IRQ { get; private set; }
        public Bits StopBits => Bits.One;
        public Parity ParityBit => Parity.None;
        public uint BaudRate => 9600;

        private void Init(string path)
        {
            var imports = new ImportDictionary
            {
                { "uart", "SetIRQ", new FunctionImport(new Action<int>(value => IRQ.Set(Convert.ToBoolean(value)))) },
                { "uart", "InvokeCharReceived", new FunctionImport(new Action<int>(character => CharReceived?.Invoke( (byte) character))) },
            };
            wasmExports = Compile.FromBinary<WASMExports>(new FileStream(path, FileMode.Open, FileAccess.Read))(imports);
            exports = wasmExports.Exports;

            Reset();
            this.path = path;
            this.Log(LogLevel.Info, "Initialized the peripheral to {0}", path);
        }

        private Instance<WASMExports> wasmExports;
        private WASMExports exports;
        private string path;
    }

    public abstract class WASMExports {
        public abstract void Reset();
        public abstract void WriteChar(int value);
        public abstract int ReadDoubleWord(long offset);
        public abstract void WriteDoubleWord(long offset, int value);
    }
}
