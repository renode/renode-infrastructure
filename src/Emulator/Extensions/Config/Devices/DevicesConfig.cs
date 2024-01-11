//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Utilities;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using Dynamitey;
using Microsoft.CSharp.RuntimeBinder;

//HACK!Type.IsPrimitive/IsEnum is used in some test due to the bug in mono 3.2.0.
//It doesn't allow to compare dynamic containing a primitive with null.
//This is fixed at least in 3.4.1. Remove this test when 3.2.0 gets obsolete.
//Oh, and it crashes for nullables too. Doh.

namespace Antmicro.Renode.Config.Devices
{
    public class DevicesConfig
    {
        private readonly Dictionary<string, List<DeviceInfo>> groups = new Dictionary<string, List<DeviceInfo>>();
        private Dictionary<KeyValuePair<string, dynamic>, string> deferred = new Dictionary<KeyValuePair<string, dynamic>, string>();
        private List<DeviceInfo> deviceList = new List<DeviceInfo>();
        private static string DefaultNamespace = "Antmicro.Renode.Peripherals.";

        public List<DeviceInfo> DeviceList{ get { return deviceList; } }

        private static void FailDevice(string deviceName, string field = null, Exception e = null)
        {
            var msg = new StringBuilder(String.Format("Could not create peripheral from node {0}", deviceName));
            if(field != null)
            {
                msg.Append(String.Format(" in section {0}.", field));
            }

            if(e != null)
            {
                msg.Append(" Exception message: ");
                if(!(e is TargetInvocationException))
                {
                    msg.Append(String.Format("{0}. ", e.Message));
                }
                if(e.InnerException != null)
                {
                    msg.Append(String.Format("{0}. ", e.InnerException.Message));
                }
                if(!(e is RecoverableException))
                {
                    throw new InvalidOperationException(msg.ToString());
                }
            }
            throw new RecoverableException(msg.ToString());
        }

        private static Type GetDeviceTypeFromName(string typeName)
        {
            var extendedTypeName = typeName.StartsWith(DefaultNamespace, StringComparison.Ordinal) ? typeName : DefaultNamespace + typeName;

            var manager = TypeManager.Instance;
            return manager.TryGetTypeByName(typeName) ?? manager.TryGetTypeByName(extendedTypeName);
        }

        private bool InitializeDevice(KeyValuePair<string, dynamic> description, string groupName = null)
        {
            if(description.Value is JsonArray)
            {
                if(!groups.ContainsKey(description.Key))
                {
                    groups.Add(description.Key, new List<DeviceInfo>());
                }
                var x = groups[description.Key];

                var any = false;
                foreach(var element in description.Value)
                {
                    var dev = InitializeSingleDevice(new KeyValuePair<string, dynamic>(element.Keys[0], element.Values[0]), description.Key);
                    if(dev != null)
                    {
                        deviceList.Add(dev);
                        x.Add(dev);
                        any = true;
                    }
                }

                return any;
            }
            else if(description.Value is JsonObject)
            {
                var dev = InitializeSingleDevice(description);
                if(dev != null)
                {
                    deviceList.Add(dev);
                    if(groupName != null)
                    {
                        if(!groups.ContainsKey(groupName))
                        {
                            groups.Add(groupName, new List<DeviceInfo>());
                        }
                        groups[groupName].Add(dev);
                    }
                    return true;
                }
            }
            else
            {
                FailDevice(description.Key);
            }

            return false;
        }

