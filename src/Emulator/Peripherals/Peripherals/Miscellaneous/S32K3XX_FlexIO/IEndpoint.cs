//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel
{
    public interface IEndpoint : IPeripheral
    {
        void RegisterInFlexIO(S32K3XX_FlexIO flexIO);
    }
}
