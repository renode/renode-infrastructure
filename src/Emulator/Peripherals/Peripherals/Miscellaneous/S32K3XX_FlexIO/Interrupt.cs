//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel
{
    public class Interrupt
    {
        public static IReadOnlyList<Interrupt> BuildRegisters(IProvidesRegisterCollection<DoubleWordRegisterCollection> owner, int count, string name, System.Enum flagOffset, System.Enum enableOffset)
        {
            var flagRegister = flagOffset.Define(owner)
                .WithReservedBits(count, 32 - count);
            var enableRegister = enableOffset.Define(owner)
                .WithReservedBits(count, 32 - count);

            return Enumerable.Range(0, count).Select(index =>
                {
                    Interrupt interrupt = null;
                    var flag = flagRegister.DefineFlagField(index, FieldMode.Read | FieldMode.WriteOneToClear, name: $"{name}Flag",
                        changeCallback: (prev, val) => interrupt.OnFlagChange(prev, val)
                    );
                    var enable = enableRegister.DefineFlagField(index, name: $"{name}Enable",
                        changeCallback: (prev, val) => interrupt.OnMaskChange(prev, val)
                    );
                    interrupt = new Interrupt(flag, enable);
                    return interrupt;
                }
            ).ToList().AsReadOnly();
        }

        public void Reset()
        {
            isFlagUnchangeable = false;
        }

        public void SetFlag(bool value, bool unchangeableFlag = false)
        {
            isFlagUnchangeable = unchangeableFlag;
            if(flag.Value != value)
            {
                flag.Value = value;
                HandleChange(!value, mask.Value);
            }
        }

        public bool MaskedFlag => flag.Value && mask.Value;

        public event Action<bool> MaskedFlagChanged;

        private Interrupt(IFlagRegisterField flag, IFlagRegisterField mask)
        {
            this.flag = flag;
            this.mask = mask;
        }

        private void OnFlagChange(bool previousValue, bool value)
        {
            if(isFlagUnchangeable)
            {
                flag.Value = previousValue;
                return;
            }
            HandleChange(previousValue, mask.Value);
        }

        private void OnMaskChange(bool previousValue, bool value)
        {
            HandleChange(flag.Value, previousValue);
        }

        private void HandleChange(bool previousFlag, bool previousMask)
        {
            var previousMasked = previousFlag && previousMask;
            if(previousMasked != MaskedFlag)
            {
                MaskedFlagChanged?.Invoke(MaskedFlag);
            }
        }

        private bool isFlagUnchangeable;
        private readonly IFlagRegisterField flag;
        private readonly IFlagRegisterField mask;
    }
}
