//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;
using ELFSharp.ELF.Segments;

namespace Antmicro.Renode.Core.Extensions
{
    public static class FileLoaderExtensions
    {
        public static void LoadBinary(this ICanLoadFiles loader, ReadFilePath fileName, ulong loadPoint, ICPU cpu = null, long offset = 0)
        {
            const int bufferSize = 100 * 1024;
            List<FileChunk> chunks = new List<FileChunk>();

            Logger.LogAs(loader, LogLevel.Debug, "Loading binary file {0}.", fileName);
            try
            {
                using(var reader = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    reader.Seek(offset, SeekOrigin.Current);

                    var buffer = new byte[bufferSize];
                    var bytesCount = reader.Read(buffer, 0, buffer.Length);
                    var addr = loadPoint;

                    while(bytesCount > 0)
                    {
                        chunks.Add(new FileChunk() { Data = buffer.Take(bytesCount), OffsetToLoad = addr });
                        addr += (ulong)bytesCount;
                        buffer = new byte[bufferSize];
                        bytesCount = reader.Read(buffer, 0, buffer.Length);
                    }
                }
            }
            catch(IOException e)
            {
                throw new RecoverableException(string.Format("Exception while loading file {0}: {1}", fileName, e.Message));
            }

            chunks = SortAndJoinConsecutiveFileChunks(chunks);
            loader.LoadFileChunks(fileName, chunks, cpu);
        }

