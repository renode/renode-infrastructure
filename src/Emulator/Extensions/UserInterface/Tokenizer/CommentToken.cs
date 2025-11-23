//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.UserInterface.Tokenizer
{
    public class CommentToken : Token
    {
        public CommentToken(string value) : base(value)
        {
            Value = value.TrimStart('#');
        }

        public override object GetObjectValue()
        {
            return Value;
        }

        public override string ToString()
        {
            return string.Format("[CommentToken: Value={0}]", Value);
        }

        public string Value { get; private set; }
    }
}