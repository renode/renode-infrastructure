//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.WordToByte | AllowedTranslation.DoubleWordToByte)]
    public class ITE_IT_INTC : BasicBytePeripheral, IKnownSize, IIRQController, IHasMappedRegisters
    {
        public ITE_IT_INTC(IMachine machine, BaseRiscV cpu, Version version = Version.IT8XXX2_V1) : base(machine)
        {
            this.cpu = cpu;
            this.version = version;
            numberOfBanks = BankCountOf(version);
            numberOfInterrupts = numberOfBanks * InterruptsPerBank;
            IRQ = new GPIO();

            mapper = new RegisterMapper(typeof(Registers));
            mapper.RegisterEnumMapping(RegisterMapOf(version));

            pending = new bool[numberOfInterrupts];
            enabled = new bool[numberOfInterrupts];
            mode = new TriggerMode[numberOfInterrupts];
            polarity = new Polarity[numberOfInterrupts];
            rawInput = new bool[numberOfInterrupts];

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            for(var irq = 0; irq < numberOfInterrupts; irq++)
            {
                pending[irq] = false;
                enabled[irq] = false;
                mode[irq] = TriggerMode.Level;
                polarity[irq] = Polarity.ActiveHigh;
                rawInput[irq] = false;
            }
            UpdateInterrupt();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number < 0 || number >= numberOfInterrupts)
            {
                this.Log(LogLevel.Error, "IRQ {0} is out of range [0; {1})", number, numberOfInterrupts);
                return;
            }

            var wasActive = IsActive(number, rawInput[number]);
            rawInput[number] = value;
            var nowActive = IsActive(number, value);
            if(mode[number] == TriggerMode.Edge)
            {
                if(nowActive && !wasActive)
                {
                    pending[number] = true;
                }
            }
            else
            {
                pending[number] = nowActive;
            }
            UpdateInterrupt();
        }

        public string OffsetToString(long offset) => mapper.ToString(offset);

        public long Size => 0x100;

        public GPIO IRQ { get; private set; }

        protected override void DefineRegisters()
        {
            var registerMap = RegisterMapOf(version);
            for(var bank = 0; bank < numberOfBanks; bank++)
            {
                DefineBank(registerMap, bank);
            }

            Registers.AllInterruptVectorRegister.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "All Interrupt Vector (AIVECT)", valueProviderCallback: _ => Vector);
        }

        private static Type RegisterMapOf(Version version)
        {
            switch(version)
            {
            case Version.IT8XXX2_V1:
                return typeof(RegistersIT8XXX2V1);

            case Version.IT8XXX2_V2:
                return typeof(RegistersIT8XXX2V2);

            case Version.IT51XXX:
                return typeof(RegistersIT51XXX);

            default:
                throw new ConstructionException($"Unsupported INTC version: {version}");
            }
        }

        private static int BankCountOf(Version version)
        {
            switch(version)
            {
            case Version.IT8XXX2_V1:
            case Version.IT8XXX2_V2:
                return 24;

            case Version.IT51XXX:
                return 29;

            default:
                throw new ConstructionException($"Unsupported INTC version: {version}");
            }
        }

        private static long OffsetOf(Type registerMap, string prefix, int bank)
        {
            if(!Enum.TryParse(registerMap, $"{prefix}{bank}", out var register))
            {
                throw new ConstructionException($"{registerMap.Name} is missing {prefix}{bank}");
            }
            return Convert.ToInt64(register);
        }

        private void DefineBank(Type registerMap, int bank)
        {
            var isr = RegistersCollection.DefineRegister(OffsetOf(registerMap, "InterruptStatusRegister", bank));
            var ier = RegistersCollection.DefineRegister(OffsetOf(registerMap, "InterruptEnableRegister", bank));
            var ielmr = RegistersCollection.DefineRegister(OffsetOf(registerMap, "InterruptEdgeLevelTriggeredModeRegister", bank));
            var ipolr = RegistersCollection.DefineRegister(OffsetOf(registerMap, "InterruptPolarityRegister", bank));
            for(var bit = 0; bit < InterruptsPerBank; bit++)
            {
                var irq = bank * InterruptsPerBank + bit;
                isr.WithFlag(bit, name: $"Interrupt Status Group {bank} (ISGR{bank}[{bit}])",
                    valueProviderCallback: _ => pending[irq],
                    writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            AcknowledgeInterrupt(irq);
                        }
                        UpdateInterrupt();
                    });
                ier.WithFlag(bit, name: $"Interrupt Enable Group {bank} (IEGR{bank}[{bit}])",
                    valueProviderCallback: _ => enabled[irq],
                    writeCallback: (_, value) =>
                    {
                        enabled[irq] = value;
                        UpdateInterrupt();
                    });
                ielmr.WithFlag(bit, name: $"Interrupt Edge/Level-Triggered Mode Group {bank} (IELMGR{bank}[{bit}])",
                    valueProviderCallback: _ => mode[irq] == TriggerMode.Edge,
                    writeCallback: (_, value) =>
                    {
                        mode[irq] = value ? TriggerMode.Edge : TriggerMode.Level;
                        RefreshLevel(irq);
                        UpdateInterrupt();
                    });
                ipolr.WithFlag(bit, name: $"Interrupt Polarity Group {bank} (IPOLGR{bank}[{bit}])",
                    valueProviderCallback: _ => polarity[irq] == Polarity.ActiveLow,
                    writeCallback: (_, value) =>
                    {
                        polarity[irq] = value ? Polarity.ActiveLow : Polarity.ActiveHigh;
                        RefreshLevel(irq);
                        UpdateInterrupt();
                    });
            }
        }

        private void AcknowledgeInterrupt(int irq)
        {
            pending[irq] = mode[irq] == TriggerMode.Edge ? false : IsActive(irq, rawInput[irq]);
        }

        private void RefreshLevel(int irq)
        {
            if(mode[irq] == TriggerMode.Level)
            {
                pending[irq] = IsActive(irq, rawInput[irq]);
            }
        }

        private void UpdateInterrupt()
        {
            var active = HighestPending >= 0;
            IRQ.Set(active);

            if(active)
            {
                // The board expects to wake from wfi even when the interrupt is masked at the core
                cpu.RequestWakeUpFromWfi();
            }
        }

        private bool IsActive(int irq, bool raw)
        {
            return polarity[irq] == Polarity.ActiveLow ? !raw : raw;
        }

        private int HighestPending
        {
            get
            {
                for(var irq = numberOfInterrupts - 1; irq >= 0; irq--)
                {
                    if(pending[irq] && enabled[irq])
                    {
                        return irq;
                    }
                }
                return -1;
            }
        }

        private byte Vector => (byte)((HighestPending < 0 ? 0 : HighestPending) + VectorIrqOffset);

        private readonly BaseRiscV cpu;
        private readonly Version version;
        private readonly RegisterMapper mapper;
        private readonly int numberOfBanks;
        private readonly int numberOfInterrupts;
        private readonly bool[] pending;
        private readonly bool[] enabled;
        private readonly TriggerMode[] mode;
        private readonly Polarity[] polarity;
        private readonly bool[] rawInput;

        private const int InterruptsPerBank = 8;
        private const byte VectorIrqOffset = 0x10;

        public enum Version
        {
            IT8XXX2_V1,
            IT8XXX2_V2,
            IT51XXX,
        }

        private enum TriggerMode
        {
            Level = 0,
            Edge = 1,
        }

        private enum Polarity
        {
            ActiveHigh = 0,
            ActiveLow = 1,
        }

        private enum Registers : long
        {
            AllInterruptVectorRegister = 0x10,
        }

        private enum RegistersIT8XXX2V1 : long
        {
            InterruptStatusRegister0    = 0x00,
            InterruptStatusRegister1    = 0x01,
            InterruptStatusRegister2    = 0x02,
            InterruptStatusRegister3    = 0x03,
            InterruptEnableRegister0    = 0x04,
            InterruptEnableRegister1    = 0x05,
            InterruptEnableRegister2    = 0x06,
            InterruptEnableRegister3    = 0x07,
            InterruptEdgeLevelTriggeredModeRegister0  = 0x08,
            InterruptEdgeLevelTriggeredModeRegister1  = 0x09,
            InterruptEdgeLevelTriggeredModeRegister2  = 0x0A,
            InterruptEdgeLevelTriggeredModeRegister3  = 0x0B,
            InterruptPolarityRegister0  = 0x0C,
            InterruptPolarityRegister1  = 0x0D,
            InterruptPolarityRegister2  = 0x0E,
            InterruptPolarityRegister3  = 0x0F,
            PowerFailStatus             = 0x11,
            PowerFailRegister           = 0x12,
            InterruptControlRegister    = 0x13,
            InterruptStatusRegister4    = 0x14,
            InterruptEnableRegister4    = 0x15,
            InterruptEdgeLevelTriggeredModeRegister4  = 0x16,
            InterruptPolarityRegister4  = 0x17,
            InterruptStatusRegister5    = 0x18,
            InterruptEnableRegister5    = 0x19,
            InterruptEdgeLevelTriggeredModeRegister5  = 0x1A,
            InterruptPolarityRegister5  = 0x1B,
            InterruptStatusRegister6    = 0x1C,
            InterruptEnableRegister6    = 0x1D,
            InterruptEdgeLevelTriggeredModeRegister6  = 0x1E,
            InterruptPolarityRegister6  = 0x1F,
            InterruptStatusRegister7    = 0x20,
            InterruptEnableRegister7    = 0x21,
            InterruptEdgeLevelTriggeredModeRegister7  = 0x22,
            InterruptPolarityRegister7  = 0x23,
            InterruptStatusRegister8    = 0x24,
            InterruptEnableRegister8    = 0x25,
            InterruptEdgeLevelTriggeredModeRegister8  = 0x26,
            InterruptPolarityRegister8  = 0x27,
            InterruptStatusRegister9    = 0x28,
            InterruptEnableRegister9    = 0x29,
            InterruptEdgeLevelTriggeredModeRegister9  = 0x2A,
            InterruptPolarityRegister9  = 0x2B,
            InterruptStatusRegister10   = 0x2C,
            InterruptEnableRegister10   = 0x2D,
            InterruptEdgeLevelTriggeredModeRegister10 = 0x2E,
            InterruptPolarityRegister10 = 0x2F,
            InterruptStatusRegister11   = 0x30,
            InterruptEnableRegister11   = 0x31,
            InterruptEdgeLevelTriggeredModeRegister11 = 0x32,
            InterruptPolarityRegister11 = 0x33,
            InterruptStatusRegister12   = 0x34,
            InterruptEnableRegister12   = 0x35,
            InterruptEdgeLevelTriggeredModeRegister12 = 0x36,
            InterruptPolarityRegister12 = 0x37,
            InterruptStatusRegister13   = 0x38,
            InterruptEnableRegister13   = 0x39,
            InterruptEdgeLevelTriggeredModeRegister13 = 0x3A,
            InterruptPolarityRegister13 = 0x3B,
            InterruptStatusRegister14   = 0x3C,
            InterruptEnableRegister14   = 0x3D,
            InterruptEdgeLevelTriggeredModeRegister14 = 0x3E,
            InterruptPolarityRegister14 = 0x3F,
            InterruptStatusRegister15   = 0x40,
            InterruptEnableRegister15   = 0x41,
            InterruptEdgeLevelTriggeredModeRegister15 = 0x42,
            InterruptPolarityRegister15 = 0x43,
            InterruptStatusRegister16   = 0x44,
            InterruptEnableRegister16   = 0x45,
            InterruptEdgeLevelTriggeredModeRegister16 = 0x46,
            InterruptPolarityRegister16 = 0x47,
            InterruptStatusRegister17   = 0x48,
            InterruptEnableRegister17   = 0x49,
            InterruptEdgeLevelTriggeredModeRegister17 = 0x4A,
            InterruptPolarityRegister17 = 0x4B,
            InterruptStatusRegister18   = 0x4C,
            InterruptEnableRegister18   = 0x4D,
            InterruptEdgeLevelTriggeredModeRegister18 = 0x4E,
            InterruptPolarityRegister18 = 0x4F,
            InterruptStatusRegister19   = 0x50,
            InterruptEnableRegister19   = 0x51,
            InterruptEdgeLevelTriggeredModeRegister19 = 0x52,
            InterruptPolarityRegister19 = 0x53,
            InterruptStatusRegister20   = 0x54,
            InterruptEnableRegister20   = 0x55,
            InterruptEdgeLevelTriggeredModeRegister20 = 0x56,
            InterruptPolarityRegister20 = 0x57,
            InterruptStatusRegister21   = 0x58,
            InterruptEnableRegister21   = 0x59,
            InterruptEdgeLevelTriggeredModeRegister21 = 0x5A,
            InterruptPolarityRegister21 = 0x5B,
            InterruptStatusRegister22   = 0x5C,
            InterruptEnableRegister22   = 0x5D,
            InterruptEdgeLevelTriggeredModeRegister22 = 0x5E,
            InterruptPolarityRegister22 = 0x5F,
            InterruptStatusRegister23   = 0x90,
            InterruptEnableRegister23   = 0x91,
            InterruptEdgeLevelTriggeredModeRegister23 = 0x92,
            InterruptPolarityRegister23 = 0x93,
        }

        private enum RegistersIT8XXX2V2 : long
        {
            InterruptStatusRegister0    = 0x00,
            InterruptEnableRegister0    = 0x01,
            InterruptEdgeLevelTriggeredModeRegister0  = 0x02,
            InterruptPolarityRegister0  = 0x03,
            InterruptStatusRegister1    = 0x04,
            InterruptEnableRegister1    = 0x05,
            InterruptEdgeLevelTriggeredModeRegister1  = 0x06,
            InterruptPolarityRegister1  = 0x07,
            InterruptStatusRegister2    = 0x08,
            InterruptEnableRegister2    = 0x09,
            InterruptEdgeLevelTriggeredModeRegister2  = 0x0A,
            InterruptPolarityRegister2  = 0x0B,
            InterruptStatusRegister3    = 0x0C,
            InterruptEnableRegister3    = 0x0D,
            InterruptEdgeLevelTriggeredModeRegister3  = 0x0E,
            InterruptPolarityRegister3  = 0x0F,
            InterruptControlRegister    = 0x13,
            InterruptStatusRegister4    = 0x14,
            InterruptEnableRegister4    = 0x15,
            InterruptEdgeLevelTriggeredModeRegister4  = 0x16,
            InterruptPolarityRegister4  = 0x17,
            InterruptStatusRegister5    = 0x18,
            InterruptEnableRegister5    = 0x19,
            InterruptEdgeLevelTriggeredModeRegister5  = 0x1A,
            InterruptPolarityRegister5  = 0x1B,
            InterruptStatusRegister6    = 0x1C,
            InterruptEnableRegister6    = 0x1D,
            InterruptEdgeLevelTriggeredModeRegister6  = 0x1E,
            InterruptPolarityRegister6  = 0x1F,
            InterruptStatusRegister7    = 0x20,
            InterruptEnableRegister7    = 0x21,
            InterruptEdgeLevelTriggeredModeRegister7  = 0x22,
            InterruptPolarityRegister7  = 0x23,
            InterruptStatusRegister8    = 0x24,
            InterruptEnableRegister8    = 0x25,
            InterruptEdgeLevelTriggeredModeRegister8  = 0x26,
            InterruptPolarityRegister8  = 0x27,
            InterruptStatusRegister9    = 0x28,
            InterruptEnableRegister9    = 0x29,
            InterruptEdgeLevelTriggeredModeRegister9  = 0x2A,
            InterruptPolarityRegister9  = 0x2B,
            InterruptStatusRegister10   = 0x2C,
            InterruptEnableRegister10   = 0x2D,
            InterruptEdgeLevelTriggeredModeRegister10 = 0x2E,
            InterruptPolarityRegister10 = 0x2F,
            InterruptStatusRegister11   = 0x30,
            InterruptEnableRegister11   = 0x31,
            InterruptEdgeLevelTriggeredModeRegister11 = 0x32,
            InterruptPolarityRegister11 = 0x33,
            InterruptStatusRegister12   = 0x34,
            InterruptEnableRegister12   = 0x35,
            InterruptEdgeLevelTriggeredModeRegister12 = 0x36,
            InterruptPolarityRegister12 = 0x37,
            InterruptStatusRegister13   = 0x38,
            InterruptEnableRegister13   = 0x39,
            InterruptEdgeLevelTriggeredModeRegister13 = 0x3A,
            InterruptPolarityRegister13 = 0x3B,
            InterruptStatusRegister14   = 0x3C,
            InterruptEnableRegister14   = 0x3D,
            InterruptEdgeLevelTriggeredModeRegister14 = 0x3E,
            InterruptPolarityRegister14 = 0x3F,
            InterruptStatusRegister15   = 0x40,
            InterruptEnableRegister15   = 0x41,
            InterruptEdgeLevelTriggeredModeRegister15 = 0x42,
            InterruptPolarityRegister15 = 0x43,
            InterruptStatusRegister16   = 0x44,
            InterruptEnableRegister16   = 0x45,
            InterruptEdgeLevelTriggeredModeRegister16 = 0x46,
            InterruptPolarityRegister16 = 0x47,
            InterruptStatusRegister17   = 0x48,
            InterruptEnableRegister17   = 0x49,
            InterruptEdgeLevelTriggeredModeRegister17 = 0x4A,
            InterruptPolarityRegister17 = 0x4B,
            InterruptStatusRegister18   = 0x4C,
            InterruptEnableRegister18   = 0x4D,
            InterruptEdgeLevelTriggeredModeRegister18 = 0x4E,
            InterruptPolarityRegister18 = 0x4F,
            InterruptStatusRegister19   = 0x50,
            InterruptEnableRegister19   = 0x51,
            InterruptEdgeLevelTriggeredModeRegister19 = 0x52,
            InterruptPolarityRegister19 = 0x53,
            InterruptStatusRegister20   = 0x54,
            InterruptEnableRegister20   = 0x55,
            InterruptEdgeLevelTriggeredModeRegister20 = 0x56,
            InterruptPolarityRegister20 = 0x57,
            InterruptStatusRegister21   = 0x58,
            InterruptEnableRegister21   = 0x59,
            InterruptEdgeLevelTriggeredModeRegister21 = 0x5A,
            InterruptPolarityRegister21 = 0x5B,
            InterruptStatusRegister22   = 0x5C,
            InterruptEnableRegister22   = 0x5D,
            InterruptEdgeLevelTriggeredModeRegister22 = 0x5E,
            InterruptPolarityRegister22 = 0x5F,
            InterruptStatusRegister23   = 0x60,
            InterruptEnableRegister23   = 0x61,
            InterruptEdgeLevelTriggeredModeRegister23 = 0x62,
            InterruptPolarityRegister23 = 0x63,
        }

        private enum RegistersIT51XXX : long
        {
            // Empty on purpose
            InterruptStatusRegister0    = 0x04,
            InterruptEnableRegister0    = 0x05,
            InterruptEdgeLevelTriggeredModeRegister0  = 0x06,
            InterruptPolarityRegister0  = 0x07,
            InterruptStatusRegister1    = 0x08,
            InterruptEnableRegister1    = 0x09,
            InterruptEdgeLevelTriggeredModeRegister1  = 0x0A,
            InterruptPolarityRegister1  = 0x0B,
            InterruptStatusRegister2    = 0x0C,
            InterruptEnableRegister2    = 0x0D,
            InterruptEdgeLevelTriggeredModeRegister2  = 0x0E,
            InterruptPolarityRegister2  = 0x0F,
            InterruptVectorControlRegister = 0x12,
            InterruptStatusRegister3    = 0x14,
            InterruptEnableRegister3    = 0x15,
            InterruptEdgeLevelTriggeredModeRegister3  = 0x16,
            InterruptPolarityRegister3  = 0x17,
            InterruptStatusRegister4    = 0x18,
            InterruptEnableRegister4    = 0x19,
            InterruptEdgeLevelTriggeredModeRegister4  = 0x1A,
            InterruptPolarityRegister4  = 0x1B,
            InterruptStatusRegister5    = 0x1C,
            InterruptEnableRegister5    = 0x1D,
            InterruptEdgeLevelTriggeredModeRegister5  = 0x1E,
            InterruptPolarityRegister5  = 0x1F,
            InterruptStatusRegister6    = 0x20,
            InterruptEnableRegister6    = 0x21,
            InterruptEdgeLevelTriggeredModeRegister6  = 0x22,
            InterruptPolarityRegister6  = 0x23,
            InterruptStatusRegister7    = 0x24,
            InterruptEnableRegister7    = 0x25,
            InterruptEdgeLevelTriggeredModeRegister7  = 0x26,
            InterruptPolarityRegister7  = 0x27,
            InterruptStatusRegister8    = 0x28,
            InterruptEnableRegister8    = 0x29,
            InterruptEdgeLevelTriggeredModeRegister8  = 0x2A,
            InterruptPolarityRegister8  = 0x2B,
            InterruptStatusRegister9    = 0x2C,
            InterruptEnableRegister9    = 0x2D,
            InterruptEdgeLevelTriggeredModeRegister9  = 0x2E,
            InterruptPolarityRegister9  = 0x2F,
            InterruptStatusRegister10   = 0x30,
            InterruptEnableRegister10   = 0x31,
            InterruptEdgeLevelTriggeredModeRegister10 = 0x32,
            InterruptPolarityRegister10 = 0x33,
            InterruptStatusRegister11   = 0x34,
            InterruptEnableRegister11   = 0x35,
            InterruptEdgeLevelTriggeredModeRegister11 = 0x36,
            InterruptPolarityRegister11 = 0x37,
            InterruptStatusRegister12   = 0x38,
            InterruptEnableRegister12   = 0x39,
            InterruptEdgeLevelTriggeredModeRegister12 = 0x3A,
            InterruptPolarityRegister12 = 0x3B,
            InterruptStatusRegister13   = 0x3C,
            InterruptEnableRegister13   = 0x3D,
            InterruptEdgeLevelTriggeredModeRegister13 = 0x3E,
            InterruptPolarityRegister13 = 0x3F,
            InterruptStatusRegister14   = 0x40,
            InterruptEnableRegister14   = 0x41,
            InterruptEdgeLevelTriggeredModeRegister14 = 0x42,
            InterruptPolarityRegister14 = 0x43,
            InterruptStatusRegister15   = 0x44,
            InterruptEnableRegister15   = 0x45,
            InterruptEdgeLevelTriggeredModeRegister15 = 0x46,
            InterruptPolarityRegister15 = 0x47,
            InterruptStatusRegister16   = 0x48,
            InterruptEnableRegister16   = 0x49,
            InterruptEdgeLevelTriggeredModeRegister16 = 0x4A,
            InterruptPolarityRegister16 = 0x4B,
            InterruptStatusRegister17   = 0x4C,
            InterruptEnableRegister17   = 0x4D,
            InterruptEdgeLevelTriggeredModeRegister17 = 0x4E,
            InterruptPolarityRegister17 = 0x4F,
            InterruptStatusRegister18   = 0x50,
            InterruptEnableRegister18   = 0x51,
            InterruptEdgeLevelTriggeredModeRegister18 = 0x52,
            InterruptPolarityRegister18 = 0x53,
            InterruptStatusRegister19   = 0x54,
            InterruptEnableRegister19   = 0x55,
            InterruptEdgeLevelTriggeredModeRegister19 = 0x56,
            InterruptPolarityRegister19 = 0x57,
            InterruptStatusRegister20   = 0x58,
            InterruptEnableRegister20   = 0x59,
            InterruptEdgeLevelTriggeredModeRegister20 = 0x5A,
            InterruptPolarityRegister20 = 0x5B,
            InterruptStatusRegister21   = 0x5C,
            InterruptEnableRegister21   = 0x5D,
            InterruptEdgeLevelTriggeredModeRegister21 = 0x5E,
            InterruptPolarityRegister21 = 0x5F,
            InterruptStatusRegister22   = 0x60,
            InterruptEnableRegister22   = 0x61,
            InterruptEdgeLevelTriggeredModeRegister22 = 0x62,
            InterruptPolarityRegister22 = 0x63,
            InterruptStatusRegister23   = 0x64,
            InterruptEnableRegister23   = 0x65,
            InterruptEdgeLevelTriggeredModeRegister23 = 0x66,
            InterruptPolarityRegister23 = 0x67,
            InterruptStatusRegister24   = 0x68,
            InterruptEnableRegister24   = 0x69,
            InterruptEdgeLevelTriggeredModeRegister24 = 0x6A,
            InterruptPolarityRegister24 = 0x6B,
            InterruptStatusRegister25   = 0x6C,
            InterruptEnableRegister25   = 0x6D,
            InterruptEdgeLevelTriggeredModeRegister25 = 0x6E,
            InterruptPolarityRegister25 = 0x6F,
            InterruptStatusRegister26   = 0x70,
            InterruptEnableRegister26   = 0x71,
            InterruptEdgeLevelTriggeredModeRegister26 = 0x72,
            InterruptPolarityRegister26 = 0x73,
            InterruptStatusRegister27   = 0x74,
            InterruptEnableRegister27   = 0x75,
            InterruptEdgeLevelTriggeredModeRegister27 = 0x76,
            InterruptPolarityRegister27 = 0x77,
            InterruptStatusRegister28   = 0x78,
            InterruptEnableRegister28   = 0x79,
            InterruptEdgeLevelTriggeredModeRegister28 = 0x7A,
            InterruptPolarityRegister28 = 0x7B,
        }
    }
}
