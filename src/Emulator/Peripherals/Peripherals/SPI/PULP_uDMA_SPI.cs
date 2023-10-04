//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class PULP_uDMA_SPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        public PULP_uDMA_SPI(IMachine machine) : base(machine)
        {
            sysbus = machine.GetSystemBus(this);
            RxIRQ = new GPIO();
            TxIRQ = new GPIO();
            CmdIRQ = new GPIO();

            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.RxTransferAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out rxTransferAddress, name: "SPIM_RX_SADDR")
                },

                {(long)Registers.RxTransferBufferSize, new DoubleWordRegister(this)
                    .WithValueField(0, 20, out rxTransferBufferSize, name: "SPIM_RX_SIZE")
                    .WithReservedBits(20, 12)
                },

                {(long)Registers.RxTransferConfiguration, new DoubleWordRegister(this)
                    .WithTag("CONTINOUS", 0, 1)
                    .WithTag("DATASIZE", 1, 2)
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out rxEnable, name: "EN")
                    .WithTag("CLR/PENDING", 5, 1)
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => TryStartReception())
                },

                {(long)Registers.TxTransferAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out txTransferAddress, name: "SPIM_TX_SADDR")
                },

                {(long)Registers.TxTransferBufferSize, new DoubleWordRegister(this)
                    .WithValueField(0, 20, out txTransferBufferSize, name: "SPIM_TX_SIZE")
                    .WithReservedBits(20, 12)
                },

                {(long)Registers.TxTransferConfiguration, new DoubleWordRegister(this)
                    .WithTag("CONTINOUS", 0, 1)
                    .WithTag("DATASIZE", 1, 2)
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out txEnable, name: "EN")
                    .WithTag("CLR/PENDING", 5, 1)
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => TryStartTransmission())
                },

                {(long)Registers.CommandTransferAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out commandTransferAddress, name: "SPIM_CMD_SADDR")
                },

                {(long)Registers.CommandTransferBufferSize, new DoubleWordRegister(this)
                    .WithTag("CMD_SIZE", 0, 20)
                    .WithReservedBits(20, 12)
                },

                {(long)Registers.CommandTransferConfiguration, new DoubleWordRegister(this)
                    .WithTag("CONTINOUS", 0, 1)
                    .WithTag("DATASIZE", 1, 2)
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out commandEnable, name: "EN")
                    .WithTag("CLR/PENDING", 5, 1)
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) => TryExecuteTransaction())
                },
            };

            registersCollection = new DoubleWordRegisterCollection(this, registers);
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            registersCollection.Reset();
            command = Commands.None;
            // Updating interrupts here is not necessary as we blink with them
        }

        public long Size => 0x80;

        public GPIO RxIRQ { get; }
        public GPIO TxIRQ { get; }
        public GPIO CmdIRQ { get; }

        private void TryExecuteTransaction()
        {
            if(command != Commands.None)
            {
                this.Log(LogLevel.Debug, "Tried to process a new command before finishing the previous one.");
                return;
            }
            if(!commandEnable.Value)
            {
                this.Log(LogLevel.Debug, "Tried to issue a transaction without full configuration.");
                return;
            }
            command = ReadCommand();
            switch(command)
            {
                case Commands.ReceiveData:
                    TryStartReception();
                    break;
                case Commands.TransferData:
                    TryStartTransmission();
                    break;
                default:
                    this.Log(LogLevel.Error, "Encountered unsupported command: 0x{0:X}", command);
                    command = Commands.None;
                    break;
            }
            CmdIRQ.Blink();
        }

        private void TryStartReception()
        {
            if(!rxEnable.Value || command != Commands.ReceiveData)
            {
                this.Log(LogLevel.Debug, "Tried to issue a transaction without full configuration.");
                return;
            }
            if(RegisteredPeripheral == null)
            {
                this.Log(LogLevel.Warning, "Trying to issue a transaction to a slave peripheral, but nothing is connected");
                return;
            }
            var receiveQueue = new Queue<byte>();
            for(int i = 0; i < (int)rxTransferBufferSize.Value; i++)
            {
                receiveQueue.Enqueue(RegisteredPeripheral.Transmit(0));
            }
            RegisteredPeripheral.FinishTransmission();
            sysbus.WriteBytes(receiveQueue.ToArray(), rxTransferAddress.Value);
            receiveQueue.Clear();
            command = Commands.None;
            RxIRQ.Blink();
        }

        private void TryStartTransmission()
        {            
            if(!txEnable.Value || command != Commands.TransferData)
            {
                this.Log(LogLevel.Debug, "Tried to issue a transaction without full configuration.");
                return;
            }
            if(RegisteredPeripheral == null)
            {
                this.Log(LogLevel.Warning, "Trying to issue a transaction to a slave peripheral, but nothing is connected");
                return;
            }
            foreach(var b in sysbus.ReadBytes(txTransferAddress.Value, (int)txTransferBufferSize.Value))
            {
                RegisteredPeripheral.Transmit(b);
            }
            RegisteredPeripheral.FinishTransmission();
            command = Commands.None;
            TxIRQ.Blink();
        }

        private Commands ReadCommand()
        {
            // The command is the third element of an array stored in memory, hence the addition
            var cmd = (Commands)(sysbus.ReadDoubleWord(commandTransferAddress.Value + 8) >> 28);
            if(!Enum.IsDefined(typeof(Commands), cmd))
            {
                this.Log(LogLevel.Warning, "Invalid command has been issued: {0}", cmd);
            }
            return cmd;
        }

        private Commands command;

        private readonly IBusController sysbus;
        private readonly DoubleWordRegisterCollection registersCollection;
        private readonly IFlagRegisterField commandEnable;
        private readonly IFlagRegisterField rxEnable;
        private readonly IFlagRegisterField txEnable;
        private readonly IValueRegisterField rxTransferAddress;
        private readonly IValueRegisterField rxTransferBufferSize;
        private readonly IValueRegisterField txTransferAddress;
        private readonly IValueRegisterField txTransferBufferSize;
        private readonly IValueRegisterField commandTransferAddress;

        private enum Commands : byte
        {
            Config = 0x0,
            SetChipSelect = 0x1,
            SendCommand = 0x2,
            // no 0x3 command
            ReceiveDummyBits = 0x4,
            Wait = 0x5,
            TransferData = 0x6,
            ReceiveData = 0x7,
            RepeatCommand = 0x8,
            ClearChipSelect = 0x9,
            RepeatCommandEnd = 0xA,
            CheckAgainstExpectedValue = 0xB,
            FullDuplex = 0xC,
            SetAddressForDMA = 0xD,
            SetSizeAndStartDMA = 0xE,
            // not a standard command
            None = 0xF
        }

        private enum Registers
        {
            RxTransferAddress = 0x0,
            RxTransferBufferSize = 0x4,
            RxTransferConfiguration = 0x8,
            RxInitConfig = 0xC,
            TxTransferAddress = 0x10,
            TxTransferBufferSize = 0x14,
            TxTransferConfiguration = 0x18,
            TxInitConfig = 0x1C,
            CommandTransferAddress = 0x20,
            CommandTransferBufferSize = 0x24,
            CommandTransferConfiguration = 0x28,
            CommandInitConfig = 0x2C
        }
    }
}
