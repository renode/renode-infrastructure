//
// Copyright (c) 2010-2026 Antmicro
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
                        changeCallback: (prev, _) => interrupt.OnFlagChange(prev)
                    );
                    var enable = enableRegister.DefineFlagField(index, name: $"{name}Enable",
                        changeCallback: (prev, _) => interrupt.OnMaskChange(prev)
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

        public bool Flag => flag.Value;

        public event Action<bool> MaskedFlagChanged;

        public event Action<bool> FlagChanged;

        private Interrupt(IFlagRegisterField flag, IFlagRegisterField mask)
        {
            this.flag = flag;
            this.mask = mask;
        }

        private void OnFlagChange(bool previousValue)
        {
            if(isFlagUnchangeable)
            {
                flag.Value = previousValue;
                return;
            }
            HandleChange(previousValue, mask.Value);
        }

        private void OnMaskChange(bool previousValue)
        {
            HandleChange(flag.Value, previousValue);
        }

        private void HandleChange(bool previousFlag, bool previousMask)
        {
            var previousMasked = previousFlag && previousMask;
            if(previousFlag != flag.Value)
            {
                FlagChanged?.Invoke(flag.Value);
            }
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
