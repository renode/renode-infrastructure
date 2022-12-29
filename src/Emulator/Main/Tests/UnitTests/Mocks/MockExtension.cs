//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Utilities
{
    public static class MockExtension
    {
        public static string GetMockString(this IInterestingType str)
        {
            return "this is an extension";
        }

        public static string MethodThatTakesParamsIntArray(this IInterestingType emulation, params int[] args)
        {
            return string.Format("args: [{0}]", string.Join(", ", args));
        }

        public static string MethodThatTakesParamsIntArrayAfterString(this IInterestingType emulation, string arg, params int[] args)
        {
            return string.Format("arg: {0} args: [{1}]", arg, string.Join(", ", args));
        }

        public static string MethodThatTakesParamsIntArrayAfterOptionalString(this IInterestingType emulation, string arg = "default", params int[] args)
        {
            return string.Format("arg: {0} args: [{1}]", arg, string.Join(", ", args));
        }
    }
}
