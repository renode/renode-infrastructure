//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    public struct GDBCustomType
    {
        public GDBCustomType(string type, IReadOnlyDictionary<string, string> attributes, IEnumerable<IReadOnlyDictionary<string, string>> fields) : this()
        {
            this.Type = type;
            this.Attributes = attributes;
            this.Fields = fields ?? Enumerable.Empty<Dictionary<string, string>>();
        }

        public static GDBCustomType Vector(string id, string type, uint count)
        {
            var attributes = new Dictionary<string, string>();
            attributes.Add("id", id);
            attributes.Add("type", type);
            attributes.Add("count", $"{count}");
            return new GDBCustomType("vector", attributes, null);
        }

        public static GDBCustomType Union(string id, IEnumerable<GDBTypeField> fields)
        {
            var attributes = new Dictionary<string, string>();
            attributes.Add("id", id);
            return new GDBCustomType("union", attributes, CreateFields(fields));
        }

        public static GDBCustomType Struct(string id, IEnumerable<GDBTypeField> fields)
        {
            var attributes = new Dictionary<string, string>();
            attributes.Add("id", id);
            return new GDBCustomType("struct", attributes, CreateFields(fields));
        }

        public static GDBCustomType Struct(string id, uint size, IEnumerable<GDBTypeBitField> fields)
        {
            var attributes = new Dictionary<string, string>();
            attributes.Add("id", id);
            attributes.Add("size", $"{size}");
            return new GDBCustomType("struct", attributes, CreateFields(fields));
        }

        public static GDBCustomType Flags(string id, uint size, IEnumerable<GDBTypeBitField> fields)
        {
            var attributes = new Dictionary<string, string>();
            attributes.Add("id", id);
            attributes.Add("size", $"{size}");
            return new GDBCustomType("flags", attributes, CreateFields(fields));
        }

        public static GDBCustomType Enum(string id, uint size, IEnumerable<GDBTypeEnumValue> values)
        {
            var attributes = new Dictionary<string, string>();
            attributes.Add("id", id);
            attributes.Add("size", $"{size}");
            return new GDBCustomType("enum", attributes, CreateFields(values));
        }

        private static IEnumerable<IReadOnlyDictionary<string, string>> CreateFields(IEnumerable<GDBTypeField> types)
        {
            var fields = new List<Dictionary<string, string>>();
            foreach(var type in types)
            {
                var field = new Dictionary<string, string>();
                field.Add("name", type.Name);
                field.Add("type", type.Type);
                fields.Add(field);
            }
            return fields;
        }

        private static IEnumerable<IReadOnlyDictionary<string, string>> CreateFields(IEnumerable<GDBTypeBitField> bitFields)
        {
            var fields = new List<Dictionary<string, string>>();
            foreach(var type in bitFields)
            {
                var field = new Dictionary<string, string>();
                field.Add("name", type.Name);
                field.Add("start", $"{type.Start}");
                field.Add("end", $"{type.End}");
                field.Add("type", type.Type);
                fields.Add(field);
            }
            return fields;
        }

        private static IEnumerable<IReadOnlyDictionary<string, string>> CreateFields(IEnumerable<GDBTypeEnumValue> values)
        {
            var fields = new List<Dictionary<string, string>>();
            foreach(var value in values)
            {
                var field = new Dictionary<string, string>();
                field.Add("name", value.Name);
                field.Add("value", $"{value.Value}");
                fields.Add(field);
            }
            return fields;
        }

        public string Type { get; }
        public IReadOnlyDictionary<string, string> Attributes { get; }
        public IEnumerable<IReadOnlyDictionary<string, string>> Fields { get; }
    }
}
