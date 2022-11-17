//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class CalculateCRCCommand : Command
    {
        public CalculateCRCCommand(CommandsManager manager) : base(manager)
        {
            // CRC engine is initialized according to the gdb specification
            // Source: https://sourceware.org/gdb/onlinedocs/gdb/Separate-Debug-Files.html#Separate-Debug-Files
            crcEngine = new CRCEngine(CRCPolynomial.CRC32, false, false, 0xffffffff, 0);
        }

        [Execute("qCRC:")]
        public PacketData Execute(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]ulong address,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]uint length)
        {
            var accesses = GetTranslatedAccesses(address, length, write: false);

            if(accesses == null)
            {
                return PacketData.ErrorReply(Error.BadAddress);
            }

            byte[] data = new byte[length];
            int currentIndex = 0;

            foreach(var access in accesses)
            {
                if(manager.Machine.SystemBus.WhatIsAt(access.Address, context: manager.Cpu) == null)
                {
                    return PacketData.ErrorReply(Error.BadAddress);
                }

                try
                {
                    manager.Machine.SystemBus.ReadBytes(access.Address, (int)access.Length, data, currentIndex, onlyMemory: true, context: manager.Cpu);
                    currentIndex += (int)access.Length;
                }
                catch(RecoverableException)
                {
                    return PacketData.ErrorReply(Error.BadAddress);
                }
            }

            return new PacketData($"C{crcEngine.Calculate(data):X8}");
        }

        private readonly CRCEngine crcEngine;
    }
}
