//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
// 

using System.Collections.Generic;
using System.Globalization;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class ScalarSample : SensorSample
    {
        public ScalarSample()
        {
        }

        public ScalarSample(decimal value)
        {
            Value = value;
        }

        public override void Load(IList<decimal> data)
        {
            if(data.Count != Dimensions)
            {
                throw new Exceptions.RecoverableException($"Tried to create a {Dimensions}-dimensional ScalarSample using {data.Count} values");
            }

            Value = data[0];
        }

        public override bool TryLoad(params string[] data)
        {
            var value = 0m;

            var result = data.Length == 1
                    && decimal.TryParse(data[0], NumberStyles.Any, CultureInfo.InvariantCulture, out value);

            Value = value;
            return result;
        }

        public override string ToString()
        {
            return $"[Value: {Value}]";
        }

        public decimal Value { get; set; }

        public const int Dimensions = 1;
    }
}
