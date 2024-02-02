//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Utilities.GDB
{
    // Based on the extended "thread-id" syntax information from https://sourceware.org/gdb/current/onlinedocs/gdb.html/Packets.html
    // Shouldn't be used for "those packets and replies explicitly documented to include a process ID, rather than a thread-id".
    public struct PacketThreadId
    {
        /// <param name="gdbArgument">It must be a full argument, i.e., including <c>"p"</c> if it was present.</param>
        public PacketThreadId(string gdbArgument)
        {
            var ids = gdbArgument.TrimStart('p').Split('.');

            if(ids.Length == 1 && TryParseId(ids[0], out var id))
            {
                if(gdbArgument.StartsWith('p'))
                {
                    ProcessId = id;
                    ThreadId = All;
                }
                else
                {
                    ProcessId = null;
                    ThreadId = id;
                }
            }
            else if(ids.Length == 2 && gdbArgument.StartsWith('p')
                && TryParseId(ids[0], out var id1) && id1 != All && TryParseId(ids[1], out var id2))
            {
                ProcessId = id1;
                ThreadId = id2;
            }
            else
            {
                throw new RecoverableException($"Invalid GDB packet's thread-id argument: {gdbArgument}");
            }
        }

        public override string ToString()
        {
            return string.Empty
                .AppendIf(ProcessId.HasValue, $"process-id: {IdToString(ProcessId.Value)}, ")
                .Append($"thread-id: {IdToString(ThreadId)}").ToString();
        }

        public int? ProcessId;
        public int ThreadId;

        // All and Any can be passed as a part of a valid argument.
        public const int All = -1;
        public const int Any = 0;

        private static string IdToString(int id)
        {
            switch(id)
            {
                case All:
                    return "all";
                case Any:
                    return "any";
                default:
                    return id.ToString();
            }
        }

        private static bool TryParseId(string s, out int result)
        {
            if(s == "-1")
            {
                result = -1;
                return true;
            }
            else
            {
                // No need to ensure it isn't lower than -1 because negative values aren't allowed with HexNumber.
                return int.TryParse(s, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out result);
            }
        }
    }
}

