//
// Copyright (c) 2026 Microsoft
// Licensed under the MIT License.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.I2C
{
    // Aspeed AST2600 I2C Controller — 16 buses
    // Reference: QEMU hw/i2c/aspeed_i2c.c (AST2600 "new mode")
    //
    // Memory map (0x1000 total):
    //   0x000-0x00F: Global control
    //   0x080+N*0x80: Bus N registers (N=0..15)
    //   0xC00-0xDFF: Shared buffer pool
    //
    // Stub behavior: empty buses return TX_NAK on START+TX commands.
    // Linux aspeed-i2c driver probes successfully with NACKs.
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class Aspeed_I2C : IDoubleWordPeripheral, IKnownSize, INumberedGPIOOutput
    {
        public Aspeed_I2C()
        {
            storage = new uint[RegisterSpaceSize / 4];
            var gpios = new Dictionary<int, IGPIO>();
            for(int i = 0; i < NumBuses; i++)
            {
                gpios[i] = new GPIO();
            }
            Connections = gpios;
            Reset();
        }

        public long Size => RegisterSpaceSize;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

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
            return storage[(uint)offset / 4];
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset < 0 || offset >= RegisterSpaceSize)
            {
                return;
            }

            var reg = (uint)offset / 4;

            // Determine if this is a bus register
            int busIndex = GetBusIndex((uint)offset);
            if(busIndex >= 0)
            {
                uint busLocalOffset = ((uint)offset - BusBase) % BusSpacing;
                HandleBusWrite(busIndex, busLocalOffset, value);
                return;
            }

            // Global registers and pool: plain R/W
            storage[reg] = value;
        }

        private void HandleBusWrite(int bus, uint localOffset, uint value)
        {
            uint absOffset = BusBase + (uint)bus * BusSpacing + localOffset;
            uint reg = absOffset / 4;

            switch(localOffset)
            {
                case MasterIntrSts: // I2CM_INTR_STS — W1C
                    storage[reg] &= ~(value & 0xF007F07F);
                    UpdateBusIRQ(bus);
                    return;

                case SlaveIntrSts: // I2CS_INTR_STS — W1C
                    storage[reg] &= ~value;
                    UpdateBusIRQ(bus);
                    return;

                case MasterCmd: // I2CM_CMD — command execution
                    storage[reg] = value;
                    HandleMasterCommand(bus, value);
                    return;

                case AcTiming:
                    storage[reg] = value & 0x1FFFF0FF;
                    return;

                case MasterIntrCtrl:
                    storage[reg] = value & 0x0007F07F;
                    return;

                default:
                    storage[reg] = value;
                    return;
            }
        }

        private void HandleMasterCommand(int bus, uint cmd)
        {
            uint absOffset = BusBase + (uint)bus * BusSpacing;
            uint intrStsReg = (absOffset + MasterIntrSts) / 4;

            bool startCmd = (cmd & CmdStartBit) != 0;
            bool txCmd = (cmd & CmdTxBit) != 0;
            bool rxCmd = (cmd & CmdRxBit) != 0;
            bool stopCmd = (cmd & CmdStopBit) != 0;

            if(startCmd && txCmd)
            {
                // START + TX: send address byte → NACK (no devices on stub bus)
                storage[intrStsReg] |= IntrTxNak;
            }
            else if(txCmd)
            {
                // Data TX → NACK
                storage[intrStsReg] |= IntrTxNak;
            }
            else if(rxCmd)
            {
                // RX → NACK (no device to receive from)
                storage[intrStsReg] |= IntrTxNak;
            }

            if(stopCmd)
            {
                storage[intrStsReg] |= IntrNormalStop;
            }

            // Clear the command bits (command consumed)
            storage[(absOffset + MasterCmd) / 4] &= ~(CmdStartBit | CmdTxBit | CmdRxBit | CmdStopBit);

            UpdateBusIRQ(bus);
        }

        private void UpdateBusIRQ(int bus)
        {
            uint absOffset = BusBase + (uint)bus * BusSpacing;
            uint intrSts = storage[(absOffset + MasterIntrSts) / 4];
            uint intrCtrl = storage[(absOffset + MasterIntrCtrl) / 4];

            bool pending = (intrSts & intrCtrl) != 0;
            ((GPIO)Connections[bus]).Set(pending);
        }

        private int GetBusIndex(uint offset)
        {
            if(offset < BusBase || offset >= BusBase + NumBuses * BusSpacing)
            {
                return -1;
            }
            return (int)((offset - BusBase) / BusSpacing);
        }

        private readonly uint[] storage;

        // Layout
        private const uint BusBase = 0x80;
        private const uint BusSpacing = 0x80;
        private const int NumBuses = 16;

        // Per-bus register offsets (relative to bus base)
        private const uint FunCtrl        = 0x00;
        private const uint AcTiming       = 0x04;
        private const uint TxRxByteBuf    = 0x08;
        private const uint PoolCtrl       = 0x0C;
        private const uint MasterIntrCtrl = 0x10;
        private const uint MasterIntrSts  = 0x14;
        private const uint MasterCmd      = 0x18;
        private const uint SlaveIntrCtrl  = 0x20;
        private const uint SlaveIntrSts   = 0x24;

        // Command bits
        private const uint CmdStartBit = 1u << 0;
        private const uint CmdTxBit    = 1u << 1;
        private const uint CmdRxBit    = 1u << 3;
        private const uint CmdStopBit  = 1u << 5;

        // Interrupt status bits
        private const uint IntrTxAck      = 1u << 0;
        private const uint IntrTxNak      = 1u << 1;
        private const uint IntrRxDone     = 1u << 2;
        private const uint IntrNormalStop = 1u << 4;
        private const uint IntrAbnormal   = 1u << 5;

        private const int RegisterSpaceSize = 0x1000;
    }
}
