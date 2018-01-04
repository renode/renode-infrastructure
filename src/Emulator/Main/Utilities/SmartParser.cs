//
// Copyright (c) 2010-2018 Antmicro
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
            try
            {
                result = Parse(input, outputType);
                return true;
            }
            catch(TargetInvocationException)
            {
                result = null;
                return false;
            }
        }

        public object Parse(string input, Type outputType)
        {
            if(outputType == typeof(string))
            {
                return input;
            }
            var underlyingType = Nullable.GetUnderlyingType(outputType);
            if(underlyingType != null)
            {
                return input == null ? null : Parse(input, underlyingType);
            }
            if(outputType.IsEnum)
            {
                return Enum.Parse(outputType, input);
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
                        TryGetParseMethodDelegate(outputType, new[] { typeof(string), typeof(NumberStyles), typeof(CultureInfo) }, new object[] { NumberStyles.HexNumber, CultureInfo.InvariantCulture }, out Delegate result);
                        return result;
                    }
                );
            }
            else
            {
                parser = GetFromCacheOrAdd(
                    cache,
                    outputType,
                    () =>
                    {
                        if(!TryGetParseMethodDelegate(outputType, new[] { typeof(string), typeof(CultureInfo) }, new object[] { CultureInfo.InvariantCulture }, out Delegate result)
                          && !TryGetParseMethodDelegate(outputType, new[] { typeof(string) }, new object[0], out result))
                        {
                            result = null;
                        }
                        return result;
                    }
                );
            }
            return parser.DynamicInvoke(input);
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
        }

        private Delegate GetFromCacheOrAdd(Dictionary<Type, Delegate> cacheDict, Type outputType, Func<Delegate> function)
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

        private readonly Dictionary<Type, Delegate> cache;
        private readonly Dictionary<Type, Delegate> hexCache;
    }
}

