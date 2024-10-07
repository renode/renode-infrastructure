//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;

using TimeDirection = Antmicro.Renode.Time.Direction;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class RenesasRA_GPT : IDoubleWordPeripheral, INumberedGPIOOutput, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasRA_GPT(IMachine machine, int numberOf32BitChannels, int numberOf16BitChannels, long commonRegistersOffset,
            long peripheralClockDFrequency)
        {
            this.peripheralClockDFrequency = peripheralClockDFrequency;
            this.machine = machine;

            this.numberOf32BitChannels = numberOf32BitChannels;
            this.numberOf16BitChannels = numberOf16BitChannels;
            this.channels = new GPTChannel[TotalChannels];

            Size = commonRegistersOffset + 0x100;
            RegistersCollection = new DoubleWordRegisterCollection(this, BuildRegisterMap(commonRegistersOffset));

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

        // IRQs are bundled in 9 signals per channel in the following order:
        // 0: CCMPA - ompare match / input capture A
        // 1: CCMPB - compare match / input capture B
        // 2: CMPC - compare match C
        // 3: CMPD - compare match D
        // 4: CMPE - compare match E
        // 5: CMPF - compare match F
        // 6: OVF - overflow
        // 7: UDF - underflow
        // 8: PC - count stop
        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap(long commonRegistersOffset)
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {commonRegistersOffset + (long)CommonRegisters.OutputPhaseSwitchingControl, new DoubleWordRegister(this)
                    .WithFlag(0, name: "UF (Input Phase Soft Setting)")
                    .WithFlag(1, name: "VF (Input Phase Soft Setting)")
                    .WithFlag(2, name: "WF (Input Phase Soft Setting)")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, name: "U (Input U-Phase Monitor)")
                    .WithFlag(5, name: "V (Input V-Phase Monitor)")
                    .WithFlag(6, name: "W (Input W-Phase Monitor)")
                    .WithReservedBits(7, 1)
                    .WithFlag(8, name: "EN (Output Phase Enable)")
                    .WithReservedBits(9, 7)
                    .WithFlag(16, name: "FB (External Feedback Signal Enable)")
                    .WithFlag(17, name: "P (Positive-Phase Output (P) Control)")
                    .WithFlag(18, name: "N (Negative-Phase Output (N) Control)")
                    .WithFlag(19, name: "INV (Output Phase Invert Control)")
                    .WithFlag(20, name: "RV (Output Phase Rotation Direction Reversal Control)")
                    .WithFlag(21, name: "ALIGN (Input Phase Alignment)")
                    .WithReservedBits(22, 2)
                    .WithValueField(24, 2, name: "GRP (Output Disabled Source Selection)")
                    .WithFlag(26, name: "GODF (Group Output Disable Function)")
                    .WithReservedBits(27, 2)
                    .WithFlag(29, name: "NFEN (External Input Noise Filter Enable)")
                    .WithValueField(30, 2, name: "NFCS (External Input Noise Filter Clock Selection)")
                },
            };

            for(long i = 0; i < numberOf32BitChannels; ++i)
            {
                var channel = new GPTChannel(this, i, 32);
                channels[i] = channel;
                registerMap = registerMap
                    .Concat(BuildChannelRegisterMap(0x100 * i, channel))
                    .ToDictionary(x => x.Key, x => x.Value);
            }

            for(long i = 0; i < numberOf16BitChannels; ++i)
            {
                // 16-bit channels are numbered after 32-bit channels,
                // i.e. if 32-bit channels are 0 to 7 then 16-bit are 8 to 13.
                var channelNumber = numberOf32BitChannels + i;
                var channel = new GPTChannel(this, channelNumber, 16);
                channels[channelNumber] = channel;
                registerMap = registerMap
                    .Concat(BuildChannelRegisterMap(0x100 * channelNumber, channel))
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
                    .WithFlag(1, name: "STRWP (CSTRT Bit Write Disable)")
                    .WithFlag(2, name: "STPWP (CSTOP Bit Write Disable)")
                    .WithFlag(3, name: "CLRWP (CCLR Bit Write Disable)")
                    .WithFlag(4, name: "CMNWP (Common Register Write Disable)")
                    .WithReservedBits(5, 3)
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
                    .WithReservedBits(1, 31)
                },
                {channelOffset + (long)ChannelRegisters.DeadTimeValue, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "GTDVU")
                },
                {channelOffset + (long)ChannelRegisters.ADConversionStartSignalMonitoring, new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "ADSMS0 (A/D Conversion Start Request Signal Monitor 0 Selection)")
                    .WithReservedBits(2, 6)
                    .WithFlag(8, name: "ADSMEN0 (A/D Conversion Start Request Signal Monitor 0 Output Enabling)")
                    .WithReservedBits(9, 7)
                    .WithValueField(16, 2, name: "ADSMS1 (A/D Conversion Start Request Signal Monitor 1 Selection)")
                    .WithReservedBits(18, 6)
                    .WithFlag(24, name: "ADSMEN1 (A/D Conversion Start Request Signal Monitor 1 Output Enabling)")
                    .WithReservedBits(25, 7)
                },
                {channelOffset + (long)ChannelRegisters.InterChannelSetting, new DoubleWordRegister(this)
                    .WithValueField(0, 3, name: "ICLFA (GTIOCnA Output Logical Operation Function Select)")
                    .WithReservedBits(3, 1)
                    .WithValueField(4, 6, name: "ICLFSELC (Inter Channel Signaal C Select)")
                    .WithReservedBits(10, 6)
                    .WithValueField(16, 3, name: "ICLFB (GTIOCnB Output Logical Operation Function Select)")
                    .WithReservedBits(19, 1)
                    .WithValueField(20, 6, name: "ICLFSELD (Inter Channel Signal D Select)")
                    .WithReservedBits(26, 6)
                },
                {channelOffset + (long)ChannelRegisters.PeriodCount, new DoubleWordRegister(this)
                    .WithFlag(0, name: "PCEN (Period Count Function Enable)")
                    .WithReservedBits(1, 7)
                    .WithFlag(8, name: "ASTP (Automatic Stop Function Enable)")
                    .WithReservedBits(9, 7)
                    .WithValueField(16, 12, name: "PCNT (Period Counter)")
                    .WithReservedBits(28, 4)
                },
                {channelOffset + (long)ChannelRegisters.OperationEnableBitChannelSelect, new DoubleWordRegister(this)
                    .WithFlags(0, 14, name: "SECSEL")
                    .WithReservedBits(14, 18)
                },
                {channelOffset + (long)ChannelRegisters.OperationEnableBit, new DoubleWordRegister(this)
                    .WithFlag(0, name: "SBDCE (GTCCR Register Buffer Operation Simultaneous Enable)")
                    .WithFlag(1, name: "SBDPE (GTPR Register Buffer Operation Simultaneous Enable)")
                    .WithFlag(2, name: "SBDAE (GTADTR Register Buffer Operation Simultaneous Enable)")
                    .WithReservedBits(3, 5)
                    .WithFlag(8, name: "SBDCD (GTCCR Register Buffer Operation Simultaneous Disable)")
                    .WithFlag(9, name: "SBDPD (GTPR Register Buffer Operation Simultaneous Disable)")
                    .WithFlag(10, name: "SBDAD (GTADTR Register Buffer Operation Simultaneous Disable)")
                    .WithReservedBits(17, 7)
                    .WithReservedBits(25, 7)
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
            foreach(var f in RangeWithLetters(16, 8))
            {
                sourceSelectRegister
                    .WithFlag(f.offset, name: $"{marker}SELC{f.c} (ELC_GPT{f.c} Event Source {sourceName} {name} Enable)");
            }
            if(hasClear)
            {
                sourceSelectRegister
                    .WithReservedBits(24, 7)
                    .WithFlag(31, name: $"C{name.ToUpper()} (Software Source {sourceName} {name} Enable)");
            }
            else
            {
                sourceSelectRegister
                    .WithReservedBits(24, 8);
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

        private int TotalChannels => numberOf32BitChannels + numberOf16BitChannels;

        private readonly IMachine machine;
        private readonly int numberOf32BitChannels;
        private readonly int numberOf16BitChannels;
        private readonly long peripheralClockDFrequency;
        private readonly GPTChannel[] channels;

        private class GPTChannel
        {
            public GPTChannel(RenesasRA_GPT parent, long index, int width)
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
                set
                {
                    timer.Enabled = value;
                }
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
                    timer.Limit = value & MaxLimit;
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
                    var newValue = value & MaxLimit;
                    if(newValue >= Cycle)
                    {
                        Overflow = true;
                        IRQ[OvfInterruptIndex].Blink();
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

            public const long InterruptCount = 9;

            private void OnMainTimerLimitReached()
            {
                if(direction == Direction.UpCounting)
                {
                    Overflow = true;
                    IRQ[OvfInterruptIndex].Blink();
                }
                else
                {
                    Underflow = true;
                    IRQ[UdfInterruptIndex].Blink();
                }
            }

            private ulong MaxLimit => (1ul << width) - 1ul;

            private Mode mode;
            private Direction direction;

            private readonly LimitTimer timer;
            private readonly RenesasRA_GPT parent;
            private readonly long index;
            private readonly int width;

            private const long OvfInterruptIndex = 6;
            private const long UdfInterruptIndex = 7;
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
            WriteProtection                   = 0x00,
            SoftwareStart                     = 0x04,
            SoftwareStop                      = 0x08,
            SoftwareClear                     = 0x0C,
            StartSourceSelect                 = 0x10,
            StopSourceSelect                  = 0x14,
            ClearSourceSelect                 = 0x18,
            UpCountSourceSelect               = 0x1C,
            DownCountSourceSelect             = 0x20,
            InputCaptureSourceSelectA         = 0x24,
            InputCaptureSourceSelectB         = 0x28,
            TimerControl                      = 0x2C,
            CountDirectionAndDutySetting      = 0x30,
            IOControl                         = 0x34,
            InterruptOutputSetting            = 0x38,
            Status                            = 0x3C,
            BufferEnable                      = 0x40,
            Counter                           = 0x48,
            CompareCaptureA                   = 0x4C,
            // CompareCaptureB..F             = 0x50..0x60,
            CycleSetting                      = 0x64,
            CycleSettingBuffer                = 0x68,
            ADConversionStartA                = 0x70,
            ADConversionStartB                = 0x7C,
            ADConversionStartBufferA          = 0x74,
            ADConversionStartBufferB          = 0x80,
            ADConversionStartDoubleBufferA    = 0x78,
            ADConversionStartDoubleBufferB    = 0x84,
            DeadTimeControl                   = 0x88,
            DeadTimeValue                     = 0x8C,
            ADConversionStartSignalMonitoring = 0xA4,
            InterChannelSetting               = 0xB8,
            PeriodCount                       = 0xBC,
            OperationEnableBitChannelSelect   = 0xD0,
            OperationEnableBit                = 0xD4,
        }

        public enum CommonRegisters
        {
            OutputPhaseSwitchingControl       = 0x00,
        }
    }
}
