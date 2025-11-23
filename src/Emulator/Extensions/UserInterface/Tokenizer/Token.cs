//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public abstract class Token
    {
        public abstract object GetObjectValue();

        public string OriginalValue { get; protected set; }

        protected Token(string originalValue)
        {
            OriginalValue = originalValue;
        }
    }
}