        /// Required/possible nodes:
        /// _type
        /// _irq/_gpio - optional
        /// _connection - optional?
        /// ctorParam
        /// PropertyWithSetter
        private DeviceInfo InitializeSingleDevice(KeyValuePair<string, dynamic> device, string groupName = null)
        {
            var info = new DeviceInfo();
            info.Name = device.Key;
            var devContent = device.Value;
            if(devContent == null)
            {
                FailDevice(info.Name);
            }

            //Type
            if(!devContent.ContainsKey(TYPE_NODE))
            {
                FailDevice(info.Name, TYPE_NODE);
            }
            var typeName = (string)devContent[TYPE_NODE];

            var devType = GetDeviceTypeFromName(typeName);
            if(devType == null)
            {
                FailDevice(info.Name, TYPE_NODE);
            }

            object peripheral;
            //Constructor
            if(!TryInitializeCtor(devType, devContent, out peripheral))
            {
                FailDevice(info.Name, "constructor_invoke");
            }
            if(peripheral == null)
            {
                // special case when construction of the object has been deferred
                deferred.Add(device, groupName);
                return null;
            }
            devContent.Remove(TYPE_NODE);

            info.Peripheral = (IPeripheral)peripheral;

            //Properties
            try
            {
                InitializeProperties(info.Peripheral, devContent);
            }
            catch(InvalidOperationException e)
            {
                FailDevice(info.Name, e.Message, e.InnerException);
            }

            //GPIOs
            if(devContent.ContainsKey(IRQ_NODE))
            {
                info.AddIrq(IRQ_NODE, devContent[IRQ_NODE]);
                devContent.Remove(IRQ_NODE);
            }
            else if(devContent.ContainsKey(GPIO_NODE))
            {
                info.AddIrq(GPIO_NODE, devContent[GPIO_NODE]);
                devContent.Remove(GPIO_NODE);
            }

            //IRQs From
            if(devContent.ContainsKey(IRQ_FROM_NODE))
            {
                info.AddIrqFrom(IRQ_FROM_NODE, devContent[IRQ_FROM_NODE]);
                devContent.Remove(IRQ_FROM_NODE);
            }
            else if(devContent.ContainsKey(GPIO_FROM_NODE))
            {
                info.AddIrqFrom(GPIO_FROM_NODE, devContent[GPIO_FROM_NODE]);
                devContent.Remove(GPIO_FROM_NODE);
            }

            //Connections
            if(devContent.ContainsKey(CONNECTION_NODE))
            {
                InitializeConnections(info, devContent[CONNECTION_NODE]);
                devContent.Remove(CONNECTION_NODE);
            }

            return info;
        }

        private void InitializeGPIOsFrom(DeviceInfo device)
        {
            foreach(var nodeName in device.IrqsFrom.Keys)
            {
                var gpioReceiver = device.Peripheral as IGPIOReceiver;
                if(gpioReceiver == null)
                {
                    FailDevice(device.Name, nodeName);
                }

                var irqs = device.IrqsFrom[nodeName];
                if(irqs == null)
                {
                    FailDevice(device.Name, nodeName);
                }

                foreach(var source in irqs.Keys)
                {
                    var sourceIrqs = irqs[source] as List<dynamic>;
                    if(sourceIrqs == null)
                    {
                        FailDevice(device.Name, nodeName + ": " + source);
                    }

                    IPeripheral sourcePeripheral;

                    var fromList = deviceList.SingleOrDefault(x => x.Name == source);

                    if(fromList != null)
                    {
                        sourcePeripheral = (IGPIOReceiver)fromList.Peripheral;
                    }
                    else if(!machine.TryGetByName<IPeripheral>(source, out sourcePeripheral))
                    {
                        FailDevice(device.Name, nodeName + ": " + source);
                    }
                    if(sourcePeripheral is ILocalGPIOReceiver && source.Length == 2)
                    {
                        sourcePeripheral = ((ILocalGPIOReceiver)sourcePeripheral).GetLocalReceiver(int.Parse(source[1]));
                    }

                    var props = sourcePeripheral.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    var connectors = props
                        .Where(x => typeof(GPIO).IsAssignableFrom(x.PropertyType)).ToArray();
                    PropertyInfo defaultConnector = null;
                    if(connectors.Count() == 1)
                    {
                        defaultConnector = connectors.First();
                    }

                    try
                    {
                        if(sourceIrqs.All(x => x is JsonArray))
                        {
                            foreach(var irqEntry in sourceIrqs)
                            {
                                InitializeGPIO(sourcePeripheral, device.Name, gpioReceiver, irqEntry.ToDynamic(), defaultConnector);
                            }
                        }
                        else
                        {
                            InitializeGPIO(sourcePeripheral, device.Name, gpioReceiver, ((JsonArray)sourceIrqs).ToDynamic(), defaultConnector);
                        }
                    }
                    catch(ArgumentException)
                    {
                        FailDevice(device.Name, nodeName + ": " + source);
                    }
                }
            }
        }

