﻿﻿//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Utilities;

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
            return $"[Message: Data=[{Data.Select(x => "0x{0:X}".FormatWith(x)).Stringify()}], Remote: {RemoteFrame}, Extended:{ExtendedFormat}, BitRateSwitch:{BitRateSwitch}, Id={Id}, DataLength={Data.Length}]";
        }

        public uint Id { get; }
        public byte[] Data { get; }
        public bool ExtendedFormat { get; }
        public bool RemoteFrame { get; }
        public bool FDFormat { get; }
        public bool BitRateSwitch { get; }
    }
}
