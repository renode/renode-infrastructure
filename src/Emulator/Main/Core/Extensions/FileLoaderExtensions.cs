//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.CPU;
using System.IO;
using System.Linq;
using Antmicro.Renode.Exceptions;
using System.Globalization;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.Extensions
{
    public static class FileLoaderExtensions
    {
        public static void LoadBinary(this ICanLoadFiles loader, ReadFilePath fileName, ulong loadPoint, ICPU cpu = null)
        {
            const int bufferSize = 100 * 1024;
            List<FileChunk> chunks = new List<FileChunk>();

            try
            {
                using(var reader = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
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

            loader.LoadFileChunks(fileName, chunks, cpu);
        }

        public static void LoadHEX(this ICanLoadFiles loader, ReadFilePath fileName, IInitableCPU cpu = null)
        {
            string line;
            int lineNum = 1;
            ulong extendedTargetAddress = 0;
            bool endOfFileReached = false;
            List<FileChunk> chunks = new List<FileChunk>();

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
                            || !byte.TryParse(line.Substring(7, 2), NumberStyles.HexNumber,CultureInfo.InvariantCulture, out var type))
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
                                var targetAddr = (extendedTargetAddress << 16) | address;
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
                                    throw new RecoverableException($"Parsing error at line #{lineNum}: {line}. Could not parse address");
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

            loader.LoadFileChunks(fileName, chunks, cpu);
        }

        private enum HexRecordType
        {
            Data = 0,
            EndOfFile = 1,
            ExtendedSegmentAddress = 2,
            StartSegmentAddress = 3,
            ExtendedLinearAddress = 4,
            StartLinearAddress = 5
        }
    }
}