        private void InitializeGPIOs(DeviceInfo device)
        {
            foreach(var nodeName in device.Irqs.Keys)
            {
                var irqs = device.Irqs[nodeName];
                if(irqs == null)
                {
                    FailDevice(device.Name, nodeName);
                }
                var props = device.Peripheral.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var connectors = props
                .Where(x => typeof(GPIO).IsAssignableFrom(x.PropertyType)).ToArray();
                PropertyInfo defaultConnector = null;
                if(connectors.Count() == 1)
                {
                    defaultConnector = connectors.First();
                }

                foreach(var controller in irqs.Keys)
                {
                    var controllerIrqs = irqs[controller] as List<dynamic>;
                    if(controllerIrqs == null)
                    {
                        FailDevice(device.Name, nodeName + ": " + controller);
                    }

                    var controllerElements = controller.Split('#');
                    if(controllerElements.Length > 2)
                    {
                        FailDevice(device.Name, nodeName + ": " + controller);
                    }

                    IGPIOReceiver receiver;

                    var fromList = deviceList.SingleOrDefault(x => x.Name == controllerElements[0]);

                    if(fromList != null && fromList.Peripheral is IGPIOReceiver)
                    {
                        receiver = (IGPIOReceiver)fromList.Peripheral;
                    }
                    else if(!machine.TryGetByName<IGPIOReceiver>(controllerElements[0], out receiver))
                    {
                        FailDevice(device.Name, nodeName + ": " + controller);
                    }
                    if(receiver is ILocalGPIOReceiver && controllerElements.Length == 2)
                    {
                        receiver = ((ILocalGPIOReceiver)receiver).GetLocalReceiver(int.Parse(controllerElements[1]));
                    }

                    try
                    {
                        if(controllerIrqs.All(x => x is JsonArray))
                        {
                            foreach(var irqEntry in controllerIrqs)
                            {
                                InitializeGPIO(device.Peripheral, device.Name, receiver, irqEntry.ToDynamic(), defaultConnector);
                            }
                        }
                        else
                        {
                            InitializeGPIO(device.Peripheral, device.Name, receiver, ((JsonArray)controllerIrqs).ToDynamic(), defaultConnector);
                        }
                    }
                    catch(ArgumentException)
                    {
                        FailDevice(device.Name, nodeName + ": " + controller);
                    }
                }
            }
        }

        //[source,dest] or [dest] with non-null defaultConnector
        void InitializeGPIO(IPeripheral device, string deviceName, IGPIOReceiver receiver, IList<int> irqEntry, PropertyInfo defaultConnector)
        {
            var periByNumber = device as INumberedGPIOOutput;
            if(irqEntry.Count == 2 && periByNumber != null)
            {
                periByNumber.Connections[irqEntry[0]].Connect(receiver, irqEntry[1]);
            }
            else if(irqEntry.Count == 1 && defaultConnector != null)
            {
                var gpioField = defaultConnector.GetValue(device, null) as GPIO;
                if(gpioField == null)
                {
                    FailDevice(deviceName);
                }
                gpioField.Connect(receiver, irqEntry[0]);
            }
            else
            {
                throw new ArgumentException();
            }
        }

        void InitializeGPIO(IPeripheral device, string deviceName, IGPIOReceiver receiver, IList<object> irqEntry, PropertyInfo defaultConnector)
        {
            if(!(irqEntry[0] is string && irqEntry[1] is int))
            {
                throw new ArgumentException();
            }
            //May throw AmbiguousMatchException - then use BindingFlags.DeclaredOnly or sth
            var connector = device.GetType().GetProperty(irqEntry[0] as string);
            if(connector == null)
            {
                throw new ArgumentException();
            }
            var gpio = connector.GetValue(device, null) as GPIO;
            if(gpio == null)
            {
                FailDevice(deviceName);
            }
            gpio.Connect(receiver, (int)irqEntry[1]);

        }

        private bool IsShortNotation(IPeripheral peripheral, PropertyInfo defaultConnector, IList<dynamic> entry)
        {
            return (peripheral is INumberedGPIOOutput && entry.Count == 2 && entry.All(x => x is int))
            || (defaultConnector != null && entry.Count == 1 && entry[0] is int)
            || (entry.Count == 2 && entry[0] is String && entry[1] is int);
        }

        private static void InitializeConnections(DeviceInfo device, IDictionary<string, dynamic> connections)
        {
            if(connections == null)
            {
                FailDevice(device.Name, CONNECTION_NODE);
            }
            foreach(var container in connections.Keys)
            {
                var conDict = connections[container];
                device.AddConnection(container, conDict);
            }
        }

        private static void InitializeConnections(DeviceInfo device, string connection)
        {
            if(string.IsNullOrWhiteSpace(connection))
            {
                FailDevice(device.Name, CONNECTION_NODE);
            }
            device.AddConnection(connection);
        }

