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
    public class RenesasRA2_GPIO : RenesasRA_GPIO
    {
        public RenesasRA2_GPIO(IMachine machine, int portNumber, int numberOfConnections, RenesasRA_GPIOMisc pfsMisc)
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
                new InterruptOutput(2,  IRQ2),
                new InterruptOutput(4,  IRQ3),
                new InterruptOutput(15, IRQ7),
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
                new InterruptOutput(5,  IRQ1),
                new InterruptOutput(6,  IRQ0),
                new InterruptOutput(12, IRQ3),
                new InterruptOutput(13, IRQ2),
            },
            /* PORT3 */ new List<InterruptOutput>
            {
                new InterruptOutput(1,  IRQ6),
                new InterruptOutput(2,  IRQ5),
            },
            /* PORT4 */ new List<InterruptOutput>
            {
                new InterruptOutput(0,  IRQ0),
                new InterruptOutput(1,  IRQ5),
                new InterruptOutput(2,  IRQ4),
                new InterruptOutput(8,  IRQ7),
                new InterruptOutput(9,  IRQ6),
                new InterruptOutput(10, IRQ5),
                new InterruptOutput(11, IRQ4),
            },
            /* PORT5 */ new List<InterruptOutput>
            {
                // Intentionally left blank
            },
            /* PORT6 */ new List<InterruptOutput>
            {
                // Intentionally left blank
            },
            /* PORT7 */ new List<InterruptOutput>
            {
                // Intentionally left blank
            },
            /* PORT8 */ new List<InterruptOutput>
            {
                // Intentionally left blank
            },
            /* PORT9 */ new List<InterruptOutput>
            {
                // Intentionally left blank
            },
            /* PORTA */ new List<InterruptOutput>
            {
                // Intentionally left blank
            },
            /* PORTB */ new List<InterruptOutput>
            {
                // Intentionally left blank
            },
        };
    }
}
