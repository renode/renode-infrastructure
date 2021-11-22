//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using System.Linq;
using System.IO;
using System.Collections.Generic;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
    public static class RiscvOpcodesExtensions
    {
        public static void EnableRiscvOpcodesCounting(this BaseRiscV cpu, string file)
        {
            foreach(var x in RiscVOpcodesParser.Parse(file))
            {
                cpu.InstallOpcodeCounterPattern(x.Item1, x.Item2);
            }

            cpu.EnableOpcodesCounting = true;
        }
    
        public static class RiscVOpcodesParser
        {
            public static IEnumerable<Tuple<string, string>> Parse(string file)
            {
                var result = new List<Tuple<string, string>>();
                
                try
                {
                    foreach(var line in File.ReadLines(file).Select(x => RemoveComments(x)))
                    {
                        if(line.Length == 0)
                        {
                            continue;
                        }
                        
                        result.Add(ParseLine(line));
                    }
                }
                catch(IOException e)
                {
                    throw new RecoverableException($"There was na error when parsing RISC-V opcodes from {file}:\n{e.Message}");
                }
                
                return result;
            }

            private static string RemoveComments(string line)
            {
                var pos = line.IndexOf("#");
                if(pos != -1)
                {
                    line = line.Remove(pos);
                }
                return line.Trim();
            }

            private static Tuple<string, string> ParseLine(string lineContent, int opcodeLength = 32)
            {
                var pattern = new StringBuilder(new String('_', opcodeLength));
                
                var elems = lineContent.Split(LineSplitPatterns, StringSplitOptions.RemoveEmptyEntries);
                if(elems.Length < 2)
                {
                    throw new RecoverableException($"Couldn't split line: {lineContent}");
                }

                var instructionName = elems[0];
                foreach(var elem in elems.Skip(1))
                {
                    var parts = elem.Split(PartSplitPatterns);
                    if(parts.Length != 2)
                    {
                        // let's ignore all non-explicit ranges
                        continue;
                    }

                    if(!BitsRange.TryParse(parts[0], out var range))
                    {
                        throw new RecoverableException($"Couldn't parse range: {parts[0]}");
                    }

                    if(!BitsValue.TryParse(parts[1], out var value))
                    {
                        throw new RecoverableException($"Couldn't parse value: {parts[1]}");
                    }

                    if(!range.TryApply(pattern, value))
                    {
                        throw new RecoverableException($"Couldn't apply value {value} in range {range} to pattern {pattern}");
                    }
                }

                return Tuple.Create(instructionName, pattern.ToString());
            }
            
            private static readonly char[] PartSplitPatterns = new [] { '=' };
            private static readonly char[] LineSplitPatterns = new [] { ' ', '\t' };
            private static readonly string[] BitRangeSplitPatterns = new [] { ".." };

            private struct BitsValue
            {
                public static bool TryParse(string s, out BitsValue bv)
                {
                    if(s == "ignore")
                    {
                        bv = new BitsValue(-1, ignored: true);
                        return true;
                    }
                    
                    if(SmartParser.Instance.TryParse(s, typeof(int), out var result))
                    {
                        bv = new BitsValue((int)result);
                        return true;
                    }
                    
                    bv = new BitsValue(-1);
                    return false;
                }

                public bool TryGetBinaryPattern(int length, out string result)
                {
                    if(Ignored)
                    {
                        result = new String('x', length);
                        return true;
                    }
                    
                    result = Convert.ToString(Value, 2);
                    if(result.Length > length)
                    {
                        return false;
                    }

                    result = result.PadLeft(length, '0');
                    return true;
                }

                private BitsValue(int value, bool ignored = false)
                {
                    Value = value;
                    Ignored = ignored;
                }

                public int Value { get; }
                public bool Ignored { get; }

                public override string ToString()
                {
                    return $"[BitsValue: {Value}, ignored: {Ignored}]";
                }
            }

            private struct BitsRange
            {
                // There are two expected formats:
                // * hi..lo (hi, lo being integers)
                // * bit (bit being an integer; treated as bit..bit)
                public static bool TryParse(string s, out BitsRange br)
                {
                    var x = s.Split(BitRangeSplitPatterns, StringSplitOptions.None);
                    if(x.Length == 1)
                    {
                        if(int.TryParse(x[0], out var bit))
                        {
                            br = new BitsRange(bit, bit);
                            return true;
                        }
                    }
                    else if(x.Length == 2)
                    {
                        if(int.TryParse(x[0], out var upperBit)
                            && int.TryParse(x[1], out var lowerBit))
                        {
                            br = new BitsRange(lowerBit, upperBit);
                            return true;
                        }
                    }

                    br = new BitsRange(-1, -1);
                    return false;
                }

                public bool TryApply(StringBuilder s, BitsValue v)
                {
                    if(!v.TryGetBinaryPattern(this.Width, out var pattern))
                    {
                        return false;
                    }

                    if(Higher >= s.Length)
                    {
                        return false;
                    }

                    var pIdx = pattern.Length - 1;
                    var sIdx = s.Length - Lower - 1;
                    
                    for(var i = Lower; i <= Higher; i++, pIdx--, sIdx--)
                    {
                        if(s[sIdx] == '1' || s[sIdx] == '0')
                        {
                            return false;
                        }

                        s[sIdx] = pattern[pIdx];
                    }
                    
                    return true;
                }
                
                public override string ToString()
                {
                    return $"[BitsRange: {Lower} - {Higher}]";
                }

                private BitsRange(int lower, int higher)
                {
                    Lower = lower;
                    Higher = higher;
                }

                public int Lower { get; }
                public int Higher { get; }

                public int Width => Higher - Lower + 1;
            }
        }
    }
}
