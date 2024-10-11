//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Xml.Linq;
using System.Linq;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using System.Xml;
using System.IO;
using Antmicro.Renode.Utilities;
using System.IO.Compression;

namespace Antmicro.Renode.Peripherals.Bus
{
    public sealed class SVDParser
    {
        public SVDParser(string path, IBusController parent)
        {
            currentSystemBus = parent;
            XDocument document;
            Stream possibleGzip;
            try
            {
                using(possibleGzip = File.OpenRead(path))
                {
                    if(possibleGzip.ReadByte() == 0x1F && possibleGzip.ReadByte() == 0x8B) // gzip header
                    {
                        parent.Log(LogLevel.Info, "Detected gzipped file, ungzipping.");
                        possibleGzip.Close();
                        possibleGzip = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
                        path = TemporaryFilesManager.Instance.GetTemporaryFile();
                        using(var extractedFile = File.OpenWrite(path))
                        {
                            possibleGzip.CopyTo(extractedFile);
                        }
                        parent.Log(LogLevel.Info, "Successfully ungzipped.");
                    }
                }
                using(var stream = File.OpenRead(path))
                {
                    document = XDocument.Load(stream);
                }
            }
            catch(Exception ex)
            {
                if(ex is FileNotFoundException || ex is DirectoryNotFoundException || ex is PathTooLongException)
                {
                    throw new RecoverableException($"File '{path}' does not exist.");
                }
                else if(ex is UnauthorizedAccessException)
                {
                    throw new RecoverableException($"File '{path}' cannot be loaded due to insufficient permissions.");
                }
                else if(ex is IOException)
                {
                    throw new RecoverableException($"An I/O error occurred while opening the file '{path}'.");
                }
                else if(ex is XmlException)
                {
                    throw new RecoverableException($"Given SVD file could not be loaded due to an exception: {ex.Message}.");
                }
                throw;
            }
            var deviceNode = document.Elements().FirstOrDefault(x => x.Name == "device");
            if(deviceNode == null)
            {
                throw new RecoverableException($"There is no <device> element in the file '{path}'.");
            }
            var device = new SVDDevice(deviceNode, this);

            registerDictionary = new Dictionary<ulong, SVDRegister>();
            foreach(var register in device.Peripherals.SelectMany(x => x.Registers))
            {
                AppendRegisterToDictionary(register);
            }
            parent.Log(LogLevel.Info, "Loaded SVD: {0}. Name: {1}. Description: {2}.", path, device.Name, device.Description);
        }

        public bool TryReadAccess(ulong offset, out ulong value, SysbusAccessWidth width)
        {
            var bytesToRead = CheckAndGetWidth(width);
            var weHaveIt = false;
            value = 0;

            if(HitInTheRegisterDictionary(out var tmpRegister, offset, bytesToRead))
            {
                weHaveIt = true;
                if(tmpRegister.HasReadAccess)
                {
                    value = tmpRegister.ResetValue;
                    LogReadSuccess(value, tmpRegister.Peripheral.Name, tmpRegister.Name, offset, width);
                }
                else
                {
                    LogReadFail(tmpRegister.Peripheral.Name, tmpRegister.Name, offset, width);
                }
            }
            else
            {
                value = AssembleValueFromRegisters(offset, width, ref weHaveIt);
            }
            return weHaveIt;
        }

        public bool TryWriteAccess(ulong offset, ulong value, SysbusAccessWidth width)
        {
            int bytesToWrite = CheckAndGetWidth(width);
            var weHaveIt = false;
            if(HitInTheRegisterDictionary(out var tmpRegister, offset, bytesToWrite))
            {
                weHaveIt = true;
                if(tmpRegister.HasWriteAccess || tmpRegister.HasWriteOnceAccess)
                {
                    LogWriteSuccess(value, tmpRegister.Peripheral.Name, tmpRegister.Name, offset, width, tmpRegister.HasWriteOnceAccess);
                }
                else
                {
                    LogWriteFail(value, tmpRegister.Peripheral.Name, tmpRegister.Name, offset, width);
                }
            }
            else
            {
                LogWriteRequests(value, offset, width, ref weHaveIt);
            }
            return weHaveIt;
        }

        private bool HitInTheRegisterDictionary(out SVDRegister tmpRegister, ulong offset, int countOfBytes)
        {
            return registerDictionary.TryGetValue(offset, out tmpRegister) && tmpRegister.Address == offset && tmpRegister.SizeInBytes == countOfBytes;
        }

