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
    [AllowedTranslations(AllowedTranslation.ByteToWord | AllowedTranslation.WordToByte)]
    public class NPCX_SPIP : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IWordPeripheral, IKnownSize
    {
        public NPCX_SPIP(IMachine machine) : base(machine)
        {
            DefineRegisters();
            IRQ = new GPIO();
        }

        public override void Reset()
        {
            registerCollection.Reset();
            data = 0;
        }

        public ushort ReadWord(long offset)
        {
            return registerCollection.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            registerCollection.Write(offset, value);
        }

        public long Size => 0x1000;
        public GPIO IRQ { get; }

        private void DefineRegisters()
        {
            var registersMap = new Dictionary <long, WordRegister>();

            registersMap[(long)Registers.DataInOut] = new WordRegister(this)
                .WithValueField(0, 16,
                    writeCallback: (_, val) => WriteData(val),
                    valueProviderCallback: _ => ReadData(),
                    name: "DATA (SPIP Read/Write Data)")
                .WithChangeCallback((_,__) => UpdateInterrupts());

            registersMap[(long)Registers.Control1] = new WordRegister(this)
                .WithFlag(0, out isEnabled, name: "SPIEN (SPI Enable)")
                .WithReservedBits(1, 1)
                .WithFlag(2, out is16BitMode, name: "MOD (Data Interface Mode)")
                .WithReservedBits(3, 2)
                .WithFlag(5, out enableIrqRead, name: "EIR (Enable Interrupt for Read)")
                .WithFlag(6, out enableIrqWrite, name: "EIW (Enable Interrupt for Write)")
                .WithTag("SCM (Clocking Mode)", 7, 1)
                .WithTag("SCIDL (Value of SPI_SCLK when Bus is Idle)", 8, 1)
                .WithTag("SCDV6-0 (Shift Clock Divider Value)", 9, 7)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            registersMap[(long)Registers.Status] = new WordRegister(this)
                .WithTag("BSY (Shift Register Busy)", 0, 1)
                .WithFlag(1, out readBufferFull, name: "RBF (Read Buffer Full)")
                .WithReservedBits(2, 14);

            registerCollection = new WordRegisterCollection(this, registersMap);
        }

        private void WriteData(ulong val)
        {
            if(!isEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Tried to write data while peripheral is not enabled: 0x{0:X} Aborting!", val);
                return;
            }
            data = RegisteredPeripheral.Transmit((byte)val);
            if(is16BitMode.Value)
            {
                data |= (ushort)((ushort)RegisteredPeripheral.Transmit((byte)(val >> 8)) << 8);
            }
            readBufferFull.Value = true;
        }

        private ulong ReadData()
        {
            if(!isEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Tried to read data while peripheral is not enabled. Returning 0!");
                return 0;
            }
            readBufferFull.Value = false;

            var result = (is16BitMode.Value ? data : (byte)data);
            data = 0;
            return result;
        }

        private void UpdateInterrupts()
        {
            var state = enableIrqRead.Value && readBufferFull.Value;
            this.DebugLog("IRQ {0}.", state ? "set" : "unset");
            IRQ.Set(state);
        }

        private WordRegisterCollection registerCollection;

        private ushort data;
        private IFlagRegisterField isEnabled;
        private IFlagRegisterField is16BitMode;
        private IFlagRegisterField enableIrqRead;
        private IFlagRegisterField enableIrqWrite;
        private IFlagRegisterField readBufferFull;

        private enum Registers
        {
            DataInOut = 0x0,
            Control1 = 0x2,
            Status = 0x4
        }
    }
}
