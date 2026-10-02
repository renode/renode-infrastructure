//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core
{
    public readonly struct StateMask
    {
        public StateMask(ulong state, ulong mask)
        {
            State = state;
            Mask = mask;
        }

        public bool HasMaskBit(int position)
        {
            return BitHelper.IsBitSet(Mask, (byte)position);
        }

        public StateMask WithBitValue(int position, bool value)
        {
            var bit = 1UL << position;
            return new StateMask(value ? (State | bit) : (State & ~bit), Mask | bit);
        }

        public override string ToString()
        {
            return $"[StateMask: State={State:x}, Mask={Mask:x}]";
        }

        public readonly ulong State;
        public readonly ulong Mask;

        public static readonly StateMask AllAccess = new StateMask(0, 0);
    }
}
