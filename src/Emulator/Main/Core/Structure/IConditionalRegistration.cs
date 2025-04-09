//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core.Structure
{
    public interface IConditionalRegistration : IBusRegistration
    {
        string Condition { get; }
        IConditionalRegistration WithInitiatorAndStateMask(IPeripheral initiator, StateMask mask);
    }
}

