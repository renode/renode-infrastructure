//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
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
            return $"[Message: Data={DataAsHex}, Remote={RemoteFrame}, Extended={ExtendedFormat}, BitRateSwitch={BitRateSwitch}, FDFormat={FDFormat}, Id={Id}, DataLength={Data.Length}]";
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

        public static bool TryFromSocketCAN(ISocketCANFrame frame, out CANMessageFrame message)
        {
            message = default(CANMessageFrame);

            if(frame is ClassicalSocketCANFrame classicalFrame)
            {
                if(classicalFrame.errorMessageFrame)
                {
                    return false;
                }

                message = new CANMessageFrame(
                    id: classicalFrame.id,
                    data: classicalFrame.data.CopyAndResize(classicalFrame.length),
                    extendedFormat: classicalFrame.extendedFrameFormat,
                    remoteFrame: classicalFrame.remoteTransmissionRequest,
                    fdFormat: false,
                    bitRateSwitch: false
                );
                return true;
            }

            if(frame is FlexibleSocketCANFrame fdFrame)
            {
                if(fdFrame.errorMessageFrame)
                {
                    return false;
                }

                message = new CANMessageFrame(
                    id: fdFrame.id,
                    data: fdFrame.data.CopyAndResize(fdFrame.length),
                    extendedFormat: fdFrame.extendedFrameFormat,
                    remoteFrame: fdFrame.remoteTransmissionRequest,
                    fdFormat: true,
                    bitRateSwitch: fdFrame.bitRateSwitch
                );
                return true;
            }

            return false;
        }

        public static bool TryFromSocketCAN(IList<byte> data, out CANMessageFrame message, out int bytesUsed, bool useNetworkByteOrder)
        {
            if(data.TryDecodeAsSocketCANFrame(out var frame, useNetworkByteOrder))
            {
                bytesUsed = frame.Size;
                return TryFromSocketCAN(frame, out message);
            }

            message = default(CANMessageFrame);
            bytesUsed = 0;
            return false;
        }

        public string DataAsHex => Misc.PrettyPrintCollectionHex(Data);

        public uint Id { get; }
        public byte[] Data { get; }
        public bool ExtendedFormat { get; }
        public bool RemoteFrame { get; }
        public bool FDFormat { get; }
        public bool BitRateSwitch { get; }
    }
}
