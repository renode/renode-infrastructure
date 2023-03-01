//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Bus
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord | AllowedTranslation.QuadWordToDoubleWord)]
    public class Silencer : IDoubleWordPeripheral
    {
        public uint ReadDoubleWord(long offset)
        {
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {

        }

        public void Reset()
        {

        }

    }
}

