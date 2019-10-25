//
// Copyright (c) 2010-2018 Antmicro
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
                    if(element.Offset.HasValue)
                    {
                        offset = element.Offset.Value;
                    }

                    var co = offset + element.Width;
                    maxOffset = Math.Max(co, maxOffset);
                    offset += element.Width;
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

                    if(element.Offset.HasValue)
                    {
                        offset = element.Offset.Value;
                    }

                    var co = offset + element.Width;
                    maxOffset = Math.Max(co, maxOffset);
                    offset += element.Width;
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

                if(field.Offset.HasValue)
                {
                    offset = dataOffset + field.Offset.Value;
                }

                if(type == typeof(uint))
                {
                    if(offset + sizeof(uint) > data.Count)
                    {
                        return false;
                    }

                    var v = field.IsLSBFirst
                        ? (uint)((data[offset + 3] << 24) | (data[offset + 2] << 16) | (data[offset + 1] << 8) | (data[offset]))
                        : (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | (data[offset + 3]));
                    offset += 4;
                    field.SetValue(innerResult, v);
                }
                else if(type == typeof(short))
                {
                    if(offset + sizeof(short) > data.Count)
                    {
                        return false;
                    }

                    var v = field.IsLSBFirst
                        ? (short)((data[offset + 1] << 8) | (data[offset]))
                        : (short)((data[offset] << 8) | (data[offset + 1]));
                    offset += 2;
                    field.SetValue(innerResult, v);
                }
                else if(type == typeof(ushort))
                {
                    if(offset + sizeof(ushort) > data.Count)
                    {
                        return false;
                    }

                    var v = field.IsLSBFirst
                        ? (ushort)((data[offset + 1] << 8) | (data[offset]))
                        : (ushort)((data[offset] << 8) | (data[offset + 1]));
                    offset += 2;
                    field.SetValue(innerResult, v);
                }
                else if(type == typeof(byte))
                {
                    if(offset + sizeof(byte) > data.Count)
                    {
                        return false;
                    }

                    // TODO: support Offset.bits/Width in other type as well
                    var offsetInBits = (int)(field.GetAttribute<OffsetAttribute>()?.OffsetInBits ?? 0);
                    var width = (int)(field.GetAttribute<WidthAttribute>()?.Value ?? 8);

                    if(offsetInBits + width > 8)
                    {
                        throw new ArgumentException($"Unsupported offset/width combination: {(offsetInBits + width)}");
                    }

                    var v = (byte)BitHelper.GetValue(data[offset], offsetInBits, width);
                    offset += 1;
                    field.SetValue(innerResult, v);
                }
                else if(type == typeof(byte[]))
                {
                    var width = (int)(field.GetAttribute<WidthAttribute>()?.Value ?? 0);
                    if(width == 0)
                    {
                        throw new ArgumentException("Positive width must be provided to decode byte array");
                    }

                    if(offset + width > data.Count)
                    {
                        return false;
                    }

                    var v = new byte[width];
                    for(var i = 0; i < width; i++)
                    {
                        v[i] = data[offset + i];
                    }

                    offset += width;
                    field.SetValue(innerResult, v);
                }
                else if(type == typeof(ulong))
                {
                    if(offset + sizeof(ulong) > data.Count)
                    {
                        return false;
                    }

                    var v = field.IsLSBFirst
                        ? (((ulong)data[offset + 7] << 56)
                                | ((ulong)data[offset + 6] << 48)
                                | ((ulong)data[offset + 5] << 40)
                                | ((ulong)data[offset + 4] << 32)
                                | ((ulong)data[offset + 3] << 24)
                                | ((ulong)data[offset + 2] << 16)
                                | ((ulong)data[offset + 1] << 8)
                                | ((ulong)data[offset]))
                        : (((ulong)data[offset] << 56)
                                | ((ulong)data[offset + 1] << 48)
                                | ((ulong)data[offset + 2] << 40)
                                | ((ulong)data[offset + 3] << 32)
                                | ((ulong)data[offset + 4] << 24)
                                | ((ulong)data[offset + 5] << 16)
                                | ((ulong)data[offset + 6] << 8)
                                | ((ulong)data[offset + 7]));
                    offset += 8;
                    field.SetValue(innerResult, v);
                }
                else
                {
                    throw new ArgumentException($"Unsupported field type: {type.Name}");
                }
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
                if(field.Offset.HasValue)
                {
                    offset = dataOffset + field.Offset.Value;
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

                if(element.Offset.HasValue)
                {
                    offset = element.Offset.Value;
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

            public int? Offset
            {
                get
                {
                    var offsetAttribute = GetAttribute<OffsetAttribute>();
                    return (offsetAttribute != null)
                        ? (int)offsetAttribute.OffsetInBytes
                        : (int?)null;
                }
            }

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

                    if(type == typeof(byte))
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

            public Type ElementType => fieldInfo?.FieldType ?? propertyInfo.PropertyType;

            public string ElementName => fieldInfo?.Name ?? propertyInfo.Name;

            private readonly FieldInfo fieldInfo;
            private readonly PropertyInfo propertyInfo;
        }
    }
}