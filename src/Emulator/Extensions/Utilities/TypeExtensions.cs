//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Reflection;
using System.Runtime.CompilerServices;
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Microsoft.CSharp.RuntimeBinder;
using System.Collections.Generic;
using Dynamitey;
using Antmicro.Renode.Time;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Utilities
{
    public static class TypeExtensions
    {
        public static bool IsCallable(this PropertyInfo info)
        {
            return IsTypeConvertible(info.PropertyType) && info.GetIndexParameters().Length == 0 && info.IsBaseCallable(); //disallow indexers
        }

        public static bool IsCallableIndexer(this PropertyInfo info)
        {
            return IsTypeConvertible(info.PropertyType) && info.GetIndexParameters().Length != 0 && info.IsBaseCallable(); //only indexers
        }

        public static bool IsCallable(this FieldInfo info)
        {
            return IsTypeConvertible(info.FieldType) && info.IsBaseCallable();
        }

        public static bool IsCallable(this MethodInfo info)
        {
            return info.GetParameters().All(x => !x.IsOut && IsTypeConvertible(x.ParameterType) || x.IsOptional) && info.IsBaseCallable();
        }

        public static bool IsExtensionCallable(this MethodInfo info)
        {
            return !info.IsGenericMethod && info.GetParameters().Skip(1).All(x => !x.IsOut && IsTypeConvertible(x.ParameterType) || x.IsOptional) && info.IsBaseCallable();
        }

        private static bool IsBaseCallable(this MemberInfo info)
        {
            return !info.IsDefined(typeof(HideInMonitorAttribute));
        }

        public static bool IsStatic(this MemberInfo info)
        {
            var eventInfo = info as EventInfo;
            var fieldInfo = info as FieldInfo;
            var methodInfo = info as MethodInfo;
            var propertyInfo = info as PropertyInfo;
            var type = info as Type;

            if(eventInfo != null)
            {
                var addMethod = eventInfo.GetAddMethod(true);
                if(addMethod != null)
                {
                    return addMethod.IsStatic;
                }
                var rmMethod = eventInfo.GetRemoveMethod(true);
                if(rmMethod != null)
                {
                    return rmMethod.IsStatic;
                }
                throw new ArgumentException(String.Format("Unhandled type of event: {0} in {1}.", eventInfo.Name, eventInfo.DeclaringType));
            }

            if(fieldInfo != null)
            {
                return (fieldInfo.Attributes & FieldAttributes.Static) != 0;
            }

            if(methodInfo != null)
            {
                return methodInfo.IsStatic;
            }

            if(propertyInfo != null)
            {
                var getMethod = propertyInfo.GetGetMethod(true);
                if(getMethod != null)
                {
                    return getMethod.IsStatic;
                }
                var setMethod = propertyInfo.GetSetMethod(true);
                if(setMethod != null)
                {
                    return setMethod.IsStatic;
                }
                throw new ArgumentException(String.Format("Unhandled type of property: {0} in {1}.", propertyInfo.Name, propertyInfo.DeclaringType));
            }

            if(type != null)
            {
                return type.IsAbstract && type.IsSealed;
            }
            throw new ArgumentException(String.Format("Unhandled type of MemberInfo: {0} in {1}.", info.Name, info.DeclaringType));
        }

        public static bool IsCurrentlyGettable(this PropertyInfo info, BindingFlags flags)
        {
            return info.CanRead && info.GetGetMethod((flags & BindingFlags.NonPublic) > 0) != null;
        }

        public static bool IsCurrentlySettable(this PropertyInfo info, BindingFlags flags)
        {
            return info.CanWrite && info.GetSetMethod((flags & BindingFlags.NonPublic) > 0) != null;
        }

        public static bool IsExtension(this MethodInfo info)
        {
            return info.IsDefined(typeof(ExtensionAttribute), true);
        }

        private static Type GetEnumerableType(Type type)
        {
            var ifaces = type.GetInterfaces();
            if(ifaces.Length == 0)
            {
                return null;
            }
            var iface = ifaces.FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if(iface == null)
            {
                return null;
            }
            return iface.GetGenericArguments()[0];
        }

        private static Type[] convertibleTypes = {
            typeof(IConnectable),
            typeof(IConvertible),
            typeof(IPeripheral),
            typeof(IExternal),
            typeof(IEmulationElement),
            typeof(IClockSource),
            typeof(Range),
            typeof(TimeSpan),
            typeof(TimerResult)
        };
        private static bool IsTypeConvertible(Type type)
        {
            var underlyingType = GetEnumerableType(type);
            if(underlyingType != null)
            {
                return IsTypeConvertible(underlyingType);
            }
            if(type.IsEnum || type.IsDefined(typeof(ConvertibleAttribute), true)
                || convertibleTypes.Any(x => x.IsAssignableFrom(type)))
            {
                return true;
            }
            if(type.IsByRef || type == typeof(object)) //these are always wrong
            {
                return false;
            }
            try     //try with a number
            {
                Dynamic.InvokeConvert(1, type, false);
                return true;
            }
            catch(RuntimeBinderException)   //because every conversion operator may throw anything
            {
            }
            try    //try with a string
            {
                Dynamic.InvokeConvert("a", type, false);
                return true;
            }
            catch(RuntimeBinderException)
            {
            }
            try    //try with a character
            {
                Dynamic.InvokeConvert('a', type, false);
                return true;
            }
            catch(RuntimeBinderException)
            {
            }
            try    //try with a bool
            {
                Dynamic.InvokeConvert(true, type, false);
                return true;
            }
            catch(RuntimeBinderException)
            {
            }
            return false;
        }
    }
}

