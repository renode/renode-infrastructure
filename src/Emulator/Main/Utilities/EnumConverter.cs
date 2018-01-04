//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq.Expressions;

namespace Antmicro.Renode.Utilities
{
    static class EnumConverter<TEnum> where TEnum : struct, IConvertible
    {
        public static readonly Func<uint, TEnum> ToEnum = GenerateEnumConverter();

        public static readonly Func<TEnum, uint> ToUInt = GenerateLongConverter();

        private static Func<uint, TEnum> GenerateEnumConverter()
        {
            var parameter = Expression.Parameter(typeof(uint));
            var dynamicMethod = Expression.Lambda<Func<uint, TEnum>>(
                Expression.Convert(parameter, typeof(TEnum)),
                parameter);
            return dynamicMethod.Compile();
        }

        private static Func<TEnum, uint> GenerateLongConverter()
        {
            var parameter = Expression.Parameter(typeof(TEnum));
            var dynamicMethod = Expression.Lambda<Func<TEnum, uint>>(
                Expression.Convert(parameter, typeof(uint)),
                parameter);
            return dynamicMethod.Compile();
        }
    }

}

