//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class CommaToken : Token
    {
        public CommaToken(string token) : base(token)
        {
        }

        public override object GetObjectValue()
        {
            return OriginalValue;
        }

        public override string ToString()
        {
            return string.Format("[CommaToken]");
        }
    }
}

