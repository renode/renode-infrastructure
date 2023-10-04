//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class PULP_uDMA_I2C : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public PULP_uDMA_I2C(IMachine machine) : base(machine)
        {
            sysbus = machine.GetSystemBus(this);
            outputBuffer = new Queue<byte>();
            RegistersCollection = new DoubleWordRegisterCollection(this);

            TxEvent = new GPIO();
            RxEvent = new GPIO();

            DefineRegisters();

            Reset();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            outputBuffer.Clear();

            repeatCounter = 1;
            dataBytesLeft = 0;
            bytesToRead = 0;

            state = State.WaitForCommand;
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public GPIO TxEvent { get; }
        public GPIO RxEvent { get; }

        private void DefineRegisters()
        {
            Registers.RxBufferBaseAddress.Define(this)
                // this is not consistent with the documentation
                // that states that only 21 bits are used for the address,
                // but otherwise the sample fails
                .WithValueField(0, 32, out rxBufferAddress, name: "RX_SADDR")
            ;

            Registers.RxBufferSize.Define(this)
                .WithValueField(0, 20, out rxBufferSize, name: "RX_SIZE")
                .WithReservedBits(20, 12)
            ;

            Registers.RxStreamConfiguration.Define(this)
                .WithTag("CONTINOUS", 0, 1)
                .WithReservedBits(1, 3)
                .WithFlag(4, out rxStreamEnabled, name: "EN")
                .WithTag("PENDING", 5, 1)
                .WithTag("CLR", 6, 1)
                .WithReservedBits(7, 25)
            ;

            Registers.TxBufferBaseAddress.Define(this)
                // this is not consistent with the documentation
                // that states that only 21 bits are used for the address,
                // but otherwise the sample fails
                .WithValueField(0, 32, out txBufferAddress, name: "TX_SADDR")
            ;

            Registers.TxBufferSize.Define(this)
                .WithValueField(0, 20, out txBufferSize, name: "TX_SIZE")
                .WithReservedBits(20, 12)
            ;

            Registers.TxStreamConfiguration.Define(this)
                .WithTag("CONTINOUS", 0, 1)
                .WithReservedBits(1, 3)
                .WithFlag(4, name: "EN",
                    valueProviderCallback: _ => false, // the transfer is instant + we don't support the continous mode, so we disable right away
                    writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }

                        this.Log(LogLevel.Debug, "Starting a DMA TX transaction");
                        var data = sysbus.ReadBytes(txBufferAddress.Value, (int)txBufferSize.Value);
#if DEBUG_PACKETS
                        this.Log(LogLevel.Noisy, "Read data from the memory @ 0x{0:X}: {1}", txBufferAddress.Value, Misc.PrettyPrintCollectionHex(data));
#endif

                        HandleIncomingBytes(data);
                        TxEvent.Blink();
                    })
                .WithFlag(5, FieldMode.Read, name: "PENDING", valueProviderCallback: _ => false)
                .WithTag("CLR", 6, 1)
                .WithReservedBits(7, 25)
            ;

            Registers.CommandBufferBaseAddress.Define(this)
                .WithValueField(0, 21, name: "CMD_SADDR")
                .WithReservedBits(21, 11)
            ;

            Registers.CommandBufferSize.Define(this)
                .WithValueField(0, 20, name: "CMD_SIZE")
                .WithReservedBits(20, 12)
            ;

            Registers.CommandStreamConfiguration.Define(this)
                .WithTag("CONTINOUS", 0, 1)
                .WithReservedBits(1, 3)
                .WithTag("EN", 4, 1)
                .WithTag("PENDING", 5, 1)
                .WithTag("CLR", 6, 1)
                .WithReservedBits(7, 25)
            ;

            Registers.Status.Define(this)
                .WithTag("BUSY", 0, 1)
                .WithTag("ARB_LOST", 1, 1)
                .WithReservedBits(2, 30)
            ;

            Registers.Setup.Define(this)
                .WithTag("DO_RST", 0, 1)
                .WithReservedBits(1, 31)
            ;
        }

        private bool HandleIncomingBytes(byte[] data)
        {
            var index = 0;
            while(index < data.Length)
            {
                switch(state)
                {
                    case State.WaitForCommand:
                    {
                        var result = HandleCommand((Command)(data[index] >> 4), data, index + 1);
                        if(result == -1)
                        {
                            // there was an error
                            return false;
                        }
                        index += result + 1; 
                    }
                    break;

                    case State.WaitForData:
                    {
                        var result = HandleData(data, index);
                        if(result == -1)
                        {
                            // there was an error
                            return false;
                        }
                        index += result; 
                    }
                    break;

                    default:
                        this.Log(LogLevel.Warning, "Received data in an unexpected state: {0}", state);
                        return false;
                }
            }

            return true;
        }

        private int HandleCommand(Command command, byte[] data, int argumentOffset)
        {
            // A command can be followed by argument bytes
            // - their actual amount is dependent on:
            //  (a) the command type, 
            //  (b) the preceeding RPT command.
            //  That's why we must dynamically return the number of bytes that were consumed in this particular execution.

            this.Log(LogLevel.Debug, "Handling command {0} (0x{0:X}) at offset {1}", command, argumentOffset - 1);

            int argumentBytesConsumed;
            switch(command)
            {
                case Command.Start:
                case Command.Stop:
                    argumentBytesConsumed = 0;
                    // it might be a repeated start
                    TrySendToDevice(finishTransmission: true, generateWarningOnEmptyBuffer: false);
                    break;

                case Command.ReadAck:
                    argumentBytesConsumed = 0;
                    bytesToRead += repeatCounter;
                    break;

                case Command.ReadNotAck:
                    argumentBytesConsumed = 0;
                    bytesToRead++;
                    TryReadFromDevice();
                    break;

                case Command.EndOfTransmission:
                    argumentBytesConsumed = 0;
                    break;

                case Command.Write:
                    argumentBytesConsumed = 0;
                    dataBytesLeft = repeatCounter;
                    state = State.WaitForData;
                    this.Log(LogLevel.Debug, "Write command detected with {0} bytes of data", repeatCounter);
                    break;

                case Command.Configure:
                    // skip the div value, we don't simulate it anyway
                    argumentBytesConsumed = 2;
                    break;

                case Command.Repeat:
                    repeatCounter = data[argumentOffset];
                    argumentBytesConsumed = 1;
                    break;

                default:
                    this.Log(LogLevel.Warning, "Unhandled command: {0} (0x{0:X})", command);
                    argumentBytesConsumed = -1;
                    break;
            }

            if(command != Command.Repeat)
            {
                // reset the repeat counter
                repeatCounter = 1;
            }

            return argumentBytesConsumed;
        }

        private int HandleData(byte[] data, int offset)
        {
            var bytesConsumed = 0;

            while(offset < data.Length && dataBytesLeft > 0)
            {
                outputBuffer.Enqueue(data[offset]);

                bytesConsumed++;
                offset++;
                dataBytesLeft--;
            }

            if(dataBytesLeft == 0)
            {
                state = State.WaitForCommand;
            }

            this.Log(LogLevel.Debug, "Handled {0} bytes of incoming data, bytes left {1}", bytesConsumed, dataBytesLeft);

            return bytesConsumed;
        }

        private bool TrySendToDevice(bool finishTransmission, bool generateWarningOnEmptyBuffer = true)
        {
            if(outputBuffer.Count == 0)
            {
                if(generateWarningOnEmptyBuffer)
                {
                    this.Log(LogLevel.Warning, "Tried to send data to the device, but there is no device address in the buffer");
                }
                return false;
            }

            var addressAndDirection = outputBuffer.Dequeue();
            var address = addressAndDirection >> 1;
            var isRead = BitHelper.IsBitSet(addressAndDirection, 0);

            if(isRead)
            {
                this.Log(LogLevel.Warning, "Read bit should not be set when sending data to the device, ignoring the transfer");
                return false;
            }

            if(outputBuffer.Count == 0)
            {
                this.Log(LogLevel.Warning, "Tried to send data to the device, but the output buffer is empty");
                return false;
            }

            if(!TryGetByAddress(address, out var device))
            {
                this.Log(LogLevel.Warning, "Tried to send data to the device 0x{0:X}, but it's not connected", address);
                return false;
            }

            var data = outputBuffer.DequeueAll();

            this.Log(LogLevel.Debug, "Sending data of size {0} to the device 0x{1:X}", data.Length, address);
            device.Write(data);

            if(finishTransmission)
            {
                device.FinishTransmission();
            }
            return true;
        }

        private bool TryReadFromDevice()
        {
            if(outputBuffer.Count == 0)
            {
                this.Log(LogLevel.Warning, "Tried to read data from the device, but there is no device address in the buffer");
                return false;
            }

            var addressAndDirection = outputBuffer.Dequeue();
            var address = addressAndDirection >> 1;
            var isRead = BitHelper.IsBitSet(addressAndDirection, 0);

            if(!isRead)
            {
                this.Log(LogLevel.Warning, "Read bit should be set when reading data from the device, ignoring the transfer");
                return false;
            }

            if(!TryGetByAddress(address, out var device))
            {
                this.Log(LogLevel.Warning, "Tried to read data from the device 0x{0:X}, but it's not connected", address);
                return false;
            }

            var data = device.Read(bytesToRead);
            if(data.Length != bytesToRead)
            {
                this.Log(LogLevel.Warning, "Tried to read {0} bytes from the device, but it returned {1} bytes", bytesToRead, data.Length);
            }

            if(!rxStreamEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Received {0} bytes from the device, but RX DMA stream is not enabled. Dropping it", data.Length);
            }
            else
            {
                if(data.Length != (int)rxBufferSize.Value)
                {
                    this.Log(LogLevel.Warning, "Received {0} bytes from the device, but RX DMA stream is configured for {1} bytes. This might indicate problems in the driver", data.Length, rxBufferSize.Value);
                }

                sysbus.WriteBytes(data, rxBufferAddress.Value);
                RxEvent.Blink();

                rxStreamEnabled.Value = false;
            }

            bytesToRead = 0;
            return true;
        }

        // holds information about
        // how many times *the next* command
        // should be repeated;
        // set by the RPT command;
        // reset to 1 after the first use
        private int repeatCounter;
        private int dataBytesLeft;
        private int bytesToRead;

        private State state;

        private IValueRegisterField txBufferAddress;
        private IValueRegisterField txBufferSize;
        private IValueRegisterField rxBufferAddress;
        private IValueRegisterField rxBufferSize;
        private IFlagRegisterField rxStreamEnabled;

        private readonly IBusController sysbus;
        private readonly Queue<byte> outputBuffer;

        private enum State
        {
            WaitForCommand,
            WaitForData
        }

        private enum Command : byte
        {
            Start = 0x0,
            WaitEvent = 0x1,
            Stop = 0x2,
            SetupStartAddress = 0x3,
            ReadAck = 0x4,
            SetupTransferSize = 0x5,
            ReadNotAck = 0x6,
            WriteByte = 0x7,
            Write = 0x8,
            EndOfTransmission = 0x9,
            Wait = 0xA,
            Repeat = 0xC,
            Configure = 0xE
        }

        private enum Registers
        {
            RxBufferBaseAddress = 0x0,
            RxBufferSize = 0x4,
            RxStreamConfiguration = 0x8,

            TxBufferBaseAddress = 0x10,
            TxBufferSize = 0x14,
            TxStreamConfiguration = 0x18,

            CommandBufferBaseAddress = 0x20,
            CommandBufferSize = 0x24,
            CommandStreamConfiguration = 0x28,

            Status = 0x30,
            Setup = 0x34
        }
    }
}
