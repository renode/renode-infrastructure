//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Utilities
{
    public class VmemReader
    {
        // Implementation based on section 17.2.9 of
        // http://staff.ustc.edu.cn/~songch/download/IEEE.1364-2005.pdf
        public VmemReader(ReadFilePath file)
        {
            this.file = file;
        }

        public IEnumerable<Tuple<long, ulong>> GetIndexDataPairs()
        {
            return GetMappedEnumerable(data =>
            {
                if(UInt64.TryParse(data, NumberStyles.HexNumber, null, out var l))
                {
                    return l;
                }
                else
                {    
                    throw new RecoverableException($"Invalid hexstring \"{data}\" at line {lineNumber}");
                }
            });
        }

        private IEnumerable<Tuple<long, string>> GetRawEnumerable()
        {
            if(started)
            {
                yield break;
            }
            else
            {
                started = true;
            }
            var matchEvaluator = new MatchEvaluator(MatchEvaluatorFunction);

            foreach(var line in File.ReadLines(file))
            {
                lineNumber += 1;
                string dataLine;
                if(inMultilineComment)
                {
                    var endOfComment = line.IndexOf("*/");
                    if(line.Length > 0 && endOfComment != -1)
                    {
                        inMultilineComment = false;
                        dataLine = line.Substring(endOfComment + 2);
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    dataLine = regexComments.Replace(line, matchEvaluator);
                }

                foreach(var e in ParseCommentless(dataLine))
                {
                    yield return e;
                }
            }
        }

        private IEnumerable<Tuple<long, T>> GetMappedEnumerable<T>(Func<string, T> mapping)
        {
            return GetRawEnumerable().Select(e => new Tuple<long, T>(e.Item1, mapping(e.Item2)));
        }

        private string MatchEvaluatorFunction(Match match)
        {
            if(match.Length >= 2 && match.Value[1] == '*' && (match.Length == 2 || match.Value[match.Length - 2] != '*'))
            {
                inMultilineComment = true;
            }
            return " ";
        }

        private IEnumerable<Tuple<long, string>> ParseCommentless(string sub)
        {
            var words = sub.Split(whitespaces, StringSplitOptions.RemoveEmptyEntries);
            foreach(var data in CheckForIndex(words))
            {
                yield return new Tuple<long, string>(index, data);
                index += 1;
            }
        }

        private IEnumerable<string> CheckForIndex(IEnumerable<string> words)
        {
            foreach(var word in words)
            {
                if(word.StartsWith("@"))
                {
                    if(Int64.TryParse(word.Substring(1), NumberStyles.HexNumber, null, out var idx))
                    {
                        index = idx;
                    }
                    else
                    {
                        throw new RecoverableException($"Invalid index \"{word}\" at line {lineNumber}");
                    }
                }
                else
                {
                    yield return word;
                }
            }
        }

        private static readonly char[] whitespaces = new char[] { ' ', '\n', '\t', '\f' };
        private static readonly Regex regexComments = new Regex(@"/\*(.*?\*/|.*)|//.*");

        private bool started;
        private bool inMultilineComment;
        private long index;
        private long lineNumber;

        private readonly string file;
    }
}
