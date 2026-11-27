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
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class SiLabs_IADC_1 : SiLabsPeripheral
    {
        public SiLabs_IADC_1(Machine machine, uint defaultSampleValue = 0x800) : base(machine, buildRegisters: false)
        {
            IRQ = new GPIO();
            SingleDmaRequest = new GPIO();
            ScanDmaRequest = new GPIO();
            singleFifo = new Queue<uint>();
            scanFifo = new Queue<uint>();
            inputSamples = new Dictionary<uint, uint>();
            scanEntries = new uint[ScanTableEntryCount];
            DefaultSampleValue = defaultSampleValue & Sample12BitMask;

            registersCollection = BuildRegistersCollection();
            Reset();
        }

        public override void Reset()
        {
            singleFifo.Clear();
            scanFifo.Clear();
            Array.Clear(scanEntries, 0, scanEntries.Length);
            singleQueueEnabled = false;
            scanQueueEnabled = false;
            timerActive = false;
            lastSingleData = 0;
            lastScanData = 0;

            base.Reset();
            UpdateDmaSignals();
        }

        public void SetInputSample(uint portPos, uint pinPos, uint sample12Bit)
        {
            inputSamples[MakeInputKey(portPos, pinPos)] = sample12Bit & Sample12BitMask;
        }

        public void SetInputVoltageMillivolts(uint portPos, uint pinPos, uint millivolts, uint vrefMillivolts = 1210)
        {
            SetInputSample(portPos, pinPos, MillivoltsToSample12Bit(millivolts, vrefMillivolts));
        }

        public void SetDefaultVoltageMillivolts(uint millivolts, uint vrefMillivolts = 1210)
        {
            DefaultSampleValue = MillivoltsToSample12Bit(millivolts, vrefMillivolts);
        }

        public void ClearInputSamples()
        {
            inputSamples.Clear();
        }

        public GPIO IRQ { get; }

        public GPIO SingleDmaRequest { get; }

        public GPIO ScanDmaRequest { get; }

        public uint DefaultSampleValue
        {
            get => defaultSampleValue;
            set => defaultSampleValue = value & Sample12BitMask;
        }

        public override bool Enabled
        {
            get => enabledFlag.Value;
            set => enabledFlag.Value = value;
        }

        protected override Type RegistersType => typeof(Registers);

        protected override DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.IpVersion, new DoubleWordRegister(this, 0x00000003)
                    .WithValueField(0, 32, FieldMode.Read, name: "IPVERSION")
                },
                {(long)Registers.Enable, new DoubleWordRegister(this)
                    .WithFlag(0, out enabledFlag, name: "EN",
                        writeCallback: (_, value) =>
                        {
                            if(!value)
                            {
                                singleQueueEnabled = false;
                                scanQueueEnabled = false;
                                timerActive = false;
                            }
                        })
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "DISABLING")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, name: "EM23WUCONVERT")
                    .WithFlag(1, name: "ADCCLKSUSPEND0")
                    .WithFlag(2, name: "ADCCLKSUSPEND1")
                    .WithFlag(3, name: "DBGHALT")
                    .WithValueField(4, 2, name: "WARMUPMODE")
                    .WithReservedBits(6, 10)
                    .WithValueField(16, 7, name: "TIMEBASE")
                    .WithReservedBits(23, 5)
                    .WithValueField(28, 3, name: "HSCLKRATE")
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "SINGLESTART",
                        writeCallback: (_, value) =>
                        {
                            if(value && Enabled)
                            {
                                singleQueueEnabled = true;
                                if(singleTrigSel.Value == TriggerSelect.Immediate)
                                {
                                    PerformSingleConversion();
                                    if(singleTrigAction.Value == TriggerAction.Once)
                                    {
                                        singleQueueEnabled = false;
                                    }
                                }
                            }
                        })
                    .WithFlag(1, FieldMode.Write, name: "SINGLESTOP",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                singleQueueEnabled = false;
                            }
                        })
                    .WithReservedBits(2, 1)
                    .WithFlag(3, FieldMode.Write, name: "SCANSTART",
                        writeCallback: (_, value) =>
                        {
                            if(value && Enabled)
                            {
                                scanQueueEnabled = true;
                                if(scanTrigSel.Value == TriggerSelect.Immediate)
                                {
                                    PerformScanConversion();
                                    if(scanTrigAction.Value == TriggerAction.Once)
                                    {
                                        scanQueueEnabled = false;
                                    }
                                }
                            }
                        })
                    .WithFlag(4, FieldMode.Write, name: "SCANSTOP",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                scanQueueEnabled = false;
                            }
                        })
                    .WithReservedBits(5, 11)
                    .WithFlag(16, FieldMode.Write, name: "TIMEREN",
                        writeCallback: (_, value) =>
                        {
                            if(value && Enabled)
                            {
                                timerActive = true;
                            }
                        })
                    .WithFlag(17, FieldMode.Write, name: "TIMERDIS",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                timerActive = false;
                            }
                        })
                    .WithReservedBits(18, 6)
                    .WithFlag(24, FieldMode.Write, name: "SINGLEFIFOFLUSH",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                singleFifo.Clear();
                                UpdateSingleFifoFlags();
                            }
                        })
                    .WithFlag(25, FieldMode.Write, name: "SCANFIFOFLUSH",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                scanFifo.Clear();
                                UpdateScanFifoFlags();
                            }
                        })
                    .WithReservedBits(26, 6)
                },
                {(long)Registers.Timer, new DoubleWordRegister(this)
                    .WithValueField(0, 16, name: "TIMER")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => singleQueueEnabled, name: "SINGLEQEN")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "SINGLEQUEUEPENDING")
                    .WithReservedBits(2, 1)
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => scanQueueEnabled, name: "SCANQEN")
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "SCANQUEUEPENDING")
                    .WithReservedBits(5, 1)
                    .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => false, name: "CONVERTING")
                    .WithReservedBits(7, 1)
                    .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => singleFifo.Count > 0, name: "SINGLEFIFODV")
                    .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => scanFifo.Count > 0, name: "SCANFIFODV")
                    .WithReservedBits(10, 4)
                    .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => false, name: "SINGLEFIFOFLUSHING")
                    .WithFlag(15, FieldMode.Read, valueProviderCallback: _ => false, name: "SCANFIFOFLUSHING")
                    .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => timerActive, name: "TIMERACTIVE")
                    .WithReservedBits(17, 3)
                    .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => false, name: "SINGLEWRITEPENDING")
                    .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => false, name: "MASKREQWRITEPENDING")
                    .WithReservedBits(22, 2)
                    .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => false, name: "SYNCBUSY")
                    .WithReservedBits(25, 5)
                    .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => Enabled, name: "ADCWARM")
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.MaskRequest, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out maskRequestField, name: "MASKREQ")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.ScanTableMask, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => maskRequestField.Value, name: "STMASK")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.CompareThreshold, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out cmpThresholdLessField, name: "ADLT")
                    .WithValueField(16, 16, out cmpThresholdGreaterField, name: "ADGT")
                },
                {(long)Registers.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out ifSingleFifoDvl, name: "SINGLEFIFODVL")
                    .WithFlag(1, out ifScanFifoDvl, name: "SCANFIFODVL")
                    .WithFlag(2, out ifSingleCmp, name: "SINGLECMP")
                    .WithFlag(3, out ifScanCmp, name: "SCANCMP")
                    .WithReservedBits(4, 3)
                    .WithFlag(7, out ifScanEntryDone, name: "SCANENTRYDONE")
                    .WithFlag(8, out ifScanTableDone, name: "SCANTABLEDONE")
                    .WithFlag(9, out ifSingleDone, name: "SINGLEDONE")
                    .WithReservedBits(10, 2)
                    .WithFlag(12, out ifPolarityErr, name: "POLARITYERR")
                    .WithFlag(13, out ifPortAllocErr, name: "PORTALLOCERR")
                    .WithReservedBits(14, 2)
                    .WithFlag(16, out ifSingleFifoOf, name: "SINGLEFIFOOF")
                    .WithFlag(17, out ifScanFifoOf, name: "SCANFIFOOF")
                    .WithFlag(18, out ifSingleFifoUf, name: "SINGLEFIFOUF")
                    .WithFlag(19, out ifScanFifoUf, name: "SCANFIFOUF")
                    .WithReservedBits(20, 11)
                    .WithFlag(31, out ifEm23AbortErr, name: "EM23ABORTERROR")
                    .WithWriteCallback((_, __) =>
                    {
                        if(singleFifo.Count >= (int)singleFifoDvl.Value + 1)
                        {
                            ifSingleFifoDvl.Value = true;
                        }
                        if(scanFifo.Count >= (int)scanFifoDvl.Value + 1)
                        {
                            ifScanFifoDvl.Value = true;
                        }
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out ienSingleFifoDvl, name: "SINGLEFIFODVL")
                    .WithFlag(1, out ienScanFifoDvl, name: "SCANFIFODVL")
                    .WithFlag(2, out ienSingleCmp, name: "SINGLECMP")
                    .WithFlag(3, out ienScanCmp, name: "SCANCMP")
                    .WithReservedBits(4, 3)
                    .WithFlag(7, out ienScanEntryDone, name: "SCANENTRYDONE")
                    .WithFlag(8, out ienScanTableDone, name: "SCANTABLEDONE")
                    .WithFlag(9, out ienSingleDone, name: "SINGLEDONE")
                    .WithReservedBits(10, 2)
                    .WithFlag(12, out ienPolarityErr, name: "POLARITYERR")
                    .WithFlag(13, out ienPortAllocErr, name: "PORTALLOCERR")
                    .WithReservedBits(14, 2)
                    .WithFlag(16, out ienSingleFifoOf, name: "SINGLEFIFOOF")
                    .WithFlag(17, out ienScanFifoOf, name: "SCANFIFOOF")
                    .WithFlag(18, out ienSingleFifoUf, name: "SINGLEFIFOUF")
                    .WithFlag(19, out ienScanFifoUf, name: "SCANFIFOUF")
                    .WithReservedBits(20, 11)
                    .WithFlag(31, out ienEm23AbortErr, name: "EM23ABORTERROR")
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Trigger, new DoubleWordRegister(this)
                    .WithEnumField(0, 3, out scanTrigSel, name: "SCANTRIGSEL")
                    .WithReservedBits(3, 1)
                    .WithEnumField(4, 1, out scanTrigAction, name: "SCANTRIGACTION")
                    .WithReservedBits(5, 3)
                    .WithEnumField(8, 3, out singleTrigSel, name: "SINGLETRIGSEL")
                    .WithReservedBits(11, 1)
                    .WithEnumField(12, 1, out singleTrigAction, name: "SINGLETRIGACTION")
                    .WithReservedBits(13, 3)
                    .WithFlag(16, name: "SINGLETAILGATE")
                    .WithReservedBits(17, 15)
                },
                {(long)Registers.Config0, BuildConfigRegister("CFG0")},
                {(long)Registers.Scale0, BuildScaleRegister("SCALE0")},
                {(long)Registers.Schedule0, BuildScheduleRegister("SCHED0")},
                {(long)Registers.Config1, BuildConfigRegister("CFG1")},
                {(long)Registers.Scale1, BuildScaleRegister("SCALE1")},
                {(long)Registers.Schedule1, BuildScheduleRegister("SCHED1")},
                {(long)Registers.SingleFifoConfig, new DoubleWordRegister(this, 0x00000030)
                    .WithEnumField(0, 3, out singleFifoAlignment, name: "ALIGNMENT")
                    .WithFlag(3, out singleFifoShowId, name: "SHOWID")
                    .WithValueField(4, 3, out singleFifoDvl, name: "DVL")
                    .WithReservedBits(7, 1)
                    .WithFlag(8, name: "DMAWUFIFOSINGLE")
                    .WithReservedBits(9, 23)
                    .WithWriteCallback((_, __) => UpdateSingleFifoFlags())
                },
                {(long)Registers.SingleFifoData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read,
                        valueProviderCallback: _ => ReadSingleFifoData(),
                        name: "DATA")
                },
                {(long)Registers.SingleFifoStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 4, FieldMode.Read,
                        valueProviderCallback: _ => (ulong)singleFifo.Count,
                        name: "FIFOREADCNT")
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.SingleData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read,
                        valueProviderCallback: _ => lastSingleData,
                        name: "DATA")
                },
                {(long)Registers.ScanFifoConfig, new DoubleWordRegister(this, 0x00000030)
                    .WithEnumField(0, 3, out scanFifoAlignment, name: "ALIGNMENT")
                    .WithFlag(3, out scanFifoShowId, name: "SHOWID")
                    .WithValueField(4, 3, out scanFifoDvl, name: "DVL")
                    .WithReservedBits(7, 1)
                    .WithFlag(8, name: "DMAWUFIFOSCAN")
                    .WithReservedBits(9, 23)
                    .WithWriteCallback((_, __) => UpdateScanFifoFlags())
                },
                {(long)Registers.ScanFifoData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read,
                        valueProviderCallback: _ => ReadScanFifoData(),
                        name: "DATA")
                },
                {(long)Registers.ScanFifoStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 4, FieldMode.Read,
                        valueProviderCallback: _ => (ulong)scanFifo.Count,
                        name: "FIFOREADCNT")
                    .WithReservedBits(4, 28)
                },
                {(long)Registers.ScanData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read,
                        valueProviderCallback: _ => lastScanData,
                        name: "DATA")
                },
                {(long)Registers.Single, new DoubleWordRegister(this)
                    .WithValueField(0, 4, out singlePinNeg, name: "PINNEG")
                    .WithValueField(4, 4, out singlePortNeg, name: "PORTNEG")
                    .WithValueField(8, 4, out singlePinPos, name: "PINPOS")
                    .WithValueField(12, 4, out singlePortPos, name: "PORTPOS")
                    .WithFlag(16, name: "CFG")
                    .WithFlag(17, out singleCmpEnable, name: "CMP")
                    .WithReservedBits(18, 14)
                },
            };

            for(var i = 0; i < ScanTableEntryCount; i++)
            {
                var entryIndex = i;
                var offset = (long)Registers.ScanTable0 + (4 * entryIndex);
                registerDictionary[offset] = new DoubleWordRegister(this)
                    .WithValueField(0, 18,
                        valueProviderCallback: _ => scanEntries[entryIndex],
                        writeCallback: (_, value) => scanEntries[entryIndex] = (uint)value,
                        name: $"SCAN{entryIndex}")
                    .WithReservedBits(18, 14);
            }

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override void UpdateInterrupts()
        {
            var pending =
                (ifSingleFifoDvl.Value && ienSingleFifoDvl.Value) ||
                (ifScanFifoDvl.Value && ienScanFifoDvl.Value) ||
                (ifSingleCmp.Value && ienSingleCmp.Value) ||
                (ifScanCmp.Value && ienScanCmp.Value) ||
                (ifScanEntryDone.Value && ienScanEntryDone.Value) ||
                (ifScanTableDone.Value && ienScanTableDone.Value) ||
                (ifSingleDone.Value && ienSingleDone.Value) ||
                (ifPolarityErr.Value && ienPolarityErr.Value) ||
                (ifPortAllocErr.Value && ienPortAllocErr.Value) ||
                (ifSingleFifoOf.Value && ienSingleFifoOf.Value) ||
                (ifScanFifoOf.Value && ienScanFifoOf.Value) ||
                (ifSingleFifoUf.Value && ienSingleFifoUf.Value) ||
                (ifScanFifoUf.Value && ienScanFifoUf.Value) ||
                (ifEm23AbortErr.Value && ienEm23AbortErr.Value);

            IRQ.Set(pending);
        }

        private static uint MakeInputKey(uint portPos, uint pinPos)
        {
            return ((portPos & 0xFU) << 4) | (pinPos & 0xFU);
        }

        private static uint MillivoltsToSample12Bit(uint millivolts, uint vrefMillivolts)
        {
            if(vrefMillivolts == 0)
            {
                return 0;
            }
            var raw = ((ulong)millivolts * Sample12BitMask) / vrefMillivolts;
            return raw > Sample12BitMask ? Sample12BitMask : (uint)raw;
        }

        private DoubleWordRegister BuildConfigRegister(string name)
        {
            return new DoubleWordRegister(this)
                .WithValueField(0, 32, name: name);
        }

        private DoubleWordRegister BuildScaleRegister(string name)
        {
            return new DoubleWordRegister(this, 0x8002C000)
                .WithValueField(0, 18, name: $"{name}_OFFSET")
                .WithValueField(18, 13, name: $"{name}_GAIN13LSB")
                .WithFlag(31, name: $"{name}_GAIN3MSB");
        }

        private DoubleWordRegister BuildScheduleRegister(string name)
        {
            return new DoubleWordRegister(this)
                .WithValueField(0, 10, name: $"{name}_PRESCALE")
                .WithReservedBits(10, 22);
        }

        private uint GetSample12Bit(uint portPos, uint pinPos)
        {
            if(inputSamples.TryGetValue(MakeInputKey(portPos, pinPos), out var sample))
            {
                return sample;
            }
            return defaultSampleValue;
        }

        private uint FormatSample(uint sample12Bit, Alignment alignment, bool showId, byte id)
        {
            var sample = sample12Bit & Sample12BitMask;
            uint idMask = showId ? id : 0u;
            switch(alignment)
            {
            case Alignment.Right12:
                return sample | (idMask << 24);
            case Alignment.Right16:
                return (sample << 4) | (idMask << 24);
            case Alignment.Right20:
                return (sample << 8) | (idMask << 24);
            case Alignment.Left12:
                return (sample << 20) | idMask;
            case Alignment.Left16:
                return (sample << 16) | idMask;
            case Alignment.Left20:
                return (sample << 12) | idMask;
            default:
                return sample | (idMask << 24);
            }
        }

        private bool IsWithinCompareWindow(uint sample12Bit)
        {
            var scaled16 = sample12Bit << 4;
            var adlt = (uint)cmpThresholdLessField.Value;
            var adgt = (uint)cmpThresholdGreaterField.Value;
            if(adgt <= adlt)
            {
                return scaled16 >= adgt && scaled16 <= adlt;
            }
            return scaled16 >= adgt || scaled16 <= adlt;
        }

        private void PerformSingleConversion()
        {
            var sample = GetSample12Bit((uint)singlePortPos.Value, (uint)singlePinPos.Value);
            var formatted = FormatSample(sample, singleFifoAlignment.Value, singleFifoShowId.Value, SingleResultId);
            lastSingleData = formatted;

            if(singleFifo.Count >= MaxFifoEntries)
            {
                ifSingleFifoOf.Value = true;
            }
            else
            {
                singleFifo.Enqueue(formatted);
            }

            ifSingleDone.Value = true;
            if(singleCmpEnable.Value && IsWithinCompareWindow(sample))
            {
                ifSingleCmp.Value = true;
            }

            UpdateSingleFifoFlags();
        }

        private void PerformScanConversion()
        {
            var mask = (uint)maskRequestField.Value;
            for(var i = 0; i < ScanTableEntryCount; i++)
            {
                if((mask & (1u << i)) == 0)
                {
                    continue;
                }

                var entry = scanEntries[i];
                var pinPos = (entry >> 8) & 0xFu;
                var portPos = (entry >> 12) & 0xFu;
                var cmpEnable = (entry & (1u << 17)) != 0;

                var sample = GetSample12Bit(portPos, pinPos);
                var formatted = FormatSample(sample, scanFifoAlignment.Value, scanFifoShowId.Value, (byte)i);
                lastScanData = formatted;

                if(scanFifo.Count >= MaxFifoEntries)
                {
                    ifScanFifoOf.Value = true;
                }
                else
                {
                    scanFifo.Enqueue(formatted);
                }

                ifScanEntryDone.Value = true;
                if(cmpEnable && IsWithinCompareWindow(sample))
                {
                    ifScanCmp.Value = true;
                }
            }

            ifScanTableDone.Value = true;
            UpdateScanFifoFlags();
        }

        private uint ReadSingleFifoData()
        {
            if(singleFifo.Count == 0)
            {
                ifSingleFifoUf.Value = true;
                UpdateInterrupts();
                return 0;
            }

            var value = singleFifo.Dequeue();
            UpdateSingleFifoFlags();
            return value;
        }

        private uint ReadScanFifoData()
        {
            if(scanFifo.Count == 0)
            {
                ifScanFifoUf.Value = true;
                UpdateInterrupts();
                return 0;
            }

            var value = scanFifo.Dequeue();
            UpdateScanFifoFlags();
            return value;
        }

        private void UpdateSingleFifoFlags()
        {
            var dvlReached = singleFifo.Count >= (int)singleFifoDvl.Value + 1;
            ifSingleFifoDvl.Value = dvlReached;
            SingleDmaRequest.Set(dvlReached);
            UpdateInterrupts();
        }

        private void UpdateScanFifoFlags()
        {
            var dvlReached = scanFifo.Count >= (int)scanFifoDvl.Value + 1;
            ifScanFifoDvl.Value = dvlReached;
            ScanDmaRequest.Set(dvlReached);
            UpdateInterrupts();
        }

        private void UpdateDmaSignals()
        {
            SingleDmaRequest.Set(singleFifo.Count >= (int)singleFifoDvl.Value + 1);
            ScanDmaRequest.Set(scanFifo.Count >= (int)scanFifoDvl.Value + 1);
        }

        private IFlagRegisterField enabledFlag;
        private IValueRegisterField maskRequestField;
        private IValueRegisterField cmpThresholdLessField;
        private IValueRegisterField cmpThresholdGreaterField;

        private IFlagRegisterField ifSingleFifoDvl;
        private IFlagRegisterField ifScanFifoDvl;
        private IFlagRegisterField ifSingleCmp;
        private IFlagRegisterField ifScanCmp;
        private IFlagRegisterField ifScanEntryDone;
        private IFlagRegisterField ifScanTableDone;
        private IFlagRegisterField ifSingleDone;
        private IFlagRegisterField ifPolarityErr;
        private IFlagRegisterField ifPortAllocErr;
        private IFlagRegisterField ifSingleFifoOf;
        private IFlagRegisterField ifScanFifoOf;
        private IFlagRegisterField ifSingleFifoUf;
        private IFlagRegisterField ifScanFifoUf;
        private IFlagRegisterField ifEm23AbortErr;

        private IFlagRegisterField ienSingleFifoDvl;
        private IFlagRegisterField ienScanFifoDvl;
        private IFlagRegisterField ienSingleCmp;
        private IFlagRegisterField ienScanCmp;
        private IFlagRegisterField ienScanEntryDone;
        private IFlagRegisterField ienScanTableDone;
        private IFlagRegisterField ienSingleDone;
        private IFlagRegisterField ienPolarityErr;
        private IFlagRegisterField ienPortAllocErr;
        private IFlagRegisterField ienSingleFifoOf;
        private IFlagRegisterField ienScanFifoOf;
        private IFlagRegisterField ienSingleFifoUf;
        private IFlagRegisterField ienScanFifoUf;
        private IFlagRegisterField ienEm23AbortErr;

        private IEnumRegisterField<TriggerSelect> scanTrigSel;
        private IEnumRegisterField<TriggerAction> scanTrigAction;
        private IEnumRegisterField<TriggerSelect> singleTrigSel;
        private IEnumRegisterField<TriggerAction> singleTrigAction;

        private IEnumRegisterField<Alignment> singleFifoAlignment;
        private IFlagRegisterField singleFifoShowId;
        private IValueRegisterField singleFifoDvl;

        private IEnumRegisterField<Alignment> scanFifoAlignment;
        private IFlagRegisterField scanFifoShowId;
        private IValueRegisterField scanFifoDvl;

        private IValueRegisterField singlePinNeg;
        private IValueRegisterField singlePortNeg;
        private IValueRegisterField singlePinPos;
        private IValueRegisterField singlePortPos;
        private IFlagRegisterField singleCmpEnable;

        private bool singleQueueEnabled;
        private bool scanQueueEnabled;
        private bool timerActive;
        private uint defaultSampleValue;
        private uint lastSingleData;
        private uint lastScanData;

        private readonly Queue<uint> singleFifo;
        private readonly Queue<uint> scanFifo;
        private readonly Dictionary<uint, uint> inputSamples;
        private readonly uint[] scanEntries;

        private const int MaxFifoEntries = 8;
        private const int ScanTableEntryCount = 16;
        private const uint Sample12BitMask = 0xFFF;
        private const byte SingleResultId = 0x20;

        private enum Alignment
        {
            Right12 = 0,
            Right16 = 1,
            Right20 = 2,
            Left12  = 3,
            Left16  = 4,
            Left20  = 5,
        }

        private enum TriggerSelect
        {
            Immediate   = 0,
            Timer       = 1,
            PrsClkGrp   = 2,
            PrsPosEdge  = 3,
            PrsNegEdge  = 4,
            PrsPosEdge2 = 5,
        }

        private enum TriggerAction
        {
            Once       = 0,
            Continuous = 1,
        }

        private enum Registers : long
        {
            IpVersion        = 0x000,
            Enable           = 0x004,
            Control          = 0x008,
            Command          = 0x00C,
            Timer            = 0x010,
            Status           = 0x014,
            MaskRequest      = 0x018,
            ScanTableMask    = 0x01C,
            CompareThreshold = 0x020,
            InterruptFlags   = 0x024,
            InterruptEnable  = 0x028,
            Trigger          = 0x02C,
            Config0          = 0x048,
            Scale0           = 0x050,
            Schedule0        = 0x054,
            Config1          = 0x058,
            Scale1           = 0x060,
            Schedule1        = 0x064,
            SingleFifoConfig = 0x070,
            SingleFifoData   = 0x074,
            SingleFifoStatus = 0x078,
            SingleData       = 0x07C,
            ScanFifoConfig   = 0x080,
            ScanFifoData     = 0x084,
            ScanFifoStatus   = 0x088,
            ScanData         = 0x08C,
            Single           = 0x098,
            ScanTable0       = 0x0A0,
        }
    }
}
