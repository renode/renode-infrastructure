//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class Emios : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize
    {
        public Emios(IMachine machine) : base(machine, 2 * NumberOfChannels)
        {
            controlRegisters = new DoubleWordRegister[NumberOfChannels];
            statusRegisters = new DoubleWordRegister[NumberOfChannels];
            channelModes = new IEnumRegisterField<ChannelMode>[NumberOfChannels];
            channelFlags = new IFlagRegisterField[NumberOfChannels];
            inputState = new IFlagRegisterField[NumberOfChannels];
            interruptEnabled = new IFlagRegisterField[NumberOfChannels];

            for(var i = 0; i < controlRegisters.Length; i++)
            {
                var j = i;
                controlRegisters[i] = new DoubleWordRegister(this);
                controlRegisters[i].DefineFlagField(7, writeCallback: 
					(oldValue, newValue) =>
                {
                    if(channelModes[j].Value == ChannelMode.Output)
                    {
                        Connections[j].Set(newValue);
                    }
                }, name: "Edge polarity");
                channelModes[i] = controlRegisters[i].DefineEnumField<ChannelMode>(0, 7);
                interruptEnabled[i] = controlRegisters[i].DefineFlagField(17);

                statusRegisters[i] = new DoubleWordRegister(this);
                channelFlags[i] = statusRegisters[i].DefineFlagField(0, FieldMode.WriteOneToClear, 
                    writeCallback: (oldValue, newValue) =>
                    {
                        if(newValue)
                        {
                            Connections[NumberOfChannels + j].Unset();
                        }
                    });
                inputState[i] = statusRegisters[i].DefineFlagField(2, FieldMode.Read);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            int channelNo;
            ChannelRegister channelRegister;
            if(TryGetChannelRegister(offset, out channelNo, out channelRegister))
            {
                return ReadChannelRegister(channelNo, channelRegister);
            }
            switch((GlobalRegister)offset)
            {
            case GlobalRegister.GlobalFlag:
                return GetGlobalFlagRegister();
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            int channelNo;
            ChannelRegister channelRegister;
            if(TryGetChannelRegister(offset, out channelNo, out channelRegister))
            {
                WriteChannelRegister(offset, channelNo, channelRegister, value);
                return;
            }
            this.LogUnhandledRead(offset);
        }

        public long Size
        {
            get
            {
                return UnifiedChannelRegistersEnd + UnifiedChannelSize;
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            base.OnGPIO(number, value);
            if(number >= NumberOfChannels)
            {
                this.Log(LogLevel.Warning, "Input interrupt {0}, higher than channel number {1}, ignoring.", number, NumberOfChannels);
                return;
            }
            if(value)
            {
                channelFlags[number].Value = true;
                if(interruptEnabled[number].Value)
                {
                    Connections[NumberOfChannels + number].Set();
                }
            }
            inputState[number].Value = value;
        }

        private uint GetGlobalFlagRegister()
        {
            var value = 0u;
            for(var i = 0; i < channelFlags.Length; i++)
            {
                if(channelFlags[i].Value)
                {
                    value |= (1u << i);
                }
            }
            return value;
        }

        private uint ReadChannelRegister(int channelNo, ChannelRegister register)
        {
            switch(register)
            {
            case ChannelRegister.Control:
                return controlRegisters[channelNo].Read();
            case ChannelRegister.Status:
                return statusRegisters[channelNo].Read();
            default:
                this.Log(LogLevel.Warning, "Unhandled read on channel register {0}, channel no {1}.", register, channelNo);
                return 0;
            }
        }

        private void WriteChannelRegister(long offset, int channelNo, ChannelRegister register, uint value)
        {
            switch(register)
            {
            case ChannelRegister.Control:
                controlRegisters[channelNo].Write(offset, value);
                break;
            case ChannelRegister.Status:
                statusRegisters[channelNo].Write(offset, value);
                break;
            default:
                this.Log(LogLevel.Warning, "Unhandled write on channel register {0}, channel no {1}, value 0x{2:X}",
                    register, channelNo, value);
                break;
            }
        }

        private static bool TryGetChannelRegister(long offset, out int channelNo, out ChannelRegister channelRegister)
        {
            if(offset < UnifiedChannelRegistersStart || offset > UnifiedChannelRegistersEnd)
            {
                channelRegister = 0;
                channelNo = 0;
                return false;
            }
            channelNo = (int)((offset - UnifiedChannelRegistersStart) / UnifiedChannelSize);
            channelRegister = (ChannelRegister)((offset - UnifiedChannelRegistersStart) % UnifiedChannelSize);
            return true;
        }

        public override void Reset()
        {
            base.Reset();
            Array.ForEach(controlRegisters, x => x.Reset());
            Array.ForEach(statusRegisters, x => x.Reset());
        }

        private readonly DoubleWordRegister[] controlRegisters;
        private readonly DoubleWordRegister[] statusRegisters;
        private readonly IFlagRegisterField[] channelFlags;
        private readonly IFlagRegisterField[] inputState;
        private readonly IFlagRegisterField[] interruptEnabled;
        private readonly IEnumRegisterField<ChannelMode>[] channelModes;

        private enum GlobalRegister
        {
            GlobalFlag = 4
        }

        private enum ChannelRegister
        {
            AData = 0,
            BData = 0x4,
            Counter = 0x8,
            Control = 0xC,
            Status = 0x10,
            AlternateAddress = 0x14
        }

        private enum ChannelMode
        {
            Input = 0,
            Output = 1
        }

        private const int UnifiedChannelRegistersStart = 0x20;
        private const int UnifiedChannelRegistersEnd = 0x300;
        private const int UnifiedChannelSize = 32;
        private const int NumberOfChannels = (UnifiedChannelRegistersEnd - UnifiedChannelRegistersStart) / UnifiedChannelSize;
    }
}

