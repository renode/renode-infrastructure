//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Antmicro.Renode.Utilities;

using Dynamitey;

namespace Antmicro.Renode.UserInterface
{
    public static class DeviceHelper
    {
        public static object InvokeGetIndex(object device, PropertyInfo property, List<object> parameters)
        {
            var context = CreateInvocationContext(device, property);
            if(context != null)
            {
                return Dynamic.InvokeGetIndex(context, parameters.ToArray());
            }
            else
            {
                throw new NotImplementedException($"Unsupported field {property.Name} in InvokeGetIndex");
            }
        }

        public static void InvokeSetIndex(object device, PropertyInfo property, List<object> parameters)
        {
            var context = CreateInvocationContext(device, property);
            if(context != null)
            {
                Dynamic.InvokeSetIndex(context, parameters.ToArray());
            }
            else
            {
                throw new NotImplementedException($"Unsupported field {property.Name} in InvokeSetIndex");
            }
        }

        public static object InvokeMethod(object device, MethodInfo method, List<object> parameters)
        {
            var context = CreateInvocationContext(device, method);
            if(context != null)
            {
                return InvokeWithContext(context, method, parameters.ToArray());
            }
            else
            {
                throw new NotImplementedException($"Unsupported field {method.Name} in InvokeMethod");
            }
        }

        public static void InvokeSet(object device, MemberInfo info, object parameter)
        {
            var context = CreateInvocationContext(device, info);
            if(context != null)
            {
                Dynamic.InvokeSet(context, info.Name, parameter);
            }
            else
            {
                var propInfo = info as PropertyInfo;
                var fieldInfo = info as FieldInfo;
                if(fieldInfo != null)
                {
                    fieldInfo.SetValue(null, parameter);
                    return;
                }
                if(propInfo != null)
                {
                    propInfo.SetValue(!propInfo.IsStatic() ? device : null, parameter, null);
                    return;
                }
                throw new NotImplementedException($"Unsupported field {info.Name} in InvokeSet");
            }
        }

        public static object InvokeExtensionMethod(object device, MethodInfo method, List<object> parameters)
        {
            var context = InvokeContext.CreateStatic(method.ReflectedType);
            if(context != null)
            {
                return InvokeWithContext(context, method, (new[] { device }.Concat(parameters)).ToArray());
            }
            else
            {
                throw new NotImplementedException($"Unsupported field {method.Name} in InvokeExtensionMethod");
            }
        }

        public static object InvokeGet(object device, MemberInfo info)
        {
            var context = CreateInvocationContext(device, info);
            if(context != null)
            {
                return Dynamic.InvokeGet(context, info.Name);
            }
            else
            {
                var propInfo = info as PropertyInfo;
                var fieldInfo = info as FieldInfo;
                if(fieldInfo != null)
                {
                    return fieldInfo.GetValue(null);
                }
                if(propInfo != null)
                {
                    return propInfo.GetValue(!propInfo.IsStatic() ? device : null, null);
                }
                throw new NotImplementedException($"Unsupported field {info.Name} in InvokeGet");
            }
        }

        private static object InvokeWithContext(InvokeContext context, MethodInfo method, object[] parameters)
        {
            if(method.ReturnType == typeof(void))
            {
                Dynamic.InvokeMemberAction(context, method.Name, parameters);
                return null;
            }
            else
            {
                return Dynamic.InvokeMember(context, method.Name, parameters);
            }
        }

        /// <summary>
        /// Creates the invocation context.
        /// </summary>
        /// <returns>The invokation context or null, if can't be handled by Dynamitey.</returns>
        /// <param name="device">Target device.</param>
        /// <param name="info">Field, property or method info.</param>
        private static InvokeContext CreateInvocationContext(object device, MemberInfo info)
        {
            if(info.IsStatic())
            {
                if(info is FieldInfo || info is PropertyInfo)
                {
                    //FieldInfo not supported in Dynamitey
                    return null;
                }
                return InvokeContext.CreateStatic(device.GetType());
            }
            var propertyInfo = info as PropertyInfo;
            if(propertyInfo != null)
            {
                //private properties not supported in Dynamitey
                if((propertyInfo.CanRead && propertyInfo.GetGetMethod(true).IsPrivate)
                   || (propertyInfo.CanWrite && propertyInfo.GetSetMethod(true).IsPrivate))
                {
                    return null;
                }
            }
            return InvokeContext.CreateContext(device, info.ReflectedType);
        }
    }
}
