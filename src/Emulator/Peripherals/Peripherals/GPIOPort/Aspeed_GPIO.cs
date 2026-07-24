//
// Copyright (c) 2026 Microsoft
// Licensed under the MIT License.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    // Aspeed AST2600 GPIO Controller
    // Reference: QEMU hw/gpio/aspeed_gpio.c
    //
    // 3.3V controller (0x1E780000): 7 sets (ABCD..YZAAAB), 208 pins, IRQ 40
    // 1.8V controller (0x1E780800): 2 sets (18ABCD, 18E), 36 pins, IRQ 11
    //
    // R/W register file. INT_STATUS registers are W1C (write-1-to-clear).
    // DATA_READ registers return corresponding DATA_VALUE.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_GPIO : IDoubleWordPeripheral, IKnownSize, IGPIOSender
    {
        public Aspeed_GPIO(int numberOfSets = 7)
        {
            this.numberOfSets = numberOfSets;
            storage = new uint[RegisterSpaceSize / 4];

            // Build lookup tables for special registers
            intStatusOffsets = new HashSet<uint>();
            dataReadMap = new Dictionary<uint, uint>();

            if(numberOfSets >= 7)
            {
                // 3.3V GPIO: 7 sets
                BuildSetMappings_3_3V();
            }
            else
            {
                // 1.8V GPIO: 2 sets
                BuildSetMappings_1_8V();
            }

            Reset();
        }

        public long Size => RegisterSpaceSize;

        public GPIO IRQ { get; } = new GPIO();

        public void Reset()
        {
            Array.Clear(storage, 0, storage.Length);
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset < 0 || offset >= RegisterSpaceSize)
            {
                return 0;
            }

            var byteOff = (uint)offset;

            // DATA_READ registers return the corresponding DATA_VALUE
            if(dataReadMap.TryGetValue(byteOff, out var dataValueOff))
            {
                return storage[dataValueOff / 4];
            }

            return storage[byteOff / 4];
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset < 0 || offset >= RegisterSpaceSize)
            {
                return;
            }

            var byteOff = (uint)offset;
            var reg = byteOff / 4;

            // DATA_READ registers are read-only
            if(dataReadMap.ContainsKey(byteOff))
            {
                return;
            }

            // INT_STATUS registers are W1C
            if(intStatusOffsets.Contains(byteOff))
            {
                storage[reg] &= ~value;
                UpdateIRQ();
                return;
            }

            storage[reg] = value;
        }

        private void UpdateIRQ()
        {
            bool anyPending = false;
            foreach(var off in intStatusOffsets)
            {
                var enableOff = off - 0x10; // INT_ENABLE is 0x10 before INT_STATUS in each set's block
                if(storage[off / 4] != 0)
                {
                    anyPending = true;
                    break;
                }
            }
            IRQ.Set(anyPending);
        }

        private void BuildSetMappings_3_3V()
        {
            // INT_STATUS offsets (byte addresses) — W1C
            uint[] intStatOffsets = { 0x018, 0x038, 0x0A8, 0x0F8, 0x128, 0x158, 0x188 };
            foreach(var off in intStatOffsets)
            {
                intStatusOffsets.Add(off);
            }

            // DATA_READ → DATA_VALUE mappings
            dataReadMap[0x0C0] = 0x000; // ABCD
            dataReadMap[0x0C4] = 0x020; // EFGH
            dataReadMap[0x0C8] = 0x070; // IJKL
            dataReadMap[0x0CC] = 0x078; // MNOP
            dataReadMap[0x0D0] = 0x080; // QRST
            dataReadMap[0x0D4] = 0x088; // UVWX
            dataReadMap[0x0D8] = 0x1E0; // YZAAAB
        }

        private void BuildSetMappings_1_8V()
        {
            // 1.8V has same layout as first 2 sets of 3.3V
            intStatusOffsets.Add(0x018); // 18ABCD
            intStatusOffsets.Add(0x038); // 18E

            dataReadMap[0x0C0] = 0x000; // 18ABCD
            dataReadMap[0x0C4] = 0x020; // 18E
        }

        private readonly int numberOfSets;
        private readonly uint[] storage;
        private readonly HashSet<uint> intStatusOffsets;
        private readonly Dictionary<uint, uint> dataReadMap;

        private const int RegisterSpaceSize = 0x800;
    }
}
