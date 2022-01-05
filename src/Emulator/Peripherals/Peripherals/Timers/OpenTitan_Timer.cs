//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2021 Google LLC
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class OpenTitan_Timer : IDoubleWordPeripheral, INumberedGPIOOutput, IKnownSize, IRiscVTimeProvider
    {
        public OpenTitan_Timer(Machine machine, long frequency = 24_000_000, int timersPerHart = 1, int numberOfHarts = 1)
        {
            // OpenTitan rv_timer has a configurable number of timers and harts/Harts.
            // It is compliant with the v1.11 RISC-V privilege specification.
            // The counters are all 64-bit and each timer has a configurable prescaler and step.
            // A multi-register array containing the enable bits for each time is at offset 0x0
            // Each hart timer then has its own config registers starting at offsets of 0x100 * timer_index
            if(timersPerHart > MaxNumTimers)
            {
                throw new ConstructionException($"Current {this.GetType().Name} implementation does not support more than {MaxNumTimers} timers per hart");
            }

            this.numberOfHarts = numberOfHarts;
            this.timersPerHart = timersPerHart;
            this.frequency = frequency;
            // Array containing all IRQs
            IRQs = new Dictionary<int, IGPIO>();
            Timers = new ComparingTimer[this.numberOfHarts, this.timersPerHart];
            for(var hartIdx = 0; hartIdx < numberOfHarts; hartIdx++)
            {
                for(var timerIdx = 0; timerIdx < timersPerHart; timerIdx++)
                {
                    IRQs[hartIdx * timersPerHart + timerIdx] = new GPIO();
                    Timers[hartIdx, timerIdx] = new ComparingTimer(machine.ClockSource, frequency, this, $"OpenTitan_Timer[{hartIdx},{timerIdx}]", limit: ulong.MaxValue,
                                                        direction: Time.Direction.Ascending, workMode: Time.WorkMode.Periodic,
                                                        enabled: false, eventEnabled: false, compare: ulong.MaxValue);
                    var i = hartIdx;
                    var j = timerIdx;
                    Timers[i, j].CompareReached += delegate
                    {
                        this.Log(LogLevel.Noisy, "Timer[{0},{1}] IRQ compare event", i, j);
                        GetIrqForHart(i, j).Set(true);
                    };
                    this.Log(LogLevel.Noisy, "Creating Timer[{0},{1}]", hartIdx, timerIdx);
                }
            }

            Connections = new ReadOnlyDictionary<int, IGPIO>(IRQs);

            timerEnables = new IFlagRegisterField[numberOfHarts];
            steps = new IValueRegisterField[numberOfHarts];
            prescalers = new IValueRegisterField[numberOfHarts];

            var registersMap = new Dictionary<long, DoubleWordRegister>();
            AddTimerControlRegisters(registersMap, numberOfHarts);
            for(int i = 0; i < numberOfHarts; i++)
            {
                AddTimerRegisters(registersMap, i);
            }
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void Reset()
        {
            registers.Reset();
            for(int i = 0; i < numberOfHarts; i++)
            {
                for(int j = 0; j < timersPerHart; j++)
                {
                    Timers[i, j].Reset();
                    GetIrqForHart(i, j).Set(false);
                }
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public String PrintInnerTimerStatus(int hartIdx, int timerIdx)
        {
            try
            {
                ComparingTimer timer = Timers[hartIdx, timerIdx];
                return String.Format("ComparingTimer(Enabled:{0},EventEnables:{1},Compare:0x{2:X},Value:0x{3:X},Divider:0x{4:X})",
                    timer.Enabled, timer.EventEnabled, timer.Compare, timer.Value, timer.Divider);
            }
            catch(System.IndexOutOfRangeException)
            {
                return String.Format("Timer does not exist for hart:{0} and timer:{1}", hartIdx, timerIdx);
            }
        }

        public long Size => MaxHartRegMapSize * (numberOfHarts + 1);
        public IReadOnlyDictionary<int, IGPIO> Connections { get; }
        // Uses the first timer of the first hart for IRiscVTimeProvider
        public ulong TimerValue => Timers[0, 0].Value;

        private IGPIO GetIrqForHart(int hartIdx, int timerIdx)
        {
            return IRQs[hartIdx * timersPerHart + timerIdx];
        }

        private void AddTimerControlRegisters(Dictionary<long, DoubleWordRegister> registersMap, int hartCount)
        {
            var maximumControlRegCount = (int)Math.Ceiling((hartCount) / 32.0);
            for(int timerCtrlIdx = 0; timerCtrlIdx < maximumControlRegCount; timerCtrlIdx++)
            {
                var offset = timerCtrlIdx * 4 + 4;
                int hartBitCount = (hartCount < 32) ? hartCount : 32;
                hartCount -= hartBitCount;
                var controlRegister = new DoubleWordRegister(this);
                for(int hartBitIdx = 0; hartBitIdx < hartBitCount; hartBitIdx++)
                {
                    int timerEnablesIdx = timerCtrlIdx * 32 + hartBitIdx;
                    timerEnables[timerEnablesIdx] = controlRegister.DefineFlagField(hartBitIdx, name: $"CONTROL{timerCtrlIdx}_{timerEnablesIdx}", writeCallback: (idx, val) =>
                    {
                        this.Log(LogLevel.Noisy, "Set Enable bit for {0} to {1}", timerEnablesIdx, val);
                        for(int i = 0; i < timersPerHart; i++)
                        {
                            Timers[timerEnablesIdx, i].Enabled = val;
                        }
                    });
                }
                registersMap.Add(offset, controlRegister);
            }
        }

        private void AddTimerRegisters(Dictionary<long, DoubleWordRegister> registersMap, int hartId)
        {
            var hartxOffset = MaxHartRegMapSize * (hartId + 1);
            var compareOffset = AddTimerConfigAndValueRegisters(registersMap, hartxOffset, hartId);
            var intrEnableOffset = AddTimerCompareRegisters(registersMap, compareOffset, hartId);
            var intrStateOffset = AddMultiRegisters(registersMap, intrEnableOffset, hartId, timersPerHart,
                flagPrefix: "IE", regName: "INTR_ENABLE", multiRegWriteCallback: (hartIdx, timerIdx, oldValue, newValue) =>
                 {
                     var timer = Timers[hartIdx, timerIdx];
                     timer.EventEnabled = newValue;
                 }, multiRegValueProviderCallback: (hartIdx, timerIdx) =>
                 {
                     var timer = Timers[hartIdx, timerIdx];
                     return timer.EventEnabled;
                 });
            var intrTestOffset = AddMultiRegisters(registersMap, intrStateOffset, hartId, timersPerHart,
                flagPrefix: "IS", regName: "INTR_STATE", multiRegWriteCallback: (hartIdx, timerIdx, oldValue, newValue) =>
                 {
                     if(newValue)
                     {
                         var gpio = GetIrqForHart(hartIdx, timerIdx);
                         gpio.Set(false);
                     }
                 }, multiRegValueProviderCallback: (hartIdx, timerIdx) =>
                 {
                     var gpio = GetIrqForHart(hartIdx, timerIdx);
                     return gpio.IsSet;
                 });
            AddMultiRegisters(registersMap, intrTestOffset, hartId, timersPerHart,
                flagPrefix: "T", regName: "INTR_TEST", multiRegWriteCallback: (hartIdx, timerIdx, oldValue, newValue) =>
                 {
                     if(newValue)
                     {
                         var gpio = GetIrqForHart(hartIdx, timerIdx);
                         gpio.Set(true);
                     }
                 }, multiRegValueProviderCallback: (hartIdx, timerIdx) =>
                 {
                     return false;
                 });
        }

        private long AddTimerConfigAndValueRegisters(Dictionary<long, DoubleWordRegister> registersMap, long address, int hartId)
        {
            long offset = 0;
            var configRegister = new DoubleWordRegister(this, 0x10000);
            var i = hartId;
            configRegister.WithValueField(0, 12, out prescalers[hartId], name: $"PRESCALE_{hartId}", writeCallback: (_, val) =>
            {
                this.Log(LogLevel.Noisy, "Set prescaler value {0} for hart {1}", val, hartId);
                // TODO: Modify timer to change frequency
                this.Log(LogLevel.Warning, "Changing prescaler does not currently effect the frequency.");
                TimerUpdateDivider(hartId);
            }).WithValueField(16, 8, out steps[hartId], name: $"STEP_{hartId}", writeCallback: (_, val) =>
            {
                this.Log(LogLevel.Noisy, "Set the step value {0} for hart {1}", val, hartId);
                // TODO: Modify timer to change frequency
                this.Log(LogLevel.Warning, "Changing step does not currently effect the frequency.");
            }).WithReservedBits(24, 8);

            registersMap.Add(address + offset, configRegister);

            offset += 4;
            var lowerValueRegister = new DoubleWordRegister(this, 0x0).WithValueField(0, 32, name: $"VALUELOW_{hartId}",
                    valueProviderCallback: _ => (uint)(Timers[i, 0].Value),
                    writeCallback: (_, val) =>
                    {
                        for(int j = 0; j < timersPerHart; j++)
                        {
                            var timer = Timers[i, j];
                            ulong currentValue = timer.Value;
                            timer.Value = currentValue & (ulong)0xFFFF_FFFF_0000_0000 | (ulong)val;
                        }
                    });
            registersMap.Add(address + offset, lowerValueRegister);

            offset += 4;
            var hiValueRegister = new DoubleWordRegister(this, 0x0).WithValueField(0, 32, name: $"VALUEHI_{hartId}",
                    valueProviderCallback: (_) => (uint)(Timers[i, 0].Value >> 32),
                    writeCallback: (_, val) =>
                    {
                        for(int j = 0; j < timersPerHart; j++)
                        {
                            var timer = Timers[i, j];
                            ulong currentValue = timer.Value;
                            timer.Value = (currentValue & (ulong)0x0000_0000_FFFF_FFFF) | (((ulong)val) << 32);
                        }
                    });
            registersMap.Add(address + offset, hiValueRegister);
            return address + offset + 4;
        }

        private long AddTimerCompareRegisters(Dictionary<long, DoubleWordRegister> registersMap, long address, int hartId)
        {
            long offset = 0;
            for(int timerIdx = 0; timerIdx < timersPerHart; timerIdx++)
            {
                var i = hartId;
                var j = timerIdx;
                var lowCompareRegister = new DoubleWordRegister(this, 0xFFFF_FFFF).WithValueField(0, 32,
                        name: $"COMPARELOW_{hartId}_{hartId}",
                        valueProviderCallback: _ => (uint)(Timers[i, j].Compare),
                        writeCallback: (_, val) =>
                        {
                            var timer = Timers[i, j];
                            ulong currentValue = timer.Compare;
                            timer.Compare = (currentValue & (ulong)0xFFFF_FFFF_0000_0000) | ((ulong)val);
                        });
                registersMap.Add(address + offset, lowCompareRegister);
                offset += 4;

                var hiCompareRegister = new DoubleWordRegister(this, 0xFFFF_FFFF).WithValueField(0, 32,
                        name: $"COMPAREHI_{hartId}_{timerIdx}",
                        valueProviderCallback: _ => (uint)(Timers[i, j].Compare >> 32),
                        writeCallback: (_, val) =>
                        {
                            var timer = Timers[i, j];
                            ulong currentValue = timer.Compare;
                            timer.Compare = (currentValue & (ulong)0x0000_0000_FFFF_FFFF) | (((ulong)val) << 32);
                        });
                registersMap.Add(address + offset, hiCompareRegister);
                offset += 4;
            }
            return address + offset;
        }

        private long AddMultiRegisters(Dictionary<long, DoubleWordRegister> registersMap,
            long address, int hartId, int timerCount,
            String flagPrefix = "FLAG", String regName = "REG", Action<int, int, bool, bool> multiRegWriteCallback = null,
             Func<int, int, bool> multiRegValueProviderCallback = null)
        {
            int maximumRegCount = (int)Math.Ceiling((timerCount) / 32.0);
            int offset = 0;
            for(int i = 0; i < maximumRegCount; i++)
            {
                offset = i * 4;
                int bitCount = (timerCount < 32) ? timerCount : 32;
                timerCount -= bitCount;
                var multiReg = new DoubleWordRegister(this, 0x0);
                for(int bitIdx = 0; bitIdx < bitCount; bitIdx++)
                {
                    var flagIdx = i * 32 + bitIdx;
                    multiReg.DefineFlagField(bitIdx, name: $"{flagPrefix}{hartId}_{flagIdx}", writeCallback: (oldValue, newValue) =>
                    {
                        multiRegWriteCallback?.Invoke(hartId, flagIdx, oldValue, newValue);
                    },
                    valueProviderCallback: _ =>
                    {
                        if(multiRegValueProviderCallback != null)
                        {
                            return multiRegValueProviderCallback(hartId, flagIdx);
                        }
                        return false;
                    });
                }
                registersMap.Add(address + offset, multiReg);
            }
            return address + offset + 4;
        }

        private void TimerUpdateDivider(int hartId)
        {
            for(int i = 0; i < timersPerHart; i++)
            {
                uint prescaler = prescalers[hartId].Value + 1;
                uint step = steps[hartId].Value;
                uint divider = prescaler * step;
                Timers[hartId, i].Divider = (divider == 0) ? 1 : divider;
            }
        }

        private Dictionary<int, IGPIO> IRQs { get; }
        private readonly  ComparingTimer[,] Timers;

        private readonly IFlagRegisterField[] timerEnables;
        private readonly IValueRegisterField[] prescalers;
        private readonly IValueRegisterField[] steps;

        private readonly DoubleWordRegisterCollection registers;

        private readonly int numberOfHarts;
        private readonly int timersPerHart;
        private readonly long frequency;
        private const uint MaxNumTimers = 32; // Limit set in python script for rv_timer registers.
        private const uint MaxHartRegMapSize = 0x100;
    }
}
