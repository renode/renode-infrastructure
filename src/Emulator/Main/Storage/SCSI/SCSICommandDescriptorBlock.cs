//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Storage.SCSI
{
    public struct SCSICommandDescriptorBlock
    {
        public static SCSICommand DecodeCommand(byte[] data, int dataOffset = 0)
        {
            return (SCSICommand)data[dataOffset];
        }
    }
}