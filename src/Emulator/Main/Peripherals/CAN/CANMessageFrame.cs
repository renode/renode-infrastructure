//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.CAN
{
    public class CANMessageFrame
    {
        public CANMessageFrame(uint id, byte[] data, bool extendedFormat = false, bool remoteFrame = false, bool fdFormat = false, bool bitRateSwitch = false)
        {
            Id = id;
            Data = data;
            ExtendedFormat = extendedFormat;
            RemoteFrame = remoteFrame;
            FDFormat = fdFormat;
            BitRateSwitch = bitRateSwitch;
        }

        public override string ToString()
        {
            return $"[Message: Data=[{DataAsHex}], Remote={RemoteFrame}, Extended={ExtendedFormat}, BitRateSwitch={BitRateSwitch}, FDFormat={FDFormat}, Id={Id}, DataLength={Data.Length}]";
        }

        public byte[] ToSocketCAN(bool useNetworkByteOrder)
        {
            if(!FDFormat)
            {
                if(Data.Length > ClassicalSocketCANFrame.MaxDataLength)
                {
                    throw new RecoverableException($"Classical frame cannot exceed {ClassicalSocketCANFrame.MaxDataLength} bytes of data");
                }
                return ClassicalSocketCANFrame.FromCANMessageFrame(this).Encode(useNetworkByteOrder);
            }

            if(Data.Length > FlexibleSocketCANFrame.MaxDataLength)
            {
                throw new RecoverableException($"FD frame cannot exceed {FlexibleSocketCANFrame.MaxDataLength} bytes of data");
            }
            return FlexibleSocketCANFrame.FromCANMessageFrame(this).Encode(useNetworkByteOrder);
        }

        public string DataAsHex => Data.Select(x => "0x{0:X2}".FormatWith(x)).Stringify(", ");

        public uint Id { get; }
        public byte[] Data { get; }
        public bool ExtendedFormat { get; }
        public bool RemoteFrame { get; }
        public bool FDFormat { get; }
        public bool BitRateSwitch { get; }
    }
}
