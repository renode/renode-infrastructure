//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class RenesasRA6_GPIO : RenesasRA_GPIO
    {
        public RenesasRA6_GPIO(IMachine machine, int portNumber, int numberOfConnections, RenesasRA_GPIOMisc pfsMisc)
            : base(machine, portNumber, numberOfConnections, pfsMisc)
        {
            // Intentionally left blank
        }

        protected override List<InterruptOutput>[] PinInterruptOutputs => new List<InterruptOutput>[]
        {
            /* PORT0 */ new List<InterruptOutput>
            {
                new InterruptOutput(0,  IRQ6),
                new InterruptOutput(1,  IRQ7),
                new InterruptOutput(2,  IRQ8),
                new InterruptOutput(4,  IRQ9),
                new InterruptOutput(5,  IRQ10),
                new InterruptOutput(6,  IRQ11),
                new InterruptOutput(8,  IRQ12),
                new InterruptOutput(9,  IRQ13),
                new InterruptOutput(10, IRQ14),
                new InterruptOutput(15, IRQ13),
            },
            /* PORT1 */ new List<InterruptOutput>
            {
                new InterruptOutput(0,  IRQ2),
                new InterruptOutput(1,  IRQ1),
                new InterruptOutput(4,  IRQ1),
                new InterruptOutput(5,  IRQ0),
                new InterruptOutput(10, IRQ3),
                new InterruptOutput(11, IRQ4),
            },
            /* PORT2 */ new List<InterruptOutput>
            {
                new InterruptOutput(2,  IRQ3),
                new InterruptOutput(3,  IRQ2),
                new InterruptOutput(5,  IRQ1),
                new InterruptOutput(6,  IRQ0),
                new InterruptOutput(12, IRQ3),
                new InterruptOutput(13, IRQ2),
            },
            /* PORT3 */ new List<InterruptOutput>
            {
                new InterruptOutput(1,  IRQ6),
                new InterruptOutput(2,  IRQ5),
                new InterruptOutput(4,  IRQ9),
                new InterruptOutput(5,  IRQ8),
            },
            /* PORT4 */ new List<InterruptOutput>
            {
                new InterruptOutput(0,  IRQ0),
                new InterruptOutput(1,  IRQ5),
                new InterruptOutput(2,  IRQ4),
                new InterruptOutput(3,  IRQ14),
                new InterruptOutput(4,  IRQ15),
                new InterruptOutput(8,  IRQ7),
                new InterruptOutput(9,  IRQ6),
                new InterruptOutput(10, IRQ5),
                new InterruptOutput(11, IRQ4),
                new InterruptOutput(14, IRQ9),
                new InterruptOutput(15, IRQ8),
            },
            /* PORT5 */ new List<InterruptOutput>
            {
                new InterruptOutput(1,  IRQ11),
                new InterruptOutput(2,  IRQ12),
                new InterruptOutput(5,  IRQ14),
                new InterruptOutput(6,  IRQ15),
                new InterruptOutput(11, IRQ15),
                new InterruptOutput(12, IRQ14),
            },
            /* PORT6 */ new List<InterruptOutput>
            {
                new InterruptOutput(15, IRQ7),
            },
            /* PORT7 */ new List<InterruptOutput>
            {
                new InterruptOutput(6,  IRQ7),
                new InterruptOutput(7,  IRQ8),
                new InterruptOutput(8,  IRQ11),
                new InterruptOutput(9,  IRQ10),
            },
            /* PORT8 */ new List<InterruptOutput>
            {
                new InterruptOutput(2,  IRQ3),
                new InterruptOutput(3,  IRQ2),
                new InterruptOutput(4,  IRQ1),
                new InterruptOutput(6,  IRQ0),
            },
            /* PORT9 */ new List<InterruptOutput>
            {
                new InterruptOutput(5,  IRQ8),
                new InterruptOutput(6,  IRQ9),
                new InterruptOutput(7,  IRQ10),
                new InterruptOutput(8,  IRQ11),
            },
            /* PORTA */ new List<InterruptOutput>
            {
                new InterruptOutput(8,  IRQ6),
                new InterruptOutput(9,  IRQ5),
                new InterruptOutput(10, IRQ4),
            },
            /* PORTB */ new List<InterruptOutput>
            {
                // Intentionally left blank
            },
        };
    }
}
