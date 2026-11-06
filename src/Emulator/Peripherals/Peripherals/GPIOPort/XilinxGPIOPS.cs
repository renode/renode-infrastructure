//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class XilinxGPIOPS : BaseGPIOPort, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public XilinxGPIOPS(IMachine machine, bool isZynqMP) : base(machine, PinAmount(isZynqMP))
        {
            this.isZynqMP = isZynqMP;
            banksDescription = BanksDescription(isZynqMP);
            IRQ = new GPIO();
            gpioORGate = new GPIOGate(IRQ);
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineBanks();
            DefineRegisters();
            Reset();
        }

        public uint ReadDoubleWord(long offset) => RegistersCollection.Read(offset);

        public void WriteDoubleWord(long offset, uint value) => RegistersCollection.Write(offset, value);

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            foreach(var bank in bankControllers)
            {
                if(bank.TryTranslateGlobalPinIntoLocal(number, out var localPin))
                {
                    bank.OnGPIO(localPin, value);
                    return;
                }
            }
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            base.Reset();
            foreach(var bank in bankControllers)
            {
                bank.Reset();
            }
        }

        public long Size => 0x1000;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; }

        private static (int pinAmount, BankController.Type type)[] BanksDescription(bool isZynqMP)
        {
            if(isZynqMP)
            {
                return
                [
                    (26, BankController.Type.MIO),
                    (26, BankController.Type.MIO),
                    (26, BankController.Type.MIO),
                    (32, BankController.Type.EMIO),
                    (32, BankController.Type.EMIO),
                    (32, BankController.Type.EMIO),
                ];
            }
            return
            [
                (32, BankController.Type.MIO),
                (22, BankController.Type.MIO),
                (32, BankController.Type.EMIO),
                (32, BankController.Type.EMIO),
            ];
        }

        private static int PinAmount(bool isZynqMP) => BanksDescription(isZynqMP).Sum(description => description.pinAmount);

        private void SetPinState(int pin, bool value) => SetConnectionStateBit(pin, value);

        private void DefineBanks()
        {
            bankControllers = new BankController[banksDescription.Length];

            var bankPinsOffset = 0;
            for(var bankIndex = 0; bankIndex < banksDescription.Length; bankIndex++)
            {
                var (pinAmount, type) = banksDescription[bankIndex];
                var bankIRQ = gpioORGate.GetGPIO();
                var bankController = new BankController(this, bankIndex, bankPinsOffset, pinAmount, bankIRQ, type);
                bankControllers[bankIndex] = bankController;
                bankPinsOffset += pinAmount;
            }
        }

        // Must be called after defining banks
        private void DefineRegisters()
        {
            var bankCount = (uint)bankControllers.Length;
            Registers.MaskableOutputData0Low.DefineMany(this, bankCount, stepInBytes: MaskableDataStride,
                setup: (register, index) => register
                    .WithValueField(0, 16, out bankControllers[index].MaskedGPIODataLowField,
                        valueProviderCallback: _ => bankControllers[index].GPIODataLow,
                        name: $"DATA_{index}_LSW")
                    .WithValueField(16, 16, out bankControllers[index].GPIODataLowMaskField,
                        FieldMode.Write, name: $"MASK_{index}_LSW")
                    .WithWriteCallback((_, __) =>
                    {
                        var bank = bankControllers[index];
                        bank.SetGPIODataLowMasked((ushort)bank.MaskedGPIODataLowField.Value,
                            (ushort)bank.GPIODataLowMaskField.Value);
                        bank.UpdateState();
                    })
            );
            Registers.MaskableOutputData0High.DefineMany(this, bankCount, stepInBytes: MaskableDataStride,
                setup: (Action<DoubleWordRegister, int>)((register, index) =>
                {
                    var upperHalfWidth = bankControllers[(int)index].NumberOfPins - 16;
                    register
                        .WithValueField(0, (int)upperHalfWidth, out bankControllers[index].MaskedGPIODataHighField,
                            valueProviderCallback: _ => bankControllers[index].GPIODataHigh,
                            name: $"DATA_{index}_MSW")
                        .WithReservedBits((int)upperHalfWidth, (int)(16 - upperHalfWidth))
                        .WithValueField(16, (int)upperHalfWidth, out bankControllers[index].GPIODataHighMaskField,
                            FieldMode.Write, name: $"MASK_{index}_MSW")
                        .WithReservedBits((int)(16 + upperHalfWidth), (int)(16 - upperHalfWidth))
                        .WithWriteCallback((_, __) =>
                        {
                            var bank = bankControllers[index];
                            bank.SetGPIODataHighMasked((ushort)bank.MaskedGPIODataHighField.Value,
                                (ushort)bank.GPIODataHighMaskField.Value);
                            bank.UpdateState();
                        });
                })
            );
            Registers.OutputData0.DefineMany(this, bankCount, setup: (register, index) => register
                .WithValueField(0, bankControllers[index].NumberOfPins,
                    valueProviderCallback: _ => bankControllers[index].GPIOData,
                    writeCallback: (_, value) => bankControllers[index].GPIOData = (uint)value,
                    name: $"DATA_{index}")
                .WithReservedBits(bankControllers[index].NumberOfPins, 32 - bankControllers[index].NumberOfPins)
                .WithWriteCallback((_, __) => bankControllers[index].UpdateState())
            );
            Registers.DataReadonly0.DefineMany(this, bankCount, setup: (register, index) => register
                .WithValueField(0, bankControllers[index].NumberOfPins, FieldMode.Read,
                    valueProviderCallback: _ => bankControllers[index].GPIOReadback,
                    name: $"DATA_{index}_RO")
                .WithReservedBits(bankControllers[index].NumberOfPins, 32 - bankControllers[index].NumberOfPins)
            );

            Registers.DirectionMode0.DefineMany(this, bankCount, stepInBytes: BankFrameStride, setup: (register, index) => register
                .WithValueField(0, bankControllers[index].NumberOfPins, out bankControllers[index].DirectionModeField,
                    name: $"DIRM_{index}")
                .WithReservedBits(bankControllers[index].NumberOfPins, 32 - bankControllers[index].NumberOfPins)
                .WithWriteCallback((_, __) => bankControllers[index].UpdateState())
            );
            Registers.OutputEnable0.DefineMany(this, bankCount, stepInBytes: BankFrameStride, setup: (register, index) => register
                .WithValueField(0, bankControllers[index].NumberOfPins, out bankControllers[index].OutputEnabledField,
                    writeCallback: (_, value) => bankControllers[index].LogOutputEnableWrite(value),
                    name: $"OEN_{index}")
                .WithReservedBits(bankControllers[index].NumberOfPins, 32 - bankControllers[index].NumberOfPins)
                .WithWriteCallback((_, __) => bankControllers[index].UpdateState())
            );
            Registers.InterruptMask0.DefineMany(this, bankCount, stepInBytes: BankFrameStride, setup: (register, index) => register
                .WithValueField(0, bankControllers[index].NumberOfPins, out bankControllers[index].InterruptMaskField,
                    FieldMode.Read, name: $"INT_MASK_{index}")
                .WithReservedBits(bankControllers[index].NumberOfPins, 32 - bankControllers[index].NumberOfPins),
                resetValue: isZynqMP ? 0xFFFFFFFF : 0
            );
            Registers.InterruptEnable0.DefineMany(this, bankCount, stepInBytes: BankFrameStride, setup: (register, index) => register
                .WithValueField(0, bankControllers[index].NumberOfPins, FieldMode.Write,
                    writeCallback: (_, value) => bankControllers[index].EnableInterrupts((uint)value),
                    name: $"INT_EN_{index}")
                .WithReservedBits(bankControllers[index].NumberOfPins, 32 - bankControllers[index].NumberOfPins)
                .WithWriteCallback((_, __) => bankControllers[index].UpdateState())
            );
            Registers.InterruptDisable0.DefineMany(this, bankCount, stepInBytes: BankFrameStride, setup: (register, index) => register
                .WithValueField(0, bankControllers[index].NumberOfPins, FieldMode.Write,
                    writeCallback: (_, value) => bankControllers[index].DisableInterrupts((uint)value),
                    name: $"INT_DIS_{index}")
                .WithReservedBits(bankControllers[index].NumberOfPins, 32 - bankControllers[index].NumberOfPins)
                .WithWriteCallback((_, __) => bankControllers[index].UpdateState())
            );
            Registers.InterruptStatus0.DefineMany(this, bankCount, stepInBytes: BankFrameStride, setup: (register, index) => register
                .WithValueField(0, bankControllers[index].NumberOfPins,
                    valueProviderCallback: _ => bankControllers[index].InterruptStatus,
                    writeCallback: (_, value) => bankControllers[index].ClearInterrupts((uint)value), name: $"INT_STAT_{index}")
                .WithReservedBits(bankControllers[index].NumberOfPins, 32 - bankControllers[index].NumberOfPins)
                .WithWriteCallback((_, __) => bankControllers[index].UpdateState())
            );
            Registers.InterruptType0.DefineMany(this, bankCount, stepInBytes: BankFrameStride, setup: (register, index) => register
                .WithValueField(0, bankControllers[index].NumberOfPins, out bankControllers[index].InterruptTypeField,
                    name: $"INT_TYPE_{index}")
                .WithReservedBits(bankControllers[index].NumberOfPins, 32 - bankControllers[index].NumberOfPins)
                .WithWriteCallback((_, __) => bankControllers[index].UpdateState()),
                resetValue: 0xFFFFFFFF);
            Registers.InterruptPolarity0.DefineMany(this, bankCount, stepInBytes: BankFrameStride, setup: (register, index) => register
                .WithValueField(0, bankControllers[index].NumberOfPins, out bankControllers[index].InterruptPolarityField,
                    name: $"INT_POLARITY_{index}")
                .WithReservedBits(bankControllers[index].NumberOfPins, 32 - bankControllers[index].NumberOfPins)
                .WithWriteCallback((_, __) => bankControllers[index].UpdateState())
            );
            Registers.InterruptAnyEdge0.DefineMany(this, bankCount, stepInBytes: BankFrameStride, setup: (register, index) => register
                .WithValueField(0, bankControllers[index].NumberOfPins, out bankControllers[index].InterruptAnyField,
                    name: $"INT_ANY_{index}")
                .WithReservedBits(bankControllers[index].NumberOfPins, 32 - bankControllers[index].NumberOfPins)
                .WithWriteCallback((_, __) => bankControllers[index].UpdateState())
            );
        }

        private BankController[] bankControllers;

        private readonly (int pinAmount, BankController.Type type)[] banksDescription;

        private readonly bool isZynqMP;

        private readonly GPIOGate gpioORGate;

        private const uint MaskableDataStride = 0x8;
        private const uint BankFrameStride = 0x40;
        private const long BankFrameOffset = 0x204;

        private class BankController : IGPIOReceiver
        {
            public BankController(XilinxGPIOPS parent, int bankIndex, int bankPinsOffset, int numberOfPins, IGPIO bankIRQ, Type type)
            {
                this.parent = parent;
                NumberOfPins = numberOfPins;
                AlwaysIgnoreMask = BitHelper.CalculateMask(32 - numberOfPins, numberOfPins);
                this.bankIndex = bankIndex;
                this.bankPinsOffset = bankPinsOffset;
                this.bankIRQ = bankIRQ;
                this.type = type;
            }

            public void LogOutputEnableWrite(ulong value)
            {
                if(type == Type.EMIO)
                {
                    parent.NoisyLog(
                        "Attempted enabling EMIO output, but EMIO outputs are exposed exclusively to PL",
                        value, bankIndex);
                }
            }

            public void EnableInterrupts(uint pins)
            {
                InterruptMaskField.Value &= ~pins;
            }

            public void DisableInterrupts(uint pins)
            {
                InterruptMaskField.Value |= pins;
            }

            public void ClearInterrupts(uint pins)
            {
                interruptStatus &= ~pins;
            }

            public void UpdateState()
            {
                GPIOReadback = type switch
                {
                    // UG1085, "MIO Signals", p. 775: "When the output enable is inactive, the pin is an input pin,
                    // and the value is available at the read only location."
                    Type.MIO => (inputGpioState & ~OutputDrivingMask) | (gpioData & OutputDrivingMask),
                    // UG1085, "EMIO Signals", p. 775: inputs can be read from DATA_RO when DIRM is set to 0.
                    Type.EMIO => inputGpioState & ~(uint)DirectionModeField.Value,
                    _ => throw new ArgumentOutOfRangeException(nameof(type)),
                };

                // UG585, DATA_0_RO, Appendix B, p. 1354: "bits[8:7] of bank0 cannot be used as inputs
                // and will always return 0 when read."
                GPIOReadback &= ~VModePinsMask;

                for(var pin = 0; pin < NumberOfPins; pin++)
                {
                    var pinMask = 1u << pin;
                    parent.SetPinState(bankPinsOffset + pin, (OutputDrivingMask & gpioData & pinMask) != 0);
                }

                for(var pin = 0; pin < NumberOfPins; pin++)
                {
                    var pinMask = 1u << pin;
                    var currentState = (GPIOReadback & pinMask) != 0;
                    var changed = ((previousGpioReadback ^ GPIOReadback) & pinMask) != 0;

                    switch(DecodeInterruptTrigger(pin))
                    {
                    case InterruptTrigger.ActiveLow:
                        BitHelper.SetBit(ref interruptStatus, (byte)pin, !currentState);
                        break;
                    case InterruptTrigger.ActiveHigh:
                        BitHelper.SetBit(ref interruptStatus, (byte)pin, currentState);
                        break;
                    case InterruptTrigger.FallingEdge:
                        interruptStatus |= changed && !currentState ? pinMask : 0;
                        break;
                    case InterruptTrigger.RisingEdge:
                        interruptStatus |= changed && currentState ? pinMask : 0;
                        break;
                    case InterruptTrigger.BothEdges:
                        interruptStatus |= changed ? pinMask : 0;
                        break;
                    }
                }

                bankIRQ.Set((interruptStatus & ~(uint)InterruptMaskField.Value) != 0);
                previousGpioReadback = GPIOReadback;
            }

            public bool TryTranslateGlobalPinIntoLocal(int pin, out int localPin)
            {
                localPin = -1;
                if(bankPinsOffset <= pin && pin < bankPinsOffset + NumberOfPins)
                {
                    localPin = pin - bankPinsOffset;
                    return true;
                }
                return false;
            }

            public void Reset()
            {
                inputGpioState = 0;
                gpioData = 0;
                GPIOReadback = 0;
                previousGpioReadback = 0;
                interruptStatus = 0;
                bankIRQ.Unset();
            }

            public void OnGPIO(int number, bool value)
            {
                if(IsVModePin(number) || number < 0 || NumberOfPins <= number)
                {
                    return;
                }
                BitHelper.SetBit(ref inputGpioState, (byte)number, value);
                UpdateState();
            }

            public void SetGPIODataLowMasked(ushort newValue, ushort unchangedMask)
            {
                GPIODataLow = (ushort)((GPIODataLow & unchangedMask) | (newValue & ~unchangedMask));
            }

            public void SetGPIODataHighMasked(ushort newValue, ushort unchangedMask)
            {
                GPIODataHigh = (ushort)((GPIODataHigh & unchangedMask) | (newValue & ~unchangedMask));
            }

            public uint ValidPinsMask => ~AlwaysIgnoreMask;

            public uint GPIOReadback
            {
                get => gpioReadback;
                private set => gpioReadback = value & ~AlwaysIgnoreMask;
            }

            public uint InterruptStatus => interruptStatus;

            public uint GPIOData
            {
                get => gpioData;
                set
                {
                    gpioData = ~AlwaysIgnoreMask & value;
                }
            }

            public ushort GPIODataLow
            {
                get => (ushort)(GPIOData & 0xFFFF);
                set
                {
                    GPIOData = (GPIOData & (0xFFFFu << 16)) | (value & 0xFFFFu);
                }
            }

            public ushort GPIODataHigh
            {
                get => (ushort)(GPIOData >> 16);
                set
                {
                    GPIOData = (GPIOData & 0xFFFFu) | ((uint)value << 16);
                }
            }

            public IValueRegisterField InterruptAnyField;
            public IValueRegisterField InterruptPolarityField;
            public IValueRegisterField InterruptTypeField;

            public IValueRegisterField DirectionModeField;
            public IValueRegisterField GPIODataHighMaskField;
            public IValueRegisterField GPIODataLowMaskField;
            public IValueRegisterField OutputEnabledField;
            public IValueRegisterField InterruptMaskField;
            public IValueRegisterField MaskedGPIODataHighField;
            public IValueRegisterField MaskedGPIODataLowField;
            public readonly int NumberOfPins;

            private InterruptTrigger DecodeInterruptTrigger(int pinIndex)
            {
                var isEdgeSensitive = BitHelper.IsBitSet(InterruptTypeField.Value, (byte)pinIndex);
                var isHighOrRising = BitHelper.IsBitSet(InterruptPolarityField.Value, (byte)pinIndex);
                var isAnyEdge = BitHelper.IsBitSet(InterruptAnyField.Value, (byte)pinIndex);

                return (isEdgeSensitive, isHighOrRising, isAnyEdge) switch
                {
                    (false, false, _) => InterruptTrigger.ActiveLow,
                    (false, true, _) => InterruptTrigger.ActiveHigh,
                    (true, _, true) => InterruptTrigger.BothEdges,
                    (true, false, false) => InterruptTrigger.FallingEdge,
                    (true, true, false) => InterruptTrigger.RisingEdge,
                };
            }

            private bool IsVModePin(int pinNumber) => IsVModeBank && (pinNumber == 7 || pinNumber == 8);

            private bool IsVModeBank => !parent.isZynqMP && bankIndex == 0;

            private uint OutputDrivingMask => type switch
            {
                Type.MIO => (uint)(DirectionModeField.Value & OutputEnabledField.Value),
                Type.EMIO => (uint)DirectionModeField.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };

            private uint VModePinsMask => IsVModeBank ? (1u << 7) | (1u << 8) : 0;

            private uint inputGpioState;
            private uint gpioData;
            private uint gpioReadback;
            private uint previousGpioReadback;
            private uint interruptStatus;

            private readonly uint AlwaysIgnoreMask;
            private readonly int bankIndex;
            private readonly int bankPinsOffset;
            private readonly IGPIO bankIRQ;
            private readonly Type type;
            private readonly XilinxGPIOPS parent;

            public enum Type
            {
                MIO,
                EMIO,
            }

            private enum InterruptTrigger
            {
                ActiveLow,
                ActiveHigh,
                FallingEdge,
                RisingEdge,
                BothEdges,
            }
        }

        private enum BankRegisters : long
        {
            DirectionMode = 0,
            OutputEnable  = 0x4,
            InterruptMask = 0x8,
            InterruptEnable = 0xC,
            InterruptDisable = 0x10,
            InterruptStatus = 0x14,
            InterruptType = 0x18,
            InterruptPolarity = 0x1C,
            InterruptAnyEdge = 0x20,
        }

        private enum Registers : long
        {
            MaskableOutputData0Low = 0x000,
            MaskableOutputData0High = 0x004,
            OutputData0 = 0x040,
            DataReadonly0 = 0x060,

            DirectionMode0 = BankFrameOffset + (long)BankRegisters.DirectionMode,
            OutputEnable0 = BankFrameOffset + (long)BankRegisters.OutputEnable,
            InterruptMask0 = BankFrameOffset + (long)BankRegisters.InterruptMask,
            InterruptEnable0 = BankFrameOffset + (long)BankRegisters.InterruptEnable,
            InterruptDisable0 = BankFrameOffset + (long)BankRegisters.InterruptDisable,
            InterruptStatus0 = BankFrameOffset + (long)BankRegisters.InterruptStatus,
            InterruptType0 = BankFrameOffset + (long)BankRegisters.InterruptType,
            InterruptPolarity0 = BankFrameOffset + (long)BankRegisters.InterruptPolarity,
            InterruptAnyEdge0 = BankFrameOffset + (long)BankRegisters.InterruptAnyEdge,
        }
    }
}
