//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Time;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class EFR32xG2_RNGCTRL : IBusPeripheral
    {
        public EFR32xG2_RNGCTRL(Machine machine)
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
        }

        [ConnectionRegionAttribute("rngctrl_s")]
        public uint ReadDoubleWordRegisterSecure(long offset)
        {
            return ReadRegister(offset);
        }

        [ConnectionRegionAttribute("rngctrl_ns")]
        public uint ReadDoubleWordRegisterNonSecure(long offset)
        {
            return ReadRegister(offset);
        }

        [ConnectionRegionAttribute("rngctrl_s")]
        public void WriteDoubleWordRegisterSecure(long offset, uint value)
        {
            WriteRegister(offset, value);
        }

        [ConnectionRegionAttribute("rngctrl_ns")]
        public void WriteDoubleWordRegisterNonSecure(long offset, uint value)
        {
            WriteRegister(offset, value);
        }

        [ConnectionRegionAttribute("rngfifo_s")]
        public uint ReadDoubleWordFifoSecure(long offset)
        {
            return ReadFifo(offset);
        }

        [ConnectionRegionAttribute("rngfifo_ns")]
        public uint ReadDoubleWordFifoNonSecure(long offset)
        {
            return ReadFifo(offset);
        }

        [ConnectionRegionAttribute("rngfifo_s")]
        public void WriteDoubleWordFifoSecure(long offset, uint value)
        {
            // Writing not supported
        }

        [ConnectionRegionAttribute("rngfifo_ns")]
        public void WriteDoubleWordFifoNonSecure(long offset, uint value)
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

        public uint ReadFifo(long offset)
        {
            return FifoDequeue();
        }

        private DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.RngControl, new DoubleWordRegister(this, 0x00040000)
                    .WithFlag(0, valueProviderCallback: _ => Enable, writeCallback: (_, value) => Enable = value, name: "ENABLE")
                    .WithTaggedFlag("CONTROL", 1)
                    .WithFlag(2, out testEnable, name: "TESTEN")
                    .WithTaggedFlag("CONDBYPASS", 3)
                    .WithTaggedFlag("REPCOUNTIEN", 4)
                    .WithTaggedFlag("APT64IEN", 5)
                    .WithTaggedFlag("APT4096IEN", 6)
                    .WithFlag(7, out fifoFullInterruptEnable, name: "FULLIEN")
                    .WithFlag(8, valueProviderCallback: _ => SoftwareReset, writeCallback: (_, value) => SoftwareReset = value, name: "SOFTRESET")
                    .WithTaggedFlag("PREIEN", 9)
                    .WithTaggedFlag("ALMIEN", 10)
                    .WithTaggedFlag("FORCERUN", 11)
                    .WithTaggedFlag("BYPNIST", 12)
                    .WithTaggedFlag("BYPAIS31", 13)
                    .WithTaggedFlag("HEALTHTESTSEL", 14)
                    .WithTaggedFlag("AIS31TESTSEL", 15)
                    .WithValueField(16, 4, valueProviderCallback: _ => CbcMacBlockNumber, writeCallback: (_, value) => CbcMacBlockNumber = (uint)value, name: "NB128BITBLOCKS")
                    .WithTaggedFlag("FIFOWRSTARTUP", 20)
                    .WithReservedBits(21, 11)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.FifoLevel, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => FifoLevel, name: "FIFOLEVEL")
                },
                {(long)Registers.FifoDepth, new DoubleWordRegister(this, 0x40)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => FifoDepth, name: "FIFODEPTH")
                },
                {(long)Registers.RngStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("TESTDATABUSY", 0)
                    .WithEnumField<DoubleWordRegister, RngState>(1, 3, FieldMode.Read, valueProviderCallback: _ => State, name: "RXSETEVENT1")
                    .WithTaggedFlag("REPCOUNTIF", 4)
                    .WithTaggedFlag("APT64IF", 5)
                    .WithTaggedFlag("APT4096IF", 6)
                    .WithFlag(7, out fifoFullInterrupt, name: "FULLIF")
                    .WithTaggedFlag("PREIF", 8)
                    .WithTaggedFlag("ALMIF", 9)
                    .WithTaggedFlag("STARTUPPASS", 10)
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

        public GPIO IRQ { get; }
        private readonly Machine machine;
        private static PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        private readonly DoubleWordRegisterCollection registersCollection;
        private const uint FifoDepth = 64;
        private RngState state;
        private bool enabled = false;
        private bool softwareReset = false;
        private Queue<uint> fifo;
        private byte[] key = new byte[16];
        private byte[] cbcMacBlock = new byte[16];
        private uint cbcMacBlockIndex;
        private uint cbcMacBlockNumber;
        private uint conditioningTestDataCurrentSize;
        private bool conditioningTestOngoing = false;
        private CbcBlockCipherMac cbcMacCipher;
#region register fields
        private IFlagRegisterField fifoFullInterruptEnable;
        private IFlagRegisterField fifoFullInterrupt;
        private IFlagRegisterField testEnable;
#endregion

#region methods
        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;
        
        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                var irq = (fifoFullInterruptEnable.Value && fifoFullInterrupt.Value);
                IRQ.Set(irq);                        
            });
        }

        private uint GetKeyDoubleWord(uint index)
        {
            if (index > 3)
            {
                this.Log(LogLevel.Error, "GetKeyDoubleWord(): index invalid");
                return 0;
            }

            return ((uint)key[index * 4] | (uint)(key[(index * 4) + 1] << 8) | (uint)(key[(index * 4) + 2] << 16) | (uint)(key[(index * 4) + 3] << 24));
        }

        private void SetKeyDoubleWord(uint index, uint value)
        {
            if (index > 3)
            {
                this.Log(LogLevel.Error, "SetKeyDoubleWord(): index invalid");
                return;
            }

            key[index * 4] = (byte)(value & 0xFF);
            key[(index * 4) + 1] = (byte)((value >> 8) & 0xFF);
            key[(index * 4) + 2] = (byte)((value >> 16) & 0xFF);
            key[(index * 4) + 3] = (byte)((value >> 24) & 0xFF);
        }

        private void FifoEnqueue(uint value)
        {
            if (fifo.Count == FifoDepth)
            {
                // Ignore
                return;
            }
            
            fifo.Enqueue(value);

            if (fifo.Count == FifoDepth)
            {
                fifoFullInterrupt.Value = true;
                UpdateInterrupts();
            }
        }

        private uint FifoDequeue()
        {
            if (fifo.Count == 0)
            {
                this.Log(LogLevel.Warning, "FifoDequeue(): queue is empty!");
                return 0;
            }

            uint ret = fifo.Dequeue();
            FillQueue();
            return ret;
        }

        private void FillQueue()
        {
            while(fifo.Count < FifoDepth)
            {
                FifoEnqueue((uint)random.Next());
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
                if (!enabled && value)
                {
                    enabled = true;
                    if (fifo.Count > 0)
                    {
                        this.Log(LogLevel.Warning, "Fifo not empty upon enable!");
                    }
                    State = RngState.Running;
                    FillQueue();
                    State = RngState.FifoFullOff;
                }

                if (!value)
                {
                    enabled = false;
                    State = RngState.Reset;
                }
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
                if (!softwareReset)
                {
                    softwareReset = true;
                    State = RngState.Reset;
                    enabled = false;
                    conditioningTestOngoing = false;
                    fifo.Clear();
                }
            }
        }

        private uint FifoLevel
        {
            get
            {
                return (uint)fifo.Count;
            }
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

        private uint CbcMacBlockNumber
        {
            get
            {
                return cbcMacBlockNumber;
            }
            set
            {
                if (value != 0)
                {
                    cbcMacBlockNumber = value;
                }
            }
        }

        private uint TestData
        {
            set
            {
                if (!testEnable.Value)
                {
                    return;
                }

                if (!conditioningTestOngoing)
                {
                    conditioningTestOngoing = true;
                    conditioningTestDataCurrentSize = 0;
                    cbcMacBlockIndex = 0;
                    KeyParameter keyParameter = new KeyParameter(key);
                    byte[] iv = {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};
                    ParametersWithIV parametersWithIV = new ParametersWithIV(keyParameter, iv);
                    cbcMacCipher.Init(parametersWithIV);
                    this.Log(LogLevel.Noisy, "Creating CBCMAC object, key=[{0}]", BitConverter.ToString(key));
                }

                if (conditioningTestDataCurrentSize <= CbcMacBlockNumber*16 - 4)
                {
                    conditioningTestDataCurrentSize += 4;
                    cbcMacBlock[cbcMacBlockIndex + 3] = (byte)((value >> 24) & 0xFF);
                    cbcMacBlock[cbcMacBlockIndex + 2] = (byte)((value >> 16) & 0xFF);
                    cbcMacBlock[cbcMacBlockIndex + 1] = (byte)((value >> 8) & 0xFF);
                    cbcMacBlock[cbcMacBlockIndex] = (byte)(value & 0xFF);
                    cbcMacBlockIndex += 4;

                    if (cbcMacBlockIndex == 16)
                    {
                        this.Log(LogLevel.Noisy, "Adding block=[{0}]", BitConverter.ToString(cbcMacBlock));
                        cbcMacCipher.BlockUpdate(cbcMacBlock, 0, 16);
                        cbcMacBlockIndex = 0;
                    }
                }

                if (conditioningTestDataCurrentSize == CbcMacBlockNumber*16)
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
            }
        }
#endregion

#region enums
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
            RepThreshold                            = 0x024,
            PropThreshold                           = 0x028,
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
#endregion
    }
}