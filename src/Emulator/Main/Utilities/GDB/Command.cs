//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;
using ELFSharp.ELF;

namespace Antmicro.Renode.Utilities.GDB
{
    public abstract class Command : IAutoLoadType
    {
        public static IEnumerable<PacketData> Execute(Command command, Packet packet)
        {
            var executeMethod = GetExecutingMethod(command, packet);
            var mnemonic = packet.Data.Mnemonic;
            var parsingContext = new ParsingContext(packet, mnemonic.Length);
            var parameters = executeMethod.GetParameters().Select(x => HandleArgumentNotResolved(parsingContext, x)).ToArray();

            var result = executeMethod.Invoke(command, parameters);
            if(result == null)
            {
                return Enumerable.Empty<PacketData>();
            }
            else if(result is IEnumerable<PacketData> resultPackets)
            {
                return resultPackets;
            }
            else if(result is PacketData resultPacket)
            {
                return new [] { resultPacket };
            }
            throw new ArgumentOutOfRangeException("result",
                $"Result of GDB command method was of invalid type '{result.GetType().FullName}'");
        }

        public static MethodInfo[] GetExecutingMethods(Type t)
        {
            if(t.GetConstructor(new[] { typeof(CommandsManager) }) == null)
            {
                return new MethodInfo[0];
            }

            return t.GetMethods().Where(x =>
                x.GetCustomAttribute<ExecuteAttribute>() != null &&
                x.GetParameters().All(y => y.GetCustomAttribute<ArgumentAttribute>() != null)).ToArray();
        }

        protected Command(CommandsManager manager)
        {
            this.manager = manager;
            executingMethods = new Dictionary<string, MethodInfo>();
        }

        protected bool TryTranslateAddress(ulong address, out ulong translatedAddress, bool write)
        {
            var cpu = manager.Cpu as ICPUWithMMU;
            if(cpu == null)
            {
                translatedAddress = address;
                return true;
            }

            var errorValue = ulong.MaxValue;
            translatedAddress = errorValue;

            if(write)
            {
                if(!cpu.TryTranslateAddress(address, MpuAccess.Write, out translatedAddress))
                {
                    Logger.LogAs(this, LogLevel.Warning, "Translation address failed for write access type!");
                    return false;
                }
            }
            else
            {
                if(!cpu.TryTranslateAddress(address, MpuAccess.InstructionFetch, out var fetchAddress))
                {
                    Logger.LogAs(this, LogLevel.Warning, "Translation address failed for fetch access type!");
                    return false;
                }
                if(!cpu.TryTranslateAddress(address, MpuAccess.Read, out var readAddress))
                {
                    Logger.LogAs(this, LogLevel.Warning, "Translation address failed for read access type!");
                    return false;
                }

                if(fetchAddress == errorValue && readAddress == errorValue)
                {
                    Logger.LogAs(this, LogLevel.Warning, "Translation address failed for both read and instruction fetch access types!");
                }
                else if(fetchAddress == errorValue || readAddress == errorValue)
                {
                    var fetchFailed = fetchAddress == errorValue;
                    var failed = fetchFailed ? "instruction fetch" : "read";
                    var fallback = fetchFailed ? "read" : "instruction fetch";
                    Logger.LogAs(this, LogLevel.Debug, "Translation address failed for {0} access type! Returned translation for {1} access type.", failed, fallback);

                    translatedAddress = fetchFailed ? readAddress : fetchAddress;
                }
                else if(fetchAddress != readAddress)
                {
                    Logger.LogAs(this, LogLevel.Warning, "Translation address missmatch for read and instruction fetch access types!");
                }
                else
                {
                    translatedAddress = fetchAddress;
                }
            }

            return translatedAddress != errorValue;
        }

        protected IEnumerable<MemoryFragment> GetTranslatedAccesses(ulong address, ulong length, bool write)
        {
            var cpu = manager.Cpu as ICPUWithMMU;
            if(cpu == null)
            {
                // NOTE: If given CPU doesn't support MMU, we just assume 1:1 translation
                return new List<MemoryFragment>()
                {
                    new MemoryFragment(address, length)
                };
            }

            var pageSize = (ulong)cpu.PageSize;
            var accesses = new List<MemoryFragment>();
            var firstLength = Math.Min(length, pageSize - address % pageSize);

            if(!TryAddTranslatedMemoryFragment(ref accesses, address, firstLength, write))
            {
                return null;
            }
            address += firstLength;
            length -= firstLength;

            while(length > 0)
            {
                var translateLength = Math.Min(pageSize, length);
                if(!TryAddTranslatedMemoryFragment(ref accesses, address, translateLength, write))
                {
                    return null;
                }
                length -= translateLength;
                address += translateLength;
            }

            return accesses;
        }

