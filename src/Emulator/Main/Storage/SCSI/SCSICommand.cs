//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Storage.SCSI
{
    public enum SCSICommand : byte
    {
        TestUnitReady = 0x00,
        RequestSense = 0x03,
        Inquiry = 0x12,
        ReadCapacity = 0x25,
        ModeSense6 = 0x1A,
        PreventAllowMediumRemoval = 0x1E,
        Read10 = 0x28,
        Write10 = 0x2A
    }
}