//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class Arm_Dma250 : Arm_Dma350
    {
        public Arm_Dma250(IMachine machine, int numberOfChannels = 8) : base(machine, numberOfChannels, Arm_Dma350.Variant.Dma250)
        {
        }
    }
}
