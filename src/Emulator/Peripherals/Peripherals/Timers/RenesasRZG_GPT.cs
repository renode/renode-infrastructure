//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

using TimeDirection = Antmicro.Renode.Time.Direction;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class RenesasRZG_GPT : IDoubleWordPeripheral, INumberedGPIOOutput, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasRZG_GPT(IMachine machine, int numberOf32BitChannels, long peripheralClockDFrequency)
        {
            this.peripheralClockDFrequency = peripheralClockDFrequency;
            this.machine = machine;

            this.numberOf32BitChannels = numberOf32BitChannels;
            this.channels = new GPTChannel[TotalChannels];

            Size = 0x100 * TotalChannels;

            RegistersCollection = new DoubleWordRegisterCollection(this, BuildRegisterMap());

            Connections = new ReadOnlyDictionary<int, IGPIO>(
                channels
                    .SelectMany(channel => channel.IRQ)
                    .Select((x, i) => new { Key = i, Value = (IGPIO)x })
                    .ToDictionary(x => x.Key, x => x.Value)
            );

            Reset();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            foreach(var channel in this.channels)
            {
                channel.Reset();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size { get; }
        public DoubleWordRegisterCollection RegistersCollection { get; }

        // IRQs are bundled into 10 signals per channel in the following order:
        // 0: CCMPA - ompare match / input capture A
        // 1: CCMPB - compare match / input capture B
        // 2: CMPC - compare match C
        // 3: CMPD - compare match D
        // 4: CMPE - compare match E
        // 5: CMPF - compare match F
        // 6: ADTRGA - compare match A/D converter start request
        // 7: ADTRGB - compare match A/D converter start request
        // 8: OVF - overflow
        // 9: UDF - underflow
        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>();

            for(long i = 0; i < numberOf32BitChannels; ++i)
            {
                var channel = new GPTChannel(this, i, 32);
                channels[i] = channel;
                registerMap = registerMap
                    .Concat(BuildChannelRegisterMap(0x100 * i, channel))
                    .ToDictionary(x => x.Key, x => x.Value);
            }

            return registerMap;
        }

        private Dictionary<long, DoubleWordRegister> BuildChannelRegisterMap(long channelOffset, GPTChannel channel)
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {channelOffset + (long)ChannelRegisters.WriteProtection, new DoubleWordRegister(this)
                    .WithFlag(0, name: "WP (Register Write Disable)")
                    .WithReservedBits(1, 7)
                    .WithValueField(8, 8, name: "PRKEY (GTWP Key Code)")
                    .WithReservedBits(16, 16)
                },
                {channelOffset + (long)ChannelRegisters.SoftwareStart, new DoubleWordRegister(this)
                    .WithFlags(0, TotalChannels, name: "CSTRT",
                        valueProviderCallback: (i, _) => channels[i].Enable,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                channels[i].Enable = true;
                            }
                        }
                    )
                    .WithReservedBits(TotalChannels, 32 - TotalChannels)
                },
                {channelOffset + (long)ChannelRegisters.SoftwareStop, new DoubleWordRegister(this)
                    .WithFlags(0, TotalChannels, name: "CSTOP",
                        valueProviderCallback: (i, _) => !channels[i].Enable,
                        writeCallback: (i, _, value) =>
                        {
                            if(value)
                            {
                                channels[i].Enable = false;
                            }
                        }
                    )
                    .WithReservedBits(TotalChannels, 32 - TotalChannels)
                },
                {channelOffset + (long)ChannelRegisters.SoftwareClear, new DoubleWordRegister(this)
                    .WithFlags(0, 14, name: "CCLR")
                    .WithReservedBits(14, 18)
                },
                {channelOffset + (long)ChannelRegisters.StartSourceSelect, DefineSourceSelectRegister("Start", 'S')},
                {channelOffset + (long)ChannelRegisters.StopSourceSelect, DefineSourceSelectRegister("Stop", 'P')},
                {channelOffset + (long)ChannelRegisters.ClearSourceSelect, DefineSourceSelectRegister("Clear", 'C')},
                {channelOffset + (long)ChannelRegisters.UpCountSourceSelect, DefineSourceSelectRegister("Count Up", 'U', false)},
                {channelOffset + (long)ChannelRegisters.DownCountSourceSelect, DefineSourceSelectRegister("Count Down", 'D', false)},
                {channelOffset + (long)ChannelRegisters.InputCaptureSourceSelectA, DefineSourceSelectRegister("Input Capture", 'A', false, "GTCCRA")},
                {channelOffset + (long)ChannelRegisters.InputCaptureSourceSelectB, DefineSourceSelectRegister("Input Capture", 'B', false, "GTCCRB")},
                {channelOffset + (long)ChannelRegisters.TimerControl, new DoubleWordRegister(this)
                    .WithFlag(0, name: "CST (Count Start)")
                    .WithReservedBits(1, 15)
                    .WithEnumField<DoubleWordRegister, Mode>(16, 3, name: "MD (Mode Select)",
                        valueProviderCallback: _ => channel.Mode,
                        writeCallback: (_, value) => channel.Mode = value
                    )
                    .WithReservedBits(19, 4)
                    .WithValueField(23, 4, name: "TPCS (Timer Prescaler Select)")
                    .WithReservedBits(27, 5)
                },
                {channelOffset + (long)ChannelRegisters.CountDirectionAndDutySetting, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Direction>(0, 1, name: "UD (Count Direction Setting)",
                        valueProviderCallback: _ => channel.Direction,
                        writeCallback: (_, value) => channel.Direction = value
                    )
                    .WithFlag(1, name: "UDF (Forcible Count Direction Setting)")
                    .WithReservedBits(2, 14)
                    .WithValueField(16, 2, name: "OADTY (GTIOCnA Output Duty Setting)")
                    .WithFlag(18, name: "OADTYF (Forcible GTIOCnA Output Duty Setting)")
                    .WithFlag(19, name: "OADTYR (GTIOCnA Output Value Selecting)")
                    .WithReservedBits(20, 4)
                    .WithValueField(24, 2, name: "OBDTY (GTIOCnB Output Duty Setting)")
                    .WithFlag(26, name: "OBDTYF (Forcible GTIOCnB Output Duty Setting)")
                    .WithFlag(27, name: "OBDTYR (GTIOCnB Output Value Selecting)")
                    .WithReservedBits(28, 4)
                },
                {channelOffset + (long)ChannelRegisters.IOControl, new DoubleWordRegister(this)
                    .WithValueField(0, 5, name: "GTIOA (GTIOCnA Pin Function Select)")
                    .WithReservedBits(5, 1)
                    .WithFlag(6, name: "OADFLT (GTIOCnA Pin Output Value Setting at the Count Stop)")
                    .WithFlag(7, name: "OAHLD (GTIOCnA Pin Output Value Setting at the Start/Stop Count)")
                    .WithFlag(8, name: "OAE (GTIOCnA Pin Output Enable)")
                    .WithFlag(9, name: "OADF (GTIOCnA Pin Disaable Value Setting)")
                    .WithReservedBits(11, 2)
                    .WithFlag(13, name: "NFAEN (Noise Filter A Enable)")
                    .WithValueField(14, 2, name: "NFCSA (Noise Filter A Sampling Clock Select)")
                    .WithValueField(16, 5, name: "GTIOB (GTIOCnB Pin Function Select)")
                    .WithReservedBits(21, 1)
                    .WithFlag(22, name: "OBDFLT (GTIOCnB Pin Output Value Setting at the Count Stop)")
                    .WithFlag(23, name: "OBHLD (GTIOCnB Pin Output Value Setting at the Start/Stop Count)")
                    .WithFlag(24, name: "OBE (GTIOCnB Pin Output Enable)")
                    .WithFlag(25, name: "OBDF (GTIOCnB Pin Disaable Value Setting)")
                    .WithReservedBits(27, 2)
                    .WithFlag(29, name: "NFBEN (Noise Filter B Enable)")
                    .WithValueField(30, 2, name: "NFCSB (Noise Filter B Sampling Clock Select)")
                },
                {channelOffset + (long)ChannelRegisters.InterruptOutputSetting, new DoubleWordRegister(this)
                    .WithReservedBits(0, 16)
                    .WithFlag(16, name: "ADTRAUEN (GTADTRA Register Compare Match (Up-Counting) A/D Conversion Start)")
                    .WithFlag(17, name: "ADTRADEN (GTADTRA Register Compare Match (Down-Counting) A/D Conversion Start)")
                    .WithFlag(18, name: "ADTRBUEN (GTADTRB Register Compare Match (Up-Counting) A/D Conversion Start)")
                    .WithFlag(19, name: "ADTRBDEN (GTADTRB Register Compare Match (Down-Counting) A/D Conversion Start)")
                    .WithReservedBits(20, 4)
                    .WithValueField(24, 2, name: "GRP (Output Disable Source Select)")
                    .WithReservedBits(26, 3)
                    .WithFlag(29, name: "GRPABH (Same Time Output Level High Disable Request Enable)")
                    .WithFlag(30, name: "GRPABL (Same Time Output Level Low Disable Request Enable)")
                    .WithReservedBits(31, 1)
                },
                // Status register defined later
                {channelOffset + (long)ChannelRegisters.BufferEnable, new DoubleWordRegister(this)
                    .WithFlag(0, name: "BD0 (GTCCR Buffer Operation Disable)")
                    .WithFlag(1, name: "BD1 (GTPR Buffer Operation Disable)")
                    .WithFlag(2, name: "BD2 (GTAADTRA/GTADTRB Registers Buffer Operation Disable)")
                    .WithReservedBits(3, 13)
                    .WithValueField(16, 2, name: "CCRA (GTCCRA Buffer Operation)")
                    .WithValueField(18, 2, name: "CCRB (GTCCRB Buffer Operation)")
                    .WithValueField(20, 2, name: "PR (GTPR Buffer Operation)")
                    .WithFlag(22, name: "CCRSWT (GTCCRA and GTCCRB Forcible Buffer Operation)")
                    .WithReservedBits(23, 1)
                    .WithValueField(24, 2, name: "ADTTA (GTADTRA Register Buffer Transfer Timing Select)")
                    .WithFlag(26, name: "ADTDA (GTADTRA Register Double Buffer Operation)")
                    .WithReservedBits(27, 1)
                    .WithValueField(28, 2, name: "ADTTB (GTADTRB Register Buffer Transfer Timing Select)")
                    .WithFlag(30, name: "ADTDB (GTADTRB Register Double Buffer Operation)")
                    .WithReservedBits(31, 1)
                },
                // Interrupt and A/D Converter Skipping Register defined later
                {channelOffset + (long)ChannelRegisters.Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTCNT",
                        valueProviderCallback: _ => channel.Value,
                        writeCallback: (_, value) => channel.Value = value
                    )
                },
                // Compare Capture register A..F defined later
                {channelOffset + (long)ChannelRegisters.CycleSetting, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTPR",
                        valueProviderCallback: _ => channel.Cycle,
                        writeCallback: (_, value) => channel.Cycle = value
                    )
                },
                {channelOffset + (long)ChannelRegisters.CycleSettingBuffer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTPBR")
                },
                {channelOffset + (long)ChannelRegisters.CycleSettingDoubleBuffer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTPDBR")
                },
                {channelOffset + (long)ChannelRegisters.ADConversionStartA, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTADTRA")
                },
                {channelOffset + (long)ChannelRegisters.ADConversionStartB, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTADTRB")
                },
                {channelOffset + (long)ChannelRegisters.ADConversionStartBufferA, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTADTBRA")
                },
                {channelOffset + (long)ChannelRegisters.ADConversionStartBufferB, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTADTBRB")
                },
                {channelOffset + (long)ChannelRegisters.ADConversionStartDoubleBufferA, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTADTDBRA")
                },
                {channelOffset + (long)ChannelRegisters.ADConversionStartDoubleBufferB, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTADTDBRB")
                },
                {channelOffset + (long)ChannelRegisters.DeadTimeControl, new DoubleWordRegister(this)
                    .WithFlag(0, name: "TDE (Negative-Phase Waveform Setting)")
                    .WithReservedBits(1, 3)
                    .WithFlag(4, name: "TDBUE")
                    .WithFlag(5, name: "TDBDE")
                    .WithReservedBits(6, 2)
                    .WithFlag(8, name: "TDFER")
                    .WithReservedBits(9, 23)
                },
                {channelOffset + (long)ChannelRegisters.DeadTimeValueU, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTDVU")
                },
                {channelOffset + (long)ChannelRegisters.DeadTimeValueD, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTDVD")
                },
                {channelOffset + (long)ChannelRegisters.DeadTimeBufferU, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTDBU")
                },
                {channelOffset + (long)ChannelRegisters.DeadTimeBufferD, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTDBD")
                },
                {channelOffset + (long)ChannelRegisters.OutputProtectionStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "SOS")
                    .WithReservedBits(2, 30)
                },
                {channelOffset + (long)ChannelRegisters.OutputProtectionTempRelease, new DoubleWordRegister(this)
                    .WithFlag(0, name: "SOTR")
                    .WithReservedBits(1, 31)
                },
            };

            var statusRegister = new DoubleWordRegister(this);
            foreach(var f in RangeWithLetters(0, 6))
            {
                statusRegister
                    .WithFlag(f.offset, name: $"TCF{f.c} (Input Capture/Compare Match Flag {f.c})");
            }
            statusRegister
                .WithFlag(6, FieldMode.Read | FieldMode.WriteZeroToClear, name: "TCFPO (Overflow Flag)",
                    valueProviderCallback: _ => channel.Overflow,
                    writeCallback: (_, __) => channel.Overflow = false
                )
                .WithFlag(7, name: "TCFPU (Underflow Flag)")
                .WithReservedBits(8, 7)
                .WithFlag(15, name: "TUCF (Count Direction Flag)")
                .WithFlag(16, name: "ADTRAUF (GTADTRA Register Compare Match (Up-Counting) A/D Conversion Start)")
                .WithFlag(17, name: "ADTRADF (GTADTRA Register Compare Match (Down-Counting) A/D Conversion Start)")
                .WithFlag(18, name: "ADTRBUF (GTADTRB Register Compare Match (Up-Counting) A/D Conversion Start)")
                .WithFlag(19, name: "ADTRBDF (GTADTRB Register Compare Match (Down-Counting) A/D Conversion Start)")
                .WithReservedBits(20, 4)
                .WithFlag(24, name: "ODF (Output Disable Flag)")
                .WithReservedBits(25, 4)
                .WithFlag(29, name: "OABHF (Same Time Output Level High Flag)")
                .WithFlag(30, name: "OABLF (Same Time Output Level Low Flag)")
                .WithFlag(31, name: "PCF (Period Count Function Finish Flag)");
            registerMap.Add(channelOffset + (long)ChannelRegisters.Status, statusRegister);

            var skippingSettingResiter = new DoubleWordRegister(this);
            foreach(var f in RangeWithLetters(0, 6))
            {
                skippingSettingResiter
                    .WithFlag(f.offset, name: $"ITL{f.c} (GTCCR{f.c} Compare Match/Input Capture Interrupt Link)");
            }
            skippingSettingResiter
                .WithValueField(6, 2, name: "IVTC (OVFn/UNFn Interrupt Skipping Function Select)")
                .WithValueField(8, 2, name: "IVTT (OVFn/UNFn Interrupt Skipping Count Select)")
                .WithReservedBits(11, 1)
                .WithFlag(12, name: "ADTAL (GTADTRA A/D Converter Start Request Link)")
                .WithReservedBits(13, 1)
                .WithFlag(14, name: "ADTBL (GTADTRB A/D Converter Start Request Link)")
                .WithReservedBits(15, 17);
            registerMap.Add(channelOffset + (long)ChannelRegisters.InterruptAndADConverterSkipping, skippingSettingResiter);

            foreach(var r in RangeWithLetters((int)channelOffset + (int)ChannelRegisters.CompareCaptureA, 6, 4))
            {
                var compareCaptureRegister = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: $"GTCCR{r.c}");
                registerMap.Add(r.offset, compareCaptureRegister);
            }

            return registerMap;
        }

        private DoubleWordRegister DefineSourceSelectRegister(string name, char marker, bool hasClear = true, string sourceName = "Counter")
        {
            var sourceSelectRegister = new DoubleWordRegister(this);
            foreach(var f in RangeWithLetters(0, 4, 2))
            {
                sourceSelectRegister
                    .WithFlag(f.offset,     name: $"{marker}SGTRG{f.c}R (GTETRG{f.c} Pin Rising Input Source {sourceName} {name} Enable)")
                    .WithFlag(f.offset + 1, name: $"{marker}SGTRG{f.c}F (GTETRG{f.c} Pin Falling Input Source {sourceName} {name} Enable)");
            }
            foreach(var f in RangeWithLetters(8, 2, 4))
            {
                var other = f.c == 'A' ? 'B' : 'A';
                sourceSelectRegister
                    .WithFlag(f.offset,     name: $"{marker}SC{f.c}RBL (GTIOCn{f.c} Pin Rising Input during GTIOCn{other} Value Low Source {sourceName} {name} Enable)")
                    .WithFlag(f.offset + 1, name: $"{marker}SC{f.c}RBH (GTIOCn{f.c} Pin Rising Input during GTIOCn{other} Value High Source {sourceName} {name} Enable)")
                    .WithFlag(f.offset + 2, name: $"{marker}SC{f.c}FBL (GTIOCn{f.c} Pin Falling Input during GTIOCn{other} Value Low Source {sourceName} {name} Enable)")
                    .WithFlag(f.offset + 3, name: $"{marker}SC{f.c}FBH (GTIOCn{f.c} Pin Falling Input during GTIOCn{other} Value High Source {sourceName} {name} Enable)");
            }
            if(hasClear)
            {
                sourceSelectRegister
                    .WithReservedBits(16, 15)
                    .WithFlag(31, name: $"C{name.ToUpper()} (Software Source {sourceName} {name} Enable)");
            }
            else
            {
                sourceSelectRegister
                    .WithReservedBits(16, 16);
            }
            return sourceSelectRegister;
        }

        private class OffsetWithLetter
        {
            public int index;
            public int offset;
            public char c;

            public OffsetWithLetter(int index, int offset, char c)
            {
                this.index = index;
                this.offset = offset;
                this.c = c;
            }
        }

        private IEnumerable<OffsetWithLetter> RangeWithLetters(int start, int count, int step = 1)
        {
            for(int i = 0; i < count; ++i)
            {
                int offset = start + i * step;
                var c = (char)('A' + i);
                yield return new OffsetWithLetter(i, offset, c);
            }
        }

        private int TotalChannels => numberOf32BitChannels;

        private readonly IMachine machine;
        private readonly int numberOf32BitChannels;
        private readonly long peripheralClockDFrequency;
        private readonly GPTChannel[] channels;

        private class GPTChannel
        {
            public GPTChannel(RenesasRZG_GPT parent, long index, int width)
            {
                this.parent = parent;
                this.index = index;
                this.width = width;
                IRQ = new GPIO[InterruptCount];
                for(int i = 0; i < InterruptCount; ++i)
                {
                    IRQ[i] = new GPIO();
                }

                timer = new LimitTimer(parent.machine.ClockSource, parent.peripheralClockDFrequency, parent, $"Timer{index}",
                    MaxLimit, direction: TimeDirection.Descending, workMode: WorkMode.Periodic, eventEnabled: true
                );
                timer.LimitReached += OnMainTimerLimitReached;

                Reset();
            }

            public void Reset()
            {
                Overflow = false;
                timer.Reset();
                Cycle = MaxLimit;
                Direction = Direction.DownCounting;
                Mode = Mode.SawWave;
            }

            public Mode Mode
            {
                get => mode;
                set
                {
                    if(value != Mode.SawWave)
                    {
                        this.parent.Log(LogLevel.Warning, "GPT{0}: Modes other than Saw Wave (default) are not supported yet. Ignoring", index);
                        return;
                    }
                    mode = value;
                }
            }

            public Direction Direction
            {
                get => direction;
                set
                {
                    direction = value;
                    timer.Direction = direction == Direction.UpCounting ? TimeDirection.Ascending : TimeDirection.Descending;
                }
            }

            public bool Enable
            {
                get => timer.Enabled;
                set => timer.Enabled = value;
                
            }

            public ulong Cycle
            {
                get => timer.Limit;
                set
                {
                    if(value > MaxLimit)
                    {
                        this.parent.Log(LogLevel.Warning, "GPT{0}: Cycle {1} is higher than maximum limit of {2}. Truncating to {3} bits", index, value, MaxLimit, width);
                    }
                    timer.Limit = value.Clamp(0u, MaxLimit);
                }
            }

            public ulong Value
            {
                get => timer.Value;
                set
                {
                    if(Enable)
                    {
                        this.parent.Log(LogLevel.Warning, "GPT{0}: Setting GTCNT while counting is still on-going. Ignoring", index);
                        return;
                    }
                    if(value > MaxLimit)
                    {
                        this.parent.Log(LogLevel.Warning, "GPT{0}: Value {1} is higher than maximum limit of {2}. Truncating to {3} bits", index, value, MaxLimit, width);
                    }
                    var newValue = value.Clamp(0u, MaxLimit);
                    if(newValue >= Cycle)
                    {
                        Overflow = true;
                        IRQ[OverflowInterruptIndex].Blink();
                        timer.Reset();
                    }
                    else
                    {
                        Overflow = false;
                        timer.Value = newValue;
                    }
                }
            }

            public GPIO[] IRQ { get; }

            public bool Overflow { get; set; }
            public bool Underflow { get; set; }

            public const long InterruptCount = 10;

            private void OnMainTimerLimitReached()
            {
                if(direction == Direction.UpCounting)
                {
                    Overflow = true;
                    IRQ[OverflowInterruptIndex].Blink();
                }
                else
                {
                    Underflow = true;
                    IRQ[UnderflowInterruptIndex].Blink();
                }
            }

            private ulong MaxLimit => (1ul << width) - 1ul;

            private Mode mode;
            private Direction direction;

            private readonly LimitTimer timer;
            private readonly RenesasRZG_GPT parent;
            private readonly long index;
            private readonly int width;

            private const long OverflowInterruptIndex = 8;
            private const long UnderflowInterruptIndex = 9;
        }

        public enum Mode
        {
            // single buffer or double buffer possible
            SawWave        = 0b000,
            // fixed buffer operation
            SawWaveOneShot = 0b001,
            // single buffer or double buffer possible
            // 32-bit transfer at trough
            TriangleWave1  = 0b100,
            // single buffer or double buffer possible
            // 32-bit transfer at crest and trough
            TriangleWave2  = 0b101,
            // fixed buffer operation
            // 64-bit transfer at trough
            TriangleWave3  = 0b110,
        }

        public enum Direction
        {
            DownCounting = 0,
            UpCounting   = 1,
        }

        public enum ChannelRegisters
        {
            WriteProtection                   = 0x00, // GTWP
            SoftwareStart                     = 0x04, // GTSTR
            SoftwareStop                      = 0x08, // GTSTP
            SoftwareClear                     = 0x0C, // GTCLR
            StartSourceSelect                 = 0x10, // GTSSR
            StopSourceSelect                  = 0x14, // GTPSR
            ClearSourceSelect                 = 0x18, // GTCSR
            UpCountSourceSelect               = 0x1C, // GTUPSR
            DownCountSourceSelect             = 0x20, // GTDNSR
            InputCaptureSourceSelectA         = 0x24, // GTICASR
            InputCaptureSourceSelectB         = 0x28, // GTICBSR
            TimerControl                      = 0x2C, // GTCR
            CountDirectionAndDutySetting      = 0x30, // GTUDDTYC
            IOControl                         = 0x34, // GTIOR
            InterruptOutputSetting            = 0x38, // GTINTAD
            Status                            = 0x3C, // GTST
            BufferEnable                      = 0x40, // GTBER
            InterruptAndADConverterSkipping   = 0x44, // GTITC
            Counter                           = 0x48, // GTCNT
            CompareCaptureA                   = 0x4C, // GTCCRA
            // CompareCaptureB..F             = 0x50..0x60, GTCCRB..F
            CycleSetting                      = 0x64, // GTPR
            CycleSettingBuffer                = 0x68, // GTPBR
            CycleSettingDoubleBuffer          = 0x6C, // GTPDBR
            ADConversionStartA                = 0x70, // GTADTRA
            ADConversionStartBufferA          = 0x74, // GTADTBRA
            ADConversionStartDoubleBufferA    = 0x78, // GTADTDBRA
            ADConversionStartB                = 0x7C, // GTADTRB
            ADConversionStartBufferB          = 0x80, // GTADTBRB
            ADConversionStartDoubleBufferB    = 0x84, // GTADTDBRB
            DeadTimeControl                   = 0x88, // GTDTCR
            DeadTimeValueU                    = 0x8C, // GTDVU
            DeadTimeValueD                    = 0x90, // GTDVD
            DeadTimeBufferU                   = 0x94, // GTDBU
            DeadTimeBufferD                   = 0x98, // GTDBD
            OutputProtectionStatus            = 0x9C, // GTSOS
            OutputProtectionTempRelease       = 0xA0, // GTSOTR
        }
    }
}