        public static void LoadHEX(this ICanLoadFiles loader, ReadFilePath fileName, IInitableCPU cpu = null)
        {
            string line;
            int lineNum = 1;
            ulong extendedTargetAddress = 0;
            ulong extendedSegmentAddress = 0;
            bool endOfFileReached = false;
            List<FileChunk> chunks = new List<FileChunk>();

            Logger.LogAs(loader, LogLevel.Debug, "Loading HEX file {0}.", fileName);
            try
            {
                using(var file = new System.IO.StreamReader(fileName))
                {
                    while((line = file.ReadLine()) != null)
                    {
                        if(endOfFileReached)
                        {
                            throw new RecoverableException($"Unexpected data after the end of file marker at line #{lineNum}");
                        }

                        if(line.Length < 11)
                        {
                            throw new RecoverableException($"Line is too short error at line #{lineNum}.");
                        }
                        if(line[0] != ':'
                            || !int.TryParse(line.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var length)
                            || !ulong.TryParse(line.Substring(3, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address)
                            || !byte.TryParse(line.Substring(7, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var type))
                        {
                            throw new RecoverableException($"Parsing error at line #{lineNum}: {line}. Could not parse header");
                        }

                        // this does not include the final CRC
                        if(line.Length < 9 + length * 2)
                        {
                            throw new RecoverableException($"Parsing error at line #{lineNum}: {line}. Line too short");
                        }

                        switch((HexRecordType)type)
                        {
                            case HexRecordType.Data:
                                var targetAddr = (extendedTargetAddress << 16) | (extendedSegmentAddress << 4) | address;
                                var pos = 9;
                                var buffer = new byte[length];
                                for(var i = 0; i < length; i++, pos += 2)
                                {
                                    if(!byte.TryParse(line.Substring(pos, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out buffer[i]))
                                    {
                                        throw new RecoverableException($"Parsing error at line #{lineNum}: {line}. Could not parse bytes");
                                    }
                                }
                                chunks.Add(new FileChunk() { Data = buffer, OffsetToLoad = targetAddr });
                                break;

                            case HexRecordType.ExtendedLinearAddress:
                                if(!ulong.TryParse(line.Substring(9, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out extendedTargetAddress))
                                {
                                    throw new RecoverableException($"Parsing error at line #{lineNum}: {line}. Could not parse extended linear address");
                                }
                                break;

                            case HexRecordType.StartLinearAddress:
                                if(!ulong.TryParse(line.Substring(9, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var startingAddress))
                                {
                                    throw new RecoverableException($"Parsing error at line #{lineNum}: {line}. Could not parse starting address");
                                }

                                if(cpu != null)
                                {
                                    cpu.PC = startingAddress;
                                }
                                break;

                            case HexRecordType.ExtendedSegmentAddress:
                                if(!ulong.TryParse(line.Substring(9, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out extendedSegmentAddress))
                                {
                                    throw new RecoverableException($"Parsing error at line #{lineNum}: {line}. Could not parse extended segment address");
                                }
                                break;

                            case HexRecordType.StartSegmentAddress:
                                if(!ulong.TryParse(line.Substring(9, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var startingSegment)
                                   || !ulong.TryParse(line.Substring(13, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var startingSegmentAddress))
                                {
                                    throw new RecoverableException($"Parsing error at line #{lineNum}: {line}. Could not parse starting segment/address");
                                }

                                if(cpu != null)
                                {
                                    cpu.PC = (startingSegment << 4) + startingSegmentAddress;
                                }
                                break;

                            case HexRecordType.EndOfFile:
                                endOfFileReached = true;
                                break;

                            default:
                                break;
                        }
                        lineNum++;
                    }
                }
            }
            catch(IOException e)
            {
                throw new RecoverableException($"Exception while loading file {fileName}: {(e.Message)}");
            }

            chunks = SortAndJoinConsecutiveFileChunks(chunks);
            loader.LoadFileChunks(fileName, chunks, cpu);
        }

        public static void LoadSRecord(this ICanLoadFiles loader, ReadFilePath fileName, IInitableCPU cpu = null)
        {
            SRecPurpose type;
            ulong address;
            string line = "";
            int lineNum = 1;
            bool endOfFileReached = false;
            List<FileChunk> chunks = new List<FileChunk>();
            Range? currentSegmentInfo = null;

            Logger.LogAs(loader, LogLevel.Debug, "Loading S-record file {0}.", fileName);
            try
            {
                using(var file = new System.IO.StreamReader(fileName))
                {
                    while((line = file.ReadLine()) != null)
                    {
                        if(endOfFileReached)
                        {
                            throw new RecoverableException($"Unexpected data after the end of file marker at line #{lineNum}");
                        }

                        // an S-record consist of:
                        // * 'S' character followed by a digit character 0-9 denoting type of the record,
                        // * hexstring byte value of bytes count, e.i. number of bytes that follow this byte,
                        // * hexstring bytes representing address, number of them depends on the record type,
                        // * hexstring bytes representing data, number of them depends on bytes count,
                        // * hexstring byte value of checksum,
                        // records are seperated by end of line that can differ between operating systems.

                        // ensure that line contains bytes count
                        if(line.Length < SRecAddressStart)
                        {
                            throw new RecoverableException($"Line is too short error at line #{lineNum}:\n\"{line}\"");
                        }

                        if(line[SRecStartOfRecordIndex] != SRecStartOfRecord)
                        {
                            throw new RecoverableException($"Invalid Start-of-Record at line #{lineNum}:\n\"{line}\"");
                        }

                        // number of address, data and checksum bytes
                        var bytesCount = Convert.ToUInt16(line.Substring(SRecBytesCountStart, SRecBytesCountLength), 16);

                        if(line.Length != SRecAddressStart + bytesCount * 2)
                        {
                            throw new RecoverableException($"Line length does not match bytes count error at line #{lineNum}:\n\"{line}\"");
                        }

                        var addressLength = 4;
                        switch((SRecType)line[SRecTypeIndex])
                        {
                            case SRecType.Header:
                                type = SRecPurpose.Header;
                                break;
                            case SRecType.Data16BitAddress:
                                type = SRecPurpose.Data;
                                break;
                            case SRecType.Data24BitAddress:
                                addressLength = 6;
                                type = SRecPurpose.Data;
                                break;
                            case SRecType.Data32BitAddress:
                                addressLength = 8;
                                type = SRecPurpose.Data;
                                break;
                            case SRecType.Reserved:
                                throw new RecoverableException($"Reserved record type at line #{lineNum}:\n\"{line}\"");
                            case SRecType.Count16Bit:
                                type = SRecPurpose.Count;
                                break;
                            case SRecType.Count24Bit:
                                addressLength = 6;
                                type = SRecPurpose.Count;
                                break;
                            case SRecType.Termination32BitAddress:
                                addressLength = 8;
                                type = SRecPurpose.Termination;
                                break;
                            case SRecType.Termination24BitAddress:
                                addressLength = 6;
                                type = SRecPurpose.Termination;
                                break;
                            case SRecType.Termination16BitAddress:
                                type = SRecPurpose.Termination;
                                break;
                            default:
                                throw new RecoverableException($"Invalid record type at line #{lineNum}:\n\"{line}\"");
                        }

                        // bytes count needs to allow for at least address and checksum bytes
                        if(bytesCount * 2 < addressLength + SRecChecksumLength)
                        {
                            throw new RecoverableException($"Bytes count is too small error at line #{lineNum}:\n\"{line}\"");
                        }

                        var addressString = line.Substring(SRecAddressStart, addressLength);
                        address = Convert.ToUInt32(addressString, 16);

                        var bufferLength = bytesCount * 2 + SRecBytesCountLength - SRecChecksumLength;
                        var checksumStart = SRecAddressStart + bytesCount * 2 - SRecChecksumLength;

                        var buffer = Misc.HexStringToByteArray(line.Substring(SRecBytesCountStart, bufferLength));
                        var checksum = Convert.ToByte(line.Substring(checksumStart, SRecChecksumLength), 16);

                        // checksum is 0xFF minus a sum of bytes count, address and data bytes
                        var calculatedChecksum = 0xFF - buffer.Aggregate((byte)0x0, (a, b) => (byte)(a + b));
                        if(calculatedChecksum != checksum)
                        {
                            throw new RecoverableException($"Checksum error (calculated: 0x{calculatedChecksum:X02}, given: 0x{checksum:X02}) at line #{lineNum}:\n\"{line}\"");
                        }

                        var data = buffer.Skip((addressLength + SRecBytesCountLength) / 2);
                        var dataLength = (ulong)(bytesCount - (addressLength + SRecChecksumLength) / 2);

                        switch(type)
                        {
                            case SRecPurpose.Header:
                                if(address != 0)
                                {
                                    throw new RecoverableException($"Invalid Header record at line #{lineNum}:\n\"{line}\"");
                                }
                                break;
                            case SRecPurpose.Data:
                                if(!currentSegmentInfo.HasValue)
                                {
                                    currentSegmentInfo = address.By(dataLength);
                                }
                                else if(currentSegmentInfo.Value.EndAddress + 1 == address)
                                {
                                    currentSegmentInfo = currentSegmentInfo.Value.StartAddress.By(currentSegmentInfo.Value.Size + dataLength);
                                }
                                else
                                {
                                    currentSegmentInfo = address.By(dataLength);
                                }
                                chunks.Add(new FileChunk() { Data = data.ToArray(), OffsetToLoad = address });
                                break;
                            case SRecPurpose.Count:
                                if(dataLength != 0)
                                {
                                    throw new RecoverableException($"Unexpected data in a count record error at line #{lineNum}:\n\"{line}\"");
                                }
                                if(chunks.Count != (int)address)
                                {
                                    throw new RecoverableException($"Data record count mismatch error (calculated: {chunks.Count}, given: {address}) at line #{lineNum}:\n\"{line}\"");
                                }
                                break;
                            case SRecPurpose.Termination:
                                if(dataLength != 0)
                                {
                                    throw new RecoverableException($"Unexpected data in a termination record error at line #{lineNum}:\n\"{line}\"");
                                }
                                if(cpu != null)
                                {
                                    cpu.Log(LogLevel.Info, "Setting PC value to 0x{0:X}", address);
                                    cpu.PC = address;
                                }
                                else if(loader is IBusController bus)
                                {
                                    foreach(var core in bus.GetCPUs())
                                    {
                                        cpu.Log(LogLevel.Info, "Setting PC value to 0x{0:X}", address);
                                        core.PC = address;
                                    }
                                }
                                else
                                {
                                    Logger.Log(LogLevel.Warning, "S-record loader: Found start addres: 0x{0:X}, but no cpu is selected to set it for", address);
                                }
                                break;
                            default:
                                throw new Exception("Unreachable");
                        }

                        lineNum++;
                    }
                }
            }
            catch(IOException e)
            {
                throw new RecoverableException($"Exception while loading file {fileName}: {(e.Message)}");
            }
            catch(FormatException e)
            {
                throw new RecoverableException($"Exception while parsing line #{lineNum}:\n\"{line}\"", e);
            }

            if(lineNum == 1)
            {
                Logger.Log(LogLevel.Warning, "S-record loader: Attempted to load empty file {0}", fileName);
                return;
            }

            chunks = SortAndJoinConsecutiveFileChunks(chunks);
            loader.LoadFileChunks(fileName, chunks, cpu);
        }

        // Name of the last parameter is kept as 'cpu' for backward compatibility.
        public static void LoadELF(this IBusController loader, ReadFilePath fileName, bool useVirtualAddress = false, bool allowLoadsOnlyToMemory = true, ICluster<IInitableCPU> cpu = null)
        {
            if(!loader.Machine.IsPaused)
            {
                throw new RecoverableException("Cannot load ELF on an unpaused machine.");
            }
            Logger.LogAs(loader, LogLevel.Debug, "Loading ELF file {0}.", fileName);

            using(var elf = ELFUtils.LoadELF(fileName))
            {
                var segmentsToLoad = elf.Segments.Where(x => x.Type == SegmentType.Load);
                if(!segmentsToLoad.Any())
                {
                    throw new RecoverableException($"ELF '{fileName}' has no loadable segments.");
                }

                List<FileChunk> chunks = new List<FileChunk>();
                foreach(var s in segmentsToLoad)
                {
                    var contents = s.GetContents();
                    var loadAddress = useVirtualAddress ? s.GetSegmentAddress() : s.GetSegmentPhysicalAddress();
                    chunks.Add(new FileChunk() { Data = contents, OffsetToLoad = loadAddress});
                }

                // If cluster is passed as parameter, we setup ELF locally so it only affects cpus in cluster.
                // Otherwise, we initialize all cpus on sysbus and load symbols to global lookup.
                if(cpu != null)
                {
                    foreach(var initableCpu in cpu.Clustered)
                    {
                        loader.LoadFileChunks(fileName, chunks, initableCpu);
                        loader.LoadSymbolsFrom(elf, useVirtualAddress, context: initableCpu);
                        initableCpu.InitFromElf(elf);
                    }
                }
                else
                {
                    loader.LoadFileChunks(fileName, chunks, null);
                    loader.LoadSymbolsFrom(elf, useVirtualAddress);
                    foreach(var initableCpu in loader.GetCPUs().OfType<IInitableCPU>())
                    {
                        initableCpu.InitFromElf(elf);
                    }
                }
            }
        }

        private static List<FileChunk> SortAndJoinConsecutiveFileChunks(List<FileChunk> chunks)
        {
            if(chunks.Count == 0)
            {
                return chunks;
            }

            chunks.Sort((lhs, rhs) => lhs.OffsetToLoad.CompareTo(rhs.OffsetToLoad));

            List<FileChunk> joinedChunks = new List<FileChunk>();
            var nextOffset = chunks[0].OffsetToLoad;
            var firstChunkIdx = 0;

            for(var chunkIdx = 0; chunkIdx < chunks.Count; ++chunkIdx)
            {
                var chunk = chunks[chunkIdx];
                if(chunk.OffsetToLoad != nextOffset)
                {
                    joinedChunks.Add(JoinFileChunks(chunks.GetRange(firstChunkIdx, chunkIdx - firstChunkIdx)));
                    firstChunkIdx = chunkIdx;
                }
                var chunkSize = chunk.Data.Count();
                nextOffset = chunk.OffsetToLoad + (ulong)chunkSize;
            }
            joinedChunks.Add(JoinFileChunks(chunks.GetRange(firstChunkIdx, chunks.Count - firstChunkIdx)));

            return joinedChunks;
        }

        private static FileChunk JoinFileChunks(List<FileChunk> chunks)
        {
            var loadOffset = chunks[0].OffsetToLoad;
            var data = chunks.SelectMany(chunk => chunk.Data);
            return new FileChunk() { Data = data, OffsetToLoad = loadOffset };
        }

        private const char SRecStartOfRecord = 'S';
        private const int SRecStartOfRecordIndex = 0;
        private const int SRecTypeIndex = 1;
        private const int SRecBytesCountStart = 2;
        private const int SRecBytesCountLength = 2;
        private const int SRecAddressStart = 4;
        private const int SRecChecksumLength = 2;

        private enum HexRecordType
        {
            Data = 0,
            EndOfFile = 1,
            ExtendedSegmentAddress = 2,
            StartSegmentAddress = 3,
            ExtendedLinearAddress = 4,
            StartLinearAddress = 5
        }

        private enum SRecPurpose
        {
            Header,
            Data,
            Count,
            Termination,
        }

        private enum SRecType : byte
        {
            Header = (byte)'0', // S0
            Data16BitAddress, // S1
            Data24BitAddress, // S2
            Data32BitAddress, // S3
            Reserved, // S4
            Count16Bit, // S5
            Count24Bit, // S6
            Termination32BitAddress, // S7
            Termination24BitAddress, // S8
            Termination16BitAddress, // S
        }
    }
}
