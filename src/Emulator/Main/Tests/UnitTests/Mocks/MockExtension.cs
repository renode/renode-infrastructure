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
    }
}