        private void InitializeProperties(object device, IDictionary<string, dynamic> node)
        {
            foreach(var item in node.Keys.Where(x=>Char.IsUpper(x,0)))
            {
                var value = node[item];
                try
                {
                    Dynamic.InvokeSet(device, item, value);
                }
                catch(Exception e)
                {
                    throw new RecoverableException(item, e);
                }
            }
        }

        public DevicesConfig(string text, IMachine machine)
        {
            try
            {
                var devices = SimpleJson.DeserializeObject<dynamic>(text);
                this.machine = machine;
                //Every main node is one peripheral/device
                foreach(var dev in devices)
                {
                    InitializeDevice(dev);
                }

                while(deferred.Count > 0)
                {
                    var lastCount = deferred.Count;
                    foreach(var deferredDevice in deferred.ToList())
                    {
                        if(InitializeDevice(deferredDevice.Key, deferredDevice.Value))
                        {
                            deferred.Remove(deferredDevice.Key);
                        }
                    }

                    if(lastCount == deferred.Count)
                    {
                        throw new ConstructionException("The provided configuration is not consistent. Some devices could not have been created due to wrong references.");
                    }
                }

                //Initialize connections
                while(deviceList.Any(x => !x.IsRegistered))
                {
                    var anyChange = false;
                    //setup connections
                    foreach(var periConn in deviceList.Where(x=> !x.IsRegistered && x.HasConnections))
                    {
                        var parents = new Dictionary<string, IPeripheral>();
                        foreach(var conn in periConn.Connections.Select(x=>x.Key))
                        {
                            var fromList = deviceList.SingleOrDefault(x => x.Name == conn);
                            if(fromList != null)
                            {
                                parents.Add(conn, fromList.Peripheral);
                            }
                            else
                            {
                                IPeripheral candidate;
                                if(!machine.TryGetByName(conn, out candidate))
                                {
                                    FailDevice(periConn.Name, "connection to " + conn, null);
                                }
                                parents.Add(conn, candidate);
                            }
                        }

                        var canBeRegistered = parents.All(x => machine.IsRegistered(x.Value));
                        if(canBeRegistered)
                        {
                            RegisterInParents(periConn, parents);
                            periConn.IsRegistered = true;
                            anyChange = true;
                        }
                    }
                    if(!anyChange)
                    {
                        var invalidDevices = deviceList.Where(x => !x.IsRegistered).Select(x => x.Name).Aggregate((x, y) => x + ", " + y);
                        throw new RegistrationException("The " +
                        "provided configuration is not consistent. The following devices could not have been registered: "
                        + invalidDevices
                        );
                    }
                }

                foreach(var device in deviceList.Where(x=>x.Irqs.Any()))
                {
                    InitializeGPIOs(device);
                }

                foreach(var device in deviceList.Where(x=>x.IrqsFrom.Any()))
                {
                    InitializeGPIOsFrom(device);
                }
            }
            catch(SerializationException e)
            {
                throw new RecoverableException("Invalid JSON string.", e);
            }
            catch(RuntimeBinderException e)
            {
                throw new RecoverableException("The config file could not be analyzed. You should reset your current emulation.", e);
            }

            foreach(var group in groups)
            {
                machine.PeripheralsGroups.GetOrCreate(group.Key, group.Value.Select(x => x.Peripheral));
            }

            foreach(var device in deviceList)
            {
                machine.SetLocalName(device.Peripheral, device.Name);
            }
        }

        public static IEnumerable<DeviceInfo> GetShortInfo(string filename)
        {
            var devices = ((JsonObject)SimpleJson.DeserializeObject<dynamic>(File.ReadAllText(filename))).ToList();
            // flattening peripheral groups
            var arrays = devices.Where(d => d.Value is JsonArray).ToList();
            arrays.ForEach(a => devices.Remove(a));
            arrays.Select(a => a.Value).Cast<JsonArray>().SelectMany(y => y).Cast<JsonObject>().ToList().ForEach(x => devices.AddRange(x));

            foreach(var device in devices)
            {
                var info = new DeviceInfo();
                info.Name = device.Key;
                dynamic devContent = device.Value;
                if(devContent == null)
                {
                    FailDevice(device.Key);
                }

                if(!devContent.ContainsKey(TYPE_NODE))
                {
                    FailDevice(device.Key, TYPE_NODE);
                }
                var typeName = (string)devContent[TYPE_NODE];

                var devType = GetDeviceTypeFromName(typeName);
                if(devType == null)
                {
                    FailDevice(device.Key, TYPE_NODE);
                }
                info.Type = devType;

                if(devContent.ContainsKey(CONNECTION_NODE))
                {
                    InitializeConnections(info, devContent[CONNECTION_NODE]);
                }
                yield return info;
            }
        }

