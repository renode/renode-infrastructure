//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Sound;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sound
{
    public class PULP_I2S: BasicDoubleWordPeripheral, IDisposable, IKnownSize, INumberedGPIOOutput
    {
        public PULP_I2S(IMachine machine) : base(machine)
        {
            CreateRegisters();
            var irqs = new Dictionary<int, IGPIO>();
            irqs[(int)Events.Rx] = new GPIO();
            irqs[(int)Events.Tx] = new GPIO();
            irqs[(int)Events.Extra] = new GPIO();
            Connections = new ReadOnlyDictionary<int, IGPIO>(irqs);
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            decoder?.Reset();
            encoder?.FlushBuffer();
            
            rxThread = null;
            txThread = null;
            rxChannels = 1;
            txChannels = 1;
            rxSampleWidth = 16;
            txSampleWidth = 16;

            RxSampleFrequency = 0;
            TxSampleFrequency = 0;

            InputFile = "";
            OutputFile = "";
        }

        public void Dispose()
        {
            encoder?.Dispose();
        }

        public string InputFile { get; set; }
        public string OutputFile { get; set; }
        public uint RxSampleFrequency { get; set; }
        public uint TxSampleFrequency { get; set; }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x80;

        private void StartRx()
        {
            // All three flags must be set to start the acquisition, as they are stored in 3 separate registers this function will be called two times before we are ready to start
            if(rxEnabled.Value == false || slaveEnabled.Value == false || slaveClockEnabled.Value == false)
            {
                this.Log(LogLevel.Debug, 
                         @"Reception has not been started - it needs I2S_RX_CFG.ENABLE, I2S_SLV_SETUP.SLAVE_EN and I2S_CLKCFG_SETUP.SLAVE_CLK_EN to be set.
                         Current state: I2S_RX_CFG.ENABLE             = {0}, 
                                        I2S_SLV_SETUP.SLAVE_EN        = {1},
                                        I2S_CLKCFG_SETUP.SLAVE_CLK_EN = {2}",
                         rxEnabled.Value, slaveEnabled.Value, slaveClockEnabled.Value);
                return;
            }

            if(InputFile == "")
            {
                this.Log(LogLevel.Error, "Starting reception without an input file! Aborting");
                return;
            }
            decoder = new PCMDecoder(rxSampleWidth, RxSampleFrequency, rxChannels, false, this);
            decoder.LoadFile(InputFile);
            
            this.Log(LogLevel.Debug, "Starting reception");
            if(rxContinuous.Value)
            {
                if(RxSampleFrequency == 0)
                {
                    this.Log(LogLevel.Error, "Sampling frequency not set. Aborting continuous reception");
                    return;
                }
                rxThread = machine.ObtainManagedThread(InputFrames, RxSampleFrequency);
                rxThread.Start();
            }
            else
            {
                InputFrames();
            }
        }

        private void StartTx()
        {
            // All three flags must be set to start the transmission, as they are stored in 3 separate registers this function will be called two times before we are ready to start
            if(txEnabled.Value == false || masterEnabled.Value == false || masterClockEnabled.Value == false)
            {
                this.Log(LogLevel.Debug, 
                         @"Transmission has not been started - it needs I2S_TX_CFG.ENABLE, I2S_MST_SETUP.MASTER_EN and I2S_CLKCFG_SETUP.MASTER_CLK_EN to be set.
                         Current state: I2S_TX_CFG.ENABLE              = {0}, 
                                        I2S_MST_SETUP.MASTER_EN        = {1}, 
                                        I2S_CLKCFG_SETUP.MASTER_CLK_EN = {2}",
                         txEnabled.Value, masterEnabled.Value, masterClockEnabled.Value);
                return;
            }

            if(OutputFile == "")
            {
                this.Log(LogLevel.Error, "Starting reception without an output file! Aborting");
                return;
            }

            encoder = new PCMEncoder(txSampleWidth, TxSampleFrequency, txChannels, false);
            // Write samples after reading whole buffer, rather than performing a write after receiving every single one
            encoder.SetBufferingBySamplesCount((uint)txBufferSize.Value / (txSampleWidth / 8));
            encoder.Output = OutputFile;

            this.Log(LogLevel.Debug, "Starting transmission");
            if(txContinuous.Value)
            {
                if(TxSampleFrequency == 0)
                {
                    this.Log(LogLevel.Error, "Sampling frequency not set. Aborting continuous transmission");
                    return;
                }
                txThread = machine.ObtainManagedThread(OutputFrames, TxSampleFrequency);
                txThread.Start();
            }
            else
            {
                OutputFrames();
            }
        }

        private void StopThread(ref IManagedThread thread)
        {
            thread?.Stop();
            thread = null;
        }

        private void OutputFrames()
        {
            var bufferPointerBackup = txBufferPointer.Value;
            var bufferSizeBackup = txBufferSize.Value;

            var bufferStep = txSampleWidth / 8;
            uint sample;
            while(txBufferSize.Value > 0)
            {
                if(txBufferSize.Value < bufferStep)
                {
                    this.Log(LogLevel.Warning, "The I2S_TX_SIZE ({0} bytes) is misaligned to I2S_TX_CFG.DATASIZE ({1} bytes). This model will read bits form outside of the buffer, expect that your program will fail soon.",
                             bufferSizeBackup, bufferStep);
                }
                    
                switch(txSampleWidth)
                {
                    case 8:
                        sample = sysbus.ReadByte(txBufferPointer.Value);
                        break;
                    case 16:
                        sample = sysbus.ReadWord(txBufferPointer.Value);
                        break;
                    case 32:
                        sample = sysbus.ReadDoubleWord(txBufferPointer.Value);
                        break;
                    default:
                        throw new ArgumentException(String.Format("Invalid TX sample width: {}", txSampleWidth));
                }

                encoder.AcceptSample(sample);
                // In case of some interrupt with higher priority we make sure we have buffer pointer and remaining buffer size updated
                txBufferPointer.Value += bufferStep;
                txBufferSize.Value -= bufferStep;
            }

            Connections[(int)Events.Tx].Blink();
            //  At the end of the buffer the uDMA reloads the address and size and starts a new transfer
            if(txContinuous.Value)
            {
                txBufferPointer.Value = bufferPointerBackup;
                txBufferSize.Value = bufferSizeBackup;
            }
        }

        private void InputFrames()
        {
            var bufferPointerBackup = rxBufferPointer.Value;
            var bufferSizeBackup = rxBufferSize.Value;

            var samplesPerDoubleWord = 32 / rxSampleWidth;

            while(rxBufferSize.Value > 0)
            {
                uint temp = 0;
                for(int i = 0; i < samplesPerDoubleWord; i++)
                {
                    temp |= decoder.GetSingleSample() << (int)(rxSampleWidth * i);
                }

                if(rxBufferSize.Value < 4)
                {
                    // Handle buffer unaligned to double word
                    var bitsLeft = (int)(8 * rxBufferSize.Value);
                    BitHelper.ReplaceBits(ref temp, sysbus.ReadDoubleWord(rxBufferPointer.Value), 32 - bitsLeft, bitsLeft, bitsLeft);
                }
                sysbus.WriteDoubleWord(rxBufferPointer.Value, temp);
                // In case of some interrupt with higher priority we make sure we have buffer pointer and remaining buffer size updated
                rxBufferPointer.Value += 4;
                rxBufferSize.Value -= 4;
             }

            Connections[(int)Events.Rx].Blink();
            //  At the end of the buffer the uDMA reloads the address and size and starts a new transfer
            if(rxContinuous.Value)
            {
                rxBufferPointer.Value = bufferPointerBackup;
                rxBufferSize.Value = bufferSizeBackup;
            }
        }

        private void CreateRegisters()
        {
            Registers.RxBufferPointer.Define(this)
                .WithValueField(0, 16, out rxBufferPointer, name: "RX_SADDR")
                .WithReservedBits(16, 16);
            Registers.RxBufferSize.Define(this)
                .WithValueField(0, 17, out rxBufferSize, name: "RX_SIZE")
                .WithReservedBits(17, 15);
            Registers.RxConfig.Define(this, 0x4)
                .WithFlag(0, out rxContinuous, name: "CONTINOUS")
                .WithValueField(1, 2, out rxDataSize,
                    writeCallback: (_, val) => {
                        // b00 (8 bits)
                        // b01 (16 bits)
                        // b10 (32 bits)
                        if(val > 2)
                        {
                            this.Log(LogLevel.Warning, "Trying to set forbidden RX DataSize. Setting to default");
                            val = 0b10;
                        }
                        rxSampleWidth = (uint)(8 << (int)val);
                    },
                    name: "DATASIZE")
                .WithReservedBits(3, 1)
                .WithFlag(4, out rxEnabled,
                    writeCallback: (_, val) => { if(val) StartRx(); },
                    name: "EN")
                //The queue flag is not implemented as transfer is completed instantly
                .WithFlag(5,
                    writeCallback: (_, val) => { if(val) StopThread(ref rxThread); },
                    name: "CLR/PENDING")
                .WithReservedBits(6, 26);
            Registers.RxInit.Define(this)
                // The documentation defines no fields in this register 
                .WithReservedBits(0, 32);
            Registers.TxBufferPointer.Define(this)
                .WithValueField(0, 16, out txBufferPointer, name: "TX_SADDR")
                .WithReservedBits(16, 16);
            Registers.TxBufferSize.Define(this)
                .WithValueField(0, 17, out txBufferSize, name: "TX_SIZE")
                .WithReservedBits(17, 15);
            Registers.TxConfig.Define(this, 0x4)
                .WithFlag(0, out txContinuous, name: "CONTINOUS")
                .WithValueField(1, 2, out txDataSize,
                    writeCallback: (_, val) => {
                        // b00 (8 bits)
                        // b01 (16 bits)
                        // b10 (32 bits)
                        if(val > 2)
                        {
                            this.Log(LogLevel.Warning, "Trying to set forbidden TX DataSize. Setting to default");
                            val = 0b10;
                        }
                    txSampleWidth = (uint)(8 << (int)val);
                    }, name: "DATASIZE")
                .WithReservedBits(3, 1)
                .WithFlag(4, out txEnabled,
                    writeCallback: (_, val) => { if(val) StartTx(); },
                    name: "EN")
                .WithFlag(5,
                    writeCallback: (_, val) => { if(val) StopThread(ref txThread); },
                    name: "CLR/PENDING")
                .WithReservedBits(6, 26);
            Registers.TxInit.Define(this)
                // The documentation defines no fields in this register 
                .WithReservedBits(0, 32);
            Registers.ClockConfiguration.Define(this)
                .WithTag("MASTER_CLK_DIV", 0, 8)
                .WithTag("SLAVE_CLK_DIV", 8, 8)
                .WithTag("COMMON_CLK_DIV", 16, 8)
                .WithFlag(24, out slaveClockEnabled, writeCallback: (_, val) => { if(val) StartRx(); }, name: "SLAVE_CLK_EN")
                .WithFlag(25, out masterClockEnabled, writeCallback: (_, val) => { if(val) StartTx(); }, name: "MASTER_CLK_EN")
                .WithFlag(26, out pdmClockEnabled, name: "PDM_CLK_EN")
                .WithTag("SLAVE_EXT", 28, 1)
                .WithTag("SLAVE_NUM", 29, 1)
                .WithTag("MASTER_EXT", 30, 1)
                .WithTag("MASTER_NUM", 31, 1);
            Registers.SlaveSettings.Define(this)
                .WithTag("SLAVE_WORDS", 0, 3)
                .WithReservedBits(3, 5)
                .WithTag("SLAVE_BITS", 8, 5)
                .WithReservedBits(13, 3)
                .WithTag("SLAVE_LSB", 16, 1)
                .WithFlag(17, writeCallback: (_, val) => { rxChannels = val ? 2u : 1u; }, name: "SLAVE_2CH")
                .WithReservedBits(18, 13)
                .WithFlag(31, out slaveEnabled, writeCallback: (_, val) => { if(val) StartRx(); }, name: "SLAVE_EN");
            Registers.MasterSettings.Define(this)
                .WithTag("MASTER_WORDS", 0, 3)
                .WithReservedBits(3, 5)
                .WithTag("MASTER_BITS", 8, 5)
                .WithReservedBits(13, 3)
                .WithTag("MASTER_LSB", 16, 1)
                .WithFlag(17, writeCallback: (_, val) => { txChannels = val ? 2u : 1u; }, name: "MASTER_2CH")
                .WithReservedBits(18, 13)
                .WithFlag(31, out masterEnabled, writeCallback: (_, val) => { if(val) StartTx(); }, name:"MASTER_EN");
            Registers.PdmConfig.Define(this)
                .WithTag("PDM_SHIFT", 0, 3)
                .WithTag("PDM_DECIMATION", 3, 10)
                .WithTag("PDM_MODE", 13, 2)
                .WithReservedBits(15, 16)
                .WithTag("PDM_EN", 31, 1);
        }

        private IFlagRegisterField masterClockEnabled;
        private IFlagRegisterField masterEnabled;
        private IFlagRegisterField pdmClockEnabled;
        private IFlagRegisterField slaveClockEnabled;
        private IFlagRegisterField slaveEnabled;
        private IFlagRegisterField rxContinuous;
        private IFlagRegisterField rxEnabled;
        private IFlagRegisterField txContinuous;
        private IFlagRegisterField txEnabled;

        private IValueRegisterField rxBufferPointer;
        private IValueRegisterField rxBufferSize;
        private IValueRegisterField rxDataSize;
        private IValueRegisterField txBufferPointer;
        private IValueRegisterField txBufferSize;
        private IValueRegisterField txDataSize;

        private uint rxChannels;
        private uint rxSampleWidth;
        private uint txChannels;
        private uint txSampleWidth;

        private IManagedThread rxThread;
        private IManagedThread txThread;
        private PCMDecoder decoder;
        private PCMEncoder encoder;

        private enum Events
        {
            Rx = 0,
            Tx = 1,
            Extra = 2,
        }

        private enum Registers :long
        {
            RxBufferPointer    = 0x0,  //    RX Channel 0 I2S uDMA transfer address of associated buffer
            RxBufferSize       = 0x4,  //    RX Channel 0 I2S uDMA transfer size of buffer
            RxConfig           = 0x8,  //    RX Channel 0 I2S uDMA transfer configuration
            RxInit             = 0xC,  //
            TxBufferPointer    = 0x10, //    TX Channel I2S uDMA transfer address of associated buffer
            TxBufferSize       = 0x14, //    TX Channel I2S uDMA transfer size of buffer
            TxConfig           = 0x18, //    TX Channel I2S uDMA transfer configuration
            TxInit             = 0x1C, //
            ClockConfiguration = 0x20, //    Clock configuration for both master, slave and pdm
            SlaveSettings      = 0x24, //    Configuration of I2S slave
            MasterSettings     = 0x28, //    Configuration of I2S master
            PdmConfig          = 0x2C, //    Configuration of PDM module
        }
    }
}
