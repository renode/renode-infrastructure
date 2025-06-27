//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.UserInterface.Tokenizer
{

    public class Tokenizer
    {
        public static Tokenizer CreateTokenizer()
        {
            var tokenizer = new Tokenizer();
            // comment
            tokenizer.AddToken(new Regex(@"^\#.*"), x => new CommentToken(x));

            tokenizer.AddToken(new Regex(@"^:.*"), x => new CommentToken(x));

            // execution
            tokenizer.AddToken(new Regex(@"^`.*?`"), x => new ExecutionToken(x));

            // variable
            tokenizer.AddToken(new Regex(@"^\$([0-9]|(?i:[a-z])|_|\.)+"), x => new VariableToken(x));

            // left brace
            tokenizer.AddToken(new Regex(@"^\["), x => new LeftBraceToken(x));

            // right brace
            tokenizer.AddToken(new Regex(@"^\]"), x => new RightBraceToken(x));

            //multiline string
            tokenizer.AddToken(new Regex(@"^"""""".+?""""""", RegexOptions.Singleline), x => new MultilineStringToken(x));

            //multiline terminator
            tokenizer.AddToken(new Regex(@"^"""""""), x => new MultilineStringTerminatorToken(x));

            // string
            tokenizer.AddToken(new Regex(@"^'[^'\\]*(?:\\.[^'\\]*)*'"), x => new StringToken(x));
            tokenizer.AddToken(new Regex(@"^""[^""\\]*(?:\\.[^""\\]*)*"""), x => new StringToken(x));

            // absolute range
            tokenizer.AddToken(new Regex(@"^<\s*((0x([0-9]|(?i:[a-f]))+)|([+-]?\d+))\s*,\s*((0x([0-9]|(?i:[a-f]))+)|([+-]?\d+))\s*>"), x => new AbsoluteRangeToken(x));

            // relative range
            tokenizer.AddToken(new Regex(@"^<\s*((0x([0-9]|(?i:[a-f]))+)|([+-]?\d+))\s+((0x([0-9]|(?i:[a-f]))+)|(\+?\d+))\s*>"), x => new RelativeRangeToken(x));

            // path
            tokenizer.AddToken(new Regex(@"^\@(?:(?!;)((\\ )|\S))+"), x => new PathToken(x));

            // hex number
            tokenizer.AddToken(new Regex(@"^0x([0-9]|(?i:[a-f]))+"), x => new HexToken(x));

            // float number
            tokenizer.AddToken(new Regex(@"^[+-]?((\d+\.(\d*)?))"), x => new FloatToken(x));

            // integer
            tokenizer.AddToken(new Regex(@"^[+-]?\d+"), x => new DecimalIntegerToken(x));

            // boolean ignore case
            tokenizer.AddToken(new Regex(@"^(?i)(true|false)"), x => new BooleanToken(x));

            // "null"
            tokenizer.AddToken(new Regex(@"^null"), x => new NullToken(x));

            // "="
            tokenizer.AddToken(new Regex(@"^="), x => new EqualityToken(x));

            // "?="
            tokenizer.AddToken(new Regex(@"^\?="), x => new ConditionalEqualityToken(x));

            // ";" or new line
            tokenizer.AddToken(new Regex(@"^\;"), x => new CommandSplit(x));

            // ","
            tokenizer.AddToken(new Regex(@"^,"), x => new CommaToken(x));

            // literal
            tokenizer.AddToken(new Regex(@"^[\w\.\-\?]+"), x => new LiteralToken(x));

            // whitespace
            tokenizer.AddToken(new Regex(@"^\s+"), x => null);
            return tokenizer;
        }

        private Tokenizer()
        {
            tokens = new List<InternalToken>();
        }

        public TokenizationResult Tokenize(string input)
        {
            var producedTokens = new List<Token>();
            RecoverableException exception = null;
            while(input.Length > 0)
            {
                var success = false;
                foreach(var proposition in tokens)
                {
                    var regex = proposition.ApplicabilityCondition;
                    var match = regex.Match(input);
                    if(!match.Success)
                    {
                        continue;
                    }
                    success = true;
                    Token producedToken;
                    try
                    {
                        producedToken = proposition.Factory(input.Substring(0, match.Length));
                    }
                    catch(RecoverableException e)
                    {
                        success = false;
                        exception = exception ?? e;
                        break;
                    }
                    input = input.Substring(match.Length);
                    if(producedToken != null)
                    {
                        producedTokens.Add(producedToken);
                    }
                    break;
                }
                if(!success)
                {
                    break;
                }
            }
            return new TokenizationResult(input.Length, producedTokens, exception);
        }

        public void AddToken(Regex applicabilityCondition, Func<string, Token> factory)
        {
            tokens.Add(new InternalToken(applicabilityCondition, factory));
        }

        private readonly List<InternalToken> tokens;

        private class InternalToken
        {
            public InternalToken(Regex applicabilityCondition, Func<string, Token> factory)
            {
                this.ApplicabilityCondition = applicabilityCondition;
                this.Factory = factory;
            }

            public Regex ApplicabilityCondition { get; private set; }
            public Func<string, Token> Factory { get; private set; }
        }
    }
}
