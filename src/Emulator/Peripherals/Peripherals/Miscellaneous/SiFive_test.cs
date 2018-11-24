//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class SiFive_test : IDoubleWordPeripheral
    {
        public SiFive_test(Machine machine)
        {
        }

        public uint ReadDoubleWord(long offset)
        {
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
	    Console.Error.WriteLine("Test finisher exit: 0x{0:x}", value);
	    if (offset == 0) {
		    int status = (int)(value & 0xffff);
		    int code = (int)((value >> 16) & 0xffff);
		    switch (status) {
		    case TEST_FAIL:
			    Environment.Exit(code);
			    break;
		    case TEST_PASS:
			    Environment.Exit(0);
			    break;
		    default:
			    break;
		    }
	    }
	    Environment.Exit(-1);
	}

        public void Reset()
        {
        }

	private const int TEST_FAIL = 0x3333;
	private const int TEST_PASS = 0x5555;

    }
}