        private void LogWriteRequests(ulong value, ulong offset, SysbusAccessWidth width, ref bool weHaveIt)
        {
            var sizeInBytes = (int)width;
            for(var i = 0; i < sizeInBytes; i++)
            {
                var tmpOffset = offset + (ulong)i;
                if(registerDictionary.TryGetValue(tmpOffset, out var register))
                {
                    var howManyTimes = HowManyRequestsToTheRegister(register, sizeInBytes, offset, tmpOffset);
                    var mask = ((1ul << (8 * howManyTimes)) - 1) << (8 * i);
                    var tmpValue = (value & mask) >> (8 * i);
                    if(register.HasWriteAccess || register.HasWriteOnceAccess)
                    {
                        weHaveIt = true;
                        LogWriteSuccess(tmpValue, register.Peripheral.Name, register.Name, tmpOffset, width, register.Access == PermittedAccess.WriteOnce, offset, value);
                    }
                    else
                    {
                        LogWriteFail(tmpValue, register.Peripheral.Name, register.Name, tmpOffset, width, offset, value);
                    }
                    i += howManyTimes - 1;
                }
            }
        }

        private ulong AssembleValueFromRegisters(ulong offset, SysbusAccessWidth width, ref bool weHaveIt)
        {
            var sizeInBytes = (int)width;
            var result = 0ul;
            for(var i = 0; i < sizeInBytes; i++)
            {
                var tmpOffset = offset + (ulong)i;
                if(registerDictionary.TryGetValue(tmpOffset, out var register))
                {
                    var howManyTimes = HowManyRequestsToTheRegister(register, sizeInBytes, offset, tmpOffset);
                    var mask = ((1ul << (8 * howManyTimes)) - 1) << (8 * (int)(tmpOffset - register.Address));
                    var tmpValue = (register.ResetValue & mask) >> (8 * (int)(tmpOffset - register.Address));
                    if(register.HasReadAccess)
                    {
                        weHaveIt = true;
                        LogReadSuccess(tmpValue, register.Peripheral.Name, register.Name, tmpOffset, width, offset);
                        result += tmpValue << (8 * i);
                    }
                    else
                    {
                        LogReadFail(register.Peripheral.Name, register.Name, tmpOffset, width, offset);
                    }
                    i += howManyTimes - 1;
                }
            }
            return result;
        }

        private int HowManyRequestsToTheRegister(SVDRegister register, int sizeInBytes, ulong offset, ulong tmpOffset)
        {
            var registerLastAddress = register.Address + register.SizeInBytes - 1;
            var lastReadingAddress = offset + checked((ulong)sizeInBytes) - 1;
            var diff = (int)(registerLastAddress - lastReadingAddress);
            int howManyTimes = (int)(registerLastAddress - tmpOffset + 1) - (diff > 0 ? diff : 0);
            return howManyTimes;
        }

        private void AppendRegisterToDictionary(SVDRegister register)
        {
            var bytes = register.SizeInBytes;
            for(var i = 0u; i < bytes; i++)
            {
                var address = register.Address + i;
                if(!registerDictionary.ContainsKey(address))
                {
                    registerDictionary.Add(address, register);
                }
                else
                {
                    // There is a posibility to set the same address for various registers.
                    // e.g. first register with a read-only permission access but second with a write-only.
                    registerDictionary[address].MergeWithRegister(register);
                }
            }
        }

        private void LogReadSuccess(ulong value, string peripheralName, string name, ulong offset, SysbusAccessWidth width, ulong? originalOffset = null)
        {
            var formatString = "Read{5} from an unimplemented register {1}:{2} (0x{3:X}){4}, returning a value from SVD: 0x{0:X}";
            formatString = currentSystemBus.DecorateWithCPUNameAndPC(formatString);

            var originalReadIndication = String.Empty;
            if(originalOffset.HasValue)
            {
                originalReadIndication = $" (caused by reading offset 0x{originalOffset:X})";
            }
            currentSystemBus.Log(
                LogLevel.Warning,
                formatString,
                value,
                peripheralName,
                name,
                offset,
                originalReadIndication,
                width
            );
        }

