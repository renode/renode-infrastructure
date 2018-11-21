﻿﻿//
// Copyright (c) 2010-2018 Antmicro
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
        public CANMessageFrame(uint id, byte[] data)
        {
            Id = id;
            Data = data;
        }

        public override string ToString()
        {
            return $"[Message: Data=[{Data.Select(x => "0x{0:X}".FormatWith(x)).Stringify()}], Id={Id}, DataLength={Data.Length}]";
        }

        public uint Id { get; }
        public byte[] Data { get; }
    }
}
