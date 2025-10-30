//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public interface SiLabs_IKeyStorage
    {
        byte[] GetKey(uint slot);

        void AddKey(uint slot, byte[] key);

        void RemoveKey(uint slot);

        bool ContainsKey(uint slot);
    }
}