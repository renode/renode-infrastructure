//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.UserInterface
{

	public class TokenizationResult
	{
		public TokenizationResult(int unmatchedCharactersLeft, IEnumerable<Token> tokens, RecoverableException e)
		{
			UnmatchedCharactersLeft = unmatchedCharactersLeft;
			Tokens = tokens;
            Exception = e;
		}

		public int UnmatchedCharactersLeft { get; private set; }
		public IEnumerable<Token> Tokens { get; private set; }
        public RecoverableException Exception { get; private set; }

        public override string ToString()
        {
            return String.Join("", Tokens.Select(x => x.ToString())) + ((UnmatchedCharactersLeft != 0) ? String.Format(" (unmatched characters: {0})", UnmatchedCharactersLeft) : ""
                                                                        + Exception != null ? String.Format(" Exception message: {0}",Exception.Message):"");
        }
	}

}
