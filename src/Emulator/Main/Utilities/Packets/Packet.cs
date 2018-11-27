//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Reflection;

namespace Antmicro.Renode.Utilities.Packets
{
    public class Packet
    {
        public static T Decode<T>(byte[] data, int dataOffset = 0)
        {
            // we need to do the casting as otherwise setting value would not work on structs
            var result = (object)default(T);

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
                    var v = field.IsLSBFirst
                        ? (uint)((data[offset + 3] << 24) | (data[offset + 2] << 16) | (data[offset + 1] << 8) | (data[offset]))
                        : (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | (data[offset + 3]));
                    offset += 4;
                    field.SetValue(result, v);
                }
                else if(type == typeof(short))
                {
                    var v = field.IsLSBFirst
                        ? (short)((data[offset + 1] << 8) | (data[offset]))
                        : (short)((data[offset] << 8) | (data[offset + 1]));
                    offset += 2;
                    field.SetValue(result, v);
                }
                else if(type == typeof(ushort))
                {
                    var v = field.IsLSBFirst
                        ? (ushort)((data[offset + 1] << 8) | (data[offset]))
                        : (ushort)((data[offset] << 8) | (data[offset + 1]));
                    offset += 2;
                    field.SetValue(result, v);
                }
                else if(type == typeof(byte))
                {
                    // TODO: support Offset.bits/Width in other type as well
                    var offsetInBits = field.GetAttribute<OffsetAttribute>()?.OffsetInBits ?? 0;
                    var width = field.GetAttribute<WidthAttribute>()?.Value ?? 8;

                    if(offsetInBits + width > 8)
                    {
                        throw new ArgumentException($"Unsupported offset/width combination");
                    }

                    var v = (byte)BitHelper.GetValue(data[offset], offsetInBits, width);
                    offset += 1;
                    field.SetValue(result, v);
                }
                else
                {
                    throw new ArgumentException($"Unsupported field type: {typeof(T).Name}");
                }
            }

            return (T)result;
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
            var result = new byte[maxOffset];

            offset = 0;
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
                else if(type == typeof(byte))
                {
                    result[offset] = (byte)element.GetValue(packet);
                    offset++;
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
            return typeof(T).GetFields()
                .Where(x => Attribute.IsDefined(x, typeof(PacketFieldAttribute)))
                .Select(x => new FieldPropertyInfoWrapper(x))
                .Union(typeof(T).GetProperties()
                    .Where(x => Attribute.IsDefined(x, typeof(PacketFieldAttribute)))
                    .Select(x => new FieldPropertyInfoWrapper(x))
                ).OrderBy(x => x.Order).ToArray();
        }

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

            public void SetValue(object o, object v)
            {
                if(fieldInfo != null)
                {
                    fieldInfo.SetValue(o, v);
                }
                else
                {
                    propertyInfo.SetValue(o, v);
                }
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
                        ? offsetAttribute.OffsetInBytes
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