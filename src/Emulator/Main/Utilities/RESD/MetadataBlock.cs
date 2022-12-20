//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities.RESD
{
    public class MetadataBlock
    {
        public static IDictionary<String, MetadataValue> ReadFromStream(SafeBinaryReader reader)
        {
            var metadata = new Dictionary<string, MetadataValue>();

            var metadataLength = reader.ReadUInt64();
            if(metadataLength == 0)
            {
                return metadata;
            }

            var startPosition = (ulong)reader.BaseStream.Position;

            while((ulong)reader.BaseStream.Position < startPosition + metadataLength)
            {
                var keyName = reader.ReadCString();
                var valueType = (MetadataValueType)reader.ReadByte();
                MetadataValue value;
                switch(valueType)
                {
                    case MetadataValueType.Int8:
                        value = new MetadataValue(reader.ReadSByte());
                        break;
                    case MetadataValueType.UInt8:
                        value = new MetadataValue(reader.ReadByte());
                        break;
                    case MetadataValueType.Int16:
                        value = new MetadataValue(reader.ReadInt16());
                        break;
                    case MetadataValueType.UInt16:
                        value = new MetadataValue(reader.ReadUInt16());
                        break;
                    case MetadataValueType.Int32:
                        value = new MetadataValue(reader.ReadInt32());
                        break;
                    case MetadataValueType.UInt32:
                        value = new MetadataValue(reader.ReadUInt32());
                        break;
                    case MetadataValueType.Int64:
                        value = new MetadataValue(reader.ReadInt64());
                        break;
                    case MetadataValueType.UInt64:
                        value = new MetadataValue(reader.ReadUInt64());
                        break;
                    case MetadataValueType.Float:
                        value = new MetadataValue(reader.ReadSingle());
                        break;
                    case MetadataValueType.Double:
                        value = new MetadataValue(reader.ReadDouble());
                        break;
                    case MetadataValueType.String:
                        value = new MetadataValue(reader.ReadCString());
                        break;
                    case MetadataValueType.Blob:
                        var blobLength = reader.ReadUInt32();
                        value = new MetadataValue(reader.ReadBytes((int)blobLength));
                        break;
                    default:
                        throw new RESDException($"Invalid metadata type ({valueType}), offset: {reader.BaseStream.Position - 1}");
                }

                metadata.Add(keyName, value);
            }

            if((ulong)reader.BaseStream.Position != (startPosition + metadataLength))
            {
                throw new RESDException($"Invalid amount of data read, expected: {metadataLength}, actual: {(ulong)reader.BaseStream.Position - startPosition}");
            }

            return metadata;
        }
    }
}
