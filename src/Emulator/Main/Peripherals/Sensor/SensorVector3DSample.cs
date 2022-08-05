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
    public class Vector3DSample : SensorSample
    {
        public Vector3DSample()
        {
        }

        public Vector3DSample(decimal x, decimal y, decimal z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override void Load(IList<decimal> data)
        {
            if(data.Count != Dimensions)
            {
                throw new Exceptions.RecoverableException($"Tried to create a {Dimensions}-dimensional Vector3DSample using {data.Count} values");
            }

            X = data[0];
            Y = data[1];
            Z = data[2];
        }

        public override bool TryLoad(params string[] data)
        {
            var x = 0m;
            var y = 0m;
            var z = 0m;

            var result = data.Length == 3
                    && decimal.TryParse(data[0], NumberStyles.Any, CultureInfo.InvariantCulture, out x)
                    && decimal.TryParse(data[1], NumberStyles.Any, CultureInfo.InvariantCulture, out y)
                    && decimal.TryParse(data[2], NumberStyles.Any, CultureInfo.InvariantCulture, out z);

            X = x;
            Y = y;
            Z = z;

            return result;
        }

        public override string ToString()
        {
            return $"[X: {X}, Y: {Y}, Z: {Z}]";
        }

        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Z { get; set; }

        public const int Dimensions = 3;
    }
}
