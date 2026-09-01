//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Tools.Network;

namespace Antmicro.Renode.Peripherals.CAN;

public class CANBackend : IAnalyzableBackend<CANHub>
{
    public void Attach(CANHub emulationElement)
    {
        CANHub = emulationElement;
    }

    public void Detach()
    {
        CANHub = null;
    }

    public void Reset()
    {
        // Intentionally empty, we want to keep the connection on machine reset
    }

    public IAnalyzable AnalyzableElement => CANHub;

    public CANHub CANHub { get; private set; }

    [PostDeserializationAttribute]
    private void ReAttach()
    {
        if(CANHub != null)
        {
            Attach(CANHub);
        }
    }
}
