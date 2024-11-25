//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class RenesasRZG_MHU : IDoubleWordPeripheral, INumberedGPIOOutput, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasRZG_MHU()
        {
            messageInterruptsNonSecure = new GPIO[ChannelCount];
            responseInterruptsNonSecure = new GPIO[ChannelCount];
            messageInterruptsSecure = new GPIO[ChannelCount];
            responseInterruptsSecure = new GPIO[ChannelCount];

            RegistersCollection = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            Connections = new ReadOnlyDictionary<int, IGPIO>(
                messageInterruptsSecure
                    .Concat(responseInterruptsSecure)
                    .Concat(messageInterruptsNonSecure)
                    .Concat(responseInterruptsNonSecure)
                    .Select((x, i) => new { Key = i, Value = (IGPIO)x })
                    .ToDictionary(x => x.Key, x => x.Value)
            );
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            foreach(var conn in Connections)
            {
                conn.Value.Unset();
            }
            foreach(var irq in softwareInterrupts)
            {
                irq.Unset();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size => 0x1800;
        public DoubleWordRegisterCollection RegistersCollection { get; }
        public IReadOnlyDictionary<int, IGPIO> Connections { get; }
        public GPIO SoftwareIRQ0 => softwareInterrupts[0];
        public GPIO SoftwareIRQ1 => softwareInterrupts[1];
        public GPIO SoftwareIRQ2 => softwareInterrupts[2];
        public GPIO SoftwareIRQ3 => softwareInterrupts[3];

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registerMap = BuildChannelRegisterMap(0x0, false)
                .Concat(BuildChannelRegisterMap(0x1000, true))
                .ToDictionary(x => x.Key, x => x.Value);

            for(long i = 0; i < softwareInterrupts.Length; ++i)
            {
                long offset = 0x10 * i;
                BuildInterruptRegisters(registerMap, softwareInterrupts[i], offset + (long)Registers.SoftwareInterruptStatus);
            }

            return registerMap;
        }

        private Dictionary<long, DoubleWordRegister> BuildChannelRegisterMap(long baseOffset, bool secure)
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>();

            var messageIrqs = secure ? messageInterruptsSecure : messageInterruptsNonSecure;
            var responseIrqs = secure ? responseInterruptsSecure : responseInterruptsNonSecure;

            for(long i = 0; i < ChannelCount; ++i)
            {
                long offset = baseOffset + 0x20 * i;
                var msgIrq = new GPIO();
                var rspIrq = new GPIO();

                BuildInterruptRegisters(registerMap, msgIrq, offset + (long)Registers.MessageInterruptStatus);
                BuildInterruptRegisters(registerMap, rspIrq, offset + (long)Registers.ResponseInterruptStatus);

                messageIrqs[i] = msgIrq;
                responseIrqs[i] = rspIrq;
            }

            return registerMap;
        }

        private void BuildInterruptRegisters(Dictionary<long, DoubleWordRegister> registerMap, GPIO irq, long baseOffset)
        {
            registerMap.Add(baseOffset + 0x0, new DoubleWordRegister(this)
                .WithFlag(0, FieldMode.Read, name: "STAT",
                    valueProviderCallback: _ => irq.IsSet
                )
                .WithReservedBits(1, 31)
            );
            registerMap.Add(baseOffset + 0x4, new DoubleWordRegister(this)
                .WithFlag(0, FieldMode.Write, name: "SET",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            irq.Set();
                        }
                    }
                )
                .WithReservedBits(1, 31)
            );
            registerMap.Add(baseOffset + 0x8, new DoubleWordRegister(this)
                .WithFlag(0, FieldMode.Write, name: "CLEAR",
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            irq.Unset();
                        }
                    }
                )
                .WithReservedBits(1, 31)
            );
        }

        private readonly GPIO[] softwareInterrupts = {new GPIO(), new GPIO(), new GPIO(), new GPIO()};
        private readonly GPIO[] messageInterruptsNonSecure;
        private readonly GPIO[] responseInterruptsNonSecure;
        private readonly GPIO[] messageInterruptsSecure;
        private readonly GPIO[] responseInterruptsSecure;

        private const long ChannelCount = 6;

        public enum Registers : long
        {
            MessageInterruptStatus  = 0x000, // MSG_INT_STSn
            MessageInterruptSet     = 0x004, // MSG_INT_SETn
            MessageInterruptClear   = 0x008, // MSG_INT_CLRn
            ResponseInterruptStatus = 0x010, // RSP_INT_STSn
            ResponseInterruptSet    = 0x014, // RSP_INT_SETn
            ResponseInterruptClear  = 0x018, // RSP_INT_CLRn
            SoftwareInterruptStatus = 0x800, // SW_INT_STSn
            SoftwareInterruptSet    = 0x804, // SW_INT_SETn
            SoftwareInterruptClear  = 0x808, // SW_INT_CLRn
        }
    }
}