        private void LogWriteSuccess(ulong value, string peripheralName, string name, ulong offset, SysbusAccessWidth width, bool writeOnce = false, ulong? originalOffset = null, ulong? originalValue = null)
        {
            var formatString = "Write{6} of value 0x{0:X} to an unimplemented{5} register {1}:{2} (0x{3:X}){4} generated from SVD";
            formatString = currentSystemBus.DecorateWithCPUNameAndPC(formatString);

            var originalWriteIndication = String.Empty;
            var writeOnceIndication = String.Empty;
            if(originalValue.HasValue)
            {
                originalWriteIndication = $" (caused by writing offset 0x{originalOffset:X} value 0x{originalValue:X})";
            }
            if(writeOnce)
            {
                writeOnceIndication = " write-once";
            }
            currentSystemBus.Log(
                LogLevel.Warning,
                formatString,
                value,
                peripheralName,
                name,
                offset,
                originalWriteIndication,
                writeOnceIndication,
                width
            );
        }

        private void LogReadFail(string peripheralName, string name, ulong offset, SysbusAccessWidth width, ulong? originalOffset = null)
        {
            var formatString = "Invalid Read{4} from an unimplemented write-only register {0}:{1} (0x{2:X}){3}, returning a value from SVD: 0x0";
            formatString = currentSystemBus.DecorateWithCPUNameAndPC(formatString);

            var originalReadIndication = String.Empty;
            if(originalOffset.HasValue)
            {
                originalReadIndication = $" (caused by reading offset 0x{originalOffset:X})";
            }
            currentSystemBus.Log(
                LogLevel.Warning,
                formatString,
                peripheralName,
                name,
                offset,
                originalReadIndication,
                width
            );
        }

        private void LogWriteFail(ulong value, string peripheralName, string name, ulong offset, SysbusAccessWidth width, ulong? originalOffset = null, ulong? originalValue = null)
        {
            var formatString = "Invalid Write{5} of value 0x{0:X} to an unimplemented read-only register {1}:{2} (0x{3:X}){4} generated from SVD";
            formatString = currentSystemBus.DecorateWithCPUNameAndPC(formatString);

            var originalWriteIndication = String.Empty;
            if(originalValue.HasValue)
            {
                originalWriteIndication = $" (caused by writing offset 0x{originalOffset:X} value 0x{originalValue:X})";
            }
            currentSystemBus.Log(
                LogLevel.Warning,
                formatString,
                value,
                peripheralName,
                name,
                offset,
                originalWriteIndication,
                width
            );
        }

        private static int CheckAndGetWidth(SysbusAccessWidth type)
        {
            switch(type)
            {
            case SysbusAccessWidth.Byte:
            case SysbusAccessWidth.Word:
            case SysbusAccessWidth.DoubleWord:
            case SysbusAccessWidth.QuadWord:
                return (int)type;
            default:
                throw new ArgumentException($"'{type}' is unsupported width of data.");
            }
        }

        private static ulong? CalculateOffset(ulong? baseAddress, ulong? addressOffset)
        {
            if(baseAddress == null)
            {
                return null;
            }
            else
            {
                if(addressOffset == null)
                {
                    return baseAddress;
                }
                else
                {
                    return baseAddress + addressOffset;
                }
            }
        }

        private static string GetMandatoryField(XElement node, string fieldName)
        {
            var fieldElement = node.Element(fieldName);
            if(fieldElement != null)
            {
                return fieldElement.Value;
            }
            else
            {
                var path = GetPath(node);
                throw new RecoverableException($"Field '{fieldName}' in '{path}' is required.");
            }
        }

        private static string GetOptionalFieldOrNull(XElement node, string fieldName)
        {
            var fieldElement = node.Element(fieldName);
            if(fieldElement != null)
            {
                return fieldElement.Value;
            }
            else
            {
                return null;
            }
        }

        private static string GetPath(XElement node)
        {
            var rootElementName = "device";
            var path = node.Element("name") != null ? node.Element("name").Value : $"<{node.Name.LocalName}>";
            var tmpElement = node.Parent;
            if(tmpElement != null)
            {
                while(tmpElement.Name != rootElementName)
                {
                    var tmpNameElement = tmpElement.Element("name");
                    if(tmpNameElement != null)
                    {
                        path = $"{tmpNameElement.Value}.{path}";
                    }
                    tmpElement = tmpElement.Parent;
                }
            }
            return path;
        }

