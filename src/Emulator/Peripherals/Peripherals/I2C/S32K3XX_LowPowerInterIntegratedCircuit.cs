//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.I2C
{
    [AllowedTranslations(AllowedTranslation.DoubleWordToByte)]
    public class S32K3XX_LowPowerInterIntegratedCircuit : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public S32K3XX_LowPowerInterIntegratedCircuit(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();

            rxQueue = new Queue<byte>();
            txQueue = new Queue<byte>();

            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            RegistersCollection.Reset();

            rxQueue.Clear();
            txQueue.Clear();
            UpdateInterrupts();
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }
        public GPIO IRQ { get; }
        public long Size => 0x1000;

        private void UpdateInterrupts()
        {
            var interrupt = false;
            interrupt |= transmitDataInterruptEnabled.Value;
            interrupt |= receiveDataInterruptEnabled.Value && ReceiveDataFlag;
            interrupt |= stopDetectFlag.Value && stopDetectInterruptEnabled.Value;
            interrupt |= endPacketFlag.Value && endPacketInterruptEnabled.Value;
            interrupt |= nackDetectFlag.Value && nackDetectInterruptEnabled.Value;

            this.Log(LogLevel.Debug, "IRQ={0}, transmitDataInterrupt={1}, receiveDataInterrupt={2}, stopDetectInterrupt={3}, endPacketInterrupt={4}, nackDetectInterrupt={5}",
                interrupt,
                transmitDataInterruptEnabled.Value,
                receiveDataInterruptEnabled.Value && ReceiveDataFlag,
                stopDetectFlag.Value && stopDetectInterruptEnabled.Value,
                endPacketFlag.Value && endPacketInterruptEnabled.Value,
                nackDetectFlag.Value && nackDetectInterruptEnabled.Value);
            IRQ.Set(interrupt);
        }

        private void HandleCommand(Command command)
        {
            if(command != Command.TransmitData)
            {
                TryFlushTransmitFIFO();
            }

            switch(command)
            {
                case Command.TransmitData:
                    if(AssertActivePeripheral("transmit data"))
                    {
                        txQueue.Enqueue((byte)transmitData.Value);
                        UpdateInterrupts();
                    }
                    break;

                case Command.ReceiveData:
                case Command.ReceiveDataAndDiscard:
                    if(!AssertActivePeripheral("receive data"))
                    {
                        break;
                    }

                    // transmitData now contains `amount of data - 1` we would want to receive
                    var receivedData = activePeripheral.Read((int)transmitData.Value + 1);

                    if(command != Command.ReceiveDataAndDiscard)
                    {
                        rxQueue.EnqueueRange(RunDataMatcher(receivedData));
                        UpdateInterrupts();
                    }
                    break;

                case Command.GenerateSTOP:
                    GenerateStopCondition();
                    break;

                case Command.Start:
                case Command.HiSpeedStart:
                    if(activePeripheral != null)
                    {
                        // This is RESTART condition
                        endPacketFlag.Value = true;
                        UpdateInterrupts();
                    }

                    if(automaticSTOPGeneration.Value)
                    {
                        GenerateStopCondition();
                    }

                    // transmitData now contains I2C address
                    if(!TryGetByAddress((int)transmitData.Value >> 1, out activePeripheral))
                    {
                        nackDetectFlag.Value = true;
                        UpdateInterrupts();
                    }

                    break;

                case Command.StartExpectsNACK:
                case Command.HiSpeedStartExpectsNACK:
                    if(activePeripheral != null)
                    {
                        // This is RESTART condition
                        endPacketFlag.Value = true;
                        UpdateInterrupts();
                    }

                    if(TryGetByAddress((int)transmitData.Value, out var _))
                    {
                        // We should write 1 to NACK Detect Flag (NDF) in case we don't expect ACK
                        nackDetectFlag.Value = true;
                        UpdateInterrupts();
                    }

                    break;
            }
        }

        private IEnumerable<byte> RunDataMatcher(IEnumerable<byte> bytes)
        {
            if(matchConfiguration.Value == MatchMode.Disabled || matchConfiguration.Value == MatchMode.Reserved)
            {
                return bytes;
            }

            int? found = null;
            var bytesIterator = bytes;
            switch(matchConfiguration.Value)
            {
                case MatchMode.FirstMatch0OrMatch1:
                    bytesIterator = bytesIterator.Take(1);
                    goto case MatchMode.AnyMatch0OrMatch1;
                case MatchMode.AnyMatch0OrMatch1:
                    found = bytesIterator
                        .Select((Byte, Index) => new { Byte, Index })
                        .FirstOrDefault(item => item.Byte == match0Value.Value || item.Byte == match1Value.Value)
                        ?.Index;
                    break;

                case MatchMode.FirstMatch0ThenMatch1:
                    bytesIterator = bytesIterator.Take(2);
                    goto case MatchMode.AnyMatch0ThenMatch1;
                case MatchMode.AnyMatch0ThenMatch1:
                    found = bytesIterator
                        .Zip(bytesIterator.Skip(1), (First, Second) => new { First, Second })
                        .Select((Byte, Index) => new { Byte, Index })
                        .FirstOrDefault(item => item.Byte.First == match0Value.Value && item.Byte.Second == match1Value.Value)
                        ?.Index;
                    break;

                case MatchMode.FirstAndMatch1EqualMatch0AndMatch1:
                    bytesIterator = bytesIterator.Take(1);
                    goto case MatchMode.AnyAndMatch1EqualMatch0AndMatch1;
                case MatchMode.AnyAndMatch1EqualMatch0AndMatch1:
                    var maskedValue = match0Value.Value & match1Value.Value;
                    found = bytesIterator
                        .Select((Byte, Index) => new { Byte, Index })
                        .FirstOrDefault(item => (item.Byte & match1Value.Value) == maskedValue)
                        ?.Index;
                    break;
            }

            dataMatchFlag.Value |= found.HasValue;
            UpdateInterrupts();

            if(receiveDataMatchOnly.Value)
            {
                return found.HasValue ? bytes.Skip(found.Value) : Enumerable.Empty<byte>();
            }
            return bytes;
        }

        private void GenerateStopCondition()
        {
            TryFlushTransmitFIFO();
            activePeripheral?.FinishTransmission();
            activePeripheral = null;
            stopDetectFlag.Value = true;
            endPacketFlag.Value = true;
            UpdateInterrupts();
        }

        private bool AssertActivePeripheral(string reason)
        {
            if(activePeripheral == null)
            {
                this.Log(LogLevel.Warning, "Tried to {0}, but target device wasn't chosen", reason);
                return false;
            }
            return true;
        }

        private void TryFlushTransmitFIFO()
        {
            if(txQueue.Count > 0 && controllerEnabled.Value)
            {
                activePeripheral?.Write(txQueue.ToArray());
            }

            txQueue.Clear();
            UpdateInterrupts();
        }

        private void DefineRegisters()
        {
            Registers.VersionID.Define(this, 0x1203)
                .WithTag("FeatureSpecificationNumber", 0, 16)
                .WithTag("MinorVersionNumber", 16, 8)
                .WithTag("MajorVersionNumber", 24, 8)
            ;

            Registers.Parameter.Define(this, 0x22)
                .WithTag("ControllerTransmitFIFOSize", 0, 4)
                .WithReservedBits(4, 4)
                .WithTag("ControllerReceiveFIFOSize", 8, 4)
                .WithReservedBits(12, 20)
            ;

            Registers.ControllerControl.Define(this)
                .WithFlag(0, out controllerEnabled, name: "ControllerEnable",
                    changeCallback: (_, value) => { if(value) TryFlushTransmitFIFO(); })
                .WithTaggedFlag("SoftwareReset", 1)
                .WithTaggedFlag("DozeModeEnable", 2)
                .WithTaggedFlag("DebugEnable", 3)
                .WithReservedBits(4, 4)
                .WithFlag(8, FieldMode.Read | FieldMode.WriteOneToClear, name: "ResetTransmitFIFO",
                    // If controller is enabled, we really shouldn't have anything in the FIFO anyway, so
                    // we can try to flush instead of just clearing buffer
                    writeCallback: (_, value) => { if(value) TryFlushTransmitFIFO(); })
                .WithFlag(9, FieldMode.Read | FieldMode.WriteOneToClear, name: "ResetReceiveFIFO",
                    writeCallback: (_, value) => { if(value) rxQueue.Clear(); })
                .WithReservedBits(10, 22)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.ControllerStatus.Define(this, 0x1)
                .WithFlag(0, FieldMode.Read, name: "TransmitDataFlag",
                    valueProviderCallback: _ => true)
                .WithFlag(1, FieldMode.Read, name: "ReceiveDataFlag",
                    valueProviderCallback: _ => ReceiveDataFlag)
                .WithReservedBits(2, 6)
                .WithFlag(8, out endPacketFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "EndPacketFlag")
                .WithFlag(9, out stopDetectFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "STOPDetectFlag")
                .WithFlag(10, out nackDetectFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "NACKDetectFlag")
                .WithTaggedFlag("ArbitrationLostFlag", 11)
                .WithTaggedFlag("FIFOErrorFlag", 12)
                .WithTaggedFlag("PinLowTimeoutFlag", 13)
                .WithFlag(14, out dataMatchFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "DataMatchFlag")
                .WithReservedBits(15, 9)
                .WithTaggedFlag("ControllerBusyFlag", 24)
                .WithTaggedFlag("BusBusyFlag", 25)
                .WithReservedBits(26, 6)
                .WithChangeCallback((_, __) => UpdateInterrupts());
            ;

            Registers.ControllerInterruptEnable.Define(this)
                .WithFlag(0, out transmitDataInterruptEnabled, name: "TransmitDataInterruptEnable")
                .WithFlag(1, out receiveDataInterruptEnabled, name: "ReceiveDataInterruptEnable")
                .WithReservedBits(2, 6)
                .WithFlag(8, out endPacketInterruptEnabled, name: "EndPacketInterruptEnable")
                .WithFlag(9, out stopDetectInterruptEnabled, name: "STOPDetectInterruptEnable")
                .WithFlag(10, out nackDetectInterruptEnabled, name: "NACKDetectInterruptEnable")
                .WithTaggedFlag("ArbitrationLostInterruptEnable", 11)
                .WithTaggedFlag("FIFOErrorInterruptEnable", 12)
                .WithTaggedFlag("PinLowTimeoutInterruptEnable", 13)
                .WithFlag(14, out dataMatchInterruptEnabled, name: "DataMatchInterruptEnable")
                .WithReservedBits(15, 7)
                .WithChangeCallback((_, __) => UpdateInterrupts());
            ;

            Registers.ControllerDMAEnable.Define(this)
                .WithTaggedFlag("TransmitDataDMAEnable", 0)
                .WithTaggedFlag("ReceiveDataDMAEnable", 1)
                .WithReservedBits(3, 29)
            ;

            Registers.ControllerConfiguration0.Define(this)
                .WithTaggedFlag("HostRequestEnable", 0)
                .WithTaggedFlag("HostRequestPolarity", 1)
                .WithTaggedFlag("HostRequestSelect", 2)
                .WithReservedBits(3, 5)
                .WithTaggedFlag("CircularFIFOEnable", 8)
                .WithFlag(9, out receiveDataMatchOnly, name: "ReceiveDataMatchOnly")
                .WithReservedBits(10, 12)
            ;

            Registers.ControllerConfiguration1.Define(this)
                .WithTag("Prescaler", 0, 3)
                .WithReservedBits(4, 4)
                .WithFlag(8, out automaticSTOPGeneration, name: "AutomaticSTOPGeneration")
                .WithTaggedFlag("IgnoreNACK", 9)
                .WithTaggedFlag("TimeoutConfiguration", 10)
                .WithReservedBits(11, 5)
                .WithEnumField(16, 3, out matchConfiguration, name: "MatchConfiguration")
                .WithReservedBits(19, 4)
                .WithTag("PinConfiguration", 24, 3)
                .WithReservedBits(27, 5)
            ;

            Registers.ControllerConfiguration2.Define(this)
                .WithTag("BusIdleTimeout", 0, 12)
                .WithReservedBits(12, 4)
                .WithTag("GlitchFilterSCL", 16, 4)
                .WithReservedBits(20, 4)
                .WithTag("GlitchFilterSDA", 24, 4)
                .WithReservedBits(28, 4)
            ;

            Registers.ControllerConfiguration3.Define(this)
                .WithReservedBits(0, 8)
                .WithTag("PinLowTimeout", 8, 12)
                .WithReservedBits(20, 12)
            ;

            Registers.ControllerDataMatch.Define(this)
                .WithValueField(0, 8, out match0Value, name: "Match0Value")
                .WithReservedBits(8, 8)
                .WithValueField(16, 8, out match1Value, name: "Match1Value")
                .WithReservedBits(24, 8)
            ;

            Registers.ControllerClockConfiguration0.Define(this)
                .WithTag("ClockLowPeriod", 0, 6)
                .WithReservedBits(6, 2)
                .WithTag("ClockHighPeriod", 8, 5)
                .WithReservedBits(14, 2)
                .WithTag("SetupHoldDelay", 16, 6)
                .WithReservedBits(22, 2)
                .WithTag("DataValidDelay", 24, 6)
                .WithReservedBits(30, 2)
            ;

            Registers.ControllerClockConfiguration1.Define(this)
                .WithTag("ClockLowPeriod", 0, 6)
                .WithReservedBits(6, 2)
                .WithTag("ClockHighPeriod", 8, 5)
                .WithReservedBits(14, 2)
                .WithTag("SetupHoldDelay", 16, 6)
                .WithReservedBits(22, 2)
                .WithTag("DataValidDelay", 24, 6)
                .WithReservedBits(30, 2)
            ;

            Registers.ControllerFIFOControl.Define(this)
                .WithValueField(0, 2, out txWatermark, name: "TransmitFIFOWatermark")
                .WithReservedBits(2, 14)
                .WithValueField(16, 2, out rxWatermark, name: "ReceiveFIFOWatermark")
                .WithReservedBits(18, 14)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.ControllerFIFOStatus.Define(this)
                .WithValueField(0, 2, FieldMode.Read, name: "TransmitFIFOCount",
                    valueProviderCallback: _ => 0)
                .WithReservedBits(2, 14)
                .WithValueField(16, 2, FieldMode.Read, name: "ReceiveFIFOCount",
                    valueProviderCallback: _ => (ulong)rxQueue.Count.Clamp(0, 2))
                .WithReservedBits(18, 14)
            ;

            Registers.ControllerTransmitData.Define(this)
                .WithValueField(0, 8, out transmitData, name: "TransmitData")
                .WithEnumField<DoubleWordRegister, Command>(8, 3, name: "CommandData",
                    writeCallback: (_, value) => HandleCommand(value))
                .WithReservedBits(11, 21)
            ;

            Registers.ControllerReceiveData.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "ReceiveData",
                    valueProviderCallback: _ =>
                    {
                        if(!rxQueue.TryDequeue(out var b))
                        {
                            this.Log(LogLevel.Warning, "Trying to read from empty rx fifo");
                            return default(byte);
                        }
                        if(rxQueue.Count == 0 && automaticSTOPGeneration.Value)
                        {
                            GenerateStopCondition();
                        }
                        return b;
                    })
                .WithReservedBits(8, 6)
                .WithFlag(14, FieldMode.Read, name: "ReceiveEmpty",
                    valueProviderCallback: _ => rxQueue.Count == 0)
                .WithReservedBits(15, 17)
                .WithReadCallback((_, __) => UpdateInterrupts())
            ;

            Registers.TargetControl.Define(this)
                .WithTaggedFlag("TargetEnable", 0)
                .WithTaggedFlag("SoftwareReset", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("FilterEnable", 4)
                .WithTaggedFlag("FilterDozeEnable", 5)
                .WithReservedBits(6, 2)
                .WithTaggedFlag("ResetTransmitFIFO", 8)
                .WithTaggedFlag("ResetReceiveFIFO", 9)
                .WithReservedBits(10, 22)
            ;

            Registers.TargetStatus.Define(this)
                .WithTaggedFlag("TransmitDataFlag", 0)
                .WithTaggedFlag("ReceiveDataFlag", 1)
                .WithTaggedFlag("AddressValidFlag", 2)
                .WithTaggedFlag("TransmitACKFlag", 3)
                .WithReservedBits(4, 4)
                .WithTaggedFlag("RepeatedStartFlag", 8)
                .WithTaggedFlag("STOPDetectFlag", 9)
                .WithTaggedFlag("BitErrorFlag", 10)
                .WithTaggedFlag("FIFOErrorFlag", 11)
                .WithTaggedFlag("AddressMatch0Flag", 12)
                .WithTaggedFlag("AddressMatch1Flag", 13)
                .WithTaggedFlag("GeneralCallFlag", 14)
                .WithTaggedFlag("SMBusAlertResponseFlag", 15)
                .WithReservedBits(16, 8)
                .WithTaggedFlag("TargetBusyFlag", 24)
                .WithTaggedFlag("BusBusyFlag", 25)
                .WithReservedBits(26, 6)
            ;

            Registers.TargetInterruptEnable.Define(this)
                .WithTaggedFlag("TransmitDataInterruptEnable", 0)
                .WithTaggedFlag("ReceiveDataInterruptEnable", 1)
                .WithTaggedFlag("AddressValidInterruptEnable", 2)
                .WithTaggedFlag("TransmitACKInterruptEnable", 3)
                .WithReservedBits(4, 4)
                .WithTaggedFlag("RepeatedStartInterruptEnable", 8)
                .WithTaggedFlag("STOPDetectInterruptEnable", 9)
                .WithTaggedFlag("BitErrorInterruptEnable", 10)
                .WithTaggedFlag("FIFOErrorInterruptEnable", 11)
                .WithTaggedFlag("AddressMatch0InterruptEnable", 12)
                .WithTaggedFlag("AddressMatch1InterruptEnable", 13)
                .WithTaggedFlag("GeneralCallInterruptEnable", 14)
                .WithTaggedFlag("SMBusAlertResponseInterruptEnable", 15)
                .WithReservedBits(16, 16)
            ;

            Registers.TargetDMAEnable.Define(this)
                .WithTaggedFlag("TransmitDataDMAEnable", 0)
                .WithTaggedFlag("ReceiveDataDMAEnable", 1)
                .WithTaggedFlag("AddressValidDMAEnable", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.TargetConfiguration1.Define(this)
                .WithTaggedFlag("AddressSCLStall", 0)
                .WithTaggedFlag("RXSCLStall", 1)
                .WithTaggedFlag("TransmitDataSCLStall", 2)
                .WithTaggedFlag("ACKSCLStall", 3)
                .WithReservedBits(4, 4)
                .WithTaggedFlag("GeneralCallEnable", 8)
                .WithTaggedFlag("SMBusAlertEnable", 9)
                .WithTaggedFlag("TransmitFlagConfiguration", 10)
                .WithTaggedFlag("ReceiveDataConfiguration", 11)
                .WithTaggedFlag("IgnoreNACK", 12)
                .WithTaggedFlag("HighSpeedModeEnable,", 13)
                .WithReservedBits(14, 2)
                .WithTag("AddressConfiguration", 16, 3)
                .WithReservedBits(19, 13)
            ;

            Registers.TargetConfiguration2.Define(this)
                .WithTag("ClockHoldTime", 0, 4)
                .WithReservedBits(4, 4)
                .WithTag("DataValidDelay", 8, 4)
                .WithReservedBits(14, 2)
                .WithTag("GlitchFilterSCL", 16, 4)
                .WithTag("GlitchFilterSDA", 24, 4)
            ;

            Registers.TargetAddressMatch.Define(this)
                .WithReservedBits(0, 1)
                .WithTag("Address0Value", 1, 10)
                .WithReservedBits(11, 6)
                .WithTag("Address0Value", 17, 10)
                .WithReservedBits(27, 5)
            ;

            Registers.TargetAddressStatus.Define(this)
                .WithTag("ReceivedAddress", 0, 11)
                .WithReservedBits(11, 3)
                .WithTaggedFlag("AddressNotValid", 14)
                .WithReservedBits(15, 17)
            ;

            Registers.TargetTransmitACK.Define(this)
                .WithTaggedFlag("TransmitNACK", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.TargetTransmitData.Define(this)
                .WithTag("TransmitData", 0, 8)
                .WithReservedBits(8, 24)
            ;

            Registers.TargetReceiveData.Define(this)
                .WithTag("ReceiveData", 0, 8)
                .WithTag("ReceivedAddress", 8, 3)
                .WithReservedBits(11, 3)
                .WithTaggedFlag("ReceiveEmpty", 14)
                .WithTaggedFlag("StartOfFrame", 15)
                .WithReservedBits(16, 16)
            ;
        }

        private bool ReceiveDataFlag => rxQueue.Count > (int)rxWatermark.Value;

        private readonly Queue<byte> txQueue;
        private readonly Queue<byte> rxQueue;

        private II2CPeripheral activePeripheral;

        private IEnumRegisterField<MatchMode> matchConfiguration;

        private IValueRegisterField transmitData;
        private IValueRegisterField txWatermark;
        private IValueRegisterField rxWatermark;
        private IValueRegisterField match0Value;
        private IValueRegisterField match1Value;

        private IFlagRegisterField controllerEnabled;
        private IFlagRegisterField automaticSTOPGeneration;

        private IFlagRegisterField endPacketFlag;
        private IFlagRegisterField nackDetectFlag;
        private IFlagRegisterField stopDetectFlag;
        private IFlagRegisterField dataMatchFlag;

        private IFlagRegisterField transmitDataInterruptEnabled;
        private IFlagRegisterField receiveDataInterruptEnabled;
        private IFlagRegisterField endPacketInterruptEnabled;
        private IFlagRegisterField nackDetectInterruptEnabled;
        private IFlagRegisterField stopDetectInterruptEnabled;
        private IFlagRegisterField dataMatchInterruptEnabled;
        private IFlagRegisterField receiveDataMatchOnly;

        private enum MatchMode
        {
            Disabled,
            Reserved,
            FirstMatch0OrMatch1,
            AnyMatch0OrMatch1,
            FirstMatch0ThenMatch1,
            AnyMatch0ThenMatch1,
            FirstAndMatch1EqualMatch0AndMatch1,
            AnyAndMatch1EqualMatch0AndMatch1,
        }

        private enum Command
        {
            TransmitData,
            ReceiveData,
            GenerateSTOP,
            ReceiveDataAndDiscard,
            Start,
            StartExpectsNACK,
            HiSpeedStart,
            HiSpeedStartExpectsNACK
        }

        private enum Registers
        {
            VersionID = 0x0,
            Parameter = 0x4,
            ControllerControl = 0x10,
            ControllerStatus = 0x14,
            ControllerInterruptEnable = 0x18,
            ControllerDMAEnable = 0x1c,
            ControllerConfiguration0 = 0x20,
            ControllerConfiguration1 = 0x24,
            ControllerConfiguration2 = 0x28,
            ControllerConfiguration3 = 0x2C,
            ControllerDataMatch = 0x40,
            ControllerClockConfiguration0 = 0x48,
            ControllerClockConfiguration1 = 0x50,
            ControllerFIFOControl = 0x58,
            ControllerFIFOStatus = 0x5C,
            ControllerTransmitData = 0x60,
            ControllerReceiveData = 0x70,
            TargetControl = 0x110,
            TargetStatus = 0x114,
            TargetInterruptEnable = 0x118,
            TargetDMAEnable = 0x11C,
            TargetConfiguration1 = 0x124,
            TargetConfiguration2 = 0x128,
            TargetAddressMatch = 0x140,
            TargetAddressStatus = 0x150,
            TargetTransmitACK = 0x154,
            TargetTransmitData = 0x160,
            TargetReceiveData = 0x170,
        }
    }
}
