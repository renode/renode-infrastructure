using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Utilities;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Core.Structure.Registers
{
    // Reads are prefetched: a read starting at the first byte latches the whole value, and the
    // following bytes are served from that snapshot, so a value changing under the transfer cannot
    // be torn. Writes go through as they arrive, merged with the current value of the register.
    public class MultibyteRegister
    {
        public MultibyteRegister(IPeripheral parent, int widthInBytes, Endianess endianess)
        {
            if(widthInBytes < 1)
            {
                throw new ArgumentException($"A multibyte register has to be at least 1 byte long, but {widthInBytes} was given");
            }
            this.parent = parent;
            this.endianess = endianess;
            WidthInBytes = widthInBytes;
            partIndexByByte = new int?[widthInBytes];
        }

        public byte[] Read(int startByte, int count)
        {
            if(startByte < 0 || count < 0 || startByte + count > WidthInBytes)
            {
                throw new ArgumentException($"Cannot read {count} byte(s) at byte offset {startByte} of a {WidthInBytes}-byte register");
            }

            if(startByte == 0)
            {
                InvalidatePrefetch();
            }

            var result = new byte[count];
            for(var i = 0; i < count; ++i)
            {
                result[i] = GetLatchedByte(ValueByteIndex(startByte + i));
            }
            return result;
        }

        public void Write(int startByte, byte[] bytes)
        {
            if(startByte < 0 || startByte + bytes.Length > WidthInBytes)
            {
                throw new ArgumentException($"Cannot write {bytes.Length} byte(s) at byte offset {startByte} of a {WidthInBytes}-byte register");
            }

            InvalidatePrefetch();

            foreach(var part in parts)
            {
                var mask = 0UL;
                var value = 0UL;
                for(var i = 0; i < bytes.Length; ++i)
                {
                    var index = ValueByteIndex(startByte + i) - part.FirstByte;
                    if(index < 0 || index >= part.WidthInBytes)
                    {
                        continue;
                    }
                    var shift = index * 8;
                    mask |= 0xFFUL << shift;
                    value |= (ulong)bytes[i] << shift;
                }

                if(mask == 0)
                {
                    continue;
                }
                if(mask != part.Mask)
                {
                    // the bytes that were not delivered keep their current value
                    value |= part.ReadUnderlying() & ~mask;
                }
                part.Write(value);
            }
        }

        public void InvalidatePrefetch()
        {
            foreach(var part in parts)
            {
                part.InvalidatePrefetch();
            }
        }

        public void Reset()
        {
            foreach(var part in parts)
            {
                part.Reset();
            }
        }

        public string[,] Dump(bool allowSideEffects = false)
        {
            if(parts.Count == 1)
            {
                return parts[0].Register.Dump(allowSideEffects);
            }

            var fields = new List<DumpedField>();
            foreach(var part in parts)
            {
                var partDump = part.Register.Dump(allowSideEffects);
                for(var row = 1; row < partDump.GetLength(0); ++row)
                {
                    var bits = partDump[row, 1].Split('-').Select(int.Parse).ToArray();
                    fields.Add(
                        new DumpedField
                        {
                            Name = partDump[row, 0],
                            Position = bits.First() + part.FirstByte * 8,
                            Width = bits.Last() - bits.First() + 1,
                            Value = partDump[row, 2],
                            Reliability = partDump[row, 3]
                        });
                }
            }
            fields.Sort((x, y) => x.Position.CompareTo(y.Position));

            var table = new Table().AddRow("Name", "Bits", "Value", "Reliability");
            table.AddRows(fields, x => x.Name, x => $"{x.Position}" + (x.Width == 1 ? "" : $"-{x.Position + x.Width - 1}"),
                          x => x.Value, x => x.Reliability);

            return table.ToArray();
        }

        public MultibyteRegister WithRegister<T>(int firstByte, Action<T> setup, ulong resetValue = 0, bool softResettable = true)
            where T : PeripheralRegister
        {
            if(firstByte < 0 || firstByte >= WidthInBytes)
            {
                throw new ArgumentException($"Byte {firstByte} is outside of a {WidthInBytes}-byte register");
            }

            var register = (T)CreateRegister(typeof(T), parent, resetValue, softResettable, out var read, out var readUnderlying, out var write);
            var registerBits = register.RegisterWidth;
            var widthInBytes = Math.Min(registerBits / 8, WidthInBytes - firstByte);
            var widthInBits = widthInBytes * 8;

            for(var i = 0; i < widthInBytes; ++i)
            {
                if(partIndexByByte[firstByte + i].HasValue)
                {
                    throw new ArgumentException($"Byte {firstByte + i} of this register is already defined");
                }
            }

            if(widthInBits < registerBits)
            {
                register.Reserved(widthInBits, registerBits - widthInBits);
            }
            setup(register);

            for(var i = 0; i < widthInBytes; ++i)
            {
                partIndexByByte[firstByte + i] = parts.Count;
            }
            parts.Add(new Part(register, read, readUnderlying, write, firstByte, widthInBytes));
            return this;
        }

        public int WidthInBytes { get; }

        public int DefinedBytes => parts.Sum(part => part.WidthInBytes);

        private static PeripheralRegister CreateRegister(Type type, IPeripheral parent, ulong resetValue, bool softResettable, out Func<ulong> read, out Func<ulong> readUnderlying, out Action<ulong> write)
        {
            if(type == typeof(ByteRegister))
            {
                var register = new ByteRegister(parent, resetValue, softResettable);
                read = () => register.Read();
                readUnderlying = () => register.Value;
                write = value => register.Write(0, (byte)value);
                return register;
            }
            if(type == typeof(WordRegister))
            {
                var register = new WordRegister(parent, resetValue, softResettable);
                read = () => register.Read();
                readUnderlying = () => register.Value;
                write = value => register.Write(0, (ushort)value);
                return register;
            }
            if(type == typeof(DoubleWordRegister))
            {
                var register = new DoubleWordRegister(parent, resetValue, softResettable);
                read = () => register.Read();
                readUnderlying = () => register.Value;
                write = value => register.Write(0, (uint)value);
                return register;
            }
            if(type == typeof(QuadWordRegister))
            {
                var register = new QuadWordRegister(parent, resetValue, softResettable);
                read = () => register.Read();
                readUnderlying = () => register.Value;
                write = value => register.Write(0, value);
                return register;
            }
            throw new ArgumentException($"{type.Name} cannot be used as a part of a multibyte register");
        }

        private byte GetLatchedByte(int valueByte)
        {
            var partIndex = partIndexByByte[valueByte];
            if(!partIndex.HasValue)
            {
                return 0;
            }
            var part = parts[partIndex.Value];
            return (byte)BitHelper.GetValue(part.ReadPrefetched(), (valueByte - part.FirstByte) * 8, 8);
        }

        private int ValueByteIndex(int wireByte)
        {
            return endianess == Endianess.BigEndian ? WidthInBytes - 1 - wireByte : wireByte;
        }

        // Which part backs each byte of the value, indexed from the least significant one.
        private readonly int?[] partIndexByByte;
        private readonly List<Part> parts = new();
        private readonly IPeripheral parent;
        private readonly Endianess endianess;

        private class Part
        {
            public Part(PeripheralRegister register, Func<ulong> read, Func<ulong> readUnderlying, Action<ulong> write, int firstByte, int widthInBytes)
            {
                Register = register;
                FirstByte = firstByte;
                WidthInBytes = widthInBytes;
                Mask = BitHelper.CalculateQuadWordMask(widthInBytes * 8, 0);
                this.read = read;
                this.readUnderlying = readUnderlying;
                this.write = write;
            }

            public ulong ReadPrefetched()
            {
                if(!prefetched)
                {
                    latchedValue = Read();
                    prefetched = true;
                }
                return latchedValue;
            }

            public void InvalidatePrefetch() => prefetched = false;

            public void Reset()
            {
                Register.Reset();
                InvalidatePrefetch();
            }

            public ulong Read() => read() & Mask;

            public ulong ReadUnderlying() => readUnderlying() & Mask;

            public void Write(ulong value) => write(value & Mask);

            public PeripheralRegister Register { get; }

            public int FirstByte { get; }

            public int WidthInBytes { get; }

            public ulong Mask { get; }

            private ulong latchedValue;
            private bool prefetched;

            private readonly Func<ulong> read;
            private readonly Func<ulong> readUnderlying;
            private readonly Action<ulong> write;
        }

        private struct DumpedField
        {
            public string Name;
            public int Position;
            public int Width;
            public string Value;
            public string Reliability;
        }
    }

    public sealed class MultibyteRegisterCollection : IRegisterCollection
    {
        public MultibyteRegisterCollection(IPeripheral parent)
        {
            this.parent = parent;
        }

        public MultibyteRegister DefineMultibyte(long offset, int lengthInBytes, Endianess endianess = DefaultEndianess)
        {
            if(registers.ContainsKey(offset))
            {
                throw new ArgumentException($"A register is already defined at offset 0x{offset:X}");
            }
            var register = new MultibyteRegister(parent, lengthInBytes, endianess);
            registers.Add(offset, register);
            return register;
        }

        public byte[] Read(long offset) => ReadWithOffset(offset, 0);

        public byte[] Read(long offset, int count) => ReadWithOffset(offset, 0, count);

        public byte[] ReadWithOffset(long offset, int startByte, int? count = null)
        {
            if(!TryReadWithOffset(offset, startByte, count, out var result))
            {
                LogUnhandledRead(offset);
                return Array.Empty<byte>();
            }
            return result;
        }

        public bool TryRead(long offset, out byte[] result) => TryReadWithOffset(offset, 0, null, out result);

        public bool TryRead(long offset, int count, out byte[] result) => TryReadWithOffset(offset, 0, count, out result);

        public bool TryReadWithOffset(long offset, int startByte, int? count, out byte[] result)
        {
            result = null;

            if(beforeReadHooks.TryGetValue(offset, out var beforeReadHook))
            {
                var hookOutput = beforeReadHook(offset);
                if(hookOutput != null)
                {
                    result = hookOutput;
                    return true;
                }
            }

            if(!registers.TryGetValue(offset, out var register))
            {
                return false;
            }

            if(startByte < 0 || startByte + (count ?? 0) > register.WidthInBytes)
            {
                parent.Log(LogLevel.Warning, "Read of {0} byte(s) at byte offset {1} exceeds the {2}-byte width of the register at offset 0x{3:X}, ignoring it",
                    count, startByte, register.WidthInBytes, offset);
                return false;
            }

            result = register.Read(startByte, count ?? register.WidthInBytes - startByte);

            if(afterReadHooks.TryGetValue(offset, out var afterReadHook))
            {
                result = afterReadHook(offset, result) ?? result;
            }
            return true;
        }

        public void Write(long offset, byte[] bytes)
        {
            if(registers.TryGetValue(offset, out var register) && bytes.Length != register.WidthInBytes)
            {
                parent.Log(LogLevel.Warning, "Register at offset 0x{0:X} is {1} bytes wide, but the write supplied {2}, ignoring it", offset, register.WidthInBytes, bytes.Length);
                return;
            }
            WriteWithOffset(offset, 0, bytes);
        }

        public bool TryWrite(long offset, byte[] bytes)
        {
            if(!registers.TryGetValue(offset, out var register) || bytes.Length != register.WidthInBytes)
            {
                return false;
            }
            return TryWriteWithOffset(offset, 0, bytes);
        }

        public void WriteWithOffset(long offset, int startByte, byte[] bytes)
        {
            if(!TryWriteWithOffset(offset, startByte, bytes))
            {
                LogUnhandledWrite(offset, bytes);
            }
        }

        public bool TryWriteWithOffset(long offset, int startByte, byte[] bytes)
        {
            if(!registers.TryGetValue(offset, out var register))
            {
                return false;
            }

            if(startByte < 0 || startByte + bytes.Length > register.WidthInBytes)
            {
                parent.Log(LogLevel.Warning, "Write to register 0x{0:X} at byte offset {1} ({2} byte(s)) exceeds its {3}-byte width, ignoring it",
                    offset, startByte, bytes.Length, register.WidthInBytes);
                return false;
            }

            if(beforeWriteHooks.TryGetValue(offset, out var beforeWriteHook))
            {
                bytes = beforeWriteHook(offset, bytes) ?? bytes;
            }

            register.Write(startByte, bytes);

            if(afterWriteHooks.TryGetValue(offset, out var afterWriteHook))
            {
                afterWriteHook(offset, bytes);
            }
            return true;
        }

        public void Reset()
        {
            foreach(var register in registers.Values)
            {
                register.Reset();
            }
        }

        public string[,] DumpRegister(long offset, bool allowSideEffects = false)
        {
            if(registers.TryGetValue(offset, out var register))
            {
                return register.Dump(allowSideEffects);
            }
            return null;
        }

        public void AddBeforeReadHook(long offset, Func<long, byte[]> hook)
        {
            if(beforeReadHooks.ContainsKey(offset))
            {
                throw new RecoverableException($"Before-read hook for 0x{offset:X} is already registered");
            }
            beforeReadHooks.Add(offset, hook);
        }

        public void AddAfterReadHook(long offset, Func<long, byte[], byte[]> hook)
        {
            if(afterReadHooks.ContainsKey(offset))
            {
                throw new RecoverableException($"After-read hook for 0x{offset:X} is already registered");
            }
            afterReadHooks.Add(offset, hook);
        }

        public void AddBeforeWriteHook(long offset, Func<long, byte[], byte[]> hook)
        {
            if(beforeWriteHooks.ContainsKey(offset))
            {
                throw new RecoverableException($"Before-write hook for 0x{offset:X} is already registered");
            }
            beforeWriteHooks.Add(offset, hook);
        }

        public void AddAfterWriteHook(long offset, Action<long, byte[]> hook)
        {
            if(afterWriteHooks.ContainsKey(offset))
            {
                throw new RecoverableException($"After-write hook for 0x{offset:X} is already registered");
            }
            afterWriteHooks.Add(offset, hook);
        }

        public void RemoveBeforeReadHook(long offset) => beforeReadHooks.Remove(offset);

        public void RemoveAfterReadHook(long offset) => afterReadHooks.Remove(offset);

        public void RemoveBeforeWriteHook(long offset) => beforeWriteHooks.Remove(offset);

        public void RemoveAfterWriteHook(long offset) => afterWriteHooks.Remove(offset);

        public const Endianess DefaultEndianess = Endianess.BigEndian;

        private void LogUnhandledRead(long offset)
        {
            parent.Log(LogLevel.Warning, "Unhandled read from offset 0x{0:X}.", offset);
        }

        private void LogUnhandledWrite(long offset, byte[] bytes)
        {
            parent.Log(LogLevel.Warning, "Unhandled write to offset 0x{0:X}, value 0x{1}.", offset, Misc.PrettyPrintCollectionHex(bytes));
        }

        private readonly IPeripheral parent;
        private readonly Dictionary<long, MultibyteRegister> registers = new();
        private readonly Dictionary<long, Func<long, byte[]>> beforeReadHooks = new();
        private readonly Dictionary<long, Func<long, byte[], byte[]>> afterReadHooks = new();
        private readonly Dictionary<long, Func<long, byte[], byte[]>> beforeWriteHooks = new();
        private readonly Dictionary<long, Action<long, byte[]>> afterWriteHooks = new();
    }

    public static class MultibyteRegisterCollectionExtensions
    {
        public static MultibyteRegister DefineMultibyte(this Enum o, MultibyteRegisterCollection c, int length, Endianess endianess = MultibyteRegisterCollection.DefaultEndianess)
        {
            return c.DefineMultibyte(Convert.ToInt64(o), length, endianess);
        }

        public static MultibyteRegister DefineMultibyte(this Enum o, IProvidesRegisterCollection<MultibyteRegisterCollection> p, int length, Endianess endianess = MultibyteRegisterCollection.DefaultEndianess)
        {
            return o.DefineMultibyte(p.RegistersCollection, length, endianess);
        }
    }
}
