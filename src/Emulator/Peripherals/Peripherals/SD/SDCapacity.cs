//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.SD
{
    public static class SDCapacity
    {
        public static SDCapacityParameters SeekForCapacityParametes(long userCapacity)
        {
            if(userCapacity > MaxCapacity || userCapacity < 0)
            {
                throw new ConstructionException($"SD Card capacity is out of the range <1 - {MaxCapacity}>");
            }

            foreach(int blockSize in Enum.GetValues(typeof(BlockLength)))
            {
                foreach(int multiplier in Enum.GetValues(typeof(SizeMultiplier)))
                {
                    var sizeBytes = (long)Math.Ceiling(userCapacity / Math.Pow(2, 2 + multiplier + blockSize) - 1);
                    if(sizeBytes <= MaxDeviceSize)
                    {
                        //memory_capacity = (device_size+1) * 2^multiplier+2 * 2^block_length
                        var memoryCapacity = (long)Math.Ceiling(Math.Pow(2, 2 + multiplier + blockSize) * (sizeBytes + 1));
                        return new SDCapacityParameters(blockSize, multiplier, sizeBytes, memoryCapacity);
                    }
                }
            }

            // We should never reach here.
            throw new InvalidOperationException();
        }

        private const long MaxDeviceSize = 0xFFF;
        private const long MaxCapacity = 0x100000000;

        private enum SizeMultiplier
        {
            Multiplier4 = 0,
            Multiplier8 = 1,
            Multiplier16 = 2,
            Multiplier32 = 3,
            Multiplier64 = 4,
            Multiplier128 = 5,
            Multiplier256 = 6,
            Multiplier512 = 7
        }

        private enum BlockLength
        {
            Block512 = 9,
            Block1024 = 10,
            Block2048 = 11
            // other values are reserved
        }
    }

    public struct SDCapacityParameters
    {
        public SDCapacityParameters(int blockSize, int multiplier, long deviceSize, long memoryCapacity)
        {
            this.BlockSize = blockSize;
            this.Multiplier = multiplier;
            this.DeviceSize = deviceSize;
            this.MemoryCapacity = memoryCapacity;
        }

        public int BlockSize { get; }
        public int Multiplier { get; }
        public long DeviceSize { get; }
        public long MemoryCapacity { get; }
    }
}
