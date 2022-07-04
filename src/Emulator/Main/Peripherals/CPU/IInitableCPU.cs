//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface IInitableCPU : ICPU
    {
        void InitFromElf(ELFSharp.ELF.IELF elf);
        void InitFromUImage(ELFSharp.UImage.UImage uImage);
    }
}

