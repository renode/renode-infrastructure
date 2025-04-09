//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class SAMD21_I2C : SimpleContainer<II2CPeripheral>,
        IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>,
        IWordPeripheral, IProvidesRegisterCollection<WordRegisterCollection>,
        IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>,
        IKnownSize
    {
        public SAMD21_I2C(IMachine machine) : base(machine)
        {
            // NOTE: This class is manually managing access translation for respective
            //       register collections. As such, this comes with some caveats; please refer
            //       to the implementation of Read/Write methods for more information.
            doubleWordRegistersCollection = new DoubleWordRegisterCollection(this);
            wordRegistersCollection = new WordRegisterCollection(this);
            byteRegistersCollection = new ByteRegisterCollection(this);

            IRQ = new GPIO();

            DefineRegisters();
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return doubleWordRegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            doubleWordRegistersCollection.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            if(!wordRegistersCollection.HasRegisterAtOffset(offset))
            {
                // NOTE: Fallback to DoubleWord registers if we don't have
                //       an explicit Word register for given offset
                return this.ReadWordUsingDoubleWord(offset);
            }
            return wordRegistersCollection.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            if(!wordRegistersCollection.HasRegisterAtOffset(offset))
            {
                // NOTE: Fallback to DoubleWord registers if we don't have
                //       an explicit Word register for given offset
                // WARN: THIS SHOULD BE USED CAREFULLY IF THE valueProviderCallback
                //       FOR GIVEN REGISTER HAS SIDE-EFFECTS!
                this.WriteWordUsingDoubleWord(offset, value);
                return;
            }
            wordRegistersCollection.Write(offset, value);
        }

        public byte ReadByte(long offset)
        {
            if(!byteRegistersCollection.HasRegisterAtOffset(offset))
            {
                // NOTE: Fallback to Word registers if we don't have
                //       an explicit Byte register for given offset
                return this.ReadByteUsingWord(offset);
            }
            return byteRegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            if(!byteRegistersCollection.HasRegisterAtOffset(offset))
            {
                // NOTE: Fallback to Word registers if we don't have
                //       an explicit Byte register for given offset
                // WARN: THIS SHOULD BE USED CAREFULLY IF THE valueProviderCallback
                //       FOR GIVEN REGISTER HAS SIDE-EFFECTS!
                this.WriteByteUsingWord(offset, value);
                return;
            }
            byteRegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            doubleWordRegistersCollection.Reset();
            wordRegistersCollection.Reset();
            byteRegistersCollection.Reset();
        }

        public GPIO IRQ { get; }

        DoubleWordRegisterCollection IProvidesRegisterCollection<DoubleWordRegisterCollection>.RegistersCollection => doubleWordRegistersCollection;
        WordRegisterCollection IProvidesRegisterCollection<WordRegisterCollection>.RegistersCollection => wordRegistersCollection;
        ByteRegisterCollection IProvidesRegisterCollection<ByteRegisterCollection>.RegistersCollection => byteRegistersCollection;

        public long Size => 0x100;

        private void TryFlush()
        {
            if(activePeripheral == null || txQueue.Count == 0)
            {
                return;
            }

            activePeripheral.Write(txQueue.ToArray());
            txQueue.Clear();
        }

        private void StartTransaction(int address, bool reading)
        {
            if(!TryGetByAddress(address, out var peripheral))
            {
                // NOTE: Peripheral with given address is not connected
                hostOnBusInterruptPending.Value = true;
                acknowledgeMissing.Value = true;
                UpdateInterrupts();
                return;
            }

            activePeripheral = peripheral;
            state = reading ? State.Reading : State.Writing;
            RestartTransaction();
        }

        private void RestartTransaction()
        {
            if(state != State.Reading && state != State.Writing)
            {
                this.Log(LogLevel.Warning, "Tried to RESTART without an ongoing transaction");
                return;
            }


            txQueue.Clear();
            hostOnBusInterruptPending.Value |= state != State.Reading;
            clientOnBusInterruptPending.Value |= state == State.Reading;
            acknowledgeMissing.Value = false;
            UpdateInterrupts();
        }

        private void DefineRegisters()
        {
            var thisProvidesDoubleWordRegisterCollection = this as IProvidesRegisterCollection<DoubleWordRegisterCollection>;
            var thisProvidesWordRegisterCollection = this as IProvidesRegisterCollection<WordRegisterCollection>;
            var thisProvidesByteRegisterCollection = this as IProvidesRegisterCollection<ByteRegisterCollection>;

            Registers.ControlA.Define(thisProvidesDoubleWordRegisterCollection)
                .WithFlag(0, FieldMode.Write, name: "SWRST",
                    writeCallback: (_, value) => { if (value) { Reset(); } })
                .WithFlag(1, out enabled, name: "ENABLE")
                .WithEnumField(2, 3, out mode, name: "MODE",
                    changeCallback: (previousValue, value) =>
                    {
                        switch(value)
                        {
                            case Mode.I2CHost:
                                break;

                            default:
                                this.Log(LogLevel.Warning, "{0} mode is currently not supported, reverting to {1}", value, previousValue);
                                mode.Value = previousValue;
                                break;
                        }

                    })
                .WithReservedBits(5, 2)
                .WithTaggedFlag("RUNSTDBY", 7)
                .WithReservedBits(8, 8)
                .WithTaggedFlag("PINOUT", 16)
                .WithReservedBits(17, 3)
                .WithTag("SDAHOLD", 20, 2)
                .WithTaggedFlag("MEXTTOEN", 22)
                .WithTaggedFlag("SEXTTOEN", 23)
                .WithEnumField(24, 2, out speedMode, name: "SPEED",
                    changeCallback: (previousValue, value) =>
                    {
                        switch(value)
                        {
                            case SpeedMode.Standard:
                            case SpeedMode.Fast:
                            case SpeedMode.HighSpeed:
                                break;
                            default:
                                this.WarningLog("Attempted write with reserved value to SPEED (0x{0:X}), ignoring", value);
                                speedMode.Value = previousValue;
                            break;
                        }
                    }
                )
                .WithReservedBits(26, 1)
                .WithTaggedFlag("SCLSM", 27)
                .WithTag("INACTOUT", 28, 2)
                .WithTaggedFlag("LOWTOUTEN", 30)
                .WithReservedBits(31, 1)
            ;

            Registers.ControlB.Define(thisProvidesDoubleWordRegisterCollection)
                .WithReservedBits(0, 8)
                .WithFlag(8, out smartMode, name: "SMEN")
                .WithTaggedFlag("GCMD / QCEN", 9)
                .WithTaggedFlag("AACKEN", 10)
                .WithReservedBits(11, 3)
                .WithTag("AMODE", 14, 2)
                .WithValueField(16, 2, name: "CMD",
                    valueProviderCallback: _ => 0,
                    writeCallback: (_, value) =>
                    {
                        switch(value)
                        {
                            case 0: // NOTE: No-op
                                break;
                            case 1:
                                RestartTransaction();
                                break;
                            case 2:
                                // NOTE: "Execute acknowledge, then read byte"
                                //       As we execute actual byte read on reading the `DATA` register,
                                //       we can omit that here.
                                break;
                            case 3:
                                state = State.Idle;
                                if(shouldFinishTransmission.Value && !smartMode.Value)
                                {
                                    activePeripheral?.FinishTransmission();
                                }
                                break;
                        }
                    })
                .WithFlag(18, out shouldFinishTransmission, name: "ACKACT")
                .WithReservedBits(19, 5)
                .WithReservedBits(24, 8)
            ;

            Registers.BaudRate.Define(thisProvidesDoubleWordRegisterCollection)
                .WithTag("BAUD", 0, 8)
                .WithTag("BAUDLOW", 8, 8)
                .WithTag("HSBAUD", 16, 8)
                .WithTag("HSBAUDLOW", 24, 8)
            ;

            Registers.InterruptEnableClear.Define(thisProvidesByteRegisterCollection)
                .WithFlag(0, name: "AMATCH / MB",
                    valueProviderCallback: _ => hostOnBusInterruptEnabled,
                    writeCallback: (_, value) => { if(value) hostOnBusInterruptEnabled = false; })
                .WithFlag(1, name: "PREC / SB",
                    valueProviderCallback: _ => clientOnBusInterruptEnabled,
                    writeCallback: (_, value) => { if(value) clientOnBusInterruptEnabled = false; })
                .WithReservedBits(2, 5)
                .WithFlag(7, name: "ERROR",
                    valueProviderCallback: _ => errorInterruptEnabled,
                    writeCallback: (_, value) => { if(value) errorInterruptEnabled = false; })
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptEnableSet.Define(thisProvidesByteRegisterCollection)
                .WithFlag(0, name: "AMATCH / MB",
                    valueProviderCallback: _ => hostOnBusInterruptEnabled,
                    writeCallback: (_, value) => { if(value) hostOnBusInterruptEnabled = true; })
                .WithFlag(1, name: "PREC / SB",
                    valueProviderCallback: _ => clientOnBusInterruptEnabled,
                    writeCallback: (_, value) => { if(value) clientOnBusInterruptEnabled = true; })
                .WithReservedBits(2, 5)
                .WithFlag(7, name: "ERROR",
                    valueProviderCallback: _ => errorInterruptEnabled,
                    writeCallback: (_, value) => { if(value) errorInterruptEnabled = true; })
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptFlagStatusAndClear.Define(thisProvidesByteRegisterCollection)
                .WithFlag(0, out hostOnBusInterruptPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "AMATCH / MB")
                .WithFlag(1, out clientOnBusInterruptPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "PREC / SB")
                .WithReservedBits(2, 5)
                .WithFlag(7, out errorInterruptPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "ERROR")
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.Status.Define(thisProvidesWordRegisterCollection)
                .WithTaggedFlag("BUSERR", 0)
                .WithTaggedFlag("COLL / ARBLOST", 1)
                .WithFlag(2, out acknowledgeMissing, name: "RXNACK")
                .WithTaggedFlag("DIR", 3)
                .WithTag("SR / BUSSTATE", 4, 2)
                .WithTaggedFlag("LOWTOUT", 6)
                .WithTaggedFlag("CLKHOLD", 7)
                .WithTaggedFlag("MEXTTOUT", 8)
                .WithTaggedFlag("SEXTTOUT", 9)
                .WithTaggedFlag("HS / LENERR", 10)
                .WithReservedBits(11, 5)
            ;

            Registers.SynchronizationBusy.Define(thisProvidesDoubleWordRegisterCollection)
                .WithTaggedFlag("SWRST", 0)
                .WithTaggedFlag("ENABLE", 1)
                .WithTaggedFlag("SYSOP", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.AddressRegister.Define(thisProvidesDoubleWordRegisterCollection)
                .WithValueField(0, 11, name: "ADDR",
                    writeCallback: (_, value) =>
                    {
                        TryFlush();
                        var reading = (value & 0x1) > 0;
                        var address = (int)(value >> 1);
                        StartTransaction(address, reading);
                    })
                .WithReservedBits(11, 2)
                .WithTaggedFlag("LENEN", 13)
                .WithTaggedFlag("HS", 14)
                .WithTaggedFlag("TENBITEN", 15)
                .WithTag("LEN", 16, 8)
                .WithReservedBits(24, 8)
            ;

            Registers.DataRegister0.Define(thisProvidesByteRegisterCollection)
                .WithValueField(0, 8, name: "DATA",
                    valueProviderCallback: _ =>
                    {
                        if(state == State.Reading)
                        {
                            clientOnBusInterruptPending.Value = true;
                            UpdateInterrupts();
                        }

                        var returnValue = activePeripheral?.Read(1)?.FirstOrDefault() ?? (byte)0x00;
                        if(smartMode.Value && shouldFinishTransmission.Value)
                        {
                            activePeripheral?.FinishTransmission();
                        }

                        return returnValue;
                    },
                    writeCallback: (_, value) =>
                    {
                        if(state != State.Writing)
                        {
                            this.Log(LogLevel.Warning, "Tried to write the DATA register while not in writing state");
                            return;
                        }

                        txQueue.Enqueue((byte)value);
                        hostOnBusInterruptPending.Value = true;
                        UpdateInterrupts();
                    })
            ;

            Registers.DataRegister1.Define(thisProvidesByteRegisterCollection)
                .WithReservedBits(0, 8)
            ;

            Registers.DebugControl.Define(thisProvidesByteRegisterCollection)
                .WithTaggedFlag("DBGSTOP", 0)
                .WithReservedBits(1, 7)
            ;
        }

        private void UpdateInterrupts()
        {
            var interrupt = false;

            interrupt |= hostOnBusInterruptEnabled && hostOnBusInterruptPending.Value;
            interrupt |= clientOnBusInterruptEnabled && clientOnBusInterruptPending.Value;
            interrupt |= errorInterruptEnabled && errorInterruptPending.Value;

            this.Log(LogLevel.Debug, "IRQ set to: {0}", interrupt);
            IRQ.Set(interrupt);
        }

        private readonly DoubleWordRegisterCollection doubleWordRegistersCollection;
        private readonly WordRegisterCollection wordRegistersCollection;
        private readonly ByteRegisterCollection byteRegistersCollection;
        private readonly Queue<byte> txQueue = new Queue<byte>();

        private State state;
        private II2CPeripheral activePeripheral;

        private IFlagRegisterField enabled;
        private IEnumRegisterField<Mode> mode;
        private IFlagRegisterField smartMode;
        private IEnumRegisterField<SpeedMode> speedMode;
        private IFlagRegisterField acknowledgeMissing;
        private IFlagRegisterField shouldFinishTransmission;

        private IFlagRegisterField hostOnBusInterruptPending;
        private IFlagRegisterField clientOnBusInterruptPending;
        private IFlagRegisterField errorInterruptPending;

        private bool hostOnBusInterruptEnabled;
        private bool clientOnBusInterruptEnabled;
        private bool errorInterruptEnabled;

        private enum State
        {
            Unknown,
            Idle,
            Reading,
            Writing,
        }

        private enum Mode : ulong
        {
            USARTExternalClock,
            USARTInternalClock,
            SPIClient,
            SPIHost,
            I2CClient,
            I2CHost,
            Undefined0,
            Undefined1,
        }

        private enum SpeedMode : ulong
        {
            Standard,
            Fast,
            HighSpeed,
        }

        private enum Registers : long
        {
            ControlA = 0x0, // CTRLA
            ControlB = 0x4, // CTRLB
            // Reserved 0x08 - 0x0B,
            BaudRate = 0xC, // BAUD
            // Reserved 0x10 - 0x13,
            InterruptEnableClear = 0x14, // INTENCLR
            // Reserved 0x15,
            InterruptEnableSet = 0x16, // INTENSET
            // Reserved 0x17,
            InterruptFlagStatusAndClear = 0x18, // INTFLAG
            // Reserved 0x19,
            Status = 0x1A, // STATUS
            SynchronizationBusy = 0x1C, // SYNCBUSY
            // Reserved 0x20 - 0x23,
            AddressRegister = 0x24, // ADDR
            DataRegister0 = 0x28, // DATA [ 7:0]
            DataRegister1 = 0x29, // DATA [15:8]
            DebugControl = 0x30 // DBGCTRL
        }
    }
}
