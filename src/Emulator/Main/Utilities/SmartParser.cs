//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Antmicro.Renode.Utilities
{
    public class SmartParser
    {
        public static SmartParser Instance = new SmartParser();

        public bool TryParse(string input, Type outputType, out object result)
        {
            if(outputType == typeof(string))
            {
                result = input;
                return true;
            }
            var underlyingType = Nullable.GetUnderlyingType(outputType);
            if(underlyingType != null && TryParse(input, underlyingType, out var res))
            {
                result = res;
                return true;
            }
            if(outputType.IsEnum)
            {
                if(Enum.GetNames(outputType).Contains(input, StringComparer.Ordinal))
                {
                    result = Enum.Parse(outputType, input, false);
                    return true;
                }
                if(Int32.TryParse(input, out var number))
                {
                    if(outputType.GetCustomAttributes<AllowAnyNumericalValue>().Any())
                    {
                        result = Enum.Parse(outputType, input, false);
                        return true;
                    }
                    // We cannot use Enum.IsDefined here, because it requires us to provide the number cast to the enum's underlying type, which we do not know.
                    // Therefore, we use the loop below.
                    foreach(var enumValue in Enum.GetValues(outputType))
                    {
                        if(Convert.ToInt32(enumValue) == number)
                        {
                            result = Enum.Parse(outputType, input, false);
                            return true;
                        }
                    }
                }
                result = null;
                return false;
            }

            Delegate parser;
            if(input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                input = input.Substring(2);
                parser = GetFromCacheOrAdd(
                    hexCache,
                    outputType,
                    () =>
                {
                    TryGetParseMethodDelegate(outputType, new[] { typeof(string), typeof(NumberStyles), typeof(CultureInfo) }, new object[] { NumberStyles.HexNumber, CultureInfo.InvariantCulture }, out Delegate del);
                    return del;
                }
                );
            }
            else if(input.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            {
                input = input.Substring(2);
                parser = GetFromCacheOrAdd(
                    binaryCache,
                    outputType,
                    () =>
                {
                    TryGetConvertMethodDelegate(outputType, 2, out Delegate del);
                    return del;
                }
                );
            }
            else if(GetDefault(outputType) == null && (input == "null" || input == ""))
            {
                result = null;
                return true;
            }
            else
            {
                parser = GetFromCacheOrAdd(
                    cache,
                    outputType,
                    () =>
                {
                    if(!TryGetParseMethodDelegate(outputType, new[] { typeof(string), typeof(CultureInfo) }, new object[] { CultureInfo.InvariantCulture }, out Delegate del)
                       && !TryGetParseMethodDelegate(outputType, new[] { typeof(string) }, new object[0], out del))
                    {
                        del = null;
                    }
                    return del;
                }
                );
            }
            try
            {
                result = parser.DynamicInvoke(input);
                return true;
            }
            catch(TargetInvocationException)
            {
                result = null;
                return false;
            }
        }

        private static object GetDefault(Type type)
        {
            if(type.IsValueType)
            {
                // this will handle the nullable case
                return Activator.CreateInstance(type);
            }
            return null;
        }

        private static bool TryGetConvertMethodDelegate(Type type, int fromBase, out Delegate result)
        {
            var parameters = new [] { typeof(string), typeof(int) };
            var method = typeof(Convert).GetMethod($"To{type.Name}", parameters);

            if(method == null)
            {
                result = null;
                return false;
            }

            var delegateType = Expression.GetDelegateType(parameters.Concat(new[] { method.ReturnType }).ToArray());
            var methodDelegate = method.CreateDelegate(delegateType);
            result = (Func<string, object>)(i => methodDelegate.DynamicInvoke(new object[] { i, fromBase }));

            return true;
        }

        private static bool TryGetParseMethodDelegate(Type type, Type[] parameters, object[] additionalParameters, out Delegate result)
        {
            var method = type.GetMethod("Parse", parameters);
            if(method == null)
            {
                result = null;
                return false;
            }

            var delegateType = Expression.GetDelegateType(parameters.Concat(new[] { method.ReturnType }).ToArray());
            var methodDelegate = method.CreateDelegate(delegateType);
            result = additionalParameters.Length > 0 ? (Func<string, object>)(i => methodDelegate.DynamicInvoke(new object[] { i }.Concat(additionalParameters).ToArray())) : (Func<string, object>)(i => methodDelegate.DynamicInvoke(i));

            return true;
        }

        private SmartParser()
        {
            cache = new Dictionary<Type, Delegate>();
            hexCache = new Dictionary<Type, Delegate>();
            binaryCache = new Dictionary<Type, Delegate>();
            sync = new object();
        }

        private Delegate GetFromCacheOrAdd(Dictionary<Type, Delegate> cacheDict, Type outputType, Func<Delegate> function)
        {
            lock(sync)
            {
                if(!cacheDict.TryGetValue(outputType, out Delegate parser))
                {
                    parser = function();
                    if(parser == null)
                    {
                        throw new ArgumentException(string.Format("Type \"{0}\" does not have a \"Parse\" method with the requested parameters", outputType.Name));
                    }
                    cacheDict.Add(outputType, parser);
                }
                return parser;
            }
        }

        private readonly Dictionary<Type, Delegate> cache;
        private readonly Dictionary<Type, Delegate> hexCache;
        private readonly Dictionary<Type, Delegate> binaryCache;
        private readonly object sync;
    }
}

