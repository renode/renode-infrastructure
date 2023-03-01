//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public static class EmulatedNetworkServiceExtensions
    {
        public static void CreateEmulatedNetworkService(this Emulation emulation, string name, string typeName, string host, ushort port, string args = "")
        {
            var type = TypeManager.Instance.TryGetTypeByName(typeName);
            if(type == null)
            {
                throw new RecoverableException($"Type '{typeName}' not found when creating emulated network service");
            }

            try
            {
                var service = (IEmulatedNetworkService)Activator.CreateInstance(type, host, port, args);
                emulation.ExternalsManager.AddExternal(service, name);
            }
            catch(Exception e)
            {
                throw new RecoverableException("Failed to create emulated network service", e);
            }
        }
    }
}
