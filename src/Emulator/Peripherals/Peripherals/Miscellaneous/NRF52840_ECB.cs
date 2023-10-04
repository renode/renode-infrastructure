//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities.Crypto;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class NRF52840_ECB : BasicDoubleWordPeripheral, IKnownSize, INRFEventProvider
    {
        public NRF52840_ECB(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            UpdateInterrupts();
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        public event Action<uint> EventTriggered;

        private void DefineRegisters()
        {
            Registers.Start.Define(this)
                .WithFlag(0, FieldMode.Write, name: "TASKS_START",
                    writeCallback: (_, __) =>
                    {
                        RunEncryption();
                        eventEnd.Value = true;
                        EventTriggered?.Invoke((uint)Registers.EventEnd);
                        UpdateInterrupts();
                    })
                .WithReservedBits(1, 31)
            ;

            Registers.EventEnd.Define(this, name: "EVENTS_ENDECB")
                .WithFlag(0, out eventEnd, name: "ENDECB")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptEnableSet.Define(this, name: "INTENSET")
                .WithFlag(0, out endInterruptEnabled, FieldMode.Set | FieldMode.Read, name: "ENDECB")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptEnableClear.Define(this, name: "INTENCLR")
                .WithFlag(0,
                    writeCallback: (_, value) => endInterruptEnabled.Value &= !value,
                    valueProviderCallback: _ => endInterruptEnabled.Value, name: "ENDECB")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.DataPtr.Define(this)
                .WithValueField(0, 32, out dataPointer, name: "ECBDATAPTR")
            ;
        }

        private void RunEncryption()
        {
            this.Log(LogLevel.Debug, "Running the encryption process; key at 0x{0:X}, cleartext at 0x{1:X}", dataPointer.Value, dataPointer.Value + KeySize);
            
            var key = sysbus.ReadBytes(dataPointer.Value, KeySize);
            var clearText = sysbus.ReadBytes(dataPointer.Value + KeySize, ClearTextSize);
            var clearTextBlock = Block.UsingBytes(clearText);
            
            using(var aes = AesProvider.GetEcbProvider(key))
            {
                aes.EncryptBlockInSitu(clearTextBlock);
            }

            sysbus.WriteBytes(clearText, dataPointer.Value + KeySize + ClearTextSize);
        }

        private void UpdateInterrupts()
        {
            var flag = false;

            flag |= endInterruptEnabled.Value & eventEnd.Value;

            IRQ.Set(flag);
        }

        private IFlagRegisterField eventEnd;
        private IFlagRegisterField endInterruptEnabled;
        private IValueRegisterField dataPointer;

        private const int KeySize = 16;
        private const int ClearTextSize = 16;

        private enum Registers
        {
            Start = 0x0,
            Stop = 0x4,
            EventEnd = 0x100,
            EventError = 0x104,
            InterruptEnableSet = 0x304,
            InterruptEnableClear = 0x308,
            DataPtr = 0x504,
        }
    }
}
