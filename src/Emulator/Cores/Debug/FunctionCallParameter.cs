//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Debug
{
    public struct FunctionCallParameter
    {
        static FunctionCallParameter()
        {
            IgnoredParameter = new FunctionCallParameter { Type = FunctionCallParameterType.Ignore };
        }

        public FunctionCallParameterType Type;
        public int NumberOfElements;

        public static FunctionCallParameter IgnoredParameter;
    }
    
}
