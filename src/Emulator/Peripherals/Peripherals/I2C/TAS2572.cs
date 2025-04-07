//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class TAS2572 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public TAS2572()
        {
            // Registers are grouped into pages and books. Page 0 to Page 8 are in Book 0, while Page 9 is in Book 100.
            // Not all books and pages are implemented currently.
            var book0 = new Dictionary<int, ByteRegisterCollection>();
            var book0page0 = DefineBook0Page0Registers();

            book0[0] = book0page0;
            booksPages[Book0] = book0;

            Reset();
        }

        public void Write(byte[] data)
        {
            this.NoisyLog("Written {0} bytes", data.Length);
            foreach(var b in data)
            {
                WriteByte(b);
            }
        }

        public byte[] Read(int count)
        {
            var data = new byte[count];
            for(var i = 0; i < count; i++)
            {
                var result = RegistersCollection.Read(address);
                this.NoisyLog("Reading register 0x{0:X} from device: 0x{1:X}", address, result);
                data[i] = result;
                SetNextAddress();
            }

            return data;
        }

        public void FinishTransmission()
        {
            this.NoisyLog("Finishing transmission, going to the idle state");
            ResetState();
        }

        public virtual void Reset()
        {
            address = 0;
            ResetState();
        }

        public ByteRegisterCollection RegistersCollection
        {
            get
            {
                TryGetPageInBookOrDefault((int)deviceBook.Value, (int)devicePage.Value, out var page);
                return page;
            }
        }

        private void ResetState()
        {
            // Address is kept until it is overwritten in WriteByte; this way Read method can access register at previously selected address.
            state = State.CollectingAddress;
        }

        private void WriteByte(byte b)
        {
            switch(state)
            {
            case State.CollectingAddress:
                address = b;
                this.NoisyLog("Setting register address to 0x{0:X}", address);
                state = State.Processing;
                break;
            case State.Processing:
                this.NoisyLog("Writing value 0x{0:X} to register 0x{1:X}", b, address);
                RegistersCollection.Write(address, b);
                SetNextAddress();
                break;
            default:
                throw new ArgumentException($"Unexpected state: {state}");
            }
        }

        // During multi-byte transfers, after transmitting data to/from the register, the register address is incremented.
        private void SetNextAddress()
        {
            address++;
        }

        private bool TryGetPageInBookOrDefault(int bookNumber, int pageNumber, out ByteRegisterCollection page)
        {
            var foundBookAndPage = true;
            if(!booksPages.TryGetValue(bookNumber, out var book))
            {
                book = booksPages[DefaultBook];
                foundBookAndPage = false;
            }
            if(!book.TryGetValue(pageNumber, out page))
            {
                page = book[DefaultPage];
                foundBookAndPage = false;
            }
            return foundBookAndPage;
        }

        private ByteRegisterCollection DefineBook0Page0Registers()
        {
            var page = new ByteRegisterCollection(this);

            Book0Page0Registers.DevicePage.Define(page)
                .WithValueField(0, 8, out devicePage, changeCallback: (_, __) =>
                {
                    if(!TryGetPageInBookOrDefault((int)deviceBook.Value, (int)devicePage.Value, out var unused))
                    {
                        this.WarningLog("Registers for page {0} in book {1} are not implemented", devicePage.Value, deviceBook.Value);
                    }
                }, name: "PAGE");

            Book0Page0Registers.SoftwareReset.Define(page)
                .WithFlag(0, valueProviderCallback: _ => false /* bit is self clearing*/, name: "SW_RESET")
                .WithReservedBits(1, 7);

            Book0Page0Registers.DeviceBook.Define(page)
                .WithValueField(0, 8, out deviceBook, changeCallback: (_, __) =>
                {
                    if(!TryGetPageInBookOrDefault((int)deviceBook.Value, (int)devicePage.Value, out var unused))
                    {
                        this.WarningLog("Registers for page {0} in book {1} are not implemented", devicePage.Value, deviceBook.Value);
                    }
                }, name: "BOOK");

            return page;
        }

        private uint address;
        private State state;

        private IValueRegisterField devicePage;
        private IValueRegisterField deviceBook;

        private readonly Dictionary<int, Dictionary<int, ByteRegisterCollection>> booksPages = new Dictionary<int, Dictionary<int, ByteRegisterCollection>>();

        private const int Book0 = 0;
        private const int Book100 = 100;
        private const int DefaultBook = Book0;
        private const int DefaultPage = 0;

        private enum State
        {
            CollectingAddress,
            Processing
        }

        private enum Book0Page0Registers
        {
            DevicePage = 0x0,                       // PAGE
            SoftwareReset = 0x1,                    // SW_RESET
            PowerControl = 0x2,                     // PWR_CTL
            DeviceConfiguration1 = 0x3,             // DEVICE_CFG_01
            DeviceConfiguration2 = 0x4,             // DEVICE_CFG_02
            DeviceConfiguration3 = 0x5,             // DEVICE_CFG_03
            DeviceConfiguration4 = 0x6,             // DEVICE_CFG_04
            DeviceConfiguration5 = 0x7,             // DEVICE_CFG_05
            TDMConfiguration1 = 0x8,                // TDM_CFG1
            TDMConfiguration2 = 0x9,                // TDM_CFG2
            TDMConfiguration3 = 0xA,                // TDM_CFG3
            TDMConfiguration5 = 0xC,                // TDM_CFG5
            TDMConfiguration6 = 0xD,                // TDM_CFG6
            TDMConfiguration7 = 0xE,                // TDM_CFG7
            TDMConfiguration8 = 0xF,                // TDM_CFG8
            TDMConfiguration9 = 0x10,               // TDM_CFG9
            TDMConfiguration10 = 0x11,              // TDM_CFG10
            TDMConfiguration11 = 0x12,              // TDM_CFG11
            TDMConfiguration12 = 0x13,              // TDM_CFG12
            TDMClockDetectionMonitor = 0x14,        // TDM_DET
            MonitoringConfiguration1 = 0x15,        // MONITOR_CFG_01
            MonitoringConfiguration2 = 0x16,        // MONITOR_CFG_02
            LimiterConfiguration = 0x17,            // LIM_CFG_0
            BrownOutProtectionConfiguration = 0x18, // BOP_CFG_0
            InternalToneGenerator1 = 0x19,          // TONE_GEN_01
            InternalToneGenerator2 = 0x1A,          // TONE_GEN_02
            IOConfiguration1 = 0x1B,                // IO_CFG_01
            IOConfiguration2 = 0x1C,                // IO_CFG_02
            IOConfiguration3 = 0x1D,                // IO_CFG_03
            NoiseGateControls = 0x1E,               // NG_CFG0
            BoostConfiguration1 = 0x21,             // BST_CFG_01
            BoostConfiguration2 = 0x22,             // BST_CFG_02
            BoostConfiguration3 = 0x24,             // BST_CFG_03
            IRQZClear = 0x25,                       // INTERRUPT_CFG1
            VBATMonitorMSB1 = 0x26,                 // SAR_MONITOR_01
            VBATMonitorMSB2 = 0x27,                 // SAR_MONITOR_02
            PVDDMonitorMSB3 = 0x28,                 // SAR_MONITOR_03
            PVDDMonitorMSB4 = 0x29,                 // SAR_MONITOR_04
            TemperatureMonitor = 0x2A,              // TEMP_MONITOR
            ClassDAmpConfigurations1 = 0x31,        // CLASSD_CFG_01
            ClassDAmpConfigurations2 = 0x32,        // CLASSD_CFG_02
            BoostConfiguration = 0x3B,              // BST_CFG_05
            ThermalWarningConfiguration = 0x3C,     // THERM_CFG
            InterruptMasks0 = 0x5B,                 // INT_MASK_0
            InterruptMasks1 = 0x5C,                 // INT_MASK_1
            InterruptMasks2 = 0x5D,                 // INT_MASK_2
            InterruptMasks3 = 0x5E,                 // INT_MASK_3
            InterruptMasks4 = 0x5F,                 // INT_MASK_4
            LatchedInterruptReadback0 = 0x60,       // INT_LATCH_0
            LatchedInterruptReadback1 = 0x61,       // INT_LATCH_1
            LatchedInterruptReadback2 = 0x62,       // INT_LATCH_2
            LatchedInterruptReadback3 = 0x63,       // INT_LATCH_3
            LatchedInterruptReadback4 = 0x64,       // INT_LATCH_4
            NoiseGateIdleStatus = 0x65,             // NG_IDLE_STATUS
            RevisionAndPGID = 0x78,                 // REV_ID
            DeviceBook = 0x7F,                      // BOOK
        }
    }
}