//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class AndesATCGPIO100 : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public AndesATCGPIO100(IMachine machine) : base(machine, NumberOfPorts)
        {
            IRQ = new GPIO();
            irqManager = new GPIOInterruptManager(IRQ, State);
            outputData = new IFlagRegisterField[NumberOfPorts];
            RegistersCollection = new DoubleWordRegisterCollection(this);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            irqManager.Reset();
            RegistersCollection.Reset();
            ResetDirection();
        }

        public override void OnGPIO(int number, bool value)
        {
            if((irqManager.PinDirection[number] & GPIOInterruptManager.Direction.Input) == 0)
            {
                this.Log(LogLevel.Warning, "Attempting to set an output GPIO pin #{0}", number);
                return;
            }

            base.OnGPIO(number, value);
            irqManager.RefreshInterrupts();
        }

        public uint ReadDoubleWord(long offset) => RegistersCollection.Read(offset);

        public void WriteDoubleWord(long offset, uint value) => RegistersCollection.Write(offset, value);

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void UpdateIOLine(int number)
        {
            this.Log(LogLevel.Debug, "Setting connection on pin {0} to {1}", number, outputData[number].Value);
            Connections[number].Set(outputData[number].Value);
        }

        private void UpdateInterruptMode(int channel, ulong raw)
        {
            var mappedIrqMode = (InterruptModeMapping)raw;
            if(mappedIrqMode == InterruptModeMapping.NoOperation)
            {
                irqManager.InterruptEnable[channel] = false;
                return;
            }

            GPIOInterruptManager.InterruptTrigger selectedirqmode;

            switch(mappedIrqMode)
            {
            case InterruptModeMapping.HighLevel:
                selectedirqmode = GPIOInterruptManager.InterruptTrigger.ActiveHigh;
                break;

            case InterruptModeMapping.LowLevel:
                selectedirqmode = GPIOInterruptManager.InterruptTrigger.ActiveLow;
                break;

            case InterruptModeMapping.NegativeEdge:
                selectedirqmode = GPIOInterruptManager.InterruptTrigger.FallingEdge;
                break;

            case InterruptModeMapping.PositiveEdge:
                selectedirqmode = GPIOInterruptManager.InterruptTrigger.RisingEdge;
                break;

            case InterruptModeMapping.DualEdge:
                selectedirqmode = GPIOInterruptManager.InterruptTrigger.BothEdges;
                break;

            default:
                this.Log(LogLevel.Warning, "Channel {0}: invalid interrupt mode value {1}", channel, raw);
                return;
            }

            irqManager.InterruptEnable[channel] = true;
            this.Log(LogLevel.Debug, "Channel {0}: set: {1}", channel, selectedirqmode);
            irqManager.InterruptType[channel] = selectedirqmode;
        }

        private void ResetDirection()
        {
            for(var i = 0; i < NumberOfPorts; i++)
            {
                irqManager.PinDirection[i] = GPIOInterruptManager.Direction.Input;
            }
        }

        private void DefineRegisters()
        {
            Registers.IdRev.Define(this, 0x2031000)
                .WithTag("RevMinor", 0, 4)
                .WithTag("RevMajor", 4, 4)
                .WithTag("ID", 8, 24);

            Registers.Cfg.Define(this)
                .WithValueField(0, 4, name: "ChannelNum", valueProviderCallback: _ => NumberOfPorts)
                .WithReservedBits(5, 24)
                .WithTaggedFlag("Debounce", 29)
                .WithTaggedFlag("Intr", 30)
                .WithTaggedFlag("Pull", 31);

            Registers.DataIn.Define(this)
                .WithFlags(0, 32, FieldMode.Read, valueProviderCallback: (i, _) => State[i], name: "DataOut");

            Registers.DataOut.Define(this)
                .WithFlags(0, 32, out outputData, name: "DataOut",
                    writeCallback: (i, _, value) =>
                    {
                        outputData[i].Value = value;
                        UpdateIOLine(i);
                    }
                );

            Registers.ChannelDir.Define(this)
                .WithFlags(0, 32, name: "ChannelDir",
                    writeCallback: (i, _, value) =>
                    {
                        irqManager.PinDirection[i] = value
                            ? GPIOInterruptManager.Direction.Output
                            : GPIOInterruptManager.Direction.Input;

                        UpdateIOLine(i);
                    },
                    valueProviderCallback: (i, _) =>
                    {
                        var dir = irqManager.PinDirection[i];
                        if(dir == GPIOInterruptManager.Direction.Input)
                        {
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                );

            Registers.DoutClear.Define(this)
                .WithFlags(0, 32, FieldMode.Write, name: "DoutClear",
                    writeCallback: (i, _, value) =>
                    {
                        if(value)
                        {
                            outputData[i].Value = false;
                            UpdateIOLine(i);
                        }
                    }
                );

            Registers.DataSet.Define(this)
                .WithFlags(0, 32, FieldMode.Write, name: "DoutSet",
                    writeCallback: (i, _, value) =>
                    {
                        if(value)
                        {
                            outputData[i].Value = true;
                            UpdateIOLine(i);
                        }
                    }
                );

            Registers.PullEn.Define(this)
                .WithTag("PullEn", 0, 32);

            Registers.PullType.Define(this)
                .WithTag("PullType", 0, 32);

            Registers.IntrEn.Define(this)
                .WithFlags(0, 32, name: "IntrEn",
                    writeCallback: (i, _, value) => irqManager.InterruptEnable[i] = value,
                    valueProviderCallback: (i, _) => irqManager.InterruptEnable[i]
                );

            Registers.IntrMode0.DefineMany(this, 4, (register, idx) =>
            {
                for(var ch = 0; ch < PortsPerConfigurationRegister; ch++)
                {
                    var chLocal = ch;
                    var bit = chLocal * 4;
                    register
                        .WithValueField(bit, 3, name: $"Ch{chLocal}IntrM",
                            writeCallback: (_, val) => UpdateInterruptMode(idx * 8 + chLocal, val))
                        .WithReservedBits(bit + 3, 1);
                }
            });

            Registers.IntrStatus.Define(this)
                .WithFlags(0, 32, name: "IntrStatus", // R/W in renode but R/W1C in hardware. Handled by callbacks
                    writeCallback: (i, _, value) => irqManager.ClearInterrupt(i),
                    valueProviderCallback: (i, _) => irqManager.ActiveInterrupts.ElementAt(i)
                );

            Registers.DeBounceEn.Define(this)
                .WithTag("DeBounceEn", 0, 32);

            Registers.DeBounceCtrl.Define(this)
                .WithTag("DeBounceCtrl", 0, 32);
        }

        private IFlagRegisterField[] outputData;

        private readonly GPIOInterruptManager irqManager;
        private readonly DoubleWordRegisterCollection registers;

        private const int NumberOfPorts = 32;
        private const int PortsPerConfigurationRegister = 8;

        private enum InterruptModeMapping
        {
            NoOperation  = 0x0,
            // reserved    0x1,
            HighLevel    = 0x2,
            LowLevel     = 0x3,
            // reserved    0x4,
            NegativeEdge = 0x5,
            PositiveEdge = 0x6,
            DualEdge     = 0x7
        }

        private enum Registers : long
        {
            IdRev        = 0x00, // IDR
            Cfg          = 0x10, // CFG
            DataIn       = 0x20, // DIN
            DataOut      = 0x24, // DOUT
            ChannelDir   = 0x28, // DIR
            DoutClear    = 0x2C, // DCLR
            DataSet      = 0x30, // DSET
            PullEn       = 0x40, // PUEN
            PullType     = 0x44, // PTYP
            IntrEn       = 0x50, // INTE
            IntrMode0    = 0x54, // IMD0
            IntrMode1    = 0x58, // IMD1
            IntrMode2    = 0x5C, // IMD2
            IntrMode3    = 0x60, // IMD3
            IntrStatus   = 0x64, // ISTA
            DeBounceEn   = 0x70, // DEBE
            DeBounceCtrl = 0x74, // DEBC
        }
    }
}
