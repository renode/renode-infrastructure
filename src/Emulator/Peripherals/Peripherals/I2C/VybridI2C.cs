//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;


using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public sealed class VybridI2C : SimpleContainer<II2CPeripheral>, IBytePeripheral, IKnownSize
    {
        public VybridI2C(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            Reset();
        }

        public byte ReadByte(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.AddressRegister:
                return address;
            case Registers.FrequencyDividerRegister:
                return frequencyDivider;
            case Registers.ControlRegister:
                return control;
            case Registers.StatusRegister:
                return status;
            case Registers.DataIORegister:
                if(mode == Mode.Transmit || state == State.AwaitingAddress)
                {
                    //reading not ready
                    return 0;
                }
                if(state == State.DummyRead)
                {
                    //return 0, because read mode was just enabled.
                    state = State.AwaitingData;
                    return 0;
                }
                if(receiveFifo.Count == 0)
                {
                    //current state is State.AwaitingData - perform read
                    II2CPeripheral device;
                    if(!TryGetByAddress(address, out device))
                    {
                        return 0;
                    }
                    receiveFifo = new Queue<byte>(device.Read());
                    if(receiveFifo.Count == 0)
                    {
                        this.Log(LogLevel.Warning, "Reading from slave device {0} did not return any data.", device.GetType());
                    }
                   
                }
                //Acknowledge before returning
                TransferComplete();
                return receiveFifo.Dequeue();
              
            case Registers.DebugRegister:
            case Registers.InterruptConfigRegister:
                //not used in driver
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteByte(long offset, byte value)
        {
            switch((Registers)offset)
            {
            case Registers.AddressRegister:
                address = value;
                break;
            case Registers.FrequencyDividerRegister:
                frequencyDivider = value;
                break;
            case Registers.ControlRegister:
                control = value;
                //Changing Master-Slave mode. Be warned, that slave mode is not implemented.
                if(BitHelper.IsBitSet(control, (byte)ControlBits.MasterSlaveMode))
                {
                    if(!isMaster)
                    {
                        isMaster = true;
                        BitHelper.SetBit(ref status, (byte)StatusBits.BusBusy, true);
                    }
                }
                else
                {
                    if(isMaster)
                    {
                        isMaster = false;
                        BitHelper.SetBit(ref status, (byte)StatusBits.BusBusy, false);
                        receiveFifo.Clear();
                    }
                }

                //Switching direction Tx<->Rx
                if(BitHelper.IsBitSet(control, (byte)ControlBits.TxRx))
                {
                    state = State.AwaitingAddress;
                    mode = Mode.Transmit;
                }
                else
                {
                    if(state == State.AwaitingData)
                    {
                        II2CPeripheral device;
                        if(TryGetByAddress(address, out device))
                        {
                            device.Write(transmitFifo.ToArray());
                            transmitFifo.Clear();
                        }
                    }
                    state = State.DummyRead;
                    mode = Mode.Receive;
                }
                TransferComplete();
                break;
            case Registers.StatusRegister:
                //Write 1 to clear interrupt
                if(BitHelper.IsBitSet(status, (byte)StatusBits.InterruptFlag))
                {
                    BitHelper.SetBit(ref status, (byte)StatusBits.InterruptFlag, false);
                    IRQ.Unset();
                }
                break;
            case Registers.DataIORegister:
                if(mode == Mode.Receive && state != State.AwaitingAddress)
                {
                    this.Log(LogLevel.Warning, "Writing in incorrect mode: {0} or state: {1}.", mode, state);
                }
                else if(state == State.AwaitingAddress)
                {
                    address = (byte)(value >> 1);
                    if(BitHelper.IsBitSet(value, 0))
                    {
                        //read
                        state = State.DummyRead;
                    }
                    else
                    {
                        //write
                        state = State.AwaitingData;
                    }
                }
                else if(state == State.AwaitingData)
                {
                    transmitFifo.Enqueue(value);
                    TransferComplete();
                }
                else
                {
                    this.Log(LogLevel.Warning, "Writing in incorrect mode: {0} or state: {1}.", mode, state);
                }
                break;
            case Registers.InterruptConfigRegister:
            case Registers.DebugRegister:
                //not used in driver
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }   
        }

        public override void Reset()
        {
            isMaster = true;
            state = State.AwaitingAddress;
            mode = Mode.Receive;
            address = 0;
            frequencyDivider = 0;
            control = 0;
            status = (byte)(1 << (int)StatusBits.TransferComplete);
            transmitFifo.Clear();
            receiveFifo.Clear();
        }

        public GPIO IRQ { get; private set; }

        public long Size { get { return 0x10; } }

        private void Update()
        {
            if(BitHelper.IsBitSet(control, (byte)ControlBits.InterruptEnable) && BitHelper.IsBitSet(status, (byte)StatusBits.InterruptFlag))
            {
                IRQ.Set();
            }
        }

        private void TransferComplete()
        {
            BitHelper.SetBit(ref status, (byte)StatusBits.TransferComplete, true);
            BitHelper.SetBit(ref status, (byte)StatusBits.InterruptFlag, true);
            Update();
        }

        private Queue<byte> transmitFifo = new Queue<byte>();
        private Queue<byte> receiveFifo = new Queue<byte>();

        private byte address;
        private byte frequencyDivider;
        private byte control;
        private byte status;
        private Mode mode;
        private State state;
        private bool isMaster;

        private enum Mode
        {
            Transmit,
            Receive
        }

        private enum State
        {
            AwaitingAddress,
            AwaitingData,
            DummyRead
        }

        private enum Registers
        {
            AddressRegister = 0x0,
            // IBAD
            FrequencyDividerRegister = 0x1,
            // IBFD
            ControlRegister = 0x2,
            // IBCR
            StatusRegister = 0x3,
            // IBSR
            DataIORegister = 0x4,
            // IBDR
            InterruptConfigRegister = 0x5,
            // IBIC
            DebugRegister = 0x6
            // IBDBG
        }

        private enum ControlBits : byte
        {
            InterruptEnable = 6,
            MasterSlaveMode = 5,
            TxRx = 4,
        }

        private enum  StatusBits : byte
        {
            TransferComplete = 7,
            AddressedAsASlave = 6,
            BusBusy = 5,
            ArbitrationLost = 4,
            InterruptFlag = 1

        }
    }
}