//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Core
{
    public struct StateMask
    {
        public StateMask(ulong state, ulong mask)
        {
            State = state;
            Mask = mask;
        }

        public ulong State;
        public ulong Mask;
    }
}
