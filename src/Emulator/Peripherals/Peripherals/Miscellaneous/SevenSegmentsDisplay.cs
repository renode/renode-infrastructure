//
// Copyright (c) 2020 LabMICRO FACET UNT
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;
using System.Collections.Generic;
using System.Text;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // This class implements an multiplexed seven segment display with a variable number of digits 
    // First eighths input gpio lines are used to handle from "a" to "f" segments and dot point
    // Remaining input gpio lines are used to enable each of digits starting from left
    // To activate a segment, the digit and segment gpio inputs must be set to true
    // Anode and catode common displays can be emulated with invertSegments and invertDigits parameters

    public class SevenSegmentsDisplay : IGPIOReceiver
    {
        public SevenSegmentsDisplay(uint digitsCount = 1, bool invertSegments = false, bool invertDigits = false)
        {
            this.invertSegments = invertSegments;
            this.invertDigits = invertDigits;

            digit = new Digit();
            enabledDigits = new bool[digitsCount];

            sync = new object();
            Reset();
        }

        public void Reset()
        {
            digit.Clear();
            for(var index = 0; index < enabledDigits.Length; index++)
            {
                enabledDigits[index] = invertDigits;
            }

            Update();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number >= 0 && number < SegmentsCount)
            {
                digit.SetSegment((Segments)(1 << number), invertSegments ? !value : value);
            }
            else if(number >= SegmentsCount && number - SegmentsCount < enabledDigits.Length)
            {
                enabledDigits[number - SegmentsCount] = invertDigits ? !value : value;
            }
            else
            {
                this.Log(LogLevel.Error, "This device can handle GPIOs in range 0 - {0}, but {1} was set", SegmentsCount + enabledDigits.Length, number);
                return;
            }

            Update();
        }

        [field: Transient]
        public event Action<IPeripheral, string> StateChanged;

        public string Image { get; private set; }

        public string State { get; private set; }
        
        private void Update()
        {
            lock(sync)
            {              
                var newState = AsSegmentsString();
                if(newState == State)
                {
                    return;
                }

                State = newState;
                Image = AsPrettyString();

                StateChanged?.Invoke(this, State);

                this.Log(LogLevel.Noisy, "Seven Segments state changed to {0} {1}", State, Image);
            }
        }

        private string AsPrettyString()
        {
            var result = new StringBuilder();

            result.Append("(");
            foreach(var isEnabled in enabledDigits)
            {
                result.Append(isEnabled
                    ? digit.AsString()
                    : "_");
            }
            result.Append(")");

            return result.ToString();
        }

        private string AsSegmentsString()
        {
            var result = new StringBuilder();

            foreach(var isEnabled in enabledDigits)
            {
                result.Append("[");
                if(isEnabled)
                {
                    result.Append(digit.Value.ToString());
                }
                result.Append("]");
            }
            return result.ToString();
        }

        private readonly Digit digit;
        private readonly bool[] enabledDigits;

        private readonly bool invertSegments;
        private readonly bool invertDigits;
        private readonly object sync;

        private const int SegmentsCount = 8;

        [Flags]
        private enum Segments
        {
            A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2,
            D = 1 << 3,
            E = 1 << 4,
            F = 1 << 5,
            G = 1 << 6,
            DOT = 1 << 7
        }

        private class Digit
        {
            public Segments Value { get; private set; }

            public void SetSegment(Segments segment, bool asOn)
            {
                if(asOn)
                {
                    Value |= segment;
                }
                else
                {
                    Value &= ~segment;
                }
            }

            public void Clear()
            {
                Value = 0;
            }

            public string AsString()
            {
                var hasDot = (Value & Segments.DOT) == Segments.DOT;

                if(!SegmentsToStringMapping.TryGetValue(Value & ~Segments.DOT, out var result))
                {
                    result = "?";
                }

                if(hasDot)
                {
                    result += ".";
                }

                return result;
            }

            private static readonly Dictionary<Segments, string> SegmentsToStringMapping = new Dictionary<Segments, string>()
            {
                { Segments.A | Segments.B | Segments.C | Segments.D | Segments.E | Segments.F             , "0" },
                {              Segments.B | Segments.C                                                    , "1" },
                { Segments.A | Segments.B |              Segments.D | Segments.E |              Segments.G, "2" },
                { Segments.A | Segments.B | Segments.C | Segments.D |                           Segments.G, "3" },
                {              Segments.B | Segments.C |                           Segments.F | Segments.G, "4" },
                { Segments.A |              Segments.C | Segments.D |              Segments.F | Segments.G, "5" },
                { Segments.A |              Segments.C | Segments.D | Segments.E | Segments.F | Segments.G, "6" },
                { Segments.A | Segments.B | Segments.C                                                    , "7" },
                { Segments.A | Segments.B | Segments.C | Segments.D | Segments.E | Segments.F | Segments.G, "8" },
                { Segments.A | Segments.B | Segments.C | Segments.D |              Segments.F | Segments.G, "9" },
                { Segments.A | Segments.B | Segments.C |              Segments.E | Segments.F | Segments.G, "A" },
                {                           Segments.C | Segments.D | Segments.E | Segments.F | Segments.G, "B" },
                { Segments.A |                           Segments.D | Segments.E | Segments.F             , "C" },
                {              Segments.B | Segments.C | Segments.D | Segments.E |              Segments.G, "D" },
                { Segments.A |                           Segments.D | Segments.E | Segments.F | Segments.G, "E" },
                { Segments.A |                                        Segments.E | Segments.F | Segments.G, "F" },
            };
        }
    }
}

