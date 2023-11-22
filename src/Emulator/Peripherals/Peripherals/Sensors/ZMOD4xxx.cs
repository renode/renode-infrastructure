//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class ZMOD4xxx : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ISensor
    {
        public ZMOD4xxx(Model model)
        {
            this.model = model;
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        public void Write(byte[] data)
        {
            throw new NotImplementedException();
        }

        public byte[] Read(int count)
        {
            throw new NotImplementedException();
        }

        public void FinishTransmission()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public decimal AirQuality
        {
            get; set;
        }

        private void DefineRegisters()
        {
            Registers.X.Define(this)
                .WithTag("X", 0, 8);

            Registers.Y.Define(this)
                .WithTag("Y", 0, 8);
        }

        private readonly Model model;

        public enum Model
        {
            ZMOD4410 = 0x32,
            ZMOD4510 = 0x33,
        }

        private enum Registers : byte
        {
            X = 0x88,
            Y = 0x8B,
        }
    }
}
