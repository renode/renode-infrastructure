//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Network
{
    public enum CRCMode
    {
        // do not compute a new CRC and do not try to use the CRC from the data
        NoOperation,
        // compute a CRC
        Add,
        // remove a CRC from the data and compute a new CRC
        Replace,
        // use the CRC from the data
        Keep,
    }
}
