//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class Renesas_GPIO : BaseGPIOPort, IBytePeripheral, IWordPeripheral, IKnownSize
    {
        public Renesas_GPIO(Machine machine) : base(machine, NumberOfPorts * NumberOfPinsPerPort)
        {
            portMode = new IEnumRegisterField<Mode>[NumberOfPorts][];

            DefineRegisters();
        }

        public byte ReadByte(long offset)
        {
            return byteRegisters.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            byteRegisters.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            return wordRegisters.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            wordRegisters.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            byteRegisters.Reset();
            wordRegisters.Reset();
        }

        public long Size => 0x10000;

        private void DefineRegisters()
        {
            var byteRegistersMap = new Dictionary <long, ByteRegister>();
            var wordRegistersMap = new Dictionary <long, WordRegister>();

            for(int i = 0; i < NumberOfPorts; i++)
            {
                byteRegistersMap[(long)Registers.Port + i] = new ByteRegister(this)
                    .WithEnumFields<ByteRegister, byte>(0, 1, NumberOfPinsPerPort, name: "Pm",
                        valueProviderCallback: CreatePortRegisterValueProviderCallback(i),
                        writeCallback: CreatePortRegisterWriteCallback(i)
                    );

                // these registers are necessary to allow software read back the previously written value
                byteRegistersMap[(long)Registers.PortModeControl + i] = new ByteRegister(this)
                    .WithEnumFields<ByteRegister, byte>(0, 1, NumberOfPinsPerPort, name: "PMCm");
                byteRegistersMap[(long)Registers.PortRegionSelect + i] = new ByteRegister(this)
                    .WithEnumFields<ByteRegister, byte>(0, 1, NumberOfPinsPerPort, name: "RSELPm");

                wordRegistersMap[(long)Registers.PortMode + 0x2 * i] = new WordRegister(this)
                    .WithEnumFields<WordRegister, Mode>(0, 2, NumberOfPinsPerPort, out portMode[i], name: "PMm",
                        writeCallback: CreatePortModeRegisterWriteCallback(i)
                    );
            }

            byteRegisters = new ByteRegisterCollection(this, byteRegistersMap);
            wordRegisters = new WordRegisterCollection(this, wordRegistersMap);
        }

        private Func<int, byte, byte> CreatePortRegisterValueProviderCallback(int port)
        {
            return (idx, _) => Connections[port * NumberOfPinsPerPort + idx].IsSet ? (byte)1 : (byte)0;
        }

        private Action<int, byte, byte> CreatePortRegisterWriteCallback(int port)
        {
            return (idx, _, value) => Connections[port * NumberOfPinsPerPort + idx].Set(value == (byte)1);
        }

        private Action<int, Mode, Mode> CreatePortModeRegisterWriteCallback(int port)
        {
            return (idx, oldValue, newValue) =>
            {
                if(newValue != Mode.Output)
                {
                    this.Log(LogLevel.Warning, "{0:X} - port mode not supported, keeping the previous value: {0:X}", oldValue);
                    portMode[port][idx].Value = oldValue;
                }
            };
        }

        private const int NumberOfPorts = 25;
        private const int NumberOfPinsPerPort = 8;

        private readonly IEnumRegisterField<Mode>[][] portMode;

        private ByteRegisterCollection byteRegisters;
        private WordRegisterCollection wordRegisters;

        private enum Mode
        {
            HiZ = 0x0,
            Input = 0x1,
            Output = 0x2,
            OutputInputBuffer = 0x3,
        }

        private enum Registers
        {
            Port = 0x0,
            PortMode = 0x200,
            PortModeControl = 0x400,
            PortRegionSelect = 0xc00,
        }
    }
}