        private static ulong? SmartParseHexOrDecimal(string whatToParse, XElement node)
        {
            if(whatToParse == null)
            {
                return null;
            }
            else
            {
                SmartParser.Instance.TryParse(whatToParse, typeof(ulong), out var result);
                if(result != null)
                {
                    return (ulong)result;
                }
                else
                {
                    throw new RecoverableException($"Cannot parse '{whatToParse}' to a number in '{GetPath(node)}'.");
                }
            }
        }

        private IBusController currentSystemBus;
        private Dictionary<ulong, SVDRegister> registerDictionary;

        private class SVDDevice
        {
            public SVDDevice(XElement deviceNode, SVDParser parent)
            {
                this.deviceNode = deviceNode;
                Parent = parent;
                Description = GetMandatoryField(deviceNode, "description");
                Name = GetMandatoryField(deviceNode, "name");
                Peripherals = new List<SVDPeripheral>();

                var cpuElement = deviceNode.Element("cpu");
                if(cpuElement == null)
                {
                    Endianess = Endianess.LittleEndian;
                }
                else
                {
                    var endianessString = GetMandatoryField(cpuElement, "endian");
                    Endianess = GetEndianess(endianessString);
                }

                var defaultRegisterSettings = GetLocalRegisterSettings(deviceNode);
                ScanPeripherals(defaultRegisterSettings);
            }

            public String Name { get; private set; }
            public String Description { get; private set; }
            public Endianess Endianess { get; private set; }
            public List<SVDPeripheral> Peripherals { get; private set; }
            public SVDParser Parent { get; private set; }

            private void ScanPeripherals(RegisterSettings defaultRegisterSettings)
            {
                if(deviceNode.Element("peripherals") == null)
                {
                    throw new RecoverableException("There is no <peripherals> section.");
                }
                var peripheralElements = deviceNode.Element("peripherals").Elements("peripheral");
                if(!peripheralElements.Any())
                {
                    Parent.currentSystemBus.Log(LogLevel.Warning, "There are no <peripheral> sections in <peripherals>.");
                }
                foreach(var peripheralElement in peripheralElements)
                {
                    var derivingAttribute = peripheralElement.Attribute("derivedFrom");
                    if(derivingAttribute == null)
                    {
                        ScanPeripheral(peripheralElement, defaultRegisterSettings);
                    }
                    else
                    {
                        var derivedElement = GetDerivedElementFromTheScope(
                            peripheralElement,
                            peripheralElements,
                            NestingType.Peripheral,
                            derivingAttribute.Value
                        );
                        if(!peripheralElement.Elements("registers").Any())
                        {
                            peripheralElement.Add(derivedElement.Element("registers"));
                        }
                        var newDefaultRegisterSettings = GetRegisterSettings(derivedElement, defaultRegisterSettings);
                        ScanPeripheral(peripheralElement, newDefaultRegisterSettings);
                    }
                }
            }

            private void ScanPeripheral(XElement node, RegisterSettings defaultRegisterSettings)
            {
                var name = GetMandatoryField(node, "name");
                var newRegisterSettings = GetRegisterSettings(node, defaultRegisterSettings);
                var peripheral = new SVDPeripheral(name, this);
                Peripherals.Add(peripheral);

                var registersElement = node.Element("registers");
                if(registersElement != null)
                {
                    ScanRegistersAndClusters(registersElement, newRegisterSettings, peripheral);
                }
            }

            private void ScanRegistersAndClusters(XElement node, RegisterSettings defaultRegisterSettings, SVDPeripheral peripheral)
            {
                ScanClusters(node, defaultRegisterSettings, peripheral);
                ScanRegisters(node, defaultRegisterSettings, peripheral);
            }

            private XElement GetDerivedElementFromTheScope(XElement element, IEnumerable<XElement> collection, NestingType type, string derivedName)
            {
                var result = collection.FirstOrDefault(x => x.Name == type.ToString().ToLower() && ElementNameEquals(x, derivedName));
                if(result == null)
                {
                    var name = GetMandatoryField(element, "name");
                    throw new RecoverableException($"The SVD {type} '{name}' derives from '{derivedName}', but '{derivedName}' cannot be found.");
                }
                return result;
            }

