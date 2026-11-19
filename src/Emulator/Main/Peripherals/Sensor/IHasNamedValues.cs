//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Sensor;

public interface IHasNamedValues
{
    IDictionary<string, object> NamedValues { get; }

    string ActiveNamedValue { get; set; }
}
