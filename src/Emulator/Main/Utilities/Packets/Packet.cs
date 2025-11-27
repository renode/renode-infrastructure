//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Utilities.Packets
{
    public class Packet
    {
        public static int CalculateLength<T>()
        {
            return CalculateLengthInner(typeof(T));
        }

        public static int CalculateBitLength<T>()
        {
            return CalculateBitLengthInner(typeof(T));
        }

        public static int CalculateOffset<T>(string fieldName)
        {
            return CalculateOffsetInner(typeof(T), fieldName);
        }

        public static T Decode<T>(IList<byte> data, int dataOffset = 0)
        {
            if(!TryDecode<T>(data, out var result, dataOffset))
            {
                throw new ArgumentException($"Could not decode the packet of type {typeof(T)} due to insufficient data. Required {Packet.CalculateLength<T>()} bytes, but received {(data.Count - dataOffset)}");
            }
            return result;
        }

        public static T DecodeSubclass<T>(IList<byte> data, Func<IList<byte>, Type> typeSelector, int dataOffset = 0)
        {
            if(!TryDecodeSubclass<T>(data, typeSelector, out var result, dataOffset))
            {
                var t = result.GetType();
                throw new ArgumentException($"Could not decode the packet of type {t} due to insufficient data. Required {Packet.CalculateLengthInner(t)} bytes, but received {(data.Count - dataOffset)}");
            }
            return result;
        }

        public static bool TryDecode<T>(IList<byte> data, out T result, int dataOffset = 0)
        {
            var success = TryDecode(typeof(T), data, out var tryResult, dataOffset);
            result = (T)tryResult;
            return success;
        }

        public static bool TryDecodeSubclass<T>(IList<byte> data, Func<IList<byte>, Type> typeSelector, out T result, int dataOffset = 0)
        {
            var type = typeSelector(data);
            if(type == null || !(typeof(T).IsAssignableFrom(type)))
            {
                throw new ArgumentException($"Could not decode packet: subtype selector did not return a subtype of {typeof(T)}");
            }
            var success = TryDecode(type, data, out var resultObject, dataOffset);
            result = (T)resultObject;
            return success;
        }

        public static dynamic DecodeDynamic<T>(byte[] data, int dataOffset = 0) where T : class
        {
            if(!typeof(T).IsInterface)
            {
                throw new ArgumentException("This can be called on interfaces only");
            }

            var result = new DynamicPropertiesObject();

            var fieldsAndProperties = GetFieldsAndProperties(typeof(T));

            var offset = dataOffset;
            foreach(var field in fieldsAndProperties)
            {
                if(field.ByteOffset.HasValue)
                {
                    offset = dataOffset + field.ByteOffset.Value;
                }

                if(field.ElementType == typeof(uint))
                {
                    var lOffset = offset;
                    result.ProvideProperty(field.ElementName, getter: () => (uint)((data[lOffset] << 24) | (data[lOffset + 1] << 16) | (data[lOffset + 2] << 8) | (data[lOffset + 3])));

                    offset += 4;
                }
                else if(field.ElementType == typeof(ushort))
                {
                    var lOffset = offset;
                    result.ProvideProperty(field.ElementName, getter: () => (ushort)((data[lOffset] << 16) | (data[lOffset + 1])));

                    offset += 2;
                }
                else
                {
                    throw new ArgumentException($"Unsupported field type: {typeof(T).Name}");
                }
            }

            return (dynamic)result;
        }

        public static byte[] Encode<T>(T packet)
        {
            var size = CalculateLength<T>();
            var result = new byte[size];
            if(size == 0)
            {
                return result;
            }

            EncodeInner(packet.GetType(), packet, result, 0);

            return result;
        }

        private static bool TryDecode(Type t, IList<byte> data, out object result, int dataOffset = 0)
        {
            var offset = dataOffset;
            if(offset < 0)
            {
                throw new ArgumentException("Data offset cannot be less than zero", "dataOffset");
            }

            result = Activator.CreateInstance(t);

            var fieldsAndProperties = GetFieldsAndProperties(t);

            foreach(var field in fieldsAndProperties)
            {
                var type = field.ElementType;
                if(type.IsEnum)
                {
                    type = type.GetEnumUnderlyingType();
                }

                if(field.ByteOffset.HasValue)
                {
                    offset = dataOffset + field.ByteOffset.Value;
                }
                var bitOffset = field.BitOffset ?? 0;

                if(type.IsArray)
                {
                    var elementType = type.GetElementType();
                    if(bitOffset != 0)
                    {
                        throw new ArgumentException("Bit offset for array is not supported.");
                    }
                    if(!elementType.IsPrimitive || elementType == typeof(bool))
                    {
                        throw new ArgumentException($"Decoding {elementType}[] is not currently supported (only non-bool primitives)");
                    }

                    var width = field.Width;
                    if(offset + width > data.Count)
                    {
                        return false;
                    }

                    if(width == 0)
                    {
                        throw new ArgumentException("Positive width must be provided to decode array");
                    }

                    var elementSize = CalculateLengthInner(elementType);
                    if(width % elementSize != 0)
                    {
                        throw new ArgumentException("Width in bytes must be a multiple of the element size to decode array");
                    }
                    var nElements = width / elementSize;

                    var v = Array.CreateInstance(elementType, nElements);
                    var source = data.Skip(offset).Take(width).ToArray();
                    if(field.IsLSBFirst != BitConverter.IsLittleEndian && elementSize > 1)
                    {
                        for(var i = 0; i < width; i += elementSize)
                        {
                            Array.Reverse(source, i, elementSize);
                        }
                    }

                    Buffer.BlockCopy(source, 0, v, 0, width);

                    field.SetValue(result, v);
                    offset += width;
                    continue;
                }

                var bytesRequired = field.BytesRequired;
                if(offset + bytesRequired > data.Count)
                {
                    return false;
                }

                if(Misc.IsStructType(type) || type.IsClass)
                {
                    if(!TryDecode(type, data, out var nestedPacket, offset))
                    {
                        return false;
                    }

                    field.SetValue(result, nestedPacket);
                    offset += bytesRequired;
                    continue;
                }

                var intermediate = 0UL;
                {
                    var i = 0;
                    foreach(var b in data.Skip(offset))
                    {
                        if(i >= bytesRequired)
                        {
                            break;
                        }
                        // assume host LSB bit ordering
                        var shift = (i << 3) - bitOffset;
                        intermediate |= shift > 0 ? (ulong)b << shift : (ulong)b >> -shift;
                        i += 1;
                    }
                }

                if(type == typeof(int) || type == typeof(uint))
                {
                    var v = (uint)intermediate;

                    if(!field.IsLSBFirst)
                    {
                        v = BitHelper.ReverseBytes(v);
                    }
                    v = BitHelper.GetValue(v, 0, field.BitWidth ?? 32);
                    if(type == typeof(int))
                    {
                        field.SetValue(result, (int)v);
                    }
                    else
                    {
                        field.SetValue(result, v);
                    }
                }
                else if(type == typeof(short) || type == typeof(ushort))
                {
                    var v = (ushort)intermediate;

                    if(!field.IsLSBFirst)
                    {
                        v = BitHelper.ReverseBytes(v);
                    }
                    v = (ushort)BitHelper.GetValue(v, 0, field.BitWidth ?? 16);
                    if(type == typeof(short))
                    {
                        field.SetValue(result, (short)v);
                    }
                    else
                    {
                        field.SetValue(result, v);
                    }
                }
                else if(type == typeof(byte) || type == typeof(sbyte))
                {
                    var v = (byte)intermediate;
                    v = BitHelper.GetValue(v, 0, field.BitWidth ?? 8);
                    if(type == typeof(sbyte))
                    {
                        field.SetValue(result, (sbyte)v);
                    }
                    else
                    {
                        field.SetValue(result, v);
                    }
                }
                else if(type == typeof(long) || type == typeof(ulong))
                {
                    var v = intermediate;
                    if(!field.IsLSBFirst)
                    {
                        v = BitHelper.ReverseBytes(v);
                    }
                    v = BitHelper.GetValue(v, 0, field.BitWidth ?? 64);
                    if(type == typeof(long))
                    {
                        field.SetValue(result, (long)v);
                    }
                    else
                    {
                        field.SetValue(result, v);
                    }
                }
                else if(type == typeof(bool))
                {
                    field.SetValue(result, BitHelper.IsBitSet(intermediate, 0));
                }
                else
                {
                    throw new ArgumentException($"Unsupported field type: {type.Name}");
                }
                offset += bytesRequired;
            }

            return true;
        }

        private static int EncodeInner(Type t, object packet, byte[] result, int offset)
        {
            var fieldsAndProperties = GetFieldsAndProperties(t);
            var startingOffset = offset;

            foreach(var field in fieldsAndProperties)
            {
                var type = field.ElementType;
                if(type.IsEnum)
                {
                    type = type.GetEnumUnderlyingType();
                }

                if(field.ByteOffset.HasValue)
                {
                    offset = startingOffset + field.ByteOffset.Value;
                }
                var bitOffset = field.BitOffset ?? 0;

                if(type.IsArray)
                {
                    var elementType = type.GetElementType();
                    if(bitOffset != 0)
                    {
                        throw new ArgumentException("Bit offset for array is not supported.");
                    }
                    if(!elementType.IsPrimitive || elementType == typeof(bool))
                    {
                        throw new ArgumentException($"Encoding {elementType}[] is not currently supported (only non-bool primitives)");
                    }

                    var width = field.Width;
                    if(width == 0)
                    {
                        throw new ArgumentException("Positive width must be provided to encode array");
                    }

                    var elementSize = CalculateLengthInner(elementType);
                    if(width % elementSize != 0)
                    {
                        throw new ArgumentException("Width must be a multiple of the element size to encode array");
                    }

                    var val = (Array)field.GetValue(packet);
                    var nElements = width / elementSize;

                    if(val == null)
                    {
                        // If field is not defined, assume this field is filled with zeros
                        val = Array.CreateInstance(elementType, nElements);
                    }

                    if(nElements != val.Length)
                    {
                        throw new ArgumentException("Declared and actual width is different: {0} vs {1}".FormatWith(width, val.Length * elementSize));
                    }

                    Buffer.BlockCopy(val, 0, result, offset, width);

                    if(field.IsLSBFirst != BitConverter.IsLittleEndian && elementSize > 1)
                    {
                        for(var i = offset; i < offset + width; i += elementSize)
                        {
                            Array.Reverse(result, i, elementSize);
                        }
                    }
                    offset += width;
                    continue;
                }

                var intermediate = 0UL;
                var bitWidth = field.BitWidth ?? 0;

                if(type == typeof(int))
                {
                    var v = (int)field.GetValue(packet);
                    intermediate = field.IsLSBFirst ? (uint)v : BitHelper.ReverseBytes((uint)v);
                }
                else if(type == typeof(uint))
                {
                    var v = (uint)field.GetValue(packet);
                    intermediate = field.IsLSBFirst ? v : BitHelper.ReverseBytes(v);
                }
                else if(type == typeof(short))
                {
                    var v = (short)field.GetValue(packet);
                    intermediate = field.IsLSBFirst ? (ushort)v : BitHelper.ReverseBytes((ushort)v);
                }
                else if(type == typeof(ushort))
                {
                    var v = (ushort)field.GetValue(packet);
                    intermediate = field.IsLSBFirst ? v : BitHelper.ReverseBytes(v);
                }
                else if(type == typeof(byte))
                {
                    intermediate = (byte)field.GetValue(packet);
                }
                else if(type == typeof(sbyte))
                {
                    intermediate = (ulong)(sbyte)field.GetValue(packet);
                }
                else if(type == typeof(long))
                {
                    var v = (long)field.GetValue(packet);
                    intermediate = field.IsLSBFirst ? (ulong)v : BitHelper.ReverseBytes((ulong)v);
                }
                else if(type == typeof(ulong))
                {
                    var v = (ulong)field.GetValue(packet);
                    intermediate = field.IsLSBFirst ? v : BitHelper.ReverseBytes(v);
                }
                else if(type == typeof(bool))
                {
                    intermediate = (bool)field.GetValue(packet) ? 1UL : 0UL;
                }
                else if(Misc.IsStructType(type) || type.IsClass)
                {
                    var nestedPacket = field.GetValue(packet);
                    offset += EncodeInner(type, nestedPacket, result, offset);
                    continue;
                }
                else
                {
                    throw new ArgumentException($"Unsupported field type: {field.ElementType}");
                }

                // write first byte
                result[offset] = result[offset].ReplaceBits((byte)intermediate, Math.Min(8, bitWidth), bitOffset);
                bitWidth -= Math.Min(8 - bitOffset, bitWidth);
                offset += 1;
                intermediate >>= 8 - bitOffset;
                // write next full bytes
                for(; bitWidth > 8; bitWidth -= 8)
                {
                    result[offset] = (byte)intermediate;
                    intermediate >>= 8;
                    offset += 1;
                }
                // write last non-full byte if exist
                if(bitWidth > 0)
                {
                    result[offset] = result[offset].ReplaceBits((byte)intermediate, bitWidth);
                    offset += 1;
                }
            }

            return offset - startingOffset;
        }

        private static int CalculateBitLengthInner(Type t)
        {
            lock(cache)
            {
                return cache.Get(t, CalculateBitLengthCacheGenerator);
            }
        }

        private static int CalculateLengthInner(Type t, object obj = null)
        {
            return CalculateBitLengthInner(t, obj) / 8;
        }

        // Separate function to prevent unintentional context capture when using a lambda, which completely destroys caching.
        private static int CalculateBitLengthCacheGenerator(Type t)
        {
            t = t.IsEnum ? t.GetEnumUnderlyingType() : t;
            if(t.IsPrimitive)
            {
                return Marshal.SizeOf(t) * 8;
            }

            var fieldsAndProperties = GetFieldsAndProperties(t);

            var maxOffset = 0;
            var offset = 0;
            foreach(var element in fieldsAndProperties)
            {
                var bytesRequired = element.BytesRequired;
                offset = element.ByteOffset ?? offset;

                var co = offset + bytesRequired;
                maxOffset = Math.Max(co, maxOffset);
                offset += bytesRequired;
            }

            maxOffset *= 8; // attribute value is in bits, convert offset to match
            var attrWidth = (int?)t.GetCustomAttribute<WidthAttribute>()?.Value;
            if(attrWidth < maxOffset)
            {
                throw new ArgumentException($"Explicitly-specified width ({attrWidth} bits) is less than actual width ({maxOffset} bits)");
            }
            return attrWidth ?? maxOffset;
        }

        private static FieldPropertyInfoWrapper[] GetFieldsAndProperties(Type t)
        {
            lock(cache)
            {
                return cache.Get(t, GetFieldsAndPropertiesCacheGenerator);
            }
        }

        // Separate function for the same reasons as CalculateLengthCacheGenerator
        private static FieldPropertyInfoWrapper[] GetFieldsAndPropertiesCacheGenerator(Type t)
        {
            return t.GetFields(DefaultBindingFlags)
                .Where(x => Attribute.IsDefined(x, typeof(PacketFieldAttribute)))
                .Select(x => new FieldPropertyInfoWrapper(x))
                .Union(t.GetProperties(DefaultBindingFlags)
                    .Where(x => Attribute.IsDefined(x, typeof(PacketFieldAttribute)))
                    .Select(x => new FieldPropertyInfoWrapper(x))
                ).OrderBy(x => x.Order).ToArray();
        }

        private static int CalculateOffsetInner(Type t, string fieldName)
        {
            lock(cache)
            {
                return cache.Get(t, fieldName, CalculateOffsetGenerator);
            }
        }

        // Separate function for the same reasons as CalculateLengthCacheGenerator
        private static int CalculateOffsetGenerator(Type t, string fieldName)
        {
            var fieldsAndProperties = GetFieldsAndProperties(t);

            var maxOffset = 0;
            var offset = 0;
            foreach(var element in fieldsAndProperties)
            {
                if(element.ElementName == fieldName)
                {
                    return maxOffset;
                }

                if(element.ByteOffset.HasValue)
                {
                    offset = element.ByteOffset.Value;
                }

                var bytesRequired = element.BytesRequired;
                var co = offset + bytesRequired;
                maxOffset = Math.Max(co, maxOffset);
                offset += bytesRequired;
            }

            return -1;
        }

        private static readonly SimpleCache cache = new SimpleCache();
        private static readonly BindingFlags DefaultBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private class FieldPropertyInfoWrapper
        {
            public FieldPropertyInfoWrapper(FieldInfo info)
            {
                fieldInfo = info;
            }

            public FieldPropertyInfoWrapper(PropertyInfo info)
            {
                propertyInfo = info;
            }

            public object GetValue(object o)
            {
                return (fieldInfo != null)
                    ? fieldInfo.GetValue(o)
                    : propertyInfo.GetValue(o);
            }

            public bool SetValue(object o, object v)
            {
                if(fieldInfo != null)
                {
                    fieldInfo.SetValue(o, v);
                }
                else
                {
                    if(!propertyInfo.CanWrite)
                    {
                        return false;
                    }
                    propertyInfo.SetValue(o, v);
                }

                return true;
            }

            public T GetAttribute<T>()
            {
                return (T)((MemberInfo)fieldInfo ?? propertyInfo).GetCustomAttributes(typeof(T), false).FirstOrDefault();
            }

            public bool IsLSBFirst => ((MemberInfo)fieldInfo ?? propertyInfo).DeclaringType.GetCustomAttribute<LeastSignificantByteFirst>(true) != null
                    || GetAttribute<LeastSignificantByteFirst>() != null;

            public int? ByteOffset => (int?)GetAttribute<OffsetAttribute>()?.OffsetInBytes;

            public int? BitOffset => (int?)GetAttribute<OffsetAttribute>()?.OffsetInBits;

            public int BytesRequired => ((BitOffset ?? 0) + BitWidth + 7) / 8 ?? (Width + (BitOffset > 0 ? 1 : 0));

            public int Order => GetAttribute<PacketFieldAttribute>().Order;

            public int Width
            {
                get
                {
                    var type = ElementType;
                    if(type.IsEnum)
                    {
                        type = type.GetEnumUnderlyingType();
                    }

                    if(type == typeof(byte) || type == typeof(bool) || type == typeof(sbyte))
                    {
                        return 1;
                    }
                    if(type == typeof(ushort) || type == typeof(short))
                    {
                        return 2;
                    }
                    if(type == typeof(uint) || type == typeof(int))
                    {
                        return 4;
                    }
                    if(type == typeof(ulong) || type == typeof(long))
                    {
                        return 8;
                    }
                    if(type.IsArray)
                    {
                        // attribute value is in bits
                        return (int)(GetAttribute<WidthAttribute>()?.Value ?? throw new ArgumentException("Array type must have specified Width")) / 8;
                    }
                    if(Misc.IsStructType(type) || type.IsClass)
                    {
                        return CalculateLengthInner(type);
                    }

                    throw new ArgumentException($"Unknown width of type: {type}");
                }
            }

            public int? BitWidth
            {
                get
                {
                    var type = ElementType;
                    if(type.IsEnum)
                    {
                        type = type.GetEnumUnderlyingType();
                    }

                    if(type == typeof(bool))
                    {
                        return 1;
                    }

                    int inBytes;
                    if(type == typeof(byte) || type == typeof(sbyte))
                    {
                        inBytes = sizeof(byte);
                    }
                    else if(type == typeof(ushort) || type == typeof(short))
                    {
                        inBytes = sizeof(ushort);
                    }
                    else if(type == typeof(uint) || type == typeof(int))
                    {
                        inBytes = sizeof(uint);
                    }
                    else if(type == typeof(ulong) || type == typeof(long))
                    {
                        inBytes = sizeof(ulong);
                    }
                    else
                    {
                        return null;
                    }

                    var width = inBytes << 3;
                    var setWidth = (int?)GetAttribute<WidthAttribute>()?.Value;
                    if(setWidth < 0)
                    {
                        throw new ArgumentException($"Width is less than zero.");
                    }
                    if(setWidth > width)
                    {
                        throw new ArgumentException($"Width is greater than the size of type: {type} ({width} bits).");
                    }
                    return setWidth ?? width;
                }
            }

            public Type ElementType => fieldInfo?.FieldType ?? propertyInfo.PropertyType;

            public string ElementName => fieldInfo?.Name ?? propertyInfo.Name;

            private readonly FieldInfo fieldInfo;
            private readonly PropertyInfo propertyInfo;
        }
    }
}
