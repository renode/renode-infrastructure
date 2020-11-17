//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.I2C;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class EmptyPeripheral : II2CPeripheral, IBytePeripheral, IDoubleWordPeripheral
    {
        // TODO: more interfaces

        public EmptyPeripheral()
        {
            Increment();
        }

        public void FinishTransmission()
        {
        }

        public EmptyPeripheral(double value)
        {
            Increment();
            Increment();
        }

        public EmptyPeripheral(int value, bool enabled = false)
        {
            Increment();
            Increment();
            Increment();
        }

        public virtual void Reset()
        {

        }

        public byte[] Read(int count)
        {
            return new byte[] { 0 };
        }

        public void Write(byte[] data)
        {

        }

        public byte ReadByte(long offset)
        {
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {

        }

        public virtual uint ReadDoubleWord(long offset)
        {
            return 0;
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {

        }

        public void Increment()
        {
            Counter++;
        }

        public int ThrowingProperty
        {
            get
            {
                return 0;
            }
            set
            {
                throw new RecoverableException("Fake exception");
            }
        }

        public bool BooleanProperty { get; set; }

        public int Counter { get; private set; }
    }
}

