//
// Copyright (c) 2010-2023 Antmicro
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
        public static readonly Func<ulong, TEnum> ToEnum = GenerateEnumConverter();

        public static readonly Func<TEnum, uint> ToUInt32 = GenerateUInt32Converter();

        public static readonly Func<TEnum, ulong> ToUInt64 = GenerateUInt64Converter();

        private static Func<ulong, TEnum> GenerateEnumConverter()
        {
            var parameter = Expression.Parameter(typeof(ulong));
            var dynamicMethod = Expression.Lambda<Func<ulong, TEnum>>(
                Expression.Convert(parameter, typeof(TEnum)),
                parameter);
            return dynamicMethod.Compile();
        }

        private static Func<TEnum, uint> GenerateUInt32Converter()
        {
            var parameter = Expression.Parameter(typeof(TEnum));
            var dynamicMethod = Expression.Lambda<Func<TEnum, uint>>(
                Expression.Convert(parameter, typeof(uint)),
                parameter);
            return dynamicMethod.Compile();
        }

        private static Func<TEnum, ulong> GenerateUInt64Converter()
        {
            var parameter = Expression.Parameter(typeof(TEnum));
            var dynamicMethod = Expression.Lambda<Func<TEnum, ulong>>(
                Expression.Convert(parameter, typeof(ulong)),
                parameter);
            return dynamicMethod.Compile();
        }
    }
}
