//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2022-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Time;

using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    /// <summary>
    /// CRYPTOACC RNGCTRL peripheral (BA431-based TRNG with APT64/APT4096 extensions).
    ///
    /// The register map matches SE firmware and TRNG headers (P1Thres/P2Thres, APT64IF/APT4096IF),
    /// not the vanilla Silex Insight BA431 datasheet which documents a single 1024-sample adaptive
    /// proportion test at offset 0x28.
    ///
    /// Continuous health tests are only emulated for firmware bring-up: status flags, threshold
    /// registers, IRQ wiring, and soft-reset clearing are modeled, but NIST SP 800-90B sample
    /// counting is not.
    /// </summary>
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class SiLabs_RNGCTRL : IBusPeripheral
    {
        public SiLabs_RNGCTRL(Machine machine)
        {
            this.machine = machine;

            fifo = new Queue<uint>();
            cbcMacCipher = new CbcBlockCipherMac(new AesEngine(), 128);

            IRQ = new GPIO();
            registersCollection = BuildRegistersCollection();
        }

        public void Reset()
        {
            SoftwareReset = false;
            SoftwareReset = true;
            SoftwareReset = false;

            registersCollection.Reset();
            repThreshold = DefaultRepThreshold;
            propThreshold = DefaultPropThreshold;
            testModeSampleCount = 0;
            ClearHealthTestFailures();
            UpdateInterrupts();
        }

        [ConnectionRegion("rngctrl_s")]
        public uint ReadDoubleWordRegisterSecure(long offset)
        {
            return ReadRegister(offset);
        }

        [ConnectionRegion("rngctrl_ns")]
        public uint ReadDoubleWordRegisterNonSecure(long offset)
        {
            return ReadRegister(offset);
        }

        [ConnectionRegion("rngctrl_s")]
        public void WriteDoubleWordRegisterSecure(long offset, uint value)
        {
            WriteRegister(offset, value);
        }

        [ConnectionRegion("rngctrl_ns")]
        public void WriteDoubleWordRegisterNonSecure(long offset, uint value)
        {
            WriteRegister(offset, value);
        }

        [ConnectionRegion("rngfifo_s")]
        public uint ReadDoubleWordFifoSecure(long offset)
        {
            return ReadFifo(offset);
        }

        [ConnectionRegion("rngfifo_ns")]
        public uint ReadDoubleWordFifoNonSecure(long offset)
        {
            return ReadFifo(offset);
        }

        [ConnectionRegion("rngfifo_s")]
        public void WriteDoubleWordFifoSecure(long _, uint __)
        {
            // Writing not supported
        }

        [ConnectionRegion("rngfifo_ns")]
        public void WriteDoubleWordFifoNonSecure(long _, uint __)
        {
            // Writing not supported
        }

        public uint ReadRegister(long offset)
        {
            var result = 0U;

            if(!registersCollection.TryRead(offset, out result))
            {
                this.Log(LogLevel.Noisy, "Unhandled read at offset 0x{0:X} ({1}).", offset, (Registers)offset);
            }
            else
            {
                this.Log(LogLevel.Noisy, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", offset, (Registers)offset, result);
            }

            return result;
        }

        public void WriteRegister(long offset, uint value)
        {
            this.Log(LogLevel.Noisy, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", offset, (Registers)offset, value);
            if(!registersCollection.TryWrite(offset, value))
            {
                this.Log(LogLevel.Noisy, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", offset, (Registers)offset, value);
            }
        }

        public uint ReadFifo(long _)
        {
            return FifoDequeue();
        }

        public GPIO IRQ { get; }

        private static readonly PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;

        private void ProcessTestModeHealthTestSample()
        {
            testModeSampleCount++;
            if(testModeSampleCount >= Apt64WindowSize && !apt64Fail.Value)
            {
                TriggerApt64Failure();
            }
        }

        private void FillQueue()
        {
            while(fifo.Count < FifoDepth)
            {
                FifoEnqueue((uint)random.Next());
            }
        }

        private uint FifoDequeue()
        {
            if(fifo.Count == 0)
            {
                this.Log(LogLevel.Warning, "FifoDequeue(): queue is empty!");
                return 0;
            }

            uint ret = fifo.Dequeue();
            FillQueue();
            ProcessHealthTestSample();
            return ret;
        }

        private void FifoEnqueue(uint value)
        {
            if(fifo.Count == FifoDepth)
            {
                // Ignore
                return;
            }

            fifo.Enqueue(value);

            if(fifo.Count == FifoDepth)
            {
                fifoFullInterrupt.Value = true;
                UpdateInterrupts();
            }
        }

        private void SetKeyDoubleWord(uint index, uint value)
        {
            if(index > 3)
            {
                this.Log(LogLevel.Error, "SetKeyDoubleWord(): index invalid");
                return;
            }

            key[index * 4] = (byte)(value & 0xFF);
            key[(index * 4) + 1] = (byte)((value >> 8) & 0xFF);
            key[(index * 4) + 2] = (byte)((value >> 16) & 0xFF);
            key[(index * 4) + 3] = (byte)((value >> 24) & 0xFF);
        }

        private uint GetKeyDoubleWord(uint index)
        {
            if(index > 3)
            {
                this.Log(LogLevel.Error, "GetKeyDoubleWord(): index invalid");
                return 0;
            }

            return ((uint)key[index * 4] | (uint)(key[(index * 4) + 1] << 8) | (uint)(key[(index * 4) + 2] << 16) | (uint)(key[(index * 4) + 3] << 24));
        }

        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var irq = (fifoFullInterruptEnable.Value && fifoFullInterrupt.Value)
                    || (repCountInterruptEnable.Value && repCountFail.Value)
                    || (apt64InterruptEnable.Value && apt64Fail.Value)
                    || (apt4096InterruptEnable.Value && apt4096Fail.Value)
                    || (alarmInterruptEnable.Value && alarmFail.Value);
                IRQ.Set(irq);
            });
        }

        // Continuous-test emulation (APT64/APT4096 register layout). Not cycle-accurate BA431; see class summary.

        private void ClearHealthTestFailures()
        {
            repCountFail.Value = false;
            apt64Fail.Value = false;
            apt4096Fail.Value = false;
            alarmFail.Value = false;
            apt64FailureScheduled = false;
            healthTestSampleCount = 0;
            testModeSampleCount = 0;
        }

        private void OnPropThresholdWritten(uint value)
        {
            propThreshold = value;
            EvaluateHealthTests();
        }

        private void OnRepThresholdWritten(uint value)
        {
            repThreshold = value;
            EvaluateHealthTests();
        }

        private void EvaluateHealthTests()
        {
            if(!enabled || testEnable.Value)
            {
                return;
            }

            if(repThreshold == 0)
            {
                TriggerRepCountFailure();
                return;
            }

            // A cutoff of zero forces an immediate APT64 failure; firmware uses this in fault tests.
            if(propThreshold == 0)
            {
                ScheduleApt64Failure();
            }
        }

        private void ProcessHealthTestSample()
        {
            if(!enabled || testEnable.Value || propThreshold != 0)
            {
                return;
            }

            healthTestSampleCount++;
            if(healthTestSampleCount >= Apt64WindowSize)
            {
                TriggerApt64Failure();
            }
            else
            {
                ScheduleApt64Failure();
            }
        }

        private void ScheduleApt64Failure()
        {
            if(apt64FailureScheduled || apt64Fail.Value)
            {
                return;
            }

            apt64FailureScheduled = true;
            machine.ScheduleAction(TimeInterval.FromMicroseconds(10), _ =>
            {
                apt64FailureScheduled = false;
                if(propThreshold == 0 && enabled && !testEnable.Value)
                {
                    TriggerApt64Failure();
                }
            }, "trng-apt64-failure");
        }

        private void TriggerRepCountFailure()
        {
            if(repCountFail.Value)
            {
                return;
            }

            repCountFail.Value = true;
            State = RngState.Error;
            this.Log(LogLevel.Noisy, "Repetition count health test failed");
            UpdateInterrupts();
        }

        private void TriggerApt64Failure()
        {
            if(apt64Fail.Value)
            {
                return;
            }

            apt64Fail.Value = true;
            State = RngState.Error;
            this.Log(LogLevel.Noisy, "APT64 health test failed");
            UpdateInterrupts();
        }

        // Although typically Renode uses tags for unimplemented fields, to minimize the amount of logs we switch to plain fields.
        // Todo: verify which can be switched to silent tags
        private DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.RngControl, new DoubleWordRegister(this, 0x00040000)
                    .WithFlag(0, valueProviderCallback: _ => Enable, writeCallback: (_, value) => Enable = value, name: "ENABLE")
                    .WithFlag(1, name: "CONTROL")
                    .WithFlag(2, out testEnable, name: "TESTEN")
                    .WithFlag(3, name: "CONDBYPASS")
                    .WithFlag(4, out repCountInterruptEnable, name: "REPCOUNTIEN")
                    .WithFlag(5, out apt64InterruptEnable, name: "APT64IEN")
                    .WithFlag(6, out apt4096InterruptEnable, name: "APT4096IEN")
                    .WithFlag(7, out fifoFullInterruptEnable, name: "FULLIEN")
                    .WithFlag(8, valueProviderCallback: _ => SoftwareReset, writeCallback: (_, value) => SoftwareReset = value, name: "SOFTRESET")
                    .WithFlag(9, name: "PREIEN")
                    .WithFlag(10, out alarmInterruptEnable, name: "ALMIEN")
                    .WithFlag(11, name: "FORCERUN")
                    .WithFlag(12, name: "BYPNIST")
                    .WithFlag(13, name: "BYPAIS31")
                    .WithFlag(14, name: "HEALTHTESTSEL")
                    .WithFlag(15, name: "AIS31TESTSEL")
                    .WithValueField(16, 4, valueProviderCallback: _ => CbcMacBlockNumber, writeCallback: (_, value) => CbcMacBlockNumber = (uint)value, name: "NB128BITBLOCKS")
                    .WithFlag(20, name: "FIFOWRSTARTUP")
                    .WithReservedBits(21, 11)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.FifoLevel, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => FifoLevel, name: "FIFOLEVEL")
                },
                {(long)Registers.FifoDepth, new DoubleWordRegister(this, 0x40)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => FifoDepth, name: "FIFODEPTH")
                },
                {(long)Registers.RepThreshold, new DoubleWordRegister(this, DefaultRepThreshold)
                    .WithValueField(0, 32, valueProviderCallback: _ => repThreshold, writeCallback: (_, value) => OnRepThresholdWritten((uint)value), name: "REPTHRES")
                },
                // P1ThresReg in firmware: APT64 (64-sample window) cutoff. Vanilla BA431 uses a single 1024-sample APT here.
                {(long)Registers.PropThreshold, new DoubleWordRegister(this, DefaultPropThreshold)
                    .WithValueField(0, 32, valueProviderCallback: _ => propThreshold, writeCallback: (_, value) => OnPropThresholdWritten((uint)value), name: "P1THRES")
                },
                {(long)Registers.RngStatus, new DoubleWordRegister(this)
                    .WithFlag(0, name: "TESTDATABUSY")
                    .WithEnumField<DoubleWordRegister, RngState>(1, 3, FieldMode.Read, valueProviderCallback: _ => State, name: "RXSETEVENT1")
                    .WithFlag(4, out repCountFail, name: "REPCOUNTIF")
                    .WithFlag(5, out apt64Fail, name: "APT64IF")
                    .WithFlag(6, out apt4096Fail, name: "APT4096IF")
                    .WithFlag(7, out fifoFullInterrupt, name: "FULLIF")
                    .WithFlag(8, name: "PREIF")
                    .WithFlag(9, out alarmFail, name: "ALMIF")
                    .WithFlag(10, name: "STARTUPPASS")
                    .WithReservedBits(11, 21)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Key0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => GetKeyDoubleWord(0), writeCallback: (_, value) => SetKeyDoubleWord(0, (uint)value), name: "KEY")
                },
                {(long)Registers.Key1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => GetKeyDoubleWord(1), writeCallback: (_, value) => SetKeyDoubleWord(1, (uint)value), name: "KEY")
                },
                {(long)Registers.Key2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => GetKeyDoubleWord(2), writeCallback: (_, value) => SetKeyDoubleWord(2, (uint)value), name: "KEY")
                },
                {(long)Registers.Key3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => GetKeyDoubleWord(3), writeCallback: (_, value) => SetKeyDoubleWord(3, (uint)value), name: "KEY")
                },
                {(long)Registers.TestData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) => TestData = (uint)value, name: "VALUE")
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private RngState State
        {
            set
            {
                state = value;
            }

            get
            {
                return state;
            }
        }

        private uint FifoLevel
        {
            get
            {
                return (uint)fifo.Count;
            }
        }

        private bool SoftwareReset
        {
            get
            {
                return softwareReset;
            }

            set
            {
                if(value && !softwareReset)
                {
                    softwareReset = true;
                    var wasEnabled = enabled;
                    State = RngState.Reset;
                    conditioningTestOngoing = false;
                    fifo.Clear();
                    fifoFullInterrupt.Value = false;
                    ClearHealthTestFailures();
                    if(wasEnabled)
                    {
                        State = RngState.Running;
                        FillQueue();
                        State = RngState.FifoFullOff;
                    }
                }
                else if(!value)
                {
                    softwareReset = false;
                }
            }
        }

        private bool Enable
        {
            get
            {
                return enabled;
            }

            set
            {
                if(!enabled && value)
                {
                    enabled = true;
                    if(fifo.Count > 0)
                    {
                        this.Log(LogLevel.Warning, "Fifo not empty upon enable!");
                    }
                    State = RngState.Running;
                    FillQueue();
                    State = RngState.FifoFullOff;
                }

                if(!value)
                {
                    enabled = false;
                    State = RngState.Reset;
                }
            }
        }

        private uint CbcMacBlockNumber
        {
            get
            {
                return cbcMacBlockNumber;
            }

            set
            {
                if(value != 0)
                {
                    cbcMacBlockNumber = value;
                }
            }
        }

        private uint TestData
        {
            set
            {
                if(!testEnable.Value)
                {
                    return;
                }

                if(!conditioningTestOngoing)
                {
                    conditioningTestOngoing = true;
                    conditioningTestDataCurrentSize = 0;
                    cbcMacBlockIndex = 0;
                    var keyParameter = new KeyParameter(key);
                    byte[] iv = {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};
                    var parametersWithIV = new ParametersWithIV(keyParameter, iv);
                    cbcMacCipher.Init(parametersWithIV);
                    this.Log(LogLevel.Noisy, "Creating CBCMAC object, key=[{0}]", BitConverter.ToString(key));
                }

                if(conditioningTestDataCurrentSize <= CbcMacBlockNumber * 16 - 4)
                {
                    conditioningTestDataCurrentSize += 4;
                    cbcMacBlock[cbcMacBlockIndex + 3] = (byte)((value >> 24) & 0xFF);
                    cbcMacBlock[cbcMacBlockIndex + 2] = (byte)((value >> 16) & 0xFF);
                    cbcMacBlock[cbcMacBlockIndex + 1] = (byte)((value >> 8) & 0xFF);
                    cbcMacBlock[cbcMacBlockIndex] = (byte)(value & 0xFF);
                    cbcMacBlockIndex += 4;

                    if(cbcMacBlockIndex == 16)
                    {
                        this.Log(LogLevel.Noisy, "Adding block=[{0}]", BitConverter.ToString(cbcMacBlock));
                        cbcMacCipher.BlockUpdate(cbcMacBlock, 0, 16);
                        cbcMacBlockIndex = 0;
                    }
                }

                if(conditioningTestDataCurrentSize == CbcMacBlockNumber * 16)
                {
                    conditioningTestOngoing = false;
                    byte[] output = new byte[16];
                    cbcMacCipher.DoFinal(output, 0);
                    this.Log(LogLevel.Noisy, "CBC output=[{0}]", BitConverter.ToString(output));
                    for(uint i = 0; i < 4; i++)
                    {
                        uint val = (uint)output[i*4] | ((uint)output[i*4 + 1] << 8) | ((uint)output[i*4 + 2] << 16) | ((uint)output[i*4 + 3] << 24);
                        FifoEnqueue(val);
                    }
                }

                if(value == 0)
                {
                    ProcessTestModeHealthTestSample();
                }
            }
        }

        private bool conditioningTestOngoing = false;
        private uint conditioningTestDataCurrentSize;
        private uint cbcMacBlockNumber;
        private uint cbcMacBlockIndex;
        private bool softwareReset = false;
        private bool enabled = false;
        private RngState state;
        private IFlagRegisterField fifoFullInterruptEnable;
        private IFlagRegisterField fifoFullInterrupt;
        private IFlagRegisterField testEnable;
        private IFlagRegisterField alarmInterruptEnable;
        private IFlagRegisterField repCountInterruptEnable;
        private IFlagRegisterField apt64InterruptEnable;
        private IFlagRegisterField apt4096InterruptEnable;
        private IFlagRegisterField repCountFail;
        private IFlagRegisterField apt64Fail;
        private IFlagRegisterField apt4096Fail;
        private IFlagRegisterField alarmFail;
        private uint repThreshold = DefaultRepThreshold;
        private uint propThreshold = DefaultPropThreshold;
        private uint healthTestSampleCount;
        private uint testModeSampleCount;
        private bool apt64FailureScheduled;
        private readonly CbcBlockCipherMac cbcMacCipher;
        private readonly byte[] cbcMacBlock = new byte[16];
        private readonly byte[] key = new byte[16];
        private readonly Queue<uint> fifo;
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registersCollection;
        private const uint FifoDepth = 64;
        // Defaults for the APT64/APT4096 register layout. Vanilla BA431 uses RepThresReg=0x29 and a single 1024-sample APT at 0x28.
        private const uint DefaultRepThreshold = 31;
        private const uint DefaultPropThreshold = 10;
        private const uint Apt64WindowSize = 64;

        private enum RngState
        {
            Reset       = 0,
            Startup     = 1,
            FifoFullOn  = 2,
            FifoFullOff = 3,
            Running     = 4,
            Error       = 5,
            Unused_6    = 6,
            Unused      = 7,
        }

        [RegistersDescription("rngctrl_s", "rngctrl_ns")]
        private enum Registers
        {
            RngControl                              = 0x000,
            FifoLevel                               = 0x004,
            FifoThreshold                           = 0x008,
            FifoDepth                               = 0x00C,
            Key0                                    = 0x010,
            Key1                                    = 0x014,
            Key2                                    = 0x018,
            Key3                                    = 0x01C,
            TestData                                = 0x020,
            RepThreshold                            = 0x024, // RepThresReg / repetition-count cutoff
            PropThreshold                           = 0x028, // P1ThresReg / APT64 cutoff (not BA431 1024-sample PropThresReg)
            Apt4096Threshold                        = 0x02C, // P2ThresReg / APT4096 cutoff (reserved on vanilla BA431)
            RngStatus                               = 0x030,
            InitWaitCounter                         = 0x034,
            DisableOscillatorRings0                 = 0x038,
            DisableOscillatorRings1                 = 0x03C,
            SwitchOffTimer                          = 0x040,
            ClockDivider                            = 0x044,
            AIS31Configuration0                     = 0x048,
            AIS31Configuration1                     = 0x04C,
            AIS31Configuration2                     = 0x050,
            AIS31Status                             = 0x054,
        }
    }
}
