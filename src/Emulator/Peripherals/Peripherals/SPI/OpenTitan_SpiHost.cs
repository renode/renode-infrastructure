//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class OpenTitan_SpiHost: SimpleContainer<ISPIPeripheral>, IWordPeripheral, IBytePeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_SpiHost(IMachine machine, int numberOfCSLines) : base(machine)
        {
            this.numberOfCSLines = numberOfCSLines;

            DefineRegisters();
            Error = new GPIO();
            SpiEvent = new GPIO();
            FatalAlert = new GPIO();

            txFifo = new Queue<byte>();
            rxFifo = new Queue<byte>();
            cmdFifo = new Queue<CommandDefinition>();

            Reset();
        }

        public override void Reset()
        {
            txFifo.Clear();
            rxFifo.Clear();
            cmdFifo.Clear();
            selectedSlave = null;
            readyFlag.Value = true;
        }

        public uint ReadDoubleWord(long addr)
        {
            return RegistersCollection.Read(addr);
        }

        public void WriteDoubleWord(long addr, uint val)
        {
            if(addr == (long)Registers.Txdata)
            {
                EnqueueTx(val, sizeof(uint));
                return;
            }
            RegistersCollection.Write(addr, val);
        }

        // Below methods are meeded just to make sure we don't enqueue too much tx bytes on write.
        public ushort ReadWord(long offset)
        {
            return (ushort)RegistersCollection.Read(offset);
        }

        public void WriteWord(long addr, ushort val)
        {
            if(addr == (long)Registers.Txdata)
            {
                EnqueueTx(val, sizeof(ushort));
                return;
            }
            RegistersCollection.Write(addr, val);
        }

        public void WriteByte(long addr, byte val)
        {
            if(addr == (long)Registers.Txdata)
            {
                EnqueueTx(val, sizeof(byte));
                return;
            }
            RegistersCollection.Write(addr, val);
        }

        public byte ReadByte(long offset)
        {
            return (byte)RegistersCollection.Read(offset);
        }

        public long Size => 0x1000;

        // Common Interrupt Offsets
        public GPIO Error { get; }
        public GPIO SpiEvent { get; }

        // Alerts
        public GPIO FatalAlert { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; private set; }

        private void DefineRegisters()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {{(long)Registers.InterruptState, new DoubleWordRegister(this)
                .WithFlag(0, out errorInterruptTriggered, FieldMode.Read | FieldMode.WriteOneToClear, name: "error")
                .WithFlag(1, out spiEventInterruptTriggered, FieldMode.Read | FieldMode.WriteOneToClear, name: "spi_event")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            },
            {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                .WithFlag(0, out errorInterruptEnabled, name: "error")
                .WithFlag(1, out spiEventInterruptEnabled, name: "spi_event")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            },
            {(long)Registers.InterruptTest, new DoubleWordRegister(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) errorInterruptTriggered.Value = true; }, name: "error")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) spiEventInterruptTriggered.Value = true; }, name: "spi_event")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            },
            {(long)Registers.AlertTest, new DoubleWordRegister(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => {if(val) FatalAlert.Blink();}, name: "fatal_fault")
                .WithReservedBits(1, 31)
            },
            {(long)Registers.Control, new DoubleWordRegister(this, 0x7f)
                .WithValueField(0, 8, out rxWatermarkInDoublewords, name: "RX_WATERMARK")
                .WithValueField(8, 8, out txWatermarkInDoublewords, name: "TX_WATERMARK")
                .WithReservedBits(16, 13)
                .WithFlag(29, out outputEnabled, name: "OUTPUT_EN")
                .WithFlag(30, writeCallback: (_, val) => { if(val) Reset(); }, name: "SW_RST")
                .WithFlag(31, out enabled, name: "SPIEN")
                .WithChangeCallback((_, __) => 
                {
                    this.DebugLog("Control set to 'enabled': {0}, 'outputEnabled': {1}, rxWatermark = {2}[doublewords], txWatermark = {3}[doublewords]", enabled.Value, outputEnabled.Value, rxWatermarkInDoublewords.Value, txWatermarkInDoublewords.Value);
                    if(enabled.Value && outputEnabled.Value)
                    {
                        ExecuteCommands();
                    }
                })
            },
            {(long)Registers.Status, new DoubleWordRegister(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => txCountInDoubleWords, name: "TXQD")
                .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => rxCountInDoubleWords, name: "RXQD")
                .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => cmdCountInDoubleWords, name: "CMDQD")
                .WithFlag(20, out rxWatermarkEventTriggered, FieldMode.Read, name: "RXWM")
                .WithReservedBits(21, 1)
                .WithTaggedFlag("BYTEORDER", 22)
                .WithTaggedFlag("RXSTALL", 23)
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => rxFifo.Count == 0, name: "RXEMPTY")
                .WithFlag(25, out rxFullEventTriggered, FieldMode.Read, name: "RXFULL")
                .WithFlag(26, out txWatermarkEventTriggered, FieldMode.Read, name: "TXWM")
                .WithTaggedFlag("TXSTALL", 27)
                .WithFlag(28, out txEmptyEventTriggered, FieldMode.Read, name: "TXEMPTY")
                .WithFlag(29, FieldMode.Read, valueProviderCallback: _ => txFifo.Count == SpiHostTxDepth, name: "TXFULL")
                .WithFlag(30, out active, FieldMode.Read, name: "ACTIVE")
                .WithFlag(31, out readyFlag, FieldMode.Read, name: "READY")
            },
            {(long)Registers.Configopts, new DoubleWordRegister(this)
                .WithTag("CLKDIV_0", 0, 16)
                .WithTag("CSNIDLE_0", 16, 4)
                .WithTag("CSNTRAIL_0", 20, 4)
                .WithTag("CSNLEAD_0", 24, 4)
                .WithTaggedFlag("FULLCYC_0", 29)
                .WithTaggedFlag("CPHA_0", 30)
                .WithTaggedFlag("CPOL_0", 31)
            },
            {(long)Registers.ChipSelectID, new DoubleWordRegister(this)
                .WithValueField(0, 32, writeCallback: (_, val) =>
                    {
                        if((long)val >= numberOfCSLines)
                        {
                            this.Log(LogLevel.Warning, "Tried to set CS id out of range: {0}", val);
                            csIdInvalidErrorTriggered.Value = true;
                            UpdateEvents();
                            return;
                        }

                        if(!TryGetByAddress((int)val, out selectedSlave))
                        {
                            this.Log(LogLevel.Warning, "No device connected with the ID {0}", val);
                            return;
                        }

                        this.DebugLog("The device address set to {0}", val);
                    }
                , name: "CSID")
            },
            {(long)Registers.Command, new DoubleWordRegister(this)
                .WithValueField(0, 9, out var commandLength, FieldMode.Write, name: "LEN")
                .WithFlag(9, out var keepChipSelect, name: "CSAAT")
                .WithEnumField<DoubleWordRegister, CommandSpeed>(10, 2, out var commandSpeed, FieldMode.Write, name: "SPEED")
                .WithEnumField<DoubleWordRegister, CommandDirection>(12, 2, out var commandDirection, FieldMode.Write, name: "DIRECTION")
                .WithReservedBits(14, 18)
                .WithWriteCallback((_, __) => EnqueueCommand((uint)commandLength.Value, commandDirection.Value, commandSpeed.Value, keepChipSelect.Value))
            },
            {(long)Registers.Rxdata, new DoubleWordRegister(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => RxDequeueAsUInt(), name: "SPIReceiveData")
            },
            {(long)Registers.Txdata, new DoubleWordRegister(this)
                // This is already implemented in the WriteDoubleWord/WriteWord/WriteByte
                .WithTag("SPITransmitData", 0, 32)
            },
            {(long)Registers.ErrorEnable, new DoubleWordRegister(this, 0x1f)
                .WithTaggedFlag("CMDBUSY", 0)
                .WithFlag(1, out txOverflowErrorEnabled, name: "OVERFLOW")
                .WithFlag(2, out rxUnderflowErrorEnabled, name: "UNDERFLOW")
                .WithFlag(3, out commandInvalidErrorEnabled, name: "CMDINVAL")
                .WithFlag(4, out csIdInvalidErrorEnabled, name: "CSIDINVAL")
                .WithReservedBits(5, 27)
                .WithWriteCallback((_,__) => UpdateErrors())
            },
            {(long)Registers.ErrorStatus, new DoubleWordRegister(this)
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "CMDBUSY")
                .WithFlag(1, out txOverflowErrorTriggered, FieldMode.Read | FieldMode.WriteOneToClear, name: "OVERFLOW")
                .WithFlag(2, out rxUnderflowErrorTriggred, FieldMode.Read | FieldMode.WriteOneToClear, name: "UNDERFLOW")
                .WithFlag(3, out commandInvalidErrorTriggered, FieldMode.Read | FieldMode.WriteOneToClear, name: "CMDINVAL")
                .WithFlag(4, out csIdInvalidErrorTriggered, FieldMode.Read | FieldMode.WriteOneToClear, name: "CSIDINVAL")
                .WithTaggedFlag("ACCESSINVAL", 5)
                .WithReservedBits(6, 26)
                .WithWriteCallback((_,__) => UpdateErrors())
            },
            {(long)Registers.EventEnable, new DoubleWordRegister(this)
                .WithFlag(0, out rxFullEventEnabled, name: "RXFULL")
                .WithFlag(1, out txEmptyEventEnabled, name: "TXEMPTY")
                .WithFlag(2, out rxWatermarkEventEnabled, name: "RXWM")
                .WithFlag(3, out txWatermarkEventEnabled, name: "TXWM")
                .WithFlag(4, out readyEventEnabled, name: "READY")
                .WithFlag(5, out idleEventEnabled, name: "IDLE")
                .WithReservedBits(6, 26)
                .WithWriteCallback((_,__) => UpdateEvents())
            }};
            RegistersCollection = new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private void UpdateInterrupts()
        {
            Error.Set(errorInterruptEnabled.Value & errorInterruptTriggered.Value);
            SpiEvent.Set(spiEventInterruptEnabled.Value & spiEventInterruptTriggered.Value);
        }
        
        private void UpdateEvents()
        {
            spiEventInterruptTriggered.Value = 
                (rxFullEventTriggered.Value && rxFullEventEnabled.Value) || 
                (txEmptyEventTriggered.Value && txEmptyEventEnabled.Value) || 
                (txWatermarkEventTriggered.Value && txWatermarkEventEnabled.Value) || 
                (rxWatermarkEventTriggered.Value && rxWatermarkEventEnabled.Value) || 
                (readyFlag.Value && readyEventEnabled.Value) || 
                (!active.Value && idleEventEnabled.Value);

            UpdateInterrupts();
        }

        private void UpdateErrors()
        {
            // There should be also an CMDBUSY error, but there is no need as our peripheral will never be busy
            errorInterruptTriggered.Value = 
                (txOverflowErrorTriggered.Value && txOverflowErrorEnabled.Value) || 
                (rxUnderflowErrorTriggred.Value && rxUnderflowErrorEnabled.Value) || 
                (commandInvalidErrorTriggered.Value && commandInvalidErrorEnabled.Value) || 
                (csIdInvalidErrorTriggered.Value && csIdInvalidErrorEnabled.Value);

            UpdateInterrupts();
        }
        
        private void ExecuteCommands()
        {
            this.NoisyLog("Executing queued commands");
            while(cmdFifo.TryDequeue(out var x))
            {
                HandleCommand(x);
            }
            readyFlag.Value = true;
            active.Value = false;
            UpdateEvents();
        }

        private void EnqueueCommand(uint length, CommandDirection direction, CommandSpeed speed, bool keepChipSelect)
        {
            if((direction == CommandDirection.TxRx && speed != CommandSpeed.Standard) ||
                speed == CommandSpeed.Reserved)
            {
                commandInvalidErrorTriggered.Value = true;
                UpdateErrors();
                return;
            }

            cmdFifo.Enqueue(new CommandDefinition(length, direction, keepChipSelect));
            if(outputEnabled.Value && enabled.Value)
            {
                ExecuteCommands();
            }
        }
        
        private void EnqueueTx(uint val, int accessSize)
        {
            var asBytes = Misc.AsBytes(new uint[] { val });
            for(int i = 0; i < accessSize; i++)
            {
                if(txFifo.Count < SpiHostTxDepth)
                {
                    txFifo.Enqueue(asBytes[i]);
                }
                else
                {
                    txOverflowErrorTriggered.Value = true;
                    break;
                }
            }
            this.Log(LogLevel.Noisy, "Enqueued {0} Tx bytes, current fifo depth in words: {1}", accessSize, txCountInWords);

            txWatermarkEventTriggered.Value = (txCountInDoubleWords < txWatermarkInDoublewords.Value);
            UpdateEvents();
        }

        private void EnqueueRx(byte val)
        {
            if(rxFifo.Count < SpiHostRxDepth)
            {
                rxFifo.Enqueue(val);
            }
            else
            {
                rxFullEventTriggered.Value = true;
            }

            rxWatermarkEventTriggered.Value = (rxCountInDoubleWords >= rxWatermarkInDoublewords.Value);
            
            UpdateEvents();
        }

        private uint RxDequeueAsUInt()
        {
            uint output = 0;
            var count = Math.Min(sizeof(uint), rxFifo.Count);
            for(int i = 0; i < count; i++)
            {
                output |= (uint)(rxFifo.Dequeue() << (i * 8));
            }
            return output;
        }

        private void HandleCommand(CommandDefinition command)
        {
            if(command.Direction == CommandDirection.Dummy)
            {
                return;
            }

            // number of bytes to transfer is equal to `COMMAND.LEN + 1`
            for(var i = 0; i <= command.Length; i++)
            {
                var byteToTransfer = (byte)0;
                if(command.Direction == CommandDirection.TxOnly || command.Direction == CommandDirection.TxRx)
                {
                    if(!Misc.TryDequeue(txFifo, out byteToTransfer))
                    {
                        this.Log(LogLevel.Warning, "Tx Fifo empty, transmitting 0 instead");
                        byteToTransfer = 0;
                    }
                }

                byte response;
                if(selectedSlave != null)
                {
                    this.NoisyLog("Transferring byte {0:x}", byteToTransfer);
                    response = selectedSlave.Transmit(byteToTransfer);
                    this.NoisyLog("Received byte {0:x}", response);
                }
                else
                {
                    response = 0xff;
                    this.NoisyLog("No target device is available, returning dummy byte {0:x}", response);
                }

                if(command.Direction == CommandDirection.RxOnly || command.Direction == CommandDirection.TxRx)
                {
                    EnqueueRx(response);
                }
            }
            
            if(!command.KeepChipSelect)
            {
                this.Log(LogLevel.Debug, "Finished the transmission. Current fifo depth in words: {0}", rxCountInWords);
                selectedSlave?.FinishTransmission();
            }
        }

        private IFlagRegisterField errorInterruptEnabled;
        private IFlagRegisterField spiEventInterruptEnabled;
        private IFlagRegisterField errorInterruptTriggered;
        private IFlagRegisterField spiEventInterruptTriggered;

        private IFlagRegisterField readyFlag;
        private IFlagRegisterField active;
        private IFlagRegisterField enabled;
        private IFlagRegisterField outputEnabled;

        private IValueRegisterField txWatermarkInDoublewords;
        private IValueRegisterField rxWatermarkInDoublewords;

        private ISPIPeripheral selectedSlave;

        private IFlagRegisterField txEmptyEventTriggered;
        private IFlagRegisterField rxFullEventTriggered;
        private IFlagRegisterField txWatermarkEventTriggered;
        private IFlagRegisterField rxWatermarkEventTriggered;
        private IFlagRegisterField txOverflowErrorTriggered;
        private IFlagRegisterField rxUnderflowErrorTriggred;
        private IFlagRegisterField commandInvalidErrorTriggered;
        private IFlagRegisterField csIdInvalidErrorTriggered;
        private IFlagRegisterField rxFullEventEnabled;
        private IFlagRegisterField txEmptyEventEnabled;
        private IFlagRegisterField txWatermarkEventEnabled;
        private IFlagRegisterField rxWatermarkEventEnabled;
        private IFlagRegisterField readyEventEnabled;
        private IFlagRegisterField idleEventEnabled;
        private IFlagRegisterField txOverflowErrorEnabled;
        private IFlagRegisterField rxUnderflowErrorEnabled;
        private IFlagRegisterField commandInvalidErrorEnabled;
        private IFlagRegisterField csIdInvalidErrorEnabled;
        
        private readonly Queue<CommandDefinition> cmdFifo;
        private readonly Queue<byte> txFifo;
        private readonly Queue<byte> rxFifo;
        private readonly int numberOfCSLines;

        // The size of the Tx FIFO (in bytes)
        private const uint SpiHostTxDepth = 288;

        // The size of the Rx FIFO (in bytes)
        private const uint SpiHostRxDepth = 256;

        // The size of the Cmd FIFO (one segment descriptor per entry)
        private const uint SpiHostCmdDepth = 4;

        private uint txCountInWords => (uint)txFifo.Count / 2;
        private uint rxCountInWords => (uint)rxFifo.Count / 2;
        private uint cmdCountInWords => (uint)cmdFifo.Count / 2;

        private uint txCountInDoubleWords => (uint)txFifo.Count / 4;
        private uint rxCountInDoubleWords => (uint)rxFifo.Count / 4;
        private uint cmdCountInDoubleWords => (uint)cmdFifo.Count / 4;

        private enum CommandDirection
        {
            Dummy = 0,
            RxOnly = 1,
            TxOnly = 2,
            TxRx = 3,
        }
        
        private enum CommandSpeed
        {
            Standard = 0,
            Dual = 1, 
            Quad = 2,
            Reserved = 3,
        }

        public enum Registers
        {
            InterruptState = 0x0,
            InterruptEnable = 0x4,
            InterruptTest = 0x8,
            AlertTest = 0xc,
            Control = 0x10,
            Status = 0x14,
            Configopts = 0x18,
            ChipSelectID = 0x1c,
            Command = 0x20,
            Rxdata = 0x24,
            Txdata = 0x28,
            ErrorEnable = 0x2c,
            ErrorStatus = 0x30,
            EventEnable = 0x34,
        }
        
        private struct CommandDefinition
        {
            public CommandDefinition(uint length, CommandDirection direction, bool keepChipSelect)
            {
                this.Length = length;
                this.Direction = direction;
                this.KeepChipSelect = keepChipSelect;
            }
            
            public uint Length { get; }
            public CommandDirection Direction { get; }
            public bool KeepChipSelect { get; }
        }
    } // End class OpenTitan_SpiHost
} // End of namespace
