//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
namespace Antmicro.Renode.Utilities
{
    public static class MockExtension
    {
        public static string GetMockString(this Emulation str)
        {
            return "this is an extension";
        }

        public static string MethodThatTakesParamsIntArray(this Emulation emulation, params int[] args)
        {
            return string.Format("args: [{0}]", string.Join(", ", args));
        }

        public static string MethodThatTakesParamsIntArrayAfterString(this Emulation emulation, string arg, params int[] args)
        {
            return string.Format("arg: {0} args: [{1}]", arg, string.Join(", ", args));
        }

        public static string MethodThatTakesParamsIntArrayAfterOptionalString(this Emulation emulation, string arg = "default", params int[] args)
        {
            return string.Format("arg: {0} args: [{1}]", arg, string.Join(", ", args));
        }

        public static string MethodWithOptionalParameters(this Emulation emulation, int a = 1, int b = 2, int c = 3)
        {
            return string.Format("a: {0}, b: {1}, c: {2}", a, b, c);
        }

        public static string MethodWithOptionalParametersAndParamArray(this Emulation emulation, int a = 1, int b = 2, int c = 3, params int[] args)
        {
            return string.Format("a: {0}, b: {1}, c: {2}; {3}", a, b, c, string.Join(", ", args));
        }

        public static string MethodWithArrayThenString(this Emulation emulation, int[] numbers, string separator)
        {
            return string.Format("numbers: [{0}] separator: {1}", string.Join(", ", numbers), separator);
        }

        public static string MethodWithListOfStrings(this Emulation emulation, List<string> words)
        {
            var content = string.Join(", ", words.Select(w => $"\"{w}\""));
            return string.Format("words: [{0}]", content);
        }

        public static string MethodWithParamsStringArray(this Emulation emulation, params string[] args)
        {
            var content = args.Any()
                ? "\"" + string.Join("\", \"", args) + "\""
                : "";
            return string.Format("args: [{0}]", content);
        }

        public static string MethodWithNullableIntArray(this Emulation emulation, int?[] numbers)
        {
            var stringified = numbers.Select(n => n.HasValue ? n.Value.ToString() : "null");
            return string.Format("numbers: [{0}]", string.Join(", ", stringified));
        }

        public static string MethodWithNullableIntList(this Emulation emulation, List<int?> numbers)
        {
            return MethodWithNullableIntArray(emulation, numbers.ToArray());
        }

        public static string MethodWithStringThenIntArrayThenAnotherString(this Emulation emulation, string s1, int[] numbers, string s2)
        {
            return $"s1:{s1} numbers:[{string.Join(",", numbers)}] s2:{s2}";
        }
    }
}
