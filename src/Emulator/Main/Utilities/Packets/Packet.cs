//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Utilities.Packets
{
    public class Packet
    {
        public static int CalculateLength<T>()
        {
            return cache.Get(typeof(T), _ =>
            {
                var fieldsAndProperties = GetFieldsAndProperties<T>();

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

                return maxOffset;
            });
        }

        public static int CalculateOffset<T>(string fieldName)
        {
            return cache.Get(Tuple.Create(typeof(T), fieldName), _ =>
            {
                var fieldsAndProperties = GetFieldsAndProperties<T>();

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
            });
        }

        public static T Decode<T>(IList<byte> data, int dataOffset = 0)
        {
            if(!TryDecode<T>(data, out var result, dataOffset))
            {
                throw new ArgumentException($"Could not decode the packet of type {typeof(T)} due to insufficient data. Required {Packet.CalculateLength<T>()} bytes, but received {(data.Count - dataOffset)}");
            }
            return result;
        }

        public static bool TryDecode<T>(IList<byte> data, out T result, int dataOffset = 0)
        {
            // we need to do the casting as otherwise setting value would not work on structs
            var innerResult = (object)default(T);
            result = default(T);

            var fieldsAndProperties = GetFieldsAndProperties<T>();

            var offset = dataOffset;
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

                if(type == typeof(byte[]))
                {
                    var width = field.Width;
                    if(offset + width > data.Count)
                    {
                        return false;
                    }
                    if(width == 0)
                    {
                        throw new ArgumentException("Positive width must be provided to decode byte array");
                    }

                    var v = new byte[width];
                    for(var i = 0; i < width; i++)
                    {
                        v[i] = data[offset + i];
                    }

                    field.SetValue(innerResult, v);
                    offset += width;
                    continue;
                }

                var bitOffset = field.BitOffset ?? 0;
                var bytesRequired = field.BytesRequired;
                if(offset + bytesRequired > data.Count)
                {
                    return false;
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

                if(type == typeof(uint))
                {
                    var v = (uint)intermediate;

                    if(!field.IsLSBFirst)
                    {
                        v = BitHelper.ReverseBytes(v);
                    }
                    v = BitHelper.GetValue(v, 0, field.BitWidth ?? 32);
                    field.SetValue(innerResult, v);
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
                        field.SetValue(innerResult, (short)v);
                    }
                    else
                    {
                        field.SetValue(innerResult, v);
                    }
                }
                else if(type == typeof(byte))
                {
                    var v = (byte)intermediate;
                    v = BitHelper.GetValue(v, 0, field.BitWidth ?? 8);
                    field.SetValue(innerResult, v);
                }
                else if(type == typeof(ulong))
                {
                    var v = intermediate;
                    if(!field.IsLSBFirst)
                    {
                        v = BitHelper.ReverseBytes(v);
                    }
                    v = BitHelper.GetValue(v, 0, field.BitWidth ?? 64);
                    field.SetValue(innerResult, v);
                }
                else if(type == typeof(bool))
                {
                    field.SetValue(innerResult, BitHelper.IsBitSet(intermediate, 0));
                }
                else
                {
                    throw new ArgumentException($"Unsupported field type: {type.Name}");
                }
                offset += bytesRequired;
            }

            result = (T)innerResult;
            return true;
        }

        public static dynamic DecodeDynamic<T>(byte[] data, int dataOffset = 0) where T : class
        {
            if(!typeof(T).IsInterface)
            {
                throw new ArgumentException("This can be called on interfaces only");
            }

            var result = new DynamicPropertiesObject();

            var fieldsAndProperties = GetFieldsAndProperties<T>();

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
            var fieldsAndProperties = GetFieldsAndProperties<T>();
            var size = CalculateLength<T>();
            var result = new byte[size];

            var offset = 0;
            foreach(var element in fieldsAndProperties)
            {
                var type = element.ElementType;
                if(type.IsEnum)
                {
                    type = type.GetEnumUnderlyingType();
                }

                if(element.ByteOffset.HasValue)
                {
                    offset = element.ByteOffset.Value;
                }

                var widthAttribute = element.GetAttribute<WidthAttribute>();
                var offsetAttribute = element.GetAttribute<OffsetAttribute>();

                if(widthAttribute != null && !(type == typeof(byte) || type == (typeof(byte[]))))
                {
                    throw new Exception("Width attribtue is currently supported only for byte/byte[] types");
                }

                if(offsetAttribute != null && type != typeof(byte))
                {
                    throw new Exception("Offset attribtue is currently supported only for byte type");
                }

                if(type == typeof(uint))
                {
                    var isLsb = element.IsLSBFirst;
                    var value = (uint)element.GetValue(packet);
                    result[offset] = (byte)(value >> (isLsb ? 0: 24));
                    result[offset + 1] = (byte)(value >> (isLsb ? 8 : 16));
                    result[offset + 2] = (byte)(value >> (isLsb ? 16 : 8));
                    result[offset + 3] = (byte)(value >> (isLsb ? 24 : 0));
                    offset += 4;
                }
                else if(type == typeof(ushort))
                {
                    var isLsb = element.IsLSBFirst;
                    var value = (ushort)element.GetValue(packet);
                    result[offset] = (byte)(value >> (isLsb ? 0: 8));
                    result[offset + 1] = (byte)(value >> (isLsb ? 8 : 0));
                    offset += 2;
                }
                else if(type == typeof(byte))
                {
                    var val = (byte)element.GetValue(packet);
                    if(offsetAttribute != null)
                    {
                        var width = (int)(widthAttribute?.Value ?? 0);
                        if(width == 0)
                        {
                            throw new ArgumentException("Positive width must be provided together with offset attribute");
                        }

                        if(offsetAttribute.OffsetInBytes != 0)
                        {
                            throw new Exception("Non-zero byte offset is currently not supported");
                        }

                        if(offsetAttribute.OffsetInBits > 7)
                        {
                            throw new Exception("Offset in bits can be only in range 0 to 7");
                        }

                        if(offsetAttribute.OffsetInBits + width > 8)
                        {
                            throw new Exception($"Offset/width combination has a wrong value: {(offsetAttribute.OffsetInBits + width)}");
                        }

                        result[offset] = result[offset].ReplaceBits(val, width, (int)offsetAttribute.OffsetInBits);
                    }
                    else
                    {
                        result[offset] = val;
                    }

                    offset++;
                }
                else if(type == typeof(byte[]))
                {
                    var width = (int)(widthAttribute?.Value ?? 0);
                    if(width == 0)
                    {
                        throw new ArgumentException("Positive width must be provided to decode byte array");
                    }

                    var val = (byte[])element.GetValue(packet);

                    if(width != val.Length)
                    {
                        throw new ArgumentException("Declared and actual width is different: {0} vs {1}".FormatWith(width, val.Length));
                    }

                    Array.Copy(val, 0, result, offset, width);
                    offset += width;
                }
                else if(type == typeof(ulong))
                {
                    var isLsb = element.IsLSBFirst;
                    var value = (ulong)element.GetValue(packet);
                    result[offset] = (byte)(value >> (isLsb ? 0: 56));
                    result[offset + 1] = (byte)(value >> (isLsb ? 8 : 48));
                    result[offset + 2] = (byte)(value >> (isLsb ? 16 : 40));
                    result[offset + 3] = (byte)(value >> (isLsb ? 24 : 32));
                    result[offset + 4] = (byte)(value >> (isLsb ? 32 : 24));
                    result[offset + 5] = (byte)(value >> (isLsb ? 40 : 16));
                    result[offset + 6] = (byte)(value >> (isLsb ? 48 : 8));
                    result[offset + 7] = (byte)(value >> (isLsb ? 56 : 0));
                    offset += 8;
                }
                else
                {
                    throw new ArgumentException($"Unsupported field type: {element.ElementType}");
                }
            }

            if(result.Length == 0)
            {
                throw new ArgumentException($"It seems there was no fields in the packet of type {typeof(T).Name}");
            }

            return result;
        }

        private static FieldPropertyInfoWrapper[] GetFieldsAndProperties<T>()
        {
            return cache.Get(typeof(T), _ =>
            {
                return typeof(T).GetFields()
                    .Where(x => Attribute.IsDefined(x, typeof(PacketFieldAttribute)))
                    .Select(x => new FieldPropertyInfoWrapper(x))
                    .Union(typeof(T).GetProperties()
                        .Where(x => Attribute.IsDefined(x, typeof(PacketFieldAttribute)))
                        .Select(x => new FieldPropertyInfoWrapper(x))
                    ).OrderBy(x => x.Order).ToArray();
            });
        }

        private static readonly SimpleCache cache = new SimpleCache();

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

            public bool IsLSBFirst => ((MemberInfo)fieldInfo ?? propertyInfo).DeclaringType.GetCustomAttribute<LeastSignificantByteFirst>() != null
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

                    if(type == typeof(byte) || type == typeof(bool))
                    {
                        return 1;
                    }
                    if(type == typeof(ushort))
                    {
                        return 2;
                    }
                    if(type == typeof(uint))
                    {
                        return 4;
                    }
                    if(type == typeof(ulong))
                    {
                        return 8;
                    }
                    if(type == typeof(byte[]))
                    {
                        return (int)(GetAttribute<WidthAttribute>()?.Value ?? 0);
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
                    if(type == typeof(byte))
                    {
                        inBytes = sizeof(byte);
                    }
                    else if(type == typeof(ushort))
                    {
                        inBytes = sizeof(ushort);
                    }
                    else if(type == typeof(uint))
                    {
                        inBytes = sizeof(uint);
                    }
                    else if(type == typeof(ulong))
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