        private void RegisterInParents(DeviceInfo device, IDictionary<string, IPeripheral> parents)
        {
            foreach(var parentName in device.Connections.Keys)
            {
                //TODO: nongeneric version
                var parent = parents.Single(x => x.Key == parentName).Value;
                var connections = device.Connections[parentName];
                var ifaces = parent.GetType().GetInterfaces().Where(x => IsSpecializationOfRawGeneric(typeof(IPeripheralRegister<,>), x)).ToList();
                var ifaceCandidates = ifaces
                    .Where(x => x.GetGenericArguments()[0].IsAssignableFrom(device.Peripheral.GetType()))
                    .OrderBy(x => typeof(NullRegistrationPoint).IsAssignableFrom(x.GetGenericArguments()[1])) // Use the null registration point last
                    .ToList();
                foreach(var connection in connections)
                {
                    IRegistrationPoint regPoint = null;
                    Type formalType = null;
                    if(connection.ContainsKey(TYPE_NODE))
                    {
                        var name = (string)connection[TYPE_NODE];
                        formalType = GetDeviceTypeFromName(name);
                    }

                    Type foundIface = null;
                    foreach(var iface in ifaceCandidates)
                    {
                        var iRegPoint = iface.GetGenericArguments()[1];
                        Type objType;
                        if(formalType != null && iRegPoint.IsAssignableFrom(formalType))
                        {
                            objType = formalType;
                        }
                        else
                        {
                            objType = iRegPoint;
                        }

                        object regPointObject;
                        if(!TryInitializeCtor(objType, connection, out regPointObject))
                        {
                            if(connection.Keys.Any() || !TryHandleSingleton(objType, out regPointObject))
                            {
                                continue;
                            }
                        }
                        regPoint = (IRegistrationPoint)regPointObject;
                        foundIface = iface;
                        break;
                        //is a construable type
                    }
                    if(foundIface == null)
                    {
                        // let's try attachment through the AttachTo mechanism
                        FailDevice(device.Name, "connection to " + parentName);
                    }
                    else
                    {
                        Dynamic.InvokeMemberAction(parent, "Register", new object[] {
                            device.Peripheral,
                            regPoint
                        }
                        );
                    }
                }
            }
        }

        private static bool IsSpecializationOfRawGeneric(Type generic, Type toCheck)
        {
            var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
            if(generic == cur)
            {
                return true;
            }

            return false;
        }

        private static bool TryHandleSingleton(Type type, out object instance)
        {
            var properties = type.GetProperties();
            var desiredProperty = properties.FirstOrDefault(x => x.Name == "Instance" && x.PropertyType == type);
            if(desiredProperty == null)
            {
                instance = null;
                return false;
            }
            // We have to use reflection-based approach because of a bug in Mono 5.2:
            // https://bugzilla.xamarin.com/show_bug.cgi?id=58455
            //
            //instance = Dynamic.InvokeGet(InvokeContext.CreateStatic(type), desiredProperty.Name);
            instance = desiredProperty.GetGetMethod().Invoke(null, Type.EmptyTypes);
            return true;
        }

