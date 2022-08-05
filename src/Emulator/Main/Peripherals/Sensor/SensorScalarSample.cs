//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
// 

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
    }
}
