//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    public struct GBDRegisterDescriptor
    {
        public GBDRegisterDescriptor(uint number, uint size, string name, string type, string group) : this()
        {
            this.Number = number;
            this.Size = size;
            this.Name = name;
            this.Type = type;
            this.Group = group;
        }

        public uint Number { get; }
        public uint Size { get; }
        public string Name { get; }
        public string Type { get; }
        public string Group { get; }
    }

    public struct GBDFeatureDescriptor
    {
        public GBDFeatureDescriptor(string name) : this()
        {
            this.Name = name;
            this.Registers = new List<GBDRegisterDescriptor>();
            this.Types = new List<GDBCustomType>();
        }

        public string Name { get; }
        public List<GBDRegisterDescriptor> Registers { get; }
        public List<GDBCustomType> Types { get; }
    }

    public struct GDBCustomType
    {
        public GDBCustomType(string type, IReadOnlyDictionary<string, string> attributes, IEnumerable<IReadOnlyDictionary<string, string>> fields) : this()
        {
            this.Type = type;
            this.Attributes = attributes;
            this.Fields = fields;
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

    public struct GDBTypeField
    {
        public GDBTypeField(string name, string type) : this()
        {
            this.Name = name;
            this.Type = type;
        }

        public string Name { get; }
        public string Type { get; }
    }

    public struct GDBTypeBitField
    {
        public GDBTypeBitField(string name, uint start, uint end, string type) : this()
        {
            this.Name = name;
            this.Start = start;
            this.End = end;
            this.Type = type;
        }

        public static GDBTypeBitField Filler(uint start, uint end, string type)
        {
            return new GDBTypeBitField("", start, end, type);
        }

        public string Name { get; }
        public uint Start { get; }
        public uint End { get; }
        public string Type { get; }
    }

    public struct GDBTypeEnumValue
    {
        public GDBTypeEnumValue(string name, uint value) : this()
        {
            this.Name = name;
            this.Value = value;
        }

        public string Name { get; }
        public uint Value { get; }
    }

    public interface ICpuSupportingGdb : ICPUWithHooks, IControllableCPU
    {
        ulong Step(int count = 1, bool? blocking = null);
        ExecutionMode ExecutionMode { get; set; }
        uint PageSize { get; }
        event Action<HaltArguments> Halted;
        void EnterSingleStepModeSafely(HaltArguments args, bool? blocking = null);
        ulong TranslateAddress(ulong logicalAddress, MpuAccess accessType);

        string GDBArchitecture { get; }
        List<GBDFeatureDescriptor> GDBFeatures { get; }
        bool DebuggerConnected { get; set; }
        uint Id { get; }
        string Name { get; }
    }

    public enum MpuAccess
    {
        Read = 0,
        Write = 1,
        InstructionFetch = 2
    }
}

