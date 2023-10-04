//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sound
{
    public class EOSS3_Voice : BasicDoubleWordPeripheral, IKnownSize
    {
        public EOSS3_Voice(IMachine machine) : base(machine)
        {
            CreateRegisters();
            IRQ = new GPIO();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            decoderLeft?.Reset();
            decoderRight?.Reset();
            sampleThread?.Dispose();
            sampleThread = null;
            inputFileLeft = null;
            inputFileRight = null;
            numberOfChannels = 1;
            IRQ.Unset();
        }

        public void SetInputFile(string fileName, Channel channel = Channel.Left, int repeat = 1)
        {
            switch(channel)
            {
                case Channel.Left:
                    {
                        if(decoderLeft == null)
                        {
                            decoderLeft = new PCMDecoder(16, 16000, 1, false, this);
                        }

                        for(var i = 0; i < repeat; i++)
                        {
                            decoderLeft.LoadFile(fileName);
                        }
                        inputFileLeft = fileName;
                    }
                    break;

                case Channel.Right:
                    {
                        if(decoderRight == null)
                        {
                            decoderRight = new PCMDecoder(16, 16000, 1, false, this);
                        }

                        for(var i = 0; i < repeat; i++)
                        {
                            decoderRight.LoadFile(fileName);
                        }
                        inputFileRight = fileName;
                    }
                    break;
            }
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        public enum Channel
        {
            Left = 0,
            Right = 1,
        }

        private void Start()
        {
            if(!enable.Value)
            {
                this.Log(LogLevel.Warning, "Trying to start samples aquisition before enabling peripheral. Will not start");
                return;
            }

            if(inputFileLeft == null || (numberOfChannels == 2 && inputFileRight == null))
            {
                this.Log(LogLevel.Error, "Trying to start reception with not enough input files - please set input using `SetinputFile`. Aborting.");
                return;
            }

            StartPDMThread();
        }

        private void Stop()
        {
            StopPDMThread();
            this.Log(LogLevel.Debug, "Event Stopped");
        }

        private void StartPDMThread()
        {
            StopPDMThread();
            sampleThread = machine.ObtainManagedThread(InputSamples, 1);
            sampleThread.Start();
        }

        private void StopPDMThread()
        {
            if(sampleThread == null)
            {
                return;
            }
            sampleThread.Stop();
            sampleThread.Dispose();
            sampleThread = null;
        }

        private void InputSamples()
        {
            var samplesCount = (uint)bufferTransferLength.Value * 2; // samples are 16bit and the register indicates the amount of 32bit words
            var preparedDoubleWords = new uint[samplesCount / 2];

            switch(numberOfChannels)
            {
                case 1:
                    var samples = decoderLeft.GetSamplesByCount(samplesCount);

                    var index = 1u;
                    ushort prev = 0;

                    foreach(ushort sample in samples)
                    {
                        if(index % 2 != 0)
                        {
                            prev = sample;
                        }
                        else
                        {
                            // Assuming input file format of s16le
                            preparedDoubleWords[(index / 2) - 1] = (uint)((Misc.SwapBytesUShort(sample) << 16) | Misc.SwapBytesUShort(prev));
                        }
                        index++;
                    }

                    if(index % 2 == 0)
                    {
                        // One sample left
                        preparedDoubleWords[(index / 2) - 1] = prev;
                    }

                    break;
                case 2:
                    var samplesLeft = decoderLeft.GetSamplesByCount(samplesCount / 2).ToArray();
                    var samplesRight = decoderRight.GetSamplesByCount(samplesCount / 2).ToArray();

                    if(samplesLeft.Length != samplesRight.Length)
                    {
                        // Make sure arrays have equal size
                        var neededSize = Math.Max(samplesLeft.Length, samplesRight.Length);
                        Array.Resize(ref samplesLeft, neededSize);
                        Array.Resize(ref samplesRight, neededSize);
                    }

                    for(var i = 0; i < samplesLeft.Length; i++)
                    {
                        var right = (uint)Misc.SwapBytesUShort((ushort)samplesRight[i]);
                        var left = (uint)Misc.SwapBytesUShort((ushort)samplesLeft[i]);

                        preparedDoubleWords[i] = (right << 16) | left;
                    }
                    break;
            }

            var data = new byte[preparedDoubleWords.Length * 4];
            System.Buffer.BlockCopy(preparedDoubleWords, 0, data, 0, preparedDoubleWords.Length * 4);
            sysbus.WriteBytes(data, dmac0DestAddr.Value);

            IRQ.Blink();
        }

        private void CreateRegisters()
        {
            Registers.VoiceConfig.Define(this)
                .WithTag("DMIC_SEL", 0, 1)
                .WithTag("LPSD_SEL", 1, 1)
                .WithTag("MODE_SEL", 2, 1)
                .WithTag("MONO_CHN_SEL", 3, 1)
                .WithTag("I2S_DS_SEL", 4, 1)
                .WithTag("PDM_VOICE_SCENARIO", 5, 3)
                .WithTag("PDM_MIC_SWITCH_TO_AP", 8, 1)
                .WithTag("LPSD_USE_DC_BLOCK", 9, 1)
                .WithTag("LPSD_MUX", 10, 1)
                .WithTag("LPSD_NO", 11, 1)
                .WithTag("I2S_PGA_EN", 12, 1)
                .WithReservedBits(13, 2)
                .WithTag("DIV_AP", 15, 3)
                .WithTag("DIV_WD", 18, 6)
                .WithTag("FIFO_0_CLEAR", 24, 1)
                .WithTag("FIFO_1_CLEAR", 25, 1)
                .WithTag("LPSD_VOICE_DETECTED_MASK", 26, 1)
                .WithTag("DMIC_VOICE_DETECTED_MASK", 27, 1)
                .WithTag("DMAC_BLK_DONE_MASK", 28, 1)
                .WithTag("DMAC_BUF_DONE_MASK", 29, 1)
                .WithTag("AP_PDM_CLK_ON_MASK", 30, 1)
                .WithTag("AP_PDM_CLK_OFF_MASK", 31, 1);

            Registers.LPSDConfig.Define(this)
                .WithTag("LPSD_THD", 0, 16)
                .WithTag("LPSD_RATIO_STOP", 16, 8)
                .WithTag("LPSD_RATIO_RUN", 24, 8);

            Registers.VoiceDMACConfig.Define(this)
                .WithFlag(0, out enable, name: "DMAC_EN")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        Start();
                    }, name: "DMAC_START")
                .WithFlag(2, writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }
                        Stop();
                    }, name: "DMAC_STOP")
                .WithTag("AHB_RDY", 3, 1)
                .WithTag("AHB_BURST_LENGTH", 4, 2)
                .WithTag("PINGPONG_MODE", 6, 1)
                .WithTag("STEREO_DUAL_BUF_MODE", 7, 1)
                .WithTag("VOICE_DMAC_BURST_SPD", 8, 8)
                .WithReservedBits(16, 16);

            Registers.VoiceDMACLength.Define(this)
                .WithValueField(0, 16, out blockTransferLength, name: "DMAC_BLK_LEN")
                .WithValueField(16, 16, out bufferTransferLength, name: "DMAC_BUF_LEN");

            Registers.VoiceDMACFifo.Define(this)
                .WithReservedBits(0, 16)
                .WithTag("DMAC_BUF_OFFSET", 16, 16);

            Registers.VoiceDMACDestinationAddress0.Define(this)
                .WithValueField(0, 32, out dmac0DestAddr, name: "VOICE_DMAC_DST_ADDR0");

            Registers.VoiceDMACDestinationAddress1.Define(this)
                .WithTag("VOICE_DMAC_DST_ADDR1", 0, 32);

            Registers.PDMCoreConfig.Define(this)
                .WithTag("PDM_CORE_EN", 0, 1)
                .WithTag("SOFT_MUTE", 1, 1)
                .WithTag("DIV_MODE", 2, 1)
                .WithTag("S_CYCLES", 3, 3)
                .WithTag("HP_GAIN", 6, 4)
                .WithTag("ADCHPD", 10, 1)
                .WithTag("M_CLK_DIV", 11, 2)
                .WithTag("SINC_RATE", 13, 7)
                .WithTag("PGA_L", 20, 5)
                .WithTag("PGA_R", 25, 5)
                .WithTag("DMICK_DLY", 30, 1)
                .WithTag("DIV_WD_MODE", 31, 1);

            Registers.VoiceStatus.Define(this)
                .WithTag("FIFO_0A_EMPTY", 0, 1)
                .WithTag("FIFO_0A_FULL", 1, 1)
                .WithTag("FIFO_0A_OVERFLOW", 2, 1)
                .WithReservedBits(3, 1)
                .WithTag("FIFO_0B_EMPTY", 4, 1)
                .WithTag("FIFO_0B_FULL", 5, 1)
                .WithTag("FIFO_0B_OVERFLOW", 6, 1)
                .WithReservedBits(7, 1)
                .WithTag("FIFO_1A_EMPTY", 8, 1)
                .WithTag("FIFO_1A_FULL", 9, 1)
                .WithTag("FIFO_1A_OVERFLOW", 10, 1)
                .WithReservedBits(11, 1)
                .WithTag("FIFO_1B_EMPTY", 12, 1)
                .WithTag("FIFO_1B_FULL", 13, 1)
                .WithTag("FIFO_1B_OVERFLOW", 14, 1)
                .WithReservedBits(15, 1)
                .WithTag("DMIC_VOICE_DETECTED_REG", 16, 1)
                .WithTag("LPSD_VOICE_DETECTED_REG", 17, 1)
                .WithTag("AP_PDM_CLK_OFF_REG", 18, 1)
                .WithTag("AP_PDM_CLK_ON_REG", 19, 1)
                .WithTag("DMAC1_BUF_DONE_REG", 20, 1)
                .WithTag("DMAC1_BLK_DONE_REG", 21, 1)
                .WithTag("DMAC0_BUF_DONE_REG", 22, 1)
                .WithTag("DMAC0_BLK_DONE_REG", 23, 1)
                .WithReservedBits(24, 8);

            Registers.I2SConfig.Define(this)
                .WithTag("I2S_LRCDIV", 0, 12)
                .WithTag("I2S_BCLKDIV", 12, 6)
                .WithTag("I2S_CLK_INV", 18, 1)
                .WithTag("I2S_IWL", 19, 2)
                .WithReservedBits(21, 11);

            Registers.FifoSRAMConfig.Define(this)
                .WithTag("SRAM_0A_TEST1", 0, 1)
                .WithTag("SRAM_0A_RME", 1, 1)
                .WithTag("SRAM_0A_RM", 2, 4)
                .WithTag("SRAM_0B_TEST1", 6, 1)
                .WithTag("SRAM_0B_RME", 7, 1)
                .WithTag("SRAM_0B_RM", 8, 4)
                .WithTag("SRAM_1A_TEST1", 12, 1)
                .WithTag("SRAM_1A_RME", 13, 1)
                .WithTag("SRAM_1A_RM", 14, 4)
                .WithTag("SRAM_1B_TEST1", 18, 1)
                .WithTag("SRAM_1B_RME", 19, 1)
                .WithTag("SRAM_1B_RM", 20, 4)
                .WithReservedBits(24, 8);

            Registers.PDMSRAMConfig.Define(this)
                .WithTag("PDM_SRAM_L_TEST1", 0, 1)
                .WithTag("PDM_SRAM_L_RME", 1, 1)
                .WithTag("PDM_SRAM_L_RM", 2, 4)
                .WithTag("PDM_SRAM_R_TEST1", 6, 1)
                .WithTag("PDM_SRAM_R_RME", 7, 1)
                .WithTag("PDM_SRAM_R_RM", 8, 4)
                .WithReservedBits(12, 20);

            Registers.DebugMUXConfig.Define(this)
                .WithTag("DBG_MUX_CFG", 0, 32);
        }

        private uint numberOfChannels;
        private string inputFileLeft;
        private string inputFileRight;

        private PCMDecoder decoderLeft;
        private PCMDecoder decoderRight;
        private IManagedThread sampleThread;

        private IFlagRegisterField enable;
        private IValueRegisterField blockTransferLength;
        private IValueRegisterField bufferTransferLength;
        private IValueRegisterField dmac0DestAddr;

        private enum Registers : long
        {
            VoiceConfig = 0x0,
            LPSDConfig = 0x4,
            VoiceDMACConfig = 0x8,
            VoiceDMACLength = 0xC,
            VoiceDMACFifo = 0x10,
            VoiceDMACDestinationAddress0 = 0x14,
            VoiceDMACDestinationAddress1 = 0x18,
            PDMCoreConfig = 0x1C,
            VoiceStatus = 0x20,
            I2SConfig = 0x24,
            FifoSRAMConfig = 0x28,
            PDMSRAMConfig = 0x2C,
            DebugMUXConfig = 0x30
        }
    }
}
