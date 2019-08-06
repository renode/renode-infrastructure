//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities
{
    public class BitPatternDetector
    {
        public BitPatternDetector(int width) : this(new bool[width])
        {
        }

        public BitPatternDetector(bool[] resetValue)
        {
            this.resetValue = resetValue;
            this.previousState = new bool[resetValue.Length];
            items = new List<Item>();

            Reset();
        }

        public void Reset()
        {
            Array.Copy(resetValue, previousState, previousState.Length);
        }

        public int AcceptState(bool[] state)
        {
            var result = -1;

            for(var i = 0; i < items.Count; i++)
            {
                if(items[i].patternDecoder(state, previousState))
                {
                    if(items[i].action != null)
                    {
                        items[i].action(state);
                    }
                    result = i;
                    break;
                }
            }

            Array.Copy(state, previousState, previousState.Length);
            return result;
        }

        public int RegisterPatternHandler(Func<bool[], bool[], bool> patternDecoder, Action<bool[]> action = null)
        {
            items.Add(new Item { patternDecoder = patternDecoder, action = action });
            return items.Count - 1;
        }

        private readonly List<Item> items;

        private readonly bool[] previousState;
        private readonly bool[] resetValue;

        private struct Item
        {
            public Func<bool[], bool[], bool> patternDecoder;
            public Action<bool[]> action;
        }
    }
}
