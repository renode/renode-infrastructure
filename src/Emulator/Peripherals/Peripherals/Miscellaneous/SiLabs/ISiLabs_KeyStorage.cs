//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public interface ISiLabs_KeyStorage
    {
        byte[] GetKey(uint slot);

        void AddKey(uint slot, byte[] key);

        void RemoveKey(uint slot);

        bool ContainsKey(uint slot);
    }
}