            private void ScanClusters(XElement node, RegisterSettings defaultRegisterSettings, SVDPeripheral peripheral)
            {
                var clusterItems = node.Elements("cluster");
                foreach(var cluster in clusterItems)
                {
                    var derivedAttribute = cluster.Attribute("derivedFrom");
                    if(derivedAttribute == null)
                    {
                        ScanCluster(cluster, defaultRegisterSettings, peripheral);
                    }
                    else
                    {
                        var derivedClusterName = derivedAttribute.Value;
                        var derivedCluster = FindDerivedElement(NestingType.Cluster, cluster, derivedAttribute.Value, defaultRegisterSettings, out var outRegisterSettings);

                        if(!cluster.Elements("register").Any())
                        {
                            cluster.Add(derivedCluster.Elements("register"));
                        }
                        if(!cluster.Elements("cluster").Any())
                        {
                            cluster.Add(derivedCluster.Elements("cluster"));
                        }

                        ScanCluster(cluster, outRegisterSettings, peripheral);
                    }
                }
            }

            private void ScanRegisters(XElement node, RegisterSettings defaultRegisterSettings, SVDPeripheral peripheral)
            {
                var registerItems = node.Elements("register");
                foreach(var register in registerItems)
                {
                    var derivedAttribute = register.Attribute("derivedFrom");
                    if(derivedAttribute == null)
                    {
                        ScanRegister(register, defaultRegisterSettings, peripheral);
                    }
                    else
                    {
                        var derivedRegisterName = derivedAttribute.Value;
                        var derivedRegister = FindDerivedElement(NestingType.Register, register, derivedAttribute.Value, defaultRegisterSettings, out var outRegisterSettings);

                        ScanRegister(register, outRegisterSettings, peripheral);
                    }
                }
            }

            private XElement FindDerivedElement(NestingType nestingType, XElement element, String derivedElementName, RegisterSettings defaultRegisterSettings, out RegisterSettings outRegisterSettings)
            {
                string[] derivedSplitNode = derivedElementName.Split('.');
                outRegisterSettings = defaultRegisterSettings;

                XElement derivedElement;
                if(derivedSplitNode.Length == 1)
                {
                    derivedElement = GetDerivedElementFromTheScope(
                        element,
                        element.Parent.Elements(),
                        nestingType,
                        derivedElementName
                    );
                }
                else
                {
                    var derivedPeripheral = GetDerivedElementFromTheScope(
                        element,
                        deviceNode.Element("peripherals").Elements(),
                        NestingType.Peripheral,
                        derivedSplitNode[0]
                    );

                    outRegisterSettings = GetRegisterSettings(derivedPeripheral, defaultRegisterSettings);
                    derivedElement = derivedPeripheral.Element("registers");

                    // if we are deriving from the cluster we should go through all cluster in the cluster chain, 
                    // but if we are deriving from register we should go through all cluster chain but last one.
                    var limit = derivedSplitNode.Length - (nestingType == NestingType.Register ? 1 : 0);
                    for(var i = 1; i < limit; i++)
                    {
                        derivedElement = GetDerivedElementFromTheScope(
                            derivedElement,
                            derivedElement.Elements("cluster"),
                            NestingType.Cluster,
                            derivedSplitNode[i]
                        );
                        var address = CalculateOffset(
                            outRegisterSettings.Address,
                            GetAddressOffset(derivedElement)
                        );
                        outRegisterSettings = GetRegisterSettings(derivedElement, outRegisterSettings, address);
                    }
                    // If we are deriving from the register, we must find this register in the last cluster in the chain. 
                    if(nestingType == NestingType.Register)
                    {
                        derivedElement = derivedElement.Elements("register").First(x => x.Element("name").Value == derivedSplitNode[derivedSplitNode.Length - 1]);
                    }
                }
                outRegisterSettings = GetRegisterSettings(derivedElement, outRegisterSettings, defaultRegisterSettings.Address);
                return derivedElement;
            }

            private void ScanCluster(XElement node, RegisterSettings defaultRegisterSettings, SVDPeripheral peripheral)
            {
                GetMandatoryField(node, "name");
                var address = CalculateOffset(
                    defaultRegisterSettings.Address,
                    GetAddressOffset(node)
                );
                var newRegisterSettings = GetRegisterSettings(node, defaultRegisterSettings, address);
                ScanRegistersAndClusters(node, newRegisterSettings, peripheral);
            }

