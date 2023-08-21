//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using static Antmicro.Renode.Peripherals.SPI.Cadence_xSPI;

namespace Antmicro.Renode.Peripherals.SPI.Cadence_xSPICommands
{
    internal interface IDMACommand
    {
        void WriteData(IReadOnlyList<byte> data);
        IList<byte> ReadData(int length);

        TransmissionDirection DMADirection { get; }
        uint DMADataCount { get; }
        bool DMATriggered { get; }
        bool DMAError { get; }
    }
}