        protected bool TryAddTranslatedMemoryFragment(ref List<MemoryFragment> accesses, ulong address, ulong length, bool write)
        {
            if(length > 0)
            {
                if(!TryTranslateAddress(address, out var translatedAddress, write))
                {
                    Logger.LogAs(this, LogLevel.Warning, "Could not translate address 0x{0:X} to a valid physical address.", address);
                    return false;
                }

                accesses.Add(new MemoryFragment(translatedAddress, length));
            }
            return true;
        }

        protected void ExpandRegisterValue(ref StringBuilder data, int start, int end, int register)
        {
            // register may have been reported with bigger Width, fill with zeros up to reported size
            var isLittleEndian = manager.Cpu.Endianness == Endianess.LittleEndian;
            var width = (end - start) * 4;
            var reportedRegisters = manager.GetCompiledRegisters(register);
            if(reportedRegisters.Any() && reportedRegisters.First().Size > width)
            {
                data.Insert(isLittleEndian ? end : start, "00", ((int)reportedRegisters.First().Size - width) / 8);
            }
        }

        protected readonly CommandsManager manager;

        private static MethodInfo GetExecutingMethod(Command command, Packet packet)
        {
            var mnemonic = packet.Data.Mnemonic;
            if(mnemonic == null)
            {
                return null;
            }
            if(executingMethods.TryGetValue(mnemonic, out var output))
            {
                return output;
            }

            var interestingMethods = GetExecutingMethods(command.GetType());
            if(!interestingMethods.Any())
            {
                return null;
            }

            // May return null if the given mnemonic is not supported. We should still put it in the executeMethods to avoid repeating the lookup
            var method = interestingMethods.FirstOrDefault(x => x.GetCustomAttribute<ExecuteAttribute>().Mnemonic == mnemonic);

            executingMethods.Add(mnemonic, method);
            return method;
        }

        private static object HandleArgumentNotResolved(ParsingContext context, ParameterInfo parameterInfo)
        {
            var attribute = parameterInfo.GetCustomAttribute<ArgumentAttribute>();
            if(attribute == null)
            {
                throw new ArgumentException(string.Format("Could not resolve argument: {0}", parameterInfo.Name));
            }

            var startPosition = context.CurrentPosition;

            // we do not support multiple sets of operation+coreId parameters
            if(startPosition > context.Packet.Data.DataAsString.Length)
            {
                return parameterInfo.DefaultValue;
            }

            var separatorPosition = attribute.Separator == '\0' ? -1 : context.Packet.Data.DataAsString.IndexOf(attribute.Separator, startPosition);
            var length = (separatorPosition == -1 ? context.Packet.Data.DataAsString.Length : separatorPosition) - startPosition;
            var valueToParse = context.Packet.Data.DataAsString.Substring(startPosition, length);
            context.CurrentPosition += length + 1;

            switch(attribute.Encoding)
            {
                case ArgumentAttribute.ArgumentEncoding.HexNumber:
                    return Parse(parameterInfo.ParameterType, valueToParse, NumberStyles.HexNumber);
                case ArgumentAttribute.ArgumentEncoding.DecimalNumber:
                    return Parse(parameterInfo.ParameterType, valueToParse);
                case ArgumentAttribute.ArgumentEncoding.BinaryBytes:
                    return context.Packet.Data.DataAsBinary.Skip(startPosition).ToArray();
                case ArgumentAttribute.ArgumentEncoding.HexBytesString:
                    return valueToParse.Split(2).Select(x => byte.Parse(x, NumberStyles.HexNumber)).ToArray();
                case ArgumentAttribute.ArgumentEncoding.HexString:
                    return Encoding.UTF8.GetString(valueToParse.Split(2).Select(x => byte.Parse(x, NumberStyles.HexNumber)).ToArray());
                case ArgumentAttribute.ArgumentEncoding.String:
                    return valueToParse;
                case ArgumentAttribute.ArgumentEncoding.ThreadId:
                    return new PacketThreadId(valueToParse);
                default:
                    throw new ArgumentException(string.Format("Unsupported argument type: {0}", parameterInfo.ParameterType.Name));
            }
        }

        private static object Parse(Type type, string input, NumberStyles style = NumberStyles.Integer)
        {
            if(type.IsEnum)
            {
                return Parse(type.GetEnumUnderlyingType(), input, style);
            }
            if(type == typeof(int))
            {
                return int.Parse(input, style);
            }
            if(type == typeof(uint))
            {
                return uint.Parse(input, style);
            }
            if(type == typeof(ulong))
            {
                return ulong.Parse(input, style);
            }

            throw new ArgumentException(string.Format("Unsupported type for parsing: {0}", type.Name));
        }

        private static Dictionary<string, MethodInfo> executingMethods;

        private class ParsingContext
        {
            public ParsingContext(Packet packet, int currentPosition)
            {
                Packet = packet;
                CurrentPosition = currentPosition;
            }

            public int CurrentPosition { get; set; }
            public Packet Packet { get; set; }
        }
    }

    public struct MemoryFragment
    {
        public MemoryFragment(ulong address, ulong length)
        {
            Address = address;
            Length = length;
        }

        public ulong Address { get; }
        public ulong Length { get; }
    }
}

