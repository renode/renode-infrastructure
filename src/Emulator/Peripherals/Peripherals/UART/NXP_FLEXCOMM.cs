//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure;
using Antmicro.Migrant;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.I2C;

namespace Antmicro.Renode.Peripherals.UART
{
    public class NXP_FLEXCOMM : IKnownSize, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IUART,
        IPeripheralContainer<IUART, NullRegistrationPoint>,
        IPeripheralContainer<II2CPeripheral, NumberRegistrationPoint<int>>
    {
        public NXP_FLEXCOMM(IMachine machine, uint uartFifoSize = 256, bool uartPresent = true, bool i2cPresent = true, bool spiPresent = true)
        {
            if (!uartPresent && !i2cPresent && !spiPresent)
            {
                throw new ConstructionException("At least one communication mode (UART, SPI, or I2C) must be present!");
            }

            IRQ = new GPIO();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            this.machine = machine;

            this.uartPresent = uartPresent;
            this.i2cPresent = i2cPresent;
            this.spiPresent = spiPresent;

            uartInstance = new NXP_LPUART(machine, fifoSize: uartFifoSize, separateIRQs: true);
            uartInstance.CharReceived += (data) => { CharReceived?.Invoke(data); };
            uartInstance.IRQ.AddStateChangedHook((state) => UpdateTxGPIO(PeripheralMode.UART, state));
            uartInstance.SeparateRxIRQ.AddStateChangedHook((state) => UpdateRxGPIO(PeripheralMode.UART, state));
            uartContainer = new NullRegistrationPointContainerHelper<IUART>(machine, this);

            i2cInstance = new S32K3XX_LowPowerInterIntegratedCircuit(machine);
            i2cInstance.IRQ.AddStateChangedHook((state) => UpdateI2CMasterGPIO(PeripheralMode.I2C, state));

            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
            IRQ.Unset();
            uartInstance.Reset();
            RegistersCollection.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            if (IsFlexcommRegister(offset))
            {
                return RegistersCollection.Read(offset);
            }
            else if (currentPeripheralMode == PeripheralMode.UART)
            {
                return uartInstance.ReadDoubleWord(offset);
            }
            else if (currentPeripheralMode == PeripheralMode.I2C)
            {
                return i2cInstance.ReadDoubleWord(GetI2CRegistersOffset(offset));
            }
            else
            {
                this.Log(LogLevel.Warning, $"Read (0x{offset:x}) for unsupported FLEXCOMM mode: {currentPeripheralMode} - returning 0x0.");
                return 0x0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if (IsFlexcommRegister(offset))
            {
                RegistersCollection.Write(offset, value);
            }
            else if (currentPeripheralMode == PeripheralMode.UART)
            {
                uartInstance.WriteDoubleWord(offset, value);
            }
            else if (currentPeripheralMode == PeripheralMode.I2C)
            {
                i2cInstance.WriteDoubleWord(GetI2CRegistersOffset(offset), value);
            }
            else
            {
                this.Log(LogLevel.Warning, $"Write (0x{offset:x} with 0x{value:x}) for unsupported FLEXCOMM mode: {currentPeripheralMode} - ignoring write.");
            }
        }

        public void WriteChar(byte value)
        {
            uartInstance.WriteChar(value);
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(IUART peripheral)
        {
            return uartContainer.GetRegistrationPoints(peripheral);
        }

        public void Register(IUART peripheral, NullRegistrationPoint registrationPoint)
        {
            uartContainer.Register(peripheral, registrationPoint);
        }

        public void Unregister(IUART peripheral)
        {
            uartContainer.Unregister(peripheral);
        }

        public virtual void Register(II2CPeripheral peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            EnsureChildPeripheralRegistered(i2cInstance, $"{machine.GetLocalName(this)}_i2c");
            i2cInstance.Register(peripheral, registrationPoint);
        }

        public virtual void Unregister(II2CPeripheral peripheral)
        {
            i2cInstance.Unregister(peripheral);
        }

        public IEnumerable<NumberRegistrationPoint<int>> GetRegistrationPoints(II2CPeripheral peripheral)
        {
            return i2cInstance.GetRegistrationPoints(peripheral);
        }

        public void UpdateTxGPIO(PeripheralMode source, bool state)
        {
            switch (source)
            {
                case PeripheralMode.UART:
                    {
                        uartTxInterruptSet.Value = state;
                        break;
                    }
                default:
                    {
                        this.Log(LogLevel.Warning, $"Unsupported FLEXCOMM mode: {currentPeripheralMode} - ignoring IRQ.");
                        return;
                    }
            }

            IRQ.Set(state);
        }

        public void UpdateRxGPIO(PeripheralMode source, bool state)
        {
            switch (source)
            {
                case PeripheralMode.UART:
                    {
                        uartRxInterruptSet.Value = state;
                        break;
                    }
                default:
                    {
                        this.Log(LogLevel.Error, $"Unsupported FLEXCOMM mode: {currentPeripheralMode} - ignoring IRQ.");
                        return;
                    }
            }

            IRQ.Set(uartTxInterruptSet.Value || uartRxInterruptSet.Value);
        }

        public void UpdateI2CMasterGPIO(PeripheralMode source, bool state)
        {
            switch (source)
            {
                case PeripheralMode.I2C:
                    {
                        i2cMasterInterruptSet.Value = state;
                        break;
                    }
                default:
                    {
                        this.Log(LogLevel.Error, $"Unsupported FLEXCOMM mode: {currentPeripheralMode} - ignoring IRQ.");
                        return;
                    }
            }

            IRQ.Set(state);
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        public uint BaudRate => uartInstance.BaudRate;
        public Bits StopBits => uartInstance.StopBits;
        public Parity ParityBit => uartInstance.ParityBit;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        [field: Transient]
        public event Action<byte> CharReceived;

        IEnumerable<IRegistered<IUART, NullRegistrationPoint>> IPeripheralContainer<IUART, NullRegistrationPoint>.Children => uartContainer.Children;
        IEnumerable<IRegistered<II2CPeripheral, NumberRegistrationPoint<int>>> IPeripheralContainer<II2CPeripheral, NumberRegistrationPoint<int>>.Children => i2cInstance.Children;

        private static bool IsFlexcommRegister(long offset)
        {
            return offset == (long)Registers.InterruptStatus || offset == (long)Registers.PeripheralSelectAndID;
        }

        private static long GetI2CRegistersOffset(long offset)
        {
            return offset - I2CRegistersOffset;
        }

        private void EnsureChildPeripheralRegistered(IPeripheral peripheral, string name)
        {
            if (!machine.IsRegistered(peripheral))
            {
                machine.RegisterAsAChildOf(this, peripheral, NullRegistrationPoint.Instance);
                machine.SetLocalName(i2cInstance, name);
            }
        }

        private bool IsCommunicationModeSupported(PeripheralMode mode)
        {
            switch (mode)
            {
                case PeripheralMode.UART:
                    return uartPresent;
                case PeripheralMode.I2C:
                    return i2cPresent;
                case PeripheralMode.SPI:
                    return spiPresent;
                case PeripheralMode.UARTI2C:
                    return uartPresent && i2cPresent;
                case PeripheralMode.None:
                    return true;
                default:
                    return false;
            }
        }

        private void TryChangePeripheralMode(PeripheralMode newMode)
        {
            if (peripheralModeLock.Value)
            {
                this.Log(LogLevel.Warning, "Cannot change mode: peripheral is locked.");
                return;
            }

            if (!IsCommunicationModeSupported(newMode))
            {
                this.Log(LogLevel.Warning, $"Cannot switch to {newMode}: mode not available.");
                return;
            }

            currentPeripheralMode = newMode;
        }

        private void DefineRegisters()
        {
            var peripheralSelectAndIdResetValue = 0x103000u;
            if (uartPresent)
            {
                BitHelper.SetBit(ref peripheralSelectAndIdResetValue, 4, true);
            }
            if (spiPresent)
            {
                BitHelper.SetBit(ref peripheralSelectAndIdResetValue, 5, true);
            }
            if (i2cPresent)
            {
                BitHelper.SetBit(ref peripheralSelectAndIdResetValue, 6, true);
            }

            Registers.InterruptStatus.Define(this)
                    .WithFlag(0, out uartTxInterruptSet, mode: FieldMode.Read, name: "UARTTX")
                    .WithFlag(1, out uartRxInterruptSet, mode: FieldMode.Read, name: "UARTRX")
                    .WithTaggedFlag("SPI", 2)
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out i2cMasterInterruptSet, mode: FieldMode.Read, name: "I2CM")
                    .WithTaggedFlag("I2CS", 5)
                    .WithReservedBits(6, 26);

            Registers.PeripheralSelectAndID.Define(this, peripheralSelectAndIdResetValue)
                    .WithEnumField<DoubleWordRegister, PeripheralMode>(0, 3,
                        writeCallback: (_, mode) => TryChangePeripheralMode(mode),
                        valueProviderCallback: _ => currentPeripheralMode,
                        name: "PERSEL"
                    )
                    .WithFlag(3, out peripheralModeLock, name: "LOCK")
                    .WithTaggedFlag("UARTPRESENT", 4)
                    .WithTaggedFlag("SPIPRESENT", 5)
                    .WithTaggedFlag("I2CPRESENT", 6)
                    .WithReservedBits(7, 5)
                    .WithTag("ID", 12, 20);
        }

        private readonly bool uartPresent;
        private readonly bool i2cPresent;
        private readonly bool spiPresent;

        private readonly IMachine machine;

        private readonly NXP_LPUART uartInstance;
        private readonly NullRegistrationPointContainerHelper<IUART> uartContainer;

        private readonly S32K3XX_LowPowerInterIntegratedCircuit i2cInstance;

        private PeripheralMode currentPeripheralMode;
        private IFlagRegisterField uartTxInterruptSet;
        private IFlagRegisterField uartRxInterruptSet;
        private IFlagRegisterField i2cMasterInterruptSet;
        private IFlagRegisterField peripheralModeLock;

        private const long I2CRegistersOffset = 0x800;

        public enum PeripheralMode
        {
            None = 0b000,
            UART = 0b001,
            SPI = 0b010,
            I2C = 0b011,
            UARTI2C = 0b111,
        }

        private enum Registers
        {
            VersionId = 0x0,
            Parameter = 0x4,
            Global = 0x8,
            PinConfiguration = 0xC,
            BaudRate = 0x10,
            Status = 0x14,
            Control = 0x18,
            Data = 0x1C,
            MatchAddress = 0x20,
            ModemIrda = 0x24,
            Fifo = 0x28,
            Watermark = 0x2C,
            DataReadOnly = 0x30,
            InterruptStatus = 0xFF4,
            PeripheralSelectAndID = 0xFF8,
        }
    }
}
