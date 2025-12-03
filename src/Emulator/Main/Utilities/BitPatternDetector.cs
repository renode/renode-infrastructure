//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Utilities
{
    public class BitPatternDetector
    {
        public BitPatternDetector(int width, IPeripheral loggingParent = null) : this(new bool[width], loggingParent)
        {
        }

        public BitPatternDetector(bool[] resetValue, IPeripheral loggingParent = null)
        {
            this.loggingParent = loggingParent;

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

            loggingParent?.Log(LogLevel.Noisy, "Accepting new state [{0}]; previous state [{1}]", string.Join(", ", state), string.Join(", ", previousState));

            for(var i = 0; i < items.Count; i++)
            {
                if(items[i].PatternDecoder(previousState, state))
                {
                    loggingParent?.Log(LogLevel.Noisy, "Pattern {0} decoded", items[i].Name);
                    if(items[i].Action != null)
                    {
                        items[i].Action(state);
                    }
                    result = i;
                    break;
                }
            }

            Array.Copy(state, previousState, previousState.Length);
            return result;
        }

        public int RegisterPatternHandler(Func<bool[], bool[], bool> patternDecoder, Action<bool[]> action = null, string name = null)
        {
            items.Add(new Item { PatternDecoder = patternDecoder, Action = action, Name = name });
            return items.Count - 1;
        }

        private readonly List<Item> items;
        private readonly IPeripheral loggingParent;

        private readonly bool[] previousState;
        private readonly bool[] resetValue;

        private struct Item
        {
            public Func<bool[], bool[], bool> PatternDecoder;
            public Action<bool[]> Action;
            public string Name;
        }
    }
}