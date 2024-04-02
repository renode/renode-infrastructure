//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SD
{
    public static class SDHelpers
    {
        public static CardType TypeFromCapacity(ulong capacity)
        {
            if(capacity <= 2.GB())
            {
                return CardType.StandardCapacity_SC;
            }
            else if(capacity <= 32.GB())
            {
                return CardType.HighCapacity_HC;
            }
            else if(capacity <= 2.TB())
            {
                return CardType.ExtendedCapacity_XC;
            }
            else
            {
                throw new ArgumentException($"Unexpected capacity: {capacity}");
            }
        }

        public static uint BlockLengthInBytes(BlockLength blockSize)
        {
            return blockSize == BlockLength.Undefined
               ? 0
               : (uint)(1 << (int)blockSize);
        }

        public static SDCapacityParameters SeekForCapacityParameters(long capacity, BlockLength blockSize = BlockLength.Undefined)
        {
            DebugHelper.Assert((blockSize == BlockLength.Undefined) || (capacity % BlockLengthInBytes(blockSize) == 0));

            if(blockSize != BlockLength.Undefined)
            {
                if(TryFindParameters(capacity, blockSize, out var result))
                {
                    return result;
                }
            }
            else
            {
                foreach(BlockLength possibleBlockSize in Enum.GetValues(typeof(BlockLength)))
                {
                    if(possibleBlockSize == BlockLength.Undefined)
                    {
                        continue;
                    }
                    
                    if(TryFindParameters(capacity, possibleBlockSize, out var result))
                    {
                        return result;
                    }
                }
            }

            // We should never reach here.
            throw new InvalidOperationException($"Could not calculate capacity parameters for given arguments: capacity={capacity}, blockSize={blockSize}");
        }
        
        private static bool TryFindParameters(long capacity, BlockLength blockSize, out SDCapacityParameters parameters)
        {
            switch(TypeFromCapacity((ulong)capacity))
            {
                case CardType.StandardCapacity_SC:
                    return TryFindParametersStandardCapacity(capacity, blockSize, out parameters);
                    
                case CardType.HighCapacity_HC:
                case CardType.ExtendedCapacity_XC:
                    return TryFindParametersHighCapacity(capacity, blockSize, out parameters);

                default:
                    // unsupported type
                    parameters = default(SDCapacityParameters);
                    return false;
            }
        }

        private static bool TryFindParametersStandardCapacity(long capacity, BlockLength blockSize, out SDCapacityParameters parameters)
        {
            // cSize has only 12 bits, hence the limit
            const int MaxCSizeValue = (1 << 12) - 1;
            
            var numberOfBlocks = (int)(capacity >> (int)blockSize);

            foreach(var multiplierEncoded in Enum.GetValues(typeof(SizeMultiplier))
                    .Cast<SizeMultiplier>()
                    .Where(x => x != SizeMultiplier.Undefined)
                    .OrderByDescending(x => (int)x))
            {
                var multiplierValue = 1 << ((int)multiplierEncoded + 2);
                if(multiplierValue > numberOfBlocks)
                {
                    // we are looking for the highest possible multiplier that is lower-or-equal than numberOfBlocks
                    continue;
                }
                
                var cSize = (long)(numberOfBlocks / multiplierValue) - 1;
                if(cSize > MaxCSizeValue)
                {
                    // if for the highest possible multiplier cSize is still too high,
                    // checking smaller multipliers won't help; we can back-off immediately
                    break;
                }

                parameters = new SDCapacityParameters(blockSize, multiplierEncoded, cSize);
                return true;
            }

            parameters = default(SDCapacityParameters);
            return false;
        }
        
        private static bool TryFindParametersHighCapacity(long capacity, BlockLength blockSize, out SDCapacityParameters parameters)
        {
            // block sizes other than 512 are not allowed in the HC/XC modes
            if(blockSize != BlockLength.Block512)
            {
                parameters = default(SDCapacityParameters);
                return false;
            }
                
            var cSize = (capacity / 1024 / 512) - 1;

            // for HC/XC cards multiplier is not used
            parameters = new SDCapacityParameters(BlockLength.Block512, SizeMultiplier.Undefined, cSize);
            return true;
        }
    }
    
    public enum SizeMultiplier
    {
        Multiplier4 = 0,
        Multiplier8 = 1,
        Multiplier16 = 2,
        Multiplier32 = 3,
        Multiplier64 = 4,
        Multiplier128 = 5,
        Multiplier256 = 6,
        Multiplier512 = 7,
        // other values are reserved
        Undefined = int.MaxValue
    }

    public enum CardType
    {
        StandardCapacity_SC,
        HighCapacity_HC,
        ExtendedCapacity_XC
    }

    public enum BlockLength
    {
        Block512 = 9,
        Block1024 = 10, 
        Block2048 = 11,
        // other values are reserved
        Undefined = int.MaxValue
    }

    public struct SDCapacityParameters
    {
        public SDCapacityParameters(BlockLength blockSize, SizeMultiplier multiplier, long deviceSize)
        {
            this.BlockSize = blockSize;
            this.Multiplier = multiplier;
            this.DeviceSize = deviceSize;
        }

        public override string ToString()
        {
            return $"[BlockSize={BlockSize}, Multiplier={Multiplier}, DeviceSize={DeviceSize}, MemoryCapacity={MemoryCapacity}]";
        }

        public BlockLength BlockSize { get; }
        public SizeMultiplier Multiplier { get; }
        public long DeviceSize { get; }

        public long MemoryCapacity 
        {
            get
            {
                // HC/XC SD cards do not use Multiplier to describe size
                // but calculate the it by simply multiplying `DeviceSize + 1` by 512Kb;
                // we internally mark this by setting Multipler to Undefined 
                if(Multiplier == SizeMultiplier.Undefined)
                {
                    DebugHelper.Assert(BlockSize == BlockLength.Block512);
                    return (DeviceSize + 1) * 512.KB();
                }
                else
                {
                    return (DeviceSize + 1) * (1L << ((int)Multiplier + (int)BlockSize + 2));
                }
            }
        }


    }
}