            private void ScanRegister(XElement node, RegisterSettings defaultRegisterSettings, SVDPeripheral peripheral)
            {
                var address = CalculateOffset(
                    defaultRegisterSettings.Address,
                    GetAddressOffset(node)
                );
                var newRegisterSettings = GetRegisterSettings(node, defaultRegisterSettings, address);
                var name = GetMandatoryField(node, "name");
                var newRegister = new SVDRegister(node, name, newRegisterSettings, peripheral);
                peripheral.Registers.Add(newRegister);
            }

            private static RegisterSettings GetLocalRegisterSettings(XElement node)
            {
                var sizeString = GetOptionalFieldOrNull(node, "size");
                var resetValueString = GetOptionalFieldOrNull(node, "resetValue");
                var accessString = GetOptionalFieldOrNull(node, "access");
                ulong? address = null;
                var nodeName = node.Name.LocalName;
                if(nodeName == NestingType.Peripheral.ToString().ToLower())
                {
                    address = GetBaseAddress(node);
                }
                else if(nodeName == NestingType.Cluster.ToString().ToLower() || nodeName == NestingType.Register.ToString().ToLower())
                {
                    address = GetAddressOffset(node);
                }

                return new RegisterSettings(
                    (int?)SmartParseHexOrDecimal(sizeString, node),
                    (ulong?)SmartParseHexOrDecimal(resetValueString, node),
                    address,
                    GetPermittedAccess(accessString)
                );
            }

            private static RegisterSettings GetRegisterSettings(XElement node, RegisterSettings defaultRegisterSettings, ulong? definiteAddress = null)
            {
                var localRegisterSettings = GetLocalRegisterSettings(node);
                return new RegisterSettings(
                    defaultRegisterSettings,
                    localRegisterSettings.Size,
                    localRegisterSettings.ResetValue,
                    definiteAddress ?? localRegisterSettings.Address,
                    localRegisterSettings.Access
                );
            }

            private static ulong? GetBaseAddress(XElement node)
            {
                var addressOffsetString = GetMandatoryField(node, "baseAddress");
                return SmartParseHexOrDecimal(addressOffsetString, node);
            }

            private static ulong? GetAddressOffset(XElement node)
            {
                var addressOffsetString = GetMandatoryField(node, "addressOffset");
                return SmartParseHexOrDecimal(addressOffsetString, node);
            }

            private static PermittedAccess? GetPermittedAccess(string accessString)
            {
                switch(accessString?.ToLower())
                {
                case null:
                    return null;
                case "read-only":
                    return PermittedAccess.Read;
                case "write-only":
                    return PermittedAccess.Write;
                case "read-write":
                    return PermittedAccess.Read | PermittedAccess.Write;
                case "writeonce":
                    return PermittedAccess.WriteOnce;
                case "read-writeonce":
                    return PermittedAccess.Read | PermittedAccess.WriteOnce;
                default:
                    throw new RecoverableException(string.Format("Found element with unexpected access type: {0}.", accessString));
                }
            }

            private static Endianess GetEndianess(string endianessString)
            {
                switch(endianessString)
                {
                case "little":
                    return Endianess.LittleEndian;
                case "big":
                    return Endianess.BigEndian;
                case "selectable":
                    return Endianess.Selectable;
                case "other":
                    return Endianess.Other;
                default:
                    throw new RecoverableException(string.Format("Found element with unexpected endianess type: {0}.", endianessString));
                }
            }

            private static bool ElementNameEquals(XElement node, string name)
            {
                var nameElement = node.Elements("name").FirstOrDefault();
                if(nameElement != null && nameElement.Value == name)
                {
                    return true;
                }
                return false;
            }

            private XElement deviceNode;
        }

        private class SVDPeripheral
        {
            public SVDPeripheral(string name, SVDDevice includedDevice)
            {
                Name = name;
                Registers = new List<SVDRegister>();
                ParentDevice = includedDevice;
            }

            public string Name { get; private set; }
            public List<SVDRegister> Registers { get; private set; }
            public SVDDevice ParentDevice { get; private set; }
        }

