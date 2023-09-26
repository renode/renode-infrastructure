//
// Copyright (c) 2010-2023 Antmicro
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
        FormatUnit = 0x04,
        Read6 = 0x08,
        Write6 = 0x0A,
        Inquiry = 0x12,
        ModeSense6 = 0x1A,
        StartStopUnit = 0x1B,
        SendDiagnostic = 0x1D,
        PreventAllowMediumRemoval = 0x1E,
        ReadCapacity = 0x25, // ReadCapacity (10)
        Read10 = 0x28,
        Write10 = 0x2A,
        Verify10 = 0x2F,
        PreFetch10 = 0x34,
        SynchronizeCache10 = 0x35,
        WriteBuffer = 0x3B,
        ReadBuffer = 0x3C,
        Unmap = 0x42,
        ModeSelect10 = 0x55,
        ModeSense10 = 0x5A,
        Read16 = 0x88,
        Write16 = 0x8A,
        PreFetch16 = 0x90,
        SynchronizeCache16 = 0x91,
        ReadCapacity16 = 0x9E,
        ReportLUNs = 0xA0,
        SecurityProtocolIn1 = 0xA2,
        SecurityProtocolOut1 = 0xB5,
    }
}