        private bool TryInitializeCtor(Type devType, IDictionary<string,dynamic> node, out object constructedObject)
        {
            //Find best suitable constructor, sort parameter list and create instance. Constructor parameters begin with [a-z]
            var constructors = FindSuitableConstructors(devType, node.Where(x => Char.IsLower(x.Key, 0)).Select(x => x.Key));
            if(constructors.Count != 1)
            {
                constructedObject = null;
                return false;
            }

            var ctor = constructors[0];
            var sortedParams = new Dictionary<string, object>();
            foreach(var ctorParam in ctor.GetParameters())
            {
                if(node.ContainsKey(ctorParam.Name))
                {
                    if(typeof(IPeripheral).IsAssignableFrom(ctorParam.ParameterType) && !typeof(IMachine).IsAssignableFrom(ctorParam.ParameterType) && !ctorParam.ParameterType.IsArray)
                    {
                        var info = deviceList.SingleOrDefault(di => di.Name == node[ctorParam.Name]);
                        if(info != null)
                        {
                            sortedParams.Add(ctorParam.Name, info.Peripheral);
                        }
                        else
                        {
                            // required peripheral is not yet created, so we need to defer the construction
                            constructedObject = null;
                            return true;
                        }
                    }
                    else
                    {
                        var temp = GenerateObject(node[ctorParam.Name], ctorParam.ParameterType);
                        //HACK: The reason of the following line is described at the top of this class.
                        if(ctorParam.ParameterType.IsPrimitive || ctorParam.ParameterType.IsEnum || Nullable.GetUnderlyingType(ctorParam.ParameterType) != null || temp != null)
                        {
                            sortedParams.Add(ctorParam.Name, temp);
                        }
                        else
                        {
                            // required peripheral is not yet created, so we need to defer the construction
                            constructedObject = null;
                            return true;
                        }
                    }
                }
                else if(typeof(IMachine).IsAssignableFrom(ctorParam.ParameterType))
                {
                    sortedParams.Add(ctorParam.Name, machine);
                }
                else
                {
                    sortedParams.Add(ctorParam.Name, ctorParam.DefaultValue);
                }
            }
            var paramsArray = sortedParams.Values.ToArray();
            try
            {
                constructedObject = Dynamic.InvokeConstructor(devType, paramsArray);
            }
            catch(ConstructionException)
            {
                constructedObject = null;
                return false;
            }
            catch(Exception e)
            {
                throw new ConstructionException(String.Format("Could not create object of type {0}.", devType.Name), e);
            }
            return true;
        }

        private dynamic GenerateObject(dynamic value, Type type)
        {
            var stringValue = value as string;
            if(type.IsEnum && stringValue != null)
            {
                object[] parameters = new object[2];
                parameters[0] = stringValue;
                var parseResult = typeof(Enum).GetMethods().First(x=>x.Name == "TryParse" && x.GetParameters().Length == 2).MakeGenericMethod(type)
                    .Invoke(null,  parameters);
                if((bool)parseResult)
                {
                    return parameters[1];
                }
            }
            return Dynamic.InvokeConvert(value, type, true);
        }

        private dynamic GenerateObject(IDictionary<string, dynamic> value, Type type)
        {
            object obj;
            if(!TryInitializeCtor(type, value, out obj))
            {
                throw new ConstructionException("Could not create object " + value);
            }
            return obj;
        }

        private dynamic GenerateObject(IList<dynamic> value, Type type)
        {
            Type innerType = typeof(object);
            var isArray = type.IsArray;
            if(type.IsGenericType)
            {
                innerType = type.GetGenericArguments()[0];
            }
            else if(isArray)
            {
                innerType = type.GetElementType();
            }

            var list = isArray ? Dynamic.InvokeConstructor(typeof(List<>).MakeGenericType(innerType)) : Dynamic.InvokeConstructor(type);
            foreach(var item in value)
            {
                var obj = GenerateObject(item, innerType);
                //HACK: The reason of the following line is described at the top of this class.
                if(!innerType.IsPrimitive && !innerType.IsEnum && Nullable.GetUnderlyingType(innerType) == null && obj == null)
                {
                    return null;
                }
                list.Add(obj);
            }
            if(isArray)
            {
                return list.ToArray();
            }
            return list;
        }

        public IList<ConstructorInfo> FindSuitableConstructors(Type type, IEnumerable<string> parameters)
        {
            var goodCtors = new List<ConstructorInfo>();
            foreach(var ctor in type.GetConstructors())
            {
                var unusableFound = false;
                // every parameter in 'parameters' must be present in the constructor
                var ctorParams = ctor.GetParameters();
                if(!parameters.All(x => ctorParams.FirstOrDefault(y => y.Name == x) != null))
                {
                    continue;
                }

                // every argument in ctor must either be present in 'parameters' or set to default
                foreach(var param in ctorParams)
                {
                    if(!parameters.Contains(param.Name))
                    {

                        if(!param.IsOptional && !typeof(IMachine).IsAssignableFrom(param.ParameterType))
                        {
                            unusableFound = true;
                        }
                    }
                }
                if(unusableFound)
                {
                    continue;
                }
                goodCtors.Add(ctor);
            }
            return goodCtors;
        }

        private readonly IMachine machine;

        private const string TYPE_NODE = "_type";
        private const string IRQ_NODE = "_irq";
        private const string GPIO_NODE = "_gpio";
        private const string IRQ_FROM_NODE = "_irqFrom";
        private const string GPIO_FROM_NODE = "_gpioFrom";
        private const string CONNECTION_NODE = "_connection";
    }
}