        private class SVDRegister
        {
            public SVDRegister(XElement node, string name, RegisterSettings settings, SVDPeripheral peripheral)
            {
                Name = name;
                path = GetPath(node);
                Peripheral = peripheral;
                Size = settings.Size ?? throw new RecoverableException($"Size not provided for register '{path}'.");
                // Register's size can be not dividable by 8
                SizeInBytes = (uint)Math.Ceiling(Size / 8.0);
                // Reset value assumed to be 0 if not provided. Maximum size of the register cropped to 64 bits.
                ResetValue = (settings.ResetValue & 0xFFFFFFFFFFFFFFFFul) ?? 0u;
                if(Size > 64)
                {
                    ResetValue = 0u;
                    Peripheral.ParentDevice.Parent.currentSystemBus.Log(
                        LogLevel.Warning,
                        "Register {0} size set to {1} bits. Registers larger than 64 bits are not supported. Reset value for this register is set to {2}.",
                        Name,
                        Size,
                        ResetValue
                    );
                }
                Address = settings.Address ?? throw new RecoverableException($"Address not provided for register '{path}'.");
                Access = settings.Access ?? PermittedAccess.Read | PermittedAccess.Write;
            }

            public bool HasReadAccess => (Access & PermittedAccess.Read) != 0;
            public bool HasWriteAccess => (Access & PermittedAccess.Write) != 0;
            public bool HasWriteOnceAccess => (Access & PermittedAccess.WriteOnce) != 0;

            public string Name { get; private set; }
            public int Size { get; private set; }
            public ulong Address { get; private set; }
            public uint SizeInBytes { get; private set; }
            public PermittedAccess Access { get; private set; }
            public SVDPeripheral Peripheral { get; private set; }

            public ulong ResetValue
            {
                get
                {
                    return resetValueWithCorrectEndianess;
                }
                private set
                {
                    var resetValueInLittleEndian = value;
                    // In SVD file it is possible to try set larger value than registers capacity is.
                    var max = (ulong)(1L << Size) - 1;
                    if(resetValueInLittleEndian > max)
                    {
                        var normalizeResetValue = resetValueInLittleEndian & max;
                        Peripheral.ParentDevice.Parent.currentSystemBus.Log(
                            LogLevel.Warning,
                            "The reset value 0x{0:X} does not fit to {1}-bit register '{2}'. Reset value set to 0x{3:X}.",
                            resetValueInLittleEndian,
                            Size,
                            path,
                            normalizeResetValue
                        );
                        resetValueInLittleEndian = normalizeResetValue;
                    }

                    if(Peripheral.ParentDevice.Endianess == Endianess.BigEndian)
                    {
                        var resetValueInBigEndian = 0ul;
                        for(var i = 0; i < SizeInBytes; i++)
                        {
                            var tmp = ((resetValueInLittleEndian >> 8 * i) & 0xFF);
                            resetValueInBigEndian += tmp << (int)(8 * (SizeInBytes - 1 - i));
                        }
                        resetValueWithCorrectEndianess = resetValueInBigEndian;
                    }
                    else
                    {
                        resetValueWithCorrectEndianess = resetValueInLittleEndian;
                    }
                }
            }

            public void MergeWithRegister(SVDRegister register)
            {
                Name = Name + "|" + register.Name;
                Access |= register.Access;
                if(!HasReadAccess && register.HasReadAccess)
                {
                    ResetValue = register.ResetValue;
                }
            }

            private ulong resetValueWithCorrectEndianess;
            private string path;
        }

        private struct RegisterSettings
        {
            public RegisterSettings(int? size, ulong? resetValue, ulong? address, PermittedAccess? access)
            {
                Size = size;
                ResetValue = resetValue;
                Address = address;
                Access = access;
            }

            public RegisterSettings(RegisterSettings parentRegisterSettings, int? size = null, ulong? resetValue = null, ulong? address = null, PermittedAccess? access = null)
            {
                Size = size ?? parentRegisterSettings.Size;
                ResetValue = resetValue ?? parentRegisterSettings.ResetValue;
                Address = address ?? parentRegisterSettings.Address;
                Access = access ?? parentRegisterSettings.Access;
            }

            public int? Size { get; private set; }
            public ulong? ResetValue { get; private set; }
            public ulong? Address { get; private set; }
            public PermittedAccess? Access { get; private set; }
        }

        [Flags]
        private enum PermittedAccess
        {
            Read = 1,
            Write = 2,
            WriteOnce = 4
        }

        private enum NestingType
        {
            Peripheral,
            Cluster,
            Register
        }

        private enum Endianess
        {
            LittleEndian,
            BigEndian,
            Selectable,
            Other
        }

        private enum OperationDirection
        {
            Read,
            Write
        }
    }
}
