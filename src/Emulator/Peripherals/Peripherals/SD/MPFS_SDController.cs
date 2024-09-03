//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SD
{
    public class MPFS_SDController : NullRegistrationPointPeripheralContainer<SDCard>, IPeripheralContainer<IPhysicalLayer<byte>, NullRegistrationPoint>, IKnownSize, IDisposable,
        IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public MPFS_SDController(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            WakeupIRQ = new GPIO();
            sysbus = machine.GetSystemBus(this);
            irqManager = new InterruptManager<Interrupts>(this);
            internalBuffer = new Queue<byte>();

            RegistersCollection = new DoubleWordRegisterCollection(this);
            ByteRegistersCollection = new ByteRegisterCollection(this);
            InitializeRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            if(RegistersCollection.TryRead(offset, out var result))
            {
                return result;
            }

            return this.ReadDoubleWordUsingByte(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(RegistersCollection.TryWrite(offset, value))
            {
                return;
            }

            this.WriteDoubleWordUsingByte(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            if(ByteRegistersCollection.TryRead(offset, out byte low))
            {
                ushort high =  ByteRegistersCollection.Read(offset + 1);
                return (ushort) ((high << 8) + low);
            };

            return this.ReadWordUsingDoubleWord(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            if(ByteRegistersCollection.TryWrite(offset, (byte) value))
            {
                ByteRegistersCollection.Write(offset+1, (byte) (value >> 8));
                return;
            };

            this.WriteWordUsingDoubleWord(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return ByteRegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            ByteRegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            RegisteredPeripheral?.Reset();
            RegistersCollection.Reset();
            irqManager.Reset();
            internalBuffer.Clear();
            bytesRead = 0;
        }

        public void Dispose()
        {
            RegisteredPeripheral?.Dispose();
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(IPhysicalLayer<byte> peripheral)
        {
            return (peripheral == phy)
                ? new[] { NullRegistrationPoint.Instance }
                : new NullRegistrationPoint[0];
        }

        public void Register(IPhysicalLayer<byte> peripheral, NullRegistrationPoint registrationPoint)
        {
            if(phy != null)
            {
                throw new RegistrationException("There is already a PHY registered for this controller");
            }
            phy = peripheral;
        }

        public void Unregister(IPhysicalLayer<byte> peripheral)
        {
            if(phy != peripheral)
            {
                throw new RegistrationException("Trying to unregister PHY that is currently not registered in this controller");

            }
            phy = null;
        }

        [IrqProvider]
        public GPIO IRQ { get; private set; }

        public GPIO WakeupIRQ { get; private set; }

        public long Size => 0x2000;

        public DoubleWordRegisterCollection RegistersCollection { get; }
        public ByteRegisterCollection ByteRegistersCollection { get; }
        DoubleWordRegisterCollection IProvidesRegisterCollection<DoubleWordRegisterCollection>.RegistersCollection => RegistersCollection;
        ByteRegisterCollection IProvidesRegisterCollection<ByteRegisterCollection>.RegistersCollection => ByteRegistersCollection;

        IEnumerable<IRegistered<IPhysicalLayer<byte>, NullRegistrationPoint>> IPeripheralContainer<IPhysicalLayer<byte>, NullRegistrationPoint>.Children => phy != null
            ? new [] { Registered.Create(phy, NullRegistrationPoint.Instance) }
            : new IRegistered<IPhysicalLayer<byte>, NullRegistrationPoint>[0];

        private void InitializeRegisters()
        {
            var responseFields = new IValueRegisterField[4];

            Registers.PhySettings_HRS04.Define32(this)
                .WithValueField(0, 6, out addressField, name: "UHS-I Delay Address Pointer (USI_ADDR)")
                .WithReservedBits(6, 2)
                .WithValueField(8, 8, out writeDataField, name: "UHS-I Settings Write Data (UIS_WDATA)")
                .WithValueField(16, 5, out readDataField, FieldMode.Read, name: "UHS-I Settings Read Data (UIS_RDATA)")
                .WithReservedBits(21, 3)
                .WithFlag(24, out var writeRequestFlag, name: "UHS-I Settings Write Request (UIS_WR)")
                .WithFlag(25, out var readRequestFlag, name: "UHS-I Settings Read Request (UIS_RD)")
                .WithFlag(26, out ackField, FieldMode.Read, name: "UHS-I Settings Acknowledge (UIS_ACK)")
                .WithReservedBits(27, 5)
                .WithWriteCallback((_, value) =>
                {
                    // here we set ack field even when no PHY is registered
                    // this is intentional - the uboot test showed that this field
                    // might be necessary to proceed with booting even when actual
                    // reads/writes to PHY are ignored
                    ackField.Value = (writeRequestFlag.Value || readRequestFlag.Value);

                    if(readRequestFlag.Value)
                    {
                        readRequestFlag.Value = false;
                        if(phy == null)
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an unregistered PHY");
                        }
                        else
                        {
                            readDataField.Value = phy.Read((byte)addressField.Value);
                        }
                    }

                    if(writeRequestFlag.Value)
                    {
                        writeRequestFlag.Value = false;
                        if(phy == null)
                        {
                            this.Log(LogLevel.Warning, "Trying to write to an unregistered PHY");
                        }
                        else
                        {
                            phy.Write((byte)addressField.Value, (byte)writeDataField.Value);
                        }
                    }
                })
            ;

            Registers.BlockSizeBlockCount_SRS01.Define32(this)
                .WithValueField(0, 12, out blockSizeField, name: "Transfer Block Size (TBS)")
                .WithValueField(16, 16, out blockCountField, name: "Block Count For Current Transfer (BCCT)")
            ;

            Registers.Argument1_SRS02.Define32(this)
                .WithValueField(0, 32, out var commandArgument1Field, name: "Command Argument 1 (ARG1)")
            ;

            ByteRegisters.CommandTransferMode_SRS03_3.Define8(this)
                .WithEnumField(0, 6, out commandIndex, name: "Command Index (CI)")
                .WithReservedBits(6, 2)
                .WithWriteCallback((_, val) =>
                {
                    var sdCard = RegisteredPeripheral;
                    if(sdCard == null)
                    {
                        this.Log(LogLevel.Warning, "Tried to send a command, but no SD card is currently attached");
                        return;
                    }

                    var commandResult = sdCard.HandleCommand((uint)commandIndex.Value, (uint)commandArgument1Field.Value);
                    var responseType = responseTypeSelectField.Value;
                    switch(responseType)
                    {
                        case ResponseType.NoResponse:
                            if(commandResult.Length != 0)
                            {
                                this.Log(LogLevel.Warning, "Expected no response, but {0} bits received", commandResult.Length);
                                return;
                            }
                            break;
                        case ResponseType.Response136Bits:
                            // our response does not contain 8 bits:
                            // * start bit
                            // * transmission bit
                            // * command index / reserved bits (6 bits)
                            if(commandResult.Length != 128)
                            {
                                this.Log(LogLevel.Warning, "Unexpected a response of length 128 bits (excluding control bits), but {0} received", commandResult.Length);
                                return;
                            }
                            // the following bits are considered a part of returned register, but are not included in the response buffer:
                            // * CRC7 (7 bits)
                            // * end bit
                            // that's why we are skipping the initial 8-bits
                            responseFields[0].Value = commandResult.AsUInt32(8);
                            responseFields[1].Value = commandResult.AsUInt32(40);
                            responseFields[2].Value = commandResult.AsUInt32(72);
                            responseFields[3].Value = commandResult.AsUInt32(104, 24);
                            break;
                        case ResponseType.Response48Bits:
                        case ResponseType.Response48BitsWithBusy:
                            // our response does not contain 16 bits:
                            // * start bit
                            // * transmission bit
                            // * command index / reserved bits (6 bits)
                            // * CRC7 (7 bits)
                            // * end bit
                            if(commandResult.Length != 32)
                            {
                                this.Log(LogLevel.Warning, "Expected a response of length {0} bits (excluding control bits and CRC), but {1} received", 32, commandResult.Length);
                                return;
                            }
                            responseFields[0].Value = commandResult.AsUInt32();
                            break;
                        default:
                            this.Log(LogLevel.Warning, "Unexpected response type selected: {0}. Ignoring the command response.", responseTypeSelectField.Value);
                            return;
                    }

                    ProcessCommand(sdCard, commandIndex.Value);

                    irqManager.SetInterrupt(Interrupts.BufferWriteReady, irqManager.IsEnabled(Interrupts.BufferWriteReady));
                    irqManager.SetInterrupt(Interrupts.BufferReadReady, irqManager.IsEnabled(Interrupts.BufferReadReady));
                    irqManager.SetInterrupt(Interrupts.CommandComplete, irqManager.IsEnabled(Interrupts.CommandComplete));
                    if(responseType == ResponseType.Response48BitsWithBusy)
                    {
                        irqManager.SetInterrupt(Interrupts.TransferComplete, irqManager.IsEnabled(Interrupts.TransferComplete));
                    }
                })
            ;

            ByteRegisters.CommandTransferMode_SRS03_2.Define8(this)
                .WithEnumField(0, 2, out responseTypeSelectField, name: "Response Type Select (RTS)")
                .WithReservedBits(2, 1)
                .WithTag("Command CRC Check Enable (CRCCE)", 3, 1)
                .WithTag("Command Index Check Enable (CICE)", 4, 1)
                .WithEnumField(5, 1, out dataPresentSelect, name: "Data Present Select (DPS)")
                .WithTag("Command Type (CT)", 6, 2)
            ;

            ByteRegisters.CommandTransferMode_SRS03_1.Define8(this)
                .WithTag("Response Interrupt Disable (RID)", 0, 1)
                .WithReservedBits(1, 7)
            ;

            ByteRegisters.CommandTransferMode_SRS03_0.Define8(this)
                .WithFlag(0, out isDmaEnabled, name: "DMA Enable")
                .WithTag("Block Count Enable (BCE)", 1, 1)
                .WithTag("Auto CMD Enable (ACE)", 2, 2)
                .WithEnumField(4, 1, out dataTransferDirectionSelect, name: "Data Transfer Direction Select (DTDS)")
                .WithTag("Multi/Single Block Select (MSBS)", 5, 1)
                .WithTag("Response Type R1/R5 (RECT)", 6, 1)
                .WithTag("Response Error Check Enable (RECE)", 7, 1)
            ;

            Registers.Response1_SRS04.Define32(this)
                .WithValueField(0, 32, out responseFields[0], FieldMode.Read, name: "Response Register #1 (RESP1)")
            ;

            Registers.Response2_SRS05.Define32(this)
                .WithValueField(0, 32, out responseFields[1], FieldMode.Read, name: "Response Register #2 (RESP2)")
            ;

            Registers.Response3_SRS06.Define32(this)
                .WithValueField(0, 32, out responseFields[2], FieldMode.Read, name: "Response Register #3 (RESP3)")
            ;

            Registers.Response4_SRS07.Define32(this)
                .WithValueField(0, 32, out responseFields[3], FieldMode.Read, name: "Response Register #4 (RESP4)")
            ;

            Registers.DataBuffer_SRS08.Define32(this).WithValueField(0, 32, name: "Buffer Data Port (BDP)",
                valueProviderCallback: _ =>
                {
                    var sdCard = RegisteredPeripheral;
                    if(sdCard == null)
                    {
                        this.Log(LogLevel.Warning, "Tried to read data, but no SD card is currently attached");
                        return 0;
                    }
                    if(isDmaEnabled.Value)
                    {
                        this.Log(LogLevel.Debug, "Reading data in DMA mode from register that does not support it");
                    }
                    if(!internalBuffer.Any())
                    {
                        return 0;
                    }
                    return ReadBuffer();
                },
                writeCallback: (_, value) =>
                {
                    var sdCard = RegisteredPeripheral;
                    if(sdCard == null)
                    {
                        this.Log(LogLevel.Warning, "Tried to write data, but no SD card is currently attached");
                        return;
                    }
                    if(isDmaEnabled.Value)
                    {
                        this.Log(LogLevel.Warning, "Tried to write data in DMA mode to register that does not support it");
                        return;
                    }
                    WriteBuffer(sdCard, BitConverter.GetBytes((uint)value));
                })
            ;

            Registers.PresentState_SRS09.Define32(this)
                .WithFlag(0, FieldMode.Read, name: "Command Inhibit CMD (CICMD)")
                .WithFlag(1, FieldMode.Read, name: "Command Inhibit DAT (CIDAT)") // as sending a command is instantienous those two bits will probably always be 0
                                                                                  // ...
                .WithFlag(10, FieldMode.Read, name: "Buffer Write Enable (BWE)", valueProviderCallback: _ => true)
                .WithFlag(11, FieldMode.Read, name: "Buffer Read Enable (BRE)", valueProviderCallback: _ => RegisteredPeripheral == null ? false : internalBuffer.Any())
                .WithFlag(16, FieldMode.Read, name: "Card Inserted (CI)", valueProviderCallback: _ => RegisteredPeripheral != null)
                .WithFlag(17, FieldMode.Read, name: "Card State Stable (CSS)", valueProviderCallback: _ => true)
                .WithFlag(18, FieldMode.Read, name: "Card Detect Pin Level (CDSL)", valueProviderCallback: _ => RegisteredPeripheral != null)
                .WithFlag(20, FieldMode.Read, name: "Line Signal Level (DATSL1 - DAT[0])", valueProviderCallback: _ => true)
                .WithFlag(21, FieldMode.Read, name: "Line Signal Level (DATSL1 - DAT[1])", valueProviderCallback: _ => true)
                .WithFlag(22, FieldMode.Read, name: "Line Signal Level (DATSL1 - DAT[2])", valueProviderCallback: _ => true)
                .WithFlag(23, FieldMode.Read, name: "Line Signal Level (DATSL1 - DAT[3])", valueProviderCallback: _ => true)
            ;

            Registers.HostControl2_SRS11.Define32(this)
                .WithFlag(1, FieldMode.Read, name: "Internal Clock Stable (ICS)", valueProviderCallback: _ => true)
                .WithTag("Software Reset For CMD Line (SRCMD)", 25, 1)
                .WithTag("Software Reset For DAT Line (SRDAT)", 26, 1)
            ;

            Registers.ErrorNormalInterruptStatus_SRS12.Bind(this, irqManager.GetRegister<DoubleWordRegister>(
                valueProviderCallback: (irq, _) => irqManager.IsSet(irq),
                writeCallback: (irq, prev, curr) => { if(curr) irqManager.ClearInterrupt(irq); } ))
            ;

            Registers.ErrorNormalStatusEnable_SRS13.Bind(this, irqManager.GetRegister<DoubleWordRegister>(
                valueProviderCallback: (irq, _) => irqManager.IsEnabled(irq),
                writeCallback: (irq, _, curr) =>
                {
                    if(curr)
                    {
                        irqManager.EnableInterrupt(irq, curr);
                    }
                    else
                    {
                        irqManager.DisableInterrupt(irq);
                    }
                }))
            ;

            Registers.Capabilities1_SRS16.Define32(this)
                // these fields must return non-zero values in order for u-boot to boot
                .WithValueField(0, 6, FieldMode.Read, valueProviderCallback: _ => 4, name: "Timeout clock frequency (TCS)")
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => true, name: "Timeout clock unit (TCU)")
                .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => 1, name: "Base Clock Frequency For SD Clock (BCSDCLK)")
                .WithFlag(19, FieldMode.Read, valueProviderCallback: _ => false, name: "ADMA1 Support")
                .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => true, name: "SDMA Support")
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => true, name: "Voltage Support 3.3V (VS33)")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => true, name: "Voltage Support 3.0V (VS30)")
                .WithFlag(26, FieldMode.Read, valueProviderCallback: _ => true, name: "Voltage Support 1.8V (VS18)")
                .WithFlag(28, FieldMode.Read, valueProviderCallback: _ => true, name: "64-bit DMA Support")
            ;
            Registers.SDMASystemAddressArgument2_SRS00.Define32(this)
                .WithValueField(0, 32, name: "SDMA Address",
                    valueProviderCallback: _ =>
                    {
                        if(dmaSystemAddressHigh.Value != 0)
                        {
                            this.Log(LogLevel.Warning, "DMA System address 2 is nonzero: {0} ", dmaSystemAddressHigh.Value);
                        }
                        return dmaSystemAddressLow.Value;
                    },
                    writeCallback: (_, val) =>
                    {
                        dmaSystemAddressHigh.Value = 0;
                        dmaSystemAddressLow.Value = val;
                    }
                )

            ;

            Registers.DmaSystemAddressLow_SRS22.Define32(this)
                .WithValueField(0, 32, out dmaSystemAddressLow, name: "ADMA/SDMA System Address 1")
            ;

            Registers.DmaSystemAddressHigh_SRS23.Define32(this)
                .WithValueField(0, 32, out dmaSystemAddressHigh, name: "ADMA/SDMA System Address 2")
            ;
        }

        private void ProcessCommand(SDCard sdCard, SDCardCommand command)
        {
            switch(command)
            {
                case SDCardCommand.CheckSwitchableFunction:
                    internalBuffer.EnqueueRange(sdCard.ReadSwitchFunctionStatusRegister());
                    return;
                case SDCardCommand.SendInterfaceConditionCommand:
                    internalBuffer.EnqueueRange(sdCard.ReadExtendedCardSpecificDataRegister());
                    return;
            }

            // early exit if no data is present
            if(dataPresentSelect.Value != DataPresentSelect.DataPresent)
            {
                return;
            }

            var bytesCount = (uint)(blockCountField.Value * blockSizeField.Value);
            switch(dataTransferDirectionSelect.Value)
            {
                case DataTransferDirectionSelect.Read:
                    ReadCard(sdCard, bytesCount);
                    break;
                case DataTransferDirectionSelect.Write:
                    WriteCard(sdCard, bytesCount);
                    break;
                default:
                    this.Log(LogLevel.Warning, "Invalid data transfer direction {};", dataTransferDirectionSelect.Value);
                    break;
            }
        }

        private void ReadCard(SDCard sdCard, uint size)
        {
            var data = sdCard.ReadData(size);
            if(isDmaEnabled.Value)
            {
                internalBuffer.Clear();
                bytesRead = 0;
                sysbus.WriteBytes(data, ((ulong)dmaSystemAddressHigh.Value << 32) | dmaSystemAddressLow.Value);
                Machine.LocalTimeSource.ExecuteInNearestSyncedState(_ =>
                {
                    irqManager.SetInterrupt(Interrupts.TransferComplete, irqManager.IsEnabled(Interrupts.TransferComplete));
                });
            }
            internalBuffer.EnqueueRange(data);
        }

        private void WriteBuffer(SDCard sdCard, byte[] data)
        {
            var limit = (uint)(blockCountField.Value * blockSizeField.Value);
            internalBuffer.EnqueueRange(data);
            if(internalBuffer.Count < limit)
            {
                return;
            }
            sdCard.WriteData(internalBuffer.DequeueAll());
            irqManager.SetInterrupt(Interrupts.TransferComplete, irqManager.IsEnabled(Interrupts.TransferComplete));
        }

        private void WriteCard(SDCard sdCard, uint size)
        {
            var bytes = new byte[size];
            if(isDmaEnabled.Value)
            {
                bytes = sysbus.ReadBytes(((ulong)dmaSystemAddressHigh.Value << 32) | dmaSystemAddressLow.Value, (int)size);
            }
            else
            {
                if(internalBuffer.Count < size)
                {
                    this.Log(LogLevel.Warning, "Could not write {0} bytes to SD card, writing {1} bytes instead.", size, internalBuffer.Count);
                    size = (uint)internalBuffer.Count;
                }
                bytes = internalBuffer.DequeueRange((int)size);
            }
            sdCard.WriteData(bytes);
            Machine.LocalTimeSource.ExecuteInNearestSyncedState(_ =>
            {
                irqManager.SetInterrupt(Interrupts.TransferComplete, irqManager.IsEnabled(Interrupts.TransferComplete));
            });
        }

        private uint ReadBuffer()
        {
            var internalBytes = internalBuffer.DequeueRange(4);
            bytesRead += (uint)internalBytes.Length;
            irqManager.SetInterrupt(Interrupts.BufferReadReady, irqManager.IsEnabled(Interrupts.BufferReadReady));
            if(bytesRead >= (blockCountField.Value * blockSizeField.Value) || !internalBuffer.Any())
            {
                irqManager.SetInterrupt(Interrupts.TransferComplete, irqManager.IsEnabled(Interrupts.TransferComplete));
                bytesRead = 0;
                // If we have read the exact amount of data we wanted, we can clear the buffer from any leftovers.
                internalBuffer.Clear();
            }
            return internalBytes.ToUInt32Smart();
        }

        private IFlagRegisterField ackField;
        private IFlagRegisterField isDmaEnabled;
        private IValueRegisterField blockSizeField;
        private IValueRegisterField blockCountField;
        private IValueRegisterField addressField;
        private IValueRegisterField readDataField;
        private IValueRegisterField writeDataField;
        private IValueRegisterField dmaSystemAddressLow;
        private IValueRegisterField dmaSystemAddressHigh;
        private IEnumRegisterField<DataPresentSelect> dataPresentSelect;
        private IEnumRegisterField<DataTransferDirectionSelect> dataTransferDirectionSelect;
        private IEnumRegisterField<SDCardCommand> commandIndex;
        private IEnumRegisterField<ResponseType> responseTypeSelectField;

        private uint bytesRead;
        private IPhysicalLayer<byte> phy;
        private Queue<byte> internalBuffer;

        private readonly IBusController sysbus;
        private readonly InterruptManager<Interrupts> irqManager;

        private enum Registers
        {
            GeneralInformation_HRS00 = 0x000,
            DebounceSetting_HRS01 = 0x004,
            BusSetting_HRS02 = 0x008,
            AXIEErrorResponses_HRS03 = 0x00C,
            PhySettings_HRS04 = 0x10,
            EMMCControl_HRS06 = 0x18,
            IODelayInformation_HRS07 = 0x01C,
            HostCapability_HRS30 = 0x078,
            HostControllerVersion_HRS31 = 0x07C,
            FSMMonitor_HRS32 = 0x080,
            TuneStatus0_HRS33 = 0x084,
            TuneStatus1_HRS34 = 0x088,
            TuneDebug_HRS35 = 0x08C,
            BootStatus_HRS36 = 0x090,
            ReadBlockGapCoefficientInterfaceModeSelect_HRS37 = 0x094,
            ReadBlockGapCoefficient_HRS38 = 0x098,
            HostControllerVersion_SlotInterruptStatus_CRS63 = 0x0FC,
            SDMASystemAddressArgument2_SRS00 = 0x200,
            BlockSizeBlockCount_SRS01 = 0x204,
            Argument1_SRS02 = 0x208,
            CommandTransferMode_SRS03 = 0x20C,
            Response1_SRS04 = 0x210,
            Response2_SRS05 = 0x214,
            Response3_SRS06 = 0x218,
            Response4_SRS07 = 0x21C,
            DataBuffer_SRS08 = 0x220,
            PresentState_SRS09 = 0x224,
            HostControl1_SRS10 = 0x228,
            HostControl2_SRS11 = 0x22C,
            ErrorNormalInterruptStatus_SRS12 = 0x230,
            ErrorNormalStatusEnable_SRS13 = 0x234,
            ErrorNormalSignalEnable_SRS14 = 0x238,
            HostControl2_SRS15 = 0x23C,
            Capabilities1_SRS16 = 0x240,
            Capabilities2_SRS17 = 0x244,
            Capabilities3_SRS18 = 0x248,
            Capabilities4_SRS19 = 0x24C,
            ForceEvent_SRS20 = 0x250,
            ADMAErrorStatus_SRS21 = 0x254,
            DmaSystemAddressLow_SRS22 = 0x258,
            DmaSystemAddressHigh_SRS23 = 0x25C,
            PresetValue_DefaultSpeed_SRS24 = 0x260,
            PresetValue_HighSpeedAndSDR12_SRS25 = 0x264,
            PresetValue_SDR25AndSDR50_SRS26 = 0x268,
            PresetValue_SDR104AndDDR50_SRS27 = 0x26C,
            PresetValue_UHSII_SRS29 = 0x274,
            CommandQueuingVersion_CQRS00 = 0x400,
            CommandQueuingCapabilities_CQRS01 = 0x404,
            CommandQueuingConfiguration_CQRS02 = 0x408,
            CommandQueuingControl_CQRS03 = 0x40C,
            CommandQueuingInterruptStatus_CQRS04 = 0x410,
            CommandQueuingInterruptStatusEnable_CQRS05 = 0x414,
            CommandQueuingInterruptSignalEnable_CQRS06 = 0x418,
            InterruptCoalescing_CQRS07 = 0x41C,
            CommandQueuingTaskDescriptorListBaseAddress_CQRS08 = 0x420,
            CommandQueuingTaskDescriptorListBaseAddressUpper32Bits_CQRS09 = 0x424,
            CommandQueuingTaskDoorbell_CQRS10 = 0x428,
            TaskCompleteNotification_CQRS11 = 0x42C,
            DeviceQueueStatus_CQRS12 = 0x430,
            DevicePendingTasks_CQRS13 = 0x434,
            TaskClear_CQRS14 = 0x438,
            SendStatusConfiguration1_CQRS16 = 0x440,
            SendStatusConfiguration2_CQRS17 = 0x444,
            CommandResponseForDirect_CommandTask_CQRS18 = 0x448,
            ResponseModeErrorMask_CQRS20 = 0x450,
            TaskErrorInformation_CQRS21 = 0x454,
            CommandResponseIndex_CQRS22 = 0x458,
            CommandResponseArgument_CQRS23 = 0x45C
        }

        private enum ByteRegisters {
            CommandTransferMode_SRS03_0 = 0x20C,
            CommandTransferMode_SRS03_1 = 0x20D,
            CommandTransferMode_SRS03_2 = 0x20E,
            CommandTransferMode_SRS03_3 = 0x20F
        }

        private enum DataPresentSelect
        {
            NoDataPresent = 0,
            DataPresent = 1
        }

        private enum DataTransferDirectionSelect
        {
            Write = 0,
            Read = 1
        }

        private enum ResponseType
        {
            NoResponse = 0,
            Response136Bits = 1,
            Response48Bits = 2,
            Response48BitsWithBusy = 3
        }

        private enum Interrupts
        {
            CommandComplete = 0,
            TransferComplete = 1,
            BlockGapEvent = 2,
            DMAInterrupt = 3,
            BufferWriteReady = 4,
            BufferReadReady = 5,
            CardIsertion = 6,
            CardRemoval = 7,
            CardInterrupt = 8,
            // [13:9] Reserved
            QueuingEnabledInterrupt = 14,
            ErrorInterrupt = 15,
            CommandTimeoutError = 16,
            CommandCRCError = 17,
            CommandEndBitError = 18,
            CommandIndexError = 19,
            DataTimeoutError = 20,
            DataCRCError = 21,
            DataEndBitError = 22,
            CurrentLimitError = 23,
            AutoCMDError = 24,
            ADMAError = 25,
            // [26] Reserved
            ResponseError = 27,
            // [31:28] Reserved
        }

        private enum SDCardCommand
        {
            CheckSwitchableFunction = 6,
            SendInterfaceConditionCommand = 8,
            ReadSingleBlock = 17,
            ReadMultipleBlocks = 18,
            WriteSingleBlock = 24,
            WriteMultipleBlocks = 25,
        }
    }
}
