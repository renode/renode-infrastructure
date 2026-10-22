//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    /// <summary>
    /// Implemented by CPUs whose GDB register numbering is fixed by the debugger build rather than
    /// sent in a target description (for example Xtensa, where each core configuration has its own
    /// GDB with a hard-coded register file). When present, the GDB stub uses this map for the
    /// 'g', 'p' and 'P' packets instead of the CPU's own register indices.
    /// </summary>
    public interface ICpuWithGdbRegisterMap : ICpuSupportingGdb
    {
        /// <summary>
        /// The registers GDB transfers in its 'g' packet, in GDB register number order, or null
        /// when the CPU has no map for its current model (the default behaviour is used then).
        /// </summary>
        IReadOnlyList<GdbRegisterSlot> GdbRegisterMap { get; }
    }

    public struct GdbRegisterSlot
    {
        public GdbRegisterSlot(int? index, uint width)
        {
            Index = index;
            Width = width;
        }

        /// <summary>The CPU register index, or null when GDB expects a register the CPU does not model.</summary>
        public int? Index { get; }

        /// <summary>Width in bits as GDB expects it.</summary>
        public uint Width { get; }
    }
}
