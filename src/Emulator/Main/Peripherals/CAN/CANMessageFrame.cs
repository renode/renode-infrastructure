//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.CAN
{
    public class CANMessageFrame
    {
        public static CANMessageFrame CreateWithExtendedId(uint id, byte[] data, bool extendedFormat = false, bool remoteFrame = false, bool fdFormat = false, bool bitRateSwitch = false)
        {
            return new CANMessageFrame(extendedFormat ? id : id >> StandardIdOffset, data, extendedFormat, remoteFrame, fdFormat, bitRateSwitch);
        }

        public CANMessageFrame(uint standardIdPart, uint extendedIdPart, byte[] data, bool extendedFormat = false, bool remoteFrame = false, bool fdFormat = false, bool bitRateSwitch = false)
            : this(extendedFormat ? standardIdPart << StandardIdOffset | extendedIdPart : standardIdPart, data, extendedFormat, remoteFrame, fdFormat, bitRateSwitch)
        {
            DebugHelper.Assert(standardIdPart < (1 << StandardIdWidth));
            DebugHelper.Assert(!extendedFormat || extendedIdPart < (1 << StandardIdOffset));
        }

        public CANMessageFrame(uint id, byte[] data, bool extendedFormat = false, bool remoteFrame = false, bool fdFormat = false, bool bitRateSwitch = false)
        {
            // id := standard[10:0]                     if !extendedFormat
            //       standard[28:18] + extended[17:0]   otherwise
            DebugHelper.Assert(id < 1 << (extendedFormat ? ExtendedIdWidth : StandardIdWidth));
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

        public byte[] ToSocketCAN(bool useNetworkByteOrder = false)
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

        public string DataAsHex => Misc.PrettyPrintCollectionHex(Data);

        public uint Id { get; }
        public byte[] Data { get; }
        public bool ExtendedFormat { get; }
        public bool RemoteFrame { get; }
        public bool FDFormat { get; }
        public bool BitRateSwitch { get; }

        public uint ExtendedId => ExtendedFormat ? Id : (Id << StandardIdOffset);
        public uint ExtendedIdPart => ExtendedFormat ? (Id & ExtendedIdMask) : 0;
        public uint StandardIdPart => ExtendedFormat ? (Id >> StandardIdOffset) : Id;

        private const byte StandardIdWidth = 11;
        private const byte ExtendedIdWidth = 29;
        private const uint ExtendedIdMask = (1 << ExtendedIdWidth) - 1;
        private const byte StandardIdOffset = ExtendedIdWidth - StandardIdWidth;
    }
}
