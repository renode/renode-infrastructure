//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//  

// Uncomment the following line and rebuild in order to see packet dumps in the log
// #define DEBUG_PACKETS

using System;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.I2C
{
    public abstract class I2CPeripheralBase<T> : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection> where T : IConvertible
    {
        public I2CPeripheralBase(int addressLength)
        {
            this.addressLength = addressLength;

            cache = new SimpleCache();
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();

            Reset();
        }

        public void Write(byte[] data)
        {
#if DEBUG_PACKETS
            this.Log(LogLevel.Noisy, "Written {0} bytes: {1}", data.Length, Misc.PrettyPrintCollectionHex(data));
#else
            this.Log(LogLevel.Noisy, "Written {0} bytes", data.Length);
#endif
            foreach(var b in data)
            {
                WriteByte(b);
            }
        }

        public byte[] Read(int count)
        {
            var result = RegistersCollection.Read(address);
            this.NoisyLog("Reading register {0} (0x{1:X}) from device: 0x{2:X}", cache.Get(address, x => Enum.GetName(typeof(T), x)), address, result);

            return new byte [] { result };
        }

        public void FinishTransmission()
        {
            this.NoisyLog("Finishing transmission, going to the idle state");
            ResetState();
        }

        public virtual void Reset()
        {
            ResetState();
        }

        public ByteRegisterCollection RegistersCollection { get; }

        protected abstract void DefineRegisters();

        private void ResetState()
        {
            // clearing of the address variable itself
            // is deferred to the moment when
            // it will be overwritten in WriteByte;
            // this way ReadByte method can access it
            addressBitsLeft = addressLength; 
            state = State.CollectingAddress;
        }

        private void WriteByte(byte b)
        {
            switch(state)
            {
                case State.CollectingAddress:
                {
                    if(addressBitsLeft == addressLength)
                    {
                        // if this is the first write after
                        // resetting state, clear the address
                        // to make sure there are no stale bits
                        // (address can be shorter than 8-bits)
                        address = 0;
                    }

                    // address length is configurable to any number of bits,
                    // so we need to handle all possible cases - including
                    // values non-divisable by 8
                    var position = Math.Max(0, addressBitsLeft - 8);
                    var width = Math.Min(8, addressBitsLeft);

                    address = BitHelper.SetBitsFrom(address, b, position, width);
                    addressBitsLeft -= width;

                    if(addressBitsLeft == 0)
                    {
                        this.Log(LogLevel.Noisy, "Setting register address to {0} (0x{1:X})", cache.Get(address, x => Enum.GetName(typeof(T), x)), address);
                        state = State.Processing;
                    }
                    break;
                }

                case State.Processing:
                    this.Log(LogLevel.Noisy, "Writing value 0x{0:X} to register {1} (0x{2:X})", b, cache.Get(address, x => Enum.GetName(typeof(T), x)), address);
                    RegistersCollection.Write(address, b);
                    break;

                default:
                    throw new ArgumentException($"Unexpected state: {state}");
            }
        }

        private int addressBitsLeft;

        private uint address;
        private State state;

        private readonly int addressLength;
        private readonly SimpleCache cache;

        private enum State
        {
            CollectingAddress,
            Processing
        }
    }
}
