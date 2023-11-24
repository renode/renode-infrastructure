//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
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
    }
}
